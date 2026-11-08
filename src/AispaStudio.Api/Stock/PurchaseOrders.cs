using System.Globalization;
using System.Text;
using AispaStudio.Api.Data;
using AispaStudio.Api.Mail;
using AispaStudio.Api.Scheduling;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Stock;

/// <summary>Bestellung eines Standorts bei einem Lieferanten; die Lieferung bucht den Wareneingang ins Lager.</summary>
public sealed class PurchaseOrder
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public int Number { get; init; }
    public Guid LocationId { get; init; }
    public required string Supplier { get; set; }
    public string? Note { get; set; }
    public PurchaseOrderStatus Status { get; set; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? OrderedAt { get; set; }
    public DateTimeOffset? ClosedAt { get; set; }
    public string? UserId { get; init; }
    public List<PurchaseOrderLine> Lines { get; init; } = [];
}

public sealed class PurchaseOrderLine
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid OrderId { get; init; }
    public Guid ItemId { get; init; }
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal Received { get; set; }
}

/// <summary>
/// Bestellungen: aus dem Bestellvorschlag je Lieferant, als Text zum Verschicken durch das Spa oder nach Freigabe per Mail, Lieferungen (auch in Teilen) als Wareneingang.
/// An Lieferanten geht nie etwas ohne Freigabe in der Maske.
/// </summary>
internal sealed class PurchaseOrders(AispaDbContext db, StockService stock, TimeProvider time)
{
    /// <summary>Status, deren Restmengen als „bestellt“ zählen und den Bestellvorschlag mindern.</summary>
    public static readonly PurchaseOrderStatus[] Open = [PurchaseOrderStatus.Draft, PurchaseOrderStatus.Ordered, PurchaseOrderStatus.PartiallyReceived];

    /// <summary>Über so viele Tage wird der Tagesverbrauch gemittelt.</summary>
    public const int UsageDays = 90;

    private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-AT");

    /// <summary>
    /// Bestellmenge in der Einheit des Materials. Erwarteter Stand = Bestand + offen bestellt − (Tagesverbrauch × Lieferzeit + gebuchte Termine); er darf negativ sein, weil das Lager beim Verbrauch ins Minus bucht.
    /// Fällt er unter die Mindestmenge oder nicht über 0, wird bis zur Höchstmenge bestellt (ohne Höchstmenge bis zur Mindestmenge), aufgerundet auf ganze Packungen, sonst auf ganze Einheiten.
    /// Ohne bekannte Lieferzeit zählt kein Tagesverbrauch.
    /// </summary>
    internal static decimal ReorderQuantity(decimal onHand, decimal dailyUse, int? leadTimeDays, decimal booked, decimal onOrder, decimal minimum, decimal? maximum, decimal? packSize)
    {
        var expected = onHand + onOrder - (dailyUse * (leadTimeDays ?? 0)) - booked;
        if (expected >= minimum && expected > 0)
        {
            return 0;
        }

        var missing = ReorderTarget(minimum, maximum) - expected;
        if (missing <= 0)
        {
            return 0;
        }

        return packSize is { } size ? Math.Ceiling(missing / size) * size : Math.Ceiling(missing);
    }

    /// <summary>Bis hierher füllt der Vorschlag auf: die Höchstmenge, mindestens aber der Mindestbestand.</summary>
    internal static decimal ReorderTarget(decimal minimum, decimal? maximum) => Math.Max(maximum ?? minimum, minimum);

    public async Task<List<PurchaseOrderDto>> ListAsync(Guid locationId, CancellationToken ct)
    {
        // Alle offenen, dazu die letzten 30 abgeschlossenen.
        var open = await Query().Where(o => o.LocationId == locationId && Open.Contains(o.Status)).ToListAsync(ct);
        var closed = await Query().Where(o => o.LocationId == locationId && !Open.Contains(o.Status))
            .OrderByDescending(o => o.Number).Take(30).ToListAsync(ct);
        return await ToDtosAsync([.. open.Concat(closed).OrderByDescending(o => o.Number)], ct);
    }

    public async Task<PurchaseOrderDto?> GetAsync(Guid id, CancellationToken ct) =>
        await Query().FirstOrDefaultAsync(o => o.Id == id, ct) is { } order ? (await ToDtosAsync([order], ct))[0] : null;

    public async Task<(PurchaseOrderDto? Order, Errors? Errors)> CreateAsync(Guid locationId, PurchaseOrderInput input, string? userId,
        CancellationToken ct)
    {
        var (lines, errors) = await ValidateAsync(input, ct);
        if (errors.Any)
        {
            return (null, errors);
        }

        var order = await AddAsync(locationId, input.Supplier.Trim(), Blank(input.Note), lines, userId, ct);
        return ((await ToDtosAsync([order], ct))[0], null);
    }

    /// <summary>
    /// Legt aus dem Bestellvorschlag je Lieferant einen Entwurf an. Materialien ohne Lieferant bleiben draußen; schon offen bestellte Mengen
    /// sind im Vorschlag abgezogen, ein zweiter Aufruf legt also nichts doppelt an.
    /// </summary>
    public async Task<List<PurchaseOrderDto>> FromSuggestionsAsync(Guid locationId, string? userId, CancellationToken ct)
    {
        var overview = await stock.OverviewAsync(locationId, ct);
        var items = await db.StockItems.AsNoTracking().ToDictionaryAsync(i => i.Id, ct);
        var orders = new List<PurchaseOrder>();
        foreach (var group in overview.Items.Where(l => l.Suggested > 0 && !string.IsNullOrWhiteSpace(l.Supplier))
                     .GroupBy(l => l.Supplier!.Trim(), StringComparer.OrdinalIgnoreCase).OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase))
        {
            var lines = group.Select(l => new PurchaseOrderLine { ItemId = l.ItemId, Quantity = l.Suggested, UnitCost = items[l.ItemId].UnitCost }).ToList();
            orders.Add(await AddAsync(locationId, group.First().Supplier!.Trim(), null, lines, userId, ct));
        }

        return await ToDtosAsync(orders, ct);
    }

    public async Task<(PurchaseOrderDto? Order, Errors? Errors)> UpdateAsync(Guid id, PurchaseOrderInput input, CancellationToken ct)
    {
        var order = await db.PurchaseOrders.Include(o => o.Lines).FirstAsync(o => o.Id == id, ct);
        var (lines, errors) = await ValidateAsync(input, ct);
        errors.Check(order.Status == PurchaseOrderStatus.Draft, "status", "Nur Entwürfe lassen sich ändern.");
        if (errors.Any)
        {
            return (null, errors);
        }

        order.Supplier = input.Supplier.Trim();
        order.Note = Blank(input.Note);
        db.PurchaseOrderLines.RemoveRange(order.Lines);
        order.Lines.Clear();
        foreach (var line in lines)
        {
            var added = new PurchaseOrderLine { OrderId = order.Id, ItemId = line.ItemId, Quantity = line.Quantity, UnitCost = line.UnitCost };
            order.Lines.Add(added);
            db.PurchaseOrderLines.Add(added);
        }

        await db.SaveChangesAsync(ct);
        return ((await ToDtosAsync([order], ct))[0], null);
    }

    /// <summary>Entwurf als bestellt markieren, nachdem das Spa ihn an den Lieferanten geschickt hat.</summary>
    public async Task<(PurchaseOrderDto? Order, Errors? Errors)> MarkOrderedAsync(Guid id, CancellationToken ct)
    {
        var order = await db.PurchaseOrders.Include(o => o.Lines).FirstAsync(o => o.Id == id, ct);
        var errors = new Errors();
        errors.Check(order.Status == PurchaseOrderStatus.Draft, "status", "Die Bestellung ist schon bestellt oder abgeschlossen.");
        if (errors.Any)
        {
            return (null, errors);
        }

        order.Status = PurchaseOrderStatus.Ordered;
        order.OrderedAt = time.GetUtcNow();
        await db.SaveChangesAsync(ct);
        return ((await ToDtosAsync([order], ct))[0], null);
    }

    /// <summary>
    /// „Bestellen und senden“ nach der Freigabe in der Maske: setzt den Entwurf auf „bestellt“ und liefert die Mail an den Lieferanten für die Queue. Die Bestellung wird dafür gesperrt,
    /// damit ein zweiter Klick nicht zweimal sendet.
    /// </summary>
    public async Task<(PurchaseOrderDto? Order, OutgoingMail? Mail, Errors? Errors)> SendAsync(Guid id, CancellationToken ct)
    {
        Errors? failed = null;
        var strategy = db.Database.CreateExecutionStrategy();
        var email = await strategy.ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await db.Database.ExecuteSqlAsync($"""SELECT 1 FROM aispa."PurchaseOrders" WHERE "Id" = {id} FOR UPDATE""", ct);
            var order = await db.PurchaseOrders.FirstAsync(o => o.Id == id, ct);
            var address = (await SupplierOfAsync(order.Supplier, ct))?.Email;
            var errors = new Errors();
            errors.Check(order.Status == PurchaseOrderStatus.Draft, "status", "Die Bestellung ist schon bestellt oder abgeschlossen.");
            errors.Check(address is not null, "supplier", "Beim Lieferanten fehlt die Mailadresse. Trage sie im Reiter „Lieferanten“ ein.");
            if (errors.Any)
            {
                failed = errors;
                return null;
            }

            order.Status = PurchaseOrderStatus.Ordered;
            order.OrderedAt = time.GetUtcNow();
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return address;
        });

        if (email is null)
        {
            return (null, null, failed);
        }

        // Der Stand „bestellt“ ist schon gespeichert: Ein Abbruch der Anfrage darf die Mail jetzt nicht mehr verhindern.
        var sent = (await GetAsync(id, CancellationToken.None))!;
        var text = $"Guten Tag,\n\nbitte liefern Sie uns die folgende Bestellung.\n\n{await TextAsync(id, CancellationToken.None)}\nVielen Dank!\n";
        return (sent, new OutgoingMail(email, $"Bestellung {sent.Number}", text), null);
    }

    /// <summary>Stornieren bzw. abschließen: ohne Lieferung „storniert“, nach einer Teillieferung „geliefert“, der Rest kommt nicht mehr.</summary>
    public async Task<(PurchaseOrderDto? Order, Errors? Errors)> CloseAsync(Guid id, CancellationToken ct)
    {
        var order = await db.PurchaseOrders.Include(o => o.Lines).FirstAsync(o => o.Id == id, ct);
        var errors = new Errors();
        errors.Check(Open.Contains(order.Status), "status", "Die Bestellung ist schon abgeschlossen.");
        if (errors.Any)
        {
            return (null, errors);
        }

        order.Status = order.Lines.Any(l => l.Received > 0) ? PurchaseOrderStatus.Received : PurchaseOrderStatus.Cancelled;
        order.ClosedAt = time.GetUtcNow();
        await db.SaveChangesAsync(ct);
        return ((await ToDtosAsync([order], ct))[0], null);
    }

    /// <summary>
    /// Lieferung buchen: je Position ein Wareneingang mit Bestell- oder Lieferscheinpreis. Die Bestellung wird dafür gesperrt, damit dieselbe
    /// Lieferung nicht doppelt ins Lager kommt. Sind alle Positionen voll geliefert, ist die Bestellung abgeschlossen.
    /// </summary>
    public async Task<(PurchaseOrderDto? Order, Errors? Errors)> DeliverAsync(Guid id, PurchaseOrderDeliveryInput input, string? userId,
        CancellationToken ct)
    {
        Errors? failed = null;
        var strategy = db.Database.CreateExecutionStrategy();
        var order = await strategy.ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await db.Database.ExecuteSqlAsync($"""SELECT 1 FROM aispa."PurchaseOrders" WHERE "Id" = {id} FOR UPDATE""", ct);
            var order = await db.PurchaseOrders.Include(o => o.Lines).FirstAsync(o => o.Id == id, ct);
            var errors = new Errors();
            errors.Check(order.Status is PurchaseOrderStatus.Ordered or PurchaseOrderStatus.PartiallyReceived, "status",
                order.Status == PurchaseOrderStatus.Draft ? "Erst als bestellt markieren." : "Die Bestellung ist schon abgeschlossen.");
            errors.Check(input.Lines is { Count: > 0 } && input.Lines.Any(l => l.Quantity > 0), nameof(input.Lines), "Gelieferte Menge angeben.");
            errors.Check(input.DeliveryNote is null || input.DeliveryNote.Length <= 100, nameof(input.DeliveryNote), "Lieferschein mit höchstens 100 Zeichen.");
            foreach (var line in input.Lines ?? [])
            {
                errors.Check(order.Lines.Any(l => l.Id == line.LineId), nameof(input.Lines), "Position gehört nicht zu dieser Bestellung.");
                errors.Check(line.Quantity >= 0 && line.Quantity < 1_000_000 && decimal.Round(line.Quantity, 3) == line.Quantity, nameof(input.Lines),
                    "Menge ab 0 mit höchstens drei Nachkommastellen.");
                errors.Check(line.UnitCost is null or >= 0 and < 1_000_000, nameof(input.Lines), "Preis nicht negativ.");
                StockService.CheckBatch(errors, line.Batch, line.ExpiresOn, nameof(input.Lines));
            }

            errors.Check((input.Lines ?? []).Select(l => l.LineId).Distinct().Count() == (input.Lines ?? []).Count, nameof(input.Lines),
                "Jede Position nur einmal.");
            if (errors.Any)
            {
                failed = errors;
                return null;
            }

            var now = time.GetUtcNow();
            var reason = $"Bestellung {order.Number}" + (Blank(input.DeliveryNote) is { } note ? $", Lieferschein {note}" : "");
            var items = await db.StockItems.Where(i => order.Lines.Select(l => l.ItemId).Contains(i.Id)).ToDictionaryAsync(i => i.Id, ct);
            var mainPlace = await stock.MainPlaceIdAsync(order.LocationId, ct);
            foreach (var delivered in input.Lines!.Where(l => l.Quantity > 0))
            {
                var line = order.Lines.First(l => l.Id == delivered.LineId);
                await stock.GoodsReceiptAsync(items[line.ItemId], order.LocationId, mainPlace, delivered.Quantity, delivered.UnitCost ?? line.UnitCost, now,
                    userId, reason, order.Id, ct, delivered.Batch, delivered.ExpiresOn);
                line.Received += delivered.Quantity;
            }

            var complete = order.Lines.All(l => l.Received >= l.Quantity);
            order.Status = complete ? PurchaseOrderStatus.Received : PurchaseOrderStatus.PartiallyReceived;
            order.ClosedAt = complete ? now : null;
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return order;
        });

        return order is null ? (null, failed) : ((await ToDtosAsync([order], ct))[0], null);
    }

    /// <summary>Bestellung als Text zum Kopieren in eine Mail oder zum Ausdrucken; das Spa schickt sie selbst an den Lieferanten.</summary>
    public async Task<string> TextAsync(Guid id, CancellationToken ct)
    {
        var order = (await GetAsync(id, ct))!;
        var location = await db.Locations.AsNoTracking().FirstAsync(l => l.Id == order.LocationId, ct);
        var text = new StringBuilder();
        text.AppendLine(CultureInfo.InvariantCulture, $"Bestellung {order.Number} an {order.Supplier}");
        text.AppendLine(CultureInfo.InvariantCulture, $"Lieferung an: {string.Join(", ", new[] { location.Name, location.Street, $"{location.PostalCode} {location.City}".Trim() }.Where(p => !string.IsNullOrWhiteSpace(p)))}");
        text.AppendLine();
        foreach (var line in order.Lines)
        {
            var number = line.SupplierArticleNumber is { } n ? $"{n}  " : "";
            var amount = line.PackageText ?? $"{line.Quantity.ToString("0.###", German)} {line.Unit}";
            text.AppendLine(CultureInfo.InvariantCulture, $"{amount}  {number}{line.ItemName}");
        }

        if (order.Note is { } note)
        {
            text.AppendLine();
            text.AppendLine(note);
        }

        return text.ToString();
    }

    private IQueryable<PurchaseOrder> Query() => db.PurchaseOrders.AsNoTracking().Include(o => o.Lines);

    private async Task<(List<PurchaseOrderLine> Lines, Errors Errors)> ValidateAsync(PurchaseOrderInput input, CancellationToken ct)
    {
        var errors = new Errors();
        errors.Check(!string.IsNullOrWhiteSpace(input.Supplier) && input.Supplier.Trim().Length <= 200, nameof(input.Supplier), "Lieferanten angeben.");
        errors.Check(input.Note is null || input.Note.Length <= 1000, nameof(input.Note), "Anmerkung mit höchstens 1000 Zeichen.");
        errors.Check(input.Lines is { Count: > 0 and <= 200 }, nameof(input.Lines), "Mindestens eine Position.");
        var ids = (input.Lines ?? []).Select(l => l.ItemId).ToList();
        var items = await db.StockItems.AsNoTracking().Where(i => ids.Contains(i.Id)).ToDictionaryAsync(i => i.Id, ct);
        errors.Check(ids.Distinct().Count() == ids.Count, nameof(input.Lines), "Jedes Material nur einmal.");
        foreach (var line in input.Lines ?? [])
        {
            errors.Check(items.ContainsKey(line.ItemId), nameof(input.Lines), "Material nicht gefunden.");
            errors.Check(line.Quantity > 0 && line.Quantity < 1_000_000 && decimal.Round(line.Quantity, 3) == line.Quantity, nameof(input.Lines),
                "Menge größer als 0 mit höchstens drei Nachkommastellen.");
            errors.Check(line.UnitCost is null or >= 0 and < 1_000_000, nameof(input.Lines), "Preis nicht negativ.");
        }

        var lines = errors.Any
            ? []
            : input.Lines!.Select(l => new PurchaseOrderLine { ItemId = l.ItemId, Quantity = l.Quantity, UnitCost = l.UnitCost ?? items[l.ItemId].UnitCost })
                .ToList();
        return (lines, errors);
    }

    /// <summary>Neue Bestellung mit der nächsten Nummer des Unternehmens; bei gleichzeitiger Vergabe (eindeutiger Index) mit der folgenden.</summary>
    private async Task<PurchaseOrder> AddAsync(Guid locationId, string supplier, string? note, List<PurchaseOrderLine> lines, string? userId,
        CancellationToken ct)
    {
        for (var attempt = 0; ; attempt++)
        {
            var number = (await db.PurchaseOrders.MaxAsync(o => (int?)o.Number, ct) ?? 0) + 1;
            var order = new PurchaseOrder
            {
                Number = number,
                LocationId = locationId,
                Supplier = supplier,
                Note = note,
                Status = PurchaseOrderStatus.Draft,
                CreatedAt = time.GetUtcNow(),
                UserId = userId,
            };
            foreach (var line in lines)
            {
                order.Lines.Add(new PurchaseOrderLine { OrderId = order.Id, ItemId = line.ItemId, Quantity = line.Quantity, UnitCost = line.UnitCost });
            }

            db.PurchaseOrders.Add(order);
            try
            {
                await db.SaveChangesAsync(ct);
                return order;
            }
            catch (DbUpdateException) when (attempt < 3)
            {
                db.ChangeTracker.Clear();
            }
        }
    }

    private async Task<List<PurchaseOrderDto>> ToDtosAsync(List<PurchaseOrder> orders, CancellationToken ct)
    {
        var ids = orders.SelectMany(o => o.Lines).Select(l => l.ItemId).Distinct().ToList();
        var items = await db.StockItems.AsNoTracking().Where(i => ids.Contains(i.Id)).ToDictionaryAsync(i => i.Id, ct);
        var suppliers = await db.StockSuppliers.AsNoTracking().ToListAsync(ct);
        return [.. orders.Select(o =>
        {
            var total = decimal.Round(o.Lines.Sum(l => l.Quantity * l.UnitCost), 2);
            var supplier = suppliers.FirstOrDefault(s => string.Equals(s.Name, o.Supplier, StringComparison.OrdinalIgnoreCase));
            return new PurchaseOrderDto(o.Id, o.Number, o.LocationId, o.Supplier, o.Status, o.Note, o.CreatedAt, o.OrderedAt, o.ClosedAt, total,
                [.. o.Lines.OrderBy(l => items[l.ItemId].Name, StringComparer.CurrentCulture).Select(l => new PurchaseOrderLineDto(l.Id, l.ItemId,
                    items[l.ItemId].Name, items[l.ItemId].Unit, items[l.ItemId].SupplierArticleNumber, l.Quantity, l.UnitCost, l.Received, StockPackaging.PackageText(items[l.ItemId], l.Quantity)))],
                supplier?.MinimumOrderValue, Math.Max(0, (supplier?.MinimumOrderValue ?? 0) - total), supplier?.Email);
        })];
    }

    /// <summary>Die Bestellung hält den Namen des Lieferanten als Text; der Stamm wird über den Namen gefunden, Groß- und Kleinschreibung zählen nicht.</summary>
    private async Task<StockSupplier?> SupplierOfAsync(string name, CancellationToken ct) =>
        (await db.StockSuppliers.AsNoTracking().ToListAsync(ct)).FirstOrDefault(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase));

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
