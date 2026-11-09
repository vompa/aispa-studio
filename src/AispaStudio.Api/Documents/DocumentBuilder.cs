using System.Globalization;
using System.Text;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using QRCoder;

namespace AispaStudio.Api.Documents;

/// <summary>
/// Baut ein PDF aus Bausteinen (ADR 0091): Kopf mit Spa-Name, Firmendaten und Standort auf jeder Seite, Titel, Überschriften, Absätze, Schlüssel-Wert-Listen,
/// Tabellen mit Spaltenausrichtung und wiederholter Kopfzeile nach einem Seitenumbruch, QR-Code, Unterschriftenzeilen; Fußzeile mit „Seite n von m“ und
/// „Erstellt am … von …“. Gestaltung nach docs/gestaltung/dokumente.md: nur Schwarz und Grau, Linien nur waagrecht, Beträge rechts, Summen fett.
/// Schrift ist die eingebettete Liberation Sans (<see cref="DocumentFonts"/>) in WinAnsi, Zahlen und Daten im Format de-AT wie <c>ReceiptPrinter</c>.
/// </summary>
internal sealed class DocumentBuilder(DocumentFormat format, DocumentHeader header)
{
    private const double A4Short = 595.28;
    private const double A4Long = 841.89;
    private const double ReceiptWidthMm = 80;
    private const double LineFactor = 1.3;
    private const double CellPadding = 3;
    private const double SpaNameSize = 14;

    private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-AT");
    private static readonly Encoding WinAnsi = CreateWinAnsi();
    private static readonly XPdfFontOptions FontOptions = new(PdfFontEncoding.WinAnsi);
    private static readonly XSolidBrush Ink = new(XColors.Black);
    private static readonly XSolidBrush Grey = new(XColor.FromGrayScale(0.4));
    private static readonly XSolidBrush Shade = new(XColor.FromGrayScale(0.94));
    private static readonly XPen BlackLine = new(XColors.Black, 0.75);
    private static readonly XPen GreyLine = new(XColor.FromGrayScale(0.7), 0.25);

    private readonly Sizes _sizes = format == DocumentFormat.Receipt80 ? Sizes.Receipt : Sizes.A4;
    private readonly List<IBlock> _blocks = [];
    private string? _title;

    private enum Style
    {
        Title,
        Heading,
        Body,
        Bold,
        Muted,
        Small,
        Display,
    }

    private interface IBlock
    {
        IEnumerable<Item> Layout(Layouter layouter);
    }

    public static string Money(decimal amount) => amount.ToString("#,##0.00", German);

    /// <summary>Betrag mit Zeichen für den Euro, sonst mit dem Kürzel der Währung: „25,00 €“.</summary>
    public static string Amount(decimal amount, string currency) => $"{Money(amount)} {(currency == "EUR" ? "€" : currency)}";

    public static string Number(decimal value) => value.ToString("#,##0.##", German);

    public static string Date(DateOnly day) => day.ToString("dd.MM.yyyy", German);

    public static string Date(DateTimeOffset at, TimeZoneInfo zone) => TimeZoneInfo.ConvertTime(at, zone).ToString("dd.MM.yyyy", German);

    public static string DateTime(DateTimeOffset at, TimeZoneInfo zone) => TimeZoneInfo.ConvertTime(at, zone).ToString("dd.MM.yyyy HH:mm", German);

    public static string Time(DateTimeOffset at, TimeZoneInfo zone) => TimeZoneInfo.ConvertTime(at, zone).ToString("HH:mm", German);

    /// <summary>Titel des Dokuments; der erste Titel wird auch der Titel der PDF-Datei.</summary>
    public DocumentBuilder Title(string text)
    {
        _title ??= text;
        return Add(new TextBlock(text, Style.Title, DocumentAlign.Left));
    }

    /// <summary>
    /// Große Zeile in der Mitte, z. B. der Spa-Name auf dem Geschenkgutschein; <paramref name="documentTitle"/> wird, wenn noch kein Titel steht, der Titel der PDF-Datei.
    /// </summary>
    public DocumentBuilder Display(string text, string? documentTitle = null)
    {
        _title ??= documentTitle;
        return Add(new TextBlock(text, Style.Display, DocumentAlign.Center));
    }

    public DocumentBuilder Heading(string text) => Add(new TextBlock(text, Style.Heading, DocumentAlign.Left));

    public DocumentBuilder Paragraph(string text, bool bold = false, bool muted = false, DocumentAlign align = DocumentAlign.Left) =>
        Add(new TextBlock(text, bold ? Style.Bold : muted ? Style.Muted : Style.Body, align));

    public DocumentBuilder Small(string text, DocumentAlign align = DocumentAlign.Left) => Add(new TextBlock(text, Style.Small, align));

    /// <summary>Beschriftung links in Grau, Wert rechts; leere Werte stehen als „–“.</summary>
    public DocumentBuilder KeyValues(IEnumerable<(string Key, string Value)> rows) => Add(new KeyValueBlock([.. rows]));

    /// <summary>Tabelle mit Kopfzeile, die nach einem Seitenumbruch wiederholt wird; <paramref name="total"/> ist die fette Summenzeile mit Linie darüber.</summary>
    public DocumentBuilder Table(IReadOnlyList<DocumentColumn> columns, IEnumerable<DocumentRow> rows, DocumentRow? total = null) =>
        Add(new TableBlock(columns, [.. rows], total));

    public DocumentBuilder QrCode(string text, double sizeMm = 30, DocumentAlign align = DocumentAlign.Center) => Add(new QrBlock(text, sizeMm, align));

    public DocumentBuilder Space(double points = 8) => Add(new SpaceBlock(points));

    public DocumentBuilder Rule() => Add(new RuleBlock());

    /// <summary>Unterschriftenzeilen nebeneinander, z. B. „Kassierin“ und „Leitung“.</summary>
    public DocumentBuilder SignatureLines(params string[] labels) => Add(new SignatureBlock(labels));

    public DocumentBuilder PageBreak() => Add(new PageBreakBlock());

    public byte[] Build()
    {
        DocumentFonts.Ensure();
        var isReceipt = format == DocumentFormat.Receipt80;
        var pageWidth = format switch
        {
            DocumentFormat.A4Portrait => A4Short,
            DocumentFormat.A4Landscape => A4Long,
            _ => XUnit.FromMillimeter(ReceiptWidthMm).Point,
        };
        var pageHeight = format == DocumentFormat.A4Landscape ? A4Short : A4Long;
        var margin = _sizes.Margin;
        var width = pageWidth - (2 * margin);
        using var measure = XGraphics.CreateMeasureContext(new XSize(pageWidth, A4Long), XGraphicsUnit.Point, XPageDirection.Downwards);
        var layouter = new Layouter(measure, width, _sizes);
        var pageHeader = isReceipt ? ReceiptHeader(layouter) : PageHeader(layouter);
        var footerHeight = LineHeight(_sizes.Small) + (isReceipt ? 4 : 8);
        var items = _blocks.SelectMany(b => b.Layout(layouter)).ToList();
        List<List<Item>> pages;
        if (isReceipt)
        {
            pages = [items];
            pageHeight = margin + pageHeader.Height + items.Sum(i => i.Height) + footerHeight + margin;
        }
        else
        {
            pages = Paginate(items, pageHeight - (2 * margin) - pageHeader.Height - footerHeight);
        }

        using var document = new PdfDocument();
        document.Info.Title = _title ?? "Dokument";
        document.Info.Author = header.SpaName;
        document.Info.Creator = "AISPA Studio";
        for (var index = 0; index < pages.Count; index++)
        {
            var page = document.AddPage();
            page.Width = XUnit.FromPoint(pageWidth);
            page.Height = XUnit.FromPoint(pageHeight);
            using var gfx = XGraphics.FromPdfPage(page);
            var y = margin;
            pageHeader.Draw(gfx, margin, y);
            y += pageHeader.Height;
            foreach (var item in pages[index])
            {
                item.Draw(gfx, margin, y);
                y += item.Height;
            }

            DrawFooter(gfx, layouter, margin, pageHeight - margin - footerHeight, index + 1, pages.Count, isReceipt);
        }

        using var stream = new MemoryStream();
        document.Save(stream, false);
        return stream.ToArray();
    }

    private static Encoding CreateWinAnsi()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding(1252, EncoderFallback.ReplacementFallback, DecoderFallback.ReplacementFallback);
    }

    /// <summary>Zeichen außerhalb von WinAnsi (z. B. Häkchen) werden zu „?“, statt die Schrift scheitern zu lassen.</summary>
    private static string Ansi(string text) => WinAnsi.GetString(WinAnsi.GetBytes(text));

    private static double LineHeight(double size) => size * LineFactor;

    private static XStringAlignment Alignment(DocumentAlign align) => align switch
    {
        DocumentAlign.Right => XStringAlignment.Far,
        DocumentAlign.Center => XStringAlignment.Center,
        _ => XStringAlignment.Near,
    };

    private static void Write(XGraphics gfx, string text, XFont font, XBrush brush, double x, double y, double width, DocumentAlign align)
    {
        if (text.Length == 0)
        {
            return;
        }

        var style = new XStringFormat { Alignment = Alignment(align), LineAlignment = XLineAlignment.Near };
        gfx.DrawString(text, font, brush, new XRect(x, y, width, LineHeight(font.Size)), style);
    }

    /// <summary>
    /// Verteilt die Bausteine auf Seiten: ein Baustein, der nicht mehr passt, beginnt die nächste Seite; Überschriften bleiben bei ihrem Inhalt, Tabellenzeilen
    /// bekommen auf der neuen Seite die Kopfzeile wieder.
    /// </summary>
    private static List<List<Item>> Paginate(List<Item> items, double available)
    {
        var pages = new List<List<Item>> { new() };
        var y = 0.0;
        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            if (item.PageBreak)
            {
                if (pages[^1].Count > 0)
                {
                    pages.Add([]);
                    y = 0;
                }

                continue;
            }

            var needed = item.Height;
            for (var next = i; next < items.Count - 1 && items[next].KeepWithNext && next - i < 3; next++)
            {
                needed += items[next + 1].Height;
            }

            if (y + needed > available && pages[^1].Count > 0)
            {
                pages.Add([]);
                y = 0;
                if (item.TableHeader is { } tableHeader)
                {
                    pages[^1].Add(tableHeader);
                    y += tableHeader.Height;
                }
            }

            pages[^1].Add(item);
            y += item.Height;
        }

        return pages;
    }

    private Item PageHeader(Layouter layouter)
    {
        var nameFont = layouter.Font(SpaNameSize, true);
        var small = layouter.Font(_sizes.Small);
        var lineHeight = LineHeight(_sizes.Small);
        var rightWidth = layouter.Width * 0.45;
        var leftWidth = layouter.Width - rightWidth - 12;
        var leftHeight = LineHeight(SpaNameSize) + (header.LocationLines.Count * lineHeight);
        var rightHeight = header.CompanyLines.Count * lineHeight;
        var ruleOffset = Math.Max(leftHeight, rightHeight) + _sizes.Gap;
        return new Item(ruleOffset + 1 + (2 * _sizes.Gap), (gfx, x, y) =>
        {
            Write(gfx, Ansi(header.SpaName), nameFont, Ink, x, y, leftWidth, DocumentAlign.Left);
            for (var i = 0; i < header.LocationLines.Count; i++)
            {
                Write(gfx, Ansi(header.LocationLines[i]), small, Grey, x, y + LineHeight(SpaNameSize) + (i * lineHeight), leftWidth, DocumentAlign.Left);
            }

            for (var i = 0; i < header.CompanyLines.Count; i++)
            {
                Write(gfx, Ansi(header.CompanyLines[i]), small, Grey, x + layouter.Width - rightWidth, y + (i * lineHeight), rightWidth, DocumentAlign.Right);
            }

            gfx.DrawLine(BlackLine, x, y + ruleOffset, x + layouter.Width, y + ruleOffset);
        });
    }

    private Item ReceiptHeader(Layouter layouter)
    {
        var nameFont = layouter.Font(_sizes.Title, true);
        var small = layouter.Font(_sizes.Small);
        var lineHeight = LineHeight(_sizes.Small);
        var lines = header.CompanyLines.Concat(header.LocationLines).ToList();
        var ruleOffset = LineHeight(_sizes.Title) + (lines.Count * lineHeight) + _sizes.Gap;
        return new Item(ruleOffset + 1 + (2 * _sizes.Gap), (gfx, x, y) =>
        {
            Write(gfx, Ansi(header.SpaName), nameFont, Ink, x, y, layouter.Width, DocumentAlign.Center);
            for (var i = 0; i < lines.Count; i++)
            {
                Write(gfx, Ansi(lines[i]), small, Grey, x, y + LineHeight(_sizes.Title) + (i * lineHeight), layouter.Width, DocumentAlign.Center);
            }

            gfx.DrawLine(GreyLine, x, y + ruleOffset, x + layouter.Width, y + ruleOffset);
        });
    }

    private void DrawFooter(XGraphics gfx, Layouter layouter, double x, double y, int page, int pages, bool isReceipt)
    {
        var small = layouter.Font(_sizes.Small);
        if (isReceipt)
        {
            Write(gfx, Ansi(header.CreatedText), small, Grey, x, y + 4, layouter.Width, DocumentAlign.Center);
            return;
        }

        gfx.DrawLine(GreyLine, x, y, x + layouter.Width, y);
        Write(gfx, Ansi(header.CreatedText), small, Grey, x, y + 6, layouter.Width * 0.7, DocumentAlign.Left);
        Write(gfx, $"Seite {page} von {pages}", small, Grey, x + (layouter.Width * 0.7), y + 6, layouter.Width * 0.3, DocumentAlign.Right);
    }

    private DocumentBuilder Add(IBlock block)
    {
        _blocks.Add(block);
        return this;
    }

    /// <summary>Schriftgrößen und Ränder in Punkt je Format; der Bon ist schmal und dicht gesetzt.</summary>
    private sealed record Sizes(double Title, double Heading, double Body, double Small, double Margin, double Gap)
    {
        public static readonly Sizes A4 = new(16, 12, 10, 8, 48, 6);
        public static readonly Sizes Receipt = new(11, 9, 8, 7, 10, 3);
    }

    /// <summary>Ein gesetztes Stück Inhalt mit fester Höhe; Zeilen, Tabellenzeilen und Abstände sind je ein Stück, damit Seiten zwischen ihnen brechen.</summary>
    private sealed class Item(double height, Action<XGraphics, double, double>? draw = null)
    {
        public double Height { get; } = height;

        /// <summary>Bleibt beim nächsten Stück (Überschrift vor ihrem Inhalt, Kopfzeile vor der ersten Tabellenzeile).</summary>
        public bool KeepWithNext { get; init; }

        public bool PageBreak { get; init; }

        /// <summary>Kopfzeile der Tabelle, die auf einer neuen Seite vor dieser Zeile wiederholt wird.</summary>
        public Item? TableHeader { get; init; }

        public void Draw(XGraphics gfx, double x, double y) => draw?.Invoke(gfx, x, y);
    }

    /// <summary>Misst Text mit den echten Metriken der eingebetteten Schrift und bricht ihn in Zeilen um.</summary>
    private sealed class Layouter(XGraphics measure, double width, Sizes sizes)
    {
        private readonly Dictionary<(double Size, bool Bold), XFont> _fonts = [];

        public double Width => width;

        public Sizes Sizes => sizes;

        public XFont Font(double size, bool bold = false)
        {
            if (!_fonts.TryGetValue((size, bold), out var font))
            {
                font = new XFont(DocumentFonts.FamilyName, size, bold ? XFontStyleEx.Bold : XFontStyleEx.Regular, FontOptions);
                _fonts[(size, bold)] = font;
            }

            return font;
        }

        public double TextWidth(string text, XFont font) => measure.MeasureString(text, font).Width;

        public List<string> Wrap(string text, XFont font, double available)
        {
            var lines = new List<string>();
            foreach (var paragraph in Ansi(text).ReplaceLineEndings("\n").Split('\n'))
            {
                var line = "";
                foreach (var word in paragraph.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                {
                    var candidate = line.Length == 0 ? word : $"{line} {word}";
                    if (TextWidth(candidate, font) <= available)
                    {
                        line = candidate;
                        continue;
                    }

                    if (line.Length > 0)
                    {
                        lines.Add(line);
                    }

                    line = word;
                    while (line.Length > 1 && TextWidth(line, font) > available)
                    {
                        var fit = line.Length - 1;
                        while (fit > 1 && TextWidth(line[..fit], font) > available)
                        {
                            fit--;
                        }

                        lines.Add(line[..fit]);
                        line = line[fit..];
                    }
                }

                lines.Add(line);
            }

            return lines;
        }
    }

    private sealed class TextBlock(string text, Style style, DocumentAlign align) : IBlock
    {
        public IEnumerable<Item> Layout(Layouter layouter)
        {
            var sizes = layouter.Sizes;
            var (size, bold, brush, before, after, keep) = style switch
            {
                Style.Title => (sizes.Title, true, Ink, 0d, sizes.Gap, true),
                Style.Heading => (sizes.Heading, true, Ink, sizes.Gap * 1.5, sizes.Gap / 2, true),
                Style.Display => (sizes.Title * 2, true, Ink, 0d, sizes.Gap, false),
                Style.Bold => (sizes.Body, true, Ink, 0d, sizes.Gap / 2, false),
                Style.Muted => (sizes.Body, false, (XBrush)Grey, 0d, sizes.Gap / 2, false),
                Style.Small => (sizes.Small, false, Grey, 0d, sizes.Gap / 2, false),
                _ => (sizes.Body, false, Ink, 0d, sizes.Gap / 2, false),
            };
            var font = layouter.Font(size, bold);
            var lineHeight = LineHeight(size);
            if (before > 0)
            {
                yield return new Item(before);
            }

            foreach (var line in layouter.Wrap(text, font, layouter.Width))
            {
                yield return new Item(lineHeight, (gfx, x, y) => Write(gfx, line, font, brush, x, y, layouter.Width, align)) { KeepWithNext = keep };
            }

            yield return new Item(after) { KeepWithNext = keep };
        }
    }

    private sealed class KeyValueBlock(IReadOnlyList<(string Key, string Value)> rows) : IBlock
    {
        public IEnumerable<Item> Layout(Layouter layouter)
        {
            if (rows.Count == 0)
            {
                yield break;
            }

            var font = layouter.Font(layouter.Sizes.Body);
            var lineHeight = LineHeight(layouter.Sizes.Body);
            var keyWidth = Math.Min(rows.Max(r => layouter.TextWidth(Ansi(r.Key), font)) + 12, layouter.Width * 0.45);
            var valueWidth = layouter.Width - keyWidth;
            foreach (var (key, value) in rows)
            {
                var lines = layouter.Wrap(string.IsNullOrWhiteSpace(value) ? "–" : value, font, valueWidth);
                yield return new Item((lines.Count * lineHeight) + 1, (gfx, x, y) =>
                {
                    Write(gfx, Ansi(key), font, Grey, x, y, keyWidth, DocumentAlign.Left);
                    for (var i = 0; i < lines.Count; i++)
                    {
                        Write(gfx, lines[i], font, Ink, x + keyWidth, y + (i * lineHeight), valueWidth, DocumentAlign.Left);
                    }
                });
            }

            yield return new Item(layouter.Sizes.Gap);
        }
    }

    private sealed class TableBlock(IReadOnlyList<DocumentColumn> columns, IReadOnlyList<DocumentRow> rows, DocumentRow? total) : IBlock
    {
        public IEnumerable<Item> Layout(Layouter layouter)
        {
            var weights = columns.Sum(c => c.Weight);
            var widths = columns.Select(c => layouter.Width * c.Weight / weights).ToArray();
            var headFont = layouter.Font(layouter.Sizes.Small, true);
            var font = layouter.Font(layouter.Sizes.Body);
            var boldFont = layouter.Font(layouter.Sizes.Body, true);
            var headerItem = Row(layouter, new DocumentRow([.. columns.Select(c => c.Title)]), widths, headFont, LineHeight(layouter.Sizes.Small), BlackLine, null);
            yield return headerItem;
            foreach (var row in rows)
            {
                yield return Row(layouter, row, widths, row.Bold ? boldFont : font, LineHeight(layouter.Sizes.Body), GreyLine, headerItem);
            }

            if (total is not null)
            {
                yield return Row(layouter, total with { Bold = true }, widths, boldFont, LineHeight(layouter.Sizes.Body), null, headerItem, BlackLine);
            }

            yield return new Item(layouter.Sizes.Gap);
        }

        private Item Row(Layouter layouter, DocumentRow row, double[] widths, XFont font, double lineHeight, XPen? below, Item? tableHeader, XPen? above = null)
        {
            var cells = widths.Select((w, i) => layouter.Wrap(i < row.Cells.Count ? row.Cells[i] : "", font, Math.Max(w - (2 * CellPadding), 1))).ToArray();
            var height = (cells.Max(c => c.Count) * lineHeight) + (2 * CellPadding);
            var brush = row.Muted ? (XBrush)Grey : Ink;
            return new Item(height, (gfx, x, y) =>
            {
                if (row.Muted)
                {
                    gfx.DrawRectangle(Shade, x, y, layouter.Width, height);
                }
                else if (row.MutedCells is { Count: > 0 })
                {
                    var shadeX = x;
                    for (var i = 0; i < widths.Length; i++)
                    {
                        if (row.MutedCells.Contains(i))
                        {
                            gfx.DrawRectangle(Shade, shadeX, y, widths[i], height);
                        }

                        shadeX += widths[i];
                    }
                }

                if (above is not null)
                {
                    gfx.DrawLine(above, x, y, x + layouter.Width, y);
                }

                var cellX = x;
                for (var i = 0; i < widths.Length; i++)
                {
                    var cellBrush = row.MutedCells?.Contains(i) == true ? Grey : brush;
                    for (var line = 0; line < cells[i].Count; line++)
                    {
                        Write(gfx, cells[i][line], font, cellBrush, cellX + CellPadding, y + CellPadding + (line * lineHeight), widths[i] - (2 * CellPadding), columns[i].Align);
                    }

                    cellX += widths[i];
                }

                if (below is not null)
                {
                    gfx.DrawLine(below, x, y + height, x + layouter.Width, y + height);
                }
            })
            {
                KeepWithNext = tableHeader is null,
                TableHeader = tableHeader,
            };
        }
    }

    private sealed class QrBlock(string text, double sizeMm, DocumentAlign align) : IBlock
    {
        public IEnumerable<Item> Layout(Layouter layouter)
        {
            using var generator = new QRCodeGenerator();
            using var data = generator.CreateQrCode(text, QRCodeGenerator.ECCLevel.M);
            var modules = data.ModuleMatrix.Select(row => row.Cast<bool>().ToArray()).ToArray();
            var size = XUnit.FromMillimeter(sizeMm).Point;
            var module = size / modules.Length;
            var offset = align switch
            {
                DocumentAlign.Center => (layouter.Width - size) / 2,
                DocumentAlign.Right => layouter.Width - size,
                _ => 0,
            };
            yield return new Item(size + layouter.Sizes.Gap, (gfx, x, y) =>
            {
                for (var row = 0; row < modules.Length; row++)
                {
                    for (var column = 0; column < modules[row].Length; column++)
                    {
                        if (modules[row][column])
                        {
                            gfx.DrawRectangle(Ink, x + offset + (column * module), y + (row * module), module, module);
                        }
                    }
                }
            });
        }
    }

    private sealed class SignatureBlock(IReadOnlyList<string> labels) : IBlock
    {
        public IEnumerable<Item> Layout(Layouter layouter)
        {
            var small = layouter.Font(layouter.Sizes.Small);
            var gap = 24.0;
            var segment = (layouter.Width - (gap * (labels.Count - 1))) / labels.Count;
            yield return new Item(layouter.Sizes.Gap * 3);
            yield return new Item(LineHeight(layouter.Sizes.Small) + 4, (gfx, x, y) =>
            {
                for (var i = 0; i < labels.Count; i++)
                {
                    var left = x + (i * (segment + gap));
                    gfx.DrawLine(BlackLine, left, y, left + segment, y);
                    Write(gfx, Ansi(labels[i]), small, Grey, left, y + 3, segment, DocumentAlign.Left);
                }
            });
        }
    }

    private sealed class SpaceBlock(double points) : IBlock
    {
        public IEnumerable<Item> Layout(Layouter layouter)
        {
            yield return new Item(points);
        }
    }

    private sealed class RuleBlock : IBlock
    {
        public IEnumerable<Item> Layout(Layouter layouter)
        {
            var gap = layouter.Sizes.Gap;
            yield return new Item(2 * gap, (gfx, x, y) => gfx.DrawLine(GreyLine, x, y + gap, x + layouter.Width, y + gap));
        }
    }

    private sealed class PageBreakBlock : IBlock
    {
        public IEnumerable<Item> Layout(Layouter layouter)
        {
            yield return new Item(0) { PageBreak = true };
        }
    }
}
