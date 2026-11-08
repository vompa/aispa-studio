using System.Text.RegularExpressions;

namespace AispaStudio.Contracts;

/// <summary>Telefonnummern aus SMS-, WhatsApp- und Telefonanbietern einheitlich als „+“ und Ziffern (E.164).</summary>
public static partial class PhoneNumbers
{
    /// <summary>„whatsapp:+43 660 123“, „43660123“ oder „+43-660-123“ werden zu „+43660123“; leer, wenn keine Ziffern.</summary>
    public static string Normalize(string value)
    {
        var raw = value.Trim();
        var colon = raw.IndexOf(':', StringComparison.Ordinal);
        if (colon >= 0)
        {
            raw = raw[(colon + 1)..];
        }

        var digits = new string([.. raw.Where(char.IsAsciiDigit)]);
        if (digits.StartsWith("00", StringComparison.Ordinal))
        {
            digits = digits[2..];
        }

        return digits.Length == 0 ? "" : $"+{digits}";
    }

    /// <summary>Die letzten neun Ziffern: genug, um „0660 123 4567“ im Kundenstamm als „+43 660 1234567“ zu finden.</summary>
    public static string Tail(string normalized)
    {
        var digits = normalized.TrimStart('+');
        return digits.Length <= 9 ? digits : digits[^9..];
    }

    private static readonly Dictionary<string, string> CountryCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["AT"] = "43",
        ["DE"] = "49",
        ["CH"] = "41",
    };

    /// <summary>
    /// Nummer im internationalen Format („+436601234567“), damit verschiedene Schreibweisen gleich vergleichbar sind. Nationale Nummern
    /// („0660 1234567“) bekommen die Vorwahl des Standortlandes (AT, DE, CH; sonst AT), „(0)“ nach der Landesvorwahl, Leerzeichen,
    /// Bindestriche, Schrägstriche und Klammern fallen weg. Leer, wenn die Nummer nicht lesbar ist (zu kurz oder zu lang, ohne Vorwahl).
    /// </summary>
    public static string ToE164(string? value, string? country = "AT")
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "";
        }

        var raw = value.Trim();
        var colon = raw.IndexOf(':', StringComparison.Ordinal);
        if (colon >= 0)
        {
            raw = raw[(colon + 1)..].Trim();
        }

        // „+43 (0) 660 …“: die eingeklammerte Null gehört nicht zur internationalen Nummer.
        var international = raw.StartsWith('+') || raw.StartsWith("00", StringComparison.Ordinal);
        if (international)
        {
            raw = TrunkZero().Replace(raw, "");
        }

        var digits = new string([.. raw.Where(char.IsAsciiDigit)]);
        if (digits.StartsWith("00", StringComparison.Ordinal))
        {
            digits = digits[2..];
        }
        else if (!international)
        {
            var code = CountryCodes.TryGetValue(country ?? "AT", out var known) ? known : CountryCodes["AT"];
            if (digits.StartsWith('0'))
            {
                digits = code + digits[1..];
            }
            else if (!CountryCodes.Values.Any(c => digits.StartsWith(c, StringComparison.Ordinal)) || digits.Length < 10)
            {
                // Ohne führende Null und ohne erkennbare Landesvorwahl lässt sich die Nummer nicht sicher deuten.
                return "";
            }
        }

        // Nach der Landesvorwahl stehen noch mindestens sechs Ziffern; kürzere Zahlen sind keine Telefonnummer.
        return digits.Length < 8 || digits.Length > 15 ? "" : $"+{digits}";
    }

    /// <summary>
    /// Nummer zum Anzeigen und Speichern im Kundenstamm: „+43 664 1234567“. Österreich trennt nach der Vorwahl (Wien „1“, sonst drei
    /// Ziffern), Deutschland trennt Mobilnummern (drei Ziffern), die Schweiz nach zwei Ziffern; andere Länder und Festnetz
    /// in Deutschland bleiben ungetrennt, weil ihre Vorwahlen unterschiedlich lang sind. Leer, wenn die Nummer nicht lesbar ist.
    /// </summary>
    public static string Format(string? value, string? country = "AT")
    {
        var e164 = ToE164(value, country);
        if (e164.Length == 0)
        {
            return "";
        }

        var digits = e164[1..];
        if (CountryCodes.Values.FirstOrDefault(c => digits.StartsWith(c, StringComparison.Ordinal)) is not { } code)
        {
            return e164;
        }

        var national = digits[code.Length..];
        var prefix = (code, national[0]) switch
        {
            ("43", '1') => 1,
            ("43", _) => 3,
            ("49", '1') => 3,
            ("41", _) => 2,
            _ => 0,
        };
        return prefix == 0 || national.Length <= prefix ? $"+{code} {national}" : $"+{code} {national[..prefix]} {national[prefix..]}";
    }

    /// <summary>Die letzten <paramref name="count"/> Ziffern einer Nummer in beliebiger Schreibweise.</summary>
    public static string LastDigits(string? value, int count)
    {
        var digits = new string([.. (value ?? "").Where(char.IsAsciiDigit)]);
        return digits.Length <= count ? digits : digits[^count..];
    }

    [GeneratedRegex(@"\(\s*0\s*\)")]
    private static partial Regex TrunkZero();
}
