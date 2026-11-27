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
3. Im Zielordner liegt dann der Ordner `AispaStudio-Windows`. Darin stehen `AispaStudio.Desktop.exe` und die
   Unterordner `api`, `web` und `postgres`. Nichts davon verschieben; die App findet Datenbank und Server neben sich.

> [!NOTE]
> Die App ist nicht signiert. Beim ersten Start kann Windows SmartScreen warnen („Der Computer wurde durch Windows
> geschützt“). Über „Weitere Informationen“ und „Trotzdem ausführen“ startet sie. Auf einem frischen PC mit
> Virenscanner ist das noch nicht ausprobiert.

## 2. Als Einzelplatz starten

Auf einem PC ohne Einrichtung zeigt der erste Start die Startauswahl: **Neues Spa auf diesem PC einrichten** schaltet den
Einzelplatz ein und startet die App neu. Darunter stehen **Beispiel-Spa ansehen**, **Aus einer Sicherung wiederherstellen**
und **Mit einem vorhandenen Spa verbinden**. Die Startauswahl erscheint nur, wenn kein Schalter gesetzt ist, keine Adresse
über `AISPA_API_URL` vorgegeben ist, noch keine Betriebsart gewählt wurde und auf diesem PC kein Server antwortet.

Für Skripte und Verknüpfungen gibt es den Schalter `--standalone`; er schaltet den Einzelplatz ohne Startauswahl ein:

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

Wiederherstellen auf einem neuen PC: in der Startauswahl **Aus einer Sicherung wiederherstellen** wählen (oder die App mit `--standalone` starten) und vor „Spa einrichten“ „Aus einer Sicherung
wiederherstellen …“ wählen. Ausführlich: Hilfethema [Sicherung](../../web/src/help/content/sicherung.md).

## Beispiel-Spa zum Ausprobieren

Wer AISPA Studio erst ansehen will, startet die App mit dem Schalter `--demo`, am einfachsten über eine zweite
Verknüpfung (Ziel: Pfad plus ` --demo`):

- Die App legt dafür eine eigene Datenbank `aispademo` an und füllt sie mit einem Beispiel-Spa. Der erste Start dauert
  rund eine halbe Minute, die Anmeldung zeigt den Stand. Die echte Datenbank `aispadb` bleibt unberührt.
- Der Schalter gilt nur für diesen Start und schaltet immer den Einzelplatz ein. Die App merkt ihn sich nicht;
  ohne `--demo` startet sie wieder das echte Spa bzw. **„Spa einrichten“**.
- Alle Beispielkonten haben das Passwort `demo`, zum Beispiel `inhaberin@demo.aispa.test`. Die Anmeldung nennt das.
  Weitere Konten: [Testdaten](../testdaten.md).
- Über allen Seiten steht die Leiste **„Sie sehen Beispieldaten.“**. Wer den Standort verwalten darf, löscht das
  Beispiel-Spa dort mit **„Beispieldaten löschen …“**; nach der Rückfrage schließt sich die App.
- Im Beispiel-Spa sichert die App nicht und gleicht nicht mit einer zentralen Datenbank ab.

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
| Daten, Datenbank, Protokolle | `%LOCALAPPDATA%\AispaStudio\daten` (Datenbank in `pg`, Protokolle in `protokolle`); das echte Spa liegt in der Datenbank `aispadb`, das Beispiel-Spa (`--demo`) in `aispademo` im selben Ordner |
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
| „AISPA Studio kann auf diesem PC nicht starten“ | Die Startprüfung hat einen Fund, der den Start verhindert: Windows älter als 10 22H2, kein 64-Bit-Windows oder weniger als 1 GB frei auf dem Laufwerk der Daten. Die Karte nennt den Grund; Windows aktualisieren bzw. Platz schaffen und die App neu starten. |

Technische Einzelheiten zu allen Schaltern und Dateien: [Betriebsarten und Module: Referenz](operating-modes.md).
