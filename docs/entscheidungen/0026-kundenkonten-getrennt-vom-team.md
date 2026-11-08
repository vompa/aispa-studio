# 0026. Kundenkonten getrennt von den Teamkonten

- **Status:** angenommen
- **Datum:** 17.10.2026
- **Entschieden von:** Claude (autonom, siehe ADR 0019)

## Kontext

Kunden melden sich im Online-Bereich an; ein Kundenkonto darf nie einen Endpunkt der Rezeption öffnen.

## Optionen

| Option | Bewertung |
| --- | --- |
| Eine Benutzertabelle mit Rolle Kunde | ein Fehler in der Rechteprüfung öffnet Teamdaten |
| Eigene Identity, eigener DbContext, eigenes Cookie | gewählt |

## Entscheidung

`CustomerAccount` ist eine zweite Identity im eigenen `OnlineDbContext` (Schema `online`, Anamnese im Schema `health`) mit eigenem Cookie `aispa.kunde` und Policy `Customer`. Anmeldung per Mail-Code, Passkey, Google oder Apple.

## Begründung

Trennung durch Aufbau statt durch Rechteprüfung.

## Folgen und Stand

Eigene Migrationen mit `--context OnlineDbContext -o Online/Migrations`. Falle: `AddSignInManager<CustomerAccount>` überschreibt den `ISecurityStampValidator` des Teams; `CustomerAuth` entfernt die Kunden-Registrierung wieder, ein Test prüft das.

## Quellen

- [Funktionen, Kundenbereich](../features.md#kundenbereich-online-buchung)
