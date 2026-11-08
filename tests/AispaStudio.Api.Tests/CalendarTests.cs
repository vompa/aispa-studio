using System.Net;
using System.Net.Http.Json;
using AispaStudio.Contracts;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;

namespace AispaStudio.Api.Tests;

public sealed class CalendarTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Buchung_waehlt_Person_und_Raum_und_sperrt_Vor_und_Nachbereitung()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);

        var response = await spa.BookAsync(TestSpa.At(10), Ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var appointment = await TestSpa.ReadAsync<AppointmentDto>(response, Ct);
        Assert.Equal(TestSpa.At(10), appointment.Start);
        Assert.Equal(TestSpa.At(10, 50), appointment.End);
        Assert.Equal(TestSpa.At(9, 55), appointment.BlockedFrom);
        Assert.Equal(TestSpa.At(11), appointment.BlockedUntil);
        Assert.Contains(appointment.EmployeeId, new[] { spa.Anna.Id, spa.Ben.Id });
        Assert.Contains(appointment.RoomId, new[] { spa.RoomA.Id, spa.RoomB.Id });
        Assert.Equal(79m, appointment.Price);
        Assert.Equal(AppointmentStatus.Booked, appointment.Status);
        Assert.StartsWith("Eva ", appointment.CustomerName, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Wiederholte_Buchung_mit_derselben_Id_bucht_nicht_doppelt()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var request = new AppointmentRequest(spa.Location.Id, spa.MassageTreatment.Id, spa.Customer.Id, TestSpa.At(10), spa.Anna.Id, null, null,
            "offline vorgemerkt", Guid.NewGuid());

        var first = await TestSpa.ReadAsync<AppointmentDto>(await spa.Client.PostAsJsonAsync("/api/appointments", request, TestSpa.Json, Ct), Ct);
        var again = await TestSpa.ReadAsync<AppointmentDto>(await spa.Client.PostAsJsonAsync("/api/appointments", request, TestSpa.Json, Ct), Ct);
        var otherCustomer = await spa.Client.PostAsJsonAsync("/api/appointments", request with { CustomerId = Guid.NewGuid() }, TestSpa.Json, Ct);

        Assert.Equal(request.ClientId, first.Id);
        Assert.Equal(first.Id, again.Id);
        Assert.Equal(first.Start, again.Start);
        Assert.NotEqual(HttpStatusCode.Created, otherCustomer.StatusCode);
        var day = await spa.GetAsync<CalendarDay>($"/api/locations/{spa.Location.Id}/calendar?date={TestSpa.Monday:yyyy-MM-dd}", Ct);
        Assert.Single(day.Appointments, a => a.Id == first.Id);
        Assert.Single(day.Appointments);
    }

    [Fact]
    public async Task Ohne_Verbindung_angelegte_Kundin_wird_mit_ihrer_Id_einmal_angelegt_und_ist_sofort_buchbar()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var other = await TestSpa.CreateAsync(fixture, Ct);
        var input = new CustomerInput("Olga", "Offline", null, "+43 660 123", null, null, Guid.NewGuid());

        var first = await TestSpa.ReadAsync<CustomerDto>(await spa.Client.PostAsJsonAsync("/api/customers", input, TestSpa.Json, Ct), Ct);
        var again = await TestSpa.ReadAsync<CustomerDto>(await spa.Client.PostAsJsonAsync("/api/customers", input, TestSpa.Json, Ct), Ct);
        var foreign = await other.Client.PostAsJsonAsync("/api/customers", input, TestSpa.Json, Ct);
        var booked = await spa.Client.PostAsJsonAsync("/api/appointments",
            new AppointmentRequest(spa.Location.Id, spa.MassageTreatment.Id, input.ClientId!.Value, TestSpa.At(10), null, null, null, null, Guid.NewGuid()),
            TestSpa.Json, Ct);

        Assert.Equal(input.ClientId, first.Id);
        Assert.Equal(first, again);
        Assert.Equal(HttpStatusCode.BadRequest, foreign.StatusCode);
        Assert.Equal(HttpStatusCode.Created, booked.StatusCode);
        Assert.Single(await spa.GetAsync<List<CustomerDto>>("/api/customers?search=Offline", Ct));
    }

    [Fact]
    public async Task Fuer_eine_neue_Kundin_genuegt_ein_Name()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);

        var lastOnly = await TestSpa.ReadAsync<CustomerDto>(await spa.Client.PostAsJsonAsync("/api/customers",
            new CustomerInput("", " Huber ", null, null, null, null), TestSpa.Json, Ct), Ct);
        var firstOnly = await TestSpa.ReadAsync<CustomerDto>(await spa.Client.PostAsJsonAsync("/api/customers",
            new CustomerInput("Mia", "", null, null, null, null), TestSpa.Json, Ct), Ct);
        var nameless = await spa.Client.PostAsJsonAsync("/api/customers", new CustomerInput(" ", "", null, "+43 660 1", null, null), TestSpa.Json, Ct);

        Assert.Equal(("", "Huber"), (lastOnly.FirstName, lastOnly.LastName));
        Assert.Equal(("Mia", ""), (firstOnly.FirstName, firstOnly.LastName));
        Assert.Equal(HttpStatusCode.BadRequest, nameless.StatusCode);
        Assert.Single(await spa.GetAsync<List<CustomerDto>>("/api/customers?search=Huber", Ct));
    }

    [Fact]
    public async Task Stammdaten_der_Kundin_lassen_sich_nachtraeglich_aendern_und_leeren()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var other = await TestSpa.CreateAsync(fixture, Ct);
        var path = $"/api/customers/{spa.Customer.Id}";

        var changed = await TestSpa.ReadAsync<CustomerDto>(await spa.Client.PutAsJsonAsync(path,
            new CustomerInput(" Anna ", "Kern-Huber", "anna@example.at", " +43 660 123 ", new DateOnly(1985, 3, 14), "Mag Lavendel"), TestSpa.Json, Ct), Ct);
        var cleared = await TestSpa.ReadAsync<CustomerDto>(await spa.Client.PutAsJsonAsync(path,
            new CustomerInput("Anna", "Kern-Huber", " ", null, null, ""), TestSpa.Json, Ct), Ct);
        var nameless = await spa.Client.PutAsJsonAsync(path, new CustomerInput("", " ", null, null, null, null), TestSpa.Json, Ct);
        var badMail = await spa.Client.PutAsJsonAsync(path, new CustomerInput("Anna", "", "anna.example.at", null, null, null), TestSpa.Json, Ct);
        var foreign = await other.Client.PutAsJsonAsync(path, new CustomerInput("Fremd", "", null, null, null, null), TestSpa.Json, Ct);

        Assert.Equal(("Anna", "Kern-Huber", "anna@example.at", "+43 660 123", new DateOnly(1985, 3, 14), "Mag Lavendel"),
            (changed.FirstName, changed.LastName, changed.Email, changed.Phone, changed.BirthDate, changed.Note));
        Assert.Equal((null, null, null, null), (cleared.Email, cleared.Phone, cleared.BirthDate, cleared.Note));
        Assert.Equal(HttpStatusCode.BadRequest, nameless.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, badMail.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
        Assert.Equal("Kern-Huber", (await spa.GetAsync<CustomerDto>(path, Ct)).LastName);
    }

    [Fact]
    public async Task Rechnungsempfaenger_der_Kundin_wird_gespeichert_geprueft_und_mit_leerem_Inhalt_geloescht()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var other = await TestSpa.CreateAsync(fixture, Ct);
        var path = $"/api/customers/{spa.Customer.Id}/billing";

        var saved = await TestSpa.ReadAsync<CustomerDto>(
            await spa.Client.PutAsJsonAsync(path, new CustomerBilling(" Huber Consulting GmbH ", "Ringstraße 1\n1010 Wien", "atu12345678"), TestSpa.Json, Ct), Ct);
        var loaded = await spa.GetAsync<CustomerDto>($"/api/customers/{spa.Customer.Id}", Ct);
        var noAddress = await spa.Client.PutAsJsonAsync(path, new CustomerBilling("Huber Consulting GmbH", " "), TestSpa.Json, Ct);
        var foreign = await other.Client.PutAsJsonAsync(path, new CustomerBilling("Fremd", "Irgendwo"), TestSpa.Json, Ct);
        var cleared = await TestSpa.ReadAsync<CustomerDto>(await spa.Client.PutAsJsonAsync(path, new CustomerBilling("", ""), TestSpa.Json, Ct), Ct);

        Assert.Equal(new CustomerBilling("Huber Consulting GmbH", "Ringstraße 1\n1010 Wien", "ATU12345678"), saved.Billing);
        Assert.Equal(saved, loaded);
        Assert.Equal(HttpStatusCode.BadRequest, noAddress.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
        Assert.Null(cleared.Billing);
    }

    [Fact]
    public async Task Dieselbe_Person_kann_nicht_doppelt_gebucht_werden()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await spa.BookOkAsync(TestSpa.At(10), Ct, employee: spa.Anna.Id);

        var response = await spa.BookAsync(TestSpa.At(10, 30), Ct, employee: spa.Anna.Id);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var rejected = await response.Content.ReadFromJsonAsync<BookingRejected>(TestSpa.Json, Ct);
        var conflict = Assert.Single(rejected!.Conflicts);
        Assert.Equal(ResourceKind.Employee, conflict.Kind);
        Assert.Equal(spa.Anna.Id, conflict.ResourceId);
    }

    [Fact]
    public async Task Raumpflege_haelt_den_Raum_frei_und_danach_darf_direkt_gebucht_werden()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        // Raum A: 9:55–11:00 Termin mit Vor-/Nachbereitung, dazu 15 Minuten Raumpflege bis 11:15.
        await spa.BookOkAsync(TestSpa.At(10), Ct, employee: spa.Anna.Id, room: spa.RoomA.Id);

        var tooEarly = await spa.BookAsync(TestSpa.At(11, 15), Ct, employee: spa.Ben.Id, room: spa.RoomA.Id);
        var adjacent = await spa.BookAsync(TestSpa.At(11, 20), Ct, employee: spa.Ben.Id, room: spa.RoomA.Id);

        Assert.Equal(HttpStatusCode.Conflict, tooEarly.StatusCode);
        Assert.Equal(HttpStatusCode.Created, adjacent.StatusCode);
    }

    [Fact]
    public async Task Ohne_Wunsch_werden_freie_Personen_und_Raeume_verteilt_bis_alles_belegt_ist()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);

        var first = await spa.BookOkAsync(TestSpa.At(10), Ct);
        var second = await spa.BookOkAsync(TestSpa.At(10), Ct);
        var third = await spa.BookAsync(TestSpa.At(10), Ct);

        Assert.NotEqual(first.EmployeeId, second.EmployeeId);
        Assert.NotEqual(first.RoomId, second.RoomId);
        Assert.Equal(HttpStatusCode.Conflict, third.StatusCode);
    }

    [Fact]
    public async Task Gleichzeitige_Buchungen_derselben_Zeit_ergeben_genau_einen_Termin()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);

        var responses = await Task.WhenAll(Enumerable.Range(0, 10)
            .Select(_ => spa.BookAsync(TestSpa.At(14), Ct, employee: spa.Anna.Id)));

        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.Created));
        Assert.Equal(9, responses.Count(r => r.StatusCode == HttpStatusCode.Conflict));
    }

    [Fact]
    public async Task Ausserhalb_von_Oeffnungszeit_und_Dienst_wird_abgelehnt_ausser_an_einem_Schliesstag()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);

        var closed = await spa.BookAsync(TestSpa.At(8), Ct);
        var afterShift = await spa.BookAsync(TestSpa.At(16, 30), Ct, employee: spa.Anna.Id);
        var sunday = await spa.BookAsync(TestSpa.At(10, date: TestSpa.Monday.AddDays(-1)), Ct);

        Assert.Equal(HttpStatusCode.Conflict, closed.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, afterShift.StatusCode);
        // Der Sonntag hat keine Öffnungszeit: ein Schließtag, an dem die Rezeption einträgt (ADR 0073, Nachtrag R03).
        Assert.Equal(HttpStatusCode.Created, sunday.StatusCode);
        var rejected = await afterShift.Content.ReadFromJsonAsync<BookingRejected>(TestSpa.Json, Ct);
        Assert.Contains("nicht im Dienst", Assert.Single(rejected!.Conflicts).Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Ohne_passende_Qualifikation_wird_abgelehnt()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var hotStone = await spa.PostAsync<QualificationDto>("/api/qualifications", new QualificationInput($"Hot Stone {Guid.NewGuid():N}"), Ct);
        var treatment = await spa.PostAsync<TreatmentDto>("/api/treatments",
            new TreatmentInput("Hot Stone", 75, 10, 10, 119m, [hotStone.Id], [spa.RoomA.Id], []), Ct);

        var response = await spa.BookAsync(TestSpa.At(10), Ct, treatment: treatment);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Ein_Geraet_wird_wie_Person_und_Raum_gesperrt()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var heater = await spa.PostAsync<EquipmentDto>($"/api/locations/{spa.Location.Id}/equipment", new EquipmentInput("Steinwärmer"), Ct);
        var treatment = await spa.PostAsync<TreatmentDto>("/api/treatments",
            new TreatmentInput("Hot Stone", 60, 0, 0, 99m, [spa.Massage.Id], [spa.RoomA.Id, spa.RoomB.Id], [heater.Id]), Ct);

        var first = await TestSpa.ReadAsync<AppointmentDto>(await spa.BookAsync(TestSpa.At(10), Ct, treatment: treatment), Ct);
        var second = await spa.BookAsync(TestSpa.At(10, 30), Ct, treatment: treatment);

        Assert.Equal(heater.Id, first.EquipmentId);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        var rejected = await second.Content.ReadFromJsonAsync<BookingRejected>(TestSpa.Json, Ct);
        Assert.Equal(ResourceKind.Equipment, Assert.Single(rejected!.Conflicts).Kind);
    }

    [Fact]
    public async Task Storno_gibt_die_Zeit_sofort_wieder_frei()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var appointment = await spa.BookOkAsync(TestSpa.At(10), Ct, employee: spa.Anna.Id, room: spa.RoomA.Id);

        var cancelled = await spa.PostAsync<AppointmentDto>($"/api/appointments/{appointment.Id}/cancel", new CancelAppointmentRequest("krank"), Ct);
        var again = await spa.BookAsync(TestSpa.At(10), Ct, employee: spa.Anna.Id, room: spa.RoomA.Id);

        Assert.Equal(AppointmentStatus.Cancelled, cancelled.Status);
        Assert.Equal(HttpStatusCode.Created, again.StatusCode);
    }

    [Fact]
    public async Task Verschieben_behaelt_die_Person_und_gibt_die_alte_Zeit_frei()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var appointment = await spa.BookOkAsync(TestSpa.At(10), Ct, employee: spa.Anna.Id);

        var moved = await spa.PostAsync<AppointmentDto>($"/api/appointments/{appointment.Id}/move",
            new MoveAppointmentRequest(TestSpa.At(14), null, null, null), Ct);
        var oldSlot = await spa.BookAsync(TestSpa.At(10), Ct, employee: spa.Anna.Id);

        Assert.Equal(TestSpa.At(14), moved.Start);
        Assert.Equal(spa.Anna.Id, moved.EmployeeId);
        Assert.True(moved.UpdatedAt > appointment.UpdatedAt);
        Assert.Equal(HttpStatusCode.Created, oldSlot.StatusCode);
    }

    [Fact]
    public async Task Verschieben_auf_belegte_Person_wird_abgelehnt()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await spa.BookOkAsync(TestSpa.At(13), Ct, employee: spa.Ben.Id);
        var appointment = await spa.BookOkAsync(TestSpa.At(10), Ct, employee: spa.Anna.Id);

        var response = await spa.Client.PostAsJsonAsync($"/api/appointments/{appointment.Id}/move",
            new MoveAppointmentRequest(TestSpa.At(13), spa.Ben.Id, null, null), TestSpa.Json, Ct);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Freie_Zeiten_beruecksichtigen_Termine_und_Sperrzeiten()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await spa.BookOkAsync(TestSpa.At(10), Ct, employee: spa.Anna.Id);
        var block = await spa.Client.PostAsJsonAsync("/api/blocks",
            new BlockInput(ResourceKind.Employee, spa.Anna.Id, TestSpa.At(12), TestSpa.At(13), "Schulung"), TestSpa.Json, Ct);

        var slots = await spa.GetAsync<List<AvailableSlot>>(
            $"/api/locations/{spa.Location.Id}/availability?treatmentId={spa.MassageTreatment.Id}&date={TestSpa.Monday:yyyy-MM-dd}", Ct);

        Assert.Equal(HttpStatusCode.Created, block.StatusCode);
        var anna = slots.Where(s => s.EmployeeId == spa.Anna.Id).Select(s => s.Start).ToHashSet();
        var ben = slots.Where(s => s.EmployeeId == spa.Ben.Id).Select(s => s.Start).ToHashSet();
        Assert.DoesNotContain(TestSpa.At(10), anna);
        Assert.DoesNotContain(TestSpa.At(12), anna);
        Assert.Contains(TestSpa.At(14), anna);
        Assert.Contains(TestSpa.At(10), ben);
        // Dienst 9–17 Uhr: 9:00 geht nicht (Vorbereitung ab 8:55), 16:00 gerade noch (Nachbereitung bis 17:00).
        Assert.DoesNotContain(TestSpa.At(9), ben);
        Assert.Contains(TestSpa.At(9, 15), ben);
        Assert.Contains(TestSpa.At(16), ben);
        Assert.DoesNotContain(TestSpa.At(16, 15), ben);
    }

    [Fact]
    public async Task Sperrzeit_ueber_bestehendem_Termin_wird_abgelehnt()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await spa.BookOkAsync(TestSpa.At(10), Ct, room: spa.RoomB.Id);

        var response = await spa.Client.PostAsJsonAsync("/api/blocks",
            new BlockInput(ResourceKind.Room, spa.RoomB.Id, TestSpa.At(9), TestSpa.At(18), "Wartung"), TestSpa.Json, Ct);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    /// <summary>Slice 71 (Ä89): Die Pause lässt sich im Kalender am Rand ziehen; der Server prüft wie beim Anlegen, nur nicht gegen sich selbst.</summary>
    [Fact]
    public async Task Sperrzeit_aendern_verschiebt_Beginn_und_Ende_und_lehnt_Ueberschneidung_ab()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await spa.BookOkAsync(TestSpa.At(14), Ct, employee: spa.Anna.Id);
        var pause = await spa.PostAsync<BlockDto>("/api/blocks", new BlockInput(ResourceKind.Employee, spa.Anna.Id, TestSpa.At(12), TestSpa.At(12, 30), "Pause"), Ct);

        // Früher beginnen und länger dauern: überschneidet nur sich selbst.
        var changed = await spa.Client.PatchAsJsonAsync($"/api/blocks/{pause.Id}", new BlockChange(TestSpa.At(11, 45), TestSpa.At(13)), TestSpa.Json, Ct);
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        var block = await TestSpa.ReadAsync<BlockDto>(changed, Ct);
        Assert.Equal(pause.Id, block.Id);
        Assert.Equal(TestSpa.At(11, 45), block.From);
        Assert.Equal(TestSpa.At(13), block.Until);
        Assert.Equal("Pause", block.Reason);

        // In den Termin um 14 Uhr (belegt ab 13:55): abgelehnt, die Pause bleibt.
        var overlap = await spa.Client.PatchAsJsonAsync($"/api/blocks/{pause.Id}", new BlockChange(TestSpa.At(11, 45), TestSpa.At(14, 15)), TestSpa.Json, Ct);
        Assert.Equal(HttpStatusCode.Conflict, overlap.StatusCode);
        var rejected = await overlap.Content.ReadFromJsonAsync<BookingRejected>(TestSpa.Json, Ct);
        Assert.Contains(rejected!.Conflicts, c => c.AppointmentId is not null);

        var backwards = await spa.Client.PatchAsJsonAsync($"/api/blocks/{pause.Id}", new BlockChange(TestSpa.At(13), TestSpa.At(12)), TestSpa.Json, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, backwards.StatusCode);
        var missing = await spa.Client.PatchAsJsonAsync($"/api/blocks/{Guid.NewGuid()}", new BlockChange(TestSpa.At(12), TestSpa.At(13)), TestSpa.Json, Ct);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);

        var day = await spa.GetAsync<CalendarDay>($"/api/locations/{spa.Location.Id}/calendar?date={TestSpa.Monday:yyyy-MM-dd}", Ct);
        var stored = Assert.Single(day.Blocks, b => b.Id == pause.Id);
        Assert.Equal(TestSpa.At(11, 45), stored.From);
        Assert.Equal(TestSpa.At(13), stored.Until);
    }

    [Fact]
    public async Task Kundenstamm_gilt_fuer_alle_Standorte()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var graz = await spa.CreateLocationAsync("Graz", [DayOfWeek.Monday], Ct);
        var room = await spa.PostAsync<RoomDto>($"/api/locations/{graz.Id}/rooms", new RoomInput("Raum Graz", 0), Ct);
        var clara = await spa.CreateEmployeeAsync("Clara", [spa.Massage.Id], graz.Id, Ct);
        var treatment = await spa.PostAsync<TreatmentDto>("/api/treatments",
            new TreatmentInput("Massage Graz", 50, 0, 0, 79m, [spa.Massage.Id], [room.Id], []), Ct);
        await spa.BookOkAsync(TestSpa.At(10), Ct);

        var inGraz = await spa.BookAsync(TestSpa.At(15), Ct, treatment: treatment, location: graz.Id);
        var found = await spa.GetAsync<List<CustomerDto>>($"/api/customers?search={Uri.EscapeDataString("eva " + spa.Customer.LastName)}", Ct);
        var history = await spa.GetAsync<List<AppointmentDto>>($"/api/customers/{spa.Customer.Id}/appointments", Ct);

        Assert.Equal(HttpStatusCode.Created, inGraz.StatusCode);
        Assert.Equal(clara.Id, (await TestSpa.ReadAsync<AppointmentDto>(inGraz, Ct)).EmployeeId);
        Assert.Equal(spa.Customer.Id, Assert.Single(found).Id);
        Assert.Equal([graz.Id, spa.Location.Id], history.Select(a => a.LocationId));
    }

    [Fact]
    public async Task Kalendertag_zeigt_Oeffnungszeit_Dienste_und_Termine()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var appointment = await spa.BookOkAsync(TestSpa.At(10), Ct);

        var day = await spa.GetAsync<CalendarDay>($"/api/locations/{spa.Location.Id}/calendar?date={TestSpa.Monday:yyyy-MM-dd}", Ct);

        Assert.Equal("Europe/Vienna", day.TimeZone);
        Assert.Equal(new TimeRange(TestSpa.At(9), TestSpa.At(20)), Assert.Single(day.Opening));
        Assert.Equal(2, day.Shifts.Count);
        Assert.Equal(appointment.Id, Assert.Single(day.Appointments).Id);
    }

    [Fact]
    public async Task Kalendertag_nennt_Besuche_Nicht_Erscheinen_und_Hinweis_der_Gaeste()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        foreach (var (weeksAgo, status) in new[] { (2, AppointmentStatus.Completed), (3, AppointmentStatus.Completed), (4, AppointmentStatus.NoShow) })
        {
            var past = await spa.BookOkAsync(TestSpa.At(10, date: TestSpa.Monday.AddDays(-7 * weeksAgo)), Ct);
            await spa.PostAsync<AppointmentDto>($"/api/appointments/{past.Id}/status", new AppointmentStatusChange(status), Ct);
        }

        var today = await spa.BookOkAsync(TestSpa.At(10), Ct);
        var newcomer = await TestSpa.ReadAsync<CustomerDto>(await spa.Client.PostAsJsonAsync("/api/customers",
            new CustomerInput("Nora", "Neu", null, null, null, "Nur Therapeutin"), TestSpa.Json, Ct), Ct);
        Assert.Equal(HttpStatusCode.Created, (await spa.BookAsync(TestSpa.At(14), Ct, customer: newcomer.Id)).StatusCode);

        var day = await spa.GetAsync<CalendarDay>($"/api/locations/{spa.Location.Id}/calendar?date={TestSpa.Monday:yyyy-MM-dd}", Ct);

        var regular = Assert.Single(day.Guests!, g => g.CustomerId == today.CustomerId);
        Assert.Equal(2, regular.Visits);
        Assert.Equal(2, regular.VisitsLastYear);
        Assert.Equal(1, regular.NoShows);
        Assert.Equal(TestSpa.Monday.AddDays(-14), regular.LastVisit);
        var first = Assert.Single(day.Guests!, g => g.CustomerId == newcomer.Id);
        Assert.Equal(0, first.Visits);
        Assert.Null(first.LastVisit);
        Assert.Equal("Nur Therapeutin", first.Note);
    }

    [Fact]
    public async Task Kalendertag_nennt_Abwesende_mit_letztem_Tag_ohne_Krankheitsnotiz_und_Vorbereitungstext()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        foreach (var date in new[] { TestSpa.Monday, TestSpa.Monday.AddDays(1) })
        {
            await spa.PutAsync<RosterDayResult>($"/api/locations/{spa.Location.Id}/roster/{spa.Ben.Id}/{date:yyyy-MM-dd}",
                new RosterDayInput([new RosterEntry(RosterKind.Sick, null, null, null)], "Grippe"), Ct);
        }

        await spa.PutAsync<List<PreparationStepDto>>($"/api/reports/treatments/{spa.MassageTreatment.Id}/preparation",
            new[] { new PreparationStepInput("Öl auf 38 °C anwärmen", 30), new PreparationStepInput("Liege mit frischem Laken", null) }, Ct);
        await spa.BookOkAsync(TestSpa.At(10), Ct, spa.Anna.Id);

        var day = await spa.GetAsync<CalendarDay>($"/api/locations/{spa.Location.Id}/calendar?date={TestSpa.Monday:yyyy-MM-dd}", Ct);

        var sick = Assert.Single(day.Absences!);
        Assert.Equal(spa.Ben.Id, sick.EmployeeId);
        Assert.Equal(RosterKind.Sick, sick.Kind);
        Assert.Equal(TestSpa.Monday.AddDays(1), sick.Until);
        Assert.Null(sick.Note);
        var preparation = Assert.Single(day.Preparations!);
        Assert.Equal(spa.MassageTreatment.Id, preparation.TreatmentId);
        Assert.Equal("Öl auf 38 °C anwärmen", preparation.Text);
    }

    [Fact]
    public async Task Kalendertag_nennt_wer_heute_an_einem_anderen_Standort_arbeitet()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var graz = await spa.CreateLocationAsync("Graz", [DayOfWeek.Monday], Ct);
        await spa.PutAsync<RosterDayResult>($"/api/locations/{graz.Id}/roster/{spa.Ben.Id}/{TestSpa.Monday:yyyy-MM-dd}",
            new RosterDayInput([new RosterEntry(RosterKind.Work, graz.Id, new(10, 0), new(18, 0))], null), Ct);

        var day = await spa.GetAsync<CalendarDay>($"/api/locations/{spa.Location.Id}/calendar?date={TestSpa.Monday:yyyy-MM-dd}", Ct);

        var away = Assert.Single(day.Elsewhere!);
        Assert.Equal(spa.Ben.Id, away.EmployeeId);
        Assert.Equal("Graz", away.LocationName);
        Assert.Equal(TestSpa.At(10), away.Start);
        Assert.Equal(TestSpa.At(18), away.End);
        // Wer hier Dienst hat, steht nicht unter „anderer Standort“, und wer dort arbeitet, ist nicht abwesend.
        Assert.DoesNotContain(day.Shifts, s => s.EmployeeId == spa.Ben.Id);
        Assert.Empty(day.Absences!);
    }

    [Fact]
    public async Task Kalender_warnt_wenn_fuer_die_gesetzliche_Pause_keine_Zeit_bleibt()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var url = $"/api/locations/{spa.Location.Id}/calendar?date={TestSpa.Monday:yyyy-MM-dd}";
        await spa.PostAsync<BlockDto>("/api/blocks", new BlockInput(ResourceKind.Employee, spa.Anna.Id, TestSpa.At(9), TestSpa.At(12), "Büro"), Ct);
        var afternoon = await spa.PostAsync<BlockDto>("/api/blocks",
            new BlockInput(ResourceKind.Employee, spa.Anna.Id, TestSpa.At(12, 20), TestSpa.At(17), "Schulung im Haus"), Ct);

        var full = Assert.Single((await spa.GetAsync<CalendarDay>(url, Ct)).Breaks!, b => b.EmployeeId == spa.Anna.Id);
        Assert.Equal(30, full.RequiredMinutes);
        Assert.Equal(0, full.PlannedMinutes);
        Assert.StartsWith("Keine Zeit mehr für die gesetzliche Pause: 30 Min.", full.Warning, StringComparison.Ordinal);
        var ben = Assert.Single((await spa.GetAsync<CalendarDay>(url, Ct)).Breaks!, b => b.EmployeeId == spa.Ben.Id);
        Assert.Null(ben.Warning);

        Assert.Equal(HttpStatusCode.NoContent, (await spa.Client.DeleteAsync($"/api/blocks/{afternoon.Id}", Ct)).StatusCode);
        await spa.PostAsync<BlockDto>("/api/blocks", new BlockInput(ResourceKind.Employee, spa.Anna.Id, TestSpa.At(12), TestSpa.At(12, 30), "Pause"), Ct);

        var planned = Assert.Single((await spa.GetAsync<CalendarDay>(url, Ct)).Breaks!, b => b.EmployeeId == spa.Anna.Id);
        Assert.Equal(30, planned.PlannedMinutes);
        Assert.Null(planned.Warning);
    }

    [Fact]
    public async Task Notizen_fuer_den_Tag_und_an_einer_Zeit_lassen_sich_abhaken_und_loeschen()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var url = $"/api/locations/{spa.Location.Id}/calendar?date={TestSpa.Monday:yyyy-MM-dd}";
        var dayNote = await spa.PostAsync<CalendarNoteDto>($"/api/locations/{spa.Location.Id}/calendar-notes",
            new CalendarNoteInput(TestSpa.Monday, " Lieferung Öle um 11 "), Ct);
        var callBack = await spa.PostAsync<CalendarNoteDto>($"/api/locations/{spa.Location.Id}/calendar-notes",
            new CalendarNoteInput(TestSpa.Monday, "Herr Lang ruft zurück", ResourceKind.Employee, spa.Anna.Id, TestSpa.At(14)), Ct);
        var half = await spa.Client.PostAsJsonAsync($"/api/locations/{spa.Location.Id}/calendar-notes",
            new CalendarNoteInput(TestSpa.Monday, "ohne Zeit", ResourceKind.Employee, spa.Anna.Id), TestSpa.Json, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, half.StatusCode);
        Assert.Equal("Lieferung Öle um 11", dayNote.Text);
        var notes = (await spa.GetAsync<CalendarDay>(url, Ct)).Notes!;
        Assert.Equal(2, notes.Count);
        Assert.Contains(notes, n => n.Id == callBack.Id && n.ResourceId == spa.Anna.Id && n.At == TestSpa.At(14) && !n.Done);

        var done = await spa.PatchAsync<CalendarNoteDto>($"/api/calendar-notes/{callBack.Id}", new CalendarNoteChange(Done: true), Ct);
        Assert.True(done.Done);
        Assert.Equal("Herr Lang ruft zurück", done.Text);
        Assert.Equal(HttpStatusCode.NoContent, (await spa.Client.DeleteAsync($"/api/calendar-notes/{dayNote.Id}", Ct)).StatusCode);

        var left = Assert.Single((await spa.GetAsync<CalendarDay>(url, Ct)).Notes!);
        Assert.Equal(callBack.Id, left.Id);
        Assert.True(left.Done);
    }

    [Fact]
    public async Task Check_in_aendert_den_Status()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var appointment = await spa.BookOkAsync(TestSpa.At(10), Ct);

        var checkedIn = await spa.PostAsync<AppointmentDto>($"/api/appointments/{appointment.Id}/status",
            new AppointmentStatusChange(AppointmentStatus.CheckedIn), Ct);

        Assert.Equal(AppointmentStatus.CheckedIn, checkedIn.Status);
    }

    [Fact]
    public async Task Kalender_ohne_Anmeldung_wird_abgelehnt()
    {
        using var client = fixture.CreateClient();

        var response = await client.GetAsync($"/api/locations/{Guid.NewGuid()}/calendar?date=2027-03-01", Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Buchung_kommt_live_beim_Kalender_des_Standorts_an()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var received = new TaskCompletionSource<AppointmentDto>(TaskCreationOptions.RunContinuationsAsynchronously);

        await using var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(fixture.Factory.Server.BaseAddress, LiveHubContract.Path), options =>
            {
                options.HttpMessageHandlerFactory = _ => fixture.Factory.Server.CreateHandler();
                options.Transports = HttpTransportType.LongPolling;
                options.Headers["Cookie"] = spa.Cookie;
            })
            .AddJsonProtocol(o => o.PayloadSerializerOptions = TestSpa.Json)
            .Build();
        connection.On<AppointmentDto>(LiveHubContract.AppointmentChanged, a => received.TrySetResult(a));
        await connection.StartAsync(Ct);
        await connection.InvokeAsync(LiveHubContract.WatchLocation, spa.Location.Id, Ct);

        var appointment = await spa.BookOkAsync(TestSpa.At(10), Ct);

        var live = await received.Task.WaitAsync(TimeSpan.FromSeconds(30), Ct);
        Assert.Equal(appointment.Id, live.Id);
        Assert.Equal(AppointmentStatus.Booked, live.Status);
    }
}
