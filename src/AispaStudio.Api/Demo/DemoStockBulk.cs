using AispaStudio.Api.Data;
using AispaStudio.Api.Demo.Memory;
using AispaStudio.Api.Pos;
using AispaStudio.Api.Stock;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AispaStudio.Api.Demo;

/// <summary>
/// Das Lager des Beispiel-Spas im Speicher: dieselben Schritte wie <see cref="DemoStock.AddMovementsAsync"/> (Verbrauch und Verkäufe buchen,
/// Wareneingänge ins Hauptlager zum Beginn des Verlaufs, Nachfüllen von Verkaufsregal und Raumschränken, dann nochmals mit Chargen), aber mit
/// einem Hauptbuch im Speicher statt je Termin und Beleg einer Transaktion. Die Rechenschritte (Chargenverteilung, Lagerort des Verbrauchs und
/// des Verkaufs, Nachfüllen, Einstandspreis) sind die des <c>StockService</c>.
/// </summary>
internal static class DemoStockBulk
{
    public static async Task AddMovementsAsync(AispaDbContext db, Guid tenantId, DateTimeOffset start, DateTimeOffset now, CancellationToken ct)
    {
        var data = await LoadAsync(db, ct);
        var ledger = new Ledger();

        // Erst ohne Bestand buchen, um den Bedarf zu kennen; danach alles löschen und nach Wareneingängen und Nachfüllen mit Chargen neu buchen.
        BookUsage(data, ledger, now);
        var levels = ledger.Quantities();
        ledger = new Ledger();

        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var items = data.Items.Values.OrderBy(i => i.Name, StringComparer.Ordinal).ToList();
        var soon = items.Where(i => i.ArticleId != null).First().Id;
        foreach (var (locationId, name) in data.Locations.OrderBy(l => l.Value, StringComparer.Ordinal))
        {
            var main = StockService.MainOf(data.PlacesOf(locationId)).Id;
            foreach (var (item, index) in items.Select((i, n) => (i, n)))
            {
                var used = -data.PlacesOf(locationId).Sum(p => levels.GetValueOrDefault((p.Id, item.Id)));
                var expires = item.Id == soon && name.StartsWith("Wien", StringComparison.Ordinal) ? today.AddDays(21) : today.AddMonths(9);
                var quantity = Math.Ceiling(Math.Max(used, 0) + DemoStock.StockBuffer(name, item));

                // Wareneingang mit Preis: der Einstandspreis wird als gleitender Durchschnitt über den Bestand aller Standorte fortgeschrieben.
                var onHand = Math.Max(0, ledger.QuantityOf(item.Id));
                item.UnitCost = StockService.AverageCost(onHand, item.UnitCost, quantity, item.UnitCost);
                ledger.Add(StockService.Movement(item, locationId, main, StockMovementKind.GoodsReceipt, quantity, start, null, null, null,
                    $"A{start:yyMM}-{index + 1:00}", expires));
            }
        }

        foreach (var refill in DemoStock.PlanRefills(data.Places, data.Minimums, items, (locationId, item) => DemoStock.StockBuffer(data.Locations[locationId], item),
            (placeId, itemId) => -levels.GetValueOrDefault((placeId, itemId))))
        {
            foreach (var movement in StockService.MovePlaces(refill.Item, refill.LocationId, refill.FromPlaceId, refill.ToPlaceId, StockMovementKind.Refill,
                refill.Quantity, start, null, "Nachfüllen", ledger.Shares(refill.FromPlaceId, refill.Item.Id)))
            {
                ledger.Add(movement);
            }
        }

        BookUsage(data, ledger, now);
        await WriteAsync(db, tenantId, ledger.Movements, ct);
        await DemoStock.AddOrdersAsync(db, data.Locations, [.. data.Items.Values.AsEnumerable()], now, ct);
    }

    private sealed record Data(
        Dictionary<Guid, string> Locations, Dictionary<Guid, StockItem> Items, List<CompletedAppointment> Appointments,
        Dictionary<Guid, Dictionary<Guid, decimal>> Recipes, List<Receipt> Receipts, Dictionary<Guid, Guid> RegisterLocations,
        Dictionary<Guid, Guid> ArticleOfLine, Dictionary<Guid, StockItem> ItemOfArticle, List<StoragePlace> Places,
        Dictionary<(Guid Place, Guid Item), decimal> Minimums)
    {
        public List<StoragePlace> PlacesOf(Guid locationId) => [.. Places.Where(p => p.LocationId == locationId)];
    }

    private sealed record CompletedAppointment(Guid Id, Guid TreatmentId, Guid LocationId, Guid RoomId, DateTimeOffset End);

    private static async Task<Data> LoadAsync(AispaDbContext db, CancellationToken ct)
    {
        // Lagerartikel verfolgt der Kontext: der gleitende Einstandspreis ändert sie.
        var items = await db.StockItems.ToDictionaryAsync(i => i.Id, ct);
        return new Data(
            await db.Locations.AsNoTracking().ToDictionaryAsync(l => l.Id, l => l.Name, ct),
            items,
            await db.Appointments.AsNoTracking().Where(a => a.Status == AppointmentStatus.Completed).OrderBy(a => a.End)
                .ThenBy(a => a.Customer.LastName).ThenBy(a => a.Customer.FirstName)
                .Select(a => new CompletedAppointment(a.Id, a.TreatmentId, a.LocationId, a.RoomId, a.End)).ToListAsync(ct),
            await StockRecipes.ForAsync(db, await db.Treatments.AsNoTracking().Select(t => t.Id).ToListAsync(ct), ct),
            await db.Receipts.AsNoTracking().Include(r => r.Lines).Where(r => r.Kind == ReceiptKind.Sale || r.Kind == ReceiptKind.Void)
                .OrderBy(r => r.IssuedAt).ThenBy(r => r.Number).AsSplitQuery().ToListAsync(ct),
            await db.CashRegisters.AsNoTracking().ToDictionaryAsync(r => r.Id, r => r.LocationId, ct),
            await db.BillLines.AsNoTracking().Where(l => l.ArticleId != null).ToDictionaryAsync(l => l.Id, l => l.ArticleId!.Value, ct),
            items.Values.Where(i => i.ArticleId != null).ToDictionary(i => i.ArticleId!.Value),
            await db.StoragePlaces.AsNoTracking().ToListAsync(ct),
            await db.StoragePlaceMinimums.AsNoTracking().ToDictionaryAsync(m => (m.StoragePlaceId, m.ItemId), m => m.MinimumQuantity, ct));
    }

    /// <summary>
    /// Verbrauch der abgeschlossenen Termine (<c>StockService.ReconcileAppointmentAsync</c>: vom Raumschrank, sonst vom Hauptlager), danach Verkäufe und
    /// Stornos der Belege (<c>BookReceiptAsync</c>: vom Verkaufsregal).
    /// </summary>
    private static void BookUsage(Data data, Ledger ledger, DateTimeOffset now)
    {
        foreach (var appointment in data.Appointments)
        {
            var target = data.Recipes.GetValueOrDefault(appointment.TreatmentId) ?? [];
            var places = data.PlacesOf(appointment.LocationId);
            foreach (var (itemId, quantity) in target)
            {
                var (placeId, shortfall) = StockService.ConsumptionPlace(places, appointment.RoomId, quantity, id => ledger.QuantityAt(id, itemId));
                foreach (var part in StockService.Allocate(quantity, ledger.Shares(placeId, itemId)))
                {
                    ledger.Add(new StockMovement
                    {
                        ItemId = itemId,
                        LocationId = appointment.LocationId,
                        StoragePlaceId = placeId,
                        Kind = StockMovementKind.Consumption,
                        Quantity = -part.Quantity,
                        UnitCost = data.Items[itemId].UnitCost,
                        // Verbrauch zählt zum Ende der Behandlung, auch wenn sie später abgeschlossen wird.
                        At = appointment.End < now ? appointment.End : now,
                        AppointmentId = appointment.Id,
                        Reason = shortfall ? StockService.ShortfallReason : StockService.ConsumptionReason,
                        Batch = part.Batch,
                        ExpiresOn = part.ExpiresOn,
                    });
                }
            }
        }

        foreach (var receipt in data.Receipts)
        {
            BookReceipt(data, ledger, receipt);
        }
    }

    private static void BookReceipt(Data data, Ledger ledger, Receipt receipt)
    {
        var locationId = data.RegisterLocations[receipt.RegisterId];
        var salesPlaceId = StockService.SalesPlace(data.PlacesOf(locationId)).Id;
        var voided = receipt.Kind == ReceiptKind.Void && receipt.VoidsReceiptId is { } voids ? ledger.SalesOf(voids) : [];
        var movements = new List<StockMovement>();
        foreach (var sold in voided)
        {
            // Ein Storno gibt in die Chargen zurück, aus denen der Verkauf gebucht war.
            movements.Add(new StockMovement
            {
                ItemId = sold.ItemId,
                LocationId = locationId,
                StoragePlaceId = sold.StoragePlaceId,
                Kind = StockMovementKind.Sale,
                Quantity = -sold.Quantity,
                UnitCost = sold.UnitCost,
                At = receipt.IssuedAt,
                ReceiptId = receipt.Id,
                Reason = "Storno",
                Batch = sold.Batch,
                ExpiresOn = sold.ExpiresOn,
            });
        }

        if (voided.Count == 0)
        {
            var sign = receipt.Kind == ReceiptKind.Void ? 1 : -1;
            foreach (var group in receipt.Lines
                .Where(l => l.BillLineId is { } id && data.ArticleOfLine.TryGetValue(id, out var article) && data.ItemOfArticle.ContainsKey(article))
                .GroupBy(l => data.ItemOfArticle[data.ArticleOfLine[l.BillLineId!.Value]]))
            {
                var quantity = group.Sum(l => l.Quantity);
                if (quantity == 0)
                {
                    continue;
                }

                // Verkauft wird aus den Chargen, die zuerst ablaufen; ein Storno ohne auffindbaren Verkauf geht ohne Charge zurück.
                var parts = sign < 0
                    ? StockService.Allocate(quantity, ledger.Shares(salesPlaceId, group.Key.Id))
                    : [new StockService.Part(null, null, quantity)];
                movements.AddRange(parts.Select(part => new StockMovement
                {
                    ItemId = group.Key.Id,
                    LocationId = locationId,
                    StoragePlaceId = salesPlaceId,
                    Kind = StockMovementKind.Sale,
                    Quantity = sign * part.Quantity,
                    UnitCost = group.Key.UnitCost,
                    At = receipt.IssuedAt,
                    ReceiptId = receipt.Id,
                    Reason = receipt.Kind == ReceiptKind.Void ? "Storno" : null,
                    Batch = part.Batch,
                    ExpiresOn = part.ExpiresOn,
                }));
            }
        }

        foreach (var movement in movements)
        {
            ledger.Add(movement);
        }
    }

    private static async Task WriteAsync(AispaDbContext db, Guid tenantId, List<StockMovement> movements, CancellationToken ct)
    {
        var connection = (NpgsqlConnection)db.Database.GetDbConnection();
        await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await db.Database.OpenConnectionAsync(ct);
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            await db.StockMovements.ExecuteDeleteAsync(ct);
            var writer = new BulkWriter(connection, tenantId);
            await writer.WriteAsync(db.Model, movements, ct);
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        });
    }

    /// <summary>Die Bewegungen mit dem Bestand je Lagerort und Material und den Eingängen, aus denen sich die Chargen ergeben.</summary>
    private sealed class Ledger
    {
        private readonly Dictionary<(Guid Place, Guid Item), decimal> _quantity = [];
        private readonly Dictionary<(Guid Place, Guid Item), List<StockService.Incoming>> _incoming = [];
        private readonly Dictionary<Guid, decimal> _byItem = [];
        private readonly Dictionary<Guid, List<StockMovement>> _salesOfReceipt = [];

        public List<StockMovement> Movements { get; } = [];

        public void Add(StockMovement movement)
        {
            Movements.Add(movement);
            var key = (movement.StoragePlaceId, movement.ItemId);
            _quantity[key] = _quantity.GetValueOrDefault(key) + movement.Quantity;
            _byItem[movement.ItemId] = _byItem.GetValueOrDefault(movement.ItemId) + movement.Quantity;
            if (movement.Quantity > 0 && movement.Kind is StockMovementKind.GoodsReceipt or StockMovementKind.Transfer or StockMovementKind.Refill)
            {
                if (!_incoming.TryGetValue(key, out var list))
                {
                    _incoming[key] = list = [];
                }

                list.Add(new StockService.Incoming(movement.ItemId, movement.Quantity, movement.At, movement.Batch, movement.ExpiresOn));
            }

            if (movement is { Kind: StockMovementKind.Sale, ReceiptId: { } receiptId })
            {
                if (!_salesOfReceipt.TryGetValue(receiptId, out var sales))
                {
                    _salesOfReceipt[receiptId] = sales = [];
                }

                sales.Add(movement);
            }
        }

        public Dictionary<(Guid Place, Guid Item), decimal> Quantities() => new(_quantity);

        public decimal QuantityAt(Guid placeId, Guid itemId) => _quantity.GetValueOrDefault((placeId, itemId));

        public decimal QuantityOf(Guid itemId) => _byItem.GetValueOrDefault(itemId);

        public List<StockMovement> SalesOf(Guid receiptId) => _salesOfReceipt.GetValueOrDefault(receiptId) ?? [];

        public List<StockService.Share> Shares(Guid placeId, Guid itemId) =>
            StockService.SharesOf(_incoming.GetValueOrDefault((placeId, itemId)) ?? [],
                new Dictionary<Guid, decimal> { [itemId] = _quantity.GetValueOrDefault((placeId, itemId)) });
    }
}
