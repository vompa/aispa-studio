using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using AispaStudio.Api.Data;
using AispaStudio.Api.Mail;
using AispaStudio.Api.Retention;
using AispaStudio.Api.Tenancy;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AispaStudio.Api.Tests;

/// <summary>Erinnerung an die nächste Behandlung: nur mit bestätigter Einwilligung (Double-Opt-in), Text von der KI, Abmeldelink.</summary>
public sealed partial class RetentionTests(ApiFixture fixture)
{
    private const int Days = 28;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Erinnert_erst_nach_bestaetigter_Einwilligung_mit_KI_Text_und_Abmeldelink()
    {
        var spa = await SpaWithIntervalAsync();
        var guest = await GuestAsync(spa, "Mara");
        var last = await CompletedAsync(spa, guest);

        Assert.Equal(0, await PlanAsync(spa, last.End.AddDays(Days + 1)));

        var pending = await spa.PostAsync<MarketingConsentDto>($"/api/customers/{guest.Id}/marketing-consent", new MarketingConsentRequest("de"), Ct);
        Assert.Equal(MarketingConsentState.Pending, pending.State);
        Assert.Equal(guest.Email, pending.Email);
        var request = await fixture.Mailbox.WaitForAsync(guest.Email!, m => m.Subject.StartsWith("Bitte bestätigen", StringComparison.Ordinal), Ct);
        var token = Token(request.Text);
        var again = await spa.Client.PostAsJsonAsync($"/api/customers/{guest.Id}/marketing-consent", new MarketingConsentRequest("de"), TestSpa.Json, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, again.StatusCode);

        // Der Link öffnet nur die Seite (ohne Spa in der Anfrage: der Link genügt); bestätigt wird mit dem Knopf.
        using var online = fixture.CreateClient();
        var page = await online.GetFromJsonAsync<OnlineConsentDto>($"/api/online/consent/{token}", TestSpa.Json, Ct);
        Assert.Equal(MarketingConsentState.Pending, page!.State);
        Assert.Equal("Mara", page.FirstName);
        Assert.Equal(0, await PlanAsync(spa, last.End.AddDays(Days + 1)));

        var confirmed = await ReadAsync<OnlineConsentDto>(await online.PostAsync($"/api/online/consent/{token}/confirm", null, Ct));
        Assert.Equal(MarketingConsentState.Confirmed, confirmed.State);
        Assert.NotNull((await spa.GetAsync<MarketingConsentDto>($"/api/customers/{guest.Id}/marketing-consent", Ct)).ConfirmedAt);

        Assert.Equal(0, await PlanAsync(spa, last.End.AddDays(Days - 1)));
        Assert.Equal(1, await PlanAsync(spa, last.End.AddDays(Days + 1)));
        Assert.Equal(0, await PlanAsync(spa, last.End.AddDays(Days + 2)));

        var mail = await fixture.Mailbox.WaitForAsync(guest.Email!, m => m.Subject.Contains("Zeit für Ihren nächsten Termin", StringComparison.Ordinal), Ct);
        Assert.StartsWith($"Guten Tag Mara {guest.LastName},", mail.Text, StringComparison.Ordinal);
        Assert.Contains("Ihre Massage tut bestimmt wieder gut.", mail.Text, StringComparison.Ordinal);
        Assert.Contains(RebookTexts.AiNoticeDe, mail.Text, StringComparison.Ordinal);
        Assert.Contains($"#/?standort={spa.Location.Id}&behandlung={spa.MassageTreatment.Id}", mail.Text, StringComparison.Ordinal);
        Assert.Contains("Abmelden: ", mail.Text, StringComparison.Ordinal);

        var sent = Assert.Single(await WaitForSentAsync(spa, 1));
        Assert.Equal("Sent", sent.State);
        Assert.True(sent.AiWritten);
        Assert.Equal("Massage", sent.TreatmentName);

        // Abmelden über den Link in der Erinnerung, ohne Anmeldung.
        var withdrawn = await ReadAsync<OnlineConsentDto>(await online.PostAsync($"/api/online/consent/{Token(mail.Text)}/withdraw", null, Ct));
        Assert.Equal(MarketingConsentState.Withdrawn, withdrawn.State);
        Assert.Equal(MarketingConsentState.Withdrawn, (await spa.GetAsync<MarketingConsentDto>($"/api/customers/{guest.Id}/marketing-consent", Ct)).State);
    }

    [Fact]
    public async Task Ohne_Termin_in_Aussicht_mit_derselben_Adresse_und_nur_kurz_nach_dem_Faelligwerden()
    {
        var spa = await SpaWithIntervalAsync();

        // Schon wieder gebucht: keine Erinnerung.
        var booked = await GuestAsync(spa, "Nora");
        var bookedLast = await CompletedAsync(spa, booked);
        await ConfirmAsync(spa, booked);
        await TestSpa.ReadAsync<AppointmentDto>(await spa.BookAsync(TestSpa.At(15, date: TestSpa.Monday.AddDays(Days + 7)), Ct, customer: booked.Id), Ct);
        Assert.Equal(0, await PlanAsync(spa, bookedLast.End.AddDays(Days + 1)));

        // Mailadresse geändert: die Einwilligung galt der alten.
        var moved = await GuestAsync(spa, "Olga");
        var movedLast = await CompletedAsync(spa, moved, hour: 12);
        await ConfirmAsync(spa, moved);
        await WithDbAsync(spa, db => db.Customers.Where(c => c.Id == moved.Id)
            .ExecuteUpdateAsync(u => u.SetProperty(c => c.Email, $"neu-{Guid.NewGuid():N}@example.at"), Ct));
        Assert.Equal(0, await PlanAsync(spa, movedLast.End.AddDays(Days + 1)));

        // Mehr als zwei Wochen nach dem Fälligwerden kommt nichts mehr nach.
        var late = await GuestAsync(spa, "Paula");
        var lateLast = await CompletedAsync(spa, late, hour: 14);
        await ConfirmAsync(spa, late);
        Assert.Equal(0, await PlanAsync(spa, lateLast.End.AddDays(Days + RebookPlanner.WindowDays + 1)));
    }

    [Fact]
    public async Task Text_der_KI_mit_Zahlen_wird_verworfen_und_der_feste_Text_geht_ohne_KI_Hinweis()
    {
        var spa = await SpaWithIntervalAsync();
        var firstName = $"Rabatt{Guid.NewGuid():N}"[..12];
        FakeRebookWriter.Answers[firstName] = "Nur diese Woche 20 % Rabatt auf Ihre nächste Massage!";
        var guest = await GuestAsync(spa, firstName);
        var last = await CompletedAsync(spa, guest);
        await ConfirmAsync(spa, guest);

        Assert.Equal(1, await PlanAsync(spa, last.End.AddDays(Days)));
        var mail = await fixture.Mailbox.WaitForAsync(guest.Email!, m => m.Subject.Contains("Zeit für Ihren nächsten Termin", StringComparison.Ordinal), Ct);
        Assert.Contains("ist schon eine Weile her", mail.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("Rabatt auf", mail.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(RebookTexts.AiNoticeDe, mail.Text, StringComparison.Ordinal);
        Assert.False(Assert.Single(await WaitForSentAsync(spa, 1)).AiWritten);

        var facts = new RebookFacts(new GuestName("Eva", "Eva"), "Massage 60", "Spa 1080", "de");
        Assert.False(RebookSender.Acceptable("Kurz.", facts));
        Assert.False(RebookSender.Acceptable("Besuchen Sie https://example.at für Ihre nächste Massage.", facts));
        Assert.False(RebookSender.Acceptable("Ihre Massage 60 wartet, jetzt um 10 Euro günstiger.", facts));
        Assert.True(RebookSender.Acceptable("Ihre letzte Massage 60 ist eine Weile her. Gönnen Sie sich bald wieder eine Auszeit im Spa 1080.", facts));
    }

    [Fact]
    public async Task Ein_alter_Link_bestaetigt_keine_neue_Adresse()
    {
        var spa = await SpaWithIntervalAsync();
        var guest = await GuestAsync(spa, "Sara");
        await spa.PostAsync<MarketingConsentDto>($"/api/customers/{guest.Id}/marketing-consent", new MarketingConsentRequest("en"), Ct);
        var old = Token((await fixture.Mailbox.WaitForAsync(guest.Email!, m => m.Subject.StartsWith("Please confirm", StringComparison.Ordinal), Ct)).Text);

        var email = $"sara-neu-{Guid.NewGuid():N}@example.at";
        await WithDbAsync(spa, db => db.Customers.Where(c => c.Id == guest.Id).ExecuteUpdateAsync(u => u.SetProperty(c => c.Email, email), Ct));
        Assert.Equal(email, (await spa.PostAsync<MarketingConsentDto>($"/api/customers/{guest.Id}/marketing-consent", new MarketingConsentRequest("en"), Ct)).Email);
        var current = Token((await fixture.Mailbox.WaitForAsync(email, m => m.Subject.StartsWith("Please confirm", StringComparison.Ordinal), Ct)).Text);

        using var online = fixture.CreateClient();
        Assert.Equal(MarketingConsentState.Pending,
            (await ReadAsync<OnlineConsentDto>(await online.PostAsync($"/api/online/consent/{old}/confirm", null, Ct))).State);
        Assert.Equal(MarketingConsentState.Confirmed,
            (await ReadAsync<OnlineConsentDto>(await online.PostAsync($"/api/online/consent/{current}/confirm", null, Ct))).State);
        Assert.Equal(HttpStatusCode.NotFound, (await online.GetAsync($"/api/online/consent/{new string('x', 43)}", Ct)).StatusCode);
    }

    [Fact]
    public async Task Einstellungen_brauchen_Rechte_und_gueltige_Angaben_und_bleiben_im_eigenen_Spa()
    {
        var spa = await SpaWithIntervalAsync();
        var other = await TestSpa.CreateAsync(fixture, Ct);
        var guest = await GuestAsync(spa, "Tina");

        Assert.Equal(HttpStatusCode.Unauthorized, (await fixture.CreateClient().GetAsync("/api/retention/settings", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await spa.Client.PutAsJsonAsync("/api/retention/settings",
            new RebookSettingsDto(true, [new RebookIntervalDto(spa.MassageTreatment.Id, 3)]), TestSpa.Json, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await spa.Client.PutAsJsonAsync("/api/retention/settings",
            new RebookSettingsDto(true, [new RebookIntervalDto(other.MassageTreatment.Id, 30)]), TestSpa.Json, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.Client.GetAsync($"/api/customers/{guest.Id}/marketing-consent", Ct)).StatusCode);
        Assert.Equal(MarketingConsentState.None, (await spa.GetAsync<MarketingConsentDto>($"/api/customers/{guest.Id}/marketing-consent", Ct)).State);

        var noMail = await spa.PostAsync<CustomerDto>("/api/customers", new CustomerInput("Uta", $"Ohne{Guid.NewGuid():N}", null, "+43 660 1", null, null), Ct);
        Assert.Equal(HttpStatusCode.BadRequest, (await spa.Client.PostAsJsonAsync($"/api/customers/{noMail.Id}/marketing-consent",
            new MarketingConsentRequest(null), TestSpa.Json, Ct)).StatusCode);

        // Ausgeschaltet: niemand wird erinnert, auch mit Einwilligung.
        var last = await CompletedAsync(spa, guest);
        await ConfirmAsync(spa, guest);
        var off = await spa.PutAsync<RebookSettingsDto>("/api/retention/settings",
            new RebookSettingsDto(false, [new RebookIntervalDto(spa.MassageTreatment.Id, 60)]), Ct);
        Assert.Equal(60, Assert.Single(off.Intervals).Days);
        Assert.Equal(0, await PlanAsync(spa, last.End.AddDays(61)));

        var withdrawn = await (await spa.Client.DeleteAsync($"/api/customers/{guest.Id}/marketing-consent", Ct)).Content
            .ReadFromJsonAsync<MarketingConsentDto>(TestSpa.Json, Ct);
        Assert.Equal(MarketingConsentState.Withdrawn, withdrawn!.State);
    }

    private async Task<TestSpa> SpaWithIntervalAsync()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var settings = await spa.PutAsync<RebookSettingsDto>("/api/retention/settings",
            new RebookSettingsDto(true, [new RebookIntervalDto(spa.MassageTreatment.Id, Days)]), Ct);
        Assert.True(settings.Enabled);
        return spa;
    }

    private static Task<CustomerDto> GuestAsync(TestSpa spa, string firstName)
    {
        var suffix = Guid.NewGuid().ToString("N");
        return spa.PostAsync<CustomerDto>("/api/customers",
            new CustomerInput(firstName, $"Gast{suffix}", $"{firstName.ToLowerInvariant()}-{suffix}@example.at", null, null, null), Ct);
    }

    private static async Task<AppointmentDto> CompletedAsync(TestSpa spa, CustomerDto guest, int hour = 10)
    {
        var appointment = await TestSpa.ReadAsync<AppointmentDto>(await spa.BookAsync(TestSpa.At(hour), Ct, customer: guest.Id), Ct);
        return await spa.SetStatusAsync(appointment.Id, AppointmentStatus.Completed, Ct);
    }

    private async Task ConfirmAsync(TestSpa spa, CustomerDto guest)
    {
        await spa.PostAsync<MarketingConsentDto>($"/api/customers/{guest.Id}/marketing-consent", new MarketingConsentRequest(null), Ct);
        var mail = await fixture.Mailbox.WaitForAsync(guest.Email!, m => m.Subject.StartsWith("Bitte bestätigen", StringComparison.Ordinal), Ct);
        using var online = fixture.CreateClient();
        Assert.Equal(MarketingConsentState.Confirmed,
            (await ReadAsync<OnlineConsentDto>(await online.PostAsync($"/api/online/consent/{Token(mail.Text)}/confirm", null, Ct))).State);
    }

    private async Task<int> PlanAsync(TestSpa spa, DateTimeOffset now)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Use(spa.TenantId);
        return await scope.ServiceProvider.GetRequiredService<RebookPlanner>().PlanAsync(now, Ct);
    }

    private async Task WithDbAsync(TestSpa spa, Func<AispaDbContext, Task> work)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Use(spa.TenantId);
        await work(scope.ServiceProvider.GetRequiredService<AispaDbContext>());
    }

    /// <summary>Versand läuft über die Queue: warten, bis so viele Erinnerungen nicht mehr in der Warteschlange stehen.</summary>
    private static async Task<List<RebookReminderDto>> WaitForSentAsync(TestSpa spa, int count)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));
        while (true)
        {
            var reminders = await spa.GetAsync<List<RebookReminderDto>>("/api/retention/reminders", timeout.Token);
            if (reminders.Count == count && reminders.All(r => r.State != "Queued"))
            {
                return reminders;
            }

            await Task.Delay(50, timeout.Token);
        }
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<T>(TestSpa.Json, Ct))!;
    }

    private static string Token(string text) => ConsentLink().Match(text) is { Success: true } m
        ? m.Groups["token"].Value
        : throw new InvalidOperationException($"Kein Link zur Einwilligung in: {text}");

    [GeneratedRegex("#/einwilligung/(?<token>[A-Za-z0-9_-]+)")]
    private static partial Regex ConsentLink();
}
