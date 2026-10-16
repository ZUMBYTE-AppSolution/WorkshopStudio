# ✅ Entwicklung und Prüfstatus

Dieses Wiki beschreibt den Funktionsstand **1.3.0 vom 16. Oktober 2026**. Der Code ist ein fortgeführtes C#/.NET-10-WPF-Projekt. Die Aufteilung besteht aus Core für Modelle/Steam/Quellen/Pakete, Desktop für WPF sowie einer lokal ausführbaren Selbsttest-Suite.

## Verfügbare Nachweise

- Windows-Release-Build mit dem .NET-10-SDK.
- Selbsttests für Loginprotokoll und echte lokale Kindprozesse, Paketgrenzen, B41/B42-Strukturen, lokale Zuordnung, Workspace-Erhalt, Metadatenerkennung, Changelog-Deduplizierung, Item-Backups und Tool-Updater.
- Separate Quellprüfungen für Theme, Paketierung und Login-Verträge.
- WPF-Laufzeittest mit isoliertem Workspace: Dashboard, Bibliothek, maximierte Fensterknöpfe, lokale Mod-Zuordnung, markierbarer/kopierbarer Config-Originaltext und automatisch vorausgewählte Updatefelder.
- Lesender Live-Backuptest an einem bestätigten eigenen Eintrag: SteamCMD-Download mit passender Kontoidentität, 13 Inhaltsdateien, Metadaten und Vorschaubild gesichert; Datei-SHA-256 erneut geprüft. Steam-Zeitstand unverändert und Nutzer-Workspace bytegleich.

Die endgültigen Zähler und Build-Nachweise stehen im [Lieferbericht 1.3.0](https://github.com/ZUMBYTE-AppSolution/WorkshopStudio/blob/main/docs/DELIVERY_1.3.0.md) und in [GitHub Actions](https://github.com/ZUMBYTE-AppSolution/WorkshopStudio/actions).

## Noch erforderliche Abnahmen

Ein echter privater Erst-/Bestandsupload mit anschließendem Spieltest wurde während dieser Entwicklung nicht gestartet. Damit ist auch ein vollständiger Wiederherstellungs-Upload aus einem Backup noch nicht live abgenommen. Ein Update zwischen zwei öffentlichen installierten Programmversionen, frische Steam-Guard-Anmeldung sowie zusätzliche WPF-Prüfungen bei 125/150 % DPI und im hellen Theme bleiben gesondert zu prüfen. Spielabhängige Workshop-Rechte sind nicht für jede AppID nachgewiesen.

## Selbst bauen

Windows, .NET 10 SDK und Python 3 für die Quellprüfungen bereitstellen. `WorkshopStudio.sln` in Visual Studio öffnen oder aus dem Projektordner ausführen:

```powershell
./scripts/Build.ps1
./scripts/Package-Source.ps1
```

Das Buildskript restauriert Abhängigkeiten, kompiliert, führt die netzwerkfreien Selbsttests aus und erstellt das portable Paket samt Versionsmarker. Ein Versionstag passend zu `Directory.Build.props` startet die Release-Action. Diese veröffentlicht Windows-ZIP, vollständiges Quellprojekt und `SHA256SUMS.txt`.

Das Wiki liegt zusätzlich unter `docs/wiki` im Quellprojekt und wird in das separate GitHub-Wiki-Repository übertragen. Dadurch gehören Bedienhinweise zum überprüfbaren Projektstand. [Changelog](https://github.com/ZUMBYTE-AppSolution/WorkshopStudio/blob/main/CHANGELOG.md) · [Sicherheit](https://github.com/ZUMBYTE-AppSolution/WorkshopStudio/blob/main/docs/SECURITY.md)

## 📚 Technische Quellen

[Valve: Workshop-Implementierung und SteamCMD-VDF](https://partner.steamgames.com/doc/features/workshop/implementation?l=english) · [Valve: öffentliche Workshop-Detailabfrage](https://partner.steamgames.com/doc/webapi/isteamremotestorage?l=english) · [GitHub: Wiki-Seiten lokal bearbeiten](https://docs.github.com/en/communities/documenting-your-project-with-wikis/adding-or-editing-wiki-pages)
