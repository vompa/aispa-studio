using System.Net;
using System.Text.Json;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;
using AispaStudio.Desktop.ViewModels;

namespace AispaStudio.Desktop.Tests;

/// <summary>
/// Stornierung zu einer neuen Zeit zurücknehmen (Arbeitsanweisung Verlauf, Aufgabe E): im Termin-Panel fragt der gemeinsame Dienst <c>AppointmentRestorer</c> vor der Rückfrage
/// nach Vergangenheit und Belegung; der Kalender springt auf den nächsten freien Tag und zeigt die Verschieben-Karte mit den Freie-Zeit-Vorschlägen des Gastes.
/// </summary>
public sealed class RestoreAtNewTimeTests
{
    private static readonly TimeZoneInfo Vienna = TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna");

    private static readonly CustomerDto Guest = new(Guid.NewGuid(), "Eva", "Muster", null, "0664 1234567", null, null);

    private sealed class FixedTime : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => TestData.At(10);

        public override TimeZoneInfo LocalTimeZone => Vienna;
    }

    /// <summary>Gestern um 14 Uhr, storniert: der Tag ist vorbei.</summary>
    private static AppointmentDto Yesterday() => Cancelled(14, -1);

    private static AppointmentDto Cancelled(int hour, int dayOffset = 0) => TestData.Appointment(hour, TestData.Anna, AppointmentStatus.Cancelled) with
    {
        CustomerId = Guest.Id,
        Start = TestData.At(hour).AddDays(dayOffset),
        End = TestData.At(hour, 50).AddDays(dayOffset),
        BlockedFrom = TestData.At(hour).AddMinutes(-5).AddDays(dayOffset),
        BlockedUntil = TestData.At(hour).AddMinutes(60).AddDays(dayOffset),
        Version = 4,
    };

    private static AvailableSlot Slot(int dayOffset, int hour) =>
        new(TestData.At(hour).AddDays(dayOffset), TestData.At(hour, 50).AddDays(dayOffset), TestData.Anna.Id, TestData.Room.Id, null);

    private static string SearchRoute(string date = "2027-03-01") =>
        $"GET /api/locations/{TestData.LocationId}/availability?treatmentId={TestData.MassageTreatment.Id}&date={date}&employeeId={TestData.Anna.Id}&roomId={TestData.Room.Id}&customerId={Guest.Id}&minSlots=3&days=14";

    private static StubApi Api(params AppointmentDto[] today) => new StubApi()
        .On("GET /api/locations", new List<LocationDto> { TestData.Location })
        .On($"GET /api/locations/{TestData.LocationId}/resources", TestData.Resources)
        .On($"GET /api/locations/{TestData.LocationId}/calendar?date=2027-03-01", TestData.Day(today))
        .On($"GET /api/locations/{TestData.LocationId}/calendar?date=2027-03-03", TestData.Day() with { Date = TestData.Monday.AddDays(2) })
        .On($"GET /api/customers/{Guest.Id}", Guest);

    private static async Task<CalendarViewModel> OpenAsync(StubApi api, FakeDialogs? dialogs = null)
    {
        var vm = new CalendarViewModel(api.Client(), TestData.Owner(), null, new FixedTime()) { Dialogs = dialogs };
        await vm.InitializeAsync();
        await vm.LoadAsync();
        return vm;
    }

    private static T BodyOf<T>(StubApi api, string routeEnd) =>
        JsonSerializer.Deserialize<T>(api.Calls.Last(c => c.Route.EndsWith(routeEnd, StringComparison.Ordinal) && c.Body is not null).Body!, ApiClient.Json)!;

    private static BookingRejected Occupied() => new("Zu dieser Zeit ist Anna schon belegt.",
        [new BookingConflict(ResourceKind.Employee, TestData.Anna.Id, "Anna", "ist zu dieser Zeit schon belegt", TestData.At(10), TestData.At(11), Guid.NewGuid())]);

    // ---------- Termin-Panel: der gemeinsame Dienst entscheidet vor der Rückfrage ----------

    [Fact]
    public async Task Given_ein_vergangener_Termin_When_das_Termin_Panel_zuruecknimmt_Then_fragt_es_nach_einer_neuen_Zeit_und_oeffnet_Verschieben_ohne_restore()
    {
        var cancelled = Yesterday();
        var api = Api().On($"GET /api/appointments/{cancelled.Id}/restoration", new AppointmentRestoration(CancellationNoticeState.None, false, InPast: true));
        var dialogs = new FakeDialogs().Answer(ConfirmChoice.Confirmed);
        var vm = await OpenAsync(api, dialogs);
        vm.OpenAppointment(cancelled);
        var panel = vm.Selected!;

        await panel.AskRestoreCommand.ExecuteAsync(null);

        var asked = Assert.Single(dialogs.AssertRules().Asked);
        Assert.Equal(("Zu neuer Zeit zurücknehmen?", "Neue Zeit wählen"), (asked.Title, asked.ConfirmText));
        Assert.Contains("Der Termin liegt in der Vergangenheit.", asked.Message, StringComparison.Ordinal);
        Assert.True(panel.IsMovingToDay);
        Assert.DoesNotContain(api.Calls, c => c.Route.EndsWith("/restore", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Given_die_Zeit_ist_belegt_When_das_Termin_Panel_zuruecknimmt_Then_nennt_die_Rueckfrage_die_Belegung_und_das_Panel_zeigt_sie_in_der_Karte()
    {
        var cancelled = Cancelled(10);
        var api = Api().On($"GET /api/appointments/{cancelled.Id}/restoration", new AppointmentRestoration(CancellationNoticeState.None, false, Conflict: Occupied()));
        var dialogs = new FakeDialogs().Answer(ConfirmChoice.Confirmed);
        var vm = await OpenAsync(api, dialogs);
        vm.OpenAppointment(cancelled);
        var panel = vm.Selected!;

        await panel.AskRestoreCommand.ExecuteAsync(null);

        Assert.Contains("Die Zeit ist inzwischen belegt: Termin · 10:00–11:00 · Anna", Assert.Single(dialogs.Asked).Message, StringComparison.Ordinal);
        Assert.True(panel.IsMovingToDay);
        Assert.NotNull(panel.Conflict);
        Assert.True(panel.ConflictInMove);
    }

    [Fact]
    public async Task Given_die_Rueckfrage_zur_neuen_Zeit_wird_abgebrochen_When_das_Termin_Panel_zuruecknimmt_Then_bleibt_alles_wie_es_war()
    {
        var cancelled = Yesterday();
        var api = Api().On($"GET /api/appointments/{cancelled.Id}/restoration", new AppointmentRestoration(CancellationNoticeState.None, false, InPast: true));
        var vm = await OpenAsync(api, new FakeDialogs().Answer(ConfirmChoice.Cancelled));
        vm.OpenAppointment(cancelled);
        var panel = vm.Selected!;

        await panel.AskRestoreCommand.ExecuteAsync(null);

        Assert.False(panel.IsMovingToDay);
        Assert.Null(panel.Conflict);
        Assert.DoesNotContain(api.Calls, c => c.Route.EndsWith("/restore", StringComparison.Ordinal));
    }

    // ---------- Kalender: nächster freier Tag, Verschieben-Karte, Vorschläge ----------

    [Fact]
    public async Task Given_ein_vergangener_Termin_When_der_Kalender_die_neue_Zeit_anbietet_Then_zeigt_er_den_Tag_des_ersten_Vorschlags_und_das_Panel_mit_offener_Karte_und_Vorschlaegen()
    {
        var cancelled = Yesterday();
        var api = Api().On(SearchRoute(), new List<AvailableSlot> { Slot(2, 9), Slot(2, 11), Slot(3, 10) });
        var vm = await OpenAsync(api);

        await vm.ShowRestoreAtNewTimeAsync(new NewTimeRequest(cancelled, false, null));

        // Gesucht wird ab heute (der Tag des Termins ist vorbei), mit Gast, Behandlung, Person und Raum des Termins.
        Assert.Contains(api.Calls, c => c.Route == SearchRoute());
        Assert.Equal(new DateOnly(2027, 3, 3), vm.Date);
        var panel = Assert.IsType<AppointmentViewModel>(vm.Selected);
        Assert.Equal(cancelled.Id, panel.Appointment.Id);
        Assert.True(panel.ShowRestore);
        Assert.True(panel.IsMovingToDay);
        Assert.Equal(3, panel.RestoreSlots.Count);
        Assert.True(panel.ShowRestoreSlots);
        Assert.Equal(("03.03.2027", "09:00"), (panel.MoveDateText, panel.MoveTimeText));
        Assert.Null(vm.FreeTime);
    }

    [Fact]
    public async Task Given_ein_Termin_in_der_Zukunft_dessen_Zeit_belegt_ist_When_der_Kalender_die_neue_Zeit_anbietet_Then_beginnt_die_Suche_am_Tag_des_Termins_und_die_Belegung_steht_in_der_Karte()
    {
        var cancelled = Cancelled(10, 2);
        var api = Api().On(SearchRoute("2027-03-03"), new List<AvailableSlot> { Slot(2, 12), Slot(2, 15), Slot(3, 10) });
        var vm = await OpenAsync(api);

        await vm.ShowRestoreAtNewTimeAsync(new NewTimeRequest(cancelled, false, Occupied()));

        Assert.Contains(api.Calls, c => c.Route == SearchRoute("2027-03-03"));
        Assert.Equal(new DateOnly(2027, 3, 3), vm.Date);
        var panel = vm.Selected!;
        Assert.NotNull(panel.Conflict);
        Assert.Equal(("03.03.2027", "12:00"), (panel.MoveDateText, panel.MoveTimeText));
    }

    [Fact]
    public async Task Given_nichts_ist_frei_When_der_Kalender_die_neue_Zeit_anbietet_Then_bleibt_er_am_Suchtag_und_die_Karte_ist_ohne_Vorschlaege_offen()
    {
        var cancelled = Yesterday();
        var api = Api().On(SearchRoute(), new List<AvailableSlot>());
        var vm = await OpenAsync(api);

        await vm.ShowRestoreAtNewTimeAsync(new NewTimeRequest(cancelled, false, null));

        Assert.Equal(TestData.Monday, vm.Date);
        var panel = vm.Selected!;
        Assert.True(panel.IsMovingToDay);
        Assert.Empty(panel.RestoreSlots);
        Assert.False(panel.ShowRestoreSlots);
    }

    [Fact]
    public async Task Given_ein_Vorschlag_When_er_gewaehlt_und_bestaetigt_wird_Then_schickt_restore_den_neuen_Beginn_und_die_Korrektur_des_Wunsches()
    {
        var cancelled = Yesterday();
        var api = Api().On(SearchRoute(), new List<AvailableSlot> { Slot(2, 9), Slot(2, 11), Slot(3, 10) });
        var vm = await OpenAsync(api);
        await vm.ShowRestoreAtNewTimeAsync(new NewTimeRequest(cancelled, true, null));
        var panel = vm.Selected!;
        api.On($"POST /api/appointments/{cancelled.Id}/restore", cancelled with { Status = AppointmentStatus.Booked, Start = TestData.At(11).AddDays(2), End = TestData.At(11, 50).AddDays(2), Version = 5 });

        panel.ChooseRestoreSlotCommand.Execute(panel.RestoreSlots[1]);
        Assert.Equal(("03.03.2027", "11:00"), (panel.MoveDateText, panel.MoveTimeText));
        await panel.MoveToDayCommand.ExecuteAsync(null);

        var request = BodyOf<RestoreAppointmentRequest>(api, "/restore");
        Assert.Equal((TestData.At(11).AddDays(2), true, 4u), (request.Start, request.SendCorrection, request.ExpectedVersion));
    }

    [Fact]
    public async Task Given_die_Karte_ist_offen_When_die_Rezeption_abbricht_Then_bleibt_der_Termin_storniert_und_nichts_wird_gesendet()
    {
        var cancelled = Yesterday();
        var api = Api().On(SearchRoute(), new List<AvailableSlot> { Slot(2, 9), Slot(2, 11), Slot(3, 10) });
        var vm = await OpenAsync(api);
        await vm.ShowRestoreAtNewTimeAsync(new NewTimeRequest(cancelled, false, null));
        var panel = vm.Selected!;

        panel.CancelMoveToDayCommand.Execute(null);

        Assert.False(panel.IsMovingToDay);
        Assert.False(panel.ShowRestoreSlots);
        Assert.Equal(AppointmentStatus.Cancelled, panel.Appointment.Status);
        Assert.DoesNotContain(api.Calls, c => c.Route.EndsWith("/restore", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Given_der_Gast_ist_nicht_ladbar_When_der_Kalender_die_neue_Zeit_anbietet_Then_sucht_er_ohne_Gast()
    {
        var cancelled = Yesterday();
        var api = Api().On($"GET /api/customers/{Guest.Id}", null, HttpStatusCode.NotFound);
        var route = SearchRoute().Replace($"&customerId={Guest.Id}", "", StringComparison.Ordinal);
        api.On(route, new List<AvailableSlot> { Slot(1, 9), Slot(1, 11), Slot(1, 13) });
        var vm = await OpenAsync(api);

        await vm.ShowRestoreAtNewTimeAsync(new NewTimeRequest(cancelled, false, null));

        Assert.Contains(api.Calls, c => c.Route == route);
        Assert.Equal(3, vm.Selected!.RestoreSlots.Count);
    }

    [Fact]
    public async Task Given_die_Shell_When_Neue_Zeit_waehlen_aus_dem_Verlauf_kommt_Then_wechselt_sie_in_den_Kalender()
    {
        var cancelled = Yesterday();
        var api = Api().On(SearchRoute(), new List<AvailableSlot> { Slot(2, 9), Slot(2, 11), Slot(3, 10) })
            .On("GET /api/pos/registers", new List<CashRegisterDto>());
        var shell = new ShellViewModel(api.Client(), TestData.Owner(), null, new FixedTime(), new ThemeController(new MemorySettingsStore()), "Test", () => Task.CompletedTask);
        await shell.Calendar.InitializeAsync();
        await shell.Calendar.LoadAsync();

        await shell.ShowRestoreAtNewTimeAsync(new NewTimeRequest(cancelled, false, null));

        Assert.Same(shell.Calendar, shell.SelectedPage?.Page);
        Assert.Equal(new DateOnly(2027, 3, 3), shell.Calendar.Date);
        Assert.True(shell.Calendar.Selected?.IsMovingToDay);
    }
}
