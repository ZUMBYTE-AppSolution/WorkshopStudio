# 🎛️ Einstellungen und Daten

## Darstellung und Bedienung

Dark/Light-Theme, Akzentfarbe, UI-Skalierung und maximierter Start sind einstellbar. Die Standardoberfläche verwendet Anthrazit und Orange. Navigation und Aktionen nutzen lokal eingebettete Iconify-Symbole. Die Fensterknöpfe bleiben im maximierten Hauptfenster innerhalb des nutzbaren Monitorbereichs.

Die Kopfzeilensuche filtert die jeweiligen Listen nach den angebotenen Namen und IDs. Strg+N öffnet eine neue Mod, F5 prüft Änderungen und Strg+S speichert den Einstellungsentwurf. Einstellungen werden erst mit **Speichern** übernommen; ein Validierungsfehler muss vorher korrigiert werden.

## Publishing und Schutzgrenzen

| Einstellung | Wirkung |
|---|---|
| Standard-Zielkanal | Vorgabe für Test oder Live; beide besitzen getrennte IDs. |
| Unveränderte Pakete überspringen | Inhalt und Metadatenzustand prüfen, bevor erneut übertragen wird. |
| Automatische Metadatenerkennung | Lokale Angaben lesen und geänderte sichere Felder für Updates auswählen. |
| Warteschlange bei Fehler stoppen | Nach einem fehlgeschlagenen Auftrag keine weiteren Jobs desselben Durchlaufs starten. |
| Steam-Seite nach manuellem Upload öffnen | Erfolgreichen manuellen Eintrag im Browser zeigen. |
| Strenge mod.info-Prüfung | Technische IDs in allen relevanten Metadatendateien prüfen. Die Spielordner-/Einzelmod-Grenze gilt immer. |
| Paketgröße und Dateianzahl | Harte Grenzen für Paketierung; die Größenbegrenzung gilt auch für Item-Backups. |
| Globale Ausschlüsse | Mit Spiel- und Modregeln kombinierte Dateifilter. |
| Typische Geheimnisse ausschließen | Beispielsweise `.env`, Schlüsseldateien, `.ssh` und `.aws` aus neuen Uploadpaketen ausschließen. |
| Erfolgreiche Paketordner behalten | Staging nach Erfolg für Diagnose erhalten. |
| Paket-Backup nach Erfolg | Zusätzlich zur verpflichtenden alten Item-Sicherung den neuen Uploadstand archivieren. |
| Backup-Aufbewahrung | Anzahl erkannter Sicherungen je Item/Mod und Kanal begrenzen. |
| Verlauf-Aufbewahrung | Zahl der in der App sichtbaren abgeschlossenen Jobs begrenzen. Logdateien werden dadurch nicht automatisch gelöscht. |

Filter verstehen `*.tmp`, `Ordner/`, `Unterordner/*.log` und `**/*.tmp`. Es handelt sich nicht um die vollständige gitignore-Syntax. Ein Filter ersetzt keine eigene Kontrolle der Paketvorschau.

## Netzwerk, Anmeldung und Automation

SteamCMD-Pfad, Auto-Login, Websitzung, Metadatenabruf beim Start, Store-Sprache/-Land, Anfragezeitlimit, Upload-/Login-Zeitlimit und Bildcachealter steuern die jeweiligen Abläufe. Die Config-Dateigröße und der Workshop-Scan besitzen eigene Grenzen. Dateiüberwachung, Ruhezeit und Zeitpläne stehen unter [Automatisierung](Automatisierung). Der feste GitHub-Tool-Updater ist unter [Installation und Updates](Installation-und-Updates) beschrieben.

## Datenablage

```text
%LOCALAPPDATA%\WorkshopStudio\
  workspace.json       Profile, IDs, Einstellungen und Verlauf
  workspace.json.bak   vorheriger gespeicherter Workspace
  backups\             Workspace-, Item- und Paket-Backups
  staging\             getrennte Upload-Pakete
  logs\                Login-/Uploadprotokolle
  cache\               Steam-Metadaten und Bilder
```

WebView2 verwaltet zusätzlich pro Konto ein Sitzungsprofil. Gespeicherte Passwörter liegen im Windows Credential Manager. Ein Workspace-Export enthält keine Passwort- oder Sitzungstokenfelder, kann aber Kontonamen, lokale Pfade und selbst eingegebene Notizen enthalten.

Vor Migration, Übernahme lokaler Zuordnungen und Ersetzen des Arbeitsbereichs werden Sicherungen angelegt. Ein Workspace-Import ersetzt nach bewusster Bestätigung die lokalen Profildaten; übernommene Automationen und automatische Anmeldungen bleiben ausgeschaltet. Mod-Dateien werden vom Workspace-Export nicht mitgesichert.

**Paketablage bereinigen** löscht nur von Studio markierte Staging-Ordner. Der Benutzer-Datenordner soll nicht als pauschaler Reparaturschritt gelöscht werden.
