using System.Threading;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Text.Json;
using WorkshopStudio.Core;
using WorkshopStudio.Desktop.Services;
using WorkshopStudio.Desktop.ViewModels;
using WorkshopStudio.Desktop.Views;
namespace WorkshopStudio.Desktop;
public partial class App:Application
{
    private Mutex? _mutex;
    private bool _ownsMutex;
    protected override async void OnStartup(StartupEventArgs e)
    {
        FrameworkElement.LanguageProperty.OverrideMetadata(typeof(FrameworkElement),
            new FrameworkPropertyMetadata(System.Windows.Markup.XmlLanguage.GetLanguage(System.Globalization.CultureInfo.CurrentCulture.IetfLanguageTag)));
        base.OnStartup(e);
        ShutdownMode=ShutdownMode.OnExplicitShutdown;
        if(e.Args.Length==4 && e.Args[0]=="--apply-update") {
            var code=await UpdateApplyService.ApplyAsync(AppContext.BaseDirectory,e.Args[1],
                int.Parse(e.Args[2],System.Globalization.CultureInfo.InvariantCulture),Version.Parse(e.Args[3]));
            Shutdown(code);return;
        }
        ThemeService.Apply(new StudioSettings());
        // UI smoke writes to an isolated test workspace and may run beside the user's open app.
        if(!e.Args.Contains("--ui-smoke")) {
            _mutex=new Mutex(true,@"Local\WorkshopStudio.SingleInstance",out _ownsMutex);
            if(!_ownsMutex){DesktopDialogs.Info("Workshop Studio ist bereits geöffnet.","Workshop Studio");Shutdown();return;}
        }
        DispatcherUnhandledException+=(_,args)=>{DesktopDialogs.Error(args.Exception,"Workshop Studio · Unerwarteter Fehler");args.Handled=true;};
        try{
            var importMode=e.Args.Contains("--import-own-workshop");
            var webCheckIndex=Array.IndexOf(e.Args,importMode?"--import-own-workshop":"--check-web-session");
            if(webCheckIndex>=0) {
                if(webCheckIndex+1>=e.Args.Length)throw new ArgumentException("Für --check-web-session einen Ausgabeordner angeben.");
                await CheckWebSessionDiagnosticAsync(Path.GetFullPath(e.Args[webCheckIndex+1]),importMode);
                return;
            }
            var smokeIndex=Array.IndexOf(e.Args,"--ui-smoke");
            var smokeOutput=smokeIndex>=0&&smokeIndex+1<e.Args.Length ? Path.GetFullPath(e.Args[smokeIndex+1]) : null;
            if(smokeIndex>=0&&smokeOutput is null)throw new ArgumentException("Für --ui-smoke einen Ausgabeordner angeben.");
            var root=smokeOutput is null ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"WorkshopStudio")
                : Path.Combine(smokeOutput,"isolated-workspace");
            var store=new WorkspaceStore(root);Workspace workspace;
            try{workspace=smokeOutput is null ? await store.LoadAsync() : CreateSmokeWorkspace(smokeOutput);}
            catch(Exception ex){DesktopDialogs.Info("Arbeitsbereich konnte nicht geladen werden. Es wurde nichts überschrieben.\n\n"+ex.Message+"\n\nDaten: "+store.FilePath+"\nSicherung: workspace.json.bak","Workshop Studio");Shutdown(1);return;}
            ThemeService.Apply(workspace.Settings);
            var vm=new MainViewModel(store,workspace);var window=new MainWindow(vm);MainWindow=window;ShutdownMode=ShutdownMode.OnMainWindowClose;window.Show();
            if(smokeOutput is null) {
                var updateReport=Path.Combine(root,"updates","last-update-result.txt");
                try {
                    if(File.Exists(updateReport)) {
                        var result=await File.ReadAllTextAsync(updateReport);
                        File.Delete(updateReport);
                        if(result.StartsWith("FEHLER:",StringComparison.Ordinal))DesktopDialogs.Info(result,"Workshop Studio · Update");
                        else vm.Status=result;
                    }
                } catch(IOException) { }
            }
            if(smokeOutput is not null)await RunSmokeAsync(smokeOutput,vm,window);
        }catch(Exception ex){DesktopDialogs.Error(ex,"Workshop Studio · Start fehlgeschlagen");Shutdown(1);}
    }
    private async Task CheckWebSessionDiagnosticAsync(string output,bool importMode)
    {
        Directory.CreateDirectory(output);
        var report=Path.Combine(output,"web-session-result.txt");
        BrowserWindow? browser=null;
        try {
            var root=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"WorkshopStudio");
            var path=Path.Combine(root,"workspace.json");
            var workspace=JsonSerializer.Deserialize<Workspace>(await File.ReadAllTextAsync(path),WorkspaceStore.JsonOptions)
                ?? throw new InvalidDataException("Arbeitsbereich nicht lesbar.");
            var account=workspace.Accounts.FirstOrDefault(a=>a.Id==workspace.Settings.ActiveAccountId)
                ?? throw new InvalidDataException("Kein aktives Konto.");
            // The user's workspace is read as bytes only. Steam WebView2 keeps its own session profile.
            var diagnosticStore=new WorkspaceStore(Path.Combine(output,"diagnostic-cache"));
            using var service=new SteamService(diagnosticStore);
            browser=new BrowserWindow(root,true,
                "https://steamcommunity.com/myworkshopfiles/?p=1&numperpage=30&browsesort=myfiles&browsefilter=myfiles",
                "Steam-Websitzung prüfen",false,accountId:account.Id,scanAccount:WorkspaceStore.Clone(account),scanService:service);
            browser.Show();
            for(var i=0;i<100&&(browser.Browser.CoreWebView2 is null||browser.Browser.Source is null);i++)await Task.Delay(100);
            await Task.Delay(2500);
            var result=await browser.TryScanAsync(false);
            ulong? uploadId=null;
            if(File.Exists(workspace.Settings.SteamCmdPath)) {
                try {
                    using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(40));
                    var login=await new SteamCmdRunner().RunAsync(workspace.Settings.SteamCmdPath,account.UserName,null,
                        TimeSpan.FromSeconds(40),(_,_)=>Task.FromResult<string?>(null),null,timeout.Token);
                    if(login.ExitCode==0&&login.LoggedIn)uploadId=login.SteamId64;
                } catch(Exception ex) when(ex is OperationCanceledException or IOException or InvalidOperationException) { }
            }
            var webId=result?.Report.SteamId64;
            WorkshopImportSummary? imported=null;
            if(importMode) {
                if(result is null||uploadId is null||webId!=uploadId||result.Report.PagesRead==0)
                    throw new InvalidOperationException("Ohne bestätigte gleiche Web- und Upload-Identität wird nichts importiert.");
                var liveStore=new WorkspaceStore(root);
                var live=await liveStore.LoadAsync();
                var liveAccount=live.Accounts.Single(a=>a.Id==account.Id);
                var exactOriginal=await File.ReadAllTextAsync(path);
                imported=WorkshopImporter.Merge(live,liveAccount,result,uploadId.Value);
                var backup=Path.Combine(liveStore.BackupRoot,"workspace-before-workshop-import-"+DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss-fff")+".json");
                await File.WriteAllTextAsync(backup,exactOriginal);
                await liveStore.SaveAsync(live);
            }
            await File.WriteAllTextAsync(report,
                $"Websitzung erkannt: {browser.IsSteamWebSignedIn}\n"+
                $"Eigene Workshop-Seiten gelesen: {result?.Report.PagesRead??0}\n"+
                $"Eigene Einträge gefunden: {result?.Items.Count??0}\n"+
                $"Besitzer öffentlich bestätigt: {result?.Items.Count(i=>i.FieldStatus.GetValueOrDefault("owner")==RemoteFieldStatus.Known)??0}\n"+
                $"Öffentliche Details nicht verfügbar: {result?.Items.Count(i=>i.FieldStatus.GetValueOrDefault("details")==RemoteFieldStatus.AccessDenied)??0}\n"+
                $"Collections gefunden: {result?.Items.Count(i=>i.FileType==2)??0}\n"+
                $"SteamCMD-Identität bestätigt: {uploadId is not null}\n"+
                $"Web und Upload identisch: {webId is not null&&uploadId==webId}\n"+
                (imported is null?"":"Lokal importiert: "+imported.AddedMods+" Mods, "+imported.AddedCollections+
                    " Collections, "+imported.AddedRemote+" Bibliothekseinträge; nur lesbar: "+imported.ReadOnly+"\n")+
                $"Webstatus: {(result?.Report.Complete==true?"vollständig":browser.LastError.Length>0?browser.LastError:result?.Report.Error??"nicht bestätigt")}\n");
            browser.Close();Shutdown(0);
        } catch(Exception ex) {
            if(browser?.IsVisible==true)browser.Close();
            await File.WriteAllTextAsync(report,"Web/Upload-Abgleich nicht abgeschlossen: "+ex.GetType().Name+" · "+ex.Message+"\n");
            Shutdown(1);
        }
    }
    private static Workspace CreateSmokeWorkspace(string output)
    {
        var source=Path.Combine(output,"sample-mod");Directory.CreateDirectory(source);
        File.WriteAllText(Path.Combine(source,"mod.info"),"id=ui-smoke\nname=UI Smoke\nversion=1.2\n");
        File.WriteAllText(Path.Combine(source,"config.ini"),"[Server]\n; Beispielwerte für das Wiki\nGreeting=Grüße aus Workshop Studio\nEnableWelcome=true\nMaxEvents=8\n");
        var workspace=new Workspace();workspace.Settings.RefreshMetadataOnStartup=false;
        workspace.Settings.CheckToolUpdatesOnStartup=false;workspace.Settings.AutoLogin=false;
        var app=workspace.Apps[0];app.Name="Project Zomboid";
        workspace.Apps.Add(new AppProfile{AppId=244850,Name="Zweites Testspiel"});
        var mod=new ModProject{AppProfileId=app.Id,Name="UI Smoke Mod",ContentFolder=source,ModIds="ui-smoke",Version="1.2",WorkshopId=123456789};
        workspace.Mods.Add(mod);
        var account=new SteamAccount{UserName="ui_smoke",DisplayName="Testkonto",VerifiedWebSteamId64=76561198000000000UL};
        workspace.Accounts.Add(account);workspace.Settings.ActiveAccountId=account.Id;
        workspace.RemoteItems.Add(new RemoteWorkshopItem{Id=mod.WorkshopId,OwnerSteamId64=account.VerifiedWebSteamId64,
            ConsumerAppId=app.AppId,FileType=0,Title="UI Smoke Mod",Description="Bestehender Workshop-Eintrag für den Oberflächentest.",
            AuthorVersion="1.2",VersionSource="Testdaten",LocalModId=mod.Id});
        return workspace;
    }
    private async Task RunSmokeAsync(string output,MainViewModel vm,MainWindow window)
    {
        try {
            for(var i=0;i<40&&vm.IsBusy;i++)await Task.Delay(100);
            await Task.Delay(300);
            Capture(window,Path.Combine(output,"dashboard-1280x800.png"),1280,800);
            vm.CurrentPage="mods";await Dispatcher.InvokeAsync(()=>{},System.Windows.Threading.DispatcherPriority.Render);window.UpdateLayout();
            CaptureElement(FindChild<ModsView>(window)!.LocalModActions,Path.Combine(output,"mods-actions.png"));
            vm.CurrentPage="history";await Dispatcher.InvokeAsync(()=>{},System.Windows.Threading.DispatcherPriority.Render);window.UpdateLayout();
            CaptureElement(FindChild<HistoryView>(window)!.HistoryActions,Path.Combine(output,"backup-actions.png"));
            vm.CurrentPage="automation";await Dispatcher.InvokeAsync(()=>{},System.Windows.Threading.DispatcherPriority.Render);window.UpdateLayout();
            CaptureRegion(window,new Rect(260,160,975,330),Path.Combine(output,"automation-settings.png"));
            vm.CurrentPage="library";vm.SelectedRemoteItem=vm.Data.RemoteItems.Single();
            await Dispatcher.InvokeAsync(()=>{},System.Windows.Threading.DispatcherPriority.Render);
            Capture(window,Path.Combine(output,"library-1920x1080.png"),1920,1080);
            window.MaximizeButton.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            if(window.WindowState!=WindowState.Maximized)throw new InvalidDataException("Maximieren-Knopf funktioniert nicht.");
            await Dispatcher.InvokeAsync(()=>{},System.Windows.Threading.DispatcherPriority.Render);
            window.UpdateLayout();
            if(!window.CaptionButtonsInsideWorkArea())throw new InvalidDataException("Fensterknöpfe liegen im maximierten Zustand außerhalb des sichtbaren Monitorbereichs.");
            CaptureCurrent(window,Path.Combine(output,"maximized-window.png"));
            if(((System.Windows.Shapes.Path)window.MaximizeButton.Content).Data!= (Geometry)FindResource("IconRestore"))
                throw new InvalidDataException("Wiederherstellen-Symbol wird nicht angezeigt.");
            window.MaximizeButton.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            if(window.WindowState!=WindowState.Normal)throw new InvalidDataException("Wiederherstellen-Knopf funktioniert nicht.");
            window.MinimizeButton.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            if(window.WindowState!=WindowState.Minimized)throw new InvalidDataException("Minimieren-Knopf funktioniert nicht.");
            window.WindowState=WindowState.Normal;
            if(window.CloseButton.Foreground != (Brush)FindResource("DangerBrush"))
                throw new InvalidDataException("Schließen-Knopf ist nicht rot gekennzeichnet.");
            var mod=vm.Data.Mods.Single();var app=vm.Data.Apps.Single(a=>a.Id==mod.AppProfileId);
            var catalogRoot=Path.Combine(output,"local-mod-catalog");
            var matched=Path.Combine(catalogRoot,"Existing","Contents","mods","UI Smoke Mod");
            var fresh=Path.Combine(catalogRoot,"New","mods","Fresh Local Mod");
            Directory.CreateDirectory(matched);Directory.CreateDirectory(fresh);
            File.WriteAllText(Path.Combine(matched,"mod.info"),"name=UI Smoke Mod\nid=ui-smoke\n");
            File.WriteAllText(Path.Combine(fresh,"mod.info"),"name=Fresh Local Mod\nid=fresh-local\n");
            var catalog=LocalModCatalog.Discover(catalogRoot);
            var suggestions=LocalModCatalog.Suggest(catalog.Candidates,vm.Data.Mods,app.Id);
            if(catalog.Candidates.Count!=2 || suggestions.Count(s=>s.ExistingModId==mod.Id)!=1)
                throw new InvalidDataException("Lokale Einzelmod-Erkennung oder Profilabgleich fehlgeschlagen.");
            var matching=new LocalModMatchWindow(app,catalog,suggestions,vm.Data.Mods){Owner=window};matching.Show();
            await Dispatcher.InvokeAsync(()=>{},System.Windows.Threading.DispatcherPriority.Render);
            Capture(matching,Path.Combine(output,"local-mod-matching-980x740.png"),980,740);
            CaptureRegion(matching,new Rect(615,180,315,285),Path.Combine(output,"local-mod-targets.png"));
            matching.Close();
            var configs=await ConfigDiscovery.FindAsync(app,mod,512,CancellationToken.None);
            var config=new ConfigWindow(mod.Name,configs){Owner=window};config.Show();
            await Dispatcher.InvokeAsync(()=>{},System.Windows.Threading.DispatcherPriority.Render);
            Capture(config,Path.Combine(output,"config-1040x760.png"),1040,760);
            config.Tabs.SelectedIndex=1;await Dispatcher.InvokeAsync(()=>{},System.Windows.Threading.DispatcherPriority.Render);config.UpdateLayout();
            CaptureCurrent(config,Path.Combine(output,"config-options.png"));config.Tabs.SelectedIndex=0;
            await Dispatcher.InvokeAsync(()=>{},System.Windows.Threading.DispatcherPriority.Render);
            config.RawText.Focus();config.RawText.Select(0,8);
            if(config.RawText.SelectedText!="[Server]")throw new InvalidDataException("Config-Test konnte Originaltext nicht markieren: "+config.RawText.SelectionLength);
            config.CopySelectionButton.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            // The copy service reads the real Windows clipboard back and verifies the selected text before completing.
            await config.LastCopyTask;
            config.Close();
            var proposed=WorkspaceStore.Clone(mod);proposed.Description="Neue lokale Beschreibung";proposed.ChangeNote="Neue Release-Notiz";
            var builder=new PackageBuilder(vm.Store);
            var package=await builder.BuildAsync(app,proposed,ReleaseChannel.Live,vm.Settings,Guid.NewGuid(),null,CancellationToken.None);
            var automatic=WorkshopMetadataPlanner.Compare(proposed,ReleaseChannel.Live,vm.Data.RemoteItems.Single(),"","",true);
            var update=new UpdatePlanWindow(vm.Data.Accounts.Single(),app,proposed,ReleaseChannel.Live,vm.Data.RemoteItems.Single(),package,automatic){Owner=window};
            update.Show();await Dispatcher.InvokeAsync(()=>{},System.Windows.Threading.DispatcherPriority.Render);
            if(!update.SelectedFields.Contains("description")||!update.SelectedFields.Contains("changenote"))throw new InvalidDataException("Geänderte Metadaten wurden im Update-Plan nicht automatisch vorausgewählt.");
            Capture(update,Path.Combine(output,"automatic-update-plan.png"),820,680);
            CaptureElement((FrameworkElement)((FrameworkElement)update.AutomaticText.Parent).Parent,Path.Combine(output,"metadata-decisions.png"));
            var metadataScroll=FindChild<System.Windows.Controls.ScrollViewer>(update)!;
            var titlePosition=update.TitleBox.TranslatePoint(new Point(0,0),metadataScroll);
            metadataScroll.ScrollToVerticalOffset(metadataScroll.VerticalOffset+titlePosition.Y-12);
            await Dispatcher.InvokeAsync(()=>{},System.Windows.Threading.DispatcherPriority.Render);update.UpdateLayout();
            var selectionArea=metadataScroll.TransformToAncestor(update).TransformBounds(new Rect(0,0,metadataScroll.ActualWidth,metadataScroll.ActualHeight));
            selectionArea.Y+=14;selectionArea.Height-=14;CaptureRegion(update,selectionArea,Path.Combine(output,"metadata-selection.png"));
            update.Close();builder.DeleteOwnedStage(package.Root);
            await File.WriteAllTextAsync(Path.Combine(output,"ui-smoke-result.txt"),
                "PASS: Dashboard und Bibliothek als echte WPF-Fenster gerendert; Iconify-Fensterknöpfe auch maximiert geprüft; lokale Mod-Zuordnung gerendert; Config-Originaltext kopiert; automatische Vorauswahl geänderter Beschreibung und Änderungsnotiz im Update-Plan geprüft. Kein Upload.\n");
            await vm.SaveOnExitAsync();vm.Dispose();Shutdown(0);
        } catch(Exception ex) {
            await File.WriteAllTextAsync(Path.Combine(output,"ui-smoke-result.txt"),"FAIL: "+ex+"\n");
            Shutdown(1);
        }
    }
    private static void Capture(Window window,string path,int width,int height)
    {
        window.WindowState=WindowState.Normal;window.Width=width;window.Height=height;window.UpdateLayout();
        CaptureCurrent(window,path);
    }
    private static void CaptureCurrent(Window window,string path)
    {
        var width=(int)Math.Ceiling(window.ActualWidth);
        var height=(int)Math.Ceiling(window.ActualHeight);
        Visual visual=window;
        var bitmap=new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32);
        bitmap.Render(visual);
        var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));
        using var file=File.Create(path);png.Save(file);
    }
    private static T? FindChild<T>(DependencyObject root) where T:DependencyObject
    {
        if(root is T match)return match;
        for(var i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)if(FindChild<T>(VisualTreeHelper.GetChild(root,i)) is { } child)return child;
        return null;
    }
    private static void CaptureElement(FrameworkElement element,string path)
    {
        var window=Window.GetWindow(element)??throw new InvalidOperationException("Screenshot-Element besitzt kein Fenster.");
        var area=element.TransformToAncestor(window).TransformBounds(new Rect(0,0,element.ActualWidth,element.ActualHeight));
        CaptureRegion(window,area,path);
    }
    private static void CaptureRegion(Visual visual,Rect area,string path)
    {
        var drawing=new DrawingVisual();using(var context=drawing.RenderOpen())context.DrawRectangle(
            new VisualBrush(visual){Viewbox=area,ViewboxUnits=BrushMappingMode.Absolute,Stretch=Stretch.Fill},null,new Rect(0,0,area.Width,area.Height));
        var bitmap=new RenderTargetBitmap((int)Math.Ceiling(area.Width),(int)Math.Ceiling(area.Height),96,96,PixelFormats.Pbgra32);bitmap.Render(drawing);
        var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));using var file=File.Create(path);png.Save(file);
    }
    protected override void OnExit(ExitEventArgs e){if(_ownsMutex)_mutex?.ReleaseMutex();_mutex?.Dispose();base.OnExit(e);}
}
