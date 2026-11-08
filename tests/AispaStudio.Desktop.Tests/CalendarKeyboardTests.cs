using System.Net;
using System.Text.Json;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;
using AispaStudio.Desktop.ViewModels;
using Avalonia.Input;

namespace AispaStudio.Desktop.Tests;

/// <summary>Tasten, Suche und Rechtsklick im Tageskalender.</summary>
public sealed class CalendarKeyboardTests
{
    private static readonly TimeZoneInfo Vienna = TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna");

    private sealed class FixedTime : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => TestData.At(10);

        public override TimeZoneInfo LocalTimeZone => Vienna;
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

    /// <summary>Tasten starten Serveraufrufe im Hintergrund; kurz warten, bis das Ergebnis da ist.</summary>
    private static async Task Until(Func<bool> condition)
    {
        for (var i = 0; i < 200 && !condition(); i++)
        {
            await Task.Delay(10);
        }

        Assert.True(condition());
    }

    [Fact]
    public async Task Klick_markiert_nur_Pfeile_wandern_Enter_oeffnet_und_Esc_schliesst()
    {
        var early = TestData.Appointment(10, TestData.Anna);
        var late = TestData.Appointment(13, TestData.Anna);
        var ben = TestData.Appointment(14, TestData.Ben);
        var vm = await OpenAsync(Api(early, late, ben));

        vm.Mark(vm.Columns[0].Items.Single(i => i.Id == early.Id));
        Assert.Null(vm.Selected);
        Assert.True(vm.Columns[0].Items.Single(i => i.Id == early.Id).IsSelected);
        Assert.Contains(vm.KeyHints, h => h.Key == "E");

        vm.HandleKey(Key.Down, KeyModifiers.None);
        Assert.Equal(late.Id, vm.MarkedId);
        vm.HandleKey(Key.Right, KeyModifiers.None);
        Assert.Equal(ben.Id, vm.MarkedId);
        Assert.Equal(TestData.Monday, vm.Date);

        vm.HandleKey(Key.Enter, KeyModifiers.None);
        Assert.Equal(ben.Id, vm.Selected?.Appointment.Id);

        vm.HandleKey(Key.Escape, KeyModifiers.None);
        Assert.Null(vm.Selected);
        Assert.Equal(ben.Id, vm.MarkedId);
        vm.HandleKey(Key.Escape, KeyModifiers.None);
        Assert.Null(vm.MarkedId);
        Assert.Contains(vm.KeyHints, h => h.Key == "N");
    }

    [Fact]
    public async Task Ohne_Markierung_wechseln_Pfeile_den_Tag_und_Pos1_springt_auf_heute()
    {
        var vm = await OpenAsync(Api());

        Assert.True(vm.HandleKey(Key.Right, KeyModifiers.None));
        Assert.Equal(TestData.Monday.AddDays(1), vm.Date);
        vm.HandleKey(Key.Home, KeyModifiers.None);
        Assert.Equal(TestData.Monday, vm.Date);

        // Buchstaben ohne Bedeutung bleiben dem Rest der App.
        Assert.False(vm.HandleKey(Key.Q, KeyModifiers.None));
        Assert.True(vm.HandleText("?"));
        Assert.True(vm.ShowKeyHelp);
        vm.HandleKey(Key.Escape, KeyModifiers.None);
        Assert.False(vm.ShowKeyHelp);
    }

    [Fact]
    public async Task Pfeil_ab_ohne_Markierung_markiert_den_naechsten_anstehenden_Termin()
    {
        var past = TestData.Appointment(9, TestData.Anna);
        var next = TestData.Appointment(11, TestData.Anna);
        var vm = await OpenAsync(Api(past, next));

        vm.HandleKey(Key.Down, KeyModifiers.None);

        Assert.Equal(next.Id, vm.MarkedId);
        Assert.NotNull(vm.MarkedBounds);
    }

    [Fact]
    public async Task Alt_Pfeile_verschieben_den_markierten_Termin_ueber_den_Server()
    {
        var appointment = TestData.Appointment(10, TestData.Anna);
        var api = Api(appointment);
        var vm = await OpenAsync(api);
        var later = appointment with { Start = TestData.At(10, 15), End = TestData.At(11, 5), UpdatedAt = TestData.At(9) };
        api.On($"POST /api/appointments/{appointment.Id}/move", later);
        vm.Mark(vm.Columns[0].Items[0]);

        vm.HandleKey(Key.Down, KeyModifiers.Alt);
        await Until(() => vm.CanUndo);

        var request = JsonSerializer.Deserialize<MoveAppointmentRequest>(api.Calls.Last().Body!, ApiClient.Json)!;
        Assert.Equal(TestData.At(10, 15), request.Start);
        Assert.Equal(TestData.Anna.Id, request.EmployeeId);
        Assert.Equal("10:15–11:05", vm.Columns[0].Items[0].TimeText);
        Assert.Equal(appointment.Id, vm.MarkedId);

        api.On($"POST /api/appointments/{appointment.Id}/move", appointment with { UpdatedAt = TestData.At(9, 30) });
        vm.HandleKey(Key.Z, KeyModifiers.Control);
        await Until(() => vm.Columns[0].Items[0].TimeText == "10:00–10:50");
    }

    [Fact]
    public async Task Lehnt_der_Server_das_Verschieben_ab_bleibt_der_Termin_stehen_und_nur_die_InfoBar_meldet_es()
    {
        var appointment = TestData.Appointment(10, TestData.Anna);
        var api = Api(appointment);
        var vm = await OpenAsync(api);
        api.On($"POST /api/appointments/{appointment.Id}/move",
            new BookingRejected("Zu dieser Zeit ist nicht alles frei, was die Behandlung braucht.", []), HttpStatusCode.Conflict);
        vm.Mark(vm.Columns[0].Items[0]);

        vm.HandleKey(Key.Down, KeyModifiers.Alt);
        await Until(() => vm.Problem is not null);

        Assert.Equal("Nicht verschoben", vm.Problem!.Title);
        Assert.Null(vm.Toast);
        Assert.Equal("10:00–10:50", vm.Columns[0].Items[0].TimeText);
    }

    [Fact]
    public async Task Kommt_spaeter_zeigt_die_Ankunft_und_laesst_den_Termin_an_seiner_Zeit()
    {
        var appointment = TestData.Appointment(10, TestData.Anna);
        var api = Api(appointment);
        var vm = await OpenAsync(api);
        var late = appointment with { LateMinutes = 15, UpdatedAt = TestData.At(9) };
        api.On($"PUT /api/appointments/{appointment.Id}/late", late);

        // Keine Untermenüs: jede Verspätung ist ein eigener Eintrag des Kontextmenüs (Design-Brief 5.21).
        var menu = vm.ActionsFor(vm.Columns[0].Items[0]);
        Assert.DoesNotContain(menu, a => a.Header == "Kommt später");
        menu.Single(a => a.Header == "Kommt 15 Min. später").Run!();
        await Until(() => vm.Toast is not null);

        Assert.Equal("Eva Muster kommt 15 Min. später (10:15)", vm.Toast);
        Assert.Null(vm.Problem);
        Assert.StartsWith("kommt 10:15 · 10:00–10:50", vm.Columns[0].Items[0].MetaText, StringComparison.Ordinal);
        Assert.DoesNotContain(api.Calls, c => c.Route.StartsWith("POST", StringComparison.Ordinal) && c.Route.EndsWith("/move", StringComparison.Ordinal));

        // „Doch pünktlich“ nimmt die Markierung zurück.
        api.On($"PUT /api/appointments/{appointment.Id}/late", appointment with { UpdatedAt = TestData.At(9, 30) });
        vm.ActionsFor(vm.Columns[0].Items[0]).Single(a => a.Header == "Doch pünktlich").Run!();
        await Until(() => vm.Toast == "Eva Muster kommt doch pünktlich");

        Assert.StartsWith("10:00–10:50", vm.Columns[0].Items[0].MetaText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task E_checkt_ein_ohne_das_Panel_zu_oeffnen()
    {
        var appointment = TestData.Appointment(10, TestData.Anna);
        var api = Api(appointment);
        var vm = await OpenAsync(api);
        api.On($"POST /api/appointments/{appointment.Id}/status", appointment with { Status = AppointmentStatus.CheckedIn, UpdatedAt = TestData.At(9) });
        vm.Mark(vm.Columns[0].Items[0]);

        vm.HandleKey(Key.E, KeyModifiers.None);
        await Until(() => vm.Columns[0].Items[0].IsCheckedIn);

        Assert.Null(vm.Selected);
        Assert.Equal("Eva Muster eingecheckt", vm.Toast);
    }

    [Fact]
    public async Task Entf_fragt_nach_Grund_und_storniert_erst_nach_Bestaetigung()
    {
        var appointment = TestData.Appointment(10, TestData.Anna);
        var api = Api(appointment);
        var vm = await OpenAsync(api);
        api.On($"POST /api/appointments/{appointment.Id}/cancel", appointment with { Status = AppointmentStatus.Cancelled, UpdatedAt = TestData.At(9) });
        var dialogs = new FakeDialogs().Answer(ConfirmChoice.Cancelled).Answer(ConfirmChoice.Confirmed, "Gast krank");
        vm.Dialogs = dialogs;
        vm.Mark(vm.Columns[0].Items[0]);

        // Erst „Abbrechen“: gefragt wird mit Grund-Feld und zerstörender Aktion, storniert wird nichts.
        vm.HandleKey(Key.Delete, KeyModifiers.None);
        var panel = vm.Selected!;
        var request1 = Assert.Single(dialogs.Asked);
        Assert.Equal("Termin stornieren?", request1.Title);
        Assert.True(request1.IsDestructive);
        Assert.Equal("Grund", request1.InputLabel);
        Assert.Equal("Stornieren", request1.ConfirmText);
        dialogs.AssertRules();
        Assert.DoesNotContain(api.Calls, c => c.Route.EndsWith("/cancel", StringComparison.Ordinal));

        // Dann „Stornieren“ mit dem eingetragenen Grund.
        await panel.AskCancelCommand.ExecuteAsync(null);
        Assert.Equal(2, dialogs.Asked.Count);

        var request = JsonSerializer.Deserialize<CancelAppointmentRequest>(api.Calls.Last(c => c.Body is not null).Body!, ApiClient.Json)!;
        Assert.Equal("Gast krank", request.Reason);
    }

    [Fact]
    public async Task Rechtsklick_zeigt_die_naechsten_Schritte_je_Status()
    {
        var booked = TestData.Appointment(10, TestData.Anna);
        var here = TestData.Appointment(13, TestData.Anna, AppointmentStatus.CheckedIn);
        var vm = await OpenAsync(Api(booked, here));
        vm.CheckoutRequested = _ => Task.CompletedTask;
        vm.CustomerRequested = _ => Task.CompletedTask;

        var forBooked = vm.ActionsFor(vm.Columns[0].Items.Single(i => i.Id == booked.Id));
        var forHere = vm.ActionsFor(vm.Columns[0].Items.Single(i => i.Id == here.Id));
        var blank = vm.ActionsAt(DayLayout.ColumnLeft(1) + 20, 300 * DayLayout.PixelsPerMinute);

        // Keine Untermenüs (Design-Brief 5.21): die Aktionen aus „Mehr“ stehen flach im Menü, das Riskante zuletzt.
        Assert.Equal(["Details öffnen", "Einchecken", "Kundenkarte", "Folgetermin", "Verschieben", "Kommt 5 Min. später", "Kommt 10 Min. später", "Kommt 15 Min. später",
            "Zeiten dieses Termins ändern …", "Erledigt", "Nicht erschienen", "Wiederholen …", "Stornieren …"], forBooked.Select(a => a.Header));
        Assert.True(forBooked[^1].IsDanger);
        Assert.Single(forBooked, a => a.IsDanger);
        Assert.Contains(forHere, a => a.Header == "Kassieren");
        Assert.DoesNotContain(forHere, a => a.Header == "Einchecken");
        Assert.DoesNotContain(forHere, a => a.Header.StartsWith("Kommt", StringComparison.Ordinal));
        Assert.Equal("Termin hier (14:00, Ben)", blank[0].Header);
    }

    [Fact]
    public async Task Suche_findet_Termine_des_Tages_und_naechste_Termine_ueber_die_Kundenkarte()
    {
        var today = TestData.Appointment(10, TestData.Anna);
        var customer = new CustomerDto(Guid.NewGuid(), "Lena", "Gruber", null, "+43 660 1234567", null, null);
        var nextWeek = TestData.Appointment(15, TestData.Ben) with
        {
            CustomerId = customer.Id,
            CustomerName = "Lena Gruber",
            Start = TestData.At(15).AddDays(7),
            End = TestData.At(15, 50).AddDays(7),
        };
        var api = Api(today)
            .On("GET /api/customers?search=0660", new List<CustomerDto> { customer })
            .On($"GET /api/customers/{customer.Id}/appointments", new List<AppointmentDto> { nextWeek })
            .On($"GET /api/locations/{TestData.LocationId}/calendar?date=2027-03-08", TestData.Day(nextWeek) with { Date = TestData.Monday.AddDays(7) })
            .On($"GET /api/locations/{TestData.LocationId}/waitlist", new List<WaitlistStaffEntry>());
        var vm = await OpenAsync(api);

        await vm.SearchAsync("eva");
        Assert.Equal("Eva Muster", Assert.Single(vm.SearchHits).Title);
        Assert.StartsWith("10:00 · Massage", vm.SearchHits[0].Detail, StringComparison.Ordinal);

        await vm.SearchAsync("0660");
        var hit = Assert.Single(vm.SearchHits);
        Assert.Equal("Lena Gruber", hit.Title);

        vm.OpenSearchHit(hit);
        await Until(() => vm.MarkedId == nextWeek.Id);
        Assert.Equal(TestData.Monday.AddDays(7), vm.Date);
        Assert.Empty(vm.SearchHits);
        Assert.Equal("", vm.SearchText);
    }

    [Fact]
    public async Task F_oeffnet_den_Folgetermin_in_vier_Wochen_mit_Gast_und_Behandlung()
    {
        var appointment = TestData.Appointment(10, TestData.Anna);
        var customer = new CustomerDto(appointment.CustomerId, "Eva", "Muster", null, null, null, null);
        var later = TestData.Monday.AddDays(28);
        var api = Api(appointment)
            .On($"GET /api/customers/{customer.Id}", customer)
            .On($"GET /api/locations/{TestData.LocationId}/calendar?date={later:yyyy-MM-dd}", TestData.Day() with { Date = later });
        var vm = await OpenAsync(api);
        vm.Mark(vm.Columns[0].Items[0]);

        vm.HandleKey(Key.F, KeyModifiers.None);
        await Until(() => vm.Booking is not null);

        Assert.Equal(later, vm.Date);
        Assert.Equal(customer, vm.Booking!.Customer);
        Assert.Equal(TestData.MassageTreatment.Id, vm.Booking.Treatment?.Id);
        Assert.Equal("10:00", vm.Booking.StartText);
        Assert.Equal("bei Anna", vm.Booking.ColumnText);
    }

    [Fact]
    public async Task F_ohne_Anna_im_Dienst_sucht_freie_Zeit_fuer_Gast_und_Behandlung_statt_einer_anderen_Behandlung_bei_Ben()
    {
        var appointment = TestData.Appointment(10, TestData.Anna);
        var customer = new CustomerDto(appointment.CustomerId, "Eva", "Muster", null, null, null, null);
        var later = TestData.Monday.AddDays(28);
        var onlyBen = TestData.Day() with { Date = later, Shifts = [new EmployeeShift(TestData.Ben.Id, TestData.At(12).AddDays(28), TestData.At(20).AddDays(28))] };
        var api = Api(appointment)
            .On($"GET /api/customers/{customer.Id}", customer)
            .On($"GET /api/locations/{TestData.LocationId}/calendar?date={later:yyyy-MM-dd}", onlyBen);
        var vm = await OpenAsync(api);
        vm.Mark(vm.Columns[0].Items[0]);

        vm.HandleKey(Key.F, KeyModifiers.None);
        await Until(() => vm.FreeTime is not null || vm.Booking is not null);

        Assert.Null(vm.Booking);
        Assert.Equal(TestData.MassageTreatment.Id, vm.FreeTime?.Treatment?.Id);
        Assert.Equal(customer.Id, vm.FreeTime?.Guest.Customer?.Id);
    }

    [Fact]
    public async Task Strg_N_sucht_freie_Zeit_Pfeile_wechseln_und_Enter_bucht()
    {
        var api = Api(TestData.Appointment(10, TestData.Anna));
        var slots = new List<AvailableSlot>
        {
            new(TestData.At(9, 30), TestData.At(10, 20), TestData.Anna.Id, TestData.Room.Id, null),
            new(TestData.At(11), TestData.At(11, 50), TestData.Anna.Id, TestData.Room.Id, null, SlotFit.Snug),
            new(TestData.At(11, 15), TestData.At(12, 5), TestData.Anna.Id, TestData.Room.Id, null),
            new(TestData.At(13), TestData.At(13, 50), TestData.Ben.Id, TestData.Room.Id, null, SlotFit.LeavesGap, "lässt 20 Min. Lücke"),
        };
        api.On($"GET /api/locations/{TestData.LocationId}/availability?treatmentId={TestData.MassageTreatment.Id}&date=2027-03-01&minSlots=3&days=14", slots)
            // Der Server nennt nur Personen mit der Qualifikation: Ben kann hier die Massage.
            .On($"GET /api/locations/{TestData.LocationId}/resources",
                TestData.Resources with { Employees = [TestData.Anna, TestData.Ben with { QualificationIds = [TestData.Massage] }] });
        var vm = await OpenAsync(api);

        Assert.True(vm.HandleKey(Key.N, KeyModifiers.Control));
        var freeTime = vm.FreeTime!;
        freeTime.Treatment = TestData.MassageTreatment;
        await Until(() => freeTime.HasSlots);

        // Vor 10:00 (jetzt) zählt heute nicht mehr; was lückenlos anschließt, steht bei gleicher Zeit vorne.
        Assert.Equal(["11:00–11:50", "11:15–12:05", "13:00–13:50"], freeTime.Slots.Select(s => s.Time));
        Assert.True(freeTime.Slots[0].IsSnug);
        Assert.Equal("lässt 20 Min. Lücke", freeTime.Slots[2].GapNote);
        // Anna: 11:00 bis 12:05 als eine Fläche, Ben eine Fläche, dazu der Rahmen der gewählten Zeit.
        Assert.Equal(3, vm.SlotMarks.Count);
        Assert.Equal("11:00–11:50 · Raum Zeder", vm.SlotMarks.Single(m => m.IsActive).Text);

        vm.HandleKey(Key.Down, KeyModifiers.None);
        vm.HandleKey(Key.Down, KeyModifiers.None);
        Assert.Equal("13:00–13:50", freeTime.Active?.Time);
        Assert.Equal(DayLayout.ColumnLeft(1, vm.Scale), vm.SlotMarks.Single(m => m.IsActive).Left);

        vm.HandleKey(Key.Enter, KeyModifiers.None);

        Assert.Null(vm.FreeTime);
        Assert.Empty(vm.SlotMarks);
        Assert.Equal("13:00", vm.Booking?.StartText);
        Assert.Equal(TestData.MassageTreatment.Id, vm.Booking?.Treatment?.Id);
        Assert.Equal("bei Ben", vm.Booking?.ColumnText);
    }
}
