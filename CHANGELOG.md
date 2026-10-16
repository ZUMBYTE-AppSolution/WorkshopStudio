# Changelog

Änderungen an Workshop Studio, nach Versionen geordnet. Das Format folgt [Keep a Changelog](https://keepachangelog.com/de/1.1.0/).

## [Unreleased]

Noch keine öffentlich freigegebenen Funktionsänderungen.

## [1.3.0] - 2026-10-16

### Hinzugefügt

- Automatische Erkennung und Auswahl geänderter Titel, Beschreibungen, Versionshinweise, Release-Notizen und ausdrücklich gewählter Vorschaubilder.
- Vergleich mit akzeptierten Metadaten-Baselines, aktuelle Changelog-Sektion und getrennte Notiz-/Bildprüfsummen für Test und Live.
- Verpflichtende geprüfte Sicherung des bisherigen Steam-Item-Inhalts und seiner Metadaten vor jedem Bestandsupdate. Ein Backupfehler stoppt vor dem Upload.
- Backup-Zugriff im Verlauf und eigenes Opt-in für automatische Live-Sichtbarkeitsänderungen.
- Umfassendes [GitHub-Wiki](https://github.com/ZUMBYTE-AppSolution/WorkshopStudio/wiki) mit 16 Themenbereichen, Sidebar und Wiederherstellungsanleitung; in der App verlinkt.

### Korrigiert

- Automatische Jobs können nun erkannte Metadaten mit übertragen.
- Zugehörige Metadatendateien außerhalb des eigentlichen Mod-Stamms werden überwacht, ohne das Upload-Paket um Nachbarordner zu erweitern.
- Eigene erfolgreiche Uploads aktualisieren die Remote-Baseline für nachfolgende Jobs. Ein erneuter Import erhält tatsächliche lokale Editoränderungen.
- Mehrzeilige `description=`-Angaben und die Workshop-Projektebene über `Contents/mods` werden erkannt.
- Config-Kopieren wiederholt kurz bei gesperrter Windows-Zwischenablage und prüft den übernommenen Text.

### Geprüft

- 162 Selbsttests, Windows-Build, Quellprüfungen und WPF-Updateplan ausgeführt.
- Echter lesender Steam-Download mit geprüftem Backup aus 13 Inhaltsdateien; Steam-Zeitstand und Nutzer-Workspace unverändert.
- Kein echter Steam-Upload oder Wiederherstellungs-Upload. [Prüfbericht und offene Punkte](docs/DELIVERY_1.3.0.md).

## [1.2.0] - 2026-10-15

### Hinzugefügt

- Integrierter Updater für offizielle GitHub-Releases mit SHA-256-Prüfung, sicherer ZIP-Entpackung, Bestätigung vor Installation und Rücknahme bei Kopierfehlern.
- GitHub Actions für Windows-CI und taggesteuerte Releases mit portablem Windows-Paket, vollständigem Quellprojekt und Prüfsummen.
- [Release v1.2.0](https://github.com/ZUMBYTE-AppSolution/WorkshopStudio/releases/tag/v1.2.0) und [Prüfbericht](docs/DELIVERY_1.2.0.md).

### Geändert

- Dashboard-Spiele als niedrigere Listeneinträge mit skaliertem Bild links; bei ausreichender Breite stehen zwei nebeneinander.
- Titelleiste an den nutzbaren Monitorbereich angepasst, damit Minimieren, Maximieren und das rote Schließen auch im maximierten Fenster sichtbar bleiben.

### Geprüft

- Windows-.NET-10-Build ohne Warnungen und Fehler; 131 Selbsttests bestanden.
- WPF-Oberflächentest mit zwei Spielprofilen, maximierter Titelleiste, lokaler Mod-Zuordnung und kopierbarem Config-Originaltext bestanden.
- Ein echter Steam-Upload und der vollständige Updatezyklus zwischen zwei öffentlichen Versionen stehen noch aus.

## [1.1.1] - 2026-10-15

### Hinzugefügt

- **Mehrere Mods auswählen** durchsucht einen lokalen Stammordner nach einzelnen B41-/B42-Mods, auch in verschachtelten Projektordnern.
- Vorschläge für vorhandene Profile anhand gespeicherter Pfade, technischer Mod-IDs und eindeutiger Namen; unklare Treffer bleiben zur manuellen Prüfung offen.
- Sichere Übernahme der einzelnen Mod-Stämme als Quellpfad mit Workspace-Sicherung. Bestehende Workshop-IDs bleiben erhalten; neue Profile starten ohne Workshop-ID, zugeordnete Profile ohne Auto-Publish.

### Geändert

- Die bewusste gemeinsame Paketauswahl heißt **Mehrmod-Paket zusammenstellen**.

### Geprüft

- 123 Selbsttests bestanden, einschließlich lokaler Suche, Zuordnung und Paketgrenzen.

## [1.1.0] - 2026-10-15

### Hinzugefügt

- Eigene Steam-Workshop-Mods und Collections über die angemeldete Websitzung ermitteln, Besitzer und AppID öffentlich abgleichen und Metadaten lokal importieren.
- Bestehende Workshop-IDs bei Bestandsupdates erhalten; Quelle, Ziel, Version und gewählte Metadatenänderungen im Update-Dialog prüfen.
- Lokales Workshop-Schema für Bild, Version und Changelog sowie ein Config-Fenster mit markierbarem Originaltext und Kopierfunktionen.
- 48 lokal eingebettete Iconify-Symbole für Navigation, Aktionen und Fensterknöpfe.

### Geprüft

- 114 Selbsttests und ein WPF-Oberflächentest bestanden. Vorhandene Steam-Web- und SteamCMD-Sitzungen wurden lesend derselben SteamID64 zugeordnet; kein Steam-Upload.

## [1.0.3] - 2026-09-16

- Anthrazit-Orange-Oberfläche mit kategorisierter Sidebar, Dashboard, Schnellaktionen und Aktivitätsbereich überarbeitet.
- Einzelmods direkt auswählbar; B41/B42-Inhalte werden nur aus der gewählten Mod paketiert. Spielinstallation, ungewählte Nachbarordner, Links und Junctions werden ausgeschlossen.
- Paketvorschau mit Dateiliste, Quellen, Zielpfaden, Größen und SHA-256 ergänzt.

## [1.0.2] - 2026-09-16

- SteamCMD-Login auf Windows-ConPTY umgestellt; Passwort- und Guard-Abfragen, Abbruch, Timeout und Protokoll verbessert.
- Getrennte Steam-Webprofile, Weblogin-Diagnose und mehrstufiges App-Icon ergänzt.

## [1.0.1] - 2026-09-16

- Fenster- und Seitenstile, Dialoge und Steuerelementvorlagen vereinheitlicht. Gespeichertes Theme wird vor dem ersten Fenster angewendet.
- Titelzeile und WebView2-Startfläche an das Anthrazit-Orange-Design angebunden.
