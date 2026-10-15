using System.Text.RegularExpressions;

namespace WorkshopStudio.Core;

/// <summary>Resolves user-selected sources into explicit upload mounts. Never walks a game tree.</summary>
public sealed record ModSourceCandidate(string Root, string FolderName, string DisplayName,
    IReadOnlyList<string> ModIds, IReadOnlyList<string> InfoFiles, bool Versioned)
{
    public string IdLabel => string.Join("; ", ModIds);
    public string LayoutLabel => Versioned ? "Versionierte Mod (B42 / gemeinsame Assets)" : "Einzelmod (mod.info im Stamm)";
}
public sealed record ModSourceDiscovery(string RequestedPath, string ContainerPath,
    bool DirectMod, IReadOnlyList<ModSourceCandidate> Candidates);
public sealed record PackageSource(string SourceRoot, string TargetPrefix,
    IReadOnlyList<string> RequiredInfoFiles, IReadOnlyList<string> RequiredDirectories);
public sealed record PackagePlan(bool IsZomboid, string RequestedPath, IReadOnlyList<PackageSource> Sources,
    IReadOnlyList<string> ModIds, IReadOnlyList<string> Warnings)
{
    public string Summary => string.Join(Environment.NewLine, Sources.Select(s =>
        $"{s.SourceRoot}{Environment.NewLine}  → Upload/{(s.TargetPrefix.Length == 0 ? "" : s.TargetPrefix + "/")}"));
}

public static class ModSourceResolver
{
    public static bool IsZomboid(AppProfile app) => app.AppId == 108600 || app.Template != GameTemplate.Generic;
    internal static bool IsVersion(string name) => Regex.IsMatch(name, @"^\d{2}(?:\.\d+){0,2}$");

    // This guard is also used for generic apps and during traversal. Validation settings cannot disable it.
    public static void EnsureSafeDirectory(string directory)
    {
        var full = Path.GetFullPath(directory);
        if (!Directory.Exists(full)) throw new DirectoryNotFoundException("Quellordner fehlt: " + full);
        var info = new DirectoryInfo(full);
        if (info.Parent is null) throw new InvalidDataException("Ein gesamtes Laufwerk ist keine Mod. Bitte einen einzelnen Mod-Ordner auswählen.");
        for (DirectoryInfo? ancestor = info; ancestor is not null; ancestor = ancestor.Parent)
            if ((ancestor.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("Verknüpfte Quellpfade/Junctions sind nicht erlaubt: " + ancestor.FullName);
        EnsureNotGameDirectory(full);
    }

    public static void EnsureNotGameDirectory(string directory)
    {
        var files = Directory.EnumerateFiles(directory).Select(Path.GetFileName).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var directories = Directory.EnumerateDirectories(directory).Select(Path.GetFileName).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var executable = new[] { "ProjectZomboid64.exe", "ProjectZomboid32.exe", "ProjectZomboid64", "ProjectZomboid32",
            "ProjectZomboid64.json", "ProjectZomboid32.json", "steam.exe", "steamclient.dll" }.Any(files.Contains);
        var vanillaLayout = directories.Contains("media") && (directories.Contains("zombie") || directories.Contains("jre64") || directories.Contains("jre"));
        if (executable || vanillaLayout || directories.Contains("steamapps"))
            throw new InvalidDataException("Spiel-/Steam-Installationsordner erkannt. Dieser Ordner wird NICHT verpackt. Bitte die eigene Mod unter mods/<DeineMod>/ auswählen, nicht das Spielverzeichnis.");
        var info = new DirectoryInfo(directory);
        if (info.Name.Equals("common",StringComparison.OrdinalIgnoreCase) &&
            info.Parent?.Name.Equals("steamapps",StringComparison.OrdinalIgnoreCase) == true)
            throw new InvalidDataException("Steam-Bibliothekswurzel ist keine Mod-Quelle.");
    }

    internal static ModSourceCandidate? ReadCandidate(string root)
    {
        if (!Directory.Exists(root)) return null;
        // Do not follow a linked directory even for metadata discovery.
        if ((File.GetAttributes(root) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidDataException("Verknüpfter Mod-Ordner ist nicht erlaubt: " + root);
        var infoFiles = new List<string>();
        if (File.Exists(Path.Combine(root, "mod.info"))) infoFiles.Add("mod.info");
        foreach (var dir in Directory.EnumerateDirectories(root).Take(2049))
        {
            var name = Path.GetFileName(dir);
            if (!IsVersion(name)) continue;
            if ((File.GetAttributes(dir) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("Verknüpfter Versionsordner ist nicht erlaubt: " + dir);
            if (File.Exists(Path.Combine(dir, "mod.info"))) infoFiles.Add(name + "/mod.info");
        }
        if (infoFiles.Count == 0) return null;
        var ids = new List<string>(); string displayName = Path.GetFileName(root);
        foreach (var relative in infoFiles.OrderBy(s => s, StringComparer.Ordinal))
        {
            var file = new FileInfo(Path.Combine(root, relative));
            if ((file.Attributes & FileAttributes.ReparsePoint) != 0 || file.Length > 262144)
                throw new InvalidDataException("Ungültige oder zu große mod.info: " + file.FullName);
            var metadata = File.ReadLines(file.FullName).Select(line => line.Split('=', 2))
                .Where(parts => parts.Length == 2).GroupBy(parts => parts[0].Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.Last()[1].Trim(), StringComparer.OrdinalIgnoreCase);
            if (metadata.TryGetValue("id", out var id) && !string.IsNullOrWhiteSpace(id))
            {
                if (id.Length > 256 || id.Any(char.IsControl) || id.Contains(';')) throw new InvalidDataException("Ungültige Mod-ID in " + file.FullName);
                ids.Add(id);
            }
            if (metadata.TryGetValue("name", out var name) && !string.IsNullOrWhiteSpace(name)) displayName = name;
        }
        return new(root, Path.GetFileName(root), displayName, ids.Distinct(StringComparer.Ordinal).ToArray(),
            infoFiles.OrderBy(s => s, StringComparer.Ordinal).ToArray(), infoFiles.Any(s => s.Contains('/')));
    }

    /// <summary>Recognizes direct mods, mods/, Contents/, or a Workshop-project wrapper. Bounded to immediate children.</summary>
    public static ModSourceDiscovery Discover(string requestedPath)
    {
        if (string.IsNullOrWhiteSpace(requestedPath)) throw new InvalidDataException("Bitte einen eigenen Mod-Ordner auswählen.");
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(requestedPath));
        EnsureSafeDirectory(root);
        // Selecting 42/ must not discard common/ or other supported versions.
        if (IsVersion(Path.GetFileName(root)) && Directory.GetParent(root) is { } parent &&
            !parent.Name.Equals("mods", StringComparison.OrdinalIgnoreCase) && !parent.Name.Equals("Contents", StringComparison.OrdinalIgnoreCase))
        {
            var parentCandidate = ReadCandidate(parent.FullName);
            if (parentCandidate is { Versioned: true })
            {
                EnsureSafeDirectory(parent.FullName);
                return new(root, parent.FullName, true, [parentCandidate]);
            }
        }
        var namedContainer = Path.GetFileName(root).Equals("mods", StringComparison.OrdinalIgnoreCase) &&
            !File.Exists(Path.Combine(root,"mod.info")) && !Directory.Exists(Path.Combine(root,"common"));
        if (!namedContainer && ReadCandidate(root) is { } direct) return new(root, root, true, [direct]);
        string container = root;
        if (Directory.Exists(Path.Combine(root, "Contents", "mods"))) container = Path.Combine(root, "Contents", "mods");
        else if (Directory.Exists(Path.Combine(root, "mods"))) container = Path.Combine(root, "mods");
        EnsureSafeDirectory(container);
        var children = Directory.EnumerateDirectories(container).Take(2049).OrderBy(s => s, StringComparer.Ordinal).ToArray();
        if (children.Length > 2048) throw new InvalidDataException("Zu viele Unterordner. Bitte direkt den einzelnen Mod-Ordner auswählen.");
        var candidates = new List<ModSourceCandidate>();
        foreach (var child in children)
        {
            if (Path.GetFileName(child).StartsWith('.')) continue;
            if (ReadCandidate(child) is { } candidate) candidates.Add(candidate);
        }
        if (candidates.Count == 0)
            throw new InvalidDataException("Keine Einzelmod gefunden. Erwartet wird <DeineMod>/mod.info oder <DeineMod>/42/mod.info. Ein Spielordner oder ein media/-Ordner ist keine Mod. Contents/ musst du nicht selbst anlegen.");
        return new(root, container, false, candidates);
    }

    public static PackagePlan Resolve(AppProfile app, ModProject mod)
    {
        if (!Enum.IsDefined(mod.SourceMode)) throw new InvalidDataException("Unbekannter Quellmodus.");
        EnsureSafeDirectory(mod.ContentFolder);
        if (!IsZomboid(app)) {
            var root=new DirectoryInfo(Path.GetFullPath(mod.ContentFolder));
            if (root.Name.Equals("mods",StringComparison.OrdinalIgnoreCase) && !File.Exists(Path.Combine(root.FullName,"mod.info")) &&
                Directory.EnumerateDirectories(root.FullName).Skip(1).Any())
                throw new InvalidDataException("Ein mods/-Sammelordner mit mehreren Nachbarn ist keine einzelne Mod. Bitte eine konkrete Mod auswählen.");
            return new(false, root.FullName, [new(root.FullName, "", [], [])], [], []);
        }
        var discovery = Discover(mod.ContentFolder);
        IReadOnlyList<ModSourceCandidate> selected;
        if (mod.SourceMode == ModSourceMode.SelectedMods)
        {
            if (mod.SelectedModFolders is not { Count: > 0 })
                throw new InvalidDataException("Für ein Mehrmod-Paket zuerst die enthaltenen Mods ausdrücklich auswählen.");
            var names = mod.SelectedModFolders;
            if (names.Distinct(StringComparer.OrdinalIgnoreCase).Count() != names.Count ||
                names.Any(s => string.IsNullOrWhiteSpace(s) || s is "." or ".." || s.IndexOfAny(['/', '\\']) >= 0))
                throw new InvalidDataException("Ungültige gespeicherte Mod-Auswahl. Bitte neu auswählen.");
            selected = names.Select(name => discovery.Candidates.FirstOrDefault(c => string.Equals(c.FolderName, name, StringComparison.Ordinal))
                ?? throw new InvalidDataException("Ausgewählte Mod fehlt: " + name + ". Es werden keine anderen Mods automatisch ergänzt.")).ToArray();
        }
        else
        {
            if (discovery.Candidates.Count != 1)
                throw new InvalidDataException($"{discovery.Candidates.Count} Mods im gewählten Ordner gefunden. Bitte unter ‚Mod-Ordner wählen‘ genau eine auswählen. Für ein bewusstes gemeinsames Upload-Paket ‚Mehrmod-Paket zusammenstellen‘ verwenden. Es wird nicht automatisch alles hochgeladen.");
            selected = discovery.Candidates;
        }
        var sources = new List<PackageSource>(); var warnings = new List<string>();
        var allIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var candidate in selected.OrderBy(c => c.FolderName, StringComparer.Ordinal))
        {
            EnsureSafeDirectory(candidate.Root);
            if (candidate.ModIds.Count == 0) throw new InvalidDataException("Keine id= in der mod.info von " + candidate.FolderName);
            foreach (var id in candidate.ModIds)
                if (!allIds.Add(id)) throw new InvalidDataException("Mehrere ausgewählte Mods besitzen dieselbe Mod-ID: " + id);
            var prefix = "mods/" + candidate.FolderName;
            sources.Add(new(candidate.Root, prefix, candidate.InfoFiles,
                candidate.Versioned ? ["common"] : []));
            if (candidate.Versioned && !Directory.Exists(Path.Combine(candidate.Root, "common")))
                warnings.Add(candidate.FolderName + ": leerer common/-Ordner wird nur im Uploadpaket ergänzt. Die Quelle bleibt unverändert.");
        }
        if (!discovery.DirectMod)
            warnings.Add("Nur ausdrücklich aufgelistete Mods werden übernommen. Dateien neben mods/ und nicht ausgewählte Geschwister bleiben außerhalb des Pakets.");
        return new(true, discovery.RequestedPath, sources, allIds.OrderBy(s => s, StringComparer.Ordinal).ToArray(), warnings);
    }
}
