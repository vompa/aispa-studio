using System.Net;
using AispaStudio.Contracts;
using AispaStudio.Desktop.ViewModels;
using Avalonia.Input;

namespace AispaStudio.Desktop.Tests;

/// <summary>„Freie Zeit suchen“: immer mindestens drei Vorschläge über mehrere Tage, Raum, Gast mit seinen Wünschen und direktes Buchen.</summary>
public sealed class FreeTimeSearchTests
{
    private static readonly TimeZoneInfo Vienna = TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna");
    private static readonly RoomDto Lotus = new(Guid.NewGuid(), TestData.LocationId, "Raum Lotus", 10);
    private static readonly LocationResources Resources = TestData.Resources with
    {
        Rooms = [TestData.Room, Lotus],
        Treatments = [TestData.Facial, TestData.MassageTreatment with { RoomIds = [TestData.Room.Id, Lotus.Id] }],
    };

    private sealed class FixedTime : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => TestData.At(10);

        public override TimeZoneInfo LocalTimeZone => Vienna;
    }

    private static AvailableSlot Slot(int dayOffset, int hour, EmployeeDto who, RoomDto? room = null) => new(
        TestData.At(hour).AddDays(dayOffset), TestData.At(hour, 50).AddDays(dayOffset), who.Id, (room ?? TestData.Room).Id, null);

    private static string Route(string extra = "") =>
        $"GET /api/locations/{TestData.LocationId}/availability?treatmentId={TestData.MassageTreatment.Id}&date=2027-03-01{extra}&minSlots=3&days=14";

    private static StubApi Api() => new StubApi()
        .On("GET /api/locations", new List<LocationDto> { TestData.Location })
        .On($"GET /api/locations/{TestData.LocationId}/resources", Resources)
        .On($"GET /api/locations/{TestData.LocationId}/calendar?date=2027-03-01", TestData.Day())
        .On($"GET /api/locations/{TestData.LocationId}/calendar?date=2027-03-02", TestData.Day() with { Date = TestData.Monday.AddDays(1) });

    private static async Task<CalendarViewModel> OpenAsync(StubApi api, CurrentUser? user = null)
    {
        var vm = new CalendarViewModel(api.Client(), user ?? TestData.Reception(), null, new FixedTime());
        await vm.InitializeAsync();
        await vm.LoadAsync();
        return vm;
    }

    private static async Task Until(Func<bool> condition)
    {
        for (var i = 0; i < 300 && !condition(); i++)
        {
            await Task.Delay(10);
        }

        Assert.True(condition());
    }

    private static async Task<FreeTimeViewModel> SearchAsync(CalendarViewModel vm, Action<FreeTimeViewModel>? prepare = null)
    {
        Assert.True(vm.HandleKey(Key.N, KeyModifiers.Control));
        var freeTime = vm.FreeTime!;
        prepare?.Invoke(freeTime);
        freeTime.Treatment = freeTime.Treatments.Single(t => t.Id == TestData.MassageTreatment.Id);
        await Until(() => !freeTime.Busy);
        return freeTime;
    }

    [Fact]
    public async Task Ist_am_Tag_weniger_als_drei_frei_kommen_Vorschlaege_der_Folgetage_mit_Tag_und_Uhrzeit()
    {
        var api = Api().On(Route(), new List<AvailableSlot> { Slot(0, 15, TestData.Anna), Slot(1, 9, TestData.Anna), Slot(1, 14, TestData.Ben) });
        var vm = await OpenAsync(api);

        var freeTime = await SearchAsync(vm);
        await Until(() => freeTime.Slots.Count == 3);

        Assert.Equal([null, "Di. 2.3.", "Di. 2.3."], freeTime.Slots.Select(s => s.DayText));
        Assert.Equal([true, false, false], freeTime.Slots.Select(s => s.OnThisDay));
        // Es ist heute noch etwas frei: der Hinweis „An diesem Tag ist nichts frei.“ entfällt.
        Assert.Null(freeTime.DayHint);
        // Im Kalender leuchtet nur, was am gezeigten Tag liegt: eine Fläche und der Rahmen der gewählten Zeit, beide bei Anna.
        Assert.Equal(2, vm.SlotMarks.Count);
        Assert.Equal(vm.SlotMarks[0].Left, vm.SlotMarks[1].Left);
    }

    [Fact]
    public async Task Ist_am_Tag_nichts_frei_steht_der_Hinweis_klein_ueber_den_Vorschlaegen_der_Folgetage()
    {
        var api = Api().On(Route(), new List<AvailableSlot> { Slot(1, 9, TestData.Anna), Slot(1, 14, TestData.Ben), Slot(2, 10, TestData.Anna) });
        var vm = await OpenAsync(api);

        var freeTime = await SearchAsync(vm);
        await Until(() => freeTime.Slots.Count == 3);

        Assert.Equal("An diesem Tag ist nichts frei.", freeTime.DayHint);
        Assert.All(freeTime.Slots, s => Assert.NotNull(s.DayText));
        Assert.Empty(vm.SlotMarks);
        Assert.Equal(3, freeTime.Top().Count);
    }

    [Fact]
    public async Task Ist_auch_in_14_Tagen_nichts_frei_nennt_die_Meldung_Ursache_und_Weg_statt_der_Zusammenfassung()
    {
        var api = Api().On(Route(), new List<AvailableSlot>());

        var freeTime = await SearchAsync(await OpenAsync(api), f => f.Part = DayPartChoice.All.Single(p => p.Value == DayPart.Afternoon));

        Assert.False(freeTime.HasSlots);
        Assert.True(freeTime.HasNoSlots);
        Assert.Equal("In den nächsten 14 Tagen ist nichts frei: nachmittags.", freeTime.NoSlotsMessage);
        Assert.Equal("Tageszeit: egal", freeTime.RelaxText);
        // Die Zusammenfassung wiederholt es nicht als kleiner grauer Text.
        Assert.DoesNotContain("nichts frei", freeTime.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Der_Weg_Tageszeit_egal_lockert_die_Suche_und_sucht_neu()
    {
        var api = Api().On(Route(), new List<AvailableSlot>());
        var freeTime = await SearchAsync(await OpenAsync(api), f => f.Part = DayPartChoice.All.Single(p => p.Value == DayPart.Afternoon));
        api.On(Route(), new List<AvailableSlot> { Slot(0, 11, TestData.Anna), Slot(0, 12, TestData.Anna), Slot(1, 9, TestData.Ben) });

        freeTime.RelaxCommand.Execute(null);
        await Until(() => freeTime.Slots.Count == 3);

        Assert.Equal(DayPart.Any, freeTime.Part.Value);
        Assert.False(freeTime.HasNoSlots);
    }

    [Fact]
    public async Task Ohne_Einschraenkung_bleibt_nur_der_Rat_zur_anderen_Behandlung_ohne_Knopf()
    {
        var api = Api().On(Route(), new List<AvailableSlot>());

        var freeTime = await SearchAsync(await OpenAsync(api));

        Assert.True(freeTime.HasNoSlots);
        Assert.Null(freeTime.RelaxText);
        Assert.Equal("In den nächsten 14 Tagen ist nichts frei. Eine andere Behandlung kann mehr Platz haben.", freeTime.NoSlotsMessage);
    }

    [Fact]
    public async Task Eine_Person_als_Einschraenkung_wird_genannt_und_als_zweiter_Weg_angeboten()
    {
        var api = Api().On(Route(), new List<AvailableSlot>()).On(Route($"&employeeId={TestData.Anna.Id}"), new List<AvailableSlot>());
        var vm = await OpenAsync(api);
        var freeTime = await SearchAsync(vm, f => f.Person = f.People.Single(p => p.Id == TestData.Anna.Id));

        Assert.Equal("In den nächsten 14 Tagen ist nichts frei: bei Anna.", freeTime.NoSlotsMessage);
        Assert.Equal("Alle Personen", freeTime.RelaxText);
    }

    [Fact]
    public async Task Die_Auswahl_Raum_gibt_den_Raum_an_den_Server_und_zeigt_nur_Raeume_der_Behandlung()
    {
        var api = Api().On(Route(), new List<AvailableSlot> { Slot(0, 15, TestData.Anna) })
            .On(Route($"&roomId={Lotus.Id}"), new List<AvailableSlot> { Slot(0, 16, TestData.Ben, Lotus), Slot(1, 10, TestData.Ben, Lotus), Slot(1, 11, TestData.Anna, Lotus) });
        var vm = await OpenAsync(api);

        var freeTime = await SearchAsync(vm);
        Assert.Equal(["Alle", "Raum Zeder", "Raum Lotus"], freeTime.Rooms.Select(r => r.Name));
        freeTime.Room = freeTime.Rooms.Single(r => r.Id == Lotus.Id);
        await Until(() => freeTime.Slots.Count == 3);

        Assert.All(freeTime.Slots, s => Assert.Equal("Raum Lotus", s.Room));
        // Das Gesicht läuft nur im Raum Zeder: mit dieser Behandlung gibt es den Raum Lotus nicht zur Auswahl.
        freeTime.Treatment = freeTime.Treatments.Single(t => t.Id == TestData.Facial.Id);
        Assert.Equal(["Alle"], freeTime.Rooms.Select(r => r.Name).Where(n => n == "Alle"));
        Assert.DoesNotContain(freeTime.Rooms, r => r.Id == Lotus.Id);
        Assert.Null(freeTime.Room.Id);
    }

    private static CustomerDto Guest(CustomerBookingPreferences? wishes = null, string? language = null) =>
        new(Guid.NewGuid(), "Brigitte", "Müller", "b@example.at", "+43 1 000", null, null, Booking: wishes, Language: language);

    [Fact]
    public async Task Ein_Gast_bringt_feste_Person_Sprache_und_Wunschzeiten_in_die_Suche_mit()
    {
        var guest = Guest(new CustomerBookingPreferences(TestData.Ben.Id, EmployeeFixed: true, Weekdays: [DayOfWeek.Tuesday], From: new TimeOnly(12, 0), Until: new TimeOnly(18, 0)));
        var resources = Resources with { Employees = [TestData.Anna, TestData.Ben with { Languages = ["it"] }] };
        var route = Route($"&employeeId={TestData.Ben.Id}&customerId={guest.Id}");
        var api = new StubApi()
            .On("GET /api/locations", new List<LocationDto> { TestData.Location })
            .On($"GET /api/locations/{TestData.LocationId}/resources", resources)
            .On($"GET /api/locations/{TestData.LocationId}/calendar?date=2027-03-01", TestData.Day())
            .On(route, new List<AvailableSlot> { Slot(0, 15, TestData.Ben), Slot(1, 10, TestData.Ben), Slot(1, 14, TestData.Ben) });
        var vm = await OpenAsync(api);

        var freeTime = await SearchAsync(vm);
        freeTime.Guest.Customer = guest;
        await Until(() => freeTime.Slots.Count == 3);

        // Die feste Person ist vorgewählt, die Sprache des Gastes gesetzt; die Anfrage nennt den Gast.
        Assert.Equal(TestData.Ben.Id, freeTime.Person.Id);
        Assert.StartsWith("Feste Therapeut:in: Ben · lieber Di", freeTime.GuestHint, StringComparison.Ordinal);
        Assert.Contains(api.Calls, c => c.Route == route);
        // Dienstag 14 Uhr passt zu Wochentag und Uhrzeit und steht vor dem Montag und vor Dienstag 10 Uhr.
        Assert.Equal(["Di. 2.3.", null, "Di. 2.3."], freeTime.Slots.Select(s => s.DayText));
        Assert.Equal(TestData.At(14).AddDays(1), freeTime.Slots[0].Slot.Start);
        Assert.Null(freeTime.Slots[0].OutsideWish);
        Assert.All(freeTime.Slots.Skip(1), s => Assert.Equal("außerhalb der Wunschzeit des Gastes", s.OutsideWish));
        Assert.All(freeTime.Slots, s => Assert.True(s.IsWishPerson));
        Assert.Equal("Für Brigitte Müller buchen (Enter)", freeTime.BookText);
    }

    [Fact]
    public async Task Ein_gewaehlter_Vorschlag_wird_mit_einem_Klick_fuer_den_Gast_gebucht()
    {
        var guest = Guest();
        var slot = Slot(0, 15, TestData.Anna);
        var booked = TestData.Appointment(15, TestData.Anna) with { CustomerId = guest.Id, CustomerName = "Brigitte Müller" };
        var api = Api().On(Route($"&customerId={guest.Id}"), new List<AvailableSlot> { slot })
            .On(Route(), new List<AvailableSlot> { slot })
            .On("POST /api/appointments", booked);
        var vm = await OpenAsync(api);
        var freeTime = await SearchAsync(vm);
        freeTime.Guest.Customer = guest;
        await Until(() => freeTime.HasSlots && freeTime.BookText.StartsWith("Für", StringComparison.Ordinal));
        await Until(() => !freeTime.Busy);

        await freeTime.BookActiveCommand.ExecuteAsync(null);

        var post = Assert.Single(api.Calls, c => c.Route == "POST /api/appointments");
        Assert.Contains(guest.Id.ToString(), post.Body, StringComparison.Ordinal);
        Assert.Contains(TestData.Anna.Id.ToString(), post.Body, StringComparison.Ordinal);
        Assert.Null(vm.NewEntry);
        Assert.Contains(vm.Columns.SelectMany(c => c.Items), i => i.Id == booked.Id);
        Assert.StartsWith("Gebucht: Brigitte Müller", vm.Toast, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Nach_dem_Buchen_fuer_den_Gast_bleibt_kein_Reiter_mit_der_alten_Liste_stehen()
    {
        // Auftraggeber, 28.10.2026: die gebuchte Zeit stand im Reiter weiter in der Liste, ein zweiter Klick darauf meldete „belegt“.
        var guest = Guest();
        var slot = Slot(0, 15, TestData.Anna);
        var booked = TestData.Appointment(15, TestData.Anna) with { CustomerId = guest.Id, CustomerName = "Brigitte Müller" };
        var api = Api().On(Route($"&customerId={guest.Id}"), new List<AvailableSlot> { slot, Slot(0, 17, TestData.Anna) })
            .On(Route(), new List<AvailableSlot> { slot })
            .On("POST /api/appointments", booked);
        var vm = await OpenAsync(api);
        var freeTime = await SearchAsync(vm);
        freeTime.Guest.Customer = guest;
        await Until(() => freeTime.Slots.Count == 2 && !freeTime.Busy);

        await freeTime.BookActiveCommand.ExecuteAsync(null);

        Assert.StartsWith("Gebucht: Brigitte Müller", vm.Toast, StringComparison.Ordinal);
        Assert.Null(vm.FreeTime);
        Assert.DoesNotContain(vm.OpenPanels, t => t.Kind == PanelKind.FreeTime);
    }

    [Fact]
    public async Task Der_Reiter_Freie_Zeit_laedt_beim_Zurueckholen_die_Zeiten_neu()
    {
        var api = Api().On(Route(), new List<AvailableSlot> { Slot(0, 15, TestData.Anna), Slot(0, 17, TestData.Ben) });
        var vm = await OpenAsync(api);
        var freeTime = await SearchAsync(vm);
        await Until(() => freeTime.Slots.Count == 2 && !freeTime.Busy);

        // Ohne Gast öffnet die Zeit „Neuer Termin“; „Freie Zeit suchen“ bleibt als Reiter stehen. Inzwischen ist 15 Uhr vergeben.
        await freeTime.BookActiveCommand.ExecuteAsync(null);
        api.On(Route(), new List<AvailableSlot> { Slot(0, 17, TestData.Ben) });
        vm.SelectPanelTabCommand.Execute(vm.OpenPanels.Single(t => t.Kind == PanelKind.FreeTime));

        Assert.Same(freeTime, vm.FreeTime);
        await Until(() => freeTime.Slots.Count == 1 && !freeTime.Busy);
        Assert.Equal(TestData.At(17), freeTime.Slots[0].Slot.Start);
    }

    [Fact]
    public async Task Lehnt_der_Server_die_Buchung_ab_bleibt_Neuer_Termin_mit_der_Begruendung_offen()
    {
        var guest = Guest();
        var slot = Slot(0, 15, TestData.Anna);
        var api = Api().On(Route(), new List<AvailableSlot> { slot }).On(Route($"&customerId={guest.Id}"), new List<AvailableSlot> { slot })
            .On("POST /api/appointments", new { title = "Diese Zeit wurde gerade anderweitig vergeben." }, HttpStatusCode.Conflict);
        var vm = await OpenAsync(api);
        var freeTime = await SearchAsync(vm);
        freeTime.Guest.Customer = guest;
        await Until(() => freeTime.HasSlots);
        await Until(() => !freeTime.Busy);

        await freeTime.BookActiveCommand.ExecuteAsync(null);

        Assert.NotNull(vm.Booking);
        Assert.Equal(guest.Id, vm.Booking!.Customer!.Id);
        Assert.False(string.IsNullOrEmpty(vm.Booking.Error));
        Assert.Null(vm.Toast);
    }

    [Fact]
    public async Task Ohne_Gast_oeffnet_der_Vorschlag_Neuer_Termin_am_Tag_der_Zeit()
    {
        var api = Api().On(Route(), new List<AvailableSlot> { Slot(1, 9, TestData.Anna), Slot(1, 14, TestData.Ben), Slot(2, 10, TestData.Anna) });
        var vm = await OpenAsync(api);
        var freeTime = await SearchAsync(vm);
        await Until(() => freeTime.Slots.Count == 3);

        await freeTime.BookActiveCommand.ExecuteAsync(null);

        // Der Kalender ist zum Dienstag gewechselt; Neuer Termin steht bei Anna um 9:00.
        Assert.Equal(TestData.Monday.AddDays(1), vm.Date);
        var booking = Assert.IsType<BookingViewModel>(vm.NewEntry!.Content);
        Assert.Equal("09:00", booking.StartText);
        Assert.Equal(TestData.Anna.Id, booking.ColumnId);
        Assert.Null(booking.Customer);
    }

    [Fact]
    public async Task Aus_der_Warteliste_oeffnet_die_Suche_vorbelegt_mit_Behandlung_Zeitraum_und_Tageszeit()
    {
        var api = Api().On($"GET /api/locations/{TestData.LocationId}/calendar?date=2027-03-02", TestData.Day() with { Date = TestData.Monday.AddDays(1) })
            .On($"GET /api/locations/{TestData.LocationId}/availability?treatmentId={TestData.MassageTreatment.Id}&date=2027-03-02&minSlots=3&days=3",
                new List<AvailableSlot> { Slot(1, 9, TestData.Anna), Slot(1, 15, TestData.Anna), Slot(1, 17, TestData.Ben), Slot(2, 13, TestData.Anna), Slot(2, 16, TestData.Ben) });
        var vm = await OpenAsync(api);

        // Wunsch: Massage vom 2.3. bis 4.3., nachmittags.
        await vm.ShowFreeTimeAsync(new FreeTimePrefill(TestData.MassageTreatment.Id, Part: DayPart.Afternoon, Until: TestData.Monday.AddDays(3)), TestData.Monday.AddDays(1));
        var freeTime = vm.FreeTime!;
        await Until(() => freeTime.HasSlots && !freeTime.Busy);

        Assert.Equal(TestData.Monday.AddDays(1), vm.Date);
        Assert.Equal(TestData.MassageTreatment.Id, freeTime.Treatment!.Id);
        Assert.Equal("nachmittags", freeTime.Part.Label);
        // Nur nachmittags: 9 Uhr und 17 Uhr fallen weg.
        Assert.Equal([TestData.At(15).AddDays(1), TestData.At(13).AddDays(2), TestData.At(16).AddDays(2)], freeTime.Slots.Select(s => s.Slot.Start));
    }

    [Fact]
    public async Task Die_Tageszeit_holt_fehlende_Vorschlaege_an_den_naechsten_Tagen_nach()
    {
        // Der erste Abruf liefert nur Vormittage; „nachmittags“ lässt davon nichts übrig, also geht die Suche am Tag danach weiter.
        var api = Api()
            .On(Route(), new List<AvailableSlot> { Slot(0, 11, TestData.Anna), Slot(0, 11, TestData.Ben), Slot(0, 11, TestData.Anna, Lotus) })
            .On($"GET /api/locations/{TestData.LocationId}/availability?treatmentId={TestData.MassageTreatment.Id}&date=2027-03-02&minSlots=3&days=13",
                new List<AvailableSlot> { Slot(1, 14, TestData.Anna), Slot(1, 15, TestData.Ben), Slot(1, 16, TestData.Anna) });
        var vm = await OpenAsync(api);

        var freeTime = await SearchAsync(vm, f => f.Part = DayPartChoice.All.Single(p => p.Value == DayPart.Afternoon));
        await Until(() => freeTime.Slots.Count == 3);

        Assert.All(freeTime.Slots, s => Assert.Equal(TestData.Monday.AddDays(1), DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(s.Slot.Start, Vienna).DateTime)));
        Assert.Equal("An diesem Tag ist nichts frei.", freeTime.DayHint);
    }
}
