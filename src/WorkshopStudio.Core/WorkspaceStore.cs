using System.Text.Json;
using System.Text.Json.Serialization;

namespace WorkshopStudio.Core;

public sealed class WorkspaceStore
{
    public static readonly JsonSerializerOptions JsonOptions = new() {
        WriteIndented = true, PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() } };
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    public string Root { get; }
    public string FilePath => Path.Combine(Root, "workspace.json");
    public string StagingRoot => Path.Combine(Root, "staging");
    public string BackupRoot => Path.Combine(Root, "backups");
    public string LogRoot => Path.Combine(Root, "logs");
    public WorkspaceStore(string root)
    {
        Root = Path.GetFullPath(root);
        foreach (var p in new[] { Root, StagingRoot, BackupRoot, LogRoot, Path.Combine(Root, "cache") }) Directory.CreateDirectory(p);
    }
    public async Task<Workspace> LoadAsync()
    {
        if (!File.Exists(FilePath)) return new Workspace();
        var data = await File.ReadAllTextAsync(FilePath);
        var workspace = JsonSerializer.Deserialize<Workspace>(data, JsonOptions) ?? throw new InvalidDataException("Arbeitsbereich ist leer.");
        if (workspace.SchemaVersion == 1) {
            // Keep an immutable copy of the exact old file before any schema or accent migration.
            var backup = Path.Combine(BackupRoot, "workspace-schema1-" + DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + ".json");
            await File.WriteAllTextAsync(backup, data);
            UpgradeSchema1(workspace);
        }
        Validate(workspace);
        // Migrate only the former stock accent; user-selected custom colors remain unchanged.
        if(workspace.Settings.Accent.Equals("#FF944D",StringComparison.OrdinalIgnoreCase))workspace.Settings.Accent="#FF781F";
        foreach (var job in workspace.Jobs.Where(j => j.Status is JobStatus.Preparing or JobStatus.Uploading)) {
            job.Status = JobStatus.Interrupted; job.Message = "Voriger Vorgang wurde unterbrochen. Steam-ID vor erneutem Upload prüfen.";
            job.FinishedAt = DateTimeOffset.UtcNow;
        }
        return workspace;
    }
    public async Task SaveAsync(Workspace workspace)
    {
        Validate(workspace);
        // Serialize before yielding: callers use one UI synchronization context.
        var json = JsonSerializer.Serialize(workspace, JsonOptions);
        await _writeLock.WaitAsync();
        var temp = FilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try {
            await File.WriteAllTextAsync(temp, json);
            if (File.Exists(FilePath)) File.Copy(FilePath, FilePath + ".bak", true);
            File.Move(temp, FilePath, true);
        } finally { if (File.Exists(temp)) File.Delete(temp); _writeLock.Release(); }
    }
    public static T Clone<T>(T value) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value, JsonOptions), JsonOptions)!;
    public static void UpgradeSchema1(Workspace workspace)
    {
        if(workspace.SchemaVersion!=1)throw new InvalidDataException("Nur Arbeitsbereiche der Version 1 können migriert werden.");
        workspace.SchemaVersion=2;
        workspace.RemoteItems??=[];
    }
    public static void Validate(Workspace w)
    {
        if (w.SchemaVersion != 2) throw new InvalidDataException("Diese Arbeitsbereich-Version wird nicht unterstützt.");
        if (w.Apps is null || w.Mods is null || w.Accounts is null || w.Collections is null || w.Jobs is null || w.Settings is null || w.RemoteItems is null)
            throw new InvalidDataException("Arbeitsbereich enthält fehlende Datenlisten.");
        if (w.Apps.GroupBy(a => a.AppId).Any(g => g.Count() > 1) || w.Apps.Any(a => a.AppId == 0))
            throw new InvalidDataException("AppIDs müssen eindeutig und größer als null sein.");
        if (w.Apps.Select(a=>a.Id).Distinct().Count()!=w.Apps.Count || w.Mods.Select(a=>a.Id).Distinct().Count()!=w.Mods.Count)
            throw new InvalidDataException("Doppelte interne IDs im Arbeitsbereich.");
        if (w.Mods.Any(m => !w.Apps.Any(a => a.Id == m.AppProfileId)) || w.Collections.Any(c => !w.Apps.Any(a=>a.Id==c.AppProfileId)))
            throw new InvalidDataException("Mod oder Collection verweist auf eine nicht vorhandene App.");
        foreach (var c in w.Collections)
            if (c.ModProjectIds.Any(id => !w.Mods.Any(m=>m.Id==id && m.AppProfileId==c.AppProfileId)))
                throw new InvalidDataException("Collection enthält eine fehlende Mod oder eine Mod einer anderen App.");
        if (w.Collections.Select(a=>a.Id).Distinct().Count()!=w.Collections.Count || w.Accounts.Select(a=>a.Id).Distinct().Count()!=w.Accounts.Count || w.Jobs.Select(a=>a.Id).Distinct().Count()!=w.Jobs.Count)
            throw new InvalidDataException("Doppelte Collection-, Konto- oder Vorgangs-ID.");
        if (w.Settings.ActiveAccountId is {} active && !w.Accounts.Any(a=>a.Id==active)) w.Settings.ActiveAccountId=null;
        var workshopIds=w.Mods.SelectMany(m=>new[]{m.WorkshopId,m.TestWorkshopId}).Where(id=>id>0).ToArray();
        if(workshopIds.Distinct().Count()!=workshopIds.Length)throw new InvalidDataException("Workshop-IDs dürfen nicht mehrfach zugeordnet sein.");
        if (w.RemoteItems.Any(i => i.Id == 0 || i.OwnerSteamId64 == 0) ||
            w.RemoteItems.GroupBy(i => (i.OwnerSteamId64, i.Id)).Any(g => g.Count() > 1) ||
            w.RemoteItems.Any(i => i.LocalModId is { } id && !w.Mods.Any(m => m.Id == id)))
            throw new InvalidDataException("Workshop-Bibliothek enthält ungültige oder doppelte Zuordnungen.");
        if(w.Apps.Any(a=>!Enum.IsDefined(a.Template))||w.Jobs.Any(j=>!Enum.IsDefined(j.Status)||!Enum.IsDefined(j.Channel)))throw new InvalidDataException("Unbekannter Profil- oder Vorgangstyp.");
        if(w.Mods.Any(m=>!Enum.IsDefined(m.SourceMode)||m.SelectedModFolders is null))throw new InvalidDataException("Ungültiger Mod-Quellmodus oder fehlende Auswahl.");
        foreach(var account in w.Accounts)SteamCmdRunner.ValidateUserName(account.UserName);
        ValidateSettings(w.Settings);
    }
    public static void ValidateSettings(StudioSettings s)
    {
        if(!Enum.IsDefined(s.DefaultChannel)||!Enum.IsDefined(s.AutomationChannel)||!Enum.IsDefined(s.ScheduleMode))throw new InvalidDataException("Unbekannter Kanal oder Zeitplan.");
        if(s.Theme is not ("Dark" or "Light") || !System.Text.RegularExpressions.Regex.IsMatch(s.Accent??"",@"^#[0-9a-fA-F]{6}$"))throw new InvalidDataException("Theme oder Akzentfarbe ungültig.");
        if (s.HttpTimeoutSeconds is < 5 or > 300 || s.UploadTimeoutMinutes is < 1 or > 1440 || s.AuthenticationTimeoutMinutes is < 1 or > 60)
            throw new InvalidDataException("Zeitlimits außerhalb des gültigen Bereichs.");
        if (s.DebounceSeconds is < 5 or > 3600 || s.ScheduleIntervalMinutes is < 1 or > 10080)
            throw new InvalidDataException("Automationsintervalle außerhalb des gültigen Bereichs.");
        if (s.MaxPackageMiB is < 1 or > 1048576 || s.MaxFileCount is < 1 or > 2000000 || s.BackupRetention is < 1 or > 1000 || s.HistoryRetention is < 10 or > 50000)
            throw new InvalidDataException("Größen- oder Aufbewahrungsgrenzen sind ungültig.");
        if (!double.IsFinite(s.UiScale) || s.UiScale is < .8 or > 1.5 || s.ImageCacheDays is < 0 or > 365) throw new InvalidDataException("UI-Skalierung oder Cachealter ungültig.");
        if (!TimeOnly.TryParseExact(s.DailyTime, "HH:mm", out _)) throw new InvalidDataException("Tägliche Uhrzeit muss HH:mm sein.");
        if (s.AllowLiveAutomation == false && s.AutomationEnabled && s.AutomationChannel == ReleaseChannel.Live)
            throw new InvalidDataException("Live-Automation muss ausdrücklich freigegeben werden.");
        if (s.WorkshopScanTimeoutSeconds is < 5 or > 300 || s.ConfigMaxKiB is < 16 or > 4096)
            throw new InvalidDataException("Workshop- oder Config-Grenzen sind ungültig.");
        if (!string.IsNullOrWhiteSpace(s.UpdateManifestUrl) && (!Uri.TryCreate(s.UpdateManifestUrl, UriKind.Absolute, out var u) || u.Scheme != "https"))
            throw new InvalidDataException("Update-Manifest muss eine HTTPS-Adresse sein.");
    }
}
