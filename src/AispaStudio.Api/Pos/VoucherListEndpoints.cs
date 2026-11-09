using System.Security.Claims;
using AispaStudio.Api.Access;
using AispaStudio.Api.Data;
using AispaStudio.Api.Documents;
using AispaStudio.Api.Online;
using AispaStudio.Api.Scheduling;
using AispaStudio.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Pos;

/// <summary>
/// Gutscheinliste eines Standorts als PDF (PDF-Ausgaben, Zusatz; ADR 0091). Die Summe der offenen Restwerte ist die Verbindlichkeit für die Buchhaltung: Recht ist deshalb die Kassenaufsicht oder
/// „Berichte mit Beträgen“ (dort stehen die Gutscheinsummen), nicht die Rezeption. Den Standort prüft der Mandantenfilter, ein fremder ist „nicht gefunden“. Ein Gutschein gehört zum Standort der
/// Kasse, an der er verkauft wurde (auch online gekaufte Gutscheine laufen über die Kasse des Standorts).
/// </summary>
internal static class VoucherListEndpoints
{
    public static IEndpointRouteBuilder MapVoucherListEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGroup("/api/pos/locations/{locationId:guid}").WithTags("Kassa").RequirePermission(AccessRight.SuperviseCash)
            .MapGet("/vouchers/pdf", async Task<Results<FileContentHttpResult, NotFound, ValidationProblem>> (
                Guid locationId, string? @from, string? until, string? status, string? expiring, string? notSent,
                ClaimsPrincipal user, AispaDbContext db, OnlineDbContext online, DocumentHeaders headers, CancellationToken ct) =>
            {
                var (filter, errors) = VoucherListDocument.ParseFilter(@from, until, status, expiring, notSent);
                if (filter is null)
                {
                    return TypedResults.ValidationProblem(errors!);
                }

                if (await db.Locations.AsNoTracking().FirstOrDefaultAsync(l => l.Id == locationId, ct) is not { } location)
                {
                    return TypedResults.NotFound();
                }

                var header = await headers.ForAsync(locationId, user, ct);
                var today = LocalTime.LocalDate(header.CreatedAt, header.Zone);
                var entries = VoucherListDocument.Select(await LoadAsync(locationId, db, online, ct), filter, today, header.Zone);
                var data = new VoucherListDocument.Data(location.Name, location.Currency, filter, entries);
                return DocumentFiles.Pdf(VoucherListDocument.Build(data, header), VoucherListDocument.FileName(location.Name, today));
            })
            .WithName("VoucherListPdf")
            .AlsoAllow(AccessRight.ViewAmountReports, insteadOf: AccessRight.SuperviseCash)
            .WithSummary(
                "Gutscheinliste als PDF (A4 quer): alle Gutscheine des Standorts mit Code, Art, Werten, Status, Käufer, Empfänger, Kanal und Versandvermerk und der Summe der offenen Restwerte. "
                + "Filter: from und until (Ausstellung, jjjj-mm-tt), status (active, redeemed, expired, voided), expiring und notSent (true oder false).");

        return routes;
    }

    /// <summary>Alle Gutscheine der Kassen des Standorts mit Behandlung, Käufer, Online-Bestellung (Empfänger) und den Versandvermerken aller Wege.</summary>
    private static async Task<List<VoucherListDocument.Entry>> LoadAsync(Guid locationId, AispaDbContext db, OnlineDbContext online, CancellationToken ct)
    {
        var sold = await (
            from voucher in db.Vouchers.AsNoTracking()
            join receipt in db.Receipts.AsNoTracking() on voucher.SoldOnReceiptId equals receipt.Id
            join register in db.CashRegisters.AsNoTracking() on receipt.RegisterId equals register.Id
            where register.LocationId == locationId
            select new { Voucher = voucher, ReceiptCustomerId = receipt.CustomerId }).ToListAsync(ct);
        var voucherIds = sold.ConvertAll(s => s.Voucher.Id);
        var treatmentIds = sold.Select(s => s.Voucher.TreatmentId).OfType<Guid>().Distinct().ToList();
        var customerIds = sold.Select(s => s.Voucher.CustomerId ?? s.ReceiptCustomerId).OfType<Guid>().Distinct().ToList();

        var treatments = await db.Treatments.AsNoTracking().Where(t => treatmentIds.Contains(t.Id)).ToDictionaryAsync(t => t.Id, t => t.Name, ct);
        var buyers = await db.Customers.AsNoTracking().Where(c => customerIds.Contains(c.Id)).Select(c => new { c.Id, c.FirstName, c.LastName }).ToListAsync(ct);
        var orders = (await online.VoucherOrders.AsNoTracking().Where(o => o.VoucherId != null && voucherIds.Contains(o.VoucherId.Value))
            .Select(o => new { VoucherId = o.VoucherId!.Value, o.RecipientName }).ToListAsync(ct)).ToDictionary(o => o.VoucherId, o => o.RecipientName);
        var sent = await SentDocuments.ListAllChannelsAsync(db, SentDocument.VoucherKind, voucherIds, ct);

        return [.. sold.Select(s =>
        {
            var buyerId = s.Voucher.CustomerId ?? s.ReceiptCustomerId;
            var buyer = buyers.FirstOrDefault(b => b.Id == buyerId) is { } found ? $"{found.FirstName} {found.LastName}".Trim() : null;
            return new VoucherListDocument.Entry(
                s.Voucher,
                s.Voucher.TreatmentId is { } treatmentId && treatments.TryGetValue(treatmentId, out var name) ? name : null,
                buyer,
                orders.GetValueOrDefault(s.Voucher.Id) is { Length: > 0 } recipient ? recipient : null,
                orders.ContainsKey(s.Voucher.Id),
                sent.GetValueOrDefault(s.Voucher.Id) ?? []);
        })];
    }
}
