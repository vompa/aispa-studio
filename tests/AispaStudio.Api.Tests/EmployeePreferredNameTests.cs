using System.Net;
using System.Net.Http.Json;
using AispaStudio.Contracts;
using Microsoft.AspNetCore.Http;

namespace AispaStudio.Api.Tests;

/// <summary>Rufname („Name im Haus“) für Mitarbeitende (ADR 0064, Nachtrag): speichern, begrenzen, im Haus erkennen; nach außen gilt der volle Name.</summary>
public sealed class EmployeePreferredNameTests(ApiFixture fixture)
{
    private const string FullName = "Siriporn Kaewdee";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Rufname_wird_getrimmt_gespeichert_und_leer_wird_null()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);

        var saved = await SaveAsync(spa, spa.Anna, FullName, "  Siri ");
        var listed = (await spa.GetAsync<List<EmployeeDto>>("/api/employees", Ct)).Single(e => e.Id == spa.Anna.Id);
        var cleared = await SaveAsync(spa, spa.Anna, FullName, "   ");

        Assert.Equal(("Siri", FullName), (saved.PreferredName, saved.DisplayName));
        Assert.Equal("Siri", listed.PreferredName);
        Assert.Null(cleared.PreferredName);
        Assert.Equal("Siri", EmployeeNames.InHouse(FullName, saved.PreferredName));
        Assert.Equal(FullName, EmployeeNames.InHouse(FullName, cleared.PreferredName));
    }

    [Fact]
    public async Task Rufname_mit_mehr_als_40_Zeichen_wird_abgelehnt_40_gehen()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);

        var tooLong = await spa.Client.PutAsJsonAsync($"/api/employees/{spa.Anna.Id}", Input(spa.Anna, "Anna", new string('x', 41)), TestSpa.Json, Ct);
        var exact = await SaveAsync(spa, spa.Anna, "Anna", new string('x', 40));

        Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);
        var problem = await tooLong.Content.ReadFromJsonAsync<HttpValidationProblemDetails>(TestSpa.Json, Ct);
        Assert.Equal("Höchstens 40 Zeichen.", problem!.Errors["preferredName"].Single());
        Assert.Equal(40, exact.PreferredName!.Length);
    }

    [Fact]
    public async Task Kalenderanfrage_findet_die_Person_unter_Rufname_und_vollem_Namen()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await SaveAsync(spa, spa.Anna, FullName, "Siri");

        var byNickname = await AskAsync(spa, "lieber bei Siri");
        var byFullFirstName = await AskAsync(spa, "lieber bei Siriporn");
        var excluded = await AskAsync(spa, "Massage am 01.03.2027 um 10 Uhr, nicht bei Siri");

        Assert.Equal(spa.Anna.Id, byNickname.Wish.EmployeeId);
        Assert.Equal(spa.Anna.Id, byFullFirstName.Wish.EmployeeId);
        Assert.Contains(byNickname.Chips, c => c is { Part: CalendarWishPart.Employee, Text: "bei Siri" });
        Assert.Contains(byFullFirstName.Chips, c => c is { Part: CalendarWishPart.Employee, Text: "bei Siri" });
        Assert.Equal([spa.Anna.Id], excluded.Wish.ExcludedEmployeeIds);
        Assert.Equal("nicht Siri", excluded.Chips[^1].Text);
        Assert.All(excluded.Proposals, p => Assert.Equal(spa.Ben.Id, p.EmployeeId));
    }

    [Fact]
    public async Task Lohn_nennt_weiter_den_vollen_Namen()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await SaveAsync(spa, spa.Anna, FullName, "Siri");

        var payroll = await spa.GetAsync<List<PayrollEmployeeDto>>("/api/payroll/employees", Ct);

        Assert.Equal(FullName, payroll.Single(e => e.EmployeeId == spa.Anna.Id).DisplayName);
    }

    private static EmployeeInput Input(EmployeeDto e, string displayName, string? preferredName) =>
        new(displayName, e.Color, e.QualificationIds, e.WorkingHours, e.Languages, e.Gender, preferredName);

    private static Task<EmployeeDto> SaveAsync(TestSpa spa, EmployeeDto e, string displayName, string? preferredName) =>
        spa.PutAsync<EmployeeDto>($"/api/employees/{e.Id}", Input(e, displayName, preferredName), Ct);

    private static async Task<CalendarRequestResult> AskAsync(TestSpa spa, string text)
    {
        var response = await spa.Client.PostAsJsonAsync($"/api/locations/{spa.Location.Id}/calendar-request", new CalendarRequestInput(text, null), TestSpa.Json, Ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CalendarRequestResult>(TestSpa.Json, Ct))!;
    }
}
