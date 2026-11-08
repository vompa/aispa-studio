# Hinweise für Slice-Sitzungen

`/slice` liest diese Hinweise in Abschnitt 2, und das Slice-Panel hängt sie an jeden Start-Prompt einer Slice-Sitzung an. Sie ergänzen `.claude/commands/slice.md` und `work/conventions.md` um Erfahrungen aus den Slices; jede Runde hängt an, was sie gelernt hat, bestehende Punkte bleiben.

- Worktree mit `git worktree add .claude/worktrees/slice-NN -b slice-NN dev` anlegen, nicht mit EnterWorktree: dort lehnt der Bash-Schutz jeden Befehl ab, in dem „git“ vorkommt.
- `web/node_modules` im Worktree als Junction auf den Hauptordner anlegen (PowerShell `New-Item -ItemType Junction`) und vor `git worktree remove` mit `cmd /c rmdir` lösen.
- Basisprüfung (`dotnet format AispaStudio.slnx --verify-no-changes`, `dotnet build AispaStudio.slnx -c Release`) und lange Testläufe im Hintergrund mit Log im Scratchpad starten.
- Web-Prüfungen aus `web/`: `npm run lint`, `npm run typecheck`, Vitest mit `node node_modules/vitest/vitest.mjs run --maxWorkers=3` (`npx` scheitert in Git Bash am Leerzeichen im Pfad), `npm run build`, `npm run check:bundle`. Wird `help/helpPage.test.tsx` unter Last zufällig rot, die Datei einzeln wiederholen.
- Windows-App: `dotnet test --project tests/AispaStudio.Desktop.Tests -c Release` im Hintergrund (unter Last bis zu 20 Minuten), nur ein `dotnet build` zugleich im Worktree.
- Plan unter `work/plans/<JJJJ-MM-TT>-slice-NN.md` nach dem Vorbild `work/plans/2026-10-24-slice-12.md`; vor dem Abschluss eine frische Durchsicht des ganzen Branches durch einen lesenden Prüfer (Subagent, Modell Opus).
- Beim Squash auf `dev` sind Konflikte in `work/checklist.md`, der Rundenakte, dem PR-Protokoll und der Stand-Seite zu erwarten: alle Haken und Einträge beider Seiten behalten.

Ergänzt nach Runde R02 (Slices 30–38) und vibe-coding 1.2.0:
- Vor jedem `git add` oder Squash im Hauptordner `.git/SQUASH_MSG` und `UU`-Dateien prüfen: eine parallele Slice-Sitzung kann gerade squashen, ein eigenes `git add` landet dann in ihrem Index.
- Commit-Texte immer per `git commit -F <datei>`: Git Bash schreibt einen Text mit führendem `/` (etwa „/slice …“) in einen Windows-Pfad um.
- Prüfungen der Windows-App in Stufen nach `work/conventions.md`: nur eine Ansicht berührt → ihre Testklassen plus die Scan-Tests (`--filter-method "*jeder_Seite*"` usw., getrennter Aufruf); `Controls/`, `Styles.axaml`, `Colors.axaml`, `MainWindow` oder Shell berührt → volle Suite unter der Sperre `.slice-loop/full-suite.lock/`.
- Im Parallelbetrieb (`/slice NN parallel`) squasht die Warteschlange (`python .claude/scripts/slice_loop.py --merge`); sie stoppt bei Konflikten im PR-Protokoll, in der Stand-Seite oder der Befundliste, die dann in der Slice-Sitzung von Hand gelöst werden.
- Jede Slice kennzeichnet ihre Befunde in `docs/umsetzung/aenderungsliste-oberflaechen-2026-10-23.md` als „erledigt in Slice NN“ (Regel aus Slice 30).

Ergänzt nach Runde R02 (Slices 39–56, Rundenabschluss):
- Die Merge-Warteschlange hält beim ersten Konflikt an; fast jeder Squash kollidiert am Ende des PR-Protokolls und in der Stand-Zeile der Runde. Die wartende Slice mischt `dev` ein, behält beide Seiten und meldet wieder `SLICE-READY`. Slices, die dieselben Bereiche ändern, mischen `dev` vorab ein, damit beim Squash nur noch die Doku kollidiert.
- Befundkennungen, die parallele Slices vergeben (z. B. B71 ff.), vor dem Vergeben auf `dev` nachsehen und beim Einmischen umnummerieren, wenn eine andere Slice sie schon vergeben hat.
- Nach dem Einmischen von `dev` alle Prüfungen der Slice wiederholen, auch `npm run build` und `npm run check:bundle`, bevor `SLICE-READY` kommt.
- Die volle Desktop-Suite nur, wenn gemeinsame Teile der Windows-App berührt sind (Stufen in `work/conventions.md`); ändert eine Slice nur Testdateien oder eine Ansicht, genügen deren Testklassen.
- Vitest-Gesamtläufe immer mit `--maxWorkers=3`; rote Dateien unter Last einzeln zweimal wiederholen, bevor sie zählen. Zeitgrenzen in Tests nur verlängern, wenn eine Prüfung dadurch nicht wegfällt, und das im Protokoll nennen.
- Keine Shell im Worktree stehen lassen, wenn die Warteschlange squasht: der Worktree lässt sich dann nicht entfernen und bleibt als Ordnerrest liegen.
- Befunde ab B71 kennzeichnet die Slice in der Rundenakte R02 („– erledigt in Slice NN“ am Ende der Befundzeile), nicht in der Befundliste.
- GUI-Tests immer mit Inhaber-Rechten, damit alle Masken sichtbar sind (Windows-App: `Owner()` in `ViewTests.cs`; Web: Benutzer mit Rolle `Owner` im Stub von `/api/auth/me`). Nur Tests einer Rechtegrenze setzen eine andere Rolle, und zwar ausdrücklich.

Ergänzt nach Runde R04 (Slices 82–91) und vibe-coding 1.5.0:
- Die Warteschlange löst Konflikte, bei denen beide Seiten nur anhängen (PR-Protokoll, Befundliste), und die Status-Zeile der Runde selbst; die Punkte oben, nach denen sie dort stoppt, sind überholt.
- Reihenfolge am Slice-Ende: Checks abhaken, Protokoll und Rundenakte schreiben, committen, `dev` einmischen, dann erst die letzte Prüfkette auf der Spitze (sonst läuft sie zweimal, ~35 min), dann `.slice-loop/slice-NN.msg` und als Letztes der Spitzen-SHA in `.slice-loop/slice-NN.ready`. Ohne passende Ready-Datei squasht die Warteschlange nicht; nach jedem späteren Commit die Datei neu schreiben.
- Die Basisprüfung im neuen Worktree vor der ersten Änderung laufen lassen: im Hauptordner zieht der Build halbfertige Dateien aus `.claude/worktrees/` mit (Slices 87, 90).
- Bilderzeugung mit `AISPA_SCREENSHOTS` ändert viele fremde PNGs (Slice 84: 113): nur die Bilder der eigenen Ansicht committen, den Rest mit `git restore <pfad>` zurücksetzen.
- `TaskStop` auf einen Hintergrundlauf beendet `dotnet test` und `testhost` nicht: per `Get-CimInstance Win32_Process` mit dem Worktree-Namen in der Befehlszeile suchen und beenden, sonst sperren sie die DLLs für den nächsten Build.
- Oberflächenfehler über den echten Weg testen (Klick, Taste), nicht nur im ViewModel: Ä146 war mit einem ViewModel-Test grün und nur nach einem Klick auf einen Treffer kaputt (Slice 83).

Ergänzt mit vibe-coding 1.9.0 (Regeln aus `/slice` hierher verschoben, Überholtes benannt):
- Prüfbefehle nie mit `| tail` oder einer anderen Pipe in einer `&&`-Kette: die Pipe verdeckt den Exitcode; Ausgabe in eine Datei schreiben und den Exitcode lesen.
- Erzeugte Dateien, die du nicht angefasst hast und die geändert oder fehlend erscheinen, stammen meist vom Build einer anderen Sitzung in diesem Checkout: warten, bis kein solcher Build mehr läuft (höchstens 10 Minuten), erneut nachsehen, nicht anfassen.
- Jede in einem Test verlängerte Zeitgrenze oder Wartezeit kommt mit Grund und gemessener Zeit in den Protokolleintrag.
- Überholt: Die Punkte oben, nach denen jedes Einmischen von `dev` alle Prüfungen wiederholt und `dev` vor jeder letzten Prüfkette eingemischt wird. Es gilt `.claude/commands/slice.md` Abschnitt 4 Schritt 6a: `dev` nur bei Überschneidung einmischen, danach Basisprüfung und eigene Prüfungen.
