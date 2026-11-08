# 0030. Lokaler Test mit Docker Compose und Windows-App als Download

- **Status:** angenommen
- **Datum:** 18.10.2026
- **Entschieden von:** Auftraggeber (Wunsch), Umsetzung Claude

## Kontext

Auftraggeber: „Ich möchte das zuerst lokal testen, kann man den Windows-Client mit Docker-Servern lokal betreiben?“ Er soll dafür weder .NET noch Node installieren müssen.

## Optionen

| Option | Bewertung |
| --- | --- |
| Nur Aspire | braucht .NET-SDK und Node |
| Docker Compose plus fertige Windows-App aus der CI | gewählt |

## Entscheidung

`docker compose up -d --build` startet Datenbank, API (Port 5086, Entwicklungsumgebung mit Beispiel-Spa), Web (5173) und Mailpit (8025), nur an 127.0.0.1. Die Windows-App liegt nach jedem grünen Lauf auf `main` als ZIP im Release `windows-app`.

## Begründung

Der Auftraggeber kann den Prototyp selbst ausprobieren und daran wieder entscheiden (ADR 0019).

## Folgen und Stand

PR #26; ein CI-Job prüft den Compose-Start. So nie öffentlich betreiben.

## Quellen

- [Entwicklung, Lokal mit Docker testen](../development.md#lokal-mit-docker-testen-ohne-net-und-node)
