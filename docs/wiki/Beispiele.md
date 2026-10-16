# 🧪 Beispiele zum Kopieren

Diese Beispiele verwenden erfundene Projektnamen und harmlose Werte. Sie zeigen Dateiformate und erwartetes Verhalten. Sie erzeugen selbst keine Workshop-Veröffentlichung. Die [Beispieldateien im Repository](https://github.com/ZUMBYTE-AppSolution/WorkshopStudio/tree/main/docs/examples) können einzeln geöffnet und kopiert werden.

## 📝 Beispiel 1: Beschreibung, Version und Changelog

Angenommen, dein bestehender Steam-Eintrag heißt **RP Events**, beschreibt bisher Version **2.0.0** und ist einem lokalen Mod-Stamm zugeordnet. Die neue lokale Struktur sieht so aus:

```text
RPEvents-Projekt/
  workshop.txt
  version.txt
  CHANGELOG.md
  Contents/
    mods/
      RPEvents/
        42/
          mod.info
          media/
```

`workshop.txt` liegt auf der zugehörigen Projektebene. Mehrere Beschreibungszeilen sind erlaubt:

```ini
title=RP Events
description=Zusätzliche RP-Ereignisse für unseren Server.
description=
description=Version: 2.1.0
description=Neu: Begrüßung und einstellbare Ereignisanzahl.
```

`version.txt` enthält nur die ausdrücklich gewünschte Autoren-Version:

```text
2.1.0
```

`mod.info` enthält weiterhin eine stabile technische Mod-ID:

```ini
name=RP Events
id=RPEvents
version=2.1.0
description=Zusätzliche RP-Ereignisse.
```

`CHANGELOG.md` trennt die neue Notiz von alten Releases:

```markdown
# Changelog

## [2.1.0] - 2026-10-16
### Hinzugefügt
- Begrüßung für neue Spieler.
- Ereignisanzahl über die Config einstellbar.

## [2.0.0] - 2026-10-01
- Erste Ereignisgruppe veröffentlicht.
```

### ✅ Was Studio daraus entscheidet

| Feld | Bisher / lokal | Ergebnis |
|---|---|---|
| Titel | Beide heißen RP Events | Beibehalten. |
| Beschreibung | Steam 2.0.0 / lokaler neuer Text 2.1.0 | Neue mehrzeilige Beschreibung übernehmen, sofern kein eigener abweichender Editorwert Vorrang hat. |
| Version | Lokal ausdrücklich 2.1.0 | Im Profil verwenden; eine vorhandene einfache Versionszeile entsprechend aktualisieren. |
| Änderungsnotiz | Abschnitt 2.1.0 noch nicht veröffentlicht | Nur diesen aktuellen Abschnitt anhängen. Der Abschnitt 2.0.0 wird nicht erneut angehängt. |
| Workshop-ID | Bestehende Live-ID | Unverändert als Ziel verwenden. |
| Bild | Kein Workshop-Bild ausdrücklich gewählt | Bisheriges Steam-Bild erhalten. |
| Sichtbarkeit | Kein automatisches Sichtbarkeits-Opt-in | Bestehenden Steam-Wert erhalten. |

Beim nächsten Durchlauf ohne neue Änderung wird dieselbe Notiz nicht erneut übertragen. Wenn du stattdessen im Mod-Editor bewusst eine eigene Beschreibung oder eigene Notiz eingibst, gilt deren dokumentierter Vorrang. Die technischen IDs aus `mod.info` sind keine Steam-Workshop-IDs.

![Entscheidung des Metadatenvergleichs an Demodaten](https://raw.githubusercontent.com/ZUMBYTE-AppSolution/WorkshopStudio/main/docs/images/wiki/metadata-decisions.png)

## 📋 Beispiel 2: Configdateien

### INI mit Kommentar und Abschnitt

```ini
[Server]
; Beispielwerte für das Wiki
Greeting=Grüße aus Workshop Studio
EnableWelcome=true
MaxEvents=8
```

**Originaldateien** zeigt alle fünf Zeilen einschließlich Kommentar. **Erkannte Optionen** ordnet `Greeting`, `EnableWelcome` und `MaxEvents` dem Abschnitt `Server` zu. Die App führt diese Werte nicht aus und schreibt sie nicht automatisch in einen Gameserver.

### JSON in einer Datei namens `config.json`

```json
{
  "welcome": {
    "enabled": true,
    "message": "Willkommen auf dem Server"
  },
  "maxEvents": 8
}
```

Die statische Ansicht zeigt unter anderem `welcome.enabled`, `welcome.message` und `maxEvents`. Der Originaltext bleibt unabhängig davon kopierbar.

### Lua in einer Datei namens `config.lua`

```lua
WorkshopDemoConfig = {
    EnableWelcome = true,
    MaxEvents = 8,
    Message = "Willkommen auf dem Server",
    DynamicValue = getServerOptions(),
}
```

Studio erkennt die einfachen Literale der inneren Zeilen. Für `getServerOptions()` wird kein Ergebnis berechnet: Die Funktion wird nicht ausgeführt. Die vollständige Zeile steht weiterhin im Originaltext.

### YAML mit einfachen Werten

```yaml
enableWelcome: true
maxEvents: 8
message: Willkommen auf dem Server
```

Die einfachen Schlüssel-/Wertpaare können statisch angezeigt werden. Die App ist kein vollständiger YAML-Interpreter für alle komplexen Datentypen.

## 💾 Beispiel 3: Eine Sicherung erkennen

```text
backups/workshop-items/<deine Workshop-ID>/Live/
  before-<Zeitstempel>-<Kennung>.zip
  before-<Zeitstempel>-<Kennung>.zip.sha256
```

Das ZIP enthält `content/`, `metadata/`, `backup-manifest.json` und `RESTORE.txt`. Die passende Prüfsummendatei liegt daneben. Für ein Rücksetzen die [Wiederherstellungsanleitung](Backups-und-Wiederherstellung) verwenden und die bestehende Workshop-ID beibehalten.

## 🔗 Beispiel 4: Zomboid-Servereinbindung

```ini
WorkshopItems=<deine numerische Live-Workshop-ID>
Mods=RPEvents
```

Die Platzhalterzeile ist nicht direkt spielbereit. Die reale numerische ID aus deiner eigenen Veröffentlichung einsetzen oder die korrekten Zeilen in Studio erzeugen. `Mods=RPEvents` stammt aus der technischen `id=` in `mod.info`.
