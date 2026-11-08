using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using AispaStudio.Api.Online;
using AispaStudio.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace AispaStudio.Api.Tests;

public sealed class CardGuaranteeTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static DateOnly ViennaToday =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna")).DateTime);

    private static string NewEmail() => $"karte-{Guid.NewGuid():N}@example.at";

    /// <summary>Standort mit 50 % Ausfallgebühr, Buchungshorizont ein Jahr (die Testtermine liegen im März 2027).</summary>
    private static Task<LocationOnlineRules> RequireCardAsync(TestSpa spa, int percent = 50) =>
        spa.PutAsync<LocationOnlineRules>($"/api/locations/{spa.Location.Id}/online-rules",
            new LocationOnlineRules(true, 60, 365, 24, 15, null, false, percent), Ct);

    private static OnlineBookingRequest Request(TestSpa spa, DateTimeOffset start, string email, bool acceptFee) =>
        new(spa.Location.Id, spa.MassageTreatment.Id, start, new OnlineContact("Nora", "Karte", email, null), null, true, "de", acceptFee);

    private static async Task<CashRegisterDto> CreateRegisterAsync(TestSpa spa) =>
        await spa.PostAsync<CashRegisterDto>("/api/pos/registers", new CashRegisterInput(
            spa.Location.Id, $"K-{Guid.NewGuid():N}"[..14], FiscalCountry.AT, "Test-Spa GmbH\nTeststraße 1, 1010 Wien\nATU12345678",
            "Test-Spa GmbH", "AT61 1904 3002 3457 3201", null), Ct);

    private static async Task<OnlineBookingConfirmation> BookAsync(TestSpa spa, DateTimeOffset start, string email)
    {
        using var client = spa.OnlineClient();
        var response = await client.PostAsJsonAsync("/api/online/bookings", Request(spa, start, email, true), TestSpa.Json, Ct);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await TestSpa.ReadAsync<OnlineBookingConfirmation>(response, Ct);
    }

    private static async Task<CardGuaranteeStaffDto> WaitForAsync(TestSpa spa, Guid appointmentId, CardGuaranteeStatus status)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));
        while (true)
        {
            var guarantee = await spa.GetAsync<CardGuaranteeStaffDto>($"/api/appointments/{appointmentId}/card-guarantee", Ct);
            if (guarantee.Status == status)
            {
                return guarantee;
            }

            await Task.Delay(50, timeout.Token);
        }
    }

    [Fact]
    public async Task Ohne_Zustimmung_zur_Ausfallgebuehr_keine_Buchung()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await RequireCardAsync(spa);
        using var client = spa.OnlineClient();

        var locations = await client.GetFromJsonAsync<List<OnlineLocation>>("/api/online/locations", TestSpa.Json, Ct);
        var response = await client.PostAsJsonAsync("/api/online/bookings", Request(spa, TestSpa.At(10), NewEmail(), false), TestSpa.Json, Ct);

        Assert.Equal(50, Assert.Single(locations!, l => l.Id == spa.Location.Id).NoShowFeePercent);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("acceptNoShowFee", await response.Content.ReadAsStringAsync(Ct), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Ohne_Regel_wird_keine_Karte_verlangt()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await spa.PutAsync<LocationOnlineRules>($"/api/locations/{spa.Location.Id}/online-rules",
            new LocationOnlineRules(true, 60, 365, 24, 15, null, false), Ct);

        var booked = await BookAsync(spa, TestSpa.At(10), NewEmail());
        var staff = await spa.Client.GetAsync($"/api/appointments/{booked.Appointment.Id}/card-guarantee", Ct);

        Assert.Null(booked.Appointment.CardGuarantee);
        Assert.Equal(HttpStatusCode.NoContent, staff.StatusCode);
    }

    [Fact]
    public async Task Karte_hinterlegen_und_bei_Nichterscheinen_die_Gebuehr_ueber_die_Kasse_belasten()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await RequireCardAsync(spa);
        var register = await CreateRegisterAsync(spa);
        var email = NewEmail();

        var booked = await BookAsync(spa, TestSpa.At(10), email);
        var card = booked.Appointment.CardGuarantee!;
        Assert.Equal((39.50m, 50, CardGuaranteeStatus.Pending, true), (card.FeeAmount, card.FeePercent, card.Status, card.TestMode));
        var confirmation = await fixture.Mailbox.WaitForAsync(email, m => m.Subject.StartsWith("Termin bestätigt", StringComparison.Ordinal), Ct);
        Assert.Contains("berechnen wir 39,50 EUR (50 % des Preises)", confirmation.Text, StringComparison.Ordinal);

        // Vor dem Hinterlegen und vor dem Termin lässt sich nichts belasten.
        var pending = await spa.GetAsync<CardGuaranteeStaffDto>($"/api/appointments/{booked.Appointment.Id}/card-guarantee", Ct);
        Assert.False(pending.CanCharge);
        Assert.Contains("noch nicht hinterlegt", pending.Hint, StringComparison.Ordinal);

        using var client = spa.OnlineClient();
        var saved = await client.PostAsJsonAsync($"/api/online/bookings/{booked.ManageToken}/card/test", new TestCardResult(true), TestSpa.Json, Ct);
        Assert.Equal(CardGuaranteeStatus.Saved, (await TestSpa.ReadAsync<OnlineAppointment>(saved, Ct)).CardGuarantee!.Status);

        var early = await spa.Client.PostAsync($"/api/appointments/{booked.Appointment.Id}/card-guarantee/charge", null, Ct);
        Assert.Equal(HttpStatusCode.Conflict, early.StatusCode);

        await spa.PostAsync<AppointmentDto>($"/api/appointments/{booked.Appointment.Id}/status", new AppointmentStatusChange(AppointmentStatus.NoShow), Ct);
        var due = await spa.GetAsync<CardGuaranteeStaffDto>($"/api/appointments/{booked.Appointment.Id}/card-guarantee", Ct);
        Assert.True(due.CanCharge);

        var started = await spa.Client.PostAsync($"/api/appointments/{booked.Appointment.Id}/card-guarantee/charge", null, Ct);
        Assert.Equal(HttpStatusCode.OK, started.StatusCode);
        var charged = await WaitForAsync(spa, booked.Appointment.Id, CardGuaranteeStatus.Charged);
        Assert.NotNull(charged.ReceiptId);
        Assert.False(charged.CanCharge);

        // Ein zweites Auslösen belastet nicht noch einmal.
        var again = await spa.Client.PostAsync($"/api/appointments/{booked.Appointment.Id}/card-guarantee/charge", null, Ct);
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);

        var receipts = await spa.GetAsync<List<ReceiptDto>>($"/api/pos/registers/{register.Id}/receipts?date={ViennaToday:yyyy-MM-dd}", Ct);
        // Neben dem Startbeleg der Kasse genau ein Beleg über die Gebühr.
        var receipt = Assert.Single(receipts, r => r.Total > 0);
        Assert.Equal(charged.ReceiptId, receipt.Id);
        Assert.Equal(39.50m, receipt.Total);
        Assert.StartsWith("Ausfallgebühr Massage", Assert.Single(receipt.Lines).Description, StringComparison.Ordinal);
        Assert.Equal(PaymentMethod.Card, Assert.Single(receipt.Payments).Method);
        Assert.StartsWith("Online Test test-charge-", receipt.Payments[0].Reference, StringComparison.Ordinal);

        var mail = await fixture.Mailbox.WaitForAsync(email, m => m.Subject.StartsWith("Ausfallgebühr", StringComparison.Ordinal), Ct);
        Assert.Contains($"Beleg: {register.Code} Nr. {receipt.Number}", mail.Text, StringComparison.Ordinal);
        Assert.Contains("39,50 EUR", mail.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Rechtzeitig_storniert_kostet_nichts_und_abgebrochenes_Hinterlegen_laesst_sich_neu_starten()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await RequireCardAsync(spa);
        await CreateRegisterAsync(spa);
        var booked = await BookAsync(spa, TestSpa.At(11), NewEmail());
        using var client = spa.OnlineClient();

        var aborted = await client.PostAsJsonAsync($"/api/online/bookings/{booked.ManageToken}/card/test", new TestCardResult(false), TestSpa.Json, Ct);
        Assert.Equal(CardGuaranteeStatus.Failed, (await TestSpa.ReadAsync<OnlineAppointment>(aborted, Ct)).CardGuarantee!.Status);
        var retried = await client.PostAsync($"/api/online/bookings/{booked.ManageToken}/card/retry", null, Ct);
        Assert.Equal(CardGuaranteeStatus.Pending, (await TestSpa.ReadAsync<OnlineAppointment>(retried, Ct)).CardGuarantee!.Status);
        await client.PostAsJsonAsync($"/api/online/bookings/{booked.ManageToken}/card/test", new TestCardResult(true), TestSpa.Json, Ct);

        await spa.PostAsync<AppointmentDto>($"/api/appointments/{booked.Appointment.Id}/cancel", new CancelAppointmentRequest("Gast hat angerufen"), Ct);
        var guarantee = await spa.GetAsync<CardGuaranteeStaffDto>($"/api/appointments/{booked.Appointment.Id}/card-guarantee", Ct);
        var charge = await spa.Client.PostAsync($"/api/appointments/{booked.Appointment.Id}/card-guarantee/charge", null, Ct);

        Assert.Equal(CardGuaranteeStatus.Saved, guarantee.Status);
        Assert.False(guarantee.CanCharge);
        Assert.Contains("Rechtzeitig storniert", guarantee.Hint, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.Conflict, charge.StatusCode);
    }

    [Fact]
    public async Task Rezeption_fordert_die_Karte_per_Link_an_und_der_Gast_stimmt_zu()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await RequireCardAsync(spa);
        await CreateRegisterAsync(spa);
        var email = NewEmail();
        var customer = await spa.PostAsync<CustomerDto>("/api/customers", new CustomerInput("Lea", "Telefon", email, null, null, null), Ct);
        var booked = await TestSpa.ReadAsync<AppointmentDto>(await spa.BookAsync(TestSpa.At(14), Ct, customer: customer.Id), Ct);

        var offer = await spa.GetAsync<CardRequestOffer>($"/api/appointments/{booked.Id}/card-guarantee/offer", Ct);
        Assert.Equal((true, 39.50m, 50), (offer.Possible, offer.FeeAmount, offer.FeePercent));
        var requested = await spa.PostAsync<CardGuaranteeStaffDto>($"/api/appointments/{booked.Id}/card-guarantee/request", new { }, Ct);
        Assert.Equal(CardGuaranteeStatus.AwaitingConsent, requested.Status);
        Assert.Null(requested.AcceptedAt);
        Assert.Contains("noch nicht zugestimmt", requested.Hint, StringComparison.Ordinal);

        var mail = await fixture.Mailbox.WaitForAsync(email, m => m.Subject.StartsWith("Bitte Karte hinterlegen", StringComparison.Ordinal), Ct);
        Assert.Contains("Ausfallgebühr von 39,50 EUR (50 % des Preises)", mail.Text, StringComparison.Ordinal);
        var token = Regex.Match(mail.Text, "#/termin/([A-Za-z0-9_-]+)").Groups[1].Value;
        using var client = spa.OnlineClient();
        var page = await client.GetFromJsonAsync<OnlineAppointment>($"/api/online/bookings/{token}", TestSpa.Json, Ct);
        Assert.Equal((CardGuaranteeStatus.AwaitingConsent, 24), (page!.CardGuarantee!.Status, page.CardGuarantee.DeadlineHours));

        // Ohne Zustimmung lässt sich keine Karte hinterlegen.
        var noTest = await client.PostAsJsonAsync($"/api/online/bookings/{token}/card/test", new TestCardResult(true), TestSpa.Json, Ct);
        Assert.Equal(HttpStatusCode.NotFound, noTest.StatusCode);

        var accepted = await client.PostAsync($"/api/online/bookings/{token}/card/accept", null, Ct);
        Assert.Equal(CardGuaranteeStatus.Pending, (await TestSpa.ReadAsync<OnlineAppointment>(accepted, Ct)).CardGuarantee!.Status);
        var saved = await client.PostAsJsonAsync($"/api/online/bookings/{token}/card/test", new TestCardResult(true), TestSpa.Json, Ct);
        Assert.Equal(CardGuaranteeStatus.Saved, (await TestSpa.ReadAsync<OnlineAppointment>(saved, Ct)).CardGuarantee!.Status);
        var staff = await spa.GetAsync<CardGuaranteeStaffDto>($"/api/appointments/{booked.Id}/card-guarantee", Ct);
        Assert.NotNull(staff.AcceptedAt);

        var again = await spa.Client.PostAsJsonAsync($"/api/appointments/{booked.Id}/card-guarantee/request", new { }, TestSpa.Json, Ct);
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
    }

    [Fact]
    public async Task Ohne_Regel_oder_ohne_Mail_laesst_sich_keine_Karte_anfordern()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var noMail = await spa.PostAsync<CustomerDto>("/api/customers", new CustomerInput("Ohne", "Mail", null, "+43 660 1", null, null), Ct);
        var booked = await TestSpa.ReadAsync<AppointmentDto>(await spa.BookAsync(TestSpa.At(15), Ct, customer: noMail.Id), Ct);

        var withoutRule = await spa.GetAsync<CardRequestOffer>($"/api/appointments/{booked.Id}/card-guarantee/offer", Ct);
        await RequireCardAsync(spa);
        var withoutMail = await spa.GetAsync<CardRequestOffer>($"/api/appointments/{booked.Id}/card-guarantee/offer", Ct);
        var request = await spa.Client.PostAsJsonAsync($"/api/appointments/{booked.Id}/card-guarantee/request", new { }, TestSpa.Json, Ct);

        Assert.False(withoutRule.Possible);
        Assert.Contains("keine Ausfallgebühr", withoutRule.Reason, StringComparison.Ordinal);
        Assert.Contains("keine E-Mail-Adresse", withoutMail.Reason, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.Conflict, request.StatusCode);
    }

    [Fact]
    public async Task Karte_wird_nach_Ablauf_der_Frist_beim_Anbieter_geloescht()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await RequireCardAsync(spa);
        var booked = await BookAsync(spa, TestSpa.At(10), NewEmail());
        using var client = spa.OnlineClient();
        await client.PostAsJsonAsync($"/api/online/bookings/{booked.ManageToken}/card/test", new TestCardResult(true), TestSpa.Json, Ct);
        await spa.PostAsync<AppointmentDto>($"/api/appointments/{booked.Appointment.Id}/status", new AppointmentStatusChange(AppointmentStatus.NoShow), Ct);
        var cleanup = fixture.Factory.Services.GetRequiredService<SavedCardCleanup>();

        // Innerhalb der 14 Tage nach dem Termin bleibt die Karte, damit die Gebühr noch belastet werden kann.
        var early = await cleanup.RunOnceAsync(booked.Appointment.End.AddDays(13), spa.Location.Id, Ct);
        var late = await cleanup.RunOnceAsync(booked.Appointment.End.AddDays(15), spa.Location.Id, Ct);
        CardGuaranteeStaffDto removed;
        using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(Ct))
        {
            timeout.CancelAfter(TimeSpan.FromSeconds(15));
            while ((removed = await spa.GetAsync<CardGuaranteeStaffDto>($"/api/appointments/{booked.Appointment.Id}/card-guarantee", Ct)).CardRemovedAt is null)
            {
                await Task.Delay(50, timeout.Token);
            }
        }

        var charge = await spa.Client.PostAsync($"/api/appointments/{booked.Appointment.Id}/card-guarantee/charge", null, Ct);
        var again = await cleanup.RunOnceAsync(booked.Appointment.End.AddDays(15), spa.Location.Id, Ct);

        Assert.Equal((0, 1, 0), (early, late, again));
        Assert.Equal(CardGuaranteeStatus.Saved, removed.Status);
        Assert.False(removed.CanCharge);
        Assert.Contains("beim Zahlungsanbieter gelöscht", removed.Hint, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.Conflict, charge.StatusCode);
    }

    [Fact]
    public async Task Online_Termin_ohne_Karte_wird_nach_der_Frist_freigegeben()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await spa.PutAsync<LocationOnlineRules>($"/api/locations/{spa.Location.Id}/online-rules",
            new LocationOnlineRules(true, 60, 365, 24, 15, null, false, 50, 2), Ct);
        var email = NewEmail();
        var open = await BookAsync(spa, TestSpa.At(10), email);
        var secured = await BookAsync(spa, TestSpa.At(14), NewEmail());
        using var client = spa.OnlineClient();
        await client.PostAsJsonAsync($"/api/online/bookings/{secured.ManageToken}/card/test", new TestCardResult(true), TestSpa.Json, Ct);
        var holdUntil = open.Appointment.CardGuarantee!.HoldUntil!.Value;
        var confirmation = await fixture.Mailbox.WaitForAsync(email, m => m.Subject.StartsWith("Termin bestätigt", StringComparison.Ordinal), Ct);
        var release = fixture.Factory.Services.GetRequiredService<UnsecuredBookingRelease>();

        var early = await release.RunOnceAsync(holdUntil.AddMinutes(-5), spa.Location.Id, Ct);
        var due = await release.RunOnceAsync(holdUntil.AddMinutes(5), spa.Location.Id, Ct);
        var released = await WaitForAsync(spa, open.Appointment.Id, CardGuaranteeStatus.Released);
        // Die Mail geht erst nach dem Storno hinaus; vorher kann der Termin für einen Moment noch „gebucht“ sein.
        var mail = await fixture.Mailbox.WaitForAsync(email, m => m.Subject.StartsWith("Termin freigegeben", StringComparison.Ordinal), Ct);
        var guest = await client.GetFromJsonAsync<OnlineAppointment>($"/api/online/bookings/{open.ManageToken}", TestSpa.Json, Ct);
        var kept = await spa.GetAsync<CardGuaranteeStaffDto>($"/api/appointments/{secured.Appointment.Id}/card-guarantee", Ct);

        Assert.InRange(holdUntil, DateTimeOffset.UtcNow.AddHours(1.9), DateTimeOffset.UtcNow.AddHours(2.1));
        Assert.Contains("Bitte hinterlegen Sie die Karte bis", confirmation.Text, StringComparison.Ordinal);
        Assert.Equal((0, 1), (early, due));
        Assert.Contains("keine Karte hinterlegt", released.Hint, StringComparison.Ordinal);
        Assert.False(released.CanCharge);
        Assert.Equal(OnlineAppointmentState.Cancelled, guest!.State);
        Assert.Equal(CardGuaranteeStatus.Released, guest.CardGuarantee!.Status);
        Assert.Contains("abgebucht wurde nichts", mail.Text, StringComparison.Ordinal);
        Assert.Equal(CardGuaranteeStatus.Saved, kept.Status);
        Assert.Null(kept.ReleaseAt);
    }

    [Fact]
    public async Task Stammgast_mit_zwei_No_Shows_muss_wieder_eine_Karte_hinterlegen()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await spa.PutAsync<LocationOnlineRules>($"/api/locations/{spa.Location.Id}/online-rules",
            new LocationOnlineRules(true, 60, 365, 24, 15, null, false, 50, 0, true), Ct);
        var email = NewEmail();
        var regular = await spa.PostAsync<CustomerDto>("/api/customers", new CustomerInput("Rita", "Stamm", email, null, null, null), Ct);
        foreach (var (hour, status) in new[] { (10, AppointmentStatus.Completed), (12, AppointmentStatus.NoShow), (13, AppointmentStatus.NoShow) })
        {
            var visit = await TestSpa.ReadAsync<AppointmentDto>(await spa.BookAsync(TestSpa.At(hour), Ct, customer: regular.Id), Ct);
            await spa.PostAsync<AppointmentDto>($"/api/appointments/{visit.Id}/status", new AppointmentStatusChange(status), Ct);
        }

        using var client = spa.OnlineClient();
        var withoutConsent = await client.PostAsJsonAsync("/api/online/bookings", Request(spa, TestSpa.At(14), email, false), TestSpa.Json, Ct);
        var booked = await BookAsync(spa, TestSpa.At(15), email);

        Assert.Equal(HttpStatusCode.BadRequest, withoutConsent.StatusCode);
        Assert.Equal(50, booked.Appointment.CardGuarantee!.FeePercent);
    }

    [Fact]
    public async Task Gebuehr_nur_fuer_neue_Gaeste_und_eigene_Prozent_je_Behandlung()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await spa.PutAsync<LocationOnlineRules>($"/api/locations/{spa.Location.Id}/online-rules",
            new LocationOnlineRules(true, 60, 365, 24, 15, null, false, 50, 0, true), Ct);
        // Stammgast: eine abgeschlossene Behandlung im Spa.
        var regularEmail = NewEmail();
        var regular = await spa.PostAsync<CustomerDto>("/api/customers", new CustomerInput("Rita", "Stamm", regularEmail, null, null, null), Ct);
        var past = await TestSpa.ReadAsync<AppointmentDto>(await spa.BookAsync(TestSpa.At(10), Ct, customer: regular.Id), Ct);
        await spa.PostAsync<AppointmentDto>($"/api/appointments/{past.Id}/status", new AppointmentStatusChange(AppointmentStatus.Completed), Ct);
        // Diese Behandlung verlangt immer 20 %, auch an Standorten ohne Regel.
        var strict = await spa.PostAsync<TreatmentDto>("/api/treatments",
            new TreatmentInput("Ritual", 60, 0, 0, 200m, [spa.Massage.Id], [spa.RoomA.Id], [], null, null, 20), Ct);
        using var client = spa.OnlineClient();

        var locations = await client.GetFromJsonAsync<List<OnlineLocation>>("/api/online/locations", TestSpa.Json, Ct);
        var treatments = await client.GetFromJsonAsync<List<OnlineTreatment>>($"/api/online/locations/{spa.Location.Id}/treatments", TestSpa.Json, Ct);
        // Der Stammgast bucht ohne Zustimmung und bekommt keine Hinterlegung.
        var regularBooking = await client.PostAsJsonAsync("/api/online/bookings", Request(spa, TestSpa.At(12), regularEmail, false), TestSpa.Json, Ct);
        var regularBooked = await TestSpa.ReadAsync<OnlineBookingConfirmation>(regularBooking, Ct);
        var offer = await spa.GetAsync<CardRequestOffer>($"/api/appointments/{regularBooked.Appointment.Id}/card-guarantee/offer", Ct);
        // Ein neuer Gast muss zustimmen und hinterlegt.
        var newRefused = await client.PostAsJsonAsync("/api/online/bookings", Request(spa, TestSpa.At(13), NewEmail(), false), TestSpa.Json, Ct);
        var newBooked = await BookAsync(spa, TestSpa.At(14), NewEmail());
        // Für die Behandlung mit eigenen Prozent gilt 20 % statt 50 %.
        var strictBooking = await client.PostAsJsonAsync("/api/online/bookings", new OnlineBookingRequest(spa.Location.Id, strict.Id, TestSpa.At(16),
            new OnlineContact("Noah", "Neu", NewEmail(), null), null, true, "de", true), TestSpa.Json, Ct);
        var strictBooked = await TestSpa.ReadAsync<OnlineBookingConfirmation>(strictBooking, Ct);

        Assert.True(Assert.Single(locations!, l => l.Id == spa.Location.Id).NoShowFeeNewCustomersOnly);
        Assert.Equal((50, 20), (treatments!.Single(t => t.Id == spa.MassageTreatment.Id).NoShowFeePercent, treatments!.Single(t => t.Id == strict.Id).NoShowFeePercent));
        Assert.Equal(HttpStatusCode.Created, regularBooking.StatusCode);
        Assert.Null(regularBooked.Appointment.CardGuarantee);
        Assert.False(offer.Possible);
        Assert.Equal(HttpStatusCode.BadRequest, newRefused.StatusCode);
        Assert.Equal((39.50m, 50), (newBooked.Appointment.CardGuarantee!.FeeAmount, newBooked.Appointment.CardGuarantee.FeePercent));
        Assert.Equal((40m, 20), (strictBooked.Appointment.CardGuarantee!.FeeAmount, strictBooked.Appointment.CardGuarantee.FeePercent));
    }

    [Fact]
    public async Task Behandlung_ohne_Gebuehr_verlangt_trotz_Regel_des_Standorts_keine_Karte()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await RequireCardAsync(spa);
        var free = await spa.PostAsync<TreatmentDto>("/api/treatments",
            new TreatmentInput("Kurzmassage", 20, 5, 5, 29m, [spa.Massage.Id], [spa.RoomA.Id], [], null, null, 0), Ct);
        using var client = spa.OnlineClient();

        var response = await client.PostAsJsonAsync("/api/online/bookings", new OnlineBookingRequest(spa.Location.Id, free.Id, TestSpa.At(10),
            new OnlineContact("Nora", "Kurz", NewEmail(), null), null, true, "de", false), TestSpa.Json, Ct);
        var booked = await TestSpa.ReadAsync<OnlineBookingConfirmation>(response, Ct);

        Assert.Equal(0, free.NoShowFeePercent);
        Assert.Null(booked.Appointment.CardGuarantee);
    }
}
