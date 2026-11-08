using System.Net;
using System.Net.Http.Json;
using AispaStudio.Contracts;
using Microsoft.AspNetCore.Http;

namespace AispaStudio.Api.Tests;

/// <summary>
/// Betriebsinformationen (ADR 0074): anlegen, ändern und archivieren mit Prüfregeln, Stufe „Team“ und „Leitung“ je Rolle (der Server filtert), Backoffice liest alles und
/// schreibt nichts, Rezeption und Therapeut:in verwalten nichts, Standortleitung nur Einträge ihrer Standorte.
/// </summary>
public sealed class BusinessContactTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly AnswerKit _kit = new(fixture);

    internal static BusinessContactInput Input(string designation = "Installateur", BusinessContactCategory category = BusinessContactCategory.Craftsman,
        string? company = "Haustechnik Muster GmbH", string? person = "Franz Muster", string? phone = "+43 1 234 56 78", string? email = "office@haustechnik-muster.example",
        string? responsibility = "Wasserschaden, Heizung", bool all = true, Guid[]? locations = null,
        BusinessContactVisibility visibility = BusinessContactVisibility.Team) =>
        new(category, designation, company, person, phone, null, email, null, responsibility, null, null, null, null, null, all, locations ?? [], null, visibility);

    private static Task<BusinessContactDto> CreateAsync(TestSpa spa, BusinessContactInput input) => spa.PostAsync<BusinessContactDto>("/api/business-contacts", input, Ct);

    private static Task<List<BusinessContactDto>> ListAsync(HttpClient client, string query = "") =>
        client.GetFromJsonAsync<List<BusinessContactDto>>($"/api/business-contacts{query}", TestSpa.Json, Ct)!;

    private static async Task<List<string>> FieldsAsync(HttpClient client, BusinessContactInput input)
    {
        var response = await client.PostAsJsonAsync("/api/business-contacts", input, TestSpa.Json, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = (await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>(TestSpa.Json, Ct))!;
        return [.. problem.Errors.Keys.Order(StringComparer.Ordinal)];
    }

    [Fact]
    public async Task Eintrag_anlegen_aendern_und_archivieren_mit_Versicherungsfeldern_nur_bei_Versicherungen()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var insurance = await CreateAsync(spa, Input("Betriebshaftpflicht", BusinessContactCategory.Insurance, "Muster Versicherung AG", "Eva Polizze", "+43 1 111 22 33",
            responsibility: "Haftpflichtschäden", visibility: BusinessContactVisibility.Management) with
        {
            Insurer = "Muster Versicherung AG",
            PolicyNumber = "POL-123456",
            Coverage = "Haftpflicht bis 2 Mio. EUR",
            ClaimsHotline = "+43 800 123 456",
            Notes = "Jährlich zum 1. Jänner fällig.",
        });
        var plumber = await CreateAsync(spa, Input());

        Assert.Equal((BusinessContactCategory.Insurance, "Betriebshaftpflicht", "POL-123456", "+43 800 123 456", BusinessContactVisibility.Management, true, false, true),
            (insurance.Category, insurance.Designation, insurance.PolicyNumber, insurance.ClaimsHotline, insurance.Visibility, insurance.AllLocations, insurance.IsArchived, insurance.CanEdit));
        Assert.Equal(("Haftpflicht bis 2 Mio. EUR", "Jährlich zum 1. Jänner fällig."), (insurance.Coverage, insurance.Notes));
        Assert.Empty(insurance.LocationIds);
        // Geordnet nach Kategorie (Versicherung vor Handwerker), dann Bezeichnung.
        Assert.Equal(["Betriebshaftpflicht", "Installateur"], (await ListAsync(spa.Client)).Select(c => c.Designation));

        // Ändern: Kategorie wechseln lässt die Versicherungsfelder verschwinden, Standort statt „alle“ wählen, Leerzeichen und leere Angaben werden bereinigt.
        var changed = await spa.PutAsync<BusinessContactDto>($"/api/business-contacts/{insurance.Id}",
            Input("  Elektriker  ", BusinessContactCategory.Craftsman, " Strom Muster ", "  ", " 01 / 222 33 44 ", all: false, locations: [spa.Location.Id]), Ct);
        Assert.Equal(("Elektriker", "Strom Muster", null, "01 / 222 33 44", null, null, false), (changed.Designation, changed.Company, changed.ContactPerson, changed.Phone,
            changed.PolicyNumber, changed.Coverage, changed.AllLocations));
        Assert.Equal([spa.Location.Id], changed.LocationIds);
        var back = await spa.PutAsync<BusinessContactDto>($"/api/business-contacts/{insurance.Id}", Input("Elektriker"), Ct);
        Assert.True(back.AllLocations);
        Assert.Empty(back.LocationIds);

        // Archivieren: bleibt erhalten, steht aber nur auf Wunsch in der Liste; zurückholen geht.
        var archived = await spa.PostAsync<BusinessContactDto>($"/api/business-contacts/{plumber.Id}/archive", new { }, Ct);
        Assert.True(archived.IsArchived);
        Assert.Equal(["Elektriker"], (await ListAsync(spa.Client)).Select(c => c.Designation));
        Assert.Equal(["Elektriker", "Installateur"], (await ListAsync(spa.Client, "?includeArchived=true")).Select(c => c.Designation));
        Assert.False((await spa.PostAsync<BusinessContactDto>($"/api/business-contacts/{plumber.Id}/unarchive", new { }, Ct)).IsArchived);
        Assert.Equal(HttpStatusCode.NotFound, (await spa.Client.PutAsJsonAsync($"/api/business-contacts/{Guid.NewGuid()}", Input(), TestSpa.Json, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await spa.Client.PostAsJsonAsync($"/api/business-contacts/{Guid.NewGuid()}/archive", new { }, TestSpa.Json, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await spa.Client.PostAsJsonAsync($"/api/business-contacts/{Guid.NewGuid()}/unarchive", new { }, TestSpa.Json, Ct)).StatusCode);
    }

    [Fact]
    public async Task Pruefregeln_Bezeichnung_Erreichbarkeit_Telefon_Mail_Versicherungsfelder_und_Standorte()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);

        Assert.Equal(["designation"], await FieldsAsync(spa.Client, Input("   ")));
        Assert.Equal(["designation"], await FieldsAsync(spa.Client, Input(new string('x', 81))));
        Assert.Equal(["category"], await FieldsAsync(spa.Client, Input() with { Category = (BusinessContactCategory)99 }));
        Assert.Equal(["visibility"], await FieldsAsync(spa.Client, Input() with { Visibility = (BusinessContactVisibility)99 }));
        // Ohne Firma, Ansprechperson, Telefon, Mobil und Mail fehlt jede Möglichkeit, jemanden zu erreichen.
        Assert.Equal(["company"], await FieldsAsync(spa.Client, Input(company: null, person: " ", phone: null, email: null)));
        Assert.Equal(["phone"], await FieldsAsync(spa.Client, Input(phone: "abc")));
        Assert.Equal(["phone"], await FieldsAsync(spa.Client, Input(phone: "12")));
        Assert.Equal(["mobile"], await FieldsAsync(spa.Client, Input() with { Mobile = "0664 / 12x" }));
        Assert.Equal(["email"], await FieldsAsync(spa.Client, Input(email: "kein-mail")));
        Assert.Equal(["company"], await FieldsAsync(spa.Client, Input(company: new string('f', 121))));
        Assert.Equal(["responsibility"], await FieldsAsync(spa.Client, Input(responsibility: new string('z', 301))));
        Assert.Equal(["notes"], await FieldsAsync(spa.Client, Input() with { Notes = new string('n', 1001) }));
        // Versicherungsfelder nur bei Versicherungen.
        Assert.Equal(["insurer"], await FieldsAsync(spa.Client, Input() with { PolicyNumber = "POL-1" }));
        Assert.Equal(["claimsHotline"], await FieldsAsync(spa.Client, Input("Haftpflicht", BusinessContactCategory.Insurance) with { ClaimsHotline = "hotline" }));
        // Standorte wie bei den Aktionen.
        Assert.Equal(["locationIds"], await FieldsAsync(spa.Client, Input(all: false)));
        Assert.Equal(["locationIds"], await FieldsAsync(spa.Client, Input(all: true, locations: [spa.Location.Id])));
        Assert.Equal(["locationIds"], await FieldsAsync(spa.Client, Input(all: false, locations: [Guid.NewGuid()])));

        // Gültige Randfälle: nur eine Mail oder nur ein Name genügt, Telefon mit Klammern und Schrägstrich.
        Assert.Equal(HttpStatusCode.Created, (await spa.Client.PostAsJsonAsync("/api/business-contacts", Input("Nur Mail", company: null, person: null, phone: null), TestSpa.Json, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await spa.Client.PostAsJsonAsync("/api/business-contacts", Input("Nur Person", company: null, phone: "(01) 234/56-78", email: null), TestSpa.Json, Ct)).StatusCode);
    }

    [Fact]
    public async Task Stufe_Leitung_lesen_Inhaber_Manager_Standortleitung_und_Backoffice_sonst_filtert_der_Server_sie_heraus()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await CreateAsync(spa, Input("Installateur"));
        await CreateAsync(spa, Input("Lohnverrechnung", BusinessContactCategory.Payroll, visibility: BusinessContactVisibility.Management));
        using var reception = await _kit.JoinAsync(spa, StaffRole.Reception);
        using var therapist = await _kit.JoinAsync(spa, StaffRole.Therapist, employee: spa.Anna.Id);
        using var location = await _kit.JoinAsync(spa, StaffRole.Manager);
        using var manager = await _kit.JoinAsync(spa, StaffRole.GeneralManager);
        using var backoffice = await _kit.JoinAsync(spa, StaffRole.Backoffice);
        using var anonymous = fixture.CreateClient();

        foreach (var client in new[] { spa.Client, manager, location, backoffice })
        {
            Assert.Equal(["Installateur", "Lohnverrechnung"], (await ListAsync(client)).Select(c => c.Designation).Order(StringComparer.Ordinal));
        }

        // Rezeption und Therapeut:in lesen das Team-Verzeichnis, aber nicht die Stufe „Leitung“; die Antwort enthält sie gar nicht erst.
        foreach (var client in new[] { reception, therapist })
        {
            var listed = await ListAsync(client);
            Assert.Equal(["Installateur"], listed.Select(c => c.Designation));
            Assert.All(listed, c => Assert.False(c.CanEdit));
            Assert.DoesNotContain("Lohnverrechnung", await client.GetStringAsync("/api/business-contacts?includeArchived=true", Ct), StringComparison.Ordinal);
        }

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/business-contacts", Ct)).StatusCode);
    }

    [Fact]
    public async Task Verwalten_nur_mit_Recht_Standort_verwalten_Backoffice_Rezeption_und_Therapeutin_nicht()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var contact = await CreateAsync(spa, Input());
        using var reception = await _kit.JoinAsync(spa, StaffRole.Reception);
        using var therapist = await _kit.JoinAsync(spa, StaffRole.Therapist, employee: spa.Anna.Id);
        using var backoffice = await _kit.JoinAsync(spa, StaffRole.Backoffice);
        using var manager = await _kit.JoinAsync(spa, StaffRole.GeneralManager);

        foreach (var client in new[] { reception, therapist, backoffice })
        {
            Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/business-contacts", Input("Heimlich"), TestSpa.Json, Ct)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.PutAsJsonAsync($"/api/business-contacts/{contact.Id}", Input("Heimlich"), TestSpa.Json, Ct)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync($"/api/business-contacts/{contact.Id}/archive", new { }, TestSpa.Json, Ct)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync($"/api/business-contacts/{contact.Id}/unarchive", new { }, TestSpa.Json, Ct)).StatusCode);
        }

        // Das Backoffice liest, aber ändert nichts; der Manager darf wie der Inhaber alles.
        Assert.False((await ListAsync(backoffice)).Single().CanEdit);
        Assert.True((await ListAsync(manager)).Single().CanEdit);
        Assert.Equal(HttpStatusCode.Created, (await manager.PostAsJsonAsync("/api/business-contacts", Input("Vom Manager"), TestSpa.Json, Ct)).StatusCode);

        // Archivierte sieht nur, wer verwalten darf; für alle anderen bleibt die Option ohne Wirkung.
        await spa.PostAsync<BusinessContactDto>($"/api/business-contacts/{contact.Id}/archive", new { }, Ct);
        Assert.Equal(2, (await ListAsync(manager, "?includeArchived=true")).Count);
        Assert.Equal(["Vom Manager"], (await ListAsync(reception, "?includeArchived=true")).Select(c => c.Designation));
        Assert.Equal(["Vom Manager"], (await ListAsync(backoffice, "?includeArchived=true")).Select(c => c.Designation));
    }

    [Fact]
    public async Task Standortleitung_liest_ihre_Standorte_und_alle_Standorte_aber_aendert_nur_Eintraege_ihrer_Standorte()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var other = await spa.CreateLocationAsync("Zweiter Standort", [DayOfWeek.Monday], Ct);
        var everywhere = await CreateAsync(spa, Input("Überall"));
        var here = await CreateAsync(spa, Input("Hier", all: false, locations: [spa.Location.Id]));
        var there = await CreateAsync(spa, Input("Dort", all: false, locations: [other.Id]));
        using var manager = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Manager, [spa.Location.Id]);

        var listed = await ListAsync(manager);
        Assert.Equal(["Hier", "Überall"], listed.Select(c => c.Designation).Order(StringComparer.Ordinal));
        // „Überall“ liest sie, ändern darf sie nur, was ganz in ihren Standorten liegt.
        Assert.Equal([("Hier", true), ("Überall", false)], listed.OrderBy(c => c.Designation, StringComparer.Ordinal).Select(c => (c.Designation, c.CanEdit)));
        Assert.Equal(HttpStatusCode.NotFound, (await manager.PutAsJsonAsync($"/api/business-contacts/{everywhere.Id}", Input("Überall"), TestSpa.Json, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await manager.PutAsJsonAsync($"/api/business-contacts/{there.Id}", Input("Dort", all: false, locations: [other.Id]), TestSpa.Json, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await manager.PostAsJsonAsync($"/api/business-contacts/{everywhere.Id}/archive", new { }, TestSpa.Json, Ct)).StatusCode);
        Assert.Contains("allLocations", await FieldsAsync(manager, Input("Für alle")));
        Assert.Contains("locationIds", await FieldsAsync(manager, Input("Fremd", all: false, locations: [other.Id])));
        Assert.Equal(HttpStatusCode.Created, (await manager.PostAsJsonAsync("/api/business-contacts", Input("Eigene", all: false, locations: [spa.Location.Id]), TestSpa.Json, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await manager.PutAsJsonAsync($"/api/business-contacts/{here.Id}", Input("Hier 2", all: false, locations: [spa.Location.Id]), TestSpa.Json, Ct)).StatusCode);

        // Der Inhaber grenzt die Liste auf einen Standort ein; ein fremder Standort ist für die Standortleitung gesperrt.
        Assert.Equal(["Dort", "Überall"], (await ListAsync(spa.Client, $"?locationId={other.Id}")).Select(c => c.Designation).Order(StringComparer.Ordinal));
        Assert.Equal(HttpStatusCode.Forbidden, (await manager.GetAsync($"/api/business-contacts?locationId={other.Id}", Ct)).StatusCode);
    }
}
