---
art: seite
bereich: Verlauf
oberflaeche: windows-app
rollen: rezeption, leitung, inhaber, manager
seite: verlauf
stichwoerter: Verlauf, Protokoll, Historie, Journal, Aktivitäten, wer hat was gemacht, wer hat storniert, Stornos heute, bezahlt bar, bezahlt Karte, Zahlungen, Änderungen, activity, history, log, audit trail
---
# Seite „Verlauf“ in der Windows-App

Die Seite „Verlauf“ zeigt, was im Betrieb des Standorts passiert ist: Buchungen, Verschiebungen, Stornos, Statuswechsel, Zahlungen, neue Kunden und geänderte Stammdaten, neueste zuerst. Sie beantwortet Fragen wie „Wer hat den Termin von Frau Müller storniert?“ oder „Was wurde heute bar bezahlt?“.

Die Seite steht in der Navigation nach **Kunden** und ist für alle da, die den Kalender sehen.

![Seite Verlauf mit Suchfeld, Zeitraum und Art-Chips](images/app/verlauf-seite.png)

## Aufbau {#aufbau}

- **Suchfeld** „Gast, Behandlung, Person oder Grund“: sucht im ganzen gewählten Zeitraum, auch in Storno-Gründen und Belegnummern. **Strg+F** setzt den Fokus hinein, **Esc** leert es.
- **Zeitraum**: **Heute**, **Gestern**, **7 Tage**, **30 Tage** oder **Zeitraum** mit Von und Bis. „Heute“ ist der Tag in der Ortszeit des Standorts.
- **Art-Chips**, mehrfach wählbar: **Buchungen**, **Verschoben**, **Stornos** (mit Rücknahmen), **Status**, **Zahlungen**, **Anfragen**, **Neue Kunden**, **Kundendaten**, **Behandlungen**, **Dienstplan**, **Zugänge**. Ohne gewählten Chip stehen alle Arten da.
- **Person** und **Kanal** („Alle Kanäle“, Rezeption, Online, Terminannahme, Hintergrund). Die Auswahl „Person“ nennt nur Personen, die im gewählten Zeitraum etwas getan haben.
- **Zurücksetzen** erscheint, sobald ein Filter gesetzt ist, und stellt den Anfang wieder her.
- Die **Liste** gruppiert nach Tagen („Heute, Montag, 9. November“). Jede Zeile zeigt Uhrzeit, Symbol der Art, den Satz („Storniert von Lena: Grund „Krank““) und darunter Gast und Person. Am Ende lädt die Liste beim Scrollen nach, oder über **Weitere laden**.

## Pillen in der Zeile {#pillen}

- **Kurzfristig**: ein Storno innerhalb der Stornofrist des Standorts.
- **Online**: der Gast selbst im Kundenbereich, oder eine Online-Anzahlung.
- **Terminannahme**: die KI-Terminannahme aus einer Nachricht.
- **Hintergrund**: eine Aufgabe des Programms ohne Person, etwa das Einchecken beim Kassieren.

## Seitenpanel {#panel}

**Enter** oder ein Doppelklick auf eine Zeile öffnet rechts das Panel des Eintrags: Satz, Gast mit Telefon, Person, Kanal, bei Stornos der Grund und darunter die Werte vorher und nachher. Ist das Panel offen, folgt es der Auswahl in der Liste. **Esc** oder das Kreuz schließen es.

Je nach Eintrag stehen dort **Stornierung zurücknehmen**, **Beleg anzeigen**, **Anfrage öffnen**, **Termin öffnen**, **Kundenkarte öffnen**, **Behandlung öffnen**, **Dienstplan öffnen** oder **Zugang öffnen**. Wie das geht, steht unter [Im Verlauf suchen](hilfe:verlauf-suchen).

## Was die Rezeption sieht {#rechte}

- Buchungen, Verschiebungen, Stornos, Status, Kunden und Behandlungen sieht, wer den Kalender sieht.
- **Zahlungen** (zum Termin, Anzahlungen und Verkäufe ohne Termin wie Gutscheine oder Artikel) sieht, wer die Kassa sieht (Recht „Termine buchen“).
- **Anfragen** (Eingang über Mail, SMS, WhatsApp, Anruf oder Treatwell; gebucht, Zeiten vorgeschlagen, als erledigt markiert, Termin storniert, Antwort freigegeben) sieht, wer die Seite **Anfragen** sieht (Recht „Termine buchen“). Text, Notiz und Antwort der Nachricht stehen nicht im Verlauf, nur in der Anfrage selbst.
- **Dienstplan**, **Zugänge** und Firmendaten sehen nur Inhaber und Manager.
- Inhalte von Gesundheitsdaten und der Personalakte stehen nie im Verlauf.

## Gut zu wissen {#gut-zu-wissen}

- Der Verlauf eines einzelnen Termins steht auch im Termin-Panel, siehe [Verlauf eines Termins](hilfe:kalender-termin-verlauf).
- Der Chip **Storniert n** in der Kopfzeile des Kalenders öffnet den Verlauf des angezeigten Tages mit dem Filter „Stornos“, siehe [Kopfzeile](hilfe:seite-kalender#kopfzeile).
- Der Verlauf bleibt 3 Jahre und wird mit dem Gast gelöscht.

![Kopfzeile des Kalenders mit dem Chip Storniert 2](images/app/kalender-storniert-zaehler.png)
