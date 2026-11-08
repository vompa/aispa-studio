# 0044. Pull Requests bleiben, bei CI-Ausfall Merge nach lokal grüner Prüfung

- **Status:** angenommen
- **Datum:** 18.10.2026, 12:35
- **Entschieden von:** Auftraggeber (Entscheidungskarte), Vorschlag Claude

## Kontext

Am 18.10.2026 ab 12:21 UTC startete GitHub Actions für neue Pull Requests nicht mehr (Actions-Minuten aufgebraucht). Fünf Threads arbeiteten gleichzeitig am Code. Der Auftraggeber schrieb: „PR braucht es eigentlich nicht wir arbeiten ja alleine.“ Claude stellte die Frage als Entscheidungskarte: „Sollen die Threads ohne Pull Requests direkt auf main pushen?“ Um 15:30 UTC setzte der Auftraggeber ein Budget für GitHub Actions („hab die actions auf 20$ gesetzt“); danach lief die CI wieder.

## Optionen

| Option | Bewertung |
| --- | --- |
| PRs behalten, bei CI-Ausfall lokal prüfen und warten | sicher, aber die Arbeit steht still, solange Actions nicht läuft |
| PRs behalten, Merge nach lokal grünen Tests auch ohne CI | gewählt: Änderungen bleiben getrennt und nachvollziehbar, unabhängig von GitHub-Ausfällen |
| Direkt auf main pushen | schneller, aber hohes Risiko für Konflikte und kaputtes `main` bei fünf parallelen Threads |

## Entscheidung

Jede Änderung geht weiter über Branch und Pull Request auf `main`. Startet die CI nicht, darf der PR nach der vollen lokalen Prüfkette gemergt werden (`dotnet format --verify-no-changes`, Release-Build, alle Tests, Web-Lint, Typecheck, Web-Tests, Build, Bundle-Prüfung), nachdem `main` eingemischt wurde. Der PR-Text vermerkt dann „CI ausgefallen, lokal geprüft“ mit den Zahlen der gelaufenen Tests. Läuft die CI, bleibt es bei [ADR 0019](0019-autonomes-arbeiten-bis-zum-prototyp.md): Merge erst nach grüner CI.

## Begründung

Pull Requests halten die parallelen Änderungen getrennt, dokumentieren Detailentscheidungen (PR-Protokoll) und lassen Konflikte früh erkennen. Die lokale Prüfkette ist dieselbe wie in der CI, sodass ein Ausfall des Dienstes kein Grund ist, ungeprüft zu mergen oder die Arbeit anzuhalten.

## Folgen und Stand

- PR #91 (Rückverfolgung je Charge) wurde nach dieser Regel lokal geprüft, aber noch nicht gemergt (siehe [Übergabe](../uebergabe.md)).
- GitHub Actions hat ein Ausgabenlimit von 20 USD. Ist es aufgebraucht, startet die CI nicht; dann gilt diese Regel. Der Auftraggeber fragte, warum Actions nicht in seiner Pro-Lizenz enthalten ist: GitHub Pro enthält 3.000 Minuten im Monat für private Repositorys, Windows-Läufe zählen doppelt; der Desktop-Job läuft auf `windows-latest`.
- Eine Alternative für später: eigener Runner oder weniger Windows-Minuten (Desktop-Job nur bei Änderungen unter `src/AispaStudio.Desktop`).

## Quellen

- Projektchat 18.10.2026, 12:34 bis 12:35 UTC (Entscheidungskarte), 15:30 UTC (Budget)
- `CLAUDE.md`, Abschnitt Arbeitsweise

## Nachtrag 19.10.2026

Der Auftraggeber hat das Budget für GitHub Actions am 19.10.2026 auf 60 USD erhöht; die CI läuft wieder. Es wird auf grüne CI gewartet, bevor gemergt wird. Ohne CI wird nur gemergt, wenn die Jobs wirklich nicht starten (kein Läufer, keine Logs).
