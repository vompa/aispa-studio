using System.Net;
using System.Net.Http.Json;
using AispaStudio.Api.Scheduling;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Tests;

/// <summary>
/// Die Rezeption bucht nur für heute oder später (Kalendertag in der Ortszeit des Standorts): ein früherer Tag ergibt 409 mit
/// <see cref="PastBookings.Message"/>, an heute ist der ganze Tag erlaubt. Die Testumgebung hat keine Test-Uhr; „heute“ ist die echte Uhr,
/// deshalb liegen die Fälle relativ dazu (gestern, vorgestern, 00:30 Ortszeit heute).
/// </summary>
public sealed class PastBookingTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly TimeZoneInfo Vienna = TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna");

    private static DateTimeOffset Utc(int year, int month, int day, int hour, int minute) => new(year, month, day, hour, minute, 0, TimeSpan.Zero);

    private static DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, Vienna).DateTime);

    // ---------- reine Logik ----------

    [Fact]
    public void Gestern_23_30_Ortszeit_liegt_vor_heute_und_heute_00_30_nicht()
    {
        // Winterzeit (UTC+1): 22:30 UTC ist 23:30 Ortszeit, 23:30 UTC ist 00:30 Ortszeit des nächsten Tages.
        var now = Utc(2027, 3, 2, 12, 0);

        Assert.True(PastBookings.IsBeforeToday(Utc(2027, 3, 1, 22, 30), now, Vienna));
        Assert.False(PastBookings.IsBeforeToday(Utc(2027, 3, 1, 23, 30), now, Vienna));
    }

    [Fact]
    public void Heute_gilt_den_ganzen_Tag_auch_vor_der_aktuellen_Uhrzeit_und_spaeter_sowieso()
    {
        var now = Utc(2027, 3, 2, 15, 0);

        Assert.False(PastBookings.IsBeforeToday(Utc(2027, 3, 1, 23, 0), now, Vienna));
        Assert.False(PastBookings.IsBeforeToday(Utc(2027, 3, 2, 9, 0), now, Vienna));
        Assert.False(PastBookings.IsBeforeToday(Utc(2027, 3, 2, 22, 59), now, Vienna));
        Assert.False(PastBookings.IsBeforeToday(Utc(2027, 3, 3, 8, 0), now, Vienna));
    }

    [Fact]
    public void Beginn_in_UTC_am_Vortag_der_lokal_schon_heute_ist_zaehlt_als_heute()
    {
        // 23:30 UTC am 1. März ist in Wien (UTC+1) der 2. März, 00:30.
        var now = Utc(2027, 3, 2, 8, 0);

        Assert.False(PastBookings.IsBeforeToday(Utc(2027, 3, 1, 23, 30), now, Vienna));
        // Umgekehrt: „jetzt“ in UTC noch am Vortag, in Wien schon der nächste Tag; 22:30 UTC am selben UTC-Tag liegt lokal vor heute.
        Assert.True(PastBookings.IsBeforeToday(Utc(2027, 3, 1, 22, 30), Utc(2027, 3, 1, 23, 30), Vienna));
    }

    [Fact]
    public void An_der_Sommerzeit_Grenze_zaehlt_der_Kalendertag_der_Ortszeit()
    {
        // 2027 beginnt die Sommerzeit am 28. März um 01:00 UTC (02:00 Winterzeit wird 03:00). Danach ist Wien UTC+2.
        var now = Utc(2027, 3, 28, 12, 0);

        // 28.03. 00:30 Ortszeit = 27.03. 23:30 UTC: heute.
        Assert.False(PastBookings.IsBeforeToday(Utc(2027, 3, 27, 23, 30), now, Vienna));
        // 27.03. 23:30 Ortszeit = 22:30 UTC: gestern.
        Assert.True(PastBookings.IsBeforeToday(Utc(2027, 3, 27, 22, 30), now, Vienna));

        // Nach der Umstellung (UTC+2): 22:30 UTC am 28.03. ist schon der 29.03. um 00:30, also nach heute.
        Assert.False(PastBookings.IsBeforeToday(Utc(2027, 3, 28, 22, 30), now, Vienna));
        // „Jetzt“ am 29.03. um 00:30 Ortszeit (22:30 UTC): 28.03. 21:30 UTC (23:30 Ortszeit) ist gestern.
        Assert.True(PastBookings.IsBeforeToday(Utc(2027, 3, 28, 21, 30), Utc(2027, 3, 28, 22, 30), Vienna));
    }

    [Fact]
    public void Die_Ablehnungstexte_nennen_den_Weg_ohne_Bitte()
    {
        Assert.EndsWith("Möglich ist heute oder ein späterer Tag.", PastBookings.Message, StringComparison.Ordinal);
        Assert.EndsWith("Möglich ist heute oder ein späterer Tag.", PastBookings.MoveMessage, StringComparison.Ordinal);
        Assert.DoesNotContain("Bitte", PastBookings.Message + PastBookings.MoveMessage, StringComparison.Ordinal);
    }

    // ---------- Endpunkte ----------

    private static async Task AssertRejectedAsync(HttpResponseMessage response, string message = PastBookings.Message)
    {
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var rejected = (await response.Content.ReadFromJsonAsync<BookingRejected>(TestSpa.Json, Ct))!;
        Assert.Equal(message, rejected.Message);
        Assert.Empty(rejected.Conflicts);
    }

    // ---------- Verschieben (L2) ----------

    [Fact]
    public async Task Verschieben_auf_gestern_wird_mit_409_abgelehnt_auf_heute_und_uebermorgen_geht_es()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var appointment = await spa.BookOkAsync(LocalToday(10, 0, 1), Ct, spa.Anna.Id, spa.RoomA.Id);

        var yesterday = await spa.Client.PostAsJsonAsync($"/api/appointments/{appointment.Id}/move",
            new MoveAppointmentRequest(LocalToday(10, 0, -1), null, null, null), TestSpa.Json, Ct);
        var today = await spa.Client.PostAsJsonAsync($"/api/appointments/{appointment.Id}/move",
            new MoveAppointmentRequest(LocalToday(10, 0), null, null, null), TestSpa.Json, Ct);
        var later = await spa.Client.PostAsJsonAsync($"/api/appointments/{appointment.Id}/move",
            new MoveAppointmentRequest(LocalToday(10, 0, 2), null, null, null), TestSpa.Json, Ct);

        await AssertRejectedAsync(yesterday, PastBookings.MoveMessage);
        Assert.Equal(HttpStatusCode.OK, today.StatusCode);
        Assert.Equal(HttpStatusCode.OK, later.StatusCode);
        var unchanged = await spa.GetAsync<CalendarDay>($"/api/locations/{spa.Location.Id}/calendar?date={Today.AddDays(-1):yyyy-MM-dd}", Ct);
        Assert.Empty(unchanged.Appointments);
    }

    [Fact]
    public async Task Ganze_Buchung_auf_gestern_verschieben_wird_mit_409_abgelehnt()
    {
        // Eine gemeinsame Buchung braucht zwei Personen im Dienst; der Standort „Rund um die Uhr“ hat sie an jedem Wochentag.
        var (spa, location, treatment, _) = await OpenAllDayAsync();
        var partner = await spa.PostAsync<CustomerDto>("/api/customers", new CustomerInput("Paul", $"Gast{Guid.NewGuid():N}", null, null, null, null), Ct);
        var group = await spa.PostAsync<GroupBookingResult>("/api/appointment-groups", new GroupBookingRequest(location.Id, LocalToday(10, 0, 1),
            [new GroupBookingItem(treatment.Id, spa.Customer.Id, 0), new GroupBookingItem(treatment.Id, partner.Id, 0)]), Ct);

        var response = await spa.Client.PostAsJsonAsync($"/api/locations/{location.Id}/appointment-groups/{group.GroupId}/move",
            new MoveGroupRequest(LocalToday(10, 0, -1)), TestSpa.Json, Ct);

        await AssertRejectedAsync(response, PastBookings.MoveMessage);
    }

    // ---------- Stornierung zurücknehmen (Verlauf, Aufgabe E) ----------

    [Fact]
    public async Task Ruecknahme_eines_gestrigen_Termins_ohne_neue_Zeit_wird_mit_409_abgelehnt_mit_Zeit_morgen_geht_sie()
    {
        var (spa, location, treatment, _) = await OpenAllDayAsync();
        var appointment = await spa.BookInThePastAsync(LocalToday(10, 0, -1), Ct, treatment: treatment, location: location.Id);
        _ = await spa.PostAsync<AppointmentDto>($"/api/appointments/{appointment.Id}/cancel", new CancelAppointmentRequest("Krank"), Ct);

        var info = await spa.GetAsync<AppointmentRestoration>($"/api/appointments/{appointment.Id}/restoration", Ct);
        var samePlace = await spa.Client.PostAsJsonAsync($"/api/appointments/{appointment.Id}/restore", new RestoreAppointmentRequest(), TestSpa.Json, Ct);
        var tomorrow = await spa.Client.PostAsJsonAsync($"/api/appointments/{appointment.Id}/restore",
            new RestoreAppointmentRequest(LocalToday(10, 0, 1)), TestSpa.Json, Ct);

        Assert.True(info.InPast);
        Assert.Null(info.Conflict);
        await AssertRejectedAsync(samePlace, PastBookings.RestoreMessage);
        Assert.Equal(HttpStatusCode.OK, tomorrow.StatusCode);
        var restored = await TestSpa.ReadAsync<AppointmentDto>(tomorrow, Ct);
        Assert.Equal((AppointmentStatus.Booked, LocalToday(10, 0, 1)), (restored.Status, restored.Start));
    }

    [Fact]
    public async Task Ruecknahme_auf_gestern_mit_neuer_Zeit_wird_wie_Verschieben_abgelehnt()
    {
        var (spa, location, treatment, _) = await OpenAllDayAsync();
        var appointment = await TestSpa.ReadAsync<AppointmentDto>(await spa.BookAsync(LocalToday(10, 0, 1), Ct, treatment: treatment, location: location.Id), Ct);
        _ = await spa.PostAsync<AppointmentDto>($"/api/appointments/{appointment.Id}/cancel", new CancelAppointmentRequest("Krank"), Ct);

        var response = await spa.Client.PostAsJsonAsync($"/api/appointments/{appointment.Id}/restore",
            new RestoreAppointmentRequest(LocalToday(10, 0, -1)), TestSpa.Json, Ct);

        await AssertRejectedAsync(response, PastBookings.MoveMessage);
    }

    [Fact]
    public async Task Restoration_meldet_die_inzwischen_belegte_Zeit_ohne_zu_speichern()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var cancelled = await spa.BookOkAsync(TestSpa.At(10), Ct, spa.Anna.Id, spa.RoomA.Id);
        _ = await spa.PostAsync<AppointmentDto>($"/api/appointments/{cancelled.Id}/cancel", new CancelAppointmentRequest("Krank"), Ct);
        var other = await spa.BookOkAsync(TestSpa.At(10), Ct, spa.Anna.Id, spa.RoomA.Id);

        var info = await spa.GetAsync<AppointmentRestoration>($"/api/appointments/{cancelled.Id}/restoration", Ct);

        Assert.False(info.InPast);
        Assert.NotNull(info.Conflict);
        Assert.Contains(info.Conflict.Conflicts, c => c.Kind == ResourceKind.Employee && c.AppointmentId == other.Id);
        Assert.Equal(AppointmentStatus.Cancelled, (await spa.GetAsync<AppointmentDto>($"/api/appointments/{cancelled.Id}", Ct)).Status);
        var free = await spa.BookOkAsync(TestSpa.At(14), Ct, spa.Ben.Id, spa.RoomB.Id);
        _ = await spa.PostAsync<AppointmentDto>($"/api/appointments/{free.Id}/cancel", new CancelAppointmentRequest("Krank"), Ct);
        Assert.Null((await spa.GetAsync<AppointmentRestoration>($"/api/appointments/{free.Id}/restoration", Ct)).Conflict);
    }

    private static async Task AssertNotPastRejectionAsync(HttpResponseMessage response)
    {
        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            var rejected = await response.Content.ReadFromJsonAsync<BookingRejected>(TestSpa.Json, Ct);
            Assert.NotEqual(PastBookings.Message, rejected?.Message);
        }
    }

    private static DateTimeOffset LocalToday(int hour, int minute, int addDays = 0) =>
        TestSpa.At(hour, minute, Today.AddDays(addDays));

    [Fact]
    public async Task Termin_gestern_wird_mit_409_und_der_Meldung_abgelehnt_und_nichts_gespeichert()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);

        var yesterday = await spa.BookAsync(LocalToday(10, 0, -1), Ct, spa.Anna.Id, spa.RoomA.Id);
        var longAgo = await spa.BookAsync(LocalToday(10, 0, -30), Ct, spa.Anna.Id, spa.RoomA.Id);
        var late = await spa.BookAsync(LocalToday(23, 30, -1), Ct, spa.Anna.Id, spa.RoomA.Id);

        await AssertRejectedAsync(yesterday);
        await AssertRejectedAsync(longAgo);
        await AssertRejectedAsync(late);
        var day = await spa.GetAsync<CalendarDay>($"/api/locations/{spa.Location.Id}/calendar?date={Today.AddDays(-1):yyyy-MM-dd}", Ct);
        Assert.Empty(day.Appointments);
    }

    [Fact]
    public async Task Gruppenbuchung_gestern_wird_mit_409_und_der_Meldung_abgelehnt()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var partner = await spa.PostAsync<CustomerDto>("/api/customers", new CustomerInput("Paul", $"Gast{Guid.NewGuid():N}", null, null, null, null), Ct);

        var response = await spa.Client.PostAsJsonAsync("/api/appointment-groups", new GroupBookingRequest(spa.Location.Id, LocalToday(10, 0, -1),
            [new GroupBookingItem(spa.MassageTreatment.Id, spa.Customer.Id, 0), new GroupBookingItem(spa.MassageTreatment.Id, partner.Id, 0)]), TestSpa.Json, Ct);

        await AssertRejectedAsync(response);
    }

    [Fact]
    public async Task Eintritt_in_einen_Bereich_gestern_wird_mit_409_und_der_Meldung_abgelehnt()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var sauna = await spa.PostAsync<RoomDto>($"/api/locations/{spa.Location.Id}/rooms", new RoomInput("Sauna", 0, 5, IsArea: true), Ct);

        var response = await spa.Client.PostAsJsonAsync("/api/area-visits",
            new AreaVisitRequest(spa.Location.Id, sauna.Id, spa.Customer.Id, LocalToday(10, 0, -1), LocalToday(12, 0, -1)), TestSpa.Json, Ct);

        await AssertRejectedAsync(response);
    }

    [Fact]
    public async Task Unbekannter_Standort_ist_keine_Vergangenheitsablehnung_die_Buchung_meldet_ihn_selbst()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);

        var response = await spa.BookAsync(LocalToday(10, 0, -1), Ct, location: Guid.NewGuid());

        Assert.NotEqual(HttpStatusCode.Created, response.StatusCode);
        await AssertNotPastRejectionAsync(response);
    }

    /// <summary>Ein Standort, der jeden Tag von 00:00 bis 23:59 offen ist, mit zwei Personen und zwei Räumen, die heute arbeiten.</summary>
    private async Task<(TestSpa Spa, LocationDto Location, TreatmentDto Treatment, RoomDto Sauna)> OpenAllDayAsync()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var days = Enum.GetValues<DayOfWeek>();
        var location = await spa.PostAsync<LocationDto>("/api/locations",
            new LocationInput("Rund um die Uhr", "Europe/Vienna", "EUR", [.. days.Select(d => new OpeningHours(d, new(0, 0), new(23, 59)))]), Ct);
        var room1 = await spa.PostAsync<RoomDto>($"/api/locations/{location.Id}/rooms", new RoomInput("Raum 1", 0), Ct);
        var room2 = await spa.PostAsync<RoomDto>($"/api/locations/{location.Id}/rooms", new RoomInput("Raum 2", 0), Ct);
        var sauna = await spa.PostAsync<RoomDto>($"/api/locations/{location.Id}/rooms", new RoomInput("Sauna", 0, 5, IsArea: true), Ct);
        foreach (var name in new[] { "Cora", "Dirk" })
        {
            _ = await spa.PostAsync<EmployeeDto>("/api/employees", new EmployeeInput(name, "#2f6f5e", [spa.Massage.Id],
                [.. days.Select(d => new WorkingHours(location.Id, d, new(0, 0), new(23, 59)))]), Ct);
        }

        var treatment = await spa.PostAsync<TreatmentDto>("/api/treatments",
            new TreatmentInput("Nachttermin", 50, 0, 0, 59m, [spa.Massage.Id], [room1.Id, room2.Id], []), Ct);
        return (spa, location, treatment, sauna);
    }

    [Fact]
    public async Task Heute_um_00_30_ist_erlaubt_auch_wenn_es_schon_vorbei_ist_gestern_derselben_Zeit_nicht()
    {
        var (spa, location, treatment, _) = await OpenAllDayAsync();

        var yesterday = await spa.Client.PostAsJsonAsync("/api/appointments",
            new AppointmentRequest(location.Id, treatment.Id, spa.Customer.Id, LocalToday(0, 30, -1), null, null, null, null), TestSpa.Json, Ct);
        var today = await spa.Client.PostAsJsonAsync("/api/appointments",
            new AppointmentRequest(location.Id, treatment.Id, spa.Customer.Id, LocalToday(0, 30), null, null, null, null), TestSpa.Json, Ct);

        await AssertRejectedAsync(yesterday);
        Assert.Equal(HttpStatusCode.Created, today.StatusCode);
    }

    [Fact]
    public async Task Gruppenbuchung_und_Bereichseintritt_heute_um_00_30_sind_erlaubt()
    {
        var (spa, location, treatment, sauna) = await OpenAllDayAsync();
        var partner = await spa.PostAsync<CustomerDto>("/api/customers", new CustomerInput("Paul", $"Gast{Guid.NewGuid():N}", null, null, null, null), Ct);

        var group = await spa.Client.PostAsJsonAsync("/api/appointment-groups", new GroupBookingRequest(location.Id, LocalToday(0, 30),
            [new GroupBookingItem(treatment.Id, spa.Customer.Id, 0), new GroupBookingItem(treatment.Id, partner.Id, 0)]), TestSpa.Json, Ct);
        var visit = await spa.Client.PostAsJsonAsync("/api/area-visits",
            new AreaVisitRequest(location.Id, sauna.Id, spa.Customer.Id, LocalToday(0, 30), LocalToday(1, 30)), TestSpa.Json, Ct);

        Assert.Equal(HttpStatusCode.Created, group.StatusCode);
        Assert.Equal(HttpStatusCode.Created, visit.StatusCode);
    }
}
