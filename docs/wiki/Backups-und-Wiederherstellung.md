# 💾 Backups und Wiederherstellung

## Verpflichtende Sicherung vor Bestandsupdates

Seit 1.3.0 muss vor dem Ändern einer vorhandenen Workshop-ID eine geprüfte Sicherung entstehen. Das gilt für manuelle und automatische Jobs sowie für Test und Live. Schlägt Download, Bildabruf, Archivierung oder Prüfsummenprüfung fehl, wird der Updatevorgang vor dem Upload gestoppt.

Studio lädt den aktuell veröffentlichten Inhalt mit SteamCMD unter dem verifizierten Konto und den genauen App-/Workshop-IDs. Ein Prozess-Exitcode allein reicht nicht: Der bestätigte Download, Zielpfad und die Kontoidentität werden geprüft. Studio kopiert ausschließlich den Ordner dieses Items. Die eigentliche lokale Entwicklungsquelle und die Spielinstallation werden dabei nicht gesichert oder hochgeladen.

Der Steam-Zeitstand wird nach der Sicherung erneut geprüft. Bei einer zwischenzeitlichen Änderung bleibt das Backup erhalten und der Upload wird gestoppt. Steam bietet hier keine gemeinsame atomare Download-/Update-Transaktion.

## Inhalt des Sicherungs-ZIPs

| Pfad | Inhalt |
|---|---|
| `content/` | Der vor dem Update von Steam heruntergeladene Paketinhalt. |
| `metadata/steam-item.json` | Gelesene Remote-Metadaten, Eigentümer, IDs, Zeitstand und Feldstatus. |
| `metadata/local-profile-before-update.json` | Das lokale Mod-Profil vor dem Upload. |
| `metadata/description.txt` | Die bisherige Steam-Beschreibung als Text. |
| `metadata/changelog.txt` | Erreichbare Änderungsnotizen mit Abrufstatus; derzeit erste öffentliche Seite. |
| `metadata/preview…` | Das bisherige Hauptvorschaubild, sofern Steam eines liefert. |
| `backup-manifest.json` | Dateiliste mit Größen und SHA-256-Prüfsummen. |
| `RESTORE.txt` | Kurzanleitung zum bewussten Rücksetzen. |

Jede manifestierte Datei wird nach dem Schreiben nochmals aus dem ZIP gelesen und geprüft. Eine zusätzliche `.sha256`-Datei schützt das komplette Archiv. Ein abgebrochener `.partial`-Stand gilt nicht als fertiges Backup.

## Ablage und Öffnen

```text
%LOCALAPPDATA%\WorkshopStudio\backups\workshop-items\<Workshop-ID>\<Live|Test>\before-….zip
```

Unter **Logs & Verlauf** einen Vorgang auswählen und **Sicherung vor Update** anklicken. Der zugehörige Ordner wird geöffnet. **Backups öffnen** in den Einstellungen zeigt die gesamte Sicherungsablage. Die Zahl der behaltenen Sicherungen ist einstellbar. Ältere erkannte Sicherungen werden erst nach einem erfolgreich erstellten neuen Backup entsprechend der Aufbewahrung entfernt.

![Aktionsleiste im Verlauf mit Sicherung vor Update](https://raw.githubusercontent.com/ZUMBYTE-AppSolution/WorkshopStudio/main/docs/images/wiki/backup-actions.png)

**① Vorgang in der Liste markieren → ② „Sicherung vor Update“ → ③ ZIP und passende `.sha256`-Datei im geöffneten Ordner prüfen.**

Die bisherigen optionalen **Paket-Backups nach erfolgreichem Upload** bleiben zusätzlich verfügbar. Sie enthalten den neu hochgeladenen lokalen Paketstand und liegen pro Modprofil/Kanal in einer anderen Unterstruktur. Ein Workspace-Backup enthält wiederum Profile und Einstellungen, nicht die vollständigen Mod-Dateien.

## Einen alten Stand wiederherstellen

1. Studio-Automation für die betreffende Mod zunächst ausschalten.
2. Das gewünschte Backup vollständig in einen neuen Ordner entpacken. Den bestehenden Entwicklungsordner nicht ungeprüft überschreiben.
3. Workshop-ID, AppID, Kanal und Zeitstand anhand von `metadata/steam-item.json` prüfen.
4. Im vorhandenen Mod-Profil die gewünschte Einzelmod unter `content/` als Quelle auswählen. Enthielt die Veröffentlichung mehrere Mods, die beabsichtigte Mehrmod-Auswahl explizit zusammenstellen.
5. Frühere Beschreibung, Titel, Sichtbarkeit und Bild anhand des Metadatenordners bewusst im Editor übernehmen. Das heruntergeladene Paket enthält den veröffentlichten Stand; nicht veröffentlichte Entwicklungsdateien können daraus nicht rekonstruiert werden.
6. Dieselbe Workshop-ID beibehalten, Paketvorschau prüfen und den Rücksetz-Upload gezielt freigeben. Auch dieser Vorgang erzeugt zuerst eine Sicherung des dann aktuellen Steam-Stands.

Alte Steam-Änderungsnotizen lassen sich mit diesem Verfahren nicht entfernen oder historisch zurückdrehen. Bei Bedarf eine neue Notiz zum Rücksetzen schreiben. Tags, Abhängigkeiten und Collection-Mitgliedschaften im Steam-Webeditor kontrollieren.

Backups sind lokale, unverschlüsselte Dateien. Private Workshop-Pakete und enthaltene Configwerte deshalb vor Weitergabe prüfen. Ein Backup ist kein automatischer Steam-Rollback und kein Ersatz für eine zusätzliche Sicherung deines Entwicklungsprojekts.
