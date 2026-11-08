# Einzelplatz und mehrere PCs im Spa

Im Einzelplatz läuft AISPA Studio ganz auf einem Windows-PC, ohne Server und ohne Internet. Die Windows-App startet
dafür selbst eine eigene Datenbank (PostgreSQL) und den Server von AISPA Studio. Weitere PCs im selben Netz können
sich mit diesem PC als Haupt-PC verbinden.

Voraussetzungen: [Windows-PC am Empfang und Zusatz für Einzelplatz](README.md#systemvoraussetzungen).

> [!WARNING]
> Vorabstand zum Ausprobieren: kein Installer, keine Signatur, Kassa nur mit Test-Signatur. Nicht für den Alltag eines
> Spas verwenden.

## 1. Herunterladen und entpacken

1. Im Release [„Windows-App“](https://github.com/vompa/aispa-studio/releases/tag/windows-app) die Datei
   `AispaStudio-Windows.zip` laden (rund 185 MB). Der Release ist ein Vorabstand und wird bei jedem geprüften Stand
   auf `main` ersetzt.
2. Die ZIP-Datei in einen eigenen Ordner entpacken, z. B. `%LOCALAPPDATA%\Programs\AispaStudio`. Ein Ordner unter
   `C:\Program Files` geht auch, braucht aber Administratorrechte zum Entpacken.
3. Im Ordner liegen `AispaStudio.Desktop.exe` und die Unterordner `api`, `web` und `postgres`. Nichts davon
   verschieben; die App findet Datenbank und Server neben sich.

> [!NOTE]
> Die App ist nicht signiert. Beim ersten Start kann Windows SmartScreen warnen („Der Computer wurde durch Windows
> geschützt“). Über „Weitere Informationen“ und „Trotzdem ausführen“ startet sie. Auf einem frischen PC mit
> Virenscanner ist das noch nicht ausprobiert.

## 2. Als Einzelplatz starten

Die App einmal mit dem Schalter `--standalone` starten, am einfachsten über eine Verknüpfung:

1. Rechtsklick auf `AispaStudio.Desktop.exe` › „Verknüpfung erstellen“.
2. Rechtsklick auf die Verknüpfung › „Eigenschaften“ › im Feld „Ziel“ hinter den Pfad ` --standalone` anhängen.
3. Über die Verknüpfung starten.

Die App merkt sich die Betriebsart; jeder weitere Start läuft ohne Schalter als Einzelplatz. `--server` schaltet zurück
auf den Betrieb mit Server.

Beim ersten Start legt die App die Datenbank an und zeigt dann **„Spa einrichten“**: Name des Spas, Land (Österreich,
Deutschland oder Schweiz), erster Standort und das Konto der Inhaberin bzw. des Inhabers mit Benutzername und Passwort.
Das geht nur an diesem PC und nur einmal. Weitere Zugänge legt die Leitung danach unter „Zugänge“ an.

Ausführlich: Hilfethema [Einzelplatz](../../web/src/help/content/einzelplatz.md).

## 3. Tägliche Sicherung einrichten

Im Einzelplatz ist der PC die einzige Kopie aller Daten. Deshalb gleich nach dem Einrichten:

1. In der App „Sicherung“ öffnen und einen Ordner auf einem zweiten Laufwerk, einem USB-Stick oder im Netz wählen.
2. Den **Wiederherstellungsschlüssel** (sechs Vierergruppen) aufschreiben und getrennt vom PC aufbewahren. Die App zeigt
   ihn nur einmal. Ohne ihn lassen sich Gesundheitsdaten aus einer Sicherung nicht zurückholen.

Die App sichert danach einmal am Tag und legt dafür in Windows die Aufgabe „AISPA Studio Sicherung“ an (jeden Abend
um 23 Uhr, wird nachgeholt, wenn der PC aus war). Die letzten 14 Sicherungen bleiben. Ist die letzte Sicherung älter als
sieben Tage, steht ein Hinweis über allen Seiten.

Wiederherstellen auf einem neuen PC: App mit `--standalone` starten und vor „Spa einrichten“ „Aus einer Sicherung
wiederherstellen …“ wählen. Ausführlich: Hilfethema [Sicherung](../../web/src/help/content/sicherung.md).

## Weitere PCs im Spa verbinden

Ein PC im Einzelplatz kann für die anderen PCs im selben Netz der **Haupt-PC** sein. Die Datenbank bleibt dabei auf
dem Haupt-PC; die anderen verbinden sich verschlüsselt (HTTPS mit eigenem Zertifikat).

1. **Am Haupt-PC:** In der App „Im Netz“ öffnen und den Einzelplatz freigeben. Die Seite zeigt die Adressen des PCs
   und einen **Verbindungscode** (20 Zeichen). Windows fragt nach der Firewall: für **private Netzwerke** erlauben.
2. **An jedem weiteren PC:** die App wie in Schritt 1 entpacken und **ohne** `--standalone` starten. Auf dem
   Startbildschirm „Mit Haupt-PC im Netz verbinden“ wählen und Adresse und Verbindungscode eintragen.
3. Angemeldet wird mit denselben Zugängen wie am Haupt-PC.

Der Haupt-PC muss laufen und die App offen haben, solange an den anderen PCs gearbeitet wird; im Router braucht er eine
feste IP-Adresse. Die App öffnet keine Ports am Router. Verwalten von zu Hause geht über ein VPN des Spas oder einen
eigenen [Server](server.md), nicht über eine Portweiterleitung.

Ausführlich: Hilfethemen [Im Netz](../../web/src/help/content/im-netz.md) und
[Verwalten von zu Hause](../../web/src/help/content/verwalten-von-zu-hause.md).

## Wo was liegt

| Was | Wo |
| --- | --- |
| Programm | der Ordner, in den die ZIP-Datei entpackt wurde |
| Daten, Datenbank, Protokolle | `%LOCALAPPDATA%\AispaStudio\daten` (Datenbank in `pg`, Protokolle in `protokolle`) |
| Zugang zur Datenbank und Schlüssel für Gesundheitsdaten | `%LOCALAPPDATA%\AispaStudio\daten\zugang.bin`, mit Windows geschützt; lesbar nur für dasselbe Windows-Konto |
| Zwischenspeicher für Netzausfall | `%LOCALAPPDATA%\AispaStudio\offline.bin` |
| Ports | Datenbank und Server nur an 127.0.0.1 (meist 55432 und 55086), im Netz HTTPS auf 55443 |

Immer mit demselben Windows-Konto arbeiten: Ein anderes Konto kann `zugang.bin` nicht lesen.

## Neuer Stand

Eine Update-Funktion gibt es noch nicht. Ein neuer Stand kommt so auf den PC:

1. Prüfen, dass die letzte Sicherung von heute ist.
2. Die App schließen.
3. Die neue ZIP-Datei in denselben Ordner entpacken und dabei die alten Dateien ersetzen.
4. Die App wie gewohnt starten. Die Daten unter `%LOCALAPPDATA%\AispaStudio` bleiben; die Datenbank wird beim Start auf
   den neuen Stand gebracht.

## Entfernen

1. Die App schließen.
2. In der Windows-Aufgabenplanung die Aufgabe „AISPA Studio Sicherung“ löschen.
3. Den Programmordner löschen.
4. Nur wenn die Daten wirklich weg sollen: `%LOCALAPPDATA%\AispaStudio` löschen. Vorher prüfen, ob eine aktuelle
   Sicherung existiert.

## Wenn etwas nicht geht

| Was passiert | Was tun |
| --- | --- |
| „Einzelplatz startet nicht“ | Die Meldung nennt den Grund und den Pfad zum Protokoll in `protokolle`. Von dort geht auch „Aus einer Sicherung wiederherstellen“. |
| SmartScreen blockiert den Start | „Weitere Informationen“ › „Trotzdem ausführen“ (die App ist nicht signiert). |
| Weiterer PC findet den Haupt-PC nicht | Haupt-PC an und App offen? Firewall für private Netzwerke erlaubt? Adresse und Verbindungscode auf der Seite „Im Netz“ vergleichen. |
| Gesundheitsdaten nicht lesbar | Die App läuft unter einem anderen Windows-Konto als beim Einrichten, oder `zugang.bin` fehlt. Mit dem ursprünglichen Konto starten oder aus der Sicherung wiederherstellen. |
| Hilfe bleibt leer | Die WebView2-Runtime fehlt (nur Windows 10 ohne Edge); bei Microsoft kostenlos laden. |

Technische Einzelheiten zu allen Schaltern und Dateien: [Betriebsarten und Module: Referenz](operating-modes.md).
