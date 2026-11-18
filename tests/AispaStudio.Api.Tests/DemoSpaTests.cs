using System.Net;
using System.Net.Http.Json;
using AispaStudio.Api.Demo;
using AispaStudio.Api.System;
using AispaStudio.Contracts;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

namespace AispaStudio.Api.Tests;

/// <summary>
/// Beispiel-Spa in eigener Datenbank (ADR 0100). Die Datenbank heißt immer <c>aispademo</c>, deshalb laufen diese Tests nacheinander
/// in einer Klasse und räumen sie vorher und nachher weg.
/// </summary>
public sealed class DemoSpaTests(ApiFixture fixture) : IAsyncLifetime
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => await DemoSpa.DropAsync(fixture.ConnectionString, Ct);

    public async ValueTask DisposeAsync() => await DemoSpa.DropAsync(fixture.ConnectionString, CancellationToken.None);

    private static HttpClient Client(WebApplicationFactory<Program> host, string? cookie = null)
    {
        var client = host.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        if (cookie is not null)
        {
            client.DefaultRequestHeaders.Add("Cookie", cookie);
        }

        return client;
    }

    private static async Task<string> SignInAsync(HttpClient anonymous, string email)
    {
        var response = await anonymous.PostAsJsonAsync("/api/auth/standalone/login",
            new PasswordLogin(email, DemoAccounts.Password), TestSpa.Json, Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return string.Join("; ", response.Headers.GetValues("Set-Cookie").Select(c => c.Split(';')[0]));
    }

    private static string ServerConnection(string connection) =>
        new NpgsqlConnectionStringBuilder(connection) { Database = "postgres", Pooling = false }.ConnectionString;

    private async Task<bool> DatabaseExistsAsync(string name)
    {
        await using var server = new NpgsqlConnection(ServerConnection(fixture.ConnectionString));
        await server.OpenAsync(Ct);
        await using var query = new NpgsqlCommand("SELECT 1 FROM pg_database WHERE datname = @name", server);
        query.Parameters.AddWithValue("name", name);
        return await query.ExecuteScalarAsync(Ct) is not null;
    }

    private static async Task<long> TableCountAsync(string connection)
    {
        var plain = new NpgsqlConnectionStringBuilder(connection) { Pooling = false }.ConnectionString;
        await using var db = new NpgsqlConnection(plain);
        await db.OpenAsync(Ct);
        await using var query = new NpgsqlCommand(
            "SELECT count(*) FROM information_schema.tables WHERE table_schema NOT IN ('pg_catalog', 'information_schema')", db);
        return (long)(await query.ExecuteScalarAsync(Ct))!;
    }

    private static async Task WaitUntilAsync(Func<Task<bool>> condition, string what)
    {
        var deadline = DateTime.UtcNow.AddSeconds(120);
        while (!await condition())
        {
            Assert.True(DateTime.UtcNow < deadline, $"Zeitüberschreitung: {what}");
            await Task.Delay(300, Ct);
        }
    }

    [Fact]
    public async Task Beispiel_Spa_liegt_in_aispademo_die_Ausgangsdatenbank_bleibt_leer_und_Loeschen_entfernt_sie()
    {
        var original = await fixture.CreateDatabaseAsync(Ct);
        await using var host = fixture.Factory.WithWebHostBuilder(b =>
        {
            b.UseSetting("ConnectionStrings:aispadb", original);
            b.UseSetting("Hosting:Standalone", "true");
            b.UseSetting("Database:SeedDemoData", "false");
            b.UseSetting("Demo:Enabled", "true");
            b.UseSetting("DemoData:WeeksBack", "1");
            b.UseSetting("DemoData:WeeksAhead", "1");
        });
        using var anonymous = Client(host);

        await WaitUntilAsync(async () =>
            (await anonymous.GetFromJsonAsync<SystemStatus>("/api/system/status", TestSpa.Json, Ct))!.Startup is { Phase: StartupPhase.Ready },
            "Beispieldaten angelegt");

        // Die Daten liegen in aispademo; die Datenbank, auf die die Verbindung zeigt, hat keine einzige Tabelle.
        Assert.True(await DatabaseExistsAsync(DemoSpa.DatabaseName));
        Assert.Equal(0, await TableCountAsync(original));
        Assert.True(await TableCountAsync(DemoSpa.ConnectionFor(original)) > 0);

        var modules = await anonymous.GetFromJsonAsync<SystemModules>("/api/system/modules", TestSpa.Json, Ct);
        Assert.True(modules!.Demo);

        // Alle Beispielkonten haben das Passwort „demo“; ein falsches wird abgelehnt.
        var wrong = await anonymous.PostAsJsonAsync("/api/auth/standalone/login",
            new PasswordLogin(DemoAccounts.Email("inhaberin"), "falsch"), TestSpa.Json, Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        using var reception = Client(host, await SignInAsync(anonymous, DemoAccounts.Email("empfang.wien")));
        using var therapist = Client(host, await SignInAsync(anonymous, DemoAccounts.EmployeeEmail("Lena Huber")));
        using var owner = Client(host, await SignInAsync(anonymous, DemoAccounts.Email("inhaberin")));
        Assert.Equal(StaffRole.Owner, (await owner.GetFromJsonAsync<CurrentUser>("/api/auth/me", TestSpa.Json, Ct))!.Access!.Role);

        // Ohne das Recht, Standorte zu verwalten: 403, die Datenbank bleibt.
        Assert.Equal(HttpStatusCode.Forbidden, (await reception.DeleteAsync("/api/system/demo", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await therapist.DeleteAsync("/api/system/demo", Ct)).StatusCode);
        Assert.True(await DatabaseExistsAsync(DemoSpa.DatabaseName));

        var deleted = await owner.DeleteAsync("/api/system/demo", Ct);
        Assert.Equal(HttpStatusCode.Accepted, deleted.StatusCode);
        await WaitUntilAsync(async () => !await DatabaseExistsAsync(DemoSpa.DatabaseName), "Datenbank gelöscht");
    }

    [Fact]
    public async Task Ohne_Beispiel_Spa_meldet_modules_Demo_false_und_Loeschen_ist_404()
    {
        using var owner = await TestUsers.ClientAsync(fixture, Ct);

        var modules = await owner.GetFromJsonAsync<SystemModules>("/api/system/modules", TestSpa.Json, Ct);
        var response = await owner.DeleteAsync("/api/system/demo", Ct);

        Assert.False(modules!.Demo);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("Kein Beispiel-Spa", await response.Content.ReadAsStringAsync(Ct), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Ohne_Demo_Einstellung_bekommen_die_Beispielkonten_kein_Passwort()
    {
        // Wie in der Entwicklung und den Tests: Beispieldaten ohne Demo:Enabled, Passwort-Anmeldung gelingt nicht.
        var connection = await fixture.CreateDatabaseAsync(Ct);
        await using var host = fixture.Factory.WithWebHostBuilder(b =>
        {
            b.UseSetting("ConnectionStrings:aispadb", connection);
            b.UseSetting("Hosting:Standalone", "true");
        });
        _ = host.Server;

        await DemoSeeder.SeedAsync(host.Services, TimeProvider.System.GetUtcNow(), new DemoDataOptions { WeeksBack = 1, WeeksAhead = 1 },
            NullLogger.Instance, Ct);

        using var anonymous = Client(host);
        var login = await anonymous.PostAsJsonAsync("/api/auth/standalone/login",
            new PasswordLogin(DemoAccounts.Email("inhaberin"), DemoAccounts.Password), TestSpa.Json, Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
    }
}
