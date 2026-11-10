using System.Net;
using AispaStudio.Contracts;
using AispaStudio.Desktop.ViewModels;
using static AispaStudio.Desktop.Tests.StockSample;

namespace AispaStudio.Desktop.Tests;

/// <summary>
/// PDF-Ausgaben F (ADR 0092): Die Masken des Betreibers tragen den Knopfblock für ihr Dokument: Dienstplan („Aushang als PDF“), Zeiten einer Person („Stundenliste als PDF“), Inventur
/// („Zählliste“ und „Inventurliste“), Bestellungen (mit Senden an den Lieferanten und dem Vermerk „Gesendet am … an …“) und die Ablauf-Aufgaben („Ablaufliste als PDF“). Den Inhalt der PDFs
/// prüft die API (<c>RosterPdfTests</c>, <c>TimesheetPdfTests</c>, <c>StockCountPdfTests</c>, <c>PurchaseOrderPdfTests</c>, <c>PurchaseOrderSendTests</c>, <c>ExpiringStockPdfTests</c>).
/// </summary>
public sealed class OperatorDocumentsTests
{
    private static readonly byte[] Pdf = [0x25, 0x50, 0x44, 0x46, 9];

    private static readonly Guid CabinetSessionId = Guid.Parse("00000000-0000-0000-0000-0000000c1701");

    private static readonly StockCountSessionDto CabinetSession = new(CabinetSessionId, Vienna.Id, CabinetId, "Schrank Zeder", StockCountSessionStatus.Submitted, "Lena Huber",
        new DateTimeOffset(2027, 2, 28, 8, 30, 0, TimeSpan.Zero), new DateTimeOffset(2027, 2, 28, 8, 50, 0, TimeSpan.Zero), null,
        [new StockCountLineDto(OilId, "Massageöl", "Flasche", 8m, 10m)]);

    private sealed class FixedTime : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => TestData.At(10);

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

    private static async Task<StockViewModel> Stock(StubApi api, CurrentUser? user = null)
    {
        var stock = new StockViewModel(api.Client(), user ?? TestData.Owner(), new FixedTime()) { Dialogs = new FakeDialogs() };
        await stock.EnsureLoadedAsync();
        return stock;
    }

    // ---------- Dienstplan ----------

    private static StubApi RosterApi()
    {
        var (api, _) = RosterTests.Setup();
        return api
            .OnFile($"GET /api/locations/{RosterSample.Vienna.Id}/roster/pdf?week=2027-03-01", Pdf, "Dienstplan-Wien-2027-03-01.pdf")
            .OnFile($"GET /api/locations/{RosterSample.Vienna.Id}/roster/pdf?week=2027-03-08", Pdf, "Dienstplan-Wien-2027-03-08.pdf");
    }

    private static async Task<RosterPlanViewModel> Plan(TeamViewModel team)
    {
        Assert.True(await team.SwitchToAsync(TeamViewModel.RosterMode));
        await RosterTests.Until(() => team.Plan!.IsLoaded);
        return team.Plan!;
    }

    [Fact]
    public async Task Der_Aushang_oeffnet_den_Knopfblock_fuer_die_Woche_ohne_Senden_und_folgt_dem_Wochenwechsel()
    {
        var team = await RosterTests.Loaded(RosterApi());
        var plan = await Plan(team);
        Assert.Null(plan.Document);

        plan.ToggleDocumentCommand.Execute(null);

        var document = plan.Document!;
        Assert.Equal($"api/locations/{RosterSample.Vienna.Id}/roster/pdf?week=2027-03-01", document.Source.PdfPath);
        Assert.False(document.HasSend);
        Assert.True(document.IsAllowed);
        Assert.Contains("01.03. – 07.03.2027", plan.DocumentHint, StringComparison.Ordinal);

        await plan.MoveToAsync(RosterSample.Monday.AddDays(7));

        Assert.EndsWith("week=2027-03-08", plan.Document!.Source.PdfPath, StringComparison.Ordinal);
        Assert.Contains("08.03. – 14.03.2027", plan.DocumentHint, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Speichern_des_Aushangs_nimmt_den_Dateinamen_der_API_und_meldet_ihn_unter_den_Knoepfen()
    {
        var team = await RosterTests.Loaded(RosterApi());
        var plan = await Plan(team);
        plan.ToggleDocumentCommand.Execute(null);
        var files = new RecordingFileSaver();
        plan.Document!.Files = files;

        await plan.Document.SaveCommand.ExecuteAsync(null);

        Assert.Equal("Dienstplan-Wien-2027-03-01.pdf", files.SavedName);
        Assert.Equal("Gespeichert: Dienstplan-Wien-2027-03-01.pdf", plan.DocumentMessage);
    }

    [Fact]
    public async Task Ein_zweiter_Klick_schliesst_den_Aushang_und_auf_der_Karte_einer_Person_gibt_es_ihn_nicht()
    {
        var api = RosterApi();
        var team = await RosterTests.Loaded(api);
        var plan = await Plan(team);
        Assert.True(plan.CanOfferDocument);

        plan.ToggleDocumentCommand.Execute(null);
        plan.ToggleDocumentCommand.Execute(null);

        Assert.Null(plan.Document);
        team.SelectedEntry = team.Entries.Single(e => e.Title == "Lena Huber");
        await RosterTests.Until(() => team.Card?.Entry.Title == "Lena Huber");
        team.Card!.SelectTab(PersonCardViewModel.RosterTab);
        await RosterTests.Until(() => team.Card.Roster!.IsLoaded);
        Assert.False(team.Card.Roster!.CanOfferDocument);
    }

    [Fact]
    public async Task Ohne_das_Recht_zu_planen_ist_der_Aushang_gesperrt_mit_Hinweis()
    {
        var team = await RosterTests.Loaded(RosterApi(), TestData.Reception());
        var plan = await Plan(team);

        plan.ToggleDocumentCommand.Execute(null);

        Assert.False(plan.Document!.IsAllowed);
        Assert.Equal("Den Aushang druckt nur die Standortleitung.", plan.Document.DisabledHint);
        Assert.False(plan.Document.SaveCommand.CanExecute(null));
    }

    // ---------- Stundenliste ----------

    [Fact]
    public async Task Die_Stundenliste_gibt_es_auf_der_Karte_einer_Person_fuer_den_Monat_der_Woche()
    {
        var (api, _) = RosterTests.Setup();
        api.OnFile($"GET /api/employees/{RosterSample.Lena.Id}/timesheet/pdf?month=2027-03", Pdf, "Stundenliste-Lena-Huber-2027-03.pdf")
            .OnFile($"GET /api/employees/{RosterSample.Lena.Id}/timesheet/pdf?month=2027-02", Pdf, "Stundenliste-Lena-Huber-2027-02.pdf");
        var team = await RosterTests.Loaded(api);
        team.SelectedEntry = team.Entries.Single(e => e.Title == "Lena Huber");
        await RosterTests.Until(() => team.Card?.Entry.Title == "Lena Huber");
        var card = team.Card!;
        card.SelectTab(PersonCardViewModel.TimesTab);
        await RosterTests.Until(() => card.Times!.IsLoaded);
        var times = card.Times!;
        Assert.True(times.CanOfferDocument);

        times.ToggleDocumentCommand.Execute(null);

        var document = times.Document!;
        Assert.Equal($"api/employees/{RosterSample.Lena.Id}/timesheet/pdf?month=2027-03", document.Source.PdfPath);
        Assert.False(document.HasSend);
        Assert.Equal("Druckt die Stundenliste für März 2027.", times.DocumentHint);

        await times.MoveToAsync(RosterSample.Monday.AddDays(-7));

        Assert.EndsWith("month=2027-02", times.Document!.Source.PdfPath, StringComparison.Ordinal);
        var files = new RecordingFileSaver();
        times.Document.Files = files;
        await times.Document.SaveCommand.ExecuteAsync(null);
        Assert.Equal("Stundenliste-Lena-Huber-2027-02.pdf", files.SavedName);
        Assert.Equal("Gespeichert: Stundenliste-Lena-Huber-2027-02.pdf", times.DocumentMessage);
    }

    [Fact]
    public async Task In_der_Uebersicht_aller_Zeiten_gibt_es_keine_Stundenliste()
    {
        var (api, _) = RosterTests.Setup();
        var team = await RosterTests.Loaded(api);
        Assert.True(await team.SwitchToAsync(TeamViewModel.TimesMode));
        await RosterTests.Until(() => team.Times!.IsLoaded);

        Assert.False(team.Times!.CanOfferDocument);
    }

    // ---------- Inventur ----------

    [Fact]
    public async Task Die_Zaehlliste_des_Lagerorts_gibt_es_vor_und_waehrend_der_Zaehlung_und_folgt_dem_Lagerort()
    {
        var api = StockSample.Api().OnFile($"GET /api/locations/{Vienna.Id}/storage-places/{MainId}/count-sheet/pdf", Pdf, "Zählliste-Hauptlager-2027-03-01.pdf");
        var stock = await Stock(api);
        stock.SetTab(StockTab.Count);
        var count = stock.Count;
        Assert.Equal("Zählliste drucken …", count.DocumentButtonText);

        count.ToggleDocumentCommand.Execute(null);

        var document = count.Document!;
        Assert.Equal($"api/locations/{Vienna.Id}/storage-places/{MainId}/count-sheet/pdf", document.Source.PdfPath);
        Assert.False(document.HasSend);
        Assert.Equal("Zählliste Hauptlager", document.Source.Title);

        count.SelectedPlace = count.PlaceOptions.Single(p => p.Id == ShopId);
        Assert.EndsWith($"{ShopId}/count-sheet/pdf", count.Document!.Source.PdfPath, StringComparison.Ordinal);

        count.SelectedPlace = count.PlaceOptions.Single(p => p.Id == MainId);
        await count.StartCommand.ExecuteAsync(null);
        Assert.NotNull(count.Document);
        var files = new RecordingFileSaver();
        count.Document.Files = files;
        await count.Document.SaveCommand.ExecuteAsync(null);
        Assert.Equal("Zählliste-Hauptlager-2027-03-01.pdf", files.SavedName);
        Assert.Equal("Gespeichert: Zählliste-Hauptlager-2027-03-01.pdf", count.DocumentMessage);
    }

    [Fact]
    public async Task Eine_uebernommene_Zaehlung_vom_Handy_traegt_die_Inventurliste_mit_der_Zaehlung_im_Pfad()
    {
        var api = StockSample.Api()
            .On($"GET {Route("/count-sessions?status=Submitted")}", new List<StockCountSessionDto> { CabinetSession })
            .On($"GET {Route($"/count-sessions/{CabinetSessionId}")}", CabinetSession);
        var stock = await Stock(api);
        stock.SetTab(StockTab.Count);
        await stock.Count.LoadHandySessionsAsync();
        stock.Count.ToggleDocumentCommand.Execute(null);
        Assert.Contains("count-sheet", stock.Count.Document!.Source.PdfPath, StringComparison.Ordinal);

        stock.Count.HandySessions.Single().AdoptCommand!.Execute(null);
        await RosterTests.Until(() => stock.Count.IsAdopted);

        Assert.Equal("Inventurliste drucken …", stock.Count.DocumentButtonText);
        Assert.Equal($"api/locations/{Vienna.Id}/stock-counts/{CabinetSessionId}/pdf", stock.Count.Document!.Source.PdfPath);
        Assert.Equal("Inventurliste Schrank Zeder", stock.Count.Document.Source.Title);
    }

    [Fact]
    public async Task Die_Inventur_ohne_das_Recht_der_Leitung_hat_gesperrte_Knoepfe_mit_Hinweis()
    {
        var stock = await Stock(StockSample.Api(), new CurrentUser("u9", "recht@aispa.test", "Rolf", 1, new CurrentAccess(Guid.NewGuid(), "Test-Spa", StaffRole.Manager, true, []), null,
            [AccessRight.ViewCalendar, AccessRight.Book]));
        stock.SetTab(StockTab.Count);

        stock.Count.ToggleDocumentCommand.Execute(null);

        Assert.False(stock.Count.Document!.IsAllowed);
        Assert.Equal("Die Inventur druckt nur die Standortleitung.", stock.Count.Document.DisabledHint);
    }

    // ---------- Bestellungen ----------

    [Fact]
    public async Task Jede_Bestellung_traegt_den_Knopfblock_mit_PDF_und_Versand_an_den_Lieferanten()
    {
        var stock = await Stock(StockSample.Api());

        var card = Assert.Single(stock.Orders.Orders);

        Assert.Equal($"api/purchase-orders/{OrderId}/pdf", card.Document.Source.PdfPath);
        Assert.Equal($"api/purchase-orders/{OrderId}/send", card.Document.Source.SendPath);
        Assert.Equal("Bestellung 7", card.Document.Source.Title);
        Assert.True(card.Document.HasSend);
        Assert.False(card.HasSentMails);
    }

    [Fact]
    public async Task Die_Vermerke_stehen_als_Gesendet_am_an_unter_der_Bestellung()
    {
        var sent = new[]
        {
            new SentDocumentDto("bestellung@lavendelhof.example", new DateTimeOffset(2027, 2, 28, 9, 12, 0, TimeSpan.Zero), "Anna"),
            new SentDocumentDto("lager@lavendelhof.example", new DateTimeOffset(2027, 3, 1, 8, 0, 0, TimeSpan.Zero), null),
        };
        var stock = await Stock(StockSample.Api(startOrder: DraftOrder with { SentMails = sent }));

        var card = Assert.Single(stock.Orders.Orders);

        Assert.True(card.HasSentMails);
        Assert.Equal(["Gesendet am 28.02.2027 10:12 an bestellung@lavendelhof.example von Anna", "Gesendet am 01.03.2027 09:00 an lager@lavendelhof.example"], card.SentMailLines);
    }

    [Fact]
    public async Task Senden_der_Bestellung_fragt_mit_der_Adresse_des_Lieferanten_und_laedt_danach_den_Vermerk_neu()
    {
        var order = DraftOrder;
        var api = StockSample.Api(startOrder: order)
            .OnJson($"GET {Route("/orders")}", _ => new List<PurchaseOrderDto> { order })
            .On($"GET /api/purchase-orders/{OrderId}/send", new DocumentMailDraft("bestellung@lavendelhof.example", "Bestellung 7 vom 19.10.2026", "Guten Tag,"))
            .OnJson($"POST /api/purchase-orders/{OrderId}/send", _ =>
            {
                order = order with { SentMails = [new SentDocumentDto("bestellung@lavendelhof.example", new DateTimeOffset(2027, 3, 1, 9, 0, 0, TimeSpan.Zero), "Anna")] };
                return new { };
            }, HttpStatusCode.Accepted);
        var stock = await Stock(api);
        var dialogs = (FakeDialogs)stock.Dialogs!;
        var card = Assert.Single(stock.Orders.Orders);

        var sending = card.Document.SendCommand.ExecuteAsync(null);
        var dialog = Assert.IsType<SendDocumentDialogViewModel>(await Until(() => dialogs.Open));
        Assert.Equal("Bestellung 7 per Mail senden?", dialog.Title);
        Assert.Equal("bestellung@lavendelhof.example", dialog.To);
        dialog.SendCommand.Execute(null);
        await sending;
        await RosterTests.Until(() => stock.Orders.Orders.Single().HasSentMails);

        Assert.Equal("Gesendet an bestellung@lavendelhof.example", stock.Message);
        Assert.Equal(["Gesendet am 01.03.2027 10:00 an bestellung@lavendelhof.example von Anna"], stock.Orders.Orders.Single().SentMailLines);
    }

    private static async Task<T> Until<T>(Func<T?> read)
        where T : class
    {
        for (var i = 0; i < 200; i++)
        {
            if (read() is { } value)
            {
                return value;
            }

            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        throw new TimeoutException("Der Dialog kam nicht.");
    }

    // ---------- Ablauf-Aufgaben ----------

    [Fact]
    public async Task Die_Ablaufliste_traegt_den_Zeitraum_im_Pfad_und_folgt_der_Wahl()
    {
        var api = StockSample.Api()
            .OnFile($"GET /api/locations/{Vienna.Id}/stock/expiring/pdf?days=30", Pdf, "Ablaufliste-Wien-2027-03-01.pdf")
            .OnFile($"GET /api/locations/{Vienna.Id}/stock/expiring/pdf?days=60", Pdf, "Ablaufliste-Wien-2027-03-01.pdf");
        var stock = await Stock(api);
        var tasks = stock.Tasks;
        Assert.Null(tasks.Document);
        Assert.Equal([7, 14, 30, 60, 90], tasks.DaysOptions);

        tasks.ToggleDocumentCommand.Execute(null);

        Assert.Equal($"api/locations/{Vienna.Id}/stock/expiring/pdf?days=30", tasks.Document!.Source.PdfPath);
        Assert.False(tasks.Document.HasSend);

        tasks.SelectedDays = 60;

        Assert.Equal($"api/locations/{Vienna.Id}/stock/expiring/pdf?days=60", tasks.Document!.Source.PdfPath);
        var files = new RecordingFileSaver();
        tasks.Document.Files = files;
        await tasks.Document.SaveCommand.ExecuteAsync(null);
        Assert.Equal("Ablaufliste-Wien-2027-03-01.pdf", files.SavedName);
        Assert.Equal("Gespeichert: Ablaufliste-Wien-2027-03-01.pdf", tasks.DocumentMessage);
    }
}
