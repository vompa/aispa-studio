using AispaStudio.Api.Data;
using AispaStudio.Api.Scheduling;
using AispaStudio.Api.Tenancy;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;
using Wolverine;

namespace AispaStudio.Api.Pos.Fiscal;

/// <summary>Angeforderter TSE-Export einer deutschen Kasse; die TAR-Datei liegt nach dem Export in der Datenbank.</summary>
public sealed class TseExport
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid RegisterId { get; init; }
    public DateOnly From { get; init; }
    public DateOnly Until { get; init; }
    public TseExportStatus Status { get; set; }
    public DateTimeOffset RequestedAt { get; init; }
    public string? RequestedBy { get; init; }
    public DateTimeOffset? CompletedAt { get; set; }
    public string? FileName { get; set; }
    public byte[]? Content { get; set; }
    public string? Error { get; set; }
}

public sealed record RunTseExport(Guid TseExportId);

public static class RunTseExportHandler
{
    public static async Task Handle(RunTseExport message, IServiceScopeFactory scopes, CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AispaDbContext>();
        if (await TenantModel.TenantOfAsync(db.Set<TseExport>().Where(e => e.Id == message.TseExportId), ct) is not { } tenantId)
        {
            return;
        }

        scope.ServiceProvider.GetRequiredService<TenantContext>().Use(tenantId);
        await scope.ServiceProvider.GetRequiredService<TseExports>().RunAsync(message.TseExportId, ct);
    }
}

/// <summary>
/// TSE-Export nach BSI TR-03153 (TAR mit Protokollnachrichten und Zertifikaten) für Kassen-Nachschau und Außenprüfung, neben der DSFinV-K.
/// Eine Cloud-TSE exportiert nur asynchron und kann dauern; deshalb läuft der Export immer über die Queue, die Oberfläche holt die Datei danach.
/// </summary>
internal sealed partial class TseExports(AispaDbContext db, IEnumerable<ITseClient> clients, IMessageBus bus, TimeProvider time, ILogger<TseExports> log)
{
    private const int Shown = 10;

    public async Task<PosResult<TseExportDto>> RequestAsync(Guid registerId, TseExportInput input, string? userId, CancellationToken ct)
    {
        var register = await db.CashRegisters.AsNoTracking().FirstOrDefaultAsync(r => r.Id == registerId, ct);
        if (register is null)
        {
            return PosFailure.NotFound("Kasse").Error;
        }

        if (register.Country != FiscalCountry.DE || input.Until < input.From || input.Until.DayNumber - input.From.DayNumber > 366)
        {
            return PosFailure.Invalid("until", "Den TSE-Export gibt es nur für deutsche Kassen, für höchstens ein Jahr.").Error;
        }

        var export = new TseExport
        {
            RegisterId = registerId,
            From = input.From,
            Until = input.Until,
            Status = TseExportStatus.Pending,
            RequestedAt = time.GetUtcNow(),
            RequestedBy = userId,
        };
        db.Add(export);
        await db.SaveChangesAsync(ct);
        await bus.PublishAsync(new RunTseExport(export.Id));
        return new PosResult<TseExportDto>(ToDto(export), null);
    }

    public async Task<List<TseExportDto>> ListAsync(Guid registerId, CancellationToken ct) =>
        await db.Set<TseExport>().AsNoTracking().Where(e => e.RegisterId == registerId)
            .OrderByDescending(e => e.RequestedAt).Take(Shown)
            .Select(e => new TseExportDto(e.Id, e.RegisterId, e.From, e.Until, e.Status, e.RequestedAt, e.CompletedAt, e.FileName,
                e.Content == null ? null : e.Content.Length, e.Error))
            .ToListAsync(ct);

    public async Task RunAsync(Guid exportId, CancellationToken ct)
    {
        var export = await db.Set<TseExport>().FirstOrDefaultAsync(e => e.Id == exportId && e.Status == TseExportStatus.Pending, ct);
        if (export is null)
        {
            return;
        }

        var register = await db.CashRegisters.AsNoTracking().FirstAsync(r => r.Id == export.RegisterId, ct);
        var zone = LocalTime.Zone(await db.Locations.Where(l => l.Id == register.LocationId).Select(l => l.TimeZone).FirstAsync(ct));
        var (from, _) = LocalTime.Day(export.From, zone);
        var (_, until) = LocalTime.Day(export.Until, zone);
        byte[]? content = null;
        if (clients.FirstOrDefault() is { } client)
        {
            try
            {
                content = await client.ExportAsync(register.Code, from, until, ct);
            }
            catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or TimeoutException)
            {
                ExportFailed(log, ex, register.Code);
            }
        }

        export.CompletedAt = time.GetUtcNow();
        if (content is null)
        {
            export.Status = TseExportStatus.Failed;
            export.Error = clients.Any() ? "Die TSE hat den Export nicht geliefert. Bitte erneut anfordern." : "Für Deutschland ist keine TSE angebunden.";
        }
        else
        {
            export.Status = TseExportStatus.Done;
            export.Content = content;
            export.FileName = $"tse-export-{register.Code}-{export.From:yyyyMMdd}-{export.Until:yyyyMMdd}.tar";
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task<(byte[] Content, string FileName)?> FileAsync(Guid exportId, CancellationToken ct) =>
        await db.Set<TseExport>().AsNoTracking().Where(e => e.Id == exportId && e.Status == TseExportStatus.Done)
            .Select(e => new { e.Content, e.FileName }).FirstOrDefaultAsync(ct) is { Content: { } content, FileName: { } name }
            ? (content, name)
            : null;

    private static TseExportDto ToDto(TseExport e) =>
        new(e.Id, e.RegisterId, e.From, e.Until, e.Status, e.RequestedAt, e.CompletedAt, e.FileName, e.Content?.Length, e.Error);

    [LoggerMessage(Level = LogLevel.Warning, Message = "TSE-Export der Kasse {Code} fehlgeschlagen")]
    private static partial void ExportFailed(ILogger logger, Exception ex, string code);
}
