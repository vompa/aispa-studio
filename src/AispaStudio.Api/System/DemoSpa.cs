using Npgsql;

namespace AispaStudio.Api.System;

/// <summary>
/// Beispiel-Spa in einer eigenen Datenbank (ADR 0100). Mit <c>Demo:Enabled</c> nutzt die API die Datenbank <see cref="DatabaseName"/> im selben
/// PostgreSQL und füllt sie mit den Beispieldaten; die echte Datenbank <c>aispadb</c> bleibt unberührt. Der Name ist fest: Löschen trifft nie
/// eine andere Datenbank.
/// </summary>
internal static class DemoSpa
{
    public const string DatabaseName = "aispademo";

    private const string MaintenanceDatabase = "postgres";
    private const string DuplicateDatabase = "42P04";
    private const string UniqueViolation = "23505";

    public static bool IsEnabled(IConfiguration configuration) => configuration.GetValue<bool>("Demo:Enabled");

    /// <summary>Verbindung zur Datenbank des Beispiel-Spas: dieselbe Verbindung wie die echte Datenbank, nur mit anderem Datenbanknamen.</summary>
    public static string ConnectionFor(string connectionString) =>
        new NpgsqlConnectionStringBuilder(connectionString) { Database = DatabaseName }.ConnectionString;

    /// <summary>
    /// Schaltet die Konfiguration auf das Beispiel-Spa um und legt die Datenbank an, falls sie fehlt. Vor dem Registrieren von DbContext und
    /// Wolverine aufrufen: Wolverine verbindet vor der Migration, die Datenbank muss also schon da sein.
    /// </summary>
    public static async Task ApplyAsync(ConfigurationManager configuration, string connectionName, CancellationToken ct = default)
    {
        var demo = ConnectionFor(configuration.GetConnectionString(connectionName)!);
        configuration[$"ConnectionStrings:{connectionName}"] = demo;
        // Füllen darf nur eine Instanz mit Hintergrunddiensten; im Mehrinstanz-Betrieb arbeitet die Rolle Api nur im Beispiel-Spa (ADR 0087).
        configuration["Database:SeedDemoData"] = ModuleSwitches.From(configuration).RunsBackgroundServices.ToString();
        await EnsureDatabaseAsync(demo, ct);
    }

    internal static async Task EnsureDatabaseAsync(string demoConnection, CancellationToken ct)
    {
        await using var server = new NpgsqlConnection(MaintenanceConnection(demoConnection));
        await server.OpenAsync(ct);
        await using var exists = new NpgsqlCommand("SELECT 1 FROM pg_database WHERE datname = @name", server);
        exists.Parameters.AddWithValue("name", DatabaseName);
        if (await exists.ExecuteScalarAsync(ct) is not null)
        {
            return;
        }

        try
        {
            await using var create = new NpgsqlCommand($"CREATE DATABASE {DatabaseName}", server);
            await create.ExecuteNonQueryAsync(ct);
        }
        catch (PostgresException ex) when (ex.SqlState is DuplicateDatabase or UniqueViolation)
        {
            // Eine zweite Instanz war schneller (Mehrinstanz-Betrieb).
        }
    }

    /// <summary>Löscht die Datenbank des Beispiel-Spas samt aller Verbindungen darauf; <paramref name="demoConnection"/> liefert nur Server und Zugang.</summary>
    internal static async Task DropAsync(string demoConnection, CancellationToken ct)
    {
        // Verbindungen des eigenen Pools zuerst schließen, sonst bleiben sie bis zum Zwangsende offen.
        NpgsqlConnection.ClearAllPools();
        await using var server = new NpgsqlConnection(MaintenanceConnection(demoConnection));
        await server.OpenAsync(ct);
        await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS {DatabaseName} WITH (FORCE)", server);
        await drop.ExecuteNonQueryAsync(ct);
    }

    private static string MaintenanceConnection(string connectionString) =>
        new NpgsqlConnectionStringBuilder(connectionString) { Database = MaintenanceDatabase, Pooling = false }.ConnectionString;
}
