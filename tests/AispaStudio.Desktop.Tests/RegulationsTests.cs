using System.Net;
using System.Text.Json;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;
using AispaStudio.Desktop.ViewModels;

namespace AispaStudio.Desktop.Tests;

/// <summary>
/// R05 Slice 111 (Ä252, E10, E11): Reiter „Regelungen und Vollmachten“, „Wer darf was?“, Warnungen mit „Trotzdem“ und Symbole. Regeln, Rechte und Texte prüft die API (<c>StaffRegulationTests</c>, <c>StaffRuleCheckTests</c>);
/// hier zählt, was die App von ihr nimmt, schickt und sagt.
/// </summary>
public sealed class RegulationsTests
{
    private const string RuleText = "Lena Huber hat eine Sonderregelung: keine Termine nach 16:00, gültig bis 31.12.2027.";

    private static readonly EmployeeDto Lena = RosterSample.Lena;
    private static readonly DateOnly Tuesday = RosterSample.Monday.AddDays(1);
    private static readonly string BasePath = $"/api/employees/{Lena.Id}/regulations";
    private static readonly string DayRoute = $"PUT /api/locations/{RosterSample.Vienna.Id}/roster/{Lena.Id}/{Tuesday:yyyy-MM-dd}";
    private static readonly BookingRejected Rule = new(RuleText, [], StaffRule: true);

    private sealed class FixedTime : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => TestData.At(10);

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna");
    }

    private static StaffRegulationsDto Regulations() => new(
        [new StaffNoteDto(Guid.NewGuid(), Lena.Id, "Spricht Italienisch", StaffNoteVisibility.Team, true, null, null, false)],
        [new StaffRuleDto(Guid.NewGuid(), Lena.Id, StaffRuleKind.NoAppointmentsAfter, "Keine Termine nach 16:00", new DateOnly(2027, 1, 1), new DateOnly(2027, 12, 31), new TimeOnly(16, 0), null, [], "Kinderbetreuung", false)],
        [new StaffPowerDto(Guid.NewGuid(), Lena.Id, StaffPowerKind.CashVoid, "Kassa-Storno", new DateOnly(2027, 1, 1), new DateOnly(2027, 3, 1), "Katrin", true)]);

    private static StubApi Api(StaffRegulationsDto? regulations = null)
    {
        var (api, _) = RosterTests.Setup();
        api.On($"GET {BasePath}", regulations ?? Regulations());
        return api;
    }

    private static async Task<PersonRegulationsViewModel> Open(StubApi api, CurrentUser? user = null)
    {
        var team = await RosterTests.Loaded(api, user);
        team.Open(Lena.Id, PersonCardViewModel.RegulationsTab, null);
        var tab = team.Card!.Regulations!;
        await RosterTests.Until(() => tab.IsLoaded);
        return tab;
    }

    private static List<RosterDayInput> Puts(StubApi api) =>
        [.. api.Calls.Where(c => c.Route == DayRoute).Select(c => JsonSerializer.Deserialize<RosterDayInput>(c.Body!, ApiClient.Json)!)];

    [Fact]
    public async Task Given_eine_Rezeptionistin_When_sie_die_Karte_einer_Kollegin_oeffnet_Then_fehlen_Reiter_Kennzeichen_und_Uebersicht()
    {
        var api = Api();
        var team = await RosterTests.Loaded(api, RosterTests.WithRights(StaffRole.Reception, AccessRight.ManageCatalog, AccessRight.ViewStaffRecords));

        team.Open(Lena.Id, PersonCardViewModel.RegulationsTab, null);

        Assert.Null(team.Card!.Regulations);
        Assert.DoesNotContain(team.Card.Tabs, t => t.Id == PersonCardViewModel.RegulationsTab);
        Assert.False(team.Card.HasFlags);
        Assert.DoesNotContain(team.Modes, m => m.Id == TeamViewModel.PowersMode);
        Assert.DoesNotContain(api.Calls, c => c.Route.Contains("staff-regulations", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Given_die_Inhaberin_When_sie_den_Reiter_oeffnet_Then_stehen_die_drei_Listen_und_die_abgelaufene_Vollmacht_ist_ausgegraut()
    {
        var tab = await Open(Api());

        Assert.Equal("Spricht Italienisch", Assert.Single(tab.Notes).Title);
        var rule = Assert.Single(tab.Rules);
        Assert.Equal("Keine Termine nach 16:00", rule.Title);
        Assert.Equal(["Gültig von 1.1.2027 bis 31.12.2027", "Grund: Kinderbetreuung"], rule.Lines);
        var power = Assert.Single(tab.Powers);
        Assert.Equal("Kassa-Storno (abgelaufen)", power.Title);
        Assert.True(power.IsExpired);
        Assert.Equal(["Gültig von 1.1.2027 bis 1.3.2027", "Erteilt von Katrin"], power.Lines);
    }

    [Fact]
    public async Task Given_eine_neue_Sonderregelung_When_gespeichert_wird_Then_geht_nur_der_Wert_der_Art_an_die_API()
    {
        var api = Api().On($"POST {BasePath}/rules", Regulations().Rules[0]);
        var tab = await Open(api);

        tab.AddRuleCommand.Execute(null);
        tab.SelectedRuleKind = tab.RuleKinds.Single(k => k.Value == StaffRuleKind.NoAppointmentsAfter);
        tab.LatestEnd = new TimeOnly(16, 0);
        tab.MaxHours = 6;
        tab.ValidUntil = new DateOnly(2027, 12, 31);
        tab.Reason = " Kinderbetreuung ";
        Assert.True(tab.HasUnsavedChanges);
        await tab.SaveCommand.ExecuteAsync(null);

        var sent = JsonSerializer.Deserialize<StaffRuleInput>(Assert.Single(api.Calls, c => c.Route == $"POST {BasePath}/rules").Body!, ApiClient.Json)!;
        Assert.Equal(StaffRuleKind.NoAppointmentsAfter, sent.Kind);
        Assert.Equal(new DateOnly(2027, 3, 2), sent.ValidFrom);
        Assert.Equal(new DateOnly(2027, 12, 31), sent.ValidUntil);
        Assert.Equal(new TimeOnly(16, 0), sent.LatestEnd);
        Assert.Null(sent.MaxHours);
        Assert.Equal("Kinderbetreuung", sent.Reason);
        Assert.False(tab.IsEditing);
        Assert.Equal("Gespeichert.", tab.StatusText);
    }

    [Fact]
    public async Task Given_eine_Sonderregelung_ohne_Uhrzeit_When_gespeichert_wird_Then_sagt_das_Feld_was_fehlt_und_nichts_geht_an_die_API()
    {
        var api = Api();
        var tab = await Open(api);

        tab.AddRuleCommand.Execute(null);
        await tab.SaveCommand.ExecuteAsync(null);

        Assert.Equal("Bitte die Uhrzeit eingeben.", tab.ValueError);
        Assert.True(tab.IsEditing);
        Assert.DoesNotContain(api.Calls, c => c.Route == $"POST {BasePath}/rules");
    }

    [Fact]
    public async Task Given_Kennzeichen_der_API_When_die_Liste_laedt_Then_stehen_sie_in_der_Zeile_und_oben_auf_der_Karte()
    {
        var api = Api().On("GET /api/staff-regulations/flags", new List<StaffFlagsDto> { new(Lena.Id, ["Spricht Italienisch"], ["Keine Termine nach 16:00"], ["Schlüssel"]) });

        var team = await RosterTests.Loaded(api);
        team.Open(Lena.Id, PersonCardViewModel.DetailsTab, null);

        Assert.Equal("Hinweis · Sonderregelung · Vollmacht", team.Entries.Single(e => e.EmployeeId == Lena.Id).FlagText);
        Assert.Null(team.Entries.Single(e => e.EmployeeId == RosterSample.Marco.Id).FlagText);
        Assert.Equal(["Hinweis: Spricht Italienisch", "Sonderregelung: Keine Termine nach 16:00", "Vollmacht: Schlüssel"], team.Card!.FlagLines);
    }

    [Fact]
    public async Task Given_die_Uebersicht_When_die_Inhaberin_sie_oeffnet_Then_steht_je_Vollmacht_wer_und_bis_wann()
    {
        var api = Api().On("GET /api/staff-regulations/powers", new List<PowerOverviewRowDto>
        {
            new(StaffPowerKind.CashVoid, "Kassa-Storno", true,
                [new(Lena.Id, "Lena", new DateOnly(2027, 1, 1), new DateOnly(2027, 6, 30), "Katrin", false), new(RosterSample.Marco.Id, "Marco", new DateOnly(2026, 1, 1), new DateOnly(2027, 3, 1), "Katrin", true)]),
            new(StaffPowerKind.Safe, "Tresor", false, [new(Lena.Id, "Lena", new DateOnly(2027, 1, 1), null, "Katrin", false)]),
            new(StaffPowerKind.Keys, "Schlüssel", false, []),
        });
        var team = await RosterTests.Loaded(api);

        Assert.True(await team.SwitchToAsync(TeamViewModel.PowersMode));
        await RosterTests.Until(() => team.Powers!.Rows.Count == 3);

        var rows = team.Powers!.Rows;
        Assert.Equal("Prüft die API zusätzlich zur Rolle", rows[0].Effect);
        Assert.Equal(["Lena, bis 30.6.2027, erteilt von Katrin", "Marco, abgelaufen am 1.3.2027, erteilt von Katrin"], rows[0].Holders.Select(h => h.Text));
        Assert.Equal([false, true], rows[0].Holders.Select(h => h.IsExpired));
        Assert.Equal("Nur zur Anzeige", rows[1].Effect);
        Assert.Equal("Lena, ohne Ende, erteilt von Katrin", Assert.Single(rows[1].Holders).Text);
        Assert.True(rows[2].HasNoHolders);
    }

    [Fact]
    public async Task Given_Lena_hat_die_Regel_When_die_Managerin_im_Tageseditor_speichert_Then_warnt_die_Maske_und_speichert_erst_nach_Klick_auf_Trotzdem_speichern()
    {
        var api = Api().On(DayRoute, Rule, HttpStatusCode.Conflict);
        var team = await RosterTests.Loaded(api);
        Assert.True(await team.SwitchToAsync(TeamViewModel.RosterMode));
        var plan = team.Plan!;
        await RosterTests.Until(() => plan.IsLoaded);
        var row = plan.Rows.Single(r => r.Name == "Lena Huber");
        await plan.OpenDayAsync(row, row.Cells[1]);
        var day = plan.Day!;
        day.Shifts[0].End = new TimeOnly(18, 0);

        Assert.False(await day.SaveChangesAsync());

        Assert.Equal(RuleText, day.RuleWarning);
        Assert.Null(day.Error);
        Assert.True(plan.HasUnsavedChanges);
        Assert.False(Assert.Single(Puts(api)).RulesConfirmed);

        var saved = new RosterCell(Tuesday, RosterSource.Plan, day.Shifts.Select(_ => new RosterEntry(RosterKind.Work, RosterSample.Vienna.Id, new TimeOnly(9, 0), new TimeOnly(18, 0))).ToList(), null, [], false);
        api.On(DayRoute, new RosterDayResult(saved, []));
        await day.SaveAnywayCommand.ExecuteAsync(null);

        Assert.True(Puts(api)[^1].RulesConfirmed);
        Assert.Null(day.RuleWarning);
        Assert.False(plan.HasUnsavedChanges);
    }

    [Fact]
    public async Task Given_ein_Symbol_der_API_When_der_Wochenplan_laedt_Then_steht_der_Hinweis_an_Lenas_Zeile()
    {
        var api = Api().On("GET /api/employee-notes/symbols?on=2027-03-02", new List<StaffNoteSymbolDto> { new(Lena.Id, "Spricht Italienisch") });
        var team = await RosterTests.Loaded(api);

        Assert.True(await team.SwitchToAsync(TeamViewModel.RosterMode));
        await RosterTests.Until(() => team.Plan!.IsLoaded);

        var row = team.Plan!.Rows.Single(r => r.Name == "Lena Huber");
        Assert.True(row.HasNote);
        Assert.Equal("Spricht Italienisch", row.NoteText);
    }

    private static StubApi CalendarApi() => new StubApi()
        .On("GET /api/locations", new List<LocationDto> { TestData.Location })
        .On($"GET /api/locations/{TestData.LocationId}/resources", TestData.Resources)
        .On($"GET /api/locations/{TestData.LocationId}/calendar?date=2027-03-01", TestData.Day())
        .On("GET /api/customers?search=eva", new List<CustomerDto> { new(Guid.NewGuid(), "Eva", "Muster", null, null, null, null) })
        .On("POST /api/appointments", TestData.Appointment(10, TestData.Anna));

    private static async Task<CalendarViewModel> Calendar(StubApi api)
    {
        var calendar = new CalendarViewModel(api.Client(), TestData.Owner(), null, new FixedTime());
        await calendar.InitializeAsync();
        await calendar.LoadAsync();
        return calendar;
    }

    [Fact]
    public async Task Given_ein_Hinweis_Team_When_der_Kalender_Lenas_Spalte_zeigt_Then_steht_der_Hinweis_im_Tooltip()
    {
        var api = CalendarApi().On("GET /api/employee-notes/symbols?on=2027-03-01", new List<StaffNoteSymbolDto> { new(TestData.Anna.Id, "Spricht Italienisch") });

        var calendar = await Calendar(api);

        var column = calendar.Columns.Single(c => c.Id == TestData.Anna.Id);
        Assert.True(column.HasNote);
        Assert.Equal("Spricht Italienisch", column.NoteText);
        Assert.Contains(column.HeaderTipLines, l => l.Text == "Spricht Italienisch");
        Assert.All(calendar.Columns.Where(c => c.Id != TestData.Anna.Id), c => Assert.False(c.HasNote));
    }

    [Fact]
    public async Task Given_die_Regel_When_die_Rezeption_bucht_Then_warnt_der_Kalender_und_bucht_erst_nach_Trotzdem_buchen()
    {
        var api = CalendarApi().On("POST /api/appointments", Rule, HttpStatusCode.Conflict);
        var calendar = await Calendar(api);
        calendar.StartBooking(calendar.Columns[0], 30 * DayLayout.PixelsPerMinute);
        var booking = calendar.Booking!;
        await booking.SearchAsync("eva");
        booking.Customer = booking.Customers[0];
        booking.StartText = "16:00";

        await booking.BookCommand.ExecuteAsync(null);

        Assert.Equal(RuleText, booking.OutsideHours);
        Assert.Null(booking.Error);
        var first = JsonSerializer.Deserialize<AppointmentRequest>(Assert.Single(api.Calls, c => c.Route == "POST /api/appointments").Body!, ApiClient.Json)!;
        Assert.False(first.RulesConfirmed);

        api.On("POST /api/appointments", TestData.Appointment(10, TestData.Anna));
        await booking.BookAnywayCommand.ExecuteAsync(null);

        var second = JsonSerializer.Deserialize<AppointmentRequest>(api.Calls.Last(c => c.Route == "POST /api/appointments").Body!, ApiClient.Json)!;
        Assert.True(second.RulesConfirmed);
        Assert.False(second.OutsideHoursConfirmed);
    }

    [Fact]
    public async Task Given_keine_Verbindung_When_die_Rezeption_bucht_Then_geht_der_Termin_bestaetigt_in_die_Warteschlange_damit_der_Abgleich_nicht_scheitert()
    {
        var cache = new OfflineCache(new MemoryOfflineStore(), new FixedTime());
        var api = CalendarApi();
        var calendar = new CalendarViewModel(api.Client(cache), TestData.Owner(), null, new FixedTime());
        await calendar.InitializeAsync();
        await calendar.LoadAsync();
        calendar.StartBooking(calendar.Columns[0], 30 * DayLayout.PixelsPerMinute);
        var booking = calendar.Booking!;
        await booking.SearchAsync("eva");
        booking.Customer = booking.Customers[0];
        booking.StartText = "16:00";

        api.Down = true;
        await booking.BookCommand.ExecuteAsync(null);

        Assert.True(JsonSerializer.Deserialize<AppointmentRequest>(Assert.Single(cache.Pending).Body, ApiClient.Json)!.RulesConfirmed);
    }

    [Fact]
    public async Task Given_die_Warnung_im_Tageseditor_When_die_Zeit_geaendert_wird_Then_verschwindet_sie_und_nichts_ist_vorab_bestaetigt()
    {
        var api = Api().On(DayRoute, Rule, HttpStatusCode.Conflict);
        var team = await RosterTests.Loaded(api);
        Assert.True(await team.SwitchToAsync(TeamViewModel.RosterMode));
        await RosterTests.Until(() => team.Plan!.IsLoaded);
        var row = team.Plan!.Rows.Single(r => r.Name == "Lena Huber");
        await team.Plan.OpenDayAsync(row, row.Cells[1]);
        var day = team.Plan.Day!;
        day.Shifts[0].End = new TimeOnly(18, 0);
        await day.SaveChangesAsync();
        Assert.True(day.HasRuleWarning);

        day.Shifts[0].End = new TimeOnly(20, 0);

        Assert.False(day.HasRuleWarning);
    }

    [Fact]
    public async Task Given_erst_ausserhalb_der_Oeffnungszeit_bestaetigt_When_danach_die_Regel_warnt_Then_bleibt_die_erste_Bestaetigung_erhalten()
    {
        var api = CalendarApi().On("POST /api/appointments", Rule, HttpStatusCode.Conflict);
        var calendar = await Calendar(api);
        calendar.StartBooking(calendar.Columns[0], 30 * DayLayout.PixelsPerMinute);
        var booking = calendar.Booking!;
        await booking.SearchAsync("eva");
        booking.Customer = booking.Customers[0];
        booking.StartText = "19:30";

        await booking.BookAnywayCommand.ExecuteAsync(null);
        Assert.Equal(RuleText, booking.OutsideHours);
        await booking.BookAnywayCommand.ExecuteAsync(null);

        var last = JsonSerializer.Deserialize<AppointmentRequest>(api.Calls.Last(c => c.Route == "POST /api/appointments").Body!, ApiClient.Json)!;
        Assert.True(last.OutsideHoursConfirmed);
        Assert.True(last.RulesConfirmed);
    }
}
