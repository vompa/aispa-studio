# 0040. Erinnerung an die nächste Behandlung nur mit bestätigter Einwilligung

- **Status:** angenommen
- **Datum:** 18.10.2026
- **Entschieden von:** Claude (autonom, siehe ADR 0019)

## Kontext

Vorgabe des Auftraggebers: KI hilft in jedem Fall. Ein naheliegender Einsatz ist, Stammgäste an die nächste Behandlung zu erinnern
(„Ihre letzte Massage ist sechs Wochen her“), mit einem Text, den die KI persönlich schreibt. Anders als die
Terminerinnerung gehört so eine Mail nicht zu einem gebuchten Termin, sondern soll zu einer neuen
Buchung führen. Das ist Werbung.

Werbung per Mail braucht in Deutschland nach § 7 Abs. 2 UWG und in Österreich nach § 174 TKG 2021 eine vorherige
Einwilligung. Die Ausnahme für Bestandskunden (§ 7 Abs. 3 UWG, entsprechend § 174 TKG) verlangt unter anderem einen Hinweis auf das
Widerspruchsrecht schon bei der Erhebung der Adresse; den gibt es bei bestehenden Gästen nicht nachweisbar. Die Einwilligung
muss das Spa beweisen können, nach Rechtsprechung des BGH mit Double-Opt-in.

## Optionen

| Option | Bewertung |
| --- | --- |
| Bestandskunden-Ausnahme, Abmeldelink in jeder Mail | wenig Aufwand, aber für bestehende Gäste nicht belegbar; Abmahnrisiko beim Spa |
| Häkchen am Empfang, Team bestätigt | einfach, aber kein Nachweis, dass die Adresse dem Gast gehört |
| Team fragt an, Gast bestätigt per Link (Double-Opt-in) | ein Schritt mehr für den Gast, dafür belegbar; gewählt |

## Entscheidung

- Erinnerungen an die nächste Behandlung gehen nur an Gäste mit bestätigter Einwilligung und nur an genau die Adresse, an die
  angefragt wurde. Ändert sich die Mailadresse, ruht die Einwilligung, bis neu angefragt und bestätigt ist.
- Die Rezeption fragt im Termin an. Der Gast bekommt eine Mail ohne Werbung mit Link in den Kundenbereich. Die Seite ändert
  beim Öffnen nichts (Virenscanner rufen Links vorab auf), bestätigt wird mit dem Knopf. Gespeichert werden Zeitpunkt der
  Anfrage, der Bestätigung und die Adresse.
- Nur ein Link, der nach der letzten Anfrage verschickt wurde, bestätigt. Widerrufen geht über jeden Link des Gastes, ohne
  Anmeldung, und am Empfang. Jede Erinnerung hat einen Abmeldelink.
- Höchstens eine Anfrage-Mail je Tag und Gast, höchstens eine Erinnerung je Gast in 30 Tagen, keine, wenn schon ein Termin
  gebucht ist, und keine verspätete mehr als 14 Tage nach dem Fälligwerden.
- Die Funktion ist je Spa ausgeschaltet, bis das Spa sie einschaltet und für Behandlungen einen Abstand einträgt.
- Der persönliche Teil kommt von der KI und bekommt nur Vorname, Behandlung, Spa und Sprache. Nennt der Text Zahlen, Preise,
  Links oder Mailadressen (außer im Namen von Spa oder Behandlung), gilt ein fester Text. Hat die KI geschrieben, steht der
  Hinweis darauf in der Mail.
- Nur per Mail. SMS und WhatsApp brauchen eigene Einwilligungen und bei WhatsApp freigegebene Werbevorlagen.

## Begründung

Der Nachweis liegt beim Spa; ohne Double-Opt-in hätte es im Streitfall nichts in der Hand. Die Grenzen für die Häufigkeit
schützen die Gäste und damit den Ruf des Spas. Dass die KI keine Preise oder Angebote nennen darf, folgt aus der Regel, dass
die KI keine Zahlen erfindet, und verhindert Zusagen, die das Spa nicht gemacht hat.

## Folgen und Stand

- Code: `src/AispaStudio.Api/Retention/` (Einwilligung, Planer, Versand), Seite `web/src/booking/ConsentPage.tsx`, Einstellungen
  unter „Stammdaten“ › „Erinnerungen“, Einwilligung im Termin.
- Erfassung am Empfang (Export/Import, Aufgabe B, 09.11.2026): Die Kundenkarte zeigt den Abschnitt „Werbung per Mail“ mit
  „Ja“, „Nein“ und „Nicht gefragt“ (`PUT /api/customers/{id}/marketing-consent`). „Ja“ setzt `Confirmed` ohne Mail zum
  Bestätigen; vorher fragt die App, ob der Gast mündlich oder schriftlich zugestimmt hat. Die Quelle heißt dann „Rezeption“,
  damit der Nachweis nach Art. 7 Abs. 1 DSGVO erkennbar bleibt: „Team“ steht weiter für die Anfrage per Mail mit
  Double-Opt-in. Die Kundenkarte zeigt die Quelle getrennt („Ja, am Empfang erfasst am … von Lena“ gegen „Ja, per Mail
  bestätigt am …“); die Person liest die API aus dem Verlauf (`RecordedBy`). „Nein“ ist der neue Zustand `Refused` (ohne frühere Einwilligung, anders als `Withdrawn`). Eine schon per Mail
  bestätigte Einwilligung bleibt bei „Ja“ unverändert. Der Kunden-Export zeigt „Ja“, „Nein“ oder „offen“ mit Datum, die
  Newsletter-Liste enthält nur `Confirmed`. Der Kunden-Import (Aufgabe E) setzt `Confirmed` mit Quelle „Import“ nur, wenn eine
  zugeordnete Spalte die Zustimmung ausdrücklich mit Ja belegt, sonst bleibt `None`.
- Offen: Einwilligung beim Online-Buchen anbieten (eigenes Häkchen, nicht vorausgewählt), `List-Unsubscribe`-Kopfzeile für
  Abmelden mit einem Klick im Mailprogramm, SMS und WhatsApp, Schweiz (Art. 3 Abs. 1 lit. o UWG) prüfen, Texte rechtlich
  gegenlesen lassen.

## Quellen

- § 7 UWG (DE), § 174 TKG 2021 (AT), Art. 7 DSGVO (Nachweis der Einwilligung)
- PR-Protokoll, Abschnitt zu diesem PR
