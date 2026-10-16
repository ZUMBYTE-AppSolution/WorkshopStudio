# ⬆️ Neue Mods und Bestandsupdates veröffentlichen

## Test und Live

Jedes Mod-Profil besitzt getrennte Workshop-IDs für **Test** und **Live**. Test verwendet einen privaten Eintrag und den eingestellten Titelzusatz. Live verwendet die Live-ID und die gewünschte Sichtbarkeit. Eine vorhandene Live-Mod wird nicht automatisch zum Testeintrag umgewidmet.

Bei einer neuen Veröffentlichung bleibt die Ziel-ID zunächst leer. Steam erzeugt beim erfolgreichen Erstupload eine ID, die Studio direkt im Profil speichert. Eine bereits existierende ID wird bei einem Bestandsupdate immer beibehalten.

## Ein normaler manueller Ablauf

1. Mod-Quelle, Spielprofil und Metadaten kontrollieren.
2. **Upload-Inhalt prüfen** öffnen. Die Vorschau erzeugt ein lokales Paket und zeigt vollständige Datei- und Pfadzuordnungen; sie startet keinen Upload.
3. Gewünschte Mods und Zielkanal wählen, in die Warteschlange einreihen und diese starten.
4. Studio bestätigt SteamCMD-Identität und Übereinstimmung mit dem Webkonto.
5. Bei einem bestehenden Eintrag werden Besitzer, AppID und Remote-Zeitstand erneut gelesen. Der Update-Plan zeigt Quellen, Ziel-ID, Größe und die [automatisch erkannten Feldänderungen](Metadaten-und-Changelog). Die Auswahl kann vor der Freigabe geändert werden.
6. Studio lädt den bisherigen Inhalt von Steam, erstellt und prüft das [Backup](Backups-und-Wiederherstellung) und kontrolliert den Remote-Stand erneut.
7. Erst danach startet der Upload. Das Ergebnis wird mit ID und Protokoll gespeichert. Eine öffentliche Detailabfrage prüft anschließend Besitzer, AppID und übertragene Textfelder.

Wenn **Unveränderte Pakete überspringen** aktiv ist, werden bereits bekannte Inhalte ohne neue Metadatenänderung ausgelassen. Eine neue Beschreibung oder Änderungsnotiz kann deshalb auch dann einen Updatevorgang auslösen, wenn die Mod-Dateien unverändert sind. Der aktuelle SteamCMD-Weg kann dabei den Inhalt zusammen mit den Metadaten erneut übertragen.

## Ergebnisse richtig lesen

| Zustand | Bedeutung |
|---|---|
| Wartend | Noch nicht übertragen. Ein Auftrag kann einzeln entfernt werden. |
| Paketierung | Quellen und Metadaten werden geprüft; bei Bestandsupdates gehört die Sicherung zur Vorbereitung. |
| Upload | SteamCMD verarbeitet den freigegebenen Auftrag. |
| Erfolgreich | SteamCMD hat eine veröffentlichte ID bestätigt. Den zusätzlichen Status der Remote-Nachprüfung lesen. |
| Unverändert | Keine neue Übertragung erforderlich. |
| Fehler | Protokoll und Meldung prüfen. Ohne Backup oder bei Konto-/Zielkonflikt startet kein Bestandsupload. |
| Abgebrochen/Unterbrochen | Der lokale Ablauf endete. Ein bereits von Steam verarbeiteter Upload kann trotzdem wirksam sein. |

## Unklare Erstuploads

Vor einem Erstupload setzt Studio eine Abgleichsperre. Fehlt danach eine eindeutige Erfolgsbestätigung, wird nicht blind eine neue Veröffentlichung erzeugt. Zuerst die Steam-Seite prüfen, eine dort vorhandene ID im richtigen Kanal eintragen und die Zuordnung abgleichen. Erst nach dieser Prüfung erneut einreihen.

**Abbrechen** stoppt den lokalen Vorgang, rollt aber keine bereits serverseitig erfolgte Änderung zurück. Mehrere Mods in einer Warteschlange sind keine gemeinsame atomare Transaktion. Rücksetzen erfolgt bewusst mit einem früheren Backup unter derselben ID.
