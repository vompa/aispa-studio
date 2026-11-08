using System.Net;
using System.Net.Http.Json;
using AispaStudio.Api.Data;
using AispaStudio.Api.Demo;
using AispaStudio.Contracts;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace AispaStudio.Api.Tests;

public sealed class StartupStateTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private sealed class ManualClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 10, 20, 8, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => Now;
    }

    private WebApplicationFactory<Program> Host(string connection, bool seed, string? mode = null) => fixture.Factory.WithWebHostBuilder(b =>
    {
        b.UseSetting("ConnectionStrings:aispadb", connection);
        b.UseSetting("Database:SeedDemoData", seed ? "true" : "false");
        // Klein halten: es geht um den Ablauf, nicht um den Umfang.
        b.UseSetting("DemoData:WeeksBack", "1");
        b.UseSetting("DemoData:WeeksAhead", "1");
        if (mode is not null)
        {
            b.UseSetting("DemoData:Mode", mode);
        }
    });

    private static async Task<SystemStatus> StatusAsync(HttpClient client) =>
        (await client.GetFromJsonAsync<SystemStatus>("/api/system/status", TestSpa.Json, Ct))!;

    [Fact]
    public void Zustand_ohne_Migration_und_Beispieldaten_ist_von_Anfang_an_bereit()
    {
        Assert.Equal(new StartupInfo(StartupPhase.Ready, "Bereit"), new StartupState(TimeProvider.System).Snapshot());
    }

    [Fact]
    public void Zustand_nennt_Phase_und_Sekunden_seit_dem_Beginn()
    {
        var clock = new ManualClock();
        var state = new StartupState(clock);

        state.Begin(StartupPhase.Migrating);
        clock.Now = clock.Now.AddSeconds(3);
        var migrating = state.Snapshot();
        state.Begin(StartupPhase.Seeding);
        clock.Now = clock.Now.AddSeconds(42);
        var seeding = state.Snapshot();
        state.Fail("kaputt");
        var failed = state.Snapshot();
        state.Complete();

        Assert.Equal(new StartupInfo(StartupPhase.Migrating, "Datenbank wird migriert (seit 3 s)"), migrating);
        Assert.Equal(new StartupInfo(StartupPhase.Seeding, "Beispiel-Spa wird eingerichtet (seit 42 s)"), seeding);
        Assert.Equal(new StartupInfo(StartupPhase.Failed, "kaputt"), failed);
        Assert.Equal(StartupPhase.Ready, state.Snapshot().Phase);
    }

    [Fact]
    public async Task Hintergrundjobs_warten_bis_der_Start_bereit_ist_und_nach_einem_Fehlschlag_weiter()
    {
        var state = new StartupState(TimeProvider.System);

        Assert.True(state.WaitUntilReadyAsync(Ct).IsCompletedSuccessfully, "von Anfang an bereit");

        state.Begin(StartupPhase.Seeding);
        var waiting = state.WaitUntilReadyAsync(Ct);
        await Task.Delay(50, Ct);
        Assert.False(waiting.IsCompleted);

        state.Fail("kaputt");
        await Task.Delay(50, Ct);
        Assert.False(waiting.IsCompleted, "unvollständige Daten: weiter warten");

        state.Complete();
        Assert.True(await waiting.WaitAsync(TimeSpan.FromSeconds(5), Ct));
    }

    [Fact]
    public async Task Ohne_Beispieldaten_ist_die_API_bereit_und_der_Health_Check_gesund()
    {
        using var client = fixture.CreateClient();

        var status = await StatusAsync(client);
        var health = await client.GetAsync("/health", Ct);

        Assert.Equal(StartupPhase.Ready, status.Startup?.Phase);
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
        Assert.Contains("startup: Healthy", await health.Content.ReadAsStringAsync(Ct), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Einspielen_blockiert_den_Start_nicht_meldet_den_Stand_und_wird_beim_naechsten_Start_uebersprungen()
    {
        var connection = await fixture.CreateDatabaseAsync(Ct);
        // Über die Dienste (langsam), damit der Zustand „wird eingerichtet“ sicher zu sehen ist; im Speicher ist es oft schon fertig.
        await using (var host = Host(connection, seed: true, mode: "Services"))
        {
            using var client = host.CreateClient();

            // Die API antwortet schon, das Einspielen läuft im Hintergrund; /alive bleibt gesund, /health nicht.
            var status = await StatusAsync(client);
            Assert.Equal(StartupPhase.Seeding, status.Startup?.Phase);
            Assert.StartsWith("Beispiel-Spa wird eingerichtet (seit ", status.Startup?.Text, StringComparison.Ordinal);
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/alive", Ct)).StatusCode);
            var health = await client.GetAsync("/health", Ct);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, health.StatusCode);
            Assert.Contains("startup: Unhealthy - Beispiel-Spa wird eingerichtet", await health.Content.ReadAsStringAsync(Ct), StringComparison.Ordinal);

            await WaitForReadyAsync(client);
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health", Ct)).StatusCode);
            var accounts = await client.GetFromJsonAsync<List<DevAccount>>("/api/auth/dev/accounts", TestSpa.Json, Ct);
            Assert.NotEmpty(accounts!);
        }

        // Zweiter Start auf derselben Datenbank: vollständig, also gleich bereit und nichts Neues.
        await using var again = Host(connection, seed: true);
        using var second = again.CreateClient();
        Assert.Equal(StartupPhase.Ready, (await StatusAsync(second)).Startup?.Phase);
        Assert.Equal(DemoDataStatus.Complete, await DemoSeeder.InspectAsync(again.Services, Ct));
    }

    [Fact]
    public async Task Teilstand_eines_abgebrochenen_Einspielens_wird_gemeldet_statt_uebersprungen_und_nichts_geloescht()
    {
        var connection = await fixture.CreateDatabaseAsync(Ct);
        await using (var empty = Host(connection, seed: false))
        {
            using var scope = empty.Services.CreateAsyncScope();
            _ = await Scheduling.DemoData.SeedAsync(scope.ServiceProvider, new DemoClock(TimeProvider.System.GetUtcNow()), Ct);
            Assert.Equal(DemoDataStatus.Incomplete, await DemoSeeder.InspectAsync(empty.Services, Ct));
        }

        await using var host = Host(connection, seed: true);
        using var client = host.CreateClient();

        var status = await StatusAsync(client);
        var health = await client.GetAsync("/health", Ct);

        Assert.Equal(new StartupInfo(StartupPhase.Failed, DemoSeeder.IncompleteMessage), status.Startup);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, health.StatusCode);
        Assert.Contains("aispa-postgres-data", await health.Content.ReadAsStringAsync(Ct), StringComparison.Ordinal);
        Assert.Equal(DemoDataStatus.Incomplete, await DemoSeeder.InspectAsync(host.Services, Ct));
    }

    [Fact]
    public async Task Stopp_waehrend_des_Einspielens_beendet_die_API_sauber()
    {
        var connection = await fixture.CreateDatabaseAsync(Ct);
        // Über die Dienste, nicht im Speicher: der schnelle Weg ist auf flotten Rechnern schon fertig, bevor der erste Abruf kommt.
        var host = Host(connection, seed: true, mode: "Services");
        using (var client = host.CreateClient())
        {
            Assert.Equal(StartupPhase.Seeding, (await StatusAsync(client)).Startup?.Phase);
        }

        var stopping = host.DisposeAsync().AsTask();

        Assert.True(await Task.WhenAny(stopping, Task.Delay(TimeSpan.FromSeconds(60), Ct)) == stopping, "API stoppt nicht");
        await stopping;
        await using var check = Host(connection, seed: false);
        Assert.NotEqual(DemoDataStatus.Complete, await DemoSeeder.InspectAsync(check.Services, Ct));
    }

    private static async Task WaitForReadyAsync(HttpClient client)
    {
        // Der Weg über die Dienste ist der langsame (Messung Runde 5b: 355.000 Datenbankbefehle statt 250.000 bis 278.000 in demo-daten-messung.md);
        // allein dauert das Einspielen hier gut 3 Minuten, unter der Last der Gesamtsuite 6 bis 7. Die Frist soll nur ein Hängen erkennen, nicht die Zeit prüfen.
        var deadline = DateTimeOffset.UtcNow.AddMinutes(12);
        while ((await StatusAsync(client)).Startup?.Phase == StartupPhase.Seeding)
        {
            Assert.True(DateTimeOffset.UtcNow < deadline, "Einspielen dauert zu lange");
            await Task.Delay(TimeSpan.FromMilliseconds(500), Ct);
        }

        Assert.Equal(StartupPhase.Ready, (await StatusAsync(client)).Startup?.Phase);
    }
}
