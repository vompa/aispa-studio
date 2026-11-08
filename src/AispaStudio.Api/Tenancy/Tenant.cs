using System.Security.Claims;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Tenancy;

/// <summary>Ein Spa-Unternehmen (Mandant) mit seinen Standorten, Mitarbeitenden, Kunden und Belegen. Daten verschiedener Mandanten sehen einander nie.</summary>
public sealed class Tenant
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public required string Name { get; set; }

    /// <summary>ISO-Ländercode des Unternehmenssitzes (AT, DE, CH); bestimmt später Kassenrecht und Arbeitszeitgrenzen.</summary>
    public required string Country { get; set; }

    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>Vom Betreiber gesperrt (z. B. Vertrag beendet): Lesen und Exportieren geht weiter, Ändern nicht (ADR 0039).</summary>
    public DateTimeOffset? LockedAt { get; set; }

    /// <summary>Grund der Sperre, wie ihn das Spa sieht.</summary>
    public string? LockReason { get; set; }

    /// <summary>Abo-Stufe (ADR 0015); setzt der Betreiber. Neue und bestehende Spas haben zunächst alles.</summary>
    public SubscriptionPlan Plan { get; set; } = SubscriptionPlan.SpaPlus;

    // Firmendaten (ADR 0052): gelten für alle Standorte ohne eigene Unternehmensangaben.
    public string? LegalName { get; set; }
    public string? Street { get; set; }
    public string? PostalCode { get; set; }
    public string? City { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Website { get; set; }
    public string? VatId { get; set; }
    public string? CompanyRegisterNumber { get; set; }
    public string? CompanyRegisterCourt { get; set; }
    public string? AccountHolder { get; set; }

    /// <summary>Ohne Leerzeichen, in Großbuchstaben.</summary>
    public string? Iban { get; set; }

    public string? Bic { get; set; }
    public string? BusinessPurpose { get; set; }
    public string? SupervisoryAuthority { get; set; }
    public string? ChamberAndRules { get; set; }

    /// <summary>Rabatt in Prozent, den jede Person geben darf; darüber nur die Kassenaufsicht.</summary>
    public decimal DiscountLimitPercent { get; set; } = 20;

    /// <summary>Die Standard-Absender-Regeln für Treatwell sind angelegt (ADR 0062); danach gehören die Regeln dem Spa.</summary>
    public bool SenderRulesSeeded { get; set; }

    /// <summary>Barzahlungsrabatt in Prozent, 0 = aus; eigene Belegzeile je Steuersatz und Erlöskonto.</summary>
    public decimal CashDiscountPercent { get; set; }

    /// <summary>Websites, die den Kundenbereich einbetten dürfen (<c>frame-ancestors</c> von <c>/buchen/</c>, ADR 0080); leer = nur die eigene Adresse.</summary>
    public List<string> EmbedAllowedOrigins { get; set; } = [];
}

/// <summary>
/// Der Mandant des laufenden Aufrufs. In HTTP-Anfragen und Hub-Aufrufen kommt er aus dem Anmelde-Cookie (Claim);
/// öffentliche Abläufe (Einladung annehmen, Spa registrieren, Online-Buchung) setzen ihn mit <see cref="Use"/>.
/// Ohne HTTP-Anfrage (Migration, Demodaten, Hintergrundaufträge) gilt er für alle Mandanten, bis <see cref="Use"/> ihn festlegt.
/// </summary>
public sealed class TenantContext(IHttpContextAccessor http)
{
    public const string ClaimType = "aispa:tenant";

    private Guid? _override;

    public Guid? TenantId => _override ?? (http.HttpContext?.User.FindFirstValue(ClaimType) is { } value && Guid.TryParse(value, out var id) ? id : null);

    /// <summary>Kein Filter: nur außerhalb von HTTP-Anfragen und solange kein Mandant gesetzt ist.</summary>
    public bool AllTenants => _override is null && http.HttpContext is null;

    public void Use(Guid tenantId) => _override = tenantId;
}
