using AispaStudio.Contracts;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>
/// Links aus Telefonnummer und E-Mail eines Gastes: <c>tel:</c> (öffnet die Anruf-App), <c>mailto:</c> mit dem Gast im Betreff und
/// <c>https://wa.me/&lt;Ziffern&gt;</c> für WhatsApp. WhatsApp braucht die Nummer in E.164 nur mit Ziffern; gelesen wird sie mit derselben Regel,
/// mit der die API Telefonnummern speichert (<see cref="PhoneNumbers.ToE164"/>, Landesvorwahl des Standorts für nationale Nummern).
/// </summary>
public static class PhoneLinks
{
    /// <summary>„tel:+436641234567“; Leerzeichen, Klammern, Striche und Schrägstriche fallen weg, ein führendes „+“ bleibt.</summary>
    public static Uri? Tel(string? phone)
    {
        var number = Compact(phone);
        return number.Length == 0 ? null : new Uri($"tel:{number}");
    }

    /// <summary>„mailto:eva@example.at?subject=Eva%20Muster“.</summary>
    public static Uri? Mail(string? email, string guestName) =>
        string.IsNullOrWhiteSpace(email) ? null : new Uri($"mailto:{email.Trim()}?subject={Uri.EscapeDataString(guestName)}");

    /// <summary>„https://wa.me/436641234567“; ohne eindeutige Nummer null.</summary>
    public static Uri? WhatsApp(string? phone, FiscalCountry country)
    {
        var digits = E164Digits(phone, country);
        return digits is null ? null : new Uri($"https://wa.me/{digits}");
    }

    /// <summary>E.164 ohne Plus („436641234567“) oder null, wenn sich die Landesvorwahl nicht herleiten lässt.</summary>
    public static string? E164Digits(string? phone, FiscalCountry country) =>
        PhoneNumbers.ToE164(phone, country.ToString()) is { Length: > 0 } e164 ? e164[1..] : null;

    private static string Compact(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
        {
            return "";
        }

        var text = phone.Replace("(0)", "", StringComparison.Ordinal).Trim();
        return string.Concat(text.Where((c, i) => char.IsAsciiDigit(c) || (i == 0 && c == '+')));
    }
}
