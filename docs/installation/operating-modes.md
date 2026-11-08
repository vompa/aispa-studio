# Betriebsarten und Module: Referenz

Technische Einzelheiten zu Einzelplatz, Mehrplatz im eigenen Netz, Sicherung, Abgleich und Server mit mehreren Instanzen. Schritt für Schritt: [Einzelplatz](standalone.md) und [Server](server.md).

## Betriebsart und Webmodule

Server-Betrieb ist die Vorgabe. `Hosting:Standalone=true` (Umgebungsvariable `Hosting__Standalone`) schaltet den
Einzelplatz-Betrieb ein ([ADR 0041](../entscheidungen/0041-einzelplatz-standalone-und-zuschaltbare-webmodule.md)): Datenbank und
API laufen dann lokal beim Windows-Client, und alle Webmodule sind aus. Jedes Modul lässt sich mit `Modules:<Name>=true|false`
einzeln setzen, in beiden Betriebsarten:

| Modul | Inhalt |
| --- | --- |
| `OnlineBooking` | Kundenbereich: Online-Buchung, Kundenkonten, Warteliste online, Gutscheinkauf, Kartenhinterlegung, Online-Erstattung, Buchungsregeln |
| `BookingIntake` | Terminannahme per Mail, SMS, WhatsApp, Anruf, Buchungsplattformen, Kalender-Feeds, Betreiber-Seite |
| `Assistant` | KI-Assistent |
| `Reminders` | Terminerinnerungen |
| `Rebooking` | Erinnerung an die nächste Behandlung |
| `Benchmark` | Vergleich mit anderen Spas |

Ausgeschaltete Module antworten mit 404 („Dieses Modul ist ausgeschaltet.“), ihre Hintergrundläufe starten nicht, und
die Web-Oberfläche blendet sie nach `GET /api/system/modules` aus. Kalender, Kassa, Stammdaten, Kunden, Dienstplan,
Zeiterfassung, Lager, Kosten, Berichte, Exporte, Zugänge und die Anamnese am Tablet und auf Papier gehören zum Kern.

Zusätzlich hat jedes Spa eine Abo-Stufe ([ADR 0050](../entscheidungen/0050-abo-stufe-schaltet-webmodule-frei.md)):
Start enthält `OnlineBooking` und `Reminders`, Spa dazu `Assistant` und `Rebooking`, Spa Plus dazu `BookingIntake` und
`Benchmark`. Ein Modul ist an, wenn die Installation und die Stufe es enthalten; fehlt es nur in der Stufe, antwortet es mit
403 („Nicht in der Abo-Stufe.“). Der Kern gehört zu jeder Stufe. Vorgabe ist Spa Plus; der Betreiber ändert die Stufe auf
`/betreiber/`.

Im Einzelplatz-Betrieb gilt eine klassische Benutzerverwaltung statt Passkeys und Einladungen: Beim ersten Start richtet die
Web-Oberfläche (oder `POST /api/auth/standalone/setup`) Spa, ersten Standort und das Inhaber-Konto mit Benutzername und Passwort
ein. Danach legt die Leitung unter „Zugänge“ Konten mit Benutzername und Startpasswort an (`POST /api/staff/accounts`), das die
Person bei der ersten Anmeldung ändern muss; „Passwort zurücksetzen“ vergibt ein neues Startpasswort. Passwörter haben
mindestens acht Zeichen; nach fünf Fehlversuchen ist das Konto fünf Minuten gesperrt. Die Windows-App meldet sich mit
Benutzername und Passwort an und fragt ein Startpasswort gleich nach der Anmeldung ab. Ohne Leitung hilft „Passwort vergessen?“
auf der Anmeldung: Am Haupt-PC setzt der Wiederherstellungsschlüssel aus der Sicherung das Passwort eines Kontos neu
(`POST /api/auth/standalone/recover`, [ADR 0051](../entscheidungen/0051-passwort-vergessen-mit-wiederherstellungsschluessel.md)).

**Windows-App als Einzelplatz.** `AispaStudio.Desktop.exe --standalone` schaltet die App in den Einzelplatz-Betrieb und merkt
sich das (`--server` schaltet zurück). Sie startet dann die mitgelieferte PostgreSQL (`postgres\bin` im Paket) und die API
(`api\`) selbst, beide nur an 127.0.0.1, und richtet beim ersten Start das Spa gleich in der App ein. Daten liegen unter
`%LOCALAPPDATA%\AispaStudio\daten` (Datenbank in `pg`, Protokolle in `protokolle`, Ports in `einzelplatz.json`,
Datenbank-Passwort und Schlüssel für Gesundheitsdaten DPAPI-geschützt in `zugang.bin`). Die Web-Apps (Anmelden, Mein Tag, Dashboard,
Hilfe) liefert dieselbe API aus (`Hosting:WebRoot`), unter der Adresse, die die App auf dem Startbildschirm zeigt (meist
`http://127.0.0.1:55086`). Ohne `zugang.bin` sind die Gesundheitsdaten nicht mehr lesbar. Beim Schließen beendet die App die API
geordnet: Sie legt `api.stop` im Datenordner an (`Hosting:StopFile`), die API schließt laufende Anfragen und Hintergrundarbeit ab
und endet; nur wenn sie nach 20 Sekunden noch läuft, wird sie hart beendet.

**Mehrplatz im eigenen Netz** ([ADR 0049](../entscheidungen/0049-mehrplatz-im-eigenen-netz.md)). Am Haupt-PC gibt, wer den
Standort verwalten darf, unter „Im Netz“ den Einzelplatz frei. Die App stellt dabei einmal ein eigenes Zertifikat aus (`netz.pfx`,
Passwort in `zugang.bin`) und startet die API zusätzlich unter `https://*:55443` (Kestrel-Endpunkt `Netz`); die Datenbank bleibt
an 127.0.0.1. Die Seite zeigt die Adressen des PCs und den Verbindungscode (20 Zeichen aus dem Fingerabdruck des Zertifikats).
An jedem weiteren PC startet man die App ohne `--standalone` und trägt beides auf dem Startbildschirm unter „Mit Haupt-PC im Netz
verbinden“ ein; die App merkt es sich in `einstellungen.json` (`serverAddress`, `serverCode`), startet neu und nimmt nur noch das
Zertifikat mit diesem Code an. Angemeldet wird mit denselben Zugängen. Das Spa einrichten geht nur am Haupt-PC selbst. Windows
fragt beim ersten Freigeben nach der Firewall: für private Netzwerke erlauben.

**Tägliche Sicherung im Einzelplatz.** Unter „Sicherung“ wählt, wer den Standort verwalten darf, einen Ordner auf einem zweiten
Laufwerk oder im Netz. Dabei zeigt die App einmal einen Wiederherstellungsschlüssel (sechs Vierergruppen), der getrennt vom PC
aufzubewahren ist. Danach sichert die App einmal am Tag, solange sie läuft, mit `pg_dump` (`aispa-JJJJMMTT-hhmmss.dump`) und legt
den mit dem Wiederherstellungsschlüssel verschlüsselten Schlüssel für Gesundheitsdaten daneben (`….schluessel.json`). Die letzten 14
Sicherungen bleiben. Fehlt eine Sicherung oder ist die letzte älter als sieben Tage, steht ein Hinweis über allen Seiten.
Unter Windows legt die App dazu die Aufgabe „AISPA Studio Sicherung“ an: Sie startet die App jeden Abend um 23 Uhr mit `--sichern`
ohne Fenster, sichert, wenn die letzte Sicherung mindestens zwölf Stunden alt ist, und beendet die Datenbank wieder, wenn sie sie
dafür gestartet hat. War der PC aus, holt Windows sie beim nächsten Start nach. Sie läuft im angemeldeten Windows-Konto, weil nur
dieses `zugang.bin` lesen kann; Protokoll in `protokolle\sicherung.log`.

**Wiederherstellen.** Auf einem neuen PC (`--standalone`, vor „Spa einrichten“) oder wenn `zugang.bin` nicht mehr lesbar ist
(Startbildschirm), führt „Aus einer Sicherung wiederherstellen …“ durch: Sicherung wählen, Wiederherstellungsschlüssel eintippen.
Die App legt eine neue Datenbank an, spielt die Sicherung mit `pg_restore` ein und holt den Schlüssel für Gesundheitsdaten zurück.
Bisherige Daten bleiben als `pg-vor-wiederherstellung-…` und `zugang-vor-wiederherstellung-….bin` liegen. Ist schon ein Spa
eingerichtet, lehnt die App ab, damit nichts überschrieben wird.

**Abgleich mit einer zentralen Datenbank.** Ist `Sync:CentralConnectionString` gesetzt (beim Einzelplatz in der Windows-App unter
„Abgleich“ eingetragen, DPAPI-geschützt in `zugang.bin`, nie im Repository), gleicht die API jede Minute (`Sync:IntervalSeconds`) in
beide Richtungen ab. `Sync:SiteId` benennt die Stelle (Standard: Rechnername). Stand und Fehler stehen in `sync.peers`, abgelehnte
Zeilen in `sync.conflicts`. Die Windows-App zeigt auf der Seite „Abgleich“ den Stand und die abgelehnten Einträge; wer buchen darf, markiert sie nach dem Klären als erledigt. Eintragen und Austragen der zentralen Datenbank darf, wer den Standort verwalten darf; die App startet die API dafür neu.
Damit jede Stelle die Anamnesen der anderen lesen kann, gibt es den **Schlüsselcode des Spas** ([ADR 0048](../entscheidungen/0048-schluessel-fuer-gesundheitsdaten-beim-abgleich.md)): bei der ersten Stelle das Feld leer lassen, die App zeigt dann den Code einmal; jede weitere Stelle trägt ihn beim Verbinden ein. Unter „Abgleich“ lässt er sich der Leitung wieder anzeigen. Passt er nicht, bricht der Abgleich ab, bevor etwas übertragen wird (`HealthData:ShareCode`). Ein neuer PC wählt bei der Einrichtung „Mit einem bestehenden Spa verbinden“, trägt zentrale Datenbank und Code ein und meldet sich nach dem ersten Abgleich mit den gewohnten Zugängen an.
Das Protokoll der zentralen Datenbank räumt der Abgleich nach 90 Tagen auf (`Sync:CentralRetentionDays`, 0 = nie). Eine Stelle, die länger nicht verbunden war, und jeder neue PC holen dann den ganzen Bestand ihres Spas statt des Protokolls.

**Mehrere Instanzen im Server-Betrieb.** `Hosting:Role` (`All`, `Api`, `Worker`; Standard `All` wie bisher) teilt Anfragen und
Hintergrundläufe auf ([ADR 0087](../entscheidungen/0087-mehrinstanz-betrieb.md)): `Api` startet keine Hintergrundläufe, `Worker`
beantwortet außer den Health-Endpunkten keine Anfragen; der Einzelplatz läuft immer mit `All`. Wolverine läuft in jeder Rolle; die Mail-Queue
der Terminannahme und „Jetzt prüfen“ laufen mit `Api` und `Worker` über eine Queue in der Datenbank, die nur der Worker abarbeitet ([Server-Einstellungen](../../web/src/help/content/server.md#mail-queue-mit-mehreren-instanzen-mail-queue)).
`DataProtection:Store=Database` legt den Schlüsselring für Anmelde-Cookies und gespeicherte Zugänge in die Datenbank, damit alle
Instanzen ihn teilen; die Schlüssel aus dem bisherigen Ordner übernimmt der Server bei jedem Start mit `Database` ([Server-Einstellungen](../../web/src/help/content/server.md#schlüsselring-des-servers-schluesselring)).
Hinter einem Proxy oder Ingress übernimmt `Hosting:ForwardedHeaders:Enabled=true` Client-Adresse und Schema aus `X-Forwarded-For` und `X-Forwarded-Proto`, aber nur von den
Netzen in `KnownNetworks` und den Proxys in `KnownProxies`; ohne Angaben nur von Loopback, im Einzelplatz nie ([Server-Einstellungen](../../web/src/help/content/server.md#hinter-proxy-oder-ingress-proxy)).
`Hosting:HealthEndpoints=true` schaltet `/health` (bereit) und `/alive` (lebt) auch im Betrieb ein, für die Proben von Container Apps oder Kubernetes; sie melden nur den Status, im Einzelplatz
bricht der Start ab ([Server-Einstellungen](../../web/src/help/content/server.md#betriebsart-und-module-betriebsart)).
`Live:Backplane=Redis` mit `ConnectionStrings:redis` verteilt die Live-Meldungen (Kalender, Tafel, Posteingang) über Redis an alle Instanzen, auch die des Workers; der Ingress braucht Sticky Sessions
([Server-Einstellungen](../../web/src/help/content/server.md#live-meldungen-mit-mehreren-instanzen-live-backplane)). Im AppHost schaltet `-- --Live:Backplane=Redis` einen Valkey-Container (Redis-Protokoll, [ADR 0089](../entscheidungen/0089-valkey-statt-redis-8.md)) dazu.
