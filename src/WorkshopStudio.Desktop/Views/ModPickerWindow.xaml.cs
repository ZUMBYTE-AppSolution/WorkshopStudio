using System.Windows;
using System.Windows.Controls;
using WorkshopStudio.Core;
using WorkshopStudio.Desktop.Services;
namespace WorkshopStudio.Desktop.Views;
public partial class ModPickerWindow : Window
{
    public IReadOnlyList<ModSourceCandidate> Selected { get; private set; } = [];
    public ModPickerWindow(IReadOnlyList<ModSourceCandidate> candidates, bool multiple, IReadOnlyList<string>? existing = null)
    {
        InitializeComponent(); Choices.ItemsSource = candidates;
        Choices.SelectionMode = multiple ? SelectionMode.Multiple : SelectionMode.Single;
        Heading.Text = multiple ? "Mods für dieses Paket auswählen" : "Genau eine Mod auswählen";
        Hint.Text = multiple
            ? "Gewünschte Mods einzeln anklicken. Nur diese gespeicherte Auswahl gehört zum Paket; später hinzugefügte Nachbarordner werden nicht automatisch aufgenommen."
            : $"{candidates.Count} Mods gefunden. Wähle die Mod, die dieses Workshop-Profil veröffentlichen soll.";
        if (multiple && existing is not null)
            foreach (var candidate in candidates.Where(c => existing.Contains(c.FolderName))) Choices.SelectedItems.Add(candidate);
    }
    private void Accept_Click(object sender, RoutedEventArgs e)
    {
        Selected = Choices.SelectedItems.Cast<ModSourceCandidate>().ToArray();
        if (Selected.Count == 0) { DesktopDialogs.Info("Bitte mindestens eine Mod auswählen."); return; }
        DialogResult = true;
    }
}
