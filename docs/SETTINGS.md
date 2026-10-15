# Einstellungen und Datenfelder

## Allgemein

- Theme: Dark/Light. Akzent: `#RRGGBB`. UI-Skalierung: 0,8 bis 1,5 zusätzlich zur Windows-DPI-Skalierung; bei hoher Skalierung ausreichend großes Fenster nutzen. Start maximiert.
- Auto-Login: SteamCMD-Sitzung mit dem aktiven Konto beim Start prüfen. Keine Garantie gegen erneutes Steam Guard.
- Steam-Websitzung merken: normales persistentes WebView2-Profil statt privater Sitzung.

## SteamCMD und Publishing

- EXE-Pfad: installierter Valve-Bootstrap oder eigene Installation. Standard liegt unter dem lokalen Studio-Datenverzeichnis.
- Uploadtimeout: 1–1440 Minuten; Anmeldung: 1–60 Minuten. Große Inhalte können längere Uploadlimits brauchen.
- Standardkanal: Test/Live. Test bleibt privat und benutzt eine andere Workshop-ID.
- Nur Änderungen: Fingerprint-Vergleich gegen letzten bestätigten Upload dieses Kanals.
- Bei Fehler stoppen; Workshop nach manuellem Upload öffnen; alle mod.info-Dateien auf vorhandene IDs prüfen (zusätzliche Metadatenprüfung).
- Paketlimit: 1–1.048.576 MiB. Dateilimit: 1–2.000.000. Dies sind lokale Sicherheitslimits, keine Zusage entsprechender Steam-Uploadgrenzen.

## Dateien und Aufbewahrung

- Globale Ausschlüsse: eine Regel pro Zeile. App- und Mod-Regeln kommen additiv hinzu.
- `*.tmp` gilt für Dateinamen auch in Unterordnern; `ordner/` für passende Verzeichnisse; Regeln mit Slash werden sowohl relativ zur gewählten Einzelmod als auch relativ zum Uploadpaket geprüft, beispielsweise `media/debug/**` und `mods/rp-core/media/debug/**`. `*`, `?` und `**` werden als einfache Globs interpretiert. Kein Negationsoperator `!`, keine vollständige gitignore-Auswertung. Für alle Ebenen optional beide Regeln `*.tmp` und `**/*.tmp` verwenden.
- Typische Geheimnisse ausschließen: `.env`, `.env.*`, `secret.json`, `secrets.json`, `.ssh`, `.aws`, `id_rsa`, `id_ed25519`, `.key`, `.pem`, `.pfx`, `.p12`.
- Erfolgreiche Pakete behalten oder Staging nach Erfolg entfernen. Fehlgeschlagene Pakete bleiben zur Diagnose, soweit bereits erzeugt.
- Paket-Backup nach Erfolg; 1–1000 Backups je Mod/Kanal. Alte Paket-ZIPs dieser Reihe werden entfernt. Workspace-Backups besitzen keine automatische Mengenbegrenzung.
- Upload-Verlauf: 10–50.000 abgeschlossene Einträge. Die zugehörigen Protokolldateien bleiben bei Verlaufskürzung bestehen.
- Bildcache und eigene Staging-Ablage sind separat bereinigbar. Quelldateien werden nicht gelöscht.

## Steam-Metadaten

Startabruf ein/aus, Store-Sprache, zweistelliger Store-Ländercode, HTTP-Zeitlimit 5–300 Sekunden für App-Metadaten, Bildcache 0–365 Tage. Bei 0 werden Bilder bei jedem Abruf erneuert. Collection-/Update-Anfragen verwenden derzeit das allgemeine 5-Minuten-HttpClient-Limit; das App-Metadaten-Zeitlimit überschreibt dieses nur für App-Abrufe. Nicht jede AppID hat eine öffentliche Store-Seite oder ein öffentliches Icon. Icons nutzen bei Bedarf einen Ausschnitt des Produktbilds als Fallback. Geladene Steam-Namen können einen lokal geänderten Anzeigenamen wieder ersetzen.

## Automation

Globale Aktivierung, Ordnerüberwachung, Ruhezeit 5–3600 Sekunden, Mod-spezifisches Auto-Publish und Zielkanal. Zeitplan: Manual, Interval oder Daily. Intervall 1–10.080 Minuten; Tageszeit `HH:mm` nach Windows-Ortszeit. Live-Automation benötigt zusätzliche ausdrückliche Freigabe. Die Engine läuft nur bei geöffneter Anwendung. "Manual" deaktiviert den Zeitplan, nicht eine zusätzlich eingeschaltete Ordnerüberwachung.

## Tool-Updates und Daten

Fester Updatekanal: neuestes stabiles Release von `ZUMBYTE-AppSolution/WorkshopStudio` auf GitHub. **Jetzt prüfen** und optionale Prüfung beim App-Start. Ein verfügbares ZIP wird nach Bestätigung heruntergeladen, anhand des offiziellen `SHA256SUMS.txt` geprüft und nach einer weiteren Bestätigung beim Neustart installiert. Die bisherige Manifestadresse und Kanalauswahl bleiben in älteren Workspace-Dateien erhalten, werden vom eingebauten Updater aber nicht verwendet. Die Programminstallation braucht Schreibrechte; andernfalls meldet der Updater einen Fehler und erhält die bisherige Fassung.

Workspace sichern, exportieren, importieren und Datenordner öffnen. Workspace-Export ist keine Sicherung deiner Mod-Quellverzeichnisse und enthält keine gespeicherten Passwörter/Sitzungen. Quellcode separat mit Git oder einem Backup sichern.

## App-Profil

Steam-AppID, Name, Quellbasis, Vorlagenart (Generic/B41/B42), Standard-Sichtbarkeit, Standard-Ausschlüsse und Notizen. Metadatenbilder werden online gepflegt. Ein App-Profil mit zugeordneten Mods/Collections wird nicht versehentlich entfernt. Eine veröffentlichte Mod kann nicht einfach auf eine andere AppID umgebogen werden.

## Mod-Profil

App-Zuordnung, Titel, Version, Mod-Quellpfad, Quellmodus (Einzelmod oder explizite Mehrmod-Auswahl), gespeicherte ausgewählte Mod-Ordnernamen, Vorschaubild, Beschreibung, Mod-IDs aus `mod.info`, Änderungsnotiz, Tags als lokale Arbeitsnotiz, Ausschlussregeln, Sichtbarkeit, Live-/Test-Workshop-ID, Test-Titelsuffix, Aktiviert, Auto-Publish, Notizen. `Update {version}` und `{date}` werden in Änderungsnotizen ersetzt. Tags selbst werden nicht als inoffizielles SteamCMD-Feld übertragen. Die gewählte Vorschaudatei ist PNG/JPEG bis 1 MiB. Titel maximal 128 UTF-8-Bytes einschließlich Testzusatz; Beschreibung maximal 8000 UTF-8-Bytes. Das Zielspiel kann engere Grenzen oder zusätzliche Felder verlangen.

Neue Mod-Struktur, Mod-IDs erkennen, VDF importieren, Mod-Profil duplizieren, Unsicherheitsmarkierung nach manueller Prüfung zurücksetzen. Duplizieren kopiert zunächst das Profil, nicht seine Quelldateien. Unterschiedliche Profile können daher bewusst dieselbe Quelle verwenden. Steam-IDs dürfen in der Workspace-Zuordnung nicht mehrfach vorkommen.

## Collection und Konto

Collection: App, Name, Beschreibung, eigene Mod-Projekte, externe IDs, optionale Steam-Collection-ID und letzter eingelesener Remote-Stand. Der Inhalt einer Collection und der Inhalt einzelner Mods sind unterschiedliche Dinge. Eine private Collection ist über die öffentlichen Lese-Endpunkte eventuell nicht überprüfbar; eine manuell gepflegte Verknüpfung lässt sich im lokalen Editor speichern.

Konto: Steam-Loginname, lokale Bezeichnung, optionales Passwort und Merkoption. Das aktive Upload-Konto wird explizit ausgewählt. Steam-Weblogin ist davon unabhängig.

## Quellenauswahl und Sicherheitsprüfung ab 1.0.3

**Mod-Ordner wählen** verwendet standardmäßig genau eine Mod. Unter **Mods & Resources → Mehrere Mods auswählen** durchsucht Studio einen Stammordner und ordnet gefundene Einzelmods nach technischer ID oder eindeutigem Namen vorhandenen Profilen zu; neue Profile sind nur nach bewusster Auswahl möglich. Im Mod-Editor erstellt **Mehrmod-Paket zusammenstellen** weiterhin ein gemeinsames Upload-Paket aus ausdrücklich ausgewählten Mods; eine Collection ist keine solche Inhaltsauswahl. **Struktur & IDs prüfen** zeigt die Quell-/Zielzuordnung und übernimmt erkannte IDs. **Upload-Inhalt prüfen** erzeugt ein lokales Staging-Paket, dessen vollständige Dateiliste gefiltert und als JSON exportiert werden kann. Auch leere Unterordner werden aufgeführt.

Die Erkennung von Spielverzeichnissen, Pfadgrenzen, Links/Junctions, einer unklaren Mehrmod-Auswahl und fehlenden grundlegenden Mod-Metadaten bleibt immer aktiv. Nur die zusätzliche Prüfung einer `id=` in jeder einzelnen Versions-`mod.info` ist einstellbar. Automatische Ordnerüberwachung verwendet dieselbe Auswahl wie die Paketierung.

Die frühere Standardakzentfarbe `#FF944D` wird beim Laden einmalig auf `#FF781F` umgestellt. Individuell gewählte andere Akzentfarben und das gespeicherte Dark-/Light-Theme bleiben erhalten.
