using System.Data.Common;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using AispaStudio.Api.Data;
using AispaStudio.Api.Demo;
using AispaStudio.Api.Online;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace AispaStudio.Api.Tests;

/// <summary>
/// Messung des Einspielens mit den Standardwerten (8 Wochen zurück, 4 voraus) in einer eigenen Datenbank: Zeit je Abschnitt,
/// Datenbankbefehle, Speichervorgänge, Zeilen je Tabelle, Nachrichten der Queue. Läuft nur mit <c>AISPA_DEMO_BENCH=1</c>;
/// das Ergebnis steht im Test-Protokoll und in <c>AISPA_DEMO_BENCH_OUT</c> (Standard: Temp-Ordner).
/// </summary>
public sealed class DemoBenchTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public static bool Enabled => Environment.GetEnvironmentVariable("AISPA_DEMO_BENCH") == "1";

    [Fact(Skip = "Nur mit AISPA_DEMO_BENCH=1", SkipUnless = nameof(Enabled))]
    public async Task Einspielen_mit_Standardwerten_messen()
    {
        var report = new StringBuilder();
        var counter = new Counter();
        var connection = await fixture.CreateDatabaseAsync(Ct);
        using var host = fixture.Factory.WithWebHostBuilder(b =>
        {
            b.UseSetting("ConnectionStrings:aispadb", connection);
            b.ConfigureTestServices(s =>
            {
                s.ConfigureDbContext<AispaDbContext>(o => o.AddInterceptors(counter.Commands, counter.Saves));
                s.ConfigureDbContext<OnlineDbContext>(o => o.AddInterceptors(counter.Commands, counter.Saves));
            });
        });
        _ = host.Server;

        // AISPA_DEMO_MODE=Services misst den langsamen Weg über die Dienste, sonst gilt der Standard.
        var options = new DemoDataOptions();
        if (Enum.TryParse<DemoMode>(Environment.GetEnvironmentVariable("AISPA_DEMO_MODE"), out var mode))
        {
            options.Mode = mode;
        }

        var stopwatch = Stopwatch.StartNew();
        await DemoSeeder.SeedAsync(host.Services, TimeProvider.System.GetUtcNow(), options, new ReportLogger(report), Ct);
        var seconds = stopwatch.Elapsed.TotalSeconds;
        report.AppendLine(CultureInfo.InvariantCulture,
            $"GESAMT ({options.Mode}) {seconds:0.0} s, Befehle {counter.Commands.Count}, Speichervorgänge {counter.Saves.Count}");

        var drained = Stopwatch.StartNew();
        await WaitForQueueAsync(connection, report);
        report.AppendLine(CultureInfo.InvariantCulture, $"Queue nach dem Einspielen noch {drained.Elapsed.TotalSeconds:0.0} s beschäftigt");
        await AppendRowsAsync(connection, report);
        await AppendTriggerCostAsync(connection, report);

        var text = report.ToString();
        File.WriteAllText(Environment.GetEnvironmentVariable("AISPA_DEMO_BENCH_OUT") ?? Path.Combine(Path.GetTempPath(), "aispa-demo-bench.txt"), text);
        TestContext.Current.SendDiagnosticMessage(text);
    }

    private static async Task WaitForQueueAsync(string connection, StringBuilder report)
    {
        await using var db = new NpgsqlConnection(connection);
        await db.OpenAsync(Ct);
        var deadline = DateTime.UtcNow.AddSeconds(20);
        while (DateTime.UtcNow < deadline)
        {
            await using var command = new NpgsqlCommand("SELECT count(*) FROM wolverine.wolverine_incoming_envelopes", db);
            if ((long)(await command.ExecuteScalarAsync(Ct))! == 0)
            {
                return;
            }

            await Task.Delay(500, Ct);
        }

        report.AppendLine("Queue nach 20 s noch nicht leer (Rest sind geplante Nachrichten)");
    }

    /// <summary>Kosten der Abgleich-Trigger: dieselbe Änderung aller Zeilen in einer zurückgerollten Transaktion mit und ohne Trigger; dazu die Dauer eines Befehls.</summary>
    private static async Task AppendTriggerCostAsync(string connection, StringBuilder report)
    {
        await using var db = new NpgsqlConnection(connection);
        await db.OpenAsync(Ct);
        var stopwatch = Stopwatch.StartNew();
        for (var i = 0; i < 500; i++)
        {
            await using var ping = new NpgsqlCommand("SELECT 1", db);
            await ping.ExecuteScalarAsync(Ct);
        }

        report.AppendLine(CultureInfo.InvariantCulture, $"Ein Befehl (SELECT 1) im Mittel {stopwatch.Elapsed.TotalMilliseconds / 500:0.00} ms");
        foreach (var table in new[] { "ReceiptLine", "ResourceBookings", "Appointments" })
        {
            foreach (var triggers in new[] { true, false })
            {
                await using var tx = await db.BeginTransactionAsync(Ct);
                if (!triggers)
                {
                    await using var off = new NpgsqlCommand($"ALTER TABLE aispa.\"{table}\" DISABLE TRIGGER aispa_sync_log", db, tx);
                    await off.ExecuteNonQueryAsync(Ct);
                }

                stopwatch.Restart();
                await using var update = new NpgsqlCommand($"UPDATE aispa.\"{table}\" SET \"Id\" = \"Id\"", db, tx);
                var rows = await update.ExecuteNonQueryAsync(Ct);
                report.AppendLine(CultureInfo.InvariantCulture,
                    $"UPDATE {table} ({rows} Zeilen) {(triggers ? "mit" : "ohne")} Trigger: {stopwatch.Elapsed.TotalMilliseconds:0} ms");
                await tx.RollbackAsync(Ct);
            }
        }
    }

    private static async Task AppendRowsAsync(string connection, StringBuilder report)
    {
        await Task.Delay(2000, Ct);
        await using var db = new NpgsqlConnection(connection);
        await db.OpenAsync(Ct);
        await using var command = new NpgsqlCommand(
            "SELECT schemaname || '.' || relname, n_tup_ins FROM pg_stat_user_tables WHERE n_tup_ins > 0 AND schemaname <> 'sync' ORDER BY n_tup_ins DESC", db);
        await using var reader = await command.ExecuteReaderAsync(Ct);
        report.AppendLine("Eingefügte Zeilen je Tabelle (ohne sync):");
        while (await reader.ReadAsync(Ct))
        {
            report.AppendLine(CultureInfo.InvariantCulture, $"  {reader.GetString(0)}: {reader.GetInt64(1)}");
        }
    }

    private sealed class Counter
    {
        public CommandCounter Commands { get; } = new();
        public SaveCounter Saves { get; } = new();
    }

    private sealed class CommandCounter : DbCommandInterceptor
    {
        private int _count;
        public int Count => _count;

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _count);
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _count);
            return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
        }

        public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<object> result, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _count);
            return base.ScalarExecutingAsync(command, eventData, result, cancellationToken);
        }
    }

    private sealed class SaveCounter : SaveChangesInterceptor
    {
        private int _count;
        public int Count => _count;

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _count);
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }

    private sealed class ReportLogger(StringBuilder report) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            lock (report)
            {
                report.AppendLine(formatter(state, exception));
            }
        }
    }
}
