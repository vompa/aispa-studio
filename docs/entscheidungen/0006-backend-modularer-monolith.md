# 0006. Backend als modularer Monolith mit Queue, SignalR und PostgreSQL

- **Status:** angenommen
- **Datum:** 17.10.2026
- **Entschieden von:** Auftraggeber (Anforderung), Empfehlung Claude, mit dem Entscheidungsdokument angenommen

## Kontext

Auftraggeber: „Continuous Integration muss gegeben sein und die Oberfläche soll so schnell reagieren wie möglich, Backend-Abfragen usw. sollen alles entkoppelt soweit möglich ablaufen.“

## Optionen

| Option | Bewertung |
| --- | --- |
| Microservices von Anfang an | viel Betriebsaufwand, Grenzen zu früh festgelegt |
| Klassischer Monolith, alles synchron | KI, Plattformen und Berichte würden Kalender und Kasse ausbremsen |
| Event Sourcing für alles | mächtig, aber komplex; nur das Kassenjournal braucht Unveränderbarkeit |
| Low-Code-Plattform | nicht für einen eigenen Ressourcen-Kalender und Kassenrecht |
| Modularer Monolith mit Queue | gewählt |

## Entscheidung

Ein modularer Monolith in ASP.NET Core 10 mit Fachmodulen (Kalender, Kasse, Kunden, Personal, KI, Kanäle, Reporting). Länger laufende oder externe Arbeit läuft über Wolverine (dauerhaft in PostgreSQL), Live-Änderungen per SignalR, lokal orchestriert mit .NET Aspire. Datenbank PostgreSQL.

## Begründung

Die Oberfläche zeigt eine Aktion sofort und bestätigt im Hintergrund. Fällt Treatwell oder die KI aus, staut sich nur deren Queue. Ein Monolith ist für ein kleines Team leichter zu betreiben, die Modulgrenzen erlauben späteres Auslagern. PostgreSQL kennt Zeitbereiche mit Ausschlussregeln.

## Folgen und Stand

Ordner je Modul unter `src/AispaStudio.Api` (Scheduling, Pos, Online, MailIntake, Assistant, Reporting, Costs, Accounting, Payroll, Roster, Access, Tenancy). Regel: Mails, KI und Schnittstellen nie im HTTP-Aufruf, sondern über die Queue.

## Quellen

- [Architektur](../architektur.md)
