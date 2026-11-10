# Arbeitsweise: wie bisher mit Branches, Pull Requests und Merge gearbeitet wurde

Stand: 20.10.2026. Beschreibt, wie Claude im Projekt bis zur Übergabe gearbeitet hat und wie es in Claude Code weitergehen soll. Die Regeln stammen aus den Vorgaben des Auftraggebers im Projektchat; die Entscheidungen dahinter stehen in [ADR 0019](entscheidungen/0019-autonomes-arbeiten-bis-zum-prototyp.md) und [ADR 0044](entscheidungen/0044-pull-requests-bleiben-merge-auch-bei-ci-ausfall.md). Einstieg in alles Übrige: [uebergabe.md](uebergabe.md).

## 1. Grundsätze

- **Selbstständig bis zum Prototyp.** Der Auftraggeber hat am 17.10.2026 freigegeben, dass Claude Detailentscheidungen nach den bisherigen Festlegungen selbst trifft und PRs nach grüner CI selbst mergt. Gefragt wird nur bei Unumkehrbarem: Produktion, Löschen, Nachrichten an Dritte (z. B. die ruhenden Partneranfragen). Der Auftraggeber entscheidet wieder selbst, sobald er den Prototyp lokal ausprobiert hat.
- **Unvollständiges selbst mitdenken.** Wichtige Themen, die der Auftraggeber nicht genannt hat, ergänzt Claude eigenständig ([ADR 0020](entscheidungen/0020-ergaenzende-themen-uebernommen.md)).
- **Alles auf Deutsch, was der Auftraggeber liest:** Oberfläche, Fehlermeldungen, PR-Titel und -Texte, Commit-Nachrichten, Dokumentation. Alle Namen im Code sind englisch (Ordner, Dateien, Klassen, Ressourcen, Link-Ordner, Skripte, Migrationen; Regel und Ausnahmen in `CLAUDE.md`, Abschnitt „Sprache“), Kommentare folgen dem Stil der Umgebung.
- **Das Repository ist die einzige Quelle** ([ADR 0046](entscheidungen/0046-uebergabe-an-claude-code-repository-als-einzige-quelle.md)). Was entschieden oder gelernt wurde, steht in `docs/`, nicht im Chat.

## 2. Ablauf einer Änderung

**Seit 20.10.2026 gilt [ADR 0060](entscheidungen/0060-weiterarbeit-auf-einem-branch-dev.md):** Der Auftraggeber und Claude arbeiten allein auf dem Branch `dev` im Hauptordner. Statt der Schritte 1, 5 und 8 unten: laufend auf `dev` committen und pushen; ein offener Entwurfs-PR `dev` → `main` lässt die CI bei jedem Push laufen; hat der Auftraggeber einen Stand ausprobiert und ist die CI grün, wird er **per Merge-Commit** nach `main` gemergt (nicht Squash) und ein neuer Entwurf geöffnet. `dev` wird nicht gelöscht. Die übrigen Schritte gelten unverändert; die Schritte unten beschreiben die Arbeit mit einem Branch je Thema, zu der wir bei paralleler Arbeit zurückkehren.

1. **Branch von `main`.** Nie direkt auf `main` pushen. Die Cloud-Threads arbeiteten auf Branches der Form `claude/<thema>-<kürzel>`; in Claude Code genügt ein sprechender Name.
2. **Bauen und prüfen**, laufend, nicht erst am Ende (Abschnitt 3). Neue Funktionen bekommen Tests (Backend gegen echtes PostgreSQL, Web mit Vitest, App-Logik in `AispaStudio.Desktop.Tests`) und, wo es um Oberfläche geht, Bildschirmfotos ([uebergabe.md, Abschnitt 2](uebergabe.md#2-lokal-starten-und-prüfen)).
3. **Doku mitliefern, im selben PR:**
   - README-Abschnitt des Moduls (Bedienung, Einstellungen), falls sich beides ändert.
   - Abschnitt im [PR-Protokoll](umsetzung/pr-protokoll.md) (Abschnitt 5 unten).
   - [stand-und-offene-punkte.md](stand-und-offene-punkte.md) nachziehen: Zeile in „Gebaut“, erledigte Punkte streichen, neue Lücken eintragen.
   - [testdaten.md](testdaten.md) und die Demo-Daten (`src/AispaStudio.Api/Demo`), wenn ein neuer Fachbereich Beispieldaten braucht.
   - Grundsatzentscheidung → neuer ADR (Abschnitt 6).
4. **Committen.** Commits auf Deutsch, Betreff im Aktiv und ohne Punkt, Rumpf nennt das Warum (siehe Abschnitt 4).
5. **Pull Request auf `main`** mit Text nach Abschnitt 4. PR-Nummer erst nach dem Anlegen bekannt: Platzhalter (`#130`, `161`) in Doku nachtragen und mit einem Folgecommit ersetzen.
6. **`main` einmischen**, wenn `main` sich bewegt hat (Merge, kein Rebase). Konflikte entstehen fast immer in `pr-protokoll.md` und `stand-und-offene-punkte.md`: beide Seiten behalten.
7. **CI abwarten, dann mergen** (Abschnitt 3 und 4).
8. **Branch löschen,** nachdem der PR gemergt ist (nur eigene Branches; bei fremden Branches den Auftraggeber fragen).

## 3. Prüfen: CI und lokale Prüfkette

Die CI (`.github/workflows/ci.yml`) läuft bei jedem PR und bei jedem Push auf `main`. Sie war von Tag eins an da. Jobs: Backend (Format, Release-Build mit Warnungen als Fehler, OpenAPI aktuell, API-Tests mit PostgreSQL), Web (Typen aktuell, Lint, Typecheck, Tests, Build, Bundle-Grenze des Kundenbereichs), Avalonia-Linter (`avalonia-lint` mit `--fail-on error` über das ganze Repository, Waivers in `.avalonia-lint.json`), Windows-App (Desktop-Tests ohne Ansichtstests und Veröffentlichung, danach Release-Tag `windows-app`), Windows-App-Ansichtstests (vier Jobs, je eine Gruppe aus `scripts/view-tests.ps1 -Group`) und Docker-Compose-Start. Seit 04.11.2026 so aufgeteilt, weil die Desktop-Suite in einem Prozess die 30 Minuten riss; jeder Windows-Job baut selbst.

**Die lokale Prüfkette entspricht der CI** und ist Voraussetzung für jeden Merge ohne CI:

```bash
dotnet format AispaStudio.slnx --verify-no-changes
dotnet build AispaStudio.slnx -c Release
dotnet test --solution AispaStudio.slnx -c Release --no-build -- --filter-not-class "AispaStudio.Desktop.Tests.ViewTests"
pwsh scripts/view-tests.ps1
cd web && npm run gen:api && npm run lint && npm run typecheck && npm test && npm run build && npm run check:bundle
```

Die Ansichtstests laufen getrennt in vier Prozessen (`scripts/view-tests.ps1`, ein Oberflächen-Thread je Prozess). Nach API-Änderungen `web/openapi/aispa-api.json` und `web/src/api/schema.d.ts` mit committen, sonst scheitert die CI.

**Actions sparsam nutzen.** Seit 08.11.2026 läuft die CI im öffentlichen Repository, dort kosten die Minuten nichts. Ein Lauf blockiert trotzdem 10 bis 15 Minuten lang die Merge-Entscheidung. Deshalb:

- Nicht mehrfach „zum Testen“ pushen; erst lokal prüfen, dann pushen. Keine leeren Commits und kein Schließen und Öffnen des PRs, um die CI anzustoßen.
- Läuft die CI, **auf grün warten**. Ein Lauf dauert einige Minuten; mit einer Wartezeit im Hintergrund prüfen statt ständig abzufragen. Bei Rot zuerst die Ursache klären und beheben, nie Tests überspringen oder abschalten. Ein wackeliger Test wird stabilisiert (Beispiel: PR #84).
- Startet die CI nicht, gilt [ADR 0044](entscheidungen/0044-pull-requests-bleiben-merge-auch-bei-ci-ausfall.md): `main` einmischen, die volle lokale Prüfkette laufen lassen, mergen und im PR-Text **„CI ausgefallen, lokal geprüft“** mit den Zahlen der gelaufenen Tests vermerken.
- Spar-Idee für später: Desktop-Job nur bei Änderungen unter `src/AispaStudio.Desktop`, oder eigener Runner.

### Live-Tests der Windows-App

Ein Live-Test spielt eine Rolle sichtbar in der echten App gegen einen Server mit Demo-Daten durch ([ADR 0090](entscheidungen/0090-live-tests-mit-flaui.md)). Er ist kein Teil der Prüfkette und läuft nicht in der CI; er dient zum Zusehen und Belegen.

```bash
docker compose up -d --build
pwsh scripts/live-tests.ps1 -Role Reception -Speed slow
pwsh scripts/live-tests.ps1 -Gui
```

- `-Gui` baut die App und startet die Starter-App: Rolle wählen, Themen anhaken, Start; Pause/Weiter, Schritt, Bild und Stopp sind Knöpfe, die Schritte erscheinen live mit ✓/✗. Die Starter-App steht rechts, die AISPA-App links daneben.
- `-Topics customer,appointment` wählt in der Konsole Themen aus (Rezeption: `customer` „Kundin anlegen“, `appointment` „Termin buchen“); ohne Angabe laufen alle. Die Anmeldung läuft immer mit.

- `-Speed slow|normal|fast`: Pause je Schritt 1,5 s, 0,5 s oder keine. `-Step` beginnt angehalten.
- Tasten in der Konsole: Leertaste Pause/Weiter, Enter ein Schritt (in der Pause), `S` Bild jetzt, `Q` Stopp.
- Bericht und Bilder: `.test-logs/live/<Zeitstempel>-<rolle>/bericht.md`, ein Bild je Schritt, bei einem Fehler zusätzlich `-fehler.png`.
- Exitcode: 0 bestanden, 1 mindestens ein Thema rot, 2 Aufruf oder Server, 3 gestoppt (`Q` oder Vordergrundschutz). Ein roter Schritt macht nur sein Thema rot: Fehlerbild und Bericht wie sonst, dann bringt der Tester die App in den Grundzustand (Rückfrage zu, Panel zu, Nina Kovač angemeldet, Kalender „Heute“) und spielt das nächste Thema; Themen, die ein rotes Thema brauchen, werden übersprungen und im Bericht genannt.
- Vordergrundschutz: Vor jeder Tasten- und Texteingabe prüft der Tester, dass das Vordergrundfenster zum Prozess der App gehört (bis zu 2 s Wartezeit nach `SetForeground`); sonst endet der Lauf mit klarer Meldung, ohne eine Taste zu senden.
- Themen der Rezeption (Schlüssel für `-Topics`, feste Reihenfolge; Themen mit „braucht“ verlangen das genannte Thema vorher im selben Lauf): `signin`, `signin-other-account`, `overview`, `customer-search`, `customer-search-alias`, `customer`, `customer-duplicate`, `customer-minimal`, `customer-empty`, `customer-phone-format`, `customer-badmail`, `appointment`, `appointment-conflict` (braucht `appointment`), `appointment-past`, `appointment-closed`, `checkin`, `checkin-late`, `checkin-undo-late`, `checkin-noshow`, `reschedule`, `reschedule-past`, `reschedule-checked-in`, `cancel`, `cancel-restore`, `cancel-esc`, `cancel-no-fee` (alle ab `checkin` brauchen `appointment`), `checkout` (braucht `appointment`), `voucher`, `voucher-invalid`, `receipt-void-empty-reason`, `day-closing` (immer zuletzt). Der Stand je Thema (geprüft oder nicht) steht im [Katalog](umsetzung/live-tests-rezeption-onboarding.md). Der Lauf bucht am Tag des Laufs (Einchecken und Kassieren gehen nur am Tag des Termins) „Maniküre mit Lack“ für die Kundin des Themas `customer` oder, ohne dieses, für den Stammgast Anna Bauer; zum Aufräumen die Datenbank neu laden.
- Gegenprobe: `AISPA_LIVE_SKIP_CLICK="Anlegen"` lässt Klicks auf Elemente dieses Namens aus; die Prüfung dahinter muss scheitern.
- Mausprobe: `AISPA_LIVE_MOUSE=1` bedient Knöpfe mit echtem Mausklick statt „Invoke“, mit 300 ms Pause davor (`AISPA_LIVE_MOUSE_PAUSE_MS=0` ohne Pause). Der Bericht nennt jeden Klick („Mausklick auf …“). `AISPA_LIVE_MOUSE_HOVER_MS=600` gleitet erst auf den Knopf, wartet und klickt dann. Die Themen `probe-checkin-loop` und `probe-cancel-loop` (mit `appointment` gewählt) klicken „Einchecken“ bzw. „Termin stornieren …“ `AISPA_LIVE_LOOP_N`-mal (Standard 10) ohne Wiederholung und schreiben „verloren x von n“ in den Bericht (Probe zu Befund B8, `docs/umsetzung/live-sichtpruefung-2026-11-09.md`).
- Zusätzliche Themen der Sichtprüfung (Schlüssel `extra-…`, vor den Themen der Kassa): `extra-cancel-late` (Stornofrist 24 Stunden und 50 % über die API setzen, Rückfrage als Rezeption und als Standortleitung, Regel danach zurücksetzen), `extra-sick-pause-block`, `extra-reschedule-yesterday`, `extra-checkin-tomorrow`, `extra-pos-checkin-prompt`, `extra-withdrawal-tab`, `extra-no-contact-pill-and-key-v`, `extra-api-errors` (Statuscodes und Texte vom Server) und `extra-offline`. Die Texte der Dialoge, Meldungen und Hinweise stehen wörtlich im Bericht („DIALOG …“, „MENÜ …“, „KNÖPFE …“, „OFFLINE …“); was fehlt, steht als „BEFUND: …“ im Bericht, der Lauf geht weiter. Die API-Proben melden sich über `/api/auth/dev/login` mit einem Testkonto an (Datei `LiveApi.cs`).
- `extra-offline` hält den API-Container an und startet ihn wieder (`docker compose -p aispa-sicht … stop|start api`); dafür muss `AISPA_LIVE_COMPOSE_OVERRIDE` auf die Override-Datei des Projekts `aispa-sicht` zeigen und der Lauf im Repo-Ordner starten. Nie gegen den Stack einer anderen Sitzung.

## 4. Commits und Pull Requests

- **Mergen per Squash.** Der Titel des PRs wird zum Commit auf `main`, mit der PR-Nummer in Klammern: „Rechnungsempfänger in der Kundenkarte der Windows-App ändern (#104)“. Der Titel beschreibt, was die Nutzerin danach kann, in einem deutschen Satz; keine Präfixe wie `feat:`.
- **Auto-Merge ist im Repository aus.** Nach dem Push die CI aktiv prüfen und, sobald grün, selbst mergen. Merge-Fenster klein halten: `main` bewegt sich bei mehreren gleichzeitigen Arbeiten im Minutentakt; nach Konflikt neu einmischen, die Prüfkette dort wiederholen, wo sich Code geändert hat.
- **Attribution von Claude** (Co-Authored-By, Claude-Session) setzt die Umgebung automatisch; nichts Eigenes erfinden.
- **PR-Text** auf Deutsch, in dieser Reihenfolge:
  1. **Was:** was die Person danach sieht oder kann, in wenigen Sätzen.
  2. **Detailentscheidungen:** jede mit Begründung, in Stichpunkten. Das ist der eigentliche Wert des Textes; er wird später ins PR-Protokoll übernommen.
  3. **Bewusst offen / später:** was nicht gebaut wurde und warum.
  4. **Geprüft:** welche Prüfkette mit welchen Testzahlen lief; bei CI-Ausfall der Vermerk aus Abschnitt 3.
- **Ein PR, ein Thema.** Große Vorhaben in Schritte schneiden, die jeweils grün und lauffähig sind (Beispiel Einzelplatz: Schritt 1 von 5 gemergt, der Rest als Entwurf).
- **Pull Requests bleiben auch bei Einzelarbeit** (Auftraggeber: „PR braucht es eigentlich nicht, wir arbeiten ja alleine“, danach bewusst beibehalten): Sie halten Änderungen getrennt, dokumentieren Entscheidungen und fangen Fehler früh ab.

## 5. PR-Protokoll und Stand pflegen

- Für jeden gemergten PR gibt es einen Abschnitt in [umsetzung/pr-protokoll.md](umsetzung/pr-protokoll.md): Überschrift `## PR #N: Titel (gemergt JJJJ-MM-TT)`, dann **Was**, **Detailentscheidungen**, **Bewusst offen / später**, so wie die vorhandenen Abschnitte.
- **Nach PR-Nummer einsortieren, nicht ans Ende hängen.** Alle hängen sonst an derselben Stelle an und erzeugen bei jedem zweiten Merge einen Konflikt. Dasselbe gilt für Zeilen in der Tabelle von `stand-und-offene-punkte.md`.
- Die Kopfzeile des Protokolls („PRs #1 bis #N“) und die Zahlen in [uebergabe.md](uebergabe.md) (Stand von `main`, Testzahlen) nur bei Gelegenheit nachziehen; sie sind Momentaufnahmen.

## 6. Entscheidungen festhalten

- **Detailentscheidung** (innerhalb des Bestehenden): im PR-Text und im PR-Protokoll.
- **Grundsatzentscheidung** (neue Technik, neue Regel, Abweichung von einem ADR): neuer ADR nach der [Vorlage](entscheidungen/vorlage.md) in `entscheidungen/`, Zeile im [Index](entscheidungen/README.md), „Entschieden von“ mit dem Auftraggeber oder „Claude, autonom nach ADR 0019“. Von bestehenden ADRs nie stillschweigend abweichen. Die nächste freie Nummer vor dem Merge prüfen (0042 wurde einmal doppelt vergeben, der Anzahlungs-ADR wurde deshalb 0047).
- Konzepte unter `docs/konzepte/` bleiben Stand 17.10.2026; Änderungen werden als Entscheidung festgehalten, nicht im Konzept überschrieben.

## 7. Wer macht was: Modelle und Parallelarbeit

- Bis zum 18.10.2026 arbeiteten bis zu sechs Threads gleichzeitig, je einer pro Modul (Kalender-Kern, Kassa, KI-Terminannahme, Online-Buchung, Windows-App, Einzelplatz). Sie haben sich über `main` abgestimmt, nie über fremde Branches.
- **Modelle:** Opus für Kernmodule (Kalender, Kassa, Rechtliches, Migrationen), Sonnet für Unkritisches (Doku, kleine Oberflächenänderungen). Seit dem 18.10.2026 ist Opus Standard.
- Am 18.10.2026 um 21:34 UTC hat der Auftraggeber alle Threads gestoppt ([ADR 0046](entscheidungen/0046-uebergabe-an-claude-code-repository-als-einzige-quelle.md)); danach liefen die beiden Windows-App-Threads auf seine Anweisung weiter, ab dem 19.10. auch neue Threads aus dem Projektchat. Seit dem 20.10.2026 um 03:58 UTC arbeitet der Auftraggeber nur noch in Claude Code ([Übergabe](uebergabe.md)). Wer parallel im Repository arbeitet, ändert nicht die Dateien des anderen und mischt `main` vor jedem Merge ein.

## 8. Grenzen, die immer gelten

Aus `CLAUDE.md`, hier nur zur Erinnerung:

- Nie Funktionen bauen, die Umsätze an Kasse oder Buchhaltung vorbeiführen ([ADR 0010](entscheidungen/0010-kassa-compliance-by-design.md)).
- Keine Nachrichten an Dritte, kein Produktionszugriff, kein Löschen fremder Daten ohne Freigabe des Auftraggebers.
- Keine Geheimnisse ins Repository.
- Pflichtzahlen rechnet nie die KI; die KI schlägt vor, der Mensch bestätigt ([ADR 0008](entscheidungen/0008-ki-schlaegt-vor-mensch-bestaetigt.md)).

## 9. Arbeit mit Code Guardian (seit 03.11.2026)

Seit dem 03.11.2026 ersetzt Code Guardian den Ablauf mit `/slice`, Slice-Schleife und Merge-Warteschlange ([ADR 0086](entscheidungen/0086-code-guardian-statt-vibe-coding.md)). `work/` ist Archiv ([work/README.md](../work/README.md)) und seit dem Abschluss der Runde R07 (04.11.2026) eingefroren; Handtestblöcke stehen in [umsetzung/handtests.md](umsetzung/handtests.md).

- **Aufgabenliste:** `.code-guardian-todo.md` im Hauptordner (nicht versioniert, Skill `todo`). Jede Aufgabe hat eine Detaildatei (Plan oder Aufgabenordner von Code Guardian); für R07 waren das die Slice-Dateien in `work/slices/`. Abhängigkeiten stehen als Zustand `blockiert (wartet auf T-…)`.
- **Eine Sitzung je Aufgabe**, gestartet im Hauptordner mit `claude --model <haiku|sonnet|opus>` (Modell aus der Aufgabe), mit vollem Code-Guardian-Ablauf. Höchstens zwei Sitzungen zugleich; Aufgaben mit gemeinsamen Dateien nacheinander.

### Worktree

- Anlegen mit `git worktree add .claude/worktrees/slice-NNN -b slice-NNN dev` (nicht mit `EnterWorktree`; dort lehnt der Bash-Schutz Befehle mit „git“ ab).
- `web/node_modules` als Junction auf den Hauptordner: PowerShell `New-Item -ItemType Junction -Path <worktree>\web\node_modules -Target <hauptordner>\web\node_modules`.
- Die Basisprüfung im neuen Worktree vor der ersten Änderung laufen lassen; nur ein `dotnet build` zugleich im Worktree.
- **Entfernen:** zuerst die Junction lösen, PowerShell `[IO.Directory]::Delete('<worktree>\web\node_modules')` (löst nur den Verweis), dann prüfen, dass `web/node_modules` im Hauptordner noch Inhalt hat, erst danach `git worktree remove .claude/worktrees/slice-NNN`. Nie `--force`, solange eine Junction besteht: git folgt ihr und leert den Hauptordner.

### Prüfen in der Aufgabe

Nur Basisprüfung und zuständige Tests; volle Suiten und Scan-Tests laufen erst beim Rundenabschluss.

- **Basisprüfung:** `dotnet format AispaStudio.slnx --verify-no-changes` und `dotnet build AispaStudio.slnx -c Release`.
- **Windows-App:** die Testklassen der geänderten Ansicht, `dotnet test --project tests/AispaStudio.Desktop.Tests -c Release --filter-class "AispaStudio.Desktop.Tests.<Klasse>"`; `ViewTests` nur für die geänderte Ansicht per `--filter-method`. Klassen- und Methodenfilter in getrennten Aufrufen (in einem Aufruf sind sie UND-verknüpft).
- **Web** (aus `web/`): `npm run lint`, `npm run typecheck`, `node node_modules/vitest/vitest.mjs run --maxWorkers=3 <betroffene Testdateien>`; bei Änderungen an Build oder Bundle zusätzlich `npm run build` und `npm run check:bundle`.
- **API:** die betroffenen Testklassen, `dotnet test --project tests/AispaStudio.Api.Tests -c Release --filter-class "AispaStudio.Api.Tests.<Klasse>"`; nach API-Änderungen `cd web && npm run gen:api` und beide erzeugten Dateien committen.
- Nach dem Einmischen von `dev` nur Build und die eigenen Tests wiederholen.
- Unter Last rote Tests zuerst einzeln wiederholen. Prüfbefehle nie mit einer Pipe in einer `&&`-Kette (verdeckt den Exitcode); Ausgabe in eine Datei schreiben und den Exitcode lesen.

### Rundenabschluss

Einmal am Ende der Runde auf `dev` die volle Prüfkette aus Abschnitt 3, das Web mit `npm test -- --maxWorkers=3`. Laufzeiten (Messung 01.11.2026): API-Suite 14 min, Desktop-Suite 50 min in einem Prozess; schneller ist die Desktop-Suite ohne `ViewTests` (`--filter-not-class "AispaStudio.Desktop.Tests.ViewTests"`, rund 2,5 min) plus `pwsh scripts/view-tests.ps1` (vier Prozesse, rund 13 min). Vor jedem vollen Lauf die Sperre `.test-logs/full-suite.lock/` per `mkdir` nehmen und danach freigeben: zwei volle Suiten zugleich verdoppeln die Laufzeit und machen sich gegenseitig rot. Ein roter Test wird über die Squash-Commits (einer je Aufgabe) eingegrenzt und als neue Aufgabe behoben. Danach die Handtestblöcke der Runde in [umsetzung/handtests.md](umsetzung/handtests.md) auf Vollständigkeit prüfen.

### Abschluss einer Aufgabe

Der Projekt-Skill `task-close` (`.claude/skills/task-close/`) führt diese Schritte als Checkliste mit Befehlen und Prüfungen.

1. Doku nach Abschnitt 2 Schritt 3; im [PR-Protokoll](umsetzung/pr-protokoll.md) ein Abschnitt `## <Titel> (Aufgabe T-NNN, TT.MM.JJJJ)` mit **Auftrag**, **Was**, **Detailentscheidungen**, **Geprüft**, **Nicht geprüft**, **Beim Schreiben festgestellt**; geschlossene Befunde dort als „erledigt in T-NNN“ kennzeichnen, wo ihre Ä-Nummer steht (Rundenakte unter `work/rounds/`, Stand).
2. Handtestblock in [umsetzung/handtests.md](umsetzung/handtests.md): `### T-NNN – Titel`, eine Startzeile, nummerierte Schritte mit „Expected:“, `Closes: Ä…`, `Result: ☐`; ändert die Aufgabe nichts Sichtbares, stattdessen T-NNN in die Zeile „Ohne Block“ eintragen.
3. Im Worktree committen, `dev` einmischen, Build und eigene Tests wiederholen.
4. Squash im Hauptordner unter der Sperre `.test-logs/squash.lock/` (`mkdir -p .test-logs`, dann `mkdir .test-logs/squash.lock`; scheitert er, squasht gerade eine andere Sitzung, dann warten):
   - vorher `git status --porcelain` leer und kein `.git/SQUASH_MSG`;
   - `git merge --squash slice-NNN`, Konflikte so lösen, dass beide Seiten erhalten bleiben (keine Konfliktmarker, Zahl der `Result:`-Zeilen der Testliste vorher und nachher vergleichen);
   - ein Commit, Text per `git commit -F <datei>` (Git Bash schreibt einen Text mit führendem `/` in einen Pfad um);
   - die Aufgabe in `.code-guardian-todo.md` mit dem Commit-SHA als Beleg abhaken;
   - Sperre freigeben.
5. Junction lösen, Worktree entfernen (oben), Branch löschen: nach einem Squash mit `git branch -D` (`-d` scheitert, weil der Branch kein Vorfahr von `dev` ist), erst nachdem belegt ist, dass der Squash den ganzen Branch enthält (`git diff <squash-SHA> <branch>` leer, wenn `dev` dazwischen stillstand).

### Startvorlage

Im Hauptordner `claude --model <Modell>` starten und eingeben:

> Setze T-00N um, Plan: `<Detaildatei der Aufgabe>`. Code Guardian BUILD MODE. Worktree `.claude/worktrees/slice-<nr>` nach `docs/arbeitsweise.md` Abschnitt 9. Prüfe nur Basis und zuständige Tests, keine vollen Suiten. Handtestblock in `docs/umsetzung/handtests.md`. Danach `dev` einmischen, Build und eigene Tests erneut, unter `.test-logs/squash.lock` ein Squash-Commit auf `dev` und T-00N abhaken, Junction lösen, Worktree entfernen.
