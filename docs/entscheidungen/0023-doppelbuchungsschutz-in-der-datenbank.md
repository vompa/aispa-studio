# 0023. Doppelbuchungsschutz in der Datenbank

- **Status:** angenommen
- **Datum:** 17.10.2026
- **Entschieden von:** Claude (PR #3, vor der Autonomie-Freigabe vom Auftraggeber gemergt)

## Kontext

Mehrere Kanäle (Rezeption, Online, KI, Treatwell) buchen gleichzeitig. Eine Prüfung nur im Code kann bei gleichzeitigen Anfragen versagen.

## Optionen

| Option | Bewertung |
| --- | --- |
| Prüfung nur im Anwendungscode | Wettlaufsituationen möglich |
| Sperren je Ressource in der Anwendung | fehleranfällig über mehrere Instanzen |
| PostgreSQL-Ausschlussbedingung plus Vorprüfung mit Begründung | gewählt |

## Entscheidung

Alle Belegungen (Termin oder Sperrzeit) stehen in `ResourceBookings`. Eine `EXCLUDE`-Bedingung (btree_gist, DEFERRABLE INITIALLY DEFERRED) verhindert Überschneidungen je `ResourceId`. Der `ResourcePlanner` prüft vorher und begründet Ablehnungen (HTTP 409 `BookingRejected`). Ein Termin sperrt Person (mit Vor- und Nachbereitung), Raum (plus Raumpflege) und Gerät (nur Behandlungszeit).

## Begründung

Die Datenbank garantiert, was der Code verspricht, auch bei zwei gleichzeitigen Buchungen.

## Folgen und Stand

API-Zeiten sind UTC, angezeigt wird in der Zeitzone des Standorts. Änderungen gehen über die Queue per SignalR an die Gruppe `location:{id}`.

## Quellen

- [Funktionen, Kalender](../features.md#kalender)
- [PR-Protokoll](../umsetzung/pr-protokoll.md)
