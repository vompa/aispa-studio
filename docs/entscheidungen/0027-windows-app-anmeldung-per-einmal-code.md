# 0027. Windows-App meldet sich per Einmal-Code aus dem Web an

- **Status:** angenommen
- **Datum:** 18.10.2026
- **Entschieden von:** Claude (autonom, siehe ADR 0019)

## Kontext

Passkeys sind im Browser einfach, in einer Desktop-App unter Avalonia aber aufwendig. Die Rezeption soll trotzdem ohne Passwort arbeiten.

## Optionen

| Option | Bewertung |
| --- | --- |
| Passkey direkt in der App | aufwendige Plattform-Integration |
| Passwort für die App | widerspricht ADR 0007 |
| Einmal-Code aus dem angemeldeten Web | gewählt |

## Entscheidung

Im Web erzeugt „Windows-App verbinden“ einen Einmal-Code, den man in der App eingibt. Die App speichert ihr Token mit DPAPI.

## Begründung

Passwortlos, ohne Plattform-Sonderwege.

## Folgen und Stand

PIN-Schnellwechsel am Empfang ist weiterhin offen.

## Quellen

- [Entwicklung, Lokal mit Docker testen](../development.md#lokal-mit-docker-testen-ohne-net-und-node)
