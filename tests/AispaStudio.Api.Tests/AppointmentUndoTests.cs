using System.Net;
using System.Net.Http.Json;
using AispaStudio.Api.Closures;
using AispaStudio.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace AispaStudio.Api.Tests;

/// <summary>
/// Rückgängig für Stornieren und Einchecken (Ä95, Ä97): eine Stornierung lässt sich zurücknehmen, solange Zeit, Person und Raum frei sind; das Einchecken, solange nicht kassiert ist.
/// Die Nachricht an den Gast aus Slice 73 wartet noch (dann geht sie nie raus) oder ist schon raus (dann gibt es auf Wunsch eine Korrektur).
/// </summary>
public sealed class AppointmentUndoTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly DateOnly Day = TestSpa.Monday.AddDays(7);

    private static string Path(AppointmentDto appointment, string tail) => $"/api/appointments/{appointment.Id}/{tail}";

    private static Task<AppointmentDto> CancelAsync(TestSpa spa, AppointmentDto appointment) =>
        spa.PostAsync<AppointmentDto>(Path(appointment, "cancel"), new CancelAppointmentRequest("Versehen"), Ct);

    private static Task<AppointmentDto> RestoreAsync(TestSpa spa, AppointmentDto appointment, RestoreAppointmentRequest? request = null) =>
        spa.PostAsync<AppointmentDto>(Path(appointment, "restore"), request ?? new RestoreAppointmentRequest(), Ct);

    private static Task<HttpResponseMessage> RestoreRawAsync(TestSpa spa, AppointmentDto appointment, RestoreAppointmentRequest? request = null) =>
        spa.Client.PostAsJsonAsync(Path(appointment, "restore"), request ?? new RestoreAppointmentRequest(), TestSpa.Json, Ct);

    private static Task<HttpResponseMessage> SetStatusRawAsync(TestSpa spa, AppointmentDto appointment, AppointmentStatus status) =>
        spa.Client.PostAsJsonAsync(Path(appointment, "status"), new AppointmentStatusChange(status), TestSpa.Json, Ct);

    [Fact]
    public async Task Given_ein_stornierter_Termin_When_die_Stornierung_zurueckgenommen_wird_Then_steht_er_wieder_auf_Gebucht_und_blockiert_die_Zeit()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var booked = await spa.BookOkAsync(TestSpa.At(10, 0, Day), Ct);
        var cancelled = await CancelAsync(spa, booked);
        Assert.Equal(AppointmentStatus.Cancelled, cancelled.Status);

        var restored = await RestoreAsync(spa, cancelled);

        Assert.Equal(AppointmentStatus.Booked, restored.Status);
        Assert.Equal((booked.Start, booked.EmployeeId, booked.RoomId), (restored.Start, restored.EmployeeId, restored.RoomId));
        // Die Zeit ist wieder belegt: dieselbe Person zur selben Zeit lässt sich nicht noch einmal buchen.
        var second = await spa.BookAsync(TestSpa.At(10, 0, Day), Ct, employee: booked.EmployeeId, room: booked.RoomId);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Given_ein_stornierter_Termin_dessen_Zeit_inzwischen_belegt_ist_When_zurueckgenommen_Then_nennt_die_Antwort_den_Konflikt_und_der_Termin_bleibt_storniert()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var booked = await spa.BookOkAsync(TestSpa.At(10, 0, Day), Ct);
        var cancelled = await CancelAsync(spa, booked);
        var other = await spa.BookOkAsync(TestSpa.At(10, 0, Day), Ct, employee: booked.EmployeeId, room: booked.RoomId);

        var response = await RestoreRawAsync(spa, cancelled);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var rejected = (await response.Content.ReadFromJsonAsync<BookingRejected>(TestSpa.Json, Ct))!;
        Assert.Contains(rejected.Conflicts, c => c.AppointmentId == other.Id);
        var again = await spa.GetAsync<AppointmentDto>($"/api/appointments/{cancelled.Id}", Ct);
        Assert.Equal(AppointmentStatus.Cancelled, again.Status);
    }

    [Fact]
    public async Task Given_ein_stornierter_Termin_mit_belegter_Zeit_When_mit_neuem_Beginn_zurueckgenommen_Then_steht_er_dort_auf_Gebucht()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var booked = await spa.BookOkAsync(TestSpa.At(10, 0, Day), Ct);
        var cancelled = await CancelAsync(spa, booked);
        await spa.BookOkAsync(TestSpa.At(10, 0, Day), Ct, employee: booked.EmployeeId, room: booked.RoomId);

        var restored = await RestoreAsync(spa, cancelled, new RestoreAppointmentRequest(TestSpa.At(15, 0, Day)));

        Assert.Equal(AppointmentStatus.Booked, restored.Status);
        Assert.Equal(TestSpa.At(15, 0, Day), restored.Start);
    }

    [Fact]
    public async Task Ein_Termin_der_nicht_storniert_ist_laesst_sich_nicht_zuruecknehmen()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var booked = await spa.BookOkAsync(TestSpa.At(10, 0, Day), Ct);

        Assert.Equal(HttpStatusCode.BadRequest, (await RestoreRawAsync(spa, booked)).StatusCode);
    }

    [Fact]
    public async Task Given_ein_eingecheckter_Termin_When_Booked_gesetzt_wird_Then_steht_er_wieder_auf_Gebucht()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var booked = await spa.BookOkAsync(TestSpa.At(10, 0, Day), Ct);
        await spa.PostAsync<AppointmentDto>(Path(booked, "status"), new AppointmentStatusChange(AppointmentStatus.CheckedIn), Ct);

        var back = await spa.PostAsync<AppointmentDto>(Path(booked, "status"), new AppointmentStatusChange(AppointmentStatus.Booked), Ct);

        Assert.Equal(AppointmentStatus.Booked, back.Status);
    }

    [Fact]
    public async Task Given_ein_erledigter_oder_kassierter_Termin_When_Booked_gesetzt_wird_Then_lehnt_die_API_ab()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var done = await spa.BookOkAsync(TestSpa.At(10, 0, Day), Ct);
        await spa.PostAsync<AppointmentDto>(Path(done, "status"), new AppointmentStatusChange(AppointmentStatus.Completed), Ct);
        Assert.Equal(HttpStatusCode.BadRequest, (await SetStatusRawAsync(spa, done, AppointmentStatus.Booked)).StatusCode);

        // Eingecheckt, aber schon auf einer Rechnung der Kassa: kassiert.
        var register = await spa.PostAsync<CashRegisterDto>("/api/pos/registers", new CashRegisterInput(
            spa.Location.Id, $"A-{Guid.NewGuid():N}"[..14], FiscalCountry.AT, "Test-Spa GmbH\nTeststraße 1, 1010 Wien\nATU12345678",
            "Test-Spa GmbH", "AT61 1904 3002 3457 3201", null), Ct);
        var billed = await spa.BookOkAsync(TestSpa.At(13, 0, Day), Ct);
        await spa.PostAsync<AppointmentDto>(Path(billed, "status"), new AppointmentStatusChange(AppointmentStatus.CheckedIn), Ct);
        await spa.PostAsync<BillDto>("/api/pos/bills", new BillInput(register.Id, null, [billed.Id]), Ct);

        Assert.Equal(HttpStatusCode.BadRequest, (await SetStatusRawAsync(spa, billed, AppointmentStatus.Booked)).StatusCode);

        // Der Kalender des Tages nennt den kassierten Termin, damit die App den Eintrag gar nicht erst anbietet.
        var day = await spa.GetAsync<CalendarDay>($"/api/locations/{spa.Location.Id}/calendar?date={Day:yyyy-MM-dd}", Ct);
        Assert.Equal([billed.Id], day.BilledAppointmentIds);
    }

    [Fact]
    public async Task Ein_gebuchter_oder_nicht_erschienener_Termin_laesst_sich_nicht_auf_Gebucht_setzen()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var booked = await spa.BookOkAsync(TestSpa.At(10, 0, Day), Ct);
        var noShow = await spa.BookOkAsync(TestSpa.At(13, 0, Day), Ct);
        await spa.PostAsync<AppointmentDto>(Path(noShow, "status"), new AppointmentStatusChange(AppointmentStatus.NoShow), Ct);

        Assert.Equal(HttpStatusCode.BadRequest, (await SetStatusRawAsync(spa, booked, AppointmentStatus.Booked)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await SetStatusRawAsync(spa, noShow, AppointmentStatus.Booked)).StatusCode);
    }

    // ---------- Nachricht an den Gast (Slice 73) ----------

    private static CancelClosureAppointmentsRequest Request(AppointmentDto appointment) =>
        new([appointment.Id], "Absage: {Behandlung}", "Guten Tag {Anrede},\n\nleider fällt Ihr Termin ({Behandlung}, {Zeit}) aus.\n\nHerzlich, das Team", false, false);

    private async Task<(TestSpa Spa, CustomerDto Guest, AppointmentDto Cancelled, ClosureDto Closure)> ClosureCancelledAsync(int delayMinutes)
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await spa.PutAsync<GuestMessageDelayDto>($"/api/locations/{spa.Location.Id}/guest-message-delay", new GuestMessageDelayDto(delayMinutes), Ct);
        var suffix = Guid.NewGuid().ToString("N");
        var guest = await spa.PostAsync<CustomerDto>("/api/customers", new CustomerInput("Ida", $"Mail{suffix}", $"ida-{suffix}@example.at", null, null, null), Ct);
        var appointment = await spa.BookOkAsync(TestSpa.At(9, 30, Day), Ct, customer: guest.Id);
        var closure = await ClosureTests.CreateAsync(spa, ClosureTests.Input(Day, Day));
        await spa.PostAsync<CancelClosureAppointmentsResult>($"{ClosureTests.Path(spa)}/{closure.Id}/cancellation", Request(appointment), Ct);
        return (spa, guest, appointment with { Status = AppointmentStatus.Cancelled }, closure);
    }

    private static async Task<List<ClosureNoticeDto>> NoticesAsync(TestSpa spa, ClosureDto closure) =>
        await spa.GetAsync<List<ClosureNoticeDto>>($"{ClosureTests.Path(spa)}/{closure.Id}/notices", Ct);

    /// <summary>Die Schließzeit liegt über dem Tag des Termins, also lässt sich nur an einem anderen Tag zurücknehmen.</summary>
    private static RestoreAppointmentRequest ElsewhereRequest(bool correction = false) => new(TestSpa.At(11, 0, Day.AddDays(7)), correction);

    [Fact]
    public async Task Given_die_Nachricht_wartet_noch_When_die_Absage_zurueckgenommen_wird_Then_geht_sie_nie_raus()
    {
        var (spa, guest, cancelled, closure) = await ClosureCancelledAsync(10);
        var notice = Assert.Single(await NoticesAsync(spa, closure));
        Assert.Equal(ClosureNoticeState.Queued, notice.State);
        var info = await spa.GetAsync<AppointmentRestoration>(Path(cancelled, "restoration"), Ct);
        Assert.Equal(CancellationNoticeState.Waiting, info.Notice);

        var restored = await RestoreAsync(spa, cancelled, ElsewhereRequest());

        Assert.Equal(AppointmentStatus.Booked, restored.Status);
        Assert.Equal(ClosureNoticeState.Cancelled, Assert.Single(await NoticesAsync(spa, closure)).State);
        await SendClosureNoticeHandler.Handle(new SendClosureNotice(notice.Id), fixture.Factory.Services.GetRequiredService<IServiceScopeFactory>(), Ct);
        Assert.Empty(fixture.Mailbox.To(guest.Email!));
    }

    [Fact]
    public async Task Given_die_Nachricht_ist_schon_raus_When_mit_Korrektur_zurueckgenommen_Then_bekommt_der_Gast_eine_Korrekturmail()
    {
        var (spa, guest, cancelled, closure) = await ClosureCancelledAsync(0);
        await fixture.Mailbox.WaitForAsync(guest.Email!, m => m.Subject.StartsWith("Absage", StringComparison.Ordinal), Ct);
        var info = await spa.GetAsync<AppointmentRestoration>(Path(cancelled, "restoration"), Ct);
        Assert.Equal(CancellationNoticeState.Sent, info.Notice);

        await RestoreAsync(spa, cancelled, ElsewhereRequest(correction: true));

        var correction = await fixture.Mailbox.WaitForAsync(guest.Email!, m => m.Subject.StartsWith("Ihr Termin", StringComparison.Ordinal), Ct);
        Assert.Contains("Irrtum", correction.Text, StringComparison.Ordinal);
        Assert.Equal(2, (await NoticesAsync(spa, closure)).Count);
    }

    [Fact]
    public async Task Given_die_Nachricht_ist_schon_raus_When_ohne_Korrektur_zurueckgenommen_Then_geht_keine_weitere_Nachricht_raus()
    {
        var (spa, guest, cancelled, closure) = await ClosureCancelledAsync(0);
        await fixture.Mailbox.WaitForAsync(guest.Email!, m => m.Subject.StartsWith("Absage", StringComparison.Ordinal), Ct);

        await RestoreAsync(spa, cancelled, ElsewhereRequest(correction: false));
        await Task.Delay(500, Ct);

        Assert.Single(await NoticesAsync(spa, closure));
        Assert.Single(fixture.Mailbox.To(guest.Email!));
    }

    [Fact]
    public async Task Given_ein_von_Hand_stornierter_Termin_When_Restoration_gefragt_Then_gibt_es_keine_Nachricht()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var cancelled = await CancelAsync(spa, await spa.BookOkAsync(TestSpa.At(10, 0, Day), Ct));

        var info = await spa.GetAsync<AppointmentRestoration>(Path(cancelled, "restoration"), Ct);

        Assert.Equal(new AppointmentRestoration(CancellationNoticeState.None, false), info);
    }
}
