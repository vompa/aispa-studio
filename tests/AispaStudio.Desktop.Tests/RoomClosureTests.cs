using System.Text.Json;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Controls;
using AispaStudio.Desktop.ViewModels;

namespace AispaStudio.Desktop.Tests;

/// <summary>
/// Raumsperren im Kalender (ADR 0076): die gesperrte Zeit in der Spalte des Raums (schraffiert, Grund im Kopf), die Meldung „n Termine im gesperrten Raum“ mit der
/// Umbuchungshilfe, der Hinweis an Terminen im gesperrten Raum, und die Spaltenbreite der Raumansicht, die der Teamansicht gleicht (Ä65).
/// </summary>
public sealed class RoomClosureTests
{
    private static readonly Guid ClosureId = Guid.Parse("00000000-0000-0000-0000-0000000005e1");

    private static readonly string CalendarRoute = $"GET /api/locations/{TestData.LocationId}/calendar?date=2027-03-01";

    /// <summary>Wartung des Raums Zeder von Montag, 1.3., 13:00 bis Mittwoch, 3.3., Ende.</summary>
    private static RoomClosureBand Maintenance(int open = 0, string? reason = "Lüftung wird gewartet", DateTimeOffset? from = null, bool urgent = true) =>
        new(ClosureId, TestData.Room.Id, TestData.Room.Name, from ?? TestData.At(13), ClosureTests.Midnight.AddDays(3), RoomClosureKind.Maintenance, "Wartung", reason, open, urgent);

    private static StubApi Api(CalendarDay day, LocationResources? resources = null) => new StubApi()
        .On("GET /api/locations", new List<LocationDto> { TestData.Location })
        .On($"GET /api/locations/{TestData.LocationId}/resources", resources ?? TestData.Resources)
        .On(CalendarRoute, day);

    private static Task<CalendarViewModel> OpenAsync(StubApi api, CurrentUser? user = null) => ClosureTests.OpenAsync(api, user);

    private static CalendarDay DayWith(RoomClosureBand band, params AppointmentDto[] appointments) =>
        TestData.Day(appointments) with { RoomClosures = [band], RoomClosuresDue = [band] };

    // ---------- Spaltenbreite (Ä65) ----------

    private static LocationResources ManyRooms(int rooms) => TestData.Resources with
    {
        Rooms = [TestData.Room, .. Enumerable.Range(1, rooms - 1).Select(i => new RoomDto(Guid.NewGuid(), TestData.LocationId, $"Hand- und Fußpflegeplatz {i}", 10))],
    };

    [Theory]
    [InlineData(1300, 6)]
    [InlineData(1300, 2)]
    [InlineData(700, 9)]
    public async Task Die_Raumansicht_hat_dieselbe_Spaltenbreite_wie_die_Teamansicht_egal_wie_viele_Raeume_es_sind(double width, int rooms)
    {
        var vm = await OpenAsync(Api(TestData.Day(), ManyRooms(rooms)));
        vm.SetViewport(width, 800);
        var team = vm.Scale;
        Assert.Equal(2, vm.Columns.Count);

        vm.ByRoom = true;

        Assert.Equal(rooms, vm.Columns.Count);
        Assert.Equal(team.ColumnWidth, vm.Scale.ColumnWidth);
        Assert.Equal(team.ItemWidth, vm.Scale.ItemWidth);
        // Wie bei vielen Personen: reicht die Breite nicht, scrollt der Kalender waagrecht, statt die Spalten zu stauchen.
        Assert.Equal(rooms * (team.ColumnWidth + DayLayout.ColumnGap) - DayLayout.ColumnGap, vm.ColumnsWidth, 0.5);
        Assert.Equal(rooms > 2 && width < rooms * team.ColumnWidth, vm.ColumnsWidth > width);

        vm.ByRoom = false;
        Assert.Equal(team.ColumnWidth, vm.Scale.ColumnWidth);
    }

    [Fact]
    public async Task Bei_vielen_Personen_gilt_in_beiden_Ansichten_die_kleinste_Breite_und_die_Raumansicht_laeuft_ueber_nicht_ueber()
    {
        var people = Enumerable.Range(0, 9).Select(i => new EmployeeDto(Guid.NewGuid(), $"Person {i}", "#2f6f5e", [], [])).ToList();
        var resources = ManyRooms(3) with { Employees = people };
        var day = TestData.Day() with { Shifts = [.. people.Select(p => new EmployeeShift(p.Id, TestData.At(9), TestData.At(17)))] };
        var vm = await OpenAsync(Api(day, resources));
        vm.SetViewport(1000, 800);
        Assert.Equal(160, vm.Scale.ColumnWidth);

        vm.ByRoom = true;

        // Drei Räume, aber die Breite der neun Personenspalten: 160, und der Kalender füllt die Fläche nicht bis zum Rand.
        Assert.Equal(160, vm.Scale.ColumnWidth);
        Assert.Equal(3 * (160 + DayLayout.ColumnGap) - DayLayout.ColumnGap, vm.ColumnsWidth, 0.5);
    }

    [Fact]
    public async Task Der_Kopf_einer_Raumspalte_hat_dieselbe_Form_wie_der_einer_Person()
    {
        var vm = await OpenAsync(Api(TestData.Day(), ManyRooms(3)));
        vm.SetViewport(1300, 800);
        var person = vm.Columns[0];

        vm.ByRoom = true;
        var room = vm.Columns[0];

        // Gleiche zwei Zeilen im Kopf (Name, Kurzinfo) und höchstens fünf Zeilen im Hinweis wie bei einer Person.
        Assert.True(room.IsRoom && !person.IsRoom);
        Assert.False(string.IsNullOrEmpty(room.Title));
        Assert.False(string.IsNullOrEmpty(room.SecondLine));
        Assert.True(room.HeaderTipLines.Count <= 5);
    }

    // ---------- Spalte, Kopf und Fläche ----------

    [Fact]
    public async Task Der_gesperrte_Raum_ist_ab_der_Uhrzeit_schraffiert_und_der_Kopf_nennt_Art_und_Zeitraum()
    {
        var vm = await OpenAsync(Api(DayWith(Maintenance())));

        vm.ByRoom = true;

        var column = Assert.Single(vm.Columns);
        var thirteen = vm.Hours.Single(h => h.Label == "13:00").Top;
        var span = Assert.Single(column.RoomClosures);
        Assert.Equal(thirteen, span.Top, 0.5);
        Assert.Equal(vm.DayHeight - thirteen, span.Height, 0.5);
        Assert.Equal("Wartung · Lüftung wird gewartet", span.Label);
        Assert.True(span.ShowLabel);
        Assert.True(column.IsClosed && column.HasReasonLine);

        // Bis 13:00 ist der Raum buchbar (hell), danach nicht mehr verfügbar (das Raster schraffiert es).
        var open = Assert.Single(column.Available);
        Assert.Equal(0, open.Top, 0.5);
        Assert.Equal(thirteen, open.Height, 0.5);

        Assert.Equal("gesperrt: Wartung ab 13:00", column.Subtitle);
        Assert.Contains(column.HeaderTipLines, l => l.Icon == Icons.Lock && l.Text == "gesperrt: Wartung ab 13:00: Lüftung wird gewartet");
        Assert.True(column.HeaderTipLines.Count <= 5);
    }

    [Theory]
    [InlineData(13, "gesperrt: Wartung ab 13:00")]
    [InlineData(0, "gesperrt: Wartung bis 3.3.")]
    public async Task Der_Kopf_nennt_halbe_Tage_mit_Uhrzeit_und_mehrtaegige_Sperren_mit_dem_letzten_Tag(int hour, string expected)
    {
        var from = hour == 0 ? ClosureTests.Midnight : TestData.At(hour);
        var vm = await OpenAsync(Api(DayWith(Maintenance(from: from))));

        vm.ByRoom = true;

        Assert.Equal(expected, vm.Columns.Single().Subtitle);
    }

    [Fact]
    public async Task Ein_Tag_im_Zeitraum_der_Sperre_ist_ganz_schraffiert_ohne_verfuegbare_Zeit()
    {
        var vm = await OpenAsync(Api(DayWith(Maintenance(from: ClosureTests.Midnight))));

        vm.ByRoom = true;

        var column = vm.Columns.Single();
        Assert.Empty(column.Available);
        Assert.Equal((0d, vm.DayHeight), (column.RoomClosures[0].Top, column.RoomClosures[0].Height));
    }

    [Fact]
    public async Task Ohne_Raumsperre_bleibt_der_Kopf_wie_er_war()
    {
        var vm = await OpenAsync(Api(TestData.Day()));

        vm.ByRoom = true;

        var column = vm.Columns.Single();
        Assert.False(column.IsClosed || column.HasReasonLine || column.HasAffected);
        Assert.Equal("Raumpflege 10 Min. · 2 Behandlungen", column.Subtitle);
        Assert.Contains(column.HeaderTipLines, l => l.Icon == Icons.Broom);
        Assert.DoesNotContain(column.HeaderTipLines, l => l.Icon == Icons.Lock);
    }

    [Fact]
    public async Task Offene_Termine_im_gesperrten_Raum_stehen_im_Kopf_zum_Umbuchen_und_sind_im_Raum_und_im_Team_markiert()
    {
        var inside = TestData.Appointment(14, TestData.Anna);
        var before = TestData.Appointment(10, TestData.Anna);
        var done = TestData.Appointment(16, TestData.Ben, AppointmentStatus.Completed);
        var vm = await OpenAsync(Api(DayWith(Maintenance(open: 1), inside, before, done)));

        // Teamansicht: der Termin im gesperrten Raum trägt den Hinweis, der davor nicht.
        var items = vm.Columns.SelectMany(c => c.Items).ToDictionary(i => i.Id);
        Assert.True(items[inside.Id].IsAffected);
        Assert.False(items[before.Id].IsAffected);
        Assert.False(items[done.Id].IsAffected);
        Assert.Contains("Raum Zeder ist gesperrt (Wartung · Lüftung wird gewartet): bitte umbuchen", items[inside.Id].Hover!.Alerts);
        Assert.DoesNotContain(items[before.Id].Hover!.Alerts, a => a.Contains("gesperrt", StringComparison.Ordinal));

        vm.ByRoom = true;

        var column = vm.Columns.Single();
        Assert.True(column.HasAffected);
        Assert.Equal("1 Termin umbuchen", column.AffectedText);
        Assert.Equal(ClosureId, column.RoomClosureId);
        Assert.True(column.Items.Single(i => i.Id == inside.Id).IsAffected);
    }

    [Fact]
    public async Task Klick_in_den_gesperrten_Teil_eines_Raums_ist_belegt_und_ohne_freie_Luecke()
    {
        var day = DayWith(Maintenance());
        var vm = await OpenAsync(Api(day));

        Assert.True(DayLayout.IsOccupied(day, true, TestData.Room.Id, TestData.At(13), TestData.At(14)));
        Assert.False(DayLayout.IsOccupied(day, true, TestData.Room.Id, TestData.At(10), TestData.At(11)));
        Assert.Null(DayLayout.GapAt(day, true, TestData.Room.Id, TestData.At(14)));
        // Davor reicht die freie Lücke bis zum Beginn der Sperre.
        var gap = DayLayout.GapAt(day, true, TestData.Room.Id, TestData.At(10));
        Assert.Equal(TestData.At(13), gap!.Until);
        Assert.NotNull(vm);
    }

    // ---------- Meldung ----------

    [Fact]
    public async Task Die_Meldung_zaehlt_die_Termine_im_gesperrten_Raum_und_ist_dringend_ein_Fehler_sonst_eine_Warnung()
    {
        var urgent = await OpenAsync(Api(DayWith(Maintenance(open: 2), TestData.Appointment(14, TestData.Anna))));
        var later = await OpenAsync(Api(DayWith(Maintenance(open: 2, urgent: false), TestData.Appointment(14, TestData.Anna))));

        var message = Assert.Single(urgent.Messages, m => m.Kind == MessageKind.RoomClosure);
        Assert.Equal("Raum Zeder ist gesperrt (Wartung, Mo. 1.3. 13:00 Uhr bis Mi. 3.3.): 2 Termine umbuchen.", message.Text);
        Assert.Equal(("2 Termine im gesperrten Raum", MessageSeverity.Error, "Raumsperre"), (message.ShortText, message.Severity, message.KindText));
        Assert.Contains("Grund: Lüftung wird gewartet", message.Lines);
        Assert.Equal("Umbuchen …", message.ActionLabel);
        Assert.Equal(MessageSeverity.Warning, Assert.Single(later.Messages, m => m.Kind == MessageKind.RoomClosure).Severity);
        Assert.Equal(TestData.Room.Id, Assert.Single(message.Subjects).Id);
    }

    [Fact]
    public async Task Ohne_offene_Termine_gibt_es_keine_Meldung_und_die_Meldung_steht_auch_an_einem_Tag_ohne_Sperre_wenn_sie_kommt()
    {
        var none = await OpenAsync(Api(DayWith(Maintenance(open: 0))));
        Assert.DoesNotContain(none.Messages, m => m.Kind == MessageKind.RoomClosure);

        // Der Tag selbst liegt nicht in der Sperre, sie kommt aber (RoomClosuresDue): die Meldung steht trotzdem da.
        var later = Maintenance(open: 1, urgent: false);
        var vm = await OpenAsync(Api(TestData.Day() with { RoomClosuresDue = [later] }));
        Assert.Single(vm.Messages, m => m.Kind == MessageKind.RoomClosure);
    }

    [Fact]
    public async Task Die_Meldung_schlaegt_ohne_Recht_zu_buchen_keine_Umbuchung_vor()
    {
        var vm = await OpenAsync(Api(DayWith(Maintenance(open: 1))), new CurrentUser("u9", "x@aispa.test", "Viewer", 1,
            new CurrentAccess(Guid.NewGuid(), "Test-Spa", StaffRole.Reception, true, []), null, [AccessRight.ViewCalendar]));

        var message = Assert.Single(vm.Messages, m => m.Kind == MessageKind.RoomClosure);

        Assert.Null(message.ActionLabel);
    }

    [Fact]
    public async Task Die_Meldung_hat_ihr_Hilfethema()
    {
        var vm = await OpenAsync(Api(DayWith(Maintenance(open: 1))));

        var message = Assert.Single(vm.Messages, m => m.Kind == MessageKind.RoomClosure);

        Assert.Equal(("raumsperren-termine-umbuchen", "umbuchen"), message.Help);
    }

    // ---------- Umbuchungshilfe ----------

    [Fact]
    public async Task Die_Umbuchungshilfe_holt_die_Vorschlaege_zur_Sperre_und_uebernimmt_die_gewaehlten_ueber_den_Weg_des_Ausfall_Assistenten()
    {
        var other = new RoomDto(Guid.NewGuid(), TestData.LocationId, "Raum Lotus", 10);
        var inside = TestData.Appointment(14, TestData.Anna);
        var fixedInside = inside with { RoomId = TestData.Room.Id };
        var proposal = new AbsenceProposal(Guid.Empty, TestData.Monday,
        [
            new AbsenceProposalItem(fixedInside, AbsenceOption.SameTime, fixedInside.Start, TestData.Anna.Id, "Anna", other.Id, other.Name, "Raum Lotus ist um 14:00 frei; Anna bleibt."),
            new AbsenceProposalItem(TestData.Appointment(16, TestData.Ben), AbsenceOption.None, null, null, null, null, null, "An diesem Tag ist kein anderer Raum frei."),
        ]);
        var api = Api(DayWith(Maintenance(open: 2), fixedInside)).On($"GET /api/locations/{TestData.LocationId}/room-closures/{ClosureId}/rebooking", proposal)
            .On($"POST /api/locations/{TestData.LocationId}/absence/apply", new AbsenceApplyResult([fixedInside with { RoomId = other.Id }], [], 0));
        var vm = await OpenAsync(api);

        Assert.Single(vm.Messages, m => m.Kind == MessageKind.RoomClosure).ActionCommand!.Execute(null);
        await ClosureTests.Until(() => vm.Absence is { Rows.Count: 2 });

        var assistant = vm.Absence!;
        Assert.True(assistant.IsRoomClosure);
        Assert.Equal(("Umbuchung aus Raum Zeder", "Umbuchung", "Keine offenen Termine im gesperrten Raum."), (assistant.Title, assistant.PanelTabText, assistant.EmptyText));
        Assert.Equal("Mo. 1.3. 14:00–14:50 · Massage", assistant.Rows[0].Before);
        Assert.Equal("gleiche Zeit bei Anna, Raum Lotus", assistant.Rows[0].After);
        Assert.Equal("kein anderer Raum an diesem Tag", assistant.Rows[1].After);
        Assert.True(assistant.Rows[0].IsChosen && !assistant.Rows[1].CanApply);

        await assistant.ApplyAsync();

        var apply = api.Calls.Single(c => c.Route.StartsWith("POST", StringComparison.Ordinal) && c.Route.EndsWith("/absence/apply", StringComparison.Ordinal));
        using var body = JsonDocument.Parse(apply.Body!);
        var move = Assert.Single(body.RootElement.GetProperty("moves").EnumerateArray());
        Assert.Equal((fixedInside.Id.ToString(), other.Id.ToString()), (move.GetProperty("appointmentId").GetString(), move.GetProperty("roomId").GetString()));
        Assert.StartsWith("1 Termin umgebucht", assistant.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Der_Kopf_und_das_Menue_des_gesperrten_Raums_fuehren_zur_Umbuchungshilfe_und_zum_Sperren()
    {
        var inside = TestData.Appointment(14, TestData.Anna);
        var proposal = new AbsenceProposal(Guid.Empty, TestData.Monday, []);
        var api = Api(DayWith(Maintenance(open: 1), inside)).On($"GET /api/locations/{TestData.LocationId}/room-closures/{ClosureId}/rebooking", proposal);
        var vm = await OpenAsync(api);
        var requested = new List<(string Page, string? Tab, Guid? Id)>();
        vm.AdminRequested = (page, tab, id) => requested.Add((page, tab, id));
        vm.ByRoom = true;
        var column = vm.Columns.Single();

        var actions = vm.ActionsForColumn(column);

        Assert.Equal(["Termine in andere Räume umbuchen", "Raum sperren …"], actions.Select(a => a.Header));
        actions[1].Run!();
        Assert.Equal([("masterData", "roomClosures", (Guid?)TestData.Room.Id)], requested);
        vm.OpenAbsence(column);
        Assert.True(vm.Absence!.IsRoomClosure);
        await ClosureTests.Until(() => api.Calls.Any(c => c.Route.EndsWith("/rebooking", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task Die_Rezeption_sperrt_keine_Raeume_und_ohne_Sperre_gibt_es_nur_das_Sperren()
    {
        var vm = await OpenAsync(Api(TestData.Day()), TestData.Reception());
        vm.AdminRequested = (_, _, _) => { };
        vm.ByRoom = true;
        Assert.Empty(vm.ActionsForColumn(vm.Columns.Single()));

        var manager = await OpenAsync(Api(TestData.Day()));
        manager.AdminRequested = (_, _, _) => { };
        manager.ByRoom = true;
        Assert.Equal(["Raum sperren …"], manager.ActionsForColumn(manager.Columns.Single()).Select(a => a.Header));
    }
}
