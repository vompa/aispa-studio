using System.Collections.Concurrent;
using System.Net.Http.Json;
using AispaStudio.Contracts;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;

namespace AispaStudio.Api.Tests;

/// <summary>
/// Meldungen live (Schritt 16): Lager, Raumpflege, Tagesstart und Handbuch veröffentlichen nach dem Speichern eine <c>OperationsChangedMessage</c>, die per SignalR
/// an alle Kalender des Standorts geht (<c>OperationsChanged</c> mit Standort und Art); die Oberfläche lädt danach nur diese Quelle nach.
/// </summary>
public sealed class OperationsLiveTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>Ein vergangener Montag: die Pflegezeit ist vorbei, der Tag liegt nicht in der Zukunft.</summary>
    private static readonly DateOnly Past = new(2026, 10, 5);

    private sealed class Listener(HubConnection connection) : IAsyncDisposable
    {
        private readonly ConcurrentQueue<(Guid Location, OperationsChangeKind Kind)> _events = [];

        public static async Task<Listener> StartAsync(HubConnection connection, Guid? location)
        {
            var listener = new Listener(connection);
            connection.On<Guid, OperationsChangeKind>(LiveHubContract.OperationsChanged, (id, kind) => listener._events.Enqueue((id, kind)));
            await connection.StartAsync(Ct);
            if (location is { } watched)
            {
                await connection.InvokeAsync(LiveHubContract.WatchLocation, watched, Ct);
            }

            return listener;
        }

        public IReadOnlyList<(Guid Location, OperationsChangeKind Kind)> Events => [.. _events];

        /// <summary>Wartet, bis in einer halben Sekunde nichts mehr kam, und vergisst alles Bisherige (Buchungen im Aufbau lösen selbst Meldungen aus).</summary>
        public async Task SettleAsync()
        {
            int before;
            do
            {
                before = _events.Count;
                await Task.Delay(600, Ct);
            }
            while (_events.Count != before);
            _events.Clear();
        }

        public async Task<(Guid Location, OperationsChangeKind Kind)> NextAsync(OperationsChangeKind kind)
        {
            for (var i = 0; i < 300; i++)
            {
                var found = _events.FirstOrDefault(e => e.Kind == kind);
                if (found.Location != Guid.Empty)
                {
                    return found;
                }

                await Task.Delay(100, Ct);
            }

            throw new TimeoutException($"Keine Meldung {kind}; gekommen: {string.Join(", ", _events.Select(e => e.Kind))}");
        }

        public ValueTask DisposeAsync() => connection.DisposeAsync();
    }

    private HubConnection Live(string cookie) =>
        new HubConnectionBuilder()
            .WithUrl(new Uri(fixture.Factory.Server.BaseAddress, LiveHubContract.Path), options =>
            {
                options.HttpMessageHandlerFactory = _ => fixture.Factory.Server.CreateHandler();
                options.Transports = HttpTransportType.LongPolling;
                options.Headers["Cookie"] = cookie;
            })
            .AddJsonProtocol(o => o.PayloadSerializerOptions = TestSpa.Json)
            .Build();

    private async Task<(TestSpa Spa, Listener Listener)> WatchAsync()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        return (spa, await Listener.StartAsync(Live(spa.Cookie), spa.Location.Id));
    }

    [Fact]
    public async Task Raumpflege_abhaken_meldet_RoomCare_an_alle_Kalender_des_Standorts()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var past = await spa.BookInThePastAsync(TestSpa.At(10, 0, Past), Ct, spa.Anna.Id, spa.RoomA.Id);
        await using var listener = await Listener.StartAsync(Live(spa.Cookie), spa.Location.Id);
        await listener.SettleAsync();

        (await spa.Client.PutAsJsonAsync($"/api/appointments/{past.Id}/room-care", new RoomCareCheckInput(true), TestSpa.Json, Ct)).EnsureSuccessStatusCode();

        var (location, kind) = await listener.NextAsync(OperationsChangeKind.RoomCare);
        Assert.Equal((spa.Location.Id, OperationsChangeKind.RoomCare), (location, kind));
    }

    [Fact]
    public async Task Eine_Terminaenderung_meldet_RoomCare_weil_Raumpflege_Aufgaben_entstehen_oder_wegfallen()
    {
        var (spa, listener) = await WatchAsync();
        await using var _ = listener;
        var appointment = await spa.BookOkAsync(TestSpa.At(10), Ct, spa.Anna.Id, spa.RoomA.Id);
        await listener.SettleAsync();

        await spa.SetStatusAsync(appointment.Id, AppointmentStatus.Completed, Ct);

        var (location, _) = await listener.NextAsync(OperationsChangeKind.RoomCare);
        Assert.Equal(spa.Location.Id, location);
    }

    [Fact]
    public async Task Tagesstart_Haken_und_Abnahme_melden_RoomStart()
    {
        var (spa, listener) = await WatchAsync();
        await using var _ = listener;
        var url = $"/api/rooms/{spa.RoomA.Id}/start-checklist";
        var items = await spa.PutAsync<List<RoomStartItemDto>>(url, new RoomStartItemInput[] { new("Sauna aufheizen") }, Ct);
        await listener.SettleAsync();

        (await spa.Client.PutAsJsonAsync($"/api/rooms/{spa.RoomA.Id}/start-checks/{items[0].Id}", new RoomStartCheckInput(Past, true), TestSpa.Json, Ct)).EnsureSuccessStatusCode();
        await listener.NextAsync(OperationsChangeKind.RoomStart);
        await listener.SettleAsync();

        (await spa.Client.PostAsJsonAsync($"/api/rooms/{spa.RoomA.Id}/approvals", new RoomApprovalInput(Past, RoomApprovalKind.DayStart, null), TestSpa.Json, Ct)).EnsureSuccessStatusCode();
        await listener.NextAsync(OperationsChangeKind.RoomStart);
    }

    [Fact]
    public async Task Lagerbewegung_meldet_Stock()
    {
        var (spa, listener) = await WatchAsync();
        await using var _ = listener;
        var oil = await spa.PostAsync<StockItemDto>("/api/stock/items", new StockItemInput("Massageöl", "ml", 0.04m, 500m, null), Ct);
        await listener.SettleAsync();

        await spa.PostAsync<List<StockMovementDto>>($"/api/stock/locations/{spa.Location.Id}/movements", new StockMovementInput(oil.Id, StockMovementKind.GoodsReceipt, 1000m, 0.05m), Ct);

        var (location, _) = await listener.NextAsync(OperationsChangeKind.Stock);
        Assert.Equal(spa.Location.Id, location);
    }

    [Fact]
    public async Task Nachfuellen_im_Raumcheck_meldet_RoomCare_und_Stock()
    {
        var (spa, listener) = await WatchAsync();
        await using var _ = listener;
        var url = $"/api/stock/locations/{spa.Location.Id}";
        var places = await spa.GetAsync<List<StoragePlaceDto>>($"{url}/storage-places", Ct);
        var oil = await spa.PostAsync<StockItemDto>("/api/stock/items", new StockItemInput("Massageöl neutral", "ml", 0.05m, 0, null), Ct);
        var cabinet = await spa.PostAsync<StoragePlaceDto>($"{url}/storage-places", new StoragePlaceInput("Schrank Raum A", null, StoragePlaceKind.Room, spa.RoomA.Id, true,
            [new StoragePlaceMinimumDto(oil.Id, 500m)]), Ct);
        var main = places.Single(p => p.Kind == StoragePlaceKind.Main);
        await spa.PostAsync<List<StockMovementDto>>($"{url}/movements", new StockMovementInput(oil.Id, StockMovementKind.GoodsReceipt, 1000m, 0.05m), Ct);
        await spa.PostAsync<List<StockMovementDto>>($"{url}/movements", new StockMovementInput(oil.Id, StockMovementKind.Refill, 200m, StoragePlaceId: main.Id, TargetStoragePlaceId: cabinet.Id), Ct);
        var appointment = await spa.BookOkAsync(TestSpa.At(10), Ct, spa.Anna.Id, spa.RoomA.Id);
        await spa.SetStatusAsync(appointment.Id, AppointmentStatus.Completed, Ct);
        await listener.SettleAsync();

        (await spa.Client.PutAsJsonAsync($"/api/appointments/{appointment.Id}/room-care/restock/{oil.Id}", new RestockAnswer(RestockState.Refilled, 300m), TestSpa.Json, Ct))
            .EnsureSuccessStatusCode();

        await listener.NextAsync(OperationsChangeKind.RoomCare);
        await listener.NextAsync(OperationsChangeKind.Stock);
    }

    [Fact]
    public async Task Ein_Handbucheintrag_meldet_Reminders_an_jeden_Standort()
    {
        var (spa, listener) = await WatchAsync();
        await using var _ = listener;
        await listener.SettleAsync();

        await spa.PostAsync<HandbookEntryDto>("/api/handbook", new HandbookEntryInput("Gäste mit Hund", "Hund im Eingang anbinden.", "Empfang", true, [], true), Ct);

        var (location, _) = await listener.NextAsync(OperationsChangeKind.Reminders);
        Assert.Equal(spa.Location.Id, location);
    }

    [Fact]
    public async Task Ein_anderes_Spa_hoert_die_Meldungen_nicht()
    {
        var (spa, listener) = await WatchAsync();
        await using var _ = listener;
        var other = await TestSpa.CreateAsync(fixture, Ct);
        await using var stranger = await Listener.StartAsync(Live(other.Cookie), null);
        await listener.SettleAsync();

        await spa.PostAsync<HandbookEntryDto>("/api/handbook", new HandbookEntryInput("Nur für uns", "Text.", null, true, [], true), Ct);
        await listener.NextAsync(OperationsChangeKind.Reminders);
        await Task.Delay(500, Ct);

        Assert.Empty(stranger.Events);
    }
}
