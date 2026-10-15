# Lieferung 1.2.0 · Lokale Mod-Zuordnung, Dashboard-Liste und GitHub-Updater

Die bestehende C#/.NET-WPF-Anwendung verwaltet nun lokale Einzelmods aus einem gewählten Stammordner. **Mods & Resources → Mehrere Mods auswählen** erkennt B41-/B42-Mods in Unterordnern, schlägt eindeutige bestehende Profile vor und übernimmt nach Sichtprüfung jeden Mod-Stamm als eigenen Quellpfad. Workshop-IDs und Metadaten bleiben erhalten; Auto-Publish wird für zugeordnete Profile ausgeschaltet. Die bewusste gemeinsame Paketauswahl bleibt separat im Mod-Editor.

Im Dashboard werden Spiele in kompakten Listeneinträgen mit proportional skaliertem Bild links und Informationen rechts dargestellt. Bei ausreichender Breite stehen zwei nebeneinander. Die eigenen Fensterknöpfe bleiben beim Maximieren im sichtbaren Monitorbereich. Der feste Tool-Updater prüft das neueste stabile Release bei GitHub. Nach Bestätigung lädt er das portable Windows-ZIP, prüft die SHA-256-Datei, entpackt mit Pfadkontrolle und installiert nach einer weiteren Bestätigung beim Neustart. Vorhandene Programmdateien werden für eine Rücknahme bei Kopierfehlern gesichert. Workspace und Steam-Daten liegen außerhalb des Installationsordners.

## Geprüft

- Windows/.NET-10-Release-Build ohne Warnungen und Fehler; 131/131 C#-Selbsttests bestanden.
- 15 Theme-, 20 Paketierungs- und 22 Login-Quellprüfungen bestanden.
- WPF-Smoke-Test mit isoliertem Workspace: zwei kompakte Dashboard-Spiele nebeneinander, Bibliothek, Iconify-Fensterknöpfe auch im maximierten Monitorbereich, verschachtelte lokale Mod-Zuordnung und kopierbarer Config-Originaltext gerendert.
- Updater-Tests für offizielle Release-Metadaten, Fremd-URL, Entwurf, SHA-256-Dateiname, Paketstruktur, falsche Version und ZIP-Pfadgrenze bestanden.

## Noch offen

Ein tatsächlicher Mod-Stammordner des Nutzers muss in der Anwendung ausgewählt und gegen die vorhandenen Workshop-Profile geprüft werden. Ein reales Update von einer älteren öffentlichen Version auf eine neuere kann erst mit einem folgenden Release vollständig erprobt werden. Echte Steam-Uploads und Spielabnahme wurden nicht durchgeführt. Die [weiteren bekannten Grenzen](KNOWN_LIMITATIONS.md) gelten fort.
