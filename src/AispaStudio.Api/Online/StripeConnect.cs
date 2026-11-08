using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using AispaStudio.Api.Data;
using AispaStudio.Api.Tenancy;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AispaStudio.Api.Online;

/// <summary>Stand eines Stripe-Kontos, wie Stripe ihn meldet.</summary>
public sealed record StripeAccountState(string Id, string? Name, bool ChargesEnabled, bool DetailsSubmitted);

/// <summary>
/// Aufrufe an Stripe für das Zahlungskonto des Spas (ADR 0054): Connect-Konto anlegen, Einrichtungslink holen, Stand abfragen.
/// Nur aus der Queue, nie im HTTP-Aufruf.
/// </summary>
internal sealed class StripeAccountClient(IHttpClientFactory http, IOptions<OnlineVoucherOptions> options)
{
    private StripeOptions Options => options.Value.Stripe;

    /// <summary>
    /// Standard-Konto anlegen: das Spa hat ein eigenes Stripe-Dashboard, Stripe rechnet die Gebühren mit dem Spa ab und zahlt direkt
    /// an seine Bank aus. Der Idempotenzschlüssel verhindert ein zweites Konto, wenn die Queue wiederholt.
    /// </summary>
    public async Task<string> CreateConnectAccountAsync(Guid tenantId, string country, string? email, string name, CancellationToken ct)
    {
        var form = new Dictionary<string, string>
        {
            ["type"] = "standard",
            ["country"] = country,
            ["business_profile[name]"] = name,
            ["metadata[tenantId]"] = tenantId.ToString(),
        };
        if (email is not null)
        {
            form["email"] = email;
        }

        using var json = await SendAsync(HttpMethod.Post, "v1/accounts", Options.SecretKey!, form, $"aispa-connect-{tenantId:N}", ct);
        return json.RootElement.GetProperty("id").GetString()!;
    }

    /// <summary>Einrichtungslink von Stripe; gilt nur wenige Minuten und nur einmal.</summary>
    public async Task<(string Url, DateTimeOffset ExpiresAt)> CreateOnboardingLinkAsync(string accountId, string returnUrl, CancellationToken ct)
    {
        using var json = await SendAsync(HttpMethod.Post, "v1/account_links", Options.SecretKey!, new Dictionary<string, string>
        {
            ["account"] = accountId,
            ["refresh_url"] = returnUrl,
            ["return_url"] = returnUrl,
            ["type"] = "account_onboarding",
        }, null, ct);
        return (json.RootElement.GetProperty("url").GetString()!,
            DateTimeOffset.FromUnixTimeSeconds(json.RootElement.GetProperty("expires_at").GetInt64()));
    }

    /// <summary>Verbundenes Konto über den Schlüssel der Plattform abfragen.</summary>
    public async Task<StripeAccountState> GetConnectAccountAsync(string accountId, CancellationToken ct)
    {
        using var json = await SendAsync(HttpMethod.Get, $"v1/accounts/{Uri.EscapeDataString(accountId)}", Options.SecretKey!, null, null, ct);
        return Read(json.RootElement);
    }

    /// <summary>Eigene Schlüssel prüfen: <c>GET v1/account</c> liefert das Konto, dem der Schlüssel gehört.</summary>
    public async Task<StripeAccountState> GetOwnAccountAsync(string secretKey, CancellationToken ct)
    {
        using var json = await SendAsync(HttpMethod.Get, "v1/account", secretKey, null, null, ct);
        return Read(json.RootElement);
    }

    private static StripeAccountState Read(JsonElement account)
    {
        var name = account.TryGetProperty("business_profile", out var profile) && profile.ValueKind == JsonValueKind.Object
            && profile.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String
                ? n.GetString()
                : account.TryGetProperty("settings", out var settings) && settings.ValueKind == JsonValueKind.Object
                  && settings.TryGetProperty("dashboard", out var dashboard) && dashboard.TryGetProperty("display_name", out var d)
                  && d.ValueKind == JsonValueKind.String
                    ? d.GetString()
                    : null;
        return new StripeAccountState(account.GetProperty("id").GetString()!, name, Flag(account, "charges_enabled"), Flag(account, "details_submitted"));
    }

    private static bool Flag(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True;

    private async Task<JsonDocument> SendAsync(HttpMethod method, string path, string secretKey, Dictionary<string, string>? form,
        string? idempotencyKey, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, new Uri(Options.ApiBase, path));
        if (form is not null)
        {
            request.Content = new FormUrlEncodedContent(form);
        }

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", secretKey);
        if (idempotencyKey is not null)
        {
            request.Headers.Add("Idempotency-Key", idempotencyKey);
        }

        using var response = await http.CreateClient(StripePaymentProvider.HttpClientName).SendAsync(request, ct);
        var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        if (!response.IsSuccessStatusCode)
        {
            using (json)
            {
                var message = json.RootElement.TryGetProperty("error", out var error) && error.TryGetProperty("message", out var m)
                    ? m.GetString()
                    : null;
                throw new HttpRequestException(response.StatusCode == HttpStatusCode.Unauthorized
                    ? "Stripe kennt diesen Schlüssel nicht."
                    : $"Stripe meldet: {message ?? response.StatusCode.ToString()}", null, response.StatusCode);
            }
        }

        return json;
    }
}

/// <summary>Legt das Connect-Konto an (einmal) und holt einen frischen Einrichtungslink; Fehler stehen danach beim Konto.</summary>
public static partial class PrepareStripeConnectHandler
{
    public static async Task Handle(PrepareStripeConnect message, IServiceScopeFactory scopes, CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var services = scope.ServiceProvider;
        services.GetRequiredService<TenantContext>().Use(message.TenantId);
        var db = services.GetRequiredService<AispaDbContext>();
        var account = await db.PaymentAccounts.FirstOrDefaultAsync(ct);
        var tenant = await db.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Id == message.TenantId, ct);
        if (account is not { Mode: PaymentAccountMode.Connect } || tenant is null)
        {
            return;
        }

        // Ohne Stripe auf dem Server (PaymentProvider=None) gibt es nichts einzurichten.
        if (services.GetService<StripeAccountClient>() is not { } stripe)
        {
            return;
        }

        var returnUrl = $"{services.GetRequiredService<IOptions<OnlineBookingOptions>>().Value.PublicUrl.TrimEnd('/')}/anmelden/?zahlungen=1";
        try
        {
            if (account.ConnectAccountId is null)
            {
                account.ConnectAccountId = await stripe.CreateConnectAccountAsync(message.TenantId, tenant.Country, tenant.Email,
                    tenant.LegalName ?? tenant.Name, ct);
                // Sofort merken: scheitert der Link, legt der nächste Versuch kein zweites Konto an.
                await db.SaveChangesAsync(ct);
            }

            var state = await stripe.GetConnectAccountAsync(account.ConnectAccountId, ct);
            Apply(account, state);
            if (!state.DetailsSubmitted || !state.ChargesEnabled)
            {
                (account.ConnectOnboardingUrl, var expires) = await stripe.CreateOnboardingLinkAsync(account.ConnectAccountId, returnUrl, ct);
                account.ConnectOnboardingExpiresAt = expires;
            }

            account.Error = null;
        }
        catch (Exception e) when (e is HttpRequestException or JsonException or KeyNotFoundException or InvalidOperationException)
        {
            account.Error = StartCardGuaranteeHandler.Short($"Stripe Connect: {e.Message}");
            Failed(services.GetRequiredService<ILogger<PaymentAccount>>(), message.TenantId, e.Message);
        }

        account.ConnectPreparing = false;
        account.CheckedAt = services.GetRequiredService<TimeProvider>().GetUtcNow();
        await db.SaveChangesAsync(ct);
    }

    internal static void Apply(PaymentAccount account, StripeAccountState state)
    {
        account.ConnectChargesEnabled = state.ChargesEnabled;
        account.ConnectDetailsSubmitted = state.DetailsSubmitted;
        account.AccountName = state.Name ?? account.AccountName;
        if (state.ChargesEnabled)
        {
            account.ConnectOnboardingUrl = null;
            account.ConnectOnboardingExpiresAt = null;
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Stripe Connect für Spa {TenantId} nicht vorbereitet: {Error}")]
    private static partial void Failed(ILogger logger, Guid tenantId, string error);
}

/// <summary>
/// Fragt den Stand bei Stripe ab: beim Connect-Konto, ob es Zahlungen annimmt; bei eigenen Schlüsseln, ob Stripe sie kennt. Kommt
/// über einen Webhook (<c>account.updated</c>) nur die Kontonummer, wird das Spa darüber gesucht.
/// </summary>
public static partial class CheckPaymentAccountHandler
{
    public static async Task Handle(CheckPaymentAccount message, IServiceScopeFactory scopes, CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var db = services.GetRequiredService<AispaDbContext>();
        var tenantId = message.TenantId;
        if (tenantId is null && message.ConnectAccountId is { } connected)
        {
            tenantId = await TenantModel.TenantOfAsync(db.PaymentAccounts.Where(a => a.ConnectAccountId == connected), ct);
        }

        if (tenantId is not { } id)
        {
            return;
        }

        services.GetRequiredService<TenantContext>().Use(id);
        var account = await db.PaymentAccounts.FirstOrDefaultAsync(ct);
        if (account is null)
        {
            return;
        }

        if (services.GetService<StripeAccountClient>() is not { } stripe)
        {
            return;
        }

        try
        {
            switch (account)
            {
                case { Mode: PaymentAccountMode.Connect, ConnectAccountId: { } accountId }:
                    PrepareStripeConnectHandler.Apply(account, await stripe.GetConnectAccountAsync(accountId, ct));
                    break;
                case { Mode: PaymentAccountMode.OwnKeys }:
                    var key = services.GetRequiredService<PaymentSecrets>().Unprotect(id, PaymentSecrets.SecretKey, account.ProtectedSecretKey)
                        ?? throw new InvalidOperationException("Der geheime Schlüssel lässt sich auf diesem Server nicht lesen. Bitte neu eintragen.");
                    var own = await stripe.GetOwnAccountAsync(key, ct);
                    account.AccountName = own.Name;
                    break;
                default:
                    return;
            }

            account.Error = null;
        }
        catch (HttpRequestException e) when (e.StatusCode is null or >= HttpStatusCode.InternalServerError)
        {
            // Stripe nicht erreichbar: nichts ändern, die Queue versucht es erneut.
            throw;
        }
        catch (Exception e) when (e is HttpRequestException or JsonException or KeyNotFoundException or InvalidOperationException)
        {
            account.Error = StartCardGuaranteeHandler.Short(e.Message);
            Failed(services.GetRequiredService<ILogger<PaymentAccount>>(), id, e.Message);
        }

        account.CheckedAt = services.GetRequiredService<TimeProvider>().GetUtcNow();
        await db.SaveChangesAsync(ct);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Zahlungskonto von Spa {TenantId} nicht bereit: {Error}")]
    private static partial void Failed(ILogger logger, Guid tenantId, string error);
}
