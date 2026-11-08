using AispaStudio.Contracts;
using AispaStudio.Desktop.ViewModels;
using Avalonia.Input;

namespace AispaStudio.Desktop.Tests;

/// <summary>7-Tage-Ansicht: Klick auf eine Person oder einen Raum zeigt deren Termine über sieben Tage.</summary>
public sealed class CalendarWeekTests
{
    private static readonly TimeZoneInfo Vienna = TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna");

    private sealed class FixedTime : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => TestData.At(10);

        public override TimeZoneInfo LocalTimeZone => Vienna;
    }

    /// <summary>Der Testmontag um <paramref name="days"/> Tage verschoben, mit neuen Ids; Anna hat einen Termin um 10 + Tag Uhr, Ben einen um 14 Uhr.</summary>
    private static CalendarDay DayAfter(int days)
    {
        AppointmentDto Shift(AppointmentDto a) => a with
        {
            Id = Guid.NewGuid(),
            Start = a.Start.AddDays(days),
            End = a.End.AddDays(days),
            BlockedFrom = a.BlockedFrom.AddDays(days),
            BlockedUntil = a.BlockedUntil.AddDays(days),
        };

        var day = TestData.Day(Shift(TestData.Appointment(10 + days % 7, TestData.Anna)), Shift(TestData.Appointment(14, TestData.Ben)));
        return day with
        {
            Date = TestData.Monday.AddDays(days),
            Opening = [.. day.Opening.Select(o => new TimeRange(o.Start.AddDays(days), o.End.AddDays(days)))],
            Shifts = [.. day.Shifts.Select(s => s with { Start = s.Start.AddDays(days), End = s.End.AddDays(days) })],
            // Jeder Tag meldet Annas Pause; in der 7-Tage-Ansicht steht sie damit siebenmal im zusammengefassten Tag.
            Breaks = [new CalendarBreak(TestData.Anna.Id, 30, 30, null)],
        };
    }

    private static StubApi Api(int days = 21)
    {
        var api = new StubApi()
            .On("GET /api/locations", new List<LocationDto> { TestData.Location })
            .On($"GET /api/locations/{TestData.LocationId}/resources", TestData.Resources);
        for (var i = 0; i < days; i++)
        {
            api.On($"GET /api/locations/{TestData.LocationId}/calendar?date={TestData.Monday.AddDays(i):yyyy-MM-dd}", DayAfter(i));
        }

        return api;
    }

    private static async Task<CalendarViewModel> OpenAsync(StubApi api)
    {
        var vm = new CalendarViewModel(api.Client(), TestData.Reception(), null, new FixedTime());
        await vm.InitializeAsync();
        await vm.LoadAsync();
        return vm;
    }

    private static async Task<CalendarViewModel> FilteredAsync(StubApi api, int column = 0)
    {
        var vm = await OpenAsync(api);
        vm.FilterBy(vm.Columns[column]);
        await WaitAsync(vm, v => v.Columns.Count == 7);
        return vm;
    }

    /// <summary>Wartet, bis die Antworten der Test-API verarbeitet sind.</summary>
    private static async Task WaitAsync(CalendarViewModel vm, Func<CalendarViewModel, bool> done)
    {
        for (var i = 0; i < 100 && !done(vm); i++)
        {
            await Task.Delay(10);
        }

        Assert.True(done(vm));
    }

    [Fact]
    public async Task Filter_aufheben_und_sofort_neu_zeichnen_stuerzt_nicht_ab()
    {
        // Ohne Verbindung (oder solange die Antwort noch unterwegs ist) bleiben die sieben zusammengefassten Tage geladen.
        var api = Api();
        var vm = await FilteredAsync(api);
        api.Down = true;

        vm.ClearFilterCommand.Execute(null);
        var exception = Record.Exception(() => vm.SetViewport(1200, 800));

        Assert.Null(exception);
        Assert.Equal(2, vm.Columns.Count);
    }

    [Fact]
    public async Task Klick_auf_eine_Person_zeigt_deren_Termine_ueber_sieben_Tage()
    {
        var api = Api();
        var vm = await FilteredAsync(api);

        Assert.True(vm.IsFiltered);
        Assert.Equal("Anna · 7 Tage", vm.FilterText);
        Assert.Equal("Anna", vm.DayTitle);
        Assert.Equal("1.–7. März 2027", vm.DateShort);
        Assert.Equal(["Mo. 1.3.", "Di. 2.3.", "Mi. 3.3.", "Do. 4.3.", "Fr. 5.3.", "Sa. 6.3.", "So. 7.3."], vm.Columns.Select(c => c.Title));
        Assert.All(vm.Columns, c => Assert.True(c.IsDay));
        // Anna hat an jedem Tag einen Termin, Bens Termine (14 Uhr) gehören nicht dazu.
        Assert.All(vm.Columns, c => Assert.Single(c.Items));
        Assert.All(vm.Columns.SelectMany(c => c.Items), i => Assert.Equal(TestData.Anna.Id, i.Appointment!.EmployeeId));
        var dates = api.Calls.Where(c => c.Route.Contains("/calendar?date=")).Select(c => c.Route[^10..]).Distinct().Order().ToList();
        Assert.Equal(Enumerable.Range(0, 7).Select(i => $"{TestData.Monday.AddDays(i):yyyy-MM-dd}"), dates);
    }

    [Fact]
    public async Task Der_Tagesueberblick_zaehlt_nur_die_Termine_der_Person()
    {
        var vm = await FilteredAsync(Api());

        Assert.Equal(7, vm.Overview!.Appointments);
        Assert.All(vm.Upcoming, u => Assert.Equal(TestData.Anna.Id, u.Appointment.EmployeeId));
    }

    [Fact]
    public async Task Die_Tage_liegen_auf_einer_Hoehe()
    {
        var vm = await FilteredAsync(Api());

        // Dienst von 9 bis 17 Uhr, Termin um 10 Uhr am Montag und um 16 Uhr am Sonntag: derselbe Maßstab für alle Spalten.
        var monday = vm.Columns[0].Items.Single();
        var sunday = vm.Columns[6].Items.Single();
        Assert.Equal((16 - 10) * 60 * vm.Scale.PixelsPerMinute, sunday.Top - monday.Top, 0.01);
    }

    [Fact]
    public async Task Pfeile_schalten_wochenweise_weiter()
    {
        var api = Api();
        var vm = await FilteredAsync(api);

        vm.NextDayCommand.Execute(null);
        await WaitAsync(vm, v => v.Date == TestData.Monday.AddDays(7) && v.Columns.Count == 7 && v.Columns[0].Items.Count == 1);
        Assert.Equal("8.–14. März 2027", vm.DateShort);
        Assert.Equal("Mo. 8.3.", vm.Columns[0].Title);

        vm.PreviousDayCommand.Execute(null);
        await WaitAsync(vm, v => v.Date == TestData.Monday);
        Assert.Equal("Mo. 1.3.", vm.Columns[0].Title);

        // Ohne Markierung wechseln auch ← und → wochenweise.
        Assert.True(vm.HandleKey(Key.Right, KeyModifiers.None));
        await WaitAsync(vm, v => v.Date == TestData.Monday.AddDays(7));
    }

    [Fact]
    public async Task Filter_aufheben_und_Esc_fuehren_zum_Tag_mit_allen_Spalten_zurueck()
    {
        var vm = await FilteredAsync(Api());

        vm.ClearFilterCommand.Execute(null);
        await WaitAsync(vm, v => !v.IsFiltered && v.Columns.Count == 2);
        Assert.Equal(["Anna", "Ben"], vm.Columns.Select(c => c.Title));
        Assert.Equal(TestData.Monday, vm.Date);

        vm.FilterBy(vm.Columns[1]);
        await WaitAsync(vm, v => v.Columns.Count == 7);
        Assert.Equal("Ben · 7 Tage", vm.FilterText);
        Assert.True(vm.HandleKey(Key.Escape, KeyModifiers.None));
        await WaitAsync(vm, v => !v.IsFiltered && v.Columns.Count == 2);
    }

    [Fact]
    public async Task In_der_Raumansicht_zeigt_der_Klick_auf_einen_Raum_dessen_Termine()
    {
        var vm = await OpenAsync(Api());
        vm.ByRoom = true;
        Assert.Equal(["Raum Zeder"], vm.Columns.Select(c => c.Title));

        vm.FilterBy(vm.Columns[0]);
        await WaitAsync(vm, v => v.Columns.Count == 7);

        Assert.Equal("Raum Zeder · 7 Tage", vm.FilterText);
        // Unter dem Tag steht die Öffnungszeit, nicht noch einmal, was der Raum kann.
        Assert.All(vm.Columns, c => Assert.Equal("09:00–20:00", c.Subtitle));
        // Beide Termine des Tages liegen im selben Raum.
        Assert.All(vm.Columns, c => Assert.Equal(2, c.Items.Count));
    }

    [Fact]
    public async Task Wechsel_zwischen_Team_und_Raeumen_hebt_den_Filter_auf()
    {
        var vm = await FilteredAsync(Api());

        vm.ShowRoomsCommand.Execute(null);
        await WaitAsync(vm, v => !v.IsFiltered && v.Columns.Count == 1);
        Assert.True(vm.ByRoom);
    }

    [Fact]
    public async Task In_der_Ansicht_eines_Gastes_wird_nicht_gebucht_und_nicht_gezogen()
    {
        // Ein Gast hat keine Spalte je Person oder Raum, in die sich buchen oder ziehen ließe.
        var guest = new CustomerDto(Guid.NewGuid(), "Steffi", "Huber", null, "+43 1", null, null);
        var api = Api().On($"GET /api/customers/{guest.Id}", guest).On($"GET /api/customers/{guest.Id}/appointments", new List<AppointmentDto>());
        var vm = await OpenAsync(api);
        await vm.FilterByCustomerAsync(guest.Id);
        await WaitAsync(vm, v => v.Columns.Count == 7);

        vm.StartBookingAt(40, 100);
        vm.Hover(40, 100);
        Assert.Null(vm.NewEntry);
        Assert.Null(vm.Ghost);
        Assert.Empty(vm.ActionsAt(40, 100));
        Assert.False(vm.CanMove(new CalendarItem(Guid.NewGuid(), "", "", "", 0, 0, "#000", AppointmentStatus.Booked, TestData.Appointment(10, TestData.Anna))));
    }

    private static double Y(CalendarViewModel vm, string hour, int extraMinutes = 0) =>
        vm.Hours.Single(h => h.Label == hour).Top + (extraMinutes * DayLayout.PixelsPerMinute) + 1;

    private static double X(CalendarViewModel vm, int column) => DayLayout.ColumnLeft(column, vm.Scale) + 10;

    [Fact]
    public async Task Klick_auf_freie_Zeit_der_dritten_Spalte_bucht_bei_der_Person_am_dritten_Tag()
    {
        var api = Api();
        var vm = await FilteredAsync(api);
        var booked = TestData.Appointment(13, TestData.Anna) with { Start = TestData.At(13, 15).AddDays(2), End = TestData.At(14, 5).AddDays(2) };
        api.On("POST /api/appointments", booked, System.Net.HttpStatusCode.Created)
            .On("GET /api/customers?search=eva", new List<CustomerDto> { new(booked.CustomerId, "Eva", "Muster", null, null, null, null) });

        vm.StartBookingAt(X(vm, 2), Y(vm, "13:00", 15));

        // Die Vorschau steht in der Tagesspalte, in die geklickt wurde.
        Assert.Equal(DayLayout.ColumnLeft(2, vm.Scale), vm.Ghost!.Left);
        var booking = vm.Booking!;
        Assert.Equal("Anna", booking.ColumnTitle);
        Assert.False(booking.ColumnIsRoom);
        Assert.Equal("13:15", booking.StartText);
        await booking.SearchAsync("eva");
        booking.Customer = booking.Customers[0];
        await booking.BookCommand.ExecuteAsync(null);

        // Mittwoch, 3. März 13:15 Uhr in Wien sind 12:15 Uhr UTC; die Buchung geht an Anna.
        var post = Assert.Single(api.Calls, c => c.Route == "POST /api/appointments");
        Assert.Contains("2027-03-03T12:15", post.Body, StringComparison.Ordinal);
        Assert.Contains(TestData.Anna.Id.ToString(), post.Body, StringComparison.Ordinal);
        Assert.Null(vm.Booking);
        Assert.Equal(2, vm.Columns[2].Items.Count);
        Assert.All(vm.Columns.Where((_, i) => i != 2), c => Assert.Single(c.Items));
    }

    [Fact]
    public async Task Pause_Sperrzeit_und_Notiz_gehoeren_zum_Tag_der_Spalte()
    {
        var vm = await FilteredAsync(Api());

        vm.OpenBreakAt(X(vm, 4), Y(vm, "13:00"));
        Assert.Equal(CalendarEntryKind.Break, vm.Entry!.Kind);
        Assert.Equal("Anna · Freitag, 5. März 2027", vm.Entry.Where);

        vm.OpenBlockAt(X(vm, 3), Y(vm, "13:00"));
        Assert.Equal(CalendarEntryKind.Block, vm.Entry!.Kind);
        Assert.Equal("Anna · Donnerstag, 4. März 2027", vm.Entry.Where);

        vm.OpenNoteAt(X(vm, 5), Y(vm, "13:00"));
        Assert.Equal(CalendarEntryKind.Note, vm.Entry!.Kind);
        Assert.Equal("Anna · Samstag, 6. März 2027", vm.Entry.Where);
    }

    [Fact]
    public async Task In_der_Raumansicht_bucht_der_Klick_in_den_Raum_am_Tag_der_Spalte()
    {
        var vm = await OpenAsync(Api());
        vm.ByRoom = true;
        vm.FilterBy(vm.Columns[0]);
        await WaitAsync(vm, v => v.Columns.Count == 7);

        vm.StartBookingAt(X(vm, 1), Y(vm, "18:00"));

        Assert.True(vm.Booking!.ColumnIsRoom);
        Assert.Equal("Raum Zeder", vm.Booking.ColumnTitle);
    }

    [Fact]
    public async Task Rechtsklick_auf_freie_Zeit_bietet_die_Eintraege_mit_Tag_und_Person()
    {
        var vm = await FilteredAsync(Api());

        var actions = vm.ActionsAt(X(vm, 2), Y(vm, "13:00", 15));

        Assert.Equal("Termin hier (Mi. 3.3. 13:15, Anna)", actions[0].Header);
        Assert.Contains(actions, a => a.Header.StartsWith("Pause hier", StringComparison.Ordinal));
        Assert.Contains(actions, a => a.Header == "Sperrzeit hier");
        Assert.DoesNotContain(actions, a => a.Header == "Tagesnotiz");
    }

    [Fact]
    public async Task Die_Vorschau_unter_der_Maus_liegt_in_der_Spalte_und_zeigt_die_Uhrzeit_des_Tages()
    {
        var vm = await FilteredAsync(Api());

        vm.Hover(X(vm, 2), Y(vm, "13:00", 15));

        Assert.NotNull(vm.Ghost);
        Assert.Equal(DayLayout.ColumnLeft(2, vm.Scale), vm.Ghost.Left);
        Assert.Equal(vm.Hours.Single(h => h.Label == "13:00").Top + (15 * DayLayout.PixelsPerMinute), vm.Ghost.Top, 0.01);
        Assert.Equal("13:15", vm.Ghost.Text);
    }

    [Fact]
    public async Task Ziehen_verschiebt_den_Termin_auf_einen_anderen_Tag_derselben_Person()
    {
        var api = Api();
        var vm = await FilteredAsync(api);
        var item = vm.Columns[0].Items.Single();
        var moved = item.Appointment! with { Start = TestData.At(11).AddDays(3), End = TestData.At(11, 50).AddDays(3), UpdatedAt = TestData.At(9) };
        api.On($"POST /api/appointments/{item.Appointment.Id}/move", moved);

        Assert.True(vm.CanMove(item));
        vm.BeginDrag(item, 5);
        vm.DragTo(X(vm, 3), Y(vm, "11:00") + 5);
        Assert.Equal(DayLayout.ColumnLeft(3, vm.Scale), vm.Ghost!.Left);
        Assert.Equal("11:00–11:50 · Do. 4.3.", vm.Ghost.Text);
        await vm.DropAsync();

        // Donnerstag, 4. März 11:00 Uhr in Wien sind 10:00 Uhr UTC; Person und Raum bleiben.
        var post = Assert.Single(api.Calls, c => c.Route.EndsWith("/move", StringComparison.Ordinal));
        Assert.Contains("2027-03-04T10:00", post.Body, StringComparison.Ordinal);
        Assert.Contains(TestData.Anna.Id.ToString(), post.Body, StringComparison.Ordinal);
        Assert.Empty(vm.Columns[0].Items);
        Assert.Equal(2, vm.Columns[3].Items.Count);
        Assert.StartsWith("Verschoben auf Donnerstag, 4. März 2027", vm.Toast, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Alt_Pfeil_rechts_verschiebt_den_markierten_Termin_in_die_Nachbarspalte_zur_selben_Uhrzeit()
    {
        var api = Api();
        var vm = await FilteredAsync(api);
        var item = vm.Columns[0].Items.Single();
        api.On($"POST /api/appointments/{item.Appointment!.Id}/move", item.Appointment with { Start = item.Appointment.Start.AddDays(1), UpdatedAt = TestData.At(9) });
        vm.Mark(item);

        Assert.Contains(vm.ActionsFor(item), a => a.Header == "Verschieben");
        Assert.True(vm.HandleKey(Key.Right, KeyModifiers.Alt));
        for (var i = 0; i < 100 && !api.Calls.Any(c => c.Route.EndsWith("/move", StringComparison.Ordinal)); i++)
        {
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        // Montag 10:00 Uhr in Wien sind 09:00 UTC; am Dienstag dieselbe Uhrzeit.
        var post = Assert.Single(api.Calls, c => c.Route.EndsWith("/move", StringComparison.Ordinal));
        Assert.Contains("2027-03-02T09:00", post.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Termin_in_der_7_Tage_Ansicht_oeffnet_das_Panel()
    {
        var vm = await FilteredAsync(Api());
        var item = vm.Columns[3].Items.Single();

        vm.Open(item);

        Assert.Equal(item.Appointment!.Id, vm.Selected?.Appointment.Id);
        Assert.True(vm.IsFiltered);
        Assert.Contains(vm.Columns[3].Items, i => i.IsSelected);
    }

    [Fact]
    public async Task Kopf_eines_Tages_der_7_Tage_Ansicht_laesst_sich_nicht_noch_einmal_filtern()
    {
        var vm = await FilteredAsync(Api());

        vm.FilterBy(vm.Columns[2]);

        Assert.Equal("Anna", vm.Filter!.Title);
        Assert.False(vm.Columns[2].CanFilter);
    }

    [Fact]
    public async Task Klick_auf_den_Tageskopf_fuehrt_zu_diesem_Tag_ohne_Filter()
    {
        var api = Api();
        var vm = await FilteredAsync(api);
        var column = vm.Columns[2];
        int CalendarLoads() => api.Calls.Count(c => c.Route.Contains("/calendar?date=", StringComparison.Ordinal));
        var before = CalendarLoads();

        Assert.Equal("Klick: zu diesem Tag", column.FilterTip);
        Assert.Equal(TestData.Monday.AddDays(2), column.Date);
        vm.GoToDay(column);
        await WaitAsync(vm, v => !v.IsFiltered && v.Columns.Count == 2);

        Assert.Null(vm.Filter);
        Assert.Equal(TestData.Monday.AddDays(2), vm.Date);
        Assert.Equal(["Anna", "Ben"], vm.Columns.Select(c => c.Title));
        // Datum und Filter ändern sich zusammen: nur der eine Tag wird geladen, nicht zusätzlich die sieben Tage.
        Assert.Equal(1, CalendarLoads() - before);
    }

    [Fact]
    public async Task Spalten_ohne_Datum_springen_nicht()
    {
        var vm = await OpenAsync(Api());

        vm.GoToDay(vm.Columns[0]);

        Assert.Equal(TestData.Monday, vm.Date);
        Assert.False(vm.IsFiltered);
        Assert.Null(vm.Columns[0].Date);
        Assert.True(vm.Columns[0].CanClick);
    }
}
