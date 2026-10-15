using System.Windows;
using System.Windows.Controls;
using WorkshopStudio.Core;
using WorkshopStudio.Desktop.Services;
namespace WorkshopStudio.Desktop.Views;
public partial class ModEditorWindow:Window
{
    private readonly ModProject _model;
    private readonly IReadOnlyList<AppProfile> _apps;
    private readonly string _studioRoot;
    private bool _ready;
    public ModEditorWindow(ModProject model,IReadOnlyList<AppProfile> apps,string studioRoot)
    {
        _model=model;_apps=apps;_studioRoot=studioRoot;InitializeComponent();
        DataContext=model;AppBox.ItemsSource=apps;VisibilityBox.ItemsSource=new[]{"Öffentlich","Nur Freunde","Privat","Nicht gelistet"};
        _ready=true;
        if(!string.IsNullOrWhiteSpace(model.ContentFolder))RefreshSource(false);
    }
    private AppProfile CurrentApp => _apps.FirstOrDefault(a=>a.Id==_model.AppProfileId)??throw new InvalidOperationException("App auswählen.");
    private void ContentChanged(object sender,TextChangedEventArgs e)
    {
        if(!_ready)return;
        SourceStatus.Text="Quelle geändert · bitte Struktur prüfen";SourceMapping.Text="";
    }
    private void BrowseContent_Click(object sender,RoutedEventArgs e)=>ChooseSource(false);
    private void BrowseBundle_Click(object sender,RoutedEventArgs e)=>ChooseSource(true);
    private void ChooseSource(bool multiple)
    {
        try
        {
            var app=CurrentApp;
            if(multiple&&!ModSourceResolver.IsZomboid(app))throw new InvalidOperationException("Die Mod-Auswahl gilt für Project Zomboid. Bei anderen Spielen den vom Spiel erwarteten Inhaltsordner wählen.");
            var p=DesktopDialogs.Folder(multiple?"Ordner mit deinen Mods auswählen":"Einzelne Mod auswählen (nicht das Spielverzeichnis)",_model.ContentFolder);
            if(p is null)return;
            if(!ModSourceResolver.IsZomboid(app))
            {
                ModSourceResolver.EnsureSafeDirectory(p);_model.SourceMode=ModSourceMode.SingleMod;_model.SelectedModFolders=[];ContentBox.Text=p;
            }
            else
            {
                var discovery=ModSourceResolver.Discover(p);
                IReadOnlyList<ModSourceCandidate> selected=discovery.Candidates;
                if(multiple||selected.Count>1)
                {
                    var chooser=new ModPickerWindow(discovery.Candidates,multiple,_model.SelectedModFolders){Owner=this};
                    if(chooser.ShowDialog()!=true)return;selected=chooser.Selected;
                }
                _model.SourceMode=multiple?ModSourceMode.SelectedMods:ModSourceMode.SingleMod;
                _model.SelectedModFolders=multiple?selected.Select(c=>c.FolderName).ToList():[];
                ContentBox.Text=multiple?discovery.ContainerPath:selected.Single().Root;
                ModIdsBox.Text=string.Join(';',selected.SelectMany(c=>c.ModIds).Distinct());
                if(_model.Name=="Neue Mod"&&selected.Count==1)
                {
                    _model.Name=selected[0].DisplayName;
                    DataContext=null;DataContext=_model;
                }
            }
            RefreshSource(true);
        }
        catch(Exception ex){DesktopDialogs.Error(ex);}
    }
    private bool RefreshSource(bool showError)
    {
        try
        {
            var plan=ModSourceResolver.Resolve(CurrentApp,_model);
            if(PackageBuilder.IsWithin(_model.ContentFolder,_studioRoot)||PackageBuilder.IsWithin(_studioRoot,_model.ContentFolder))
                throw new InvalidDataException("Mod-Quellen und Studio-Daten müssen getrennt liegen.");
            SourceStatus.Text=plan.IsZomboid?$"{plan.Sources.Count} Mod(s) · "+_model.SourceModeLabel:"Generischer Workshop-Inhalt";
            SourceMapping.Text=plan.Summary+(plan.Warnings.Count>0?"\n\n"+string.Join("\n",plan.Warnings):"");
            if(plan.IsZomboid)ModIdsBox.Text=string.Join(';',plan.ModIds);
            return true;
        }
        catch(Exception ex)
        {
            SourceStatus.Text="Quelle noch nicht verwendbar";SourceMapping.Text=ex.Message;
            if(showError)DesktopDialogs.Error(ex);return false;
        }
    }
    private void BrowsePreview_Click(object sender,RoutedEventArgs e){var p=DesktopDialogs.OpenFile("Vorschaubilder|*.png;*.jpg;*.jpeg");if(p is not null)PreviewBox.Text=p;}
    private void Template_Click(object sender,RoutedEventArgs e)
    {
        try{
            var app=_apps.FirstOrDefault(a=>a.Id==_model.AppProfileId)??throw new InvalidOperationException("App auswählen.");
            var input=new InputWindow("Neue Mod-Struktur","Technische Mod-ID, z. B. rp-core",false){Owner=this};if(input.ShowDialog()!=true)return;
            var parent=DesktopDialogs.Folder("Übergeordneten Projektordner auswählen",app.SourceRoot);if(parent is null)return;
            if(PackageBuilder.IsWithin(parent,_studioRoot))throw new InvalidDataException("Mod-Quellen außerhalb des Studio-Datenordners anlegen.");
            var path=TemplateCreator.Create(parent,input.Value,_model.Name,app.Template);_model.SourceMode=ModSourceMode.SingleMod;_model.SelectedModFolders=[];ContentBox.Text=path;ModIdsBox.Text=input.Value;RefreshSource(false);DesktopDialogs.Info("Neue Struktur angelegt. Das Mod-Profil anschließend speichern. Bei Abbrechen bleiben angelegte Quelldateien erhalten.");
        }catch(Exception ex){DesktopDialogs.Error(ex);}
    }
    private void DetectIds_Click(object sender,RoutedEventArgs e)=>RefreshSource(true);
    private void DetectSchema_Click(object sender,RoutedEventArgs e)
    {
        try {
            if(!RefreshSource(true))return;
            var found=LocalWorkshopSchemaReader.Inspect(CurrentApp,_model);
            var text=$"Titel: {(found.Title.Length==0?"nicht gefunden":found.Title)}\nVersion: {(found.Version.Length==0?"nicht gefunden":found.Version)}\n"+
                $"Bild: {(found.PreviewPath.Length==0?"nicht gefunden":found.PreviewPath)}\nMod-IDs: {string.Join("; ",found.ModIds)}\n\nQUELLEN\n{found.Summary}\n\n"+
                $"BESCHREIBUNG\n{found.Description}\n\nLOKALER ÄNDERUNGSVERLAUF\n{found.Changelog}";
            new TextViewerWindow("Erkannte Workshop-Metadaten",text){Owner=this}.ShowDialog();
            var fields=new List<string>();
            if((_model.Name=="Neue Mod"||_model.Name.Length==0)&&found.Title.Length>0)fields.Add("Titel");
            if(_model.Description.Length==0&&found.Description.Length>0)fields.Add("Beschreibung");
            if((_model.Version.Length==0||_model.Version=="1.0.0"&&!_model.ImportedFromWorkshop)&&found.Version.Length>0)fields.Add("Version");
            if(_model.PreviewFile.Length==0&&found.PreviewPath.Length>0)fields.Add("Bildpfad");
            if(fields.Count==0)return;
            if(!DesktopDialogs.Confirm("Folgende noch leeren Felder aus den angezeigten lokalen Dateien übernehmen?\n"+string.Join(", ",fields)+
                "\n\nDies speichert und veröffentlicht noch nichts."))return;
            if(fields.Contains("Titel"))_model.Name=found.Title;
            if(fields.Contains("Beschreibung"))_model.Description=found.Description;
            if(fields.Contains("Version"))_model.Version=found.Version;
            if(fields.Contains("Bildpfad"))_model.PreviewFile=found.PreviewPath;
            DataContext=null;DataContext=_model;
        } catch(Exception ex){DesktopDialogs.Error(ex);}
    }
    private void ResetReconciliation_Click(object sender,RoutedEventArgs e)
    {
        if(!DesktopDialogs.Confirm("Hast du geprüft, dass kein unzugeordneter Workshop-Eintrag existiert? Mit einer ID von 0 kann der nächste Upload einen neuen Eintrag erzeugen."))return;
        _model.NeedsLiveReconciliation=false;_model.NeedsTestReconciliation=false;DataContext=null;DataContext=_model;
    }
    private void Save_Click(object sender,RoutedEventArgs e)
    {
        if(DesktopDialogs.HasValidationErrors(this)){DesktopDialogs.Info("Markierte Eingaben prüfen.");return;}
        if(string.IsNullOrWhiteSpace(_model.Name)||!_apps.Any(a=>a.Id==_model.AppProfileId)){DesktopDialogs.Info("App und Titel erforderlich.");return;}
        if(!string.IsNullOrWhiteSpace(_model.ContentFolder))
        {
            if(!RefreshSource(true))return;
            // Pin one resolved directory. A new sibling must never silently become part of this profile.
            if(ModSourceResolver.IsZomboid(CurrentApp)&&_model.SourceMode==ModSourceMode.SingleMod)
                ContentBox.Text=ModSourceResolver.Resolve(CurrentApp,_model).Sources.Single().SourceRoot;
        }
        if(_model.Visibility is <0 or >3){DesktopDialogs.Info("Sichtbarkeit auswählen.");return;}
        if(_model.WorkshopId>0&&_model.WorkshopId==_model.TestWorkshopId){DesktopDialogs.Info("Live und Test benötigen unterschiedliche IDs.");return;}
        if(_model.WorkshopId>0)_model.NeedsLiveReconciliation=false;if(_model.TestWorkshopId>0)_model.NeedsTestReconciliation=false;
        DialogResult=true;
    }
}
