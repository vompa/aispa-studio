using System.Net;
using System.Net.Http.Json;
using AispaStudio.Api.Mail;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Tests;

public sealed class WaitlistTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string NewEmail() => $"warteliste-{Guid.NewGuid():N}@example.at";

    private static WaitlistRequest Request(TestSpa spa, string email, DayPart part = DayPart.Any, DateOnly? from = null, DateOnly? until = null,
        bool privacy = true) =>
        new(spa.Location.Id, spa.MassageTreatment.Id, from ?? TestSpa.Monday, until ?? TestSpa.Monday.AddDays(6), part,
            new OnlineContact("Wanda", "Warte", email, null), privacy, "de");

    private static async Task<WaitlistConfirmation> JoinAsync(TestSpa spa, string email, DayPart part = DayPart.Any)
    {
        using var client = spa.OnlineClient();
        var response = await client.PostAsJsonAsync("/api/online/waitlist", Request(spa, email, part), TestSpa.Json, Ct);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await TestSpa.ReadAsync<WaitlistConfirmation>(response, Ct);
    }

    private Task<OutgoingMail> OfferAsync(string email) =>
        fixture.Mailbox.WaitForAsync(email, m => m.Subject.StartsWith("Termin frei", StringComparison.Ordinal), Ct);

    private static async Task CancelAsync(TestSpa spa, AppointmentDto appointment)
    {
        var response = await spa.Client.PostAsJsonAsync($"/api/appointments/{appointment.Id}/cancel", new CancelAppointmentRequest("Krank"), TestSpa.Json, Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Gast_traegt_sich_ein_bekommt_Mail_und_kann_sich_per_Link_austragen()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var email = NewEmail();

        var confirmation = await JoinAsync(spa, email, DayPart.Morning);

        Assert.Equal(WaitlistStatus.Waiting, confirmation.Entry.Status);
        Assert.Equal("Massage", confirmation.Entry.TreatmentName);
        var mail = await fixture.Mailbox.WaitForAsync(email, m => m.Subject.StartsWith("Sie stehen auf der Warteliste", StringComparison.Ordinal), Ct);
        Assert.Contains($"/buchen/#/warteliste/{confirmation.ManageToken}", mail.Text, StringComparison.Ordinal);
        Assert.Contains("1. 3. 2027 bis 7. 3. 2027, vormittags", mail.Text, StringComparison.Ordinal);

        var staff = await spa.GetAsync<List<WaitlistStaffEntry>>($"/api/locations/{spa.Location.Id}/waitlist", Ct);
        var listed = Assert.Single(staff);
        Assert.Equal("Wanda Warte", listed.CustomerName);
        Assert.Equal(DayPart.Morning, listed.DayPart);

        using var client = spa.OnlineClient();
        var byLink = await client.GetFromJsonAsync<WaitlistEntryDto>($"/api/online/waitlist/{confirmation.ManageToken}", TestSpa.Json, Ct);
        Assert.Equal(confirmation.Entry.Id, byLink!.Id);
        var left = await client.PostAsync($"/api/online/waitlist/{confirmation.ManageToken}/cancel", null, Ct);
        Assert.Equal(WaitlistStatus.Cancelled, (await TestSpa.ReadAsync<WaitlistEntryDto>(left, Ct)).Status);
        Assert.Empty(await spa.GetAsync<List<WaitlistStaffEntry>>($"/api/locations/{spa.Location.Id}/waitlist", Ct));
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/online/waitlist/{new string('x', 43)}", Ct)).StatusCode);
    }

    [Fact]
    public async Task Storno_meldet_freie_Zeiten_passend_zur_Tageszeit_und_nicht_zweimal_am_selben_Tag()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var morning = NewEmail();
        var evening = NewEmail();
        await JoinAsync(spa, morning, DayPart.Morning);
        await JoinAsync(spa, evening, DayPart.Evening);
        var first = await spa.BookOkAsync(TestSpa.At(10), Ct);
        var second = await spa.BookOkAsync(TestSpa.At(14), Ct);

        await CancelAsync(spa, first);

        var offer = await OfferAsync(morning);
        Assert.Contains("Montag, 1. März 2027", offer.Subject, StringComparison.Ordinal);
        Assert.Contains("Freie Zeiten: 09:15, 09:30, ", offer.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("12:", offer.Text, StringComparison.Ordinal);
        Assert.Contains($"/buchen/?spa={spa.TenantId}#/?standort={spa.Location.Id}&behandlung={spa.MassageTreatment.Id}&datum=2027-03-01", offer.Text, StringComparison.Ordinal);

        // Zweite Stornierung am selben Tag: eine neue Kundin bekommt die Mail, die erste nicht noch einmal.
        var late = NewEmail();
        await JoinAsync(spa, late, DayPart.Afternoon);
        await CancelAsync(spa, second);
        await OfferAsync(late);
        Assert.Single(fixture.Mailbox.To(morning), m => m.Subject.StartsWith("Termin frei", StringComparison.Ordinal));
        // Das Team arbeitet bis 17 Uhr: abends gibt es nichts.
        Assert.DoesNotContain(fixture.Mailbox.To(evening), m => m.Subject.StartsWith("Termin frei", StringComparison.Ordinal));

        var staff = await spa.GetAsync<List<WaitlistStaffEntry>>($"/api/locations/{spa.Location.Id}/waitlist", Ct);
        Assert.Equal(1, staff.Single(e => e.DayPart == DayPart.Morning).OfferCount);
    }

    [Fact]
    public async Task Verschieben_meldet_die_alte_Zeit_als_frei()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var morning = NewEmail();
        await JoinAsync(spa, morning, DayPart.Morning);
        var booked = await spa.BookOkAsync(TestSpa.At(10), Ct);

        var moved = await spa.Client.PostAsJsonAsync($"/api/appointments/{booked.Id}/move",
            new MoveAppointmentRequest(TestSpa.At(15), null, null, null), TestSpa.Json, Ct);
        Assert.Equal(HttpStatusCode.OK, moved.StatusCode);

        var offer = await OfferAsync(morning);
        Assert.Contains("Montag, 1. März 2027", offer.Subject, StringComparison.Ordinal);
        Assert.Contains("10:00", offer.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Passende_Buchung_erledigt_den_Eintrag()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var email = NewEmail();
        var confirmation = await JoinAsync(spa, email);
        using var client = spa.OnlineClient();

        var booked = await client.PostAsJsonAsync("/api/online/bookings", new OnlineBookingRequest(
            spa.Location.Id, spa.MassageTreatment.Id, TestSpa.At(11), new OnlineContact("Wanda", "Warte", email, null), null, true, "de"), TestSpa.Json, Ct);
        Assert.Equal(HttpStatusCode.Created, booked.StatusCode);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));
        WaitlistEntryDto? entry;
        do
        {
            await Task.Delay(50, timeout.Token);
            entry = await client.GetFromJsonAsync<WaitlistEntryDto>($"/api/online/waitlist/{confirmation.ManageToken}", TestSpa.Json, Ct);
        }
        while (entry!.Status == WaitlistStatus.Waiting);

        Assert.Equal(WaitlistStatus.Booked, entry.Status);
        Assert.Empty(await spa.GetAsync<List<WaitlistStaffEntry>>($"/api/locations/{spa.Location.Id}/waitlist", Ct));
    }

    [Fact]
    public async Task Eintrag_wird_geprueft_Zeitraum_Datenschutz_doppelt_und_hoechstens_fuenf()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var email = NewEmail();
        using var client = spa.OnlineClient();

        async Task<HttpStatusCode> PostAsync(WaitlistRequest request) =>
            (await client.PostAsJsonAsync("/api/online/waitlist", request, TestSpa.Json, Ct)).StatusCode;

        Assert.Equal(HttpStatusCode.BadRequest, await PostAsync(Request(spa, email, until: TestSpa.Monday.AddDays(60))));
        Assert.Equal(HttpStatusCode.BadRequest, await PostAsync(Request(spa, email, from: TestSpa.Monday.AddDays(3), until: TestSpa.Monday)));
        Assert.Equal(HttpStatusCode.BadRequest, await PostAsync(Request(spa, email, privacy: false)));
        Assert.Equal(HttpStatusCode.BadRequest, await PostAsync(Request(spa, email, from: new DateOnly(2020, 1, 1), until: new DateOnly(2020, 1, 2))));
        Assert.Equal(HttpStatusCode.NotFound, await PostAsync(Request(spa, email) with { TreatmentId = Guid.NewGuid() }));

        Assert.Equal(HttpStatusCode.Created, await PostAsync(Request(spa, email)));
        Assert.Equal(HttpStatusCode.BadRequest, await PostAsync(Request(spa, email, from: TestSpa.Monday.AddDays(2), until: TestSpa.Monday.AddDays(3))));
        for (var week = 1; week < 5; week++)
        {
            Assert.Equal(HttpStatusCode.Created, await PostAsync(Request(spa, email, from: TestSpa.Monday.AddDays(7 * week), until: TestSpa.Monday.AddDays(7 * week + 1))));
        }

        Assert.Equal(HttpStatusCode.BadRequest, await PostAsync(Request(spa, email, from: TestSpa.Monday.AddDays(50), until: TestSpa.Monday.AddDays(51))));
    }

    [Fact]
    public async Task Ein_anderes_Spa_sieht_die_Warteliste_nicht()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var other = await TestSpa.CreateAsync(fixture, Ct);
        await JoinAsync(spa, NewEmail());

        var response = await other.Client.GetAsync($"/api/locations/{spa.Location.Id}/waitlist", Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Team_ohne_Kalenderzugang_sieht_die_Warteliste_nicht()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        using var anonymous = fixture.CreateClient();

        var response = await anonymous.GetAsync($"/api/locations/{spa.Location.Id}/waitlist", Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Die_Liste_der_Rezeption_zeigt_die_Telefonnummer_des_Gastes()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        using var client = spa.OnlineClient();
        var request = Request(spa, NewEmail()) with { Contact = new OnlineContact("Wanda", "Warte", NewEmail(), "+43 664 555 12 34") };

        var joined = await client.PostAsJsonAsync("/api/online/waitlist", request, TestSpa.Json, Ct);
        Assert.Equal(HttpStatusCode.Created, joined.StatusCode);

        var listed = Assert.Single(await spa.GetAsync<List<WaitlistStaffEntry>>($"/api/locations/{spa.Location.Id}/waitlist", Ct));
        Assert.Equal("+43 664 555 12 34", listed.Phone);
    }

    [Fact]
    public async Task Die_Rezeption_nimmt_einen_Wunsch_von_der_Liste_vereinbart_gilt_als_gebucht_sonst_als_ausgetragen()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var arranged = await JoinAsync(spa, NewEmail());
        var unwanted = await JoinAsync(spa, NewEmail());

        var first = await spa.Client.PostAsJsonAsync($"/api/waitlist/{arranged.Entry.Id}/close", new WaitlistCloseInput(WaitlistCloseReason.Arranged), TestSpa.Json, Ct);
        var second = await spa.Client.PostAsJsonAsync($"/api/waitlist/{unwanted.Entry.Id}/close", new WaitlistCloseInput(WaitlistCloseReason.NoLongerWanted), TestSpa.Json, Ct);

        Assert.Equal(WaitlistStatus.Booked, (await TestSpa.ReadAsync<WaitlistEntryDto>(first, Ct)).Status);
        Assert.Equal(WaitlistStatus.Cancelled, (await TestSpa.ReadAsync<WaitlistEntryDto>(second, Ct)).Status);
        Assert.Empty(await spa.GetAsync<List<WaitlistStaffEntry>>($"/api/locations/{spa.Location.Id}/waitlist", Ct));

        // Ein schon geschlossener Eintrag bleibt, wie er ist.
        var again = await spa.Client.PostAsJsonAsync($"/api/waitlist/{arranged.Entry.Id}/close", new WaitlistCloseInput(WaitlistCloseReason.NoLongerWanted), TestSpa.Json, Ct);
        Assert.Equal(WaitlistStatus.Booked, (await TestSpa.ReadAsync<WaitlistEntryDto>(again, Ct)).Status);
    }

    [Fact]
    public async Task Schliessen_braucht_das_Recht_zu_buchen_und_den_Standort_des_Eintrags()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var entry = await JoinAsync(spa, NewEmail());
        using var therapist = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Therapist, [spa.Location.Id], spa.Anna.Id);
        var other = (await TestSpa.CreateAsync(fixture, Ct)).Client;
        var input = new WaitlistCloseInput(WaitlistCloseReason.Arranged);

        Assert.Equal(HttpStatusCode.Forbidden, (await therapist.PostAsJsonAsync($"/api/waitlist/{entry.Entry.Id}/close", input, TestSpa.Json, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PostAsJsonAsync($"/api/waitlist/{entry.Entry.Id}/close", input, TestSpa.Json, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await spa.Client.PostAsJsonAsync($"/api/waitlist/{Guid.NewGuid()}/close", input, TestSpa.Json, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await spa.Client.PostAsJsonAsync($"/api/waitlist/{entry.Entry.Id}/close",
            new { reason = 99 }, TestSpa.Json, Ct)).StatusCode);
        // Nichts davon hat den Eintrag geschlossen.
        Assert.Single(await spa.GetAsync<List<WaitlistStaffEntry>>($"/api/locations/{spa.Location.Id}/waitlist", Ct));
    }
}
