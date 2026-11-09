using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;
using AispaStudio.Desktop.ViewModels;

namespace AispaStudio.Desktop.Tests;

public sealed class PosTests
{
    private sealed class SampleTime : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => SpaSample.At(11, 40);

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna");
    }

    private static StubApi Api() => new StubApi()
        .On("GET /api/locations", new List<LocationDto> { SpaSample.Vienna })
        .On("GET /api/pos/registers", new List<CashRegisterDto> { SpaSample.Register })
        .On("GET /api/pos/articles", SpaSample.Articles)
        .On($"GET /api/pos/locations/{SpaSample.LocationId}/day-list?date=2026-10-19", SpaSample.DayList())
        .On($"GET /api/pos/registers/{SpaSample.Register.Id}/bills", new List<BillDto>())
        .On("POST /api/pos/bills", SpaSample.Bill())
        .On($"POST /api/pos/bills/{SpaSample.BillId}/checkout", SpaSample.Receipt());

    [Theory]
    [InlineData("12,50", 12.5)]
    [InlineData("12.50", 12.5)]
    [InlineData("1.234,5", 1234.5)]
    [InlineData("", 0)]
    [InlineData("100 €", 100)]
    public void Betraege_mit_Komma_oder_Punkt(string text, double expected) => Assert.Equal((decimal)expected, Money.Parse(text));

    [Fact]
    public void Unlesbare_Betraege_sind_null()
    {
        Assert.Null(Money.Parse("zwölf"));
        Assert.Null(Money.Parse("-5"));
    }

    [Fact]
    public async Task Eintritte_stehen_unter_heute_abzurechnen_und_kommen_mit_Personenzahl_auf_die_Rechnung()
    {
        var visits = SpaSample.UnbilledVisits();
        var stub = Api()
            .On($"GET /api/pos/locations/{SpaSample.LocationId}/day-list?date=2026-10-19", SpaSample.DayList(visits))
            .On($"POST /api/pos/bills/{SpaSample.BillId}/lines", SpaSample.Bill());
        var pos = new PosViewModel(stub.Client(), new SampleTime());
        await pos.InitializeAsync(SpaSample.LocationId);
        await pos.LoadDayAsync();

        // Termine und Eintritte nach Uhrzeit gemischt; den Preis rechnet der Server (Tagesliste), die App zeigt ihn nur an.
        Assert.Equal(5, pos.Unbilled.Count);
        Assert.Equal(pos.Unbilled.OrderBy(u => u.Time, StringComparer.Ordinal).Select(u => u.Time), pos.Unbilled.Select(u => u.Time));
        var sauna = pos.Unbilled.Single(u => u.Visit?.Id == SpaSample.SaunaVisitId);
        Assert.Equal("2 × Eintritt Finnische Sauna", sauna.Treatment);
        Assert.Equal("58,00 €", sauna.Price);
        Assert.Equal("kein Preis", pos.Unbilled.Single(u => u.Customer == "Brigitte Müller").Price);

        // Ohne offene Rechnung: neue Rechnung mit dem Eintritt, Kundin vom Eintritt.
        await pos.AddUnbilledCommand.ExecuteAsync(sauna);
        var created = stub.Calls.Last(c => c.Route == "POST /api/pos/bills").Body!;
        Assert.Contains($"\"areaVisitIds\":[\"{SpaSample.SaunaVisitId}\"]", created, StringComparison.Ordinal);
        Assert.Contains($"\"customerId\":\"{visits[0].CustomerId}\"", created, StringComparison.Ordinal);
        Assert.NotNull(pos.Bill);

        // Mit offener Rechnung: als Position vom Typ Eintritt.
        await pos.AddUnbilledCommand.ExecuteAsync(sauna);
        var line = stub.Calls.Last(c => c.Route == $"POST /api/pos/bills/{SpaSample.BillId}/lines").Body!;
        Assert.Contains("\"kind\":\"AreaVisit\"", line, StringComparison.Ordinal);
        Assert.Contains($"\"areaVisitId\":\"{SpaSample.SaunaVisitId}\"", line, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Paket_verkaufen_und_beim_Kassieren_den_Termin_abziehen()
    {
        var stub = Api()
            .On($"GET /api/pos/customers/{SpaSample.Paying.CustomerId}/packages", SpaSample.Packages())
            .On("GET /api/treatments", SpaSample.Treatments.ToList())
            .On($"POST /api/pos/bills/{SpaSample.BillId}/lines", SpaSample.Bill());
        var pos = new PosViewModel(stub.Client(), new SampleTime());
        await pos.InitializeAsync(SpaSample.LocationId);
        await pos.AddAppointmentAsync(SpaSample.Paying);

        var hint = Assert.Single(pos.PackageHints);
        Assert.StartsWith("Paket ", hint.Text, StringComparison.Ordinal);
        Assert.EndsWith(": 7 von 10 offen, gültig bis 30.06.2027", hint.Text, StringComparison.Ordinal);
        Assert.Equal("1 Termin abziehen", hint.ButtonText);
        pos.UsePackageCommand.Execute(hint);
        var voucher = pos.Payments.First(p => p.IsVoucher);
        Assert.Equal(("PK7Q-M2XA-9RDT", SpaSample.Paying.Price), (voucher.VoucherCode, Money.Parse(voucher.Amount)));

        // Paket verkaufen: Anzahl, Paketpreis und Ablaufdatum gehen mit.
        pos.VoucherTreatment = pos.Treatments.First(t => t.Id == SpaSample.Paying.TreatmentId);
        pos.PackageUnits = "10";
        pos.PackagePrice = "699";
        pos.PackageValidUntil = "30.6.2027";
        Assert.True(pos.SellPackageCommand.CanExecute(null));
        await pos.SellPackageCommand.ExecuteAsync(null);
        var line = System.Text.Json.JsonSerializer.Deserialize<BillLineInput>(stub.Calls.Last(c => c.Route == $"POST /api/pos/bills/{SpaSample.BillId}/lines").Body!, ApiClient.Json)!;
        Assert.Equal((BillLineKind.Voucher, VoucherKind.SinglePurpose, 10, 699m, new DateOnly(2027, 6, 30)),
            (line.Kind, line.VoucherKind, line.Units, line.UnitPrice, line.ValidUntil));
        pos.PackageUnits = "1";
        Assert.False(pos.SellPackageCommand.CanExecute(null));
    }

    [Fact]
    public async Task Gutschein_und_Rest_bar_mit_Rueckgeld()
    {
        var stub = Api();
        var pos = new PosViewModel(stub.Client(), new SampleTime());
        await pos.InitializeAsync(SpaSample.LocationId);
        Assert.Equal(4, pos.Payments.Count);
        Assert.Equal(3, pos.Unbilled.Count);

        await pos.AddAppointmentAsync(SpaSample.Paying);
        Assert.Equal(138.80m, pos.Bill!.Open);
        Assert.False(pos.CanPay);

        var voucher = pos.Payments.First(p => p.IsVoucher);
        voucher.Amount = "50";
        Assert.False(pos.CanPay); // Gutschein ohne Code
        voucher.VoucherCode = "GS-7K3M-Q9";
        Assert.Equal("Noch offen: 88,80 €", pos.CoverText);

        var cash = pos.Payments.First(p => p.Method == PaymentMethod.Cash);
        pos.FillRestCommand.Execute(cash);
        Assert.Equal("88,80", cash.Amount);
        cash.Amount = "100";
        Assert.Equal("Alles gedeckt", pos.CoverText);
        Assert.Equal("Rückgeld: 11,20 €", pos.ChangeText);
        Assert.True(pos.CanPay);

        await pos.PayCommand.ExecuteAsync(null);
        var body = stub.Calls.Single(c => c.Route.EndsWith("/checkout", StringComparison.Ordinal)).Body!;
        Assert.Contains("GS-7K3M-Q9", body, StringComparison.Ordinal);
        Assert.NotNull(pos.Receipt);
        Assert.False(pos.HasBill);
        Assert.Equal("11,20 €", pos.Receipt.Change);
    }

    [Fact]
    public async Task Rabatt_auf_eine_Position_geht_an_den_Server_und_steht_in_der_Zeile()
    {
        var stub = Api().On($"PUT /api/pos/bills/{SpaSample.BillId}/lines/{SpaSample.TreatmentLineId}/discount", SpaSample.Bill(discount: 15m));
        var pos = new PosViewModel(stub.Client(), new SampleTime());
        await pos.InitializeAsync(SpaSample.LocationId);
        await pos.AddAppointmentAsync(SpaSample.Paying);

        var line = pos.Lines[0];
        var voucherSale = pos.Lines[1] with { Line = pos.Lines[1].Line with { Kind = BillLineKind.Voucher } };
        pos.EditDiscountCommand.Execute(line);
        var titleWhileEditing = pos.DiscountTitle;
        pos.DiscountInput = "fünfzehn";
        var canApplyUnreadable = pos.ApplyDiscountCommand.CanExecute(null);
        pos.DiscountInput = "10";
        var percentPreview = pos.DiscountPreview;
        pos.DiscountInAmount = true;
        pos.DiscountInput = "15";
        var amountPreview = pos.DiscountPreview;
        var amountTitle = pos.DiscountTitle;
        await pos.ApplyDiscountCommand.ExecuteAsync(null);

        Assert.True(line.CanDiscount);
        Assert.False(voucherSale.CanDiscount);
        Assert.Equal("Rabatt auf „Klassische Massage (Lena Huber)“ in %", titleWhileEditing);
        Assert.Equal("Rabatt auf „Klassische Massage (Lena Huber)“ in EUR", amountTitle);
        Assert.False(canApplyUnreadable);
        Assert.Equal("= −8,90 €", percentPreview);
        Assert.Equal("= 16,9 %", amountPreview);
        Assert.Contains("\"discount\":15", stub.Calls.Single(c => c.Route.EndsWith("/discount", StringComparison.Ordinal)).Body, StringComparison.Ordinal);
        Assert.Equal("20 % USt · Rabatt −15,00 €", pos.Lines[0].Tax);
        Assert.Equal("74,00 €", pos.Lines[0].Amount);
        Assert.False(pos.IsDiscounting);
    }

    [Fact]
    public async Task Rabatt_in_Prozent_geht_als_Prozent_an_den_Server_und_nennt_die_Grenze()
    {
        var stub = Api()
            .On("GET /api/pos/settings", new PosSettingsDto(20m, false))
            .On($"PUT /api/pos/bills/{SpaSample.BillId}/lines/{SpaSample.TreatmentLineId}/discount", SpaSample.Bill(discount: 8.90m));
        var pos = new PosViewModel(stub.Client(), new SampleTime());
        await pos.InitializeAsync(SpaSample.LocationId);
        await pos.AddAppointmentAsync(SpaSample.Paying);

        pos.EditDiscountCommand.Execute(pos.Lines[0]);
        pos.DiscountInput = "10 %";
        await pos.ApplyDiscountCommand.ExecuteAsync(null);

        Assert.Equal("Bis 20 % möglich, darüber gibt die Standortleitung mit ihrer PIN frei.", pos.DiscountLimitHint);
        var body = stub.Calls.Single(c => c.Route.EndsWith("/discount", StringComparison.Ordinal)).Body!;
        Assert.Contains("\"percent\":10", body, StringComparison.Ordinal);
        pos.EditDiscountCommand.Execute(pos.Lines[0]);
        Assert.Equal("10", pos.DiscountInput);
    }

    [Fact]
    public async Task Rabatt_ueber_der_Grenze_braucht_Freigabe_mit_Leitungs_PIN()
    {
        var manager = new QuickSwitchPerson("u-leitung", "Maria Leitner", StaffRole.Manager);
        var stub = Api()
            .On("GET /api/pos/settings", new PosSettingsDto(20m, false))
            .On($"GET /api/pos/approvers?locationId={SpaSample.LocationId}", new List<QuickSwitchPerson> { manager })
            .On($"PUT /api/pos/bills/{SpaSample.BillId}/lines/{SpaSample.TreatmentLineId}/discount", SpaSample.Bill(discount: 19.75m));
        var pos = new PosViewModel(stub.Client(), new SampleTime());
        await pos.InitializeAsync(SpaSample.LocationId);
        await pos.AddAppointmentAsync(SpaSample.Paying);

        pos.EditDiscountCommand.Execute(pos.Lines[0]);
        pos.DiscountInput = "10";
        var withinLimit = pos.NeedsApproval;
        pos.DiscountInput = "25";
        for (var i = 0; i < 50 && pos.Approver is null; i++)
        {
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        var withoutPin = pos.ApplyDiscountCommand.CanExecute(null);
        pos.ApprovalPin = "4711";
        var withPin = pos.ApplyDiscountCommand.CanExecute(null);
        await pos.ApplyDiscountCommand.ExecuteAsync(null);

        Assert.False(withinLimit);
        Assert.True(pos.Approvers.Count == 1);
        Assert.Equal(manager, pos.Approver);
        Assert.False(withoutPin);
        Assert.True(withPin);
        var body = stub.Calls.Single(c => c.Route.EndsWith("/discount", StringComparison.Ordinal)).Body!;
        Assert.Contains("\"approval\":{\"userId\":\"u-leitung\",\"pin\":\"4711\"}", body, StringComparison.Ordinal);
        Assert.Equal("", pos.ApprovalPin);
    }

    /// <summary>Betragsfelder melden jedes getippte Zeichen (Slice 130); die Freigebenden kommen trotzdem nur einmal vom Server.</summary>
    [Fact]
    public async Task Rabatt_ueber_der_Grenze_tippen_laedt_die_Freigebenden_nur_einmal()
    {
        var manager = new QuickSwitchPerson("u-leitung", "Maria Leitner", StaffRole.Manager);
        var slowAnswer = new TaskCompletionSource();
        var approversRoute = $"GET /api/pos/approvers?locationId={SpaSample.LocationId}";
        var stub = Api()
            .On("GET /api/pos/settings", new PosSettingsDto(20m, false))
            .On(approversRoute, new List<QuickSwitchPerson> { manager })
            .Hold(approversRoute, slowAnswer.Task);
        var pos = new PosViewModel(stub.Client(), new SampleTime());
        await pos.InitializeAsync(SpaSample.LocationId);
        await pos.AddAppointmentAsync(SpaSample.Paying);
        pos.EditDiscountCommand.Execute(pos.Lines[0]);

        pos.DiscountInput = "2";
        pos.DiscountInput = "25";
        pos.DiscountInput = "25,";
        pos.DiscountInput = "25,0";
        pos.DiscountInput = "25,00";
        slowAnswer.SetResult();
        for (var i = 0; i < 50 && pos.Approver is null; i++)
        {
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        Assert.Equal(manager, pos.Approver);
        Assert.Single(stub.Calls, c => c.Route == approversRoute);
    }

    /// <summary>Gibt es am Standort niemanden, der freigeben darf, fragt das weitere Tippen nicht bei jedem Zeichen erneut nach.</summary>
    [Fact]
    public async Task Rabatt_ueber_der_Grenze_ohne_Freigebende_fragt_beim_Tippen_nur_einmal()
    {
        var approversRoute = $"GET /api/pos/approvers?locationId={SpaSample.LocationId}";
        var stub = Api()
            .On("GET /api/pos/settings", new PosSettingsDto(20m, false))
            .On(approversRoute, new List<QuickSwitchPerson>());
        var pos = new PosViewModel(stub.Client(), new SampleTime());
        await pos.InitializeAsync(SpaSample.LocationId);
        await pos.AddAppointmentAsync(SpaSample.Paying);
        pos.EditDiscountCommand.Execute(pos.Lines[0]);

        pos.DiscountInput = "25";
        for (var i = 0; i < 50 && !stub.Calls.Any(c => c.Route == approversRoute); i++)
        {
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        await Task.Delay(50, TestContext.Current.CancellationToken);
        pos.DiscountInput = "25,";
        pos.DiscountInput = "25,0";
        pos.DiscountInput = "25,00";
        await Task.Delay(50, TestContext.Current.CancellationToken);

        Assert.Empty(pos.Approvers);
        Assert.Single(stub.Calls, c => c.Route == approversRoute);
    }

    [Fact]
    public async Task Barzahlungsrabatt_wird_angezeigt_und_nur_bei_reiner_Barzahlung_abgezogen()
    {
        var stub = Api().On("POST /api/pos/bills", SpaSample.Bill() with { CashDiscountPercent = 3m, CashDiscount = 4.16m });
        var pos = new PosViewModel(stub.Client(), new SampleTime());
        await pos.InitializeAsync(SpaSample.LocationId);
        await pos.AddAppointmentAsync(SpaSample.Paying);

        var cash = pos.Payments.First(p => p.Method == PaymentMethod.Cash);
        pos.FillRestCommand.Execute(cash);
        var filled = cash.Amount;
        var cashOnly = pos.CoverText;
        pos.Payments.First(p => p.Method == PaymentMethod.Card).Amount = "10";
        cash.Amount = "124,64";
        var mixed = pos.CoverText;

        Assert.Equal("Bar 4,16 € günstiger (3 % Barzahlungsrabatt)", pos.CashDiscountText);
        Assert.Equal("134,64", filled);
        Assert.Equal("Alles gedeckt", cashOnly);
        Assert.Equal("Noch offen: 4,16 €", mixed);
    }

    [Fact]
    public async Task Gutschein_verkaufen_legt_eine_Rechnung_an_und_zeigt_den_Code_auf_dem_Beleg()
    {
        var stub = Api()
            .On("GET /api/treatments", SpaSample.Treatments.ToList())
            .On($"POST /api/pos/bills/{SpaSample.BillId}/lines", SpaSample.Bill(withArticle: false, voucher: 50m))
            .On($"POST /api/pos/bills/{SpaSample.BillId}/checkout", SpaSample.Receipt() with
            {
                IssuedVouchers = [new VoucherDto(Guid.NewGuid(), "GS-9Q2A-X1", VoucherKind.MultiPurpose, 50m, 50m, null, VoucherStatus.Active, null)],
            });
        var pos = new PosViewModel(stub.Client(), new SampleTime());
        await pos.InitializeAsync(SpaSample.LocationId);

        var canSellEmpty = pos.SellVoucherCommand.CanExecute(null);
        pos.VoucherValue = "50";
        await pos.SellVoucherCommand.ExecuteAsync(null);
        var lineBody = stub.Calls.Single(c => c.Route.EndsWith("/lines", StringComparison.Ordinal)).Body!;
        pos.VoucherTreatment = SpaSample.Treatments[2];
        await pos.SellTreatmentVoucherCommand.ExecuteAsync(null);
        var treatmentBody = stub.Calls.Last(c => c.Route.EndsWith("/lines", StringComparison.Ordinal)).Body!;
        var voucherLine = pos.Lines.Last();
        pos.Payments.First(p => p.Method == PaymentMethod.Card).Amount = Money.Input(pos.Bill!.Open);
        await pos.PayCommand.ExecuteAsync(null);

        Assert.False(canSellEmpty);
        Assert.Equal(SpaSample.Treatments.Length, pos.Treatments.Count);
        Assert.Contains("\"voucherKind\":\"MultiPurpose\"", lineBody, StringComparison.Ordinal);
        Assert.Contains("\"unitPrice\":50", lineBody, StringComparison.Ordinal);
        Assert.Contains("\"voucherKind\":\"SinglePurpose\"", treatmentBody, StringComparison.Ordinal);
        Assert.Contains($"\"voucherTreatmentId\":\"{SpaSample.Treatments[2].Id}\"", treatmentBody, StringComparison.Ordinal);
        Assert.Contains("\"unitPrice\":null", treatmentBody, StringComparison.Ordinal);
        Assert.Equal("", pos.VoucherValue);
        Assert.Null(pos.VoucherTreatment);
        Assert.Equal("Wertgutschein", voucherLine.Description);
        Assert.False(voucherLine.CanDiscount);
        Assert.True(pos.Receipt!.HasVouchers);
        Assert.Equal(new ReceiptRow("Gutschein GS-9Q2A-X1", "50,00 €"), Assert.Single(pos.Receipt.Vouchers));
    }

    [Fact]
    public async Task Schweizer_Kasse_zeigt_Franken()
    {
        var register = SpaSample.Register with { Id = Guid.NewGuid(), Country = FiscalCountry.CH, Code = "ZH-1" };
        var stub = Api()
            .On("GET /api/pos/registers", new List<CashRegisterDto> { register })
            .On($"GET /api/pos/registers/{register.Id}/bills", new List<BillDto>())
            .On("POST /api/pos/bills", SpaSample.Bill() with { RegisterId = register.Id, Total = 1288.80m, Open = 1288.80m });
        var pos = new PosViewModel(stub.Client(), new SampleTime());
        await pos.InitializeAsync(SpaSample.LocationId);
        await pos.AddAppointmentAsync(SpaSample.Paying);
        var cash = pos.Payments.First(p => p.Method == PaymentMethod.Cash);
        pos.FillRestCommand.Execute(cash);

        Assert.Equal("CHF", pos.Currency);
        Assert.Equal("CHF 1’288.80", pos.TotalText);
        Assert.Equal("1288.80", cash.Amount);
        Assert.Equal("Alles gedeckt", pos.CoverText);
        Assert.Equal("CHF 1’234.50", Money.Format(1234.5m, "CHF"));
        Assert.Equal("CHF 1’235", Money.Whole(1234.5m, "CHF"));
        Assert.Equal(1234.5m, Money.Parse("CHF 1’234.50"));
        Assert.Equal("1.234,50 €", Money.Format(1234.5m));
    }

    [Fact]
    public async Task Rechnung_mit_Empfaenger_prueft_die_UID_und_haengt_den_Nachweis_an()
    {
        var receipt = SpaSample.LargeReceipt();
        var checkId = Guid.NewGuid();
        VatIdCheckDto Check(VatIdCheckStatus status) => new(checkId, "ATU87654321", "ATU12345678", status,
            status == VatIdCheckStatus.Valid ? "Beispiel Consulting GmbH" : null, status == VatIdCheckStatus.Valid ? "Ringstraße 1\n1010 Wien" : null,
            status == VatIdCheckStatus.Valid ? "WAPIAAAAX3RKnNxd" : null, null, SpaSample.At(11, 41), null);
        var stub = Api()
            .On($"POST /api/pos/bills/{SpaSample.BillId}/checkout", receipt)
            .On("POST /api/pos/vat-checks", Check(VatIdCheckStatus.Pending))
            .On($"GET /api/pos/vat-checks/{checkId}", Check(VatIdCheckStatus.Valid))
            .On($"POST /api/pos/receipts/{receipt.Id}/recipient", receipt with { Recipient = SpaSample.Recipient with { VatIdCheckId = checkId } });
        var pos = new PosViewModel(stub.Client(), new SampleTime(), cardPoll: TimeSpan.Zero);
        await pos.InitializeAsync(SpaSample.LocationId);
        await pos.AddAppointmentAsync(SpaSample.Paying);
        pos.Payments.First(p => p.Method == PaymentMethod.Card).Amount = "480";
        await pos.PayCommand.ExecuteAsync(null);

        var before = pos.Receipt!;
        var hint = before.InvoiceHint;
        await pos.StartInvoiceCommand.ExecuteAsync(before);
        var canAddEmpty = pos.AddRecipientCommand.CanExecute(null);
        pos.RecipientVatId = "atu 87654321";
        await pos.CheckVatIdCommand.ExecuteAsync(null);
        var vatText = pos.VatCheckText;
        await pos.AddRecipientCommand.ExecuteAsync(null);
        var body = stub.Calls.Single(c => c.Route.EndsWith("/recipient", StringComparison.Ordinal)).Body!;

        Assert.True(before.CanAddRecipient);
        Assert.Equal("Über 400,00 €: Für ein Unternehmen braucht die Rechnung Name und Anschrift des Empfängers.", hint);
        Assert.False(canAddEmpty);
        Assert.Equal("ATU87654321", pos.RecipientVatId);
        Assert.Equal("Beispiel Consulting GmbH", pos.RecipientName);
        Assert.StartsWith("UID gültig: Beispiel Consulting GmbH · Abfrage WAPIAAAAX3RKnNxd", vatText, StringComparison.Ordinal);
        Assert.Contains($"\"vatIdCheckId\":\"{checkId}\"", body, StringComparison.Ordinal);
        Assert.Contains("\"vatId\":\"ATU87654321\"", body, StringComparison.Ordinal);
        Assert.False(pos.IsInvoicing);
        Assert.Equal("Rechnung Nr. WIEN-1-1842", pos.Receipt!.Title);
        Assert.False(pos.Receipt.CanAddRecipient);
        Assert.Equal(["Beispiel Consulting GmbH", "Ringstraße 1", "1010 Wien", "UID ATU87654321"], pos.Receipt.RecipientLines);
        Assert.Equal(["FN 123456a, Handelsgericht Wien"], pos.Receipt.CompanyLines);
    }

    [Fact]
    public async Task Rechnung_belegt_den_gespeicherten_Empfaenger_der_Kundin_vor_und_merkt_eine_Aenderung()
    {
        var receipt = SpaSample.LargeReceipt();
        var customer = new CustomerDto(SpaSample.Paying.CustomerId, "Markus", "Steiner", null, null, null, null,
            new CustomerBilling("Steiner Bau GmbH", "Hauptplatz 3\n8010 Graz", "ATU11112222"));
        var stub = Api()
            .On($"POST /api/pos/bills/{SpaSample.BillId}/checkout", receipt)
            .On($"GET /api/customers/{customer.Id}", customer)
            .On($"PUT /api/customers/{customer.Id}/billing", customer)
            .On($"POST /api/pos/receipts/{receipt.Id}/recipient",
                receipt with { Recipient = new InvoiceRecipient("Steiner Bau GmbH", "Annenstraße 7\n8020 Graz", "ATU11112222") });
        var pos = new PosViewModel(stub.Client(), new SampleTime());
        await pos.InitializeAsync(SpaSample.LocationId);
        await pos.AddAppointmentAsync(SpaSample.Paying);
        pos.Payments.First(p => p.Method == PaymentMethod.Card).Amount = "480";
        await pos.PayCommand.ExecuteAsync(null);

        await pos.StartInvoiceCommand.ExecuteAsync(pos.Receipt);
        var prefilled = (pos.RecipientName, pos.RecipientAddress, pos.RecipientVatId);
        var remember = (pos.CanRememberRecipient, pos.RememberRecipient, pos.RememberRecipientText);
        pos.RecipientAddress = "Annenstraße 7\n8020 Graz";
        await pos.AddRecipientCommand.ExecuteAsync(null);
        var saved = stub.Calls.Single(c => c.Route == $"PUT /api/customers/{customer.Id}/billing").Body!;

        Assert.Equal(("Steiner Bau GmbH", "Hauptplatz 3\n8010 Graz", "ATU11112222"), prefilled);
        Assert.Equal((true, true, "Bei Markus Steiner als Rechnungsempfänger merken"), remember);
        Assert.Equal(new CustomerBilling("Steiner Bau GmbH", "Annenstraße 7\n8020 Graz", "ATU11112222"),
            System.Text.Json.JsonSerializer.Deserialize<CustomerBilling>(saved, ApiClient.Json));
        Assert.Null(pos.Error);
        Assert.False(pos.CanRememberRecipient);
    }

    [Fact]
    public async Task Rechnung_ohne_Merken_laesst_die_Kundenkarte_unveraendert()
    {
        var receipt = SpaSample.LargeReceipt();
        var customer = new CustomerDto(SpaSample.Paying.CustomerId, "Markus", "Steiner", null, null, null, null);
        var stub = Api()
            .On($"POST /api/pos/bills/{SpaSample.BillId}/checkout", receipt)
            .On($"GET /api/customers/{customer.Id}", customer)
            .On($"POST /api/pos/receipts/{receipt.Id}/recipient", receipt with { Recipient = SpaSample.Recipient });
        var pos = new PosViewModel(stub.Client(), new SampleTime());
        await pos.InitializeAsync(SpaSample.LocationId);
        await pos.AddAppointmentAsync(SpaSample.Paying);
        pos.Payments.First(p => p.Method == PaymentMethod.Card).Amount = "480";
        await pos.PayCommand.ExecuteAsync(null);

        await pos.StartInvoiceCommand.ExecuteAsync(pos.Receipt);
        var empty = pos.RecipientName;
        pos.RecipientName = SpaSample.Recipient.Name;
        pos.RecipientAddress = SpaSample.Recipient.Address;
        pos.RememberRecipient = false;
        await pos.AddRecipientCommand.ExecuteAsync(null);

        Assert.Equal("", empty);
        Assert.DoesNotContain(stub.Calls, c => c.Route.StartsWith("PUT ", StringComparison.Ordinal));
        Assert.Equal("Rechnung Nr. WIEN-1-1842", pos.Receipt!.Title);
    }

    [Fact]
    public async Task Karte_ueber_dem_offenen_Betrag_geht_nicht()
    {
        var pos = new PosViewModel(Api().Client(), new SampleTime());
        await pos.InitializeAsync(SpaSample.LocationId);
        await pos.AddAppointmentAsync(SpaSample.Paying);
        pos.Payments.First(p => p.Method == PaymentMethod.Card).Amount = "200";
        Assert.False(pos.CanPay);
    }

    [Fact]
    public async Task Ohne_Kasse_eine_verstaendliche_Meldung()
    {
        var pos = new PosViewModel(new StubApi()
            .On("GET /api/locations", new List<LocationDto>())
            .On("GET /api/pos/articles", new List<ArticleDto>())
            .On("GET /api/pos/registers", new List<CashRegisterDto>()).Client(), new SampleTime());
        await pos.InitializeAsync();
        Assert.Contains("keine Kasse", pos.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Kassen_einer_anderen_Stelle_stehen_nicht_zur_Wahl()
    {
        var elsewhere = SpaSample.Register with { Id = Guid.NewGuid(), Code = "SERVER-1", OtherSite = true };
        var pos = new PosViewModel(new StubApi()
            .On("GET /api/locations", new List<LocationDto>())
            .On("GET /api/pos/articles", new List<ArticleDto>())
            .On("GET /api/pos/registers", new List<CashRegisterDto> { elsewhere }).Client(), new SampleTime());
        await pos.InitializeAsync();
        Assert.Empty(pos.Registers);
        Assert.Contains("an einer anderen Stelle", pos.Error, StringComparison.Ordinal);
    }

    private static readonly PaymentTerminalDto Terminal = new(Guid.NewGuid(), SpaSample.LocationId, "Empfang", TerminalProvider.Local, "192.168.1.50", true);

    private static CardPaymentDto Card(CardPaymentStatus status) => new(CardId, SpaSample.BillId, Terminal.Id, 138.80m, "EUR", status, null,
        status == CardPaymentStatus.Approved ? "Mastercard" : null, status == CardPaymentStatus.Approved ? "•••• 4711" : null, null, "42", null,
        SpaSample.At(11, 40), null);

    private static readonly Guid CardId = Guid.NewGuid();

    private sealed class FakePrinter : IReceiptPrinter
    {
        public List<(string Address, byte[] Data)> Printed { get; } = [];

        public string? Address { get; set; }

        public Task<string?> PrintAsync(byte[] escPos, CancellationToken ct)
        {
            Printed.Add((Address!, escPos));
            return Task.FromResult<string?>(null);
        }
    }

    [Fact]
    public async Task Karte_am_Terminal_genehmigt_dann_kassiert_und_gedruckt()
    {
        var receipt = SpaSample.Receipt();
        var stub = Api()
            .On($"GET /api/pos/terminals?locationId={SpaSample.LocationId}", new List<PaymentTerminalDto> { Terminal })
            .On($"POST /api/pos/bills/{SpaSample.BillId}/card-payments", Card(CardPaymentStatus.Pending))
            .On($"GET /api/pos/card-payments/{CardId}", Card(CardPaymentStatus.Approved))
            .On($"POST /api/pos/bills/{SpaSample.BillId}/checkout", receipt)
            .OnBytes($"GET /api/pos/receipts/{receipt.Id}/print?format=EscPos&width=48", [0x1B, 0x40]);
        var settings = new MemorySettingsStore();
        settings.Save(new DesktopSettings(PrinterAddress: "192.168.1.60", PrinterWidth: 48));
        var printer = new FakePrinter();
        var pos = new PosViewModel(stub.Client(), new SampleTime(), settings, a => { printer.Address = a; return printer; }, TimeSpan.FromMilliseconds(1));
        await pos.InitializeAsync(SpaSample.LocationId);
        await pos.AddAppointmentAsync(SpaSample.Paying);
        await Task.Delay(50, TestContext.Current.CancellationToken);

        var card = pos.Payments.First(p => p.IsCard);
        Assert.True(card.ShowTerminalButton);
        await pos.StartCardPaymentCommand.ExecuteAsync(card);

        Assert.Equal("138,80", card.Amount);
        Assert.True(card.IsApproved);
        Assert.True(card.IsLocked);
        Assert.Equal("Genehmigt · Mastercard •••• 4711", card.TerminalText);
        Assert.True(pos.CanPay);

        // Genehmigtes Geld am Terminal: Rechnung lässt sich nicht verwerfen.
        await pos.DiscardBillCommand.ExecuteAsync(null);
        Assert.True(pos.HasBill);
        Assert.Contains("am Terminal stornieren", pos.Error, StringComparison.Ordinal);

        await pos.PayCommand.ExecuteAsync(null);
        var body = stub.Calls.Single(c => c.Route.EndsWith("/checkout", StringComparison.Ordinal)).Body!;
        Assert.Contains(CardId.ToString(), body, StringComparison.Ordinal);
        Assert.Equal("192.168.1.60", printer.Printed.Single().Address);
        Assert.Equal("Beleg gedruckt.", pos.PrintText);
    }

    private static async Task<(PosViewModel Pos, StubApi Stub, FakePrinter Printer, ReceiptDto Receipt)> PaidReceipt(bool withPrinter, ReceiptDto receipt)
    {
        var stub = Api()
            .On($"POST /api/pos/bills/{SpaSample.BillId}/checkout", receipt)
            .On($"POST /api/pos/receipts/{receipt.Id}/recipient", receipt with { Recipient = SpaSample.Recipient })
            .OnBytes($"GET /api/pos/receipts/{receipt.Id}/print?format=EscPos&width=48", [0x1B, 0x40]);
        var settings = new MemorySettingsStore();
        settings.Save(new DesktopSettings(PrinterAddress: withPrinter ? "192.168.1.60" : null, PrinterWidth: 48, PrintAutomatically: false));
        var printer = new FakePrinter();
        var pos = new PosViewModel(stub.Client(), new SampleTime(), settings, a => { printer.Address = a; return printer; });
        await pos.InitializeAsync(SpaSample.LocationId);
        await pos.AddAppointmentAsync(SpaSample.Paying);
        pos.Payments.First(p => p.Method == PaymentMethod.Card).Amount = receipt.Total.ToString("0.00", System.Globalization.CultureInfo.GetCultureInfo("de-AT"));
        await pos.PayCommand.ExecuteAsync(null);
        return (pos, stub, printer, receipt);
    }

    [Fact]
    public async Task Drucken_Given_bezahlter_Beleg_und_Drucker_When_Drucken_gewaehlt_Then_geht_der_Beleg_an_192_168_1_60()
    {
        var (pos, _, printer, _) = await PaidReceipt(true, SpaSample.Receipt());

        await pos.PrintReceiptCommand.ExecuteAsync(pos.Receipt);

        Assert.Equal("192.168.1.60", printer.Printed.Single().Address);
        Assert.Equal("Beleg gedruckt.", pos.PrintText);
    }

    [Fact]
    public async Task Drucken_Given_Empfaenger_erfasst_When_Drucken_gewaehlt_Then_wird_nach_dem_Empfaenger_derselbe_Beleg_gedruckt()
    {
        var (pos, stub, printer, receipt) = await PaidReceipt(true, SpaSample.LargeReceipt());
        await pos.StartInvoiceCommand.ExecuteAsync(pos.Receipt);
        pos.RecipientName = SpaSample.Recipient.Name;
        pos.RecipientAddress = SpaSample.Recipient.Address;
        pos.RememberRecipient = false;
        await pos.AddRecipientCommand.ExecuteAsync(null);

        await pos.PrintReceiptCommand.ExecuteAsync(pos.Receipt);

        var routes = stub.Calls.Select(c => c.Route).ToList();
        var recipient = routes.IndexOf($"POST /api/pos/receipts/{receipt.Id}/recipient");
        var print = routes.IndexOf($"GET /api/pos/receipts/{receipt.Id}/print?format=EscPos&width=48");
        Assert.True(recipient >= 0 && print > recipient);
        Assert.Single(printer.Printed);
    }

    [Fact]
    public async Task Drucken_Given_kein_Drucker_When_Drucken_gewaehlt_Then_wird_nichts_abgerufen_und_PrintText_bleibt_leer()
    {
        var (pos, stub, printer, _) = await PaidReceipt(false, SpaSample.Receipt());

        await pos.PrintReceiptCommand.ExecuteAsync(pos.Receipt);

        Assert.Empty(printer.Printed);
        Assert.DoesNotContain(stub.Calls, c => c.Route.Contains("/print", StringComparison.Ordinal));
        Assert.Null(pos.PrintText);
    }

    [Fact]
    public async Task Abgelehnte_Karte_gibt_den_Betrag_frei()
    {
        var stub = Api()
            .On($"GET /api/pos/terminals?locationId={SpaSample.LocationId}", new List<PaymentTerminalDto> { Terminal })
            .On($"POST /api/pos/bills/{SpaSample.BillId}/card-payments", Card(CardPaymentStatus.Pending))
            .On($"GET /api/pos/card-payments/{CardId}", Card(CardPaymentStatus.Declined) with { Message = "Falsche PIN" });
        var pos = new PosViewModel(stub.Client(), new SampleTime(), cardPoll: TimeSpan.FromMilliseconds(1));
        await pos.InitializeAsync(SpaSample.LocationId);
        await pos.AddAppointmentAsync(SpaSample.Paying);
        await Task.Delay(50, TestContext.Current.CancellationToken);

        var card = pos.Payments.First(p => p.IsCard);
        await pos.StartCardPaymentCommand.ExecuteAsync(card);
        Assert.Equal("", card.Amount);
        Assert.False(card.IsLocked);
        Assert.Equal("Nicht genehmigt: Falsche PIN", card.TerminalText);
        Assert.False(pos.CanPay);
    }

    private static ReceiptDto VoidReceipt(ReceiptDto original) => original with
    {
        Id = Guid.NewGuid(),
        Number = original.Number + 1,
        Kind = ReceiptKind.Void,
        Total = -original.Total,
        Change = 0,
        VoidsReceiptId = original.Id,
        VoidReason = "Falsche Behandlung",
    };

    [Fact]
    public async Task Beleg_stornieren_mit_Grund()
    {
        var original = SpaSample.Receipt();
        var storno = VoidReceipt(original);
        var stub = Api()
            .On($"GET /api/pos/registers/{SpaSample.Register.Id}/receipts?date=2026-10-19", new List<ReceiptDto> { original })
            .On($"POST /api/pos/receipts/{original.Id}/void", storno);
        var pos = new PosViewModel(stub.Client(), new SampleTime());
        await pos.InitializeAsync(SpaSample.LocationId);
        await pos.LoadReceiptsAsync();

        Assert.Equal("Nr. 1842", pos.Receipts.Single().Number);
        Assert.True(pos.SelectedReceipt!.CanVoid);
        Assert.False(pos.VoidReceiptCommand.CanExecute(null));
        pos.VoidReasonInput = "Falsche Behandlung";
        Assert.True(pos.VoidReceiptCommand.CanExecute(null));

        stub.On($"GET /api/pos/registers/{SpaSample.Register.Id}/receipts?date=2026-10-19",
            new List<ReceiptDto> { storno, original with { VoidedByReceiptId = storno.Id } });
        // Stornieren fragt zuerst nach (Rückfragedialog): „Abbrechen“ storniert nichts, „Stornieren“ legt den Stornobeleg an.
        var dialogs = new FakeDialogs().Answer(ConfirmChoice.Cancelled).Answer(ConfirmChoice.Confirmed);
        pos.Dialogs = dialogs;
        await pos.AskVoidReceiptCommand.ExecuteAsync(null);
        Assert.DoesNotContain(stub.Calls, c => c.Route.EndsWith("/void", StringComparison.Ordinal));
        Assert.Equal("Beleg stornieren?", dialogs.Asked[0].Title);
        Assert.Contains("Nr. 1842", dialogs.Asked[0].Message, StringComparison.Ordinal);
        dialogs.Asked.ForEach(FakeDialogs.AssertRules);
        await pos.AskVoidReceiptCommand.ExecuteAsync(null);

        Assert.Contains("Falsche Behandlung", stub.Calls.Single(c => c.Route.EndsWith("/void", StringComparison.Ordinal)).Body, StringComparison.Ordinal);
        Assert.Equal("Stornobeleg Nr. 1843", pos.SelectedReceipt!.Title);
        Assert.False(pos.SelectedReceipt.CanVoid);
        Assert.True(pos.Receipts.Last().IsVoided);
    }

    [Fact]
    public async Task Storno_ohne_Kassenaufsicht_zeigt_den_Grund()
    {
        var original = SpaSample.Receipt();
        var stub = Api()
            .On($"GET /api/pos/registers/{SpaSample.Register.Id}/receipts?date=2026-10-19", new List<ReceiptDto> { original })
            .On($"POST /api/pos/receipts/{original.Id}/void", new { message = "Ältere Belege storniert nur die Kassenaufsicht." }, System.Net.HttpStatusCode.Forbidden);
        var pos = new PosViewModel(stub.Client(), new SampleTime());
        await pos.InitializeAsync(SpaSample.LocationId);
        await pos.LoadReceiptsAsync();
        pos.VoidReasonInput = "Doppelt kassiert";
        await pos.VoidReceiptCommand.ExecuteAsync(null);
        Assert.Equal("Ältere Belege storniert nur die Kassenaufsicht.", pos.Error);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public async Task Einen_Anzahlungsbeleg_storniert_nur_die_Leitung_und_der_gesperrte_Knopf_nennt_den_Grund(bool ownerLevel, bool canVoid)
    {
        var deposit = SpaSample.Receipt() with { IsDepositReceipt = true };
        var stub = Api().On($"GET /api/pos/registers/{SpaSample.Register.Id}/receipts?date=2026-10-19", new List<ReceiptDto> { deposit });
        var pos = new PosViewModel(stub.Client(), new SampleTime()) { OwnerLevel = ownerLevel };
        await pos.InitializeAsync(SpaSample.LocationId);
        await pos.LoadReceiptsAsync();
        pos.VoidReasonInput = "Falsch kassiert";

        Assert.Equal(canVoid, pos.VoidReceiptCommand.CanExecute(null));
        Assert.Equal(canVoid, pos.AskVoidReceiptCommand.CanExecute(null));
        Assert.Equal(!canVoid, pos.SelectedReceipt!.VoidBlockedByRole);
        Assert.Equal(canVoid ? "Verfügbar, sobald ein Grund mit mindestens 3 Zeichen eingetragen ist." : "Anzahlungsbelege storniert nur die Leitung.", pos.VoidDisabledHint);
    }

    [Fact]
    public async Task Ein_gewoehnlicher_Beleg_bleibt_ohne_Leitungsrolle_stornierbar()
    {
        var stub = Api().On($"GET /api/pos/registers/{SpaSample.Register.Id}/receipts?date=2026-10-19", new List<ReceiptDto> { SpaSample.Receipt() });
        var pos = new PosViewModel(stub.Client(), new SampleTime());
        await pos.InitializeAsync(SpaSample.LocationId);
        await pos.LoadReceiptsAsync();
        pos.VoidReasonInput = "Falsch kassiert";

        Assert.True(pos.VoidReceiptCommand.CanExecute(null));
        Assert.False(pos.SelectedReceipt!.VoidBlockedByRole);
    }

    private static DayClosingDto Report(decimal counted = 0, decimal difference = 0) => new(Guid.Empty, SpaSample.Register.Id, SpaSample.At(8), SpaSample.At(11, 40),
        12, 1, 1384.60m,
        [new(PaymentMethod.Cash, 412.30m), new(PaymentMethod.Card, 922.30m), new(PaymentMethod.Voucher, 50m)],
        [new(TaxCategory.Normal, 20, 1384.60m, 1153.83m, 230.77m)],
        [new(SpaSample.Revenue, "4000", "Behandlungen", null, 1210m, 1008.33m)],
        150m, 0, 100m, 462.30m, counted, difference, null);

    [Fact]
    public async Task Einlage_und_Tagesabschluss()
    {
        var stub = Api()
            .On($"GET /api/pos/registers/{SpaSample.Register.Id}/day-report", Report())
            .On($"POST /api/pos/registers/{SpaSample.Register.Id}/cash-movements",
                new CashMovementDto(Guid.NewGuid(), CashMovementKind.Withdrawal, 100m, "Bank", SpaSample.At(11)))
            .On($"POST /api/pos/registers/{SpaSample.Register.Id}/day-closings", Report(460.30m, -2m) with { Id = Guid.NewGuid() });
        var pos = new PosViewModel(stub.Client(), new SampleTime());
        await pos.InitializeAsync(SpaSample.LocationId);
        await pos.LoadReportAsync();
        Assert.Equal("462,30 €", pos.ExpectedCashText);

        pos.MovementIsWithdrawal = true;
        pos.MovementAmount = "100";
        pos.MovementReason = "Bank";
        await pos.AddMovementCommand.ExecuteAsync(null);
        Assert.Contains("\"kind\":\"Withdrawal\"", stub.Calls.Single(c => c.Route.EndsWith("/cash-movements", StringComparison.Ordinal)).Body,
            StringComparison.Ordinal);
        Assert.Equal("", pos.MovementAmount);

        Assert.False(pos.CloseDayCommand.CanExecute(null));
        pos.CountedCash = "460,30";
        Assert.Equal("Differenz −2,00 €", pos.DifferenceText);
        pos.Dialogs = new FakeDialogs().Answer(ConfirmChoice.Confirmed);
        await pos.CloseDayCommand.ExecuteAsync(null);
        Assert.True(pos.IsClosed);
        Assert.Equal("Tagesabschluss gespeichert", pos.ReportTitle);
        Assert.Equal("Differenz −2,00 €", pos.DifferenceText);
        Assert.False(pos.CloseDayCommand.CanExecute(null));
    }

    private static string DayClosingRoute => $"POST /api/pos/registers/{SpaSample.Register.Id}/day-closings";

    [Fact]
    public async Task Given_Zaehlbestand_When_Tagesabschluss_Then_fragt_vorher_und_Abbrechen_speichert_nichts()
    {
        var stub = Api()
            .On($"GET /api/pos/registers/{SpaSample.Register.Id}/day-report", Report())
            .On(DayClosingRoute, Report(460.30m, -2m) with { Id = Guid.NewGuid() });
        var pos = new PosViewModel(stub.Client(), new SampleTime());
        await pos.InitializeAsync(SpaSample.LocationId);
        await pos.LoadReportAsync();
        pos.CountedCash = "460,30";
        var dialogs = new FakeDialogs().Answer(ConfirmChoice.Cancelled).Answer(ConfirmChoice.Confirmed);
        pos.Dialogs = dialogs;

        await pos.CloseDayCommand.ExecuteAsync(null);
        Assert.DoesNotContain(stub.Calls, c => c.Route == DayClosingRoute);
        Assert.False(pos.IsClosed);
        Assert.Equal("460,30", pos.CountedCash);

        await pos.CloseDayCommand.ExecuteAsync(null);
        Assert.Single(stub.Calls, c => c.Route == DayClosingRoute);
        Assert.True(pos.IsClosed);

        Assert.Equal(2, dialogs.Asked.Count);
        Assert.Equal("Tagesabschluss durchführen?", dialogs.Asked[0].Title);
        Assert.Equal("Abschließen", dialogs.Asked[0].ConfirmText);
        Assert.True(dialogs.Asked[0].IsDestructive);
        Assert.StartsWith("Der Abschluss wird unveränderlich gespeichert.", dialogs.Asked[0].Message, StringComparison.Ordinal);
        dialogs.AssertRules();
    }

    [Fact]
    public async Task Given_neuer_Beleg_nach_dem_Oeffnen_When_Tagesabschluss_Then_nennt_die_Rueckfrage_die_frische_Differenz()
    {
        var stub = Api()
            .On($"GET /api/pos/registers/{SpaSample.Register.Id}/day-report", Report())
            .On(DayClosingRoute, Report(460.30m, -12m) with { Id = Guid.NewGuid() });
        var pos = new PosViewModel(stub.Client(), new SampleTime());
        await pos.InitializeAsync(SpaSample.LocationId);
        await pos.LoadReportAsync();
        pos.CountedCash = "460,30";
        Assert.Equal("Differenz −2,00 €", pos.DifferenceText);
        // Zwischen Öffnen des Reiters und Abschluss kommt eine Barzahlung dazu: Soll-Bestand 472,30 €.
        stub.On($"GET /api/pos/registers/{SpaSample.Register.Id}/day-report", Report() with { ExpectedCash = 472.30m });
        var dialogs = new FakeDialogs().Answer(ConfirmChoice.Cancelled);
        pos.Dialogs = dialogs;

        await pos.CloseDayCommand.ExecuteAsync(null);

        Assert.Contains("Differenz zum Soll: −12,00 €.", dialogs.Asked.Single().Message, StringComparison.Ordinal);
        Assert.Equal("Differenz −12,00 €", pos.DifferenceText);
    }

    [Fact]
    public async Task Given_Zaehlbestand_gleich_Soll_When_Tagesabschluss_Then_Rueckfrage_ohne_Differenz()
    {
        var stub = Api().On($"GET /api/pos/registers/{SpaSample.Register.Id}/day-report", Report());
        var pos = new PosViewModel(stub.Client(), new SampleTime());
        await pos.InitializeAsync(SpaSample.LocationId);
        await pos.LoadReportAsync();
        pos.CountedCash = "462,30";
        var dialogs = new FakeDialogs().Answer(ConfirmChoice.Cancelled);
        pos.Dialogs = dialogs;

        await pos.CloseDayCommand.ExecuteAsync(null);

        Assert.DoesNotContain("Differenz", dialogs.Asked.Single().Message, StringComparison.Ordinal);
        Assert.Contains("stimmt mit dem Soll überein", dialogs.Asked.Single().Message, StringComparison.Ordinal);
    }

    private static async Task<PosViewModel> PayingBillAsync(StubApi? stub = null)
    {
        var pos = new PosViewModel((stub ?? Api()).Client(), new SampleTime());
        await pos.InitializeAsync(SpaSample.LocationId);
        await pos.AddAppointmentAsync(SpaSample.Paying);
        return pos;
    }

    private static PaymentEntry Entry(PosViewModel pos, PaymentMethod method) => pos.Payments.First(p => p.Method == method);

    [Fact]
    public async Task Betrag_Given_Rechnung_138_80_ohne_Zahlung_When_Rest_vorbelegt_Then_steht_in_Bar_138_80()
    {
        var pos = await PayingBillAsync();

        var filled = pos.PrefillRest(Entry(pos, PaymentMethod.Cash));

        Assert.True(filled);
        Assert.Equal("138,80", Entry(pos, PaymentMethod.Cash).Amount);
    }

    [Fact]
    public async Task Betrag_Given_50_00_mit_Karte_eingetragen_When_Bar_vorbelegt_Then_steht_in_Bar_88_80()
    {
        var pos = await PayingBillAsync();
        Entry(pos, PaymentMethod.Card).Amount = "50";

        pos.PrefillRest(Entry(pos, PaymentMethod.Cash));

        Assert.Equal("88,80", Entry(pos, PaymentMethod.Cash).Amount);
    }

    [Fact]
    public async Task Betrag_Given_Karte_20_00_When_Karte_erneut_vorbelegt_Then_bleibt_20_00()
    {
        var pos = await PayingBillAsync();
        Entry(pos, PaymentMethod.Card).Amount = "20,00";

        var filled = pos.PrefillRest(Entry(pos, PaymentMethod.Card));

        Assert.False(filled);
        Assert.Equal("20,00", Entry(pos, PaymentMethod.Card).Amount);
    }

    [Fact]
    public async Task Betrag_Given_Rechnung_komplett_gedeckt_When_weitere_Zahlart_vorbelegt_Then_bleibt_sie_leer()
    {
        var pos = await PayingBillAsync();
        Entry(pos, PaymentMethod.Card).Amount = "138,80";

        var filled = pos.PrefillRest(Entry(pos, PaymentMethod.Cash));

        Assert.False(filled);
        Assert.Equal("", Entry(pos, PaymentMethod.Cash).Amount);
    }

    [Fact]
    public async Task Betrag_Given_Barzahlungsrabatt_und_nur_bar_When_Bar_vorbelegt_Then_steht_der_Betrag_abzueglich_Rabatt()
    {
        var pos = await PayingBillAsync(Api().On("POST /api/pos/bills", SpaSample.Bill() with { CashDiscountPercent = 3m, CashDiscount = 4.16m }));

        pos.PrefillRest(Entry(pos, PaymentMethod.Cash));

        Assert.Equal("134,64", Entry(pos, PaymentMethod.Cash).Amount);
    }
}
