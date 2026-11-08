# 0082. Änderungsverlauf über den Änderungs-Trigger

- **Status:** angenommen
- **Datum:** 28.10.2026
- **Entschieden von:** Auftraggeber (Rulings zu Ä184 in Runde R04, 28.10.2026, Empfehlungen von Claude angenommen); Umsetzung und Detailentscheidungen: Claude, autonom nach ADR 0019 (Runde R04, Slice 90)

## Kontext

Auftraggeber (Ä184): Es gibt kein Protokoll, wer wann über welchen Kanal etwas angelegt, geändert oder gelöscht hat. Ein Termin kennt nur `CreatedAt`, `UpdatedAt` und `Source`; wer ihn verschoben oder storniert hat und wann, steht nirgends. Gewünscht ist ein Verlauf je Datensatz, zuerst für Termine („angelegt per SMS am … um …, geändert von … am … um …, storniert …“, Abschnitt „Verlauf“ im Termin-Panel), danach für die übrigen Masken.

Für den Abgleich des Einzelplatzes gibt es schon einen Trigger an jeder Fachtabelle ([ADR 0041](0041-einzelplatz-standalone-und-zuschaltbare-webmodule.md), `Sync/ChangeLog.cs`): er schreibt Tabelle, Schlüssel, Mandant und Art jeder Änderung nach `sync.changes`, auch aus `ExecuteUpdate`, rohem SQL und dem Online-Kontext. Er kennt weder Person noch Kanal noch Werte, und sein Protokoll wird nach dem Abgleich aufgeräumt.

## Optionen

| Option | Bewertung |
| --- | --- |
| A: Verlauf in den Diensten schreiben (`BookingService` u. a.) | jede Änderung braucht eigenen Code; `ExecuteUpdate`, rohes SQL und der Online-Kontext gehen leicht verloren |
| B: EF-`SaveChanges`-Interceptor mit alten und neuen Werten | sieht `ExecuteUpdate`, rohes SQL und COPY nicht |
| C: Trigger neben dem Abgleich-Trigger, Person und Kanal als Sitzungseinstellung je Anfrage | gewählt: erfasst jeden Weg in derselben Transaktion; die Fachmodule wissen nichts davon |
| D: `sync.changes` um Person, Kanal und Werte erweitern | anderes Aufräumen (Abgleich) als der Verlauf (3 Jahre); Werte würden mit abgeglichen |

## Entscheidung

1. **Tabelle `sync.history`** neben `sync.changes`: Tabelle, Schlüssel, Mandant, Gast (`customer_id`), Art (I/U/D), Zeitpunkt (`now()` der Transaktion), Person (Benutzer-Id), Kanal und bei Änderungen die alten und neuen Werte der geänderten Spalten (`{"Spalte": [alt, neu]}`, ohne `UpdatedAt`). Eine Änderung, die nur `UpdatedAt` berührt, schreibt nichts. Anlegen und Löschen tragen keine Werte. Eingespielt wie das Abgleichprotokoll nach jeder Migration (`ChangeLog.AttachAsync`, `Sync/ChangeHistory.cs`), ohne EF-Migration.
2. **Umfang in zwei Schritten:** Slice 90 hängt den Trigger `aispa_history_log` an `aispa.Appointments` und an jede Tabelle im Schema `health`; Slice 91 (umgesetzt) ergänzt Kundendaten und ihre E-Mail-Adressen, Behandlungen mit Preisen je Standort und Preisregeln, Dienstplan mit Abwesenheiten sowie Zugänge mit Rollen (`ChangeHistory.Tables`; `sync.history` bekommt dafür `subject` und `context`). Einzelne Einstellungen (Farben, Layout) bekommen keinen Verlauf. Kassa-Belege auch nicht: dort bleiben RKSV/TSE die Quelle.
3. **Person und Kanal je Anfrage:** `ChangeActor` bestimmt sie: angemeldetes Team = Kanal `Team` mit Benutzer-Id, Kundenbereich (`/api/online…`) = `Online` ohne Person, ohne Anfrage = `Job`. Hintergrundjobs im Namen eines Kanals setzen ihn mit `ChangeActor.Use(…)` (die Terminannahme den Kanal der Nachricht: `Sms`, `Mail`, `WhatsApp`, `Phone`, `Treatwell`). Der `ChangeActorInterceptor` (beide Kontexte) setzt `aispa.user_id` und `aispa.channel` als Sitzungseinstellung vor dem ersten schreibenden Befehl je geöffneter Verbindung. Was der Abgleich einspielt (`aispa.sync_origin`), steht mit Kanal `Sync`.
4. **Gesundheitsdaten:** Tabellen im Schema `health` erscheinen nur mit Art, Person, Kanal und Zeit, nie mit Werten, auch nicht verschlüsselt.
5. **Sichtbarkeit:** Den Verlauf sieht, wer den Datensatz sehen darf. Für Termine `GET /api/appointments/{id}/history` mit dem Recht des Kalenders wie `GET /api/appointments/{id}`; Kunden und Behandlungen (Slice 91) ebenso mit dem Kalenderrecht (`GET /api/customers/{id}/history`, `GET /api/treatments/{id}/history`); Dienstplan, Benutzer und Rechte nur Inhaber und Manager (`GET /api/employees/{id}/roster-history`, `GET /api/staff/{userId}/history`, sonst 403). Die Sätze für Menschen baut der Server (`AppointmentHistory`): „Angelegt per SMS“, „Verschoben von Lena: 14:00 → 15:30“, „Storniert von Lena: Gast krank“, Uhrzeiten in der Zeitzone des Standorts, Personen mit ihrem Anzeigenamen zur Zeit des Lesens.
6. **Aufbewahrung:** 3 Jahre ab der Änderung (Verjährung nach § 1489 ABGB); `HistoryCleanup` löscht täglich, was älter ist. Die Kassa bleibt bei den 7 Jahren der BAO.
7. **Löschen mit dem Gast:** Wird ein Gast gelöscht, löscht der Trigger `aispa_history_forget` an `aispa.Customers` den Verlauf aller Zeilen, die ihm gehören (`customer_id`: `Id` bei Kunden, sonst `CustomerId` der Zeile; wechselt der Gast eines Termins, wandert der Verlauf mit). Für ein späteres Anonymisieren steht `sync.forget_customer_history(uuid)` bereit; das Anonymisieren selbst gibt es noch nicht.

## Begründung

Der Trigger sieht jede Änderung, gleich über welchen Weg, und schreibt sie in derselben Transaktion; was danach in den Diensten dazukommt, muss nicht an den Verlauf denken. Eine eigene Tabelle trennt die Lebensdauer (Abgleich räumt auf, der Verlauf bleibt 3 Jahre) und hält Werte aus dem Abgleich heraus. Die Sitzungseinstellung bringt Person und Kanal in den Trigger, ohne die Fachtabellen um Spalten zu erweitern. Der Interceptor setzt sie nur vor dem ersten Schreiben je Verbindung, lesende Aufrufe kosten nichts.

## Folgen und Stand

- Code: `src/AispaStudio.Api/Sync/ChangeHistory.cs`, `ChangeActor.cs`, `ChangeActorInterceptor.cs`, `HistoryCleanup.cs`, `Scheduling/AppointmentHistory.cs`; Windows-App `HistorySectionViewModel`, Abschnitt „Verlauf“ im Termin-Panel; Beispieldaten `Demo/DemoChangeHistory.cs`.
- Npgsql setzt die Sitzung bei der Rückgabe in den Pool zurück; rohe Npgsql-Wege ohne den Interceptor (COPY der Beispieldaten, der Abgleich) stehen deshalb als `Job` bzw. `Sync`.
- Die Beispieldaten erzeugen ihren Verlauf am Ende aus den fertigen Terminen (gleich auf beiden Wegen, `DemoBulkTests`), nicht beim Einspielen.
- Termine von vor diesem Stand haben keinen Verlauf; der Abschnitt zeigt dann „Noch keine Einträge.“
- Der Verlauf wird nicht abgeglichen: ein Einzelplatz zeigt Änderungen der zentralen Seite als „beim Abgleich“.
- Slice 91: Das Löschen eines Gastes schreibt keine eigene Zeile mehr (sonst bliebe nach `aispa_history_forget` eine Löschzeile); die Sätze für die übrigen Masken baut `Sync/EntityHistory.cs`, die Satzteile „von Lena“, „per SMS“ liegen in `Sync/HistoryActor.cs`; Gesundheitsdaten des Gastes erscheinen als „Anamnese geändert von Lena“.
- Offen: Anonymisieren eines Gastes (ruft dann `sync.forget_customer_history`); die Beispieldaten haben nur den Verlauf der Termine; Standortleitung und Backoffice sehen den Verlauf von Dienstplan und Rechten nicht (Ä190).

## Quellen

- Runde R04, Befund Ä184 und Rulings in `work/rounds/R04-booking-test-notes.md`
- `work/slices/90-appointment-history.md`, `work/slices/91-history-other-forms.md`
- [ADR 0041](0041-einzelplatz-standalone-und-zuschaltbare-webmodule.md) (Änderungsprotokoll des Abgleichs), [ADR 0048](0048-schluessel-fuer-gesundheitsdaten-beim-abgleich.md) (Gesundheitsdaten beim Abgleich)
