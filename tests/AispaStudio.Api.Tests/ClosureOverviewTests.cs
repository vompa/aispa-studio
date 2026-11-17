using System.Net;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Tests;

/// <summary>Übersicht von Schließzeiten und Raumsperren in einer Liste (ADR 0098): beide Arten, kommend und vergangen, Zahl offener Termine, Rechte.</summary>
public sealed class ClosureOverviewTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static DateOnly Monday(int weeks = 0) => TestSpa.Monday.AddDays(7 * weeks);

    private static string Path(TestSpa spa, string query = "") => $"/api/locations/{spa.Location.Id}/closure-overview{query}";

    [Fact]
    public async Task Uebersicht_mischt_Schliesszeiten_und_Raumsperren_kommende_aufsteigend_vergangene_absteigend()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var later = await ClosureTests.CreateAsync(spa, ClosureTests.Input(Monday(4), Monday(4).AddDays(1), reason: "Betriebsurlaub"));
        var room = await spa.PostAsync<RoomClosureDto>($"/api/locations/{spa.Location.Id}/room-closures",
            new RoomClosureInput(spa.RoomA.Id, Monday(2), null, Monday(2), null, RoomClosureKind.Maintenance, "Wartung Ofen"), Ct);
        var older = await ClosureTests.CreateAsync(spa, ClosureTests.Input(new DateOnly(2026, 1, 10), new DateOnly(2026, 1, 12), ClosureKind.Outage, reason: null, guestText: "Wasserschaden"));
        var oldest = await spa.PostAsync<RoomClosureDto>($"/api/locations/{spa.Location.Id}/room-closures",
            new RoomClosureInput(spa.RoomB.Id, new DateOnly(2026, 1, 2), null, new DateOnly(2026, 1, 3), null, RoomClosureKind.Other, null), Ct);

        var upcoming = await spa.GetAsync<ClosureOverview>(Path(spa), Ct);
        Assert.Equal([room.Id, later.Id], upcoming.Items.Select(i => i.Id));
        Assert.Equal((2, 2), (upcoming.UpcomingCount, upcoming.PastCount));
        var roomItem = upcoming.Items[0];
        Assert.Equal((ClosureEntryType.Room, spa.RoomA.Id, "Raum A", "Maintenance", "Wartung Ofen", ClosurePhase.Planned),
            (roomItem.Type, roomItem.RoomId, roomItem.RoomName, roomItem.Kind, roomItem.Reason, roomItem.Phase));
        var closureItem = upcoming.Items[1];
        Assert.Equal((ClosureEntryType.Location, "Vacation", (Guid?)null, "Betriebsurlaub", spa.Location.Id), (closureItem.Type, closureItem.Kind, closureItem.RoomId, closureItem.Reason, closureItem.LocationId));

        var past = await spa.GetAsync<ClosureOverview>(Path(spa, "?scope=Past"), Ct);
        Assert.Equal([older.Id, oldest.Id], past.Items.Select(i => i.Id));
        Assert.Equal(("Wasserschaden", "Outage"), (past.Items[0].Reason, past.Items[0].Kind));
        Assert.All(past.Items, i => Assert.Equal(ClosurePhase.Ended, i.Phase));

        var all = await spa.GetAsync<ClosureOverview>(Path(spa, "?scope=All"), Ct);
        Assert.Equal([room.Id, later.Id, older.Id, oldest.Id], all.Items.Select(i => i.Id));
    }

    [Fact]
    public async Task Uebersicht_zaehlt_offene_Termine_je_Eintrag()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var day = Monday(3);
        await spa.BookOkAsync(TestSpa.At(10, 0, day), Ct, room: spa.RoomA.Id);
        var whole = await ClosureTests.CreateAsync(spa, ClosureTests.Input(day));
        var room = await spa.PostAsync<RoomClosureDto>($"/api/locations/{spa.Location.Id}/room-closures",
            new RoomClosureInput(spa.RoomB.Id, day, null, day, null, RoomClosureKind.Damage, null), Ct);

        var overview = await spa.GetAsync<ClosureOverview>(Path(spa), Ct);

        Assert.Equal(1, overview.Items.Single(i => i.Id == whole.Id).OpenAppointments);
        Assert.Equal(0, overview.Items.Single(i => i.Id == room.Id).OpenAppointments);
    }

    [Fact]
    public async Task Uebersicht_ist_fuer_jeden_mit_Kalender_lesbar_nicht_ohne_Recht_und_nicht_fuer_fremde_Standorte()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var other = await spa.CreateLocationAsync("Zweiter Standort", [DayOfWeek.Monday], Ct);
        using var reception = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Reception, [spa.Location.Id]);
        using var backoffice = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Backoffice, [spa.Location.Id]);
        using var elsewhere = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Manager, [other.Id]);
        using var anonymous = fixture.CreateClient();

        Assert.Equal(HttpStatusCode.OK, (await reception.GetAsync(Path(spa), Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await backoffice.GetAsync(Path(spa), Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await elsewhere.GetAsync(Path(spa), Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(Path(spa), Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await spa.Client.GetAsync($"/api/locations/{Guid.NewGuid()}/closure-overview", Ct)).StatusCode);
    }
}
