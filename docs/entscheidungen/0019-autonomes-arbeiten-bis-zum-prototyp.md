# 0019. Arbeitsweise: Claude merged nach grüner CI selbst und entscheidet Details bis zum lauffähigen Prototyp

- **Status:** angenommen
- **Datum:** 17.10.2026, 21:54
- **Entschieden von:** Auftraggeber

## Kontext

Nach dem Kalender-PR wollte der Auftraggeber nicht bei jedem Schritt gefragt werden.

## Optionen

| Option | Bewertung |
| --- | --- |
| Jeder PR wartet auf Freigabe des Auftraggebers | langsam |
| Autonomes Mergen nach grüner CI | gewählt |

## Entscheidung

„Du kannst jetzt alle PR autonom mergen, ich schaue mir diese dann morgen an, Entscheidungen treffe ich dann erst wieder, sobald wir einen lauffähigen Prototyp haben.“ Claude trifft Detailentscheidungen selbst nach den bisherigen Festlegungen und dokumentiert sie im PR-Text.

## Begründung

Tempo bis zum Prototyp.

## Folgen und Stand

Alle Detailentscheidungen ab PR #4 stehen in den PR-Beschreibungen und gesammelt im [PR-Protokoll](../umsetzung/pr-protokoll.md). Unumkehrbares (Produktion, Löschen, Nachrichten nach außen wie die Partneranfragen) bleibt beim Auftraggeber.
