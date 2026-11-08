using System.Net;
using System.Net.Http.Json;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Tests;

/// <summary>
/// Namensregeln (ADR 0064): Rufname im Haus (Kalender, Suche, Kassa-Oberfläche), gültiger Name auf Belegen, Titel und Anrede in der Kundenkarte,
/// Protokoll jeder Änderung am gültigen Namen, Suche und Dublettenabgleich ohne Titel und mit Rufname.
/// </summary>
public sealed class CustomerNameTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static CustomerInput Steffi(CustomerDto c, string firstName = "Stefanie") =>
        new(firstName, c.LastName, c.Email, c.Phone, null, null, Salutation: CustomerSalutation.Female, PreferredName: " Steffi ",
            TitlesBefore: "Mag.  Dr.", TitlesAfter: "MSc MBA", Address: new CustomerAddress("Hauptstraße 1", "1010", "Wien", "at"), Pronouns: " sie/ihr ");

    [Fact]
    public async Task Anrede_Rufname_Titel_und_Anschrift_werden_gespeichert_der_Kalender_zeigt_den_Rufname()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);

        var saved = await spa.PutAsync<CustomerDto>($"/api/customers/{spa.Customer.Id}", Steffi(spa.Customer), Ct);
        var appointment = await spa.BookOkAsync(TestSpa.At(10), Ct);
        var day = await spa.GetAsync<CalendarDay>($"/api/locations/{spa.Location.Id}/calendar?date={TestSpa.Monday:yyyy-MM-dd}", Ct);

        Assert.Equal((CustomerSalutation.Female, "Steffi", "Mag. Dr.", "MSc, MBA"), (saved.Salutation, saved.PreferredName, saved.TitlesBefore, saved.TitlesAfter));
        Assert.Equal(new CustomerAddress("Hauptstraße 1", "1010", "Wien", "AT"), saved.Address);
        Assert.Equal("sie/ihr", saved.Pronouns);
        Assert.Equal($"Steffi {spa.Customer.LastName}", appointment.CustomerName);
        Assert.Equal($"Steffi {spa.Customer.LastName}", day.Appointments.Single().CustomerName);
        Assert.Equal(CustomerSalutation.Female, day.Guests!.Single().Salutation);
    }

    [Fact]
    public async Task Suche_findet_ueber_Rufname_und_ohne_Titel()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await spa.PutAsync<CustomerDto>($"/api/customers/{spa.Customer.Id}", Steffi(spa.Customer), Ct);

        var byPreferred = await spa.GetAsync<List<CustomerDto>>($"/api/customers?search=Steffi%20{spa.Customer.LastName}", Ct);
        var withTitle = await spa.GetAsync<List<CustomerDto>>($"/api/customers?search=Dr.%20{spa.Customer.LastName}", Ct);

        Assert.Contains(byPreferred, c => c.Id == spa.Customer.Id);
        Assert.Contains(withTitle, c => c.Id == spa.Customer.Id);
    }

    [Fact]
    public async Task Dublettenabgleich_kennt_den_Rufname_und_ignoriert_Titel()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await spa.PutAsync<CustomerDto>($"/api/customers/{spa.Customer.Id}", Steffi(spa.Customer), Ct);

        var byPreferred = await spa.PostAsync<List<CustomerCandidateDto>>("/api/customers/duplicates",
            new DuplicateCheckInput("Steffi", spa.Customer.LastName, null, null), Ct);
        var withTitle = await spa.PostAsync<List<CustomerCandidateDto>>("/api/customers/duplicates",
            new DuplicateCheckInput("Dr. Stefanie", spa.Customer.LastName, null, null), Ct);

        var candidate = Assert.Single(byPreferred, c => c.CustomerId == spa.Customer.Id);
        Assert.Equal($"Steffi {spa.Customer.LastName}", candidate.Name);
        Assert.Contains("gleicher Name", Assert.Single(withTitle, c => c.CustomerId == spa.Customer.Id).Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Aenderungen_am_gueltigen_Namen_werden_protokolliert_der_Rufname_nicht()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await spa.PutAsync<CustomerDto>($"/api/customers/{spa.Customer.Id}", Steffi(spa.Customer), Ct);
        await spa.PutAsync<CustomerDto>($"/api/customers/{spa.Customer.Id}", Steffi(spa.Customer) with { PreferredName = "Fanny" }, Ct);

        var insights = await spa.GetAsync<CustomerInsights>($"/api/customers/{spa.Customer.Id}/insights", Ct);

        var change = Assert.Single(insights.NameChanges!);
        Assert.Equal(($"Eva {spa.Customer.LastName}", $"Stefanie {spa.Customer.LastName}"), (change.OldName, change.NewName));
        Assert.NotNull(change.ChangedBy);
    }

    [Fact]
    public async Task Kassa_zeigt_den_Rufname_und_kennt_den_gueltigen_Namen_fuer_Belege()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await spa.PutAsync<CustomerDto>($"/api/customers/{spa.Customer.Id}", Steffi(spa.Customer), Ct);
        var register = await spa.PostAsync<CashRegisterDto>("/api/pos/registers",
            new CashRegisterInput(spa.Location.Id, $"T-{Guid.NewGuid():N}"[..14], FiscalCountry.AT, "Test-Spa GmbH\nTeststraße 1, 1010 Wien\nATU12345678",
                "Test-Spa GmbH", "AT61 1904 3002 3457 3201", null), Ct);
        var visit = await spa.BookOkAsync(TestSpa.At(10), Ct);

        await spa.SetStatusAsync(visit.Id, AppointmentStatus.CheckedIn, Ct);
        var bill = await spa.PostAsync<BillDto>("/api/pos/bills", new BillInput(register.Id, null, [visit.Id]), Ct);

        Assert.Equal($"Steffi {spa.Customer.LastName}", bill.CustomerName);
        Assert.Equal($"Stefanie {spa.Customer.LastName}", bill.CustomerLegalName);
    }

    [Fact]
    public async Task Ungueltige_Angaben_werden_abgewiesen()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);

        var response = await spa.Client.PutAsJsonAsync($"/api/customers/{spa.Customer.Id}",
            Steffi(spa.Customer) with { Address = new CustomerAddress(null, null, null, "Österreich") }, TestSpa.Json, Ct);
        var pronouns = await spa.Client.PutAsJsonAsync($"/api/customers/{spa.Customer.Id}",
            Steffi(spa.Customer) with { Pronouns = new string('x', 41) }, TestSpa.Json, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, pronouns.StatusCode);
    }
}
