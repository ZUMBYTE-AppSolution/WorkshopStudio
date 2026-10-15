using System.Windows;
using WorkshopStudio.Core;
using WorkshopStudio.Desktop.Services;

namespace WorkshopStudio.Desktop.Views;

public sealed record LocalModMatchTarget(string Label, Guid? ModId = null, bool CreateNew = false);
public sealed record LocalModAssignment(ModSourceCandidate Candidate, LocalModMatchTarget Target);

public sealed class LocalModMatchRow
{
    public ModSourceCandidate Candidate { get; }
    public string MatchReason { get; }
    public IReadOnlyList<LocalModMatchTarget> Choices { get; }
    public LocalModMatchTarget Selected { get; set; }

    public LocalModMatchRow(LocalModSuggestion suggestion, IReadOnlyList<ModProject> profiles)
    {
        Candidate = suggestion.Candidate;
        MatchReason = suggestion.Reason;
        var choices = new List<LocalModMatchTarget> { new("Überspringen") };
        if (Candidate.ModIds.Count > 0)
        {
            choices.Add(new("Neues lokales Profil (Workshop-ID 0)", CreateNew: true));
            choices.AddRange(profiles.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
                .Select(p => new LocalModMatchTarget(p.Name +
                    (p.WorkshopId > 0 ? " · Live-ID " + p.WorkshopId : " · ohne Live-ID"), p.Id)));
        }
        Choices = choices;
        Selected = choices.FirstOrDefault(c => c.ModId == suggestion.ExistingModId && c.ModId is not null) ?? choices[0];
    }
}

public partial class LocalModMatchWindow : Window
{
    private readonly LocalModMatchRow[] _rows;
    public IReadOnlyList<LocalModAssignment> Assignments { get; private set; } = [];

    public LocalModMatchWindow(AppProfile app, LocalModCatalogResult catalog,
        IReadOnlyList<LocalModSuggestion> suggestions, IReadOnlyList<ModProject> profiles)
    {
        InitializeComponent();
        _rows = suggestions.Select(s => new LocalModMatchRow(s, profiles)).ToArray();
        ModRows.ItemsSource = _rows;
        Summary.Text = $"{catalog.Candidates.Count} " +
            (catalog.Candidates.Count == 1 ? "einzelne Mod" : "einzelne Mods") + $" für {app.Label} gefunden. " +
            $"{_rows.Count(r => r.Selected.ModId is not null)} eindeutige Treffer vorgeschlagen." +
            (catalog.Warnings.Count == 0 ? "" : $" {catalog.Warnings.Count} Ordner übersprungen; bitte den gewählten Stammordner prüfen.");
    }

    private void Accept_Click(object sender, RoutedEventArgs e)
    {
        var chosen = _rows.Where(r => r.Selected.ModId is not null || r.Selected.CreateNew).ToArray();
        if (chosen.Length == 0) { DesktopDialogs.Info("Bitte mindestens eine Mod einem Profil zuordnen oder als neues Profil auswählen."); return; }
        var duplicates = chosen.Where(r => r.Selected.ModId is not null)
            .GroupBy(r => r.Selected.ModId!.Value).FirstOrDefault(g => g.Count() > 1);
        if (duplicates is not null) { DesktopDialogs.Info("Ein vorhandenes Profil kann nur einer lokalen Mod zugeordnet werden."); return; }
        Assignments = chosen.Select(r => new LocalModAssignment(r.Candidate, r.Selected)).ToArray();
        DialogResult = true;
    }
}
