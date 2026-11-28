using System.Net;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;
using AispaStudio.Desktop.ViewModels;

namespace AispaStudio.Desktop.Tests;

/// <summary>Rezeption ohne Internet (ADR 0005): letzter Stand lesen, Änderungen vormerken, danach abgleichen.</summary>
public sealed class OfflineTests
{
    [Fact]
    public async Task Fiscal_secrets_never_enter_or_escape_the_offline_cache_or_queue()
    {
        var register = SpaSample.Register;
        var registration = $"api/pos/registers/{register.Id}/fiscal-registration";
        var credentials = $"api/pos/registers/{register.Id}/fiscal-credentials";
        var normal = $"api/pos/registers/{register.Id}/receipts?date=2026-10-19";
        var calendar = $"api/locations/{SpaSample.LocationId}/calendar?date=2026-10-19";
        var store = new MemoryOfflineStore();
        var old = OfflineState.Empty();
        var now = DateTimeOffset.UtcNow;
        old.Responses[registration] = new CachedResponse("{}", now);
        old.Responses[credentials] = new CachedResponse("{}", now);
        old.Responses[normal] = new CachedResponse("[]", now);
        old.Pending.Add(new PendingChange(Guid.NewGuid(), credentials, "{}", "Test", now, null));
        old.Failed.Add(new FailedChange(Guid.NewGuid(), "Test", "Test", now, Path: "/" + registration, Body: "{}"));
        store.Save(old);

        var offline = new OfflineCache(store);
        var stub = new StubApi().On($"GET /{registration}", new FiscalRegistrationDto(register.Code, "", "", null, "test", true, null))
            .On($"GET /{credentials}", new FiscalCredentialsDto([], null))
            .On($"GET /{calendar}", SpaSample.CalendarDay());
        var api = stub.Client(offline);
        var ct = TestContext.Current.CancellationToken;
        Assert.NotNull((await api.GetAsync<FiscalRegistrationDto>(registration, ct)).Value);
        Assert.NotNull((await api.GetAsync<FiscalCredentialsDto>(credentials, ct)).Value);
        Assert.NotNull((await api.GetAsync<object>(calendar, ct)).Value);
        offline.Put(registration, "{}");
        offline.Enqueue(new PendingChange(Guid.NewGuid(), "api/pos/registers", "{}", "Test", now, null));
        offline.Flush();

        var persisted = store.Load();
        Assert.Null(offline.Get(registration));
        Assert.Null(offline.Get(credentials));
        Assert.Equal(new[] { calendar, normal }.Order(), persisted.Responses.Keys.Order());
        Assert.Empty(persisted.Pending);
        Assert.Empty(persisted.Failed);
        stub.Down = true;
        Assert.Null((await api.GetAsync<FiscalRegistrationDto>(registration, ct)).Value);
        Assert.Null((await api.GetAsync<FiscalCredentialsDto>(credentials, ct)).Value);
        Assert.NotNull((await api.GetAsync<object>(calendar, ct)).CachedAt);
        Assert.NotNull((await api.GetAsync<List<ReceiptDto>>(normal, ct)).CachedAt);
    }

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

    private static async Task<ShellViewModel> StartAsync(StubApi stub, OfflineCache cache, ISessionStore? session = null)
    {
        var main = new MainViewModel(stub.Client(cache, session), time: new SampleTime());
        await main.StartAsync();
        var shell = Assert.IsType<ShellViewModel>(main.Content);
        await shell.Calendar.LoadAsync();
        return shell;
    }

    private static CalendarItem Item(CalendarViewModel calendar, Guid id) => calendar.Columns.SelectMany(c => c.Items).Single(i => i.Id == id);

    [Theory]
    [InlineData(49, true)]
    [InlineData(47, false)]
    public async Task Given_aeltester_Ersatzbeleg_vor_n_Stunden_When_ohne_Verbindung_Then_erinnert_der_Banner_nach_48_Stunden_an_die_Meldung(int hours, bool hint)
    {
        // Given ohne Verbindung ein vorgemerkter Ersatzbeleg, ausgegeben vor n Stunden
        var stub = Api();
        var cache = new OfflineCache(new MemoryOfflineStore(), new SampleTime());
        var shell = await StartAsync(stub, cache);
        stub.Down = true;
        await shell.Calendar.LoadAsync();
        var issued = SpaSample.At(11, 40).AddHours(-hours);
        var substitute = SpaSample.Receipt() with { Number = 0, SubstituteNumber = "E-WIEN-1-1", SubstituteIssuedAt = issued };
        cache.Enqueue(new PendingChange(Guid.NewGuid(), "api/pos/registers/x/substitute-receipts", "{}", "Ersatzbeleg E-WIEN-1-1: 138,80 €", issued, null,
            Substitute: substitute));

        // When die App den Stand prüft
        await shell.CheckInAsync(Ct);

        // Then nennt der Banner ab 48 Stunden den ältesten Ersatzbeleg und die Meldung über FinanzOnline
        Assert.True(shell.IsOffline);
        Assert.Equal(hint, shell.LongOutageHint is not null);
        Assert.Equal(hint, shell.OfflineBanner!.Contains("seit mehr als 48 Stunden nicht nacherfasst", StringComparison.Ordinal));
        Assert.Equal(hint, shell.OfflineBanner!.Contains("FinanzOnline", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Given_verbunden_und_abgelehnter_Ersatzbeleg_seit_50_Stunden_Then_steht_die_Erinnerung_unter_nicht_uebernommen()
    {
        // Given die App ist verbunden, die Kasse hat einen 50 Stunden alten Ersatzbeleg abgelehnt
        var stub = Api();
        var cache = new OfflineCache(new MemoryOfflineStore(), new SampleTime());
        var shell = await StartAsync(stub, cache);
        Assert.Equal("Bitte neu klären.", shell.FailedMessage);
        var issued = SpaSample.At(11, 40).AddHours(-50);
        var substitute = SpaSample.Receipt() with { Number = 0, SubstituteNumber = "E-WIEN-1-1", SubstituteIssuedAt = issued };
        var id = Guid.NewGuid();
        cache.Enqueue(new PendingChange(id, "api/pos/registers/x/substitute-receipts", "{}", "Ersatzbeleg E-WIEN-1-1: 138,80 €", issued, null, Substitute: substitute));
        cache.Complete(id, new FailedChange(id, "Ersatzbeleg E-WIEN-1-1: 138,80 €", "Dieser Termin steht schon auf einer Rechnung.", SpaSample.At(11, 40),
            Substitute: substitute, Path: "api/pos/registers/x/substitute-receipts", Body: "{}"));

        // When die App den Stand prüft
        await shell.CheckInAsync(Ct);

        // Then ist sie verbunden, und die Erinnerung an die Meldung steht beim abgelehnten Ersatzbeleg
        Assert.False(shell.IsOffline);
        Assert.Null(shell.OfflineBanner);
        Assert.True(shell.HasFailed);
        Assert.StartsWith("Bitte neu klären. Der älteste Ersatzbeleg ist vom", shell.FailedMessage, StringComparison.Ordinal);
        Assert.Contains("FinanzOnline", shell.FailedMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Ohne_Verbindung_letzter_Stand_und_vorgemerkter_Status_auch_nach_Neustart()
    {
        var stub = Api();
        var store = new MemoryOfflineStore();
        var cache = new OfflineCache(store, new SampleTime());
        var session = new MemorySessionStore();
        session.Save("test-session");
        var shell = await StartAsync(stub, cache, session);
        Assert.Null(shell.Calendar.OfflineNote);

        stub.Down = true;
        await shell.Calendar.LoadAsync();
        Assert.True(shell.IsOffline);
        Assert.Equal("Ohne Verbindung · Stand 19.10. 11:40", shell.Calendar.OfflineNote);
        Assert.Equal("Offline", shell.ConnectionText);
        Assert.Contains("Die Kassa gibt Ersatzbelege aus", shell.OfflineBanner, StringComparison.Ordinal);

        shell.Calendar.OpenAppointment(SpaSample.Selected);
        await shell.Calendar.Selected!.CheckInCommand.ExecuteAsync(null);
        var item = Item(shell.Calendar, SpaSample.Selected.Id);
        Assert.True(item.IsPending);
        Assert.Equal(AppointmentStatus.CheckedIn, item.Status);
        Assert.Contains("vorgemerkt", item.MetaText, StringComparison.Ordinal);
        Assert.Equal(1, shell.PendingCount);

        // App neu gestartet, Netz immer noch weg: angemeldet bleiben, Stand und Vorgemerktes sind noch da.
        var restarted = await StartAsync(stub, new OfflineCache(store, new SampleTime()), session);
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
        Assert.Contains("Die App prüft alle 15 Sekunden von selbst und lädt neu, sobald die Verbindung wieder steht.", shell.OfflineBanner, StringComparison.Ordinal);
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

    private sealed class MovingTime : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = SpaSample.At(11, 40);

        public override DateTimeOffset GetUtcNow() => Now;
    }

    private static readonly CustomerDto Anna = SpaSample.Customers[0] with { Id = Guid.NewGuid(), FirstName = "Anna", LastName = "Anonym", PreferredName = null };

    private static string Json(object value) => System.Text.Json.JsonSerializer.Serialize(value, value.GetType(), ApiClient.Json);

    /// <summary>Stand, in dem der Gast überall vorkommt, wo die App ihn ablegt, dazu Fremdes, das bleiben muss.</summary>
    private static void FillWith(OfflineCache cache, CustomerDto guest)
    {
        var location = SpaSample.LocationId;
        cache.Put($"api/customers/{guest.Id}", Json(guest));
        cache.Put($"api/customers/{guest.Id}/appointments", "[]");
        cache.Put($"api/customers?search={Uri.EscapeDataString("Anna Anonym")}", Json(new List<CustomerDto> { guest }));
        cache.Put("api/customers?search=", Json(SpaSample.Customers.Append(guest).ToList()));
        cache.Put($"api/locations/{location}/calendar?date=2026-10-20", Json(SpaSample.CalendarDay() with
        {
            Appointments = [SpaSample.Selected with { Id = Guid.NewGuid(), CustomerId = guest.Id, CustomerName = "Anna Anonym" }],
        }));
        cache.Put($"api/pos/locations/{location}/day-list?date=2026-10-19", "{\"entries\":[{\"customerName\":\"Anna Anonym\",\"appointment\":{\"customerId\":\"" + guest.Id + "\"}}]}");

        // Diese Antworten tragen den Namen ohne Kunden-Id.
        cache.Put($"api/locations/{location}/appointment-holds", """[{"customerName":"Anna Anonym"}]""");
        cache.Put($"api/locations/{location}/gap-suggestions", """[{"customerName":"Anna Anonym"}]""");
        cache.Put($"api/locations/{location}/closures/{Guid.NewGuid()}/notices", """[{"customerName":"Anna Anonym"}]""");
        cache.Put($"api/locations/{location}/closure-overview?scope=open", """{"closures":[{"appointments":[{"customerName":"Anna Anonym"}]}]}""");
        cache.Put($"api/locations/{location}/room-closures/{Guid.NewGuid()}", """{"appointments":[{"customerName":"Anna Anonym"}]}""");
        cache.Put($"api/locations/{location}/voucher-orders", """[{"buyerEmail":"anna.anonym@example.com","recipientName":"Anna Anonym"}]""");

        cache.Put("api/locations", Json(new List<LocationDto> { SpaSample.Vienna }));
        cache.Put($"api/customers/{SpaSample.Customers[1].Id}", Json(SpaSample.Customers[1]));
        cache.Put($"api/locations/{location}/calendar?date=2026-10-19", Json(SpaSample.CalendarDay()));
    }

    [Fact]
    public void Nach_dem_Anonymisieren_enthaelt_der_Stand_den_Gast_nirgends_mehr_und_alles_andere_bleibt()
    {
        // Given ein Stand mit dem Gast in Kundenkarte, Suchlisten, Kalendertag, Tagesliste der Kassa und drei Antworten ohne Kunden-Id
        var store = new MemoryOfflineStore();
        var cache = new OfflineCache(store, new SampleTime());
        FillWith(cache, Anna);

        // When der Gast aus dem Stand entfernt wird
        cache.RemoveCustomers([Anna.Id]);

        // Then steht weder die Id noch der Name noch ein Suchbegriff im gespeicherten Stand, Fremdes bleibt
        var saved = store.Load().Responses;
        Assert.DoesNotContain(saved, r => r.Key.Contains(Anna.Id.ToString(), StringComparison.Ordinal) || r.Value.Json.Contains(Anna.Id.ToString(), StringComparison.Ordinal));
        Assert.DoesNotContain(saved, r => r.Key.Contains("Anonym", StringComparison.Ordinal) || r.Value.Json.Contains("Anonym", StringComparison.Ordinal));
        Assert.DoesNotContain(saved.Keys, k => k.StartsWith("api/customers?search=", StringComparison.Ordinal));
        Assert.Contains("api/locations", saved.Keys);
        Assert.Contains($"api/customers/{SpaSample.Customers[1].Id}", saved.Keys);
        Assert.Contains($"api/locations/{SpaSample.LocationId}/calendar?date=2026-10-19", saved.Keys);

        // And die Suche ohne Verbindung findet den Gast nicht, andere Gäste schon
        Assert.DoesNotContain("Anonym", cache.Get("api/customers?search=anna")!.Json, StringComparison.Ordinal);
        Assert.Contains(SpaSample.Customers[1].Id.ToString(), cache.Get("api/customers?search=")!.Json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Eine_Suche_legt_ihren_Suchbegriff_nicht_im_Stand_ab()
    {
        // Given die Suche nach einem Namen liefert einen Gast
        var stub = Api().On($"GET /api/customers?search={Uri.EscapeDataString("Anna Anonym")}", new List<CustomerDto> { Anna });
        var store = new MemoryOfflineStore();
        var cache = new OfflineCache(store, new SampleTime());

        // When die App sucht
        await stub.Client(cache).GetAsync<List<CustomerDto>>($"api/customers?search={Uri.EscapeDataString("Anna Anonym")}", Ct);
        cache.Flush();

        // Then liegt der Gast als Kundenkarte im Stand, kein Schlüssel trägt den Namen, und die Suche ohne Verbindung findet ihn
        Assert.Contains($"api/customers/{Anna.Id}", store.Load().Responses.Keys);
        Assert.DoesNotContain(store.Load().Responses.Keys, k => k.Contains("search", StringComparison.Ordinal));
        Assert.Contains("Anonym", cache.Get("api/customers?search=anna")!.Json, StringComparison.Ordinal);
    }

    [Fact]
    public void Die_Suche_ohne_Verbindung_zeigt_keinen_anonymisierten_Gast()
    {
        // Given ein anderes Gerät hat die Karte nach dem Anonymisieren neu geladen: Platzhalter mit DeletedAt
        var cache = new OfflineCache(new MemoryOfflineStore(), new SampleTime());
        cache.Put($"api/customers/{Anna.Id}", Json(Anna with { FirstName = "Gelöschter", LastName = "Gast", DeletedAt = SpaSample.At(10) }));
        cache.Put($"api/customers/{SpaSample.Customers[1].Id}", Json(SpaSample.Customers[1]));

        // When ohne Verbindung gesucht wird
        var found = cache.Get("api/customers?search=")!.Json;

        // Then fehlt der Platzhalter
        Assert.DoesNotContain("Gelöschter", found, StringComparison.Ordinal);
        Assert.Contains(SpaSample.Customers[1].Id.ToString(), found, StringComparison.Ordinal);
    }

    [Fact]
    public void Die_Suche_ohne_Verbindung_prueft_jeden_Begriff_einzeln_wie_der_Server()
    {
        // Given die Karte von Anna Anonym liegt im Stand
        var cache = new OfflineCache(new MemoryOfflineStore(), new SampleTime());
        cache.Put($"api/customers/{Anna.Id}", Json(Anna));

        // When nach den Anfängen von Vor- und Nachname gesucht wird, Then trifft die Suche; ein fremder Begriff trifft nicht
        Assert.Contains(Anna.Id.ToString(), cache.Get("api/customers?search=Ann%20Ano")!.Json, StringComparison.Ordinal);
        Assert.DoesNotContain(Anna.Id.ToString(), cache.Get("api/customers?search=Ann%20Huber")!.Json, StringComparison.Ordinal);
    }

    [Fact]
    public void Eine_vorgemerkte_Buchung_nennt_den_anonymisierten_Gast_nicht_mehr_und_bleibt_vorgemerkt()
    {
        // Given ohne Verbindung ist eine Buchung für den Gast vorgemerkt, daneben eine für jemand anderen
        var store = new MemoryOfflineStore();
        var cache = new OfflineCache(store, new SampleTime());
        var preview = SpaSample.Selected with { CustomerId = Anna.Id, CustomerName = "Anna Anonym" };
        cache.Enqueue(new PendingChange(Guid.NewGuid(), "api/appointments", "{\"customerId\":\"" + Anna.Id + "\"}", "Anna Anonym, Massage buchen", SpaSample.At(11), preview));
        cache.Enqueue(new PendingChange(Guid.NewGuid(), "api/appointments", "{}", "Bea Beispiel, Massage buchen", SpaSample.At(11), null));

        // When der Gast aus dem Stand entfernt wird
        cache.RemoveCustomers([Anna.Id]);

        // Then bleiben beide vorgemerkt, die eine ohne den Namen
        var pending = store.Load().Pending;
        Assert.Equal(2, pending.Count);
        Assert.Equal("Gelöschter Gast: vorgemerkte Änderung", pending[0].Description);
        Assert.Equal("Gelöschter Gast", pending[0].Preview!.CustomerName);
        Assert.Equal("Bea Beispiel, Massage buchen", pending[1].Description);
    }

    [Fact]
    public void Ein_Stand_aelter_als_vierzehn_Tage_wird_nicht_mehr_gezeigt()
    {
        // Given ein Gerät hat eine Kundenkarte geladen und war dann zwanzig Tage aus
        var time = new MovingTime();
        var cache = new OfflineCache(new MemoryOfflineStore(), time);
        cache.Put($"api/customers/{Anna.Id}", Json(Anna));
        time.Now += TimeSpan.FromDays(13);
        Assert.NotNull(cache.Get($"api/customers/{Anna.Id}"));

        // When es ohne Verbindung startet
        time.Now += TimeSpan.FromDays(7);

        // Then liefert der Stand weder die Karte noch einen Treffer in der Suche
        Assert.Null(cache.Get($"api/customers/{Anna.Id}"));
        Assert.DoesNotContain("Anonym", cache.Get("api/customers?search=")!.Json, StringComparison.Ordinal);
    }

    [Fact]
    public void Ein_Vertragstyp_mit_Kundenname_traegt_die_Kunden_Id_oder_steht_auf_der_Liste_der_Ausnahmen()
    {
        // Das Bereinigen findet einen Gast über seine Id. Antworten mit Namen ohne Id entfernt OfflineCache.RemoveCustomers über den Pfad;
        // ein neuer Typ ohne Id braucht dort einen Eintrag, sonst bleibt der Klarname im Stand.
        string[] removedByPath =
        [
            nameof(AppointmentHoldDto), nameof(GapSuggestion), nameof(GapChainMove), nameof(ClosureNoticeDto), nameof(ClosureAppointmentDto),
            nameof(ClosureWaitlistDto),
        ];

        // Anamnese wird nie zwischengespeichert (IsCacheable).
        string[] neverCached = [nameof(AnamnesisPaperSheet)];

        // Jede Zeile der Tagesliste trägt Termin oder Besuch und damit die Kunden-Id (PosDayList im Server).
        string[] idInside = [nameof(PosDayEntryDto)];

        var missing = typeof(CustomerDto).Assembly.GetTypes()
            .Where(t => t.GetProperty("CustomerName") is not null && t.GetProperty("CustomerId") is null)
            .Select(t => t.Name)
            .Except(removedByPath)
            .Except(neverCached)
            .Except(idInside)
            .Order()
            .ToList();

        Assert.Empty(missing);
    }

    private const string AnonymizedRoute = "GET /api/customers/anonymized?since=2026-10-04T09:40:00Z";

    [Fact]
    public async Task Beim_Vorausladen_entfernt_ein_anderes_Geraet_die_inzwischen_anonymisierten_Gaeste()
    {
        // Given ein Gerät mit dem Gast im Stand; der Server meldet ihn als anonymisiert
        var stub = Api().On(AnonymizedRoute, new AnonymizedCustomers([Anna.Id]));
        var store = new MemoryOfflineStore();
        var cache = new OfflineCache(store, new SampleTime());
        var shell = await StartAsync(stub, cache);
        FillWith(cache, Anna);

        // When die App vorauslädt
        await shell.PrefetchAsync(Ct);

        // Then ist der Gast aus dem Stand entfernt, und die Liste der Ids selbst liegt nicht im Stand
        Assert.Contains(stub.Calls, c => c.Route == AnonymizedRoute);
        Assert.DoesNotContain(store.Load().Responses, r => r.Value.Json.Contains("Anonym", StringComparison.Ordinal));
        Assert.DoesNotContain(store.Load().Responses.Keys, k => k.Contains("anonymized", StringComparison.Ordinal));
        Assert.False(OfflineCache.IsCacheable("api/customers/anonymized?since=2026-10-04T09:40:00Z"));
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task Ohne_Antwort_auf_die_Liste_der_Anonymisierten_bleibt_der_Stand_wie_er_ist(HttpStatusCode status)
    {
        var stub = Api().On(AnonymizedRoute, null, status);
        var cache = new OfflineCache(new MemoryOfflineStore(), new SampleTime());
        var shell = await StartAsync(stub, cache);
        FillWith(cache, Anna);

        await shell.PrefetchAsync(Ct);

        Assert.NotNull(cache.Get($"api/customers/{Anna.Id}"));
    }
}
