Vorabstand der Windows-App zum Ausprobieren, Stand `{{SHA}}`. Keine Signatur, Kassa nur mit Test-Signatur: nicht für den Alltag eines Spas verwenden.

**Empfohlen ist das Setup mit Updates** im Release [„Windows-Setup“](https://github.com/vompa/aispa-studio/releases/tag/windows-setup) (rund 200 MB). Diese ZIP-Datei ist der zweite Weg, ohne Installation und ohne Updates.

## Voraussetzungen

- **Jeder PC:** Windows 10 22H2 oder Windows 11, 64 Bit, mindestens 4 GB Arbeitsspeicher und rund 1 GB freier Platz. Die .NET-Laufzeit ist in der ZIP-Datei, installiert werden muss nichts. Die WebView2-Runtime braucht nur die Hilfe; Windows 11 hat sie, unter Windows 10 kommt sie mit Microsoft Edge.
- **Als Einzelplatz:** nichts weiter. Datenbank und Server sind in der ZIP-Datei, Docker ist nicht nötig. Für Datenbank und Server kommen [rund 1 GB Arbeitsspeicher und 0,6 GB Platz plus Daten](https://github.com/vompa/aispa-studio/blob/main/docs/installation/README.md#zus%C3%A4tzlich-f%C3%BCr-einzelplatz-und-haupt-pc) dazu; empfohlen sind 8 GB Arbeitsspeicher gesamt.
- **Mit Server:** ein laufender Server von AISPA Studio. Zum Ausprobieren auf demselben PC: [Docker Desktop](https://www.docker.com/products/docker-desktop/) installieren und den Server mit `docker compose up -d` starten ([Anleitung](https://github.com/vompa/aispa-studio/blob/main/docs/installation/server.md)). Ein weiterer PC im Spa braucht nur den Haupt-PC im selben Netz.

## 1. Herunterladen und entpacken

1. Unten unter „Assets“ die Datei `AispaStudio-Windows.zip` laden.
2. Rechtsklick auf die ZIP-Datei › „Alle extrahieren …“ und einen eigenen Ordner wählen, z. B. `%LOCALAPPDATA%\Programs\AispaStudio`.
3. Im Zielordner liegt jetzt der Ordner `AispaStudio-Windows`. Darin stehen `AispaStudio.Desktop.exe` und die Unterordner `api`, `web` und `postgres`. Nichts davon verschieben, und die App nicht direkt aus der ZIP-Datei heraus starten.

## 2. Starten

**Als Einzelplatz (ohne Server, zum Ausprobieren am einfachsten):** Die App startet Datenbank und Server selbst auf diesem PC.

1. Im Ordner `AispaStudio-Windows` Rechtsklick auf `AispaStudio.Desktop.exe` › „Verknüpfung erstellen“.
2. Rechtsklick auf die Verknüpfung › „Eigenschaften“ › im Feld „Ziel“ hinter den Pfad ` --standalone` anhängen (mit Leerzeichen davor) › „OK“.
3. Über die Verknüpfung starten. Beim ersten Start legt die App die Datenbank an und zeigt „Spa einrichten“: Name des Spas, Land, erster Standort und das Konto der Inhaberin bzw. des Inhabers.

Die App merkt sich die Betriebsart. Danach genügt ein Doppelklick auf `AispaStudio.Desktop.exe`.

**Beispiel-Spa zum Ausprobieren:** wie beim Einzelplatz eine Verknüpfung anlegen, aber ` --demo` anhängen. Die App legt beim ersten Start in einer eigenen Datenbank ein Beispiel-Spa mit Testdaten an (rund eine halbe Minute); die echten Daten bleiben unberührt. Anmelden z. B. mit `inhaberin@demo.aispa.test` und dem Passwort `demo`. Die Leiste „Sie sehen Beispieldaten.“ bietet „Beispieldaten löschen …“. Die App merkt sich `--demo` nicht: ohne den Schalter startet sie wieder mit den echten Daten.

**Mit Server oder Haupt-PC:** `AispaStudio.Desktop.exe` per Doppelklick starten, ohne Schalter. Die App verbindet sich mit dem Server unter `http://localhost:5086`. Für einen Haupt-PC im Spa auf dem Startbildschirm in der Karte „Mit Haupt-PC im Netz verbinden“ Adresse und Verbindungscode eintragen; beides zeigt der Haupt-PC unter „Im Netz“.

> [!NOTE]
> Die App ist nicht signiert. Warnt Windows beim ersten Start („Der Computer wurde durch Windows geschützt“), über „Weitere Informationen“ › „Trotzdem ausführen“ starten.

## Neuer Stand

App schließen, die neue ZIP-Datei in denselben Zielordner entpacken und die alten Dateien ersetzen. Die Daten liegen getrennt unter `%LOCALAPPDATA%\AispaStudio\daten` und bleiben erhalten.

Ausführlich: [Einzelplatz und mehrere PCs im Spa](https://github.com/vompa/aispa-studio/blob/main/docs/installation/standalone.md) · [Eigener Server](https://github.com/vompa/aispa-studio/blob/main/docs/installation/server.md)
