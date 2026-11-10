using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;
using AispaStudio.Desktop.ViewModels;

namespace AispaStudio.Desktop.Tests;

/// <summary>
/// PDF-Ausgaben, Zusatz (ADR 0092): Die Karte „Gutscheinliste …“ der Kassa baut den Pfad der API aus Zeitraum, Status und den zwei Schaltern, sperrt den Knopfblock ohne das Recht und bei
/// verkehrtem Zeitraum, und der Gutschein meldet Druck und Übergabe an die API, damit die Liste den Versandvermerk zeigt. Den Inhalt der Liste prüft die API (<c>VoucherListPdfTests</c>).
/// </summary>
public sealed class PosVoucherListTests
{
    private static readonly byte[] Pdf = [0x25, 0x50, 0x44, 0x46, 9];

    private static readonly VoucherDto Gift = new(Guid.NewGuid(), "K7QM-2XPA-9RDT", VoucherKind.MultiPurpose, 50m, 50m, null, VoucherStatus.Active, null);

    private static string BasePath => $"api/pos/locations/{SpaSample.LocationId}/vouchers/pdf";

    private sealed class SampleTime : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => SpaSample.At(11, 40);

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna");
    }

    private sealed class FakePrinter(string? problem = null) : IDocumentPrinter
    {
        public Task<string?> PrintAsync(string fileName, byte[] pdf, CancellationToken ct) => Task.FromResult(problem);
    }

    private static VoucherListPrintViewModel Card(bool allowed = true, StubApi? api = null) =>
        new(SpaSample.LocationId, allowed, (api ?? new StubApi()).Client(), null, _ => { }, () => { });

    private static async Task<PosViewModel> PosAsync(StubApi api, bool supervise = false, bool amounts = false)
    {
        var pos = new PosViewModel(api.Client(), new SampleTime()) { MaySuperviseCash = supervise, MaySeeAmountReports = amounts };
        await pos.InitializeAsync(SpaSample.LocationId);
        return pos;
    }

    private static StubApi PosApi() => new StubApi()
        .On("GET /api/locations", new List<LocationDto> { SpaSample.Vienna })
        .On("GET /api/pos/registers", new List<CashRegisterDto> { SpaSample.Register })
        .On("GET /api/pos/articles", SpaSample.Articles)
        .On($"GET /api/pos/locations/{SpaSample.LocationId}/day-list?date=2026-10-19", SpaSample.DayList())
        .On($"GET /api/pos/registers/{SpaSample.Register.Id}/bills", new List<BillDto>());

    [Fact]
    public void Ohne_Wahl_zeigt_der_Pfad_auf_alle_Gutscheine_des_Standorts()
    {
        var card = Card();

        Assert.Equal(BasePath, card.Path);
        Assert.Equal(BasePath, card.Document.Source.PdfPath);
        Assert.Null(card.Document.Source.SendPath);
        Assert.Equal("Gutscheinliste", card.Document.Source.Title);
        Assert.True(card.Document.IsAllowed);
    }

    [Fact]
    public void Jede_Wahl_steht_in_der_Abfrage_und_ersetzt_den_Knopfblock()
    {
        var card = Card();
        var before = card.Document;

        card.From = new DateOnly(2026, 10, 1);
        card.Until = new DateOnly(2026, 10, 31);
        card.SelectedStatus = VoucherListPrintViewModel.StatusChoices.Single(c => c.Label == "abgelaufen");
        card.Expiring = true;
        card.NotSent = true;

        Assert.Equal($"{BasePath}?from=2026-10-01&until=2026-10-31&status=expired&expiring=true&notSent=true", card.Path);
        Assert.Equal(card.Path, card.Document.Source.PdfPath);
        Assert.NotSame(before, card.Document);

        card.SelectedStatus = VoucherListPrintViewModel.StatusChoices[0];
        card.Expiring = false;
        card.NotSent = false;
        card.From = card.Until = null;
        Assert.Equal(BasePath, card.Path);
    }

    [Fact]
    public void Ein_Ende_vor_dem_Beginn_sperrt_den_Knopfblock_mit_Hinweis()
    {
        var card = Card();

        card.From = new DateOnly(2026, 11, 1);
        card.Until = new DateOnly(2026, 10, 1);

        Assert.True(card.HasRangeError);
        Assert.False(card.Document.IsAllowed);
        Assert.Equal("Das Ende darf nicht vor dem Beginn liegen.", card.Document.DisabledHint);

        card.Until = new DateOnly(2026, 11, 30);
        Assert.False(card.HasRangeError);
        Assert.True(card.Document.IsAllowed);
    }

    [Fact]
    public void Ohne_das_Recht_ist_der_Knopfblock_gesperrt_mit_Hinweis()
    {
        var card = Card(allowed: false);

        Assert.False(card.Document.IsAllowed);
        Assert.Contains("Kassenaufsicht", card.Document.DisabledHint, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    [InlineData(false, false, false)]
    public async Task Kassenaufsicht_oder_Berichte_mit_Betraegen_duerfen_die_Liste_drucken(bool supervise, bool amounts, bool expected)
    {
        var pos = await PosAsync(PosApi(), supervise, amounts);

        pos.ToggleVoucherListCommand.Execute(null);

        Assert.NotNull(pos.VoucherList);
        Assert.Equal(expected, pos.MayPrintVoucherList);
        Assert.Equal(expected, pos.VoucherList.Document.IsAllowed);
        Assert.Equal(SpaSample.LocationId, pos.Register!.LocationId);
        Assert.Equal(BasePath, pos.VoucherList.Path);
    }

    [Fact]
    public async Task Die_Karte_geht_mit_demselben_Knopf_oder_ihrem_Schliessen_wieder_zu()
    {
        var pos = await PosAsync(PosApi(), supervise: true);

        pos.ToggleVoucherListCommand.Execute(null);
        Assert.NotNull(pos.VoucherList);
        pos.VoucherList.CloseCommand.Execute(null);
        Assert.Null(pos.VoucherList);

        pos.ToggleVoucherListCommand.Execute(null);
        pos.ToggleVoucherListCommand.Execute(null);
        Assert.Null(pos.VoucherList);
    }

    [Fact]
    public async Task Ein_erfolgreicher_Druck_des_Gutscheins_wird_an_die_API_vermerkt()
    {
        var api = new StubApi().OnFile($"GET /api/pos/vouchers/{Gift.Id}/pdf", Pdf, "Gutschein.pdf").On($"POST /api/pos/vouchers/{Gift.Id}/printed", null, System.Net.HttpStatusCode.NoContent);
        var actions = new DocumentActionsViewModel(PosViewModel.VoucherDocumentSource(Gift), api.Client(), null, new FakePrinter());

        await actions.PrintCommand.ExecuteAsync(null);

        Assert.Equal($"api/pos/vouchers/{Gift.Id}/printed", actions.Source.PrintedPath);
        Assert.Contains(api.Calls, c => c.Route == $"POST /api/pos/vouchers/{Gift.Id}/printed");
        Assert.False(actions.HasError);
    }

    [Fact]
    public async Task Ein_gescheiterter_Druck_wird_nicht_vermerkt()
    {
        var api = new StubApi().OnFile($"GET /api/pos/vouchers/{Gift.Id}/pdf", Pdf, "Gutschein.pdf");
        var actions = new DocumentActionsViewModel(PosViewModel.VoucherDocumentSource(Gift), api.Client(), null, new FakePrinter("Kein Drucker eingerichtet."));

        await actions.PrintCommand.ExecuteAsync(null);

        Assert.DoesNotContain(api.Calls, c => c.Route.StartsWith("POST", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Ein_Dokument_ohne_Druckpfad_vermerkt_nichts()
    {
        var api = new StubApi().OnFile("GET /api/x/pdf", Pdf, "x.pdf");
        var actions = new DocumentActionsViewModel(new DocumentSource("x", "api/x/pdf"), api.Client(), null, new FakePrinter());

        await actions.PrintCommand.ExecuteAsync(null);

        Assert.DoesNotContain(api.Calls, c => c.Route.StartsWith("POST", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Uebergeben_vermerkt_die_Uebergabe_und_meldet_es_als_Toast()
    {
        var api = PosApi().On($"POST /api/pos/vouchers/{Gift.Id}/handed-over", null, System.Net.HttpStatusCode.NoContent);
        var pos = await PosAsync(api);
        var item = pos.ReceiptFor(SpaSample.Receipt() with { IssuedVouchers = [Gift] }, copy: false).VoucherDocuments[0];

        await item.HandOverCommand.ExecuteAsync(null);

        Assert.Contains(api.Calls, c => c.Route == $"POST /api/pos/vouchers/{Gift.Id}/handed-over");
        Assert.Equal($"Gutschein {Gift.Code} als übergeben vermerkt", pos.Toast);
    }

    [Fact]
    public async Task Scheitert_der_Vermerk_der_Uebergabe_steht_der_Fehler_in_der_Kassa()
    {
        var api = PosApi().On($"POST /api/pos/vouchers/{Gift.Id}/handed-over", null, System.Net.HttpStatusCode.NotFound);
        var pos = await PosAsync(api);
        var item = pos.ReceiptFor(SpaSample.Receipt() with { IssuedVouchers = [Gift] }, copy: false).VoucherDocuments[0];

        await item.HandOverCommand.ExecuteAsync(null);

        Assert.NotNull(pos.Error);
        Assert.Null(pos.Toast);
    }
}
