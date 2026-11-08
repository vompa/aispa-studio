using System.Globalization;
using AispaStudio.Api.Scheduling;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Stock;

/// <summary>Packungen eines Materials: die einzige Stelle, die Packung und Gebinde in die Einheit des Materials umrechnet.</summary>
internal static class StockPackaging
{
    public const int DefaultExpiryWarningDays = 30;

    private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-AT");

    /// <summary>Menge einer Ebene in der Einheit des Materials; <c>null</c>, wenn das Material diese Ebene nicht kennt.</summary>
    public static decimal? BaseQuantity(StockItem item, StockPackageLevel level) => level switch
    {
        StockPackageLevel.Unit => 1,
        StockPackageLevel.Pack => item.PackSize,
        StockPackageLevel.Case => item.PackSize * item.PacksPerCase,
        _ => null,
    };

    /// <summary>Die buchbaren Ebenen: immer die Einheit, dazu Packung und Gebinde, wenn das Material sie kennt.</summary>
    public static List<StockPackageDto> Packages(StockItem item)
    {
        var packages = new List<StockPackageDto> { new(StockPackageLevel.Unit, item.Unit, item.Unit, 1) };
        if (item.PackUnit is { } pack && item.PackSize is { } size)
        {
            packages.Add(new(StockPackageLevel.Pack, pack, $"{pack} ({Number(size)} {item.Unit})", size));
            if (item.CaseUnit is { } container && item.PacksPerCase is { } count)
            {
                packages.Add(new(StockPackageLevel.Case, container, $"{container} ({count} × {Number(size)} {item.Unit})", size * count));
            }
        }

        return packages;
    }

    /// <summary>Menge als ganze Packungen („3 × Flasche (500 ml)“); <c>null</c>, wenn das Material keine Packung kennt oder die Menge nicht aufgeht.</summary>
    public static string? PackageText(StockItem item, decimal quantity) =>
        item.PackUnit is { } pack && item.PackSize is { } size && quantity > 0 && quantity % size == 0
            ? $"{Number(quantity / size)} × {pack} ({Number(size)} {item.Unit})"
            : null;

    /// <summary>Für wie viele Behandlungen der Bestand laut Richtwert reicht; ohne Richtwert <c>null</c>.</summary>
    public static int? TreatmentsCovered(StockItem item, decimal quantity) =>
        item.TreatmentDose is { } dose && dose > 0 ? (int)Math.Floor(Math.Max(0, quantity) / dose) : null;

    /// <summary>Prüft die Angaben am Material: Packung und Gebinde nur vollständig, ein Gebinde nur mit Packung.</summary>
    public static void Check(Errors errors, StockItemInput input)
    {
        var hasPackName = !string.IsNullOrWhiteSpace(input.PackUnit);
        var hasCaseName = !string.IsNullOrWhiteSpace(input.CaseUnit);
        errors.Check(hasPackName == input.PackSize is not null, nameof(input.PackSize), "Packung und Menge je Packung zusammen eintragen, z. B. Flasche und 500.");
        errors.Check(IsAmount(input.PackSize), nameof(input.PackSize), "Menge je Packung größer als 0 mit höchstens drei Nachkommastellen.");
        errors.Check(input.PackUnit is null || input.PackUnit.Trim().Length <= 20, nameof(input.PackUnit), "Packung mit höchstens 20 Zeichen.");
        errors.Check(hasCaseName == input.PacksPerCase is not null, nameof(input.PacksPerCase), "Gebinde und Packungen je Gebinde zusammen eintragen, z. B. Karton und 4.");
        errors.Check(!hasCaseName || hasPackName, nameof(input.CaseUnit), "Ein Gebinde braucht eine Packung.");
        errors.Check(input.PacksPerCase is null or (> 0 and <= 10_000), nameof(input.PacksPerCase), "Packungen je Gebinde von 1 bis 10000.");
        errors.Check(!IsAmount(input.PackSize) || input.PackSize is null || input.PacksPerCase is not (> 0 and <= 10_000) || input.PackSize * input.PacksPerCase < 1_000_000, nameof(input.PacksPerCase),
            "Ein Gebinde fasst weniger als 1 Million in der Einheit des Materials.");
        errors.Check(input.CaseUnit is null || input.CaseUnit.Trim().Length <= 20, nameof(input.CaseUnit), "Gebinde mit höchstens 20 Zeichen.");
        errors.Check(IsAmount(input.TreatmentDose), nameof(input.TreatmentDose), "Menge je Behandlung größer als 0 mit höchstens drei Nachkommastellen.");
        errors.Check(input.ExpiryWarningDays is >= 1 and <= 365, nameof(input.ExpiryWarningDays), "Ablaufwarnung von 1 bis 365 Tagen.");
    }

    public static void Apply(StockItem item, StockItemInput input)
    {
        item.PackUnit = Blank(input.PackUnit);
        item.PackSize = input.PackSize;
        item.CaseUnit = Blank(input.CaseUnit);
        item.PacksPerCase = input.PacksPerCase;
        item.TreatmentDose = input.TreatmentDose;
        item.ExpiryWarningDays = input.ExpiryWarningDays;
    }

    private static bool IsAmount(decimal? value) => value is null || (value is > 0 and < 1_000_000 && decimal.Round(value.Value, 3) == value);

    private static string Number(decimal value) => value.ToString("0.###", German);

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
