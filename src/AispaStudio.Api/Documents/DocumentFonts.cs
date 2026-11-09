using PdfSharp.Fonts;

namespace AispaStudio.Api.Documents;

/// <summary>
/// Die eingebettete Schrift der Dokumente: Liberation Sans 2.1.5 (SIL Open Font License 1.1, <c>Fonts/LICENSE-LiberationSans.txt</c>), Regular und Bold,
/// mit Umlauten, Euro und Gedankenstrich. Keine Systemschrift: die API läuft in Linux-Containern ohne Schriften (ADR 0091).
/// </summary>
internal sealed class DocumentFonts : IFontResolver
{
    public const string FamilyName = "AispaDocument";

    private const string RegularFace = "LiberationSans-Regular";
    private const string BoldFace = "LiberationSans-Bold";

    private static readonly Lock Gate = new();
    private static readonly Lazy<byte[]> Regular = new(() => Load("LiberationSans-Regular.ttf"));
    private static readonly Lazy<byte[]> Bold = new(() => Load("LiberationSans-Bold.ttf"));

    /// <summary>Meldet die Schrift einmal je Prozess bei PDFsharp an; PDFsharp erlaubt nur einen Auflöser.</summary>
    public static void Ensure()
    {
        lock (Gate)
        {
            if (GlobalFontSettings.FontResolver is not DocumentFonts)
            {
                GlobalFontSettings.FontResolver = new DocumentFonts();
            }
        }
    }

    public FontResolverInfo? ResolveTypeface(string familyName, bool bold, bool italic) =>
        string.Equals(familyName, FamilyName, StringComparison.OrdinalIgnoreCase) ? new FontResolverInfo(bold ? BoldFace : RegularFace) : null;

    public byte[]? GetFont(string faceName) => faceName switch
    {
        RegularFace => Regular.Value,
        BoldFace => Bold.Value,
        _ => null,
    };

    private static byte[] Load(string fileName)
    {
        using var stream = typeof(DocumentFonts).Assembly.GetManifestResourceStream($"Fonts/{fileName}")
            ?? throw new InvalidOperationException($"Die Schrift {fileName} fehlt in der Assembly.");
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }
}
