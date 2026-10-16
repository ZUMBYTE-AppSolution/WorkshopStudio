# ⚙️ Automatisierung

Studio kann freigegebene Mod-Projekte nach Dateiänderungen oder einem Zeitplan prüfen und veröffentlichen. Das Programm muss geöffnet bleiben. Nach einem Import oder einer neuen lokalen Zuordnung ist Auto-Publish zunächst ausgeschaltet.

## Einmal bewusst einrichten

![Einstellungen für automatische Veröffentlichung und Ruhezeit](https://raw.githubusercontent.com/ZUMBYTE-AppSolution/WorkshopStudio/main/docs/images/wiki/automation-settings.png)

*Der Demozustand zeigt ausgeschaltete Automation. Die drei Freigaben „Automatische Uploads“, „Live-Automation“ und Auto-Publish pro Mod werden bewusst gesetzt.*

1. Die betreffende Mod manuell prüfen und ihre richtige Test-/Live-ID kontrollieren.
2. Im Mod-Editor **Auto-Publish für diese Mod erlauben** aktivieren.
3. Unter **Automatisierung** automatische Uploads aktivieren und den Zielkanal auswählen.
4. Für Live zusätzlich **Live-Automation ausdrücklich erlauben** aktivieren.
5. Dateiüberwachung und/oder einen Zeitplan einstellen und speichern. Studio zeigt beim Aktivieren eine konkrete Upload-Bestätigung.

Die automatische Metadatenerkennung unter **Einstellungen → Publishing** ist davon unabhängig. Sie entscheidet, welche erkannten Angaben bei einem bereits erlaubten Upload mitgehen. Sie aktiviert selbst keine automatischen Veröffentlichungen.

## Dateiüberwachung

**Ordneränderungen überwachen** beobachtet genau die aufgelösten ausgewählten Mod-Stämme. Ausschlüsse gelten auch für die Überwachung. Bei aktiver Metadatenerkennung werden zusätzlich zugehörige `workshop.txt`-, Versions- und Changelog-Dateien sowie das ausdrücklich gewählte Vorschaubild beobachtet. Ein Metadatenordner wird dadurch nicht vollständig zum Upload-Paket.

Die Ruhezeit verhindert, dass Studio direkt nach jedem einzelnen Speichervorgang ein Paket erstellt. Erst nach Ablauf der eingestellten Sekunden seit der letzten relevanten Änderung wird verarbeitet. Umbenennungen werden für alten und neuen Pfad berücksichtigt. Bei einem Überwachungsfehler wird ein erneutes Prüfen vorgemerkt.

## Zeitpläne

| Modus | Verhalten |
|---|---|
| Manual | Kein Zeitplan; Dateiüberwachung kann dennoch aktiv sein. |
| Interval | In den eingestellten Minutenabständen prüfen. |
| Daily | Einmal pro lokalem Windows-Tag ab der eingestellten Uhrzeit prüfen. |

Bei aktivem **Unveränderte Pakete überspringen** führt eine Prüfung ohne Änderungen zu keiner erneuten Übertragung. Neue Metadaten können dennoch einen Updatevorgang auslösen.

## Verhalten eines automatischen Jobs

Konto, Besitzer, AppID und Remote-Baseline werden wie bei einem manuellen Auftrag geprüft. Die erkannten Metadatenfelder werden automatisch gewählt. Vor einem Bestandsupload entsteht verpflichtend das vollständige Item-Backup. Alle Entscheidungen und Ergebnisse stehen im Verlauf/Protokoll.

Ein konkurrierender Steam-Stand, ein fehlgeschlagenes Backup oder ein ungeklärter Erstupload wird nicht durch eine automatische Neuanlage umgangen. Eine aktive Dialoginteraktion oder ein anderer laufender Vorgang unterbricht die Bearbeitung weiterer automatischer Auslöser. SteamCMD arbeitet exklusiv; parallele Uploads derselben Installation werden verhindert.

Beim Import eines kompletten Workspace-Exports bleiben übernommene Automationen ausgeschaltet. Nach Kontrolle der Pfade und IDs können sie wieder bewusst eingerichtet werden.
