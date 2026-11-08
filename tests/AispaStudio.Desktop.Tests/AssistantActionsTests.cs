using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;
using AispaStudio.Desktop.ViewModels;
using Avalonia.Input;

namespace AispaStudio.Desktop.Tests;

/// <summary>
/// „KI und Hilfe“ wirkt auf die offene Seite: Termin zeigen, alle Termine eines Gastes über sieben Tage, „Freie Zeit suchen“ vorbelegt,
/// „Jetzt suchen“ unter der Hilfe. Ausgeführt wird erst nach dem Klick (ADR 0008).
/// </summary>
public sealed class AssistantActionsTests
{
    private static readonly TimeZoneInfo Vienna = TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna");

    private sealed class FixedTime : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => TestData.At(10);

        public override TimeZoneInfo LocalTimeZone => Vienna;
    }

    private static AssistantAnswer Answer(IReadOnlyList<AssistantSuggestion> suggestions, IReadOnlyList<AssistantHelpLink>? help = null) =>
        new(AssistantAudience.Reception, "Gefunden.", [], suggestions, "Antwort einer KI.", help ?? []);

    private static CompanionViewModel Companion(AssistantAnswer answer, bool canBook = true) => new(new StubApi().On("POST /api/assistant", answer).Client(),
        new MemorySettingsStore(), true, AssistantAudience.Reception)
    { CanBook = canBook, FreeTimeRequested = (_, _) => Task.CompletedTask };

    private static async Task AskAsync(CompanionViewModel companion)
    {
        companion.Question = "Frage";
        await companion.AskCommand.ExecuteAsync(null);
    }

    [Fact]
    public async Task Die_Antwort_auf_Wann_kommt_Frau_Huber_zeigt_den_Termin_und_alle_Termine_als_Knoepfe()
    {
        var customer = Guid.NewGuid();
        var appointment = Guid.NewGuid();
        var start = TestData.At(17);
        var companion = Companion(Answer(
        [
            new(AssistantSuggestionKind.OpenAppointment, "Steffi Huber: Mo., 17:00 Massage", TestData.LocationId, null, null, customer, appointment, start),
            new(AssistantSuggestionKind.ShowCustomerAppointments, "Alle Termine von Steffi Huber", null, null, null, customer, null, null),
            new(AssistantSuggestionKind.OpenCustomer, "Steffi Huber", null, null, null, customer, null, null),
        ]));
        (Guid, DateTimeOffset?)? shown = null;
        Guid? all = null;
        companion.AppointmentRequested = (id, at) => shown = (id, at);
        companion.CustomerAppointmentsRequested = id =>
        {
            all = id;
            return Task.CompletedTask;
        };
        companion.CustomerRequested = _ => Task.CompletedTask;

        await AskAsync(companion);

        Assert.Equal(["Im Kalender zeigen", "Alle Termine zeigen", "Kunde öffnen"], companion.Suggestions.Select(s => s.Action));
        // Nichts geschieht, bevor jemand klickt.
        Assert.Null(shown);
        Assert.Null(all);

        await companion.UseSuggestionCommand.ExecuteAsync(companion.Suggestions[0]);
        await companion.UseSuggestionCommand.ExecuteAsync(companion.Suggestions[1]);

        Assert.Equal((appointment, (DateTimeOffset?)start), shown);
        Assert.Equal(customer, all);
    }

    [Fact]
    public async Task Freie_Zeit_fuer_Hot_Stone_morgen_bei_Lena_im_Raum_Lotus_oeffnet_die_Suche_vorbelegt_erst_nach_dem_Klick()
    {
        var room = Guid.NewGuid();
        var tomorrow = TestData.At(9).AddDays(1);
        var companion = Companion(Answer(
        [
            new(AssistantSuggestionKind.SearchFreeTime, "Freie Zeit suchen", TestData.LocationId, TestData.MassageTreatment.Id, TestData.Anna.Id, null, null, tomorrow, room),
        ]));
        FreeTimePrefill? prefill = null;
        DateTimeOffset? from = null;
        companion.FreeTimeRequested = (p, at) =>
        {
            (prefill, from) = (p, at);
            return Task.CompletedTask;
        };

        await AskAsync(companion);
        Assert.Null(prefill);
        await companion.UseSuggestionCommand.ExecuteAsync(Assert.Single(companion.Suggestions));

        Assert.Equal(new FreeTimePrefill(TestData.MassageTreatment.Id, TestData.Anna.Id, room), prefill);
        Assert.Equal(tomorrow, from);
        // Ohne Buchungsrecht gibt es den Knopf nicht (der Server schlägt ihn dann auch nicht vor).
        var viewOnly = Companion(Answer(
        [
            new(AssistantSuggestionKind.SearchFreeTime, "Freie Zeit suchen", TestData.LocationId, null, null, null, null, tomorrow),
        ]), canBook: false);
        await AskAsync(viewOnly);
        Assert.Empty(viewOnly.Suggestions);
    }

    [Fact]
    public async Task Unter_der_Hilfe_zu_freien_Zeiten_steht_Jetzt_suchen_und_oeffnet_erst_auf_Klick()
    {
        var help = new AssistantHelpLink("kalender-freie-zeit-suchen", null, "Freie Zeit suchen", "Wo ist noch Platz?");
        var companion = Companion(Answer([], [help]));
        FreeTimePrefill? opened = null;
        companion.FreeTimeRequested = (p, _) =>
        {
            opened = p;
            return Task.CompletedTask;
        };

        await AskAsync(companion);

        Assert.Equal("Jetzt suchen", companion.HelpAction?.Label);
        Assert.Null(opened);
        await companion.RunHelpActionCommand.ExecuteAsync(null);
        Assert.Equal(new FreeTimePrefill(), opened);

        // Andere Hilfethemen und fehlendes Recht: kein Knopf.
        var viewOnly = Companion(Answer([], [help]), canBook: false);
        await AskAsync(viewOnly);
        Assert.Null(viewOnly.HelpAction);
        var other = Companion(Answer([], [new AssistantHelpLink("kassa", null, "Kassa", "x")]));
        await AskAsync(other);
        Assert.Null(other.HelpAction);
    }

    // ---------- Alle Termine eines Gastes ----------

    private static StubApi CalendarApi(CustomerDto guest, params AppointmentDto[] appointments)
    {
        var api = new StubApi()
            .On("GET /api/locations", new List<LocationDto> { TestData.Location })
            .On($"GET /api/locations/{TestData.LocationId}/resources", TestData.Resources)
            .On($"GET /api/customers/{guest.Id}", guest)
            .On($"GET /api/customers/{guest.Id}/appointments", appointments.ToList());
        for (var i = 0; i < 28; i++)
        {
            var date = TestData.Monday.AddDays(i);
            var onDay = appointments.Where(a => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(a.Start, Vienna).DateTime) == date).ToArray();
            var others = i % 3 == 0 ? [TestData.Appointment(12, TestData.Ben) with { Id = Guid.NewGuid(), CustomerId = Guid.NewGuid(), Start = TestData.At(12).AddDays(i), End = TestData.At(12, 50).AddDays(i), BlockedFrom = TestData.At(12).AddDays(i), BlockedUntil = TestData.At(13).AddDays(i) }] : Array.Empty<AppointmentDto>();
            api.On($"GET /api/locations/{TestData.LocationId}/calendar?date={date:yyyy-MM-dd}", TestData.Day([.. onDay, .. others]) with
            {
                Date = date,
                Opening = [new TimeRange(TestData.At(9).AddDays(i), TestData.At(20).AddDays(i))],
                Shifts = [new EmployeeShift(TestData.Anna.Id, TestData.At(9).AddDays(i), TestData.At(17).AddDays(i)), new EmployeeShift(TestData.Ben.Id, TestData.At(12).AddDays(i), TestData.At(20).AddDays(i))],
            });
        }

        return api;
    }

    [Fact]
    public async Task Alle_Termine_von_Steffi_Huber_zeigt_die_Termine_des_Gastes_bei_allen_Personen_ueber_sieben_Tage()
    {
        var guest = new CustomerDto(Guid.NewGuid(), "Steffi", "Huber", null, "+43 1", null, null);
        AppointmentDto Mine(int dayOffset, int hour, EmployeeDto who) => TestData.Appointment(hour, who) with
        {
            Id = Guid.NewGuid(),
            CustomerId = guest.Id,
            CustomerName = "Steffi Huber",
            Start = TestData.At(hour).AddDays(dayOffset),
            End = TestData.At(hour, 50).AddDays(dayOffset),
            BlockedFrom = TestData.At(hour).AddDays(dayOffset),
            BlockedUntil = TestData.At(hour + 1).AddDays(dayOffset),
        };
        var api = CalendarApi(guest, Mine(0, 10, TestData.Anna), Mine(2, 14, TestData.Ben), Mine(2, 16, TestData.Anna), Mine(9, 11, TestData.Anna));
        var vm = new CalendarViewModel(api.Client(), TestData.Reception(), null, new FixedTime());
        await vm.InitializeAsync();
        await vm.LoadAsync();

        await vm.FilterByCustomerAsync(guest.Id);
        for (var i = 0; i < 200 && vm.Columns.Count != 7; i++)
        {
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        Assert.True(vm.IsFiltered);
        Assert.Equal("Steffi Huber · 7 Tage", vm.FilterText);
        Assert.Equal(7, vm.Columns.Count);
        Assert.Equal(TestData.Monday, vm.Date);
        // Am Mittwoch zwei Termine bei verschiedenen Personen in einer Spalte, am Montag einer; die Termine anderer Gäste fehlen.
        Assert.Equal([1, 0, 2, 0, 0, 0, 0], vm.Columns.Select(c => c.Items.Count));
        Assert.All(vm.Columns.SelectMany(c => c.Items), i => Assert.Equal(guest.Id, i.Appointment!.CustomerId));
        Assert.Equal(["1 Termin", "kein Termin", "2 Termine"], vm.Columns.Take(3).Select(c => c.Subtitle));

        // Die Pfeile blättern wochenweise; der Termin eine Woche später ist dann zu sehen.
        vm.NextDayCommand.Execute(null);
        for (var i = 0; i < 200 && vm.Date != TestData.Monday.AddDays(7); i++)
        {
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        for (var i = 0; i < 200 && vm.Columns.Sum(c => c.Items.Count) != 1; i++)
        {
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        Assert.Equal(1, vm.Columns.Sum(c => c.Items.Count));

        // Esc führt zum Tag mit allen Spalten zurück.
        Assert.True(vm.HandleKey(Key.Escape, KeyModifiers.None));
        Assert.False(vm.IsFiltered);
    }

    [Fact]
    public async Task Hat_der_Gast_nur_vergangene_Termine_beginnt_die_Ansicht_am_letzten()
    {
        var guest = new CustomerDto(Guid.NewGuid(), "Steffi", "Huber", null, null, null, null);
        var past = TestData.Appointment(10, TestData.Anna) with
        {
            Id = Guid.NewGuid(),
            CustomerId = guest.Id,
            CustomerName = "Steffi Huber",
            Start = TestData.At(10).AddDays(-3),
            End = TestData.At(10, 50).AddDays(-3),
            BlockedFrom = TestData.At(10).AddDays(-3),
            BlockedUntil = TestData.At(11).AddDays(-3),
            Status = AppointmentStatus.Completed
        };
        var api = CalendarApi(guest, past);
        var vm = new CalendarViewModel(api.Client(), TestData.Reception(), null, new FixedTime());
        await vm.InitializeAsync();
        await vm.LoadAsync();

        await vm.FilterByCustomerAsync(guest.Id);

        Assert.Equal(TestData.Monday.AddDays(-3), vm.Date);
    }

    // ---------- Aufträge öffnen die vorausgefüllte Maske (Ä319) ----------

    [Fact]
    public async Task Auftraege_erscheinen_als_Knoepfe_und_oeffnen_die_Maske_erst_nach_dem_Klick()
    {
        var order = new AssistantSuggestion(AssistantSuggestionKind.NewAppointment, "Neuer Termin …", TestData.LocationId, TestData.MassageTreatment.Id, TestData.Anna.Id,
            Guid.NewGuid(), null, TestData.At(11));
        var companion = Companion(Answer([order]));
        AssistantSuggestion? opened = null;
        companion.OrderRequested = s =>
        {
            opened = s;
            return Task.CompletedTask;
        };

        await AskAsync(companion);

        var button = Assert.Single(companion.Suggestions);
        Assert.StartsWith("Öffnet „Neuer Termin“ vorausgefüllt", button.Action, StringComparison.Ordinal);
        Assert.Null(opened);

        await companion.UseSuggestionCommand.ExecuteAsync(button);

        Assert.Equal(order, opened);
    }

    [Fact]
    public async Task Ohne_Buchungsrecht_zeigt_die_App_keinen_Auftragsknopf()
    {
        var companion = Companion(Answer([new(AssistantSuggestionKind.CancelAppointment, "Stornieren …", TestData.LocationId, null, null, Guid.NewGuid(), Guid.NewGuid(), TestData.At(11))]),
            canBook: false);
        companion.OrderRequested = _ => Task.CompletedTask;

        await AskAsync(companion);

        Assert.Empty(companion.Suggestions);
    }

    private static CustomerDto Steiner { get; } = new(Guid.NewGuid(), "Maria", "Steiner", null, "+43 1", null, null);

    private static AppointmentDto SteinerAt(int hour) => TestData.Appointment(hour, TestData.Anna) with
    {
        Id = Guid.NewGuid(),
        CustomerId = Steiner.Id,
        CustomerName = "Maria Steiner",
        Start = TestData.At(hour),
        End = TestData.At(hour, 50),
        BlockedFrom = TestData.At(hour),
        BlockedUntil = TestData.At(hour + 1),
    };

    private static async Task<(CalendarViewModel Calendar, StubApi Api)> CalendarAsync(params AppointmentDto[] appointments)
    {
        var api = CalendarApi(Steiner, appointments);
        var vm = new CalendarViewModel(api.Client(), TestData.Reception(), null, new FixedTime());
        await vm.InitializeAsync();
        await vm.LoadAsync();
        return (vm, api);
    }

    [Fact]
    public async Task Buche_Steiner_morgen_10_Uhr_Massage_oeffnet_Neuer_Termin_vorausgefuellt_und_bucht_nichts()
    {
        var (vm, api) = await CalendarAsync();
        var tomorrow = TestData.At(10).AddDays(1);

        await vm.OpenAssistantOrderAsync(new AssistantSuggestion(AssistantSuggestionKind.NewAppointment, "Neuer Termin …", TestData.LocationId, TestData.MassageTreatment.Id,
            TestData.Anna.Id, Steiner.Id, null, tomorrow));

        Assert.Equal(TestData.Monday.AddDays(1), vm.Date);
        var booking = Assert.IsType<BookingViewModel>(vm.Booking);
        Assert.Equal(TestData.MassageTreatment.Id, booking.Treatment?.Id);
        Assert.Equal("10:00", booking.StartText);
        Assert.Equal(Steiner.Id, booking.Customer?.Id);
        Assert.Equal(CalendarViewModel.AssistantBookingNote, booking.AssistantNote);
        Assert.DoesNotContain(api.Calls, c => c.Route.StartsWith("POST", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Buche_ohne_Uhrzeit_laesst_Beginn_leer()
    {
        var (vm, _) = await CalendarAsync();

        await vm.OpenAssistantOrderAsync(new AssistantSuggestion(AssistantSuggestionKind.NewAppointment, "Neuer Termin …", TestData.LocationId, null, null, Steiner.Id, null, null)
        {
            Day = TestData.Monday,
        });

        var booking = Assert.IsType<BookingViewModel>(vm.Booking);
        Assert.Equal("", booking.StartText);
        Assert.Equal(Steiner.Id, booking.Customer?.Id);
    }

    [Fact]
    public async Task Sag_den_Termin_von_Steiner_ab_fragt_mit_der_Gebuehr_und_storniert_erst_nach_Bestaetigung()
    {
        var appointment = SteinerAt(15);
        var (vm, api) = await CalendarAsync(appointment);
        var dialogs = new FakeDialogs().Answer(ConfirmChoice.Cancelled);
        vm.Dialogs = dialogs;

        await vm.OpenAssistantOrderAsync(new AssistantSuggestion(AssistantSuggestionKind.CancelAppointment, "Stornieren …", TestData.LocationId, null, null, Steiner.Id,
            appointment.Id, appointment.Start)
        {
            Note = "Laut Stornoregel fallen 50 % an.",
        });

        Assert.Equal(appointment.Id, vm.Selected?.Appointment.Id);
        Assert.Equal(CalendarViewModel.AssistantAppointmentNote, vm.Selected?.AssistantNote);
        var request = Assert.Single(dialogs.Asked);
        Assert.Equal("Termin stornieren?", request.Title);
        Assert.Contains("Laut Stornoregel fallen 50 % an.", request.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(api.Calls, c => c.Route.EndsWith("/cancel", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Verschiebe_Steiner_auf_morgen_14_Uhr_traegt_Tag_und_Zeit_ein_und_verschiebt_nichts()
    {
        var appointment = SteinerAt(15);
        var (vm, api) = await CalendarAsync(appointment);
        var newStart = TestData.At(14).AddDays(1);

        await vm.OpenAssistantOrderAsync(new AssistantSuggestion(AssistantSuggestionKind.MoveAppointment, "Verschieben …", TestData.LocationId, null, null, Steiner.Id,
            appointment.Id, appointment.Start)
        {
            NewStart = newStart,
            Day = TestData.Monday.AddDays(1),
        });

        var panel = Assert.IsType<AppointmentViewModel>(vm.Selected);
        Assert.True(panel.IsMovingToDay);
        Assert.Equal($"{TestData.Monday.AddDays(1):dd.MM.yyyy}", panel.MoveDateText);
        Assert.Equal("14:00", panel.MoveTimeText);
        Assert.DoesNotContain(api.Calls, c => c.Route.StartsWith("PUT", StringComparison.Ordinal) || c.Route.StartsWith("POST", StringComparison.Ordinal));
    }

    private static string SteinerOverview => $"GET /api/customers/{Steiner.Id}/anamnesis/overview";

    private static AssistantSuggestion Questionnaire(AppointmentDto appointment) =>
        new(AssistantSuggestionKind.SendQuestionnaire, "Termin mit Anamnese öffnen", TestData.LocationId, null, null, Steiner.Id, appointment.Id, appointment.Start);

    [Fact]
    public async Task Schick_Steiner_den_Bogen_springt_im_Termin_zum_Abschnitt_Anamnese_und_sendet_nichts()
    {
        var appointment = SteinerAt(15);
        var (vm, api) = await CalendarAsync(appointment);
        api.On(SteinerOverview, new AnamnesisOverview(null, null, null, []));
        var focus = new List<string>();
        vm.PanelFocusRequested += focus.Add;

        await vm.OpenAssistantOrderAsync(Questionnaire(appointment));

        Assert.Equal(appointment.Id, vm.Selected?.Appointment.Id);
        Assert.Equal(CalendarViewModel.AssistantAppointmentNote, vm.Selected?.AssistantNote);
        Assert.True(vm.Selected?.HasAnamnesis);
        Assert.Equal([AppointmentViewModel.AnamnesisLanguageBox], focus);
        Assert.Single(api.Calls, c => c.Route == SteinerOverview);
        Assert.DoesNotContain(api.Calls, c => c.Route.StartsWith("PUT", StringComparison.Ordinal) || c.Route.StartsWith("POST", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Schick_den_Bogen_springt_erst_wenn_die_Karten_darueber_geladen_sind()
    {
        var appointment = SteinerAt(15);
        var (vm, api) = await CalendarAsync(appointment);
        var consent = new TaskCompletionSource();
        api.On(SteinerOverview, new AnamnesisOverview(null, null, null, []))
            .Hold($"GET /api/customers/{Steiner.Id}/marketing-consent", consent.Task);
        var focus = new List<string>();
        vm.PanelFocusRequested += focus.Add;

        var open = vm.OpenAssistantOrderAsync(Questionnaire(appointment));
        await Task.Delay(100, TestContext.Current.CancellationToken);
        var early = focus.ToList();
        consent.SetResult();
        await open;

        Assert.Empty(early);
        Assert.Equal([AppointmentViewModel.AnamnesisLanguageBox], focus);
    }
}
