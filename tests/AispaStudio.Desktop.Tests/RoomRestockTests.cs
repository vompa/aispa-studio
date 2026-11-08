using System.Net;
using AispaStudio.Contracts;
using AispaStudio.Desktop.ViewModels;

namespace AispaStudio.Desktop.Tests;

/// <summary>Raumcheck nach der Behandlung im Windows-Kalender (ADR 0068): Seitenleiste „Raumpflege &lt;Raum&gt;“ statt direktem Abhaken, Aufgefüllt, Reicht noch, Zähler der Betriebsleiste.</summary>
public sealed class RoomRestockTests
{
    private static readonly TimeZoneInfo Vienna = TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna");
    private static readonly string CalendarRoute = $"GET /api/locations/{TestData.LocationId}/calendar?date=2027-03-01";
    private static readonly Guid OilId = Guid.Parse("00000000-0000-0000-0000-0000000000b1");
    private static readonly Guid TowelsId = Guid.Parse("00000000-0000-0000-0000-0000000000b2");

    private sealed class FixedTime : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => TestData.At(10);

        public override TimeZoneInfo LocalTimeZone => Vienna;
    }

    private static RestockLine Oil(RestockState state = RestockState.Open) =>
        new(OilId, "Massageöl neutral", "ml", state == RestockState.Refilled ? 500 : 200, 500, state == RestockState.Refilled ? 0 : 300, state);

    private static RestockLine Towels(RestockState state = RestockState.Open) => new(TowelsId, "Handtuch", "Stk", 2, 6, 4, state);

    private static RoomCareTaskDto CareTask(AppointmentDto appointment, RoomCareState state, IReadOnlyList<RestockLine>? restock) => new(appointment.Id, [appointment.Id],
        TestData.Room.Id, TestData.Room.Name, "Raum Zeder pflegen bis 11:10", appointment.BlockedUntil, appointment.BlockedUntil.AddMinutes(10), appointment.EmployeeId, state,
        null, null, restock);

    private static StubApi Api(AppointmentDto appointment, IReadOnlyList<RestockLine>? restock, int roomsBelowMinimum = 0, RoomCareState state = RoomCareState.Open) => new StubApi()
        .On("GET /api/locations", new List<LocationDto> { TestData.Location })
        .On($"GET /api/locations/{TestData.LocationId}/resources", TestData.Resources)
        .On(CalendarRoute, TestData.Day(appointment) with
        {
            RoomCare = new RoomCareDay([CareTask(appointment, state, restock)],
                [new RoomDayStateDto(TestData.Room.Id, TestData.Room.Name, RoomStartState.NoChecklist, 0, 0, null, null, 1, 0, null, null)], roomsBelowMinimum),
        });

    private static async Task<CalendarViewModel> OpenAsync(StubApi api, CurrentUser? user = null)
    {
        var vm = new CalendarViewModel(api.Client(), user ?? TestData.Reception(), null, new FixedTime());
        await vm.InitializeAsync();
        await vm.LoadAsync();
        vm.ShowRoomsCommand.Execute(null);
        return vm;
    }

    private static CalendarStripe CareStripe(CalendarViewModel vm) => vm.Columns.Single(c => c.Id == TestData.Room.Id).Stripes.Single(s => s.HasCare);

    private static string AnswerRoute(AppointmentDto appointment, Guid itemId) => $"PUT /api/appointments/{appointment.Id}/room-care/restock/{itemId}";

    [Fact]
    public async Task Ein_Klick_auf_einen_Streifen_mit_offenen_Zeilen_oeffnet_die_Seitenleiste_statt_abzuhaken()
    {
        var appointment = TestData.Appointment(10, TestData.Anna);
        var api = Api(appointment, [Oil(), Towels()]);
        var vm = await OpenAsync(api);
        var stripe = CareStripe(vm);

        await vm.ToggleCareAsync(stripe);

        Assert.DoesNotContain(api.Calls, c => c.Route.StartsWith("PUT ", StringComparison.Ordinal));
        var panel = Assert.IsType<RoomCarePanelViewModel>(vm.RoomCarePanel);
        Assert.True(vm.HasPanel);
        Assert.Same(panel, vm.ActivePanel);
        Assert.Equal(("Raumpflege Raum Zeder", "Raum Zeder pflegen bis 11:10"), (panel.PanelTitle, panel.PanelSubtitle));
        Assert.Equal(["Massageöl neutral: 200 von 500 ml, 300 ml nachfüllen", "Handtuch: 2 von 6 Stk, 4 Stk nachfüllen"], panel.Rows.Select(r => r.Text));
        Assert.Equal(300m, panel.Rows[0].Quantity);
        Assert.Contains("2 Materialien nachfüllen", stripe.Tip, StringComparison.Ordinal);
        Assert.False(panel.CanFinish);
    }

    [Fact]
    public async Task Das_Schliessen_Kreuz_schliesst_die_Seitenleiste_ohne_Buchung()
    {
        var appointment = TestData.Appointment(10, TestData.Anna);
        var api = Api(appointment, [Oil()]);
        var vm = await OpenAsync(api);
        await vm.ToggleCareAsync(CareStripe(vm));

        vm.RoomCarePanel!.CloseCommand.Execute(null);

        Assert.Null(vm.RoomCarePanel);
        Assert.False(vm.HasPanel);
        Assert.DoesNotContain(api.Calls, c => c.Route.StartsWith("PUT ", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Aufgefuellt_sendet_die_Menge_und_zeigt_die_Zeile_als_aufgefuellt()
    {
        var appointment = TestData.Appointment(10, TestData.Anna);
        var api = Api(appointment, [Oil(), Towels()])
            .On(AnswerRoute(appointment, OilId), CareTask(appointment, RoomCareState.Open, [Oil(RestockState.Refilled), Towels()]));
        var vm = await OpenAsync(api);
        await vm.ToggleCareAsync(CareStripe(vm));
        var panel = vm.RoomCarePanel!;
        var loads = api.Calls.Count(c => c.Route == CalendarRoute);

        panel.Rows[0].Quantity = 250m;
        await panel.Rows[0].RefillCommand.ExecuteAsync(null);

        var put = Assert.Single(api.Calls, c => c.Route == AnswerRoute(appointment, OilId));
        Assert.Contains("\"state\":\"Refilled\"", put.Body, StringComparison.Ordinal);
        Assert.Contains("\"quantity\":250", put.Body, StringComparison.Ordinal);
        Assert.True(panel.Rows[0].IsRefilled);
        Assert.False(panel.Rows[0].IsOpen);
        Assert.Equal("Massageöl neutral: 500 von 500 ml", panel.Rows[0].Text);
        Assert.True(panel.Rows[1].IsOpen);
        Assert.True(api.Calls.Count(c => c.Route == CalendarRoute) > loads);
    }

    [Fact]
    public async Task Reicht_noch_sendet_nur_die_Antwort()
    {
        var appointment = TestData.Appointment(10, TestData.Anna);
        var api = Api(appointment, [Oil()])
            .On(AnswerRoute(appointment, OilId), CareTask(appointment, RoomCareState.Open, [Oil(RestockState.Enough)]));
        var vm = await OpenAsync(api);
        await vm.ToggleCareAsync(CareStripe(vm));
        var panel = vm.RoomCarePanel!;

        await panel.Rows[0].EnoughCommand.ExecuteAsync(null);

        var put = Assert.Single(api.Calls, c => c.Route == AnswerRoute(appointment, OilId));
        Assert.Contains("\"state\":\"Enough\"", put.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("\"quantity\":2", put.Body, StringComparison.Ordinal);
        Assert.True(panel.Rows[0].IsEnough);
        Assert.Equal(RestockState.Enough, panel.Rows[0].Line.State);
    }

    [Fact]
    public async Task Raumpflege_erledigt_ist_erst_aktiv_wenn_keine_Zeile_mehr_offen_ist()
    {
        var appointment = TestData.Appointment(10, TestData.Anna);
        var api = Api(appointment, [Oil(), Towels()])
            .On(AnswerRoute(appointment, OilId), CareTask(appointment, RoomCareState.Open, [Oil(RestockState.Refilled), Towels()]))
            .On(AnswerRoute(appointment, TowelsId), CareTask(appointment, RoomCareState.Open, [Oil(RestockState.Refilled), Towels(RestockState.Enough)]))
            .On($"PUT /api/appointments/{appointment.Id}/room-care", null, HttpStatusCode.NoContent);
        var vm = await OpenAsync(api);
        await vm.ToggleCareAsync(CareStripe(vm));
        var panel = vm.RoomCarePanel!;

        Assert.False(panel.FinishCommand.CanExecute(null));
        await panel.Rows[0].RefillCommand.ExecuteAsync(null);
        Assert.False(panel.FinishCommand.CanExecute(null));
        await panel.Rows[1].EnoughCommand.ExecuteAsync(null);
        Assert.True(panel.CanFinish);
        Assert.True(panel.FinishCommand.CanExecute(null));
        await panel.FinishCommand.ExecuteAsync(null);

        var put = Assert.Single(api.Calls, c => c.Route == $"PUT /api/appointments/{appointment.Id}/room-care");
        Assert.Contains("\"done\":true", put.Body, StringComparison.Ordinal);
        Assert.Null(vm.RoomCarePanel);
    }

    [Fact]
    public async Task Ein_Streifen_ohne_Zeilen_hakt_wie_bisher_ab()
    {
        var appointment = TestData.Appointment(10, TestData.Anna);
        var api = Api(appointment, [])
            .On($"PUT /api/appointments/{appointment.Id}/room-care", null, HttpStatusCode.NoContent);
        var vm = await OpenAsync(api);

        await vm.ToggleCareAsync(CareStripe(vm));

        Assert.Null(vm.RoomCarePanel);
        Assert.Contains("\"done\":true", Assert.Single(api.Calls, c => c.Route.StartsWith("PUT ", StringComparison.Ordinal)).Body, StringComparison.Ordinal);
        Assert.DoesNotContain("nachfüllen", CareStripe(vm).Tip, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Ohne_Verbindung_wird_nur_angezeigt_und_die_Antwort_meldet_Nur_mit_Verbindung()
    {
        var appointment = TestData.Appointment(10, TestData.Anna);
        var api = Api(appointment, [Oil()]);
        var vm = await OpenAsync(api);
        await vm.ToggleCareAsync(CareStripe(vm));
        var panel = vm.RoomCarePanel!;

        api.Down = true;
        await vm.LoadAsync();
        api.Calls.Clear();
        await panel.Rows[0].RefillCommand.ExecuteAsync(null);

        Assert.NotNull(vm.RoomCarePanel);
        Assert.Contains(vm.Messages, m => m.Text == "Nur mit Verbindung");
        Assert.Empty(api.Calls);
        Assert.True(panel.Rows[0].IsOpen);
    }

    [Fact]
    public async Task Ohne_Recht_Status_aendern_oeffnet_der_Klick_nichts()
    {
        var appointment = TestData.Appointment(10, TestData.Anna);
        var vm = await OpenAsync(Api(appointment, [Oil()]), TestData.Reception() with { Rights = [AccessRight.ViewCalendar] });

        await vm.ToggleCareAsync(CareStripe(vm));

        Assert.Null(vm.RoomCarePanel);
    }

    [Fact]
    public async Task Die_Meldung_zaehlt_Raeume_unter_Mindestbestand_und_die_Aktion_wechselt_in_die_Raumansicht()
    {
        var appointment = TestData.Appointment(10, TestData.Anna);
        var api = Api(appointment, [Oil()], roomsBelowMinimum: 2);
        var vm = new CalendarViewModel(api.Client(), TestData.Reception(), null, new FixedTime());
        await vm.InitializeAsync();
        await vm.LoadAsync();

        var message = Assert.Single(vm.Messages, m => m.Kind == MessageKind.RoomsBelowMinimum);
        Assert.Equal("2 Räume unter Mindestbestand", message.ShortText);
        Assert.Equal(MessageSeverity.Warning, message.Severity);
        Assert.False(vm.ByRoom);

        message.ActionCommand!.Execute(null);

        Assert.True(vm.ByRoom);
    }

    [Fact]
    public async Task Ohne_Raeume_unter_Mindestbestand_gibt_es_die_Meldung_nicht()
    {
        var appointment = TestData.Appointment(10, TestData.Anna);
        var vm = new CalendarViewModel(Api(appointment, []).Client(), TestData.Reception(), null, new FixedTime());
        await vm.InitializeAsync();
        await vm.LoadAsync();

        Assert.DoesNotContain(vm.Messages, m => m.Kind == MessageKind.RoomsBelowMinimum);
    }
}
