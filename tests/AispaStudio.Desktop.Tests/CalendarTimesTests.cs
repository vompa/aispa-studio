using System.Net;
using System.Text.Json;
using AispaStudio.Contracts;
using AispaStudio.Desktop.ViewModels;
using Avalonia.Input;

namespace AispaStudio.Desktop.Tests;

/// <summary>Schritt 12: Zeiten je Termin und Gast-Puffer getrennt in Streifen, Hover-Karte und Panel; Ändern im Panel.</summary>
public sealed class CalendarTimesTests
{
    private static readonly TimeZoneInfo Vienna = TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna");

    private sealed class FixedTime : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => TestData.At(10);

        public override TimeZoneInfo LocalTimeZone => Vienna;
    }

    /// <summary>Massage 10:00–10:50: 5 Minuten Vorbereitung, 10 Nachbereitung und 10 Minuten Gast-Puffer (gebunden bis 11:10).</summary>
    private static AppointmentDto WithBuffer() => TestData.Appointment(10, TestData.Anna) with
    {
        PreparationMinutes = 5,
        FollowUpMinutes = 10,
        ExtraFollowUpMinutes = 10,
        BlockedUntil = TestData.At(11, 10),
    };

    private static StubApi Api(AppointmentDto appointment) => new StubApi()
        .On("GET /api/locations", new List<LocationDto> { TestData.Location })
        .On($"GET /api/locations/{TestData.LocationId}/resources", TestData.Resources)
        .On($"GET /api/locations/{TestData.LocationId}/calendar?date=2027-03-01", TestData.Day(appointment));

    private static async Task<CalendarViewModel> OpenAsync(StubApi api, CurrentUser? user = null)
    {
        var vm = new CalendarViewModel(api.Client(), user ?? TestData.Reception(), null, new FixedTime());
        await vm.InitializeAsync();
        await vm.LoadAsync();
        return vm;
    }

    private static CalendarColumn Team(AppointmentDto appointment) =>
        DayLayout.Build(TestData.Day(appointment), TestData.Resources, byRoom: false, Vienna).Columns.Single(c => c.Id == TestData.Anna.Id);

    [Fact]
    public void Streifen_nennt_Nachbereitung_und_Puffer_getrennt()
    {
        var stripes = Team(WithBuffer()).Stripes;

        Assert.Equal(["Vorb. 5 Min.", "Nachb. 10 · Puffer 10 Min."], stripes.Select(s => s.Text));
        Assert.Equal("Nachbereitung 10 Min. · Puffer 10 Min.", stripes[1].Tip);
        Assert.Equal(20 * DayLayout.PixelsPerMinute, stripes[1].Height, 3);
    }

    [Fact]
    public void Hover_Karte_nennt_den_Ablauf_mit_Puffer_und_markiert_geaenderte_Zeiten()
    {
        var plain = Team(WithBuffer()).Items.Single().Hover!;
        var changed = Team(WithBuffer() with { PreparationMinutes = 15, BlockedFrom = TestData.At(9, 45) }).Items.Single().Hover!;

        // Die Raumpflege des Raums (10 Minuten) steht dahinter, wie bisher.
        Assert.Equal("Vorb. 5 · Nachb. 10 · Puffer 10 · Raumpflege 10 Min.", plain.Flow);
        Assert.Equal("Vorb. 15 · Nachb. 10 · Puffer 10 · Raumpflege 10 Min. (geändert)", changed.Flow);
    }

    [Fact]
    public void Alte_Staende_ohne_die_neuen_Felder_rechnen_wie_bisher_aus_der_Bindung()
    {
        var old = TestData.Appointment(10, TestData.Anna);

        Assert.Equal(["Vorb. 5 Min.", "Nachb. 10 Min."], Team(old).Stripes.Select(s => s.Text));
        Assert.Equal((5, 10, 0), DayLayout.TimesOf(old));
    }

    [Fact]
    public async Task Panel_nennt_Puffer_und_sagt_geaendert_wenn_die_Zeiten_von_der_Behandlung_abweichen()
    {
        var plain = WithBuffer();
        var changed = TestData.Appointment(14, TestData.Anna) with
        {
            PreparationMinutes = 15,
            FollowUpMinutes = 20,
            BlockedFrom = TestData.At(13, 45),
            BlockedUntil = TestData.At(15, 10),
        };
        var api = new StubApi()
            .On("GET /api/locations", new List<LocationDto> { TestData.Location })
            .On($"GET /api/locations/{TestData.LocationId}/resources", TestData.Resources)
            .On($"GET /api/locations/{TestData.LocationId}/calendar?date=2027-03-01", TestData.Day(plain, changed));
        var vm = await OpenAsync(api);

        vm.OpenAppointment(plain);
        var first = vm.Selected!.BoundText;
        vm.OpenAppointment(changed);
        var second = vm.Selected!.BoundText;

        Assert.Equal("Gebunden 09:55–11:10 · Vorb. 5 · Nachb. 10 · Puffer 10 Min.", first);
        Assert.Equal("Gebunden 13:45–15:10 · Vorb. 15 · Nachb. 20 Min. (geändert)", second);
    }

    [Fact]
    public async Task Aendern_belegt_die_Felder_vor_und_uebernehmen_sendet_beide_Werte()
    {
        var appointment = WithBuffer();
        var updated = appointment with
        {
            PreparationMinutes = 15,
            FollowUpMinutes = 20,
            BlockedFrom = TestData.At(9, 45),
            BlockedUntil = TestData.At(11, 20),
            UpdatedAt = TestData.At(9),
        };
        var api = Api(appointment).On($"PUT /api/appointments/{appointment.Id}/times", updated);
        var vm = await OpenAsync(api);
        vm.OpenAppointment(appointment);
        var panel = vm.Selected!;

        Assert.True(panel.CanChangeTimes);
        Assert.False(panel.IsEditingTimes);
        panel.BeginEditTimes();
        Assert.True(panel.IsEditingTimes);
        Assert.Equal((5, 10), (panel.TimesPreparation, panel.TimesFollowUp));

        panel.TimesPreparation = 15;
        panel.TimesFollowUp = 20;
        await panel.ApplyTimesCommand.ExecuteAsync(null);

        var call = Assert.Single(api.Calls, c => c.Route.StartsWith("PUT ", StringComparison.Ordinal));
        using var body = JsonDocument.Parse(call.Body!);
        Assert.Equal((15, 20), (body.RootElement.GetProperty("preparationMinutes").GetInt32(), body.RootElement.GetProperty("followUpMinutes").GetInt32()));
        Assert.Equal(["Vorb. 15 Min.", "Nachb. 20 · Puffer 10 Min."], vm.Columns[0].Stripes.Select(s => s.Text));
        Assert.False(vm.Selected!.IsEditingTimes);
    }

    [Fact]
    public async Task Ablehnung_des_Planers_erscheint_als_Meldung_mit_der_Begruendung_und_das_Panel_bleibt_offen()
    {
        var appointment = WithBuffer();
        var rejected = new BookingRejected("Zu dieser Zeit ist nicht alles frei, was die Behandlung braucht.",
            [new BookingConflict(ResourceKind.Employee, TestData.Anna.Id, "Anna", "ist zu dieser Zeit schon belegt")]);
        var api = Api(appointment).On($"PUT /api/appointments/{appointment.Id}/times", rejected, HttpStatusCode.Conflict);
        var vm = await OpenAsync(api);
        vm.OpenAppointment(appointment);
        var panel = vm.Selected!;
        panel.BeginEditTimes();
        panel.TimesPreparation = 60;

        await panel.ApplyTimesCommand.ExecuteAsync(null);

        Assert.Equal("Zeiten nicht geändert", vm.Problem!.Title);
        Assert.Contains("Anna: ist zu dieser Zeit schon belegt", vm.Problem.Message, StringComparison.Ordinal);
        Assert.True(panel.IsEditingTimes);
    }

    [Fact]
    public async Task Abbrechen_und_Esc_beenden_das_Aendern_ohne_Aufruf_und_das_Panel_bleibt()
    {
        var appointment = WithBuffer();
        var api = Api(appointment);
        var vm = await OpenAsync(api);
        vm.OpenAppointment(appointment);
        vm.Selected!.BeginEditTimes();

        Assert.True(vm.HandleKey(Key.Escape, KeyModifiers.None));

        Assert.NotNull(vm.Selected);
        Assert.False(vm.Selected.IsEditingTimes);
        vm.Selected.BeginEditTimes();
        vm.Selected.CancelEditTimes();
        Assert.False(vm.Selected.IsEditingTimes);
        Assert.DoesNotContain(api.Calls, c => c.Route.StartsWith("PUT ", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Aendern_gibt_es_nur_mit_Buchungsrecht_und_bei_gebuchten_oder_eingecheckten_Terminen()
    {
        var done = TestData.Appointment(10, TestData.Anna, AppointmentStatus.Completed);
        var checkedIn = TestData.Appointment(12, TestData.Anna, AppointmentStatus.CheckedIn);
        var vm = await OpenAsync(Api(done));
        var readOnly = TestData.Reception() with { Rights = [AccessRight.ViewCalendar, AccessRight.ChangeStatus] };
        var viewOnly = await OpenAsync(Api(checkedIn), readOnly);

        vm.OpenAppointment(done);
        var completed = vm.Selected!.CanChangeTimes;
        vm.Selected.BeginEditTimes();
        var editingCompleted = vm.Selected.IsEditingTimes;
        vm.OpenAppointment(checkedIn);
        var open = vm.Selected!.CanChangeTimes;
        viewOnly.OpenAppointment(checkedIn);

        Assert.False(completed);
        Assert.False(editingCompleted);
        Assert.True(open);
        Assert.True(vm.Selected.HasMore);
        Assert.False(viewOnly.Selected!.CanChangeTimes);
    }

    [Fact]
    public async Task Rechtsklick_Menue_bietet_Aendern_vor_Bearbeiten_und_oeffnet_das_Panel_mit_den_Feldern()
    {
        var appointment = WithBuffer();
        var vm = await OpenAsync(Api(appointment), TestData.Reception() with { Rights = [.. TestData.Reception().Rights, AccessRight.ManageCatalog] });
        vm.AdminRequested = (_, _, _) => { };
        var item = vm.Columns[0].Items.Single();

        var headers = vm.ActionsFor(item).Select(a => a.Header).ToList();
        var fromStripe = vm.ActionsForStripe(vm.Columns[0].Stripes[0]).Select(a => a.Header).ToList();

        Assert.True(headers.IndexOf("Zeiten dieses Termins ändern …") >= 0);
        Assert.Equal(headers.IndexOf("Zeiten dieses Termins ändern …") + 1, headers.IndexOf("Behandlungszeiten bearbeiten …"));
        Assert.Equal(["Zeiten dieses Termins ändern …", "Behandlungszeiten bearbeiten …"], fromStripe);

        vm.ActionsFor(item).Single(a => a.Header == "Zeiten dieses Termins ändern …").Run!();

        Assert.Equal(appointment.Id, vm.Selected!.Appointment.Id);
        Assert.True(vm.Selected.IsEditingTimes);
        Assert.Equal((5, 10), (vm.Selected.TimesPreparation, vm.Selected.TimesFollowUp));
    }
}
