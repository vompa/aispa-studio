using AispaStudio.Api.Data;
using AispaStudio.Api.Scheduling;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;

namespace AispaStudio.Api.Stock;

/// <summary>
/// Zählung eines Lagerorts vom Handy (Slice 116): offen, solange gezählt wird, abgeschickt, wenn die Leitung sie abschließen soll, danach
/// geschlossen (gebucht) oder verworfen. Je Lagerort gibt es höchstens eine offene oder abgeschickte Zählung.
/// </summary>
public sealed class StockCountSession
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid LocationId { get; init; }
    public Guid StoragePlaceId { get; init; }
    public string? StartedByUserId { get; init; }
    public DateTimeOffset StartedAt { get; init; }
    public DateTimeOffset? SubmittedAt { get; set; }
    public DateTimeOffset? ClosedAt { get; set; }
    public StockCountSessionStatus Status { get; set; }
    public List<StockCountLine> Lines { get; init; } = [];
}

/// <summary>Gezählte Menge eines Materials in einer Zählung.</summary>
public sealed class StockCountLine
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid SessionId { get; init; }
    public Guid ItemId { get; init; }
    public decimal Counted { get; set; }
}

internal sealed class StockCountSessions(AispaDbContext db, StockService stock, TimeProvider time)
{
    private static readonly StockCountSessionStatus[] Running = [StockCountSessionStatus.Open, StockCountSessionStatus.Submitted];

    public async Task<StockCountSession?> FindAsync(Guid locationId, Guid sessionId, CancellationToken ct) =>
        await db.StockCountSessions.Include(s => s.Lines).FirstOrDefaultAsync(s => s.Id == sessionId && s.LocationId == locationId, ct);

    /// <summary>Startet die Zählung eines Lagerorts oder liefert die laufende zurück, damit zwei Handys nicht gegeneinander zählen.</summary>
    public async Task<(StockCountSession? Session, Errors? Errors)> StartAsync(Guid locationId, Guid placeId, string? userId, CancellationToken ct)
    {
        var running = await RunningAsync(locationId, placeId, ct);
        if (running is not null)
        {
            return (running, null);
        }

        var errors = new Errors();
        errors.Check(await db.StoragePlaces.AnyAsync(p => p.Id == placeId && p.LocationId == locationId && p.IsActive, ct),
            "storagePlaceId", "Lagerort nicht gefunden oder deaktiviert.");
        if (errors.Any)
        {
            return (null, errors);
        }

        var session = new StockCountSession { LocationId = locationId, StoragePlaceId = placeId, StartedByUserId = userId, StartedAt = time.GetUtcNow() };
        db.StockCountSessions.Add(session);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Ein zweites Handy war schneller (eindeutiger Index je Lagerort): dessen Zählung gilt.
            db.Entry(session).State = EntityState.Detached;
            return (await RunningAsync(locationId, placeId, ct) ?? throw new InvalidOperationException("Zählung konnte nicht angelegt werden."), null);
        }

        return (session, null);
    }

    /// <summary>Zwischenstand einer Zeile; <c>null</c> löscht sie. Nur solange die Zählung offen ist.</summary>
    public async Task<Errors?> SetLineAsync(StockCountSession session, Guid itemId, decimal? counted, CancellationToken ct)
    {
        var errors = new Errors();
        errors.Check(session.Status == StockCountSessionStatus.Open, "status", "Die Zählung ist nicht mehr offen.");
        if (counted is { } value)
        {
            errors.Check(value >= 0 && value < 1_000_000 && decimal.Round(value, 3) == value, nameof(StockCountLineInput.Counted),
                "Menge ab 0 mit höchstens drei Nachkommastellen.");
            errors.Check(await db.StockItems.AnyAsync(i => i.Id == itemId && i.IsActive, ct), "itemId", "Material nicht gefunden.");
        }

        if (errors.Any)
        {
            return errors;
        }

        var line = session.Lines.FirstOrDefault(l => l.ItemId == itemId);
        if (counted is null)
        {
            if (line is not null)
            {
                session.Lines.Remove(line);
                db.StockCountLines.Remove(line);
            }
        }
        else if (line is null)
        {
            db.StockCountLines.Add(new StockCountLine { SessionId = session.Id, ItemId = itemId, Counted = counted.Value });
        }
        else
        {
            line.Counted = counted.Value;
        }

        await db.SaveChangesAsync(ct);
        return null;
    }

    public async Task<Errors?> SubmitAsync(StockCountSession session, CancellationToken ct)
    {
        var errors = new Errors();
        errors.Check(session.Status == StockCountSessionStatus.Open, "status", "Die Zählung ist nicht mehr offen.");
        errors.Check(await db.StockCountLines.AnyAsync(l => l.SessionId == session.Id, ct), "lines", "Zähle mindestens ein Material, bevor du abschickst.");
        if (errors.Any)
        {
            return errors;
        }

        var now = time.GetUtcNow();
        return await ClaimAsync(session, [StockCountSessionStatus.Open], x => x.SetProperty(s => s.Status, StockCountSessionStatus.Submitted).SetProperty(s => s.SubmittedAt, now),
            "Die Zählung ist nicht mehr offen.", ct);
    }

    public async Task<Errors?> DiscardAsync(StockCountSession session, CancellationToken ct)
    {
        var errors = new Errors();
        errors.Check(session.Status is StockCountSessionStatus.Open or StockCountSessionStatus.Submitted, "status", "Die Zählung ist schon abgeschlossen oder verworfen.");
        if (errors.Any)
        {
            return errors;
        }

        return await ClaimAsync(session, Running, x => x.SetProperty(s => s.Status, StockCountSessionStatus.Discarded),
            "Die Zählung ist schon abgeschlossen oder verworfen.", ct);
    }

    /// <summary>Ändert den Status nur, wenn die Zählung noch in einem der erlaubten Zustände ist; eine gleichzeitige Änderung (etwa Abschließen) gewinnt.</summary>
    private async Task<Errors?> ClaimAsync(StockCountSession session, StockCountSessionStatus[] from,
        Action<UpdateSettersBuilder<StockCountSession>> change, string lostMessage, CancellationToken ct)
    {
        var claimed = await db.StockCountSessions.Where(s => s.Id == session.Id && from.Contains(s.Status)).ExecuteUpdateAsync(change, ct);
        if (claimed == 0)
        {
            var lost = new Errors();
            lost.Add("status", lostMessage);
            return lost;
        }

        await db.Entry(session).ReloadAsync(ct);
        return null;
    }

    /// <summary>
    /// Bucht jede Zeile über denselben Weg wie die Inventur der Windows-App (Art Inventur, Differenz zum Bestand zum Abschluss). Statusprüfung und Buchung
    /// laufen in einer Transaktion; ein zweiter Aufruf findet die Zählung nicht mehr abgeschickt und bucht nichts.
    /// </summary>
    public async Task<Errors?> CloseAsync(StockCountSession session, string? userId, CancellationToken ct)
    {
        var errors = new Errors();
        errors.Check(session.Status == StockCountSessionStatus.Submitted, "status", "Nur eine abgeschickte Zählung lässt sich abschließen.");
        if (errors.Any)
        {
            return errors;
        }

        var now = time.GetUtcNow();
        return await db.Database.CreateExecutionStrategy().ExecuteAsync<Errors?>(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            var claimed = await db.StockCountSessions.Where(s => s.Id == session.Id && s.Status == StockCountSessionStatus.Submitted)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, StockCountSessionStatus.Closed).SetProperty(x => x.ClosedAt, now), ct);
            if (claimed == 0)
            {
                var lost = new Errors();
                lost.Add("status", "Die Zählung ist schon abgeschlossen.");
                return lost;
            }

            foreach (var line in session.Lines.OrderBy(l => l.ItemId))
            {
                var (_, failed) = await stock.AddAsync(session.LocationId,
                    new StockMovementInput(line.ItemId, StockMovementKind.Count, line.Counted, StoragePlaceId: session.StoragePlaceId, Reason: "Inventur vom Handy"),
                    userId, ct);
                if (failed is not null)
                {
                    await transaction.RollbackAsync(ct);
                    return failed;
                }
            }

            await transaction.CommitAsync(ct);
            return null;
        });
    }

    public async Task<List<StockCountSessionDto>> ListAsync(Guid locationId, StockCountSessionStatus? status, CancellationToken ct)
    {
        var sessions = await db.StockCountSessions.AsNoTracking().Include(s => s.Lines)
            .Where(s => s.LocationId == locationId && (status == null || s.Status == status))
            .OrderByDescending(s => s.StartedAt).ThenBy(s => s.Id).ToListAsync(ct);
        var dtos = new List<StockCountSessionDto>();
        foreach (var session in sessions)
        {
            dtos.Add(await ToDtoAsync(session, withExpected: true, ct));
        }

        return dtos;
    }

    /// <summary>Die Zählung als Antwort. Mit <paramref name="withExpected"/> (Leitung) steht neben dem Gezählten der Bestand des Lagerorts, das Handy zählt blind.</summary>
    public async Task<StockCountSessionDto> ToDtoAsync(StockCountSession session, bool withExpected, CancellationToken ct)
    {
        var itemIds = session.Lines.Select(l => l.ItemId).ToList();
        var items = await db.StockItems.AsNoTracking().Where(i => itemIds.Contains(i.Id)).ToDictionaryAsync(i => i.Id, ct);
        var expected = withExpected
            ? await db.StockMovements.AsNoTracking().Where(m => m.StoragePlaceId == session.StoragePlaceId && itemIds.Contains(m.ItemId))
                .GroupBy(m => m.ItemId).Select(g => new { ItemId = g.Key, Quantity = g.Sum(m => m.Quantity) })
                .ToDictionaryAsync(g => g.ItemId, g => g.Quantity, ct)
            : [];
        var placeName = await db.StoragePlaces.AsNoTracking().Where(p => p.Id == session.StoragePlaceId).Select(p => p.Name).FirstAsync(ct);
        var startedBy = session.StartedByUserId is { } userId
            ? await db.Users.AsNoTracking().Where(u => u.Id == userId).Select(u => u.DisplayName).FirstOrDefaultAsync(ct)
            : null;
        var lines = session.Lines.Where(l => items.ContainsKey(l.ItemId))
            .Select(l => new StockCountLineDto(l.ItemId, items[l.ItemId].Name, items[l.ItemId].Unit, l.Counted,
                withExpected ? expected.GetValueOrDefault(l.ItemId) : null))
            .OrderBy(l => l.Name, StringComparer.Ordinal).ThenBy(l => l.ItemId).ToList();
        return new(session.Id, session.LocationId, session.StoragePlaceId, placeName, session.Status, startedBy, session.StartedAt, session.SubmittedAt,
            session.ClosedAt, lines);
    }

    private Task<StockCountSession?> RunningAsync(Guid locationId, Guid placeId, CancellationToken ct) =>
        db.StockCountSessions.Include(s => s.Lines).FirstOrDefaultAsync(s => s.LocationId == locationId && s.StoragePlaceId == placeId && Running.Contains(s.Status), ct);
}

internal static class StockCountSessionModel
{
    public static void Configure(ModelBuilder builder)
    {
        builder.Entity<StockCountSession>(e =>
        {
            e.Property(s => s.Id).ValueGeneratedNever();
            e.Property(s => s.StartedByUserId).HasMaxLength(450);
            e.Property(s => s.Status).HasConversion<string>().HasMaxLength(20);
            e.HasOne<Location>().WithMany().HasForeignKey(s => s.LocationId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<StoragePlace>().WithMany().HasForeignKey(s => s.StoragePlaceId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(s => new { s.LocationId, s.Status });
            // Je Lagerort höchstens eine laufende Zählung.
            e.HasIndex(s => s.StoragePlaceId).IsUnique().HasDatabaseName("IX_StockCountSessions_StoragePlaceId_Running")
                .HasFilter("\"Status\" IN ('Open', 'Submitted')");
            e.HasMany(s => s.Lines).WithOne().HasForeignKey(l => l.SessionId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<StockCountLine>(e =>
        {
            e.Property(l => l.Id).ValueGeneratedNever();
            e.Property(l => l.Counted).HasPrecision(12, 3);
            e.HasOne<StockItem>().WithMany().HasForeignKey(l => l.ItemId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(l => new { l.SessionId, l.ItemId }).IsUnique();
        });
    }
}
