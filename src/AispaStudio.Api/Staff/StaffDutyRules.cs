using System.Globalization;

namespace AispaStudio.Api.Staff;

/// <summary>
/// Regeln zu Zertifikaten und Zuständigkeiten (R05 Slice 117, Ä274, E14), rein: ohne Datenbank und ohne Uhr; „heute“ und der Tag kommen in der Zeitzone des Spas herein.
/// </summary>
internal static class StaffDutyRules
{
    public const int DefaultRemindDays = 60;

    private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-AT");

    /// <summary>Ab diesem Tag erinnert ein Zertifikat: „gültig bis“ minus Frist.</summary>
    public static DateOnly ReminderDay(DateOnly validUntil, int remindDays) => validUntil.AddDays(-remindDays);

    /// <summary>Ein neueres Zertifikat gleicher Bezeichnung mit späterem (oder offenem) „gültig bis“ erneuert dieses.</summary>
    public static bool IsRenewed(StaffCertificate certificate, IEnumerable<StaffCertificate> sameEmployee) =>
        certificate.ValidUntil is { } until
        && sameEmployee.Any(other => other.Id != certificate.Id && Matches(other.Name, certificate.Name)
            && (other.ValidUntil is null || other.ValidUntil > until));

    /// <summary>Fällig ab dem Erinnerungstag, auch nach dem Ablauf, bis es erneuert ist; unbefristete nie.</summary>
    public static bool IsDue(StaffCertificate certificate, DateOnly today, IEnumerable<StaffCertificate> sameEmployee) =>
        certificate.ValidUntil is { } until && today >= ReminderDay(until, certificate.RemindDays) && !IsRenewed(certificate, sameEmployee);

    /// <summary>Bezeichnung gleich dem verlangten Zertifikat, ohne Groß-/Kleinschreibung und Leerzeichen am Rand.</summary>
    public static bool Matches(string certificateName, string required) =>
        string.Equals(certificateName.Trim(), required.Trim(), StringComparison.OrdinalIgnoreCase);

    /// <summary>Gilt das Zertifikat an diesem Tag? „Ausgestellt am“ und „gültig bis“ einschließlich.</summary>
    public static bool IsValidOn(StaffCertificate certificate, DateOnly day) => StaffRegulationModel.IsValidOn(day, certificate.IssuedOn, certificate.ValidUntil);

    /// <summary>Erfüllt eine Zuständigkeit ihr verlangtes Zertifikat an diesem Tag? Ohne Verlangen immer.</summary>
    public static bool IsSatisfied(string? required, IEnumerable<StaffCertificate> certificates, DateOnly day) =>
        string.IsNullOrWhiteSpace(required) || certificates.Any(c => Matches(c.Name, required) && IsValidOn(c, day));

    /// <summary>Warnung, wenn weniger Ersthelferinnen im Dienst sind als verlangt; <c>null</c>, wenn genug oder die Mindestzahl 0 ist.</summary>
    public static string? ShortageWarning(int onDuty, int minimum, DateOnly day)
    {
        if (onDuty >= minimum)
        {
            return null;
        }

        var date = day.ToString("dddd, d.M.", German);
        return onDuty == 0 ? $"Keine Ersthelferin im Dienst am {date}" : $"Nur {onDuty} von {minimum} Ersthelferinnen im Dienst am {date}";
    }

    /// <summary>„Erste-Hilfe-Kurs läuft am 31.12. ab“ bzw. „… ist am 31.12. abgelaufen“.</summary>
    public static string ReminderText(string name, DateOnly validUntil, DateOnly today) =>
        validUntil < today ? $"{name} ist am {Day(validUntil)} abgelaufen" : $"{name} läuft am {Day(validUntil)} ab";

    /// <summary>Tag ohne Jahr, „31.12.“.</summary>
    public static string Day(DateOnly day) => day.ToString("d.M.", CultureInfo.InvariantCulture);
}
