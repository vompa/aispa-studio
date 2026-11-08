# Mehrinstanz-Betrieb: Instanzen und Prüfliste

Stand 07.11.2026. Die Entscheidung steht in [ADR 0087](../entscheidungen/0087-mehrinstanz-betrieb.md) (vorgeschlagen), die Umsetzung in der [Arbeitsanweisung](../umsetzung/arbeitsanweisung-mehrinstanz-betrieb.md) (Aufgaben T-041 bis T-047). Ohne neue Einstellung läuft alles in einer Instanz wie bisher; der Einzelplatz (`Hosting:Standalone`) bleibt immer bei einer Instanz.

## Instanzen im Mehrinstanz-Betrieb

| Instanz | Anzahl | Aufgabe | Einstellungen |
| --- | --- | --- | --- |
| Migration | einmal je Update, endet danach | spielt die Migrationen ein (`dotnet AispaStudio.Api.dll migrate`), Exitcode 0 oder 1 | gleiche Datenbank wie die API |
| API | zwei oder mehr | nimmt Anfragen und Live-Verbindungen an, ohne Hintergrunddienste; stellt Aufträge der Terminannahme und „Jetzt prüfen“ nur in die Mail-Queue in der Datenbank ein | `Hosting:Role=Api`, `Database:MigrateOnStartup=false`, `DataProtection:Store=Database`, `Live:Backplane=Redis`, `Hosting:ForwardedHeaders:Enabled=true`, `Hosting:HealthEndpoints=true` |
| Worker | genau eine | führt die Hintergrunddienste aus (Zahlungsabgleich, Postfachabruf, Erinnerungen, Kalender-Abos, Aufräumen) und arbeitet die Mail-Queue eine Nachricht nach der anderen ab; ist Leader des Wolverine-Clusters, nimmt keinen Benutzerverkehr an; beim Austausch erst den alten beenden, dann den neuen starten | wie die API, aber `Hosting:Role=Worker` |
| Web (nginx) | eine oder mehr | liefert die Weboberfläche und leitet `/api` und `/hubs` an die API-Instanzen weiter, `/health` und `/alive` nicht nach außen | – |
| Redis | eine | Backplane für SignalR, damit eine Live-Nachricht die Clients an allen API-Instanzen erreicht, auch die aus dem Worker; nicht öffentlich, mit Passwort | `ConnectionStrings:redis` bei API und Worker, Ingress mit Sticky Sessions |

Die gemeinsame Datenbank trägt neben den Daten der Spas auch den Schlüsselring für Data Protection (eigener Kontext, Schema `system`) und die Mail-Queue (Schema `wolverine_queues`, legt Wolverine beim Start an). Keine Instanz läuft dabei mit der Rolle `All`.

## Prüfliste

Vor jedem neuen Hintergrunddienst, Zwischenspeicher oder Endpunkt prüfen:

- [ ] Neuer `BackgroundService` oder Timer? Nur in den Rollen `All` und `Worker` registrieren.
- [ ] Neuer Zwischenspeicher im Speicher? Klären, was eine zweite Instanz mit veraltetem Stand anrichten kann, und das im Kommentar festhalten (wie bei `OpeningCache`).
- [ ] Neue Live-Nachricht? Geht über den `LiveHub`, damit der Backplane greift, und senden Handler über `LiveDelivery.SendAsync`, damit ein Redis-Ausfall nur eine Warnung ergibt und keine Wiederholung.
- [ ] Neue verschlüsselte Daten? Data Protection oder `HealthDataProtector`, nie eigene Schlüssel im Dateisystem.
- [ ] Neue Dateien? Nicht ins lokale Dateisystem der Instanz, sondern in die Datenbank oder einen gemeinsamen Speicher.
- [ ] Neue Migration? Läuft auch über `migrate`, nicht nur über den Start.
- [ ] Neue Nachricht, deren Reihenfolge zählt oder die nur ein Knoten verarbeiten darf? In `QueuedMessageTypes` (`MailIntake/MailIntakeSetup.cs`) eintragen, nicht an eine lokale Queue hängen; lokale Queues laufen auf jeder Instanz. Die Liste steuert das Routing in jeder Rolle und die Fehlerregel `MailIntakeFailurePolicy`.
- [ ] Neuer Endpunkt, der Client-IP oder Schema auswertet? Funktioniert hinter dem Proxy (Forwarded Headers).
