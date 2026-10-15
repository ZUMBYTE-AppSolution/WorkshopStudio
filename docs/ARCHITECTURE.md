# Architektur

## Verantwortlichkeiten

`WorkshopStudio.Core` ist unabhängig von WPF. `WorkspaceStore` speichert ein schema-versioniertes JSON-Modell atomar und erhält den vorigen Stand als `.bak`. `ModSourceResolver` erkennt Mod-Quellen und erstellt ausdrückliche Source-to-Target-Mounts. `PackageBuilder` validiert diese Quellen, erstellt einen separaten Snapshot, berechnet SHA-256-Werte, schreibt VDF/Manifest und archiviert erfolgreiche Pakete. `SteamCmdRunner` kapselt einen einzelnen Kindprozess samt Ein-/Ausgabe, Timeout, Abbruch und Passwortmaskierung. `SteamService` lädt öffentliche Metadaten, Bilder und Collection-Informationen. `CollectionExporter` erzeugt JSON-/INI-kompatible Listen. `TemplateCreator` legt ausschließlich neue Mod-Verzeichnisse an.

`WorkshopStudio.Desktop` besteht aus der WPF-Shell, wiederverwendbaren Themes, zehn Ansichten und modalen Editoren. `MainViewModel` koordiniert UI, Warteschlange, Kontoauswahl und Automation. Die Konfigurationsmodelle sind absichtlich einfache serialisierbare Objekte. Nach strukturellen Änderungen aktualisiert das ViewModel die gebundenen Ansichten. Editoren bearbeiten Klone, erst Speichern übernimmt sie. Einstellungen werden an einen separaten Entwurf gebunden. Erst validiertes Speichern übernimmt sie; laufende Automationen lesen niemals unbestätigte Einstellungsentwürfe.

## Release-Zustände

```text
Pending → Preparing → Uploading → Succeeded
                     └──────────→ Failed / Cancelled
          └─────────────────────→ Skipped (identischer Fingerprint)
App-Neustart: Preparing/Uploading → Interrupted
```

Test und Live haben getrennte Workshop-IDs, Fingerprints und Reconciliation-Flags. Vor einer möglichen Erstveröffentlichung wird das entsprechende Reconciliation-Flag persistent gesetzt. Sobald eine reale ID aus der von SteamCMD geschriebenen VDF vorliegt, wird diese gespeichert. Erst eine eindeutige positive SteamCMD-Ausgabe, Prozesscode 0 und eine ID > 0 markieren einen Upload als erfolgreich. Bei unklarer Antwort kann deshalb ein manuell erfolgreicher Upload konservativ als ungeklärt/fehlgeschlagen erscheinen. Dies verhindert falsche Erfolgsanzeigen und vorschnelles erneutes Erstellen.

Optionale Paket-Backups erfolgen nach der Speicherung des bestätigten Erfolgs. Ein Backupfehler darf die bekannte Steam-ID nicht verlieren. Abbruch beendet den lokalen Prozessbaum; Steam-seitige Verarbeitung ist nicht rückrollbar. Ein bereits vorhandener Steam-Eintrag wird nie automatisch gelöscht.

## Paketierung

`ModSourceResolver.Discover` erkennt direkte Mods, `mods/`, `Contents/` und Workshop-Projektwrapper mit begrenzter, nicht rekursiver Metadatenentdeckung. `Resolve` liefert für Zomboid nur ausgewählte Mod-Stämme als `PackageSource` mit Zielpräfix `mods/<Ordnername>`. Der Standardmodus ist `SingleMod`; mehr als ein Kandidat ohne explizite gespeicherte Auswahl wird abgewiesen. Eine Auswahl von `42/` wird auf ihren passenden Mod-Stamm normalisiert, damit gemeinsame Assets nicht verlorengehen. Ein direkt unter `mods/` liegender Mod-Ordner namens `42` wird dabei nicht zum gesamten Container erweitert.

Dateikopie und Watcher verwenden dieselben aufgelösten Mounts. `PackageSnapshot` trägt Plan, vollständige Dateiliste und Verzeichnisse für die UI-Vorschau. Es wird nicht mehr rekursiv der gesamte rohe `ContentFolder` als Workshop-Inhalt übernommen. Spiele- und Steam-Wurzelerkennung ist unabhängig vom optionalen Metadaten-Validierungsfeld aktiv.

Quellen bleiben unangetastet; Uploads lesen ausschließlich den erzeugten Staging-Inhalt. Laufwerkswurzeln, sich überlappende Studio-/Quellverzeichnisse und nicht ausgeschlossene Links/Junctions werden abgelehnt. Der Dateifilter kombiniert globale, App- und Mod-Regeln. Anschließend zählen Dateianzahl und Paketgröße. Eigene Marker schützen die Staging-Bereinigung gegen das Löschen beliebiger Ordner.

Der Fingerprint umfasst Dateipfade, Bytes, Dateihashes, sortierte Verzeichnisse (einschließlich leerer Ordner), AppID, Kanal, relevante Textmetadaten und den Vorschaubild-Hash. Eine geänderte Workshop-ID zählt selbst nicht als Inhaltsänderung. Eine nur zeitbedingt andere Expansion von `{date}` erzwingt keinen Upload eines sonst unveränderten Pakets. Mit `OnlyChanged=false` kann ein erneuter Upload bewusst erzwungen werden.

Ein Snapshot wird Datei für Datei erzeugt. Bei gleichzeitigem Umbau vieler Quelldateien ist keine atomare Git-Revision garantiert. Für Live-Veröffentlichungen einen fertig gebauten, unveränderten Release-Ordner verwenden. Externe Build-Schritte und Shell-Hooks werden nicht heimlich ausgeführt; sie sind in dieser Version nicht implementiert.

## Automation

Dateiänderungen werden ausschließlich innerhalb der ausgewählten Source-Mounts über `FileSystemWatcher` gesammelt und mit einer Ruhezeit entprellt. Intervall- und Tageszeitpläne laufen über einen WPF-Timer. Die geöffnete Anwendung ist der Scheduler. Bei Prozessende gibt es keine Hintergrundinstanz. Schlafende Rechner lösen keine garantierten Jobs aus; beim nächsten Timerlauf wird die aktuelle Fälligkeit geprüft. Die Tageszeit gilt in der lokalen Windows-Zeitzone. Nach Programmneustart kann ein bereits überschrittener Tageszeitpunkt erneut fällig sein; der Inhaltsvergleich vermeidet normalerweise Doppel-Uploads unveränderter Pakete.

Nur automatisch eingereihte Jobs werden durch einen Automationslauf gestartet. Von Hand eingereihte Releases warten weiter auf eine bewusste Freigabe. Ohne globale Live-Freigabe ist automatisches Live-Publishing blockiert. Unbekannte Erst-Upload-IDs stoppen Wiederholungen.

## Erweiterungen

Neue App-Profile benötigen für grundlegende VDF-Inhalte keine neue UI. Für spieleigene Validatoren und SDK-Publisher ist eine separate Adapter-Schicht sinnvoll; derzeit existiert ein gemeinsamer SteamCMD-Publisher. Neue WPF-Seiten werden im `MainWindow`, der Navigation und dem ViewModel registriert. Theme-Ressourcen sind zentral und müssen nicht in einzelnen Ansichten überschrieben werden.
