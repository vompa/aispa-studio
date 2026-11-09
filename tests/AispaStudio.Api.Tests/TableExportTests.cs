using System.Globalization;
using System.Text;
using AispaStudio.Api.Exports;
using AispaStudio.Contracts;
using ClosedXML.Excel;

namespace AispaStudio.Api.Tests;

/// <summary>Gemeinsamer Export-Dienst (Export/Import, Aufgabe A): eine Tabelle wird zu Excel oder CSV, beide Dateien werden zurückgelesen.</summary>
public sealed class TableExportTests
{
    private static ExportTable Sample() => new("Kunden",
    [
        new("Nachname", ColumnKind.Text),
        new("Geburtstag", ColumnKind.Date),
        new("Beginn", ColumnKind.Time),
        new("Betrag", ColumnKind.Money),
        new("Besuche", ColumnKind.Number),
        new("Werbung", ColumnKind.YesNo),
    ],
    [
        ["Müller-Öhlinger", new DateOnly(1985, 3, 14), new TimeOnly(14, 30), 1234.5m, 7, true],
        ["Huber; \"Steffi\"", null, null, 0.1m, 0, false],
    ]);

    [Fact]
    public void Excel_hat_fette_Kopfzeile_Autofilter_Typen_und_Umlaute()
    {
        var bytes = TableExport.ToExcel(Sample());
        SampleFiles.Save("Kunden_2026-11-09.xlsx", bytes);

        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        var sheet = Assert.Single(workbook.Worksheets);
        Assert.Equal("Kunden", sheet.Name);
        Assert.Equal("Nachname", sheet.Cell(1, 1).GetString());
        Assert.True(sheet.Cell(1, 1).Style.Font.Bold);
        Assert.True(sheet.AutoFilter.IsEnabled);
        Assert.Equal("Müller-Öhlinger", sheet.Cell(2, 1).GetString());
        Assert.Equal(XLDataType.DateTime, sheet.Cell(2, 2).DataType);
        Assert.Equal(new DateTime(1985, 3, 14), sheet.Cell(2, 2).GetDateTime());
        Assert.Equal(XLDataType.TimeSpan, sheet.Cell(2, 3).DataType);
        Assert.Equal(new TimeSpan(14, 30, 0), sheet.Cell(2, 3).GetTimeSpan());
        Assert.Equal(XLDataType.Number, sheet.Cell(2, 4).DataType);
        Assert.Equal(1234.5, sheet.Cell(2, 4).GetDouble());
        Assert.Equal("#,##0.00", sheet.Cell(2, 4).Style.NumberFormat.Format);
        Assert.Equal(7, sheet.Cell(2, 5).GetDouble());
        Assert.Equal("Ja", sheet.Cell(2, 6).GetString());
        Assert.Equal("Nein", sheet.Cell(3, 6).GetString());
        Assert.True(sheet.Cell(3, 2).IsEmpty());
        Assert.True(sheet.Column(1).Width > sheet.Column(5).Width, "Spaltenbreite folgt dem Inhalt");
    }

    [Fact]
    public void Csv_hat_BOM_Strichpunkt_Anfuehrungszeichen_und_Komma_im_Betrag()
    {
        var bytes = TableExport.ToCsv(Sample());
        SampleFiles.Save("Kunden_2026-11-09.csv", bytes);

        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bytes[..3]);
        var lines = Encoding.UTF8.GetString(bytes[3..]).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal("Nachname;Geburtstag;Beginn;Betrag;Besuche;Werbung", lines[0]);
        Assert.Equal("Müller-Öhlinger;14.03.1985;14:30;1234,50;7;Ja", lines[1]);
        Assert.Equal("\"Huber; \"\"Steffi\"\"\";;;0,10;0;Nein", lines[2]);
        Assert.Equal(3, lines.Length);
    }

    [Fact]
    public void Dateiname_ist_sprechend_ohne_Umlaute_mit_Datum()
    {
        Assert.Equal("Kunden_2026-11-09.xlsx", ExportFiles.Name("Kunden", ExportFormat.Xlsx, new DateOnly(2026, 11, 9)));
        Assert.Equal("Belege_Maerz_2026-11-09.csv", ExportFiles.Name("Belege März", ExportFormat.Csv, new DateOnly(2026, 11, 9)));
        Assert.Equal("Termin_2026-11-12_1400.ics", ExportFiles.Name("Termin", ExportFormat.Ics, new DateOnly(2026, 11, 12), new TimeOnly(14, 0)));
        Assert.Equal("Kontakt_Oehlinger.vcf", ExportFiles.Name("Kontakt Öhlinger", ExportFormat.Vcf));
    }

    [Fact]
    public void Mehr_als_50000_Zeilen_werden_abgelehnt()
    {
        var rows = Enumerable.Range(0, TableExport.MaxRows + 1).Select(i => new object?[] { i.ToString(CultureInfo.InvariantCulture) }).ToList();
        var table = new ExportTable("Zu groß", [new("Nr", ColumnKind.Text)], rows);

        Assert.True(TableExport.TooLarge(table));
        Assert.False(TableExport.TooLarge(Sample()));
        Assert.Contains("50.000", TableExport.TooLargeMessage, StringComparison.Ordinal);
    }
}
