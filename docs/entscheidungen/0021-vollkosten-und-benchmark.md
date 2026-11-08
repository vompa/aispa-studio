# 0021. Vollkosten pro Behandlung und Vergleich mit anderen Spas

- **Status:** angenommen
- **Datum:** 17.10.2026
- **Entschieden von:** Auftraggeber (Anforderung), Ausgestaltung Claude

## Kontext

Auftraggeber: „Eine wahre Kostenanalyse pro Behandlung mit Material, Mitarbeiter, Miete, Raumpflege usw. Eine Möglichkeit, sich mit anderen auf Knopfdruck zu vergleichen.“ Kein Anbieter rechnet Vollkosten je Termin; einen DACH-Benchmark gibt es nicht.

## Optionen

| Option | Bewertung |
| --- | --- |
| Kosten nur je Kategorie (wie Zenoti) | zu grob |
| Kostenrechnung dem Kunden in Excel überlassen | macht niemand regelmäßig |
| Benchmark aus öffentlichen Studien | veraltet, kein Vergleich nach Größe und Region |
| Vollkosten je Termin, Benchmark nur als anonyme Verteilung | gewählt |

## Entscheidung

Jeder Termin bekommt Material, Personal, Raum, Raumpflege und Gemeinkosten. Der Vergleich zeigt andere Spas nur als Verteilung, nur aggregiert und historisch, erst ab 10 Spas je Gruppe, mit Opt-in. Keine aktuellen Einzelpreise anderer Betriebe (Kartellrecht).

## Begründung

Die Kanalmarge (z. B. 35 % Treatwell-Provision) zeigt sonst niemand; Preisentscheidungen brauchen die einzelne Behandlung.

## Folgen und Stand

Vollkosten in PR #14 (Vorgaben 70 % Auslastung, 75 % Produktivität), Vergleich in PR #24 mit Beispieldaten (`SampleBenchmarkPeers`, gekennzeichnet). Ein echter Vergleichsdienst ersetzt später nur `IBenchmarkPeers`.
