---
art: ablauf
bereich: Schließzeiten
oberflaeche: verwaltung, windows-app
rollen: leitung, inhaber, manager
seite: verwaltung
stichwoerter: Termine absagen, Schließzeit, Betriebsurlaub Termine, Wasserschaden Termine absagen, Gäste benachrichtigen, Absage Mail, Absage SMS, storniert durch Betrieb, Umbuchung anbieten, Meldung Termine in der Schließzeit, Sprache der Absage, Englisch Französisch Italienisch, Warteliste Schließzeit, Anzahlung zurück, Anzahlung retourniert, cancel appointments, notify guests, waiting list, deposit refund
---
# Termine einer Schließzeit absagen

Bestehende Termine bleiben gebucht, wenn eine Schließzeit angelegt wird. Wer sie absagen will, tut das in einer Rückfrage: aus der Meldung im Kalender oder aus der Maske der Schließzeit. Nichts wird ohne Klick abgesagt oder gesendet. Dafür braucht es die Rechte „Standorte verwalten“ und „Buchen“ (Inhaber, Manager, Standortleitung).

Die Meldung „n Termine in der Schließzeit“ steht in der Seitenleiste **Meldungen**, sobald eine Schließzeit offene Termine oder Einträge der Warteliste hat, egal wie fern sie liegt. Sie ist eine **Warnung**, solange die Schließzeit später als in 14 Tagen beginnt, und ein **Fehler**, wenn sie läuft oder in 14 Tagen beginnt.

## Absagen {#absagen}

1. Im Kalender in der Seitenleiste **Meldungen** bei „n Termine in der Schließzeit“ auf **Termine absagen …** klicken. Oder in der Windows-App unter **Verwaltung** › **Betrieb** › **Schließzeiten** die Schließzeit wählen und in der Maske auf **Termine absagen …** klicken. Es ist dieselbe Rückfrage wie im Kalender.
2. In der Rückfrage stehen alle offenen Termine der Schließzeit mit Gast, Zeit, Behandlung, dem Weg der Nachricht (Mail, SMS oder „kein Kontakt“) und dem **Kürzel der Sprache** (DE, EN, FR, IT). Alle sind gewählt; mit den Häkchen lassen sich einzelne abwählen, **Alle auswählen** schaltet alle um.
3. Gibt es **Einträge der Warteliste**, deren Wunsch in die Schließzeit fällt, stehen sie darunter im Abschnitt **Warteliste (n)**, ebenfalls mit Häkchen. Es zählt die **Tageszeit des Wunsches**: Ist nur der Vormittag geschlossen, fällt ein Wunsch „nachmittags“ oder „abends“ nicht hinein; ein Wunsch ohne Tageszeit zählt den ganzen Tag (Vormittag bis 12 Uhr, Nachmittag 12 bis 17 Uhr, Abend ab 17 Uhr, in Ortszeit des Standorts). Meldung und Zahl in der Liste rechnen genauso. Sie bekommen eine kurze Nachricht in ihrer Sprache und **bleiben auf der Liste**, sofern sie nicht abspringen. Ohne Termine genügt es, nur Einträge zu wählen; dann gibt es kein Textfeld.
4. **Betreff** und **Nachricht** prüfen. Die Vorlage passt zur Art der Schließzeit und ist frei bearbeitbar. Der Server ersetzt je Gast die Platzhalter `{Anrede}`, `{Behandlung}` und `{Zeit}`. Der Text ist die **deutsche Vorlage**: Gäste mit einer anderen Sprache bekommen die Standardvorlage ihrer Sprache (siehe unten).
5. Auf Wunsch **Umbuchung anbieten (Link in den Kundenbereich)** ankreuzen: die Nachricht endet dann mit dem Link, unter dem der Gast neu bucht.
6. Unten steht, wie viele Nachrichten hinausgehen („3 Nachrichten werden gesendet (2 Mail, 1 SMS)“), wie sie sich auf die Sprachen verteilen („Nachrichten nach Sprache: 2 Deutsch, 1 Englisch“), wer ohne Kontakt nicht benachrichtigt wird und welche **Anzahlungen über die Kassa zurückgehen**.
7. Auf **n Termine absagen** klicken (mit Warteliste: „n Termine absagen, m benachrichtigen“). **Abbrechen** (oder Esc) lässt alles, wie es ist; der Fokus steht anfangs auf **Abbrechen**.

Danach erscheint „n Termine abgesagt, n Nachrichten werden gesendet“ (in der Windows-App über der Liste der Schließzeiten), bei Warteliste und Rückzahlung mit „m Wartelisten-Einträge benachrichtigt, k Anzahlungen retourniert“. Die Termine haben den Status „storniert“ mit dem Grund „Schließzeit (Betriebsurlaub): …“ und sind vom Betrieb abgesagt: dafür gibt es nie eine Ausfallgebühr. Bei Terminen, die bei Treatwell vorab bezahlt wurden, kümmert sich die Rezeption um die Erstattung dort.

![Rückfrage Termine absagen in der Windows-App: Sprache je Gast und der Abschnitt Warteliste](images/app/schliesszeit-absage-dialog.png)

## Anzahlung {#anzahlung}

Eine geleistete **Anzahlung wird mit der Absage automatisch retourniert**: als Storno-Beleg über die Kassa (mit Signatur, im Tagesabschluss), auf dem **Zahlungsweg der Einzahlung** (Karte, bar, Gutschein). Eine Online-Zahlung erstattet danach der Zahlungsanbieter. Die Nachricht an den Gast nennt die Rückzahlung („Ihre Anzahlung von 30,00 € wird auf dem Zahlungsweg der Einzahlung retourniert“); hat der Gast **bar** angezahlt, heißt es „Sie erhalten den Betrag von 30,00 € bar an der Rezeption zurück“. Im Protokoll steht der Betrag mit Zahlungsweg und Storno-Beleg, am Termin der Hinweis „Storno-Beleg …, zurück: Bar“. Ging der Storno nicht (zum Beispiel ist die Kassa außer Betrieb), ist der Termin trotzdem abgesagt; die Nachricht nennt dann keine Rückzahlung, die App meldet „Anzahlung nicht retourniert“, und die Anzahlung ist an der Kassa zu erstatten: [Anzahlung retournieren](hilfe:kassa-anzahlung-retournieren).

![Unten in der Rückfrage: Nachrichten nach Sprache und die Anzahlungen, die über die Kassa zurückgehen](images/app/schliesszeit-absage-dialog-unten.png)

## Sprache der Nachricht {#sprache}

Die Sprache kommt aus den Kundendaten (**Sprache** im Formular der Kundendaten). Es gibt die Absage in **Deutsch, Englisch, Französisch und Italienisch**, je Art der Schließzeit; die Anrede und die Zeit stehen in der Schreibweise der Sprache. Hat ein Gast keine Sprache oder eine, für die es keine Vorlage gibt (zum Beispiel Spanisch), geht die Absage auf Deutsch; dasselbe gilt für alle anderen Nachrichten an Gäste (Erinnerungen, Bestätigungen, Rückzahlungen, Antworten der Terminannahme). Eintrag der Warteliste: die Sprache, in der sich der Gast eingetragen hat.

Was Sie in der Rückfrage bearbeiten, ist die deutsche Vorlage. Die Vorlagen der anderen Sprachen sind Standard und nicht bearbeitbar; der eigene **Text für Gäste** der Schließzeit gilt nur in der deutschen Nachricht. Der Hinweis auf die KI steht nur in deutschen Nachrichten, weil nur dort die KI den Text umformuliert hat.

## Text mit KI umformulieren {#ki}

Mit **Freundlicher formulieren (KI)**, **Mit Entschuldigung (KI)** oder **Förmlicher formulieren (KI)** formuliert die KI den Text um, nur auf Knopfdruck. Sie behält die Sprache des Textes (Deutsch), darf keine neuen Zahlen einbauen und die Platzhalter nicht entfernen; sonst bleibt der Text, wie er war, und ein Hinweis erklärt es. Wurde der Text mit KI umformuliert, steht neben **Nachricht** die Pille „KI“, und die Nachricht trägt beim Senden den Hinweis, dass sie mit KI erstellt wurde. Ohne KI-Schlüssel fehlen die Knöpfe; der Text der Vorlage braucht keine KI.

## Was beim Gast ankommt {#gast}

- **Mail** an Gäste mit Mailadresse, sonst **SMS** an die Telefonnummer (wenn der Anbieter eingerichtet ist). Ist bei den Erinnerungen SMS bevorzugt, geht es zuerst per SMS.
- Pro Termin genau eine Nachricht, auch wenn ein Gast mehrere Termine hat; die Gäste erkennen die Absage am Betreff und dem Text mit ihrer Behandlung und Zeit, in ihrer Sprache.
- Einträge der Warteliste bekommen eine eigene kurze Nachricht („Ihr Wartelisten-Wunsch fällt in eine Schließzeit …“), je Eintrag einmal. Sie bleiben auf der Liste.
- Gäste ohne Mailadresse und Telefonnummer werden abgesagt, aber nicht benachrichtigt. Sie stehen im Protokoll mit „kein Kontakt“; die Rezeption ruft sie an.

## Protokoll {#protokoll}

Jede Absage und jede Nachricht an die Warteliste steht im **Protokoll der Absagen** der Schließzeit: Gast, Termin (bei der Warteliste der Wunsch), Kanal mit Sprache, retournierte Anzahlung mit Zahlungsweg, Zustand, wer abgesagt hat und wann. Schlägt der Versand fehl, wiederholt ihn der Server; der Zustand „fehlgeschlagen“ steht, bis er gelingt. Mehr zur Maske: [Schließzeiten](hilfe:schliesszeiten#protokoll).

