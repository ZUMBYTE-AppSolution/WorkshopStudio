# Steam-Anmeldung und App-Icon · 1.0.2

## Was wurde im alten Quellstand gefunden?

Der integrierte SteamCMD-Prozess wurde mit umgeleiteten Standard-Streams gestartet. Damit wurde kein Windows-Konsolenhost bereitgestellt, wie ihn interaktive Konsolen-Eingabe-APIs benötigen. Die neue Windows-Implementierung stellt eine echte Pseudokonsole bereit. Beide Seiten bleiben asynchron angebunden; Ausgabe wird auch während eines Eingabedialogs und während des Prozess-Abbaus abgeholt.

Außerdem hat die alte Erkennung nach einem Prompt eine Sekunde lang weitere Prompts unterdrückt. Ein gespeichertes Passwort konnte dadurch schnell beantwortet werden, während eine unmittelbar folgende Guard-Abfrage nicht mehr verarbeitet wurde. Abfragen auf stderr wurden vollständig ignoriert. Abbruch wurde fälschlich als Timeout gemeldet. Diese Fehlerquellen sind im Code beseitigt.

Das Kontoeditor-Passwort wurde ohne Speicher-Häkchen sofort verworfen. Jetzt wird es ausschließlich für die laufende Anwendungssitzung gehalten. Der dauerhafte Windows-Anmeldedatenspeicher bleibt optional. Bei späteren Anmeldungen wird zunächst die von Valve verwaltete Sitzung verwendet, nicht unnötig ein Passwort übergeben.

Der bisherige Weblogin zeigte nur eine Steam-Seite an. Er lieferte keinen Anmeldestatus an die Kontoseite zurück und verwendete ein gemeinsames Browserprofil. Jetzt gibt es getrennte Profile und eine sichtbare Rückmeldung anhand des originalen Steam-Kontomenüs. Es werden keine Passwortfelder oder Token ausgelesen. Das ist eine UI-Erkennung, kein universelles Steam-Authentifizierungs-SDK.

## Update installieren

1. Die alte Anwendung schließen. Das neue Quellarchiv in einen eigenen Projektordner entpacken.
2. `WorkshopStudio.sln` öffnen und die Projektmappe neu erstellen, oder `Build.cmd` ausführen.
3. Die neu gebaute Anwendung starten. In der Sidebar muss `Version 1.0.2 · Login-Fix` stehen.
4. Den vorhandenen Studio-Datenordner und `workspace.json` nicht löschen. App-, Mod-, Collection- und Theme-Einstellungen werden unverändert weiterverwendet.

Das neue App-Icon ist in der EXE-Konfiguration, dem gemeinsamen Fensterstil, der Titelleiste und der Sidebar eingebunden. Es muss nicht manuell in Visual Studio ausgewählt werden. Die ICO-Datei enthält 16, 20, 24, 32, 40, 48, 64, 128 und 256 Pixel.

## Zum Hochladen anmelden

Auf `Steam & Konten` das richtige Upload-Konto aktivieren und `Für Uploads anmelden` wählen. Fehlt ein Konto oder SteamCMD, wird dessen Einrichtung angeboten. Im Kontofeld den tatsächlichen Steam-Login-Namen verwenden, nicht den frei wählbaren Anzeigenamen.

SteamCMD versucht zuerst seine vorhandene Sitzung. Bei Bedarf erscheint die Passwortabfrage, danach ein Code-Dialog oder der Hinweis, die Anmeldung in der Steam-App zu bestätigen. Ein erfolgreich geladenes Steam-API-Modul allein gilt nicht als erfolgreicher Login.

Bei einem ungültigen gespeicherten Passwort das betroffene Konto auswählen, `Gespeichertes Passwort löschen` benutzen und erneut anmelden. Bei einer Steam-Sperre wegen zu vieler Versuche nicht weiter wiederholt anmelden. Im unteren Bereich derselben Seite steht das bereinigte Login-Protokoll mit Kopier-/Speicherfunktion. Es wird zusätzlich im Studio-Datenordner unter `logs/login-*.log` abgelegt.

## Originalkonsole als Alternative

`Originale SteamCMD-Konsole` öffnet Valves interaktive Anwendung. Dort Passwort und Guard eingeben, auf die bestätigte Anmeldung warten und `quit` eingeben. Studio wartet auf das Ende dieser Konsole und überprüft danach die gespeicherte Sitzung. Währenddessen bleiben konkurrierende Uploads gesperrt. In Prozessargumenten und temporären Skripten stehen weiterhin keine Passwörter.

## Steam-Weblogin

`Steam-Weblogin öffnen` dient ausschließlich den originalen Workshop-/Community-Seiten. Ein Weblogin ersetzt nicht die SteamCMD-Anmeldung. Auch der Standardbrowser besitzt eine separate Sitzung. Es gibt keine Übertragung von Cookies aus anderen Browsern oder des Steam-Clients.

Beim ersten Öffnen eines Kontos in 1.0.2 kann eine neue Webanmeldung erforderlich sein, weil alte gemeinsame Cookies nicht in die neuen Kontoprofile kopiert werden. Ohne aktives Upload-Konto wird das vorhandene Default-Profil verwendet. Mit deaktivierter Option `Websitzung merken` bleibt die Sitzung privat.

Fehlende WebView2 Runtime, Seiten-Ladefehler und abgestürzte Browserprozesse erscheinen jetzt im Fenster. Dort gibt es `Neu laden`, die originale Microsoft-Installationsseite und den Standardbrowser als Alternative. HTTPS-Prüfung und Browsersicherheit werden nicht ausgeschaltet.

## Lokale Abnahme auf Windows

`Build.cmd` kompiliert und führt die C#-Selbsttests aus. Die neuen Prozess-Tests starten das Testprogramm selbst als lokale SteamCMD-Simulation. Dabei werden Passwort, Guard, stderr, mobile Freigabe, Sitzung, Timeout, Abbruch und Wiederverwendung nach Abbruch geprüft. Es gibt keine Steam-Verbindung und keine echten Zugangsdaten.

Danach echte Login-Fälle prüfen: frische Installation, Kontopasswort mit/ohne Merken, E-Mail-Guard, Authenticator, mobile Freigabe, Neustart/Auto-Login, falsches Passwort, Abbruch, Originalkonsole, Weblogin, Profilwechsel und privates Browserprofil. Erst danach mit einer privaten Test-Mod veröffentlichen.

**Lieferstatus:** Im Erstellungsumfeld standen weder Windows noch ein .NET SDK zur Verfügung. Die C#-Tests, die native Konsole und ein echter Steam-Login wurden hier nicht ausgeführt. Der konkrete ursprüngliche Fehler wurde ohne Fehlermeldung nicht reproduziert. Die beigefügten Strukturprüfungen können einen erfolgreichen Build oder eine erfolgreiche Anmeldung nicht ersetzen.
