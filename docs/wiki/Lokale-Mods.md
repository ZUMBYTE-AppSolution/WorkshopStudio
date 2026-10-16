# 📂 Lokale Mods und Ordner

Studio arbeitet mit den vorhandenen Mod-Quellen. Es erzeugt die Uploadstruktur in einer getrennten Paketablage. Du musst deine Spielinstallation dafür nicht umbauen.

## Mehrere lokale Mods finden und zuordnen

**① Diesen Knopf unter „Mods & Resources“ verwenden:**

![Aktionsleiste: Mehrere Mods auswählen](https://raw.githubusercontent.com/ZUMBYTE-AppSolution/WorkshopStudio/main/docs/images/wiki/mods-actions.png)

Unter **Mods & Resources → Mehrere Mods auswählen** einen Stammordner wählen, beispielsweise `D:\MeineMods`. Studio durchsucht begrenzt dessen Unterordner und zeigt jede erkannte Zomboid-Einzelmod separat. Es erkennt B41-Strukturen mit `mod.info` und B42-Stämme mit versionierten Ordnern sowie gemeinsamen Daten.

Vorschläge verwenden bereits gespeicherte Quellpfade, technische Mod-IDs und eindeutige kompatible Namen. Eine gleiche Bezeichnung allein ist kein sicherer Schlüssel. Bei mehreren möglichen Quellen oder Zielen wird nicht stillschweigend entschieden.

Im Zuordnungsdialog pro Treffer das Ziel prüfen. Einen Treffer überspringen, einem vorhandenen Profil zuordnen oder als neues lokales Profil anlegen. Erst **Übernehmen** speichert die Pfade. Vor der Änderung wird der bisherige Workspace gesichert. Bestehende Workshop-IDs und Metadaten bleiben bestehen; neue Profile erhalten keine Workshop-ID. Auto-Publish wird für die zugeordneten Profile ausgeschaltet, damit neue Quellen zuerst geprüft werden können.

Doppelte Ziele, widersprechende technische IDs, die Studio-Datenablage und Profile mit wartenden Uploadjobs werden abgewiesen.

**② Zielprofil pro Mod prüfen:** Im oberen Demotreffer wurde eine technische Mod-ID eindeutig einem vorhandenen Workshop-Profil zugeordnet. Der untere Treffer bleibt ohne eindeutige Zuordnung auf **Überspringen**.

![Ausschnitt der Zielauswahl im Zuordnungsdialog](https://raw.githubusercontent.com/ZUMBYTE-AppSolution/WorkshopStudio/main/docs/images/wiki/local-mod-targets.png)

**③ Erst danach „Zuordnungen übernehmen“ wählen.** Dadurch werden Pfade gespeichert; es startet noch kein Upload.

## Einzelmod auswählen

Im Mod-Editor **Einzelne Mod auswählen** verwenden. Bei Zomboid ist der Mod-Stamm gemeint, nicht nur dessen `42/`-Ordner. Bei B42 müssen passende Versionsordner und `common/` gemeinsam erhalten bleiben. Studio zeigt die erkannten Quellen und Upload-Zielpfade an.

```text
MeineMod/
  common/          # falls für die Mod vorhanden
  42/
    mod.info
    media/
```

Beim Paketieren entsteht daraus im Staging `mods/MeineMod/...`. Ein leerer erforderlicher gemeinsamer Ordner kann im Staging ergänzt werden. Die Quelle selbst bleibt unverändert.

## Ein bewusstes gemeinsames Workshop-Paket

Wenn eine Workshop-ID mehrere Einzelmods enthalten soll, im Mod-Editor **Mehrmod-Paket zusammenstellen** wählen. Nur die ausdrücklich gespeicherte Auswahl wird paketiert. Ein später neu hinzugefügter Nachbarordner erweitert diese Auswahl nicht automatisch. Fehlt eine gewählte Mod, wird sie nicht durch irgendeinen Nachbarn ersetzt.

Ein übergeordneter Sammelordner darf deshalb nicht ohne konkrete Auswahl zum Uploadinhalt werden. Die Listenaktion zum Zuordnen mehrerer Einzelmods und die gemeinsame Paketauswahl sind unterschiedliche Arbeitsabläufe.

## Grenzen und Filter

Spielinstallation, Steam-/Bibliothekswurzel, Dateisystemwurzel und Studio-Datenordner werden als Paketquelle gesperrt. Junctions, symbolische Links, Pfad-Traversal und doppelte technische IDs werden geprüft. Diese Einzelmod-Grenze lässt sich nicht mit einem generischen Spieleprofil umgehen.

Globale, spielbezogene und modbezogene Ausschlüsse werden kombiniert. Die Paketvorschau ist die verbindliche Kontrolle: Dateiliste, Quellen, Zielpfade, Dateianzahl, Größe und SHA-256 zeigen, was tatsächlich hochgeladen würde.
