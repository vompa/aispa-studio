# 0009. Automatische Terminannahme über alle Kanäle in einer Buchungslogik

- **Status:** angenommen
- **Datum:** 17.10.2026
- **Entschieden von:** Auftraggeber (Anforderung), Empfehlung Claude

## Kontext

Auftraggeber: „Eine automatische Terminentgegennahme per Mail, Telefon, SMS und was sonst noch sinnvoll ist, mit Text und Sprache in den wichtigsten Sprachen, aber auch erweiterbar.“ Kein Anbieter verarbeitet E-Mails automatisch; angeflanschte KI-Telefon-Tools sehen den Kalender nur von außen.

## Optionen

| Option | Bewertung |
| --- | --- |
| Fremden KI-Empfang anbinden (Ainora, fonio.ai) | kennt Räume und Paarlogik nicht; als Übergang für Telefon denkbar |
| Nur Online-Buchung | viele Spa-Gäste rufen weiterhin an |
| Jeder Kanal mit eigener Logik | führt zu Doppelbuchungen |
| Gemeinsamer Posteingang, eine Buchungslogik | gewählt |

## Entscheidung

Ein Posteingang für Mail, SMS, WhatsApp, Telefon und Chat. Die KI bucht über denselben Planer wie die Rezeption. Was sie nicht sicher buchen kann, geht als Anfrage mit Vorschlag an die Rezeption. Gesundheitsfragen gehen immer an einen Menschen. Sprachen als erweiterbare Pakete.

## Begründung

Nur so gelten für alle Kanäle dieselben Regeln gegen Doppelbuchungen.

## Folgen und Stand

Umgesetzt in PR #8 (Mail, Treatwell-Mails, ICS), #13 (IMAP, Microsoft Graph), #17 (SMS, WhatsApp, Anruf). `AutoBook` an, `AutoSend` aus: die Rezeption gibt Antworten frei. Offen: Spracherkennung durch einen EU-Dienst, ein Postfach je Spa, WhatsApp-Vorlagen.
