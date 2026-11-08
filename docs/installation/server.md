# Eigener Server mit Docker

Mit einem Server arbeiten mehrere Standorte, die Online-Buchung, die Handy-Apps und die KI-Terminannahme zusammen.
Die Windows-App am Empfang verbindet sich dann mit diesem Server.

Voraussetzungen: [Server](README.md#server) und [Windows-PC am Empfang](README.md#windows-pc-am-empfang).

> [!WARNING]
> Der Server-Betrieb ist **noch nicht für den Echtbetrieb freigegeben**. Die beiden Compose-Dateien im Repository
> laufen in der Entwicklungsumgebung: mit Testkonten ohne Passkey, Beispiel-Spa, Test-Postfach und Test-Signatur der
> Kassa. Eine fertige Produktions-Konfiguration mit HTTPS gibt es noch nicht. **So nie öffentlich erreichbar betreiben.**

## Zum Ausprobieren: ein Server auf einem Rechner

Nur Docker ist nötig, kein .NET und kein Node.js.

```bash
git clone https://github.com/vompa/aispa-studio.git
cd aispa-studio
docker compose up -d --build
```

Beim ersten Start legt der Server ein Beispiel-Spa mit Testdaten an (rund eine halbe Minute, Fortschritt mit
`docker compose logs -f api`). Alle Ports hören nur auf diesem Rechner (127.0.0.1).

| Dienst | Adresse |
| --- | --- |
| Anmelden (auch Code für die Windows-App, Einladung) | http://localhost:5173/anmelden/ |
| Online-Buchung | http://localhost:5173/buchen/ |
| Mein Tag (Handy der Mitarbeitenden) | http://localhost:5173/mein-tag/ |
| Über AISPA Studio | http://localhost:5173/ueber/ |
| API, für die Windows-App | http://localhost:5086 |
| Test-Postfach (Mailpit) | http://localhost:8025 |

Testkonten und Inhalt des Beispiel-Spas: [Testdaten](../testdaten.md).

**Windows-App verbinden:** die App aus dem Release [„Windows-App“](https://github.com/vompa/aispa-studio/releases/tag/windows-app)
entpacken und **ohne** `--standalone` starten. Sie verbindet sich ohne weitere Einstellung mit `http://localhost:5086`.
Angemeldet wird direkt in der App mit einem Testkonto oder im Web über „Als Testkonto anmelden“ › „Code für die
Windows-App“.

**Optional:**

- **KI mit Claude** (Assistent, Terminannahme): eine Datei `.env` neben `compose.yaml` mit
  `AISPA_CLAUDE_API_KEY=<schlüssel>` anlegen. Die Datei steht in `.gitignore`.
- **Lokale KI ohne Internet:** in `.env` zusätzlich `COMPOSE_PROFILES=ki-lokal` und
  `AISPA_LOCAL_AI_URL=http://ollama:11434`, dann `docker compose up -d` und in der App unter Stammdaten › KI das Modell
  laden. Achtung Größe: [lokale KI](README.md#optional-lokale-ki-ohne-internet).

**Neuer Stand:** `git pull` und noch einmal `docker compose up -d --build`. **Von vorn mit leerer Datenbank:**
`docker compose down -v` (löscht alle Daten).

## Mehrere Server-Instanzen

So läuft es später in der Cloud: ein Migrationslauf, zwei API-Instanzen, genau ein Worker für Hintergrundarbeit,
Redis für Live-Meldungen und nginx davor. Zum Ausprobieren auf einem Rechner (Docker Compose 2.24 oder neuer):

```bash
docker compose -f compose.yaml -f compose.scale.yaml up -d --build
```

Web und Test-Postfach liegen an denselben Adressen wie oben; die API hat keinen eigenen Port, die Windows-App meldet
sich an `http://localhost:5173` an. Einzelheiten zu Rollen, Redis, Proxy und Healthchecks:
[Betriebsarten und Module: Referenz](operating-modes.md#betriebsart-und-webmodule) und Hilfethema
[Server-Einstellungen](../../web/src/help/content/server.md) (Abschnitt „Mehrere Instanzen mit Docker Compose“).

## Bausteine eines Servers

| Baustein | Image | Port im Container | Aufgabe |
| --- | --- | --- | --- |
| Datenbank | `postgres:17-alpine` | 5432 | alle Daten aller Spas, getrennt je Mandant |
| API | aus `docker/api.Dockerfile` (.NET 10) | 8080 | Geschäftslogik, Anmeldung, Live-Meldungen, Hintergrundläufe |
| Web | aus `docker/web.Dockerfile` (nginx 1.29) | 80 | Web-Apps ausliefern, `/api/` und `/hubs/` an die API weiterleiten |
| Redis-Protokoll (Valkey) | `valkey/valkey:8-alpine` | 6379 | nur bei mehreren API-Instanzen: Live-Meldungen verteilen |
| Mailpit | `axllent/mailpit` | 8025 | nur zum Testen: fängt alle Mails ab |
| Ollama | `ollama/ollama:0.40.6` | 11434 | optional: KI ohne Internet |

Die API wird über Umgebungsvariablen eingestellt: Aus dem Schlüssel `Abschnitt:Name` wird `Abschnitt__Name`.

## Wichtige Einstellungen

| Einstellung | Pflicht | Wozu |
| --- | --- | --- |
| `ConnectionStrings__aispadb` | ja | Verbindung zur Datenbank; die Datenbank `aispadb` muss vorher existieren |
| `ASPNETCORE_ENVIRONMENT` | – | ohne Angabe `Production`; nur `Development` erlaubt Testkonten und Beispieldaten |
| `Database__MigrateOnStartup` | – | `true` bringt die Datenbank beim Start auf den neuesten Stand; sonst Befehl `migrate` |
| `HealthData__CurrentKeyId`, `HealthData__Keys__<id>` | für Echtbetrieb | Schlüssel für Gesundheitsdaten (32 Byte, Base64); ohne ihn lassen sich keine Anamnesen speichern |
| `OnlineBooking__PublicUrl`, `Passkeys__ServerDomain` | für Echtbetrieb | öffentliche Adresse für Online-Buchung und Anmeldung mit Passkey |
| `Mail__Smtp__Host`, `Mail__FromAddress` | für Echtbetrieb | Mailversand |
| `Ai__Claude__ApiKey` | – | KI mit Claude; ohne Schlüssel sind Assistent und Terminannahme aus |
| `Modules__<Name>` | – | Webmodule einzeln ein- oder ausschalten |
| `Hosting__Role` | – | `All` (Standard), `Api` oder `Worker` für mehrere Instanzen |

Geheimnisse (Passwörter, Schlüssel) nur als Umgebungsvariable oder aus einem Geheimnisspeicher, nie in einer Datei
im Repository. Alle Einstellungen mit Beschreibung: Hilfethema
[Server-Einstellungen](../../web/src/help/content/server.md).

## Datenbank migrieren

```bash
docker compose run --rm api migrate
```

Der Befehl bringt die Datenbank auf den neuesten Stand und endet (Exitcode 0 bei Erfolg, 1 bei einem Fehler der
Migration). Mit mehreren Instanzen migriert ein eigener Lauf je Update; die Instanzen laufen dann ohne
`Database__MigrateOnStartup`.

## Datensicherung

Die Datenbank mit `pg_dump` sichern, mit dem Aufbau aus `compose.yaml` zum Beispiel:

```bash
docker compose exec db pg_dump -U postgres -Fc aispadb > aispa-$(date +%Y%m%d).dump
```

Mit gesichert gehören der Schlüsselring (Volume `api-keys`, oder mit `DataProtection__Store=Database` schon in der
Datenbank) und die Schlüssel für Gesundheitsdaten aus dem Geheimnisspeicher. Ohne diese Schlüssel sind Anmeldungen
und Anamnesen aus einer Sicherung nicht mehr lesbar.

## Vor dem Echtbetrieb

Bevor ein Server öffentlich erreichbar wird, fehlt noch:

- eine Produktions-Konfiguration ohne Entwicklungsumgebung, Testkonten und Beispieldaten,
- HTTPS über einen Reverse Proxy oder Ingress mit Zertifikat (nginx im Paket lauscht nur auf Port 80),
- eine echte Signatur für die Kassa (RKSV, TSE) statt der Test-Signatur,
- die Checkliste „Vor dem Echtbetrieb prüfen“ im Hilfethema [Server-Einstellungen](../../web/src/help/content/server.md).

Die geplante Zielarchitektur in der Cloud (Azure Container Apps, PostgreSQL, Redis) steht in
Hosting-Kosten und [ADR 0087](../entscheidungen/0087-mehrinstanz-betrieb.md).
