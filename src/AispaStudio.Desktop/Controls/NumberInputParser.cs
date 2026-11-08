namespace AispaStudio.Desktop.Controls;

/// <summary>Was ein Zahlenfeld verlangt: Grenzen und wie viele Nachkommastellen erlaubt sind (0 = ganze Zahl).</summary>
public sealed record NumberRules(decimal? Minimum = null, decimal? Maximum = null, int DecimalPlaces = 0);

/// <summary>
/// Liest Zahlen (Design-Brief 5.19, de-AT): Dezimalkomma, Tausenderpunkt, Minus nur bei einem Minimum unter 0. Formeln werden nicht gerechnet.
/// Ungültiges ist ein Fehlertext mit den Grenzen des Feldes, nie stilles Begrenzen. Leer heißt „keine Angabe“.
/// </summary>
public static class NumberInputParser
{
    public const string FormatError = "Bitte eine Zahl eingeben.";

    public const string WholeError = "Bitte eine ganze Zahl eingeben.";

    public const string MissingError = "Die Zahl fehlt. Bitte eine Zahl eingeben.";

    /// <summary>Tippen erlaubt: Ziffern, Komma (Dezimalzeichen), Punkt (Tausendertrenner) und bei einem Minimum unter 0 das Minus.</summary>
    public static string AllowedCharacters(NumberRules rules) => rules.Minimum < 0 ? "0123456789,.-" : "0123456789,.";

    public static string Format(decimal value, NumberRules rules) =>
        value.ToString(rules.DecimalPlaces > 0 ? $"#,##0.{new string('#', rules.DecimalPlaces)}" : "#,##0", GermanNumbers.Format);

    /// <summary>Für ViewModels: die Zahl oder null, wenn leer oder nicht lesbar.</summary>
    public static decimal? TryParse(string? text, NumberRules? rules = null) => Parse(text, rules).Value;

    public static InputResult<decimal> Parse(string? text, NumberRules? rules = null)
    {
        rules ??= new NumberRules();
        var raw = (text ?? "").Trim();
        if (raw.Length == 0)
        {
            return InputResult.Empty<decimal>();
        }

        if (!GermanNumbers.TryRead(raw, dotMayBeDecimal: false, out var negative, out var integerDigits, out var fractionDigits)
            || (negative && rules.Minimum is not < 0))
        {
            return InputResult.Fail<decimal>(FormatError);
        }

        fractionDigits = fractionDigits.TrimEnd('0');
        if (fractionDigits.Length > rules.DecimalPlaces)
        {
            return InputResult.Fail<decimal>(rules.DecimalPlaces == 0 ? WholeError : $"Höchstens {rules.DecimalPlaces} Nachkommastellen.");
        }

        var value = GermanNumbers.ToDecimal(negative, integerDigits, fractionDigits);
        return RangeError(value, rules) is { } error ? InputResult.Fail<decimal>(error) : InputResult.Ok<decimal>(value);
    }

    /// <summary>Ein Schritt; das Ergebnis liegt in den Grenzen (kein Umlauf). Ohne Wert beginnt er beim Minimum, sonst bei 0.</summary>
    public static decimal Step(decimal? value, decimal delta, NumberRules? rules = null)
    {
        rules ??= new NumberRules();
        var target = value is { } current ? current + delta : rules.Minimum ?? 0m;
        return Math.Clamp(target, rules.Minimum ?? decimal.MinValue, rules.Maximum ?? decimal.MaxValue);
    }

    private static string? RangeError(decimal value, NumberRules rules) => (rules.Minimum, rules.Maximum) switch
    {
        ({ } min, { } max) when value < min || value > max => $"Der Wert liegt zwischen {Format(min, rules)} und {Format(max, rules)}.",
        ({ } min, null) when value < min => $"Der Wert ist mindestens {Format(min, rules)}.",
        (null, { } max) when value > max => $"Der Wert ist höchstens {Format(max, rules)}.",
        _ => null,
    };
}
