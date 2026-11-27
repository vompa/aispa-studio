using AispaStudio.Contracts;

namespace AispaStudio.Api.Pos.Fiscal;

internal static class FiscalProviderCatalog
{
    public static IReadOnlyList<FiscalProviderDto> For(FiscalCountry country) => country switch
    {
        FiscalCountry.AT => [new("test", "AT0-Testeinrichtung", FiscalProviderKind.Test, true, [], "kassa")],
        FiscalCountry.DE => [new("test", "Test-TSE", FiscalProviderKind.Test, true, [], "kassa")],
        FiscalCountry.CH => [new("none", "Ohne Signatureinrichtung", FiscalProviderKind.NoSignature, false, [], "kassa")],
        _ => [],
    };

    public static FiscalProviderDto? Select(FiscalCountry country, string? id)
    {
        var providers = For(country);
        if (string.IsNullOrEmpty(id))
        {
            return providers.Count == 0 ? null : providers[0];
        }

        return providers.FirstOrDefault(p => p.Id == id);
    }
}
