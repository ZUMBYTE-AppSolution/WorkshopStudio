using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace WorkshopStudio.Core;

public sealed record ConfigOption(string Section, string Key, string Value, int Line);
public sealed record ConfigDocument(string RelativePath, string Format, string RawText,
    IReadOnlyList<ConfigOption> Options, bool PossibleSecrets, string Status);
public sealed record ConfigDiscoveryResult(IReadOnlyList<ConfigDocument> Files, string State, string ServerLines, IReadOnlyList<string> Warnings);

public static class ConfigDiscovery
{
    private static readonly Regex Secret = new(@"(?im)^\s*[\w.-]*(?:password|passwd|token|secret|api[_-]?key|private[_-]?key)[\w.-]*\s*[:=]", RegexOptions.Compiled);
    private static readonly Regex Pair = new(@"^\s*(?<key>[\w.\-]+)\s*[:=]\s*(?<value>.*?)\s*$", RegexOptions.Compiled);
    private static readonly Regex LuaPair = new("""^\s*(?<key>[\w.\-]+)\s*=\s*(?<value>true|false|nil|[-+]?\d+(?:\.\d+)?|"[^"]*"|'[^']*')\s*[,;]?\s*(?:--.*)?$""", RegexOptions.Compiled);
    public static async Task<ConfigDiscoveryResult> FindAsync(AppProfile app, ModProject mod, int maxKiB, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(mod.ContentFolder) || !Directory.Exists(mod.ContentFolder)) {
            var missingServer=ModSourceResolver.IsZomboid(app) ?
                "WorkshopItems="+(mod.WorkshopId==0?"[Live-Workshop-ID fehlt]":mod.WorkshopId.ToString())+"\nMods="+
                (string.IsNullOrWhiteSpace(mod.ModIds)?"[Mod-ID ohne lokale Quelle nicht ermittelbar]":mod.ModIds) : "";
            return new([], "Lokale Quelle fehlt", missingServer, []);
        }
        var plan = ModSourceResolver.Resolve(app, mod);
        var docs = new List<ConfigDocument>(); var warnings = new List<string>();
        var seen = 0;
        foreach (var mount in plan.Sources) {
            var stack = new Stack<string>(); stack.Push(mount.SourceRoot);
            while (stack.Count > 0) {
                ct.ThrowIfCancellationRequested();
                var dir = stack.Pop();
                if ((File.GetAttributes(dir) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("Verknüpftes Verzeichnis in Mod-Quelle.");
                foreach (var child in Directory.EnumerateDirectories(dir)) stack.Push(child);
                foreach (var file in Directory.EnumerateFiles(dir)) {
                    ct.ThrowIfCancellationRequested();
                    if (++seen > 100000) throw new InvalidDataException("Zu viele Dateien für Config-Erkennung.");
                    var relative = Path.GetRelativePath(mount.SourceRoot, file).Replace('\\', '/');
                    if (!IsConfig(relative)) continue;
                    var named = (mount.TargetPrefix.Length == 0 ? "" : mount.TargetPrefix + "/") + relative;
                    var info = new FileInfo(file);
                    if ((info.Attributes & FileAttributes.ReparsePoint) != 0) { warnings.Add(named + ": Link ausgelassen"); continue; }
                    if (info.Length > maxKiB * 1024L) { warnings.Add(named + ": zu groß"); continue; }
                    var bytes = await File.ReadAllBytesAsync(file, ct);
                    if (bytes.Contains((byte)0)) { warnings.Add(named + ": Binärdaten ausgelassen"); continue; }
                    string raw;
                    try { raw = new UTF8Encoding(false, true).GetString(bytes).TrimStart('\uFEFF'); }
                    catch (DecoderFallbackException) { warnings.Add(named + ": keine gültige UTF-8-Datei"); continue; }
                    var format = Path.GetExtension(file).TrimStart('.').ToUpperInvariant();
                    var (options, status) = Parse(raw, format);
                    docs.Add(new(named, format, raw, options, Secret.IsMatch(raw), status));
                }
            }
        }
        var ids = plan.ModIds.Count > 0 ? plan.ModIds : mod.ModIds.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var workshopId = mod.WorkshopId;
        var server = plan.IsZomboid ?
            "WorkshopItems=" + (workshopId == 0 ? "[Live-Workshop-ID fehlt]" : workshopId.ToString()) + "\nMods=" +
            (ids.Count == 0 ? "[Mod-ID fehlt]" : string.Join(';', ids.Distinct(StringComparer.Ordinal))) : "Für dieses Spiel keine Zomboid-Serverzeilen.";
        return new(docs, docs.Count == 0 ? "Keine Konfiguration gefunden" : warnings.Count == 0 ? "Bereit" : "Teilweise analysiert", server, warnings);
    }
    private static bool IsConfig(string relative)
    {
        var name = Path.GetFileName(relative);
        var extension = Path.GetExtension(name).ToLowerInvariant();
        return extension is ".ini" or ".cfg" or ".yaml" or ".yml" ||
            extension == ".json" && name.Contains("config", StringComparison.OrdinalIgnoreCase) ||
            extension == ".lua" && (name.Contains("config", StringComparison.OrdinalIgnoreCase) || name.Contains("sandbox", StringComparison.OrdinalIgnoreCase)) ||
            name.Equals("sandbox-options.txt", StringComparison.OrdinalIgnoreCase);
    }
    public static (IReadOnlyList<ConfigOption> Options, string Status) Parse(string raw, string format)
    {
        var options = new List<ConfigOption>();
        if (format == "JSON") {
            try {
                using var document = JsonDocument.Parse(raw, new JsonDocumentOptions { MaxDepth = 32, CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
                void Walk(JsonElement value, string prefix) {
                    if (options.Count >= 2000) return;
                    if (value.ValueKind == JsonValueKind.Object) foreach (var prop in value.EnumerateObject()) Walk(prop.Value, prefix.Length == 0 ? prop.Name : prefix + "." + prop.Name);
                    else if (value.ValueKind != JsonValueKind.Array) options.Add(new("", prefix, value.ToString(), 0));
                }
                Walk(document.RootElement, ""); return (options, "Statisch gelesen");
            } catch (JsonException) { return (options, "JSON-Parserfehler; Originaltext verfügbar"); }
        }
        var section = ""; var lineNo = 0;
        foreach (var line in raw.Split('\n')) {
            lineNo++;
            var trimmed = line.Trim();
            if (trimmed.StartsWith('#') || trimmed.StartsWith(';') || trimmed.StartsWith("--")) continue;
            if (format is "INI" or "CFG" && trimmed.StartsWith('[') && trimmed.EndsWith(']')) { section = trimmed[1..^1]; continue; }
            var match = format == "LUA" ? LuaPair.Match(line) : Pair.Match(line);
            if (match.Success && options.Count < 2000) options.Add(new(section, match.Groups["key"].Value, match.Groups["value"].Value.TrimEnd('\r'), lineNo));
        }
        return (options, format == "LUA" ? "Nur statische Lua-Literale; dynamische Werte unbekannt" : "Statisch gelesen");
    }
}
