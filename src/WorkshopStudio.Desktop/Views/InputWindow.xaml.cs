using System.Windows;
namespace WorkshopStudio.Desktop.Views;
public partial class InputWindow:Window
{
    private readonly bool _secret;
    public string Value=>_secret?Secret.Password:Plain.Text.Trim();
    public InputWindow(string title,string prompt,bool secret){InitializeComponent();Title=title;Heading.Text=title;Prompt.Text=prompt;_secret=secret;Plain.Visibility=secret?Visibility.Collapsed:Visibility.Visible;Secret.Visibility=secret?Visibility.Visible:Visibility.Collapsed;Loaded+=(_,_)=>{if(secret)Secret.Focus();else Plain.Focus();};}
    private void Accept_Click(object sender,RoutedEventArgs e){if(string.IsNullOrWhiteSpace(Value))return;DialogResult=true;}
}
