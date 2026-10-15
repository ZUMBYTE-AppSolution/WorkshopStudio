# Lieferprüfung 1.0.3

## Tatsächlich ausgeführt

- `python scripts/Check-Theme.py`: **15/15** Offline-Strukturprüfungen bestanden. Vollständige Ausgabe: `THEME_SOURCE_CHECKS.txt`.
- `python scripts/Check-Login.py`: **22/22** Offline-Quell-/Assetprüfungen bestanden. Vollständige Ausgabe: `LOGIN_SOURCE_CHECKS.txt`.
- `python scripts/Check-Packaging.py`: **20/20** Offline-Quellverträge bestanden. Vollständige Ausgabe: `PACKAGING_SOURCE_CHECKS.txt`.
- Zusammen **57 Offline-Prüfungen**. XML/XAML, Ressourcen, Eventhandler, Icon-Verknüpfungen, Paketplan-Integration, Quellenauswahl, Watcher-Zuordnung und Dokumentation wurden auf strukturelle Konsistenz geprüft.
- Diff gegen den gelieferten Quellstand 1.0.2: Dateiliste in `CHANGED_FILES_1.0.3.txt`. Keine frühere Quelldatei entfernt.
- Das ZIP wurde nach dem Erstellen auf lesbare Dateien und CRC-Integrität geprüft. Ein separater SHA-256-Wert wird neben dem ZIP ausgegeben.

Diese Prüfungen lesen Quelltexte, Projektdateien und Assets. Insbesondere Check-Packaging.py führt **keine C#-Paketierung** aus. Ein grüner Quellvertrag beweist nicht das vollständige Laufzeitverhalten. Das Login-Skript prüft repräsentative Prompt-Ausdrücke mit der gemeinsamen Python/.NET-RegEx-Teilmenge, nicht innerhalb der .NET-Laufzeit.

## Implementierte, aber hier nicht ausgeführte C#-Tests

Die SelfTest-Suite enthält **102 registrierte Testfälle**, davon **35 neue Paketierungs-/Mod-Auswahl-Regressionstests**. Neue Fälle betreffen Einzelmods, Wrapper, B42, gemeinsame und leere Ordner, ausdrückliche Mehrmod-Auswahl, neue Nachbarmods, Quellenintegrität, Filter, doppelte IDs, Game-Root-Sperren, alte JSON-Profile und numerische Mod-Ordnernamen.

`Build.cmd` führt Restore, Build und die gesamte Suite vor dem Erstellen des Windows-Anwendungspakets aus. Die vorhandenen Steam-Prozesstests verwenden ein lokales Simulationsprogramm, keine Steam-Zugangsdaten.

## Nicht ausgeführt

Im Arbeitsumfeld gab es kein .NET SDK und keinen Windows/WPF-Host. Daher **kein bestätigter C#-Build, keine ausgeführten C#-Selbsttests, kein WPF-Renderingtest, keine DPI-Abnahme, keine Windows-ConPTY-Ausführung und kein echter Steam-Login oder Upload**. Das ZIP enthält Quellcode, keine kompilierte EXE.

Der Fehlerpfad der alten Contents-Prüfung wurde direkt im gelieferten Quellstand gefunden. Er liegt vor der Staging-Erstellung. Die neue Implementierung korrigiert diesen Codepfad und ergänzt Auswahl und Schutzprüfungen; eine erfolgreiche Veröffentlichung auf dem Zielrechner wird nicht behauptet.

Das frühere generierte Bild war ein Entwurf, kein Screenshot. MainWindow und Dashboard wurden für diese Version neu an dessen Aufteilung angelehnt. Ohne Windows-Sichtprüfung wird keine pixelgenaue Übereinstimmung behauptet. Anleitung und Abnahme: `DESIGN_AND_MOD_SCOPE_1.0.3.md` und `TESTPLAN.md`.
