# 0024. Mandantentrennung in einer gemeinsamen Datenbank

- **Status:** angenommen
- **Datum:** 17.10.2026
- **Entschieden von:** Claude (autonom, siehe ADR 0019)

## Kontext

Die Software ist SaaS für viele Spas. Kein Spa darf Daten eines anderen sehen (Ergänzende Themen, Sicherheit P1).

## Optionen

| Option | Bewertung |
| --- | --- |
| Eine Datenbank je Spa | starke Trennung, aber viel Betriebsaufwand |
| Schema je Spa | Migrationen je Schema, aufwendig |
| Gemeinsame Tabellen mit `TenantId` und globalem Filter | gewählt |

## Entscheidung

Jede Fachtabelle hat eine Schatten-Spalte `TenantId`, jede Abfrage filtert automatisch, neue Einträge bekommen beim Speichern den Mandanten. Gilt automatisch für alle Entities in `AispaStudio.Api`. Ohne Mandant wirft `SaveChanges`.

## Begründung

Einfacher Betrieb bei vielen kleinen Kunden, ohne dass Fachcode an die Trennung denken muss.

## Folgen und Stand

Hintergrundjobs setzen den Mandanten mit `TenantContext.Use(id)`. Der Kundenbereich erkennt das Spa an `?spa=`, Kopfzeile `X-Aispa-Spa`, Standort, Link-Token oder Kundenkonto. Row-Level-Security in PostgreSQL ist als zusätzliche Absicherung empfohlen, aber noch nicht gebaut.

## Quellen

- [Funktionen, Mandanten](../features.md#mandanten)
- [Ergänzende Themen, Abschnitt 3](../konzepte/ergaenzende-themen.md#3-sicherheit)
