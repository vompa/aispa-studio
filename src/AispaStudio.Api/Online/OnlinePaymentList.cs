using AispaStudio.Api.Access;
using AispaStudio.Api.Data;
using AispaStudio.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Online;

/// <summary>Belegnummern in Worten („K1 Nr. 12, 17.11.2026“) für Listen, die nur die Ids der Belege kennen.</summary>
internal static class ReceiptTexts
{
    public static async Task<IReadOnlyDictionary<Guid, string>> LoadAsync(AispaDbContext db, IEnumerable<Guid?> receiptIds, CancellationToken ct)
    {
        var ids = receiptIds.OfType<Guid>().Distinct().ToList();
        return ids.Count == 0
            ? new Dictionary<Guid, string>()
            : await db.Receipts.AsNoTracking().Where(r => ids.Contains(r.Id))
                .Join(db.CashRegisters.AsNoTracking(), r => r.RegisterId, c => c.Id, (r, c) => new { r.Id, c.Code, r.Number, r.IssuedAt })
                .ToDictionaryAsync(r => r.Id, r => $"{r.Code} Nr. {r.Number}, {r.IssuedAt:dd.MM.yyyy}", ct);
    }
}

/// <summary>Liste „Online-Zahlungen“ in der Kassa: Erstattungen online bezahlter Belege eines Standorts, neueste zuerst.</summary>
internal static class OnlinePaymentEndpoints
{
    public static IEndpointRouteBuilder MapOnlinePaymentEndpoints(this IEndpointRouteBuilder routes)
    {
        // Der Standort wird über das Fachmodell geprüft, damit der Mandantenfilter auch hier greift.
        routes.MapGet("/api/locations/{locationId:guid}/online-refunds", async Task<Results<Ok<List<OnlineRefundStaffEntry>>, NotFound>> (
                Guid locationId, AispaDbContext db, OnlineDbContext online, CancellationToken ct) =>
            {
                if (!await db.Locations.AnyAsync(l => l.Id == locationId, ct))
                {
                    return TypedResults.NotFound();
                }

                var refunds = await online.Refunds.AsNoTracking()
                    .Where(r => r.LocationId == locationId)
                    .OrderByDescending(r => r.RequestedAt)
                    .Take(100)
                    .ToListAsync(ct);
                var receipts = await ReceiptTexts.LoadAsync(db, refunds.Select(r => (Guid?)r.ReceiptId).Concat(refunds.Select(r => r.VoidReceiptId)), ct);
                return TypedResults.Ok(refunds.Select(r => new OnlineRefundStaffEntry(r.ReceiptId, receipts.GetValueOrDefault(r.ReceiptId),
                    r.VoidReceiptId is { } voidId ? receipts.GetValueOrDefault(voidId) : null, r.Source, r.Amount, r.Currency, r.Status, r.RequestedAt,
                    r.RefundedAt, r.Error)).ToList());
            })
            .RequirePermission(AccessRight.Book)
            .WithTags("Kassa")
            .WithName("ListOnlineRefunds")
            .WithSummary("Erstattungen online bezahlter Belege eines Standorts, neueste zuerst (höchstens 100), mit Beleg, Stornobeleg und Stand.");

        return routes;
    }
}
