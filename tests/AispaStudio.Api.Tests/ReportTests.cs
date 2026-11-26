using System.Net;
using System.Net.Http.Json;
using AispaStudio.Contracts;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace AispaStudio.Api.Tests;

public sealed class ReportTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static DateOnly ViennaToday =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna")).DateTime);

    [Fact]
    public async Task Behandlungskarte_mit_Rezept_Vorlieben_Verlauf_und_abhakbarer_Vorbereitung()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var steps = await spa.PutAsync<List<PreparationStepDto>>($"/api/reports/treatments/{spa.MassageTreatment.Id}/preparation",
            new[] { new PreparationStepInput("Öl auf 38 °C anwärmen", 30), new PreparationStepInput("Liege mit frischem Laken", null) }, Ct);
        await spa.PutAsync<CustomerPreferences>($"/api/reports/customers/{spa.Customer.Id}/preferences",
            new CustomerPreferences("fest", "Lavendel", "24 °C", null, true, "Nacken", "Füße", null), Ct);
        var first = await spa.BookOkAsync(TestSpa.At(10), Ct, spa.Anna.Id);
        await spa.SetStatusAsync(first.Id, AppointmentStatus.Completed, Ct);
        var note = await spa.Client.PutAsJsonAsync($"/api/reports/appointments/{first.Id}/follow-up", new FollowUpNote("Mag festeren Druck im Nacken."), TestSpa.Json, Ct);
        var second = await spa.BookOkAsync(TestSpa.At(13), Ct, spa.Ben.Id);

        var firstCard = await spa.GetAsync<TreatmentCard>($"/api/reports/appointments/{first.Id}/card", Ct);
        var check = await spa.Client.PutAsJsonAsync($"/api/reports/appointments/{second.Id}/preparation/{steps[0].Id}", new PreparationCheck(true), TestSpa.Json, Ct);
        await spa.PutAsync<List<PreparationStepDto>>($"/api/reports/treatments/{spa.MassageTreatment.Id}/preparation",
            new[] { new PreparationStepInput("Öl auf 38 °C anwärmen", 30), new PreparationStepInput("Handtuch vorwärmen", 10) }, Ct);
        var card = await spa.GetAsync<TreatmentCard>($"/api/reports/appointments/{second.Id}/card", Ct);

        Assert.Equal(HttpStatusCode.NoContent, note.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, check.StatusCode);
        Assert.Equal((1, "Fragebogen fehlt (Erstbesuch)"), (firstCard.VisitNumber, firstCard.AnamnesisStatus));
        Assert.Equal((2, "Kein Fragebogen hinterlegt", "Raum A"), (card.VisitNumber, card.AnamnesisStatus, card.RoomName));
        var past = Assert.Single(card.History);
        Assert.Equal(("Anna", "Mag festeren Druck im Nacken."), (past.EmployeeName, past.Note));
        var recipe = card.Todo.Where(t => t.Source == "Rezept").ToList();
        Assert.Equal(["Öl auf 38 °C anwärmen", "Handtuch vorwärmen"], recipe.Select(t => t.Text));
        Assert.True(recipe[0].Done);
        Assert.False(recipe[1].Done);
        Assert.Equal(second.Start.AddMinutes(-30), recipe[0].DueAt);
        Assert.Contains(card.Todo, t => t.Text == "Duft: Lavendel");
        Assert.Contains(card.Todo, t => t.Text == "Nicht behandeln: Füße");
        Assert.Contains(card.Todo, t => t.Text == "Wenig Gespräch gewünscht");
        Assert.Contains(card.Todo, t => t.Source == "Notiz" && t.Text.Contains("festeren Druck", StringComparison.Ordinal));
        Assert.Contains(firstCard.Todo, t => t.Text.StartsWith("Erstbesuch", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Therapeutin_sieht_nur_den_eigenen_Tagesplan_und_eigene_Karten()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var own = await spa.BookOkAsync(TestSpa.At(10), Ct, spa.Anna.Id);
        var other = await spa.BookOkAsync(TestSpa.At(10), Ct, spa.Ben.Id);
        using var anna = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Therapist, [spa.Location.Id], spa.Anna.Id);
        var path = $"/api/reports/locations/{spa.Location.Id}/day-plan?date={TestSpa.Monday:yyyy-MM-dd}";

        var plan = await anna.GetFromJsonAsync<DayPlanDto>(path, TestSpa.Json, Ct);
        var bens = await anna.GetAsync($"{path}&employeeId={spa.Ben.Id}", Ct);
        var ownCard = await anna.GetAsync($"/api/reports/appointments/{own.Id}/card", Ct);
        var otherCard = await anna.GetAsync($"/api/reports/appointments/{other.Id}/card", Ct);

        Assert.Equal("Anna", plan!.EmployeeName);
        Assert.Equal(own.Id, Assert.Single(plan.Cards).AppointmentId);
        Assert.Equal(TestSpa.At(9), Assert.Single(plan.Shifts).Start);
        Assert.Equal(HttpStatusCode.Forbidden, bens.StatusCode);
        Assert.Equal(HttpStatusCode.OK, ownCard.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, otherCard.StatusCode);
    }

    [Fact]
    public async Task Tagesstart_zeigt_Abwesenheit_Termine_ohne_Dienst_und_Erstbesuche()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await spa.BookOkAsync(TestSpa.At(10), Ct, spa.Anna.Id);
        await spa.BookOkAsync(TestSpa.At(12), Ct, spa.Ben.Id);
        await spa.PutAsync<RosterDayResult>($"/api/locations/{spa.Location.Id}/roster/{spa.Ben.Id}/{TestSpa.Monday:yyyy-MM-dd}",
            new RosterDayInput([new RosterEntry(RosterKind.Sick, null, null, null)], null), Ct);
        using var reception = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Reception, [spa.Location.Id]);
        using var therapist = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Therapist, [spa.Location.Id], spa.Anna.Id);
        var path = $"/api/reports/locations/{spa.Location.Id}/briefing?date={TestSpa.Monday:yyyy-MM-dd}";

        var briefing = await reception.GetFromJsonAsync<BriefingDto>(path, TestSpa.Json, Ct);
        var forbidden = await therapist.GetAsync(path, Ct);

        Assert.StartsWith("Heute 2 Behandlungen", briefing!.Summary, StringComparison.Ordinal);
        Assert.Contains(briefing.Staff, s => s is { Name: "Anna", State: "Dienst", Times: "09:00–17:00" });
        Assert.Contains(briefing.Staff, s => s is { Name: "Ben", State: "Krankenstand" });
        Assert.Contains(briefing.ActionNeeded, a => a.Text.Contains("Ben hat zu der Zeit keinen Dienst", StringComparison.Ordinal));
        Assert.Contains(briefing.Guests, g => g.Text.Contains("Erstbesuch", StringComparison.Ordinal));
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
    }

    [Fact]
    public async Task Der_Tagesstart_nennt_den_Anfangsbestand_je_Kassa_als_Zahl()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var austria = await spa.PostAsync<CashRegisterDto>("/api/pos/registers", new CashRegisterInput(spa.Location.Id, $"A-{Guid.NewGuid():N}"[..8],
            FiscalCountry.AT, "Test-Spa GmbH", "Test-Spa GmbH", "AT61 1904 3002 3457 3201", null), Ct);
        var swiss = await spa.PostAsync<CashRegisterDto>("/api/pos/registers", new CashRegisterInput(spa.Location.Id, $"C-{Guid.NewGuid():N}"[..8],
            FiscalCountry.CH, "Test-Spa AG", "Test-Spa AG", "CH93 0076 2011 6238 5295 7", null), Ct);
        var path = $"/api/reports/locations/{spa.Location.Id}/briefing?date={TestSpa.Monday:yyyy-MM-dd}";

        var before = await spa.GetAsync<BriefingDto>(path, Ct);
        await spa.PostAsync<DayClosingDto>($"/api/pos/registers/{austria.Id}/day-closings", new DayClosingInput(1250.50m, null), Ct);
        var after = await spa.GetAsync<BriefingDto>(path, Ct);

        // Given zwei aktive Kassen ohne Abschluss; Then hat jede einen Eintrag ohne Betrag und mit der Währung ihres Landes
        Assert.Equal(2, before.Registers!.Count);
        Assert.Equal(("EUR", (decimal?)null), (Assert.Single(before.Registers, r => r.RegisterId == austria.Id).Currency, Assert.Single(before.Registers, r => r.RegisterId == austria.Id).OpeningCash));
        Assert.Equal(("CHF", (decimal?)null), (Assert.Single(before.Registers, r => r.RegisterId == swiss.Id).Currency, Assert.Single(before.Registers, r => r.RegisterId == swiss.Id).OpeningCash));
        // When die österreichische Kassa mit 1.250,50 abgeschlossen wurde; Then ist das ihr Anfangsbestand als Zahl, die andere bleibt ohne
        var closed = Assert.Single(after.Registers!, r => r.RegisterId == austria.Id);
        Assert.Equal((austria.Code, 1250.50m), (closed.Code, closed.OpeningCash));
        Assert.Null(Assert.Single(after.Registers!, r => r.RegisterId == swiss.Id).OpeningCash);
        // Und der Satz für die KI bleibt wörtlich, weil sein Wortlaut in den Fingerabdruck der gespeicherten Zusammenfassungen eingeht
        Assert.Contains(after.Register, i => i.Text == $"Kassa {austria.Code}: Anfangsbestand laut letztem Abschluss 1250,50 €.");
        Assert.Contains(after.Register, i => i.Text == $"Kassa {swiss.Code}: noch kein Tagesabschluss.");
    }

    [Fact]
    public async Task Management_Kennzahlen_und_Pflichtberichte()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var register = await spa.PostAsync<CashRegisterDto>("/api/pos/registers", new CashRegisterInput(spa.Location.Id, $"R-{Guid.NewGuid():N}"[..14],
            FiscalCountry.AT, "Test-Spa GmbH", "Test-Spa GmbH", "AT61 1904 3002 3457 3201", null), Ct);
        var bill = await spa.PostAsync<BillDto>("/api/pos/bills", new BillInput(register.Id, null, []), Ct);
        await spa.PostAsync<BillDto>($"/api/pos/bills/{bill.Id}/lines",
            new BillLineInput(BillLineKind.Custom, null, null, 1, 12.50m, 0, "Tee", TaxCategory.Normal, null, null, null), Ct);
        await spa.PostAsync<ReceiptDto>($"/api/pos/bills/{bill.Id}/checkout", new CheckoutRequest(null, [new PaymentInput(PaymentMethod.Cash, 12.50m, null, null)]), Ct);
        var done = await spa.BookOkAsync(TestSpa.At(10), Ct, spa.Anna.Id);
        var missed = await spa.BookOkAsync(TestSpa.At(12), Ct, spa.Ben.Id);
        await spa.BookOkAsync(TestSpa.At(14), Ct, spa.Anna.Id);
        await spa.SetStatusAsync(done.Id, AppointmentStatus.Completed, Ct);
        await spa.SetStatusAsync(missed.Id, AppointmentStatus.NoShow, Ct);
        await spa.PutAsync<RosterDayResult>($"/api/locations/{spa.Location.Id}/roster/{spa.Ben.Id}/2027-03-08",
            new RosterDayInput([new RosterEntry(RosterKind.Sick, null, null, null)], null), Ct);
        using var reception = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Reception, [spa.Location.Id]);

        var report = await ManagementAtAsync(spa, AfterAppointments, ViennaToday, TestSpa.Monday);
        var compliance = await spa.GetAsync<ComplianceReportDto>($"/api/reports/locations/{spa.Location.Id}/compliance?date=2027-03-15", Ct);
        var forbidden = await reception.GetAsync($"/api/reports/locations/{spa.Location.Id}/compliance?date=2027-03-15", Ct);

        decimal Kpi(string key) => report.Kpis.Single(k => k.Key == key).Value;
        Assert.Equal(12.50m, Kpi("revenue"));
        Assert.Equal(10.42m, Kpi("revenueNet"));
        Assert.Equal(2, Kpi("appointments"));
        Assert.Equal(33.3m, Kpi("noShowRate"));
        Assert.Equal(1, Kpi("newCustomers"));
        Assert.Equal(0m, report.Kpis.Single(k => k.Key == "revenue").Previous);
        Assert.Equal(new RevenueLine("Bar", null, 12.50m, 12.50m), Assert.Single(report.RevenueByPayment));
        var massage = Assert.Single(report.Treatments);
        Assert.Equal((2, 0m), (massage.Appointments, massage.NetRevenue));
        Assert.Contains("2 Termine noch nicht kassiert.", report.Alerts);
        Assert.Contains(report.Alerts, a => a.StartsWith("No-Show-Quote", StringComparison.Ordinal));
        Assert.Contains(report.Employees, e => e.Name == "Anna" && e.TreatmentHours > 0);

        var kasse = Assert.Single(compliance.Registers);
        Assert.Equal($"/api/v1/pos/registers/{register.Id}/dep", kasse.ExportPath);
        Assert.Contains(kasse.Duties, d => d.Contains("Jahresbeleg", StringComparison.Ordinal));
        Assert.NotNull(kasse.StartedAt);
        var anna = compliance.WorkingTime.Single(w => w.Name == "Anna");
        var ben = compliance.WorkingTime.Single(w => w.Name == "Ben");
        Assert.Equal((40m, 5), (anna.PlannedHours, anna.WorkDays));
        Assert.Equal((32m, 4, 1), (ben.PlannedHours, ben.WorkDays, ben.SickDays));
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
    }

    [Fact]
    public async Task Verkaufter_Mehrzweckgutschein_ist_kein_Umsatz_der_Leistungen()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var register = await RegisterAsync(spa);
        var bill = await spa.PostAsync<BillDto>("/api/pos/bills", new BillInput(register.Id, null, []), Ct);
        bill = await spa.PostAsync<BillDto>($"/api/pos/bills/{bill.Id}/lines",
            new BillLineInput(BillLineKind.Custom, null, null, 1, 12.50m, 0, "Tee", TaxCategory.Normal, null, null, null), Ct);
        await CheckoutAsync(spa, bill);
        var voucher = await spa.PostAsync<BillDto>("/api/pos/bills", new BillInput(register.Id, null, []), Ct);
        voucher = await spa.PostAsync<BillDto>($"/api/pos/bills/{voucher.Id}/lines",
            new BillLineInput(BillLineKind.Voucher, null, null, 1, 50m, 0, null, null, null, VoucherKind.MultiPurpose, null), Ct);
        await CheckoutAsync(spa, voucher);

        var report = await ManagementAsync(spa, ViennaToday, ViennaToday);

        // Given ein Tee (12,50 € brutto, 10,42 € netto) und ein verkaufter Mehrzweckgutschein (50 €, eine Verbindlichkeit)
        // Then enthält „Umsatz brutto“ den Gutschein (bleibt so), der Umsatz und der Durchschnittsbon ohne Gutscheinverkauf aber nicht
        Assert.Equal(62.50m, Kpi(report, "revenue"));
        Assert.Equal(10.42m, Kpi(report, "serviceRevenueNet"));
        Assert.Equal(10.42m, Kpi(report, "serviceAverageTicket"));
    }

    [Fact]
    public async Task Folgetermin_zählt_auch_wenn_er_inzwischen_stattgefunden_hat()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var visit = await spa.BookOkAsync(TestSpa.At(10), Ct, spa.Anna.Id);
        var followUp = await spa.BookOkAsync(TestSpa.At(10, date: TestSpa.Monday.AddDays(7)), Ct, spa.Anna.Id);
        await spa.SetStatusAsync(visit.Id, AppointmentStatus.Completed, Ct);
        await spa.SetStatusAsync(followUp.Id, AppointmentStatus.Completed, Ct);

        var report = await ManagementAsync(spa, TestSpa.Monday, TestSpa.Monday);

        // Given der Gast war am Montag da und hatte eine Woche später schon den nächsten Termin, der inzwischen stattgefunden hat
        // Then hat er für den Montag einen Folgetermin gebucht
        Assert.Equal(100m, Kpi(report, "rebookingRate"));
    }

    [Fact]
    public async Task Umsatz_je_Behandlung_und_Person_kommt_aus_dem_Beleg_mit_Rabatt()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var register = await RegisterAsync(spa);
        var appointment = await spa.BookOkAsync(TestSpa.At(10), Ct, spa.Anna.Id);
        await spa.SetStatusAsync(appointment.Id, AppointmentStatus.CheckedIn, Ct);
        var bill = await spa.PostAsync<BillDto>("/api/pos/bills", new BillInput(register.Id, null, []), Ct);
        bill = await spa.PostAsync<BillDto>($"/api/pos/bills/{bill.Id}/lines",
            new BillLineInput(BillLineKind.Appointment, appointment.Id, null, 1, null, 10m, null, null, null, null, null), Ct);
        await CheckoutAsync(spa, bill);

        var report = await ManagementAsync(spa, ViennaToday, TestSpa.Monday);

        // Given eine Massage zum Terminpreis 79 €, kassiert mit 10 € Rabatt: 69 € brutto, 57,50 € netto
        // Then zählen Behandlung und Person 57,50 € (laut Terminpreis wären es 65,83 €); die Massage dauert 50 Minuten,
        // je Behandlungsstunde sind das 69 € (laut Terminpreis 79 €)
        Assert.Equal(69m, Kpi(report, "revenue"));
        Assert.Equal(57.50m, Assert.Single(report.Treatments).NetRevenue);
        Assert.Equal(69m, report.Employees.Single(e => e.Name == "Anna").RevenuePerHour);
    }

    [Fact]
    public async Task Mit_Einzweckgutschein_bezahlte_Behandlung_zählt_null_Euro_und_ist_kassiert()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var register = await RegisterAsync(spa);
        var sale = await spa.PostAsync<BillDto>("/api/pos/bills", new BillInput(register.Id, null, []), Ct);
        sale = await spa.PostAsync<BillDto>($"/api/pos/bills/{sale.Id}/lines",
            new BillLineInput(BillLineKind.Voucher, null, null, 1, 79m, 0, null, null, null, VoucherKind.SinglePurpose, spa.MassageTreatment.Id), Ct);
        var voucher = Assert.Single((await CheckoutAsync(spa, sale)).IssuedVouchers);
        var appointment = await spa.BookOkAsync(TestSpa.At(10), Ct, spa.Anna.Id);
        await spa.SetStatusAsync(appointment.Id, AppointmentStatus.CheckedIn, Ct);
        var bill = await spa.PostAsync<BillDto>("/api/pos/bills", new BillInput(register.Id, null, [appointment.Id]), Ct);
        await spa.PostAsync<ReceiptDto>($"/api/pos/bills/{bill.Id}/checkout",
            new CheckoutRequest(null, [new PaymentInput(PaymentMethod.Voucher, 0, voucher.Code, null)]), Ct);

        var report = await ManagementAsync(spa, ViennaToday, TestSpa.Monday);

        // Given ein Einzweckgutschein für die Massage, heute verkauft (Umsatz 65,83 € netto) und am Montag eingelöst
        // Then zählt die Behandlung 0 €, weil der Umsatz schon beim Verkauf entstand, und sie gilt als kassiert
        Assert.Equal(65.83m, Kpi(report, "serviceRevenueNet"));
        Assert.Equal(0m, Assert.Single(report.Treatments).NetRevenue);
        Assert.DoesNotContain(report.Alerts, a => a.Contains("nicht kassiert", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Stornierter_Beleg_nimmt_den_Umsatz_der_Behandlung_zurück()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var register = await RegisterAsync(spa);
        var appointment = await spa.BookOkAsync(TestSpa.At(10), Ct, spa.Anna.Id);
        await spa.SetStatusAsync(appointment.Id, AppointmentStatus.CheckedIn, Ct);
        var bill = await spa.PostAsync<BillDto>("/api/pos/bills", new BillInput(register.Id, null, [appointment.Id]), Ct);
        var receipt = await CheckoutAsync(spa, bill);
        await spa.PostAsync<ReceiptDto>($"/api/pos/receipts/{receipt.Id}/void", new VoidRequest("Falsche Behandlung"), Ct);

        var report = await ManagementAtAsync(spa, AfterAppointments, ViennaToday, TestSpa.Monday);

        // Given die Massage wurde kassiert und der Beleg storniert; Then zählt sie 0 € und ist wieder offen
        Assert.Equal(0m, Assert.Single(report.Treatments).NetRevenue);
        Assert.Contains("1 Termin noch nicht kassiert.", report.Alerts);
    }

    [Fact]
    public async Task Marge_rechnet_jeden_Termin_mit_den_Kosten_seines_Monats()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await spa.PutAsync<LocationCostSettingsDto>($"/api/costs/locations/{spa.Location.Id}/settings", new LocationCostSettingsDto(30m, 30m, 891m, 50, 75), Ct);
        var february = TestSpa.Monday.AddDays(-7);
        await spa.BookOkAsync(TestSpa.At(10, date: february), Ct, spa.Anna.Id);
        await spa.BookOkAsync(TestSpa.At(10), Ct, spa.Anna.Id);
        async Task<decimal> UnitCostAsync(string month) =>
            (await spa.GetAsync<CostAnalysisDto>($"/api/costs/locations/{spa.Location.Id}/analysis?month={month}", Ct))
            .Treatments.Single(t => t.TreatmentId == spa.MassageTreatment.Id).Total;
        var (februaryCost, marchCost) = (await UnitCostAsync("2027-02"), await UnitCostAsync("2027-03"));

        var report = await ManagementAsync(spa, february, TestSpa.Monday);

        // Given je ein noch nicht kassierter Termin im Februar und im März, deren Monate verschiedene Stückkosten haben
        // Then ist die Marge je Termin der Mittelwert beider Monate, nicht zweimal die Kosten des März
        Assert.NotEqual(februaryCost, marchCost);
        Assert.Equal(Math.Round(-(februaryCost + marchCost) / 2, 2), Assert.Single(report.Treatments).MarginPerAppointment);
    }

    [Fact]
    public async Task Kleine_Kassendifferenz_unter_fünf_Euro_ist_keine_Warnung()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var register = await RegisterAsync(spa);
        await spa.PostAsync<DayClosingDto>($"/api/pos/registers/{register.Id}/day-closings", new DayClosingInput(1.38m, null), Ct);

        var report = await ManagementAsync(spa, ViennaToday, ViennaToday);

        // Given ein Tagesabschluss mit 1,38 € zu viel in der Lade; Then steht das nicht unter den Warnungen
        Assert.DoesNotContain(report.Alerts, a => a.StartsWith("Kassendifferenzen", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Einzweckgutschein_bleibt_Umsatz_und_Gutscheine_zählen_verkauft_und_eingelöst()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var register = await RegisterAsync(spa);
        var gifts = await spa.PostAsync<BillDto>("/api/pos/bills", new BillInput(register.Id, null, []), Ct);
        await spa.PostAsync<BillDto>($"/api/pos/bills/{gifts.Id}/lines",
            new BillLineInput(BillLineKind.Voucher, null, null, 1, 100m, 0, null, null, null, VoucherKind.SinglePurpose, spa.MassageTreatment.Id), Ct);
        gifts = await spa.PostAsync<BillDto>($"/api/pos/bills/{gifts.Id}/lines",
            new BillLineInput(BillLineKind.Voucher, null, null, 1, 40m, 0, null, null, null, VoucherKind.MultiPurpose, null), Ct);
        var sold = await CheckoutAsync(spa, gifts);
        var value = sold.IssuedVouchers.Single(v => v.Kind == VoucherKind.MultiPurpose);
        var tea = await spa.PostAsync<BillDto>("/api/pos/bills", new BillInput(register.Id, null, []), Ct);
        tea = await spa.PostAsync<BillDto>($"/api/pos/bills/{tea.Id}/lines",
            new BillLineInput(BillLineKind.Custom, null, null, 1, 12.50m, 0, "Tee", TaxCategory.Normal, null, null, null), Ct);
        await spa.PostAsync<ReceiptDto>($"/api/pos/bills/{tea.Id}/checkout",
            new CheckoutRequest(null, [new PaymentInput(PaymentMethod.Voucher, 12.50m, value.Code, null)]), Ct);

        var report = await ManagementAsync(spa, ViennaToday, ViennaToday);

        // Given ein Einzweckgutschein für die Massage (100 €, beim Verkauf mit 20 % versteuert), ein Wertgutschein (40 €)
        // und ein Tee (12,50 €), der mit dem Wertgutschein bezahlt wurde
        // Then bleibt der Einzweckgutschein Umsatz, der Tee wird es beim Einlösen, der Wertgutschein beim Verkauf nicht
        Assert.Equal(93.75m, Kpi(report, "serviceRevenueNet"));
        Assert.Equal(140m, Kpi(report, "vouchersSold"));
        Assert.Equal(12.50m, Kpi(report, "vouchersRedeemed"));
    }

    [Fact]
    public async Task Hinweis_wenn_die_Vorperiode_vor_dem_ersten_Termin_beginnt()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await spa.BookOkAsync(TestSpa.At(10), Ct, spa.Anna.Id);

        var firstWeek = await ManagementAsync(spa, TestSpa.Monday, TestSpa.Monday.AddDays(6));
        var secondWeek = await ManagementAsync(spa, TestSpa.Monday.AddDays(7), TestSpa.Monday.AddDays(13));

        // Given der erste Termin des Standorts am Montag; Then fehlt der Woche davor der Verlauf, der Woche danach nicht
        Assert.Contains(firstWeek.Alerts, a => a.StartsWith("Vergleich reicht vor den Beginn der Aufzeichnung am 01.03.2027", StringComparison.Ordinal));
        Assert.DoesNotContain(secondWeek.Alerts, a => a.StartsWith("Vergleich reicht", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Künftiger_Termin_ohne_Beleg_ist_keine_Warnung()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await spa.BookOkAsync(TestSpa.At(10), Ct, spa.Anna.Id);

        var before = await ManagementAtAsync(spa, TestSpa.At(9), TestSpa.Monday, TestSpa.Monday);
        var after = await ManagementAtAsync(spa, AfterAppointments, TestSpa.Monday, TestSpa.Monday);

        // Given ein Termin am Montag um 10 Uhr ohne Beleg; Then fehlt die Kassa-Warnung, solange er nicht vorbei ist, und steht, sobald er vorbei ist
        Assert.DoesNotContain(before.Alerts, a => a.Contains("nicht kassiert", StringComparison.Ordinal));
        Assert.Contains("1 Termin noch nicht kassiert.", after.Alerts);
    }

    private static decimal Kpi(ManagementReportDto report, string key) => report.Kpis.Single(k => k.Key == key).Value;

    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    /// <summary>Die Testtermine liegen am Montag zwischen 9 und 17 Uhr; mit dieser Uhr sind sie alle vorbei, egal wann der Test läuft.</summary>
    private static DateTimeOffset AfterAppointments => TestSpa.At(23);

    /// <summary>
    /// Bericht mit fester Uhr. Angemeldet wird an dieser API, ein Cookie der Test-API von heute wäre dort schon abgelaufen (wie in <see cref="PosDayListTests"/>);
    /// Belege und Termine entstehen vorher mit der echten Uhr.
    /// </summary>
    private async Task<ManagementReportDto> ManagementAtAsync(TestSpa spa, DateTimeOffset now, DateOnly from, DateOnly until)
    {
        await using var host = fixture.Factory.WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddSingleton<TimeProvider>(new FixedTime(now))));
        using var anonymous = host.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        var signIn = await anonymous.PostAsJsonAsync("/api/auth/dev/register",
            new DevRegistration($"test-{Guid.NewGuid():N}@aispa.test", "Test Person", StaffRole.Owner, [spa.Location.Id], null), TestSpa.Json, Ct);
        signIn.EnsureSuccessStatusCode();
        using var client = host.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        client.DefaultRequestHeaders.Add("Cookie", string.Join("; ", signIn.Headers.GetValues("Set-Cookie").Select(c => c.Split(';')[0])));
        return (await client.GetFromJsonAsync<ManagementReportDto>(
            $"/api/reports/locations/{spa.Location.Id}/management?from={from:yyyy-MM-dd}&until={until:yyyy-MM-dd}", TestSpa.Json, Ct))!;
    }

    private static Task<ManagementReportDto> ManagementAsync(TestSpa spa, DateOnly from, DateOnly until) =>
        spa.GetAsync<ManagementReportDto>($"/api/reports/locations/{spa.Location.Id}/management?from={from:yyyy-MM-dd}&until={until:yyyy-MM-dd}", Ct);

    private static Task<CashRegisterDto> RegisterAsync(TestSpa spa) =>
        spa.PostAsync<CashRegisterDto>("/api/pos/registers", new CashRegisterInput(spa.Location.Id, $"R-{Guid.NewGuid():N}"[..14],
            FiscalCountry.AT, "Test-Spa GmbH", "Test-Spa GmbH", "AT61 1904 3002 3457 3201", null), Ct);

    private static Task<ReceiptDto> CheckoutAsync(TestSpa spa, BillDto bill) =>
        spa.PostAsync<ReceiptDto>($"/api/pos/bills/{bill.Id}/checkout", new CheckoutRequest(null, [new PaymentInput(PaymentMethod.Cash, bill.Open, null, null)]), Ct);
}
