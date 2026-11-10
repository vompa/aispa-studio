# 0094. Proportionen von Masken: Drittel und goldener Schnitt

- **Status:** angenommen
- **Datum:** 10.11.2026
- **Entschieden von:** Auftraggeber (Teilung 3 : 2, Kürzungen der Kundenkarte, Regel für alle Masken), Umsetzung Claude nach ADR 0019

## Kontext

Der Auftraggeber fand die Kundenkarte „etwas überladen“ und wollte prüfen, was der goldene Schnitt und die Drittelregel für Masken zur Eingabe und zur Anzeige leisten. Bis dahin legten [design.md](../design.md) und [masken.md](../gestaltung/masken.md) Werte und Aufbau fest, aber keine Proportionen: Die Kundenkarte teilte „Gut zu wissen“ und „Verlauf“ 1 : 1, obwohl die beiden Inhalte nicht gleich wichtig sind.

Messung am Bild des Auftraggebers (Fenster 1440 px):

| Stelle | Heute | Goldener Schnitt | Drittel |
| --- | --- | --- | --- |
| Liste : Karte | 380 : 940 (0,29) | 504 : 816 | 440 : 880 |
| Gut zu wissen : Verlauf (Inhalt 890) | 429 : 429 | 550 : 340 | 593 : 297 |
| Kopf : Rumpf der Karte | 185 : 510 (0,36) | 0,38 | 0,33 |
| Warn-Chip „Nicht behandeln: Füße“ | bei 43 % der Kartenhöhe | – | über 33 % |

Dazu kamen Wiederholungen:
- Der Name stand dreimal da: als Name, als Adresszeile und als Anrede.
- Der nächste Termin stand zweimal da: als fette Zeile und unter „Demnächst“.
- „Buchen“ gab es zweimal: als Knopf und als Link.

## Optionen

| Option | Bewertung |
| --- | --- |
| Goldenes Raster für alles (Abstände 8, 13, 21, 34; Spalten 61,8 : 38,2) | Neue Werte außerhalb von design.md 4.1. Die Abstände 8, 12, 20, 32 wachsen schon um 1,5 bis 1,67 und liegen damit nahe an der Fibonacci-Folge; die Feldhöhe 34 ist selbst eine. Verworfen. |
| Strenge Drittel (2 : 1) für Haupt- und Nebeninhalt | Der Verlauf hätte bei 1440 px nur rund 297 px, Zeilen wie „Gesichtsbehandlung Classic bei Lena Huber“ brechen dann öfter um. Verworfen. |
| **3 : 2 für Haupt- und Nebeninhalt, Drittellinie für Warnungen** | 1,5 : 1 ist ein Fibonacci-Paar nahe am goldenen Schnitt (1,618) und lässt dem Nebeninhalt genug Breite. Gewählt. |

## Entscheidung

- **R14** in [masken.md](../gestaltung/masken.md):
  - Haupt- und Nebeninhalt nebeneinander stehen im Verhältnis 3 : 2, gleich breite Spalten nur bei gleichrangigen Inhalten.
  - Warnungen und alles, was vor der Handlung gelesen werden muss, stehen über der oberen Drittellinie.
  - Unter einer Mindestbreite stehen die Spalten untereinander.
  - Innerhalb eines Formulars gelten weiter R3 und R4 (eine Spalte, Breite nach der Eingabe). Neue Abstands- oder Schriftwerte entstehen nicht.
- Umsetzung in der Windows-App mit dem Panel `MainSideColumns` (`src/AispaStudio.Desktop/Controls/MainSideColumns.cs`).
- Kundenkarte (`Views/CustomersView.axaml`):
  - „Gut zu wissen“ und „Verlauf“ stehen 3 : 2, unter 720 untereinander.
  - Ein Warnstreifen (Hinweis „Warnung“, design.md 5.9) unter dem Kopf bündelt „Nicht behandeln: …“ und die Ausfälle („1× nicht erschienen · 1 späte Absage“). Der Warn-Chip bei den Wünschen entfällt.
  - Die Anrede in Nachrichten steht als Tooltip am Namen statt als eigene Zeile.
  - Die Zeile „Nächster Termin“ und der Link „buchen“ entfallen. Der Verlauf zeigt unter „Demnächst“ die Termine oder „Kein Termin geplant.“, gebucht wird mit „Termin buchen“ im Kopf.

## Begründung

- Für den goldenen Schnitt gibt es keine belastbaren Belege, dass er Bedienfehler senkt. Er ist eine Regel für ruhige Proportionen.
- Die Drittelregel kommt aus der Fotografie. Auf Oberflächen übertragen heißt sie: Der Blick landet zuerst links oben, also gehört dorthin, was vor der Behandlung gelesen werden muss.
- Beide Regeln wirken deshalb nur auf Verhältnisse und Reihenfolge, nicht auf Werte. Damit bleibt design.md die einzige Quelle für Größen.
- Die Kürzungen nehmen Wiederholungen weg, ohne Information zu verlieren: Die Anrede steht im Tooltip, der nächste Termin im Verlauf.

## Folgen und Stand

- Die neue Regel gilt für neue und geänderte Masken. Altbestand wird nur bei einer Änderung angepasst (masken.md Abschnitt 8).
- Die Kundenkarte ist umgestellt. Prüfung über Headless-Tests in `ViewTests.CustomerCardLayout.cs`:
  - Verhältnis 1,5 bei 1440 px,
  - Spalten untereinander bei Mindestbreite,
  - Warnstreifen im oberen Drittel.
- Nicht geprüft ist, wie die Teilung bei 125 und 150 Prozent Windows-Skalierung wirkt. Die Schwelle 720 ist in DIP gesetzt und skaliert mit.
- Das Web (Dashboard, Handy-Apps) hat keine Karte mit Haupt- und Nebeninhalt. Dort gilt R14, sobald eine entsteht.

## Quellen

- Rückmeldung des Auftraggebers vom 10.11.2026 mit Bild der Kundenkarte
- [masken.md](../gestaltung/masken.md) R1, R3, R4, R14; [design.md](../design.md) 4.1, 5.5, 5.9
- [ADR 0067](0067-masken-und-meldungen-nach-material-3-und-fluent-2.md), [ADR 0069](0069-fluent-2-als-grundlage-der-windows-app.md)
