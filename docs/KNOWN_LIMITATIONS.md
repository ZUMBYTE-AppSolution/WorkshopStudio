# Bekannte Grenzen und noch erforderliche Abnahme

## Stand der Prüfung für 1.2.0

131/131 C#-Selbsttests und ein lokaler WPF-Renderingtest bestanden. Die Dashboard-Spiele sind als Liste mit proportional skaliertem Bild links und Informationen rechts sichtbar. Der Updater wurde mit gültigen und manipulierten Release-Metadaten, SHA-256-Zuordnung und sicheren ZIP-Pfaden offline getestet. Ein End-to-End-Update von einer älteren installierten Version auf ein neueres öffentliches Release kann erst nach einem nachfolgenden Release geprüft werden. Das Tool-Paket ist portabel und nicht signiert; für geschützte Installationsordner sind Schreibrechte nötig. Der Nutzerordner für echte lokale Mods und ein echter Steam-Upload bleiben offen.

## Stand der Prüfung für 1.1.1

Der Windows-Build und 123/123 C#-Selbsttests sind erfolgreich. Ein WPF-Smoke-Test hat die neue Zuordnung zweier verschachtelter Einzelmods, das Zuordnungsfenster sowie die bisherigen Dashboard-, Bibliotheks-, Fensterknopf- und Config-Ansichten gerendert. Die Suche wurde mit Testordnern ausgeführt. Ein echter lokaler Mod-Stammordner des Nutzers wurde noch nicht ausgewählt; seine Zuordnung zu den importierten Workshop-Profilen muss in der Anwendung überprüft werden. Kein Steam-Upload wurde durchgeführt.

Die automatische Zuordnung nutzt identische technische Mod-IDs, bereits gespeicherte Quellpfade und eindeutige kompatible Namen. Stimmen diese Merkmale nicht eindeutig überein, bleibt die Mod im Dialog zunächst übersprungen. Ein manuell gewähltes Profil mit widersprechender gespeicherter Mod-ID wird beim Übernehmen abgelehnt. Neue lokale Profile erhalten keine Workshop-ID. Die Suche ist aus Sicherheitsgründen in Tiefe und Anzahl begrenzt und folgt keinen Verknüpfungen oder Junctions.

## Stand der Prüfung für 1.1.0

Windows-Build mit .NET SDK 10.0.402: erfolgreich, ohne Warnungen. 114/114 C#-Selbsttests bestanden. Dashboard, Bibliothek und Config-Fenster wurden als echte WPF-Fenster bei 1280×800, 1920×1080 und 1040×760 gerendert; die Auswahl aus dem Config-Originaltext wurde über den Fensterknopf in die Zwischenablage kopiert. Die vorhandenen Steam-Web- und SteamCMD-Sitzungen meldeten dieselbe SteamID64. Der Webscan fand 28 eigene Inhalte auf zwei Seiten; 27 wurden öffentlich als Eigentum bestätigt. 19 Mods, 8 Collections und alle 28 Bibliothekseinträge wurden unter Erhalt der bisherigen Einstellungen importiert. Der exakte bisherige Arbeitsbereich wurde vor der Migration und vor dem Import gesichert.

Offen bleiben ein echter privater Erst- und Bestandsupload, die Abnahme im Spiel, Steam Guard bei einer frischen Anmeldung, UI-Prüfungen bei 125/150 % DPI und Hellmodus sowie ein End-to-End-Test der Bildaktualisierung. Ohne gezielte Freigabe wurde nichts veröffentlicht.

## Steam-spezifische Grenzen

- SteamCMD-Workshop-Publishing ist Valves Entwickler-/Testwerkzeug. Unterschiedliche AppIDs, Konten, Eigentümerrechte, Workshop-Vereinbarungen und Spiele können andere Anforderungen haben. Beliebige AppID-Verwaltung ist keine universelle Uploadberechtigung.
- Der integrierte Login verwendet ab 1.0.2 die Windows-Pseudokonsole (ConPTY). Konkrete Authentifizierungsprompts und Konsolenverhalten können sich trotzdem ändern. Für mobile Bestätigung oder nicht erkannte Dialoge gibt es eine originale interaktive SteamCMD-Konsole. Dort die Sitzung einrichten, mit `quit` schließen und dann erneut in Studio prüfen. Kein QR-Login-SDK, keine Steam-Guard-Umgehung.
- Originale Steam-Webseiten erledigen Collection-Anlage/-Änderung, Tags, Abhängigkeiten und Spezialmetadaten. Lokale Collections sind nicht automatisch serverseitig synchronisiert. Öffentlicher API-Abgleich kann private Objekte nicht immer lesen.
- Produktbilder verwenden einen öffentlichen, nicht als stabil garantierten Store-Endpunkt. Bei Ausfall manuell weiterarbeiten. Steam-Seitendesign und erlaubte Login-Domains können Anpassungen erfordern. Eine feste Datei-Bibliothek mit allen Steam-Bildern wird nicht mitgeliefert.
- Der eingebettete Browser speichert auf Wunsch eine Websitzung. Pro Upload-Konto gibt es ein eigenes WebView2-Profil. Die Identität wird über die angemeldete Profilseite ermittelt und vor Uploads mit der SteamCMD-SteamID64 verglichen. Eine alte gemeinsame Websitzung wird nicht automatisch kopiert. Steam-Seitenänderungen können die Profilerkennung und Seitenanalyse beeinflussen.
- Ein eigener Eintrag war über die öffentliche Steam-Detail-API nicht zugänglich. Er erscheint in der Bibliothek, ist aber ohne zusätzliche Bestätigung nicht als schreibbares Mod-Profil importiert. Private Inhalte können daher teilweise ohne Bild, Beschreibung, Versionshinweis oder Changelog bleiben.
- Versionsangaben sind kein verpflichtendes Steam-Workshop-Feld. Studio liest nur ausdrücklich angegebene lokale Versionen oder kennzeichnet einen Hinweis aus der Beschreibung als Heuristik. Config-Dateien werden nur aus einem ausdrücklich zugeordneten lokalen Mod-Quellordner gelesen.

## Funktionsumfang

Kein FiveM-Live-Resource-Loader, kein Spielserver-Deployment per SSH, kein automatischer Zomboid-Serverneustart, kein Resource-Framework und keine RP-Spiellogik. Das Tool erstellt/veröffentlicht Workshop-Inhalte und exportiert benötigte Listen. B41/B42-Vorlagen sind Ausgangsstrukturen, keine im Spiel abgenommenen Beispielmods.

Kein Dienst/Autostart-Task, keine garantierte Veröffentlichung im ausgeschalteten Zustand, keine integrierte Git-Authentifizierung, keine automatisch ausgeführten Build-Hooks. Ordnerüberwachung und Zeitpläne sind lokal und benötigen die geöffnete Anwendung. Ein modübergreifend atomarer Snapshot während paralleler Bearbeitung wird nicht garantiert.

Der integrierte Updater nutzt die offiziellen GitHub-Releases und deren SHA-256-Datei. Er aktualisiert die portable Programminstallation nach Bestätigung, nicht eine verwaltete MSI-/Store-Installation. Das Windows-Paket enthält eine gebaute EXE, aber keinen signierten Installer; Windows SmartScreen kann warnen.

Keine frei konfigurierbaren Proxy-Credentials; das Standardverhalten von Windows/HttpClient gilt. Keine frei konfigurierbare Gesamt-Datenablage; diese bleibt unter dem aktuellen Benutzerprofil. Keine behauptete Abdeckung jedes zukünftigen Steam- oder spieleigenen Settingsfelds. Alle tatsächlich implementierten Optionen stehen in `SETTINGS.md`.

## Theme-Fix 1.0.1

Native Datei-/Ordnerdialoge und der eigentliche Inhalt von Steam-Webseiten sind keine WPF-Theme-Flächen. Dokumentierte DWM-Titelleistenfarben werden ab Windows 11 gesetzt. Details und visuelle Prüfschritte stehen in `THEME_FIX_1.0.1.md`.

## Login-Fix 1.0.2

Der konkrete Fehler des Nutzers wurde mangels Fehlerprotokoll nicht reproduziert. Korrigiert wurden im Quellstand erkennbare Fehlerquellen: fehlende Konsolensemantik, unterdrückte schnelle Folgeabfragen, keine Guard-Erkennung auf stderr, verschwundene einmalige Passworteingaben und fehlende Weblogin-Rückmeldung. Das ist kein bereits nachgewiesener Steam-End-to-End-Fix. Details und Abnahmeschritte: `LOGIN_FIX_1.0.2.md`.

## Design und Mod-Auswahl 1.0.3

Das zuvor generierte Werbebild war ein Entwurf, kein Screenshot. Die WPF-Shell und das Dashboard übernehmen die wesentliche Aufteilung und den Anthrazit-Orange-Stil. Ein WPF-Renderingtest bei 100 % wurde durchgeführt; pixelgenauer Vergleich sowie 125/150-%-DPI-Abnahme stehen aus. Steam-Bilder hängen von den tatsächlich abrufbaren Bildern ab.

Zomboid-Quellen werden anhand von `mod.info` am Mod-Stamm oder in numerischen Versionsordnern erkannt. Auswahl und Grundprüfungen können nicht über die optionale erweiterte Metadatenprüfung ausgeschaltet werden. Spielverzeichnisse werden anhand typischer ausführbarer Dateien bzw. Verzeichnisstrukturen abgewiesen. Das ist keine vollständige Klassifizierung beliebiger fremder Dateien und kein Ersatz für die Paketvorschau. Innerhalb der ausdrücklich ausgewählten Mod werden Dateien gemäß den Ausschlussregeln übernommen; private Notizen oder fremde Assets gehören nicht in eine Upload-Quelle.

Alte Profile mit mehreren Mods im Contents-Ordner verlangen jetzt einmalig eine bewusste Auswahl. Alte Einzelmod-Profile können weiter auf einen passenden Wrapper zeigen, werden beim Speichern aber auf die Einzelmod normalisiert. Die neue Fingerprint-Struktur kann einmalig eine Veröffentlichung trotz unverändertem Dateibestand auslösen. B42-Pakete erhalten bei Bedarf einen leeren `common/`-Ordner nur im Staging. Die Spielkompatibilität sämtlicher zukünftiger Build-Unterversionen wird nicht garantiert.

Generische App-Profile behalten das Laden des bewusst ausgewählten Inhaltsordners bei; Zomboids AppID 108600 wird immer als Zomboid behandelt. Eigene AppIDs mit anderen Paketformaten benötigen weiterhin die vom jeweiligen Spiel erwartete Ordnerstruktur.
