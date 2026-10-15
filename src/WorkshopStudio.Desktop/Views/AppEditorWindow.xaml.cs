using System.Windows;
using WorkshopStudio.Core;
using WorkshopStudio.Desktop.Services;
namespace WorkshopStudio.Desktop.Views;
public partial class AppEditorWindow:Window
{
    private readonly AppProfile _model;
    public AppEditorWindow(AppProfile model){InitializeComponent();_model=model;DataContext=model;TemplateBox.ItemsSource=Enum.GetValues<GameTemplate>();VisibilityBox.ItemsSource=new[]{"Öffentlich","Nur Freunde","Privat","Nicht gelistet"};}
    private void Browse_Click(object sender,RoutedEventArgs e){var p=DesktopDialogs.Folder("Standard-Quellverzeichnis",_model.SourceRoot);if(p is not null)SourceRootBox.Text=p;}
    private void Save_Click(object sender,RoutedEventArgs e){if(DesktopDialogs.HasValidationErrors(this)){DesktopDialogs.Info("Markierte Eingaben prüfen.");return;}if(_model.AppId==0||string.IsNullOrWhiteSpace(_model.Name)){DesktopDialogs.Info("Gültige AppID und Name erforderlich.");return;}DialogResult=true;}
}
