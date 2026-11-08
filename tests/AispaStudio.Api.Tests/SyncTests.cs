using System.Net.Http.Json;
using System.Security.Cryptography;
using AispaStudio.Api.Online;
using AispaStudio.Api.Sync;
using AispaStudio.Contracts;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Options;
using Npgsql;

namespace AispaStudio.Api.Tests;

/// <summary>
/// Der Abgleich liest nur unterhalb von <c>pg_snapshot_xmin</c>, und das gilt für den ganzen PostgreSQL-Server. Laufen andere Tests
/// gleichzeitig Transaktionen in ihren Datenbanken, kommt eine Änderung erst beim nächsten Lauf; die Zählungen hier gehen davon aus,
/// dass sie im selben Lauf kommt. Deshalb laufen diese Tests für sich.
/// </summary>
[CollectionDefinition(DisableParallelization = true)]
public sealed class SyncAlone;

/// <summary>Abgleich eines Einzelplatzes mit einer zentralen PostgreSQL (ADR 0041, Schritt 4b), mit zwei echten Datenbanken.</summary>
[Collection(typeof(SyncAlone))]
public sealed class SyncTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Einzelplatz_sendet_holt_loescht_und_schickt_nichts_zurueck()
    {
        var localDb = await fixture.CreateDatabaseAsync(Ct);
        var centralDb = await fixture.CreateDatabaseAsync(Ct);
        await using var local = fixture.Factory.WithWebHostBuilder(b =>
        {
            b.UseSetting("ConnectionStrings:aispadb", localDb);
            b.UseSetting("Hosting:Standalone", "true");
        });
        await using var central = fixture.Factory.WithWebHostBuilder(b => b.UseSetting("ConnectionStrings:aispadb", centralDb));
        _ = central.Server;

        using var anonymous = local.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        var setup = await anonymous.PostAsJsonAsync("/api/auth/standalone/setup",
            new StandaloneSetup("Spa am See", "AT", "Gmunden", "Eva Huber", "eva", "seeblick-2026"), TestSpa.Json, Ct);
        setup.EnsureSuccessStatusCode();
        using var team = local.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        team.DefaultRequestHeaders.Add("Cookie", string.Join("; ", setup.Headers.GetValues("Set-Cookie").Select(c => c.Split(';')[0])));
        var guest = await (await team.PostAsJsonAsync("/api/customers", new CustomerInput("Lea", "Abgleich", null, null, null, null), TestSpa.Json, Ct))
            .EnsureSuccessStatusCode().Content.ReadFromJsonAsync<CustomerDto>(TestSpa.Json, Ct);

        var runner = new SyncRunner(localDb, centralDb, "gmunden-empfang");

        // Erster Lauf: der ganze Einzelplatz geht an die zentrale Seite.
        var first = await runner.RunOnceAsync(Ct);
        Assert.True(first.Pushed >= 4, $"nur {first.Pushed} Zeilen gesendet");
        Assert.Equal(0, first.Conflicts);
        Assert.Equal("Abgleich", await ScalarAsync<string>(centralDb, $"""SELECT "LastName" FROM aispa."Customers" WHERE "Id" = '{guest!.Id}'"""));
        Assert.Equal("Spa am See", await ScalarAsync<string>(centralDb, """SELECT "Name" FROM aispa."Tenants" """));

        // Was zurückkommt, ist schon da: kein Echo.
        var quiet = await runner.RunOnceAsync(Ct);
        Assert.Equal(new SyncResult(0, 0, 0), quiet);

        // Änderung an der zentralen Seite kommt an, ohne wieder zurückgeschickt zu werden.
        await ExecuteAsync(centralDb, $"""UPDATE aispa."Customers" SET "LastName" = 'Zentrale' WHERE "Id" = '{guest.Id}'""");
        var pulled = await runner.RunOnceAsync(Ct);
        Assert.Equal(new SyncResult(0, 1, 0), pulled);
        Assert.Equal("Zentrale", await ScalarAsync<string>(localDb, $"""SELECT "LastName" FROM aispa."Customers" WHERE "Id" = '{guest.Id}'"""));
        Assert.Equal(new SyncResult(0, 0, 0), await runner.RunOnceAsync(Ct));

        // Lokal gelöscht: auch zentral weg.
        await ExecuteAsync(localDb, $"""DELETE FROM aispa."Customers" WHERE "Id" = '{guest.Id}'""");
        Assert.Equal(new SyncResult(1, 0, 0), await runner.RunOnceAsync(Ct));
        Assert.Equal(0L, await ScalarAsync<long>(centralDb, $"""SELECT count(*) FROM aispa."Customers" WHERE "Id" = '{guest.Id}'"""));

        // Gesendetes Protokoll wird lokal aufgeräumt.
        Assert.Equal(0L, await ScalarAsync<long>(localDb, "SELECT count(*) FROM sync.changes WHERE origin IS NULL"));
        Assert.Null(await ScalarAsync<string?>(localDb, "SELECT last_error FROM sync.peers"));
    }

    [Fact]
    public async Task Abgelehnte_Zeile_bleibt_lokal_und_die_Rezeption_sieht_und_erledigt_sie()
    {
        var localDb = await fixture.CreateDatabaseAsync(Ct);
        var centralDb = await fixture.CreateDatabaseAsync(Ct);
        await using var local = fixture.Factory.WithWebHostBuilder(b =>
        {
            b.UseSetting("ConnectionStrings:aispadb", localDb);
            b.UseSetting("Hosting:Standalone", "true");
            b.UseSetting("Sync:CentralConnectionString", centralDb);
            b.UseSetting("Sync:IntervalSeconds", "3600");
        });
        await using var central = fixture.Factory.WithWebHostBuilder(b => b.UseSetting("ConnectionStrings:aispadb", centralDb));
        _ = central.Server;

        // Eingerichtet wird, bevor die zentrale Datenbank eingetragen ist; mit ihr verbindet sich ein neuer PC statt einzurichten.
        await using (var beforeSync = fixture.Factory.WithWebHostBuilder(b =>
                     {
                         b.UseSetting("ConnectionStrings:aispadb", localDb);
                         b.UseSetting("Hosting:Standalone", "true");
                     }))
        {
            using var anonymous = beforeSync.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
            (await anonymous.PostAsJsonAsync("/api/auth/standalone/setup",
                new StandaloneSetup("Spa am See", "AT", "Gmunden", "Eva Huber", "eva", "seeblick-2026"), TestSpa.Json, Ct)).EnsureSuccessStatusCode();
        }

        using var login = local.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        var signedIn = await login.PostAsJsonAsync("/api/auth/standalone/login", new PasswordLogin("eva", "seeblick-2026"), TestSpa.Json, Ct);
        signedIn.EnsureSuccessStatusCode();
        using var team = local.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        team.DefaultRequestHeaders.Add("Cookie", string.Join("; ", signedIn.Headers.GetValues("Set-Cookie").Select(c => c.Split(';')[0])));

        var runner = new SyncRunner(localDb, centralDb, "gmunden-empfang");
        await runner.RunOnceAsync(Ct);
        var tenant = await ScalarAsync<Guid>(localDb, """SELECT "Id" FROM aispa."Tenants" """);

        // Zentral hat jemand dieselbe Qualifikation schon angelegt: der Name ist je Spa eindeutig.
        await ExecuteAsync(centralDb, $"""INSERT INTO aispa."Qualifications" ("Id", "Name", "TenantId") VALUES (gen_random_uuid(), 'Sauna', '{tenant}')""");
        (await team.PostAsJsonAsync("/api/qualifications", new QualificationInput("Sauna"), TestSpa.Json, Ct)).EnsureSuccessStatusCode();

        // Beide Seiten lehnen die Zeile der anderen ab; jede bleibt, wo sie ist.
        var result = await runner.RunOnceAsync(Ct);
        Assert.Equal(2, result.Conflicts);
        Assert.Equal(1L, await ScalarAsync<long>(localDb, """SELECT count(*) FROM aispa."Qualifications" WHERE "Name" = 'Sauna'"""));

        var status = await team.GetFromJsonAsync<SyncStatus>("/api/system/sync", TestSpa.Json, Ct);
        Assert.True(status!.Enabled);
        Assert.NotNull(status.LastRun);
        Assert.Null(status.LastError);
        Assert.Equal(2, status.Conflicts.Count);
        Assert.All(status.Conflicts, c => Assert.Equal("Qualifications", c.Table));
        Assert.Equal(["Hier abgelehnt", "Zentral abgelehnt"], status.Conflicts.Select(c => c.Reason.Split(':')[0]).Order());
        var conflict = status.Conflicts[0];

        Assert.Equal(global::System.Net.HttpStatusCode.NoContent,
            (await team.PostAsync($"/api/system/sync/conflicts/{conflict.Id}/resolve", null, Ct)).StatusCode);
        Assert.Single((await team.GetFromJsonAsync<SyncStatus>("/api/system/sync", TestSpa.Json, Ct))!.Conflicts);
        Assert.Equal(global::System.Net.HttpStatusCode.NotFound,
            (await team.PostAsync($"/api/system/sync/conflicts/{conflict.Id}/resolve", null, Ct)).StatusCode);
    }

    [Fact]
    public async Task Ohne_zentrale_Verbindung_bleibt_der_Fehler_vermerkt_und_nichts_geht_verloren()
    {
        var localDb = await fixture.CreateDatabaseAsync(Ct);
        await using var local = fixture.Factory.WithWebHostBuilder(b =>
        {
            b.UseSetting("ConnectionStrings:aispadb", localDb);
            b.UseSetting("Hosting:Standalone", "true");
        });
        using var anonymous = local.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        (await anonymous.PostAsJsonAsync("/api/auth/standalone/setup",
            new StandaloneSetup("Spa", "DE", "Berlin", "Ida", "ida", "sommer-2026"), TestSpa.Json, Ct)).EnsureSuccessStatusCode();

        var missing = new NpgsqlConnectionStringBuilder(localDb) { Database = "gibt_es_nicht" }.ConnectionString;
        await Assert.ThrowsAnyAsync<Exception>(() => new SyncRunner(localDb, missing, "berlin").RunOnceAsync(Ct));

        Assert.True(await ScalarAsync<long>(localDb, "SELECT count(*) FROM sync.changes") >= 3);
    }

    [Fact]
    public async Task Zeilen_ohne_eigenen_Mandanten_kommen_ueber_ihre_Verweise_beim_richtigen_Spa_an()
    {
        // Zugänge, Anamnesen und Zuordnungstabellen haben keine Spalte TenantId; der Mandant ergibt sich aus der Zeile, auf die sie verweisen.
        var localDb = await fixture.CreateDatabaseAsync(Ct);
        var centralDb = await fixture.CreateDatabaseAsync(Ct);
        await using var local = fixture.Factory.WithWebHostBuilder(b =>
        {
            b.UseSetting("ConnectionStrings:aispadb", localDb);
            b.UseSetting("Hosting:Standalone", "true");
        });
        await using var central = fixture.Factory.WithWebHostBuilder(b => b.UseSetting("ConnectionStrings:aispadb", centralDb));
        _ = central.Server;
        using var anonymous = local.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        var setup = await anonymous.PostAsJsonAsync("/api/auth/standalone/setup",
            new StandaloneSetup("Spa am See", "AT", "Gmunden", "Eva Huber", "eva", "seeblick-2026"), TestSpa.Json, Ct);
        setup.EnsureSuccessStatusCode();
        using var team = local.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        team.DefaultRequestHeaders.Add("Cookie", string.Join("; ", setup.Headers.GetValues("Set-Cookie").Select(c => c.Split(';')[0])));
        var guest = await (await team.PostAsJsonAsync("/api/customers", new CustomerInput("Lea", "Abgleich", null, null, null, null), TestSpa.Json, Ct))
            .EnsureSuccessStatusCode().Content.ReadFromJsonAsync<CustomerDto>(TestSpa.Json, Ct);
        var runner = new SyncRunner(localDb, centralDb, "gmunden");
        await runner.RunOnceAsync(Ct);
        var tenant = await ScalarAsync<Guid>(localDb, """SELECT "Id" FROM aispa."Tenants" """);

        // Der Zugang entsteht vor seiner Berechtigung: beim Lesen ergibt sich der Mandant trotzdem.
        var eva = await ScalarAsync<string>(centralDb, """SELECT "Id" FROM aispa."AspNetUsers" WHERE "UserName" = 'eva'""");
        Assert.Equal(tenant, await ScalarAsync<Guid>(centralDb, $"SELECT sync.key_tenant('aispa', 'AspNetUsers', jsonb_build_object('Id', '{eva}'))"));

        // Anamnese, an einer anderen Stelle ausgefüllt: kommt an; die eines fremden Gastes nicht.
        var mine = Guid.NewGuid();
        await ExecuteAsync(centralDb, $"""
            INSERT INTO health."AnamnesisRecords" ("Id", "CustomerId", "FormVersion", "Channel", "SubmittedAt", "ConsentGivenAt", "Payload")
            VALUES ('{mine}', '{guest!.Id}', 'v1', 'Tablet', now(), now(), '\x01'),
                   (gen_random_uuid(), gen_random_uuid(), 'v1', 'Tablet', now(), now(), '\x02')
            """);
        Assert.Equal(new SyncResult(0, 1, 0), await runner.RunOnceAsync(Ct));
        Assert.Equal(1L, await ScalarAsync<long>(localDb, $"""SELECT count(*) FROM health."AnamnesisRecords" WHERE "Id" = '{mine}'"""));
        Assert.Equal(1L, await ScalarAsync<long>(localDb, """SELECT count(*) FROM health."AnamnesisRecords" """));
    }

    [Fact]
    public async Task Stellen_lesen_die_Gesundheitsdaten_der_anderen_nur_mit_dem_Schluesselcode_des_Spas()
    {
        // ADR 0048: jede Stelle verschlüsselt mit eigenem Schlüssel und legt ihn, mit dem Code verschlüsselt, zentral ab.
        var localDb = await fixture.CreateDatabaseAsync(Ct);
        var otherDb = await fixture.CreateDatabaseAsync(Ct);
        var centralDb = await fixture.CreateDatabaseAsync(Ct);
        await using var local = fixture.Factory.WithWebHostBuilder(b =>
        {
            b.UseSetting("ConnectionStrings:aispadb", localDb);
            b.UseSetting("Hosting:Standalone", "true");
        });
        await using var central = fixture.Factory.WithWebHostBuilder(b => b.UseSetting("ConnectionStrings:aispadb", centralDb));
        _ = central.Server;
        using var anonymous = local.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        (await anonymous.PostAsJsonAsync("/api/auth/standalone/setup",
            new StandaloneSetup("Spa am See", "AT", "Gmunden", "Eva Huber", "eva", "seeblick-2026"), TestSpa.Json, Ct)).EnsureSuccessStatusCode();
        var tenant = await ScalarAsync<Guid>(localDb, """SELECT "Id" FROM aispa."Tenants" """);
        await ExecuteAsync(otherDb, ChangeLog.InstallSql);

        var code = KeySeal.NewCode();
        var (gmunden, gmundenKeys) = Site("lokal-gmunden", code);
        var (ischl, ischlKeys) = Site("lokal-ischl", code.ToLowerInvariant().Replace("-", " ", StringComparison.Ordinal));
        var record = Guid.NewGuid();
        var anamnesis = gmunden.Protect("Allergie: Nüsse", record);
        Assert.ThrowsAny<Exception>(() => ischl.Unprotect(anamnesis, record));

        // Erste Stelle: ganzer Bestand und ihr Schlüssel gehen an die zentrale Seite; der Code selbst liegt nirgends in der Datenbank.
        await new SyncRunner(localDb, centralDb, "gmunden", gmundenKeys).RunOnceAsync(Ct);
        Assert.Equal(1L, await ScalarAsync<long>(centralDb, "SELECT count(*) FROM sync.health_keys"));
        Assert.DoesNotContain(code.Replace("-", "", StringComparison.Ordinal),
            await ScalarAsync<string>(centralDb, "SELECT sealed FROM sync.health_keys"), StringComparison.Ordinal);

        // Zweite Stelle mit demselben Code: liest die Anamnese der ersten, ihr Schlüssel liegt danach auch lokal und zentral.
        await using (var other = new NpgsqlConnection(otherDb))
        await using (var hub = new NpgsqlConnection(centralDb))
        {
            await other.OpenAsync(Ct);
            await hub.OpenAsync(Ct);
            await ischlKeys.ExchangeAsync(other, hub, tenant, Ct);

            Assert.Equal("Allergie: Nüsse", ischl.Unprotect(anamnesis, record));
            Assert.Equal(2L, await ScalarAsync<long>(centralDb, "SELECT count(*) FROM sync.health_keys"));
            Assert.Equal(2L, await ScalarAsync<long>(otherDb, "SELECT count(*) FROM sync.health_keys"));

            // Falscher Code: der Abgleich bricht ab, bevor etwas übertragen wird.
            var (_, wrongKeys) = Site("lokal-linz", KeySeal.NewCode());
            var wrong = await Assert.ThrowsAsync<InvalidOperationException>(() => wrongKeys.ExchangeAsync(other, hub, tenant, Ct));
            Assert.Equal(HealthKeyExchange.WrongCode, wrong.Message);
            Assert.Equal(2L, await ScalarAsync<long>(centralDb, "SELECT count(*) FROM sync.health_keys"));
        }

        // Die erste Stelle liest beim nächsten Lauf auch, was die zweite verschlüsselt hat.
        var reply = ischl.Protect("Schwanger: nein", record);
        await new SyncRunner(localDb, centralDb, "gmunden", gmundenKeys).RunOnceAsync(Ct);
        Assert.Equal("Schwanger: nein", gmunden.Unprotect(reply, record));

        var (_, wrongSite) = Site("lokal-gmunden", KeySeal.NewCode());
        await Assert.ThrowsAsync<InvalidOperationException>(() => new SyncRunner(localDb, centralDb, "gmunden", wrongSite).RunOnceAsync(Ct));
        Assert.Equal(HealthKeyExchange.WrongCode, await ScalarAsync<string>(localDb, "SELECT last_error FROM sync.peers"));
    }

    [Fact]
    public async Task Neuer_PC_verbindet_sich_mit_dem_Schluesselcode_und_das_Team_meldet_sich_wie_gewohnt_an()
    {
        var firstDb = await fixture.CreateDatabaseAsync(Ct);
        var newDb = await fixture.CreateDatabaseAsync(Ct);
        var centralDb = await fixture.CreateDatabaseAsync(Ct);
        await using var first = fixture.Factory.WithWebHostBuilder(b =>
        {
            b.UseSetting("ConnectionStrings:aispadb", firstDb);
            b.UseSetting("Hosting:Standalone", "true");
        });
        await using var central = fixture.Factory.WithWebHostBuilder(b => b.UseSetting("ConnectionStrings:aispadb", centralDb));
        _ = central.Server;
        using (var anonymous = first.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false }))
        {
            (await anonymous.PostAsJsonAsync("/api/auth/standalone/setup",
                new StandaloneSetup("Spa am See", "AT", "Gmunden", "Eva Huber", "eva", "seeblick-2026"), TestSpa.Json, Ct)).EnsureSuccessStatusCode();
        }

        var code = KeySeal.NewCode();
        await new SyncRunner(firstDb, centralDb, "gmunden", Site("lokal-gmunden", code).Keys).RunOnceAsync(Ct);

        // Neuer PC: zentrale Datenbank eingetragen, noch kein Spa. Neu einrichten geht nicht, er wartet auf den Abgleich.
        await using var newPc = fixture.Factory.WithWebHostBuilder(b =>
        {
            b.UseSetting("ConnectionStrings:aispadb", newDb);
            b.UseSetting("Hosting:Standalone", "true");
            b.UseSetting("Sync:CentralConnectionString", centralDb);
            b.UseSetting("Sync:IntervalSeconds", "3600");
        });
        using var client = newPc.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        var state = await client.GetFromJsonAsync<StandaloneSetupState>("/api/auth/standalone/setup", TestSpa.Json, Ct);
        Assert.Equal(new StandaloneSetupState(true, true, null), state! with { JoinError = null });
        var refused = await client.PostAsJsonAsync("/api/auth/standalone/setup",
            new StandaloneSetup("Zweites Spa", "AT", "Ischl", "Ida", "ida", "sommer-2026"), TestSpa.Json, Ct);
        Assert.Equal(global::System.Net.HttpStatusCode.Conflict, refused.StatusCode);

        // Falscher Code: kein Spa, der Fehler steht beim Verbinden da.
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new SyncRunner(newDb, centralDb, "ischl", Site("lokal-ischl", KeySeal.NewCode()).Keys).RunOnceAsync(Ct));
        state = await client.GetFromJsonAsync<StandaloneSetupState>("/api/auth/standalone/setup", TestSpa.Json, Ct);
        Assert.Equal(HealthKeyExchange.NoSpa, state!.JoinError);

        // Richtiger Code: der ganze Bestand kommt, Eva meldet sich mit ihrem Passwort an.
        var joined = await new SyncRunner(newDb, centralDb, "ischl", Site("lokal-ischl", code).Keys).RunOnceAsync(Ct);
        Assert.True(joined.Pulled >= 4, $"nur {joined.Pulled} Zeilen geholt");
        Assert.Equal(new StandaloneSetupState(false), await client.GetFromJsonAsync<StandaloneSetupState>("/api/auth/standalone/setup", TestSpa.Json, Ct));
        var login = await client.PostAsJsonAsync("/api/auth/standalone/login", new PasswordLogin("eva", "seeblick-2026"), TestSpa.Json, Ct);
        login.EnsureSuccessStatusCode();
        var me = await login.Content.ReadFromJsonAsync<CurrentUser>(TestSpa.Json, Ct);
        Assert.Equal("Spa am See", me!.Access!.TenantName);
    }

    [Fact]
    public async Task Nach_der_Frist_raeumt_der_Abgleich_das_zentrale_Protokoll_auf_und_ein_neuer_PC_holt_den_ganzen_Bestand()
    {
        var firstDb = await fixture.CreateDatabaseAsync(Ct);
        var newDb = await fixture.CreateDatabaseAsync(Ct);
        var centralDb = await fixture.CreateDatabaseAsync(Ct);
        await using var first = fixture.Factory.WithWebHostBuilder(b =>
        {
            b.UseSetting("ConnectionStrings:aispadb", firstDb);
            b.UseSetting("Hosting:Standalone", "true");
        });
        await using var central = fixture.Factory.WithWebHostBuilder(b => b.UseSetting("ConnectionStrings:aispadb", centralDb));
        _ = central.Server;
        using (var anonymous = first.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false }))
        {
            (await anonymous.PostAsJsonAsync("/api/auth/standalone/setup",
                new StandaloneSetup("Spa am See", "AT", "Gmunden", "Eva Huber", "eva", "seeblick-2026"), TestSpa.Json, Ct)).EnsureSuccessStatusCode();
        }

        var code = KeySeal.NewCode();
        var gmunden = new SyncRunner(firstDb, centralDb, "gmunden", Site("lokal-gmunden", code).Keys);
        await gmunden.RunOnceAsync(Ct);
        Assert.Equal(0L, await ScalarAsync<long>(centralDb, "SELECT count(*) FROM sync.pruned"));

        // Innerhalb der Frist bleibt alles; danach räumt der nächste Lauf auf.
        await ExecuteAsync(centralDb, "UPDATE sync.changes SET changed_at = now() - interval '91 days'");
        await ExecuteAsync(firstDb, """UPDATE aispa."Tenants" SET "Name" = 'Spa am Traunsee'""");
        await gmunden.RunOnceAsync(Ct);
        Assert.Equal(1L, await ScalarAsync<long>(centralDb, "SELECT count(*) FROM sync.pruned"));
        Assert.Equal(1L, await ScalarAsync<long>(centralDb, "SELECT count(*) FROM sync.changes"));

        // Der neue PC findet Spa, Zugänge und Berechtigungen nicht mehr im Protokoll: er holt den ganzen Bestand.
        await using var newPc = fixture.Factory.WithWebHostBuilder(b =>
        {
            b.UseSetting("ConnectionStrings:aispadb", newDb);
            b.UseSetting("Hosting:Standalone", "true");
        });
        _ = newPc.Server;
        var ischl = new SyncRunner(newDb, centralDb, "ischl", Site("lokal-ischl", code).Keys);
        var joined = await ischl.RunOnceAsync(Ct);
        Assert.True(joined.Pulled >= 4, $"nur {joined.Pulled} Zeilen geholt");
        Assert.Equal("Spa am Traunsee", await ScalarAsync<string>(newDb, """SELECT "Name" FROM aispa."Tenants" """));
        Assert.Equal(1L, await ScalarAsync<long>(newDb, """SELECT count(*) FROM aispa."AspNetUsers" WHERE "UserName" = 'eva'"""));
        Assert.Equal(1L, await ScalarAsync<long>(newDb, """SELECT count(*) FROM aispa."StaffAccess" """));
        Assert.Equal(1L, await ScalarAsync<long>(newDb, """SELECT count(*) FROM aispa."Locations" """));

        // Danach wieder nur das Protokoll.
        Assert.Equal(new SyncResult(0, 0, 0), await ischl.RunOnceAsync(Ct));
    }

    private static (HealthDataProtector Protector, HealthKeyExchange Keys) Site(string keyId, string code)
    {
        var options = new HealthDataOptions
        {
            CurrentKeyId = keyId,
            Keys = { [keyId] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)) },
            ShareCode = code,
        };
        var ring = new HealthKeyRing();
        return (new HealthDataProtector(Options.Create(options), ring), HealthKeyExchange.From(options, ring)!);
    }

    private static async Task ExecuteAsync(string connectionString, string sql)
    {
        await using var db = new NpgsqlConnection(connectionString);
        await db.OpenAsync(Ct);
        await using var command = new NpgsqlCommand(sql, db);
        await command.ExecuteNonQueryAsync(Ct);
    }

    private static async Task<T> ScalarAsync<T>(string connectionString, string sql)
    {
        await using var db = new NpgsqlConnection(connectionString);
        await db.OpenAsync(Ct);
        await using var command = new NpgsqlCommand(sql, db);
        var value = await command.ExecuteScalarAsync(Ct);
        return value is DBNull ? default! : (T)value!;
    }
}
