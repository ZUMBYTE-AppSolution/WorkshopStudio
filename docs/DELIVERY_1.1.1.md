# Lieferung 1.1.1 · Lokale Mods finden und Profilen zuordnen

Die bestehende Workshop-Studio-Anwendung wurde erweitert. Unter **Mods & Resources → Mehrere Mods auswählen** lässt sich ein lokaler Stammordner wählen. Die Anwendung sucht darin nach B41- und B42-Einzelmods, einschließlich verschachtelter Workshop-Projekte. Jede gefundene Mod wird einzeln angezeigt. Eindeutige Treffer zu vorhandenen Profilen sind vorgeschlagen; die Workshop-ID des Zielprofils steht im Dialog. Unklare Einträge bleiben zunächst übersprungen und lassen sich manuell zuordnen oder als neues lokales Profil anlegen.

Beim Übernehmen wird der einzelne Mod-Stamm als Quellpfad gesetzt. Vorhandene Workshop-IDs und sonstige Metadaten bleiben erhalten. Neue Profile erhalten Workshop-ID 0. Die Mod-Dateien werden nicht verändert. Vor dem Speichern wird der bisherige Workspace gesichert; ein ungültiger Treffer verhindert die gesamte Änderung. Auto-Publish ist für zugeordnete Profile aus. Die bisherige Aktion für ein bewusst gemeinsames Upload-Paket heißt jetzt **Mehrmod-Paket zusammenstellen** und steht weiter im Mod-Editor.

## Durchgeführte Prüfungen

- Windows/.NET-Release-Build und selbständiges Windows-Paket: erfolgreich.
- C#-Selbsttests: 123/123 bestanden. Neue Fälle decken verschachtelte Ordner, B42, Schutz vor Spielinstallationsordnern, Profilvorschläge und fehlersichere Pfadänderungen ab.
- WPF-Smoke-Test: Dashboard, Bibliothek, Config-Kopieren, Iconify-Fensterknöpfe und das neue Zuordnungsfenster gerendert. Zwei verschachtelte lokale Testmods wurden gefunden; ein bestehendes Profil wurde über technische Mod-ID vorgeschlagen.
- Offline-Quellprüfungen und Paketinhalt geprüft.

## Noch offen

Der tatsächliche Mod-Ordner des Nutzers muss in der Anwendung gewählt und die angezeigten Profil-/Workshop-ID-Zuordnungen müssen dort geprüft werden. Bei einem nicht eindeutig passenden Namen ist manuelle Zuordnung erforderlich. Echte Steam-Veröffentlichungen und die Abnahme im Spiel wurden nicht ausgeführt. Die übrigen offenen Punkte des [Lieferberichts 1.1.0](DELIVERY_1.1.0.md) gelten weiter.
