using System.Windows.Controls;
namespace WorkshopStudio.Desktop.Views;
public partial class AccountsView : UserControl
{
    public AccountsView() { InitializeComponent(); }
    private void LoginLog_Changed(object sender, TextChangedEventArgs e)
    {
        if (sender is TextBox log) log.ScrollToEnd();
    }
}
