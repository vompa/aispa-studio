# 0052. Firmendaten des Spas zentral, Standorte weichen nur als eigene Gesellschaft ab

- **Status:** angenommen
- **Datum:** 19.10.2026
- **Entschieden von:** Auftraggeber (Firmendaten einstellbar, auch für Zahlungen und Impressum), Ausgestaltung Claude

## Kontext

Auftraggeber: „Die Firmendaten bzw. SPA-Studio-Daten müssen einstellbar sein … für Zahlungen, Anbindungen, Impressum usw.“ Bisher standen Firmenwortlaut, UID und Firmenbuch nur je Standort, IBAN und Empfänger nur an jeder Kasse (beim Anlegen), und die Online-Buchung hatte kein Impressum.

## Optionen

| Option | Bewertung |
| --- | --- |
| Alles je Standort lassen | jede Angabe mehrfach pflegen; kein Ort für Impressum und Zahlungsanbieter |
| Firmendaten am Spa, Standort und Kasse überschreiben feldweise | mischt bei zwei Gesellschaften leicht UID der einen mit Firmenbuch der anderen |
| Firmendaten am Spa, ein Standort mit eigenen Angaben ist eine eigene Gesellschaft | gewählt |

## Entscheidung

- Das Spa (Mandant) hat Firmendaten: Firmenwortlaut, Sitz, Kontakt, Website, UID, Firmenbuch, Bankverbindung und die Angaben fürs Impressum (Unternehmensgegenstand, Aufsichtsbehörde, Kammer und Berufsrecht). Gepflegt über `PUT /api/tenant` mit `ManageOrganization`.
- Rechnungen nehmen die Unternehmensangaben des Standorts, sobald dort eine davon eingetragen ist, sonst vollständig die des Spas (`CompanyProfiles.CompanyFor`). Nie feldweise gemischt.
- Die Sofortüberweisung nimmt das Konto der Kasse, ohne eines das Konto aus den Firmendaten. Das gilt nur, wenn der Standort keine eigene Gesellschaft ist.
- Die Online-Buchung zeigt unter „Impressum“ die Firmendaten ohne Bankverbindung (`GET /api/online/imprint`). Fehlen Anschrift oder Kontakt, stehen die des ersten Standorts. Solange Firmenwortlaut, Anschrift oder E-Mail fehlen, steht ein Hinweis dabei.
- Belegkopf und DSFinV-K-Stammdaten der Kasse bleiben an der Kasse: Sie gehören zur registrierten Kasse und ändern sich nicht nachträglich.

## Begründung

Ein Day-Spa mit bis zu 50 Mitarbeitenden ist fast immer eine Gesellschaft. Einmal pflegen reicht. Wer einen Standort in einer eigenen GmbH führt, trägt ihn dort vollständig ein. Weil nie gemischt wird, kann auf einer Rechnung keine falsche UID stehen.

## Folgen und Stand

- Code: `Tenancy/CompanyProfiles.cs`, `Tenancy/TenantEndpoints.cs`, Seite „Firmendaten“ unter Stammdaten, `booking/ImprintPage.tsx`.
- Offen: Logo; Zahlungsanbieter je Spa (Entscheidung vom Auftraggeber steht aus); Firmendaten als Vorschlag beim Anlegen einer Kasse.
- Rechnungen zeigen die Unternehmensangaben zum Zeitpunkt des Abrufs, wie schon vorher bei den Angaben des Standorts. Eine Änderung der Firmendaten ändert also auch den Nachdruck alter Rechnungen. Für eine spätere Archivierung muss der Stand beim Ausstellen gespeichert werden.

## Quellen

- PR „Firmendaten des Spas einstellbar, Impressum der Online-Buchung“
