---
art: seite
bereich: Anfragen
oberflaeche: windows-app
rollen: rezeption, leitung, inhaber, manager
seite: anfragen
stichwoerter: Anfragen, Posteingang, Inbox, Mails, Nachrichten, SMS, WhatsApp, Anruf, Treatwell, KI-Terminannahme, Terminanfrage, Buchungsanfrage, inbox, messages, booking requests
---
# Seite „Anfragen“ in der Windows-App

Die Seite „Anfragen“ ist der Posteingang der KI-Terminannahme: Anfragen per Mail, SMS, WhatsApp, Anruf und Treatwell. Die KI liest jede Nachricht, erkennt den Wunsch, sucht freie Zeiten und entwirft eine Antwort. Gesendet wird erst, wenn die Rezeption freigibt.

Die Seite sehen alle, die Termine buchen dürfen (Inhaber, Standortleitung, Rezeption), und nur, wenn die Terminannahme eingeschaltet ist. Im Einzelplatz-Betrieb ist sie meist aus. Die Zahl am Symbol in der Navigation nennt die Nachrichten, die auf eine Entscheidung warten.

![Posteingang mit geöffneter Anfrage](images/app/posteingang.png)

## Kopfzeile {#kopfzeile}

- **nur „Prüfen“**: zeigt nur Nachrichten, bei denen die Rezeption entscheiden muss.
- „Live“ oder „Nicht live“: ob neue Nachrichten sofort erscheinen oder die App regelmäßig nachfragt.
- Der Knopf mit den Pfeilen (**Aktualisieren**) lädt die Liste neu.

## Liste {#liste}

Links die Nachrichten mit Status, Absender, Kanal, Uhrzeit, Betreff und dem, was die KI erkannt hat. Die Status:

- **In Arbeit**: die KI verarbeitet die Nachricht noch.
- **Prüfen**: die Rezeption muss entscheiden.
- **Vorschlag**: die KI hat freie Zeiten vorgeschlagen.
- **Eingetragen**: der Termin steht im Kalender.
- **Storniert**: ein Termin wurde auf Wunsch storniert.
- **Erledigt**: ohne Termin und Antwort abgeschlossen.
- **Fehler**: die Verarbeitung ist fehlgeschlagen.

## Einzelheiten {#einzelheiten}

Rechts die gewählte Nachricht: Absender und Status (beim erkannten Kunden mit Initialen, siehe [Kundenübersicht](hilfe:seite-anfragen#kundenuebersicht)), darunter gleich die Nachricht selbst (bei fremder Sprache mit **DE**), was die KI erkannt hat, bei unbekanntem Absender „Wer ist das?“ mit Vorschlägen (**Zuordnen**, **Neu anlegen**, **Adresse/Nummer merken**) bzw. nach der Entscheidung „Zugeordnet: …“ mit **Zuordnung lösen**, eine vorgemerkte Zeit mit **Bestätigen** und **Freigeben**, bei einer Absage **Termin stornieren** und der Antwortentwurf mit **Entwurf speichern**, **Senden** und den Knöpfen zum Überarbeiten mit KI. Neben einer Terminanfrage steht rechts **Freie Zeit** mit **Vormerken und vorschlagen**. Ganz unten **Erneut verarbeiten** (bei Fehler) und **Erledigt, ohne Termin und Antwort**. Bei einer Treatwell-Buchung stehen dazu „Vorab über Treatwell bezahlt: …“ oder „Zahlt vor Ort“ und der Abschnitt „In Treatwell noch annehmen“ mit **Erledigt**; siehe [Treatwell-Buchung im Posteingang](hilfe:anfragen-treatwell-buchung).

## Kundenübersicht {#kundenuebersicht}

Ist der Absender einem Kunden zugeordnet, stehen oben die **Initialen** und der Name aus der Kundenkarte, darunter der Absender, wie er geschrieben hat. Ein Klick auf Initialen oder Namen zeigt die Kundenübersicht, dieselbe wie im [Kalender](hilfe:seite-kalender#kundenuebersicht): Anrede, Telefon und Mailadresse zum Kopieren (daneben steht kurz „Kopiert.“), Besuche in zwölf Monaten, nächste Termine, Kennzeichen, Notiz, Wünsche und offene Pakete. **Kundenkarte öffnen** wechselt zu den Kunden, **Bearbeiten** öffnet dort gleich die Kundendaten. Unter „Wer ist das?“ zeigt ein Klick auf den Namen eines Vorschlags dieselbe Übersicht.

![Erkannter Absender mit geöffneter Kundenübersicht](images/app/posteingang-kundenuebersicht.png)

Bei einem unbekannten Absender stehen nur Name und Mailadresse bzw. Nummer; Initialen und Übersicht gibt es erst nach dem [Zuordnen](hilfe:anfragen-wer-ist-das#zuordnen).

## Tastenkürzel {#tasten}

Außerhalb von Eingabefeldern:

- **O** wechselt bei übersetzten Nachrichten zwischen Original und Deutsch (wie **DE**).
- **Strg+Z** holt die Fassung des Entwurfs vor der letzten KI-Überarbeitung zurück.

## Abläufe {#ablaeufe}

- [Absender einem Kunden zuordnen („Wer ist das?“)](hilfe:anfragen-wer-ist-das)
- [Anfrage annehmen (vorgeschlagene Zeit buchen)](hilfe:anfragen-annehmen)
- [Antwort prüfen, überarbeiten und senden](hilfe:anfragen-antwort-freigeben)
- [Fremdsprachige Nachricht übersetzen](hilfe:anfragen-uebersetzen)
- [Absage eines Gasts stornieren](hilfe:anfragen-absage-stornieren)
- [Anfrage ohne Termin erledigen](hilfe:anfragen-erledigen)
