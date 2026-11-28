using AispaStudio.Api.Pos.Fiscal.Rksv;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Pos.Fiscal;

internal sealed record FiscalProviderDefinition(FiscalCountry Country, FiscalProviderDto Provider);

internal sealed class FiscalProviderCatalog(IEnumerable<FiscalProviderDefinition> definitions)
{
    private readonly ILookup<FiscalCountry, FiscalProviderDto> _providers = definitions.ToLookup(d => d.Country, d => d.Provider);

    public IReadOnlyList<FiscalProviderDto> For(FiscalCountry country) => [.. _providers[country]];

    public FiscalProviderDto? Select(FiscalCountry country, string? id)
    {
        var selected = string.IsNullOrEmpty(id) ? country == FiscalCountry.CH ? "none" : "test" : id;
        return _providers[country].SingleOrDefault(p => p.Id == selected);
    }
}

internal static class FiscalProviders
{
    public static void AddFiscalProviders(this IServiceCollection services)
    {
        services.AddSingleton<FiscalProviderCatalog>();
        services.AddFiscalProvider<IRksvSignatureDevice, TestSignatureDevice>(FiscalCountry.AT,
            new("test", "AT0-Testeinrichtung", FiscalProviderKind.Test, true, [], "kassa"));
        services.AddFiscalProvider<ITseClient, TestTseClient>(FiscalCountry.DE,
            new("test", "Test-TSE", FiscalProviderKind.Test, true, [], "kassa"));
        services.AddFiscalProvider<IFiscalization, SwissFiscalization>(FiscalCountry.CH,
            new("none", "Ohne Signatureinrichtung", FiscalProviderKind.NoSignature, false, [], "kassa"));
    }

    public static void AddFiscalProvider<TService, TAdapter>(this IServiceCollection services, FiscalCountry country, FiscalProviderDto provider)
        where TService : class where TAdapter : class, TService
    {
        services.AddSingleton(new FiscalProviderDefinition(country, provider));
        services.AddScoped<TService, TAdapter>();
    }
}
