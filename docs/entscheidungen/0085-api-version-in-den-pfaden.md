# 0085. API-Version in den Pfaden: `/api/v1`, alte Pfade bleiben als Alias

- **Status:** angenommen
- **Datum:** 02.11.2026
- **Entschieden von:** Auftraggeber (Arbeitsanweisung „Avalonia-Client gegen Azure-API“, Aufgabe 2, Runde R06), Ausgestaltung Claude, autonom nach ADR 0019 (Slice 121)

## Kontext

Die Arbeitsanweisung vom 30.10.2026 verlangt eine versionierte API, damit ausgelieferte Windows-Apps weiterlaufen, wenn sich die API ändert. Bisher hatte die API keine Version: Jeder Endpunkt lag unter `/api/…`, jedes Modul schreibt seinen Präfix selbst (88 Registrierungen in 61 Dateien). Ausgelieferte Clients sind die Windows-App (Einzelplatz und an einem Server, Slice 118) und die Web-Seiten, die der Browser zwischenspeichert (Service-Worker von „Mein Tag“).

Rechte (`PermissionFilter`) und Kanal-Sperre (`UseChannelGate`, Slice 104) hängen an Endpunkt-Metadaten. Mandantensperre (`TenantLocks`), Mandant des Kundenbereichs (`OnlineTenant`) und der Kanal im Verlauf (`ChangeActor`) prüfen dagegen den Pfad.

## Optionen

| Option | Bewertung |
| --- | --- |
| A: Paket `Asp.Versioning` mit Version in Kopfzeile oder Abfrage | Neues Paket; die Version ist in Logs und Links nicht sichtbar; für eine einzige Version zu viel |
| B: Jede Gruppe zweimal registrieren (`/api` und `/api/v1`) | Doppelte Endpunkte im Kanal-Scan und in OpenAPI; jede Pfadprüfung müsste beide Präfixe kennen, eine übersehene Stelle öffnet die Mandantensperre |
| C: Alle Routen auf `/api/v1` umschreiben, alte Pfade umleiten | Großer Diff; dieselben Pfadprüfungen müssten umgestellt werden |
| D: Pfadpräfix `/api/v1`, ein Startfilter schreibt ihn vor dem Routing auf `/api/…` um | gewählt |

## Entscheidung

1. **Version im Pfad.** Die Team-API heißt nach außen `/api/v1/…`. Ein Startfilter (`ApiVersion.AddApiVersion`, `src/AispaStudio.Api/System/ApiVersion.cs`) schreibt `/api/v1/<rest>` vor der ganzen Pipeline auf `/api/<rest>` um. Jeder Endpunkt existiert nur einmal. Rechte, Kanal-Sperre, Mandantensperre, Verlauf und Rate-Limiter sehen unter beiden Pfaden denselben Pfad.
2. **`/api/…` ohne Version ist v1** und bleibt erreichbar, bis es eine v2 gibt. Danach bleibt v1 unter `/api/v1/…` und `/api/…` mindestens 12 Monate nach der ersten Windows-App, die v2 spricht. Abschalten braucht einen eigenen ADR.
3. **Breaking Change nur über eine neue Version.** Breaking ist: ein Pfad, Feld oder Typ fällt weg oder wird umbenannt, die Bedeutung eines Felds oder Statuscodes ändert sich, ein Request bekommt ein neues Pflichtfeld. Ein Feld eines C#-Records mit Standardwert erscheint im Schema als `required` und zählt dazu. In v1 erlaubt bleibt: neue Endpunkte, neue optionale Felder in Requests, neue Felder in Antworten. Eine v2 entsteht als eigene Gruppe `/api/v2/…` für die geänderten Endpunkte; unveränderte bleiben in v1.
4. **Routen im Code bleiben `/api/…`.** Neue Endpunkte werden wie bisher unter `/api/…` registriert und sind damit unter `/api/v1/…` erreichbar. Das OpenAPI-Dokument nennt sie mit v1; ein Dokument-Transformer (`ApiVersion.VersionPaths`) schreibt die Pfade um.
5. **Server vor Clients aktualisieren.** Eine neue Windows-App an einem alten Server bekommt 404. Einen Rückfall auf `/api/…` hat die App nicht.
6. **Clients.** Die Windows-App hängt die Version an einer Stelle an (`HttpApiClient`). ViewModels, Offline-Stand und vorgemerkte Aufrufe behalten `api/…`, ein Offline-Stand aus einer älteren App bleibt deshalb gültig. Die Team-Seiten im Web (Anmelden, Mein Tag, Dashboard) sprechen v1 über den erzeugten Client.

Eingeordnet:

| Bereich | Pfad | Version |
| --- | --- | --- |
| Team-API (Windows-App, Mein Tag, Dashboard, Anmelden) | `/api/v1/…`, Alias `/api/…` | v1 |
| Kundenbereich | `/api/online/…` | ohne. Kommt mit dem Server aus; eine eigene Version erst, wenn eine fremde App ihn nutzt |
| Webhooks der Anbieter (Zahlung, SMS, WhatsApp, Anrufe) | `/api/hooks/…` | ohne. Die Adressen sind beim Anbieter hinterlegt und werden nie verschoben |
| OAuth-Rückrufe | `/api/online/auth/external/…` | ohne, aus demselben Grund |
| Betreiber | `/api/operator/…` | ohne. Eigener Schlüssel, eigene Werkzeuge |
| Live-Hub | `/hubs/live` | ohne. Der Vertrag liegt in `AispaStudio.Contracts` (`LiveHubContract`); neue Ereignisse kommen dazu, bestehende ändern sich nicht |
| Health, OpenAPI | `/health`, `/alive`, `/openapi/v1.json` | nicht unter `/api` |
| Sprungziele | Export-Links der Pflichtberichte (`ExportPath`) | v1. Die Hilfe verlinkt keine API-Pfade |

## Begründung

Der Pfad ist die einfachste sichtbare Version, ohne neues Paket. Das Umschreiben vor dem Routing hält die Sicherheitsregeln an einer Stelle: Wer einen neuen Präfix-Check schreibt, muss die Version nicht kennen. Die Routen im Code umzuschreiben brächte nur Diff, denn die Version ist der Name nach außen.

## Folgen und Stand

- Umgesetzt in Slice 121 (R06, Ä283). Tests: `ApiVersionTests` (Scan über alle Endpunkte mit Anmeldung: gleicher Status unter beiden Pfaden, Rechte, Kanal-Sperre, Mandantensperre, Dokument) und `ApiVersionClientTests` (die Windows-App spricht v1).
- Die Adressen in `Created`-Antworten (`Location`) nennen weiter `/api/…`. Sie bleiben über den Alias gültig, kein Client liest sie.
- Die Statusabfrage des Einzelplatz-Servers beim Start (`LocalServer.PingAsync`) fragt weiter `/api/system/status`, damit sie auch einen älteren, noch laufenden lokalen Server erkennt.

## Quellen

- Rundenakte `work/rounds/R06-api-client-gaps.md`, Ä283
- Plan `work/plans/2026-11-02-slice-121.md`
- [ADR 0041](0041-einzelplatz-standalone-und-zuschaltbare-webmodule.md), [ADR 0084](0084-alle-masken-nativ-web-fuer-dashboard-und-handy.md)
