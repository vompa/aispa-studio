# 0041. Einzelplatz-Betrieb „standalone“ mit lokaler Datenbank, Webmodule zuschaltbar

- **Status:** angenommen
- **Datum:** 18.10.2026
- **Entschieden von:** Auftraggeber (Vorgaben), Ausgestaltung Claude (autonom, siehe ADR 0019)

## Kontext

Auftraggeber, 18.10.2026:

- „berücksichtige auch das alle webmodule optional sind auch die datenbank kann lokal im windows client betrieben werden“
- „die date sollen sich dann abgleichen sobald eine postgres dazugeschaltet wird“
- „soll mit flag standalone laufen und sobald das flag gesetzt ist greift eine klassische benutzerverwaltung“

Bisher setzt alles einen Server voraus: PostgreSQL und API laufen zentral (ADR 0006, 0016), die Windows-App ist ein Client mit Offline-Stand (ADR 0005), die Anmeldung läuft über Passkeys (ADR 0007), und alle Module sind immer eingeschaltet. Ein kleines Spa mit einem Empfangs-PC soll die Software aber auch ohne Server und ohne Webfunktionen betreiben können und später auf einen Server umsteigen, ohne Daten zu verlieren.

## Optionen

| Option | Bewertung |
| --- | --- |
| Lokale Datenbank SQLite | klein, aber zweiter Datenbank-Dialekt: Doppelbuchungsschutz per Exclusion Constraint (ADR 0023), `SELECT FOR UPDATE` in der Kassa, Schemas und die Wolverine-Queue gibt es dort nicht; jede Fachlogik müsste doppelt geprüft werden |
| Windows-App als eigener Client mit eigener Logik | widerspricht „Belegungslogik nur im Backend“ und verdoppelt Kassa und Planer |
| PostgreSQL und API lokal im Windows-Client, Flag `standalone` | gewählt: derselbe Code, dieselbe Datenbank, nur anders verpackt |

## Entscheidung

### 1. Flag `Hosting:Standalone`

- Ein Schalter für die ganze Installation (`Hosting:Standalone=true`, als Umgebungsvariable `Hosting__Standalone`). Ohne Schalter bleibt alles wie bisher (Server-Betrieb).
- Im Einzelplatz-Betrieb startet die Windows-App eine mitgelieferte PostgreSQL (portable Binärdateien, Datenverzeichnis unter `%LOCALAPPDATA%\AispaStudio\daten`) und die API als Hintergrundprozess, beide nur an `127.0.0.1`. Die App spricht mit ihr wie mit einem Server; die Team-Oberfläche im Browser liefert dieselbe API unter `http://localhost:<port>` aus.
- Mehrplatz im eigenen Netz bleibt möglich: dieselbe Installation auf einem PC als Server freigeben, die anderen Windows-Apps verbinden sich wie heute mit einem Server. Umgesetzt mit HTTPS, eigenem Zertifikat und Verbindungscode ([ADR 0049](0049-mehrplatz-im-eigenen-netz.md)).

### 2. Webmodule sind zuschaltbar

Je Modul ein Schalter `Modules:<Name>`. Im Server-Betrieb sind alle eingeschaltet, im Einzelplatz-Betrieb alle ausgeschaltet; jeder Schalter lässt sich einzeln setzen (ein Einzelplatz mit Internet kann z. B. Terminerinnerungen per Mail einschalten).

| Modul | Inhalt |
| --- | --- |
| `OnlineBooking` | Kundenbereich: Online-Buchung, Kundenkonten, Anamnese per Link, Warteliste online, Gutscheinkauf, Kartenhinterlegung, Online-Erstattung, Buchungsregeln |
| `BookingIntake` | Terminannahme per Mail, SMS, WhatsApp, Anruf, Buchungsplattformen und Kalender-Feeds, Betreiber-Seite |
| `Assistant` | KI-Assistent je Rolle |
| `Reminders` | Terminerinnerungen |
| `Rebooking` | Erinnerung an die nächste Behandlung |
| `Benchmark` | Vergleich mit anderen Spas (braucht den zentralen Dienst) |

Immer da (Kern): Kalender, Stammdaten, Kunden, Kassa, Dienstplan, Zeiterfassung, Lager, Kosten, Berichte, Buchhaltungs- und Lohnexport, Anamnese am Tablet und auf Papier, Zugänge.

Ein ausgeschaltetes Modul antwortet mit 404 und „Dieses Modul ist ausgeschaltet.“, seine Hintergrundläufe starten nicht, und die Oberflächen blenden es nach `GET /api/system/modules` aus. Der API-Vertrag bleibt gleich, damit Web, Windows-App und Tests nicht je Betriebsart verzweigen. Freischalten je Spa (für Abo-Stufen, ADR 0015) baut auf denselben Schaltern auf ([ADR 0050](0050-abo-stufe-schaltet-webmodule-frei.md)).

### 3. Klassische Benutzerverwaltung im Einzelplatz-Betrieb

- Mit `standalone` melden sich alle mit Benutzername und Passwort an (ASP.NET Core Identity, Passwort-Hash und Sperre nach fünf Fehlversuchen). Passkeys und Einladungen per Mail sind dort aus, weil es keinen Server mit fester Adresse gibt und kein Mailversand vorausgesetzt werden kann.
- Beim ersten Start legt ein Einrichtungsschritt Spa, ersten Standort und Inhaber an. Danach legt die Inhaberin oder Leitung Zugänge selbst an: Name, Benutzername, Rolle und Standorte wie bisher (ADR 0003), dazu ein Startpasswort, das bei der ersten Anmeldung geändert werden muss. Passwort zurücksetzen macht die Leitung; ohne Leitung der Wiederherstellungsschlüssel am Haupt-PC ([ADR 0051](0051-passwort-vergessen-mit-wiederherstellungsschluessel.md)).
- Rollen, Rechte und `RequirePermission` bleiben unverändert; nur der Weg zur Anmeldung ist ein anderer.
- Wird später ein Server dazugeschaltet, bleiben die Konten erhalten; Passkeys lassen sich dann zusätzlich einrichten.

### 4. Abgleich, sobald eine PostgreSQL dazugeschaltet wird

- Eine zentrale Datenbank wird mit `Sync:CentralConnectionString` (TLS, eigenes Datenbankkonto) dazugeschaltet. Ab dann gleicht ein Hintergrundlauf in beide Richtungen ab; ohne Verbindung arbeitet der Einzelplatz weiter und holt den Abgleich nach.
- Jede Änderung schreibt in derselben Transaktion einen Eintrag ins Änderungsprotokoll (Schema `sync`: Tabelle, Id, Zeitpunkt, Herkunft). Der Lauf überträgt die geänderten Zeilen und holt die Änderungen der anderen Stellen. Alle Ids sind Guids, die vorab vergeben werden; Zusammenstöße von Ids gibt es deshalb nicht.
- Das Protokoll schreiben Datenbank-Trigger (`sync.log_change`) an jeder Fachtabelle der Schemas `aispa`, `online` und `health`, nicht EF-Hooks. So ist auch erfasst, was `ExecuteUpdate`, rohes SQL oder der zweite Kontext ändert. Die Herkunft setzt der Abgleich mit `aispa.sync_origin` in seiner Transaktion; Fremdschlüssel sind aufschiebbar, damit er Zeilen in beliebiger Reihenfolge einspielen kann (Schritt 4a).
- Beim ersten Dazuschalten wird der ganze Mandant übertragen.
- Der Abgleich liest nur Transaktionen unterhalb von `pg_snapshot_xmin` und merkt sich diese Grenze je Gegenstelle (`sync.peers`). Eine Folgenummer allein reicht nicht, weil eine Transaktion mit kleinerer Nummer später festgeschrieben werden kann (Schritt 4b).
- Der Abgleich baut auf dem Offline-Betrieb der Windows-App auf (ADR 0005, PR #63) und übernimmt dessen Regeln: Änderungen werden vorgemerkt und tragen ihre eigene Id, damit eine Wiederholung nichts doppelt anlegt; über die Belegung entscheidet die zentrale Seite; was sie ablehnt, erscheint wie heute als „beim Abgleich abgelehnt“ mit Grund in der App; lokale Daten liegen DPAPI-geschützt. Neu ist nur, dass vorgemerkt in der lokalen Datenbank statt in `offline.bin` und für alle Fachdaten statt nur für Termine. Damit ist auch der offene Punkt „Kassieren ohne Verbindung“ für Einzelplätze gelöst: Die Kasse signiert lokal und überträgt danach.
- Konflikte:
  - Stammdaten, Kunden, Dienstplan: die zuletzt gespeicherte Änderung gilt, die überschriebene steht im Protokoll.
  - Termine: über die Belegung entscheidet die zentrale Datenbank (Exclusion Constraint, ADR 0023). Kollidiert ein lokal gebuchter Termin, wird er nicht verworfen, sondern als Konflikt markiert und der Rezeption gezeigt.
  - Kassa: Jede Kasse gehört genau einer Stelle (Kassen-Id mit eigener Signatureinrichtung bzw. TSE). Belege, Kassenjournal und Tagesabschlüsse werden nur übertragen, nie überschrieben und nie an einer anderen Stelle nachsigniert. Damit bleibt die Signaturkette je Kasse lückenlos.
  - Gesundheitsdaten bleiben verschlüsselt; übertragen wird der verschlüsselte Inhalt.

### 5. Kassa-Pflichten im Einzelplatz-Betrieb

- Österreich (RKSV): Signaturkarte im Kartenleser am PC oder eine Cloud-Signatureinrichtung. Fällt die Signatur aus (z. B. kein Internet bei Cloud-Signatur), gilt der Ausfall nach RKSV: Belege mit dem Vermerk „Sicherheitseinrichtung ausgefallen“, nach 48 Stunden Meldung über FinanzOnline, Sammelbeleg nach Wiederinbetriebnahme. Startbeleg, Monats- und Jahresbeleg wie im Server-Betrieb.
- Deutschland (KassenSichV): eine lokale TSE (USB oder microSD) ist im Einzelplatz-Betrieb die Empfehlung; eine Cloud-TSE braucht Internet. Meldung der Kasse nach § 146a Abs. 4 AO wie bisher.
- Aufbewahrung: Die lokale Datenbank ist die einzige Kopie, solange keine zentrale dazugeschaltet ist. Deshalb sichert die App täglich automatisch auf ein zweites Laufwerk oder einen Netzordner und erinnert, wenn die letzte Sicherung älter als sieben Tage ist. Datenerfassungsprotokoll (AT) und DSFinV-K (DE) lassen sich jederzeit exportieren. Aufbewahrung sieben Jahre (AT, § 132 BAO) bzw. zehn Jahre (DE, § 147 AO).
- Schlüssel für Gesundheitsdaten und Datenbank-Passwort liegen mit Windows DPAPI geschützt im Benutzerprofil des Geräts, nie im Klartext.
- Nicht gebaut wird, wie immer, ein Weg, Belege an der Signatur oder am Abgleich vorbeizuführen (ADR 0010). Eine lokale Kasse, die nie abgleicht, ist zulässig; ihre Daten bleiben vollständig und prüfbar.

## Begründung

Ein Code und eine Datenbank für beide Betriebsarten: Kassa, Planer und Doppelbuchungsschutz bleiben an einer Stelle und werden nur einmal getestet. Ausgeschaltete Module statt entfernter Module halten den Vertrag stabil. Die klassische Anmeldung ist ohne Server mit fester Adresse und ohne Mail die einfachste, die jedes Team kennt. Der Abgleich über ein Änderungsprotokoll erlaubt den späteren Umstieg auf einen Server ohne Export und Import, und die feste Zuordnung jeder Kasse zu einer Stelle hält die Kassenpflichten auch beim Abgleich ein.

## Folgen und Stand

Umsetzung in Schritten, jeder als eigener PR:

1. Flag `Hosting:Standalone`, Modulschalter, `GET /api/system/modules`, Ausblenden in Web und Windows-App: PR #80.
2. Klassische Benutzerverwaltung (Einrichtung, Benutzername und Passwort, Zugänge mit Startpasswort): PR #88.
3. Windows-App startet PostgreSQL und API lokal; Team-Oberfläche über die lokale API: PR #96.
4. Änderungsprotokoll (PR #106), Abgleich mit einer dazugeschalteten PostgreSQL (PR #107), Stand und abgelehnte Einträge im Web (PR #108) und in der Windows-App samt Eintragen der zentralen Datenbank (PR #109), jede Kasse fest an ihre Stelle gebunden (PR #112), Schlüssel für Gesundheitsdaten je Stelle mit dem Schlüsselcode des Spas geteilt (ADR 0048, PR #113), Zeilen ohne eigenen Mandanten über ihre Verweise (PR #114), neuer PC verbindet sich bei der Einrichtung mit einem bestehenden Spa (PR #115), Protokoll der zentralen Seite nach einer Frist aufgeräumt (PR #116).
5. Tägliche Sicherung und Erinnerung mit Wiederherstellungsschlüssel (PR #110), Wiederherstellen in der App (PR #111), Sicherung bei geschlossener App über eine Windows-Aufgabe (PR #117). Ausfallbelege (RKSV), DEP- und DSFinV-K-Export laufen im Einzelplatz wie im Server-Betrieb, ohne eigenen Code (geprüft mit PR #112).

ADR 0005 (Offline-Stand der Windows-App) bleibt für den Server-Betrieb gültig. ADR 0007 (Passkeys) gilt weiter für den Server-Betrieb; im Einzelplatz-Betrieb ersetzt diese Entscheidung die Anmeldung.

## Quellen

- [ADR 0005](0005-offline-windows-app-am-empfang.md), [0006](0006-backend-modularer-monolith.md), [0007](0007-anmeldung-passkeys-identity.md), [0010](0010-kassa-compliance-by-design.md), [0016](0016-hosting-azure-portabel.md), [0023](0023-doppelbuchungsschutz-in-der-datenbank.md)
- RKSV § 17 (Ausfall der Signatur- bzw. Siegelerstellungseinheit), § 132 BAO, § 146a und § 147 AO

## Nachtrag Runde R05, Slice 92 (29.10.2026): Webmodule nach ADR 0084

Mit [ADR 0084](0084-alle-masken-nativ-web-fuer-dashboard-und-handy.md) gibt es keine Web-Team-Oberfläche mehr. Zuschaltbare Webmodule sind danach das Dashboard für den Pausenraum, die Handy-App für Mitarbeitende (aus „Mein Tag“) und die Handy-App für Kunden (aus dem Kundenbereich mit der Online-Buchung). Der Einzelplatz läuft ohne sie. Die Hilfe ist kein zuschaltbares Modul: die Windows-App zeigt sie immer im eingebetteten Browser.
