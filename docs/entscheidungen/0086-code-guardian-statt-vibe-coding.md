# 0086. Code Guardian statt vibe-coding, volle Prüfung nur am Rundenende

- **Status:** angenommen
- **Datum:** 03.11.2026
- **Entschieden von:** Auftraggeber (Vorgabe und alle Einzelentscheidungen), Ausgestaltung Claude

## Kontext

Von Runde R01 bis R07 lief die Arbeit über das Plugin vibe-coding: Slice-Pläne in `work/`, der Befehl `/slice`, eine Slice-Schleife und eine Merge-Warteschlange (`.claude/scripts/slice_loop.py`), die fertige Slices nach `dev` squashte. Je Slice lief eine Prüfkette nach Prüfstufe; mit dem Einmischen von `dev` und den Prüfläufen der Warteschlange dauerte das Prüfen einer Slice oft Stunden. Auftraggeber am 03.11.2026: „ich würde gerne die offenen slices an codeguardien übergeben und damit vibe-coding ablösen“, „vibe-coding kann dann eigentlich entfernt werden aus aispa“ und „können wir die prüfungen pro slice in eine gesamtprüfung am ende der runde verschieben, jedesmal prüfen dauert stunden“.

## Optionen

| Option | Bewertung |
| --- | --- |
| Code-Guardian-Skill `multitask` (bis zu fünf Hintergrundsitzungen) | nur unter macOS erprobt, kein Modell je Aufgabe, keine Ketten abhängiger Aufgaben, keine Junction für `web/node_modules` |
| Alle Aufgaben in einer Sitzung nacheinander | keine Parallelität, ein Modell für alles |
| Code-Guardian-Aufgabenliste, eine eigene Sitzung je Aufgabe im Worktree, die sich selbst nach `dev` squasht | gewählt |

## Entscheidung

- Die Mechanik von vibe-coding ist aus dem Repository entfernt (`.claude/commands/slice.md`, `.claude/slice/`, `.claude/scripts/slice_loop.py`). Das Plugin selbst bleibt außerhalb des Repositorys unberührt.
- `work/` bleibt als Archiv stehen ([work/README.md](../../work/README.md)). Bis zum Ende von R07 kommen nur noch die Handtestblöcke der Slices 138 bis 147 in die Rundenakte R07.
- Offene Arbeit steht in der Code-Guardian-Aufgabenliste `.code-guardian-todo.md` (nicht versioniert). Je Aufgabe läuft eine eigene Sitzung mit dem Modell aus der Aufgabe, höchstens zwei zugleich. Die Sitzung arbeitet in einem Worktree und bringt ihre Arbeit selbst als einen Squash-Commit nach `dev`, unter einer Sperre gegen gleichzeitige Squashes.
- In einer Aufgabe laufen nur Basisprüfung (Format, Release-Build) und die zuständigen Tests. Volle Suiten, Scan-Tests und die volle Prüfkette laufen einmal beim Rundenabschluss; ein Fehler dort wird über die Squash-Commits eingegrenzt und als neue Aufgabe behoben.
- Ablauf, Befehle und Startvorlage: [arbeitsweise.md](../arbeitsweise.md) Abschnitt 9. Ergänzt [ADR 0060](0060-weiterarbeit-auf-einem-branch-dev.md) (Arbeit auf `dev`), ersetzt keine ADR.

## Begründung

Die Aufgabenliste mit einer Sitzung je Aufgabe behält, was sich bewährt hat (frischer Kontext je Slice, Modell je Slice, ein Commit je Slice), ohne die Schleife und die Warteschlange, die eigene Prüfläufe und Konflikte erzeugten. Die volle Prüfung am Rundenende spart die Stunden je Slice; der Preis ist, dass ein Bruch an anderer Stelle erst am Rundenende auffällt. Ein Commit je Slice hält das Eingrenzen dann kurz.

## Folgen und Stand

- Ablauf in [arbeitsweise.md](../arbeitsweise.md) Abschnitt 9; Protokolle und Sperren lokaler Läufe liegen in `.test-logs/` (statt `.slice-loop/`, auch für `scripts/view-tests.ps1`).
- Das Konzept [eingabepruefung-rundenabschluss.md](../konzepte/eingabepruefung-rundenabschluss.md) nennt in Abschnitt 3 noch `/slice close` als Ort des Rundenabschlusses; nach Abschnitt 6 der Arbeitsweise bleibt es unverändert. An die Stelle von `/slice close` tritt der Rundenabschluss aus Abschnitt 9.
- Runde R07 am 04.11.2026 abgeschlossen: Slices 137 bis 147 auf `dev`. Die volle Prüfkette auf 48209096 hatte 3 rote Tests: zwei echte (veraltete Testerwartung nach Slice 140, aufzählender Tooltip aus Slice 141), behoben in 5cb3b73d und die Klassen danach grün; einer war Last (einzeln zweimal grün). `work/` ist seitdem eingefroren; Handtestblöcke stehen in [umsetzung/handtests.md](../umsetzung/handtests.md).

## Quellen

- [work/rounds/R07-reception-daily-layout.md](../../work/rounds/R07-reception-daily-layout.md)
- [work/conventions.md](../../work/conventions.md) (bisherige Prüfstufen und Laufzeiten)
