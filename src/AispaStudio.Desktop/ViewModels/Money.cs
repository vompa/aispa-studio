using System.Globalization;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Controls;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>
/// Beträge wie an der Kassa üblich: „1.234,50 €“, in der Schweiz „CHF 1’234.50“; Eingaben mit Komma oder Punkt.
/// Die Währung kommt vom Standort bzw. vom Land der Kasse, ohne Angabe gilt Euro.
/// </summary>
public static class Money
{
    // Feste Trennzeichen statt de-AT und de-CH, weil Windows und ICU unterschiedliche Tausendertrennzeichen liefern.
    private static readonly NumberFormatInfo German = new() { NumberDecimalSeparator = ",", NumberGroupSeparator = "." };

    private static readonly NumberFormatInfo Swiss = new() { NumberDecimalSeparator = ".", NumberGroupSeparator = "’" };

    public const string Euro = "EUR";

    public static string CurrencyOf(FiscalCountry country) => country == FiscalCountry.CH ? "CHF" : Euro;

    public static string Format(decimal amount, string? currency = null) => currency switch
    {
        null or Euro => amount.ToString("#,##0.00 €", German),
        "CHF" => $"CHF {amount.ToString("#,##0.00", Swiss)}",
        _ => $"{amount.ToString("#,##0.00", German)} {currency}",
    };

    /// <summary>Ohne Nachkommastellen, z. B. geplanter Umsatz im Tagesüberblick.</summary>
    public static string Whole(decimal amount, string? currency = null) => currency switch
    {
        null or Euro => amount.ToString("#,##0 €", German),
        "CHF" => $"CHF {amount.ToString("#,##0", Swiss)}",
        _ => $"{amount.ToString("#,##0", German)} {currency}",
    };

    /// <summary>
    /// Leere Eingabe ist 0; Unlesbares, Negatives und Beträge mit mehr als zwei Nachkommastellen ergeben null, nie stilles Runden
    /// (Design-Brief 5.20, RKSV/TSE). Die Regeln des Betragsfelds stehen im <see cref="MoneyInputParser"/>; hier kommen nur die Schweizer Zeichen dazu.
    /// </summary>
    public static decimal? Parse(string? text)
    {
        var value = (text ?? "").Replace("CHF", "", StringComparison.OrdinalIgnoreCase).Replace("’", "", StringComparison.Ordinal)
            .Replace("'", "", StringComparison.Ordinal);
        var result = MoneyInputParser.Parse(value);
        return result.IsEmpty ? 0 : result.Value;
    }

    public static string Input(decimal amount, string? currency = null) =>
        amount.ToString("0.00", currency == "CHF" ? CultureInfo.InvariantCulture : German);

    public static string MethodText(PaymentMethod method) => method switch
    {
        PaymentMethod.Cash => "Bar",
        PaymentMethod.Card => "Karte",
        PaymentMethod.Voucher => "Gutschein",
        PaymentMethod.InstantTransfer => "Sofortüberweisung",
        PaymentMethod.Treatwell => "Über Treatwell bezahlt",
        _ => "Digitaler Euro",
    };
}
