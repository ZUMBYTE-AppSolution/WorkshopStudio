using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using WorkshopStudio.Core;
using WorkshopStudio.Desktop.Services;
using WorkshopStudio.Desktop.Views;

namespace WorkshopStudio.Desktop.ViewModels;

public sealed class MainViewModel : ObservableObject, IDisposable
{
    public Workspace Data { get; private set; }
    public WorkspaceStore Store { get; }
    private readonly SteamService _steam;
    private readonly SteamCmdRunner _runner = new();
    private readonly PackageBuilder _builder;
    private CancellationTokenSource? _operation;
    private readonly List<FileSystemWatcher> _watchers=[];
    private readonly Dictionary<Guid,DateTimeOffset> _dirty=[];
    private readonly DispatcherTimer _timer;
    private DateTimeOffset _lastSchedule=DateTimeOffset.UtcNow;
    private DateOnly? _lastDaily;
    private bool _tickBusy;
    private bool _busy;
    public bool IsBusy { get=>_busy; private set {Set(ref _busy,value);Raise(nameof(CanEdit));} }
    public bool CanEdit=>!IsBusy;
    private string _currentPage="dashboard";
    public string CurrentPage {get=>_currentPage;set{Set(ref _currentPage,value);Raise(nameof(PageTitle));Raise(nameof(PageSubtitle));}}
    public string PageTitle=>CurrentPage switch {"dashboard"=>"Willkommen zurück!","library"=>"Meine Workshop-Inhalte","apps"=>"Spiele & AppIDs","mods"=>"Deine Mods","collections"=>"Collections","queue"=>"Veröffentlichungen","history"=>"Upload-Verlauf","accounts"=>"Steam & Konten","automation"=>"Automatisierung","settings"=>"Einstellungen",_=>"Hilfe & System"};
    public string PageSubtitle=>CurrentPage switch {"dashboard"=>"Verwalte deine Mods, Collections und Veröffentlichungen für den Steam Workshop.","library"=>"Eigene Veröffentlichungen aus der angemeldeten Steam-Websitzung.","apps"=>"Mehrere Spiele verwalten. Inhalte und Profile sauber trennen.","mods"=>"Entwickeln, prüfen und gezielt veröffentlichen.","collections"=>"Eigene Pakete organisieren und Steam-Collections verknüpfen.","queue"=>"Kontrollierte Releases statt unbeabsichtigter Updates.","history"=>"Jeder Vorgang mit Ergebnis, Paketgröße und Protokoll.","accounts"=>"SteamCMD für Uploads. Steam Community für Web-Funktionen.","automation"=>"Verzeichnisüberwachung und Zeitpläne. Bewusst freigegeben.","settings"=>"Arbeitsweise, Sicherheit und Darstellung anpassen.",_=>"Technische Hinweise, Datenablage und Diagnose."};
    private string _search="";
    public string Search {get=>_search;set{Set(ref _search,value);Refresh();}}
    public StudioSettings Settings=>Data.Settings;
    public StudioSettings SettingsDraft {get;private set;}=new();
    public IEnumerable<AppProfile> Apps=>Data.Apps.ToArray();
    public IEnumerable<AppProfile> VisibleApps=>Data.Apps.Where(a=>Matches(a.Label));
    public IEnumerable<ModProject> VisibleMods=>Data.Mods.Where(m=>Matches(m.Name+" "+m.AppName+" "+m.ModIds));
    public IEnumerable<RemoteWorkshopItem> VisibleRemoteItems=>Data.RemoteItems.Where(i=>ActiveAccount is { VerifiedWebSteamId64: > 0 } a && i.OwnerSteamId64==a.VerifiedWebSteamId64 && Matches(i.Title+" "+i.Id+" "+i.ConsumerAppId)).OrderBy(i=>i.ConsumerAppId).ThenBy(i=>i.Title);
    public string WorkshopScanSummary=>Data.LastWorkshopScan is { } scan && scan.SteamId64==ActiveAccount?.VerifiedWebSteamId64 ? scan.Summary : "Noch kein Scan für dieses Konto.";
    public IEnumerable<WorkshopCollection> Collections=>Data.Collections.Where(c=>Matches(c.Name+" "+c.AppName));
    public IEnumerable<SteamAccount> Accounts=>Data.Accounts.ToArray();
    public IEnumerable<PublishJob> Queue=>Data.Jobs.Where(j=>j.Status is JobStatus.Pending or JobStatus.Preparing or JobStatus.Uploading).OrderBy(j=>j.CreatedAt);
    public IEnumerable<PublishJob> History=>Data.Jobs.Where(j=>j.Status is not (JobStatus.Pending or JobStatus.Preparing or JobStatus.Uploading)).OrderByDescending(j=>j.CreatedAt);
    public IEnumerable<PublishJob> RecentJobs=>Data.Jobs.OrderByDescending(j=>j.CreatedAt).Take(5);
    public IEnumerable<DashboardAppCard> DashboardApps=>VisibleApps.Select(a=>new DashboardAppCard(a,Data.Mods.Count(m=>m.AppProfileId==a.Id),Data.Collections.Count(c=>c.AppProfileId==a.Id)));
    public string DashboardDate=>DateTime.Now.ToString("dd. MMMM yyyy",CultureInfo.GetCultureInfo("de-DE"));
    public string DashboardTime=>DateTime.Now.ToString("HH:mm");
    public int PublishedCount=>Data.Jobs.Count(j=>j.Status==JobStatus.Succeeded);
    public string QueueSummary=>$"{PendingCount} in der Warteschlange";
    public bool HasNoJobs=>Data.Jobs.Count==0;
    public int AppCount=>Data.Apps.Count;
    public int ModCount=>Data.Mods.Count;
    public int CollectionCount=>Data.Collections.Count;
    public int PendingCount=>Data.Jobs.Count(j=>j.Status==JobStatus.Pending);
    public int SuccessCount=>Data.Jobs.Count(j=>j.Status==JobStatus.Succeeded && j.FinishedAt>DateTimeOffset.UtcNow.AddDays(-7));
    public int FailedCount=>Data.Jobs.Count(j=>j.Status is JobStatus.Failed or JobStatus.Interrupted);
    public string SuccessRate {get {var jobs=Data.Jobs.Where(j=>j.Status is JobStatus.Succeeded or JobStatus.Failed).ToList();return jobs.Count==0?"Noch keine Daten":$"{jobs.Count(j=>j.Status==JobStatus.Succeeded)*100d/jobs.Count:N0} % erfolgreich";}}
    public bool HasNoMods=>Data.Mods.Count==0;
    public string DataPath=>Store.Root;
    public string SteamCmdStatus=>File.Exists(Settings.SteamCmdPath)?"SteamCMD installiert":"SteamCMD fehlt";
    private readonly Dictionary<Guid,string> _sessionPasswords = [];
    private string _loginLog = "Noch kein Login-Vorgang in dieser Sitzung.";
    public string LoginLog { get => _loginLog; private set => Set(ref _loginLog, value); }
    private string _webLoginStatus = "Websitzung noch nicht geprüft";
    public string WebLoginStatus { get => _webLoginStatus; private set => Set(ref _webLoginStatus, value); }
    private string _lastLoginLogFile = "";
    private string _loginStatus="Noch nicht geprüft";
    public string LoginStatus {get=>_loginStatus;private set=>Set(ref _loginStatus,value);}
    private string _status="Bereit. Neue Uploads werden erst nach deiner Freigabe gestartet.";
    public string Status {get=>_status;set=>Set(ref _status,value);}
    private string _log="";
    public string LiveLog {get=>_log;private set=>Set(ref _log,value);}
    public string AutomationStatus=>Settings.AutomationEnabled?"Aktiv, solange Workshop Studio geöffnet ist":"Deaktiviert";
    public SteamAccount? ActiveAccount=>Data.Accounts.FirstOrDefault(a=>a.Id==Settings.ActiveAccountId);
    public string ActiveAccountLabel=>ActiveAccount?.Label??"Kein Upload-Konto";
    public AppProfile? SelectedApp {get;set;}
    public ModProject? SelectedMod {get;set;}
    private RemoteWorkshopItem? _selectedRemoteItem;
    public RemoteWorkshopItem? SelectedRemoteItem {get=>_selectedRemoteItem;set{if(Set(ref _selectedRemoteItem,value)&&value is not null)_=LoadPreviewAsync(value);}}
    private async Task LoadPreviewAsync(RemoteWorkshopItem item)
    {
        if(item.PreviewArtwork.Length>0||!SteamService.IsSteamHttps(item.PreviewUrl))return;
        try {
            item.PreviewCachePath=await _steam.CacheWorkshopPreviewAsync(item,Settings.ImageCacheDays,CancellationToken.None);
            if(ReferenceEquals(SelectedRemoteItem,item))Raise(nameof(SelectedRemoteItem));
            await SaveAsync();
        } catch(Exception ex) when(ex is HttpRequestException or IOException or InvalidDataException or TaskCanceledException) {
            AppendLog("Vorschaubild nicht abrufbar: "+ex.Message);
        }
    }
    public WorkshopCollection? SelectedCollection {get;set;}
    public PublishJob? SelectedJob {get;set;}
    public SteamAccount? SelectedAccount {get;set;}
    public ReleaseChannel ReleaseTarget
    {
        get=>Settings.DefaultChannel;
        set { Settings.DefaultChannel=value;SettingsDraft.DefaultChannel=value;Raise();Raise(nameof(SettingsDraft)); }
    }
    public ReleaseChannel[] Channels=>Enum.GetValues<ReleaseChannel>();
    public ScheduleMode[] ScheduleModes=>Enum.GetValues<ScheduleMode>();
    public string[] Themes=>["Dark","Light"];
    public string[] UpdateChannels=>["stable","preview"];
    public ICommand ActionCommand {get;}
    public ICommand NavigateCommand {get;}
    public ICommand EditAppCommand {get;}
    public ICommand OpenAppCommand {get;}
    public ICommand RefreshAppCommand {get;}
    public ICommand EditModCommand {get;}
    public ICommand QueueModCommand {get;}
    public ICommand OpenModCommand {get;}

    public MainViewModel(WorkspaceStore store,Workspace data)
    {
        Store=store;Data=data;_steam=new(store);_builder=new(store);
        if(string.IsNullOrWhiteSpace(Settings.SteamCmdPath))Settings.SteamCmdPath=Path.Combine(Store.Root,"steamcmd","steamcmd.exe");
        SettingsDraft=WorkspaceStore.Clone(Settings);
        ActionCommand=new StudioCommand(p=>ActAsync(p?.ToString()??""),OnError);
        NavigateCommand=new StudioCommand(p=>{CurrentPage=p?.ToString()??"dashboard";return Task.CompletedTask;},OnError);
        EditAppCommand=new StudioCommand(p=>EditAppAsync(p as AppProfile),OnError);
        OpenAppCommand=new StudioCommand(p=>{if(p is AppProfile a)OpenWeb($"https://steamcommunity.com/app/{a.AppId}/workshop/");return Task.CompletedTask;},OnError);
        RefreshAppCommand=new StudioCommand(async p=>{if(p is AppProfile a)await RefreshSingleAppAsync(a);},OnError);
        EditModCommand=new StudioCommand(p=>EditModAsync(p as ModProject),OnError);
        QueueModCommand=new StudioCommand(p=>EnqueueAsync(p is ModProject m?[m]:SelectedMod is {} s?[s]:[],false),OnError);
        OpenModCommand=new StudioCommand(p=>{OpenMod(p as ModProject??SelectedMod);return Task.CompletedTask;},OnError);
        _timer=new DispatcherTimer{Interval=TimeSpan.FromSeconds(5)};_timer.Tick+=async(_,_)=>{Raise(nameof(DashboardDate));Raise(nameof(DashboardTime));await TickAsync();};
        Refresh();
    }
    public async Task InitializeAsync()
    {
        IsBusy=true;_operation=new();var cancelled=false;
        try {
            ThemeService.Apply(Settings);await SaveAsync();
            if(Settings.RefreshMetadataOnStartup)
                foreach(var app in Data.Apps.ToArray()) {
                    if(_operation.IsCancellationRequested){cancelled=true;break;}
                    try { await _steam.RefreshAppAsync(app,Settings,_operation.Token);Refresh(); }
                    catch(OperationCanceledException) when(_operation.IsCancellationRequested) {cancelled=true;break;}
                    catch(Exception e) {AppendLog("Steam-Daten: "+e.Message);}
                }
            await SaveAsync();
        } finally {_operation?.Dispose();_operation=null;IsBusy=false;}
        if(!cancelled&&Settings.AutoLogin&&ActiveAccount is not null&&File.Exists(Settings.SteamCmdPath))
            try {await LoginAsync();} catch(Exception e) {LoginStatus="Erneute Anmeldung nötig";AppendLog(e.Message);}
        if(!cancelled&&Settings.CheckToolUpdatesOnStartup)
            try {await CheckUpdatesAsync();} catch(Exception e) {AppendLog("Update-Prüfung: "+e.Message);}
        ConfigureAutomation();_timer.Start();
    }
    private bool Matches(string text)=>string.IsNullOrWhiteSpace(Search)||text.Contains(Search,StringComparison.OrdinalIgnoreCase);
    private void OnError(Exception e){Status=e.Message;AppendLog("FEHLER · "+e.Message);DesktopDialogs.Error(e);}
    private void EnsureIdle(){if(IsBusy)throw new InvalidOperationException("Während eines Vorgangs ist diese Änderung gesperrt.");}
    private void AppendLog(string text)
    {
        if(string.IsNullOrWhiteSpace(text))return;
        LiveLog+=$"[{DateTime.Now:HH:mm:ss}] {text.Trim()}\n";
        if(LiveLog.Length>60000)LiveLog=LiveLog[^50000..];
    }
    public void Refresh()
    {
        foreach(var m in Data.Mods)m.AppName=Data.Apps.FirstOrDefault(a=>a.Id==m.AppProfileId)?.Name??"App fehlt";
        foreach(var c in Data.Collections)c.AppName=Data.Apps.FirstOrDefault(a=>a.Id==c.AppProfileId)?.Name??"App fehlt";
        foreach(var p in new[]{nameof(Apps),nameof(VisibleApps),nameof(VisibleMods),nameof(VisibleRemoteItems),nameof(WorkshopScanSummary),nameof(Collections),nameof(Accounts),nameof(Queue),nameof(History),nameof(RecentJobs),nameof(DashboardApps),nameof(PublishedCount),nameof(QueueSummary),nameof(HasNoJobs),nameof(AppCount),nameof(ModCount),nameof(CollectionCount),nameof(PendingCount),nameof(SuccessCount),nameof(FailedCount),nameof(SuccessRate),nameof(HasNoMods),nameof(SteamCmdStatus),nameof(ActiveAccountLabel),nameof(AutomationStatus),nameof(Settings),nameof(SelectedApp),nameof(SelectedMod),nameof(SelectedCollection),nameof(SelectedAccount),nameof(SelectedJob),nameof(ReleaseTarget)})Raise(p);
    }
    private async Task SaveAsync(){await Store.SaveAsync(Data);Refresh();}
    private T Required<T>(T? value,string what)where T:class=>value??throw new InvalidOperationException(what+" zuerst auswählen.");
    private async Task ActAsync(string action)
    {
        switch(action){
            case "new-app":await EditAppAsync(null);break;
            case "edit-app":await EditAppAsync(Required(SelectedApp,"App"));break;
            case "delete-app":await DeleteAppAsync();break;
            case "refresh-all":await RefreshMetadataAsync();break;
            case "new-mod":await EditModAsync(null);break;
            case "match-local-mods":await MatchLocalModsAsync();break;
            case "edit-mod":await EditModAsync(Required(SelectedMod,"Mod"));break;
            case "delete-mod":await DeleteModAsync();break;
            case "duplicate-mod":await DuplicateModAsync();break;
            case "import-vdf":await ImportVdfAsync();break;
            case "queue-mod":await EnqueueAsync([Required(SelectedMod,"Mod")],false);break;
            case "queue-all":await EnqueueAsync(VisibleMods.Where(m=>m.Enabled).ToArray(),false);break;
            case "build-mod":await BuildPreviewAsync(Required(SelectedMod,"Mod"));break;
            case "scan":await ScanAsync();break;
            case "scan-workshop":await ScanWorkshopAsync();break;
            case "import-workshop":await ImportWorkshopAsync();break;
            case "import-all-workshop":await ImportAllWorkshopAsync();break;
            case "workshop-details":await ShowWorkshopDetailsAsync();break;
            case "workshop-config":await ShowConfigAsync(Required(Data.Mods.FirstOrDefault(m=>m.Id==Required(SelectedRemoteItem,"Workshop-Eintrag").LocalModId),"Lokale Quelle"));break;
            case "mod-config":await ShowConfigAsync(Required(SelectedMod,"Mod"));break;
            case "open-mod":OpenMod(Required(SelectedMod,"Mod"));break;
            case "open-source":DesktopDialogs.OpenFolder(Required(SelectedMod,"Mod").ContentFolder);break;
            case "new-collection":await EditCollectionAsync(null);break;
            case "edit-collection":await EditCollectionAsync(Required(SelectedCollection,"Collection"));break;
            case "delete-collection":await DeleteCollectionAsync();break;
            case "import-collection":await ImportCollectionAsync();break;
            case "refresh-collection":await RefreshCollectionAsync();break;
            case "create-steam-collection":await CreateSteamCollectionAsync();break;
            case "open-collection":OpenCollection();break;
            case "queue-collection":var c=Required(SelectedCollection,"Collection");await EnqueueAsync(c.ModProjectIds.Select(id=>Data.Mods.Single(m=>m.Id==id)).ToArray(),false);break;
            case "export-collection":ExportCollection(false);break;
            case "export-zomboid":ExportCollection(true);break;
            case "copy-ids":Clipboard.SetText(string.Join(';',CollectionExporter.ResolveIds(Required(SelectedCollection,"Collection"),Data.Mods,Settings.DefaultChannel)));Status="Workshop-IDs kopiert.";break;
            case "run-queue":await RunQueueAsync();break;
            case "stop":_operation?.Cancel();Status="Abbruch angefordert. Ein bereits serverseitig verarbeiteter Upload lässt sich dadurch nicht zurücknehmen.";break;
            case "cancel-job":await CancelJobAsync();break;
            case "retry-job":await RetryJobAsync();break;
            case "open-log":OpenJobLog();break;
            case "help-wiki":DesktopDialogs.OpenUrl("https://github.com/ZUMBYTE-AppSolution/WorkshopStudio/wiki");break;
            case "open-job-backup":var backupPath=Required(SelectedJob,"Verlaufseintrag").PreUpdateBackupPath;if(!File.Exists(backupPath))throw new FileNotFoundException("Für diesen Vorgang ist keine Sicherung vor einem Update vorhanden. Ältere Sicherungen können gemäß Aufbewahrung entfernt worden sein.");DesktopDialogs.OpenFolder(Path.GetDirectoryName(backupPath)!);break;
            case "clear-history":EnsureIdle();if(DesktopDialogs.Confirm("Abgeschlossene Einträge aus dem Verlauf entfernen? Protokolldateien bleiben erhalten.")){Data.Jobs.RemoveAll(j=>j.Status is not JobStatus.Pending);await SaveAsync();}break;
            case "new-account":await EditAccountAsync(null);break;
            case "edit-account":await EditAccountAsync(Required(SelectedAccount,"Konto"));break;
            case "delete-account":await DeleteAccountAsync();break;
            case "activate-account":EnsureIdle();Settings.ActiveAccountId=Required(SelectedAccount,"Konto").Id;LoginStatus="Noch nicht geprüft";WebLoginStatus="Websitzung für dieses Konto noch nicht geprüft";await SaveAsync();break;
            case "login":await LoginAsync();break;
            case "interactive-login":await InteractiveLoginAsync();break;
            case "web-login":OpenWebLogin();break;
            case "copy-login-log":Clipboard.SetText(LoginLog);Status="Bereinigtes Login-Protokoll kopiert. Vor Weitergabe Kontometadaten prüfen.";break;
            case "save-login-log":SaveLoginLog();break;
            case "forget-password":EnsureIdle();var forgotten=Required(SelectedAccount??ActiveAccount,"Konto");CredentialVault.Delete(forgotten.Id);_sessionPasswords.Remove(forgotten.Id);forgotten.RememberPassword=false;await SaveAsync();Status="Gespeichertes Passwort entfernt. SteamCMD kann weiterhin einen eigenen Sitzungstoken besitzen.";break;
            case "clear-web-session":EnsureIdle();if(await BrowserWindow.ClearSessionAsync(Store.Root,ActiveAccount?.Id)){WebLoginStatus="Websitzung gelöscht";Status="Steam-Websitzung dieses Profils gelöscht.";}break;
            case "clear-cmd-session":ClearCmdSession();break;
            case "install-steamcmd":await InstallSteamCmdAsync();break;
            case "choose-steamcmd":EnsureIdle();var p=DesktopDialogs.OpenFile("SteamCMD|steamcmd.exe");if(p is not null){Settings.SteamCmdPath=p;SettingsDraft.SteamCmdPath=p;Raise(nameof(SettingsDraft));await SaveAsync();}break;
            case "save-settings":await SaveSettingsAsync();break;
            case "export-workspace":await ExportWorkspaceAsync();break;
            case "import-workspace":await ImportWorkspaceAsync();break;
            case "workspace-backup":await BackupWorkspaceAsync();break;
            case "open-data":DesktopDialogs.OpenFolder(Store.Root);break;
            case "open-backups":DesktopDialogs.OpenFolder(Store.BackupRoot);break;
            case "clear-cache":EnsureIdle();foreach(var f in Directory.GetFiles(Path.Combine(Store.Root,"cache")))File.Delete(f);foreach(var a in Data.Apps){a.HeaderCachePath="";a.IconCachePath="";}await SaveAsync();Status="Bild-Cache geleert. Steam-Daten erneut laden, um Bilder abzurufen.";break;
            case "clear-staging":ClearStaging();break;
            case "check-updates":await CheckUpdatesAsync();break;
            case "workshop-terms":DesktopDialogs.OpenUrl("https://steamcommunity.com/sharedfiles/workshoplegalagreement");break;
            case "help-docs":DesktopDialogs.OpenFolder(Path.Combine(AppContext.BaseDirectory,"docs"));break;
            case "diagnostics":ShowDiagnostics();break;
            default:throw new InvalidOperationException("Unbekannte Aktion: "+action);
        }
    }
    private async Task EditAppAsync(AppProfile? existing)
    {
        EnsureIdle();var model=existing is null?new AppProfile{AppId=0,Name="",Template=GameTemplate.Generic}:WorkspaceStore.Clone(existing);
        var dialog=new AppEditorWindow(model){Owner=DesktopDialogs.Owner};if(dialog.ShowDialog()!=true)return;
        if(model.AppId==0||string.IsNullOrWhiteSpace(model.Name))throw new InvalidDataException("AppID und Name werden benötigt.");
        if(Data.Apps.Any(a=>a.AppId==model.AppId&&a.Id!=model.Id))throw new InvalidDataException("Diese AppID ist bereits vorhanden.");
        if(existing is not null && existing.AppId!=model.AppId && Data.Mods.Any(m=>m.AppProfileId==existing.Id))throw new InvalidOperationException("Eine AppID mit zugeordneten Mods kann nicht geändert werden. Ein neues App-Profil anlegen.");
        if(existing is null)Data.Apps.Add(model);else Data.Apps[Data.Apps.IndexOf(existing)]=model;
        SelectedApp=model;await SaveAsync();
        try{await RefreshSingleAppAsync(model);}catch(Exception e){Status="App gespeichert. Steam-Daten nicht geladen: "+e.Message;}
    }
    private async Task DeleteAppAsync()
    {
        EnsureIdle();var app=Required(SelectedApp,"App");
        if(Data.Mods.Any(m=>m.AppProfileId==app.Id)||Data.Collections.Any(c=>c.AppProfileId==app.Id))throw new InvalidOperationException("Zuerst zugeordnete Mods und Collections entfernen.");
        if(!DesktopDialogs.Confirm($"App-Profil {app.Label} lokal entfernen?"))return;Data.Apps.Remove(app);SelectedApp=null;await SaveAsync();
    }
    private async Task RefreshSingleAppAsync(AppProfile app)
    {
        EnsureIdle();IsBusy=true;_operation=new();
        try {await _steam.RefreshAppAsync(app,Settings,_operation.Token);await SaveAsync();Status=app.Name+": Steam-Daten aktualisiert.";}
        finally {_operation?.Dispose();_operation=null;IsBusy=false;}
    }
    private async Task RefreshMetadataAsync()
    {
        EnsureIdle();IsBusy=true;_operation=new();
        try {
            foreach(var app in Data.Apps.ToArray()) {
                _operation.Token.ThrowIfCancellationRequested();Status="Steam-Daten: "+app.Name;
                try {await _steam.RefreshAppAsync(app,Settings,_operation.Token);}
                catch(OperationCanceledException) when(_operation.IsCancellationRequested) {throw;}
                catch(Exception e) {AppendLog(app.Name+": "+e.Message);}
                Refresh();
            }
            await SaveAsync();Status="Steam-Abfrage abgeschlossen. Details stehen im Protokoll.";
        } finally {_operation?.Dispose();_operation=null;IsBusy=false;}
    }
    private async Task EditModAsync(ModProject? existing)
    {
        EnsureIdle();if(Data.Apps.Count==0)throw new InvalidOperationException("Zuerst ein App-Profil anlegen.");
        var model=existing is null?new ModProject{AppProfileId=(SelectedApp??Data.Apps[0]).Id,Visibility=(SelectedApp??Data.Apps[0]).DefaultVisibility}:WorkspaceStore.Clone(existing);
        var dialog=new ModEditorWindow(model,Data.Apps,Store.Root){Owner=DesktopDialogs.Owner};if(dialog.ShowDialog()!=true)return;
        if(existing is not null && existing.AppProfileId!=model.AppProfileId && (existing.WorkshopId>0||existing.TestWorkshopId>0))throw new InvalidOperationException("Veröffentlichte Mods können nicht einer anderen AppID zugeordnet werden.");
        var duplicate=Data.Mods.Any(m=>m.Id!=model.Id && ((model.WorkshopId>0&&(m.WorkshopId==model.WorkshopId||m.TestWorkshopId==model.WorkshopId))||(model.TestWorkshopId>0&&(m.WorkshopId==model.TestWorkshopId||m.TestWorkshopId==model.TestWorkshopId))));
        if(duplicate || model.WorkshopId>0&&model.WorkshopId==model.TestWorkshopId)throw new InvalidDataException("Workshop-IDs dürfen nicht mehrfach oder zugleich für Test und Live verwendet werden.");
        if(existing is null)Data.Mods.Add(model);else Data.Mods[Data.Mods.IndexOf(existing)]=model;
        SelectedMod=model;await SaveAsync();ConfigureAutomation();Status="Mod gespeichert.";
    }
    private async Task MatchLocalModsAsync()
    {
        EnsureIdle();
        var selectedApp=SelectedMod is { } selected ? Data.Apps.FirstOrDefault(a=>a.Id==selected.AppProfileId) : null;
        var zomboidApps=Data.Apps.Where(ModSourceResolver.IsZomboid).ToArray();
        var app=selectedApp is not null && ModSourceResolver.IsZomboid(selectedApp) ? selectedApp :
            zomboidApps.Length==1 ? zomboidApps[0] :
            throw new InvalidOperationException("Für die Ordnersuche zuerst eine Mod des gewünschten Project-Zomboid-App-Profils auswählen.");
        var initial=Directory.Exists(app.SourceRoot) ? app.SourceRoot :
            SelectedMod is { ContentFolder.Length: > 0 } mod && Directory.Exists(mod.ContentFolder) ? mod.ContentFolder : "";
        var folder=DesktopDialogs.Folder("Stammordner mit lokalen Mods auswählen",initial);
        if(folder is null)return;
        if(PackageBuilder.IsWithin(folder,Store.Root)||PackageBuilder.IsWithin(Store.Root,folder))
            throw new InvalidDataException("Der Studio-Datenordner und seine übergeordneten Ordner sind keine Mod-Quelle.");
        IsBusy=true;LocalModCatalogResult catalog;
        try {Status="Lokale Mod-Unterordner werden gelesen …";catalog=await Task.Run(()=>LocalModCatalog.Discover(folder));}
        finally {IsBusy=false;}
        var profiles=Data.Mods.Where(m=>m.AppProfileId==app.Id).ToArray();
        var suggestions=LocalModCatalog.Suggest(catalog.Candidates,profiles,app.Id);
        var dialog=new LocalModMatchWindow(app,catalog,suggestions,profiles){Owner=DesktopDialogs.Owner};
        if(dialog.ShowDialog()!=true)return;
        var mappings=dialog.Assignments.Select(a=>new LocalModMappingSelection(a.Candidate,a.Target.ModId,a.Target.CreateNew)).ToArray();
        var result=LocalModMapper.Prepare(Data,app,mappings,Store.Root);
        if(File.Exists(Store.FilePath))
        {
            var backup=Path.Combine(Store.BackupRoot,"workspace-before-local-mod-match-"+
                DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss-fff")+"-"+Guid.NewGuid().ToString("N")+".json");
            File.Copy(Store.FilePath,backup);
        }
        await Store.SaveAsync(result.Workspace);
        Data.Mods=result.Workspace.Mods;
        SelectedMod=Data.Mods.FirstOrDefault(m=>m.Id==result.LastModId) ?? Data.Mods.FirstOrDefault(m=>m.Id==SelectedMod?.Id);
        Refresh();ConfigureAutomation();
        Status=$"Lokale Mods zugeordnet: {result.Updated} vorhandene Profile, {result.Created} neue Profile. Keine Veröffentlichung; Auto-Publish für diese Zuordnungen aus.";
    }
    private async Task DeleteModAsync()
    {
        EnsureIdle();var m=Required(SelectedMod,"Mod");if(!DesktopDialogs.Confirm($"{m.Name} nur aus Workshop Studio entfernen? Quelldateien und Steam-Einträge bleiben unverändert."))return;
        Data.Mods.Remove(m);foreach(var r in Data.RemoteItems.Where(r=>r.LocalModId==m.Id))r.LocalModId=null;foreach(var c in Data.Collections)c.ModProjectIds.Remove(m.Id);Data.Jobs.RemoveAll(j=>j.ModId==m.Id&&j.Status==JobStatus.Pending);SelectedMod=null;await SaveAsync();ConfigureAutomation();
    }
    private async Task DuplicateModAsync()
    {
        EnsureIdle();var m=WorkspaceStore.Clone(Required(SelectedMod,"Mod"));m.Id=Guid.NewGuid();m.Name+=" Kopie";m.WorkshopId=0;m.TestWorkshopId=0;m.ImportedFromWorkshop=false;m.OwnerSteamId64=0;m.RemoteBaselineUpdatedAt=null;m.LastLiveFingerprint="";m.LastTestFingerprint="";m.LastPublishedAt=null;m.AutoPublish=false;m.NeedsLiveReconciliation=false;m.NeedsTestReconciliation=false;
        m.LastLiveNoteHash=m.LastTestNoteHash=m.LastLivePreviewHash=m.LastTestPreviewHash="";m.LiveMetadataBaseline=m.TestMetadataBaseline=null;
        // The source is deliberately shared; the editor shows the original path for an explicit change.
        var d=new ModEditorWindow(m,Data.Apps,Store.Root){Owner=DesktopDialogs.Owner};if(d.ShowDialog()!=true)return;Data.Mods.Add(m);SelectedMod=m;await SaveAsync();ConfigureAutomation();
    }
    private async Task ImportVdfAsync()
    {
        EnsureIdle();var path=DesktopDialogs.OpenFile("Steam-Workshop-VDF|*.vdf");if(path is null)return;
        var v=VdfWriter.ReadFlat(await File.ReadAllTextAsync(path));
        if(!uint.TryParse(v.GetValueOrDefault("appid"),out var appid)||appid==0)throw new InvalidDataException("VDF enthält keine gültige AppID.");
        var app=Data.Apps.FirstOrDefault(a=>a.AppId==appid);
        if(app is null){app=new AppProfile{AppId=appid,Name="Steam App "+appid,Template=appid==108600?GameTemplate.ProjectZomboid42:GameTemplate.Generic};Data.Apps.Add(app);}
        string Resolve(string key){var raw=v.GetValueOrDefault(key,"");return string.IsNullOrWhiteSpace(raw)?"":Path.GetFullPath(raw,Path.GetDirectoryName(path)!);}
        var m=new ModProject{AppProfileId=app.Id,Name=v.GetValueOrDefault("title","Importierte Mod"),Description=v.GetValueOrDefault("description",""),ContentFolder=Resolve("contentfolder"),PreviewFile=Resolve("previewfile"),ChangeNote=v.GetValueOrDefault("changenote","Update {version}"),WorkshopId=VdfWriter.ReadPublishedId(await File.ReadAllTextAsync(path))};
        if(int.TryParse(v.GetValueOrDefault("visibility"),out var visibility))m.Visibility=visibility;
        if(m.WorkshopId>0&&Data.Mods.Any(x=>x.WorkshopId==m.WorkshopId||x.TestWorkshopId==m.WorkshopId))throw new InvalidDataException("Workshop-ID ist bereits in einem Mod-Profil vorhanden.");
        var d=new ModEditorWindow(m,Data.Apps,Store.Root){Owner=DesktopDialogs.Owner};if(d.ShowDialog()==true){Data.Mods.Add(m);await SaveAsync();ConfigureAutomation();}
    }
    private void OpenMod(ModProject? model)
    {
        var m=Required(model,"Mod");var id=m.GetWorkshopId(Settings.DefaultChannel);if(id==0)throw new InvalidOperationException("Für den ausgewählten Kanal existiert noch keine Workshop-ID.");OpenWeb($"https://steamcommunity.com/sharedfiles/filedetails/?id={id}");
    }
    private async Task BuildPreviewAsync(ModProject mod)
    {
        EnsureIdle();IsBusy=true;_operation=new();
        try{
            var app=Required(Data.Apps.FirstOrDefault(a=>a.Id==mod.AppProfileId),"App");
            var snapshot=await Task.Run(()=>_builder.BuildAsync(WorkspaceStore.Clone(app),WorkspaceStore.Clone(mod),Settings.DefaultChannel,WorkspaceStore.Clone(Settings),Guid.NewGuid(),new Progress<string>(s=>Application.Current.Dispatcher.Invoke(()=>Status=s)),_operation.Token));
            Status=$"Paket geprüft: {snapshot.Files.Count:N0} Dateien · {Format.Bytes(snapshot.Bytes)}. Kein Upload.";
            new PackagePreviewWindow(snapshot){Owner=DesktopDialogs.Owner}.ShowDialog();
        }finally{_operation?.Dispose();_operation=null;IsBusy=false;}
    }
    private async Task ScanAsync()
    {
        EnsureIdle();IsBusy=true;_operation=new();
        try{foreach(var m in VisibleMods.ToArray()){
            try{
                var app=Data.Apps.Single(a=>a.Id==m.AppProfileId);var token=_operation.Token;
                var cached=Data.RemoteItems.FirstOrDefault(r=>r.Id==m.GetWorkshopId(Settings.DefaultChannel));
                var schema=await Task.Run(()=>LocalWorkshopSchemaReader.Inspect(app,m),token);
                var effective=WorkshopMetadataPlanner.ResolveLocal(m,schema,cached,Settings.AutoDetectMetadata,Settings.DefaultChannel);
                var snapshot=await Task.Run(()=>_builder.BuildAsync(app,effective,Settings.DefaultChannel,WorkspaceStore.Clone(Settings),Guid.NewGuid(),null,token),token);
                var metadata=cached is null?null:WorkshopMetadataPlanner.Compare(effective,Settings.DefaultChannel,cached,
                    await WorkshopMetadataPlanner.HashFileAsync(effective.PreviewFile,token),
                    await WorkshopMetadataPlanner.HashFileAsync(File.Exists(cached.PreviewCachePath)?cached.PreviewCachePath:"",token),
                    cached.RemoteChangelogStatus!="Nicht angefragt"&&!cached.RemoteChangelogStatus.StartsWith("Nicht abrufbar",StringComparison.Ordinal));
                m.ChangeState=metadata?.Fields.Count>0?"Metadaten geändert":snapshot.Fingerprint==m.GetFingerprint(Settings.DefaultChannel)?"Unverändert":"Änderungen vorhanden";
                _builder.DeleteOwnedStage(snapshot.Root);
            }
            catch(OperationCanceledException){throw;}catch(Exception e){m.ChangeState="Prüfung fehlgeschlagen";AppendLog(m.Name+": "+e.Message);}Refresh();
        }Status="Änderungsprüfung abgeschlossen.";}finally{_operation?.Dispose();_operation=null;IsBusy=false;}
    }
    private async Task ScanWorkshopAsync()
    {
        EnsureIdle();
        var account=Required(ActiveAccount,"Aktives Steam-Konto");
        var browser=new BrowserWindow(Store.Root,Settings.RememberWebSession,
            "https://steamcommunity.com/myworkshopfiles/?p=1&numperpage=30&browsesort=myfiles&browsefilter=myfiles",
            "Meine Workshop-Inhalte · Steam-Websitzung",false,accountId:account.Id,scanAccount:account,scanService:_steam,
            scanTimeoutSeconds:Settings.WorkshopScanTimeoutSeconds){Owner=DesktopDialogs.Owner};
        if(browser.ShowDialog()!=true || browser.ScanResult is not { } result) {
            if(browser.LastError.Length>0)Status=browser.LastError;
            return;
        }
        foreach(var item in result.Items) {
            var old=Data.RemoteItems.FirstOrDefault(x=>x.Id==item.Id&&x.OwnerSteamId64==item.OwnerSteamId64);
            if(old is null) Data.RemoteItems.Add(item);
            else {
                item.LocalModId=old.LocalModId;
                if(item.PreviewUrl==old.PreviewUrl)item.PreviewCachePath=old.PreviewCachePath;
                if(item.UpdatedAt==old.UpdatedAt){item.RemoteChangelog=old.RemoteChangelog;item.RemoteChangelogStatus=old.RemoteChangelogStatus;item.RemoteChangeNotes=old.RemoteChangeNotes;}
                if(item.FieldStatus.GetValueOrDefault("details") is RemoteFieldStatus.Error or RemoteFieldStatus.AccessDenied ||
                   item.FieldStatus.GetValueOrDefault("owner") != RemoteFieldStatus.Known) {
                    old.FetchedAt=item.FetchedAt; continue;
                }
                Data.RemoteItems[Data.RemoteItems.IndexOf(old)]=item;
            }
        }
        Data.LastWorkshopScan=result.Report;
        await SaveAsync();
        Status=result.Report.Summary;
    }
    private async Task ImportWorkshopAsync()
    {
        EnsureIdle();
        var item=Required(SelectedRemoteItem,"Workshop-Eintrag");
        var account=Required(ActiveAccount,"Aktives Steam-Konto");
        if(account.VerifiedWebSteamId64==0||account.VerifiedWebSteamId64!=item.OwnerSteamId64)
            throw new InvalidOperationException("Web-Konto und Eigentümer stimmen nicht überein.");
        if(item.FieldStatus.GetValueOrDefault("owner")!=RemoteFieldStatus.Known)
            throw new InvalidOperationException("Steam konnte den Eigentümer dieses Eintrags nicht per Detailabfrage bestätigen. Der Eintrag bleibt lesbar, ein Upload ist gesperrt.");
        if(item.ConsumerAppId==0)throw new InvalidDataException("AppID dieses Eintrags fehlt.");
        var app=Data.Apps.FirstOrDefault(a=>a.AppId==item.ConsumerAppId);
        if(app is null) {
            app=new AppProfile{AppId=item.ConsumerAppId,Name="Steam App "+item.ConsumerAppId,
                Template=item.ConsumerAppId==108600?GameTemplate.ProjectZomboid42:GameTemplate.Generic};
            Data.Apps.Add(app);
        }
        if(item.FileType==2) {
            if(Data.Collections.Any(c=>c.SteamCollectionId==item.Id)) {Status="Collection bereits verknüpft.";return;}
            var children=await _steam.GetCollectionAsync(item.Id,CancellationToken.None);
            Data.Collections.Add(new WorkshopCollection{AppProfileId=app.Id,SteamCollectionId=item.Id,Name=item.Title,
                Description=item.Description,RemoteWorkshopIds=children,LastSyncedAt=DateTimeOffset.UtcNow});
            await SaveAsync();Status="Collection lokal verknüpft. Auf Steam unverändert.";return;
        }
        if(item.FileType!=0)throw new InvalidOperationException("Dieser Inhaltstyp wird angezeigt, aber nicht als Mod importiert.");
        var existing=Data.Mods.FirstOrDefault(m=>m.WorkshopId==item.Id||m.TestWorkshopId==item.Id);
        if(existing is not null) {item.LocalModId=existing.Id;SelectedMod=existing;if(existing.WorkshopId==item.Id)existing.RemoteBaselineUpdatedAt=item.UpdatedAt;WorkshopMetadataPlanner.RememberBaseline(existing,existing.WorkshopId==item.Id?ReleaseChannel.Live:ReleaseChannel.Test,item,true);await SaveAsync();Status="Workshop-ID zugeordnet; zuletzt gelesener Steam-Stand als Baseline übernommen. Lokale Editoränderungen erhalten.";return;}
        var mod=new ModProject{AppProfileId=app.Id,Name=item.Title,Version="",Description=item.Description,
            PreviewFile="",Visibility=item.Visibility??2,WorkshopId=item.Id,OwnerSteamId64=item.OwnerSteamId64,
            ImportedFromWorkshop=true,RemoteBaselineUpdatedAt=item.UpdatedAt,ContentFolder="",AutoPublish=false,Enabled=true,
            ChangeNote=""};
        WorkshopMetadataPlanner.RememberBaseline(mod,ReleaseChannel.Live,item);Data.Mods.Add(mod);item.LocalModId=mod.Id;SelectedMod=mod;
        await SaveAsync();Status="Vorhandene Workshop-ID lokal importiert. Für Inhaltsupdates eine einzelne lokale Mod zuordnen.";
    }
    private async Task ImportAllWorkshopAsync()
    {
        EnsureIdle();var account=Required(ActiveAccount,"Aktives Steam-Konto");
        if(account.VerifiedWebSteamId64==0||account.VerifiedUploadSteamId64!=account.VerifiedWebSteamId64)
            throw new InvalidOperationException("Vor dem Gesamtimport Web- und SteamCMD-Konto anmelden und abgleichen.");
        var report=Data.LastWorkshopScan??throw new InvalidOperationException("Zuerst die eigenen Workshop-Inhalte ermitteln.");
        var items=Data.RemoteItems.Where(i=>i.OwnerSteamId64==account.VerifiedWebSteamId64).ToArray();
        if(!DesktopDialogs.Confirm($"{items.Length} eigene Einträge lokal zuordnen? Neue Mod-Profile erhalten keine Quellordner und veröffentlichen nichts automatisch."))return;
        var summary=WorkshopImporter.Merge(Data,account,new PortfolioResult(items,report),account.VerifiedUploadSteamId64);
        await SaveAsync();Refresh();
        Status=$"Importiert: {summary.AddedMods} Mods, {summary.AddedCollections} Collections; {summary.ReadOnly} nur lesbar.";
    }
    private async Task ShowWorkshopDetailsAsync()
    {
        var item=Required(SelectedRemoteItem,"Workshop-Eintrag");
        if(item.RemoteChangelogStatus=="Nicht angefragt") {
            try {
                var notes=await _steam.GetChangeNotesAsync(item.Id,CancellationToken.None);
                item.RemoteChangelog=notes.Text;item.RemoteChangelogStatus=notes.Status;item.RemoteChangeNotes=notes.Notes.ToList();
                item.FieldStatus["changelog"]=notes.Text.Length==0?RemoteFieldStatus.NotProvided:RemoteFieldStatus.Known;
                await SaveAsync();
            } catch(Exception ex) when(ex is HttpRequestException or IOException or InvalidDataException or TaskCanceledException) {
                item.RemoteChangelogStatus="Nicht abrufbar: "+ex.Message;
                item.FieldStatus["changelog"]=RemoteFieldStatus.Error;
            }
        }
        var text=$"Workshop-ID: {item.Id}\nBesitzer: {item.OwnerSteamId64}\nAppID: {item.ConsumerAppId}\nTyp: {item.TypeLabel}\nSichtbarkeit: {(item.Visibility?.ToString()??"Nicht ermittelbar")}\n" +
            $"Version: {item.VersionLabel}\nChangelog: {item.RemoteChangelogStatus}\nVorschau: {item.PreviewUrl}\n" +
            $"Erstellt: {item.CreatedAt?.ToLocalTime().ToString()??"Nicht ermittelbar"}\nAktualisiert: {item.UpdatedAt?.ToLocalTime().ToString()??"Nicht ermittelbar"}\n" +
            $"Quelle: {item.MetadataSource}\n\nTags: {string.Join(", ",item.Tags)}\n\nBESCHREIBUNG (Originalformat)\n{item.Description}"+
            $"\n\nÄNDERUNGSNOTIZEN (erste Steam-Seite)\n{item.RemoteChangelog}\n\nVollständiger Verlauf: https://steamcommunity.com/sharedfiles/filedetails/changelog/{item.Id}";
        new TextViewerWindow("Workshop-Details · "+item.Title,text){Owner=DesktopDialogs.Owner}.ShowDialog();
    }
    private async Task ShowConfigAsync(ModProject mod)
    {
        var app=Data.Apps.Single(a=>a.Id==mod.AppProfileId);
        var result=await Task.Run(()=>ConfigDiscovery.FindAsync(app,mod,Settings.ConfigMaxKiB,CancellationToken.None));
        new ConfigWindow(mod.Name,result){Owner=DesktopDialogs.Owner}.ShowDialog();
    }
    private async Task EditCollectionAsync(WorkshopCollection? existing)
    {
        EnsureIdle();if(Data.Apps.Count==0)throw new InvalidOperationException("Zuerst eine App anlegen.");
        var model=existing is null?new WorkshopCollection{AppProfileId=(SelectedApp??Data.Apps[0]).Id}:WorkspaceStore.Clone(existing);
        var dialog=new CollectionEditorWindow(model,Data.Apps,Data.Mods){Owner=DesktopDialogs.Owner};if(dialog.ShowDialog()!=true)return;
        if(existing is null)Data.Collections.Add(model);else Data.Collections[Data.Collections.IndexOf(existing)]=model;
        SelectedCollection=model;await SaveAsync();Status="Collection lokal gespeichert. Steam-Mitgliedschaften werden nicht stillschweigend verändert.";
    }
    private async Task DeleteCollectionAsync()
    {
        EnsureIdle();var c=Required(SelectedCollection,"Collection");if(!DesktopDialogs.Confirm($"Collection {c.Name} lokal entfernen? Die Steam-Collection bleibt bestehen."))return;Data.Collections.Remove(c);SelectedCollection=null;await SaveAsync();
    }
    private async Task ImportCollectionAsync()
    {
        EnsureIdle();var prompt=new InputWindow("Steam-Collection importieren","Collection-ID oder Steam-Link",false){Owner=DesktopDialogs.Owner};if(prompt.ShowDialog()!=true)return;
        var id=ParseWorkshopId(prompt.Value);
        // GetCollectionDetails is authoritative; public file details may omit file_type.
        var ids=await _steam.GetCollectionAsync(id,CancellationToken.None);
        var details=await _steam.GetDetailsAsync(id,CancellationToken.None);
        var app=Data.Apps.FirstOrDefault(a=>a.AppId==details.ConsumerAppId);
        if(app is null){app=new AppProfile{AppId=details.ConsumerAppId,Name="Steam App "+details.ConsumerAppId,Template=details.ConsumerAppId==108600?GameTemplate.ProjectZomboid42:GameTemplate.Generic};Data.Apps.Add(app);}
        var collection=new WorkshopCollection{AppProfileId=app.Id,SteamCollectionId=id,Name=details.Title,Description=details.Description,RemoteWorkshopIds=ids,LastSyncedAt=DateTimeOffset.UtcNow};
        foreach(var child in ids){var own=Data.Mods.FirstOrDefault(m=>m.AppProfileId==app.Id&&(m.WorkshopId==child||m.TestWorkshopId==child));if(own is null)collection.ExternalWorkshopIds.Add(child);else collection.ModProjectIds.Add(own.Id);}
        if(Data.Collections.Any(c=>c.SteamCollectionId==id))throw new InvalidOperationException("Diese Steam-Collection ist bereits verknüpft.");
        Data.Collections.Add(collection);SelectedCollection=collection;await SaveAsync();
    }
    private async Task RefreshCollectionAsync()
    {
        EnsureIdle();var c=Required(SelectedCollection,"Collection");if(c.SteamCollectionId==0)throw new InvalidOperationException("Zuerst eine Steam-Collection verknüpfen.");
        c.RemoteWorkshopIds=await _steam.GetCollectionAsync(c.SteamCollectionId,CancellationToken.None);c.LastSyncedAt=DateTimeOffset.UtcNow;await SaveAsync();
        Status=$"Steam-Stand gelesen: {c.RemoteWorkshopIds.Count} Einträge. Lokale Auswahl wurde nicht überschrieben.";
        new TextViewerWindow("Lokaler Stand / Steam-Stand","Lokale Mod-Projekte: "+c.ModProjectIds.Count+"\nLokale externe IDs: "+string.Join(';',c.ExternalWorkshopIds)+"\n\nAuf Steam: "+string.Join(';',c.RemoteWorkshopIds)+"\n\nÄnderungen an der echten Steam-Collection im Steam-Editor vornehmen."){Owner=DesktopDialogs.Owner}.ShowDialog();
    }
    private async Task CreateSteamCollectionAsync()
    {
        EnsureIdle(); var c=Required(SelectedCollection,"Collection"); var app=Data.Apps.Single(a=>a.Id==c.AppProfileId);
        var browser=new BrowserWindow(Store.Root,Settings.RememberWebSession,$"https://steamcommunity.com/sharedfiles/editcollection/?appid={app.AppId}","Steam-Collection anlegen",true,accountId:ActiveAccount?.Id){Owner=DesktopDialogs.Owner};
        if(browser.ShowDialog()!=true || browser.SelectedWorkshopId==0)return;
        var id=browser.SelectedWorkshopId;
        if(Data.Collections.Any(x=>x.Id!=c.Id&&x.SteamCollectionId==id))throw new InvalidOperationException("Diese Steam-Collection ist bereits verknüpft.");
        var children=await _steam.GetCollectionAsync(id,CancellationToken.None);
        var details=await _steam.GetDetailsAsync(id,CancellationToken.None);
        if(details.ConsumerAppId!=app.AppId)throw new InvalidDataException("Ausgewählte Steam-ID ist keine öffentlich überprüfbare Collection dieser App. Bisherige Verknüpfung bleibt unverändert.");
        c.SteamCollectionId=id;c.RemoteWorkshopIds=children;c.LastSyncedAt=DateTimeOffset.UtcNow;await SaveAsync();Status="Steam-Collection verknüpft.";
    }
    private void OpenCollection()
    {
        var c=Required(SelectedCollection,"Collection");if(c.SteamCollectionId==0)throw new InvalidOperationException("Noch keine Steam-Collection verknüpft.");OpenWeb($"https://steamcommunity.com/sharedfiles/filedetails/?id={c.SteamCollectionId}");
    }
    private void ExportCollection(bool zomboid)
    {
        var c=Required(SelectedCollection,"Collection");var app=Data.Apps.Single(a=>a.Id==c.AppProfileId);
        if(zomboid&&app.AppId!=108600)throw new InvalidOperationException("Server-INI-Export ist nur für Project Zomboid vorgesehen.");
        var text=zomboid?CollectionExporter.ZomboidIni(c,Data.Mods,Settings.DefaultChannel):JsonSerializer.Serialize(new{app.AppId,c.Name,c.SteamCollectionId,channel=Settings.DefaultChannel.ToString(),workshopItems=CollectionExporter.ResolveIds(c,Data.Mods,Settings.DefaultChannel)},WorkspaceStore.JsonOptions);
        var path=DesktopDialogs.SaveFile(zomboid?"server-mods.ini":"collection.json",zomboid?"INI-Ausschnitt|*.ini":"JSON|*.json");if(path is null)return;File.WriteAllText(path,text);Status="Collection exportiert.";
    }
    public static ulong ParseWorkshopId(string input)
    {
        if(ulong.TryParse(input.Trim(),out var id)&&id>0)return id;
        if(Uri.TryCreate(input.Trim(),UriKind.Absolute,out var uri)&&uri.Scheme=="https"&&uri.Host.Equals("steamcommunity.com",StringComparison.OrdinalIgnoreCase)){
            var m=Regex.Match(uri.Query,@"(?:^|[?&])id=(\d+)(?:&|$)");if(m.Success&&ulong.TryParse(m.Groups[1].Value,out id)&&id>0)return id;
        }
        throw new InvalidDataException("Ungültige Workshop-ID oder Steam-Community-Adresse.");
    }
    private async Task EnqueueAsync(IEnumerable<ModProject> mods,bool automatic)
    {
        var list=mods.Where(m=>m.Enabled).DistinctBy(m=>m.Id).ToArray();if(list.Length==0)throw new InvalidOperationException("Keine aktivierten Mods ausgewählt.");
        var channel=automatic?Settings.AutomationChannel:Settings.DefaultChannel;
        if(channel==ReleaseChannel.Live&&automatic&&!Settings.AllowLiveAutomation)throw new InvalidOperationException("Live-Automation ist gesperrt.");
        if(!automatic&&!DesktopDialogs.Confirm($"{list.Length} Mod(s) für {channel} einreihen?\n\nDie Veröffentlichung startet erst mit „Warteschlange starten“. Für Test werden separate, private Workshop-Einträge verwendet."))return;
        foreach(var m in list){if(m.NeedsReconciliation(channel)){if(automatic){AppendLog(m.Name+": automatische Wiederholung wegen ungeklärtem Uploadstatus gesperrt.");continue;}throw new InvalidOperationException(m.Name+": Voriger Erst-Upload ist ungeklärt. Im Mod-Editor die existierende Steam-ID eintragen oder nach manueller Prüfung die Sperre zurücksetzen.");}
            if(Data.Jobs.Any(j=>j.ModId==m.Id&&j.Channel==channel&&(j.Status is JobStatus.Pending or JobStatus.Preparing or JobStatus.Uploading)))continue;
            Data.Jobs.Add(new PublishJob{ModId=m.Id,ModName=m.Name,AppId=Data.Apps.Single(a=>a.Id==m.AppProfileId).AppId,Channel=channel,Automatic=automatic});}
        await SaveAsync();if(!automatic){CurrentPage="queue";Status="In Warteschlange aufgenommen. Noch nicht veröffentlicht.";}
    }
    private async Task RunQueueAsync(bool automaticOnly=false)
    {
        EnsureIdle();var account=Required(ActiveAccount,"Aktives Steam-Konto");
        if(!File.Exists(Settings.SteamCmdPath))throw new FileNotFoundException("SteamCMD ist noch nicht installiert.");
        if(!Data.Jobs.Any(j=>j.Status==JobStatus.Pending&&(!automaticOnly||j.Automatic)))return;
        IsBusy=true;_operation=new();var token=_operation.Token;
        try{
            if(account.VerifiedWebSteamId64==0)
                throw new InvalidOperationException("Vor Veröffentlichungen das aktive Webkonto unter 'Meine Workshop-Inhalte' per SteamID64 prüfen.");
            var identity=await _runner.RunAsync(Settings.SteamCmdPath,account.UserName,null,
                TimeSpan.FromMinutes(Settings.AuthenticationTimeoutMinutes),CreateSecretProvider(account),
                new Progress<string>(AppendLoginLog),token);
            if(identity.ExitCode!=0||!identity.LoggedIn||identity.SteamId64 is null)
                throw new InvalidOperationException("SteamCMD-Anmeldung und Upload-SteamID konnten nicht bestätigt werden.");
            account.VerifiedUploadSteamId64=identity.SteamId64.Value;
            account.UploadIdentityCheckedAt=DateTimeOffset.UtcNow;
            await SaveAsync();
            if(account.VerifiedUploadSteamId64!=account.VerifiedWebSteamId64)
                throw new InvalidOperationException("SteamCMD-Konto und Steam-Webkonto besitzen verschiedene SteamIDs. Upload abgebrochen.");
            while(!token.IsCancellationRequested){
                var job=Data.Jobs.FirstOrDefault(j=>j.Status==JobStatus.Pending&&(!automaticOnly||j.Automatic));if(job is null)break;
                if(automaticOnly&&(!Settings.AutomationEnabled || job.Channel==ReleaseChannel.Live&&!Settings.AllowLiveAutomation))break;
                var source=Data.Mods.FirstOrDefault(m=>m.Id==job.ModId);if(source is null){job.Status=JobStatus.Failed;job.Message="Mod-Profil fehlt.";await SaveAsync();continue;}
                var m=WorkspaceStore.Clone(source);var app=WorkspaceStore.Clone(Data.Apps.Single(a=>a.Id==m.AppProfileId));var settings=WorkspaceStore.Clone(Settings);
                PackageSnapshot? snapshot=null;bool uploadStarted=false;var originalId=m.GetWorkshopId(job.Channel);
                WorkshopMetadataPlan? metadata=null;IReadOnlyCollection<string>? writtenFields=null;
                job.StartedAt=DateTimeOffset.UtcNow;job.Status=JobStatus.Preparing;job.LogFile=Path.Combine(Store.LogRoot,job.Id.ToString("N")+".log");
                var progress=new Progress<string>(line=>{AppendLog(line);try{File.AppendAllText(job.LogFile,$"[{DateTime.Now:O}] {line}\n");}catch(IOException){}});
                try{
                    if(!m.Enabled)throw new InvalidOperationException("Mod wurde deaktiviert.");
                    if(m.NeedsReconciliation(job.Channel))throw new InvalidOperationException("Ungeklärter Erst-Upload. Vor erneutem Versuch Workshop-ID prüfen.");
                    await SaveAsync();Status="Metadaten prüfen: "+m.Name;
                    var cached=Data.RemoteItems.FirstOrDefault(r=>r.Id==originalId&&r.OwnerSteamId64==account.VerifiedWebSteamId64);
                    var schema=await Task.Run(()=>LocalWorkshopSchemaReader.Inspect(app,m),token);
                    m=WorkshopMetadataPlanner.ResolveLocal(m,schema,cached,settings.AutoDetectMetadata,job.Channel);
                    RemoteWorkshopItem? current=null;var remotePreview="";var historyRead=false;
                    if(originalId>0) {
                        if(account.VerifiedWebSteamId64==0 || m.OwnerSteamId64!=0&&m.OwnerSteamId64!=account.VerifiedWebSteamId64)
                            throw new InvalidOperationException("Bestehender Eintrag: verifizierte Web-SteamID64 fehlt oder stimmt nicht mit dem importierten Besitzer überein.");
                        if(cached is null)throw new InvalidOperationException("Ziel-ID im Bereich 'Meine Workshop-Inhalte' zuerst mit diesem Konto ermitteln.");
                        current=new RemoteWorkshopItem{Id=originalId,OwnerSteamId64=account.VerifiedWebSteamId64,FileType=cached.FileType};
                        await _steam.EnrichWorkshopItemsAsync([current],token);
                        if(current.FieldStatus.GetValueOrDefault("owner")!=RemoteFieldStatus.Known||current.ConsumerAppId!=app.AppId||current.FileType!=0)
                            throw new InvalidOperationException("Eigentümer, AppID oder Dateityp des Ziel-Eintrags konnte vor Upload nicht bestätigt werden.");
                        if(cached.UpdatedAt is null||current.UpdatedAt is null||cached.UpdatedAt!=current.UpdatedAt)
                            throw new InvalidOperationException("Remote-Stand seit dem letzten Scan geändert oder nicht überprüfbar. Bibliothek erneut ermitteln und Änderungen prüfen.");
                        if(job.Channel==ReleaseChannel.Live&&m.ImportedFromWorkshop&&m.RemoteBaselineUpdatedAt!=current.UpdatedAt)
                            throw new InvalidOperationException("Import-Baseline und aktueller Steam-Stand weichen ab. Konflikt zuerst prüfen.");
                        try {
                            var notes=await _steam.GetChangeNotesAsync(originalId,token);current.RemoteChangelog=notes.Text;
                            current.RemoteChangelogStatus=notes.Status;current.RemoteChangeNotes=notes.Notes.ToList();historyRead=true;
                        } catch(Exception ex) when(ex is HttpRequestException or IOException or InvalidDataException or TaskCanceledException) {
                            token.ThrowIfCancellationRequested();current.RemoteChangelogStatus="Nicht abrufbar: "+ex.Message;
                        }
                        if(current.PreviewUrl.Length>0)remotePreview=await _steam.CacheWorkshopPreviewAsync(current,0,token);
                        metadata=WorkshopMetadataPlanner.Compare(m,job.Channel,current,
                            await WorkshopMetadataPlanner.HashFileAsync(m.PreviewFile,token),
                            await WorkshopMetadataPlanner.HashFileAsync(remotePreview,token),historyRead);
                        job.MetadataSummary=metadata.Summary;AppendLog(metadata.Summary);
                    }
                    Status="Paketierung: "+m.Name;
                    snapshot=await Task.Run(()=>_builder.BuildAsync(app,m,job.Channel,settings,job.Id,progress,token),token);
                    job.StageFolder=snapshot.Root;job.PackageBytes=snapshot.Bytes;
                    if(settings.OnlyChanged&&originalId>0&&snapshot.Fingerprint==m.GetFingerprint(job.Channel)&&
                        (job.Automatic&&!settings.AutoDetectMetadata||metadata?.Fields.Count==0)){
                        job.Status=JobStatus.Skipped;job.Message="Inhalt und Upload-Metadaten sind unverändert.";
                    }else{
                        if(originalId>0 && current is not null && metadata is not null) {
                            IReadOnlyCollection<string> fields=settings.AutoDetectMetadata?metadata.Fields:[];
                            if(!job.Automatic) {
                                var plan=new UpdatePlanWindow(account,app,m,job.Channel,current,snapshot,metadata,settings.AutoDetectMetadata){Owner=DesktopDialogs.Owner};
                                if(plan.ShowDialog()!=true)throw new OperationCanceledException("Update-Plan nicht freigegeben.");
                                fields=plan.SelectedFields;
                            }
                            job.MetadataSummary+="\nFür diesen Upload: "+(fields.Count==0?"nur Inhalt":string.Join(", ",fields));AppendLog(job.MetadataSummary);
                            writtenFields=fields;
                            var preview=string.IsNullOrWhiteSpace(m.PreviewFile)?"":Path.Combine(snapshot.Root,"preview"+Path.GetExtension(m.PreviewFile));
                            await File.WriteAllTextAsync(snapshot.VdfPath,
                                VdfWriter.Build(app,m,job.Channel,snapshot.ContentPath,preview,fields),new UTF8Encoding(false),token);
                            Status="Bisherigen Workshop-Stand sichern: "+m.Name;
                            var download=await _runner.DownloadWorkshopItemAsync(settings.SteamCmdPath,account.UserName,app.AppId,originalId,
                                TimeSpan.FromMinutes(settings.UploadTimeoutMinutes),CreateSecretProvider(account),progress,token);
                            if(download.Command.SteamId64!=account.VerifiedWebSteamId64)throw new InvalidOperationException("Backup-Download hat das erwartete Steam-Konto nicht bestätigt.");
                            var backup=await Task.Run(()=>new WorkshopItemBackup(Store).CreateAsync(current,source,job.Channel,download.ContentPath,
                                remotePreview,settings,progress,token),token);
                            job.PreUpdateBackupPath=backup.Path;await SaveAsync();
                            var recheck=new RemoteWorkshopItem{Id=originalId,OwnerSteamId64=account.VerifiedWebSteamId64};
                            await _steam.EnrichWorkshopItemsAsync([recheck],token);
                            if(recheck.FieldStatus.GetValueOrDefault("owner")!=RemoteFieldStatus.Known||recheck.ConsumerAppId!=app.AppId||
                                recheck.UpdatedAt!=current.UpdatedAt||recheck.Title!=current.Title||recheck.Description!=current.Description||
                                recheck.PreviewUrl!=current.PreviewUrl||recheck.Visibility!=current.Visibility)
                                throw new InvalidOperationException("Steam wurde während der Sicherung geändert. Backup behalten; Update vor Upload gestoppt. Bibliothek erneut prüfen.");
                        }
                        if(originalId==0){source.SetReconciliation(job.Channel,true);await SaveAsync();}
                        job.Status=JobStatus.Uploading;job.Message="Wird an Steam übertragen";Status=$"{job.Channel} · {m.Name}: Upload";await SaveAsync();
                        uploadStarted=true;
                        var result=await _runner.RunAsync(settings.SteamCmdPath,account.UserName,snapshot.VdfPath,TimeSpan.FromMinutes(settings.UploadTimeoutMinutes),
                            CreateSecretProvider(account),progress,token);
                        var id=VdfWriter.ReadPublishedId(await File.ReadAllTextAsync(snapshot.VdfPath,token));
                        if(originalId>0&&id!=originalId)throw new InvalidDataException("SteamCMD meldet eine andere Workshop-ID als das bestätigte Update-Ziel. Lokale Zuordnung bleibt unverändert; Steam-Seite und Protokoll prüfen.");
                        if(id>0){source.SetWorkshopId(job.Channel,id);source.SetReconciliation(job.Channel,false);job.PublishedFileId=id;await SaveAsync();}
                        if(result.ExitCode!=0||!result.Published||id==0)throw new IOException("SteamCMD hat den Upload nicht eindeutig bestätigt. Protokoll und Steam-Seite prüfen. Exit-Code: "+result.ExitCode);
                        source.SetFingerprint(job.Channel,snapshot.Fingerprint);source.LastPublishedAt=DateTimeOffset.UtcNow;source.LastPackageBytes=snapshot.Bytes;source.ChangeState="Unverändert";
                        source.Version=m.Version;
                        if(writtenFields is null||writtenFields.Contains("title"))source.Name=m.Name;
                        if(writtenFields is null||writtenFields.Contains("description"))source.Description=m.Description;
                        source.SetMetadataHashes(job.Channel,writtenFields is null||writtenFields.Contains("changenote")?WorkshopMetadataPlanner.HashText(WorkshopMetadataPlanner.RenderNote(m)):"",
                            writtenFields is null||writtenFields.Contains("previewfile")?await WorkshopMetadataPlanner.HashFileAsync(m.PreviewFile,token):"");
                        job.Status=JobStatus.Succeeded;job.Message="Steam hat den Upload bestätigt.";LoginStatus="SteamCMD-Anmeldung bestätigt";
                        // Persist the remote ID and verified success before optional local post-processing.
                        await SaveAsync();
                        job.RemoteVerification=await VerifyAfterUploadAsync(id,account.VerifiedWebSteamId64,app.AppId,source,m,job.Channel,writtenFields,token);
                        job.Message+=" "+job.RemoteVerification;
                        await SaveAsync();
                        if(settings.CreatePackageBackups)try{await Task.Run(()=>_builder.Backup(snapshot,source,job.Channel,settings.BackupRetention),token);}catch(Exception e){job.Message+=" Backup fehlgeschlagen: "+e.Message;}
                        if(settings.OpenWorkshopAfterPublish&&!job.Automatic)DesktopDialogs.OpenUrl($"https://steamcommunity.com/sharedfiles/filedetails/?id={id}");
                    }
                }catch(OperationCanceledException){job.Status=JobStatus.Cancelled;job.Message="Abgebrochen; bei bereits gestartetem Upload den Steam-Stand prüfen.";}
                catch(Exception e){job.Status=JobStatus.Failed;job.Message=e.Message;((IProgress<string>)progress).Report("FEHLER: "+e.Message);}
                finally{
                    if(uploadStarted&&snapshot is not null&&File.Exists(snapshot.VdfPath)){
                        try{var recovered=VdfWriter.ReadPublishedId(await File.ReadAllTextAsync(snapshot.VdfPath));if(recovered>0&&(originalId==0||recovered==originalId)){source.SetWorkshopId(job.Channel,recovered);source.SetReconciliation(job.Channel,false);job.PublishedFileId=recovered;}}catch(IOException){}
                    }
                    job.FinishedAt=DateTimeOffset.UtcNow;
                    if(snapshot is not null&&!settings.KeepSuccessfulStaging&&(job.Status is JobStatus.Succeeded or JobStatus.Skipped))try{_builder.DeleteOwnedStage(snapshot.Root);job.StageFolder="";}catch(IOException e){AppendLog("Paketbereinigung: "+e.Message);}
                    TrimHistory();await SaveAsync();
                }
                if(job.Status==JobStatus.Failed&&Settings.StopQueueOnError)break;
            }
            Status=token.IsCancellationRequested?"Warteschlange angehalten.":"Durchlauf beendet. Ergebnisse im Verlauf prüfen.";
        }finally{_operation?.Dispose();_operation=null;IsBusy=false;Refresh();}
    }
    private async Task<string> VerifyAfterUploadAsync(ulong id,ulong owner,uint appId,ModProject source,ModProject uploaded,
        ReleaseChannel channel,IReadOnlyCollection<string>? fields,CancellationToken ct)
    {
        for(var attempt=0;attempt<3;attempt++) {
            try {
                if(attempt>0)await Task.Delay(TimeSpan.FromSeconds(2),ct);
                var remote=new RemoteWorkshopItem{Id=id,OwnerSteamId64=owner,FileType=0};
                await _steam.EnrichWorkshopItemsAsync([remote],ct);
                bool Included(string field)=>fields is null||fields.Contains(field);
                if(remote.FieldStatus.GetValueOrDefault("owner")==RemoteFieldStatus.Known&&remote.ConsumerAppId==appId&&remote.UpdatedAt is not null&&
                    (!Included("title")||remote.Title==uploaded.Name+(channel==ReleaseChannel.Test?uploaded.TestTitleSuffix:""))&&
                    (!Included("description")||WorkshopMetadataPlanner.Normalize(remote.Description)==WorkshopMetadataPlanner.Normalize(uploaded.Description))&&
                    (!Included("visibility")||remote.Visibility==(channel==ReleaseChannel.Test?2:uploaded.Visibility))) {
                    remote.LocalModId=source.Id;
                    var old=Data.RemoteItems.FirstOrDefault(r=>r.Id==id&&r.OwnerSteamId64==owner);
                    if(old is null)Data.RemoteItems.Add(remote);else Data.RemoteItems[Data.RemoteItems.IndexOf(old)]=remote;
                    source.OwnerSteamId64=owner;
                    WorkshopMetadataPlanner.RememberBaseline(source,channel,remote);
                    if(channel==ReleaseChannel.Live)source.RemoteBaselineUpdatedAt=remote.UpdatedAt;
                    return "Steam-Detailabfrage bestätigt ID, Besitzer, AppID und übertragene Textfelder; Baseline aktualisiert.";
                }
            } catch(OperationCanceledException) when(ct.IsCancellationRequested){return "Remote-Nachprüfung abgebrochen; Steam-Seite prüfen.";}
            catch(Exception ex) when(ex is HttpRequestException or IOException or InvalidDataException or TaskCanceledException) {AppendLog("Remote-Nachprüfung: "+ex.Message);}
        }
        return "Remote-Nachprüfung noch offen; Steam-Seite prüfen.";
    }
    private void TrimHistory()
    {
        var remove=Data.Jobs.Where(j=>j.Status is not (JobStatus.Pending or JobStatus.Preparing or JobStatus.Uploading)).OrderByDescending(j=>j.CreatedAt).Skip(Settings.HistoryRetention).ToArray();foreach(var j in remove)Data.Jobs.Remove(j);
    }
    private async Task CancelJobAsync(){var j=Required(SelectedJob,"Auftrag");if(j.Status==JobStatus.Pending){j.Status=JobStatus.Cancelled;j.Message="Vor Upload aus Warteschlange entfernt.";j.FinishedAt=DateTimeOffset.UtcNow;await SaveAsync();}else throw new InvalidOperationException("Nur wartende Aufträge können einzeln entfernt werden. Laufenden Vorgang mit Stoppen beenden.");}
    private async Task RetryJobAsync(){EnsureIdle();var j=Required(SelectedJob,"Verlaufseintrag");if(j.Status is JobStatus.Pending or JobStatus.Preparing or JobStatus.Uploading)throw new InvalidOperationException("Auftrag läuft bereits.");var m=Required(Data.Mods.FirstOrDefault(m=>m.Id==j.ModId),"Mod-Profil");if(m.NeedsReconciliation(j.Channel))throw new InvalidOperationException("Erst Workshop-ID im Mod-Editor prüfen; automatisches Duplizieren wird verhindert.");if(!DesktopDialogs.Confirm($"{m.Name} erneut für {j.Channel} einreihen?"))return;Data.Jobs.Add(new PublishJob{ModId=m.Id,ModName=m.Name,AppId=j.AppId,Channel=j.Channel});await SaveAsync();CurrentPage="queue";}
    private void OpenJobLog(){var j=Required(SelectedJob,"Auftrag");new TextViewerWindow("Upload-Protokoll · "+j.ModName,File.Exists(j.LogFile)?File.ReadAllText(j.LogFile):j.Message){Owner=DesktopDialogs.Owner}.ShowDialog();}
    private async Task EditAccountAsync(SteamAccount? existing)
    {
        EnsureIdle();var model=existing is null?new SteamAccount():WorkspaceStore.Clone(existing);var d=new AccountEditorWindow(model){Owner=DesktopDialogs.Owner};if(d.ShowDialog()!=true)return;SteamCmdRunner.ValidateUserName(model.UserName);
        if(Data.Accounts.Any(a=>a.Id!=model.Id&&a.UserName.Equals(model.UserName,StringComparison.OrdinalIgnoreCase)))throw new InvalidDataException("Dieses Steam-Konto existiert bereits.");
        if(existing is not null&&!existing.UserName.Equals(model.UserName,StringComparison.OrdinalIgnoreCase)){CredentialVault.Delete(model.Id);_sessionPasswords.Remove(model.Id);model.VerifiedWebSteamId64=0;model.WebIdentityCheckedAt=null;model.VerifiedUploadSteamId64=0;model.UploadIdentityCheckedAt=null;}
        if(!string.IsNullOrEmpty(d.Password))_sessionPasswords[model.Id]=d.Password;
        if(model.RememberPassword&&!string.IsNullOrEmpty(d.Password))CredentialVault.Save(model.Id,model.UserName,d.Password);else if(!model.RememberPassword)CredentialVault.Delete(model.Id);
        if(existing is null)Data.Accounts.Add(model);else Data.Accounts[Data.Accounts.IndexOf(existing)]=model;
        Settings.ActiveAccountId??=model.Id;SelectedAccount=model;LoginStatus="Noch nicht geprüft";await SaveAsync();
    }
    private async Task DeleteAccountAsync(){EnsureIdle();var a=Required(SelectedAccount,"Konto");if(!DesktopDialogs.Confirm("Konto und gespeichertes Passwort lokal entfernen? SteamCMD-eigene Sitzungstoken können separat unter Sitzung entfernen gelöscht werden."))return;CredentialVault.Delete(a.Id);_sessionPasswords.Remove(a.Id);Data.Accounts.Remove(a);if(Settings.ActiveAccountId==a.Id)Settings.ActiveAccountId=null;SelectedAccount=null;LoginStatus="Noch nicht geprüft";await SaveAsync();}
    private Func<SecretKind,CancellationToken,Task<string?>> CreateSecretProvider(SteamAccount account)
    {
        var storedPasswordUsed=false;
        return async(kind,ct)=>{
            var allowStored=!storedPasswordUsed;
            if(kind==SecretKind.Password)storedPasswordUsed=true;
            return await RequestSecretAsync(account,kind,ct,allowStored);
        };
    }
    private async Task<string?> RequestSecretAsync(SteamAccount account,SecretKind kind,CancellationToken ct,bool allowStoredPassword)
    {
        ct.ThrowIfCancellationRequested();
        if(kind==SecretKind.Password&&allowStoredPassword&&_sessionPasswords.TryGetValue(account.Id,out var current))return current;
        if(kind==SecretKind.Password&&account.RememberPassword&&allowStoredPassword){var saved=CredentialVault.Read(account.Id);if(!string.IsNullOrEmpty(saved))return saved;}
        return await Application.Current.Dispatcher.InvokeAsync(()=>{
            var dialog=new InputWindow(kind==SecretKind.Password?"SteamCMD-Anmeldung":"Steam Guard",kind==SecretKind.Password?$"Passwort für {account.UserName}":"Den von Steam angeforderten E-Mail- oder Authenticator-Code eingeben. Eine mobile Freigabe erfolgt direkt in Steam.",true){Owner=DesktopDialogs.Owner};
            using var registration=ct.Register(()=>Application.Current.Dispatcher.BeginInvoke(new Action(()=>{if(dialog.IsVisible)dialog.Close();})));
            if(ct.IsCancellationRequested)return null;
            if(dialog.ShowDialog()!=true)return null;
            if(kind==SecretKind.Password){_sessionPasswords[account.Id]=dialog.Value;if(account.RememberPassword)CredentialVault.Save(account.Id,account.UserName,dialog.Value);}
            return dialog.Value;
        });
    }
    private async Task<bool> EnsureLoginReadyAsync()
    {
        EnsureIdle();
        if (ActiveAccount is null)
        {
            if (SelectedAccount is { } selected) { Settings.ActiveAccountId=selected.Id; await SaveAsync(); }
            else if (Data.Accounts.Count == 1) { Settings.ActiveAccountId=Data.Accounts[0].Id; await SaveAsync(); }
            else
            {
                await EditAccountAsync(null);
                if (ActiveAccount is null) { Status="Anmeldung abgebrochen: kein Upload-Konto gewählt."; return false; }
            }
        }
        if (!File.Exists(Settings.SteamCmdPath)) await InstallSteamCmdAsync();
        if (!File.Exists(Settings.SteamCmdPath)) { LoginStatus="SteamCMD ist noch nicht installiert"; return false; }
        var folder=Path.GetDirectoryName(Path.GetFullPath(Settings.SteamCmdPath))!;
        try
        {
            using var probe=new FileStream(Path.Combine(folder,".studio-write-check-"+Guid.NewGuid().ToString("N")),FileMode.CreateNew,FileAccess.Write,FileShare.None,1,FileOptions.DeleteOnClose);
        }
        catch (UnauthorizedAccessException ex) { throw new IOException("Der SteamCMD-Ordner ist nicht beschreibbar. Unter Einstellungen eine Installation im eigenen Benutzerprofil auswählen.",ex); }
        return true;
    }
    private void AppendLoginLog(string message)
    {
        LoginLog += $"[{DateTime.Now:HH:mm:ss}] {message.Trim()}\n";
        if(LoginLog.Length>120000)LoginLog=LoginLog[^100000..];
        AppendLog(message);
    }
    private async Task LoginAsync()
    {
        if(!await EnsureLoginReadyAsync())return;
        var account=ActiveAccount!;
        IsBusy=true;_operation=new();
        LoginLog=$"Workshop Studio 1.3.0 · SteamCMD-Anmeldung\n.NET: {Environment.Version} · {Environment.OSVersion}\n";
        _lastLoginLogFile=Path.Combine(Store.LogRoot,"login-"+DateTime.Now.ToString("yyyyMMdd-HHmmss-fff")+".log");
        try
        {
            LoginStatus="SteamCMD wird gestartet. Gespeicherte Sitzung wird zuerst geprüft.";
            var auth=new Progress<SteamLoginSignal>(signal=>{
                LoginStatus=signal switch {
                    SteamLoginSignal.Password=>"Steam fordert ein Passwort an",
                    SteamLoginSignal.GuardCode=>"Steam fordert einen aktuellen Steam-Guard-Code an",
                    SteamLoginSignal.MobileApproval=>"Bitte die Anmeldung in deiner Steam-App bestätigen",
                    _=>"Steam hat die Anmeldung bestätigt; Sitzung wird abgeschlossen"
                };
                Status=LoginStatus;
            });
            var result=await _runner.RunAsync(Settings.SteamCmdPath,account.UserName,null,
                TimeSpan.FromMinutes(Settings.AuthenticationTimeoutMinutes),CreateSecretProvider(account),
                new Progress<string>(AppendLoginLog),_operation.Token,auth);
            // Bootstrap self-updates can replace SteamCMD before a login is attempted.
            // Retry login once, but NEVER replay a Workshop upload automatically here.
            if(!result.LoggedIn && Regex.IsMatch(result.Output,@"restarting|relaunching|update complete",RegexOptions.IgnoreCase)
                && !Regex.IsMatch(result.Output,@"password\s*:|Login Failure|InvalidPassword",RegexOptions.IgnoreCase))
            {
                AppendLoginLog("SteamCMD-Erstupdate erkannt. Anmeldung mit aktualisierter Installation wird einmal erneut gestartet.");
                result=await _runner.RunAsync(Settings.SteamCmdPath,account.UserName,null,
                    TimeSpan.FromMinutes(Settings.AuthenticationTimeoutMinutes),CreateSecretProvider(account),
                    new Progress<string>(AppendLoginLog),_operation.Token,auth);
            }
            if(result.ExitCode==0&&result.LoggedIn&&result.SteamId64 is { } steamId) {
                account.VerifiedUploadSteamId64=steamId;account.UploadIdentityCheckedAt=DateTimeOffset.UtcNow;
                await SaveAsync();
            }
            LoginStatus=result.ExitCode==0&&result.LoggedIn
                ?result.SteamId64 is null
                    ?"SteamCMD angemeldet, aber SteamID64 nicht ermittelbar"
                    :account.VerifiedWebSteamId64!=0&&account.VerifiedWebSteamId64!=result.SteamId64
                        ?"Kontokonflikt: Websitzung und SteamCMD verwenden verschiedene SteamIDs"
                        :"SteamCMD angemeldet · Upload-SteamID bestätigt"
                :SteamCmdPromptDetector.DescribeFailure(result.Output,result.ExitCode);
            Status=LoginStatus;
            AppendLoginLog(LoginStatus);
        }
        catch(OperationCanceledException) { LoginStatus="Anmeldung abgebrochen";Status=LoginStatus;AppendLoginLog(LoginStatus); }
        catch(Exception ex) { LoginStatus="Anmeldung fehlgeschlagen: "+ex.Message;Status=LoginStatus;AppendLoginLog(LoginStatus); }
        finally
        {
            try { await File.WriteAllTextAsync(_lastLoginLogFile,LoginLog); }
            catch(Exception ex) when(ex is IOException or UnauthorizedAccessException) { AppendLog("Login-Protokoll konnte nicht gespeichert werden: "+ex.Message); }
            _operation?.Dispose();_operation=null;IsBusy=false;
        }
    }
    private async Task InteractiveLoginAsync()
    {
        if(!await EnsureLoginReadyAsync())return;
        var account=ActiveAccount!;
        SteamCmdRunner.EnsureNoExternalInstance(Settings.SteamCmdPath);
        IsBusy=true;_operation=new();var completed=false;
        try
        {
            var start=new ProcessStartInfo(Path.GetFullPath(Settings.SteamCmdPath)) {
                UseShellExecute=true,WorkingDirectory=Path.GetDirectoryName(Path.GetFullPath(Settings.SteamCmdPath))!
            };
            start.ArgumentList.Add("+login");start.ArgumentList.Add(account.UserName);
            using var process=Process.Start(start)??throw new IOException("Originale SteamCMD-Konsole konnte nicht geöffnet werden.");
            using var kill=_operation.Token.Register(()=>{try{if(!process.HasExited)process.Kill(true);}catch(InvalidOperationException){}catch(System.ComponentModel.Win32Exception){}});
            LoginStatus="In der originalen SteamCMD-Konsole anmelden. Danach quit eingeben; Studio prüft die gespeicherte Sitzung automatisch.";
            Status=LoginStatus;
            await process.WaitForExitAsync(_operation.Token);completed=true;
        }
        catch(OperationCanceledException){LoginStatus="Konsolenanmeldung abgebrochen";Status=LoginStatus;}
        finally{_operation?.Dispose();_operation=null;IsBusy=false;}
        if(completed)await LoginAsync();
    }
    private void SaveLoginLog()
    {
        var path=DesktopDialogs.SaveFile("WorkshopStudio-Login.log","Login-Protokoll|*.log|Textdatei|*.txt");
        if(path is null)return;
        File.WriteAllText(path,LoginLog);
        Status="Bereinigtes Login-Protokoll gespeichert. Enthaltene Kontometadaten vor Weitergabe prüfen.";
    }
    private void OpenWebLogin()
    {
        EnsureIdle();
        var browser=new BrowserWindow(Store.Root,Settings.RememberWebSession,
            "https://steamcommunity.com/login/home/","Steam Community · Web-Anmeldung",false,accountId:ActiveAccount?.Id){Owner=DesktopDialogs.Owner};
        browser.ShowDialog();
        WebLoginStatus=browser.IsSteamWebSignedIn
            ?"Web-Anmeldung erkannt: "+browser.WebAccountName+" · Upload-Anmeldung separat"
            :!string.IsNullOrWhiteSpace(browser.LastError)?browser.LastError:"Keine Steam-Webanmeldung erkannt";
        Status=WebLoginStatus;
    }
    private void ClearCmdSession()
    {
        EnsureIdle();var managed=Path.Combine(Store.Root,"steamcmd");var path=Path.GetFullPath(Settings.SteamCmdPath);
        if(!PackageBuilder.IsWithin(path,managed))throw new InvalidOperationException("Externe SteamCMD-Installation: Sitzung wird nicht automatisch gelöscht. Die externe Installation selbst verwalten.");
        if(!DesktopDialogs.Confirm("Alle von SteamCMD gespeicherten Anmeldesitzungen dieser Studio-Installation entfernen? SteamCMD muss vollständig geschlossen sein."))return;
        SteamCmdRunner.EnsureNoExternalInstance(path);
        var config=Path.Combine(managed,"config");if(Directory.Exists(config))Directory.Delete(config,true);LoginStatus="Sitzungsdaten entfernt";
    }
    private async Task InstallSteamCmdAsync()
    {
        EnsureIdle();if(!DesktopDialogs.Confirm("SteamCMD direkt von Valve herunterladen und im Studio-Datenordner installieren? Beim ersten Start lädt Valve weitere Updates."))return;
        IsBusy=true;_operation=new();try{var dir=Path.Combine(Store.Root,"steamcmd");await _steam.DownloadSteamCmdAsync(dir,new Progress<string>(s=>{Status=s;AppendLog(s);}),_operation.Token);Settings.SteamCmdPath=Path.Combine(dir,"steamcmd.exe");SettingsDraft.SteamCmdPath=Settings.SteamCmdPath;Raise(nameof(SettingsDraft));await SaveAsync();}finally{_operation?.Dispose();_operation=null;IsBusy=false;}
    }
    private void OpenWeb(string url)=>new BrowserWindow(Store.Root,Settings.RememberWebSession,url,"Steam Community",false,accountId:ActiveAccount?.Id){Owner=DesktopDialogs.Owner}.ShowDialog();
    private async Task SaveSettingsAsync()
    {
        EnsureIdle();
        if(DesktopDialogs.HasValidationErrors(Application.Current.MainWindow))throw new InvalidDataException("Markierte Eingabefelder enthalten ungültige Werte.");
        WorkspaceStore.ValidateSettings(SettingsDraft);
        var updated=WorkspaceStore.Clone(SettingsDraft);
        // Profile selection is managed separately on the accounts and mods pages.
        updated.ActiveAccountId=Settings.ActiveAccountId;
        if(updated.AutomationEnabled&&!DesktopDialogs.Confirm($"Automatische Uploads für {updated.AutomationChannel} aktivieren?\n\nNur Mods mit aktivierter Auto-Publish-Option werden verarbeitet. Ein Upload veröffentlicht Inhalte auf Steam. Workshop Studio muss geöffnet bleiben."))return;
        Data.Settings=updated;
        SettingsDraft=WorkspaceStore.Clone(updated);Raise(nameof(SettingsDraft));
        ThemeService.Apply(Settings);await SaveAsync();ConfigureAutomation();Status="Einstellungen gespeichert.";
    }
    private void ConfigureAutomation()
    {
        foreach(var w in _watchers)w.Dispose();_watchers.Clear();_dirty.Clear();_lastSchedule=DateTimeOffset.UtcNow;
        if(!Settings.AutomationEnabled||!Settings.WatchFolders)return;
        foreach(var mod in Data.Mods.Where(m=>m.Enabled&&m.AutoPublish&&Directory.Exists(m.ContentFolder)))
        {
            try
            {
                var appProfile=Data.Apps.Single(a=>a.Id==mod.AppProfileId);
                var plan=ModSourceResolver.Resolve(appProfile,mod);
                var exclusions=string.Join('\n',Settings.GlobalExclusions,appProfile.DefaultExclusions,mod.Exclusions);
                foreach(var mount in plan.Sources)
                {
                    var id=mod.Id;var sourceFolder=mount.SourceRoot;var excludeSecrets=Settings.ExcludePotentialSecrets;
                    var watcher=new FileSystemWatcher(sourceFolder){IncludeSubdirectories=true,NotifyFilter=NotifyFilters.FileName|NotifyFilters.DirectoryName|NotifyFilters.LastWrite|NotifyFilters.Size,InternalBufferSize=65536};
                    void Changed(object? s,FileSystemEventArgs e)
                    {
                        var rel=Path.GetRelativePath(sourceFolder,e.FullPath).Replace('\\','/');
                        if(PackageBuilder.IsExcluded(rel,exclusions,excludeSecrets)||PackageBuilder.IsExcluded(mount.TargetPrefix+"/"+rel,exclusions,excludeSecrets))return;
                        Application.Current.Dispatcher.BeginInvoke(new Action(()=>_dirty[id]=DateTimeOffset.UtcNow));
                    }
                    watcher.Changed+=Changed;watcher.Created+=Changed;watcher.Deleted+=Changed;
                    watcher.Renamed+=(s,e)=>{Changed(s,e);Changed(s,new FileSystemEventArgs(WatcherChangeTypes.Deleted,Path.GetDirectoryName(e.OldFullPath)!,Path.GetFileName(e.OldFullPath)));};
                    watcher.Error+=(_,e)=>Application.Current.Dispatcher.BeginInvoke(new Action(()=>{_dirty[id]=DateTimeOffset.UtcNow;AppendLog("Dateiüberwachung: "+e.GetException().Message);}));
                    watcher.EnableRaisingEvents=true;_watchers.Add(watcher);
                }
                if(Settings.AutoDetectMetadata) {
                    var metadataRoots=LocalWorkshopSchemaReader.MetadataRoots(plan).ToList();
                    if(File.Exists(mod.PreviewFile))metadataRoots.Add(Path.GetDirectoryName(Path.GetFullPath(mod.PreviewFile))!);
                    foreach(var root in metadataRoots.Distinct(StringComparer.OrdinalIgnoreCase)) {
                        var id=mod.Id;
                        var metadataWatcher=new FileSystemWatcher(root){IncludeSubdirectories=false,NotifyFilter=NotifyFilters.FileName|NotifyFilters.LastWrite|NotifyFilters.Size};
                        void MetadataChanged(object? sender,FileSystemEventArgs e) {
                            var name=Path.GetFileName(e.FullPath);
                            if(!new[]{"workshop.txt","version.txt","VERSION","CHANGELOG.md","CHANGELOG.txt"}.Contains(name,StringComparer.OrdinalIgnoreCase)&&
                                !Path.GetFullPath(e.FullPath).Equals(Path.GetFullPath(string.IsNullOrWhiteSpace(mod.PreviewFile)?Path.Combine(root,".no-preview"):mod.PreviewFile),StringComparison.OrdinalIgnoreCase))return;
                            Application.Current.Dispatcher.BeginInvoke(new Action(()=>_dirty[id]=DateTimeOffset.UtcNow));
                        }
                        metadataWatcher.Changed+=MetadataChanged;metadataWatcher.Created+=MetadataChanged;metadataWatcher.Deleted+=MetadataChanged;
                        metadataWatcher.Renamed+=(s,e)=>{MetadataChanged(s,e);MetadataChanged(s,new FileSystemEventArgs(WatcherChangeTypes.Deleted,Path.GetDirectoryName(e.OldFullPath)!,Path.GetFileName(e.OldFullPath)));};
                        metadataWatcher.Error+=(_,_)=>Application.Current.Dispatcher.BeginInvoke(new Action(()=>_dirty[id]=DateTimeOffset.UtcNow));
                        metadataWatcher.EnableRaisingEvents=true;_watchers.Add(metadataWatcher);
                    }
                }
            }
            catch(Exception ex){AppendLog("Dateiüberwachung für "+mod.Name+" nicht eingerichtet: "+ex.Message);}
        }
    }

    private async Task TickAsync()
    {
        if(_tickBusy||IsBusy||!Settings.AutomationEnabled||Application.Current.Windows.OfType<Window>().Any(w=>w.IsVisible&&w!=Application.Current.MainWindow))return;_tickBusy=true;
        try{
            var now=DateTimeOffset.UtcNow;var targets=new HashSet<Guid>();
            foreach(var entry in _dirty.Where(k=>now-k.Value>=TimeSpan.FromSeconds(Settings.DebounceSeconds)).ToArray()){targets.Add(entry.Key);_dirty.Remove(entry.Key);}
            bool scheduled=Settings.ScheduleMode==ScheduleMode.Interval&&now-_lastSchedule>=TimeSpan.FromMinutes(Settings.ScheduleIntervalMinutes);
            var today=DateOnly.FromDateTime(DateTime.Now);
            if(Settings.ScheduleMode==ScheduleMode.Daily&&TimeOnly.TryParseExact(Settings.DailyTime,"HH:mm",out var time)&&TimeOnly.FromDateTime(DateTime.Now)>=time&&_lastDaily!=today){scheduled=true;_lastDaily=today;}
            if(scheduled){_lastSchedule=now;foreach(var m in Data.Mods.Where(m=>m.Enabled&&m.AutoPublish))targets.Add(m.Id);}
            var mods=Data.Mods.Where(m=>targets.Contains(m.Id)&&m.AutoPublish&&m.Enabled).ToArray();
            if(mods.Length>0){await EnqueueAsync(mods,true);await RunQueueAsync(true);}
        }catch(Exception e){Status="Automation pausiert: "+e.Message;AppendLog(Status);Settings.AutomationEnabled=false;SettingsDraft.AutomationEnabled=false;Raise(nameof(SettingsDraft));ConfigureAutomation();await SaveAsync();}
        finally{_tickBusy=false;}
    }
    private async Task ExportWorkspaceAsync(){var path=DesktopDialogs.SaveFile("workshop-studio.workspace.json","Arbeitsbereich|*.json");if(path is null)return;var export=WorkspaceStore.Clone(Data);export.Settings.AutomationEnabled=false;export.Settings.AutoLogin=false;export.Settings.CheckToolUpdatesOnStartup=false;await File.WriteAllTextAsync(path,JsonSerializer.Serialize(export,WorkspaceStore.JsonOptions));Status="Arbeitsbereich exportiert. Keine Passwörter, Cookies oder SteamCMD-Sitzungstoken enthalten.";}
    private async Task ImportWorkspaceAsync()
    {
        EnsureIdle();var path=DesktopDialogs.OpenFile("Arbeitsbereich|*.json");if(path is null)return;if(new FileInfo(path).Length>50*1024*1024)throw new InvalidDataException("Arbeitsbereich ist zu groß.");
        var data=JsonSerializer.Deserialize<Workspace>(await File.ReadAllTextAsync(path),WorkspaceStore.JsonOptions)??throw new InvalidDataException("Leere Datei.");
        if(data.SchemaVersion==1)WorkspaceStore.UpgradeSchema1(data);
        data.Settings.AutomationEnabled=false;data.Settings.AutoLogin=false;data.Settings.CheckToolUpdatesOnStartup=false;WorkspaceStore.Validate(data);WorkshopMetadataPlanner.SeedBaselines(data);
        foreach(var j in data.Jobs.Where(j=>j.Status is JobStatus.Preparing or JobStatus.Uploading)){j.Status=JobStatus.Interrupted;j.Message="Importierter Vorgang: nicht automatisch fortgesetzt.";}
        if(!DesktopDialogs.Confirm("Arbeitsbereich ersetzen? Vorher wird der aktuelle Stand gesichert. Importierte Automationen bleiben ausgeschaltet; Pfade und Update-Adressen vor Benutzung prüfen."))return;
        await BackupWorkspaceAsync();Data=data;SettingsDraft=WorkspaceStore.Clone(Settings);Raise(nameof(SettingsDraft));SelectedApp=null;SelectedMod=null;SelectedCollection=null;SelectedAccount=null;SelectedJob=null;ThemeService.Apply(Settings);await SaveAsync();ConfigureAutomation();
    }
    private async Task BackupWorkspaceAsync(){var path=Path.Combine(Store.BackupRoot,"workspace-"+DateTime.Now.ToString("yyyyMMdd-HHmmss-fff")+".json");await File.WriteAllTextAsync(path,JsonSerializer.Serialize(Data,WorkspaceStore.JsonOptions));Status="Arbeitsbereich gesichert. Quell-Mods und Zugangsdaten sind nicht enthalten.";}
    private void ClearStaging(){EnsureIdle();if(!DesktopDialogs.Confirm("Alle von Workshop Studio angelegten Paketordner löschen? Dies entfernt auch Diagnosepakete fehlgeschlagener Uploads. Quelldateien bleiben unangetastet."))return;foreach(var dir in Directory.GetDirectories(Store.StagingRoot))if(File.Exists(Path.Combine(dir,".workshopstudio-owned")))_builder.DeleteOwnedStage(dir);Status="Paketablage bereinigt.";}
    private async Task CheckUpdatesAsync()
    {
        EnsureIdle();
        using var updater=new ReleaseUpdater();
        Status="Offizielles GitHub-Release wird geprüft …";
        var release=await updater.CheckLatestAsync();
        if(release is null){Status="Noch kein öffentliches Workshop-Studio-Release vorhanden.";return;}
        var assembly=typeof(MainViewModel).Assembly.GetName().Version ?? new Version(0,0,0);
        var installed=new Version(assembly.Major,assembly.Minor,Math.Max(0,assembly.Build));
        if(release.Version<=installed){Status=$"Workshop Studio {installed} ist aktuell.";return;}
        var notes=release.Notes.Length>2500?release.Notes[..2500]+" …":release.Notes;
        if(!DesktopDialogs.Confirm($"Workshop Studio {release.Version} ist verfügbar.\n\n{notes}\n\nGeprüftes Windows-Paket jetzt von GitHub herunterladen?"))
        {Status="Update bleibt verfügbar; kein Download gestartet.";return;}
        IsBusy=true;_operation=new CancellationTokenSource();
        string payload;
        try {
            var progress=new Progress<string>(message=>Status=message);
            payload=await updater.DownloadAndStageAsync(release,Path.Combine(Store.Root,"updates"),progress,_operation.Token);
        } finally {_operation.Dispose();_operation=null;IsBusy=false;}
        Status=$"Update {release.Version} geprüft und vorbereitet.";
        if(!DesktopDialogs.Confirm($"Workshop Studio {release.Version} jetzt installieren und neu starten?\n\nDer aktuelle Arbeitsbereich bleibt im Benutzerprofil erhalten. Die bisherigen Programmdateien werden für eine Rücknahme gesichert."))
        {Status="Geprüftes Update liegt bereit: "+payload;return;}
        await Store.SaveAsync(Data);
        UpdateApplyService.Start(payload,AppContext.BaseDirectory,release.Version);
        Application.Current.MainWindow.Close();
    }
    private void ShowDiagnostics()=>new TextViewerWindow("Systemdiagnose",$"Workshop Studio 1.3.0\n.NET: {Environment.Version}\nBetriebssystem: {Environment.OSVersion}\n64-Bit: {Environment.Is64BitProcess}\nArbeitsbereich: {Store.Root}\nSteamCMD vorhanden: {File.Exists(Settings.SteamCmdPath)}\nApps: {AppCount}\nMods: {ModCount}\nCollections: {CollectionCount}\nWorkshop-Einträge: {Data.RemoteItems.Count}\n\nKeine Passwörter oder Sitzungstoken in dieser Ausgabe.\nEchte Veröffentlichungen sind erst nach gezielter Freigabe zu testen."){Owner=DesktopDialogs.Owner}.ShowDialog();
    public async Task SaveOnExitAsync(){await Store.SaveAsync(Data);}
    public void Dispose(){_sessionPasswords.Clear();_timer.Stop();_operation?.Cancel();foreach(var w in _watchers)w.Dispose();_steam.Dispose();}
}
