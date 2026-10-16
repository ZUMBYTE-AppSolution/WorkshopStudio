# Lieferung 1.3.0 · Automatische Metadaten und Sicherungen vor Updates

Die bestehende WPF-App erkennt lokale Metadaten vor Prüfung und Upload. Eigene Editorwerte werden von unveränderten importierten Angaben unterschieden; verfügbare lokale Dateiangaben ergänzen den akzeptierten Stand. Die Entscheidung je Feld wird protokolliert und im manuellen Update-Plan vorausgewählt. Automatische Jobs wenden sie im Rahmen der bereits freigegebenen Automation an.

Beschreibungen aus mehrzeiligem `workshop.txt`, Versionsdateien und aktuellen Changelog-Sektionen werden gelesen. Prüfsummen verhindern wiederholte identische Änderungsnotizen und Bildübertragungen. Fehlende Werte werden nicht zum Löschauftrag. Live-Sichtbarkeit wird nur bei eigenem Opt-in automatisch geändert. Tags und Abhängigkeiten bleiben im Steam-Webeditor.

Vor jedem Bestandsupload lädt SteamCMD den bisherigen Inhalt der genauen App-/Workshop-ID. Studio bestätigt Downloadpfad und Konto, archiviert nur diesen Inhalt samt Remote-Metadaten und Bild und prüft jede manifestierte Datei erneut aus dem ZIP. Fehler stoppen den Upload. Nach der Sicherung wird der Remote-Stand nochmals auf zwischenzeitliche Änderungen geprüft. Nach erfolgreichen Uploads wird die Remote-Baseline aktualisiert, damit spätere erlaubte automatische Updates nicht an der eigenen vorherigen Änderung scheitern.

## Ausgeführt

- Windows/.NET-10-Release-Build; 162/162 C#-Selbsttests bestanden.
- 15 Theme-, 20 Paketierungs- und 22 Login-Quellprüfungen bestanden.
- WPF-Test mit isoliertem Workspace: Dashboard, Bibliothek, maximierte Iconify-Fensterknöpfe, lokale Mod-Zuordnung, Config-Auswahl/Kopieren und automatische Auswahl geänderter Beschreibung/Notiz im Update-Plan.
- Lesender Live-Backuptest am finalen Stand an einem bestätigten eigenen Steam-Eintrag: Download unter passender SteamID, 13 Inhaltsdateien, 59.365 ZIP-Bytes, gesicherte Metadaten und Vorschau, SHA-256-Dateiprüfung. Remote-Zeitstand unverändert; Nutzer-Workspace vor/nach dem Test bytegleich. Keine Uploadargumente ausgeführt.
- [Wiki-Quellen](wiki/Home.md) für 16 Themenbereiche samt Sidebar und Footer im vollständigen Projekt enthalten.
- Acht geprüfte WPF-Bildausschnitte/-ansichten im Wiki, Emojis, kurze Bedienfolgen sowie kopierbare Config-/Metadatenbeispiele. Wiki-Prüfung: 18 Markdown-Dateien und 70 lokale Link-/Bildziele aufgelöst.

## Datenkompatibilität

Die neuen optionalen Felder bleiben mit Workspace-Schema 2 kompatibel. Fehlende Metadaten-Baselines werden aus dem vorhandenen bestätigten Bestand ergänzt; IDs, Quellen, Konten, Themes und Einstellungen bleiben erhalten. Details: [Migration](MIGRATION_1.3.0.md).

## Noch offen

Ein echter privater Erst-/Bestandsupload und ein Rücksetz-Upload aus einem Backup wurden nicht durchgeführt. Ebenso fehlen die Abnahme im Spiel, ein vollständiger Tool-Updatezyklus zwischen zwei öffentlichen installierten Versionen sowie zusätzliche DPI-/Hellmodus-Prüfungen. Der echte Mod-Stammordner des Nutzers muss noch gewählt und seine Zuordnung kontrolliert werden. Workshop-Downloadrechte anderer Spiele und eingeschränkter Inhalte können abweichen; ein nicht sicherbarer Eintrag bleibt für Updates gesperrt. Der gespeicherte Steam-Änderungsverlauf umfasst derzeit die erste erreichbare öffentliche Seite und ist kein rücksetzbarer vollständiger Steam-Historienstand.
