# 1.0.3 · Design und Einzelmod-Auswahl · 16.09.2026

- WPF-Hauptfenster und Dashboard an die Aufteilung des früheren Entwurfs angepasst: Anthrazit, orange aktive Navigation, Kopfzeilensuche, Symbol-KPIs, kompakte Steam-Spielkarten, Schnellaktionen, rechter Aktivitätsbereich und responsive Umordnung.
- Vorhandenes Icon, Login-Code, Light-Theme und individuelle Akzentfarben erhalten. Frühere Standardakzentfarbe wird auf das neue Referenzorange migriert.
- Direkte Einzelmod-Auswahl statt verpflichtend manuell vorbereiteter Contents-Struktur. Mod-Auswahl bei übergeordneten Ordnern; Mehrmod-Pakete nur mit expliziter gespeicherter Auswahl.
- Automatische Paketstruktur `mods/<Modname>/`, B41/B42-Metadaten, vollständiger B42-Mod-Stamm mit gemeinsamen Assets und leeren Ordnern.
- Unabhängige Schutzprüfung für Spiel-/Steam-Wurzeln, unklare Auswahl, Links/Junctions und erforderliche Paketmetadaten nach Anwendung der Dateifilter.
- Upload-Vorschau mit vollständiger filterbarer Dateiliste, Größen, SHA-256, Quellen/Zielen, Verzeichnissen, VDF und JSON-Export. Kein Upload durch die Vorschau.
- Dateiwatcher an exakt dieselbe Quellenauswahl wie Paketierung gebunden.
- Neue Mod-Vorlagen erzeugen einen direkt verwendbaren Mod-Stamm. Bestehende Workspace-Pfade und Daten bleiben bestehen.
- 35 zusätzliche C#-Regressionstests für die Paketierung geschrieben, hier nicht ausgeführt. Offline-Quellprüfungen ausgeführt, aber weiterhin kein .NET-Build, WPF-Rendering- oder Steam-End-to-End-Test.

# 1.0.2 · Login und App-Icon · 16.09.2026

- Windows-ConPTY-Anbindung anstelle reiner stdin/stdout-Umleitung beim integrierten SteamCMD-Login.
- Chunk-/ANSI-feste Prompt-Erkennung, Guard-Abfragen auf beiden Ausgabekanälen, keine zeitbasierte Unterdrückung schneller Folgeprompts.
- Mobile Bestätigungsaufforderung separat, Abbruch und Timeout getrennt, Kindprozess-Abbau und exklusive Ausführung.
- Temporäre Passwörter aus dem Kontoeditor bleiben bis zum Programmende nutzbar; persistentes Speichern weiter nur per Opt-in.
- Geführter Einstieg bei fehlendem Upload-Konto oder fehlendem SteamCMD, beschreibbares Installationsverzeichnis prüfen.
- Konkrete Loginfehler und bereinigtes Protokoll direkt in der Kontoseite, Kopieren und Export.
- WebView2-Profile pro Upload-Konto, Erkennung des sichtbaren Steam-Kontomenüs, Lade-/Laufzeitdiagnose und Standardbrowser-Alternative.
- Originale Konsole blockiert parallele Vorgänge; nach quit erneute Sitzungsprüfung.
- Multi-Resolution-ICO und PNG integriert, vorhandene Anthrazit-Orange-Palette unverändert.
- Zusätzliche Protokoll- und lokale Prozess-Selbsttests; keine echten Steam-Zugangsdaten in Tests.
- Nicht hier kompiliert oder gegen Steam ausgeführt. Siehe docs/DELIVERY_CHECKS.md.

# Änderungen

## 1.0.1: Theme-Fix

- Explizite Styles für Hauptfenster, Editorfenster und Seiten. Gespeichertes Theme wird vor dem Erstellen des Hauptfensters angewendet.
- Theme-Vorlagen für zuvor unvollständig abgedeckte WPF-Steuerelemente, einschließlich Tabs, Passwortfeldern, Dropdowns, Tabellenköpfen und Scrollbereichen.
- Eigene Hinweis-, Fehler- und Bestätigungsdialoge; standardmäßig ablehnende Bestätigung und native Notfallanzeige bei Dialogfehlern.
- Explizite Kontextmenüs, thematisierte Tooltips und unveränderte Passwort-Vertraulichkeit.
- Windows-11-Titelleisten und WebView2-Initialhintergrund an das Theme angebunden.
- Lesbare Beschriftungen durch vererbte Vordergrundfarben und Kontrastwahl bei eigenen Akzentfarben.
- Build-Paketname übernimmt die Projektversion, statt fest 1.0.0 zu verwenden.
- 15 Offline-Strukturprüfungen hinzugefügt und ausgeführt. Acht zusätzliche C#-Quellverträge für Theme-Regressionsschutz hinzugefügt, hier nicht ausgeführt.

Bestehende Anthrazit-Orange-Palette und Seitenstruktur bleiben erhalten. Keine Änderungen an Datenmodell, Speicherpfaden, SteamCMD-/Workshop-Veröffentlichung, Auto-Publish-Regeln oder Zugangsdatenverwaltung.

Lieferstand: Quellprojekt. Kein bestätigter Windows-Build, kein Windows-Renderingtest und kein Steam-End-to-End-Test im Lieferumfeld.
# 1.1.0 · Eigene Workshop-Bibliothek und sichere Bestandsupdates · 15.10.2026

- Angemeldete Steam-Websitzung ermittelt eigene Dateien und Collections über alle Profilseiten; öffentlicher Detailabgleich bestätigt Besitzer und AppID, Remote-Metadaten werden lokal gesichert.
- SteamCMD-Login liest die SteamID64 aus `+info`; Web- und Upload-Konto müssen vor Veröffentlichungen übereinstimmen.
- Bestehende eigene Workshop-IDs bleiben beim Import erhalten. Neue Profile besitzen zunächst keine lokale Quelle und kein Auto-Publish. Ein Eintrag ohne öffentliche Eigentümerbestätigung bleibt nur lesbar.
- Bestandsupdates prüfen ID, Besitzer, AppID und Remote-Zeitstand. Ein Dialog zeigt Ziel, Quellordner und Paketgröße; Metadatenfelder sind zunächst abgewählt. Nach einem Upload erfolgt eine zusätzliche öffentliche Detailprüfung.
- Lokales Workshop-Schema erkennt `mod.info`, `workshop.txt`, Versionsdateien, Bild und Changelog in der gewählten Mod. Öffentliche Steam-Änderungsnotizen werden bei Bedarf gelesen.
- Update-Dialog zeigt lokalen Versionswert gegen den lesbaren Steam-Hinweis und die expandierte Änderungsnotiz. Eine Versionszeile in der Beschreibung wird nur bei ausdrücklich ausgewählter Beschreibungsaktualisierung übertragen.
- Config-Fenster zeigt Originaldateien als markierbaren Text, statisch erkannte Optionen und getrennte Serverzeilen. Auswahl und Gesamttext lassen sich kopieren.
- Schema-1-Arbeitsbereich wird vor Migration exakt gesichert; Import bestehender Daten erhält Einstellungen und Quellen. Windows-Build, 114 Selbsttests und WPF-Oberflächentest ausgeführt.
- 48 lokal eingebettete Iconify-`mdi`-Vektoren ersetzen die alten Windows-Symbolglyphen in Navigation, Dashboard, Dialogen und Aktionsknöpfen. Fenstertasten für Minimieren, Maximieren/Wiederherstellen und rotes Schließen besitzen eigene Symbole; ihre Darstellung und Funktion wurden im WPF-Laufzeittest geprüft.
- Kein echter Workshop-Upload im Rahmen dieser Lieferung.

# 1.1.1 · Lokale Mod-Ordner zuordnen · 15.10.2026

- **Mehrere Mods auswählen** öffnet eine begrenzte, lesende Unterordnersuche. Sie erkennt B41- und B42-Einzelmods, auch in verschachtelten Projekt- und `Contents/mods`-Ordnern.
- Jede gefundene Einzelmod erhält eine eigene Zeile. Bereits zugeordnete Pfade, technische Mod-IDs und eindeutige kompatible Namen schlagen bestehende Profile vor. Mehrdeutige Treffer bleiben zunächst übersprungen.
- Zuordnungen werden vor dem Speichern einzeln bestätigt. Bestehende Workshop-IDs und Metadaten bleiben erhalten; neu angelegte Profile starten ohne Workshop-ID. Doppelte Ziele, unpassende Mod-IDs und wartende Uploadjobs werden abgewiesen.
- Der Quellpfad wird auf den einzelnen Mod-Stamm gesetzt; Auto-Publish wird für zugeordnete Profile deaktiviert. Vor der Änderung wird der bisherige Workspace gesichert. Die Ordnersuche veröffentlicht nichts.
- Die bisherige gemeinsame Paketauswahl bleibt im Mod-Editor als **Mehrmod-Paket zusammenstellen** erhalten.
- Neun neue C#-Tests für Erkennung, Vorschläge und sichere Zuordnung; insgesamt 123 Selbsttests.

# 1.2.0 · GitHub-Release, integrierter Updater und Dashboard-Liste · 15.10.2026

- Dashboard-Spiele als vertikale Liste mit proportional skaliertem Bild links und Spielinformationen rechts.
- Fest integrierte Prüfung des offiziellen stabilen GitHub-Releases. Nach Zustimmung lädt Studio das Windows-ZIP, gleicht Größe und SHA-256 mit `SHA256SUMS.txt` ab, entpackt in einen isolierten Ordner und installiert nach erneuter Zustimmung beim Neustart.
- Update-Helfer sichert vorhandene Programmdateien und nimmt sie bei Kopierfehlern zurück. Der Workspace im Benutzerprofil bleibt getrennt. Manipulierte URLs, falsche Versionen und gefährliche ZIP-Pfade werden abgewiesen.
- GitHub Actions für Windows-CI und taggesteuerten Release-Build mit Quellprojekt, portablem Windows-Paket und Prüfsummen.
- Acht neue Updater-Selbsttests; insgesamt 131 Tests.

