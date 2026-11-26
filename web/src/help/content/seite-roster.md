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
- Die Personenkarte zeigt im Bereich **Arbeitszeit** unter **Dienstplan und Abwesenheit** die Woche einer Person.
- Die Schaltfläche **Anträge** im Wochenplan öffnet die offenen Abwesenheitsanträge als Seitenleiste.

Über der Fläche schaltet eine Leiste zwischen **Personen**, **Wochenplan**, **Zeiten**, **Lohn** und **Rechte und Zuständigkeiten** um. Sie zeigt nur, was die eigene Rolle darf. Die Rezeption und Therapeutinnen sehen nur den **Wochenplan**, ohne Personenliste.

Sehen dürfen den Plan alle Rollen, auch das Backoffice. Planen, Veröffentlichen, Verwerfen und Anträge entscheiden dürfen nur Inhaber, Manager und Standortleitung (die Standortleitung an ihren eigenen Standorten). Wer nur sehen darf, kann keine Zelle anklicken.

## Aufbau {#aufbau}

- Oben die Auswahl des Standorts.
- Daneben **‹** (vorherige Woche), **Diese Woche** und **›** (nächste Woche), dazu die Überschrift „Woche ab …“.
- Gibt es Entwürfe in der angezeigten Woche, steht oben ein Band: „Entwurf: 2 Änderungen nicht veröffentlicht“ mit **Veröffentlichen** und **Verwerfen**, darunter „Zuletzt veröffentlicht …“. Die Karte einer Person zeigt das Band nicht.
- **Die Tabelle**: eine Zeile je Person mit Farbpunkt, eine Spalte je Tag. Beim Namen stehen die geplanten Stunden der Woche gegen die Wochenarbeitszeit, z. B. „8 / 40 h“; ohne Wochenarbeitszeit nur die Stunden. Die Spalte von heute trägt eine helle Fläche vom Kopf bis zum Ende der Besetzung über den Tag; ihr Kopf heißt „Heute · Do. 26.11.“.
- **Dienste** sind graue Chips mit der Zeit, z. B. „09:00–17:00“. Die Farbe der Person steht nur als Punkt vor dem Namen. Ein Dienst an einem anderen Standort hat keine Fläche und nennt in einer zweiten Zeile mit Ortssymbol den Standort; ein langer Name wird gekürzt, die Maus zeigt ihn ganz. Ein fett gesetzter Chip ist für diesen Tag geplant, ein normaler folgt der Wochenarbeitszeit. Ein Tag ohne Dienst zeigt ein schwaches „frei“.
- **Abwesenheiten** sind ein Balken über die Tage, z. B. „Urlaub Di–Fr“, ebenso „Krank“, „Frei“ und „Schulung“. Jeder Tag unter dem Balken bleibt einzeln anklickbar.
- Ein **Entwurf** trägt einen gestrichelten Rand um die Zelle.
- Unter der Tabelle zählt die Zeile **Besetzung**, wie viele Personen pro Tag am Standort im Dienst sind. Darunter zeigt **Besetzung über den Tag**, in welchen Stunden wie viele da sind, siehe [Besetzung über den Tag](#besetzung). Auf der Karte einer Person fehlt beides.
- Eine Warnung in einer Zelle oder bei den Stunden weist auf Arbeitszeitgrenzen hin, z. B. zu kurze Ruhezeit, zu kurze Pause oder zu viele Stunden. Die Grenzen richten sich nach dem Land des Spas.
- Ein Klick auf eine Zelle öffnet direkt an der Zelle das Feld zum Planen dieses Tages. Für eine Person weiter unten zuerst **Schließen**.
- Ein Klick auf den Namen einer Person wechselt zu **Personen** und öffnet ihre Karte im Bereich **Arbeitszeit**, beim Dienstplan. Das geht nur mit dem Recht, Personen zu sehen.
- **Ausgabe** am rechten Rand der Kopfzeile, neben **Anträge** und dem Fragezeichen, ist ein Menü, das beim Blättern an seinem Platz bleibt. Die Gruppe „Liste · mit Entwürfen“ speichert den Dienstplan der angezeigten Woche (Tage als Zeilen, Personen als Spalten, mit Entwürfen; rechts die Besetzung je Tag, unten die Stunden je Person) und die Abwesenheiten dieser Woche als Excel oder CSV; sie sehen nur Inhaberin und Geschäftsführung. Die Gruppe „Aushang · veröffentlichter Stand“ druckt den Aushang oder speichert ihn als PDF, siehe [Dienstplan veröffentlichen](hilfe:dienstplan-veroeffentlichen#aushang). Die Bestätigung „Gespeichert: …“ erscheint als kurze Meldung und verschiebt nichts. Wer keine der beiden Gruppen sehen darf, sieht den Knopf nicht.

![Wochenplan in der Windows-App](images/app/verwaltung-team-wochenplan.png)

## Besetzung über den Tag {#besetzung}

Die Zeile **Besetzung** nennt nur die Summe je Tag. Zwei Dienste von 10 bis 18 Uhr und von 11 bis 19 Uhr ergeben dort „2“, obwohl von 10 bis 11 Uhr und von 18 bis 19 Uhr nur eine Person da ist. Unter der Zeile **Besetzung** zeigt der Abschnitt **Besetzung über den Tag**, wann am Tag der Standort dünn besetzt ist. Er steht nur im Wochenplan, nicht auf der Karte einer Person.

- Die Matrix hat dieselben sieben Tagesspalten wie der Plan und eine Zeile je volle Stunde, von der frühesten Öffnung bis zum spätesten Schluss der Woche. „10–11“ steht für die Stunde von 10:00 bis 11:00 Uhr.
- Die Zahl in einer Zelle nennt, wie viele Personen in dieser Stunde am gewählten Standort im Dienst sind. Eine Person zählt, wenn sie in der Stunde mindestens 30 Minuten Dienst hat. Entwürfe zählen mit, wenn der Plan sie zeigt. Abwesenheiten und Dienste an einem anderen Standort zählen nicht. Ein Dienst über Mitternacht zählt nicht mit.
- Ein Warndreieck mit „0“ auf gelber Fläche kennzeichnet eine **Lücke**: Der Standort ist in dieser Stunde geöffnet, aber niemand hat Dienst.
- Eine graue Zelle ohne Zahl liegt außerhalb der Öffnungszeit, auch an Tagen mit einer Schließzeit wie Betriebsurlaub oder Feiertag. Dort ist keine Lücke.
- Ist der Standort in der ganzen Woche nie geöffnet, steht statt der Matrix ein Satz mit dem Weg zu den Öffnungszeiten ([Öffnungszeiten ändern](hilfe:stammdaten-oeffnungszeiten-aendern)). Ohne Personen im Plan fehlt der Abschnitt.
- Die Zellen sind nicht anklickbar. Ein Screenreader liest je Zelle Tag, Stunde und Besetzung, z. B. „Mi. 25.11., 18 bis 19 Uhr: 1 Person im Dienst“.

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
