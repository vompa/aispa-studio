using AispaStudio.Api.Demo;
using Npgsql;

namespace AispaStudio.Api.Tests;

/// <summary>Veraltete Beispieldaten beim Start (T-091): neu anlegen nur mit Erlaubnis und nur mit Konten der Beispiel-Domain.</summary>
public sealed class StaleDemoDataTests(ApiFixture fixture)
{
    private static readonly DateTimeOffset Now = new(2026, 11, 25, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset OldStart = new(2026, 11, 17, 14, 45, 0, TimeSpan.Zero);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>Eigene Datenbank mit den zwei Tabellen, die die Prüfung liest: ein Termin und optional ein Konto der Beispiel-Domain.</summary>
    private async Task<string> DatabaseAsync(bool demoAccount)
    {
        var connection = await fixture.CreateDatabaseAsync(Ct);
        await using var db = new NpgsqlConnection(new NpgsqlConnectionStringBuilder(connection) { Pooling = false }.ConnectionString);
        await db.OpenAsync(Ct);
        await using var command = new NpgsqlCommand("""
            CREATE SCHEMA aispa;
            CREATE TABLE aispa."Appointments" ("Start" timestamptz NOT NULL);
            CREATE TABLE aispa."AspNetUsers" ("NormalizedEmail" text);
            INSERT INTO aispa."Appointments" VALUES (@start);
            INSERT INTO aispa."AspNetUsers" VALUES (@email);
            """, db);
        command.Parameters.AddWithValue("start", OldStart);
        command.Parameters.AddWithValue("email", demoAccount ? "INHABERIN@DEMO.AISPA.TEST" : "CHEFIN@SPA.EXAMPLE");
        await command.ExecuteNonQueryAsync(Ct);
        return connection;
    }

    [Theory]
    [InlineData(-30, 4, true)]
    [InlineData(13, 4, true)]
    [InlineData(15, 4, false)]
    [InlineData(4, 1, false)]
    public void Beispieldaten_gelten_als_veraltet_wenn_der_letzte_Termin_vor_vierzehn_Tagen_voraus_liegt(int daysAhead, int weeksAhead, bool stale)
    {
        Assert.Equal(stale, StaleDemoData.IsStale(Now.AddDays(daysAhead), Now, weeksAhead));
        Assert.False(StaleDemoData.IsStale(null, Now, weeksAhead));
    }

    [Fact]
    public async Task Veraltete_Beispieldaten_werden_neu_angelegt_wenn_es_erlaubt_ist()
    {
        var connection = await DatabaseAsync(demoAccount: true);

        var check = await StaleDemoData.HandleAsync(connection, Now, 4, ct => StaleDemoData.RecreateDatabaseAsync(connection, ct), Ct);

        Assert.Equal(new StaleDemoCheck(StaleDemoOutcome.Recreated, OldStart), check);
        // Die Datenbank besteht weiter (Wolverine verbindet vor der Migration), ist aber leer.
        Assert.Null(await StaleDemoData.LatestStartAsync(connection, Ct));
    }

    [Fact]
    public async Task Ohne_Erlaubnis_bleibt_die_Datenbank_und_es_gibt_einen_Hinweis()
    {
        var connection = await DatabaseAsync(demoAccount: true);

        var check = await StaleDemoData.HandleAsync(connection, Now, 4, recreate: null, Ct);

        Assert.Equal(StaleDemoOutcome.Stale, check.Outcome);
        Assert.Contains("letzter Termin 17.11.2026", check.Notice, StringComparison.Ordinal);
        Assert.Equal(OldStart, await StaleDemoData.LatestStartAsync(connection, Ct));
    }

    [Fact]
    public async Task Eingespielte_Sicherung_wird_nie_neu_angelegt()
    {
        var connection = await DatabaseAsync(demoAccount: true);
        var database = new NpgsqlConnectionStringBuilder(connection).Database;
        await using (var db = new NpgsqlConnection(new NpgsqlConnectionStringBuilder(connection) { Pooling = false }.ConnectionString))
        {
            await db.OpenAsync(Ct);
            await using var comment = new NpgsqlCommand($"COMMENT ON DATABASE \"{database}\" IS '{StaleDemoData.RestoredBackupComment}'", db);
            await comment.ExecuteNonQueryAsync(Ct);
        }

        var check = await StaleDemoData.HandleAsync(connection, Now, 4, ct => StaleDemoData.RecreateDatabaseAsync(connection, ct), Ct);

        Assert.Equal(StaleDemoCheck.Fresh, check);
        Assert.Equal(OldStart, await StaleDemoData.LatestStartAsync(connection, Ct));
    }

    [Fact]
    public async Task Datenbank_ohne_Beispielkonten_wird_nie_geloescht()
    {
        var connection = await DatabaseAsync(demoAccount: false);

        var check = await StaleDemoData.HandleAsync(connection, Now, 4, ct => StaleDemoData.RecreateDatabaseAsync(connection, ct), Ct);

        Assert.Equal(StaleDemoCheck.Fresh, check);
        Assert.Equal(OldStart, await StaleDemoData.LatestStartAsync(connection, Ct));
    }
}
