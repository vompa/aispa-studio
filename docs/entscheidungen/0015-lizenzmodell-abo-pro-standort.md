# 0015. Lizenzmodell: Abo pro Standort, 99, 199 und 279 Euro

- **Status:** angenommen
- **Datum:** 17.10.2026
- **Entschieden von:** Auftraggeber

## Kontext

Auftraggeber: „Soll man die Software als Paket kaufen oder eine Lizenz oder nur leasen? Wie kann man die Lizenzen von der Weiterentwicklung entkoppeln?“

## Optionen

| Option | Bewertung |
| --- | --- |
| Kauflizenz mit Einmalpreis | deckt laufende KI- und Kassenkosten nicht |
| Kauf plus Wartungsvertrag | nur als Ausnahme für große Ketten |
| Preis pro Mitarbeitenden | häufige Kritik, bestraft Wachstum |
| Provision pro Buchung | der größte Ärger am Markt |
| Abo pro Standort, monatlich kündbar | gewählt |

## Entscheidung

Abo pro Standort, monatlich kündbar, alle Updates inklusive: Start ca. 99 €, Spa ca. 199 €, Spa Plus ca. 279 € je Standort und Monat netto; Telefon-KI als Zusatz. Funktionen schaltet eine Berechtigung aus dem Vertrag frei, nicht ein Programmstand. Die Kasse wird nie gesperrt, nach Vertragsende folgt ein Nur-Lesen-Modus mit Export.

## Begründung

Kassenregeln ändern sich laufend, eine gekaufte Version ohne Updates würde rechtswidrig. KI, SMS und Hosting kosten jeden Monat. Die Preise sind Arbeitshypothesen für die ersten Gespräche mit 5 bis 10 Spas.

## Folgen und Stand

Die Abo-Stufe je Spa schaltet seit PR #121 die Webmodule frei, der Kern bleibt in jeder Stufe ([ADR 0050](0050-abo-stufe-schaltet-webmodule-frei.md)). Gesetzt wird sie von Hand auf der Betreiber-Seite; ein Vertragssystem, die Telefon-KI als eigener Zusatz und die Stufe je Standort sind offen. Den Nur-Lesen-Modus nach Vertragsende deckt die Sperre ab ([ADR 0039](0039-spa-sperren.md)).
