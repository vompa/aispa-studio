using System.Net;
using System.Net.Http.Json;
using AispaStudio.Api.Sync;
using AispaStudio.Contracts;
using Npgsql;

namespace AispaStudio.Api.Tests;

/// <summary>Ä184, Slice 91 (ADR 0082): Verlauf von Kundendaten, Behandlungen, Dienstplan und Abwesenheiten sowie Benutzern und Rechten.</summary>
public sealed class EntityHistoryTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Given_Rezeption_aendert_Telefonnummer_When_Verlauf_der_Kundendaten_Then_alt_neu_Person_und_Zeit()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var guest = await spa.PostAsync<CustomerDto>("/api/customers", new CustomerInput("Lea", $"Tel{Guid.NewGuid():N}", null, "0664 111", null, null), Ct);
        var (lena, _) = await StaffAsync(spa, "Lena", StaffRole.Reception);
        using var client = lena;

        var before = DateTimeOffset.UtcNow.AddSeconds(-5);
        var changed = await lena.PutAsJsonAsync($"/api/customers/{guest.Id}",
            new CustomerInput(guest.FirstName, guest.LastName, null, "0664 222", null, null), TestSpa.Json, Ct);
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);

        var history = await lena.GetFromJsonAsync<List<HistoryEntry>>($"/api/customers/{guest.Id}/history", TestSpa.Json, Ct);
        Assert.Equal("Geändert von Lena: Telefon +43 664 111 → +43 664 222", history![0].Text);
        Assert.InRange(history[0].At, before, DateTimeOffset.UtcNow.AddSeconds(5));
        Assert.Equal("Angelegt von Test Person", history[^1].Text);
    }

    [Fact]
    public async Task Given_Anamnese_geaendert_When_Verlauf_geoeffnet_Then_nur_geaendert_von_ohne_Inhalt()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var (lena, lenaId) = await StaffAsync(spa, "Lena", StaffRole.Reception);
        using var client = lena;
        var recordId = Guid.NewGuid();
        await using (var db = await OpenAsync())
        {
            await ExecuteAsync(db, $"SELECT set_config('{ChangeHistory.UserSetting}', '{lenaId}', false), set_config('{ChangeHistory.ChannelSetting}', 'Team', false)");
            await ExecuteAsync(db, $"""
                INSERT INTO health."AnamnesisRecords" ("Id", "CustomerId", "FormVersion", "Channel", "SubmittedAt", "ConsentGivenAt", "Payload")
                VALUES ('{recordId}', '{spa.Customer.Id}', 'v1', 'Tablet', now(), now(), '\x01'::bytea)
                """);
            await ExecuteAsync(db, $"""UPDATE health."AnamnesisRecords" SET "FormVersion" = 'v2', "Payload" = '\x02'::bytea WHERE "Id" = '{recordId}'""");
        }

        var history = (await lena.GetFromJsonAsync<List<HistoryEntry>>($"/api/customers/{spa.Customer.Id}/history", TestSpa.Json, Ct))!;
        Assert.Equal(["Anamnese geändert von Lena", "Anamnese angelegt von Lena"], history.Take(2).Select(e => e.Text));
        Assert.DoesNotContain(history, e => e.Text.Contains("v2", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Given_Preis_einer_Behandlung_geaendert_When_Verlauf_Then_alter_und_neuer_Preis_mit_Person_und_Zeit()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var before = DateTimeOffset.UtcNow.AddSeconds(-5);
        _ = await spa.PutAsync<TreatmentDto>($"/api/treatments/{spa.MassageTreatment.Id}",
            new TreatmentInput("Massage", 50, 5, 10, 89m, [spa.Massage.Id], [spa.RoomA.Id, spa.RoomB.Id], []), Ct);

        var history = await spa.GetAsync<List<HistoryEntry>>($"/api/treatments/{spa.MassageTreatment.Id}/history", Ct);
        Assert.Equal("Geändert von Test Person: Preis 79,00 € → 89,00 €", history[0].Text);
        Assert.InRange(history[0].At, before, DateTimeOffset.UtcNow.AddSeconds(5));
        Assert.Equal("Angelegt von Test Person", history[^1].Text);
    }

    [Fact]
    public async Task Given_Urlaub_eingetragen_When_Verlauf_des_Dienstplans_Then_nur_Inhaberin_sieht_ihn_und_Rezeption_bekommt_403()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        _ = await spa.PutAsync<RosterDayResult>($"/api/locations/{spa.Location.Id}/roster/{spa.Ben.Id}/{TestSpa.Monday:yyyy-MM-dd}",
            new RosterDayInput([new RosterEntry(RosterKind.Vacation, null, null, null)], null), Ct);

        var history = await spa.GetAsync<List<HistoryEntry>>($"/api/employees/{spa.Ben.Id}/roster-history", Ct);
        Assert.Equal([$"Urlaub am {TestSpa.Monday:d.M.yyyy} eingetragen von Test Person"], history.Select(e => e.Text));

        var (lena, _) = await StaffAsync(spa, "Lena", StaffRole.Reception);
        using var client = lena;
        Assert.Equal(HttpStatusCode.Forbidden, (await lena.GetAsync($"/api/employees/{spa.Ben.Id}/roster-history", Ct)).StatusCode);
    }

    [Fact]
    public async Task Given_eine_Rezeptionistin_When_sie_den_Verlauf_von_Benutzern_und_Rechten_abruft_Then_403_und_die_Inhaberin_sieht_die_Aenderung()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var (lena, lenaId) = await StaffAsync(spa, "Lena", StaffRole.Reception);
        using var client = lena;

        Assert.Equal(HttpStatusCode.Forbidden, (await lena.GetAsync($"/api/staff/{lenaId}/history", Ct)).StatusCode);

        var changed = await spa.Client.PutAsJsonAsync($"/api/staff/{lenaId}",
            new StaffAccessInput(StaffRole.Manager, false, [spa.Location.Id], null, true), TestSpa.Json, Ct);
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);

        var history = await spa.GetAsync<List<HistoryEntry>>($"/api/staff/{lenaId}/history", Ct);
        Assert.Equal("Geändert von Test Person: Rolle Rezeption → Standortleitung", history[0].Text);
    }

    [Fact]
    public async Task Verlauf_eines_fremden_Datensatzes_ist_nicht_gefunden()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var other = await TestSpa.CreateAsync(fixture, Ct);

        Assert.Equal(HttpStatusCode.NotFound, (await other.Client.GetAsync($"/api/customers/{spa.Customer.Id}/history", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.Client.GetAsync($"/api/treatments/{spa.MassageTreatment.Id}/history", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.Client.GetAsync($"/api/employees/{spa.Ben.Id}/roster-history", Ct)).StatusCode);
    }

    /// <summary>Weiteres Konto im Spa mit eigenem Anzeigenamen; liefert den Client und die Benutzer-Id.</summary>
    private async Task<(HttpClient Client, string UserId)> StaffAsync(TestSpa spa, string name, StaffRole role)
    {
        var email = $"{name.ToLowerInvariant()}-{Guid.NewGuid():N}@aispa.test";
        using var client = fixture.CreateClient(spa.Cookie);
        var response = await client.PostAsJsonAsync("/api/auth/dev/register", new DevRegistration(email, name, role, [spa.Location.Id]), TestSpa.Json, Ct);
        response.EnsureSuccessStatusCode();
        await using var db = await OpenAsync();
        var userId = await ScalarAsync<string>(db, $"""SELECT "Id" FROM aispa."AspNetUsers" WHERE "Email" = '{email}'""");
        return (fixture.CreateClient(string.Join("; ", response.Headers.GetValues("Set-Cookie").Select(c => c.Split(';')[0]))), userId);
    }

    private async Task<NpgsqlConnection> OpenAsync()
    {
        var db = new NpgsqlConnection(fixture.ConnectionString);
        await db.OpenAsync(Ct);
        return db;
    }

    private static async Task ExecuteAsync(NpgsqlConnection db, string sql)
    {
        await using var command = new NpgsqlCommand(sql, db);
        await command.ExecuteNonQueryAsync(Ct);
    }

    private static async Task<T> ScalarAsync<T>(NpgsqlConnection db, string sql)
    {
        await using var command = new NpgsqlCommand(sql, db);
        return (T)(await command.ExecuteScalarAsync(Ct))!;
    }
}
