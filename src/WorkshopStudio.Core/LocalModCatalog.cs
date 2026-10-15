namespace WorkshopStudio.Core;

public sealed record LocalModCatalogResult(IReadOnlyList<ModSourceCandidate> Candidates,
    IReadOnlyList<string> Warnings);

public sealed record LocalModSuggestion(ModSourceCandidate Candidate, Guid? ExistingModId,
    string Reason);

/// <summary>Bounded, read-only discovery and conservative matching of local Zomboid mods.</summary>
public static class LocalModCatalog
{
    private const int MaxDepth = 6;
    private const int MaxDirectories = 4096;
    private const int MaxCandidates = 1024;
    private static readonly HashSet<string> SkipNames = new(StringComparer.OrdinalIgnoreCase) {
        ".git", ".vs", ".idea", "bin", "obj", "node_modules", "media", "common", "staging", "cache"
    };

    public static LocalModCatalogResult Discover(string selectedRoot)
    {
        ModSourceResolver.EnsureSafeDirectory(selectedRoot);
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(selectedRoot));
        if (ModSourceResolver.IsVersion(Path.GetFileName(root)) && Directory.GetParent(root) is { } parent &&
            ModSourceResolver.ReadCandidate(parent.FullName) is { Versioned: true })
            root = parent.FullName;
        var pending = new Queue<(string Path, int Depth)>();
        var found = new List<ModSourceCandidate>();
        var warnings = new List<string>();
        pending.Enqueue((root, 0));
        var visited = 0;
        while (pending.Count > 0)
        {
            var (current, depth) = pending.Dequeue();
            if (++visited > MaxDirectories)
                throw new InvalidDataException("Zu viele Unterordner. Bitte einen engeren Mod-Stammordner auswählen.");
            try
            {
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                {
                    warnings.Add("Verknüpfter Ordner übersprungen: " + current);
                    continue;
                }
                try { ModSourceResolver.EnsureNotGameDirectory(current); }
                catch (InvalidDataException) when (depth > 0) {
                    warnings.Add("Spiel-/Steam-Ordner übersprungen: " + current);
                    continue;
                }
                ModSourceCandidate? candidate;
                try { candidate = ModSourceResolver.ReadCandidate(current); }
                catch (InvalidDataException ex) {
                    warnings.Add("Ungültige Mod übersprungen: " + current + " (" + ex.Message + ")");
                    continue;
                }
                if (candidate is not null)
                {
                    found.Add(candidate);
                    if (found.Count > MaxCandidates)
                        throw new InvalidDataException("Zu viele Mods. Bitte einen engeren Ordner auswählen.");
                    // A B42 mod contains 42/mod.info; do not add that version as another mod.
                    continue;
                }
                if (depth == MaxDepth) continue;
                var children = Directory.EnumerateDirectories(current).Take(MaxDirectories + 1)
                    .OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToArray();
                if (children.Length > MaxDirectories)
                    throw new InvalidDataException("Zu viele Unterordner. Bitte einen engeren Mod-Stammordner auswählen.");
                foreach (var child in children)
                    if (!SkipNames.Contains(Path.GetFileName(child))) pending.Enqueue((child, depth + 1));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                warnings.Add("Ordner nicht lesbar: " + current + " (" + ex.Message + ")");
            }
        }
        if (found.Count == 0)
            throw new InvalidDataException("Keine lokale Mod gefunden. Erwartet wird ein Ordner mit mod.info oder 42/mod.info unter dem gewählten Stammordner.");
        return new(found.OrderBy(c => c.Root, StringComparer.OrdinalIgnoreCase).ToArray(), warnings);
    }

    public static IReadOnlyList<LocalModSuggestion> Suggest(IReadOnlyList<ModSourceCandidate> candidates,
        IReadOnlyList<ModProject> profiles, Guid appProfileId)
    {
        var relevant = profiles.Where(p => p.AppProfileId == appProfileId).ToArray();
        var suggestions = candidates.Select(candidate => SuggestOne(candidate, relevant)).ToArray();
        var repeated = suggestions.Where(s => s.ExistingModId is not null)
            .GroupBy(s => s.ExistingModId!.Value).Where(g => g.Count() > 1)
            .Select(g => g.Key).ToHashSet();
        return suggestions.Select(s => s.ExistingModId is { } id && repeated.Contains(id)
            ? s with { ExistingModId = null, Reason = "Mehrere lokale Mods passen zu demselben Profil – manuell prüfen" }
            : s).ToArray();
    }

    private static LocalModSuggestion SuggestOne(ModSourceCandidate candidate, ModProject[] profiles)
    {
        var path = profiles.Where(p => p.ContentFolder.Length > 0 && SamePath(p.ContentFolder, candidate.Root)).ToArray();
        if (path.Length == 1) return new(candidate, path[0].Id, "Bereits zugeordneter Quellpfad");
        if (path.Length > 1) return new(candidate, null, "Mehrere Profile verwenden diesen Pfad – manuell prüfen");
        var ids = candidate.ModIds.ToHashSet(StringComparer.Ordinal);
        var byId = profiles.Where(p => p.ModIds.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(ids.Contains)).ToArray();
        if (byId.Length == 1) return new(candidate, byId[0].Id, "Eindeutige technische Mod-ID");
        if (byId.Length > 1) return new(candidate, null, "Mod-ID passt zu mehreren Profilen – manuell prüfen");
        var byName = profiles.Where(p => string.Equals(p.Name.Trim(), candidate.DisplayName.Trim(), StringComparison.OrdinalIgnoreCase) ||
            string.Equals(p.Name.Trim(), candidate.FolderName.Trim(), StringComparison.OrdinalIgnoreCase)).ToArray();
        if (byName.Length == 1 && candidate.ModIds.Count > 0 &&
            (byName[0].ModIds.Length == 0 || byName[0].ModIds.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Any(ids.Contains)))
            return new(candidate, byName[0].Id, "Eindeutiger Name; Mod-ID beim Übernehmen prüfen");
        return new(candidate, null, byName.Length > 1 ? "Name passt zu mehreren Profilen – manuell prüfen" : "Kein eindeutiger Treffer");
    }

    private static bool SamePath(string left, string right)
    {
        try { return string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase); }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException) { return false; }
    }
}
