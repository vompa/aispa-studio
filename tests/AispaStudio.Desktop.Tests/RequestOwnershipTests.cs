using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;

namespace AispaStudio.Desktop.Tests;

public sealed class RequestOwnershipTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public static TheoryData<string, bool, bool> Requests
    {
        get
        {
            var cases = new TheoryData<string, bool, bool>();
            foreach (var path in new[] { "api/appointments", "api/customers", "api/area-visits", "api/appointment-groups" })
            {
                cases.Add(path, true, true);
                cases.Add(path, true, false);
                cases.Add(path, false, false);
            }

            return cases;
        }
    }

    [Theory]
    [MemberData(nameof(Requests))]
    public async Task Beim_Senden_bestimmter_Betrieb_behaelt_die_Vormerkung(string path, bool known, bool logout)
    {
        var origin = Guid.NewGuid();
        var store = new MemoryOfflineStore();
        var cache = new OfflineCache(store) { Tenant = known ? origin : null };
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var server = new StubApi().Hold("POST /" + path, release.Task);
        var api = server.Client(cache);
        var id = Guid.NewGuid();
        var send = SubmitAsync(api, path, id);
        Assert.Contains(server.Calls, call => call.Route == "POST /" + path);
        if (logout)
        {
            await api.SignOutAsync(Ct);
        }

        if (!logout)
        {
            cache.Tenant = Guid.NewGuid();
        }

        release.SetException(new HttpRequestException("Test: offline"));
        var (queued, hasPreview) = await send;
        Assert.Equal(known, queued);
        Assert.False(hasPreview);
        if (logout)
        {
            cache.Tenant = Guid.NewGuid();
        }
        Assert.Empty(cache.Pending);
        Assert.Null(cache.Get($"api/customers/{id}"));
        cache.Tenant = origin;
        if (known)
        {
            var saved = Assert.Single(store.Load().Pending);
            Assert.Equal(origin, saved.TenantId);
            Assert.Equal(path, saved.Path);
            Assert.Single(cache.Pending);
        }
        else
        {
            Assert.Empty(store.Load().Pending);
        }
    }

    private static async Task<(bool Queued, bool HasPreview)> SubmitAsync(ApiClient api, string path, Guid id)
    {
        var visit = TestData.Appointment(10, TestData.Anna) with { Id = id };
        return path switch
        {
            "api/customers" => State(await api.CreateCustomerAsync(new("Test", "Gast", null, null, null, null, id), Ct)),
            "api/appointments" => State(await api.PostOrQueueAsync(path, new { Id = id }, "Test", visit, ct: Ct)),
            "api/area-visits" => State(await api.BookAreaVisitAsync(new(TestData.LocationId, TestData.Room.Id, visit.CustomerId, visit.Start, visit.End, ClientId: id), "Test", "Test", Ct)),
            "api/appointment-groups" => State(await api.BookGroupAsync(new(TestData.LocationId, visit.Start, [], ClientId: id), "Test", Ct)),
            _ => throw new ArgumentOutOfRangeException(nameof(path)),
        };

        static (bool, bool) State<T>(ApiResult<T> result) => (result.Queued, result.Value is not null);
    }

    [Fact]
    public async Task Ohne_Betrieb_startet_kein_Abgleich()
    {
        var cache = new OfflineCache(new MemoryOfflineStore()) { Tenant = Guid.NewGuid() };
        cache.Enqueue(new(Guid.NewGuid(), "api/customers", "{}", "Test", TestData.At(9), null));
        cache.Tenant = null;
        var stub = new StubApi().On("POST /api/customers", new { });
        Assert.Equal(0, await stub.Client(cache).SyncAsync(Ct));
        Assert.Empty(stub.Calls);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(false, true)]
    public async Task Unterbrochener_Abgleich_behaelt_Zuordnung_und_bestaetigte_Version(bool logout, bool foreignChange)
    {
        var origin = Guid.NewGuid();
        var store = new MemoryOfflineStore();
        var cache = new OfflineCache(store) { Tenant = origin };
        var stub = new StubApi { Down = true };
        var api = stub.Client(cache);
        var seen = TestData.Appointment(10, TestData.Anna) with { Version = 7 };
        var path = $"api/appointments/{seen.Id}/move";
        foreach (var hour in new[] { 11, 12 })
        {
            var move = new MoveAppointmentRequest(TestData.At(hour), null, null, null, seen.Version);
            Assert.True((await api.PostOrQueueAsync(path, move, "Test", seen with { Start = move.Start }, ct: Ct)).Queued);
        }

        var nextId = cache.Pending[1].Id;
        var hold = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        stub.Down = false;
        stub.On("POST /" + path, seen with { Version = 8 }).Hold("POST /" + path, hold.Task);
        var events = new List<AppointmentDto>();
        api.Synced += events.Add;
        var sync = api.SyncAsync(Ct);
        Assert.Single(stub.Calls, call => call.Route == "POST /" + path);
        if (logout)
        {
            await api.SignOutAsync(Ct);
        }

        cache.Tenant = Guid.NewGuid();
        hold.SetResult();
        Assert.Equal(1, await sync);
        Assert.Empty(events);
        Assert.Empty(cache.Pending);
        Assert.Empty(cache.Failed);
        Assert.Single(stub.Calls, call => call.Route == "POST /" + path);
        var remaining = Assert.Single(store.Load().Pending);
        Assert.Equal(nextId, remaining.Id);
        Assert.Equal(8u, JsonSerializer.Deserialize<MoveAppointmentRequest>(remaining.Body, ApiClient.Json)!.ExpectedVersion);

        var reopened = new OfflineCache(store) { Tenant = origin };
        using var resumed = new MoveServer(seen with { Version = foreignChange ? 9u : 8u });
        var restarted = new ApiClient(new Uri("http://localhost/"), new MemorySessionStore(), resumed, reopened);
        Assert.Equal(1, await restarted.SyncAsync(Ct));
        Assert.Empty(reopened.Pending);
        Assert.Equal(8u, Assert.Single(resumed.Attempts).ExpectedVersion);
        if (foreignChange)
        {
            Assert.Equal("Fremde Änderung", Assert.Single(reopened.Failed).Reason);
        }
        else
        {
            Assert.Empty(reopened.Failed);
        }
    }
    private sealed class MoveServer(AppointmentDto current) : HttpMessageHandler
    {
        public List<MoveAppointmentRequest> Attempts { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var move = await request.Content!.ReadFromJsonAsync<MoveAppointmentRequest>(ApiClient.Json, cancellationToken);
            Attempts.Add(move!);
            if (move!.ExpectedVersion != current.Version)
            {
                return new(HttpStatusCode.Conflict) { Content = JsonContent.Create(new BookingRejected("Fremde Änderung", [], Current: current), options: ApiClient.Json) };
            }

            return new(HttpStatusCode.OK) { Content = JsonContent.Create(current with { Version = current.Version + 1, Start = move.Start }, options: ApiClient.Json) };
        }
    }

    [Fact]
    public async Task Spaete_Ablehnung_bleibt_im_urspruenglichen_Betrieb()
    {
        var origin = Guid.NewGuid();
        var store = new MemoryOfflineStore();
        var cache = new OfflineCache(store) { Tenant = origin };
        var queued = new PendingChange(Guid.NewGuid(), "api/customers", "{}", "Test", TestData.At(9), null);
        cache.Enqueue(queued);
        cache.Enqueue(queued with { Id = Guid.NewGuid() });
        var held = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var stub = new StubApi().On("POST /api/customers", new { message = "Abgelehnt" }, HttpStatusCode.Conflict)
            .Hold("POST /api/customers", held.Task);
        var sync = stub.Client(cache).SyncAsync(Ct);
        cache.Tenant = Guid.NewGuid();
        held.SetResult();
        Assert.Equal(1, await sync);
        Assert.Empty(cache.Pending);
        Assert.Empty(cache.Failed);
        cache.Tenant = origin;
        Assert.Single(cache.Pending);
        Assert.Equal(queued.Id, Assert.Single(cache.Failed).Id);
        Assert.Single(stub.Calls);
    }

}
