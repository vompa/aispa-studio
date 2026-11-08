# Produktdefinition und Anforderungen

Was AISPA Studio sein soll, in den Worten des Auftraggebers und mit den daraus abgeleiteten Anforderungen. Stand: 18.10.2026. Wie die Punkte entschieden wurden, steht im [Entscheidungsprotokoll](../entscheidungen/README.md); was davon schon gebaut ist, in [Stand und offene Punkte](../stand-und-offene-punkte.md).

## Ziel

Eine Software für Day-Spas (Termine, Behandlungen, Personal, Gäste, Kasse), die besser ist als die Konkurrenz. Arbeitsname im Repository: AISPA Studio.

## Zielgruppe

- Unabhängige Day-Spas mit höchstens 50 Mitarbeitenden ([ADR 0001](../entscheidungen/0001-zielgruppe-day-spas-dach.md)).
- Märkte Deutschland, Österreich und Schweiz; Österreich zuerst, dann Deutschland, dann Schweiz ([ADR 0011](../entscheidungen/0011-erster-markt-oesterreich.md)).
- Mehrere Standorte, die sich einen Kundenstamm teilen; Standorte können eigene Firmen sein ([ADR 0013](../entscheidungen/0013-standorte-kundenstamm-gesundheitsdaten.md)).
- Nicht in Version 1: Spas mit medizinischen Behandlungen; die Architektur bleibt offen dafür ([ADR 0018](../entscheidungen/0018-medizinische-spas-nicht-in-v1.md)).

## Anforderungen des Auftraggebers (17. und 18.10.2026)

| Thema | Vorgabe vom Auftraggeber (sinngemäß, Zitate in Anführungszeichen) | Umgesetzt in |
| --- | --- | --- |
| Kalender | „Der klare Fokus liegt auf der Planbarkeit und dem Kalender, der soll die besten Eigenschaften aller Konkurrenten vereinen und natürlich noch eigene bessere enthalten.“ | ADR 0002, PR #3 |
| Schnittstellen | Anbindungen für ERP, Treatwell usw.; Treatwell-Bestätigungsmails verwerten | ADR 0012, 0014, PR #8, #11 |
| Kosten | „Eine wahre Kostenanalyse pro Behandlung mit Material, Mitarbeiter, Miete, Raumpflege usw.“ | ADR 0021, PR #14 |
| Benchmark | „Eine Möglichkeit, sich mit anderen auf Knopfdruck zu vergleichen.“ | ADR 0021, PR #24 |
| Terminannahme | „Automatische Terminentgegennahme per Mail, Telefon, SMS und was sonst noch sinnvoll ist, mit Text und Sprache in den wichtigsten Sprachen, aber auch erweiterbar.“ | ADR 0009, PR #8, #13, #17 |
| Arbeitszeit | Die rechtlichen Normen für die Ausgabe der Tages- und Schichtpläne | ADR 0025, PR #10, #16 |
| Erlöskonten | Ein eigenes Konto in der Buchhaltung für einen Teil des Geschäfts, das „natürlich versteuert wird“ | ADR 0010, PR #5, #11 |
| Kassa | „In jeder Form: bar, Gutscheine, Kartenzahlung, Sofortüberweisung, Splitten der Rechnungen usw.“, für DACH | ADR 0010, PR #5, #7, #21 |
| Aktionen und Rabatte | Runde 5 (Ä36): der Assistent beantwortet „Welche Rabatte oder Aktionen laufen gerade?“, die Kassa schlägt die laufende Aktion beim Kassieren vor; zeitlich begrenzt, an allen oder ausgewählten Standorten, für Behandlungen oder Pakete, Prozent oder Betrag; der Rabatt bleibt ein Kassa-Rabatt, nichts wird ohne Klick angewendet | ADR 0071, ADR 0010 |
| Betriebsfragen im Assistenten | Runde 5 (Ä36): „Wie lange haben wir heute offen?“, „Wer arbeitet morgen?“, „Top-Behandlungen diesen Monat“ und andere Fragen zum Betrieb beantwortet der Assistent aus den Daten des Spas, regelbasiert und mit Quelle; die KI erkennt nur die Absicht und glättet höchstens den Satz, Beträge nur mit „Berichte mit Beträgen“ | ADR 0072, ADR 0008, ADR 0070 |
| Weg zum Spa, Auswertungen für die Rezeption, fehlende Angaben, Geburtstage | Runde 5b (Ä41, Ä42, Ä44, Ä45): Gäste planen im Kundenbereich per „Route planen“ den Weg zum Standort (Karten-App des Geräts, ohne Standortabfrage) und lesen die Anreise im Termin und in der Bestätigungsmail; die Rezeption bekommt Auswertungen ohne Beträge im Assistenten; der Assistent nennt fehlende Angaben („Unvollständig“) mit Link in die Maske am Feld und bietet nach dem Speichern an, die Frage zu wiederholen; Geburtstage von Gästen mit Termin zeigt der Kalender mit einem Geschenk-Symbol und beantwortet der Assistent, ohne den Kundenstamm aufzulisten | ADR 0072 (Nachtrag), ADR 0008, ADR 0070 |
| Kalender-Abos | Runde 5b (Ä43): Feiertage, Schulferien und Hinweise kommen als öffentliche Kalender-Adressen (iCal) in den Kalender, der Datei-Import ist die Rückfallebene; je Spa gibt es vorab drei Feiertags-Abos (Österreich, Deutschland, Schweiz), eingeschaltet nur das des eigenen Landes. Zwei Schalter: „Aktiv“ für alle nur mit der Leitung, „Zeigen“ im Kalender je Zugang; ganztägige Feiertage stehen im Datumstitel der Kopfzeile, nicht als Band über den Spalten | [ADR 0075](../entscheidungen/0075-kalender-abos-fuer-feiertage-und-ferien.md), Runde 5b Block K4 |
| Betriebsinformationen und Kontakte | Runde 5b (Ä39): der Assistent beantwortet „Versicherungsdaten, Ansprechpartner, Kontaktdaten, Handwerker-Nummern, betriebsfremde Vertrauenspersonen mit Zuständigkeiten, Lohnverrechnung- oder Buchhaltungs-Kontakte, alles, was es so um einen Betrieb gibt“; ein eigenes Verzeichnis mit Kategorie, Zuständigkeit, Standorten und Stufe „Team“ oder „Leitung“, Seite „Betriebsinfo“ in der Verwaltung, der Assistent nennt Einträge der Stufe „Leitung“ nur mit Recht; keine Passwörter | ADR 0074, ADR 0072, ADR 0070 |
| Schließzeiten und Betriebsausfall | Runde 5b (Ä38): „Ausnahmen und Betriebsurlaub sollen als Einstellung entstehen, auch wenn ein schwerwiegender Schaden (Wasser) entstanden ist, vielleicht die Möglichkeit, Termine abzusagen“; je Standort mit Art, Zeitraum (auch halbe Tage) und Text für Gäste; keine freien Zeiten und keine Buchung darin (intern, online, Assistent); bestehende Termine bleiben, bis sie in einer Rückfrage abgesagt werden (Mail oder SMS über die Queue, kein Ausfallhonorar, Protokoll je Termin) | ADR 0073, ADR 0008 |
| Standorte | „Mehrere Standorte, die sich den Kundenpool usw. teilen“ | ADR 0013, PR #3, #12 |
| KI | „Von Beginn an eine KI integriert, die bei jedem Fall unterstützend eingreifen kann“ | ADR 0008, PR #8, #18 |
| Technik | „Continuous Integration muss gegeben sein, die Oberfläche soll so schnell reagieren wie möglich, Backend-Abfragen usw. sollen entkoppelt ablaufen.“ | ADR 0006, 0017, PR #1 |
| Oberflächen | Windows nativ in .NET 10 und ein sehr schnelles JS-Framework mit bester .NET-Anbindung | ADR 0004, PR #1, #20 |
| Rollen | „Die Oberfläche soll sich klar an die jeweiligen Benutzer vom Management bis zum Kunden anpassen.“ | ADR 0003, PR #6 |
| Benutzerverwaltung | „So einfach wie möglich, mit modernen Ansätzen“ | ADR 0007, PR #1, #6 |
| Reporting | Für Behörden, Management, Rezeption (Tagesstart) und Therapeuten, mit Todo je Behandlung: Raum, Kunde, Vorlieben | ADR 0022, PR #16 |
| Lizenz | Kauf, Lizenz oder Leasing; Lizenzen von der Weiterentwicklung entkoppeln | ADR 0015 |
| Hosting | Was kostet der Betrieb, z. B. auf Azure | ADR 0016 |
| Fehlendes | „All das, was ich noch nicht angegeben habe, eigenständig die wichtigsten Punkte berücksichtigen“ | ADR 0020 |
| Anamnese | Papier muss immer möglich sein, zusätzlich digital per Link und am Tablet, Foto mit KI-Übernahme | ADR 0013, PR #9, #15 |
| Online-Buchung | Online-Buchung und Webzugang für Kunden | ADR 0003, PR #9, #19, #22 |
| Digitaler Euro | „Kann man den digitalen EUR schon irgendwie vorbereiten?“ | ADR 0028, PR #21 |
| Tagesabschluss, Buchhaltung, Lohn | Tagesabschlüsse und Pakete für Buchhaltung und Lohnverrechnung | PR #5, #11, #25 |
| Demo-Daten | Beispiel-Behandlungen nach Le Siam und anderen Top-Spas in Wien | ADR 0029, PR #23 |
| Lokaler Test | Zuerst lokal testen, Windows-Client mit Docker-Servern | ADR 0030, PR #26 |
| Dokumentation | Entscheidungswege und die ganze Definition im Repository, um selbst in Claude Code weiterzuarbeiten | dieser Ordner, `CLAUDE.md`, PR #27 |
| Einzelplatz | Alle Webmodule optional, Datenbank lokal im Windows-Client, Flag `standalone` mit klassischer Benutzerverwaltung, Abgleich sobald eine PostgreSQL dazukommt | ADR 0041, PR #80, #88 (offen) |
| Windows-App | „Der Windows Client schaut etwas spartanisch aus, ist der schon fertig?“, dazu ein sinnvoller Testdatensatz | ADR 0045, 0032, PR #32 bis #89, #36 |
| Übergabe | Threads stoppen, alle Entscheidungen, offenen Arbeitsanweisungen und Überlegungen im Repository festhalten, Weiterarbeit in Claude Code | ADR 0046, [`uebergabe.md`](../uebergabe.md) |

## Leitlinien

Diese Grundsätze gelten für jede Funktion:

1. **Kalender zuerst.** Alles hängt am Ressourcen-Kalender, der Person, Raum, Gerät und Kapazität gemeinsam plant. Belegungslogik nur im Backend.
2. **Rollen statt Menüs.** Jede Rolle sieht, was sie für ihren Arbeitstag braucht. Rechte prüft immer der Server.
3. **KI schlägt vor, der Mensch bestätigt.** KI gibt sich zu erkennen, rechnet nie Pflichtzahlen und handelt mit den Rechten der Person.
4. **Recht by Design.** Kassenpflichten (RKSV, KassenSichV, GeBüV, GoBD), DSGVO mit Gesundheitsdaten als eigener Klasse, AI Act, Barrierefreiheit, Arbeitszeitrecht. Keine Funktion, die Umsätze verbirgt; getrennte Bereiche nur über versteuerte Erlöskonten.
5. **Schnell durch Architektur.** Sofortige Anzeige, Bestätigung im Hintergrund, Live-Updates, alles Langsame über die Queue.
6. **Fair im Preis.** Abo pro Standort, monatlich kündbar, keine Provision pro Buchung, keine Preise pro Mitarbeitenden.

## Rollen

| Rolle | Oberfläche | Wichtigste Aufgaben |
| --- | --- | --- |
| Inhaber:in | Web, Windows-App | alles; bleibt Eigentümer des Kontos (Inhaber- und Manager-Zugänge, Lizenz und Abrechnung) |
| Manager | Web, Windows-App | dieselben Rechte wie der Inhaber, ohne Inhaber- und Manager-Zugänge zu verwalten und ohne Lizenz und Abrechnung zu kündigen ([ADR 0070](../entscheidungen/0070-rollen-manager-und-backoffice.md)) |
| Standortleitung | Web | Stammdaten, Dienstplan und Kennzahlen ohne Beträge für die eigenen Standorte; Beträge nur mit dem Recht „Berichte mit Beträgen“ |
| Rezeption | Windows-App (offline-fähig), Web | Kalender, Buchen, Kassa, Posteingang, Tagesstart; Berichte mit Beträgen nur mit dem einzeln vergebenen Recht |
| Backoffice | Web (Verwaltung) | Buchhaltung und Lohnverrechnung: liest und exportiert Berichte mit Beträgen, Belege, Tagesabschlüsse, Dienstplan und Zeiten; bucht und kassiert nicht |
| Therapeut:in | Web als PWA auf dem Handy | Mein Tag, Behandlungskarten mit Vorbereitungs-Todo, Notizen |
| Kunde | Kundenbereich `/buchen/` | Buchen, Termine verwalten, Anamnese, Warteliste, Gutscheine |

## Abgrenzung

- Keine eigene Finanzbuchhaltung; Exporte an DATEV, BMD und später RZL, Bexio, Abacus.
- Keine nativen Mobil-Apps; Therapeuten und Kunden nutzen das Web.
- Keine Funktionen zum Verbergen von Umsätzen, auch nicht über Termin- oder Kostenseite.
- Keine Partneranfragen an Treatwell oder Google ohne Freigabe des Auftraggebers ([ADR 0014](../entscheidungen/0014-buchungsplattformen-ohne-partnervertrag.md)).
