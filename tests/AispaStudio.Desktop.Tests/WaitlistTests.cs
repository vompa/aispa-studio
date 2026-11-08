using System.Net;
using AispaStudio.Contracts;
using AispaStudio.Desktop.ViewModels;

namespace AispaStudio.Desktop.Tests;

/// <summary>Warteliste im Kalender: Freie Zeiten zeigen, Buchen, Kundenkarte, Telefon, „Frei: …“ und Anfrage abschließen mit Grund.</summary>
public sealed class WaitlistTests
{
    private static readonly TimeZoneInfo Vienna = TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna");
    private static readonly CustomerDto Guest = new(Guid.NewGuid(), "Eva", "Muster", "eva@example.at", "+43 664 123 45 67", null, null);

    private sealed class FixedTime : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => TestData.At(10);

        public override TimeZoneInfo LocalTimeZone => Vienna;
    }

    private static WaitlistStaffEntry Entry(DayPart part = DayPart.Afternoon, int from = 0, int days = 3, int offers = 0, string? phone = "+43 664 123 45 67") =>
        new(Guid.NewGuid(), Guest.Id, "Eva Muster", TestData.MassageTreatment.Id, "Massage", TestData.Monday.AddDays(from), TestData.Monday.AddDays(from + days), part,
            WaitlistStatus.Waiting, TestData.At(8), offers, null, phone);

    private static AvailableSlot Slot(int dayOffset, int hour, EmployeeDto who) =>
        new(TestData.At(hour).AddDays(dayOffset), TestData.At(hour, 50).AddDays(dayOffset), who.Id, TestData.Room.Id, null);

    private static string FreeRoute(WaitlistStaffEntry entry, int days, int minSlots = 1, int fromOffset = 0) =>
        $"GET /api/locations/{TestData.LocationId}/availability?treatmentId={entry.TreatmentId}&date={TestData.Monday.AddDays(fromOffset):yyyy-MM-dd}&customerId={Guest.Id}&minSlots={minSlots}&days={days}";

    private static StubApi Api(params WaitlistStaffEntry[] entries) => new StubApi()
        .On("GET /api/locations", new List<LocationDto> { TestData.Location })
        .On($"GET /api/locations/{TestData.LocationId}/resources", TestData.Resources)
        .On($"GET /api/locations/{TestData.LocationId}/calendar?date=2027-03-01", TestData.Day())
        .On($"GET /api/locations/{TestData.LocationId}/calendar?date=2027-03-02", TestData.Day() with { Date = TestData.Monday.AddDays(1) })
        .On($"GET /api/customers/{Guest.Id}", Guest)
        .On($"GET /api/locations/{TestData.LocationId}/waitlist", entries.ToList());

    private static async Task<CalendarViewModel> OpenAsync(StubApi api)
    {
        var vm = new CalendarViewModel(api.Client(), TestData.Reception(), null, new FixedTime());
        await vm.InitializeAsync();
        await vm.LoadAsync();
        return vm;
    }

    private static async Task Until(Func<bool> condition)
    {
        for (var i = 0; i < 300 && !condition(); i++)
        {
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        Assert.True(condition());
    }

    [Fact]
    public async Task Freie_Zeiten_zeigen_tut_auch_bei_einem_Wunsch_ab_heute_etwas_Erkennbares()
    {
        // Fehler: „Ab 1.3. anzeigen“ wechselte nur den Tag; bei einem Wunsch ab heute (dem gezeigten Tag) geschah nichts Erkennbares.
        var entry = Entry();
        var api = Api(entry);
        var vm = await OpenAsync(api);
        vm.OpenWaitlistCommand.Execute(null);

        await vm.WaitlistPanel!.Rows[0].ShowFreeCommand.ExecuteAsync(null);

        var freeTime = Assert.IsType<FreeTimeViewModel>(vm.FreeTime);
        Assert.Equal(TestData.MassageTreatment.Id, freeTime.Treatment!.Id);
        Assert.Equal(Guest.Id, freeTime.GuestCustomer!.Id);
        Assert.Equal("nachmittags", freeTime.Part.Label);
        Assert.Null(vm.WaitlistPanel);
    }

    [Fact]
    public async Task Freie_Zeiten_zeigen_beginnt_bei_einem_Wunsch_in_der_Zukunft_an_dessen_Tag_und_nie_in_der_Vergangenheit()
    {
        var future = Entry(from: 1, days: 2);
        var past = Entry(from: -5, days: 6);
        var api = Api(future, past);
        var vm = await OpenAsync(api);
        vm.OpenWaitlistCommand.Execute(null);

        await vm.WaitlistPanel!.Rows[0].ShowFreeCommand.ExecuteAsync(null);
        Assert.Equal(TestData.Monday.AddDays(1), vm.Date);

        vm.OpenWaitlistCommand.Execute(null);
        await vm.WaitlistPanel!.Rows[1].ShowFreeCommand.ExecuteAsync(null);
        // Ein Wunsch, der schon läuft, sucht ab heute, nicht ab dem Beginn vor fünf Tagen.
        Assert.Equal(TestData.Monday, vm.Date);
    }

    [Fact]
    public async Task Der_Eintrag_zeigt_Name_Behandlung_Zeitraum_Stand_und_Telefon_und_der_Name_oeffnet_die_Kundenkarte()
    {
        var api = Api(Entry(offers: 2), Entry(phone: null));
        var vm = await OpenAsync(api);
        Guid? opened = null;
        vm.CustomerRequested = id =>
        {
            opened = id;
            return Task.CompletedTask;
        };
        await vm.LoadAsync();
        vm.OpenWaitlistCommand.Execute(null);
        var rows = vm.WaitlistPanel!.Rows;

        Assert.Equal("Eva Muster", rows[0].Name);
        Assert.Equal("Massage", rows[0].Treatment);
        Assert.Equal("Mo. 1.3. bis Do. 4.3., nachmittags", rows[0].Period);
        Assert.Equal("2× freien Termin gemeldet", rows[0].State);
        Assert.True(rows[0].IsOffered);
        Assert.Equal("Wartet", rows[1].State);
        Assert.Equal("+43 664 123 45 67", rows[0].Phone);
        Assert.False(rows[1].HasPhone);

        await rows[0].OpenCustomerCommand.ExecuteAsync(null);
        Assert.Equal(Guest.Id, opened);
    }

    [Fact]
    public async Task Ist_schon_etwas_frei_steht_es_am_Eintrag_zur_Wunschtageszeit()
    {
        var entry = Entry(DayPart.Afternoon, days: 3);
        // Der Server liefert auch den Vormittag; „nachmittags“ lässt davon nichts übrig.
        var api = Api(entry).On(FreeRoute(entry, days: 4), new List<AvailableSlot> { Slot(0, 9, TestData.Anna), Slot(0, 11, TestData.Ben), Slot(0, 15, TestData.Ben) });
        var vm = await OpenAsync(api);

        vm.OpenWaitlistCommand.Execute(null);
        var row = vm.WaitlistPanel!.Rows[0];
        await Until(() => row.HasFree);

        Assert.Equal("Frei: Mo. 1.3. 15:00 bei Ben", row.FreeText);
        Assert.True(row.BookCommand.CanExecute(null));
    }

    [Fact]
    public async Task Ohne_freie_Zeit_gibt_es_kein_Frei_und_kein_Buchen()
    {
        var entry = Entry(days: 3);
        var api = Api(entry).On(FreeRoute(entry, days: 4), new List<AvailableSlot>());
        var vm = await OpenAsync(api);

        vm.OpenWaitlistCommand.Execute(null);
        var row = vm.WaitlistPanel!.Rows[0];
        await Task.Delay(100, TestContext.Current.CancellationToken);

        Assert.False(row.HasFree);
        Assert.Null(row.FreeText);
        Assert.False(row.BookCommand.CanExecute(null));
    }

    [Fact]
    public async Task Buchen_bucht_die_freie_Zeit_fuer_den_Gast_und_der_Eintrag_verlaesst_die_Liste()
    {
        var entry = Entry(DayPart.Any, days: 3);
        var other = Entry(DayPart.Any, days: 3) with { Id = Guid.NewGuid(), CustomerName = "Paul Huber" };
        var booked = TestData.Appointment(15, TestData.Anna) with { CustomerId = Guest.Id, CustomerName = "Eva Muster" };
        var api = Api(entry, other).On(FreeRoute(entry, days: 4), new List<AvailableSlot> { Slot(0, 15, TestData.Anna) }).On("POST /api/appointments", booked);
        var vm = await OpenAsync(api);
        vm.OpenWaitlistCommand.Execute(null);
        var row = vm.WaitlistPanel!.Rows[0];
        await Until(() => row.HasFree);

        await row.BookCommand.ExecuteAsync(null);

        var post = Assert.Single(api.Calls, c => c.Route == "POST /api/appointments");
        Assert.Contains(Guest.Id.ToString(), post.Body, StringComparison.Ordinal);
        Assert.Contains(TestData.Anna.Id.ToString(), post.Body, StringComparison.Ordinal);
        Assert.Equal([other.Id], vm.Waitlist.Select(r => r.Entry.Id));
        Assert.StartsWith("Gebucht: Eva Muster", vm.Toast, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Anfrage_abschliessen_fragt_nach_dem_Grund_und_nimmt_den_Eintrag_von_der_Liste()
    {
        var entry = Entry();
        var other = Entry() with { Id = Guid.NewGuid(), CustomerName = "Paul Huber" };
        var api = Api(entry, other).On($"POST /api/waitlist/{entry.Id}/close", new WaitlistEntryDto(entry.Id, TestData.LocationId, "Wien", entry.TreatmentId, "Massage",
            entry.From, entry.Until, entry.DayPart, WaitlistStatus.Booked, entry.CreatedAt, null));
        var vm = await OpenAsync(api);
        vm.OpenWaitlistCommand.Execute(null);
        var row = vm.WaitlistPanel!.Rows[0];

        row.AskCloseCommand.Execute(null);
        Assert.True(row.IsClosing);
        Assert.Equal(["Gebucht", "Abgesagt", "Nicht erreichbar"], WaitlistRow.Reasons.Select(r => r.Label));
        row.Reason = WaitlistRow.Reasons[0];
        await row.CloseCommand.ExecuteAsync(null);

        var call = Assert.Single(api.Calls, c => c.Route.EndsWith("/close", StringComparison.Ordinal));
        Assert.Contains("\"reason\":\"Arranged\"", call.Body, StringComparison.OrdinalIgnoreCase);
        Assert.Equal([other.Id], vm.Waitlist.Select(r => r.Entry.Id));
        Assert.Equal([other.Id], vm.WaitlistPanel!.Rows.Select(r => r.Entry.Id));
        Assert.Contains("Eva Muster von der Warteliste, Termin vereinbart", vm.Toast, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Schlaegt_das_Abschliessen_fehl_bleibt_der_Eintrag_und_die_Meldung_sagt_warum()
    {
        var entry = Entry();
        var api = Api(entry).On($"POST /api/waitlist/{entry.Id}/close", new { title = "Keine Berechtigung." }, HttpStatusCode.Forbidden);
        var vm = await OpenAsync(api);
        vm.OpenWaitlistCommand.Execute(null);
        var row = vm.WaitlistPanel!.Rows[0];

        row.AskCloseCommand.Execute(null);
        await row.CloseCommand.ExecuteAsync(null);

        Assert.Single(vm.Waitlist);
        Assert.Equal("Nicht von der Warteliste genommen", vm.Problem!.Title);
        Assert.False(row.IsClosing);
    }

    [Fact]
    public async Task Given_ein_Eintrag_der_Warteliste_When_Anfrage_abschliessen_gewaehlt_wird_Then_nennt_die_Auswahl_die_Gruende_Gebucht_Abgesagt_und_Nicht_erreichbar()
    {
        var vm = await OpenAsync(Api(Entry()));
        vm.OpenWaitlistCommand.Execute(null);
        var row = vm.WaitlistPanel!.Rows[0];

        row.AskCloseCommand.Execute(null);

        Assert.True(row.IsClosing);
        Assert.Equal(["Gebucht", "Abgesagt", "Nicht erreichbar"], WaitlistRow.Reasons.Select(r => r.Label));
        Assert.Equal("Anfrage abschließen …", WaitlistRow.CloseLabel);
    }

    [Theory]
    [InlineData(0, "Arranged")]
    [InlineData(1, "NoLongerWanted")]
    [InlineData(2, "NoLongerWanted")]
    public async Task Given_ein_Grund_When_die_Anfrage_abgeschlossen_wird_Then_geht_der_passende_Serverwert_hinaus(int reasonIndex, string expected)
    {
        var entry = Entry();
        var api = Api(entry).On($"POST /api/waitlist/{entry.Id}/close", new WaitlistEntryDto(entry.Id, TestData.LocationId, "Wien", entry.TreatmentId, "Massage",
            entry.From, entry.Until, entry.DayPart, WaitlistStatus.Cancelled, entry.CreatedAt, null));
        var vm = await OpenAsync(api);
        vm.OpenWaitlistCommand.Execute(null);
        var row = vm.WaitlistPanel!.Rows[0];

        row.AskCloseCommand.Execute(null);
        row.Reason = WaitlistRow.Reasons[reasonIndex];
        await row.CloseCommand.ExecuteAsync(null);

        var call = Assert.Single(api.Calls, c => c.Route.EndsWith("/close", StringComparison.Ordinal));
        Assert.Contains($"\"reason\":\"{expected}\"", call.Body, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(vm.Waitlist);
    }

    [Fact]
    public async Task Given_ein_gebuchter_Wartelisteneintrag_When_die_Liste_neu_geladen_wird_Then_steht_er_nicht_mehr_als_Wartet_da()
    {
        var waiting = Entry();
        var booked = Entry() with { Id = Guid.NewGuid(), CustomerName = "Paul Huber", Status = WaitlistStatus.Booked };
        var vm = await OpenAsync(Api(waiting, booked));

        vm.OpenWaitlistCommand.Execute(null);

        Assert.Equal([waiting.Id], vm.Waitlist.Select(r => r.Entry.Id));
        Assert.Equal([waiting.Id], vm.WaitlistPanel!.Rows.Select(r => r.Entry.Id));
    }
}
