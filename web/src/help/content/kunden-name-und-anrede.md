---
art: einstellung
bereich: Kunden
oberflaeche: windows-app
rollen: rezeption, leitung, inhaber, manager
seite: kunden
stichwoerter: Anrede, Frau, Herr, divers, Pronomen, sie/ihr, er/ihm, they/them, Geschlecht, Rufname, Spitzname, gültiger Name, Titel, Mag., Dr., DI, Ing., Prof., MSc, akademischer Grad, Adresszeile, Briefanrede, Sehr geehrte, Postanschrift, Adresse des Gastes, salutation, nickname, academic title
---
# Name, Anrede und Titel

Jeder Gast hat einen gültigen Namen (Vor- und Nachname) und auf Wunsch einen Rufnamen für das Haus. Anrede und Titel bestimmen die Adresszeile auf Belegen und die Anrede in Mails, Erinnerungen und Antworten der KI. Das Formular zeigt beides als ruhige Textzeile unter dem Namen. Es hat drei Gruppen, **Name**, **Kontakt** und **Anschrift**, dazu eingeklappt **Weitere Angaben**; je ein Fragezeichen bei Name, Kontakt und Weitere Angaben führt hierher.

![Name, Kontakt und Anschrift in der Kundenkarte bearbeiten](images/app/kunden-bearbeiten.png)

Dieselben Felder lassen sich auch im Kalender ergänzen: der Stift neben dem Gastnamen im Terminfenster öffnet [dasselbe Formular](hilfe:kunden-bearbeiten#aus-dem-kalender).

## Name {#name}

Eine Zeile mit **Anrede**, **Vorname** und **Nachname**. Der rote Stern an Vor- und Nachname kennzeichnet, was ausgefüllt sein muss: Zum Speichern genügt ein Name (Vor- oder Nachname), bis dahin bleibt **Speichern** ausgegraut. Unter der Zeile stehen, sobald ein Name da ist, die Adresszeile („Anschrift: Frau Mag. Brigitte Müller“) und die Anrede in Nachrichten („Anrede: Guten Tag Frau Mag. Müller“); hat das Spa Standorte in mehreren Ländern, steht die Anrede je Land da.

### Anrede {#anrede}

**Frau**, **Herr**, **divers** oder **keine Angabe**. Bei Frau und Herr steht im Kalender und in der Hover-Karte klein **♀** bzw. **♂** neben dem Namen, und Nachrichten beginnen mit „Guten Tag Frau Huber“. Bei divers und keine Angabe gibt es kein Zeichen, Nachrichten sprechen mit dem vollen Namen an („Guten Tag Kim Berger“).

- Die Anrede ist freiwillig und nie Pflicht.
- Sie ist etwas anderes als der Wunsch „Therapeutin“ oder „Therapeut“ unter [Termine am liebsten](hilfe:kunden-termine-am-liebsten#therapeutin); dieser Wunsch steht als Kennzeichen am Termin.

### Gültiger Name {#gueltiger-name}

Vor- und Nachname, wie sie auf Belegen stehen (wie im Ausweis). Jede Änderung wird mit Zeitpunkt und Person protokolliert und steht unter „Kennzahlen“ als „Name geändert“. Schon ausgestellte Belege behalten den Namen von damals.

## Kontakt {#kontakt}

**Telefon** und **E-Mail** stehen nebeneinander, darunter **Sprache** und **Geburtstag**.

- **E-Mail** muss ein @ enthalten, sonst steht am Feld „Die E-Mail-Adresse braucht ein @“.
- **Geburtstag** mit Jahr, z. B. `14.03.1985` (auch `14.3.85` oder `14031985`); ein Datum, das es nicht gibt, ohne Jahr oder in der Zukunft meldet das Feld.
- **Sprache** bestimmt das Kürzel im Kalender und die Sprache der Nachrichten, siehe [Sprache des Gastes](hilfe:sprachen-und-quelle#sprache-des-gastes).
- Ein geleertes Feld löscht den gespeicherten Wert. Pflicht ist nur ein Name.

### Postanschrift {#postanschrift}

Die Gruppe **Anschrift** darunter: **Straße und Hausnummer** über die volle Breite, dann **PLZ**, **Ort** und **Land** (Kürzel wie AT, DE, CH). In Österreich und der Schweiz hat die PLZ vier, in Deutschland fünf Ziffern; stimmt das nicht, färbt sich das Feld rot und der Text darunter sagt, was erwartet wird. Die Kassa schlägt die Anschrift bei einer [Rechnung mit Empfänger](hilfe:kassa-rechnung-mit-empfaenger) zusammen mit der Adresszeile vor, wenn kein eigener Rechnungsempfänger hinterlegt ist. Ein Rechnungsempfänger (z. B. eine Firma) steht getrennt unter [Rechnungsempfänger](hilfe:kunden-bearbeiten#rechnungsempfaenger).

## Weitere Angaben {#weitere-angaben}

Der eingeklappte Bereich enthält, was nur selten gebraucht wird: **Rufname im Haus**, **Pronomen**, **Titel vorangestellt**, **Titel nachgestellt**, **Titel in der Anrede** und **Notiz**. Er geht von selbst auf, wenn eines dieser Felder schon befüllt ist.

### Rufname im Haus {#rufname}

Wie das Team den Gast nennt, z. B. „Steffi“ für Stefanie. Der Rufname ersetzt den Vornamen im Kalender, in der Hover-Karte, in der Suche, im Kopf der Kundenkarte, im Posteingang, auf der Warteliste, in „Mein Tag“ und in der Kassa (groß, darunter klein der gültige Name).

- Rechnungen, Belege mit Empfänger, Anzahlungsbelege, Gutscheine auf Namen, Buchhaltungsexport, Datenschutz-Auskunft, Online-Konto, Anamnese sowie Mails, SMS und Erinnerungen an den Gast verwenden immer den **gültigen Namen**.
- Leer heißt: der Vorname gilt auch im Haus.
- Die Suche und „Gibt es schon?“ finden den Gast über Rufname und Vornamen.

### Pronomen {#pronomen}

Wenn ein Gast seine Pronomen nennt, z. B. „sie/ihr“, „er/ihm“ oder „they/them“, lassen sie sich hier eintragen. Die Kundenkarte und die Kundenübersicht im Kalender zeigen sie in Klammern hinter dem Namen, damit das Team den Gast richtig anspricht.

- Freiwillig: nur eintragen, was der Gast selbst sagt, nie vermuten.
- Nur für das Team sichtbar. Pronomen stehen nicht auf Belegen, nicht im Online-Konto und nicht in Mails, SMS oder Antworten der KI.
- Ein eigenes Feld „Geschlecht“ gibt es bewusst nicht: die [Anrede](hilfe:kunden-name-und-anrede#anrede) kennt divers und keine Angabe, mehr braucht die Software nicht.

### Titel {#titel}

- **Titel vorangestellt**: z. B. Mag., Dr., DI, Ing., Prof., DDr., MMag., HR, KR, MR; mehrere mit Leerzeichen („Mag. Dr.“).
- **Titel nachgestellt**: z. B. MSc, BSc, BA, MA, PhD, MBA; mehrere mit Komma.
- Beim Tippen schlägt das Feld die passenden Titel vor: „Ma“ bietet „Mag.“ und „MMag.“ an; ein gewählter Vorschlag wird an die schon eingetragenen Titel angehängt. Eigene Titel lassen sich frei eintippen.
- Die Adresszeile nennt alle Titel: „Frau Mag. Stefanie Huber, MSc“.
- Suche und Dublettenabgleich vergleichen Namen ohne Titel; „Dr. Huber“ findet Stefanie Huber.

### Titel in der Anrede {#titel-in-der-anrede}

**nach Land** (Standard, das Land des Standorts):

- Österreich: der höchste vorangestellte Titel („Guten Tag Frau Dr. Huber“ bei „Mag. Dr.“), nie nachgestellte.
- Deutschland: nur Dr. und Prof.
- Schweiz: keine Titel.

**ja** nimmt den höchsten vorangestellten Titel in jedem Land, **nein** nie einen. Die Zeile „Anrede: …“ unter dem Namen zeigt das Ergebnis sofort.

### Notiz {#notiz}

Vorlieben des Gastes in einem Satz; keine Gesundheitsdaten, die gehören verschlüsselt in die Anamnese.
