# Entwicklung

Für alle, die AISPA Studio bauen, starten und prüfen wollen. Der Betrieb im Spa steht in der [Installation](installation/README.md).

## Voraussetzungen

- .NET SDK 10
- Node.js 22
- Docker (für PostgreSQL und die Tests)

## Lokal starten

```bash
cd web && npm install && cd ..
dotnet run --project src/AispaStudio.AppHost
```

In Visual Studio oder Rider ist das Startprojekt `AispaStudio.AppHost`, nicht `AispaStudio.Api`; Docker Desktop muss
laufen, weil Aspire PostgreSQL und Mailpit als Container startet. Die API allein startet nur mit eigener Datenbank
(`ConnectionStrings:aispadb` in den User-Secrets), sonst bricht sie mit einem Hinweis darauf ab.

Das Aspire-Dashboard zeigt die Adressen von API und Web. Verwaltet wird nur in der Windows-App; im Browser gibt es die Seite
„Anmelden“ (`/anmelden/`). Dort meldet „Als Testkonto anmelden“ mit einem Konto des Beispiel-Spas an (nur in der
Entwicklungsumgebung, jede Rolle); „Testkonto anlegen“ legt ein neues an. Danach „Passkey hinzufügen“ und nach dem Abmelden
„Mit Passkey anmelden“. Angemeldet liefert „Code für die Windows-App“ den Code, mit dem sich die App anmeldet.

Die Windows-App startet im Dashboard über die Ressource „windows-app“ (Starten-Knopf). So gestartet ist sie mit dieser API
verbunden. Die Adresse der Web-Apps von Vite braucht sie nur für die Hilfe; startet man sie anders (z. B. aus Visual Studio),
fragt sie die API danach. Die Verwaltung selbst ist nativ und braucht den Browser nicht.

In einer leeren Datenbank legt die API in der Entwicklungsumgebung ein vollständiges Beispiel-Spa an
(`Database:SeedDemoData`): drei Standorte mit gemeinsamem Kundenstamm, Team mit Testkonten für alle Rollen,
Dienstplan, 260 Kundinnen und Kunden, Termine der letzten acht und der nächsten vier Wochen, Kassa mit Belegen,
Gutscheinen und Tagesabschlüssen, Anamnesen, Warteliste und Posteingang. Der erste Start dauert dadurch rund
eine halbe Minute und läuft im Hintergrund: Das Aspire-Dashboard zeigt den Stand an der Ressource `api`
(„Beispiel-Spa wird eingerichtet (seit N s)“, Health-Check `startup`, auch unter `/health`), `web` und `windows-app`
starten erst danach. Die Windows-App nennt ihn auf der Anmeldung und zeigt die Testkonten, sobald alles da ist.
Meldet die API „Beispieldaten unvollständig“, wurde ein früheres Einspielen abgebrochen (z. B. Aspire beendet); dann
das Volume `aispa-postgres-data` löschen (Docker Desktop › Volumes) bzw. `docker compose down -v` und neu starten.
Die API löscht nie selbst. Der Behandlungskatalog (30 Behandlungen) ist an öffentliche Preislisten Wiener Day-Spas angelehnt.
Testkonten und Inhalt: [docs/testdaten.md](testdaten.md). Eine bestehende Datenbank bekommt ihn nicht
nachträglich; dafür die Datenbank leeren.

**Datenbank sichern und zurücksetzen (nur Entwicklung):** An der Ressource `postgres` im Aspire-Dashboard (Menü „…“ der
Zeile) gibt es drei Befehle:

- **Sicherung erstellen** legt mit `pg_dump -Fc` eine Datei `.aispa-backups/aispadb-JJJJ-MM-TT-HHmmss.dump` im Repository
  an und meldet Dateiname und Größe.
- **Sicherung einspielen …** fragt nach der Sicherung (leer lassen = die neueste) und bestätigt, dass die Daten in
  `aispadb` überschrieben werden. Ablauf: API anhalten, Datenbank leeren und neu anlegen, `pg_restore`, API wieder starten.
  Auch bei einem Fehler wird die API wieder gestartet; die Fehlermeldung steht im Dashboard.
- **Beispiel-Spa neu einspielen …** leert die Datenbank und startet die API neu; Schema und Beispiel-Spa entstehen
  wieder von allein (Stand: Health-Check `startup` der API).

Die Befehle laufen nur im AppHost, nie im Betrieb, und sind nur bei laufendem PostgreSQL wählbar. Sie rufen
`docker exec` im PostgreSQL-Container auf (Docker muss auf `PATH` stehen) und brauchen kein Kennwort. `.aispa-backups/`
steht in `.gitignore`: Sicherungen enthalten Personen- und Gesundheitsdaten des Beispiel-Spas und gehören nie ins
Repository. Eine Sicherung gibt man weiter, indem man die Datei kopiert; zum Einspielen legt man sie in den Ordner
(Name `aispadb-*.dump`).

## Lokal mit Docker testen (ohne .NET und Node)

Nur Docker Desktop nötig. Im Hauptordner des Repositorys:

```bash
docker compose up -d --build
```

| Dienst | Adresse |
| --- | --- |
| Anmelden (Code für die Windows-App, Einladung) | http://localhost:5173/anmelden/ |
| Online-Buchung | http://localhost:5173/buchen/ |
| Mein Tag (Handy der Therapeut:innen) | http://localhost:5173/mein-tag/ |
| API, für die Windows-App | http://localhost:5086 |
| Test-Postfach (Mailpit) | http://localhost:8025 |

Die API läuft wie bei `dotnet run` in der Entwicklungsumgebung: sie legt beim ersten Start das Beispiel-Spa an
(rund eine halbe Minute, Fortschritt in `docker compose logs -f api`) und bietet die Anmeldung mit den Testkonten
([docs/testdaten.md](testdaten.md)). Die Ports hören nur auf diesem Rechner (127.0.0.1); so nie öffentlich betreiben.

Windows-App dagegen verbinden: die fertige App liegt nach jedem grünen Lauf auf `main` im Release
[„Windows-App“](https://github.com/vompa/aispa-studio/releases/tag/windows-app) (ZIP entpacken, `AispaStudio.Desktop.exe` starten). Sie verbindet sich
ohne Einstellung mit http://localhost:5086. Anmelden: im Web „Als Testkonto anmelden“, dann „Windows-App verbinden“ und den
Einmal-Code in der App eingeben, oder einfacher direkt in der App ein Testkonto des Beispiel-Spas wählen
([docs/testdaten.md](testdaten.md)).

Neuer Stand: `git pull` und noch einmal `docker compose up -d --build`. Von vorn mit leerer Datenbank:
`docker compose down -v`. Claude für Assistent und Terminannahme: `AISPA_CLAUDE_API_KEY=<schlüssel>` in eine Datei `.env`
neben `compose.yaml` schreiben (steht in `.gitignore`).

Lokale KI ohne Internet (optional, [ADR 0057](entscheidungen/0057-lokale-ki-optional-mit-ollama.md)): in `.env` zusätzlich
`COMPOSE_PROFILES=ki-lokal` und `AISPA_LOCAL_AI_URL=http://ollama:11434`, dann `docker compose up -d` und unter Stammdaten > KI
das Modell herunterladen (3,3 GB). Mit Aspire: `dotnet run --project src/AispaStudio.AppHost -- --LocalAi:Enabled=true`.
Was der PC dafür braucht, steht im Hilfethema „Systemanforderungen“.

## Prüfen wie die CI

```bash
dotnet format AispaStudio.slnx --verify-no-changes
dotnet build AispaStudio.slnx -c Release
dotnet test --solution AispaStudio.slnx -c Release
cd web && npm run gen:api && npm run lint && npm run typecheck && npm test && npm run build
```

## Konventionen

- Paketversionen stehen zentral in `Directory.Packages.props`; Warnungen sind Fehler.
- `dotnet build` schreibt das OpenAPI-Dokument nach `web/openapi/aispa-api.json`; `npm run gen:api`
  erzeugt daraus `web/src/api/schema.d.ts`. Beides wird committet, die CI prüft, dass es aktuell ist.
- Datenbankänderungen als EF-Core-Migration:
  `dotnet tool restore && dotnet ef migrations add <Name> -p src/AispaStudio.Api --context AispaDbContext -o Data/Migrations`.
  Ein Test schlägt fehl, wenn eine Migration fehlt. Ohne `Database:MigrateOnStartup` migriert der Befehl
  `dotnet AispaStudio.Api.dll migrate` die Datenbank einmal und beendet sich (Exitcode 0 bei Erfolg, 1 bei einem Fehler der Migration, sonst ungleich 0);
  im Docker-Image `docker compose run --rm api migrate`.
- Länger laufende oder externe Arbeit (Mails, KI, Schnittstellen) läuft über die Queue, nicht im HTTP-Aufruf.
