using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Tests;

/// <summary>
/// Vorab über Treatwell bezahlte Termine an der Kassa (ADR 0062, ADR 0010): der Umsatz läuft über die Kassa mit Beleg, die Zahlungsart
/// „Über Treatwell bezahlt“ deckt nur den vorab bezahlten Betrag, und die Buchhaltung bucht auf das Verrechnungskonto für Treatwell.
/// </summary>
public sealed class TreatwellPaymentTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static DateOnly ViennaToday =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna")).DateTime);

    [Fact]
    public async Task Vorab_bezahlter_Termin_laeuft_ueber_die_Kassa_und_der_Betrag_wird_nicht_noch_einmal_verlangt()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var register = await CreateRegisterAsync(spa);
        var reference = NewReference();
        var appointment = await TreatwellAppointmentAsync(spa, MailFixtures.BookingTable, reference, "10:00");

        var bill = await spa.PostAsync<BillDto>("/api/pos/bills", new BillInput(register.Id, null, [appointment.Id]), Ct);

        Assert.Contains(PaymentMethod.Treatwell, register.PaymentMethods);
        Assert.Equal((35m, reference, 79m), (bill.PlatformPrepaid, bill.PlatformReference, bill.Open));

        var receipt = await spa.PostAsync<ReceiptDto>($"/api/pos/bills/{bill.Id}/checkout", new CheckoutRequest(null,
            [new PaymentInput(PaymentMethod.Treatwell, bill.PlatformPrepaid, null, null), new PaymentInput(PaymentMethod.Cash, 44m, null, null)]), Ct);

        // Der Umsatz steht mit dem vollen Preis auf dem Beleg; Treatwell deckt 35 €, der Gast zahlt nur den Rest.
        Assert.Equal((79m, 0m), (receipt.Total, receipt.Change));
        Assert.Equal([PaymentMethod.Cash, PaymentMethod.Treatwell], receipt.Payments.Select(p => p.Method).Order());
        Assert.Equal((35m, $"Treatwell {reference}"), receipt.Payments.Where(p => p.Method == PaymentMethod.Treatwell).Select(p => (p.Amount, p.Reference)).Single());
        Assert.Equal(BillStatus.Settled, (await spa.GetAsync<BillDto>($"/api/pos/bills/{bill.Id}", Ct)).Status);

        var postings = await spa.GetAsync<List<AccountingPosting>>(
            $"/api/accounting/postings?from={ViennaToday:yyyy-MM-dd}&until={ViennaToday:yyyy-MM-dd}&registerId={register.Id}", Ct);
        Assert.Contains(postings, p => p is { DebitAccount: "1400", CreditAccount: "1360", Amount: 35m });
        Assert.Contains(postings, p => p is { DebitAccount: "1000", Amount: 44m });
        var transit = postings.Where(p => p.DebitAccount == "1360").Sum(p => p.Amount) - postings.Where(p => p.CreditAccount == "1360").Sum(p => p.Amount);
        Assert.Equal(0m, transit);
    }

    [Fact]
    public async Task Die_Zahlungsart_Treatwell_gilt_nur_fuer_vorab_bezahlte_Termine_und_nur_bis_zum_bezahlten_Betrag()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var register = await CreateRegisterAsync(spa);
        var prepaid = await TreatwellAppointmentAsync(spa, MailFixtures.BookingTable, NewReference(), "10:00");
        var onSite = await TreatwellAppointmentAsync(spa, MailFixtures.BookingList, NewReference(), "12:00");
        var ordinary = await spa.BookOkAsync(TestSpa.At(14), Ct);

        var overpaid = await spa.PostAsync<BillDto>("/api/pos/bills", new BillInput(register.Id, null, [prepaid.Id]), Ct);
        var payOnSite = await spa.PostAsync<BillDto>("/api/pos/bills", new BillInput(register.Id, null, [onSite.Id]), Ct);
        var plain = await spa.PostAsync<BillDto>("/api/pos/bills", new BillInput(register.Id, null, [ordinary.Id]), Ct);

        Assert.Equal((0m, 0m), (payOnSite.PlatformPrepaid, plain.PlatformPrepaid));
        var message = await FailureAsync(spa, overpaid.Id, 50m);
        Assert.Contains("vorab bezahlt sind für diese Termine", message, StringComparison.Ordinal);
        Assert.Contains("35", message, StringComparison.Ordinal);
        Assert.Contains("keine Vorauszahlung über Treatwell", await FailureAsync(spa, payOnSite.Id, 10m), StringComparison.Ordinal);
        Assert.Contains("keine Vorauszahlung über Treatwell", await FailureAsync(spa, plain.Id, 10m), StringComparison.Ordinal);
        Assert.Equal(BillStatus.Open, (await spa.GetAsync<BillDto>($"/api/pos/bills/{overpaid.Id}", Ct)).Status);
    }

    [Fact]
    public async Task Verrechnungskonto_fuer_Treatwell_hat_einen_Standard_und_laesst_sich_aendern()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);

        var settings = await spa.GetAsync<AccountingSettingsDto>("/api/accounting/settings", Ct);
        Assert.Equal("1400", settings.TreatwellAccount);

        var changed = await spa.PutAsync<AccountingSettingsDto>("/api/accounting/settings", settings with { TreatwellAccount = "1500" }, Ct);
        Assert.Equal("1500", changed.TreatwellAccount);

        using var invalid = await spa.Client.PutAsJsonAsync("/api/accounting/settings", settings with { TreatwellAccount = "Treatwell" }, TestSpa.Json, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }

    private static string NewReference() => $"TW-{Guid.NewGuid():N}"[..14];

    private static async Task<string> FailureAsync(TestSpa spa, Guid billId, decimal amount)
    {
        using var response = await spa.Client.PostAsJsonAsync($"/api/pos/bills/{billId}/checkout",
            new CheckoutRequest(null, [new PaymentInput(PaymentMethod.Treatwell, amount, null, null)]), TestSpa.Json, Ct);
        Assert.False(response.IsSuccessStatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        return problem.GetProperty("errors").GetProperty("payments")[0].GetString()!;
    }

    private static async Task<CashRegisterDto> CreateRegisterAsync(TestSpa spa) =>
        await spa.PostAsync<CashRegisterDto>("/api/pos/registers", new CashRegisterInput(spa.Location.Id, $"T-{Guid.NewGuid():N}"[..14], FiscalCountry.AT,
            "Test-Spa GmbH\nTeststraße 1, 1010 Wien\nATU12345678", "Test-Spa GmbH", "AT61 1904 3002 3457 3201", null), Ct);

    /// <summary>Spielt eine synthetische Treatwell-Mail ein und liefert den Termin, der daraus entstanden ist.</summary>
    private static async Task<AppointmentDto> TreatwellAppointmentAsync(TestSpa spa, string fixtureName, string reference, string time)
    {
        var html = MailFixtures.Load(fixtureName, reference, $"1. März 2027 um {time} Uhr", spa.MassageTreatment.Name, spa.Location.Name, "Anna");
        var response = await spa.Client.PostAsJsonAsync("/api/inbox/dev/mails", new
        {
            from = MailFixtures.Sender,
            fromName = (string?)null,
            subject = "Neue Buchung über Treatwell",
            body = html,
            receivedAt = TestSpa.At(8, 0, TestSpa.Monday.AddDays(-4)),
            senderVerified = true,
        }, TestSpa.Json, Ct);
        var id = (await TestSpa.ReadAsync<JsonElement>(response, Ct)).GetProperty("id").GetGuid();
        var until = DateTime.UtcNow.AddSeconds(30);
        while (true)
        {
            var mail = await spa.GetAsync<InboundMailDto>($"/api/inbox/{id}", Ct);
            if (mail.Status != InboundMailStatus.Received)
            {
                Assert.True(mail.Status == InboundMailStatus.Booked, mail.Note);
                return await spa.GetAsync<AppointmentDto>($"/api/appointments/{mail.AppointmentId}", Ct);
            }

            Assert.True(DateTime.UtcNow < until, "Die Mail wurde nicht verarbeitet.");
            await Task.Delay(100, Ct);
        }
    }
}
