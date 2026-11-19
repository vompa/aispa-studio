# 0022. Reporting kommt zur Person und endet in Aufgaben

- **Status:** angenommen
- **Datum:** 17.10.2026
- **Entschieden von:** Auftraggeber (Anforderung), Konzept Claude

## Kontext

Auftraggeber: Reporting für Behörden, Management, Rezeption (Tagesstart) und Therapeuten, „auch eine Todo, was für die jeweilige Behandlung vorzubereiten ist, welcher Raum, welcher Kunde, Vorlieben“.

## Optionen

| Option | Bewertung |
| --- | --- |
| Klassischer Berichtsbereich mit Filtern | wird selten genutzt |
| Nur Export nach Power BI oder Excel | kleine Spas haben keine Analysten |
| Berichte live auf den Buchungstabellen | bremst Kalender und Kasse |
| Rollenberichte, Behandlungskarte mit Vorbereitungs-Todo, Lesemodelle | gewählt |

## Entscheidung

Jede Rolle bekommt zur richtigen Zeit genau die Information, die sie zum Handeln braucht: Behandlungskarte mit Vorbereitungs-Todo, Tagesbriefing, Management-Cockpit, Pflichtberichte je Land. Pflicht- und Steuerdaten kommen nur aus dem unveränderbaren Journal, nie aus der KI.

## Begründung

Die Behandlungskarte ist das sichtbarste Alleinstellungsmerkmal: Was ein Therapeut notiert, steht beim nächsten Kollegen auf der Karte.

## Folgen und Stand

Berichte in PR #16 (Mein Tag, Tagesstart, Management, Pflichtberichte, Vorbereitung). Offen: KI-Zusammenfassung im Tagesstart, Push um 7:30 und Mails, Lesemodelle bei Bedarf.

- Nachtrag (19.11.2026): KI-Kommentar im Managementbericht. Ab sieben Tagen schreibt die KI im Hintergrund drei bis fünf Sätze aus den fertig gerechneten Kennzahlen (Tabelle `ManagementSummaries` je Standort, Zeitraum und Sicht mit/ohne Beträge; Fingerabdruck der Fakten; Queue wie der Tagesstart). Jede Zahl im Text muss wörtlich in den Fakten stehen, sonst wird er verworfen (ADR 0008). Ohne das Recht „Berichte mit Beträgen“ sieht die KI keine Beträge; Gästenamen und Gesundheitsdaten nie.
- Nachtrag (19.11.2026): Wochen- und Monatsbericht per Mail. Ein Hintergrunddienst schickt montags ab 7 Uhr Ortszeit den Bericht der Vorwoche und am Monatsersten den des Vormonats, je Standort an Inhaber, Manager und Standortleitung mit Sicht auf den Standort (Standard an, jede Person bestellt selbst ab: `StaffAccess.ReportMails`). Beträge nur mit dem Recht „Berichte mit Beträgen“. Gegen doppelten Versand steht jeder Versand vorher in `ReportMailings` (eindeutig je Standort, Art und Beginn); nachgeholt wird bis drei Tage. Der KI-Kommentar wird für die Mail sofort geschrieben, mit derselben Zahlenprüfung. Push um 7:30 bleibt offen.

## Quellen

- [Reporting-Konzept](../konzepte/reporting.md)
