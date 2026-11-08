---
art: einstellung
bereich: Stammdaten
oberflaeche: verwaltung
rollen: inhaber, manager
seite: verwaltung
---
# Erinnerungen

Einstellungen in der Windows-App unter „Verwaltung“ › „Kontakt“ › „Erinnerungen“. Dort stehen zwei verschiedene Arten von Erinnerungen: die Terminerinnerung vor einem gebuchten Termin und der Wiederbuchungs-Hinweis nach einem Besuch. Ein Wiederbuchungs-Hinweis ist eine Werbe-Mail einige Wochen nach der letzten Behandlung, die den Gast zum erneuten Buchen einlädt; er braucht die Einwilligung des Gastes. Im Termin-Panel des Kalenders heißen die beiden Karten „Terminerinnerungen“ und „Wiederbuchungs-Hinweis“. Beide gelten für alle Standorte des Spas. Sehen und ändern darf sie nur, wer die Firmendaten pflegen darf (Inhaber).

![Erinnerungen in der Windows-App](images/app/verwaltung-erinnerungen.png)

## Terminerinnerungen {#terminerinnerungen}

Gäste bekommen vor einem gebuchten Termin eine Nachricht mit Behandlung, Datum und Uhrzeit, Standort und einem Link, über den sie zusagen („Ich komme“) oder absagen können. Hat die Behandlung einen Hinweis für Gäste, steht er in der Mail. Das ist keine Werbung und braucht keine Einwilligung des Gastes.

- Der Text ist fest vorgegeben und wird nicht von der KI geschrieben. Er kommt auf Deutsch, Englisch, Französisch oder Italienisch: in der Sprache, in der der Gast online gebucht hat, sonst in der Sprache aus den Kundendaten; ist keine davon bekannt, auf Deutsch (so wie bei allen Nachrichten an Gäste, auch im Kundenbereich).
- Erinnert wird nur an Termine mit dem Status „gebucht“. Wird ein Termin storniert, geht keine Erinnerung mehr. Wird er verschoben, gilt die Erinnerung für den neuen Beginn.
- Das Programm sucht etwa alle fünf Minuten nach fälligen Erinnerungen. Eine Erinnerung kommt also nicht auf die Minute genau.
- Ob, wann und über welchen Kanal ein Termin erinnert wurde, steht beim Termin im Kalender.
- Die Zusage des Gastes steht am Termin im Kalender („✓ zugesagt“, in der Hover-Karte mit Datum und Uhrzeit), und der Tagesüberblick zählt, wer noch nicht zugesagt hat (siehe [Termin finden](hilfe:kalender-termin-finden#unbestaetigt)). Wird der Termin auf eine andere Zeit verschoben, gilt sie nicht mehr; die nächste Erinnerung fragt wieder.
- Terminerinnerungen gehören zur Abo-Stufe „Start“ und allen höheren Stufen.

### Erinnerungen verschicken {#verschicken}

Schaltet die Terminerinnerungen für das ganze Spa ein oder aus. Standard ist eingeschaltet. Ausgeschaltet geht keine Erinnerung mehr hinaus, auch nicht für schon gebuchte Termine.

### Erste Erinnerung (Stunden vorher, 0 = keine) {#erste-erinnerung}

So viele Stunden vor Beginn des Termins geht die erste Erinnerung. Erlaubt sind ganze Zahlen von 0 bis 168 (eine Woche). 0 schaltet die erste Erinnerung ab. Standard sind 48 Stunden.

### Zweite Erinnerung (Stunden vorher, 0 = keine) {#zweite-erinnerung}

So viele Stunden vor Beginn geht die zweite Erinnerung. Erlaubt sind 0 bis 168; 0 schaltet sie ab. Standard sind 2 Stunden.

- Sind beide Erinnerungen eingeschaltet, muss die zweite näher am Termin liegen als die erste (also weniger Stunden haben). Sonst lässt sich nicht speichern.
- Ein Gast bekommt nie beide auf einmal. Wer kurzfristig bucht, bekommt nur die Erinnerung, die zu diesem Zeitpunkt noch passt. Beispiel: Bei 48 und 2 Stunden bekommt ein Gast, der 30 Stunden vorher bucht, nur die Erinnerung 2 Stunden vorher.

### Kanal {#kanal}

Über welchen Weg die Erinnerung bevorzugt geht. Standard ist „Mail“.

- **Mail**: an die Mailadresse in der Kundenkartei.
- **SMS**: an die Telefonnummer in der Kundenkartei, von der eigenen SMS-Nummer des Spas, wenn es eine hat (siehe „Postfach & Nummern“), sonst von der gemeinsamen Nummer.
- **WhatsApp (freigegebene Vorlage)**: an die Telefonnummer per WhatsApp. WhatsApp erlaubt solche Nachrichten nur mit einer Vorlage, die vorher beim Anbieter freigegeben wurde.

Fehlt beim Gast die Angabe für den gewählten Kanal, nimmt das Programm einen anderen: Bei WhatsApp ohne Vorlage oder ohne WhatsApp-Anschluss geht die Erinnerung per SMS, ohne SMS-Anschluss oder Telefonnummer per Mail. Bei Mail ohne Mailadresse geht sie per SMS, wenn es eine Nummer gibt. Hat der Gast weder Mailadresse noch Telefonnummer, entfällt die Erinnerung; beim Termin steht dann „Keine Mailadresse und keine Telefonnummer“.

Mails gehen aus dem eigenen Postfach des Spas, wenn dort „Aus diesem Postfach senden“ eingeschaltet ist, sonst über den gemeinsamen Versand mit dem Namen des Spas als Absender.

### WhatsApp-Vorlage (Meta: Name, Twilio: Content-SID) {#whatsapp-vorlage}

Erscheint nur beim Kanal WhatsApp und ist dann Pflicht. Hier steht die Kennung der freigegebenen Vorlage, höchstens 200 Zeichen:

- Bei WhatsApp über Meta der Name der Vorlage, so wie er bei Meta angelegt ist.
- Bei WhatsApp über Twilio die Content-SID der Vorlage (beginnt mit „HX“).

Die Vorlage legt das Spa bzw. der Betreiber beim Anbieter an. Das Programm füllt diese Platzhalter:

1. `{{1}}` Vorname des Gastes
2. `{{2}}` Behandlung
3. `{{3}}` Datum und Uhrzeit
4. `{{4}}` Standort
5. `{{5}}` Link zum Termin (ansehen oder absagen)

### Sprache der Vorlage {#vorlagensprache}

Die Sprache, in der die Vorlage beim Anbieter angelegt ist, genau so geschrieben, z. B. `de` oder `de_AT`. Pflichtfeld, höchstens 16 Zeichen. Standard ist `de`.

## Wiederbuchungs-Hinweise {#naechste-behandlung}

Nach einer abgeschlossenen Behandlung bekommt der Gast einen Wiederbuchungs-Hinweis: eine Mail mit einem Link zum Buchen, wenn der eingestellte Abstand vorbei ist und er noch keinen neuen Termin gebucht hat. Das ist Werbung. Deshalb gilt:

- Einen Hinweis bekommen nur Gäste, die ihre Einwilligung per Mail bestätigt haben (Double-Opt-in). Die Anfrage schickt das Team im Kalender beim Termin des Gastes auf der Karte „Wiederbuchungs-Hinweis“ mit „Einwilligung anfragen“; dort wählt es auch die Sprache der Mail. Der Gast bestätigt über den Link in dieser Mail. Erst danach gehen Hinweise.
- Jeder Hinweis enthält einen Link zum Abmelden. Sagt der Gast am Empfang, dass er keine Hinweise mehr möchte, klickt das Team an derselben Stelle beim Termin auf „Widerrufen“.
- Ändert sich die Mailadresse des Gastes, gilt die alte Einwilligung nicht mehr für die neue Adresse.
- Wiederbuchungs-Hinweise gehören zur Abo-Stufe „Spa“ und höher.

So entsteht ein Hinweis:

- Maßgeblich ist die letzte abgeschlossene Behandlung des Gastes. Ist der eingestellte Abstand seit ihrem Ende vorbei, wird der Hinweis fällig.
- Kein Hinweis, wenn der Gast schon einen künftigen Termin gebucht hat, wenn nach diesem Termin schon ein Hinweis ging oder wenn er in den letzten 30 Tagen schon einen bekommen hat. Höchstens eine je Gast auf einmal.
- Ist ein Hinweis länger als 14 Tage überfällig (z. B. weil die Funktion ausgeschaltet war), wird er nicht mehr nachgeholt.
- Das Programm sucht etwa einmal pro Stunde nach fälligen Hinweisen.

Den persönlichen Teil der Mail (zwei bis drei Sätze) schreibt die KI aus Vorname, Behandlung und Name des Spas. Sie nennt keine Preise, Rabatte, Angebote, Daten oder Zahlen. Enthält ihr Text trotzdem Zahlen, Links oder Preiszeichen oder kommt kein brauchbarer Text, gilt ein fester Text. Mails mit Text der KI tragen den Hinweis „Dieser Text wurde mit KI verfasst.“ Die Mail kommt in der Sprache, in der der Gast eingewilligt hat. Der Link zum Buchen öffnet die Online-Buchung beim selben Standort und derselben Behandlung.

### Wiederbuchungs-Hinweise verschicken {#naechste-behandlung-verschicken}

Schaltet diese Hinweise für das ganze Spa ein oder aus. Standard ist ausgeschaltet. Ohne mindestens eine Behandlung mit Abstand geht trotzdem nichts hinaus.

### Abstand je Behandlung (Tage, leer = keine) {#abstand}

Für jede Behandlung ein eigenes Feld mit ihrem Namen, z. B. „Hot-Stone-Massage (Tage, leer = keine)“. Die Zahl sagt, wie viele Tage nach dem Ende dieser Behandlung der Hinweis geht. Erlaubt sind 7 bis 730 Tage. Leer heißt: nach dieser Behandlung geht kein Hinweis. Standard ist leer.

Gespeichert wird mit „Speichern“ (auch Strg+S) für die ganze Maske.

### Zuletzt verschickt {#zuletzt-verschickt}

Die Liste zeigt die letzten 100 Wiederbuchungs-Hinweise, die neuesten zuerst: Gast, Behandlung, Stand und Zeitpunkt. Fährt man mit der Maus über einen Eintrag, erscheint der verschickte Text.

- **wird geschrieben**: Der Hinweis ist angelegt und wird gerade geschrieben und versendet.
- **gesendet**: Die Mail ist hinausgegangen. „(Text von der KI)“ heißt, der persönliche Teil stammt von der KI.
- **entfallen**: Vor dem Versand hat sich etwas geändert, z. B. ist inzwischen ein Termin gebucht oder die Einwilligung gilt nicht mehr. Der Grund steht in Klammern.
- **fehlgeschlagen**: Der Versand hat nicht geklappt; der Fehler steht in Klammern. Das Programm versucht es erneut.
