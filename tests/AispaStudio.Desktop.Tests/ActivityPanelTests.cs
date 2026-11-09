using System.Net;
using System.Text.Json;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;
using AispaStudio.Desktop.ViewModels;

namespace AispaStudio.Desktop.Tests;

/// <summary>Seitenpanel der Seite „Verlauf“ (Aufgabe C): Werte, Sprünge zu Termin, Kundenkarte und Beleg, „Stornierung zurücknehmen“ mit Rückfrage, Rechte, Esc.</summary>
public sealed class ActivityPanelTests
{
    private const string Today = "2027-03-01";

    private static readonly string HistoryRoute = $"GET /api/locations/{TestData.LocationId}/history?from={Today}&until={Today}";

    private static readonly Guid CustomerId = Guid.NewGuid();

    private static DateTimeOffset Local(int hour, int minute = 0) => new(2027, 3, 1, hour, minute, 0, TimeSpan.FromHours(1));

    private static ActivityEntry Entry(ActivityKind kind, string text, Guid? appointmentId = null, Guid? receiptId = null, IReadOnlyList<ActivityValue>? values = null, bool? late = null,
        long seq = 1) =>
        new(seq, Local(10, 30), kind, text, "Rita", ActivityChannel.Reception, CustomerId, "Anna Huber", appointmentId, null, null, values ?? [], late, receiptId);

    private static AppointmentDto CancelledAppointment(Guid id) =>
        TestData.Appointment(14, TestData.Anna, AppointmentStatus.Cancelled) with { Id = id, CustomerId = CustomerId, CustomerName = "Anna Huber", Version = 7 };

    private static StubApi Api(params ActivityEntry[] items) => new StubApi()
        .On(HistoryRoute, new ActivityPage(items, null))
        .On($"GET /api/customers/{CustomerId}", new CustomerDto(CustomerId, "Anna", "Huber", null, "0664 1234567", null, null));

    private static async Task<ActivityViewModel> OpenAsync(StubApi api, CurrentUser? user = null, FakeDialogs? dialogs = null, ActivityTests.TimerTime? time = null)
    {
        var activity = new ActivityViewModel(api.Client(), user ?? TestData.Owner(), time ?? new ActivityTests.TimerTime(Local(10)))
        {
            LocationSource = () => TestData.Location,
            Dialogs = dialogs,
        };
        await activity.RefreshAsync();
        return activity;
    }

    private static void Open(ActivityViewModel activity, int rowIndex = 1)
    {
        activity.SelectedItem = activity.Rows[rowIndex];
        activity.OpenSelectedCommand.Execute(null);
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        for (var i = 0; i < 200 && !condition(); i++)
        {
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        Assert.True(condition());
    }

    private static T BodyOf<T>(StubApi api, string routeEnd) =>
        JsonSerializer.Deserialize<T>(api.Calls.Last(c => c.Route.EndsWith(routeEnd, StringComparison.Ordinal)).Body!, ApiClient.Json)!;

    [Fact]
    public async Task Panel_oeffnet_mit_Titel_Untertitel_Satz_und_der_Werte_Tabelle()
    {
        var values = new ActivityValue[] { new("Beginn", "14:00", "15:30"), new("Raum", "Zeder", null) };
        var activity = await OpenAsync(Api(Entry(ActivityKind.Moved, "Verschoben von Rita: 14:00 → 15:30", Guid.NewGuid(), values: values)));
        Assert.False(activity.HasPanel);

        Open(activity);

        Assert.True(activity.HasPanel);
        Assert.Equal(("Verschoben", "Mo. 1.3., 10:30", "Verschoben von Rita: 14:00 → 15:30"), (activity.PanelTitle, activity.PanelSubtitle, activity.PanelText));
        Assert.Equal([new ActivityValueRow("Beginn", "14:00", "15:30"), new ActivityValueRow("Raum", "Zeder", "–")], activity.PanelValues);
        Assert.Equal(("Anna Huber", "Rita", "Rezeption"), (activity.PanelGuest, activity.PanelActor, activity.PanelChannel));
        await WaitUntilAsync(() => activity.HasPanelPhone);
        Assert.Equal("0664 1234567", activity.PanelPhone);
    }

    [Fact]
    public async Task Ohne_Werte_gibt_es_keine_Tabelle()
    {
        var activity = await OpenAsync(Api(Entry(ActivityKind.StatusChanged, "Eingecheckt von Rita")));

        Open(activity);

        Assert.False(activity.HasPanelValues);
    }

    [Fact]
    public async Task Ohne_Verbindung_zum_Gast_bleibt_das_Panel_ohne_Telefonnummer()
    {
        var api = new StubApi().On(HistoryRoute, new ActivityPage([Entry(ActivityKind.Booked, "Gebucht von Rita")], null));
        var activity = await OpenAsync(api);

        Open(activity);
        await WaitUntilAsync(() => api.Calls.Any(c => c.Route == $"GET /api/customers/{CustomerId}"));

        Assert.True(activity.HasPanel);
        Assert.False(activity.HasPanelPhone);
    }

    [Fact]
    public async Task Die_Auswahl_bei_offenem_Panel_wechselt_den_Eintrag_ohne_Enter()
    {
        var activity = await OpenAsync(Api(Entry(ActivityKind.Booked, "Gebucht von Rita", seq: 2), Entry(ActivityKind.StatusChanged, "Eingecheckt von Rita", seq: 1)));
        activity.SelectedItem = activity.Rows[1];
        Assert.False(activity.HasPanel);

        activity.OpenSelectedCommand.Execute(null);
        activity.SelectedItem = activity.Rows[2];

        Assert.Equal("Eingecheckt von Rita", activity.PanelText);
    }

    [Fact]
    public async Task Esc_Schliessen_laesst_das_Panel_zu_und_meldet_den_Fokus_zurueck()
    {
        var activity = await OpenAsync(Api(Entry(ActivityKind.Booked, "Gebucht von Rita")));
        var closed = 0;
        activity.PanelClosed += () => closed++;
        Open(activity);

        activity.ClosePanelCommand.Execute(null);

        Assert.False(activity.HasPanel);
        Assert.Equal(1, closed);
    }

    [Fact]
    public async Task Termin_oeffnen_meldet_Id_und_Beginn_des_Termins()
    {
        var id = Guid.NewGuid();
        var appointment = TestData.Appointment(15, TestData.Anna) with { Id = id };
        var activity = await OpenAsync(Api(Entry(ActivityKind.Booked, "Gebucht von Rita", id)).On($"GET /api/appointments/{id}", appointment));
        (Guid, DateTimeOffset?)? requested = null;
        activity.AppointmentRequested = (appointmentId, start) => requested = (appointmentId, start);
        Open(activity);
        Assert.True(activity.IsAppointmentPrimary);

        await activity.OpenAppointmentCommand.ExecuteAsync(null);

        Assert.Equal((id, (DateTimeOffset?)appointment.Start), requested);
    }

    [Fact]
    public async Task Termin_oeffnen_ohne_Antwort_zeigt_den_Fehler_im_Panel()
    {
        var id = Guid.NewGuid();
        var activity = await OpenAsync(Api(Entry(ActivityKind.Booked, "Gebucht von Rita", id)).On($"GET /api/appointments/{id}", null, HttpStatusCode.NotFound));
        var requested = false;
        activity.AppointmentRequested = (_, _) => requested = true;
        Open(activity);

        await activity.OpenAppointmentCommand.ExecuteAsync(null);

        Assert.False(requested);
        Assert.True(activity.HasPanelError);
    }

    [Fact]
    public async Task Bei_einem_Storno_oeffnet_Termin_oeffnen_das_Panel_des_stornierten_Termins_und_Kundenkarte_oeffnen_bleibt_ein_eigener_Knopf()
    {
        var id = Guid.NewGuid();
        var cancelled = CancelledAppointment(id);
        var activity = await OpenAsync(Api(Entry(ActivityKind.Cancelled, "Storniert von Rita", id)).On($"GET /api/appointments/{id}", cancelled));
        (Guid, DateTimeOffset?)? requested = null;
        var plainAppointment = false;
        Guid? customer = null;
        activity.CancelledAppointmentRequested = (appointmentId, start) => requested = (appointmentId, start);
        activity.AppointmentRequested = (_, _) => plainAppointment = true;
        activity.CustomerRequested = guest =>
        {
            customer = guest;
            return Task.CompletedTask;
        };
        Open(activity);
        Assert.True(activity.ShowAppointment);
        Assert.True(activity.ShowCustomer);
        Assert.Equal("Öffnet den stornierten Termin im Kalender.", activity.AppointmentTip);

        await activity.OpenAppointmentCommand.ExecuteAsync(null);

        Assert.Equal((id, (DateTimeOffset?)cancelled.Start), requested);
        Assert.False(plainAppointment);
        Assert.Null(customer);

        await activity.OpenCustomerCommand.ExecuteAsync(null);

        Assert.Equal(CustomerId, customer);
    }

    [Fact]
    public async Task Kundenkarte_oeffnen_ruft_die_Kundenseite_mit_dem_Gast_und_ist_ohne_Termin_primaer()
    {
        var activity = await OpenAsync(Api(Entry(ActivityKind.CustomerChanged, "Geändert von Rita: Telefon")));
        Guid? customer = null;
        activity.CustomerRequested = id =>
        {
            customer = id;
            return Task.CompletedTask;
        };
        Open(activity);
        Assert.True(activity.IsCustomerPrimary);
        Assert.False(activity.ShowAppointment);

        await activity.OpenCustomerCommand.ExecuteAsync(null);

        Assert.Equal(CustomerId, customer);
    }

    [Fact]
    public async Task Beleg_anzeigen_meldet_die_Beleg_Id_und_den_Zeitpunkt_und_ist_primaer()
    {
        var receipt = Guid.NewGuid();
        var activity = await OpenAsync(Api(Entry(ActivityKind.Paid, "Bezahlt bar: 85,00 €", Guid.NewGuid(), receipt)));
        (Guid, DateTimeOffset)? requested = null;
        activity.ReceiptRequested = (id, at) =>
        {
            requested = (id, at);
            return Task.CompletedTask;
        };
        Open(activity);
        Assert.True(activity.ShowReceipt);
        Assert.True(activity.IsReceiptPrimary);
        Assert.False(activity.IsAppointmentPrimary);

        await activity.ShowReceiptCommand.ExecuteAsync(null);

        Assert.Equal((receipt, Local(10, 30)), requested);
    }

    [Fact]
    public async Task Ohne_Beleg_gibt_es_keinen_Knopf_Beleg_anzeigen()
    {
        var activity = await OpenAsync(Api(Entry(ActivityKind.Paid, "Bezahlt bar: 85,00 €", Guid.NewGuid())));

        Open(activity);

        Assert.False(activity.ShowReceipt);
        Assert.True(activity.IsAppointmentPrimary);
    }

    [Fact]
    public async Task Storno_zeigt_den_Grund_und_den_Hinweis_mit_Frist_und_Ausfallgebuehr_des_Standorts()
    {
        var values = new ActivityValue[] { new("Grund", null, "Krank"), new("Status", "Gebucht", "Storniert") };
        var api = Api(Entry(ActivityKind.Cancelled, "Storniert von Rita: Grund „Krank“", Guid.NewGuid(), values: values, late: true))
            .On($"GET /api/locations/{TestData.LocationId}/online-rules", new LocationOnlineRules(true, 60, 90, 24, 15, null, false, NoShowFeePercent: 50));
        var activity = await OpenAsync(api);

        Open(activity);
        await WaitUntilAsync(() => activity.LateNotice!.Contains("24 Stunden", StringComparison.Ordinal));

        Assert.Equal("Krank", activity.PanelReason);
        Assert.Equal("Kurzfristig abgesagt: innerhalb der Stornofrist von 24 Stunden. Laut Regel fallen 50 % Ausfallgebühr an.", activity.LateNotice);
        // Der Grund steht schon in der Hauptkarte und kommt nicht doppelt in die Tabelle.
        Assert.Equal([new ActivityValueRow("Status", "Gebucht", "Storniert")], activity.PanelValues);
        Assert.True(activity.IsRestorePrimary);
        Assert.True(activity.ShowCustomer);
    }

    [Fact]
    public async Task Ein_rechtzeitiger_Storno_hat_keinen_Hinweis_zur_Stornofrist()
    {
        var activity = await OpenAsync(Api(Entry(ActivityKind.Cancelled, "Storniert von Rita", Guid.NewGuid(), late: false)));

        Open(activity);

        Assert.False(activity.HasLateNotice);
    }

    [Fact]
    public async Task Stornierung_zurücknehmen_fragt_ruft_restore_zeigt_den_Toast_und_laedt_den_Verlauf_neu()
    {
        var id = Guid.NewGuid();
        var cancelled = CancelledAppointment(id);
        var api = Api(Entry(ActivityKind.Cancelled, "Storniert von Rita", id))
            .On($"GET /api/appointments/{id}", cancelled)
            .On($"GET /api/appointments/{id}/restoration", new AppointmentRestoration(CancellationNoticeState.None, false))
            .On($"POST /api/appointments/{id}/restore", cancelled with { Status = AppointmentStatus.Booked, Version = 8 });
        var dialogs = new FakeDialogs().Answer(ConfirmChoice.Confirmed);
        var activity = await OpenAsync(api, dialogs: dialogs);
        Open(activity);
        var loads = api.Calls.Count(c => c.Route == HistoryRoute);

        await activity.RestoreCommand.ExecuteAsync(null);

        var asked = Assert.Single(dialogs.AssertRules().Asked);
        Assert.Equal(("Stornierung zurücknehmen?", "Zurücknehmen"), (asked.Title, asked.ConfirmText));
        Assert.Contains("Der Termin von Anna Huber am Mo. 1.3., 14:00–14:50, ist danach wieder gebucht.", asked.Message, StringComparison.Ordinal);
        var request = BodyOf<RestoreAppointmentRequest>(api, "/restore");
        Assert.Equal((null, false, 7u), (request.Start, request.SendCorrection, request.ExpectedVersion));
        Assert.Equal("Termin wiederhergestellt: Anna Huber, 14:00", activity.Toast);
        Assert.Equal(loads + 1, api.Calls.Count(c => c.Route == HistoryRoute));
        Assert.False(activity.HasPanel);
    }

    [Fact]
    public async Task Ist_die_Absage_schon_beim_Gast_fragt_die_Rueckfrage_nach_der_Korrektur()
    {
        var id = Guid.NewGuid();
        var cancelled = CancelledAppointment(id);
        var api = Api(Entry(ActivityKind.Cancelled, "Storniert von Rita", id))
            .On($"GET /api/appointments/{id}", cancelled)
            .On($"GET /api/appointments/{id}/restoration", new AppointmentRestoration(CancellationNoticeState.Sent, false))
            .On($"POST /api/appointments/{id}/restore", cancelled with { Status = AppointmentStatus.Booked });
        var dialogs = new FakeDialogs().Answer(ConfirmChoice.Confirmed);
        var activity = await OpenAsync(api, dialogs: dialogs);
        Open(activity);

        await activity.RestoreCommand.ExecuteAsync(null);

        var asked = Assert.Single(dialogs.AssertRules().Asked);
        Assert.Equal(("Mit Korrektur zurücknehmen", "Ohne Korrektur"), (asked.ConfirmText, asked.AlternativeText));
        Assert.True(BodyOf<RestoreAppointmentRequest>(api, "/restore").SendCorrection);
    }

    [Fact]
    public async Task Abbrechen_in_der_Rueckfrage_laesst_den_Termin_storniert()
    {
        var id = Guid.NewGuid();
        var api = Api(Entry(ActivityKind.Cancelled, "Storniert von Rita", id))
            .On($"GET /api/appointments/{id}", CancelledAppointment(id))
            .On($"GET /api/appointments/{id}/restoration", new AppointmentRestoration(CancellationNoticeState.None, false));
        var activity = await OpenAsync(api, dialogs: new FakeDialogs().Answer(ConfirmChoice.Cancelled));
        Open(activity);

        await activity.RestoreCommand.ExecuteAsync(null);

        Assert.DoesNotContain(api.Calls, c => c.Route.EndsWith("/restore", StringComparison.Ordinal));
        Assert.Null(activity.Toast);
        Assert.True(activity.HasPanel);
    }

    private static readonly BookingRejected OccupiedByAnna = new("Zu dieser Zeit ist Anna schon belegt.",
        [new BookingConflict(ResourceKind.Employee, TestData.Anna.Id, "Anna", "ist zu dieser Zeit schon belegt", TestData.At(14), TestData.At(15), Guid.NewGuid())]);

    private static StubApi CancelledApi(Guid id, AppointmentRestoration restoration) => Api(Entry(ActivityKind.Cancelled, "Storniert von Rita", id))
        .On($"GET /api/appointments/{id}", CancelledAppointment(id))
        .On($"GET /api/appointments/{id}/restoration", restoration);

    [Fact]
    public async Task Antwortet_der_Server_mit_409_fragt_die_zweite_Rueckfrage_nach_einer_neuen_Zeit_und_meldet_den_Wunsch()
    {
        var id = Guid.NewGuid();
        var api = CancelledApi(id, new AppointmentRestoration(CancellationNoticeState.None, false))
            .On($"POST /api/appointments/{id}/restore", OccupiedByAnna, HttpStatusCode.Conflict);
        var dialogs = new FakeDialogs().Answer(ConfirmChoice.Confirmed).Answer(ConfirmChoice.Confirmed);
        var activity = await OpenAsync(api, dialogs: dialogs);
        NewTimeRequest? wanted = null;
        activity.NewTimeRequested = request =>
        {
            wanted = request;
            return Task.CompletedTask;
        };
        Open(activity);
        var loads = api.Calls.Count(c => c.Route == HistoryRoute);

        await activity.RestoreCommand.ExecuteAsync(null);

        Assert.Equal(["Stornierung zurücknehmen?", "Zu anderer Zeit zurücknehmen?"], dialogs.AssertRules().Asked.Select(a => a.Title));
        Assert.Equal(CancelledAppointment(id), wanted?.Appointment);
        Assert.Equal(OccupiedByAnna.Conflicts, wanted?.Conflict?.Conflicts);
        Assert.Null(activity.PanelError);
        Assert.Null(activity.Toast);
        Assert.Equal(loads, api.Calls.Count(c => c.Route == HistoryRoute));
    }

    [Fact]
    public async Task Liegt_der_Termin_in_der_Vergangenheit_fragt_die_Rueckfrage_nach_einer_neuen_Zeit_und_Neue_Zeit_waehlen_meldet_den_Wunsch_ohne_restore()
    {
        var id = Guid.NewGuid();
        var api = CancelledApi(id, new AppointmentRestoration(CancellationNoticeState.None, false, InPast: true));
        var dialogs = new FakeDialogs().Answer(ConfirmChoice.Confirmed);
        var activity = await OpenAsync(api, dialogs: dialogs);
        NewTimeRequest? wanted = null;
        activity.NewTimeRequested = request =>
        {
            wanted = request;
            return Task.CompletedTask;
        };
        Open(activity);

        await activity.RestoreCommand.ExecuteAsync(null);

        var asked = Assert.Single(dialogs.AssertRules().Asked);
        Assert.Equal(("Zu neuer Zeit zurücknehmen?", "Neue Zeit wählen"), (asked.Title, asked.ConfirmText));
        Assert.False(asked.IsDestructive);
        Assert.Contains("Der Termin liegt in der Vergangenheit.", asked.Message, StringComparison.Ordinal);
        Assert.Contains("nur zu einer neuen Zeit zurücknehmen", asked.Message, StringComparison.Ordinal);
        Assert.Equal(CancelledAppointment(id), wanted?.Appointment);
        Assert.Null(wanted?.Conflict);
        Assert.DoesNotContain(api.Calls, c => c.Route.EndsWith("/restore", StringComparison.Ordinal));
        Assert.Null(activity.Toast);
        Assert.True(activity.HasPanel);
    }

    [Fact]
    public async Task Ist_die_Zeit_belegt_nennt_die_Rueckfrage_den_Grund_und_der_Wunsch_traegt_die_Belegung()
    {
        var id = Guid.NewGuid();
        var api = CancelledApi(id, new AppointmentRestoration(CancellationNoticeState.None, false, Conflict: OccupiedByAnna));
        var dialogs = new FakeDialogs().Answer(ConfirmChoice.Confirmed);
        var activity = await OpenAsync(api, dialogs: dialogs);
        NewTimeRequest? wanted = null;
        activity.NewTimeRequested = request =>
        {
            wanted = request;
            return Task.CompletedTask;
        };
        Open(activity);

        await activity.RestoreCommand.ExecuteAsync(null);

        var asked = Assert.Single(dialogs.AssertRules().Asked);
        Assert.Equal("Zu neuer Zeit zurücknehmen?", asked.Title);
        Assert.Contains("Die Zeit ist inzwischen belegt: Termin · 14:00–15:00 · Anna", asked.Message, StringComparison.Ordinal);
        Assert.Equal(OccupiedByAnna.Conflicts, wanted?.Conflict?.Conflicts);
        Assert.DoesNotContain(api.Calls, c => c.Route.EndsWith("/restore", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Ist_die_Absage_schon_beim_Gast_sagt_die_Rueckfrage_zur_neuen_Zeit_es_und_der_Wunsch_sendet_die_Korrektur_mit()
    {
        var id = Guid.NewGuid();
        var api = CancelledApi(id, new AppointmentRestoration(CancellationNoticeState.Sent, true, InPast: true));
        var dialogs = new FakeDialogs().Answer(ConfirmChoice.Confirmed);
        var activity = await OpenAsync(api, dialogs: dialogs);
        NewTimeRequest? wanted = null;
        activity.NewTimeRequested = request =>
        {
            wanted = request;
            return Task.CompletedTask;
        };
        Open(activity);

        await activity.RestoreCommand.ExecuteAsync(null);

        var asked = Assert.Single(dialogs.AssertRules().Asked);
        Assert.Contains("Der Gast hat die Absage schon bekommen und erhält mit der neuen Zeit eine Korrektur.", asked.Message, StringComparison.Ordinal);
        Assert.Contains("Die Anzahlung ging mit der Absage zurück", asked.Message, StringComparison.Ordinal);
        Assert.True(wanted?.SendCorrection);
    }

    [Fact]
    public async Task Abbrechen_in_der_Rueckfrage_zur_neuen_Zeit_laesst_alles_stehen()
    {
        var id = Guid.NewGuid();
        var api = CancelledApi(id, new AppointmentRestoration(CancellationNoticeState.None, false, InPast: true));
        var activity = await OpenAsync(api, dialogs: new FakeDialogs().Answer(ConfirmChoice.Cancelled));
        var wanted = false;
        activity.NewTimeRequested = _ =>
        {
            wanted = true;
            return Task.CompletedTask;
        };
        Open(activity);
        var loads = api.Calls.Count(c => c.Route == HistoryRoute);

        await activity.RestoreCommand.ExecuteAsync(null);

        Assert.False(wanted);
        Assert.DoesNotContain(api.Calls, c => c.Route.EndsWith("/restore", StringComparison.Ordinal));
        Assert.Null(activity.Toast);
        Assert.Null(activity.PanelError);
        Assert.True(activity.HasPanel);
        Assert.Equal(loads, api.Calls.Count(c => c.Route == HistoryRoute));
    }

    [Fact]
    public async Task Ein_veralteter_Stand_beim_Zuruecknehmen_zeigt_die_Meldung_des_Servers_als_Fehler_im_Panel()
    {
        var id = Guid.NewGuid();
        var current = CancelledAppointment(id) with { Version = 9 };
        var api = CancelledApi(id, new AppointmentRestoration(CancellationNoticeState.None, false))
            .On($"POST /api/appointments/{id}/restore", new BookingRejected("Der Termin wurde inzwischen geändert.", [], Current: current), HttpStatusCode.Conflict);
        var dialogs = new FakeDialogs().Answer(ConfirmChoice.Confirmed);
        var activity = await OpenAsync(api, dialogs: dialogs);
        Open(activity);

        await activity.RestoreCommand.ExecuteAsync(null);

        Assert.Equal("Der Termin wurde inzwischen geändert.", activity.PanelError);
        Assert.Single(dialogs.Asked);
    }

    [Fact]
    public async Task Ohne_Antwort_der_Pruefung_zeigt_das_Panel_den_Fehler_und_fragt_nicht()
    {
        var id = Guid.NewGuid();
        var api = Api(Entry(ActivityKind.Cancelled, "Storniert von Rita", id)).On($"GET /api/appointments/{id}", CancelledAppointment(id));
        var dialogs = new FakeDialogs().Answer(ConfirmChoice.Confirmed);
        var activity = await OpenAsync(api, dialogs: dialogs);
        Open(activity);

        await activity.RestoreCommand.ExecuteAsync(null);

        Assert.True(activity.HasPanelError);
        Assert.Empty(dialogs.Asked);
    }

    // ---------- Sprünge zu Behandlung, Dienstplan und Zugang ----------

    private static ActivityEntry Admin(ActivityKind kind, Guid? treatment = null, Guid? employee = null) =>
        new(5, Local(8, 40), kind, "Geändert von Katrin", "Katrin", ActivityChannel.Reception, null, null, null, treatment, employee, [], null);

    private static CurrentUser Viewer() => new("u9", "k@aispa.test", "Kim", 1, TestData.Reception().Access, null, [AccessRight.ViewCalendar]);

    [Fact]
    public async Task Behandlung_oeffnen_meldet_die_Behandlung_und_ist_primaer()
    {
        var treatment = Guid.NewGuid();
        var activity = await OpenAsync(Api(Admin(ActivityKind.TreatmentChanged, treatment)));
        Guid? requested = null;
        activity.TreatmentRequested = id => requested = id;
        Open(activity);

        Assert.True(activity.ShowTreatment);
        Assert.True(activity.IsTreatmentPrimary);
        Assert.False(activity.ShowCustomer);
        Assert.False(activity.ShowRoster || activity.ShowAccess);
        activity.OpenTreatmentCommand.Execute(null);

        Assert.Equal(treatment, requested);
    }

    [Fact]
    public async Task Ohne_das_Recht_den_Katalog_zu_verwalten_ist_Behandlung_oeffnen_gesperrt_mit_Hinweis_und_ruft_nichts()
    {
        var activity = await OpenAsync(Api(Admin(ActivityKind.TreatmentChanged, Guid.NewGuid())), Viewer());
        var requested = false;
        activity.TreatmentRequested = _ => requested = true;
        Open(activity);

        activity.OpenTreatmentCommand.Execute(null);

        Assert.False(activity.CanOpenTreatment);
        Assert.Equal("Dafür braucht der Zugang das Recht, Behandlungen zu verwalten.", activity.TreatmentHint);
        Assert.False(requested);
    }

    [Fact]
    public async Task Dienstplan_oeffnen_meldet_den_Tag_des_Eintrags_und_die_Person()
    {
        var employee = Guid.NewGuid();
        var activity = await OpenAsync(Api(Admin(ActivityKind.RosterChanged, employee: employee)));
        (DateOnly, Guid?)? requested = null;
        activity.RosterRequested = (day, person) => requested = (day, person);
        Open(activity);

        Assert.True(activity.ShowRoster);
        Assert.True(activity.IsRosterPrimary);
        activity.OpenRosterCommand.Execute(null);

        Assert.Equal((new DateOnly(2027, 3, 1), (Guid?)employee), requested);
    }

    [Fact]
    public async Task Zugang_oeffnen_meldet_die_Person()
    {
        var employee = Guid.NewGuid();
        var activity = await OpenAsync(Api(Admin(ActivityKind.AccessChanged, employee: employee)));
        Guid? requested = null;
        activity.AccessRequested = id => requested = id;
        Open(activity);

        Assert.True(activity.ShowAccess);
        Assert.True(activity.IsAccessPrimary);
        activity.OpenAccessCommand.Execute(null);

        Assert.Equal(employee, requested);
    }

    [Fact]
    public async Task Ohne_das_Recht_das_Team_zu_verwalten_sind_Dienstplan_und_Zugang_gesperrt_mit_Hinweis()
    {
        var activity = await OpenAsync(Api(Admin(ActivityKind.RosterChanged, employee: Guid.NewGuid())), Viewer());
        var requested = false;
        activity.RosterRequested = (_, _) => requested = true;
        Open(activity);

        activity.OpenRosterCommand.Execute(null);

        Assert.False(activity.CanOpenTeam);
        Assert.Equal("Dafür braucht der Zugang das Recht, das Team zu verwalten.", activity.TeamHint);
        Assert.False(requested);
    }

    [Fact]
    public async Task Eine_Firmenaenderung_hat_keinen_Sprung()
    {
        var activity = await OpenAsync(Api(Admin(ActivityKind.CompanyChanged)));

        Open(activity);

        Assert.False(activity.ShowTreatment || activity.ShowRoster || activity.ShowAccess || activity.ShowCustomer || activity.ShowAppointment);
    }

    [Fact]
    public async Task Ohne_das_Recht_zu_buchen_ist_Zuruecknehmen_gesperrt_mit_Hinweis_und_ruft_nichts()
    {
        var id = Guid.NewGuid();
        var viewer = new CurrentUser("u9", "k@aispa.test", "Kim", 1, TestData.Reception().Access, null, [AccessRight.ViewCalendar]);
        var api = Api(Entry(ActivityKind.Cancelled, "Storniert von Rita", id));
        var activity = await OpenAsync(api, viewer, new FakeDialogs().Answer(ConfirmChoice.Confirmed));
        Open(activity);

        await activity.RestoreCommand.ExecuteAsync(null);

        Assert.False(activity.CanRestore);
        Assert.Equal("Dafür braucht der Zugang das Recht, Termine zu buchen.", activity.RestoreHint);
        Assert.DoesNotContain(api.Calls, c => c.Route.Contains("/restor", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Der_Toast_geht_nach_fuenf_Sekunden_und_laesst_sich_schliessen()
    {
        var id = Guid.NewGuid();
        var cancelled = CancelledAppointment(id);
        var time = new ActivityTests.TimerTime(Local(10));
        var api = Api(Entry(ActivityKind.Cancelled, "Storniert von Rita", id))
            .On($"GET /api/appointments/{id}", cancelled)
            .On($"GET /api/appointments/{id}/restoration", new AppointmentRestoration(CancellationNoticeState.None, false))
            .On($"POST /api/appointments/{id}/restore", cancelled with { Status = AppointmentStatus.Booked });
        var activity = await OpenAsync(api, dialogs: new FakeDialogs().Answer(ConfirmChoice.Confirmed), time: time);
        Open(activity);
        await activity.RestoreCommand.ExecuteAsync(null);
        Assert.True(activity.HasToast);

        time.Advance(TimeSpan.FromSeconds(4));
        Assert.True(activity.HasToast);
        time.Advance(TimeSpan.FromSeconds(1));

        await WaitUntilAsync(() => !activity.HasToast);
    }

    [Fact]
    public async Task ShowDayAsync_zeigt_genau_diesen_Tag_und_nur_die_Stornos()
    {
        var api = Api()
            .On($"GET /api/locations/{TestData.LocationId}/history?from=2027-02-26&until=2027-02-26&kinds=Cancelled&kinds=Restored",
                new ActivityPage([Entry(ActivityKind.Cancelled, "Storniert von Rita")], null));
        var activity = await OpenAsync(api);
        activity.Query = "Huber";

        await activity.ShowDayAsync(new DateOnly(2027, 2, 26), ActivityKind.Cancelled);

        Assert.Equal((ActivityPeriod.Custom, new DateOnly(2027, 2, 26), new DateOnly(2027, 2, 26)), (activity.Period, activity.CustomFrom, activity.CustomUntil));
        Assert.Equal("", activity.Query);
        Assert.Equal(["Stornos"], activity.KindChips.Where(c => c.IsOn).Select(c => c.Label));
        Assert.Equal(2, activity.Rows.Count);
    }

    [Fact]
    public async Task Die_Shell_oeffnet_den_Beleg_in_der_Kassa_auf_dem_Reiter_Belege_am_Tag_des_Belegs()
    {
        // 1. März 10:00 in Wien; der Beleg ist vom 27. Februar: drei Tage zurück.
        var api = Api().On("GET /api/pos/registers", new List<CashRegisterDto>());
        var shell = new ShellViewModel(api.Client(), TestData.Owner(), null, new ActivityTests.TimerTime(Local(10)), new ThemeController(new MemorySettingsStore()), "Test", () => Task.CompletedTask);
        await shell.Calendar.InitializeAsync();

        await shell.ShowReceiptAsync(Guid.NewGuid(), new DateTimeOffset(2027, 2, 27, 18, 0, 0, TimeSpan.FromHours(1)));

        Assert.Same(shell.Pos, shell.SelectedPage?.Page);
        Assert.Equal((PosMode.Receipts, 2), (shell.Pos.Mode, shell.Pos.ReceiptDaysBack));
    }

    [Fact]
    public async Task Die_Shell_oeffnet_einen_stornierten_Termin_im_Kalender_mit_seinem_Panel()
    {
        var cancelled = CancelledAppointment(Guid.NewGuid()) with { Start = TestData.At(14), End = TestData.At(14, 50), BlockedFrom = TestData.At(14), BlockedUntil = TestData.At(15) };
        var api = Api()
            .On("GET /api/locations", new List<LocationDto> { TestData.Location })
            .On($"GET /api/locations/{TestData.LocationId}/resources", TestData.Resources)
            .On($"GET /api/locations/{TestData.LocationId}/calendar?date=2027-03-01", TestData.Day(cancelled))
            .On("GET /api/pos/registers", new List<CashRegisterDto>());
        var shell = new ShellViewModel(api.Client(), TestData.Owner(), null, new ActivityTests.TimerTime(Local(10)), new ThemeController(new MemorySettingsStore()), "Test", () => Task.CompletedTask);
        await shell.Calendar.InitializeAsync();
        await shell.Calendar.LoadAsync();

        shell.ShowCancelledAppointment(cancelled.Id, cancelled.Start);

        Assert.Same(shell.Calendar, shell.SelectedPage?.Page);
        Assert.Equal(cancelled.Id, shell.Calendar.Selected?.Appointment.Id);
        Assert.True(shell.Calendar.Selected?.ShowRestore);
        Assert.NotNull(shell.Activity.CancelledAppointmentRequested);
        Assert.NotNull(shell.Activity.NewTimeRequested);
        Assert.NotNull(shell.Activity.TreatmentRequested);
        Assert.NotNull(shell.Activity.RosterRequested);
        Assert.NotNull(shell.Activity.AccessRequested);
    }
}
