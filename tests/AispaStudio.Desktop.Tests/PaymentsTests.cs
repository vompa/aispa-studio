using System.Text.Json;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;
using AispaStudio.Desktop.ViewModels;

namespace AispaStudio.Desktop.Tests;

/// <summary>R05 Slice 96 (Ä191, ADR 0084, ADR 0054): Zahlungskonto als native Maske; Schlüssel gehen nur hin und stehen nur maskiert da.</summary>
public sealed class PaymentsTests
{
    internal static readonly PaymentAccountDto OwnKeys = new("Stripe", true, false, PaymentAccountMode.OwnKeys, true,
        "Online-Zahlungen laufen über das Stripe-Konto „Day-Spa Wien“.", "Day-Spa Wien", null, false, false, false, null, "sk_live_…wxyz", true, true,
        "https://spa.example/api/payments/stripe/webhook/t1", null);

    internal static readonly PaymentAccountDto Nothing = new("Stripe", true, false, PaymentAccountMode.None, false,
        "Noch kein Zahlungskonto eingerichtet.", null, null, false, false, false, null, null, false, false, null, null);

    private static StubApi Api(PaymentAccountDto account) => new StubApi()
        .On("GET /api/tenant/payments", account)
        .OnJson("PUT /api/tenant/payments", body =>
        {
            var input = JsonSerializer.Deserialize<PaymentAccountInput>(body!, ApiClient.Json)!;
            return account with { Mode = input.Mode, Status = "Gespeichert." };
        });

    private static async Task<PaymentsViewModel> Loaded(StubApi api)
    {
        var payments = new PaymentsViewModel(api.Client()) { PollDelay = TimeSpan.Zero };
        await payments.LoadAsync();
        Assert.True(payments.IsLoaded);
        return payments;
    }

    [Fact]
    public async Task Given_hinterlegter_Schluessel_When_die_Maske_geladen_wird_Then_nur_Punkte_und_die_letzten_vier_Zeichen()
    {
        var payments = await Loaded(Api(OwnKeys));

        Assert.True(payments.IsOwnKeys);
        Assert.Equal("Hinterlegt: ••••wxyz", payments.SecretKeyPlaceholder);
        Assert.DoesNotContain("sk_live", payments.SecretKeyPlaceholder, StringComparison.Ordinal);
        Assert.Equal("", payments.SecretKey);
        Assert.Equal("Hinterlegt", payments.WebhookSecretPlaceholder);
        Assert.Equal("", payments.WebhookSecret);
        Assert.False(payments.ShowTestKeyWarning);
        Assert.False(payments.HasUnsavedChanges);
    }

    [Fact]
    public async Task Given_eigene_Schluessel_When_ohne_neuen_Schluessel_gespeichert_Then_geht_kein_Schluessel_an_die_API()
    {
        var api = Api(OwnKeys with { Mode = PaymentAccountMode.None });
        var payments = await Loaded(api);

        payments.IsOwnKeys = true;
        Assert.True(payments.HasUnsavedChanges);
        Assert.True(await payments.SaveChangesAsync());

        var put = Assert.Single(api.Calls, c => c.Route == "PUT /api/tenant/payments");
        Assert.Equal(new PaymentAccountInput(PaymentAccountMode.OwnKeys, null, null), JsonSerializer.Deserialize<PaymentAccountInput>(put.Body!, ApiClient.Json));
    }

    [Fact]
    public async Task Given_neuer_Schluessel_When_gespeichert_Then_PUT_mit_Schluessel_und_das_Feld_ist_wieder_leer()
    {
        var api = Api(OwnKeys);
        var payments = await Loaded(api);

        payments.SecretKey = "sk_live_neu1234";
        Assert.True(payments.HasUnsavedChanges);
        Assert.True(await payments.SaveChangesAsync());

        var put = Assert.Single(api.Calls, c => c.Route == "PUT /api/tenant/payments");
        Assert.Equal(new PaymentAccountInput(PaymentAccountMode.OwnKeys, "sk_live_neu1234", null),
            JsonSerializer.Deserialize<PaymentAccountInput>(put.Body!, ApiClient.Json));
        Assert.Equal("", payments.SecretKey);
        Assert.False(payments.HasUnsavedChanges);
        Assert.Equal("Gespeichert.", payments.StatusText);
    }

    [Fact]
    public async Task Given_kein_Konto_und_Connect_verfuegbar_When_geladen_Then_ist_Connect_vorgewaehlt_und_nichts_ungespeichert()
    {
        var payments = await Loaded(Api(Nothing));

        Assert.True(payments.IsConnect);
        Assert.True(payments.ShowConnect);
        Assert.False(payments.ShowSaveBar);
        Assert.False(payments.HasUnsavedChanges);
        Assert.Equal("Mit Stripe verbinden", payments.ConnectButtonText);
    }

    [Fact]
    public async Task Given_Connect_gewaehlt_When_verbunden_Then_fragt_die_Maske_bis_der_Link_da_ist()
    {
        var preparing = Nothing with { Mode = PaymentAccountMode.Connect, ConnectPreparing = true, Status = "Das Konto wird vorbereitet." };
        var linked = preparing with
        {
            ConnectPreparing = false,
            ConnectAccountId = "acct_123",
            ConnectOnboardingUrl = "https://connect.stripe.com/setup/abc",
            Status = "Einrichtung bei Stripe offen.",
        };
        var asked = 0;
        var api = Api(Nothing).On("POST /api/tenant/payments/connect", preparing, System.Net.HttpStatusCode.Accepted);
        var payments = await Loaded(api);
        api.OnJson("GET /api/tenant/payments", _ => ++asked < 2 ? preparing : linked);

        await payments.ConnectAsync();

        Assert.Equal(2, asked);
        Assert.Equal("https://connect.stripe.com/setup/abc", payments.Account?.ConnectOnboardingUrl);
        Assert.True(payments.CanOpenStripe);
        Assert.Equal("Einrichtung bei Stripe fortsetzen", payments.ConnectButtonText);
        Assert.Equal("Konto: ohne Namen (acct_123)", payments.ConnectAccountLine);
        Uri? opened = null;
        payments.OpenRequested += uri => opened = uri;
        payments.OpenStripeCommand.Execute(null);
        Assert.Equal(new Uri("https://connect.stripe.com/setup/abc"), opened);
    }

    [Fact]
    public async Task Given_Konto_mit_eigenen_Schluesseln_When_Keine_gewaehlt_Then_Warnung_zum_Wechsel()
    {
        var payments = await Loaded(Api(OwnKeys));
        Assert.False(payments.IsSwitching);

        payments.IsNone = true;

        Assert.True(payments.IsSwitching);
        Assert.True(payments.HasUnsavedChanges);
        payments.Discard();
        Assert.True(payments.IsOwnKeys);
        Assert.False(payments.IsSwitching);
    }

    [Fact]
    public void Maske_zeigt_nur_die_letzten_vier_Zeichen()
    {
        Assert.Equal("••••a1b2", PaymentsViewModel.Mask("sk_test_…a1b2"));
        Assert.Null(PaymentsViewModel.Mask(null));
    }
}
