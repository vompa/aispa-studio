using AispaStudio.Contracts;
using AispaStudio.Desktop.ViewModels;

namespace AispaStudio.Desktop.Tests;

/// <summary>
/// PDF-Ausgaben B (ADR 0092): Der Beleg in der Kassa trägt den Knopfblock „Drucken · PDF speichern · Per Mail senden“. A4 ist der Standard, das Bon-Format die zweite Wahl,
/// der Nachdruck aus dem Reiter „Belege“ trägt „Kopie“, und gesendete Mails stehen als Vermerk unter dem Beleg. Inhalt des PDFs prüft die API (<c>ReceiptPdfTests</c>).
/// </summary>
public sealed class PosReceiptDocumentTests
{
    private static readonly byte[] Pdf = [0x25, 0x50, 0x44, 0x46, 9];

    private sealed class SampleTime : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => SpaSample.At(11, 40);

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna");
    }

    private sealed class RecordingFileSaver : IFileSaver
    {
        public string? SavedName { get; private set; }

        public Task<bool> SaveAsync(string suggestedName, string typeName, IReadOnlyList<string> patterns, byte[] content)
        {
            SavedName = suggestedName;
            return Task.FromResult(true);
        }
    }

    private static readonly ReceiptDto Sent = SpaSample.Receipt() with
    {
        SentMails = [new SentDocumentDto("eva@example.at", SpaSample.At(11, 45), "Anna")],
    };

    private static StubApi Api() => new StubApi()
        .On("GET /api/locations", new List<LocationDto> { SpaSample.Vienna })
        .On("GET /api/pos/registers", new List<CashRegisterDto> { SpaSample.Register })
        .On("GET /api/pos/articles", SpaSample.Articles)
        .On($"GET /api/pos/locations/{SpaSample.LocationId}/day-list?date=2026-10-19", SpaSample.DayList())
        .On($"GET /api/pos/registers/{SpaSample.Register.Id}/bills", new List<BillDto>())
        .On($"GET /api/pos/registers/{SpaSample.Register.Id}/receipts?date=2026-10-19", new List<ReceiptDto> { Sent })
        .OnFile($"GET /api/pos/receipts/{Sent.Id}/pdf?format=Receipt&copy=true", Pdf, "Beleg-K1-1842.pdf");

    private static async Task<PosViewModel> OpenReceiptsAsync()
    {
        var pos = new PosViewModel(Api().Client(), new SampleTime());
        await pos.InitializeAsync(SpaSample.LocationId);
        await pos.ShowReceiptsCommand.ExecuteAsync(null);
        return pos;
    }

    [Fact]
    public void Der_frische_Beleg_kommt_als_A4_ohne_Kopie_und_das_Bon_Format_ist_die_zweite_Wahl()
    {
        var source = PosViewModel.ReceiptDocumentSource(Sent, copy: false);

        Assert.Equal($"api/pos/receipts/{Sent.Id}/pdf", source.PdfPath);
        Assert.Equal($"api/pos/receipts/{Sent.Id}/pdf?format=Receipt", source.AlternativePdfPath);
        Assert.Equal($"api/pos/receipts/{Sent.Id}/send", source.SendPath);
        Assert.Equal("Beleg Nr. 1842", source.Title);
    }

    [Fact]
    public void Der_Nachdruck_traegt_in_beiden_Formaten_Kopie()
    {
        var source = PosViewModel.ReceiptDocumentSource(Sent, copy: true);

        Assert.Equal($"api/pos/receipts/{Sent.Id}/pdf?copy=true", source.PdfPath);
        Assert.Equal($"api/pos/receipts/{Sent.Id}/pdf?format=Receipt&copy=true", source.AlternativePdfPath);
    }

    [Fact]
    public async Task Der_Beleg_im_Reiter_Belege_zeigt_den_Vermerk_der_gesendeten_Mail()
    {
        var pos = await OpenReceiptsAsync();

        var receipt = pos.SelectedReceipt;

        Assert.NotNull(receipt?.Document);
        Assert.True(receipt.HasSentMails);
        Assert.Equal(["Per Mail gesendet an eva@example.at am 19.10.2026 11:45 von Anna"], receipt.SentMailLines);
    }

    [Fact]
    public async Task Speichern_im_Bon_Format_holt_die_Kopie_und_meldet_es_als_Toast_der_Kassa()
    {
        var pos = await OpenReceiptsAsync();
        var files = new RecordingFileSaver();
        var document = pos.SelectedReceipt!.Document!;
        document.Files = files;

        await document.SaveAlternativeCommand.ExecuteAsync(null);

        Assert.Equal("Beleg-K1-1842.pdf", files.SavedName);
        Assert.Equal("Gespeichert: Beleg-K1-1842.pdf", pos.Toast);
        pos.CloseToastCommand.Execute(null);
        Assert.False(pos.HasToast);
    }

    [Fact]
    public void Ohne_gesendete_Mail_gibt_es_keinen_Vermerk()
    {
        var receipt = new ReceiptViewModel(SpaSample.Receipt(), TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna"), FiscalCountry.AT);

        Assert.False(receipt.HasSentMails);
        Assert.Empty(receipt.SentMailLines);
    }
}
