using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AispaStudio.Api.System;

/// <summary>Wo der Schlüsselring von Data Protection liegt (<c>DataProtection:Store</c>, ADR 0087).</summary>
public enum DataProtectionStore
{
    /// <summary>Standard von ASP.NET Core, wie bisher: Ordner des Benutzers bzw. im Docker-Image das Volume <c>api-keys</c>.</summary>
    FileSystem,

    /// <summary>Tabelle <c>system."DataProtectionKeys"</c> in derselben Datenbank; alle Instanzen teilen den Schlüsselring.</summary>
    Database,
}

/// <summary>
/// Schlüsselring von Data Protection in der Datenbank. Eigener Kontext im Schema „system“, weil die Schlüssel keinem Mandanten
/// gehören; im <c>AispaDbContext</c> wäre jede Entity automatisch mandantengetrennt.
/// </summary>
public sealed class KeyStoreDbContext(DbContextOptions<KeyStoreDbContext> options) : DbContext(options), IDataProtectionKeyContext
{
    public const string Schema = "system";

    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        // Eindeutig, damit die Übernahme aus dem Ordner bei jedem Start laufen kann, ohne Schlüssel doppelt einzutragen.
        modelBuilder.Entity<DataProtectionKey>().HasIndex(k => k.FriendlyName).IsUnique();
    }
}

/// <summary>Für "dotnet ef migrations add … --context KeyStoreDbContext": baut den Kontext ohne laufende App und ohne Datenbank.</summary>
internal sealed class KeyStoreDbContextDesignTimeFactory : IDesignTimeDbContextFactory<KeyStoreDbContext>
{
    public KeyStoreDbContext CreateDbContext(string[] args) => new(new DbContextOptionsBuilder<KeyStoreDbContext>()
        .UseNpgsql("Host=localhost;Database=aispadb", KeyStore.ConfigureNpgsql)
        .Options);
}

/// <summary>Registrierung und Startprüfung des Schlüsselrings nach <c>DataProtection:Store</c>.</summary>
internal static class KeyStore
{
    public static void ConfigureNpgsql(Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure.NpgsqlDbContextOptionsBuilder npgsql) =>
        npgsql.MigrationsHistoryTable("__EFMigrationsHistory", KeyStoreDbContext.Schema);

    /// <summary>
    /// Bei <c>FileSystem</c> bleibt alles wie bisher, ohne eigenen <c>AddDataProtection</c>-Aufruf. Bei <c>Database</c> liegen die
    /// Schlüssel in <see cref="KeyStoreDbContext"/>. <c>SetApplicationName</c> bleibt bewusst weg: Der Name ändert den Zweck aller
    /// Schlüssel, vorhandene Cookies und Zugänge wären danach nicht mehr lesbar. Alle Instanzen brauchen deshalb denselben
    /// Content-Root, im Container <c>/app</c>.
    /// </summary>
    public static DataProtectionStore AddKeyStore(this IServiceCollection services, IConfiguration configuration, string connectionString)
    {
        var store = StoreFrom(configuration["DataProtection:Store"]);
        if (store == DataProtectionStore.Database)
        {
            services.AddDbContext<KeyStoreDbContext>(o => o.UseNpgsql(connectionString, ConfigureNpgsql));
            services.AddDataProtection().PersistKeysToDbContext<KeyStoreDbContext>();
            services.AddScoped<KeyStoreMigration>();
        }

        return store;
    }

    /// <summary>
    /// Bei <c>Database</c> vor <c>RunAsync</c>: mit <c>Database:MigrateOnStartup</c> die Tabelle migrieren, sonst prüfen, dass sie da ist,
    /// und danach die Schlüssel aus dem Ordner übernehmen. Vor dem Start, weil Data Protection den Schlüsselring beim Start einmal lädt;
    /// eine spätere Übernahme kennte die Instanz nach dem Auffrischfenster (2 Minuten) erst beim nächsten Laden.
    /// </summary>
    public static async Task PrepareKeyStoreAsync(this WebApplication app, DataProtectionStore store)
    {
        if (store != DataProtectionStore.Database)
        {
            return;
        }

        await using var scope = app.Services.CreateAsyncScope();
        var keyStore = scope.ServiceProvider.GetRequiredService<KeyStoreMigration>();
        if (app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
        {
            await keyStore.MigrateAsync(app.Lifetime.ApplicationStopping);
        }
        else
        {
            await keyStore.EnsureTableAsync(app.Lifetime.ApplicationStopping);
        }

        await keyStore.ImportAsync(app.Lifetime.ApplicationStopping);
    }

    /// <summary>Nur die Namen, ohne Rücksicht auf Groß- und Kleinschreibung; <c>Enum.TryParse</c> nähme auch Zahlen wie „1“ an.</summary>
    private static DataProtectionStore StoreFrom(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return DataProtectionStore.FileSystem;
        }

        var name = value.Trim();
        return Enum.GetNames<DataProtectionStore>().Contains(name, StringComparer.OrdinalIgnoreCase)
            ? Enum.Parse<DataProtectionStore>(name, ignoreCase: true)
            : throw new InvalidOperationException($"DataProtection:Store „{value}“ ist ungültig; erlaubt sind FileSystem und Database.");
    }
}

/// <summary>
/// Schritte für <c>DataProtection:Store=Database</c>: Tabelle anlegen oder prüfen und die Schlüssel aus dem bisherigen Ordner übernehmen.
/// Die Übernahme läuft bei jedem Start und trägt nur fehlende Einträge ein. Ein „nur bei leerer Tabelle“ verlöre die alten Schlüssel,
/// wenn schon ein neuer Schlüssel in der Tabelle steht (Start ohne Ordner, zweite Instanz); ein neuer Schlüssel neben den alten schadet nicht.
/// </summary>
internal sealed partial class KeyStoreMigration(KeyStoreDbContext db, IConfiguration configuration, ILogger<KeyStoreMigration> logger)
{
    public Task MigrateAsync(CancellationToken cancellationToken) => db.Database.MigrateAsync(cancellationToken);

    /// <summary>
    /// Ohne Migration beim Start muss die Tabelle schon da sein. Sonst liefe der Server an, und jede Anmeldung und jedes
    /// gespeicherte Geheimnis scheiterte erst bei der Benutzung.
    /// </summary>
    public async Task EnsureTableAsync(CancellationToken cancellationToken)
    {
        var exists = await db.Database
            .SqlQuery<bool>($"""SELECT to_regclass('"system"."DataProtectionKeys"') IS NOT NULL AS "Value" """)
            .SingleAsync(cancellationToken);
        if (!exists)
        {
            throw new InvalidOperationException(
                "DataProtection:Store=Database braucht die Tabelle system.\"DataProtectionKeys\", sie fehlt. Zuerst migrieren: "
                + "dotnet AispaStudio.Api.dll migrate ausführen oder einmal mit Database:MigrateOnStartup=true starten.");
        }
    }

    public async Task ImportAsync(CancellationToken cancellationToken)
    {
        var folder = ImportFolder();
        if (folder is null || !Directory.Exists(folder))
        {
            return;
        }

        var imported = 0;
        var boundToWindowsAccount = 0;
        foreach (var file in Directory.EnumerateFiles(folder, "*.xml"))
        {
            // ReadAllText statt Bytes: Die Dateien beginnen mit einer BOM, die sonst im XML stünde und den Schlüsselring unlesbar machte.
            var xml = await File.ReadAllTextAsync(file, cancellationToken);
            var name = Path.GetFileNameWithoutExtension(file);
            var rows = await db.Database.ExecuteSqlAsync(
                $"""INSERT INTO "system"."DataProtectionKeys" ("FriendlyName", "Xml") VALUES ({name}, {xml}) ON CONFLICT ("FriendlyName") DO NOTHING""",
                cancellationToken);
            imported += rows;
            if (rows > 0 && xml.Contains("DpapiXmlDecryptor", StringComparison.Ordinal))
            {
                boundToWindowsAccount++;
            }
        }

        Imported(logger, imported, folder);
        if (boundToWindowsAccount > 0)
        {
            BoundToWindowsAccount(logger, boundToWindowsAccount, folder);
        }
    }

    /// <summary><c>DataProtection:ImportFolder</c>, sonst der Standardordner von ASP.NET Core für den Benutzer des Servers.</summary>
    private string? ImportFolder()
    {
        var configured = configuration["DataProtection:ImportFolder"];
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured;
        }

        if (OperatingSystem.IsWindows())
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ASP.NET", "DataProtection-Keys");
        }

        var home = Environment.GetEnvironmentVariable("HOME");
        return string.IsNullOrEmpty(home) ? null : Path.Combine(home, ".aspnet", "DataProtection-Keys");
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "{Count} Schlüssel aus {Folder} übernommen")]
    private static partial void Imported(ILogger logger, int count, string folder);

    [LoggerMessage(Level = LogLevel.Warning, Message = "{Count} übernommene Schlüssel aus {Folder} sind mit DPAPI geschützt und nur unter "
        + "demselben Windows-Konto auf diesem Rechner lesbar; andere Instanzen können damit geschützte Werte nicht lesen")]
    private static partial void BoundToWindowsAccount(ILogger logger, int count, string folder);
}
