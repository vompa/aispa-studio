using AispaStudio.Api.Data;
using AispaStudio.Api.Scheduling;
using AispaStudio.Api.Tenancy;
using AispaStudio.Contracts;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Wolverine;

namespace AispaStudio.Api.Online;

/// <summary>
/// Zahlungskonto des Spas (ADR 0054): Stripe Connect oder eigene Stripe-Schlüssel. Eine Zeile je Spa. Schlüssel liegen nur
/// verschlüsselt in der Datenbank (Data Protection, gebunden an Spa und Zweck).
/// </summary>
public sealed class PaymentAccount
{
    public Guid Id { get; init; } = Guid.CreateVersion7();

    public PaymentAccountMode Mode { get; set; }

    /// <summary>Verbundenes Stripe-Konto (<c>acct_…</c>); bleibt beim Wechsel auf eigene Schlüssel erhalten.</summary>
    public string? ConnectAccountId { get; set; }

    /// <summary>Stripe nimmt für das verbundene Konto Zahlungen an (<c>charges_enabled</c>).</summary>
    public bool ConnectChargesEnabled { get; set; }

    /// <summary>Das Spa hat die Angaben bei Stripe vollständig gemacht (<c>details_submitted</c>).</summary>
    public bool ConnectDetailsSubmitted { get; set; }

    /// <summary>Die Queue legt gerade Konto oder Einrichtungslink an.</summary>
    public bool ConnectPreparing { get; set; }

    public string? ConnectOnboardingUrl { get; set; }

    public DateTimeOffset? ConnectOnboardingExpiresAt { get; set; }

    public string? ProtectedSecretKey { get; set; }

    public string? ProtectedWebhookSecret { get; set; }

    /// <summary>Anfang und Ende des geheimen Schlüssels (z. B. „sk_live_…4f2a“), damit man erkennt, welcher hinterlegt ist.</summary>
    public string? SecretKeyHint { get; set; }

    /// <summary>Name des Kontos laut Stripe, nach der letzten Prüfung.</summary>
    public string? AccountName { get; set; }

    public DateTimeOffset? CheckedAt { get; set; }

    /// <summary>Warum die letzte Prüfung oder Einrichtung gescheitert ist; dann laufen keine Zahlungen über das Konto.</summary>
    public string? Error { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}

internal static class PaymentAccountModel
{
    public static void Configure(ModelBuilder builder) =>
        builder.Entity<PaymentAccount>(e =>
        {
            e.Property(a => a.Id).ValueGeneratedNever();
            e.Property(a => a.Mode).HasConversion<string>().HasMaxLength(16);
            e.Property(a => a.ConnectAccountId).HasMaxLength(64);
            e.Property(a => a.ConnectOnboardingUrl).HasMaxLength(500);
            e.Property(a => a.SecretKeyHint).HasMaxLength(40);
            e.Property(a => a.AccountName).HasMaxLength(200);
            e.Property(a => a.Error).HasMaxLength(500);
            e.HasIndex(a => a.ConnectAccountId);
        });
}

/// <summary>Verschlüsselt die Stripe-Schlüssel eines Spas; ein Schlüssel eines Spas lässt sich nicht als der eines anderen lesen.</summary>
internal sealed class PaymentSecrets(IDataProtectionProvider protection)
{
    public const string SecretKey = "secret-key";
    public const string WebhookSecret = "webhook-secret";

    private IDataProtector Protector(Guid tenantId, string purpose) =>
        protection.CreateProtector("AispaStudio.PaymentAccount.Secret", tenantId.ToString(), purpose);

    public string Protect(Guid tenantId, string purpose, string secret) => Protector(tenantId, purpose).Protect(secret);

    public string? Unprotect(Guid tenantId, string purpose, string? protectedSecret)
    {
        if (protectedSecret is null)
        {
            return null;
        }

        try
        {
            return Protector(tenantId, purpose).Unprotect(protectedSecret);
        }
        catch (global::System.Security.Cryptography.CryptographicException)
        {
            // Anderer Schlüsselring (z. B. auf einen anderen Server abgeglichen): wie nicht hinterlegt behandeln.
            return null;
        }
    }
}

/// <summary>Womit Stripe für ein Spa aufgerufen wird: geheimer Schlüssel und bei Connect das verbundene Konto.</summary>
public sealed record StripeCredentials(string SecretKey, string? ConnectedAccount);

/// <summary>Nachricht in der Queue: Connect-Konto anlegen (falls noch keines) und einen frischen Einrichtungslink holen.</summary>
public sealed record PrepareStripeConnect(Guid TenantId);

/// <summary>Nachricht in der Queue: Stand des Kontos bei Stripe abfragen (Connect-Konto oder eigene Schlüssel).</summary>
public sealed record CheckPaymentAccount(Guid? TenantId, string? ConnectAccountId = null);

/// <summary>
/// Zahlungskonto des angemeldeten bzw. gesetzten Spas: einrichten, anzeigen und für Zahlungen auflösen. Welcher Anbieter für
/// eine neue Zahlung gilt, entscheidet nur diese Klasse (<see cref="ActiveProviderAsync"/>).
/// </summary>
internal sealed class PaymentAccounts(
    AispaDbContext db,
    TenantContext tenants,
    PaymentSecrets secrets,
    IEnumerable<IOnlinePaymentProvider> providers,
    IOptions<OnlineVoucherOptions> voucherOptions,
    IOptions<OnlineBookingOptions> bookingOptions,
    IMessageBus bus,
    TimeProvider time)
{
    public const int MaxSecretLength = 300;

    private StripeOptions Stripe => voucherOptions.Value.Stripe;

    private IOnlinePaymentProvider? StripeProvider => providers.LastOrDefault(p => p.Name == StripePaymentProvider.ProviderName);

    private IOnlinePaymentProvider? TestProvider => providers.LastOrDefault(p => p.IsTest);

    private bool _loaded;
    private IOnlinePaymentProvider? _active;

    /// <summary>
    /// Anbieter für neue Zahlungen dieses Spas: Stripe, wenn ein Konto bereit ist; sonst die Testzahlung, wenn der Server sie
    /// anbietet; sonst keiner (dann gibt es online keine Gutscheine und keine Ausfallgebühr).
    /// </summary>
    public async Task<IOnlinePaymentProvider?> ActiveProviderAsync(CancellationToken ct)
    {
        if (!_loaded)
        {
            _active = StripeProvider is { } stripe && await StripeCredentialsAsync(ct) is not null ? stripe : TestProvider;
            _loaded = true;
        }

        return _active;
    }

    /// <summary>Stripe-Zugang des Spas oder <c>null</c>, wenn keiner bereit ist.</summary>
    public async Task<StripeCredentials?> StripeCredentialsAsync(CancellationToken ct)
    {
        if (tenants.TenantId is not { } tenantId)
        {
            return null;
        }

        var account = await db.PaymentAccounts.AsNoTracking().FirstOrDefaultAsync(ct);
        return Resolve(tenantId, account);
    }

    private StripeCredentials? Resolve(Guid tenantId, PaymentAccount? account) => account?.Mode switch
    {
        PaymentAccountMode.OwnKeys when account.Error is null
            && secrets.Unprotect(tenantId, PaymentSecrets.SecretKey, account.ProtectedSecretKey) is { } key
            && account.ProtectedWebhookSecret is not null => new StripeCredentials(key, null),
        PaymentAccountMode.Connect when Stripe.Connect && Stripe.HasPlatformKey && account.ConnectAccountId is { } connected
            && account.ConnectChargesEnabled => new StripeCredentials(Stripe.SecretKey!, connected),
        PaymentAccountMode.OwnKeys or PaymentAccountMode.Connect => null,
        _ => !Stripe.Connect && Stripe.HasPlatformKey ? new StripeCredentials(Stripe.SecretKey!, null) : null,
    };

    /// <summary>Webhook-Geheimnis der eigenen Schlüssel des Spas, für <c>/api/hooks/payments/stripe/{spa}</c>.</summary>
    public async Task<string?> OwnWebhookSecretAsync(CancellationToken ct)
    {
        if (tenants.TenantId is not { } tenantId)
        {
            return null;
        }

        var account = await db.PaymentAccounts.AsNoTracking().FirstOrDefaultAsync(a => a.Mode == PaymentAccountMode.OwnKeys, ct);
        return secrets.Unprotect(tenantId, PaymentSecrets.WebhookSecret, account?.ProtectedWebhookSecret);
    }

    public async Task<PaymentAccountDto> GetAsync(CancellationToken ct)
    {
        var account = await db.PaymentAccounts.AsNoTracking().FirstOrDefaultAsync(ct);
        return ToDto(account);
    }

    public async Task<(PaymentAccountDto? Account, Errors Errors)> UpdateAsync(PaymentAccountInput input, CancellationToken ct)
    {
        var errors = new Errors();
        var secretKey = Blank(input.SecretKey);
        var webhookSecret = Blank(input.WebhookSecret);
        errors.Check(Enum.IsDefined(input.Mode), nameof(input.Mode), "Unbekannte Art.");
        errors.Check(input.Mode == PaymentAccountMode.None || StripeProvider is not null, nameof(input.Mode),
            "Dieser Server bietet keine Online-Zahlung über Stripe an.");
        errors.Check(input.Mode != PaymentAccountMode.Connect || Stripe.Connect && Stripe.HasPlatformKey, nameof(input.Mode),
            "Dieser Server ist nicht für Stripe Connect eingerichtet. Bitte eigene Schlüssel verwenden.");
        errors.Check(secretKey is null || (secretKey.StartsWith("sk_", StringComparison.Ordinal) || secretKey.StartsWith("rk_", StringComparison.Ordinal))
            && secretKey.Length is >= 12 and <= MaxSecretLength && !secretKey.Any(char.IsWhiteSpace),
            nameof(input.SecretKey), "Der geheime Schlüssel beginnt mit „sk_live_“ oder „sk_test_“ (eingeschränkt: „rk_“).");
        errors.Check(webhookSecret is null || webhookSecret.StartsWith("whsec_", StringComparison.Ordinal) && webhookSecret.Length <= MaxSecretLength
            && !webhookSecret.Any(char.IsWhiteSpace), nameof(input.WebhookSecret), "Das Webhook-Geheimnis beginnt mit „whsec_“.");
        var account = await db.PaymentAccounts.FirstOrDefaultAsync(ct);
        if (input.Mode == PaymentAccountMode.OwnKeys)
        {
            errors.Check(secretKey is not null || account?.ProtectedSecretKey is not null, nameof(input.SecretKey), "Bitte den geheimen Schlüssel eintragen.");
            errors.Check(webhookSecret is not null || account?.ProtectedWebhookSecret is not null, nameof(input.WebhookSecret),
                "Bitte das Webhook-Geheimnis eintragen; ohne es erfährt AISPA nicht, dass bezahlt wurde.");
        }

        if (errors.Any || tenants.TenantId is not { } tenantId)
        {
            return (null, errors);
        }

        if (account is null)
        {
            account = new PaymentAccount();
            db.PaymentAccounts.Add(account);
        }

        var changed = account.Mode != input.Mode || secretKey is not null || webhookSecret is not null;
        account.Mode = input.Mode;
        if (secretKey is not null)
        {
            account.ProtectedSecretKey = secrets.Protect(tenantId, PaymentSecrets.SecretKey, secretKey);
            account.SecretKeyHint = Hint(secretKey);
        }

        if (webhookSecret is not null)
        {
            account.ProtectedWebhookSecret = secrets.Protect(tenantId, PaymentSecrets.WebhookSecret, webhookSecret);
        }

        if (changed)
        {
            account.Error = null;
            account.CheckedAt = null;
            if (input.Mode == PaymentAccountMode.OwnKeys)
            {
                account.AccountName = null;
            }
        }

        account.UpdatedAt = time.GetUtcNow();
        await db.SaveChangesAsync(ct);
        if (changed && input.Mode != PaymentAccountMode.None)
        {
            // Schlüssel bzw. Connect-Konto bei Stripe prüfen; das läuft über die Queue, nie im Aufruf.
            await bus.PublishAsync(new CheckPaymentAccount(tenantId));
        }

        return (ToDto(account), errors);
    }

    /// <summary>Stripe Connect beginnen oder fortsetzen: die Queue legt Konto und Einrichtungslink an.</summary>
    public async Task<(PaymentAccountDto? Account, string? Error)> StartConnectAsync(CancellationToken ct)
    {
        if (!Stripe.Connect || !Stripe.HasPlatformKey)
        {
            return (null, "Dieser Server ist nicht für Stripe Connect eingerichtet. Bitte eigene Schlüssel verwenden.");
        }

        if (tenants.TenantId is not { } tenantId)
        {
            return (null, "Kein Spa angemeldet.");
        }

        var account = await db.PaymentAccounts.FirstOrDefaultAsync(ct);
        if (account is null)
        {
            account = new PaymentAccount();
            db.PaymentAccounts.Add(account);
        }

        account.Mode = PaymentAccountMode.Connect;
        account.ConnectPreparing = true;
        account.ConnectOnboardingUrl = null;
        account.ConnectOnboardingExpiresAt = null;
        account.Error = null;
        account.UpdatedAt = time.GetUtcNow();
        await db.SaveChangesAsync(ct);
        await bus.PublishAsync(new PrepareStripeConnect(tenantId));
        return (ToDto(account), null);
    }

    /// <summary>Stand bei Stripe neu abfragen (z. B. nach der Rückkehr von der Einrichtungsseite).</summary>
    public async Task<PaymentAccountDto> RefreshAsync(CancellationToken ct)
    {
        var account = await db.PaymentAccounts.AsNoTracking().FirstOrDefaultAsync(ct);
        if (account is { Mode: not PaymentAccountMode.None } && tenants.TenantId is { } tenantId)
        {
            await bus.PublishAsync(new CheckPaymentAccount(tenantId));
        }

        return ToDto(account);
    }

    /// <summary>Adresse, die bei eigenen Schlüsseln im Stripe-Dashboard als Webhook eingetragen wird.</summary>
    public string WebhookUrl(Guid tenantId) => $"{bookingOptions.Value.PublicUrl.TrimEnd('/')}/api/hooks/payments/stripe/{tenantId}";

    private PaymentAccountDto ToDto(PaymentAccount? a)
    {
        var provider = StripeProvider?.Name ?? TestProvider?.Name;
        var mode = a?.Mode ?? PaymentAccountMode.None;
        var tenantId = tenants.TenantId ?? Guid.Empty;
        var ready = StripeProvider is not null && Resolve(tenantId, a) is not null;
        var serverAccount = StripeProvider is not null && !Stripe.Connect && Stripe.HasPlatformKey;
        var link = a?.ConnectOnboardingExpiresAt > time.GetUtcNow() ? a.ConnectOnboardingUrl : null;
        return new PaymentAccountDto(provider, StripeProvider is not null && Stripe.Connect && Stripe.HasPlatformKey, serverAccount, mode, ready,
            StatusText(a, mode, ready, serverAccount), a?.AccountName, a?.ConnectAccountId, a?.ConnectChargesEnabled ?? false,
            a?.ConnectDetailsSubmitted ?? false, a?.ConnectPreparing ?? false, link, a?.SecretKeyHint,
            a?.SecretKeyHint?.Contains("_live_", StringComparison.Ordinal) ?? false, a?.ProtectedWebhookSecret is not null,
            mode == PaymentAccountMode.OwnKeys ? WebhookUrl(tenantId) : null, a?.Error);
    }

    private string StatusText(PaymentAccount? a, PaymentAccountMode mode, bool ready, bool serverAccount)
    {
        if (StripeProvider is null)
        {
            return TestProvider is null
                ? "Dieser Server bietet keine Online-Zahlung an."
                : "Testbetrieb: Online-Zahlungen werden nur simuliert, es fließt kein Geld.";
        }

        if (a?.Error is { } error)
        {
            return $"Nicht bereit: {error}";
        }

        return mode switch
        {
            PaymentAccountMode.Connect when ready => "Verbunden. Online-Zahlungen gehen direkt auf das Stripe-Konto des Spas.",
            PaymentAccountMode.Connect when a?.ConnectPreparing == true => "Stripe wird vorbereitet …",
            PaymentAccountMode.Connect when a?.ConnectDetailsSubmitted == true => "Angaben bei Stripe gemacht; Stripe prüft sie noch.",
            PaymentAccountMode.Connect when a?.ConnectAccountId is not null => "Die Einrichtung bei Stripe ist noch nicht abgeschlossen.",
            PaymentAccountMode.Connect => "Noch nicht mit Stripe verbunden.",
            PaymentAccountMode.OwnKeys when ready && a?.CheckedAt is null => "Schlüssel gespeichert; die Prüfung bei Stripe läuft.",
            PaymentAccountMode.OwnKeys when ready => "Bereit. Online-Zahlungen laufen über die eigenen Schlüssel des Spas.",
            PaymentAccountMode.OwnKeys => "Schlüssel fehlen noch.",
            _ when serverAccount => "Online-Zahlungen laufen über das Stripe-Konto dieses Servers.",
            _ => TestProvider is null
                ? "Noch kein Zahlungskonto: online gibt es keine Gutscheine und keine Ausfallgebühr."
                : "Noch kein Zahlungskonto: Online-Zahlungen werden bis dahin nur simuliert.",
        };
    }

    /// <summary>„sk_live_…4f2a“: genug zum Wiedererkennen, nichts zum Verwenden.</summary>
    internal static string Hint(string key)
    {
        var prefix = key.StartsWith("sk_live_", StringComparison.Ordinal) || key.StartsWith("rk_live_", StringComparison.Ordinal) ? key[..8]
            : key.StartsWith("sk_test_", StringComparison.Ordinal) || key.StartsWith("rk_test_", StringComparison.Ordinal) ? key[..8]
            : key[..3];
        return $"{prefix}…{key[^4..]}";
    }

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
