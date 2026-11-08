# 0088. Öffentliches Repository unter MIT-Lizenz, Konzeptstudie zur KI-nativen Entwicklung

- **Status:** angenommen
- **Datum:** 07.11.2026
- **Entschieden von:** Auftraggeber

## Kontext

Das Repository soll öffentlich werden. Bisher gab es keine Lizenzdatei; öffentlich ohne Lizenz hieße: ansehen ja,
nutzen, ändern und weitergeben nein. Auftraggeber, 07.11.2026: „ich möchte ein lizenzmodell das jeder machen kann was er will
mit dem code und der app“. Dazu am selben Tag: AISPA Studio ist
überall als „Eine Konzeptstudie zur KI-nativen Softwareentwicklung“ zu bezeichnen. Gemeint ist ein Vorzeigeprojekt: Das Repository zeigt, wie eine vollständige Software KI-nativ entsteht.

## Optionen

| Option | Bewertung |
| --- | --- |
| MIT | alles erlaubt, auch Weiterverkauf; einfach, verbreitet, wie OPCAgentSample; schützt das Abo-Modell nicht |
| AGPL-3.0 | quelloffen, Änderungen müssen auch beim Betrieb als Dienst offengelegt werden; schränkt Nutzer ein |
| BUSL-1.1 | Code sichtbar, Einsatz im Betrieb nur mit Vertrag, nach vier Jahren frei; nicht „jeder darf alles“ |
| FSL-1.1 | wie BUSL, verbietet nur konkurrierende Angebote, nach zwei Jahren frei; ebenfalls Einschränkung |
| MIT, Repository als Konzeptstudie zur KI-nativen Entwicklung | gewählt |

## Entscheidung

1. **Lizenz MIT** für Code und App (`LICENSE` im Hauptordner, „Copyright (c) 2026 vompa“, wortgleich wie OPCAgentSample).
   Fremde Bausteine behalten ihre Lizenzen; geprüft am 07.11.2026: kein Copyleft in den ausgelieferten Paketen (NuGet,
   npm im Web-Bundle). Offen ist nur die Liste der Bibliotheken im mitgelieferten PostgreSQL-Build.
2. **AISPA Studio ist eine Konzeptstudie zur KI-nativen Softwareentwicklung.** README, Installationsseiten, die Erklärseite
   `/ueber/`, die Hilfe-Startseite und die Anmeldung der Windows-App nennen das. Die Day-Spa-Software ist das
   durchgehende Beispiel, an dem sich die Arbeitsweise zeigt (Mensch entscheidet, Agenten bauen und prüfen).
3. **Öffentliche Dokumentation:** kurze README nach dem Muster großer Repositories, Installation und
   Systemvoraussetzungen in `docs/installation/`, Funktionen in `docs/features.md`, Entwicklung in
   `docs/development.md`, Meldeweg für Sicherheitslücken in `SECURITY.md`.

## Begründung

Der Auftraggeber will ausdrücklich keine Einschränkung. MIT ist die bekannteste Lizenz ohne Auflagen außer dem Copyright-Hinweis,
passt zu den übrigen öffentlichen Repositories (OPCAgentSample) und zu allen Abhängigkeiten. Als Vorzeigeprojekt
lebt das Repository davon, dass andere es lesen, nachbauen und weiterverwenden dürfen.

## Folgen und Stand

- Jede und jeder darf AISPA Studio selbst betreiben, ändern und auch als eigenes Angebot verkaufen. Das Abo-Modell aus
  [ADR 0015](0015-lizenzmodell-abo-pro-standort.md) bleibt möglich (Betrieb, Hosting, Support), ist aber nicht durch die
  Lizenz geschützt. Veröffentlichte Stände bleiben für immer MIT; eine spätere andere Lizenz gilt nur für neue Stände.
- Bei der Weitergabe der Windows-App gehören die Lizenztexte der Fremdbausteine dazu (Third-Party-Notices); das ist
  noch nicht umgesetzt.
- Die Git-Historie enthält private Angaben (Autor-Mailadressen von Commits über die GitHub-Oberfläche). Deshalb wird
  nicht dieses Repository öffentlich, sondern ein neues mit einem einzigen Commit des bereinigten Stands; dieses bleibt
  privat als Archiv (Auftraggeber, 07.11.2026). Ein Force-Push genügte nicht: GitHub hält die alten Commits über die PR-Verweise
  (`refs/pull/N/head`) erreichbar. `CLAUDE.md` und `.claude/` bleiben im öffentlichen Stand draußen (lokal).

## Quellen

- [README](../../README.md), [Installation](../installation/README.md), [SECURITY.md](../../SECURITY.md)
- OPCAgentSample, `LICENSE`
