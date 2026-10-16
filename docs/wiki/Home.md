# 🧰 Workshop Studio – Handbuch

Workshop Studio verwaltet lokale Mod-Projekte und eigene Steam-Workshop-Inhalte unter Windows. Es findet bestehende Veröffentlichungen, ordnet ihnen lokale Einzelmods zu und veröffentlicht neue oder geänderte Pakete unter der passenden Workshop-ID.

Dieses Handbuch beschreibt Version **1.3.0**. Die Anwendung verwendet C#/.NET 10 und WPF. Das portable Windows-Paket enthält die .NET-Laufzeit. Die Oberfläche bietet ein Anthrazit-Orange-Design, ein helles Theme und eine eigene Akzentfarbe.

**Neu in 1.3.0:** Lokale Metadaten werden beim Prüfen und Veröffentlichen automatisch verglichen. Vor jedem Bestandsupdate muss eine geprüfte Sicherung des bisherigen Steam-Inhalts entstehen. [Änderungen und Prüfstand](Entwicklung-und-Pruefstatus)

![Dashboard mit isolierten Beispieldaten](https://raw.githubusercontent.com/ZUMBYTE-AppSolution/WorkshopStudio/main/docs/images/dashboard.png)

## 🧭 Die passenden Einstiege

| Ziel | Anleitung |
|---|---|
| Zum ersten Mal starten | [Schnellstart](Schnellstart) und [Installation und Updates](Installation-und-Updates) |
| Bereits vorhandene Workshop-Mods übernehmen | [Steam und Konten](Steam-und-Konten), danach [Workshop-Bibliothek](Workshop-Bibliothek) |
| Lokale Mod-Ordner angeben | [Lokale Mods](Lokale-Mods) |
| Beschreibung, Bild, Version und Changelog verstehen | [Metadaten und Changelog](Metadaten-und-Changelog) |
| Neu veröffentlichen oder bestehende IDs aktualisieren | [Veröffentlichen](Veroeffentlichen) |
| Einen früheren Stand sichern oder wiederherstellen | [Backups und Wiederherstellung](Backups-und-Wiederherstellung) |
| Änderungen automatisch veröffentlichen | [Automatisierung](Automatisierung) |
| Collections und Serverzeilen nutzen | [Collections](Collections) |
| Original-Konfigurationen kopieren | [Config und Befehle](Config-und-Befehle) |
| Ablage, Darstellung und Grenzen anpassen | [Einstellungen und Daten](Einstellungen-und-Daten) |
| Ein Problem eingrenzen | [Fehlerbehebung](Fehlerbehebung) |
| Direkt kopierbare Dateien ausprobieren | [Beispiele für Configs und Metadaten](Beispiele) |

## 🚀 So läuft ein Update ab

**① 📂 Ordner wählen → ② 🔗 Workshop-ID zuordnen → ③ 📝 Änderungen erkennen → ④ 💾 Steam-Stand sichern → ⑤ ☁️ freigegebenes Update übertragen**

Die folgenden Seiten zeigen echte Ausschnitte aus einem WPF-Oberflächentest. Namen, IDs und Configwerte in den Abbildungen sind Demodaten. Orange Häkchen im Metadatenplan zeigen tatsächlich vorausgewählte Änderungen.

## 🖥️ Die Bereiche der App

- **Dashboard:** Spiele, Projektzahlen, Schnellaktionen und letzte Vorgänge. Spiele stehen bei passender Breite zu zweit nebeneinander, mit skaliertem Bild links.
- **Steam & Konten:** Upload-Konto, Websitzung, SteamCMD und Login-Protokoll.
- **Meine Workshop-Inhalte:** Eigene Veröffentlichungen ermitteln, prüfen und importieren.
- **Spiele & AppIDs:** Spieleprofile, Vorlagen und gemeinsame Ausschlüsse.
- **Mods & Resources:** Einzelmods, Pfade, Metadaten, Vorlagen und Paketvorschau.
- **Collections:** Eigene Projekte und externe IDs organisieren.
- **Veröffentlichungen:** Warteschlange, Zielkanal und Start/Abbruch.
- **Automatisierung:** Dateiüberwachung, Ruhezeit und Zeitpläne.
- **Logs & Verlauf:** Ergebnis, Protokoll, Wiederholung und Sicherung vor dem Update.
- **Einstellungen:** Darstellung, Publishing, Ablage und Tool-Updates.
- **Hilfe & Support:** Lokale Dokumentation, Wiki und Systemdiagnose.

Steam-Funktionen hängen von Konto, Spiel und Sichtbarkeit ab. Ein Metadatenimport erzeugt noch keine lokale Arbeitskopie. Das Tool schreibt keine Gameserver-Konfiguration und startet keinen Server neu.

[Downloads](https://github.com/ZUMBYTE-AppSolution/WorkshopStudio/releases/latest) · [Changelog](https://github.com/ZUMBYTE-AppSolution/WorkshopStudio/blob/main/CHANGELOG.md) · [Probleme melden](https://github.com/ZUMBYTE-AppSolution/WorkshopStudio/issues)
