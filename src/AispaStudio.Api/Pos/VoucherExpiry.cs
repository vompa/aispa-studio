using System.Globalization;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Pos;

/// <summary>
/// Standard-Ablaufdatum für Gutscheine je Land der Kasse (ADR 0010, Nachtrag 2026-11-09): Deutschland bis 31.12. des dritten Jahres nach dem
/// Kaufjahr (Verjährung §§ 195, 199 BGB), Schweiz fünf Jahre ab Kauf, Österreich ohne Ablaufdatum (kürzere Fristen ohne Auszahlung sind dort
/// unwirksam). Ein anderes Datum bleibt erlaubt; ist es kürzer, gibt es einen Hinweis, kein Verbot.
/// </summary>
public static class VoucherExpiry
{
    private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-AT");

    public static DateOnly? Default(FiscalCountry country, DateOnly purchase) => country switch
    {
        FiscalCountry.DE => new DateOnly(purchase.Year + 3, 12, 31),
        FiscalCountry.CH => purchase.AddYears(5),
        FiscalCountry.AT => null,
        _ => throw new ArgumentOutOfRangeException(nameof(country), country, null),
    };

    /// <summary>Kurze Erklärung der Vorbelegung für das Feld in der Kassa.</summary>
    public static string Rule(FiscalCountry country) => country switch
    {
        FiscalCountry.DE => "Vorgabe in Deutschland: bis 31.12. des dritten Jahres nach dem Kauf. Leer: unbefristet.",
        FiscalCountry.CH => "Vorgabe in der Schweiz: fünf Jahre ab Kauf. Leer: unbefristet.",
        FiscalCountry.AT => "In Österreich ohne Ablaufdatum; leer lassen.",
        _ => throw new ArgumentOutOfRangeException(nameof(country), country, null),
    };

    /// <summary>Hinweis zu einem eingetragenen Datum: in Österreich bei jedem Datum, sonst nur, wenn es vor der Standardfrist liegt.</summary>
    public static string? Warning(FiscalCountry country, DateOnly purchase, DateOnly? until)
    {
        if (until is not { } date)
        {
            return null;
        }

        if (country == FiscalCountry.AT)
        {
            return "In Österreich sind Fristen unter 5 Jahren ohne Auszahlungsmöglichkeit unwirksam.";
        }

        return Default(country, purchase) is { } safe && date < safe
            ? $"Kürzer als die übliche Frist bis {safe.ToString("dd.MM.yyyy", German)}; eine kürzere Frist kann unwirksam sein."
            : null;
    }
}
