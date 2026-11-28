using System.Net;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;
using AispaStudio.Desktop.ViewModels;

namespace AispaStudio.Desktop.Tests;

/// <summary>Kassieren ohne Verbindung: Ersatzbeleg am Gerät und Nacherfassung beim Abgleich (ADR 0096).</summary>
public sealed class PosSubstituteTests
{
    private sealed class SampleTime : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => SpaSample.At(11, 40);

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna");
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string ReenterRoute => $"POST /api/pos/registers/{SpaSample.Register.Id}/substitute-receipts";

    /// <summary>Kassa mit lokalem Stand, geladen mit Verbindung; danach ist der Server weg.</summary>
    private static async Task<(PosViewModel Pos, StubApi Stub, ApiClient Api, OfflineCache Cache)> OfflinePosAsync(bool practice = false)
    {
        var stub = new StubApi()
            .On("GET /api/locations", new List<LocationDto> { SpaSample.Vienna })
            .On("GET /api/pos/registers", new List<CashRegisterDto> { SpaSample.Register with { IsPractice = practice } })
            .On("GET /api/pos/articles", SpaSample.Articles)
            .On($"GET /api/pos/locations/{SpaSample.LocationId}/day-list?date=2026-10-19", SpaSample.DayList(SpaSample.UnbilledVisits()))
            .On($"GET /api/pos/registers/{SpaSample.Register.Id}/bills", new List<BillDto>());
        var cache = new OfflineCache(new MemoryOfflineStore(), new SampleTime());
        var api = stub.Client(cache);
        var pos = new PosViewModel(api, new SampleTime());
        await pos.InitializeAsync(SpaSample.LocationId);
        stub.Down = true;
        await api.GetAsync<List<LocationDto>>("api/locations", Ct);
        Assert.True(api.IsOffline);
        return (pos, stub, api, cache);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Ersatzbeleg_behält_das_Übungskennzeichen_im_lokalen_Stand_und_auf_dem_Bon(bool practice)
    {
        var (pos, _, _, cache) = await OfflinePosAsync(practice);
        await pos.AddAppointmentAsync(SpaSample.Paying);
        pos.FillRestCommand.Execute(pos.Payments.First(payment => payment.Method == PaymentMethod.Cash));
        await pos.PayCommand.ExecuteAsync(null);

        var receipt = Assert.Single(cache.Pending).Substitute!;
        Assert.Equal(practice, receipt.IsPractice);
        Assert.Equal(practice, pos.Receipt!.IsPractice);
        var narrow = PosViewModel.SubstituteText(receipt, TimeZoneInfo.Utc, "EUR", 24).Split('\n');
        var wide = PosViewModel.SubstituteText(receipt, TimeZoneInfo.Utc, "EUR", 42).Split('\n');
        Assert.Equal(practice, wide[0] == ReceiptViewModel.PracticeLabel);
        Assert.Equal(practice, narrow.Take(2).SequenceEqual(["ÜBUNGSKASSA", "kein gültiger Beleg"]));
        Assert.All(narrow, line => Assert.InRange(line.Length, 0, 24));
    }

    [Fact]
    public async Task Given_keine_Verbindung_When_Termin_und_Artikel_bar_bezahlt_Then_entsteht_ein_Ersatzbeleg_und_er_ist_zur_Nacherfassung_vorgemerkt()
    {
        // Given die Kassa ohne Verbindung, ein abgeschlossener Termin über 89,00 und ein Artikel
        var (pos, _, _, cache) = await OfflinePosAsync();

        // When der Termin und ein Artikel auf die Rechnung kommen
        await pos.AddAppointmentAsync(SpaSample.Paying);
        await pos.AddArticleCommand.ExecuteAsync(SpaSample.Articles[0]);

        // Then gibt es die Rechnung nur an diesem Gerät, mit dem Hinweis auf den Ersatzbeleg
        Assert.True(pos.IsSubstitute);
        Assert.Null(pos.Error);
        var expected = SpaSample.Paying.Price + SpaSample.Articles[0].Price;
        Assert.Equal(expected, pos.Bill!.Open);
        Assert.Equal(2, pos.Lines.Count);
        Assert.All(pos.Lines, l => Assert.Equal("inkl. USt", l.Tax));

        // When bar mit 5,00 Trinkgeld bezahlt wird
        pos.TipText = "5";
        var cash = pos.Payments.First(p => p.Method == PaymentMethod.Cash);
        cash.Amount = "200";
        Assert.True(pos.CanPay);
        await pos.PayCommand.ExecuteAsync(null);

        // Then zeigt die Kassa den Ersatzbeleg mit eigener Nummer, ohne Belegnummer der Kasse und ohne Signatur
        var receipt = pos.Receipt!;
        Assert.True(receipt.IsSubstitute);
        Assert.StartsWith("Ersatzbeleg E-WIEN-1-20261019-114000-", receipt.Title, StringComparison.Ordinal);
        Assert.Equal("Ersatzbeleg, der signierte Beleg folgt", receipt.SignatureText);
        Assert.False(receipt.CanAddRecipient);
        Assert.Equal(expected, receipt.Receipt.Total);
        Assert.Equal(200m - expected - 5m, receipt.Receipt.Change);
        Assert.Null(pos.Bill);
        Assert.False(pos.IsSubstitute);

        // And er ist mit Termin, Artikel, Summe und Trinkgeld zur Nacherfassung vorgemerkt
        var pending = Assert.Single(cache.Pending);
        Assert.Equal($"api/pos/registers/{SpaSample.Register.Id}/substitute-receipts", pending.Path);
        Assert.Equal(receipt.Receipt.SubstituteNumber, pending.Substitute!.SubstituteNumber);
        Assert.Contains(SpaSample.Paying.Id.ToString(), pending.Body, StringComparison.Ordinal);
        Assert.Contains(SpaSample.Articles[0].Id.ToString(), pending.Body, StringComparison.Ordinal);
        Assert.Contains("\"tip\":5", pending.Body, StringComparison.Ordinal);

        // And derselbe Termin lässt sich nicht noch einmal kassieren
        await pos.AddAppointmentAsync(SpaSample.Paying);
        Assert.Equal("Für diesen Termin gibt es schon einen Ersatzbeleg.", pos.Error);
        Assert.Null(pos.Bill);
    }

    [Fact]
    public async Task Given_Ersatzbeleg_vorgemerkt_When_die_Verbindung_zurueck_ist_Then_wird_er_nacherfasst_und_die_Vormerkung_ist_weg()
    {
        var (pos, stub, api, cache) = await OfflinePosAsync();
        await pos.AddAppointmentAsync(SpaSample.Paying);
        pos.FillRestCommand.Execute(pos.Payments.First(p => p.Method == PaymentMethod.Card));
        await pos.PayCommand.ExecuteAsync(null);
        Assert.Single(cache.Pending);

        stub.Down = false;
        stub.On(ReenterRoute, SpaSample.Receipt());
        var sent = await api.SyncAsync(Ct);

        Assert.Equal(1, sent);
        Assert.Empty(cache.Pending);
        Assert.Empty(cache.Failed);
        var body = stub.Calls.Single(c => c.Route == ReenterRoute).Body!;
        Assert.Contains("\"method\":\"Card\"", body, StringComparison.Ordinal);
        Assert.Contains("\"number\":\"E-WIEN-1-", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Given_die_Kasse_lehnt_die_Nacherfassung_ab_Then_bleibt_der_Ersatzbeleg_stehen_und_laesst_sich_nur_erneut_senden()
    {
        var (pos, stub, api, cache) = await OfflinePosAsync();
        await pos.AddAppointmentAsync(SpaSample.Paying);
        pos.FillRestCommand.Execute(pos.Payments.First(p => p.Method == PaymentMethod.Cash));
        await pos.PayCommand.ExecuteAsync(null);

        // When die Kasse ablehnt, weil der Termin inzwischen an einem anderen Gerät abgerechnet wurde
        stub.Down = false;
        stub.On(ReenterRoute, new { title = "Dieser Termin steht schon auf einer Rechnung." }, HttpStatusCode.Conflict);
        await api.SyncAsync(Ct);

        // Then steht er unter „nicht übernommen“ und lässt sich nicht abhaken: das Geld ist kassiert
        var failed = Assert.Single(cache.Failed);
        Assert.False(failed.CanDismiss);
        Assert.True(failed.CanRetry);
        Assert.NotNull(failed.Substitute);
        cache.DismissFailed(failed.Id);
        Assert.Single(cache.Failed);

        // When der Grund geklärt ist und erneut gesendet wird
        stub.On(ReenterRoute, SpaSample.Receipt());
        cache.Retry(failed.Id);
        Assert.Single(cache.Pending);
        await api.SyncAsync(Ct);

        // Then ist der Ersatzbeleg nacherfasst
        Assert.Empty(cache.Pending);
        Assert.Empty(cache.Failed);
    }

    [Fact]
    public async Task Given_Ersatzbelege_ausgegeben_Then_stehen_sie_unter_Belege_lassen_sich_zeigen_und_verschwinden_nach_der_Nacherfassung()
    {
        // Given zwei Ersatzbelege ohne Verbindung
        var (pos, stub, api, cache) = await OfflinePosAsync();
        Assert.False(pos.HasSubstitutes);
        await pos.AddAppointmentAsync(SpaSample.Paying);
        pos.FillRestCommand.Execute(pos.Payments.First(p => p.Method == PaymentMethod.Cash));
        await pos.PayCommand.ExecuteAsync(null);
        await pos.AddArticleCommand.ExecuteAsync(SpaSample.Articles[0]);
        pos.FillRestCommand.Execute(pos.Payments.First(p => p.Method == PaymentMethod.Card));
        await pos.PayCommand.ExecuteAsync(null);

        // Then stehen beide in der Liste, der älteste zuerst, mit Nummer, Betrag und Stand
        Assert.True(pos.HasSubstitutes);
        Assert.Equal("Ersatzbelege, noch nicht nacherfasst (2)", pos.SubstitutesTitle);
        Assert.All(pos.Substitutes, s => Assert.Equal(PosViewModel.SubstituteWaiting, s.State));
        Assert.All(pos.Substitutes, s => Assert.StartsWith("E-WIEN-1-", s.Number, StringComparison.Ordinal));
        Assert.Equal("89,00 €", pos.Substitutes[0].Total);

        // When einer gewählt wird, Then steht er rechts als Ersatzbeleg, ohne Storno und ohne Empfänger
        pos.SelectedSubstitute = pos.Substitutes[0];
        Assert.True(pos.HasShownReceipt);
        Assert.True(pos.SelectedReceipt!.IsSubstitute);
        Assert.False(pos.SelectedReceipt.CanVoid);
        Assert.False(pos.SelectedReceipt.CanAddRecipient);
        Assert.Null(pos.SelectedReceiptItem);

        // When die Kasse den zweiten nacherfasst hat und den ersten ablehnt
        stub.Down = false;
        stub.On(ReenterRoute, new { title = "Dieser Termin steht schon auf einer Rechnung." }, HttpStatusCode.Conflict);
        cache.Complete(cache.Pending[1].Id);
        await api.SyncAsync(Ct);

        // Then bleibt nur der abgelehnte stehen, mit dem Grund
        var rejected = Assert.Single(pos.Substitutes);
        Assert.True(rejected.IsRejected);
        Assert.Equal("nicht übernommen: Dieser Termin steht schon auf einer Rechnung.", rejected.State);
        Assert.Equal("Ersatzbelege, noch nicht nacherfasst (1)", pos.SubstitutesTitle);

        // When der Grund geklärt ist und erneut gesendet wird, Then ist die Liste leer und rechts steht nichts mehr
        stub.On(ReenterRoute, SpaSample.Receipt());
        stub.On($"GET /api/pos/registers/{SpaSample.Register.Id}/receipts?date=2026-10-19", new List<ReceiptDto>());
        await pos.RetrySubstituteCommand.ExecuteAsync(rejected);
        Assert.False(pos.HasSubstitutes);
        Assert.Null(pos.SelectedSubstitute);
        Assert.False(pos.HasShownReceipt);
    }

    [Fact]
    public async Task Given_keine_Verbindung_When_Eintritt_kassiert_wird_Then_steht_er_mit_dem_Preis_der_Tagesliste_auf_dem_Ersatzbeleg()
    {
        // Given die Tagesliste mit einem Eintritt für zwei Personen in die Sauna (58,00) und einem ohne Artikel am Bereich
        var (pos, _, _, cache) = await OfflinePosAsync();
        await pos.LoadDayAsync();
        var visits = SpaSample.UnbilledVisits();
        var sauna = visits[0];
        var relax = visits[1];

        // When der Eintritt in die Sauna auf die Rechnung kommt
        await pos.AddVisitAsync(sauna);

        // Then steht er mit Personenzahl und dem Preis der Tagesliste auf der Rechnung ohne Verbindung
        Assert.True(pos.IsSubstitute);
        Assert.Null(pos.Error);
        var line = Assert.Single(pos.Bill!.Lines);
        Assert.Equal((BillLineKind.AreaVisit, 2, sauna.Id), (line.Kind, line.Quantity, line.AreaVisitId));
        var price = SpaSample.DayEntry(sauna).Price!.Value;
        Assert.Equal(price, pos.Bill.Open);
        Assert.Equal(sauna.CustomerName, pos.Bill.CustomerName);

        // And ein Eintritt ohne bekannten Preis lässt sich ohne Verbindung nicht kassieren
        await pos.AddVisitAsync(relax);
        Assert.Equal("Der Preis dieses Eintritts ist ohne Verbindung nicht bekannt. Den Betrag notieren und mit Verbindung kassieren.", pos.Error);
        Assert.Single(pos.Bill.Lines);

        // When bezahlt wird, Then nennt die Vormerkung den Eintritt, und er lässt sich nicht noch einmal kassieren
        pos.FillRestCommand.Execute(pos.Payments.First(p => p.Method == PaymentMethod.Card));
        await pos.PayCommand.ExecuteAsync(null);
        var pending = Assert.Single(cache.Pending);
        Assert.Contains($"\"areaVisitIds\":[\"{sauna.Id}\"]", pending.Body, StringComparison.Ordinal);
        Assert.Equal(price, pending.Substitute!.Total);
        await pos.AddVisitAsync(sauna);
        Assert.Equal("Für diesen Eintritt gibt es schon einen Ersatzbeleg.", pos.Error);
    }

    [Fact]
    public async Task Given_keine_Verbindung_When_mit_Gutschein_bezahlt_werden_soll_Then_lehnt_die_Kassa_ab_und_parken_geht_nicht()
    {
        var (pos, _, _, cache) = await OfflinePosAsync();
        await pos.AddAppointmentAsync(SpaSample.Paying);

        var voucher = pos.Payments.First(p => p.IsVoucher);
        voucher.Amount = "89";
        voucher.VoucherCode = "K7M3-Q9XA-B2CD";
        await pos.PayCommand.ExecuteAsync(null);

        Assert.Equal(PosViewModel.SubstitutePaymentsOnly, pos.Error);
        Assert.Empty(cache.Pending);
        Assert.NotNull(pos.Bill);

        await pos.ParkBillCommand.ExecuteAsync(null);
        Assert.Equal("Ohne Verbindung lässt sich die Rechnung nicht parken. Bezahlen oder verwerfen.", pos.Error);
        Assert.NotNull(pos.Bill);

        await pos.DiscardBillCommand.ExecuteAsync(null);
        Assert.Null(pos.Bill);
        Assert.False(pos.IsSubstitute);
    }

    [Fact]
    public async Task Given_keine_Verbindung_und_kein_lokaler_Stand_Then_bleibt_es_beim_Hinweis_auf_die_Verbindung()
    {
        // Einzelplatz: kein lokaler Stand, also auch kein Ersatzbeleg
        var stub = new StubApi()
            .On("GET /api/locations", new List<LocationDto> { SpaSample.Vienna })
            .On("GET /api/pos/registers", new List<CashRegisterDto> { SpaSample.Register })
            .On("GET /api/pos/articles", SpaSample.Articles);
        var pos = new PosViewModel(stub.Client(), new SampleTime());
        await pos.InitializeAsync(SpaSample.LocationId);
        stub.Down = true;

        await pos.AddAppointmentAsync(SpaSample.Paying);

        Assert.False(pos.IsSubstitute);
        Assert.Null(pos.Bill);
        Assert.NotNull(pos.Error);
    }

    [Fact]
    public void Der_Bon_des_Ersatzbelegs_nennt_Nummer_Summe_Zahlung_und_dass_der_signierte_Beleg_folgt()
    {
        var receipt = SpaSample.Receipt() with { Number = 0, Tip = 5m, SubstituteNumber = "E-WIEN-1-20261019-114000-7F", SubstituteIssuedAt = SpaSample.At(11, 40) };

        var text = PosViewModel.SubstituteText(receipt, TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna"), "EUR", 42);

        Assert.Contains("ERSATZBELEG", text, StringComparison.Ordinal);
        Assert.Contains("E-WIEN-1-20261019-114000-7F", text, StringComparison.Ordinal);
        Assert.Contains("Summe inkl. USt", text, StringComparison.Ordinal);
        Assert.Contains("138,80", text, StringComparison.Ordinal);
        Assert.Contains("Trinkgeld (kein Umsatz)", text, StringComparison.Ordinal);
        Assert.Contains("Rückgeld", text, StringComparison.Ordinal);
        Assert.Contains("Der signierte Beleg wird nacherfasst", text, StringComparison.Ordinal);
        Assert.All(text.Split('\n'), line => Assert.True(line.Length <= 42, line));
    }
}
