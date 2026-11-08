# 0012. Buchhaltungsexporte: DATEV und BMD zuerst

- **Status:** angenommen
- **Datum:** 17.10.2026
- **Entschieden von:** Auftraggeber

## Kontext

Steuerberater in DACH arbeiten mit unterschiedlichen Systemen (DATEV, BMD, RZL, Bexio, Abacus, Banana). Eine eigene Finanzbuchhaltung wollen Steuerberater nicht.

## Optionen

| Option | Bewertung |
| --- | --- |
| Nur DATEV | reicht nicht für AT und CH |
| Eigene Finanzbuchhaltung | riesiger Aufwand |
| CSV, Zuordnung macht der Steuerberater | Fehler bei jedem Monatsabschluss |
| Eine Kontenzuordnung, Exporte je System, DATEV und BMD zuerst | gewählt |

## Entscheidung

Eine Kontenzuordnung im System speist alle Exporte. Zuerst DATEV (DE) und BMD (AT), danach RZL, Bexio und Abacus. Erlöskonten frei je Leistung, Produkt, Gutschein und Kanal; Kostenstellen je Raum, Bereich oder Standort.

## Begründung

Ein Export je Steuerberater-System ist Voraussetzung für den Verkauf; DATEV und BMD decken die ersten beiden Märkte ab.

## Folgen und Stand

Buchhaltungsexport in PR #11, Lohnexport (BMD Lohn, DATEV LODAS, DATEV Lohn und Gehalt) und Kassendifferenzen in PR #25. Schweizer Kassen sind noch nicht im Export. LODAS-Bearbeitungsschlüssel mit Steuerberatung prüfen.
