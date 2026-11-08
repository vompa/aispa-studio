# 0043. Anrufe: Ansage mit Hinweis, eine Aufnahme, Antwort per SMS

- **Status:** angenommen
- **Datum:** 18.10.2026
- **Entschieden von:** Claude (autonom, siehe ADR 0019)

## Kontext

Vorgabe des Auftraggebers: Terminannahme über alle Kanäle, auch am Telefon, mit Text und Sprache. Bisher musste eine Telefonanlage
die Aufnahme selbst an `POST /api/hooks/voice/recordings` schicken; die meisten Day-Spas haben so eine Anlage nicht.
Mit Twilio Programmable Voice kann AISPA Studio den Anruf selbst annehmen. Dafür ist zu entscheiden, wie weit der
Sprachassistent am Telefon geht und was mit der Aufnahme passiert.

Ein Anruf aufzunehmen ist nur mit Wissen des Anrufers zulässig (§ 120 StGB in Österreich, § 201 StGB in Deutschland,
Art. 179ter StGB in der Schweiz); die DSGVO verlangt dazu Information und Datenminimierung. Die KI-Verordnung
verlangt, dass Menschen erfahren, wenn sie mit einem KI-System zu tun haben.

## Optionen

| Option | Bewertung |
| --- | --- |
| Gesprächs-Bot: KI spricht mit dem Gast, fragt nach, bucht am Telefon | am bequemsten, aber Rückfragen in Echtzeit, Sprachausgabe je Sprache, Buchung ohne Prüfung durch die Rezeption; viel Technik, Fehler hört der Gast sofort |
| Nur Mailbox: Aufnahme landet als Datei bei der Rezeption | einfach, aber keine KI und niemand hört die Aufnahmen rechtzeitig ab |
| Ansage, eine Aufnahme, Abschrift, Antwort per SMS nach Freigabe | gewählt: der Gast spricht einmal, dieselbe Terminannahme wie bei SMS, die Rezeption prüft vor dem Senden |

## Entscheidung

- Twilio nimmt den Anruf an und fragt `POST /api/hooks/twilio/voice`. Die Ansage nennt das Spa, sagt vor dem Ton, dass
  der Anruf aufgenommen und von einer KI-Terminannahme ausgewertet wird und dass die Antwort per SMS kommt; zuerst
  Deutsch, dann kurz Englisch, weil die Sprache des Anrufers erst mit der Aufnahme feststeht. Wer das nicht möchte,
  legt vor dem Ton auf, dann wird nichts gespeichert.
- Es gibt eine Aufnahme (höchstens `Voice:MaxRecordingSeconds`, Vorgabe 120 Sekunden), keinen Dialog. Nach der
  Aufnahme bedankt sich die Ansage und legt auf.
- Die Aufnahme wird in der Queue geholt, mit `ISpeechToText` abgeschrieben (Azure in der EU, ADR zu #40) und wie eine
  SMS durch die Terminannahme geführt; die Antwort geht nach Freigabe per SMS vom Spa. Nach gelungener Abschrift wird
  die Aufnahme bei Twilio gelöscht. AISPA Studio speichert nur die Abschrift. Scheitert die Abschrift, bleibt die
  Aufnahme bei Twilio, und der Anruf steht mit Link zur Aufnahme im Posteingang, damit die Rezeption zurückruft.
- Jeder Aufruf ist signiert (`X-Twilio-Signature`); Zugangsdaten gehen nur an die Twilio-API, auch wenn ein Formular
  eine andere Adresse nennt.
- Anonyme Anrufer bekommen die Ansage, aber ohne Nummer gibt es keine Antwort und nichts im Posteingang.

## Begründung

Der Weg nutzt alles, was es schon gibt (Erkennung, Kalenderprüfung, Antwortentwurf, Freigabe, SMS-Versand) und hält
die Rezeption in der Verantwortung: kein Termin entsteht am Telefon ohne Prüfung. Die Ansage erfüllt die Hinweispflichten,
das Löschen bei Twilio die Datenminimierung. Ein echter Gesprächs-Bot bleibt möglich, wenn Spas ihn wollen; er braucht
dann Sprachausgabe je Sprache und eine eigene Entscheidung zur Buchung ohne Freigabe.

## Folgen und Stand

- Code: `MailIntake/Channels/TwilioVoice.cs` (Ansage, Aufnahme holen und löschen, Handler), Webhooks in
  `ChannelHookEndpoints`.
- Offen: Ansage in weiteren Sprachen und je Spa anpassbar, Rückruf statt SMS für Anrufer ohne Mobilnummer (Festnetz),
  Gesprächs-Bot mit Rückfragen, andere Telefonie-Anbieter (z. B. österreichische SIP-Anbieter) als weiterer Adapter.

## Quellen

- § 120 StGB (AT), § 201 StGB (DE), Art. 179ter StGB (CH); Art. 5 und 13 DSGVO; Art. 50 KI-Verordnung
- Twilio: TwiML `Record`, `Say`; Signatur der Webhooks
- PR-Protokoll, Abschnitt zu diesem PR
