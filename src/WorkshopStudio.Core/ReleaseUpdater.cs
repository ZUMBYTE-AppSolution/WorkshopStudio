using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace WorkshopStudio.Core;

public sealed record GitHubUpdateRelease(Version Version, string Tag, string Notes, Uri PackageUrl,
    Uri ChecksumsUrl, string PackageName, long PackageBytes);

/// <summary>Checks only the official Workshop Studio GitHub release and stages a verified portable package.</summary>
public sealed class ReleaseUpdater : IDisposable
{
    public const string Repository = "ZUMBYTE-AppSolution/WorkshopStudio";
    public static readonly Uri LatestApi = new($"https://api.github.com/repos/{Repository}/releases/latest");
    private readonly HttpClient _client;
    private readonly bool _ownsClient;
    private const long MaxPackageBytes = 512L * 1024 * 1024;

    public ReleaseUpdater(HttpClient? client = null)
    {
        _client = client ?? new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        _ownsClient = client is null;
        if (!_client.DefaultRequestHeaders.UserAgent.Any())
            _client.DefaultRequestHeaders.UserAgent.ParseAdd("WorkshopStudio-Updater/1.0");
        _client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
    }

    public async Task<GitHubUpdateRelease?> CheckLatestAsync(CancellationToken ct = default)
    {
        using var response = await _client.GetAsync(LatestApi, HttpCompletionOption.ResponseHeadersRead, ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        var bytes = await ReadLimitedAsync(response.Content, 2 * 1024 * 1024, ct);
        return ParseLatest(bytes);
    }

    public static GitHubUpdateRelease ParseLatest(byte[] json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var tag = root.GetProperty("tag_name").GetString() ?? "";
        if (!Regex.IsMatch(tag, @"^v(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)$", RegexOptions.CultureInvariant) ||
            root.GetProperty("draft").GetBoolean() || root.GetProperty("prerelease").GetBoolean() ||
            !Version.TryParse(tag[1..], out var version))
            throw new InvalidDataException("GitHub meldet kein gültiges stabiles Release.");
        var packageName = $"WorkshopStudio-{version}-win-x64.zip";
        var prefix = $"https://github.com/{Repository}/releases/download/{tag}/";
        Uri? zip = null, sums = null;
        long size = 0;
        foreach (var asset in root.GetProperty("assets").EnumerateArray())
        {
            var name = asset.GetProperty("name").GetString();
            var url = asset.GetProperty("browser_download_url").GetString();
            if (name is not ("SHA256SUMS.txt") && name != packageName) continue;
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
                !string.Equals(uri.AbsoluteUri, prefix + name, StringComparison.Ordinal))
                throw new InvalidDataException("Release-Asset liegt nicht am erwarteten GitHub-Pfad.");
            if (name == packageName) { zip = uri; size = asset.GetProperty("size").GetInt64(); }
            else sums = uri;
        }
        if (zip is null || sums is null || size is <= 0 or > MaxPackageBytes)
            throw new InvalidDataException("Release-Paket oder SHA256SUMS.txt fehlt bzw. ist zu groß.");
        return new(version, tag, root.TryGetProperty("body", out var body) ? body.GetString() ?? "" : "",
            zip, sums, packageName, size);
    }

    public async Task<string> DownloadAndStageAsync(GitHubUpdateRelease release, string updateRoot,
        IProgress<string>? progress = null, CancellationToken ct = default)
    {
        var stage = Path.Combine(Path.GetFullPath(updateRoot), release.Tag + "-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(stage);
        try
        {
            progress?.Report("Prüfsumme von GitHub laden …");
            using var sumsResponse = await _client.GetAsync(release.ChecksumsUrl, HttpCompletionOption.ResponseHeadersRead, ct);
            sumsResponse.EnsureSuccessStatusCode();
            var sums = System.Text.Encoding.UTF8.GetString(await ReadLimitedAsync(sumsResponse.Content, 64 * 1024, ct));
            var expectedHash = ParseChecksum(sums, release.PackageName);
            var archivePath = Path.Combine(stage, release.PackageName);
            progress?.Report("Windows-Paket herunterladen …");
            using (var zipResponse = await _client.GetAsync(release.PackageUrl, HttpCompletionOption.ResponseHeadersRead, ct))
            {
                zipResponse.EnsureSuccessStatusCode();
                if (zipResponse.Content.Headers.ContentLength is > MaxPackageBytes)
                    throw new InvalidDataException("Release-Paket ist zu groß.");
                await using var source = await zipResponse.Content.ReadAsStreamAsync(ct);
                await using var target = new FileStream(archivePath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                var buffer = new byte[1024 * 1024]; long total = 0;
                int read;
                while ((read = await source.ReadAsync(buffer, ct)) != 0)
                {
                    total += read;
                    if (total > MaxPackageBytes) throw new InvalidDataException("Release-Paket überschreitet die Größenbegrenzung.");
                    hash.AppendData(buffer, 0, read);
                    await target.WriteAsync(buffer.AsMemory(0, read), ct);
                }
                if (total != release.PackageBytes ||
                    !string.Equals(Convert.ToHexString(hash.GetHashAndReset()), expectedHash, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Release-Paket stimmt nicht mit Größe und SHA-256-Prüfsumme überein.");
            }
            progress?.Report("Geprüftes Paket vorbereiten …");
            var payload = Path.Combine(stage, "payload");
            ExtractPackage(archivePath, payload, release.Version);
            return payload;
        }
        catch
        {
            try { Directory.Delete(stage, true); } catch (IOException) { }
            throw;
        }
    }

    public static string ParseChecksum(string text, string packageName)
    {
        var line = text.Split('\n').Select(s => s.TrimEnd('\r')).SingleOrDefault(s =>
            Regex.IsMatch(s, @"^[0-9a-fA-F]{64}  \*?" + Regex.Escape(packageName) + "$"));
        if (line is null) throw new InvalidDataException("SHA-256-Prüfsumme für das Windows-Paket fehlt oder ist mehrfach vorhanden.");
        return line[..64].ToUpperInvariant();
    }

    public static void ExtractPackage(string archivePath, string payload, Version expectedVersion)
    {
        if (Directory.Exists(payload)) throw new InvalidDataException("Update-Zielordner ist nicht leer.");
        Directory.CreateDirectory(payload);
        using var archive = ZipFile.OpenRead(archivePath);
        if (archive.Entries.Count is 0 or > 10000) throw new InvalidDataException("Ungültige Zahl von Release-Dateien.");
        var prefix = "WorkshopStudio-win-x64/";
        long expanded = 0;
        foreach (var entry in archive.Entries)
        {
            var name = entry.FullName.Replace('\\', '/');
            if (!name.StartsWith(prefix, StringComparison.Ordinal) || name.Contains("//") ||
                name.Split('/').Any(part => part is "." or "..") ||
                ((entry.ExternalAttributes >> 16) & 0xF000) == 0xA000)
                throw new InvalidDataException("Ungültiger Pfad oder Link im Release-Paket.");
            var relative = name[prefix.Length..];
            if (relative.Length == 0 || name.EndsWith('/')) continue;
            expanded += entry.Length;
            if (expanded > 1024L * 1024 * 1024) throw new InvalidDataException("Entpacktes Release ist zu groß.");
            var target = Path.GetFullPath(Path.Combine(payload, relative));
            if (!target.StartsWith(Path.GetFullPath(payload) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Release-Pfad verlässt den Update-Ordner.");
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            entry.ExtractToFile(target, false);
        }
        var marker = Path.Combine(payload, "release-info.json");
        if (!File.Exists(Path.Combine(payload, "WorkshopStudio.exe")) ||
            !File.Exists(Path.Combine(payload, "WorkshopStudio.dll")) ||
            !File.Exists(Path.Combine(payload, "WorkshopStudio.Core.dll")) || !File.Exists(marker))
            throw new InvalidDataException("Das Release enthält keine vollständige Anwendung.");
        using var metadata = JsonDocument.Parse(File.ReadAllBytes(marker));
        if (metadata.RootElement.GetProperty("version").GetString() != expectedVersion.ToString() ||
            metadata.RootElement.GetProperty("repository").GetString() != Repository)
            throw new InvalidDataException("Die Paketversion passt nicht zum GitHub-Release.");
    }

    private static async Task<byte[]> ReadLimitedAsync(HttpContent content, int maxBytes, CancellationToken ct)
    {
        if (content.Headers.ContentLength is > 0 && content.Headers.ContentLength > maxBytes)
            throw new InvalidDataException("GitHub-Metadaten sind zu groß.");
        await using var stream = await content.ReadAsStreamAsync(ct);
        using var result = new MemoryStream();
        var buffer = new byte[8192]; int read;
        while ((read = await stream.ReadAsync(buffer, ct)) != 0)
        {
            if (result.Length + read > maxBytes) throw new InvalidDataException("GitHub-Metadaten sind zu groß.");
            result.Write(buffer, 0, read);
        }
        return result.ToArray();
    }

    public void Dispose() { if (_ownsClient) _client.Dispose(); }
}
