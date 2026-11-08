using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using AispaStudio.Contracts;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace AispaStudio.Api.Tests;

/// <summary>Handy-App für Kunden: späte Absage mit Gebühr aus der API, Termin als .ics, eigene Gutscheine mit Restwert.</summary>
public sealed partial class CustomerAppTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    [GeneratedRegex(@"\d{6}")]
    private static partial Regex SixDigits();

    private static string NewEmail() => $"app-{Guid.NewGuid():N}@example.at";

    private static OnlineBookingRequest Booking(TestSpa spa, DateTimeOffset start, string email, bool acceptFee = false) =>
        new(spa.Location.Id, spa.MassageTreatment.Id, start, new OnlineContact("Nora", "Handy", email, null), null, true, "de", acceptFee);

    private static async Task<OnlineBookingConfirmation> BookAsync(TestSpa spa, HttpClient client, DateTimeOffset start, string email, bool acceptFee = false)
    {
        var response = await client.PostAsJsonAsync("/api/online/bookings", Booking(spa, start, email, acceptFee), TestSpa.Json, Ct);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await TestSpa.ReadAsync<OnlineBookingConfirmation>(response, Ct);
    }

    /// <summary>Gebucht mit 50 % Ausfallgebühr und hinterlegter Karte; die Antwort kommt von einer API, deren Uhr 12 Stunden vor dem Termin steht.</summary>
    private async Task<(OnlineBookingConfirmation Booked, HttpClient Late)> BookWithCardTwelveHoursAheadAsync(TestSpa spa, bool saveCard, int deadlineHours = 24)
    {
        await spa.PutAsync<LocationOnlineRules>($"/api/locations/{spa.Location.Id}/online-rules",
            new LocationOnlineRules(true, 60, 365, deadlineHours, 15, null, false, 50), Ct);
        using var client = spa.OnlineClient();
        var booked = await BookAsync(spa, client, TestSpa.At(10), NewEmail(), acceptFee: true);
        if (saveCard)
        {
            await client.PostAsJsonAsync($"/api/online/bookings/{booked.ManageToken}/card/test", new TestCardResult(true), TestSpa.Json, Ct);
        }

        var factory = fixture.Factory.WithWebHostBuilder(b =>
            b.ConfigureTestServices(s => s.AddSingleton<TimeProvider>(new FixedTime(booked.Appointment.Start.AddHours(-12)))));
        var late = factory.CreateClient();
        late.DefaultRequestHeaders.Add("X-Aispa-Spa", spa.TenantId.ToString());
        return (booked, late);
    }

    [Fact]
    public async Task Given_Termin_in_der_Frist_mit_Karte_When_der_Gast_absagen_will_Then_nennt_die_API_die_Gebuehr_und_verlangt_die_Zustimmung()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var (booked, late) = await BookWithCardTwelveHoursAheadAsync(spa, saveCard: true);

        var view = await late.GetFromJsonAsync<OnlineAppointment>($"/api/online/bookings/{booked.ManageToken}", TestSpa.Json, Ct);
        var withoutConsent = await late.PostAsync($"/api/online/bookings/{booked.ManageToken}/cancel", null, Ct);
        var stillBooked = await spa.GetAsync<AppointmentDto>($"/api/appointments/{booked.Appointment.Id}", Ct);
        var withConsent = await late.PostAsync($"/api/online/bookings/{booked.ManageToken}/cancel?acceptFee=true", null, Ct);
        var guarantee = await spa.GetAsync<CardGuaranteeStaffDto>($"/api/appointments/{booked.Appointment.Id}/card-guarantee", Ct);

        // 50 % von 79 EUR, wie beim Buchen zugestimmt.
        Assert.Equal((false, true, 39.50m), (view!.CanCancel, view.CanCancelLate, view.LateCancelFee));
        Assert.Equal(HttpStatusCode.Conflict, withoutConsent.StatusCode);
        Assert.Equal(AppointmentStatus.Booked, stillBooked.Status);
        Assert.Equal(OnlineAppointmentState.Cancelled, (await TestSpa.ReadAsync<OnlineAppointment>(withConsent, Ct)).State);
        // Die App belastet nichts; die Kassenaufsicht kann die Gebühr danach auslösen.
        Assert.Equal((CardGuaranteeStatus.Saved, true), (guarantee.Status, guarantee.CanCharge));
    }

    [Fact]
    public async Task Given_Termin_in_der_Frist_ohne_Karte_Then_bleibt_nur_der_Anruf()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var (booked, late) = await BookWithCardTwelveHoursAheadAsync(spa, saveCard: false);

        var view = await late.GetFromJsonAsync<OnlineAppointment>($"/api/online/bookings/{booked.ManageToken}", TestSpa.Json, Ct);
        var cancel = await late.PostAsync($"/api/online/bookings/{booked.ManageToken}/cancel?acceptFee=true", null, Ct);

        Assert.Equal((false, null), (view!.CanCancelLate, view.LateCancelFee));
        Assert.Equal(HttpStatusCode.Conflict, cancel.StatusCode);
    }

    [Theory]
    [InlineData(24, 6, false, true)]
    [InlineData(6, 24, true, false)]
    public async Task Given_die_Frist_wurde_nach_der_Buchung_geaendert_Then_gilt_die_Frist_der_Zustimmung(
        int deadlineAtBooking, int deadlineNow, bool canCancel, bool canCancelLate)
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var (booked, late) = await BookWithCardTwelveHoursAheadAsync(spa, saveCard: true, deadlineHours: deadlineAtBooking);
        await spa.PutAsync<LocationOnlineRules>($"/api/locations/{spa.Location.Id}/online-rules",
            new LocationOnlineRules(true, 60, 365, deadlineNow, 15, null, false, 50), Ct);

        var view = await late.GetFromJsonAsync<OnlineAppointment>($"/api/online/bookings/{booked.ManageToken}", TestSpa.Json, Ct);

        Assert.Equal((canCancel, canCancelLate), (view!.CanCancel, view.CanCancelLate));
        Assert.Equal(booked.Appointment.Start.AddHours(-deadlineAtBooking), view.CancellableUntil);
    }

    [Fact]
    public async Task Given_bezahlte_Anzahlung_und_Karte_nach_der_Frist_Then_bleibt_nur_der_Anruf()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await spa.PutAsync<LocationOnlineRules>($"/api/locations/{spa.Location.Id}/online-rules",
            new LocationOnlineRules(true, 60, 365, 24, 15, null, false, 50, 0, false, 30), Ct);
        await spa.PostAsync<CashRegisterDto>("/api/pos/registers", new CashRegisterInput(
            spa.Location.Id, $"A-{Guid.NewGuid():N}"[..14], FiscalCountry.AT, "Test-Spa GmbH\nTeststraße 1, 1010 Wien\nATU12345678",
            "Test-Spa GmbH", "AT61 1904 3002 3457 3201", null), Ct);
        using var client = spa.OnlineClient();
        var booked = await BookAsync(spa, client, TestSpa.At(10), NewEmail(), acceptFee: true);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync($"/api/online/bookings/{booked.ManageToken}/deposit/test", new TestPaymentResult(true), TestSpa.Json, Ct)).StatusCode);
        await spa.PostAsync<CardGuaranteeStaffDto>($"/api/appointments/{booked.Appointment.Id}/card-guarantee/request", new { }, Ct);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"/api/online/bookings/{booked.ManageToken}/card/accept", null, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync($"/api/online/bookings/{booked.ManageToken}/card/test", new TestCardResult(true), TestSpa.Json, Ct)).StatusCode);
        var factory = fixture.Factory.WithWebHostBuilder(b =>
            b.ConfigureTestServices(s => s.AddSingleton<TimeProvider>(new FixedTime(booked.Appointment.Start.AddHours(-12)))));
        var late = factory.CreateClient();
        late.DefaultRequestHeaders.Add("X-Aispa-Spa", spa.TenantId.ToString());

        var view = await late.GetFromJsonAsync<OnlineAppointment>($"/api/online/bookings/{booked.ManageToken}", TestSpa.Json, Ct);
        var refused = await late.PostAsync($"/api/online/bookings/{booked.ManageToken}/cancel?acceptFee=true", null, Ct);

        Assert.Equal(CardGuaranteeStatus.Saved, view!.CardGuarantee!.Status);
        Assert.Equal((false, false), (view.CanCancel, view.CanCancelLate));
        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
    }

    [Fact]
    public async Task Given_Termin_When_die_Kalenderdatei_geladen_wird_Then_enthaelt_sie_Zeit_Behandlung_und_Adresse()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await spa.PutAsync<LocationDto>($"/api/locations/{spa.Location.Id}", new LocationInput(spa.Location.Name, spa.Location.TimeZone, spa.Location.Currency,
            spa.Location.OpeningHours, null, new LocationContact("Kärntner Straße 1", "1010", "Wien", null, null)), Ct);
        using var client = spa.OnlineClient();
        var booked = await BookAsync(spa, client, TestSpa.At(10), NewEmail());
        // Ohne Kopfzeile, wie ein Link aus dem Browser: das Spa kommt aus dem Code.
        using var plain = fixture.CreateClient();

        var response = await plain.GetAsync($"/api/online/bookings/{booked.ManageToken}/calendar.ics", Ct);
        var text = await response.Content.ReadAsStringAsync(Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/calendar", response.Content.Headers.ContentType!.MediaType);
        Assert.Equal("termin.ics", response.Content.Headers.ContentDisposition!.FileName);
        // Montag, 1. März 2027, 10:00 Uhr Wien (Winterzeit) ist 09:00 UTC.
        Assert.Contains("DTSTART:20270301T090000Z\r\n", text, StringComparison.Ordinal);
        Assert.Contains($"SUMMARY:{spa.MassageTreatment.Name}\r\n", text, StringComparison.Ordinal);
        Assert.Contains("Kärntner Straße 1\\, 1010 Wien", text.Replace("\r\n ", "", StringComparison.Ordinal), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Given_fremder_Termin_When_die_Kalenderdatei_verlangt_wird_Then_404()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        using var guest = spa.OnlineClient();
        var foreign = await BookAsync(spa, guest, TestSpa.At(10), NewEmail());
        using var other = spa.OnlineClient(await SignInAsync(spa, NewEmail()));
        await BookAsync(spa, other, TestSpa.At(12), NewEmail());

        var response = await other.GetAsync($"/api/online/me/appointments/{foreign.Appointment.Id}/calendar.ics", Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Given_online_gekaufter_Gutschein_When_der_Gast_seine_Gutscheine_ansieht_Then_steht_der_Restwert_dort()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await spa.PostAsync<CashRegisterDto>("/api/pos/registers", new CashRegisterInput(
            spa.Location.Id, $"A-{Guid.NewGuid():N}"[..14], FiscalCountry.AT, "Test-Spa GmbH\nTeststraße 1, 1010 Wien\nATU12345678",
            "Test-Spa GmbH", "AT61 1904 3002 3457 3201", null), Ct);
        var email = NewEmail();
        using var client = spa.OnlineClient(await SignInAsync(spa, email));
        await client.PutAsJsonAsync("/api/online/me/profile", new CustomerProfileInput("Gerda", "Schenk", null), TestSpa.Json, Ct);
        var ordered = await client.PostAsJsonAsync("/api/online/vouchers/orders", new VoucherOrderRequest(spa.Location.Id, VoucherKind.MultiPurpose, 50m, null,
            new OnlineContact("Gerda", "Schenk", email, null), null, null, true, "de"), TestSpa.Json, Ct);
        var token = (await TestSpa.ReadAsync<VoucherOrderStarted>(ordered, Ct)).Token;
        await client.PostAsJsonAsync($"/api/online/vouchers/orders/{token}/test-payment", new TestPaymentResult(true), TestSpa.Json, Ct);

        OnlineOwnedVoucher? voucher = null;
        for (var attempt = 0; attempt < 150 && voucher is null; attempt++)
        {
            voucher = (await client.GetFromJsonAsync<List<OnlineOwnedVoucher>>("/api/online/me/vouchers", TestSpa.Json, Ct))!.FirstOrDefault();
            if (voucher is null)
            {
                await Task.Delay(100, Ct);
            }
        }

        Assert.NotNull(voucher);
        Assert.Equal((50m, 50m, "EUR", VoucherStatus.Active), (voucher.InitialValue, voucher.Balance, voucher.Currency, voucher.Status));
    }

    [Fact]
    public async Task Given_kein_Konto_When_die_Gutscheine_verlangt_werden_Then_401()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        using var client = spa.OnlineClient();

        var response = await client.GetAsync("/api/online/me/vouchers", Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private async Task<string> SignInAsync(TestSpa spa, string email)
    {
        using var client = spa.OnlineClient();
        await client.PostAsJsonAsync("/api/online/auth/code", new SignInCodeRequest(email, "de"), TestSpa.Json, Ct);
        var mail = await fixture.Mailbox.WaitForAsync(email, m => m.Subject.StartsWith("Ihr Anmeldecode", StringComparison.Ordinal), Ct);
        var verify = await client.PostAsJsonAsync("/api/online/auth/code/verify", new SignInCodeVerification(email, SixDigits().Match(mail.Subject).Value), TestSpa.Json, Ct);
        Assert.Equal(HttpStatusCode.OK, verify.StatusCode);
        return string.Join("; ", verify.Headers.GetValues("Set-Cookie").Select(c => c.Split(';')[0]).Where(c => c.StartsWith("aispa.kunde=", StringComparison.Ordinal)));
    }
}
