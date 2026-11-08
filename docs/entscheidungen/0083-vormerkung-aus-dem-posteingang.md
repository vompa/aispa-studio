# 0083. Vormerkung aus dem Posteingang

- **Status:** angenommen
- **Datum:** 29.10.2026
- **Entschieden von:** Auftraggeber (Auftrag und Antworten vom 29.10.2026); Umsetzung und Detailentscheidungen: Claude, autonom nach ADR 0019

## Kontext

Bisher schlug der Posteingang dem Gast Zeiten nur als Text vor; bis der Gast antwortete, konnte jemand anderes die Zeit buchen. Der Entwurfstext versprach trotzdem „dann reserviere ich ihn gleich für Sie“. Auftraggeber (29.10.2026): Rechts neben der Nachricht steht die freie Zeit; wird eine gewählt, wird sie dem Gast vorgeschlagen und ein Blockierungstermin auf den Gast eingetragen. Meldet sich der Gast nicht, erinnert eine Meldung im Kalender, je nach Termintag. Auf Rückfrage: Die Vormerkung bleibt, bis die Rezeption entscheidet; die Erinnerung richtet sich nach dem Termintag.

## Optionen

| Option | Bewertung |
| --- | --- |
| A: neuer Status `Held` in `AppointmentStatus` | sauber benannt, aber jeder Ort, der Status abfragt (Berichte, Kassa, Abgleich, Web), müsste ihn kennen; großer Umbau |
| B: gebuchter Termin mit Kennzeichen „vorgemerkt“ (wie „In Treatwell noch annehmen“, ADR 0062) | gewählt: belegt den Kalender sofort über denselben Planer, Freigeben ist ein Storno, wenig Umbau |
| C: eigene Sperre ohne Termin | der Gast stünde nicht im Kalender, Bestätigen müsste neu buchen und könnte scheitern |

## Entscheidung

1. **Vormerken:** `POST /api/inbox/{mailId}/hold` mit Standort, Behandlung, Beginn und optional Person, Raum, Gerät. Der Server bucht über den `BookingService` einen Termin für den zugeordneten Gast (dieselbe Zuordnungsregel wie beim Annehmen, ADR 0061) und setzt `Appointment.HeldSince`, `HoldMailId` und `HoldRemindAt`. Die Anfrage steht danach auf „Vorgeschlagen“ mit diesem Termin, und der Antwortentwurf wird neu geschrieben: Die Zeit ist für den Gast freigehalten, er soll kurz bestätigen. Gesendet wird wie bisher erst nach Prüfung durch die Rezeption.
2. **Erinnerungszeitpunkt** (`HoldReminder.RemindAt`, Ortszeit des Standorts, Tagesstart = erste Öffnung des Tages, ohne Öffnung 8:00): Termin am selben Tag → 1 Stunde nach dem Vormerken, aber spätestens 1 Stunde vor Beginn; Termin am nächsten Tag → bis 12 Uhr vorgemerkt 3 Stunden später, danach zum Tagesstart des Termintags; Termin später → zum Tagesstart des Vortags. Nie vor dem Vormerken.
3. **Antwort des Gastes:** Antwortet der Gast auf den gesendeten Vorschlag (dieselbe Erkennung wie bei Vorschlägen, 14 Tage), sucht die Terminannahme keine neuen Zeiten. Die neue Nachricht zeigt die Vormerkung zum Bestätigen oder Freigeben, und am Termin steht `HoldAnsweredAt`.
4. **Meldung:** Der Kalender zeigt „Gast hat sich nicht gemeldet“, sobald `HoldRemindAt` erreicht ist und keine Antwort da ist, mit „Bestätigen“ und „Freigeben“. Die Regel steht in der API; die App vergleicht nur mit der Uhr.
5. **Bestätigen** (`POST /api/appointments/{id}/hold/confirm`): Das Kennzeichen fällt weg, die Anfrage steht auf „Gebucht“, und die Bestätigung an den Gast wird neu entworfen. **Freigeben** (`…/hold/release`): Der Termin wird storniert („Vormerkung freigegeben“), ohne Nachricht an den Gast; die Anfrage steht wieder auf „Prüfen“. Nichts läuft von selbst ab.
6. **Vorgemerkte Termine** bekommen keine Termin-Erinnerung an den Gast und stehen im Kalender mit dem Hinweis „Vorgemerkt“.

## Begründung

Der Gast hat Sicherheit, dass die Zeit frei bleibt, und die Rezeption sieht im Kalender, warum die Zeit belegt ist. Ein Kennzeichen am gebuchten Termin nutzt Planer, Konfliktprüfung, Live-Verteilung und Warteliste unverändert. Kein automatisches Ablaufen, weil der Auftraggeber es so entschieden hat: Ein still verschwundener Termin wäre an der Rezeption schwer zu erklären.

## Folgen und Stand

- Code: `Scheduling/BookingService.Holds.cs`, `Scheduling/HoldReminder.cs`, Endpunkte in `MailIntakeEndpoints` und `CalendarEndpoints`, Entwurfsfall `ReplyCase.Held`; Windows-App: Posteingang (freie Zeit rechts) und Meldungen.
- Berichte und Auswertungen zählen vorgemerkte Termine wie gebuchte; offen ist, ob sie dort getrennt erscheinen sollen.
- Die Team-Oberfläche (Web) kennt das Kennzeichen noch nicht.

## Quellen

- Auftrag des Auftraggebers vom 29.10.2026 (Posteingang vereinfachen), [ADR 0062](0062-absender-regeln-und-treatwell-sofort-blocken.md), [ADR 0061](0061-kundenzuordnung-eingehender-nachrichten.md), [ADR 0009](0009-terminannahme-alle-kanaele.md)
