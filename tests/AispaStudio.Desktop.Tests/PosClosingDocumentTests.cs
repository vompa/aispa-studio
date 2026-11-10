using AispaStudio.Contracts;
using AispaStudio.Desktop.ViewModels;

namespace AispaStudio.Desktop.Tests;

/// <summary>
/// PDF-Ausgaben D (ADR 0092): Nach dem Tagesabschluss trägt die Abschlussmaske der Kassa den Knopfblock „Drucken · PDF speichern“ (ohne „Per Mail senden“); das PDF ist der
/// gespeicherte Abschluss, der Zwischenbericht hat keinen. Das Recht „Berichte mit Beträgen“ entscheidet, ob die Knöpfe gehen. Den Inhalt prüft die API (<c>DayClosingPdfTests</c>).
/// </summary>
public sealed class PosClosingDocumentTests
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

    private static readonly DayClosingDto Closed = new(Guid.NewGuid(), SpaSample.Register.Id, SpaSample.At(8), SpaSample.At(11, 40), 12, 1, 1384.60m,
        [new(PaymentMethod.Cash, 412.30m)], [new(TaxCategory.Normal, 20, 1384.60m, 1153.83m, 230.77m)], [], 150m, 0, 100m, 462.30m, 460.30m, -2m, "Münzen verlegt");

    private static StubApi Api() => new StubApi()
        .On("GET /api/locations", new List<LocationDto> { SpaSample.Vienna })
        .On("GET /api/pos/registers", new List<CashRegisterDto> { SpaSample.Register })
        .On("GET /api/pos/articles", SpaSample.Articles)
        .On($"GET /api/pos/locations/{SpaSample.LocationId}/day-list?date=2026-10-19", SpaSample.DayList())
        .On($"GET /api/pos/registers/{SpaSample.Register.Id}/bills", new List<BillDto>())
        .On($"GET /api/pos/registers/{SpaSample.Register.Id}/day-report", Closed with { Id = Guid.Empty, CountedCash = 0, Difference = 0, Note = null })
        .On($"POST /api/pos/registers/{SpaSample.Register.Id}/day-closings", Closed)
        .OnFile($"GET /api/pos/registers/{SpaSample.Register.Id}/day-closings/{Closed.Id}/pdf", Pdf, "Tagesabschluss-K1-2026-10-19.pdf");

    private static async Task<PosViewModel> ClosedDayAsync(bool mayReadAmounts = true)
    {
        var pos = new PosViewModel(Api().Client(), new SampleTime()) { MaySeeAmountReports = mayReadAmounts, Dialogs = new FakeDialogs().Answer(ConfirmChoice.Confirmed) };
        await pos.InitializeAsync(SpaSample.LocationId);
        await pos.ShowClosingCommand.ExecuteAsync(null);
        pos.CountedCash = "460,30";
        await pos.CloseDayCommand.ExecuteAsync(null);
        return pos;
    }

    [Fact]
    public void Der_Knopfblock_zeigt_auf_das_PDF_des_gespeicherten_Abschlusses_ohne_Versand()
    {
        var source = PosViewModel.ClosingDocumentSource(Closed, mayReadAmounts: true);

        Assert.Equal($"api/pos/registers/{SpaSample.Register.Id}/day-closings/{Closed.Id}/pdf", source.PdfPath);
        Assert.Null(source.SendPath);
        Assert.Null(source.AlternativePdfPath);
        Assert.Equal("Tagesabschluss", source.Title);
        Assert.True(source.IsAllowed);
    }

    [Fact]
    public void Ohne_das_Recht_Berichte_mit_Betraegen_ist_der_Knopfblock_gesperrt_mit_Hinweis()
    {
        var source = PosViewModel.ClosingDocumentSource(Closed, mayReadAmounts: false);

        Assert.False(source.IsAllowed);
        Assert.Contains("Berichte mit Beträgen", source.DisabledHint, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Nach_dem_Abschluss_gibt_es_den_Knopfblock_und_vorher_nicht()
    {
        var pos = new PosViewModel(Api().Client(), new SampleTime()) { MaySeeAmountReports = true };
        await pos.InitializeAsync(SpaSample.LocationId);
        await pos.ShowClosingCommand.ExecuteAsync(null);
        Assert.Null(pos.ClosingDocument);

        pos = await ClosedDayAsync();

        Assert.True(pos.IsClosed);
        Assert.NotNull(pos.ClosingDocument);
        Assert.False(pos.ClosingDocument.HasSend);
    }

    [Fact]
    public async Task Speichern_holt_das_PDF_und_meldet_es_als_Toast_der_Kassa()
    {
        var pos = await ClosedDayAsync();
        var files = new RecordingFileSaver();
        pos.ClosingDocument!.Files = files;

        await pos.ClosingDocument.SaveCommand.ExecuteAsync(null);

        Assert.Equal("Tagesabschluss-K1-2026-10-19.pdf", files.SavedName);
        Assert.Equal("Gespeichert: Tagesabschluss-K1-2026-10-19.pdf", pos.Toast);
    }

    [Fact]
    public async Task Ohne_Recht_gehen_die_Knoepfe_nicht()
    {
        var pos = await ClosedDayAsync(mayReadAmounts: false);

        Assert.False(pos.ClosingDocument!.SaveCommand.CanExecute(null));
        Assert.False(pos.ClosingDocument.PrintCommand.CanExecute(null));
    }

    [Fact]
    public async Task Der_naechste_Zwischenbericht_blendet_den_Knopfblock_aus()
    {
        var pos = await ClosedDayAsync();

        await pos.ShowClosingCommand.ExecuteAsync(null);

        Assert.False(pos.IsClosed);
        Assert.Null(pos.ClosingDocument);
    }
}
