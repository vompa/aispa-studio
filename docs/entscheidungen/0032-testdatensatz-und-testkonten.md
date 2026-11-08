# 0032. Vollständiger Testdatensatz und Testkonten ohne Passkey in der Entwicklung

- **Status:** angenommen
- **Datum:** 18.10.2026
- **Entschieden von:** Auftraggeber (Wunsch), Umsetzung Claude (autonom, siehe ADR 0019)

## Kontext

Auftraggeber: „im development braucht man einen sinnvollen testdatensatz“. Bisher zeigte das Beispiel-Spa (ADR 0029) nur Stammdaten und Termine für drei Tage; Kassa, Berichte, Vergleich, Lohn und Posteingang waren leer, und jede Rolle musste man sich erst mit „Testkonto anlegen“ und einem Passkey selbst bauen.

## Optionen

| Option | Bewertung |
| --- | --- |
| SQL-Dump mit festen Daten | schnell geladen, veraltet aber mit jeder Migration und umgeht Planer und Kassa (keine gültige Signaturkette) |
| Daten direkt in die Tabellen schreiben | schnell, aber Belegkette, Zähler und Belegungen müssten nachgebaut werden |
| Betrieb über die echten Dienste nachspielen, mit zurückgestellter Uhr | gewählt: dieselben Prüfungen wie im Betrieb, Belege signiert, bleibt mit dem Code aktuell |

## Entscheidung

Beim ersten Start in der Entwicklungsumgebung spielt `Demo.DemoSeeder` acht Wochen Betrieb nach und bucht vier Wochen voraus, über `BookingService`, `PosService`, `AnamnesisStore`, `MailIngestion` und die Queue, mit einer `DemoClock` für Zeitpunkte in der Vergangenheit. Für jede Rolle gibt es Testkonten unter `@demo.aispa.test`; in der Entwicklungsumgebung meldet `/api/auth/dev/login` sie ohne Passkey an, die Anmeldeseite bietet dafür eine Auswahl. Echte Konten bleiben auch dort hinter ihrem Passkey.

## Begründung

Der Auftraggeber und die Threads (z. B. die Windows-App) sehen sofort einen gefüllten Betrieb in jeder Rolle. Weil alles durch die echten Dienste läuft, fällt ein Fehler in Planer oder Kassa schon beim Anlegen auf.

## Folgen und Stand

Der erste Start dauert zwei bis drei Minuten; die API hört erst danach. Neue Fachbereiche ergänzen ihre Beispieldaten in `src/AispaStudio.Api/Demo`. Die Anmeldung ohne Passkey gibt es nur, wenn die API in der Entwicklungsumgebung läuft (wie `dev/register`).

## Quellen

- [Testdatensatz](../testdaten.md)
