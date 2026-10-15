namespace WorkshopStudio.Core;

public static class CollectionExporter
{
    public static IReadOnlyList<ulong> ResolveIds(WorkshopCollection collection, IEnumerable<ModProject> mods, ReleaseChannel channel)
    {
        var indexed=mods.ToDictionary(m=>m.Id);
        var ids=new List<ulong>();
        foreach (var id in collection.ModProjectIds) {
            if (!indexed.TryGetValue(id,out var mod) || mod.AppProfileId!=collection.AppProfileId) throw new InvalidDataException("Collection-Mod fehlt oder gehört zu einer anderen App.");
            var published=mod.GetWorkshopId(channel);
            if (published==0) throw new InvalidDataException($"{mod.Name} hat noch keine {channel}-Workshop-ID.");
            ids.Add(published);
        }
        ids.AddRange(collection.ExternalWorkshopIds.Where(x=>x>0));
        return ids.Distinct().ToArray();
    }
    public static string ZomboidIni(WorkshopCollection collection,IEnumerable<ModProject> mods,ReleaseChannel channel)
    {
        var all=mods.ToArray();
        var ids=ResolveIds(collection,all,channel);
        var modIds=collection.ModProjectIds.Select(id=>all.Single(m=>m.Id==id)).SelectMany(m=>m.ModIds.Split([';','\r','\n'],StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries)).Distinct().ToArray();
        if (collection.ModProjectIds.Any(id=>string.IsNullOrWhiteSpace(all.Single(m=>m.Id==id).ModIds))) throw new InvalidDataException("Für jede eigene Zomboid-Mod muss die Mod-ID aus mod.info eingetragen sein.");
        return "# Ausschnitt für die Server-INI. Nicht automatisch auf einen Server angewendet.\n"+
            "# Mod-IDs externer Workshop-Einträge ggf. manuell ergänzen.\n"+
            "WorkshopItems="+string.Join(';',ids)+"\nMods="+string.Join(';',modIds)+"\n";
    }
}
