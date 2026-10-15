using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WorkshopStudio.Core;
using WorkshopStudio.Desktop.Services;

namespace WorkshopStudio.Desktop.Views;
public partial class ConfigWindow : Window
{
    private readonly ConfigDiscoveryResult _result;
    public ConfigWindow(string modName, ConfigDiscoveryResult result)
    {
        InitializeComponent(); _result=result; Heading.Text="Config / Befehle · "+modName;
        StateText.Text=result.State+(result.Warnings.Count>0?" · "+string.Join("; ",result.Warnings):"");
        Files.ItemsSource=result.Files; ServerText.Text=result.ServerLines;
        if(result.Files.Count>0)Files.SelectedIndex=0;
        else RawText.Text=result.State=="Lokale Quelle fehlt" ? "Lokale Quelle fehlt. Aus Steam-Metadaten kann keine Configdatei wiederhergestellt werden." : result.State;
    }
    private void Files_SelectionChanged(object sender,SelectionChangedEventArgs e)
    {
        if(Files.SelectedItem is not ConfigDocument doc)return;
        RawText.Text=doc.RawText;
        OptionsText.Text=doc.Status+"\n\n"+string.Join("\n",doc.Options.Select(o=>$"{(o.Section.Length>0?"["+o.Section+"] ":"")}{o.Key} = {o.Value}"));
        StateText.Text=_result.State+" · "+doc.RelativePath+" · "+doc.Status+
            (doc.PossibleSecrets?" · Mögliche Zugangsdaten: Rohtext vor Weitergabe prüfen.":"");
    }
    private TextBox ActiveText() => Tabs.SelectedIndex switch { 1 => OptionsText, 2 => ServerText, _ => RawText };
    private void CopySelection_Click(object sender,RoutedEventArgs e)
    {
        var text=ActiveText().SelectedText;
        if(text.Length>0)Clipboard.SetText(text);
    }
    private void CopyAll_Click(object sender,RoutedEventArgs e) => Clipboard.SetText(ActiveText().Text);
    private void Wrap_Click(object sender,RoutedEventArgs e)
    {
        RawText.TextWrapping=OptionsText.TextWrapping=WrapBox.IsChecked==true?TextWrapping.Wrap:TextWrapping.NoWrap;
    }
    private void Search_KeyDown(object sender,KeyEventArgs e){if(e.Key==Key.Enter){Find_Click(sender,e);e.Handled=true;}}
    private void Find_Click(object sender,RoutedEventArgs e)
    {
        var term=SearchBox.Text;if(term.Length==0)return;
        var text=RawText.Text;var start=Math.Max(0,RawText.SelectionStart+RawText.SelectionLength);
        var index=text.IndexOf(term,start,StringComparison.OrdinalIgnoreCase);
        if(index<0)index=text.IndexOf(term,StringComparison.OrdinalIgnoreCase);
        if(index<0){StateText.Text="Suchtext nicht gefunden: "+term;return;}
        RawText.Focus();RawText.Select(index,term.Length);RawText.ScrollToLine(RawText.GetLineIndexFromCharacterIndex(index));
    }
    private void Export_Click(object sender,RoutedEventArgs e)
    {
        if(Files.SelectedItem is not ConfigDocument doc)return;
        if(doc.PossibleSecrets&&!DesktopDialogs.Confirm("Diese Datei enthält möglicherweise Passwörter oder Tokens. Originaltext trotzdem als Datei exportieren?"))return;
        var path=DesktopDialogs.SaveFile(Path.GetFileNameWithoutExtension(doc.RelativePath)+".txt","Textdatei|*.txt");
        if(path is not null)File.WriteAllText(path,doc.RawText);
    }
}
