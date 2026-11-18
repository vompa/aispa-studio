---
art: ablauf
bereich: Kalender
oberflaeche: windows-app
rollen: rezeption, leitung, inhaber, manager
seite: kalender
stichwoerter: Verlauf, Protokoll, Historie, wer hat geändert, wer hat storniert, wer hat verschoben, Änderungen, angelegt per SMS, history, log, audit
---
# Verlauf eines Termins

Im Termin-Panel steht unter **Kunde** der Abschnitt **Verlauf**: Er zeigt, wer den Termin wann über welchen Weg angelegt, verschoben, geändert oder storniert hat.

## Verlauf ansehen {#ansehen}

1. Den Termin im Kalender anklicken. Rechts öffnet sich das Termin-Panel.
2. Auf **Verlauf** klicken. Der Abschnitt klappt auf, die neueste Änderung steht oben.

Jeder Eintrag sagt, was geschah, darunter stehen Datum und Uhrzeit in der Ortszeit des Standorts. Beispiele:

- „Angelegt per SMS“: Die KI-Terminannahme hat den Termin aus einer SMS gebucht.
- „Online gebucht vom Gast“: Der Gast hat im Kundenbereich gebucht.
- „Verschoben von Lena: 14:00 → 15:30“: An einem anderen Tag steht das Datum dabei, bei einer anderen Person oder einem anderen Raum beide Namen.
- „Storniert von Lena: Gast krank“: Mit dem Grund aus der Rückfrage. Bei einer Schließzeit steht „Vom Betrieb abgesagt“.
- „Als nicht erschienen markiert von Lena“, „Eingecheckt von Lena“, „Notiz geändert von Lena“.
- „Bezahlt bar: 85,00 € von Lena (Beleg …)“, „Anzahlung …“ und „Beleg … storniert …“: Zahlungen zum Termin, sichtbar für alle, die die Kassa sehen.

## Ausfallgebühr und Erinnerungen {#ausfallgebuehr}

Im Termin-Panel stehen weitere Abschnitte, sobald es dazu etwas gibt:

- **Ausfallgebühr** zeigt, ob eine Karte hinterlegt ist, mit Betrag und Prozent. Verlangt der Standort eine Gebühr und fehlt die Karte, schickt **Karte per Link anfordern** dem Gast einen Link, per Mail oder ohne Mailadresse per SMS an die Handynummer aus der Kundenkarte. Ist die Gebühr fällig, bucht **Ausfallgebühr belasten …** sie nach einer Rückfrage ab; diesen Knopf sieht nur die Kassenaufsicht.
- **Anzahlung** zeigt, ob der Gast angezahlt hat, mit Betrag und Prozent. Verlangt der Standort eine Anzahlung und hat die Rezeption den Termin gebucht, schickt **Anzahlung per Link** dem Gast einen Link, per Mail oder ohne Mailadresse per SMS. Dort bezahlt er; der Anzahlungsbeleg entsteht an der Kassa und wird beim Kassieren abgezogen. Bezahlt der Gast nicht, bleibt der Termin trotzdem stehen. Solange nichts bezahlt ist, schickt **Link erneut senden** einen neuen Link. Den Knopf gibt es nicht, wenn der Gast weder Mailadresse noch Handynummer hat (SMS nur, wenn ein SMS-Anbieter eingerichtet ist) oder schon eine Karte für die Ausfallgebühr hinterlegt oder angefordert ist. Kam der Link per SMS, gehen auch Anzahlungsbeleg, Belastung der Ausfallgebühr und Erstattung als kurze SMS.

![Termin mit dem Abschnitt Anzahlung, nachdem der Link verschickt ist](images/app/termin-anzahlung-anfordern.png)

- **Terminerinnerungen** zeigt, welche Erinnerungen der Gast bekommen hat, mit Kanal, Zustand und Zeit.
- **Wiederbuchungs-Hinweis** ist eine Werbe-Mail einige Wochen nach der letzten Behandlung und braucht die Einwilligung des Gastes. Die Karte zeigt, ob der Gast eingewilligt hat. **Einwilligung anfragen** schickt ihm eine Mail (Sprache wählbar), er bestätigt per Link; erst dann kommen Hinweise. Sagt der Gast am Empfang, er möchte keine mehr, klicken Sie **Widerrufen**. Beide Knöpfe sehen nur Personen, die buchen dürfen.

## Gut zu wissen {#gut-zu-wissen}

- Den Verlauf sieht, wer den Termin sieht.
- Alles, was am Standort passiert ist, über alle Termine hinweg, zeigt die Seite [Verlauf](hilfe:seite-verlauf).
- Was ohne Person geschieht, nennt den Weg: „automatisch“ für Aufgaben im Hintergrund, „beim Abgleich“ für Änderungen vom zentralen Server.
- Der Verlauf bleibt 3 Jahre und wird mit dem Gast gelöscht. Inhalte von Gesundheitsdaten stehen nie darin.
- Termine aus der Zeit vor dem Verlauf zeigen „Noch keine Einträge.“
- Die Behandlung eines gebuchten Termins lässt sich nicht ändern. Dafür den Termin stornieren und neu buchen, siehe [Termin stornieren](hilfe:kalender-termin-stornieren) und [Termin verschieben](hilfe:kalender-termin-verschieben).
