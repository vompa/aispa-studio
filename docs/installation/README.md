# Installation

AISPA Studio läuft auf einem Windows-PC allein, mit mehreren PCs im eigenen Netz oder mit einem eigenen Server.
Diese Seite hilft bei der Wahl und nennt, was der Rechner dafür braucht.

> [!WARNING]
> AISPA Studio ist ein **Prototyp**. Die Windows-App ist ein Vorabstand ohne Installer und ohne Signatur, die Kassa
> signiert nur mit einer Test-Signatur, und der Server-Betrieb ist noch nicht für den Echtbetrieb freigegeben
> (keine fertige Produktions-Konfiguration, kein HTTPS im Paket). Zum Ausprobieren gedacht, nicht für den Alltag
> eines Spas.

## Welche Betriebsart passt?

| | Einzelplatz | Mehrere PCs im Spa | Eigener Server |
| --- | --- | --- | --- |
| **Für wen** | ein Empfang, ein PC | mehrere PCs im selben Netz | mehrere Standorte, Online-Buchung, Handy-Apps von unterwegs |
| **Wo die Daten liegen** | auf dem PC | auf dem Haupt-PC | auf dem Server (Datenbank PostgreSQL) |
| **Internet nötig** | nein | nein, nur das eigene Netz | ja, für alle Geräte außerhalb des Spas |
| **Online-Buchung, KI, Erinnerungen** | aus (lassen sich einzeln einschalten) | wie Einzelplatz | ja, als zuschaltbare Module |
| **Anleitung** | [Einzelplatz](standalone.md) | [Einzelplatz](standalone.md#weitere-pcs-im-spa-verbinden) | [Server](server.md) |

Eine Browser-Fassung ohne Installation (WebAssembly) ist ein Versuch und noch nicht verfügbar.

## Systemvoraussetzungen

Werte vom 19.10.2026 mit dem Testdatensatz (drei Standorte, 260 Kunden, rund 2.300 Termine). „Gemessen“ und
„geschätzt“ sind unterschieden; was nicht dabeisteht, ist eine Vorgabe. Ausführlich mit allen Messwerten:
Hilfethema [Systemanforderungen](../../web/src/help/content/systemanforderungen.md).

### Windows-PC am Empfang

Gilt für jeden PC mit der Windows-App. Die App bringt ihre .NET-Laufzeit mit; es muss nichts weiter installiert werden.

| | Mindestens | Empfohlen |
| --- | --- | --- |
| Windows | Windows 10 22H2, 64 Bit | Windows 11, 64 Bit |
| Prozessor | 2 Kerne, x64 (Intel oder AMD, ab etwa 2017) | 4 Kerne |
| Arbeitsspeicher | 4 GB | 8 GB |
| Speicherplatz | 1 GB frei | 2 GB frei |
| Bildschirm | 1280 × 720 | 1920 × 1080 |
| Netzwerk (nur mit Server oder Haupt-PC) | LAN oder WLAN, dauerhaft | LAN-Kabel |

- **Download:** App allein 56 MB, entpackt 122 MB (gemessen).
- **WebView2:** nur für die Hilfe in der App nötig; auf Windows 11 vorhanden, auf Windows 10 kommt sie mit Microsoft Edge.
- **ARM-Prozessoren** (z. B. Snapdragon): nicht geprüft; Windows 11 führt die x64-App dort in Emulation aus.
- **Windows 10** bekommt seit Oktober 2025 nur noch mit erweitertem Support Sicherheitsupdates. Für einen PC mit Kassa
  ist Windows 11 die bessere Wahl.

### Zusätzlich für Einzelplatz und Haupt-PC

Auf dem PC laufen dann auch die Datenbank und der Server von AISPA Studio. Die Werte kommen zu denen oben dazu.

| | Zusätzlich |
| --- | --- |
| Prozessor | 4 Kerne empfohlen |
| Arbeitsspeicher | 1 GB im Betrieb (gemessen: Server 300 MB, Datenbank 100 MB), 8 GB gesamt empfohlen |
| Speicherplatz | rund 0,6 GB für das Paket, dazu Daten (geschätzt 100 bis 200 MB je Standort und Jahr) |
| Sicherung | ein zweites Laufwerk, ein USB-Stick oder ein Netzordner |

- **Download:** ganzes Paket mit Datenbank rund 185 MB.
- **Haupt-PC:** muss laufen, solange im Spa gearbeitet wird, und braucht im Router eine feste IP-Adresse.
  Energiesparmodus während der Öffnungszeiten aus; eine kleine USV schützt die Datenbank bei Stromausfall.

### Server

| | Mindestens | Hinweis |
| --- | --- | --- |
| Betriebssystem | Linux mit Docker Engine oder Windows/macOS mit Docker Desktop | geprüft unter Linux (CI) und Windows |
| Docker Compose | 2.24 oder neuer | nötig für `compose.scale.yaml` |
| Prozessor, Speicher | wie „Einzelplatz“ oben | je weitere Instanz rund 300 MB Arbeitsspeicher (gemessen je Server-Prozess) |
| Datenbank | PostgreSQL 17 | Image `postgres:17-alpine`, 424 MB |
| Redis | 8 | nur bei mehreren Server-Instanzen, Image `redis:8-alpine`, 120 MB |
| HTTPS | eigener Reverse Proxy mit Zertifikat | nicht im Paket enthalten |

- **Docker Desktop** ist für Firmen ab 250 Mitarbeitenden oder 10 Mio. USD Umsatz kostenpflichtig; Day-Spas bis
  50 Mitarbeitende fallen nicht darunter.
- **Lizenz von Redis 8:** steht nicht mehr unter BSD. Für den Eigenbetrieb kein Hindernis; wer Redis mitliefert oder
  als Dienst anbietet, prüft die Lizenz neu.

### Optional: lokale KI ohne Internet

Für die KI ohne Cloud (Ollama mit Gemma 3, 4 Mrd. Parameter):

| | Mindestens | Empfohlen |
| --- | --- | --- |
| Prozessor | 4 Kerne mit AVX2 | 8 Kerne |
| Arbeitsspeicher | 8 GB gesamt | 16 GB gesamt |
| Speicherplatz | 6 GB frei | 15 GB frei (mit Docker) |
| Grafikkarte | nicht nötig | nicht nötig |

> [!CAUTION]
> Das Docker-Image von Ollama ist groß: 3,8 GB Download, 9,4 GB auf der Festplatte (gemessen, Ollama 0.40.6). Für den
> Einzelplatz ist das Windows-Programm von Ollama der kleinere Weg.

### Handys und Browser

„Mein Tag“ für Mitarbeitende, die Online-Buchung für Kundinnen und Kunden und das Dashboard laufen in jedem aktuellen
Browser (Chrome, Edge, Firefox, Safari). Sie brauchen den Server bzw. den Haupt-PC; im Einzelplatz nur auf demselben PC.

### Kassa-Geräte

- **Bondrucker:** Thermodrucker mit ESC/POS über LAN, Papier 80 mm oder 58 mm.
- **Kartenterminal:** mit ZVT oder OPI über LAN, vom Zahlungsanbieter des Spas.
- Netzfähige Drucker und Terminals lassen sich auch direkt aus der Cloud ansprechen, wenn das Gerät es unterstützt.
- **RKSV (Österreich) und TSE (Deutschland):** Im Prototyp läuft eine Test-Signatur; die echte Signatur ist offen.

## Weiter

- [Einzelplatz und mehrere PCs im Spa](standalone.md)
- [Eigener Server mit Docker](server.md)
- [Betriebsarten und Module: Referenz](operating-modes.md) mit allen technischen Einzelheiten
- [Entwicklung](../development.md): bauen, starten und prüfen aus dem Quellcode
