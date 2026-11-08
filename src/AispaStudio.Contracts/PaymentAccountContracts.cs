namespace AispaStudio.Contracts;

// Zahlungskonto des Spas für Online-Zahlungen (ADR 0054): Gutscheine, hinterlegte Karten und Ausfallgebühren landen direkt beim
// Spa, nie auf einem Konto von AISPA.

/// <summary>Wie das Spa Online-Zahlungen annimmt.</summary>
public enum PaymentAccountMode
{
    /// <summary>Nichts eingerichtet. Läuft der Server mit eigenem Stripe-Konto (Einzelbetrieb), gilt dieses.</summary>
    None,

    /// <summary>Stripe Connect: das Spa verbindet sein Stripe-Konto mit wenigen Klicks; Schlüssel braucht es keine.</summary>
    Connect,

    /// <summary>Eigene Stripe-Schlüssel des Spas (geheimer Schlüssel und Webhook-Geheimnis).</summary>
    OwnKeys,
}

/// <summary>
/// Stand des Zahlungskontos. Schlüssel kommen nie zurück, nur ihr Ende (<paramref name="SecretKeyHint"/>).
/// </summary>
/// <param name="Provider">Zahlungsanbieter des Servers: „Stripe“, „Test“ (Zahlung wird nur simuliert) oder leer (keine Online-Zahlung).</param>
/// <param name="ConnectAvailable">Der Server ist als Stripe-Connect-Plattform eingerichtet.</param>
/// <param name="ServerAccount">Der Server hat ein eigenes Stripe-Konto, das gilt, solange das Spa nichts einrichtet (Einzelbetrieb).</param>
/// <param name="Ready">Online-Zahlungen laufen über dieses Konto.</param>
/// <param name="Status">Stand in einem Satz, für die Anzeige.</param>
/// <param name="ConnectOnboardingUrl">Seite von Stripe zum Einrichten; nur, solange der Link gilt.</param>
/// <param name="WebhookUrl">Bei eigenen Schlüsseln: diese Adresse im Stripe-Dashboard als Webhook eintragen.</param>
public sealed record PaymentAccountDto(
    string? Provider,
    bool ConnectAvailable,
    bool ServerAccount,
    PaymentAccountMode Mode,
    bool Ready,
    string Status,
    string? AccountName,
    string? ConnectAccountId,
    bool ConnectChargesEnabled,
    bool ConnectDetailsSubmitted,
    bool ConnectPreparing,
    string? ConnectOnboardingUrl,
    string? SecretKeyHint,
    bool LiveMode,
    bool HasWebhookSecret,
    string? WebhookUrl,
    string? Error);

/// <summary>Art des Kontos wählen; Schlüssel leer lassen, um die gespeicherten zu behalten.</summary>
public sealed record PaymentAccountInput(PaymentAccountMode Mode, string? SecretKey = null, string? WebhookSecret = null);
