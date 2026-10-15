using System.Windows;
using WorkshopStudio.Core;

namespace WorkshopStudio.Desktop.Views;
public partial class UpdatePlanWindow : Window
{
    public IReadOnlyCollection<string> SelectedFields => new[] {
        TitleBox.IsChecked==true?"title":"",DescriptionBox.IsChecked==true?"description":"",
        VisibilityBox.IsChecked==true?"visibility":"",PreviewBox.IsChecked==true?"previewfile":"",
        ChangeNoteBox.IsChecked==true?"changenote":"" }.Where(s=>s.Length>0).ToArray();
    public UpdatePlanWindow(SteamAccount account, AppProfile app, ModProject mod, ReleaseChannel channel,
        RemoteWorkshopItem remote, PackageSnapshot package)
    {
        InitializeComponent();
        TargetText.Text=$"Upload-Konto: {account.UserName} · Web-SteamID64: {account.VerifiedWebSteamId64}\n"+
            $"Spiel: {app.Name} ({app.AppId}) · Kanal: {channel} · bestehende Ziel-ID: {remote.Id}\n"+
            $"Steam geändert: {remote.UpdatedAt?.ToLocalTime().ToString()??"Nicht ermittelbar"}";
        PackageText.Text=$"{package.Files.Count:N0} Dateien · {Format.Bytes(package.Bytes)}\n"+
            string.Join("\n",package.Plan.Sources.Select(s=>s.SourceRoot+" → "+s.TargetPrefix));
        VersionDiff.Text=$"Version: Steam-Hinweis aus Beschreibung: {remote.VersionLabel} · Lokal: {(string.IsNullOrWhiteSpace(mod.Version)?"nicht gesetzt":mod.Version)}. "+
            "Steam besitzt kein separates Versionsfeld. Für eine Änderung die Beschreibung auswählen oder eine Änderungsnotiz mit {version} verwenden.";
        TitleDiff.Text=$"Steam: {remote.Title}\nLokal: {mod.Name}";
        DescriptionDiff.Text=$"Steam: {remote.Description.Length} Zeichen · Lokal: {mod.Description.Length} Zeichen. Volltext im Mod-Editor und in den Workshop-Details prüfen.";
        VisibilityDiff.Text=$"Steam: {remote.Visibility?.ToString()??"unbekannt"} · Lokal: {mod.Visibility}";
        PreviewDiff.Text=$"Steam: {remote.PreviewUrl}\nLokal: {(mod.PreviewFile.Length==0?"kein Bild gewählt":mod.PreviewFile)}";
        PreviewBox.IsEnabled=mod.PreviewFile.Length>0;
        NoteText.Text=mod.ChangeNote.Length==0?"Keine neue Notiz eingegeben.":
            mod.ChangeNote.Replace("{version}",mod.Version).Replace("{date}",DateTime.Now.ToString("yyyy-MM-dd"));
        ChangeNoteBox.IsEnabled=mod.ChangeNote.Length>0;
    }
    private void Accept_Click(object sender,RoutedEventArgs e) => DialogResult=true;
}
