using System.Globalization;

namespace AispaStudio.Desktop.Controls;

/// <summary>
/// Liest Uhrzeiten tolerant (Design-Brief 5.18): <c>9</c>, <c>930</c>, <c>9:30</c>, <c>9.30</c>, <c>9,30</c>, <c>9:5</c>; 24-Stunden-Uhr.
/// Die Anzeige ist immer <c>HH:mm</c>. Eine 24:00 gibt es nicht: ein Feld, das „bis Mitternacht“ erlaubt, existiert in der App nicht.
/// </summary>
public static class TimeInputParser
{
    public const string FormatError = "Bitte die Uhrzeit im Format 09:30 eingeben.";

    public const string RangeError = "Die Uhrzeit liegt zwischen 00:00 und 23:59.";

    public const string MissingError = "Die Uhrzeit fehlt. Bitte im Format 09:30 eingeben.";

    public const string EndBeforeStartError = "Ende liegt vor dem Beginn.";

    public const string EndEqualsStartError = "Das Ende muss nach dem Beginn liegen.";

    /// <summary>Tippen erlaubt: Ziffern, Doppelpunkt, Punkt und Komma.</summary>
    public const string AllowedCharacters = "0123456789:.,";

    public const int MaxLength = 5;

    public static string Format(TimeOnly time) => time.ToString("HH:mm", CultureInfo.InvariantCulture);

    /// <summary>Für ViewModels: die Uhrzeit oder null, wenn leer oder nicht lesbar (kein Fehlertext nötig).</summary>
    public static TimeOnly? TryParse(string? text) => Parse(text).Value;

    /// <summary>Für ViewModels, wo die Uhrzeit Pflicht ist: die Uhrzeit oder ein Fehlertext (auch bei leerer Eingabe).</summary>
    public static (TimeOnly? Time, string? Error) ParseRequired(string? text)
    {
        var result = Parse(text);
        return result.IsEmpty ? (null, MissingError) : (result.Value, result.Error);
    }

    public static InputResult<TimeOnly> Parse(string? text)
    {
        var raw = (text ?? "").Trim();
        if (raw.Length == 0)
        {
            return InputResult.Empty<TimeOnly>();
        }

        if (raw.Length > MaxLength || raw.Any(c => !AllowedCharacters.Contains(c, StringComparison.Ordinal)))
        {
            return InputResult.Fail<TimeOnly>(FormatError);
        }

        if (!TrySplit(raw, out var hours, out var minutes))
        {
            return InputResult.Fail<TimeOnly>(FormatError);
        }

        return hours is > 23 || minutes is > 59
            ? InputResult.Fail<TimeOnly>(RangeError)
            : InputResult.Ok<TimeOnly>(new TimeOnly(hours, minutes));
    }

    /// <summary>Ein Schritt ohne Umlauf über Mitternacht (der Wert wird begrenzt); ohne Wert setzt der erste Schritt den Anfang (Minimum, sonst 09:00).</summary>
    public static TimeOnly Step(TimeOnly? time, int minutes, TimeOnly? minimum = null, TimeOnly? maximum = null)
    {
        var lower = minimum is { } min ? (min.Hour * 60) + min.Minute : 0;
        var upper = maximum is { } max ? (max.Hour * 60) + max.Minute : (24 * 60) - 1;
        var target = time is { } current ? (current.Hour * 60) + current.Minute + minutes : minimum is null ? 9 * 60 : lower;
        var clamped = Math.Clamp(target, lower, Math.Max(lower, upper));
        return new TimeOnly(clamped / 60, clamped % 60);
    }

    private static bool TrySplit(string raw, out int hours, out int minutes)
    {
        hours = minutes = 0;
        var separators = raw.Count(c => c is ':' or '.' or ',');
        if (separators > 1)
        {
            return false;
        }

        if (separators == 1)
        {
            var parts = raw.Split(':', '.', ',');
            return parts[0].Length is 1 or 2 && parts[1].Length is 1 or 2
                && int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out hours)
                && int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out minutes);
        }

        // Nur Ziffern: „9“ und „09“ sind Stunden, „930“ und „0930“ Stunden und Minuten.
        switch (raw.Length)
        {
            case 1 or 2:
                return int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out hours);
            case 3:
                return int.TryParse(raw[..1], NumberStyles.None, CultureInfo.InvariantCulture, out hours)
                    && int.TryParse(raw[1..], NumberStyles.None, CultureInfo.InvariantCulture, out minutes);
            case 4:
                return int.TryParse(raw[..2], NumberStyles.None, CultureInfo.InvariantCulture, out hours)
                    && int.TryParse(raw[2..], NumberStyles.None, CultureInfo.InvariantCulture, out minutes);
            default:
                return false;
        }
    }
}
