using System.Windows;
using WorkshopStudio.Core;
using WorkshopStudio.Desktop.Services;
namespace WorkshopStudio.Desktop.Views;
public partial class AccountEditorWindow:Window
{
    private readonly SteamAccount _model;
    public string Password=>PasswordInput.Password;
    public AccountEditorWindow(SteamAccount model){InitializeComponent();_model=model;DataContext=model;}
    private void Save_Click(object sender,RoutedEventArgs e){try{_model.UserName = _model.UserName.Trim(); SteamCmdRunner.ValidateUserName(_model.UserName);DialogResult=true;}catch(Exception ex){DesktopDialogs.Error(ex);}}
}
