# Workshop Studio
## Steam-Workshop-Publisher für Windows · Version 1.2.0


**Lokale Mods 1.1.1:** Unter **Mods & Resources → Mehrere Mods auswählen** einen Stammordner angeben. Studio durchsucht begrenzt dessen Unterordner nach einzelnen B41-/B42-Mods, schlägt eindeutige Zuordnungen anhand technischer Mod-ID oder Namen vor und zeigt jedes Zielprofil mit seiner Workshop-ID. In der Vorschau lassen sich Treffer bestätigen, unklare Mods überspringen oder als neue lokale Profile ohne Workshop-ID anlegen. Erst beim Übernehmen werden die Quellpfade gesichert; Auto-Publish ist für diese Profile aus. Die frühere gemeinsame Paketauswahl bleibt im Mod-Editor als **Mehrmod-Paket zusammenstellen** erhalten.

**Version 1.2.0:** Dashboard-Spiele stehen in kompakten Listenfeldern mit proportional skaliertem Bild links und Informationen rechts; bei ausreichender Breite passen zwei Spiele nebeneinander. Die eigenen Fensterknöpfe bleiben auch maximiert im sichtbaren Bereich. Der Tool-Updater prüft fest die offiziellen GitHub-Releases, lädt das Windows-ZIP nach Bestätigung, kontrolliert dessen SHA-256-Prüfsumme und installiert es nach einem weiteren Bestätigen beim Neustart. Die Release-Action baut und testet das Windows-Paket bei einem passenden Versionstag.

**Workshop-Bibliothek 1.1.0:** Die angemeldete Steam-Websitzung ermittelt eigene Mods und Collections über alle Profilseiten. Öffentliche Detaildaten werden mit Besitzer und AppID abgeglichen, Metadaten lokal importiert und bestehende Workshop-IDs für Updates bewahrt. Ein gezielter Update-Dialog zeigt Paketquelle und Metadatenänderungen. Config-Dateien aus ausgewählten lokalen Mods erscheinen als markierbarer Originaltext mit Kopierknöpfen. Die Anthrazit-Orange-Oberfläche und das App-Icon bleiben erhalten; Navigation, Dashboard, Aktionsknöpfe und Fenstertasten verwenden lokal eingebettete Iconify-Symbole.

**Design und Einzelmods 1.0.3:** Die reale WPF-Oberfläche wurde an die Aufteilung des früheren Bildentwurfs angepasst. Einzelmods werden direkt ausgewählt und automatisch als `mods/<Modname>/` verpackt. Spielverzeichnisse sind gesperrt; mehrere Mods benötigen eine ausdrückliche Auswahl. Anleitung: [Design und Mod-Auswahl](docs/DESIGN_AND_MOD_SCOPE_1.0.3.md).

**Login und App-Icon 1.0.2:** Windows-Pseudokonsole für SteamCMD, überarbeitete Passwort-/Guard-Abfragen, Login-Protokoll und getrennte Webprofile. Einrichtung: [Login-Fix](docs/LOGIN_FIX_1.0.2.md). In 1.1.0 wurden vorhandene Web- und SteamCMD-Sitzungen tatsächlich gelesen und anhand ihrer SteamID64 abgeglichen.

**Theme-Korrektur 1.0.1:** Explizite Fenster-/Seitenstile, vervollständigte Steuerelementvorlagen und eigene Dialoge. Die Anthrazit-Orange-Gestaltung bleibt erhalten. Installation, Grenzen und Prüfschritte: [Theme-Fix](docs/THEME_FIX_1.0.1.md). Der vorhandene Datenordner muss nicht gelöscht werden.

C# / .NET 10 / WPF. Ein gemeinsamer Arbeitsplatz für mehrere Steam-AppIDs, eigene Mods, Collections und kontrollierte Workshop-Veröffentlichungen. Der Ausgangspunkt ist Alfreds Project-Zomboid-RP-Projekt; die App-Verwaltung ist nicht auf Zomboid beschränkt.

**Lieferstatus:** Unter Windows mit .NET SDK 10 gebaut, 131 C#-Selbsttests bestanden und die WPF-Oberfläche samt neuer Zuordnungsvorschau mit isoliertem Testarbeitsbereich gerendert. Die vorhandene Steam-Websitzung und SteamCMD-Sitzung wurden lesend auf dieselbe SteamID64 geprüft. 28 eigene Workshop-Einträge wurden gefunden; 27 konnten über öffentliche Details bestätigt und 19 Mods sowie 8 Collections lokal importiert werden. Ein Eintrag bleibt mangels öffentlicher Detaildaten nur lesbar. Es wurde **kein Steam-Upload** ausgeführt. Weitere Abnahmen stehen in [Bekannte Grenzen](docs/KNOWN_LIMITATIONS.md).

Prüfprotokoll und offene Punkte: [Lieferbericht 1.2.0](docs/DELIVERY_1.2.0.md).

## Öffnen und bauen

1. Windows 11, Visual Studio 2026 mit **.NET-Desktopentwicklung** und **.NET 10 SDK** bereitstellen.
2. `WorkshopStudio.sln` öffnen. NuGet-Restore zulassen. **WorkshopStudio.Desktop** als Startprojekt wählen und mit F5 starten.
3. Alternativ `Build.cmd` ausführen. Das Skript restauriert Pakete, baut die Solution, führt die netzwerkfreien Selbsttests aus und erstellt erst dann ein portables Release-Paket.

Ausgabe nach erfolgreichem lokalen Build:

```text
artifacts/WorkshopStudio-win-x64/WorkshopStudio.exe
artifacts/WorkshopStudio-1.2.0-win-x64.zip
artifacts/WorkshopStudio-1.2.0-win-x64.zip.sha256
```

Die Standardausgabe enthält die .NET-Laufzeit. Kein globales SDK auf späteren Zielrechnern erforderlich. **WebView2 Evergreen Runtime** wird für eingebettete Steam-Seiten weiterhin benötigt. Die Runtime ist nicht im Quellarchiv enthalten. Installation siehe `docs/SOURCES.md`. Keine Administratorrechte für den normalen App-Betrieb vorgesehen. Unsigned Build: Eine Authenticode-Signatur ist nicht enthalten.

`Start.cmd` startet direkt aus dem Quellprojekt. `scripts/Build.ps1 -FrameworkDependent` erstellt eine kleinere Ausgabe, die eine passende .NET-Desktop-Laufzeit voraussetzt. `-Runtime win-arm64` ist als Buildziel vorgesehen, aber ebenso lokal zu prüfen; SteamCMD selbst ist eine Valve-Komponente mit eigenen Plattformvoraussetzungen.

## Oberfläche

Dunkles Anthrazit, orange Akzente, kompakte Kopfzeile, anpassbare Farbe und helles Theme. Seitenleiste mit drei Kategorien und elf Bereichen:

| Kategorie | Seiten |
|---|---|
| Arbeitsbereich | Dashboard, Steam & Konten, Meine Workshop-Inhalte, Spiele & AppIDs, Mods & Resources, Collections |
| Veröffentlichung | Veröffentlichungen, Automatisierung, Logs & Verlauf |
| Verwaltung | Einstellungen, Hilfe & System |

Dashboard mit vier Symbol-KPIs, kompakten Steam-Spielkarten, sechs Schnellaktionen und seitlichem Aktivitätsbereich. Bei schmalen Fenstern wandert der Aktivitätsbereich unter die Spielkarten. Kennzahlen stammen aus dem Arbeitsbereich; die Veröffentlichungszahl bezieht sich auf erfolgreiche Einträge im aufbewahrten Verlauf. Keine erfundenen Beispieldaten. Project Zomboid mit AppID **108600** ist als erstes App-Profil vorkonfiguriert. Bilder und Namen werden online geladen und lokal gecacht; ohne verfügbare Store-Daten bleibt das Profil manuell bearbeitbar.

## Erster eigener Upload

1. Unter **Einstellungen → SteamCMD** Valve-SteamCMD installieren oder eine vorhandene `steamcmd.exe` auswählen.
2. Unter **Steam & Konten** ein Upload-Konto mit dem echten Steam-Login-Namen anlegen und aktivieren. Optional das Passwort im Windows-Anmeldedatenspeicher ablegen.
3. **Für Uploads anmelden**. Eine Steam-Guard-Abfrage bestätigen. Bei Bedarf **Interaktive Anmeldung** öffnen, in der originalen SteamCMD-Konsole anmelden und anschließend `quit` eingeben. Studio prüft die Sitzung danach erneut. Danach unter **Meine Workshop-Inhalte** die Websitzung anmelden und ermitteln; vor Uploads werden beide SteamIDs abgeglichen.
4. Eine App anlegen oder Project Zomboid bearbeiten. Unter **Mods & Resources → Mod hinzufügen** das App-Profil auswählen. **Mod-Ordner wählen** öffnet die Auswahl der eigenen Einzelmod, beispielsweise `D:\RP\mods\rp-core`. Der gewählte Mod-Ordner enthält direkt `mod.info` (B41) oder `42/mod.info` und ggf. `common/` (B42). Beim Wählen eines übergeordneten `mods/`-Ordners mit mehreren Mods erscheint eine Auswahl. **Neue Mod-Struktur anlegen** erzeugt eine direkt verwendbare Einzelmod, keinen manuell zu pflegenden Contents-Wrapper.
5. Speichern und **Upload-Inhalt prüfen**. Die lokale Vorschau zeigt alle Quelldaten-Zuordnungen, Zielpfade, Dateianzahl, Größe, SHA-256 und VDF, ohne Upload. Studio erzeugt `content/mods/rp-core/...` in seiner Staging-Ablage. Du musst keinen `Content/`- oder `Contents/`-Ordner im Spiel anlegen. Ein Spiel-Installationsverzeichnis wird abgewiesen. Für ein bewusstes gemeinsames Paket im Mod-Editor **Mehrmod-Paket zusammenstellen** verwenden; ungewählte oder später hinzugefügte Nachbarordner bleiben ausgeschlossen.
6. Kanal **Test** wählen, einreihen und die Warteschlange ausdrücklich starten. Test verwendet eine eigene, private Workshop-ID. Den veröffentlichten Inhalt und die Ladefähigkeit im passenden Spielbuild prüfen.
7. Erst danach **Live** verwenden. Live besitzt eine eigene Workshop-ID und verwendet die gewählte Sichtbarkeit.

Neue Einträge starten mit Workshop-ID `0`. Die vom Uploader zurückgeschriebene ID wird gespeichert; weitere Uploads aktualisieren diesen Eintrag. Nach unklarem Erst-Upload ist eine erneute Neuanlage gesperrt, bis du den Steam-Stand geprüft hast. Bereits auf Steam verarbeitete Uploads kann die Stopptaste nicht rückgängig machen.

## Vorhandene eigene Workshop-Inhalte

Unter **Steam & Konten** zuerst das Upload-Konto per SteamCMD anmelden. Unter **Meine Workshop-Inhalte → Workshop-Inhalte ermitteln** im eingebetteten Steam-Browser anmelden und den Scan starten. Studio liest alle Seiten des Bereichs „Meine Dateien“ und die Collections dieses Profils. Die SteamID64 der Websitzung muss zur SteamCMD-Identität passen, bevor ein Upload beginnt. **Alle bestätigten importieren** legt fehlende lokale Mod- und Collection-Profile mit ihren bestehenden Workshop-IDs an. Importierte Mods haben anfangs keinen lokalen Quellordner und Auto-Publish ist aus.

Für einen Bestandsupdate im Mod-Editor genau den lokalen Mod-Ordner wählen. **Metadaten erkennen** liest `mod.info`, `workshop.txt`, Versionsdateien, ein lokales Bild und `CHANGELOG` nur aus der gewählten Mod und ihrem unmittelbaren Workshop-Wrapper. Der angezeigte Änderungsverlauf lässt sich kopieren; leere Felder können nach Bestätigung übernommen werden. Ein bestehendes Steam-Bild wird in der Bibliothek gezeigt, aber nicht ohne Auswahl durch ein lokales Bild ersetzt. **Details / Schema** liest zusätzlich öffentlich sichtbare Steam-Änderungsnotizen.

Vor jedem Update prüft Studio die Workshop-ID, den Besitzer, die AppID und den seit dem Scan bekannten Stand erneut. Im Update-Dialog sind Änderungen an Titel, Beschreibung, Sichtbarkeit, Bild und Änderungsnotiz zunächst ausgeschaltet. Ein Inhaltsupdate verwendet nur den ausdrücklich gewählten Quellordner und erhält die bestehende ID. Für einen Eintrag ohne bestätigbare öffentliche Detaildaten bleibt der Import in ein schreibbares Mod-Profil gesperrt; er wird in der Bibliothek angezeigt.

**Config / Befehle anzeigen** öffnet vorhandene Konfigurationsdateien der gewählten lokalen Mod als echten, frei markierbaren Text. Die Tabs trennen Originaldateien, statisch erkannte Optionen und ergänzende Zomboid-Serverzeilen. Ohne lokalen Quellordner kann Studio die Config eines remote veröffentlichten Mods nicht aus dessen Dateien lesen.

## Collections: lokal und auf Steam

Lokale Collections ordnen eigene Mod-Projekte und fremde Workshop-IDs einem Spiel zu. Sie unterstützen gemeinsamen Upload eigener Mods, Export, Zwischenablage und für Zomboid einen `WorkshopItems=`/`Mods=`-INI-Ausschnitt. Externe Mods werden **nicht** als eigene Inhalte erneut hochgeladen.

**Auf Steam anlegen** öffnet den originalen Steam-Collection-Editor innerhalb der App. Dort kannst du die echte Collection erstellen und die erzeugte ID anschließend übernehmen. Import und Abgleich lesen öffentlich abrufbare Collections über Steam. Lokale Änderungen ersetzen nicht automatisch die Mitgliedschaften auf Steam. Tags, Abhängigkeiten und spielabhängige Spezialfelder bearbeitest du ebenfalls auf der originalen Steam-Seite. Es gibt hier absichtlich keine erfundene universelle Collection-Schreib-API.

## Automatische Updates

Manueller Workflow: Inhalte ändern → einreihen → veröffentlichen. Optional beobachten Dateisystem-Watcher ausschließlich die aufgelösten, ausgewählten Mod-Quellordner; nach einer konfigurierbaren Ruhezeit wird der Inhalt erneut paketiert. Zeitpläne unterstützen Intervall oder tägliche lokale Uhrzeit. Es zählen SHA-256-Fingerprints des tatsächlich gepackten Inhalts und der Upload-Metadaten, nicht nur Dateizeitstempel.

Automation ist standardmäßig aus. Sie verlangt die globale Aktivierung und zusätzlich **Auto-Publish** pro Mod. Automatisches Live-Publishing benötigt eine weitere Freigabe. Die Anwendung muss geöffnet bleiben; dies ist kein Windows-Dienst und kein Cloud-Scheduler. Ein unklarer Erst-Upload wird nicht blind wiederholt.

Unter **Einstellungen → Tool-Updates** prüft **Jetzt prüfen** das neueste stabile Release im offiziellen GitHub-Repository. Auf Wunsch prüft Studio auch beim Start. Ein neues Paket wird erst nach Bestätigung geladen, gegen `SHA256SUMS.txt` geprüft und nach einer weiteren Bestätigung beim Neustart installiert. Der Updater sichert bisherige Programmdateien für eine Rücknahme bei Kopierfehlern; der Workspace liegt getrennt im Benutzerprofil. Das alte Manifestfeld bleibt in vorhandenen Einstellungen gespeichert, wird für den eingebauten GitHub-Updater aber nicht mehr benötigt.

## Speicherung und Sicherheit

```text
%LOCALAPPDATA%/WorkshopStudio/
├── workspace.json / workspace.json.bak
├── cache/        Steam-Bilder
├── staging/      erzeugte Upload-Snapshots
├── backups/      Paket-ZIPs und lokale Workspace-Sicherungen
├── logs/         Veröffentlichungsprotokolle
├── steamcmd/     verwaltete Valve-Installation samt deren Sitzungsdaten
└── webview/      optional gespeichertes Steam-Webprofil
```

Passwörter stehen nicht in `workspace.json`, VDF-Dateien oder Prozessargumenten. Optionaler Passwortspeicher ist der Windows Credential Manager des angemeldeten Benutzers. SteamCMD verwaltet eigene Sitzungsdateien; WebView2 verwaltet separat Cookies. Weblogin ist kein SteamCMD-Login. Steam Guard, Session-Ablauf oder Änderungen am Konto können eine neue Anmeldung erzwingen.

Keine Zugangsdaten mit Mods veröffentlichen. Vordefinierte Dateifilter erkennen typische `.env`-Dateien, private Schlüssel und Credential-Ordner, aber nicht jedes Geheimnis in Lua, JSON oder Text. Paket-Backups enthalten deinen Quellinhalt unverschlüsselt. Ein Workspace-Export enthält keine Passwörter, Cookies oder SteamCMD-Tokens, aber Namen, Pfade, Einstellungen und Workshop-IDs.

## Projektaufbau

```text
src/WorkshopStudio.Core/       Modelle, Speicher, Paketierung, SteamCMD, Steam-HTTP, Export
src/WorkshopStudio.Desktop/    WPF-Shell, XAML-Seiten, Dialoge, ViewModels, Credential Vault
src/WorkshopStudio.Desktop/Themes/StudioTheme.xaml
tests/WorkshopStudio.SelfTest/ Netzwerkfreie Console-Selbsttests
scripts/Build.ps1              Restore → Build → Tests → Publish → ZIP
docs/                        Architektur, Einstellungen, Sicherheit, Testplan, Quellen
```

Die SteamCMD-Integration ist ein Entwicklerwerkzeug für deinen eigenen Account und autorisierte Inhalte. Nicht jedes Spiel erlaubt generisches Publishing mit einem Endnutzerkonto. Für andere AppIDs zuerst einen privaten Test durchführen; spielabhängige SDK-Anforderungen und Rechte bleiben bestehen.

Weitere Grenzen und manuelle Abnahme: `docs/KNOWN_LIMITATIONS.md` und `docs/TESTPLAN.md`.
