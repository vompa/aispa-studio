using System.Text.Json;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;
using AispaStudio.Desktop.ViewModels;

namespace AispaStudio.Desktop.Tests;

/// <summary>
/// Kundenliste exportieren, Kontakt als vCard und Werbe-Einwilligung am Empfang (Export/Import, Aufgabe B): die App ruft die Export-Endpunkte mit der aktuellen Suche auf,
/// speichert die Datei, meldet „Gespeichert: …“ unten und speichert die Entscheidung des Gastes; Inhalt und Rechte der Dateien prüfen die API-Tests.
/// </summary>
public sealed class CustomersExportTests
{
    private static readonly CustomerDto Customer = SpaSample.Customers[0];

    private static StubApi Api() => new StubApi()
        .On("GET /api/customers?search=", SpaSample.Customers.ToList())
        .On("GET /api/customers?search=m%C3%BCl", SpaSample.Customers.ToList())
        .On($"GET /api/customers/{Customer.Id}/appointments", new List<AppointmentDto>())
        .On($"GET /api/customers/{Customer.Id}/marketing-consent", new MarketingConsentDto(MarketingConsentState.None, null, null, null, null, null, null));

    private static async Task<CustomersViewModel> Customers(StubApi api, CurrentUser user, IFileSaver? files = null)
    {
        var customers = new CustomersViewModel(api.Client(), new ManualTime(new DateTimeOffset(2026, 11, 9, 10, 0, 0, TimeSpan.FromHours(1)))) { User = user };
        customers.Export.Files = files;
        await customers.InitializeAsync();
        return customers;
    }

    [Fact]
    public async Task Die_Leitung_sieht_fuenf_Formate_die_Rezeption_vier_und_jeder_Pfad_traegt_die_Suche()
    {
        var owner = await Customers(Api(), TestData.Owner());
        var reception = await Customers(Api(), TestData.Reception());

        Assert.Equal(["Excel", "CSV", "Portal-CSV", "vCard", "Newsletter-Liste"], owner.Export.Options.Select(o => o.Label));
        Assert.Equal(["Excel", "CSV", "Portal-CSV", "vCard"], reception.Export.Options.Select(o => o.Label));

        owner.Query = "mül";
        Assert.Equal("api/customers/export?format=xlsx&search=m%C3%BCl", owner.Export.Options[0].Path());
        Assert.Equal("api/customers/export?format=csv&search=m%C3%BCl&list=newsletter", owner.Export.Options[4].Path());
    }

    [Fact]
    public async Task Excel_speichern_meldet_Gespeichert_unten()
    {
        var api = Api().OnFile("GET /api/customers/export?format=xlsx&search=", [1, 2, 3], "Kunden_2026-11-09.xlsx");
        var files = new FakeFileSaver();
        var customers = await Customers(api, TestData.Owner(), files);

        await customers.Export.ExportCommand.ExecuteAsync(customers.Export.Options[0]);

        Assert.Equal("Kunden_2026-11-09.xlsx", Assert.Single(files.Saved).Name);
        Assert.Equal("Gespeichert: Kunden_2026-11-09.xlsx", customers.Toast);
        Assert.True(customers.HasToast);
        customers.CloseToastCommand.Execute(null);
        Assert.Null(customers.Toast);
    }

    [Fact]
    public async Task Ein_Serverfehler_steht_als_Hinweis_der_Seite()
    {
        var api = Api().On("GET /api/customers/export?format=csv&search=", new { title = "Mehr als 50.000 Zeilen. Zeitraum oder Filter enger wählen." }, System.Net.HttpStatusCode.BadRequest);
        var customers = await Customers(api, TestData.Owner(), new FakeFileSaver());

        await customers.Export.ExportCommand.ExecuteAsync(customers.Export.Options[1]);

        Assert.Equal("Mehr als 50.000 Zeilen. Zeitraum oder Filter enger wählen.", customers.Error);
        Assert.Null(customers.Toast);
    }

    [Fact]
    public async Task Kontakt_als_vCard_nimmt_die_ausgewaehlte_Kundin()
    {
        var api = Api().OnFile($"GET /api/customers/{Customer.Id}/vcard", [1], "Kontakt_Mueller.vcf");
        var files = new FakeFileSaver();
        var customers = await Customers(api, TestData.Reception(), files);
        customers.Selected = customers.Results[0];

        await customers.Export.ExportCommand.ExecuteAsync(customers.VCardOption);

        Assert.Equal("Kontakt_Mueller.vcf", Assert.Single(files.Saved).Name);
        Assert.Equal("Gespeichert: Kontakt_Mueller.vcf", customers.Toast);
    }

    [Fact]
    public async Task Werbe_Einwilligung_laden_aendern_und_speichern()
    {
        var saved = new MarketingConsentDto(MarketingConsentState.Confirmed, "b.mueller@example.at", "de", new DateTimeOffset(2026, 11, 9, 9, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 11, 9, 9, 0, 0, TimeSpan.Zero), null, "Rezeption", "Lena Berger");
        var api = Api().On($"PUT /api/customers/{Customer.Id}/marketing-consent", saved);
        var customers = await Customers(api, TestData.Reception());

        customers.Selected = customers.Results[0];
        await Task.Yield();
        Assert.Equal("Nicht gefragt", customers.ConsentText);
        Assert.True(customers.ConsentSectionVisible);

        customers.EditConsentCommand.Execute(null);
        Assert.True(customers.IsEditingConsent);
        Assert.Equal("Nicht gefragt", customers.ConsentChoice.Label);
        customers.ConsentChoice = ConsentChoice.All[0];
        var dialogs = new FakeDialogs().Answer(ConfirmChoice.Cancelled).Answer(ConfirmChoice.Confirmed);
        customers.Dialogs = dialogs;
        await customers.SaveConsentCommand.ExecuteAsync(null);
        Assert.DoesNotContain(api.Calls, c => c.Route == $"PUT /api/customers/{Customer.Id}/marketing-consent");
        Assert.True(customers.IsEditingConsent);
        Assert.Equal("Hat der Gast zugestimmt?", dialogs.Asked[0].Title);
        Assert.Contains("mündlich oder schriftlich", dialogs.Asked[0].Message, StringComparison.Ordinal);
        Assert.Equal("Erfassen", dialogs.Asked[0].ConfirmText);
        Assert.False(dialogs.Asked[0].IsDestructive);
        dialogs.AssertRules();
        await customers.SaveConsentCommand.ExecuteAsync(null);

        var sent = JsonSerializer.Deserialize<MarketingConsentDecision>(api.Calls.Last(c => c.Route == $"PUT /api/customers/{Customer.Id}/marketing-consent").Body!, ApiClient.Json)!;
        Assert.Equal(MarketingConsentState.Confirmed, sent.State);
        Assert.False(customers.IsEditingConsent);
        Assert.Equal("Ja, am Empfang erfasst am 9.11.2026 von Lena Berger", customers.ConsentText);
        Assert.Equal("Werbe-Einwilligung gespeichert", customers.Toast);

        customers.EditConsentCommand.Execute(null);
        Assert.Equal("Ja", customers.ConsentChoice.Label);
        customers.CancelConsentCommand.Execute(null);
        Assert.False(customers.IsEditingConsent);
    }

    [Fact]
    public async Task Einwilligung_abgelehnt_hat_ihren_eigenen_Text_und_die_Auswahl_Nein()
    {
        var refused = new MarketingConsentDto(MarketingConsentState.Refused, "b.mueller@example.at", "de", null, null, new DateTimeOffset(2026, 11, 2, 9, 0, 0, TimeSpan.Zero), "Rezeption", "Lena Berger");
        var api = Api().On($"GET /api/customers/{Customer.Id}/marketing-consent", refused);
        var customers = await Customers(api, TestData.Reception());
        customers.Selected = customers.Results[0];
        await Task.Yield();

        Assert.Equal("Nein, am Empfang erfasst am 2.11.2026 von Lena Berger", customers.ConsentText);
        customers.EditConsentCommand.Execute(null);
        Assert.Equal("Nein", customers.ConsentChoice.Label);
    }

    [Theory]
    [InlineData("Team", null, "Ja, per Mail bestätigt am 9.11.2026 (b.mueller@example.at)")]
    [InlineData("Online", null, "Ja, per Mail bestätigt am 9.11.2026 (b.mueller@example.at)")]
    [InlineData("Import", null, "Ja, aus dem Import übernommen am 9.11.2026")]
    [InlineData("Rezeption", null, "Ja, am Empfang erfasst am 9.11.2026")]
    public async Task Quelle_der_Einwilligung_ist_im_Text_getrennt_lesbar(string source, string? recordedBy, string expected)
    {
        var at = new DateTimeOffset(2026, 11, 9, 9, 0, 0, TimeSpan.Zero);
        var consent = new MarketingConsentDto(MarketingConsentState.Confirmed, "b.mueller@example.at", "de", at, at, null, source, recordedBy);
        var customers = await Customers(Api().On($"GET /api/customers/{Customer.Id}/marketing-consent", consent), TestData.Reception());
        customers.Selected = customers.Results[0];
        await Task.Yield();

        Assert.Equal(expected, customers.ConsentText);
    }
}
