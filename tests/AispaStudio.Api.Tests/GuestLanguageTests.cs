using System.Net;
using System.Net.Http.Json;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Tests;

/// <summary>Sprache des Gastes, Sprachen im Team und Quelle eines Termins.</summary>
public sealed class GuestLanguageTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Sprache_des_Gastes_wird_gespeichert_und_steht_im_Kalender()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var c = spa.Customer;
        var path = $"/api/customers/{c.Id}";

        var italian = await spa.PutAsync<CustomerDto>(path, new CustomerInput(c.FirstName, c.LastName, c.Email, c.Phone, null, null, Language: " IT "), Ct);
        Assert.Equal("it", italian.Language);
        await spa.BookOkAsync(TestSpa.At(10), Ct);
        var day = await spa.GetAsync<CalendarDay>($"/api/locations/{spa.Location.Id}/calendar?date={TestSpa.Monday:yyyy-MM-dd}", Ct);
        Assert.Equal("it", Assert.Single(day.Guests!).Language);
        Assert.Equal(AppointmentSource.Reception, Assert.Single(day.Appointments).Source);

        // Deutsch und leer heißen dasselbe; Unbekanntes wird abgelehnt.
        Assert.Null((await spa.PutAsync<CustomerDto>(path, new CustomerInput(c.FirstName, c.LastName, c.Email, c.Phone, null, null, Language: "de"), Ct)).Language);
        var unknown = await spa.Client.PutAsJsonAsync(path, new CustomerInput(c.FirstName, c.LastName, c.Email, c.Phone, null, null, Language: "xx"), TestSpa.Json, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);
        var created = await spa.PostAsync<CustomerDto>("/api/customers", new CustomerInput("Ana", "Gost", null, null, null, null, Language: "hr"), Ct);
        Assert.Equal("hr", created.Language);
    }

    [Fact]
    public async Task Sprachen_im_Team_werden_geordnet_gespeichert_und_Unbekanntes_abgelehnt()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var anna = spa.Anna;
        var path = $"/api/employees/{anna.Id}";

        var saved = await spa.PutAsync<EmployeeDto>(path,
            new EmployeeInput(anna.DisplayName, anna.Color, anna.QualificationIds, anna.WorkingHours, ["it", "EN", "en", "de"]), Ct);
        Assert.Equal(["en", "it"], saved.Languages);
        var unknown = await spa.Client.PutAsJsonAsync(path,
            new EmployeeInput(anna.DisplayName, anna.Color, anna.QualificationIds, anna.WorkingHours, ["klingonisch"]), TestSpa.Json, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);

        // Ohne Angabe (ältere Oberfläche) heißt: keine Sprachen außer Deutsch.
        var none = await spa.PutAsync<EmployeeDto>(path, new EmployeeInput(anna.DisplayName, anna.Color, anna.QualificationIds, anna.WorkingHours), Ct);
        Assert.Null(none.Languages);
    }
}
