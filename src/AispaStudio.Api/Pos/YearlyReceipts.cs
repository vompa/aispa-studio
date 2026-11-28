using AispaStudio.Api.Scheduling;
using AispaStudio.Api.Data;
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
    internal async Task ScheduleYearlyAsync(DateTimeOffset now, CancellationToken ct)
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
                && !IsOtherSite(candidate.Register) && await FiscalAvailableAsync(candidate.Register, ct))
            {
                await YearlyReceiptAsync(candidate.Register.Id, ct);
            }
        }
    }

    internal async Task<(YearlyReceiptStatusDto? Status, List<RegisterConfirmationDto> Confirmations, List<string> Warnings,
        List<YearlyReceiptStatusDto> Pending)> ClosingStatusAsync(
        CashRegister register, DateOnly today, TimeZoneInfo zone, CancellationToken ct)
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

        var canCreate = register.IsActive && !IsOtherSite(register) && today.Month == 12 && today.Day >= 15;
        var currentYear = today.Month == 12 ? today.Year : today.Year - 1;
        var years = await db.Receipts.Where(r => r.RegisterId == register.Id && r.ClosingYear != null)
            .Select(r => r.ClosingYear!.Value).Distinct().ToListAsync(ct);
        YearlyReceiptStatusDto? status = null;
        var pending = new List<YearlyReceiptStatusDto>();
        foreach (var year in years.Append(currentYear).Distinct().Order())
        {
            var receipt = await CurrentYearlyAsync(register.Id, year, zone, ct);
            var confirmation = stored.Find(c => c.YearKey == year && receipt != null && c.ReceiptId == receipt.Id);
            if (confirmation is not null)
            {
                confirmations.Add(confirmation.ToDto(names.GetValueOrDefault(confirmation.ConfirmedBy)));
            }
            else if (receipt is not null)
            {
                pending.Add(new(year, receipt.Id, false, false));
                warnings.Add($"Kassa {register.Code}: Jahresbeleg {year} bis 15.02.{year + 1} mit der Belegcheck-App prüfen.");
            }
            else if (register.IsActive && LocalTime.LocalDate(register.CreatedAt, zone).Year <= year
                && (today.Year > year || today.Month == 12 && today.Day >= 15))
            {
                warnings.Add($"Kassa {register.Code}: Der gültige Jahresbeleg {year} fehlt.");
            }

            if (year == currentYear)
            {
                status = new(year, receipt?.Id, confirmation is not null, canCreate);
            }
        }
        return (status, confirmations, warnings, pending);
    }

    private async Task<Receipt?> CurrentYearlyAsync(Guid registerId, int year, TimeZoneInfo zone, CancellationToken ct)
    {
        var nextYear = TimeZoneInfo.ConvertTimeToUtc(new DateTime(year + 1, 1, 1, 0, 0, 0, DateTimeKind.Unspecified), zone);
        var receipts = db.Receipts.AsNoTracking().Where(r => r.RegisterId == registerId);
        return await receipts.Where(r => r.ClosingYear == year && !r.SignatureDeviceFailed && r.FiscalSignature != null
            && !receipts.Any(later => later.Number > r.Number && later.IssuedAt < nextYear))
            .OrderByDescending(r => r.Number).FirstOrDefaultAsync(ct);
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

            var existing = await CurrentYearlyAsync(registerId, today.Year, zone, ct);
            if (existing is not null)
            {
                return existing;
            }

            var receipt = await IssueAsync(register, ReceiptKind.Yearly, ct, automatic: false);
            receipt.ClosingYear = today.Year;
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

            if (yearly && !remove && (await CurrentYearlyAsync(registerId, year!.Value, await ZoneAsync(register, ct), ct))?.Id != receiptId)
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
        await using var catalog = scopes.CreateAsyncScope();
        var tenants = await catalog.ServiceProvider.GetRequiredService<AispaDbContext>().Tenants
            .Where(t => t.LockedAt == null).Select(t => t.Id).ToListAsync(ct);
        foreach (var tenantId in tenants)
        {
            await using var scope = scopes.CreateAsyncScope();
            scope.ServiceProvider.GetRequiredService<TenantContext>().Use(tenantId);
            await scope.ServiceProvider.GetRequiredService<PosService>().ScheduleYearlyAsync(time.GetUtcNow(), ct);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Der geplante Jahresabschluss konnte nicht ausgeführt werden.")]
    private static partial void SchedulerFailed(ILogger logger);
}
