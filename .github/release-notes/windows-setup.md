Setup und Updates der Windows-App (Vorabstand). Dieses Release wird nie gelöscht, die App holt hier ihre Updates. Die ZIP-Datei ohne Setup gibt es im Release „Windows-App“. Kein Zertifikat, Kassa nur mit Test-Signatur: nicht für den Alltag eines Spas verwenden.

## Installieren

1. Unter „Assets“ die Datei `AispaStudioApp-win-Setup.exe` laden (rund 200 MB) und starten. Administratorrechte sind nicht nötig.
2. Windows SmartScreen warnt („Der Computer wurde durch Windows geschützt“), weil das Setup nicht signiert ist. Über „Weitere Informationen“ › „Trotzdem ausführen“ geht es weiter.
3. Das Setup installiert das Programm nach `%LOCALAPPDATA%\AispaStudioApp`, legt einen Eintrag im Startmenü an und holt die WebView2-Runtime nach, falls sie fehlt. Datenbank und Server sind im Paket.
4. AISPA Studio über das Startmenü starten.

Die Daten liegen getrennt unter `%LOCALAPPDATA%\AispaStudio`. Eine Deinstallation (Windows-Einstellungen › Apps) löscht nur das Programm, die Daten bleiben.

## Updates

Die App prüft nach dem Start im Hintergrund, ob es einen neuen Stand gibt, und lädt ihn. Eingespielt wird er beim nächsten Start der App. Eine eigene Oberfläche dafür gibt es nicht.

Ausführlich: [Einzelplatz und mehrere PCs im Spa](https://github.com/vompa/aispa-studio/blob/main/docs/installation/standalone.md)
