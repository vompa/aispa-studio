using AispaStudio.Api.Access;
using AispaStudio.Api.Data;
using AispaStudio.Api.Scheduling;
using AispaStudio.Api.Stock;
using AispaStudio.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using static AispaStudio.Api.Exports.ExportRequests;

namespace AispaStudio.Api.Exports;

/// <summary>
/// Listen des Lagers als Excel oder CSV (Export/Import, Aufgabe D), nur für die Leitung: Bestand je Lagerort, Inventur mit Differenzen (eingereichte Zählung),
/// Bewegungen eines Zeitraums, Bestellungen wie auf der Seite und die Ablaufliste der Chargen. Die Zahlen kommen aus denselben Diensten wie die Seite „Lager“.
/// </summary>
internal static class StockExports
{
    private const string Levels = "levels";
    private const string Count = "count";
    private const string Movements = "movements";
    private const string Orders = "orders";
    private const string Expiry = "expiry";

    public const string NoCount = "Keine eingereichte oder abgeschlossene Zählung an diesem Standort.";

    private static readonly string[] Lists = [Levels, Count, Movements, Orders, Expiry];

    public static IEndpointRouteBuilder MapStockExportEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/api/stock/locations/{locationId:guid}/export", async Task<Results<FileContentHttpResult, ValidationProblem, NotFound, ProblemHttpResult>> (
                Guid locationId, string? list, DateOnly? from, DateOnly? until, Guid? sessionId, string? format, AispaDbContext db, AccessService accessService,
                StockService stock, StockCountSessions counts, PurchaseOrders orders, ExportLog log, TimeProvider time, CancellationToken ct) =>
            {
                var (request, failure) = await LeadershipListAsync(locationId, list, Lists, format, db, accessService, ct);
                if (request is null)
                {
                    return failure!;
                }

                var zone = ZoneOf(request.Location);
                var today = LocalTime.LocalDate(time.GetUtcNow(), zone);
                switch (request.List)
                {
                    case Levels:
                        return await FileAsync(await LevelTableAsync(db, stock, locationId, ct), request.Format, "Bestand je Lagerort", null, "Lagerbestand", today, log, ct);
                    case Count:
                        var sessions = await counts.ListAsync(locationId, null, ct);
                        var session = sessionId is { } id
                            ? sessions.FirstOrDefault(s => s.Id == id)
                            : sessions.FirstOrDefault(s => s.Status == StockCountSessionStatus.Submitted) ?? sessions.FirstOrDefault(s => s.Status == StockCountSessionStatus.Closed);
                        if (session is null)
                        {
                            return sessionId is null ? Problem("list", NoCount) : TypedResults.NotFound();
                        }

                        var counted = LocalTime.LocalDate(session.StartedAt, zone);
                        return await FileAsync(CountTable(session), request.Format, "Inventur", $"{session.StoragePlaceName}, {Day(counted)}", "Inventur", counted, log, ct);
                    case Orders:
                        return await FileAsync(OrderTable(await orders.ListAsync(locationId, ct), zone), request.Format, "Bestellungen", null, "Bestellungen", today, log, ct);
                    case Expiry:
                        return await FileAsync(ExpiryTable(await stock.BatchesAsync(locationId, ct)), request.Format, "Ablaufliste", null, "Ablaufliste", today, log, ct);
                }

                if (RangeError(from, until) is { } rangeError)
                {
                    return Problem("from", rangeError);
                }

                var table = await MovementTableAsync(db, locationId, LocalTime.Day(from!.Value, zone).From, LocalTime.Day(until!.Value, zone).Until, zone, ct);
                return await FileAsync(table, request.Format, "Lagerbewegungen", $"{Day(from.Value)} bis {Day(until.Value)}", "Lagerbewegungen", from, log, ct);
            })
            .WithTags("Lager")
            .RequirePermission(AccessRight.ManageLocation)
            .WithName("ExportStockList")
            .WithSummary("Listen des Lagers als Excel oder CSV, nur für die Leitung: list=levels (Bestand je Lagerort), count (Inventur mit Differenzen: sessionId oder die "
                + "zuletzt eingereichte Zählung), movements (Bewegungen von from bis until), orders (Bestellungen), expiry (Ablaufliste der Chargen).");

        return routes;
    }

    private static async Task<ExportTable> LevelTableAsync(AispaDbContext db, StockService stock, Guid locationId, CancellationToken ct)
    {
        var overview = await stock.OverviewAsync(locationId, ct);
        var places = await db.StoragePlaces.AsNoTracking().Where(p => p.LocationId == locationId).ToDictionaryAsync(p => p.Id, p => p.Name, ct);
        var columns = new ExportColumn[]
        {
            new("Material", ColumnKind.Text), new("Lagerort", ColumnKind.Text), new("Menge", ColumnKind.Number), new("Einheit", ColumnKind.Text),
            new("Mindestbestand", ColumnKind.Number), new("Unter Mindestbestand", ColumnKind.YesNo),
        };
        var rows = overview.Items
            .SelectMany(item => (item.Places ?? []).Select(place => new object?[]
            {
                item.Name, places.GetValueOrDefault(place.StoragePlaceId), place.Quantity, item.Unit, item.MinimumQuantity, item.BelowMinimum,
            }))
            .ToList();
        return new ExportTable("Bestand je Lagerort", columns, rows);
    }

    private static ExportTable CountTable(StockCountSessionDto session)
    {
        var columns = new ExportColumn[]
        {
            new("Material", ColumnKind.Text), new("Lagerort", ColumnKind.Text), new("Einheit", ColumnKind.Text), new("Bestand", ColumnKind.Number),
            new("Gezählt", ColumnKind.Number), new("Differenz", ColumnKind.Number),
        };
        var rows = session.Lines.OrderBy(l => l.Name, StringComparer.CurrentCulture)
            .Select(l => new object?[] { l.Name, session.StoragePlaceName, l.Unit, l.Expected, l.Counted, l.Expected is { } expected ? l.Counted - expected : null })
            .ToList();
        return new ExportTable("Inventur", columns, rows);
    }

    private static async Task<ExportTable> MovementTableAsync(AispaDbContext db, Guid locationId, DateTimeOffset begin, DateTimeOffset end, TimeZoneInfo zone,
        CancellationToken ct)
    {
        // Wie die Bewegungsliste des Lagers, aber ohne deren Obergrenze von 1000 Zeilen: die Datei hat die Grenze aller Exporte.
        var movements = await db.StockMovements.AsNoTracking()
            .Where(m => m.LocationId == locationId && m.At >= begin && m.At < end)
            .Join(db.StockItems, m => m.ItemId, i => i.Id, (m, i) => new { Movement = m, Item = i.Name, i.Unit })
            .Join(db.StoragePlaces, x => x.Movement.StoragePlaceId, p => p.Id, (x, p) => new { x.Movement, x.Item, x.Unit, Place = p.Name })
            .OrderBy(x => x.Movement.At).ThenBy(x => x.Movement.Id)
            .Take(TableExport.MaxRows + 1)
            .ToListAsync(ct);
        var columns = new ExportColumn[]
        {
            new("Tag", ColumnKind.Date), new("Zeit", ColumnKind.Time), new("Material", ColumnKind.Text), new("Lagerort", ColumnKind.Text), new("Art", ColumnKind.Text),
            new("Menge", ColumnKind.Number), new("Einheit", ColumnKind.Text), new("Charge", ColumnKind.Text), new("Grund", ColumnKind.Text),
        };
        var rows = movements.Select(x =>
            {
                var local = TimeZoneInfo.ConvertTime(x.Movement.At, zone).DateTime;
                return new object?[]
                {
                    DateOnly.FromDateTime(local), TimeOnly.FromDateTime(local), x.Item, x.Place, KindText(x.Movement.Kind), x.Movement.Quantity, x.Unit, x.Movement.Batch,
                    x.Movement.Reason,
                };
            })
            .ToList();
        return new ExportTable("Lagerbewegungen", columns, rows);
    }

    private static ExportTable OrderTable(List<PurchaseOrderDto> orders, TimeZoneInfo zone)
    {
        var columns = new ExportColumn[]
        {
            new("Nummer", ColumnKind.Number), new("Lieferant", ColumnKind.Text), new("Status", ColumnKind.Text), new("Angelegt", ColumnKind.Date),
            new("Bestellt", ColumnKind.Date), new("Positionen", ColumnKind.Number), new("Summe netto", ColumnKind.Money),
        };
        var rows = orders
            .Select(o => new object?[]
            {
                o.Number, o.Supplier, OrderStatusText(o.Status), LocalTime.LocalDate(o.CreatedAt, zone),
                o.OrderedAt is { } ordered ? LocalTime.LocalDate(ordered, zone) : null, o.Lines.Count, o.Total,
            })
            .ToList();
        return new ExportTable("Bestellungen", columns, rows);
    }

    private static ExportTable ExpiryTable(StockBatchesDto batches)
    {
        var columns = new ExportColumn[]
        {
            new("Material", ColumnKind.Text), new("Charge", ColumnKind.Text), new("Ablaufdatum", ColumnKind.Date), new("Menge", ColumnKind.Number),
            new("Einheit", ColumnKind.Text), new("Lagerort", ColumnKind.Text), new("Abgelaufen", ColumnKind.YesNo),
        };
        var rows = batches.Batches.OrderBy(b => b.ExpiresOn).ThenBy(b => b.ItemName, StringComparer.CurrentCulture)
            .Select(b => new object?[] { b.ItemName, b.Batch, b.ExpiresOn, b.Quantity, b.Unit, b.StoragePlaceName, b.Expired })
            .ToList();
        return new ExportTable("Ablaufliste", columns, rows);
    }

    private static string KindText(StockMovementKind kind) => kind switch
    {
        StockMovementKind.GoodsReceipt => "Wareneingang",
        StockMovementKind.Consumption => "Verbrauch",
        StockMovementKind.Sale => "Verkauf",
        StockMovementKind.Count => "Inventur",
        StockMovementKind.Correction => "Korrektur",
        StockMovementKind.Transfer => "Umbuchung",
        StockMovementKind.Refill => "Nachfüllen",
        _ => kind.ToString(),
    };

    private static string OrderStatusText(PurchaseOrderStatus status) => status switch
    {
        PurchaseOrderStatus.Draft => "Entwurf",
        PurchaseOrderStatus.Ordered => "bestellt",
        PurchaseOrderStatus.PartiallyReceived => "teilweise geliefert",
        PurchaseOrderStatus.Received => "geliefert",
        _ => "storniert",
    };
}
