using System.Security.Claims;
using AispaStudio.Api.Access;
using AispaStudio.Api.Data;
using AispaStudio.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Pos.Terminals;

/// <summary>
/// Kartenterminals: anlegen, Zahlung starten, Ergebnis abfragen, abbrechen oder stornieren. Das Terminal arbeitet über die Queue;
/// die Oberfläche fragt den Stand ab, bis die Zahlung genehmigt oder abgelehnt ist, und kassiert dann mit der Zahlung.
/// </summary>
internal static class TerminalEndpoints
{
    public static IEndpointRouteBuilder MapTerminalEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/pos").WithTags("Kartenterminals").RequirePermission(AccessRight.Book);

        group.MapGet("/terminals", async (Guid locationId, AispaDbContext db, CancellationToken ct) =>
                (await db.PaymentTerminals.AsNoTracking().Where(t => t.LocationId == locationId && t.IsActive).OrderBy(t => t.Name).ToListAsync(ct))
                .Select(ToDto).ToList())
            .WithName("ListPaymentTerminals");

        group.MapPost("/terminals", async Task<Results<Created<PaymentTerminalDto>, ValidationProblem>> (
                PaymentTerminalInput input, AispaDbContext db, CancellationToken ct) =>
            {
                var errors = new Dictionary<string, string[]>();
                if (string.IsNullOrWhiteSpace(input.Name) || input.Name.Trim().Length > 100)
                {
                    errors[nameof(input.Name)] = ["Name mit 1 bis 100 Zeichen."];
                }

                if (input.Provider == TerminalProvider.Local && string.IsNullOrWhiteSpace(input.Address))
                {
                    errors[nameof(input.Address)] = ["Adresse im Netz, z. B. „192.168.1.50:20007“."];
                }

                if (!Enum.IsDefined(input.Protocol))
                {
                    errors[nameof(input.Protocol)] = ["ZVT oder OPI."];
                }

                if (!await db.Locations.AnyAsync(l => l.Id == input.LocationId, ct))
                {
                    errors[nameof(input.LocationId)] = ["Standort nicht gefunden."];
                }

                if (errors.Count > 0)
                {
                    return TypedResults.ValidationProblem(errors);
                }

                var terminal = new PaymentTerminal
                {
                    LocationId = input.LocationId,
                    Name = input.Name.Trim(),
                    Provider = input.Provider,
                    Address = string.IsNullOrWhiteSpace(input.Address) ? null : input.Address.Trim(),
                    Protocol = input.Protocol,
                };
                db.PaymentTerminals.Add(terminal);
                await db.SaveChangesAsync(ct);
                return TypedResults.Created($"/api/pos/terminals/{terminal.Id}", ToDto(terminal));
            })
            .WithName("CreatePaymentTerminal")
            .RequirePermission(AccessRight.ManageLocation);

        group.MapDelete("/terminals/{terminalId:guid}", async Task<Results<NoContent, NotFound>> (Guid terminalId, AispaDbContext db, CancellationToken ct) =>
            {
                var terminal = await db.PaymentTerminals.FirstOrDefaultAsync(t => t.Id == terminalId && t.IsActive, ct);
                if (terminal is null)
                {
                    return TypedResults.NotFound();
                }

                terminal.IsActive = false;
                await db.SaveChangesAsync(ct);
                return TypedResults.NoContent();
            })
            .WithName("DeactivatePaymentTerminal")
            .RequirePermission(AccessRight.ManageLocation);

        group.MapPost("/bills/{billId:guid}/card-payments", async Task<Results<Ok<CardPaymentDto>, ValidationProblem, ProblemHttpResult>> (
                Guid billId, CardPaymentRequest request, ClaimsPrincipal user, CardTerminalService terminals, CancellationToken ct) =>
                PosEndpoints.Respond(await terminals.StartAsync(billId, request, user.FindFirstValue(ClaimTypes.NameIdentifier), ct)))
            .WithName("StartCardPayment")
            .WithSummary("Schickt einen Betrag an das Terminal. Danach den Stand abfragen; genehmigt wird mit der Zahlung kassiert.");

        group.MapGet("/card-payments/{cardPaymentId:guid}", async Task<Results<Ok<CardPaymentDto>, NotFound>> (
                Guid cardPaymentId, AispaDbContext db, CancellationToken ct) =>
                await db.CardPayments.AsNoTracking().FirstOrDefaultAsync(c => c.Id == cardPaymentId, ct) is { } payment
                    ? TypedResults.Ok(CardTerminalService.ToDto(payment))
                    : TypedResults.NotFound())
            .WithName("GetCardPayment");

        group.MapPost("/card-payments/{cardPaymentId:guid}/cancel", async Task<Results<Ok<CardPaymentDto>, ValidationProblem, ProblemHttpResult>> (
                Guid cardPaymentId, CardTerminalService terminals, CancellationToken ct) =>
                PosEndpoints.Respond(await terminals.CancelAsync(cardPaymentId, ct)))
            .WithName("CancelCardPayment")
            .WithSummary("Bricht eine laufende Zahlung ab; eine genehmigte, noch nicht kassierte wird am Terminal storniert.");

        group.MapGet("/terminals/{terminalId:guid}/jobs", async (Guid terminalId, CardTerminalService terminals, CancellationToken ct) =>
                await terminals.JobsAsync(terminalId, ct))
            .WithName("ListCardTerminalJobs")
            .WithSummary("Für die Windows-App am Empfang: Zahlungen und Stornos, die das Gerät im Netz ausführen soll.");

        group.MapPost("/card-payments/{cardPaymentId:guid}/result", async Task<Results<Ok<CardPaymentDto>, ValidationProblem, ProblemHttpResult>> (
                Guid cardPaymentId, CardTerminalResult result, CardTerminalService terminals, CancellationToken ct) =>
                PosEndpoints.Respond(await terminals.ReportAsync(cardPaymentId, result, ct)))
            .WithName("ReportCardTerminalResult")
            .WithSummary("Für die Windows-App am Empfang: Ergebnis vom Gerät melden.");

        return routes;
    }

    private static PaymentTerminalDto ToDto(PaymentTerminal t) => new(t.Id, t.LocationId, t.Name, t.Provider, t.Address, t.IsActive, t.Protocol);
}
