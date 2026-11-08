using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using AispaStudio.Api.Closures;
using AispaStudio.Api.Online;
using AispaStudio.Contracts;
using Microsoft.AspNetCore.Http;

namespace AispaStudio.Api.Tests;

/// <summary>
/// Warteliste bei einer Schließzeit (ADR 0073, Ä48): Einträge, deren Wunschzeitraum in die Schließzeit fällt, stehen in der Rückfrage als eigener Abschnitt, werden mit einer kurzen
/// Vorlage in ihrer Sprache benachrichtigt, bleiben auf der Liste und stehen nach der Benachrichtigung nicht mehr in der Meldung; das Protokoll hält sie wie die Termine fest.
/// </summary>
public sealed class ClosureWaitlistTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly TimeZoneInfo Vienna = TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna");

    private static DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, Vienna).DateTime);

    /// <summary>Die Schließzeit: Montag, der 8. März 2027, bis Mittwoch.</summary>
    private static readonly DateOnly Day = TestSpa.Monday.AddDays(7);

    private static string Path(TestSpa spa, ClosureDto closure, string tail = "") => $"{ClosureTests.Path(spa)}/{closure.Id}{tail}";

    private static string NewEmail() => $"warteliste-{Guid.NewGuid():N}@example.at";

    private static async Task<WaitlistConfirmation> JoinAsync(TestSpa spa, string email, DateOnly from, DateOnly until, string language, DayPart part = DayPart.Any)
    {
        using var client = spa.OnlineClient();
        var request = new WaitlistRequest(spa.Location.Id, spa.MassageTreatment.Id, from, until, part, new OnlineContact("Wanda", "Warte", email, null), true, language);
        var response = await client.PostAsJsonAsync("/api/online/waitlist", request, TestSpa.Json, Ct);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await TestSpa.ReadAsync<WaitlistConfirmation>(response, Ct);
    }

    private static async Task<List<ClosureNoticeDto>> WaitForNoticesAsync(TestSpa spa, ClosureDto closure, int count)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));
        while (true)
        {
            var notices = await spa.GetAsync<List<ClosureNoticeDto>>(Path(spa, closure, "/notices"), timeout.Token);
            if (notices.Count >= count && notices.All(n => n.State != ClosureNoticeState.Queued))
            {
                return notices;
            }

            await Task.Delay(50, timeout.Token);
        }
    }

    private static CancelClosureAppointmentsRequest Request(IEnumerable<AppointmentDto> appointments, IEnumerable<Guid> waitlist) =>
        new([.. appointments.Select(a => a.Id)], "Absage: {Behandlung}", "Guten Tag {Anrede},\n\n{Behandlung}, {Zeit} fällt aus.\n\nIhr Team", false, false, [.. waitlist]);

    [Fact]
    public async Task Eintraege_im_Zeitraum_stehen_in_der_Rueckfrage_werden_in_ihrer_Sprache_benachrichtigt_und_bleiben_auf_der_Liste()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var (french, german, outside, left) = (NewEmail(), NewEmail(), NewEmail(), NewEmail());
        // Der Wunsch fällt (ganz oder teilweise) in die Schließzeit; einer davor, einer ist schon ausgetragen.
        var inFrench = await JoinAsync(spa, french, Day.AddDays(1), Day.AddDays(5), "fr");
        var inGerman = await JoinAsync(spa, german, Day.AddDays(-3), Day, "de");
        await JoinAsync(spa, outside, Day.AddDays(-6), Day.AddDays(-1), "de");
        var gone = await JoinAsync(spa, left, Day, Day.AddDays(2), "en");
        using (var client = spa.OnlineClient())
        {
            Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"/api/online/waitlist/{gone.ManageToken}/cancel", null, Ct)).StatusCode);
        }

        var closure = await ClosureTests.CreateAsync(spa, ClosureTests.Input(Day, Day.AddDays(2), ClosureKind.Vacation));
        Assert.Equal((0, 2), (closure.OpenAppointments, closure.OpenWaitlist));

        // Ohne offene Termine ist die Schließzeit trotzdem fällig: die Meldung nennt die Einträge.
        var calendar = await spa.GetAsync<CalendarDay>($"/api/locations/{spa.Location.Id}/calendar?date={Today:yyyy-MM-dd}", Ct);
        var band = Assert.Single(calendar.ClosuresDue!);
        Assert.Equal((closure.Id, 0, 2), (band.Id, band.OpenAppointments, band.WaitlistEntries));

        var preview = await spa.GetAsync<ClosureCancellationPreview>(Path(spa, closure, "/cancellation"), Ct);
        Assert.Empty(preview.Appointments);
        Assert.Equal([inGerman.Entry.Id, inFrench.Entry.Id], preview.Waitlist!.Select(w => w.EntryId));
        Assert.Equal(["de", "fr"], preview.Waitlist!.Select(w => w.Language));
        Assert.All(preview.Waitlist!, w => Assert.Equal(("Wanda Warte", "Massage", ClosureChannel.Email), (w.CustomerName, w.TreatmentName, w.Channel)));

        var result = await spa.PostAsync<CancelClosureAppointmentsResult>(Path(spa, closure, "/cancellation"), Request([], preview.Waitlist!.Select(w => w.EntryId)), Ct);

        Assert.Equal((0, 2, 0, 0, 2), (result.Cancelled, result.Queued, result.WithoutContact, result.Failed, result.WaitlistNotified));
        var frenchPeriod = ClosureText.Period(Day.AddDays(1), null, Day.AddDays(5), null, Today, "fr");
        var frenchMail = await fixture.Mailbox.WaitForAsync(french, m => m.Subject.StartsWith("Votre demande", StringComparison.Ordinal), Ct);
        Assert.Equal("Votre demande sur la liste d’attente tombe pendant une fermeture", frenchMail.Subject);
        Assert.StartsWith("Bonjour Wanda,\n\nvotre demande sur la liste d’attente tombe pendant une fermeture :\n\nMassage\n", frenchMail.Text, StringComparison.Ordinal);
        Assert.Contains($"Massage\n{frenchPeriod}\n\nNous sommes fermés pour congés annuels (", frenchMail.Text, StringComparison.Ordinal);
        Assert.Contains("Vous restez sur la liste d’attente", frenchMail.Text, StringComparison.Ordinal);
        var germanMail = await fixture.Mailbox.WaitForAsync(german, m => m.Subject.StartsWith("Ihr Wartelisten-Wunsch", StringComparison.Ordinal), Ct);
        Assert.Contains("Sie bleiben auf der Warteliste:", germanMail.Text, StringComparison.Ordinal);
        Assert.Contains($"Massage\n{ClosureText.Period(Day.AddDays(-3), null, Day, null, Today)}\n\nWegen unseres Betriebsurlaubs (", germanMail.Text, StringComparison.Ordinal);
        Assert.Single(fixture.Mailbox.To(french), m => m.Subject.StartsWith("Votre demande", StringComparison.Ordinal));

        // Die Einträge bleiben auf der Liste, der ausgetragene nicht.
        var staff = await spa.GetAsync<List<WaitlistStaffEntry>>($"/api/locations/{spa.Location.Id}/waitlist", Ct);
        Assert.Equal(3, staff.Count);
        Assert.All(staff, w => Assert.Equal(WaitlistStatus.Waiting, w.Status));

        // Protokoll wie bei den Terminen; abgesagt wurde kein Termin, die Schließzeit bleibt als Verlauf stehen.
        var notices = await WaitForNoticesAsync(spa, closure, 2);
        Assert.All(notices, n => Assert.Equal((ClosureNoticeState.Sent, "Test Person", false), (n.State, n.SentBy, n.OfferedRebooking)));
        Assert.All(notices, n => Assert.Null(n.AppointmentId));
        var frenchNotice = Assert.Single(notices, n => n.WaitlistEntryId == inFrench.Entry.Id);
        Assert.Equal(("fr", Day.AddDays(1), Day.AddDays(5), ClosureChannel.Email, "Massage"), (frenchNotice.Language, frenchNotice.WishFrom, frenchNotice.WishUntil, frenchNotice.Channel, frenchNotice.TreatmentName));
        var updated = await spa.GetAsync<ClosureDto>(Path(spa, closure), Ct);
        Assert.Equal((0, 0, 0), (updated.OpenAppointments, updated.CancelledAppointments, updated.OpenWaitlist));
        Assert.Equal(HttpStatusCode.Conflict, (await spa.Client.DeleteAsync(Path(spa, closure), Ct)).StatusCode);

        // Wer benachrichtigt ist, steht nicht mehr in Rückfrage und Meldung, und ein zweites Mal geht nichts hinaus.
        Assert.Empty((await spa.GetAsync<ClosureCancellationPreview>(Path(spa, closure, "/cancellation"), Ct)).Waitlist!);
        Assert.True((await spa.GetAsync<CalendarDay>($"/api/locations/{spa.Location.Id}/calendar?date={Today:yyyy-MM-dd}", Ct)).ClosuresDue is null or { Count: 0 });
        var again = await spa.Client.PostAsJsonAsync(Path(spa, closure, "/cancellation"), Request([], [inFrench.Entry.Id]), TestSpa.Json, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, again.StatusCode);
        Assert.Contains("waitlistEntryIds", (await again.Content.ReadFromJsonAsync<HttpValidationProblemDetails>(TestSpa.Json, Ct))!.Errors.Keys);
    }

    [Fact]
    public async Task Termine_und_Eintraege_gehen_in_einer_Anfrage_ein_nicht_gewaehlter_Eintrag_bleibt_offen()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var appointment = await spa.BookOkAsync(TestSpa.At(10, 0, Day), Ct);
        var (first, second) = (NewEmail(), NewEmail());
        var chosen = await JoinAsync(spa, first, Day, Day.AddDays(1), "de");
        var kept = await JoinAsync(spa, second, Day.AddDays(1), Day.AddDays(2), "de");
        var closure = await ClosureTests.CreateAsync(spa, ClosureTests.Input(Day, Day.AddDays(2)));

        var result = await spa.PostAsync<CancelClosureAppointmentsResult>(Path(spa, closure, "/cancellation"), Request([appointment], [chosen.Entry.Id]), Ct);

        Assert.Equal((1, 2, 1), (result.Cancelled, result.Queued, result.WaitlistNotified));
        Assert.Equal(2, result.Notices.Count);
        var updated = await spa.GetAsync<ClosureDto>(Path(spa, closure), Ct);
        // Die Zahl der abgesagten Termine zählt die Benachrichtigung der Warteliste nicht mit; der zweite Eintrag wartet auf seine Benachrichtigung.
        Assert.Equal((0, 1, 1), (updated.OpenAppointments, updated.CancelledAppointments, updated.OpenWaitlist));
        Assert.Equal(kept.Entry.Id, Assert.Single((await spa.GetAsync<ClosureCancellationPreview>(Path(spa, closure, "/cancellation"), Ct)).Waitlist!).EntryId);
        var calendar = await spa.GetAsync<CalendarDay>($"/api/locations/{spa.Location.Id}/calendar?date={Today:yyyy-MM-dd}", Ct);
        Assert.Equal((0, 1), (Assert.Single(calendar.ClosuresDue!).OpenAppointments, calendar.ClosuresDue![0].WaitlistEntries));
        Assert.DoesNotContain(fixture.Mailbox.To(second), m => m.Subject.StartsWith("Ihr Wartelisten-Wunsch", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Eine_Anfrage_ohne_Termin_und_ohne_Eintrag_ist_ungueltig_und_ein_Eintrag_ausserhalb_der_Schliesszeit_auch()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var outside = await JoinAsync(spa, NewEmail(), Day.AddDays(10), Day.AddDays(12), "de");
        var closure = await ClosureTests.CreateAsync(spa, ClosureTests.Input(Day));

        async Task<List<string>> FieldsAsync(CancelClosureAppointmentsRequest request)
        {
            var response = await spa.Client.PostAsJsonAsync(Path(spa, closure, "/cancellation"), request, TestSpa.Json, Ct);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            return [.. (await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>(TestSpa.Json, Ct))!.Errors.Keys];
        }

        Assert.Equal(["appointmentIds"], await FieldsAsync(Request([], [])));
        Assert.Equal(["waitlistEntryIds"], await FieldsAsync(Request([], [outside.Entry.Id])));
        Assert.Equal(["waitlistEntryIds"], await FieldsAsync(Request([], [Guid.NewGuid()])));
        Assert.Empty(await spa.GetAsync<List<ClosureNoticeDto>>(Path(spa, closure, "/notices"), Ct));
    }

    // ---------- Tageszeit bei halben Schließtagen (Ä54) ----------

    /// <summary>Je ein Wunsch für jede Tageszeit am selben Tag; gibt zurück, welche Tageszeiten die Rückfrage und die Zählung nennen.</summary>
    private async Task<(DayPart[] Parts, int Count)> PartsInClosureAsync(TimeOnly? fromTime, TimeOnly? untilTime, DateOnly? wishFrom = null, DateOnly? wishUntil = null)
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var entries = new Dictionary<Guid, DayPart>();
        foreach (var part in Enum.GetValues<DayPart>())
        {
            var joined = await JoinAsync(spa, NewEmail(), wishFrom ?? Day, wishUntil ?? Day, "de", part);
            entries[joined.Entry.Id] = part;
        }

        var closure = await ClosureTests.CreateAsync(spa, ClosureTests.Input(Day, fromTime: fromTime, untilTime: untilTime));
        var preview = await spa.GetAsync<ClosureCancellationPreview>(Path(spa, closure, "/cancellation"), Ct);
        var parts = preview.Waitlist!.Select(w => entries[w.EntryId]).Order().ToArray();
        // Meldung, Liste und Rückfrage zählen dasselbe.
        Assert.Equal(parts.Length, closure.OpenWaitlist);
        var calendar = await spa.GetAsync<CalendarDay>($"/api/locations/{spa.Location.Id}/calendar?date={Today:yyyy-MM-dd}", Ct);
        Assert.Equal(parts.Length, calendar.ClosuresDue?.Sum(c => c.WaitlistEntries) ?? 0);
        return (parts, closure.OpenWaitlist);
    }

    [Fact]
    public async Task Ist_nur_der_Vormittag_zu_zaehlen_Wuensche_ohne_Tageszeit_und_vormittags_aber_nicht_nachmittags_und_abends()
    {
        var (parts, count) = await PartsInClosureAsync(null, new TimeOnly(12, 0));

        Assert.Equal([DayPart.Any, DayPart.Morning], parts);
        Assert.Equal(2, count);
    }

    [Fact]
    public async Task Ist_nur_der_Nachmittag_und_Abend_zu_faellt_ein_Wunsch_vormittags_nicht_hinein()
    {
        var (parts, _) = await PartsInClosureAsync(new TimeOnly(14, 0), null);

        Assert.Equal([DayPart.Any, DayPart.Afternoon, DayPart.Evening], parts);
    }

    [Fact]
    public async Task Die_Grenzen_der_Tageszeiten_sind_Mittag_12_Uhr_und_17_Uhr_in_Ortszeit_des_Standorts()
    {
        // Schließt das Spa um 12 Uhr, ist der Vormittag offen; ab 17 Uhr betrifft es nur den Abend; bis 17 Uhr nicht den Abend.
        Assert.Equal([DayPart.Any, DayPart.Afternoon, DayPart.Evening], (await PartsInClosureAsync(new TimeOnly(12, 0), null)).Parts);
        Assert.Equal([DayPart.Any, DayPart.Evening], (await PartsInClosureAsync(new TimeOnly(17, 0), null)).Parts);
        Assert.Equal([DayPart.Any, DayPart.Morning, DayPart.Afternoon], (await PartsInClosureAsync(null, new TimeOnly(17, 0))).Parts);
    }

    [Fact]
    public async Task Ein_ganzer_Tag_Schliesszeit_betrifft_jede_Tageszeit_und_ein_Wunsch_ueber_mehrere_Tage_zaehlt_pro_Tag()
    {
        Assert.Equal([DayPart.Any, DayPart.Morning, DayPart.Afternoon, DayPart.Evening], (await PartsInClosureAsync(null, null)).Parts);

        // Die Schließzeit ist am 8. März ab 14 Uhr: ein Vormittags-Wunsch für den 7. bis 8. März fällt nicht hinein, ein Nachmittags-Wunsch schon.
        var (parts, _) = await PartsInClosureAsync(new TimeOnly(14, 0), null, Day.AddDays(-1), Day);
        Assert.Equal([DayPart.Any, DayPart.Afternoon, DayPart.Evening], parts);
    }

    [Theory]
    [InlineData("Europe/Vienna", "2027-03-29", DayPart.Afternoon, 12, 0, true, false)]
    [InlineData("Europe/Vienna", "2027-03-29", DayPart.Afternoon, 12, 1, true, true)]
    [InlineData("Europe/Vienna", "2027-03-29", DayPart.Morning, 12, 0, true, true)]
    [InlineData("Europe/Vienna", "2027-03-28", DayPart.Morning, 12, 0, false, false)]
    [InlineData("Europe/Vienna", "2027-03-28", DayPart.Afternoon, 16, 59, false, true)]
    [InlineData("Europe/Vienna", "2027-03-28", DayPart.Afternoon, 17, 0, false, false)]
    [InlineData("America/New_York", "2027-03-14", DayPart.Afternoon, 17, 0, false, false)]
    [InlineData("America/New_York", "2027-03-14", DayPart.Afternoon, 16, 59, false, true)]
    [InlineData("America/New_York", "2027-03-14", DayPart.Evening, 17, 0, false, true)]
    [InlineData("America/New_York", "2027-03-14", DayPart.Morning, 12, 0, false, false)]
    public void Die_Tageszeit_des_Wunsches_wird_in_Ortszeit_des_Standorts_auch_am_Tag_der_Zeitumstellung_gerechnet(
        string zoneId, string day, DayPart part, int hour, int minute, bool closedUntil, bool expected)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(zoneId);
        var date = DateOnly.Parse(day, CultureInfo.InvariantCulture);
        var time = new TimeOnly(hour, minute);
        // closedUntil: zu bis zur Uhrzeit, sonst ab der Uhrzeit.
        var closure = new Closure
        {
            LocationId = Guid.NewGuid(),
            FromDate = date,
            FromTime = closedUntil ? null : time,
            UntilDate = date,
            UntilTime = closedUntil ? time : null,
        };
        var entry = new WaitlistEntry { LocationId = closure.LocationId, From = date, Until = date, DayPart = part, Language = "de", PrivacyNoticeVersion = "1" };

        Assert.Equal(expected, ClosureService.WishFallsIn(entry, closure, zone));
    }
}
