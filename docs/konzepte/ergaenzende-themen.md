# Ergänzende Themen SPA-Management

Stand: 17.10.2026. Ursprünglich als Claude-Dokument im Projekt erstellt, hier unverändert übernommen.

## Kurzfassung

Bis auf Kundenbindung brauchen alle fünfzehn ergänzten Themen zum Start zumindest eine Grundform, weil sie gesetzlich vorgeschrieben sind oder ohne sie kein Spa wechselt. Am meisten unterschätzt werden dabei Gesundheitsdaten in der Anamnese, die neuen EU-Pflichten für KI und Software (AI Act, Cyber Resilience Act, Barrierefreiheit) und die Übernahme von Gutscheinguthaben aus dem Altsystem.

Diese Punkte hat der Auftraggeber bisher nicht genannt. Sie ergänzen die Ergebnisse aus Konkurrenzanalyse, Technik-Stack, Reporting, Buchungsplattformen, Lizenzmodell und Hosting und wiederholen diese nicht.

**Prioritäten:**

- **P1:** muss vor dem ersten zahlenden Kunden fertig sein (Pflicht oder Kaufgrund)
- **P2:** im ersten Jahr nach Start
- **P3:** später, nach Nachfrage

Die rechtlichen Angaben stammen aus Fachwissen mit Stand Oktober 2026. Vor der Umsetzung sollten ein Datenschutzjurist und ein Steuerberater sie prüfen.

## 1. Datenschutz und DSGVO

**Priorität: P1.** Anamnesen enthalten Gesundheitsdaten (Allergien, Schwangerschaft, Hauterkrankungen, Medikamente), und die gehören nach Art. 9 DSGVO zur höchsten Schutzstufe. Das ist der wichtigste Datenschutzpunkt der ganzen Software.

**Empfehlung:**

- **Gesundheitsdaten getrennt speichern:** eigene Tabelle, zusätzlich verschlüsselt, sichtbar nur für den behandelnden Therapeuten und die Leitung. Die Rezeption sieht nur einen Hinweis wie „Kontraindikation beachten“. Jeder Zugriff wird protokolliert.
- **Digitale Einwilligung:** Der Kunde füllt Anamnesebogen und Einwilligung vorab über einen Link aus der Buchungsbestätigung am Handy aus, ohne Konto, in seiner Sprache und nur mit den Fragen zur gebuchten Behandlung. Gleichwertige Alternativen sind ein Tablet am Empfang per QR-Code und Papier: Die Software druckt einen auf Kunde und Termin vorbereiteten Bogen mit QR-Code, nach dem Ausfüllen lädt die Rezeption ein Foto (Handy, Tablet oder Scanner) hoch, der Bogen wird automatisch zugeordnet, die KI übernimmt Ankreuzfelder und Handschrift, markiert unsichere Felder gelb und die Rezeption bestätigt nur diese. Auch fremde oder alte Bögen ohne QR-Code lassen sich so per Foto übernehmen. Das Original wird verschlossen aufbewahrt oder nach Vorgabe des Spas vernichtet. Stammkunden bestätigen nur Änderungen, nach 12 Monaten wird neu abgefragt. Fehlt der Bogen, erinnert die Software am Vortag und markiert den Termin im Kalender. Die Einwilligung ist versioniert und jederzeit widerrufbar.
- **Rollen klar regeln:** Wir sind Auftragsverarbeiter, das Spa ist Verantwortlicher. Wir liefern einen fertigen Auftragsverarbeitungsvertrag (Art. 28), eine Liste der Unterauftragnehmer und eine Vorlage für die Datenschutz-Folgenabschätzung (Art. 35), die jedes Spa wegen der Gesundheitsdaten braucht.
- **Geteilter Kundenstamm:** Gehören die Standorte zu verschiedenen Firmen, brauchen sie eine Vereinbarung über gemeinsame Verantwortung (Art. 26). Die Software kennt deshalb Firmen und Standorte getrennt und liefert die Vorlage mit.
- **Löschkonzept automatisch:** Kundendaten werden nach einstellbarer Inaktivität anonymisiert. Kassenbelege bleiben trotzdem so lange wie vorgeschrieben (DE 8 Jahre für Buchungsbelege und 10 Jahre für Bücher, AT 7 Jahre, CH 10 Jahre). Auskunft und Datenexport für Kunden gehen per Knopfdruck.
- **Mitarbeiterdaten:** Zeiterfassung und Schichtplanung sind eine technische Einrichtung zur Leistungsüberwachung. Bei einem Betriebsrat braucht es dessen Zustimmung (DE § 87 BetrVG, AT § 96 ArbVG). Wir liefern dafür eine Mustervorlage für die Betriebsvereinbarung.
- **Schweiz:** Das revidierte Datenschutzgesetz (seit 2023) ist der DSGVO ähnlich. Hosting in der EU ist für Schweizer Kunden zulässig, sollte aber im Vertrag genannt werden.

## 2. KI-Recht und KI-Telefon

**Priorität: P1.** Weil die KI überall eingreifen soll, gelten die Pflichten des EU AI Act für fast jedes Modul. Die Transparenzpflichten gelten nach aktuellem Stand seit August 2026, also schon zum Start.

**Empfehlung:**

- **KI gibt sich zu erkennen:** Der Telefon-, SMS- und Chat-Assistent sagt am Anfang, dass er eine KI ist (Art. 50 AI Act). Der Kunde kann jederzeit zu einem Menschen wechseln.
- **Keine heimlichen Aufnahmen:** Telefonate aufzuzeichnen ist ohne Zustimmung strafbar (DE § 201 StGB). Standard ist daher nur das Mitschreiben als Text ohne Speicherung des Tons. Eine Aufnahme gibt es nur nach Ansage und Zustimmung.
- **Schichtplanung bleibt Vorschlag:** KI, die Arbeit nach Verhalten oder Eigenschaften von Mitarbeitern verteilt oder ihre Leistung bewertet, gilt als Hochrisiko-KI (Anhang III AI Act). Deshalb schlägt die KI nur vor, ein Mensch bestätigt, und es gibt keine KI-Leistungsbewertung von Mitarbeitern. Das spart die aufwendige Hochrisiko-Zulassung.
- **KI-Anbieter in der EU:** Sprachmodelle laufen über Anbieter mit Datenverarbeitung in der EU und ohne Training auf Kundendaten. Gesundheitsdaten gehen nur an die KI, wenn es für den Vorgang nötig ist.
- **Nachvollziehbar:** Jede KI-Aktion (gebucht, umgeplant, Text verschickt) steht im Protokoll mit Begründung und kann rückgängig gemacht werden.
- **KI-Schulung:** Spas müssen ihre Mitarbeiter im Umgang mit KI schulen (Art. 4 AI Act). Kurze Lernmodule direkt in der Software nehmen ihnen das ab und sind ein Verkaufsargument.

## 3. Sicherheit

**Priorität: P1.** Ab Dezember 2027 gilt der EU Cyber Resilience Act für Software, die als Produkt verkauft wird. Die native Windows-App fällt darunter, und die Meldepflicht für ausgenutzte Sicherheitslücken gilt schon seit September 2026.

**Empfehlung:**

- **Cyber Resilience Act von Anfang an einplanen:** Software-Stückliste (SBOM) im Build erzeugen, einen Prozess für gemeldete Schwachstellen aufsetzen, Sicherheitsupdates über den zugesagten Supportzeitraum garantieren (in der Regel mindestens 5 Jahre) und eine CE-Konformitätserklärung vorbereiten. Das betrifft auch das Lizenzmodell.
- **Mandanten strikt trennen:** Jede Abfrage ist an den Mandanten gebunden (Row-Level-Security in der Datenbank zusätzlich zur Prüfung im Code). Ein automatischer Test prüft bei jedem Build, dass kein Spa Daten eines anderen sieht.
- **Verschlüsselung:** überall TLS, Daten im Ruhezustand verschlüsselt, Gesundheitsdaten zusätzlich pro Mandant mit eigenem Schlüssel.
- **Zahlungsdaten nie selbst speichern:** Kartendaten laufen nur über die Felder des Zahlungsanbieters. So bleibt der PCI-DSS-Aufwand minimal (Fragebogen SAQ A).
- **Prüfen lassen:** Penetrationstest vor dem Start, danach jährlich. Automatische Code- und Abhängigkeitsprüfung in der CI-Pipeline.
- **Zertifizierung später:** ISO 27001 oder ein BSI-C5-Testat verlangen größere Ketten oft. Das als P2 vorbereiten, indem die Prozesse von Anfang an dokumentiert werden.

## 4. Backup, Ausfallsicherheit und Offline-Betrieb

**Priorität: P1.** Ein Spa mit vollem Kalender verliert bei einem Ausfall sofort Umsatz und Vertrauen, deshalb braucht es feste Zusagen: höchstens 5 Minuten Datenverlust und Wiederanlauf in unter einer Stunde.

**Empfehlung:**

- **Backups:** Datenbank mit Wiederherstellung auf jede Minute der letzten 35 Tage, georedundant in einer zweiten EU-Region. Einmal im Monat wird eine Wiederherstellung automatisch getestet.
- **Rezeption läuft offline weiter:** Die Windows-App hält Kalender, Kundenkarten und Kasse lokal vor und gleicht nach dem Ausfall ab (im Technik-Stack schon angelegt). Konflikte löst der Buchungsdienst nach festen Regeln.
- **Kasse bei Ausfall:** In Deutschland muss ein Ausfall der Cloud-TSE dokumentiert werden, in Österreich muss ein Ausfall der Signatureinrichtung über 48 Stunden an FinanzOnline gemeldet werden. Die Software erkennt das, vermerkt es auf dem Beleg und erinnert an die Meldung.
- **Notfallplan auf Papier:** Jeden Morgen wird der Tagesplan aller Standorte als PDF erzeugt und lokal gespeichert, damit die Rezeption auch bei einem Totalausfall weiß, wer wann kommt.
- **Statusseite:** Öffentliche Statusseite und automatische Benachrichtigung der Spas bei Störungen.

## 5. Online-Zahlung, Anzahlung und No-Show-Schutz

**Priorität: P1.** Nicht erschienene Kunden kosten Day-Spas nach gängigen Branchenschätzungen 5 bis 10 Prozent des Umsatzes (nicht selbst geprüft). Anzahlung und Kartenhinterlegung bei der Onlinebuchung sind das wirksamste Mittel dagegen.

**Empfehlung:**

- **Zahlungsanbieter:** einen europäischen Anbieter, der Online, Terminal und TWINT für die Schweiz aus einer Hand bietet (z. B. Adyen, Stripe oder Mollie, Auswahl in P1 nach Gebühren). So landen alle Zahlungen in einem Abgleich mit der Kasse.
- **Regeln pro Behandlung:** Anzahlung, Vorauszahlung oder nur Kartenhinterlegung, abhängig von Preis, Dauer oder Kunde (Neukunde zahlt an, Stammkunde nicht). Kostenlose Stornofrist einstellbar.
- **Ausfallgebühr rechtssicher:** Die Gebühr wird nur fällig, wenn der Kunde beim Buchen den AGB zugestimmt hat. Die Karte wird mit starker Kundenauthentifizierung nach PSD2 hinterlegt und später als Händler-initiierte Zahlung belastet.
- **Warteliste:** Wird ein Termin frei, bietet die Software ihn automatisch per SMS oder WhatsApp den Kunden auf der Warteliste an. Wer zuerst bestätigt, bekommt ihn.
- **Erinnerungen:** automatisch 48 und 2 Stunden vorher, mit Link zum Bestätigen, Verschieben oder Absagen. Die KI ruft bei unbestätigten teuren Terminen auf Wunsch an.

## 6. Gutscheine, Pakete und Mitgliedschaften

**Priorität: P1.** Gutscheine sind bei Day-Spas oft der größte Einzelkanal, besonders vor Weihnachten, und steuerlich fehleranfällig. Die Kasse kennt sie schon, es fehlen der Verkauf, die Steuerlogik und die Regeln über Standorte hinweg.

**Empfehlung:**

- **Gutscheinshop online:** Wert- und Behandlungsgutscheine im eigenen Design, als PDF sofort oder gedruckt per Post, mit Wunschtermin für den Versand. Einbindung in die Website des Spas mit einer Zeile Code.
- **Steuer richtig:** Einzweckgutscheine (Leistung und Steuersatz stehen fest) werden beim Verkauf versteuert, Mehrzweckgutscheine (Wertgutscheine) erst bei der Einlösung. Die Software unterscheidet das automatisch (EU-Gutscheinrichtlinie, in DE § 3 Abs. 13 bis 15 UStG).
- **Gültigkeit:** In DE gilt die regelmäßige Verjährung von 3 Jahren, kürzere Fristen sind meist unwirksam. In AT haben Gerichte kurze Befristungen oft gekippt, in CH gelten 10 Jahre. Die Software schlägt pro Land eine zulässige Frist vor.
- **Standortübergreifend:** Einlösen an jedem Standort. Gehören die Standorte zu verschiedenen Firmen, erzeugt die Software automatisch die Verrechnung zwischen ihnen.
- **Fremdgutscheine:** Jochen Schweizer, mydays und andere als Forderung gegenüber dem Portal buchen, nicht als Umsatz (steht schon im Konzept Buchungsplattformen).
- **Pakete und Mitgliedschaften (P2):** 10er-Karten, Kuren und Monatsabos mit wiederkehrender Abbuchung. Für online abgeschlossene Abos in DE ist ein Kündigungsbutton Pflicht (§ 312k BGB).

## 7. Kundenbindung und Marketing

**Priorität: P2.** Ein Stammkunde ist für ein Day-Spa viel mehr wert als ein Neukunde, und die Daten dafür liegen ohnehin in der Software. Die Konkurrenz bietet hier meist nur einfache Newsletter.

**Empfehlung:**

- **Automatische Kampagnen:** Geburtstagsgruß mit Angebot, Erinnerung an die nächste Behandlung nach passendem Abstand (z. B. Gesichtsbehandlung nach 4 bis 6 Wochen), Rückholaktion nach längerer Pause. Die KI wählt Zeitpunkt und Text und erklärt die Auswahl.
- **Zielgruppen per Satz:** „Alle Kunden, die im Sommer Massagen gebucht haben und seit 3 Monaten nicht da waren“ reicht, die KI baut die Zielgruppe.
- **Rechtlich sauber:** Werbung per E-Mail nur mit Double-Opt-in, per SMS und WhatsApp nur mit ausdrücklicher Einwilligung (DE § 7 UWG, AT § 174 TKG). Terminerinnerungen sind keine Werbung und brauchen keine Einwilligung. Die Software trennt beides.
- **Bewertungen:** Nach dem Besuch eine Bitte um eine Google-Bewertung. Keine Belohnung für positive Bewertungen, denn das wäre irreführend und unzulässig. Unzufriedene Kunden landen zuerst bei der Leitung.
- **Treueprogramm (P3):** Punkte oder Stempelkarte, standortübergreifend, mit Wallet-Karte für Apple und Google.
- **Messbar:** Jede Kampagne zeigt den daraus entstandenen Umsatz im Reporting.

## 8. Lager, Material und Produktverkauf

**Priorität: P1 für den Materialverbrauch, P2 für das volle Lager.** Ohne erfassten Materialverbrauch pro Behandlung ist die vom Auftraggeber gewünschte echte Kostenrechnung nicht möglich, deshalb gehört dieser Teil zum Start.

**Empfehlung:**

- **Rezeptur pro Behandlung:** Jede Behandlung hat eine Stückliste (z. B. 30 ml Massageöl, 1 Maske, 2 Handtücher). Nach der Behandlung wird automatisch abgebucht, und die Kosten fließen in die Kostenrechnung.
- **Verkaufsprodukte:** Kosmetik und Pflegeprodukte über die Kasse verkaufen, mit Barcode und eigener Erlösgruppe. Therapeuten bekommen Produktempfehlungen passend zur Behandlung, auf Wunsch mit Provision.
- **Bestand pro Standort:** Mindestbestände, Umbuchung zwischen Standorten, Bestellvorschläge durch die KI anhand der gebuchten Termine der nächsten Wochen.
- **Charge und Haltbarkeit:** Bei Kosmetik Charge und Ablaufdatum erfassen. So lässt sich bei einem Rückruf sagen, bei welchen Kunden ein Produkt verwendet wurde.
- **Inventur:** Inventur per Handy-Scan, mit GoBD-konformer Protokollierung jeder Korrektur.
- **Wäsche (P3):** Handtücher und Bademäntel als Verbrauch erfassen, wenn das Spa einen Wäscheservice bezahlt.

## 9. Datenmigration von Altsystemen

**Priorität: P1.** Jeder Kunde kommt von einem anderen System, und die größte Angst beim Wechsel ist, Kunden, künftige Termine oder Gutscheinguthaben zu verlieren. Ein einfacher Umzug ist deshalb ein Hauptverkaufsargument.

**Empfehlung:**

- **Fertige Importe** für die häufigsten Herkunftssysteme aus der Konkurrenzanalyse (z. B. Shore, Salonized, Treatwell, TAC, Fresha) und ein allgemeiner Import für Excel und CSV.
- **KI ordnet zu:** Die KI erkennt die Spalten der Exportdatei, schlägt die Zuordnung vor und findet Dubletten („Maria Müller“ und „M. Müller“ mit gleicher Telefonnummer).
- **Reihenfolge der Wichtigkeit:** 1. Offene Gutscheine und Paketguthaben, 2. künftige Termine, 3. Kunden mit Einwilligungen, 4. Behandlungshistorie und Notizen, 5. Leistungen und Preise.
- **Probelauf:** erst Testimport in eine Kopie, das Spa prüft Stichproben, dann der echte Umzug an einem ruhigen Tag mit einem Abgleich der Summen (Anzahl Kunden, Summe der Gutscheine).
- **Alte Kassendaten bleiben im Altsystem:** Sie müssen dort oder als offizieller Export (DSFinV-K in DE, DEP in AT) für die Aufbewahrungsfrist lesbar bleiben. Die Software kann diese Exporte als Archiv ablegen.
- **Umzugsservice:** Bei den ersten Kunden machen wir den Umzug selbst. Daraus entstehen die fertigen Importe.

## 10. Onboarding und Einrichtung

**Priorität: P1.** Das Ziel ist, dass ein Spa an einem Nachmittag ohne Schulungstermin startklar ist. Das senkt unsere Kosten pro Kunde und ist ein klarer Vorteil gegenüber TAC und anderen Hotel-Systemen mit wochenlanger Einführung.

**Empfehlung:**

- **Einrichtung im Gespräch mit der KI:** Das Spa nennt seine Website oder lädt die Preisliste als PDF hoch. Die KI legt daraus Behandlungen, Dauer, Preise, Räume und Öffnungszeiten an, das Spa korrigiert nur noch.
- **Vorlagen:** fertige Behandlungskataloge für typische Day-Spas (Massage, Kosmetik, Hamam, Sauna-Tageskarte) mit Rezepturen für den Materialverbrauch.
- **Kassen-Anmeldung geführt:** In DE müssen elektronische Kassen seit 2025 über ELSTER gemeldet werden, in AT werden Registrierkasse und Signatureinrichtung bei FinanzOnline registriert und ein Startbeleg erstellt. Die Software führt Schritt für Schritt durch und erzeugt die nötigen Angaben.
- **Start-Checkliste** mit Fortschrittsanzeige: Leistungen, Mitarbeiter eingeladen, Kasse gemeldet, Onlinebuchung live, Zahlungsanbieter verbunden.
- **Lernen in der Software:** kurze Rundgänge pro Rolle beim ersten Start, Testmodus mit Beispieldaten zum Ausprobieren ohne echte Buchungen.
- **Testphase:** 30 Tage kostenlos mit voller Funktion (Abstimmung mit dem Thread Lizenzmodell).

## 11. Support, Updates und Betrieb

**Priorität: P1 für Updates ohne Ausfall und Grundsupport, P2 für Servicestufen.** Spas arbeiten abends und am Wochenende, also genau dann, wenn Softwarefirmen üblicherweise Wartung machen. Updates dürfen deshalb nie zu einer Pause führen.

**Empfehlung:**

- **Updates ohne Ausfall:** schrittweise Auslieferung (erst interne Test-Spas, dann 10 Prozent, dann alle) mit Feature-Schaltern, die sich pro Spa an- und abschalten lassen. Bei Fehlern automatisch zurück auf die alte Version.
- **Windows-App aktualisiert sich selbst** im Hintergrund und wechselt erst beim nächsten Start, nie mitten im Kassiervorgang.
- **Support in drei Stufen:** 1. KI-Hilfe in der Software, die das Problem mit Kontext kennt (welche Seite, welcher Fehler), 2. Mensch per Chat oder Telefon, 3. Entwicklung. Fernzugriff nur nach Freigabe durch das Spa.
- **Servicestufen (P2):** z. B. Basis mit Antwort am nächsten Werktag und Premium mit Erreichbarkeit auch am Wochenende. Kasse und Buchung haben immer höchste Dringlichkeit.
- **Überwachung:** OpenTelemetry über .NET Aspire und Azure Monitor. Fehler werden erkannt, bevor ein Spa anruft.
- **Neuigkeiten:** Versionshinweise in der Software, kurz und pro Rolle gefiltert.

## 12. Barrierefreiheit

**Priorität: P1 für den Kundenbereich, P2 für die Mitarbeiteroberflächen.** Seit Juni 2025 müssen Onlinebuchungen für Verbraucher barrierefrei sein (DE Barrierefreiheitsstärkungsgesetz, AT Barrierefreiheitsgesetz). Ausgenommen sind nur Kleinstunternehmen mit weniger als 10 Mitarbeitern und höchstens 2 Mio. Euro Umsatz, also trifft es viele Spas unserer Zielgruppe.

**Empfehlung:**

- **Kundenbereich nach WCAG 2.2 Stufe AA** bauen (erfüllt die geforderte Norm EN 301 549): Bedienung per Tastatur, Screenreader, ausreichende Kontraste, Größenänderung bis 200 Prozent.
- **Barrierefreiheitserklärung** liefern wir fertig mit, das Spa muss sie veröffentlichen.
- **Automatisch prüfen:** Barrierefreiheits-Tests (z. B. axe) laufen in der CI-Pipeline bei jeder Änderung mit.
- **Mitarbeiteroberflächen:** gut lesbare Kontraste, große Bedienflächen für Tablets, Bedienung ohne Maus. Das hilft allen und vermeidet Probleme bei Mitarbeitern mit Einschränkungen.
- **Verkaufsargument:** Haften muss das Spa, nicht der Softwareanbieter. Ein nachweislich barrierefreier Buchungsbereich nimmt ihm dieses Risiko ab.

## 13. Hardware und Geräte

**Priorität: P1.** Die Kasse funktioniert nur mit passender Hardware, und Spas wollen ein Komplettpaket statt einzeln gekaufter Geräte, die nicht zusammenspielen.

**Empfehlung:**

- **Zertifizierte Grundausstattung:** eine kurze Liste getesteter Geräte, die wir garantiert unterstützen: Bondrucker (Epson-Standard ESC/POS), Kassenlade, Barcodescanner, Kundendisplay.
- **Kartenterminal:** Cloud-Terminals des gewählten Zahlungsanbieters statt lokaler Anbindung. Das funktioniert in DE, AT und CH gleich und spart Treiberprobleme. Klassische Terminals über das Protokoll ZVT nur in P2.
- **Sicherheitseinrichtung der Kasse:** In DE eine Cloud-TSE (z. B. fiskaly), in AT eine Signatur nach RKSV über einen Cloud-Dienst. Die Schweiz braucht keine. Cloud-Lösungen vermeiden Hardware am Standort.
- **Tablets** für Anamnese und Unterschrift am Empfang und für Therapeuten in der Kabine.
- **Telefonie für den KI-Assistenten:** über einen SIP-Anbieter, der die bestehende Nummer des Spas übernimmt oder Anrufe weiterleitet.
- **Später (P3):** Spindsysteme, Armbänder und Eintrittskontrolle für Spas mit Sauna- oder Thermenbereich, über offene Schnittstellen der gängigen Hersteller.
- **Hardware-Paket** mit vorkonfigurierten Geräten als zusätzliche Einnahmequelle anbieten (Abstimmung mit dem Thread Lizenzmodell).

## 14. Testen und Qualität

**Priorität: P1.** Bei Kalender und Kasse darf nichts schiefgehen: Eine Doppelbuchung kostet einen Kunden, ein falscher Kassenbeleg kann eine Betriebsprüfung auslösen. Tests gehören deshalb ab dem ersten Commit in die CI-Pipeline, wie der Auftraggeber es für die Integration gefordert hat.

**Empfehlung:**

- **Testpyramide:** viele schnelle Unit-Tests (xUnit), Integrationstests gegen echte Datenbanken in Containern (Testcontainers), wenige End-to-End-Tests der Oberflächen (Playwright fürs Web, Avalonia Headless für Windows).
- **Doppelbuchungen beweisen:** zufällig erzeugte, gleichzeitige Buchungen über alle Kanäle (eigene Buchung, Google, Treatwell) gegen den Kalender laufen lassen. Der Test schlägt fehl, sobald eine Ressource doppelt belegt ist.
- **Schnittstellen-Verträge:** Aus der OpenAPI-Beschreibung prüft ein Test bei jedem Build, dass Web, Windows-App und Partner-Schnittstellen nicht brechen.
- **Kasse gegen Recht testen:** feste Testfälle für TSE, RKSV, Storno, Gutschein und Rechnungssplitting, mit den Testumgebungen der TSE-Anbieter. Optional ein Prüfbericht eines Wirtschaftsprüfers (IDW PS 880) als Verkaufsargument.
- **KI-Qualität messen:** eine Sammlung echter (anonymisierter) Buchungsmails und Anrufe in Deutsch, Österreichisch, Schweizerdeutsch und weiteren Sprachen. Jede Änderung an der KI muss die Trefferquote halten oder verbessern.
- **Geschwindigkeit als Test:** feste Zeitbudgets, z. B. Kalenderwechsel unter 100 ms, und Lasttests vor jedem größeren Release. Wird ein Budget gerissen, schlägt der Build fehl.
- **Pilot-Spas:** 3 bis 5 Spas in DE, AT und CH testen jede Version eine Woche vor allen anderen.

## 15. Weitere Punkte

Sechs kleinere Themen, die einzeln wenig Aufwand sind, aber spät eingebaut teuer werden.

| Thema | Priorität | Warum | Empfehlung |
| --- | --- | --- | --- |
| Qualifikationen der Mitarbeiter | P1 | Für Laser und IPL braucht es in DE seit 2022 einen Fachkundenachweis (NiSV), viele Behandlungen setzen Zertifikate voraus. | Der Kalender plant nur Therapeuten ein, die für die Behandlung qualifiziert sind, und erinnert vor Ablauf eines Nachweises. |
| Steuersätze und Steuerbefreiungen | P1 | DE: Speisen im Spa-Bistro 7 Prozent seit 2026, Getränke 19 Prozent; Heilbehandlungen mit ärztlicher Verordnung steuerfrei (§ 4 Nr. 14 UStG). AT 20 Prozent, CH 8,1 Prozent. | Steuersätze pro Land und Leistung als Stammdaten, Pakete werden automatisch auf die Sätze aufgeteilt. |
| EU Data Act und Anbieterwechsel | P1 | Seit September 2025 müssen Cloud-Dienste den Wechsel zu einem anderen Anbieter ermöglichen, Wechselgebühren fallen bis 2027 weg. | Vollständiger Datenexport in offenem Format per Knopfdruck. Betrifft auch die Verträge im Lizenzmodell. |
| E-Rechnung | P2 | DE: Empfang für Firmen seit 2025 Pflicht, Ausstellung ab 2027 bzw. 2028. Relevant bei Firmenkunden (Firmengutscheine, Mitarbeiter-Wellness). CH: QR-Rechnung. | Rechnungen an Firmen als ZUGFeRD bzw. XRechnung, in CH mit QR-Teil. |
| Minderjährige Kunden | P2 | Manche Behandlungen brauchen die Zustimmung der Eltern. | Alter beim Buchen prüfen, Zustimmung digital einholen. |
| Quellcode-Hinterlegung | P3 | Große Kunden mit Kauflizenz fragen, was bei einer Insolvenz des Anbieters passiert. | Hinterlegung bei einem Treuhänder als Option im Lizenzvertrag. |

## Gesamtübersicht

Die folgende Tabelle ist die Arbeitsliste für die Planung. Die Spalten zeigen pro Thema, was zum Start, im ersten Jahr und später kommt.

| Nr. | Thema | Zum Start (P1) | Im ersten Jahr (P2) | Später (P3) |
| --- | --- | --- | --- | --- |
| 1 | Datenschutz und DSGVO | Gesundheitsdaten getrennt, Einwilligung, AVV, Löschkonzept | Vorlagen Betriebsvereinbarung |  |
| 2 | KI-Recht | KI gibt sich zu erkennen, keine Tonaufnahme ohne Zustimmung, KI nur als Vorschlag | KI-Schulungsmodule |  |
| 3 | Sicherheit | Mandantentrennung, Verschlüsselung, Pentest, SBOM | ISO 27001 vorbereiten |  |
| 4 | Backup und Ausfall | Minuten-Backup, Offline-Rezeption, Kassenausfall |  |  |
| 5 | Zahlung und No-Show | Anzahlung, Kartenhinterlegung, Erinnerungen | Warteliste automatisch |  |
| 6 | Gutscheine und Pakete | Gutscheinshop, Steuerlogik, standortübergreifend | Pakete, Abos |  |
| 7 | Kundenbindung |  | Kampagnen, Bewertungen | Treueprogramm |
| 8 | Lager und Material | Materialverbrauch pro Behandlung | Bestand, Bestellvorschläge, Inventur | Wäsche |
| 9 | Datenmigration | Import für Gutscheine, Termine, Kunden | Fertige Importe pro Altsystem |  |
| 10 | Onboarding | KI-Einrichtung, Kassenanmeldung, Checkliste |  |  |
| 11 | Support und Updates | Updates ohne Ausfall, KI-Hilfe, Überwachung | Servicestufen |  |
| 12 | Barrierefreiheit | Kundenbereich WCAG 2.2 AA | Mitarbeiteroberflächen |  |
| 13 | Hardware | Drucker, Lade, Cloud-Terminal, Cloud-TSE | ZVT-Terminals | Spind- und Eintrittssysteme |
| 14 | Testen und Qualität | Testpyramide, Doppelbuchungstest, Kassentests, Zeitbudgets | Prüfbericht IDW PS 880 |  |
| 15 | Weitere Punkte | Qualifikationen, Steuersätze, Datenexport | E-Rechnung, Minderjährige | Quellcode-Hinterlegung |

**Hinweise für andere Themen:** Der Cyber Resilience Act verlangt zugesagte Sicherheitsupdates, und der Data Act verbietet Wechselhürden. Beides betrifft das Lizenzmodell. Telefonie, SMS, Zahlungsanbieter und Backup in einer zweiten Region gehören in die Hosting-Kosten.

**Offene Frage an den Auftraggeber:** Sollen auch Spas mit ärztlichen oder medizinischen Behandlungen (z. B. Physiotherapie, ästhetische Medizin) Zielgruppe sein? Dann kommen Patientendokumentation und Abrechnung mit Krankenkassen dazu.
