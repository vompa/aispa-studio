using System.Net;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Tests;

/// <summary>Runde 5, Ä34: der Kundenüberblick im Termin-Panel, aus einem Aufruf; Beträge nur mit „Berichte mit Beträgen“, nie Gesundheitsdaten.</summary>
public sealed class CustomerSummaryTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string SummaryPath(CustomerDto customer) => $"/api/customers/{customer.Id}/summary";

    private static async Task<AppointmentDto> VisitAsync(TestSpa spa, int weeks, EmployeeDto employee, AppointmentStatus status)
    {
        var appointment = await spa.BookOkAsync(TestSpa.At(10, date: TestSpa.Monday.AddDays(7 * weeks)), Ct, employee.Id);
        return await spa.SetStatusAsync(appointment.Id, status, Ct);
    }

    [Fact]
    public async Task Ohne_Besuche_ist_alles_leer_und_ein_unbekannter_Gast_ist_nicht_gefunden()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);

        var empty = await spa.GetAsync<CustomerSummary>(SummaryPath(spa.Customer), Ct);

        Assert.Equal((0, null, 0, null, false), (empty.Visits, empty.LastVisit, empty.NoShows, empty.Next, empty.AnamnesisOnFile));
        Assert.Empty(empty.Recent);
        Assert.Null(empty.Open);
        var unknown = await spa.Client.GetAsync($"/api/customers/{Guid.NewGuid()}/summary", Ct);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
    }

    [Fact]
    public async Task Kennzahlen_Zuletzt_Naechster_Termin_Notiz_und_Kontakt()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await spa.PutAsync<CustomerDto>($"/api/customers/{spa.Customer.Id}",
            new CustomerInput(spa.Customer.FirstName, spa.Customer.LastName, "gast@example.test", "+43 664 111", null, " Kommt immer mit Hund "), Ct);

        // Sechs erledigte Besuche im Wochenabstand abwechselnd bei Anna und Ben, einmal nicht erschienen, einmal storniert, einmal in der Zukunft gebucht.
        var done = new List<AppointmentDto>();
        for (var week = 0; week < 6; week++)
        {
            done.Add(await VisitAsync(spa, week, week % 2 == 0 ? spa.Anna : spa.Ben, AppointmentStatus.Completed));
        }

        await VisitAsync(spa, 6, spa.Anna, AppointmentStatus.NoShow);
        var cancelled = await spa.BookOkAsync(TestSpa.At(13, date: TestSpa.Monday.AddDays(7 * 7)), Ct, spa.Anna.Id);
        await spa.PostAsync<AppointmentDto>($"/api/appointments/{cancelled.Id}/cancel", new CancelAppointmentRequest(null), Ct);
        var later = await spa.BookOkAsync(TestSpa.At(11, date: TestSpa.Monday.AddDays(7 * 9)), Ct, spa.Anna.Id);
        var earlier = await spa.BookOkAsync(TestSpa.At(11, date: TestSpa.Monday.AddDays(7 * 8)), Ct, spa.Ben.Id);

        var summary = await spa.GetAsync<CustomerSummary>(SummaryPath(spa.Customer), Ct);

        Assert.Equal((6, 1), (summary.Visits, summary.NoShows));
        Assert.Equal(TestSpa.Monday.AddDays(7 * 5), summary.LastVisit);
        Assert.Equal(new CustomerNextAppointment(earlier.Id, earlier.Start, "Massage"), summary.Next);
        Assert.NotEqual(later.Id, summary.Next!.AppointmentId);
        // Zuletzt: die fünf neuesten erledigten Behandlungen, neueste zuerst, mit der Person.
        Assert.Equal(done.AsEnumerable().Reverse().Take(5).Select(a => a.Id), summary.Recent.Select(r => r.AppointmentId));
        Assert.Equal(["Ben", "Anna", "Ben", "Anna", "Ben"], summary.Recent.Select(r => r.EmployeeName));
        Assert.All(summary.Recent, r => Assert.Equal("Massage", r.TreatmentName));
        Assert.Equal(("Kommt immer mit Hund", "+43 664 111", "gast@example.test"), (summary.Note, summary.Phone, summary.Email));
    }

    [Fact]
    public async Task Offener_Betrag_und_Pakete_nur_mit_Berichten_mit_Betraegen_und_nur_wenn_es_sie_gibt()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var register = await spa.PostAsync<CashRegisterDto>("/api/pos/registers",
            new CashRegisterInput(spa.Location.Id, $"T-{Guid.NewGuid():N}"[..14], FiscalCountry.AT, "Test-Spa GmbH\nTeststraße 1, 1010 Wien\nATU12345678",
                "Test-Spa GmbH", "AT61 1904 3002 3457 3201", null), Ct);
        Assert.Null((await spa.GetAsync<CustomerSummary>(SummaryPath(spa.Customer), Ct)).Open);

        // Offene Rechnung über eine Massage, dazu ein Paket aus einer anderen, bezahlten Rechnung.
        var open = await spa.BookOkAsync(TestSpa.At(10), Ct, spa.Anna.Id);
        await spa.SetStatusAsync(open.Id, AppointmentStatus.CheckedIn, Ct);
        await spa.PostAsync<BillDto>("/api/pos/bills", new BillInput(register.Id, null, [open.Id]), Ct);
        var packageVisit = await spa.BookOkAsync(TestSpa.At(13), Ct, spa.Anna.Id);
        await spa.SetStatusAsync(packageVisit.Id, AppointmentStatus.CheckedIn, Ct);
        var packageBill = await spa.PostAsync<BillDto>("/api/pos/bills", new BillInput(register.Id, null, [packageVisit.Id]), Ct);
        var validUntil = new DateOnly(2028, 12, 31);
        await spa.PostAsync<BillDto>($"/api/pos/bills/{packageBill.Id}/lines",
            new BillLineInput(BillLineKind.Voucher, null, null, 1, 200m, 0, null, null, null, VoucherKind.SinglePurpose, spa.MassageTreatment.Id, Units: 3, ValidUntil: validUntil), Ct);
        await spa.PostAsync<ReceiptDto>($"/api/pos/bills/{packageBill.Id}/checkout",
            new CheckoutRequest(null, [new PaymentInput(PaymentMethod.Cash, 279m, null, null)]), Ct);

        var owner = await spa.GetAsync<CustomerSummary>(SummaryPath(spa.Customer), Ct);

        Assert.Equal(79m, owner.Open!.OpenAmount);
        Assert.Equal("EUR", owner.Open.Currency);
        Assert.Equal(new GuestPackage(spa.MassageTreatment.Id, "Massage", 3, 3, validUntil), Assert.Single(owner.Open.Packages));

        // Die Rezeption ohne das Recht sieht weder den Betrag noch das Paket; der Rest des Überblicks bleibt.
        using var reception = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Reception, [spa.Location.Id]);
        var atDesk = await TestSpa.ReadAsync<CustomerSummary>(await reception.GetAsync(SummaryPath(spa.Customer), Ct), Ct);
        Assert.Null(atDesk.Open);
        Assert.Equal(owner.Next, atDesk.Next);
    }

    [Fact]
    public async Task Termine_anderer_Standorte_ohne_Zugang_zaehlen_nicht()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var other = await spa.CreateLocationAsync("Zweiter Standort", [DayOfWeek.Monday], Ct);
        await VisitAsync(spa, 0, spa.Anna, AppointmentStatus.Completed);

        using var reception = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Reception, [other.Id]);
        var elsewhere = await TestSpa.ReadAsync<CustomerSummary>(await reception.GetAsync(SummaryPath(spa.Customer), Ct), Ct);

        Assert.Equal((0, null), (elsewhere.Visits, elsewhere.LastVisit));
        Assert.Empty(elsewhere.Recent);
    }
}
