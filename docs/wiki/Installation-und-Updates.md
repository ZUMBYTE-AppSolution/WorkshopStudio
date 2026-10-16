# 📦 Installation und Updates

## Voraussetzungen

Das veröffentlichte Paket ist für Windows x64 vorgesehen. Es enthält die .NET-10-Laufzeit. Für die eingebetteten Steam-Webseiten wird die Microsoft WebView2 Evergreen Runtime benötigt. SteamCMD wird getrennt von Valve geladen und beim ersten Start selbst aktualisiert. Ein eigenes Steam-Konto und die passenden Berechtigungen für das jeweilige Spiel sind erforderlich.

Das Paket ist portabel und derzeit nicht mit Authenticode signiert. Den vollständigen ZIP-Inhalt in einen Ordner entpacken, in dem dein Benutzer schreiben darf. Nicht nur die EXE aus dem Archiv heraus starten: Bibliotheken, WebView2-Komponenten und Dokumentation gehören zum Paket.

## Erstinstallation

1. [Neueste Veröffentlichung](https://github.com/ZUMBYTE-AppSolution/WorkshopStudio/releases/latest) öffnen.
2. `WorkshopStudio-<Version>-win-x64.zip` laden. `Quellprojekt.zip` ist für Entwicklung gedacht.
3. Vollständig entpacken und `WorkshopStudio.exe` starten.
4. Unter Einstellungen den SteamCMD-Pfad wählen oder die integrierte Installation verwenden.
5. Mit [Steam und Konten](Steam-und-Konten) fortfahren.

Arbeitsbereich, Kontoprofile, Cache und Sicherungen werden unter `%LOCALAPPDATA%\WorkshopStudio` abgelegt. Ein Programmupdate benötigt deshalb kein Löschen dieses Ordners.

## Integrierter Tool-Updater

Unter **Einstellungen → Updates → Jetzt prüfen** fragt Studio das neueste stabile Release aus `ZUMBYTE-AppSolution/WorkshopStudio` ab. Die Prüfung beim Start lässt sich ein- oder ausschalten. Es wird kein fremder Updateserver benötigt.

Bei einer neueren Version zeigt Studio die Release-Notizen. Nach Bestätigung lädt es das Windows-ZIP und die zugehörige Prüfsummendatei. Paketname, offizielle Downloadadresse, Größe, SHA-256, Archivpfade, Repository und enthaltene Version werden kontrolliert. Entwürfe und Vorabversionen werden nicht als stabiles Update gewählt.

Nach einer weiteren Bestätigung speichert Studio den Arbeitsbereich und startet einen Update-Helfer. Dieser wartet auf das Programmende, sichert zu ersetzende Programmdateien, kopiert den neuen Stand und startet die App wieder. Bei einem Kopierfehler versucht er, die gesicherten Dateien zurückzunehmen. Ein geschützter Installationsordner ohne Schreibrecht kann nicht automatisch aktualisiert werden.

## Manuell aktualisieren

Studio schließen. Den neuen Stand in einen neuen Ordner entpacken und von dort starten. Der Benutzer-Datenordner bleibt derselbe. Alternativ die alten Programmdateien nach eigener Sicherung ersetzen. Zwei Programmversionen sollen denselben Arbeitsbereich nicht gleichzeitig bearbeiten.

Die heruntergeladene App und ein Workshop-Item sind verschiedene Updates: Der Tool-Updater aktualisiert das Windows-Programm. Workshop-Veröffentlichungen laufen über die separate Upload-Warteschlange.
