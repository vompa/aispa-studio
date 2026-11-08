using System.Net;
using System.Net.Http.Json;
using AispaStudio.Api.Online;
using AispaStudio.Contracts;
using Wolverine.Attributes;

namespace AispaStudio.Api.Tests;

/// <summary>Kartenterminal: Zahlung über die Queue, Kassieren genau einmal, Storno am Terminal, Gerät im Netz über die Windows-App.</summary>
public sealed class CardTerminalTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Testterminal_genehmigt_und_die_Zahlung_steht_genau_einmal_auf_dem_Beleg()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var (register, terminal) = await SetupAsync(spa, TerminalProvider.Test);
        var bill = await OpenBillAsync(spa, register, 40m);

        var tooMuch = await spa.Client.PostAsJsonAsync($"/api/pos/bills/{bill.Id}/card-payments", new CardPaymentRequest(terminal.Id, 41m), TestSpa.Json, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, tooMuch.StatusCode);

        var started = await spa.PostAsync<CardPaymentDto>($"/api/pos/bills/{bill.Id}/card-payments", new CardPaymentRequest(terminal.Id, 40m), Ct);
        var approved = await WaitAsync(spa, started.Id);
        Assert.Equal(CardPaymentStatus.Approved, approved.Status);
        Assert.Equal("Testkarte", approved.CardBrand);

        // Der Tagesabschluss wartet, solange genehmigtes Geld keinem Beleg zugeordnet ist.
        var closing = await spa.Client.PostAsJsonAsync($"/api/pos/registers/{register.Id}/day-closings", new DayClosingInput(0, null), TestSpa.Json, Ct);
        Assert.Equal(HttpStatusCode.Conflict, closing.StatusCode);

        var receipt = await spa.PostAsync<ReceiptDto>($"/api/pos/bills/{bill.Id}/checkout",
            new CheckoutRequest(null, [new PaymentInput(PaymentMethod.Card, 40m, null, null, approved.Id)]), Ct);
        Assert.Equal(approved.Reference, Assert.Single(receipt.Payments).Reference);
        Assert.Equal(receipt.Id, (await spa.GetAsync<CardPaymentDto>($"/api/pos/card-payments/{approved.Id}", Ct)).ReceiptId);

        var second = await OpenBillAsync(spa, register, 40m);
        var reused = await spa.Client.PostAsJsonAsync($"/api/pos/bills/{second.Id}/checkout",
            new CheckoutRequest(null, [new PaymentInput(PaymentMethod.Card, 40m, null, null, approved.Id)]), TestSpa.Json, Ct);
        Assert.Equal(HttpStatusCode.NotFound, reused.StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await spa.Client.PostAsJsonAsync($"/api/pos/registers/{register.Id}/day-closings", new DayClosingInput(0, null), TestSpa.Json, Ct)).StatusCode);
    }

    [Fact]
    public async Task Abgelehnt_und_storniert()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var (register, terminal) = await SetupAsync(spa, TerminalProvider.Test);
        var bill = await OpenBillAsync(spa, register, 40.13m);

        var declined = await WaitAsync(spa, (await spa.PostAsync<CardPaymentDto>($"/api/pos/bills/{bill.Id}/card-payments",
            new CardPaymentRequest(terminal.Id, 40.13m), Ct)).Id);
        Assert.Equal(CardPaymentStatus.Declined, declined.Status);
        var notApproved = await spa.Client.PostAsJsonAsync($"/api/pos/bills/{bill.Id}/checkout",
            new CheckoutRequest(null, [new PaymentInput(PaymentMethod.Card, 40.13m, null, null, declined.Id)]), TestSpa.Json, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, notApproved.StatusCode);

        var approved = await WaitAsync(spa, (await spa.PostAsync<CardPaymentDto>($"/api/pos/bills/{bill.Id}/card-payments",
            new CardPaymentRequest(terminal.Id, 20m), Ct)).Id);
        Assert.Equal(CardPaymentStatus.Approved, approved.Status);
        var reversal = await spa.PostAsync<CardPaymentDto>($"/api/pos/card-payments/{approved.Id}/cancel", new { }, Ct);
        Assert.Equal(CardPaymentStatus.ReversalPending, reversal.Status);
        Assert.Equal(CardPaymentStatus.Reversed, (await WaitAsync(spa, approved.Id)).Status);

        Assert.Equal(HttpStatusCode.NoContent, (await spa.Client.DeleteAsync($"/api/pos/bills/{bill.Id}", Ct)).StatusCode);
    }

    [Fact]
    public async Task Geraet_im_Netz_meldet_das_Ergebnis_ueber_die_Windows_App()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var (register, terminal) = await SetupAsync(spa, TerminalProvider.Local);
        var bill = await OpenBillAsync(spa, register, 30m);
        using var reception = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Reception, [spa.Location.Id]);
        using var elsewhere = await TestUsers.ClientAsync(fixture, Ct); // anderes Spa

        var started = await spa.PostAsync<CardPaymentDto>($"/api/pos/bills/{bill.Id}/card-payments", new CardPaymentRequest(terminal.Id, 30m), Ct);
        var jobs = await reception.GetFromJsonAsync<List<CardPaymentDto>>($"/api/pos/terminals/{terminal.Id}/jobs", TestSpa.Json, Ct);
        Assert.Equal(started.Id, Assert.Single(jobs!).Id);
        Assert.Empty((await elsewhere.GetFromJsonAsync<List<CardPaymentDto>>($"/api/pos/terminals/{terminal.Id}/jobs", TestSpa.Json, Ct))!);

        // An der Kasse abgebrochen, das Gerät hat aber schon genehmigt: das Geld darf nicht verloren gehen.
        await spa.PostAsync<CardPaymentDto>($"/api/pos/card-payments/{started.Id}/cancel", new { }, Ct);
        var result = await reception.PostAsJsonAsync($"/api/pos/card-payments/{started.Id}/result",
            new CardTerminalResult(true, null, "Maestro", "**** 1234", "123456", "ZVT-4711"), TestSpa.Json, Ct);
        var reported = (await result.Content.ReadFromJsonAsync<CardPaymentDto>(TestSpa.Json, Ct))!;
        Assert.Equal(CardPaymentStatus.Approved, reported.Status);
        Assert.Contains("doch genehmigt", reported.Message, StringComparison.Ordinal);

        var receipt = await spa.PostAsync<ReceiptDto>($"/api/pos/bills/{bill.Id}/checkout",
            new CheckoutRequest(null, [new PaymentInput(PaymentMethod.Card, 30m, null, null, reported.Id)]), Ct);
        Assert.Equal("ZVT-4711", Assert.Single(receipt.Payments).Reference);
    }

    [Fact]
    public async Task Storno_eines_Kartenbelegs_schreibt_den_Betrag_am_Terminal_gut()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var (register, terminal) = await SetupAsync(spa, TerminalProvider.Test);
        var bill = await OpenBillAsync(spa, register, 40m);
        var approved = await WaitAsync(spa, (await spa.PostAsync<CardPaymentDto>($"/api/pos/bills/{bill.Id}/card-payments",
            new CardPaymentRequest(terminal.Id, 40m), Ct)).Id);
        var receipt = await spa.PostAsync<ReceiptDto>($"/api/pos/bills/{bill.Id}/checkout",
            new CheckoutRequest(null, [new PaymentInput(PaymentMethod.Card, 40m, null, null, approved.Id)]), Ct);

        await spa.PostAsync<ReceiptDto>($"/api/pos/receipts/{receipt.Id}/void", new VoidRequest("Falsche Behandlung kassiert"), Ct);
        var refunded = await WaitAsync(spa, approved.Id, until: CardPaymentStatus.Refunded);

        Assert.Equal(CardPaymentStatus.Refunded, refunded.Status);
        Assert.Equal("Gutgeschrieben (Testterminal).", refunded.Message);
        Assert.Equal(receipt.Id, refunded.ReceiptId);
    }

    [Fact]
    public async Task Gutschrift_am_Geraet_im_Netz_kommt_als_Auftrag_und_nennt_einen_Fehlschlag()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var (register, terminal) = await SetupAsync(spa, TerminalProvider.Local);
        using var reception = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Reception, [spa.Location.Id]);
        var bill = await OpenBillAsync(spa, register, 30m);
        var started = await spa.PostAsync<CardPaymentDto>($"/api/pos/bills/{bill.Id}/card-payments", new CardPaymentRequest(terminal.Id, 30m), Ct);
        await reception.PostAsJsonAsync($"/api/pos/card-payments/{started.Id}/result",
            new CardTerminalResult(true, null, "Maestro", "**** 1234", "123456", "17"), TestSpa.Json, Ct);
        var receipt = await spa.PostAsync<ReceiptDto>($"/api/pos/bills/{bill.Id}/checkout",
            new CheckoutRequest(null, [new PaymentInput(PaymentMethod.Card, 30m, null, null, started.Id)]), Ct);

        await spa.PostAsync<ReceiptDto>($"/api/pos/receipts/{receipt.Id}/void", new VoidRequest("Doppelt kassiert"), Ct);
        List<CardPaymentDto>? jobs = null;
        for (var i = 0; i < 100 && jobs is not { Count: > 0 }; i++)
        {
            jobs = await reception.GetFromJsonAsync<List<CardPaymentDto>>($"/api/pos/terminals/{terminal.Id}/jobs", TestSpa.Json, Ct);
            await Task.Delay(50, Ct);
        }

        var job = Assert.Single(jobs!);
        Assert.Equal(CardPaymentStatus.RefundPending, job.Status);
        Assert.Equal(30m, job.Amount);
        var failed = await (await reception.PostAsJsonAsync($"/api/pos/card-payments/{started.Id}/result",
            new CardTerminalResult(false, "Gutschrift nicht freigeschaltet", null, null, null, null), TestSpa.Json, Ct))
            .Content.ReadFromJsonAsync<CardPaymentDto>(TestSpa.Json, Ct);

        Assert.Equal(CardPaymentStatus.RefundFailed, failed!.Status);
        Assert.Contains("Gutschrift nicht freigeschaltet", failed.Message, StringComparison.Ordinal);
        Assert.Contains("direkt am Terminal", failed.Message, StringComparison.Ordinal);
        Assert.Empty((await reception.GetFromJsonAsync<List<CardPaymentDto>>($"/api/pos/terminals/{terminal.Id}/jobs", TestSpa.Json, Ct))!);
    }

    [Fact]
    public async Task Geraet_im_Netz_merkt_sich_sein_Protokoll()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);

        var opi = await spa.PostAsync<PaymentTerminalDto>("/api/pos/terminals",
            new PaymentTerminalInput(spa.Location.Id, "Empfang OPI", TerminalProvider.Local, "192.168.1.60:20002", TerminalProtocol.Opi), Ct);
        var zvt = await spa.PostAsync<PaymentTerminalDto>("/api/pos/terminals",
            new PaymentTerminalInput(spa.Location.Id, "Empfang ZVT", TerminalProvider.Local, "192.168.1.50:20007"), Ct);
        var unknown = await spa.Client.PostAsJsonAsync("/api/pos/terminals",
            new PaymentTerminalInput(spa.Location.Id, "Unbekannt", TerminalProvider.Local, "192.168.1.70", (TerminalProtocol)7), TestSpa.Json, Ct);
        var listed = await spa.GetAsync<List<PaymentTerminalDto>>($"/api/pos/terminals?locationId={spa.Location.Id}", Ct);

        Assert.Equal(TerminalProtocol.Opi, opi.Protocol);
        Assert.Equal(TerminalProtocol.Zvt, zvt.Protocol);
        Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);
        Assert.Equal(TerminalProtocol.Opi, Assert.Single(listed, t => t.Id == opi.Id).Protocol);
    }

    [Fact]
    public async Task Auftrag_ans_Terminal_wird_nach_einem_Fehler_nicht_wiederholt()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var (register, terminal) = await SetupAsync(spa, TerminalProvider.Test);
        var bill = await OpenBillAsync(spa, register, ThrowingCardTerminal.FailingAmount);
        var before = fixture.CardTerminal.FailedAttempts;

        await spa.PostAsync<CardPaymentDto>($"/api/pos/bills/{bill.Id}/card-payments", new CardPaymentRequest(terminal.Id, ThrowingCardTerminal.FailingAmount), Ct);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));
        while (fixture.CardTerminal.FailedAttempts == before)
        {
            await Task.Delay(50, timeout.Token);
        }

        // Länger als eine Wiederholung im Test bräuchte: 1 s Wartezeit plus 5 s Abfragetakt für geplante Nachrichten.
        await Task.Delay(TimeSpan.FromSeconds(8), Ct);
        Assert.Equal(before + 1, fixture.CardTerminal.FailedAttempts);
    }

    [Fact]
    public void Warteliste_ist_von_der_Wiederholregel_ausgenommen() =>
        Assert.True(typeof(CheckWaitlistHandler).IsDefined(typeof(MoveToErrorQueueOnAttribute), false));

    private static async Task<(CashRegisterDto Register, PaymentTerminalDto Terminal)> SetupAsync(TestSpa spa, TerminalProvider provider)
    {
        var register = await spa.PostAsync<CashRegisterDto>("/api/pos/registers", new CashRegisterInput(spa.Location.Id, $"T-{Guid.NewGuid():N}"[..14],
            FiscalCountry.AT, "Test-Spa GmbH\nTeststraße 1, 1010 Wien", "Test-Spa GmbH", null, null), Ct);
        var terminal = await spa.PostAsync<PaymentTerminalDto>("/api/pos/terminals",
            new PaymentTerminalInput(spa.Location.Id, "Empfang", provider, provider == TerminalProvider.Local ? "192.168.1.50:20007" : null), Ct);
        Assert.Contains(await spa.GetAsync<List<PaymentTerminalDto>>($"/api/pos/terminals?locationId={spa.Location.Id}", Ct), t => t.Id == terminal.Id);
        return (register, terminal);
    }

    private static async Task<BillDto> OpenBillAsync(TestSpa spa, CashRegisterDto register, decimal amount)
    {
        var bill = await spa.PostAsync<BillDto>("/api/pos/bills", new BillInput(register.Id, null, []), Ct);
        return await spa.PostAsync<BillDto>($"/api/pos/bills/{bill.Id}/lines",
            new BillLineInput(BillLineKind.Custom, null, null, 1, amount, 0, "Massage", TaxCategory.Normal, null, null, null), Ct);
    }

    private static async Task<CardPaymentDto> WaitAsync(TestSpa spa, Guid id, CardPaymentStatus? until = null)
    {
        for (var i = 0; ; i++)
        {
            var payment = await spa.GetAsync<CardPaymentDto>($"/api/pos/card-payments/{id}", Ct);
            var done = until is { } wanted
                ? payment.Status == wanted
                : payment.Status is not (CardPaymentStatus.Pending or CardPaymentStatus.ReversalPending or CardPaymentStatus.RefundPending);
            if (done || i == 100)
            {
                return payment;
            }

            await Task.Delay(100, Ct);
        }
    }
}
