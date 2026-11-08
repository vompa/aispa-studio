# Übergabe: Einzelplatz-Betrieb „standalone“ und zuschaltbare Webmodule

Stand 18.10.2026, 21:40 UTC. Thread „Windows-App eigenständig mit lokaler Datenbank“, gestoppt auf Anweisung des Auftraggebers.

## Auftrag (Auftraggeber, 18.10.2026)

- „alle webmodule optional … auch die datenbank kann lokal im windows client betrieben werden“
- „die date sollen sich dann abgleichen sobald eine postgres dazugeschaltet wird“
- „soll mit flag standalone laufen und sobald das flag gesetzt ist greift eine klassische benutzerverwaltung“
- „sollte aber wie im offline betrieb eh schon das meiste berücksichtigt sein“ (auf PR #63 aufbauen)

Grundsatzentscheidung: [ADR 0041](https://github.com/vompa/aispa-studio/blob/main/docs/entscheidungen/0041-einzelplatz-standalone-und-zuschaltbare-webmodule.md), Umsetzung in fünf PRs.

## Gemergt

- **PR #80**: Flag `Hosting:Standalone`, Modulschalter `Modules:<Name>` (`OnlineBooking`, `BookingIntake`, `Assistant`, `Reminders`, `Rebooking`, `Benchmark`), `GET /api/system/modules`, Ausblenden in Web und Windows-App. Ausgeschaltete Module antworten mit 404, ihre Hintergrundläufe starten nicht.

## Offen, nicht gemergt

- **PR #88** (Branch `claude/project-thread-osadul`, Head `e96aaa7`): klassische Benutzerverwaltung. Einrichtung beim ersten Start, Anmeldung mit Benutzername und Passwort, Zugänge mit Startpasswort, Zurücksetzen durch die Leitung, Pflicht-Passwortwechsel; Web und Windows-App. Auf main von 21:30 UTC gebracht, lokal 454 .NET-Tests und 142 Web-Tests grün; die CI lief beim Stopp noch. Ist sie grün, kann der PR per Squash gemergt werden.
- **PR 3 als Entwurf, nicht committet**: [standalone-pr3-entwurf.patch](../standalone-pr3-entwurf.patch) (auf PR #88 anwenden mit `git apply`). Inhalt:
  - API liefert die Web-Oberfläche aus, wenn `Hosting:WebRoot` gesetzt ist (`System/TeamWebRoot.cs`, Tests in `TeamWebRootTests.cs`).
  - Windows-App: `Services/LocalServer.cs` legt beim ersten Start mit `initdb` eine PostgreSQL unter `%LOCALAPPDATA%\AispaStudio\daten\pg` an (Benutzer `aispa`, scram-sha-256, UTF8, ICU `de-AT`), startet sie mit `pg_ctl` an 127.0.0.1, legt `aispadb` an und startet die API mit Einzelplatz-Umgebung. Ports (Wunsch 55432 und 55086) in `einzelplatz.json`, Datenbank-Passwort und Schlüssel für Gesundheitsdaten DPAPI-geschützt in `zugang.bin`.
  - `--standalone` schaltet die App in den Einzelplatz-Betrieb und merkt es sich, `--server` schaltet zurück (`StandaloneMode.cs`).
  - Einrichtungsformular direkt in der Windows-App (`LoginViewModel`, `LoginView.axaml`).
  - CI: API, Web-Oberfläche und PostgreSQL des Runner-Images (`$env:PGROOT`: bin, lib, share) ins Windows-Paket, danach `LocalServerPackageTests` gegen das echte Paket.
  - Geprüft: Desktop-Tests mit nachgespieltem Prozessstart grün; initdb- und pg_ctl-Aufrufe in PostgreSQL 17 geprüft; API in Production mit Standalone, WebRoot und Migration gegen echte PostgreSQL gestartet, Einrichtung, Anmeldung und Standorte per curl geprüft.
  - **Noch nicht fertig:** Der Test mit echtem Paket lief unter Linux noch nicht durch (Pfadfehler beim Symlink auf die PostgreSQL-Binärdateien, noch nicht untersucht). Der CI-Filter ist falsch: statt `-- --filter-class "*LocalServerPackageTests"` die Schreibweise der anderen Testaufrufe im Repo prüfen. `PGROOT` auf `windows-latest` ist angenommen, nicht geprüft. Doku (PR-Protokoll, Stand, README, ADR-Schritt 3) fehlt noch.

## Entscheidungen unterwegs

- PostgreSQL auch lokal statt SQLite: ein Code, Doppelbuchungsschutz per Exclusion Constraint, Kassa-Sperren und Wolverine bleiben gleich.
- Module bleiben im API-Vertrag und antworten ausgeschaltet mit 404, damit Web, App und Tests nicht je Betriebsart verzweigen.
- Anamnese am Tablet und auf Papier gehört zum Kern, nur die Anamnese per Link zum Modul `OnlineBooking`.
- Klassische Anmeldung nutzt dieselbe Identity-Tabelle wie Passkeys, damit Konten beim Umstieg auf einen Server bleiben. Passwort mindestens acht Zeichen, Sperre nach fünf Fehlversuchen für fünf Minuten (423), Umlaute im Benutzernamen, keine E-Mail nötig.
- Im Einzelplatz gibt es keine Passkeys, Einladungen und Spa-Registrierung (404), im Server-Betrieb keine Passwort-Endpunkte.
- Die Windows-App hat im Einzelplatz keinen Offline-Stand, weil die Datenbank auf demselben PC liegt.
- Unlesbare Zugangsdaten werden nie still neu angelegt, sonst wären Datenbank und Gesundheitsdaten verloren.

## Nächste Schritte in dieser Reihenfolge

1. PR #88 mergen, sobald die CI grün ist.
2. PR 3 aus dem Entwurf fertigstellen: Pakettest unter Linux und Windows grün, CI-Filter korrigieren, Doku nachziehen.
3. PR 4: Änderungsprotokoll (Schema `sync`) und Abgleich in beide Richtungen mit `Sync:CentralConnectionString`. Regeln stehen in ADR 0041 Abschnitt 4: zuletzt gespeichert gilt bei Stammdaten, über Termine entscheidet die zentrale Seite und Konflikte werden angezeigt, jede Kasse gehört einer Stelle, Belege werden nie nachsigniert.
4. PR 5: tägliche Sicherung auf ein zweites Laufwerk mit Erinnerung nach sieben Tagen, RKSV-Ausfallbelege im Einzelplatz, Exporte (DEP, DSFinV-K).

## Für später

- Ein Wiederherstellungsschlüssel für Gesundheitsdaten und Datenbank-Passwort gehört in die Sicherung. DPAPI bindet beide an das Windows-Konto, ein neuer PC kann sie sonst nicht lesen.
- Die API wird beim Beenden hart beendet; ein geordnetes Herunterfahren wäre schöner.
- Mehrplatz im eigenen Netz: einen Einzelplatz-PC als Server freigeben (Bindung an das LAN statt 127.0.0.1, TLS).
- Freischalten der Module je Spa für die Abo-Stufen baut auf denselben Schaltern auf.
- Offene Punkte aus PR #88: Passwort vergessen ohne Leitung, Passkeys zusätzlich nach dem Umstieg auf einen Server.
- Ein Formatierer ist im Web-Projekt nicht festgelegt. PR #88 hat einige Dateien mit Prettier umformatiert (einfache Anführungszeichen, ohne Semikolon, Breite 160); ein gemeinsamer Formatierer würde solche Diffs vermeiden.
