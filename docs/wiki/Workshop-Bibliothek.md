# ☁️ Meine Workshop-Inhalte

**Workshop-Inhalte ermitteln** liest die eigenen Veröffentlichungen aus der angemeldeten Steam-Websitzung. Der Scan folgt den erreichbaren Profilseiten für Dateien und Collections und ist nicht auf die bereits lokal angelegten Spieleprofile begrenzt.

Der Scanbericht nennt Seiten, Abdeckung, Zeitpunkt und Fehler. Ein Teilausfall löscht bereits ermittelte Inhalte nicht. „Keine Ergebnisse“ und ein fehlgeschlagener Scan sind unterschiedliche Zustände.

## Welche Daten gelesen werden

Studio erfasst unter anderem Workshop-ID, Besitzer, AppID, Inhaltstyp, Titel, Beschreibung, Sichtbarkeit, Tags, Vorschaubild, Zeitpunkte und verfügbare Kennzahlen. Die öffentliche Steam-Detailabfrage bestätigt Besitzer und Spiel. Feldstatus und Herkunft bleiben erhalten. Nicht gelieferte Angaben werden nicht als leere Löschwerte interpretiert.

Die Autoren-Version ist kein universelles Steam-Feld. Eine Versionszeile in einer Beschreibung wird als Hinweis gekennzeichnet. Verfügbare Steam-Änderungsnotizen lassen sich in den Details lesen; aktuell wird die erste öffentlich erreichbare Changelog-Seite übernommen. Der vollständige Steam-Verlauf bleibt zusätzlich verlinkt.

## Importieren

Einen Eintrag auswählen und **Auswahl importieren** verwenden oder alle bestätigten Einträge übernehmen. Für bisher unbekannte AppIDs werden lokale Spieleprofile angelegt. Mods erhalten ihre bestehende Workshop-ID; Collections werden als lokale Verknüpfung übernommen. Ein erneuter Import erzeugt keine zweite Mod mit derselben ID.

Importierte Mods starten ohne lokale Quelle und ohne Auto-Publish. Der Import von Titel, Bild und Beschreibung lädt keine Mod-Dateien als Arbeitskopie herunter. Die lokale Quelle wird anschließend unter [Lokale Mods](Lokale-Mods) zugeordnet.

Ein bestätigter erneuter Import übernimmt den zuletzt gelesenen Remote-Stand als Baseline. Lokale Editoränderungen bleiben erhalten. Vor einem Upload wird Steam erneut gelesen. Ändert sich der entfernte Stand zwischenzeitlich, stoppt Studio den Vorgang, bis der neue Stand geprüft und bewusst übernommen wurde.

## Nur remote oder nicht aktualisierbar

Fehlt eine lokale Quelle, kann Studio weder ihren Inhalt paketieren noch eine Config daraus lesen. Ein nicht öffentlich bestätigbarer Besitzer, eine fremde AppID oder ein nicht unterstützter Typ sperrt den Upload. Solche Einträge bleiben zur Übersicht sichtbar. Abonnements und Favoriten werden nicht als eigene Veröffentlichungen behandelt.

Die Suche berücksichtigt Titel, ID und AppID. Die Detailansicht zeigt den aktuellen ausgewählten Eintrag; Bilddaten werden separat lokal gecacht.
