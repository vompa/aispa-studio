---
art: ablauf
bereich: Kunden
oberflaeche: windows-app
rollen: rezeption, leitung, inhaber, manager
seite: kunden
stichwoerter: Kunde bearbeiten, Kundendaten ändern, Name ändern, Anrede ändern, Titel eintragen, Rufname, Postanschrift, Telefonnummer ändern, E-Mail ändern, Geburtstag eintragen, Notiz ändern, Tippfehler, Rechnungsempfänger, Firma hinterlegen, Rechnungsadresse, UID speichern, Firmenkunde, Adresse ändern, edit customer, billing address, company details
---
# Kundendaten ändern

In der Kundenkarte lassen sich Name, Anrede, Rufname, Titel, Postanschrift, Telefon, E-Mail, Geburtstag und Notiz jederzeit ändern. Dort steht auch der Rechnungsempfänger, z. B. die Firma der Kundin; die Kassa belegt damit die [Rechnung mit Empfänger](hilfe:kassa-rechnung-mit-empfaenger) vor.

## Name, Anrede, Titel, Anschrift, Telefon, E-Mail, Geburtstag, Notiz {#stammdaten}

1. Die [Kundenkarte](hilfe:kunden-kundenkarte) öffnen.
2. Im Kopf der Karte im Menü **…** auf **Bearbeiten (Name, Anrede, Kontakt, Notiz)** klicken.
3. Die Felder ändern. Das Formular hat die Gruppen **Name**, **Kontakt** und **Anschrift**; was selten gebraucht wird (Rufname, Pronomen, Titel, Notiz), steht in **Weitere Angaben** und geht auf, wenn man darauf klickt oder schon etwas darin steht. Den Geburtstag als TT.MM.JJJJ eintippen, z. B. `14.03.1985`. Bei den Titeln tippen und einen Vorschlag wählen; die Zeilen unter dem Namen zeigen Adresszeile und Anrede, bevor gespeichert wird. Was die Felder bewirken: [Name, Anrede und Titel](hilfe:kunden-name-und-anrede).
4. Auf **Speichern** klicken. Die Karte zeigt die neuen Daten.

![Stammdaten in der Kundenkarte bearbeiten](images/app/kunden-bearbeiten.png)

- Pflicht ist ein Name (Vor- oder Nachname, mit Stern gekennzeichnet); bis dahin bleibt **Speichern** ausgegraut. Ein geleertes Feld löscht den gespeicherten Wert.
- Ist eine Eingabe ungültig (z. B. eine PLZ mit zu wenigen Ziffern, eine E-Mail ohne @, ein Geburtstag, den es nicht gibt), färbt sich das Feld rot, der Text darunter sagt, was erwartet wird, und der Cursor springt ins erste solche Feld.
- Kann der Server nicht speichern (z. B. keine Verbindung), steht über **Speichern** die Meldung „Nicht gespeichert“ mit **Erneut versuchen**.

![Fehler am Feld: die PLZ hat vier Ziffern](images/app/kunden-bearbeiten-fehler.png)
- Eine Änderung am gültigen Namen (Vor- oder Nachname) wird protokolliert und steht unter „Kennzahlen“ als „Name geändert“; ausgestellte Belege bleiben unverändert.
- **Abbrechen** verwirft die Eingabe.
- Bearbeitet wird immer nur ein Bereich der Kundenkarte. Öffnet man einen anderen (z. B. den Stift bei den Wünschen), schließt sich der offene, ohne zu speichern.
- Mit der Tastatur: Öffnet man einen Bereich (Stift, **+** oder ein Link unter „Noch nicht erfasst“), steht der Cursor im ersten Feld. Enter in einem Eingabefeld speichert (in mehrzeiligen Feldern Strg+Enter); in einer Auswahlliste öffnet und wählt Enter. Esc schließt den Bereich, ohne zu speichern. Hat man gerade in einem Feld getippt, setzt das erste Esc nur dieses Feld zurück.
- In die Notiz gehören Vorlieben, keine Gesundheitsdaten; die gehören in die Anamnese.
- Ändern dürfen alle, die buchen dürfen (Rezeption, Standortleitung, Inhaber). Ohne Verbindung geht es nicht; die Meldung sagt das.

## Aus dem Kalender ergänzen {#aus-dem-kalender}

Beim Buchen oder Einchecken fehlen oft noch Angaben zum Gast. Dafür muss man die Kundenkarte nicht öffnen:

1. Im Kalender den Termin anklicken.
2. Im Panel auf den **Stift** neben dem Namen klicken.
3. Anrede, Rufname, Pronomen, Titel, Anschrift, Telefon, E-Mail, Geburtstag, Sprache oder Notiz ergänzen. Es ist dasselbe Formular wie in der Kundenkarte, mit derselben Prüfung. Das Panel wird dafür breiter; **Speichern** und **Abbrechen** stehen fest am unteren Rand, der Kopf mit dem Gast bleibt oben.
4. **Speichern** klicken. Der Kalender lädt neu und zeigt den Termin mit den neuen Daten (z. B. ♀/♂ und Rufname). **Abbrechen** verwirft die Eingabe.

![Kundendaten im Panel des Kalenders ergänzen](images/app/kalender-kundendaten.png)

- Den Stift sieht, wer buchen darf. Bei einem Gast ohne Namen heißt der Weg [Namen nachtragen](hilfe:kalender-paar-und-spa-tag#ohne-namen).
- Die Kundenkarte (für Termine, Rabatt und Rechnungsempfänger) öffnet man über die [Kundenübersicht](hilfe:seite-kalender#kundenuebersicht): Klick auf Initialen oder Namen im Kopf, dann **Kundenkarte öffnen**. **…** › **Bearbeiten** dort öffnet dasselbe Formular.

## Weitere E-Mail-Adressen {#weitere-adressen}

Die Hauptadresse steht bei **…** › **Bearbeiten**. Hat ein Gast mehrere Adressen (privat, geschäftlich), lassen sich weitere hinzufügen; Nachrichten von dort ordnet die KI-Terminannahme dem Kunden sicher zu.

1. Die [Kundenkarte](hilfe:kunden-kundenkarte) öffnen.
2. Im Menü **…** auf **Weitere E-Mail-Adresse** klicken, die Adresse eintippen und auf **Hinzufügen** klicken. Die Karte zeigt die Adressen unter „Weitere E-Mail-Adressen“.
3. Mit dem Kreuz hinter einer Adresse lässt sie sich wieder entfernen.

- Höchstens zehn weitere Adressen; die Hauptadresse und doppelte Adressen werden abgewiesen.
- Die Suche findet Kunden auch über weitere Adressen.
- Diese Änderungen gehen nur mit Verbindung zum Server.
- Aus dem Posteingang lässt sich die Adresse eines Absenders gleich merken: [Absender einem Kunden zuordnen](hilfe:anfragen-wer-ist-das).

## Rechnungsempfänger {#rechnungsempfaenger}

1. Die [Kundenkarte](hilfe:kunden-kundenkarte) öffnen.
2. Im Menü **…** auf **Rechnungsempfänger** klicken; ist schon einer hinterlegt, geht auch der Stift beim Rechnungsempfänger.
3. „Empfänger (Name oder Firma)“ und „Anschrift“ ausfüllen, bei Bedarf die „UID-Nummer, z. B. ATU12345678“.
4. Auf **Speichern** klicken. Die Karte zeigt den Empfänger.

- **Entfernen** löscht den hinterlegten Empfänger, **Abbrechen** verwirft die Eingabe.
- Name und Anschrift sind Pflicht.
- Bei der Rechnung an der Kassa lässt sich der Empfänger auch gleich bei der Kundin merken lassen.
- Schon ausgestellte Rechnungen ändern sich dadurch nicht.

## Rabatt {#rabatt}

Den dauerhaften Rabatt ändert man auf derselben Karte: [Dauerhafter Rabatt](hilfe:kunden-rabatt).
