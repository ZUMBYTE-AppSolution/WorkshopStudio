using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using System.Text;

namespace WorkshopStudio.Core;

public sealed record WorkshopDetails(ulong Id, uint ConsumerAppId, int FileType, string Title, string Description, string PreviewUrl, int Subscriptions, int Views);
public sealed record UpdateInfo(string Version, string DownloadUrl, string Notes, string Sha256, string Channel);

public sealed class SteamService : IDisposable
{
    private readonly HttpClient _http;
    private readonly WorkspaceStore _store;
    public SteamService(WorkspaceStore store)
    {
        _store = store;
        _http = new HttpClient(new HttpClientHandler { AutomaticDecompression = DecompressionMethods.All }) { Timeout = TimeSpan.FromMinutes(5) };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("WorkshopStudio/1.1 (+desktop-publisher)");
    }
    public static bool IsSteamHttps(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var u) || u.Scheme != "https" || !u.IsDefaultPort) return false;
        return new[] { "steamcommunity.com", "steampowered.com", "steamstatic.com", "steamusercontent.com" }
            .Any(h => u.Host.Equals(h, StringComparison.OrdinalIgnoreCase) || u.Host.EndsWith("." + h, StringComparison.OrdinalIgnoreCase));
    }
    public static bool IsSteamBrowserUrl(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != "https" ||
            !uri.IsDefaultPort || !string.IsNullOrEmpty(uri.UserInfo)) return false;
        return new[] { "steamcommunity.com", "www.steamcommunity.com", "store.steampowered.com",
            "login.steampowered.com", "help.steampowered.com", "checkout.steampowered.com" }
            .Contains(uri.Host, StringComparer.OrdinalIgnoreCase);
    }

    public async Task RefreshAppAsync(AppProfile app, StudioSettings settings, CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromSeconds(settings.HttpTimeoutSeconds));
        // Store appdetails is a public, undocumented convenience endpoint. Failures do not block publishing.
        var url = $"https://store.steampowered.com/api/appdetails?appids={app.AppId}&l={Uri.EscapeDataString(settings.StoreLanguage)}&cc={Uri.EscapeDataString(settings.StoreCountry)}";
        using var response = await _http.GetAsync(url, timeout.Token); response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(timeout.Token));
        if (!json.RootElement.TryGetProperty(app.AppId.ToString(), out var item) || !item.TryGetProperty("success", out var success) || !success.GetBoolean())
            throw new InvalidDataException("Für diese AppID sind keine öffentlichen Store-Daten verfügbar. Name und AppID können manuell gepflegt werden.");
        var data = item.GetProperty("data"); app.Name = data.GetProperty("name").GetString() ?? app.Name;
        if (data.TryGetProperty("header_image", out var header)) app.HeaderImageUrl = header.GetString() ?? "";
        if (IsSteamHttps(app.HeaderImageUrl)) app.HeaderCachePath = await CacheImageAsync(app.HeaderImageUrl, $"{app.AppId}-header", settings.ImageCacheDays, timeout.Token);
        try {
            using var iconResponse = await _http.GetAsync($"https://steamcommunity.com/games/{app.AppId}/?xml=1", timeout.Token);
            iconResponse.EnsureSuccessStatusCode();
            var xml = await iconResponse.Content.ReadAsStringAsync(timeout.Token);
            using var reader = XmlReader.Create(new StringReader(xml), new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 2_000_000 });
            var document = XDocument.Load(reader);
            var icon = document.Descendants("avatarFull").FirstOrDefault()?.Value ?? "";
            if (IsSteamHttps(icon)) { app.IconImageUrl = icon; app.IconCachePath = await CacheImageAsync(icon, $"{app.AppId}-icon", settings.ImageCacheDays, timeout.Token); }
        } catch (Exception e) when (e is HttpRequestException or XmlException or InvalidDataException or TaskCanceledException) { /* Artwork is the explicit fallback. */ }
        app.MetadataUpdatedAt = DateTimeOffset.UtcNow;
    }
    private async Task<string> CacheImageAsync(string url, string key, int days, CancellationToken ct)
    {
        if (!IsSteamHttps(url)) throw new InvalidDataException("Vorschaubild ist nicht auf einer Steam-Domain.");
        var path = Path.Combine(_store.Root, "cache", key + ".img");
        if (File.Exists(path) && DateTime.UtcNow-File.GetLastWriteTimeUtc(path) < TimeSpan.FromDays(days)) return path;
        using var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct); response.EnsureSuccessStatusCode();
        if (!IsSteamHttps(response.RequestMessage?.RequestUri?.AbsoluteUri ?? "")) throw new InvalidDataException("Bild-Weiterleitung ist nicht auf einer Steam-Domain.");
        if (response.Content.Headers.ContentLength > 10*1024*1024) throw new InvalidDataException("Steam-Bild ist zu groß.");
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var bytes = new MemoryStream(); var buffer = new byte[81920]; int n;
        while ((n=await stream.ReadAsync(buffer,ct))>0) { if (bytes.Length+n > 10*1024*1024) throw new InvalidDataException("Steam-Bild ist zu groß."); await bytes.WriteAsync(buffer.AsMemory(0,n),ct); }
        var content = bytes.ToArray();
        if (content.Length<8 || !((content[0]==0x89 && content[1]==0x50) || (content[0]==0xFF && content[1]==0xD8) || (content[0]==0x52 && content[1]==0x49)))
            throw new InvalidDataException("Unbekanntes Bildformat.");
        await File.WriteAllBytesAsync(path+".tmp",content,ct); File.Move(path+".tmp",path,true); return path;
    }
    public async Task<string> CacheWorkshopPreviewAsync(RemoteWorkshopItem item, int days, CancellationToken ct)
    {
        if (item.Id == 0 || !IsSteamHttps(item.PreviewUrl)) return "";
        var signature=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(item.PreviewUrl)))[..16];
        return await CacheImageAsync(item.PreviewUrl, "workshop-" + item.Id + "-" + signature, days, ct);
    }
    public async Task<WorkshopDetails> GetDetailsAsync(ulong id, CancellationToken ct)
    {
        if (id==0) throw new ArgumentException("Workshop-ID fehlt.");
        using var form = new FormUrlEncodedContent(new Dictionary<string,string> { ["itemcount"]="1",["publishedfileids[0]"]=id.ToString() });
        using var response = await _http.PostAsync("https://api.steampowered.com/ISteamRemoteStorage/GetPublishedFileDetails/v1/",form,ct); response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        var d = json.RootElement.GetProperty("response").GetProperty("publishedfiledetails")[0];
        if (d.GetProperty("result").GetInt32()!=1) throw new InvalidDataException("Workshop-Eintrag nicht öffentlich abrufbar. Private Einträge sind über diese API eventuell nicht sichtbar.");
        string S(string key) => d.TryGetProperty(key,out var v) ? v.ToString() : "";
        int I(string key) => d.TryGetProperty(key,out var v) && v.TryGetInt32(out var n) ? n : 0;
        return new(id,(uint)I("consumer_app_id"),I("file_type"),S("title"),S("description"),S("preview_url"),I("subscriptions"),I("views"));
    }
    public async Task EnrichWorkshopItemsAsync(IReadOnlyList<RemoteWorkshopItem> items, CancellationToken ct)
    {
        if (items.Count == 0) return;
        if (items.Count > 30) throw new ArgumentOutOfRangeException(nameof(items));
        var fields = new Dictionary<string,string> { ["itemcount"] = items.Count.ToString() };
        for (var i = 0; i < items.Count; i++) fields[$"publishedfileids[{i}]"] = items[i].Id.ToString();
        using var form = new FormUrlEncodedContent(fields);
        using var response = await _http.PostAsync("https://api.steampowered.com/ISteamRemoteStorage/GetPublishedFileDetails/v1/", form, ct);
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        var byId = items.ToDictionary(i => i.Id);
        foreach (var detail in json.RootElement.GetProperty("response").GetProperty("publishedfiledetails").EnumerateArray()) {
            if (!ulong.TryParse(detail.GetProperty("publishedfileid").ToString(), out var id) || !byId.TryGetValue(id, out var item)) continue;
            if (!detail.TryGetProperty("result", out var result) || result.GetInt32() != 1) {
                item.FieldStatus["details"] = RemoteFieldStatus.AccessDenied; continue;
            }
            static string Str(JsonElement d, string key) => d.TryGetProperty(key, out var v) ? v.ToString() : "";
            static int? Int(JsonElement d, string key) => int.TryParse(Str(d, key), out var n) ? n : null;
            static long? Long(JsonElement d, string key) => long.TryParse(Str(d, key), out var n) ? n : null;
            static uint Uint(JsonElement d, string key) => uint.TryParse(Str(d, key), out var n) ? n : 0;
            static DateTimeOffset? Time(JsonElement d, string key) => Long(d, key) is { } n && n > 0 ? DateTimeOffset.FromUnixTimeSeconds(n) : null;
            if (!ulong.TryParse(Str(detail, "creator"), out var creator) || creator != item.OwnerSteamId64) {
                item.FieldStatus["owner"] = RemoteFieldStatus.Error;
                continue;
            }
            item.FieldStatus["owner"] = RemoteFieldStatus.Known;
            item.CreatorAppId = Uint(detail, "creator_app_id");
            var consumer = Uint(detail, "consumer_app_id"); if (consumer > 0) item.ConsumerAppId = consumer;
            item.FileType = Int(detail, "file_type") ?? item.FileType;
            item.Title = Str(detail, "title"); item.Description = Str(detail, "description");
            item.PreviewUrl = Str(detail, "preview_url"); item.Visibility = Int(detail, "visibility");
            item.CreatedAt = Time(detail, "time_created"); item.UpdatedAt = Time(detail, "time_updated");
            item.FileSize = Long(detail, "file_size"); item.Subscriptions = Int(detail, "subscriptions"); item.Views = Int(detail, "views");
            item.Tags = detail.TryGetProperty("tags", out var tags) && tags.ValueKind == JsonValueKind.Array
                ? tags.EnumerateArray().Select(t => Str(t, "tag")).Where(s => s.Length > 0).ToList() : [];
            // Author version is never inferred as a default. Description hints remain explicitly heuristic.
            var version = Regex.Match(item.Description, @"(?im)^\s*(?:latest\s+)?version\s*[:=]\s*(?<version>[^\r\n\[]+)", RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(200));
            if (version.Success) { item.AuthorVersion = version.Groups["version"].Value.Trim(); item.VersionSource = "Hinweis in Beschreibung"; }
            item.MetadataSource = "Steam-Profil + öffentliche Detail-API"; item.FetchedAt = DateTimeOffset.UtcNow;
            foreach (var field in new[] { "title", "description", "preview", "visibility", "tags", "created", "updated", "size", "statistics" })
                item.FieldStatus[field] = RemoteFieldStatus.Known;
            item.FieldStatus["changelog"] = RemoteFieldStatus.NotRequested;
        }
    }
    public static (string Text, int Count,IReadOnlyList<string> Notes) ParseChangeNotes(string html)
    {
        if (html.Length > 4_000_000) throw new InvalidDataException("Steam-Änderungsverlauf ist zu groß.");
        var matches = Regex.Matches(html,
            "<div\\s+class=\"changelog headline\"[^>]*>(?<date>.*?)</div>.*?<p\\s+id=\"[0-9]+\"[^>]*>(?<note>.*?)</p>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline, TimeSpan.FromSeconds(2));
        static string Plain(string value) => WebUtility.HtmlDecode(Regex.Replace(
            Regex.Replace(value, @"<br\s*/?>", "\n", RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(200)),
            "<[^>]+>", "", RegexOptions.Singleline, TimeSpan.FromMilliseconds(200))).Trim();
        var lines = matches.Cast<Match>().Take(50).Select(m => Plain(m.Groups["date"].Value) + "\n" + Plain(m.Groups["note"].Value));
        return (string.Join("\n\n", lines), matches.Count,matches.Cast<Match>().Take(50).Select(m=>Plain(m.Groups["note"].Value)).ToArray());
    }
    public async Task<(string Text, string Status,IReadOnlyList<string> Notes)> GetChangeNotesAsync(ulong id, CancellationToken ct)
    {
        if (id == 0) throw new ArgumentOutOfRangeException(nameof(id));
        using var response = await _http.GetAsync($"https://steamcommunity.com/sharedfiles/filedetails/changelog/{id}?l=english", ct);
        response.EnsureSuccessStatusCode();
        if (!IsSteamHttps(response.RequestMessage?.RequestUri?.AbsoluteUri ?? "") || response.Content.Headers.ContentLength > 4_000_000)
            throw new InvalidDataException("Änderungsverlauf wurde nicht von Steam geliefert oder ist zu groß.");
        var html = await response.Content.ReadAsStringAsync(ct);
        var parsed = ParseChangeNotes(html);
        return (parsed.Text, parsed.Count == 0 ? "Keine öffentlich sichtbaren Änderungsnotizen gefunden" :
            $"{parsed.Count} öffentlich sichtbare Notizen (erste Seite; vollständiger Verlauf auf Steam)",parsed.Notes);
    }
    public async Task<List<ulong>> GetCollectionAsync(ulong id, CancellationToken ct)
    {
        using var form = new FormUrlEncodedContent(new Dictionary<string,string> { ["collectioncount"]="1",["publishedfileids[0]"]=id.ToString() });
        using var response = await _http.PostAsync("https://api.steampowered.com/ISteamRemoteStorage/GetCollectionDetails/v1/",form,ct); response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        var d = json.RootElement.GetProperty("response").GetProperty("collectiondetails")[0];
        if (d.GetProperty("result").GetInt32()!=1) throw new InvalidDataException("Collection kann nicht abgerufen werden. ID/Sichtbarkeit prüfen.");
        return d.TryGetProperty("children",out var children) ? children.EnumerateArray().Select(x=>ulong.Parse(x.GetProperty("publishedfileid").ToString())).ToList() : [];
    }
    public async Task DownloadSteamCmdAsync(string directory, IProgress<string>? progress, CancellationToken ct)
    {
        Directory.CreateDirectory(directory);
        var zip = Path.Combine(directory,"steamcmd-bootstrap.zip");
        progress?.Report("SteamCMD wird direkt von Valve geladen.");
        using var response = await _http.GetAsync("https://steamcdn-a.akamaihd.net/client/installer/steamcmd.zip",HttpCompletionOption.ResponseHeadersRead,ct); response.EnsureSuccessStatusCode();
        await using (var dest=File.Create(zip)) await response.Content.CopyToAsync(dest,ct);
        using (var archive=System.IO.Compression.ZipFile.OpenRead(zip)) {
            foreach (var entry in archive.Entries) {
                var dest = Path.GetFullPath(Path.Combine(directory,entry.FullName));
                if (!PackageBuilder.IsWithin(dest,directory)) throw new InvalidDataException("Unsicherer Pfad im SteamCMD-Archiv.");
                if (entry.FullName.EndsWith('/')) { Directory.CreateDirectory(dest); continue; }
                // Do not replace an existing installation or its cached credentials.
                if (File.Exists(dest)) continue;
                Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                await using var source = entry.Open(); await using var target = File.Create(dest); await source.CopyToAsync(target,ct);
            }
        }
        File.Delete(zip);
        if (!File.Exists(Path.Combine(directory,"steamcmd.exe"))) throw new InvalidDataException("Archiv enthält keine steamcmd.exe.");
        progress?.Report("Bootstrap installiert. SteamCMD aktualisiert sich bei seinem ersten Start selbst.");
    }
    public async Task<UpdateInfo?> CheckUpdateAsync(string manifestUrl,string channel,CancellationToken ct)
    {
        if (!Uri.TryCreate(manifestUrl,UriKind.Absolute,out var u) || u.Scheme!="https") throw new InvalidDataException("HTTPS-Manifest-Adresse fehlt.");
        using var response=await _http.GetAsync(u,ct); response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength>1024*1024) throw new InvalidDataException("Update-Manifest ist zu groß.");
        var update = await response.Content.ReadFromJsonAsync<UpdateInfo>(WorkspaceStore.JsonOptions,ct);
        if (update is null || !Version.TryParse(update.Version,out _) || !Uri.TryCreate(update.DownloadUrl,UriKind.Absolute,out var download) || download.Scheme!="https") throw new InvalidDataException("Ungültiges Update-Manifest.");
        return update.Channel.Equals(channel,StringComparison.OrdinalIgnoreCase) ? update : null;
    }
    public void Dispose() => _http.Dispose();
}
