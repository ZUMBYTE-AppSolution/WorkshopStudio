using System.Windows;
using WorkshopStudio.Desktop.Services;
namespace WorkshopStudio.Desktop.Views;
public partial class TextViewerWindow:Window
{
    public TextViewerWindow(string title,string content){InitializeComponent();Title=title;Heading.Text=title;Output.Text=content;}
    private void Copy_Click(object sender,RoutedEventArgs e){try{Clipboard.SetText(Output.Text);}catch(Exception ex){DesktopDialogs.Error(ex);}}
    private void Save_Click(object sender,RoutedEventArgs e){try{var path=DesktopDialogs.SaveFile("workshop-studio-log.txt","Text|*.txt");if(path is not null)File.WriteAllText(path,Output.Text);}catch(Exception ex){DesktopDialogs.Error(ex);}}
}
