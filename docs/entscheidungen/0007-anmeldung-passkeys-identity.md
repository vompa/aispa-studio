# 0007. Anmeldung ohne Passwort mit ASP.NET Core Identity und Passkeys

- **Status:** angenommen
- **Datum:** 17.10.2026
- **Entschieden von:** Auftraggeber

## Kontext

Auftraggeber: „Die Benutzerverwaltung soll so einfach wie möglich gehalten werden, nimm dazu moderne Ansätze.“ Das Hosting-Konzept hatte Microsoft Entra External ID vorgesehen.

## Optionen

| Option | Bewertung |
| --- | --- |
| Passwörter mit Zwei-Faktor | mehr Aufwand und Support |
| Externer Anbieter (Auth0, Entra, Okta) | Kosten pro Benutzer, Daten außerhalb unserer Kontrolle |
| Keycloak selbst betreiben | ein weiterer Dienst; Identity in .NET reicht |
| Freie Rechtevergabe pro Person | fehleranfällig und schwer zu prüfen |
| Identity mit Passkeys, Einladung per Link, Rollenvorlagen | gewählt |

## Entscheidung

Selbst betrieben mit ASP.NET Core Identity und Passkeys, nicht Entra External ID. Einladen statt Anlegen (Link, Passkey beim ersten Öffnen), Rollenvorlagen je Standort statt Häkchenlisten, später PIN-Schnellwechsel am Empfang, zeitlich begrenzte Zugänge und optional SSO über OIDC. Kunden buchen als Gast oder mit Code, Passkey, Google oder Apple.

## Begründung

Passwörter sind die häufigste Ursache für Support und Sicherheitslücken. .NET 10 kann Passkeys direkt. Anmeldedaten bleiben bei uns, in der EU und portabel, ohne Kosten pro Benutzer.

## Folgen und Stand

Einladung per Link (7 Tage, einmalig) in PR #6. Kundenkonten sind eine eigene Identity (PR #9, siehe [ADR 0026](0026-kundenkonten-getrennt-vom-team.md)). Die Windows-App meldet sich per Einmal-Code aus dem Web an (PR #20, [ADR 0027](0027-windows-app-anmeldung-per-einmal-code.md)). PIN-Wechsel und SSO sind offen.

## Quellen

- [Technik-Stack Oberflächen](../konzepte/technik-stack-oberflaechen.md)
