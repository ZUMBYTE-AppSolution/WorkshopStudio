# 🚀 Schnellstart

## Bereits veröffentlichte eigene Mods verwalten

1. Das aktuelle Windows-ZIP aus den [Releases](https://github.com/ZUMBYTE-AppSolution/WorkshopStudio/releases/latest) vollständig in einen beschreibbaren Ordner entpacken und `WorkshopStudio.exe` starten.
2. Unter **Steam & Konten** ein Konto anlegen. Der Loginname ist der Steam-Kontoname. Anschließend SteamCMD anmelden und die Steam-Websitzung öffnen. Beide Sitzungen müssen zum selben Konto gehören.
3. Unter **Meine Workshop-Inhalte → Workshop-Inhalte ermitteln** den eigenen Bestand lesen. Den Scanbericht prüfen und bestätigte Einträge lokal importieren. Vorhandene Workshop-IDs werden beibehalten.
4. Unter **Mods & Resources → Mehrere Mods auswählen** den Stammordner deiner lokalen Mods wählen. Studio sucht darin nach einzelnen Zomboid-Mods. Pro Treffer das vorgeschlagene Profil und dessen Workshop-ID kontrollieren.
5. Eine zugeordnete Mod öffnen. **Upload-Inhalt prüfen** zeigt die tatsächlich ausgewählten Dateien. Unter **Config / Befehle anzeigen** lassen sich vorhandene Config-Dateien als Originaltext kopieren.
6. Unter **Einstellungen → Publishing** ist die automatische Metadatenerkennung standardmäßig aktiv. Ein lokales `workshop.txt`, Versionsdateien und die aktuelle Changelog-Notiz werden beim nächsten Prüfen berücksichtigt. Einzelheiten stehen unter [Metadaten und Changelog](Metadaten-und-Changelog).
7. Die gewünschte Mod für **Live** einreihen, wenn die bestehende Live-ID aktualisiert werden soll. **Test** verwendet eine getrennte, private Workshop-ID.
8. Die Warteschlange starten. Bei einem manuellen Bestandsupdate zeigt Studio den Update-Plan mit automatisch ausgewählten Feldänderungen. Nach Freigabe wird zuerst der alte Steam-Inhalt heruntergeladen und gesichert. Erst ein erfolgreich geprüftes Backup erlaubt den Upload.
9. Ergebnis und Remote-Nachprüfung im Verlauf lesen. Die Mod anschließend mit deinem Spielbuild testen.

**Wichtig für die Zuordnung:** Technische Mod-ID und Workshop-ID sind unterschiedliche Dinge. Eine Workshop-Veröffentlichung kann mehrere technische Mod-IDs enthalten. Jede Live-/Test-Workshop-ID darf nur einem lokalen Profil zugeordnet sein.

## Eine neue Mod veröffentlichen

Unter **Mods & Resources → Mod hinzufügen** ein Profil erstellen oder eine passende Vorlage verwenden. Den einzelnen Mod-Stamm auswählen, Titel und Inhalte prüfen und die Workshop-ID leer lassen. Beim erfolgreichen Erst-Upload übernimmt Studio die von Steam zurückgegebene ID. Unklare Ergebnisse werden gesperrt, damit ein erneuter Versuch nicht versehentlich ein Duplikat erzeugt.

Für einen ersten Versuch den Kanal **Test** verwenden. Er erzeugt einen privaten Eintrag mit eigener ID. Ein späterer Live-Erstupload erzeugt einen zweiten Eintrag. Wenn bereits ein gewünschter Live-Eintrag existiert, diesen zuerst importieren und zuordnen.

## Möglichst automatisch arbeiten

Nach erfolgreicher Einrichtung kannst du pro Mod **Auto-Publish** erlauben und anschließend die [Automatisierung](Automatisierung) aktivieren. Für Live sind zusätzlich Zielkanal und die ausdrückliche Live-Freigabe erforderlich. Die automatische Metadatenentscheidung und das verpflichtende Backup gelten auch dann. Studio muss geöffnet bleiben.
