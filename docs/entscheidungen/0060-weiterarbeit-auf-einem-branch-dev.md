# 0060. Weiterarbeit auf einem Branch `dev`, gesammelt nach `main`

- **Status:** angenommen
- **Datum:** 20.10.2026
- **Entschieden von:** Auftraggeber (Rahmen), Claude (Ausgestaltung)

## Kontext

Bis zum 20.10.2026 entstand jede Änderung auf einem eigenen Branch mit eigenem Pull Request ([arbeitsweise.md](../arbeitsweise.md)); parallel arbeitende Threads brauchten das. Seit der Übergabe an Claude Code ([ADR 0046](0046-uebergabe-an-claude-code-repository-als-einzige-quelle.md)) arbeiten der Auftraggeber und Claude allein. Beim Ausprobieren in Visual Studio lag der neue Stand auf einem anderen Branch in einem eigenen Ordner, und der Auftraggeber sah die Änderungen nicht. Auftraggeber am 20.10.2026: „wir arbeite ab jetzt alleine an der branche, lass uns im main bleiben oder eine dev branche erstellen wo wir bleiben kannst du entscheiden“.

## Optionen

| Option | Bewertung |
| --- | --- |
| Direkt auf `main` | am einfachsten; aber jeder Zwischenstand landet ungeprüft auf `main`, und nach jedem grünen `main` veröffentlicht die CI die Windows-App als Download |
| Weiter ein Branch je Thema | sauber getrennt; aber Umschalten und Ordner, die beim Ausprobieren verwirren, und Konflikte in Protokoll und Stand |
| Ein Branch `dev` für die laufende Arbeit, gesammelt nach `main` | gewählt |

## Entscheidung

- Gearbeitet wird auf `dev`, im Hauptordner des Repositorys. Claude committet und pusht dort laufend; der Auftraggeber startet in Visual Studio immer diesen Stand.
- Ein dauerhaft offener Pull Request `dev` → `main` (Entwurf) lässt die CI bei jedem Push auf `dev` laufen. Sein Text sammelt, was seit dem letzten Merge dazukam, mit den Detailentscheidungen.
- Nach `main` geht es, wenn der Auftraggeber einen Stand ausprobiert hat und die CI grün ist: Pull Request aus dem Entwurf holen und **per Merge-Commit** mergen (nicht Squash, sonst entfernen sich `dev` und `main` nach jedem Merge). Danach einen neuen Entwurf `dev` → `main` öffnen.
- PR-Protokoll und Stand werden je Merge nach `main` nachgezogen; ein Abschnitt im Protokoll fasst die Themen dieses Merges zusammen.
- `dev` wird nach einem Merge nicht gelöscht.

## Begründung

`main` bleibt der geprüfte Stand, von dem die Windows-App veröffentlicht wird. Ein einziger Arbeitsbranch nimmt das Umschalten weg, das beim Ausprobieren in die Irre geführt hat. Der Entwurfs-PR behält die CI für jeden Push, ohne für jedes Thema einen eigenen PR zu brauchen.

## Folgen und Stand

- [arbeitsweise.md](../arbeitsweise.md) und `CLAUDE.md` beschreiben den neuen Ablauf; die bisherigen Regeln (Doku im selben Stand, Detailentscheidungen festhalten, nie Tests überspringen) gelten weiter.
- Der Inhalt des geschlossenen PR #172 (Kalender der Windows-App: Klick öffnet, Ziehen, Hover-Karte) ist der erste Stand auf `dev`.
- Arbeiten wieder mehrere gleichzeitig, kehren wir zu einem Branch je Thema zurück.

## Quellen

- Der Auftraggeber im Gespräch mit Claude Code, 20.10.2026
- [ADR 0019](0019-autonomes-arbeiten-bis-zum-prototyp.md), [ADR 0044](0044-pull-requests-bleiben-merge-auch-bei-ci-ausfall.md), [ADR 0046](0046-uebergabe-an-claude-code-repository-als-einzige-quelle.md)
