# 🛠️ Fehlerbehebung

## Steam-Anmeldung oder falsches Konto

Den aktiven Kontonamen, Login-Protokoll und getrennte Web-/SteamCMD-Sitzungen prüfen. Ein Anzeigename ist kein Loginname. Bei Guard-Abfragen die tatsächlich angeforderte Methode verwenden. Eine externe SteamCMD-Konsole derselben Installation mit `quit` beenden. Bei fehlendem WebView2 die offizielle Microsoft-Runtime installieren.

## Workshop-Eintrag bleibt nur lesbar

Studio konnte Besitzer, AppID oder Typ nicht ausreichend bestätigen. Die öffentliche Detail-API sieht nicht jeden privaten oder eingeschränkten Inhalt. Die angemeldete eigene Steam-Seite prüfen und erneut scannen. Keine fremde ID als eigene Mod einsetzen. Ein fehlgeschlagener Detailabruf wird nicht durch eine automatische Neuanlage umgangen.

## „Remote-Stand geändert“ oder „Import-Baseline weicht ab“

Steam wurde seit dem zuletzt akzeptierten Stand geändert. Bibliothek erneut ermitteln, aktuelle Beschreibung/Metadaten prüfen und den gewünschten Eintrag bewusst importieren. Lokale Editoränderungen bleiben erhalten. Danach die beabsichtigte Fassung erneut prüfen und einreihen.

Bei einer Änderung während des Backups bleibt die Sicherung erhalten; der Upload hat noch nicht begonnen. Den neuen Steam-Stand prüfen und erst dann fortfahren.

## Lokale Mods werden nicht erkannt

Einen konkreteren Stammordner oder direkt die Einzelmod wählen. Bei Zomboid müssen gültige `mod.info`-Dateien in einem unterstützten B41-/B42-Layout vorhanden sein. Nicht die gesamte Spielinstallation oder Steam-Bibliothek auswählen. Die Suche ist begrenzt und folgt keinen Junctions/Symlinks. Mehrdeutige technische IDs brauchen eine bewusste Zuordnung.

## Backup vor dem Update schlägt fehl

Im Verlauf das Protokoll lesen. Häufige Ursachen sind fehlende Steam-Rechte für den Workshop-Download, ein abgelaufener Login, SteamCMD-Downloadfehler, fehlendes altes Vorschaubild, verknüpfte Pfade, Speicherplatz oder die eingestellte Größen-/Dateigrenze. Studio verlangt einen bestätigten Download unter dem erwarteten Item-Pfad der gewählten SteamCMD-Installation. Andere oder manuell verschobene Downloadpfade werden nicht ungeprüft als Backupquelle akzeptiert.

Die Ursache beheben und den Auftrag neu einreihen. Die verpflichtende Sicherung vor einem Bestandsupdate lässt sich nicht durch Abschalten des optionalen Paket-Backups umgehen.

## Metadaten gehen nicht mit

Unter Publishing die automatische Erkennung prüfen. Ein leeres lokales Feld löscht nichts. Eine eigene Editorbeschreibung hat Vorrang vor Dateivorschlägen. Das Workshop-Bild muss ausdrücklich im Profil gewählt sein. Live-Sichtbarkeitsänderungen brauchen ihr eigenes Opt-in. Beim Changelog werden nur neue Notizen übertragen; eine bereits veröffentlichte oder nicht sicher vergleichbare erste Notiz wird ausgelassen. Die Entscheidung steht im Update-Plan und im Jobprotokoll.

## Erstupload unklar oder Abbruch während Upload

Zuerst auf Steam nachsehen. Wenn Steam bereits eine ID angelegt hat, diese im richtigen Kanal übernehmen. Die Sperre nicht ungeprüft zurücksetzen und kein Duplikat erzeugen. Ein lokaler Abbruch nimmt eine bereits serverseitige Änderung nicht zurück.

## Tool-Update oder Darstellung

Ein automatisches Tool-Update benötigt Schreibrechte im Programmordner. Alternativ die neue Version vollständig in einen neuen Ordner entpacken. Bei einem Darstellungsproblem Theme, UI-Skalierung, Fenstergröße, Windows-DPI und verwendete Version notieren. Den bestehenden Workspace erhalten.

Für eine [Fehlermeldung](https://github.com/ZUMBYTE-AppSolution/WorkshopStudio/issues) Version, Schritte, erwartetes/tatsächliches Verhalten und ein bereinigtes Protokoll beilegen. Passwörter, Guard-Codes, Cookies und vertrauliche Configwerte vorher entfernen.
