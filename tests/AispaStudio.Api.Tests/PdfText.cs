using System.Text;
using PdfSharp.Pdf;
using PdfSharp.Pdf.Content;
using PdfSharp.Pdf.Content.Objects;
using PdfSharp.Pdf.IO;

namespace AispaStudio.Api.Tests;

/// <summary>
/// Liest ein PDF mit PDFsharp zurück: Seitenzahl, Seitengröße und den Text aus den Textoperatoren (Tj, TJ) jeder Seite. Die Dokumente schreiben ihre Schrift
/// in WinAnsi (<c>DocumentBuilder</c>), deshalb lässt sich jede Zeichenkette direkt in Text zurückwandeln; die Codepage steht hier unabhängig vom Code.
/// </summary>
internal static class PdfText
{
    private static readonly Encoding WinAnsi = Encoding.GetEncoding(1252);

    static PdfText() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    public static PdfDocument Open(byte[] pdf) => PdfReader.Open(new MemoryStream(pdf), PdfDocumentOpenMode.Import);

    /// <summary>Legt das PDF für die Sichtprüfung als Bild ab, wenn <c>AISPA_PDF_SAMPLES</c> einen Ordner nennt (dokumente.md, Abschnitt 7); sonst nichts.</summary>
    public static byte[] Keep(byte[] pdf, string name)
    {
        if (Environment.GetEnvironmentVariable("AISPA_PDF_SAMPLES") is { Length: > 0 } folder)
        {
            Directory.CreateDirectory(folder);
            File.WriteAllBytes(Path.Combine(folder, $"{name}.pdf"), pdf);
        }

        return pdf;
    }

    /// <summary>Alle Textstücke aller Seiten, je Operator eine Zeile.</summary>
    public static string Text(byte[] pdf) => string.Join('\n', Pages(pdf));

    /// <summary>Der Text je Seite.</summary>
    public static IReadOnlyList<string> Pages(byte[] pdf)
    {
        using var document = Open(pdf);
        return [.. document.Pages.Cast<PdfPage>().Select(page => string.Join('\n', Strings(ContentReader.ReadContent(page))))];
    }

    /// <summary>Wie oft ein Operator (z. B. <c>re</c> für Rechtecke des QR-Codes) auf allen Seiten vorkommt.</summary>
    public static int CountOperators(byte[] pdf, string name)
    {
        using var document = Open(pdf);
        return document.Pages.Cast<PdfPage>().Sum(page => Operators(ContentReader.ReadContent(page)).Count(op => op.OpCode.Name == name));
    }

    private static IEnumerable<string> Strings(CSequence sequence) =>
        Operators(sequence)
            .Where(op => op.OpCode.Name is "Tj" or "TJ" or "'" or "\"")
            .Select(op => string.Concat(Flatten(op.Operands).OfType<CString>().Select(Decode)))
            .Where(text => text.Length > 0);

    private static IEnumerable<COperator> Operators(CSequence sequence)
    {
        foreach (var item in sequence)
        {
            if (item is COperator op)
            {
                yield return op;
            }
            else if (item is CSequence nested)
            {
                foreach (var inner in Operators(nested))
                {
                    yield return inner;
                }
            }
        }
    }

    private static IEnumerable<CObject> Flatten(CSequence operands)
    {
        foreach (var operand in operands)
        {
            if (operand is CArray array)
            {
                foreach (var inner in Flatten(array))
                {
                    yield return inner;
                }
            }
            else
            {
                yield return operand;
            }
        }
    }

    /// <summary>PDFsharp liefert jedes Byte der Zeichenkette als ein Zeichen; die Bytes sind WinAnsi.</summary>
    private static string Decode(CString value) => WinAnsi.GetString([.. value.Value.Select(c => (byte)c)]);
}
