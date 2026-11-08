using System.Text.Json;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;
using AispaStudio.Desktop.ViewModels;

namespace AispaStudio.Desktop.Tests;

/// <summary>Seitenpanel eines Termins: Paarbuchung mit allen Teilen, ♀/♂, Kundendaten ergänzen, Raum-Infos.</summary>
public sealed class CalendarPanelTests
{
    private static readonly TimeZoneInfo Vienna = TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna");

    private static readonly CustomerDto Eva = new(Guid.NewGuid(), "Eva", "Muster", null, "+43 664 1", null, null, Salutation: CustomerSalutation.Female);
    private static readonly CustomerDto Paul = new(Guid.NewGuid(), "Paul", "Muster", null, "+43 664 2", null, null, Salutation: CustomerSalutation.Male);

    private sealed class FixedTime : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => TestData.At(10);

        public override TimeZoneInfo LocalTimeZone => Vienna;
    }

    private static AppointmentDto Grouped(AppointmentDto a, Guid groupId, CustomerDto customer) =>
        a with { GroupId = groupId, CustomerId = customer.Id, CustomerName = $"{customer.FirstName} {customer.LastName}" };

    private static CalendarDay DayWith(params AppointmentDto[] appointments) => TestData.Day(appointments) with
    {
        Guests = [.. appointments.DistinctBy(a => a.CustomerId).Select(a => new CalendarGuest(a.CustomerId, 2, 1, 0, null, null,
            Salutation: a.CustomerId == Eva.Id ? CustomerSalutation.Female : a.CustomerId == Paul.Id ? CustomerSalutation.Male : CustomerSalutation.None))],
    };

    private static StubApi Api(CalendarDay day) => new StubApi()
        .On("GET /api/locations", new List<LocationDto> { TestData.Location })
        .On($"GET /api/locations/{TestData.LocationId}/resources", TestData.Resources)
        .On($"GET /api/locations/{TestData.LocationId}/calendar?date=2027-03-01", day);

    private static async Task<CalendarViewModel> OpenAsync(StubApi api, CurrentUser? user = null)
    {
        var vm = new CalendarViewModel(api.Client(), user ?? TestData.Reception(), null, new FixedTime());
        await vm.InitializeAsync();
        await vm.LoadAsync();
        return vm;
    }

    private static (AppointmentDto Eva, AppointmentDto Paul, Guid Group) Couple()
    {
        var group = Guid.NewGuid();
        return (Grouped(TestData.Appointment(14, TestData.Anna), group, Eva), Grouped(TestData.Appointment(14, TestData.Ben), group, Paul), group);
    }

    [Fact]
    public async Task Paarbuchung_zeigt_beide_Gaeste_mit_Zeit_Person_Raum_und_Status()
    {
        var (eva, paul, _) = Couple();
        paul = paul with { Status = AppointmentStatus.CheckedIn };
        var vm = await OpenAsync(Api(DayWith(eva, paul)));

        vm.OpenAppointment(eva);
        var panel = vm.Selected!;

        Assert.Equal("Paar", panel.GroupTitle);
        Assert.Equal(["Eva Muster", "Paul Muster"], panel.GroupParts.Select(p => p.Guest));
        Assert.Equal(["14:00–14:50", "14:00–14:50"], panel.GroupParts.Select(p => p.Time));
        Assert.Equal([true, false], panel.GroupParts.Select(p => p.IsCurrent));
        Assert.Equal(["Gebucht", "Eingecheckt"], panel.GroupParts.Select(p => p.Status));
        Assert.Equal("Massage · Anna · Raum Zeder", panel.GroupParts[0].Detail);
        Assert.Equal("Massage · Ben · Raum Zeder", panel.GroupParts[1].Detail);
        Assert.Equal(["♀", "♂"], panel.GroupParts.Select(p => p.Symbol));
    }

    [Fact]
    public async Task Klick_auf_einen_anderen_Teil_oeffnet_ihn()
    {
        var (eva, paul, _) = Couple();
        var vm = await OpenAsync(Api(DayWith(eva, paul)));
        vm.OpenAppointment(eva);

        vm.Selected!.OpenGroupPartCommand.Execute(vm.Selected.GroupParts.Single(p => !p.IsCurrent));

        Assert.Equal(paul.Id, vm.Selected!.Appointment.Id);
        // Der geöffnete Teil ist nicht noch einmal anklickbar.
        vm.Selected.OpenGroupPartCommand.Execute(vm.Selected.GroupParts.Single(p => p.IsCurrent));
        Assert.Equal(paul.Id, vm.Selected.Appointment.Id);
    }

    [Fact]
    public async Task Gemeinsam_einchecken_checkt_alle_gebuchten_Teile_ein()
    {
        var (eva, paul, _) = Couple();
        var api = Api(DayWith(eva, paul));
        api.On($"POST /api/appointments/{eva.Id}/status", eva with { Status = AppointmentStatus.CheckedIn, UpdatedAt = TestData.At(10) });
        api.On($"POST /api/appointments/{paul.Id}/status", paul with { Status = AppointmentStatus.CheckedIn, UpdatedAt = TestData.At(10) });
        var vm = await OpenAsync(api);
        vm.OpenAppointment(eva);

        Assert.True(vm.Selected!.CanCheckInGroup);
        Assert.Equal("Alle einchecken (2)", vm.Selected.CheckInGroupText);
        await vm.Selected.CheckInGroupCommand.ExecuteAsync(null);

        Assert.All(vm.Columns.SelectMany(c => c.Items), i => Assert.Equal(AppointmentStatus.CheckedIn, i.Status));
        Assert.Equal(["Eingecheckt", "Eingecheckt"], vm.Selected!.GroupParts.Select(p => p.Status));
        Assert.False(vm.Selected.CanCheckInGroup);
        Assert.Equal(2, api.Calls.Count(c => c.Route.StartsWith("POST /api/appointments/", StringComparison.Ordinal) && c.Route.EndsWith("/status", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task Gemeinsam_kassieren_reicht_alle_eingecheckten_Teile_weiter()
    {
        var (eva, paul, _) = Couple();
        eva = eva with { Status = AppointmentStatus.CheckedIn };
        paul = paul with { Status = AppointmentStatus.CheckedIn };
        var vm = await OpenAsync(Api(DayWith(eva, paul)));
        IReadOnlyList<AppointmentDto>? sent = null;
        vm.CheckoutGroupRequested = parts =>
        {
            sent = parts;
            return Task.CompletedTask;
        };

        vm.OpenAppointment(eva);
        Assert.True(vm.Selected!.CanCheckoutGroup);
        await vm.Selected.CheckoutGroupCommand.ExecuteAsync(null);

        Assert.Equal([eva.Id, paul.Id], sent!.Select(a => a.Id));
    }

    [Fact]
    public async Task Gemeinsam_kassieren_gibt_es_erst_wenn_mehr_als_ein_Teil_da_ist()
    {
        var (eva, paul, _) = Couple();
        eva = eva with { Status = AppointmentStatus.CheckedIn };
        var vm = await OpenAsync(Api(DayWith(eva, paul)));
        vm.CheckoutGroupRequested = _ => Task.CompletedTask;

        vm.OpenAppointment(eva);

        Assert.False(vm.Selected!.CanCheckoutGroup);
    }

    [Fact]
    public async Task Einzeltermin_hat_keine_Gruppenaktionen()
    {
        var single = TestData.Appointment(10, TestData.Anna);
        var vm = await OpenAsync(Api(DayWith(single)));

        vm.OpenAppointment(single);

        Assert.False(vm.Selected!.IsGroup);
        Assert.Empty(vm.Selected.GroupParts);
        Assert.False(vm.Selected.CanCheckInGroup);
    }

    [Fact]
    public async Task Panel_zeigt_das_Symbol_aus_der_Anrede_neben_dem_Namen()
    {
        var (eva, paul, _) = Couple();
        var vm = await OpenAsync(Api(DayWith(eva, paul)));

        vm.OpenAppointment(eva);
        Assert.Equal("♀", vm.Selected!.Symbol);
        Assert.Equal(Controls.Icons.Female, vm.Selected.SymbolIcon);
        vm.OpenAppointment(paul);
        Assert.Equal(Controls.Icons.Male, vm.Selected!.SymbolIcon);

        var without = TestData.Appointment(10, TestData.Anna);
        var other = await OpenAsync(Api(DayWith(without)));
        other.OpenAppointment(without);
        Assert.Null(other.Selected!.SymbolIcon);
    }

    [Fact]
    public async Task Stift_oeffnet_die_Kundendaten_im_Panel_und_nach_dem_Speichern_zeigt_der_Termin_die_neuen_Daten()
    {
        var (eva, paul, _) = Couple();
        var api = Api(DayWith(eva, paul))
            .On($"GET /api/customers/{Eva.Id}", Eva)
            .On($"PUT /api/customers/{Eva.Id}", Eva with { Pronouns = "sie/ihr", Phone = "+43 664 999" });
        var vm = await OpenAsync(api);
        vm.OpenAppointment(eva);
        var loads = api.Calls.Count(c => c.Route.Contains("/calendar?date=", StringComparison.Ordinal));

        Assert.True(vm.Selected!.CanEditCustomer);
        Assert.Equal(360, vm.PanelWidth);
        await vm.Selected.EditCustomerCommand.ExecuteAsync(null);
        var form = vm.Selected.CustomerForm!;
        Assert.Equal("Eva", form.FirstName);
        Assert.Equal(560, vm.PanelWidth);
        form.Pronouns = "sie/ihr";
        form.Phone = "+43 664 999";
        await form.SaveCommand.ExecuteAsync(null);

        var sent = JsonSerializer.Deserialize<CustomerInput>(api.Calls.Last(c => c.Route == $"PUT /api/customers/{Eva.Id}").Body!, ApiClient.Json)!;
        Assert.Equal(("sie/ihr", "+43 664 999"), (sent.Pronouns, sent.Phone));
        Assert.True(api.Calls.Count(c => c.Route.Contains("/calendar?date=", StringComparison.Ordinal)) > loads);
        Assert.Equal(eva.Id, vm.Selected!.Appointment.Id);
        Assert.Null(vm.Selected.CustomerForm);
        Assert.Equal(360, vm.PanelWidth);
    }

    [Fact]
    public async Task Abbrechen_schliesst_das_Formular_ohne_zu_speichern()
    {
        var (eva, paul, _) = Couple();
        var api = Api(DayWith(eva, paul)).On($"GET /api/customers/{Eva.Id}", Eva);
        var vm = await OpenAsync(api);
        vm.OpenAppointment(eva);
        await vm.Selected!.EditCustomerCommand.ExecuteAsync(null);

        vm.Selected.CustomerForm!.CancelCommand.Execute(null);

        Assert.Null(vm.Selected.CustomerForm);
        Assert.DoesNotContain(api.Calls, c => c.Route.StartsWith("PUT ", StringComparison.Ordinal));
        Assert.Equal(360, vm.PanelWidth);
    }

    [Fact]
    public async Task Gast_ohne_Namen_hat_keinen_Stift_und_ohne_Buchungsrecht_auch_nicht()
    {
        var (eva, paul, _) = Couple();
        var vm = await OpenAsync(Api(DayWith(eva, paul with { PlaceholderGuest = 2 })));
        vm.OpenAppointment(paul with { PlaceholderGuest = 2 });
        Assert.False(vm.Selected!.CanEditCustomer);

        var therapist = TestData.Reception() with { Rights = [AccessRight.ViewCalendar, AccessRight.ChangeStatus] };
        var other = await OpenAsync(Api(DayWith(eva, paul)), therapist);
        other.OpenAppointment(eva);
        Assert.False(other.Selected!.CanEditCustomer);
    }

    [Fact]
    public async Task Raum_Info_nennt_Raumart_Raumpflege_Behandlungen_und_weitere_Termine_des_Raums()
    {
        var first = TestData.Appointment(10, TestData.Anna);
        var second = TestData.Appointment(12, TestData.Ben);
        var api = Api(DayWith(first, second));
        var user = TestData.Reception() with { Rights = [.. TestData.Reception().Rights, AccessRight.ManageLocation] };
        var vm = await OpenAsync(api, user);
        var requests = new List<(string Page, string? Tab, Guid? Id)>();
        vm.AdminRequested = (page, tab, id) => requests.Add((page, tab, id));

        vm.OpenAppointment(first);
        var room = vm.Selected!.RoomInfo!;

        Assert.Equal("Raum Zeder", room.Name);
        Assert.Equal("Einzelraum", room.Kind);
        Assert.Equal("Raumpflege 10 Min. nach jedem Termin", room.Cleaning);
        Assert.Equal("2 Behandlungen", room.TreatmentsTitle);
        Assert.Equal([("Gesicht", ""), ("Massage", "")], room.TreatmentRows.Select(r => (r.Name, r.Durations)));
        Assert.Equal("Heute", room.DayTitle);
        Assert.Equal([("10:00–10:50", "Eva Muster", "Massage", true), ("12:00–12:50", "Eva Muster", "Massage", false)],
            room.DayRows.Select(r => (r.Time, r.Guest, r.Treatment, r.IsCurrent)));
        Assert.True(room.HasOthers);
        Assert.Equal("Raum Zeder, Einzelraum", room.AccessibleName);
        Assert.True(room.CanEdit);
        room.EditInAdminCommand.Execute(null);
        Assert.Equal([("masterData", "locations", (Guid?)TestData.Room.Id)], requests);
    }

    [Fact]
    public async Task Raum_Info_ohne_Recht_auf_die_Standorte_hat_keinen_Link_zur_Verwaltung()
    {
        var first = TestData.Appointment(10, TestData.Anna);
        var vm = await OpenAsync(Api(DayWith(first)));
        vm.AdminRequested = (_, _, _) => { };

        vm.OpenAppointment(first);

        Assert.False(vm.Selected!.RoomInfo!.CanEdit);
        Assert.False(vm.Selected.RoomInfo.HasOthers);
    }

    [Fact]
    public void Raum_Info_fasst_Varianten_desselben_Namens_zu_einer_Zeile_zusammen()
    {
        var rows = RoomInfoViewModel.MergeVariants(
            ["Lava-Hot-Stone-Massage (60 Min.)", "Gesicht", "Lava-Hot-Stone-Massage (90 Min.)", "Rücken (30 Min.)", "Rücken (60 Min.)", "Rücken (90 Min.)"]);

        Assert.Equal([("Lava-Hot-Stone-Massage", "60 und 90 Min."), ("Gesicht", ""), ("Rücken", "30, 60 und 90 Min.")], rows.Select(r => (r.Name, r.Durations)));
    }

    [Fact]
    public void Raum_Info_zaehlt_die_Behandlungen_in_der_Ueberschrift_und_klappt_ab_sieben_Zeilen_auf()
    {
        string[] names = [.. Enumerable.Range(1, 11).Select(i => $"Behandlung {i:00}")];
        var info = NewRoomInfo(names, []);

        Assert.Equal("11 Behandlungen", info.TreatmentsTitle);
        Assert.Equal(6, info.VisibleTreatments.Count);
        Assert.True(info.CanExpandTreatments);
        Assert.Equal("Alle anzeigen", info.ToggleTreatmentsText);

        info.ToggleTreatmentsCommand.Execute(null);

        Assert.Equal(11, info.VisibleTreatments.Count);
        Assert.Equal("Weniger anzeigen", info.ToggleTreatmentsText);

        var few = NewRoomInfo(names[..6], []);
        Assert.False(few.CanExpandTreatments);
        Assert.Equal("1 Behandlung", NewRoomInfo(names[..1], []).TreatmentsTitle);
        Assert.Equal("Keine Behandlung zugeordnet", NewRoomInfo([], []).TreatmentsTitle);
    }

    [Fact]
    public void Raum_Info_zeigt_hoechstens_sechs_Terminzeilen_den_gewaehlten_Termin_immer_und_und_n_weitere()
    {
        var rows = Enumerable.Range(0, 9).Select(i => new RoomDayRow($"{9 + i:00}:00–{9 + i:00}:50", $"Gast {i}", "Massage", i == 8)).ToList();
        var info = NewRoomInfo([], rows);

        Assert.Equal(6, info.VisibleDayRows.Count);
        Assert.Contains(info.VisibleDayRows, r => r.IsCurrent);
        Assert.Equal(["Gast 0", "Gast 1", "Gast 2", "Gast 3", "Gast 4", "Gast 8"], info.VisibleDayRows.Select(r => r.Guest));
        Assert.True(info.HasMoreDayRows);
        Assert.Equal("und 3 weitere", info.MoreDayRowsText);

        var few = NewRoomInfo([], rows[..3]);
        Assert.False(few.HasMoreDayRows);
        Assert.Equal(3, few.VisibleDayRows.Count);
    }

    private static RoomInfoViewModel NewRoomInfo(IReadOnlyList<string> treatments, IReadOnlyList<RoomDayRow> day) =>
        new(TestData.Room, new DayLayout.RoomFacts("Einzelraum", "Raumpflege 10 Min.", treatments), day, "Heute", null);

    [Fact]
    public async Task Panel_nennt_die_gebundene_Zeit_mit_Vor_und_Nachbereitung()
    {
        // Massage 10:00–10:50 mit 5 Minuten Vorbereitung und 10 Minuten Nachbereitung: gebunden 09:55–11:00.
        var appointment = TestData.Appointment(10, TestData.Anna);
        var vm = await OpenAsync(Api(DayWith(appointment)));

        vm.OpenAppointment(appointment);

        Assert.Equal("Gebunden 09:55–11:00 · Vorb. 5 · Nachb. 10 Min.", vm.Selected!.BoundText);
    }

    [Fact]
    public async Task Panel_laesst_Teile_mit_0_Minuten_weg_und_sagt_es_wenn_beide_fehlen()
    {
        var prepOnly = TestData.Appointment(10, TestData.Anna) with { BlockedUntil = TestData.At(10, 50) };
        var followOnly = TestData.Appointment(12, TestData.Anna) with { BlockedFrom = TestData.At(12) };
        var none = TestData.Appointment(14, TestData.Anna) with { BlockedFrom = TestData.At(14), BlockedUntil = TestData.At(14, 50) };
        var vm = await OpenAsync(Api(DayWith(prepOnly, followOnly, none)));

        vm.OpenAppointment(prepOnly);
        var first = vm.Selected!.BoundText;
        vm.OpenAppointment(followOnly);
        var second = vm.Selected!.BoundText;
        vm.OpenAppointment(none);
        var third = vm.Selected!.BoundText;

        // Die Behandlung hat 5 und 10 Minuten; fehlt ein Teil, weicht der Termin davon ab.
        Assert.Equal("Gebunden 09:55–10:50 · Vorb. 5 Min. (geändert)", first);
        Assert.Equal("Gebunden 12:00–13:00 · Nachb. 10 Min. (geändert)", second);
        Assert.Equal("Keine Vor- und Nachbereitung", third);
    }
}
