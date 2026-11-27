using System.Net;
using System.Net.Http.Json;
using AispaStudio.Api.Reporting;
using AispaStudio.Contracts;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace AispaStudio.Api.Tests;

/// <summary>Wochen- und Monatsbericht per Mail: einmal je Zeitraum, an die Leitung, abbestellbar, Beträge nur mit Recht.</summary>
public sealed class ReportMailTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>Montag nach der Woche der Testtermine (1. bis 7. März 2027), 8 Uhr in Wien.</summary>
    private static readonly DateTimeOffset NextMonday = new(2027, 3, 8, 7, 0, 0, TimeSpan.Zero);

    private ReportMailScheduler Scheduler => fixture.Factory.Services.GetRequiredService<ReportMailScheduler>();

    private static async Task<string> EmailAsync(HttpClient client) => (await client.GetFromJsonAsync<CurrentUser>("/api/auth/me", TestSpa.Json, Ct))!.Email;

    [Fact]
    public async Task Wochenbericht_geht_montags_einmal_an_die_Leitung_mit_KI_Kommentar()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await spa.BookOkAsync(TestSpa.At(10), Ct, spa.Anna.Id);
        var owner = await EmailAsync(spa.Client);
        using var reception = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Reception, [spa.Location.Id]);
        var receptionMail = await EmailAsync(reception);

        await Scheduler.RunOnceAsync(NextMonday.AddHours(-2), Ct);
        Assert.Empty(fixture.Mailbox.To(owner));

        await Scheduler.RunOnceAsync(NextMonday, Ct);
        await Scheduler.RunOnceAsync(NextMonday.AddHours(1), Ct);

        var mail = Assert.Single(fixture.Mailbox.To(owner));
        Assert.Equal($"Wochenbericht {spa.Location.Name}: 1. März bis 7. März 2027", mail.Subject);
        Assert.Contains("Kennzahlen:", mail.Text, StringComparison.Ordinal);
        Assert.Contains("- Umsatz brutto: ", mail.Text, StringComparison.Ordinal);
        Assert.Contains("Kommentar der KI:", mail.Text, StringComparison.Ordinal);
        Assert.Contains("Abbestellen:", mail.Text, StringComparison.Ordinal);
        var pdf = Assert.Single(mail.Attachments ?? []);
        Assert.Equal("application/pdf", pdf.ContentType);
        Assert.Equal("%PDF"u8.ToArray(), pdf.Content[..4]);
        Assert.Empty(fixture.Mailbox.To(receptionMail));
    }

    [Fact]
    public async Task Abbestellt_kommt_nichts_und_die_Rezeption_kann_ihn_nicht_bestellen()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var owner = await EmailAsync(spa.Client);
        Assert.Equal(new ReportMailSubscription(true, true), await spa.GetAsync<ReportMailSubscription>("/api/reports/mail-subscription", Ct));

        var off = await spa.PutAsync<ReportMailSubscription>("/api/reports/mail-subscription", new ReportMailSubscriptionInput(false), Ct);
        await Scheduler.RunOnceAsync(NextMonday.AddDays(7), Ct);

        Assert.False(off.Enabled);
        Assert.Empty(fixture.Mailbox.To(owner));
        using var manager = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Manager, [spa.Location.Id]);
        var refused = await manager.PutAsJsonAsync("/api/reports/mail-subscription", new ReportMailSubscriptionInput(true), TestSpa.Json, Ct);
        Assert.Equal(HttpStatusCode.OK, refused.StatusCode);
        using var reception = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Reception, [spa.Location.Id]);
        Assert.Equal(HttpStatusCode.Forbidden, (await reception.GetAsync("/api/reports/mail-subscription", Ct)).StatusCode);
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("smtp.spa-am-see.at", true)]
    public async Task Im_Einzelplatz_gibt_es_den_Bericht_per_Mail_nur_mit_eingerichtetem_Mailversand(string? smtpHost, bool offered)
    {
        // T-110: Ohne Mail:Smtp:Host gingen die Mails im Einzelplatz nur ins Log; der Haken fehlt dort. Mit Server gilt die Rolle allein (Test oben).
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await using var host = fixture.Factory.WithWebHostBuilder(b =>
        {
            b.UseSetting("Hosting:Standalone", "true");
            if (smtpHost is not null)
            {
                b.UseSetting("Mail:Smtp:Host", smtpHost);
            }
        });
        using var client = host.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        client.DefaultRequestHeaders.Add("Cookie", spa.Cookie);
        client.DefaultRequestHeaders.Add("X-Aispa-Spa", spa.TenantId.ToString());

        var subscription = await client.GetFromJsonAsync<ReportMailSubscription>("/api/reports/mail-subscription", TestSpa.Json, Ct);

        Assert.Equal(new ReportMailSubscription(offered, offered), subscription);
    }

    [Fact]
    public void Ohne_Recht_auf_Betraege_stehen_keine_Betraege_in_der_Mail()
    {
        var report = new ManagementReportDto(Guid.NewGuid(), new DateOnly(2027, 3, 1), new DateOnly(2027, 3, 7),
            [new Kpi("revenue", "Umsatz brutto", 0, 0, "€"), new Kpi("appointments", "Behandlungen", 12, 10, "")], [], [], [], [], ["Auslastung niedrig."], AmountsHidden: true);

        var body = ReportMailPlanner.Body(report, "Wien", ReportMailKind.Week, null);

        Assert.DoesNotContain("Umsatz", body, StringComparison.Ordinal);
        Assert.Contains("- Behandlungen: 12 (Woche davor: 10)", body, StringComparison.Ordinal);
        Assert.Contains("- Auslastung niedrig.", body, StringComparison.Ordinal);
        Assert.Contains("Beträge stehen hier nur mit dem Recht", body, StringComparison.Ordinal);
        Assert.DoesNotContain("Kommentar der KI", body, StringComparison.Ordinal);
    }
}
