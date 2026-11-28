using System.Globalization;
using AispaStudio.Api.Data;
using AispaStudio.Api.Pos.Fiscal;
using AispaStudio.Api.Scheduling;
using AispaStudio.Api.Tenancy;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Pos;

internal sealed class RegisterConfirmation
{
    public Guid RegisterId { get; init; }
    public RegisterConfirmationKind Kind { get; init; }
    public int YearKey { get; init; }
    public Guid? ReceiptId { get; set; }
    public DateTimeOffset ConfirmedAt { get; set; }
    public required string ConfirmedBy { get; set; }

    public RegisterConfirmationDto ToDto(string? name) => new(Kind, YearKey == 0 ? null : YearKey, ReceiptId, ConfirmedAt, ConfirmedBy, name);
}

internal sealed partial class PosService
{
    internal async Task ScheduleYearlyAsync(DateTimeOffset now, Action<Guid, PosErrorKind> failed, CancellationToken ct)
    {
        var candidates = await (from r in db.CashRegisters.AsNoTracking()
                                join location in db.Locations on r.LocationId equals location.Id
                                where r.IsActive && r.Country == FiscalCountry.AT
                                      && db.Receipts.Any(s => s.RegisterId == r.Id && s.Kind == ReceiptKind.Start)
                                select new { Register = r, location.TimeZone }).ToListAsync(ct);
        foreach (var candidate in candidates)
        {
            var local = TimeZoneInfo.ConvertTime(now, LocalTime.Zone(candidate.TimeZone));
            if (local.Month == 12 && local.Day == 31 && local.Hour == 23 && local.Minute >= 30
                && !IsOtherSite(candidate.Register) && !await ScheduledAsync(candidate.Register, local.Year, ct)
                && await FiscalReadyAsync(candidate.Register, ct))
            {
                var result = await YearlyReceiptAsync(candidate.Register.Id, ct);
                if (result.Error is { } error)
                {
                    failed(candidate.Register.Id, error.Kind);
                }
            }
        }
    }

    private Task<bool> ScheduledAsync(CashRegister register, int year, CancellationToken ct) =>
        db.Database.SqlQuery<int>($"""
            SELECT 1 AS "Value" FROM aispa_maintenance.fiscal_history_checkpoint m
            JOIN aispa."CashRegisters" c ON c."Id" = m.register_id AND c."TenantId" = m.tenant_id
            WHERE c."Id" = {register.Id} AND ({db.AllTenants} OR c."TenantId" = {db.CurrentTenantId})
              AND m.register_code = c."Code" AND m.scheduled_year = {year}
              AND m.scheduled_nr = c."LastReceiptNumber" AND m.scheduled_counter = c."TurnoverCounter"
              AND NOT EXISTS (SELECT 1 FROM aispa."Receipts" r WHERE r."RegisterId" = c."Id"
                  AND r."TenantId" = c."TenantId" AND r."Number" > c."LastReceiptNumber")
            """).AnyAsync(ct);

    private Task<int> MarkScheduledAsync(CashRegister register, int year, CancellationToken ct) =>
        db.Database.ExecuteSqlAsync($"""
            INSERT INTO aispa_maintenance.fiscal_history_checkpoint
                (tenant_id, register_id, register_code, scheduled_year, scheduled_nr, scheduled_counter)
            SELECT "TenantId", "Id", "Code", {year}, {register.LastReceiptNumber}, {register.TurnoverCounter}
            FROM aispa."CashRegisters" WHERE "Id" = {register.Id} AND ({db.AllTenants} OR "TenantId" = {db.CurrentTenantId})
            ON CONFLICT (tenant_id, register_id) DO UPDATE SET scheduled_year = excluded.scheduled_year,
                scheduled_nr = excluded.scheduled_nr, scheduled_counter = excluded.scheduled_counter
            """, ct);

    internal async Task<(YearlyReceiptStatusDto? Status, List<RegisterConfirmationDto> Confirmations, List<string> Warnings,
        List<YearlyReceiptStatusDto> Pending)> ClosingStatusAsync(
        CashRegister register, DateOnly today, CancellationToken ct)
    {
        var stored = await db.Set<RegisterConfirmation>().AsNoTracking().Where(c => c.RegisterId == register.Id)
            .OrderBy(c => c.YearKey).ThenBy(c => c.Kind).ToListAsync(ct);
        var users = stored.Select(c => c.ConfirmedBy).Distinct().ToArray();
        var names = await db.Users.Where(u => users.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.DisplayName ?? u.UserName, ct);
        var confirmations = stored.Where(c => c.Kind != RegisterConfirmationKind.YearlyReceiptCheck)
            .Select(c => c.ToDto(names.GetValueOrDefault(c.ConfirmedBy))).ToList();
        var warnings = new List<string>();
        if (register.Country != FiscalCountry.AT)
        {
            return (null, confirmations, warnings, []);
        }

        var closingPeriod = today.Month == 12 && today.Day >= 15;
        var canCreate = register.IsActive && !IsOtherSite(register) && closingPeriod;
        var currentYear = today.Month == 12 ? today.Year : today.Year - 1;
        var history = await FiscalYearsAsync(register, ct);
        if (history.Unknown)
        {
            warnings.Add($"Kassa {register.Code}: Historische Fiskalzeit oder Zählerstand sind nicht eindeutig. Den offenen Abschluss fachlich klären.");
            canCreate = false;
        }
        YearlyReceiptStatusDto? status = null;
        var pending = new List<YearlyReceiptStatusDto>();
        foreach (var year in history.Years.Append(currentYear).Distinct().Order())
        {
            var receipt = history.Valid(year);
            var priorOperatingYear = year < today.Year && history.Years.Contains(year);
            var confirmation = stored.Find(c => c.YearKey == year && receipt != null && c.ReceiptId == receipt.Value);
            if (confirmation is not null)
            {
                confirmations.Add(confirmation.ToDto(names.GetValueOrDefault(confirmation.ConfirmedBy)));
            }
            else if (receipt is not null)
            {
                pending.Add(new(year, receipt.Value, false, false));
                warnings.Add($"Kassa {register.Code}: Jahresbeleg {year} bis 15.02.{year + 1} mit der Belegcheck-App prüfen.");
            }
            else if (register.IsActive && (priorOperatingYear || year == today.Year && closingPeriod))
            {
                var reason = priorOperatingYear && !history.CanClose(year, register.TurnoverCounter)
                    ? " Der aktuelle Umsatzzähler weicht vom Jahresendstand ab. Den offenen Abschluss fachlich klären." : "";
                warnings.Add($"Kassa {register.Code}: Der gültige Jahresbeleg {year} fehlt.{reason}");
            }
            else if (!priorOperatingYear || !history.Years.Any(laterYear => laterYear > year))
            {
                continue;
            }

            if (year == currentYear)
            {
                status = new(year, receipt, confirmation is not null, canCreate);
            }
            else if (receipt is null)
            {
                pending.Add(new(year, null, false, false));
            }
        }
        return (status, confirmations, warnings, pending);
    }

    private sealed class FiscalYearHistory(List<FiscalYearRow> rows)
    {
        private readonly Dictionary<int, FiscalYearRow> _activity = rows.Where(r => r.Tag == 0 && r.Year != null)
            .ToDictionary(r => r.Year!.Value);
        private readonly List<FiscalYearRow> _closures = rows.Where(r => r.Tag == 1).ToList();

        public bool Unknown { get; } = rows.Count == 0 || rows.Any(r => r.Year == null || r.Uncertain);
        public DateTime? LastLocal { get; } = rows.FirstOrDefault(r => r.Tag == 2)?.Local;
        public IEnumerable<int> Years => _activity.Keys.Order();
        public decimal TotalCents => _activity.Values.Sum(r => r.Amount) * 100m;
        public bool HasYearly => _closures.Count != 0;
        public decimal[] PendingCounters(int year) => Years.Where(y => y < year && Valid(y) == null)
            .Select(y => EndCounter(y) * 100m).Distinct().ToArray();
        public bool Attempted(int year) => _closures.Any(r => r.Year == year);
        private decimal EndCounter(int year) => _activity.Where(p => p.Key <= year).Sum(p => p.Value.Amount);
        public bool CanClose(int year, long counter) => !Unknown && EndCounter(year) * 100m == counter;

        public Guid? Valid(int year)
        {
            if (Unknown || !_activity.ContainsKey(year))
            {
                return null;
            }
            var last = _activity.Where(p => p.Key <= year).Max(p => p.Value.Number);
            var end = EndCounter(year);
            return _closures.Where(c => c.Year == year && c.Signed && c.Number >= last && c.Amount == end)
                .OrderByDescending(c => c.Number).Select(c => c.Id).FirstOrDefault();
        }
    }

    private sealed record FiscalYearRow(int Tag, int? Year, long Number, decimal Amount, Guid? Id, bool Signed, bool Uncertain, DateTime? Local);

    private sealed record HistoryCheckpoint(long Number, DateOnly? FiscalDate, long Counter, decimal[] Pending);

    private async Task<DateOnly?> PrepareYearsAsync(CashRegister register, DateTimeOffset now, TimeZoneInfo zone, CancellationToken ct)
    {
        var year = LocalTime.LocalDate(now, zone).Year;
        var checkpoint = await db.Database.SqlQuery<HistoryCheckpoint>($"""
            SELECT through_nr AS "Number", fiscal_date AS "FiscalDate", turnover AS "Counter", pending AS "Pending"
            FROM aispa_maintenance.fiscal_history_checkpoint
            WHERE register_id = {register.Id} AND register_code = {register.Code}
              AND ({db.AllTenants} OR tenant_id = {db.CurrentTenantId})
              AND NOT EXISTS (SELECT 1 FROM aispa."Receipts" r WHERE r."RegisterId" = {register.Id}
                AND r."TenantId" = tenant_id AND r."Number" > {register.LastReceiptNumber})
            """).SingleOrDefaultAsync(ct);
        if (checkpoint is { Number: > 0, FiscalDate: { } date } && date.Year == year
            && checkpoint.Number <= register.LastReceiptNumber && !checkpoint.Pending.Contains(register.TurnoverCounter))
        {
            if (checkpoint.Number == register.LastReceiptNumber && checkpoint.Counter == register.TurnoverCounter)
            {
                return date;
            }
            var tail = await FiscalYearsAsync(register, ct, checkpoint.Number);
            if (!tail.Unknown && !tail.HasYearly && tail.LastLocal is { } last
                && tail.Years.All(y => y == year) && checkpoint.Counter + tail.TotalCents == register.TurnoverCounter)
            {
                await StoreCheckpointAsync(register, last, checkpoint.Pending, ct);
                return DateOnly.FromDateTime(last);
            }
        }

        var history = await FiscalYearsAsync(register, ct);
        if (!history.Unknown && history.LastLocal is { } head && head.Year == year && history.Years.All(y => y <= year)
            && history.TotalCents == register.TurnoverCounter)
        {
            await StoreCheckpointAsync(register, head, history.PendingCounters(year), ct);
        }
        await CloseEarlierYearsAsync(register, history, year, zone, now, ct);
        return history.LastLocal is { } local ? DateOnly.FromDateTime(local) : null;
    }

    private Task<int> StoreCheckpointAsync(CashRegister register, DateTime stamp, decimal[] pending, CancellationToken ct) =>
        db.Database.ExecuteSqlAsync($"""
            INSERT INTO aispa_maintenance.fiscal_history_checkpoint
                (tenant_id, register_id, register_code, through_nr, fiscal_date, turnover, pending)
            SELECT "TenantId", "Id", "Code", {register.LastReceiptNumber}, {DateOnly.FromDateTime(stamp)}, {register.TurnoverCounter}, {pending}
            FROM aispa."CashRegisters" WHERE "Id" = {register.Id} AND ({db.AllTenants} OR "TenantId" = {db.CurrentTenantId})
            ON CONFLICT (tenant_id, register_id) DO UPDATE SET register_code = excluded.register_code, through_nr = excluded.through_nr,
                fiscal_date = excluded.fiscal_date, turnover = excluded.turnover, pending = excluded.pending
            """, ct);

    private async Task<FiscalYearHistory> FiscalYearsAsync(CashRegister register, CancellationToken ct, long through = 0)
    {
        // PostgreSQL fasst die unveränderlichen RKSV-Daten auf eine Zeile je Betriebsjahr und je
        // Jahresbeleg zusammen. Kein Belegkörper und keine Signatur verlassen dafür die Datenbank.
        var query = db.Database.SqlQuery<FiscalYearRow>($$"""
            WITH parts AS (
                SELECT r."Id" AS id, r."Number" AS nr, r."ClosingYear" AS target, r."Kind" AS kind,
                       NOT r."SignatureDeviceFailed" AND r."FiscalSignature" IS NOT NULL AS signed,
                       string_to_array(r."FiscalCode", '_') AS f
                FROM aispa."Receipts" r JOIN aispa."CashRegisters" c
                    ON c."Id" = r."RegisterId" AND c."TenantId" = r."TenantId"
                WHERE r."RegisterId" = {{register.Id}} AND r."Number" BETWEEN {{(through == 0 ? long.MinValue : checked(through + 1))}}
                    AND {{(through == 0 ? long.MaxValue : register.LastReceiptNumber)}}
                  AND ({{db.AllTenants}} OR r."TenantId" = {{db.CurrentTenantId}})
            ), facts AS (
                SELECT *, CASE WHEN cardinality(f) = 14 AND f[2] LIKE 'R1-%' AND f[3] = {{register.Code}}
                    AND f[4] = nr::text AND f[5] ~ '^[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}:[0-9]{2}$'
                    AND pg_input_is_valid(f[5], 'timestamp') THEN f[5]::timestamp END AS stamp,
                    CASE WHEN (SELECT count(*) FROM unnest(f[6:10]) a WHERE a ~ '^-?[0-9]+,[0-9]{2}$') = 5
                    THEN (SELECT sum(replace(a, ',', '.')::numeric) FROM unnest(f[6:10]) a) END AS gross
                FROM parts
            ), chain AS (
                SELECT *, extract(year FROM stamp)::int AS yr, lag(extract(year FROM stamp)::int) OVER seq AS prev,
                       {{through}} + row_number() OVER seq AS ordinal, sum(gross) OVER seq AS running
                FROM facts WINDOW seq AS (ORDER BY nr)
            )
            SELECT 0 AS "Tag", yr AS "Year", max(nr) AS "Number", coalesce(sum(gross), 0) AS "Amount",
                   NULL::uuid AS "Id", false AS "Signed", bool_or(gross IS NULL OR nr <> ordinal) AS "Uncertain", NULL::timestamp AS "Local"
                FROM chain GROUP BY yr
            UNION ALL
            SELECT 1, coalesce(target, CASE WHEN prev < yr THEN prev END), nr, coalesce(running, 0), id, signed,
                   coalesce(target > yr, false), stamp FROM chain WHERE kind = 'Yearly'
            UNION ALL
            (SELECT 2, yr, nr, 0, NULL::uuid, false, nr <> {{register.LastReceiptNumber}}, stamp FROM chain ORDER BY nr DESC LIMIT 1)
            """);
        return new(await (through == 0 ? query : query.TagWith("RKSV-Nachlauf")).ToListAsync(ct));
    }

    private async Task<bool> CloseEarlierYearsAsync(CashRegister register, FiscalYearHistory history, int currentYear,
        TimeZoneInfo zone, DateTimeOffset now, CancellationToken ct)
        => await CloseEarlierYearsAsync(register, history, currentYear, zone, now, fiscal.For(register.Country),
            r => db.Receipts.Add(r), () => FiscalReadyAsync(register, ct), ct);

    private static async Task<bool> CloseEarlierYearsAsync(CashRegister register, FiscalYearHistory history, int currentYear,
        TimeZoneInfo zone, DateTimeOffset now, IFiscalization adapter, Action<Receipt> add, Func<Task<bool>> available, CancellationToken ct)
    {
        var issued = false;
        foreach (var year in history.Years.Where(y => y < currentYear))
        {
            if (history.Valid(year) is null && history.CanClose(year, register.TurnoverCounter)
                && (!history.Attempted(year) || await available()))
            {
                await IssueCoreAsync(register, ReceiptKind.Yearly, adapter, zone, now,
                    (number, at) => new Receipt { RegisterId = register.Id, Number = number, IssuedAt = at, Kind = ReceiptKind.Yearly, ClosingYear = year },
                    false, add, ct);
                issued = true;
            }
        }
        return issued;
    }

    internal static async Task<DateOnly?> PrepareMemoryYearsAsync(CashRegister register, IReadOnlyList<Receipt> receipts, IFiscalization adapter,
        TimeZoneInfo zone, DateTimeOffset now, Action<Receipt> add, CancellationToken ct)
    {
        if (register.Country != FiscalCountry.AT || receipts.Count == 0)
        {
            return null;
        }
        var rows = new List<FiscalYearRow>();
        var activity = new Dictionary<int, FiscalYearRow>();
        decimal running = 0;
        DateTime? last = null;
        long previousNumber = 0;
        foreach (var receipt in receipts.OrderBy(r => r.Number))
        {
            var parts = receipt.FiscalCode?.Split('_');
            if (parts is not { Length: 14 } || !parts[1].StartsWith("R1-", StringComparison.Ordinal) || parts[2] != register.Code
                || parts[3] != receipt.Number.ToString(CultureInfo.InvariantCulture) || receipt.Number != previousNumber + 1
                || !DateTime.TryParseExact(parts[4], "yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var stamp))
            {
                return null;
            }
            var gross = receipt.Lines.Sum(l => l.Amount);
            running += gross;
            var before = activity.GetValueOrDefault(stamp.Year)?.Amount ?? 0;
            activity[stamp.Year] = new(0, stamp.Year, receipt.Number, before + gross, null, false, false, null);
            if (receipt.Kind == ReceiptKind.Yearly)
            {
                var target = receipt.ClosingYear ?? (last?.Year < stamp.Year ? last.Value.Year : (int?)null);
                rows.Add(new(1, target, receipt.Number, running, receipt.Id, !receipt.SignatureDeviceFailed && receipt.FiscalSignature != null,
                    target > stamp.Year, stamp));
            }
            last = stamp;
            previousNumber = receipt.Number;
        }
        rows.AddRange(activity.Values);
        await CloseEarlierYearsAsync(register, new(rows), LocalTime.LocalDate(now, zone).Year, zone, now, adapter, add,
            () => adapter.IsDeviceAvailableAsync(register, ct), ct);
        return last.HasValue ? DateOnly.FromDateTime(last.Value) : null;
    }

    public Task<PosResult<ReceiptDto>> YearlyReceiptAsync(Guid registerId, CancellationToken ct) =>
        ReceiptTransactionAsync(registerId, async register =>
        {
            EnsureActive(register);
            var zone = await ZoneAsync(register, ct);
            var today = LocalTime.LocalDate(time.GetUtcNow(), zone);
            if (register.Country != FiscalCountry.AT || today.Month != 12 || today.Day < 15)
            {
                throw PosFailure.Conflict("Der Jahresbeleg kann für österreichische Kassen ab dem 15. Dezember erstellt werden.");
            }

            var history = await FiscalYearsAsync(register, ct);
            if (!history.CanClose(today.Year, register.TurnoverCounter))
            {
                throw PosFailure.Conflict("Der Jahresendstand ist aus der Belegkette nicht eindeutig bestimmbar.");
            }
            var added = await CloseEarlierYearsAsync(register, history, today.Year, zone, time.GetUtcNow(), ct);
            var earlierComplete = !history.Years.Any(y => y < today.Year && history.Valid(y) == null
                && history.CanClose(y, register.TurnoverCounter)
                && !db.Receipts.Local.Any(r => r.RegisterId == register.Id && r.ClosingYear == y
                    && !r.SignatureDeviceFailed && r.FiscalSignature != null && db.Entry(r).State == EntityState.Added));
            if (!added && history.Valid(today.Year) is { } existing)
            {
                if (earlierComplete)
                {
                    await MarkScheduledAsync(register, today.Year, ct);
                }
                return await db.Receipts.AsNoTracking().SingleAsync(r => r.Id == existing, ct);
            }
            var receipt = await IssueAsync(register, ReceiptKind.Yearly, ct, automatic: false);
            receipt.ClosingYear = today.Year;
            if (earlierComplete && !receipt.SignatureDeviceFailed && receipt.FiscalSignature != null)
            {
                await MarkScheduledAsync(register, today.Year, ct);
            }
            return receipt;
        }, ct);

    public async Task<PosResult<RegisterConfirmationDto>> ConfirmAsync(Guid registerId, RegisterConfirmationKind kind, int? year,
        Guid? receiptId, string? userId, bool remove, CancellationToken ct)
    {
        RegisterConfirmationDto? result = null;
        var (_, error) = await InRegisterAsync(registerId, async register =>
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                throw PosFailure.Forbidden("Die bestätigende Person fehlt.");
            }

            var yearly = kind == RegisterConfirmationKind.YearlyReceiptCheck;
            var permitted = register.Country switch
            {
                FiscalCountry.AT => kind is RegisterConfirmationKind.FinanzOnlineRegistration or RegisterConfirmationKind.StartReceiptCheck or RegisterConfirmationKind.YearlyReceiptCheck,
                FiscalCountry.DE => kind == RegisterConfirmationKind.ElsterRegistration,
                _ => false,
            };
            if (!permitted || (yearly ? year is not (>= 1 and < 9999) || receiptId == null : year != null || receiptId != null))
            {
                throw PosFailure.Invalid("kind", "Diese Bestätigung passt nicht zur Kassa oder zum Beleg.");
            }

            if (yearly && !remove && (await FiscalYearsAsync(register, ct)).Valid(year!.Value) != receiptId)
            {
                throw PosFailure.Conflict("Für diese Prüfung ist ein gültiger signierter Jahresbeleg erforderlich.");
            }
            if (!remove && kind == RegisterConfirmationKind.StartReceiptCheck &&
                !await db.Receipts.AnyAsync(r => r.RegisterId == registerId && r.Kind == ReceiptKind.Start
                    && !r.SignatureDeviceFailed && r.FiscalSignature != null, ct))
            {
                throw PosFailure.Conflict("Ein gültiger signierter Startbeleg fehlt.");
            }

            var set = db.Set<RegisterConfirmation>();
            var key = year ?? 0;
            var stored = await set.SingleOrDefaultAsync(c => c.RegisterId == registerId && c.Kind == kind && c.YearKey == key, ct);
            if (remove)
            {
                if (stored is not null && stored.ReceiptId == receiptId)
                {
                    set.Remove(stored);
                }
            }
            else
            {
                if (stored is null)
                {
                    stored = new() { RegisterId = registerId, Kind = kind, YearKey = key, ConfirmedBy = userId };
                    set.Add(stored);
                }
                stored.ConfirmedBy = userId;
                stored.ConfirmedAt = time.GetUtcNow();
                stored.ReceiptId = receiptId;
                var name = await db.Users.Where(u => u.Id == userId).Select(u => u.DisplayName ?? u.UserName).SingleOrDefaultAsync(ct);
                result = stored.ToDto(name);
            }
            return registerId;
        }, ct);
        return new(result, error);
    }
}

internal sealed partial class YearlyReceiptScheduler(IServiceScopeFactory scopes, StartupState startup, TimeProvider time,
    ILogger<YearlyReceiptScheduler> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1), time);
        while (await timer.WaitForNextTickAsync(stoppingToken) && await startup.WaitUntilReadyAsync(stoppingToken))
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                SchedulerFailed(logger);
            }
        }
    }

    internal async Task RunOnceAsync(CancellationToken ct)
    {
        var now = time.GetUtcNow().UtcDateTime;
        // Weltweit liegt der lokale 31.12. ab 23:30 zwischen UTC+14 und UTC−12.
        if (!(now.Month == 12 && now.Day == 31 && now.TimeOfDay >= new TimeSpan(9, 30, 0))
            && !(now.Month == 1 && now.Day == 1 && now.Hour < 12))
        {
            return;
        }
        await using var catalog = scopes.CreateAsyncScope();
        var tenants = await catalog.ServiceProvider.GetRequiredService<AispaDbContext>().Tenants
            .Where(t => t.LockedAt == null).Select(t => t.Id).ToListAsync(ct);
        foreach (var tenantId in tenants)
        {
            await using var scope = scopes.CreateAsyncScope();
            scope.ServiceProvider.GetRequiredService<TenantContext>().Use(tenantId);
            await scope.ServiceProvider.GetRequiredService<PosService>()
                .ScheduleYearlyAsync(time.GetUtcNow(), (id, error) => RegisterFailed(logger, id, error), ct);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Der geplante Jahresabschluss konnte nicht ausgeführt werden.")]
    private static partial void SchedulerFailed(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Jahresabschluss für Kassa {RegisterId} fehlgeschlagen ({ErrorKind}).")]
    private static partial void RegisterFailed(ILogger logger, Guid registerId, PosErrorKind errorKind);
}
