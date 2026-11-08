---
art: ablauf
bereich: Anfragen
oberflaeche: windows-app
rollen: rezeption, leitung, inhaber, manager
seite: anfragen
stichwoerter: Wer ist das, Kunde zuordnen, Absender zuordnen, Kundenzuordnung, Vorschlag übernehmen, neu anlegen, Zuordnung lösen, Adresse merken, Nummer merken, doppelter Kunde, Dublette, unbekannter Absender, assign customer, match sender, duplicate
---
# Wer ist das? Absender einem Kunden zuordnen

Die KI-Terminannahme bucht und antwortet nur dann mit Daten aus der Kundenkarte, wenn sie den Absender sicher kennt: Mailadresse (Hauptadresse oder eine weitere) oder Telefonnummer stimmen mit einem Kunden überein. In allen anderen Fällen schlägt sie bis zu drei Kunden vor, und die Rezeption entscheidet. Bis dahin bucht sie nichts, legt niemanden an, und im Antwortentwurf steht nichts aus der Kundenkarte (keine Termine, Guthaben, Pakete, Vorlieben oder Kennzeichen).

![Anfrage mit Vorschlägen unter „Wer ist das?“](images/app/posteingang-wer-ist-das.png)

![Kundenübersicht zu einem Vorschlag](images/app/posteingang-wer-ist-das-kundenuebersicht.png)

## Vorschlag übernehmen {#zuordnen}

1. In der Navigation **Anfragen** öffnen und die Nachricht mit Status „Prüfen“ anklicken. Unter dem Hinweis „Wer ist das? …“ steht der Abschnitt **Wer ist das?** mit bis zu drei Kunden.
2. Zu jedem Vorschlag steht, warum er passt, zum Beispiel „gleicher Name, Telefon endet auf 4567, letzter Besuch 14.09.2026“. Darunter steht, wie sicher er ist (zum Beispiel „Name und ein weiteres Merkmal passen“). Die besten stehen oben. Ein Klick auf den Namen zeigt die [Kundenübersicht](hilfe:seite-anfragen#kundenuebersicht) wie im Kalender: Telefon, Mailadresse, Besuche, nächste Termine und Kennzeichen.
3. Optional das Häkchen **Adresse/Nummer merken** setzen. Dann merkt sich die Kundenkarte die Mailadresse bzw. Nummer des Absenders, und die nächste Nachricht von dort ist sicher zugeordnet. Eine Mailadresse wird die Hauptadresse, wenn die Karte noch keine hat, sonst eine weitere Adresse; eine Nummer wird nur eingetragen, wenn die Karte noch keine hat.
4. Beim richtigen Kunden auf **Zuordnen** klicken. Es erscheint „Zugeordnet: …“.
5. Jetzt geht alles wie sonst: aus „Freie Zeiten“ buchen ([Anfrage annehmen](hilfe:anfragen-annehmen)), bei einer Absage den Termin stornieren ([Absage stornieren](hilfe:anfragen-absage-stornieren)), Antwort senden ([Antwort freigeben](hilfe:anfragen-antwort-freigeben)).

## Niemand passt: neu anlegen {#neu-anlegen}

Auf **Neu anlegen** klicken. Die App legt einen Kunden aus Name, Telefon und Mailadresse der Nachricht an und ordnet die Nachricht zu. Die Kundenkarte vermerkt „Aus Mail angelegt, bitte Angaben prüfen“; Angaben lassen sich dort mit **Bearbeiten** korrigieren.

## Zuordnung lösen {#loesen}

War es doch ein anderer Kunde, steht in der geöffneten Nachricht **Zuordnung lösen**. Der Kunde wird dann nicht mehr vorgeschlagen, die Vorschläge werden ohne ihn neu gesucht, und die Nachricht wartet wieder auf die Entscheidung. Das geht nur, solange kein Termin gebucht oder storniert ist.

## Wann die Terminannahme selbst zuordnet {#automatisch}

- **Sicher** (ohne Rückfrage, wie bisher): gleiche Mailadresse (Groß- und Kleinschreibung egal) oder gleiche Telefonnummer (Schreibweise egal: „0660 1234567“ ist „+43 660 1234567“). Teilen sich mehrere Kunden dieselbe Adresse oder Nummer, ist es nicht sicher.
- **Kein Kandidat**: Gibt es weder einen sicheren Treffer noch einen ähnlichen Kunden, legt die Terminannahme den Gast beim Buchen neu an und vermerkt „Aus Mail angelegt“ (bzw. SMS, WhatsApp, Anruf, Treatwell).
- **Kandidaten**: In allen anderen Fällen entscheidet die Rezeption. Die Stufen, von oben nach unten: die vom Gast genannte Telefonnummer passt; Name und ein weiteres Merkmal passen (die letzten vier Ziffern der Nummer, der Geburtstag oder ein Termin in den letzten 24 Monaten); nur der Name ist ähnlich (Umlaute, ß und Akzente zählen nicht als Unterschied, kleine Tippfehler auch nicht).
- Kunden mit dem Kennzeichen „Nicht mehr buchen“ werden trotzdem vorgeschlagen; im Grund steht „„Nicht mehr buchen“ ist gesetzt“.

## Gut zu wissen {#hinweise}

- Treatwell-Buchungen gehen anders: Der Termin steht sofort im Kalender und blockt die Zeit, auch wenn der Gast noch nicht sicher zugeordnet ist. Der Gast ist dann vorläufig angelegt („Zuordnung offen“). Mit **Zuordnen** wandert der Termin auf den gewählten Kunden, und der vorläufige Gast wird entfernt, wenn er sonst nichts hat (keine weiteren Termine, Rechnungen oder Belege; sonst bleibt er stehen, und die Nachricht sagt es). **Als neuen Kunden bestätigen** macht den vorläufigen Gast zum Kunden. Siehe [Treatwell-Buchung im Posteingang](hilfe:anfragen-treatwell-buchung).
- „Freie Zeiten“ sind schon vorgeschlagen; gebucht wird aber erst nach der Zuordnung. Wer vorher eine Zeit anklickt, bekommt „Wer ist das?“ als Meldung.
- Zuordnen, Neu anlegen und Lösen gehen nur mit Verbindung zum Server; ohne Verbindung steht „Ohne Verbindung zum Server nicht möglich …“.
- Sieht die Terminannahme eine Antwort des Gasts auf einen Vorschlag, den die Rezeption schon zugeordnet hat, gilt die Zuordnung weiter.
- Beim Anlegen eines Kunden zeigt die App „Gibt es schon?“, wenn Name, Telefon oder Adresse zu einem vorhandenen Kunden passen: [Neue Kundin oder neuen Kunden anlegen](hilfe:kunden-anlegen).
