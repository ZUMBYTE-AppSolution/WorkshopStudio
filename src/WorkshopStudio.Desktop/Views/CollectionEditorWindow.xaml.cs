using System.Windows;
using System.Windows.Controls;
using WorkshopStudio.Core;
using WorkshopStudio.Desktop.Services;
namespace WorkshopStudio.Desktop.Views;
public partial class CollectionEditorWindow:Window
{
    private readonly WorkshopCollection _model;
    private readonly IReadOnlyList<ModProject> _mods;
    private bool _loading=true;
    public CollectionEditorWindow(WorkshopCollection model,IReadOnlyList<AppProfile> apps,IReadOnlyList<ModProject> mods)
    {
        InitializeComponent();_model=model;_mods=mods;DataContext=model;AppBox.ItemsSource=apps;ExternalBox.Text=string.Join(';',model.ExternalWorkshopIds);_loading=false;FillMods();
    }
    private void FillMods(){ModsBox.ItemsSource=_mods.Where(m=>m.AppProfileId==_model.AppProfileId).ToArray();foreach(var m in _mods.Where(m=>m.AppProfileId==_model.AppProfileId&&_model.ModProjectIds.Contains(m.Id)))ModsBox.SelectedItems.Add(m);}
    private void App_SelectionChanged(object sender,SelectionChangedEventArgs e){if(!_loading)FillMods();}
    private void Save_Click(object sender,RoutedEventArgs e)
    {
        try{
            if(DesktopDialogs.HasValidationErrors(this))throw new InvalidDataException("Markierte Eingaben prüfen.");
            if(string.IsNullOrWhiteSpace(_model.Name))throw new InvalidDataException("Name fehlt.");
            var ids=ExternalBox.Text.Split([';',' ',',','\r','\n','\t'],StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries).Select(s=>ulong.TryParse(s,out var id)&&id>0?id:throw new InvalidDataException("Ungültige externe Workshop-ID: "+s)).Distinct().ToList();
            _model.ExternalWorkshopIds=ids;_model.ModProjectIds=ModsBox.SelectedItems.Cast<ModProject>().Select(m=>m.Id).ToList();DialogResult=true;
        }catch(Exception ex){DesktopDialogs.Error(ex);}
    }
}
