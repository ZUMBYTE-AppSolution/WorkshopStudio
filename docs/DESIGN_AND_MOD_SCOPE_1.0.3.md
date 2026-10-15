# Version 1.0.3: tatsächliche Oberfläche und richtige Mod-Quellen

## Das frühere Bild

Das generierte Bild war ein Designentwurf mit einer dargestellten Beispieloberfläche, kein Screenshot aus der laufenden Anwendung. Der vorherige WPF-Quellstand setzte dessen Layout nicht um. Dieser Unterschied hätte bei der Lieferung des Bildes ausdrücklich benannt werden müssen.

Jetzt sind MainWindow.xaml, DashboardView.xaml, ihre Bindings und das zentrale Theme umgebaut: aktive orange Navigation, obere Suchleiste, vier Symbol-KPIs, kompakte Spielkarten mit echten Steam-Bildern, sechs Schnellaktionen und Aktivitätsbereich rechts. Schmalere Fenster stellen die Aktivität darunter dar. Die Anthrazit-Orange-Familie, vorhandene Funktionen und Icon bleiben erhalten. Individuelle Farben und das Light-Theme funktionieren über dieselben Ressourcen. Industriekulisse, Werbetexte und erfundene Zahlen aus dem Bild sind keine Programmfunktionen und wurden nicht übernommen.

Keine Sichtprüfung unter Windows erfolgt. Deshalb kein Versprechen eines pixelgleichen Ergebnisses oder fehlerfreier Darstellung bei jeder DPI.

## Warum die Fehlermeldung auftrat

Der frühere PackageBuilder verlangte direkt `mods/` unter dem gewählten Inhaltsordner. Er vermischte damit deine Entwicklungsquelle mit der Struktur eines vorbereiteten Workshop-Uploads. Die Fehlermeldung über Contents war folglich kein Beleg dafür, dass im Spiel ein Content-Ordner fehlen würde. Nach erfolgreicher Validierung hätte der alte Builder den gesamten gewählten Inhalt rekursiv kopiert. Die konkret genannte Fehlermeldung stoppte den Vorgang jedoch bereits vor Dateikopie und Upload.

## Neuer Standard: eine eigene Mod pro Profil

Quelle direkt auswählen:

```text
D:/RP/mods/rp-core/
├── mod.info             B41-Variante
└── media/
```

Oder eine versionierte Quelle:

```text
D:/RP/mods/rp-core/
├── common/
└── 42/
    ├── mod.info
    └── media/
```

Im privaten Staging erzeugt Studio selbst:

```text
content/
└── mods/
    └── rp-core/
        └── ...nur die Dateien dieser Mod...
```

`content/` ist hier lediglich der Arbeitsordner für SteamCMD; `workshop.vdf` verweist auf ihn. Es wird kein zusätzlicher `Contents/`-Ordner in den Mod-Inhalt geschachtelt. Studio verändert oder verschiebt deine Quellen nicht. Fremde Mod-Inhalte dürfen nur mit den erforderlichen Rechten erneut veröffentlicht werden.

## Bestehende Profile umstellen

1. Anwendung schließen und Workspace sichern. Datenordner nicht löschen.
2. Neues Quellarchiv separat entpacken und Build.cmd oder Visual Studio verwenden. Versionsanzeige: `Version 1.0.3 · Mod-Auswahl`.
3. Bestehendes Mod-Profil bearbeiten und **Mod-Ordner wählen**. Den echten Unterordner der eigenen Mod wählen, nicht die gesamte Spielinstallation. Bei einer Auswahl von `mods/` mit mehreren Einträgen folgt eine Mod-Auswahl.
4. **Struktur & IDs prüfen**, speichern und in der Mod-Liste **Upload-Inhalt prüfen**. Alle Quellen, Zielpfade, Dateianzahl und Größe überprüfen. Erst danach bewusst für den Test-Kanal einreihen.

Ein alter Contents-Wrapper mit genau einer Mod bleibt lesbar. Ein alter Wrapper mit mehreren Mods fordert jetzt eine Entscheidung; es wird nicht automatisch alles übernommen. Die damalige Paketauswahl heißt seit 1.1.1 **Mehrmod-Paket zusammenstellen**: Ausschließlich die angeklickten, gespeicherten Ordner gehören zum gemeinsamen Paket. Neue Nachbarmods erweitern die Auswahl nicht. Collection-Mitgliedschaften sind unabhängig davon.

## Grenzen und Prüfstand

Die Sicherheitsprüfung erkennt typische Spielinstallationen. Sie kann private Daten in einer absichtlich ausgewählten Mod nicht universell erkennen. Dateifilter und Vorschau sind weiterhin erforderlich. Symlinks/Junctions werden abgewiesen. Filter gelten sowohl relativ zur Einzelmod als auch relativ zum gesamten Uploadziel. Nach den Filtern müssen die benötigten mod.info-Dateien im Paket noch vorhanden sein.

Die Dateilisten-Vorschau umfasst alle Dateien, nicht nur die ersten 500. Sie kann gefiltert und als JSON exportiert werden. Im Vorschaufenster findet kein Upload statt. Eine Vorschau legt wie bisher ein lokales Staging-Paket an; dieses lässt sich über die Einstellungen bereinigen.

Nur Offline-Quellprüfungen wurden tatsächlich ausgeführt. C#-Regressionstests sind enthalten, aber ohne .NET SDK nicht ausgeführt. Kein kompiliertes EXE, kein bestätigter WPF-Lauf und kein echter Steam-Upload in dieser Lieferung. Details: DELIVERY_CHECKS.md und TESTPLAN.md.
