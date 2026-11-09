using System.Net;
using System.Net.Http.Json;
using AispaStudio.Api.Sync;
using AispaStudio.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace AispaStudio.Api.Tests;

/// <summary>
/// Seite „Verlauf“ (Arbeitsanweisung Verlauf, Aufgabe A): <c>GET /api/locations/{id}/history</c> liefert alles, was im Betrieb passiert ist, seitenweise und
/// gefiltert; Zahlungen kommen aus der Kassa dazu und stehen auch im Verlauf des Termins. Die Einträge entstehen zur echten Uhrzeit, die Termine liegen im März 2027.
/// </summary>
public sealed class ActivityFeedTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly TimeZoneInfo Vienna = TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna");

    private static DateOnly ViennaToday => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, Vienna).DateTime);

    private static string Path(TestSpa spa, string query = "") => $"/api/locations/{spa.Location.Id}/history{query}";

    [Fact]
    public async Task Buchen_Verschieben_Stornieren_und_Status_stehen_neueste_zuerst_mit_Art_Gast_Person_und_Werten()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var appointment = await spa.BookOkAsync(TestSpa.At(14), Ct, spa.Anna.Id);
        _ = await spa.PostAsync<AppointmentDto>($"/api/appointments/{appointment.Id}/move", new MoveAppointmentRequest(TestSpa.At(15, 30), spa.Anna.Id, null, null), Ct);
        await spa.SetStatusAsync(appointment.Id, AppointmentStatus.CheckedIn, Ct);
        _ = await spa.PostAsync<AppointmentDto>($"/api/appointments/{appointment.Id}/cancel", new CancelAppointmentRequest("Gast krank"), Ct);

        var page = await spa.GetAsync<ActivityPage>(Path(spa), Ct);

        var texts = page.Items.Where(i => i.AppointmentId == appointment.Id).Select(i => (i.Kind, i.Text)).ToList();
        Assert.Equal(
        [
            (ActivityKind.Cancelled, "Storniert von Test Person: Gast krank"),
            (ActivityKind.StatusChanged, "Eingecheckt automatisch"),
            (ActivityKind.Moved, "Verschoben von Test Person: 14:00 → 15:30"),
            (ActivityKind.Booked, "Angelegt von Test Person"),
        ], texts);
        var moved = page.Items.Single(i => i.Kind == ActivityKind.Moved);
        Assert.Equal(("Test Person", ActivityChannel.Reception, spa.Customer.Id, spa.MassageTreatment.Id, spa.Anna.Id),
            (moved.Actor, moved.Channel, moved.CustomerId, moved.TreatmentId, moved.EmployeeId));
        Assert.StartsWith("Eva Muster", moved.CustomerName, StringComparison.Ordinal);
        Assert.Equal(new ActivityValue("Beginn", "1.3.2027 14:00", "1.3.2027 15:30"), Assert.Single(moved.Values));
        var cancelled = page.Items.Single(i => i.Kind == ActivityKind.Cancelled);
        Assert.Contains(new ActivityValue("Grund", null, "Gast krank"), cancelled.Values);
        Assert.Null(page.NextBefore);
        Assert.True(page.Items.Zip(page.Items.Skip(1)).All(p => p.First.At >= p.Second.At));
    }

    [Fact]
    public async Task Kassieren_bar_und_Karte_und_Beleg_Storno_stehen_als_Zahlungen_im_Verlauf_und_im_Termin()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var register = await CreateRegisterAsync(spa);
        var cash = await spa.BookOkAsync(TestSpa.At(10), Ct, spa.Anna.Id);
        await spa.SetStatusAsync(cash.Id, AppointmentStatus.CheckedIn, Ct);
        var cashBill = await spa.PostAsync<BillDto>("/api/pos/bills", new BillInput(register.Id, null, [cash.Id]), Ct);
        var cashReceipt = await CheckoutAsync(spa, cashBill.Id, new PaymentInput(PaymentMethod.Cash, 100m, null, null));
        var card = await spa.BookOkAsync(TestSpa.At(12), Ct, spa.Ben.Id);
        await spa.SetStatusAsync(card.Id, AppointmentStatus.CheckedIn, Ct);
        var cardBill = await spa.PostAsync<BillDto>("/api/pos/bills", new BillInput(register.Id, null, [card.Id]), Ct);
        var cardReceipt = await CheckoutAsync(spa, cardBill.Id, new PaymentInput(PaymentMethod.Card, 79m, null, "Terminal 4711"));
        var voided = await spa.PostAsync<ReceiptDto>($"/api/pos/receipts/{cardReceipt.Id}/void", new VoidRequest("Falscher Gast"), Ct);

        var page = await spa.GetAsync<ActivityPage>(Path(spa, "?kinds=Paid&kinds=ReceiptVoided"), Ct);

        Assert.Equal(
        [
            (ActivityKind.ReceiptVoided, $"Beleg {register.Code} Nr. {cardReceipt.Number} storniert von Test Person: Falscher Gast"),
            (ActivityKind.Paid, $"Bezahlt Karte: 79,00 € von Test Person (Beleg {register.Code} Nr. {cardReceipt.Number})"),
            (ActivityKind.Paid, $"Bezahlt bar: 79,00 € von Test Person (Beleg {register.Code} Nr. {cashReceipt.Number})"),
        ], page.Items.Select(i => (i.Kind, i.Text)));
        var paid = page.Items.Single(i => i.Text.Contains("bar", StringComparison.Ordinal));
        Assert.Equal((cash.Id, spa.Customer.Id, "Test Person", ActivityChannel.Reception), (paid.AppointmentId, paid.CustomerId, paid.Actor, paid.Channel));
        Assert.Equal(card.Id, page.Items[0].AppointmentId);
        Assert.Equal((voided.Id, cardReceipt.Id), (page.Items[0].ReceiptId, page.Items[1].ReceiptId));
        Assert.All(page.Items, i => Assert.True(i.Seq < 0));
        Assert.Equal(voided.Id, (await spa.GetAsync<ReceiptDto>($"/api/pos/receipts/{cardReceipt.Id}", Ct)).VoidedByReceiptId);

        // Dieselben Sätze im Verlauf des Termins, zeitlich einsortiert (neueste oben).
        var history = await spa.GetAsync<List<HistoryEntry>>($"/api/appointments/{card.Id}/history", Ct);
        Assert.Equal(
        [
            $"Beleg {register.Code} Nr. {cardReceipt.Number} storniert von Test Person: Falscher Gast",
            $"Bezahlt Karte: 79,00 € von Test Person (Beleg {register.Code} Nr. {cardReceipt.Number})",
            "Eingecheckt automatisch",
            "Angelegt von Test Person",
        ], history.Select(e => e.Text));
        Assert.True(history.Zip(history.Skip(1)).All(p => p.First.At >= p.Second.At));
    }

    [Fact]
    public async Task Online_bezahlte_Anzahlung_steht_als_Anzahlung_ohne_Person_mit_Kanal_Online()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var register = await CreateRegisterAsync(spa);
        _ = await spa.PutAsync<LocationOnlineRules>($"/api/locations/{spa.Location.Id}/online-rules",
            new LocationOnlineRules(true, 60, 365, 24, 15, null, false, 0, 0, false, 30), Ct);
        using var online = spa.OnlineClient();
        var booked = await TestSpa.ReadAsync<OnlineBookingConfirmation>(await online.PostAsJsonAsync("/api/online/bookings",
            new OnlineBookingRequest(spa.Location.Id, spa.MassageTreatment.Id, TestSpa.At(10), new OnlineContact("Ida", "Anzahl", $"ida-{Guid.NewGuid():N}@example.at", null),
                null, true, "de", true), TestSpa.Json, Ct), Ct);
        var paid = await online.PostAsJsonAsync($"/api/online/bookings/{booked.ManageToken}/deposit/test", new TestPaymentResult(true), TestSpa.Json, Ct);
        Assert.Equal(HttpStatusCode.OK, paid.StatusCode);
        var deposit = await spa.GetAsync<DepositStaffDto>($"/api/appointments/{booked.Appointment.Id}/deposit", Ct);
        for (var attempt = 0; attempt < 50 && deposit.Status != OnlineDepositStatus.Receipted; attempt++)
        {
            await Task.Delay(200, Ct);
            deposit = await spa.GetAsync<DepositStaffDto>($"/api/appointments/{booked.Appointment.Id}/deposit", Ct);
        }

        var page = await spa.GetAsync<ActivityPage>(Path(spa, "?kinds=Deposit"), Ct);

        var entry = Assert.Single(page.Items);
        var receipt = await spa.GetAsync<ReceiptDto>($"/api/pos/receipts/{deposit.ReceiptId}", Ct);
        Assert.Equal($"Anzahlung 23,70 € Karte (Beleg {register.Code} Nr. {receipt.Number})", entry.Text);
        Assert.Equal((booked.Appointment.Id, (string?)null, ActivityChannel.Online), (entry.AppointmentId, entry.Actor, entry.Channel));
        var history = await spa.GetAsync<List<HistoryEntry>>($"/api/appointments/{booked.Appointment.Id}/history", Ct);
        Assert.Contains(history.Select(e => e.Text), t => t.StartsWith("Anzahlung 23,70 € Karte", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Kundin_anlegen_und_aendern_Preis_Dienst_und_Rolle_stehen_mit_ihrer_Art_und_den_Werten_im_Verlauf()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var (lena, lenaId) = await StaffAsync(spa, "Lena", StaffRole.Reception);
        using var client = lena;
        var guest = await spa.PostAsync<CustomerDto>("/api/customers", new CustomerInput("Lea", $"Neu{Guid.NewGuid():N}", null, "0664 111", null, null), Ct);
        _ = await spa.PutAsync<CustomerDto>($"/api/customers/{guest.Id}", new CustomerInput(guest.FirstName, guest.LastName, null, "0664 222", null, null), Ct);
        _ = await spa.PutAsync<TreatmentDto>($"/api/treatments/{spa.MassageTreatment.Id}",
            new TreatmentInput("Massage", 50, 5, 10, 89m, [spa.Massage.Id], [spa.RoomA.Id, spa.RoomB.Id], []), Ct);
        _ = await spa.PutAsync<RosterDayResult>($"/api/locations/{spa.Location.Id}/roster/{spa.Ben.Id}/{TestSpa.Monday:yyyy-MM-dd}",
            new RosterDayInput([new RosterEntry(RosterKind.Vacation, null, null, null)], null), Ct);
        var role = await spa.Client.PutAsJsonAsync($"/api/staff/{lenaId}", new StaffAccessInput(StaffRole.Manager, false, [spa.Location.Id], null, true), TestSpa.Json, Ct);
        Assert.Equal(HttpStatusCode.OK, role.StatusCode);

        var page = await spa.GetAsync<ActivityPage>(Path(spa), Ct);

        var byKind = page.Items.ToLookup(i => i.Kind);
        Assert.Equal("Geändert von Test Person: Rolle Rezeption → Standortleitung", byKind[ActivityKind.AccessChanged].First().Text);
        Assert.Equal(new ActivityValue("Rolle", "Rezeption", "Standortleitung"), Assert.Single(byKind[ActivityKind.AccessChanged].First().Values));
        Assert.Equal($"Urlaub am {TestSpa.Monday:d.M.yyyy} eingetragen von Test Person", byKind[ActivityKind.RosterChanged].First().Text);
        Assert.Equal(spa.Ben.Id, byKind[ActivityKind.RosterChanged].First().EmployeeId);
        var price = byKind[ActivityKind.TreatmentChanged].First();
        Assert.Equal(("Geändert von Test Person: Preis 79,00 € → 89,00 €", spa.MassageTreatment.Id), (price.Text, price.TreatmentId));
        Assert.Equal(new ActivityValue("Preis", "79,00 €", "89,00 €"), Assert.Single(price.Values));
        var changed = byKind[ActivityKind.CustomerChanged].Single(i => i.CustomerId == guest.Id);
        Assert.Equal(("Geändert von Test Person: Telefon +43 664 111 → +43 664 222", $"Lea {guest.LastName}"), (changed.Text, changed.CustomerName));
        Assert.Equal("Angelegt von Test Person", byKind[ActivityKind.CustomerCreated].Single(i => i.CustomerId == guest.Id).Text);
    }

    [Fact]
    public async Task Filter_nach_Zeitraum_Art_Person_Kanal_und_Gast_schraenken_die_Antwort_ein()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var (lena, lenaId) = await StaffAsync(spa, "Lena", StaffRole.Reception);
        using var client = lena;
        var other = await spa.PostAsync<CustomerDto>("/api/customers", new CustomerInput("Max", $"Filter{Guid.NewGuid():N}", null, null, null, null), Ct);
        var byOwner = await spa.BookOkAsync(TestSpa.At(10), Ct, spa.Anna.Id);
        var byLena = await TestSpa.ReadAsync<AppointmentDto>(await lena.PostAsJsonAsync("/api/appointments",
            new AppointmentRequest(spa.Location.Id, spa.MassageTreatment.Id, other.Id, TestSpa.At(12), spa.Ben.Id, null, null, null), TestSpa.Json, Ct), Ct);
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        using (ChangeActor.Use(ChangeChannel.Online))
        {
            scope.ServiceProvider.GetRequiredService<Tenancy.TenantContext>().Use(spa.TenantId);
            var db = scope.ServiceProvider.GetRequiredService<Data.AispaDbContext>();
            (await db.Appointments.FindAsync([byOwner.Id], Ct))!.ConfirmedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(Ct);
        }

        var today = ViennaToday;
        Assert.NotEmpty((await spa.GetAsync<ActivityPage>(Path(spa, $"?from={today:yyyy-MM-dd}&until={today:yyyy-MM-dd}"), Ct)).Items);
        Assert.Empty((await spa.GetAsync<ActivityPage>(Path(spa, $"?from={today.AddDays(1):yyyy-MM-dd}"), Ct)).Items);
        Assert.Empty((await spa.GetAsync<ActivityPage>(Path(spa, $"?until={today.AddDays(-1):yyyy-MM-dd}"), Ct)).Items);

        var booked = await spa.GetAsync<ActivityPage>(Path(spa, "?kinds=Booked"), Ct);
        Assert.Equal([byLena.Id, byOwner.Id], booked.Items.Select(i => i.AppointmentId));

        var lenas = await spa.GetAsync<ActivityPage>(Path(spa, $"?actor={lenaId}"), Ct);
        Assert.Equal([ActivityKind.Booked], lenas.Items.Select(i => i.Kind));
        Assert.All(lenas.Items, i => Assert.Equal("Lena", i.Actor));
        // Die Personen für den Filter kommen mit, unabhängig vom gewählten Filter „Person“; auch die Rezeption bekommt sie.
        Assert.Equal(["Lena", "Test Person"], lenas.Actors!.Select(a => a.Name));
        Assert.Equal(lenaId, lenas.Actors![0].Id);
        var asReception = (await lena.GetFromJsonAsync<ActivityPage>(Path(spa), TestSpa.Json, Ct))!;
        Assert.Equal(["Lena", "Test Person"], asReception.Actors!.Select(a => a.Name));

        var online = await spa.GetAsync<ActivityPage>(Path(spa, "?channel=Online"), Ct);
        Assert.Equal(["Vom Gast bestätigt (online)"], online.Items.Select(i => i.Text));
        Assert.Null(online.Items[0].Actor);

        var guest = await spa.GetAsync<ActivityPage>(Path(spa, $"?customerId={other.Id}"), Ct);
        Assert.Equal([ActivityKind.Booked, ActivityKind.CustomerCreated], guest.Items.Select(i => i.Kind));
    }

    [Fact]
    public async Task Suche_trifft_Gast_Behandlung_Person_Storno_Grund_und_Belegnummer_ohne_Gross_und_Kleinschreibung()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var register = await CreateRegisterAsync(spa);
        var guest = await spa.PostAsync<CustomerDto>("/api/customers", new CustomerInput("Zita", "Suchwort", null, null, null, null), Ct);
        var appointment = await spa.BookOkAsync(TestSpa.At(10), Ct, spa.Anna.Id, customer: guest.Id);
        await spa.SetStatusAsync(appointment.Id, AppointmentStatus.CheckedIn, Ct);
        var bill = await spa.PostAsync<BillDto>("/api/pos/bills", new BillInput(register.Id, null, [appointment.Id]), Ct);
        var receipt = await CheckoutAsync(spa, bill.Id, new PaymentInput(PaymentMethod.Cash, 79m, null, null));
        var cancelled = await spa.BookOkAsync(TestSpa.At(14), Ct, spa.Ben.Id);
        _ = await spa.PostAsync<AppointmentDto>($"/api/appointments/{cancelled.Id}/cancel", new CancelAppointmentRequest("Schneechaos"), Ct);

        var byGuest = await spa.GetAsync<ActivityPage>(Path(spa, "?search=suchWORT"), Ct);
        Assert.NotEmpty(byGuest.Items);
        Assert.All(byGuest.Items, i => Assert.Equal(guest.Id, i.CustomerId));
        Assert.Contains(byGuest.Items, i => i.Kind == ActivityKind.Paid);

        var byTreatment = await spa.GetAsync<ActivityPage>(Path(spa, "?search=massage&kinds=Booked"), Ct);
        Assert.Equal(2, byTreatment.Items.Count);

        var byReason = await spa.GetAsync<ActivityPage>(Path(spa, "?search=schnee"), Ct);
        Assert.Equal([cancelled.Id], byReason.Items.Select(i => i.AppointmentId));

        var byPerson = await spa.GetAsync<ActivityPage>(Path(spa, "?search=test%20person&kinds=Cancelled"), Ct);
        Assert.Equal([ActivityKind.Cancelled], byPerson.Items.Select(i => i.Kind));

        var byNumber = await spa.GetAsync<ActivityPage>(Path(spa, $"?search=Nr.%20{receipt.Number}"), Ct);
        Assert.Equal([ActivityKind.Paid], byNumber.Items.Select(i => i.Kind));
        Assert.Empty((await spa.GetAsync<ActivityPage>(Path(spa, "?search=gibtesnicht"), Ct)).Items);
    }

    [Fact]
    public async Task Seiten_mit_take_2_ueber_drei_Seiten_haben_weder_Luecke_noch_Doppelte()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var register = await CreateRegisterAsync(spa);
        foreach (var hour in new[] { 10, 12, 14 })
        {
            var appointment = await spa.BookOkAsync(TestSpa.At(hour), Ct, spa.Anna.Id);
            await spa.SetStatusAsync(appointment.Id, AppointmentStatus.CheckedIn, Ct);
            var bill = await spa.PostAsync<BillDto>("/api/pos/bills", new BillInput(register.Id, null, [appointment.Id]), Ct);
            _ = await CheckoutAsync(spa, bill.Id, new PaymentInput(PaymentMethod.Cash, 79m, null, null));
        }

        var all = (await spa.GetAsync<ActivityPage>(Path(spa), Ct)).Items;
        Assert.True(all.Count >= 6, $"{all.Count} Einträge");
        var paged = new List<ActivityEntry>();
        long? before = null;
        var pages = 0;
        do
        {
            var page = await spa.GetAsync<ActivityPage>(Path(spa, $"?take=2{(before is { } b ? $"&before={b}" : "")}"), Ct);
            Assert.True(page.Items.Count <= 2);
            paged.AddRange(page.Items);
            before = page.NextBefore;
            pages++;
        }
        while (before is not null && pages < 20);

        Assert.Equal(all.Select(i => i.Seq), paged.Select(i => i.Seq));
        Assert.Equal(paged.Count, paged.Select(i => i.Seq).Distinct().Count());
        Assert.True(pages >= 3);
    }

    [Fact]
    public async Task Rezeption_sieht_keinen_Dienstplan_bekommt_bei_gemischter_Anfrage_200_und_bei_nur_verbotenen_Arten_403()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        _ = await spa.PutAsync<RosterDayResult>($"/api/locations/{spa.Location.Id}/roster/{spa.Ben.Id}/{TestSpa.Monday:yyyy-MM-dd}",
            new RosterDayInput([new RosterEntry(RosterKind.Vacation, null, null, null)], null), Ct);
        _ = await spa.BookOkAsync(TestSpa.At(10), Ct, spa.Anna.Id);
        var (lena, _) = await StaffAsync(spa, "Lena", StaffRole.Reception);
        using var client = lena;

        Assert.Contains((await spa.GetAsync<ActivityPage>(Path(spa), Ct)).Items, i => i.Kind == ActivityKind.RosterChanged);
        var asReception = (await lena.GetFromJsonAsync<ActivityPage>(Path(spa), TestSpa.Json, Ct))!;
        Assert.DoesNotContain(asReception.Items, i => i.Kind is ActivityKind.RosterChanged or ActivityKind.AccessChanged or ActivityKind.CompanyChanged);
        Assert.Contains(asReception.Items, i => i.Kind == ActivityKind.Booked);

        var mixed = (await lena.GetFromJsonAsync<ActivityPage>(Path(spa, "?kinds=Booked&kinds=RosterChanged"), TestSpa.Json, Ct))!;
        Assert.Equal([ActivityKind.Booked], mixed.Items.Select(i => i.Kind).Distinct());

        var forbidden = await lena.GetAsync(Path(spa, "?kinds=RosterChanged&kinds=AccessChanged"), Ct);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        Assert.Contains("nur Inhaber und Manager", await forbidden.Content.ReadAsStringAsync(Ct), StringComparison.Ordinal);

        var (tom, _) = await StaffAsync(spa, "Tom", StaffRole.Therapist);
        using var therapist = tom;
        Assert.Equal(HttpStatusCode.Forbidden, (await tom.GetAsync(Path(spa, "?kinds=Paid"), Ct)).StatusCode);
    }

    [Fact]
    public async Task Fremder_Standort_zeigt_keine_Termine_und_ein_fremder_Mandant_findet_den_Standort_nicht()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        _ = await spa.BookOkAsync(TestSpa.At(10), Ct, spa.Anna.Id);
        var graz = await spa.CreateLocationAsync("Graz", [DayOfWeek.Monday], Ct);

        var there = await spa.GetAsync<ActivityPage>($"/api/locations/{graz.Id}/history", Ct);
        Assert.DoesNotContain(there.Items, i => i.Kind is ActivityKind.Booked or ActivityKind.RosterChanged);
        // Kunden sind mandantenweit und erscheinen an jedem Standort.
        Assert.Contains(there.Items, i => i.Kind == ActivityKind.CustomerCreated && i.CustomerId == spa.Customer.Id);

        var other = await TestSpa.CreateAsync(fixture, Ct);
        Assert.Equal(HttpStatusCode.NotFound, (await other.Client.GetAsync(Path(spa), Ct)).StatusCode);
    }

    [Fact]
    public async Task Gesundheitsdaten_stehen_nur_als_geaendert_von_ohne_Werte()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var (lena, lenaId) = await StaffAsync(spa, "Lena", StaffRole.Reception);
        using var client = lena;
        await using (var db = await OpenAsync())
        {
            await ExecuteAsync(db, $"SELECT set_config('{ChangeHistory.UserSetting}', '{lenaId}', false), set_config('{ChangeHistory.ChannelSetting}', 'Team', false)");
            await ExecuteAsync(db, $"""
                INSERT INTO health."AnamnesisRecords" ("Id", "CustomerId", "FormVersion", "Channel", "SubmittedAt", "ConsentGivenAt", "Payload")
                VALUES ('{Guid.NewGuid()}', '{spa.Customer.Id}', 'v1', 'Tablet', now(), now(), '\x01'::bytea)
                """);
        }

        var page = await spa.GetAsync<ActivityPage>(Path(spa, "?kinds=CustomerChanged"), Ct);

        var entry = Assert.Single(page.Items, i => i.Text.StartsWith("Anamnese", StringComparison.Ordinal));
        Assert.Equal(("Anamnese angelegt von Lena", spa.Customer.Id), (entry.Text, entry.CustomerId));
        Assert.Empty(entry.Values);
    }

    [Fact]
    public async Task Kurzfristig_ist_eine_Minute_vor_der_Frist_wahr_und_eine_Minute_danach_falsch()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var late = await spa.BookOkAsync(TestSpa.At(10), Ct, spa.Anna.Id);
        var early = await spa.BookOkAsync(TestSpa.At(14), Ct, spa.Ben.Id);
        _ = await spa.PostAsync<AppointmentDto>($"/api/appointments/{late.Id}/cancel", new CancelAppointmentRequest("Krank"), Ct);
        _ = await spa.PostAsync<AppointmentDto>($"/api/appointments/{early.Id}/cancel", new CancelAppointmentRequest("Verhindert"), Ct);
        await using (var db = await OpenAsync())
        {
            // Die Stornofrist des Standorts ist die Vorgabe (24 Stunden); die Absage wird auf den Zeitpunkt knapp davor bzw. danach gesetzt.
            await ExecuteAsync(db, CancelledAt(late.Id, TestSpa.At(10).AddHours(-24).AddMinutes(1)));
            await ExecuteAsync(db, CancelledAt(early.Id, TestSpa.At(14).AddHours(-24).AddMinutes(-1)));
        }

        var page = await spa.GetAsync<ActivityPage>(Path(spa, "?kinds=Cancelled"), Ct);

        Assert.Equal(true, page.Items.Single(i => i.AppointmentId == late.Id).LateCancellation);
        Assert.Equal(false, page.Items.Single(i => i.AppointmentId == early.Id).LateCancellation);
        Assert.Null((await spa.GetAsync<ActivityPage>(Path(spa, "?kinds=Booked"), Ct)).Items[0].LateCancellation);
    }

    private static string CancelledAt(Guid appointmentId, DateTimeOffset at) =>
        $$"""UPDATE sync.history SET changed_at = '{{at:O}}' WHERE table_name = 'Appointments' AND row_key = '{"Id": "{{appointmentId}}"}'::jsonb AND changes ? 'Status'""";

    private static async Task<CashRegisterDto> CreateRegisterAsync(TestSpa spa) =>
        await spa.PostAsync<CashRegisterDto>("/api/pos/registers", new CashRegisterInput(
            spa.Location.Id, $"V-{Guid.NewGuid():N}"[..14], FiscalCountry.AT, "Test-Spa GmbH\nTeststraße 1, 1010 Wien\nATU12345678",
            "Test-Spa GmbH", "AT61 1904 3002 3457 3201", null), Ct);

    private static Task<ReceiptDto> CheckoutAsync(TestSpa spa, Guid billId, params PaymentInput[] payments) =>
        spa.PostAsync<ReceiptDto>($"/api/pos/bills/{billId}/checkout", new CheckoutRequest(null, payments), Ct);

    /// <summary>Weiteres Konto im Spa mit eigenem Anzeigenamen; liefert den Client und die Benutzer-Id.</summary>
    private async Task<(HttpClient Client, string UserId)> StaffAsync(TestSpa spa, string name, StaffRole role)
    {
        var email = $"{name.ToLowerInvariant()}-{Guid.NewGuid():N}@aispa.test";
        using var client = fixture.CreateClient(spa.Cookie);
        var response = await client.PostAsJsonAsync("/api/auth/dev/register", new DevRegistration(email, name, role, [spa.Location.Id]), TestSpa.Json, Ct);
        response.EnsureSuccessStatusCode();
        await using var db = await OpenAsync();
        await using var command = new NpgsqlCommand($"""SELECT "Id" FROM aispa."AspNetUsers" WHERE "Email" = '{email}'""", db);
        var userId = (string)(await command.ExecuteScalarAsync(Ct))!;
        return (fixture.CreateClient(string.Join("; ", response.Headers.GetValues("Set-Cookie").Select(c => c.Split(';')[0]))), userId);
    }

    private async Task<NpgsqlConnection> OpenAsync()
    {
        var db = new NpgsqlConnection(fixture.ConnectionString);
        await db.OpenAsync(Ct);
        return db;
    }

    private static async Task ExecuteAsync(NpgsqlConnection db, string sql)
    {
        await using var command = new NpgsqlCommand(sql, db);
        await command.ExecuteNonQueryAsync(Ct);
    }
}
