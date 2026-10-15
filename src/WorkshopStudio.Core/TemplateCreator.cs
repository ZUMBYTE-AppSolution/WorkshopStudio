using System.Text.RegularExpressions;

namespace WorkshopStudio.Core;

public static class TemplateCreator
{
    public static string Create(string parent,string id,string name,GameTemplate template)
    {
        if(!Regex.IsMatch(id,@"^[A-Za-z0-9_-]{1,80}$"))throw new InvalidDataException("Mod-ID: nur Buchstaben, Ziffern, Unterstrich und Bindestrich.");
        var root=Path.Combine(parent,id);
        if(Directory.Exists(root))throw new IOException("Zielordner existiert bereits. Vorhandene Dateien werden nicht überschrieben.");
        if(template==GameTemplate.Generic){Directory.CreateDirectory(root);File.WriteAllText(Path.Combine(root,"README.txt"),"Hier den vom Spiel erwarteten Workshop-Inhalt ablegen.\n");return root;}
        var mod=root;
        var versionRoot=template==GameTemplate.ProjectZomboid42?Path.Combine(mod,"42"):mod;
        foreach(var layer in new[]{"client","server","shared"})Directory.CreateDirectory(Path.Combine(versionRoot,"media","lua",layer));
        if(template==GameTemplate.ProjectZomboid42){Directory.CreateDirectory(Path.Combine(mod,"common"));File.WriteAllText(Path.Combine(mod,"common","README.txt"),"Gemeinsame Assets für die versionierten Mod-Inhalte. Dieser Ordner wird mit veröffentlicht.\n");}
        File.WriteAllText(Path.Combine(versionRoot,"mod.info"),$"name={name.Replace("\r","").Replace("\n","")}\nid={id}\ndescription=RP-Mod\n");
        File.WriteAllText(Path.Combine(versionRoot,"media","lua","shared",id+".lua"),$"-- Einstiegspunkt für {id}. Keine privaten Zugangsdaten im Workshop veröffentlichen.\nlocal module = {{ version = \"1.0.0\" }}\nreturn module\n");
        File.WriteAllText(Path.Combine(root,"README.md"),"# "+name+"\n\nIn Workshop Studio diesen Mod-Ordner direkt auswählen. mods/<Modname>/ wird beim Paketieren automatisch erstellt.\nDie Vorlage ist ein struktureller Ausgangspunkt; Spiellogik separat im Zielbuild testen.\n");
        return mod;
    }
}
