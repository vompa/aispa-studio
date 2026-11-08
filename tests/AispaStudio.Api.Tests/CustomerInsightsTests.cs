using System.Net;
using System.Net.Http.Json;
using AispaStudio.Api.Scheduling;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Tests;

/// <summary>Kundenkarte: Kennzahlen aus Kalender und Kassa, berechnete Sterne, Rezeptionsvermerk nur für Rezeption und Leitung.</summary>
public sealed class CustomerInsightsTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Kennzahlen_aus_Kalender_und_Kassa_Umsatz_nur_mit_Recht_Vermerk_nur_fuer_die_Rezeption()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var path = $"/api/customers/{spa.Customer.Id}/insights";
        var empty = await spa.GetAsync<CustomerInsights>(path, Ct);
        Assert.Equal((0, 0, "Kein Besuch in zwölf Monaten"), (empty.Visits, empty.Stars, empty.StarsText));
        Assert.Null(empty.IntervalDays);

        // Drei Besuche im Abstand von vier Wochen, zwei bei Anna, einer bei Ben; einmal nicht erschienen.
        var register = await spa.PostAsync<CashRegisterDto>("/api/pos/registers",
            new CashRegisterInput(spa.Location.Id, $"T-{Guid.NewGuid():N}"[..14], FiscalCountry.AT, "Test-Spa GmbH\nTeststraße 1, 1010 Wien\nATU12345678",
                "Test-Spa GmbH", "AT61 1904 3002 3457 3201", null), Ct);
        foreach (var (weeks, employee) in new[] { (0, spa.Anna), (4, spa.Ben), (8, spa.Anna) })
        {
            var visit = await spa.BookOkAsync(TestSpa.At(10, date: TestSpa.Monday.AddDays(7 * weeks)), Ct, employee.Id);
            await spa.SetStatusAsync(visit.Id, AppointmentStatus.CheckedIn, Ct);
            var bill = await spa.PostAsync<BillDto>("/api/pos/bills", new BillInput(register.Id, null, [visit.Id]), Ct);
            await spa.PostAsync<ReceiptDto>($"/api/pos/bills/{bill.Id}/checkout",
                new CheckoutRequest(null, [new PaymentInput(PaymentMethod.Cash, 79m, null, null)]), Ct);
            await spa.SetStatusAsync(visit.Id, AppointmentStatus.Completed, Ct);
        }

        var missed = await spa.BookOkAsync(TestSpa.At(10, date: TestSpa.Monday.AddDays(7 * 12)), Ct, spa.Anna.Id);
        await spa.SetStatusAsync(missed.Id, AppointmentStatus.NoShow, Ct);
        await spa.Client.PutAsJsonAsync($"/api/customers/{spa.Customer.Id}/reception-note", new CustomerReceptionNoteInput(" Mann holt ab "), TestSpa.Json, Ct);

        var owner = await spa.GetAsync<CustomerInsights>(path, Ct);
        Assert.Equal((3, 28, 1), (owner.Visits, owner.IntervalDays, owner.Stars));
        Assert.Equal("Stammgast: 3 Besuche in zwölf Monaten", owner.StarsText);
        Assert.Equal([TestSpa.Monday.AddDays(7 * 12)], owner.NoShows);
        Assert.Equal(TestSpa.Monday.AddDays(7 * 8), owner.LastVisit);
        Assert.Equal(new CustomerRevenue("EUR", 237m, 237m), Assert.Single(owner.Revenue!));
        Assert.Equal([("Anna", 2, (decimal?)158m), ("Ben", 1, 79m)], owner.Employees.Select(e => (e.Name, e.Visits, e.Revenue)));
        Assert.Equal((true, "Mann holt ab"), (owner.MaySeeReceptionNote, owner.ReceptionNote));

        using var reception = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Reception, [spa.Location.Id]);
        var atDesk = await TestSpa.ReadAsync<CustomerInsights>(await reception.GetAsync(path, Ct), Ct);
        Assert.Null(atDesk.Revenue);
        Assert.All(atDesk.Employees, e => Assert.Null(e.Revenue));
        Assert.Equal("Mann holt ab", atDesk.ReceptionNote);

        using var therapist = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Therapist, [spa.Location.Id], spa.Anna.Id);
        var inRoom = await TestSpa.ReadAsync<CustomerInsights>(await therapist.GetAsync(path, Ct), Ct);
        Assert.Equal((3, false, null), (inRoom.Visits, inRoom.MaySeeReceptionNote, inRoom.ReceptionNote));
        var change = await therapist.PutAsJsonAsync($"/api/customers/{spa.Customer.Id}/reception-note", new CustomerReceptionNoteInput("x"), TestSpa.Json, Ct);
        Assert.Equal(HttpStatusCode.Forbidden, change.StatusCode);

        // Vermerk im Kalender nicht sichtbar, leer löscht ihn.
        var day = await spa.GetAsync<CalendarDay>($"/api/locations/{spa.Location.Id}/calendar?date={TestSpa.Monday:yyyy-MM-dd}", Ct);
        Assert.Null(Assert.Single(day.Guests!).Note);
        await spa.Client.PutAsJsonAsync($"/api/customers/{spa.Customer.Id}/reception-note", new CustomerReceptionNoteInput(""), TestSpa.Json, Ct);
        Assert.Null((await spa.GetAsync<CustomerInsights>(path, Ct)).ReceptionNote);
    }

    [Fact]
    public async Task Offene_Gutscheine_und_letzte_Kaeufe_nur_mit_dem_Recht_zu_buchen()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var steiner = await spa.PostAsync<CustomerDto>("/api/customers", new CustomerInput("Christian", "Steiner", null, null, null, null), Ct);
        var register = await spa.PostAsync<CashRegisterDto>("/api/pos/registers", new CashRegisterInput(spa.Location.Id, $"K-{Guid.NewGuid():N}"[..14], FiscalCountry.AT,
            "Test-Spa GmbH", "Test-Spa GmbH", "AT61 1904 3002 3457 3201", null), Ct);
        async Task<ReceiptDto> SellAsync(CustomerDto guest, Guid[] appointments, BillLineInput[] lines, params PaymentInput[] payments)
        {
            foreach (var appointmentId in appointments)
            {
                await spa.SetStatusAsync(appointmentId, AppointmentStatus.CheckedIn, Ct);
            }

            var bill = await spa.PostAsync<BillDto>("/api/pos/bills", new BillInput(register.Id, guest.Id, appointments), Ct);
            foreach (var line in lines)
            {
                bill = await spa.PostAsync<BillDto>($"/api/pos/bills/{bill.Id}/lines", line, Ct);
            }

            return await spa.PostAsync<ReceiptDto>($"/api/pos/bills/{bill.Id}/checkout", new CheckoutRequest(null, payments), Ct);
        }

        static BillLineInput Value(decimal value) => new(BillLineKind.Voucher, null, null, 1, value, 0, null, null, null, VoucherKind.MultiPurpose, null);
        static BillLineInput Item(string name, int quantity, decimal price) => new(BillLineKind.Custom, null, null, quantity, price, 0, name, TaxCategory.Normal, null, null, null);
        static PaymentInput Cash(decimal amount) => new(PaymentMethod.Cash, amount, null, null);
        static PaymentInput Voucher(VoucherDto voucher, decimal amount) => new(PaymentMethod.Voucher, amount, voucher.Code, null);

        // Zwei Wertgutscheine gekauft (50 und 10), damit einen Besuch bezahlt: der 50er hat noch 30 offen, der 10er ist leer.
        var sold = await SellAsync(steiner, [], [Value(50m), Value(10m)], Cash(60m));
        var fifty = sold.IssuedVouchers.Single(v => v.InitialValue == 50m);
        var ten = sold.IssuedVouchers.Single(v => v.InitialValue == 10m);
        var first = await spa.BookOkAsync(TestSpa.At(10), Ct, spa.Anna.Id, customer: steiner.Id);
        await SellAsync(steiner, [first.Id], [], Voucher(fifty, 20m), Voucher(ten, 10m), Cash(49m));

        // Ein geschenkter Behandlungsgutschein über 100, beim Besuch mit der Massage (79) eingelöst: 21 bleiben offen. Ein Gutschein eines anderen Gastes erscheint nicht.
        var gift = await SellAsync(spa.Customer, [], [new(BillLineKind.Voucher, null, null, 1, 100m, 0, null, null, null, VoucherKind.SinglePurpose, spa.MassageTreatment.Id), Value(40m)],
            Cash(140m));
        var massage = gift.IssuedVouchers.Single(v => v.Kind == VoucherKind.SinglePurpose);
        var second = await spa.BookOkAsync(TestSpa.At(14), Ct, spa.Ben.Id, customer: steiner.Id);
        await SellAsync(steiner, [second.Id], [], Voucher(massage, 0m));

        // Käufe: stornierter Beleg, Paketeinlösung und Termine erscheinen nicht; höchstens fünf, der neueste Beleg zuerst.
        await SellAsync(steiner, [], [Item("Massageöl", 2, 12m), Item("Badetuch", 1, 8m)], Cash(32m));
        var tea = await SellAsync(steiner, [], [Item("Tee", 1, 10m)], Cash(10m));
        await spa.PostAsync<ReceiptDto>($"/api/pos/receipts/{tea.Id}/void", new VoidRequest("Falsch kassiert"), Ct);
        var package = await SellAsync(steiner, [], [new(BillLineKind.Voucher, null, null, 1, 200m, 0, null, null, null, VoucherKind.SinglePurpose, spa.MassageTreatment.Id, Units: 3)],
            Cash(200m));
        var third = await spa.BookOkAsync(TestSpa.At(10, date: TestSpa.Monday.AddDays(7)), Ct, spa.Anna.Id, customer: steiner.Id);
        await SellAsync(steiner, [third.Id], [], Voucher(Assert.Single(package.IssuedVouchers), 0m));
        var last = await SellAsync(steiner, [], [Item("Kerze", 1, 5m), Item("Seife", 1, 4m)], Cash(9m));

        var path = $"/api/customers/{steiner.Id}/insights";
        var owner = await spa.GetAsync<CustomerInsights>(path, Ct);
        Assert.Equal([(VoucherKind.MultiPurpose, fifty.Code[^4..], (string?)null, 30m), (VoucherKind.SinglePurpose, massage.Code[^4..], "Massage", 21m)],
            owner.Vouchers!.Select(v => (v.Kind, v.CodeEnd, v.TreatmentName, v.Balance)));
        Assert.All(owner.Vouchers!, v => Assert.Equal("EUR", v.Currency));
        Assert.Equal([("Kerze", 1, 5m), ("Seife", 1, 4m), ("Paket 3× Massage", 1, 200m), ("Massageöl", 2, 24m), ("Badetuch", 1, 8m)],
            owner.Purchases!.Select(p => (p.Description, p.Quantity, p.Amount)));
        var zone = TimeZoneInfo.FindSystemTimeZoneById(spa.Location.TimeZone);
        Assert.Equal(DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(last.IssuedAt, zone).DateTime), owner.Purchases![0].Date);

        using var reception = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Reception, [spa.Location.Id]);
        var atDesk = await TestSpa.ReadAsync<CustomerInsights>(await reception.GetAsync(path, Ct), Ct);
        Assert.Equal([30m, 21m], atDesk.Vouchers!.Select(v => v.Balance));
        Assert.Equal(5, atDesk.Purchases!.Count);

        using var therapist = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Therapist, [spa.Location.Id], spa.Anna.Id);
        var inRoom = await TestSpa.ReadAsync<CustomerInsights>(await therapist.GetAsync(path, Ct), Ct);
        Assert.Null(inRoom.Vouchers);
        Assert.Null(inRoom.Purchases);
    }

    [Fact]
    public void Sterne_nach_festen_Regeln_und_ueblicher_Abstand()
    {
        Assert.Equal(0, CustomerInsightsService.Stars(2, 500m, false).Stars);
        Assert.Equal(1, CustomerInsightsService.Stars(3, 0m, false).Stars);
        Assert.Equal((2, "6 Besuche in zwölf Monaten"), CustomerInsightsService.Stars(6, 0m, false));
        Assert.Equal((2, "Umsatz in zwölf Monaten über der Schwelle"), CustomerInsightsService.Stars(1, 1000m, false));
        Assert.Equal(3, CustomerInsightsService.Stars(1, 50m, true).Stars);

        var start = new DateTimeOffset(2026, 1, 5, 10, 0, 0, TimeSpan.Zero);
        Assert.Null(CustomerInsightsService.IntervalDays([start, start.AddDays(30)]));
        Assert.Equal(35, CustomerInsightsService.IntervalDays([start, start.AddDays(30), start.AddDays(65), start.AddDays(105)]));
    }
}
