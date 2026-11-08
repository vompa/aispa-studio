using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using AispaStudio.Api.Data;
using AispaStudio.Api.Online;
using AispaStudio.Contracts;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AispaStudio.Api.Tests;

/// <summary>Zahlungskonto des Spas (ADR 0054): Stripe Connect als Standard, eigene Schlüssel als Alternative.</summary>
public sealed class PaymentAccountTests(ApiFixture fixture)
{
    private const string PlatformKey = "sk_test_plattform0001";
    private const string ConnectSecret = "whsec_connect_aispa";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>Spielt Stripe: Checkout, eigene Konten (je Schlüssel) und Connect-Konten der Plattform.</summary>
    private sealed class FakeStripe : HttpMessageHandler
    {
        public ConcurrentQueue<(string Method, string Path, string? Auth, string? Account, Dictionary<string, string> Form)> Requests { get; } = new();

        /// <summary>Ob das Connect-Konto schon Zahlungen annimmt.</summary>
        public volatile bool ChargesEnabled;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
            var form = body.Length == 0
                ? []
                : body.Split('&').Select(p => p.Split('=', 2)).ToDictionary(p => Uri.UnescapeDataString(p[0]), p => Uri.UnescapeDataString(p[1].Replace('+', ' ')));
            var path = request.RequestUri!.AbsolutePath;
            var auth = request.Headers.Authorization?.Parameter;
            Requests.Enqueue((request.Method.Method, path, auth, request.Headers.TryGetValues("Stripe-Account", out var a) ? a.Single() : null, form));
            object answer = path switch
            {
                "/v1/account" when auth == "sk_test_falsch000000" => null!,
                "/v1/account" => new { id = "acct_own", business_profile = new { name = $"Konto {auth![^4..]}" }, charges_enabled = true },
                "/v1/accounts" => new { id = "acct_spa" },
                "/v1/accounts/acct_spa" => new
                {
                    id = "acct_spa",
                    charges_enabled = ChargesEnabled,
                    details_submitted = ChargesEnabled,
                    business_profile = new { name = "Spa über Connect" }
                },
                "/v1/account_links" => new { url = "https://connect.stripe.com/setup/s/acct_spa", expires_at = DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeSeconds() },
                "/v1/checkout/sessions" => new { id = $"cs_{Guid.Parse(form["client_reference_id"]):N}", url = "https://checkout.stripe.com/c/pay/x" },
                _ => throw new InvalidOperationException(path),
            };
            return answer is null
                ? new HttpResponseMessage(HttpStatusCode.Unauthorized) { Content = JsonContent.Create(new { error = new { message = "Invalid API Key" } }) }
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(answer) };
        }
    }

    private WebApplicationFactory<Program> Host(FakeStripe stripe, bool connect) =>
        fixture.Factory.WithWebHostBuilder(b =>
        {
            b.UseSetting("OnlineVouchers:PaymentProvider", "Stripe");
            if (connect)
            {
                b.UseSetting("OnlineVouchers:Stripe:Connect", "true");
                b.UseSetting("OnlineVouchers:Stripe:SecretKey", PlatformKey);
                b.UseSetting("OnlineVouchers:Stripe:ConnectWebhookSecret", ConnectSecret);
            }

            b.ConfigureTestServices(s => s.AddHttpClient(StripePaymentProvider.HttpClientName).ConfigurePrimaryHttpMessageHandler(() => stripe));
        });

    private static HttpClient Staff(WebApplicationFactory<Program> host, TestSpa spa)
    {
        var client = host.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        client.DefaultRequestHeaders.Add("Cookie", spa.Cookie);
        return client;
    }

    private static HttpClient Online(WebApplicationFactory<Program> host, TestSpa spa)
    {
        var client = host.CreateClient();
        client.DefaultRequestHeaders.Add("X-Aispa-Spa", spa.TenantId.ToString());
        return client;
    }

    private static async Task<PaymentAccountDto> WaitForAsync(HttpClient staff, Func<PaymentAccountDto, bool> done)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));
        while (true)
        {
            var account = (await staff.GetFromJsonAsync<PaymentAccountDto>("/api/tenant/payments", TestSpa.Json, Ct))!;
            if (done(account))
            {
                return account;
            }

            await Task.Delay(50, timeout.Token);
        }
    }

    private static async Task<VoucherOrderStarted> OrderVoucherAsync(HttpClient online, TestSpa spa)
    {
        var response = await online.PostAsJsonAsync("/api/online/vouchers/orders", new VoucherOrderRequest(spa.Location.Id, VoucherKind.MultiPurpose, 50m, null,
            new OnlineContact("Gerda", "Schenk", $"zahlung-{Guid.NewGuid():N}@example.at", null), null, null, true, "de"), TestSpa.Json, Ct);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var started = await TestSpa.ReadAsync<VoucherOrderStarted>(response, Ct);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));
        while ((await online.GetFromJsonAsync<VoucherOrderDto>($"/api/online/vouchers/orders/{started.Token}", TestSpa.Json, Ct))!.PaymentUrl is null)
        {
            await Task.Delay(50, timeout.Token);
        }

        return started;
    }

    private static async Task<HttpResponseMessage> PostWebhookAsync(HttpClient client, string path, string secret, object payload)
    {
        var body = JsonSerializer.Serialize(payload);
        using var content = new StringContent(body, Encoding.UTF8, "application/json");
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = content };
        request.Headers.Add("Stripe-Signature", StripeWebhook.Sign(secret, body, DateTimeOffset.UtcNow));
        return await client.SendAsync(request, Ct);
    }

    private static object Paid(Guid orderId) => new
    {
        id = "evt_paid",
        type = "checkout.session.completed",
        data = new { @object = new { id = $"cs_{orderId:N}", client_reference_id = orderId, payment_status = "paid" } },
    };

    private static async Task<TestSpa> SpaWithRegisterAsync(ApiFixture fixture)
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await spa.PostAsync<CashRegisterDto>("/api/pos/registers", new CashRegisterInput(
            spa.Location.Id, $"S-{Guid.NewGuid():N}"[..14], FiscalCountry.AT, "Test-Spa GmbH\nWien", "Test-Spa GmbH", "AT61 1904 3002 3457 3201", null), Ct);
        return spa;
    }

    private async Task<VoucherOrderStatus> StatusAsync(Guid orderId)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<OnlineDbContext>().VoucherOrders.Where(o => o.Id == orderId).Select(o => o.Status).FirstAsync(Ct);
    }

    [Fact]
    public async Task Eigene_Schluessel_werden_verschluesselt_gespeichert_und_bezahlen_auf_das_Konto_des_Spas()
    {
        var spa = await SpaWithRegisterAsync(fixture);
        var other = await SpaWithRegisterAsync(fixture);
        var stripe = new FakeStripe();
        await using var host = Host(stripe, connect: false);
        using var staff = Staff(host, spa);
        using var otherStaff = Staff(host, other);
        using var online = Online(host, spa);

        var empty = (await staff.GetFromJsonAsync<PaymentAccountDto>("/api/tenant/payments", TestSpa.Json, Ct))!;
        Assert.Equal(("Stripe", false, false, PaymentAccountMode.None, false), (empty.Provider, empty.ConnectAvailable, empty.ServerAccount, empty.Mode, empty.Ready));
        Assert.False((await online.GetFromJsonAsync<OnlineVoucherOffer>($"/api/online/locations/{spa.Location.Id}/vouchers", TestSpa.Json, Ct))!.Available);

        // Ohne Schlüssel, mit falschem Format oder Connect ohne Plattform: abgelehnt.
        Assert.Equal(HttpStatusCode.BadRequest, (await staff.PutAsJsonAsync("/api/tenant/payments", new PaymentAccountInput(PaymentAccountMode.OwnKeys), TestSpa.Json, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await staff.PutAsJsonAsync("/api/tenant/payments",
            new PaymentAccountInput(PaymentAccountMode.OwnKeys, "pk_test_oeffentlich01", "whsec_x"), TestSpa.Json, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await staff.PutAsJsonAsync("/api/tenant/payments", new PaymentAccountInput(PaymentAccountMode.Connect), TestSpa.Json, Ct)).StatusCode);

        const string Key = "sk_test_eigenes_konto_a1b2";
        const string Secret = "whsec_eigenes_spa";
        var response = await staff.PutAsJsonAsync("/api/tenant/payments", new PaymentAccountInput(PaymentAccountMode.OwnKeys, Key, Secret), TestSpa.Json, Ct);
        var text = await response.Content.ReadAsStringAsync(Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain(Key, text, StringComparison.Ordinal);
        Assert.DoesNotContain(Secret, text, StringComparison.Ordinal);
        var saved = await WaitForAsync(staff, a => a.AccountName is not null);
        Assert.Equal(("sk_test_…a1b2", false, true, true), (saved.SecretKeyHint, saved.LiveMode, saved.HasWebhookSecret, saved.Ready));
        Assert.Equal("Konto a1b2", saved.AccountName);
        Assert.EndsWith($"/api/hooks/payments/stripe/{spa.TenantId}", saved.WebhookUrl, StringComparison.Ordinal);
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var stored = await scope.ServiceProvider.GetRequiredService<AispaDbContext>().PaymentAccounts.IgnoreQueryFilters()
                .SingleAsync(a => EF.Property<Guid>(a, "TenantId") == spa.TenantId, Ct);
            Assert.DoesNotContain(Key, stored.ProtectedSecretKey!, StringComparison.Ordinal);
            Assert.DoesNotContain(Secret, stored.ProtectedWebhookSecret!, StringComparison.Ordinal);
        }

        // Gutschein: Checkout mit dem Schlüssel des Spas, ohne Stripe-Account.
        Assert.True((await online.GetFromJsonAsync<OnlineVoucherOffer>($"/api/online/locations/{spa.Location.Id}/vouchers", TestSpa.Json, Ct))!.Available);
        var order = await OrderVoucherAsync(online, spa);
        var checkout = Assert.Single(stripe.Requests, r => r.Path == "/v1/checkout/sessions");
        Assert.Equal((Key, (string?)null), (checkout.Auth, checkout.Account));

        // Das zweite Spa hat eigene Schlüssel; mit ihnen lässt sich die Zahlung des ersten nicht melden.
        await otherStaff.PutAsJsonAsync("/api/tenant/payments", new PaymentAccountInput(PaymentAccountMode.OwnKeys, "sk_test_anderes_spa_9999", "whsec_anderes"), TestSpa.Json, Ct);
        Assert.Equal(HttpStatusCode.OK, (await PostWebhookAsync(online, $"/api/hooks/payments/stripe/{other.TenantId}", "whsec_anderes", Paid(order.OrderId))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await PostWebhookAsync(online, $"/api/hooks/payments/stripe/{spa.TenantId}", "whsec_anderes", Paid(order.OrderId))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await PostWebhookAsync(online, "/api/hooks/payments/stripe", Secret, Paid(order.OrderId))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await PostWebhookAsync(online, $"/api/hooks/payments/stripe/{Guid.NewGuid()}", Secret, Paid(order.OrderId))).StatusCode);
        Assert.Equal(VoucherOrderStatus.Pending, await StatusAsync(order.OrderId));

        Assert.Equal(HttpStatusCode.OK, (await PostWebhookAsync(online, $"/api/hooks/payments/stripe/{spa.TenantId}", Secret, Paid(order.OrderId))).StatusCode);
        Assert.NotEqual(VoucherOrderStatus.Pending, await StatusAsync(order.OrderId));

        // Ein Schlüssel, den Stripe nicht kennt: das Konto ist nicht bereit, online gibt es keine Gutscheine.
        await staff.PutAsJsonAsync("/api/tenant/payments", new PaymentAccountInput(PaymentAccountMode.OwnKeys, "sk_test_falsch000000"), TestSpa.Json, Ct);
        var wrong = await WaitForAsync(staff, a => a.Error is not null);
        Assert.False(wrong.Ready);
        Assert.Equal("Stripe kennt diesen Schlüssel nicht.", wrong.Error);
        Assert.False((await online.GetFromJsonAsync<OnlineVoucherOffer>($"/api/online/locations/{spa.Location.Id}/vouchers", TestSpa.Json, Ct))!.Available);
    }

    [Fact]
    public async Task Stripe_Connect_richtet_das_Konto_ueber_die_Queue_ein_und_bezahlt_direkt_beim_Spa()
    {
        var spa = await SpaWithRegisterAsync(fixture);
        await spa.PutAsync<LocationOnlineRules>($"/api/locations/{spa.Location.Id}/online-rules", new LocationOnlineRules(true, 60, 365, 24, 15, null, false, 50), Ct);
        var stripe = new FakeStripe();
        await using var host = Host(stripe, connect: true);
        using var staff = Staff(host, spa);
        using var online = Online(host, spa);

        var start = (await staff.GetFromJsonAsync<PaymentAccountDto>("/api/tenant/payments", TestSpa.Json, Ct))!;
        Assert.Equal((true, false, false), (start.ConnectAvailable, start.ServerAccount, start.Ready));
        // Ohne Konto: keine Gutscheine, keine Ausfallgebühr (der Schlüssel der Plattform zahlt nie selbst).
        Assert.False((await online.GetFromJsonAsync<OnlineVoucherOffer>($"/api/online/locations/{spa.Location.Id}/vouchers", TestSpa.Json, Ct))!.Available);
        Assert.Equal(0, (await online.GetFromJsonAsync<List<OnlineLocation>>("/api/online/locations", TestSpa.Json, Ct))!.Single().NoShowFeePercent);

        Assert.Equal(HttpStatusCode.Accepted, (await staff.PostAsync("/api/tenant/payments/connect", null, Ct)).StatusCode);
        var linked = await WaitForAsync(staff, a => !a.ConnectPreparing);
        Assert.Equal(("acct_spa", "https://connect.stripe.com/setup/s/acct_spa", false), (linked.ConnectAccountId, linked.ConnectOnboardingUrl, linked.Ready));
        var create = Assert.Single(stripe.Requests, r => r.Path == "/v1/accounts");
        Assert.Equal((PlatformKey, "standard", "AT"), (create.Auth, create.Form["type"], create.Form["country"]));
        Assert.EndsWith("/anmelden/?zahlungen=1", Assert.Single(stripe.Requests, r => r.Path == "/v1/account_links").Form["return_url"], StringComparison.Ordinal);

        // Ein zweites Verbinden legt kein zweites Konto an.
        await staff.PostAsync("/api/tenant/payments/connect", null, Ct);
        await WaitForAsync(staff, a => !a.ConnectPreparing);
        Assert.Single(stripe.Requests, r => r.Path == "/v1/accounts");

        // Stripe meldet die abgeschlossene Einrichtung über den Connect-Webhook.
        stripe.ChargesEnabled = true;
        var updated = new { id = "evt_acct", type = "account.updated", account = "acct_spa", data = new { @object = new { id = "acct_spa", charges_enabled = true } } };
        Assert.Equal(HttpStatusCode.BadRequest, (await PostWebhookAsync(online, "/api/hooks/payments/stripe", "whsec_falsch", updated)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await PostWebhookAsync(online, "/api/hooks/payments/stripe", ConnectSecret, updated)).StatusCode);
        var ready = await WaitForAsync(staff, a => a.Ready);
        Assert.Equal(("Spa über Connect", (string?)null), (ready.AccountName, ready.ConnectOnboardingUrl));
        Assert.Equal(50, (await online.GetFromJsonAsync<List<OnlineLocation>>("/api/online/locations", TestSpa.Json, Ct))!.Single().NoShowFeePercent);

        // Direkte Zahlung auf dem verbundenen Konto.
        var order = await OrderVoucherAsync(online, spa);
        var checkout = Assert.Single(stripe.Requests, r => r.Path == "/v1/checkout/sessions");
        Assert.Equal((PlatformKey, "acct_spa"), (checkout.Auth, checkout.Account));
        Assert.Equal(HttpStatusCode.OK, (await PostWebhookAsync(online, "/api/hooks/payments/stripe", ConnectSecret, Paid(order.OrderId))).StatusCode);
        Assert.NotEqual(VoucherOrderStatus.Pending, await StatusAsync(order.OrderId));
    }

    [Fact]
    public async Task Nur_wer_das_Unternehmen_verwaltet_sieht_das_Zahlungskonto()
    {
        using var client = fixture.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/tenant/payments", Ct)).StatusCode);
    }

    [Fact]
    public void Hinweis_zeigt_nur_Anfang_und_Ende_des_Schluessels()
    {
        Assert.Equal("sk_live_…wxyz", PaymentAccounts.Hint("sk_live_abcdefghijklmnopwxyz"));
        Assert.Equal("rk_test_…1234", PaymentAccounts.Hint("rk_test_abcdef1234"));
    }
}
