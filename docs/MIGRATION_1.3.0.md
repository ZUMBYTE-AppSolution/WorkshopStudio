# Datenkompatibilität 1.3.0

Die Workspace-Datei behält Schema 2. Es kommen optionale Eigenschaften hinzu: Metadaten-Baselines und Notiz-/Bildprüfsummen pro Kanal, das separate Opt-in für Live-Sichtbarkeit, die aktivierte Metadatenerkennung sowie Backup-Pfad und Metadatenentscheidung je Uploadjob. Alte Dateien ohne diese Eigenschaften bleiben lesbar.

Beim Laden werden fehlende Metadaten-Baselines ausschließlich aus bereits bestätigten vorhandenen Bibliothekseinträgen ergänzt. Das löst keine Netzwerkveröffentlichung aus. Beim Speichern bleibt die bisherige `workspace.json` als `workspace.json.bak` erhalten. Die bestehende exakte Schema-1-Sicherung und Migration gelten weiter.

Workshop-IDs, lokale Quellen, Kontozuordnung, Automationsfreigaben und Theme-Einstellungen werden nicht zurückgesetzt. Neue Live-Sichtbarkeitsautomatik ist aus. Die neue Metadatenerkennung ist an, aktiviert aber nicht selbst Auto-Publish oder globale Automation. Manuelle Uploads zeigen weiterhin einen konkreten Update-Plan.

Vor einer älteren Programmversion, die die neuen optionalen Felder noch nicht kennt, den aktuellen Workspace zusätzlich exportieren. Ein Speichern mit alter Software kann unbekannte Felder verlieren.
