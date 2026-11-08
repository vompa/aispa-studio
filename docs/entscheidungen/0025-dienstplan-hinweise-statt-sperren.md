# 0025. Dienstplan: Hinweise zur Arbeitszeit statt Sperren

- **Status:** angenommen
- **Datum:** 17.10.2026
- **Entschieden von:** Claude (autonom, siehe ADR 0019)

## Kontext

Der Dienstplan muss gegen das Arbeitszeitrecht geprüft werden. Spas brauchen aber Spielraum (Aushilfe, kurzfristiger Tausch), und die Regeln unterscheiden sich je Land.

## Optionen

| Option | Bewertung |
| --- | --- |
| Harte Sperre bei Verstoß | blockiert den Betrieb in Ausnahmefällen |
| Keine Prüfung | Haftungsrisiko für Kunden |
| Sichtbare Hinweise im Plan | gewählt |

## Entscheidung

Der Plan zeigt Hinweise (mehr als 10 Stunden am Tag, weniger als 11 Stunden Ruhe, mehr als 48 Stunden pro Woche, 7 Tage am Stück) direkt an. `RosterDay` ersetzt die Wochenarbeitszeit für einen Tag; `RosterService.ShiftsAsync` ist die einzige Quelle für Kalender und Planer.

## Begründung

Die Verantwortung bleibt beim Menschen, und KI-Planung bleibt Vorschlag (AI Act).

## Folgen und Stand

Grenzwerte je Land und die Ist-Zeiterfassung kamen mit [ADR 0031](0031-zeiterfassung-abschnitte-mit-protokoll.md); Landesfeiertage sind offen.

## Quellen

- [Funktionen, Dienstplan](../features.md#dienstplan)
