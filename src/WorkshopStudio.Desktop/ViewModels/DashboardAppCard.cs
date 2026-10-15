using WorkshopStudio.Core;
namespace WorkshopStudio.Desktop.ViewModels;
public sealed record DashboardAppCard(AppProfile Profile, int ModCount, int CollectionCount)
{
    public string ContentSummary => $"{ModCount} Mods · {CollectionCount} Collections";
}
