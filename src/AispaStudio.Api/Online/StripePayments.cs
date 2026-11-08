using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AispaStudio.Api.Tenancy;
using Microsoft.Extensions.Options;

namespace AispaStudio.Api.Online;

/// <summary>
/// Stripe auf diesem Server (Abschnitt <c>OnlineVouchers:Stripe</c>, ADR 0054). Mit <see cref="Connect"/> ist der Schlüssel der einer
/// Stripe-Connect-Plattform und jedes Spa verbindet sein eigenes Konto; ohne gilt er als Konto des Servers (Einzelbetrieb), solange
/// ein Spa keine eigenen Schlüssel einträgt. Schlüssel kommen aus dem Geheimnisspeicher. Als Webhook wird
/// <c>/api/hooks/payments/stripe</c> mit den Ereignissen <c>checkout.session.*</c> (bei Connect zusätzlich als Connect-Webhook mit
/// <c>account.updated</c>) eingetragen.
/// </summary>
public sealed class StripeOptions
{
    /// <summary>Geheimer API-Schlüssel des Servers bzw. der Plattform (<c>sk_live_…</c> oder <c>sk_test_…</c>).</summary>
    public string? SecretKey { get; set; }

    /// <summary>Signaturgeheimnis des Webhooks (<c>whsec_…</c>).</summary>
    public string? WebhookSecret { get; set; }

    /// <summary>Der Schlüssel gehört einer Stripe-Connect-Plattform; Spas verbinden ihr eigenes Konto.</summary>
    public bool Connect { get; set; }

    /// <summary>Signaturgeheimnis des Connect-Webhooks (Ereignisse der verbundenen Konten).</summary>
    public string? ConnectWebhookSecret { get; set; }

    public Uri ApiBase { get; set; } = new("https://api.stripe.com/");

    /// <summary>So lange bleibt die Zahlungsseite offen; Stripe erlaubt 30 Minuten bis 24 Stunden.</summary>
    public int SessionMinutes { get; set; } = 60;

    public bool HasPlatformKey => !string.IsNullOrWhiteSpace(SecretKey);
}

/// <summary>
/// Zahlung über Stripe Checkout: Stripe zeigt die Zahlungsarten an, die im Stripe-Konto eingeschaltet sind (Karte, eps,
/// Twint, Klarna, Apple Pay …), und meldet das Ergebnis über den Webhook. Jeder Aufruf geht an das Konto des Spas
/// (<see cref="PaymentAccounts"/>): bei Connect mit dem Schlüssel der Plattform und <c>Stripe-Account</c>, sonst mit dem Schlüssel
/// des Spas.
/// </summary>
internal sealed class StripePaymentProvider(IHttpClientFactory http, IOptions<OnlineVoucherOptions> options, TimeProvider time, IServiceScopeFactory scopes)
    : IOnlinePaymentProvider
{
    public const string HttpClientName = "stripe";
    public const string ProviderName = "Stripe";

    /// <summary>Wie alt eine Webhook-Signatur höchstens sein darf (Schutz gegen wiedereingespielte Meldungen).</summary>
    public static readonly TimeSpan SignatureTolerance = TimeSpan.FromMinutes(5);

    private StripeOptions Options => options.Value.Stripe;

    public string Name => ProviderName;

    public bool IsTest => false;

    public async Task<PaymentSession> StartAsync(PaymentStart payment, CancellationToken ct)
    {
        var o = Options;
        var credentials = await CredentialsAsync(payment.TenantId, ct);
        var form = new Dictionary<string, string>
        {
            ["mode"] = "payment",
            ["success_url"] = payment.ReturnUrl,
            ["cancel_url"] = payment.ReturnUrl,
            ["customer_email"] = payment.BuyerEmail,
            ["client_reference_id"] = payment.OrderId.ToString(),
            ["metadata[orderId]"] = payment.OrderId.ToString(),
            ["expires_at"] = time.GetUtcNow().AddMinutes(Math.Clamp(o.SessionMinutes, 30, 24 * 60)).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture),
            ["line_items[0][quantity]"] = "1",
            ["line_items[0][price_data][currency]"] = payment.Currency.ToLowerInvariant(),
            ["line_items[0][price_data][unit_amount]"] = MinorUnits(payment.Amount).ToString(CultureInfo.InvariantCulture),
            ["line_items[0][price_data][product_data][name]"] = payment.Description,
        };
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(o.ApiBase, "v1/checkout/sessions"))
        {
            Content = new FormUrlEncodedContent(form),
        };
        Authorize(request, credentials);
        // Wiederholt die Queue den Aufruf, legt Stripe keine zweite Sitzung an.
        request.Headers.Add("Idempotency-Key", payment.Attempt == 1 ? $"aispa-voucher-{payment.OrderId:N}" : $"aispa-payment-{payment.OrderId:N}-{payment.Attempt}");
        using var response = await http.CreateClient(HttpClientName).SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        return new PaymentSession(json.RootElement.GetProperty("id").GetString()!, json.RootElement.GetProperty("url").GetString()!);
    }

    /// <summary>
    /// Checkout im Modus „setup“: Stripe legt eine Kundin bzw. einen Kunden an, prüft die Karte mit starker Kundenauthentifizierung
    /// und merkt sie für spätere, vom Händler ausgelöste Zahlungen vor (<c>usage = off_session</c>). Abgebucht wird nichts.
    /// </summary>
    public async Task<PaymentSession> StartCardSetupAsync(CardSetupStart setup, CancellationToken ct)
    {
        var o = Options;
        var credentials = await CredentialsAsync(setup.TenantId, ct);
        using var customer = await PostAsync(credentials, "v1/customers", new Dictionary<string, string>
        {
            ["email"] = setup.Email,
            ["metadata[guaranteeId]"] = setup.GuaranteeId.ToString(),
        }, $"aispa-guarantee-customer-{setup.GuaranteeId:N}", ct);
        var form = new Dictionary<string, string>
        {
            ["mode"] = "setup",
            ["currency"] = setup.Currency.ToLowerInvariant(),
            ["customer"] = customer.RootElement.GetProperty("id").GetString()!,
            ["payment_method_types[0]"] = "card",
            ["success_url"] = setup.ReturnUrl,
            ["cancel_url"] = setup.ReturnUrl,
            ["client_reference_id"] = setup.GuaranteeId.ToString(),
            ["metadata[guaranteeId]"] = setup.GuaranteeId.ToString(),
            ["setup_intent_data[description]"] = setup.Description,
            ["setup_intent_data[metadata][guaranteeId]"] = setup.GuaranteeId.ToString(),
            ["expires_at"] = time.GetUtcNow().AddMinutes(Math.Clamp(o.SessionMinutes, 30, 24 * 60)).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture),
        };
        using var json = await PostAsync(credentials, "v1/checkout/sessions", form, $"aispa-guarantee-{setup.GuaranteeId:N}-{setup.Attempt}", ct);
        return new PaymentSession(json.RootElement.GetProperty("id").GetString()!, json.RootElement.GetProperty("url").GetString()!);
    }

    /// <summary>
    /// Holt die Zahlungsart aus dem SetupIntent und belastet sie ohne die Kundin oder den Kunden (<c>off_session</c>). Lehnt die
    /// Bank ab oder verlangt sie eine Bestätigung, meldet Stripe 402; das ist ein Ergebnis, kein Fehler.
    /// </summary>
    public async Task<CardChargeResult> ChargeSavedCardAsync(SavedCardCharge charge, CancellationToken ct)
    {
        var o = Options;
        var credentials = await CredentialsAsync(charge.TenantId, ct);
        string paymentMethod;
        using (var request = new HttpRequestMessage(HttpMethod.Get, new Uri(o.ApiBase, $"v1/setup_intents/{Uri.EscapeDataString(charge.CardReference)}")))
        {
            Authorize(request, credentials);
            using var response = await http.CreateClient(HttpClientName).SendAsync(request, ct);
            response.EnsureSuccessStatusCode();
            using var intent = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
            paymentMethod = intent.RootElement.GetProperty("payment_method").GetString()
                ?? throw new InvalidOperationException("Beim Anbieter ist keine Karte hinterlegt.");
        }

        var form = new Dictionary<string, string>
        {
            ["amount"] = MinorUnits(charge.Amount).ToString(CultureInfo.InvariantCulture),
            ["currency"] = charge.Currency.ToLowerInvariant(),
            ["payment_method"] = paymentMethod,
            ["off_session"] = "true",
            ["confirm"] = "true",
            ["description"] = charge.Description,
            ["metadata[guaranteeId]"] = charge.GuaranteeId.ToString(),
        };
        if (charge.ProviderCustomer is { } customer)
        {
            form["customer"] = customer;
        }

        using var post = new HttpRequestMessage(HttpMethod.Post, new Uri(o.ApiBase, "v1/payment_intents")) { Content = new FormUrlEncodedContent(form) };
        Authorize(post, credentials);
        // Ein zweiter Versuch derselben Gebühr belastet nicht doppelt.
        post.Headers.Add("Idempotency-Key", $"aispa-noshow-{charge.GuaranteeId:N}");
        using var result = await http.CreateClient(HttpClientName).SendAsync(post, ct);
        using var json = await JsonDocument.ParseAsync(await result.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        if (result.StatusCode == HttpStatusCode.PaymentRequired)
        {
            var error = json.RootElement.GetProperty("error");
            return new CardChargeResult(false, null, error.TryGetProperty("message", out var m) ? m.GetString() : "Die Bank hat abgelehnt.");
        }

        result.EnsureSuccessStatusCode();
        var status = json.RootElement.GetProperty("status").GetString();
        return status == "succeeded"
            ? new CardChargeResult(true, json.RootElement.GetProperty("id").GetString(), null)
            : new CardChargeResult(false, null, $"Zahlung nicht abgeschlossen ({status}).");
    }

    /// <summary>
    /// Erstattet über <c>v1/refunds</c>. Gutscheine merken sich die Checkout-Sitzung (<c>cs_…</c>); deren Zahlung wird erst
    /// nachgeschlagen. Ausfallgebühren merken sich die Zahlung (<c>pi_…</c>) direkt. „pending“ gilt als erstattet: Stripe hat
    /// die Erstattung angenommen und schickt sie an die Bank.
    /// </summary>
    public async Task<RefundResult> RefundAsync(PaymentRefund refund, CancellationToken ct)
    {
        var o = Options;
        var credentials = await CredentialsAsync(refund.TenantId, ct);
        var paymentIntent = refund.PaymentReference;
        if (paymentIntent.StartsWith("cs_", StringComparison.Ordinal))
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(o.ApiBase, $"v1/checkout/sessions/{Uri.EscapeDataString(paymentIntent)}"));
            Authorize(request, credentials);
            using var response = await http.CreateClient(HttpClientName).SendAsync(request, ct);
            response.EnsureSuccessStatusCode();
            using var session = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
            paymentIntent = session.RootElement.TryGetProperty("payment_intent", out var pi) && pi.ValueKind == JsonValueKind.String
                ? pi.GetString()!
                : throw new InvalidOperationException("Zur Checkout-Sitzung gibt es beim Anbieter keine Zahlung.");
        }

        var form = new Dictionary<string, string>
        {
            ["payment_intent"] = paymentIntent,
            ["amount"] = MinorUnits(refund.Amount).ToString(CultureInfo.InvariantCulture),
            ["reason"] = "requested_by_customer",
            ["metadata[refundId]"] = refund.RefundId.ToString(),
        };
        using var post = new HttpRequestMessage(HttpMethod.Post, new Uri(o.ApiBase, "v1/refunds")) { Content = new FormUrlEncodedContent(form) };
        Authorize(post, credentials);
        // Ein zweiter Versuch derselben Erstattung zahlt nicht doppelt zurück.
        post.Headers.Add("Idempotency-Key", $"aispa-refund-{refund.RefundId:N}");
        using var result = await http.CreateClient(HttpClientName).SendAsync(post, ct);
        using var json = await JsonDocument.ParseAsync(await result.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        if (result.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.PaymentRequired)
        {
            var error = json.RootElement.GetProperty("error");
            return new RefundResult(false, null, error.TryGetProperty("message", out var m) ? m.GetString() : "Der Anbieter hat die Erstattung abgelehnt.");
        }

        result.EnsureSuccessStatusCode();
        var status = json.RootElement.GetProperty("status").GetString();
        return status is "succeeded" or "pending"
            ? new RefundResult(true, json.RootElement.GetProperty("id").GetString(), null)
            : new RefundResult(false, null, $"Erstattung nicht abgeschlossen ({status}).");
    }

    /// <summary>
    /// Löscht den Kunden, den das Hinterlegen bei Stripe angelegt hat (<c>DELETE v1/customers</c>); damit sind Karte und Mail-Adresse
    /// dort weg, Zahlungen und Erstattungen bleiben. Ohne Kunden wird die Zahlungsart des SetupIntents abgehängt. 404 heißt: schon
    /// gelöscht; 400 beim Abhängen: nicht mehr angehängt.
    /// </summary>
    public async Task RemoveSavedCardAsync(SavedCardRemoval removal, CancellationToken ct)
    {
        var o = Options;
        var credentials = await CredentialsAsync(removal.TenantId, ct);
        if (removal.ProviderCustomer is { } customer)
        {
            using var delete = new HttpRequestMessage(HttpMethod.Delete, new Uri(o.ApiBase, $"v1/customers/{Uri.EscapeDataString(customer)}"));
            Authorize(delete, credentials);
            using var deleted = await http.CreateClient(HttpClientName).SendAsync(delete, ct);
            if (deleted.StatusCode != HttpStatusCode.NotFound)
            {
                deleted.EnsureSuccessStatusCode();
            }

            return;
        }

        if (removal.CardReference is not { } setupIntent)
        {
            return;
        }

        string? paymentMethod;
        using (var request = new HttpRequestMessage(HttpMethod.Get, new Uri(o.ApiBase, $"v1/setup_intents/{Uri.EscapeDataString(setupIntent)}")))
        {
            Authorize(request, credentials);
            using var response = await http.CreateClient(HttpClientName).SendAsync(request, ct);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return;
            }

            response.EnsureSuccessStatusCode();
            using var intent = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
            paymentMethod = intent.RootElement.TryGetProperty("payment_method", out var pm) && pm.ValueKind == JsonValueKind.String ? pm.GetString() : null;
        }

        if (paymentMethod is null)
        {
            return;
        }

        using var detach = new HttpRequestMessage(HttpMethod.Post, new Uri(o.ApiBase, $"v1/payment_methods/{Uri.EscapeDataString(paymentMethod)}/detach"));
        Authorize(detach, credentials);
        using var detached = await http.CreateClient(HttpClientName).SendAsync(detach, ct);
        if (detached.StatusCode is not (HttpStatusCode.BadRequest or HttpStatusCode.NotFound))
        {
            detached.EnsureSuccessStatusCode();
        }
    }

    /// <summary>Webhook des Servers: Konto des Servers bzw. der Plattform und, bei Connect, Ereignisse der verbundenen Konten.</summary>
    public WebhookOutcome ReadWebhook(ReadOnlySpan<byte> body, IHeaderDictionary headers)
    {
        var signature = headers["Stripe-Signature"].FirstOrDefault();
        var now = time.GetUtcNow();
        if (!StripeWebhook.IsValid(Options.WebhookSecret ?? "", body, signature, now, SignatureTolerance)
            && !StripeWebhook.IsValid(Options.ConnectWebhookSecret ?? "", body, signature, now, SignatureTolerance))
        {
            return new WebhookOutcome.Invalid("Signatur ungültig oder zu alt.");
        }

        return StripeWebhook.Read(body);
    }

    /// <summary>Zugang zum Konto des Spas; ohne bereites Konto wird nichts an Stripe geschickt.</summary>
    private async Task<StripeCredentials> CredentialsAsync(Guid tenantId, CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Use(tenantId);
        return await scope.ServiceProvider.GetRequiredService<PaymentAccounts>().StripeCredentialsAsync(ct)
            ?? throw new InvalidOperationException("Für dieses Spa ist kein Stripe-Konto bereit.");
    }

    private static void Authorize(HttpRequestMessage request, StripeCredentials credentials)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", credentials.SecretKey);
        if (credentials.ConnectedAccount is { } account)
        {
            // Direkte Zahlung auf dem verbundenen Konto: das Geld gehört dem Spa, Stripe rechnet die Gebühren mit ihm ab.
            request.Headers.Add("Stripe-Account", account);
        }
    }

    private async Task<JsonDocument> PostAsync(StripeCredentials credentials, string path, Dictionary<string, string> form, string idempotencyKey, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(Options.ApiBase, path)) { Content = new FormUrlEncodedContent(form) };
        Authorize(request, credentials);
        request.Headers.Add("Idempotency-Key", idempotencyKey);
        using var response = await http.CreateClient(HttpClientName).SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        return await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
    }

    /// <summary>Betrag in Cent bzw. Rappen, wie Stripe ihn für EUR und CHF erwartet.</summary>
    public static long MinorUnits(decimal amount) => (long)decimal.Round(amount * 100m, 0, MidpointRounding.AwayFromZero);
}

/// <summary>Webhooks von Stripe: Signatur prüfen und Ereignisse zu Checkout-Sitzungen lesen.</summary>
public static class StripeWebhook
{
    /// <summary>
    /// Prüft <c>Stripe-Signature: t=…,v1=…</c>: v1 = Hex(HMAC-SHA256(Geheimnis, „t.Rohdaten“)), höchstens
    /// <paramref name="tolerance"/> alt. Mehrere v1 (Schlüsselwechsel) sind erlaubt.
    /// </summary>
    public static bool IsValid(string secret, ReadOnlySpan<byte> body, string? header, DateTimeOffset now, TimeSpan tolerance)
    {
        if (string.IsNullOrEmpty(secret) || string.IsNullOrEmpty(header))
        {
            return false;
        }

        var parts = header.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Split('=', 2)).Where(p => p.Length == 2).ToList();
        if (parts.FirstOrDefault(p => p[0] == "t") is not [_, var t] || !long.TryParse(t, CultureInfo.InvariantCulture, out var seconds)
            || (now - DateTimeOffset.FromUnixTimeSeconds(seconds)).Duration() > tolerance)
        {
            return false;
        }

        var signed = new byte[t.Length + 1 + body.Length];
        Encoding.ASCII.GetBytes(t, signed);
        signed[t.Length] = (byte)'.';
        body.CopyTo(signed.AsSpan(t.Length + 1));
        var expected = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), signed);
        foreach (var part in parts.Where(p => p[0] == "v1"))
        {
            try
            {
                if (CryptographicOperations.FixedTimeEquals(expected, Convert.FromHexString(part[1])))
                {
                    return true;
                }
            }
            catch (FormatException)
            {
                // Nächste Signatur versuchen.
            }
        }

        return false;
    }

    /// <summary>Signatur für einen Webhook berechnen (Tests und lokale Vorführung).</summary>
    public static string Sign(string secret, string body, DateTimeOffset at)
    {
        var t = at.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        var mac = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes($"{t}.{body}"));
        return $"t={t},v1={Convert.ToHexStringLower(mac)}";
    }

    /// <summary>
    /// Bezahlt: <c>checkout.session.completed</c> mit <c>payment_status = paid</c> oder <c>checkout.session.async_payment_succeeded</c>
    /// (z. B. Überweisung). Nicht bezahlt: <c>checkout.session.expired</c> und <c>checkout.session.async_payment_failed</c>.
    /// </summary>
    public static WebhookOutcome Read(ReadOnlySpan<byte> body)
    {
        try
        {
            var reader = new Utf8JsonReader(body);
            using var json = JsonDocument.ParseValue(ref reader);
            var root = json.RootElement;
            var type = root.GetProperty("type").GetString();
            var session = root.GetProperty("data").GetProperty("object");
            if (type == "account.updated")
            {
                return session.GetProperty("id").GetString() is { } account ? new WebhookOutcome.AccountUpdated(account) : new WebhookOutcome.Ignored();
            }

            if (session.TryGetProperty("mode", out var mode) && mode.GetString() == "setup")
            {
                return ReadSetup(type, session);
            }

            var result = type switch
            {
                "checkout.session.completed" when session.TryGetProperty("payment_status", out var s) && s.GetString() == "paid" => PaymentResult.Paid,
                "checkout.session.async_payment_succeeded" => PaymentResult.Paid,
                "checkout.session.expired" or "checkout.session.async_payment_failed" => PaymentResult.Failed,
                _ => (PaymentResult?)null,
            };
            if (result is null)
            {
                return new WebhookOutcome.Ignored();
            }

            var reference = session.GetProperty("id").GetString();
            var orderText = session.TryGetProperty("client_reference_id", out var r) ? r.GetString() : null;
            return reference is not null && Guid.TryParse(orderText, out var orderId)
                ? new WebhookOutcome.Notified(new PaymentNotification(orderId, reference, result.Value))
                : new WebhookOutcome.Ignored();
        }
        catch (Exception e) when (e is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            return new WebhookOutcome.Invalid("Unbekanntes Format.");
        }
    }

    /// <summary>Hinterlegte Karte: <c>checkout.session.completed</c> mit <c>setup_intent</c>; abgelaufen: <c>checkout.session.expired</c>.</summary>
    private static WebhookOutcome ReadSetup(string? type, JsonElement session)
    {
        var reference = session.GetProperty("id").GetString();
        var idText = session.TryGetProperty("client_reference_id", out var r) ? r.GetString() : null;
        if (reference is null || !Guid.TryParse(idText, out var guaranteeId))
        {
            return new WebhookOutcome.Ignored();
        }

        return type switch
        {
            "checkout.session.completed" => new WebhookOutcome.CardSaved(guaranteeId, reference, Text(session, "setup_intent"), Text(session, "customer")),
            "checkout.session.expired" => new WebhookOutcome.CardSetupFailed(guaranteeId, reference),
            _ => new WebhookOutcome.Ignored(),
        };
    }

    private static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
