using System.Globalization;

namespace AispaStudio.Desktop.Controls;

/// <summary>Was ein Datumsfeld zusätzlich verlangt: Grenzen, Jahr, nicht in der Zukunft (Geburtstag).</summary>
public sealed record DateRules(DateOnly? Minimum = null, DateOnly? Maximum = null, bool RequireYear = false, bool NotInFuture = false);

/// <summary>
/// Liest Datumsangaben tag-zuerst (Design-Brief 5.17, de-AT): <c>21102026</c>, <c>211026</c>, <c>21.10.26</c>, <c>21.10.</c>, <c>1.2.26</c>.
/// Ein zweistelliges Jahr wird das Jahr, das heute am nächsten liegt (Geburtstag: nie in der Zukunft); ohne Jahr gilt das laufende.
/// Die Anzeige ist immer <c>TT.MM.JJJJ</c>.
/// </summary>
public static class DateInputParser
{
    public const string FormatError = "Bitte das Datum im Format 21.10.2026 eingeben.";

    public const string MissingError = "Das Datum fehlt. Bitte im Format 21.10.2026 eingeben.";

    public const string YearMissingError = "Bitte das Jahr angeben, z. B. 14.03.1985.";

    /// <summary>Tippen erlaubt: Ziffern, Punkt, Schrägstrich und Bindestrich.</summary>
    public const string AllowedCharacters = "0123456789./-";

    public const int MaxLength = 10;

    public static string Format(DateOnly date) => date.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);

    /// <summary>Für ViewModels: das Datum oder null, wenn leer oder nicht lesbar.</summary>
    public static DateOnly? TryParse(string? text, DateOnly today, DateRules? rules = null) => Parse(text, today, rules).Value;

    /// <summary>Für ViewModels, wo das Datum Pflicht ist: das Datum oder ein Fehlertext (auch bei leerer Eingabe).</summary>
    public static (DateOnly? Date, string? Error) ParseRequired(string? text, DateOnly today, DateRules? rules = null)
    {
        var result = Parse(text, today, rules);
        return result.IsEmpty ? (null, MissingError) : (result.Value, result.Error);
    }

    public static InputResult<DateOnly> Parse(string? text, DateOnly today, DateRules? rules = null)
    {
        var raw = (text ?? "").Trim();
        if (raw.Length == 0)
        {
            return InputResult.Empty<DateOnly>();
        }

        if (raw.Length > MaxLength || raw.Any(c => !AllowedCharacters.Contains(c, StringComparison.Ordinal)) || !TrySplit(raw, out var parts))
        {
            return InputResult.Fail<DateOnly>(FormatError);
        }

        rules ??= new DateRules();
        var (dayText, monthText, yearText) = parts;
        if (yearText is null && rules.RequireYear)
        {
            return InputResult.Fail<DateOnly>(YearMissingError);
        }

        var day = int.Parse(dayText, CultureInfo.InvariantCulture);
        var month = int.Parse(monthText, CultureInfo.InvariantCulture);
        var year = yearText switch
        {
            null => today.Year,
            { Length: 4 } => int.Parse(yearText, CultureInfo.InvariantCulture),
            _ => Century(int.Parse(yearText, CultureInfo.InvariantCulture), day, month, today, rules.NotInFuture),
        };
        if (year is < 1 or > 9999 || month is < 1 or > 12 || day < 1 || day > DateTime.DaysInMonth(year, month))
        {
            return InputResult.Fail<DateOnly>($"Den {day:00}.{month:00}.{year:0000} gibt es nicht.");
        }

        var date = new DateOnly(year, month, day);
        var maximum = rules.NotInFuture && (rules.Maximum is null || rules.Maximum > today) ? today : rules.Maximum;
        return RangeError(date, rules.Minimum, maximum) is { } error ? InputResult.Fail<DateOnly>(error) : InputResult.Ok<DateOnly>(date);
    }

    /// <summary>Ein Schritt in Tagen, Monaten oder Jahren; ohne Wert setzt der erste Schritt den heutigen Tag. Das Ergebnis liegt in den Grenzen.</summary>
    public static DateOnly Step(DateOnly? date, DateOnly today, int days = 0, int months = 0, int years = 0, DateRules? rules = null)
    {
        var target = date is { } current ? current.AddYears(years).AddMonths(months).AddDays(days) : today;
        if (rules?.Minimum is { } minimum && target < minimum)
        {
            target = minimum;
        }

        var maximum = rules?.NotInFuture == true && (rules.Maximum is null || rules.Maximum > today) ? today : rules?.Maximum;
        return maximum is { } max && target > max ? max : target;
    }

    private static string? RangeError(DateOnly date, DateOnly? minimum, DateOnly? maximum) => (minimum, maximum) switch
    {
        ({ } min, { } max) when date < min || date > max => $"Das Datum liegt zwischen {Format(min)} und {Format(max)}.",
        ({ } min, null) when date < min => $"Das Datum liegt am {Format(min)} oder später.",
        (null, { } max) when date > max => $"Das Datum liegt am {Format(max)} oder früher.",
        _ => null,
    };

    /// <summary>Das Jahrhundert, in dem das zweistellige Jahr heute am nächsten liegt; beim Geburtstag das jüngste, das nicht in der Zukunft liegt.</summary>
    private static int Century(int twoDigits, int day, int month, DateOnly today, bool notInFuture)
    {
        var candidates = new[] { (today.Year / 100 * 100) + twoDigits - 100, (today.Year / 100 * 100) + twoDigits, (today.Year / 100 * 100) + twoDigits + 100 };
        if (notInFuture)
        {
            var past = candidates.Where(y => y >= 1 && y <= today.Year && (y < today.Year || (month, day).CompareTo((today.Month, today.Day)) <= 0)).ToList();
            return past.Count > 0 ? past.Max() : candidates[0];
        }

        return candidates.Where(y => y >= 1).OrderBy(y => Math.Abs(y - today.Year)).ThenBy(y => y).First();
    }

    private static bool TrySplit(string raw, out (string Day, string Month, string? Year) parts)
    {
        parts = default;
        var separators = raw.Count(c => c is '.' or '/' or '-');
        if (separators == 0)
        {
            // Nur Ziffern: TTMM, TTMMJJ oder TTMMJJJJ.
            switch (raw.Length)
            {
                case 4:
                    parts = (raw[..2], raw[2..4], null);
                    return true;
                case 6 or 8:
                    parts = (raw[..2], raw[2..4], raw[4..]);
                    return true;
                default:
                    return false;
            }
        }

        var pieces = raw.Split('.', '/', '-');
        // „21.10.“ endet mit einem Trenner: kein Jahr.
        if (pieces.Length == 3 && pieces[2].Length == 0)
        {
            pieces = pieces[..2];
        }

        if (pieces.Length is < 2 or > 3 || pieces[0].Length is < 1 or > 2 || pieces[1].Length is < 1 or > 2)
        {
            return false;
        }

        if (pieces.Length == 3 && pieces[2].Length is not (2 or 4))
        {
            return false;
        }

        parts = (pieces[0], pieces[1], pieces.Length == 3 ? pieces[2] : null);
        return true;
    }
}
