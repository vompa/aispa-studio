using System.Net;
using System.Net.Http.Json;
using System.Text;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Tests;

/// <summary>
/// Werbe-Einwilligung am Empfang (Export/Import, Aufgabe B): „Ja“, „Nein“ oder „nicht gefragt“ an der bestehenden Einwilligung (ADR 0040 Double-Opt-in bleibt), Satz im Verlauf
/// des Gastes, Newsletter-Liste nur mit bestätigter Einwilligung und nur für die Leitung.
/// </summary>
public sealed class MarketingConsentTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Am_Empfang_erfasst_Ja_dann_Nein_mit_Saetzen_im_Verlauf()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var guest = await spa.PostAsync<CustomerDto>("/api/customers", new CustomerInput("Anna", "Huber", "anna@beispiel.at", null, null, null), Ct);

        var yes = await spa.PutAsync<MarketingConsentDto>($"/api/customers/{guest.Id}/marketing-consent", new MarketingConsentDecision(MarketingConsentState.Confirmed), Ct);
        Assert.Equal((MarketingConsentState.Confirmed, "Rezeption", "anna@beispiel.at"), (yes.State, yes.Source, yes.Email));
        Assert.NotNull(yes.ConfirmedAt);
        Assert.Equal("Test Person", yes.RecordedBy);
        var read = await spa.GetAsync<MarketingConsentDto>($"/api/customers/{guest.Id}/marketing-consent", Ct);
        Assert.Equal((MarketingConsentState.Confirmed, "Test Person"), (read.State, read.RecordedBy));

        var no = await spa.PutAsync<MarketingConsentDto>($"/api/customers/{guest.Id}/marketing-consent", new MarketingConsentDecision(MarketingConsentState.Refused), Ct);
        Assert.Equal(MarketingConsentState.Refused, no.State);
        Assert.Null(no.ConfirmedAt);
        Assert.NotNull(no.WithdrawnAt);
        Assert.Equal("Test Person", no.RecordedBy);

        var history = await spa.GetAsync<List<HistoryEntry>>($"/api/customers/{guest.Id}/history", Ct);
        Assert.Contains(history, h => h.Text == "Werbe-Einwilligung: ja von Test Person");
        Assert.Contains(history, h => h.Text.StartsWith("Werbe-Einwilligung geändert von Test Person: Werbe-Einwilligung ja → nein", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Nicht_gefragt_setzt_zurueck_und_andere_Zustaende_sind_keine_Entscheidung()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var guest = await spa.PostAsync<CustomerDto>("/api/customers", new CustomerInput("Max", "Maier", null, null, null, null), Ct);

        var none = await spa.PutAsync<MarketingConsentDto>($"/api/customers/{guest.Id}/marketing-consent", new MarketingConsentDecision(MarketingConsentState.None), Ct);
        Assert.Equal(MarketingConsentState.None, none.State);
        var yes = await spa.PutAsync<MarketingConsentDto>($"/api/customers/{guest.Id}/marketing-consent", new MarketingConsentDecision(MarketingConsentState.Confirmed), Ct);
        Assert.Equal(MarketingConsentState.Confirmed, yes.State);
        var reset = await spa.PutAsync<MarketingConsentDto>($"/api/customers/{guest.Id}/marketing-consent", new MarketingConsentDecision(MarketingConsentState.None), Ct);
        Assert.Equal((MarketingConsentState.None, (DateTimeOffset?)null), (reset.State, reset.ConfirmedAt));

        var pending = await spa.Client.PutAsJsonAsync($"/api/customers/{guest.Id}/marketing-consent", new MarketingConsentDecision(MarketingConsentState.Pending), TestSpa.Json, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, pending.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await spa.Client.PutAsJsonAsync($"/api/customers/{Guid.NewGuid()}/marketing-consent",
            new MarketingConsentDecision(MarketingConsentState.Confirmed), TestSpa.Json, Ct)).StatusCode);
    }

    [Fact]
    public async Task Newsletter_Liste_nur_mit_bestaetigter_Einwilligung_und_nur_fuer_die_Leitung()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var yes = await spa.PostAsync<CustomerDto>("/api/customers", new CustomerInput("Anna", "Huber", "anna@beispiel.at", null, null, null, Salutation: CustomerSalutation.Female, Language: "it"), Ct);
        var no = await spa.PostAsync<CustomerDto>("/api/customers", new CustomerInput("Max", "Maier", "max@beispiel.at", null, null, null), Ct);
        await spa.PutAsync<MarketingConsentDto>($"/api/customers/{yes.Id}/marketing-consent", new MarketingConsentDecision(MarketingConsentState.Confirmed), Ct);
        await spa.PutAsync<MarketingConsentDto>($"/api/customers/{no.Id}/marketing-consent", new MarketingConsentDecision(MarketingConsentState.Refused), Ct);

        using var response = await spa.Client.GetAsync("/api/customers/export?format=csv&list=newsletter", Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var bytes = await response.Content.ReadAsByteArrayAsync(Ct);
        var lines = Encoding.UTF8.GetString(bytes[3..]).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(["E-Mail;Vorname;Nachname;Sprache;Anrede", "anna@beispiel.at;Anna;Huber;Italienisch;Frau"], lines);
        Assert.Matches(@"^Newsletter_\d{4}-\d{2}-\d{2}\.csv$", (response.Content.Headers.ContentDisposition?.FileNameStar ?? "").Trim('"'));

        var history = await spa.GetAsync<List<HistoryEntry>>("/api/exports/history", Ct);
        Assert.Contains(history, h => h.Text == "Export Newsletter-Liste (CSV, 1 Zeile) von Test Person");

        using var reception = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Reception, [spa.Location.Id]);
        Assert.Equal(HttpStatusCode.Forbidden, (await reception.GetAsync("/api/customers/export?format=csv&list=newsletter", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await reception.GetAsync("/api/customers/export?format=csv", Ct)).StatusCode);
    }
}
