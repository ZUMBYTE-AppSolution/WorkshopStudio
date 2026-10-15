# Workshop Studio 1.1.0 · Lieferbericht · 15.10.2026

## Codebasierte Bestandsaufnahme

Der Ausgangsstand war eine vorhandene .NET-10-WPF-Solution mit anthrazit-orangefarbener Shell, kategorisierter Seitenleiste, Dashboard, eigenem Icon, Mod-Editor, Paketvorschau, SteamCMD-Publisher, WebView2-Browser, Workspace-Speicher und Selbsttests. Er konnte einzelne bekannte Workshop-IDs lesen, aber keinen vollständigen eigenen Workshop-Bestand ermitteln. Der alte SteamCMD-Login konnte unter dem hier verwendeten Windows-Prozesskontext an ConPTY hängen. Für vorhandene Mods gab es weder einen Import ihrer Metadaten noch einen echten Config-Textviewer.

## Umsetzung

- Eigene Dateien und Collections aus der angemeldeten Steam-Profilsitzung über sämtliche Seiten lesen; AppID, Typ, Titel und öffentliche Steam-Detaildaten samt Besitzer, Bild, Beschreibung, Tags, Zeiten und Statistiken abgleichen.
- SteamCMD-`+info` ermittelt die Upload-SteamID64. Web- und Upload-Identität müssen vor einer Veröffentlichung übereinstimmen. Windows-ConPTY-Start und UTF-8-Ausgabe wurden korrigiert.
- 28 Einträge des tatsächlich angemeldeten Kontos wurden in die vorhandene lokale Bibliothek übernommen; 27 öffentlich bestätigte Einträge ergaben 19 Mod- und 8 Collection-Profile. Der eine nicht bestätigte Eintrag bleibt nur lesbar. Bestehende Workspace-Einstellungen und Quellpfade blieben erhalten; vor Migration und Import wurden exakte Sicherungen geschrieben.
- Bestehende Workshop-IDs werden nicht neu angelegt. Vor Updates werden Besitzer, AppID und Remote-Stand geprüft; der Nutzer sieht Quelle, Dateianzahl, Größe und einzeln auswählbare Metadaten. Ohne Auswahl gehen nur die gewählten Inhaltsdateien in das Update. Nach einem Upload erfolgt eine erneute Detailabfrage.
- Lokales Schema liest ausdrücklich ausgewählte `mod.info`, `workshop.txt`, Versionsdateien, Bild und Changelog. Steam-Änderungsnotizen werden bei Bedarf von der Workshop-Seite gelesen. Versionshinweise aus Beschreibungen bleiben als solche gekennzeichnet.
- Im Update-Dialog stehen der lokale Versionswert, der lesbare Steam-Versionshinweis und die tatsächlich aus `{version}` erzeugte Änderungsnotiz. Steam besitzt kein separates Versionsfeld; eine Versionszeile in der Beschreibung kann nur mit bewusst gewählter Beschreibungsaktualisierung geändert werden.
- Das Config-Fenster zeigt Originaldateien als frei markierbaren Text, erkannte statische Optionen und separate Serverzeilen. Kopier- und Exportaktionen verwenden den tatsächlichen Text.
- Schema-1-Arbeitsbereiche und alte Exporte werden migriert; die Originaldatei wird vor dem ersten Speichern gesichert. Die Paketierung bleibt auf ausgewählte Mods begrenzt; Spiel- und Steam-Installationen sowie nicht ausgewählte Nachbarordner sind gesperrt.
- Iconify-`mdi`-Symbole sind als lokale WPF-Vektoren eingebettet: alle 118 beschrifteten Aktionsknöpfe, die Sidebar, das Dashboard und Dialogsymbole sind ausgestattet. Die Fenstertasten verwenden Minimieren-, Maximieren-/Wiederherstellen- und rotes Schließen-Symbol. Das App-Icon und die Anthrazit-Orange-Palette bleiben bestehen.

## Ausgeführte Prüfungen

| Prüfung | Ergebnis |
|---|---|
| `dotnet restore`, Release-Build, self-contained `win-x64`-Publish | erfolgreich; Build ohne Warnungen/Fehler |
| C#-Selbsttests | 114/114 bestanden |
| Theme-, Paketierungs-, Login-Quellprüfungen | 15/15, 20/20, 22/22 bestanden |
| WPF-Laufzeit mit isoliertem Arbeitsbereich | Dashboard 1280×800, Bibliothek 1920×1080, Config 1040×760 gerendert; Iconify-Fenstertasten für Minimieren, Maximieren und Wiederherstellen betätigt, rotes Schließen geprüft |
| Config-Originaltext markieren/kopieren | über den Fensterknopf und Windows-Zwischenablage bestätigt |
| Echte vorhandene Steam-Web- und SteamCMD-Sitzung | beide Identitäten bestätigt und identisch; 28 eigene Inhalte auf zwei Seiten gefunden |
| Bestehender Arbeitsbereich | 19 Mods, 8 Collections und 28 Bibliothekseinträge importiert; Auto-Publish für alle importierten Mods aus |

## Offene Abnahme

Ein echter privater Erst- und Bestandsupload sowie die Ladefähigkeit im Zielspiel wurden ohne gezielte Veröffentlichungsfreigabe nicht durchgeführt. Ein privater/nicht öffentlich abfragbarer Eintrag kann noch nicht schreibbar importiert werden. 125/150-%-DPI, Hellmodus, frische Steam-Guard-Anmeldung, Bildaktualisierung und spielabhängige Sonderfelder sind nicht durchgehend geprüft. Config-Dateien von remote Einträgen erfordern einen bewusst zugeordneten lokalen Quellordner.

Das Quell-ZIP enthält nur Projektdateien, keine `bin`, `obj`, Pakete oder Nutzer-Sitzungsdaten. Das getrennte Windows-ZIP enthält die selbständige `win-x64`-Anwendung und benötigt für eingebettete Steam-Seiten die WebView2 Runtime.
