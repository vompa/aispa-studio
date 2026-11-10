---
art: ablauf
bereich: Verlauf
oberflaeche: windows-app
rollen: rezeption, leitung, inhaber, manager
seite: verlauf
stichwoerter: Verlauf durchsuchen, wer hat storniert, Stornos heute, bezahlt bar, bezahlt Karte, Beleg finden, Storno zurücknehmen, Stornierung zurücknehmen, wiederherstellen, Rückruf, Journal, Protokoll, Historie, search history, undo cancellation
---
# Im Verlauf suchen

Mit der Seite [Verlauf](hilfe:seite-verlauf) finden Sie heraus, wer was wann getan hat, und springen von dort direkt zum Gast, zum Termin oder zum Beleg.

## Suchen {#suchen}

1. In der Navigation auf **Verlauf** klicken. Die Seite zeigt den heutigen Tag.
2. Den **Zeitraum** wählen, zum Beispiel **7 Tage**. Für bestimmte Tage **Zeitraum** wählen und Von und Bis eintragen.
3. Die Art eingrenzen: etwa **Stornos** für alle Absagen oder **Zahlungen** für die Kassa. Mehrere Chips lassen sich zugleich wählen.
4. Bei Bedarf **Person** oder **Kanal** wählen, zum Beispiel „Online“ für alles, was Gäste selbst gebucht oder abgesagt haben.
5. Ins Suchfeld einen Namen, eine Behandlung, einen Storno-Grund oder eine Belegnummer tippen. Die Liste lädt nach einem kurzen Moment neu.

Beispiele:

- „Wer hat den Termin von Frau Müller abgesagt?“ **30 Tage**, **Stornos**, „Müller“ suchen. Die Zeile nennt Person und Grund.
- „Wer hat heute den Gutschein verkauft?“ **Heute**, **Zahlungen**, „Gutschein“ suchen. Ein Verkauf ohne Termin nennt die Ware („Bezahlt Karte: 50,00 € von Lena für Gutschein 50 € (Beleg …)“); die Suche findet auch die weiteren Zeilen des Belegs.
- „Was wurde heute bar bezahlt?“ **Heute**, **Zahlungen**, „bar“ suchen. Jede Zeile nennt Betrag und Beleg („Bezahlt bar: 85,00 € von Lena (Beleg …)“).
- „Wer hat die Anfrage von Frau Huber erledigt?“ **Anfragen**, „Huber“ suchen. Die Suche findet Gast, Absender und Betreff der Anfrage; die Zeile nennt, wer sie gebucht, als erledigt markiert oder die Antwort freigegeben hat.
- „Welche Absagen kamen online?“ **Stornos**, Kanal **Online**. Kurzfristige Absagen tragen die Pille **Kurzfristig**.

Findet die Suche nichts, steht „Nichts gefunden. Filter zurücksetzen?“ da, mit einem Knopf, der alle Filter zurücksetzt.

## Eintrag öffnen und springen {#springen}

1. Die Zeile wählen und **Enter** drücken, oder doppelklicken. Rechts öffnet sich das Panel des Eintrags.
2. Den passenden Knopf wählen:
   - **Kundenkarte öffnen** wechselt zu **Kunden** und wählt den Gast.
   - **Termin öffnen** wechselt in den Kalender auf den Tag des Termins und wählt ihn. Bei einem stornierten Termin öffnet sich das Panel des stornierten Termins.
   - **Beleg anzeigen** wechselt in die **Kassa**, Reiter **Belege**, auf den Tag des Belegs und wählt ihn.
   - **Anfrage öffnen** wechselt zu **Anfragen** und zeigt die Nachricht mit Antwort.
   - **Behandlung öffnen**, **Dienstplan öffnen** (auf der Woche) und **Zugang öffnen** führen in die Verwaltung.

Ein gesperrter Knopf nennt im Hinweis das fehlende Recht. Einträge zu Kunden ohne Termin haben nur **Kundenkarte öffnen**.

![Panel eines Stornos im Verlauf](images/app/verlauf-panel-storno.png)

## Aus dem Verlauf zurücknehmen {#zuruecknehmen}

Ein Storno lässt sich direkt aus dem Verlauf zurücknehmen, etwa wenn der Gast zurückruft.

1. Den Storno öffnen, zum Beispiel über den Chip **Storniert n** im Kalender oder mit dem Chip **Stornos**.
2. Auf **Stornierung zurücknehmen** klicken.
3. Sind Zeit, Person und Raum noch frei, fragt die Rückfrage „Stornierung zurücknehmen?“ mit denselben Hinweisen wie im Termin-Panel (Absage-Nachricht, Anzahlung). Mit **Zurücknehmen** bestätigen. Unten steht „Termin wiederhergestellt: <Gast>, <Tag> <Zeit>“, und der Verlauf lädt neu.
4. Ist die Zeit inzwischen belegt oder liegt der Termin in der Vergangenheit, fragt die Rückfrage „Zu neuer Zeit zurücknehmen?“ und nennt den Grund. **Neue Zeit wählen** wechselt in den Kalender auf den nächsten freien Tag und öffnet den stornierten Termin mit der Karte **Verschieben**. Darunter stehen freie Zeiten für Gast, Behandlung und Person. Der erste Vorschlag ist schon eingetragen; einen anderen anklicken oder Tag und Beginn ändern, dann mit **Verschieben** bestätigen. Der Termin ist danach zu dieser Zeit wieder gebucht. Hat der Gast die Absage schon bekommen, erhält er dabei eine Korrektur mit der neuen Zeit.

![Rückfrage Zu neuer Zeit zurücknehmen mit dem Grund](images/app/verlauf-rueckfrage-neue-zeit.png)

Wer abbricht, lässt den Termin storniert; er steht weiter im Verlauf. Zurücknehmen darf, wer Termine buchen darf. Mehr zum Zurücknehmen im Kalender steht unter [Stornierung zurücknehmen](hilfe:kalender-termin-stornieren#zuruecknehmen).
