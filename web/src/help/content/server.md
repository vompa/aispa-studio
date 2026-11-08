---
art: einstellung
bereich: Betrieb
oberflaeche: verwaltung
rollen: inhaber, manager
---
# Server-Einstellungen

Diese Seite ist für die Person, die den AISPA-Server betreibt: einen eigenen Server mit Docker oder eine andere Installation der API. Sie beschreibt jede Einstellung, die der Server beim Start liest. Die Windows-App setzt beim Einzelplatz die nötigen Werte selbst; dort braucht man diese Seite nur zum Nachschlagen oder für Sonderfälle.

Alles, was das Spa selbst in der Oberfläche einstellt (Standorte, Online-Regeln je Standort, Zahlungskonto, eigene Postfächer), steht in den anderen Hilfethemen. Hier geht es nur um Einstellungen des Servers.

## Werte setzen {#werte-setzen}

Jede Einstellung hat einen Schlüssel aus Abschnitt und Name, getrennt durch Doppelpunkte, zum Beispiel `Mail:Smtp:Host`. Groß- und Kleinschreibung spielt beim Schlüssel keine Rolle. Ein Wert lässt sich auf mehreren Wegen setzen; gibt es ihn mehrfach, gilt der spätere in dieser Reihenfolge:

1. `appsettings.json` neben dem Programm, für alle Umgebungen.
2. `appsettings.Development.json` oder `appsettings.Production.json`, je nach Umgebung (`ASPNETCORE_ENVIRONMENT`).
3. User-Secrets, nur in der Entwicklungsumgebung.
4. Umgebungsvariablen, zum Beispiel in Docker oder im Windows-Dienst.
5. Befehlszeilenargumente.

Nach jeder Änderung den Server neu starten. Er liest die Werte nur beim Start.

### Als Umgebungsvariable {#umgebungsvariablen}

In Umgebungsvariablen steht statt jedes Doppelpunkts ein doppelter Unterstrich. Aus `Mail:Smtp:Host` wird `Mail__Smtp__Host`, aus `OnlineVouchers:Stripe:Connect` wird `OnlineVouchers__Stripe__Connect`. In Docker Compose steht das unter `environment:`, zum Beispiel `Mail__Smtp__Host: smtp.example.at`.

### In der Entwicklung mit User-Secrets {#user-secrets}

Auf einem Entwicklungsrechner gehören Schlüssel in die User-Secrets. Sie liegen im Benutzerprofil, nicht im Projektordner:

- `dotnet user-secrets set "Ai:Claude:ApiKey" "<schlüssel>" --project src/AispaStudio.Api`
- `dotnet user-secrets list --project src/AispaStudio.Api` zeigt, was gesetzt ist.

User-Secrets gelten nur, wenn die Umgebung `Development` ist.

### Listen {#listen}

Einstellungen mit mehreren Werten (zum Beispiel `OnlineVouchers:SuggestedAmounts`) bekommen einen Index: `OnlineVouchers:SuggestedAmounts:0`, `OnlineVouchers:SuggestedAmounts:1` und so weiter, als Umgebungsvariable `OnlineVouchers__SuggestedAmounts__0`. In `appsettings.json` schreibt man eine normale JSON-Liste.

### Geheimnisse {#geheimnisse}

API-Schlüssel, Passwörter, Verbindungszeichenfolgen mit Passwort und die Schlüssel für Gesundheitsdaten gehören nie in eine Datei im Repository, auch nicht in `appsettings.json`. Sie kommen in einen Geheimnisspeicher, in Umgebungsvariablen (zum Beispiel aus einer Datei `.env`, die nicht eingecheckt wird) oder in der Entwicklung in die User-Secrets. Die Beispielwerte in `appsettings.Development.json` sind nur für die Entwicklung und dürfen nie im Echtbetrieb gelten.

## Grundbetrieb {#grundbetrieb}

### Umgebung {#umgebung}

`ASPNETCORE_ENVIRONMENT` (nur als Umgebungsvariable): `Development` oder `Production`. Ohne Angabe gilt `Production`. Nur in `Development` gelten `appsettings.Development.json` und die User-Secrets, gibt es die Anmeldung mit Testkonten ohne Passkey, das Test-Postfach der Terminannahme, die Health-Endpunkte unter `/health` und `/alive` (im Betrieb nur mit `Hosting:HealthEndpoints`) und das OpenAPI-Dokument, und Mails und Kurznachrichten ohne eingerichteten Versand landen im Log. Ein öffentlich erreichbarer Server läuft immer mit `Production`.

`ASPNETCORE_URLS` (Umgebungsvariable): Adresse und Port, an denen die API lauscht. Das Docker-Image setzt `http://+:8080`. Davor gehört ein Webserver mit HTTPS (zum Beispiel nginx), der auch die Web-Oberfläche ausliefert.

### Datenbank {#datenbank}

- `ConnectionStrings:aispadb`: Verbindung zur PostgreSQL-Datenbank, zum Beispiel `Host=db;Database=aispadb;Username=aispa;Password=<passwort>`. Pflicht, ohne sie startet der Server nicht. Die Datenbank `aispadb` muss vor dem ersten Start existieren. Enthält ein Passwort, also nur als Umgebungsvariable oder aus dem Geheimnisspeicher. Unter Aspire setzt der AppHost sie selbst.
- `Database:MigrateOnStartup`: `true` oder `false`, Standard `false`. Mit `true` bringt der Server beim Start die Datenbank auf den neuesten Stand (beide Datenbankbereiche, mit `DataProtection:Store` = `Database` auch den Schlüsselring, und das Änderungsprotokoll für den Abgleich). Ohne diese Einstellung migriert man mit dem Befehl `migrate` (siehe unten). Für einen einzelnen Server ist `true` der einfachste Weg; die Windows-App setzt es immer. Laufen mehrere Instanzen, migriert besser ein eigener Lauf mit `migrate` je Update, und die Instanzen laufen ohne diese Einstellung.
- Befehl `migrate`: `dotnet AispaStudio.Api.dll migrate` bringt die Datenbank auf den neuesten Stand und beendet sich dann, ohne den Server zu starten (beide Datenbankbereiche, mit `DataProtection:Store` = `Database` auch den Schlüsselring samt Übernahme aus `DataProtection:ImportFolder`, und das Änderungsprotokoll für den Abgleich). Der Befehl nimmt dieselbe Konfiguration wie der Server, mindestens `ConnectionStrings:aispadb`. Im Docker-Image hängt `docker compose run --rm api migrate` bzw. `docker run … <image> migrate` das Wort an den Start der API an. Der Exitcode ist 0 bei Erfolg und 1, wenn die Migration scheitert; ein Konfigurationsfehler vor dem Start (zum Beispiel keine Verbindung oder eine ungültige Rolle) endet mit einem anderen Code ungleich 0. Eine Sperre in der Datenbank lässt gleichzeitige Läufe nacheinander ablaufen, auch den Befehl neben einer Instanz mit `Database:MigrateOnStartup`. Ein Lauf, der auf einen anderen warten muss, meldet „Warte auf die Migrationssperre“ und wartet, bis dieser fertig ist, ohne Zeitlimit; er lässt sich mit Strg+C abbrechen. Trotzdem gehört je Update ein Lauf. Die Datenbank `aispadb` muss vorher existieren: der Befehl legt sie nicht an, sondern bricht mit einer Meldung ab, damit ein Tippfehler im Namen keine zweite, leere Datenbank erzeugt. Der Befehl lässt sich beliebig oft wiederholen. Das Schema von Wolverine legt jede Instanz weiter selbst beim Start an. Beim ersten Lauf schreibt Entity Framework Zeilen mit „fail:“ ins Protokoll, solange die Verlaufstabelle noch fehlt; sie sind kein Fehler, der Lauf endet trotzdem mit 0.
- `Database:SeedDemoData`: `true` oder `false`, Standard `false`. Legt beim Start in einer leeren Datenbank das Beispiel-Spa mit Testdaten an. Der erste Start dauert dadurch rund eine halbe Minute; das Einspielen läuft im Hintergrund, `GET /api/v1/system/status` meldet den Stand (`startup`). Wurde es abgebrochen, meldet der Server „Beispieldaten unvollständig“ und löscht nichts; dann die Datenbank leeren. Nur für Entwicklung und Vorführung, nie im Echtbetrieb.
- `DemoData:WeeksBack`: Wochen Vergangenheit für die Beispieldaten, Standard `8`. Gilt nur mit `Database:SeedDemoData`.
- `DemoData:WeeksAhead`: Wochen in die Zukunft für die Beispieldaten, Standard `4`. Gilt nur mit `Database:SeedDemoData`.
- `DemoData:Mode`: `Bulk` (Standard) erzeugt den Verlauf der Beispieldaten im Speicher und spielt ihn mit einem `COPY` je Tabelle ein (rund 20 Sekunden); `Services` bucht und kassiert jeden Schritt über die Dienste wie im Betrieb (rund 12 Minuten, nur zum Vergleichen). Gilt nur mit `Database:SeedDemoData`.

### Protokoll und Überwachung {#protokoll}

- `Logging:LogLevel:Default`: wie viel ins Protokoll geschrieben wird, Standard `Information`. Erlaubt sind `Trace`, `Debug`, `Information`, `Warning`, `Error`, `Critical` und `None`. Einzelne Bereiche lassen sich eigens einstellen, zum Beispiel `Logging:LogLevel:Microsoft.AspNetCore` (Standard `Warning`).
- `OTEL_EXPORTER_OTLP_ENDPOINT` (Umgebungsvariable): Adresse eines OpenTelemetry-Sammlers. Ist sie gesetzt, schickt der Server Protokoll, Messwerte und Ablaufspuren dorthin. Ohne Angabe nicht.
- `AllowedHosts`: Hostnamen, unter denen der Server antwortet, getrennt durch Strichpunkt. Standard `*` (alle). Hinter einem Webserver, der den Host prüft, reicht das.

## Betriebsart und Module {#betriebsart}

- `Hosting:Standalone`: `true` oder `false`, Standard `false`. `true` ist der Einzelplatz-Betrieb der Windows-App: Anmeldung mit Benutzername und Passwort, Einrichtung beim ersten Start, alle Webmodule aus. `false` ist der Server-Betrieb: Anmeldung mit Passkey, Einladungen per Link, alle Webmodule an.
- `Hosting:Role`: `All`, `Api` oder `Worker`, Standard `All`. Nur für den Server-Betrieb mit mehreren Instanzen. `All` beantwortet Anfragen und führt die Hintergrundläufe aus (Zahlungsabgleich, Postfachabruf, Erinnerungen, Aufräumen), wie bisher. `Api` beantwortet Anfragen und startet diese Hintergrundläufe nicht; die Mail-Queue mit ihren KI-Aufrufen und **Jetzt prüfen** im Postfach gibt sie an den Worker weiter (siehe [Mail-Queue mit mehreren Instanzen](#mail-queue)). `Worker` führt nur die Hintergrundläufe aus und beantwortet außer den Health-Endpunkten (im Betrieb mit `Hosting:HealthEndpoints`) nichts; davon läuft genau eine Instanz. Im Einzelplatz gilt immer `All`, eine andere Rolle zusammen mit `Hosting:Standalone` bricht den Start mit einer Meldung ab. Die Rolle nimmt die drei Namen ohne Rücksicht auf Groß- und Kleinschreibung an. Beim Start steht die Rolle mit den zuständigen Hintergrunddiensten im Protokoll; ein Dienst, den sein eigener Schalter abschaltet (etwa `Reminders:PollSeconds=0`), steht trotzdem in der Liste.
- `Hosting:HealthEndpoints`: `true` oder `false`, Standard `false`; leer gilt als `false`, jeder andere Wert (zum Beispiel `1` oder `yes`) bricht den Start ab. Ohne Angabe gibt es `/health` und `/alive` nur in `Development`. Mit `true` antwortet jede Instanz in jeder Umgebung, auch die mit `Hosting:Role=Worker`, für die Proben von Container Apps, Kubernetes oder einem Load Balancer. `/alive` ist die Lebendprüfung: sie antwortet mit 200, solange der Prozess Anfragen annimmt. `/health` ist die Bereitschaftsprüfung: sie antwortet mit 200 und `Healthy`, wenn die Instanz Anfragen bedienen kann, und mit 503 und `Unhealthy`, solange das Beispiel-Spa eingespielt wird, solange die Datenbank nicht erreichbar ist und nach einem gescheiterten Start. Mit `Database:MigrateOnStartup` wartet der Start die Migration ab, bevor der Server lauscht; in dieser Zeit antwortet die Instanz gar nicht, auch `/alive` nicht. Im Betrieb deshalb den Befehl `migrate` nutzen oder der Startprobe genug Zeit geben. Beide melden nur den Status als Text (`Healthy`, `Degraded` oder `Unhealthy`), nie Namen oder Beschreibungen der Prüfungen. Die Endpunkte haben keine Anmeldung und kein Rate Limit, und jeder Abruf von `/health` fragt die Datenbank zweimal ab: Port `8080` der Instanz darf nicht öffentlich erreichbar sein, die Proben nicht zu oft laufen. Die Proben kommen aus dem internen Netz der Plattform direkt an die Instanz, nicht über nginx; öffentlich geht es nur über nginx oder den Ingress, der `/health` und `/alive` nicht weitergibt (nginx antwortet dort mit 404). Ist `AllowedHosts` nicht `*`, müssen die Proben einen erlaubten Host senden (Kubernetes: `httpGet.httpHeaders` mit `Host`), sonst antworten beide mit 400. Eine nicht bereite Instanz schreibt je Probe und je nicht bestandener Prüfung eine Zeile mit der Stufe `Error` von `DefaultHealthCheckService` ins Protokoll, ohne Datenbank mindestens zwei; die Stufe lässt sich mit `Logging:LogLevel:Microsoft.Extensions.Diagnostics.HealthChecks.DefaultHealthCheckService` anpassen. Im Einzelplatz bricht `Hosting:Standalone` zusammen mit `Hosting:HealthEndpoints` den Start mit einer Meldung ab. Im Docker-Image fehlen `curl` und `wget`; ein `HEALTHCHECK` im Image braucht ein eigenes Werkzeug.
- `Live:Backplane`: `None` oder `Redis`, Standard `None`; leer gilt als `None`, die Schreibweise spielt keine Rolle, jeder andere Wert (zum Beispiel `1` oder `true`) bricht den Start ab. Ohne Backplane erreicht eine Live-Meldung (Termine im Kalender, Tafel im Pausenraum, Posteingang, Betrieb) nur die Clients, die an derselben Instanz hängen; mit einer Instanz genügt das. Mit `Redis` verteilt Redis jede Meldung an alle Instanzen, auch die des Workers (Postfach-Abruf, Freigabe ungesicherter Buchungen). `ConnectionStrings:redis` ist dann Pflicht, zum Beispiel `redis-host:6379,password=…`; fehlt sie oder ist sie ungültig, bricht der Start ab, ebenso zusammen mit `Hosting:Standalone`. Alle Instanzen einschließlich des Workers brauchen denselben Redis. Beim Start steht eine Zeile im Protokoll, die Verbindungszeichenfolge nie. Einzelheiten unter [Live-Meldungen mit mehreren Instanzen](#live-backplane).
- `Modules:OnlineBooking`: Online-Buchung und Kundenbereich.
- `Modules:BookingIntake`: KI-Terminannahme über Mail, SMS, WhatsApp und Anrufe.
- `Modules:Assistant`: Assistent in der Oberfläche.
- `Modules:Reminders`: Terminerinnerungen.
- `Modules:Rebooking`: Wiederbuchungs-Hinweise (Werbe-Mail einige Wochen nach der letzten Behandlung).
- `Modules:Benchmark`: Vergleich mit anderen Spas.

Jedes Modul nimmt `true` oder `false`. Ohne Angabe ist es im Server-Betrieb an und im Einzelplatz aus. Ein ausgeschaltetes Modul antwortet mit „Dieses Modul ist ausgeschaltet.“. Zusätzlich gilt die Abo-Stufe des Spas: Ein eingeschaltetes Modul, das nicht in der Stufe des Spas enthalten ist, bleibt für dieses Spa gesperrt.

### Hinter Proxy oder Ingress {#proxy}

Steht vor der API ein Proxy, ein Load Balancer oder ein Ingress, sieht sie von sich aus nur dessen Adresse und `http`. Die Zahl der Anfragen je Adresse (Online-Buchung, Anmeldung am Gerät) zählt dann alle Kundinnen und Kunden in einem Topf, und `Secure` fehlt an den Cookies. Mit diesem Schalter übernimmt die API Adresse und Schema aus `X-Forwarded-For` und `X-Forwarded-Proto`, aber nur von Proxys, denen sie vertraut.

- `Hosting:ForwardedHeaders:Enabled`: `true` oder `false`, Standard `false`. Ohne Angabe bleibt alles wie bisher. Nur im Server-Betrieb; mit `Hosting:Standalone` bricht der Start ab, denn der Einzelplatz erlaubt Einrichtung und Passwort-Wiederherstellung nur vom eigenen PC, und ein gefälschtes `X-Forwarded-For: 127.0.0.1` würde diese Prüfung aushebeln.
- `Hosting:ForwardedHeaders:KnownNetworks`: Liste der Netze mit Präfixlänge, deren Proxys die API vertraut, zum Beispiel das Netz des Ingress, `10.0.8.0/24`. Das Netz so eng wie möglich wählen; Netze mit einer Präfixlänge unter 8 (zum Beispiel `0.0.0.0/0`) lehnt der Start ab. Mehrere Netze mit `:0`, `:1` oder in einem Text mit Kommas. Die Adresse ohne Hostanteil, dann `/` und die Präfixlänge; ein anderer Eintrag bricht den Start mit einer Meldung ab.
- `Hosting:ForwardedHeaders:KnownProxies`: Liste einzelner Proxy-Adressen, zum Beispiel `10.0.0.5`.
- `Hosting:ForwardedHeaders:ForwardLimit`: Zahl der Proxys, die hintereinander vor der API stehen, ganze Zahl ab 1, Standard `1`. Stehen Ingress und nginx hintereinander, ist es `2`.

Ohne `KnownNetworks` und `KnownProxies` vertraut die API nur Loopback (`127.0.0.1`, `::1`); „allen vertrauen“ gibt es nicht. Kopfzeilen von jedem anderen Absender ignoriert sie. Wer zu große Netze einträgt, vertraut jedem Rechner darin: Dort kann jemand die Adresse eines beliebigen Kunden vortäuschen und so das Rate Limit umgehen. Deshalb nur das Netz des Proxys eintragen, nicht das ganze Firmennetz. Mit `ForwardLimit` 2 gehören die Adresse von nginx **und** das Netz des Ingress hinein.

Beispiel für Docker Compose mit dem nginx aus dem Docker-Image direkt vor der API (ein Proxy): Das Netz bekommt in einer zusätzlichen Compose-Datei ein festes Subnetz und der Dienst `web` (nginx) eine feste Adresse, zum Beispiel `10.200.0.10`. Nur diese Adresse gilt als Proxy:

```
Hosting__ForwardedHeaders__Enabled=true
Hosting__ForwardedHeaders__KnownProxies=10.200.0.10
```

Ein ganzes Docker-Netz wie `172.16.0.0/12` eignet sich nicht: Es enthält das Gateway und jeden Nachbarcontainer. Verbindungen über einen veröffentlichten API-Port kommen im Container von der Gateway-Adresse (zum Beispiel `172.17.0.1`) an; wer die API so an nginx vorbei erreicht, gälte dann als vertrauter Proxy und könnte seine Adresse frei wählen. Mit `Enabled` deshalb den API-Port nicht veröffentlichen oder nur `KnownProxies` mit der festen Adresse von nginx verwenden.

Beispiel für Container Apps oder einen anderen Ingress vor dem nginx (zwei Proxys): `ForwardLimit` auf `2` setzen und in `KnownNetworks` zusätzlich das Netz des Ingress eintragen. Dieses Netz hängt vom Betreiber ab und steht nicht in der Hilfe; es muss aus der Umgebung ermittelt werden.

Das nginx aus `docker/nginx.conf` setzt `X-Forwarded-For` und `X-Forwarded-Proto` für `/api/`, `/hubs/` und die Unteranfrage der Buchungsseite. Ein `X-Forwarded-Proto` übernimmt es nur von einem Ingress davor: dessen Netz steht in `docker/nginx.conf` im Block `geo` (`$from_ingress`). Ohne Eintrag gilt immer das Schema der Verbindung zu nginx, nie der Wert eines Clients.

Was davon abhängt: das Rate Limit je Adresse, `Secure` an den Cookies der Anmeldung (`aispa.auth`, Kunden-Cookies, Gerät, Anzeige), die Adresse der Rückkehr (`redirect_uri`) bei Google und Apple und die Signaturadresse der SMS- und Twilio-Webhooks ohne `Twilio:WebhookUrl`.

Passkeys hängen nicht davon ab: Die Prüfung vergleicht den Ursprung aus den Daten des Browsers mit dem `Origin` der Anfrage. Der Ingress muss deshalb `Host` und `Origin` unverändert durchreichen. Im Betrieb mit mehreren Instanzen `Passkeys:ServerDomain` fest setzen und `Twilio:WebhookUrl` eintragen, denn `X-Forwarded-Host` wertet die API nicht aus und nginx reicht den `Host` des Clients ungeprüft weiter.

Die Umgebungsvariable `ASPNETCORE_FORWARDEDHEADERS_ENABLED` (Schlüssel `ForwardedHeaders_Enabled`) von ASP.NET Core nicht setzen: Sie leert die Listen, und ASP.NET Core vertraut dann jedem Absender. Mit `Hosting:Standalone` bricht der Start ab; im Server-Betrieb steht eine Warnung im Protokoll. Mit `Hosting:ForwardedHeaders:Enabled` gelten trotzdem nur die eingetragenen Netze.

### Live-Meldungen mit mehreren Instanzen {#live-backplane}

Kalender, Tafel im Pausenraum und Posteingang aktualisieren sich über eine Live-Verbindung. Bei mehreren Instanzen hängt jeder Client an einer davon, eine Änderung entsteht aber oft an einer anderen oder im Worker. Mit `Live:Backplane=Redis` gehen alle Meldungen über einen gemeinsamen Redis, und jeder Client bekommt sie, an welcher Instanz er auch hängt.

- **Redis absichern.** Redis nur im internen Netz der Instanzen erreichbar machen, nie öffentlich, und ein Passwort setzen (`password=…` in der Verbindungszeichenfolge; außerhalb des eigenen Hosts zusätzlich `ssl=true`). Die Meldungen tragen Termine mit Gastnamen. Redis speichert sie nicht (Pub/Sub), ein Volume und eine Sicherung braucht er nicht. Wähle das Kennwort ohne Komma: Ein Komma trennt in der Verbindungszeichenfolge die Endpunkte, der Rest des Kennworts würde als Adresse gelesen und könnte in Meldungen und im Protokoll stehen. Die Zeitlimits für den Verbindungsaufbau und für Befehle stehen standardmäßig auf 1 s und 2 s, damit ein Ausfall die Instanz nicht lange blockiert; für einen verwalteten Redis mit TLS (etwa über Zonen hinweg) lassen sie sich in der Verbindungszeichenfolge anheben (`connectTimeout=4000,asyncTimeout=6000`, Millisekunden). Die Kanäle haben das Präfix `aispa`. Zwei Installationen (zum Beispiel Test und Betrieb, oder zwei Kunden) brauchen je einen eigenen Redis oder ein eigenes `channelPrefix=` in der Verbindungszeichenfolge (`redis-host:6379,password=…,channelPrefix=kunde2`); sonst kommen die Meldungen der einen Installation bei den Clients der anderen an.
- **Sticky Sessions am Ingress.** Die Windows-App und die Tafel legen den Transport nicht fest; fällt WebSockets aus (Proxy, Firewall), wechseln sie auf Server-Sent Events oder Long Polling. Dort gehören mehrere Anfragen zu einer Verbindung und müssen dieselbe Instanz erreichen, sonst bricht die Verbindung ab. Der Ingress braucht deshalb Session-Affinität: In Azure Container Apps `stickySessions` mit `affinity: sticky` (Cookie, nur im Einzel-Revisionsmodus und mit Ingress für Web-Anfragen). Zwischen nginx und mehreren API-Instanzen braucht es ebenfalls eine feste Zuordnung für `/hubs/`: `compose.scale.yaml` nutzt dafür `hash $remote_addr;` (siehe [Mehrere Instanzen mit Docker Compose](#compose-scale)). Steht ein Ingress vor nginx, sieht nginx nur dessen Adresse, und alle Clients landen auf einer Instanz; dann muss der Ingress die Zuordnung übernehmen. Die Backplane ersetzt das nicht: Sie verteilt die Meldungen, die Verbindung selbst bleibt an einer Instanz.
- **Fällt Redis aus**, läuft die Instanz weiter und verbindet im Hintergrund neu. Es fehlen nur die Live-Meldungen: Eine Meldung, die in dieser Zeit entsteht, geht verloren und wird nicht wiederholt; Buchungen und alle anderen Anfragen funktionieren. Die Ansicht zeigt die Änderung erst beim nächsten Laden (Seitenwechsel, Neuladen) oder nach einem Neuverbinden der Live-Verbindung, denn die Verbindung zur Instanz bleibt bestehen. Je verlorener Meldung steht eine Warnung im Protokoll („Live-Meldung … nicht zugestellt“), ohne Eintrag in der Fehlerablage. Wer sich in dieser Zeit am Live-Kanal anmeldet (zum Beispiel einen Kalender öffnet), scheitert: bei Long Polling mit einem Serverfehler (500), bei WebSockets schließt die Instanz die Verbindung. Die Anmeldung wird nicht halb gespeichert; der Client meldet sich beim nächsten Versuch erneut an. Ein Aussetzer bis zu zwei Sekunden wird überbrückt; ein Redis, der nur hängt, endet nach zwei Sekunden in derselben Warnung. Redis steht nicht in `/health`: Ein Ausfall nimmt keine Instanz aus der Verteilung.

### Mail-Queue mit mehreren Instanzen {#mail-queue}

Die Terminannahme arbeitet eingehende Mails, Antwortentwürfe, Übersetzungen und den Versand der Antworten eine nach der anderen ab. Mit der Rolle `All` geschieht das in der Instanz selbst. Mit `Api` und `Worker` stehen diese Aufträge in einer gemeinsamen Queue in der Datenbank: Die API-Instanzen stellen sie nur ein, der Worker arbeitet sie in der Reihenfolge des Eingangs ab. Auch **Jetzt prüfen** im Postfach läuft dann im Worker, damit nie zwei Instanzen dasselbe Postfach zugleich abrufen.

- **Keine Kopien mit der Rolle `All`.** Zwei Instanzen mit `All` arbeiten jede für sich: KI-Aufrufe laufen doppelt, und die Reihenfolge der Mails eines Spas ist nicht gesichert. Mehr als eine Instanz nur mit `Api` und `Worker`.
- **Genau ein Worker.** Beim Austausch den alten Worker zuerst beenden, dann den neuen starten. Zwei Worker zugleich ließen die Hintergrundläufe (Zahlungsabgleich, Postfachabruf, Erinnerungen) doppelt laufen.
- **Ohne Worker bleiben die Aufträge liegen.** Die API nimmt Mails und **Senden** weiter an, abgearbeitet wird nichts. Auch geplante Nachrichten und das Zurückholen von Nachrichten einer ausgefallenen API-Instanz laufen dann nicht, denn nur der Worker verteilt diese Arbeit. Startet der Worker wieder, arbeitet er die wartenden Aufträge in der Reihenfolge des Eingangs ab. Ausnahme: Aufträge, die ein abgestürzter Worker schon abgeholt hatte, können nach später eingegangenen kommen.
- **Die Übernahme dauert einige Sekunden.** Ein neu gestarteter Worker übernimmt die Queue nach höchstens rund 25 Sekunden (gemessen: 4 bis 24 s). So lange warten Mails und Antworten.

Scheitert ein Auftrag, versucht der Worker ihn noch dreimal, nach 1, 5 und 15 Sekunden. Die Queue wartet in dieser Zeit, damit die Reihenfolge erhalten bleibt (zum Beispiel Buchung vor Storno). Scheitert auch der letzte Versuch, legt der Worker den Auftrag in die Fehlerablage (Tabelle `wolverine.wolverine_dead_letters`) und schreibt eine Zeile mit der Stufe `Error` ins Protokoll („Mail-Queue: … nach 4 Versuchen in der Fehlerablage.“). Danach geht es mit dem nächsten Auftrag weiter. Ist es der Versand einer Antwort, wird die Antwort wieder Entwurf und trägt im Posteingang den Hinweis „Nicht gesendet: Der Versand ist mehrmals gescheitert. …“. Die Rezeption kann sie dort prüfen und erneut senden. Mit der Rolle `All` gilt weiter die allgemeine Regel: Ein gescheiterter Auftrag wird später noch einmal eingeplant.

Die Queue legt beim ersten Start eigene Tabellen im Schema `wolverine_queues` an. Der Datenbankbenutzer der Instanzen braucht dafür das Recht `CREATE` auf der Datenbank, wie für das Schema `wolverine`.

Vor dem Wechsel von `All` auf `Api` und `Worker` die bisherige Instanz erst beenden, wenn sie keine Mail mehr in Arbeit hat: `SELECT count(*) FROM wolverine.wolverine_incoming_envelopes WHERE received_at LIKE 'local://mail-intake%'` ergibt 0. Sonst laufen diese Aufträge nach dem Wechsel auf einer beliebigen Instanz, auch auf einer API-Instanz, und außerhalb der Reihenfolge der Mail-Queue.

Für den Weg zurück von `Api` und `Worker` auf `All` gilt dasselbe in der anderen Richtung: Den Worker erst beenden, wenn die Queues leer sind. `SELECT (SELECT count(*) FROM wolverine_queues.wolverine_queue_mail_intake) + (SELECT count(*) FROM wolverine_queues.wolverine_queue_mail_intake_scheduled) + (SELECT count(*) FROM wolverine_queues.wolverine_queue_mailbox_check) + (SELECT count(*) FROM wolverine_queues.wolverine_queue_mailbox_check_scheduled) + (SELECT count(*) FROM wolverine.wolverine_incoming_envelopes WHERE received_at LIKE 'postgresql://%')` ergibt 0. Eine Instanz mit `All` liest diese Queues nicht; was dort liegt, bliebe ohne Fehlermeldung liegen, Antworten stünden weiter auf „Wird gesendet“.

### Mehrere Instanzen mit Docker Compose {#compose-scale}

Zum Ausprobieren des Betriebs mit mehreren Instanzen auf einem Rechner liegt `compose.scale.yaml` neben `compose.yaml`. Der Aufruf nimmt beide Dateien, in dieser Reihenfolge:

```
docker compose -f compose.yaml -f compose.scale.yaml up -d --build
```

Es startet einen Migrationslauf (Befehl `migrate`, danach beendet), zwei API-Instanzen (`Hosting__Role=Api`), genau einen Worker (`Hosting__Role=Worker`), Redis und nginx. Alle Instanzen nutzen den Schlüsselring in der Datenbank (`DataProtection__Store=Database`), Redis als Backplane (`Live__Backplane=Redis`), `Hosting__ForwardedHeaders__Enabled=true` mit der festen Adresse von nginx als einzigem vertrauten Proxy und `Hosting__HealthEndpoints=true`. Wie `compose.yaml` läuft der Aufbau in der Entwicklungsumgebung mit Testkonten ohne Passkey: nie öffentlich betreiben.

- **Web, Mails und Anmeldung.** Die Web-Oberfläche liegt wie bisher unter `http://localhost:5173`, die Mails unter `http://localhost:8025`. Die API hat keinen eigenen Port: zwei Instanzen können sich keinen teilen. Die Windows-App meldet sich an `http://localhost:5173` an; nginx leitet `/api/` reihum und `/hubs/` mit fester Zuordnung je Client-Adresse an die Instanzen.
- **Der Worker legt das Beispiel-Spa an** (`Database__SeedDemoData`), die API-Instanzen nicht. Die Testkonten erscheinen nach dem Einspielen; der Dienst `worker` ist bis dahin nicht `healthy`.
- **Adressen der Instanzen.** nginx löst den Namen `api` nur beim Start auf. Nach einem Neuanlegen der API-Instanzen (zum Beispiel `up -d --build` mit neuem Image, nicht nach `restart`) deshalb `docker compose -f compose.yaml -f compose.scale.yaml restart web` ausführen. Die Zahl der Instanzen steht in `compose.scale.yaml` bei `api` unter `deploy.replicas`; der Worker bleibt bei einer Instanz.
- **Healthchecks.** Das Image hat weder `curl` noch `wget`; die Proben lesen die Statuszeile von `/health` über ein TCP-Socket von `bash`. Die API-Instanzen warten auf `migrate` (erfolgreich beendet) und Redis; nginx wartet, bis die API-Instanzen `healthy` sind.
- **Redis** läuft in diesem Aufbau ohne Passwort und ohne Port nach außen. Im Betrieb ein Passwort setzen (siehe [Live-Meldungen mit mehreren Instanzen](#live-backplane)).

### Nur für die Windows-App {#windows-app}

Diese Werte setzt die Windows-App beim Start der API selbst. Von Hand braucht man sie nur, wenn man die API ohne Windows-App ausliefern will.

- `Hosting:WebRoot`: Ordner mit der gebauten Web-Oberfläche. Ist er gesetzt und enthält eine `index.html`, liefert die API die Oberfläche selbst aus, ohne eigenen Webserver. Ohne Angabe liefert sie keine Oberfläche aus; in der Entwicklung leitet die Startseite dann auf `OnlineBooking:PublicUrl` weiter.
- `Hosting:StopFile`: Pfad einer Datei. Legt jemand diese Datei an, beendet sich die API geordnet. So beendet die Windows-App die API ohne Konsole. Ohne Angabe ist das aus.
- `Hosting:RecoveryFile`: Pfad der Datei mit dem verschlüsselten Schlüssel für Gesundheitsdaten, die zum Wiederherstellungsschlüssel gehört. Damit lässt sich am PC mit der Datenbank ein vergessenes Passwort neu setzen.

## Anmeldung des Teams {#anmeldung}

`Passkeys:ServerDomain`: die Domain, an die Passkeys gebunden sind, zum Beispiel `app.mein-spa.at`. In der Entwicklung `localhost`. Ohne Angabe nimmt der Server die Adresse der Anfrage. Im Echtbetrieb fest eintragen: Passkeys gelten nur für genau diese Domain, nach einem Wechsel muss jede Person ihren Passkey neu anlegen. Gilt nur im Server-Betrieb; im Einzelplatz gibt es keine Passkeys.

## Mailversand {#mail}

Über diesen Zugang gehen alle Mails des Servers: Anmeldecodes, Buchungsbestätigungen, Erinnerungen und Antworten der Terminannahme, solange ein Spa kein eigenes Postfach verbunden hat.

- `Mail:Smtp:Host`: SMTP-Server, zum Beispiel `smtp.example.at`. Ohne Angabe verschickt der Server keine Mails. In der Entwicklung stehen sie dann im Log, im Echtbetrieb steht dort nur eine Warnung. Dann kommen auch Anmeldecodes für den Kundenbereich nicht an.
- `Mail:Smtp:Port`: Standard `587`.
- `Mail:Smtp:Security`: `StartTls` (Standard), `SslOnConnect` (meist mit Port `465`) oder `None`. `None` nur für ein lokales Test-Postfach wie Mailpit.
- `Mail:Smtp:UserName`: Benutzername am SMTP-Server. Ohne Angabe meldet sich der Server nicht an.
- `Mail:Smtp:Password`: Passwort dazu. Nur über Geheimnisspeicher oder Umgebungsvariable.
- `Mail:FromAddress`: Absenderadresse, Standard `noreply@aispa.local`. Im Echtbetrieb immer auf eine Adresse der eigenen Domain setzen, die der SMTP-Server versenden darf.
- `Mail:FromName`: Absendername, Standard `AISPA Studio`. Mails eines Spas tragen meist den Namen des Spas.

## Online-Buchung {#online-buchung}

Der Abschnitt `OnlineBooking` gilt für den Kundenbereich. Mehrere Werte sind nur Vorgaben für Standorte, für die das Spa in der Oberfläche keine eigene Regel gespeichert hat.

- `OnlineBooking:PublicUrl`: öffentliche Adresse der Web-Oberfläche, zum Beispiel `https://buchen.mein-spa.at`. Standard `http://localhost:5173`. Links in Mails (Anmeldung, Buchung, Anamnese, Zahlung) zeigen dorthin. Im Echtbetrieb immer setzen.
- `OnlineBooking:MinLeadMinutes`: Vorgabe, wie viele Minuten vor Beginn ein Termin spätestens online buchbar ist. Standard `120`.
- `OnlineBooking:MaxDaysAhead`: Vorgabe, wie viele Tage im Voraus online buchbar ist. Standard `90`.
- `OnlineBooking:CancellationDeadlineHours`: Vorgabe, bis wie viele Stunden vor Beginn Gäste online stornieren können. Standard `24`.
- `OnlineBooking:MaxGuestMoves`: wie oft Gäste einen Termin selbst online verschieben können. Standard `2`, `0` schaltet das Verschieben ab.
- `OnlineBooking:SlotStepMinutes`: Vorgabe für das Raster der angebotenen Startzeiten. Erlaubt `5`, `10`, `15`, `20`, `30` oder `60`, Standard `15`. Ein anderer Wert gilt als `15`.
- `OnlineBooking:NoShowFeePercent`: Vorgabe für die Ausfallgebühr in Prozent des Preises, `0` bis `100`. Standard `0`, also keine Kartenhinterlegung.
- `OnlineBooking:NoShowFeeNewCustomersOnly`: Vorgabe, die Ausfallgebühr nur von Gästen ohne abgeschlossene Behandlung im Spa zu verlangen. Standard `false`.
- `OnlineBooking:CardHoldHours`: Vorgabe, nach wie vielen Stunden ein online gebuchter Termin ohne hinterlegte Karte wieder frei wird. `0` bis `168`, Standard `0` (nie).
- `OnlineBooking:CardHoldPollSeconds`: wie oft der Server nach solchen Terminen sucht, in Sekunden. Standard `300`, mindestens `30`, `0` schaltet die Suche ab.
- `OnlineBooking:CardRetentionDays`: so viele Tage nach dem Termin lässt sich die Ausfallgebühr noch belasten. Danach löscht der Server die hinterlegte Karte beim Zahlungsanbieter. Standard `14`.
- `OnlineBooking:CardCleanupPollSeconds`: wie oft der Server nach solchen Karten sucht, in Sekunden. Standard `3600`, mindestens `60`, `0` schaltet das Löschen ab.
- `OnlineBooking:PaymentRecoveryPollSeconds`: wie oft der Server nach bezahlten Online-Zahlungen sucht, denen nach 15 Minuten noch Beleg, Gutschein oder Erstattung fehlt. Er versucht es bis zu zwei Tage lang erneut. Standard `600`, mindestens `60`, `0` schaltet die Suche ab.
- `Queue:RetrySeconds`: nach wie vielen Sekunden der Server eine fehlgeschlagene Aufgabe wiederholt, etwa eine Mail bei einem Ausfall des Mailservers. Jeder Wert ist ein Versuch. Standard `60`, `300`, `900`. Scheitert auch der letzte Versuch, kommt die Aufgabe in die Fehlerablage.
- `OnlineBooking:PrivacyNoticeVersion`: Kennung des Datenschutzhinweises, den Gäste beim Buchen bestätigen. Standard `2026-10`. Nach einer Änderung des Hinweises erhöhen, damit nachvollziehbar bleibt, wer welche Fassung bestätigt hat.
- `OnlineBooking:AnamnesisValidMonths`: so viele Monate gilt ein Anamnesebogen; danach wird vor dem nächsten Termin ein neuer erbeten. Standard `12`.
- `OnlineBooking:SensitiveRequestsPerMinute`: höchstens so viele Anfragen je IP-Adresse und Minute an Anmeldecode und Buchung. Standard `10`. Schützt vor dem Durchprobieren von Codes.
- `OnlineBooking:PublicRequestsPerMinute`: höchstens so viele lesende Anfragen je IP-Adresse und Minute. Standard `300`.

Wer mehr Anfragen schickt, bekommt die Antwort 429 und muss bis zur nächsten Minute warten. Steht der Server hinter einem Webserver, muss dieser die echte Adresse der Gäste weiterreichen, sonst teilen sich alle Gäste eine Grenze.

### Anmeldung mit Google und Apple {#google-apple}

Gäste können sich im Kundenbereich auch mit Google oder Apple anmelden. Beides ist nur aktiv, wenn alle Werte gesetzt sind; sonst gibt es nur den Code per Mail und den Passkey.

- `OnlineBooking:Google:ClientId` und `OnlineBooking:Google:ClientSecret`: aus der Google Cloud Console (OAuth-Client vom Typ Webanwendung). Weiterleitungsadresse dort: die öffentliche Adresse plus `/api/online/auth/external/google-callback`.
- `OnlineBooking:Apple:ClientId` (die Service-ID), `OnlineBooking:Apple:TeamId`, `OnlineBooking:Apple:KeyId` und `OnlineBooking:Apple:PrivateKey` (der Inhalt der `.p8`-Datei von Apple): aus dem Apple-Developer-Konto. Weiterleitungsadresse dort: die öffentliche Adresse plus `/api/online/auth/external/apple-callback`.

ClientSecret und PrivateKey nur über Geheimnisspeicher oder Umgebungsvariable.

## Online-Zahlungen und Gutscheine {#online-zahlungen}

Wie ein Spa sein Stripe-Konto verbindet und wie der Server als Stripe-Connect-Plattform angemeldet wird, steht im Hilfethema „Zahlungen“, Abschnitt „Stripe Connect als Plattform einrichten“. Hier nur die Übersicht der Schlüssel.

- `OnlineVouchers:PaymentProvider`: leer oder `Stripe` (Standard): Online-Zahlung über das Stripe-Konto des Spas. `Test`: zusätzlich eine Testzahlung für Spas ohne Konto, es fließt kein Geld; nie im Echtbetrieb. `None`: keine Online-Zahlung am ganzen Server, also keine Gutscheine online und keine Ausfallgebühr.
- `OnlineVouchers:Stripe:Connect`: `true`, wenn der Server als Stripe-Connect-Plattform angemeldet ist. Standard `false`.
- `OnlineVouchers:Stripe:SecretKey`: geheimer Stripe-Schlüssel der Plattform oder, ohne Connect, des Servers.
- `OnlineVouchers:Stripe:WebhookSecret`: Signaturgeheimnis des Webhooks für den Schlüssel des Servers.
- `OnlineVouchers:Stripe:ConnectWebhookSecret`: Signaturgeheimnis des Webhooks für die verbundenen Konten.
- `OnlineVouchers:Stripe:SessionMinutes`: so lange bleibt die Zahlungsseite bei Stripe offen, Standard `60`. Stripe erlaubt `30` bis `1440`; Werte außerhalb setzt der Server auf die nächste Grenze.
- `OnlineVouchers:MinAmount` und `OnlineVouchers:MaxAmount`: kleinster und größter Wertgutschein, den Gäste online kaufen können. Standard `10` und `500` in der Währung des Standorts.
- `OnlineVouchers:SuggestedAmounts`: vorgeschlagene Beträge, Standard `25`, `50`, `100` und `150`. Als Liste setzen, siehe „Listen“ oben.

Die Stripe-Schlüssel nur über Geheimnisspeicher oder Umgebungsvariable.

## Gesundheitsdaten {#gesundheitsdaten}

Anamnesebögen und andere Gesundheitsdaten speichert der Server verschlüsselt. Den Schlüssel dafür gibt der Betreiber vor.

- `HealthData:CurrentKeyId`: Kennung des Schlüssels, mit dem neu verschlüsselt wird, zum Beispiel `2026-1`. Frei wählbar.
- `HealthData:Keys:<kennung>`: der Schlüssel zur Kennung, 32 zufällige Byte als Base64. Erzeugen zum Beispiel mit `openssl rand -base64 32`. Als Umgebungsvariable `HealthData__Keys__2026-1`.
- `HealthData:ShareCode`: Schlüsselcode des Spas für den Abgleich mehrerer Stellen. Mit ihm tauschen die Stellen ihre Schlüssel verschlüsselt über die zentrale Datenbank aus. Nur nötig mit „Abgleich“ (siehe unten).

Ohne gültigen Schlüssel zu `CurrentKeyId` lassen sich keine Gesundheitsdaten speichern; der Versuch endet mit einem Fehler. Den Schlüssel sicher aufbewahren: Ist er verloren, sind die damit verschlüsselten Daten nicht mehr lesbar. Er kommt nie in die Datenbank, nie in eine Sicherung neben der Datenbank und nie ins Repository.

So wechselt man den Schlüssel: einen neuen Schlüssel mit neuer Kennung dazugeben, `CurrentKeyId` auf die neue Kennung setzen und den alten Schlüssel stehen lassen. Mit dem alten liest der Server weiter, was früher verschlüsselt wurde.

Die Windows-App erzeugt Schlüssel und Kennung beim Einzelplatz selbst und gibt sie der API mit.

## Abgleich mit der zentralen Datenbank {#abgleich}

Ein Einzelplatz oder ein Standort kann seine Daten mit einer zentralen Datenbank abgleichen. Der Abgleich läuft nur, wenn `Sync:CentralConnectionString` gesetzt ist.

- `Sync:CentralConnectionString`: Verbindung zur zentralen PostgreSQL-Datenbank, mit TLS und eigenem Konto. Enthält ein Passwort, also nur über Geheimnisspeicher oder Umgebungsvariable. Die Windows-App setzt es aus ihren eigenen Einstellungen.
- `Sync:SiteId`: Name dieser Stelle im Protokoll der zentralen Seite. Standard ist der Rechnername.
- `Sync:IntervalSeconds`: Abstand zwischen zwei Abgleichen in Sekunden, Standard `60`, mindestens `10`. Fehlt die Verbindung, versucht es der Server beim nächsten Mal wieder.
- `Sync:CentralRetentionDays`: so viele Tage bleiben Änderungen im Protokoll der zentralen Datenbank, Standard `90`, `0` heißt nie aufräumen. Eine Stelle, die länger nicht abgeglichen hat, und jeder neue PC holen danach den ganzen Bestand des Spas.

## KI {#ki}

Claude übernimmt Terminannahme, Assistent, Zusammenfassungen, Texte für die Wiederbuchungs-Hinweise und das Lesen von Anamnesebögen auf Papier. Ohne Schlüssel arbeitet der Server ohne KI: Die Terminannahme läuft nach festen Regeln, Papierbögen überträgt das Team von Hand. Pflichtzahlen wie Kasse und Steuer rechnet nie die KI.

- `Ai:Claude:ApiKey`: API-Schlüssel von Anthropic. Ist er gesetzt, ist die KI an. Nur über Geheimnisspeicher, Umgebungsvariable oder in der Entwicklung über User-Secrets. In Docker Compose zum Beispiel aus der Variable `AISPA_CLAUDE_API_KEY` in einer Datei `.env`.
- `Ai:Provider`: `RuleBased` schaltet die KI ganz ab, auch wenn ein Schlüssel gesetzt ist, und auch das lokale Modell. Ohne Angabe gilt der Schlüssel.
- `Ai:Claude:Model`: welches Modell, Standard `claude-opus-5-5`.
- `Ai:Claude:Effort`: Denktiefe, zum Beispiel `low`, `medium` oder `high`. Ohne Angabe nimmt die Terminannahme `low` und das Lesen von Anamnesebögen `medium`. Ein gesetzter Wert gilt für beide.
- `Ai:Local:Url`, `Ai:Local:Model` und weitere: das optionale lokale KI-Modell ohne Internet, beschrieben im Hilfethema „KI“, Abschnitt „Einstellungen am Server“. Leer heißt aus.

## Terminannahme und Postfach {#terminannahme}

Die Terminannahme liest Anfragen aus einem Postfach, schlägt Termine vor und schreibt Antworten. Spas verbinden ihr eigenes Postfach meist in der Oberfläche; die Einstellungen hier gelten für ein Postfach, das der Server selbst abfragt.

- `MailIntake:Mailbox`: `Imap`, `Graph` (Microsoft 365) oder leer. Leer heißt Test-Postfach im Speicher; Mails einwerfen kann man dort nur in der Entwicklung.
- `MailIntake:TenantId`: Kennung des Spas, dem dieses Postfach gehört. Ohne Angabe das einzige Spa auf dem Server; gibt es mehrere, ist sie Pflicht.
- `MailIntake:AutoBook`: `true` (Standard) trägt einen Termin gleich ein, wenn die Wunschzeit genau angegeben und frei ist; `false` schlägt nur vor.
- `MailIntake:AutoSend`: `true` sendet Antworten ohne Prüfung durch die Rezeption. Standard `false`. Erst einschalten, wenn das Team den Antworten vertraut.
- `MailIntake:PollSeconds`: wie oft die Postfächer abgefragt werden, in Sekunden. Standard `30`, mindestens `5`.
- `MailIntake:SpaName`: Name in der Grußformel, Standard `Ihr Spa-Team`.
- `MailIntake:MaxProposals`: höchstens so viele Terminvorschläge in einer Antwort, Standard `3`.
- `MailIntake:SearchDays`: so viele Tage ab dem Wunschtermin wird nach Alternativen gesucht, Standard `14`.
- `MailIntake:MinLeadMinutes`: frühester Termin, den die Terminannahme vergibt, gerechnet ab Eingang der Mail. Standard `60`.
- Welche Absender als Treatwell oder andere Buchungsplattform gelten, steht nicht mehr in der Server-Konfiguration, sondern je Spa unter „Verwaltung“ › „Kontakt“ › „Postfach & Nummern“ › [Absender-Regeln](hilfe:postfach#absender-regeln). Die frühere Option `MailIntake:TreatwellSenderDomains` entfällt.

Gesendet wird über den Mailversand (siehe „Mailversand“).

### IMAP {#imap}

Gilt mit `MailIntake:Mailbox` = `Imap`.

- `MailIntake:Imap:Host`: IMAP-Server.
- `MailIntake:Imap:Port`: Standard `993`.
- `MailIntake:Imap:Security`: `SslOnConnect` (Standard), `StartTls` oder `None`.
- `MailIntake:Imap:UserName` und `MailIntake:Imap:Password`: Zugang zum Postfach. Das Passwort nur über Geheimnisspeicher oder Umgebungsvariable.
- `MailIntake:Imap:Folder`: Ordner, der gelesen wird, Standard `INBOX`.
- `MailIntake:Imap:ProcessedFolder`: verarbeitete Mails hierher verschieben. Ohne Angabe bleiben sie im Ordner und werden nur markiert.
- `MailIntake:Imap:LookbackDays`: nur Mails der letzten so vielen Tage abrufen, Standard `7`. So landet ein volles Postfach nicht auf einmal in der Warteschlange.
- `MailIntake:Imap:TrustedAuthServers`: Kennungen der eigenen Empfangsserver in der Kopfzeile „Authentication-Results“. Ohne Angabe zählt nur die oberste dieser Kopfzeilen.

### Microsoft 365 {#graph}

Gilt mit `MailIntake:Mailbox` = `Graph`. Dafür braucht es eine App-Registrierung in Microsoft Entra mit Zugriff auf das Postfach.

- `MailIntake:Graph:TenantId`: Verzeichnis-ID (Mandant) bei Microsoft. Nicht zu verwechseln mit `MailIntake:TenantId`.
- `MailIntake:Graph:ClientId` und `MailIntake:Graph:ClientSecret`: aus der App-Registrierung. Das Geheimnis nur über Geheimnisspeicher oder Umgebungsvariable.
- `MailIntake:Graph:Mailbox`: Adresse oder ID des Postfachs, zum Beispiel `termine@mein-spa.at`.
- `MailIntake:Graph:Folder`: Ordner, Standard `inbox`. Name eines bekannten Ordners oder dessen ID.
- `MailIntake:Graph:ProcessedCategory`: Kategorie, mit der verarbeitete Mails markiert werden, Standard `AISPA verarbeitet`. In Outlook sichtbar.
- `MailIntake:Graph:LookbackDays`: Standard `7`, wie bei IMAP.
- `MailIntake:Graph:TrustedAuthServers`: wie bei IMAP. Für Exchange Online `spf.protection.outlook.com` eintragen.

## SMS, WhatsApp und Anrufe {#textkanaele}

Ohne Anbieter verschickt der Server keine SMS und keine WhatsApp-Nachrichten. In der Entwicklung stehen sie dann im Log, im Echtbetrieb steht dort nur eine Warnung.

- `TextChannels:SmsProvider`: `Twilio` oder leer.
- `TextChannels:WhatsAppProvider`: `Twilio`, `Meta` (WhatsApp Cloud API) oder leer. Erinnerungen per WhatsApp gehen zusätzlich nur mit einer freigegebenen Vorlage, die das Spa in den Erinnerungen einträgt.

### Twilio {#twilio}

Aktiv, wenn `AccountSid` und `AuthToken` gesetzt sind.

- `TextChannels:Twilio:AccountSid` und `TextChannels:Twilio:AuthToken`: aus der Twilio-Konsole. Das Token nur über Geheimnisspeicher oder Umgebungsvariable.
- `TextChannels:Twilio:SmsFrom`: Absendernummer für SMS, zum Beispiel `+43676800123`.
- `TextChannels:Twilio:WhatsAppFrom`: freigeschaltete WhatsApp-Business-Nummer.
- `TextChannels:Twilio:WebhookUrl`: öffentliche Adresse des Webhooks genau so, wie sie in Twilio eingetragen ist, zum Beispiel `https://app.mein-spa.at/api/hooks/twilio/messages`. Twilio signiert über diese Adresse; hinter einem Webserver weicht die interne Adresse ab, deshalb hier die öffentliche eintragen.

### WhatsApp über Meta {#meta}

Aktiv für eingehende Nachrichten, wenn `AppSecret` und `VerifyToken` gesetzt sind. Webhook-Adresse in Meta: die öffentliche Adresse plus `/api/hooks/whatsapp`.

- `TextChannels:Meta:AccessToken`: Zugriffstoken der WhatsApp Cloud API, zum Senden.
- `TextChannels:Meta:PhoneNumberId`: ID der Absendernummer bei Meta.
- `TextChannels:Meta:AppSecret`: App-Geheimnis, mit dem der Server die Signatur eingehender Nachrichten prüft.
- `TextChannels:Meta:VerifyToken`: frei gewählter Text, den man auch bei Meta beim Einrichten des Webhooks einträgt.

Token und Geheimnis nur über Geheimnisspeicher oder Umgebungsvariable.

### Anrufe und Sprachnachrichten {#anrufe}

- `TextChannels:Voice:ApiKey`: Schlüssel, mit dem eine Telefonanlage Aufnahmen an `/api/hooks/voice/recordings` schickt (Kopfzeile `X-Aispa-Voice-Key`). Ohne Angabe nimmt der Server dort nichts an. Frei wählen, lang und zufällig.
- `TextChannels:Voice:MaxRecordingBytes`: größte angenommene Aufnahme in Byte, Standard `20971520` (20 MB).
- `TextChannels:Voice:MaxRecordingSeconds`: längste Aufnahme eines Anrufs über Twilio in Sekunden, Standard `120`.
- `TextChannels:Voice:SpeechProvider`: `Azure` schaltet die Spracherkennung mit Azure AI Speech ein. Ohne Angabe gibt es keine echte Spracherkennung.
- `TextChannels:Voice:Azure:Key`: Schlüssel des Azure-Speech-Dienstes. Nur über Geheimnisspeicher oder Umgebungsvariable.
- `TextChannels:Voice:Azure:Region`: Region des Dienstes, Standard `westeurope`. Erlaubt sind nur Regionen in der EU und der Schweiz: `westeurope`, `northeurope`, `germanywestcentral`, `francecentral`, `swedencentral`, `italynorth`, `polandcentral`, `spaincentral`, `switzerlandnorth` und `switzerlandwest`.
- `TextChannels:Voice:Azure:Locales`: Sprachen, unter denen der Dienst die passende erkennt, Standard `de-DE` und `en-US`. Als Liste setzen.

Mit `Azure` prüft der Server die Werte schon beim Start und startet nicht, wenn etwas fehlt oder die Region nicht erlaubt ist.

## Betreiber-Zugang {#betreiber}

`Operator:ApiKey`: Schlüssel für die Betreiber-Oberfläche unter `/betreiber/`. Dort sieht der Betreiber alle Spas und ordnet ihnen Nummern und Adressen für die Terminannahme zu. Ohne Schlüssel gibt es die Betreiber-Oberfläche nicht. Frei wählen, lang und zufällig, nur über Geheimnisspeicher oder Umgebungsvariable.

## Hintergrundaufgaben {#hintergrund}

Der Server sucht regelmäßig nach Arbeit. `0` schaltet die jeweilige Aufgabe ab.

- `Reminders:PollSeconds`: wie oft nach fälligen Terminerinnerungen gesucht wird, in Sekunden. Standard `300`, mindestens `30`.
- `Retention:PollSeconds`: wie oft nach Gästen gesucht wird, die einen Wiederbuchungs-Hinweis bekommen möchten. Standard `3600`, mindestens `60`.
- `AccessExpiry:PollSeconds`: wie oft nach befristeten Zugängen des Teams gesucht wird, die bald enden. Standard `3600`, mindestens `60`.
- `AccessExpiry:NoticeDays`: so viele Tage vor dem Ende eines befristeten Zugangs wird Bescheid gegeben. Standard `7`, mindestens `1`.

## Kassa {#kassa-server}

- `Payments:DigitalEuro`: `true` bietet an Kassen in Euro-Ländern den digitalen Euro als Zahlungsart an. Standard `false`. Erst einschalten, wenn die EZB ihn ausgibt und der Zahlungsdienstleister ihn abwickelt.
- `Fiscal:TestTse`: `true` signiert deutsche Kassen mit einer Test-TSE. Standard `false`. Nur für Entwicklung und Prototyp, nie im Echtbetrieb. Ohne TSE lässt sich eine deutsche Kassa nicht einrichten.
- `VatIdCheck:Provider`: `Test` prüft UID-Nummern ohne Netz: Nummern mit `000` am Ende gelten als ungültig, bei `999` am Ende antwortet der Dienst nicht. Ohne Angabe prüft der Server über VIES der EU. Im Echtbetrieb leer lassen.

## Schlüsselring des Servers {#schluesselring}

Der Server verschlüsselt Anmelde-Cookies und die Geheimnisse, die Spas in der Oberfläche eintragen (Passwörter eigener Postfächer, eigene Stripe-Schlüssel), mit einem eigenen Schlüsselring. Geht er verloren, sind alle Anmeldungen beendet und die Spas müssen Postfach-Passwörter und Stripe-Schlüssel neu eintragen.

- `DataProtection:Store`: `FileSystem` oder `Database`, Standard `FileSystem`. Die Schreibweise spielt keine Rolle, ein anderer Wert bricht den Start mit einer Meldung ab. Mit `FileSystem` liegt der Schlüsselring wie bisher in einem Ordner, im Docker-Image unter `/home/app/.aspnet/DataProtection-Keys`; diesen Ordner als Volume dauerhaft speichern und mitsichern. Mit `Database` liegt er in der Tabelle `system."DataProtectionKeys"` derselben Datenbank. Alle Instanzen teilen ihn dann, und er ist Teil der Datenbanksicherung; ein eigenes Volume braucht es nicht. Die Tabelle legen der Befehl `migrate` und `Database:MigrateOnStartup` an; fehlt sie, bricht der Start mit einer Meldung ab, die auf beides hinweist. Alle Instanzen laufen im selben Programmordner, im Docker-Image `/app`, denn ASP.NET Core leitet den Zweck der Schlüssel davon ab. Die Schlüssel stehen in der Datenbank ohne eigenen Schutz: Wer die Datenbank oder ihre Sicherung lesen kann, kann damit auch die Geheimnisse der Spas entschlüsseln. Ein Schutz über Azure Key Vault folgt mit dem echten Deployment.
- `DataProtection:ImportFolder`: Ordner, aus dem der Server mit `Database` die bisherigen Schlüssel übernimmt. Ohne Angabe gilt der Standardordner, im Docker-Image `/home/app/.aspnet/DataProtection-Keys`. Die Übernahme läuft bei jedem Start mit `Database`, auch ohne `Database:MigrateOnStartup`, bevor der Server Anfragen annimmt. Sie trägt nur fehlende Schlüssel ein und schreibt ins Protokoll, wie viele es waren. So bleiben Anmeldungen und gespeicherte Geheimnisse nach der Umstellung lesbar. Unter Windows schützt ASP.NET Core die Schlüssel im Standardordner mit `DPAPI`; sie sind dann nur unter demselben Windows-Konto auf demselben Rechner lesbar, und das Protokoll warnt. Das Protokoll von Data Protection steht fest auf `Warning` (`Logging:LogLevel:Microsoft.AspNetCore.DataProtection`); diese Stufe nicht auf `Debug` oder `Trace` senken, denn dann stehen die Schlüssel aus der Datenbank im Klartext im Protokoll.

## Vor dem Echtbetrieb prüfen {#checkliste}

1. `ASPNETCORE_ENVIRONMENT` ist `Production` oder nicht gesetzt.
2. `ConnectionStrings:aispadb` zeigt auf die echte Datenbank, das Passwort kommt nicht aus einer Datei im Repository.
3. `Database:SeedDemoData`, `Fiscal:TestTse` und `VatIdCheck:Provider` = `Test` sind nicht gesetzt, `OnlineVouchers:PaymentProvider` ist nicht `Test`.
4. `HealthData:CurrentKeyId` und der Schlüssel dazu sind gesetzt und an einem sicheren Ort aufbewahrt; der Schlüssel aus `appsettings.Development.json` gilt nicht.
5. `OnlineBooking:PublicUrl` und `Passkeys:ServerDomain` tragen die echte Adresse.
6. `Mail:Smtp:Host` und `Mail:FromAddress` sind gesetzt und eine Probemail kommt an.
7. Der Schlüsselring liegt in einem dauerhaften Volume und ist Teil der Sicherung; mit `DataProtection:Store` = `Database` liegt er in der Datenbank und ist mit ihr gesichert.
8. Mit Container Apps, Kubernetes oder einem Load Balancer: `Hosting:HealthEndpoints` ist `true`, und die Proben für Start, Bereitschaft (`/health`) und Lebendprüfung (`/alive`) gehen aus dem internen Netz direkt an die Instanz, nicht über nginx. Port `8080` ist nicht öffentlich erreichbar, denn die Endpunkte haben keine Anmeldung und kein Rate Limit. Ist `AllowedHosts` nicht `*`, senden die Proben einen erlaubten Host (Kubernetes: `httpGet.httpHeaders`), sonst antworten sie mit 400.
9. Hinter Proxy oder Ingress: `Hosting:ForwardedHeaders:Enabled` ist `true`, `KnownNetworks` enthält nur die Netze der Proxys, `ForwardLimit` stimmt mit der Zahl der Proxys überein, und `ASPNETCORE_FORWARDEDHEADERS_ENABLED` ist nicht gesetzt. Mit mehreren Instanzen sind `Passkeys:ServerDomain` und `Twilio:WebhookUrl` fest gesetzt.
10. Mit mehreren Instanzen: `Live:Backplane` ist bei API und Worker `Redis` mit demselben `ConnectionStrings:redis`, Redis ist nur im internen Netz erreichbar und hat ein Passwort, und der Ingress hält die Verbindungen eines Clients bei derselben Instanz (Session-Affinität, in Container Apps `affinity: sticky`).
11. Mit mehreren Instanzen: Keine Instanz läuft mit `Hosting:Role` = `All`, genau eine mit `Worker`, und der Datenbankbenutzer darf das Schema `wolverine_queues` anlegen (siehe [Mail-Queue mit mehreren Instanzen](#mail-queue)).
