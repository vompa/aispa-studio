using System.Security.Claims;
using AispaStudio.Api.Access;
using AispaStudio.Api.Data;
using AispaStudio.Api.Documents;
using AispaStudio.Api.Tenancy;
using AispaStudio.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Wolverine;

namespace AispaStudio.Api.Pos;

/// <summary>
/// Gutschein als PDF und per Mail (PDF-Ausgaben E, ADR 0092). Rechte wie die Kassa und der Beleg (buchen dürfen oder Belege ansehen); den Gutschein prüft der Mandantenfilter,
/// ein fremder ist „nicht gefunden“. Der Käufer ist der Gast auf dem Verkaufsbeleg; nach dem Versand steht der Vermerk in <see cref="SentDocuments"/> (Art voucher).
/// </summary>
internal static class VoucherDocumentEndpoints
{
    public static IEndpointRouteBuilder MapVoucherDocumentEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/pos/vouchers/{voucherId:guid}").WithTags("Kassa").RequirePermission(AccessRight.Book);

        group.MapGet("/pdf", async Task<Results<FileContentHttpResult, NotFound>> (
                Guid voucherId, ClaimsPrincipal user, AispaDbContext db, DocumentHeaders headers, CancellationToken ct) =>
            {
                if (await LoadAsync(voucherId, db, ct) is not { } loaded)
                {
                    return TypedResults.NotFound();
                }

                var pdf = VoucherDocument.Build(loaded.Data, await headers.ForAsync(loaded.LocationId, user, ct));
                return DocumentFiles.Pdf(pdf, VoucherDocument.FileName(loaded.Data.Voucher));
            })
            .WithName("VoucherPdf")
            .AlsoAllow(AccessRight.ViewReceipts, insteadOf: AccessRight.Book)
            .WithSummary("Gutschein als PDF zum Verschenken (A4): Spa-Name, Wert oder Behandlung, Code, QR-Code mit dem Code, „Gültig bis“ und „Einlösbar in“.");

        group.MapGet("/send", async Task<Results<Ok<DocumentMailDraft>, NotFound>> (
                Guid voucherId, ClaimsPrincipal user, AispaDbContext db, DocumentHeaders headers, CancellationToken ct) =>
                await PrepareAsync(voucherId, user, db, headers, ct) is { } prepared ? TypedResults.Ok(prepared.Draft) : TypedResults.NotFound())
            .WithName("VoucherMailDraft")
            .AlsoAllow(AccessRight.ViewReceipts, insteadOf: AccessRight.Book)
            .WithSummary("Vorbelegung für „Per Mail senden“: Adresse des Käufers aus der Kundenkarte (leer, wenn er unbekannt ist), Betreff und Text.");

        group.MapPost("/send", async Task<Results<Accepted, NotFound, ValidationProblem>> (
                Guid voucherId, SendDocumentRequest request, ClaimsPrincipal user, AispaDbContext db, DocumentHeaders headers, SentDocuments sent,
                TenantContext tenants, IMessageBus bus, CancellationToken ct) =>
            {
                if (DocumentMails.Validate(request) is { } errors)
                {
                    return TypedResults.ValidationProblem(errors);
                }

                if (await PrepareAsync(voucherId, user, db, headers, ct) is not { } prepared)
                {
                    return TypedResults.NotFound();
                }

                var (subject, text) = DocumentMails.Resolve(prepared.Draft, request);
                var pdf = VoucherDocument.Build(prepared.Data, prepared.Header);
                await bus.PublishAsync(DocumentMails.Message(tenants.TenantId!.Value, request.To, subject, text, VoucherDocument.FileName(prepared.Data.Voucher), pdf));
                await sent.RecordAsync(SentDocument.VoucherKind, voucherId, request.To, subject, user, ct);
                return TypedResults.Accepted($"/api/pos/vouchers/{voucherId}");
            })
            .WithName("SendVoucher")
            .AlsoAllow(AccessRight.ViewReceipts, insteadOf: AccessRight.Book)
            .WithSummary("Gutschein als PDF per Mail an den Käufer oder eine eingetippte Adresse (über die Queue), mit Vermerk; ohne Betreff und Text gilt die Vorbelegung.");

        // Vermerke für die Gutscheinliste: ausgedruckt (die App meldet es nach dem erfolgreichen Druck) und persönlich übergeben.
        MapDispatch(group, "/printed", "VoucherPrinted", SentDocument.PrintChannel, "Vermerkt, dass der Gutschein ausgedruckt wurde (die App meldet es nach dem Drucken); die Gutscheinliste zeigt „Gedruckt am …“.");
        MapDispatch(group, "/handed-over", "VoucherHandedOver", SentDocument.HandoverChannel, "Vermerkt, dass der Gutschein persönlich übergeben wurde; die Gutscheinliste zeigt „Übergeben am …“.");

        return routes;
    }

    private static void MapDispatch(RouteGroupBuilder group, string path, string name, string channel, string summary) =>
        group.MapPost(path, async Task<Results<NoContent, NotFound>> (Guid voucherId, ClaimsPrincipal user, AispaDbContext db, SentDocuments sent, CancellationToken ct) =>
            {
                if (!await db.Vouchers.AnyAsync(v => v.Id == voucherId, ct))
                {
                    return TypedResults.NotFound();
                }

                await sent.RecordAsync(SentDocument.VoucherKind, voucherId, "", "", user, ct, channel);
                return TypedResults.NoContent();
            })
            .WithName(name)
            .AlsoAllow(AccessRight.ViewReceipts, insteadOf: AccessRight.Book)
            .WithSummary(summary);

    private static async Task<PreparedVoucher?> PrepareAsync(Guid voucherId, ClaimsPrincipal user, AispaDbContext db, DocumentHeaders headers, CancellationToken ct)
    {
        if (await LoadAsync(voucherId, db, ct) is not { } loaded)
        {
            return null;
        }

        var header = await headers.ForAsync(loaded.LocationId, user, ct);
        return new PreparedVoucher(loaded.Data, header, VoucherDocument.Draft(loaded.Data, header.SpaName, loaded.BuyerEmail));
    }

    /// <summary>
    /// Der Gutschein mit Behandlung, den Standorten des Spas und der Adresse des Käufers (Gast auf dem Verkaufsbeleg, sonst der Gast des Pakets); Standort und Währung des Verkaufs
    /// bestimmen Kopf und Betrag. Null, wenn es den Gutschein nicht gibt.
    /// </summary>
    private static async Task<LoadedVoucher?> LoadAsync(Guid voucherId, AispaDbContext db, CancellationToken ct)
    {
        if (await db.Vouchers.AsNoTracking().FirstOrDefaultAsync(v => v.Id == voucherId, ct) is not { } voucher)
        {
            return null;
        }

        var locations = await db.Locations.AsNoTracking().OrderBy(l => l.Name).Select(l => new { l.Id, l.Name, l.Currency }).ToListAsync(ct);
        var sale = await db.Receipts.AsNoTracking().Where(r => r.Id == voucher.SoldOnReceiptId)
            .Join(db.CashRegisters, r => r.RegisterId, c => c.Id, (r, c) => new { r.CustomerId, c.LocationId }).FirstOrDefaultAsync(ct);
        var location = locations.FirstOrDefault(l => l.Id == sale?.LocationId) ?? locations[0];
        var treatment = voucher.TreatmentId is { } treatmentId ? await db.Treatments.AsNoTracking().Where(t => t.Id == treatmentId).Select(t => t.Name).FirstOrDefaultAsync(ct) : null;
        var buyerId = sale?.CustomerId ?? voucher.CustomerId;
        var email = buyerId is { } id ? await db.Customers.AsNoTracking().Where(c => c.Id == id).Select(c => c.Email).FirstOrDefaultAsync(ct) : null;
        return new LoadedVoucher(new VoucherDocument.Data(voucher, treatment, [.. locations.Select(l => l.Name)], location.Currency), location.Id, string.IsNullOrWhiteSpace(email) ? null : email);
    }

    private sealed record LoadedVoucher(VoucherDocument.Data Data, Guid LocationId, string? BuyerEmail);

    private sealed record PreparedVoucher(VoucherDocument.Data Data, DocumentHeader Header, DocumentMailDraft Draft);
}
