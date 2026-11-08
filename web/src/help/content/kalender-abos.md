---
art: einstellung
bereich: Stammdaten
oberflaeche: verwaltung
rollen: inhaber, manager, leitung
seite: verwaltung
stichwoerter: Kalender-Abo, Abo, ICS, iCal, webcal, Feiertage, Schulferien, Ferien, Hinweise, Feiertagskalender, Quelle, Adresse, Datei importieren, CSV, Jetzt abrufen, Widerspruch, übernehmen, ignorieren, Google Kalender, OpenHolidays, feiertagskalender.ch, calendar subscription, public holidays, school holidays
---
# Kalender-Abos

Einstellungen in der Windows-App unter **Verwaltung** › **Betrieb** › **Kalender-Abos**. Ein Abo holt Feiertage, Schulferien oder Hinweise aus einer öffentlichen Kalender-Adresse (ICS) und zeigt sie im Kalender. Gesetzliche Feiertage erscheinen außerdem in den Auskünften des Assistenten und in der Feiertagsliste. Feiertage ändern nie eine Buchung (siehe unten). Links steht die Liste der Abos mit dem Stand des Abrufs, rechts die Maske zum gewählten Abo. Verwalten darf, wer die Standorte verwaltet (Inhaber, Manager, Standortleitung, die Standortleitung nur für ihre Standorte). Alle mit Kalenderrecht sehen die Abos und blenden sie für sich im Kalender ein oder aus.

## Kalender-Abos verwalten {#kalender-abos}

![Kalender-Abos in der Windows-App: Liste der Abos mit Stand des Abrufs](images/app/kalender-abos-liste.png)

Jedes Spa hat von Anfang an drei Abos für gesetzliche Feiertage, je eines für Österreich, Deutschland und die Schweiz. Eingeschaltet ist nur das Abo des Landes, in dem das Spa liegt; die anderen beiden stehen ausgeschaltet in der Liste und lassen sich mit einem Haken bei **Aktiv** einschalten. Die Adressen der drei sind öffentliche Feiertagskalender von Google. **Das Programm prüft diese Quelle nicht für Sie:** der Betrieb bestätigt sie oder tauscht sie gegen eine andere Adresse. Ein Fehler beim Abruf erscheint als Meldung im Kalender.

Der Server holt ein Abo im Hintergrund, einmal am Tag und sofort nach dem Anlegen, Einschalten oder Ändern der Adresse. Er ruft nur ab, was in der Adresse steht, und schickt nichts nach außen. Ohne Internet oder bei einem Fehler gilt die zuletzt gespeicherte Kopie weiter.

Wer darf was:

- Abos anlegen, ändern, löschen, ein- und ausschalten, abrufen, importieren und Widersprüche entscheiden: Inhaber, Manager und Standortleitung (die Standortleitung nur Abos für ihre Standorte, nicht für alle).
- Im Kalender zeigen oder ausblenden: jede Person für sich, im Kalender über den Chip **Kalender** in der Kopfzeile ([Kalender-Abos im Kalender](hilfe:kalender-abos-anzeigen)).

So bedient man die Maske in der Windows-App:

1. In der Liste links ein Abo wählen oder auf **Neues Abo** klicken. Die Liste nennt zu jedem Abo die Art, den Stand des Abrufs und wo es gilt; „Aus“, „Fehler“ und die Zahl offener Widersprüche stehen darüber.
2. Rechts die Maske ausfüllen und auf **Speichern** klicken (oder **Strg+S**). Es erscheint „Gespeichert.“. Ein neues Abo mit Adresse wird danach gleich abgerufen; das Ergebnis steht als Meldung über der Maske.
3. **Verwerfen** neben **Speichern** holt den gespeicherten Stand zurück. Wer mit ungespeicherten Änderungen ein anderes Abo oder einen anderen Bereich wählt, wird gefragt: „Änderungen speichern?“ mit **Speichern**, **Verwerfen** und **Abbrechen**.
4. Ein Fehler steht am Feld, und der Cursor springt ins erste Feld mit einem Fehler.
5. **Löschen** am Ende der Maske fragt vorher nach: „Abo löschen?“.

## Abo {#abo}

### Name {#name}

So heißt das Abo in der Liste, im Kalender und in den Meldungen, höchstens 120 Zeichen, zum Beispiel „Feiertage Salzburg“. Pflichtfeld.

### Art {#art}

Was die Einträge sind:

- **Gesetzliche Feiertage**: die Tage sind reine Information, wie die Feiertagsregeln des Programms. Kalender (Titel des Tages), Feiertagsliste und Assistent kennen sie. **Ein Feiertag hat keine Wirkung auf Öffnungszeiten, freie Zeiten, Serienvorschau und Online-Buchung, weder als Sperre noch als Warnung:** Feiertage sind für Spa-Betriebe die umsatzstärksten Tage, an einem Feiertag wird gebucht wie an einem Werktag, und der Kundenbereich zeigt keinen Feiertagshinweis. Wer an einem Feiertag geschlossen hat, trägt dafür eine [Schließzeit](hilfe:schliesszeiten) ein. Die **Lohnverrechnung** rechnet weiter nur mit den Feiertagsregeln im Programm (gesetzliche Feiertage zählen dort wie ein Sonntag); Tage nur im Abo ändern keine Lohnzahl.
- **Schulferien**: erscheinen im Kalender am Datum (im Tooltip des Datums und im Tagesüberblick) und beim Assistenten („Wann sind Ferien?“). Die Buchung ändern sie nicht.
- **Hinweise** und **Sonstiges**: erscheinen im Kalender am Datum (Messe, Straßensperre). Die Buchung ändern sie nicht.

Die Art eines vorab eingetragenen Abos bleibt.

### Quelle wählen {#quelle}

Eine Auswahl von Vorschlägen je Land und Region: Feiertage Österreich, Deutschland und Schweiz (öffentliche Google-Kalender), Schulferien über die öffentliche Schnittstelle OpenHolidays (Bundesland oder Kanton in der Adresse anpassen) und ein Hinweis auf feiertagskalender.ch für Schweizer Kantone. Ein Vorschlag füllt Adresse, Art und Region aus; den Namen nur, wenn noch keiner steht. Alles bleibt änderbar. Bei jedem Vorschlag steht, wann die Adresse zuletzt geprüft wurde. **Der Betrieb bestätigt die Quelle**, die Vorschläge sind keine Zusage des Anbieters.

- Die Google-Kalender nennen bei jedem Tag, ob er ein gesetzlicher Feiertag, ein regionaler Feiertag (nur in genannten Bundesländern oder Kantonen) oder nur ein Gedenktag ist (Muttertag, Silvester). Das Programm übernimmt als Feiertag nur die ersten beiden; ein regionaler Feiertag gilt nur, wenn der Standort in einem der genannten Bundesländer oder Kantone liegt (dafür das Bundesland oder den Kanton am Standort eintragen, siehe [Bundesland oder Kanton](hilfe:standorte#bundesland-kanton)). Gedenktage zählen nicht.
- Die Seite feiertagskalender.ch verlangt eine Sicherheitsprüfung und ist für den automatischen Abruf nicht gedacht. Dafür die Kalenderdatei dort herunterladen und über **Datei importieren** einspielen, oder die Adresse eines anderen Anbieters eintragen.
- Der Zeitraum der Schulferien-Vorschläge steht in der Adresse (von Jahresbeginn bis Ende des übernächsten Jahres) und wird jährlich im November fortgeschrieben, siehe Erinnerung unten.

### Adresse {#adresse}

Die Adresse des Kalenders, **https://** oder **webcal://** (ein webcal wird als https gespeichert). Sie muss öffentlich sein: Adressen mit Benutzername und Passwort, mit einem Zugangscode im Pfad oder in der Adresse (zum Beispiel `token=`), mit einem anderen Anschluss als 443 oder auf interne Rechner (localhost, Netzadressen wie 192.168.x.x) werden abgelehnt. Höchstens 500 Zeichen. Leer heißt: nur Datei-Import, es gibt keinen Abruf.

## Gilt für {#gilt-fuer}

### Standort {#standort}

**Alle Standorte** oder ein einzelner Standort. Ein Abo für alle Standorte gilt dort, wo auch die Region passt. Ein Abo für alle Standorte legt nur an, wer alle Standorte verwaltet.

### Region {#region}

Ein Land (AT, DE, CH) oder ein Bundesland oder Kanton (AT-9, DE-BY, CH-ZH). Das Abo gilt nur an Standorten in diesem Land bzw. in dieser Region. Leer gilt es überall. So gilt das Abo „Feiertage Deutschland“ mit Region DE nur an Standorten in Deutschland, auch wenn es für alle Standorte eingetragen ist.

## Zustand {#zustand}

### Aktiv {#aktiv}

Schaltet das Abo **für alle** ein oder aus. Ausgeschaltet gibt es keinen Abruf, und das Abo gilt nirgends: nicht im Kalender, nicht in der Feiertagsliste und nicht beim Assistenten. Die gespeicherte Kopie bleibt erhalten und gilt sofort wieder, wenn das Abo eingeschaltet wird. Das Ausblenden im Kalender, das jede Person für sich einstellt, ändert dagegen nichts an der Wirkung: es betrifft nur, was diese Person im Kalender sieht.

Vorab eingetragene Abos lassen sich ausschalten, nicht löschen. Andere Abos löscht der Knopf **Löschen** nach einer Rückfrage.

### Abruf und Stand {#abruf}

Die Zeile unter **Aktiv** nennt den Stand („Stand gestern · 136 Tage“), den Zeitraum der gespeicherten Tage und bei einem Fehler den Grund. **Jetzt abrufen** stößt den Abruf im Hintergrund an (nur bei einem eingeschalteten Abo mit Adresse und ohne ungespeicherte Änderungen); das Ergebnis erscheint nach wenigen Sekunden als Meldung über der Maske („Abgerufen: Stand heute · 150 Tage“ oder der Grund des Fehlers).

**Im Kalender zeigen** (Haken unter dem Knopf, nur für Sie) schaltet um, ob Sie dieses Abo im Kalender sehen. Es wirkt sofort und ist dasselbe wie der Schalter **Zeigen** im Chip **Kalender** ([Kalender-Abos im Kalender](hilfe:kalender-abos-anzeigen)); für die anderen Personen ändert es nichts.

Nach jedem Abruf vergleicht das Programm die neue mit der alten Kopie. Ändert sich etwas, erscheint im Kalender unter **Meldungen** eine Meldung, zum Beispiel „Abo ‚Feiertage Salzburg‘: 2 neue Tage, 1 Tag entfernt“. Schlägt der Abruf fehl, steht dort „Kalender-Abo nicht abgerufen“ mit **Erneut versuchen**. Ein Kalender ohne Einträge im Zeitraum ersetzt eine vorhandene Kopie nicht.

Gespeichert werden die Tage vom Jahresbeginn bis zum Ende des übernächsten Jahres, Wiederholungen (zum Beispiel „jedes Jahr am 1. Mai“) sind darin ausgeschrieben.

### Datei importieren {#import}

Die Rückfallebene, wenn die Adresse nicht geht oder es keine gibt. **Datei importieren** öffnet den Dateidialog von Windows; zu wählen ist eine Datei im ICS-Format (iCalendar) oder eine CSV-Liste mit „Datum;Titel“ oder „Von;Bis;Titel“ (Trenner Strichpunkt, Komma oder Tabulator, Datum als 26.10.2026 oder 2026-10-26, eine Kopfzeile ist erlaubt), höchstens 2 MB. Es gilt derselbe Zeitraum wie beim Abruf. Der Import ersetzt die Einträge früherer Dateien dieses Abos; was ein Abruf geholt hat, bleibt. Das Ergebnis steht als Meldung („5 Tage übernommen“). Eine Datei ohne lesbare Einträge im Zeitraum wird abgelehnt, die bisherige Kopie bleibt.

Im November erinnert eine Meldung einmal im Jahr an die **Feiertage für das übernächste Jahr** („Feiertage für 2028 prüfen“): Prüfen, ob Abos und Regeln für dieses Jahr stimmen, und bei den Schulferien-Adressen den Zeitraum fortschreiben.

## Widersprüche {#widersprueche}

Bei Abos für gesetzliche Feiertage vergleicht das Programm die Tage mit den Feiertagsregeln des Programms für jeden Standort, an dem das Abo gilt, und listet die Unterschiede ab heute auf:

- **Nur im Abo, nicht in den Feiertagsregeln** (zum Beispiel „Heiligabend, halber Tag“): der Tag gilt als Feiertag, bis die Leitung ihn **ignoriert**.
- **Nur in den Feiertagsregeln, nicht im Abo**: die Regel bleibt gültig, sie ist die Grundlage der Lohnverrechnung. Die Zeile weist nur darauf hin, dass das Abo den Tag nicht kennt.

Zu jedem Tag hat die Leitung zwei Kontrollkästchen: **Übernehmen** (der Tag gilt, die Entscheidung ist festgehalten) und **Ignorieren** (der Tag gilt nicht, bei einem Tag nur im Abo; er wird nicht mehr gemeldet). Ein zweiter Klick auf dasselbe Kästchen nimmt die Entscheidung zurück. **Nichts wird stumm überschrieben**: das Programm ändert weder die Feiertagsregeln noch die gespeicherte Kopie. Neben der Überschrift **Widersprüche** zählt die Liste die Tage ohne Entscheidung; die Liste der Abos nennt sie auch („2 Widersprüche“).
