namespace AispaStudio.Desktop.Controls;

/// <summary>Was ein Betragsfeld zusätzlich verlangt: negativ nur bei Storno und Gutschrift, Grenzen.</summary>
public sealed record MoneyRules(bool AllowNegative = false, decimal? Minimum = null, decimal? Maximum = null);

/// <summary>
/// Liest Beträge mit <c>decimal</c> (Design-Brief 5.20, de-AT): <c>12</c> ergibt 12,00, <c>12,5</c> und <c>12.5</c> ergeben 12,50,
/// <c>1.234,56</c> ergibt 1.234,56, <c>1234</c> ergibt 1.234,00 (keine Kommaverschiebung). Mehr als zwei Nachkommastellen sind ein Fehler:
/// ein Betrag wird nie stumm gerundet (RKSV/TSE, ADR 0010). Leer heißt „keine Angabe“, nicht 0,00.
/// </summary>
public static class MoneyInputParser
{
    public const string FormatError = "Bitte einen Betrag wie 12,50 eingeben.";

    public const string FractionError = "Höchstens zwei Nachkommastellen.";

    public const string NegativeError = "Der Betrag darf nicht negativ sein.";

    public const string MissingError = "Der Betrag fehlt. Bitte wie 12,50 eingeben.";

    /// <summary>Tippen erlaubt: Ziffern, Komma, Punkt und (nur bei Storno und Gutschrift) Minus.</summary>
    public static string AllowedCharacters(bool allowNegative) => allowNegative ? "0123456789,.-" : "0123456789,.";

    public static string Format(decimal amount) => amount.ToString("#,##0.00", GermanNumbers.Format);

    /// <summary>Für ViewModels: der Betrag oder null, wenn leer oder nicht lesbar.</summary>
    public static decimal? TryParse(string? text, MoneyRules? rules = null) => Parse(text, rules).Value;

    public static InputResult<decimal> Parse(string? text, MoneyRules? rules = null)
    {
        rules ??= new MoneyRules();
        // Währungszeichen und geschützte Leerzeichen entfernt schon das Einfügen; hier zur Sicherheit auch beim Lesen.
        var raw = (text ?? "").Replace("€", "", StringComparison.Ordinal).Trim();
        if (raw.Length == 0)
        {
            return InputResult.Empty<decimal>();
        }

        if (!GermanNumbers.TryRead(raw, dotMayBeDecimal: true, out var negative, out var integerDigits, out var fractionDigits))
        {
            return InputResult.Fail<decimal>(FormatError);
        }

        if (fractionDigits.Length > 2)
        {
            return InputResult.Fail<decimal>(FractionError);
        }

        var amount = GermanNumbers.ToDecimal(negative, integerDigits, fractionDigits);
        if (negative && amount != 0 && !rules.AllowNegative)
        {
            return InputResult.Fail<decimal>(NegativeError);
        }

        return RangeError(amount, rules) is { } error ? InputResult.Fail<decimal>(error) : InputResult.Ok<decimal>(amount);
    }

    /// <summary>Ein Schritt (±1,00, Umschalt ±0,10, Bild ±10,00); ohne Wert beginnt er bei 0,00. Das Ergebnis liegt in den Grenzen.</summary>
    public static decimal Step(decimal? amount, decimal delta, MoneyRules? rules = null)
    {
        rules ??= new MoneyRules();
        var target = (amount ?? 0m) + delta;
        var lower = rules.Minimum ?? (rules.AllowNegative ? decimal.MinValue : 0m);
        var upper = rules.Maximum ?? decimal.MaxValue;
        return Math.Clamp(target, lower, upper);
    }

    private static string? RangeError(decimal amount, MoneyRules rules) => (rules.Minimum, rules.Maximum) switch
    {
        ({ } min, { } max) when amount < min || amount > max => $"Der Betrag liegt zwischen {Format(min)} und {Format(max)}.",
        ({ } min, null) when amount < min => $"Der Betrag ist mindestens {Format(min)}.",
        (null, { } max) when amount > max => $"Der Betrag ist höchstens {Format(max)}.",
        _ => null,
    };
}
