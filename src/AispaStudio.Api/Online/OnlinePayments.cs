namespace AispaStudio.Api.Online;

/// <summary>Einstellungen für den Gutscheinverkauf (Abschnitt <c>OnlineVouchers</c>).</summary>
public sealed class OnlineVoucherOptions
{
    public const string Section = "OnlineVouchers";

    /// <summary>
    /// Leer oder „Stripe“: Stripe Checkout über das Zahlungskonto des Spas (ADR 0054). „Test“: zusätzlich die Testzahlung für Spas
    /// ohne Konto. „None“: keine Online-Zahlung.
    /// </summary>
    public string? PaymentProvider { get; set; }

    public StripeOptions Stripe { get; set; } = new();

    public decimal MinAmount { get; set; } = 10m;
    public decimal MaxAmount { get; set; } = 500m;
    public decimal[] SuggestedAmounts { get; set; } = [25m, 50m, 100m, 150m];
}

/// <summary>
/// Was der Zahlungsanbieter für eine Zahlung braucht. <c>TenantId</c>: das Spa, auf dessen Zahlungskonto gezahlt wird (ADR 0054).
/// <c>OrderId</c>: Gutscheinbestellung oder Anzahlung. <c>ReturnUrl</c>: zurück zur Bestellung bzw. zum Termin im Kundenbereich.
/// <c>Attempt</c>: wievielter Anlauf; gehört zum Idempotenzschlüssel beim Anbieter.
/// </summary>
public sealed record PaymentStart(Guid TenantId, Guid OrderId, decimal Amount, string Currency, string Description, string BuyerEmail, string ReturnUrl,
    int Attempt = 1);

/// <summary>Begonnene Zahlung: Kennung beim Anbieter und die Seite, auf der bezahlt wird.</summary>
public sealed record PaymentSession(string Reference, string PaymentUrl);

public enum PaymentResult
{
    Paid,

    /// <summary>Abgebrochen, abgelaufen oder abgelehnt: es wurde nichts abgebucht.</summary>
    Failed,
}

/// <summary>Karte für eine spätere Ausfallgebühr hinterlegen (ohne Abbuchung). <c>ReturnUrl</c>: zurück zum Termin im Kundenbereich.</summary>
public sealed record CardSetupStart(Guid TenantId, Guid GuaranteeId, int Attempt, string Currency, string Description, string Email, string ReturnUrl);

/// <summary>Hinterlegte Karte mit der Ausfallgebühr belasten (vom Händler ausgelöst, ohne die Kundin oder den Kunden).</summary>
public sealed record SavedCardCharge(Guid TenantId, Guid GuaranteeId, string CardReference, string? ProviderCustomer, decimal Amount, string Currency, string Description);

/// <summary>Ergebnis einer Belastung: Kennung beim Anbieter oder der Grund der Ablehnung (z. B. Karte gesperrt).</summary>
public sealed record CardChargeResult(bool Succeeded, string? Reference, string? Error);

/// <summary>Hinterlegte Karte beim Anbieter löschen. <c>ProviderCustomer</c>: Kunde beim Anbieter, der mitsamt Karte gelöscht wird.</summary>
public sealed record SavedCardRemoval(Guid TenantId, Guid GuaranteeId, string? CardReference, string? ProviderCustomer);

/// <summary>Online-Zahlung ganz zurückerstatten. <c>PaymentReference</c>: was der Anbieter beim Bezahlen geliefert hat.</summary>
public sealed record PaymentRefund(Guid TenantId, Guid RefundId, string PaymentReference, decimal Amount, string Currency);

/// <summary>Ergebnis einer Erstattung: Kennung beim Anbieter oder der Grund der Ablehnung.</summary>
public sealed record RefundResult(bool Succeeded, string? Reference, string? Error);

/// <summary>Geprüfte Meldung des Anbieters zu einer Zahlung.</summary>
public sealed record PaymentNotification(Guid OrderId, string Reference, PaymentResult Result);

/// <summary>Ergebnis der Prüfung eines Webhooks: ungültig (Signatur, Format), ohne Belang für uns, oder eine Meldung.</summary>
public abstract record WebhookOutcome
{
    public sealed record Invalid(string Reason) : WebhookOutcome;

    public sealed record Ignored : WebhookOutcome;

    public sealed record Notified(PaymentNotification Notification) : WebhookOutcome;

    /// <summary>Karte hinterlegt: Sitzung, Kennung der Zahlungsart und Kunde beim Anbieter.</summary>
    public sealed record CardSaved(Guid GuaranteeId, string SessionReference, string? CardReference, string? ProviderCustomer) : WebhookOutcome;

    /// <summary>Hinterlegen abgebrochen oder abgelaufen.</summary>
    public sealed record CardSetupFailed(Guid GuaranteeId, string SessionReference) : WebhookOutcome;

    /// <summary>Stripe Connect: ein verbundenes Konto hat sich geändert (z. B. Einrichtung abgeschlossen).</summary>
    public sealed record AccountUpdated(string AccountId) : WebhookOutcome;
}

/// <summary>
/// Zahlungsanbieter für Online-Zahlungen (z. B. Stripe, Adyen, Mollie). Der Anbieter meldet die erfolgreiche Zahlung über seinen
/// Webhook; der ruft <see cref="VoucherShop.ConfirmPaymentAsync"/> auf. Austauschbar, damit kein Spa an einen Anbieter gebunden ist.
/// </summary>
public interface IOnlinePaymentProvider
{
    /// <summary>Kurzname, wird bei der Bestellung gespeichert und steht als Zahlungsreferenz auf dem Beleg.</summary>
    string Name { get; }

    /// <summary>Testbetrieb: die Zahlung wird im Kundenbereich nur simuliert.</summary>
    bool IsTest { get; }

    Task<PaymentSession> StartAsync(PaymentStart payment, CancellationToken ct);

    /// <summary>Seite zum Hinterlegen einer Karte für eine spätere Ausfallgebühr (mit starker Kundenauthentifizierung).</summary>
    Task<PaymentSession> StartCardSetupAsync(CardSetupStart setup, CancellationToken ct);

    /// <summary>Hinterlegte Karte belasten. Eine Ablehnung ist ein Ergebnis, keine Ausnahme; ausgelöst wird nur vom Team.</summary>
    Task<CardChargeResult> ChargeSavedCardAsync(SavedCardCharge charge, CancellationToken ct);

    /// <summary>Löscht die hinterlegte Karte beim Anbieter. Ist sie dort schon weg, gilt das als gelöscht; andere Fehler werfen.</summary>
    Task RemoveSavedCardAsync(SavedCardRemoval removal, CancellationToken ct);

    /// <summary>Erstattet eine Zahlung; nur nach dem Storno des Belegs an der Kasse. Eine Ablehnung ist ein Ergebnis.</summary>
    Task<RefundResult> RefundAsync(PaymentRefund refund, CancellationToken ct);

    /// <summary>Prüft einen eingehenden Webhook (Signatur) und liest die Meldung heraus.</summary>
    WebhookOutcome ReadWebhook(ReadOnlySpan<byte> body, IHeaderDictionary headers);
}

/// <summary>Testzahlung ohne Geldfluss: „bezahlt“ wird auf der Bestellseite per Knopf. Nur für Entwicklung und Vorführung.</summary>
internal sealed class TestPaymentProvider : IOnlinePaymentProvider
{
    public string Name => "Test";

    public bool IsTest => true;

    public Task<PaymentSession> StartAsync(PaymentStart payment, CancellationToken ct) =>
        Task.FromResult(new PaymentSession(payment.Attempt == 1 ? $"test-{payment.OrderId:N}" : $"test-{payment.OrderId:N}-{payment.Attempt}", payment.ReturnUrl));

    public Task<PaymentSession> StartCardSetupAsync(CardSetupStart setup, CancellationToken ct) =>
        Task.FromResult(new PaymentSession($"test-card-{setup.GuaranteeId:N}-{setup.Attempt}", setup.ReturnUrl));

    public Task<CardChargeResult> ChargeSavedCardAsync(SavedCardCharge charge, CancellationToken ct) =>
        Task.FromResult(new CardChargeResult(true, $"test-charge-{charge.GuaranteeId:N}", null));

    public Task<RefundResult> RefundAsync(PaymentRefund refund, CancellationToken ct) =>
        Task.FromResult(new RefundResult(true, $"test-refund-{refund.RefundId:N}", null));

    public Task RemoveSavedCardAsync(SavedCardRemoval removal, CancellationToken ct) => Task.CompletedTask;

    /// <summary>Die Testzahlung hat keinen Webhook; bezahlt wird über die Bestellseite.</summary>
    public WebhookOutcome ReadWebhook(ReadOnlySpan<byte> body, IHeaderDictionary headers) => new WebhookOutcome.Invalid("Kein Webhook im Testbetrieb.");
}
