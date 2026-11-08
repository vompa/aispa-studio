using System.Net;
using System.Net.Http.Json;
using AispaStudio.Contracts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AispaStudio.Api.Tests;

/// <summary>Einladungen gehen als Mail über die Queue; ausgeschiedene Personen stehen in einer eigenen Liste (Slice 98).</summary>
public sealed class TeamInvitationTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Given_neue_Mitarbeiterin_When_die_Inhaberin_sie_mit_Mailadresse_einlaedt_Then_geht_die_Einladung_als_Mail_ueber_die_Queue_mit_Link()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var email = $"Neu-{Guid.NewGuid():N}@aispa.test";
        var publicUrl = fixture.Factory.Services.GetRequiredService<IConfiguration>()["OnlineBooking:PublicUrl"] ?? "http://localhost:5173";

        var created = await spa.PostAsync<InvitationCreated>("/api/staff/invitations",
            new InvitationInput(email, "Nina Neu", StaffRole.Reception, false, [spa.Location.Id], null), Ct);

        var mail = await fixture.Mailbox.WaitForAsync(email.ToLowerInvariant(), _ => true, Ct);
        Assert.Single(fixture.Mailbox.To(email));
        Assert.Equal(email.ToLowerInvariant(), mail.To);
        Assert.Contains("Einladung", mail.Subject, StringComparison.Ordinal);
        Assert.Contains($"{publicUrl.TrimEnd('/')}/anmelden/?einladung={Uri.EscapeDataString(created.Token)}", mail.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Given_zwei_Spas_When_das_zweite_einlaedt_Then_nennt_die_Mail_das_zweite_und_die_Antwort_den_Link_der_Mail()
    {
        var first = await TestSpa.CreateAsync(fixture, Ct);
        var second = await TestSpa.CreateAsync(fixture, Ct);
        var firstName = $"Erstes Spa {Guid.NewGuid():N}";
        var secondName = $"Zweites Spa {Guid.NewGuid():N}";
        await first.PutAsync<TenantDto>("/api/tenant", new TenantInput(firstName, "AT"), Ct);
        await second.PutAsync<TenantDto>("/api/tenant", new TenantInput(secondName, "AT"), Ct);
        var email = $"neu-{Guid.NewGuid():N}@aispa.test";

        var created = await second.PostAsync<InvitationCreated>("/api/staff/invitations",
            new InvitationInput(email, "Nina Neu", StaffRole.Reception, false, [second.Location.Id], null), Ct);

        var mail = await fixture.Mailbox.WaitForAsync(email, _ => true, Ct);
        Assert.Equal($"Einladung zu {secondName}", mail.Subject);
        Assert.Contains($"bei {secondName}", mail.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(firstName, mail.Text, StringComparison.Ordinal);
        Assert.Contains(created.Link, mail.Text, StringComparison.Ordinal);
        Assert.EndsWith($"/anmelden/?einladung={Uri.EscapeDataString(created.Token)}", created.Link, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Einladungslink_fuehrt_auf_anmelden()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var created = await spa.PostAsync<InvitationCreated>("/api/staff/invitations",
            new InvitationInput($"link-{Guid.NewGuid():N}@aispa.test", "Lena Link", StaffRole.Reception, false, [spa.Location.Id], null), Ct);

        var uri = new Uri(created.Link);

        Assert.Equal("/anmelden/", uri.AbsolutePath);
        Assert.Equal($"?einladung={Uri.EscapeDataString(created.Token)}", uri.Query);
    }

    // Die Kette, die die Seite /anmelden/ nutzt: Einladung annehmen, Code für die Windows-App holen, in der App einlösen.
    [Fact]
    public async Task Eingeladene_Person_nimmt_an_und_loest_einen_Code_fuer_die_Windows_App_ein()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var email = $"kette-{Guid.NewGuid():N}@aispa.test";
        var created = await spa.PostAsync<InvitationCreated>("/api/staff/invitations",
            new InvitationInput(email, "Karla Kette", StaffRole.Reception, false, [spa.Location.Id], null), Ct);
        using var browser = fixture.CreateClient();

        var preview = await browser.GetFromJsonAsync<InvitationPreview>($"/api/auth/invitations/{created.Token}", TestSpa.Json, Ct);
        var accepted = await browser.PostAsync($"/api/auth/invitations/{created.Token}/accept", null, Ct);
        var invited = await TestSpa.ReadAsync<CurrentUser>(accepted, Ct);
        using var signedIn = fixture.CreateClient(string.Join("; ", accepted.Headers.GetValues("Set-Cookie").Select(c => c.Split(';')[0])));
        var code = await TestSpa.ReadAsync<DeviceCode>(await signedIn.PostAsync("/api/auth/device-code", null, Ct), Ct);
        using var app = fixture.CreateClient();
        var redeemed = await app.PostAsJsonAsync("/api/auth/device-code/redeem", new DeviceCodeRedeem(code.Code), TestSpa.Json, Ct);
        var user = await TestSpa.ReadAsync<CurrentUser>(redeemed, Ct);

        Assert.Equal(email, preview!.Email, ignoreCase: true);
        Assert.Equal(invited.Id, user.Id);
        Assert.Equal(StaffRole.Reception, user.Access?.Role);
    }

    [Fact]
    public async Task Given_ausgeschiedene_Person_When_die_Liste_der_Ausgeschiedenen_geladen_wird_Then_steht_sie_dort_und_nicht_in_der_Teamliste()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var person = await spa.PostAsync<EmployeeDto>("/api/employees",
            new EmployeeInput($"Gone {Guid.NewGuid():N}", "#2f6f5e", [spa.Massage.Id], []), Ct);

        var removed = await spa.Client.DeleteAsync($"/api/employees/{person.Id}", Ct);
        var former = await spa.Client.GetAsync("/api/employees/former", Ct);
        var team = await spa.GetAsync<List<EmployeeDto>>("/api/employees", Ct);

        Assert.True(removed.IsSuccessStatusCode);
        Assert.Equal(HttpStatusCode.OK, former.StatusCode);
        Assert.Contains((await TestSpa.ReadAsync<List<EmployeeDto>>(former, Ct)), e => e.Id == person.Id);
        Assert.DoesNotContain(team, e => e.Id == person.Id);
    }

    [Fact]
    public async Task Given_Rezeption_When_sie_die_Ausgeschiedenen_laden_will_Then_403()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        using var reception = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Reception, [spa.Location.Id]);

        var response = await reception.GetAsync("/api/employees/former", Ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
