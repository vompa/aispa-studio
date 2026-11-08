using System.Net;
using System.Net.Http.Json;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Tests;

/// <summary>Raumpflege als Aufgabe, Tagesstart-Kontrolle mit Checkliste, Abnahme per Recht oder PIN, Hinweise beim Einchecken und Hygieneprotokoll.</summary>
public sealed class RoomCareTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>Uhrzeit am heutigen Tag in Wien: Die Tagesregel am Status-Endpunkt rechnet mit der echten Uhr.</summary>
    private static DateTimeOffset TodayAt(int hour) => TestSpa.At(hour, 0, DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna")).DateTime));

    /// <summary>Ein vergangener Montag: die Pflegezeit ist vorbei, der Tag liegt nicht in der Zukunft.</summary>
    private static readonly DateOnly Past = new(2026, 10, 5);

    private static string Iso(DateOnly date) => $"{date:yyyy-MM-dd}";

    [Fact]
    public async Task Raumpflege_nach_dem_Termin_ist_eine_Aufgabe_mit_Zustand_und_Haken()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var past = await spa.BookInThePastAsync(TestSpa.At(10, 0, Past), Ct, spa.Anna.Id, spa.RoomA.Id);
        var withoutCare = await spa.BookInThePastAsync(TestSpa.At(14, 0, Past), Ct, spa.Anna.Id, spa.RoomB.Id);
        var future = await spa.BookOkAsync(TestSpa.At(10), Ct, spa.Anna.Id, spa.RoomA.Id);

        var overdue = (await Day(spa, Past)).RoomCare!;
        var open = (await Day(spa, TestSpa.Monday)).RoomCare!;
        var direct = await spa.GetAsync<RoomCareDay>($"/api/locations/{spa.Location.Id}/room-care?date={Iso(Past)}", Ct);
        var check = await spa.Client.PutAsJsonAsync($"/api/appointments/{past.Id}/room-care", new RoomCareCheckInput(true), TestSpa.Json, Ct);
        var done = (await Day(spa, Past)).RoomCare!;
        await spa.Client.PutAsJsonAsync($"/api/appointments/{past.Id}/room-care", new RoomCareCheckInput(false), TestSpa.Json, Ct);
        var again = (await Day(spa, Past)).RoomCare!;
        var noCare = await spa.Client.PutAsJsonAsync($"/api/appointments/{withoutCare.Id}/room-care", new RoomCareCheckInput(true), TestSpa.Json, Ct);
        var unknown = await spa.Client.PutAsJsonAsync($"/api/appointments/{Guid.NewGuid()}/room-care", new RoomCareCheckInput(true), TestSpa.Json, Ct);

        var task = Assert.Single(overdue.Tasks);
        Assert.Equal(task.Text, Assert.Single(direct.Tasks).Text);
        Assert.Equal((past.Id, spa.RoomA.Id, RoomCareState.Overdue, spa.Anna.Id), (task.AppointmentId, task.RoomId, task.State, task.EmployeeId));
        Assert.Equal("Raum A pflegen bis 11:15", task.Text);
        Assert.Equal(past.BlockedUntil, task.From);
        Assert.Equal(past.BlockedUntil.AddMinutes(15), task.Until);
        var state = Assert.Single(overdue.Rooms);
        Assert.Equal((1, 0), (state.CareOverdue, state.CareOpen));
        var openTask = Assert.Single(open.Tasks);
        Assert.Equal((future.Id, RoomCareState.Open), (openTask.AppointmentId, openTask.State));
        Assert.Equal(HttpStatusCode.NoContent, check.StatusCode);
        var doneTask = Assert.Single(done.Tasks);
        Assert.Equal(RoomCareState.Done, doneTask.State);
        Assert.NotNull(doneTask.DoneAt);
        Assert.False(string.IsNullOrEmpty(doneTask.DoneBy));
        Assert.Equal(RoomCareState.Overdue, Assert.Single(again.Tasks).State);
        Assert.Equal(HttpStatusCode.BadRequest, noCare.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
    }

    [Fact]
    public async Task Checkliste_wird_gepflegt_abgehakt_und_vom_Leiter_abgenommen()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var url = $"/api/rooms/{spa.RoomA.Id}/start-checklist";
        var saved = await spa.PutAsync<List<RoomStartItemDto>>(url, new RoomStartItemInput[] { new("Sauna aufheizen"), new("Temperatur prüfen") }, Ct);
        var again = await spa.PutAsync<List<RoomStartItemDto>>(url, new RoomStartItemInput[] { new("Sauna aufheizen"), new("Temperatur prüfen") }, Ct);
        using var reception = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Reception, [spa.Location.Id]);
        Assert.Equal(HttpStatusCode.Forbidden, (await reception.PutAsJsonAsync(url, new RoomStartItemInput[] { new("X") }, TestSpa.Json, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await spa.Client.PutAsJsonAsync(url, new RoomStartItemInput[] { new(" ") }, TestSpa.Json, Ct)).StatusCode);

        var tick = (Guid item) => reception.PutAsJsonAsync($"/api/rooms/{spa.RoomA.Id}/start-checks/{item}", new RoomStartCheckInput(Past, true), TestSpa.Json, Ct);
        var approve = (HttpClient client, RoomApprovalKind kind, SupervisorApproval? approval) =>
            client.PostAsJsonAsync($"/api/rooms/{spa.RoomA.Id}/approvals", new RoomApprovalInput(Past, kind, approval), TestSpa.Json, Ct);
        var owner = await spa.GetAsync<CurrentUser>("/api/auth/me", Ct);
        var colleague = (await (await TestUsers.ClientAsync(fixture, Ct, StaffRole.Reception, [spa.Location.Id])).GetFromJsonAsync<CurrentUser>("/api/auth/me", TestSpa.Json, Ct))!;
        (await spa.Client.PutAsJsonAsync("/api/auth/pin", new QuickSwitchPin("258014"), TestSpa.Json, Ct)).EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.NoContent, (await tick(saved[0].Id)).StatusCode);
        var partial = Assert.Single((await spa.GetAsync<RoomStartDayDto>($"/api/locations/{spa.Location.Id}/room-start?date={Iso(Past)}", Ct)).Rooms);
        var tooEarly = await approve(spa.Client, RoomApprovalKind.DayStart, null);
        Assert.Equal(HttpStatusCode.NoContent, (await tick(saved[1].Id)).StatusCode);
        var checkedState = Assert.Single((await spa.GetAsync<RoomStartDayDto>($"/api/locations/{spa.Location.Id}/room-start?date={Iso(Past)}", Ct)).Rooms);
        var withoutRight = await approve(reception, RoomApprovalKind.DayStart, null);
        var wrongPin = await approve(reception, RoomApprovalKind.DayStart, new SupervisorApproval(owner.Id, "000000"));
        var byColleague = await approve(reception, RoomApprovalKind.DayStart, new SupervisorApproval(colleague.Id, "4711"));
        var approvers = (await reception.GetFromJsonAsync<List<QuickSwitchPerson>>($"/api/locations/{spa.Location.Id}/room-approvers", TestSpa.Json, Ct))!;
        var byPin = await approve(reception, RoomApprovalKind.DayStart, new SupervisorApproval(owner.Id, "258014"));
        var approval = await TestSpa.ReadAsync<RoomApprovalDto>(byPin, Ct);
        var twice = await approve(spa.Client, RoomApprovalKind.DayStart, null);
        var changeAfter = await reception.PutAsJsonAsync($"/api/rooms/{spa.RoomA.Id}/start-checks/{saved[0].Id}", new RoomStartCheckInput(Past, false), TestSpa.Json, Ct);
        var future = await reception.PutAsJsonAsync($"/api/rooms/{spa.RoomA.Id}/start-checks/{saved[0].Id}", new RoomStartCheckInput(new DateOnly(2099, 1, 1), true), TestSpa.Json, Ct);
        var approved = Assert.Single((await spa.GetAsync<RoomStartDayDto>($"/api/locations/{spa.Location.Id}/room-start?date={Iso(Past)}", Ct)).Rooms);
        var noCare = await approve(spa.Client, RoomApprovalKind.RoomCare, null);

        Assert.Equal(saved.Select(i => i.Id), again.Select(i => i.Id));
        Assert.Equal(RoomStartState.Partial, partial.State);
        Assert.Equal(HttpStatusCode.BadRequest, tooEarly.StatusCode);
        Assert.Contains("Noch offen", await tooEarly.Content.ReadAsStringAsync(Ct));
        Assert.Equal(RoomStartState.Checked, checkedState.State);
        Assert.All(checkedState.Items, i => Assert.True(i.Done && i.DoneAt is not null && !string.IsNullOrEmpty(i.DoneBy)));
        Assert.Equal(HttpStatusCode.Forbidden, withoutRight.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, wrongPin.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, byColleague.StatusCode);
        Assert.Contains(approvers, p => p.UserId == owner.Id);
        Assert.DoesNotContain(approvers, p => p.UserId == colleague.Id);
        Assert.Equal((RoomApprovalKind.DayStart, owner.DisplayName), (approval.Kind, approval.ApprovedBy));
        Assert.Equal(HttpStatusCode.BadRequest, twice.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, changeAfter.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, future.StatusCode);
        Assert.Equal((RoomStartState.Approved, owner.DisplayName), (approved.State, approved.ApprovedBy));
        Assert.Equal(HttpStatusCode.BadRequest, noCare.StatusCode);
    }

    [Fact]
    public async Task Raumpflege_wird_vom_Leiter_nach_dem_Abhaken_abgenommen_und_steht_im_Hygieneprotokoll()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await spa.PutAsync<List<RoomStartItemDto>>($"/api/rooms/{spa.RoomA.Id}/start-checklist", new RoomStartItemInput[] { new("Liege beziehen") }, Ct);
        var item = (await spa.GetAsync<List<RoomStartItemDto>>($"/api/rooms/{spa.RoomA.Id}/start-checklist", Ct))[0];
        var appointment = await spa.BookInThePastAsync(TestSpa.At(10, 0, Past), Ct, spa.Anna.Id, spa.RoomA.Id);
        var approve = () => spa.Client.PostAsJsonAsync($"/api/rooms/{spa.RoomA.Id}/approvals", new RoomApprovalInput(Past, RoomApprovalKind.RoomCare), TestSpa.Json, Ct);

        var early = await approve();
        await spa.Client.PutAsJsonAsync($"/api/appointments/{appointment.Id}/room-care", new RoomCareCheckInput(true), TestSpa.Json, Ct);
        var approved = await approve();
        await spa.Client.PutAsJsonAsync($"/api/rooms/{spa.RoomA.Id}/start-checks/{item.Id}", new RoomStartCheckInput(Past, true), TestSpa.Json, Ct);
        await spa.PutAsync<List<RoomStartItemDto>>($"/api/rooms/{spa.RoomA.Id}/start-checklist", new RoomStartItemInput[] { new("Dusche prüfen") }, Ct);
        var log = await spa.GetAsync<HygieneLogDto>($"/api/locations/{spa.Location.Id}/hygiene-log?from={Iso(Past)}&until={Iso(Past)}", Ct);
        var csv = await spa.Client.GetAsync($"/api/locations/{spa.Location.Id}/hygiene-log.csv?from={Iso(Past)}&until={Iso(Past)}", Ct);
        var bytes = await csv.Content.ReadAsByteArrayAsync(Ct);
        using var reception = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Reception, [spa.Location.Id]);
        var forbidden = await reception.GetAsync($"/api/locations/{spa.Location.Id}/hygiene-log?from={Iso(Past)}&until={Iso(Past)}", Ct);
        var tooLong = await spa.Client.GetAsync($"/api/locations/{spa.Location.Id}/hygiene-log?from=2026-01-01&until=2026-10-01", Ct);

        Assert.Equal(HttpStatusCode.BadRequest, early.StatusCode);
        Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
        var careRow = Assert.Single(log.Rows, r => r.Kind == RoomApprovalKind.RoomCare);
        Assert.Equal(("Raum A", true, "Raum A pflegen bis 11:15"), (careRow.RoomName, careRow.Done, careRow.Item));
        Assert.NotNull(careRow.ApprovedAt);
        var removed = Assert.Single(log.Rows, r => r.Item == "Liege beziehen");
        Assert.True(removed.Done);
        var added = Assert.Single(log.Rows, r => r.Item == "Dusche prüfen");
        Assert.False(added.Done);
        Assert.Equal(HttpStatusCode.OK, csv.StatusCode);
        Assert.Equal([0xEF, 0xBB, 0xBF], bytes.Take(3).Select(b => (int)b));
        var text = global::System.Text.Encoding.UTF8.GetString(bytes);
        Assert.Contains("Datum;Raum;Art;Punkt", text);
        Assert.Contains("05.10.2026;Raum A;Tagesstart;Liege beziehen;;ja", text);
        Assert.Contains("Raumpflege;Raum A pflegen bis 11:15;11:15;ja", text);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);
    }

    [Fact]
    public async Task Einchecken_warnt_vor_offenem_Tagesstart_und_nicht_abgehakter_Raumpflege_blockiert_aber_nicht()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await spa.PutAsync<List<RoomStartItemDto>>($"/api/rooms/{spa.RoomA.Id}/start-checklist", new RoomStartItemInput[] { new("Liege beziehen"), new("Temperatur prüfen") }, Ct);
        // Der Test prüft die Hinweise der Endpunkt-Antwort; die Tagesregel verlangt dafür Termine am heutigen Tag.
        var first = await spa.BookOkAsync(TodayAt(10), Ct, spa.Anna.Id, spa.RoomA.Id);
        var second = await spa.BookOkAsync(TodayAt(13), Ct, spa.Anna.Id, spa.RoomA.Id);
        var other = await spa.BookOkAsync(TodayAt(13), Ct, spa.Ben.Id, spa.RoomB.Id);

        var firstIn = await CheckIn(spa, first.Id);
        var secondIn = await CheckIn(spa, second.Id);
        await spa.Client.PutAsJsonAsync($"/api/appointments/{first.Id}/room-care", new RoomCareCheckInput(true), TestSpa.Json, Ct);
        var again = await CheckIn(spa, second.Id);
        var otherIn = await CheckIn(spa, other.Id);

        Assert.Equal(AppointmentStatus.CheckedIn, firstIn.Status);
        Assert.Contains("Tagesstart-Kontrolle für Raum A ist noch nicht abgeschlossen (0 von 2", Assert.Single(firstIn.Warnings!));
        Assert.Equal(AppointmentStatus.CheckedIn, secondIn.Status);
        Assert.Equal(2, secondIn.Warnings!.Count);
        Assert.Contains(secondIn.Warnings, w => w.StartsWith("Raumpflege in Raum A nach dem Termin um 10:50", StringComparison.Ordinal));
        Assert.Equal(AppointmentStatus.CheckedIn, again.Status);
        Assert.Contains("Tagesstart-Kontrolle", Assert.Single(again.Warnings!));
        Assert.Null(otherIn.Warnings);
    }

    private static async Task<AppointmentDto> CheckIn(TestSpa spa, Guid id) =>
        await spa.PostAsync<AppointmentDto>($"/api/appointments/{id}/status", new AppointmentStatusChange(AppointmentStatus.CheckedIn), Ct);

    private static async Task<CalendarDay> Day(TestSpa spa, DateOnly date) =>
        await spa.GetAsync<CalendarDay>($"/api/locations/{spa.Location.Id}/calendar?date={Iso(date)}", Ct);
}
