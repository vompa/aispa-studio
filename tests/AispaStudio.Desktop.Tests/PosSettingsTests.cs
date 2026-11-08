using System.Net;
using System.Text.Json;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;
using AispaStudio.Desktop.ViewModels;

namespace AispaStudio.Desktop.Tests;

/// <summary>R05 Slice 96 (Ä191, ADR 0084, ADR 0010): Kassa-Einstellungen als native Maske; die Kassa-Logik bleibt in der API.</summary>
public sealed class PosSettingsTests
{
    private static StubApi Api() => new StubApi()
        .On("GET /api/pos/settings", new PosSettingsDto(10m, true, 2m))
        .OnJson("PUT /api/pos/settings", body =>
        {
            var input = JsonSerializer.Deserialize<PosSettingsInput>(body!, ApiClient.Json)!;
            return new PosSettingsDto(input.DiscountLimitPercent, true, input.CashDiscountPercent ?? 0);
        });

    private static async Task<PosSettingsViewModel> Loaded(StubApi api)
    {
        var settings = new PosSettingsViewModel(api.Client());
        await settings.LoadAsync();
        Assert.True(settings.IsLoaded);
        return settings;
    }

    [Fact]
    public async Task Given_10_und_2_Prozent_When_12_5_eingetragen_und_gespeichert_Then_PUT_mit_12_5_und_2()
    {
        var api = Api();
        var settings = await Loaded(api);
        Assert.Equal((10m, 2m), (settings.DiscountLimit, settings.CashDiscount));

        settings.DiscountLimit = 12.5m;
        Assert.True(settings.HasUnsavedChanges);
        Assert.True(await settings.SaveChangesAsync());

        var put = Assert.Single(api.Calls, c => c.Route == "PUT /api/pos/settings");
        Assert.Equal(new PosSettingsInput(12.5m, 2m), JsonSerializer.Deserialize<PosSettingsInput>(put.Body!, ApiClient.Json));
        Assert.False(settings.HasUnsavedChanges);
        Assert.Equal("Gespeichert.", settings.StatusText);
    }

    [Fact]
    public async Task Given_unveraenderte_Einstellungen_When_gespeichert_Then_geht_nichts_an_die_API()
    {
        var api = Api();
        var settings = await Loaded(api);

        Assert.True(await settings.SaveChangesAsync());

        Assert.DoesNotContain(api.Calls, c => c.Route.StartsWith("PUT ", StringComparison.Ordinal));
        Assert.Equal("Gespeichert.", settings.StatusText);
    }

    [Fact]
    public async Task Given_API_lehnt_ab_When_gespeichert_Then_Fehler_ueber_der_Leiste_und_ungespeichert()
    {
        var api = Api().On("PUT /api/pos/settings",
            new { title = "Eingaben prüfen", errors = new Dictionary<string, string[]> { ["CashDiscountPercent"] = ["Zwischen 0 und 100 Prozent."] } }, HttpStatusCode.BadRequest);
        var settings = await Loaded(api);

        settings.CashDiscount = 3m;
        Assert.False(await settings.SaveChangesAsync());

        Assert.Contains("Zwischen 0 und 100 Prozent.", settings.Error, StringComparison.Ordinal);
        Assert.True(settings.HasUnsavedChanges);
        Assert.Null(settings.StatusText);
    }

    [Fact]
    public async Task Given_leere_Rabattgrenze_When_gespeichert_Then_Fehler_am_Feld_und_kein_Aufruf()
    {
        var api = Api();
        var settings = await Loaded(api);

        settings.DiscountLimit = null;
        Assert.False(await settings.SaveChangesAsync());

        Assert.NotNull(settings.LimitError);
        Assert.Equal(PosSettingsViewModel.LimitField, settings.FocusTarget);
        Assert.DoesNotContain(api.Calls, c => c.Route.StartsWith("PUT ", StringComparison.Ordinal));
    }
}
