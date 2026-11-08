using System.Net;
using System.Net.Http.Json;
using AispaStudio.Api.Closures;
using AispaStudio.Api.Data;
using AispaStudio.Api.Tenancy;
using AispaStudio.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AispaStudio.Api.Tests;

/// <summary>
/// Anzahlung bei Absage durch den Betrieb und von Hand (ADR 0073, Ä47): eine Absage wegen einer Schließzeit retourniert eine geleistete Anzahlung automatisch als Storno-Beleg
/// über die Kassa (derselbe Weg wie eine manuelle Rückzahlung, Zahlungsweg der Einzahlung) und nennt es in der Nachricht; in allen anderen Fällen retourniert nur ein Manager oder der
/// Inhaber, mit Rückfrage und Pflichtgrund. Nichts geht an der Kassa vorbei (ADR 0010): jede Rückzahlung hat genau einen Storno-Beleg.
/// </summary>
public sealed class ClosureDepositTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly DateOnly Day = TestSpa.Monday;

    private static string Path(TestSpa spa, ClosureDto closure, string tail = "") => $"{ClosureTests.Path(spa)}/{closure.Id}{tail}";

    private static CancelClosureAppointmentsRequest Request(params Guid[] appointmentIds) =>
        new(appointmentIds, "Absage: {Behandlung}", "Guten Tag {Anrede},\n\nIhr Termin ({Behandlung}, {Zeit}) fällt leider aus.\n\nIhr Team", false, false);

    private static async Task<CashRegisterDto> CreateRegisterAsync(TestSpa spa) =>
        await spa.PostAsync<CashRegisterDto>("/api/pos/registers", new CashRegisterInput(
            spa.Location.Id, $"A-{Guid.NewGuid():N}"[..14], FiscalCountry.AT, "Test-Spa GmbH\nTeststraße 1, 1010 Wien\nATU12345678",
            "Test-Spa GmbH", "AT61 1904 3002 3457 3201", null), Ct);

    /// <summary>Anzahlung an der Kassa: 20 Euro bar auf einen Termin.</summary>
    private static async Task<ReceiptDto> PayCashDepositAsync(TestSpa spa, CashRegisterDto register, AppointmentDto appointment, decimal amount = 20m, PaymentMethod method = PaymentMethod.Cash)
    {
        var bill = await spa.PostAsync<BillDto>("/api/pos/bills", new BillInput(register.Id, appointment.CustomerId, []), Ct);
        bill = await spa.PostAsync<BillDto>($"/api/pos/bills/{bill.Id}/lines",
            new BillLineInput(BillLineKind.Deposit, appointment.Id, null, 1, amount, 0, null, null, null, null, null), Ct);
        return await spa.PostAsync<ReceiptDto>($"/api/pos/bills/{bill.Id}/checkout", new CheckoutRequest(null, [new PaymentInput(method, amount, null, null)]), Ct);
    }

    private async Task<int> VoidCountAsync(TestSpa spa, Guid receiptId)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Use(spa.TenantId);
        return await scope.ServiceProvider.GetRequiredService<AispaDbContext>().Receipts.CountAsync(r => r.VoidsReceiptId == receiptId, Ct);
    }

    // ---------- Online-Anzahlung ----------

    private static Task<LocationOnlineRules> RequireDepositAsync(TestSpa spa) =>
        spa.PutAsync<LocationOnlineRules>($"/api/locations/{spa.Location.Id}/online-rules",
            new LocationOnlineRules(true, 60, 365, 24, 15, null, false, 0, 0, false, 30), Ct);

    private static async Task<OnlineBookingConfirmation> BookOnlineAsync(TestSpa spa, DateTimeOffset start, string email)
    {
        using var client = spa.OnlineClient();
        var response = await client.PostAsJsonAsync("/api/online/bookings",
            new OnlineBookingRequest(spa.Location.Id, spa.MassageTreatment.Id, start, new OnlineContact("Ida", "Anzahl", email, null), null, true, "de", true), TestSpa.Json, Ct);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await TestSpa.ReadAsync<OnlineBookingConfirmation>(response, Ct);
    }

    private static async Task<DepositStaffDto> WaitForAsync(TestSpa spa, Guid appointmentId, Func<DepositStaffDto, bool> done)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));
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

    private static async Task<DepositStaffDto> PayOnlineAsync(TestSpa spa, OnlineBookingConfirmation booked)
    {
        using var client = spa.OnlineClient();
        var paid = await client.PostAsJsonAsync($"/api/online/bookings/{booked.ManageToken}/deposit/test", new TestPaymentResult(true), TestSpa.Json, Ct);
        Assert.Equal(HttpStatusCode.OK, paid.StatusCode);
        return await WaitForAsync(spa, booked.Appointment.Id, d => d.Status == OnlineDepositStatus.Receipted);
    }

    [Fact]
    public async Task Die_Absage_wegen_einer_Schliesszeit_retourniert_die_Online_Anzahlung_als_Storno_Beleg_und_nennt_es_in_der_Nachricht()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await RequireDepositAsync(spa);
        await CreateRegisterAsync(spa);
        var email = $"anzahlung-{Guid.NewGuid():N}@example.at";
        var booked = await BookOnlineAsync(spa, TestSpa.At(10, 0, Day), email);
        var deposit = await PayOnlineAsync(spa, booked);
        var closure = await ClosureTests.CreateAsync(spa, ClosureTests.Input(Day, kind: ClosureKind.Outage, guestText: "Wegen eines Wasserschadens bleibt das Spa heute zu."));

        // Die Rückfrage nennt den Betrag, der zurückgeht.
        var preview = await spa.GetAsync<ClosureCancellationPreview>(Path(spa, closure, "/cancellation"), Ct);
        var row = Assert.Single(preview.Appointments);
        Assert.Equal((true, 23.70m), (row.HasDeposit, row.DepositAmount));

        var result = await spa.PostAsync<CancelClosureAppointmentsResult>(Path(spa, closure, "/cancellation"), Request(booked.Appointment.Id), Ct);

        Assert.Equal((1, 1, 0, 0), (result.Cancelled, result.Refunded, result.RefundsFailed, result.Failed));
        var notice = Assert.Single(result.Notices);
        Assert.Equal((23.70m, "Karte"), (notice.RefundAmount, notice.RefundWay));
        Assert.NotNull(notice.RefundReceiptId);
        Assert.Null(notice.RefundError);

        // Genau ein Storno-Beleg zum Anzahlungsbeleg, mit dem Grund der Schließzeit, auf dem Zahlungsweg der Einzahlung (Karte).
        var voidReceipt = await spa.GetAsync<ReceiptDto>($"/api/pos/receipts/{notice.RefundReceiptId}", Ct);
        Assert.Equal((ReceiptKind.Void, deposit.ReceiptId, -23.70m), (voidReceipt.Kind, voidReceipt.VoidsReceiptId, voidReceipt.Total));
        Assert.Equal((PaymentMethod.Card, -23.70m), (Assert.Single(voidReceipt.Payments).Method, Assert.Single(voidReceipt.Payments).Amount));
        Assert.StartsWith("Schließzeit (Ausfall): ", voidReceipt.VoidReason, StringComparison.Ordinal);
        Assert.EndsWith("Anzahlung retourniert", voidReceipt.VoidReason, StringComparison.Ordinal);
        var original = await spa.GetAsync<ReceiptDto>($"/api/pos/receipts/{deposit.ReceiptId}", Ct);
        Assert.Equal(voidReceipt.Id, original.VoidedByReceiptId);

        // Die Nachricht nennt die Rückzahlung; der Zahlungsanbieter erstattet danach, der Termin zeigt das Protokoll.
        var mail = await fixture.Mailbox.WaitForAsync(email, m => m.Subject.StartsWith("Absage", StringComparison.Ordinal), Ct);
        Assert.Contains("fällt leider aus.\n\nIhre Anzahlung von 23,70 € wird auf dem Zahlungsweg der Einzahlung retourniert.", mail.Text, StringComparison.Ordinal);
        var refunded = await WaitForAsync(spa, booked.Appointment.Id, d => d.Status == OnlineDepositStatus.Refunded);
        Assert.True(refunded.Voided);
        Assert.Contains($"Storno-Beleg {voidReceipt.RegisterCode} Nr. {voidReceipt.Number}", refunded.Hint, StringComparison.Ordinal);
        Assert.Contains("Anzahlung retourniert", refunded.Hint, StringComparison.Ordinal);
        Assert.Contains("zurück: Karte.", refunded.Hint, StringComparison.Ordinal);

        // Auch nach der Erstattung über die Queue bleibt es bei einem einzigen Storno-Beleg.
        Assert.Equal(1, await VoidCountAsync(spa, deposit.ReceiptId!.Value));
        Assert.Single(await spa.GetAsync<List<ClosureNoticeDto>>(Path(spa, closure, "/notices"), Ct));
    }

    [Fact]
    public async Task Eine_noch_nicht_bezahlte_Online_Anzahlung_wird_abgebrochen_und_nichts_retourniert()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await RequireDepositAsync(spa);
        await CreateRegisterAsync(spa);
        var email = $"anzahlung-{Guid.NewGuid():N}@example.at";
        var booked = await BookOnlineAsync(spa, TestSpa.At(10, 0, Day), email);
        var closure = await ClosureTests.CreateAsync(spa, ClosureTests.Input(Day));

        var result = await spa.PostAsync<CancelClosureAppointmentsResult>(Path(spa, closure, "/cancellation"), Request(booked.Appointment.Id), Ct);

        Assert.Equal((1, 0, 0), (result.Cancelled, result.Refunded, result.RefundsFailed));
        Assert.Null(Assert.Single(result.Notices).RefundAmount);
        Assert.Equal(OnlineDepositStatus.Cancelled, (await spa.GetAsync<DepositStaffDto>($"/api/appointments/{booked.Appointment.Id}/deposit", Ct)).Status);
        var mail = await fixture.Mailbox.WaitForAsync(email, m => m.Subject.StartsWith("Absage", StringComparison.Ordinal), Ct);
        Assert.DoesNotContain("Anzahlung", mail.Text, StringComparison.Ordinal);
    }

    // ---------- Anzahlung an der Kassa ----------

    [Fact]
    public async Task Eine_bar_geleistete_Anzahlung_geht_bei_der_Absage_bar_zurueck_in_der_Sprache_des_Gastes()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var register = await CreateRegisterAsync(spa);
        var customer = await spa.PostAsync<CustomerDto>("/api/customers",
            new CustomerInput("Emma", $"Cash{Guid.NewGuid():N}", $"emma-{Guid.NewGuid():N}@example.at", null, null, null, Language: "en"), Ct);
        var appointment = await TestSpa.ReadAsync<AppointmentDto>(await spa.BookAsync(TestSpa.At(10, 0, Day), Ct, customer: customer.Id), Ct);
        var deposit = await PayCashDepositAsync(spa, register, appointment);
        var closure = await ClosureTests.CreateAsync(spa, ClosureTests.Input(Day));

        var result = await spa.PostAsync<CancelClosureAppointmentsResult>(Path(spa, closure, "/cancellation"), Request(appointment.Id), Ct);

        Assert.Equal((1, 0), (result.Refunded, result.RefundsFailed));
        var voidReceipt = await spa.GetAsync<ReceiptDto>($"/api/pos/receipts/{Assert.Single(result.Notices).RefundReceiptId}", Ct);
        Assert.Equal((deposit.Id, -20m), (voidReceipt.VoidsReceiptId, voidReceipt.Total));
        Assert.Equal((PaymentMethod.Cash, -20m), (Assert.Single(voidReceipt.Payments).Method, Assert.Single(voidReceipt.Payments).Amount));
        Assert.Equal(1, await VoidCountAsync(spa, deposit.Id));
        var mail = await fixture.Mailbox.WaitForAsync(customer.Email!, m => m.Subject.StartsWith("Cancellation", StringComparison.Ordinal), Ct);
        Assert.Contains("You will receive the amount of €20.00 back in cash at reception.", mail.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("payment method", mail.Text, StringComparison.Ordinal);
        Assert.Equal("Bar", Assert.Single(result.Notices).RefundWay);
        // Der Termin ist abgesagt und die Anzahlung nicht mehr offen.
        Assert.Equal(HttpStatusCode.NoContent, (await spa.Client.GetAsync($"/api/appointments/{appointment.Id}/deposit-return", Ct)).StatusCode);
    }

    [Fact]
    public async Task Geht_das_Storno_nicht_wird_trotzdem_abgesagt_die_Nachricht_nennt_keine_Rueckzahlung_und_es_bleibt_zu_erstatten()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var register = await CreateRegisterAsync(spa);
        var appointment = await spa.BookOkAsync(TestSpa.At(10, 0, Day), Ct);
        var deposit = await PayCashDepositAsync(spa, register, appointment);
        // Die Kassa ist außer Betrieb genommen: ein Storno-Beleg lässt sich nicht mehr ausstellen.
        Assert.Equal(HttpStatusCode.OK, (await spa.Client.PostAsync($"/api/pos/registers/{register.Id}/decommission", null, Ct)).StatusCode);
        var closure = await ClosureTests.CreateAsync(spa, ClosureTests.Input(Day));

        var result = await spa.PostAsync<CancelClosureAppointmentsResult>(Path(spa, closure, "/cancellation"), Request(appointment.Id), Ct);

        Assert.Equal((1, 0, 1), (result.Cancelled, result.Refunded, result.RefundsFailed));
        var notice = Assert.Single(result.Notices);
        Assert.Null(notice.RefundAmount);
        Assert.False(string.IsNullOrWhiteSpace(notice.RefundError));
        Assert.DoesNotContain("Anzahlung", notice.Text, StringComparison.Ordinal);
        Assert.Equal(0, await VoidCountAsync(spa, deposit.Id));
        var info = await spa.GetAsync<DepositReturnInfo>($"/api/appointments/{appointment.Id}/deposit-return", Ct);
        Assert.Equal(20m, info.Amount);
    }

    // ---------- Von Hand: nur Manager und Inhaber, mit Grund ----------

    private async Task<(TestSpa Spa, AppointmentDto Appointment, ReceiptDto Deposit)> DepositedAsync()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var register = await CreateRegisterAsync(spa);
        var appointment = await spa.BookOkAsync(TestSpa.At(10, 0, Day), Ct);
        return (spa, appointment, await PayCashDepositAsync(spa, register, appointment));
    }

    [Fact]
    public async Task Von_Hand_retourniert_nur_ein_Manager_oder_der_Inhaber_und_nur_mit_Grund()
    {
        var (spa, appointment, deposit) = await DepositedAsync();
        var path = $"/api/appointments/{appointment.Id}/deposit-return";
        var reception = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Reception, [spa.Location.Id]);
        var standort = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Manager, [spa.Location.Id]);
        var manager = await TestUsers.ClientAsync(fixture, Ct, StaffRole.GeneralManager, [spa.Location.Id]);

        // Wer es sieht, weiß, ob er es darf; der Server prüft es bei jedem Aufruf.
        var forOwner = await spa.GetAsync<DepositReturnInfo>(path, Ct);
        Assert.Equal((deposit.Id, 20m, "Bar", true, "EUR"), (forOwner.ReceiptId, forOwner.Amount, forOwner.Way, forOwner.CanReturn, forOwner.Currency));
        Assert.StartsWith($"{deposit.RegisterCode} Nr. {deposit.Number}", forOwner.ReceiptText, StringComparison.Ordinal);
        Assert.False((await reception.GetFromJsonAsync<DepositReturnInfo>(path, TestSpa.Json, Ct))!.CanReturn);
        Assert.False((await standort.GetFromJsonAsync<DepositReturnInfo>(path, TestSpa.Json, Ct))!.CanReturn);
        Assert.True((await manager.GetFromJsonAsync<DepositReturnInfo>(path, TestSpa.Json, Ct))!.CanReturn);

        // Rezeption und Standortleitung bekommen 403, auch mit Grund; es entsteht kein Beleg.
        foreach (var client in new[] { reception, standort })
        {
            var refused = await client.PostAsJsonAsync(path, new DepositReturnRequest("Kulanz"), TestSpa.Json, Ct);
            Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
        }

        Assert.Equal(0, await VoidCountAsync(spa, deposit.Id));

        // Der Grund ist Pflicht und begrenzt.
        foreach (var reason in new[] { "", "   ", new string('x', 301) })
        {
            var invalid = await manager.PostAsJsonAsync(path, new DepositReturnRequest(reason), TestSpa.Json, Ct);
            Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
            Assert.Contains("reason", (await invalid.Content.ReadFromJsonAsync<HttpValidationProblemDetails>(TestSpa.Json, Ct))!.Errors.Keys);
        }

        var returned = await manager.PostAsJsonAsync(path, new DepositReturnRequest("  Gast hat Kulanz bekommen  "), TestSpa.Json, Ct);
        Assert.Equal(HttpStatusCode.OK, returned.StatusCode);
        var voidReceipt = await TestSpa.ReadAsync<ReceiptDto>(returned, Ct);
        Assert.Equal((ReceiptKind.Void, deposit.Id, -20m, "Anzahlung retourniert: Gast hat Kulanz bekommen"), (voidReceipt.Kind, voidReceipt.VoidsReceiptId, voidReceipt.Total, voidReceipt.VoidReason));
        Assert.Equal((PaymentMethod.Cash, -20m), (Assert.Single(voidReceipt.Payments).Method, Assert.Single(voidReceipt.Payments).Amount));
        Assert.Equal(1, await VoidCountAsync(spa, deposit.Id));

        // Danach gibt es nichts mehr zu retournieren: weder Knopf noch ein zweites Storno.
        Assert.Equal(HttpStatusCode.NoContent, (await spa.Client.GetAsync(path, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await spa.Client.PostAsJsonAsync(path, new DepositReturnRequest("Noch einmal"), TestSpa.Json, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await spa.Client.GetAsync($"/api/appointments/{Guid.NewGuid()}/deposit-return", Ct)).StatusCode);
        Assert.Equal(1, await VoidCountAsync(spa, deposit.Id));
    }

    [Fact]
    public async Task Der_Inhaber_retourniert_auch_nach_einer_spaeten_Absage_des_Gastes()
    {
        var (spa, appointment, deposit) = await DepositedAsync();
        var cancel = await spa.Client.PostAsJsonAsync($"/api/appointments/{appointment.Id}/cancel", new CancelAppointmentRequest("Krank"), TestSpa.Json, Ct);
        Assert.Equal(HttpStatusCode.OK, cancel.StatusCode);

        var returned = await spa.Client.PostAsJsonAsync($"/api/appointments/{appointment.Id}/deposit-return", new DepositReturnRequest("Kulanz wegen Krankheit"), TestSpa.Json, Ct);

        Assert.Equal(HttpStatusCode.OK, returned.StatusCode);
        Assert.Equal(deposit.Id, (await TestSpa.ReadAsync<ReceiptDto>(returned, Ct)).VoidsReceiptId);
    }

    [Theory]
    [InlineData("de", "Sie erhalten den Betrag von 30,00 € bar an der Rezeption zurück.", "Ihre Anzahlung von 30,00 € wird auf dem Zahlungsweg der Einzahlung retourniert.")]
    [InlineData("en", "You will receive the amount of €30.00 back in cash at reception.", "Your deposit of €30.00 will be refunded to the payment method you used.")]
    [InlineData("fr", "Vous recevrez le montant de 30,00 € en espèces à la réception.", "Votre acompte de 30,00 € sera remboursé par le moyen de paiement utilisé.")]
    [InlineData("it", "Riceverà l’importo di 30,00 € in contanti alla reception.", "Il suo acconto di 30,00 € le sarà rimborsato con il metodo di pagamento utilizzato.")]
    [InlineData("es", "Sie erhalten den Betrag von 30,00 € bar an der Rezeption zurück.", "Ihre Anzahlung von 30,00 € wird auf dem Zahlungsweg der Einzahlung retourniert.")]
    public void Der_Satz_zur_Rueckzahlung_nennt_bei_Barzahlung_die_Rezeption_sonst_den_Zahlungsweg(string language, string cash, string other)
    {
        Assert.Equal(cash, ClosureText.RefundSentence(30m, "EUR", language, cash: true));
        Assert.Equal(other, ClosureText.RefundSentence(30m, "EUR", language, cash: false));
        Assert.Equal(other, ClosureText.RefundSentence(30m, "EUR", language));
    }

    [Fact]
    public async Task Je_Zahlungsweg_der_Anzahlung_sagt_die_Nachricht_bar_an_der_Rezeption_oder_Zahlungsweg_der_Einzahlung()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var register = await CreateRegisterAsync(spa);
        var (cashEmail, cardEmail) = ($"bar-{Guid.NewGuid():N}@example.at", $"karte-{Guid.NewGuid():N}@example.at");
        var cashGuest = await spa.PostAsync<CustomerDto>("/api/customers", new CustomerInput("Bea", $"Bar{Guid.NewGuid():N}", cashEmail, null, null, null), Ct);
        var cardGuest = await spa.PostAsync<CustomerDto>("/api/customers", new CustomerInput("Karl", $"Karte{Guid.NewGuid():N}", cardEmail, null, null, null), Ct);
        var cashAppointment = await TestSpa.ReadAsync<AppointmentDto>(await spa.BookAsync(TestSpa.At(10, 0, Day), Ct, customer: cashGuest.Id), Ct);
        var cardAppointment = await TestSpa.ReadAsync<AppointmentDto>(await spa.BookAsync(TestSpa.At(14, 0, Day), Ct, customer: cardGuest.Id), Ct);
        await PayCashDepositAsync(spa, register, cashAppointment, 30m);
        await PayCashDepositAsync(spa, register, cardAppointment, 30m, PaymentMethod.Card);
        var closure = await ClosureTests.CreateAsync(spa, ClosureTests.Input(Day));

        var result = await spa.PostAsync<CancelClosureAppointmentsResult>(Path(spa, closure, "/cancellation"), Request(cashAppointment.Id, cardAppointment.Id), Ct);

        Assert.Equal((2, 0), (result.Refunded, result.RefundsFailed));
        Assert.Equal(["Bar"], result.Notices.Where(n => n.AppointmentId == cashAppointment.Id).Select(n => n.RefundWay));
        Assert.Equal(["Karte"], result.Notices.Where(n => n.AppointmentId == cardAppointment.Id).Select(n => n.RefundWay));
        var cashMail = await fixture.Mailbox.WaitForAsync(cashEmail, m => m.Subject.StartsWith("Absage", StringComparison.Ordinal), Ct);
        Assert.Contains("fällt leider aus.\n\nSie erhalten den Betrag von 30,00 € bar an der Rezeption zurück.\n\nIhr Team", cashMail.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("Zahlungsweg", cashMail.Text, StringComparison.Ordinal);
        var cardMail = await fixture.Mailbox.WaitForAsync(cardEmail, m => m.Subject.StartsWith("Absage", StringComparison.Ordinal), Ct);
        Assert.Contains("Ihre Anzahlung von 30,00 € wird auf dem Zahlungsweg der Einzahlung retourniert.", cardMail.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("bar an der Rezeption", cardMail.Text, StringComparison.Ordinal);

        // Das Protokoll der Schließzeit nennt den Zahlungsweg.
        var notices = await spa.GetAsync<List<ClosureNoticeDto>>(Path(spa, closure, "/notices"), Ct);
        Assert.Equal(["Bar", "Karte"], notices.Select(n => n.RefundWay).OrderBy(w => w, StringComparer.Ordinal));
    }

    // ---------- Storno des Anzahlungsbelegs an der Kassa: nur die Leitung (Ä51) ----------

    [Fact]
    public async Task Einen_Anzahlungsbeleg_storniert_an_der_Kassa_nur_die_Leitung()
    {
        var (spa, _, deposit) = await DepositedAsync();
        var reception = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Reception, [spa.Location.Id]);
        var standort = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Manager, [spa.Location.Id]);
        var manager = await TestUsers.ClientAsync(fixture, Ct, StaffRole.GeneralManager, [spa.Location.Id]);
        var path = $"/api/pos/receipts/{deposit.Id}/void";

        Assert.True((await spa.GetAsync<ReceiptDto>($"/api/pos/receipts/{deposit.Id}", Ct)).IsDepositReceipt);

        // Rezeption und Standortleitung (mit Kassenaufsicht) bekommen 403 mit der Meldung; es entsteht kein Storno-Beleg.
        foreach (var client in new[] { reception, standort })
        {
            var refused = await client.PostAsJsonAsync(path, new VoidRequest("Falsch kassiert"), TestSpa.Json, Ct);
            Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
            Assert.Equal("Anzahlungsbelege storniert nur die Leitung.", (await refused.Content.ReadFromJsonAsync<HttpValidationProblemDetails>(TestSpa.Json, Ct))!.Title);
        }

        Assert.Equal(0, await VoidCountAsync(spa, deposit.Id));

        // Der Manager darf es, mit Pflichtgrund wie bei jedem Storno.
        Assert.Equal(HttpStatusCode.BadRequest, (await manager.PostAsJsonAsync(path, new VoidRequest(""), TestSpa.Json, Ct)).StatusCode);
        var voided = await manager.PostAsJsonAsync(path, new VoidRequest("Falsch kassiert"), TestSpa.Json, Ct);
        Assert.Equal(HttpStatusCode.OK, voided.StatusCode);
        var receipt = await TestSpa.ReadAsync<ReceiptDto>(voided, Ct);
        Assert.Equal((ReceiptKind.Void, deposit.Id, false), (receipt.Kind, receipt.VoidsReceiptId, receipt.IsDepositReceipt));
        Assert.Equal(1, await VoidCountAsync(spa, deposit.Id));
    }

    [Fact]
    public async Task Ein_gewoehnlicher_Beleg_bleibt_fuer_die_Standortleitung_stornierbar_und_der_Inhaber_storniert_auch_den_Anzahlungsbeleg()
    {
        var (spa, _, deposit) = await DepositedAsync();
        var standort = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Manager, [spa.Location.Id]);
        var bill = await spa.PostAsync<BillDto>("/api/pos/bills", new BillInput(deposit.RegisterId, null, []), Ct);
        bill = await spa.PostAsync<BillDto>($"/api/pos/bills/{bill.Id}/lines",
            new BillLineInput(BillLineKind.Custom, null, null, 1, 12m, 0, "Tee", TaxCategory.Normal, null, null, null), Ct);
        var sale = await spa.PostAsync<ReceiptDto>($"/api/pos/bills/{bill.Id}/checkout", new CheckoutRequest(null, [new PaymentInput(PaymentMethod.Cash, 12m, null, null)]), Ct);

        Assert.False(sale.IsDepositReceipt);
        Assert.Equal(HttpStatusCode.OK, (await standort.PostAsJsonAsync($"/api/pos/receipts/{sale.Id}/void", new VoidRequest("Falsch kassiert"), TestSpa.Json, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await spa.Client.PostAsJsonAsync($"/api/pos/receipts/{deposit.Id}/void", new VoidRequest("Falsch kassiert"), TestSpa.Json, Ct)).StatusCode);
    }

    [Fact]
    public async Task Die_Absage_durch_den_Betrieb_retourniert_die_Anzahlung_weiter_auch_wenn_die_Standortleitung_sie_ausloest()
    {
        var (spa, appointment, deposit) = await DepositedAsync();
        var standort = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Manager, [spa.Location.Id]);
        var closure = await ClosureTests.CreateAsync(spa, ClosureTests.Input(Day));

        var response = await standort.PostAsJsonAsync(Path(spa, closure, "/cancellation"), Request(appointment.Id), TestSpa.Json, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await TestSpa.ReadAsync<CancelClosureAppointmentsResult>(response, Ct);
        Assert.Equal((1, 1, 0), (result.Cancelled, result.Refunded, result.RefundsFailed));
        Assert.Equal(1, await VoidCountAsync(spa, deposit.Id));
    }
}
