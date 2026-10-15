using System.Text.Json.Serialization;

namespace WorkshopStudio.Core;

public enum ReleaseChannel { Test, Live }
public enum JobStatus { Pending, Preparing, Uploading, Succeeded, Skipped, Failed, Cancelled, Interrupted }
public enum GameTemplate { Generic, ProjectZomboid41, ProjectZomboid42 }
public enum ModSourceMode { SingleMod, SelectedMods }
public enum ScheduleMode { Manual, Interval, Daily }

public sealed class AppProfile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public uint AppId { get; set; } = 108600;
    public string Name { get; set; } = "Project Zomboid";
    public string HeaderImageUrl { get; set; } = "";
    public string IconImageUrl { get; set; } = "";
    public string HeaderCachePath { get; set; } = "";
    public string IconCachePath { get; set; } = "";
    public string SourceRoot { get; set; } = "";
    public string DefaultExclusions { get; set; } = ".git/\n.vs/\nbin/\nobj/\n*.tmp\n*.bak\n.DS_Store";
    public GameTemplate Template { get; set; } = GameTemplate.ProjectZomboid42;
    public int DefaultVisibility { get; set; } = 2;
    public string Notes { get; set; } = "";
    public DateTimeOffset? MetadataUpdatedAt { get; set; }
    [JsonIgnore] public string Label => $"{Name} · {AppId}";
    [JsonIgnore] public string Artwork => File.Exists(HeaderCachePath) ? HeaderCachePath : "";
    [JsonIgnore] public string Icon => File.Exists(IconCachePath) ? IconCachePath : Artwork;
}

public sealed class ModProject
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AppProfileId { get; set; }
    public string Name { get; set; } = "Neue Mod";
    public string ModIds { get; set; } = "";
    public string Version { get; set; } = "1.0.0";
    public string ContentFolder { get; set; } = "";
    public ModSourceMode SourceMode { get; set; } = ModSourceMode.SingleMod;
    public List<string> SelectedModFolders { get; set; } = [];
    [JsonIgnore] public string SourceModeLabel => SourceMode == ModSourceMode.SelectedMods ? $"Auswahlpaket · {SelectedModFolders.Count} Mods" : "Einzelmod";
    [JsonIgnore] public string VersionLabel => string.IsNullOrWhiteSpace(Version) ? "Version nicht angegeben" : "Version " + Version;
    public string PreviewFile { get; set; } = "";
    public string Description { get; set; } = "";
    // Tags are managed through Steam's own web editor, not an invented SteamCMD field.
    public string Tags { get; set; } = "";
    public string ChangeNote { get; set; } = "Update {version}";
    public string Exclusions { get; set; } = "";
    public int Visibility { get; set; } = 2;
    public ulong WorkshopId { get; set; }
    public ulong TestWorkshopId { get; set; }
    public string TestTitleSuffix { get; set; } = " [TEST]";
    public bool AutoPublish { get; set; }
    public bool Enabled { get; set; } = true;
    public bool NeedsLiveReconciliation { get; set; }
    public bool NeedsTestReconciliation { get; set; }
    public string LastLiveFingerprint { get; set; } = "";
    public string LastTestFingerprint { get; set; } = "";
    public DateTimeOffset? LastPublishedAt { get; set; }
    public long LastPackageBytes { get; set; }
    public string Notes { get; set; } = "";
    // Imported items retain their owner and remote baseline. An absent local source is read-only.
    public ulong OwnerSteamId64 { get; set; }
    public bool ImportedFromWorkshop { get; set; }
    public DateTimeOffset? RemoteBaselineUpdatedAt { get; set; }
    [JsonIgnore] public string AppName { get; set; } = "";
    [JsonIgnore] public string ChangeState { get; set; } = "Nicht geprüft";
    [JsonIgnore] public string WorkshopLabel => WorkshopId == 0 ? "Noch nicht veröffentlicht" : WorkshopId.ToString();
    [JsonIgnore] public string LastPublishedLabel => LastPublishedAt?.ToLocalTime().ToString("dd.MM.yyyy HH:mm") ?? "Noch kein Upload";
    public ulong GetWorkshopId(ReleaseChannel c) => c == ReleaseChannel.Live ? WorkshopId : TestWorkshopId;
    public void SetWorkshopId(ReleaseChannel c, ulong id) { if (c == ReleaseChannel.Live) WorkshopId = id; else TestWorkshopId = id; }
    public string GetFingerprint(ReleaseChannel c) => c == ReleaseChannel.Live ? LastLiveFingerprint : LastTestFingerprint;
    public void SetFingerprint(ReleaseChannel c, string hash) { if (c == ReleaseChannel.Live) LastLiveFingerprint = hash; else LastTestFingerprint = hash; }
    public bool NeedsReconciliation(ReleaseChannel c) => c == ReleaseChannel.Live ? NeedsLiveReconciliation : NeedsTestReconciliation;
    public void SetReconciliation(ReleaseChannel c, bool value) { if (c == ReleaseChannel.Live) NeedsLiveReconciliation = value; else NeedsTestReconciliation = value; }
}

public sealed class WorkshopCollection
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AppProfileId { get; set; }
    public string Name { get; set; } = "Neue Collection";
    public string Description { get; set; } = "";
    public ulong SteamCollectionId { get; set; }
    public List<Guid> ModProjectIds { get; set; } = [];
    public List<ulong> ExternalWorkshopIds { get; set; } = [];
    public List<ulong> RemoteWorkshopIds { get; set; } = [];
    public DateTimeOffset? LastSyncedAt { get; set; }
    [JsonIgnore] public string AppName { get; set; } = "";
    [JsonIgnore] public int ItemCount => ModProjectIds.Count + ExternalWorkshopIds.Count;
    [JsonIgnore] public string LinkState => SteamCollectionId == 0 ? "Nur lokal" : $"Steam · {SteamCollectionId}";
    [JsonIgnore] public string SyncLabel => LastSyncedAt?.ToLocalTime().ToString("dd.MM.yyyy HH:mm") ?? "Noch nicht abgerufen";
}

public sealed class SteamAccount
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string UserName { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public bool RememberPassword { get; set; }
    public ulong VerifiedWebSteamId64 { get; set; }
    public DateTimeOffset? WebIdentityCheckedAt { get; set; }
    public ulong VerifiedUploadSteamId64 { get; set; }
    public DateTimeOffset? UploadIdentityCheckedAt { get; set; }
    [JsonIgnore] public string Label => string.IsNullOrWhiteSpace(DisplayName) ? UserName : $"{DisplayName} ({UserName})";
}

public sealed class PublishJob
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ModId { get; set; }
    public string ModName { get; set; } = "";
    public uint AppId { get; set; }
    public ReleaseChannel Channel { get; set; }
    public JobStatus Status { get; set; } = JobStatus.Pending;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
    public string Message { get; set; } = "Wartet auf Freigabe";
    public string LogFile { get; set; } = "";
    public string StageFolder { get; set; } = "";
    public long PackageBytes { get; set; }
    public ulong PublishedFileId { get; set; }
    public string RemoteVerification { get; set; } = "Nicht geprüft";
    public bool Automatic { get; set; }
    [JsonIgnore] public string CreatedLabel => CreatedAt.ToLocalTime().ToString("dd.MM. HH:mm");
    [JsonIgnore] public string StatusLabel => Status switch {
        JobStatus.Pending => "Wartend", JobStatus.Preparing => "Paketierung", JobStatus.Uploading => "Upload",
        JobStatus.Succeeded => "Erfolgreich", JobStatus.Skipped => "Unverändert", JobStatus.Failed => "Fehler",
        JobStatus.Cancelled => "Abgebrochen", _ => "Unterbrochen" };
    [JsonIgnore] public string SizeLabel => Format.Bytes(PackageBytes);
    [JsonIgnore] public string DurationLabel => StartedAt is null ? "–" : $"{((FinishedAt ?? DateTimeOffset.UtcNow) - StartedAt.Value).TotalSeconds:N0} s";
}

public sealed class StudioSettings
{
    public string Theme { get; set; } = "Dark";
    public string Accent { get; set; } = "#FF781F";
    public double UiScale { get; set; } = 1;
    public bool StartMaximized { get; set; }
    public string SteamCmdPath { get; set; } = "";
    public Guid? ActiveAccountId { get; set; }
    public bool AutoLogin { get; set; }
    public bool RememberWebSession { get; set; } = true;
    public bool RefreshMetadataOnStartup { get; set; } = true;
    public string StoreLanguage { get; set; } = "german";
    public string StoreCountry { get; set; } = "DE";
    public int HttpTimeoutSeconds { get; set; } = 30;
    public int UploadTimeoutMinutes { get; set; } = 60;
    public int AuthenticationTimeoutMinutes { get; set; } = 5;
    public int ImageCacheDays { get; set; } = 7;
    public ReleaseChannel DefaultChannel { get; set; } = ReleaseChannel.Test;
    public bool OnlyChanged { get; set; } = true;
    public bool ExcludePotentialSecrets { get; set; } = true;
    public bool ValidateZomboidLayout { get; set; } = true;
    public bool OpenWorkshopAfterPublish { get; set; }
    public bool StopQueueOnError { get; set; } = true;
    public bool KeepSuccessfulStaging { get; set; }
    public bool CreatePackageBackups { get; set; } = true;
    public int BackupRetention { get; set; } = 10;
    public int HistoryRetention { get; set; } = 500;
    public int MaxPackageMiB { get; set; } = 4096;
    public int MaxFileCount { get; set; } = 100000;
    public bool AutomationEnabled { get; set; }
    public bool WatchFolders { get; set; }
    public int DebounceSeconds { get; set; } = 30;
    public ScheduleMode ScheduleMode { get; set; } = ScheduleMode.Manual;
    public int ScheduleIntervalMinutes { get; set; } = 30;
    public string DailyTime { get; set; } = "20:00";
    public ReleaseChannel AutomationChannel { get; set; } = ReleaseChannel.Test;
    public bool AllowLiveAutomation { get; set; }
    public string UpdateManifestUrl { get; set; } = "";
    public bool CheckToolUpdatesOnStartup { get; set; } = true;
    public string ToolUpdateChannel { get; set; } = "stable";
    public string GlobalExclusions { get; set; } = ".git/\n.vs/\n.idea/\nbin/\nobj/\nnode_modules/\n*.tmp\n*.bak";
    public int WorkshopScanTimeoutSeconds { get; set; } = 30;
    public int ConfigMaxKiB { get; set; } = 512;
}

public enum RemoteFieldStatus { NotRequested, Known, NotProvided, AccessDenied, Error, Unsupported }
public sealed class RemoteWorkshopItem
{
    public ulong Id { get; set; }
    public ulong OwnerSteamId64 { get; set; }
    public uint CreatorAppId { get; set; }
    public uint ConsumerAppId { get; set; }
    public int? FileType { get; set; }
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string PreviewUrl { get; set; } = "";
    public string PreviewCachePath { get; set; } = "";
    public int? Visibility { get; set; }
    public List<string> Tags { get; set; } = [];
    public DateTimeOffset? CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public long? FileSize { get; set; }
    public int? Subscriptions { get; set; }
    public int? Views { get; set; }
    public string AuthorVersion { get; set; } = "";
    public string VersionSource { get; set; } = "Nicht angegeben";
    public string RemoteChangelogStatus { get; set; } = "Nicht angefragt";
    public string RemoteChangelog { get; set; } = "";
    public string MetadataSource { get; set; } = "Steam-Profil";
    public DateTimeOffset FetchedAt { get; set; } = DateTimeOffset.UtcNow;
    public Dictionary<string,RemoteFieldStatus> FieldStatus { get; set; } = [];
    public Guid? LocalModId { get; set; }
    [JsonIgnore] public string TypeLabel => FileType switch { 2 => "Collection", 0 => "Datei / Mod", null => "Nicht ermittelt", _ => "Anderer Inhalt (" + FileType + ")" };
    [JsonIgnore] public string VersionLabel => string.IsNullOrEmpty(AuthorVersion) ? "Nicht angegeben" : AuthorVersion + " · " + VersionSource;
    [JsonIgnore] public string AppLabel => ConsumerAppId == 0 ? "App unbekannt" : ConsumerAppId.ToString();
    [JsonIgnore] public string LocalState => LocalModId is null ? "Nur remote" : "Lokal zugeordnet";
    [JsonIgnore] public string PreviewArtwork => File.Exists(PreviewCachePath) ? PreviewCachePath : "";
}

public sealed class WorkshopScanReport
{
    public ulong SteamId64 { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset FinishedAt { get; set; }
    public int PagesRead { get; set; }
    public int ItemsSeen { get; set; }
    public bool Complete { get; set; }
    public string Source { get; set; } = "Steam Community / myworkshopfiles";
    public string Error { get; set; } = "";
    [JsonIgnore] public string Summary => $"{ItemsSeen} Einträge · {PagesRead} Seiten · " + (Complete ? "vollständig erreichbarer Bestand" : "teilweise / nicht bestätigt") + (string.IsNullOrEmpty(Error) ? "" : " · " + Error);
}

public sealed class Workspace
{
    public int SchemaVersion { get; set; } = 2;
    public StudioSettings Settings { get; set; } = new();
    public List<AppProfile> Apps { get; set; } = [new AppProfile()];
    public List<ModProject> Mods { get; set; } = [];
    public List<WorkshopCollection> Collections { get; set; } = [];
    public List<SteamAccount> Accounts { get; set; } = [];
    public List<PublishJob> Jobs { get; set; } = [];
    public List<RemoteWorkshopItem> RemoteItems { get; set; } = [];
    public WorkshopScanReport? LastWorkshopScan { get; set; }
}

public static class Format
{
    public static string Bytes(long value) => value >= 1073741824 ? $"{value / 1073741824d:N2} GB"
        : value >= 1048576 ? $"{value / 1048576d:N1} MB" : value >= 1024 ? $"{value / 1024d:N1} KB" : $"{value} B";
}
