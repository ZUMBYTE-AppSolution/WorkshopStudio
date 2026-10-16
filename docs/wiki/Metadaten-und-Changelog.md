# 📝 Metadaten, Version und Changelog

Unter **Einstellungen → Publishing** steuert **Geänderte lokale Metadaten automatisch erkennen und mit aktualisieren** den automatischen Abgleich. Die Option ist ab 1.3.0 standardmäßig aktiv. Sie schaltet selbst keine Upload-Automation ein.

Studio liest lokale Angaben, vergleicht sie mit dem zuletzt akzeptierten und dem frisch abgefragten Steam-Stand und entscheidet je Feld. Ein manueller Update-Plan zeigt die Entscheidung und wählt erkannte Änderungen vor. Ein bereits freigegebener automatischer Job verwendet dieselben Entscheidungen ohne zusätzlichen Felddialog.

## 👀 Hier siehst du die Entscheidung

![Automatischer Metadatenvergleich mit Begründung je Feld](https://raw.githubusercontent.com/ZUMBYTE-AppSolution/WorkshopStudio/main/docs/images/wiki/metadata-decisions.png)

Im Beispiel unterscheiden sich Beschreibung und Änderungsnotiz. Titel und Bild bleiben erhalten. Die Live-Sichtbarkeit besitzt keine automatische Freigabe. Direkt darunter steht, dass vor dem Upload das Backup entstehen muss.

![Automatisch ausgewählte Beschreibung und neue Änderungsnotiz](https://raw.githubusercontent.com/ZUMBYTE-AppSolution/WorkshopStudio/main/docs/images/wiki/metadata-selection.png)

**Orange Häkchen = dieses Feld geht mit.** Bei einem manuellen Job kannst du die Auswahl ändern. Bei einem bereits erlaubten automatischen Job wird dieselbe ermittelte Auswahl ohne Felddialog angewendet. [Kopierbare Beispiele und erwartetes Ergebnis](Beispiele)

## Lokale Quellen und Vorrang

| Angabe | Quelle und Verhalten |
|---|---|
| Titel | Mod-Editor; bei unverändert importiertem Titel kann ein lokaler `title=`-Wert aus `workshop.txt` übernommen werden. |
| Beschreibung | Bewusst geänderter Editorwert hat Vorrang. Sonst wird eine nicht leere lokale `description=`-Angabe gelesen. Mehrere `description=`-Zeilen werden als mehrzeiliger Text verbunden. |
| Version | `version.txt`, `VERSION`/`VERSION.txt` oder ein ausdrücklicher `version=`-Wert in den ausgewählten `mod.info`-Dateien. |
| Änderungsnotiz | Eigene Notiz im Editor; bei leerer Notiz oder der Standardvorlage `Update {version}` wird ein lokaler `CHANGELOG.md`/`.txt` verwendet. |
| Vorschaubild | Das im Mod-Profil ausdrücklich ausgewählte Workshop-Bild. Eine Vorschau wird anhand ihrer Dateidaten verglichen. Ein spielinternes Poster ersetzt nicht automatisch das Steam-Bild. |
| Sichtbarkeit | Bei bestehenden Live-Einträgen nur automatisch, wenn **Sichtbarkeit bei Bestandsupdates automatisch abgleichen** im Mod-Profil erlaubt ist. Im manuellen Plan ist sie weiterhin gezielt auswählbar. Test verwendet privat. |

Gelesen werden ausschließlich die ausgewählten Mod-Stämme und eindeutig zugehörige unmittelbare Workshop-Metadatenordner. Ein `workshop.txt` neben einem `Contents/`-Ordner kann Metadaten liefern. Das erweitert den Paketinhalt nicht um andere Mods oder Nachbarordner.

## Was automatisch mitgeht

Ein nicht leerer Titel oder Beschreibungstext wird geschrieben, wenn er vom bestätigten Steam-Wert abweicht. Unterschiedliche Windows-/Unix-Zeilenenden allein gelten nicht als Änderung. Fehlende lokale Texte und ein nicht ausgewähltes Bild bedeuten **beibehalten**. Sie werden nicht als Löschauftrag versendet.

Studio merkt sich akzeptierte Remote-Werte, um unveränderte importierte Angaben von eigenen Editoränderungen zu unterscheiden. Ein frisch akzeptierter Steam-Text kann einen unveränderten importierten Wert ersetzen. Ein lokaler Editorwert, der seit der Baseline geändert wurde, bleibt erhalten.

Wenn ein anderer Rechner den Workshop-Eintrag seit dem letzten akzeptierten Scan geändert hat, stoppt der Upload. Den neuen Stand in der Bibliothek prüfen und bewusst erneut importieren, bevor du die gewünschte lokale Fassung veröffentlichst. Ein automatischer Lauf entscheidet diesen konkurrierenden Inhaltskonflikt nicht stillschweigend.

## Changelog ohne doppelte Notizen

Aus einer Markdown-Changelog-Datei verwendet Studio einen gefüllten **Unreleased**-/**Unveröffentlicht**-Abschnitt oder die erste aktuelle Versionssektion. Ältere Versionssektionen werden nicht bei jedem Upload erneut angehängt. Ohne erkennbare Versionsüberschriften gilt der vorhandene Text als Notiz. Die aktuelle Notiz darf höchstens 8000 UTF-8-Bytes enthalten; zu lange Texte werden vor dem Upload abgewiesen.

Vorlagen unterstützen `{version}` und `{date}`. Die gerenderte Notiz wird je Test-/Live-Kanal anhand einer Prüfsumme wiedererkannt. Steht dieselbe Notiz bereits im lesbaren Steam-Verlauf, wird sie ebenfalls ausgelassen. Ist der erste Vergleich mit Steam nicht möglich und existiert noch keine bekannte Notiz-Prüfsumme, wird diese erste Notiz zurückgestellt, damit kein alter Verlauf doppelt erscheint. Inhalt und andere sichere Felder können weiterhin aktualisiert werden.

Eine neue Notiz wird in Steam angehängt. Alte Steam-Notizen werden nicht überschrieben oder gelöscht. Der technische Uploadlog ist kein Changelog.

## Versionen auf Steam

Der verwendete generische SteamCMD-Weg hat kein eigenständiges universelles Autoren-Versionsfeld. Findet Studio in der Beschreibung bereits eine einfache Zeile wie `Version: 1.2`, kann eine ausdrücklich gelesene lokale Version diese Zeile aktualisieren. Ohne vorhandene Zeile wird keine Versionszeile erfunden. Eine Version kann außerdem in der neuen Änderungsnotiz stehen.

Tags, Abhängigkeiten und weitere Vorschauobjekte werden im aktuellen Steam-Webeditor gepflegt. Sie werden vom VDF-Upload nicht durch erfundene Felder ersetzt.
