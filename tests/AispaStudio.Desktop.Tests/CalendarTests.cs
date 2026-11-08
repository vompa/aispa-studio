using System.Net;
using System.Text.Json;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Controls;
using AispaStudio.Desktop.Services;
using AispaStudio.Desktop.ViewModels;
using Avalonia.Input;

namespace AispaStudio.Desktop.Tests;

public sealed class CalendarTests
{
    private static readonly TimeZoneInfo Vienna = TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna");

    private sealed class FixedTime : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => TestData.At(10);

        public override TimeZoneInfo LocalTimeZone => Vienna;
    }

    /// <summary>Uhr mit Zeitgebern, die erst beim Weiterstellen auslösen; so läuft die Frist der Rückmeldung ohne Warten.</summary>
    private sealed class TimerTime : TimeProvider
    {
        private readonly List<FakeTimer> _timers = [];
        private DateTimeOffset _now = TestData.At(10);

        public override DateTimeOffset GetUtcNow() => _now;

        public override TimeZoneInfo LocalTimeZone => Vienna;

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            var timer = new FakeTimer(callback, state, _now + dueTime);
            _timers.Add(timer);
            return timer;
        }

        public void Advance(TimeSpan by)
        {
            _now += by;
            foreach (var timer in _timers.Where(t => !t.Fired && t.Due <= _now).ToList())
            {
                timer.Fire();
            }
        }

        private sealed class FakeTimer(TimerCallback callback, object? state, DateTimeOffset due) : ITimer
        {
            public DateTimeOffset Due { get; } = due;

            public bool Fired { get; private set; }

            public void Fire()
            {
                Fired = true;
                callback(state);
            }

            public bool Change(TimeSpan dueTime, TimeSpan period) => true;

            public void Dispose()
            {
            }

            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }

    private static StubApi Api(params AppointmentDto[] appointments) => new StubApi()
        .On("GET /api/locations", new List<LocationDto> { TestData.Location })
        .On($"GET /api/locations/{TestData.LocationId}/resources", TestData.Resources)
        .On($"GET /api/locations/{TestData.LocationId}/calendar?date=2027-03-01", TestData.Day(appointments));

    private static async Task<CalendarViewModel> OpenAsync(StubApi api, ILiveUpdates? live = null, CurrentUser? user = null, TimeProvider? time = null)
    {
        var vm = new CalendarViewModel(api.Client(), user ?? TestData.Reception(), live, time ?? new FixedTime());
        await vm.InitializeAsync();
        await vm.LoadAsync();
        return vm;
    }

    [Fact]
    public void Termine_stehen_in_der_Spalte_ihrer_Person_auf_Hoehe_ihrer_Uhrzeit()
    {
        var appointment = TestData.Appointment(10, TestData.Anna);

        var team = DayLayout.Build(TestData.Day(appointment), TestData.Resources, byRoom: false, Vienna);
        var rooms = DayLayout.Build(TestData.Day(appointment), TestData.Resources, byRoom: true, Vienna);

        Assert.Equal(["Anna", "Ben"], team.Columns.Select(c => c.Title));
        var item = Assert.Single(team.Columns[0].Items);
        Assert.Equal(60 * DayLayout.PixelsPerMinute, item.Top);
        Assert.Equal(50 * DayLayout.PixelsPerMinute, item.Height);
        Assert.Equal("10:00–10:50", item.TimeText);
        Assert.Equal("#2f6f5e", item.Color);
        Assert.Equal(new CalendarSpan(180 * DayLayout.PixelsPerMinute, 480 * DayLayout.PixelsPerMinute), team.Columns[1].Available[0]);
        Assert.Equal("09:00", team.Hours[0].Label);
        Assert.Equal(["Raum Zeder"], rooms.Columns.Select(c => c.Title));
        Assert.Single(rooms.Columns[0].Items);
    }

    [Fact]
    public async Task Spalten_fuellen_die_Breite_und_der_Tag_passt_ohne_Zoom_in_die_Hoehe()
    {
        var vm = await OpenAsync(Api(TestData.Appointment(10, TestData.Anna)));
        var minutes = DayLayout.VisibleMinutes(TestData.Day(), Vienna);

        vm.SetViewport(1000, minutes * 1.2);

        Assert.True(vm.IsFitted);
        Assert.Equal(1.2, vm.Scale.PixelsPerMinute, 3);
        Assert.Equal(Math.Floor((1000 - DayLayout.ColumnGap) / 2), vm.Scale.ColumnWidth);
        Assert.Equal(minutes * 1.2, vm.DayHeight, 3);
        Assert.Equal(60 * 1.2, vm.Columns[0].Items[0].Top, 3);

        // Viele Spalten in einem schmalen Fenster: nie schmaler als 160 Pixel, dann wird waagrecht gescrollt.
        vm.SetViewport(200, minutes * 1.2);
        Assert.Equal(160, vm.Scale.ColumnWidth);
    }

    [Fact]
    public async Task Zoom_wird_je_Arbeitsplatz_gemerkt_und_Tag_einpassen_hebt_ihn_auf()
    {
        var settings = new MemorySettingsStore();
        var vm = new CalendarViewModel(Api().Client(), TestData.Reception(), null, new FixedTime(), settings);
        await vm.InitializeAsync();
        await vm.LoadAsync();
        vm.SetViewport(800, 600);
        var fitted = vm.Scale.PixelsPerMinute;

        vm.ZoomInCommand.Execute(null);

        Assert.False(vm.IsFitted);
        Assert.True(vm.Scale.PixelsPerMinute > fitted);
        Assert.Equal(vm.Scale.PixelsPerMinute, settings.Load().CalendarZoom);
        var again = new CalendarViewModel(Api().Client(), TestData.Reception(), null, new FixedTime(), settings);
        Assert.Equal(vm.Zoom, again.Zoom);

        vm.ZoomFitCommand.Execute(null);
        Assert.True(vm.IsFitted);
        Assert.Null(settings.Load().CalendarZoom);
        Assert.Equal(fitted, vm.Scale.PixelsPerMinute);
    }

    [Fact]
    public void Kennzeichen_Ueberfaellig_und_Hover_kommen_aus_den_Gastdaten()
    {
        var regular = TestData.Appointment(10, TestData.Anna);
        var newcomer = TestData.Appointment(13, TestData.Anna) with { CustomerId = Guid.NewGuid(), CustomerName = "Nora Neu" };
        var day = TestData.Day(regular, newcomer) with
        {
            Guests =
            [
                new CalendarGuest(regular.CustomerId, 7, 4, 2, TestData.Monday.AddDays(-30), "Nur Therapeutin"),
                new CalendarGuest(newcomer.CustomerId, 0, 0, 0, null, null),
            ],
        };

        var layout = DayLayout.Build(day, TestData.Resources, byRoom: false, Vienna, now: TestData.At(10, 12));
        var first = layout.Columns[0].Items.Single(i => i.Id == regular.Id);
        var second = layout.Columns[0].Items.Single(i => i.Id == newcomer.Id);

        Assert.Equal(["!", "★"], first.Flags.Select(f => f.Symbol));
        Assert.Equal("Hinweis: Nur Therapeutin · 2× nicht erschienen", first.Flags[0].Tip);
        Assert.True(first.IsOverdue);
        Assert.Equal("überfällig 12 Min. · 10:00–10:50", first.MetaText);
        Assert.DoesNotContain("Besuche:", first.HoverText, StringComparison.Ordinal);
        Assert.Contains("10:00–10:50 · 50 Min. · Anna · Raum Zeder", first.HoverText, StringComparison.Ordinal);
        Assert.Equal(["N"], second.Flags.Select(f => f.Symbol));
        Assert.False(second.IsOverdue);
    }

    [Fact]
    public void Paar_und_Spa_Tag_tragen_ein_Etikett_und_leuchten_gemeinsam()
    {
        var couple = Guid.NewGuid();
        var spaDay = Guid.NewGuid();
        var her = TestData.Appointment(10, TestData.Anna) with { GroupId = couple };
        var him = TestData.Appointment(10, TestData.Ben) with { GroupId = couple, CustomerId = Guid.NewGuid(), CustomerName = "Stefan Koller" };
        var morning = TestData.Appointment(14, TestData.Anna) with { GroupId = spaDay, CustomerId = Guid.NewGuid(), CustomerName = "Lisa Berg" };
        var afternoon = morning with { Id = Guid.NewGuid(), Start = TestData.At(16), End = TestData.At(16, 50) };

        var layout = DayLayout.Build(TestData.Day(her, him, morning, afternoon), TestData.Resources, byRoom: false, Vienna, selected: her.Id);
        var items = layout.Columns.SelectMany(c => c.Items).ToDictionary(i => i.Id);

        Assert.Equal("Paar mit Stefan Koller", items[her.Id].GroupLabel);
        Assert.Equal("Paar mit Eva Muster", items[him.Id].GroupLabel);
        Assert.Equal("Spa-Tag 1/2", items[morning.Id].GroupLabel);
        Assert.Equal("Spa-Tag 2/2", items[afternoon.Id].GroupLabel);
        Assert.True(items[him.Id].IsGroupHighlighted);
        Assert.False(items[morning.Id].IsGroupHighlighted);
        Assert.Equal(items[her.Id].GroupColor, items[him.Id].GroupColor);
    }

    [Fact]
    public void Raumansicht_zeigt_wer_behandelt_und_die_Plaetze()
    {
        var resources = TestData.Resources with { Rooms = [TestData.Room with { Places = 2 }] };

        var layout = DayLayout.Build(TestData.Day(TestData.Appointment(10, TestData.Anna)), resources, byRoom: true, Vienna);

        var column = layout.Columns[0];
        Assert.True(column.IsCouple);
        Assert.Equal("Raumpflege 10 Min. · 2 Behandlungen", column.Subtitle);
        var item = column.Items[0];
        Assert.Equal("AN", item.WhoInitials);
        Assert.Equal(TestData.Anna.Color, item.WhoColor);
        // Wer behandelt, zeigt der Kreis vor dem Namen; die dritte Zeile bleibt für Zeit, Gruppe und Quelle frei.
        Assert.Equal("10:00–10:50", item.MetaText);
    }

    [Fact]
    public void Raumkopf_hat_zwei_Zeilen_ohne_Etikett_und_der_Paarraum_das_Paar_Symbol()
    {
        var cabin = new RoomDto(Guid.NewGuid(), TestData.LocationId, "Hand- und Fußpflege-Kabine", 0);
        var couple = TestData.Room with { Places = 2 };
        var resources = TestData.Resources with { Rooms = [couple, cabin] };

        var layout = DayLayout.Build(TestData.Day(), resources, byRoom: true, Vienna);

        // Zeile 1 der Name, Zeile 2 Raumpflege und Behandlungen in einer Zeile; kein Etikett „Paarraum · 2 Plätze“ im Kopf.
        var paar = layout.Columns[0];
        Assert.Equal("Raumpflege 10 Min. · 2 Behandlungen", paar.Subtitle);
        Assert.Equal(paar.Subtitle, paar.SecondLine);
        Assert.DoesNotContain('\n', paar.SecondLine);
        Assert.Equal([true, false], layout.Columns.Select(c => c.IsCouple));
        Assert.Equal([Icons.Users, Icons.Door], layout.Columns.Select(c => c.TipLines[0].Icon));
        Assert.Equal("", layout.Columns[1].SecondLine);
    }

    [Fact]
    public void Der_Hinweis_am_Raumkopf_hat_hoechstens_fuenf_Zeilen_und_keine_Behandlungsliste()
    {
        var cabin = new RoomDto(Guid.NewGuid(), TestData.LocationId, "Hand- und Fußpflege-Kabine", 0);
        var couple = TestData.Room with { Places = 2 };
        var resources = TestData.Resources with { Rooms = [couple, cabin] };
        var layout = DayLayout.Build(TestData.Day(TestData.Appointment(10, TestData.Anna)), resources, byRoom: true, Vienna);
        var paar = layout.Columns[0];

        Assert.Equal(
            ["Paarraum · 2 Plätze", "Raumpflege 10 Min. nach jedem Termin", "1 Termin heute", $"Klick: alle Termine von Raum Zeder der nächsten {CalendarFilter.Days} Tage"],
            paar.HeaderTipLines.Select(l => l.Text));
        Assert.Equal([Icons.Users, Icons.Broom, Icons.Calendar, Icons.Cursor], paar.HeaderTipLines.Select(l => l.Icon));
        Assert.DoesNotContain(paar.HeaderTipLines, l => l.Text.Contains("Gesicht", StringComparison.Ordinal) || l.Text.Contains("Behandlungen:", StringComparison.Ordinal));

        var single = layout.Columns[1];
        Assert.Equal(["Einzelraum", "Keine Raumpflege nach dem Termin", "Keine Termine heute"], single.HeaderTipLines.Take(3).Select(l => l.Text));
        Assert.Equal(Icons.Door, single.HeaderTipLines[0].Icon);

        // Mit Warnung kommt eine Zeile dazu, mehr als fünf werden es nie.
        paar.Alert = new ColumnAlert(MessageSeverity.Warning, "Raumpflege überfällig");
        Assert.Equal(5, paar.HeaderTipLines.Count);
        Assert.Equal((Icons.Warning, "Raumpflege überfällig"), (paar.HeaderTipLines[3].Icon, paar.HeaderTipLines[3].Text));
        Assert.Equal(Icons.Cursor, paar.HeaderTipLines[^1].Icon);
    }

    [Fact]
    public void Klick_in_die_Spalte_ergibt_die_Viertelstunde()
    {
        var dayStart = TestData.At(9);

        Assert.Equal(TestData.At(9, 30), DayLayout.TimeAt(dayStart, 40 * DayLayout.PixelsPerMinute));
        Assert.Equal(TestData.At(9), DayLayout.TimeAt(dayStart, -5));
    }

    [Fact]
    public async Task Gegeben_Lena_meldet_begonnen_Dann_zeigt_der_Kalender_den_Termin_per_Live_als_laufend()
    {
        var appointment = TestData.Appointment(10, TestData.Anna);
        var live = new FakeLive();
        var vm = await OpenAsync(Api(appointment), live);

        live.Send(appointment with { Status = AppointmentStatus.CheckedIn, StartedAt = TestData.At(10), UpdatedAt = TestData.At(10) });

        var item = vm.Columns[0].Items[0];
        Assert.Equal("Läuft", item.StatusText);
        Assert.EndsWith("Läuft", item.AccessibleName, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Live_Updates_ersetzen_aeltere_Staende_und_verschieben_aus_dem_Tag()
    {
        var appointment = TestData.Appointment(10, TestData.Anna);
        var live = new FakeLive();
        var vm = await OpenAsync(Api(appointment), live);

        live.Send(appointment with { Status = AppointmentStatus.CheckedIn, UpdatedAt = TestData.At(9) });
        var checkedIn = vm.Columns[0].Items[0].StatusText;
        live.Send(appointment with { Status = AppointmentStatus.Completed, UpdatedAt = TestData.At(8, 30) });
        var stillCheckedIn = vm.Columns[0].Items[0].StatusText;
        live.Send(appointment with { Start = TestData.At(10).AddDays(1), End = TestData.At(10, 50).AddDays(1), UpdatedAt = TestData.At(9, 30) });

        Assert.Equal(TestData.LocationId, live.Watched);
        Assert.Equal("Eingecheckt", checkedIn);
        Assert.Equal("Eingecheckt", stillCheckedIn);
        Assert.Empty(vm.Columns[0].Items);
    }

    [Fact]
    public async Task Buchen_von_freier_Stelle_waehlt_passende_Behandlung_und_zeigt_Ablehnung()
    {
        var api = Api();
        var vm = await OpenAsync(api);
        var anna = vm.Columns[0];
        var rejected = new BookingRejected("Zu dieser Zeit ist nicht alles frei.", [new BookingConflict(ResourceKind.Room, TestData.Room.Id, "Raum Zeder", "belegt")]);
        api.On("POST /api/appointments", rejected, HttpStatusCode.Conflict)
            .On("GET /api/customers?search=eva", new List<CustomerDto> { new(Guid.NewGuid(), "Eva", "Muster", null, null, null, null) });

        vm.StartBooking(anna, 30 * DayLayout.PixelsPerMinute);
        var booking = vm.Booking!;
        await booking.SearchAsync("eva");
        booking.Customer = booking.Customers[0];
        await booking.BookCommand.ExecuteAsync(null);

        Assert.Equal("Massage", booking.Treatment?.Name);
        Assert.Equal("09:30", booking.StartText);
        Assert.Equal("Zu dieser Zeit ist nicht alles frei. Raum Zeder: belegt.", booking.Error);
        var request = JsonSerializer.Deserialize<AppointmentRequest>(api.Calls.Last().Body!, ApiClient.Json)!;
        Assert.Equal(TestData.At(9, 30), request.Start);
        Assert.Equal(TestData.Anna.Id, request.EmployeeId);
        Assert.Null(request.RoomId);
    }

    [Fact]
    public async Task Buchen_zeigt_Anamnese_pruefen_nur_bei_markierter_Behandlung()
    {
        var hotStone = TestData.MassageTreatment with { CheckAnamnesis = true };
        var eva = new CustomerDto(Guid.NewGuid(), "Eva", "Muster", null, null, null, null);
        var api = Api()
            .On($"GET /api/locations/{TestData.LocationId}/resources", TestData.Resources with { Treatments = [TestData.Facial, hotStone] })
            .On("GET /api/customers?search=eva", new List<CustomerDto> { eva })
            .On($"GET /api/customers/{eva.Id}/anamnesis/check?treatmentId={hotStone.Id}", new AnamnesisCheck(true));
        var vm = await OpenAsync(api);

        vm.StartBooking(vm.Columns[0], 30 * DayLayout.PixelsPerMinute);
        var booking = vm.Booking!;
        await booking.SearchAsync("eva");
        booking.Customer = booking.Customers[0];
        await booking.LoadAnamnesisCheckAsync();
        var shown = booking.CheckAnamnesis;
        booking.Treatment = TestData.Facial;
        await booking.LoadAnamnesisCheckAsync();

        Assert.True(shown);
        Assert.False(booking.CheckAnamnesis);
        Assert.DoesNotContain(api.Calls, c => c.Route.Contains($"treatmentId={TestData.Facial.Id}", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Gebuchter_Termin_erscheint_sofort()
    {
        var api = Api();
        var vm = await OpenAsync(api);
        var booked = TestData.Appointment(11, TestData.Anna);
        api.On("POST /api/appointments", booked, HttpStatusCode.Created)
            .On("GET /api/customers?search=eva", new List<CustomerDto> { new(booked.CustomerId, "Eva", "Muster", null, null, null, null) });

        vm.StartBooking(vm.Columns[0], 120 * DayLayout.PixelsPerMinute);
        await vm.Booking!.SearchAsync("eva");
        vm.Booking.Customer = vm.Booking.Customers[0];
        await vm.Booking.BookCommand.ExecuteAsync(null);

        Assert.Null(vm.Booking);
        Assert.Equal(booked.Id, Assert.Single(vm.Columns[0].Items).Id);
    }

    [Fact]
    public async Task Therapeutin_setzt_nur_den_Status_eigener_Termine()
    {
        var own = TestData.Appointment(10, TestData.Anna);
        var other = TestData.Appointment(13, TestData.Ben);
        var therapist = TestData.Reception(TestData.Anna.Id) with
        {
            Access = new CurrentAccess(Guid.NewGuid(), "Test-Spa", StaffRole.Therapist, true, []),
            Rights = [AccessRight.ViewCalendar, AccessRight.ChangeStatus],
        };
        var vm = await OpenAsync(Api(own, other), user: therapist);

        vm.Open(vm.Columns[0].Items[0]);
        var ownPanel = vm.Selected!;
        vm.Open(vm.Columns[1].Items[0]);
        var otherPanel = vm.Selected!;
        vm.StartBooking(vm.Columns[0], 0);

        Assert.True(ownPanel.CanChangeStatus);
        Assert.False(ownPanel.CanCancel);
        Assert.False(otherPanel.CanChangeStatus);
        Assert.Null(vm.Booking);
    }
    [Fact]
    public async Task Ziehen_verschiebt_sofort_zu_anderer_Person_und_Rueckgaengig_stellt_zurueck()
    {
        var appointment = TestData.Appointment(10, TestData.Anna);
        var api = Api(appointment);
        var vm = await OpenAsync(api);
        var moved = appointment with { Start = TestData.At(14, 30), End = TestData.At(15, 20), EmployeeId = TestData.Ben.Id, UpdatedAt = TestData.At(9) };
        api.On($"POST /api/appointments/{appointment.Id}/move", moved);
        var item = vm.Columns[0].Items[0];

        vm.BeginDrag(item, grabOffset: 10);
        var lifted = vm.Columns[0].Items[0].IsLifted;
        // Spalte Ben, Oberkante 4,5 Stunden nach 10:00 plus zwei Minuten: rundet auf 14:30.
        vm.DragTo(DayLayout.ColumnLeft(1) + 20, item.Top + 10 + (270 + 2) * DayLayout.PixelsPerMinute);
        var ghost = vm.Ghost;
        await vm.DropAsync();

        Assert.True(lifted);
        Assert.Equal("14:30–15:20 · Ben", ghost?.Text);
        var request = JsonSerializer.Deserialize<MoveAppointmentRequest>(api.Calls.Last().Body!, ApiClient.Json)!;
        Assert.Equal(TestData.At(14, 30), request.Start);
        Assert.Equal(TestData.Ben.Id, request.EmployeeId);
        Assert.Null(request.RoomId);
        Assert.Empty(vm.Columns[0].Items);
        Assert.Equal(appointment.Id, Assert.Single(vm.Columns[1].Items).Id);
        Assert.Null(vm.Ghost);
        Assert.True(vm.CanUndo);
        Assert.Contains("14:30–15:20 (Ben, Raum Zeder)", vm.Toast, StringComparison.Ordinal);

        api.On($"POST /api/appointments/{appointment.Id}/move", appointment with { UpdatedAt = TestData.At(9, 30) });
        await vm.UndoMoveCommand.ExecuteAsync(null);

        var undo = JsonSerializer.Deserialize<MoveAppointmentRequest>(api.Calls.Last().Body!, ApiClient.Json)!;
        Assert.Equal(TestData.At(10), undo.Start);
        Assert.Equal(TestData.Anna.Id, undo.EmployeeId);
        Assert.Single(vm.Columns[0].Items);
        Assert.False(vm.CanUndo);
    }

    [Fact]
    public async Task Buchungsvorschau_nur_ueber_freier_Zeit_nicht_ueber_Termin_und_Nachbereitung()
    {
        // 10:00–10:50 bei Anna, danach 10 Minuten Nachbereitung bis 11:00.
        var appointment = TestData.Appointment(10, TestData.Anna);
        appointment = appointment with { BlockedUntil = appointment.End.AddMinutes(10) };
        var vm = await OpenAsync(Api(appointment));
        var tenOClock = vm.Columns[0].Items[0].Top;
        double Y(int hour, int minute) => tenOClock + (((hour - 10) * 60) + minute) * vm.Scale.PixelsPerMinute + 1;

        vm.Hover(20, Y(10, 20));
        var overAppointment = vm.Ghost;
        vm.Hover(20, Y(10, 50));
        var overAftercare = vm.Ghost;
        vm.Hover(DayLayout.ColumnLeft(1) + 20, Y(10, 20));
        var otherColumn = vm.Ghost;
        vm.Hover(20, Y(11, 0));
        var afterwards = vm.Ghost;

        Assert.Null(overAppointment);
        Assert.Null(overAftercare);
        Assert.Equal("10:15", otherColumn?.Text);
        Assert.Equal("11:00", afterwards?.Text);
    }

    [Theory]
    [InlineData(32, "10:30–11:20 · Anna")]
    [InlineData(33, "10:35–11:25 · Anna")]
    [InlineData(37, "10:35–11:25 · Anna")]
    [InlineData(38, "10:40–11:30 · Anna")]
    public async Task Ziehen_rastet_auf_fuenf_Minuten(int minutesAfterTen, string expected)
    {
        var vm = await OpenAsync(Api(TestData.Appointment(10, TestData.Anna)));
        var item = vm.Columns[0].Items[0];

        // Der Termin beginnt um 10:00; gegriffen 10 Pixel unter dem oberen Rand.
        vm.BeginDrag(item, grabOffset: 10);
        vm.DragTo(10, item.Top + 10 + minutesAfterTen * DayLayout.PixelsPerMinute);

        Assert.Equal(expected, vm.Ghost?.Text);
    }

    [Fact]
    public async Task Esc_bricht_das_Ziehen_ab_und_laesst_Panel_und_Termin_in_Ruhe()
    {
        var appointment = TestData.Appointment(10, TestData.Anna);
        var api = Api(appointment);
        var vm = await OpenAsync(api);
        var item = vm.Columns[0].Items[0];
        vm.Open(item);
        var callsBefore = api.Calls.Count;

        vm.BeginDrag(vm.Columns[0].Items[0], grabOffset: 10);
        vm.DragTo(DayLayout.ColumnLeft(1) + 20, item.Top + 200);
        Assert.True(vm.IsDragging);
        var handled = vm.HandleKey(Key.Escape, KeyModifiers.None);
        await vm.DropAsync();

        Assert.True(handled);
        Assert.False(vm.IsDragging);
        Assert.Null(vm.Ghost);
        Assert.NotNull(vm.Selected);
        Assert.Equal(callsBefore, api.Calls.Count);
        Assert.Equal(TestData.At(10), Assert.Single(vm.Columns[0].Items).Appointment!.Start);
    }

    private static async Task<(CalendarViewModel Vm, TimerTime Time, AppointmentDto Appointment)> MovedAsync()
    {
        var appointment = TestData.Appointment(10, TestData.Anna);
        var api = Api(appointment);
        api.On($"POST /api/appointments/{appointment.Id}/move", appointment with { Start = TestData.At(14, 30), End = TestData.At(15, 20), UpdatedAt = TestData.At(9) });
        var time = new TimerTime();
        var vm = await OpenAsync(api, time: time);
        vm.BeginDrag(vm.Columns[0].Items[0], grabOffset: 0);
        vm.DragTo(10, vm.Columns[0].Items[0].Top + 270 * DayLayout.PixelsPerMinute);
        await vm.DropAsync();
        Assert.NotNull(vm.Toast);
        return (vm, time, appointment);
    }

    /// <summary>Die Frist läuft über Task.Delay; nach dem Weiterstellen kurz warten, bis die Fortsetzung durch ist (oder bis klar ist, dass nichts passiert).</summary>
    private static async Task SettleAsync(CalendarViewModel vm)
    {
        for (var i = 0; i < 20 && vm.Toast is not null; i++)
        {
            await Task.Delay(10);
        }
    }

    [Fact]
    public async Task Erfolgsmeldung_verschwindet_nach_acht_Sekunden()
    {
        var (vm, time, _) = await MovedAsync();

        time.Advance(TimeSpan.FromSeconds(7.9));
        await SettleAsync(vm);
        Assert.NotNull(vm.Toast);
        Assert.True(vm.CanUndo);

        time.Advance(TimeSpan.FromSeconds(0.2));
        await SettleAsync(vm);
        Assert.Null(vm.Toast);
        Assert.False(vm.CanUndo);
    }

    [Fact]
    public async Task Rueckgaengig_innerhalb_der_Frist_funktioniert_und_die_neue_Meldung_hat_eine_eigene_Frist()
    {
        var (vm, time, appointment) = await MovedAsync();
        time.Advance(TimeSpan.FromSeconds(6));
        await vm.UndoMoveCommand.ExecuteAsync(null);
        Assert.StartsWith("Zurück auf", vm.Toast, StringComparison.Ordinal);

        // Die alte Frist (2 s Rest) darf die neue Meldung nicht vorzeitig schließen.
        time.Advance(TimeSpan.FromSeconds(3));
        await SettleAsync(vm);
        Assert.NotNull(vm.Toast);

        time.Advance(TimeSpan.FromSeconds(5.1));
        await SettleAsync(vm);
        Assert.Null(vm.Toast);
        Assert.Equal(appointment.Id, Assert.Single(vm.Columns[0].Items).Id);
    }

    [Fact]
    public async Task Fehler_steht_als_InfoBar_und_bleibt_bis_zum_Schliessen()
    {
        var appointment = TestData.Appointment(10, TestData.Anna);
        var api = Api(appointment);
        var rejected = new BookingRejected("Zu dieser Zeit ist nicht alles frei.", [new BookingConflict(ResourceKind.Room, TestData.Room.Id, "Raum Zeder", "belegt")]);
        api.On($"POST /api/appointments/{appointment.Id}/move", rejected, HttpStatusCode.Conflict);
        var time = new TimerTime();
        var vm = await OpenAsync(api, time: time);
        vm.BeginDrag(vm.Columns[0].Items[0], grabOffset: 0);
        vm.DragTo(10, vm.Columns[0].Items[0].Top + 60 * DayLayout.PixelsPerMinute);
        await vm.DropAsync();

        time.Advance(TimeSpan.FromMinutes(5));
        await SettleAsync(vm);
        Assert.Null(vm.Toast);
        Assert.NotNull(vm.Problem);

        vm.DismissProblemCommand.Execute(null);
        Assert.Null(vm.Problem);
    }

    [Fact]
    public async Task Solange_die_Maus_ueber_der_Meldung_steht_laeuft_die_Frist_nicht()
    {
        var (vm, time, _) = await MovedAsync();

        vm.HoldToast(true);
        time.Advance(TimeSpan.FromSeconds(30));
        await SettleAsync(vm);
        Assert.NotNull(vm.Toast);

        vm.HoldToast(false);
        time.Advance(TimeSpan.FromSeconds(8.1));
        await SettleAsync(vm);
        Assert.Null(vm.Toast);
    }

    [Fact]
    public async Task Gast_ohne_Telefon_und_Mail_bekommt_beim_Buchen_die_Pille_Keine_Kontaktmoeglichkeit()
    {
        var silent = new CustomerDto(Guid.NewGuid(), "Ohne", "Kontakt", null, null, null, null);
        var reachable = new CustomerDto(Guid.NewGuid(), "Mit", "Telefon", null, "+43 664 1", null, null);
        var api = Api()
            .On("GET /api/customers?search=ohne", new List<CustomerDto> { silent })
            .On("GET /api/customers?search=mit", new List<CustomerDto> { reachable });
        var vm = await OpenAsync(api);
        vm.StartBooking(vm.Columns[0], 30 * DayLayout.PixelsPerMinute);
        var booking = vm.Booking!;

        Assert.False(booking.HasNoContact);
        booking.Customer = silent;
        Assert.True(booking.HasNoContact);
        booking.Customer = reachable;
        Assert.False(booking.HasNoContact);
    }

    [Fact]
    public async Task Verschieben_Karte_sperrt_Tage_vor_heute_und_nennt_den_Grund_im_Tooltip()
    {
        var appointment = TestData.Appointment(10, TestData.Anna);
        var vm = await OpenAsync(Api(appointment));
        vm.Open(vm.Columns[0].Items[0]);
        var panel = vm.Selected!;
        panel.AskMoveToDayCommand.Execute(null);

        Assert.Equal(TestData.Monday, panel.Today);
        Assert.True(panel.MoveToDayCommand.CanExecute(null));
        Assert.False(panel.MoveDaysCommand.CanExecute("-1"));
        Assert.True(panel.MoveDaysCommand.CanExecute("1"));
        Assert.Equal("Verschieben (Strg+S)", panel.MoveToDayTip);

        panel.MoveDateText = "28.02.2027";
        Assert.False(panel.MoveToDayCommand.CanExecute(null));
        Assert.Equal("Verfügbar für heute oder einen späteren Tag.", panel.MoveToDayTip);

        panel.MoveDateText = "02.03.2027";
        Assert.True(panel.MoveToDayCommand.CanExecute(null));
        Assert.True(panel.MoveDaysCommand.CanExecute("-1"));

        // Ein unlesbarer Tag bleibt erlaubt: „Verschieben“ meldet ihn im Feld statt still zu sperren.
        panel.MoveDateText = "32.03.2027";
        Assert.True(panel.MoveToDayCommand.CanExecute(null));
    }

    [Fact]
    public async Task Einzeltermin_aus_dem_Terminfenster_auf_einen_anderen_Tag_verschieben()
    {
        var appointment = TestData.Appointment(10, TestData.Anna);
        var api = Api(appointment);
        var vm = await OpenAsync(api);
        var moved = appointment with { Start = TestData.At(14).AddDays(2), End = TestData.At(14, 50).AddDays(2), UpdatedAt = TestData.At(9) };
        api.On($"POST /api/appointments/{appointment.Id}/move", moved)
            .On($"GET /api/locations/{TestData.LocationId}/calendar?date=2027-03-03", TestData.Day(moved) with { Date = new DateOnly(2027, 3, 3) });

        vm.Open(vm.Columns[0].Items[0]);
        var panel = vm.Selected!;
        Assert.True(panel.CanMoveToDay);
        panel.AskMoveToDayCommand.Execute(null);
        Assert.Equal("01.03.2027", panel.MoveDateText);
        panel.MoveDaysCommand.Execute("1");
        panel.MoveDaysCommand.Execute("1");
        panel.MoveTimeText = "14:00";
        await panel.MoveToDayCommand.ExecuteAsync(null);
        await vm.LoadAsync();

        var request = JsonSerializer.Deserialize<MoveAppointmentRequest>(api.Calls.Last(c => c.Route.EndsWith("/move", StringComparison.Ordinal)).Body!, ApiClient.Json)!;
        Assert.Equal(TestData.At(14).AddDays(2), request.Start);
        Assert.Equal((null, null, null), (request.EmployeeId, request.RoomId, request.EquipmentId));
        Assert.Null(panel.Error);
        Assert.Equal(new DateOnly(2027, 3, 3), vm.Date);
        Assert.Equal(appointment.Id, Assert.Single(vm.Columns[0].Items).Id);
        Assert.Equal(appointment.Id, vm.MarkedId);
        Assert.True(vm.CanUndo);
        Assert.Contains("14:00–14:50", vm.Toast, StringComparison.Ordinal);

        panel.MoveDateText = "32.03.2027";
        await panel.MoveToDayCommand.ExecuteAsync(null);
        Assert.Equal("Den 32.03.2027 gibt es nicht.", panel.Error);
    }

    [Fact]
    public async Task Abgelehntes_Verschieben_springt_zurueck_und_nennt_den_Grund()
    {
        var appointment = TestData.Appointment(10, TestData.Anna);
        var api = Api(appointment);
        var vm = await OpenAsync(api);
        var rejected = new BookingRejected("Zu dieser Zeit ist nicht alles frei.", [new BookingConflict(ResourceKind.Room, TestData.Room.Id, "Raum Zeder", "belegt")]);
        api.On($"POST /api/appointments/{appointment.Id}/move", rejected, HttpStatusCode.Conflict);

        vm.BeginDrag(vm.Columns[0].Items[0], grabOffset: 0);
        vm.DragTo(10, vm.Columns[0].Items[0].Top + 60 * DayLayout.PixelsPerMinute);
        await vm.DropAsync();

        Assert.Equal(TestData.At(10), Assert.Single(vm.Columns[0].Items).Appointment!.Start);
        Assert.Equal(AispaStudio.Desktop.Controls.InfoBarSeverity.Error, vm.Problem!.Severity);
        Assert.Equal("Nicht verschoben", vm.Problem.Title);
        Assert.Equal("Zu dieser Zeit ist nicht alles frei. Raum Zeder: belegt.", vm.Problem.Message);
    }

    [Fact]
    public async Task Erledigte_Termine_und_Termine_ohne_Buchungsrecht_lassen_sich_nicht_ziehen()
    {
        var done = TestData.Appointment(10, TestData.Anna, AppointmentStatus.Completed);
        var open = TestData.Appointment(13, TestData.Ben);
        var reception = await OpenAsync(Api(done, open));
        var therapist = await OpenAsync(Api(done, open), user: TestData.Reception(TestData.Anna.Id) with { Rights = [AccessRight.ViewCalendar] });

        Assert.False(reception.CanMove(reception.Columns[0].Items[0]));
        Assert.True(reception.CanMove(reception.Columns[1].Items[0]));
        Assert.False(therapist.CanMove(therapist.Columns[1].Items[0]));
    }

    [Fact]
    public async Task Klick_auf_eine_Zahl_im_Tagesueberblick_hebt_die_Termine_hervor()
    {
        var checkedIn = TestData.Appointment(10, TestData.Anna, AppointmentStatus.CheckedIn);
        var expected = TestData.Appointment(13, TestData.Ben);
        var vm = await OpenAsync(Api(checkedIn, expected, TestData.Appointment(15, TestData.Ben, AppointmentStatus.NoShow)));
        CalendarItem Item(AppointmentDto a) => vm.Columns.SelectMany(c => c.Items).Single(i => i.Appointment?.Id == a.Id);

        vm.FocusOnCommand.Execute(CalendarFocus.Expected);

        Assert.Equal("Nur noch erwartet: 1", vm.FocusText);
        Assert.False(Item(expected).IsFaded);
        Assert.True(Item(checkedIn).IsFaded);
        Assert.True(Item(checkedIn).Opacity < 0.5);
        Assert.Equal(expected.Id, vm.MarkedId);

        // Andere Zahl wechselt, dieselbe noch einmal oder Esc zeigt wieder alles.
        vm.FocusOnCommand.Execute(CalendarFocus.CheckedIn);
        Assert.False(Item(checkedIn).IsFaded);
        Assert.True(Item(expected).IsFaded);
        vm.FocusOnCommand.Execute(CalendarFocus.CheckedIn);
        Assert.False(vm.HasFocus);
        Assert.False(Item(expected).IsFaded);

        vm.FocusOnCommand.Execute(CalendarFocus.Appointments);
        Assert.Equal("Nur Termine: 3", vm.FocusText);
        Assert.True(vm.HandleKey(Key.Escape, KeyModifiers.None));
        Assert.False(vm.HasFocus);
    }

    [Fact]
    public async Task Zusagen_ueber_den_Link_zaehlen_im_Tagesueberblick()
    {
        var confirmed = TestData.Appointment(10, TestData.Anna) with { ConfirmedAt = TestData.At(8, 12).AddDays(-1) };
        var open = TestData.Appointment(13, TestData.Ben);
        var checkedIn = TestData.Appointment(11, TestData.Ben, AppointmentStatus.CheckedIn);
        var withoutLinks = await OpenAsync(Api(open, checkedIn));
        var vm = await OpenAsync(Api(confirmed, open, checkedIn));
        CalendarItem Item(AppointmentDto a) => vm.Columns.SelectMany(c => c.Items).Single(i => i.Appointment?.Id == a.Id);

        vm.FocusOnCommand.Execute(CalendarFocus.Unconfirmed);

        // Ohne eine einzige Zusage am Tag verwendet das Spa keine Links: dann zählt „unbestätigt“ nicht.
        Assert.Equal("2 Termine · 1 da · 1 offen", withoutLinks.OverviewChip);
        Assert.False(withoutLinks.Overview!.UsesConfirmation);
        Assert.Equal("3 Termine · 1 da · 2 offen · 1 unbestätigt", vm.OverviewChip);
        Assert.Contains("✓ zugesagt", Item(confirmed).MetaText, StringComparison.Ordinal);
        Assert.DoesNotContain("zugesagt", Item(open).MetaText, StringComparison.Ordinal);
        Assert.Contains("Zugesagt über den Link am 28.02. um 08:12", Item(confirmed).HoverText, StringComparison.Ordinal);
        Assert.Equal("Nur unbestätigt: 1", vm.FocusText);
        Assert.False(Item(open).IsFaded);
        Assert.True(Item(confirmed).IsFaded);
        Assert.True(Item(checkedIn).IsFaded);
    }

    [Fact]
    public async Task Ueberfaellige_Termine_zaehlen_im_Tagesueberblick_und_lassen_sich_hervorheben()
    {
        // Jetzt ist 10:00: der Termin ab 9:00 ist bis 10:30 begonnen und nicht eingecheckt, der mit gemeldeter Verspätung erst ab 9:30 + 45 Min. fällig.
        var overdue = TestData.Appointment(9, TestData.Anna) with { End = TestData.At(10, 30) };
        var announcedLate = TestData.Appointment(9, TestData.Ben) with { End = TestData.At(10, 30), LateMinutes = 90 };
        var arrived = TestData.Appointment(9, TestData.Ben, AppointmentStatus.CheckedIn) with { End = TestData.At(10, 30) };
        var later = TestData.Appointment(13, TestData.Anna);
        var vm = await OpenAsync(Api(overdue, announcedLate, arrived, later));
        CalendarItem Item(AppointmentDto a) => vm.Columns.SelectMany(c => c.Items).Single(i => i.Appointment?.Id == a.Id);

        Assert.Equal(1, vm.Overview!.Overdue);
        Assert.EndsWith(" · 1 überfällig", vm.OverviewChip, StringComparison.Ordinal);

        vm.FocusOnCommand.Execute(CalendarFocus.Overdue);

        Assert.Equal("Nur überfällig: 1", vm.FocusText);
        Assert.False(Item(overdue).IsFaded);
        Assert.True(Item(announcedLate).IsFaded);
        Assert.True(Item(arrived).IsFaded);
        Assert.True(Item(later).IsFaded);
        Assert.Equal(overdue.Id, vm.MarkedId);

        // Ohne überfällige Termine fehlt der Zusatz in der Kopfzeile.
        var calm = await OpenAsync(Api(later));
        Assert.Equal(0, calm.Overview!.Overdue);
        Assert.DoesNotContain("überfällig", calm.OverviewChip, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Die_dritte_Zeile_nennt_zuerst_was_Handeln_verlangt_und_dann_die_Uhrzeit()
    {
        var overdue = TestData.Appointment(9, TestData.Anna) with { End = TestData.At(10, 30) };
        var checkedIn = TestData.Appointment(10, TestData.Ben, AppointmentStatus.CheckedIn);
        var vm = await OpenAsync(Api(overdue, checkedIn));
        CalendarItem Item(AppointmentDto a) => vm.Columns.SelectMany(c => c.Items).Single(i => i.Appointment?.Id == a.Id);

        Assert.Equal("überfällig 60 Min. · 09:00–10:30", Item(overdue).MetaText);
        Assert.Equal("Eingecheckt · 10:00–10:50", Item(checkedIn).MetaText);
    }

    [Fact]
    public void Tagesueberblick_und_als_Naechstes()
    {
        var day = TestData.Day(
            TestData.Appointment(10, TestData.Anna, AppointmentStatus.CheckedIn),
            TestData.Appointment(13, TestData.Ben),
            TestData.Appointment(15, TestData.Ben, AppointmentStatus.NoShow));

        var overview = DayLayout.Overview(day);
        var upcoming = DayLayout.Upcoming(day, TestData.Resources, TestData.At(11), Vienna);

        Assert.Equal(new DayOverview(3, 1, 1, 0, "10 %", "158 €", Unconfirmed: 1), overview);
        Assert.Equal(["13:00 Eva Muster"], upcoming.Select(u => $"{u.Time} {u.Customer}"));
        Assert.Equal("AK", DayLayout.Initials("Anna Kern"));
        Assert.Equal("LU", DayLayout.Initials("Lukas"));
        Assert.Equal("WB", DayLayout.Initials("Dr. Wolfgang Bauer"));
    }

    [Fact]
    public async Task Ziehen_zeigt_die_gebundene_Zeit_mit_Vor_und_Nachbereitung_im_Ghost()
    {
        var appointment = TestData.Appointment(10, TestData.Anna);
        var vm = await OpenAsync(Api(appointment));
        var item = vm.Columns[0].Items[0];

        vm.BeginDrag(item, grabOffset: 10);

        // Massage: 5 Minuten Vorbereitung (09:55) und 10 Minuten Nachbereitung (bis 11:00).
        Assert.Equal(5 * vm.Scale.PixelsPerMinute, vm.Ghost!.BeforeHeight, 3);
        Assert.Equal(10 * vm.Scale.PixelsPerMinute, vm.Ghost.AfterHeight, 3);
        Assert.Equal(vm.Ghost.Top - vm.Ghost.BeforeHeight, vm.Ghost.BeforeTop, 3);
        Assert.Equal(vm.Ghost.Top + vm.Ghost.Height, vm.Ghost.AfterTop, 3);
    }

    [Fact]
    public async Task Buchungsvorschau_unter_dem_Mauszeiger_hat_keine_gebundene_Zeit()
    {
        var vm = await OpenAsync(Api(TestData.Appointment(10, TestData.Anna)));

        vm.Hover(DayLayout.ColumnLeft(1) + 20, vm.Columns[0].Items[0].Top + 20);

        Assert.Equal(0, vm.Ghost!.BeforeHeight);
        Assert.Equal(0, vm.Ghost.AfterHeight);
    }
}
