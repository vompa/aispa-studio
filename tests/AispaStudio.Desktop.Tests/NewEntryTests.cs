using AispaStudio.Contracts;
using AispaStudio.Desktop.ViewModels;
using Avalonia.Input;

namespace AispaStudio.Desktop.Tests;

/// <summary>
/// Gemeinsames Fenster für neue Einträge: freie Lücke einer Spalte, Vorschlag der Art, Passung der Behandlungen, Umschalten und Kürzel.
/// Im Testtag arbeitet Anna 9 bis 17 Uhr mit Terminen 10:00 (belegt 9:55 bis 11:00) und 12:00 (belegt 11:55 bis 13:00);
/// eine Massage braucht samt Vor- und Nachbereitung 65 Minuten.
/// </summary>
public sealed partial class NewEntryTests
{
    private static readonly TimeZoneInfo Vienna = TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna");

    private sealed class FixedTime : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => TestData.At(10);

        public override TimeZoneInfo LocalTimeZone => Vienna;
    }

    private static CalendarDay Day(params BlockDto[] blocks) =>
        TestData.Day(TestData.Appointment(10, TestData.Anna), TestData.Appointment(12, TestData.Anna)) with
        {
            Blocks = blocks,
            Breaks = [new CalendarBreak(TestData.Anna.Id, 30, 0, null)],
        };

    private static async Task<CalendarViewModel> OpenAsync(CalendarDay day, LocationResources? resources = null)
    {
        var api = new StubApi()
            .On("GET /api/locations", new List<LocationDto> { TestData.Location })
            .On($"GET /api/locations/{TestData.LocationId}/resources", resources ?? TestData.Resources)
            .On($"GET /api/locations/{TestData.LocationId}/calendar?date=2027-03-01", day)
            .On($"GET /api/locations/{TestData.LocationId}/calendar?date=2027-03-02", day with { Date = TestData.Monday.AddDays(1) });
        var vm = new CalendarViewModel(api.Client(), TestData.Reception(), null, new FixedTime());
        await vm.InitializeAsync();
        await vm.LoadAsync();
        return vm;
    }

    private static double Y(CalendarViewModel vm, string hour, int extraMinutes = 0) =>
        vm.Hours.Single(h => h.Label == hour).Top + (extraMinutes * DayLayout.PixelsPerMinute) + 1;

    private static DateTimeOffset At(int hour, int minute = 0) => TestData.At(hour, minute);

    // ---------- Lücke ----------

    [Fact]
    public void Luecke_reicht_vom_Ende_des_vorigen_bis_zum_Beginn_des_naechsten_Stuecks_mit_Vor_und_Nachbereitung()
    {
        var day = Day();

        var between = DayLayout.GapAt(day, byRoom: false, TestData.Anna.Id, At(11));
        Assert.Equal(new FreeGap(At(11), At(11, 55)), between);
        Assert.Equal(55, between!.MinutesFrom(At(11)));

        Assert.Equal(new FreeGap(At(9), At(9, 55)), DayLayout.GapAt(day, false, TestData.Anna.Id, At(9, 15)));
        Assert.Equal(new FreeGap(At(13), At(17)), DayLayout.GapAt(day, false, TestData.Anna.Id, At(13, 15)));
    }

    [Fact]
    public void Luecke_endet_an_einer_Sperrzeit_und_beginnt_nach_ihr()
    {
        var day = Day(new BlockDto(Guid.NewGuid(), ResourceKind.Employee, TestData.Anna.Id, At(14), At(15), "Arzt"));

        Assert.Equal(new FreeGap(At(13), At(14)), DayLayout.GapAt(day, false, TestData.Anna.Id, At(13, 30)));
        Assert.Equal(new FreeGap(At(15), At(17)), DayLayout.GapAt(day, false, TestData.Anna.Id, At(15, 30)));
        Assert.Null(DayLayout.GapAt(day, false, TestData.Anna.Id, At(14, 15)));
    }

    [Fact]
    public void Luecke_gibt_es_nur_in_der_Dienstzeit_und_nur_an_freier_Stelle()
    {
        var day = Day();

        Assert.Null(DayLayout.GapAt(day, false, TestData.Anna.Id, At(10, 30)));
        Assert.Null(DayLayout.GapAt(day, false, TestData.Anna.Id, At(17, 15)));
        Assert.Null(DayLayout.GapAt(day, false, TestData.Ben.Id, At(9)));
    }

    [Fact]
    public void Luecke_ignoriert_stornierte_Termine()
    {
        var cancelled = TestData.Appointment(12, TestData.Anna, AppointmentStatus.Cancelled);
        var day = TestData.Day(TestData.Appointment(10, TestData.Anna), cancelled);

        Assert.Equal(new FreeGap(At(11), At(17)), DayLayout.GapAt(day, false, TestData.Anna.Id, At(11)));
    }

    [Fact]
    public void Luecke_im_Raum_haengt_an_der_Oeffnungszeit()
    {
        var day = Day();

        Assert.Equal(new FreeGap(At(11), At(11, 55)), DayLayout.GapAt(day, byRoom: true, TestData.Room.Id, At(11)));
        Assert.Equal(new FreeGap(At(13), At(20)), DayLayout.GapAt(day, true, TestData.Room.Id, At(14)));
        Assert.Null(DayLayout.GapAt(day, true, TestData.Room.Id, At(20, 15)));
    }

    [Fact]
    public void Pause_fuellt_hoechstens_bis_zur_naechsten_Belegung_auf_fuenf_Minuten_gerundet()
    {
        Assert.Equal(55, new FreeGap(At(11), At(11, 55)).FillMinutes(At(11)));
        Assert.Equal(55, new FreeGap(At(11), At(11, 58)).FillMinutes(At(11)));
        Assert.Equal(40, new FreeGap(At(11), At(11, 55)).FillMinutes(At(11, 15)));
    }

    // ---------- Behandlungen nach Passung ----------

    private static TreatmentDto Treatment(string name, int minutes, int preparation = 0, int followUp = 0) =>
        new(Guid.NewGuid(), name, minutes, preparation, followUp, 50m, [], [], [], null);

    [Fact]
    public void Behandlungen_die_passen_stehen_zuerst_beste_Passung_vorn_und_laengere_sind_gekennzeichnet()
    {
        var long60 = Treatment("Lang", 60);
        var exact = Treatment("Genau", 40, preparation: 5, followUp: 5);
        var short30 = Treatment("Kurz", 30);
        var mid45 = Treatment("Mittel", 43);
        var gap = new FreeGap(At(11), At(11, 52));

        var ranked = TreatmentFits.Rank([long60, short30, mid45, exact], gap, At(11, 5));

        Assert.Equal(["Genau", "Mittel", "Kurz", "Lang"], ranked.Select(o => o.Treatment.Name));
        Assert.Equal("passt (50 von 52 Min.)", ranked[0].Hint);
        Assert.Equal("passt (43 von 47 Min.)", ranked[1].Hint);
        Assert.Equal("länger als die freie Zeit", ranked[3].Hint);
        Assert.Equal(FitKind.TooLong, ranked[3].Fit);
    }

    [Fact]
    public void Hinweis_nennt_Belegung_und_verfuegbare_Minuten()
    {
        var option = TreatmentFits.Of(Treatment("Massage", 50, 5, 10), new FreeGap(At(13), At(17)), At(13, 15));

        Assert.Equal(FitKind.Fits, option.Fit);
        Assert.Equal("passt (65 von 230 Min.)", option.Hint);
    }

    [Fact]
    public void Endet_die_Behandlung_genau_mit_der_Luecke_steht_passt_genau()
    {
        var option = TreatmentFits.Of(Treatment("Kurz", 30), new FreeGap(At(11), At(11, 30)), At(11));

        Assert.Equal("passt genau", option.Hint);
    }

    [Fact]
    public void Ragt_die_Vorbereitung_in_das_Stueck_davor_steht_das_dabei()
    {
        var option = TreatmentFits.Of(Treatment("Massage", 50, 5, 10), new FreeGap(At(13), At(17)), At(13));

        Assert.Equal(FitKind.PrepConflict, option.Fit);
        Assert.Equal("Vorbereitung passt nicht davor", option.Hint);
    }

    [Fact]
    public void Ohne_Luecke_bleibt_die_Reihenfolge_und_es_gibt_keinen_Hinweis()
    {
        var first = Treatment("Erste", 30);
        var second = Treatment("Zweite", 60);
        var third = Treatment("Dritte", 90);

        var ranked = TreatmentFits.Rank([first, second, third], gap: null, At(10));

        Assert.Equal(["Erste", "Zweite", "Dritte"], ranked.Select(o => o.Treatment.Name));
        Assert.All(ranked, o => Assert.Null(o.Hint));
    }

    // ---------- Vorschlag ----------

    [Fact]
    public async Task Kurze_Luecke_schlaegt_eine_Pause_in_Laenge_der_Luecke_vor()
    {
        var vm = await OpenAsync(Day());

        vm.StartBookingAt(10, Y(vm, "11:00"));

        Assert.Equal(NewEntryKind.Break, vm.NewEntry!.Kind);
        Assert.Equal(CalendarEntryKind.Break, vm.Entry!.Kind);
        Assert.Equal("11:00", vm.Entry.StartText);
        Assert.Equal("11:55", vm.Entry.EndText);
        Assert.Null(vm.Booking);
    }

    [Fact]
    public async Task Lange_Luecke_schlaegt_einen_Termin_mit_passender_Behandlung_oben_vor()
    {
        var vm = await OpenAsync(Day());

        vm.StartBookingAt(10, Y(vm, "13:00", 15));

        Assert.Equal(NewEntryKind.Appointment, vm.NewEntry!.Kind);
        var booking = vm.Booking!;
        Assert.Equal("13:15", booking.StartText);
        Assert.Equal(TestData.MassageTreatment.Id, booking.Treatment?.Id);
        Assert.Equal(FitKind.Fits, booking.Options[0].Fit);
        Assert.Equal("passt (65 von 230 Min.)", booking.Options[0].Hint);
    }

    /// <summary>
    /// Given Annas Termin um 10:00 (belegt 9:55 bis 11:00) When ein Klick neben der Karte auf den Hintergrund trifft, in belegter Zeit oder in der Viertelstunde
    /// knapp davor Then öffnet kein neuer Termin; wie beim Überfahren (keine Vorschau) gibt es dort nichts zu buchen (Auftraggeber, 05.11.2026).
    /// </summary>
    [Theory]
    [InlineData("10:00", 15)]
    [InlineData("09:00", 45)]
    [InlineData("10:00", 50)]
    public async Task Klick_in_belegte_Zeit_neben_einem_Termin_oeffnet_keinen_neuen_Termin(string hour, int minutes)
    {
        var vm = await OpenAsync(Day());

        vm.StartBookingAt(10, Y(vm, hour, minutes));

        Assert.Null(vm.NewEntry);
        Assert.Null(vm.Booking);
    }

    [Fact]
    public async Task Person_ohne_passende_Behandlung_bekommt_keine_Pause_vorgeschlagen()
    {
        var vm = await OpenAsync(Day());

        // Ben kann keine der Behandlungen; ohne Maßstab bleibt es beim Termin.
        vm.StartBookingAt(DayLayout.ColumnLeft(1, vm.Scale) + 10, Y(vm, "12:00", 30));

        Assert.Equal(NewEntryKind.Appointment, vm.NewEntry!.Kind);
    }

    [Fact]
    public async Task In_der_Raumansicht_wird_bei_kurzer_Luecke_eine_Sperrzeit_vorgeschlagen_und_es_gibt_keine_Pause()
    {
        var vm = await OpenAsync(Day());
        vm.ShowRoomsCommand.Execute(null);

        vm.StartBookingAt(10, Y(vm, "11:00"));

        Assert.Equal(NewEntryKind.Block, vm.NewEntry!.Kind);
        Assert.Equal("11:55", vm.Entry!.EndText);
        Assert.DoesNotContain(vm.NewEntry.Choices, c => c.Kind == NewEntryKind.Break);
    }

    [Fact]
    public async Task Rechtsklick_Eintraege_waehlen_die_Art_fest_und_Pause_nimmt_hoechstens_die_Luecke()
    {
        var vm = await OpenAsync(Day());
        var x = 10.0;

        vm.ActionsAt(x, Y(vm, "11:00")).Single(a => a.Header.StartsWith("Termin hier", StringComparison.Ordinal)).Run!();
        Assert.Equal(NewEntryKind.Appointment, vm.NewEntry!.Kind);

        var pause = vm.ActionsAt(x, Y(vm, "11:00", 30)).Single(a => a.Header.StartsWith("Pause hier", StringComparison.Ordinal));
        Assert.Equal("Pause hier (25 Min.)", pause.Header);
        pause.Run!();
        Assert.Equal(NewEntryKind.Break, vm.NewEntry!.Kind);
        Assert.Equal("11:55", vm.Entry!.EndText);

        vm.ActionsAt(x, Y(vm, "14:00")).Single(a => a.Header.StartsWith("Pause hier", StringComparison.Ordinal)).Run!();
        Assert.Equal("14:30", vm.Entry!.EndText);
    }

    // ---------- Umschalten ----------

    [Fact]
    public async Task Umschalten_behaelt_Beginn_Spalte_und_getippten_Text()
    {
        var vm = await OpenAsync(Day());
        vm.OpenNoteAt(10, Y(vm, "14:00"));
        var entry = vm.Entry!;
        entry.StartText = "14:20";
        entry.Text = "Herr Lang ruft zurück";

        Assert.True(vm.SelectNewEntry(4));
        Assert.Equal(CalendarEntryKind.Block, vm.Entry!.Kind);
        Assert.Equal("14:20", vm.Entry.StartText);
        Assert.Equal("Herr Lang ruft zurück", vm.Entry.Text);
        Assert.Contains("Anna", vm.Entry.Where, StringComparison.Ordinal);

        Assert.True(vm.SelectNewEntry(3));
        Assert.Equal(CalendarEntryKind.Break, vm.Entry!.Kind);
        Assert.Equal("14:20", vm.Entry.StartText);
        Assert.False(vm.Entry.HasText);

        Assert.True(vm.SelectNewEntry(5));
        Assert.Equal("Herr Lang ruft zurück", vm.Entry!.Text);
        Assert.Equal("14:20", vm.Entry.StartText);
    }

    [Fact]
    public async Task Umschalten_nimmt_die_gewaehlte_Kundin_zwischen_Termin_und_Paar_mit()
    {
        var vm = await OpenAsync(Day());
        vm.StartBooking(vm.Columns[0], Y(vm, "14:00"));
        var customer = new CustomerDto(Guid.NewGuid(), "Lena", "Gruber", null, null, null, null);
        vm.Booking!.Customers.Add(customer);
        vm.Booking.Customer = customer;
        vm.Booking.StartText = "14:30";

        Assert.True(vm.SelectNewEntry(2));
        Assert.Equal(customer, vm.GroupBooking!.Guests[0].Search.Customer);

        Assert.True(vm.SelectNewEntry(1));
        Assert.Equal(customer, vm.Booking!.Customer);
        Assert.Equal("14:30", vm.Booking.StartText);
    }

    [Fact]
    public async Task Nicht_vorhandene_Arten_lassen_sich_nicht_waehlen()
    {
        var vm = await OpenAsync(Day());
        vm.ShowRoomsCommand.Execute(null);
        vm.StartBooking(vm.Columns[0], Y(vm, "14:00"));

        Assert.False(vm.SelectNewEntry(3));
        Assert.False(vm.SelectNewEntry(6));
        Assert.Equal(NewEntryKind.Appointment, vm.NewEntry!.Kind);
    }

    // ---------- Kürzel ----------

    [Fact]
    public async Task Umschalt_B_oeffnet_die_Pause_und_Komma_die_Notiz_im_selben_Fenster()
    {
        var vm = await OpenAsync(Day());

        Assert.True(vm.HandleKey(Key.B, KeyModifiers.Shift));
        Assert.Equal(CalendarEntryKind.Break, vm.Entry?.Kind);
        Assert.Equal("10:30", vm.Entry!.EndText);

        Assert.True(vm.HandleKey(Key.B, KeyModifiers.None));
        Assert.Equal(CalendarEntryKind.Block, vm.Entry?.Kind);
        vm.Entry!.Text = "Arzt";

        var window = vm.NewEntry;
        Assert.True(vm.HandleKey(Key.OemComma, KeyModifiers.None));
        Assert.Same(window, vm.NewEntry);
        Assert.Equal(CalendarEntryKind.Note, vm.Entry?.Kind);
        Assert.Equal("Arzt", vm.Entry!.Text);
    }

    [Fact]
    public async Task Alt_Ziffer_wechselt_die_Art_und_Escape_schliesst()
    {
        var vm = await OpenAsync(Day());
        vm.HandleKey(Key.N, KeyModifiers.None);
        Assert.NotNull(vm.NewEntry);

        Assert.True(vm.HandleKey(Key.D4, KeyModifiers.Alt));
        Assert.Equal(NewEntryKind.Block, vm.NewEntry!.Kind);
        Assert.True(vm.HandleKey(Key.NumPad3, KeyModifiers.Alt));
        Assert.Equal(NewEntryKind.Break, vm.NewEntry.Kind);
        Assert.True(vm.NewEntry.Choices.Single(c => c.Kind == NewEntryKind.Break).IsOn);
        Assert.False(vm.NewEntry.Choices.Single(c => c.Kind == NewEntryKind.Block).IsOn);

        vm.HandleKey(Key.Escape, KeyModifiers.None);
        Assert.Null(vm.NewEntry);
    }

    [Fact]
    public async Task Pause_in_der_Raumansicht_ist_nicht_moeglich()
    {
        var vm = await OpenAsync(Day());
        vm.ShowRoomsCommand.Execute(null);

        vm.HandleKey(Key.B, KeyModifiers.Shift);

        Assert.Null(vm.NewEntry);
    }

    [Fact]
    public async Task Ohne_Buchungsrecht_gibt_es_das_Fenster_nicht()
    {
        var api = new StubApi()
            .On("GET /api/locations", new List<LocationDto> { TestData.Location })
            .On($"GET /api/locations/{TestData.LocationId}/resources", TestData.Resources)
            .On($"GET /api/locations/{TestData.LocationId}/calendar?date=2027-03-01", Day());
        var user = new CurrentUser("u2", "t@aispa.test", "Tina", 1, new CurrentAccess(Guid.NewGuid(), "Test-Spa", StaffRole.Therapist, true, []), TestData.Anna.Id,
            [AccessRight.ViewCalendar]);
        var vm = new CalendarViewModel(api.Client(), user, null, new FixedTime());
        await vm.InitializeAsync();
        await vm.LoadAsync();

        vm.StartBookingAt(10, Y(vm, "11:00"));
        vm.HandleKey(Key.B, KeyModifiers.Shift);
        vm.HandleKey(Key.OemComma, KeyModifiers.None);

        Assert.Null(vm.NewEntry);
    }
}
