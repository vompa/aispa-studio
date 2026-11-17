# 0099. Anlegen, Entfernen und leerer Betrieb

- **Status:** angenommen
- **Datum:** 17.11.2026
- **Entschieden von:** Auftraggeber (Auftrag zur Überarbeitung der Verwaltungsmasken, Punkt 10), Umsetzung Claude

## Kontext

Jede Verwaltungsmaske legt Daten anders an und nimmt sie anders heraus. Standorte lassen sich nur anlegen, Räume und Geräte nur aus der Planung nehmen, Qualifikation und Lieferant nicht entfernen, die Behandlung nach „Nicht mehr anbieten“ nicht zurückholen, das Material ohne Schalter in der App führen. Wer ein Spa neu einrichtet, findet keinen durchgehenden Weg; wer sich beim Anlegen vertippt hat, findet keinen Weg zurück. Die Bestandsaufnahme steht im [Masken-Inventar](../umsetzung/masken-inventar.md), Abschnitt „Anlegen und Entfernen“.

Vorgabe des Auftraggebers: ein Muster für alle beweglichen Daten (Standorte, Räume, Personen, Behandlungen, Lieferanten, Lagerorte, Materialien, Geräte, Qualifikationen, Betriebskontakte, darunter die Kategorie „Handwerker und Technik“). „Neu“ steht fest über der Liste, Entfernen im Menü „⋯“ im Kopf der Karte. Löschen nur, wenn nichts daran hängt; sonst nennt der Dialog, was daran hängt, und bietet „Nicht mehr verwenden“ an. Belege und Termine bleiben immer erhalten ([ADR 0010](0010-kassa-compliance-by-design.md)). Aktionen werden nie gelöscht, nur archiviert ([ADR 0071](0071-aktionen-und-rabatte.md)).

## Optionen

| Option | Bewertung |
| --- | --- |
| A: Jede Maske behält ihren eigenen Weg | kein Aufwand, aber die Unterschiede bleiben und jede Maske erfindet ihre Texte neu |
| B: Löschen nur dort, wo es heute schon geht, sonst nichts | zu eng: ein vertipptes Objekt bleibt für immer in der Liste |
| C: Ein Muster, die API prüft und zählt (`deletion-check`), die App zeigt einen gemeinsamen Dialog | gewählt |

## Entscheidung

**1. Anlegen.** „Neu“ steht fest über der Liste, nie als letzter Eintrag darin. Eine leere Liste zeigt den Leerzustand mit dem Knopf „Neu“ als erster Aktion.

**2. Entfernen.** Der Weg liegt im Menü „⋯“ im Kopf der Karte (in Zeilen am Zeilenende), nie als eigener Knopf neben „Speichern“. Das Menü hat höchstens zwei Einträge: „Nicht mehr verwenden“ (umkehrbar, keine Rückfrage nötig, über „Aktiv“ oder „Wieder verwenden“ rückgängig) und „Löschen …“ (rot, mit drei Punkten, weil danach ein Dialog fragt).

**3. Die API prüft, die App zeigt an.** Je Objekt gibt es `GET …/{id}/deletion-check` mit dem gemeinsamen Ergebnis `DeletionCheckDto(CanDelete, Summary, Dependents, Alternative)`: ob sich das Objekt löschen lässt, ein deutscher Satz, die Zählung je abhängiger Art mit fertigem Text („3 künftige Termine“) und der Weg, der statt des Löschens bleibt (`deactivate` = „Nicht mehr verwenden“). Ein echtes `DELETE` gibt es nur, wo Löschen sinnvoll ist (Lieferant, Qualifikation, Lagerort); es antwortet mit 409 und derselben Antwort, wenn etwas daran hängt. Jeder Endpunkt hat dieselbe `RequirePermission` wie das Bearbeiten.

**4. Der gemeinsame Dialog.** Hängt nichts daran: „„X“ löschen?“ mit „Löschen“ (rot, Fokus auf „Abbrechen“). Hängt etwas daran: „„X“ lässt sich nicht löschen“ mit der Liste, was daran hängt, und dem Knopf für den Weg der API („Nicht mehr verwenden“). Gibt es keinen Weg (Hauptlager, Qualifikation in Verwendung), bleibt „Schließen“. Der Dialog gehört der App (`DeletionDialog`, aufbauend auf `ConfirmRequest`, [Meldungen M5](../gestaltung/meldungen.md)), alle Texte der Zählung kommen aus der API.

**5. Was nie gelöscht wird.** Belege, Termine, Lagerbewegungen und Aktionen bleiben erhalten. Räume und Geräte lassen sich nicht löschen, nur aus der Planung nehmen, denn Termine, Kosten, Auswertungen und Sperrzeiten verweisen auf sie. Aktionen werden archiviert und lassen sich zurückholen (ADR 0071). Ein Standort wird nicht gelöscht, sondern ab einem Datum geschlossen (Umsetzung in Punkt 10b).

**6. Leerer Betrieb.** Die Ersteinrichtung ist keine neue Schiene und kein neuer Eintrag in der Navigation, sondern eine Karte „Betrieb einrichten“ oben auf „Firmendaten“, solange etwas fehlt, mit Fortschritt aus der API (Standort, Raum, Behandlung, Person). Jede Verwaltungsseite zeigt bei leeren Daten den Leerzustand mit der ersten Aktion.

**7. Handwerker sind eine Kategorie der Betriebsinfo.** „Handwerker und Technik“ bleibt eine Kategorie der Betriebskontakte (Zahlen › Betriebsinfo), kein eigener Bereich; sie bekommt dasselbe Muster (Archivieren und Zurückholen sind schon da).

## Begründung

Eine Regel, die sich merken lässt: „Neu“ oben, „⋯“ im Kopf, der Dialog sagt, warum etwas nicht geht. Die Prüfung in der API hält die Geschäftsregel an einem Ort (Geschäftslogik nur in der API, [Architektur](../architektur.md)); die App zählt nichts selbst und bleibt auch für das Handy und spätere Oberflächen nutzbar. „Nicht mehr verwenden“ statt Löschen erfüllt ADR 0010: Belege und Termine behalten ihre Bezüge.

## Folgen und Stand

- Umgesetzt in diesem PR (P10a): `deletion-check` für Lagerort, Raum, Gerät, Lieferant und Qualifikation; `DELETE` für Lieferant und Qualifikation; Dialog `DeletionDialog`, Menü `MoreMenuButton`, „Neu“ über der Liste und das Menü bei Lagerorten, Lieferanten, Räumen und Geräten; Handbuch zu diesen Masken.
- Folgt in P10b und den Masken-PRs der anderen Bahnen: Standort schließen (neues Feld, Migration), Schalter am Material, Behandlung und Person im Menü, Betriebskontakt und Aktion mit dem Menü, Leerzustände aller Seiten, Karte „Betrieb einrichten“, Test gegen leere API-Antworten.
- Räume und Geräte lassen sich auch ohne Verweise nicht löschen (siehe Entscheidung 5); ob ein Raum ohne jeden Verweis löschbar werden soll, ist offen.

## Quellen

- [Masken-Inventar](../umsetzung/masken-inventar.md), Abschnitt „Anlegen und Entfernen“
- [ADR 0010](0010-kassa-compliance-by-design.md), [ADR 0071](0071-aktionen-und-rabatte.md), [ADR 0098](0098-verwaltungsmasken-eine-schiene-uebersicht-seitenleiste.md)
- [Meldungen](../gestaltung/meldungen.md), [Masken](../gestaltung/masken.md)

## Nachtrag 17.11.2026 (P10b, Teil a): Standort schließen

- **Schließen ab Datum.** Ein Standort hat das Feld `ClosedFrom` (erster geschlossener Tag in seiner Ortszeit). `POST /api/locations/{id}/close` setzt es, `…/reopen` hebt es auf; beides nur mit `ManageOrganization` wie das Anlegen. Ab dem Tag nimmt der Standort keine Buchung mehr an, auch nicht von der Rezeption (`BookingService`); für Planer, Kalender und Online-Buchung zählt er über `ClosureService.SpansAsync` als dauernde Schließzeit („Sonderschließung“), freie Zeiten verschwinden also auch im Kundenbereich. Vergangene Termine, Belege und Berichte bleiben.
- **Abgelehnt** (400, Feld `closedFrom`) wird das Schließen, solange ab dem Tag noch offene Termine (gebucht oder eingecheckt) am Standort liegen, und beim letzten offenen Standort. Begründung: Ein Standort mit offenen Terminen zu schließen, ohne dass jemand sie absagt, ließe Gäste ohne Nachricht stehen; die Absage-Abläufe der Schließzeiten (ADR 0073) bleiben die Stelle dafür.
- **Löschen** (`DELETE /api/locations/{id}`) geht nur, wenn `GET …/deletion-check` nichts zählt: Räume, Geräte, Termine, Schließzeiten, Kassen, Personen mit Arbeitszeiten und Lagerbewegungen. Das Hauptlager geht mit. Sonst 409 mit der Liste und dem Weg „Schließen ab Datum …“ (`Alternative.Kind = close`); verbleibt eine Verknüpfung, die die Zählung nicht kennt, fängt die Datenbank sie ab und die Antwort ist dieselbe 409. Der letzte Standort lässt sich nie löschen.
- **Betrieb einrichten** (Entscheidung 6) steht: `GET /api/setup/progress` (nur `ManageOrganization`) liefert sechs Schritte (Standort, Öffnungszeiten, Räume, Behandlungen, Team, Kassa) mit `Done`, Hinweis und Sprungziel (`Page`, `Tab`); die Karte oben auf „Firmendaten“ zeigt den Fortschritt und springt je Schritt in die Maske, bis alle erledigt sind. Ein Schritt gilt als erledigt, sobald es einen passenden Eintrag gibt.
- **Material** wird nie gelöscht; „Nicht mehr verwenden“ und „Wieder verwenden“ stehen im Menü „⋯“ der Karte.

## Nachtrag 17.11.2026 (P10c): „Neu“ über allen Listen, Leerzustände, der leere Betrieb

- **Der leere Betrieb** (Entscheidung 6) ist festgelegt: Ein frisch eingerichteter Betrieb hat nach der Ersteinrichtung genau einen Standort (mit seinem Hauptlager) und die Inhaberin, sonst nichts. Die Shell öffnet nie ohne Standort. Ein Test (`ViewTests.EmptyBusiness`) öffnet jede Stelle der Verwaltung gegen diesen Stand und verlangt keinen Absturz, keine Bindungsfehler, keine Fehlerleiste, vorbereitete Antworten für jede Anfrage und je Seite den Knopf zum Anlegen oder einen Leersatz.
- **Geltungsbereich der Regel:** „Neu“ fest über der Liste gilt für Listen von Objekten mit Auswahl und Karte und für die Formulare, die Objekte anlegen (Material, Erlöskonto, Absenderregel). Zeilenlisten innerhalb einer Karte (Arbeitszeiten, Preisregeln, Schritte, Mindestmengen, Ausnahmen) behalten ihr „Hinzufügen“ unter der Liste.
- **Leersätze** stehen als kurzer Satz an der Stelle der Liste; nur wo die ganze Hälfte der Maske sonst leer bliebe (Behandlungen), steht eine Karte mit dem Knopf. Wo eine Liste nie leer sein kann (Erlöskonten, Lagerorte, Standorte), gibt es keinen Satz.
- Bestandsaufnahme aller Seiten: [Masken-Inventar, Abschnitt „Verwaltungsseiten“](../umsetzung/masken-inventar.md). Offen bleiben die Ansicht „Rechte und Zuständigkeiten“ im Team (nur Überschriften, P11) und das Menü „⋯“ für Aktion und Betriebskontakt.
