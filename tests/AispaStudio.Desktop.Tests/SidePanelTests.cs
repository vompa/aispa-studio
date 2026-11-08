using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;
using AispaStudio.Desktop.ViewModels;
using Avalonia.Input;

namespace AispaStudio.Desktop.Tests;

/// <summary>Alle rechten Seitenleisten nach einem Muster (Fluent 2): gleicher Kopf, gleiche Breite, nie zwei zugleich, Zustand je Seite.</summary>
public sealed class SidePanelTests
{
    private static readonly TimeZoneInfo Vienna = TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna");

    private sealed class FixedTime : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => TestData.At(10);

        public override TimeZoneInfo LocalTimeZone => Vienna;
    }

    private static async Task<CalendarViewModel> OpenAsync(AppointmentDto? appointment = null)
    {
        var api = new StubApi()
            .On("GET /api/locations", new List<LocationDto> { TestData.Location })
            .On($"GET /api/locations/{TestData.LocationId}/resources", TestData.Resources)
            .On($"GET /api/locations/{TestData.LocationId}/calendar?date=2027-03-01", appointment is null ? TestData.Day() : TestData.Day(appointment));
        var vm = new CalendarViewModel(api.Client(), TestData.Reception(), null, new FixedTime());
        await vm.InitializeAsync();
        await vm.LoadAsync();
        return vm;
    }

    private static async Task Until(Func<bool> condition)
    {
        for (var i = 0; i < 100 && !condition(); i++)
        {
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        Assert.True(condition());
    }

    [Fact]
    public async Task Jede_Leiste_nennt_Titel_und_Untertitel_fuer_den_gemeinsamen_Kopf()
    {
        var appointment = TestData.Appointment(10, TestData.Anna);
        var vm = await OpenAsync(appointment);
        Assert.Null(vm.ActivePanel);

        vm.OpenAppointment(appointment);
        Assert.Equal(("Eva Muster", "Massage"), (vm.ActivePanel!.PanelTitle, vm.ActivePanel.PanelSubtitle));

        Assert.True(vm.HandleKey(Key.N, KeyModifiers.Control));
        Assert.Equal(("Freie Zeit suchen", "Montag, 1. März 2027"), (vm.ActivePanel!.PanelTitle, vm.ActivePanel.PanelSubtitle));

        Assert.True(vm.HandleKey(Key.K, KeyModifiers.Control));
        Assert.Equal("Wunsch eintippen", vm.ActivePanel!.PanelTitle);

        vm.OpenWaitlistCommand.Execute(null);
        Assert.Equal(("Warteliste", "Wien"), (vm.ActivePanel!.PanelTitle, vm.ActivePanel.PanelSubtitle));

        vm.StartBooking(vm.Columns[0], 100);
        Assert.Equal("Neuer Termin", vm.ActivePanel!.PanelTitle);
        Assert.Equal("bei Anna", vm.ActivePanel.PanelSubtitle);
        // Wechselt die Umschaltleiste die Art, wechseln Titel und Untertitel mit.
        Assert.True(vm.SelectNewEntry(2));
        Assert.Equal("Paar oder Spa-Tag", vm.ActivePanel!.PanelTitle);
        Assert.True(vm.SelectNewEntry(4));
        Assert.Equal("Sperrzeit", vm.ActivePanel!.PanelTitle);
        Assert.StartsWith("Anna · ", vm.ActivePanel.PanelSubtitle, StringComparison.Ordinal);

        vm.CloseAllPanels();
        Assert.Null(vm.ActivePanel);
        Assert.False(vm.HasPanel);
    }

    [Fact]
    public async Task Alle_Leisten_sind_gleich_breit_nur_das_Formular_ist_breiter()
    {
        var appointment = TestData.Appointment(10, TestData.Anna);
        var vm = await OpenAsync(appointment);

        Assert.Equal(360, vm.PanelWidth);
        vm.OpenAppointment(appointment);
        Assert.Equal(360, vm.PanelWidth);
        Assert.True(vm.HandleKey(Key.N, KeyModifiers.Control));
        Assert.Equal(360, vm.PanelWidth);
    }

    [Fact]
    public async Task Die_Leiste_bleibt_nach_dem_Schliessen_kurz_im_Layout_bei_Kurzbefehlen_nicht()
    {
        var vm = await OpenAsync();

        // Mit der Maus: erscheint sofort, klingt nach dem Schließen 120 ms aus.
        vm.OpenWaitlistCommand.Execute(null);
        Assert.True(vm.Presence.IsVisible);
        vm.CloseAllPanels();
        Assert.True(vm.Presence.IsVisible);
        await Until(() => !vm.Presence.IsVisible);

        // Mit Kurzbefehl: ohne Bewegung, sofort weg.
        vm.Presence.UsingKeyboard = true;
        vm.OpenWaitlistCommand.Execute(null);
        Assert.True(vm.Presence.IsVisible);
        vm.CloseAllPanels();
        Assert.False(vm.Presence.IsVisible);
    }

    [Fact]
    public async Task Eine_neue_Leiste_oeffnet_sich_zum_Eingeben_und_meldet_es_dem_Fokus()
    {
        var appointment = TestData.Appointment(10, TestData.Anna);
        var vm = await OpenAsync(appointment);
        var opened = 0;
        vm.PanelOpened += () => opened++;

        // Ein Termin wird nur angesehen: der Fokus bleibt im Kalender, damit die Pfeiltasten weiter wirken.
        vm.OpenAppointment(appointment);
        Assert.Equal(0, opened);

        Assert.True(vm.HandleKey(Key.N, KeyModifiers.Control));
        Assert.Equal(1, opened);
    }

    // ---------- KI und Hilfe als Reiter der Seitenleiste (Ä66) ----------

    private static ShellViewModel Shell(MemorySettingsStore store) =>
        new(new StubApi().Client(), TestData.Reception(), null, TimeProvider.System, new ThemeController(store), "Test", () => Task.CompletedTask);

    [Fact]
    public void KI_und_Hilfe_ist_im_Kalender_ein_Reiter_neben_der_Warteliste()
    {
        var shell = Shell(new MemorySettingsStore());
        shell.Calendar.OpenWaitlistCommand.Execute(null);

        // Der Knopf (und Strg+Leertaste) wählt den Reiter; die Warteliste bleibt als Reiter stehen.
        shell.Companion.ToggleCommand.Execute(null);

        Assert.True(shell.Companion.IsOpen);
        Assert.Same(shell.Companion, shell.Calendar.CompanionPanel);
        Assert.Same(shell.Companion, shell.Calendar.ActivePanel);
        Assert.Null(shell.Calendar.WaitlistPanel);
        Assert.Equal([PanelKind.Assistant, PanelKind.Waitlist], shell.Calendar.OpenPanels.Select(t => t.Kind));
        var tab = shell.Calendar.OpenPanels[0];
        Assert.True(tab.IsSelected);
        Assert.Equal("KI und Hilfe", tab.Text);
        Assert.Equal(AispaStudio.Desktop.Controls.Icons.Logo, tab.Icon);
        Assert.False(shell.Companion.ShellOpen);
    }

    [Fact]
    public void Schliessen_des_Assistenten_laesst_die_Warteliste_offen()
    {
        var shell = Shell(new MemorySettingsStore());
        shell.Calendar.OpenWaitlistCommand.Execute(null);
        shell.Companion.ToggleCommand.Execute(null);

        // Das Kreuz der Seitenleiste, Esc und Strg+Leertaste schließen nur den gewählten Reiter.
        shell.Calendar.RequestClosePanelCommand.Execute(null);

        Assert.False(shell.Companion.IsOpen);
        Assert.Null(shell.Calendar.CompanionPanel);
        Assert.NotNull(shell.Calendar.WaitlistPanel);
        Assert.True(shell.Calendar.HasPanel);
        var tab = Assert.Single(shell.Calendar.OpenPanels);
        Assert.Equal(PanelKind.Waitlist, tab.Kind);
        Assert.True(tab.IsSelected);

        // Wieder offen und über den Knopf geschlossen: dasselbe.
        shell.Companion.ToggleCommand.Execute(null);
        shell.Companion.ToggleCommand.Execute(null);
        Assert.False(shell.Companion.IsOpen);
        Assert.NotNull(shell.Calendar.WaitlistPanel);
        Assert.Single(shell.Calendar.OpenPanels);
    }

    [Fact]
    public void Ohne_andere_Seite_schliesst_der_Assistent_die_Seitenleiste()
    {
        var shell = Shell(new MemorySettingsStore());

        shell.Companion.ToggleCommand.Execute(null);
        Assert.True(shell.Calendar.HasPanel);

        shell.Companion.CloseCommand.Execute(null);
        Assert.False(shell.Calendar.HasPanel);
        Assert.Empty(shell.Calendar.OpenPanels);
    }

    [Fact]
    public void Ein_anderer_Reiter_laesst_den_Assistenten_im_Hintergrund_offen_und_der_Knopf_waehlt_ihn_wieder()
    {
        var shell = Shell(new MemorySettingsStore());
        shell.Calendar.OpenWaitlistCommand.Execute(null);
        shell.Companion.ToggleCommand.Execute(null);

        var waitlist = shell.Calendar.OpenPanels.Single(t => t.Kind == PanelKind.Waitlist);
        shell.Calendar.SelectPanelTabCommand.Execute(waitlist);

        Assert.False(shell.Companion.IsOpen);
        Assert.NotNull(shell.Calendar.WaitlistPanel);
        Assert.Equal(2, shell.Calendar.OpenPanels.Count);

        shell.Companion.ToggleCommand.Execute(null);
        Assert.True(shell.Companion.IsOpen);
        Assert.Same(shell.Companion, shell.Calendar.CompanionPanel);
        Assert.Equal(2, shell.Calendar.OpenPanels.Count);
        Assert.True(shell.Calendar.OpenPanels.Single(t => t.Kind == PanelKind.Assistant).IsSelected);

        // Der Reiter der Warteliste wählt den Assistenten ab, ohne ihn zu schließen.
        shell.Calendar.SelectPanelTabCommand.Execute(shell.Calendar.OpenPanels.Single(t => t.Kind == PanelKind.Waitlist));
        Assert.False(shell.Companion.IsOpen);
    }

    [Fact]
    public void Eine_neue_Leiste_im_Kalender_waehlt_den_Assistenten_ab_und_laesst_seinen_Reiter_stehen()
    {
        var shell = Shell(new MemorySettingsStore());
        shell.Companion.ToggleCommand.Execute(null);

        shell.Calendar.OpenWaitlistCommand.Execute(null);

        Assert.False(shell.Companion.IsOpen);
        Assert.Contains(shell.Calendar.OpenPanels, t => t.Kind == PanelKind.Assistant);
        Assert.NotNull(shell.Calendar.WaitlistPanel);
    }

    [Fact]
    public void Auf_einer_anderen_Seite_ohne_Reiterschiene_steht_der_Assistent_in_der_Leiste_der_Shell()
    {
        var shell = Shell(new MemorySettingsStore());
        shell.Calendar.OpenWaitlistCommand.Execute(null);

        shell.SelectedPage = Assert.Single(shell.Pages, p => p.Title == "Kassa");
        shell.Companion.ToggleCommand.Execute(null);

        // Die Seitenleiste im Kalender bleibt unberührt; der Assistent hat dort keinen Reiter angelegt.
        Assert.True(shell.Companion.IsOpen);
        Assert.True(shell.Companion.ShellOpen);
        Assert.NotNull(shell.Companion.ShellContent);
        Assert.True(shell.Calendar.HasPanel);
        Assert.Null(shell.Calendar.CompanionPanel);
        Assert.Single(shell.Calendar.OpenPanels);

        // Zurück im Kalender: dort gilt der gemerkte Stand des Kalenders, die Leiste der Shell ist zu.
        shell.SelectedPage = shell.Pages[0];
        Assert.False(shell.Companion.ShellOpen);
        Assert.Null(shell.Companion.ShellContent);
    }

    [Fact]
    public void Alle_Seiten_schliessen_laesst_den_Reiter_des_Assistenten_stehen()
    {
        var shell = Shell(new MemorySettingsStore());
        shell.Calendar.OpenWaitlistCommand.Execute(null);
        shell.Companion.ToggleCommand.Execute(null);

        shell.Calendar.CloseAllPanels();

        Assert.True(shell.Companion.IsOpen);
        Assert.Same(shell.Companion, shell.Calendar.CompanionPanel);
        var tab = Assert.Single(shell.Calendar.OpenPanels);
        Assert.Equal(PanelKind.Assistant, tab.Kind);
    }

    [Fact]
    public void Offen_oder_zu_gilt_je_Seite()
    {
        var store = new MemorySettingsStore();
        var shell = Shell(store);
        var kassa = Assert.Single(shell.Pages, p => p.Title == "Kassa");
        var calendar = shell.Pages[0];

        shell.Companion.ToggleCommand.Execute(null);
        Assert.True(shell.Companion.IsOpen);
        shell.SelectedPage = kassa;
        Assert.True(shell.Companion.IsOpen);
        shell.Companion.CloseCommand.Execute(null);
        Assert.False(shell.Companion.IsOpen);

        shell.SelectedPage = calendar;
        Assert.True(shell.Companion.IsOpen);
        shell.SelectedPage = kassa;
        Assert.False(shell.Companion.IsOpen);

        Assert.Equal(new Dictionary<string, bool> { ["kalender"] = true, ["kassa"] = false }, store.Load().CompanionPages);
    }
}
