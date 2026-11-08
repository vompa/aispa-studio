using System.Globalization;

namespace AispaStudio.Desktop.Controls;

/// <summary>
/// Gemeinsamer Kern für Zahlen- und Betragsfelder (Design-Brief 5.19, 5.20): Dezimalkomma, Tausenderpunkt, Minus nur vorn.
/// Feste Trennzeichen statt de-AT, weil Windows und ICU unterschiedliche Tausendertrennzeichen liefern (wie <c>Money</c>).
/// </summary>
internal static class GermanNumbers
{
    internal static readonly NumberFormatInfo Format = new() { NumberDecimalSeparator = ",", NumberGroupSeparator = ".", NegativeSign = "-" };

    /// <summary>
    /// Zerlegt die Eingabe in Vorzeichen, ganze Ziffern und Nachkommaziffern; false, wenn sie keine Zahl ist.
    /// <paramref name="dotMayBeDecimal"/>: ein einzelner Punkt mit ein oder zwei Ziffern danach und ohne Komma gilt als Dezimalzeichen (Zehnertastatur).
    /// </summary>
    internal static bool TryRead(string text, bool dotMayBeDecimal, out bool negative, out string integerDigits, out string fractionDigits)
    {
        negative = false;
        integerDigits = fractionDigits = "";
        var value = text.Replace(" ", "", StringComparison.Ordinal).Replace(" ", "", StringComparison.Ordinal);
        if (value.StartsWith('-'))
        {
            negative = true;
            value = value[1..];
        }

        if (value.Length == 0 || value.Any(c => !char.IsAsciiDigit(c) && c is not ('.' or ',')))
        {
            return false;
        }

        var commas = value.Count(c => c == ',');
        var dots = value.Count(c => c == '.');
        string integerPart;
        if (commas > 1)
        {
            return false;
        }

        if (commas == 1)
        {
            var at = value.IndexOf(',', StringComparison.Ordinal);
            integerPart = value[..at];
            fractionDigits = value[(at + 1)..];
        }
        else if (dotMayBeDecimal && dots == 1 && value.IndexOf('.', StringComparison.Ordinal) is var dot and > 0 && value.Length - dot - 1 is 1 or 2)
        {
            integerPart = value[..dot];
            fractionDigits = value[(dot + 1)..];
        }
        else
        {
            integerPart = value;
        }

        if (fractionDigits.Any(c => !char.IsAsciiDigit(c)) || (integerPart.Length == 0 && fractionDigits.Length == 0) || !IsGrouped(integerPart))
        {
            return false;
        }

        integerDigits = integerPart.Replace(".", "", StringComparison.Ordinal);
        if (integerDigits.Length == 0)
        {
            integerDigits = "0";
        }

        // Mehr als 15 Ziffern passen nicht mehr sicher in einen Betrag.
        return integerDigits.Length + fractionDigits.Length <= 15;
    }

    /// <summary>Ziffern, auf Wunsch mit Tausenderpunkten: „1234“ oder „1.234“, nie „1.23“.</summary>
    private static bool IsGrouped(string integerPart)
    {
        if (integerPart.Length == 0)
        {
            return true;
        }

        var groups = integerPart.Split('.');
        if (groups[0].Length == 0 || groups.Any(g => g.Any(c => !char.IsAsciiDigit(c))))
        {
            return false;
        }

        return groups.Length == 1 || (groups[0].Length <= 3 && groups.Skip(1).All(g => g.Length == 3));
    }

    internal static decimal ToDecimal(bool negative, string integerDigits, string fractionDigits)
    {
        var value = decimal.Parse($"{integerDigits}.{(fractionDigits.Length == 0 ? "0" : fractionDigits)}", NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture);
        return negative ? -value : value;
    }
}
