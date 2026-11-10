using System.Text;
using Microsoft.AspNetCore.Http.HttpResults;

namespace AispaStudio.Api.Documents;

/// <summary>PDF-Antwort eines Endpunkts: <c>application/pdf</c> mit sprechendem Dateinamen im <c>Content-Disposition</c>; die App übernimmt den Namen für den Speicherdialog.</summary>
internal static class DocumentFiles
{
    public const string PdfContentType = "application/pdf";

    /// <summary>Dateiname aus Bausteinen, z. B. „Rechnung-K1-142.pdf“: Buchstaben und Ziffern bleiben (auch Umlaute), alles andere wird ein Bindestrich.</summary>
    public static string PdfName(params string[] parts) => $"{string.Join('-', parts.Select(Clean).Where(p => p.Length > 0))}.pdf";

    public static FileContentHttpResult Pdf(byte[] pdf, string fileName) => TypedResults.File(pdf, PdfContentType, fileName);

    private static string Clean(string part)
    {
        var builder = new StringBuilder(part.Length);
        var dash = false;
        foreach (var c in part.Trim())
        {
            if (char.IsLetterOrDigit(c))
            {
                builder.Append(c);
                dash = false;
            }
            else if (!dash && builder.Length > 0)
            {
                builder.Append('-');
                dash = true;
            }
        }

        return builder.ToString().TrimEnd('-');
    }
}
