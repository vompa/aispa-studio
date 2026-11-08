using AispaStudio.Contracts;

namespace AispaStudio.Api.Pos;

/// <summary>Umsatzsteuersätze je Land (Stand 2026). Der Prozentsatz wird je Position festgeschrieben, spätere Änderungen wirken nur auf neue Belege.</summary>
internal static class TaxRates
{
    private static readonly Dictionary<FiscalCountry, Dictionary<TaxCategory, decimal>> Rates = new()
    {
        [FiscalCountry.AT] = new()
        {
            [TaxCategory.Normal] = 20m,
            [TaxCategory.Reduced1] = 10m,
            [TaxCategory.Reduced2] = 13m,
            [TaxCategory.Special] = 19m,
            [TaxCategory.Zero] = 0m,
        },
        [FiscalCountry.DE] = new()
        {
            [TaxCategory.Normal] = 19m,
            [TaxCategory.Reduced1] = 7m,
            [TaxCategory.Zero] = 0m,
        },
        [FiscalCountry.CH] = new()
        {
            [TaxCategory.Normal] = 8.1m,
            [TaxCategory.Reduced1] = 2.6m,
            [TaxCategory.Reduced2] = 3.8m,
            [TaxCategory.Zero] = 0m,
        },
    };

    public static bool TryGet(FiscalCountry country, TaxCategory category, out decimal percent) =>
        Rates[country].TryGetValue(category, out percent);

    public static decimal Net(decimal gross, decimal percent) =>
        Math.Round(gross / (1 + (percent / 100m)), 2, MidpointRounding.AwayFromZero);

    /// <summary>Nettosumme: je Steuersatz die Bruttobeträge summieren und erst dann den Satz herausrechnen (gerundet wird je Satz).</summary>
    public static decimal NetSum<T>(IEnumerable<T> items, Func<T, decimal> gross, Func<T, decimal> percent) =>
        items.GroupBy(percent).Sum(group => Net(group.Sum(gross), group.Key));

    /// <summary>Steuer je Steuersatz aus Bruttobeträgen; gerundet wird je Satz, nicht je Position.</summary>
    public static List<TaxLineDto> Summarize(IEnumerable<(TaxCategory Category, decimal Percent, decimal Gross)> lines) =>
        [.. lines.GroupBy(l => (l.Category, l.Percent))
            .OrderBy(g => g.Key.Category)
            .Select(g =>
            {
                var gross = g.Sum(l => l.Gross);
                var net = Net(gross, g.Key.Percent);
                return new TaxLineDto(g.Key.Category, g.Key.Percent, gross, net, gross - net);
            })];
}
