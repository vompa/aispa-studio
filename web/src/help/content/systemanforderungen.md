---
art: einstellung
bereich: Betrieb
rollen: inhaber, manager
stichwoerter: Mindestanforderungen, Hardware, PC, Computer, Arbeitsspeicher, RAM, Windows 10, Windows 11, Festplatte, WebView2, Docker, requirements
---
# Systemanforderungen

Was ein Windows-PC für AISPA Studio braucht. Die schlanke Ausstattung reicht für die Windows-App am Empfang, die mit einem Server arbeitet. Für alles, was auf dem PC selbst läuft (Einzelplatz, Haupt-PC für weitere PCs, lokale KI, Docker), kommen die Zusatzpakete dazu; ihre Werte werden zur schlanken Ausstattung addiert.

Die Zahlen sind am Stand vom 19.10.2026 gemessen, wo „gemessen“ steht, mit dem Testdatensatz (drei Standorte, 260 Kunden, rund 2.300 Termine und 2.200 Belege über drei Monate). Was nicht gemessen werden konnte, ist als Schätzung gekennzeichnet.

## Schlank: Windows-App am Empfang {#schlank}

Die App verbindet sich mit dem Server des Spas (in der Cloud oder mit dem Haupt-PC im eigenen Netz). Datenbank und Server laufen dort, nicht auf diesem PC.

| | Mindestens | Empfohlen |
| --- | --- | --- |
| Windows | Windows 10 22H2, 64 Bit | Windows 11, 64 Bit |
| Prozessor | 2 Kerne, x64 (Intel oder AMD, ab etwa 2017) | 4 Kerne |
| Arbeitsspeicher | 4 GB | 8 GB |
| Speicherplatz | 1 GB frei | 2 GB frei |
| Bildschirm | 1280 × 720 | 1920 × 1080 |
| Netzwerk | dauerhaft zum Server, LAN oder WLAN | LAN-Kabel am Empfang |

- **Download:** 56 MB, entpackt 122 MB (gemessen). Den übrigen freien Platz braucht die App für den Zwischenspeicher, mit dem sie bei Netzausfall weiter Termine zeigt und Buchungen vormerkt.
- **.NET:** muss nicht installiert werden, die App bringt ihre Laufzeit mit.
- **WebView2:** nötig nur für die Hilfe in der App. Auf Windows 11 ist sie dabei, auf Windows 10 kommt sie mit Microsoft Edge. Fehlt sie, bleibt die Hilfe leer; dann die „WebView2 Runtime“ (Evergreen) kostenlos bei Microsoft laden.
- **Windows 10:** läuft, Microsoft liefert seit Oktober 2025 aber nur noch mit erweitertem Support (ESU) Sicherheitsupdates. Für einen PC mit Kassa ist Windows 11 die bessere Wahl.
- **Arbeitsspeicher der App:** geschätzt 200 bis 300 MB, mit offener Hilfe etwa 200 MB mehr. Unter Windows nicht gemessen.
- **Prozessoren mit ARM** (z. B. Snapdragon): nicht geprüft. Windows 11 führt die x64-App dort in Emulation aus.
- **Netzwerk:** Die App braucht pro Kalendertag rund 15 bis 20 kB (gemessen). Eine normale Büroleitung reicht; wichtiger als Bandbreite ist eine stabile Verbindung.

## Zusatzpaket: Einzelplatz und Haupt-PC {#einzelplatz}

Im Einzelplatz-Betrieb (siehe „Einzelplatz“) laufen auf dem PC zusätzlich die Datenbank (PostgreSQL) und der Server von AISPA Studio. Als Haupt-PC (siehe „Im Netz“) bedient er auch die anderen PCs im Spa.

| | Zusätzlich |
| --- | --- |
| Prozessor | 4 Kerne empfohlen |
| Arbeitsspeicher | 1 GB im Betrieb, 8 GB gesamt empfohlen |
| Speicherplatz | 1 GB für Programm, dazu Daten und Sicherungen |

- **Download:** das ganze Windows-Paket mit Datenbank rund 185 MB (aus gemessenen Teilen). Entpackt rund 0,6 GB (Server 174 MB gemessen, Datenbank geschätzt).
- **Arbeitsspeicher:** Server im Betrieb 300 MB, Datenbank 100 MB (gemessen). Beim ersten Laden großer Datenmengen, etwa einer Übernahme aus einem anderen Programm, kurzzeitig bis 1,3 GB (gemessen beim Anlegen des Testdatensatzes).
- **Start:** Server bereit nach rund 8 Sekunden (gemessen, auf einem Linux-Rechner mit 4 Kernen; unter Windows ähnlich).
- **Antwortzeiten:** ein Kalendertag in 30 bis 60 Millisekunden, der erste nach dem Start in 0,4 Sekunden (gemessen).
- **Daten:** der Testdatensatz belegt 48 MB (gemessen). Geschätzt 100 bis 200 MB je Standort und Jahr. Die tägliche Sicherung (siehe „Sicherung“) braucht zusätzlich Platz, am besten auf einem zweiten Laufwerk oder USB-Stick.
- **Betrieb:** Der Haupt-PC muss laufen, solange im Spa gearbeitet wird, und braucht im Router eine feste IP-Adresse. Windows fragt beim Freigeben nach der Firewall; für private Netzwerke erlauben.
- **Energie:** Energiesparmodus und Ruhezustand während der Öffnungszeiten ausschalten. Eine kleine USV schützt die Datenbank bei Stromausfall.

## Zusatzpaket: Lokale KI {#lokale-ki}

Das lokale KI-Modell (siehe „KI“) läuft ohne Internet. Es braucht den Dienst Ollama, entweder als Windows-Programm oder als Docker-Container.

| | Mindestens | Empfohlen |
| --- | --- | --- |
| Prozessor | 4 Kerne mit AVX2 (Intel ab 2013, AMD ab 2015) | 8 Kerne |
| Arbeitsspeicher | 8 GB gesamt | 16 GB gesamt |
| Speicherplatz | 6 GB frei | 15 GB frei (mit Docker) |
| Grafikkarte | nicht nötig | nicht nötig |

- **Modell:** Gemma 3 mit 4 Mrd. Parametern, 3,3 GB Download (Angabe von Ollama), danach dauerhaft auf dem PC.
- **Arbeitsspeicher:** solange das Modell geladen ist, etwa 4 bis 5 GB (geschätzt aus Modellgröße und Textlänge, nicht gemessen). Nach zehn Minuten ohne Anfrage gibt es ihn wieder frei. Der Dienst selbst braucht ohne Modell 76 MB (gemessen).
- **Antwortzeit:** auf einem Prozessor ohne Grafikkarte geschätzt 10 bis 60 Sekunden je Mail. Das stört nicht, weil Mails im Hintergrund gelesen werden.
- **Als Docker-Container:** Image 3,8 GB Download, 9,4 GB auf der Festplatte (gemessen, Ollama 0.40.6; enthält Teile für Grafikkarten, die ohne Grafikkarte ungenutzt bleiben). Dazu das Zusatzpaket „Docker“.
- **Als Windows-Programm:** kleiner, ohne Docker. Für den Einzelplatz der einfachere Weg.

## Zusatzpaket: Docker {#docker}

Für den Betrieb mit Docker auf einem Windows-PC, etwa als eigener Server im Spa oder zum Ausprobieren (`docker compose up -d`).

| | Zusätzlich |
| --- | --- |
| Windows | Windows 11 oder Windows 10 22H2, 64 Bit, mit WSL 2 |
| Prozessor | Virtualisierung im BIOS eingeschaltet |
| Arbeitsspeicher | 4 GB für Docker Desktop, 8 GB gesamt empfohlen |
| Speicherplatz | 3 GB für Images ohne lokale KI |

- **Images:** Datenbank 424 MB (gemessen, `postgres:17-alpine`), dazu Server, Web-Oberfläche und Test-Postfach.
- Docker Desktop ist für Firmen ab 250 Mitarbeitenden oder 10 Mio. USD Umsatz kostenpflichtig; Day-Spas bis 50 Mitarbeitende fallen nicht darunter.

## Zusatzpaket: Kassa-Geräte {#kassa-geraete}

- **Bondrucker:** Thermodrucker mit ESC/POS über das Netzwerk (LAN), Papier 80 mm (42 Zeichen) oder 58 mm (32 Zeichen). Eingerichtet unter „Geräte“.
- **Kartenterminal:** Terminal mit ZVT oder OPI über das Netzwerk, vom Zahlungsanbieter des Spas. Eingerichtet unter „Geräte“.
- **Signatur (Österreich, RKSV) und TSE (Deutschland):** Im Prototyp läuft eine Test-Signatur. Für den Echtbetrieb kommt eine Signaturkarte mit Lesegerät oder ein Cloud-Dienst dazu; die Auswahl ist noch offen.
