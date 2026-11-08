---
art: ablauf
aufbau: schritte
bereich: Kalender
oberflaeche: windows-app
rollen: rezeption, leitung, inhaber, manager
seite: kalender
stichwoerter: stornieren, absagen, Absage, Termin löschen, streichen, canceln, Storno, Termin entfernen, nicht erschienen, No-Show, noshow, nicht gekommen, versetzt, Grund, ganze Buchung stornieren, cancel, delete appointment
---
# Termin stornieren oder „Nicht erschienen“

Sagt ein Gast ab, wird der Termin storniert: Person, Raum und Gerät sind sofort wieder frei, und der Termin verschwindet aus dem Kalender. Kommt der Gast einfach nicht, ist das kein Storno, sondern der Status „Nicht erschienen“.

## Voraussetzungen {#voraussetzungen}

- Rezeption, Standortleitung oder Inhaber. Therapeut:innen können nicht stornieren.
- Der Termin ist „Gebucht“ oder „Eingecheckt“; bei „Erledigt“ oder „Nicht erschienen“ geht es nicht mehr.
- Ohne Verbindung wird ein einzelner Storno vorgemerkt und beim Abgleich gesendet, siehe [Arbeiten ohne Verbindung](hilfe:kalender-offline).

## Schritte {#schritte}

### 1. Termin markieren

Den Termin anklicken (markieren). Alternativ im Panel auf **Termin stornieren …** klicken (rot, unter **Verschieben …**; es braucht kein Einchecken vorher), oder per Rechtsklick **Stornieren …** wählen.

![Kalender mit markiertem Termin von Brigitte Müller](images/app/termin-stornieren-1.png)

### 2. Grund eintragen

**Entf** drücken. Eine Rückfrage „Termin stornieren?“ erscheint. Unter **Grund** den Grund eintragen; vorbelegt ist „An der Rezeption storniert“.

Liegt die Absage innerhalb der Stornofrist des Standorts, steht über dem Feld ein Hinweis: „Absage innerhalb der Stornofrist (bis 21.10. 09:00). Laut Regel fallen 50 % Ausfallgebühr an; belasten kann die Standortleitung.“ Wer die Kassenaufsicht hat, liest stattdessen den Weg „Belasten unter **Ausfallgebühr belasten …** nach dem Stornieren“. Der Grund ist dann mit „Kurzfristig an der Rezeption storniert“ vorbelegt. Frist und Gebühr kommen aus den [Regeln des Standorts](hilfe:standorte#ausfallgebuehr) (Behandlung vor Standort, „nur neue Gäste“); ohne Frist oder außerhalb der Frist gibt es keine Zeile. Belastet wird nie automatisch.

![Rückfrage zum Stornieren mit markiertem Feld Grund](images/app/termin-stornieren-2.png)

### 3. Stornieren

Auf **Stornieren** klicken. **Abbrechen** bricht ab.

![Rückfrage zum Stornieren mit markiertem Knopf Stornieren](images/app/termin-stornieren-3.png)

## Was danach {#was-danach}

Der Termin ist aus dem Kalender verschwunden, die Zeit ist frei. Wer storniert hat und wann, steht im [Verlauf des Termins](hilfe:kalender-termin-verlauf). Die frei gewordene Zeit meldet der Server von selbst passenden Gästen auf der [Warteliste](hilfe:kalender-warteliste). Ein versehentliches Storno lässt sich zurücknehmen, siehe [Stornierung zurücknehmen](hilfe:kalender-termin-stornieren#zuruecknehmen). Eine [Ausfallgebühr](hilfe:standorte#ausfallgebuehr) wird dabei nie automatisch belastet. Hat der Gast online eine Karte hinterlegt, löst die Kassenaufsicht die Gebühr bei „Nicht erschienen“ oder einer späten Absage eigens aus; im Kalender gibt es dafür keinen Knopf.

## Wenn etwas schiefgeht {#wenn-etwas-schiefgeht}

- Kommt der Gast nicht, ist **Nicht erschienen** richtig, nicht **Stornieren**: siehe [Nicht erschienen](hilfe:kalender-termin-stornieren#nicht-erschienen).
- Soll nur ein Teil einer gemeinsamen Buchung entfallen, diesen Termin wie oben einzeln stornieren; die ganze Buchung geht über [Ganze Buchung stornieren](hilfe:kalender-termin-stornieren#ganze-buchung).
- Statt zu stornieren lieber verschieben? Siehe [Termin verschieben](hilfe:kalender-termin-verschieben).



## Stornierung zurücknehmen {#zuruecknehmen}

Gleich nach dem Stornieren steht unten die Meldung „Termin storniert“ mit **Rückgängig**. Der Knopf oder **Strg+Z** bringt den Termin zurück, solange die Meldung steht (8 Sekunden).

Später geht es im Panel des stornierten Termins: **Stornierung zurücknehmen** klicken. Das Panel eines stornierten Termins bleibt nach dem Stornieren offen; bei einer gemeinsamen Buchung öffnet man es über die durchgestrichene Zeile des Termins.

- Der Termin ist danach wieder „Gebucht“, auch wenn er vorher eingecheckt war. Das Programm prüft zuerst, ob Zeit, Person und Raum noch frei sind.
- Ist die Zeit inzwischen belegt, nennt die Rückfrage den Konflikt. Mit **Verschieben …** wählt man Tag und Beginn neu; die Stornierung wird dann zu dieser Zeit zurückgenommen.
- Hat der Gast die Absage wegen einer Schließzeit schon bekommen, fragt die Rückfrage, ob er eine **Korrektur** erhält. Wartet die Nachricht noch, geht sie nie raus.
- Eine Anzahlung, die mit der Absage zurückging, bleibt zurückgezahlt und ist an der Kassa neu zu verlangen.
- Therapeut:innen können nicht stornieren und deshalb auch nichts zurücknehmen.

## Nicht erschienen {#nicht-erschienen}

1. Termin anklicken.
2. Im Panel auf **Mehr** und dann auf **Nicht erschienen** klicken (oder per Rechtsklick **Mehr** › **Nicht erschienen**).

- Geht nur, solange der Termin „Gebucht“ ist.
- Der Termin bleibt sichtbar stehen; wer zweimal oder öfter nicht erschienen ist, bekommt im Kalender das Kennzeichen **!**.
- Mehr dazu unter [Status setzen](hilfe:kalender-status-setzen#nicht-erschienen).

## Ganze Buchung stornieren (Paar, Spa-Tag) {#ganze-buchung}

1. Einen Termin der gemeinsamen Buchung anklicken.
2. Im Kasten **GEMEINSAM GEBUCHT** auf **Ganze Buchung stornieren** klicken.

- Achtung: Hier gibt es keine Rückfrage, alle Termine der Buchung werden sofort storniert (Grund „An der Rezeption storniert (ganze Buchung)“).
- Nur einen Teil stornieren: diesen Termin wie oben einzeln stornieren.

## Hinweise {#hinweise}

- Stornieren dürfen Rezeption, Standortleitung und Inhaber. Therapeut:innen können „Nicht erschienen“ nur bei eigenen Terminen setzen, aber nicht stornieren.
- Ohne Verbindung wird ein einzelnes Storno vorgemerkt und beim Abgleich gesendet. **Ganze Buchung stornieren** braucht eine Verbindung. Siehe [Arbeiten ohne Verbindung](hilfe:kalender-offline).
- Eintritte in Sauna oder Ruheraum storniert man im Panel **Sauna und Ruheraum**, siehe [Sauna-Eintritt](hilfe:kalender-sauna-eintritt#stornieren).
- Statt stornieren lieber verschieben? Siehe [Termin verschieben](hilfe:kalender-termin-verschieben).
