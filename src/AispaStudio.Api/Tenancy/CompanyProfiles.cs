using System.Net.Mail;
using AispaStudio.Api.Pos;
using AispaStudio.Api.Scheduling;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Tenancy;

/// <summary>
/// Firmendaten des Spas (ADR 0052): einmal für alle Standorte gepflegt. Ein Standort mit eigenen Unternehmensangaben
/// (z. B. eine eigene Gesellschaft) verwendet diese vollständig; Angaben werden nie zwischen zwei Gesellschaften gemischt.
/// </summary>
internal static class CompanyProfiles
{
    public static CompanyProfile Of(Tenant t) => new(t.LegalName, t.Street, t.PostalCode, t.City, t.Phone, t.Email, t.Website, t.VatId,
        t.CompanyRegisterNumber, t.CompanyRegisterCourt, t.AccountHolder, t.Iban, t.Bic, t.BusinessPurpose, t.SupervisoryAuthority,
        t.ChamberAndRules);

    public static void Validate(CompanyProfile p, Errors errors)
    {
        Max(errors, nameof(p.LegalName), p.LegalName, 200, "Firmenwortlaut");
        Max(errors, nameof(p.Street), p.Street, 200, "Straße");
        Max(errors, nameof(p.PostalCode), p.PostalCode, 10, "Postleitzahl");
        Max(errors, nameof(p.City), p.City, 100, "Ort");
        Max(errors, nameof(p.Phone), p.Phone, 40, "Telefon");
        Max(errors, nameof(p.Website), p.Website, 200, "Website");
        Max(errors, nameof(p.VatId), p.VatId, 20, "UID-Nummer");
        Max(errors, nameof(p.CompanyRegisterNumber), p.CompanyRegisterNumber, 30, "Firmenbuchnummer");
        Max(errors, nameof(p.CompanyRegisterCourt), p.CompanyRegisterCourt, 100, "Firmenbuchgericht");
        Max(errors, nameof(p.AccountHolder), p.AccountHolder, 70, "Kontoinhaber");
        Max(errors, nameof(p.Bic), p.Bic, 11, "BIC");
        Max(errors, nameof(p.BusinessPurpose), p.BusinessPurpose, 300, "Unternehmensgegenstand");
        Max(errors, nameof(p.SupervisoryAuthority), p.SupervisoryAuthority, 200, "Aufsichtsbehörde");
        Max(errors, nameof(p.ChamberAndRules), p.ChamberAndRules, 300, "Kammer und Berufsrecht");
        errors.Check(string.IsNullOrWhiteSpace(p.Email) || MailAddress.TryCreate(p.Email.Trim(), out _) && p.Email.Trim().Length <= 254,
            nameof(p.Email), "Keine gültige E-Mail-Adresse.");
        errors.Check(Iban(p.Iban) is not { } iban || PosService.IsValidIban(iban), nameof(p.Iban), "Keine gültige IBAN.");
    }

    public static void Apply(Tenant t, CompanyProfile p)
    {
        t.LegalName = Clean(p.LegalName);
        t.Street = Clean(p.Street);
        t.PostalCode = Clean(p.PostalCode);
        t.City = Clean(p.City);
        t.Phone = Clean(p.Phone);
        t.Email = Clean(p.Email);
        t.Website = Clean(p.Website);
        t.VatId = Clean(p.VatId)?.ToUpperInvariant();
        t.CompanyRegisterNumber = Clean(p.CompanyRegisterNumber);
        t.CompanyRegisterCourt = Clean(p.CompanyRegisterCourt);
        t.AccountHolder = Clean(p.AccountHolder);
        t.Iban = Iban(p.Iban);
        t.Bic = Clean(p.Bic)?.ToUpperInvariant();
        t.BusinessPurpose = Clean(p.BusinessPurpose);
        t.SupervisoryAuthority = Clean(p.SupervisoryAuthority);
        t.ChamberAndRules = Clean(p.ChamberAndRules);
    }

    /// <summary>Unternehmensangaben für Rechnungen: die des Standorts, wenn er eigene hat, sonst die Firmendaten des Spas.</summary>
    public static LocationCompany CompanyFor(Location location, Tenant? tenant) =>
        HasOwnCompany(location) || tenant is null
            ? MasterDataEndpoints.CompanyOf(location)
            : new(tenant.LegalName, tenant.VatId, tenant.CompanyRegisterNumber, tenant.CompanyRegisterCourt);

    public static bool HasOwnCompany(Location l) =>
        l.LegalName is not null || l.VatId is not null || l.CompanyRegisterNumber is not null || l.CompanyRegisterCourt is not null;

    /// <summary>Impressum der Online-Buchung. Anschrift und Kontakt notfalls vom ersten Standort.</summary>
    public static OnlineImprint Imprint(Tenant t, Location? first)
    {
        var useLocation = t.Street is null && t.City is null && first is not null;
        var street = useLocation ? first!.Street : t.Street;
        var postalCode = useLocation ? first!.PostalCode : t.PostalCode;
        var city = useLocation ? first!.City : t.City;
        var phone = t.Phone ?? first?.Phone;
        var email = t.Email ?? first?.Email;
        var complete = t.LegalName is not null && street is not null && city is not null && email is not null;
        return new OnlineImprint(t.Name, t.Country, t.LegalName, street, postalCode, city, phone, email, t.Website, t.VatId,
            t.CompanyRegisterNumber, t.CompanyRegisterCourt, t.BusinessPurpose, t.SupervisoryAuthority, t.ChamberAndRules, complete);
    }

    private static string? Iban(string? value) =>
        Clean(value?.Replace(" ", "", StringComparison.Ordinal))?.ToUpperInvariant();

    private static void Max(Errors errors, string field, string? value, int max, string label) =>
        errors.Check((value?.Trim().Length ?? 0) <= max, field, $"{label} höchstens {max} Zeichen.");

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
