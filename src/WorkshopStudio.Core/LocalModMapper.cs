namespace WorkshopStudio.Core;

public sealed record LocalModMappingSelection(ModSourceCandidate Candidate, Guid? ExistingModId, bool CreateNew);
public sealed record LocalModMappingResult(Workspace Workspace, int Updated, int Created, Guid? LastModId);

/// <summary>Prepares an atomic local-only workspace change; never queues or uploads a mod.</summary>
public static class LocalModMapper
{
    public static LocalModMappingResult Prepare(Workspace original, AppProfile app,
        IReadOnlyList<LocalModMappingSelection> selections, string studioRoot)
    {
        if (!ModSourceResolver.IsZomboid(app))
            throw new InvalidOperationException("Die Sammelzuordnung unterstützt Project-Zomboid-Mods.");
        if (selections.Count == 0)
            throw new InvalidDataException("Mindestens eine Mod auswählen.");
        if (selections.Any(s => (s.ExistingModId is not null) == s.CreateNew))
            throw new InvalidDataException("Jede lokale Mod benötigt genau ein Zielprofil oder die Auswahl 'Neues Profil'.");
        if (selections.Where(s => s.ExistingModId is not null).GroupBy(s => s.ExistingModId!.Value)
            .Any(g => g.Count() > 1))
            throw new InvalidDataException("Ein vorhandenes Profil kann nur einer lokalen Mod zugeordnet werden.");

        var staged = WorkspaceStore.Clone(original);
        var stagedApp = staged.Apps.Single(a => a.Id == app.Id && a.AppId == app.AppId);
        var updated = 0; var created = 0; Guid? lastId = null;
        var selectedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var selection in selections)
        {
            var path = Path.GetFullPath(selection.Candidate.Root);
            if (!selectedPaths.Add(path)) throw new InvalidDataException("Ein Mod-Ordner wurde mehrfach gewählt: " + path);
            if (PackageBuilder.IsWithin(path, studioRoot) || PackageBuilder.IsWithin(studioRoot, path))
                throw new InvalidDataException("Mod-Quelle darf nicht im Studio-Datenordner liegen: " + path);
            ModProject profile;
            if (selection.ExistingModId is { } id)
            {
                profile = staged.Mods.SingleOrDefault(m => m.Id == id && m.AppProfileId == app.Id)
                    ?? throw new InvalidDataException("Zielprofil fehlt oder gehört zu einem anderen Spiel.");
                if (staged.Jobs.Any(j => j.ModId == id && j.Status == JobStatus.Pending))
                    throw new InvalidOperationException(profile.Name + ": wartenden Upload zuerst entfernen, bevor der Quellpfad geändert wird.");
                updated++;
            }
            else
            {
                profile = new ModProject { AppProfileId = app.Id, Name = selection.Candidate.DisplayName,
                    Version = "", ChangeNote = "", Visibility = app.DefaultVisibility,
                    WorkshopId = 0, TestWorkshopId = 0, AutoPublish = false };
                staged.Mods.Add(profile);
                created++;
            }

            profile.ContentFolder = path;
            profile.SourceMode = ModSourceMode.SingleMod;
            profile.SelectedModFolders = [];
            profile.AutoPublish = false;
            profile.ChangeState = "Nicht geprüft";
            var plan = ModSourceResolver.Resolve(stagedApp, profile);
            if (plan.Sources.Count != 1 || !string.Equals(plan.Sources[0].SourceRoot, path, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Die Zuordnung muss genau eine lokale Mod enthalten: " + path);
            if (!plan.ModIds.ToHashSet(StringComparer.Ordinal).SetEquals(selection.Candidate.ModIds))
                throw new InvalidDataException("Die Mod-IDs haben sich seit der Ordnersuche geändert. Bitte erneut suchen: " + path);
            var previousIds = original.Mods.FirstOrDefault(m => m.Id == profile.Id)?.ModIds
                .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [];
            if (previousIds.Length > 0 && !previousIds.Intersect(plan.ModIds, StringComparer.Ordinal).Any())
                throw new InvalidDataException(profile.Name + ": technische Mod-ID passt nicht zum gewählten Ordner.");
            profile.ModIds = string.Join(';', plan.ModIds);
            lastId = profile.Id;
        }
        foreach (var path in selectedPaths)
            if (staged.Mods.Count(m => m.ContentFolder.Length > 0 && SamePath(m.ContentFolder, path)) > 1)
                throw new InvalidDataException("Ein lokaler Mod-Ordner würde mehreren Profilen zugeordnet: " + path);
        WorkspaceStore.Validate(staged);
        return new(staged, updated, created, lastId);
    }

    private static bool SamePath(string left, string right)
    {
        try { return string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase); }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException) { return false; }
    }
}
