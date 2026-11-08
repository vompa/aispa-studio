# 0002. Kalender als Kern, selbst gebaut

- **Status:** angenommen
- **Datum:** 17.10.2026
- **Entschieden von:** Auftraggeber (Schwerpunkt und Eigenbau), Empfehlung Claude

## Kontext

Auftraggeber: „Der klare Fokus liegt auf der Planbarkeit und dem Kalender, der soll die besten Eigenschaften aller Konkurrenten vereinen und natürlich noch eigene bessere enthalten.“ Die besten Bausteine gibt es am Markt nur verteilt (Regeln je Leistung bei Timify, Ressourcen-Pools bei Fresha, Paarlogik bei Trybe, Lückenoptimierung bei Boulevard).

## Optionen

| Option | Bewertung |
| --- | --- |
| Personal-Kalender wie bei Salon-Tools, Räume nur als Notiz | genau die Schwäche der Konkurrenz; Doppelbelegung von Räumen bleibt möglich |
| Fertige Kalender-Komponente kaufen (DevExpress, Syncfusion, Bryntum) | schneller Start, aber Teilressourcen, Paarlogik und Erklärungen müssten trotzdem selbst gebaut werden; Lizenzkosten und Abhängigkeit im Kern |
| Vollautomatische Optimierung ohne Erklärung | die Rezeption vertraut keiner Blackbox |
| Kalender nur im Web, in Windows per WebView2 eingebettet | spart Aufwand, verliert aber den Offline-Betrieb am Empfang |
| Eigener Ressourcen-Kalender | gewählt |

## Entscheidung

Ein selbst gebauter Ressourcen-Kalender belegt pro Termin Person, Raum, Gerät und Kapazität gemeinsam, auch für Teilabschnitte. Die Belegungslogik liegt einmal im Backend; Oberflächen zeichnen nur, geprüft wird immer am Server.

## Begründung

Der Kalender ist der Schwerpunkt des Auftraggebers und die größte Lücke am Markt. Er muss sich von der Konkurrenz abheben, deshalb gehört er nicht in eine fremde Komponente. Der Auftraggeber hat den Eigenbau am 17.10.2026 bestätigt.

## Folgen und Stand

Umgesetzt in PR #3: alle Belegungen in `ResourceBookings`, PostgreSQL-Ausschlussbedingung gegen Überschneidungen (siehe [ADR 0023](0023-doppelbuchungsschutz-in-der-datenbank.md)). Der Kalender wird zweimal gezeichnet (Avalonia und React).
