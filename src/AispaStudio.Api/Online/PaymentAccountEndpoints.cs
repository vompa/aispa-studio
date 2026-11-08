using AispaStudio.Api.Access;
using AispaStudio.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;

namespace AispaStudio.Api.Online;

/// <summary>Zahlungskonto des Spas einrichten (ADR 0054); nur wer das Unternehmen verwalten darf.</summary>
internal static class PaymentAccountEndpoints
{
    public static IEndpointRouteBuilder MapPaymentAccountEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/tenant/payments").WithTags("Unternehmen");

        group.MapGet("/", async (PaymentAccounts payments, CancellationToken ct) => TypedResults.Ok(await payments.GetAsync(ct)))
            .RequirePermission(AccessRight.ManageOrganization)
            .WithName("GetPaymentAccount")
            .WithSummary("Zahlungskonto für Online-Zahlungen: Art, Stand und Hinweise. Schlüssel kommen nie zurück.");

        group.MapPut("/", async Task<Results<Ok<PaymentAccountDto>, ValidationProblem>> (
                PaymentAccountInput input, PaymentAccounts payments, CancellationToken ct) =>
            {
                var (account, errors) = await payments.UpdateAsync(input, ct);
                return account is null ? errors.Problem() : TypedResults.Ok(account);
            })
            .RequirePermission(AccessRight.ManageOrganization)
            .WithName("UpdatePaymentAccount")
            .WithSummary("Art des Zahlungskontos wählen und eigene Stripe-Schlüssel hinterlegen; leere Schlüssel bleiben, wie sie sind.");

        group.MapPost("/connect", async Task<Results<Accepted<PaymentAccountDto>, ProblemHttpResult>> (PaymentAccounts payments, CancellationToken ct) =>
            {
                var (account, error) = await payments.StartConnectAsync(ct);
                return account is null
                    ? TypedResults.Problem(error, statusCode: StatusCodes.Status409Conflict)
                    : TypedResults.Accepted("/api/tenant/payments", account);
            })
            .RequirePermission(AccessRight.ManageOrganization)
            .WithName("StartStripeConnect")
            .WithSummary("Mit Stripe verbinden: legt im Hintergrund das Konto und einen Einrichtungslink an. Danach den Stand abfragen.");

        group.MapPost("/check", async (PaymentAccounts payments, CancellationToken ct) =>
                TypedResults.Accepted("/api/tenant/payments", await payments.RefreshAsync(ct)))
            .RequirePermission(AccessRight.ManageOrganization)
            .WithName("CheckPaymentAccount")
            .WithSummary("Stand bei Stripe im Hintergrund neu abfragen, z. B. nach der Rückkehr von der Einrichtungsseite.");

        return routes;
    }
}
