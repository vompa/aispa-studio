using AispaStudio.Api.Documents;

namespace AispaStudio.Api.Tests;

/// <summary>
/// Dokument-Dienst (ADR 0091): Kopf, Tabellen mit Seitenumbruch und wiederholter Kopfzeile, Umlaute und Euro in der eingebetteten Schrift,
/// QR-Code als Rechtecke, Bon-Format mit wachsender Höhe. Erwartete Texte stehen von Hand in den Tests.
/// </summary>
public sealed class DocumentBuilderTests
{
    private static readonly TimeZoneInfo Vienna = TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna");

    private static readonly DocumentHeader Header = new("Day-Spa Wien", ["Wellness GmbH", "ATU12345678"], ["Standort Wien", "Am Graben 12, 1010 Wien"],
        new DateTimeOffset(2026, 11, 9, 9, 12, 0, TimeSpan.FromHours(1)), Vienna, "Lena");

    private static readonly DocumentColumn[] Columns = [new("Behandlung", 3), new("Gast", 2), new("Betrag", 1, DocumentAlign.Right)];

    [Fact]
    public void Eine_Tabelle_mit_80_Zeilen_bricht_um_und_wiederholt_die_Kopfzeile_auf_Seite_2()
    {
        var rows = Enumerable.Range(1, 80).Select(i => DocumentRow.Of($"Massage {i}", $"Gast {i}", DocumentBuilder.Money(i * 10.5m)));

        var pdf = PdfText.Keep(new DocumentBuilder(DocumentFormat.A4Portrait, Header).Title("Tagesplan").Table(Columns, rows, DocumentRow.Of("Summe", "", "1.234,56")).Build(), "builder-tabelle-a4");

        var pages = PdfText.Pages(pdf);
        Assert.InRange(pages.Count, 2, 3);
        var last = pages[^1];
        Assert.Contains("Behandlung", last, StringComparison.Ordinal);
        Assert.Contains("Day-Spa Wien", last, StringComparison.Ordinal);
        Assert.Contains($"Seite {pages.Count} von {pages.Count}", last, StringComparison.Ordinal);
        Assert.Contains("Seite 1 von", pages[0], StringComparison.Ordinal);
        Assert.Contains("Erstellt am 09.11.2026 09:12 von Lena", pages[0], StringComparison.Ordinal);
        Assert.Contains("Massage 80", last, StringComparison.Ordinal);
        Assert.Contains("1.234,56", last, StringComparison.Ordinal);
        Assert.DoesNotContain("Massage 80", pages[0], StringComparison.Ordinal);
    }

    [Fact]
    public void Umlaute_Euro_und_Gedankenstrich_stehen_lesbar_im_Text()
    {
        var pdf = new DocumentBuilder(DocumentFormat.A4Portrait, Header)
            .Title("Rechnung Nr. K1-142")
            .Paragraph("Müller – 5 € für Öl, Größe „L“")
            .KeyValues([("Gast", "Eva Muster"), ("Zahlart", "Bar")])
            .Build();

        var text = PdfText.Text(pdf);
        Assert.Contains("Rechnung Nr. K1-142", text, StringComparison.Ordinal);
        Assert.Contains("Müller – 5 € für Öl, Größe „L“", text, StringComparison.Ordinal);
        Assert.Contains("Eva Muster", text, StringComparison.Ordinal);
        Assert.Contains("Wellness GmbH", text, StringComparison.Ordinal);
        Assert.Contains("Am Graben 12, 1010 Wien", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Ein_QR_Code_wird_als_Rechtecke_gezeichnet()
    {
        var pdf = PdfText.Keep(new DocumentBuilder(DocumentFormat.A4Portrait, Header).Title("Gutschein").QrCode("K7M3-Q9XA-B2CD", 30).Build(), "builder-qr");

        Assert.True(PdfText.CountOperators(pdf, "re") > 100, "Ein QR-Code besteht aus hunderten Modulen.");
    }

    [Fact]
    public void Das_Bon_Format_ist_80_mm_breit_und_waechst_mit_dem_Inhalt()
    {
        var pdf = new DocumentBuilder(DocumentFormat.Receipt80, Header).Title("Beleg Nr. 7").Paragraph("Massage 79,00").Build();
        var longer = new DocumentBuilder(DocumentFormat.Receipt80, Header).Title("Beleg Nr. 8")
            .Table([new("Position", 3), new("Betrag", 1, DocumentAlign.Right)], Enumerable.Range(1, 50).Select(i => DocumentRow.Of($"Position {i}", "1,00")))
            .Build();
        PdfText.Keep(longer, "builder-bon");

        using var shortDocument = PdfText.Open(pdf);
        using var longDocument = PdfText.Open(longer);
        Assert.Equal(1, shortDocument.PageCount);
        Assert.Equal(1, longDocument.PageCount);
        Assert.Equal(226.8, shortDocument.Pages[0].Width.Point, 0.5);
        Assert.Equal(226.8, longDocument.Pages[0].Width.Point, 0.5);
        Assert.True(longDocument.Pages[0].Height.Point > shortDocument.Pages[0].Height.Point + 300, "50 Zeilen brauchen deutlich mehr Höhe als eine.");
        Assert.Contains("Position 50", PdfText.Text(longer), StringComparison.Ordinal);
    }

    [Fact]
    public void Querformat_ist_breiter_als_hoch_und_ein_Seitenumbruch_beginnt_eine_neue_Seite()
    {
        var pdf = new DocumentBuilder(DocumentFormat.A4Landscape, Header).Title("Dienstplan").Paragraph("Woche 46").PageBreak().Paragraph("Woche 47").Build();

        using var document = PdfText.Open(pdf);
        Assert.Equal(2, document.PageCount);
        Assert.True(document.Pages[0].Width.Point > document.Pages[0].Height.Point);
        var pages = PdfText.Pages(pdf);
        Assert.Contains("Woche 46", pages[0], StringComparison.Ordinal);
        Assert.Contains("Woche 47", pages[1], StringComparison.Ordinal);
        Assert.Contains("Seite 1 von 2", pages[0], StringComparison.Ordinal);
    }

    [Fact]
    public void Eine_Anzeigezeile_steht_gross_in_der_Mitte_des_Dokuments()
    {
        var pdf = PdfText.Keep(new DocumentBuilder(DocumentFormat.A4Portrait, Header).Display("Alles Gute", documentTitle: "Geschenkgutschein").Paragraph("Für Eva").Build(), "builder-anzeige");

        var text = PdfText.Text(pdf);
        Assert.Contains("Alles Gute", text, StringComparison.Ordinal);
        Assert.Contains("Für Eva", text, StringComparison.Ordinal);
        using var document = PdfText.Open(pdf);
        Assert.Equal("Geschenkgutschein", document.Info.Title);
    }

    [Fact]
    public void Gesperrte_Zellen_einer_Zeile_werden_grau_hinterlegt_die_anderen_nicht()
    {
        DocumentColumn[] columns = [new("Zeit", 1), new("Anna", 2), new("Ben", 2)];
        var plain = new DocumentBuilder(DocumentFormat.A4Landscape, Header).Table(columns, [DocumentRow.Of("12:00", "Pause", "Eva")]).Build();
        var shaded = PdfText.Keep(new DocumentBuilder(DocumentFormat.A4Landscape, Header).Table(columns, [new DocumentRow(["12:00", "Pause", "Eva"], MutedCells: [1])]).Build(), "builder-zellen-grau");

        Assert.Equal(PdfText.CountOperators(plain, "re") + 1, PdfText.CountOperators(shaded, "re"));
        Assert.Contains("Pause", PdfText.Text(shaded), StringComparison.Ordinal);
    }

    [Fact]
    public void Dateinamen_tragen_keine_verbotenen_Zeichen()
    {
        Assert.Equal("Rechnung-K1-142.pdf", DocumentFiles.PdfName("Rechnung", "K1-142"));
        Assert.Equal("Auskunft-Eva-Muster.pdf", DocumentFiles.PdfName("Auskunft", "Eva Muster"));
        Assert.Equal("Beleg-Müller-Söhne.pdf", DocumentFiles.PdfName("Beleg", "Müller/Söhne:"));
    }
}
