using System.Net;
using AispaStudio.Api.Reminders;
using AispaStudio.Api.Retention;
using AispaStudio.Api.Tenancy;
using AispaStudio.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace AispaStudio.Api.Tests;

/// <summary>P8 (17.11.2026): Terminerinnerungen und Wiederbuchungs-Hinweise in einem Versandprotokoll mit Filtern, Zusammenfassung und Export.</summary>
public sealed class DispatchLogTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>Ein Spa mit einer verschickten Terminerinnerung und einem verschickten Wiederbuchungs-Hinweis desselben Gastes.</summary>
    private async Task<(TestSpa Spa, CustomerDto Guest)> SpaWithBothAsync()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var suffix = Guid.NewGuid().ToString("N");
        var guest = await spa.PostAsync<CustomerDto>("/api/customers",
            new CustomerInput("Mira", $"Protokoll{suffix}", $"mira-{suffix}@example.at", "+43 660 1234567", null, null), Ct);

        var upcoming = await TestSpa.ReadAsync<AppointmentDto>(await spa.BookAsync(TestSpa.At(14, 0, TestSpa.Monday.AddDays(7)), Ct, customer: guest.Id), Ct);
        var done = await TestSpa.ReadAsync<AppointmentDto>(await spa.BookAsync(TestSpa.At(10), Ct, customer: guest.Id), Ct);
        done = await spa.SetStatusAsync(done.Id, AppointmentStatus.Completed, Ct);
        await spa.PutAsync<MarketingConsentDto>($"/api/customers/{guest.Id}/marketing-consent", new MarketingConsentDecision(MarketingConsentState.Confirmed), Ct);
        await spa.PutAsync<RebookSettingsDto>("/api/retention/settings",
            new RebookSettingsDto(true, []) { DefaultDays = 28, CategoryIntervals = [], SilentTreatmentIds = [] }, Ct);

        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Use(spa.TenantId);
        Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<ReminderPlanner>().PlanAsync(upcoming.Start.AddHours(-47), Ct));
        await WaitUntilSentAsync(spa, 1);
        // Der Versand des Hinweises prüft, ob der Gast schon wieder einen Termin hat: den von oben sagt er ab, erst dann plant der zweite Lauf.
        await spa.PostAsync<AppointmentDto>($"/api/appointments/{upcoming.Id}/cancel", new CancelAppointmentRequest("Gast krank"), Ct);
        Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<RebookPlanner>().PlanAsync(done.End.AddDays(29), Ct));
        await WaitUntilSentAsync(spa, 2);
        return (spa, guest);
    }

    private static async Task WaitUntilSentAsync(TestSpa spa, int count)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));
        while (true)
        {
            var all = await spa.GetAsync<List<DispatchLogEntryDto>>("/api/dispatch-log?days=366", timeout.Token);
            if (all.Count == count && all.All(e => e.State != "Queued"))
            {
                return;
            }

            await Task.Delay(50, timeout.Token);
        }
    }

    [Fact]
    public async Task Das_Protokoll_fuehrt_Erinnerung_und_Wiederbuchung_zusammen_neueste_zuerst()
    {
        var (spa, guest) = await SpaWithBothAsync();

        var entries = await spa.GetAsync<List<DispatchLogEntryDto>>("/api/dispatch-log", Ct);

        Assert.Equal(2, entries.Count);
        Assert.Equal(entries.OrderByDescending(e => e.At), entries);
        var reminder = Assert.Single(entries, e => e.Kind == DispatchKind.Reminder);
        Assert.Equal(guest.Id, reminder.CustomerId);
        Assert.Contains("Protokoll", reminder.Recipient, StringComparison.Ordinal);
        Assert.Equal("Massage", reminder.Subject);
        Assert.Equal(ReminderChannel.Email, reminder.Channel);
        Assert.True(reminder.State == "Sent", reminder.Error);
        Assert.NotNull(reminder.AppointmentStart);
        var rebook = Assert.Single(entries, e => e.Kind == DispatchKind.Rebook);
        Assert.Equal("Sent", rebook.State);
        Assert.False(string.IsNullOrWhiteSpace(rebook.Text));
        Assert.Null(rebook.AppointmentStart);
    }

    [Fact]
    public async Task Filter_nach_Art_Kanal_und_Ergebnis_und_unbekannte_Werte_werden_abgelehnt()
    {
        var (spa, _) = await SpaWithBothAsync();

        Assert.Equal(DispatchKind.Rebook, Assert.Single(await spa.GetAsync<List<DispatchLogEntryDto>>("/api/dispatch-log?kind=rebook", Ct)).Kind);
        Assert.Equal(DispatchKind.Reminder, Assert.Single(await spa.GetAsync<List<DispatchLogEntryDto>>("/api/dispatch-log?kind=Reminder", Ct)).Kind);
        Assert.Empty(await spa.GetAsync<List<DispatchLogEntryDto>>("/api/dispatch-log?result=failed", Ct));
        Assert.Equal(2, (await spa.GetAsync<List<DispatchLogEntryDto>>("/api/dispatch-log?result=sent", Ct)).Count);
        Assert.Empty(await spa.GetAsync<List<DispatchLogEntryDto>>("/api/dispatch-log?channel=sms", Ct));
        Assert.Equal(2, (await spa.GetAsync<List<DispatchLogEntryDto>>("/api/dispatch-log?channel=email", Ct)).Count);
        Assert.Single(await spa.GetAsync<List<DispatchLogEntryDto>>("/api/dispatch-log?limit=1", Ct));
        Assert.Equal(HttpStatusCode.BadRequest, (await spa.Client.GetAsync("/api/dispatch-log?kind=post", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await spa.Client.GetAsync("/api/dispatch-log?days=0", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await spa.Client.GetAsync("/api/dispatch-log?result=lost", Ct)).StatusCode);
    }

    [Fact]
    public async Task Die_Zusammenfassung_zaehlt_je_Art_und_ein_anderes_Spa_sieht_nichts()
    {
        var (spa, _) = await SpaWithBothAsync();
        var other = await TestSpa.CreateAsync(fixture, Ct);

        var summary = await spa.GetAsync<DispatchSummaryDto>("/api/dispatch-log/summary", Ct);

        Assert.Equal(30, summary.Days);
        Assert.Equal(new DispatchKindSummaryDto(1, 0, 0), summary.Reminders);
        Assert.Equal(new DispatchKindSummaryDto(1, 0, 0), summary.Rebook);
        var empty = await other.GetAsync<DispatchSummaryDto>("/api/dispatch-log/summary", Ct);
        Assert.Equal(new DispatchKindSummaryDto(0, 0, 0), empty.Reminders);
        Assert.Empty(await other.GetAsync<List<DispatchLogEntryDto>>("/api/dispatch-log", Ct));
        Assert.Equal(HttpStatusCode.Unauthorized, (await fixture.CreateClient().GetAsync("/api/dispatch-log", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await fixture.CreateClient().GetAsync("/api/dispatch-log/export?format=csv", Ct)).StatusCode);
    }

    [Fact]
    public async Task Der_Export_liefert_Excel_oder_CSV_mit_denselben_Filtern()
    {
        var (spa, _) = await SpaWithBothAsync();

        var csv = await spa.Client.GetAsync("/api/dispatch-log/export?format=csv&kind=rebook", Ct);
        Assert.Equal(HttpStatusCode.OK, csv.StatusCode);
        Assert.StartsWith("text/csv", csv.Content.Headers.ContentType!.ToString(), StringComparison.Ordinal);
        var text = await csv.Content.ReadAsStringAsync(Ct);
        Assert.Contains("Wiederbuchung", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Terminerinnerung", text, StringComparison.Ordinal);
        Assert.Contains("Gesendet", text, StringComparison.Ordinal);

        var xlsx = await spa.Client.GetAsync("/api/dispatch-log/export?format=xlsx", Ct);
        Assert.Equal(HttpStatusCode.OK, xlsx.StatusCode);
        Assert.Contains("spreadsheetml", xlsx.Content.Headers.ContentType!.ToString(), StringComparison.Ordinal);
        Assert.StartsWith("Versandprotokoll", xlsx.Content.Headers.ContentDisposition!.FileNameStar ?? xlsx.Content.Headers.ContentDisposition.FileName, StringComparison.Ordinal);

        Assert.Equal(HttpStatusCode.BadRequest, (await spa.Client.GetAsync("/api/dispatch-log/export?format=pdf", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await spa.Client.GetAsync("/api/dispatch-log/export?format=csv&kind=post", Ct)).StatusCode);
    }

    [Fact]
    public async Task Eine_gebuendelte_Erinnerung_steht_nicht_einzeln_im_Protokoll()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var suffix = Guid.NewGuid().ToString("N");
        var guest = await spa.PostAsync<CustomerDto>("/api/customers",
            new CustomerInput("Nele", $"Gebuendelt{suffix}", $"nele-{suffix}@example.at", null, null, null), Ct);
        var first = await TestSpa.ReadAsync<AppointmentDto>(await spa.BookAsync(TestSpa.At(10), Ct, customer: guest.Id), Ct);
        await TestSpa.ReadAsync<AppointmentDto>(await spa.BookAsync(TestSpa.At(15), Ct, customer: guest.Id), Ct);

        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Use(spa.TenantId);
        Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<ReminderPlanner>().PlanAsync(first.Start.AddHours(-47), Ct));

        Assert.Single(await spa.GetAsync<List<DispatchLogEntryDto>>("/api/dispatch-log", Ct));
        var summary = await spa.GetAsync<DispatchSummaryDto>("/api/dispatch-log/summary", Ct);
        Assert.Equal(0, summary.Reminders.Skipped);
    }
}
