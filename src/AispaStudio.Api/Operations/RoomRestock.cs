using AispaStudio.Api.Data;
using AispaStudio.Api.Scheduling;
using AispaStudio.Api.Stock;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Operations;

/// <summary>Ein Material der Raumausstattung: Bestand im Raumschrank gegen die Mindestmenge des Raums.</summary>
internal sealed record RoomLevel(Guid PlaceId, Guid ItemId, string ItemName, string Unit, decimal Quantity, decimal Minimum)
{
    public bool IsBelow => Quantity < Minimum;

    public decimal Suggested => Math.Max(0, Minimum - Quantity);

    public RestockLine ToLine(RestockState state) => new(ItemId, ItemName, Unit, Quantity, Minimum, Suggested, state);
}

/// <summary>
/// Raumcheck nach der Behandlung (ADR 0068): Die Mindestmengen des Raumschranks (Lagerort der Art Raum) sind die Raumausstattung. Nach dem Verbrauch zeigt die
/// Raumpflege-Aufgabe die Materialien unter ihrer Mindestmenge; je Zeile heißt die Antwort „aufgefüllt“ (bucht Nachfüllen vom Hauptlager) oder „reicht noch“.
/// </summary>
internal sealed class RoomRestockService(AispaDbContext db, StockService stock, TimeProvider time)
{
    public const string RefillReason = "Raumcheck nach Termin";

    /// <summary>Raumausstattung je Raum: alle Materialien mit Mindestmenge im aktiven Raumschrank, mit aktuellem Bestand; Räume ohne Raumschrank oder Mindestmengen fehlen.</summary>
    public async Task<Dictionary<Guid, List<RoomLevel>>> EquipmentAsync(IReadOnlyCollection<Guid> roomIds, CancellationToken ct)
    {
        var cabinets = await db.StoragePlaces.AsNoTracking()
            .Where(p => p.Kind == StoragePlaceKind.Room && p.IsActive && p.RoomId != null && roomIds.Contains(p.RoomId.Value)).ToListAsync(ct);
        var placeIds = cabinets.Select(p => p.Id).ToList();
        var minimums = await db.StoragePlaceMinimums.AsNoTracking().Where(m => placeIds.Contains(m.StoragePlaceId)).ToListAsync(ct);
        var itemIds = minimums.Select(m => m.ItemId).Distinct().ToList();
        var items = await db.StockItems.AsNoTracking().Where(i => i.IsActive && itemIds.Contains(i.Id)).ToDictionaryAsync(i => i.Id, ct);
        var quantities = (await db.StockMovements.AsNoTracking().Where(m => placeIds.Contains(m.StoragePlaceId) && itemIds.Contains(m.ItemId))
                .GroupBy(m => new { m.StoragePlaceId, m.ItemId }).Select(g => new { g.Key.StoragePlaceId, g.Key.ItemId, Quantity = g.Sum(m => m.Quantity) }).ToListAsync(ct))
            .ToDictionary(g => (g.StoragePlaceId, g.ItemId), g => g.Quantity);

        var result = new Dictionary<Guid, List<RoomLevel>>();
        foreach (var cabinet in cabinets)
        {
            var levels = minimums.Where(m => m.StoragePlaceId == cabinet.Id && items.ContainsKey(m.ItemId))
                .Select(m => new RoomLevel(cabinet.Id, m.ItemId, items[m.ItemId].Name, items[m.ItemId].Unit, quantities.GetValueOrDefault((cabinet.Id, m.ItemId)), m.MinimumQuantity))
                .OrderBy(l => l.ItemName, StringComparer.Ordinal).ThenBy(l => l.ItemId).ToList();
            if (levels.Count > 0)
            {
                result[cabinet.RoomId!.Value] = levels;
            }
        }

        return result;
    }

    /// <summary>Räume, deren Raumschrank bei mindestens einem Material unter der Mindestmenge liegt.</summary>
    public static int RoomsBelowMinimum(Dictionary<Guid, List<RoomLevel>> equipment) => equipment.Values.Count(levels => levels.Any(l => l.IsBelow));

    /// <summary>
    /// Fügt den Aufgaben ihre Zeilen des Raumchecks an. Neue (offene) Zeilen gibt es nur, solange die Aufgabe nicht erledigt ist und alle ihre Termine abgeschlossen
    /// sind (dann ist der Verbrauch gebucht); beantwortete Zeilen bleiben stehen.
    /// </summary>
    public async Task<List<RoomCareTaskDto>> WithRestockAsync(List<RoomCareTaskDto> tasks, IReadOnlyList<Appointment> appointments,
        Dictionary<Guid, List<RoomLevel>> equipment, CancellationToken ct)
    {
        if (tasks.Count == 0 || equipment.Count == 0)
        {
            return [.. tasks.Select(t => t with { Restock = [] })];
        }

        var appointmentIds = tasks.SelectMany(t => t.AppointmentIds).ToList();
        var answers = await db.RoomCareRestockAnswers.AsNoTracking().Where(a => appointmentIds.Contains(a.AppointmentId)).ToListAsync(ct);
        var status = appointments.ToDictionary(a => a.Id, a => a.Status);
        return [.. tasks.Select(t => t with { Restock = Lines(t, equipment.GetValueOrDefault(t.RoomId) ?? [], answers.Where(a => t.AppointmentIds.Contains(a.AppointmentId)).ToList(), status) })];
    }

    /// <summary>
    /// Beantwortet eine Zeile der Aufgabe. „Aufgefüllt“ bucht die Menge (ohne Angabe die vorgeschlagene) vom Hauptlager in den Raumschrank; „reicht noch“ bucht nichts.
    /// Eine aufgefüllte Zeile lässt sich nicht noch einmal beantworten (die Buchung bleibt), „reicht noch“ darf später auf „aufgefüllt“ wechseln.
    /// </summary>
    public async Task<Errors?> AnswerAsync(RoomCareTaskDto task, Guid locationId, Guid itemId, RestockAnswer input, string? userId, CancellationToken ct)
    {
        var errors = new Errors();
        errors.Check(input.State is RestockState.Refilled or RestockState.Enough, nameof(input.State), "Antwort „aufgefüllt“ oder „reicht noch“ wählen.");
        var line = task.Restock?.FirstOrDefault(l => l.ItemId == itemId);
        errors.Check(line is not null, "itemId", "Dieses Material gehört nicht zur Prüfliste dieser Raumpflege.");
        errors.Check(line is null || line.State != RestockState.Refilled, "itemId", "Diese Zeile ist schon als aufgefüllt gebucht.");
        var quantity = input.Quantity ?? line?.Suggested ?? 0;
        if (input.State == RestockState.Refilled)
        {
            errors.Check(input.Quantity is null or > 0, nameof(input.Quantity), "Menge größer als 0.");
            errors.Check(quantity > 0, nameof(input.Quantity), "Es fehlt nichts bis zur Mindestmenge: Menge angeben.");
        }

        if (errors.Any)
        {
            return errors;
        }

        if (input.State == RestockState.Refilled)
        {
            var cabinetId = (await EquipmentAsync([task.RoomId], ct))[task.RoomId].First(l => l.ItemId == itemId).PlaceId;
            var main = await stock.MainPlaceIdAsync(locationId, ct);
            var (_, refillErrors) = await stock.AddAsync(locationId,
                new StockMovementInput(itemId, StockMovementKind.Refill, quantity, Reason: RefillReason, StoragePlaceId: main, TargetStoragePlaceId: cabinetId), userId, ct);
            if (refillErrors is not null)
            {
                return refillErrors;
            }
        }

        var answer = await db.RoomCareRestockAnswers.FirstOrDefaultAsync(a => task.AppointmentIds.Contains(a.AppointmentId) && a.ItemId == itemId, ct);
        if (answer is null)
        {
            db.RoomCareRestockAnswers.Add(new RoomCareRestockAnswer { AppointmentId = task.AppointmentId, ItemId = itemId, State = input.State, At = time.GetUtcNow(), UserId = userId });
        }
        else
        {
            answer.State = input.State;
            answer.At = time.GetUtcNow();
            answer.UserId = userId;
        }

        await db.SaveChangesAsync(ct);
        return null;
    }

    private static List<RestockLine> Lines(RoomCareTaskDto task, List<RoomLevel> levels, List<RoomCareRestockAnswer> answers,
        Dictionary<Guid, AppointmentStatus> status)
    {
        var fresh = task.State != RoomCareState.Done && task.AppointmentIds.All(id => status.GetValueOrDefault(id) == AppointmentStatus.Completed);
        var lines = new List<RestockLine>();
        foreach (var level in levels)
        {
            if (answers.FirstOrDefault(a => a.ItemId == level.ItemId) is { } answer)
            {
                lines.Add(level.ToLine(answer.State));
            }
            else if (fresh && level.IsBelow)
            {
                lines.Add(level.ToLine(RestockState.Open));
            }
        }

        return lines;
    }
}
