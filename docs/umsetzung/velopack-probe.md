# Velopack-Machbarkeitsprobe (T-130)

Befund zu [ADR 0104](../entscheidungen/0104-gefuehrte-ersteinrichtung.md), Entscheidung 1. Stand 27.11.2026, Velopack CLI 1.2.161 (`vpk`). Gemessen wurde mit der echten Windows-App ohne PostgreSQL und API; diese kommen in der CI dazu. Probe-Dateien lagen nur in `<Probe>` (`C:\Development\Caches\velopack-probe`). Nichts wurde installiert, kein Setup gestartet außer `--help`.

## Empfehlung

| Punkt | Empfehlung |
| --- | --- |
| packId | `AispaStudioApp` (nicht `AispaStudio`, siehe Punkt 1) |
| Befehlszeile | `vpk [win] pack --packId AispaStudioApp --packVersion <SemVer> --packDir <publish/desktop> --mainExe AispaStudio.Desktop.exe --packTitle "AISPA Studio" --icon src/AispaStudio.Desktop/Assets/aispa.ico --runtime win-x64 --framework webview2,vcredist143-x64 --shortcuts StartMenuRoot --outputDir <releases>` |
| Voraussetzung im Code | `VelopackApp.Build().Run();` als erste Zeile von `Program.Main` (NuGet `Velopack`), sonst bricht `vpk pack` ab |
| Framework-Liste | `webview2` (belegt), `vcredist143-x64` als Absicherung (PostgreSQL-Abhängigkeit nicht belegt) |
| Ort in der CI | `vpk pack` im Windows-Job `desktop` direkt nach dem Paket mit Lizenzhinweisen (dort ist es belegt); `vpk upload github` in `windows-download` |
| Version | `1.0.<github.run_number>` als `--packVersion`, nur in der CI, keine Datei im Repository |

## 1. Installationsordner und Datenordner

**Befund.** Velopack installiert nach `%LOCALAPPDATA%\<packId>` und legt dort `current` (die App), `packages`, `Update.exe` und einen Starter an. Es gehört ihm der **ganze** Ordner: Die Deinstallation löscht den Inhalt des Wurzelordners vollständig, und ein erneutes Ausführen von `Setup.exe` auf einem nicht leeren Ordner benennt ihn um, leert ihn und baut ihn neu auf. Mit `packId = AispaStudio` läge `daten` im Wurzelordner und ginge bei Deinstallation und bei Reparatur-Installation verloren. Ein Update ersetzt nur `current`. `packId = AispaStudioApp` trennt beides: Programm unter `%LOCALAPPDATA%\AispaStudioApp`, Daten weiter unter `%LOCALAPPDATA%\AispaStudio\daten`. `Setup.exe --help` kennt `-s/--silent` und `-t/--installto <DIR>` sowie Startparameter nach `--`.

**Beleg.** Quelltext `src/bins/src/commands/uninstall.rs` (`remove_dir_contents(&root_path)`) und `install.rs` (`ensure_empty_dir(&root_path)`, Umbenennen des alten Ordners) in github.com/velopack/velopack; Doku docs.velopack.io/packaging/installer („current fully replaced“, Ziel `%LocalAppData%\{packId}`). `Setup.exe --help` wurde ausgeführt (Ausgabe Optionen siehe oben). Installation, Update und Deinstallation selbst wurden nicht ausgeführt: belegt durch Quelltext, nicht durch Ausführung.

**Folge für T-131.** packId `AispaStudioApp` festschreiben (eine spätere Änderung wäre ein zweites Programm). Die Anleitung nennt beide Ordner. Wer `--installto` nutzt, darf nie den Datenordner angeben. Ein Test sollte prüfen, dass `LocalServerPaths.Default` den Datenordner nicht unter dem Programmordner anlegt.

## 2. Pfad der Haupt-exe und Windows-Aufgabe

**Befund.** Die App liegt immer unter `…\AispaStudioApp\current\AispaStudio.Desktop.exe`; der Pfad bleibt über Updates gleich, weil `current` ersetzt wird. Zusätzlich legt Velopack einen stabilen Starter im Wurzelordner ab: `…\AispaStudioApp\AISPA Studio.exe` (Name aus `--packTitle`). Der Starter ruft `Update.exe start` auf und beendet sich sofort; die App läuft als Kindprozess. Eine Aufgabe auf den Starter würde deshalb nach dem Start schon als beendet gelten (die Einstellung „nie doppelt“ greift nicht). Die Aufgabe sollte weiter auf `Environment.ProcessPath` zeigen, das unter Velopack `current\AispaStudio.Desktop.exe` ist. Offen: Während eines Updates fehlt `current` kurz; startet die Aufgabe genau dann, holt `StartWhenAvailable` sie nicht nach.

**Beleg.** Das gepackte `.nupkg` enthält `lib/app/AISPA Studio_ExecutionStub.exe`; `bundle.rs::extract_stubs_to_dir` entpackt ihn nach `<Wurzel>\AISPA Studio.exe`; `stub.rs` startet `Update.exe start -- <Argumente>` (Quelltext). Der Pfad nach einem Update: Doku „current is fully replaced“. Nicht ausgeführt.

**Folge für T-131.** `ScheduledBackup.cs` bleibt unverändert; Aufgabe nach der Installation einmal registrieren lassen (die App tut das beim Einschalten der Sicherung). Zu prüfen von Hand: Aufgabe nach einem echten Update.

## 3. `vpk pack` für diese App

**Befund.** Der Befehl der Empfehlung lief durch (`--skipVeloAppCheck`, weil `Program.Main` noch kein Velopack aufruft). Ohne `--runtime win-x64` baut `vpk` für x86 (Warnung). Ohne den Aufruf im Code bricht `vpk pack` ab. Ergebnis für 127 MB Publish-Ordner (252 Dateien, ohne PostgreSQL/API):

| Datei | Größe |
| --- | --- |
| `AispaStudioApp-1.0.1-full.nupkg` | 56,8 MB |
| `AispaStudioApp-win-Setup.exe` | 64,5 MB (enthält das volle Paket) |
| `AispaStudioApp-win-Portable.zip` | 56,9 MB |
| `RELEASES`, `releases.win.json`, `assets.win.json` | unter 1 KB |

Dauer 13 bis 16 Sekunden. Das Paket ist rund 45 % des Publish-Ordners groß.

**Beleg.** Lauf `vpk [win] pack …` in `<Probe>\rel2`, Meldungen „Setup bundle created“, „No signing parameters provided“; `ls -l` der Ausgabe. Der Fehler ohne Aufruf: „Unable to verify VelopackApp is called“.

**Folge für T-131.** `Velopack` in `Directory.Packages.props` aufnehmen, `VelopackApp.Build().Run()` vor allen anderen Zweigen in `Program.Main`. Die Haken `--veloapp-*` dürfen kein Fenster öffnen (Punkt 6). Die Portable-ZIP lässt sich mit `--noPortable` sparen, falls die vorhandene ZIP bleibt.

## 4. Delta-Pakete und Größe

**Befund.** Eine zweite Fassung 1.0.2 (200 KB geänderte Datei, eine neue Datei) erzeugte `AispaStudioApp-1.0.2-delta.nupkg` mit **0,32 MB** neben dem vollen Paket (56,95 MB). „250 Dateien verarbeitet, 2 geändert, 248 unverändert, 1 neu“. Schätzung für rund 185 MB (nicht gemessen): volles Paket etwa 80 bis 90 MB, `Setup.exe` etwa 8 MB mehr. Ein Delta wird so groß wie die tatsächlich geänderten Dateien: PostgreSQL, .NET-Laufzeit und Avalonia bleiben gleich, bei jedem Build ändern sich die eigenen Assemblies, `api` und `web\assets` (neue Dateinamen mit Hash); zu erwarten sind einige MB bis etwa 20 MB.

**Beleg.** `vpk pack` für 1.0.1 und 1.0.2 im selben `--outputDir`; Ausgabe „Delta processed 0250 files. 0002 patched …“; `ls -l`.

**Folge für T-131.** Für Deltas braucht der Paketbau die Vorfassung im Ausgabeordner (Punkt 7). Die Wirkung bei echten 185 MB ist beim ersten Lauf in der CI zu messen.

## 5. Visual-C++-Runtime von PostgreSQL

**Befund.** Nicht belegt. Auf diesem PC gibt es kein `postgres.exe` (kein `C:\Program Files\PostgreSQL`, keiner im Repository oder in `src/AispaStudio.Desktop/bin`); ein PE-Import-Leser konnte nicht angesetzt werden. Das EDB-Build ist für MSVC gebaut und braucht üblicherweise die VC++-Laufzeit; ob die benötigten DLLs mitliegen, ist offen. Absicherung: `--framework webview2,vcredist143-x64` (Velopack installiert die Laufzeit nur, wenn sie fehlt).

**Beleg.** Suche `find … -name postgres.exe` ohne Treffer. Doku-Beispiel für die Framework-Liste: `net6-x64-desktop,vcredist143-x64` (`vpk pack -H`).

**Folge für T-131.** In der CI nach dem Kopieren der PostgreSQL-Dateien die Importe von `postgres.exe` auflisten (zum Beispiel `dumpbin /dependents` im Windows-Job) und das Ergebnis protokollieren; Entscheidung über `vcredist143-x64` danach.

## 6. Startparameter und `--veloapp-*`

**Befund.** Velopack ruft die Haupt-exe mit einem dieser Parameter auf: `--veloapp-install`, `--veloapp-updated`, `--veloapp-obsolete`, `--veloapp-uninstall` (jeweils mit Versionsnummer); die App muss schnell enden (30 s bei Installation und Deinstallation, 15 s bei den anderen), ohne Fenster. `VelopackApp.Build().Run()` fängt sie ab. Ohne diesen Aufruf würde `Program.Main` die Parameter an Avalonia weitergeben und die Oberfläche starten (aus der Doku abgeleitet, nicht ausgeführt). Eigene Parameter kommen an: `Setup.exe -- --standalone` und der Starter geben Argumente weiter. `StandaloneMode` und `Program.cs` prüfen nur auf bekannte Schalter und ignorieren andere; `--sichern` erreicht die App unverändert, wenn die Aufgabe direkt auf `current\…exe` zeigt. Von Velopack angelegte Verknüpfungen tragen keine Parameter; eine von Hand ergänzte Verknüpfung mit Parametern bleibt bei Updates unangetastet (Quelltext `shortcuts.rs` filtert sie). Mit der Startauswahl (T-132) ist der Schalter nicht mehr nötig.

**Beleg.** Doku docs.velopack.io/integrating/hooks; Quelltext `stub.rs`, `start_windows_impl.rs`, `shortcuts.rs`; `Setup.exe --help` (`[EXE_ARGS]… Must be preceded by '--'`); `src/AispaStudio.Desktop/Program.cs` (Zeilen 12 bis 30), `App.axaml.cs` (39 bis 41), `Services/StandaloneMode.cs`.

**Folge für T-131.** Der Velopack-Aufruf steht vor dem Zweig für `ScheduledBackup.Argument`. Kein Konflikt mit `--standalone`, `--server`, `--demo`.

## 7. CI

**Befund.** `vpk` kann mit der Direktive `[win]` für Windows auch auf anderen Systemen packen (Quelltext `Velopack.Vpk/Program.cs`, „cross-compiling“); auf `ubuntu-latest` wurde das hier nicht ausgeführt. Belegt ist nur der Lauf unter Windows, und die PostgreSQL-Dateien liegen im Windows-Job. Der Job `windows-download` löscht heute das Release `windows-app` samt Tag und legt es neu an; damit gingen die Vorfassungen für Deltas verloren, und das Release ist ein Pre-Release. Ablauf laut Doku: `vpk download github --repoUrl <Repository> --token <Token> --pre` (holt die letzte Fassung nach `--outputDir`), danach `vpk pack` im selben Ordner, danach `vpk upload github --repoUrl … --token … --pre --merge --releaseName … --tag …` (ohne `--publish` bleibt das Release ein Entwurf; `contents: write` nötig). Das Tool lässt sich mit `dotnet tool install -g vpk` einrichten. Nur beschrieben, nichts ausgeführt.

**Beleg.** `vpk download github -H`, `vpk upload github -H` (Optionen `--repoUrl`, `--token`, `--pre`, `--merge`, `--publish`, `--releaseName`, `--tag`); Doku docs.velopack.io/distributing/github-actions; `.github/workflows/ci.yml` Zeilen 231 bis 262.

**Folge für T-131.** Eigenes Release für Setup und Updates (nicht das tägliche `windows-app`, das die ZIP bleibt), das nicht gelöscht, sondern ergänzt wird; Pack im Windows-Job, Upload dort oder in `windows-download` mit hochgeladenem Ordner.

## 8. Versionsnummer

**Befund.** Velopack verlangt SemVer 2 und vergleicht danach; neuere Fassungen müssen größer sein. Vorschlag: `1.0.${{ github.run_number }}` in der CI (steigt je Lauf, ändert keine Datei im Repository). Bei der Wiederholung eines Laufs bleibt `run_number` gleich; der Upload derselben Fassung ist dann zu verhindern oder zu ersetzen (nicht geprüft). Der Wechsel von `1.0.x` auf eine spätere Zählung (zum Beispiel `1.1.0`) ist jederzeit möglich, solange die Nummer nur steigt. Die gleiche Zahl kann mit `-p:Version=…` an `dotnet publish` gehen, damit die App ihre Fassung kennt.

**Beleg.** `vpk pack --packVersion` verlangt SemVer (`vpk pack -H`, „Current version for application bundle“); Pack von 1.0.1 und 1.0.2 lief. Die Versionsseite der Doku war nicht abrufbar: SemVer-Pflicht aus der Aufgabenstellung übernommen, nicht eigens belegt.

**Folge für T-131.** `github.run_number` als Quelle; Pre-Release-Kennzeichen (`1.0.5-beta`) nur, wenn ein zweiter Kanal gewünscht ist.
