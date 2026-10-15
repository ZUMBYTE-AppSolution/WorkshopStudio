# Theme-Fix 1.0.1

## Ursache und Umfang

Die bisherigen XAML-Ressourcen enthielten nur einen impliziten `Window`-Stil. Die selbst definierten Fensterklassen hatten keine explizite Stilzuweisung. Dadurch konnte gerade hinter transparenten Containern der helle Fensterhintergrund sichtbar bleiben. Hinzu kamen nicht vollständig ersetzte WPF-Standardvorlagen für Tabs, Passwortfelder und weitere Steuerelemente.

Alle neun Fenster verwenden jetzt ausdrücklich `StudioWindow`, alle zehn Seiten `StudioView`. Der Hauptbereich zeichnet zusätzlich `WindowBrush`. Das gespeicherte Theme wird vor dem Erstellen des Hauptfensters angewendet, nicht erst beim Loaded-Ereignis. Die bestehende Anthrazit-Orange-Palette, Navigation, Seitenaufteilung und Funktionen bleiben erhalten. Light bleibt weiterhin eine bewusst wählbare Einstellung.

## Ergänzungen

Eigene Vorlagen decken jetzt Passwortfelder, Dropdowns samt Einträgen, Tabs samt Inhaltsbereich, Listen, Tabellenüberschriften einschließlich Sortierung und Größenänderungs-Griffen, ScrollViewer, Fortschrittsanzeige, Tooltips und Kontextmenüs ab. Text- und Passwortfelder erhalten ausdrücklich zugeordnete Kontextmenüs; Passwörter bekommen dabei keine Kopieren- oder Ausschneiden-Funktion.

Text erbt seine Vordergrundfarbe wieder vom jeweiligen Steuerelement. Damit überschreibt ein allgemeiner TextBlock-Stil nicht länger die Beschriftungsfarbe von Akzent-Buttons oder ausgewählten Einträgen. Für selbst gewählte Akzentfarben wird eine helle oder dunkle Buttonbeschriftung bestimmt.

Eigene Hinweis-, Fehler- und Bestätigungsfenster ersetzen reguläre MessageBox-Aufrufe. Bei Bestätigungen ist standardmäßig Nein aktiv. Escape schließt ohne Zustimmung. Nur wenn das Theme oder der eigene Dialog selbst fehlschlägt, bleibt eine native MessageBox als letzter Diagnoseweg erhalten.

Native Titelleisten werden auf Windows 11 über die dokumentierten DWM-Attribute an das Anwendungstheme angepasst. Es werden keine Windows-Registry-Schlüssel und keine systemweiten Theme-Einstellungen verändert. Der eingebettete Browser erhält vor der Initialisierung eine dunkle Fläche und übernimmt anschließend das gewählte Farbschema. Steam-Seiten werden nicht per JavaScript, CSS-Injektion oder Farbfilter verändert.

## Grenzen

Datei öffnen, Datei speichern und Ordner auswählen verwenden weiterhin die nativen Windows-Dialoge. Deren Darstellung ist kein Bestandteil der WPF-Steuerelementvorlagen. Steam-Webseiten und Produktbilder behalten ihren eigenen Inhalt und können helle Bereiche enthalten. Auf Windows-Versionen vor Windows 11 wird keine nicht dokumentierte DWM-Alternative aufgerufen; native Titelleisten können dort anders aussehen.

Das ZIP ist weiterhin ein Quellprojekt. Die vorgenommenen Quellprüfungen ersetzen weder den C#-Compiler noch einen Windows-Start oder eine visuelle Prüfung. Ein Screenshot einer laufenden Anwendung wurde nicht erzeugt.

## Installation

Anwendung schließen. Das neue ZIP in einen eigenen Projektordner entpacken und dessen `WorkshopStudio.sln` öffnen. Solution neu erstellen oder `Build.cmd` ausführen. Nicht versehentlich die alte EXE im vorherigen Projektordner starten. Der neue Stand ist unten in der Sidebar als `Version 1.0.1 · Theme-Fix` erkennbar.

Gespeicherte Daten bleiben unter `%LOCALAPPDATA%\WorkshopStudio`. Datenmodell, Speicherpfad, Steam-IDs, Kontospeicher und Core-Veröffentlichungslogik wurden nicht geändert. Es ist keine Datenmigration und kein Löschen des Datenordners erforderlich. Bei bereits lokal verändertem Quellcode zunächst den eigenen Stand sichern und die Änderungen vergleichen, nicht blind überschreiben.

## Kontrolle unter Windows

1. Unter Einstellungen das Theme Dark auswählen und speichern. Hauptbereich und alle zehn Seiten prüfen, auch leere Tabellen und die freie Fläche unter kurzen Seiten.
2. App-, Mod-, Collection- und Kontoeditor, Eingabefenster, Protokollfenster sowie Info-/Bestätigungsfenster öffnen. Alle Tabs, Dropdowns, Tooltips und Kontextmenüs prüfen. In einem Passwortfeld dürfen Kopieren und Ausschneiden nicht auftauchen.
3. Tabulator, Escape, Enter, Pfeiltasten, Mausrad, Scrollbar-Ziehen und horizontales Scrollen prüfen. Tabellen sortieren und Spalten verbreitern. Löschabfrage mit Enter darf standardmäßig nicht zustimmen.
4. Light speichern und anschließend wieder Dark speichern. Auch aktuell geöffnete Dialoge und der eingebettete Browser sollen ihre App-Flächen aktualisieren. Windows-DPI 100/125/150 Prozent und die vorhandene UI-Skalierung prüfen.
5. Anwendung schließen und neu öffnen. Gespeichertes Theme und vorhandene Apps/Mods/Collections müssen weiter vorhanden sein. Für die Theme-Prüfung ist kein Upload notwendig.

## Quellprüfungen

`python scripts/Check-Theme.py` benötigt nur Python 3.10 oder neuer und führt 15 Offline-Strukturprüfungen aus. Dieser zusätzliche Prüfer wurde im Lieferumfeld ausgeführt. Er prüft unter anderem explizite Fensterstile, Ressourcenverweise, Vorlagenteile, Triggerziele und den Erhalt der Palette. Er lädt und rendert kein WPF.

Die Console-Selbsttests enthalten zusätzlich acht Theme-Quellverträge, insgesamt jetzt 46 Testfälle. Die XAML-Quellen werden ausschließlich zur Prüfung in den Ausgabeordner des Testprojekts kopiert. Diese C#-Tests wurden im Lieferumfeld mangels .NET SDK nicht ausgeführt.

## Technische Referenzen

- WPF-Ressourcen und implizite Schlüssel: https://learn.microsoft.com/en-us/dotnet/desktop/wpf/systems/xaml-resources-overview
- Konkretes Verhalten selbst definierter Fenster: https://github.com/dotnet/wpf/issues/10461
- DWM-Titelleistenattribute: https://learn.microsoft.com/en-us/windows/win32/api/dwmapi/ne-dwmapi-dwmwindowattribute
- WebView2-Hintergrund: https://learn.microsoft.com/en-us/dotnet/api/microsoft.web.webview2.wpf.webview2.defaultbackgroundcolor
- WebView2-Farbschema: https://learn.microsoft.com/en-us/dotnet/api/microsoft.web.webview2.core.corewebview2profile.preferredcolorscheme
