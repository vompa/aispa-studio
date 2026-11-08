# 0016. Hosting: Start auf Azure, portabel gebaut

- **Status:** angenommen
- **Datum:** 17.10.2026
- **Entschieden von:** Empfehlung Claude, mit dem Entscheidungsdokument vom Auftraggeber angenommen

## Kontext

Auftraggeber: „Was kostet der Betrieb der Software z. B. auf Azure?“

## Optionen

| Option | Bewertung |
| --- | --- |
| AWS | etwa gleich teuer, weniger .NET-Komfort, kein verwalteter SignalR-Dienst |
| STACKIT | 20 bis 40 % günstiger, deutscher Anbieter; SignalR und Queue selbst betreiben; bester Kandidat für später |
| Hetzner | 60 bis 80 % günstiger, aber viel Betriebsaufwand |
| Azure, portabel | gewählt |

## Entscheidung

Start auf Azure in der Region Deutschland (Container Apps, PostgreSQL, SignalR Service, Service Bus), ausgerollt mit .NET Aspire. Container, PostgreSQL und Messaging hinter Abstraktionen, damit ein Umzug zu STACKIT oder Hetzner möglich bleibt.

## Begründung

Azure hat die beste Anbindung an .NET 10 und Aspire. Bei 10 bis 100 Kunden entscheidet der Anbieter nicht über das Geschäft, die Geschwindigkeit bis zum stabilen Produkt schon. KI ist ab etwa 100 Kunden der größere Kostenblock.

## Folgen und Stand

Noch kein Produktivbetrieb. Lokal laufen Aspire und `docker compose` (PR #26).
