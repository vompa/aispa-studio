using System.Text.Json;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;
using AispaStudio.Desktop.ViewModels;

namespace AispaStudio.Desktop.Tests;

/// <summary>
/// Masken-Überarbeitung P11 (ADR 0098, Nachtrag): Tabelle „Wer darf was, wer ist zuständig?“. Themen, Hinweise, Wirkung und „Unbesetzt“ liefert die API (<c>StaffRightsTableTests</c>);
/// hier zählt, was die App daraus macht: eine Tabelle nach Themen, Filter, Standort, und dass „+ Person“ und „✕“ dieselben Endpunkte rufen wie die Personenkarte.
/// </summary>
public sealed class RightsTableTests
{
    private static readonly EmployeeDto Lena = RosterSample.Lena;
    private static readonly EmployeeDto Marco = RosterSample.Marco;
    private static readonly Guid StornoEntry = Guid.NewGuid();
    private static readonly Guid FirstAidEntry = Guid.NewGuid();
    private static readonly Guid FirstAidType = Guid.NewGuid();

    internal static StubApi Api() => AddRoutes(RosterTests.Setup().Api);

    /// <summary>Die Antworten der beiden Übersichten.</summary>
    internal static StubApi AddRoutes(StubApi api) =>
        api
            .On("GET /api/staff-regulations/powers", new List<PowerOverviewRowDto>
            {
                new(StaffPowerKind.CashVoid, "Kassa-Storno", true, [new(Lena.Id, "Lena", new DateOnly(2026, 1, 1), null, "Katrin", false) { EntryId = StornoEntry }])
                {
                    Group = "Kassa und Geld", GroupOrder = 1, Hint = "Belege stornieren", Effect = "Schaltet frei", IsUnstaffed = false,
                },
                new(StaffPowerKind.TriggerOrders, "Bestellungen auslösen", true, [])
                {
                    Group = "Lager", GroupOrder = 2, Hint = "Bestellungen an Lieferanten senden", Effect = "Schaltet frei", IsUnstaffed = true,
                },
                new(StaffPowerKind.Keys, "Schlüssel", false, [new(Marco.Id, "Marco", new DateOnly(2026, 1, 1), new DateOnly(2026, 3, 1), "Katrin", true) { EntryId = Guid.NewGuid() }])
                {
                    Group = "Sicherheit und Vertretung", GroupOrder = 3, Hint = "Schlüssel zum Haus", Effect = "Nur Aushang", IsUnstaffed = true,
                },
            })
            .On("GET /api/staff-duties/overview", new List<DutyOverviewRowDto>
            {
                new(FirstAidType, DutyTypeKey.FirstAid, "Ersthelfer", "Erste-Hilfe-Kurs", [new(FirstAidEntry, Lena.Id, "Lena", null, null, new DateOnly(2026, 1, 1), null, false)])
                {
                    Group = "Sicherheit und Vertretung", GroupOrder = 3, Hint = "Verlangt: Erste-Hilfe-Kurs", Effect = "Warnt im Dienstplan", IsUnstaffed = false,
                },
            });

    private static async Task<(TeamViewModel Team, RightsTableViewModel Table, FakeDialogs Dialogs)> Open(StubApi api)
    {
        var dialogs = new FakeDialogs();
        var team = await RosterTests.Loaded(api, dialogs: dialogs);
        Assert.True(await team.SwitchToAsync(TeamViewModel.PowersMode));
        var table = team.Powers!;
        await RosterTests.Until(() => table.IsLoaded);
        return (team, table, dialogs);
    }

    [Fact]
    public async Task Given_Vollmachten_und_Zustaendigkeiten_When_die_Tabelle_laedt_Then_stehen_beide_nach_Themen_in_der_Reihenfolge_der_API_in_einer_Tabelle()
    {
        var (_, table, _) = await Open(Api());

        Assert.Equal(["Kassa und Geld", "Lager", "Sicherheit und Vertretung"], table.Groups.Select(g => g.Title));
        Assert.Equal(["Schlüssel", "Ersthelfer"], table.Groups[2].Rows.Select(r => r.Name));
        var storno = table.Groups[0].Rows.Single();
        Assert.Equal(("Belege stornieren", "Schaltet frei", true, false), (storno.Hint, storno.Effect, storno.FreesSomething, storno.IsUnstaffed));
        var firstAid = table.Groups[2].Rows.Single(r => r.Name == "Ersthelfer");
        Assert.Equal(("Verlangt: Erste-Hilfe-Kurs", "Warnt im Dienstplan", false), (firstAid.Hint, firstAid.Effect, firstAid.FreesSomething));
    }

    [Fact]
    public async Task Given_die_Tabelle_When_sie_Personen_zeigt_Then_stehen_abgelaufene_ausgegraut_mit_Datum_und_Tooltip()
    {
        var (_, table, _) = await Open(Api());

        var keys = table.Groups[2].Rows.Single(r => r.Name == "Schlüssel");
        var marco = Assert.Single(keys.Holders);
        Assert.Equal(("Marco (abgelaufen am 1.3.2026)", true, "abgelaufen am 1.3.2026, erteilt von Katrin"), (marco.Text, marco.IsExpired, marco.Tip));
        Assert.True(keys.IsUnstaffed);
        Assert.Equal("Lena", Assert.Single(table.Groups[0].Rows[0].Holders).Text);
    }

    [Fact]
    public async Task Given_die_Filter_When_Unbesetzt_und_Schaltet_etwas_frei_gewaehlt_werden_Then_bleiben_nur_diese_Zeilen_und_die_Zahlen_stehen_am_Filter()
    {
        var (_, table, _) = await Open(Api());

        Assert.Equal(["Alle 4", "Unbesetzt 2", "Schaltet etwas frei 2"], table.Filters.Select(f => f.Label));
        table.Filters[1].SelectCommand.Execute(null);
        Assert.Equal(["Bestellungen auslösen", "Schlüssel"], table.Groups.SelectMany(g => g.Rows).Select(r => r.Name));
        Assert.Equal([false, true, false], table.Filters.Select(f => f.IsOn));
        table.Filters[2].SelectCommand.Execute(null);
        Assert.Equal(["Kassa-Storno", "Bestellungen auslösen"], table.Groups.SelectMany(g => g.Rows).Select(r => r.Name));
        table.Filters[0].SelectCommand.Execute(null);
        Assert.Equal(4, table.Groups.Sum(g => g.Rows.Count));
    }

    [Fact]
    public async Task Given_mehrere_Standorte_When_einer_gewaehlt_wird_Then_laedt_die_Tabelle_mit_locationId_und_bietet_nur_Personen_dieses_Standorts_an()
    {
        var api = Api();
        var vienna = $"?locationId={RosterSample.Vienna.Id}";
        api.On($"GET /api/staff-regulations/powers{vienna}", new List<PowerOverviewRowDto>
        {
            new(StaffPowerKind.Safe, "Tresor", false, []) { Group = "Kassa und Geld", GroupOrder = 1, Effect = "Nur Aushang", IsUnstaffed = true },
        })
            .On($"GET /api/staff-duties/overview{vienna}", new List<DutyOverviewRowDto>());
        var (_, table, _) = await Open(api);
        Assert.True(table.ShowsLocations);
        Assert.Equal(["Alle Standorte", RosterSample.Vienna.Name, RosterSample.Graz.Name], table.Locations.Select(l => l.Name));

        table.SelectedLocation = table.Locations[1];
        await RosterTests.Until(() => table.Groups.Count == 1 && table.Groups[0].Rows[0].Name == "Tresor");

        // Lena arbeitet in Wien, Marco hat keine Wochenarbeitszeit: nur Lena steht zur Wahl.
        Assert.Equal(["Lena Huber"], table.Groups[0].Rows[0].Candidates.Select(c => c.Name));
    }

    [Fact]
    public async Task Given_eine_Vollmacht_When_Person_hinzugefuegt_wird_Then_ruft_die_Tabelle_denselben_Endpunkt_wie_die_Personenkarte_und_meldet_die_Aenderung()
    {
        var api = Api();
        var route = $"POST /api/employees/{Marco.Id}/regulations/powers";
        api.OnJson(route, body => new StaffPowerDto(Guid.NewGuid(), Marco.Id, JsonSerializer.Deserialize<StaffPowerInput>(body!, ApiClient.Json)!.Kind, "Bestellungen auslösen", new DateOnly(2026, 11, 17), null, "Katrin", false));
        var (_, table, _) = await Open(api);
        var changed = 0;
        table.Changed += () =>
        {
            changed++;
            return Task.CompletedTask;
        };
        var orders = table.Groups[1].Rows.Single();
        Assert.Equal(["Lena Huber", "Marco Berger"], orders.Candidates.Select(c => c.Name));

        await orders.Candidates.Single(c => c.Name == "Marco Berger").AddCommand.ExecuteAsync(null);

        var sent = JsonSerializer.Deserialize<StaffPowerInput>(Assert.Single(api.Calls, c => c.Route == route).Body!, ApiClient.Json)!;
        Assert.Equal(StaffPowerKind.TriggerOrders, sent.Kind);
        Assert.Equal((1, "Eingetragen."), (changed, table.StatusText));
    }

    [Fact]
    public async Task Given_eine_Zustaendigkeit_When_Person_hinzugefuegt_wird_Then_ruft_die_Tabelle_den_Endpunkt_der_Zustaendigkeiten_der_Person()
    {
        var api = Api();
        var route = $"POST /api/employees/{Marco.Id}/duties";
        api.OnJson(route, _ => new StaffDutyDto(Guid.NewGuid(), Marco.Id, FirstAidType, "Ersthelfer", new DateOnly(2026, 11, 17), null, null, null, false));
        var (_, table, _) = await Open(api);

        var firstAid = table.Groups[2].Rows.Single(r => r.Name == "Ersthelfer");
        Assert.DoesNotContain("Lena Huber", firstAid.Candidates.Select(c => c.Name));
        await firstAid.Candidates.Single(c => c.Name == "Marco Berger").AddCommand.ExecuteAsync(null);

        var sent = JsonSerializer.Deserialize<StaffDutyInput>(Assert.Single(api.Calls, c => c.Route == route).Body!, ApiClient.Json)!;
        Assert.Equal((FirstAidType, null, null), (sent.DutyTypeId, sent.ValidUntil, sent.DeputyId));
    }

    [Fact]
    public async Task Given_eine_Zeile_When_die_Inhaberin_Person_entfernt_Then_fragt_die_Tabelle_und_loescht_erst_nach_Bestaetigung()
    {
        var api = Api();
        var route = $"DELETE /api/employees/{Lena.Id}/regulations/powers/{StornoEntry}";
        api.On(route, null);
        var (_, table, dialogs) = await Open(api);
        var holder = table.Groups[0].Rows.Single().Holders.Single();

        await holder.RemoveCommand.ExecuteAsync(null);
        Assert.DoesNotContain(api.Calls, c => c.Route == route);

        dialogs.Answer(ConfirmChoice.Confirmed);
        await holder.RemoveCommand.ExecuteAsync(null);

        Assert.Single(api.Calls, c => c.Route == route);
        Assert.Equal("Entfernt.", table.StatusText);
        dialogs.AssertRules();
        Assert.Equal("Lena bei „Kassa-Storno“ entfernen?", dialogs.Asked[0].Title);
    }

    [Fact]
    public async Task Given_eine_Zustaendigkeit_When_die_Inhaberin_Person_entfernt_Then_geht_der_Aufruf_an_die_Zustaendigkeit_der_Person()
    {
        var api = Api();
        var route = $"DELETE /api/employees/{Lena.Id}/duties/{FirstAidEntry}";
        api.On(route, null);
        var (_, table, dialogs) = await Open(api);
        dialogs.Answer(ConfirmChoice.Confirmed);

        await table.Groups[2].Rows.Single(r => r.Name == "Ersthelfer").Holders.Single().RemoveCommand.ExecuteAsync(null);

        Assert.Single(api.Calls, c => c.Route == route);
    }

    [Fact]
    public async Task Given_die_API_lehnt_ab_When_eine_Person_hinzugefuegt_wird_Then_steht_der_Grund_in_der_Meldung()
    {
        var api = Api();
        api.On($"POST /api/employees/{Marco.Id}/regulations/powers", new { detail = "Die Person ist ausgeschieden." }, System.Net.HttpStatusCode.BadRequest);
        var (_, table, _) = await Open(api);

        await table.Groups[1].Rows.Single().Candidates.Single(c => c.Name == "Marco Berger").AddCommand.ExecuteAsync(null);

        Assert.True(table.HasError);
        Assert.Null(table.StatusText);
    }
}
