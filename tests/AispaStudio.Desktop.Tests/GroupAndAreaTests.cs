using System.Net;
using System.Text.Json;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;
using AispaStudio.Desktop.ViewModels;

namespace AispaStudio.Desktop.Tests;

public sealed class GroupAndAreaTests
{
    private static readonly TimeZoneInfo Vienna = TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna");
    private static readonly RoomDto Sauna = new(Guid.NewGuid(), TestData.LocationId, "Finnische Sauna", 0, Places: 12, IsArea: true);
    private static readonly LocationResources Resources = TestData.Resources with { Rooms = [TestData.Room, Sauna] };
    private static readonly CustomerDto Eva = new(Guid.NewGuid(), "Eva", "Muster", null, "+43 664 1", null, null);
    private static readonly CustomerDto Paul = new(Guid.NewGuid(), "Paul", "Muster", null, "+43 664 2", null, null);

    private sealed class FixedTime : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => TestData.At(10);

        public override TimeZoneInfo LocalTimeZone => Vienna;
    }

    private static StubApi Api(params AppointmentDto[] appointments) => new StubApi()
        .On("GET /api/locations", new List<LocationDto> { TestData.Location })
        .On($"GET /api/locations/{TestData.LocationId}/resources", Resources)
        .On($"GET /api/locations/{TestData.LocationId}/calendar?date=2027-03-01", TestData.Day(appointments))
        .On("GET /api/customers?search=eva", new List<CustomerDto> { Eva })
        .On("GET /api/customers?search=paul", new List<CustomerDto> { Paul });

    private static async Task<CalendarViewModel> OpenAsync(StubApi api)
    {
        var vm = new CalendarViewModel(api.Client(), TestData.Reception(), null, new FixedTime());
        await vm.InitializeAsync();
        await vm.LoadAsync();
        return vm;
    }

    private static string Availability => $"POST /api/locations/{TestData.LocationId}/appointment-groups/availability";

    private static GroupSlot Slot(int hour) => new(TestData.At(hour), TestData.At(hour, 50), SharedRoom: true,
    [
        new GroupSlotItem(TestData.At(hour), TestData.At(hour, 50), TestData.Anna.Id, TestData.Room.Id, null),
        new GroupSlotItem(TestData.At(hour), TestData.At(hour, 50), TestData.Ben.Id, TestData.Room.Id, null),
    ]);

    private static AppointmentDto Grouped(AppointmentDto a, Guid groupId, CustomerDto customer) =>
        a with { GroupId = groupId, CustomerId = customer.Id, CustomerName = $"{customer.FirstName} {customer.LastName}" };

    [Fact]
    public async Task Paarbuchung_sucht_Zeiten_mit_Platzhaltern_und_bucht_beide_Gaeste_gemeinsam()
    {
        var api = Api().On(Availability, new List<GroupSlot> { Slot(13), Slot(15) });
        var vm = await OpenAsync(api);
        var groupId = Guid.NewGuid();
        var booked = new GroupBookingResult(groupId,
        [
            Grouped(TestData.Appointment(15, TestData.Anna), groupId, Eva),
            Grouped(TestData.Appointment(15, TestData.Ben), groupId, Paul),
        ]);
        api.On("POST /api/appointment-groups", booked, HttpStatusCode.Created);

        vm.OpenGroupBookingCommand.Execute(null);
        var group = vm.GroupBooking!;
        await group.SearchSlotsAsync();
        var search = JsonSerializer.Deserialize<GroupAvailabilityRequest>(api.Calls.Last(c => c.Route == Availability).Body!, ApiClient.Json)!;
        var cannotBookYet = group.BookCommand.CanExecute(null);

        await group.Guests[0].Search.SearchAsync("eva");
        group.Guests[0].Search.Customer = group.Guests[0].Search.Results[0];
        await group.Guests[1].Search.SearchAsync("paul");
        group.Guests[1].Search.Customer = group.Guests[1].Search.Results[0];
        group.Slot = group.Slots[1];
        var plan = group.Plan;
        await group.BookCommand.ExecuteAsync(null);

        Assert.Equal(2, search.Items.Count);
        Assert.All(search.Items, i => Assert.Equal(TestData.Facial.Id, i.TreatmentId));
        Assert.Equal(2, search.Items.Select(i => i.CustomerId).Distinct().Count());
        Assert.DoesNotContain(search.Items, i => i.CustomerId == Eva.Id);
        Assert.Equal(15, search.GapMinutes);
        Assert.False(cannotBookYet);
        Assert.Equal(["13:00", "15:00"], group.Slots.Select(s => s.Text));
        Assert.Equal("15:00–15:50  Eva: Gesicht bei Anna, Raum Zeder", plan[0]);
        Assert.Equal("15:00–15:50  Paul: Gesicht bei Ben, Raum Zeder", plan[1]);

        var request = JsonSerializer.Deserialize<GroupBookingRequest>(api.Calls.Last().Body!, ApiClient.Json)!;
        Assert.Equal(TestData.At(15), request.Start);
        Assert.Equal([Eva.Id, Paul.Id], request.Items.Select(i => i.CustomerId));
        Assert.Null(vm.GroupBooking);
        var items = vm.Columns.SelectMany(c => c.Items).ToList();
        Assert.Equal(2, items.Count);
        Assert.All(items, i => Assert.Contains("Paar mit ", i.MetaText, StringComparison.Ordinal));
        Assert.Equal("Gemeinsam gebucht: 2 Termine ab 15:00", vm.Toast);
    }

    [Fact]
    public async Task Gast_ohne_Namen_buchen_im_Kalender_zeigen_und_spaeter_nachtragen()
    {
        var api = Api().On(Availability, new List<GroupSlot> { Slot(13) });
        var vm = await OpenAsync(api);
        var groupId = Guid.NewGuid();
        var placeholder = Grouped(TestData.Appointment(13, TestData.Ben), groupId, Eva) with { PlaceholderGuest = 2 };
        api.On("POST /api/appointment-groups",
            new GroupBookingResult(groupId, [Grouped(TestData.Appointment(13, TestData.Anna), groupId, Eva), placeholder]), HttpStatusCode.Created);

        vm.OpenGroupBookingCommand.Execute(null);
        var group = vm.GroupBooking!;
        group.Guests[1].NameLater = true;
        Assert.Equal("Mindestens ein Gast braucht einen Namen: wer bucht.", group.NameLaterNote);
        await group.Guests[0].Search.SearchAsync("eva");
        group.Guests[0].Search.Customer = group.Guests[0].Search.Results[0];
        await group.SearchSlotsAsync();
        Assert.StartsWith("Gäste ohne Namen laufen auf Eva Muster.", group.NameLaterNote, StringComparison.Ordinal);
        Assert.Equal("13:00–13:50  Gast 2: Gesicht bei Ben, Raum Zeder", group.Plan[1]);
        await group.BookCommand.ExecuteAsync(null);

        var request = JsonSerializer.Deserialize<GroupBookingRequest>(api.Calls.Last().Body!, ApiClient.Json)!;
        Assert.Equal([(Eva.Id, (int?)null), (Eva.Id, 2)], request.Items.Select(i => (i.CustomerId, i.PlaceholderGuest)));
        var item = vm.Columns.SelectMany(c => c.Items).Single(i => i.Id == placeholder.Id);
        Assert.Equal("Gast 2 · Name folgt", item.Title);
        Assert.StartsWith("Gast 2, Name folgt (gebucht von Eva Muster)", item.HoverText, StringComparison.Ordinal);
        Assert.Contains(vm.ActionsFor(item), a => a.Header == "Namen nachtragen");

        vm.OpenAppointment(placeholder);
        var panel = vm.Selected!;
        Assert.True(panel.CanNameGuest);
        Assert.False(panel.NameGuestCommand.CanExecute(null));
        await panel.GuestSearch.SearchAsync("paul");
        panel.GuestSearch.Customer = panel.GuestSearch.Results[0];
        var named = Grouped(placeholder, groupId, Paul) with { PlaceholderGuest = null, UpdatedAt = TestData.At(9) };
        api.On($"PUT /api/appointments/{placeholder.Id}/guest", new GroupBookingResult(groupId, [named]));
        await panel.NameGuestCommand.ExecuteAsync(null);

        Assert.Equal(Paul.Id, JsonSerializer.Deserialize<AssignGuestRequest>(api.Calls.Last(c => c.Body is not null).Body!, ApiClient.Json)!.CustomerId);
        Assert.Equal("Paul Muster", vm.Columns.SelectMany(c => c.Items).Single(i => i.Id == placeholder.Id).Title);
    }

    [Fact]
    public async Task Paarbuchung_ohne_Verbindung_mit_eigenem_Beginn_vormerken_und_beim_Abgleich_senden()
    {
        var api = Api();
        var cache = new OfflineCache(new MemoryOfflineStore(), new FixedTime());
        var client = api.Client(cache);
        var vm = new CalendarViewModel(client, TestData.Reception(), null, new FixedTime());
        await vm.InitializeAsync();
        await vm.LoadAsync();
        vm.OpenGroupBookingCommand.Execute(null);
        var group = vm.GroupBooking!;
        await group.Guests[0].Search.SearchAsync("eva");
        group.Guests[0].Search.Customer = group.Guests[0].Search.Results[0];
        await group.Guests[1].Search.SearchAsync("paul");
        group.Guests[1].Search.Customer = group.Guests[1].Search.Results[0];

        api.Down = true;
        await group.SearchSlotsAsync();
        Assert.True(group.IsOffline);
        Assert.StartsWith("Ohne Verbindung: Beginn selbst eintragen", group.SlotsNote, StringComparison.Ordinal);
        group.OfflineStartText = "14:30";
        Assert.Equal("14:30 vormerken", group.BookText);
        await group.BookCommand.ExecuteAsync(null);

        var pending = Assert.Single(cache.Pending);
        var request = JsonSerializer.Deserialize<GroupBookingRequest>(pending.Body, ApiClient.Json)!;
        Assert.Null(vm.GroupBooking);
        Assert.Equal("Ohne Verbindung vorgemerkt, Abgleich folgt", vm.Toast);
        Assert.Equal("api/appointment-groups", pending.Path);
        Assert.Equal(TestData.At(14, 30), request.Start);
        Assert.Equal([Eva.Id, Paul.Id], request.Items.Select(i => i.CustomerId));
        Assert.NotNull(request.ClientId);
        Assert.Contains("Eva Muster und Paul Muster", pending.Description, StringComparison.Ordinal);

        api.Down = false;
        var groupId = request.ClientId!.Value;
        api.On("POST /api/appointment-groups", new GroupBookingResult(groupId,
        [
            Grouped(TestData.Appointment(14, TestData.Anna), groupId, Eva),
            Grouped(TestData.Appointment(14, TestData.Ben), groupId, Paul),
        ]), HttpStatusCode.Created);
        Assert.Equal(1, await client.SyncAsync(TestContext.Current.CancellationToken));
        Assert.Empty(cache.Pending);
        Assert.Empty(cache.Failed);
    }

    [Fact]
    public async Task Spa_Tag_nummeriert_Abschnitte_sucht_erst_ab_zwei_Behandlungen_und_meldet_volle_Tage()
    {
        var api = Api().On(Availability, new List<GroupSlot>());
        var vm = await OpenAsync(api);
        vm.OpenGroupBookingCommand.Execute(null);
        var group = vm.GroupBooking!;

        group.Guests[1].RemoveCommand.Execute(null);
        await group.SearchSlotsAsync();
        var tooFew = group.SlotsNote;
        group.AddStepCommand.Execute(null);
        group.Steps[1].Entries[0].Choice = group.Steps[1].Entries[0].Choices.First(c => c.Id == TestData.MassageTreatment.Id);
        group.Gap = 30;
        await group.SearchSlotsAsync();

        Assert.Equal("Mindestens zwei Teile wählen, davon eine Behandlung.", tooFew);
        Assert.Equal(["Abschnitt 1", "Abschnitt 2"], group.Steps.Select(s => s.Title));
        Assert.True(group.ShowGap);
        var search = JsonSerializer.Deserialize<GroupAvailabilityRequest>(api.Calls.Last(c => c.Route == Availability).Body!, ApiClient.Json)!;
        Assert.Equal([0, 1], search.Items.Select(i => i.Step));
        Assert.Equal(30, search.GapMinutes);
        Assert.Equal("An diesem Tag ist keine gemeinsame Zeit frei. Anderen Tag wählen.", group.SlotsNote);
        Assert.Single(group.Guests);
        Assert.False(group.Guests[0].CanRemove);
    }

    [Fact]
    public async Task Datum_der_Paarbuchung_beginnt_heute_und_ein_anderer_Tag_laedt_die_freien_Zeiten_neu()
    {
        var api = Api().On(Availability, new List<GroupSlot> { Slot(13) });
        var vm = await OpenAsync(api);
        vm.OpenGroupBookingCommand.Execute(null);
        var group = vm.GroupBooking!;
        await group.SearchSlotsAsync();

        Assert.Equal(new DateOnly(2027, 3, 1), group.Today);
        Assert.Equal("01.03.2027", group.DateText);

        group.DateText = "02.03.2027";
        await group.SearchSlotsAsync();
        var next = JsonSerializer.Deserialize<GroupAvailabilityRequest>(api.Calls.Last(c => c.Route == Availability).Body!, ApiClient.Json)!;
        Assert.Equal(new DateOnly(2027, 3, 2), next.Date);

        var searches = api.Calls.Count(c => c.Route == Availability);
        group.DateText = "28.02.2027";
        await group.SearchSlotsAsync();
        Assert.Equal(searches, api.Calls.Count(c => c.Route == Availability));
        Assert.Equal("Verfügbar, sobald ein Tag ab heute gewählt ist.", group.BookBlocker);
    }

    [Fact]
    public async Task Freie_Zeiten_werden_erst_gesucht_wenn_jeder_Gast_eine_Behandlung_hat()
    {
        var api = Api().On(Availability, new List<GroupSlot> { Slot(13) });
        var vm = await OpenAsync(api);
        vm.OpenGroupBookingCommand.Execute(null);
        var group = vm.GroupBooking!;
        await group.SearchSlotsAsync();
        var searches = api.Calls.Count(c => c.Route == Availability);

        group.Steps[0].Entries[1].Choice = TreatmentChoice.None;
        await group.SearchSlotsAsync();

        Assert.Equal("Bitte erst für jeden Gast eine Behandlung wählen.", group.SlotsNote);
        Assert.Empty(group.Slots);
        Assert.Equal(searches, api.Calls.Count(c => c.Route == Availability));

        group.Steps[0].Entries[1].Choice = group.Steps[0].Entries[1].Choices[1];
        await group.SearchSlotsAsync();
        Assert.Null(group.SlotsNote);
        Assert.NotEmpty(group.Slots);
    }

    [Fact]
    public async Task Gesperrter_Knopf_nennt_den_ersten_fehlenden_Punkt()
    {
        var api = Api().On(Availability, new List<GroupSlot>());
        var vm = await OpenAsync(api);
        vm.OpenGroupBookingCommand.Execute(null);
        var group = vm.GroupBooking!;
        await group.SearchSlotsAsync();

        Assert.Equal("Verfügbar, sobald Gast 1 einen Namen hat oder „Name folgt“ gilt.", group.BookBlocker);
        await group.Guests[0].Search.SearchAsync("eva");
        group.Guests[0].Search.Customer = group.Guests[0].Search.Results[0];
        Assert.Equal("Verfügbar, sobald Gast 2 einen Namen hat oder „Name folgt“ gilt.", group.BookBlocker);
        await group.Guests[1].Search.SearchAsync("paul");
        group.Guests[1].Search.Customer = group.Guests[1].Search.Results[0];
        await group.SearchSlotsAsync();
        Assert.Equal("An diesem Tag ist keine gemeinsame Zeit frei. Anderen Tag wählen.", group.SlotsNote);
        Assert.Equal("Verfügbar, sobald eine gemeinsame Zeit frei ist; anderen Tag wählen.", group.BookBlocker);

        group.Steps[0].Entries[1].Choice = TreatmentChoice.None;
        Assert.Equal("Verfügbar, sobald für Paul Muster eine Behandlung gewählt ist.", group.BookBlocker);
        Assert.Equal("Paul Muster", group.Guests[1].FullName);

        group.Steps[0].Entries[1].Choice = group.Steps[0].Entries[1].Choices[1];
        group.Guests[1].NameLater = true;
        Assert.Equal("Gast 2", group.Guests[1].FullName);
    }

    [Fact]
    public async Task Baukasten_nimmt_die_Sauna_als_Teil_laesst_den_Planer_ordnen_und_zeigt_den_Ablauf()
    {
        var slot = new GroupSlot(TestData.At(10), TestData.At(11, 55), SharedRoom: false,
            [new GroupSlotItem(TestData.At(11, 5), TestData.At(11, 55), TestData.Anna.Id, TestData.Room.Id, null)],
            [new GroupSlotArea(TestData.At(10), TestData.At(11), Sauna.Id, Eva.Id)]);
        var api = Api().On(Availability, new List<GroupSlot> { slot });
        var vm = await OpenAsync(api);
        vm.OpenGroupBookingCommand.Execute(null);
        var group = vm.GroupBooking!;
        group.Guests[1].RemoveCommand.Execute(null);
        await group.Guests[0].Search.SearchAsync("eva");
        group.Guests[0].Search.Customer = group.Guests[0].Search.Results[0];
        group.Steps[0].Entries[0].Choice = group.Steps[0].Entries[0].Choices.First(c => c.Id == TestData.MassageTreatment.Id);
        group.AddStepCommand.Execute(null);
        var saunaChoice = group.Steps[1].Entries[0].Choices.First(c => c.AreaId == Sauna.Id && c.Minutes == 60);
        group.Steps[1].Entries[0].Choice = saunaChoice;
        group.FlexibleOrder = true;
        await group.SearchSlotsAsync();

        var search = JsonSerializer.Deserialize<GroupAvailabilityRequest>(api.Calls.Last(c => c.Route == Availability).Body!, ApiClient.Json)!;
        Assert.Equal("Finnische Sauna · 60 Min.", saunaChoice.Text);
        Assert.True(group.ShowFlexible);
        Assert.True(search.FlexibleOrder);
        Assert.Equal(TestData.MassageTreatment.Id, Assert.Single(search.Items).TreatmentId);
        var area = Assert.Single(search.Areas!);
        Assert.Equal((Sauna.Id, 1, 60), (area.RoomId, area.Step, area.Minutes));
        Assert.Equal(["10:00–11:00  Eva: Finnische Sauna", "11:05–11:55  Eva: Massage bei Anna, Raum Zeder"], group.Plan);

        var groupId = Guid.NewGuid();
        var massage = Grouped(TestData.Appointment(11, TestData.Anna), groupId, Eva) with { TreatmentId = TestData.MassageTreatment.Id };
        api.On("POST /api/appointment-groups", new GroupBookingResult(groupId, [massage]), HttpStatusCode.Created);
        await group.BookCommand.ExecuteAsync(null);
        var request = JsonSerializer.Deserialize<GroupBookingRequest>(api.Calls.Last(c => c.Route == "POST /api/appointment-groups").Body!, ApiClient.Json)!;
        Assert.True(request.FlexibleOrder);
        Assert.Equal(Eva.Id, Assert.Single(request.Areas!).CustomerId);
    }

    [Fact]
    public void Kalender_zaehlt_die_Sauna_im_Spa_Tag_mit_und_nennt_sie_im_Hover()
    {
        var groupId = Guid.NewGuid();
        var massage = Grouped(TestData.Appointment(11, TestData.Anna), groupId, Eva);
        var visit = new AreaVisitDto(Guid.NewGuid(), TestData.LocationId, Sauna.Id, Eva.Id, "Eva Muster", 1, TestData.At(10), TestData.At(11), false, null, groupId);

        var label = DayLayout.GroupLabel(massage, [massage], [visit]);
        var hover = DayLayout.AreaText(massage, [visit], new Dictionary<Guid, RoomDto> { [Sauna.Id] = Sauna }, Vienna);

        Assert.Equal("Spa-Tag 2/2", label);
        Assert.Equal("Dazu: Finnische Sauna 10:00–11:00", hover);
    }

    [Fact]
    public async Task Gemeinsamer_Termin_zeigt_die_anderen_und_storniert_oder_verschiebt_die_ganze_Buchung()
    {
        var groupId = Guid.NewGuid();
        var eva = Grouped(TestData.Appointment(13, TestData.Anna), groupId, Eva);
        var paul = Grouped(TestData.Appointment(13, TestData.Ben), groupId, Paul);
        var api = Api(eva, paul);
        var vm = await OpenAsync(api);
        var moved = new GroupBookingResult(groupId,
        [
            eva with { Start = TestData.At(14), End = TestData.At(14, 50), UpdatedAt = TestData.At(10) },
            paul with { Start = TestData.At(14), End = TestData.At(14, 50), UpdatedAt = TestData.At(10) },
        ]);
        var move = $"POST /api/locations/{TestData.LocationId}/appointment-groups/{groupId}/move";
        api.On(move, moved);

        vm.OpenAppointment(eva);
        var panel = vm.Selected!;
        var lines = panel.GroupParts.Where(p => !p.IsCurrent).Select(p => $"{p.Time}  {p.Guest} · {p.Treatment}").ToList();
        var startText = panel.GroupStartText;
        panel.GroupStartText = "14:00";
        await panel.MoveGroupCommand.ExecuteAsync(null);

        Assert.True(panel.IsGroup);
        Assert.Equal(["13:00–13:50  Paul Muster · Massage"], lines);
        Assert.Equal("13:00", startText);
        Assert.True(panel.CanMoveGroup);
        Assert.Equal(TestData.At(14), JsonSerializer.Deserialize<MoveGroupRequest>(api.Calls.Last(c => c.Body is not null).Body!, ApiClient.Json)!.Start);
        Assert.All(vm.Columns.SelectMany(c => c.Items), i => Assert.Equal("14:00–14:50", i.TimeText));
        Assert.Equal(["14:00–14:50  Paul Muster · Massage"], vm.Selected!.GroupParts.Where(p => !p.IsCurrent).Select(p => $"{p.Time}  {p.Guest} · {p.Treatment}"));

        var rejected = new BookingRejected("Zu dieser Zeit ist nicht alles frei.", []);
        api.On($"POST /api/locations/{TestData.LocationId}/appointment-groups/{groupId}/cancel", rejected, HttpStatusCode.Conflict);
        var dialogs = new FakeDialogs().Answer(ConfirmChoice.Cancelled).Answer(ConfirmChoice.Confirmed);
        vm.Selected.Dialogs = dialogs;
        await vm.Selected.AskCancelGroupCommand.ExecuteAsync(null);
        Assert.DoesNotContain(api.Calls, c => c.Route.EndsWith("/cancel", StringComparison.Ordinal) && c.Route.Contains("appointment-groups", StringComparison.Ordinal));
        Assert.Equal("Ganze Buchung stornieren?", dialogs.Asked[0].Title);
        dialogs.AssertRules();
        await vm.Selected.AskCancelGroupCommand.ExecuteAsync(null);
        Assert.Equal("Zu dieser Zeit ist nicht alles frei.", vm.Selected.Error);
    }

    [Fact]
    public async Task Bereiche_zeigen_Belegung_und_Eintritte()
    {
        var visit = new AreaVisitDto(Guid.NewGuid(), TestData.LocationId, Sauna.Id, Eva.Id, "Eva Muster", 2, TestData.At(9), TestData.At(12), false, null);
        var day = new AreaDay(Sauna.Id, Sauna.Name, 12, [visit],
            [new AreaLoad(TestData.At(9), 2), new AreaLoad(TestData.At(11), 12), new AreaLoad(TestData.At(12), 0)]);
        var areas = $"GET /api/locations/{TestData.LocationId}/areas?date=2027-03-01";
        var api = Api().On(areas, new List<AreaDay> { day });
        var vm = await OpenAsync(api);
        vm.ByRoom = true;

        vm.OpenAreasCommand.Execute(null);
        var panel = vm.Areas!;
        await panel.LoadAsync();
        var card = Assert.Single(panel.Areas);

        Assert.True(vm.HasAreas);
        Assert.Equal("2 / 12", Assert.Single(vm.AreaGlances).Text);
        Assert.Equal(["Raum Zeder", "Finnische Sauna"], vm.Columns.Select(c => c.Title));
        Assert.Equal("jetzt 2 von 12 Plätzen belegt · höchstens 12", card.Summary);
        Assert.Equal(2, card.Bars.Count);
        Assert.True(card.Bars[1].IsFull);
        Assert.Equal(AreasViewModel.TimelineWidth * 2 / 11, card.Bars[1].Left, 3);
        Assert.Equal("09:00–12:00", card.Visits[0].TimeText);
        Assert.Equal("2 Personen", card.Visits[0].Detail);
        Assert.Equal("10:00", panel.FromText);
        Assert.Equal("13:00", panel.UntilText);
    }

    [Fact]
    public async Task Eintritt_ohne_Verbindung_vormerken_zeigen_und_beim_Abgleich_senden()
    {
        var areas = $"GET /api/locations/{TestData.LocationId}/areas?date=2027-03-01";
        var api = Api().On(areas, new List<AreaDay> { new(Sauna.Id, Sauna.Name, 12, [], []) });
        var cache = new OfflineCache(new MemoryOfflineStore(), new FixedTime());
        var client = api.Client(cache);
        var vm = new CalendarViewModel(client, TestData.Reception(), null, new FixedTime());
        await vm.InitializeAsync();
        await vm.LoadAsync();
        vm.OpenAreasCommand.Execute(null);
        var panel = vm.Areas!;
        await panel.LoadAsync();
        await panel.Search.SearchAsync("eva");
        panel.Search.Customer = panel.Search.Results[0];

        api.Down = true;
        await panel.BookCommand.ExecuteAsync(null);
        var pending = Assert.Single(cache.Pending);
        var row = Assert.Single(panel.Areas[0].Visits);

        Assert.Null(panel.Error);
        Assert.Equal("Ohne Verbindung vorgemerkt: Eva Muster, Finnische Sauna 10:00–13:00. Ob Platz ist, prüft der Server beim Abgleich.", panel.Info);
        Assert.Equal("api/area-visits", pending.Path);
        Assert.Equal(pending.Visit!.Id, row.Visit.Id);
        Assert.Contains($"\"clientId\":\"{row.Visit.Id}\"", pending.Body, StringComparison.Ordinal);
        Assert.True(row.IsPending);
        Assert.Equal("vorgemerkt", row.Detail);
        Assert.False(row.CanCancel);
        Assert.False(row.CanCheckout);

        api.Down = false;
        var booked = row.Visit;
        api.On("POST /api/area-visits", booked, HttpStatusCode.Created)
            .On(areas, new List<AreaDay> { new(Sauna.Id, Sauna.Name, 12, [booked], [new AreaLoad(TestData.At(10), 1), new AreaLoad(TestData.At(13), 0)]) });
        Assert.Equal(1, await client.SyncAsync(TestContext.Current.CancellationToken));
        await panel.LoadAsync();

        Assert.Empty(cache.Pending);
        Assert.Empty(cache.Failed);
        Assert.False(Assert.Single(panel.Areas[0].Visits).IsPending);
    }

    [Fact]
    public async Task Eintritt_buchen_nennt_den_Grund_einer_Absage_und_storniert()
    {
        var visit = new AreaVisitDto(Guid.NewGuid(), TestData.LocationId, Sauna.Id, Eva.Id, "Eva Muster", 1, TestData.At(10), TestData.At(13), false, null);
        var areas = $"GET /api/locations/{TestData.LocationId}/areas?date=2027-03-01";
        var api = Api().On(areas, new List<AreaDay> { new(Sauna.Id, Sauna.Name, 12, [], []) });
        var vm = await OpenAsync(api);
        vm.OpenAreasCommand.Execute(null);
        var panel = vm.Areas!;
        await panel.LoadAsync();
        api.On("POST /api/area-visits", new BookingRejected("Der Bereich ist zu dieser Zeit voll.",
            [new BookingConflict(ResourceKind.Room, Sauna.Id, "Finnische Sauna", "hat ab 11:00 nur noch 1 Platz frei")]), HttpStatusCode.Conflict);

        var summary = panel.Areas[0].Summary;
        await panel.Search.SearchAsync("eva");
        panel.Search.Customer = panel.Search.Results[0];
        panel.Persons = 2;
        await panel.BookCommand.ExecuteAsync(null);
        var request = JsonSerializer.Deserialize<AreaVisitRequest>(api.Calls.Last().Body!, ApiClient.Json)!;
        var error = panel.Error;

        api.On("POST /api/area-visits", visit, HttpStatusCode.Created)
            .On(areas, new List<AreaDay> { new(Sauna.Id, Sauna.Name, 12, [visit], [new AreaLoad(TestData.At(10), 1), new AreaLoad(TestData.At(13), 0)]) });
        await panel.BookCommand.ExecuteAsync(null);
        var info = panel.Info;
        var row = Assert.Single(panel.Areas[0].Visits);
        api.On($"POST /api/locations/{TestData.LocationId}/area-visits/{visit.Id}/cancel", visit with { IsCancelled = true });
        var dialogs = new FakeDialogs().Answer(ConfirmChoice.Cancelled).Answer(ConfirmChoice.Confirmed);
        panel.Dialogs = dialogs;
        await row.CancelCommand.ExecuteAsync(null);
        Assert.DoesNotContain(api.Calls, c => c.Route.EndsWith("/cancel", StringComparison.Ordinal));
        Assert.Equal("Eintritt stornieren?", dialogs.Asked[0].Title);
        dialogs.AssertRules();
        await row.CancelCommand.ExecuteAsync(null);

        Assert.Equal("12 Plätze, noch frei", summary);
        Assert.Equal(Sauna.Id, request.RoomId);
        Assert.Equal(Eva.Id, request.CustomerId);
        Assert.Equal(2, request.Persons);
        Assert.Equal(TestData.At(10), request.From);
        Assert.Equal(TestData.At(13), request.Until);
        Assert.Equal("Der Bereich ist zu dieser Zeit voll. Finnische Sauna: hat ab 11:00 nur noch 1 Platz frei.", error);
        Assert.Equal("Eva Muster: Finnische Sauna 10:00–13:00 gebucht.", info);
        Assert.False(panel.Search.HasCustomer);
        Assert.Equal(1, panel.Persons);
        Assert.Equal("Eintritt von Eva Muster storniert.", panel.Info);
    }
}
