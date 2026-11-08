# 0081. Wartezeit vor der Nachricht an den Gast

- **Status:** angenommen
- **Datum:** 26.10.2026
- **Entschieden von:** Auftraggeber (Ruling zu Ä95 beim Test von R02, 26.10.2026); Umsetzung und Detailentscheidungen: Claude, autonom nach ADR 0019 (Runde R03, Slice 73)

## Kontext

Sagt das Team einen Termin ab, geht heute sofort eine Mail oder SMS an den Gast ([ADR 0073](0073-schliesszeiten-und-terminabsage.md)). Wer versehentlich absagt, kann den Termin nicht mehr zurückholen, ohne dass der Gast die Absage schon hat. Auftraggeber (Ä95): Die Nachricht an den Gast wartet mindestens 10 Minuten und ist einstellbar, damit ein Versehen noch zurückgenommen werden kann.

Die einzige Absage des Teams mit Nachricht an den Gast ist die wegen einer Schließzeit (`ClosureCancellations`, Protokollzeile `ClosureNotice`, Queue-Nachricht `SendClosureNotice`). Eine Verschiebung des Teams verschickt keine Nachricht, nur die Erinnerungen folgen dem neuen Beginn. Gäste, die selbst absagen oder umbuchen, laufen über den Kundenbereich und die Terminannahme.

## Optionen

| Option | Bewertung |
| --- | --- |
| A: sofort senden (Stand) | ein Versehen erreicht den Gast, bevor das Team es merkt |
| B: Nachricht erst bei Bestätigung durch eine zweite Person | zu schwer für Spas mit ein bis zwei Personen an der Rezeption |
| C: feste Wartezeit je Standort, in der „Nicht senden“ und „Jetzt senden“ gehen | gewählt: wirkt ohne Zutun, Standard 10 Minuten, einstellbar |
| D: Wartezeit im Prozess halten (Timer) | geht beim Neustart verloren; die Queue hält geplante Nachrichten dauerhaft |

## Entscheidung

1. **Einstellung je Standort:** `Location.GuestMessageDelayMinutes`, 0 bis 60 Minuten, Standard 10 (auch für bestehende Standorte). Endpunkt `GET/PUT /api/locations/{id}/guest-message-delay` mit dem Recht „Standorte verwalten“; die Maske steht auf der Seite „Schließzeiten“. 0 sendet sofort.
2. **Sendezeit:** `GuestMessageTiming.SendAt(jetzt, Beginn des Termins, Wartezeit)`: sofort, wenn die Wartezeit 0 ist oder der Termin in **weniger als 2 Stunden** beginnt; sonst jetzt plus Wartezeit. Genau 2 Stunden gelten als später. Nachrichten an die Warteliste haben keinen Termin; dort gilt nur die Wartezeit.
3. **Planen über die Queue:** Die Protokollzeile bekommt `ScheduledFor`; `bus.ScheduleAsync(new SendClosureNotice(id), sendeZeit)` hält die Zustellung dauerhaft in PostgreSQL (CLAUDE.md: Mails nie im HTTP-Aufruf). Liegt die Sendezeit nicht in der Zukunft, geht die Nachricht wie bisher sofort in die Queue.
4. **„Jetzt senden“ und „Nicht senden“:** `POST …/closures/{id}/notices/{noticeId}/send-now` und `…/cancel` (Rechte „Standorte verwalten“ und „Buchen“). Beide gehen nur, solange die Nachricht im Zustand „Queued“ auf ihre Sendezeit wartet; sonst 409. „Nicht senden“ setzt den neuen Zustand `Cancelled`; der Sender prüft den Zustand und tut nichts, wenn die geplante Queue-Nachricht später trotzdem eintrifft. „Jetzt senden“ zieht die Sendezeit auf jetzt vor und stellt sofort zu; die später eintreffende geplante Nachricht findet „Sent“ vor.
5. **Der Termin bleibt abgesagt,** eine Anzahlung geht sofort zurück. Nur die Nachricht wartet. Ein Zurücknehmen der Absage selbst ist [Slice 74](../../work/slices/74-app-undo-cancel-checkin.md).
6. **Anzeige:** Im Protokoll der Absagen steht bei einer wartenden Nachricht „Nachricht an den Gast geht um 14:32 raus.“ (Ortszeit des Standorts) mit den Knöpfen „Jetzt senden“ und „Nicht senden“.

## Begründung

Eine feste, kurze Wartezeit braucht keine zweite Person und keinen Zusatzklick im Normalfall. Die Queue hält die geplante Zustellung über einen Neustart hinweg. Die Zwei-Stunden-Regel verhindert, dass ein Gast von einer Absage erst erfährt, wenn er schon unterwegs ist.

## Folgen und Stand

- Neue Spalten `Location.GuestMessageDelayMinutes` und `ClosureNotice.ScheduledFor` (Migration `GuestMessageDelay`), neuer Zustand `ClosureNoticeState.Cancelled`.
- Code: `src/AispaStudio.Api/Closures/GuestMessageTiming.cs`, `ClosureCancellations` (`DispatchAsync`, `SendNowAsync`, `WithdrawAsync`), `ClosureNoticeSender`, `ClosureEndpoints`; Web `web/src/closures/GuestDelaySetting.tsx` und das Protokoll in `ClosuresPage.tsx`; Hilfe `schliesszeiten#wartezeit`.
- Die Tests der API setzen die Wartezeit des Testmandanten auf 0 (`TestSpa`), damit Absagen dort wie bisher sofort zugestellt werden; `ClosureGuestDelayTests` stellt sie auf 10.
- **Offen:** Sobald das Team bei einer Verschiebung eine Nachricht an den Gast schickt, nutzt sie `GuestMessageTiming` und `ScheduledFor` (Befund Ä119). Ein Rennen zwischen „Nicht senden“ oder „Jetzt senden“ und einem Versand in derselben Sekunde ist nicht abgesichert (der Sender hat keinen Zustand „wird gesendet“). Die Mail über die Erstattung einer Online-Anzahlung (`PaymentRefunded`) geht weiter sofort raus und verrät dem Gast die Absage vor dem Ende der Wartezeit (Befund Ä121, offen).

## Quellen

- Rundenakte R03, Ä95; [ADR 0073](0073-schliesszeiten-und-terminabsage.md)
- `work/slices/73-api-guest-message-delay.md`, `work/plans/2026-10-26-slice-73.md`
