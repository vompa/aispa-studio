using System.Net;
using System.Net.Http.Json;
using AispaStudio.Api.Data;
using AispaStudio.Api.MailIntake;
using AispaStudio.Api.Scheduling;
using AispaStudio.Api.Tenancy;
using AispaStudio.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace AispaStudio.Api.Tests;

/// <summary>
/// Such- und Auswahlstellen schließen anonymisierte Gäste aus (gezielter Filter, kein globaler Abfragefilter: der würde über <c>Appointment.Customer</c> auch die alten
/// Termine des Gastes ausblenden): Kundensuche, Buchen, Mail-Zuordnung, Assistent, Kundenbereich, Marketing und Kassa. Lesestellen für alte Termine, Belege, Berichte und
/// Verlauf bleiben ohne Filter.
/// </summary>
public sealed partial class CustomerAnonymizeTests
{
    [Fact]
    public async Task Kundensuche_findet_den_anonymisierten_Gast_nicht_mehr_auch_nicht_als_Platzhalter()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var other = await spa.PostAsync<CustomerDto>("/api/customers", new CustomerInput("Rita", $"Gast{Guid.NewGuid():N}", null, null, null, null), Ct);
        var lastName = spa.Customer.LastName;
        Assert.Single(await spa.GetAsync<List<CustomerDto>>($"/api/customers?search={lastName}", Ct));

        Assert.Equal(HttpStatusCode.NoContent, (await spa.Client.PostAsync($"/api/customers/{spa.Customer.Id}/anonymize", null, Ct)).StatusCode);

        Assert.Empty(await spa.GetAsync<List<CustomerDto>>($"/api/customers?search={lastName}", Ct));
        Assert.DoesNotContain(await spa.GetAsync<List<CustomerDto>>("/api/customers?search=Gelöschter", Ct), c => c.Id == spa.Customer.Id);
        Assert.Contains(await spa.GetAsync<List<CustomerDto>>($"/api/customers?search={other.LastName}", Ct), c => c.Id == other.Id);
    }

    [Fact]
    public async Task Kundenexport_laesst_den_anonymisierten_Gast_weg_und_seine_vCard_gibt_es_nicht_mehr()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var other = await spa.PostAsync<CustomerDto>("/api/customers", new CustomerInput("Rita", $"Gast{Guid.NewGuid():N}", null, null, null, null), Ct);
        Assert.Equal(HttpStatusCode.NoContent, (await spa.Client.PostAsync($"/api/customers/{spa.Customer.Id}/anonymize", null, Ct)).StatusCode);

        using var export = await spa.Client.GetAsync("/api/customers/export?format=csv", Ct);
        Assert.Equal(HttpStatusCode.OK, export.StatusCode);
        var text = await export.Content.ReadAsStringAsync(Ct);
        Assert.Contains(other.LastName, text, StringComparison.Ordinal);
        Assert.DoesNotContain(CustomerAnonymizer.PlaceholderFirstName, text, StringComparison.Ordinal);

        using var vcard = await spa.Client.GetAsync($"/api/customers/{spa.Customer.Id}/vcard", Ct);
        Assert.Equal(HttpStatusCode.NotFound, vcard.StatusCode);
    }

    [Fact]
    public async Task Buchen_lehnt_eine_geloeschte_CustomerId_mit_Validierungsfehler_ab()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        Assert.Equal(HttpStatusCode.NoContent, (await spa.Client.PostAsync($"/api/customers/{spa.Customer.Id}/anonymize", null, Ct)).StatusCode);

        var single = await spa.BookAsync(TestSpa.At(10), Ct);
        var started = await spa.Client.PostAsJsonAsync("/api/appointments",
            new AppointmentRequest(spa.Location.Id, spa.MassageTreatment.Id, spa.Customer.Id, TestSpa.At(12), spa.Anna.Id, null, null, null), TestSpa.Json, Ct);

        foreach (var response in new[] { single, started })
        {
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var problem = (await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>(TestSpa.Json, Ct))!;
            Assert.Contains("anonymisiert", Assert.Single(problem.Errors, e => string.Equals(e.Key, "CustomerId", StringComparison.OrdinalIgnoreCase)).Value[0], StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task Mail_Zuordnung_schlaegt_den_Platzhalter_weder_als_Treffer_noch_als_Kandidaten_vor()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        Assert.Equal(HttpStatusCode.NoContent, (await spa.Client.PostAsync($"/api/customers/{spa.Customer.Id}/anonymize", null, Ct)).StatusCode);

        var duplicates = await spa.Client.PostAsJsonAsync("/api/customers/duplicates", new DuplicateCheckInput("Gelöschter", "Gast", null, null), TestSpa.Json, Ct);
        Assert.Empty((await duplicates.Content.ReadFromJsonAsync<List<CustomerCandidateDto>>(TestSpa.Json, Ct))!);

        // Eine Anfrage, die den Gast schon bestätigt hatte, ordnet ihn nicht mehr zu: der Kunde lässt sich nicht mehr wählen.
        var mailId = await AddMailAsync(spa, null);
        var assign = await spa.Client.PostAsJsonAsync($"/api/inbox/{mailId}/customer/assign", new AssignCustomerInput(spa.Customer.Id), TestSpa.Json, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, assign.StatusCode);
    }

    [Fact]
    public async Task Assistent_findet_den_Platzhalter_nicht()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var other = await spa.PostAsync<CustomerDto>("/api/customers", new CustomerInput("Rita", $"Beispiel{Guid.NewGuid():N}", null, null, null, null), Ct);
        Assert.Equal(HttpStatusCode.NoContent, (await spa.Client.PostAsync($"/api/customers/{spa.Customer.Id}/anonymize", null, Ct)).StatusCode);

        // Gegenprobe: ein anderer Gast wird gefunden, der Assistent sucht also.
        var control = await AskAsync(spa, $"Wer ist Frau {other.LastName}?");
        Assert.Equal("1 Gast gefunden.", control.Text);

        var answer = await AskAsync(spa, "Wer ist Frau Gelöschter Gast?");

        Assert.DoesNotContain(answer.Block?.Items ?? [], i => i.Suggestion?.CustomerId == spa.Customer.Id);
        Assert.DoesNotContain(answer.Suggestions, s => s.CustomerId == spa.Customer.Id);
        Assert.NotEqual("1 Gast gefunden.", answer.Text);

        // Der Gästefinder hinter „Wann war … zuletzt da?“ kennt den Platzhalter ebenso wenig.
        var lastVisit = await AskAsync(spa, "Wann war Frau Gelöschter Gast zuletzt da?");
        Assert.Null(lastVisit.Block?.GuestId);
        Assert.StartsWith("Keinen Gast", lastVisit.Text, StringComparison.Ordinal);
    }

    private static async Task<AssistantAnswer> AskAsync(TestSpa spa, string question)
    {
        var response = await spa.Client.PostAsJsonAsync("/api/assistant", new AssistantQuestion(question, spa.Location.Id), TestSpa.Json, Ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AssistantAnswer>(TestSpa.Json, Ct))!;
    }

    [Fact]
    public async Task Online_Anmeldung_mit_der_alten_Adresse_verknuepft_den_anonymisierten_Gast_nicht_mehr()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var email = $"anonym-{Guid.NewGuid():N}@example.at";
        _ = await spa.PutAsync<CustomerDto>($"/api/customers/{spa.Customer.Id}",
            new CustomerInput("Eva", spa.Customer.LastName, email, "+43 664 000", null, null), Ct);
        var first = await SignInAsync(spa, email);
        Assert.Equal("Eva", (await first.GetFromJsonAsync<CustomerSession>("/api/online/me", TestSpa.Json, Ct))!.FirstName);

        Assert.Equal(HttpStatusCode.NoContent, (await spa.Client.PostAsync($"/api/customers/{spa.Customer.Id}/anonymize", null, Ct)).StatusCode);

        // Das alte Konto ist weg; wer sich mit derselben Adresse neu anmeldet, bekommt ein frisches Konto ohne Kunden.
        using var again = await SignInAsync(spa, email);
        var session = (await again.GetFromJsonAsync<CustomerSession>("/api/online/me", TestSpa.Json, Ct))!;
        Assert.Null(session.FirstName);
        Assert.False(session.HasProfile);
        Assert.Equal(1L, await ScalarAsync<long>(fixture.ConnectionString, $"""SELECT count(*) FROM online."CustomerAccounts" WHERE lower("Email") = '{email}'"""));
        Assert.Equal(0L, await ScalarAsync<long>(fixture.ConnectionString,
            $"""SELECT count(*) FROM online."CustomerAccounts" WHERE "CustomerId" = '{spa.Customer.Id}'"""));
    }

    [Fact]
    public async Task Marketing_Einwilligung_und_Kassa_Gastwahl_kennen_den_anonymisierten_Gast_nicht()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var register = await spa.PostAsync<CashRegisterDto>("/api/pos/registers", RegisterInput(spa), Ct);
        Assert.Equal(HttpStatusCode.NoContent, (await spa.Client.PostAsync($"/api/customers/{spa.Customer.Id}/anonymize", null, Ct)).StatusCode);

        var consent = await spa.Client.PostAsJsonAsync($"/api/customers/{spa.Customer.Id}/marketing-consent", new MarketingConsentRequest(null), TestSpa.Json, Ct);
        var bill = await spa.Client.PostAsJsonAsync("/api/pos/bills", new BillInput(register.Id, spa.Customer.Id, []), TestSpa.Json, Ct);

        Assert.Equal(HttpStatusCode.NotFound, consent.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, bill.StatusCode);
    }

    private async Task<Guid> AddMailAsync(TestSpa spa, Guid? customerId)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Use(spa.TenantId);
        var db = scope.ServiceProvider.GetRequiredService<AispaDbContext>();
        var mail = new InboundMail
        {
            MessageId = $"m-{Guid.NewGuid():N}",
            From = "neu@example.at",
            To = "spa@example.at",
            Subject = "Frage",
            Body = "Bitte melden",
            ReceivedAt = DateTimeOffset.UtcNow,
            Kind = InboundMailKind.BookingRequest,
            Status = InboundMailStatus.NeedsReview,
            CustomerId = customerId,
        };
        db.InboundMails.Add(mail);
        await db.SaveChangesAsync(Ct);
        return mail.Id;
    }
}
