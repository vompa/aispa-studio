using System.Globalization;
using System.Text;

namespace AispaStudio.Api.HrFile;

/// <summary>
/// Schreibt reinen Text als PDF, ohne Paket (R05 Slice 110): A4, Helvetica aus den Standardschriften jedes PDF-Lesers, Zeichensatz WinAnsi, Zeilenumbruch und neue Seiten.
/// Zeichen außerhalb von WinAnsi werden zu „?“.
/// </summary>
internal sealed class SimplePdf
{
    private const int PageWidth = 595;
    private const int PageHeight = 842;
    private const int Margin = 56;
    private const int LineHeight = 14;
    private const int BodySize = 10;
    private const int HeadingSize = 12;
    private const int FallbackWidth = 667;

    /// <summary>Breiten der Helvetica in Tausendstel der Schriftgröße für die Zeichen 32 bis 126 (Adobe Font Metrics).</summary>
    private static readonly int[] Widths =
    [
        278, 278, 355, 556, 556, 889, 667, 191, 333, 333, 389, 584, 278, 333, 278, 278, 556, 556, 556, 556, 556, 556, 556, 556, 556, 556, 278, 278, 584, 584, 584, 556,
        1015, 667, 667, 722, 722, 667, 611, 778, 722, 278, 500, 667, 556, 833, 722, 778, 667, 778, 722, 667, 611, 722, 667, 944, 667, 667, 611, 278, 278, 278, 469, 556,
        333, 556, 556, 500, 556, 556, 278, 556, 556, 222, 222, 500, 222, 833, 556, 556, 556, 556, 333, 500, 278, 556, 500, 722, 500, 500, 500, 334, 260, 334, 584,
    ];

    private static readonly Dictionary<char, byte> WinAnsiSpecial = new()
    {
        ['€'] = 0x80,
        ['‚'] = 0x82,
        ['„'] = 0x84,
        ['…'] = 0x85,
        ['‘'] = 0x91,
        ['’'] = 0x92,
        ['“'] = 0x93,
        ['”'] = 0x94,
        ['•'] = 0x95,
        ['–'] = 0x96,
        ['—'] = 0x97,
    };

    private readonly List<(string Text, bool Bold)> _lines = [];

    public void Heading(string text)
    {
        if (_lines.Count > 0)
        {
            _lines.Add(("", false));
        }

        AddWrapped(text, true);
    }

    public void Text(string text) => AddWrapped(text, false);

    public byte[] ToBytes()
    {
        using var output = new MemoryStream();
        var offsets = new List<long>();

        void Object(string body)
        {
            offsets.Add(output.Position);
            Ascii(output, $"{offsets.Count} 0 obj\n{body}\nendobj\n");
        }

        Ascii(output, "%PDF-1.4\n");
        var pages = _lines.Count == 0 ? [[]] : _lines.Chunk((PageHeight - (2 * Margin)) / LineHeight).ToList();
        const int FirstPageObject = 5;
        var kids = string.Join(' ', Enumerable.Range(0, pages.Count).Select(i => $"{FirstPageObject + (2 * i) + 1} 0 R"));
        Object("<< /Type /Catalog /Pages 2 0 R >>");
        Object($"<< /Type /Pages /Kids [{kids}] /Count {pages.Count} >>");
        Object("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>");
        Object("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold /Encoding /WinAnsiEncoding >>");
        for (var i = 0; i < pages.Count; i++)
        {
            var content = Content(pages[i]);
            Object($"<< /Length {content.Length} >>\nstream\n{content}\nendstream");
            Object($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {PageWidth} {PageHeight}] /Resources << /Font << /F1 3 0 R /F2 4 0 R >> >> /Contents {FirstPageObject + (2 * i)} 0 R >>");
        }

        var start = output.Position;
        var table = new StringBuilder($"xref\n0 {offsets.Count + 1}\n0000000000 65535 f \n");
        foreach (var offset in offsets)
        {
            table.Append(CultureInfo.InvariantCulture, $"{offset:D10} 00000 n \n");
        }

        table.Append(CultureInfo.InvariantCulture, $"trailer\n<< /Size {offsets.Count + 1} /Root 1 0 R >>\nstartxref\n{start}\n%%EOF\n");
        Ascii(output, table.ToString());
        return output.ToArray();
    }

    private static void Ascii(Stream output, string text) => output.Write(Encoding.ASCII.GetBytes(text));

    private static string Content(IEnumerable<(string Text, bool Bold)> lines)
    {
        var content = new StringBuilder();
        var y = PageHeight - Margin;
        foreach (var (text, bold) in lines)
        {
            if (text.Length > 0)
            {
                content.Append(CultureInfo.InvariantCulture, $"BT /{(bold ? "F2" : "F1")} {(bold ? HeadingSize : BodySize)} Tf {Margin} {y} Td <{Convert.ToHexString(Encode(text))}> Tj ET\n");
            }

            y -= LineHeight;
        }

        return content.ToString();
    }

    private static byte[] Encode(string text) => [.. text.Select(c => c < 0x7F || (c >= 0xA0 && c <= 0xFF) ? (byte)c : WinAnsiSpecial.GetValueOrDefault(c, (byte)'?'))];

    /// <summary>Breite in Punkt; fette Schrift ist etwas breiter, die Annahme liegt auf der sicheren Seite.</summary>
    private static double Width(string text, bool bold)
    {
        var units = text.Sum(c => c is >= ' ' and <= '~' ? Widths[c - ' '] : FallbackWidth);
        return bold ? units * 1.1 * HeadingSize / 1000 : units * (double)BodySize / 1000;
    }

    private void AddWrapped(string text, bool bold)
    {
        const int Available = PageWidth - (2 * Margin);
        foreach (var paragraph in text.ReplaceLineEndings("\n").Split('\n'))
        {
            var line = "";
            foreach (var word in paragraph.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                var candidate = line.Length == 0 ? word : $"{line} {word}";
                if (Width(candidate, bold) <= Available)
                {
                    line = candidate;
                    continue;
                }

                if (line.Length > 0)
                {
                    _lines.Add((line, bold));
                }

                line = word;
                while (Width(line, bold) > Available)
                {
                    var fit = line.Length - 1;
                    while (Width(line[..fit], bold) > Available)
                    {
                        fit--;
                    }

                    _lines.Add((line[..fit], bold));
                    line = line[fit..];
                }
            }

            _lines.Add((line, bold));
        }
    }
}
