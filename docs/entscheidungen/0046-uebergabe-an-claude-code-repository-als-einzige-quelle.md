# 0046. Weiterarbeit in Claude Code: Threads gestoppt, das Repository ist die einzige Quelle

- **Status:** angenommen
- **Datum:** 18.10.2026, 21:34
- **Entschieden von:** Auftraggeber

## Kontext

Vom 17.10. bis 18.10.2026 entstanden Konzept und Prototyp in einem Claude-Projekt mit parallelen Threads je Modul (Kalender-Kern, Kassa, Online-Buchung, KI-Terminannahme, Windows-App ausbauen, Windows-App eigenständig mit lokaler Datenbank). Am 18.10. um 06:37 UTC bat der Auftraggeber, „die ganzen entscheidungswege usw auch im repo“ abzulegen, „auch die ganze definition damit ich in code weiterarbeiten kann“ (PR #27). Um 08:58 UTC: „bauen einfach autonom alle module weiter ich kontrolliere dann immer wieder.“ Um 21:34 UTC: „Stoppe alle aktiven threads, starte keine neuen, und bereite das projekt so auf, dass du alle entscheidungen, offenen arbeitsanweisungen, zukünftige überlegungen usw im repository für code schreibst, ich arbeite dann später in code das ganze projekt auf.“

## Optionen

| Option | Bewertung |
| --- | --- |
| Threads weiterlaufen lassen, der Auftraggeber liest im Projektchat mit | Wissen verteilt sich auf Chat, Dateien im Projekt und Repository; nicht aus Claude Code heraus nutzbar |
| Alles ins Repository, Threads beenden, Weiterarbeit mit Claude Code | gewählt |

## Entscheidung

Alle Threads wurden am 18.10.2026 um 21:40 UTC gestoppt; jeder hat eine Übergabe geschrieben ([docs/uebergabe/threads](../uebergabe/threads/)). Das Repository `vompa/aispa-studio` ist ab jetzt die einzige Quelle für Produktdefinition, Entscheidungen, Stand, offene Arbeitsanweisungen und Überlegungen. Einstieg ist [docs/uebergabe.md](../uebergabe.md); `CLAUDE.md` verweist zuerst dorthin. Halbfertige Arbeit außerhalb von `main` (offene PRs, Branches, ein Patch-Entwurf) wird dort mit Branch-Namen geführt und nicht mehr von Threads gemergt.

Die Arbeitsregeln bleiben: Branch und Pull Request auf `main`, Merge nach grüner CI ([ADR 0019](0019-autonomes-arbeiten-bis-zum-prototyp.md), bei Ausfall [ADR 0044](0044-pull-requests-bleiben-merge-auch-bei-ci-ausfall.md)), PR-Protokoll und Stand nachziehen, Grundsatzentscheidungen als ADR, Konzepte nicht überschreiben.

## Begründung

Der Auftraggeber will das Projekt selbst mit Claude Code im Code aufarbeiten. Dafür muss alles, was bisher im Projektchat und in den Threads lag, als Text im Repository stehen, versioniert und ohne Zugang zum Projekt lesbar.

## Folgen und Stand

- Der Projektchat und die Thread-Dateien unter `/mnt/project-files` werden nicht mehr gepflegt; was dort stand, ist in `docs/` übernommen (Konzepte, Übergaben, Bildschirmfotos unter `docs/bilder/`).
- Die Partneranfragen an Treatwell und Google bleiben zurückgestellt ([ADR 0014](0014-buchungsplattformen-ohne-partnervertrag.md)) und werden nur mit Freigabe des Auftraggebers versendet.
- Modellwahl und Kontingente des Claude-Projekts betreffen das Repository nicht und sind hier nicht festgehalten.

## Quellen

- Projektchat 18.10.2026, 06:37, 08:58 und 21:34 UTC
- [docs/uebergabe.md](../uebergabe.md)

## Nachtrag 20.10.2026

Am 18.10.2026 um 22:49 UTC schrieb der Auftraggeber in den Threads „Windows-App eigenständig mit lokaler Datenbank“ und „Windows-App ausbauen“ „weitermachen“; ab dem 19.10. kamen neue Aufträge wieder über den Projektchat (Kalender aus Sicht der Rezeption, Handbuch, lokale KI, Buchungsprobleme). Bis zum 20.10.2026 wurden so die PRs #88 bis #170 gemergt. Am 20.10.2026 um 03:58 UTC schrieb der Auftraggeber: „jetzt nochmal die übergabe auf code vorbereiten und dann alle weiteren punkte werden dort gemacht“. Alle Threads haben ihre Arbeit gemergt oder in der [Übergabe](../uebergabe.md) vermerkt; seitdem gilt die Entscheidung oben wieder ohne Ausnahme.
