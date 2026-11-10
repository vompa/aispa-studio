---
art: ablauf
bereich: Zeiten
oberflaeche: verwaltung
rollen: inhaber, manager, leitung
seite: verwaltung
stichwoerter: Zeit korrigieren, Stempelzeit ändern, Gehen vergessen, falsch gestempelt, Abschnitt löschen, Zeit entfernen, Verlauf, Änderungsprotokoll, edit time, correct timesheet, audit log, Stundenliste, Stundenzettel, Monatsliste, Stundenliste drucken, Stundenliste als PDF, Unterschrift, timesheet PDF, print timesheet
---
# Arbeitszeit korrigieren oder entfernen

Falsche oder fehlende Zeiten korrigiert die Leitung in den Zeiten. Jede Änderung braucht einen Grund und bleibt im Verlauf sichtbar.

## Abschnitt ändern {#aendern}

1. In der Windows-App **Verwaltung** › **Team** öffnen und **Zeiten** wählen. Standort und Woche wählen.
2. Auf die Zelle der Person und des Tages klicken.
3. Beim Abschnitt auf **Ändern** klicken. Es öffnet sich **Abschnitt ändern**.
4. **Beginn** und **Ende** anpassen. Bei „Gehen vergessen“ das richtige Ende eintragen.
5. Bei **Grund (wird protokolliert)** den Grund eintragen. Das Feld ist Pflicht; ohne Grund erscheint „Grund angeben.“ und nichts wird gespeichert.
6. Auf **Speichern** klicken oder Strg+S drücken.

Der Abschnitt trägt danach den Zusatz „korrigiert“. Auf der Personenkarte (Reiter **Zeiten**) geht es genauso.

## Abschnitt entfernen {#entfernen}

1. Beim Abschnitt auf **Ändern** klicken.
2. Bei **Grund (wird protokolliert)** den Grund eintragen.
3. Auf **Entfernen** klicken.
4. In der Rückfrage **Abschnitt entfernen?** nochmals auf **Entfernen** klicken. Mit **Abbrechen** bleibt der Abschnitt bestehen.

Die Rückfrage nennt die Zeitspanne und den Grund. Danach steht „Abschnitt entfernt.“ bei den Zeiten des Tages. Der Abschnitt bleibt mit dem Grund im Verlauf sichtbar.

## Verlauf ansehen {#verlauf}

Bei nachgetragenen oder korrigierten Abschnitten steht **Verlauf**. Ein Klick zeigt jede Änderung mit Zeitpunkt, Person, vorherigen Zeiten und Grund; ein zweiter Klick blendet sie wieder aus. Auch das Backoffice sieht den Verlauf.

## Stundenliste drucken {#stundenliste}

Die Stundenliste einer Person für einen Monat gibt es als PDF zum Unterschreiben.

1. In der Windows-App **Verwaltung** › **Team** › **Personen** öffnen, die Person wählen und den Reiter **Zeiten** öffnen. In der Übersicht aller Zeiten gibt es den Knopf nicht.
2. Auf **Stundenliste als PDF** klicken. Es erscheint die Karte „Stundenliste“ mit dem Satz „Druckt die Stundenliste für <Monat> <Jahr>.“. Der Monat ist der der gezeigten Woche.
3. Auf **Drucken** oder **PDF speichern** klicken. **Per Mail senden** gibt es hier nicht.

Das PDF `Stundenliste-<Name>-<JJJJ-MM>.pdf` ist A4 hoch. Es zeigt je Tag mit Dienst oder Zeiten Kommen, Gehen, Pause, Ist, Soll und Saldo, unten die Monatssumme und zwei Unterschriftenzeilen („Unterschrift <Name>“, „Unterschrift Leitung“). Tage ohne Dienst und ohne Zeiten sind nicht aufgeführt. Die Zahlen sind dieselben wie in der Zeitenmaske.

Drucken darf die Stundenliste, wer die Zeiten lesen darf, auch das Backoffice; sonst nennt der Hinweis am Knopf den Grund. Wie die Knöpfe arbeiten: [Dokumente drucken, speichern und senden](hilfe:dokumente-drucken-speichern-senden).

## Gut zu wissen {#hinweise}

- Ändern lassen sich nur Abschnitte am gewählten Standort. Abschnitte an einem anderen Standort tragen dessen Namen und sind nur lesbar; dafür dort den Standort wählen.
- Alle Uhrzeiten gelten in der Zeitzone des gewählten Standorts.
- Liegt das Ende vor dem Beginn, steht unter dem Feld **Ende** „Endet am Folgetag.“; der Abschnitt läuft dann über Mitternacht.
- Es gelten dieselben Regeln wie beim Nachtragen: [Arbeitszeit nachtragen](hilfe:zeiten-nachtragen#hinweise).
- Läuft bei jemandem seit über 12 Stunden ein Abschnitt, steht in der Zelle „Seit über 12 Stunden eingestempelt: Gehen vergessen?“.
- Wer nur lesen darf (Backoffice), sieht kein **Ändern**.
