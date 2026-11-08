using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using AispaStudio.Contracts;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace AispaStudio.Api.Tests;

/// <summary>Das Dashboard im Pausenraum (Slice 105): Tafel mit Tagesleiste, freien Räumen und nächsten Terminen, Initialen statt Gastnamen, live.</summary>
public sealed class DisplayBoardTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private static async Task<string> PairedCookieAsync(TestSpa spa, CancellationToken ct)
    {
        using var device = spa.Fixture.CreateClient();
        var start = await TestSpa.ReadAsync<DisplayPairingStart>(await device.PostAsync("/api/display/pairing", null, ct), ct);
        _ = await spa.PostAsync<DisplayDeviceDto>("/api/displays", new DisplayPairInput(start.Code, "Pausenraum", spa.Location.Id), ct);
        var redeemed = await device.PostAsJsonAsync("/api/display/pairing/redeem", new DisplayPairingRedeem(start.Secret), TestSpa.Json, ct);
        Assert.Equal(HttpStatusCode.OK, redeemed.StatusCode);
        return string.Join("; ", redeemed.Headers.GetValues("Set-Cookie").Select(c => c.Split(';')[0]));
    }

    private HubConnection Live(string cookie) =>
        new HubConnectionBuilder()
            .WithUrl(new Uri(fixture.Factory.Server.BaseAddress, LiveHubContract.Path), options =>
            {
                options.HttpMessageHandlerFactory = _ => fixture.Factory.Server.CreateHandler();
                options.Transports = HttpTransportType.LongPolling;
                options.Headers["Cookie"] = cookie;
            })
            .Build();

    [Fact]
    public async Task Gegeben_ein_Termin_mit_Gast_Anna_Huber_Wenn_das_Dashboard_ihn_zeigt_Dann_steht_dort_A_H_und_nichts_Gesundheitliches()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        // Die Therapeutin der Testdaten heißt ebenfalls Anna; sie darf auf der Tafel stehen, der Gast nur als Initialen.
        var guest = await spa.PostAsync<CustomerDto>("/api/customers",
            new CustomerInput("Anna", "Huber", "anna.huber@example.at", "+43 664 111", null, "Bluthochdruck, Diabetes"), Ct);
        _ = await spa.BookOkAsync(TestSpa.At(10), Ct, spa.Ben.Id, spa.RoomA.Id, guest.Id);
        await using var host = Clock(new DateTimeOffset(2027, 3, 1, 8, 0, 0, TimeSpan.Zero));
        using var display = host.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        display.DefaultRequestHeaders.Add("Cookie", await PairedCookieAsync(spa, Ct));

        var response = await display.GetAsync("/api/display/board", Ct);
        var json = await response.Content.ReadAsStringAsync(Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var board = await response.Content.ReadFromJsonAsync<DisplayBoard>(TestSpa.Json, Ct);
        Assert.Equal("A. H.", Assert.Single(board!.Next).Initials);
        foreach (var forbidden in new[] { "Anna Huber", "Huber", "anna.huber", "+43 664 111", "Bluthochdruck", "Diabetes", "Massage", "79" })
        {
            Assert.DoesNotContain(forbidden, json, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task Tagesleiste_freie_Raeume_und_naechste_Termine_stimmen_zur_Uhrzeit()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var current = await spa.BookOkAsync(TestSpa.At(10), Ct, spa.Anna.Id, spa.RoomA.Id);
        var later = await spa.BookOkAsync(TestSpa.At(14), Ct, spa.Ben.Id, spa.RoomB.Id);
        await using var host = Clock(TestSpa.At(10, 30));
        using var display = host.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        display.DefaultRequestHeaders.Add("Cookie", await PairedCookieAsync(spa, Ct));

        var board = await display.GetFromJsonAsync<DisplayBoard>("/api/display/board", TestSpa.Json, Ct);

        Assert.Equal("Wien", board!.LocationName);
        var anna = board.People.Single(p => p.Name == "Anna");
        var ben = board.People.Single(p => p.Name == "Ben");
        Assert.Equal(DisplayPersonState.InTreatment, anna.State);
        Assert.Equal(DisplayPersonState.Free, ben.State);
        Assert.Equal([new DisplaySpan(current.Start, current.End)], anna.Appointments);
        Assert.Equal([new DisplaySpan(TestSpa.At(9), TestSpa.At(17))], anna.Shifts);
        var roomA = board.Rooms.Single(r => r.Name == "Raum A");
        var roomB = board.Rooms.Single(r => r.Name == "Raum B");
        Assert.False(roomA.Free);
        Assert.Equal(current.BlockedUntil, roomA.Until);
        Assert.True(roomB.Free);
        Assert.Equal(later.BlockedFrom, roomB.Until);
        var next = Assert.Single(board.Next);
        Assert.Equal((later.Start, "Ben", "Raum B"), (next.Start, next.Person, next.Room));
        Assert.Matches(@"^[A-Z]\. [A-Z]\.$", next.Initials);
    }

    [Fact]
    public async Task Gegeben_die_Anzeige_laeuft_Wenn_ein_Termin_an_der_Rezeption_verschoben_wird_Dann_kommt_das_Signal_innerhalb_von_5_Sekunden()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var appointment = await spa.BookOkAsync(TestSpa.At(10), Ct, spa.Anna.Id, spa.RoomA.Id);
        var displayCookie = await PairedCookieAsync(spa, Ct);
        await using var connection = Live(displayCookie);
        var arrived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.On(LiveHubContract.DisplayChanged, () => arrived.TrySetResult());
        await connection.StartAsync(Ct);
        await connection.InvokeAsync(LiveHubContract.WatchDisplay, Ct);

        var clock = Stopwatch.StartNew();
        (await spa.Client.PostAsJsonAsync($"/api/appointments/{appointment.Id}/move",
            new MoveAppointmentRequest(TestSpa.At(12), null, null, null), TestSpa.Json, Ct)).EnsureSuccessStatusCode();
        await arrived.Task.WaitAsync(TimeSpan.FromSeconds(5), Ct);

        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(5));
        // Die Tafel liest danach die neue Zeit (zweiter Host mit fester Uhr am Montag um 8 Uhr: das Team-Cookie überlebt die Uhr nicht, das der Anzeige schon).
        await using var host = Clock(TestSpa.At(8));
        using var display = host.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        display.DefaultRequestHeaders.Add("Cookie", displayCookie);
        var board = await display.GetFromJsonAsync<DisplayBoard>("/api/display/board", TestSpa.Json, Ct);
        Assert.Equal(TestSpa.At(12), Assert.Single(board!.Next).Start);
    }

    [Fact]
    public async Task Raumsperren_gelten_nur_am_heutigen_Tag_und_kuenftige_Tage_verfaelschen_die_Zeiten_nicht()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var nextMonday = TestSpa.Monday.AddDays(7);
        _ = await spa.PostAsync<RoomClosureDto>($"/api/locations/{spa.Location.Id}/room-closures",
            new RoomClosureInput(spa.RoomB.Id, TestSpa.Monday, new(11, 0), TestSpa.Monday, new(12, 0), RoomClosureKind.Maintenance, null), Ct);
        _ = await spa.PostAsync<RoomClosureDto>($"/api/locations/{spa.Location.Id}/room-closures",
            new RoomClosureInput(spa.RoomA.Id, nextMonday, new(9, 0), nextMonday.AddDays(4), null, RoomClosureKind.Renovation, null), Ct);
        await using var host = Clock(TestSpa.At(10, 30));
        using var display = host.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        display.DefaultRequestHeaders.Add("Cookie", await PairedCookieAsync(spa, Ct));

        var board = await display.GetFromJsonAsync<DisplayBoard>("/api/display/board", TestSpa.Json, Ct);

        var roomA = board!.Rooms.Single(r => r.Name == "Raum A");
        var roomB = board.Rooms.Single(r => r.Name == "Raum B");
        Assert.True(roomA.Free);
        Assert.Null(roomA.Until);
        Assert.True(roomB.Free);
        Assert.Equal(TestSpa.At(11), roomB.Until);
    }

    [Fact]
    public async Task Eine_laufende_Raumsperre_ueber_mehrere_Tage_zeigt_belegt_bis_Tagesende()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        _ = await spa.PostAsync<RoomClosureDto>($"/api/locations/{spa.Location.Id}/room-closures",
            new RoomClosureInput(spa.RoomA.Id, TestSpa.Monday, new(9, 0), TestSpa.Monday.AddDays(4), null, RoomClosureKind.Renovation, null), Ct);
        await using var host = Clock(TestSpa.At(10, 30));
        using var display = host.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        display.DefaultRequestHeaders.Add("Cookie", await PairedCookieAsync(spa, Ct));

        var board = await display.GetFromJsonAsync<DisplayBoard>("/api/display/board", TestSpa.Json, Ct);

        var roomA = board!.Rooms.Single(r => r.Name == "Raum A");
        Assert.False(roomA.Free);
        Assert.Null(roomA.Until);
    }

    [Fact]
    public async Task Die_Anzeige_darf_weder_Standorte_noch_den_Posteingang_abonnieren()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await using var connection = Live(await PairedCookieAsync(spa, Ct));
        await connection.StartAsync(Ct);

        await Assert.ThrowsAsync<HubException>(() => connection.InvokeAsync(LiveHubContract.WatchLocation, spa.Location.Id, Ct));
        await Assert.ThrowsAsync<HubException>(() => connection.InvokeAsync(LiveHubContract.WatchInbox, Ct));
    }

    [Fact]
    public async Task Ohne_Kanal_Display_gibt_es_weder_Tafel_noch_Abo_der_Anzeige()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);

        var board = await spa.Client.GetAsync("/api/display/board", Ct);

        Assert.True(board.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden, board.StatusCode.ToString());
        await using var team = Live(spa.Cookie);
        await team.StartAsync(Ct);
        await Assert.ThrowsAsync<HubException>(() => team.InvokeAsync(LiveHubContract.WatchDisplay, Ct));
    }

    [Fact]
    public async Task Im_Einzelplatz_ist_das_Modul_Dashboard_aus_und_die_Endpunkte_antworten_404()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await using var host = fixture.Factory.WithWebHostBuilder(b => b.UseSetting("Hosting:Standalone", "true"));
        using var client = host.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        client.DefaultRequestHeaders.Add("Cookie", spa.Cookie);
        client.DefaultRequestHeaders.Add("X-Aispa-Spa", spa.TenantId.ToString());

        var modules = await client.GetFromJsonAsync<SystemModules>("/api/system/modules", TestSpa.Json, Ct);
        var pairing = await client.PostAsync("/api/display/pairing", null, Ct);
        var list = await client.GetAsync($"/api/locations/{spa.Location.Id}/displays", Ct);

        Assert.False(modules!.Dashboard);
        Assert.Equal(HttpStatusCode.NotFound, pairing.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, list.StatusCode);
    }

    [Theory]
    [InlineData("Anna", "Huber", "A. H.")]
    [InlineData("anna maria", "huber-müller", "A. H.")]
    [InlineData("Anna", "", "A.")]
    [InlineData("", "Huber", "H.")]
    [InlineData("Émile", "Öztürk", "É. Ö.")]
    public void Initialen_nehmen_den_ersten_Buchstaben_von_Vor_und_Nachname(string first, string last, string expected) =>
        Assert.Equal(expected, Access.DisplayBoards.GuestInitials(first, last, placeholder: null));

    [Fact]
    public void Ein_Platzhaltergast_ohne_Namen_heisst_Gast() =>
        Assert.Equal("Gast", Access.DisplayBoards.GuestInitials("Eva", "Muster", placeholder: 2));

    private WebApplicationFactory<Program> Clock(DateTimeOffset now) =>
        fixture.Factory.WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddSingleton<TimeProvider>(new FixedTime(now))));
}
