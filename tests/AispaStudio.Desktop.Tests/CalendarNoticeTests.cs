using System.Net;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Controls;
using AispaStudio.Desktop.ViewModels;

namespace AispaStudio.Desktop.Tests;

/// <summary>Rückmeldungen des Kalenders nach docs/gestaltung/meldungen.md: Toast, Fehler als Meldung, „Heute“-Streifen (die Seitenleiste „Meldungen“ prüft MessagesPanelTests).</summary>
public sealed class CalendarNoticeTests
{
    private static readonly TimeZoneInfo Vienna = TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna");
    private static readonly string CalendarRoute = $"GET /api/locations/{TestData.LocationId}/calendar?date=2027-03-01";

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

    private static StubApi Api(CalendarDay day) => new StubApi()
        .On("GET /api/locations", new List<LocationDto> { TestData.Location })
        .On($"GET /api/locations/{TestData.LocationId}/resources", TestData.Resources)
        .On(CalendarRoute, day)
        .On($"GET /api/locations/{TestData.LocationId}/calendar?date=2027-03-02", day);

    private static async Task<CalendarViewModel> OpenAsync(StubApi api, TimeProvider? time = null)
    {
        var vm = new CalendarViewModel(api.Client(), TestData.Reception(), null, time ?? new TimerTime());
        await vm.InitializeAsync();
        await vm.LoadAsync();
        return vm;
    }

    private static async Task Until(Func<bool> condition)
    {
        for (var i = 0; i < 200 && !condition(); i++)
        {
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        Assert.True(condition());
    }

    private static CalendarDay DayWithBreakWarnings() => TestData.Day() with
    {
        Breaks =
        [
            new CalendarBreak(TestData.Anna.Id, 30, 0, "Keine Zeit mehr für die gesetzliche Pause: 30 Min."),
            new CalendarBreak(TestData.Ben.Id, 30, 0, "Keine Zeit mehr für die gesetzliche Pause: 30 Min."),
        ],
    };

    [Fact]
    public async Task Ein_Ladefehler_ist_eine_Meldung_Fehler_mit_Erneut_versuchen()
    {
        var api = new StubApi()
            .On("GET /api/locations", new List<LocationDto> { TestData.Location })
            .On($"GET /api/locations/{TestData.LocationId}/resources", TestData.Resources);
        var vm = new CalendarViewModel(api.Client(), TestData.Reception(), null, new TimerTime());
        await vm.InitializeAsync();
        await vm.LoadAsync();

        var notice = Assert.Single(vm.Messages);
        Assert.Equal(MessageSeverity.Error, notice.Severity);
        Assert.Equal("Erneut versuchen", notice.ActionLabel);

        api.On(CalendarRoute, TestData.Day());
        notice.ActionCommand!.Execute(null);
        await Until(() => vm.Messages.Count == 0);
        Assert.Null(vm.Error);
    }

    [Fact]
    public async Task Warnungen_und_Notizen_bleiben_getrennt_und_nur_Meldungen_zaehlen()
    {
        var note = new CalendarNoteDto(Guid.NewGuid(), TestData.Monday, "Lieferung 10 Uhr", null, null, null, false, TestData.At(8));
        var day = DayWithBreakWarnings() with { Notes = [note], Absences = [new CalendarAbsence(TestData.Ben.Id, RosterKind.Sick, TestData.Monday, null)] };
        var vm = await OpenAsync(Api(day));

        Assert.True(vm.HasDayNotes);
        Assert.Equal(["Lieferung 10 Uhr"], vm.DayNotes.Select(n => n.Text));
        Assert.All(vm.Messages, n => Assert.Equal(MessageSeverity.Warning, n.Severity));
        Assert.DoesNotContain(vm.Messages, n => n.Text.Contains("Lieferung", StringComparison.Ordinal));
        Assert.Equal("1 Notiz heute", vm.DayNotesText);
        Assert.Equal(vm.Messages.Count, vm.MessageCount);
    }

    [Fact]
    public async Task Toast_ohne_Rueckgaengig_verschwindet_nach_fuenf_Sekunden()
    {
        var appointment = TestData.Appointment(10, TestData.Anna);
        var time = new TimerTime();
        var api = Api(TestData.Day(appointment)).On($"PUT /api/appointments/{appointment.Id}/late", appointment);
        var vm = await OpenAsync(api, time);

        await vm.ReportLateAsync(appointment, 0);
        Assert.Equal("Eva Muster kommt doch pünktlich", vm.Toast);
        Assert.False(vm.CanUndo);

        time.Advance(TimeSpan.FromSeconds(4.9));
        await Task.Delay(30, TestContext.Current.CancellationToken);
        Assert.NotNull(vm.Toast);
        time.Advance(TimeSpan.FromSeconds(0.2));
        await Until(() => vm.Toast is null);
    }

    // ---------- Fluent 2 F21: Toast-Texte, Fehler und Warnungen als InfoBar ----------

    [Theory]
    [InlineData("Termin verschoben.", "Termin verschoben")]
    [InlineData("  Gebucht  ", "Gebucht")]
    [InlineData("Eva Muster kommt 15 Min. später (10:15)", "Eva Muster kommt 15 Min. später (10:15)")]
    public void Der_Toast_endet_ohne_Schlusspunkt(string text, string expected) => Assert.Equal(expected, CalendarViewModel.FitToast(text));

    [Fact]
    public void Ein_zu_langer_Toast_wird_auf_hoechstens_60_Zeichen_gekuerzt()
    {
        var line = CalendarViewModel.FitToast(new string('a', 40) + " " + new string('b', 40) + ".");

        Assert.Equal(CalendarViewModel.ToastMaxLength, line.Length);
        Assert.EndsWith("…", line, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Der_Toast_bleibt_auch_bei_einem_sehr_langen_Namen_unter_60_Zeichen_ohne_Punkt()
    {
        var appointment = TestData.Appointment(10, TestData.Anna) with { CustomerName = "Prof. Dr. Maria-Theresia von Habsburg-Lothringen zu Schönbrunn" };
        var late = appointment with { LateMinutes = 15 };
        var api = Api(TestData.Day(appointment)).On($"PUT /api/appointments/{appointment.Id}/late", late);
        var vm = await OpenAsync(api);

        await vm.ReportLateAsync(appointment, 15);

        Assert.InRange(vm.Toast!.Length, 1, CalendarViewModel.ToastMaxLength);
        Assert.False(vm.Toast.EndsWith('.'));
    }

    // ---------- A2 (B3, B4): Zeitpunkt in Toasts kurz, ohne Kürzung ----------

    [Theory]
    [InlineData(10, 14, 35, "14:35")] // heute (Montag 1.3.2027, 11:00 Ortszeit): nur die Uhrzeit
    [InlineData(10, 9, 5, "09:05")]
    public void Der_Zeitpunkt_im_Toast_nennt_am_heutigen_Tag_nur_die_Uhrzeit(int nowHour, int hour, int minute, string expected)
    {
        var today = DateOnly.FromDateTime(DayLayout.Local(TestData.At(nowHour), Vienna));

        Assert.Equal(expected, DayLayout.WhenText(TestData.At(hour, minute), Vienna, today));
    }

    [Fact]
    public void Der_Zeitpunkt_im_Toast_nennt_an_einem_anderen_Tag_den_kurzen_Wochentag_und_die_Uhrzeit()
    {
        var today = DateOnly.FromDateTime(DayLayout.Local(TestData.At(10), Vienna));

        Assert.Equal("Do 14:35", DayLayout.WhenText(TestData.At(14, 35).AddDays(3), Vienna, today));
    }

    [Fact]
    public async Task Die_Toasts_nach_Stornierung_zuruecknehmen_und_Verschieben_enden_mit_Demo_Namen_nicht_auf_Auslassungspunkte()
    {
        var appointment = TestData.Appointment(10, TestData.Anna) with { CustomerName = "Elif Demir", RoomId = TestData.Room.Id };
        var api = Api(TestData.Day(appointment)).On($"POST /api/appointments/{appointment.Id}/cancel", appointment with { Status = AppointmentStatus.Cancelled, UpdatedAt = TestData.At(9, 1) });
        api.On($"POST /api/appointments/{appointment.Id}/restore", appointment with { Status = AppointmentStatus.Booked, UpdatedAt = TestData.At(9, 2) });
        var vm = await OpenAsync(api);
        vm.Dialogs = new FakeDialogs().Answer(ConfirmChoice.Confirmed, "Versehen");
        vm.Mark(vm.Columns[0].Items[0]);
        vm.HandleKey(Avalonia.Input.Key.Delete, Avalonia.Input.KeyModifiers.None);
        await Until(() => vm.Toast == "Termin storniert");

        await vm.UndoMoveCommand.ExecuteAsync(null);

        Assert.Equal("Wieder gebucht: Elif Demir, 10:00", vm.Toast);
    }

    [Fact]
    public async Task Verschieben_auf_einen_anderen_Tag_meldet_Wochentag_und_Uhrzeit_in_hoechstens_60_Zeichen()
    {
        var appointment = TestData.Appointment(10, TestData.Anna) with { CustomerName = "Elif Demir", RoomId = TestData.Room.Id };
        var moved = appointment with { Start = TestData.At(14, 35).AddDays(3), End = TestData.At(15, 20).AddDays(3), UpdatedAt = TestData.At(9) };
        var api = Api(TestData.Day(appointment)).On($"POST /api/appointments/{appointment.Id}/move", moved);
        var vm = await OpenAsync(api);
        vm.BeginDrag(vm.Columns[0].Items[0], grabOffset: 0);
        vm.DragTo(10, vm.Columns[0].Items[0].Top + 270 * DayLayout.PixelsPerMinute);
        await vm.DropAsync();
        Assert.StartsWith("Verschoben auf", vm.Toast, StringComparison.Ordinal);
        Assert.EndsWith("14:35", vm.Toast, StringComparison.Ordinal);
        Assert.Equal("Verschoben auf Do 14:35", vm.Toast);
    }

    [Fact]
    public async Task Eine_Sperrzeit_ohne_Grund_meldet_den_Fehler_am_Feld_Grund_ohne_Bitte()
    {
        var vm = await OpenAsync(Api(TestData.Day()));
        vm.OpenNoteAt(10, 100);
        Assert.True(vm.SelectNewEntry(4));
        var entry = vm.Entry!;
        entry.Text = "";

        await entry.SaveAsync();

        Assert.Equal("Der Grund fehlt.", entry.TextError);
        Assert.Null(entry.Error);
    }

    [Fact]
    public async Task Ein_gescheiterter_Aufruf_ist_ein_Fehler_als_InfoBar_mit_Erneut_versuchen_und_kein_Toast()
    {
        var appointment = TestData.Appointment(10, TestData.Anna);
        var route = $"PUT /api/appointments/{appointment.Id}/late";
        var api = Api(TestData.Day(appointment)).On(route, new { title = "Der Server antwortet nicht." }, HttpStatusCode.ServiceUnavailable);
        var vm = await OpenAsync(api);

        await vm.ReportLateAsync(appointment, 15);

        Assert.Null(vm.Toast);
        Assert.Equal(InfoBarSeverity.Error, vm.Problem!.Severity);
        Assert.Equal("Verspätung nicht gespeichert", vm.Problem.Title);
        Assert.Contains("Der Server antwortet nicht", vm.Problem.Message, StringComparison.Ordinal);
        Assert.Equal("Erneut versuchen", vm.Problem.ActionText);

        // „Erneut versuchen“ führt dieselbe Aktion noch einmal aus und schließt die Meldung.
        api.On(route, appointment with { LateMinutes = 15 });
        vm.Problem!.Action!.Execute(null);
        await Until(() => vm.Toast is not null);
        Assert.Null(vm.Problem);
    }

    [Fact]
    public async Task Ein_geschlossenes_Problem_kommt_in_einer_neuen_Sitzung_wieder_wenn_die_Aktion_wieder_scheitert()
    {
        var appointment = TestData.Appointment(10, TestData.Anna);
        var failing = Api(TestData.Day(appointment)).On($"PUT /api/appointments/{appointment.Id}/late", new { title = "Der Server antwortet nicht." }, HttpStatusCode.ServiceUnavailable);
        var first = await OpenAsync(failing);
        await first.ReportLateAsync(appointment, 15);
        first.DismissProblemCommand.Execute(null);
        Assert.Null(first.Problem);

        var second = await OpenAsync(failing);
        await second.ReportLateAsync(appointment, 15);

        Assert.NotNull(second.Problem);
    }

    [Fact]
    public async Task Eine_geschlossene_Fehlermeldung_zum_Laden_kommt_nach_dem_Neustart_wieder()
    {
        var api = new StubApi()
            .On("GET /api/locations", new List<LocationDto> { TestData.Location })
            .On($"GET /api/locations/{TestData.LocationId}/resources", TestData.Resources);
        var first = await OpenAsync(api);
        var notice = Assert.Single(first.Messages);
        notice.DismissCommand!.Execute(null);
        Assert.Empty(first.Messages);

        var second = await OpenAsync(api);

        Assert.Equal(notice.Key, Assert.Single(second.Messages).Key);
    }
}
