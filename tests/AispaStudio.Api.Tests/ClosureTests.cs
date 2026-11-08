using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AispaStudio.Api.Closures;
using AispaStudio.Contracts;
using Microsoft.AspNetCore.Http;

namespace AispaStudio.Api.Tests;

/// <summary>
/// Schließzeiten (ADR 0073): anlegen, ändern und löschen mit Zustand (geplant, aktiv, beendet), Prüfregeln und Rechten, und was sie bewirken:
/// keine freien Zeiten und keine Buchung im Zeitraum (intern und online), halbe Tage, das Schließband im Kalender und der Hinweis im Kundenbereich.
/// </summary>
public sealed class ClosureTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly TimeZoneInfo Vienna = TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna");

    private static DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, Vienna).DateTime);

    /// <summary>Der Montag der Testtermine und die folgenden Montage (die Testpersonen arbeiten nur montags).</summary>
    private static DateOnly Monday(int weeks = 0) => TestSpa.Monday.AddDays(7 * weeks);

    internal static ClosureInput Input(DateOnly from, DateOnly? until = null, ClosureKind kind = ClosureKind.Vacation, TimeOnly? fromTime = null, TimeOnly? untilTime = null,
        string? reason = "Inventur", string? guestText = null) =>
        new(from, fromTime, until ?? from, untilTime, kind, reason, guestText);

    internal static string Path(TestSpa spa) => $"/api/locations/{spa.Location.Id}/closures";

    internal static Task<ClosureDto> CreateAsync(TestSpa spa, ClosureInput input) => spa.PostAsync<ClosureDto>(Path(spa), input, Ct);

    private static async Task<HttpValidationProblemDetails> ProblemAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>(TestSpa.Json, Ct))!;
    }

    // ---------- Verwaltung ----------

    [Fact]
    public async Task Schliesszeit_anlegen_aendern_und_loeschen_mit_Zustand_je_Zeitraum()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);

        var planned = await CreateAsync(spa, Input(Monday(2), Monday(2).AddDays(4)));
        var active = await CreateAsync(spa, Input(Today.AddDays(-1), Today.AddDays(1), ClosureKind.Outage, reason: "Wasserschaden", guestText: "Wegen eines Schadens zu."));
        var ended = await CreateAsync(spa, Input(new DateOnly(2026, 1, 10), new DateOnly(2026, 1, 12), ClosureKind.SpecialClosure));

        Assert.Equal((ClosurePhase.Planned, ClosurePhase.Active, ClosurePhase.Ended), (planned.Phase, active.Phase, ended.Phase));
        Assert.Equal(("Test Person", ClosureKind.Vacation, "Inventur", (string?)null), (planned.CreatedBy, planned.Kind, planned.InternalReason, planned.GuestText));
        Assert.Equal((TestSpa.At(0, 0, Monday(2)), TestSpa.At(0, 0, Monday(2).AddDays(5))), (planned.From, planned.Until));
        Assert.Equal(("Wegen eines Schadens zu.", "Wasserschaden"), (active.GuestText, active.InternalReason));
        Assert.Equal([planned.Id, active.Id, ended.Id], (await spa.GetAsync<List<ClosureDto>>(Path(spa), Ct)).Select(c => c.Id));
        Assert.Equal(planned.Id, (await spa.GetAsync<ClosureDto>($"{Path(spa)}/{planned.Id}", Ct)).Id);

        var changed = await spa.PutAsync<ClosureDto>($"{Path(spa)}/{planned.Id}",
            Input(Monday(2), Monday(2).AddDays(6), ClosureKind.SpecialClosure, new TimeOnly(13, 0), new TimeOnly(18, 0), "Betriebsfeier", "  Heute Nachmittag zu.  "), Ct);
        Assert.Equal((ClosureKind.SpecialClosure, "Heute Nachmittag zu.", "Betriebsfeier", new TimeOnly(13, 0)), (changed.Kind, changed.GuestText, changed.InternalReason, changed.FromTime));
        Assert.Equal((TestSpa.At(13, 0, Monday(2)), TestSpa.At(18, 0, Monday(2).AddDays(6))), (changed.From, changed.Until));

        Assert.Equal(HttpStatusCode.NoContent, (await spa.Client.DeleteAsync($"{Path(spa)}/{ended.Id}", Ct)).StatusCode);
        Assert.DoesNotContain(await spa.GetAsync<List<ClosureDto>>(Path(spa), Ct), c => c.Id == ended.Id);
        Assert.Equal(HttpStatusCode.NotFound, (await spa.Client.DeleteAsync($"{Path(spa)}/{ended.Id}", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await spa.Client.GetAsync($"{Path(spa)}/{Guid.NewGuid()}", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await spa.Client.PutAsJsonAsync($"{Path(spa)}/{Guid.NewGuid()}", Input(Monday(5)), TestSpa.Json, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await spa.Client.PostAsJsonAsync($"/api/locations/{Guid.NewGuid()}/closures", Input(Monday(5)), TestSpa.Json, Ct)).StatusCode);
    }

    [Fact]
    public async Task Pruefregeln_Reihenfolge_halbe_Tage_Ueberschneidung_Laenge_und_Texte()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var existing = await CreateAsync(spa, Input(Monday(3), Monday(3).AddDays(4)));

        async Task<List<string>> FieldsAsync(ClosureInput input)
        {
            var problem = await ProblemAsync(await spa.Client.PostAsJsonAsync(Path(spa), input, TestSpa.Json, Ct));
            return [.. problem.Errors.Keys.Order(StringComparer.Ordinal)];
        }

        Assert.Equal(["untilDate"], await FieldsAsync(Input(Monday(6), Monday(6).AddDays(-1))));
        Assert.Equal(["untilTime"], await FieldsAsync(Input(Monday(6), fromTime: new TimeOnly(14, 0), untilTime: new TimeOnly(13, 0))));
        Assert.Equal(["untilTime"], await FieldsAsync(Input(Monday(6), fromTime: new TimeOnly(14, 0), untilTime: new TimeOnly(14, 0))));
        Assert.Equal(["fromDate"], await FieldsAsync(Input(Monday(3).AddDays(2), Monday(3).AddDays(8))));
        Assert.Equal(["untilDate"], await FieldsAsync(Input(Monday(6), Monday(6).AddDays(400))));
        Assert.Equal(["internalReason"], await FieldsAsync(Input(Monday(6), reason: new string('x', 201))));
        Assert.Equal(["guestText"], await FieldsAsync(Input(Monday(6), guestText: new string('x', 201))));
        Assert.Equal(["kind"], await FieldsAsync(Input(Monday(6)) with { Kind = (ClosureKind)99 }));

        // Gültige Randfälle: ein Tag direkt nach der anderen Schließzeit, ein halber Tag, ohne Texte, und die eigene Zeit beim Ändern.
        var next = await CreateAsync(spa, Input(Monday(3).AddDays(5)));
        var half = await CreateAsync(spa, Input(Monday(6), fromTime: new TimeOnly(13, 0), reason: null));
        Assert.Equal((null, null), (half.InternalReason, half.GuestText));
        Assert.Equal((TestSpa.At(13, 0, Monday(6)), TestSpa.At(0, 0, Monday(6).AddDays(1))), (half.From, half.Until));
        await spa.PutAsync<ClosureDto>($"{Path(spa)}/{existing.Id}", Input(Monday(3), Monday(3).AddDays(4), reason: "Neu"), Ct);
        Assert.NotEqual(existing.Id, next.Id);
    }

    [Fact]
    public async Task Verwalten_nur_mit_Standortrecht_Lesen_mit_Kalender_und_nur_am_eigenen_Standort()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var other = await spa.CreateLocationAsync("Zweiter Standort", [DayOfWeek.Monday], Ct);
        var closure = await CreateAsync(spa, Input(Monday(4)));
        using var reception = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Reception, [spa.Location.Id]);
        using var therapist = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Therapist, [spa.Location.Id]);
        using var backoffice = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Backoffice, [spa.Location.Id]);
        using var manager = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Manager, [spa.Location.Id]);
        using var elsewhere = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Manager, [other.Id]);
        using var anonymous = fixture.CreateClient();
        var path = Path(spa);

        // Lesen: jeder mit Kalender, nicht das Backoffice und nicht ohne Anmeldung.
        Assert.Equal(HttpStatusCode.OK, (await reception.GetAsync(path, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await therapist.GetAsync($"{path}/{closure.Id}", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await backoffice.GetAsync(path, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(path, Ct)).StatusCode);

        // Verwalten und Absagen: Rezeption und Therapeut:in nicht, auch nicht Termine absagen.
        foreach (var client in new[] { reception, therapist })
        {
            Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync(path, Input(Monday(8)), TestSpa.Json, Ct)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.PutAsJsonAsync($"{path}/{closure.Id}", Input(Monday(8)), TestSpa.Json, Ct)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.DeleteAsync($"{path}/{closure.Id}", Ct)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"{path}/{closure.Id}/cancellation", Ct)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"{path}/{closure.Id}/notices", Ct)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync($"{path}/{closure.Id}/cancellation",
                new CancelClosureAppointmentsRequest([Guid.NewGuid()], "Betreff", "Text", false, false), TestSpa.Json, Ct)).StatusCode);
        }

        // Die Standortleitung verwaltet ihren Standort, einen anderen nicht.
        Assert.Equal(HttpStatusCode.Created, (await manager.PostAsJsonAsync(path, Input(Monday(9)), TestSpa.Json, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await elsewhere.PostAsJsonAsync(path, Input(Monday(10)), TestSpa.Json, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await elsewhere.GetAsync(path, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync(path, Input(Monday(10)), TestSpa.Json, Ct)).StatusCode);
    }

    // ---------- Wirkung ----------

    [Fact]
    public async Task Im_Zeitraum_gibt_es_keine_freien_Zeiten_intern_und_online_und_die_Rezeption_traegt_mit_Hinweis_ein()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var closedDay = Monday(1);
        var openDay = Monday(2);
        string Availability(DateOnly day) => $"/api/locations/{spa.Location.Id}/availability?treatmentId={spa.MassageTreatment.Id}&date={day:yyyy-MM-dd}";
        async Task<int> OnlineTimesAsync(DateOnly day)
        {
            var json = await spa.OnlineClient().GetStringAsync(
                $"/api/online/locations/{spa.Location.Id}/availability?treatmentId={spa.MassageTreatment.Id}&from={day:yyyy-MM-dd}&days=1", Ct);
            return JsonSerializer.Deserialize<OnlineAvailability>(json, TestSpa.Json)!.Days[0].Times.Count;
        }

        // Vorher gibt es an beiden Montagen Zeiten.
        Assert.NotEmpty(await spa.GetAsync<List<AvailableSlot>>(Availability(closedDay), Ct));
        Assert.True(await OnlineTimesAsync(closedDay) > 0);

        await CreateAsync(spa, Input(closedDay, kind: ClosureKind.Vacation));

        Assert.Empty(await spa.GetAsync<List<AvailableSlot>>(Availability(closedDay), Ct));
        Assert.Equal(0, await OnlineTimesAsync(closedDay));
        Assert.NotEmpty(await spa.GetAsync<List<AvailableSlot>>(Availability(openDay), Ct));
        Assert.True(await OnlineTimesAsync(openDay) > 0);

        // Die Rezeption trägt an einem Schließtag ein (Ruling Runde R03, Ä89); Gäste sehen online weiter keine Zeiten.
        var closedBooking = await spa.BookAsync(TestSpa.At(10, 0, closedDay), Ct);
        Assert.Equal(HttpStatusCode.Created, closedBooking.StatusCode);
        Assert.Equal(["Der Standort hat an diesem Tag geschlossen."], (await TestSpa.ReadAsync<AppointmentDto>(closedBooking, Ct)).Warnings);
        Assert.Equal(HttpStatusCode.Created, (await spa.BookAsync(TestSpa.At(10, 0, openDay), Ct)).StatusCode);
    }

    [Fact]
    public async Task Ein_halber_Tag_laesst_den_Rest_offen_und_Termine_muessen_davor_enden()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var day = Monday(1);
        await CreateAsync(spa, Input(day, kind: ClosureKind.Outage, fromTime: new TimeOnly(13, 0)));

        var slots = await spa.GetAsync<List<AvailableSlot>>($"/api/locations/{spa.Location.Id}/availability?treatmentId={spa.MassageTreatment.Id}&date={day:yyyy-MM-dd}", Ct);

        Assert.NotEmpty(slots);
        Assert.All(slots, s => Assert.True(s.End <= TestSpa.At(13, 0, day), $"{s.Start:o} bis {s.End:o} reicht in die Schließzeit"));
        Assert.Contains(slots, s => s.Start == TestSpa.At(12, 0, day));

        // Eine Massage von 12:30 bis 13:20 reicht in die Schließzeit, eine von 12:00 bis 12:50 nicht.
        Assert.Equal(HttpStatusCode.Conflict, (await spa.BookAsync(TestSpa.At(12, 30, day), Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await spa.BookAsync(TestSpa.At(12, 0, day), Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await spa.BookAsync(TestSpa.At(15, 0, day), Ct)).StatusCode);
    }

    [Fact]
    public async Task Der_Kalender_zeigt_das_Schliessband_mit_Art_Text_und_offenen_Terminen()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var day = Monday(1);
        var booked = await spa.BookOkAsync(TestSpa.At(10, 0, day), Ct);
        var closure = await CreateAsync(spa, Input(day, day.AddDays(2), ClosureKind.Vacation, reason: "Inventur"));
        await CreateAsync(spa, Input(Monday(2), kind: ClosureKind.Outage, guestText: "Heute leider zu."));

        var calendar = await spa.GetAsync<CalendarDay>($"/api/locations/{spa.Location.Id}/calendar?date={day:yyyy-MM-dd}", Ct);
        var band = Assert.Single(calendar.Closures!);
        Assert.Equal((closure.Id, "Betriebsurlaub", "Wegen Betriebsurlaub geschlossen.", "Inventur", 1), (band.Id, band.Title, band.Text, band.InternalReason, band.OpenAppointments));
        Assert.Equal((closure.From, closure.Until, ClosureKind.Vacation), (band.From, band.Until, band.Kind));

        // Der Termin bleibt, bis jemand ihn absagt.
        Assert.Contains(calendar.Appointments, a => a.Id == booked.Id && a.Status == AppointmentStatus.Booked);

        var outage = await spa.GetAsync<CalendarDay>($"/api/locations/{spa.Location.Id}/calendar?date={Monday(2):yyyy-MM-dd}", Ct);
        var outageBand = Assert.Single(outage.Closures!);
        Assert.Equal(("Ausfall", "Heute leider zu.", 0), (outageBand.Title, outageBand.Text, outageBand.OpenAppointments));
        var free = await spa.GetAsync<CalendarDay>($"/api/locations/{spa.Location.Id}/calendar?date={Monday(5):yyyy-MM-dd}", Ct);
        Assert.True(free.Closures is null or { Count: 0 });
    }

    [Fact]
    public async Task Schliesszeiten_mit_offenen_Terminen_stehen_im_Kalender_jedes_Tages_gleich_wie_fern_laufende_ohne_offene_Termine_nicht()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        // Der nächste Montag (die Testpersonen arbeiten montags), sieben bis dreizehn Tage voraus, und einer 70 Tage danach: keine Grenze nach vorn (Ä49).
        var soon = Today.AddDays((((int)DayOfWeek.Monday - (int)Today.DayOfWeek + 7) % 7) + 7);
        var far = soon.AddDays(70);
        await spa.BookOkAsync(TestSpa.At(10, 0, soon), Ct);
        await spa.BookOkAsync(TestSpa.At(10, 0, far), Ct);
        var due = await CreateAsync(spa, Input(soon, kind: ClosureKind.Outage));
        var distant = await CreateAsync(spa, Input(far));
        await CreateAsync(spa, Input(soon.AddDays(14)));

        var calendar = await spa.GetAsync<CalendarDay>($"/api/locations/{spa.Location.Id}/calendar?date={Today:yyyy-MM-dd}", Ct);

        Assert.Equal([due.Id, distant.Id], calendar.ClosuresDue!.Select(b => b.Id));
        var band = calendar.ClosuresDue![0];
        Assert.Equal((due.Id, "Ausfall", 1, 0), (band.Id, band.Title, band.OpenAppointments, band.WaitlistEntries));
        Assert.Equal([true, false], calendar.ClosuresDue.Select(b => b.Urgent));
        Assert.True(calendar.Closures is null or { Count: 0 });
        // Am Tag der Schließzeit steht sie auch im Band.
        var onDay = await spa.GetAsync<CalendarDay>($"/api/locations/{spa.Location.Id}/calendar?date={soon:yyyy-MM-dd}", Ct);
        Assert.Equal(due.Id, Assert.Single(onDay.Closures!).Id);
        Assert.Contains(onDay.ClosuresDue!, b => b.Id == due.Id);
    }

    [Fact]
    public async Task Die_Meldung_ist_dringend_wenn_die_Schliesszeit_laeuft_oder_in_14_Tagen_beginnt_sonst_nicht()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var monday = Today.AddDays((((int)DayOfWeek.Monday - (int)Today.DayOfWeek + 7) % 7) + 7);
        // Drei Montage mit einem Termin: bald (7 bis 13 Tage), in gut vier Wochen und in gut einem Jahr.
        var dates = new[] { monday, monday.AddDays(28), monday.AddDays(364) };
        foreach (var date in dates)
        {
            await spa.BookOkAsync(TestSpa.At(10, 0, date), Ct);
            await CreateAsync(spa, Input(date));
        }

        var calendar = await spa.GetAsync<CalendarDay>($"/api/locations/{spa.Location.Id}/calendar?date={Today:yyyy-MM-dd}", Ct);

        Assert.Equal([true, false, false], calendar.ClosuresDue!.Select(b => b.Urgent));
        Assert.Equal(3, calendar.ClosuresDue!.Count);
    }

    [Fact]
    public void Dringend_ist_eine_Schliesszeit_die_laeuft_oder_hoechstens_14_Tage_nach_jetzt_beginnt_gerechnet_in_Ortszeit()
    {
        Assert.Equal(14, ClosureService.UrgentDays);
        var closure = new Closure { LocationId = Guid.NewGuid(), FromDate = new DateOnly(2027, 3, 15), UntilDate = new DateOnly(2027, 3, 19) };
        // Sie beginnt am 15.3. um 0:00 Uhr Wiener Zeit, das ist der 14.3. um 23:00 Uhr UTC.
        Assert.Equal(new DateTimeOffset(2027, 3, 14, 23, 0, 0, TimeSpan.Zero), closure.StartsAt(Vienna));

        Assert.False(ClosureService.IsUrgent(closure, Vienna, new DateTimeOffset(2027, 2, 28, 0, 0, 0, TimeSpan.Zero)));
        Assert.False(ClosureService.IsUrgent(closure, Vienna, new DateTimeOffset(2027, 2, 28, 22, 59, 59, TimeSpan.Zero)));
        Assert.True(ClosureService.IsUrgent(closure, Vienna, new DateTimeOffset(2027, 2, 28, 23, 0, 0, TimeSpan.Zero)));
        Assert.True(ClosureService.IsUrgent(closure, Vienna, new DateTimeOffset(2027, 3, 10, 8, 0, 0, TimeSpan.Zero)));
        // Läuft sie schon, ist sie dringend.
        Assert.True(ClosureService.IsUrgent(closure, Vienna, new DateTimeOffset(2027, 3, 16, 8, 0, 0, TimeSpan.Zero)));
    }

    [Fact]
    public async Task Der_Kundenbereich_nennt_laufende_und_kommende_Schliesszeiten_mit_eigenem_Text_fuer_Gaeste_ohne_internen_Grund()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await CreateAsync(spa, Input(Monday(6), Monday(6).AddDays(6), ClosureKind.Vacation, reason: "Geheimer interner Grund"));
        await CreateAsync(spa, Input(Monday(9), kind: ClosureKind.SpecialClosure, guestText: "Am Brückentag bleibt das Spa zu."));
        await CreateAsync(spa, Input(new DateOnly(2026, 1, 10), new DateOnly(2026, 1, 12)));

        var json = await spa.OnlineClient().GetStringAsync("/api/online/locations", Ct);
        var location = JsonSerializer.Deserialize<List<OnlineLocation>>(json, TestSpa.Json)!.Single(l => l.Id == spa.Location.Id);

        Assert.Equal(2, location.Closures!.Count);
        Assert.Equal((Monday(6), Monday(6).AddDays(6), ClosureKind.Vacation, (string?)null),
            (location.Closures[0].FromDate, location.Closures[0].UntilDate, location.Closures[0].Kind, location.Closures[0].Text));
        Assert.Equal("Am Brückentag bleibt das Spa zu.", location.Closures[1].Text);
        Assert.DoesNotContain("Geheimer interner Grund", json, StringComparison.Ordinal);
    }
}
