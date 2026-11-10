---
art: seite
bereich: Dienstplan
oberflaeche: verwaltung
rollen: inhaber, manager, leitung, rezeption, therapeut
seite: verwaltung
stichwoerter: Dienstplan, Schichtplan, Wochenplan, Einsatzplan, wer arbeitet wann, Schichten, Urlaub, Krankenstand, freie Tage, Anträge, Export, Excel, CSV, Dienstplan exportieren, Dienstplan drucken, roster, shift plan, schedule, rota
---
# Seite „Dienstplan“

Der Dienstplan zeigt für einen Standort und eine Woche, wer wann arbeitet. Grundlage ist die Wochenarbeitszeit aus dem Team; einzelne Tage lassen sich abweichend planen, auch als Entwurf, der erst mit dem Veröffentlichen gilt. Kalender und Buchung richten sich nach dem veröffentlichten Stand.

In der Windows-App gibt es keinen eigenen Bereich „Dienstplan“ mehr. Er steht in **Verwaltung** › **Team**:

- Die Ansicht **Wochenplan** zeigt alle Mitarbeitenden einer Woche.
- Die Personenkarte hat den Reiter **Dienstplan und Abwesenheit** mit der Woche einer Person.
- Die Ansicht **Anträge** listet die offenen Abwesenheitsanträge.

Über der Fläche schaltet eine Leiste zwischen **Personen**, **Wochenplan**, **Zeiten** und **Anträge** um. Sie zeigt nur, was die eigene Rolle darf. Die Rezeption und Therapeutinnen sehen nur den **Wochenplan**, ohne Personenliste.

Sehen dürfen den Plan alle Rollen, auch das Backoffice. Planen, Veröffentlichen, Verwerfen und Anträge entscheiden dürfen nur Inhaber, Manager und Standortleitung (die Standortleitung an ihren eigenen Standorten). Wer nur sehen darf, kann keine Zelle anklicken.

## Aufbau {#aufbau}

- Oben die Auswahl des Standorts.
- Daneben **‹** (vorherige Woche), **Diese Woche** und **›** (nächste Woche), dazu die Überschrift „Woche ab …“.
- Gibt es Entwürfe in der angezeigten Woche, steht oben ein Band: „Entwurf: 2 Änderungen nicht veröffentlicht“ mit **Veröffentlichen** und **Verwerfen**, darunter „Zuletzt veröffentlicht …“. Die Karte einer Person zeigt das Band nicht.
- **Die Tabelle**: eine Zeile je Person mit Farbpunkt, eine Spalte je Tag. Beim Namen stehen die geplanten Stunden der Woche gegen die Wochenarbeitszeit, z. B. „8 / 40 h“; ohne Wochenarbeitszeit nur die Stunden. Die Spalte von heute ist hervorgehoben.
- **Dienste** sind Chips in der Farbe der Person mit der Zeit, z. B. „09:00–17:00“; Dienste an einem anderen Standort tragen dessen Namen. Ein fett gesetzter Chip ist für diesen Tag geplant, ein normaler folgt der Wochenarbeitszeit. Ein Tag ohne Dienst zeigt ein schwaches „frei“.
- **Abwesenheiten** sind ein Balken über die Tage, z. B. „Urlaub Di–Fr“, ebenso „Krank“, „Frei“ und „Schulung“. Jeder Tag unter dem Balken bleibt einzeln anklickbar.
- Ein **Entwurf** trägt einen gestrichelten Rand um die Zelle.
- Unter der Tabelle zählt die Zeile **Besetzung**, wie viele Personen pro Tag am Standort im Dienst sind. Auf der Karte einer Person fehlt sie.
- Eine Warnung in einer Zelle oder bei den Stunden weist auf Arbeitszeitgrenzen hin, z. B. zu kurze Ruhezeit, zu kurze Pause oder zu viele Stunden. Die Grenzen richten sich nach dem Land des Spas.
- Ein Klick auf eine Zelle öffnet direkt an der Zelle das Feld zum Planen dieses Tages. Für eine Person weiter unten zuerst **Schließen**.
- Ein Klick auf den Namen einer Person wechselt zu **Personen** und öffnet ihre Karte im Reiter **Dienstplan und Abwesenheit**. Das geht nur mit dem Recht, Personen zu sehen.
- **Exportieren** speichert den Dienstplan der angezeigten Woche (Personen als Zeilen, Tage als Spalten, mit Entwürfen) und die Abwesenheiten dieser Woche als Excel oder CSV. Den Knopf sehen nur Inhaberin und Geschäftsführung.

![Wochenplan in der Windows-App](images/app/verwaltung-team-wochenplan.png)

Wer an einem Tag ungespeicherte Änderungen hat und die Woche, den Standort oder die Ansicht wechselt, wird gefragt: „Änderungen speichern?“ mit **Speichern**, **Verwerfen** und **Abbrechen**.

## Wer in der Tabelle steht {#personen}

Im Wochenplan alle aktiven Personen, die am Standort eine Wochenarbeitszeit haben oder dort im Dienstplan eingeteilt sind. Wer am Standort noch nie eingeteilt war, erscheint erst, wenn man ihm dort eine Wochenarbeitszeit gibt ([Person im Team ändern](hilfe:stammdaten-person-anlegen#aendern)) oder ihn im Dienstplan eines anderen Standorts für diesen Standort einteilt.

Auf der Personenkarte steht immer nur diese Person, auch ohne Dienst am gewählten Standort. So lässt sich dort der erste Dienst planen. Dienste an anderen Standorten bleiben sichtbar.

## Abläufe {#ablaeufe}

- [Dienst für einen Tag planen oder ändern](hilfe:dienstplan-tag-planen)
- [Urlaub, Krankenstand oder freien Tag eintragen](hilfe:dienstplan-abwesenheit-eintragen)
- [Dienstplan veröffentlichen oder Entwürfe verwerfen](hilfe:dienstplan-veroeffentlichen)
- [Ersatz für die Termine einer ausgefallenen Person finden](hilfe:dienstplan-ersatz-vorschlagen)
- [Abwesenheitsantrag genehmigen oder ablehnen](hilfe:dienstplan-antrag-entscheiden)
- [Verlauf des Dienstplans einer Person ansehen](hilfe:dienstplan-verlauf) (Inhaber und Manager)

Eine KI, die Dienstpläne vorschlägt, gibt es derzeit nicht.
