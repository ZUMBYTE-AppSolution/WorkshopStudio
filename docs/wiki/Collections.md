# 🗂️ Collections

Eine lokale Collection organisiert eigene Mod-Projekte und externe Workshop-IDs für ein Spiel. Sie kann mit einer echten Steam-Collection verknüpft werden. Lokale Organisation und Änderungen auf Steam sind unterschiedliche Vorgänge.

## Lokal zusammenstellen

Unter **Collections** eine neue Sammlung anlegen, Spielprofil wählen und eigene Projekte zuordnen. Externe IDs dienen als Verweise auf fremde oder anderweitig verwaltete Einträge. Sie werden nicht als eigene Dateien heruntergeladen und erneut hochgeladen.

Eigene Mods können gemeinsam in die Upload-Warteschlange eingereiht werden. Dabei bleibt jede Mod ein eigener Auftrag mit eigenem Ziel und Ergebnis. Die Collection macht mehrere Uploads nicht zu einer einzigen Transaktion.

## Mit Steam verknüpfen

Eine bestehende Collection über ihre Workshop-ID bzw. URL importieren oder die Erstellung im originalen Steam-Webeditor öffnen. Nach dem Speichern die entstandene ID verknüpfen. **Aktualisieren** liest den aktuellen Steam-Mitgliederstand, überschreibt aber nicht stillschweigend die lokale Auswahl.

SteamCMD besitzt in diesem Tool keinen erfundenen Collection-Upload. Titel, Mitglieder und sonstige Eigenschaften der echten Steam-Collection werden im Steam-Webeditor bearbeitet. Importierte Collection-Metadaten bleiben lokal sichtbar.

## IDs und Zomboid-Serverzeilen

Studio kann die aufgelösten Workshop-IDs kopieren oder exportieren. Für eigene Projekte wird die ID des gewählten Kanals verwendet. Fehlt noch eine erforderliche veröffentlichte ID, wird ein unvollständiger Export nicht als fertige Serverliste ausgegeben.

Für Project Zomboid kann Studio zusätzlich zwei Zeilen erzeugen:

```ini
WorkshopItems=123456789;987654321
Mods=TechnischeModID;WeitereModID
```

Die Zahlen und Mod-IDs im Beispiel sind Platzhalter. `WorkshopItems=` enthält Steam-Veröffentlichungs-IDs, `Mods=` die technischen IDs der Mod-Dateien. Diese Werte dürfen nicht vertauscht werden. Studio dedupliziert aufgelöste IDs und prüft die Spielzuordnung.

Der Export verändert keine Serverdatei. Die Zeilen müssen nach Prüfung in die passende Serverkonfiguration übernommen werden. Die generierten Zeilen ersetzen nicht die Original-Konfigurationsdateien einer Mod.
