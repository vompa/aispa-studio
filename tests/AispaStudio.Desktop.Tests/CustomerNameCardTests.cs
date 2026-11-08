using AispaStudio.Contracts;
using AispaStudio.Desktop.ViewModels;

namespace AispaStudio.Desktop.Tests;

/// <summary>
/// Namensregeln in der Windows-App (ADR 0064): Kundenkarte mit Rufname groß und Adresszeile klein, Vorschau der Anrede je Land der Standorte,
/// Titel aus der Auswahl, Kassa mit Rufname groß und gültigem Namen klein.
/// </summary>
public sealed class CustomerNameCardTests
{
    private static readonly CustomerDto Stefanie = SpaSample.Customers[0] with
    {
        FirstName = "Stefanie",
        LastName = "Huber",
        Salutation = CustomerSalutation.Female,
        PreferredName = "Steffi",
        Pronouns = "sie/ihr",
        TitlesBefore = "Mag.",
        TitlesAfter = "MSc",
        Address = new CustomerAddress("Hauptstraße 1", "1010", "Wien", "AT"),
    };

    [Fact]
    public async Task Kundenkarte_zeigt_Rufname_gross_Adresszeile_klein_und_die_Anrede_je_Land()
    {
        var berlin = SpaSample.Vienna with { Id = Guid.NewGuid(), Name = "Berlin", TimeZone = "Europe/Berlin" };
        var stub = new StubApi()
            .On("GET /api/customers?search=", new List<CustomerDto> { Stefanie })
            .On("GET /api/locations", new List<LocationDto> { SpaSample.Vienna, berlin })
            .On($"GET /api/customers/{Stefanie.Id}/appointments", new List<AppointmentDto>());
        var customers = new CustomersViewModel(stub.Client(), TimeProvider.System);
        await customers.InitializeAsync();

        customers.Selected = customers.Results[0];

        Assert.Equal("Steffi Huber", customers.SelectedName);
        Assert.Equal("♀", customers.SelectedSymbol);
        Assert.Equal("(sie/ihr)", customers.SelectedPronouns);
        Assert.Equal("SH", customers.SelectedInitials);
        Assert.Equal("Frau Mag. Stefanie Huber, MSc", customers.SelectedAddressLine);
        Assert.Equal("Hauptstraße 1, 1010 Wien, AT", customers.SelectedPostalAddress);
        Assert.Equal("Anrede in AT: Guten Tag Frau Mag. Huber\nAnrede in DE: Guten Tag Frau Huber", customers.SelectedGreeting);
    }

    [Fact]
    public async Task Bearbeiten_mit_Vorschau_Titel_aus_der_Auswahl_und_Speichern()
    {
        var stub = new StubApi()
            .On("GET /api/customers?search=", new List<CustomerDto> { Stefanie })
            .On($"GET /api/customers/{Stefanie.Id}/appointments", new List<AppointmentDto>())
            .On($"PUT /api/customers/{Stefanie.Id}", Stefanie with { TitlesAfter = "MSc, MBA", TitleInGreeting = false });
        var customers = new CustomersViewModel(stub.Client(), TimeProvider.System);
        await customers.InitializeAsync();
        customers.Selected = customers.Results[0];

        customers.EditContactCommand.Execute(null);
        var form = customers.Contact!;
        Assert.Equal("Steffi", form.PreferredName);
        Assert.Equal("sie/ihr", form.Pronouns);
        form.Pronouns = "they/them";
        Assert.Equal("Frau Mag. Stefanie Huber, MSc", form.AddressPreview);
        Assert.Equal("Anrede: Guten Tag Frau Mag. Huber", form.GreetingPreview);

        form.AddTitleAfterCommand.Execute("MBA");
        form.AddTitleAfterCommand.Execute("MBA");
        form.TitleInGreeting = GreetingTitleChoice.All.Single(g => g.Value == false);
        Assert.Equal("Frau Mag. Stefanie Huber, MSc, MBA", form.AddressPreview);
        Assert.Equal("Anrede: Guten Tag Frau Huber", form.GreetingPreview);

        await form.SaveCommand.ExecuteAsync(null);

        var body = stub.Calls.Last(c => c.Route == $"PUT /api/customers/{Stefanie.Id}").Body!;
        Assert.Contains("\"preferredName\":\"Steffi\"", body, StringComparison.Ordinal);
        Assert.Contains("\"pronouns\":\"they/them\"", body, StringComparison.Ordinal);
        Assert.Contains("\"titlesAfter\":\"MSc, MBA\"", body, StringComparison.Ordinal);
        Assert.Contains("\"titleInGreeting\":false", body, StringComparison.Ordinal);
        Assert.Contains("\"postalCode\":\"1010\"", body, StringComparison.Ordinal);
        Assert.False(customers.IsEditingContact);
    }

    [Fact]
    public async Task Neu_anlegen_mit_Anrede_Titel_und_Rufname()
    {
        var stub = new StubApi()
            .On("GET /api/customers?search=", new List<CustomerDto>())
            .On("POST /api/customers", Stefanie);
        var customers = new CustomersViewModel(stub.Client(), TimeProvider.System);
        await customers.InitializeAsync();
        customers.StartNewCommand.Execute(null);
        customers.NewFirstName = "Stefanie";
        customers.NewLastName = "Huber";
        customers.NewSalutation = SalutationChoice.All.Single(s => s.Value == CustomerSalutation.Female);
        customers.NewTitlesBefore = "Mag.";
        customers.NewPreferredName = "Steffi";

        await customers.CreateCommand.ExecuteAsync(null);

        var body = stub.Calls.Last(c => c.Route == "POST /api/customers").Body!;
        Assert.Contains("\"salutation\":\"Female\"", body, StringComparison.Ordinal);
        Assert.Contains("\"titlesBefore\":\"Mag.\"", body, StringComparison.Ordinal);
        Assert.Contains("\"preferredName\":\"Steffi\"", body, StringComparison.Ordinal);
        Assert.Equal("", customers.NewPreferredName);
    }

    [Fact]
    public void Kassa_zeigt_den_gueltigen_Namen_klein_nur_wenn_er_vom_Rufname_abweicht()
    {
        Assert.Null(PosLegalText(SpaSample.Bill() with { CustomerName = "Eva Muster", CustomerLegalName = "Eva Muster" }));
        Assert.Equal("Stefanie Huber", PosLegalText(SpaSample.Bill() with { CustomerName = "Steffi Huber", CustomerLegalName = "Stefanie Huber" }));
    }

    [Fact]
    public void Posteingang_zeigt_Anrede_und_Titel_aus_der_Mail_nur_als_Vorschlag()
    {
        static InboxDetailViewModel Detail(InboundMailDto mail) =>
            new(new StubApi().Client(), mail, TimeZoneInfo.Utc, TimeSpan.FromHours(1), () => Task.CompletedTask, () => { });

        Assert.Null(Detail(SpaSample.InboxMail()).SalutationSuggestionText);
        Assert.Equal("KI-Vorschlag aus der Mail: Frau · Dr. (bitte in der Kundenkarte prüfen und eintragen)",
            Detail(SpaSample.InboxMail() with { SuggestedSalutation = CustomerSalutation.Female, SuggestedTitlesBefore = "Dr." }).SalutationSuggestionText);
    }

    /// <summary>Die Rechnung kommt sonst über mehrere Aufrufe der Kassa; für die Anzeige genügt es, sie direkt zu setzen.</summary>
    private static string? PosLegalText(BillDto bill)
    {
        var pos = new PosViewModel(new StubApi().Client(), TimeProvider.System);
        typeof(PosViewModel).GetProperty(nameof(PosViewModel.Bill))!.SetValue(pos, bill);
        return pos.CustomerLegalText;
    }
}
