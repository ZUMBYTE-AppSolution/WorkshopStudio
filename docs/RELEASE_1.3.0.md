## Neu in Workshop Studio 1.3.0

- **Automatischer Metadatenvergleich:** Titel, Beschreibung, lokale Versionshinweise, aktuelle Changelog-Notiz und ausdrücklich gewähltes Vorschaubild werden geprüft. Erkannte Änderungen gehen bei freigegebenen automatischen Jobs mit; der manuelle Update-Plan wählt sie passend vor.
- **Keine doppelten Release-Notizen:** Die aktuelle Changelog-Sektion und bereits veröffentlichte Notizen werden je Test-/Live-Kanal wiedererkannt. Fehlende lokale Werte löschen keine Steam-Angaben. Live-Sichtbarkeit besitzt ein eigenes Opt-in.
- **Verpflichtendes Backup vor Bestandsupdates:** Der bisherige Inhalt wird von Steam heruntergeladen und zusammen mit Metadaten, Beschreibung, erreichbarem Änderungsverlauf und Vorschaubild als ZIP mit SHA-256-Dateiliste gesichert. Ohne erfolgreich geprüftes Backup startet kein Bestandsupload.
- **Sicherung im Verlauf öffnen:** Ein neuer Knopf führt direkt zur Sicherung des ausgewählten Updatevorgangs. Die Wiederherstellung ist im Wiki beschrieben.
- **Umfassendes [GitHub-Wiki](https://github.com/ZUMBYTE-AppSolution/WorkshopStudio/wiki):** 16 Themenbereiche mit Schnellstart, Ordnern, Steam-Konten, Metadaten, Veröffentlichungen, Automation, Backups, Configs und Fehlerhilfe. Auch direkt aus der App erreichbar.

Windows-Build, Selbsttests und WPF-Test ausgeführt. Ein echter eigener Steam-Inhalt wurde lesend heruntergeladen und gesichert; sein Remote-Zeitstand und der Nutzer-Workspace blieben unverändert. Es wurde kein Steam-Upload gestartet. Details und offene Abnahmen: [Lieferbericht](https://github.com/ZUMBYTE-AppSolution/WorkshopStudio/blob/main/docs/DELIVERY_1.3.0.md).
