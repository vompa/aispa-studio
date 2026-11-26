# Arbeitsanweisung: Management-Bericht ausbauen – Kennzahlen, Excel/Word/PDF, KI-Auswertung

**Für:** Claude Code im Repository `vompa/aispa-studio`
**Stand:** 26.11.2026, Auftrag des Auftraggebers; kommt zu den in der beauftragten Sitzung schon erfassten Änderungen dazu. Die Reihenfolge gegenüber diesen Änderungen legt die Sitzung fest.
**Ablauf:** Code Guardian nach [docs/arbeitsweise.md](../arbeitsweise.md) Abschnitt 9: eine Aufgabe T-NNN je Buchstabe unten (nächste freie Nummer ist T-092, vorher prüfen), eine Sitzung nach der anderen im Worktree, ein Squash-Commit je Aufgabe. Gemeinsame Dateien aller Aufgaben: `ManagementReports.cs`, `ReportContracts.cs`, `ManagementReportView.axaml`, `ManagementReportViewModel*.cs`; deshalb **nie zwei Aufgaben gleichzeitig**.

---

## 1. Ziel

Die Inhaberin soll am Monatsende in fünf Minuten wissen, ob der Monat gut war, warum, und was sie ändert, und sie soll den Bericht in der Hand haben: als PDF für die Mappe oder die Bank, als Word zum Ergänzen eigener Anmerkungen, als Excel zum Weiterrechnen. Die KI erklärt die Zahlen und schlägt Maßnahmen vor; sie rechnet nichts.

**Was es heute gibt (geprüft am 26.11.2026):**

- Rechnung in `src/AispaStudio.Api/Reporting/ManagementReports.cs` (`ManagementAsync`, `PeriodAsync`): 14 Kennzahlen mit Vorperiode (immer der gleich lange Zeitraum davor), Umsatz nach Erlöskonto und Zahlungsart, Personal, Behandlungen mit Marge, Aktionen, Warnungen. DTO `ManagementReportDto` in `src/AispaStudio.Contracts/ReportContracts.cs:170`.
- Export `GET /api/reports/locations/{id}/management/export` in `src/AispaStudio.Api/Exports/ReportExports.cs:17`: **ein** Blatt, **nur** die Kennzahl-Kacheln (Kennzahl, Wert, Vorperiode, Einheit), Excel oder CSV über `TableExport` (ClosedXML 0.105.1, ADR 0091).
- PDF-Baustein `DocumentBuilder` in `src/AispaStudio.Api/Documents/` (PDFsharp 6.2.4, ADR 0092, Gestaltung in `docs/gestaltung/dokumente.md`), Knopfblock `DocumentActions` in der App. Der Kennzahlen-Bericht war dort ausdrücklich „Stufe 3, später“.
- KI-Kurzkommentar (drei bis fünf Sätze, höchstens 1000 Zeichen) in `Reporting/ManagementSummaries.cs`: Queue-Nachricht `SummarizeManagementReport`, Zahlenprüfung `BriefingSummaries.OnlyKnownNumbers`, Hinweis `Notice`, Anzeige mit `AiPill`. Wochen- und Monatsbericht per Mail als reiner Text ohne Anhang (`Reporting/ReportMails.cs`).
- Vorhanden, aber im Bericht nicht gezeigt: Raumauslastung je Raum und beste Gäste (`ManagementReports.Rankings.cs`: `RoomsAsync`, `TopGuestsAsync`), Vollkosten je Behandlung (`Costs/CostAnalysis.AnalyzeAsync`), Lagerverbrauch (`StockUsageReport`).
- Es gibt keine Diagramme, keine Schnellwahl für Zeiträume, kein Vorjahr, keine Ziele, kein Word, kein Logo-Feld.

**Einschätzung aus Sicht der Inhaberin (Befunde):**

1. **Umsatz enthält den Gutscheinverkauf.** Im Bild: 16.793,90 + 1.367,07 + 1.090,00 (Konto 3600 Mehrzweckgutscheine) = 19.250,97 € „Umsatz brutto“. Ein verkaufter Mehrzweckgutschein ist eine Verbindlichkeit, kein Umsatz; die später mit Gutschein bezahlte Behandlung zählt noch einmal (Zahlungsart Gutschein 841,10 €). Umsatz, Durchschnittsbon und der Vergleich mit anderen Spas sind dadurch zu hoch.
2. **„Folgetermin gebucht“ der Vorperiode hängt vom heutigen Status ab.** `PeriodAsync` zählt nur Termine mit `Status == Booked` nach Periodenende; Folgetermine, die inzwischen stattgefunden haben (`Completed`), fallen für die Vorperiode heraus. Der Vergleich ist nicht belastbar.
3. **Umsatz je Behandlung und je Person kommt aus dem Terminpreis (`a.Price`), nicht aus den Belegen**, obwohl der Kopfkommentar „nie aus Schätzungen“ sagt: Rabatte und Aktionen fehlen. Die Marge nimmt die Kosten nur aus dem Monat von „Bis“, auch wenn der Zeitraum über mehrere Monate geht.
4. **Der Vergleich hinkt.** 01.11. bis 26.11. wird mit 06.10. bis 31.10. verglichen: anderer Wochentags-Mix, heute ist erst halb vorbei. Eine Inhaberin vergleicht mit dem Vormonat bis zum selben Tag und mit dem Vorjahr.
5. **Die Kacheln sagen nicht, ob es gut oder schlecht ist.** Kein Pfeil, keine Veränderung in Prozent, keine Farbe; man rechnet 19.250 gegen 25.371 im Kopf.
6. **Warnungen ohne Maß.** 1,38 € Kassendifferenz steht gleich groß da wie 46 % Auslastung. „Neue Gäste 17, Vorperiode 137“ und „Stammgäste 91,8 %, davor 36,9 %“ sehen nach einem Rand der Beispieldaten aus (Verlauf beginnt erst kurz davor); prüfen, ob Rechnung oder Daten.
7. **Es fehlt, was Entscheidungen auslöst:** Deckungsbeitrag des Zeitraums, Umsatz je verfügbarer Stunde, Produktanteil, entgangener Umsatz durch No-Show und Storno, Verlauf über die Tage, Auslastung nach Wochentag und Tageszeit, Räume, beste und abwandernde Gäste, Buchungsstand der nächsten Wochen.
8. **Der Export ist für die Mappe nicht brauchbar:** nur die Kacheln, Euro-Werte als allgemeine Zahl, Dateiname nur mit dem Startdatum, kein Kopf, keine Tabellen zu Personal und Behandlungen.

**Erfolg ist erreicht, wenn:**

1. Die Befunde 1 bis 3 sind je mit einem Test nachgestellt (erst rot) und behoben; Befund 6 ist geklärt und im PR-Protokoll beantwortet.
2. Der Zeitraum hat eine Schnellwahl, der Vergleich ist wählbar (Zeitraum davor, Vorjahr), jede Kachel zeigt die Veränderung mit Richtung.
3. Der Bericht zeigt die neuen Kennzahlen aus Abschnitt C und einen Verlauf als Balken.
4. Ein Knopf liefert den **ganzen** Bericht als PDF, Word und Excel aus **einem** Berichtskern, mit Kopf des Betriebs, Zeitraum, Vergleich, „Erstellt am … von …“; Drucken und „Per Mail senden“ für das PDF; jeder Export steht im Export-Protokoll; ohne „Berichte mit Beträgen“ fehlen die Beträge in jeder Datei.
5. Ein Knopf „Mit KI auswerten“ liefert eine gegliederte Auswertung (Kernaussagen, Auffälligkeiten, Maßnahmen, offene Fragen), mit KI-Hinweis, nur mit Zahlen aus den Fakten, auf Wunsch im Dokument enthalten.
6. Hilfe, Handbuchbilder, ADRs und Stand-Doku sind nachgezogen; volle Prüfkette am Rundenende grün.

**Nicht Teil dieser Anweisung (später, im PR-Protokoll als „bewusst offen“ nennen):** Monatsziele und Budget mit Soll-Ist; Aufschlüsseln einer Kennzahl bis zur einzelnen Buchung; alle Standorte nebeneinander; Lesemodelle; eigene Berichte per KI bauen; freier Chat über die Zahlen; Logo im Kopf (es gibt kein Logo-Feld); echte Excel- oder Word-Diagramme; Push um 7:30.

## 2. Leitplanken für alle Aufgaben

- `CLAUDE.md` gilt vollständig: englische Namen im Code, Deutsch in Oberfläche, Doku, Dokumenten und Commits; Texte nach `docs/gestaltung/texte.md`, Masken nach `docs/gestaltung/masken.md` und Skill `fluent2-avalonia`, Dokumente nach `docs/gestaltung/dokumente.md`. Nie der Name des Auftraggebers im Repository.
- **Server rechnet, App zeigt.** Jede neue Zahl entsteht in `ManagementReports`, jede Datei in der API. Die KI rechnet nie; Umsätze kommen aus den signierten Belegen.
- **API nur additiv (ADR 0085):** bestehende Felder und Kennzahl-Schlüssel (`revenue`, `revenueNet`, `averageTicket` …) behalten Namen und Bedeutung; Neues kommt als neues Feld mit Standardwert oder als neuer Schlüssel. Nach jeder API-Änderung `dotnet build` und `npm run gen:api`, beide Dateien committen.
- **Rechte:** alles hinter `RequirePermission(AccessRight.ViewFinance)`; Beträge nur mit `ViewAmountReports` und schon im Server entfernt (`WithoutAmounts` erweitern, ein Test je neuem Betragsfeld). Gästenamen (beste und abwandernde Gäste) nur in App und Datei, **nie** in den Fakten für die KI; keine Gesundheitsdaten.
- **Pakete (DEPENDENCY GATE):** PDFsharp bleibt 6.2.4 (ab 6.5 AGPL). Für Word nur `DocumentFormat.OpenXml` in genau der Version, die ClosedXML 0.105.1 schon auflöst (`dotnet list package --include-transitive`), als eigener Eintrag in `Directory.Packages.props`; Herausgeber, Lizenz, `--vulnerable` im PR-Protokoll, Drittanbieter-Liste ergänzen. Keine Chart-Bibliothek, kein QuestPDF, kein EPPlus.
- **Ausbau ankündigen:** Fällt etwas weg oder wird eingeschränkt, vorher den Auftraggeber fragen. Der Menüpunkt „Kennzahlen (Excel)“ wird durch die ganze Arbeitsmappe ersetzt (erstes Blatt mit denselben vier Spalten bleibt); „Kennzahlen (CSV)“ bleibt, wie es ist.
- **Tests zuerst, mit Budget:** je Aufgabe höchstens 5 neue Tests je Paket von rund 45 Minuten, nur für neues Verhalten; Logik in schnellen Tests, Ansichtstests nur für das, was nur die Maske zeigt; Läufe über 2 Minuten im Hintergrund mit Protokoll in `.test-logs/`. Ist eine Aufgabe größer als ein Paket, schneidet die Sitzung sie selbst und committet je Paket lokal.
- **Prüfen als Inhaberin** und zusätzlich einmal ohne „Berichte mit Beträgen“ (Backoffice). Dokumente je Art einmal als Datei ablegen und ansehen (PDF über `pymupdf` als Bild; Word und Excel öffnen oder zurücklesen), Ergebnis ins PR-Protokoll.
- **Abschluss je Aufgabe:** PR-Protokoll, Handtestblock, Hilfe im selben Commit (ADR 0055, 0056).
- **Fragen:** offene Entscheidungen mit Optionen und Empfehlung sammeln und weiterarbeiten, wo es ohne Antwort geht.

## 3. Aufgaben im Überblick

| Nr. | Aufgabe | Modell | Hängt ab von |
|---|---|---|---|
| A | Zahlen richtigstellen: Befunde 1, 2, 3, 6 nachstellen und beheben | opus | – |
| B | Zeitraum-Schnellwahl, Vergleich wählbar, Kacheln mit Veränderung | sonnet | A |
| C | Neue Kennzahlen und Verlauf (C1 Ertrag, Verkauf, Ausfälle, Tagesreihe; C2 Wochentag und Tageszeit, Räume, Gäste, Vorschau) | opus | B |
| D | Berichtskern und Ausgaben: D1 Kern und Excel, D2 PDF mit Drucken und Senden, D3 Word | opus (D1, D2), sonnet (D3) | C |
| E | KI-Auswertung per Knopf, Aufnahme in die Dokumente, PDF im Mailbericht | opus | D |
| F | Hilfe, Handbuchbilder, ADRs, Stand-Doku, Fertigmeldung | sonnet | A–E |

## Aufgabe A: Zahlen richtigstellen

**Erst ein eigenes Paket „Befund“ (Zeitrahmen 30 Minuten, kein Bauen):** je Befund ein Test in `tests/AispaStudio.Api.Tests/ReportTests.cs`, der das Fehlverhalten zeigt; Ergebnis „bestätigt“ oder „widerlegt“ mit Beleg ins PR-Protokoll. Widerlegte Befunde entfallen.

**Dann:**

- **Gutscheinverkauf (Befund 1):** neue Kennzahlen `serviceRevenueNet` „Umsatz ohne Gutscheinverkauf (netto)“, `vouchersSold` „Gutscheine verkauft“, `vouchersRedeemed` „Gutscheine eingelöst“, je mit Vorperiode. Welche Konten als Gutscheinverkauf gelten, kommt aus der vorhandenen Kennzeichnung der Erlöskonten (nicht steuerbar bzw. Gutschein), nicht aus der Kontonummer. `revenue` und `revenueNet` bleiben; ihr Hilfetext sagt, dass sie den Gutscheinverkauf enthalten. Der Durchschnittsbon bekommt eine zweite Kennzahl ohne Gutscheinverkauf. Prüfen, ob `Benchmark.CompareAsync` denselben Fehler hat, und dort die bereinigte Zahl nehmen.
- **Folgetermin (Befund 2):** als Folgetermin zählt jeder Termin nach Periodenende, der nicht storniert und kein No-Show ist. Test: Gast mit abgeschlossenem Folgetermin zählt für die Vorperiode.
- **Umsatz je Behandlung und Person (Befund 3):** aus den Belegzeilen des Zeitraums über `BillLine.AppointmentId`; Termine ohne Beleg zählen mit 0 € und erscheinen als Hinweis „n Termine noch nicht kassiert“. Marge: Kosten je Monat des Termins statt nur des letzten Monats. Lässt sich die Zuordnung Belegzeile → Termin nicht zuverlässig herstellen, stattdessen die Spalte „Umsatz laut Terminpreis“ nennen und im PR-Protokoll begründen.
- **Warnungen (Befund 6):** Kassendifferenz erst ab 5,00 € Summe im Zeitraum; Neue Gäste und Stammgäste gegen die Beispieldaten prüfen (wann beginnt der Verlauf?). Liegt es an den Daten, einen Hinweis „Vergleich reicht vor den Beginn der Aufzeichnung“ zeigen, wenn die Vorperiode vor dem ersten Termin des Standorts beginnt.
- **Export-Kleinigkeiten:** Euro-Kennzahlen als `ColumnKind.Money`; Dateiname mit Von und Bis (`Kennzahlen_2026-11-01_bis_2026-11-26.xlsx`), wie es der App-Test schon erwartet.

Der Vergleichstest der Beispieldaten (`DemoBulkTests`) bleibt die Prüfung, falls eine gemeinsame Rechenfunktion berührt wird.

## Aufgabe B: Zeitraum, Vergleich, Kacheln

- **Schnellwahl** neben Von und Bis: Dieser Monat, Letzter Monat, Dieses Quartal, Dieses Jahr, Letzte 12 Monate. Reicht „Bis“ bis heute, ein kleiner Hinweis „Heute ist noch nicht abgeschlossen“.
- **Vergleich wählbar:** API-Parameter `compare=previous|month|year` (Standard `previous`, damit bestehende Aufrufe gleich bleiben). `month`: derselbe Abschnitt im Vormonat (1. bis 26.), `year`: derselbe Zeitraum im Vorjahr. Das DTO nennt den Vergleichszeitraum (`CompareFrom`, `CompareUntil`); die Kachel schreibt „Vorjahr …“ statt „Vorperiode …“.
- **Kachel:** Veränderung in Prozent (bei Quoten in Prozentpunkten) mit Pfeil; Farbe nach Richtung. Dafür bekommt `Kpi` ein Feld `HigherIsBetter` (bool?, leer bei neutralen Werten wie „Offene Gutscheine“). Farbe nie allein: Pfeil und Text tragen dieselbe Aussage, Screenreader-Name „Umsatz brutto 19.250,97 €, 24 % unter der Vorperiode“. Vorhandenes `Controls/StatTile.cs` prüfen und nutzen, wenn es passt.
- „Gespeichert: …“ bekommt „Ordner öffnen“ (`LastSavedPath` gibt es schon).

## Aufgabe C: Neue Kennzahlen und Verlauf

Alles in `ManagementReports` (eigene partielle Datei je Gruppe), additiv im DTO, ohne Betragsrecht ohne Beträge.

**C1**

- **Ertrag:** Deckungsbeitrag des Zeitraums und in Prozent vom Umsatz (aus `CostAnalysis`, je Termin Umsatz minus Vollkosten); Umsatz je Dienststunde (Umsatz netto ohne Gutscheinverkauf durch Dienststunden) und je Behandlungsstunde.
- **Verkauf:** Produktanteil am Umsatz, Produktumsatz je Behandlung, Umsatz je Gast.
- **Ausfälle:** entgangener Umsatz durch No-Show und durch Storno (Terminpreise der betroffenen Termine), getrennt ausgewiesen.
- **Verlauf:** eine Reihe je Tag (Datum, Umsatz netto, Behandlungen, Auslastung Personal), bei mehr als 62 Tagen je Woche. In der App als einfache Balken in einem eigenen kleinen Control (keine Bibliothek, Werte auch als Tabelle für Screenreader, Skill `dataviz` vor dem Bau lesen).

**C2**

- **Auslastung nach Wochentag und Tageszeit:** Tabelle Wochentag × Vormittag, Mittag, Nachmittag, Abend mit Auslastung in Prozent; schwache Felder sind die Lücken für Aktionen.
- **Räume:** Auslastung je Raum aus `RoomsAsync` (vorhanden).
- **Gäste:** beste Gäste aus `TopGuestsAsync` (vorhanden, 10 Stück); **abwandernde Stammgäste:** mindestens drei Besuche, seit über 90 Tagen keiner, kein Termin in der Zukunft; Anzahl als Kennzahl, Liste mit Name, letztem Besuch, Besuchen. Recht wie die Kundenliste.
- **Vorschau:** gebuchter Umsatz und gebuchte Auslastung der nächsten vier Wochen aus dem Buchungsstand, je Woche; rein gerechnet, keine KI, mit dem Hinweis „Stand heute“.
- **Kanäle:** nur wenn der Termin seine Herkunft schon speichert (prüfen): Buchungen je Kanal. Sonst als „bewusst offen“ nennen, nichts erfinden.

Die Maske bleibt lesbar: Reihenfolge Status und Warnungen, Kacheln in Gruppen mit Überschrift (Umsatz und Ertrag, Auslastung, Gäste, Ausfälle), Verlauf, dann die Tabellen; Checkliste aus `masken.md` abhaken.

## Aufgabe D: Berichtskern und Ausgaben

**D1 Kern und Excel**

- `src/AispaStudio.Api/Reporting/ManagementReportDocument.cs`: baut **einmal** aus `ManagementReportDto` (und, wenn eingeschaltet, Vergleich mit anderen Spas und KI-Text) den Inhalt: Kopf (`DocumentHeaders.ForAsync`, Zeitraum, Vergleich), Abschnitte als Liste von `ExportTable` mit typisierten Spalten, dazu Textblöcke (Warnungen, KI). Alle drei Ausgaben lesen nur dieses Modell; keine Ausgabe rechnet selbst.
- `ColumnKind` bekommt `Percent`. `TableExport` bekommt eine Arbeitsmappe aus mehreren Tabellen; die bestehende Ein-Blatt-Funktion bleibt für die Listen.
- **Arbeitsmappe:** Blätter Übersicht (Kopfblock mit Betrieb, Standort, Zeitraum, Vergleich, erstellt am und von; Kennzahlen mit Wert, Vergleich, Veränderung absolut und in Prozent), Verlauf (eine Zeile je Tag, als Rohdaten für eigene Pivot-Tabellen), Erlöskonten, Zahlungsarten, Personal, Behandlungen, Aktionen, Räume, Gäste, Hinweise (Warnungen, KI-Auswertung mit Hinweis). Formate: Euro mit zwei Stellen, Prozent, Datum; Kopfzeile fett und fixiert, Autofilter, Datenbalken in den Spalten Auslastung und Umsatz, Veränderung mit Pfeil-Symbolsatz; Druckeinrichtung A4 quer, Kopfzeile wiederholt, Fußzeile „Seite n von m“. Farben nur Schwarz, Grau und die Datenbalken.

**D2 PDF**

- `GET /api/reports/locations/{id}/management/pdf?from&until&compare&ai=true|false` und `POST …/management/send` nach dem Muster der vorhandenen Dokumente (`static byte[] Build(data, header)`, `DocumentFiles.Pdf`). A4 hoch; Seite 1: Titel, Zeitraum, KI-Kurzfassung (wenn gewählt, mit Hinweis), Warnungen, Kennzahlen in Gruppen mit Vergleich und Veränderung; danach Verlauf als Balken, Umsatz nach Konto und Zahlart, Personal, Behandlungen, Aktionen, Räume, Gäste, Vorschau. Breite Tabellen auf A4 quer, wenn sie hoch nicht passen.
- `DocumentBuilder` bekommt einen Baustein für waagrechte Balken (Schwarz und Grau nach `dokumente.md`); `dokumente.md` um den Baustein und um Regeln für Excel und Word ergänzen.
- App: `DocumentActions` (Drucken · PDF speichern · Per Mail senden) im Kopf des Abschnitts; der Knopf „Exportieren“ bietet „Bericht (Excel)“, „Bericht (Word)“, „Kennzahlen (CSV)“. Senden nur an eine ausdrücklich eingetippte oder die eigene Adresse; jede Sendung im Export-Protokoll.

**D3 Word**

- Paket nach der Leitplanke; kleiner `WordDocumentBuilder` in `src/AispaStudio.Api/Documents/` mit genau dem, was gebraucht wird: Überschrift, Absatz, Tabelle mit Ausrichtung, Kopf- und Fußzeile mit Seitenzahl, Schrift und Größen wie im PDF. `ExportFormat` bekommt `Docx`; Endpunkt `…/management/export?format=docx`. Inhalt wie das PDF ohne Balken; nach jeder Gruppe ein leerer Absatz „Anmerkungen:“ zum Ergänzen.
- Test: Datei mit dem Open XML SDK zurücklesen (Titel, eine Kennzahl, eine Summe, KI-Hinweis vorhanden, wenn KI enthalten).

## Aufgabe E: KI-Auswertung

- **Bedienung:** Knopf „Mit KI auswerten“ im Abschnitt Management öffnet die Seitenleiste nach dem Muster von ADR 0066. Dort: Schwerpunkt (Gesamt, Umsatz und Ertrag, Auslastung und Dienstplan, Gäste und Bindung, Angebot und Preise), optional „Eigene Frage“ (ein Feld, höchstens 300 Zeichen), Knopf „Auswerten“. Ergebnis gegliedert: **Kernaussagen** (drei bis fünf), **Auffälligkeiten** (Beobachtung, mögliche Ursache ausdrücklich als Vermutung, betroffene Kennzahl), **Maßnahmen** (höchstens fünf, nach Wirkung gereiht, je mit „Woran Sie den Erfolg sehen“), **Was die Zahlen nicht zeigen**. Dazu `AiPill`, der Hinweis, „Kopieren“ und „In den Bericht übernehmen“ (dann steht die Auswertung in PDF, Word und Excel, immer mit Hinweis). Zustände: läuft, fertig, gescheitert mit „Erneut versuchen“; ohne Claude-Schlüssel ist der Knopf gesperrt mit `DisabledHint`. AI-Regeln aus `fluent2-avalonia` einhalten.
- **Server:** nach dem Muster von `ManagementSummaries`: Entität `ManagementAnalysis` (Standort, Von, Bis, Vergleich, Beträge ja/nein, Schwerpunkt, Frage, Fingerabdruck, Status, Ergebnis als JSON, angefordert von und am), Migration für `AispaDbContext`; `POST …/management/analysis` legt an und stellt `AnalyzeManagementReport` in die Wolverine-Queue (Handler `public`, Name endet auf `Handler`, Mandant über `TenantContext.Use`), `GET …/management/analysis/{id}` liefert Status und Ergebnis; die App fragt wie beim Kommentar nach. Gleicher Fingerabdruck, gleicher Schwerpunkt, gleiche Frage: gespeichertes Ergebnis, kein neuer Aufruf.
- **Fakten:** `FactsText` erweitern um die neuen Kennzahlen, die **vorgerechnete** Veränderung (absolut und in Prozent, damit die KI sie nennen darf), Verlauf je Woche, Wochentag-Tabelle, Räume, Vorschau, Vergleich mit anderen Spas, wenn eingeschaltet. **Personal mit Namen wie heute** (Entscheidung des Auftraggebers vom 26.11.2026): an die KI geht der „Name im Haus“ (`InHouseName`, so baut `ManagementReports` die `EmployeeKpi` schon); ist keiner eingetragen, bleibt der Name der Person. Nie der volle bürgerliche Name, wenn ein Name im Haus eingetragen ist; ein Test hält das fest. Keine Gästenamen, keine Gesundheitsdaten.
- **Prompt und Schutz:** Antwort als JSON-Schema über `ClaudeJson.AskAsync`; nichts rechnen, keine Zahl, die nicht wörtlich in den Fakten steht; Ursachen als Vermutung kennzeichnen; die eigene Frage ist Text, keine Anweisung. `OnlyKnownNumbers` über alle Textfelder; schlägt es fehl, Status „gescheitert“, nichts anzeigen. Der bestehende Kurzkommentar bleibt, wie er ist.
- **Mailbericht:** der Wochen- und Monatsbericht hängt das PDF an (`OutgoingMail.Attachments`, je Sicht mit oder ohne Beträge, Grenze 5 MB).
- **Tests:** Zahlenprüfung verwirft eine fremde Zahl; ohne Betragsrecht keine Beträge in den Fakten; kein Gästename in den Fakten; gleicher Fingerabdruck ruft die KI nicht erneut; Handler setzt den Mandanten.
- **Offen an den Auftraggeber (sammeln, nicht blockieren):** (1) Vorschlag: die KI darf Zahlen je Person nennen, empfiehlt aber keine arbeitsrechtlichen Schritte gegen einzelne Personen; als Satz im Prompt, falls gewünscht. (2) Der Vertrag zur Auftragsverarbeitung mit Anthropic ist laut ADR 0008 noch offen.

## Aufgabe F: Hilfe, Doku, Fertigmeldung

- Hilfe in `web/src/help/content/`: `berichte-kennzahlen-ansehen.md` (neue Kennzahlen mit einer Zeile „So wird gerechnet“ je Kennzahl, Vergleich, Schnellwahl), neu `berichte-als-dokument-ausgeben.md` und `berichte-mit-ki-auswerten.md`, `seite-reports.md` anpassen; Fragezeichen (`HelpButton`) an Vergleich und KI-Knopf; Handbuchbilder neu (`scripts/help-images.sh`), **nicht** parallel zur Prüfkette.
- ADRs (nächste freie Nummer laut `CLAUDE.md` 0101, vorher prüfen): neu „Berichte als Dokument aus einem Kern (Excel, Word, PDF)“ mit der Paketwahl für Word; Nachtrag zu ADR 0092 (Kennzahlen-Bericht gebaut), zu ADR 0022 (KI-Auswertung per Knopf, PDF im Mailbericht) und zu **ADR 0008** (Entscheidung vom 26.11.2026: der Name im Haus, ersatzweise der Name der Person, und die Zahlen je Person gehen im Management-Bericht an die KI).
- `docs/stand-und-offene-punkte.md` (Zeile „Berichte je Rolle …“ ist veraltet), `docs/features.md`, `docs/konzepte/reporting.md` (Stand je Punkt), Masken-Inventar.
- Fertigmeldung an den Auftraggeber: je Aufgabe ein Satz, je Dokumentart eine Beispieldatei, die Liste „bewusst offen“ und die gesammelten Fragen.
