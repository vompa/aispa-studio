using System.Net;
using System.Text.Json;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;
using AispaStudio.Desktop.ViewModels;
using Avalonia.Input;

namespace AispaStudio.Desktop.Tests;

/// <summary>
/// Rückgängig für Stornieren und Einchecken (Ä95, Ä97): die Meldung mit „Rückgängig“ und Strg+Z, später „Stornierung zurücknehmen“ und „Einchecken zurücknehmen“ im Panel.
/// Alle Tests laufen mit Inhaber-Rechten.
/// </summary>
public sealed class UndoCancelCheckInTests
{
    private static readonly TimeZoneInfo Vienna = TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna");

    private sealed class FixedTime : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => TestData.At(10);

        public override TimeZoneInfo LocalTimeZone => Vienna;
    }

    private static StubApi Api(params AppointmentDto[] appointments) => new StubApi()
        .On("GET /api/locations", new List<LocationDto> { TestData.Location })
        .On($"GET /api/locations/{TestData.LocationId}/resources", TestData.Resources)
        .On($"GET /api/locations/{TestData.LocationId}/calendar?date=2027-03-01", TestData.Day(appointments));

    private static async Task<CalendarViewModel> OpenAsync(StubApi api)
    {
        var vm = new CalendarViewModel(api.Client(), TestData.Owner(), null, new FixedTime());
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

    /// <summary>Der Body des letzten Aufrufs an diese Route.</summary>
    private static T BodyOf<T>(StubApi api, string routeEnd) =>
        JsonSerializer.Deserialize<T>(api.Calls.Last(c => c.Route.EndsWith(routeEnd, StringComparison.Ordinal) && c.Body is not null).Body!, ApiClient.Json)!;

    private static AppointmentDto Later(AppointmentDto appointment, AppointmentStatus status, int minute) =>
        appointment with { Status = status, UpdatedAt = TestData.At(9, minute) };

    [Fact]
    public async Task Given_ein_soeben_stornierter_Termin_When_Strg_Z_gedrueckt_wird_Then_steht_der_Termin_wieder_auf_Gebucht()
    {
        var appointment = TestData.Appointment(10, TestData.Anna);
        var api = Api(appointment);
        var vm = await OpenAsync(api);
        api.On($"POST /api/appointments/{appointment.Id}/cancel", Later(appointment, AppointmentStatus.Cancelled, 1));
        vm.Dialogs = new FakeDialogs().Answer(ConfirmChoice.Confirmed, "Versehen");
        vm.Mark(vm.Columns[0].Items[0]);

        vm.HandleKey(Key.Delete, KeyModifiers.None);
        await Until(() => vm.Toast == "Termin storniert");

        Assert.True(vm.CanUndo);
        Assert.Contains(vm.KeyHints, h => h is { Key: "Strg+Z", Text: "Rückgängig" });
        Assert.Empty(vm.Columns[0].Items);

        api.On($"POST /api/appointments/{appointment.Id}/restore", Later(appointment, AppointmentStatus.Booked, 2));
        vm.HandleKey(Key.Z, KeyModifiers.Control);
        await Until(() => vm.Columns[0].Items.Count == 1);

        Assert.Equal(AppointmentStatus.Booked, vm.Columns[0].Items[0].Appointment!.Status);
        Assert.Contains(api.Calls, c => c.Route == $"POST /api/appointments/{appointment.Id}/restore");
        Assert.False(vm.CanUndo);
        Assert.DoesNotContain(vm.KeyHints, h => h.Key == "Strg+Z");
    }

    [Fact]
    public async Task Die_Meldung_Termin_storniert_hat_den_Knopf_Rueckgaengig_der_dasselbe_tut()
    {
        var appointment = TestData.Appointment(10, TestData.Anna);
        var api = Api(appointment);
        var vm = await OpenAsync(api);
        api.On($"POST /api/appointments/{appointment.Id}/cancel", Later(appointment, AppointmentStatus.Cancelled, 1));
        api.On($"POST /api/appointments/{appointment.Id}/restore", Later(appointment, AppointmentStatus.Booked, 2));
        vm.Dialogs = new FakeDialogs().Answer(ConfirmChoice.Confirmed, "Versehen");
        vm.Mark(vm.Columns[0].Items[0]);
        vm.HandleKey(Key.Delete, KeyModifiers.None);
        await Until(() => vm.Toast == "Termin storniert");

        await vm.UndoMoveCommand.ExecuteAsync(null);

        Assert.Single(vm.Columns[0].Items);
        Assert.StartsWith("Termin wieder gebucht", vm.Toast, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Given_ein_eingecheckter_Termin_When_Strg_Z_gedrueckt_wird_Then_steht_er_wieder_auf_Gebucht()
    {
        var appointment = TestData.Appointment(10, TestData.Anna);
        var api = Api(appointment);
        var vm = await OpenAsync(api);
        api.On($"POST /api/appointments/{appointment.Id}/status", Later(appointment, AppointmentStatus.CheckedIn, 1));
        vm.Mark(vm.Columns[0].Items[0]);
        vm.HandleKey(Key.E, KeyModifiers.None);
        await Until(() => vm.Columns[0].Items[0].IsCheckedIn);
        Assert.Equal("Eva Muster eingecheckt", vm.Toast);
        Assert.True(vm.CanUndo);

        api.On($"POST /api/appointments/{appointment.Id}/status", Later(appointment, AppointmentStatus.Booked, 2));
        vm.HandleKey(Key.Z, KeyModifiers.Control);
        await Until(() => !vm.Columns[0].Items[0].IsCheckedIn);

        var request = BodyOf<AppointmentStatusChange>(api, "/status");
        Assert.Equal(AppointmentStatus.Booked, request.Status);
        Assert.False(vm.CanUndo);
    }

    [Fact]
    public async Task Given_ein_eingecheckter_Termin_When_Einchecken_zurueckgenommen_wird_Then_steht_er_wieder_auf_Gebucht()
    {
        var appointment = TestData.Appointment(10, TestData.Anna, AppointmentStatus.CheckedIn);
        var api = Api(appointment);
        var vm = await OpenAsync(api);
        api.On($"POST /api/appointments/{appointment.Id}/status", Later(appointment, AppointmentStatus.Booked, 1));
        vm.OpenAppointment(appointment);
        var panel = vm.Selected!;

        Assert.True(panel.ShowUndoCheckIn);
        Assert.True(panel.HasMore);
        await panel.UndoCheckInCommand.ExecuteAsync(null);

        Assert.Equal(AppointmentStatus.Booked, vm.Selected!.Appointment.Status);
        var request = BodyOf<AppointmentStatusChange>(api, "/status");
        Assert.Equal(AppointmentStatus.Booked, request.Status);
    }

    [Fact]
    public async Task Given_ein_kassierter_Termin_When_sein_Panel_geoeffnet_wird_Then_gibt_es_kein_Einchecken_zurueckzunehmen()
    {
        var done = TestData.Appointment(10, TestData.Anna, AppointmentStatus.Completed);
        var booked = TestData.Appointment(13, TestData.Anna);
        var vm = await OpenAsync(Api(done, booked));

        vm.OpenAppointment(done);
        Assert.False(vm.Selected!.ShowUndoCheckIn);

        // Ein gebuchter Termin ist noch nicht eingecheckt.
        vm.OpenAppointment(booked);
        Assert.False(vm.Selected!.ShowUndoCheckIn);
    }

    [Fact]
    public async Task Given_ein_eingecheckter_Termin_auf_einer_Rechnung_When_sein_Panel_geoeffnet_wird_Then_gibt_es_kein_Einchecken_zurueckzunehmen()
    {
        var billed = TestData.Appointment(10, TestData.Anna, AppointmentStatus.CheckedIn);
        var api = Api(billed);
        api.On($"GET /api/locations/{TestData.LocationId}/calendar?date=2027-03-01", TestData.Day(billed) with { BilledAppointmentIds = [billed.Id] });
        var vm = await OpenAsync(api);

        vm.OpenAppointment(billed);

        Assert.False(vm.Selected!.ShowUndoCheckIn);
    }

    [Fact]
    public async Task Given_ein_stornierter_Termin_When_sein_Panel_geoeffnet_wird_Then_bietet_es_Stornierung_zurueckzunehmen_und_nimmt_sie_nach_Rueckfrage_zurueck()
    {
        var cancelled = TestData.Appointment(10, TestData.Anna, AppointmentStatus.Cancelled);
        var api = Api(cancelled);
        var vm = await OpenAsync(api);
        api.On($"GET /api/appointments/{cancelled.Id}/restoration", new AppointmentRestoration(CancellationNoticeState.None, false));
        api.On($"POST /api/appointments/{cancelled.Id}/restore", Later(cancelled, AppointmentStatus.Booked, 1));
        var dialogs = new FakeDialogs().Answer(ConfirmChoice.Confirmed);
        vm.Dialogs = dialogs;
        vm.OpenAppointment(cancelled);
        var panel = vm.Selected!;
        Assert.True(panel.ShowRestore);

        await panel.AskRestoreCommand.ExecuteAsync(null);

        var asked = Assert.Single(dialogs.Asked);
        Assert.Equal("Stornierung zurücknehmen?", asked.Title);
        dialogs.AssertRules();
        Assert.Null(asked.AlternativeText);
        Assert.Equal(AppointmentStatus.Booked, vm.Selected!.Appointment.Status);
        Assert.False(vm.Selected.ShowRestore);
        var request = BodyOf<RestoreAppointmentRequest>(api, "/restore");
        Assert.Equal((null, false), (request.Start, request.SendCorrection));
    }

    [Fact]
    public async Task Given_ein_stornierter_Termin_dessen_Zeit_inzwischen_belegt_ist_When_Stornierung_zurueckgenommen_wird_Then_nennt_die_Rueckfrage_den_Konflikt_und_bietet_Verschieben_an()
    {
        var cancelled = TestData.Appointment(10, TestData.Anna, AppointmentStatus.Cancelled);
        var api = Api(cancelled);
        var vm = await OpenAsync(api);
        var conflict = new BookingConflict(ResourceKind.Employee, TestData.Anna.Id, "Anna", "ist zu dieser Zeit schon belegt", TestData.At(10), TestData.At(11), Guid.NewGuid());
        api.On($"GET /api/appointments/{cancelled.Id}/restoration", new AppointmentRestoration(CancellationNoticeState.None, false));
        api.On($"POST /api/appointments/{cancelled.Id}/restore", new BookingRejected("Zu dieser Zeit ist nicht alles frei.", [conflict]), HttpStatusCode.Conflict);
        var dialogs = new FakeDialogs().Answer(ConfirmChoice.Confirmed).Answer(ConfirmChoice.Confirmed);
        vm.Dialogs = dialogs;
        vm.OpenAppointment(cancelled);
        var panel = vm.Selected!;

        await panel.AskRestoreCommand.ExecuteAsync(null);

        Assert.Equal(2, dialogs.Asked.Count);
        var second = dialogs.Asked[1];
        Assert.Contains("Anna", second.Message, StringComparison.Ordinal);
        Assert.Equal("Verschieben …", second.ConfirmText);
        dialogs.AssertRules();
        Assert.True(panel.IsMovingToDay);
        Assert.NotNull(panel.Conflict);

        // „Verschieben“ nimmt die Stornierung zu der neuen Zeit zurück.
        api.On($"POST /api/appointments/{cancelled.Id}/restore", Later(cancelled with { Start = TestData.At(15), End = TestData.At(15, 50) }, AppointmentStatus.Booked, 3));
        panel.MoveTimeText = "15:00";
        await panel.MoveToDayCommand.ExecuteAsync(null);

        var request = BodyOf<RestoreAppointmentRequest>(api, "/restore");
        Assert.Equal(TestData.At(15), request.Start);
        Assert.Equal(AppointmentStatus.Booked, vm.Selected!.Appointment.Status);
    }

    [Fact]
    public async Task Given_die_Absage_ist_schon_beim_Gast_When_Stornierung_zurueckgenommen_wird_Then_fragt_die_Rueckfrage_ob_der_Gast_eine_Korrektur_bekommt()
    {
        var cancelled = TestData.Appointment(10, TestData.Anna, AppointmentStatus.Cancelled);
        var api = Api(cancelled);
        var vm = await OpenAsync(api);
        api.On($"GET /api/appointments/{cancelled.Id}/restoration", new AppointmentRestoration(CancellationNoticeState.Sent, false));
        api.On($"POST /api/appointments/{cancelled.Id}/restore", Later(cancelled, AppointmentStatus.Booked, 1));
        var dialogs = new FakeDialogs().Answer(ConfirmChoice.Confirmed);
        vm.Dialogs = dialogs;
        vm.OpenAppointment(cancelled);

        await vm.Selected!.AskRestoreCommand.ExecuteAsync(null);

        var asked = Assert.Single(dialogs.Asked);
        Assert.Contains("Korrektur", asked.Message, StringComparison.Ordinal);
        Assert.Equal("Mit Korrektur zurücknehmen", asked.ConfirmText);
        Assert.Equal("Ohne Korrektur", asked.AlternativeText);
        dialogs.AssertRules();
        var request = BodyOf<RestoreAppointmentRequest>(api, "/restore");
        Assert.True(request.SendCorrection);
    }

    [Fact]
    public async Task Given_die_Absage_wartet_noch_When_Stornierung_zurueckgenommen_wird_Then_sagt_die_Rueckfrage_dass_sie_nicht_mehr_gesendet_wird()
    {
        var cancelled = TestData.Appointment(10, TestData.Anna, AppointmentStatus.Cancelled);
        var api = Api(cancelled);
        var vm = await OpenAsync(api);
        api.On($"GET /api/appointments/{cancelled.Id}/restoration", new AppointmentRestoration(CancellationNoticeState.Waiting, false));
        var dialogs = new FakeDialogs().Answer(ConfirmChoice.Cancelled);
        vm.Dialogs = dialogs;
        vm.OpenAppointment(cancelled);

        await vm.Selected!.AskRestoreCommand.ExecuteAsync(null);

        Assert.Contains("nicht mehr gesendet", Assert.Single(dialogs.Asked).Message, StringComparison.Ordinal);
        // „Abbrechen“ lässt den Termin storniert.
        Assert.DoesNotContain(api.Calls, c => c.Route.EndsWith("/restore", StringComparison.Ordinal));
    }
}
