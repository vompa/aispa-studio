using System.Net;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Controls;
using AispaStudio.Desktop.ViewModels;

namespace AispaStudio.Desktop.Tests;

/// <summary>Konfliktmeldung mit Liste in Ortszeit (Schritt 18, Ä21): der Server nennt UTC, die App zeigt die Zeit des Standorts.</summary>
public sealed class ConflictNoticeTests
{
    private static readonly TimeZoneInfo Vienna = TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna");

    private static readonly DateOnly Day = TestData.Monday;

    private sealed class FixedTime : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => TestData.At(10);

        public override TimeZoneInfo LocalTimeZone => Vienna;
    }

    private static BookingConflict Booked(Guid id, string guest, DateTimeOffset from, DateTimeOffset until) =>
        new(ResourceKind.Employee, TestData.Anna.Id, guest, "hat hier einen Termin", from, until, id);

    private static BookingConflict Blocked(string reason, DateTimeOffset from, DateTimeOffset until) =>
        new(ResourceKind.Employee, TestData.Anna.Id, reason, "ist gesperrt", from, until, IsBlock: true);

    private static BookingRejected Rejected(params BookingConflict[] conflicts) => new("Zeitraum ist belegt.", conflicts);

    [Fact]
    public void Zeiten_stehen_in_Ortszeit_ohne_Datum_am_Tag_des_Kalenders()
    {
        var notice = ConflictNotice.From(Rejected(
            Booked(Guid.NewGuid(), "Clara Fischer", TestData.At(9, 40), TestData.At(10, 50)),
            Blocked("Mittagspause", TestData.At(10, 50), TestData.At(12, 10))), Vienna, Day)!;

        Assert.Equal("Zeitraum ist belegt.", notice.Title);
        Assert.Equal(["Termin · 09:40–10:50 · Clara Fischer", "Sperrzeit · 10:50–12:10 · Mittagspause"], notice.Lines.Select(l => l.Text));
        Assert.Equal("Zuerst verschieben oder einen anderen Zeitraum wählen.", notice.Action);
        Assert.DoesNotContain("UTC", notice.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public void Ein_anderer_Tag_als_der_Kalender_steht_mit_Datum()
    {
        // 08:40 UTC am Dienstag ist 09:40 in Wien.
        var from = new DateTimeOffset(2027, 3, 2, 8, 40, 0, TimeSpan.Zero);

        var notice = ConflictNotice.From(Rejected(Booked(Guid.NewGuid(), "Clara Fischer", from, from.AddMinutes(70))), Vienna, Day)!;

        Assert.Equal("Termin · Di. 2.3. 09:40–10:50 · Clara Fischer", Assert.Single(notice.Lines).Text);
    }

    [Fact]
    public void Symbole_nach_Art_Termin_Kalender_Sperrzeit_Schloss_Raum_Tuer_Geraet_Werkzeug()
    {
        var room = new BookingConflict(ResourceKind.Room, TestData.Room.Id, "Raum Zeder", "ist zu dieser Zeit schon belegt", TestData.At(10), TestData.At(11));
        var equipment = new BookingConflict(ResourceKind.Equipment, Guid.NewGuid(), "Dampfbad", "ist zu dieser Zeit schon belegt", TestData.At(10), TestData.At(11));

        var notice = ConflictNotice.From(Rejected(
            Booked(Guid.NewGuid(), "Clara Fischer", TestData.At(9), TestData.At(10)), Blocked("Wartung", TestData.At(10), TestData.At(11)), room, equipment), Vienna, Day)!;

        Assert.Equal([Icons.Calendar, Icons.Lock, Icons.Door, Icons.Wrench], notice.Lines.Select(l => l.Icon));
    }

    [Fact]
    public void Nur_eine_Terminzeile_ist_anklickbar_und_markiert_den_Termin()
    {
        var id = Guid.NewGuid();
        (Guid, DateTimeOffset)? shown = null;
        var notice = ConflictNotice.From(Rejected(
            Booked(id, "Clara Fischer", TestData.At(9, 40), TestData.At(10, 50)),
            Blocked("Mittagspause", TestData.At(10, 50), TestData.At(12, 10))), Vienna, Day, (appointment, start) => shown = (appointment, start))!;

        Assert.Equal([true, false], notice.Lines.Select(l => l.CanShow));
        notice.Lines[0].ShowCommand!.Execute(null);

        Assert.Equal((id, TestData.At(9, 40)), shown);
    }

    [Fact]
    public void Die_Aktion_markiert_den_ersten_Termin_der_Liste()
    {
        var id = Guid.NewGuid();
        (Guid, DateTimeOffset)? shown = null;
        var notice = ConflictNotice.From(Rejected(
            Blocked("Mittagspause", TestData.At(9), TestData.At(9, 40)),
            Booked(id, "Clara Fischer", TestData.At(9, 40), TestData.At(10, 50)),
            Booked(Guid.NewGuid(), "Eva Muster", TestData.At(11), TestData.At(12))), Vienna, Day, (appointment, start) => shown = (appointment, start))!;

        Assert.Equal("Zum Konflikt", notice.ActionText);
        notice.ActionCommand!.Execute(null);

        Assert.Equal((id, TestData.At(9, 40)), shown);
    }

    [Fact]
    public void Die_Aktion_Zum_Konflikt_oeffnet_den_Konflikt_und_die_Zeile_markiert_weiter_nur()
    {
        var id = Guid.NewGuid();
        var conflict = Booked(id, "Anna", TestData.At(9, 40), TestData.At(10, 50));
        BookingConflict? opened = null;
        (Guid, DateTimeOffset)? shown = null;
        var notice = ConflictNotice.From(Rejected(conflict), Vienna, Day, new ConflictLinks((appointment, start) => shown = (appointment, start), c => opened = c))!;

        notice.ActionCommand!.Execute(null);
        Assert.Equal(conflict, opened);
        Assert.Null(shown);

        notice.Lines[0].ShowCommand!.Execute(null);
        Assert.Equal((id, TestData.At(9, 40)), shown);
    }

    [Fact]
    public void Die_Zeile_nennt_zur_belegten_Person_den_Raum_und_zum_belegten_Raum_die_Person()
    {
        var id = Guid.NewGuid();
        var person = Booked(id, "Anna", TestData.At(9, 40), TestData.At(10, 50));
        var room = new BookingConflict(ResourceKind.Room, TestData.Room.Id, "Raum Zeder", "ist zu dieser Zeit schon belegt", TestData.At(9, 40), TestData.At(10, 50), id);
        var links = new ConflictLinks(null, null, (_, kind) => kind == ResourceKind.Room ? "Anna" : "Raum Zeder");

        var notice = ConflictNotice.From(Rejected(person, room), Vienna, Day, links)!;

        Assert.Equal(["Termin · 09:40–10:50 · Anna · Raum Zeder", "Termin · 09:40–10:50 · Raum Zeder · Anna"], notice.Lines.Select(l => l.Text));
    }

    [Fact]
    public void Kein_Raum_frei_nennt_den_belegten_Raum_und_die_naechste_freie_Zeit_in_Ortszeit()
    {
        var busy = new BookingConflict(ResourceKind.Room, TestData.Room.Id, "Raum Zeder", "ist zu dieser Zeit schon belegt", TestData.At(11), TestData.At(12, 15), Guid.NewGuid());
        var summary = new BookingConflict(ResourceKind.Room, Guid.Empty, "Räume", "Kein geeigneter Raum ist zu dieser Zeit frei", NextFree: TestData.At(12, 20));

        var notice = ConflictNotice.From(Rejected(busy, summary), Vienna, Day)!;

        Assert.Equal(["Termin · 11:00–12:15 · Raum Zeder", "Räume: Kein geeigneter Raum ist zu dieser Zeit frei. Ein Raum ist frei ab 12:20"], notice.Lines.Select(l => l.Text));
        Assert.DoesNotContain("UTC", notice.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public void Eine_naechste_freie_Zeit_an_einem_anderen_Tag_steht_mit_Datum()
    {
        var summary = new BookingConflict(ResourceKind.Room, Guid.Empty, "Räume", "Kein geeigneter Raum ist zu dieser Zeit frei", NextFree: TestData.At(9).AddDays(1));
        var busy = new BookingConflict(ResourceKind.Room, TestData.Room.Id, "Raum Zeder", "ist zu dieser Zeit schon belegt", TestData.At(11), TestData.At(12), Guid.NewGuid());

        var notice = ConflictNotice.From(Rejected(busy, summary), Vienna, Day)!;

        Assert.EndsWith("Ein Raum ist frei ab Di. 2.3. 09:00", notice.Lines[1].Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Ohne_Termin_in_der_Liste_gibt_es_keine_Aktion()
    {
        var notice = ConflictNotice.From(Rejected(Blocked("Mittagspause", TestData.At(10, 50), TestData.At(12, 10))), Vienna, Day, (_, _) => { })!;

        Assert.Null(notice.ActionText);
        Assert.Null(notice.ActionCommand);
    }

    [Fact]
    public void Schliessen_blendet_die_Meldung_aus_und_meldet_die_Aenderung()
    {
        var notice = ConflictNotice.From(Rejected(Blocked("Mittagspause", TestData.At(10, 50), TestData.At(12, 10))), Vienna, Day)!;
        var changed = new List<string?>();
        notice.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
        Assert.False(notice.IsDismissed);

        notice.DismissCommand.Execute(null);

        Assert.True(notice.IsDismissed);
        Assert.Contains(nameof(ConflictNotice.IsDismissed), changed);
    }

    [Fact]
    public void Ohne_belegten_Zeitraum_gibt_es_keine_Liste_und_der_Fehlertext_gilt()
    {
        var rejected = new BookingRejected("Der Standort hat zu dieser Zeit geschlossen.",
            [new BookingConflict(ResourceKind.Employee, TestData.Anna.Id, "Anna", "hat nicht die nötige Qualifikation")]);

        Assert.Null(ConflictNotice.From(rejected, Vienna, Day));
        Assert.Null(ConflictNotice.From(null, Vienna, Day));
    }

    [Fact]
    public async Task Sperren_ueber_einem_Termin_zeigt_die_Liste_in_Ortszeit_und_ein_Klick_markiert_den_Termin()
    {
        var appointment = TestData.Appointment(10, TestData.Anna);
        var conflict = Rejected(Booked(appointment.Id, "Eva Muster", appointment.BlockedFrom, appointment.BlockedUntil));
        var api = Api(appointment).On("POST /api/blocks", conflict, HttpStatusCode.Conflict);
        var vm = await OpenAsync(api);
        vm.OpenNoteAt(10, vm.Hours.Single(h => h.Label == "14:00").Top + 1);
        Assert.True(vm.SelectNewEntry(4));
        var entry = vm.Entry!;
        entry.Text = "Schulung";

        await entry.SaveAsync();

        Assert.Null(entry.Error);
        Assert.Equal("Termin · 09:55–11:00 · Eva Muster · Raum Zeder", Assert.Single(entry.Conflict!.Lines).Text);
        Assert.Null(vm.MarkedId);

        entry.Conflict.Lines[0].ShowCommand!.Execute(null);

        Assert.Equal(appointment.Id, vm.MarkedId);
        Assert.NotNull(vm.Entry);
    }

    [Fact]
    public async Task Abgelehntes_Verschieben_nennt_den_Zeitraum_in_Ortszeit_in_der_Meldung()
    {
        var appointment = TestData.Appointment(10, TestData.Anna);
        var other = TestData.Appointment(11, TestData.Anna);
        var conflict = Rejected(Booked(other.Id, "Clara Fischer", other.Start, other.End));
        var api = Api(appointment).On($"POST /api/appointments/{appointment.Id}/move", conflict, HttpStatusCode.Conflict);
        var vm = await OpenAsync(api);

        vm.BeginDrag(vm.Columns[0].Items[0], grabOffset: 0);
        vm.DragTo(10, vm.Columns[0].Items[0].Top + 60 * DayLayout.PixelsPerMinute);
        await vm.DropAsync();

        Assert.Equal("Nicht verschoben", vm.Problem!.Title);
        Assert.Equal("Zeitraum ist belegt. Termin · 11:00–11:50 · Clara Fischer", vm.Problem.Message);
    }

    [Fact]
    public async Task Zeiten_aendern_zeigt_die_Liste_im_Panel_statt_eines_Fehlertextes()
    {
        var appointment = TestData.Appointment(10, TestData.Anna);
        var conflict = Rejected(Blocked("Mittagspause", TestData.At(11), TestData.At(12)));
        var api = Api(appointment).On($"PUT /api/appointments/{appointment.Id}/times", conflict, HttpStatusCode.Conflict);
        var vm = await OpenAsync(api);
        vm.OpenAppointment(appointment);
        var panel = vm.Selected!;
        panel.BeginEditTimes();
        panel.TimesFollowUp = 60;

        await panel.ApplyTimesCommand.ExecuteAsync(null);

        Assert.Equal("Sperrzeit · 11:00–12:00 · Mittagspause", Assert.Single(panel.Conflict!.Lines).Text);
        Assert.Null(vm.Toast);
        Assert.True(panel.IsEditingTimes);
    }

    [Fact]
    public async Task Buchen_zeigt_die_Liste_in_Ortszeit_und_loescht_sie_beim_naechsten_Versuch()
    {
        var other = TestData.Appointment(9, TestData.Anna);
        var conflict = Rejected(Booked(other.Id, "Clara Fischer", other.Start, other.End));
        var api = Api(other)
            .On("POST /api/appointments", conflict, HttpStatusCode.Conflict)
            .On("GET /api/customers?search=eva", new List<CustomerDto> { new(Guid.NewGuid(), "Eva", "Muster", null, null, null, null) });
        var vm = await OpenAsync(api);
        vm.StartBooking(vm.Columns[0], 30 * DayLayout.PixelsPerMinute);
        var booking = vm.Booking!;
        await booking.SearchAsync("eva");
        booking.Customer = booking.Customers[0];
        // In der Öffnungszeit (ab 9 Uhr): davor meldet seit Slice 78 schon die App „geschlossen“, ohne den Server zu fragen.
        booking.StartText = "09:00";

        await booking.BookCommand.ExecuteAsync(null);

        Assert.Null(booking.Error);
        Assert.Equal("Termin · 09:00–09:50 · Clara Fischer · Raum Zeder", Assert.Single(booking.Conflict!.Lines).Text);

        api.On("POST /api/appointments", other with { Id = Guid.NewGuid(), UpdatedAt = TestData.At(10) }, HttpStatusCode.Created);
        await booking.BookCommand.ExecuteAsync(null);
        Assert.Null(booking.Conflict);
    }

    private static StubApi Api(params AppointmentDto[] appointments) => new StubApi()
        .On("GET /api/locations", new List<LocationDto> { TestData.Location })
        .On($"GET /api/locations/{TestData.LocationId}/resources", TestData.Resources)
        .On($"GET /api/locations/{TestData.LocationId}/calendar?date=2027-03-01", TestData.Day(appointments));

    private static async Task<CalendarViewModel> OpenAsync(StubApi api)
    {
        var vm = new CalendarViewModel(api.Client(), TestData.Reception(), null, new FixedTime());
        await vm.InitializeAsync();
        await vm.LoadAsync();
        return vm;
    }
}
