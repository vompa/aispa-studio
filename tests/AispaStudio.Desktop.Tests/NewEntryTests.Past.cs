using AispaStudio.Contracts;
using AispaStudio.Desktop.ViewModels;

namespace AispaStudio.Desktop.Tests;

/// <summary>
/// Neue Einträge in der Vergangenheit und Zeiten beim Wechsel der Art (Runde R03): der Kalender trägt nichts an einem früheren Tag ein,
/// heute geht der ganze Tag; die Pause ist ohne eigene Pausenzeit 60 Minuten lang; ein Wechsel des Beginns zieht das Ende mit.
/// „Heute“ ist im Testtag der 1. März 2027, 10:00 Uhr (<see cref="FixedTime"/>).
/// </summary>
public sealed partial class NewEntryTests
{
    // Der Toast zeigt den Satz ohne Schlusspunkt (FitToast).
    private const string PastMessage = "Termine lassen sich nicht in der Vergangenheit eintragen";

    private static async Task<CalendarViewModel> OpenWithNeighboursAsync(CalendarDay day)
    {
        var api = new StubApi()
            .On("GET /api/locations", new List<LocationDto> { TestData.Location })
            .On($"GET /api/locations/{TestData.LocationId}/resources", TestData.Resources)
            .On($"GET /api/locations/{TestData.LocationId}/calendar?date=2027-02-28", day with { Date = TestData.Monday.AddDays(-1) })
            .On($"GET /api/locations/{TestData.LocationId}/calendar?date=2027-03-01", day)
            .On($"GET /api/locations/{TestData.LocationId}/calendar?date=2027-03-02", day with { Date = TestData.Monday.AddDays(1) });
        var vm = new CalendarViewModel(api.Client(), TestData.Reception(), null, new FixedTime());
        await vm.InitializeAsync();
        await vm.LoadAsync();
        return vm;
    }

    private static CalendarDay DayWithoutBreak() => Day() with { Breaks = [] };

    // ---------- Vergangenheit ----------

    [Fact]
    public async Task Klick_auf_einen_Slot_am_Vortag_oeffnet_kein_Neuer_Termin_und_zeigt_den_Hinweis()
    {
        var vm = await OpenWithNeighboursAsync(Day());
        vm.PreviousDayCommand.Execute(null);
        await vm.LoadAsync();
        Assert.Equal(TestData.Monday.AddDays(-1), vm.Date);

        vm.StartBookingAt(10, Y(vm, "13:00"));

        Assert.Null(vm.NewEntry);
        Assert.Null(vm.Booking);
        Assert.Equal(PastMessage, vm.Toast);
    }

    [Fact]
    public async Task Auch_Pause_Notiz_Sperrzeit_und_das_Kontextmenue_oeffnen_am_Vortag_nichts()
    {
        var vm = await OpenWithNeighboursAsync(Day());
        vm.PreviousDayCommand.Execute(null);
        await vm.LoadAsync();

        vm.OpenBreakAt(10, Y(vm, "13:00"));
        Assert.Null(vm.NewEntry);
        vm.OpenNoteAt(10, Y(vm, "13:00"));
        Assert.Null(vm.NewEntry);
        vm.OpenBlockAt(10, Y(vm, "13:00"));
        Assert.Null(vm.NewEntry);
        vm.ActionsAt(10, Y(vm, "13:00")).Single(a => a.Header.StartsWith("Termin hier", StringComparison.Ordinal)).Run!();
        Assert.Null(vm.NewEntry);
        Assert.Equal(PastMessage, vm.Toast);
    }

    [Fact]
    public async Task Heute_oeffnet_der_Slot_den_ganzen_Tag_auch_vor_der_aktuellen_Uhrzeit()
    {
        var vm = await OpenWithNeighboursAsync(Day());

        // Jetzt ist es 10:00; 9:00 liegt davor und geht trotzdem, damit sich ein Gast nachtragen lässt.
        vm.ActionsAt(10, Y(vm, "09:00")).Single(a => a.Header.StartsWith("Termin hier", StringComparison.Ordinal)).Run!();

        Assert.NotNull(vm.NewEntry);
        Assert.Equal(NewEntryKind.Appointment, vm.NewEntry.Kind);
        Assert.Equal("09:00", vm.Booking!.StartText);
        Assert.Null(vm.Toast);
    }

    [Fact]
    public async Task Morgen_oeffnet_der_Slot()
    {
        var vm = await OpenWithNeighboursAsync(Day());
        vm.NextDayCommand.Execute(null);
        await vm.LoadAsync();

        vm.OpenNoteAt(10, Y(vm, "13:00"));

        Assert.NotNull(vm.NewEntry);
        Assert.Null(vm.Toast);
    }

    // ---------- Länge der Pause ----------

    [Fact]
    public async Task Pause_ohne_eigene_Pausenzeit_dauert_60_Minuten()
    {
        var vm = await OpenWithNeighboursAsync(DayWithoutBreak());

        var action = vm.ActionsAt(10, Y(vm, "13:00")).Single(a => a.Header.StartsWith("Pause hier", StringComparison.Ordinal));
        action.Run!();

        Assert.Equal("Pause hier (60 Min.)", action.Header);
        Assert.Equal(("13:00", "14:00"), (vm.Entry!.StartText, vm.Entry.EndText));
    }

    [Fact]
    public async Task Pause_mit_eigener_Pausenzeit_nimmt_deren_Wert()
    {
        var vm = await OpenWithNeighboursAsync(Day() with { Breaks = [new CalendarBreak(TestData.Anna.Id, 45, 0, null)] });

        vm.OpenBreakAt(10, Y(vm, "13:00"));

        Assert.Equal(("13:00", "13:45"), (vm.Entry!.StartText, vm.Entry.EndText));
    }

    [Fact]
    public async Task Pause_ist_nie_laenger_als_die_freie_Luecke()
    {
        var vm = await OpenWithNeighboursAsync(DayWithoutBreak());

        // Zwischen 11:00 und dem Termin um 12:00 (belegt ab 11:55) sind es 55 Minuten, nicht 60.
        vm.OpenBreakAt(10, Y(vm, "11:00"));

        Assert.Equal(("11:00", "11:55"), (vm.Entry!.StartText, vm.Entry.EndText));
    }

    // ---------- Beginn beim Wechsel der Art ----------

    [Fact]
    public async Task Wechsel_von_Termin_zu_Pause_mit_anderem_Beginn_zieht_das_Ende_um_denselben_Abstand_mit()
    {
        var vm = await OpenWithNeighboursAsync(DayWithoutBreak());
        vm.StartBooking(vm.Columns[0], Y(vm, "13:00"));
        vm.Booking!.StartText = "11:00";

        Assert.True(vm.SelectNewEntry(3));

        // Standardlänge 60: Beginn 11:00, Ende 12:00 (nicht das Ende der Pause am angeklickten 13:00).
        Assert.Equal(CalendarEntryKind.Break, vm.Entry!.Kind);
        Assert.Equal(("11:00", "12:00"), (vm.Entry.StartText, vm.Entry.EndText));
    }

    [Fact]
    public async Task Wechsel_mit_gleichem_Beginn_laesst_das_Ende_stehen()
    {
        var vm = await OpenWithNeighboursAsync(DayWithoutBreak());
        vm.StartBooking(vm.Columns[0], Y(vm, "13:00"));

        Assert.True(vm.SelectNewEntry(3));

        Assert.Equal(("13:00", "14:00"), (vm.Entry!.StartText, vm.Entry.EndText));
    }

    [Fact]
    public async Task Beginn_ueber_Mitternacht_oder_hinter_dem_Ende_setzt_das_Ende_auf_Beginn_plus_15_Minuten()
    {
        var vm = await OpenWithNeighboursAsync(DayWithoutBreak());
        vm.OpenBreakAt(10, Y(vm, "13:00"));
        var entry = vm.Entry!;

        // Stand das Ende schon auf dem Beginn, bleibt kein Abstand zum Mitziehen: Beginn plus 15 Minuten.
        entry.EndText = "13:00";
        entry.MoveStartTo("12:00");
        Assert.Equal(("12:00", "12:15"), (entry.StartText, entry.EndText));

        // 14:00 plus 11:30 Stunden gäbe 01:30 des nächsten Tages: das Ende bleibt am selben Tag.
        entry.EndText = "14:00";
        entry.MoveStartTo("23:30");
        Assert.Equal(("23:30", "23:45"), (entry.StartText, entry.EndText));
    }

    [Fact]
    public async Task Unlesbares_Ende_oder_unlesbarer_Beginn_lassen_das_Ende_stehen()
    {
        var vm = await OpenWithNeighboursAsync(DayWithoutBreak());
        vm.OpenBreakAt(10, Y(vm, "13:00"));
        var entry = vm.Entry!;

        entry.EndText = "";
        entry.MoveStartTo("11:00");
        Assert.Equal(("11:00", ""), (entry.StartText, entry.EndText));

        entry.EndText = "14:00";
        entry.StartText = "xx";
        entry.MoveStartTo("11:30");
        Assert.Equal(("11:30", "14:00"), (entry.StartText, entry.EndText));
    }
}
