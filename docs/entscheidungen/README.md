# Entscheidungsprotokoll

Daten nach UTC, wie in den Pull Requests. Jede wichtige Entscheidung steht hier als eigene Datei im ADR-Stil (Architecture Decision Record): Kontext, geprüfte Optionen, Entscheidung, wer sie wann getroffen hat, Begründung und was daraus folgt.

Neue Entscheidungen bekommen die nächste freie Nummer und die Vorlage [`vorlage.md`](vorlage.md). Bestehende Entscheidungen werden nicht umgeschrieben; eine Änderung ist eine neue Entscheidung, die alte bekommt den Status „ersetzt durch NNNN“.

| Nr. | Entscheidung | Datum | Entschieden von |
| --- | --- | --- | --- |
| 0001 | [Zielgruppe: Day-Spas bis 50 Mitarbeitende in DACH](0001-zielgruppe-day-spas-dach.md) | 17.10.2026 | Auftraggeber |
| 0002 | [Kalender als Kern, selbst gebaut](0002-kalender-als-kern-eigenbau.md) | 17.10.2026 | Auftraggeber (Schwerpunkt und Eigenbau), Empfehlung Claude |
| 0003 | [Oberfläche passt sich der Rolle an](0003-rollenbasierte-oberflaechen.md) | 17.10.2026 | Auftraggeber (Anforderung), Empfehlung Claude, vom Auftraggeber angenommen |
| 0004 | [Oberflächen-Technik: Avalonia für Windows, React mit TypeScript und Vite fürs Web](0004-oberflaechen-avalonia-und-react.md) | 17.10.2026 | Auftraggeber („ja lass uns das so andenken“), Empfehlung Claude |
| 0005 | [Rezeption arbeitet mit einer offline-fähigen Windows-App](0005-offline-windows-app-am-empfang.md) | 17.10.2026 | Auftraggeber |
| 0006 | [Backend als modularer Monolith mit Queue, SignalR und PostgreSQL](0006-backend-modularer-monolith.md) | 17.10.2026 | Auftraggeber (Anforderung), Empfehlung Claude, mit dem Entscheidungsdokument angenommen |
| 0007 | [Anmeldung ohne Passwort mit ASP.NET Core Identity und Passkeys](0007-anmeldung-passkeys-identity.md) | 17.10.2026 | Auftraggeber |
| 0008 | [KI in jeder Ansicht: sie schlägt vor, der Mensch bestätigt](0008-ki-schlaegt-vor-mensch-bestaetigt.md) | 17.10.2026 | Auftraggeber (Anforderung), Grundregel Claude, mit dem Entscheidungsdokument angenommen |
| 0009 | [Automatische Terminannahme über alle Kanäle in einer Buchungslogik](0009-terminannahme-alle-kanaele.md) | 17.10.2026 | Auftraggeber (Anforderung), Empfehlung Claude |
| 0010 | [Eigene Kassa mit Compliance by Design, keine Umsatzverkürzung](0010-kassa-compliance-by-design.md) | 17.10.2026 | Auftraggeber (Anforderung und Klarstellung), Claude (Ablehnung der Umsatzverkürzung) |
| 0011 | [Erster Markt: Österreich, dann Deutschland, dann Schweiz](0011-erster-markt-oesterreich.md) | 17.10.2026 | Auftraggeber |
| 0012 | [Buchhaltungsexporte: DATEV und BMD zuerst](0012-buchhaltung-datev-und-bmd-zuerst.md) | 17.10.2026 | Auftraggeber |
| 0013 | [Gemeinsamer Kundenstamm über Standorte, Gesundheitsdaten getrennt, Anamnese auf drei Wegen](0013-standorte-kundenstamm-gesundheitsdaten.md) | 17.10.2026 | Auftraggeber |
| 0014 | [Buchungsplattformen: Treatwell über Mail und Kalender-Feed, Partneranfragen zurückgestellt](0014-buchungsplattformen-ohne-partnervertrag.md) | 17.10.2026 | Auftraggeber |
| 0015 | [Lizenzmodell: Abo pro Standort, 99, 199 und 279 Euro](0015-lizenzmodell-abo-pro-standort.md) | 17.10.2026 | Auftraggeber |
| 0016 | [Hosting: Start auf Azure, portabel gebaut](0016-hosting-azure-portabel.md) | 17.10.2026 | Empfehlung Claude, mit dem Entscheidungsdokument vom Auftraggeber angenommen |
| 0017 | [Continuous Integration ab dem ersten Commit](0017-ci-ab-tag-eins.md) | 17.10.2026 | Auftraggeber (Anforderung), Umsetzung Claude |
| 0018 | [Medizinische Spas nicht in Version 1, Architektur bleibt offen](0018-medizinische-spas-nicht-in-v1.md) | 17.10.2026 | Auftraggeber |
| 0019 | [Arbeitsweise: Claude merged nach grüner CI selbst und entscheidet Details bis zum lauffähigen Prototyp](0019-autonomes-arbeiten-bis-zum-prototyp.md) | 17.10.2026, 21:54 | Auftraggeber |
| 0020 | [Ergänzende Themen (DSGVO, AI Act, Sicherheit, Barrierefreiheit u. a.) übernommen](0020-ergaenzende-themen-uebernommen.md) | 17.10.2026 | Auftraggeber („passt alles so übernehmen“), Vorschlag Claude |
| 0021 | [Vollkosten pro Behandlung und Vergleich mit anderen Spas](0021-vollkosten-und-benchmark.md) | 17.10.2026 | Auftraggeber (Anforderung), Ausgestaltung Claude |
| 0022 | [Reporting kommt zur Person und endet in Aufgaben](0022-reporting-je-rolle.md) | 17.10.2026 | Auftraggeber (Anforderung), Konzept Claude |
| 0023 | [Doppelbuchungsschutz in der Datenbank](0023-doppelbuchungsschutz-in-der-datenbank.md) | 17.10.2026 | Claude (PR #3, vor der Autonomie-Freigabe vom Auftraggeber gemergt) |
| 0024 | [Mandantentrennung in einer gemeinsamen Datenbank](0024-mandantentrennung-eine-datenbank.md) | 17.10.2026 | Claude (autonom, siehe ADR 0019) |
| 0025 | [Dienstplan: Hinweise zur Arbeitszeit statt Sperren](0025-dienstplan-hinweise-statt-sperren.md) | 17.10.2026 | Claude (autonom, siehe ADR 0019) |
| 0026 | [Kundenkonten getrennt von den Teamkonten](0026-kundenkonten-getrennt-vom-team.md) | 17.10.2026 | Claude (autonom, siehe ADR 0019) |
| 0027 | [Windows-App meldet sich per Einmal-Code aus dem Web an](0027-windows-app-anmeldung-per-einmal-code.md) | 18.10.2026 | Claude (autonom, siehe ADR 0019) |
| 0028 | [Digitaler Euro als Zahlungsart vorbereitet, ausgeschaltet](0028-digitaler-euro-vorbereitet.md) | 18.10.2026 | Auftraggeber (Frage), Umsetzung Claude |
| 0029 | [Demo-Daten nach Wiener Day-Spas](0029-demo-daten-wiener-day-spas.md) | 18.10.2026 | Auftraggeber |
| 0030 | [Lokaler Test mit Docker Compose und Windows-App als Download](0030-lokaler-test-mit-docker.md) | 18.10.2026 | Auftraggeber (Wunsch), Umsetzung Claude |
| 0031 | [Zeiterfassung in Abschnitten, Korrekturen nur mit Grund und Protokoll](0031-zeiterfassung-abschnitte-mit-protokoll.md) | 18.10.2026 | Claude (autonom, siehe ADR 0019) |
| 0032 | [Vollständiger Testdatensatz und Testkonten ohne Passkey in der Entwicklung](0032-testdatensatz-und-testkonten.md) | 18.10.2026 | Auftraggeber (Wunsch), Umsetzung Claude |
| 0033 | [Postfach richtet das Spa ein, Nummern vergibt der Betreiber](0033-nummern-vergibt-der-betreiber.md) | 18.10.2026 | Claude (autonom, siehe ADR 0019) |
| 0034 | [Kartenterminal über die Queue, Geräte im Netz über die Windows-App](0034-kartenterminal-ueber-queue-und-empfangs-app.md) | 18.10.2026 | Claude (autonom, siehe ADR 0019) |
| 0035 | [Test-TSE für Entwicklung und Vorführung, DSFinV-K aus den Belegen](0035-test-tse-und-dsfinvk.md) | 18.10.2026 | Claude (autonom, siehe ADR 0019) |
| 0036 | [Kartenhinterlegung und Ausfallgebühr nur auf Knopfdruck und über die Kassa](0036-kartenhinterlegung-und-ausfallgebuehr.md) | 18.10.2026 | Claude (autonom, siehe ADR 0019) |
| 0037 | [Lager aus Bewegungen, Verbrauch über die Queue](0037-lager-aus-bewegungen.md) | 18.10.2026 | Claude (autonom, siehe ADR 0019) |
| 0038 | [Online-Zahlungen nur nach Storno an der Kassa erstatten](0038-rueckerstattung-nur-nach-storno.md) | 18.10.2026 | Claude (autonom, siehe ADR 0019) |
| 0039 | [Gesperrtes Spa: ansehen und exportieren ja, ändern nein](0039-spa-sperren.md) | 18.10.2026 | Claude (autonom, siehe ADR 0019) |
| 0040 | [Erinnerung an die nächste Behandlung nur mit bestätigter Einwilligung](0040-einwilligung-erinnerung-naechste-behandlung.md) | 18.10.2026 | Claude (autonom, siehe ADR 0019) |
| 0041 | [Einzelplatz-Betrieb „standalone“ mit lokaler Datenbank, Webmodule zuschaltbar, Abgleich mit dazugeschalteter PostgreSQL](0041-einzelplatz-standalone-und-zuschaltbare-webmodule.md) | 18.10.2026 | Auftraggeber (Vorgaben), Ausgestaltung Claude |
| 0042 | [Schnellwechsel per PIN nur am angemeldeten Empfangsgerät](0042-schnellwechsel-pin-nur-am-empfangsgeraet.md) | 18.10.2026 | Claude (autonom, siehe ADR 0019) |
| 0043 | [Anrufe: Ansage mit Hinweis, eine Aufnahme, Antwort per SMS](0043-anrufe-ansage-aufnahme-antwort-per-sms.md) | 18.10.2026 | Claude (autonom, siehe ADR 0019) |
| 0044 | [Pull Requests bleiben, bei CI-Ausfall Merge nach lokal grüner Prüfung](0044-pull-requests-bleiben-merge-auch-bei-ci-ausfall.md) | 18.10.2026, 12:35 | Auftraggeber (Entscheidungskarte), Vorschlag Claude |
| 0045 | [Windows-App als vollwertiger Empfangsarbeitsplatz](0045-windows-app-vollwertiger-empfangsarbeitsplatz.md) | 18.10.2026 | Auftraggeber (Rückmeldung), Ausgestaltung Claude |
| 0046 | [Weiterarbeit in Claude Code: Threads gestoppt, Repository als einzige Quelle](0046-uebergabe-an-claude-code-repository-als-einzige-quelle.md) | 18.10.2026, 21:34 | Auftraggeber |
| 0047 | [Anzahlung und Vorauszahlung mit Anzahlungsbeleg der Kassa](0047-anzahlung-und-vorauszahlung.md) | 18.10.2026 | Claude (autonom, siehe ADR 0019) |
| 0048 | [Schlüssel für Gesundheitsdaten beim Abgleich: je Stelle, mit dem Schlüsselcode des Spas geteilt](0048-schluessel-fuer-gesundheitsdaten-beim-abgleich.md) | 19.10.2026 | Claude (autonom, siehe ADR 0019) |
| 0049 | [Mehrplatz im eigenen Netz: HTTPS mit eigenem Zertifikat und Verbindungscode](0049-mehrplatz-im-eigenen-netz.md) | 19.10.2026 | Auftraggeber (Wahl des Punkts), Ausgestaltung Claude |
| 0050 | [Abo-Stufe je Spa schaltet Webmodule frei, nie den Kern](0050-abo-stufe-schaltet-webmodule-frei.md) | 19.10.2026 | Auftraggeber (Wahl „alle“), Ausgestaltung Claude |
| 0051 | [Passwort vergessen ohne Leitung: Wiederherstellungsschlüssel am Haupt-PC](0051-passwort-vergessen-mit-wiederherstellungsschluessel.md) | 19.10.2026 | Auftraggeber (Wahl „alle“), Ausgestaltung Claude |
| 0052 | [Firmendaten des Spas zentral, Standorte weichen nur als eigene Gesellschaft ab](0052-firmendaten-des-spas-zentral.md) | 19.10.2026 | Auftraggeber (Firmendaten einstellbar), Ausgestaltung Claude |
| 0053 | [Verwaltung in der Windows-App: Seiten der Team-Oberfläche eingebettet](0053-verwaltung-in-der-windows-app-eingebettet.md) | 19.10.2026 | Auftraggeber (Zustimmung zum Vorschlag), Ausgestaltung Claude |
| 0054 | [Zahlungskonto des Spas: Stripe Connect als Standard, eigene Schlüssel als Alternative](0054-zahlungskonto-des-spas-stripe-connect-und-eigene-schluessel.md) | 19.10.2026 | Auftraggeber (Auswahl „Beides“), Ausgestaltung Claude |
| 0055 | [Hilfe zu jeder Einstellung: Texte im Repository, eingebaut in Web und Windows-App](0055-hilfe-zu-jeder-einstellung-im-repository.md) | 19.10.2026 | Auftraggeber (Beschreibung zu jeder Einstellung), Ausgestaltung Claude |
| 0056 | [Handbuch mit Abläufen und Bildern als Grundlage der KI-Hilfe](0056-handbuch-mit-ablaeufen-und-bildern-als-grundlage-der-ki-hilfe.md) | 19.10.2026 | Auftraggeber (alle Abläufe sauber beschrieben, mit Bildern, auch für Admins), Ausgestaltung Claude |
| 0057 | [Lokale KI optional mit Ollama im Docker, Claude bleibt für Texte an Gäste](0057-lokale-ki-optional-mit-ollama.md) | 19.10.2026 | Auftraggeber (im Docker, optional), Ausgestaltung Claude |
| 0058 | [Kein Hotelgast mit Zimmer und Zahlung „aufs Zimmer“](0058-kein-hotelgast-mit-zimmerbuchung.md) | 20.10.2026 | Auftraggeber |
| 0059 | [Zum Start wird nur die Windows-App ausgeliefert](0059-zum-start-nur-die-windows-app.md) | 19.10.2026 | Auftraggeber |
| 0060 | [Weiterarbeit auf einem Branch `dev`, gesammelt nach `main`](0060-weiterarbeit-auf-einem-branch-dev.md) | 20.10.2026 | Auftraggeber, Claude |
| 0061 | [Kundenzuordnung eingehender Nachrichten: sicher, Vorschlag oder neu](0061-kundenzuordnung-eingehender-nachrichten.md) | 20.10.2026 | Auftraggeber (gestufte Kundenzuordnung zuerst), Ausgestaltung Claude |
| 0062 | [Absender-Regeln je Spa, Treatwell lesen mit KI und Regeln, Termin sofort blocken](0062-absender-regeln-und-treatwell-sofort-blocken.md) | 20.10.2026 | Auftraggeber, Ausgestaltung Claude |
| 0063 | frei gehalten für die offene Schnittstelle für Personal und Kunden (zu Beginn des Datenpakets) | | |
| 0064 | [Namensregeln: gültiger Name, Rufname, Titel und Anrede je Land](0064-namensregeln-gueltiger-name-rufname-titel-anrede.md) | 20.10.2026 | Auftraggeber, Ausgestaltung Claude |
| 0065 | [Raumpflege als Aufgabe, Tagesstart-Kontrolle je Raum und Abnahme durch die Standortleitung](0065-raumpflege-tagesstart-und-abnahme.md) | 21.10.2026 | Auftraggeber, Ausgestaltung Claude |
| 0066 | [Rechte Seitenleisten nach einem Muster; KI und Hilfe wirkt auf die offene Seite](0066-seitenleisten-ein-muster-und-ki-wirkt-auf-die-offene-seite.md) | 21.10.2026 | Auftraggeber, Ausgestaltung Claude |
| 0067 | [Masken und Meldungen nach Material 3 und Fluent 2](0067-masken-und-meldungen-nach-material-3-und-fluent-2.md) | 21.10.2026 | Auftraggeber, Ausgestaltung Claude |
| 0068 | [Lagerorte je Standort und neue Lager-Maske](0068-lagerorte-je-standort.md) | 21.10.2026 | Auftraggeber, Ausgestaltung Claude |
| 0069 | [Fluent 2 als Grundlage der Windows-App](0069-fluent-2-als-grundlage-der-windows-app.md) | 21.10.2026 | Auftraggeber, Ausgestaltung Claude |
| 0070 | [Rollen Manager und Backoffice, Recht „Berichte mit Beträgen“](0070-rollen-manager-und-backoffice.md) | 22.10.2026 | Auftraggeber (Runde 5, Ä37), Ausgestaltung Claude |
| 0071 | [Aktionen und Rabatte als eigener Fachbereich](0071-aktionen-und-rabatte.md) | 22.10.2026 | Auftraggeber, Ausgestaltung Claude |
| 0072 | [Der Assistent beantwortet Betriebsfragen aus den eigenen Daten](0072-assistent-beantwortet-betriebsfragen.md) | 22.10.2026 | Auftraggeber (Runde 5, Ä36), Ausgestaltung Claude |
| 0073 | [Schließzeiten je Standort und Absage der betroffenen Termine nach Rückfrage](0073-schliesszeiten-und-terminabsage.md) | 22.10.2026 | Auftraggeber (Runde 5b, Ä38; Nachträge Ä46 bis Ä49 und Ä51 bis Ä54; Nachtrag R03 Ä83, Ä89: die Rezeption trägt an Schließtagen ein), Ausgestaltung Claude |
| 0074 | [Betriebsinformationen und Kontakte als eigenes Verzeichnis, der Assistent liest sie nach Recht](0074-betriebsinformationen-und-kontakte.md) | 22.10.2026 | Auftraggeber (Runde 5b, Ä39), Ausgestaltung Claude |
| 0075 | [Kalender-Abos für Feiertage, Schulferien und Hinweise](0075-kalender-abos-fuer-feiertage-und-ferien.md) | 22.10.2026 | Auftraggeber (Runde 5b, Ä43), Ausgestaltung Claude |
| 0076 | [Raumsperren für Tage oder Zeiträume, Umbuchung der betroffenen Termine nach Rückfrage](0076-raumsperren.md) | 23.10.2026 | Auftraggeber (Runde 6, Ä65), Ausgestaltung Claude |
| 0077 | [Textregeln nach Google und Microsoft für Hilfe, Oberfläche, ADRs und Schnittstelle](0077-textregeln-nach-google-und-microsoft.md) | 23.10.2026 | Claude, autonom nach ADR 0019 (T-012) |
| 0078 | [Windows-Paket nur für win-x64, selbstständig und ohne Symbole der Natives](0078-windows-paket-win-x64.md) | 23.10.2026 | Claude, autonom (T-011) |
| 0079 | [Eine Hilfe für Windows-App und Web: Web-Modul im WebView2, Avalonia-Hilfe entfällt](0079-eine-hilfe-fuer-windows-app-und-web.md) | 23.10.2026 | Claude, autonom nach ADR 0019 (T-010) |
| 0080 | [CSP für den Kundenbereich: zuerst nur als Bericht, ohne Stripe-Quellen, Weg zum Durchsetzen](0080-csp-fuer-den-kundenbereich.md) | 26.10.2026 | Claude, autonom nach ADR 0019 (R02, Slice 47) |
| 0081 | [Wartezeit vor der Nachricht an den Gast: je Standort 0 bis 60 Minuten, Standard 10, „Jetzt senden“ und „Nicht senden“](0081-wartezeit-vor-der-nachricht-an-den-gast.md) | 26.10.2026 | Auftraggeber (Ä95), Umsetzung Claude, autonom nach ADR 0019 (R03, Slice 73) |
| 0082 | [Änderungsverlauf über den Änderungs-Trigger: Person, Kanal, alte und neue Werte, 3 Jahre, fällt mit dem Gast](0082-aenderungsverlauf-ueber-den-aenderungs-trigger.md) | 28.10.2026 | Auftraggeber (Ä184), Umsetzung Claude, autonom nach ADR 0019 (R04, Slice 90) |
| 0083 | [Vormerkung aus dem Posteingang: gebuchter Termin mit Kennzeichen, Erinnerung nach Termintag, Rezeption bestätigt oder gibt frei](0083-vormerkung-aus-dem-posteingang.md) | 29.10.2026 | Auftraggeber, Umsetzung Claude, autonom nach ADR 0019 |
| 0084 | [Alle Masken nativ in der Windows-App, Web nur für Dashboard, Mitarbeiter-App und Kunden-App; Team an einer Stelle](0084-alle-masken-nativ-web-fuer-dashboard-und-handy.md) | 29.10.2026 | Auftraggeber (Vorgabe, E1–E14 der Runde R05), Ausgestaltung Claude (R05, Slice 92) |
| 0085 | [API-Version in den Pfaden: `/api/v1`, alte Pfade bleiben als Alias, Breaking Changes nur über eine neue Version](0085-api-version-in-den-pfaden.md) | 02.11.2026 | Auftraggeber (Arbeitsanweisung, Ä283), Ausgestaltung Claude, autonom nach ADR 0019 (R06, Slice 121) |
| 0086 | [Code Guardian statt vibe-coding, volle Prüfung nur am Rundenende](0086-code-guardian-statt-vibe-coding.md) | 03.11.2026 | Auftraggeber (Vorgabe und Einzelentscheidungen), Ausgestaltung Claude |
| 0087 | [Mehrinstanz-Betrieb: Rollen, zentrale Schlüssel, eigener Migrationslauf, Backplane, auf Container Apps](0087-mehrinstanz-betrieb.md) | 06.11.2026 | Code-Teil angenommen (delegiert, 08.11.2026), Zielplattform entscheidet der Auftraggeber; Vorlage Claude nach der Arbeitsanweisung |
| 0088 | [Öffentliches Repository unter MIT-Lizenz, Konzeptstudie zur KI-nativen Entwicklung](0088-oeffentliches-repository-mit-mit-lizenz.md) | 07.11.2026 | Auftraggeber |
| 0089 | [Valkey statt Redis 8 für die Backplane des Live-Kanals](0089-valkey-statt-redis-8.md) | 08.11.2026 | Claude mit dem Council, vom Auftraggeber delegiert |
| 0090 | [Live-Tests der Windows-App mit FlaUI](0090-live-tests-mit-flaui.md) | 08.11.2026 | Auftraggeber (Probelauf Rezeption), Umsetzung Claude |
| 0091 | [Dokumente als PDF über PDFsharp mit eingebetteter Schrift](0091-dokumente-als-pdf-ueber-pdfsharp.md) | 09.11.2026 | Auftraggeber (Paket und Aufbau in der Arbeitsanweisung), Umsetzung Claude |

Detailentscheidungen einzelner Pull Requests stehen im [PR-Protokoll](../umsetzung/pr-protokoll.md).
