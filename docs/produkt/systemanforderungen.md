# Systemanforderungen: Messprotokoll

Die Systemanforderungen für Kundinnen und Kunden stehen im Hilfethema [Systemanforderungen](../../web/src/help/content/systemanforderungen.md) (in Web und Windows-App unter „Hilfe“). Diese Seite hält fest, wie die Zahlen dort entstanden sind, damit sie sich bei neuen Ständen nachmessen lassen.

Auftraggeber am 19.10.2026: „dann braucht es noch die windows mindestanforderungen schlank und mit zusatz paktene“. Gegliedert ist deshalb in eine schlanke Ausstattung (Windows-App am Empfang gegen einen Server) und Zusatzpakete (Einzelplatz und Haupt-PC, lokale KI, Docker, Kassa-Geräte), deren Werte addiert werden.

## Gemessen am 19.10.2026 (Stand `main` nach PR #144 plus PR „Lokale KI“)

Umgebung: Linux-Container, 4 Kerne, 15,7 GB Arbeitsspeicher, .NET SDK 10.0.113, Docker 29.9.1. Kein Windows verfügbar; Paketgrößen sind die Windows-Pakete (`-r win-x64 --self-contained`), Speicher und Zeiten die Linux-Werte derselben Programme.

| Was | Wert | Wie |
| --- | --- | --- |
| Windows-App allein | 73 MB ZIP, 212 MB entpackt (Stand 19.10.; seit T-011 56 MB und 122 MB, siehe unten) | `dotnet publish src/AispaStudio.Desktop -c Release -r win-x64 --self-contained`, gezippt |
| Server (API) | 71 MB ZIP, 174 MB entpackt | dasselbe für `src/AispaStudio.Api` mit `-p:OpenApiGenerateDocuments=false` |
| Web-Oberfläche | 1 MB ZIP, 2 MB entpackt | `npm run build`, `web/dist` |
| Ganzes Windows-Paket mit PostgreSQL | 201 MB (200.559.767 Byte) | Release `windows-app`, Asset `AispaStudio-Windows.zip`, Stand `846bea7` |
| PostgreSQL im Paket | rund 56 MB ZIP | Differenz der Zeilen oben; entpackt geschätzt 200 MB |
| Server bis „Healthy“ | 8,2 s | Start mit vorhandenen Daten bis `GET /health` |
| Server-Speicher im Betrieb | 267 MB nach Start, 308 MB nach 35 Kalenderabrufen, Tagesbericht und Kundensuche | `VmRSS` des Prozesses |
| Server-Speicher beim Anlegen der Testdaten | 1,3 GB Spitze | `VmRSS` während `DemoSeeder` (321 s) |
| PostgreSQL-Speicher | 98 MB | `docker stats`, `postgres:17-alpine` |
| Datenbank mit Testdaten | 48 MB | `pg_database_size`, 3 Standorte, 260 Kunden, 2.269 Termine, 2.226 Belege über 12 Wochen |
| Kalendertag | 0,41 s erster Abruf, danach 0,03 bis 0,06 s, 15 bis 20 kB | `GET /api/locations/{id}/calendar?date=…` mit `curl` |
| Ollama ohne Modell | 76 MB Speicher | `docker stats`, Ollama 0.40.6 |
| Ollama-Image | 3,79 GB Download, 9,37 GB auf der Festplatte | `docker images` |
| PostgreSQL-Image | 424 MB | `docker images` |

## Windows-Paket: Varianten gemessen am 23.10.2026 (T-011)

Umgebung: Windows 11, .NET SDK 10, Stand `dev` (`ee66289`), Release. Auslöser: Der Build-Ordner der Windows-App war 590 MB groß und enthielt Fremd-Natives für Plattformen, die unter Windows nie laufen. Gemessen wurde `dotnet publish src/AispaStudio.Desktop -c Release` mit den Parametern der Spalte „Aufruf“, Ausgabe in einen eigenen Ordner. „Start ok“: `AispaStudio.Desktop.exe` ohne Schalter gestartet (eigenes `LOCALAPPDATA`), nach 20 s lebt der Prozess und das Fenster „AISPA Studio – Rezeption“ steht. Die Zeit bis zum Fenster schwankt (3,6 bis 14,8 s, der erste Start nach dem Schreiben der Dateien ist langsam) und taugt nicht zum Vergleich der Varianten.

| Variante | Aufruf (Zusatz zu `dotnet publish`) | Größe entpackt | Dateien | ZIP | Publish-Dauer | Start ok | Bemerkung |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 0 Ausgangsstand | keiner (alle Plattformen) | **579 MB, groß** | 95 | 192 MB | 94 s | ja | `runtimes/` mit 17 Plattformen (Linux, macOS, Android, drei Windows-Architekturen), dazu 100 MB `.pdb` für Skia und HarfBuzz je Windows-Architektur |
| a abhängig | `-r win-x64 --no-self-contained` | 145 MB | 58 | – | 19 s | ja | verlangt das installierte .NET 10; die `.pdb` von Skia (80 MB) und HarfBuzz (20 MB) liegen noch dabei |
| b selbstständig | `-r win-x64 --self-contained` (Stand der CI vor T-011) | **222 MB, groß** | 245 | 81 MB | 12 s | ja | bringt die .NET-Laufzeit mit |
| c eine Datei | b plus `PublishSingleFile`, `EnableCompressionInSingleFile`, `IncludeNativeLibrariesForSelfExtract` | 161 MB, groß | 6 | – | 62 s | ja | entpackt Natives beim Start in `%TEMP%` |
| d Trimmen | b plus `PublishTrimmed`, `TrimMode=partial` | – | – | – | 5 s | **Build scheitert** | IL2026 aus `AispaStudio.Contracts` (`KeySeal.cs`: `System.Text.Json` mit Reflexion); weitere Reflexion in SignalR und Avalonia-Bindungen erwartet. Nicht nachgebessert |
| e vorkompiliert | b plus `PublishReadyToRun` | 245 MB, groß | 245 | – | 84 s | ja | größer als b |
| **f Empfehlung** | `-r win-x64 --self-contained` ohne die nativen `.pdb` (jetzt der Standard beim Veröffentlichen) | **122 MB** | 243 | **56 MB** | 53 s | ja | b minus 100 MB |
| a2 abhängig ohne `.pdb` | wie a, ohne die nativen `.pdb` | 45 MB | 56 | 22 MB | 12 s | ja | verlangt das installierte .NET 10 |
| g eine Datei ohne `.pdb` | wie c, ohne die nativen `.pdb` | 61 MB | 4 | 52 MB | 49 s | ja | spart gegenüber f im ZIP nur 4 MB |
| h vorkompiliert ohne `.pdb` | wie e, ohne die nativen `.pdb` | 145 MB, groß | 243 | 66 MB | 108 s | ja | +23 MB gegenüber f |

Arbeitsspeicher nach dem Start der leeren App: 230 bis 280 MB (Variante f 246 MB), also kein Unterschied zwischen den Varianten. Der Server-Teil des Pakets (API) wurde unverändert gemessen: `dotnet publish src/AispaStudio.Api -c Release -r win-x64 --self-contained -p:OpenApiGenerateDocuments=false` ergibt 179 MB in 437 Dateien, ZIP 72 MB.

**Empfehlung: Variante f.** Nur win-x64, selbstständig, ohne die `.pdb` der nativen Bibliotheken; keine eine Datei, kein Trimmen, kein ReadyToRun. Begründung in [ADR 0078](../entscheidungen/0078-windows-paket-win-x64.md). Das ganze Paket (App 56 MB, API 72 MB, Web 1 MB, PostgreSQL rund 56 MB als ZIP) liegt damit bei rund 185 MB statt 201 MB; entpackt sind es weiter rund 500 MB (App 122, API 179, PostgreSQL rund 200), **das ganze Paket ist also groß** und wird mit PostgreSQL und zweiter .NET-Laufzeit nur durch eine gemeinsame Laufzeit oder ein schlankeres PostgreSQL kleiner (offen, siehe ADR).

Nicht geprüft: Start des Einzelplatzes mit dem echten Paket (Server, Web, PostgreSQL), weil auf dem Messrechner kein PostgreSQL für Windows liegt; das übernimmt der CI-Test `LocalServerPackageTests`. Kein Lauf auf einem zweiten, sauberen PC (SmartScreen, Virenscanner).

## Nicht gemessen, geschätzt

- Arbeitsspeicher der Windows-App und von WebView2: braucht Windows. Schätzung aus vergleichbaren Avalonia- und WebView2-Anwendungen.
- PostgreSQL entpackt unter Windows: aus der ZIP-Größe geschätzt.
- Lokales Modell geladen (Speicher, Antwortzeit): das Modellverzeichnis von Ollama war aus der Entwicklungsumgebung nicht erreichbar. Größe 3,3 GB laut Ollama; Speicher und Zeiten geschätzt. Nachmessen, sobald ein Modell verfügbar ist: `docker stats` während einer Anfrage und die Dauer von `POST /api/chat`.
- Datenwachstum je Jahr: hochgerechnet aus den 12 Wochen des Testdatensatzes, mit Reserve für Anamnesen und Fotos.

## Nachmessen

```bash
dotnet publish src/AispaStudio.Desktop -c Release -o /tmp/m/desktop   # win-x64 und selbstständig stehen in der Projektdatei
dotnet publish src/AispaStudio.Api -c Release -r win-x64 --self-contained -o /tmp/m/api -p:OpenApiGenerateDocuments=false
du -sm /tmp/m/*
# Server mit Testdaten: aus src/AispaStudio.Api starten (sonst fehlen die appsettings), Development, eigene Datenbank
grep VmRSS /proc/<pid>/status
```

Auf einem Windows-PC zusätzlich: Arbeitsspeicher von `AispaStudio.Desktop.exe` und `msedgewebview2.exe` im Task-Manager mit offener Kalender- und Verwaltungsseite.
