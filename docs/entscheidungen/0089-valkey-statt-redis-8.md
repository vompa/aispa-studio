# 0089. Valkey statt Redis 8 für die Backplane des Live-Kanals

- **Status:** angenommen
- **Datum:** 08.11.2026
- **Entschieden von:** Claude mit dem Council (fünf Berater, Peer-Review, Vorsitz), vom Auftraggeber delegiert am 08.11.2026 („Entscheide du bzw frag die Berater und Arbeite automatisch weiter“)

## Kontext

Der Mehrinstanz-Betrieb ([ADR 0087](0087-mehrinstanz-betrieb.md)) verteilt die Live-Meldungen über einen Server mit
Redis-Protokoll. `compose.scale.yaml`, der AppHost und die Tests nutzten bisher `redis:8-alpine`. Redis 8 steht nicht
mehr unter BSD, sondern zur Wahl unter RSALv2, SSPLv1 oder AGPLv3. Für den Eigenbetrieb eines Spas ist das kein
Hindernis; bietet der Betreiber AISPA aber später selbst als Dienst an, greifen genau diese Lizenzen. Das Windows-Paket
enthält keinen Redis.

## Optionen

| Option | Bewertung |
| --- | --- |
| Valkey (Linux Foundation, BSD-3-Clause, Redis-kompatibel) | gewählt: gleiche Lizenzlage wie bisher, aktuelle Sicherheitsupdates, Image 42 MB statt 120 MB |
| Redis 7.2 festpinnen (letzte BSD-Fassung) | verworfen: bleibt ohne neue Sicherheitsupdates |
| Nichts ändern, nur Hinweis in der Doku | verworfen: schiebt die Lizenzfrage bis zu einer SaaS-Entscheidung, dann ist der Wechsel teurer |

## Entscheidung

`compose.scale.yaml`, der AppHost (`AddRedis(...).WithImage("valkey/valkey").WithImageTag("8-alpine")`) und
`LiveBackplaneTests` nutzen `valkey/valkey:8-alpine`. Der Code bleibt unverändert: StackExchange.Redis und
`Microsoft.AspNetCore.SignalR.StackExchangeRedis` sprechen mit Valkey wie mit Redis, Schlüssel und Einstellungen heißen
weiter `redis` (`Live:Backplane=Redis`, `ConnectionStrings:redis`). Ein vorhandener Redis-Server bleibt möglich.

## Begründung

Gemessen am 08.11.2026: Alle 30 `LiveBackplaneTests` liefen grün gegen Valkey 8.1 (Meldungen über zwei Instanzen,
Kanalpräfix, Ausfall, Hängen und Rückkehr des Servers). Das Image bringt `redis-server` und `redis-cli` als Verweise mit,
deshalb brauchen Testcontainers (`RedisBuilder`) und Aspire (`AddRedis`) kein neues Paket. Der Council war gespalten
(drei Berater für Valkey, zwei für „erst bei einer SaaS-Entscheidung“); entschieden hat die Messung, die alle fünf
verlangt hatten.

## Folgen und Stand

- Code: `compose.scale.yaml`, `src/AispaStudio.AppHost/AppHost.cs`, `tests/AispaStudio.Api.Tests/LiveBackplaneTests.cs`
  (`RedisImage`); Doku `docs/installation/README.md`, `docs/installation/server.md`.
- Nicht geprüft: der AppHost mit `--Live:Backplane=Redis` im laufenden Betrieb (nur gebaut) und `compose.scale.yaml` mit
  Valkey im Betrieb.
- Die Frage, ob AISPA als Dienst angeboten wird, bleibt beim Auftraggeber; dieser ADR entscheidet sie nicht.

## Quellen

- [ADR 0087](0087-mehrinstanz-betrieb.md) Mehrinstanz-Betrieb, Nachtrag T-046 (Backplane)
- Valkey: https://valkey.io, Lizenz BSD-3-Clause
