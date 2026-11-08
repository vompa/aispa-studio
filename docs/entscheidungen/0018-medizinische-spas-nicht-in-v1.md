# 0018. Medizinische Spas nicht in Version 1, Architektur bleibt offen

- **Status:** angenommen
- **Datum:** 17.10.2026
- **Entschieden von:** Auftraggeber

## Kontext

Offene Frage aus den ergänzenden Themen: Sollen Spas mit ärztlichen oder medizinischen Behandlungen (Physiotherapie, ästhetische Medizin) Zielgruppe sein? Dann kämen Patientendokumentation und Abrechnung mit Krankenkassen dazu.

## Optionen

| Option | Bewertung |
| --- | --- |
| Von Anfang an mit Medizin | deutlich mehr Recht und Funktionen vor dem ersten Kunden |
| Nie | verschenkt einen naheliegenden Markt |
| Vorsehen, aber nicht in Version 1 | gewählt |

## Entscheidung

„Man kann es vorsehen, für die erste Version noch nicht.“ Medizinische Spas gehören nicht zu Version 1; die Architektur bleibt für sie offen.

## Begründung

Fokus auf den Kern; Gesundheitsdaten sind ohnehin schon getrennt und verschlüsselt, Steuerbefreiung für Heilbehandlungen ist als Steuerstammdatum vorgesehen.

## Folgen und Stand

Keine Funktionen für Patientendokumentation oder Kassenabrechnung bauen, aber nichts verbauen (z. B. Gesundheitsdaten als eigene Klasse beibehalten).

## Quellen

- [Ergänzende Themen, offene Frage](../konzepte/ergaenzende-themen.md)
