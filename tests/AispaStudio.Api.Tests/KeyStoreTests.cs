using System.Reflection;
using AispaStudio.Api.MailIntake;
using AispaStudio.Api.Online;
using AispaStudio.Api.System;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace AispaStudio.Api.Tests;

/// <summary>
/// Schlüsselring wahlweise in der Datenbank (<c>DataProtection:Store</c>, ADR 0087). Jeder Host mit <c>Database</c> bekommt einen
/// ausdrücklichen <c>DataProtection:ImportFolder</c>; ohne ihn übernähme er den Standardordner des Rechners, und alle Hosts teilten
/// denselben Schlüssel auch ohne Datenbank.
/// </summary>
public sealed class KeyStoreTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private const string Purpose = PaymentSecrets.SecretKey;

    [Fact]
    public async Task Zwei_Instanzen_mit_Database_lesen_die_Geheimnisse_der_anderen()
    {
        var tenantId = Guid.NewGuid();
        var mailboxId = Guid.NewGuid();
        using var empty = new TempFolder();
        string payment;
        string mailbox;
        await using (var first = DatabaseHost(fixture.ConnectionString, empty.Path))
        {
            payment = first.Services.GetRequiredService<PaymentSecrets>().Protect(tenantId, Purpose, "sk_test_zwei");
            mailbox = first.Services.GetRequiredService<SpaMailboxSecrets>().Protect(tenantId, mailboxId, "postfach-zwei");
        }

        // Schreibweise wie bei Hosting:Role egal.
        await using var second = DatabaseHost(fixture.ConnectionString, empty.Path, store: " database ");

        Assert.Equal("sk_test_zwei", second.Services.GetRequiredService<PaymentSecrets>().Unprotect(tenantId, Purpose, payment));
        Assert.Equal("postfach-zwei", second.Services.GetRequiredService<SpaMailboxSecrets>().Unprotect(tenantId, mailboxId, mailbox));
        Assert.Equal(1, await CountAsync(fixture.ConnectionString, $"""SELECT count(*) FROM system."DataProtectionKeys" WHERE "FriendlyName" = 'key-{KeyId(payment)}'"""));
    }

    [Fact]
    public async Task Frische_Datenbank_startet_mit_eigenem_Schluessel_und_liest_fremde_Werte_nicht()
    {
        var tenantId = Guid.NewGuid();
        using var empty = new TempFolder();
        string foreign;
        await using (var shared = DatabaseHost(fixture.ConnectionString, empty.Path))
        {
            foreign = shared.Services.GetRequiredService<PaymentSecrets>().Protect(tenantId, Purpose, "sk_test_fremd");
        }

        // Frische Datenbank mit MigrateOnStartup: beim Start fehlt die Tabelle noch, Data Protection läuft trotzdem an.
        var connection = await fixture.CreateDatabaseAsync(Ct);
        await using var fresh = DatabaseHost(connection, empty.Path);
        var secrets = fresh.Services.GetRequiredService<PaymentSecrets>();

        Assert.Null(secrets.Unprotect(tenantId, Purpose, foreign));
        var own = secrets.Protect(tenantId, Purpose, "sk_test_eigen");
        Assert.Equal("sk_test_eigen", secrets.Unprotect(tenantId, Purpose, own));
        Assert.Equal(1, await CountAsync(connection, $"""SELECT count(*) FROM system."DataProtectionKeys" WHERE "FriendlyName" = 'key-{KeyId(own)}'"""));
    }

    [Fact]
    public async Task Schluessel_aus_dem_Ordner_werden_einmal_uebernommen_und_alte_Werte_bleiben_lesbar()
    {
        var tenantId = Guid.NewGuid();
        using var folder = new TempFolder();
        string old;
        await using (var fileSystem = fixture.Factory.WithWebHostBuilder(b =>
            b.ConfigureTestServices(s => s.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(folder.Path)))))
        {
            old = fileSystem.Services.GetRequiredService<PaymentSecrets>().Protect(tenantId, Purpose, "sk_test_alt");
        }

        var files = Directory.GetFiles(folder.Path, "*.xml").Length;
        Assert.True(files > 0, "Der Host mit FileSystem hat keinen Schlüssel in den Ordner geschrieben.");
        var connection = await fixture.CreateDatabaseAsync(Ct);
        await using (var imported = DatabaseHost(connection, folder.Path))
        {
            Assert.Equal("sk_test_alt", imported.Services.GetRequiredService<PaymentSecrets>().Unprotect(tenantId, Purpose, old));
        }

        var rows = await CountAsync(connection, """SELECT count(*) FROM system."DataProtectionKeys" """);
        Assert.True(rows >= files, $"{rows} Zeilen für {files} Dateien");

        await using (var again = DatabaseHost(connection, folder.Path))
        {
            Assert.Equal("sk_test_alt", again.Services.GetRequiredService<PaymentSecrets>().Unprotect(tenantId, Purpose, old));
        }

        Assert.Equal(rows, await CountAsync(connection, """SELECT count(*) FROM system."DataProtectionKeys" """));
    }

    /// <summary>Darum setzt die API keinen <c>SetApplicationName</c>: vorhandene Zugänge wären danach nicht mehr lesbar.</summary>
    [Fact]
    public async Task Mit_eigenem_Anwendungsnamen_sind_vorhandene_Werte_nicht_lesbar()
    {
        var tenantId = Guid.NewGuid();
        using var folder = new TempFolder();
        string protectedSecret;
        await using (var standard = FileSystemHost(folder.Path, applicationName: null))
        {
            protectedSecret = standard.Services.GetRequiredService<PaymentSecrets>().Protect(tenantId, Purpose, "sk_test_name");
        }

        await using var sameName = FileSystemHost(folder.Path, applicationName: null);
        await using var named = FileSystemHost(folder.Path, applicationName: "AispaStudio");

        Assert.Equal("sk_test_name", sameName.Services.GetRequiredService<PaymentSecrets>().Unprotect(tenantId, Purpose, protectedSecret));
        Assert.Null(named.Services.GetRequiredService<PaymentSecrets>().Unprotect(tenantId, Purpose, protectedSecret));
    }

    [Fact]
    public async Task Tabelle_liegt_im_Schema_system_ohne_Mandant()
    {
        using var empty = new TempFolder();
        await using (var host = DatabaseHost(fixture.ConnectionString, empty.Path))
        {
            _ = host.Server;
        }

        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync(Ct);
        await using var command = new NpgsqlCommand(
            """
            SELECT string_agg(column_name, ',' ORDER BY column_name) FROM information_schema.columns
            WHERE table_schema = 'system' AND table_name = 'DataProtectionKeys'
            """, connection);

        Assert.Equal("FriendlyName,Id,Xml", (string?)await command.ExecuteScalarAsync(Ct));
    }

    [Fact]
    public async Task Schluesselring_hat_keine_fehlende_Migration()
    {
        await using var db = new KeyStoreDbContextDesignTimeFactory().CreateDbContext([]);

        Assert.False(db.Database.HasPendingModelChanges(),
            "Modell geändert: 'dotnet ef migrations add <Name> -p src/AispaStudio.Api --context KeyStoreDbContext -o System/KeyStoreMigrations' ausführen.");
    }

    [Theory]
    [InlineData("Redis")]
    [InlineData("1")]
    public async Task Ungueltiger_Speicher_bricht_den_Start_ab(string store)
    {
        await using var host = fixture.Factory.WithWebHostBuilder(b => b.UseSetting("DataProtection:Store", store));

        var error = Assert.Throws<InvalidOperationException>(() => host.Server);

        Assert.StartsWith($"DataProtection:Store „{store}“ ist ungültig", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Database_ohne_Migration_und_ohne_Tabelle_bricht_den_Start_ab()
    {
        using var empty = new TempFolder();
        var connection = await fixture.CreateDatabaseAsync(Ct);
        await using var host = fixture.Factory.WithWebHostBuilder(b =>
        {
            b.UseSetting("ConnectionStrings:aispadb", connection);
            b.UseSetting("Database:MigrateOnStartup", "false");
            b.UseSetting("DataProtection:Store", "Database");
            b.UseSetting("DataProtection:ImportFolder", empty.Path);
        });

        var error = Assert.Throws<InvalidOperationException>(() => host.Server);

        Assert.StartsWith("DataProtection:Store=Database braucht die Tabelle", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Database_ohne_Migration_mit_Tabelle_startet()
    {
        var tenantId = Guid.NewGuid();
        using var empty = new TempFolder();
        var connection = await fixture.CreateDatabaseAsync(Ct);
        await using (var migrated = DatabaseHost(connection, empty.Path))
        {
            _ = migrated.Server;
        }

        await using var host = DatabaseHost(connection, empty.Path, migrateOnStartup: false);
        var secrets = host.Services.GetRequiredService<PaymentSecrets>();

        var protectedSecret = secrets.Protect(tenantId, Purpose, "sk_test_ohne_migration");
        Assert.Equal("sk_test_ohne_migration", secrets.Unprotect(tenantId, Purpose, protectedSecret));
    }

    /// <summary>
    /// Die Tabelle hat schon einen Schlüssel, die Übernahme bringt später weitere (Umstellung nach einer Migration ohne Übernahme,
    /// nachträglich gesetzter Ordner, Start ohne Migration). Data Protection lädt den Ring beim Start einmal; liefe die Übernahme
    /// danach, kennte die Instanz die übernommenen Schlüssel nach dem Auffrischfenster (2 Minuten) bis zum nächsten Laden nicht.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Spaete_Uebernahme_in_eine_Tabelle_mit_Schluessel_ist_sofort_lesbar(bool migrateOnStartup)
    {
        var tenantId = Guid.NewGuid();
        using var folder = new TempFolder();
        using var empty = new TempFolder();
        string old;
        await using (var fileSystem = FileSystemHost(folder.Path, applicationName: null))
        {
            old = fileSystem.Services.GetRequiredService<PaymentSecrets>().Protect(tenantId, Purpose, "sk_test_spaet");
        }

        var connection = await fixture.CreateDatabaseAsync(Ct);
        await using (var first = DatabaseHost(connection, empty.Path))
        {
            _ = first.Services.GetRequiredService<PaymentSecrets>().Protect(tenantId, Purpose, "sk_test_erster");
        }

        await using var importing = DatabaseHost(connection, folder.Path, migrateOnStartup: migrateOnStartup);
        CloseAutoRefreshWindow(importing.Services);

        Assert.Equal("sk_test_spaet", importing.Services.GetRequiredService<PaymentSecrets>().Unprotect(tenantId, Purpose, old));
    }

    private WebApplicationFactory<Program> DatabaseHost(string connection, string importFolder, string store = "Database",
        bool migrateOnStartup = true) =>
        fixture.Factory.WithWebHostBuilder(b =>
        {
            b.UseSetting("ConnectionStrings:aispadb", connection);
            b.UseSetting("Database:MigrateOnStartup", migrateOnStartup ? "true" : "false");
            b.UseSetting("DataProtection:Store", store);
            b.UseSetting("DataProtection:ImportFolder", importFolder);
        });

    /// <summary>
    /// Schließt das Auffrischfenster des Schlüsselrings (intern, <c>KeyRingProvider.AutoRefreshWindowEnd</c>): Danach liest die Instanz
    /// nur noch aus dem beim Start geladenen Ring, wie im Betrieb nach zwei Minuten.
    /// </summary>
    private static void CloseAutoRefreshWindow(IServiceProvider services)
    {
        var providerType = typeof(KeyManagementOptions).Assembly
            .GetType("Microsoft.AspNetCore.DataProtection.KeyManagement.Internal.IKeyRingProvider", throwOnError: true)!;
        var provider = services.GetRequiredService(providerType);
        var window = provider.GetType().GetProperty("AutoRefreshWindowEnd", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(window);
        window.SetValue(provider, DateTime.UtcNow.AddMinutes(-1));
    }

    private WebApplicationFactory<Program> FileSystemHost(string folder, string? applicationName) =>
        fixture.Factory.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
        {
            var protection = s.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(folder));
            if (applicationName is not null)
            {
                protection.SetApplicationName(applicationName);
            }
        }));

    /// <summary>Schlüssel-ID eines geschützten Werts: nach den 4 Bytes der Kennung (Magic Header) die 16 Bytes der Guid.</summary>
    private static Guid KeyId(string protectedValue) => new(WebEncoders.Base64UrlDecode(protectedValue).AsSpan(4, 16));

    private static async Task<long> CountAsync(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(Ct);
        await using var command = new NpgsqlCommand(sql, connection);
        return (long)(await command.ExecuteScalarAsync(Ct))!;
    }

    /// <summary>Eigener leerer Ordner je Test, nie der Standardordner des Rechners.</summary>
    private sealed class TempFolder : IDisposable
    {
        public string Path { get; } = Directory.CreateTempSubdirectory("aispa-keys-").FullName;

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
