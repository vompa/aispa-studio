---
art: ablauf
bereich: Kalender
oberflaeche: windows-app
rollen: rezeption, leitung, inhaber, manager
seite: kalender
stichwoerter: Pause, Mittagspause, gesetzliche Pause, Ruhepause, Sperrzeit, sperren, blockieren, Blocker, Arzttermin, Besprechung, Wartung, Notiz, Zettel, Rückruf, Tagesnotiz, Notizen heute, Terminnotiz, Notiz zum Termin, Abwesenheit, Urlaub, abwesend, abhaken, break, block time, note, reminder, day note
---
# Pausen, Sperrzeiten und Notizen

Pausen und Sperrzeiten belegen eine Person bzw. einen Raum, sodass dort nichts gebucht werden kann. Soll der ganze Standort zu sein (Betriebsurlaub, Schaden), legt man stattdessen eine [Schließzeit](hilfe:schliesszeiten) an. Notizen belegen nichts; sie erinnern an etwas und lassen sich abhaken. Alles wird im Panel rechts eingetragen, das sich per Rechtsklick oder Taste an der passenden Stelle öffnet.

## Art wechseln {#art-wechseln}

Termin, Paar oder Spa-Tag, Pause, Sperrzeit, Notiz und Sauna-Eintritt teilen sich ein Panel. Oben steht eine Leiste **Termin · Paar/Spa · Pause · Sperrzeit · Notiz · Sauna**; ein Klick darauf oder **Alt+1** bis **Alt+6** (in dieser Reihenfolge) wechselt die Art, auch mitten im Tippen.

- Beginn und Spalte (Person bzw. Raum) bleiben stehen.
- Die [Vorschau](hilfe:kalender-termin-buchen#vorschau) in der Spalte wechselt mit: bei **Pause** und **Sperrzeit** von Beginn bis Ende, bei **Notiz** 15 Minuten (oder bis **Bis**), mit „Pause“, dem Grund bzw. dem Notiztext. Eine Pause oder Sperrzeit über belegter Zeit steht in Warnfarbe; eine Notiz belegt nichts.
- In der [7-Tage-Ansicht](hilfe:kalender-sieben-tage#buchen) gehört der Eintrag zum Tag der Spalte, in die geklickt wurde.
- Was die neue Art als Feld hat, nimmt sie mit. Von **Termin** zu **Paar/Spa**: der Gast wird Gast 1 mit der gewählten Behandlung, dazu Datum, Beginn (unter den gemeinsamen Zeiten ist diese Uhrzeit gewählt, wenn es sie gibt) und Notiz. Zu **Sauna**: Gast, Datum, Beginn als **Von** und Notiz, eine Person. Zu **Pause** oder **Sperrzeit**: Beginn und, sobald ein Gast gewählt ist, die Länge der Behandlung. Zu **Notiz**: Beginn und Notiztext. Von **Paar/Spa** zu **Termin**: Gast 1 mit seiner Behandlung, Datum, Beginn und Notiz.
- Wer zu einer Art zurückwechselt, findet sie so vor, wie er sie verlassen hat, solange das Panel offen ist.
- Ein getippter Text bleibt zwischen **Notiz** und **Sperrzeit** erhalten (eine Pause braucht keinen Text; er ist danach wieder da). Die Notiz zu einem Termin wird nicht zum Grund einer Sperrzeit.
- In der Raumansicht fehlt **Pause**; ohne Bereiche am Standort fehlt **Sauna**.
- Die Tasten **N**, **P**, **B**, **Umschalt+B**, **,** und **S** öffnen dasselbe Panel mit der jeweiligen Art; ist es schon offen, wechseln sie nur die Art.

![Wechsel von Notiz zu Sperrzeit: der Text bleibt stehen](images/app/kalender-eintrag-umschalten.png)

## Pause eintragen {#pause}

1. In der Ansicht **Team** mit der rechten Maustaste in die Spalte der Person auf die gewünschte Uhrzeit klicken, dann **Pause hier** wählen (z. B. „Pause hier (30 Min.)“). Oder den Mauszeiger auf die Stelle halten und **Umschalt+B** drücken.
2. Im Panel **Pause** **Beginn** und **Ende** prüfen.
3. Auf **Pause eintragen** klicken. Die Pause steht sofort im Kalender; unten erscheint „Pause eingetragen.“

- Die Länge ist die gesetzliche Pause dieses Tages, sonst 30 Minuten, höchstens so lang wie die freie Zeit bis zum nächsten Eintrag. Die Pause beginnt an der Viertelstunde unter dem Mauszeiger.
- Ein Klick auf eine kurze Lücke öffnet **Pause** von selbst, dann mit der ganzen Länge der Lücke, siehe [Vorschlag nach freier Zeit](hilfe:kalender-termin-buchen#vorschlag).
- Verlangt das Arbeitszeitrecht an dem Tag eine Pause, heißt sie im Kalender „Pause, gesetzlich“.
- Bleibt für die gesetzliche Pause keine Zeit, steht ein Warnsymbol im Spaltenkopf und in den [Meldungen](hilfe:kalender-meldungen) eine Warnung mit **Pause eintragen**.
- Ist die Zeit schon belegt, steht eine [Konfliktmeldung](hilfe:kalender-termin-buchen#konflikt) im Panel.

## Sperrzeit eintragen {#sperrzeit}

Für alles, was eine Person oder einen Raum blockiert, z. B. Arzt, Besprechung, Wartung.

1. Rechtsklick an der Stelle, dann **Sperrzeit hier**. Oder den Mauszeiger auf die Stelle halten und **B** drücken (ohne Mauszeiger über dem Kalender: ab jetzt in der ersten Spalte).
2. Im Panel **Sperrzeit** **Beginn** und **Ende** prüfen (vorbelegt eine Stunde, bei weniger freier Zeit bis zum nächsten Eintrag).
3. Unter **Grund** eintragen, z. B. „Arzt“.
4. Auf **Sperren** klicken.

- Liegen in der Zeit schon Termine oder Sperrzeiten, lehnt der Server ab. Das Panel zeigt dann „Zeitraum ist belegt.“ mit einer Zeile je Belegung in Ortszeit des Standorts („Termin · 09:40–10:50 · Clara Fischer“, „Sperrzeit · 10:50–12:10 · Mittagspause“); ein Klick auf eine Terminzeile markiert den Termin im Kalender. Diese Termine zuerst verschieben oder einen anderen Zeitraum wählen.
- Ein Grund, der mit „Pause“ beginnt, zählt als Pause.
- Sperrzeiten gehen in Personen- und in Raumspalten.

## Pause oder Notiz länger oder kürzer ziehen {#ziehen}

Eine Pause und eine Notiz mit Uhrzeit lassen sich direkt im Kalender in der Höhe ändern, wie das Verschieben eines Termins.

1. Den Mauszeiger an den oberen oder unteren Rand der Pause bzw. des Zettels führen. Der Zeiger wird zum Doppelpfeil.
2. Den Rand nach oben oder unten ziehen. Eine Vorschau zeigt die neue Zeit, z. B. „14:00–15:00 · Notiz“; sie rastet auf 5 Minuten ein, und der andere Rand bleibt stehen.
3. Loslassen. Unten erscheint „Pause geändert“ bzw. „Notiz geändert“.

- Am oberen Rand ändert sich der Beginn, am unteren das Ende (bei der Notiz **Bis**). Kürzer als 5 Minuten wird der Balken nicht.
- Überschneidet die neue Zeit der Pause einen Termin oder eine Sperrzeit, lehnt der Server ab: die Pause bleibt, wie sie war, und über dem Kalender steht „Nicht geändert“ mit der Belegung. Eine Notiz belegt nichts und lässt sich überall hin ziehen.
- **Esc** oder Hinausfahren aus dem Kalender bricht das Ziehen ab. Ein Klick in die Mitte eines Zettels öffnet ihn weiter zum Bearbeiten.
- Andere Sperrzeiten (z. B. „Arzt“) haben keine ziehbaren Ränder; ihre Zeit ändert man durch Löschen und neu Eintragen.

## Pause oder Sperrzeit löschen {#loeschen}

Rechtsklick auf die Pause bzw. Sperrzeit, dann **Pause löschen** bzw. **Sperrzeit löschen**. Eine Rückfrage „Pause löschen?“ bzw. „Sperrzeit löschen?“ nennt die Zeit; **Löschen** bestätigt, **Abbrechen** lässt alles, wie es ist.

## Notiz an einer Uhrzeit {#notiz}

1. Rechtsklick an der Stelle, dann **Notiz hier**. Oder den Mauszeiger auf die Stelle halten und **,** (Komma) drücken.
2. Im Panel **Notiz** die **Uhrzeit** prüfen und den Text eintragen, z. B. „Herr Lang ruft zurück“. Unter **Bis** steht ein Ende 15 Minuten später vorgeschlagen; es lässt sich ändern oder leeren.
3. Auf **Notiz speichern** klicken.

- Die Notiz steht als kleiner Zettel mit Uhrzeit in der Spalte. Mit **Bis** reicht der gelbe Zettel von Beginn bis Ende (Text oben, mindestens 18 Pixel hoch) und zeigt „10:00–10:30 Herr Lang ruft zurück“; ohne Ende steht er nur zu seiner Uhrzeit. Das Ende muss nach dem Beginn liegen. Auch mit Ende belegt die Notiz nichts.
- Der Zettel steht **neben** Terminen und Sperrzeiten, nie darüber: Überschneidet er sich zeitlich mit einem Termin, bekommt er eine schmale Bahn rechts (ein Drittel der Spalte, mindestens 72 Pixel; längerer Text endet mit „…“, der volle Text steht im Hinweis), der Termin den Rest. Ohne Überschneidung hat er die volle Breite.
- Ein Klick auf den Zettel öffnet ihn zum Bearbeiten, wie bei einem Termin: Uhrzeit, Bis, Text und das Kästchen **Erledigt**; **Speichern** übernimmt alles. Ein erledigter Zettel bleibt blass stehen. Rechtsklick bietet **Details öffnen**, **Erledigt** bzw. **Wieder offen** und **Notiz löschen**.

![Termin links, Notiz mit Ende rechts daneben](images/app/kalender-notiz-neben-termin.png)

![Pause, Notiz und krank gemeldete Person](images/app/kalender-tagesleiste.png)

## Tagesnotiz {#tagesnotiz}

Eine Notiz für den ganzen Tag am Standort, z. B. „Lieferung Öle um 11“. Sie steht in der Seitenleiste **Meldungen** ganz oben in der Gruppe **Notizen heute**.

1. **.** (Punkt) drücken, oder im Menü neben **Termin anlegen** **Tagesnotiz** wählen, oder Rechtsklick auf eine freie Stelle und **Tagesnotiz**, oder in **Meldungen** bei **Notizen heute** auf **+ Notiz** klicken. Die Seitenleiste geht auf, und das Notizfeld hat den Fokus.
2. Text eintragen (höchstens 500 Zeichen, der Zähler unter dem Feld zählt mit) und **Notiz speichern** klicken oder **Strg+Enter** drücken. **Esc** stellt den Text zurück, ein zweites **Esc** schließt das Feld.

- Alle am Standort sehen die Notiz. Das Kästchen davor hakt sie ab (der Text steht dann durchgestrichen und blass), der Stift ändert sie, der Papierkorb löscht sie nach einer Rückfrage. Ändern und Löschen sind gesperrt, solange ein Notizfeld offen ist.
- Ohne Notizen zeigt die Gruppe nur **+ Notiz**. Wer nur ansehen darf, sieht die Gruppe nur, wenn es Notizen gibt.
- Notizen an einer Uhrzeit stehen als Zettel in der Spalte, nicht in dieser Gruppe.

## Notizen heute und Meldungen {#tagesleiste}

Über dem Kalender steht kein Band **Heute** mehr. Stattdessen:

- **Notizen heute** steht ganz oben in der Seitenleiste [Meldungen](hilfe:kalender-meldungen#notizen). Gibt es Tagesnotizen, trägt die Schaltfläche **Meldungen** in der Kopfzeile neben der Zahl einen kleinen Notiz-Punkt; die Zahl zählt weiter nur Meldungen, Notizen sind keine. Ein Filter auf eine Person oder einen Raum blendet die Gruppe aus.
- Wer heute nicht da ist, zeigen die Spalten (mit **Alle Mitarbeitenden** samt Grund). Hat eine abwesende Person offene Termine, steht zusätzlich die Meldung **Abwesenheit** in den Meldungen.
- Die **Meldung „Pause“** in der Seitenleiste [Meldungen](hilfe:kalender-meldungen), wenn für die gesetzliche Pause keine Zeit bleibt. Sie lässt sich nicht abhaken und verschwindet, wenn die Pause eingetragen ist; **Wegklicken** blendet sie bis zum nächsten Auslöser aus. **Pause eintragen** an der Karte öffnet das Fenster. Siehe [Meldungen und Notizen](hilfe:seite-kalender#tagesleiste).

## Notiz zu einem Termin {#termin-notiz}

Eine Notiz gehört zu genau einem Termin, z. B. „Lavendelöl, wenig Gespräch, Fokus Nacken“. Sie ist nicht die Notiz in den Kundendaten.

1. Den Termin anklicken. Unter den Terminangaben im Panel steht **Notiz hinzufügen**.
2. Den Text in das Feld **Notiz zum Termin** schreiben (höchstens 500 Zeichen) und **Notiz speichern** klicken oder **Strg+Enter** drücken. **Esc** stellt den Text zurück, ein zweites **Esc** bricht ab.

![Notiz im Termin-Panel](images/app/termin-notiz-hinweis.png)

- Mit Notiz steht sie als gelbe Fläche unter den Terminangaben; der **Stift** daneben ändert sie. Ein leeres Feld entfernt die Notiz beim Speichern.
- Die Notiz steht auch in der Hover-Karte des Termins. Sie lässt sich bei jedem Status nachtragen, auch nach dem Einchecken oder Erledigen.
- Schreiben dürfen Rezeption, Standortleitung und Inhaber (Recht zu buchen); Therapeut:innen sehen die Notiz.
- Notiz und **Zeiten dieses Termins ändern** schließen sich aus: solange eines offen ist, ist das andere gesperrt.

## Abwesenheiten {#abwesenheiten}

Urlaub, Schulung und freie Tage kommen aus dem Dienstplan der Verwaltung; im Kalender werden sie nur angezeigt: die Spalte ist getönt, der Kopf zeigt z. B. „Urlaub bis 23.10.“. Hat eine abwesende Person trotzdem Termine, steht „… Termine umbuchen“ im Kopf. Im Kalender selbst lässt sich nur eine Krankmeldung eintragen, siehe [Krank melden und Ausfall-Assistent](hilfe:kalender-krankmeldung).

## Hinweise {#hinweise}

- Eintragen, abhaken und löschen dürfen Rezeption, Standortleitung und Inhaber. Therapeut:innen sehen Pausen, Notizen, die **Notizen heute** und die Meldungen.
- Pausen, Sperrzeiten und Notizen brauchen eine Verbindung zum Server; ohne Verbindung kommt eine Fehlermeldung.
