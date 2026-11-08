# 0049. Mehrplatz im eigenen Netz: HTTPS mit eigenem Zertifikat und Verbindungscode

- **Status:** angenommen
- **Datum:** 19.10.2026
- **Entschieden von:** Auftraggeber (Wahl „Mehrplatz im Netz“ als nächster Punkt), Ausgestaltung Claude (autonom nach ADR 0019)

## Kontext

[ADR 0041](0041-einzelplatz-standalone-und-zuschaltbare-webmodule.md) hält fest: „Mehrplatz im eigenen Netz bleibt möglich: dieselbe Installation auf einem PC als Server freigeben, die anderen Windows-Apps verbinden sich wie heute mit einem Server.“ Bisher lauschen API und Datenbank im Einzelplatz nur an 127.0.0.1. Ein Spa mit Empfang, zweiter Kassa und Behandlungsräumen braucht aber mehrere Arbeitsplätze auf denselben Daten, ohne einen Server zu betreiben.

Im Netz eines Spas hängen auch Gäste-WLAN, Kartenterminals und fremde Geräte. Benutzernamen, Passwörter und Gesundheitsdaten dürfen dort nicht unverschlüsselt laufen, und niemand im Netz darf sich als Haupt-PC ausgeben können. Eine öffentliche Zertifizierungsstelle stellt für „empfang-pc“ oder 192.168.1.20 kein Zertifikat aus.

## Optionen

| Option | Bewertung |
| --- | --- |
| API im Netz über HTTP | einfach, aber Passwörter und Anamnesen im Klartext im WLAN; scheidet aus |
| Datenbank im Netz freigeben, jede App startet ihre eigene API | Datenbank-Passwort auf jedem PC, PostgreSQL mit TLS einrichten, mehrere APIs auf einer Datenbank (Wolverine, Abgleich doppelt); unnötig kompliziert |
| Eigene Zertifizierungsstelle, die in Windows als vertrauenswürdig eingetragen wird | braucht Administratorrechte auf jedem PC und gilt dann für alle Programme; zu viel Vertrauen für ein Spa-Programm |
| HTTPS mit einem Zertifikat, das der Haupt-PC selbst ausstellt; die anderen PCs prüfen es über einen Verbindungscode | gewählt: verschlüsselt, ohne Administratorrechte, ohne Internet, nur AISPA Studio vertraut dem Zertifikat |

## Entscheidung

- Unter „Im Netz“ gibt, wer den Standort verwalten darf, den Einzelplatz frei. Die App erzeugt dabei einmal ein Zertifikat (ECDSA P-256, zehn Jahre, `netz.pfx` im Datenordner, Passwort DPAPI-geschützt in `zugang.bin`) und startet die API mit einem zweiten Endpunkt `https://*:55443` (Port wird gemerkt; ist er belegt, ein freier). Lokal bleibt `http://127.0.0.1:<port>` wie bisher. Die Datenbank bleibt nur an 127.0.0.1.
- Der **Verbindungscode** sind die ersten 20 Hex-Stellen (80 Bit) des SHA-256-Fingerabdrucks des Zertifikats, in Vierergruppen. Die Seite zeigt ihn mit den Adressen des PCs (Name und IPv4-Adressen).
- Am anderen PC trägt man auf dem Startbildschirm „Mit Haupt-PC im Netz verbinden“ Adresse und Code ein. Die App prüft die Verbindung, merkt sich beides in `einstellungen.json` und startet neu. Sie nimmt dann nur noch das Zertifikat mit genau diesem Code an, für alle Aufrufe und für die Live-Verbindung (SignalR). Name, Aussteller und Ablauf spielen keine Rolle.
- Anmeldung an den anderen PCs wie am Haupt-PC mit Benutzername und Passwort (ADR 0041, Abschnitt 3). Die Einrichtung des Spas geht nur am Haupt-PC selbst (`POST /api/auth/standalone/setup` lehnt Anfragen über das Netz mit 403 ab).
- Zurücknehmen schaltet den Endpunkt im Netz ab; Zertifikat und Port bleiben, damit die anderen PCs beim erneuten Freigeben nichts neu eintragen müssen.

## Begründung

Alles läuft über dieselbe API wie bisher, also gelten Rechte, Belegungslogik und Kassa unverändert, und es gibt nur eine Datenbank mit einem Abgleich. Das Prüfen über den Code ist dasselbe Prinzip wie beim Koppeln von Bluetooth-Geräten: einmal abtippen, danach kann sich niemand dazwischenschalten. 80 Bit reichen, weil ein Angreifer ein Zertifikat mit passendem Fingerabdruck erzeugen müsste (rund 2^80 Versuche), nicht nur raten. Ohne Administratorrechte und ohne Eingriff in den Zertifikatsspeicher von Windows bleibt die Einrichtung etwas, das die Leitung selbst schafft.

## Folgen und Stand

- Umgesetzt mit PR #119: `NetworkShare`, `LocalServer.SetSharedAsync`, Seiten „Im Netz“ (`NetworkViewModel`) und „Mit Haupt-PC im Netz verbinden“ (`NetworkConnectViewModel`), Prüfung des Zertifikats in `ApiClient` und `LiveUpdates`.
- Windows fragt beim ersten Freigeben nach der Firewall; für private Netzwerke erlauben. Ein eigener Firewall-Eintrag bräuchte Administratorrechte und wird nicht angelegt.
- Der Haupt-PC muss an sein und die App offen haben. Fällt er aus, arbeiten die anderen PCs mit ihrem Offline-Stand weiter (ADR 0005), wie bei einem Server.
- Die Team-Oberfläche im Browser eines anderen PCs warnt einmal vor dem eigenen Zertifikat; der Browser kennt den Code nicht.
- Nach dem Wiederherstellen aus einer Sicherung fehlt das Zertifikat; erneutes Freigeben erzeugt ein neues mit neuem Code.
- Offen: Haupt-PC im Netz automatisch finden (statt Adresse eintippen); Zertifikat wechseln, ohne dass alle PCs neu verbinden.

## Quellen

- [ADR 0041](0041-einzelplatz-standalone-und-zuschaltbare-webmodule.md), Abschnitt 1
- [ADR 0005](0005-offline-windows-app-am-empfang.md) (Offline-Stand der Windows-App)
- PR #119
