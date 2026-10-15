# Sicherheit und Vertrauensgrenzen

## Anmeldedaten

Passwörter werden nur auf ausdrücklichen Wunsch über `CredWriteW` im aktuellen Windows-Anmeldedatenspeicher persistiert. Der Eintrag ist an ein Konto-GUID gebunden. Die `RememberPassword`-Option im Workspace ist nur ein boolesches Flag. SteamCMD erhält den Benutzernamen als Prozessargument, ein angefordertes Passwort bzw. einen Guard-Code dagegen über den Eingabekanal der Windows-Pseudokonsole (ConPTY). Steuerzeichen und Eingaben über 512 Zeichen werden abgelehnt. Die App speichert keine Steam-Guard-Seeds und erzeugt keine Codes selbst.

`SecretRedactor` maskiert bekannte Eingaben vor UI-/Dateiprotokollen. `SafeLogBuffer` sammelt vollständige logische Zeilen, damit ein Passwort nicht an einer Prozess-Read-Grenze vorbeirutscht. Überlange Zeilen werden verworfen. Das ist keine generelle Geheimniserkennung: SteamCMD-eigene Diagnosefiles werden nicht von Workshop Studio kontrolliert und können weitere Kontometadaten enthalten. Vor Weitergabe von Diagnosen selbst prüfen.

Die lokale verwaltete SteamCMD-Installation und deren `config/` enthalten separat von Valve gepflegte Sitzungsdaten. Passwort löschen ist nicht gleich SteamCMD ausloggen. Extern konfigurierte SteamCMD-Installationen werden nicht automatisch bereinigt. Eine noch laufende gleiche SteamCMD-Installation wird soweit Prozessinformationen zugänglich sind erkannt; nicht gleichzeitig manuell öffnen.

## Webbrowser

WebView2 rendert originale Steam-Seiten. Es gibt keine eigene nachgebaute Passwortseite, keine DOM-Auslesung von Passwörtern, keine Hostobjekte, keine Webmessage-Bridge und keine Ausführung fremder Skripte. Für den sichtbaren Anmeldestatus liest ein festes lokales Skript ausschließlich das öffentliche Kontomenü; es liest keine Passwörter, Cookies oder Token. Top-Level-Navigation ist auf geprüfte Steam-HTTPS-Domains beschränkt. Externe neue Fenster und Downloads werden im eingebetteten Browser blockiert. Kamera-/Mikrofon- und andere angefragte Berechtigungen werden abgelehnt. Browser-Passwortspeicherung und Autofill sind ausgeschaltet; auf Wunsch bleiben Sitzungs-Cookies erhalten. Steam authentifiziert seine eigene Webseite.

Web- und SteamCMD-Konten sind technisch getrennt. Der eingebettete Browser verwendet pro Upload-Konto ein benanntes WebView2-Profil. Ohne ausgewähltes Upload-Konto bleibt das allgemeine Default-Profil verfügbar. Profile synchronisieren sich nicht automatisch mit SteamCMD oder dem Standardbrowser. Beim Wechsel zwischen mehreren Konten auf den angezeigten Steam-Namen achten und nötigenfalls die Websitzung löschen. Ohne "Websitzung merken" wird ein privates Browserprofil verwendet. Eine vorhandene frühere permanente Sitzung lässt sich gesondert löschen.

## Dateisystem und Veröffentlichung

Workspace-Import ist eine vertrauensbedürftige lokale Dateioperation. Er importiert Pfade, Kontonamen und URL-Einstellungen. Auto-Publish, Auto-Login und Start-Updateprüfungen werden beim Import deaktiviert. Vor Nutzung Quelle, SteamCMD-Pfad und Manifestadresse prüfen. Importierte Einstellungen führen für sich genommen keine Skripte aus. Die Anwendung bietet absichtlich keine beliebigen Hook-Kommandos.

Der Paketfilter ist eine zusätzliche Hilfe, kein umfassender Secret-Scanner. Eine `config.lua` mit Klartextpasswort wäre ohne zusätzliche Ausschlussregel weiterhin Inhalt. Veröffentlichten Workshop-Code können die berechtigten Empfänger lesen. Nicht öffentliche Serverlogik und private Schlüssel gehören nicht ins Workshop-Paket.

Normale Arbeitsbereichsdaten, Backups und Modpakete sind nicht verschlüsselt. Der Credential Manager schützt nicht vor Schadsoftware, die bereits unter deinem Benutzerkonto läuft. Auf Mehrbenutzerrechnern Benutzerkonten sauber trennen. Keine Steam-Passwörter an Dritte geben.

## Netzwerk und Lieferkette

SteamCMD wird von Valves HTTPS-Bootstrap-URL geladen, niemals aus einer eigenen Downloadquelle. NuGet stellt das WebView2-SDK beim Restore bereit. Der Browser verwendet die lokal installierte WebView2-Runtime. Der Update-Checker vertraut der bewusst eingetragenen HTTPS-Manifestadresse, zeigt Metadaten und öffnet nach Bestätigung den Link im Systembrowser. Er installiert und startet keine Datei und verifiziert keinen vom Benutzer später heruntergeladenen Inhalt. Eine im Manifest angegebene Prüfsumme ist eine Angabe des Manifestbetreibers, kein zusätzlicher Vertrauensanker.

Für die spätere öffentliche Verteilung sind Authenticode-Signierung, CI-Builds, Paket-Hashes, Code-Review und echte Windows-/Steam-Abnahmetests vorgesehen, aber nicht bereits erledigt.

## Ergänzungen 1.0.2

Ein im Kontoeditor eingegebenes Passwort wird ohne Speicher-Häkchen ausschließlich im Arbeitsspeicher dieser Anwendungssitzung behalten. Beim Beenden wird die Referenz verworfen; wie bei normalen .NET-Strings ist eine physische Speicherlöschung nicht garantiert. Die ursprüngliche Version hat solche Eingaben sofort verworfen, obwohl das Eingabefeld ausgefüllt war.

Der Originalkonsolen-Fallback erhält ebenfalls nur den Login-Namen, keine Passwörter in Argumenten. SteamCMD selbst steuert dort Eingabe, Anzeige und seine eigenen Diagnosefiles. Während die Konsole geöffnet ist, sperrt Studio konkurrierende Vorgänge; nach `quit` wird die Sitzung überprüft.

Die Prozess-Selbsttests starten dasselbe Testprogramm in einem rein lokalen Simulationsmodus. Es wird dabei weder SteamCMD heruntergeladen noch Steam kontaktiert. Testzeichenketten sind keine produktiven Zugangsdaten.
