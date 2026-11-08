using AispaStudio.Api.Data;
using AispaStudio.Api.Operations;
using AispaStudio.Api.Pos;
using AispaStudio.Api.Scheduling;
using AispaStudio.Api.Tenancy;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;
using Wolverine;

namespace AispaStudio.Api.Stock;

/// <summary>Material oder Verkaufsprodukt; gilt für alle Standorte, der Bestand je Standort ergibt sich aus den Bewegungen.</summary>
public sealed class StockItem
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public required string Name { get; set; }
    public required string Unit { get; set; }
    public decimal UnitCost { get; set; }
    public decimal MinimumQuantity { get; set; }
    public Guid? ArticleId { get; set; }
    public bool IsActive { get; set; } = true;
    public Guid? SupplierId { get; set; }
    public StockSupplier? Supplier { get; set; }
    public string? SupplierArticleNumber { get; set; }

    /// <summary>Packung (Flasche) und ihre Menge in der Einheit (500); Bewegungen bleiben in der Einheit.</summary>
    public string? PackUnit { get; set; }
    public decimal? PackSize { get; set; }

    /// <summary>Gebinde (Karton) und Zahl der Packungen darin (4).</summary>
    public string? CaseUnit { get; set; }
    public int? PacksPerCase { get; set; }

    /// <summary>Richtwert je Behandlung in der Einheit; abgebucht wird nach dem Rezept.</summary>
    public decimal? TreatmentDose { get; set; }

    /// <summary>So viele Tage vor dem Ablaufdatum gilt eine Charge dieses Materials als bald ablaufend.</summary>
    public int ExpiryWarningDays { get; set; } = StockPackaging.DefaultExpiryWarningDays;

    /// <summary>Code auf der Verpackung (EAN oder Lieferantencode); je Spa eindeutig, ein Handscanner tippt ihn.</summary>
    public string? Barcode { get; set; }
}

/// <summary>
/// Lagerbewegung, nur angefügt und nie geändert: Korrekturen sind eigene Bewegungen mit Grund und Person (nachvollziehbar wie GoBD).
/// <c>UnitCost</c> ist der Einstandspreis zum Zeitpunkt der Bewegung.
/// </summary>
public sealed class StockMovement
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid ItemId { get; init; }
    public Guid LocationId { get; init; }

    /// <summary>Lagerort der Bewegung (ADR 0068); der Bestand des Standorts ist die Summe seiner Lagerorte.</summary>
    public Guid StoragePlaceId { get; init; }

    /// <summary>Nachfüllen und Umbuchen innerhalb des Standorts: Ziel der abgehenden Bewegung.</summary>
    public Guid? TargetStoragePlaceId { get; init; }

    public StockMovementKind Kind { get; init; }
    public decimal Quantity { get; init; }
    public decimal UnitCost { get; init; }
    public DateTimeOffset At { get; init; }
    public string? UserId { get; init; }
    public string? Reason { get; init; }
    public Guid? AppointmentId { get; init; }
    public Guid? ReceiptId { get; init; }
    public Guid? PurchaseOrderId { get; init; }

    /// <summary>
    /// Charge und Ablaufdatum laut Verpackung oder Lieferschein beim Wareneingang. Verbrauch und Verkauf tragen die Charge, aus der das
    /// System gebucht hat („zuerst ablaufend, zuerst verbraucht“); so lässt sich bei einem Rückruf sagen, wer die Charge bekommen hat.
    /// </summary>
    public string? Batch { get; init; }

    public DateOnly? ExpiresOn { get; init; }
}

/// <summary>
/// Lagerort eines Standorts (ADR 0068). Jeder Standort hat genau einen Lagerort der Art <see cref="StoragePlaceKind.Main"/>
/// („Hauptlager“, entsteht mit dem Standort), dazu höchstens <see cref="StockService.MaxPlacesPerLocation"/> − 1 weitere.
/// </summary>
public sealed class StoragePlace
{
    public const string MainName = "Hauptlager";

    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid LocationId { get; init; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public StoragePlaceKind Kind { get; set; }

    /// <summary>Nur bei <see cref="StoragePlaceKind.Room"/>: der Raum desselben Standorts, dessen Verbrauch von hier gebucht wird.</summary>
    public Guid? RoomId { get; set; }

    public bool IsActive { get; set; } = true;
    public int Position { get; set; }
}

/// <summary>Mindestmenge eines Materials an einem Lagerort (zusätzlich zum Mindestbestand je Standort).</summary>
public sealed class StoragePlaceMinimum
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid StoragePlaceId { get; init; }
    public Guid ItemId { get; init; }
    public decimal MinimumQuantity { get; set; }

    /// <summary>Höchstmenge an diesem Ort; die Summe der Lagerorte ist das Bestellziel des Standorts.</summary>
    public decimal? MaximumQuantity { get; set; }
}

/// <summary>Queue: Verbrauch eines Termins laut Rezeptur abgleichen (abgeschlossen = abgebucht, sonst zurückgebucht).</summary>
public sealed record ReconcileAppointmentStock(Guid AppointmentId);

/// <summary>Queue: verkaufte bzw. stornierte Artikel eines Belegs im Lager buchen.</summary>
public sealed record BookReceiptStock(Guid ReceiptId);

/// <summary>Jede Terminänderung gleicht den Materialverbrauch ab; entkoppelt von der Buchung.</summary>
public static class StockAppointmentChangedHandler
{
    public static ReconcileAppointmentStock Handle(AppointmentChangedMessage message) => new(message.Appointment.Id);
}

public static class ReconcileAppointmentStockHandler
{
    public static async Task Handle(ReconcileAppointmentStock message, IServiceScopeFactory scopes, IMessageBus bus, CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AispaDbContext>();
        if (await TenantModel.TenantOfAsync(db.Appointments.Where(a => a.Id == message.AppointmentId), ct) is not { } tenantId)
        {
            return;
        }

        var tenants = scope.ServiceProvider.GetRequiredService<TenantContext>();
        tenants.Use(tenantId);
        await scope.ServiceProvider.GetRequiredService<StockService>().ReconcileAppointmentAsync(message.AppointmentId, ct);

        // Der Bedarf hängt an den gebuchten Terminen, der Bestand am abgeschlossenen: die Kalender des Standorts laden das Lager neu.
        if (await db.Appointments.AsNoTracking().Where(a => a.Id == message.AppointmentId).Select(a => (Guid?)a.LocationId).FirstOrDefaultAsync(ct) is { } locationId)
        {
            await bus.PublishOperationsChangedAsync(tenants, locationId, OperationsChangeKind.Stock);
        }
    }
}

public static class BookReceiptStockHandler
{
    public static async Task Handle(BookReceiptStock message, IServiceScopeFactory scopes, IMessageBus bus, CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AispaDbContext>();
        if (await TenantModel.TenantOfAsync(db.Receipts.Where(r => r.Id == message.ReceiptId), ct) is not { } tenantId)
        {
            return;
        }

        var tenants = scope.ServiceProvider.GetRequiredService<TenantContext>();
        tenants.Use(tenantId);
        await scope.ServiceProvider.GetRequiredService<StockService>().BookReceiptAsync(message.ReceiptId, ct);

        var locationId = await db.Receipts.AsNoTracking().Where(r => r.Id == message.ReceiptId)
            .Join(db.CashRegisters, r => r.RegisterId, c => c.Id, (_, c) => (Guid?)c.LocationId).FirstOrDefaultAsync(ct);
        if (locationId is { } location)
        {
            await bus.PublishOperationsChangedAsync(tenants, location, OperationsChangeKind.Stock);
        }
    }
}

internal sealed class StockService(AispaDbContext db, TimeProvider time)
{
    public const int PlanningDays = 14;

    /// <summary>Höchstens so viele Lagerorte je Standort, das Hauptlager eingeschlossen.</summary>
    public const int MaxPlacesPerLocation = 10;

    private static readonly AppointmentStatus[] Upcoming = [AppointmentStatus.Booked, AppointmentStatus.CheckedIn];

    /// <summary>
    /// Bringt den Verbrauch eines Termins auf den Sollstand: abgeschlossen = Rezeptur abgebucht, sonst nichts. Gebucht wird nur die
    /// Differenz, so ist der Abgleich beliebig oft wiederholbar. Der Termin wird dafür gesperrt, damit zwei Aufträge nicht doppelt buchen.
    /// </summary>
    public async Task ReconcileAppointmentAsync(Guid appointmentId, CancellationToken ct)
    {
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await db.Database.ExecuteSqlAsync($"""SELECT 1 FROM aispa."Appointments" WHERE "Id" = {appointmentId} FOR UPDATE""", ct);
            var appointment = await db.Appointments.AsNoTracking().FirstOrDefaultAsync(a => a.Id == appointmentId, ct);
            if (appointment is null)
            {
                return;
            }

            var target = appointment.Status == AppointmentStatus.Completed
                ? await RecipeAsync(appointment.TreatmentId, ct)
                : [];
            var consumed = await db.StockMovements.AsNoTracking()
                .Where(m => m.AppointmentId == appointmentId && m.Kind == StockMovementKind.Consumption).ToListAsync(ct);
            var booked = consumed.GroupBy(m => m.ItemId).ToDictionary(g => g.Key, g => -g.Sum(m => m.Quantity));
            var costs = await CostsAsync([.. target.Keys, .. booked.Keys], ct);
            var places = await db.StoragePlaces.AsNoTracking().Where(p => p.LocationId == appointment.LocationId).ToListAsync(ct);
            var now = time.GetUtcNow();
            foreach (var itemId in target.Keys.Union(booked.Keys))
            {
                var difference = target.GetValueOrDefault(itemId) - booked.GetValueOrDefault(itemId);
                if (difference == 0)
                {
                    continue;
                }

                // Verbrauch zählt zum Ende der Behandlung, auch wenn sie später abgeschlossen wird; die Rücknahme zählt jetzt.
                var at = difference > 0 && appointment.End < now ? appointment.End : now;
                var parts = new List<(Guid PlaceId, Part Part)>();
                var reason = "Behandlung nicht mehr abgeschlossen";
                if (difference > 0)
                {
                    // Vom Raumschrank, wenn dort genug liegt, sonst vom Hauptlager; reicht beides nicht, geht es ins Minus am Hauptlager.
                    var onHand = await db.StockMovements.AsNoTracking().Where(m => m.LocationId == appointment.LocationId && m.ItemId == itemId)
                        .GroupBy(m => m.StoragePlaceId).Select(g => new { PlaceId = g.Key, Quantity = g.Sum(m => m.Quantity) })
                        .ToDictionaryAsync(g => g.PlaceId, g => g.Quantity, ct);
                    var (placeId, shortfall) = ConsumptionPlace(places, appointment.RoomId, difference, id => onHand.GetValueOrDefault(id));
                    reason = shortfall ? ShortfallReason : ConsumptionReason;

                    // Verbrauch kommt aus den Chargen, die zuerst ablaufen.
                    parts.AddRange(Allocate(difference, await SharesAsync(appointment.LocationId, itemId, placeId, ct)).Select(p => (placeId, p)));
                }
                else
                {
                    // Eine Rücknahme geht an die Lagerorte und in die Chargen zurück, aus denen gebucht war.
                    parts.AddRange(ReturnParts(-difference, consumed.Where(m => m.ItemId == itemId)).Select(p => (p.PlaceId, p.Part with { Quantity = -p.Part.Quantity })));
                }

                foreach (var (placeId, part) in parts)
                {
                    db.StockMovements.Add(new StockMovement
                    {
                        ItemId = itemId,
                        LocationId = appointment.LocationId,
                        StoragePlaceId = placeId,
                        Kind = StockMovementKind.Consumption,
                        Quantity = -part.Quantity,
                        UnitCost = costs.GetValueOrDefault(itemId),
                        At = at,
                        AppointmentId = appointmentId,
                        Reason = reason,
                        Batch = part.Batch,
                        ExpiresOn = part.ExpiresOn,
                    });
                }
            }

            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        });
    }

    /// <summary>Verkaufte Artikel mit Lagerartikel buchen ab, ein Storno bucht zurück. Je Beleg genau einmal.</summary>
    public async Task BookReceiptAsync(Guid receiptId, CancellationToken ct)
    {
        if (await db.StockMovements.AnyAsync(m => m.ReceiptId == receiptId, ct))
        {
            return;
        }

        var receipt = await db.Receipts.AsNoTracking().Include(r => r.Lines).FirstOrDefaultAsync(r => r.Id == receiptId, ct);
        if (receipt is not { Kind: ReceiptKind.Sale or ReceiptKind.Void })
        {
            return;
        }

        var locationId = await db.CashRegisters.Where(r => r.Id == receipt.RegisterId).Select(r => r.LocationId).FirstAsync(ct);
        var salesPlaceId = SalesPlace(await db.StoragePlaces.AsNoTracking().Where(p => p.LocationId == locationId).ToListAsync(ct)).Id;
        var billLineIds = receipt.Lines.Where(l => l.BillLineId != null).Select(l => l.BillLineId!.Value).ToList();
        var articles = await db.BillLines.AsNoTracking().Where(l => billLineIds.Contains(l.Id) && l.ArticleId != null)
            .ToDictionaryAsync(l => l.Id, l => l.ArticleId!.Value, ct);
        var articleIds = articles.Values.Distinct().ToList();
        var items = await db.StockItems.AsNoTracking().Where(i => i.ArticleId != null && articleIds.Contains(i.ArticleId.Value))
            .ToDictionaryAsync(i => i.ArticleId!.Value, ct);
        // Ein Storno gibt in die Chargen zurück, aus denen der Verkauf gebucht war.
        var voided = receipt.Kind == ReceiptKind.Void && receipt.VoidsReceiptId is { } voids
            ? await db.StockMovements.AsNoTracking().Where(m => m.ReceiptId == voids && m.Kind == StockMovementKind.Sale).ToListAsync(ct)
            : [];
        var movements = new List<StockMovement>();
        foreach (var sold in voided)
        {
            movements.Add(new StockMovement
            {
                ItemId = sold.ItemId,
                LocationId = locationId,
                StoragePlaceId = sold.StoragePlaceId,
                Kind = StockMovementKind.Sale,
                Quantity = -sold.Quantity,
                UnitCost = sold.UnitCost,
                At = receipt.IssuedAt,
                ReceiptId = receiptId,
                Reason = "Storno",
                Batch = sold.Batch,
                ExpiresOn = sold.ExpiresOn,
            });
        }

        if (voided.Count == 0)
        {
            var sign = receipt.Kind == ReceiptKind.Void ? 1 : -1;
            foreach (var group in receipt.Lines
                .Where(l => l.BillLineId is { } id && articles.TryGetValue(id, out var article) && items.ContainsKey(article))
                .GroupBy(l => items[articles[l.BillLineId!.Value]]))
            {
                var quantity = group.Sum(l => l.Quantity);
                if (quantity == 0)
                {
                    continue;
                }

                // Verkauft wird aus den Chargen, die zuerst ablaufen; ein Storno ohne auffindbaren Verkauf geht ohne Charge zurück.
                var parts = sign < 0 ? Allocate(quantity, await SharesAsync(locationId, group.Key.Id, salesPlaceId, ct)) : [new Part(null, null, quantity)];
                movements.AddRange(parts.Select(part => new StockMovement
                {
                    ItemId = group.Key.Id,
                    LocationId = locationId,
                    StoragePlaceId = salesPlaceId,
                    Kind = StockMovementKind.Sale,
                    Quantity = sign * part.Quantity,
                    UnitCost = group.Key.UnitCost,
                    At = receipt.IssuedAt,
                    ReceiptId = receiptId,
                    Reason = receipt.Kind == ReceiptKind.Void ? "Storno" : null,
                    Batch = part.Batch,
                    ExpiresOn = part.ExpiresOn,
                }));
            }
        }

        if (movements.Count == 0)
        {
            return;
        }

        db.StockMovements.AddRange(movements);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Derselbe Beleg lief parallel schon durch (eindeutiger Index je Beleg, Artikel und Charge).
        }
    }

    /// <summary>Wareneingang, Inventur, Korrektur, Umbuchung oder Nachfüllen; gibt die gebuchten Bewegungen zurück oder einen Fehler je Feld.</summary>
    public async Task<(List<StockMovement>? Movements, Errors? Errors)> AddAsync(Guid locationId, StockMovementInput input, string? userId,
        CancellationToken ct)
    {
        var errors = new Errors();
        var item = await db.StockItems.FirstOrDefaultAsync(i => i.Id == input.ItemId, ct);
        errors.Check(item is not null, nameof(input.ItemId), "Material nicht gefunden.");
        errors.Check(input.Kind is StockMovementKind.GoodsReceipt or StockMovementKind.Count or StockMovementKind.Correction or StockMovementKind.Transfer
                or StockMovementKind.Refill,
            nameof(input.Kind), "Verbrauch und Verkauf bucht das System selbst.");
        var places = await db.StoragePlaces.AsNoTracking().Where(p => p.LocationId == locationId).ToListAsync(ct);
        var place = CheckPlace(errors, places, input.StoragePlaceId, nameof(input.StoragePlaceId), required: input.Kind != StockMovementKind.GoodsReceipt);
        var targetPlace = input.Kind is StockMovementKind.Refill or StockMovementKind.Transfer && input.TargetStoragePlaceId is not null
            ? CheckPlace(errors, places, input.TargetStoragePlaceId, nameof(input.TargetStoragePlaceId), required: true)
            : null;
        errors.Check(input.Kind is StockMovementKind.Refill or StockMovementKind.Transfer || input.TargetStoragePlaceId is null,
            nameof(input.TargetStoragePlaceId), "Ziel nur beim Nachfüllen und Umbuchen.");
        errors.Check(Math.Abs(input.Quantity) < 1_000_000 && decimal.Round(input.Quantity, 3) == input.Quantity, nameof(input.Quantity), "Menge mit höchstens drei Nachkommastellen.");
        errors.Check(input.Reason is null || input.Reason.Length <= 300, nameof(input.Reason), "Grund mit höchstens 300 Zeichen.");
        errors.Check(input.Kind == StockMovementKind.GoodsReceipt || (input.Batch is null && input.ExpiresOn is null), nameof(input.Batch),
            "Charge und Ablaufdatum nur beim Wareneingang.");
        CheckBatch(errors, input.Batch, input.ExpiresOn, nameof(input.Batch));
        errors.Check(input.Kind == StockMovementKind.GoodsReceipt || input.PackageLevel == StockPackageLevel.Unit, nameof(input.PackageLevel),
            "Packung und Gebinde nur beim Wareneingang.");
        var packageQuantity = item is null ? 1 : StockPackaging.BaseQuantity(item, input.PackageLevel);
        errors.Check(packageQuantity is not null, nameof(input.PackageLevel), "Das Material hat diese Packung nicht. Trage sie beim Material ein.");
        errors.Check(packageQuantity is null || Math.Abs(input.Quantity * packageQuantity.Value) < 1_000_000, nameof(input.Quantity), "Die Menge ergibt in der Einheit des Materials 1 Million oder mehr.");
        errors.Check(packageQuantity is null || decimal.Round(input.Quantity * packageQuantity.Value, 3) == input.Quantity * packageQuantity.Value, nameof(input.Quantity),
            "Die Menge ergibt in der Einheit des Materials mehr als drei Nachkommastellen.");
        switch (input.Kind)
        {
            case StockMovementKind.GoodsReceipt:
                errors.Check(input.Quantity > 0, nameof(input.Quantity), "Eingangsmenge größer als 0.");
                errors.Check(input.UnitCost is null or >= 0 and < 1_000_000, nameof(input.UnitCost), "Einstandspreis nicht negativ.");
                break;
            case StockMovementKind.Count:
                errors.Check(input.Quantity >= 0, nameof(input.Quantity), "Gezählter Bestand nicht negativ.");
                break;
            case StockMovementKind.Correction:
                errors.Check(input.Quantity != 0, nameof(input.Quantity), "Menge mit Vorzeichen, z. B. −2 für Bruch.");
                errors.Check(!string.IsNullOrWhiteSpace(input.Reason), nameof(input.Reason), "Grund angeben, z. B. Bruch oder abgelaufen.");
                break;
            case StockMovementKind.Transfer:
                errors.Check(input.Quantity > 0, nameof(input.Quantity), "Menge größer als 0.");
                if (input.TargetStoragePlaceId is null)
                {
                    errors.Check(input.TargetLocationId is { } target && target != locationId && await db.Locations.AnyAsync(l => l.Id == target, ct),
                        nameof(input.TargetLocationId), "Anderen Standort oder einen Lagerort dieses Standorts als Ziel wählen.");
                }
                else
                {
                    errors.Check(input.TargetLocationId is null, nameof(input.TargetLocationId), "Entweder anderen Standort oder Lagerort als Ziel wählen.");
                    CheckTarget(errors, place, targetPlace);
                }

                break;
            case StockMovementKind.Refill:
                errors.Check(input.Quantity > 0, nameof(input.Quantity), "Menge größer als 0.");
                errors.Check(input.TargetLocationId is null, nameof(input.TargetLocationId), "Nachfüllen geht nur innerhalb des Standorts.");
                if (input.TargetStoragePlaceId is null)
                {
                    errors.Add(nameof(input.TargetStoragePlaceId), "Lagerort zum Nachfüllen wählen.");
                }
                else
                {
                    CheckTarget(errors, place, targetPlace);
                }

                break;
        }

        if (errors.Any || item is null)
        {
            return (null, errors);
        }

        var now = time.GetUtcNow();
        var reason = string.IsNullOrWhiteSpace(input.Reason) ? null : input.Reason.Trim();
        var movements = new List<StockMovement>();
        switch (input.Kind)
        {
            case StockMovementKind.GoodsReceipt:
                movements.Add(await GoodsReceiptAsync(item, locationId, place?.Id ?? MainOf(places).Id, input.Quantity * packageQuantity!.Value, input.UnitCost / packageQuantity, now,
                    userId, reason, null, ct, input.Batch, input.ExpiresOn));
                break;
            case StockMovementKind.Count:
                var difference = input.Quantity - await QuantityAsync(item.Id, place!.Id, ct);
                if (difference != 0)
                {
                    movements.Add(Movement(item, locationId, place.Id, input.Kind, difference, now, userId, reason ?? "Inventur"));
                }

                break;
            case StockMovementKind.Correction:
                movements.Add(Movement(item, locationId, place!.Id, input.Kind, input.Quantity, now, userId, reason));
                break;
            case StockMovementKind.Transfer when targetPlace is null:
                var targetMain = await MainPlaceIdAsync(input.TargetLocationId!.Value, ct);
                movements.Add(Movement(item, locationId, place!.Id, input.Kind, -input.Quantity, now, userId, reason ?? "Umbuchung"));
                movements.Add(Movement(item, input.TargetLocationId.Value, targetMain, input.Kind, input.Quantity, now, userId, reason ?? "Umbuchung"));
                break;
            case StockMovementKind.Transfer or StockMovementKind.Refill:
                movements.AddRange(MovePlaces(item, locationId, place!.Id, targetPlace!.Id, input.Kind, input.Quantity, now, userId,
                    reason ?? (input.Kind == StockMovementKind.Refill ? "Nachfüllen" : "Umbuchung"),
                    await SharesAsync(locationId, item.Id, place.Id, ct)));
                break;
        }

        db.StockMovements.AddRange(movements);
        await db.SaveChangesAsync(ct);
        return (movements, null);
    }

    /// <summary>
    /// Wareneingang eines Materials (noch nicht gespeichert). Mit Preis wird der Einstandspreis als gleitender Durchschnitt über den Bestand
    /// aller Standorte fortgeschrieben.
    /// </summary>
    public async Task<StockMovement> GoodsReceiptAsync(StockItem item, Guid locationId, Guid storagePlaceId, decimal quantity, decimal? unitCost,
        DateTimeOffset at, string? userId, string? reason, Guid? purchaseOrderId, CancellationToken ct, string? batch = null, DateOnly? expiresOn = null)
    {
        if (unitCost is { } price)
        {
            var onHand = Math.Max(0, await db.StockMovements.Where(m => m.ItemId == item.Id).SumAsync(m => m.Quantity, ct)
                + db.StockMovements.Local.Where(m => m.ItemId == item.Id && db.Entry(m).State == EntityState.Added).Sum(m => m.Quantity));
            item.UnitCost = AverageCost(onHand, item.UnitCost, quantity, price);
        }

        var movement = Movement(item, locationId, storagePlaceId, StockMovementKind.GoodsReceipt, quantity, at, userId, reason, purchaseOrderId, batch,
            expiresOn);
        db.StockMovements.Add(movement);
        return movement;
    }

    /// <summary>Hauptlager des Standorts: Ziel von Wareneingang, Lieferungen und Umbuchungen zwischen Standorten.</summary>
    public async Task<Guid> MainPlaceIdAsync(Guid locationId, CancellationToken ct) =>
        await db.StoragePlaces.AsNoTracking().Where(p => p.LocationId == locationId && p.Kind == StoragePlaceKind.Main).Select(p => p.Id).FirstAsync(ct);

    public static StoragePlace MainOf(IEnumerable<StoragePlace> places) => places.Single(p => p.Kind == StoragePlaceKind.Main);

    /// <summary>Verkäufe und ihre Stornos laufen über den ersten aktiven Lagerort der Art „Verkauf“, ohne einen solchen über das Hauptlager.</summary>
    public static StoragePlace SalesPlace(IReadOnlyList<StoragePlace> places) =>
        places.Where(p => p.Kind == StoragePlaceKind.Sales && p.IsActive).OrderBy(p => p.Position).ThenBy(p => p.Name, StringComparer.Ordinal)
            .FirstOrDefault() ?? MainOf(places);

    /// <summary>
    /// Lagerort für den Verbrauch eines Termins: der Raumschrank des Raums, wenn dort genug liegt, sonst das Hauptlager. Reicht beides nicht, bucht
    /// das Lager ins Minus am Hauptlager (<c>Shortfall</c>, die Bewegung trägt dann <see cref="ShortfallReason"/>).
    /// </summary>
    public static (Guid PlaceId, bool Shortfall) ConsumptionPlace(IReadOnlyList<StoragePlace> places, Guid roomId, decimal needed,
        Func<Guid, decimal> quantityAt)
    {
        var room = places.FirstOrDefault(p => p.Kind == StoragePlaceKind.Room && p.IsActive && p.RoomId == roomId);
        if (room is not null && quantityAt(room.Id) >= needed)
        {
            return (room.Id, false);
        }

        var main = MainOf(places).Id;
        return (main, quantityAt(main) < needed);
    }

    public const string ConsumptionReason = "Behandlung abgeschlossen";

    public const string ShortfallReason = "Behandlung abgeschlossen; Bestand reichte nicht, Minus im Hauptlager";

    /// <summary>
    /// Was von einer Rücknahme an welchen Lagerort und in welche Charge zurückgeht: dorthin, woher der Termin gebucht hat.
    /// </summary>
    internal static List<(Guid PlaceId, Part Part)> ReturnParts(decimal quantity, IEnumerable<StockMovement> consumed)
    {
        var result = new List<(Guid PlaceId, Part Part)>();
        foreach (var place in consumed.GroupBy(m => m.StoragePlaceId).Select(g => (PlaceId: g.Key, Movements: g.ToList()))
            .OrderByDescending(g => -g.Movements.Sum(m => m.Quantity)).ThenBy(g => g.PlaceId))
        {
            var net = -place.Movements.Sum(m => m.Quantity);
            if (quantity <= 0)
            {
                break;
            }

            if (net <= 0)
            {
                continue;
            }

            var take = Math.Min(quantity, net);
            quantity -= take;
            var shares = place.Movements.Where(m => m.Batch is not null)
                .GroupBy(m => (m.Batch, m.ExpiresOn))
                .Select(g => new Share(g.First().ItemId, g.Key.Batch, g.Key.ExpiresOn, default, -g.Sum(m => m.Quantity)))
                .Where(s => s.Quantity > 0);
            result.AddRange(Allocate(take, shares).Select(part => (place.PlaceId, part)));
        }

        return result;
    }

    /// <summary>
    /// Nachfüllen oder Umbuchen innerhalb des Standorts: je Charge zwei Bewegungen (negativ am Ausgang, positiv am Ziel), Charge und Ablaufdatum
    /// wandern mit („zuerst ablaufend, zuerst“). Reine Rechnung, auch für das Einspielen der Beispieldaten.
    /// </summary>
    internal static List<StockMovement> MovePlaces(StockItem item, Guid locationId, Guid fromPlaceId, Guid toPlaceId, StockMovementKind kind,
        decimal quantity, DateTimeOffset at, string? userId, string? reason, IEnumerable<Share> sourceShares)
    {
        var movements = new List<StockMovement>();
        foreach (var part in Allocate(quantity, sourceShares))
        {
            movements.Add(Movement(item, locationId, fromPlaceId, kind, -part.Quantity, at, userId, reason, batch: part.Batch, expiresOn: part.ExpiresOn,
                targetStoragePlaceId: toPlaceId));
            movements.Add(Movement(item, locationId, toPlaceId, kind, part.Quantity, at, userId, reason, batch: part.Batch, expiresOn: part.ExpiresOn));
        }

        return movements;
    }

    /// <summary>Prüft einen Lagerort aus der Anfrage: er muss zum Standort gehören.</summary>
    private static StoragePlace? CheckPlace(Errors errors, IReadOnlyList<StoragePlace> places, Guid? id, string field, bool required)
    {
        if (id is null)
        {
            errors.Check(!required, field, "Lagerort wählen.");
            return null;
        }

        var place = places.FirstOrDefault(p => p.Id == id);
        errors.Check(place is not null, field, "Der Lagerort gehört nicht zu diesem Standort.");
        return place;
    }

    private static void CheckTarget(Errors errors, StoragePlace? from, StoragePlace? to)
    {
        errors.Check(from is null || to is null || from.Id != to.Id, nameof(StockMovementInput.TargetStoragePlaceId), "Anderen Lagerort als Ziel wählen.");
        errors.Check(to is null || to.IsActive, nameof(StockMovementInput.TargetStoragePlaceId), "Der Ziel-Lagerort ist deaktiviert.");
    }

    /// <summary>Neuer Einstandspreis als gleitender Durchschnitt über den Bestand und den Wareneingang.</summary>
    internal static decimal AverageCost(decimal onHand, decimal currentCost, decimal quantity, decimal price) =>
        decimal.Round((onHand * currentCost + quantity * price) / (onHand + quantity), 4);

    /// <summary>Charge höchstens 50 Zeichen, Ablaufdatum in einem plausiblen Bereich.</summary>
    public static void CheckBatch(Errors errors, string? batch, DateOnly? expiresOn, string field)
    {
        errors.Check(batch is null || batch.Trim().Length <= 50, field, "Charge mit höchstens 50 Zeichen.");
        errors.Check(expiresOn is null || expiresOn.Value.Year is >= 2000 and <= 2100, field, "Ablaufdatum prüfen.");
    }

    /// <summary>
    /// Chargen mit Ablaufdatum, die am Standort noch vorhanden sind, zuerst ablaufend zuerst. Inventur, Korrekturen und ältere Bewegungen
    /// tragen keine Charge; der Bestand wird deshalb „zuerst ablaufend, zuerst verbraucht“ den Wareneingängen zugeordnet: er gehört den
    /// zuletzt ablaufenden, Eingänge ohne Ablaufdatum und Umbuchungen hierher zählen als zuletzt ablaufend.
    /// </summary>
    public async Task<StockBatchesDto> BatchesAsync(Guid locationId, CancellationToken ct)
    {
        var timeZone = await db.Locations.Where(l => l.Id == locationId).Select(l => l.TimeZone).FirstAsync(ct);
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(time.GetUtcNow(), LocalTime.Zone(timeZone)).DateTime);
        var items = await db.StockItems.AsNoTracking().Where(i => i.IsActive).ToDictionaryAsync(i => i.Id, ct);
        var places = await db.StoragePlaces.AsNoTracking().Where(p => p.LocationId == locationId).ToDictionaryAsync(p => p.Id, ct);
        var batches = BatchDtos(await SharesAsync(locationId, null, null, ct), items, places, today)
            .OrderBy(b => b.ExpiresOn).ThenBy(b => b.ItemName).ThenBy(b => places[b.StoragePlaceId].Position)
            .ToList();
        return new(locationId, today, StockPackaging.DefaultExpiryWarningDays, batches);
    }

    /// <summary>
    /// Aufgaben zu Ablaufdaten: Chargen innerhalb der Frist ihres Materials, davon der verkäufliche Teil als Abverkaufsliste, und Abgelaufenes zum Ausbuchen. Abgelaufene Ware steht nie auf
    /// der Abverkaufsliste.
    /// </summary>
    public async Task<StockExpiryTasksDto> ExpiryTasksAsync(Guid locationId, CancellationToken ct)
    {
        var batches = await BatchesAsync(locationId, ct);
        var articleIds = await db.StockItems.AsNoTracking().Where(i => i.IsActive && i.ArticleId != null).ToDictionaryAsync(i => i.Id, i => i.ArticleId!.Value, ct);
        var articles = await db.Articles.AsNoTracking().Where(a => a.IsActive && articleIds.Values.Contains(a.Id)).ToDictionaryAsync(a => a.Id, ct);
        StockExpiryTaskDto Task(StockBatchDto b)
        {
            var article = articleIds.TryGetValue(b.ItemId, out var id) ? articles.GetValueOrDefault(id) : null;
            return new(b.ItemId, b.ItemName, b.Unit, b.Batch, b.ExpiresOn, b.ExpiresOn.DayNumber - batches.Today.DayNumber, b.Quantity, b.StoragePlaceId, b.StoragePlaceName,
                article?.Name, article?.Price);
        }

        var expiring = batches.Batches.Where(b => b.ExpiresSoon && !b.Expired).Select(Task).ToList();
        return new(locationId, batches.Today, expiring, [.. expiring.Where(t => t.ArticleName is not null)], [.. batches.Batches.Where(b => b.Expired).Select(Task)]);
    }

    private static IEnumerable<StockBatchDto> BatchDtos(IEnumerable<Share> shares, Dictionary<Guid, StockItem> items,
        Dictionary<Guid, StoragePlace> places, DateOnly today) =>
        shares.Where(s => s.ExpiresOn is not null && items.ContainsKey(s.ItemId))
            .Select(s => new StockBatchDto(s.ItemId, items[s.ItemId].Name, items[s.ItemId].Unit, s.Batch, s.ExpiresOn!.Value, s.Quantity, s.ReceivedAt,
                s.ExpiresOn.Value < today, s.ExpiresOn.Value.DayNumber - today.DayNumber <= items[s.ItemId].ExpiryWarningDays, s.PlaceId, places[s.PlaceId].Name));

    /// <summary>
    /// Alles, was zu einer Charge eines Materials gebucht wurde, über alle Standorte: Eingänge, Verbrauch je Termin mit Kunde, Verkäufe
    /// je Beleg mit Kunde, Stornos und Korrekturen. Bei einem Rückruf steht damit fest, wer die Charge bekommen hat. Null ohne Material.
    /// </summary>
    public async Task<StockBatchUsageDto?> BatchUsageAsync(Guid itemId, string batch, CancellationToken ct)
    {
        var item = await db.StockItems.AsNoTracking().FirstOrDefaultAsync(i => i.Id == itemId, ct);
        var key = batch.Trim();
        if (item is null || key.Length == 0)
        {
            return null;
        }

        var movements = await db.StockMovements.AsNoTracking().Where(m => m.ItemId == itemId && m.Batch == key).OrderBy(m => m.At).ToListAsync(ct);
        var appointmentIds = movements.Where(m => m.AppointmentId != null).Select(m => m.AppointmentId!.Value).Distinct().ToList();
        var receiptIds = movements.Where(m => m.ReceiptId != null).Select(m => m.ReceiptId!.Value).Distinct().ToList();
        var appointments = await db.Appointments.AsNoTracking().Where(a => appointmentIds.Contains(a.Id))
            .Select(a => new { a.Id, a.CustomerId, Name = a.Customer.FirstName + " " + a.Customer.LastName, Treatment = a.Treatment.Name })
            .ToDictionaryAsync(a => a.Id, ct);
        var receipts = await db.Receipts.AsNoTracking().Where(r => receiptIds.Contains(r.Id))
            .Select(r => new { r.Id, r.Number, r.CustomerId })
            .ToDictionaryAsync(r => r.Id, ct);
        var customerIds = receipts.Values.Where(r => r.CustomerId != null).Select(r => r.CustomerId!.Value).Distinct().ToList();
        var customers = await db.Customers.AsNoTracking().Where(c => customerIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => (c.FirstName + " " + c.LastName).Trim(), ct);
        var locations = await db.Locations.AsNoTracking().ToDictionaryAsync(l => l.Id, l => l.Name, ct);

        var entries = movements
            .GroupBy(m => (m.Kind, m.LocationId, m.AppointmentId, m.ReceiptId, m.PurchaseOrderId))
            .Select(g =>
            {
                var appointment = g.Key.AppointmentId is { } a ? appointments.GetValueOrDefault(a) : null;
                var receipt = g.Key.ReceiptId is { } r ? receipts.GetValueOrDefault(r) : null;
                var customerId = appointment?.CustomerId ?? receipt?.CustomerId;
                var customerName = appointment?.Name.Trim() ?? (receipt?.CustomerId is { } c ? customers.GetValueOrDefault(c) : null);
                var detail = appointment?.Treatment ?? (receipt is null ? g.Select(m => m.Reason).FirstOrDefault(x => x != null) : $"Beleg {receipt.Number}");
                return new StockBatchUsageEntry(g.Min(m => m.At), g.Key.Kind, g.Key.LocationId, locations.GetValueOrDefault(g.Key.LocationId, ""),
                    g.Sum(m => m.Quantity), customerId, customerName, detail, g.Key.AppointmentId, g.Key.ReceiptId);
            })
            .Where(e => e.Quantity != 0)
            .OrderBy(e => e.At)
            .ToList();
        return new StockBatchUsageDto(item.Id, item.Name, item.Unit, key, movements.Select(m => m.ExpiresOn).FirstOrDefault(e => e != null),
            entries.Where(e => e.Quantity > 0 && e.Kind is StockMovementKind.GoodsReceipt or StockMovementKind.Transfer).Sum(e => e.Quantity),
            -entries.Where(e => e.Kind is StockMovementKind.Consumption or StockMovementKind.Sale).Sum(e => e.Quantity), entries,
            entries.Where(e => e.CustomerId is not null).GroupBy(e => e.CustomerId).Count(g => g.Sum(e => e.Quantity) < 0));
    }

    internal sealed record Share(Guid ItemId, string? Batch, DateOnly? ExpiresOn, DateTimeOffset ReceivedAt, decimal Quantity, Guid PlaceId = default);

    internal sealed record Part(string? Batch, DateOnly? ExpiresOn, decimal Quantity);

    /// <summary>Ein Wareneingang (oder eine Umbuchung hierher) eines Materials mit Charge.</summary>
    internal sealed record Incoming(Guid ItemId, decimal Quantity, DateTimeOffset At, string? Batch, DateOnly? ExpiresOn);

    /// <summary>
    /// Verteilt den Bestand eines Standorts (optional nur eines Materials, optional nur eines Lagerorts) je Lagerort auf die Wareneingänge:
    /// der Bestand gehört den zuletzt ablaufenden Eingängen, Eingänge ohne Ablaufdatum zählen als zuletzt ablaufend; was übrig ist, ist noch da.
    /// </summary>
    private async Task<List<Share>> SharesAsync(Guid locationId, Guid? itemId, Guid? placeId, CancellationToken ct)
    {
        var movements = db.StockMovements.AsNoTracking().Where(m => m.LocationId == locationId);
        if (itemId is { } only)
        {
            movements = movements.Where(m => m.ItemId == only);
        }

        if (placeId is { } onlyPlace)
        {
            movements = movements.Where(m => m.StoragePlaceId == onlyPlace);
        }

        var quantities = await movements.GroupBy(m => new { m.StoragePlaceId, m.ItemId })
            .Select(g => new { g.Key.StoragePlaceId, g.Key.ItemId, Quantity = g.Sum(m => m.Quantity) })
            .ToListAsync(ct);
        var incoming = await movements
            .Where(m => m.Quantity > 0 && (m.Kind == StockMovementKind.GoodsReceipt || m.Kind == StockMovementKind.Transfer || m.Kind == StockMovementKind.Refill))
            .Select(m => new { m.StoragePlaceId, m.ItemId, m.Quantity, m.At, m.Batch, m.ExpiresOn })
            .ToListAsync(ct);

        var shares = new List<Share>();
        foreach (var place in incoming.GroupBy(m => m.StoragePlaceId))
        {
            var placeQuantities = quantities.Where(q => q.StoragePlaceId == place.Key).ToDictionary(q => q.ItemId, q => q.Quantity);
            shares.AddRange(SharesOf([.. place.Select(m => new Incoming(m.ItemId, m.Quantity, m.At, m.Batch, m.ExpiresOn))], placeQuantities)
                .Select(s => s with { PlaceId = place.Key }));
        }

        return shares;
    }

    /// <summary>Verteilt den Bestand je Material auf die Eingänge (reine Rechnung, auch für das Einspielen der Beispieldaten).</summary>
    internal static List<Share> SharesOf(IEnumerable<Incoming> incoming, IReadOnlyDictionary<Guid, decimal> quantities)
    {
        var shares = new List<Share>();
        foreach (var group in incoming.GroupBy(m => m.ItemId))
        {
            var left = Math.Max(0, quantities.GetValueOrDefault(group.Key));
            foreach (var receipt in group.OrderByDescending(m => m.ExpiresOn ?? DateOnly.MaxValue).ThenByDescending(m => m.At))
            {
                if (left <= 0)
                {
                    break;
                }

                var share = Math.Min(left, receipt.Quantity);
                left -= share;
                shares.Add(new(group.Key, receipt.Batch, receipt.ExpiresOn, receipt.At, share));
            }
        }

        return shares;
    }

    /// <summary>
    /// Teilt eine Abgangsmenge auf Chargen auf, zuerst ablaufend zuerst; was kein Eingang mit Charge oder Ablaufdatum deckt, bleibt ohne Charge.
    /// </summary>
    internal static List<Part> Allocate(decimal quantity, IEnumerable<Share> shares)
    {
        var parts = new List<Part>();
        foreach (var share in shares
            .Where(s => s.Batch is not null || s.ExpiresOn is not null)
            .GroupBy(s => (s.Batch, s.ExpiresOn))
            .Select(g => new Part(g.Key.Batch, g.Key.ExpiresOn, g.Sum(s => s.Quantity)))
            .OrderBy(p => p.ExpiresOn ?? DateOnly.MaxValue).ThenBy(p => p.Batch, StringComparer.Ordinal))
        {
            if (quantity <= 0)
            {
                break;
            }

            var part = Math.Min(quantity, share.Quantity);
            quantity -= part;
            parts.Add(share with { Quantity = part });
        }

        if (quantity > 0)
        {
            parts.Add(new Part(null, null, quantity));
        }

        return parts;
    }

    /// <summary>Bestand, Wert, geplanter Verbrauch der nächsten Tage und Bestellvorschlag je Material an einem Standort.</summary>
    public async Task<StockOverviewDto> OverviewAsync(Guid locationId, CancellationToken ct)
    {
        var items = await db.StockItems.AsNoTracking().Include(i => i.Supplier).Where(i => i.IsActive).OrderBy(i => i.Name).ToListAsync(ct);
        var quantities = await db.StockMovements.AsNoTracking().Where(m => m.LocationId == locationId)
            .GroupBy(m => m.ItemId).Select(g => new { ItemId = g.Key, Quantity = g.Sum(m => m.Quantity) })
            .ToDictionaryAsync(g => g.ItemId, g => g.Quantity, ct);
        var now = time.GetUtcNow();
        var until = now.AddDays(PlanningDays);
        var reorder = await ReorderAsync(locationId, items, quantities, ct);
        var upcoming = await db.Appointments.AsNoTracking()
            .Where(a => a.LocationId == locationId && a.Start >= now && a.Start < until && Upcoming.Contains(a.Status))
            .GroupBy(a => a.TreatmentId).Select(g => new { TreatmentId = g.Key, Count = g.Count() })
            .ToListAsync(ct);
        var recipes = await StockRecipes.ForAsync(db, upcoming.Select(u => u.TreatmentId).ToList(), ct);
        var planned = upcoming.SelectMany(u => recipes.GetValueOrDefault(u.TreatmentId, []).Select(r => (r.Key, Quantity: r.Value * u.Count)))
            .GroupBy(r => r.Key).ToDictionary(g => g.Key, g => g.Sum(r => r.Quantity));

        var onOrder = await OnOrderAsync(locationId, ct);
        var expiry = (await BatchesAsync(locationId, ct)).Batches.GroupBy(b => b.ItemId).ToDictionary(g => g.Key, g => g.Min(b => b.ExpiresOn));
        var positions = await db.StoragePlaces.AsNoTracking().Where(p => p.LocationId == locationId).ToDictionaryAsync(p => p.Id, p => p.Position, ct);
        var byPlace = (await db.StockMovements.AsNoTracking().Where(m => m.LocationId == locationId)
                .GroupBy(m => new { m.ItemId, m.StoragePlaceId }).Select(g => new { g.Key.ItemId, g.Key.StoragePlaceId, Quantity = g.Sum(m => m.Quantity) })
                .ToListAsync(ct))
            .Where(q => q.Quantity != 0)
            .GroupBy(q => q.ItemId)
            .ToDictionary(g => g.Key, g => g.OrderBy(q => positions.GetValueOrDefault(q.StoragePlaceId)).Select(q => new StockPlaceQuantityDto(q.StoragePlaceId, q.Quantity)).ToList());

        var levels = items.Select(i =>
        {
            var quantity = quantities.GetValueOrDefault(i.Id);
            var use = planned.GetValueOrDefault(i.Id);
            var ordered = onOrder.GetValueOrDefault(i.Id);
            return new StockLevelDto(i.Id, i.Name, i.Unit, quantity, i.MinimumQuantity, decimal.Round(Math.Max(0, quantity) * i.UnitCost, 2), use,
                reorder[i.Id].Suggested, quantity < i.MinimumQuantity, ordered, i.Supplier?.Name, expiry.TryGetValue(i.Id, out var next) ? next : null,
                byPlace.GetValueOrDefault(i.Id) ?? [], StockPackaging.TreatmentsCovered(i, quantity));
        }).ToList();
        return new(locationId, PlanningDays, levels.Sum(l => l.Value), levels);
    }

    /// <summary>Die Zeilen des Bestellvorschlags eines Standorts mit dem Rechenweg; nur Material, von dem etwas zu bestellen ist.</summary>
    public async Task<List<ReorderSuggestionDto>> ReorderSuggestionsAsync(Guid locationId, CancellationToken ct)
    {
        var items = await db.StockItems.AsNoTracking().Include(i => i.Supplier).Where(i => i.IsActive).OrderBy(i => i.Name).ToListAsync(ct);
        var quantities = await db.StockMovements.AsNoTracking().Where(m => m.LocationId == locationId)
            .GroupBy(m => m.ItemId).Select(g => new { ItemId = g.Key, Quantity = g.Sum(m => m.Quantity) })
            .ToDictionaryAsync(g => g.ItemId, g => g.Quantity, ct);
        var reorder = await ReorderAsync(locationId, items, quantities, ct);
        return [.. items.Select(i => reorder[i.Id]).Where(l => l.Suggested > 0)];
    }

    /// <summary>
    /// Rechnet je Material den Vorschlag (<see cref="PurchaseOrders.ReorderQuantity"/>): Tagesverbrauch aus Verbrauch und Verkauf der letzten 90 Tage, gebuchte Termine bis zur Lieferung,
    /// Lieferzeit des Lieferanten, offene Bestellungen und die Summe der Höchstmengen der Lagerorte. Ohne Lieferzeit zählen die Termine des Planungszeitraums.
    /// </summary>
    private async Task<Dictionary<Guid, ReorderSuggestionDto>> ReorderAsync(Guid locationId, List<StockItem> items, Dictionary<Guid, decimal> quantities, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        var since = now.AddDays(-PurchaseOrders.UsageDays);
        var used = await db.StockMovements.AsNoTracking()
            .Where(m => m.LocationId == locationId && m.At >= since && m.At < now && (m.Kind == StockMovementKind.Consumption || m.Kind == StockMovementKind.Sale))
            .GroupBy(m => m.ItemId).Select(g => new { ItemId = g.Key, Quantity = g.Sum(m => m.Quantity) })
            .ToDictionaryAsync(g => g.ItemId, g => -g.Quantity, ct);
        var targets = await db.StoragePlaceMinimums.AsNoTracking()
            .Where(m => m.MaximumQuantity != null && db.StoragePlaces.Any(p => p.Id == m.StoragePlaceId && p.LocationId == locationId && p.IsActive))
            .GroupBy(m => m.ItemId).Select(g => new { ItemId = g.Key, Quantity = g.Sum(m => m.MaximumQuantity!.Value) })
            .ToDictionaryAsync(g => g.ItemId, g => g.Quantity, ct);
        var onOrder = await OnOrderAsync(locationId, ct);
        var horizon = now.AddDays(items.Select(i => i.Supplier?.LeadTimeDays ?? PlanningDays).DefaultIfEmpty(PlanningDays).Max());
        var upcoming = await db.Appointments.AsNoTracking()
            .Where(a => a.LocationId == locationId && a.Start >= now && a.Start < horizon && Upcoming.Contains(a.Status))
            .Select(a => new { a.TreatmentId, a.Start }).ToListAsync(ct);
        var recipes = await StockRecipes.ForAsync(db, upcoming.Select(a => a.TreatmentId).Distinct().ToList(), ct);

        return items.ToDictionary(i => i.Id, i =>
        {
            var lead = i.Supplier?.LeadTimeDays;
            var deliveredBy = now.AddDays(lead ?? PlanningDays);
            var booked = upcoming.Where(a => a.Start < deliveredBy).Sum(a => recipes.GetValueOrDefault(a.TreatmentId)?.GetValueOrDefault(i.Id) ?? 0m);
            var daily = Math.Max(0, used.GetValueOrDefault(i.Id)) / PurchaseOrders.UsageDays;
            var quantity = quantities.GetValueOrDefault(i.Id);
            var ordered = onOrder.GetValueOrDefault(i.Id);
            decimal? maximum = targets.TryGetValue(i.Id, out var sum) ? sum : null;
            var suggested = PurchaseOrders.ReorderQuantity(quantity, daily, lead, booked, ordered, i.MinimumQuantity, maximum, i.PackSize);
            return new ReorderSuggestionDto(i.Id, i.Name, i.Unit, i.Supplier?.Name, quantity, decimal.Round(daily, 3), lead, booked, ordered, PurchaseOrders.ReorderTarget(i.MinimumQuantity, maximum), suggested,
                StockPackaging.PackageText(i, suggested), decimal.Round(suggested * i.UnitCost, 2));
        });
    }

    /// <summary>Bestand je Material an einem Lagerort mit Mindestmenge des Orts (falls eingetragen) und den Chargen des Orts.</summary>
    public async Task<StoragePlaceLevelsDto> LevelsAsync(Guid locationId, Guid placeId, CancellationToken ct)
    {
        var timeZone = await db.Locations.Where(l => l.Id == locationId).Select(l => l.TimeZone).FirstAsync(ct);
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(time.GetUtcNow(), LocalTime.Zone(timeZone)).DateTime);
        var items = await db.StockItems.AsNoTracking().Where(i => i.IsActive).ToDictionaryAsync(i => i.Id, ct);
        var quantities = await db.StockMovements.AsNoTracking().Where(m => m.StoragePlaceId == placeId)
            .GroupBy(m => m.ItemId).Select(g => new { ItemId = g.Key, Quantity = g.Sum(m => m.Quantity) })
            .ToDictionaryAsync(g => g.ItemId, g => g.Quantity, ct);
        var minimums = await db.StoragePlaceMinimums.AsNoTracking().Where(m => m.StoragePlaceId == placeId)
            .ToDictionaryAsync(m => m.ItemId, m => m.MinimumQuantity, ct);
        var places = await db.StoragePlaces.AsNoTracking().Where(p => p.LocationId == locationId).ToDictionaryAsync(p => p.Id, ct);
        var batches = BatchDtos(await SharesAsync(locationId, null, placeId, ct), items, places, today)
            .OrderBy(b => b.ExpiresOn).ThenBy(b => b.ItemName).ToList()
            .GroupBy(b => b.ItemId).ToDictionary(g => g.Key, g => g.ToList());

        var levels = items.Values.Where(i => quantities.GetValueOrDefault(i.Id) != 0 || minimums.ContainsKey(i.Id))
            .OrderBy(i => i.Name, StringComparer.CurrentCulture)
            .Select(i =>
            {
                var quantity = quantities.GetValueOrDefault(i.Id);
                decimal? minimum = minimums.TryGetValue(i.Id, out var m) ? m : null;
                var itemBatches = batches.GetValueOrDefault(i.Id) ?? [];
                return new StoragePlaceLevelDto(i.Id, i.Name, i.Unit, quantity, minimum, minimum is { } min && quantity < min,
                    decimal.Round(Math.Max(0, quantity) * i.UnitCost, 2), itemBatches.Count == 0 ? null : itemBatches.Min(b => b.ExpiresOn), itemBatches,
                    StockPackaging.TreatmentsCovered(i, quantity));
            }).ToList();
        return new(placeId, levels.Sum(l => l.Value), levels);
    }

    /// <summary>Offen bestellte Mengen je Material: Entwürfe und bestellte Positionen abzüglich bereits geliefert, nie negativ.</summary>
    private async Task<Dictionary<Guid, decimal>> OnOrderAsync(Guid locationId, CancellationToken ct)
    {
        var lines = await db.PurchaseOrders.AsNoTracking()
            .Where(o => o.LocationId == locationId && PurchaseOrders.Open.Contains(o.Status))
            .SelectMany(o => o.Lines).Select(l => new { l.ItemId, l.Quantity, l.Received }).ToListAsync(ct);
        return lines.GroupBy(l => l.ItemId).ToDictionary(g => g.Key, g => g.Sum(l => Math.Max(0, l.Quantity - l.Received)));
    }

    public async Task<List<StockMovementDto>> MovementsAsync(Guid locationId, DateTimeOffset from, DateTimeOffset until, CancellationToken ct) =>
        await db.StockMovements.AsNoTracking()
            .Where(m => m.LocationId == locationId && m.At >= from && m.At < until)
            .Join(db.StockItems, m => m.ItemId, i => i.Id, (m, i) => new { m, i.Name })
            .Join(db.StoragePlaces, x => x.m.StoragePlaceId, p => p.Id, (x, p) => new { x.m, x.Name, PlaceName = p.Name })
            .OrderByDescending(x => x.m.At).ThenBy(x => x.m.Id)
            .Take(1000)
            .Select(x => new StockMovementDto(x.m.Id, x.m.ItemId, x.Name, x.m.LocationId, x.m.StoragePlaceId, x.PlaceName, x.m.Kind, x.m.Quantity,
                x.m.UnitCost, x.m.At, x.m.Reason, x.m.AppointmentId, x.m.ReceiptId, x.m.PurchaseOrderId, x.m.Batch, x.m.ExpiresOn))
            .ToListAsync(ct);

    public static StockMovementDto ToDto(StockMovement m, string itemName, string placeName) =>
        new(m.Id, m.ItemId, itemName, m.LocationId, m.StoragePlaceId, placeName, m.Kind, m.Quantity, m.UnitCost, m.At, m.Reason, m.AppointmentId,
            m.ReceiptId, m.PurchaseOrderId, m.Batch, m.ExpiresOn);

    private async Task<decimal> QuantityAsync(Guid itemId, Guid storagePlaceId, CancellationToken ct) =>
        await db.StockMovements.Where(m => m.ItemId == itemId && m.StoragePlaceId == storagePlaceId).SumAsync(m => m.Quantity, ct);

    private async Task<Dictionary<Guid, decimal>> RecipeAsync(Guid treatmentId, CancellationToken ct) =>
        await StockRecipes.ForAsync(db, treatmentId, ct);

    private async Task<Dictionary<Guid, decimal>> CostsAsync(List<Guid> itemIds, CancellationToken ct) =>
        await db.StockItems.AsNoTracking().Where(i => itemIds.Contains(i.Id)).ToDictionaryAsync(i => i.Id, i => i.UnitCost, ct);

    internal static StockMovement Movement(StockItem item, Guid locationId, Guid storagePlaceId, StockMovementKind kind, decimal quantity,
        DateTimeOffset at, string? userId, string? reason, Guid? purchaseOrderId = null, string? batch = null, DateOnly? expiresOn = null,
        Guid? targetStoragePlaceId = null) =>
        new()
        {
            ItemId = item.Id,
            LocationId = locationId,
            StoragePlaceId = storagePlaceId,
            TargetStoragePlaceId = targetStoragePlaceId,
            Kind = kind,
            Quantity = quantity,
            UnitCost = item.UnitCost,
            At = at,
            UserId = userId,
            Reason = reason,
            PurchaseOrderId = purchaseOrderId,
            Batch = string.IsNullOrWhiteSpace(batch) ? null : batch.Trim(),
            ExpiresOn = expiresOn,
        };
}

internal static class StockModel
{
    public static void Configure(ModelBuilder builder)
    {
        builder.Entity<StockItem>(e =>
        {
            e.Property(i => i.Name).HasMaxLength(200);
            e.Property(i => i.Unit).HasMaxLength(20);
            e.Property(i => i.UnitCost).HasPrecision(12, 4);
            e.Property(i => i.MinimumQuantity).HasPrecision(12, 3);
            e.HasOne<Article>().WithMany().HasForeignKey(i => i.ArticleId).OnDelete(DeleteBehavior.SetNull);
            e.HasIndex(i => i.ArticleId).IsUnique();
            e.HasOne(i => i.Supplier).WithMany().HasForeignKey(i => i.SupplierId).OnDelete(DeleteBehavior.Restrict);
            e.Property(i => i.SupplierArticleNumber).HasMaxLength(50);
            e.Property(i => i.Barcode).HasMaxLength(StockBarcodes.MaxLength);
            e.Property(i => i.PackUnit).HasMaxLength(20);
            e.Property(i => i.PackSize).HasPrecision(12, 3);
            e.Property(i => i.CaseUnit).HasMaxLength(20);
            e.Property(i => i.TreatmentDose).HasPrecision(12, 3);
            e.Property(i => i.ExpiryWarningDays).HasDefaultValue(StockPackaging.DefaultExpiryWarningDays);
        });

        builder.Entity<TreatmentRecommendedArticle>(e =>
        {
            e.HasKey(r => new { r.TreatmentId, r.ArticleId });
            e.HasOne<Treatment>().WithMany().HasForeignKey(r => r.TreatmentId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<Article>().WithMany().HasForeignKey(r => r.ArticleId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<StockSupplier>(e =>
        {
            e.Property(s => s.Id).ValueGeneratedNever();
            e.Property(s => s.Name).HasMaxLength(200);
            e.Property(s => s.ContactPerson).HasMaxLength(200);
            e.Property(s => s.Email).HasMaxLength(254);
            e.Property(s => s.Phone).HasMaxLength(40);
            e.Property(s => s.MinimumOrderValue).HasPrecision(12, 2);
            e.Property(s => s.OrderChannel).HasConversion<string>().HasMaxLength(20);
        });

        builder.Entity<StoragePlace>(e =>
        {
            e.Property(p => p.Id).ValueGeneratedNever();
            e.Property(p => p.Name).HasMaxLength(60);
            e.Property(p => p.Description).HasMaxLength(160);
            e.Property(p => p.Kind).HasConversion<string>().HasMaxLength(10);
            e.HasOne<Location>().WithMany().HasForeignKey(p => p.LocationId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Room>().WithMany().HasForeignKey(p => p.RoomId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(p => new { p.LocationId, p.Position });
            // Genau ein Hauptlager je Standort.
            e.HasIndex(p => p.LocationId).IsUnique().HasDatabaseName("IX_StoragePlaces_LocationId_Main").HasFilter("\"Kind\" = 'Main'");
        });

        builder.Entity<StoragePlaceMinimum>(e =>
        {
            e.Property(m => m.Id).ValueGeneratedNever();
            e.Property(m => m.MinimumQuantity).HasPrecision(12, 3);
            e.HasOne<StoragePlace>().WithMany().HasForeignKey(m => m.StoragePlaceId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<StockItem>().WithMany().HasForeignKey(m => m.ItemId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(m => new { m.StoragePlaceId, m.ItemId }).IsUnique();
        });

        builder.Entity<StockMovement>(e =>
        {
            e.HasOne<StockItem>().WithMany().HasForeignKey(m => m.ItemId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Location>().WithMany().HasForeignKey(m => m.LocationId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<StoragePlace>().WithMany().HasForeignKey(m => m.StoragePlaceId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<StoragePlace>().WithMany().HasForeignKey(m => m.TargetStoragePlaceId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(m => new { m.StoragePlaceId, m.ItemId });
            e.Property(m => m.Kind).HasConversion<string>().HasMaxLength(20);
            e.Property(m => m.Quantity).HasPrecision(12, 3);
            e.Property(m => m.UnitCost).HasPrecision(12, 4);
            e.Property(m => m.UserId).HasMaxLength(450);
            e.Property(m => m.Reason).HasMaxLength(300);
            e.Property(m => m.Batch).HasMaxLength(50);
            e.HasIndex(m => new { m.LocationId, m.ItemId });
            e.HasIndex(m => new { m.LocationId, m.At });
            e.HasIndex(m => m.AppointmentId);
            e.HasIndex(m => new { m.ReceiptId, m.ItemId, m.Batch, m.ExpiresOn }).IsUnique().AreNullsDistinct(false).HasFilter("\"ReceiptId\" IS NOT NULL");
            e.HasIndex(m => new { m.ItemId, m.Batch }).HasFilter("\"Batch\" IS NOT NULL");
            e.HasOne<PurchaseOrder>().WithMany().HasForeignKey(m => m.PurchaseOrderId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<PurchaseOrder>(e =>
        {
            e.Property(o => o.Id).ValueGeneratedNever();
            e.HasOne<Location>().WithMany().HasForeignKey(o => o.LocationId).OnDelete(DeleteBehavior.Restrict);
            e.Property(o => o.Supplier).HasMaxLength(200);
            e.Property(o => o.Note).HasMaxLength(1000);
            e.Property(o => o.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(o => o.UserId).HasMaxLength(450);
            e.HasIndex(o => new { o.LocationId, o.Status });
            e.HasMany(o => o.Lines).WithOne().HasForeignKey(l => l.OrderId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<PurchaseOrderLine>(e =>
        {
            e.Property(l => l.Id).ValueGeneratedNever();
            e.HasOne<StockItem>().WithMany().HasForeignKey(l => l.ItemId).OnDelete(DeleteBehavior.Restrict);
            e.Property(l => l.Quantity).HasPrecision(12, 3);
            e.Property(l => l.Received).HasPrecision(12, 3);
            e.Property(l => l.UnitCost).HasPrecision(12, 4);
        });
    }
}
