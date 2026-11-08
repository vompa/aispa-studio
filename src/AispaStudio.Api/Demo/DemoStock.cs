using AispaStudio.Api.Data;
using AispaStudio.Api.Pos;
using AispaStudio.Api.Stock;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Demo;

/// <summary>
/// Lager des Beispiel-Spas: die Materialien der Rezepturen und die Verkaufsprodukte der Kassa als Lagerartikel. Vor dem Verlauf angelegt,
/// damit abgeschlossene Behandlungen wie im Betrieb über die Queue abbuchen; danach Wareneingänge zum Beginn des Verlaufs und die Verkäufe.
/// </summary>
internal static class DemoStock
{
    /// <summary>Erfundene Lieferanten: Material der Behandlungen und Verkaufsprodukte.</summary>
    public const string MaterialSupplier = "Großhandel Wellnessbedarf";

    public const string ProductSupplier = "Kosmetikvertrieb Alpen";

    /// <summary>Mindestbestand je Einheit; Wäsche läuft über die Wäscherei und kommt nicht ins Lager.</summary>
    private static decimal? Minimum(string unit) => unit switch
    {
        "ml" or "g" => 1000,
        "Stk" => 30,
        "Set" => null,
        _ => 10,
    };

    public static async Task AddItemsAsync(AispaDbContext db, CancellationToken ct)
    {
        var materialSupplier = new StockSupplier
        {
            Name = MaterialSupplier,
            ContactPerson = "Eva Muster",
            Email = "bestellung@wellnessbedarf.example",
            Phone = "+43 1 234 56 40",
            LeadTimeDays = 3,
            MinimumOrderValue = 100,
            OrderChannel = SupplierOrderChannel.Email,
        };
        var productSupplier = new StockSupplier
        {
            Name = ProductSupplier,
            ContactPerson = "Paul Beispiel",
            Email = "verkauf@kosmetik-alpen.example",
            Phone = "+43 1 234 56 41",
            LeadTimeDays = 7,
            MinimumOrderValue = 250,
            OrderChannel = SupplierOrderChannel.Representative,
        };
        db.StockSuppliers.AddRange(materialSupplier, productSupplier);
        var materials = await db.TreatmentMaterials.AsNoTracking().ToListAsync(ct);
        foreach (var group in materials.GroupBy(m => (m.Name, m.Unit)).OrderBy(g => g.Key.Name, StringComparer.Ordinal))
        {
            if (Minimum(group.Key.Unit) is not { } minimum)
            {
                continue;
            }

            var item = new StockItem
            {
                Name = group.Key.Name,
                Unit = group.Key.Unit,
                UnitCost = group.First().UnitCost,
                MinimumQuantity = minimum,
                Supplier = materialSupplier,
                SupplierArticleNumber = $"WB-{100 + await db.StockItems.CountAsync(ct)}",
            };

            // Flüssiges in Flaschen à 500 ml, vier im Karton; der Richtwert je Behandlung ist die übliche Menge im Rezept.
            if (group.Key.Unit == "ml")
            {
                item.PackUnit = "Flasche";
                item.PackSize = 500;
                item.CaseUnit = "Karton";
                item.PacksPerCase = 4;
                item.TreatmentDose = group.Min(m => m.Quantity);
            }

            db.StockItems.Add(item);
            await db.SaveChangesAsync(ct);
            await db.TreatmentMaterials.Where(m => m.Name == item.Name && m.Unit == item.Unit)
                .ExecuteUpdateAsync(s => s.SetProperty(m => m.StockItemId, item.Id), ct);
        }

        // Verkaufsprodukte: Einstand etwa 40 % des Nettopreises.
        // Jedes Verkaufsprodukt trägt einen Code zum Ausprobieren des Handscanners (fortlaufend, nur für die Beispieldaten).
        var products = await db.Articles.AsNoTracking().Where(a => a.Name != PosDemoData.SaunaDayPass).OrderBy(a => a.Name).ToListAsync(ct);
        foreach (var (article, index) in products.Select((a, i) => (a, i)))
        {
            db.StockItems.Add(new StockItem
            {
                Name = article.Name,
                Unit = "Stk",
                UnitCost = decimal.Round(article.Price / 1.2m * 0.4m, 2),
                MinimumQuantity = 6,
                ArticleId = article.Id,
                Supplier = productSupplier,
                Barcode = $"9001234{index + 1:D6}",
            });
        }

        await db.SaveChangesAsync(ct);
    }

    /// <summary>Höchstens so viele Raumschränke je Standort: Hauptlager und Verkaufsregal gehören dazu, das Limit gilt für alle Lagerorte.</summary>
    private const int MaxCabinets = StockService.MaxPlacesPerLocation - 2;

    /// <summary>Mindestmenge im Raumschrank: so viele Behandlungen reichen mindestens.</summary>
    private const int CabinetTreatments = 5;

    /// <summary>
    /// Lagerorte des Beispiel-Spas: je Standort ein Verkaufsregal an der Rezeption und je Behandlungsraum ein Schrank mit Mindestmengen für das Material,
    /// das die Behandlungen im Raum brauchen. Das Hauptlager entsteht mit dem Standort. Nach den Lagerartikeln angelegt, weil die Mindestmengen sie brauchen.
    /// </summary>
    public static async Task AddPlacesAsync(AispaDbContext db, CancellationToken ct)
    {
        var rooms = await db.Rooms.AsNoTracking().Where(r => r.IsActive && !r.IsArea).ToListAsync(ct);
        var treatments = await db.Treatments.AsNoTracking().Include(t => t.Rooms).ToListAsync(ct);
        var recipes = await StockRecipes.ForAsync(db, [.. treatments.Select(t => t.Id)], ct);
        foreach (var location in await db.Locations.AsNoTracking().OrderBy(l => l.Name).ToListAsync(ct))
        {
            db.StoragePlaces.Add(new StoragePlace
            {
                LocationId = location.Id,
                Name = "Verkaufsregal Rezeption",
                Description = "Regal an der Rezeption für die Verkaufsprodukte",
                Kind = StoragePlaceKind.Sales,
                Position = 1,
            });
            foreach (var (room, index) in rooms.Where(r => r.LocationId == location.Id).OrderBy(r => r.Name, StringComparer.Ordinal).Take(MaxCabinets)
                .Select((r, n) => (r, n)))
            {
                var cabinet = new StoragePlace
                {
                    LocationId = location.Id,
                    Name = $"Schrank {room.Name}",
                    Description = $"Material für die Behandlungen im Raum {room.Name}",
                    Kind = StoragePlaceKind.Room,
                    RoomId = room.Id,
                    Position = index + 2,
                };
                db.StoragePlaces.Add(cabinet);
                var needs = treatments.Where(t => t.Rooms.Any(r => r.Id == room.Id))
                    .SelectMany(t => recipes.GetValueOrDefault(t.Id) ?? [])
                    .GroupBy(r => r.Key).ToDictionary(g => g.Key, g => g.Max(r => r.Value));
                foreach (var (itemId, quantity) in needs.OrderBy(n => n.Key))
                {
                    db.StoragePlaceMinimums.Add(new StoragePlaceMinimum
                    {
                        StoragePlaceId = cabinet.Id,
                        ItemId = itemId,
                        MinimumQuantity = Math.Ceiling(quantity * CabinetTreatments),
                    });
                }
            }
        }

        await db.SaveChangesAsync(ct);
    }

    /// <summary>Eine Nachfüllung aus dem Hauptlager an den ersten Tag des Verlaufs.</summary>
    internal sealed record Refill(Guid LocationId, Guid FromPlaceId, Guid ToPlaceId, StockItem Item, decimal Quantity);

    /// <summary>
    /// Anfangsbestand der Lagerorte aus dem Hauptlager: das Verkaufsregal bekommt, was bis heute verkauft wird, plus einen Mindestbestand; ein Raumschrank
    /// das Doppelte seiner Mindestmenge, höchstens aber ein Anteil des Puffers, damit das Hauptlager nie ins Minus rutscht. Eine Rechnung für beide Wege
    /// (Dienste und Speicher), damit ihre Lagerbewegungen gleich bleiben.
    /// </summary>
    internal static List<Refill> PlanRefills(IReadOnlyList<StoragePlace> places, IReadOnlyDictionary<(Guid Place, Guid Item), decimal> minimums,
        IReadOnlyList<StockItem> items, Func<Guid, StockItem, decimal> bufferAt, Func<Guid, Guid, decimal> soldAt)
    {
        var refills = new List<Refill>();
        foreach (var location in places.GroupBy(p => p.LocationId).OrderBy(g => g.Key))
        {
            var all = location.ToList();
            var main = StockService.MainOf(all);
            var shelf = StockService.SalesPlace(all);
            foreach (var item in items)
            {
                if (item.ArticleId is not null && shelf.Kind == StoragePlaceKind.Sales)
                {
                    refills.Add(new(location.Key, main.Id, shelf.Id, item, Math.Ceiling(Math.Max(soldAt(shelf.Id, item.Id), 0)) + item.MinimumQuantity));
                }

                var cabinets = all.Where(p => p.Kind == StoragePlaceKind.Room && minimums.ContainsKey((p.Id, item.Id))).OrderBy(p => p.Position).ToList();
                foreach (var cabinet in cabinets)
                {
                    var quantity = Math.Min(2 * minimums[(cabinet.Id, item.Id)], Math.Floor(bufferAt(location.Key, item) / (2 * cabinets.Count)));
                    if (quantity > 0)
                    {
                        refills.Add(new(location.Key, main.Id, cabinet.Id, item, quantity));
                    }
                }
            }
        }

        return refills;
    }

    /// <summary>
    /// Verbrauch der abgeschlossenen Termine (wie die Queue, die dann nichts mehr zu buchen findet) und die Verkäufe und Stornos der Kassa,
    /// dazu je Standort ein Wareneingang zu Beginn des Verlaufs: was seither abging plus anderthalb Mindestbestände, damit der Bestand heute
    /// knapp über dem Mindestbestand liegt. Das Aromaöl in Graz ist bewusst knapp (Bestellvorschlag). Der Wareneingang kommt ins Hauptlager;
    /// am ersten Tag füllt es das Verkaufsregal und die Raumschränke nach (<see cref="PlanRefills"/>). Der Verbrauch wird zweimal gebucht:
    /// einmal, um den Bedarf zu kennen, und nach Wareneingang und Nachfüllen noch einmal, damit er die Charge trägt (Rückverfolgung) und aus
    /// dem Raumschrank kommt, solange dort etwas liegt.
    /// </summary>
    public static async Task AddMovementsAsync(AispaDbContext db, DateTimeOffset start, DateTimeOffset now, CancellationToken ct)
    {
        await BookUsageAsync(db, now, ct);
        var levels = await db.StockMovements.AsNoTracking().GroupBy(m => new { m.LocationId, m.StoragePlaceId, m.ItemId })
            .Select(g => new { g.Key.LocationId, g.Key.StoragePlaceId, g.Key.ItemId, Quantity = g.Sum(m => m.Quantity) })
            .ToListAsync(ct);
        await db.StockMovements.ExecuteDeleteAsync(ct);

        var stock = new StockService(db, new DemoClock(start));
        var items = await db.StockItems.AsNoTracking().ToListAsync(ct);
        var locations = await db.Locations.AsNoTracking().ToDictionaryAsync(l => l.Id, l => l.Name, ct);
        var places = await db.StoragePlaces.AsNoTracking().ToListAsync(ct);
        var minimums = await db.StoragePlaceMinimums.AsNoTracking().ToDictionaryAsync(m => (m.StoragePlaceId, m.ItemId), m => m.MinimumQuantity, ct);
        // Anfangsbestand mit Charge und Ablaufdatum; in Wien läuft das erste Verkaufsprodukt in drei Wochen ab.
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var soon = items.Where(i => i.ArticleId != null).OrderBy(i => i.Name, StringComparer.Ordinal).First().Id;
        var ordered = items.OrderBy(i => i.Name, StringComparer.Ordinal).ToList();
        foreach (var (locationId, name) in locations.OrderBy(l => l.Value, StringComparer.Ordinal))
        {
            foreach (var (item, index) in ordered.Select((i, n) => (i, n)))
            {
                var used = -levels.Where(l => l.LocationId == locationId && l.ItemId == item.Id).Sum(l => l.Quantity);
                var expires = item.Id == soon && name.StartsWith("Wien", StringComparison.Ordinal) ? today.AddDays(21) : today.AddMonths(9);
                await stock.AddAsync(locationId, new StockMovementInput(item.Id, StockMovementKind.GoodsReceipt, Math.Ceiling(Math.Max(used, 0) + StockBuffer(name, item)),
                    item.UnitCost, Batch: $"A{start:yyMM}-{index + 1:00}", ExpiresOn: expires), null, ct);
            }
        }

        foreach (var refill in PlanRefills(places, minimums, ordered, (locationId, item) => StockBuffer(locations[locationId], item),
            (placeId, itemId) => -levels.Where(l => l.StoragePlaceId == placeId && l.ItemId == itemId).Sum(l => l.Quantity)))
        {
            await stock.AddAsync(refill.LocationId, new StockMovementInput(refill.Item.Id, StockMovementKind.Refill, refill.Quantity,
                StoragePlaceId: refill.FromPlaceId, TargetStoragePlaceId: refill.ToPlaceId), null, ct);
        }

        await BookUsageAsync(db, now, ct);
        await AddOrdersAsync(db, locations, items, now, ct);
    }

    /// <summary>Was nach dem Verbrauch des Verlaufs im Hauptlager bleibt; das Aromaöl in Graz ist bewusst knapp.</summary>
    internal static decimal StockBuffer(string locationName, StockItem item) =>
        locationName.StartsWith("Graz", StringComparison.Ordinal) && item.Name.StartsWith("Aromaöl", StringComparison.Ordinal)
            ? item.MinimumQuantity * 0.4m
            : item.MinimumQuantity * 1.5m;

    private static async Task BookUsageAsync(AispaDbContext db, DateTimeOffset now, CancellationToken ct)
    {
        var stock = new StockService(db, new DemoClock(now));
        foreach (var appointment in await db.Appointments.AsNoTracking().Where(a => a.Status == AppointmentStatus.Completed)
            .OrderBy(a => a.End).ThenBy(a => a.Customer.LastName).ThenBy(a => a.Customer.FirstName).Select(a => a.Id).ToListAsync(ct))
        {
            await stock.ReconcileAppointmentAsync(appointment, ct);
        }

        foreach (var receiptId in await db.Receipts.AsNoTracking().Where(r => r.Kind == ReceiptKind.Sale || r.Kind == ReceiptKind.Void)
            .OrderBy(r => r.IssuedAt).ThenBy(r => r.Number).Select(r => r.Id).ToListAsync(ct))
        {
            await stock.BookReceiptAsync(receiptId, ct);
        }
    }

    /// <summary>
    /// Bestellungen in Wien: eine vor zehn Tagen bestellte und drei Tage später gelieferte beim Großhandel, eine gestern bestellte und
    /// noch offene bei den Verkaufsprodukten. Graz behält seinen Bestellvorschlag.
    /// </summary>
    internal static async Task AddOrdersAsync(AispaDbContext db, Dictionary<Guid, string> locations, List<StockItem> items, DateTimeOffset now,
        CancellationToken ct)
    {
        var vienna = locations.Where(l => l.Value.StartsWith("Wien", StringComparison.Ordinal)).OrderBy(l => l.Value, StringComparer.Ordinal)
            .Select(l => l.Key).First();
        var clock = new DemoClock(now.AddDays(-10));
        var orders = new PurchaseOrders(db, new StockService(db, clock), clock);
        var materialSupplier = await db.StockSuppliers.AsNoTracking().Where(s => s.Name == MaterialSupplier).Select(s => s.Id).FirstAsync(ct);
        var materials = items.Where(i => i.SupplierId == materialSupplier).OrderBy(i => i.Name, StringComparer.Ordinal).Take(3).ToList();
        var (delivered, _) = await orders.CreateAsync(vienna, new PurchaseOrderInput(MaterialSupplier,
            [.. materials.Select(i => new PurchaseOrderLineInput(i.Id, i.MinimumQuantity))], "Lieferung an den Hintereingang"), null, ct);
        await orders.MarkOrderedAsync(delivered!.Id, ct);
        clock.Now = now.AddDays(-7);
        await orders.DeliverAsync(delivered.Id, new PurchaseOrderDeliveryInput([.. delivered.Lines.Select((l, n) => new PurchaseOrderDeliveryLineInput(l.Id, l.Quantity, Batch: $"G{now:yyMM}-{n + 1}",
                ExpiresOn: DateOnly.FromDateTime(now.UtcDateTime).AddMonths(14)))],
            "LS-2026-0815"), null, ct);

        clock.Now = now.AddDays(-1);
        var product = items.Where(i => i.ArticleId != null).OrderBy(i => i.Name, StringComparer.Ordinal).First();
        var (open, _) = await orders.CreateAsync(vienna, new PurchaseOrderInput(ProductSupplier, [new PurchaseOrderLineInput(product.Id, 12)]), null, ct);
        await orders.MarkOrderedAsync(open!.Id, ct);
    }
}
