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
    public async Task Vergleich_waehlbar_Vormonat_und_Vorjahr_nennen_ihren_Zeitraum_und_ohne_Angabe_bleibt_es_die_Vorperiode()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var path = $"/api/reports/locations/{spa.Location.Id}/management?from=2026-11-01&until=2026-11-26";

        var standard = await spa.GetAsync<ManagementReportDto>(path, Ct);
        var month = await spa.GetAsync<ManagementReportDto>(path + "&compare=month", Ct);
        var year = await spa.GetAsync<ManagementReportDto>(path + "&compare=year", Ct);
        var wholeMonth = await spa.GetAsync<ManagementReportDto>(
            $"/api/reports/locations/{spa.Location.Id}/management?from=2026-11-01&until=2026-11-30&compare=month", Ct);
        var unknown = await spa.Client.GetAsync(path + "&compare=week", Ct);

        // Given der 1. bis 26. November; Then gilt ohne Angabe der gleich lange Zeitraum davor, „month“ ist 1. bis 26. Oktober und „year“ der Vorjahreszeitraum,
        // ein ganzer November wird mit dem ganzen Oktober verglichen und eine unbekannte Wahl abgelehnt
        Assert.Equal((new DateOnly(2026, 10, 6), new DateOnly(2026, 10, 31)), (standard.CompareFrom, standard.CompareUntil));
        Assert.Equal((new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 26)), (month.CompareFrom, month.CompareUntil));
        Assert.Equal((new DateOnly(2025, 11, 1), new DateOnly(2025, 11, 26)), (year.CompareFrom, year.CompareUntil));
        Assert.Equal(new DateOnly(2026, 10, 31), wholeMonth.CompareUntil);
        Assert.Equal(global::System.Net.HttpStatusCode.BadRequest, unknown.StatusCode);
    }

    [Fact]
    public async Task Kennzahlen_sagen_ob_ein_hoeherer_Wert_gut_ist_und_neutrale_Werte_bleiben_leer()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);

        var report = await ManagementAsync(spa, TestSpa.Monday, TestSpa.Monday);

        // Then ist mehr Umsatz gut, mehr No-Shows sind schlecht und offene Gutscheine sind weder noch
        Assert.True(report.Kpis.Single(k => k.Key == "revenue").HigherIsBetter);
        Assert.False(report.Kpis.Single(k => k.Key == "noShowRate").HigherIsBetter);
        Assert.Null(report.Kpis.Single(k => k.Key == "voucherLiabilities").HigherIsBetter);
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

    [Fact]
    public async Task Deckungsbeitrag_ist_Belegumsatz_minus_Vollkosten_des_Monats_und_Umsatz_je_Behandlungsstunde_aus_dem_Beleg()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await spa.PutAsync<LocationCostSettingsDto>($"/api/costs/locations/{spa.Location.Id}/settings", new LocationCostSettingsDto(30m, 30m, 891m, 50, 75), Ct);
        var register = await RegisterAsync(spa);
        var appointment = await spa.BookOkAsync(TestSpa.At(10), Ct, spa.Anna.Id);
        await spa.SetStatusAsync(appointment.Id, AppointmentStatus.CheckedIn, Ct);
        var bill = await spa.PostAsync<BillDto>("/api/pos/bills", new BillInput(register.Id, null, []), Ct);
        bill = await spa.PostAsync<BillDto>($"/api/pos/bills/{bill.Id}/lines",
            new BillLineInput(BillLineKind.Appointment, appointment.Id, null, 1, null, 10m, null, null, null, null, null), Ct);
        await CheckoutAsync(spa, bill);
        var marchCost = (await spa.GetAsync<CostAnalysisDto>($"/api/costs/locations/{spa.Location.Id}/analysis?month=2027-03", Ct))
            .Treatments.Single(t => t.TreatmentId == spa.MassageTreatment.Id).Total;

        var report = await ManagementAsync(spa, ViennaToday, TestSpa.Monday);

        // Given eine Massage (50 Minuten) im März, mit 10 € Rabatt kassiert: 57,50 € netto statt 65,83 € laut Terminpreis
        // Then ist der Deckungsbeitrag 57,50 € minus die Vollkosten des März, und je Behandlungsstunde zählen 69 € aus dem Beleg
        Assert.Equal(57.50m - marchCost, Kpi(report, "contributionMargin"));
        Assert.Equal(Math.Round((57.50m - marchCost) / 57.50m * 100, 1), Kpi(report, "contributionMarginRate"));
        Assert.Equal(69m, Kpi(report, "serviceRevenuePerTreatmentHour"));
        Assert.Equal(57.50m, Kpi(report, "revenuePerGuest"));
    }

    [Fact]
    public async Task Entgangener_Umsatz_steht_für_No_Show_und_Storno_getrennt_und_weniger_ist_besser()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        foreach (var (hour, employee) in new[] { (10, spa.Anna.Id), (12, spa.Ben.Id) })
        {
            var missed = await spa.BookOkAsync(TestSpa.At(hour), Ct, employee);
            await spa.SetStatusAsync(missed.Id, AppointmentStatus.NoShow, Ct);
        }

        var cancelled = await spa.BookOkAsync(TestSpa.At(14), Ct, spa.Anna.Id);
        await spa.PostAsync<AppointmentDto>($"/api/appointments/{cancelled.Id}/cancel", new CancelAppointmentRequest(null), Ct);

        var report = await ManagementAsync(spa, TestSpa.Monday, TestSpa.Monday);

        // Given zwei Massagen zu 79 € ohne Erscheinen und eine stornierte; Then fehlen 158 € durch No-Show und 79 € durch Storno
        Assert.Equal(158m, Kpi(report, "lostRevenueNoShow"));
        Assert.Equal(79m, Kpi(report, "lostRevenueCancellation"));
        Assert.False(report.Kpis.Single(k => k.Key == "lostRevenueNoShow").HigherIsBetter);
    }

    [Fact]
    public async Task Verlauf_hat_bis_62_Tage_einen_Punkt_je_Tag_und_darüber_einen_je_Woche()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await spa.BookOkAsync(TestSpa.At(10, date: TestSpa.Monday.AddDays(1)), Ct, spa.Anna.Id);

        var days = await ManagementAsync(spa, TestSpa.Monday, TestSpa.Monday.AddDays(61));
        var weeks = await ManagementAsync(spa, TestSpa.Monday, TestSpa.Monday.AddDays(62));

        // Given ein Termin am Dienstag; Then zeigen 62 Tage 62 Tagespunkte und 63 Tage neun Wochen von Montag bis Sonntag, je mit dem Termin am richtigen Punkt
        Assert.Equal(62, days.Trend!.Count);
        Assert.Equal((TestSpa.Monday.AddDays(1), TestSpa.Monday.AddDays(1), 1), (days.Trend[1].From, days.Trend[1].Until, days.Trend[1].Appointments));
        Assert.Equal(9, weeks.Trend!.Count);
        Assert.Equal((TestSpa.Monday, TestSpa.Monday.AddDays(6), 1), (weeks.Trend[0].From, weeks.Trend[0].Until, weeks.Trend[0].Appointments));
        Assert.Equal(0, weeks.Trend[1].Appointments);
    }

    [Fact]
    public void Ohne_Betragsrecht_fehlen_Deckungsbeitrag_Anteile_aus_Umsätzen_und_der_Umsatz_im_Verlauf()
    {
        var shown = new ManagementReportDto(Guid.Empty, TestSpa.Monday, TestSpa.Monday,
            [new("contributionMarginRate", "Deckungsbeitrag in Prozent", 40, 35, "%", true), new("productShare", "Produktanteil", 12, 10, "%", true),
                new("staffUtilization", "Auslastung Personal", 60, 50, "%", true)],
            [], [], [], [], [], Trend: [new(TestSpa.Monday, TestSpa.Monday, 100m, 2, 50m)]);

        var hidden = Reporting.ManagementReports.WithoutAmounts(shown);

        // Then bleiben Auslastung und Anzahl, Quoten aus Geldbeträgen und der Umsatz je Tag fehlen
        Assert.Equal(["staffUtilization"], hidden.Kpis.Select(k => k.Key));
        Assert.Equal(new TrendPoint(TestSpa.Monday, TestSpa.Monday, null, 2, 50m), Assert.Single(hidden.Trend!));
    }

    [Fact]
    public async Task Auslastung_nach_Wochentag_und_Tageszeit_teilt_Termine_an_den_Grenzen_und_Termine_stehen_nach_Herkunft()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await spa.BookOkAsync(TestSpa.At(9, 10), Ct, spa.Ben.Id);
        await spa.BookOkAsync(TestSpa.At(10, 30), Ct, spa.Anna.Id);

        var report = await ManagementAsync(spa, TestSpa.Monday, TestSpa.Monday);

        // Given Anna und Ben im Dienst von 9 bis 17 Uhr, eine Massage um 9:10 und eine um 10:30 (50 Minuten, 20 davon nach 11 Uhr)
        // Then ist der Vormittag zu 80 von 240 Minuten belegt, der Mittag zu 20 von 360, der Nachmittag leer und der Abend ohne Dienst
        Assert.Equal(new WeekdayLoad(DayOfWeek.Monday, 33.3m, 5.6m, 0m, null), Assert.Single(report.LoadByWeekday!));
        Assert.Equal(50m, report.WeakLoadBelow);
        Assert.Equal([new ChannelKpi(AppointmentSource.Reception, "Rezeption", 2)], report.Channels);
        Assert.Equal(2, report.Rooms!.Sum(r => r.Appointments));
    }

    [Fact]
    public async Task Abwandernde_Stammgäste_haben_drei_Besuche_seit_über_90_Tagen_keinen_und_keinen_künftigen_Termin_und_ihr_Name_bleibt_aus_den_KI_Fakten()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var udo = await spa.PostAsync<CustomerDto>("/api/customers", new CustomerInput("Udo", $"Bald{Guid.NewGuid():N}", null, null, null, null), Ct);
        var ida = await spa.PostAsync<CustomerDto>("/api/customers", new CustomerInput("Ida", $"Selten{Guid.NewGuid():N}", null, null, null, null), Ct);
        foreach (var hour in new[] { 10, 12, 14 })
        {
            await spa.BookOkAsync(TestSpa.At(hour), Ct, spa.Anna.Id);
            await spa.BookOkAsync(TestSpa.At(hour), Ct, spa.Ben.Id, customer: udo.Id);
        }

        await spa.BookOkAsync(TestSpa.At(16), Ct, spa.Anna.Id, customer: ida.Id);
        await spa.BookOkAsync(TestSpa.At(16), Ct, spa.Ben.Id, customer: ida.Id);
        var later = TestSpa.Monday.AddDays(105);
        await spa.BookOkAsync(TestSpa.At(10, date: later), Ct, spa.Anna.Id, customer: udo.Id);

        var report = await ManagementAtAsync(spa, TestSpa.At(10, date: TestSpa.Monday.AddDays(98)), TestSpa.Monday, TestSpa.Monday);

        // Given 98 Tage nach drei Besuchen von Eva und Udo und zwei von Ida; Udo hat noch einen Termin vor sich
        // Then wandert nur Eva ab, mit letztem Besuch und drei Besuchen, und die Fakten für die KI nennen die Anzahl, nicht ihren Namen
        Assert.Equal(1, Kpi(report, "lapsingRegulars"));
        var eva = Assert.Single(report.LapsingGuests!);
        Assert.Equal((spa.Customer.Id, TestSpa.Monday, 3), (eva.CustomerId, eva.LastVisit, eva.Visits));
        Assert.NotNull(report.TopGuests);
        var facts = Reporting.ManagementSummaries.FactsText(report);
        Assert.Contains("Abwandernde Stammgäste (heute): 1", facts, StringComparison.Ordinal);
        Assert.DoesNotContain(eva.Name, facts, StringComparison.Ordinal);

        // And ohne das Recht auf Gästenamen (so rufen KI-Kommentar und Mail auf) fehlen die Listen
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<Tenancy.TenantContext>().Use(spa.TenantId);
        var withoutNames = await scope.ServiceProvider.GetRequiredService<Reporting.ManagementReports>()
            .ManagementAsync(spa.Location.Id, TestSpa.Monday, TestSpa.Monday, true, Ct);
        Assert.Null(withoutNames!.LapsingGuests);
        Assert.Null(withoutNames.TopGuests);
    }

    [Fact]
    public async Task Vorschau_zeigt_vier_Wochen_ab_heute_mit_gebuchtem_Umsatz_und_gebuchter_Auslastung()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await spa.BookOkAsync(TestSpa.At(10), Ct, spa.Anna.Id);
        await spa.BookOkAsync(TestSpa.At(10, date: TestSpa.Monday.AddDays(7)), Ct, spa.Anna.Id);

        var report = await ManagementAtAsync(spa, TestSpa.At(8), TestSpa.Monday, TestSpa.Monday);

        // Given heute ist der Testmontag, je Montag eine Massage zu 79 € und Anna und Ben je acht Stunden im Dienst
        // Then zeigen die ersten beiden Wochen je einen Termin, 79 € und 50 von 960 Minuten, die letzten zwei nichts
        Assert.Equal(TestSpa.Monday, report.ForecastAsOf);
        Assert.Equal(
            [
                new ForecastWeek(TestSpa.Monday, TestSpa.Monday.AddDays(6), 1, 79m, 5.2m),
                new ForecastWeek(TestSpa.Monday.AddDays(7), TestSpa.Monday.AddDays(13), 1, 79m, 5.2m),
                new ForecastWeek(TestSpa.Monday.AddDays(14), TestSpa.Monday.AddDays(20), 0, 0m, 0m),
                new ForecastWeek(TestSpa.Monday.AddDays(21), TestSpa.Monday.AddDays(27), 0, 0m, 0m),
            ], report.Forecast);
    }

    [Fact]
    public void Ohne_Betragsrecht_fehlen_die_Ausgaben_der_besten_Gäste_und_der_gebuchte_Umsatz_der_Vorschau()
    {
        var shown = new ManagementReportDto(Guid.Empty, TestSpa.Monday, TestSpa.Monday, [], [], [], [], [], [])
        {
            TopGuests = [new(Guid.Empty, "Eva", 3, 240m)],
            Forecast = [new(TestSpa.Monday, TestSpa.Monday.AddDays(6), 4, 316m, 20m)],
        };

        var hidden = Reporting.ManagementReports.WithoutAmounts(shown);

        // Then bleiben Besuche, Termine und Auslastung, die Beträge fehlen
        Assert.Equal(new GuestKpi(Guid.Empty, "Eva", 3, null), Assert.Single(hidden.TopGuests!));
        Assert.Equal(new ForecastWeek(TestSpa.Monday, TestSpa.Monday.AddDays(6), 4, null, 20m), Assert.Single(hidden.Forecast!));
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
