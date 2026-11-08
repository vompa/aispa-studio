# Arbeitsanweisung: AISPA Studio mehrinstanzfähig machen

**Für:** Claude Code im Repository `vompa/aispa-studio`
**Stand:** 06.11.2026, Analyse auf `main` (b5cf6e7)
**Ablauf:** Code Guardian nach [docs/arbeitsweise.md](../arbeitsweise.md) Abschnitt 9, gearbeitet auf `dev`, eine Sitzung je Aufgabe im Worktree

---

## 1. Ziel

Die API soll im Cloud-Betrieb mit **mehreren Instanzen** laufen können, ohne dass Hintergrundaufgaben doppelt laufen, Anmeldungen verloren gehen oder Live-Updates nur einen Teil der Clients erreichen.

Das ist **keine Umstellung auf Kubernetes.** [ADR 0016](../entscheidungen/0016-hosting-azure-portabel.md) bleibt gültig: Start auf Azure Container Apps mit Aspire. Die Änderungen hier braucht jeder Betrieb mit mehr als einer Instanz, egal ob Container Apps, AKS oder STACKIT. Kubernetes bleibt eine Option für einen späteren Anbieterwechsel.

**Erfolg ist erreicht, wenn:**

1. `docker compose -f compose.yaml -f compose.scale.yaml up -d --build` startet einen Migrations-Lauf, **zwei API-Instanzen** und **einen Worker**, und alles wird gesund.
2. Jede Hintergrundaufgabe läuft nachweislich nur im Worker (Log-Ausgabe beim Start je Instanz).
3. Ein Neustart aller API-Instanzen meldet die Windows-App nicht ab, und gespeicherte Zahlungs- und Postfachzugänge bleiben lesbar.
4. Eine Änderung im Kalender erreicht das Dashboard auch dann, wenn Änderung und Dashboard an verschiedenen Instanzen hängen.
5. Ohne neue Einstellungen verhält sich alles genau wie heute, vor allem der **Einzelplatz** (`Hosting:Standalone`) und `docker compose up` ohne Zusatzdatei.

## 2. Leitplanken für alle Aufgaben

- `CLAUDE.md` gilt vollständig: englische Namen im Code, Deutsch in Doku, Commits und Logs für den Auftraggeber; Paketversionen nur in `Directory.Packages.props`; Warnungen sind Fehler.
- **Standard bleibt unverändert.** Jede neue Funktion ist über einen Konfigurationsschlüssel zuschaltbar, der Standardwert ergibt das heutige Verhalten. Einzelplatz und Entwicklung dürfen nichts merken.
- **Einfach zuerst.** Keine Abstraktion für nur eine Umsetzung. Wo zwei Wege möglich sind, den einfacheren wählen und im PR-Text begründen.
- Neue Konfigurationsschlüssel dort dokumentieren, wo heute `Hosting:Standalone` und `Database:MigrateOnStartup` beschrieben sind (zuerst suchen, im README oder in `docs/`).
- Jede Aufgabe endet nach Abschnitt 9 „Abschluss einer Aufgabe“: PR-Protokoll, Handtestblock in `docs/umsetzung/handtests.md`, `docs/stand-und-offene-punkte.md` nachziehen.
- Fehler zuerst mit einem Test nachstellen. Neue Regeln mit Tests für den eingeschalteten und den ausgeschalteten Zustand.

## 3. Aufgaben im Überblick

| Nr. | Aufgabe | Modell | Hängt ab von |
|---|---|---|---|
| A | ADR 0087 und Prüfliste | sonnet | – |
| B | Rolle `Api` / `Worker` / `All` | opus | A |
| C | Data-Protection-Schlüssel zentral speichern | opus | A |
| D | Migration als eigener Befehl | sonnet | A |
| E | Forwarded Headers | sonnet | A |
| F | Health-Endpunkte im Betrieb | sonnet | A |
| G | SignalR-Backplane | sonnet | A |
| H | Compose-Profil für mehrere Instanzen und Gesamtprüfung | sonnet | B, C, D, E, F, G |

B und C ändern beide `Program.cs`: **nacheinander**, nicht parallel. D, E, F und G können paarweise parallel laufen (höchstens zwei Sitzungen zugleich), Konflikte in `Program.cs` beim Einmischen von `dev` auflösen.

Die Aufgaben in `.code-guardian-todo.md` mit der nächsten freien T-Nummer anlegen und die Abhängigkeiten als `blockiert (wartet auf T-…)` eintragen.

---

## Aufgabe A: ADR 0087 und Prüfliste

**Ziel:** Die Entscheidung festhalten, bevor Code entsteht.

1. `docs/entscheidungen/0087-mehrinstanz-betrieb.md` nach [vorlage.md](../entscheidungen/vorlage.md), Status „vorgeschlagen“, entschieden vom Auftraggeber.
2. Inhalt:
   - **Kontext:** Cloud-Betrieb nach ADR 0016 braucht mindestens zwei Instanzen für Ausfallsicherheit und Updates ohne Unterbrechung. Heute laufen Hintergrunddienste in jeder Instanz, Data-Protection-Schlüssel liegen im Dateisystem, SignalR hat keinen Backplane, die Migration läuft beim Start jeder Instanz.
   - **Optionen:** (a) bei einer Instanz bleiben, (b) mehrinstanzfähig machen und auf Container Apps bleiben (gewählt), (c) sofort auf Kubernetes. Bei (c) begründen, warum nicht: geringe Last (Day-Spas bis 50 Mitarbeitende, 10 bis 100 Kunden), wenige Bausteine, Betrieb durch eine Person, Grundkosten des Clusters.
   - **Entscheidung:** die Punkte B bis G mit ihren Konfigurationsschlüsseln.
   - **Folgen:** Kubernetes bleibt Option für einen Wechsel zu STACKIT oder Hetzner oder für Installationen beim Kunden. Kartenterminal, Signaturkarte und lokale KI (Ollama) bleiben beim Einzelplatz.
3. Eintrag in `docs/entscheidungen/README.md`, nächste freie Nummer in CLAUDE.md auf 0088 setzen.
4. `docs/konzepte/mehrinstanz-betrieb.md` mit der Prüfliste aus Abschnitt 5 dieser Anweisung und einer kurzen Übersicht der Instanzen (Migration, API, Worker, Web).

**Geprüft wenn:** `web/src/help/textRules.test.ts` und die Textregeln aus `docs/gestaltung/texte.md` eingehalten sind (Prüfsatz ins PR-Protokoll).

---

## Aufgabe B: Rolle `Api` / `Worker` / `All`

**Problem:** Diese Dienste laufen heute in jeder Instanz der API. Mit zwei Instanzen laufen sie doppelt:

| Dienst | Registriert in | Risiko bei doppeltem Lauf |
|---|---|---|
| `PaymentRecovery` | `Online/OnlineSetup.cs` | Zahlungen, hoch |
| `UnsecuredBookingRelease` | `Online/OnlineSetup.cs` | Buchungen doppelt freigegeben |
| `SavedCardCleanup` | `Online/OnlineSetup.cs` | gering |
| `MailboxPoller` | `MailIntake/MailIntakeSetup.cs` | Mails doppelt verarbeitet, doppelte KI-Kosten |
| `RebookScheduler` | `Retention/RetentionEndpoints.cs` | doppelte Claude-Aufrufe |
| `ReminderScheduler` | `Reminders/ReminderEndpoints.cs` | durch Unique-Index geschützt, trotzdem doppelte Arbeit |
| `AccessExpiryScheduler` | `Access/AccessExpiry.cs` | gering |
| `HistoryCleanup` | `Sync/SyncWorker.cs` | gering |
| `HrFileCleanup` | `Program.cs` | gering |

**Lösung:**

1. Neuer Schlüssel `Hosting:Role` mit den Werten `All` (Standard, heutiges Verhalten), `Api` und `Worker`. Ablegen bei `ModuleSwitches` in `System/Modules.cs` oder als eigener kleiner Typ daneben, je nachdem, was weniger Code braucht.
2. In der Rolle `Api` werden die Dienste oben **nicht registriert**. Am einfachsten über eine Hilfsmethode, die `AddHostedService` nur bei `Role is All or Worker` aufruft. Jede Registrierungsstelle der Tabelle darauf umstellen.
3. In der Rolle `Worker` werden **keine Endpunkte gemappt** außer den Health-Endpunkten (Aufgabe F). Der Worker nimmt keinen Benutzerverkehr an.
4. `SyncWorker`, `DemoSeeder` und `DatabaseMigrator` bleiben wie sie sind; sie hängen schon an eigenen Schaltern.
5. Beim Start eine Log-Zeile auf Deutsch mit Rolle und Liste der gestarteten Hintergrunddienste.
6. **Wolverine** bleibt in allen Rollen aktiv. Vorher in der verwendeten Wolverine-Version nachlesen, wie die Durability mit mehreren Knoten arbeitet (Leader für geplante Nachrichten, Wiederaufnahme verwaister Nachrichten), und das Ergebnis im PR-Text und im ADR-Nachtrag festhalten. Nur wenn dabei ein echtes Problem auffällt, die Verarbeitung der lokalen Queues auf den Worker beschränken.
7. `Hosting:Standalone` erzwingt `All`; eine andere Rolle zusammen mit Standalone beim Start mit deutscher Meldung ablehnen.

**Tests:**
- Je Rolle prüfen, welche `IHostedService` registriert sind (Rolle `All` ergibt genau die heutige Liste).
- Rolle `Worker` antwortet auf `/api/v1/…` mit 404, auf `/health` mit 200.
- Standalone mit Rolle `Api` bricht mit der Meldung ab.

---

## Aufgabe C: Data-Protection-Schlüssel zentral speichern

**Problem:** Die Schlüssel liegen heute im Dateisystem (in Docker im Volume `api-keys`). Daran hängen die Anmelde-Cookies und, wichtiger, die verschlüsselten Geheimnisse in `Online/PaymentAccounts.cs` (`PaymentSecrets`) und `MailIntake/IntakeSources.cs`. Hat jede Instanz eigene Schlüssel, kann eine Instanz die Daten der anderen nicht lesen. Gehen die Schlüssel verloren, sind diese Geheimnisse nicht mehr zu entschlüsseln.

**Lösung:**

1. Neuer Schlüssel `DataProtection:Store` mit `FileSystem` (Standard, heutiges Verhalten) und `Database`.
2. Bei `Database`: `PersistKeysToDbContext<…>()` mit einer eigenen Tabelle. **Falle:** Neue Entities in `AispaDbContext` sind automatisch mandantengetrennt, Data-Protection-Schlüssel gehören aber keinem Mandanten. Deshalb einen eigenen kleinen Kontext (z. B. `KeyStoreDbContext`, Schema `system`) mit eigener Migration in derselben Datenbank. Den Migrator (`Data/DatabaseMigrator.cs`) und Aufgabe D um diesen Kontext ergänzen.
3. Immer `SetApplicationName("AispaStudio")` setzen, auch bei `FileSystem`. Vorher prüfen, ob sich dadurch der Schlüsselzweck bestehender Installationen ändert (Einzelplatz, Docker-Volume). Falls ja, den Namen nur bei `Database` setzen und das im PR-Text begründen.
4. **Übernahme bestehender Schlüssel:** Einmalig beim ersten Start mit `Database` die XML-Schlüssel aus dem bisherigen Ordner übernehmen, wenn der Ordner existiert und die Tabelle leer ist. Ohne diese Übernahme wären vorhandene Zahlungs- und Postfachgeheimnisse verloren.
5. Schutz der Schlüssel in der Datenbank (`ProtectKeysWith…`, z. B. Azure Key Vault) **bewusst offen lassen** und im ADR als nächsten Schritt nennen.

**Tests:**
- Zwei `WebApplicationFactory`-Instanzen mit `Database` gegen dieselbe Test-Datenbank: was Instanz 1 mit `PaymentSecrets` schützt, liest Instanz 2.
- Übernahme aus einem Ordner mit vorhandenem Schlüssel, danach lässt sich ein vorher geschützter Wert lesen.
- `FileSystem` verhält sich wie bisher (bestehende Tests bleiben grün).

---

## Aufgabe D: Migration als eigener Befehl

**Problem:** Mit `Database:MigrateOnStartup` migriert jede Instanz beim Start, mehrere zugleich können sich stören.

**Lösung:**

1. Aufruf `dotnet AispaStudio.Api.dll migrate`: baut die Anwendung, führt **dieselben Schritte wie `DatabaseMigrator`** aus (beide Kontexte, Kontext aus Aufgabe C sobald vorhanden, `ChangeLog.AttachAsync`) und beendet sich mit Exitcode 0 oder 1. Die Schritte in eine gemeinsame Methode ziehen, die `DatabaseMigrator` und der Befehl nutzen.
2. Im Befehl den Host nicht starten (`app.Build()`, dann Scope, dann Migration, kein `RunAsync`). So starten weder Wolverine noch Hintergrunddienste.
3. Die Datenbank `aispadb` muss existieren (siehe Bekannte Fallen); fehlt sie, mit deutscher Meldung abbrechen statt mit der Npgsql-Ausnahme.
4. `Database:MigrateOnStartup` bleibt für Einzelplatz, Aspire und das einfache `compose.yaml` unverändert.

**Tests:** Befehl gegen eine leere Test-Datenbank: Exitcode 0, alle Migrationen angewendet, Änderungsprotokoll angehängt. Zweiter Aufruf ändert nichts und endet mit 0.

---

## Aufgabe E: Forwarded Headers

**Problem:** Hinter einem Ingress oder Load Balancer sieht die API `http` und die IP des Proxys. Das betrifft die Passkey-Prüfung (Origin), erzeugte Links und das IP-basierte Rate Limiting in `Online/OnlineSetup.cs`. Heute gibt es kein `UseForwardedHeaders`.

**Lösung:**

1. Neuer Abschnitt `Hosting:ForwardedHeaders` mit `Enabled` (Standard `false`) und optional `KnownNetworks` / `KnownProxies`.
2. Bei `Enabled`: `X-Forwarded-For` und `X-Forwarded-Proto` auswerten, `UseForwardedHeaders()` als **erste** Middleware vor `UseExceptionHandler`.
3. Prüfen, woher die Passkey-Anmeldung ihren Origin nimmt (`AddPasskeyIdentity`), und ob sie hinter dem Proxy mit `https` arbeitet. Ergebnis im PR-Text.
4. Die nginx-Konfiguration in `docker/nginx.conf` setzt die Header für `/api` und `/hubs` bereits oder bekommt sie ergänzt (`X-Forwarded-For`, `X-Forwarded-Proto`, `Host`).

**Tests:** Anfrage mit `X-Forwarded-Proto: https` und `X-Forwarded-For` bei eingeschaltetem Schalter ergibt `Request.IsHttps` und die richtige Client-IP, bei ausgeschaltetem nicht.

---

## Aufgabe F: Health-Endpunkte im Betrieb

**Problem:** `MapDefaultEndpoints` in `AispaStudio.ServiceDefaults` mappt `/health` und `/alive` nur in der Entwicklung. Container Apps und Kubernetes brauchen sie im Betrieb für Start-, Bereitschafts- und Lebendprüfung.

**Lösung:**

1. Neuer Schlüssel `Hosting:HealthEndpoints` (Standard `false` außerhalb der Entwicklung, in der Entwicklung wie heute immer an).
2. Außerhalb der Entwicklung antworten die Endpunkte nur mit dem Status (`Healthy`, `Degraded`, `Unhealthy`), **ohne** den ausführlichen Text von `WriteReportAsync`, damit keine Interna nach außen gehen.
3. `StartupHealthCheck` bleibt die Bereitschaftsprüfung: Solange Migration oder Beispieldaten laufen, ist die Instanz nicht bereit.
4. `docker/nginx.conf` leitet `/health` und `/alive` **nicht** nach außen weiter (prüfen und, falls nötig, mit einem Test oder Kommentar absichern).

**Tests:** Produktionsumgebung mit Schalter: `/health` gibt 200 mit kurzem Text, ohne Schalter 404. Entwicklung unverändert.

---

## Aufgabe G: SignalR-Backplane

**Problem:** Der `LiveHub` (`Live/`) erreicht nur die Clients an derselben Instanz. Das Dashboard im Pausenraum würde Änderungen verpassen, die über eine andere Instanz kommen.

**Lösung:**

1. Neuer Schlüssel `Live:Backplane` mit `None` (Standard) und `Redis`, dazu `ConnectionStrings:redis`.
2. Bei `Redis`: `AddStackExchangeRedis(...)` am SignalR-Builder, Kanalpräfix `aispa`.
3. **Begründung für Redis statt Azure SignalR Service** in den ADR: lokal und in Compose testbar, bei jedem Anbieter verfügbar (ADR 0016 verlangt Portabilität), keine Änderung an der Anmeldung der Anzeigegeräte (`AllowChannel(AccessChannel.Display)`). Azure SignalR Service bleibt als spätere Option genannt.
4. Prüfen, ob alle Clients (Windows-App, Dashboard, Handy-Apps) WebSockets nutzen. Falls ein Client auf Long Polling zurückfallen kann, im ADR festhalten, dass der Ingress Sticky Sessions braucht.
5. Aspire-AppHost: Redis nur hinzufügen, wenn ein Schalter gesetzt ist (wie bei `LocalAi:Enabled`), damit die normale Entwicklung nicht schwerer wird.

**Tests:** Zwei Test-Hosts mit gemeinsamem Redis (Testcontainers): Eine Nachricht, die über Host 1 ausgelöst wird, kommt bei einem Client an Host 2 an. Ohne Backplane bleibt das Verhalten wie heute.

---

## Aufgabe H: Compose-Profil für mehrere Instanzen und Gesamtprüfung

**Ziel:** Alles zusammen lokal so betreiben, wie es später in der Cloud läuft.

1. Neue Datei `compose.scale.yaml` als Ergänzung zu `compose.yaml`:
   - `redis` (Image über `mirror.gcr.io`, siehe Bekannte Fallen)
   - `migrate`: gleiches Image wie `api`, Befehl `migrate`, `restart: "no"`
   - `api`: `Hosting:Role=Api`, `deploy.replicas: 2`, `Database:MigrateOnStartup=false`, `DataProtection:Store=Database`, `Live:Backplane=Redis`, `Hosting:ForwardedHeaders:Enabled=true`, `Hosting:HealthEndpoints=true`, wartet auf `migrate` mit `service_completed_successfully`; der feste Host-Port entfällt (bei zwei Replicas nicht möglich), Zugriff läuft über `web`
   - `worker`: gleiches Image, `Hosting:Role=Worker`, sonst gleiche Einstellungen, ein Replica
   - Das Volume `api-keys` wird in diesem Profil nicht mehr gebraucht.
2. Prüfen, dass nginx Anfragen an `api:8080` auf **beide** Instanzen verteilt (Docker-DNS liefert mehrere Adressen; nginx löst sie beim Start auf). Falls nicht, einen `upstream`-Block oder `resolver` ergänzen und begründen.
3. Kopfkommentar in `compose.scale.yaml` wie in `compose.yaml`: Zweck, Aufruf, „nie öffentlich betreiben“.
4. Handtestblock in `docs/umsetzung/handtests.md` mit den Erfolgskriterien aus Abschnitt 1, je Kriterium mit „Expected:“. Insbesondere:
   - Logs von `api` (beide) und `worker`: Hintergrunddienste nur im Worker
   - Windows-App anmelden, `docker compose … restart api`, Windows-App bleibt angemeldet
   - Dashboard öffnen, mehrere Kalenderänderungen über die Windows-App, jede kommt an
   - eine Instanz stoppen, Windows-App und Online-Buchung arbeiten weiter
5. Danach Rundenabschluss nach Abschnitt 9 mit voller Prüfkette.

---

## 4. Bewusst nicht in dieser Runde

- **Kubernetes-Manifeste, Helm-Chart, Bicep oder `azd`-Deployment.** Erst wenn ADR 0087 angenommen ist und ein Produktivbetrieb ansteht.
- **Schutz der Data-Protection-Schlüssel mit Key Vault** und Secrets aus dem Key Vault. Folgt mit dem echten Deployment.
- **Verteiltes Rate Limiting.** Das Limit gilt vorerst je Instanz, also effektiv mal Anzahl Instanzen. Später am Ingress oder Gateway begrenzen; im ADR festhalten.
- **Caches** `TenantLocks` (30 Sekunden) und `OpeningCache` (5 Minuten, nur Vorabprüfung, der Raumplaner liest frisch): laut Code und Kommentar unkritisch bei mehreren Instanzen, keine Änderung.
- **nginx als Nicht-Root-Image** (`nginx-unprivileged`). Erst nötig, wenn ein Cluster die Pod Security Standards „restricted“ erzwingt.
- Kartenterminal (`LocalCardTerminal`), Signaturkarte und lokale KI bleiben beim Einzelplatz.

## 5. Prüfliste für den Mehrinstanz-Betrieb

Diese Liste kommt in `docs/konzepte/mehrinstanz-betrieb.md` und wird vor jedem neuen Hintergrunddienst oder Zwischenspeicher geprüft:

- [ ] Neuer `BackgroundService` oder Timer? Nur in den Rollen `All` und `Worker` registrieren.
- [ ] Neuer Zwischenspeicher im Speicher? Klären, was eine zweite Instanz mit veraltetem Stand anrichten kann, und das im Kommentar festhalten (wie bei `OpeningCache`).
- [ ] Neue Live-Nachricht? Geht über den `LiveHub`, damit der Backplane greift.
- [ ] Neue verschlüsselte Daten? Data Protection oder `HealthDataProtector`, nie eigene Schlüssel im Dateisystem.
- [ ] Neue Dateien? Nicht ins lokale Dateisystem der Instanz, sondern in die Datenbank oder einen gemeinsamen Speicher.
- [ ] Neue Migration? Läuft auch über `migrate`, nicht nur über den Start.
- [ ] Neuer Endpunkt, der Client-IP oder Schema auswertet? Funktioniert hinter dem Proxy (Forwarded Headers).
