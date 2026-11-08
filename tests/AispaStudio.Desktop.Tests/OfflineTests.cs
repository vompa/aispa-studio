using System.Net;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;
using AispaStudio.Desktop.ViewModels;

namespace AispaStudio.Desktop.Tests;

/// <summary>Rezeption ohne Internet (ADR 0005): letzter Stand lesen, Änderungen vormerken, danach abgleichen.</summary>
public sealed class OfflineTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private sealed class SampleTime : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => SpaSample.At(11, 40);

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna");
    }

    private static readonly CurrentUser Reception = new("u1", "rita@aispa.test", "Rita Steiner", 1,
        new CurrentAccess(Guid.NewGuid(), "Day-Spa Wien", StaffRole.Reception, true, []), null,
        [AccessRight.ViewCalendar, AccessRight.Book, AccessRight.ChangeStatus]);

    private static StubApi Api() => new StubApi()
        .On("GET /api/system/status", new SystemStatus("0.9.0", "Development", DateTimeOffset.UtcNow))
        .On("GET /api/auth/me", Reception)
        .On("GET /api/locations", new List<LocationDto> { SpaSample.Vienna })
        .On($"GET /api/locations/{SpaSample.LocationId}/resources", SpaSample.Resources)
        .On($"GET /api/locations/{SpaSample.LocationId}/calendar?date=2026-10-19", SpaSample.CalendarDay())
        .On("GET /api/customers?search=", SpaSample.Customers.ToList());

    private static async Task<ShellViewModel> StartAsync(StubApi stub, OfflineCache cache)
    {
        var main = new MainViewModel(stub.Client(cache), time: new SampleTime());
        await main.StartAsync();
        var shell = Assert.IsType<ShellViewModel>(main.Content);
        await shell.Calendar.LoadAsync();
        return shell;
    }

    private static CalendarItem Item(CalendarViewModel calendar, Guid id) => calendar.Columns.SelectMany(c => c.Items).Single(i => i.Id == id);

    [Fact]
    public async Task Ohne_Verbindung_letzter_Stand_und_vorgemerkter_Status_auch_nach_Neustart()
    {
        var stub = Api();
        var store = new MemoryOfflineStore();
        var cache = new OfflineCache(store, new SampleTime());
        var shell = await StartAsync(stub, cache);
        Assert.Null(shell.Calendar.OfflineNote);

        stub.Down = true;
        await shell.Calendar.LoadAsync();
        Assert.True(shell.IsOffline);
        Assert.Equal("Ohne Verbindung · Stand 19.10. 11:40", shell.Calendar.OfflineNote);
        Assert.Equal("Offline", shell.ConnectionText);
        Assert.Contains("Kassieren geht erst wieder mit Verbindung", shell.OfflineBanner, StringComparison.Ordinal);

        shell.Calendar.OpenAppointment(SpaSample.Selected);
        await shell.Calendar.Selected!.CheckInCommand.ExecuteAsync(null);
        var item = Item(shell.Calendar, SpaSample.Selected.Id);
        Assert.True(item.IsPending);
        Assert.Equal(AppointmentStatus.CheckedIn, item.Status);
        Assert.Contains("vorgemerkt", item.MetaText, StringComparison.Ordinal);
        Assert.Equal(1, shell.PendingCount);

        // App neu gestartet, Netz immer noch weg: angemeldet bleiben, Stand und Vorgemerktes sind noch da.
        var restarted = await StartAsync(stub, new OfflineCache(store, new SampleTime()));
        Assert.True(Item(restarted.Calendar, SpaSample.Selected.Id).IsPending);
        Assert.Equal(1, restarted.PendingCount);

        // Netz wieder da: Abgleich sendet, der Server bestätigt, der Kalender zeigt wieder den Server-Stand.
        stub.Down = false;
        stub.On($"POST /api/appointments/{SpaSample.Selected.Id}/status",
            SpaSample.Selected with { Status = AppointmentStatus.CheckedIn, UpdatedAt = SpaSample.At(11, 50) });
        stub.On($"GET /api/locations/{SpaSample.LocationId}/calendar?date=2026-10-19", SpaSample.CalendarDay() with
        {
            Appointments = [.. SpaSample.CalendarDay().Appointments.Select(a => a.Id == SpaSample.Selected.Id ? a with { Status = AppointmentStatus.CheckedIn } : a)],
        });
        await restarted.CheckInAsync(Ct);

        Assert.Contains("CheckedIn", stub.Calls.Single(c => c.Route.StartsWith("POST", StringComparison.Ordinal)).Body, StringComparison.Ordinal);
        Assert.Equal(0, restarted.PendingCount);
        Assert.False(restarted.IsOffline);
        Assert.Null(restarted.Calendar.OfflineNote);
        Assert.False(Item(restarted.Calendar, SpaSample.Selected.Id).IsPending);
        Assert.Equal(AppointmentStatus.CheckedIn, Item(restarted.Calendar, SpaSample.Selected.Id).Status);
    }

    [Fact]
    public async Task Ist_die_Verbindung_wieder_da_laedt_die_Kundenliste_von_selbst_neu_und_das_Banner_sagt_dass_die_App_selbst_prueft()
    {
        var stub = Api();
        var shell = await StartAsync(stub, new OfflineCache(new MemoryOfflineStore(), new SampleTime()));
        await shell.Customers.InitializeAsync();

        stub.Down = true;
        await shell.Calendar.LoadAsync();
        Assert.Contains("Die App prüft alle 15 Sekunden von selbst und lädt neu, sobald der Server wieder antwortet.", shell.OfflineBanner, StringComparison.Ordinal);
        var before = stub.Calls.Count(c => c.Route == "GET /api/customers?search=");

        stub.Down = false;
        await shell.CheckInAsync(Ct);

        Assert.False(shell.IsOffline);
        Assert.True(stub.Calls.Count(c => c.Route == "GET /api/customers?search=") > before, "Die Kundenliste wurde nach der Rückkehr nicht neu geladen.");
    }

    [Fact]
    public async Task Offline_gebucht_und_beim_Abgleich_abgelehnt()
    {
        var stub = Api();
        var cache = new OfflineCache(new MemoryOfflineStore(), new SampleTime());
        var shell = await StartAsync(stub, cache);
        await stub.Client(cache).GetAsync<List<CustomerDto>>("api/customers?search=", Ct);

        stub.Down = true;
        var calendar = shell.Calendar;
        calendar.StartBooking(calendar.Columns[1], 330 * DayLayout.PixelsPerMinute);
        await calendar.Booking!.SearchAsync("mül");
        Assert.Equal(2, calendar.Booking.Customers.Count); // Suche im lokalen Stand
        calendar.Booking.Customer = calendar.Booking.Customers.First(c => c.LastName == "Müller");
        await calendar.Booking.BookCommand.ExecuteAsync(null);

        Assert.Null(calendar.Booking);
        var pending = Assert.Single(cache.Pending);
        Assert.Contains("\"clientId\"", pending.Body, StringComparison.Ordinal);
        var preview = Item(calendar, pending.Preview!.Id);
        Assert.True(preview.IsPending);
        Assert.Equal("Brigitte Müller", preview.Title);
        Assert.Contains(preview, calendar.Columns[1].Items);

        stub.Down = false;
        stub.On("POST /api/appointments", new BookingRejected("Zu dieser Zeit ist niemand frei.", []), HttpStatusCode.Conflict);
        await shell.CheckInAsync(Ct);

        Assert.Empty(cache.Pending);
        var failed = Assert.Single(shell.Failed);
        Assert.Contains("Brigitte Müller", failed.Description, StringComparison.Ordinal);
        Assert.Equal("Zu dieser Zeit ist niemand frei.", failed.Reason);
        Assert.True(shell.HasFailed);
        Assert.DoesNotContain(calendar.Columns.SelectMany(c => c.Items), i => i.Id == pending.Preview.Id);

        shell.DismissFailedCommand.Execute(failed);
        Assert.False(shell.HasFailed);
    }

    [Fact]
    public async Task Neue_Kundin_ohne_Verbindung_anlegen_buchen_und_beim_Abgleich_in_Reihenfolge_senden()
    {
        var stub = Api();
        var cache = new OfflineCache(new MemoryOfflineStore(), new SampleTime());
        var shell = await StartAsync(stub, cache);

        stub.Down = true;
        var customers = shell.Customers;
        customers.NewFirstName = "Olga";
        customers.NewLastName = "Offline";
        customers.NewPhone = "+43 660 123";
        await customers.CreateCommand.ExecuteAsync(null);
        var olga = customers.Results[0];
        Assert.Equal("Olga", olga.FirstName);
        Assert.StartsWith("Ohne Verbindung vorgemerkt", customers.Notice, StringComparison.Ordinal);
        Assert.Equal(olga, customers.Selected);
        Assert.Null(customers.Error);

        // Gleich im Kalender buchbar: die lokale Suche findet sie, die Buchung verweist auf ihre Id.
        var calendar = shell.Calendar;
        calendar.StartBooking(calendar.Columns[1], 330 * DayLayout.PixelsPerMinute);
        await calendar.Booking!.SearchAsync("olga");
        calendar.Booking.Customer = Assert.Single(calendar.Booking.Customers);
        await calendar.Booking.BookCommand.ExecuteAsync(null);
        Assert.Equal(2, cache.Pending.Count);
        Assert.Equal("api/customers", cache.Pending[0].Path);
        Assert.Contains($"\"clientId\":\"{olga.Id}\"", cache.Pending[0].Body, StringComparison.Ordinal);
        Assert.Contains($"\"customerId\":\"{olga.Id}\"", cache.Pending[1].Body, StringComparison.Ordinal);

        stub.Down = false;
        var booked = SpaSample.Unbilled()[0] with { Id = cache.Pending[1].Preview!.Id, CustomerId = olga.Id, CustomerName = "Olga Offline" };
        stub.On("POST /api/customers", olga, HttpStatusCode.Created).On("POST /api/appointments", booked, HttpStatusCode.Created);
        await shell.CheckInAsync(Ct);

        Assert.Empty(cache.Pending);
        Assert.False(shell.HasFailed);
        var order = stub.Calls.Where(c => c.Route.StartsWith("POST", StringComparison.Ordinal)).Select(c => c.Route).ToList();
        Assert.Equal(["POST /api/customers", "POST /api/appointments"], order);
    }

    [Fact]
    public async Task Vorausladen_der_naechsten_Tage_und_Kundenkarten()
    {
        var stub = Api();
        var guest = SpaSample.Customers[0];
        var day = SpaSample.CalendarDay();
        stub.On($"GET /api/locations/{SpaSample.LocationId}/calendar?date=2026-10-20", day with
        {
            Date = SpaSample.Day.AddDays(1),
            Appointments = [SpaSample.Selected with { Id = Guid.NewGuid(), CustomerId = guest.Id, Start = SpaSample.At(10).AddDays(1) }],
        });
        stub.On($"GET /api/customers/{guest.Id}", guest);
        var cache = new OfflineCache(new MemoryOfflineStore(), new SampleTime());
        var shell = await StartAsync(stub, cache);

        await shell.PrefetchAsync(Ct);

        Assert.Contains(stub.Calls, c => c.Route == $"GET /api/locations/{SpaSample.LocationId}/calendar?date=2026-10-25");
        stub.Down = true;
        var api = stub.Client(cache);
        Assert.NotNull((await api.GetAsync<CalendarDay>($"api/locations/{SpaSample.LocationId}/calendar?date=2026-10-20", Ct)).CachedAt);
        Assert.Equal("Brigitte", (await api.GetAsync<CustomerDto>($"api/customers/{guest.Id}", Ct)).Value!.FirstName);
    }

    [Fact]
    public async Task Abmelden_ohne_Verbindung_behaelt_Vorgemerktes_bis_zur_naechsten_Anmeldung_im_selben_Spa()
    {
        var stub = Api();
        var store = new MemoryOfflineStore();
        var cache = new OfflineCache(store, new SampleTime());
        var shell = await StartAsync(stub, cache);

        stub.Down = true;
        await shell.Calendar.LoadAsync();
        shell.Calendar.OpenAppointment(SpaSample.Selected);
        await shell.Calendar.Selected!.CheckInCommand.ExecuteAsync(null);
        Assert.Equal(1, shell.PendingCount);

        await shell.SignOutCommand.ExecuteAsync(null);

        // Gelesenes (Kalender, Kundenkarten) ist weg, die vorgemerkte Änderung nicht.
        Assert.Empty(store.Load().Responses);
        Assert.Equal(Reception.Access!.TenantId, Assert.Single(store.Load().Pending).TenantId);

        stub.Down = false;
        stub.On($"POST /api/appointments/{SpaSample.Selected.Id}/status",
            SpaSample.Selected with { Status = AppointmentStatus.CheckedIn, UpdatedAt = SpaSample.At(11, 50) });
        var next = await StartAsync(stub, cache);
        Assert.Equal(1, next.PendingCount);
        await next.CheckInAsync(Ct);

        Assert.Contains("CheckedIn", stub.Calls.Single(c => c.Route.StartsWith("POST /api/appointments", StringComparison.Ordinal)).Body, StringComparison.Ordinal);
        Assert.Equal(0, next.PendingCount);
        Assert.Empty(store.Load().Pending);
    }

    [Fact]
    public void Vorgemerktes_eines_anderen_Spas_wird_nicht_gezeigt_und_nicht_gesendet()
    {
        var store = new MemoryOfflineStore();
        var cache = new OfflineCache(store, new SampleTime());
        Guid first = Guid.NewGuid(), second = Guid.NewGuid();
        cache.Tenant = first;
        cache.Enqueue(new PendingChange(Guid.NewGuid(), "api/customers", "{}", "Neue Kundin", SpaSample.At(11), null));
        cache.Put("api/locations", "[]");

        cache.Clear();
        Assert.Null(cache.Get("api/locations"));
        cache.Tenant = second;
        var foreign = cache.Pending;
        cache.Tenant = first;

        Assert.Empty(foreign);
        Assert.Equal(first, Assert.Single(cache.Pending).TenantId);
    }

    [Fact]
    public async Task Gesundheitsdaten_bleiben_draussen_und_Abmelden_loescht_den_Stand()
    {
        Assert.False(OfflineCache.IsCacheable($"api/customers/{Guid.NewGuid()}/anamnesis"));
        Assert.False(OfflineCache.IsCacheable("api/health/records"));
        Assert.False(OfflineCache.IsCacheable("api/reports/daily"));
        Assert.True(OfflineCache.IsCacheable($"api/locations/{Guid.NewGuid()}/calendar?date=2026-10-19"));

        var stub = Api().On("POST /api/auth/logout", null);
        var store = new MemoryOfflineStore();
        var cache = new OfflineCache(store, new SampleTime());
        var api = stub.Client(cache);
        await api.GetAsync<List<LocationDto>>("api/locations", Ct);
        Assert.NotNull(cache.Get("api/locations"));

        await api.SignOutAsync(Ct);

        Assert.Null(cache.Get("api/locations"));
        Assert.Empty(store.Load().Responses);
    }
}
