using System.Net;
using System.Net.Http.Json;
using AispaStudio.Api.Mail;
using AispaStudio.Api.Online;
using AispaStudio.Api.Pos;
using AispaStudio.Api.Tenancy;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace AispaStudio.Api.Tests;

/// <summary>
/// Status gespeichert, Nachricht nie in der Queue (Absturz dazwischen): die Tests schreiben den Zwischenstand direkt in die Datenbank,
/// ohne die Queue zu berühren, und erwarten, dass der Nachholjob den Ablauf zu Ende bringt.
/// </summary>
public sealed class PaymentRecoveryTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string NewEmail() => $"nachholen-{Guid.NewGuid():N}@example.at";

    private static async Task CreateRegisterAsync(TestSpa spa) =>
        await spa.PostAsync<CashRegisterDto>("/api/pos/registers", new CashRegisterInput(
            spa.Location.Id, $"N-{Guid.NewGuid():N}"[..14], FiscalCountry.AT, "Test-Spa GmbH\nTeststraße 1, 1010 Wien\nATU12345678",
            "Test-Spa GmbH", "AT61 1904 3002 3457 3201", null), Ct);

    private static async Task<OnlineBookingConfirmation> BookWithDepositAsync(TestSpa spa, string email)
    {
        await spa.PutAsync<LocationOnlineRules>($"/api/locations/{spa.Location.Id}/online-rules",
            new LocationOnlineRules(true, 60, 365, 24, 15, null, false, 0, 0, false, 30), Ct);
        await CreateRegisterAsync(spa);
        using var client = spa.OnlineClient();
        var response = await client.PostAsJsonAsync("/api/online/bookings", new OnlineBookingRequest(
            spa.Location.Id, spa.MassageTreatment.Id, TestSpa.At(10), new OnlineContact("Ida", "Anzahl", email, null), null, true, "de", true), TestSpa.Json, Ct);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await TestSpa.ReadAsync<OnlineBookingConfirmation>(response, Ct);
    }

    private async Task MarkDepositPaidAsync(Guid appointmentId, DateTimeOffset paidAt)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var changed = await scope.ServiceProvider.GetRequiredService<OnlineDbContext>().Deposits.Where(d => d.AppointmentId == appointmentId)
            .ExecuteUpdateAsync(u => u.SetProperty(d => d.Status, OnlineDepositStatus.Paid).SetProperty(d => d.PaidAt, paidAt), Ct);
        Assert.Equal(1, changed);
    }

    private static async Task<DepositStaffDto> WaitForDepositAsync(TestSpa spa, Guid appointmentId, OnlineDepositStatus status)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));
        while (true)
        {
            var deposit = await spa.GetAsync<DepositStaffDto>($"/api/appointments/{appointmentId}/deposit", Ct);
            if (deposit.Status == status)
            {
                return deposit;
            }

            await Task.Delay(50, timeout.Token);
        }
    }

    private async Task<DepositStaffDto> ReceiptedDepositAsync(TestSpa spa, OnlineBookingConfirmation booked)
    {
        await MarkDepositPaidAsync(booked.Appointment.Id, DateTimeOffset.UtcNow);
        await fixture.Factory.Services.GetRequiredService<PaymentRecovery>().RunOnceAsync(DateTimeOffset.UtcNow.AddHours(1), spa.Location.Id, Ct);
        return await WaitForDepositAsync(spa, booked.Appointment.Id, OnlineDepositStatus.Receipted);
    }

    /// <summary>Storniert wie die Kasse, aber ohne das Ereignis <see cref="ReceiptVoided"/> in die Queue zu stellen.</summary>
    private async Task VoidWithoutEventAsync(TestSpa spa, Guid receiptId)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Use(spa.TenantId);
        var voided = await scope.ServiceProvider.GetRequiredService<PosService>().VoidAsync(receiptId, "Test", null, true, Ct);
        Assert.Null(voided.Error);
    }

    [Fact]
    public async Task Bezahlte_Anzahlung_ohne_Nachricht_bekommt_ihren_Beleg_und_nur_einen()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var booked = await BookWithDepositAsync(spa, NewEmail());
        var now = DateTimeOffset.UtcNow;
        await MarkDepositPaidAsync(booked.Appointment.Id, now.AddHours(-1));
        var recovery = fixture.Factory.Services.GetRequiredService<PaymentRecovery>();

        Assert.Equal(1, await recovery.RunOnceAsync(now, spa.Location.Id, Ct));

        var deposit = await WaitForDepositAsync(spa, booked.Appointment.Id, OnlineDepositStatus.Receipted);
        Assert.NotNull(deposit.ReceiptId);
        Assert.Equal(0, await recovery.RunOnceAsync(now, spa.Location.Id, Ct));
    }

    [Fact]
    public async Task Frisch_bezahlte_Anzahlung_bleibt_der_laufenden_Nachricht_ueberlassen()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var booked = await BookWithDepositAsync(spa, NewEmail());
        var now = DateTimeOffset.UtcNow;
        await MarkDepositPaidAsync(booked.Appointment.Id, now.AddMinutes(-1));

        Assert.Equal(0, await fixture.Factory.Services.GetRequiredService<PaymentRecovery>().RunOnceAsync(now, spa.Location.Id, Ct));
    }

    [Fact]
    public async Task Anzahlung_mit_gemeldetem_Fehler_und_zu_alte_werden_nicht_wiederholt()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var booked = await BookWithDepositAsync(spa, NewEmail());
        var now = DateTimeOffset.UtcNow;
        var recovery = fixture.Factory.Services.GetRequiredService<PaymentRecovery>();
        await MarkDepositPaidAsync(booked.Appointment.Id, now.AddDays(-3));
        Assert.Equal(0, await recovery.RunOnceAsync(now, spa.Location.Id, Ct));

        await MarkDepositPaidAsync(booked.Appointment.Id, now.AddHours(-1));
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<OnlineDbContext>().Deposits.Where(d => d.AppointmentId == booked.Appointment.Id)
            .ExecuteUpdateAsync(u => u.SetProperty(d => d.Error, "An diesem Standort gibt es keine aktive Kasse mehr."), Ct);
        Assert.Equal(0, await recovery.RunOnceAsync(now, spa.Location.Id, Ct));
    }

    [Fact]
    public async Task Bezahlter_Gutschein_ohne_Nachricht_wird_ausgestellt()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await CreateRegisterAsync(spa);
        var email = NewEmail();
        using var client = spa.OnlineClient();
        var response = await client.PostAsJsonAsync("/api/online/vouchers/orders", new VoucherOrderRequest(
            spa.Location.Id, VoucherKind.MultiPurpose, 50m, null, new OnlineContact("Gerda", "Schenk", email, null), "Mama", "Alles Gute", true, "de"), TestSpa.Json, Ct);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var now = DateTimeOffset.UtcNow;
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var changed = await scope.ServiceProvider.GetRequiredService<OnlineDbContext>().VoucherOrders.Where(o => o.BuyerEmail == email)
                .ExecuteUpdateAsync(u => u.SetProperty(o => o.Status, VoucherOrderStatus.Paid).SetProperty(o => o.PaidAt, now.AddHours(-1)), Ct);
            Assert.Equal(1, changed);
        }

        Assert.Equal(1, await fixture.Factory.Services.GetRequiredService<PaymentRecovery>().RunOnceAsync(now, spa.Location.Id, Ct));

        await fixture.Mailbox.WaitForAsync(email, _ => true, Ct);
        await using var check = fixture.Factory.Services.CreateAsyncScope();
        var order = await check.ServiceProvider.GetRequiredService<OnlineDbContext>().VoucherOrders.AsNoTracking().FirstAsync(o => o.BuyerEmail == email, Ct);
        Assert.Equal(VoucherOrderStatus.Issued, order.Status);
    }

    [Fact]
    public async Task Storno_ohne_Ereignis_beginnt_die_Erstattung()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var booked = await BookWithDepositAsync(spa, NewEmail());
        var deposit = await ReceiptedDepositAsync(spa, booked);
        await VoidWithoutEventAsync(spa, deposit.ReceiptId!.Value);
        var recovery = fixture.Factory.Services.GetRequiredService<PaymentRecovery>();

        Assert.Equal(0, await recovery.RunOnceAsync(DateTimeOffset.UtcNow, spa.Location.Id, Ct));
        Assert.Equal(1, await recovery.RunOnceAsync(DateTimeOffset.UtcNow.AddHours(1), spa.Location.Id, Ct));

        await WaitForDepositAsync(spa, booked.Appointment.Id, OnlineDepositStatus.Refunded);
        Assert.Equal(0, await recovery.RunOnceAsync(DateTimeOffset.UtcNow.AddHours(1), spa.Location.Id, Ct));
    }

    [Fact]
    public async Task Begonnene_Erstattung_ohne_Nachricht_wird_zu_Ende_gebracht()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var booked = await BookWithDepositAsync(spa, NewEmail());
        var deposit = await ReceiptedDepositAsync(spa, booked);
        var now = DateTimeOffset.UtcNow.AddHours(2);
        await VoidWithoutEventAsync(spa, deposit.ReceiptId!.Value);
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var payment = await scope.ServiceProvider.GetRequiredService<OnlineRefunds>().PaymentOfAsync(deposit.ReceiptId.Value, Ct);
            var online = scope.ServiceProvider.GetRequiredService<OnlineDbContext>();
            online.Refunds.Add(new OnlineRefund
            {
                ReceiptId = deposit.ReceiptId.Value,
                Source = payment!.Source,
                SourceId = payment.SourceId,
                LocationId = payment.LocationId,
                Provider = payment.Provider,
                PaymentReference = payment.Reference,
                Amount = payment.Amount,
                Currency = payment.Currency,
                Email = payment.Email,
                Language = payment.Language,
                Status = OnlineRefundStatus.Pending,
                RequestedAt = now.AddHours(-1),
                Attempts = 1,
            });
            await online.SaveChangesAsync(Ct);
        }

        Assert.Equal(1, await fixture.Factory.Services.GetRequiredService<PaymentRecovery>().RunOnceAsync(now, spa.Location.Id, Ct));

        await WaitForDepositAsync(spa, booked.Appointment.Id, OnlineDepositStatus.Refunded);
    }

    [Fact]
    public async Task Mail_uebersteht_einen_kurzen_Ausfall_des_Mailservers()
    {
        var email = NewEmail();
        fixture.Mailbox.FailFor(email, times: 1);

        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IMessageBus>().PublishAsync(new SendMail(new OutgoingMail(email, "Test", "Test")));

        await fixture.Mailbox.WaitForAsync(email, _ => true, Ct);
        Assert.Equal(2, fixture.Mailbox.AttemptsFor(email));
        Assert.Single(fixture.Mailbox.To(email));
    }

    [Fact]
    public async Task Betreiber_sieht_die_Fehlerablage_nach_allen_Versuchen()
    {
        using var operatorClient = fixture.CreateClient();
        operatorClient.DefaultRequestHeaders.Add("X-Aispa-Operator-Key", ApiFixture.OperatorKey);
        var before = (await operatorClient.GetFromJsonAsync<OperatorQueueHealth>("/api/operator/queue-health", TestSpa.Json, Ct))!;
        var email = NewEmail();
        fixture.Mailbox.FailFor(email);

        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IMessageBus>().PublishAsync(new SendMail(new OutgoingMail(email, "Test", "Test")));

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(60));
        while (fixture.Mailbox.AttemptsFor(email) < 4
            || (await operatorClient.GetFromJsonAsync<OperatorQueueHealth>("/api/operator/queue-health", TestSpa.Json, Ct))!.DeadLetters <= before.DeadLetters)
        {
            await Task.Delay(100, timeout.Token);
        }

        // Erster Lauf und drei Wiederholungen (Program.cs); danach bleibt die Nachricht in der Fehlerablage liegen.
        Assert.Equal(4, fixture.Mailbox.AttemptsFor(email));
    }

    [Fact]
    public async Task Fehlerablage_ist_ohne_Betreiberschluessel_gesperrt()
    {
        using var client = fixture.CreateClient();

        var response = await client.GetAsync("/api/operator/queue-health", Ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Betreiber_sieht_haengende_Zahlungen_auch_nach_dem_Wiederholfenster()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var booked = await BookWithDepositAsync(spa, NewEmail());
        await MarkDepositPaidAsync(booked.Appointment.Id, DateTimeOffset.UtcNow.AddDays(-3));
        using var operatorClient = fixture.CreateClient();
        operatorClient.DefaultRequestHeaders.Add("X-Aispa-Operator-Key", ApiFixture.OperatorKey);

        var health = await operatorClient.GetFromJsonAsync<OperatorQueueHealth>("/api/operator/queue-health", TestSpa.Json, Ct);

        Assert.True(health!.UnreceiptedDeposits >= 1);
    }
}
