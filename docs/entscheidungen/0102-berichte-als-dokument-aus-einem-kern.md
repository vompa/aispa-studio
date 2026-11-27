# 0102. Berichte als Dokument aus einem Kern (Excel, Word, PDF)

- **Status:** angenommen
- **Datum:** 26.11.2026
- **Entschieden von:** Auftraggeber (Arbeitsanweisung Berichte-Ausbau: PDF, Word und Excel), Paketwahl und Aufbau Claude, autonom nach ADR 0019 (T-095)

## Kontext

Die Inhaberin soll den Management-Bericht in der Hand haben: als PDF für die Mappe oder die Bank, als Word zum Ergänzen eigener Anmerkungen, als Excel zum Weiterrechnen. Bis T-095 gab es nur einen Export mit den Kennzahl-Kacheln (ein Blatt, vier Spalten, ADR 0091) und das PDF-Gerüst `DocumentBuilder` (ADR 0092), das den Kennzahlen-Bericht ausdrücklich als „Stufe 3, später“ führte.

Drei Dateiarten aus drei Quellen würden drei Fassungen derselben Zahlen ergeben: Eine Spalte, die in Excel „Veränderung“ heißt, hieße im PDF „Differenz“, und eine Warnung stünde nur in einer Datei.

## Optionen

| Option | Bewertung |
| --- | --- |
| Je Dateiart eine eigene Abfrage und ein eigener Aufbau | schnell gebaut, aber Zahlen und Texte laufen auseinander; jede neue Kennzahl müsste dreimal eingetragen werden |
| Ein Berichtskern, aus dem Excel, PDF und Word nur zeichnen | gewählt |
| Word über Vorlagen mit Platzhaltern (Serienbrief) | braucht eine Vorlagendatei mit Layout, das niemand pflegt; Tabellen mit wechselnder Zeilenzahl gehen damit schlecht |
| Word über ein PDF-zu-Word-Werkzeug | Fremdprogramm im Container, unsauberes Ergebnis, keine echten Tabellen |

## Entscheidung

Der Bericht entsteht **einmal** als `ManagementReportDocument` (`Reporting/ManagementReportDocument.cs`) aus dem `ManagementReportDto`: Kopf des Betriebs (`DocumentHeader`), Titel, Zeitraum, Vergleich, Kopfblock („Erstellt am … von …“), Abschnitte als typisierte Tabellen (`ExportTable`: Übersicht, Verlauf, Erlöskonten, Zahlungsarten, Personal, Behandlungen, Aktionen, Räume, Wochentage, Kanäle, Vorschau, Gäste, Vergleich mit anderen Spas) und Textblöcke (Warnungen, Hinweise, KI-Kommentar, übernommene KI-Auswertung, jeweils mit ihrem Hinweis). Die Rechte (Beträge, Gästelisten) wirken **vor** dem Kern: Was die Person auf der Seite nicht sieht, steht in keiner Datei.

Die drei Ausgaben lesen nur diesen Kern:

- **Excel:** Arbeitsmappe über ClosedXML (ADR 0091), ein Blatt je Tabelle, fixierte Kopfzeile, Autofilter, Datenbalken und grauer Pfeil-Symbolsatz, A4 quer.
- **PDF:** der vorhandene `DocumentBuilder` (PDFsharp, ADR 0092) mit eingebetteter Schrift, Balken für den Verlauf.
- **Word:** neu `WordDocumentBuilder`, geschrieben gegen das **Open XML SDK**, Paket **`DocumentFormat.OpenXml` 3.1.1**. Schrift Arial (die API bettet in Word keine Schrift ein), Größen wie im PDF, Kopf und Fuß mit den Feldern `PAGE` und `NUMPAGES`, nach jeder Kennzahlen-Gruppe „Anmerkungen:“ mit einem leeren Absatz, schwarz-weiß, keine Diagramme.

Der Abruf bleibt am Pfad `GET /api/reports/locations/{id}/management/export` (additiv, ADR 0085): `format=xlsx&content=report` für die Arbeitsmappe, `format=docx` für Word (der Parameter `content` gilt dann nicht), `format=csv` und der Standard `content=kpis` für die Kennzahlen wie bisher. Das PDF liefert `…/management/pdf`. Jede Datei steht im Export-Protokoll.

### Paketwahl für Word

| Paket | Bewertung |
| --- | --- |
| `DocumentFormat.OpenXml` 3.1.1 | gewählt: Herausgeber Microsoft, Lizenz MIT, Quelle `github.com/dotnet/Open-XML-SDK`; **schon heute im Paketgraphen**, weil ClosedXML 0.105.1 genau diese Version zieht (ADR 0091); kein neuer Herausgeber, keine neue Lizenz, keine neue Version |
| `Xceed.Words.NET` (DocX) | Community-Lizenz nur für nicht kommerziellen Einsatz, sonst kostenpflichtig (nach Angabe des Herstellers, nicht neu geprüft); kein Grund für ein zweites Paket neben dem SDK |
| `NPOI` | Apache-2.0, aber ein großes Paket mit eigenem Excel-Teil, den wir schon durch ClosedXML haben |
| `Spire.Doc`, `Aspose.Words` | kostenpflichtig oder mit Wasserzeichen in der Gratisfassung |
| Word-Datei von Hand als ZIP mit XML schreiben | nur das SDK-Gerüst nachgebaut; Fehler im Schema fallen erst beim Öffnen in Word auf |

Der Paketstand steht als eigener Eintrag in `Directory.Packages.props` und als `PackageReference` im API-Projekt und bleibt damit bei 3.1.1, bis jemand ihn bewusst hebt. `dotnet list package --vulnerable --include-transitive` meldete am 26.11.2026 keine anfälligen Pakete; `THIRD-PARTY-NOTICES.md` nannte das Paket schon als Abhängigkeit von ClosedXML.

## Begründung

Ein Kern hält die Zahlen gleich: Jede neue Kennzahl steht einmal im Kern und erscheint in allen Dateien. Ein Test je Datei liest sie zurück (Titel, eine Kennzahl, die Seitenzahl). Rechte und Hinweise entstehen ebenfalls nur an einer Stelle, deshalb gilt die Regel „ohne Betragsrecht keine Beträge, ohne Hinweis kein KI-Text“ für jede Ausgabe, auch für künftige. Das Open XML SDK ist die Referenz von Microsoft für das Dateiformat; es validiert gegen das Schema, und mit dem `OpenXmlValidator` ließ sich die Datei ohne Word auf Fehler prüfen (0 Fehler, Office 2019).

## Folgen und Stand

- Code: `Reporting/ManagementReportDocument.cs`, `ManagementReportPdf.cs`, `ManagementReportWord.cs`, `Documents/WordDocumentBuilder.cs`, `Exports/TableExport.cs` (`ToWorkbook`).
- Gebaut in T-095 (Kern, Excel, PDF, Word); die KI-Auswertung kommt in alle drei Dateien über den optionalen Parameter `analysisId` (T-096, siehe Nachtrag zu ADR 0022).
- Im Menü **Ausgabe** der Maske stehen alle Wege: Drucken, PDF speichern, Per Mail senden, Bericht (Excel), Bericht (Word), Kennzahlen (CSV).
- Bewusst offen: Word-Datei in Word geöffnet und gedruckt (nur Schema und Rücklesen geprüft, auf dem Rechner ist kein Office); echte Word-Diagramme (der Verlauf steht als Tabelle); Logo im Kopf (die Firmendaten haben keines); A4 quer für einzelne breite Tabellen im PDF und in Word.

## Quellen

- [Arbeitsanweisung Berichte-Ausbau](../umsetzung/arbeitsanweisung-berichte-ausbau.md), Aufgabe D
- [Gestaltung der Dokumente](../gestaltung/dokumente.md), Abschnitt 8
- ADR 0091 (Exporte, ClosedXML), ADR 0092 (PDF), ADR 0085 (API-Version)
- `DocumentFormat.OpenXml` auf NuGet: `nuspec` 3.1.1 (Autor Microsoft, `MIT`), geprüft am 26.11.2026
