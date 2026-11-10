using System.Security.Claims;
using AispaStudio.Api.Access;
using AispaStudio.Api.Data;
using AispaStudio.Api.Documents;
using AispaStudio.Api.Staff;
using AispaStudio.Api.Tenancy;
using AispaStudio.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Wolverine;

namespace AispaStudio.Api.Stock;

/// <summary>
/// Bestellung als PDF und per Mail an den Lieferanten (PDF-Ausgaben F, ADR 0092). Recht wie die Bestellungen (<c>ManageLocation</c>); den Standort der Bestellung prüft der Filter über die Route,
/// eine fremde Bestellung ist „nicht gefunden“. Das Senden braucht wie „Bestellen und senden“ die Vollmacht „Bestellungen auslösen“ (<see cref="StaffPowerCheck"/>) und ändert den Stand der
/// Bestellung nicht („bestellt“ bleibt ein eigener Schritt); nach dem Versand steht der Vermerk in
/// <see cref="SentDocuments"/> (Art purchase-order) und an der Bestellung (<see cref="PurchaseOrderDto.SentMails"/>).
/// </summary>
internal static class PurchaseOrderDocumentEndpoints
{
    public static IEndpointRouteBuilder MapPurchaseOrderDocumentEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/purchase-orders/{purchaseOrderId:guid}").WithTags("Lager").RequirePermission(AccessRight.ManageLocation);

        group.MapGet("/pdf", async Task<Results<FileContentHttpResult, NotFound>> (
                Guid purchaseOrderId, ClaimsPrincipal user, AispaDbContext db, PurchaseOrders orders, DocumentHeaders headers, CancellationToken ct) =>
            {
                if (await LoadAsync(purchaseOrderId, db, orders, ct) is not { } data)
                {
                    return TypedResults.NotFound();
                }

                return DocumentFiles.Pdf(PurchaseOrderDocument.Build(data, await headers.ForAsync(data.Location.Id, user, ct)), PurchaseOrderDocument.FileName(data.Order));
            })
            .WithName("PurchaseOrderPdf")
            .WithSummary("Bestellung als PDF (A4): Lieferant, Lieferadresse, Positionen mit Menge, Einheit, Einzelpreis und Summe, Gesamtsumme netto, erwartete Lieferung (aus der Lieferzeit des Lieferanten) und Hinweis.");

        group.MapGet("/send", async Task<Results<Ok<DocumentMailDraft>, NotFound>> (
                Guid purchaseOrderId, ClaimsPrincipal user, AispaDbContext db, PurchaseOrders orders, DocumentHeaders headers, CancellationToken ct) =>
            {
                if (await LoadAsync(purchaseOrderId, db, orders, ct) is not { } data)
                {
                    return TypedResults.NotFound();
                }

                return TypedResults.Ok(PurchaseOrderDocument.Draft(data, await headers.ForAsync(data.Location.Id, user, ct)));
            })
            .WithName("PurchaseOrderMailDraft")
            .WithSummary("Vorbelegung für „Per Mail senden“: Adresse des Lieferanten (leer, wenn keine eingetragen ist), Betreff „Bestellung … vom …“ und Text.");

        group.MapPost("/send", async Task<Results<Accepted, NotFound, ValidationProblem, ProblemHttpResult>> (
                Guid purchaseOrderId, SendDocumentRequest request, ClaimsPrincipal user, AispaDbContext db, PurchaseOrders orders, DocumentHeaders headers, SentDocuments sent,
                TenantContext tenants, IMessageBus bus, AccessService access, TimeProvider time, CancellationToken ct) =>
            {
                // Eine Bestellung an den Lieferanten zu schicken heißt, sie auszulösen: dieselbe Vollmacht wie bei „Bestellen und senden“.
                if (await StaffPowerCheck.DeniedAsync(db, access, StaffPowerKind.TriggerOrders, time, ct) is { } denied)
                {
                    return TypedResults.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Keine Berechtigung.", detail: denied);
                }

                if (DocumentMails.Validate(request) is { } errors)
                {
                    return TypedResults.ValidationProblem(errors);
                }

                if (await LoadAsync(purchaseOrderId, db, orders, ct) is not { } data)
                {
                    return TypedResults.NotFound();
                }

                var header = await headers.ForAsync(data.Location.Id, user, ct);
                var (subject, text) = DocumentMails.Resolve(PurchaseOrderDocument.Draft(data, header), request);
                var pdf = PurchaseOrderDocument.Build(data, header);
                await bus.PublishAsync(DocumentMails.Message(tenants.TenantId!.Value, request.To, subject, text, PurchaseOrderDocument.FileName(data.Order), pdf));
                await sent.RecordAsync(SentDocument.PurchaseOrderKind, purchaseOrderId, request.To, subject, user, ct);
                return TypedResults.Accepted($"/api/stock/orders/{purchaseOrderId}");
            })
            .WithName("SendPurchaseOrderPdf")
            .WithSummary("Bestellung als PDF per Mail an den Lieferanten oder eine eingetippte Adresse (über die Queue), mit Vermerk an der Bestellung; der Stand der Bestellung bleibt, wie er ist.");

        return routes;
    }

    /// <summary>Die Bestellung mit Standort und Lieferant aus dem Stamm (über den Namen); null, wenn es sie nicht gibt.</summary>
    private static async Task<PurchaseOrderDocument.Data?> LoadAsync(Guid purchaseOrderId, AispaDbContext db, PurchaseOrders orders, CancellationToken ct)
    {
        if (await orders.GetAsync(purchaseOrderId, ct) is not { } order)
        {
            return null;
        }

        var location = await db.Locations.AsNoTracking().FirstAsync(l => l.Id == order.LocationId, ct);
        return new PurchaseOrderDocument.Data(order, location, await orders.SupplierOfAsync(order.Supplier, ct));
    }
}
