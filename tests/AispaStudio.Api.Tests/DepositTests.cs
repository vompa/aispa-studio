using System.Net;
using System.Net.Http.Json;
using AispaStudio.Api.Online;
using AispaStudio.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace AispaStudio.Api.Tests;

public sealed class DepositTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string NewEmail() => $"anzahlung-{Guid.NewGuid():N}@example.at";

    /// <summary>Standort mit 30 % Anzahlung, Buchungshorizont ein Jahr (die Testtermine liegen im März 2027).</summary>
    private static Task<LocationOnlineRules> RequireDepositAsync(TestSpa spa, int percent = 30, int noShowFee = 0, int holdHours = 0) =>
        spa.PutAsync<LocationOnlineRules>($"/api/locations/{spa.Location.Id}/online-rules",
            new LocationOnlineRules(true, 60, 365, 24, 15, null, false, noShowFee, holdHours, false, percent), Ct);

    private static OnlineBookingRequest Request(TestSpa spa, DateTimeOffset start, string email, bool accept) =>
        new(spa.Location.Id, spa.MassageTreatment.Id, start, new OnlineContact("Ida", "Anzahl", email, null), null, true, "de", accept);

    private static async Task<CashRegisterDto> CreateRegisterAsync(TestSpa spa) =>
        await spa.PostAsync<CashRegisterDto>("/api/pos/registers", new CashRegisterInput(
            spa.Location.Id, $"A-{Guid.NewGuid():N}"[..14], FiscalCountry.AT, "Test-Spa GmbH\nTeststraße 1, 1010 Wien\nATU12345678",
            "Test-Spa GmbH", "AT61 1904 3002 3457 3201", null), Ct);

    private static async Task<OnlineBookingConfirmation> BookAsync(TestSpa spa, DateTimeOffset start, string email)
    {
        using var client = spa.OnlineClient();
        var response = await client.PostAsJsonAsync("/api/online/bookings", Request(spa, start, email, true), TestSpa.Json, Ct);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await TestSpa.ReadAsync<OnlineBookingConfirmation>(response, Ct);
    }

    private static async Task<DepositStaffDto> WaitForAsync(TestSpa spa, Guid appointmentId, Func<DepositStaffDto, bool> done)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));
        while (true)
        {
            var deposit = await spa.GetAsync<DepositStaffDto>($"/api/appointments/{appointmentId}/deposit", Ct);
            if (done(deposit))
            {
                return deposit;
            }

            await Task.Delay(50, timeout.Token);
        }
    }

    private static async Task<DepositStaffDto> PayAsync(TestSpa spa, OnlineBookingConfirmation booked)
    {
        using var client = spa.OnlineClient();
        var paid = await client.PostAsJsonAsync($"/api/online/bookings/{booked.ManageToken}/deposit/test", new TestPaymentResult(true), TestSpa.Json, Ct);
        Assert.Equal(HttpStatusCode.OK, paid.StatusCode);
        return await WaitForAsync(spa, booked.Appointment.Id, d => d.Status == OnlineDepositStatus.Receipted);
    }

    [Fact]
    public async Task Anzahlung_bekommt_einen_Anzahlungsbeleg_und_wird_beim_Kassieren_verrechnet()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await RequireDepositAsync(spa, noShowFee: 50);
        var register = await CreateRegisterAsync(spa);
        var email = NewEmail();
        using var client = spa.OnlineClient();

        var treatments = await client.GetFromJsonAsync<List<OnlineTreatment>>($"/api/online/locations/{spa.Location.Id}/treatments", TestSpa.Json, Ct);
        Assert.Equal(30, Assert.Single(treatments!, t => t.Id == spa.MassageTreatment.Id).DepositPercent);
        var refused = await client.PostAsJsonAsync("/api/online/bookings", Request(spa, TestSpa.At(10), email, false), TestSpa.Json, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        Assert.Contains("Bedingungen der Anzahlung", await refused.Content.ReadAsStringAsync(Ct), StringComparison.Ordinal);

        var booked = await BookAsync(spa, TestSpa.At(10), email);
        // Die Anzahlung ersetzt die Kartenhinterlegung.
        Assert.Null(booked.Appointment.CardGuarantee);
        var deposit = booked.Appointment.Deposit!;
        Assert.Equal((23.70m, 30, OnlineDepositStatus.Pending, true), (deposit.Amount, deposit.Percent, deposit.Status, deposit.TestMode));
        var confirmation = await fixture.Mailbox.WaitForAsync(email, m => m.Subject.StartsWith("Termin bestätigt", StringComparison.Ordinal), Ct);
        Assert.Contains("Anzahlung: 23,70 EUR (30 % des Preises)", confirmation.Text, StringComparison.Ordinal);

        var receipted = await PayAsync(spa, booked);
        var depositReceipt = await spa.GetAsync<ReceiptDto>($"/api/pos/receipts/{receipted.ReceiptId}", Ct);
        Assert.Equal(23.70m, depositReceipt.Total);
        var depositLine = Assert.Single(depositReceipt.Lines);
        Assert.StartsWith("Anzahlung Massage", depositLine.Description, StringComparison.Ordinal);
        Assert.Equal(20m, depositLine.TaxPercent);
        Assert.Equal(PaymentMethod.Card, Assert.Single(depositReceipt.Payments).Method);
        var mail = await fixture.Mailbox.WaitForAsync(email, m => m.Subject.StartsWith("Anzahlung erhalten", StringComparison.Ordinal), Ct);
        Assert.Contains($"Beleg: {register.Code} Nr. {depositReceipt.Number}", mail.Text, StringComparison.Ordinal);
        var guest = await client.GetFromJsonAsync<OnlineAppointment>($"/api/online/bookings/{booked.ManageToken}", TestSpa.Json, Ct);
        Assert.Equal(OnlineDepositStatus.Receipted, guest!.Deposit!.Status);
        Assert.StartsWith($"{register.Code} Nr. {depositReceipt.Number}", guest.Deposit.Receipt, StringComparison.Ordinal);

        // Kassieren der Behandlung: nur der Rest ist offen, die Anzahlung steht als Abzug mit gleichem Steuersatz auf dem Beleg.
        await spa.SetStatusAsync(booked.Appointment.Id, AppointmentStatus.CheckedIn, Ct);
        var bill = await spa.PostAsync<BillDto>("/api/pos/bills", new BillInput(register.Id, null, [booked.Appointment.Id]), Ct);
        Assert.Equal((79m, 23.70m, 55.30m), (bill.Total, bill.DepositCredit, bill.Open));
        Assert.Equal(23.70m, Assert.Single(bill.Lines).DepositCredit);
        var sale = await spa.PostAsync<ReceiptDto>($"/api/pos/bills/{bill.Id}/checkout",
            new CheckoutRequest(null, [new PaymentInput(PaymentMethod.Card, 55.30m, null, null)]), Ct);
        Assert.Equal(55.30m, sale.Total);
        var credit = Assert.Single(sale.Lines, l => l.Amount < 0);
        Assert.Equal((-23.70m, 20m), (credit.Amount, credit.TaxPercent));
        Assert.Contains($"Beleg {register.Code} Nr. {depositReceipt.Number}", credit.Description, StringComparison.Ordinal);
        Assert.Equal(55.30m, Assert.Single(sale.Taxes).Gross);
        var settled = await spa.GetAsync<DepositStaffDto>($"/api/appointments/{booked.Appointment.Id}/deposit", Ct);
        Assert.True(settled.Settled);
        Assert.Equal(sale.Id, settled.SettledReceiptId);

        // Der Anzahlungsbeleg ist jetzt verrechnet und lässt sich nicht mehr allein stornieren.
        var voidDeposit = await spa.Client.PostAsJsonAsync($"/api/pos/receipts/{depositReceipt.Id}/void", new VoidRequest("Fehler"), TestSpa.Json, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, voidDeposit.StatusCode);

        // Storno des Behandlungsbelegs: die Anzahlung steht wieder zur Verrechnung bereit.
        await spa.PostAsync<ReceiptDto>($"/api/pos/receipts/{sale.Id}/void", new VoidRequest("Falsch kassiert"), Ct);
        var reopened = await spa.GetAsync<BillDto>($"/api/pos/bills/{bill.Id}", Ct);
        Assert.Equal((23.70m, 55.30m), (reopened.DepositCredit, reopened.Open));
        Assert.False((await spa.GetAsync<DepositStaffDto>($"/api/appointments/{booked.Appointment.Id}/deposit", Ct)).Settled);
    }

    [Fact]
    public async Task Rechtzeitige_Absage_storniert_den_Anzahlungsbeleg_und_erstattet()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await RequireDepositAsync(spa, percent: 100);
        await CreateRegisterAsync(spa);
        var email = NewEmail();
        var booked = await BookAsync(spa, TestSpa.At(11), email);
        Assert.Equal(79m, booked.Appointment.Deposit!.Amount);
        var confirmation = await fixture.Mailbox.WaitForAsync(email, m => m.Subject.StartsWith("Termin bestätigt", StringComparison.Ordinal), Ct);
        Assert.Contains("Vorauszahlung: 79,00 EUR", confirmation.Text, StringComparison.Ordinal);
        var receipted = await PayAsync(spa, booked);

        using var client = spa.OnlineClient();
        var cancel = await client.PostAsync($"/api/online/bookings/{booked.ManageToken}/cancel", null, Ct);
        Assert.Equal(HttpStatusCode.OK, cancel.StatusCode);
        var refunded = await WaitForAsync(spa, booked.Appointment.Id, d => d.Status == OnlineDepositStatus.Refunded);
        var refund = await spa.GetAsync<OnlineRefundDto>($"/api/pos/receipts/{receipted.ReceiptId}/online-refund", Ct);
        var mail = await fixture.Mailbox.WaitForAsync(email, m => m.Subject.StartsWith("Rückerstattung", StringComparison.Ordinal), Ct);

        Assert.True(refunded.Voided);
        Assert.Equal((OnlinePaymentSource.Deposit, OnlineRefundStatus.Refunded, 79m), (refund.Source, refund.Status, refund.Amount));
        Assert.Contains("Ihre Anzahlung storniert", mail.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Bei_Nichterscheinen_bleibt_die_Anzahlung_und_ohne_Kasse_gilt_die_Karte()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await RequireDepositAsync(spa, noShowFee: 50);

        // Ohne Kasse kein Anzahlungsbeleg: dann wird wie bisher eine Karte hinterlegt.
        var withoutRegister = await BookAsync(spa, TestSpa.At(10), NewEmail());
        Assert.Null(withoutRegister.Appointment.Deposit);
        Assert.Equal(39.50m, withoutRegister.Appointment.CardGuarantee!.FeeAmount);

        await CreateRegisterAsync(spa);
        var booked = await BookAsync(spa, TestSpa.At(12), NewEmail());
        await PayAsync(spa, booked);
        await spa.SetStatusAsync(booked.Appointment.Id, AppointmentStatus.NoShow, Ct);
        var kept = await spa.GetAsync<DepositStaffDto>($"/api/appointments/{booked.Appointment.Id}/deposit", Ct);

        Assert.True(kept.Kept);
        Assert.Contains("Einbehalten", kept.Hint, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Unbezahlte_Anzahlung_gibt_den_Termin_nach_der_Frist_frei()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await RequireDepositAsync(spa, holdHours: 2);
        await CreateRegisterAsync(spa);
        var email = NewEmail();
        var open = await BookAsync(spa, TestSpa.At(13), email);
        var payUntil = open.Appointment.Deposit!.PayUntil!.Value;
        var release = fixture.Factory.Services.GetRequiredService<UnsecuredBookingRelease>();

        var due = await release.RunOnceAsync(payUntil.AddMinutes(5), spa.Location.Id, Ct);
        var released = await WaitForAsync(spa, open.Appointment.Id, d => d.Status == OnlineDepositStatus.Released);
        var mail = await fixture.Mailbox.WaitForAsync(email, m => m.Subject.StartsWith("Termin freigegeben", StringComparison.Ordinal), Ct);
        using var client = spa.OnlineClient();
        var guest = await client.GetFromJsonAsync<OnlineAppointment>($"/api/online/bookings/{open.ManageToken}", TestSpa.Json, Ct);

        Assert.Equal(1, due);
        Assert.Contains("nicht bezahlt", released.Hint, StringComparison.Ordinal);
        Assert.Contains("Anzahlung", mail.Text, StringComparison.Ordinal);
        Assert.Equal(OnlineAppointmentState.Cancelled, guest!.State);
    }
}
