using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using AispaStudio.Api.Online;
using AispaStudio.Contracts;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace AispaStudio.Api.Tests;

public sealed class StripePaymentTests(ApiFixture fixture)
{
    private const string Secret = "whsec_test_aispa";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static DateOnly ViennaToday =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna")).DateTime);

    /// <summary>Spielt Stripe: legt je Bestellung eine Checkout-Sitzung „cs_{Bestellung}“ an und merkt sich die Formulare.</summary>
    private sealed class FakeStripe : HttpMessageHandler
    {
        public ConcurrentQueue<(Uri Uri, string? Auth, string? Idempotency, Dictionary<string, string> Form)> Requests { get; } = new();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var form = (await request.Content!.ReadAsStringAsync(cancellationToken)).Split('&')
                .Select(p => p.Split('=', 2)).ToDictionary(p => Uri.UnescapeDataString(p[0]), p => Uri.UnescapeDataString(p[1].Replace('+', ' ')));
            Requests.Enqueue((request.RequestUri!, request.Headers.Authorization?.ToString(),
                request.Headers.TryGetValues("Idempotency-Key", out var keys) ? keys.Single() : null, form));
            var id = $"cs_{Guid.Parse(form["client_reference_id"]):N}";
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new { id, url = $"https://checkout.stripe.com/c/pay/{id}" }),
            };
        }
    }

    private static string Event(string type, string sessionId, Guid orderId, string paymentStatus = "paid") =>
        JsonSerializer.Serialize(new
        {
            id = "evt_1",
            type,
            data = new { @object = new { id = sessionId, client_reference_id = orderId, payment_status = paymentStatus } },
        });

    private static async Task<HttpResponseMessage> PostWebhookAsync(HttpClient client, string body, string? signature = null)
    {
        using var content = new StringContent(body, Encoding.UTF8, "application/json");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/hooks/payments/stripe") { Content = content };
        request.Headers.Add("Stripe-Signature", signature ?? StripeWebhook.Sign(Secret, body, DateTimeOffset.UtcNow));
        return await client.SendAsync(request, Ct);
    }

    private static async Task<VoucherOrderDto> WaitForAsync(HttpClient client, string token, Func<VoucherOrderDto, bool> done)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));
        while (true)
        {
            var order = (await client.GetFromJsonAsync<VoucherOrderDto>($"/api/online/vouchers/orders/{token}", TestSpa.Json, Ct))!;
            if (done(order))
            {
                return order;
            }

            await Task.Delay(50, timeout.Token);
        }
    }

    [Fact]
    public async Task Stripe_Checkout_bezahlt_ueber_den_Webhook_und_die_Kassa_stellt_aus()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var register = await spa.PostAsync<CashRegisterDto>("/api/pos/registers", new CashRegisterInput(
            spa.Location.Id, $"S-{Guid.NewGuid():N}"[..14], FiscalCountry.AT, "Test-Spa GmbH\nWien", "Test-Spa GmbH", "AT61 1904 3002 3457 3201", null), Ct);
        var stripe = new FakeStripe();
        await using var host = fixture.Factory.WithWebHostBuilder(b =>
        {
            b.UseSetting("OnlineVouchers:PaymentProvider", "Stripe");
            b.UseSetting("OnlineVouchers:Stripe:SecretKey", "sk_test_aispa");
            b.UseSetting("OnlineVouchers:Stripe:WebhookSecret", Secret);
            b.ConfigureTestServices(s => s.AddHttpClient(StripePaymentProvider.HttpClientName).ConfigurePrimaryHttpMessageHandler(() => stripe));
        });
        using var client = host.CreateClient();
        client.DefaultRequestHeaders.Add("X-Aispa-Spa", spa.TenantId.ToString());
        var email = $"stripe-{Guid.NewGuid():N}@example.at";

        var offer = await client.GetFromJsonAsync<OnlineVoucherOffer>($"/api/online/locations/{spa.Location.Id}/vouchers", TestSpa.Json, Ct);
        Assert.True(offer!.Available);
        Assert.False(offer.TestPayment);
        var response = await client.PostAsJsonAsync("/api/online/vouchers/orders", new VoucherOrderRequest(
            spa.Location.Id, VoucherKind.MultiPurpose, 50m, null, new OnlineContact("Gerda", "Schenk", email, null), "Mama", null, true, "de"), TestSpa.Json, Ct);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var started = await TestSpa.ReadAsync<VoucherOrderStarted>(response, Ct);
        Assert.EndsWith($"#/gutschein/{started.Token}", started.PaymentUrl, StringComparison.Ordinal);

        var pending = await WaitForAsync(client, started.Token, o => o.PaymentUrl is not null);
        var session = $"cs_{started.OrderId:N}";
        Assert.Equal($"https://checkout.stripe.com/c/pay/{session}", pending.PaymentUrl);
        var call = Assert.Single(stripe.Requests);
        Assert.Equal("https://api.stripe.com/v1/checkout/sessions", call.Uri.ToString());
        Assert.Equal("Bearer sk_test_aispa", call.Auth);
        Assert.Equal($"aispa-voucher-{started.OrderId:N}", call.Idempotency);
        Assert.Equal(("5000", "eur", email), (call.Form["line_items[0][price_data][unit_amount]"], call.Form["line_items[0][price_data][currency]"], call.Form["customer_email"]));
        Assert.Equal(started.PaymentUrl, call.Form["success_url"]);

        // Falsche Signatur, fremde Sitzung und noch nicht bezahlte Sitzung stellen nichts aus.
        var paid = Event("checkout.session.completed", session, started.OrderId);
        Assert.Equal(HttpStatusCode.BadRequest, (await PostWebhookAsync(client, paid, StripeWebhook.Sign("whsec_falsch", paid, DateTimeOffset.UtcNow))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await PostWebhookAsync(client, Event("checkout.session.completed", "cs_fremd", started.OrderId))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await PostWebhookAsync(client, Event("checkout.session.completed", session, started.OrderId, "unpaid"))).StatusCode);
        Assert.Equal(VoucherOrderStatus.Pending, (await WaitForAsync(client, started.Token, _ => true)).Status);

        Assert.Equal(HttpStatusCode.OK, (await PostWebhookAsync(client, paid)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await PostWebhookAsync(client, paid)).StatusCode);

        var issued = await WaitForAsync(client, started.Token, o => o.Status == VoucherOrderStatus.Issued);
        Assert.NotNull(issued.Code);
        Assert.Null(issued.PaymentUrl);
        var receipts = await spa.GetAsync<List<ReceiptDto>>($"/api/pos/registers/{register.Id}/receipts?date={ViennaToday:yyyy-MM-dd}", Ct);
        var sale = Assert.Single(receipts, r => r.IssuedVouchers.Count > 0);
        Assert.Equal($"Online Stripe {session}", sale.Payments[0].Reference);
        await fixture.Mailbox.WaitForAsync(email, m => m.Subject.StartsWith("Ihr Gutschein", StringComparison.Ordinal), Ct);

        // Abgelaufene Sitzung einer zweiten Bestellung: storniert, nichts ausgestellt.
        var second = await TestSpa.ReadAsync<VoucherOrderStarted>(await client.PostAsJsonAsync("/api/online/vouchers/orders", new VoucherOrderRequest(
            spa.Location.Id, VoucherKind.MultiPurpose, 25m, null, new OnlineContact("Gerda", "Schenk", email, null), null, null, true, "de"), TestSpa.Json, Ct), Ct);
        await WaitForAsync(client, second.Token, o => o.PaymentUrl is not null);
        Assert.Equal(HttpStatusCode.OK, (await PostWebhookAsync(client, Event("checkout.session.expired", $"cs_{second.OrderId:N}", second.OrderId, "unpaid"))).StatusCode);
        Assert.Equal(VoucherOrderStatus.Cancelled, (await WaitForAsync(client, second.Token, o => o.Status != VoucherOrderStatus.Pending)).Status);
    }

    [Fact]
    public async Task Ohne_Online_Zahlung_gibt_es_keinen_Webhook()
    {
        await using var host = fixture.Factory.WithWebHostBuilder(b => b.UseSetting("OnlineVouchers:PaymentProvider", "None"));
        using var client = host.CreateClient();

        var response = await PostWebhookAsync(client, Event("checkout.session.completed", "cs_x", Guid.NewGuid()));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>Server mit eigenem Stripe-Konto (Einzelbetrieb); ein Spa ohne Zahlungskonto zahlt darüber.</summary>
    private WebApplicationFactory<Program> ServerAccountHost(HttpMessageHandler stripe) =>
        fixture.Factory.WithWebHostBuilder(b =>
        {
            b.UseSetting("OnlineVouchers:PaymentProvider", "Stripe");
            b.UseSetting("OnlineVouchers:Stripe:SecretKey", "sk_test_aispa");
            b.UseSetting("OnlineVouchers:Stripe:WebhookSecret", Secret);
            b.ConfigureTestServices(s => s.AddHttpClient(StripePaymentProvider.HttpClientName).ConfigurePrimaryHttpMessageHandler(() => stripe));
        });

    private static IOnlinePaymentProvider Stripe(WebApplicationFactory<Program> host) =>
        host.Services.GetServices<IOnlinePaymentProvider>().Single(p => p.Name == StripePaymentProvider.ProviderName);

    [Fact]
    public void Signatur_wird_mit_Zeitstempel_und_Rohdaten_geprueft()
    {
        var now = DateTimeOffset.UtcNow;
        var body = Event("checkout.session.completed", "cs_1", Guid.NewGuid());
        var bytes = Encoding.UTF8.GetBytes(body);
        var signature = StripeWebhook.Sign(Secret, body, now);
        var tolerance = TimeSpan.FromMinutes(5);

        Assert.True(StripeWebhook.IsValid(Secret, bytes, signature, now, tolerance));
        Assert.True(StripeWebhook.IsValid(Secret, bytes, $"{signature},v1=00ff", now, tolerance));
        Assert.False(StripeWebhook.IsValid(Secret, Encoding.UTF8.GetBytes(body.Replace("cs_1", "cs_2", StringComparison.Ordinal)), signature, now, tolerance));
        Assert.False(StripeWebhook.IsValid(Secret, bytes, signature, now.AddMinutes(6), tolerance));
        Assert.False(StripeWebhook.IsValid("anders", bytes, signature, now, tolerance));
        Assert.False(StripeWebhook.IsValid(Secret, bytes, "v1=abc", now, tolerance));
        Assert.False(StripeWebhook.IsValid(Secret, bytes, null, now, tolerance));
    }

    [Fact]
    public void Ereignisse_werden_als_bezahlt_oder_abgebrochen_gelesen()
    {
        var order = Guid.NewGuid();

        WebhookOutcome Read(string json) => StripeWebhook.Read(Encoding.UTF8.GetBytes(json));

        Assert.Equal(new PaymentNotification(order, "cs_1", PaymentResult.Paid),
            Assert.IsType<WebhookOutcome.Notified>(Read(Event("checkout.session.completed", "cs_1", order))).Notification);
        Assert.Equal(PaymentResult.Paid,
            Assert.IsType<WebhookOutcome.Notified>(Read(Event("checkout.session.async_payment_succeeded", "cs_1", order, "unpaid"))).Notification.Result);
        Assert.Equal(PaymentResult.Failed,
            Assert.IsType<WebhookOutcome.Notified>(Read(Event("checkout.session.expired", "cs_1", order, "unpaid"))).Notification.Result);
        Assert.IsType<WebhookOutcome.Ignored>(Read(Event("checkout.session.completed", "cs_1", order, "unpaid")));
        Assert.IsType<WebhookOutcome.Ignored>(Read(Event("charge.refunded", "cs_1", order)));
        Assert.IsType<WebhookOutcome.Invalid>(Read("{\"kein\":\"stripe\"}"));
        Assert.Equal(1999, StripePaymentProvider.MinorUnits(19.99m));
    }
    /// <summary>Spielt Stripe für die Kartenhinterlegung: Kunde, Checkout im Modus „setup“, SetupIntent und Belastung.</summary>
    private sealed class FakeStripeCards : HttpMessageHandler
    {
        public ConcurrentQueue<(string Path, string? Idempotency, Dictionary<string, string> Form)> Requests { get; } = new();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
            var form = body.Length == 0
                ? []
                : body.Split('&').Select(p => p.Split('=', 2)).ToDictionary(p => Uri.UnescapeDataString(p[0]), p => Uri.UnescapeDataString(p[1].Replace('+', ' ')));
            var path = request.RequestUri!.AbsolutePath;
            Requests.Enqueue((path, request.Headers.TryGetValues("Idempotency-Key", out var keys) ? keys.Single() : null, form));
            if (path == "/v1/customers/cus_gone")
            {
                return new HttpResponseMessage(HttpStatusCode.NotFound) { Content = JsonContent.Create(new { error = new { message = "No such customer" } }) };
            }

            object? answer = path switch
            {
                "/v1/customers" => new { id = "cus_1" },
                "/v1/checkout/sessions" => new { id = "cs_setup_1", url = "https://checkout.stripe.com/c/setup/cs_setup_1" },
                "/v1/setup_intents/seti_1" => new { id = "seti_1", payment_method = "pm_1" },
                "/v1/payment_intents" => new { id = "pi_1", status = "succeeded" },
                "/v1/checkout/sessions/cs_pay_1" => new { id = "cs_pay_1", payment_intent = "pi_voucher" },
                "/v1/refunds" when form["payment_intent"] == "pi_declined" => null,
                "/v1/refunds" => new { id = "re_1", status = "succeeded" },
                "/v1/customers/cus_1" when request.Method == HttpMethod.Delete => new { id = "cus_1", deleted = true },
                "/v1/payment_methods/pm_1/detach" => new { id = "pm_1" },
                _ => throw new InvalidOperationException(path),
            };
            return answer is null
                ? new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = JsonContent.Create(new { error = new { message = "Charge already refunded." } }) }
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(answer) };
        }
    }

    [Fact]
    public async Task Karte_ueber_Stripe_hinterlegen_und_Ausfallgebuehr_belasten()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await spa.PostAsync<CashRegisterDto>("/api/pos/registers", new CashRegisterInput(
            spa.Location.Id, $"S-{Guid.NewGuid():N}"[..14], FiscalCountry.AT, "Test-Spa GmbH\nWien", "Test-Spa GmbH", "AT61 1904 3002 3457 3201", null), Ct);
        await spa.PutAsync<LocationOnlineRules>($"/api/locations/{spa.Location.Id}/online-rules", new LocationOnlineRules(true, 60, 365, 24, 15, null, false, 100), Ct);
        var stripe = new FakeStripeCards();
        await using var host = fixture.Factory.WithWebHostBuilder(b =>
        {
            b.UseSetting("OnlineVouchers:PaymentProvider", "Stripe");
            b.UseSetting("OnlineVouchers:Stripe:SecretKey", "sk_test_aispa");
            b.UseSetting("OnlineVouchers:Stripe:WebhookSecret", Secret);
            b.ConfigureTestServices(s => s.AddHttpClient(StripePaymentProvider.HttpClientName).ConfigurePrimaryHttpMessageHandler(() => stripe));
        });
        using var client = host.CreateClient();
        client.DefaultRequestHeaders.Add("X-Aispa-Spa", spa.TenantId.ToString());
        using var staff = host.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        staff.DefaultRequestHeaders.Add("Cookie", spa.Cookie);

        var response = await client.PostAsJsonAsync("/api/online/bookings", new OnlineBookingRequest(spa.Location.Id, spa.MassageTreatment.Id, TestSpa.At(10),
            new OnlineContact("Nora", "Karte", $"stripe-karte-{Guid.NewGuid():N}@example.at", null), null, true, "de", true), TestSpa.Json, Ct);
        var booked = await TestSpa.ReadAsync<OnlineBookingConfirmation>(response, Ct);
        using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(Ct))
        {
            timeout.CancelAfter(TimeSpan.FromSeconds(15));
            while (true)
            {
                var appointment = await client.GetFromJsonAsync<OnlineAppointment>($"/api/online/bookings/{booked.ManageToken}", TestSpa.Json, Ct);
                if (appointment!.CardGuarantee!.SetupUrl is { } url)
                {
                    Assert.Equal("https://checkout.stripe.com/c/setup/cs_setup_1", url);
                    break;
                }

                await Task.Delay(50, timeout.Token);
            }
        }

        var session = Assert.Single(stripe.Requests, r => r.Path == "/v1/checkout/sessions");
        Assert.Equal(("setup", "cus_1", "card", "eur"), (session.Form["mode"], session.Form["customer"], session.Form["payment_method_types[0]"], session.Form["currency"]));
        Assert.EndsWith($"#/termin/{booked.ManageToken}", session.Form["success_url"], StringComparison.Ordinal);
        var guaranteeId = Guid.Parse(session.Form["client_reference_id"]);
        Assert.Equal($"aispa-guarantee-{guaranteeId:N}-1", session.Idempotency);

        var completed = JsonSerializer.Serialize(new
        {
            id = "evt_2",
            type = "checkout.session.completed",
            data = new { @object = new { id = "cs_setup_1", mode = "setup", client_reference_id = guaranteeId, setup_intent = "seti_1", customer = "cus_1" } },
        });
        Assert.Equal(HttpStatusCode.OK, (await PostWebhookAsync(client, completed)).StatusCode);
        var saved = await client.GetFromJsonAsync<OnlineAppointment>($"/api/online/bookings/{booked.ManageToken}", TestSpa.Json, Ct);
        Assert.Equal(CardGuaranteeStatus.Saved, saved!.CardGuarantee!.Status);
        Assert.Null(saved.CardGuarantee.SetupUrl);

        await spa.PostAsync<AppointmentDto>($"/api/appointments/{booked.Appointment.Id}/status", new AppointmentStatusChange(AppointmentStatus.NoShow), Ct);
        Assert.Equal(HttpStatusCode.OK, (await staff.PostAsync($"/api/appointments/{booked.Appointment.Id}/card-guarantee/charge", null, Ct)).StatusCode);
        using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(Ct))
        {
            timeout.CancelAfter(TimeSpan.FromSeconds(15));
            while ((await staff.GetFromJsonAsync<CardGuaranteeStaffDto>($"/api/appointments/{booked.Appointment.Id}/card-guarantee", TestSpa.Json, Ct))!.Status
                   != CardGuaranteeStatus.Charged)
            {
                await Task.Delay(50, timeout.Token);
            }
        }

        var charge = Assert.Single(stripe.Requests, r => r.Path == "/v1/payment_intents");
        Assert.Equal(("7900", "pm_1", "cus_1", "true", "true"),
            (charge.Form["amount"], charge.Form["payment_method"], charge.Form["customer"], charge.Form["off_session"], charge.Form["confirm"]));
        Assert.Equal($"aispa-noshow-{guaranteeId:N}", charge.Idempotency);

        // Storno an der Kassa erstattet die Gebühr über Stripe.
        var receiptId = (await staff.GetFromJsonAsync<CardGuaranteeStaffDto>($"/api/appointments/{booked.Appointment.Id}/card-guarantee", TestSpa.Json, Ct))!.ReceiptId;
        Assert.Equal(HttpStatusCode.OK, (await staff.PostAsJsonAsync($"/api/pos/receipts/{receiptId}/void", new VoidRequest("Kulanz"), TestSpa.Json, Ct)).StatusCode);
        using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(Ct))
        {
            timeout.CancelAfter(TimeSpan.FromSeconds(15));
            while ((await staff.GetFromJsonAsync<OnlineRefundDto>($"/api/pos/receipts/{receiptId}/online-refund", TestSpa.Json, Ct))!.Status
                   != OnlineRefundStatus.Refunded)
            {
                await Task.Delay(50, timeout.Token);
            }
        }

        var refund = Assert.Single(stripe.Requests, r => r.Path == "/v1/refunds");
        Assert.Equal(("pi_1", "7900"), (refund.Form["payment_intent"], refund.Form["amount"]));
        Assert.StartsWith("aispa-refund-", refund.Idempotency, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Erstattung_eines_Gutscheins_sucht_die_Zahlung_der_Checkout_Sitzung()
    {
        var stripe = new FakeStripeCards();
        await using var host = ServerAccountHost(stripe);
        var payments = Stripe(host);
        var tenantId = Guid.NewGuid();
        var refundId = Guid.NewGuid();

        var refunded = await payments.RefundAsync(new PaymentRefund(tenantId, refundId, "cs_pay_1", 50m, "EUR"), Ct);
        var declined = await payments.RefundAsync(new PaymentRefund(tenantId, Guid.NewGuid(), "pi_declined", 50m, "EUR"), Ct);

        Assert.Equal(new RefundResult(true, "re_1", null), refunded);
        var post = Assert.Single(stripe.Requests, r => r.Path == "/v1/refunds" && r.Form["payment_intent"] == "pi_voucher");
        Assert.Equal(("5000", $"aispa-refund-{refundId:N}"), (post.Form["amount"], post.Idempotency));
        Assert.Equal(new RefundResult(false, null, "Charge already refunded."), declined);
    }

    [Fact]
    public async Task Karte_loeschen_entfernt_den_Kunden_bei_Stripe_oder_haengt_die_Zahlungsart_ab()
    {
        var stripe = new FakeStripeCards();
        await using var host = ServerAccountHost(stripe);
        var payments = Stripe(host);
        var tenantId = Guid.NewGuid();

        await payments.RemoveSavedCardAsync(new SavedCardRemoval(tenantId, Guid.NewGuid(), "seti_1", "cus_1"), Ct);
        var withCustomer = stripe.Requests.Select(r => r.Path).ToList();
        stripe.Requests.Clear();
        await payments.RemoveSavedCardAsync(new SavedCardRemoval(tenantId, Guid.NewGuid(), "seti_1", null), Ct);
        var withoutCustomer = stripe.Requests.Select(r => r.Path).ToList();
        // Schon gelöscht gilt als gelöscht.
        await payments.RemoveSavedCardAsync(new SavedCardRemoval(tenantId, Guid.NewGuid(), "seti_1", "cus_gone"), Ct);

        Assert.Equal(["/v1/customers/cus_1"], withCustomer);
        Assert.Equal(["/v1/setup_intents/seti_1", "/v1/payment_methods/pm_1/detach"], withoutCustomer);
    }
}
