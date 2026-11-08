using System.Net.Http.Json;
using AispaStudio.Api.Pos;
using AispaStudio.Contracts;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace AispaStudio.Api.Tests;

/// <summary>Liste „Heute abzurechnen“ der Kassa (Slice 137, Ä344, Ä355): Einteilung rein geprüft, die Abfrage über HTTP gegen PostgreSQL.</summary>
public sealed class PosDayListTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly DateTimeOffset Now = new(2027, 3, 1, 10, 0, 0, TimeSpan.Zero);

    private static PosDayCandidate Candidate(string guest, int startHour, int endHour, bool completed = false, PosDayBilling billing = PosDayBilling.None,
        int endMinute = 0) =>
        new(new PosDayEntryDto(Now.AddHours(startHour - 10), guest, "Massage", "Anna", "Raum A", 79m, null, null, null),
            Guid.CreateVersion7(), Now.AddHours(endHour - 10).AddMinutes(endMinute), completed, billing);

    private static string[] Guests(IReadOnlyList<PosDayEntryDto> entries) => [.. entries.Select(e => e.CustomerName)];

    [Fact]
    public void Einteilung_legt_zwei_fertige_einen_kommenden_und_einen_bezahlten_Termin_in_Wartet_Spaeter_Erledigt()
    {
        var list = PosDayList.Classify(
        [
            Candidate("Kommt noch", 11, 12),
            Candidate("Abgeschlossen", 9, 11, completed: true),
            Candidate("Ende vorbei", 8, 9),
            Candidate("Bezahlt", 7, 8, completed: true, billing: PosDayBilling.Settled),
            Candidate("Geparkt", 8, 9, billing: PosDayBilling.Parked),
        ], Now);

        Assert.Equal(["Ende vorbei", "Abgeschlossen"], Guests(list.Waiting));
        Assert.Equal(["Kommt noch"], Guests(list.Later));
        Assert.Equal(["Bezahlt"], Guests(list.Done));
    }

    [Fact]
    public void Fertig_seit_nennt_das_Ende_und_fehlt_bei_frueher_abgeschlossenem_Termin()
    {
        var list = PosDayList.Classify([Candidate("Ende vorbei", 8, 9, endMinute: 55), Candidate("Früher fertig", 9, 11, completed: true)], Now);

        Assert.Equal(new DateTimeOffset(2027, 3, 1, 9, 55, 0, TimeSpan.Zero), list.Waiting[0].FinishedAt);
        Assert.Null(list.Waiting[1].FinishedAt);
    }

    [Fact]
    public void Wartende_stehen_nach_Ende_sortiert_bei_Gleichstand_nach_Name()
    {
        var list = PosDayList.Classify([Candidate("Zita", 8, 9), Candidate("Berta", 7, 9), Candidate("Anton", 6, 8)], Now);

        Assert.Equal(["Anton", "Berta", "Zita"], Guests(list.Waiting));
    }

    private async Task<(TestSpa Spa, CashRegisterDto Register)> SpaWithRegisterAsync()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var register = await spa.PostAsync<CashRegisterDto>("/api/pos/registers",
            new CashRegisterInput(spa.Location.Id, $"T-{Guid.NewGuid():N}"[..14], FiscalCountry.AT, "Test-Spa GmbH\nTeststraße 1, 1010 Wien\nATU12345678",
                "Test-Spa GmbH", "AT61 1904 3002 3457 3201", null), Ct);
        return (spa, register);
    }

    /// <summary>
    /// Tagesliste mit fester Uhr um 9 Uhr am Testtag: alle Termine liegen noch vor ihr, egal wann der Test läuft. Angemeldet wird an dieser API, ein Cookie der
    /// Test-API von heute wäre dort schon abgelaufen (wie in <see cref="StaffDutyTests"/>).
    /// </summary>
    private async Task<PosDayListDto> DayListAsync(TestSpa spa)
    {
        await using var host = fixture.Factory.WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddSingleton<TimeProvider>(new FixedTime(TestSpa.At(9)))));
        using var anonymous = host.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        var signIn = await anonymous.PostAsJsonAsync("/api/auth/dev/register",
            new DevRegistration($"test-{Guid.NewGuid():N}@aispa.test", "Test Person", StaffRole.Owner, [spa.Location.Id], null), TestSpa.Json, Ct);
        signIn.EnsureSuccessStatusCode();
        using var client = host.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        client.DefaultRequestHeaders.Add("Cookie", string.Join("; ", signIn.Headers.GetValues("Set-Cookie").Select(c => c.Split(';')[0])));
        return (await client.GetFromJsonAsync<PosDayListDto>($"/api/pos/locations/{spa.Location.Id}/day-list?date={TestSpa.Monday:yyyy-MM-dd}", TestSpa.Json, Ct))!;
    }

    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private static Task<ReceiptDto> PayAsync(TestSpa spa, Guid billId, decimal amount) =>
        spa.PostAsync<ReceiptDto>($"/api/pos/bills/{billId}/checkout", new CheckoutRequest(null, [new PaymentInput(PaymentMethod.Cash, amount, null, null)]), Ct);

    [Fact]
    public async Task Tagesliste_zeigt_bezahlte_Termine_unter_Erledigt_und_geparkte_in_keiner_Gruppe()
    {
        var (spa, register) = await SpaWithRegisterAsync();
        var paid = await spa.BookOkAsync(TestSpa.At(10), Ct, spa.Anna.Id);
        var parked = await spa.BookOkAsync(TestSpa.At(10), Ct, spa.Ben.Id);
        var later = await spa.BookOkAsync(TestSpa.At(14), Ct, spa.Anna.Id);
        await spa.SetStatusAsync(paid.Id, AppointmentStatus.CheckedIn, Ct);
        var paidBill = await spa.PostAsync<BillDto>("/api/pos/bills", new BillInput(register.Id, null, [paid.Id]), Ct);
        await PayAsync(spa, paidBill.Id, paid.Price);
        await spa.SetStatusAsync(parked.Id, AppointmentStatus.CheckedIn, Ct);
        await spa.PostAsync<BillDto>("/api/pos/bills", new BillInput(register.Id, null, [parked.Id]), Ct);

        var list = await DayListAsync(spa);

        Assert.Equal(paid.Id, Assert.Single(list.Done).Appointment!.Id);
        Assert.Equal(later.Id, Assert.Single(list.Later).Appointment!.Id);
        Assert.Empty(list.Waiting);
    }

    [Fact]
    public async Task Termin_mit_bezahlter_Anzahlung_steht_weiter_in_der_Tagesliste()
    {
        var (spa, register) = await SpaWithRegisterAsync();
        var appointment = await spa.BookOkAsync(TestSpa.At(11), Ct);
        var depositBill = await spa.PostAsync<BillDto>("/api/pos/bills", new BillInput(register.Id, appointment.CustomerId, []), Ct);
        await spa.PostAsync<BillDto>($"/api/pos/bills/{depositBill.Id}/lines",
            new BillLineInput(BillLineKind.Deposit, appointment.Id, null, 1, 20m, 0, null, null, null, null, null), Ct);
        await PayAsync(spa, depositBill.Id, 20m);

        var list = await DayListAsync(spa);

        Assert.Equal(appointment.Id, Assert.Single(list.Later).Appointment!.Id);
        Assert.Empty(list.Done);
    }

    [Fact]
    public async Task Tagesliste_nennt_Person_Raum_und_den_Eintrittspreis_des_Bereichs()
    {
        var (spa, _) = await SpaWithRegisterAsync();
        var dayPass = await spa.PostAsync<ArticleDto>("/api/pos/articles",
            new ArticleInput("Tageseintritt Sauna", 32m, TaxCategory.Reduced2, PosModel.Products.Id), Ct);
        var sauna = await spa.PostAsync<RoomDto>($"/api/locations/{spa.Location.Id}/rooms",
            new RoomInput("Sauna", 0, 10, IsArea: true, AdmissionArticleId: dayPass.Id), Ct);
        await spa.PostAsync<AreaVisitDto>("/api/area-visits",
            new AreaVisitRequest(spa.Location.Id, sauna.Id, spa.Customer.Id, TestSpa.At(15), TestSpa.At(18), 2), Ct);
        await spa.BookOkAsync(TestSpa.At(10), Ct, spa.Anna.Id, spa.RoomB.Id);

        var list = await DayListAsync(spa);

        Assert.Collection(list.Later,
            massage => Assert.Equal(("Massage", "Anna", "Raum B", (decimal?)79m), (massage.What, massage.EmployeeName, massage.RoomName, massage.Price)),
            visit => Assert.Equal(("2 × Eintritt Sauna", (string?)null, "Sauna", (decimal?)64m), (visit.What, visit.EmployeeName, visit.RoomName, visit.Price)));
    }
}
