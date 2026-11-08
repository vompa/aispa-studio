using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AispaStudio.Api.Scheduling;
using AispaStudio.Api.Tenancy;
using AispaStudio.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace AispaStudio.Api.Tests;

/// <summary>
/// Ein kleines Spa pro Test, über die API angelegt: Standort in Wien (Mo–Sa 9–20 Uhr), zwei Räume, zwei Personen mit Massage
/// (Mo 9–17 Uhr), eine Massage (50 min, 5 min Vorbereitung, 10 min Nachbereitung) und eine Kundin.
/// Jeder Test bekommt eigene Daten, deshalb stören sich Tests in derselben Datenbank nicht.
/// </summary>
internal sealed class TestSpa
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    /// <summary>Ein Montag in der Winterzeit (Wien = UTC+1).</summary>
    public static readonly DateOnly Monday = new(2027, 3, 1);

    private static readonly TimeZoneInfo Vienna = TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna");

    private TestSpa(ApiFixture fixture, string cookie)
    {
        Fixture = fixture;
        Cookie = cookie;
        Client = fixture.CreateClient(cookie);
    }

    public ApiFixture Fixture { get; }
    public string Cookie { get; }
    public HttpClient Client { get; }
    public LocationDto Location { get; private set; } = null!;
    public RoomDto RoomA { get; private set; } = null!;
    public RoomDto RoomB { get; private set; } = null!;
    public QualificationDto Massage { get; private set; } = null!;
    public EmployeeDto Anna { get; private set; } = null!;
    public EmployeeDto Ben { get; private set; } = null!;
    public TreatmentDto MassageTreatment { get; private set; } = null!;
    public CustomerDto Customer { get; private set; } = null!;
    public Guid TenantId { get; private set; }

    /// <summary>Ohne Anmeldung des Teams, wie die Buchungsseite dieses Spas (Kundenbereich, Mandant über die Kopfzeile X-Aispa-Spa).</summary>
    public HttpClient OnlineClient(string? customerCookie = null)
    {
        var client = Fixture.CreateClient(customerCookie);
        client.DefaultRequestHeaders.Add("X-Aispa-Spa", TenantId.ToString());
        return client;
    }

    public static async Task<TestSpa> CreateAsync(ApiFixture fixture, CancellationToken ct)
    {
        var spa = new TestSpa(fixture, await TestUsers.RegisterAsync(fixture, ct));
        spa.TenantId = (await spa.GetAsync<CurrentUser>("/api/auth/me", ct)).Access!.TenantId;
        var weekdays = new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday };

        spa.Location = await spa.CreateLocationAsync("Wien", weekdays, ct);
        // Ohne Wartezeit, damit Absagen des Teams wie vor Ä95 sofort zugestellt werden; die Tests zur Wartezeit stellen sie selbst ein.
        _ = await spa.PutAsync<GuestMessageDelayDto>($"/api/locations/{spa.Location.Id}/guest-message-delay", new GuestMessageDelayDto(0), ct);
        spa.RoomA = await spa.PostAsync<RoomDto>($"/api/locations/{spa.Location.Id}/rooms", new RoomInput("Raum A", 15), ct);
        spa.RoomB = await spa.PostAsync<RoomDto>($"/api/locations/{spa.Location.Id}/rooms", new RoomInput("Raum B", 0), ct);
        spa.Massage = await spa.PostAsync<QualificationDto>("/api/qualifications", new QualificationInput($"Massage {Guid.NewGuid():N}"), ct);
        spa.Anna = await spa.CreateEmployeeAsync("Anna", [spa.Massage.Id], spa.Location.Id, ct);
        spa.Ben = await spa.CreateEmployeeAsync("Ben", [spa.Massage.Id], spa.Location.Id, ct);
        spa.MassageTreatment = await spa.PostAsync<TreatmentDto>("/api/treatments",
            new TreatmentInput("Massage", 50, 5, 10, 79m, [spa.Massage.Id], [spa.RoomA.Id, spa.RoomB.Id], []), ct);
        spa.Customer = await spa.PostAsync<CustomerDto>("/api/customers",
            new CustomerInput("Eva", $"Muster{Guid.NewGuid():N}", "eva@example.at", "+43 664 000", null, null), ct);
        return spa;
    }

    public Task<LocationDto> CreateLocationAsync(string name, DayOfWeek[] days, CancellationToken ct) =>
        PostAsync<LocationDto>("/api/locations",
            new LocationInput(name, "Europe/Vienna", "EUR", [.. days.Select(d => new OpeningHours(d, new(9, 0), new(20, 0)))]), ct);

    public Task<EmployeeDto> CreateEmployeeAsync(string name, Guid[] qualifications, Guid locationId, CancellationToken ct) =>
        PostAsync<EmployeeDto>("/api/employees",
            new EmployeeInput(name, "#2f6f5e", qualifications, [new WorkingHours(locationId, DayOfWeek.Monday, new(9, 0), new(17, 0))]), ct);

    /// <summary>Uhrzeit in Wien am Testmontag.</summary>
    public static DateTimeOffset At(int hour, int minute = 0, DateOnly? date = null) =>
        new(TimeZoneInfo.ConvertTimeToUtc((date ?? Monday).ToDateTime(new TimeOnly(hour, minute)), Vienna), TimeSpan.Zero);

    public Task<HttpResponseMessage> BookAsync(DateTimeOffset start, CancellationToken ct, Guid? employee = null, Guid? room = null,
        TreatmentDto? treatment = null, Guid? location = null, Guid? customer = null) =>
        Client.PostAsJsonAsync("/api/appointments", new AppointmentRequest(
            location ?? Location.Id, (treatment ?? MassageTreatment).Id, customer ?? Customer.Id, start, employee, room, null, null), Json, ct);

    public async Task<AppointmentDto> BookOkAsync(DateTimeOffset start, CancellationToken ct, Guid? employee = null, Guid? room = null, Guid? customer = null)
    {
        var response = await BookAsync(start, ct, employee, room, customer: customer);
        return await ReadAsync<AppointmentDto>(response, ct);
    }

    /// <summary>
    /// Bucht einen Termin in der Vergangenheit am Endpunkt vorbei über den Buchungsdienst: Die Rezeption darf das nicht mehr (<see cref="Scheduling.PastBookings"/>),
    /// Tests zu Raumpflege und Tagesstart brauchen aber einen Termin, dessen Zeit schon vorbei ist.
    /// </summary>
    public async Task<AppointmentDto> BookInThePastAsync(DateTimeOffset start, CancellationToken ct, Guid? employee = null, Guid? room = null,
        TreatmentDto? treatment = null, Guid? customer = null)
    {
        await using var scope = Fixture.Factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Use(TenantId);
        var outcome = await scope.ServiceProvider.GetRequiredService<BookingService>().BookAsync(
            new AppointmentRequest(Location.Id, (treatment ?? MassageTreatment).Id, customer ?? Customer.Id, start, employee, room, null, null), ct);
        return outcome is BookingOutcome.Success success ? success.Appointment : throw new InvalidOperationException($"Buchung in der Vergangenheit abgelehnt: {outcome}");
    }

    /// <summary>
    /// Setzt den Status am Endpunkt vorbei über den Buchungsdienst: Am Endpunkt gilt die Tagesregel (<see cref="Scheduling.StatusDays"/>), der Testkalender
    /// liegt aber im März 2027. Tests zum Endpunkt selbst buchen relativ zu heute (<see cref="StatusDayTests"/>).
    /// </summary>
    public async Task<AppointmentDto> SetStatusAsync(Guid appointmentId, AppointmentStatus status, CancellationToken ct)
    {
        await using var scope = Fixture.Factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Use(TenantId);
        var outcome = await scope.ServiceProvider.GetRequiredService<BookingService>().ChangeStatusAsync(appointmentId, status, ct);
        return outcome is BookingOutcome.Success success ? success.Appointment : throw new InvalidOperationException($"Statuswechsel abgelehnt: {outcome}");
    }

    public async Task<T> PostAsync<T>(string path, object body, CancellationToken ct) =>
        await ReadAsync<T>(await Client.PostAsJsonAsync(path, body, Json, ct), ct);

    public async Task<T> PutAsync<T>(string path, object body, CancellationToken ct) =>
        await ReadAsync<T>(await Client.PutAsJsonAsync(path, body, Json, ct), ct);

    public async Task<T> PatchAsync<T>(string path, object body, CancellationToken ct) =>
        await ReadAsync<T>(await Client.PatchAsJsonAsync(path, body, Json, ct), ct);

    public async Task<T> GetAsync<T>(string path, CancellationToken ct) =>
        (await Client.GetFromJsonAsync<T>(path, Json, ct))!;

    public static async Task<T> ReadAsync<T>(HttpResponseMessage response, CancellationToken ct)
    {
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(ct)}");
        }

        return (await response.Content.ReadFromJsonAsync<T>(Json, ct))!;
    }
}
