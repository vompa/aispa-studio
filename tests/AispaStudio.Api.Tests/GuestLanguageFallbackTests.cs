using AispaStudio.Api.Closures;
using AispaStudio.Api.Mail;
using AispaStudio.Api.MailIntake.Assistant;
using AispaStudio.Api.Online;
using AispaStudio.Api.Reminders;
using AispaStudio.Api.Scheduling;
using AispaStudio.Api.Tenancy;
using AispaStudio.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace AispaStudio.Api.Tests;

/// <summary>
/// Eine Gastsprache ohne Vorlage (z. B. Spanisch) fällt in jeder Gästenachricht auf dieselbe Sprache zurück, auf Deutsch (ADR 0073, Ä52): die zentrale Wahl
/// <see cref="GuestLanguage"/>, dann je Art der Nachricht ein Test mit einer unbekannten Sprache.
/// </summary>
public sealed class GuestLanguageFallbackTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // ---------- Die zentrale Wahl ----------

    [Theory]
    [InlineData(null, "de")]
    [InlineData("", "de")]
    [InlineData("  ", "de")]
    [InlineData("de", "de")]
    [InlineData("de-AT", "de")]
    [InlineData("EN", "en")]
    [InlineData("en-gb", "en")]
    [InlineData("fr-CH", "fr")]
    [InlineData("it", "it")]
    [InlineData("es", "de")]
    [InlineData("tr", "de")]
    [InlineData("x", "de")]
    public void Eine_Sprache_ohne_Vorlage_faellt_auf_Deutsch_zurueck(string? code, string expected)
    {
        Assert.Equal(expected, GuestLanguage.Normalize(code));
        Assert.Equal(expected, GuestLanguage.Resolve(new Customer { FirstName = "Eva", LastName = "Muster", Language = code }));
    }

    // ---------- Je Art der Nachricht ----------

    private static readonly DateTimeOffset Start = new(2027, 3, 1, 9, 0, 0, TimeSpan.Zero);

    private static readonly OnlineAppointment Appointment = new(Guid.NewGuid(), Guid.NewGuid(), "Spa Wien", "Europe/Vienna", "Massage", Start, Start.AddHours(1), 80m, "EUR",
        OnlineAppointmentState.Booked, Start.AddDays(-1), true, false, LocationContact.Empty);

    private static readonly GuestName Guest = new("Eva", "Frau Muster");

    private static void AssertSame(OutgoingMail expected, OutgoingMail actual)
    {
        Assert.Equal(expected.Subject, actual.Subject);
        Assert.Equal(expected.Text, actual.Text);
    }

    [Fact]
    public void Die_Mails_des_Kundenbereichs_gehen_bei_unbekannter_Sprache_auf_Deutsch()
    {
        var entry = new WaitlistEntryDto(Guid.NewGuid(), Appointment.LocationId, "Spa Wien", Guid.NewGuid(), "Massage", new DateOnly(2027, 3, 1), new DateOnly(2027, 3, 5),
            DayPart.Any, WaitlistStatus.Waiting, Start, null);

        AssertSame(OnlineMails.SignInCode("eva@example.at", "123456", "de"), OnlineMails.SignInCode("eva@example.at", "123456", "es"));
        AssertSame(OnlineMails.Confirmation("eva@example.at", Guest, Appointment, "https://x/t", "https://x/a", "de"),
            OnlineMails.Confirmation("eva@example.at", Guest, Appointment, "https://x/t", "https://x/a", "es"));
        AssertSame(OnlineMails.Cancelled("eva@example.at", Guest, Appointment, "de"), OnlineMails.Cancelled("eva@example.at", Guest, Appointment, "es"));
        AssertSame(OnlineMails.Moved("eva@example.at", Guest, Appointment, "https://x/t", "de"), OnlineMails.Moved("eva@example.at", Guest, Appointment, "https://x/t", "es"));
        AssertSame(OnlineMails.WaitlistJoined("eva@example.at", Guest, entry, "https://x/w", "de"), OnlineMails.WaitlistJoined("eva@example.at", Guest, entry, "https://x/w", "es"));
        AssertSame(OnlineMails.Released("eva@example.at", Guest, Appointment, "https://x/b", "de"), OnlineMails.Released("eva@example.at", Guest, Appointment, "https://x/b", "es"));
        AssertSame(OnlineMails.DepositReceived("eva@example.at", Guest, "Massage", Start, "Europe/Vienna", "Spa Wien", 30m, "EUR", 30, "Beleg", "de"),
            OnlineMails.DepositReceived("eva@example.at", Guest, "Massage", Start, "Europe/Vienna", "Spa Wien", 30m, "EUR", 30, "Beleg", "es"));
        AssertSame(OnlineMails.PaymentRefunded("eva@example.at", Guest, OnlinePaymentSource.Deposit, "Spa Wien", 30m, "EUR", "Beleg", "de"),
            OnlineMails.PaymentRefunded("eva@example.at", Guest, OnlinePaymentSource.Deposit, "Spa Wien", 30m, "EUR", "Beleg", "es"));
        Assert.Contains("Ihre Anzahlung", OnlineMails.PaymentRefunded("eva@example.at", Guest, OnlinePaymentSource.Deposit, "Spa Wien", 30m, "EUR", "Beleg", "es").Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Die_Absage_wegen_einer_Schliesszeit_geht_bei_unbekannter_Sprache_auf_Deutsch()
    {
        foreach (var kind in Enum.GetValues<ClosureKind>())
        {
            Assert.Equal(ClosureText.CancellationTemplate(kind, null, "PERIOD", "Spa Wien", "de"), ClosureText.CancellationTemplate(kind, null, "PERIOD", "Spa Wien", "es"));
        }

        Assert.Equal(ClosureText.RefundSentence(30m, "EUR", "de"), ClosureText.RefundSentence(30m, "EUR", "es"));
        Assert.Equal(ClosureText.RebookingSentence("https://x/b", "de"), ClosureText.RebookingSentence("https://x/b", "es"));
    }

    [Fact]
    public void Der_Hinweis_auf_die_KI_und_die_Antworten_der_Terminannahme_gehen_bei_unbekannter_Sprache_auf_Deutsch()
    {
        Assert.Equal(ReplyLanguages.Disclosure("de"), ReplyLanguages.Disclosure("es"));
        Assert.Equal(ReplyLanguages.ShortDisclosure("de"), ReplyLanguages.ShortDisclosure("tr"));
    }

    [Fact]
    public async Task Die_Antwort_der_Terminannahme_geht_bei_unbekannter_Sprache_auf_Deutsch()
    {
        var booked = new DateTime(2027, 3, 1, 10, 0, 0, DateTimeKind.Unspecified);
        var german = await new RuleBasedBookingAssistant().DraftReplyAsync(new ReplyRequest("de", ReplyCase.Booked, "Claire", "Termin", "Bitte", "Spa Wien", "Massage", null, booked, [], []), Ct);
        var spanish = await new RuleBasedBookingAssistant().DraftReplyAsync(new ReplyRequest("es", ReplyCase.Booked, "Claire", "Termin", "Bitte", "Spa Wien", "Massage", null, booked, [], []), Ct);

        Assert.Equal(german, spanish);
    }

    // ---------- Erinnerungen ----------

    [Fact]
    public async Task Die_Erinnerung_geht_an_einen_Gast_ohne_passende_Vorlage_auf_Deutsch_und_sonst_in_seiner_Sprache()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var spanish = await CustomerAsync(spa, "es");
        var english = await CustomerAsync(spa, "en");

        var forSpanish = await RemindAsync(spa, spanish, 10);
        var forEnglish = await RemindAsync(spa, english, 14);

        Assert.StartsWith("Erinnerung:", forSpanish.Subject, StringComparison.Ordinal);
        Assert.Contains("Bitte bestätigen Sie kurz, dass Sie kommen", forSpanish.Text, StringComparison.Ordinal);
        Assert.StartsWith("Reminder:", forEnglish.Subject, StringComparison.Ordinal);
    }

    private static async Task<CustomerDto> CustomerAsync(TestSpa spa, string language)
    {
        var suffix = Guid.NewGuid().ToString("N");
        return await spa.PostAsync<CustomerDto>("/api/customers",
            new CustomerInput("Lena", $"Sprache{suffix}", $"lena-{suffix}@example.at", null, null, null, Language: language), Ct);
    }

    private async Task<OutgoingMail> RemindAsync(TestSpa spa, CustomerDto customer, int hour)
    {
        var appointment = await TestSpa.ReadAsync<AppointmentDto>(await spa.BookAsync(TestSpa.At(hour), Ct, customer: customer.Id), Ct);
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            scope.ServiceProvider.GetRequiredService<TenantContext>().Use(spa.TenantId);
            Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<ReminderPlanner>().PlanAsync(appointment.Start.AddHours(-47), Ct));
        }

        return await fixture.Mailbox.WaitForAsync(customer.Email!, m => m.Subject.Contains("Massage", StringComparison.Ordinal), Ct);
    }
}
