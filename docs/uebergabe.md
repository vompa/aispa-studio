# Übergabe an Claude Code

Stand: 20.10.2026, 04:30 UTC, `main` nach PR #170. Hier zuerst lesen. Seit dem 20.10.2026 macht der Auftraggeber alle weiteren Punkte selbst in Claude Code; im Claude-Projekt arbeitet kein Thread mehr ([ADR 0046](entscheidungen/0046-uebergabe-an-claude-code-repository-als-einzige-quelle.md), Nachtrag vom 20.10.). Diese Seite sagt, wo was liegt, wie man lokal startet, was seit der ersten Übergabe vom 18.10. dazukam, was außerhalb von `main` liegt, was als Nächstes ansteht und nach welchen Regeln gearbeitet wird. Die wörtlichen Übergaben der Threads liegen unter [uebergabe/threads/](uebergabe/threads/).

## 1. Wo ist was

| Frage | Antwort |
| --- | --- |
| Was soll das Produkt sein? | [produkt/anforderungen.md](produkt/anforderungen.md): Ziel, Zielgruppe, Anforderungen des Auftraggebers mit Zitaten, Leitlinien, Rollen, Abgrenzung |
| Wie soll der Kalender an der Rezeption arbeiten? | [produkt/kalender-rezeption.md](produkt/kalender-rezeption.md): Konzept mit Wünschen, Symbolen, Tastenkürzeln und Umsetzungsschritten (alle gebaut, Hotelgast bewusst weggelassen) |
| Welche Rechner braucht ein Spa? | [produkt/systemanforderungen.md](produkt/systemanforderungen.md): schlank, Einzelplatz, lokale KI, Docker, Kassa-Geräte, mit Messprotokoll |
| Warum wurde etwas so entschieden? | [entscheidungen/](entscheidungen/README.md): 59 Entscheidungen im ADR-Stil; Detailentscheidungen je PR im [PR-Protokoll](umsetzung/pr-protokoll.md) |
| Wie ist der Code aufgebaut? | [architektur.md](architektur.md): Projekte, Fachmodule, verbindliche Muster |
| Was ist fertig, was fehlt? | [stand-und-offene-punkte.md](stand-und-offene-punkte.md): Tabelle je PR, Echtbetrieb, offene Funktionen je Modul mit Reihenfolge |
| Wie bedient und konfiguriert man es? | [README](../README.md) im Wurzelverzeichnis; für Anwender das Handbuch in der App (`web/src/help/content/`, Format in `web/src/help/README.md`) |
| Welche Testdaten und Testkonten gibt es? | [testdaten.md](testdaten.md) |
| Welche Konzepte liegen zugrunde? | [konzepte/](README.md#konzepte): Konkurrenzanalyse, Entscheidungsdokument, Technik-Stack, Reporting, Buchungsplattformen, Lizenzmodell, Hosting-Kosten, ergänzende Themen, Buchungsprobleme, Partneranfragen (Entwurf, nicht versenden) |
| Wie sieht es aus? | [bilder/](bilder/) (Bildschirmfotos je Bereich) und die erzeugten Handbuch-Bilder unter `web/src/help/images/app/` (Windows-App) und `images/web/` (Web-Oberfläche) |
| Was wollten die Threads als Nächstes tun? | [uebergabe/threads/](uebergabe/threads/): sechs Übergaben vom 18.10. und [windows-app-2.md](uebergabe/threads/windows-app-2.md) vom 20.10.; was davon erledigt ist, steht im [Stand](stand-und-offene-punkte.md) |
| Wie wird mit Branches, PRs, CI und Merge gearbeitet? | [arbeitsweise.md](arbeitsweise.md) |
| Regeln für Claude Code | `CLAUDE.md` |

Der Projektchat und die Dateien unter `/mnt/project-files` des Claude-Projekts sind nicht mehr nötig; alles daraus ist hier übernommen.

## 2. Lokal starten und prüfen

Voraussetzungen: .NET SDK 10 (`global.json`), Node.js 22, Docker Desktop (läuft). Ausführlich in [Entwicklung](development.md#lokal-starten).

**Mit Aspire (Entwicklung, Visual Studio oder Rider):**

```bash
cd web && npm install && cd ..
dotnet run --project src/AispaStudio.AppHost
```

- Startprojekt ist `AispaStudio.AppHost`, nie `AispaStudio.Api` allein (die API bricht ohne `ConnectionStrings:aispadb` mit einem deutschen Hinweis ab, #144).
- Aspire startet PostgreSQL und Mailpit als Container, dazu API und Web. Das **Aspire-Dashboard** öffnet sich im Browser; dort stehen die Adressen. Die **Web-Oberfläche** öffnet man über den Link bei der Ressource `web` (Vite), die **API** läuft auf http://localhost:5086, Mailpit über den Link bei `mailpit`.
- Die **Windows-App** startet im Dashboard über die Ressource `windows-app` (Starten-Knopf). So gestartet ist sie mit dieser API verbunden und kennt die Adresse von Vite für die Hilfe (#145). Aus Visual Studio gestartet, fragt sie die API nach der Adresse (`/api/system/team-web`). Die Verwaltung ist nativ (Slice 103: keine eingebettete Verwaltung und keine Web-Team-Oberfläche mehr).
- Die leere Datenbank bekommt beim ersten Start das Beispiel-Spa (rund eine halbe Minute, im Hintergrund). Den Stand zeigt das Dashboard an der Ressource `api` (Health-Check `startup`: „Beispiel-Spa wird eingerichtet (seit N s)“); `web` und `windows-app` warten darauf. Die Windows-App zeigt ihn auf der Anmeldung und lädt die Testkonten danach selbst. Steht dort „Beispieldaten unvollständig“ (ein früheres Einspielen wurde abgebrochen), das Volume `aispa-postgres-data` löschen bzw. `docker compose down -v`; gelöscht wird nie automatisch. Im Web „Als Testkonto anmelden“, in der App ein Testkonto wählen ([testdaten.md](testdaten.md)).
- Datenbank im Dashboard sichern und zurücksetzen (nur Entwicklung): an der Ressource `postgres` die Befehle „Sicherung erstellen“ (Datei in `.aispa-backups/`, nicht im Git), „Sicherung einspielen …“ und „Beispiel-Spa neu einspielen …“; Code in `src/AispaStudio.AppHost/DatabaseCommands.cs`, Beschreibung in [development.md](development.md).
- Lokale KI dazu: `dotnet run --project src/AispaStudio.AppHost -- --LocalAi:Enabled=true`, Modell unter Stammdaten › KI laden ([ADR 0057](entscheidungen/0057-lokale-ki-optional-mit-ollama.md)).
- Claude für Terminannahme, Assistent und Übersetzungen: `dotnet user-secrets set "Ai:Claude:ApiKey" "<schlüssel>" --project src/AispaStudio.Api`. Ohne Schlüssel läuft alles regelbasiert oder mit der lokalen KI.

**Ohne .NET und Node:** `docker compose up -d --build` startet Datenbank, API (http://localhost:5086), Web (http://localhost:5173, Anmelden `/anmelden/`, Online-Buchung `/buchen/`, Mein Tag `/mein-tag/`) und Mailpit (http://localhost:8025). Lokale KI mit dem Profil `ki-lokal`. Von vorn: `docker compose down -v`.

**Windows-App als Download:** nach jedem grünen `main` als ZIP im Release-Tag `windows-app`; verbindet sich ohne Einstellung mit http://localhost:5086. Einzelplatz ohne Server: App mit `--standalone` bzw. Einrichtung beim ersten Start ([ADR 0041](entscheidungen/0041-einzelplatz-standalone-und-zuschaltbare-webmodule.md)).

**Prüfen wie die CI (vor jedem PR):**

```bash
dotnet format AispaStudio.slnx --verify-no-changes
dotnet build AispaStudio.slnx -c Release
dotnet test --solution AispaStudio.slnx -c Release
cd web && npm run gen:api && npm run lint && npm run typecheck && npm test && npm run build && npm run check:bundle
```

- Tests brauchen Docker (Testcontainers) oder `AISPA_TEST_POSTGRES="Host=localhost;Username=postgres;Password=…"`. Zuletzt (#170) 757 .NET- und 188 Web-Tests.
- Bildschirmfotos der Windows-App ohne Windows: `AISPA_SCREENSHOTS=$PWD/bilder dotnet test --project tests/AispaStudio.Desktop.Tests`. Handbuch-Bilder: `scripts/help-images.sh` und `web/scripts/help-images.mjs`.

## 3. Was auf `main` ist

`main` steht auf `5a3ec6c` (PR #170). Gemergt sind alle PRs bis #170 außer #91 (offen), #151 (Dependabot, offen) und den ohne Merge geschlossenen (#2, #44). Die Tabelle „Gebaut“ in [stand-und-offene-punkte.md](stand-und-offene-punkte.md#gebaut-prototyp) führt jeden Bereich mit PR-Nummer.

### 3.1 Seit der ersten Übergabe (18.10., PR #93) dazugekommen

| Bereich | Was | PRs |
| --- | --- | --- |
| Einzelplatz-Betrieb | Schritte 1 bis 5 von [ADR 0041](entscheidungen/0041-einzelplatz-standalone-und-zuschaltbare-webmodule.md) fertig: klassische Benutzerverwaltung, App startet PostgreSQL und API selbst, Änderungsprotokoll und Abgleich mit zentraler PostgreSQL, Anzeige des Abgleichs, Sicherung (auch bei geschlossener App) und Wiederherstellen, Kassen je Stelle, Gesundheitsschlüssel je Stelle, Mehrplatz im eigenen Netz, Passwort vergessen ohne Leitung | #88, #96, #106 bis #119, #123 |
| Abo und Firmendaten | Module je Abo-Stufe ([ADR 0050](entscheidungen/0050-abo-stufe-schaltet-webmodule-frei.md)), Firmendaten des Spas mit Impressum ([ADR 0052](entscheidungen/0052-firmendaten-des-spas-zentral.md)), Zahlungskonto je Spa mit Stripe Connect oder eigenen Schlüsseln ([ADR 0054](entscheidungen/0054-zahlungskonto-des-spas-stripe-connect-und-eigene-schluessel.md)) | #121, #122, #127 |
| Windows-App als einziger Client zum Start | Verwaltung mit allen Verwaltungsseiten eingebettet ([ADR 0053](entscheidungen/0053-verwaltung-in-der-windows-app-eingebettet.md)), Web im Aussehen der App, optisch aufgewertet, Konto-Menü, Start über Aspire | #120, #124, #125, #145 |
| KI und Hilfe | Leiste „KI und Hilfe“ rechts (Strg+Leertaste) in App und Web, verbunden mit dem Handbuch, Vorschläge in der App als Knöpfe (buchen, Kunde öffnen, im Kalender zeigen); Hilfe zu jeder Einstellung ([ADR 0055](entscheidungen/0055-hilfe-zu-jeder-einstellung-im-repository.md)); Handbuch mit 125 Abläufen und Bildern ([ADR 0056](entscheidungen/0056-handbuch-mit-ablaeufen-und-bildern-als-grundlage-der-ki-hilfe.md)) und drei geschlossene Lücken daraus; lokale KI mit Ollama, Systemanforderungen ([ADR 0057](entscheidungen/0057-lokale-ki-optional-mit-ollama.md)) | #135, #147, #149, #150, #152, #155, #170 |
| Kalender aus Sicht der Rezeption | alle Schritte des [Rezeptions-Konzepts](produkt/kalender-rezeption.md) in der Windows-App: ganze Breite, Zoom, Tastatur, Suche, Rechtsklick, Kennzeichen, freie Zeit suchen (Strg+N), Tagesleiste, Pausen, Krankmeldung mit Ausfall-Assistent, KI-Anfrage (Strg+K), Vorlieben, Serientermine, Spa-Tag als Baukasten, Pakete, Kundenkarte mit Kennzahlen, Sternen und Kennzeichen, Sprache und Quelle; aus der Recherche zu Buchungsproblemen: „Kommt später“ (Verkürzen seit 23.10.2026 entfernt), Gruppen mit „Name folgt“, Wunsch ♀/♂, „Anamnese prüfen“, Zusagen im Kalender | #130, #132 bis #134, #136 bis #138, #143, #146, #148, #154, #156 bis #160, #162 bis #164, #166, #168 |
| Posteingang | übersetzt Nachrichten und überarbeitet Antworten mit KI, „Neu vorschlagen“, Antworten auf Vorschläge verknüpft | #140, #142 |
| Kassa | Kundenrabatt, Rabatte in Prozent mit Grenze, Barzahlungsrabatt und Freigabe per Leitungs-PIN, DATEV-BU-Schlüssel je Land | #128, #129, #131, #139 |
| Buchungsprobleme mit Gästen | Anzahlung und Vorauszahlung ([ADR 0047](entscheidungen/0047-anzahlung-und-vorauszahlung.md)), Zusage per Link, Gäste verschieben selbst, Hinweis je Behandlung, Absage per Nachricht, Schutz vor No-Shows, Gutschein beim Online-Buchen (Recherche) | #161, #165, #167 |
| Arbeitsweise | [arbeitsweise.md](arbeitsweise.md), Vorgehen beim Programmieren nach den Karpathy-Richtlinien in `CLAUDE.md`, Regeln für Hilfetexte und Handbuch | #93, #141, #153 |

### 3.2 Module im Überblick

| Modul | Code | Stand |
| --- | --- | --- |
| Kalender, Stammdaten, Rollen, Dienstplan, Zeiterfassung, Mandanten | `Scheduling`, `Roster`, `Access`, `Auth`, `Tenancy`, `TimeTracking`, `web/src/calendar` | fertig für den Prototyp; Rezeptions-Kalender vor allem in der Windows-App |
| Kassa, Buchhaltung, Lohn, Kosten, Lager | `Pos`, `Accounting`, `Payroll`, `Costs`, `Stock` | AT mit Test-Signatur, DE mit Test-TSE und DSFinV-K, CH-Rundung; Exporte DATEV, BMD, RZL, Abacus, bexio; Pakete, Rabatte, Anzahlungen |
| Online-Buchung und Kundenbereich | `Online`, `web/src/booking` (eigenes Bundle `/buchen/`, unter 100 kB gzip) | Buchung, Konten ohne Passwort, Anamnese, Warteliste, Gutscheine, Karte, Anzahlung, Zusage und Verschieben per Link, No-Show-Schutz, vier Sprachen |
| KI-Terminannahme, Kanäle, Assistent | `MailIntake`, `Assistant`, `Reminders`, `Retention`, `Mail`, `Live` | Mail, SMS, WhatsApp, Anrufe, Treatwell-Mails, Übersetzen, Absagen; Claude oder lokale KI |
| Hilfe und Handbuch | `src/AispaStudio.Help`, `web/src/help`, `web/src/companion` | Handbuch, Hilfe zu Einstellungen, Leiste „KI und Hilfe“ |
| Berichte, Mein Tag, Benchmark | `Reporting`, `web/src/myday` | Behandlungskarte, Tagesstart, Management, Pflichtberichte, Handy-App |
| Windows-App | `src/AispaStudio.Desktop` | einziger Client zum Start ([ADR 0059](entscheidungen/0059-zum-start-nur-die-windows-app.md)): Kalender, Kunden, Kassa, Posteingang, Geräte und Verwaltung nativ; offline lesen und vormerken; Einzelplatz und Mehrplatz |

## 4. Außerhalb von `main`

Stand 08.11.2026: Seit diesem Tag ist dieses öffentliche Repository das einzige. Das frühere, nicht öffentliche Repository mit der ganzen Historie ist gelöscht. Alle PR-Nummern vor dem 08.11.2026 (in dieser Datei, im [PR-Protokoll](umsetzung/pr-protokoll.md) und in den ADRs) beziehen sich auf dieses frühere Repository. Ihr Inhalt steht im PR-Protokoll, die Historie hat der Auftraggeber lokal gesichert.

Offen sind nur die Dependabot-PRs dieses Repositorys. Halbfertige Stände außerhalb von `main` gibt es nicht.

## 5. Offene Arbeit, in vorgeschlagener Reihenfolge

**Festgelegt vom Auftraggeber am 20.10.2026** nach dem Vergleich mit der Konkurrenz: nach dem laufenden Kalender-Paket (Eingabefenster mit Umschaltleiste, feste Kopfzeile, Hover-Karte, Anrede/Rufname/Titel mit Postanschrift; seit 20.10.2026 auf `dev`, [ADR 0064](entscheidungen/0064-namensregeln-gueltiger-name-rufname-titel-anrede.md)) zuerst die **Startfähigkeit der Kassa** (echte Signatureinrichtung, Terminal, Trinkgeld, Netzausfall; Steuer- und Rechtsfragen parallel), dann **Daten** (Import, Dubletten zusammenführen, DSGVO-Auskunft und Anonymisierung), **Team** (Zeiterfassung am Empfangsgerät, Urlaubskonto), **KI-gestützte Lagerwirtschaft** (Vorschlag, noch nicht vom Auftraggeber festgelegt: nach den Punkten Material je Vorbereitungsschritt und Extras je Buchung; zuerst Bedarf aus Terminen, Lieferant mit Lieferzeit, Bestellvorschlag mit Begründung und Versand nach Freigabe, danach Foto-Wareneingang und Inventur am Gerät), **Kalender-Lücken** (Einwirkzeit, Wochenansicht, Mindestlücke, Treatwell-Hinweis), **Kommunikation** (Einwilligung beim Online-Buchen, Erinnerungs-Obergrenze, dann Marketing). Die Liste unten bleibt als Detail je Modul.

Je Modul ausführlich in [stand-und-offene-punkte.md](stand-und-offene-punkte.md#offene-funktionen-je-modul). Die Reihenfolge folgt den letzten Vorgaben des Auftraggebers: zum Start nur die Windows-App, alle Verwaltung dort voll nutzbar, auch im Einzelplatz ([ADR 0059](entscheidungen/0059-zum-start-nur-die-windows-app.md)); Halbfertiges zuerst.

**Runde R05 (29.10.2026, [ADR 0084](entscheidungen/0084-alle-masken-nativ-web-fuer-dashboard-und-handy.md)):** Alle Masken werden nativ in der Windows-App gebaut, das Web liefert nur Dashboard, Handy-Apps und Hilfe; dazu Team mit Personenkarte und HR, besseres Lager. Slices und Reihenfolge in `work/rounds/R05-native-masks-web-apps.md`, Zuordnung der Web-Seiten im [Masken-Inventar](umsetzung/masken-inventar.md). Die Liste unten bleibt gültig, wo sie die Web-Verwaltung nicht betrifft.

1. **Dependabot-PRs abarbeiten** (Abschnitt 4).
2. **Prototyp unter Windows ausprobieren** (nächster Schritt des Auftraggebers laut [ADR 0019](entscheidungen/0019-autonomes-arbeiten-bis-zum-prototyp.md)): Hilfe mit WebView2, Einzelplatz, Mehrplatz im Netz, Speicher der App, ZVT/OPI an einem echten Gerät. Bisher nur unter Linux und in Tests geprüft.
3. **Windows-App** ([windows-app-2.md](uebergabe/threads/windows-app-2.md)): neuen Kunden aus der Leiste „KI und Hilfe“ anlegen; Handbuch-Bild `web-calendar-assistent.png` neu erzeugen; Lotus-Knopf verdeckt auf manchen Seiten die rechte untere Ecke; Verwaltung über Aspire beim Auftraggeber prüfen (Ursache der fehlenden Weiterleitung API → Vite ungeklärt, #145 umgeht sie); Kassa ohne Verbindung mit Ersatzbelegen nach RKSV; Anzahlung anzeigen; Pakete in der Web-Kassa.
4. **Kalender:** Ausfall-Assistent zu Ende denken (Gäste ohne Ersatz per SMS/WhatsApp, andere Tage, Paar und Spa-Tag verlegen), Empfangsgeräte verwalten, Lückenketten; aus den Rezeptions-Schritten: „Kommt später“ durch den Gast selbst, Sauna und Ausfallgebühr für Gäste ohne Namen, Wunsch ♀/♂ online, Anamnese-Hinweis bei Paar, Spa-Tag und in „Mein Tag“, automatische Vorschläge für Kennzeichen.
5. **Kundenbereich:** Erinnerung vor Fristablauf, Anzahlung per Link, Links per SMS, Bestätigung der E-Mail vor einer Gastbuchung, Gutschein mit Anzahlung verrechnen, Absage eines späteren Termins, Teilerstattung.
6. **KI-Terminannahme:** Wissensbasis des Spas, Einwilligung beim Online-Buchen, Ansage je Spa, lokale KI mit echten Mails prüfen und Schalter in der App für den Einzelplatz.
7. **Kassa und Lager:** wackeligen Test `CardGuaranteeTests…Frist_freigegeben` robust machen, Schweizer UID-Register, Preisregeln für Eintritte und Artikel, Prüfprogramm der Finanzverwaltung, Rückverfolgung exportieren.
8. **Echtbetrieb vorbereiten** ([Stand, Abschnitt Echtbetrieb](stand-und-offene-punkte.md#für-den-echtbetrieb-nötig-echte-dienste-und-verträge)): Signatureinrichtung, TSE, Terminal, Stripe-Plattform, AV-Verträge, Geheimnisspeicher, Steuer- und Rechtsprüfung, Azure (der Rollout ist ein eigener Schritt, kostet und braucht Freigabe des Auftraggebers).

## 6. Bekannte Fehler und Fallen

- `CardGuaranteeTests.Online_Termin_ohne_Karte_wird_nach_der_Frist_freigegeben` schlug einmal im Gesamtlauf fehl und lief dann grün. Am 08.11.2026 nicht reproduziert: 15 Läufe der Klasse und 3 Läufe parallel mit `OnlineBookingTests` und `DepositTests`, alle grün; der Test nutzt feste Daten (2027), eine Abhängigkeit von der Uhrzeit ist im Code nicht zu sehen. Ohne Fehlerprotokoll bleibt er unverändert; schlägt er wieder fehl, die Meldung aus dem CI-Log festhalten.
- Das Treatwell-Mailformat ist angenommen, nicht mit echten Mails geprüft. ZVT und OPI nur gegen nachgebaute Terminals. Lokale KI nur gegen einen echten Ollama-Container ohne geladenes Modell geprüft.
- Ohne Verbindung bucht die Leiste „KI und Hilfe“ bewusst nicht; vorgemerkte Änderungen, die nach dem Abmelden gesendet werden, tragen im Änderungsprotokoll die Person, die beim Senden angemeldet ist.
- Parallel entstandene PRs kollidieren fast immer in `docs/umsetzung/pr-protokoll.md` und `docs/stand-und-offene-punkte.md`: beim Einmischen beide Seiten behalten. ADR-Nummern vor dem Merge prüfen (0042 und 0056 wurden doppelt angesetzt). Migrationen nach dem Einmischen auf dem Snapshot von `main` neu erzeugen.
- Squash-Merges lassen gemergte Branches in `git log main..branch` als „voraus“ erscheinen; ob etwas auf `main` ist, über den Inhalt (PR-Protokoll, Code) prüfen.
- Vitest zeigt manchmal veraltete Ergebnisse: `web/node_modules/.vitest-cache` und `.vite` löschen. `scripts/help-images-shrink.py` verkleinert alle Bilder; nur die eigenen committen.
- Strg+K (KI-Anfrage), Strg+N (freie Zeit) und F1 (Tastenhilfe) sind im Kalender der App belegt, Strg+Leertaste öffnet die Leiste.
- Im Web-Projekt ist kein Formatierer festgelegt; PR #88 hat einige Dateien mit Prettier umformatiert.
- Postgres im Cloud-Container stoppt zwischen Läufen (`service postgresql start`); `dotnet format` bemängelt die Reihenfolge der `using`-Zeilen; Records mit Listen sind nach JSON-Rundreise nicht gleich (in Tests per Id vergleichen).
- Weitere Fallen (Wolverine-Handler, `ValueGeneratedNever`, `SignInManager`, OpenAPI-Erzeugung bei laufender Datenbank, `sed` über `bin/`, Demo-Daten nur in leere Datenbank) stehen in `CLAUDE.md`.

## 7. Überlegungen für später

Gesammelt aus den Übergaben, nicht entschieden und nicht eingeplant:

- **Kalender:** Chipkarte oder NFC für den Schnellwechsel; Entfernung oder bevorzugter Ausweichstandort; Lückenbewertung in Online-Buchung und KI; SSO; Zeiterfassung am Tablet per PIN; KI-Vorschläge im Dienstplan.
- **Kassa:** Rückruf-Mail an betroffene Gäste aus der Rückverfolgung (nur nach Freigabe, über die Queue); Bestellung direkt an Lieferanten, Wareneingang per Lieferschein-Foto; Terminalbelege; Preis nach Auslastung; Datenmigration aus Altsystemen.
- **Kundenbereich:** Spa-Tag, Paar und Eintritte online buchbar; Regeln für Ausfallgebühr und Anzahlung je Kundengruppe oder No-Show-Risiko; Wissensbasis für Fragen.
- **KI und Kanäle:** Rückruf statt SMS für Festnetz; WhatsApp-Vorlagen für Werbung mit eigener Einwilligung; Betreiber-Konto statt API-Schlüssel; offizielle Treatwell-Schnittstelle, sobald der Auftraggeber die Partneranfrage freigibt (Entwurf, nicht versenden).
- **Hilfe:** Handbuch in weiteren Sprachen, dunkle Bilder, Markierungen in Bildern.
- **Einzelplatz:** Haupt-PC im Netz automatisch finden; Passkeys zusätzlich nach Umstieg auf einen Server.
- **Betrieb:** Desktop-Job der CI nur bei Änderungen an der App, um Windows-Minuten zu sparen; Lesemodelle, sobald Messungen es verlangen; medizinische Spas erst nach Version 1 ([ADR 0018](entscheidungen/0018-medizinische-spas-nicht-in-v1.md)).

## 8. Arbeitsregeln des Auftraggebers

Ausführlich mit Ablauf, Prüfkette, PR-Text und PR-Protokoll: [arbeitsweise.md](arbeitsweise.md).

Aus dem Projektchat, verbindlich für jede Weiterarbeit (Quellen in den ADRs):

- **Sprache:** Alles, was der Auftraggeber liest, auf Deutsch (Oberfläche, Fehlermeldungen, PR-Titel und -Texte, Commits, Doku); Bezeichner im Code englisch.
- **Branch und Pull Request auf `main`, Merge erst nach grüner CI** ([ADR 0019](entscheidungen/0019-autonomes-arbeiten-bis-zum-prototyp.md)). GitHub Actions ist für dieses öffentliche Repository kostenlos; ein Lauf dauert etwa 10 bis 15 Minuten. Nur wenn die Jobs wirklich nicht starten, nach voller lokaler Prüfkette mergen und „CI ausgefallen, lokal geprüft“ vermerken ([ADR 0044](entscheidungen/0044-pull-requests-bleiben-merge-auch-bei-ci-ausfall.md)).
- **Detailentscheidungen** trifft Claude selbst nach den bisherigen Festlegungen und hält sie im PR-Text fest; danach Abschnitt im [PR-Protokoll](umsetzung/pr-protokoll.md) und [Stand](stand-und-offene-punkte.md) nachziehen. Grundsatzentscheidungen als neuer ADR nach der [Vorlage](entscheidungen/vorlage.md); nächste freie Nummer: 0068 (0063 ist für die offene Schnittstelle für Personal und Kunden reserviert).
- **Hilfe und Handbuch im selben PR:** neue Einstellungen mit Hilfetext und Fragezeichen ([ADR 0055](entscheidungen/0055-hilfe-zu-jeder-einstellung-im-repository.md)), neue oder geänderte Abläufe mit Handbuch und neu erzeugten Bildern ([ADR 0056](entscheidungen/0056-handbuch-mit-ablaeufen-und-bildern-als-grundlage-der-ki-hilfe.md)).
- **Nicht ohne den Auftraggeber:** Produktion, Löschen, Nachrichten an Dritte (Partneranfragen an Treatwell und Google ruhen, [ADR 0014](entscheidungen/0014-buchungsplattformen-ohne-partnervertrag.md)).
- **Nie bauen:** Funktionen, die Umsätze an Kasse oder Buchhaltung vorbeiführen. Getrennte Bereiche nur über eigene, versteuerte Erlöskonten ([ADR 0010](entscheidungen/0010-kassa-compliance-by-design.md)).
- **Produktrahmen:** eigenständige Day-Spas bis 50 Mitarbeitende in AT, DE, CH, Österreich zuerst ([ADR 0001](entscheidungen/0001-zielgruppe-day-spas-dach.md), [0011](entscheidungen/0011-erster-markt-oesterreich.md)); kein Hotelgast mit Zimmerbuchung ([ADR 0058](entscheidungen/0058-kein-hotelgast-mit-zimmerbuchung.md)); zum Start nur die Windows-App, alle Verwaltung dort voll nutzbar, auch im Einzelplatz ([ADR 0059](entscheidungen/0059-zum-start-nur-die-windows-app.md)); Kalender als Kern; alle Webmodule optional ([ADR 0041](entscheidungen/0041-einzelplatz-standalone-und-zuschaltbare-webmodule.md)); medizinische Spas vorgesehen, aber nicht in Version 1; Preise 99, 199, 279 Euro je Standort und Monat ([ADR 0015](entscheidungen/0015-lizenzmodell-abo-pro-standort.md)).
- **KI:** schlägt vor, der Mensch bestätigt; rechnet nie Pflichtzahlen; gibt sich nach außen immer zu erkennen; ohne Schlüssel regelbasiert oder lokal ([ADR 0008](entscheidungen/0008-ki-schlaegt-vor-mensch-bestaetigt.md), [ADR 0057](entscheidungen/0057-lokale-ki-optional-mit-ollama.md)).
- **Keine Geheimnisse im Repository;** Schlüssel über User-Secrets, `.env` oder Geheimnisspeicher.
- **Fehlendes selbst mitdenken:** „All das, was ich noch nicht angegeben habe, eigenständig die wichtigsten Punkte berücksichtigen“ ([ADR 0020](entscheidungen/0020-ergaenzende-themen-uebernommen.md)).
