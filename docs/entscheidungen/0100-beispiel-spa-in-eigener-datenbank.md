# 0100. Beispiel-Spa in eigener Datenbank

- **Status:** angenommen
- **Datum:** 18.11.2026
- **Entschieden von:** Auftraggeber (Anmeldung, Löschen, Schalter), Umsetzung Claude

## Kontext

Das Beispiel-Spa (`Database:SeedDemoData`) füllte bisher die Datenbank `aispadb`, und nur in der Entwicklungsumgebung. Im Einzelplatz gab es keinen Weg, die App ohne eigenes Spa auszuprobieren, und wer es im Docker-Testbetrieb benutzte, hatte danach eine Datenbank mit Beispieldaten, die sich nicht sauber leeren ließ. Die Beispielkonten hatten kein Passwort; die Anmeldung im Einzelplatz verlangt eines.

Vorgabe des Auftraggebers: Ein Beispiel-Spa in einer eigenen Datenbank `aispademo`, im Einzelplatz und am Server auf demselben Weg, wieder löschbar. Die echte Datenbank `aispadb` wird nie angefasst.

## Optionen

| Option | Bewertung |
| --- | --- |
| A: Beispieldaten in `aispadb`, Löschen per Tabellen leeren | die echte Datenbank wäre betroffen; ein Fehler träfe echte Daten |
| B: Eigene PostgreSQL-Instanz oder eigener Datenordner für das Beispiel | doppelter Aufwand im Einzelplatz (zweiter Cluster, Ports, Zugang) |
| C: Eigene Datenbank `aispademo` im selben PostgreSQL, die API schaltet mit `Demo:Enabled` um | gewählt |

## Entscheidung

- **Einstellung `Demo:Enabled=true`:** Die API schreibt `ConnectionStrings:aispadb` auf den Datenbanknamen `aispademo` um (vor DbContext, Wolverine und Schlüsselring), legt die Datenbank über die Wartungsdatenbank `postgres` an, falls sie fehlt, und behandelt `Database:SeedDemoData` als eingeschaltet (im Mehrinstanz-Betrieb nur dort, wo Hintergrunddienste laufen, also nicht in der Rolle Api). `Sync:CentralConnectionString` bleibt unverändert. Der Name steht als Konstante in `DemoSpa`.
- **Einzelplatz:** Der Startschalter `--demo` setzt `Demo__Enabled=true` für die API, nur für diesen Start. Er schaltet immer den Einzelplatz ein und wird nicht gemerkt; die gemerkte Betriebsart bleibt unverändert. Sicherung, Wiederherstellung und tägliche Sicherung arbeiten weiter nur mit `aispadb`; im Beispiel-Spa sichert die App nicht und gibt keine zentrale Datenbank an die API weiter.
- **Server:** `AISPA_DEMO=true` (Umgebung oder `.env`). Der Docker-Server zum Ausprobieren hat sie wie bisher an (`Demo__Enabled: ${AISPA_DEMO:-true}`), `Database__SeedDemoData` ist dort aus.
- **Anmeldung:** Im Beispiel-Spa haben alle Beispielkonten das Passwort `demo`, als Hash gesetzt (die Mindestlänge für selbst gewählte Passwörter gilt dafür nicht). Ohne `Demo:Enabled` (Entwicklung, Tests) bleiben die Konten ohne Passwort. Die Anmeldemaske der Windows-App nennt Konto und Passwort.
- **Status:** `GET /api/system/modules` bekommt das Feld `demo` (optional, kein Breaking Change nach ADR 0085).
- **Löschen:** `DELETE /api/system/demo` mit dem Recht „Standort verwalten“. 404 ohne Beispiel-Spa, sonst 202; nach der Antwort löscht die API die Datenbank `aispademo` (`DROP DATABASE … WITH (FORCE)` über die Wartungsdatenbank) und beendet sich. Der Name ist fest, der Endpunkt kann `aispadb` nicht löschen.
- **Windows-App:** Über allen Seiten steht die Leiste „Sie sehen Beispieldaten.“ mit **Beispieldaten löschen …**, sichtbar nur mit dem Recht „Standort verwalten“. Nach der Rückfrage (meldungen.md, Dialog) schließt sich die App im Einzelplatz und stoppt dabei API und Datenbank; am Server nennt eine Meldung, dass `AISPA_DEMO` entfernt werden muss, sonst legt der Neustart neue Beispieldaten an.

## Begründung

Eine eigene Datenbank trennt Beispiel und Betrieb hart: Löschen ist ein einzelner Befehl auf eine Datenbank mit festem Namen, und `aispadb` ist technisch unerreichbar. Derselbe PostgreSQL spart im Einzelplatz einen zweiten Cluster. Die Umschreibung der Verbindung an einer Stelle beim Start erfasst alle Verbraucher (EF, Wolverine, Schlüsselring, Migration). Ein gesetzter Passwort-Hash umgeht die Mindestlänge, ohne die Regel für echte Passwörter zu lockern.

## Folgen und Stand

- Das Feld `Demo` in `SystemModules` und der Pfad `DELETE /api/v1/system/demo` stehen in `web/openapi/aispa-api.json`.
- Code: `System/DemoSpa.cs`, `Demo/DemoAccounts.cs`, `System/SystemEndpoints.cs`, in der App `StandaloneMode`, `LocalServer`, `ShellViewModel.Demo.cs`, `BackupViewModel`, `LoginViewModel`.
- Der Datenbankbenutzer braucht das Recht, Datenbanken anzulegen und zu löschen: im Einzelplatz hat es `aispa_app` (Superuser des eigenen Clusters), in `compose.yaml` `postgres`. Bei einem Server mit eingeschränktem Konto muss die Datenbank `aispademo` vorher bestehen; das Löschen scheitert dort mit einer Meldung im Protokoll.
- Im Mehrinstanz-Betrieb beendet sich nur die Instanz, die den Aufruf bekam; die übrigen verlieren die Datenbank und müssen neu gestartet werden.
- Offen: Eine API, die schon ohne `--demo` läuft, wird beim Start mit `--demo` wiederverwendet; die Leiste erscheint dann nicht.

## Quellen

- [Betriebsarten und Module](../installation/operating-modes.md), [Einzelplatz](../installation/standalone.md), [Server](../installation/server.md), [Testdaten](../testdaten.md)
- [ADR 0041](0041-einzelplatz-standalone-und-zuschaltbare-webmodule.md), [ADR 0085](0085-api-version-in-den-pfaden.md), [ADR 0087](0087-mehrinstanz-betrieb.md)
