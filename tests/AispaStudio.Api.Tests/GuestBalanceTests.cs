using AispaStudio.Contracts;

namespace AispaStudio.Api.Tests;

/// <summary>
/// Gutscheine, Pakete, offene Rechnungen, Anzahlungen und Kundenrabatt eines Gastes im Assistenten (Ä317). Das ist die Auskunft der Kassa und braucht deshalb das Recht zu
/// buchen; Beträge kommen aus der Kassa, nie von der KI. Wertgutscheine tragen keinen Kunden: sie gehören zum Gast, wenn sie auf seinem Beleg verkauft oder mit ihm
/// eingelöst wurden.
/// </summary>
public sealed class GuestBalanceTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly DateOnly NextMonday = TestSpa.Monday.AddDays(7);

    private readonly AnswerKit _kit = new(fixture);

    private static Task<CustomerDto> SteinerAsync(TestSpa spa) =>
        spa.PostAsync<CustomerDto>("/api/customers", new CustomerInput("Christian", "Steiner", null, null, null, null, Salutation: CustomerSalutation.Male), Ct);

    private static Task<CashRegisterDto> RegisterAsync(TestSpa spa) =>
        spa.PostAsync<CashRegisterDto>("/api/pos/registers", new CashRegisterInput(spa.Location.Id, $"G-{Guid.NewGuid():N}"[..14], FiscalCountry.AT,
            "Test-Spa GmbH", "Test-Spa GmbH", "AT61 1904 3002 3457 3201", null), Ct);

    private static async Task<BillDto> BillAsync(TestSpa spa, CashRegisterDto register, CustomerDto guest, BillLineInput? line = null, params Guid[] appointments)
    {
        var bill = await spa.PostAsync<BillDto>("/api/pos/bills", new BillInput(register.Id, guest.Id, appointments), Ct);
        return line is null ? bill : await spa.PostAsync<BillDto>($"/api/pos/bills/{bill.Id}/lines", line, Ct);
    }

    private static Task<ReceiptDto> PayAsync(TestSpa spa, BillDto bill, params PaymentInput[] payments) =>
        spa.PostAsync<ReceiptDto>($"/api/pos/bills/{bill.Id}/checkout", new CheckoutRequest(null, payments), Ct);

    private static BillLineInput VoucherLine(decimal value) =>
        new(BillLineKind.Voucher, null, null, 1, value, 0, null, null, null, VoucherKind.MultiPurpose, null);

    private static PaymentInput Cash(decimal amount) => new(PaymentMethod.Cash, amount, null, null);

    [Fact]
    public async Task Gutschein_des_Gastes_nennt_den_Rest_von_30_Euro()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var steiner = await SteinerAsync(spa);
        var register = await RegisterAsync(spa);
        var sold = await PayAsync(spa, await BillAsync(spa, register, steiner, VoucherLine(50m)), Cash(50m));
        var voucher = Assert.Single(sold.IssuedVouchers);
        var visit = await spa.BookOkAsync(TestSpa.At(10), Ct, spa.Anna.Id, customer: steiner.Id);
        await PayAsync(spa, await BillAsync(spa, register, steiner, null, visit.Id), new PaymentInput(PaymentMethod.Voucher, 20m, voucher.Code, null), Cash(59m));
        var open = await spa.BookOkAsync(TestSpa.At(14, date: NextMonday), Ct, spa.Ben.Id, customer: steiner.Id);

        var answer = await _kit.StaffAsync(spa, new AssistantQuestion("hat sie noch einen gutschein", null, Reference: new AssistantReference(open.Id, null, null)),
            role: StaffRole.Reception);

        var item = Assert.Single(answer.Block!.Items!);
        Assert.Equal($"Wertgutschein {voucher.Code}", item.Title);
        Assert.StartsWith("Rest 30,00 EUR", item.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Paket_nennt_die_offenen_Einheiten()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var steiner = await SteinerAsync(spa);
        var register = await RegisterAsync(spa);
        var package = new BillLineInput(BillLineKind.Voucher, null, null, 1, 200m, 0, null, null, null, VoucherKind.SinglePurpose, spa.MassageTreatment.Id, Units: 3);
        var sold = await PayAsync(spa, await BillAsync(spa, register, steiner, package), Cash(200m));
        var voucher = Assert.Single(sold.IssuedVouchers);
        var visit = await spa.BookOkAsync(TestSpa.At(10), Ct, spa.Anna.Id, customer: steiner.Id);
        await PayAsync(spa, await BillAsync(spa, register, steiner, null, visit.Id), new PaymentInput(PaymentMethod.Voucher, 0, voucher.Code, null));

        var block = await _kit.AnswerAsync(spa, "hat herr steiner noch ein paket", role: StaffRole.Reception);

        var item = Assert.Single(block.Items!);
        Assert.Equal("Paket Massage", item.Title);
        Assert.StartsWith("noch 2 von 3 Einheiten", item.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Offene_Rechnung_und_Anzahlung_stehen_in_der_Antwort()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var steiner = await SteinerAsync(spa);
        var register = await RegisterAsync(spa);
        var visit = await spa.BookOkAsync(TestSpa.At(10), Ct, spa.Anna.Id, customer: steiner.Id);
        await BillAsync(spa, register, steiner, null, visit.Id);
        var next = await spa.BookOkAsync(TestSpa.At(14, date: NextMonday), Ct, spa.Ben.Id, customer: steiner.Id);
        await PayAsync(spa, await BillAsync(spa, register, steiner, new BillLineInput(BillLineKind.Deposit, next.Id, null, 1, 20m, 0, null, null, null, null, null)),
            Cash(20m));

        var block = await _kit.AnswerAsync(spa, "offene rechnung und anzahlung herr steiner", role: StaffRole.Reception);

        Assert.Equal("Christian Steiner: 1 offene Rechnung, 1 offene Anzahlung.", block.Text);
        var items = block.Items!;
        Assert.Equal(["Offene Rechnung", "Anzahlung"], items.Select(i => i.Title));
        Assert.StartsWith("79,00 EUR", items[0].Detail, StringComparison.Ordinal);
        Assert.Equal("20,00 EUR für den Termin am Montag, 08.03.2027, 14:00 Uhr", items[1].Detail);
    }

    [Fact]
    public async Task Anzahlung_auf_abgesagtem_Termin_bleibt_offen_mit_Hinweis()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var steiner = await SteinerAsync(spa);
        var register = await RegisterAsync(spa);
        var next = await spa.BookOkAsync(TestSpa.At(14, date: NextMonday), Ct, spa.Ben.Id, customer: steiner.Id);
        await PayAsync(spa, await BillAsync(spa, register, steiner, new BillLineInput(BillLineKind.Deposit, next.Id, null, 1, 20m, 0, null, null, null, null, null)),
            Cash(20m));
        await spa.PostAsync<AppointmentDto>($"/api/appointments/{next.Id}/cancel", new CancelAppointmentRequest("Krank"), Ct);

        var block = await _kit.AnswerAsync(spa, "hat herr steiner noch eine anzahlung", role: StaffRole.Reception);

        var item = Assert.Single(block.Items!);
        Assert.Equal(("Anzahlung", "20,00 EUR für den Termin am Montag, 08.03.2027, 14:00 Uhr (abgesagt)"), (item.Title, item.Detail));
    }

    [Fact]
    public async Task Kundenrabatt_kommt_aus_der_Kartei()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var steiner = await SteinerAsync(spa);
        await spa.PutAsync<CustomerDto>($"/api/customers/{steiner.Id}/discount", new CustomerDiscountInput(10m, CustomerDiscountScope.Treatments), Ct);

        var block = await _kit.AnswerAsync(spa, "welchen rabatt hat herr steiner", role: StaffRole.Reception);

        var item = Assert.Single(block.Items!);
        Assert.Equal(("Kundenrabatt", "10 % auf Behandlungen"), (item.Title, item.Detail));
    }

    [Fact]
    public async Task Therapeutin_ohne_Kassarecht_erfaehrt_dass_das_Recht_fehlt()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await SteinerAsync(spa);

        var block = await _kit.AnswerAsync(spa, "hat herr steiner eine offene rechnung", role: StaffRole.Therapist, employeeId: spa.Anna.Id);

        Assert.Equal(AssistantBlockKind.Denied, block.Kind);
        Assert.Equal("Dafür fehlt die Berechtigung. Gutscheine, Rechnungen und Anzahlungen sieht die Rezeption an der Kassa.", block.Text);
    }

    /// <summary>
    /// Kurze Eingaben zur Bedienung („rechnung splitten“) nennen keinen Gast: ein Wort ohne Anrede zählt nur als Name, wenn es den Gast gibt, sonst geht die Frage wie vor
    /// Ä317 an Klassifizierer und Hilfe (Nachprüfung N1).
    /// </summary>
    [Theory]
    [InlineData("gutschein verlängern")]
    [InlineData("gutschein bestellen")]
    [InlineData("gutschein gültigkeit")]
    [InlineData("rabatt geben")]
    [InlineData("rabatt aktion")]
    [InlineData("rabatt mitarbeiter")]
    [InlineData("rechnung korrigieren")]
    [InlineData("rechnung splitten")]
    [InlineData("rechnung mailen")]
    [InlineData("anzahlung zurückzahlen")]
    [InlineData("anzahlung online")]
    [InlineData("pakete übersicht")]
    [InlineData("verlauf löschen")]
    [InlineData("verlauf exportieren")]
    [InlineData("historie filtern")]
    [InlineData("fragebogen drucken")]
    [InlineData("fragebogen vorlage")]
    [InlineData("anamnese einstellen")]
    public async Task Kurze_Bedienfrage_ohne_Gast_geht_an_die_Hilfe(string question)
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await SteinerAsync(spa);

        var answer = await _kit.StaffAsync(spa, new AssistantQuestion(question, null), role: StaffRole.Reception);

        Assert.False(answer.Block?.Topic is "GuestBalance" or "GuestHistory" or "GuestAnamnesis", $"{answer.Block?.Topic}: {answer.Text}");
        Assert.DoesNotContain("Keinen Gast", answer.Text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("rechnung steiner", "GuestBalance")]
    [InlineData("rechnung steinr", "GuestBalance")]
    [InlineData("historie christian steiner", "GuestHistory")]
    [InlineData("fragebogen steiner", "GuestAnamnesis")]
    public async Task Kurze_Eingabe_mit_einem_Gast_ohne_Anrede_bleibt_beim_Gast(string question, string topic)
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await SteinerAsync(spa);

        var answer = await _kit.StaffAsync(spa, new AssistantQuestion(question, null), role: StaffRole.Reception);

        Assert.Equal((topic, AssistantBlockKind.Answer), (answer.Block?.Topic, answer.Block?.Kind));
    }
}
