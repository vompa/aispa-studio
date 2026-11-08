---
art: ablauf
aufbau: schritte
bereich: Kalender
oberflaeche: windows-app
rollen: rezeption, leitung, inhaber, manager
seite: kalender
stichwoerter: Termin buchen, Termin anlegen, Termin eintragen, neuer Termin, buchen, reservieren, Termin vergeben, Walk-in, Laufkundschaft, neue Kundin, neuer Kunde, Kunde anlegen, Folgetermin, nächster Termin, Wiederholungstermin, Anamnese prüfen, Gesundheitsfragebogen, Kontraindikation, book, new appointment, booking, follow-up
---
# Termin buchen

Ein Termin entsteht über **Neuer Termin** im Seitenpanel: Gast, Behandlung, Tag und Beginn wählen, dann **Termin buchen**. Ob Person, Raum und Gerät frei sind, prüft der Server beim Buchen.

## Voraussetzungen {#voraussetzungen}

- Rezeption, Standortleitung oder Inhaber. Therapeut:innen sehen den Kalender, buchen aber nicht.
- Der Kalender zeigt den Tag, an dem der Termin stattfinden soll.
- Der Gast steht in der Kundenkartei; sonst zuerst [neu anlegen](hilfe:kalender-termin-buchen#neuer-gast).
- Ohne Verbindung wird der Termin nur vorgemerkt, siehe [Arbeiten ohne Verbindung](hilfe:kalender-offline).

## Schritte {#schritte}

### 1. Freie Stelle anklicken

Im Kalender auf die freie Stelle in der Spalte der gewünschten Person klicken. Die Viertelstunde unter dem Mauszeiger ist vorher hell markiert. Das Panel rechts öffnet mit der Art, die zur freien Zeit passt, siehe [Vorschlag nach freier Zeit](hilfe:kalender-termin-buchen#vorschlag).

![Kalender mit markierter freier Stelle in der Spalte von Lena Huber](images/app/termin-buchen-1.png)

### 2. Gast wählen

Unter **Kundin oder Kunde** Name, Telefon oder E-Mail eintippen (ab zwei Zeichen) und den Gast in der Liste anklicken. Sein Name steht danach im Suchfeld, darunter zeigt eine Zeile den gewählten Gast; **Ändern** beginnt die Suche neu. Der Kalender schlägt dann unter **Vorschläge** die zuletzt gebuchte Behandlung vor, nennt die gewohnte Person und zeigt Wünsche aus der Kundenkarte (Druck, Duft, Musik …); **Übernehmen** setzt die Behandlung bzw. hängt den Wunsch an die **Notiz**. Nichts davon wird ohne Klick übernommen. Kann die angeklickte Person die zuletzt gebuchte Behandlung nicht, sagt der Vorschlag das („Bei Lena Huber nicht möglich.“) und bietet **Freie Zeit suchen** statt **Übernehmen**. Optional unter **Notiz** einen Wunsch oder Hinweis eintragen. Das Feld ist mehrzeilig: **Enter** beginnt eine neue Zeile, und das Feld wächst mit dem Text, bis es zu scrollen beginnt.

![Panel Neuer Termin mit markierter Gastsuche und gewähltem Gast](images/app/termin-buchen-3.png)

### 3. Behandlung, Tag und Beginn wählen

Im Panel **Neuer Termin** unter **Behandlung** die Behandlung wählen. Die Liste enthält nur, was die angeklickte Person kann (bzw. was im angeklickten Raum möglich ist), darin zuerst, was in die freie Zeit passt; darunter steht z. B. „Nur Behandlungen, die Lena Huber kann.“. Für eine andere Behandlung auf **Freie Zeit suchen** klicken, siehe [Person oder Raum steht fest](hilfe:kalender-termin-buchen#frei-waehlen). Neben **Beginn** steht **Datum**: ein anderer Tag (z. B. `02.03.2027`) lässt den Kalender auf diesen Tag springen, die Vorschau steht dort. **Beginn** prüfen und bei Bedarf überschreiben (z. B. `14:30`; auch `1430`, `14.30` oder `14` gelten; eine Uhrzeit, die es nicht gibt, meldet das Feld unter sich), siehe [Felder ausfüllen](hilfe:kalender-tastenkuerzel#felder).

![Panel Neuer Termin mit markiertem Feld Behandlung](images/app/termin-buchen-2.png)

### 4. Termin buchen

Auf **Termin buchen** klicken.

![Panel Neuer Termin mit markiertem Knopf Termin buchen](images/app/termin-buchen-4.png)

## Was danach {#was-danach}

Der Termin steht sofort im Kalender; Person, Raum und Gerät sind für die Zeit samt Vor- und Nachbereitung belegt. Kommt der Gast, den Termin [einchecken](hilfe:kalender-status-setzen#einchecken). Wiederholt sich der Besuch, hilft der [Folgetermin](hilfe:kalender-termin-buchen#folgetermin). Ändert sich die Zeit, den Termin [verschieben](hilfe:kalender-termin-verschieben).

## Wenn etwas schiefgeht {#wenn-etwas-schiefgeht}

- **Termin buchen** bleibt grau, solange Behandlung, Gast, ein lesbares Datum oder eine gültige Uhrzeit fehlen.
- Ein Klick auf eine andere freie Stelle im Kalender schließt die offene Maske nicht: der angefangene Termin wandert an diese Stelle (Spalte, Tag und Beginn), alle Eingaben bleiben. Verworfen wird er nur mit dem Kreuz, mit **Esc** oder mit **Strg+W**, mit Rückfrage, wenn schon etwas eingetragen ist.
- Ist die Person, der Raum oder ein Gerät belegt, erscheint rot im Panel eine Konfliktmeldung; nichts wird gebucht. Dann eine andere Zeit wählen oder in der Meldung auf **Freie Zeit suchen** klicken. Siehe [Wenn die Zeit belegt ist](hilfe:kalender-termin-buchen#konflikt).
- Liegt die Zeit außerhalb der Öffnungszeit oder nach dem Dienst der Person, steht über dem Knopf eine gelbe Warnung mit **Trotzdem buchen**, siehe [Außerhalb der Öffnungszeit](hilfe:kalender-termin-buchen#ausserhalb).
- Wer nicht weiß, wann etwas frei ist: [Freie Zeit suchen](hilfe:kalender-freie-zeit-suchen). Aus **Neuer Termin** heraus nimmt die Suche Behandlung, Gast und Notiz mit; die Notiz steht danach im gebuchten Termin.

## Außerhalb der Öffnungszeit {#ausserhalb}

Liegt der Termin ganz oder teilweise außerhalb der Öffnungszeit oder in einer Schließzeit, steht schon beim Tippen der Uhrzeit die Warnung „Der Standort hat zu dieser Zeit geschlossen.“. Lehnt der Server ab, weil die Person zu dieser Zeit keinen Dienst hat, nennt die Warnung den Grund, z. B. „Lena Huber ist zu dieser Zeit nicht im Dienst.“.

1. Prüfen, ob der Termin wirklich zu dieser Zeit stattfinden soll.
2. In der Warnung auf **Trotzdem buchen** klicken.

Der Termin wird gespeichert, solange Person, Raum und Gerät frei sind; eine Doppelbelegung bleibt ausgeschlossen. Dieselbe Warnung erscheint, wenn der Termin gegen eine Sonderregelung der Person verstößt. **Termin buchen** allein bucht in diesem Fall nicht. Gäste bekommen solche Zeiten online nie, und auch **Freie Zeit suchen** und die KI-Terminannahme schlagen sie nicht vor. Paar oder Spa-Tag, Sauna-Eintritt und das Verschieben im Kalender bieten **Trotzdem buchen** noch nicht an.



## Vorschau beim Planen {#vorschau}

Solange das Fenster **Neuer Termin** offen ist, steht der geplante Eintrag als gestrichelte Karte in der Spalte: bei der Person bzw. im Raum, zum **Beginn**, so lang wie die gewählte **Behandlung**, mit Vorbereitung und Nachbereitung in Grau darüber und darunter. Der Text der Karte ist die Behandlung. Die Vorschau folgt jeder Änderung: andere Behandlung, anderer Beginn, ein Wechsel der Art (**Pause**, **Sperrzeit**, **Notiz**, mit „Pause“, dem Grund bzw. dem Notiztext). Beim Schließen des Fensters verschwindet sie.

- Belegt die Zeit schon etwas (ein Termin samt Vor- und Nachbereitung, eine Pause oder Sperrzeit), oder passt die Behandlung nicht in die freie Zeit, steht die Karte in **Warnfarbe**, so wie der Hinweis „länger als die freie Zeit“ in der Liste der Behandlungen. Gebucht werden kann trotzdem; ob es geht, entscheidet der Server beim Buchen.
- **Paar/Spa** sucht seine Zeit selbst: die Vorschau steht an der angeklickten Stelle, so lang wie die längste Behandlung des ersten Abschnitts. Die gefundene Zeit zeigt sie nicht.
- Solange die Vorschau steht, zeigt die Maus keine eigene Viertelstunde mehr.
- Die Vorschau eines Termins lässt sich mit der Maus an eine andere Stelle ziehen: zu einer anderen Uhrzeit, in die Spalte einer anderen Person oder in der [7-Tage-Ansicht](hilfe:kalender-sieben-tage) auf einen anderen Tag. Beim Loslassen übernimmt das Fenster Person, Datum und Beginn (auf 5 Minuten gerundet); Gast, Behandlung und Notiz bleiben stehen. **Esc** oder Hinausfahren aus dem Kalender bricht das Ziehen ab.

![Vorschau des geplanten Termins in der Spalte](images/app/termin-vorschau.png)

Steht unter dem Gast **Anamnese prüfen**, trägt die Behandlung dieses Merkmal und der Anamnesebogen des Gastes enthält Angaben. Was darin steht, zeigt das Panel nicht; gebucht werden kann trotzdem. Vor der Behandlung liest die Therapeut:in den Bogen. Welche Behandlungen das Merkmal tragen, steht unter [Behandlungen](hilfe:behandlungen#anamnese-pruefen).

Steht unter dem Gast **Keine Kontaktmöglichkeit**, hat er weder Telefon noch E-Mail in der Kundenkarte: Erinnerung und Zusage per Link gehen dann nicht hinaus. Gebucht werden kann trotzdem; den Kontakt ergänzt man in der [Kundenkarte](hilfe:seite-kunden).

## Vorschlag nach freier Zeit {#vorschlag}

An einem Schließtag und an einem Tag ohne Dienst steht oben im Fenster der Hinweis „Der Standort hat an diesem Tag geschlossen.“. Er sperrt nichts: **Termin buchen** bleibt aktiv, und der Termin wird gespeichert. Gäste bekommen an solchen Tagen online keine Zeiten ([Schließzeiten](hilfe:schliesszeiten)).

Beim Klick auf eine freie Stelle (und mit **N**) prüft der Kalender die Lücke in dieser Spalte: vom Ende des vorigen Termins, der Pause oder Sperrzeit (bzw. vom Dienstbeginn) bis zum Beginn des nächsten (bzw. bis Dienstende oder Schließung). Vor- und Nachbereitung zählen mit.

- Ist die Lücke kürzer als jede Behandlung, die diese Person anbieten kann, öffnet **Pause** mit der Länge der Lücke (auf 5 Minuten abgerundet).
- In der Raumansicht öffnet dann **Sperrzeit** (Pausen gehören zu Personen); den Grund trägt man ein.
- Sonst öffnet **Neuer Termin**.
- Notiz, Paar oder Spa-Tag und Sauna-Eintritt schlägt der Kalender nie selbst vor.

Der Vorschlag ist nur eine Vorauswahl: Mit der Leiste oben im Panel lässt sich die Art jederzeit wechseln, siehe [Art wechseln](hilfe:kalender-pausen-und-notizen#art-wechseln).

![Kurze Lücke: Pause vorgeschlagen](images/app/kalender-eintrag-pause.png)

### Behandlungen nach Passung {#passung}

In **Neuer Termin** stehen die Behandlungen, die mit Vor- und Nachbereitung in die Lücke passen, oben, die mit dem geringsten Rest zuerst. Darunter steht ein kleiner Hinweis:

- „passt genau“ oder „passt (50 von 55 Min.)“: Belegung und die Zeit von der Vorbereitung bis zum Ende der Lücke.
- „Vorbereitung passt nicht davor“: hinten wäre Platz, die Vorbereitung ragt aber in den Termin davor. Einen Beginn etwas später wählen.
- „länger als die freie Zeit“: die Behandlung lässt sich trotzdem wählen; ob es geht, entscheidet der Server beim Buchen.

![Lange Lücke: Termin mit passenden Behandlungen zuerst](images/app/kalender-eintrag-termin.png)

## Mit Taste oder Knopf {#taste}

- **N** öffnet das Panel an der Stelle unter dem Mauszeiger mit der vorgeschlagenen Art; steht die Maus nicht über dem Kalender, ab jetzt in der ersten Spalte.
- **Termin** im Kalenderkopf (oder im Menü daneben ebenfalls **Termin**) öffnet ihn heute etwa eine Viertelstunde ab jetzt in der ersten Spalte, an anderen Tagen zum Tagesbeginn. Gut für Gäste, die spontan hereinkommen.
- Rechtsklick auf eine freie Stelle, dann **Termin hier (…)**; das öffnet immer **Neuer Termin**, ohne Vorschlag.

## Person oder Raum steht fest {#frei-waehlen}

Der Termin entsteht immer in der angeklickten Spalte: Unter dem Titel **Neuer Termin** steht z. B. „bei Lena Huber“ oder in der Raumansicht „Raum Lotus“. Ein Klick auf eine freie Stelle in einer anderen Spalte verlegt den angefangenen Termin dorthin; die Liste **Behandlung** zeigt dann, was diese Person kann. Ist die gewählte Behandlung dort nicht möglich, ist danach keine gewählt.

Wünscht der Gast eine Behandlung, die die Person nicht kann, auf **Freie Zeit suchen** klicken. Die Suche zeigt mit Behandlung und Gast alle Zeiten, zu denen jemand mit der Qualifikation und ein passender Raum frei sind, siehe [Freie Zeit suchen](hilfe:kalender-freie-zeit-suchen).

## Neue Kundin oder neuer Kunde {#neuer-gast}

1. Im Panel auf **Neue Kundin / neuer Kunde** klicken. Passen die Angaben zu einem vorhandenen Kunden, erscheint **Gibt es schon?** mit **Übernehmen**.
2. **Vorname**, **Nachname**, dazu **Telefon** und/oder **E-Mail** eintragen.
3. Auf **Anlegen** klicken. Der neue Gast ist sofort gewählt.
4. Weiter wie oben mit **Termin buchen**.

## Folgetermin {#folgetermin}

Für denselben Gast mit derselben Behandlung in vier Wochen:

1. Den bisherigen Termin anklicken (markieren).
2. **F** drücken oder per Rechtsklick **Folgetermin** wählen.
3. Der Kalender springt auf den Tag in vier Wochen und öffnet **Neuer Termin** mit Gast, Behandlung, Person bzw. Raum, gleicher Uhrzeit und der Notiz „Folgetermin“.
   Kann die Person an diesem Tag die Behandlung nicht (z. B. weil sie keinen Dienst hat), öffnet stattdessen **Freie Zeit suchen** mit Gast und Behandlung.
4. Bei Bedarf Uhrzeit ändern und **Termin buchen** klicken.

## Wenn die Zeit belegt ist {#konflikt}

Die Konfliktmeldung ist eine rote Meldung mit dem Titel „Zeitraum ist belegt.“. Darunter sagt sie, was zu tun ist: „Zuerst verschieben oder einen anderen Zeitraum wählen.“ Es folgt je Belegung eine Zeile mit Symbol und Uhrzeit **in Ortszeit des Standorts**, z. B. „Termin · 09:40–10:50 · Clara Fischer“ (Symbol Kalender), „Sperrzeit · 10:50–12:10 · Mittagspause“ (Schloss), dazu für Räume (Tür) und Geräte (Werkzeug). Liegt die Belegung an einem anderen Tag als dem im Kalender, steht der Tag davor („Do. 22.10. 09:40–10:50“). Bei einem belegten Termin nennt die Zeile zusätzlich Raum bzw. Person („Termin · 09:40–10:50 · Clara Fischer · Raum Zeder“). Ein Klick auf eine Terminzeile markiert diesen Termin im Kalender (bei einem anderen Tag wechselt der Kalender dorthin); das Fenster bleibt offen. In **Neuer Termin** heißt der Knopf der Meldung **Freie Zeit suchen**: Er öffnet die Suche mit Behandlung und Gast, und jede Zeit dort lässt sich buchen. Bei den anderen Meldungen zeigt der Knopf **Zum Konflikt** die Woche der Person mit dem belegenden Termin (bei einem belegten Raum die Woche des Raums), beginnend an dessen Tag, der Termin ist markiert; er fehlt, wenn nur Sperrzeiten oder Geräte im Weg stehen. Ist gar kein passender Raum frei, nennt die Meldung die belegten Räume und die nächste freie Zeit („Räume: Kein geeigneter Raum ist zu dieser Zeit frei. Ein Raum ist frei ab 12:20“). Die Meldung steht im Fenster immer direkt über dem Knopf der Aktion, beim Verschieben im Termin-Fenster in der Karte „Verschieben“. Mit dem Kreuz der Meldung blenden Sie sie aus. Dieselbe Meldung erscheint bei Sperrzeit, Pause, Paar oder Spa-Tag und beim Ändern der Zeiten eines Termins; beim Verschieben per Ziehen steht dieselbe Liste mit dem Knopf **Zum Konflikt** in der Meldung über dem Kalender.

![Konfliktmeldung mit Belegungen in Ortszeit](images/app/kalender-konflikt.png)

- Wer nicht weiß, wann etwas frei ist: [Freie Zeit suchen](hilfe:kalender-freie-zeit-suchen) oder [Wunsch eintippen (KI)](hilfe:kalender-ki-anfrage). Beide öffnen am Ende dasselbe Panel **Neuer Termin**, schon ausgefüllt.

## Hinweise {#hinweise}

- Buchen dürfen Rezeption, Standortleitung und Inhaber. Therapeut:innen sehen den Kalender, buchen aber nicht.
- Ohne Verbindung wird der Termin vorgemerkt und erst beim Abgleich geprüft, siehe [Arbeiten ohne Verbindung](hilfe:kalender-offline).
- Mehrere Gäste oder mehrere Behandlungen hintereinander: [Paar oder Spa-Tag](hilfe:kalender-paar-und-spa-tag).
- Vor- und Nachbereitung der Behandlung hält der Kalender automatisch frei, siehe [Behandlungen](hilfe:behandlungen#vorbereitung).
- Während der Einwirkzeit einer Packung oder Maske ist die Person frei (der Raum nicht). Ein Klick dorthin im Kalender bucht nichts; [Freie Zeit suchen](hilfe:kalender-freie-zeit-suchen) und die Terminannahme finden diese Zeiten, weil sie über denselben Planer gehen. Der neue Termin steht dann neben dem ersten in einer eigenen Bahn.
