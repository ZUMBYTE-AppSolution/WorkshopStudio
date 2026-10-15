# Lokale Abnahme unter Windows

Status bei Lieferung 1.2.0: **teilweise erledigt**. Windows-Build, 131 Selbsttests, WPF-Rendering von Dashboard/Bibliothek/Config und neuer Ordnerzuordnung, Iconify-Fenstertasten für Minimieren/Maximieren/Wiederherstellen samt rotem Schließen, Config-Zwischenablage und lesender Web-/SteamCMD-Kontoabgleich wurden ausgeführt. Die Zuordnung eines tatsächlichen lokalen Nutzerordners, ein Update über zwei öffentliche Versionen, echte Steam-Veröffentlichung und Spielabnahme bleiben offen.

## Dashboard und GitHub-Updater 1.2.0

Spielkarten im Dashboard als vertikale Liste prüfen: vollständig sichtbares, proportional skaliertes Bild links; Titel, AppID, Inhalt und Profilstatus rechts. Mit echten Steam-Bildern sowie bei schmalem Fenster und 125/150 % DPI abnehmen. **Einstellungen → Tool-Updates → Jetzt prüfen** muss das offizielle GitHub-Release lesen. Vor dem ersten Release ist „noch kein Release“ richtig; bei gleicher Version „aktuell“. Mit einer späteren höheren Version Download, SHA-256, Neustart, Übernahme der Programmdateien und Erhalt des Workspace testen. Veränderte ZIP-Datei, falsche Prüfsumme, manipulierte URL und ZIP-Pfad außerhalb des Zielordners müssen abgewiesen werden. GitHub-Action bei Versionstag auf erfolgreichem Windows-Build, Tests, Quell-ZIP, Windows-ZIP und `SHA256SUMS.txt` prüfen.

## Lokale Ordnersuche und Zuordnung 1.1.1

Unter **Mods & Resources → Mehrere Mods auswählen** einen eigenen Mod-Stammordner wählen. B41-Einzelmod, B42-Einzelmod und verschachtelte `Contents/mods`-Projekte prüfen. Werden mehrere einzelne Mods gefunden, muss jede separat erscheinen und ein eigenes Zielprofil erhalten können. Eindeutige Mod-IDs und bereits zugeordnete Pfade sollen Treffer vorschlagen; unklare Namen bleiben zunächst übersprungen. Im Dialog vorhandene Workshop-ID und Pfad kontrollieren, Zuordnung ändern oder bewusst ein neues lokales Profil anlegen. Nach Übernahme darf nur der jeweils gewählte Einzelmodpfad im Zielprofil stehen; andere Profile, Workshop-IDs, Metadaten und Quelldateien bleiben erhalten. Auto-Publish der zugeordneten Profile ist aus. Doppelte Ziele, falsche technische Mod-ID und wartende Uploadjobs müssen den Vorgang ohne Teilspeicherung stoppen. Die bestehende Aktion **Mehrmod-Paket zusammenstellen** im Mod-Editor separat prüfen. Kein Upload durch die Ordnersuche.

## 1. Build und netzwerkfreie Tests

`Build.cmd` ausführen. Restore, Build und alle Console-Selbsttests müssen ohne Fehler abschließen. Alternativ `dotnet run --project tests/WorkshopStudio.SelfTest -c Release`. Die Tests erzeugen nur temporäre Verzeichnisse und keine Steam-Uploads. Testausgabe protokollieren. Ein erfolgreicher Core-Testlauf beweist noch nicht die korrekte WPF-Laufzeit oder Steam-Authentifizierung.

Zusätzlich die Schritte in [THEME_FIX_1.0.1.md](THEME_FIX_1.0.1.md) abarbeiten. Die Theme-Quellverträge in den Console-Selbsttests sind keine UI-Laufzeittests.

## 2. Oberfläche und Speicherung

Frischer Windows-Benutzer oder gesicherter/leerer Studio-Datenordner. Alle elf Seiten öffnen. Dark/Light und zwei Akzentfarben prüfen. 100 %, 125 % und 150 % Windows-DPI, unterschiedliche Fenstergrößen sowie maximiert testen. Texte dürfen nicht unlesbar abgeschnitten werden. Eingaben mit deutschem Dezimalkomma, Tab-Reihenfolge und Validierungsfehler prüfen. App schließen/öffnen; Theme, Profile, Auswahl aktives Konto und Einstellungen kontrollieren. Eine absichtlich beschädigte Test-Workspace-Datei darf nicht still überschrieben werden.

App hinzufügen, bearbeiten und entfernen. Online-Bildabruf und offline/manuelle App-Daten prüfen. Mod hinzufügen, Vorlage erzeugen, bearbeiten, duplizieren und lokal entfernen. Dateien dürfen beim Entfernen von Profilen bestehen bleiben. `Ctrl+N`, `Ctrl+S`, `F5` und Suche prüfen. Ein fremdes Mod-Verzeichnis mit enthaltenem Studio-Datenpfad muss für Uploads abgelehnt werden.

## 3. Paket und Ausschlüsse

Paketprüfung mit Leerzeichen und Umlauten im Pfad. Staging-Dateien, VDF, Preview und Manifest ansehen. `.env`, `.git`, Schlüsseldateien, Ausschlüsse und Größenlimit prüfen. Hash bei gleichem Inhalt stabil, bei Inhalts-/Beschreibung-/Preview-Änderung unterschiedlich. Größere Datenmenge testen und mit der Stopptaste abbrechen. Stop muss auch während manuell gestarteter Queue aktiv sein. Quellordner dürfen unverändert bleiben.

## 4. Authentifizierung

Mit einem eigenen berechtigten Testkonto anmelden. Passwort nicht speichern, einmal speichern, löschen und neu anfordern. Windows-Anmeldedatenspeicher kontrollieren. Falsches gespeichertes Passwort muss eine neue Eingabe ermöglichen. Guard-E-Mail, Authenticator bzw. mobile Freigabe passend zum Account prüfen. SteamCMD-Konsole für interaktive Erst-Anmeldung testen und mit `quit` beenden. Eine zweite parallele gleiche SteamCMD-Installation muss abgewiesen werden, soweit Prozessinformationen zugänglich sind.

Prozessargumente, Workspace, VDF und Studio-Protokolle auf Klartextpasswörter kontrollieren. Weblogin durchführen und bewusst ein anderes Webkonto als Uploadkonto verwenden, damit die Trennung sichtbar bleibt. Websitzung merken ein/aus und Websitzung löschen prüfen. Ausgeloggte private Collection-Leseabfrage muss verständlich scheitern.

## 5. Echte private Workshop-Mod

Nur eigene Inhalte und ein eigenes autorisiertes Steam-Konto verwenden. Vor Freigabe Workshop-Vereinbarung und Ziel-App prüfen. Erst eine winzige Test-Mod privat hochladen. Bestätigte ID muss übernommen sein; zweiter Upload darf keinen neuen Eintrag erzeugen. Modinhalt im Steam-Client herunterladen und im Zielspiel prüfen. Unveränderten Upload überspringen. Inhaltsänderung aktualisieren. Getrennte Live-ID mit zunächst privater Live-Sichtbarkeit testen.

SteamCMD während einer ersten Veröffentlichung abbrechen, Netzwerkfehler provozieren und App nach einem unterbrochenen Vorgang neu starten. Kein blindes Erstellen eines zweiten Eintrags. Reconciliation-Anleitung und VDF-ID-Wiederherstellung prüfen. Eine existierende ID eines anderen Kontos darf keine falsche Erfolgsanzeige auslösen.

## 6. Collections, Automation und Updates

Lokale Collection eigener/externer IDs anlegen; Exportkanäle überprüfen. Originalen Steam-Editor öffnen, echte Collection erstellen und verknüpfen. Öffentlichen Remote-Stand importieren/abgleichen. Lokale Änderungen dürfen den Steam-Stand nicht ohne separate Aktion ersetzen.

Auto-Publish zunächst ausschließlich Test: globale Aktivierung ohne Mod-Freigabe darf nichts senden; Mod-Freigabe ohne globale Aktivierung ebenfalls nicht. Nach schneller Folge mehrerer Dateiänderungen Ruhezeit abwarten und genau einen passenden Upload kontrollieren. Unveränderte Snapshots müssen übersprungen werden. Manuell wartende Jobs nicht versehentlich durch Automationsstart veröffentlichen. Fehlendes Live-Opt-in blockiert Live-Automation. Anwendung schließen: keine Hintergrundaktivität.

Eigenes HTTPS-Update-Manifest mit höherer Version prüfen. Downloadlink nur nach Zustimmung öffnen, niemals automatisch ausführen. Defekte JSON-Datei, fehlende Quelle und ungültige Version müssen verständlich scheitern. Workspace exportieren/importieren: keine Passwörter und keine automatisch wieder aktivierten Jobs/Start-Logins.

## 7. Design und Einzelmod-Auswahl 1.0.3

- Hauptfenster: orange gefüllte aktive Navigation, Suche in der Kopfleiste, vier KPI-Kacheln, echte Spielkarten, Schnellaktionen und rechter Aktivitätsbereich. Beim Verkleinern stapeln sich KPIs/Aktivität sinnvoll. 100/125/150 Prozent DPI sowie Dark/Light/eigene Akzentfarben testen; keine Demo-KPIs oder leeren weißen WPF-Flächen.
- B41-Mod-Stamm mit `mod.info` wählen. Vorschau enthält ausschließlich `mods/<Modname>/...`; kein zusätzlicher `Contents/`-Wrapper. Quellordner bleiben unverändert.
- B42-Mod-Stamm bzw. `42/` wählen. Alle gewählten Mod-Versionen sowie `common/` bleiben erhalten. Ein fehlender leerer `common/`-Ordner wird nur im Paket ergänzt.
- Übergeordneten `mods/`-Ordner mit A, B, C wählen. Standardauswahl verlangt genau eine Mod. Bewusstes Mehrmod-Paket A/C darf keine Datei von B enthalten. Nachträglich D hinzufügen: bestehende Auswahl und ihr Inhaltshash bleiben unverändert.
- Root des echten Spiels wählen: verständlicher Abbruch vor Staging/Upload. Dessen eigene Untermod unter `mods/<Name>` darf dagegen auswählbar sein. Alte Profile mit unklarer Mehrmod-Quelle müssen zur Auswahl auffordern.
- Vorschau: Filecount, Größe, Suchfilter, vollständige Dateiliste, Ordnerliste, Mapping, VDF, Export und Öffnen des tatsächlichen Staging-Ordners prüfen. Ausschluss einer erforderlichen `mod.info` muss fehlschlagen.
- Automation nur am privaten Test-Kanal: eine Änderung an einer nicht ausgewählten Nachbarmod darf keinen Dateiwatcher-Job auslösen. Änderungen an ausgewählten Mods werden nach der Ruhezeit verarbeitet.

## Freigabe

Windows-Version, .NET-SDK-Version, SteamCMD-Version, WebView2-Runtime, getestete AppID, Mod-Sichtbarkeit und Ergebnis notieren. Erst nach erfolgreicher privater Abnahme für den produktiven RP-Workshop verwenden.
