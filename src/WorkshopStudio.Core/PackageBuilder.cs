using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace WorkshopStudio.Core;

public sealed record PackageFile(string RelativePath, long Bytes, string Sha256)
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string SizeLabel => Format.Bytes(Bytes);
}
public sealed record PackageSnapshot(string Root, string ContentPath, string VdfPath, string Fingerprint, long Bytes, IReadOnlyList<PackageFile> Files, PackagePlan Plan, IReadOnlyList<string> Directories);

public sealed class PackageBuilder
{
    private readonly WorkspaceStore _store;
    public PackageBuilder(WorkspaceStore store) => _store = store;

    public static bool IsExcluded(string relative, string patterns, bool secrets)
    {
        var path = relative.Replace('\\', '/');
        if (secrets && (path.Split('/').Any(p => p.Equals(".ssh", StringComparison.OrdinalIgnoreCase) || p.Equals(".aws", StringComparison.OrdinalIgnoreCase)) ||
            Regex.IsMatch(path, @"(^|/)(\.env($|\.)|secrets?\.json$|id_rsa$|id_ed25519$)|\.(pfx|p12|key|pem)$", RegexOptions.IgnoreCase))) return true;
        foreach (var raw in patterns.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)) {
            if (raw.StartsWith('#')) continue;
            var p = raw.Replace('\\', '/').TrimStart('/');
            if (p.EndsWith('/')) {
                var dir = p.TrimEnd('/');
                if (path.StartsWith(dir + "/", StringComparison.OrdinalIgnoreCase) || path.Contains("/" + dir + "/", StringComparison.OrdinalIgnoreCase)) return true;
            } else {
                var regex = "^" + Regex.Escape(p).Replace(@"\*\*", ".*").Replace(@"\*", "[^/]*").Replace(@"\?", "[^/]") + "$";
                if (Regex.IsMatch(p.Contains('/') ? path : Path.GetFileName(path), regex, RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1))) return true;
            }
        }
        return false;
    }
    public static bool IsWithin(string child, string parent)
    {
        var c = Path.GetFullPath(child).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var p = Path.GetFullPath(parent).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return c.Equals(p, StringComparison.OrdinalIgnoreCase) || c.StartsWith(p + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }
    private static IEnumerable<(string Path, bool IsDirectory)> SafeEntries(string root, string prefix,
        string patterns, bool secrets, CancellationToken ct)
    {
        var stack = new Stack<string>(); stack.Push(root);
        while (stack.TryPop(out var directory))
        {
            ct.ThrowIfCancellationRequested();
            if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("Verknüpfte Ordner/Junctions sind in Upload-Paketen nicht erlaubt: " + directory);
            ModSourceResolver.EnsureNotGameDirectory(directory);
            yield return (directory, true);
            foreach (var file in Directory.EnumerateFiles(directory).OrderBy(f => f, StringComparer.Ordinal))
            {
                ct.ThrowIfCancellationRequested();
                var relative = Path.GetRelativePath(root, file).Replace('\\', '/');
                if (IsExcluded(relative, patterns, secrets) || IsExcluded(Join(prefix, relative), patterns, secrets)) continue;
                if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidDataException("Symbolische Links sind in Upload-Paketen nicht erlaubt: " + file);
                yield return (file, false);
            }
            foreach (var sub in Directory.EnumerateDirectories(directory).OrderByDescending(f => f, StringComparer.Ordinal))
            {
                var relative = Path.GetRelativePath(root, sub).Replace('\\', '/') + "/";
                if (!IsExcluded(relative, patterns, secrets) && !IsExcluded(Join(prefix, relative), patterns, secrets)) stack.Push(sub);
            }
        }
    }
    private static string Join(string prefix, string relative) =>
        string.IsNullOrEmpty(prefix) ? relative : relative == "." ? prefix : prefix + "/" + relative;
    public void Validate(AppProfile app, ModProject mod, StudioSettings settings)
    {
        if (string.IsNullOrWhiteSpace(mod.Name) || Encoding.UTF8.GetByteCount(mod.Name + mod.TestTitleSuffix) > 128) throw new InvalidDataException("Titel fehlt oder ist länger als 128 UTF-8-Bytes.");
        if (mod.Visibility is < 0 or > 3) throw new InvalidDataException("Sichtbarkeit ungültig.");
        if (Encoding.UTF8.GetByteCount(mod.Description) > 8000) throw new InvalidDataException("Beschreibung darf höchstens 8000 UTF-8-Bytes enthalten.");
        if (!Directory.Exists(mod.ContentFolder)) throw new DirectoryNotFoundException("Inhaltsordner fehlt: " + mod.ContentFolder);
        var source = Path.GetFullPath(mod.ContentFolder);
        if (IsWithin(_store.Root, source) || IsWithin(source, _store.Root)) throw new InvalidDataException("Mod-Quellen und Studio-Daten müssen getrennt liegen.");
        if (source.TrimEnd(Path.DirectorySeparatorChar) == Path.GetPathRoot(source)?.TrimEnd(Path.DirectorySeparatorChar)) throw new InvalidDataException("Kein gesamtes Laufwerk als Inhaltsordner verwenden.");
        if (!string.IsNullOrEmpty(mod.PreviewFile)) {
            if (!File.Exists(mod.PreviewFile)) throw new FileNotFoundException("Vorschaubild fehlt.", mod.PreviewFile);
            if (!new[] { ".png", ".jpg", ".jpeg" }.Contains(Path.GetExtension(mod.PreviewFile).ToLowerInvariant())) throw new InvalidDataException("Vorschaubild muss PNG oder JPEG sein.");
            if (new FileInfo(mod.PreviewFile).Length > 1048576) throw new InvalidDataException("Vorschaubild muss höchstens 1 MiB groß sein.");
        }
        // Scope detection is mandatory, even with optional layout validation disabled.
        var plan = ModSourceResolver.Resolve(app, mod);
        if(settings.ValidateZomboidLayout && plan.IsZomboid)
            foreach(var mount in plan.Sources)
                foreach(var info in mount.RequiredInfoFiles)
                    if(!File.ReadLines(Path.Combine(mount.SourceRoot,info)).Any(line=>Regex.IsMatch(line,@"^\s*id\s*=\s*\S+")))
                        throw new InvalidDataException("In dieser mod.info fehlt eine id=: "+Path.Combine(mount.SourceRoot,info));
        foreach (var mount in plan.Sources)
            if (IsWithin(_store.Root, mount.SourceRoot) || IsWithin(mount.SourceRoot, _store.Root))
                throw new InvalidDataException("Mod-Quellen und Studio-Daten müssen getrennt liegen.");
    }

    public async Task<PackageSnapshot> BuildAsync(AppProfile app, ModProject mod, ReleaseChannel channel, StudioSettings settings, Guid jobId, IProgress<string>? progress, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        Validate(app, mod, settings);
        var root = Path.Combine(_store.StagingRoot, jobId.ToString("N"));
        if (Directory.Exists(root)) throw new IOException("Paketverzeichnis existiert bereits.");
        Directory.CreateDirectory(root);
        await File.WriteAllTextAsync(Path.Combine(root, ".workshopstudio-owned"), "1", ct);
        var contents = Path.Combine(root, "content"); Directory.CreateDirectory(contents);
        var plan = ModSourceResolver.Resolve(app, mod);
        var patterns = string.Join('\n', settings.GlobalExclusions, app.DefaultExclusions, mod.Exclusions);
        var files = new List<PackageFile>(); long total = 0;
        var directories = new HashSet<string>(StringComparer.Ordinal);
        var targetNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        progress?.Report("Upload-Auswahl: " + (plan.IsZomboid ? $"{plan.Sources.Count} Mod(s)" : "generischer Inhaltsordner"));
        foreach (var mount in plan.Sources)
        {
            progress?.Report(mount.SourceRoot + " → " + mount.TargetPrefix + "/");
            foreach (var entry in SafeEntries(mount.SourceRoot, mount.TargetPrefix, patterns, settings.ExcludePotentialSecrets, ct))
            {
                ct.ThrowIfCancellationRequested();
                var relative = Path.GetRelativePath(mount.SourceRoot, entry.Path).Replace('\\', '/');
                var uploadPath = Join(mount.TargetPrefix, relative);
                var destination = Path.GetFullPath(Path.Combine(contents, uploadPath));
                if (!IsWithin(destination, contents)) throw new InvalidDataException("Datei liegt außerhalb des Pakets.");
                if (entry.IsDirectory)
                {
                    if (directories.Count >= 100000) throw new InvalidDataException("Zu viele Ordner im Paket.");
                    Directory.CreateDirectory(destination);
                    if (uploadPath != ".") directories.Add(uploadPath);
                    continue;
                }
                if (!targetNames.Add(uploadPath)) throw new InvalidDataException("Doppelter Zielpfad im Paket: " + uploadPath);
                if (files.Count >= settings.MaxFileCount) throw new InvalidDataException("Maximale Dateianzahl überschritten.");
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                long length = 0;
                await using (var source = new FileStream(entry.Path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true))
                await using (var target = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true))
                {
                    var buffer = new byte[81920]; int read;
                    while ((read = await source.ReadAsync(buffer.AsMemory(), ct)) > 0)
                    {
                        if (total + length + read > settings.MaxPackageMiB * 1048576L)
                            throw new InvalidDataException("Maximale Paketgröße überschritten.");
                        await target.WriteAsync(buffer.AsMemory(0, read), ct); length += read;
                    }
                }
                await using var check = File.OpenRead(destination);
                var hash = Convert.ToHexString(await SHA256.HashDataAsync(check, ct));
                total += length; files.Add(new(uploadPath, length, hash));
                if (files.Count % 100 == 0) progress?.Report($"{files.Count:N0} Dateien · {Format.Bytes(total)}");
            }
            foreach (var required in mount.RequiredDirectories)
            {
                var relative = Join(mount.TargetPrefix, required);
                Directory.CreateDirectory(Path.Combine(contents, relative)); directories.Add(relative);
            }
            // Check the actual snapshot, not just the source. Exclusions must not remove all metadata.
            if (plan.IsZomboid)
            {
                foreach (var info in mount.RequiredInfoFiles)
                    if (!files.Any(f => f.RelativePath == Join(mount.TargetPrefix, info)))
                        throw new InvalidDataException("Erforderliche mod.info fehlt im fertigen Paket (Dateifilter prüfen): " + Join(mount.TargetPrefix, info));
            }
        }
        files.Sort((a, b) => StringComparer.Ordinal.Compare(a.RelativePath, b.RelativePath));
        if (files.Count == 0) throw new InvalidDataException("Das Paket enthält nach den Ausschlüssen keine Dateien.");
        var sortedDirectories = directories.OrderBy(d => d, StringComparer.Ordinal).ToArray();
        string preview = "", previewHash = "";
        if (!string.IsNullOrWhiteSpace(mod.PreviewFile)) {
            preview = Path.Combine(root, "preview" + Path.GetExtension(mod.PreviewFile)); File.Copy(mod.PreviewFile, preview);
            await using var f = File.OpenRead(preview); previewHash = Convert.ToHexString(await SHA256.HashDataAsync(f, ct));
        }
        var identity = JsonSerializer.Serialize(new { app.AppId, mod.Name, mod.Version, mod.Description, mod.Visibility, mod.ChangeNote, mod.TestTitleSuffix, channel, previewHash, files, directories = sortedDirectories });
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity)));
        var vdf = Path.Combine(root, "workshop.vdf");
        await File.WriteAllTextAsync(vdf, VdfWriter.Build(app, mod, channel, contents, preview), new UTF8Encoding(false), ct);
        await File.WriteAllTextAsync(Path.Combine(root, "manifest.json"), JsonSerializer.Serialize(new { fingerprint, files, directories = sortedDirectories, total, app.AppId, channel, sources = plan.Sources, warnings = plan.Warnings, created = DateTimeOffset.UtcNow }, WorkspaceStore.JsonOptions), ct);
        return new(root, contents, vdf, fingerprint, total, files, plan, sortedDirectories);
    }
    public void DeleteOwnedStage(string path)
    {
        if (!IsWithin(path, _store.StagingRoot) || Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar).Equals(_store.StagingRoot.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase) || !File.Exists(Path.Combine(path, ".workshopstudio-owned")))
            throw new InvalidOperationException("Sicherheitsprüfung: kein Studio-Paketordner.");
        Directory.Delete(path, true);
    }
    public void Backup(PackageSnapshot snapshot, ModProject mod, ReleaseChannel channel, int retention)
    {
        var dir = Path.Combine(_store.BackupRoot, mod.Id.ToString("N"), channel.ToString()); Directory.CreateDirectory(dir);
        var zip = Path.Combine(dir, DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + ".zip");
        ZipFile.CreateFromDirectory(snapshot.Root, zip, CompressionLevel.Fastest, false);
        foreach (var old in new DirectoryInfo(dir).GetFiles("*.zip").OrderByDescending(f => f.Name).Skip(retention)) old.Delete();
    }
}
