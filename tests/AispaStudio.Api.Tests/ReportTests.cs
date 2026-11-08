using System.Net;
using System.Net.Http.Json;
using AispaStudio.Contracts;

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
        await spa.PostAsync<AppointmentDto>($"/api/appointments/{first.Id}/status", new AppointmentStatusChange(AppointmentStatus.Completed), Ct);
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
        await spa.PostAsync<AppointmentDto>($"/api/appointments/{done.Id}/status", new AppointmentStatusChange(AppointmentStatus.Completed), Ct);
        await spa.PostAsync<AppointmentDto>($"/api/appointments/{missed.Id}/status", new AppointmentStatusChange(AppointmentStatus.NoShow), Ct);
        await spa.PutAsync<RosterDayResult>($"/api/locations/{spa.Location.Id}/roster/{spa.Ben.Id}/2027-03-08",
            new RosterDayInput([new RosterEntry(RosterKind.Sick, null, null, null)], null), Ct);
        using var reception = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Reception, [spa.Location.Id]);

        var report = await spa.GetAsync<ManagementReportDto>(
            $"/api/reports/locations/{spa.Location.Id}/management?from={ViennaToday:yyyy-MM-dd}&until={TestSpa.Monday:yyyy-MM-dd}", Ct);
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
        Assert.Equal((2, 131.66m), (massage.Appointments, massage.NetRevenue));
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
}
