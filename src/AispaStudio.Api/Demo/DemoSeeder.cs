using System.Diagnostics;
using System.Globalization;
using AispaStudio.Api.Costs;
using AispaStudio.Api.Data;
using AispaStudio.Api.Payroll;
using AispaStudio.Api.Pos;
using AispaStudio.Api.Scheduling;
using AispaStudio.Api.Tenancy;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AispaStudio.Api.Demo;

/// <summary>Umfang des Testdatensatzes: so viele Wochen Vergangenheit (Kassa, Berichte) und Zukunft (Kalender).</summary>
public sealed class DemoDataOptions
{
    public const string Section = "DemoData";

    public int WeeksBack { get; set; } = 8;
    public int WeeksAhead { get; set; } = 4;

    /// <summary>Wie der Verlauf entsteht (<c>DemoData:Mode</c>): <see cref="DemoMode.Bulk"/> im Speicher mit <c>COPY</c>, <see cref="DemoMode.Services"/> über die Dienste.</summary>
    public DemoMode Mode { get; set; } = DemoMode.Bulk;
}

/// <summary>Uhr, die der Testdatensatz stellt, damit Termine, Belege und Abschlüsse zu ihrer Zeit in der Vergangenheit entstehen.</summary>
internal sealed class DemoClock(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;

    public override DateTimeOffset GetUtcNow() => Now;
}

/// <summary>Was die Datenbank vom Beispiel-Spa enthält.</summary>
internal enum DemoDataStatus
{
    Empty,
    Complete,
    Incomplete,
}

/// <summary>
/// Legt in einer leeren Datenbank das Beispiel-Spa als vollständigen Betrieb an: Stammdaten, Konten für alle Rollen, Dienstplan,
/// Termine der letzten Wochen mit Kassa und Tagesabschlüssen, kommende Termine, Gutscheine, Anamnesen, Warteliste und Posteingang.
/// Alles läuft über dieselben Dienste wie im Betrieb (Planer, Kassa mit Signatur), nur mit einer zurückgestellten Uhr.
/// Nur mit <c>Database:SeedDemoData</c> (Entwicklungsumgebung). Das Einspielen läuft im Hintergrund, die API ist gleich erreichbar;
/// der Stand steht in <see cref="StartupState"/> (Health-Check „startup“, <c>/api/system/status</c>).
/// </summary>
internal sealed partial class DemoSeeder(IServiceProvider services, TimeProvider time, IOptions<DemoDataOptions> options, StartupState state,
    ILogger<DemoSeeder> logger) : IHostedService, IDisposable
{
    public const string IncompleteMessage = "Beispieldaten unvollständig (das Einspielen wurde abgebrochen) – Datenbank zurücksetzen: "
        + "Volume aispa-postgres-data löschen bzw. docker compose down -v";

    private readonly CancellationTokenSource _stop = new();
    private Task _run = Task.CompletedTask;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        switch (await InspectAsync(services, cancellationToken))
        {
            case DemoDataStatus.Complete:
                return;
            case DemoDataStatus.Incomplete:
                state.Fail(IncompleteMessage);
                Incomplete(logger);
                return;
        }

        state.Begin(StartupPhase.Seeding);
        _run = Task.Run(() => RunAsync(_stop.Token), CancellationToken.None);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _stop.CancelAsync();
        try
        {
            await _run.WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Stopp oder Abbruch des Wartens: das Einspielen endet mit dem Prozess.
        }
    }

    public void Dispose() => _stop.Dispose();

    /// <summary>
    /// Leer, vollständig oder abgebrochen. Vollständig heißt: das letzte Einspielen (Bestellungen im Lager) ist angelegt;
    /// ein Standort ohne Bestellung stammt aus einem abgebrochenen Einspielen und wird nie stillschweigend übersprungen.
    /// </summary>
    internal static async Task<DemoDataStatus> InspectAsync(IServiceProvider root, CancellationToken ct)
    {
        await using var scope = root.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AispaDbContext>();
        if (!await db.Locations.IgnoreQueryFilters().AnyAsync(ct))
        {
            return DemoDataStatus.Empty;
        }

        return await db.PurchaseOrders.IgnoreQueryFilters().AnyAsync(ct) ? DemoDataStatus.Complete : DemoDataStatus.Incomplete;
    }

    private async Task RunAsync(CancellationToken ct)
    {
        try
        {
            var started = Stopwatch.GetTimestamp();
            Starting(logger, options.Value.WeeksBack, options.Value.WeeksAhead);
            await SeedAsync(services, time.GetUtcNow(), options.Value, logger, ct);
            var elapsed = Stopwatch.GetElapsedTime(started);
            Finished(logger, elapsed.TotalSeconds);
            state.Complete();
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Die API wird beendet; der nächste Start meldet den Teilstand als unvollständig.
        }
        catch (Exception ex)
        {
            Failed(logger, ex);
            state.Fail($"Beispieldaten konnten nicht angelegt werden: {ex.Message} – Datenbank zurücksetzen: "
                + "Volume aispa-postgres-data löschen bzw. docker compose down -v");
        }
    }

    public static async Task<Guid> SeedAsync(IServiceProvider root, DateTimeOffset now, DemoDataOptions options, ILogger logger, CancellationToken ct)
    {
        var timings = new DemoTimings();
        Guid tenantId;
        await using (var scope = root.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AispaDbContext>();
            using (timings.Measure("1 Stammdaten, Mandant, Standorte")) { tenantId = await DemoData.SeedAsync(scope.ServiceProvider, new DemoClock(now), ct); }
            using (timings.Measure("2 Kassa-Artikel")) { await PosDemoData.AddArticlesAsync(db, ct); }
            using (timings.Measure("3 Lagerartikel und Lagerorte")) { await DemoStock.AddItemsAsync(db, ct); await DemoStock.AddPlacesAsync(db, ct); }
            using (timings.Measure("4 Personal")) { await AddStaffDetailsAsync(db, ct); }
            using (timings.Measure("5 Preisregeln")) { await AddPriceRulesAsync(db, ct); }
            using (timings.Measure("6 Konten")) { await DemoAccounts.AddAsync(scope.ServiceProvider, now, ct); }
            using (timings.Measure("6b Aktionen")) { await DemoPromotions.AddAsync(db, now, ct); }
            using (timings.Measure("6c Betriebsinformationen")) { await DemoBusinessContacts.AddAsync(db, now, ct); }
        }

        using (timings.Measure("7 Verlauf gesamt")) { await DemoHistory.RunAsync(root, tenantId, now, options, logger, timings, options.Mode, ct); }
        using (timings.Measure("8 Extras")) { await DemoExtras.AddAsync(root, tenantId, now, ct); }
        using (timings.Measure("8b Betrieb")) { await DemoOperations.AddAsync(root, tenantId, now, ct); }
        using (timings.Measure("8c Schließzeiten")) { await DemoClosures.AddAsync(root, tenantId, now, ct); }
        await using (var scope = Open(root, tenantId))
        {
            using (timings.Measure("9 Lagerbewegungen"))
            {
                var stockDb = scope.ServiceProvider.GetRequiredService<AispaDbContext>();
                var stockStart = now.AddDays(-7 * options.WeeksBack);
                await (options.Mode == DemoMode.Bulk
                    ? DemoStockBulk.AddMovementsAsync(stockDb, tenantId, stockStart, now, ct)
                    : DemoStock.AddMovementsAsync(stockDb, stockStart, now, ct));
            }
        }

        // Änderungsverlauf der Termine (ADR 0082) zuletzt, wenn alle Termine ihren Stand haben (auch die Absagen der Schließzeiten).
        using (timings.Measure("10 Änderungsverlauf")) { await DemoChangeHistory.AddAsync(root, tenantId, now, ct); }

        timings.Log(logger);
        return tenantId;
    }

    /// <summary>Neuer Scope im Mandanten des Beispiel-Spas (im Hintergrund gibt es keine Anmeldung, die ihn setzt).</summary>
    public static AsyncServiceScope Open(IServiceProvider root, Guid tenantId)
    {
        var scope = root.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Use(tenantId);
        return scope;
    }

    /// <summary>Stundensätze (erfahrene Personen teurer als der Standortsatz), Personalnummern für den Lohnexport und Sprachen.</summary>
    private static async Task AddStaffDetailsAsync(AispaDbContext db, CancellationToken ct)
    {
        await db.Employees.Where(e => e.DisplayName == "Lena Huber").ExecuteUpdateAsync(s => s.SetProperty(e => e.Languages, ["en", "it"]), ct);
        await db.Employees.Where(e => e.DisplayName == "Marco Berger").ExecuteUpdateAsync(s => s.SetProperty(e => e.Languages, ["en"]), ct);
        var employees = await db.Employees.AsNoTracking().OrderBy(e => e.DisplayName).ToListAsync(ct);
        var number = 1000;
        foreach (var employee in employees)
        {
            number += 7;
            db.EmployeePayrolls.Add(new EmployeePayroll { EmployeeId = employee.Id, PersonnelNumber = number.ToString(CultureInfo.InvariantCulture) });
            if (employee.DisplayName is "Lena Huber" or "Marco Berger" or "Theresa Lechner")
            {
                db.EmployeeCosts.Add(new EmployeeCost { EmployeeId = employee.Id, HourlyCost = 27.50m });
            }
        }

        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Vormittagspreis für die klassische Massage (Montag bis Donnerstag 9 bis 12 Uhr) und Wochenendzuschlag auf die Thai-Massage;
    /// vor dem Verlauf angelegt, damit vergangene Buchungen die Preise ihrer Uhrzeit tragen.
    /// </summary>
    private static async Task AddPriceRulesAsync(AispaDbContext db, CancellationToken ct)
    {
        const int Weekdays = (1 << (int)DayOfWeek.Monday) | (1 << (int)DayOfWeek.Tuesday) | (1 << (int)DayOfWeek.Wednesday) | (1 << (int)DayOfWeek.Thursday);
        const int Weekend = (1 << (int)DayOfWeek.Saturday) | (1 << (int)DayOfWeek.Sunday);
        var treatments = await db.Treatments.AsNoTracking().ToDictionaryAsync(t => t.Name, ct);
        db.TreatmentPriceRules.AddRange(
            new TreatmentPriceRule { TreatmentId = treatments["Klassische Massage"].Id, Name = "Vormittag", Days = Weekdays, From = new(9, 0), Until = new(12, 0), Price = 69m },
            new TreatmentPriceRule
            {
                TreatmentId = treatments["Traditionelle Thai-Massage (60 Min.)"].Id,
                Name = "Wochenende",
                Days = Weekend,
                From = new(0, 0),
                Until = new(23, 59),
                Price = 105m,
            });
        await db.SaveChangesAsync(ct);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Beispieldaten: lege das Beispiel-Spa an ({WeeksBack} Wochen Vergangenheit, {WeeksAhead} Wochen Zukunft) …")]
    private static partial void Starting(ILogger logger, int weeksBack, int weeksAhead);

    [LoggerMessage(Level = LogLevel.Information, Message = "Beispieldaten angelegt in {Seconds:0} s. Testkonten: siehe docs/testdaten.md.")]
    private static partial void Finished(ILogger logger, double seconds);

    [LoggerMessage(Level = LogLevel.Warning, Message = IncompleteMessage)]
    private static partial void Incomplete(ILogger logger);

    [LoggerMessage(Level = LogLevel.Error, Message = "Beispieldaten konnten nicht angelegt werden. Die API bleibt erreichbar.")]
    private static partial void Failed(ILogger logger, Exception exception);
}
