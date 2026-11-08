using System.Net;
using System.Net.Http.Json;
using AispaStudio.Api.Closures;
using AispaStudio.Api.Data;
using AispaStudio.Api.Tenancy;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AispaStudio.Api.Tests;

/// <summary>
/// Wartezeit vor der Nachricht an den Gast (Ä95, ADR 0081): sagt das Team ab, geht die Nachricht erst nach der Wartezeit des Standorts raus (Standard 10 Minuten),
/// damit ein Versehen noch zurückgenommen werden kann. Beginnt der Termin in weniger als 2 Stunden, geht sie sofort. „Jetzt senden“ schickt sie gleich, „Nicht senden“ nie.
/// </summary>
public sealed class ClosureGuestDelayTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly DateOnly Day = TestSpa.Monday.AddDays(7);

    private static string DelayPath(TestSpa spa) => $"/api/locations/{spa.Location.Id}/guest-message-delay";

    private static string NoticePath(TestSpa spa, ClosureDto closure, ClosureNoticeDto notice, string tail) =>
        $"{ClosureTests.Path(spa)}/{closure.Id}/notices/{notice.Id}/{tail}";

    private static Task<GuestMessageDelayDto> SetDelayAsync(TestSpa spa, int minutes) => spa.PutAsync<GuestMessageDelayDto>(DelayPath(spa), new GuestMessageDelayDto(minutes), Ct);

    private static CancelClosureAppointmentsRequest Request(AppointmentDto appointment) =>
        new([appointment.Id], "Absage: {Behandlung}", "Guten Tag {Anrede},\n\nleider fällt Ihr Termin ({Behandlung}, {Zeit}) aus.\n\nHerzlich, das Team", false, false);

    private static async Task<List<ClosureNoticeDto>> NoticesAsync(TestSpa spa, ClosureDto closure) =>
        await spa.GetAsync<List<ClosureNoticeDto>>($"{ClosureTests.Path(spa)}/{closure.Id}/notices", Ct);

    private async Task<(TestSpa Spa, CustomerDto Guest, AppointmentDto Appointment, ClosureDto Closure)> SetupAsync(int delayMinutes, DateTimeOffset? start = null)
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await SetDelayAsync(spa, delayMinutes);
        var suffix = Guid.NewGuid().ToString("N");
        var guest = await spa.PostAsync<CustomerDto>("/api/customers", new CustomerInput("Ida", $"Mail{suffix}", $"ida-{suffix}@example.at", null, null, null), Ct);
        var appointment = await spa.BookOkAsync(TestSpa.At(9, 30, Day), Ct, customer: guest.Id);
        ClosureDto closure;
        if (start is { } soon)
        {
            await MoveAsync(spa, appointment.Id, soon);
            var today = DateOnly.FromDateTime(soon.UtcDateTime);
            closure = await ClosureTests.CreateAsync(spa, ClosureTests.Input(today.AddDays(-1), today.AddDays(1)));
        }
        else
        {
            closure = await ClosureTests.CreateAsync(spa, ClosureTests.Input(Day, Day));
        }

        return (spa, guest, appointment, closure);
    }

    /// <summary>Die Buchung lässt in der Gegenwart keine Termine zu; der Test setzt den Beginn in der Datenbank.</summary>
    private async Task MoveAsync(TestSpa spa, Guid appointmentId, DateTimeOffset start)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Use(spa.TenantId);
        var db = scope.ServiceProvider.GetRequiredService<AispaDbContext>();
        var appointment = await db.Appointments.FirstAsync(a => a.Id == appointmentId, Ct);
        var length = appointment.End - appointment.Start;
        db.Entry(appointment).Property(a => a.Start).CurrentValue = start;
        db.Entry(appointment).Property(a => a.End).CurrentValue = start + length;
        await db.SaveChangesAsync(Ct);
    }

    private static async Task WaitUntilSentAsync(TestSpa spa, ClosureDto closure)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));
        while ((await NoticesAsync(spa, closure)).Any(n => n.State == ClosureNoticeState.Queued))
        {
            await Task.Delay(50, timeout.Token);
        }
    }

    [Fact]
    public async Task Given_Wartezeit_10_Minuten_When_das_Team_absagt_Then_ist_die_Mail_fuer_10_Minuten_spaeter_geplant_und_noch_nicht_gesendet()
    {
        var (spa, guest, appointment, closure) = await SetupAsync(10);
        var before = DateTimeOffset.UtcNow;

        await spa.PostAsync<CancelClosureAppointmentsResult>($"{ClosureTests.Path(spa)}/{closure.Id}/cancellation", Request(appointment), Ct);
        await Task.Delay(1000, Ct);

        var notice = Assert.Single(await NoticesAsync(spa, closure));
        Assert.Equal(ClosureNoticeState.Queued, notice.State);
        Assert.NotNull(notice.ScheduledFor);
        Assert.InRange(notice.ScheduledFor.Value, before.AddMinutes(10), DateTimeOffset.UtcNow.AddMinutes(10));
        Assert.Empty(fixture.Mailbox.To(guest.Email!));
    }

    [Fact]
    public async Task Given_ein_Termin_in_90_Minuten_When_er_abgesagt_wird_Then_geht_die_Nachricht_sofort_raus()
    {
        var (spa, guest, appointment, closure) = await SetupAsync(10, DateTimeOffset.UtcNow.AddMinutes(90));

        await spa.PostAsync<CancelClosureAppointmentsResult>($"{ClosureTests.Path(spa)}/{closure.Id}/cancellation", Request(appointment), Ct);
        await WaitUntilSentAsync(spa, closure);

        var notice = Assert.Single(await NoticesAsync(spa, closure));
        Assert.Equal(ClosureNoticeState.Sent, notice.State);
        Assert.Single(fixture.Mailbox.To(guest.Email!));
    }

    [Fact]
    public async Task Given_eine_geplante_Nachricht_When_Nicht_senden_gedrueckt_wird_Then_geht_sie_nie_raus()
    {
        var (spa, guest, appointment, closure) = await SetupAsync(10);
        await spa.PostAsync<CancelClosureAppointmentsResult>($"{ClosureTests.Path(spa)}/{closure.Id}/cancellation", Request(appointment), Ct);
        var notice = Assert.Single(await NoticesAsync(spa, closure));

        var cancelled = await spa.PostAsync<ClosureNoticeDto>(NoticePath(spa, closure, notice, "cancel"), new { }, Ct);

        Assert.Equal(ClosureNoticeState.Cancelled, cancelled.State);
        // Die geplante Queue-Nachricht trifft später trotzdem ein: sie darf nichts mehr senden.
        await SendClosureNoticeHandler.Handle(new SendClosureNotice(notice.Id), fixture.Factory.Services.GetRequiredService<IServiceScopeFactory>(), Ct);
        Assert.Empty(fixture.Mailbox.To(guest.Email!));
        Assert.Equal(ClosureNoticeState.Cancelled, Assert.Single(await NoticesAsync(spa, closure)).State);
    }

    [Fact]
    public async Task Jetzt_senden_schickt_die_geplante_Nachricht_gleich_und_nur_einmal()
    {
        var (spa, guest, appointment, closure) = await SetupAsync(10);
        await spa.PostAsync<CancelClosureAppointmentsResult>($"{ClosureTests.Path(spa)}/{closure.Id}/cancellation", Request(appointment), Ct);
        var notice = Assert.Single(await NoticesAsync(spa, closure));

        await spa.PostAsync<ClosureNoticeDto>(NoticePath(spa, closure, notice, "send-now"), new { }, Ct);
        await WaitUntilSentAsync(spa, closure);

        Assert.Single(fixture.Mailbox.To(guest.Email!));
        Assert.Equal(ClosureNoticeState.Sent, Assert.Single(await NoticesAsync(spa, closure)).State);
        // Eine gesendete Nachricht lässt sich weder zurücknehmen noch noch einmal senden.
        Assert.Equal(HttpStatusCode.Conflict, (await spa.Client.PostAsJsonAsync(NoticePath(spa, closure, notice, "cancel"), new { }, TestSpa.Json, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await spa.Client.PostAsJsonAsync(NoticePath(spa, closure, notice, "send-now"), new { }, TestSpa.Json, Ct)).StatusCode);
    }

    [Fact]
    public async Task Ohne_Wartezeit_geht_die_Nachricht_sofort_raus()
    {
        var (spa, guest, appointment, closure) = await SetupAsync(0);

        await spa.PostAsync<CancelClosureAppointmentsResult>($"{ClosureTests.Path(spa)}/{closure.Id}/cancellation", Request(appointment), Ct);
        await WaitUntilSentAsync(spa, closure);

        Assert.Single(fixture.Mailbox.To(guest.Email!));
    }

    [Fact]
    public async Task Die_Wartezeit_ist_je_Standort_einstellbar_von_0_bis_60_Minuten_und_steht_zunaechst_auf_10()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        // Der Testmandant stellt sie auf 0; ein neuer Standort startet mit dem Standard.
        var other = await spa.CreateLocationAsync("Graz", [DayOfWeek.Monday], Ct);
        Assert.Equal(10, (await spa.GetAsync<GuestMessageDelayDto>($"/api/locations/{other.Id}/guest-message-delay", Ct)).Minutes);

        Assert.Equal(60, (await spa.PutAsync<GuestMessageDelayDto>(DelayPath(spa), new GuestMessageDelayDto(60), Ct)).Minutes);
        Assert.Equal(60, (await spa.GetAsync<GuestMessageDelayDto>(DelayPath(spa), Ct)).Minutes);
        Assert.Equal(10, (await spa.GetAsync<GuestMessageDelayDto>($"/api/locations/{other.Id}/guest-message-delay", Ct)).Minutes);
        foreach (var invalid in new[] { -1, 61 })
        {
            Assert.Equal(HttpStatusCode.BadRequest, (await spa.Client.PutAsJsonAsync(DelayPath(spa), new GuestMessageDelayDto(invalid), TestSpa.Json, Ct)).StatusCode);
        }
    }

    [Theory]
    [InlineData(10, 119, 0)]
    [InlineData(10, 121, 10)]
    [InlineData(10, 120, 10)]
    [InlineData(0, 600, 0)]
    [InlineData(45, 5000, 45)]
    public void Die_Regel_sendet_sofort_ohne_Wartezeit_oder_bei_Beginn_in_weniger_als_2_Stunden(int delay, int minutesUntilStart, int expectedWait)
    {
        var now = new DateTimeOffset(2026, 10, 26, 12, 0, 0, TimeSpan.Zero);

        Assert.Equal(now.AddMinutes(expectedWait), GuestMessageTiming.SendAt(now, now.AddMinutes(minutesUntilStart), delay));
    }

    [Fact]
    public void Ohne_Termin_wie_bei_der_Warteliste_gilt_nur_die_Wartezeit()
    {
        var now = new DateTimeOffset(2026, 10, 26, 12, 0, 0, TimeSpan.Zero);

        Assert.Equal(now.AddMinutes(10), GuestMessageTiming.SendAt(now, null, 10));
    }
}
