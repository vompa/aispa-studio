---
art: einstellung
bereich: Stammdaten
oberflaeche: verwaltung
rollen: inhaber, manager
seite: verwaltung
---
# Postfach & Nummern

Einstellungen in der Windows-App unter „Verwaltung“ › „Kontakt“ › „Postfach & Nummern“. Hier verbindet das Spa sein eigenes Mailpostfach mit der KI-Terminannahme, legt die Absender-Regeln für Buchungsplattformen wie Treatwell fest und sieht, über welche Nummern Gäste es per SMS, WhatsApp oder Anruf erreichen. Die Einstellungen gelten für alle Standorte des Spas. Sehen und ändern darf sie nur, wer die Firmendaten pflegen darf (Inhaber).

So arbeitet die Terminannahme mit Postfach und Nummern:

- Neue Mails im Postfach, SMS, WhatsApp-Nachrichten und Anrufe an die Nummern des Spas landen im „Posteingang“ dieses Spas. Den Posteingang sieht, wer Termine buchen darf (Inhaber, Standortleitung, Rezeption).
- Die KI liest die Anfrage, sucht freie Zeiten im Kalender, trägt den Termin ein oder schlägt Zeiten vor und entwirft die Antwort. Standardmäßig prüft die Rezeption den Entwurf im Posteingang und sendet ihn erst dann. Jede Antwort trägt den Hinweis, dass sie mit KI erstellt wurde.
- Buchungen von Treatwell, die in diesem Postfach ankommen, erkennen die [Absender-Regeln](hilfe:postfach#absender-regeln): Sie werden gelesen und, bei Absender-Regel „automatisch“, sofort als Termin in den Kalender eingetragen. Siehe [Treatwell-Buchung im Posteingang](hilfe:anfragen-treatwell-buchung).
- Die KI-Terminannahme gehört zur Abo-Stufe „Spa Plus“. In kleineren Stufen wird das Postfach nicht abgerufen.

## Eigenes Postfach {#eigenes-postfach}

Das Postfach, in das Gäste ihre Anfragen schreiben, z. B. eine Adresse wie termine@ beim eigenen Mailanbieter. Das Programm ruft es in kurzen Abständen ab (Standard alle 30 Sekunden).

- Verarbeitete Mails bleiben im Postfach. Sie werden nicht als gelesen markiert, sondern bekommen eine Markierung: bei IMAP das Schlagwort „$AispaVerarbeitet“, bei Microsoft 365 die Kategorie „AISPA verarbeitet“. So sieht das Team im Mailprogramm weiter, was neu ist. Kennt ein IMAP-Server keine eigenen Schlagwörter, werden verarbeitete Mails als gelesen markiert.
- Gelesen werden Mails der letzten sieben Tage, die noch nicht verarbeitet sind.
- Über dem Formular steht, wann zuletzt abgerufen wurde, oder der Fehler beim letzten Abruf (z. B. falsches Passwort). Nach dem Speichern wird der Fehler zurückgesetzt und beim nächsten Abruf neu geprüft.

### Postfach abrufen {#abrufen}

Schaltet den Abruf ein oder aus. Bei einem neuen Postfach ist er eingeschaltet. Ausgeschaltet bleiben alle Angaben gespeichert, aber es wird nichts gelesen und auch nichts aus diesem Postfach gesendet; Mails gehen dann über den gemeinsamen Versand.

### Art {#art}

Wie das Programm auf das Postfach zugreift:

- **IMAP (z. B. beim Hoster)**: für die meisten Mailanbieter. Dazu gehören Server, Port, Verschlüsselung, Benutzername, Ordner und Passwort. Standard.
- **Microsoft 365**: für Postfächer in Microsoft 365 (Exchange Online). Dazu gehören Verzeichnis-ID, Anwendungs-ID und Client-Geheimnis einer App, die in Microsoft Entra für das Spa angelegt ist. Die App braucht die Anwendungsberechtigung `Mail.ReadWrite`, für das Senden aus dem Postfach zusätzlich `Mail.Send`. Am besten beschränkt die IT des Spas die App auf dieses eine Postfach.

Wechselt man die Art, muss das Passwort bzw. Client-Geheimnis neu eingegeben werden.

### Adresse des Postfachs {#adresse}

Die Mailadresse des Postfachs, z. B. termine@ und die Domain des Spas. Pflichtfeld, muss eine gültige Mailadresse sein. Bei Microsoft 365 ist das zugleich das Postfach, das gelesen wird. Ist das Postfach eingeschaltet, sendet aber nicht selbst, steht die Adresse als Antwortadresse in jeder Mail des Spas, damit Antworten der Gäste hier ankommen.

### Server {#server}

Nur bei IMAP. Der IMAP-Server des Mailanbieters, z. B. imap. und die Domain des Anbieters. Pflichtfeld. Steht in der Anleitung des Anbieters für Mailprogramme.

### Port {#port}

Nur bei IMAP. Der Port des IMAP-Servers, 1 bis 65535. Standard ist 993 (passt zu SSL/TLS). Bei STARTTLS ist es meist 143.

### Verschlüsselung {#verschluesselung}

Nur bei IMAP. Wie die Verbindung zum IMAP-Server geschützt ist:

- **SSL/TLS**: verschlüsselt von Anfang an. Standard, üblich mit Port 993.
- **STARTTLS**: beginnt unverschlüsselt und schaltet dann auf Verschlüsselung um, üblich mit Port 143.
- **Keine (nur Test)**: unverschlüsselt. Nur für Testumgebungen; Passwort und Mails gingen sonst lesbar übers Netz.

### Benutzername {#benutzername}

Nur bei IMAP. Der Anmeldename beim Mailanbieter, oft die Mailadresse selbst. Pflichtfeld. Er gilt auch für den Versand über SMTP.

### Ordner {#ordner}

Nur bei IMAP. Der Ordner, aus dem Anfragen gelesen werden. Leer heißt „INBOX“, also der normale Posteingang. Ein anderer Ordner ist sinnvoll, wenn eine Regel im Mailprogramm Anfragen dorthin verschiebt.

### Verzeichnis-ID (Tenant) {#verzeichnis-id}

Nur bei Microsoft 365. Die Verzeichnis-ID (Tenant-ID) des Microsoft-365-Kontos des Spas. Pflichtfeld. Steht in Microsoft Entra in der Übersicht der App.

### Anwendungs-ID (Client) {#anwendungs-id}

Nur bei Microsoft 365. Die Anwendungs-ID (Client-ID) der App in Microsoft Entra. Pflichtfeld.

### Passwort bzw. Client-Geheimnis {#passwort}

Bei IMAP das Passwort des Postfachs (bei manchen Anbietern ein eigenes App-Passwort), bei Microsoft 365 das Client-Geheimnis der App.

- Wird nur geschrieben und nie wieder angezeigt: Beim Tippen erscheinen nur Punkte. Es liegt verschlüsselt in der Datenbank.
- Ist schon eines gespeichert, steht im Feld „gespeichert, leer lassen zum Behalten“. Leer lassen behält es, eine neue Eingabe ersetzt es.
- Beim ersten Einrichten und nach einem Wechsel der Art ist es Pflicht.

### Speichern, Jetzt prüfen, Postfach entfernen {#knoepfe}

- **Speichern**: übernimmt die Angaben (in der Windows-App auch mit Strg+S). Gelesen wird ab dem nächsten Abruf. **Verwerfen** setzt die Felder auf den gespeicherten Stand zurück.
- **Jetzt prüfen**: bedienbar, wenn ein eingeschaltetes Postfach gespeichert ist. Ruft das Postfach sofort ab und prüft, wenn aus dem Postfach gesendet wird, auch die Anmeldung zum Versand. Das Ergebnis steht nach wenigen Sekunden über dem Formular bzw. unter „Antworten und Erinnerungen“; kommt in etwa 15 Sekunden keines, sagt die Meldung das.
- **Postfach entfernen**: löscht die Angaben samt Passwort, nach einer Rückfrage. Schon eingegangene Anfragen bleiben im Posteingang. Danach gehen alle Mails wieder über den gemeinsamen Versand.

## Antworten und Erinnerungen {#antworten-und-erinnerungen}

Hier steht, über welchen Weg Mails des Spas an Gäste hinausgehen: Antworten der Terminannahme, Terminerinnerungen und Wiederbuchungs-Hinweise.

Ohne eigenen Versand gehen sie über den gemeinsamen Versand von AISPA Studio, mit dem Namen des Spas als Absender. Ist das Postfach eingeschaltet, steht seine Adresse als Antwortadresse in der Mail; Antworten der Gäste kommen so trotzdem in dieses Postfach.

### Aus diesem Postfach senden {#aus-postfach-senden}

Eingeschaltet gehen die Mails direkt aus dem Postfach des Spas, mit dessen Adresse als Absender. Standard ist ausgeschaltet.

- Bei IMAP wird über den SMTP-Server des Mailanbieters gesendet, mit demselben Benutzernamen und Passwort wie beim Abruf.
- Bei Microsoft 365 braucht die App zusätzlich die Berechtigung `Mail.Send`. Gesendete Mails liegen dann im Postfach unter „Gesendete Elemente“.
- Klappt der Versand nicht, steht der Fehler hier als „Letzter Versand fehlgeschlagen: …“. Das Programm versucht es erneut.

### SMTP-Server {#smtp-server}

Nur bei IMAP und eingeschaltetem Senden. Der Server für ausgehende Mails, z. B. smtp. und die Domain des Anbieters. Pflichtfeld, sobald aus dem Postfach gesendet wird.

### SMTP-Port {#smtp-port}

Port des SMTP-Servers, 1 bis 65535. Standard ist 587 (passt zu STARTTLS). Bei SSL/TLS ist es meist 465.

### Verschlüsselung beim Senden {#smtp-verschluesselung}

Wie die Verbindung zum SMTP-Server geschützt ist: **STARTTLS** (Standard, Port 587), **SSL/TLS** (Port 465) oder **Keine (nur Test)**, nur für Testumgebungen.

### Kopie im Ordner „Gesendet“ ablegen {#gesendet-kopie}

Nur bei IMAP und eingeschaltetem Senden. Legt jede gesendete Mail zusätzlich im Ordner „Gesendet“ des Postfachs ab, damit das Team sie im Mailprogramm sieht. Standard ist eingeschaltet.

- Bei Gmail ausschalten: Gmail legt über SMTP gesendete Mails selbst unter „Gesendet“ ab, sonst gäbe es sie doppelt.
- Klappt nur die Ablage nicht, ist die Mail trotzdem beim Gast angekommen. Dann steht hier ein Hinweis, der mit „Gesendet,“ oder „Senden klappt,“ beginnt.

### Ordner für gesendete Mails {#gesendet-ordner}

Nur wenn „Kopie im Ordner „Gesendet“ ablegen“ eingeschaltet ist. Name des Ordners für die Kopie, höchstens 200 Zeichen. Leer („automatisch“) heißt: Das Programm nimmt den Ordner, den der Server als „Gesendet“ kennzeichnet, oder sucht einen üblichen Namen wie „Sent“, „Gesendet“ oder „Gesendete Elemente“.

## Absender-Regeln {#absender-regeln}

Die Regeln legen fest, welche Absender Buchungsplattformen sind und was mit ihren Mails geschieht. Jedes Spa hat sie für sich. Für Treatwell sind die Domains `treatwell.de`, `treatwell.at`, `treatwell.ch`, `treatwell.com` und `treatwell.co.uk` vorbelegt, jeweils mit „Buchung automatisch übernehmen“; `partner.treatwell.com` und andere Unterdomains sind damit schon abgedeckt. Was das Spa ändert oder löscht, bleibt so; die Vorbelegung kommt nicht wieder.

Mails von Absendern ohne Regel bearbeitet die Terminannahme wie jede Anfrage.

### Adresse oder Domain {#absender-muster}

Eine genaue Mailadresse (`buchungen@plattform.example`) gilt nur für diese Adresse. Eine Domain (`plattform.example`) gilt für alle Adressen dieser Domain und ihrer Unterdomains, aber nicht für ähnliche Namen wie `plattform.example.betrug.net`. Groß- und Kleinschreibung sind egal. Passen mehrere Regeln, gilt die genauere: die genaue Adresse vor der Domain, die längere Domain vor der kürzeren. Pflichtfeld; es gibt jedes Muster nur einmal.

### Plattform {#absender-plattform}

- **Treatwell**: Die Mail wird als Treatwell-Buchung gelesen (Neubuchung, Änderung, Absage). Das Format von Treatwell ist nicht öffentlich; die App liest die Mail mit der KI und ersatzweise nach festen Regeln.
- **Andere Plattform**: Die Mail wird wie eine Anfrage gelesen. Die App antwortet nie an die Plattform: es gibt keinen Antwortentwurf.

### Verarbeitung {#absender-verarbeitung}

- **Buchung automatisch übernehmen**: Eine gültige Treatwell-Buchung kommt sofort in den Kalender. Das gilt nur, wenn das Postfach den Absender bestätigt hat (SPF/DKIM); sonst bleibt sie als Vorschlag im Posteingang mit dem Hinweis „Absender nicht bestätigt“. Bei einer anderen Plattform liest die Terminannahme die Mail und trägt eine genau gewünschte, freie Zeit ein.
- **Nur als Vorschlag in den Posteingang**: Nichts wird selbst eingetragen. Die Nachricht steht unter „Prüfen“, die gewünschte Zeit liegt als Knopf bereit (**Freie Zeiten**); ein Klick trägt den Termin ein. Eine Absage von Treatwell wird nur gemeldet, storniert wird von Hand.
- **Ignorieren**: Die Mail bleibt im Posteingang sichtbar (Stand „Erledigt“ mit Hinweis auf die Regel), wird aber nicht von der KI bearbeitet, und es gibt keinen Antwortentwurf. Gut für Newsletter und Rechnungsmails der Plattform.

### Aktiv {#absender-aktiv}

Eine Regel mit ausgeschaltetem Häkchen gilt nicht, bleibt aber gespeichert.

### Notiz {#absender-notiz}

Freier Text bis 200 Zeichen für das Team, z. B. „Newsletter“. Er erscheint im Hinweis zur Mail.

### Hinzufügen, ändern und löschen {#absender-aendern}

Änderungen an Plattform, Verarbeitung, Aktiv und Notiz gelten sofort, auch ohne „Speichern“ des Postfachs; die Notiz wird beim Verlassen des Feldes gespeichert. **Regel hinzufügen** legt eine neue an, **Löschen** entfernt sie nach einer Rückfrage. Schon eingegangene Mails ändern sich dadurch nicht.

## Nummern {#nummern}

Die Liste zeigt die eigenen Nummern des Spas, je mit Kanal, z. B. „SMS: +43…“. Sind keine eingetragen, kommen SMS, WhatsApp und Anrufe über die gemeinsame Nummer von AISPA Studio.

- Nummern richtet der Betreiber von AISPA Studio ein, weil sie in seinem Konto beim Telefonanbieter liegen. Das Spa kann sie hier nur ansehen.
- Jede Nummer gehört genau einem Spa. Nachrichten und Anrufe an diese Nummer landen im Posteingang dieses Spas.
- Antworten an Gäste und Terminerinnerungen per SMS oder WhatsApp gehen von dieser Nummer. So landet auch eine Antwort des Gastes wieder beim richtigen Spa.
- Eine eigene Nummer gibt es nur mit der Abo-Stufe „Spa Plus“.
