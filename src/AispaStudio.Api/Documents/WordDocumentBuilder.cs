using System.Globalization;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace AispaStudio.Api.Documents;

/// <summary>
/// Kleiner Baukasten für Word-Dokumente (.docx) nach docs/gestaltung/dokumente.md, Abschnitt 8: Überschrift, Absatz, Tabelle mit Ausrichtung, Kopf- und Fußzeile mit
/// Seitenzahl. Größen wie im PDF (<c>DocumentBuilder</c>: Titel 16, Überschrift 12, Text 10, klein 8 pt, Rand 48 pt, A4 hoch); nur Schwarz und Grau, keine Farben.
/// Die Schrift heißt „Arial“ (metrisch gleich der Liberation Sans der PDFs und auf jedem Rechner mit Word vorhanden); die API bettet keine Schrift ein.
/// Das Dokument ist auf Deutsch (de-AT) gesetzt, damit die Rechtschreibprüfung von Word passt.
/// </summary>
internal sealed class WordDocumentBuilder(DocumentHeader header)
{
    public const string FontName = "Arial";
    public const string ContentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

    private const int TitleSize = 32;
    private const int HeadingSize = 24;
    private const int BodySize = 20;
    private const int SmallSize = 16;
    private const int SpaNameSize = 28;
    private const string Ink = "000000";
    private const string Grey = "666666";
    private const string LightGrey = "B3B3B3";
    private const int PageWidth = 11906;
    private const int PageHeight = 16838;
    private const int Margin = 960;
    private const int ContentWidth = PageWidth - (2 * Margin);
    private const string LineSpacing = "312";

    private readonly List<OpenXmlElement> _body = [];

    public WordDocumentBuilder Title(string text) => Add(Para(text, TitleSize, bold: true, after: 120, keepNext: true));

    public WordDocumentBuilder Heading(string text) => Add(Para(text, HeadingSize, bold: true, before: 240, after: 80, keepNext: true));

    public WordDocumentBuilder Paragraph(string text, bool bold = false, bool muted = false) =>
        Add(Para(text, BodySize, bold, muted ? Grey : Ink, after: 80));

    public WordDocumentBuilder Small(string text) => Add(Para(text, SmallSize, color: Grey, after: 80));

    /// <summary>Ein leerer Absatz zum Schreiben, etwa unter „Anmerkungen:“.</summary>
    public WordDocumentBuilder EmptyParagraph() => Add(Para("", BodySize, after: 80));

    /// <summary>Tabelle über die Satzbreite; Spaltenbreite nach Gewicht, Kopfzeile fett und auf jeder Seite wiederholt, Zeilen brechen nicht mitten durch.</summary>
    public WordDocumentBuilder Table(IReadOnlyList<DocumentColumn> columns, IEnumerable<DocumentRow> rows, bool compact = false)
    {
        var totalWeight = columns.Sum(c => c.Weight);
        var widths = columns.Select(c => (int)Math.Round(ContentWidth * c.Weight / totalWeight)).ToList();
        var size = compact ? SmallSize : BodySize;
        var table = new W.Table(
            new W.TableProperties(
                new W.TableWidth { Width = Units(ContentWidth), Type = W.TableWidthUnitValues.Dxa },
                new W.TableBorders(
                    new W.TopBorder { Val = W.BorderValues.Single, Size = 6, Color = Ink },
                    new W.BottomBorder { Val = W.BorderValues.Single, Size = 6, Color = Ink },
                    new W.InsideHorizontalBorder { Val = W.BorderValues.Single, Size = 2, Color = LightGrey }),
                new W.TableLayout { Type = W.TableLayoutValues.Fixed },
                new W.TableCellMarginDefault(
                    new W.TopMargin { Width = "40", Type = W.TableWidthUnitValues.Dxa },
                    new W.TableCellLeftMargin { Width = 60, Type = W.TableWidthValues.Dxa },
                    new W.BottomMargin { Width = "40", Type = W.TableWidthUnitValues.Dxa },
                    new W.TableCellRightMargin { Width = 60, Type = W.TableWidthValues.Dxa })),
            new W.TableGrid(widths.Select(w => new W.GridColumn { Width = Units(w) })));
        table.Append(Row(columns, widths, columns.Select(c => c.Title).ToList(), SmallSize, bold: true, isHeader: true));
        foreach (var row in rows)
        {
            table.Append(Row(columns, widths, row.Cells, size, row.Bold, isHeader: false));
        }

        _body.Add(table);
        _body.Add(Para("", SmallSize, after: 60));
        return this;
    }

    public byte[] Build()
    {
        using var stream = new MemoryStream();
        using (var package = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
        {
            var main = package.AddMainDocumentPart();
            AddStyles(main);
            var headerPart = main.AddNewPart<HeaderPart>();
            headerPart.Header = PageHeader();
            var footerPart = main.AddNewPart<FooterPart>();
            footerPart.Footer = PageFooter();
            var section = new W.SectionProperties(
                new W.HeaderReference { Type = W.HeaderFooterValues.Default, Id = main.GetIdOfPart(headerPart) },
                new W.FooterReference { Type = W.HeaderFooterValues.Default, Id = main.GetIdOfPart(footerPart) },
                new W.PageSize { Width = PageWidth, Height = PageHeight },
                new W.PageMargin { Top = Margin, Bottom = Margin, Left = Margin, Right = Margin, Header = 480, Footer = 480 });
            main.Document = new W.Document(new W.Body([.. _body, section]));
            package.PackageProperties.Creator = header.SpaName;
            package.PackageProperties.Created = header.CreatedAt.UtcDateTime;
            main.Document.Save();
        }

        return stream.ToArray();
    }

    private static void AddStyles(MainDocumentPart main)
    {
        var styles = main.AddNewPart<StyleDefinitionsPart>();
        styles.Styles = new W.Styles(new W.DocDefaults(
            new W.RunPropertiesDefault(new W.RunPropertiesBaseStyle(
                new W.RunFonts { Ascii = FontName, HighAnsi = FontName, ComplexScript = FontName },
                new W.FontSize { Val = Units(BodySize) },
                new W.Languages { Val = "de-AT" })),
            new W.ParagraphPropertiesDefault(new W.ParagraphPropertiesBaseStyle(new W.SpacingBetweenLines { After = "0" }))));
    }

    private W.Header PageHeader()
    {
        var lines = new List<OpenXmlElement> { Para(header.SpaName, SpaNameSize, bold: true) };
        lines.AddRange(header.LocationLines.Select(line => Para(line, SmallSize, color: Grey)));
        lines.AddRange(header.CompanyLines.Select(line => Para(line, SmallSize, color: Grey)));
        lines.Add(new W.Paragraph(new W.ParagraphProperties(
            new W.ParagraphBorders(new W.BottomBorder { Val = W.BorderValues.Single, Size = 6, Space = 1, Color = Ink }),
            new W.SpacingBetweenLines { After = "0" })));
        return new W.Header(lines);
    }

    /// <summary>„Erstellt am … von …“ links, „Seite n von m“ rechts (Felder, die Word beim Öffnen und Drucken füllt).</summary>
    private W.Footer PageFooter()
    {
        var paragraph = new W.Paragraph(
            new W.ParagraphProperties(
                new W.ParagraphBorders(new W.TopBorder { Val = W.BorderValues.Single, Size = 2, Space = 4, Color = LightGrey }),
                new W.Tabs(new W.TabStop { Val = W.TabStopValues.Right, Position = ContentWidth })),
            Run(header.CreatedText, SmallSize, false, Grey),
            new W.Run(new W.TabChar()),
            Run("Seite ", SmallSize, false, Grey),
            Field("PAGE"),
            Run(" von ", SmallSize, false, Grey),
            Field("NUMPAGES"));
        return new W.Footer(paragraph);
    }

    private static W.SimpleField Field(string instruction) => new(Run("1", SmallSize, false, Grey)) { Instruction = instruction };

    private static W.TableRow Row(IReadOnlyList<DocumentColumn> columns, List<int> widths, IReadOnlyList<string> cells, int size, bool bold, bool isHeader)
    {
        var properties = new W.TableRowProperties(new W.CantSplit());
        if (isHeader)
        {
            properties.Append(new W.TableHeader());
        }

        var row = new W.TableRow(properties);
        for (var i = 0; i < columns.Count; i++)
        {
            var cell = new W.TableCell(new W.TableCellProperties(new W.TableCellWidth { Width = Units(widths[i]), Type = W.TableWidthUnitValues.Dxa }),
                Para(i < cells.Count ? cells[i] : "", size, bold, Ink, align: columns[i].Align, keepNext: isHeader));
            row.Append(cell);
        }

        return row;
    }

    private static W.Paragraph Para(string text, int size, bool bold = false, string color = Ink, int before = 0, int after = 0, bool keepNext = false,
        DocumentAlign align = DocumentAlign.Left)
    {
        var properties = new W.ParagraphProperties();
        if (keepNext)
        {
            properties.Append(new W.KeepNext());
        }

        properties.Append(new W.SpacingBetweenLines { Before = Units(before), After = Units(after), Line = LineSpacing, LineRule = W.LineSpacingRuleValues.Auto });
        if (align != DocumentAlign.Left)
        {
            properties.Append(new W.Justification { Val = align == DocumentAlign.Right ? W.JustificationValues.Right : W.JustificationValues.Center });
        }

        return new W.Paragraph(properties, Run(text, size, bold, color));
    }

    private static W.Run Run(string text, int size, bool bold, string color)
    {
        var properties = new W.RunProperties(new W.RunFonts { Ascii = FontName, HighAnsi = FontName, ComplexScript = FontName });
        if (bold)
        {
            properties.Append(new W.Bold());
        }

        properties.Append(new W.Color { Val = color }, new W.FontSize { Val = Units(size) });
        return new W.Run(properties, new W.Text(text) { Space = SpaceProcessingModeValues.Preserve });
    }

    private static string Units(int value) => value.ToString(CultureInfo.InvariantCulture);

    private WordDocumentBuilder Add(OpenXmlElement element)
    {
        _body.Add(element);
        return this;
    }
}
