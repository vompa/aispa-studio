using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AispaStudio.Contracts;
using Microsoft.AspNetCore.Http;

namespace AispaStudio.Api.Tests;

/// <summary>
/// Raumsperren (ADR 0076): anlegen, ändern und löschen mit Zustand (geplant, aktiv, beendet), Prüfregeln und Rechten, und was sie bewirken: der Planer wählt den gesperrten Raum
/// nie (freie Zeiten intern und online, Buchen, Raumpflege davor), das Band im Kalender mit offenen Terminen, die Umbuchungshilfe und die Bereiche (Sauna).
/// </summary>
public sealed class RoomClosureTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly TimeZoneInfo Vienna = TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna");

    private static DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, Vienna).DateTime);

    /// <summary>Der Montag der Testtermine und die folgenden Montage (die Testpersonen arbeiten nur montags).</summary>
    private static DateOnly Monday(int weeks = 0) => TestSpa.Monday.AddDays(7 * weeks);

    private static RoomClosureInput Input(RoomDto room, DateOnly from, DateOnly? until = null, RoomClosureKind kind = RoomClosureKind.Maintenance, TimeOnly? fromTime = null,
        TimeOnly? untilTime = null, string? reason = "Lüftung") =>
        new(room.Id, from, fromTime, until ?? from, untilTime, kind, reason);

    private static string Path(TestSpa spa) => $"/api/locations/{spa.Location.Id}/room-closures";

    private static Task<RoomClosureDto> CreateAsync(TestSpa spa, RoomClosureInput input) => spa.PostAsync<RoomClosureDto>(Path(spa), input, Ct);

    private static async Task<HttpValidationProblemDetails> ProblemAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>(TestSpa.Json, Ct))!;
    }

    private static string Availability(TestSpa spa, DateOnly day) =>
        $"/api/locations/{spa.Location.Id}/availability?treatmentId={spa.MassageTreatment.Id}&date={day:yyyy-MM-dd}";

    private static string CalendarPath(TestSpa spa, DateOnly day) => $"/api/locations/{spa.Location.Id}/calendar?date={day:yyyy-MM-dd}";

    // ---------- Verwaltung ----------

    [Fact]
    public async Task Raumsperre_anlegen_aendern_und_loeschen_mit_Zustand_je_Zeitraum()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);

        var planned = await CreateAsync(spa, Input(spa.RoomA, Monday(2), Monday(2).AddDays(4)));
        var active = await CreateAsync(spa, Input(spa.RoomB, Today.AddDays(-1), Today.AddDays(1), RoomClosureKind.Damage, reason: "Wasserschaden"));
        var ended = await CreateAsync(spa, Input(spa.RoomA, new DateOnly(2026, 1, 10), new DateOnly(2026, 1, 12), RoomClosureKind.Renovation));

        Assert.Equal((ClosurePhase.Planned, ClosurePhase.Active, ClosurePhase.Ended), (planned.Phase, active.Phase, ended.Phase));
        Assert.Equal(("Test Person", RoomClosureKind.Maintenance, "Lüftung", "Raum A", spa.Location.Id), (planned.CreatedBy, planned.Kind, planned.Reason, planned.RoomName, planned.LocationId));
        Assert.Equal((TestSpa.At(0, 0, Monday(2)), TestSpa.At(0, 0, Monday(2).AddDays(5))), (planned.From, planned.Until));
        Assert.Equal(("Wasserschaden", "Raum B"), (active.Reason, active.RoomName));
        Assert.Equal([planned.Id, active.Id, ended.Id], (await spa.GetAsync<List<RoomClosureDto>>(Path(spa), Ct)).Select(c => c.Id));
        Assert.Equal(planned.Id, (await spa.GetAsync<RoomClosureDto>($"{Path(spa)}/{planned.Id}", Ct)).Id);

        var changed = await spa.PutAsync<RoomClosureDto>($"{Path(spa)}/{planned.Id}",
            Input(spa.RoomA, Monday(2), Monday(2).AddDays(6), RoomClosureKind.Other, new TimeOnly(13, 0), new TimeOnly(18, 0), "  Gerät tauschen  "), Ct);
        Assert.Equal((RoomClosureKind.Other, "Gerät tauschen", new TimeOnly(13, 0)), (changed.Kind, changed.Reason, changed.FromTime));
        Assert.Equal((TestSpa.At(13, 0, Monday(2)), TestSpa.At(18, 0, Monday(2).AddDays(6))), (changed.From, changed.Until));

        // Löschen hebt die Sperre auf, auch eine beendete; ein zweites Mal gibt es sie nicht mehr.
        Assert.Equal(HttpStatusCode.NoContent, (await spa.Client.DeleteAsync($"{Path(spa)}/{ended.Id}", Ct)).StatusCode);
        Assert.DoesNotContain(await spa.GetAsync<List<RoomClosureDto>>(Path(spa), Ct), c => c.Id == ended.Id);
        Assert.Equal(HttpStatusCode.NotFound, (await spa.Client.DeleteAsync($"{Path(spa)}/{ended.Id}", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await spa.Client.GetAsync($"{Path(spa)}/{Guid.NewGuid()}", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await spa.Client.PutAsJsonAsync($"{Path(spa)}/{Guid.NewGuid()}", Input(spa.RoomA, Monday(5)), TestSpa.Json, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await spa.Client.PostAsJsonAsync($"/api/locations/{Guid.NewGuid()}/room-closures", Input(spa.RoomA, Monday(5)), TestSpa.Json, Ct)).StatusCode);
    }

    [Fact]
    public async Task Pruefregeln_Raum_Reihenfolge_halbe_Tage_Ueberschneidung_nur_im_selben_Raum_und_Laenge()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var other = await spa.CreateLocationAsync("Zweiter Standort", [DayOfWeek.Monday], Ct);
        var otherRoom = await spa.PostAsync<RoomDto>($"/api/locations/{other.Id}/rooms", new RoomInput("Fremder Raum", 0), Ct);
        var existing = await CreateAsync(spa, Input(spa.RoomA, Monday(3), Monday(3).AddDays(4)));

        async Task<List<string>> FieldsAsync(RoomClosureInput input)
        {
            var problem = await ProblemAsync(await spa.Client.PostAsJsonAsync(Path(spa), input, TestSpa.Json, Ct));
            return [.. problem.Errors.Keys.Order(StringComparer.Ordinal)];
        }

        Assert.Equal(["untilDate"], await FieldsAsync(Input(spa.RoomA, Monday(6), Monday(6).AddDays(-1))));
        Assert.Equal(["untilTime"], await FieldsAsync(Input(spa.RoomA, Monday(6), fromTime: new TimeOnly(14, 0), untilTime: new TimeOnly(13, 0))));
        Assert.Equal(["fromDate"], await FieldsAsync(Input(spa.RoomA, Monday(3).AddDays(2), Monday(3).AddDays(8))));
        Assert.Equal(["untilDate"], await FieldsAsync(Input(spa.RoomA, Monday(6), Monday(6).AddDays(400))));
        Assert.Equal(["reason"], await FieldsAsync(Input(spa.RoomA, Monday(6), reason: new string('x', 201))));
        Assert.Equal(["kind"], await FieldsAsync(Input(spa.RoomA, Monday(6)) with { Kind = (RoomClosureKind)99 }));
        // Ein Raum, den es nicht gibt oder der zu einem anderen Standort gehört.
        Assert.Equal(["roomId"], await FieldsAsync(Input(spa.RoomA, Monday(6)) with { RoomId = Guid.NewGuid() }));
        Assert.Equal(["roomId"], await FieldsAsync(Input(otherRoom, Monday(6))));

        // Gültige Randfälle: derselbe Zeitraum in einem anderen Raum, der Tag direkt danach im selben Raum, ein halber Tag, ohne Grund, die eigene Zeit beim Ändern.
        var sameTimeOtherRoom = await CreateAsync(spa, Input(spa.RoomB, Monday(3), Monday(3).AddDays(4)));
        var next = await CreateAsync(spa, Input(spa.RoomA, Monday(3).AddDays(5)));
        var half = await CreateAsync(spa, Input(spa.RoomA, Monday(6), fromTime: new TimeOnly(13, 0), reason: null));
        Assert.Null(half.Reason);
        Assert.Equal((TestSpa.At(13, 0, Monday(6)), TestSpa.At(0, 0, Monday(6).AddDays(1))), (half.From, half.Until));
        await spa.PutAsync<RoomClosureDto>($"{Path(spa)}/{existing.Id}", Input(spa.RoomA, Monday(3), Monday(3).AddDays(4), reason: "Neu"), Ct);
        Assert.Equal(3, new[] { existing.Id, sameTimeOtherRoom.Id, next.Id }.Distinct().Count());

        // Der Raum einer Sperre ändert sich nicht.
        var change = await spa.Client.PutAsJsonAsync($"{Path(spa)}/{existing.Id}", Input(spa.RoomB, Monday(3), Monday(3).AddDays(4)), TestSpa.Json, Ct);
        Assert.Equal(["roomId"], (await ProblemAsync(change)).Errors.Keys.ToList());
    }

    [Fact]
    public async Task Verwalten_nur_mit_Standortrecht_Lesen_mit_Kalender_und_nur_am_eigenen_Standort()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var other = await spa.CreateLocationAsync("Zweiter Standort", [DayOfWeek.Monday], Ct);
        var closure = await CreateAsync(spa, Input(spa.RoomA, Monday(4)));
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

        // Verwalten: Rezeption und Therapeut:in nicht; die Umbuchungshilfe verlangt das Recht zu buchen.
        foreach (var client in new[] { reception, therapist })
        {
            Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync(path, Input(spa.RoomA, Monday(8)), TestSpa.Json, Ct)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.PutAsJsonAsync($"{path}/{closure.Id}", Input(spa.RoomA, Monday(8)), TestSpa.Json, Ct)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.DeleteAsync($"{path}/{closure.Id}", Ct)).StatusCode);
        }

        Assert.Equal(HttpStatusCode.OK, (await reception.GetAsync($"{path}/{closure.Id}/rebooking", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await backoffice.GetAsync($"{path}/{closure.Id}/rebooking", Ct)).StatusCode);

        // Die Standortleitung verwaltet ihren Standort, einen anderen nicht.
        Assert.Equal(HttpStatusCode.Created, (await manager.PostAsJsonAsync(path, Input(spa.RoomA, Monday(9)), TestSpa.Json, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await elsewhere.PostAsJsonAsync(path, Input(spa.RoomA, Monday(10)), TestSpa.Json, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await elsewhere.GetAsync(path, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync(path, Input(spa.RoomA, Monday(10)), TestSpa.Json, Ct)).StatusCode);
    }

    // ---------- Wirkung im Planer ----------

    [Fact]
    public async Task Der_gesperrte_Raum_wird_nicht_gewaehlt_die_freien_Zeiten_liegen_im_anderen_Raum_und_die_Buchung_im_gesperrten_Raum_wird_abgelehnt()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var day = Monday(1);
        Assert.Contains(await spa.GetAsync<List<AvailableSlot>>(Availability(spa, day), Ct), s => s.RoomId == spa.RoomA.Id);

        await CreateAsync(spa, Input(spa.RoomA, day, kind: RoomClosureKind.Maintenance, reason: "Lüftung"));

        var slots = await spa.GetAsync<List<AvailableSlot>>(Availability(spa, day), Ct);
        Assert.NotEmpty(slots);
        Assert.All(slots, s => Assert.Equal(spa.RoomB.Id, s.RoomId));
        // Ohne Raumwunsch wählt der Server den freien Raum; mit Wunsch für den gesperrten Raum lehnt er ab und nennt den Grund.
        Assert.Equal(spa.RoomB.Id, (await spa.BookOkAsync(TestSpa.At(10, 0, day), Ct)).RoomId);
        var rejected = await spa.BookAsync(TestSpa.At(10, 0, day), Ct, room: spa.RoomA.Id, employee: spa.Ben.Id);
        Assert.Equal(HttpStatusCode.Conflict, rejected.StatusCode);
        var problem = (await rejected.Content.ReadFromJsonAsync<BookingRejected>(TestSpa.Json, Ct))!;
        var conflict = Assert.Single(problem.Conflicts);
        Assert.Equal((ResourceKind.Room, spa.RoomA.Id, "ist gesperrt (Wartung: Lüftung)", true), (conflict.Kind, conflict.ResourceId, conflict.Reason, conflict.IsBlock));
        Assert.Equal((TestSpa.At(0, 0, day), TestSpa.At(0, 0, day.AddDays(1))), (conflict.From, conflict.Until));
        // Ein anderer Tag ist unberührt.
        Assert.Contains(await spa.GetAsync<List<AvailableSlot>>(Availability(spa, Monday(2)), Ct), s => s.RoomId == spa.RoomA.Id);
    }

    [Fact]
    public async Task Sind_alle_Raeume_gesperrt_gibt_es_keine_freien_Zeiten_intern_und_online_und_die_Ablehnung_nennt_die_Raeume()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var day = Monday(1);
        async Task<int> OnlineTimesAsync()
        {
            var json = await spa.OnlineClient().GetStringAsync(
                $"/api/online/locations/{spa.Location.Id}/availability?treatmentId={spa.MassageTreatment.Id}&from={day:yyyy-MM-dd}&days=1", Ct);
            return JsonSerializer.Deserialize<OnlineAvailability>(json, TestSpa.Json)!.Days[0].Times.Count;
        }

        Assert.True(await OnlineTimesAsync() > 0);
        await CreateAsync(spa, Input(spa.RoomA, day, kind: RoomClosureKind.Damage, reason: null));
        await CreateAsync(spa, Input(spa.RoomB, day, kind: RoomClosureKind.Renovation));

        Assert.Empty(await spa.GetAsync<List<AvailableSlot>>(Availability(spa, day), Ct));
        Assert.Equal(0, await OnlineTimesAsync());
        var rejected = await spa.BookAsync(TestSpa.At(10, 0, day), Ct);
        Assert.Equal(HttpStatusCode.Conflict, rejected.StatusCode);
        var problem = (await rejected.Content.ReadFromJsonAsync<BookingRejected>(TestSpa.Json, Ct))!;
        Assert.Equal(["ist gesperrt (Schaden)", "ist gesperrt (Umbau: Lüftung)"], problem.Conflicts.Where(c => c.Kind == ResourceKind.Room && c.ResourceId != Guid.Empty).Select(c => c.Reason).Order());
        Assert.Contains(problem.Conflicts, c => c.ResourceName == "Räume");
    }

    [Fact]
    public async Task Ein_halber_Tag_laesst_den_Rest_offen_und_die_Raumpflege_vor_der_Sperre_zaehlt_mit()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var day = Monday(1);
        // Raum A (Raumpflege 15 Minuten) ist ab 13:10 gesperrt. Eine Massage um 12:00 bindet den Raum bis 13:00 plus 15 Minuten Raumpflege, also in die Sperre.
        await CreateAsync(spa, Input(spa.RoomA, day, fromTime: new TimeOnly(13, 10)));

        var inA = await spa.BookAsync(TestSpa.At(12, 0, day), Ct, room: spa.RoomA.Id);
        Assert.Equal(HttpStatusCode.Conflict, inA.StatusCode);
        Assert.Equal(spa.RoomA.Id, Assert.Single((await inA.Content.ReadFromJsonAsync<BookingRejected>(TestSpa.Json, Ct))!.Conflicts).ResourceId);
        // Eine Viertelstunde früher reicht die Raumpflege bis 13:00; im Raum B (ohne Raumpflege) geht auch 12:00.
        Assert.Equal(HttpStatusCode.Created, (await spa.BookAsync(TestSpa.At(11, 45, day), Ct, room: spa.RoomA.Id)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await spa.BookAsync(TestSpa.At(12, 0, day), Ct, room: spa.RoomB.Id, employee: spa.Ben.Id)).StatusCode);
        // Nach der Sperre ist der Raum wieder gesperrt bis zum Ende des Tages; am Nachmittag bleibt nur Raum B.
        var afternoon = (await spa.GetAsync<List<AvailableSlot>>(Availability(spa, day), Ct)).Where(s => s.Start >= TestSpa.At(14, 0, day)).ToList();
        Assert.NotEmpty(afternoon);
        Assert.All(afternoon, s => Assert.Equal(spa.RoomB.Id, s.RoomId));
    }

    [Fact]
    public async Task Verschieben_in_den_gesperrten_Raum_wird_abgelehnt_und_ins_Freie_geht()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var day = Monday(1);
        var appointment = await spa.BookOkAsync(TestSpa.At(10, 0, day), Ct, room: spa.RoomA.Id);
        await CreateAsync(spa, Input(spa.RoomB, day));

        var toClosed = await spa.Client.PostAsJsonAsync($"/api/appointments/{appointment.Id}/move",
            new MoveAppointmentRequest(TestSpa.At(11, 0, day), appointment.EmployeeId, spa.RoomB.Id, null), TestSpa.Json, Ct);
        Assert.Equal(HttpStatusCode.Conflict, toClosed.StatusCode);
        var toFree = await spa.Client.PostAsJsonAsync($"/api/appointments/{appointment.Id}/move",
            new MoveAppointmentRequest(TestSpa.At(11, 0, day), appointment.EmployeeId, spa.RoomA.Id, null), TestSpa.Json, Ct);
        Assert.Equal(HttpStatusCode.OK, toFree.StatusCode);
    }

    // ---------- Kalender ----------

    [Fact]
    public async Task Der_Kalender_zeigt_das_Band_im_Raum_mit_Art_Grund_und_offenen_Terminen_nur_dieses_Raums()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var day = Monday(1);
        var inA = await spa.BookOkAsync(TestSpa.At(10, 0, day), Ct, room: spa.RoomA.Id);
        await spa.BookOkAsync(TestSpa.At(10, 0, day), Ct, room: spa.RoomB.Id, employee: spa.Ben.Id);
        var closure = await CreateAsync(spa, Input(spa.RoomA, day, day.AddDays(2), RoomClosureKind.Damage, reason: "Wasserschaden"));
        await CreateAsync(spa, Input(spa.RoomB, Monday(2), kind: RoomClosureKind.Renovation));

        var calendar = await spa.GetAsync<CalendarDay>(CalendarPath(spa, day), Ct);

        var band = Assert.Single(calendar.RoomClosures!);
        Assert.Equal((closure.Id, spa.RoomA.Id, "Raum A", "Schaden", "Wasserschaden", 1), (band.Id, band.RoomId, band.RoomName, band.Title, band.Reason, band.OpenAppointments));
        Assert.Equal((closure.From, closure.Until, RoomClosureKind.Damage), (band.From, band.Until, band.Kind));
        // Der Termin bleibt, bis jemand ihn umbucht.
        Assert.Contains(calendar.Appointments, a => a.Id == inA.Id && a.Status == AppointmentStatus.Booked && a.RoomId == spa.RoomA.Id);
        var free = await spa.GetAsync<CalendarDay>(CalendarPath(spa, Monday(5)), Ct);
        Assert.True(free.RoomClosures is null or { Count: 0 });
        var onOtherDay = await spa.GetAsync<CalendarDay>(CalendarPath(spa, Monday(2)), Ct);
        Assert.Equal(spa.RoomB.Id, Assert.Single(onOtherDay.RoomClosures!).RoomId);
    }

    [Fact]
    public async Task Raumsperren_mit_offenen_Terminen_stehen_im_Kalender_jedes_Tages_gleich_wie_fern_dringend_ab_14_Tagen()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        // Der nächste Montag (die Testpersonen arbeiten montags), sieben bis dreizehn Tage voraus, einer gut vier Wochen danach und einer ohne Termin.
        var soon = Today.AddDays((((int)DayOfWeek.Monday - (int)Today.DayOfWeek + 7) % 7) + 7);
        var far = soon.AddDays(28);
        await spa.BookOkAsync(TestSpa.At(10, 0, soon), Ct, room: spa.RoomA.Id);
        await spa.BookOkAsync(TestSpa.At(10, 0, far), Ct, room: spa.RoomA.Id);
        var due = await CreateAsync(spa, Input(spa.RoomA, soon));
        var distant = await CreateAsync(spa, Input(spa.RoomA, far));
        await CreateAsync(spa, Input(spa.RoomB, soon.AddDays(14)));

        var calendar = await spa.GetAsync<CalendarDay>(CalendarPath(spa, Today), Ct);

        Assert.Equal([due.Id, distant.Id], calendar.RoomClosuresDue!.Select(b => b.Id));
        Assert.Equal([true, false], calendar.RoomClosuresDue!.Select(b => b.Urgent));
        Assert.Equal([1, 1], calendar.RoomClosuresDue!.Select(b => b.OpenAppointments));
        Assert.True(calendar.RoomClosures is null or { Count: 0 });
        var onDay = await spa.GetAsync<CalendarDay>(CalendarPath(spa, soon), Ct);
        Assert.Equal(due.Id, Assert.Single(onDay.RoomClosures!).Id);
    }

    // ---------- Umbuchungshilfe ----------

    [Fact]
    public async Task Die_Umbuchungshilfe_schlaegt_einen_anderen_Raum_zur_selben_Zeit_vor_sonst_die_naechste_freie_Zeit_und_uebernimmt_ueber_den_Weg_des_Ausfall_Assistenten()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var day = Monday(1);
        var email = $"gast-{Guid.NewGuid():N}@example.at";
        var guest = await spa.PostAsync<CustomerDto>("/api/customers", new CustomerInput("Maria", "Gast", email, null, null, null), Ct);
        var first = await TestSpa.ReadAsync<AppointmentDto>(await spa.BookAsync(TestSpa.At(10, 0, day), Ct, employee: spa.Anna.Id, room: spa.RoomA.Id, customer: guest.Id), Ct);
        // Um 12:00 sind Anna und Ben im Raum B belegt: im gesperrten Raum A bleibt für diesen Termin kein anderer Raum zur selben Zeit.
        var second = await spa.BookOkAsync(TestSpa.At(12, 0, day), Ct, employee: spa.Anna.Id, room: spa.RoomA.Id);
        await spa.BookOkAsync(TestSpa.At(12, 0, day), Ct, employee: spa.Ben.Id, room: spa.RoomB.Id);
        var closure = await CreateAsync(spa, Input(spa.RoomA, day));

        var proposal = await spa.GetAsync<AbsenceProposal>($"{Path(spa)}/{closure.Id}/rebooking", Ct);

        Assert.Equal(Guid.Empty, proposal.EmployeeId);
        Assert.Equal([first.Id, second.Id], proposal.Items.Select(i => i.Appointment.Id));
        var sameTime = proposal.Items[0];
        Assert.Equal((AbsenceOption.SameTime, spa.Anna.Id, spa.RoomB.Id, first.Start), (sameTime.Option, sameTime.EmployeeId, sameTime.RoomId, sameTime.Start));
        Assert.Equal("Raum B ist um 10:00 frei; Anna bleibt.", sameTime.Explanation);
        var other = proposal.Items[1];
        Assert.Equal(AbsenceOption.OtherTime, other.Option);
        Assert.NotEqual(spa.RoomA.Id, other.RoomId);
        Assert.StartsWith("Um 12:00 ist kein anderer Raum frei. Nächste freie Zeit:", other.Explanation, StringComparison.Ordinal);

        // Übernehmen über den Weg des Ausfall-Assistenten: nur der Raum wechselt, also geht keine Mail hinaus.
        var result = await spa.PostAsync<AbsenceApplyResult>($"/api/locations/{spa.Location.Id}/absence/apply",
            new AbsenceApplyRequest([new AbsenceMove(first.Id, sameTime.Start!.Value, sameTime.EmployeeId!.Value, sameTime.RoomId!.Value)], NotifyCustomers: true), Ct);
        Assert.Equal(spa.RoomB.Id, Assert.Single(result.Moved).RoomId);
        Assert.Equal(0, result.Notified);
        var after = await spa.GetAsync<AbsenceProposal>($"{Path(spa)}/{closure.Id}/rebooking", Ct);
        Assert.Equal([second.Id], after.Items.Select(i => i.Appointment.Id));
        Assert.Equal(1, Assert.Single((await spa.GetAsync<CalendarDay>(CalendarPath(spa, day), Ct)).RoomClosures!).OpenAppointments);
    }

    [Fact]
    public async Task Ohne_anderen_Raum_am_Tag_bleibt_nur_der_Anruf_und_ohne_offene_Termine_ist_der_Vorschlag_leer()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var day = Monday(1);
        await spa.BookOkAsync(TestSpa.At(10, 0, day), Ct, room: spa.RoomA.Id);
        var both = await CreateAsync(spa, Input(spa.RoomA, day));
        await CreateAsync(spa, Input(spa.RoomB, day));
        var untouched = await CreateAsync(spa, Input(spa.RoomA, Monday(7)));

        var item = Assert.Single((await spa.GetAsync<AbsenceProposal>($"{Path(spa)}/{both.Id}/rebooking", Ct)).Items);

        Assert.Equal(AbsenceOption.None, item.Option);
        Assert.Null(item.Start);
        Assert.Contains("Bitte den Gast kontaktieren", item.Explanation, StringComparison.Ordinal);
        Assert.Empty((await spa.GetAsync<AbsenceProposal>($"{Path(spa)}/{untouched.Id}/rebooking", Ct)).Items);
        Assert.Equal(HttpStatusCode.NotFound, (await spa.Client.GetAsync($"{Path(spa)}/{Guid.NewGuid()}/rebooking", Ct)).StatusCode);
    }

    // ---------- Bereiche ----------

    [Fact]
    public async Task Ein_gesperrter_Bereich_nimmt_keinen_Eintritt_an()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var day = Monday(1);
        var sauna = await spa.PostAsync<RoomDto>($"/api/locations/{spa.Location.Id}/rooms", new RoomInput("Sauna", 0, 3, IsArea: true), Ct);
        Task<HttpResponseMessage> VisitAsync(int from, int until) => spa.Client.PostAsJsonAsync("/api/area-visits",
            new AreaVisitRequest(spa.Location.Id, sauna.Id, spa.Customer.Id, TestSpa.At(from, 0, day), TestSpa.At(until, 0, day), 1), TestSpa.Json, Ct);
        await CreateAsync(spa, Input(sauna, day, fromTime: new TimeOnly(12, 0), untilTime: new TimeOnly(15, 0), reason: "Ofen"));

        var rejected = await VisitAsync(11, 13);
        Assert.Equal(HttpStatusCode.Conflict, rejected.StatusCode);
        var problem = (await rejected.Content.ReadFromJsonAsync<BookingRejected>(TestSpa.Json, Ct))!;
        Assert.Equal("Der Bereich Sauna ist in dieser Zeit gesperrt (Wartung).", problem.Message);
        Assert.Equal("ist gesperrt (Wartung: Ofen)", Assert.Single(problem.Conflicts).Reason);
        Assert.Equal(HttpStatusCode.Created, (await VisitAsync(9, 12)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await VisitAsync(15, 17)).StatusCode);
    }
}
