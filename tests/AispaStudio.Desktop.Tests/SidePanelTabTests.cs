using AispaStudio.Contracts;
using AispaStudio.Desktop.ViewModels;
using Avalonia.Input;

namespace AispaStudio.Desktop.Tests;

/// <summary>Schritt 15: Reiter am unteren Rand der Seitenleiste (Design-Brief 5.12, Punkt 7) und Zeitbalken im Termin-Panel (Punkt 5).</summary>
public sealed class SidePanelTabTests
{
    private static readonly TimeZoneInfo Vienna = TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna");

    private sealed class FixedTime : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => TestData.At(10);

        public override TimeZoneInfo LocalTimeZone => Vienna;
    }

    private static async Task<CalendarViewModel> OpenAsync(params AppointmentDto[] appointments)
    {
        var api = new StubApi()
            .On("GET /api/locations", new List<LocationDto> { TestData.Location })
            .On($"GET /api/locations/{TestData.LocationId}/resources", TestData.Resources)
            .On($"GET /api/locations/{TestData.LocationId}/calendar?date=2027-03-01", TestData.Day(appointments));
        var vm = new CalendarViewModel(api.Client(), TestData.Reception(), null, new FixedTime());
        await vm.InitializeAsync();
        await vm.LoadAsync();
        return vm;
    }

    private static string[] Texts(CalendarViewModel vm) => [.. vm.OpenPanels.Select(t => t.Text)];

    // ---------- Reiter ----------

    [Fact]
    public async Task Zwei_Seiten_ergeben_zwei_Reiter_das_Kreuz_schliesst_nur_die_gewaehlte()
    {
        var appointment = TestData.Appointment(10, TestData.Anna);
        var vm = await OpenAsync(appointment);

        vm.OpenAppointment(appointment);
        Assert.Equal(["Termin"], Texts(vm));

        vm.OpenWaitlistCommand.Execute(null);
        // Der zuletzt geöffnete Reiter steht ganz links (Ä15).
        Assert.Equal(["Warteliste", "Termin"], Texts(vm));
        Assert.Equal([true, false], vm.OpenPanels.Select(t => t.IsSelected));
        Assert.Equal("Warteliste", vm.ActivePanel!.PanelTitle);

        // Das Kreuz schließt nur die gewählte Seite; danach ist der Termin gewählt und offen.
        vm.ActivePanel.CloseCommand.Execute(null);

        Assert.Equal(["Termin"], Texts(vm));
        Assert.True(vm.OpenPanels[0].IsSelected);
        Assert.Equal(appointment.Id, vm.Selected!.Appointment.Id);
        Assert.Equal("Eva Muster", vm.ActivePanel!.PanelTitle);
        Assert.Null(vm.WaitlistPanel);

        // Die letzte Seite schließt die Leiste.
        vm.ActivePanel.CloseCommand.Execute(null);
        Assert.Empty(vm.OpenPanels);
        Assert.False(vm.HasPanel);
    }

    [Fact]
    public async Task Ein_Klick_auf_einen_Reiter_wechselt_die_Seite_und_laesst_die_andere_als_Reiter_stehen()
    {
        var appointment = TestData.Appointment(10, TestData.Anna);
        var vm = await OpenAsync(appointment);
        vm.OpenAppointment(appointment);
        vm.OpenWaitlistCommand.Execute(null);

        vm.SelectPanelTabCommand.Execute(vm.OpenPanels[1]);

        // Ein Klick wählt nur; die Reihenfolge bleibt.
        Assert.Equal(["Warteliste", "Termin"], Texts(vm));
        Assert.Equal([false, true], vm.OpenPanels.Select(t => t.IsSelected));
        Assert.Equal(appointment.Id, vm.Selected!.Appointment.Id);
        Assert.Null(vm.WaitlistPanel);
    }

    [Fact]
    public async Task Dieselbe_Art_von_Seite_ersetzt_ihren_Reiter_statt_einen_neuen_anzulegen()
    {
        var first = TestData.Appointment(10, TestData.Anna);
        var second = TestData.Appointment(14, TestData.Anna);
        var vm = await OpenAsync(first, second);

        vm.OpenAppointment(first);
        vm.OpenAppointment(second);

        Assert.Equal(["Termin"], Texts(vm));
        Assert.Equal(second.Id, vm.Selected!.Appointment.Id);
    }

    [Fact]
    public async Task Die_siebte_Seite_ersetzt_den_aeltesten_nicht_gewaehlten_Reiter()
    {
        var appointment = TestData.Appointment(10, TestData.Anna);
        var vm = await OpenAsync(appointment);

        vm.OpenAppointment(appointment);
        vm.OpenWaitlistCommand.Execute(null);
        Assert.True(vm.HandleKey(Key.N, KeyModifiers.Control));
        Assert.True(vm.HandleKey(Key.K, KeyModifiers.Control));
        vm.StartBooking(vm.Columns[0], 100);
        vm.OpenMessagesCommand.Execute(null);
        Assert.Equal(["Meldungen", "Neuer Termin", "Wunsch", "Freie Zeit", "Warteliste", "Termin"], Texts(vm));

        vm.OpenAbsence(vm.Columns.Single(c => c.Id == TestData.Ben.Id));

        // Sechs sind das Höchste: der Termin (der älteste, nicht gewählte) fällt weg, die neue Seite ist gewählt.
        Assert.Equal(["Ausfall", "Meldungen", "Neuer Termin", "Wunsch", "Freie Zeit", "Warteliste"], Texts(vm));
        Assert.Equal("Ausfall", vm.OpenPanels.Single(t => t.IsSelected).Text);
        Assert.Null(vm.Selected);
    }

    [Fact]
    public async Task Strg_Tab_wechselt_nach_rechts_und_Strg_Umschalt_Tab_nach_links()
    {
        var appointment = TestData.Appointment(10, TestData.Anna);
        var vm = await OpenAsync(appointment);
        vm.OpenAppointment(appointment);
        vm.OpenWaitlistCommand.Execute(null);
        Assert.True(vm.HandleKey(Key.N, KeyModifiers.Control));
        string Shown() => vm.OpenPanels.Single(t => t.IsSelected).Text;
        Assert.Equal("Freie Zeit", Shown());

        // Die Reihe ist Freie Zeit, Warteliste, Termin; nach rechts über das Ende hinaus beginnt wieder vorn.
        Assert.True(vm.HandleKey(Key.Tab, KeyModifiers.Control));
        Assert.Equal("Warteliste", Shown());
        Assert.True(vm.HandleKey(Key.Tab, KeyModifiers.Control));
        Assert.Equal("Termin", Shown());
        Assert.NotNull(vm.Selected);
        Assert.True(vm.HandleKey(Key.Tab, KeyModifiers.Control));
        Assert.Equal("Freie Zeit", Shown());
        Assert.NotNull(vm.FreeTime);
        Assert.True(vm.HandleKey(Key.Tab, KeyModifiers.Control | KeyModifiers.Shift));
        Assert.Equal("Termin", Shown());
        Assert.True(vm.HandleKey(Key.Tab, KeyModifiers.Control | KeyModifiers.Shift));
        Assert.Equal("Warteliste", Shown());
    }

    [Fact]
    public async Task Strg_Tab_ohne_offene_Seite_tut_nichts()
    {
        var vm = await OpenAsync();

        Assert.False(vm.HandleKey(Key.Tab, KeyModifiers.Control));
    }

    [Fact]
    public async Task Esc_schliesst_die_gewaehlte_Seite_und_waehlt_den_naechsten_Reiter()
    {
        var appointment = TestData.Appointment(10, TestData.Anna);
        var vm = await OpenAsync(appointment);
        vm.OpenAppointment(appointment);
        vm.OpenWaitlistCommand.Execute(null);

        Assert.True(vm.HandleKey(Key.Escape, KeyModifiers.None));

        Assert.Equal(["Termin"], Texts(vm));
        Assert.Equal("Eva Muster", vm.ActivePanel!.PanelTitle);
    }

    [Fact]
    public async Task Alle_Leisten_schliessen_nimmt_auch_die_Reiter_weg()
    {
        var appointment = TestData.Appointment(10, TestData.Anna);
        var vm = await OpenAsync(appointment);
        vm.OpenAppointment(appointment);
        vm.OpenWaitlistCommand.Execute(null);

        vm.CloseAllPanels();

        Assert.Empty(vm.OpenPanels);
        Assert.Null(vm.ActivePanel);
    }

    // ---------- Sichtbare Reiter, Klappliste und Pfeile (Ä15, Ä29) ----------

    private static string[] Visible(CalendarViewModel vm) => [.. vm.VisibleTabs.Select(t => t.Text)];

    /// <summary>
    /// Öffnet nacheinander bis zu sechs Seiten. Die Reihe ist danach (zuletzt geöffnet links) Meldungen, Neuer Termin, Wunsch, Freie Zeit, Warteliste, Termin,
    /// gewählt ist die zuletzt geöffnete.
    /// </summary>
    private static async Task<CalendarViewModel> WithPanelsAsync(int count)
    {
        var appointment = TestData.Appointment(10, TestData.Anna);
        var vm = await OpenAsync(appointment);
        Action[] open =
        [
            () => vm.OpenAppointment(appointment),
            () => vm.OpenWaitlistCommand.Execute(null),
            () => Assert.True(vm.HandleKey(Key.N, KeyModifiers.Control)),
            () => Assert.True(vm.HandleKey(Key.K, KeyModifiers.Control)),
            () => vm.StartBooking(vm.Columns[0], 100),
            () => vm.OpenMessagesCommand.Execute(null),
        ];
        foreach (var step in open.Take(count))
        {
            step();
        }

        return vm;
    }

    [Fact]
    public async Task Vier_offene_Seiten_zeigen_vier_Symbol_Reiter_ohne_Chip_und_Pfeile()
    {
        var vm = await WithPanelsAsync(4);

        // Ab drei Seiten reicht der Platz nicht mehr für Text (Ä19); dann stehen bis zu fünf Reiter nebeneinander (Ä29).
        Assert.True(vm.TabsCompact);
        Assert.Equal(["Wunsch", "Freie Zeit", "Warteliste", "Termin"], Visible(vm));
        Assert.False(vm.HasTabOverflow);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(5)]
    public async Task Bis_zu_fuenf_Seiten_zeigen_alle_Reiter_nur_mit_Symbol_ohne_Chip(int pages)
    {
        var vm = await WithPanelsAsync(pages);

        Assert.True(vm.TabsCompact);
        Assert.Equal(pages, vm.VisibleTabs.Count);
        Assert.Equal(vm.OpenPanels, vm.VisibleTabs);
        Assert.False(vm.HasTabOverflow);
        Assert.Equal("⋯ 0", vm.TabOverflowText);
    }

    [Fact]
    public async Task Die_sechste_Seite_bringt_den_Chip_mit_der_Zahl_der_uebrigen()
    {
        var vm = await WithPanelsAsync(6);

        // Fünf Reiter sind sichtbar, der älteste (Termin) steht nur in der Klappliste.
        Assert.Equal(["Meldungen", "Neuer Termin", "Wunsch", "Freie Zeit", "Warteliste"], Visible(vm));
        Assert.True(vm.HasTabOverflow);
        Assert.Equal("⋯ 1", vm.TabOverflowText);
    }

    [Fact]
    public async Task Die_Obergrenzen_sind_zwei_mit_Text_und_fuenf_nur_mit_Symbol()
    {
        var vm = await WithPanelsAsync(3);

        // 360 breit: Schiene 291; mit Chip und Pfeilen blieben 88,5 je Reiter mit Text, unter 96, also nur Symbole.
        Assert.Equal(88.5, vm.TabWidth);
        Assert.True(vm.TabsCompact);
        Assert.Equal(CalendarViewModel.MaxIconTabs, vm.VisibleTabLimit);
        Assert.Equal(5, CalendarViewModel.MaxIconTabs);
        Assert.Equal(2, CalendarViewModel.MaxVisibleTabs);
    }

    [Fact]
    public async Task Bis_zu_zwei_Seiten_zeigen_alle_Reiter_ohne_Chip_und_Pfeile()
    {
        var appointment = TestData.Appointment(10, TestData.Anna);
        var vm = await OpenAsync(appointment);
        vm.OpenAppointment(appointment);
        vm.OpenWaitlistCommand.Execute(null);

        Assert.Equal(["Warteliste", "Termin"], Visible(vm));
        Assert.False(vm.HasTabOverflow);
        Assert.False(vm.TabsCompact);
    }

    [Fact]
    public async Task Die_Klappliste_waehlt_einen_verborgenen_Reiter_ohne_die_Reihenfolge_zu_aendern()
    {
        var vm = await WithPanelsAsync(6);

        vm.SelectPanelTabCommand.Execute(vm.OpenPanels.Single(t => t.Text == "Termin"));

        Assert.Equal(["Meldungen", "Neuer Termin", "Wunsch", "Freie Zeit", "Warteliste", "Termin"], Texts(vm));
        Assert.Equal("Termin", vm.OpenPanels.Single(t => t.IsSelected).Text);
        Assert.NotNull(vm.Selected);
        // Der gewählte und der davor benutzte (Meldungen) sind sichtbar, die übrigen füllen die Schiene bis fünf auf; der Chip zählt die eine verborgene.
        Assert.Equal(["Meldungen", "Neuer Termin", "Wunsch", "Freie Zeit", "Termin"], Visible(vm));
        Assert.Equal("⋯ 1", vm.TabOverflowText);
    }

    [Fact]
    public async Task Die_Pfeile_blaettern_durch_die_Reihe_und_beginnen_am_Ende_von_vorn()
    {
        var vm = await WithPanelsAsync(4);
        string Shown() => vm.OpenPanels.Single(t => t.IsSelected).Text;

        vm.NextPanelTabCommand.Execute(null);
        Assert.Equal("Freie Zeit", Shown());
        vm.NextPanelTabCommand.Execute(null);
        Assert.Equal("Warteliste", Shown());
        Assert.Contains("Warteliste", Visible(vm));
        vm.PreviousPanelTabCommand.Execute(null);
        Assert.Equal("Freie Zeit", Shown());
        vm.PreviousPanelTabCommand.Execute(null);
        vm.PreviousPanelTabCommand.Execute(null);
        Assert.Equal("Termin", Shown());
        Assert.Equal(["Wunsch", "Freie Zeit", "Warteliste", "Termin"], Texts(vm));
    }

    [Fact]
    public async Task Nach_dem_Schliessen_der_gewaehlten_Seite_stehen_fuenf_Reiter_ohne_Chip()
    {
        var vm = await WithPanelsAsync(6);

        vm.ActivePanel!.CloseCommand.Execute(null);

        Assert.Equal(["Neuer Termin", "Wunsch", "Freie Zeit", "Warteliste", "Termin"], Texts(vm));
        Assert.Equal(5, vm.VisibleTabs.Count);
        Assert.False(vm.HasTabOverflow);
        Assert.Equal("⋯ 0", vm.TabOverflowText);
    }

    // ---------- Zeitbalken ----------

    private static AppointmentDto Timed(int preparation, int treatment, int followUp, int buffer = 0, int handsOn = 0, int processing = 0)
    {
        var start = TestData.At(10);
        return TestData.Appointment(10, TestData.Anna) with
        {
            End = start.AddMinutes(treatment),
            PreparationMinutes = preparation,
            FollowUpMinutes = followUp,
            ExtraFollowUpMinutes = buffer,
            HandsOnMinutes = handsOn,
            ProcessingMinutes = processing,
            BlockedFrom = start.AddMinutes(-preparation),
            BlockedUntil = start.AddMinutes(treatment + followUp + buffer),
        };
    }

    private static async Task<AppointmentViewModel> PanelOfAsync(AppointmentDto appointment, bool byRoom = false)
    {
        var vm = await OpenAsync(appointment);
        vm.ByRoom = byRoom;
        vm.OpenAppointment(appointment);
        return vm.Selected!;
    }

    [Fact]
    public async Task Zeitbalken_mit_10_45_10_Minuten_hat_drei_Abschnitte_mit_ihren_Anteilen()
    {
        var panel = await PanelOfAsync(Timed(10, 45, 10));

        Assert.Equal([TimeBarKind.Preparation, TimeBarKind.Treatment, TimeBarKind.FollowUp], panel.TimeBar.Select(s => s.Kind));
        Assert.Equal([10, 45, 10], panel.TimeBar.Select(s => s.Minutes));
        Assert.Equal([10 / 65.0, 45 / 65.0, 10 / 65.0], panel.TimeBar.Select(s => s.Fraction));
        Assert.Equal(1.0, panel.TimeBar.Sum(s => s.Fraction), 6);
        Assert.Equal(["Vorbereitung", "Behandlung", "Nachbereitung"], panel.TimeBar.Select(s => s.Label));
        Assert.Equal("09:50", panel.TimeBarFrom);
        Assert.Equal("10:55", panel.TimeBarUntil);
    }

    [Fact]
    public async Task Zeitbalken_teilt_die_Behandlung_mit_Einwirkzeit_in_Haende_Einwirken_und_Ausarbeiten()
    {
        var panel = await PanelOfAsync(Timed(10, 45, 10, handsOn: 15, processing: 15));

        Assert.Equal([TimeBarKind.Preparation, TimeBarKind.HandsOn, TimeBarKind.Processing, TimeBarKind.Finishing, TimeBarKind.FollowUp],
            panel.TimeBar.Select(s => s.Kind));
        Assert.Equal([10, 15, 15, 15, 10], panel.TimeBar.Select(s => s.Minutes));
        Assert.Equal(["Vorbereitung", "Hände", "Einwirken", "Ausarbeiten", "Nachbereitung"], panel.TimeBar.Select(s => s.Label));
    }

    [Fact]
    public async Task Zeitbalken_nennt_den_Gast_Puffer_als_vierten_Abschnitt()
    {
        var panel = await PanelOfAsync(Timed(10, 45, 10, buffer: 10));

        Assert.Equal([TimeBarKind.Preparation, TimeBarKind.Treatment, TimeBarKind.FollowUp, TimeBarKind.Buffer], panel.TimeBar.Select(s => s.Kind));
        Assert.Equal([10, 45, 10, 10], panel.TimeBar.Select(s => s.Minutes));
        Assert.Equal("11:05", panel.TimeBarUntil);
    }

    [Fact]
    public async Task Zeitbalken_laesst_Abschnitte_mit_null_Minuten_weg_und_zeigt_in_der_Raumansicht_die_Raumpflege()
    {
        var plain = await PanelOfAsync(Timed(0, 50, 0));
        var byRoom = await PanelOfAsync(Timed(0, 50, 0), byRoom: true);

        Assert.Equal([TimeBarKind.Treatment], plain.TimeBar.Select(s => s.Kind));
        Assert.Equal([TimeBarKind.Treatment, TimeBarKind.RoomCare], byRoom.TimeBar.Select(s => s.Kind));
        Assert.Equal(TestData.Room.CleaningMinutes, byRoom.TimeBar[1].Minutes);
        Assert.Equal("Raumpflege", byRoom.TimeBar[1].Label);
    }

    [Fact]
    public async Task Nur_Vor_und_Nachbereitung_lassen_sich_im_Balken_anklicken_und_nur_mit_dem_Recht()
    {
        var panel = await PanelOfAsync(Timed(10, 45, 10, buffer: 5));

        Assert.Equal([true, false, true, false], panel.TimeBar.Select(s => s.IsEditable));
        Assert.True(panel.CanChangeTimes);
    }
}
