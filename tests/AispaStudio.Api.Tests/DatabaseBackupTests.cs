using AispaStudio.AppHost;
using Testcontainers.PostgreSql;

namespace AispaStudio.Api.Tests;

/// <summary>Prüft die Logik der Dashboard-Befehle des AppHost (Sicherung erstellen und einspielen); die Quellen sind in dieses Projekt eingebunden.</summary>
public sealed class DatabaseBackupTests : IDisposable
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly string _folder = Path.Combine(Path.GetTempPath(), "aispa-backup-test-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    [Fact]
    public void Dateiname_enthaelt_Datum_und_Uhrzeit()
    {
        var name = DatabaseBackupFiles.CreateFileName(new DateTime(2026, 10, 20, 7, 5, 9));

        Assert.Equal("aispadb-2026-10-20-070509.dump", name);
    }

    [Fact]
    public void Ohne_Ordner_gibt_es_keine_Sicherungen_und_eine_Fehlermeldung()
    {
        Assert.Empty(DatabaseBackupFiles.ListNewestFirst(_folder));

        var (path, error) = DatabaseBackupFiles.Resolve(_folder, null);

        Assert.Null(path);
        Assert.Contains("Keine Sicherung", error);
    }

    [Fact]
    public void Neueste_Sicherung_wird_ohne_Auswahl_gewaehlt_fremde_Dateien_zaehlen_nicht()
    {
        Directory.CreateDirectory(_folder);
        foreach (var name in new[] { "aispadb-2026-10-19-235959.dump", "aispadb-2026-10-20-070509.dump", "notizen.txt", "aispadb-2026-10-21-000000.dump.part" })
        {
            File.WriteAllText(Path.Combine(_folder, name), "x");
        }

        var list = DatabaseBackupFiles.ListNewestFirst(_folder);
        var (path, error) = DatabaseBackupFiles.Resolve(_folder, null);

        Assert.Equal(["aispadb-2026-10-20-070509.dump", "aispadb-2026-10-19-235959.dump"], list);
        Assert.Null(error);
        Assert.Equal(Path.Combine(_folder, "aispadb-2026-10-20-070509.dump"), path);
    }

    [Theory]
    [InlineData("aispadb-2026-10-19-235959.dump", true)]
    [InlineData("aispadb-1999-01-01-000000.dump", false)]
    [InlineData("..\\..\\geheim.dump", false)]
    [InlineData("../aispadb-2026-10-19-235959.dump", false)]
    public void Auswahl_nimmt_nur_vorhandene_Sicherungen_aus_dem_Ordner(string selected, bool expected)
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllText(Path.Combine(_folder, "aispadb-2026-10-19-235959.dump"), "x");

        var (path, _) = DatabaseBackupFiles.Resolve(_folder, selected);

        Assert.Equal(expected, path is not null);
    }

    [Theory]
    [InlineData(500, "500 Byte")]
    [InlineData(2048, "2 KB")]
    [InlineData(5 * 1024 * 1024, "5 MB")]
    public void Groesse_wird_lesbar_angegeben(long bytes, string expected) =>
        Assert.Equal(expected, DatabaseBackupFiles.FormatSize(bytes));

    [Theory]
    [InlineData("3f2a9c1b7e0d", true)]
    [InlineData("", false)]
    [InlineData("x; rm -rf /", false)]
    [InlineData("--privileged", false)]
    public void Container_Kennung_muss_hexadezimal_sein(string id, bool expected) =>
        Assert.Equal(expected, PostgresContainer.IsContainerId(id));

    [Fact]
    public void Befehlszeilen_enthalten_kein_Kennwort_und_reichen_Argumente_unveraendert_durch()
    {
        var container = new PostgresContainer("abc123", "postgres");
        string[] shell = ["sh", "-c", PostgresContainer.WithPassword, "sh"];

        // Das Kennwort holt die Shell im Container aus POSTGRES_PASSWORD; auf dem Rechner steht es in keiner Befehlszeile.
        Assert.Equal(["exec", "abc123", .. shell, "pg_dump", "-U", "postgres", "-Fc", "aispadb"], container.DumpArguments("aispadb"));
        Assert.Equal(
            ["exec", "-i", "abc123", .. shell, "pg_restore", "-U", "postgres", "-d", "aispadb", "--no-owner", "--no-acl", "--exit-on-error"],
            container.RestoreArguments("aispadb"));
        // "$@" reicht die SQL-Anweisung als ein einziges Argument weiter; die Shell wertet darin nichts aus.
        Assert.Equal("DROP DATABASE \"x\"; $(id)", container.SqlArguments("postgres", "DROP DATABASE \"x\"; $(id)")[^1]);
    }

    [Fact]
    public async Task Sicherung_und_Einspielen_stellen_den_alten_Stand_wieder_her()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();
        await postgres.StartAsync(Ct);
        var container = new PostgresContainer(postgres.Id, "postgres");

        await container.RecreateDatabaseAsync("aispadb", Ct);
        await container.ExecuteSqlAsync("aispadb", "CREATE TABLE gast (name text); INSERT INTO gast VALUES ('Alt')", Ct);

        Directory.CreateDirectory(_folder);
        var file = Path.Combine(_folder, DatabaseBackupFiles.CreateFileName(DateTime.Now));
        await using (var output = File.Create(file))
        {
            await container.DumpAsync("aispadb", output, Ct);
        }

        await container.ExecuteSqlAsync("aispadb", "INSERT INTO gast VALUES ('Neu')", Ct);
        Assert.Equal("2", await container.ExecuteSqlAsync("aispadb", "SELECT count(*) FROM gast", Ct));

        await container.RecreateDatabaseAsync("aispadb", Ct);
        await using (var input = File.OpenRead(file))
        {
            await container.RestoreAsync("aispadb", input, Ct);
        }

        Assert.Equal("Alt", await container.ExecuteSqlAsync("aispadb", "SELECT string_agg(name, ',') FROM gast", Ct));
    }
}
