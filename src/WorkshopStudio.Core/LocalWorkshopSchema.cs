using System.Text;
using System.Text.RegularExpressions;

namespace WorkshopStudio.Core;

public sealed record LocalWorkshopSchema(string Title, string Description, string Version, string PreviewPath,
    string Changelog, IReadOnlyList<string> ModIds, IReadOnlyList<string> Evidence, IReadOnlyList<string> Warnings)
{
    public string Summary => string.Join(Environment.NewLine, Evidence.Concat(Warnings.Select(w => "Hinweis: " + w)));
}

/// <summary>Reads metadata only from an explicitly resolved mod source and its immediate Workshop wrapper.</summary>
public static class LocalWorkshopSchemaReader
{
    private static string ReadSmall(string path, int maxBytes)
    {
        var info = new FileInfo(path);
        if (!info.Exists || info.Length > maxBytes || (info.Attributes & FileAttributes.ReparsePoint) != 0) return "";
        try { return new UTF8Encoding(false, true).GetString(File.ReadAllBytes(path)).TrimStart('\uFEFF'); }
        catch (DecoderFallbackException) { return ""; }
    }

    private static string ReadValue(string text, string key)
    {
        var hit = Regex.Match(text, @"(?im)^[ \t]*" + Regex.Escape(key) + @"[ \t]*=[ \t]*(?<value>[^\r\n]+)",
            RegexOptions.None, TimeSpan.FromMilliseconds(200));
        return hit.Success ? hit.Groups["value"].Value.Trim() : "";
    }

    public static IReadOnlyList<string> MetadataRoots(PackagePlan plan)
    {
        // The wrapper is used for metadata only. Packaging still uses plan.Sources exclusively.
        var roots = plan.Sources.Select(s => s.SourceRoot).ToList();
        roots.Add(plan.RequestedPath);
        if (plan.IsZomboid && plan.Sources.Select(s=>Directory.GetParent(s.SourceRoot)?.FullName).Distinct(StringComparer.OrdinalIgnoreCase).Count()==1) {
            var parent = Directory.GetParent(plan.Sources[0].SourceRoot)?.FullName;
            if (parent is not null && (Path.GetFileName(parent).Equals("mods", StringComparison.OrdinalIgnoreCase) ||
                Path.GetFileName(parent).Equals("Contents", StringComparison.OrdinalIgnoreCase))) {
                var wrapper = Directory.GetParent(parent)?.FullName;
                if (wrapper is not null && PackageBuilder.IsWithin(plan.Sources[0].SourceRoot, wrapper) &&
                    Directory.Exists(wrapper) && (File.GetAttributes(wrapper) & FileAttributes.ReparsePoint) == 0)
                {
                    try {ModSourceResolver.EnsureNotGameDirectory(wrapper);roots.Add(wrapper);}catch(InvalidDataException){}
                    if(new[]{"Contents","Content"}.Contains(Path.GetFileName(wrapper),StringComparer.OrdinalIgnoreCase)&&Directory.GetParent(wrapper) is { } project) {
                        try {ModSourceResolver.EnsureNotGameDirectory(project.FullName);if((project.Attributes&FileAttributes.ReparsePoint)==0)roots.Add(project.FullName);}catch(InvalidDataException){}
                    }
                }
            }
        }
        return roots.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }
    public static LocalWorkshopSchema Inspect(AppProfile app, ModProject mod)
    {
        var plan=ModSourceResolver.Resolve(app,mod);
        var evidence=new List<string>();var warnings=new List<string>();
        string title="",description="",version="",preview="",changelog="";
        var roots=MetadataRoots(plan);
        foreach (var root in roots.Distinct(StringComparer.OrdinalIgnoreCase)) {
            var infoPath = Path.Combine(root, "workshop.txt");
            var raw = ReadSmall(infoPath, 256 * 1024);
            if (raw.Length > 0) {
                title = title.Length == 0 ? ReadValue(raw, "title") : title;
                if(description.Length==0)description=string.Join("\n",Regex.Matches(raw,@"(?im)^[ \t]*description[ \t]*=[ \t]*(?<value>[^\r\n]*)",RegexOptions.None,TimeSpan.FromMilliseconds(200))
                    .Select(m=>m.Groups["value"].Value)).Trim();
                evidence.Add(infoPath + " (Titel/Beschreibung, falls vorhanden)");
            }
            foreach (var name in new[] { "version.txt", "VERSION", "VERSION.txt" }) {
                var path = Path.Combine(root, name);
                var value = ReadSmall(path, 4096).Trim();
                if (version.Length == 0 && value.Length is > 0 and < 128 && !value.Contains('\n')) {
                    version = value; evidence.Add(path + " (Version)");
                }
            }
            foreach (var name in new[] { "poster.png", "preview.png", "preview.jpg", "preview.jpeg" }) {
                var path = Path.Combine(root, name);
                if (preview.Length == 0 && File.Exists(path) && new FileInfo(path) is { Length: > 15 and <= 1048576, Attributes: var attributes } &&
                    (attributes & FileAttributes.ReparsePoint) == 0) {
                    preview = path; evidence.Add(path + " (Bildvorschlag)");
                }
            }
            foreach (var name in new[] { "CHANGELOG.md", "CHANGELOG.txt", "changelog.md", "changelog.txt" }) {
                var path = Path.Combine(root, name);
                var value = ReadSmall(path, 256 * 1024);
                if (changelog.Length == 0 && value.Length > 0) {
                    changelog = value; evidence.Add(path + " (Änderungsverlauf)");
                }
            }
        }
        foreach (var mount in plan.Sources) foreach (var relative in mount.RequiredInfoFiles) {
            var path = Path.Combine(mount.SourceRoot, relative);
            var raw = ReadSmall(path, 256 * 1024);
            if (raw.Length == 0) { warnings.Add("mod.info nicht lesbar: " + path); continue; }
            if (version.Length == 0) {
                version = ReadValue(raw, "version");
                if (version.Length > 0) evidence.Add(path + " (Version)");
            }
        }
        if (version.Length == 0) warnings.Add("Keine ausdrücklich angegebene Version gefunden.");
        if (preview.Length == 0) warnings.Add("Kein lokales Vorschaubild gefunden; das Steam-Bild bleibt remote sichtbar.");
        return new(title, description, version, preview, changelog, plan.ModIds, evidence, warnings);
    }
}
