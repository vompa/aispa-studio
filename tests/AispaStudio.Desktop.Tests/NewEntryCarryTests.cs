using System.Net;
using System.Text.Json;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Controls;
using AispaStudio.Desktop.Services;
using AispaStudio.Desktop.ViewModels;

namespace AispaStudio.Desktop.Tests;

/// <summary>
/// R04 Slice 82 (Ä149, Ä153): Beim Wechsel der Art in „Neuer Termin“ geht nichts verloren, was die neue Art als Feld hat, eine schon besuchte Art
/// steht beim Zurückwechseln unverändert da, „Freie Zeit suchen“ nimmt die Notiz mit, und der Reiter unten nennt den Gast mit Kennzeichen „in Bearbeitung“.
/// Im Testtag arbeitet Anna (erste Spalte, kann die Massage mit 50 Minuten) am Montag, 1.3.2027; „jetzt“ ist 10 Uhr.
/// </summary>
public sealed class NewEntryCarryTests
{
    private static readonly TimeZoneInfo Vienna = TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna");
    private static readonly RoomDto Sauna = new(Guid.NewGuid(), TestData.LocationId, "Finnische Sauna", 0, Places: 12, IsArea: true);
    private static readonly LocationResources Resources = TestData.Resources with { Rooms = [TestData.Room, Sauna] };
    private static readonly CustomerDto Andrea = new(Guid.NewGuid(), "Andrea", "Auer", null, "+43 664 3", null, null);
    private static readonly DateOnly Tuesday = TestData.Monday.AddDays(1);
    private const string Wish = "Lavendelöl, wenig Gespräch";

    private sealed class FixedTime : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => TestData.At(10);

        public override TimeZoneInfo LocalTimeZone => Vienna;
    }

    private static string Availability => $"POST /api/locations/{TestData.LocationId}/appointment-groups/availability";

    private static GroupSlot Slot(int hour) => new(TestData.At(hour), TestData.At(hour, 50), SharedRoom: true,
    [
        new GroupSlotItem(TestData.At(hour), TestData.At(hour, 50), TestData.Anna.Id, TestData.Room.Id, null),
        new GroupSlotItem(TestData.At(hour), TestData.At(hour, 50), TestData.Ben.Id, TestData.Room.Id, null),
    ]);

    private static StubApi Api() => new StubApi()
        .On("GET /api/locations", new List<LocationDto> { TestData.Location })
        .On($"GET /api/locations/{TestData.LocationId}/resources", Resources)
        .On($"GET /api/locations/{TestData.LocationId}/calendar?date=2027-03-01", TestData.Day())
        .On($"GET /api/locations/{TestData.LocationId}/areas?date=2027-03-01", new List<AreaDay>())
        .On($"GET /api/locations/{TestData.LocationId}/areas?date=2027-03-02", new List<AreaDay>())
        .On(Availability, new List<GroupSlot> { Slot(13), Slot(14), Slot(15) });

    private static async Task<CalendarViewModel> OpenAsync(StubApi api)
    {
        var vm = new CalendarViewModel(api.Client(), TestData.Owner(), null, new FixedTime());
        await vm.InitializeAsync();
        await vm.LoadAsync();
        return vm;
    }

    private static double Y(CalendarViewModel vm, string hour) => vm.Hours.Single(h => h.Label == hour).Top + 1;

    /// <summary>„Neuer Termin“ bei Anna um 14:00 mit Andrea Auer, Massage und Notiz.</summary>
    private static async Task<(CalendarViewModel Vm, BookingViewModel Booking)> StartedAsync(StubApi? api = null)
    {
        var vm = await OpenAsync(api ?? Api());
        vm.StartBooking(vm.Columns[0], Y(vm, "14:00"));
        var booking = vm.Booking!;
        booking.Customers.Add(Andrea);
        booking.Customer = Andrea;
        booking.Treatment = booking.Treatments.Single(t => t.Id == TestData.MassageTreatment.Id);
        booking.Note = Wish;
        return (vm, booking);
    }

    private static TreatmentChoice ChoiceOf(GroupBookingViewModel group, Guid treatmentId) =>
        group.Steps[0].Entries[0].Choices.Single(c => c.Id == treatmentId);

    // ---------- Termin → andere Art ----------

    [Fact]
    public async Task Termin_mit_Andrea_Auer_auf_Paar_Spa_gewechselt_steht_sie_als_erste_Person_mit_Behandlung_Datum_und_Beginn_da()
    {
        var (vm, booking) = await StartedAsync();
        booking.DateText = DateInputParser.Format(Tuesday);

        Assert.True(vm.SelectNewEntry(2));
        var group = vm.GroupBooking!;
        // Für die Suche nach gemeinsamen Zeiten braucht auch Gast 2 eine Behandlung.
        group.Steps[0].Entries[1].Choice = ChoiceOf(group, TestData.Facial.Id);
        await group.SearchSlotsAsync();

        Assert.Equal(Andrea, group.Guests[0].Search.Customer);
        Assert.Equal(TestData.MassageTreatment.Id, group.Steps[0].Entries[0].Choice.Id);
        Assert.Equal("02.03.2027", group.DateText);
        Assert.Equal("14:00", group.Slot!.Text);
        Assert.Equal("14:00", group.OfflineStartText);
        Assert.Equal(Wish, group.Note);
    }

    [Fact]
    public async Task Termin_mit_Gast_auf_Sauna_gewechselt_uebernimmt_Gast_Datum_Beginn_und_Notiz_mit_einer_Person()
    {
        var (vm, booking) = await StartedAsync();
        booking.DateText = DateInputParser.Format(Tuesday);
        booking.StartText = "14:30";

        Assert.True(vm.SelectNewEntry(6));
        var areas = vm.Areas!;

        Assert.Equal(Andrea, areas.Search.Customer);
        Assert.Equal(DayLayout.DateText(Tuesday), areas.DateText);
        Assert.Equal("14:30", areas.FromText);
        Assert.Equal("17:30", areas.UntilText);
        Assert.Equal(Wish, areas.Note);
        Assert.Equal(1, areas.Persons);
    }

    [Fact]
    public async Task Termin_zu_Pause_und_Sperrzeit_uebernimmt_Beginn_und_Laenge_der_Behandlung()
    {
        var (vm, booking) = await StartedAsync();
        booking.StartText = "14:15";

        Assert.True(vm.SelectNewEntry(3));
        Assert.Equal(CalendarEntryKind.Break, vm.Entry!.Kind);
        Assert.Equal("14:15", vm.Entry.StartText);
        Assert.Equal("15:05", vm.Entry.EndText);

        Assert.True(vm.SelectNewEntry(4));
        Assert.Equal(CalendarEntryKind.Block, vm.Entry!.Kind);
        Assert.Equal("14:15", vm.Entry.StartText);
        Assert.Equal("15:05", vm.Entry.EndText);
        // Der Grund einer Sperrzeit ist keine Notiz zum Termin.
        Assert.Equal("", vm.Entry.Text);
    }

    [Fact]
    public async Task Termin_zu_Notiz_uebernimmt_Beginn_und_Notiztext()
    {
        var (vm, booking) = await StartedAsync();
        booking.StartText = "14:15";

        Assert.True(vm.SelectNewEntry(5));

        Assert.Equal(CalendarEntryKind.Note, vm.Entry!.Kind);
        Assert.Equal("14:15", vm.Entry.StartText);
        Assert.Equal(Wish, vm.Entry.Text);
    }

    [Fact]
    public async Task Pause_zu_Termin_uebernimmt_den_Beginn()
    {
        var vm = await OpenAsync(Api());
        vm.OpenBreakAt(10, Y(vm, "14:00"));
        vm.Entry!.StartText = "14:20";

        Assert.True(vm.SelectNewEntry(1));

        Assert.Equal("14:20", vm.Booking!.StartText);
    }

    [Fact]
    public async Task Termin_ohne_Gast_zu_Pause_laesst_die_vorgeschlagene_Laenge_der_Pause()
    {
        var vm = await OpenAsync(Api());
        vm.StartBooking(vm.Columns[0], Y(vm, "14:00"));
        var proposed = vm.Booking!.StartText;

        Assert.True(vm.SelectNewEntry(3));

        // Ohne Gast ist die Massage nur vorbelegt: die Pause behält ihre Standardlänge von 60 Minuten.
        Assert.Equal(proposed, vm.Entry!.StartText);
        Assert.Equal("15:00", vm.Entry.EndText);
    }

    [Fact]
    public async Task Sauna_spaet_am_Abend_endet_nicht_vor_dem_Beginn()
    {
        var (vm, booking) = await StartedAsync();
        booking.StartText = "22:30";

        Assert.True(vm.SelectNewEntry(6));

        Assert.Equal("22:30", vm.Areas!.FromText);
        Assert.Equal("23:59", vm.Areas.UntilText);
    }

    [Fact]
    public async Task Eine_geleerte_Notiz_kommt_in_der_naechsten_Art_nicht_wieder()
    {
        var (vm, _) = await StartedAsync();
        Assert.True(vm.SelectNewEntry(5));
        Assert.Equal(Wish, vm.Entry!.Text);
        vm.Entry.Text = "";

        Assert.True(vm.SelectNewEntry(2));

        Assert.Null(vm.GroupBooking!.Note);
    }

    // ---------- Rückfrage und Verlegen mit gemerkten Arten ----------

    [Fact]
    public async Task Schliessen_fragt_auch_wenn_die_Eingaben_in_einer_gemerkten_Art_stehen()
    {
        var vm = await OpenAsync(Api());
        vm.StartBooking(vm.Columns[0], Y(vm, "14:00"));
        vm.Booking!.Note = Wish;
        Assert.True(vm.SelectNewEntry(3));
        var dialogs = new FakeDialogs();
        vm.Dialogs = dialogs;

        await vm.RequestClosePanelAsync();

        Assert.Equal("Änderungen verwerfen?", Assert.Single(dialogs.Asked).Title);
        Assert.NotNull(vm.NewEntry);
    }

    [Fact]
    public async Task Die_Rueckfrage_beim_Schliessen_nennt_die_Folge_ohne_Anrede()
    {
        var vm = await OpenAsync(Api());
        vm.StartBooking(vm.Columns[0], Y(vm, "14:00"));
        vm.Booking!.Note = Wish;
        var dialogs = new FakeDialogs();
        vm.Dialogs = dialogs;

        await vm.RequestClosePanelAsync();

        Assert.Equal("Die Eingaben gehen beim Schließen verloren.", Assert.Single(dialogs.Asked).Message);
    }

    [Fact]
    public async Task Klick_in_eine_andere_Spalte_verlegt_den_gemerkten_Termin_statt_ihn_zu_verwerfen()
    {
        var (vm, booking) = await StartedAsync();
        Assert.True(vm.SelectNewEntry(3));
        var pause = vm.Entry!;

        vm.StartBooking(vm.Columns[1], Y(vm, "15:00"));

        Assert.Equal(NewEntryKind.Appointment, vm.NewEntry!.Kind);
        Assert.Same(booking, vm.Booking);
        Assert.Equal(Andrea, booking.Customer);
        Assert.Equal(Wish, booking.Note);
        Assert.Equal(vm.Columns[1].Title, booking.ColumnTitle);
        Assert.Equal("15:00", booking.StartText);

        // Die gemerkte Pause gehörte zur alten Spalte: sie wird neu gebaut.
        Assert.True(vm.SelectNewEntry(3));
        Assert.NotSame(pause, vm.Entry);
        Assert.Equal("15:00", vm.Entry!.StartText);
    }

    [Fact]
    public async Task Paar_Spa_springt_nach_einer_eigenen_Wahl_nicht_auf_den_uebernommenen_Beginn_zurueck()
    {
        var (vm, _) = await StartedAsync();
        Assert.True(vm.SelectNewEntry(2));
        var group = vm.GroupBooking!;
        group.Steps[0].Entries[1].Choice = ChoiceOf(group, TestData.Facial.Id);
        await group.SearchSlotsAsync();
        Assert.Equal("14:00", group.Slot!.Text);

        group.Slot = group.Slots.Single(s => s.Text == "13:00");
        group.Gap = 30;
        await group.SearchSlotsAsync();

        Assert.Equal("13:00", group.Slot!.Text);
    }

    // ---------- Rückweg ----------

    [Fact]
    public async Task Termin_zu_Paar_Spa_und_zurueck_laesst_Gast_Behandlung_Beginn_und_Notiz_unveraendert()
    {
        var (vm, booking) = await StartedAsync();
        booking.StartText = "14:30";

        Assert.True(vm.SelectNewEntry(2));
        vm.GroupBooking!.Note = "anders";
        Assert.True(vm.SelectNewEntry(1));

        var back = vm.Booking!;
        Assert.Equal(Andrea, back.Customer);
        Assert.Equal(TestData.MassageTreatment.Id, back.Treatment!.Id);
        Assert.Equal("14:30", back.StartText);
        Assert.Equal(Wish, back.Note);
    }

    [Fact]
    public async Task Paar_Spa_ueber_Sauna_zurueck_behaelt_alle_Gaeste_und_Abschnitte()
    {
        var (vm, _) = await StartedAsync();
        Assert.True(vm.SelectNewEntry(2));
        var group = vm.GroupBooking!;
        group.AddGuestCommand.Execute(null);
        group.AddStepCommand.Execute(null);

        Assert.True(vm.SelectNewEntry(6));
        Assert.True(vm.SelectNewEntry(2));

        Assert.Same(group, vm.GroupBooking);
        Assert.Equal(3, group.Guests.Count);
        Assert.Equal(2, group.Steps.Count);
    }

    [Fact]
    public async Task Paar_Spa_zu_Termin_nimmt_die_erste_Person_mit_ihrer_Behandlung()
    {
        var vm = await OpenAsync(Api());
        // Erst Paar/Spa, dann zum ersten Mal „Termin“: so baut der Wechsel die Termin-Eingabe aus Paar/Spa.
        vm.OpenGroupBookingCommand.Execute(null);
        var group = vm.GroupBooking!;
        group.Guests[0].Search.Customer = Andrea;
        group.Steps[0].Entries[0].Choice = ChoiceOf(group, TestData.MassageTreatment.Id);
        group.Steps[0].Entries[1].Choice = ChoiceOf(group, TestData.Facial.Id);
        group.Note = Wish;
        await group.SearchSlotsAsync();
        group.Slot = group.Slots.Single(s => s.Text == "15:00");

        Assert.True(vm.SelectNewEntry(1));
        var booking = vm.Booking!;

        Assert.Equal(Andrea, booking.Customer);
        Assert.Equal(TestData.MassageTreatment.Id, booking.Treatment!.Id);
        Assert.Equal("15:00", booking.StartText);
        Assert.Equal(Wish, booking.Note);
    }

    // ---------- Freie Zeit suchen ----------

    [Fact]
    public async Task Freie_Zeit_aus_Neuer_Termin_nimmt_die_Notiz_mit_bis_in_den_gebuchten_Termin()
    {
        var slot = new AvailableSlot(TestData.At(15), TestData.At(15, 50), TestData.Anna.Id, TestData.Room.Id, null);
        var route = $"GET /api/locations/{TestData.LocationId}/availability?treatmentId={TestData.MassageTreatment.Id}&date=2027-03-01";
        var booked = TestData.Appointment(15, TestData.Anna) with { CustomerId = Andrea.Id, CustomerName = "Andrea Auer", Note = Wish };
        var api = Api()
            .On($"{route}&minSlots=3&days=14", new List<AvailableSlot> { slot })
            .On($"{route}&customerId={Andrea.Id}&minSlots=3&days=14", new List<AvailableSlot> { slot })
            .On("POST /api/appointments", booked, HttpStatusCode.Created);
        var (vm, booking) = await StartedAsync(api);

        await booking.SearchFreeTimeCommand.ExecuteAsync(null);
        var freeTime = vm.FreeTime!;
        for (var i = 0; i < 300 && !(freeTime.HasSlots && !freeTime.Busy); i++)
        {
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        await freeTime.BookActiveCommand.ExecuteAsync(null);

        var post = Assert.Single(api.Calls, c => c.Route == "POST /api/appointments");
        var request = JsonSerializer.Deserialize<AppointmentRequest>(post.Body!, ApiClient.Json)!;
        Assert.Equal(Wish, request.Note);
        Assert.Equal(Andrea.Id, request.CustomerId);
    }

    // ---------- Reiter ----------

    [Fact]
    public async Task Reiter_von_Neuer_Termin_ohne_Gast_heisst_nur_Neuer_Termin_und_ist_in_Bearbeitung()
    {
        var vm = await OpenAsync(Api());
        vm.StartBooking(vm.Columns[0], Y(vm, "14:00"));

        var tab = Assert.Single(vm.OpenPanels);
        Assert.Equal("Neuer Termin", tab.Text);
        Assert.True(tab.IsInProgress);
        Assert.Equal("Neuer Termin, in Bearbeitung", tab.Name);
    }

    [Fact]
    public async Task Reiter_von_Neuer_Termin_mit_Andrea_Auer_nennt_den_Gast_und_ist_in_Bearbeitung()
    {
        var (vm, booking) = await StartedAsync();

        var tab = Assert.Single(vm.OpenPanels);
        Assert.Equal("Neuer Termin · Andrea Auer", tab.Text);
        Assert.True(tab.IsInProgress);
        Assert.Equal("Neuer Termin · Andrea Auer, in Bearbeitung", tab.Name);
        Assert.Equal("Neuer Termin · Andrea Auer, in Bearbeitung", tab.Tip);

        booking.ChangeCustomerCommand.Execute(null);
        Assert.Equal("Neuer Termin", tab.Text);
    }

    [Fact]
    public async Task Andere_Reiter_tragen_kein_Kennzeichen_in_Bearbeitung()
    {
        var (vm, _) = await StartedAsync();

        Assert.True(vm.SelectNewEntry(2));

        var tab = Assert.Single(vm.OpenPanels);
        Assert.Equal("Paar oder Spa-Tag", tab.Text);
        Assert.False(tab.IsInProgress);
        Assert.Equal("Paar oder Spa-Tag", tab.Name);
        Assert.Equal("Paar oder Spa-Tag", tab.Tip);
    }
}
