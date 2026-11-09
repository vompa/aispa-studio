# 0091. Exporte über einen gemeinsamen Dienst

- **Status:** angenommen
- **Datum:** 09.11.2026
- **Entschieden von:** Auftraggeber (Ziel und Formate), Claude autonom nach ADR 0019 (Aufbau und Grenzen)

## Kontext

Der Auftraggeber will jede Liste der App als Datei für Excel, Kontakte als vCard für Outlook, Apple und Google, Termine und Dienste als iCal, eine Kundenliste im Format der Buchungsportale und einen Kunden-Import. Bisher gibt es nur die Übergaben an Buchhaltung und Lohn (eigene CSV-Formate in `Accounting/` und `Payroll/`), das Datenerfassungsprotokoll und die TSE-Exporte; Kalender-Abos lesen iCal nur ein (`IcsParser`). Ein Excel-Paket, ein Export-Protokoll und ein gemeinsamer Knopf fehlen.

## Optionen

| Option | Bewertung |
| --- | --- |
| A: Je Seite ein eigener Export (wie Buchhaltung und Lohn heute) | Schnell für die erste Liste; jede weitere Liste erfindet Kopfzeile, Zahlenformat, Dateinamen und Fehlerbild neu, kein gemeinsames Protokoll. |
| B: Gemeinsamer Export-Dienst in der API, ein Knopf in der App | **gewählt.** Eine Tabelle (`ExportTable`) wird zu Excel oder CSV, Kontakte und Termine haben je einen Schreiber; jede Liste liefert nur Spalten und Zeilen. Rechte, Mandant, Protokoll und Dateinamen an einer Stelle. |
| C: Export im Client (die App schreibt die Datei aus den geladenen Daten) | Die App zeigt höchstens 25 Treffer oder einen Tag; der Export braucht die ganze gefilterte Liste, und das Protokoll gehört auf den Server („Server rechnet, App zeigt“). |

Formate: **Excel** mit ClosedXML (MIT, 0.105.1; EPPlus scheidet wegen der Lizenz mit Bedingungen aus), **CSV** (UTF-8 mit BOM, Strichpunkt, Datum TT.MM.JJJJ, Beträge mit Komma, damit Excel in Österreich und Deutschland die Datei direkt öffnet), **vCard 4.0** und **iCal 2.0** mit eigenen Schreibern ohne Paket (RFC 6350 und RFC 5545 sind für die wenigen Felder überschaubar; der eigene `IcsParser` liest die Datei zurück).

## Entscheidung

- Ordner `src/AispaStudio.Api/Exports/`: `TableExport` (Excel und CSV aus einer `ExportTable` mit Spaltenarten Text, Zahl, Betrag, Datum, Uhrzeit, Ja/Nein), `VCardWriter`, `ICalWriter` (mit `VTIMEZONE` des Standorts, damit Outlook die TZID versteht), `ExportFiles` (Dateinamen ohne Umlaute, Inhaltstypen), `ExportLog`.
- Jeder Export läuft über einen Endpunkt der jeweiligen Liste (`GET …/export?format=xlsx|csv|vcf|ics|portal` mit den Filtern der Liste) mit dem Recht der Seite; der Dateiname steht im `Content-Disposition`.
- **Protokoll:** jede Datei hinterlässt eine Zeile in `aispa.Exports` (Art, Format, Zeilenzahl, Filter); die Tabelle hat den Verlaufs-Trigger, der Satz lautet „Export Kundenliste (Excel, 312 Zeilen) von Lena“. `GET /api/exports/history` zeigt das Protokoll der Leitung; die Verlauf-Seite nimmt dieselben Zeilen auf.
- **Grenze:** höchstens 50 000 Zeilen je Datei, sonst Validierungsfehler „Mehr als 50.000 Zeilen. Zeitraum oder Filter enger wählen.“
- **App:** ein Knopf `ExportButton` (sekundär, Symbol und „Exportieren“, Menü mit den Formaten der Liste), der die Datei über `IAispaApi.GetFileAsync` lädt und über den Speicherdialog des Fensters schreibt; Toast „Gespeichert: Kunden_2026-11-09.xlsx“. Ohne Recht legt die Seite kein Export-Modell an, und der Knopf ist unsichtbar. Während des Ladens ist er gesperrt und heißt „Wird erstellt …“ (kein Spinner, Design-Brief 4.6).
- Nie in einer Datei: Gesundheitsdaten, Anamnese, Wünsche zur Behandlung, interne Vermerke (Ausnahme: Spalte „Vermerk“ der Kundenliste für die Leitung).

## Begründung

Gleiche Dateien aus allen Listen, ein Protokoll für den Datenschutz und ein Knopf, den die Rezeption einmal lernt. Der Server kennt Rechte, Mandant und die vollständige Liste; die App müsste alles nachladen. ClosedXML ist das verbreitete Paket ohne Lizenzbedingungen; die Textformate sind klein genug für eigene Schreiber, und ein eigener Leser für iCal besteht schon.

## Folgen und Stand

- Paket ClosedXML in `Directory.Packages.props`; es zieht `DocumentFormat.OpenXml` (MIT) und `SixLabors.Fonts` (Six Labors Split License, für Open Source Apache 2.0) mit; Lizenzhinweise erzeugt `scripts/third-party-notices.py`.
- Migration `ExportLog` (Tabelle `aispa.Exports`), `ChangeHistory.Tables` um `aispa.Exports` erweitert.
- Die Listen (Kunden, Termine, Kassa, Lager, Personal, Verlauf, Berichte) kommen in den Aufgaben B bis D der Arbeitsanweisung dazu; der Kunden-Import (Aufgabe E) nutzt ClosedXML zum Lesen und läuft über die Kunden-Dienste der API.
- Offen: automatische Übertragung an Portale oder Newsletter-Dienste über deren Schnittstellen; Import von Terminen, Gutscheinen und Lagerdaten.

## Quellen

- [Arbeitsanweisung Export/Import](../umsetzung/arbeitsanweisung-export-import.md)
- [ClosedXML](https://github.com/ClosedXML/ClosedXML) (MIT), RFC 6350 (vCard 4.0), RFC 5545 (iCalendar)
- ADR 0082 (Änderungsverlauf über den Änderungs-Trigger)
