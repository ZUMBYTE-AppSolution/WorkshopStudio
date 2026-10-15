using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace WorkshopStudio.Core;

public sealed record PortfolioPage(IReadOnlyList<RemoteWorkshopItem> Items, int Total, bool Recognized);
public sealed record PortfolioProgress(int Pages, int Found, string Phase);
public sealed record PortfolioResult(IReadOnlyList<RemoteWorkshopItem> Items, WorkshopScanReport Report);

/// <summary>Reads only the signed-in user's "Items" and "Collections" profile tabs, never favorites or subscriptions.</summary>
public sealed class WorkshopPortfolio
{
    private const int PageSize = 30;
    private static readonly Regex Block = new("<div[^>]*class=\"workshopItem\"[^>]*>(?:(?!<div[^>]*class=\"workshopItem\").)*", RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromSeconds(2));
    private static readonly Regex CollectionBlock = new("<a[^>]*class=\"workshopItemCollection[^\"]*\"[^>]*>.*?</a>", RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromSeconds(2));
    private static readonly Regex Id = new("data-publishedfileid=\"(?<id>[0-9]+)\"", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex App = new("data-appid=\"(?<id>[0-9]+)\"", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex Title = new("<div[^>]*class=\"workshopItemTitle[^\"]*\"[^>]*>(?<value>.*?)</div>", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);
    private static readonly Regex Total = new(@"Showing[^<]{0,80}?\bof\s+(?<count>[0-9,.\s]+)\s+entries", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    public static PortfolioPage ParsePage(string html, ulong owner)
    {
        if (owner == 0 || html.Length > 4_000_000) throw new InvalidDataException("Ungültige Profilseite oder zu große Antwort.");
        var recognized = html.Contains("workshopBrowseItems", StringComparison.OrdinalIgnoreCase) ||
            html.Contains("workshopBrowsePagingInfo", StringComparison.OrdinalIgnoreCase) ||
            html.Contains("id=\"no_items\"", StringComparison.OrdinalIgnoreCase);
        if (!recognized) return new([], 0, false);
        var items = new List<RemoteWorkshopItem>();
        foreach (Match block in Block.Matches(html)) {
            var id = Id.Match(block.Value); var app = App.Match(block.Value);
            if (!id.Success || !ulong.TryParse(id.Groups["id"].Value, out var fileId) || fileId == 0) continue;
            _ = uint.TryParse(app.Groups["id"].Value, out var appId);
            var title = Title.Match(block.Value);
            items.Add(new RemoteWorkshopItem { Id = fileId, OwnerSteamId64 = owner, ConsumerAppId = appId,
                Title = title.Success ? WebUtility.HtmlDecode(Regex.Replace(title.Groups["value"].Value, "<[^>]+>", "")).Trim() : "",
                MetadataSource = "Steam-Profil (angemeldete Websitzung)", FieldStatus = new() { ["title"] = title.Success ? RemoteFieldStatus.Known : RemoteFieldStatus.NotProvided } });
        }
        foreach (Match block in CollectionBlock.Matches(html)) {
            var id = Id.Match(block.Value); var app = App.Match(block.Value);
            if (!id.Success || !ulong.TryParse(id.Groups["id"].Value, out var fileId) || fileId == 0) continue;
            _ = uint.TryParse(app.Groups["id"].Value, out var appId);
            var title = Title.Match(block.Value);
            items.Add(new RemoteWorkshopItem { Id=fileId,OwnerSteamId64=owner,ConsumerAppId=appId,FileType=2,
                Title=title.Success?WebUtility.HtmlDecode(Regex.Replace(title.Groups["value"].Value,"<[^>]+>","")).Trim():"",
                MetadataSource="Steam-Profil (Collections)",FieldStatus=new() { ["title"]=title.Success?RemoteFieldStatus.Known:RemoteFieldStatus.NotProvided } });
        }
        var match = Total.Match(WebUtility.HtmlDecode(html));
        var countText = match.Groups["count"].Value.Replace(",", "").Replace(".", "").Replace(" ", "");
        var total = int.TryParse(countText, out var n) ? n : items.Count;
        if (html.Contains("id=\"no_items\"", StringComparison.OrdinalIgnoreCase)) total = 0;
        return new(items, total, true);
    }

    public static Uri PageUrl(ulong steamId, bool collections, int page) => new(
        $"https://steamcommunity.com/profiles/{steamId}/myworkshopfiles/?p={page}&numperpage={PageSize}&browsesort=myfiles&browsefilter=myfiles" +
        (collections ? "&section=collections" : "") + "&l=english");

    public async Task<PortfolioResult> ScanAsync(ulong steamId, Func<Uri,CancellationToken,Task<string>> readPage,
        Func<IReadOnlyList<RemoteWorkshopItem>,CancellationToken,Task>? enrich, IProgress<PortfolioProgress>? progress, CancellationToken ct)
    {
        if (steamId < 76561197960265728UL) throw new InvalidDataException("Verifizierte SteamID64 fehlt.");
        var report = new WorkshopScanReport { SteamId64 = steamId, StartedAt = DateTimeOffset.UtcNow };
        var found = new Dictionary<ulong,RemoteWorkshopItem>();
        try {
            foreach (var collections in new[] { false, true }) {
                var expected = -1;
                for (var page = 1; page <= 10000; page++) {
                    ct.ThrowIfCancellationRequested();
                    var html = await readPage(PageUrl(steamId, collections, page), ct);
                    var parsed = ParsePage(html, steamId);
                    if (!parsed.Recognized) throw new InvalidDataException("Steam-Profilseite unerwartet oder Anmeldung abgelaufen (Seite " + page + ").");
                    if (expected < 0) expected = parsed.Total;
                    if (parsed.Items.Count == 0 && expected > 0 && (page - 1) * PageSize < expected)
                        throw new InvalidDataException("Steam lieferte eine leere Seite vor Ende der Ergebnisliste.");
                    foreach (var item in parsed.Items) {
                        item.FileType = collections ? 2 : 0;
                        found.TryAdd(item.Id, item);
                    }
                    report.PagesRead++; report.ItemsSeen = found.Count;
                    progress?.Report(new(report.PagesRead, found.Count, collections ? "Collections" : "Dateien / Mods"));
                    if (page * PageSize >= expected || expected == 0) break;
                    if (page == 10000) throw new InvalidDataException("Steam-Ergebnisgrenze erreicht; Scan unvollständig.");
                }
            }
            // A metadata failure must not erase the portfolio already obtained.
            if (enrich is not null) {
                foreach (var batch in found.Values.Chunk(30)) {
                    ct.ThrowIfCancellationRequested();
                    try { await enrich(batch, ct); }
                    catch (Exception ex) when (ex is HttpRequestException or InvalidDataException or TaskCanceledException) {
                        foreach (var item in batch) item.FieldStatus["details"] = RemoteFieldStatus.Error;
                        report.Error = "Einige Detaildaten nicht abrufbar: " + ex.Message;
                    }
                }
            }
            if (found.Values.Any(i=>i.FieldStatus.GetValueOrDefault("owner")!=RemoteFieldStatus.Known) && enrich is not null)
                report.Error = string.IsNullOrEmpty(report.Error) ? "Einige Eigentümer-/Detaildaten nicht bestätigt" : report.Error;
            report.Complete = string.IsNullOrEmpty(report.Error);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { report.Error = "Scan abgebrochen"; }
        catch (Exception ex) when (ex is HttpRequestException or InvalidDataException or IOException or InvalidOperationException or TaskCanceledException or JsonException or System.Runtime.InteropServices.COMException) { report.Error = ex.Message; }
        report.FinishedAt = DateTimeOffset.UtcNow;
        return new(found.Values.ToArray(), report);
    }
}
