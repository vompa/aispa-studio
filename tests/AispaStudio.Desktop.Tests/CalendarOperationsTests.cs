using System.Net;
using AispaStudio.Contracts;
using AispaStudio.Desktop.ViewModels;

namespace AispaStudio.Desktop.Tests;

/// <summary>Betrieb im Haus im Windows-Kalender (ADR 0065): Raumpflege abhaken, Tagesstart, Bereitstellen, Hinweise.</summary>
public sealed class CalendarOperationsTests
{
    private static readonly TimeZoneInfo Vienna = TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna");
    private static readonly string CalendarRoute = $"GET /api/locations/{TestData.LocationId}/calendar?date=2027-03-01";

    private sealed class FixedTime : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => TestData.At(10);

        public override TimeZoneInfo LocalTimeZone => Vienna;
    }

    private static RoomCareTaskDto CareTask(AppointmentDto appointment, RoomCareState state) => new(appointment.Id, [appointment.Id], TestData.Room.Id,
        TestData.Room.Name, "Raum Zeder pflegen bis 11:10", appointment.BlockedUntil, appointment.BlockedUntil.AddMinutes(10), appointment.EmployeeId, state,
        state == RoomCareState.Done ? TestData.At(11, 2) : null, state == RoomCareState.Done ? "Rita" : null);

    private static RoomDayStateDto RoomState(RoomStartState start, int items, int checkedItems, int careOpen = 0, int careOverdue = 0) =>
        new(TestData.Room.Id, TestData.Room.Name, start, items, checkedItems, null, null, careOpen, careOverdue, null, null);

    private static CalendarDay DayWith(AppointmentDto appointment, RoomCareState state, RoomDayStateDto room) =>
        TestData.Day(appointment) with { RoomCare = new RoomCareDay([CareTask(appointment, state)], [room]) };

    private static StubApi Api(CalendarDay day) => new StubApi()
        .On("GET /api/locations", new List<LocationDto> { TestData.Location })
        .On($"GET /api/locations/{TestData.LocationId}/resources", TestData.Resources)
        .On(CalendarRoute, day);

    private static async Task<CalendarViewModel> OpenAsync(StubApi api, CurrentUser? user = null, bool byRoom = false)
    {
        var vm = new CalendarViewModel(api.Client(), user ?? TestData.Reception(), null, new FixedTime());
        await vm.InitializeAsync();
        await vm.LoadAsync();
        if (byRoom)
        {
            vm.ShowRoomsCommand.Execute(null);
        }

        return vm;
    }

    private static async Task Until(Func<bool> condition)
    {
        for (var i = 0; i < 200 && !condition(); i++)
        {
            await Task.Delay(10);
        }

        Assert.True(condition());
    }

    [Fact]
    public void Raumpflege_Streifen_tragen_den_Zustand()
    {
        var overdue = TestData.Appointment(10, TestData.Anna);
        var done = TestData.Appointment(13, TestData.Anna);
        var day = TestData.Day(overdue, done) with
        {
            RoomCare = new RoomCareDay([CareTask(overdue, RoomCareState.Overdue), CareTask(done, RoomCareState.Done)],
                [RoomState(RoomStartState.Partial, 3, 1, careOpen: 0, careOverdue: 1)]),
        };

        var room = DayLayout.Build(day, TestData.Resources, byRoom: true, Vienna).Columns.Single(c => c.Id == TestData.Room.Id);

        var stripes = room.Stripes.Where(s => s.HasCare).ToList();
        Assert.Equal([RoomCareState.Overdue, RoomCareState.Done], stripes.Select(s => s.Care!.State));
        Assert.True(stripes[0].IsCareOverdue);
        Assert.True(stripes[1].IsCareDone);
        Assert.Contains("überfällig", stripes[0].Tip, StringComparison.Ordinal);
        Assert.Contains("erledigt von Rita um 11:02", stripes[1].Tip, StringComparison.Ordinal);
        // Der Streifen trägt kein Kästchen mehr; der Hinweis sagt, dass ein Klick abhakt bzw. zurücknimmt.
        Assert.EndsWith("\nKlick: Raumpflege abhaken", stripes[0].Tip, StringComparison.Ordinal);
        Assert.EndsWith("\nKlick: Raumpflege zurücknehmen", stripes[1].Tip, StringComparison.Ordinal);
    }

    [Fact]
    public void Ohne_Raumpflege_Daten_gibt_es_keine_Streifen()
    {
        var day = TestData.Day(TestData.Appointment(10, TestData.Anna));

        var room = DayLayout.Build(day, TestData.Resources, byRoom: true, Vienna).Columns.Single(c => c.Id == TestData.Room.Id);

        Assert.All(room.Stripes, s => Assert.False(s.HasCare));
    }

    [Fact]
    public async Task Ein_Klick_auf_den_Streifen_hakt_die_Raumpflege_ab_und_laedt_den_Tag_neu()
    {
        var appointment = TestData.Appointment(10, TestData.Anna);
        var api = Api(DayWith(appointment, RoomCareState.Open, RoomState(RoomStartState.NoChecklist, 0, 0, careOpen: 1)))
            .On($"PUT /api/appointments/{appointment.Id}/room-care", null, HttpStatusCode.NoContent);
        var vm = await OpenAsync(api, byRoom: true);
        var stripe = vm.Columns.Single(c => c.Id == TestData.Room.Id).Stripes.Single(s => s.HasCare);
        var loads = api.Calls.Count(c => c.Route == CalendarRoute);

        await vm.ToggleCareAsync(stripe);

        var put = Assert.Single(api.Calls, c => c.Route.StartsWith("PUT ", StringComparison.Ordinal));
        Assert.Contains("\"done\":true", put.Body, StringComparison.Ordinal);
        Assert.True(api.Calls.Count(c => c.Route == CalendarRoute) > loads);
    }

    [Fact]
    public async Task Ein_erledigter_Streifen_nimmt_den_Haken_zurueck()
    {
        var appointment = TestData.Appointment(10, TestData.Anna);
        var api = Api(DayWith(appointment, RoomCareState.Done, RoomState(RoomStartState.NoChecklist, 0, 0)))
            .On($"PUT /api/appointments/{appointment.Id}/room-care", null, HttpStatusCode.NoContent);
        var vm = await OpenAsync(api, byRoom: true);
        var stripe = vm.Columns.Single(c => c.Id == TestData.Room.Id).Stripes.Single(s => s.HasCare);

        Assert.Equal(["Raumpflege zurücknehmen", "Zeiten dieses Termins ändern …"], vm.ActionsForStripe(stripe).Select(a => a.Header));
        await vm.ToggleCareAsync(stripe);

        Assert.Contains("\"done\":false", Assert.Single(api.Calls, c => c.Route.StartsWith("PUT ", StringComparison.Ordinal)).Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Ohne_Recht_Status_aendern_gibt_es_kein_Abhaken()
    {
        var appointment = TestData.Appointment(10, TestData.Anna);
        var api = Api(DayWith(appointment, RoomCareState.Open, RoomState(RoomStartState.NoChecklist, 0, 0, careOpen: 1)));
        var viewOnly = TestData.Reception() with { Rights = [AccessRight.ViewCalendar] };
        var vm = await OpenAsync(api, viewOnly, byRoom: true);
        var stripe = vm.Columns.Single(c => c.Id == TestData.Room.Id).Stripes.Single(s => s.HasCare);

        await vm.ToggleCareAsync(stripe);

        Assert.False(vm.CanCheckCare);
        Assert.DoesNotContain(api.Calls, c => c.Route.StartsWith("PUT ", StringComparison.Ordinal));
        Assert.Empty(vm.ActionsForStripe(stripe));
    }

    // ---------- Betriebsleiste ----------

    private static RoomStartDayDto StartDay(RoomStartState state, bool firstDone = false, DateTimeOffset? approvedAt = null) => new(TestData.LocationId,
        TestData.Monday,
        [new RoomStartRoomDto(TestData.Room.Id, TestData.Room.Name, state,
            [new RoomStartItemStateDto(Guid.Parse("00000000-0000-0000-0000-0000000000a1"), "Temperatur prüfen", firstDone, firstDone ? TestData.At(8, 30) : null, firstDone ? "Rita" : null),
             new RoomStartItemStateDto(Guid.Parse("00000000-0000-0000-0000-0000000000a2"), "Handtücher auffüllen", state is RoomStartState.Checked or RoomStartState.Approved, null, null)],
            approvedAt, approvedAt is null ? null : "Marta")]);

    private static StubApi WithOperations(StubApi api, RoomStartDayDto? start = null) => api
        .On($"GET /api/locations/{TestData.LocationId}/room-start?date=2027-03-01", start ?? StartDay(RoomStartState.Partial, firstDone: true))
        .On($"GET /api/locations/{TestData.LocationId}/servings?date=2027-03-01", new ServingDayDto(TestData.LocationId, TestData.Monday, [],
            [new ServingTotalDto("Früchteteller", 6, [TestData.At(11, 20), TestData.At(12, 40)]), new ServingTotalDto("Tee", 4, [TestData.At(10)])]))
        .On("GET /api/handbook/reminders?date=2027-03-01", new List<HandbookEntryDto>
        {
            new(Guid.NewGuid(), "Gäste mit Hund", "Hund im Eingang anbinden.", "Empfang", true, [], true, TestData.At(8)),
        })
        .On($"GET /api/stock/locations/{TestData.LocationId}/demand?from=2027-03-01&until=2027-03-01", new StockDemandDto(TestData.LocationId, TestData.Monday,
            TestData.Monday, 3, [], [new StockDemandLine(Guid.NewGuid(), "Massageöl", "l", 1, 3, 0, 3, 0, StockWarningKind.Insufficient, TestData.At(14),
                "Massageöl: Bestand reicht nur bis 14:00")]));

    [Fact]
    public async Task Die_Betriebsdaten_werden_zu_Meldungen_Lager_und_Tagesstart_als_Warnung_das_Handbuch_als_Hinweis()
    {
        var api = WithOperations(Api(TestData.Day(TestData.Appointment(10, TestData.Anna))));

        var vm = await OpenAsync(api);

        var operations = Assert.IsType<DayOperationsViewModel>(vm.Operations);
        Assert.Equal([MessageKind.Stock, MessageKind.DayStart, MessageKind.Reminder], vm.Messages.Select(m => m.Kind));
        Assert.Equal([MessageSeverity.Warning, MessageSeverity.Warning, MessageSeverity.Notice], vm.Messages.Select(m => m.Severity));
        Assert.Equal("Massageöl: Bestand reicht nur bis 14:00", vm.Messages[0].Text);
        Assert.Equal("Raum Zeder: 1 von 2 Punkten abgehakt.", vm.Messages[1].Text);
        Assert.Equal("Gäste mit Hund", vm.Messages[2].Title);
        Assert.Equal("Massageöl: Bestand reicht nur bis 14:00", operations.StockWarnings.Single().Message);
        Assert.Equal("Gäste mit Hund", operations.Reminders.Single().Title);
    }

    [Fact]
    public async Task Ohne_Antworten_der_Betriebsdaten_gibt_es_keine_Leiste()
    {
        var vm = await OpenAsync(Api(TestData.Day(TestData.Appointment(10, TestData.Anna))));

        Assert.Empty(vm.Messages);
    }

    [Fact]
    public async Task Ohne_Buchungsrecht_fragt_die_Leiste_die_Bedarfspruefung_nicht_ab()
    {
        var api = WithOperations(Api(TestData.Day(TestData.Appointment(10, TestData.Anna))));
        var viewOnly = TestData.Reception() with { Rights = [AccessRight.ViewCalendar, AccessRight.ChangeStatus] };

        var vm = await OpenAsync(api, viewOnly);

        Assert.DoesNotContain(api.Calls, c => c.Route.Contains("/demand", StringComparison.Ordinal));
        Assert.Empty(vm.Operations!.StockWarnings);
    }

    [Fact]
    public async Task Ein_Punkt_des_Tagesstarts_wird_abgehakt_und_der_Stand_neu_geladen()
    {
        var itemId = Guid.Parse("00000000-0000-0000-0000-0000000000a2");
        var api = WithOperations(Api(TestData.Day(TestData.Appointment(10, TestData.Anna))))
            .On($"PUT /api/rooms/{TestData.Room.Id}/start-checks/{itemId}", null, HttpStatusCode.NoContent);
        var vm = await OpenAsync(api);
        var row = vm.Operations!.Rooms.Single().Items[1];
        var loaded = api.Calls.Count(c => c.Route.Contains("/room-start", StringComparison.Ordinal));

        Assert.True(row.CanCheck);
        await row.ToggleCommand.ExecuteAsync(null);

        var put = Assert.Single(api.Calls, c => c.Route.StartsWith("PUT ", StringComparison.Ordinal));
        Assert.Contains("\"date\":\"2027-03-01\"", put.Body, StringComparison.Ordinal);
        Assert.Contains("\"done\":true", put.Body, StringComparison.Ordinal);
        Assert.Equal(loaded + 1, api.Calls.Count(c => c.Route.Contains("/room-start", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task Der_Tagesstart_laesst_sich_nicht_im_Voraus_abhaken()
    {
        var api = WithOperations(Api(TestData.Day(TestData.Appointment(10, TestData.Anna))));
        var vm = await OpenAsync(api);
        var tomorrow = new CalendarDay(TestData.LocationId, TestData.Monday.AddDays(1), "Europe/Vienna", [], [], [], []);
        api.On($"GET /api/locations/{TestData.LocationId}/calendar?date=2027-03-02", tomorrow)
            .On($"GET /api/locations/{TestData.LocationId}/room-start?date=2027-03-02", StartDay(RoomStartState.Open) with { Date = TestData.Monday.AddDays(1) });

        vm.NextDayCommand.Execute(null);
        await Until(() => vm.Operations?.Rooms.Count > 0 && vm.Date == TestData.Monday.AddDays(1));

        Assert.All(vm.Operations!.Rooms.Single().Items, i => Assert.False(i.CanCheck));
    }

    [Fact]
    public async Task Die_Abnahme_macht_die_Leitung_selbst_oder_mit_Name_und_PIN()
    {
        var api = WithOperations(Api(TestData.Day(TestData.Appointment(10, TestData.Anna))), StartDay(RoomStartState.Checked))
            .On($"POST /api/rooms/{TestData.Room.Id}/approvals", new RoomApprovalDto(TestData.Room.Id, TestData.Monday, RoomApprovalKind.DayStart, TestData.At(10), "Marta"))
            .On($"GET /api/locations/{TestData.LocationId}/room-approvers", new List<QuickSwitchPerson> { new("u-marta", "Marta Leitner", StaffRole.Manager) });
        var manager = TestData.Reception() with { Rights = [.. TestData.Reception().Rights, AccessRight.ManageLocation] };

        // Mit dem Recht „Standort verwalten“ gleich ohne PIN.
        var direct = await OpenAsync(api, manager);
        var room = direct.Operations!.Rooms.Single();
        Assert.True(room.CanApprove);
        await room.ApproveCommand.ExecuteAsync(null);
        var body = Assert.Single(api.Calls, c => c.Route.StartsWith("POST /api/rooms/", StringComparison.Ordinal)).Body!;
        Assert.Contains("\"kind\":\"DayStart\"", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("pin", body, StringComparison.OrdinalIgnoreCase);

        // Ohne das Recht fragt die Leiste nach der Leitung und der PIN und sendet erst nach „Abnehmen“.
        api.Calls.Clear();
        var reception = await OpenAsync(api);
        var operations = reception.Operations!;
        await operations.Rooms.Single().ApproveCommand.ExecuteAsync(null);
        Assert.True(operations.IsAskingApproval);
        Assert.Equal("Marta Leitner", operations.Approver!.DisplayName);
        Assert.False(operations.ConfirmApprovalCommand.CanExecute(null));
        operations.ApprovalPin = "1234";
        Assert.True(operations.ConfirmApprovalCommand.CanExecute(null));
        Assert.DoesNotContain(api.Calls, c => c.Route.StartsWith("POST /api/rooms/", StringComparison.Ordinal));

        await operations.ConfirmApprovalCommand.ExecuteAsync(null);

        var pinBody = Assert.Single(api.Calls, c => c.Route.StartsWith("POST /api/rooms/", StringComparison.Ordinal)).Body!;
        Assert.Contains("u-marta", pinBody, StringComparison.Ordinal);
        Assert.Contains("1234", pinBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Ein_abgenommener_Raum_zeigt_wer_abgenommen_hat_und_sperrt_die_Haken()
    {
        var api = WithOperations(Api(TestData.Day(TestData.Appointment(10, TestData.Anna))), StartDay(RoomStartState.Approved, approvedAt: TestData.At(9, 5)));

        var vm = await OpenAsync(api);

        var room = vm.Operations!.Rooms.Single();
        Assert.Equal("abgenommen von Marta", room.StateText);
        Assert.False(room.CanApprove);
        Assert.All(room.Items, i => Assert.False(i.CanCheck));
        Assert.DoesNotContain(vm.Messages, m => m.Kind == MessageKind.DayStart);
    }

    // ---------- Bereitstellen im Termin und in der Hover-Karte ----------

    private static StubApi WithServings(AppointmentDto appointment, StubApi api)
    {
        var extra1 = Guid.NewGuid();
        var extra2 = Guid.NewGuid();
        ServingItemDto Item(Guid extra, string text, int minute, bool done = false) => new(appointment.Id, extra, text, TestData.At(11, minute), "Massage",
            TestData.Room.Id, TestData.Room.Name, appointment.CustomerId, appointment.CustomerName, "Rooibos", true, done, null, null);
        return api.On($"GET /api/locations/{TestData.LocationId}/servings?date=2027-03-01", new ServingDayDto(TestData.LocationId, TestData.Monday,
                [Item(extra1, "Tee", 20), Item(extra2, "Früchteteller", 20)], [new ServingTotalDto("Tee", 1, [TestData.At(11, 20)])]))
            .On($"PUT /api/appointments/{appointment.Id}/extras/{extra1}/check", null, HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Das_Terminfenster_zeigt_Bereitstellen_mit_Haken_Wunsch_und_nur_den_Hinweis_auf_die_Anamnese()
    {
        var appointment = TestData.Appointment(11, TestData.Anna);
        var api = WithServings(appointment, Api(TestData.Day(appointment)));
        var vm = await OpenAsync(api);

        vm.OpenAppointment(appointment);

        var servings = vm.Selected!.Servings!;
        Assert.True(vm.Selected.HasServings);
        Assert.Equal(["Tee", "Früchteteller"], servings.Rows.Select(r => r.Text));
        Assert.Equal("Tee 11:20, Früchteteller 11:20", servings.Summary);
        Assert.Equal("Wunsch des Gastes: Rooibos", servings.WishesText);
        Assert.True(servings.AnamnesisOnFile);
        Assert.Contains("Bereitstellen: Tee 11:20, Früchteteller 11:20", vm.Columns.SelectMany(c => c.Items).Single().HoverText, StringComparison.Ordinal);

        await servings.Rows[0].ToggleCommand.ExecuteAsync(null);

        Assert.Contains("\"done\":true", Assert.Single(api.Calls, c => c.Route.StartsWith("PUT ", StringComparison.Ordinal)).Body, StringComparison.Ordinal);
    }

    // ---------- Hinweise nach Buchen und Einchecken ----------

    [Fact]
    public async Task Hinweise_nach_dem_Einchecken_bleiben_sichtbar_bis_sie_geschlossen_werden()
    {
        var appointment = TestData.Appointment(10, TestData.Anna);
        var warned = appointment with
        {
            Status = AppointmentStatus.CheckedIn,
            UpdatedAt = TestData.At(9, 59),
            Warnings = ["Der Tagesstart von Raum Zeder ist nicht abgeschlossen (0 von 3 Punkten).", "Die Raumpflege nach dem vorigen Termin ist nicht abgehakt."],
        };
        var api = Api(TestData.Day(appointment)).On($"POST /api/appointments/{appointment.Id}/status", warned);
        var time = new ManualTime(TestData.At(10));
        var vm = new CalendarViewModel(api.Client(), TestData.Reception(), null, time);
        await vm.InitializeAsync();
        await vm.LoadAsync();
        vm.OpenAppointment(appointment);

        await vm.Selected!.CheckInCommand.ExecuteAsync(null);

        var notice = Assert.Single(vm.Messages);
        Assert.Equal(MessageSeverity.Warning, notice.Severity);
        Assert.Contains("0 von 3 Punkten", notice.Text, StringComparison.Ordinal);
        Assert.Contains("Raumpflege nach dem vorigen Termin", notice.Text, StringComparison.Ordinal);
        time.Advance(TimeSpan.FromSeconds(30));
        await Task.Delay(50, TestContext.Current.CancellationToken);
        Assert.Single(vm.Messages);
        notice.DismissCommand!.Execute(null);
        Assert.Empty(vm.Messages);
    }

    [Fact]
    public async Task Hinweise_der_Antwort_auf_das_Buchen_erscheinen_auch_aus_dem_Assistenten()
    {
        var api = Api(TestData.Day());
        var vm = await OpenAsync(api);
        var booked = TestData.Appointment(15, TestData.Anna) with { Warnings = ["Massageöl: Bestand reicht nicht für alle gebuchten Termine."] };

        vm.ApplyBooked(booked);

        Assert.Equal("Massageöl: Bestand reicht nicht für alle gebuchten Termine.", Assert.Single(vm.Messages).Text);
        Assert.Contains(vm.Columns.SelectMany(c => c.Items), i => i.Id == booked.Id);
    }
}
