# 📋 Config und Befehle

**Config / Befehle anzeigen** öffnet ein eigenes Fenster mit frei markierbarem Text. Es zeigt vorhandene Dateien aus der ausgewählten lokalen Mod-Quelle. Ein reiner Remote-Metadatenimport kann keine unbekannten Configdateien rekonstruieren.

## 👀 Originaltext und erkannte Werte

![Config-Fenster mit kopierbarem INI-Originaltext und Kopierknöpfen](https://raw.githubusercontent.com/ZUMBYTE-AppSolution/WorkshopStudio/main/docs/images/wiki/config-original.png)

**① Datei auswählen → ② „Originaldateien“ → ③ Text markieren → ④ „Auswahl kopieren“ oder Strg+C.** „Alles kopieren“ übernimmt den kompletten aktuellen Text. Der Reiter „Erkannte Optionen“ zeigt dieselben Beispielwerte als statische Schlüssel:

![Config-Fenster mit statisch erkannten Optionen](https://raw.githubusercontent.com/ZUMBYTE-AppSolution/WorkshopStudio/main/docs/images/wiki/config-options.png)

[INI-, JSON- und Lua-Beispiele zum Kopieren](Beispiele)

## Erkannte Dateien

- INI- und CFG-Dateien.
- YAML/YML-Dateien mit statisch lesbaren Schlüssel-/Wertangaben.
- JSON-Dateien, deren Name `config` enthält.
- Lua-Dateien, deren Name `config` oder `sandbox` enthält.
- Zomboids `sandbox-options.txt`.

Die Erkennung ist begrenzt und führt keine Mod-Skripte aus. Die Standardgrenze pro Datei ist 512 KiB und lässt sich einstellen. Binärdaten, ungültige UTF-8-Texte, zu große Dateien und Verknüpfungen werden ausgelassen oder als Hinweis gemeldet. Die Suche bleibt in den ausgewählten Mod-Stämmen.

## Drei Ansichten

**Originaldateien** enthält den vollständigen gelesenen Text einschließlich Kommentaren und Zeilenstruktur. In der Dateiauswahl die gewünschte Datei wählen. Frei markieren oder mit Strg+A alles markieren und Strg+C kopieren. **Auswahl kopieren**, **Alles kopieren** und **Text exportieren** sind zusätzlich verfügbar.

**Erkannte Optionen** zeigt statisch gelesene Schlüssel und Werte. Bei INI/CFG werden Abschnitte berücksichtigt. JSON-Objekte werden als Schlüsselpfade gelesen. In Lua werden nur einfache Literale wie Zahl, Zeichenkette oder `true`/`false` erkannt. Berechnete oder dynamische Lua-Werte werden nicht erraten. Ein Parserfehler nimmt dir nicht den lesbaren Originaltext.

**Server-Einbindung** enthält bei Zomboid die getrennt generierten `WorkshopItems=`-/`Mods=`-Zeilen. Sie sind eine Einbindungshilfe und nicht die Mod-Konfiguration.

## Suchen, Kopieren und Datenschutz

Die Suche markiert den nächsten Treffer im Originaltext und beginnt bei Bedarf wieder am Anfang. Zeilenumbruch ist zuschaltbar. Beim Kopieren der gesamten Datei wird der vollständige Text verwendet, nicht nur ein Suchtreffer.

Erkennt Studio mögliche Passwort-, Token- oder Schlüsselbezeichnungen, erscheint ein Hinweis. Rohtext kann solche Werte tatsächlich enthalten. Beim Textexport einer entsprechend erkannten Datei wird bewusst bestätigt. Vor dem Teilen den Inhalt selbst prüfen.

**Lokale Quelle fehlt** bedeutet, dass keine geeignete Arbeitskopie verfügbar ist. **Keine Konfiguration gefunden** erscheint erst nach einer abgeschlossenen Suche ohne Treffer. Das Fenster bearbeitet die Quelldateien nicht und führt keine Befehle daraus aus.
