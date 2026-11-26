using AispaStudio.Api.Documents;
using AispaStudio.Api.Exports;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Reporting;

/// <summary>
/// Management-Bericht als Word-Datei (T-095, Paket D3), A4 hoch nach docs/gestaltung/dokumente.md, Abschnitt 8. Liest nur das <see cref="ManagementReportDocument"/> und rechnet
/// nichts; Texte und Zahlen setzt sie wie <see cref="ManagementReportPdf"/> (gleiche Schreibweise), nur ohne Balken: der Verlauf steht als Tabelle. Nach jeder Kennzahlen-Gruppe
/// folgt ein leerer Absatz „Anmerkungen:“ zum Ergänzen.
/// </summary>
internal static class ManagementReportWord
{
    public const string NotesLabel = "Anmerkungen:";

    private const int CompactFrom = 5;

    public static byte[] Build(ManagementReportDocument document)
    {
        var word = new WordDocumentBuilder(document.Header)
            .Title(ManagementReportPdf.Title(document))
            .Paragraph($"{document.Period}, verglichen mit {document.Comparison}", muted: true);
        foreach (var ai in document.Texts.Where(t => t.AiNotice is not null))
        {
            word.Heading(ai.Heading);
            ai.Lines.ToList().ForEach(line => word.Paragraph(line));
            word.Small(ai.AiNotice!);
        }

        foreach (var alerts in document.Texts.Where(t => t.Heading == ManagementReportDocument.AlertsName))
        {
            word.Heading(alerts.Heading);
            alerts.Lines.ToList().ForEach(line => word.Paragraph(line));
        }

        var overview = document.Sections.FirstOrDefault(s => s.Name == ManagementReportDocument.OverviewName);
        if (overview is not null)
        {
            KpiGroups(word, overview);
        }

        foreach (var section in document.Sections.Where(s => s != overview))
        {
            Table(word, section);
        }

        foreach (var notes in document.Texts.Where(t => t.AiNotice is null && t.Heading != ManagementReportDocument.AlertsName))
        {
            word.Heading(notes.Heading);
            notes.Lines.ToList().ForEach(line => word.Paragraph(line, muted: true));
        }

        return word.Build();
    }

    /// <summary>„Management-Bericht-Wien-2027-03-01-bis-2027-03-07.docx“.</summary>
    public static string FileName(ManagementReportDocument document, ManagementReportDto report) =>
        ManagementReportPdf.FileName(document, report)[..^".pdf".Length] + ".docx";

    private static void KpiGroups(WordDocumentBuilder word, ExportTable overview)
    {
        var columns = overview.Columns;
        DocumentColumn[] heads =
        [
            new(columns[0].Name, 3), new(columns[3].Name, 0.8), new(columns[1].Name, 1.2, DocumentAlign.Right), new(columns[2].Name, 1.2, DocumentAlign.Right),
            new(columns[4].Name, 1.4, DocumentAlign.Right), new(columns[5].Name, 1.1, DocumentAlign.Right),
        ];
        foreach (var group in overview.Rows.GroupBy(r => r[6] as string ?? "Kennzahlen"))
        {
            word.Heading(group.Key);
            word.Table(heads, group.Select(r => new DocumentRow(
            [
                ManagementReportPdf.Text(r[0]), ManagementReportPdf.Unit(r[3] as string), ManagementReportPdf.Plain(r[1]), ManagementReportPdf.Plain(r[2]),
                ManagementReportPdf.Signed(r[4]), ManagementReportPdf.Signed(r[5]),
            ])), compact: true);
            word.Paragraph(NotesLabel, bold: true).EmptyParagraph();
        }
    }

    private static void Table(WordDocumentBuilder word, ExportTable section)
    {
        word.Heading(section.Name);
        if (section.Rows.Count == 0)
        {
            word.Paragraph(ManagementReportPdf.NoRows, muted: true);
            return;
        }

        var columns = section.Columns.Select((c, i) => new DocumentColumn(ManagementReportPdf.Header(c), i == 0 ? 2.2 : ManagementReportPdf.Weight(ManagementReportPdf.Header(c)),
            c.Kind is ColumnKind.Text or ColumnKind.Date ? DocumentAlign.Left : DocumentAlign.Right)).ToList();
        word.Table(columns, section.Rows.Select(r => new DocumentRow([.. section.Columns.Select((c, i) => ManagementReportPdf.Format(r[i], c.Kind))])),
            compact: section.Columns.Count >= CompactFrom);
    }
}
