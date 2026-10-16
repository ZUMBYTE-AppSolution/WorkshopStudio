# 🔑 Steam und Konten

Studio verwendet zwei Steam-Sitzungen. **SteamCMD** übernimmt Uploads und den Download für Sicherungen. **Steam Community in WebView2** dient zum Lesen des eigenen Portfolios und für Steam-Webfunktionen. Eine erfolgreiche Webanmeldung beweist noch keinen funktionierenden Upload-Login.

## Konto einrichten

Unter **Steam & Konten** ein Konto hinzufügen. Als Loginname den Steam-Kontonamen verwenden; der öffentliche Anzeigename ist kein Loginname. Ein frei gewählter Anzeigename im lokalen Profil erleichtert die Zuordnung. Anschließend das gewünschte Konto aktivieren.

Ein im Editor eingegebenes Passwort kann bis zum Programmende im Speicher bleiben. Dauerhaftes Speichern erfolgt nur mit der entsprechenden Option über den Windows Credential Manager. Passwörter und Steam-Guard-Codes werden interaktiv eingegeben und nicht in Prozessargumente oder Upload-VDFs geschrieben. SteamCMD kann zusätzlich eigene Sitzungstoken verwalten.

## SteamCMD anmelden

Den vorhandenen SteamCMD-Pfad auswählen oder die integrierte Installation verwenden. Danach **Anmelden** wählen. Studio versucht eine vorhandene Sitzung. Steam kann ein Passwort, einen Guard-Code oder eine Freigabe in der mobilen App verlangen. Bei einer mobilen Freigabe nicht stattdessen einen beliebigen Code eingeben.

Der integrierte Login verwendet unter Windows eine Pseudokonsole. Das bereinigte Login-Protokoll unterscheidet Anmeldung, Fehler, Abbruch und Zeitlimit. Eine Meldung wie „Loading Steam API…OK“ reicht nicht als Erfolg. Studio liest zusätzlich die SteamID64 des Kontos.

## Websitzung zuordnen

Die Steam-Webanmeldung öffnen und mit demselben Konto anmelden. WebView2-Profile sind pro lokalem Konto getrennt. Vor einem Import oder Upload müssen verifizierte Web-SteamID64 und SteamCMD-SteamID64 übereinstimmen. Eine Namensähnlichkeit genügt nicht.

Wenn ein anderes Konto angemeldet ist, die zugehörige Sitzung gezielt wechseln oder löschen. Das Löschen einer Websitzung entfernt nicht automatisch die SteamCMD-Sitzung. Das Vergessen des gespeicherten Passworts entfernt ebenfalls nicht Valves eigenen Sitzungstoken.

## Häufige Grenzen

- Steam Guard, Captchas und Vereinbarungen werden über die vorgesehenen Steam-Wege bedient.
- Eine externe SteamCMD-Konsole derselben Installation muss mit `quit` beendet werden, bevor Studio darauf zugreifen kann.
- Der normale Benutzeraccount erhält nicht automatisch Workshop-Publisherrechte für jedes Spiel.
- Nicht öffentlich bestätigbare Inhalte können angezeigt werden, bleiben im aktuellen Uploadverfahren aber gesperrt.
- Nach einem abgelaufenen Login ist eine erneute Anmeldung erforderlich; Auto-Login umgeht keine Steam-Sicherheitsabfrage.

Protokolle vor dem Teilen prüfen. Die Systemdiagnose enthält keine Passwörter oder Sitzungstoken, kann aber lokale Pfade und Projektzahlen enthalten.
