---
art: ablauf
bereich: Kassa
oberflaeche: windows-app, verwaltung
rollen: inhaber, manager
seite: kassa
stichwoerter: Anzahlung retournieren, Anzahlung zurückzahlen, Anzahlung erstatten, Vorauszahlung zurück, Storno Anzahlung, Kulanz, Geld zurück Anzahlung, Absage Anzahlung, deposit refund, return deposit
---
# Anzahlung retournieren

Eine geleistete Anzahlung geht nie an der Kassa vorbei zurück, sondern über den **Storno-Beleg** des Anzahlungsbelegs: mit Signatur, im Tagesabschluss und auf dem **Zahlungsweg der Einzahlung** (Karte, bar oder Gutschein). Online bezahlte Anzahlungen erstattet danach der Zahlungsanbieter, eine Kartenzahlung am Terminal geht als Gutschrift an das Terminal.

## Bei einer Absage wegen einer Schließzeit {#automatisch}

Sagt der Betrieb Termine einer Schließzeit ab, halten die Absageregeln als eingehalten: die Anzahlung wird **automatisch retourniert**, mit der Absage. Die Nachricht an den Gast nennt es, im Protokoll der Schließzeit steht der Betrag mit dem Storno-Beleg. Nichts ist von Hand zu tun; nur wenn der Storno nicht ging (zum Beispiel ist die Kassa außer Betrieb), meldet es die App, und die Anzahlung ist wie unten zu retournieren. Mehr: [Termine absagen](hilfe:schliesszeiten-termine-absagen#anzahlung).

## In allen anderen Fällen {#von-hand}

Hat der Gast nach der Frist abgesagt, ist nicht erschienen oder soll aus Kulanz sein Geld zurückbekommen, retourniert ein Manager oder der Inhaber die Anzahlung von Hand:

1. Im Kalender den Termin öffnen. Im Seitenpanel steht der Abschnitt **Anzahlung** mit Betrag, Zahlungsweg und Beleg, solange die Anzahlung offen ist (nicht beim Kassieren der Behandlung verrechnet und der Anzahlungsbeleg nicht storniert). In der Verwaltung steht derselbe Abschnitt im Termin; an der Kassa hat der Anzahlungsbeleg den Knopf in der Belegansicht.
2. Auf **Anzahlung retournieren …** klicken.
3. In der Rückfrage steht, welcher Betrag auf welchem Zahlungsweg zurückgeht. Im Feld **Grund (Pflicht)** den Grund eintragen, zum Beispiel „Kulanz wegen Krankheit“. Ohne Grund fragt der Dialog noch einmal.
4. Auf **Retournieren** klicken. **Abbrechen** (oder Esc) lässt alles, wie es ist; der Fokus steht anfangs auf **Abbrechen**.
5. Es erscheint „Anzahlung retourniert (Storno-Beleg K1 Nr. 13)“. Der Grund steht auf dem Storno-Beleg („Anzahlung retourniert: …“) und im Hinweis am Termin.

![Termin mit dem Abschnitt Anzahlung und dem Knopf Anzahlung retournieren](images/app/termin-anzahlung-retournieren.png)

## Stand der Anzahlung {#stand}

Alle, die den Kalender sehen, lesen am Termin den Stand der Anzahlung: zum Beispiel „Bezahlt, Anzahlungsbeleg erstellt“ mit Betrag und Prozent. Ab 100 % heißt der Abschnitt **Vorauszahlung**. Eine Anzahlung entsteht bei der Online-Buchung; das Team fordert sie nicht an.

## Wer darf das {#rechte}

Nur **Manager und Inhaber**. Die Standortleitung und die Rezeption sehen den Knopf nicht; der Server lehnt den Aufruf mit „Eine Anzahlung retournieren nur Inhaber und Manager“ ab. Dasselbe gilt für das **Storno des Anzahlungsbelegs** an der Kassa: den hebt nur die Leitung auf, bei Standortleitung und Rezeption ist „Stornieren“ gesperrt und der Tooltip sagt „Anzahlungsbelege storniert nur die Leitung.“ ([Beleg stornieren](hilfe:kassa-beleg-stornieren#rechte)). Die automatische Rückzahlung bei einer Absage wegen einer Schließzeit läuft weiter, auch wenn die Standortleitung die Absage auslöst.

## Gut zu wissen {#hinweise}

- Eine **verrechnete** Anzahlung (beim Beleg der Behandlung abgezogen) lässt sich nicht retournieren: zuerst den Beleg der Behandlung stornieren, dann steht die Anzahlung wieder bereit.
- **Vorauszahlungen bei Treatwell** erstattet die Rezeption dort; die Kassa kennt sie nicht als Anzahlung.
- Jede Rückzahlung hat genau einen Storno-Beleg; ein zweites Mal geht es nicht.
- Hat der Gast **bar** angezahlt, geht das Geld bar zurück: Die Absage wegen einer Schließzeit sagt dem Gast „Sie erhalten den Betrag von 30,00 € bar an der Rezeption zurück“ (in seiner Sprache), sonst „auf dem Zahlungsweg der Einzahlung“. Der Zahlungsweg steht im Hinweis am Termin („Storno-Beleg K1 Nr. 13 vom …, zurück: Bar“) und im Protokoll der Schließzeit.
