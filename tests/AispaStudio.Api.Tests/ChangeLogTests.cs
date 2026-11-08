using AispaStudio.Api.Sync;
using AispaStudio.Contracts;
using Npgsql;

namespace AispaStudio.Api.Tests;

/// <summary>
/// Änderungsprotokoll für den Abgleich mit einer zentralen Datenbank (ADR 0041, Schritt 4). Allein, weil <c>sync.attach_all()</c>
/// Trigger an allen Tabellen der gemeinsamen Testdatenbank anlegt und sich sonst mit schreibenden Tests verklemmen kann.
/// </summary>
[Collection(typeof(SyncAlone))]
public sealed class ChangeLogTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Jede_Fachtabelle_mit_Primaerschluessel_hat_den_Trigger_und_aufschiebbare_Fremdschluessel()
    {
        await using var db = await OpenAsync();

        var missing = await ListAsync(db, $"""
            SELECT ns.nspname || '.' || c.relname
              FROM pg_class c
              JOIN pg_namespace ns ON ns.oid = c.relnamespace
             WHERE c.relkind = 'r' AND ns.nspname IN ('aispa', 'online', 'health') AND c.relname <> '__EFMigrationsHistory'
               AND EXISTS (SELECT 1 FROM pg_index i WHERE i.indrelid = c.oid AND i.indisprimary)
               AND NOT EXISTS (SELECT 1 FROM pg_trigger t WHERE t.tgrelid = c.oid AND t.tgname = 'aispa_sync_log')
            """);
        Assert.Empty(missing);

        var fixedKeys = await ListAsync(db, """
            SELECT ns.nspname || '.' || con.conname
              FROM pg_constraint con
              JOIN pg_class c ON c.oid = con.conrelid
              JOIN pg_namespace ns ON ns.oid = c.relnamespace
             WHERE con.contype = 'f' AND NOT con.condeferrable AND ns.nspname IN ('aispa', 'online', 'health')
            """);
        Assert.Empty(fixedKeys);

        Assert.True(await ScalarAsync<long>(db, """
            SELECT count(*) FROM pg_trigger t JOIN pg_class c ON c.oid = t.tgrelid
             WHERE t.tgname = 'aispa_sync_log' AND c.relname = 'Customers'
            """) == 1);
    }

    [Fact]
    public async Task Neue_geaenderte_und_geloeschte_Kundin_steht_mit_Mandant_und_Schluessel_im_Protokoll()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var guest = await spa.PostAsync<CustomerDto>("/api/customers", new CustomerInput("Lea", "Protokoll", null, null, null, null), Ct);

        await using var db = await OpenAsync();
        var key = $$"""{"Id": "{{guest.Id}}"}""";
        var insert = await ListAsync(db, $"""
            SELECT operation || '|' || coalesce(tenant_id::text, '') || '|' || coalesce(origin, '')
              FROM sync.changes WHERE table_schema = 'aispa' AND table_name = 'Customers' AND row_key = '{key}'::jsonb
             ORDER BY seq
            """);
        Assert.Equal([$"I|{spa.TenantId}|"], insert);

        // Was der Abgleich selbst einspielt, trägt seine Herkunft und wird nicht zurückgeschickt.
        await using (var tx = await db.BeginTransactionAsync(Ct))
        {
            await ExecuteAsync(db, $"SELECT set_config('{ChangeLog.OriginSetting}', 'zentrale', true)");
            await ExecuteAsync(db, $"""UPDATE aispa."Customers" SET "LastName" = 'Abgleich' WHERE "Id" = '{guest.Id}'""");
            await tx.CommitAsync(Ct);
        }

        await ExecuteAsync(db, $"""DELETE FROM aispa."Customers" WHERE "Id" = '{guest.Id}'""");

        var all = await ListAsync(db, $"""
            SELECT operation || '|' || coalesce(origin, '')
              FROM sync.changes WHERE table_name = 'Customers' AND row_key = '{key}'::jsonb ORDER BY seq
            """);
        Assert.Equal(["I|", "U|zentrale", "D|"], all);
    }

    [Fact]
    public async Task Weitere_Mailadresse_eines_Kunden_steht_mit_Mandant_im_Protokoll()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var address = $"weitere-{Guid.NewGuid():N}@example.at";
        var added = await spa.PostAsync<List<CustomerEmailDto>>($"/api/customers/{spa.Customer.Id}/emails", new CustomerEmailInput(address), Ct);

        await using var db = await OpenAsync();
        var key = $$"""{"Id": "{{added.Single().Id}}"}""";
        var rows = await ListAsync(db, $"""
            SELECT operation || '|' || coalesce(tenant_id::text, '')
              FROM sync.changes WHERE table_schema = 'aispa' AND table_name = 'CustomerEmails' AND row_key = '{key}'::jsonb ORDER BY seq
            """);
        Assert.Equal([$"I|{spa.TenantId}"], rows);
    }

    [Fact]
    public async Task Einrichten_ist_wiederholbar()
    {
        await using var db = await OpenAsync();
        var before = await ScalarAsync<int>(db, "SELECT sync.attach_all()");
        var again = await ScalarAsync<int>(db, "SELECT sync.attach_all()");

        Assert.True(before > 50, $"nur {before} Tabellen");
        Assert.Equal(before, again);
    }

    [Fact]
    public async Task Vorhandene_Trigger_anzuhaengen_wartet_nicht_auf_offene_Schreibtransaktionen()
    {
        await using var writer = await OpenAsync();
        await using var open = await writer.BeginTransactionAsync(Ct);
        await ExecuteAsync(writer, """LOCK TABLE aispa."Customers" IN ROW EXCLUSIVE MODE""");

        await using var db = await OpenAsync();
        await ExecuteAsync(db, "SET lock_timeout = '2s'");

        Assert.True(await ScalarAsync<int>(db, "SELECT sync.attach_all()") > 50);
        Assert.True(await ScalarAsync<int>(db, ChangeHistory.AttachSql) > 0);
    }

    [Fact]
    public async Task Neue_Tabelle_bekommt_den_Trigger_beim_naechsten_Anhaengen()
    {
        await using var db = await OpenAsync();
        await using var rolledBack = await db.BeginTransactionAsync(Ct);
        await ExecuteAsync(db, """CREATE TABLE aispa."TriggerProbe" ("Id" integer PRIMARY KEY)""");

        await ExecuteAsync(db, "SELECT sync.attach_all()");

        Assert.Equal(1, await ScalarAsync<long>(db, """
            SELECT count(*) FROM pg_trigger t JOIN pg_class c ON c.oid = t.tgrelid
             WHERE t.tgname = 'aispa_sync_log' AND c.relname = 'TriggerProbe'
            """));
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

    private static async Task<List<string>> ListAsync(NpgsqlConnection db, string sql)
    {
        await using var command = new NpgsqlCommand(sql, db);
        await using var reader = await command.ExecuteReaderAsync(Ct);
        var rows = new List<string>();
        while (await reader.ReadAsync(Ct))
        {
            rows.Add(reader.GetString(0));
        }

        return rows;
    }
}
