using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using WorkshopStudio.Core;
using WorkshopStudio.Desktop.Services;
namespace WorkshopStudio.Desktop.Views;
public partial class PackagePreviewWindow : Window
{
    private readonly PackageSnapshot _snapshot;
    private bool _ready;
    public PackagePreviewWindow(PackageSnapshot snapshot)
    {
        _snapshot=snapshot;InitializeComponent();
        Summary.Text=$"{snapshot.Plan.Sources.Count} Quellordner · {snapshot.Files.Count:N0} Dateien · {Format.Bytes(snapshot.Bytes)} · KEIN UPLOAD";
        MappingBox.Text=snapshot.Plan.Summary;
        Warnings.Text=string.Join("\n",snapshot.Plan.Warnings);
        DirectoriesBox.Text=string.Join("\n",snapshot.Directories);
        VdfBox.Text=File.ReadAllText(snapshot.VdfPath)+"\n\nSHA-256: "+snapshot.Fingerprint;
        FileGrid.ItemsSource=snapshot.Files;_ready=true;
    }
    private void FilterChanged(object sender,TextChangedEventArgs e)
    {
        if(!_ready)return;
        FileGrid.ItemsSource=_snapshot.Files.Where(f=>f.RelativePath.Contains(FilterBox.Text,StringComparison.OrdinalIgnoreCase)).ToArray();
    }
    private void Open_Click(object sender,RoutedEventArgs e)
    {
        try{DesktopDialogs.OpenFolder(_snapshot.ContentPath);}catch(Exception ex){DesktopDialogs.Error(ex);}
    }
    private void Save_Click(object sender,RoutedEventArgs e)
    {
        try
        {
            var path=DesktopDialogs.SaveFile("Upload-Dateiliste.json","JSON-Dateiliste|*.json");
            if(path is not null)File.WriteAllText(path,JsonSerializer.Serialize(new{files=_snapshot.Files,directories=_snapshot.Directories},WorkspaceStore.JsonOptions));
        }
        catch(Exception ex){DesktopDialogs.Error(ex);}
    }
}
