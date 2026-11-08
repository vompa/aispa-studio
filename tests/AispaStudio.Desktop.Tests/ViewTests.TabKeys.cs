using AispaStudio.Desktop.Controls;
using AispaStudio.Desktop.ViewModels;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>Fluent 2 F15 und Ä19: Tastatur der Reiterleisten (←/→, Leertaste und Enter, Strg+Tab, Strg+W, Strg+Ziffer) und gekürzte Reiter unten (Design-Brief 5.4, 5.12).</summary>
public sealed partial class ViewTests
{
    private static Border Rail(Window window) => window.GetVisualDescendants().OfType<Border>().Single(b => b.Name == "PanelRail");

    /// <summary>Die Reiter unten (ohne Chip und Pfeile), von links nach rechts.</summary>
    private static List<Button> RailTabs(Window window) =>
        [.. Rail(window).GetVisualDescendants().OfType<Button>().Where(b => b.Classes.Contains("paneltab") && b.Name is null && b.IsEffectivelyVisible).OrderBy(b => LeftOf(window, b))];

    private static async Task<CalendarViewModel> WithPages(Window window, MainViewModel main, int pages)
    {
        var calendar = await SignedInCalendar(main);
        calendar.OpenAppointment(WithTimesDay().Appointments.First(a => a.Id == SpaSample.Selected.Id));
        if (pages >= 2)
        {
            calendar.OpenWaitlistCommand.Execute(null);
        }

        if (pages >= 3)
        {
            Assert.True(calendar.HandleKey(Key.N, KeyModifiers.Control));
        }

        if (pages >= 4)
        {
            Assert.True(calendar.HandleKey(Key.K, KeyModifiers.Control));
        }

        if (pages >= 5)
        {
            calendar.StartBooking(calendar.Columns[0], 100);
        }

        if (pages >= 6)
        {
            calendar.OpenMessagesCommand.Execute(null);
        }

        await Pump(Settling);
        return calendar;
    }

    [Fact]
    public Task Reiter_unten_sind_ein_Tab_Stopp_Pfeile_bewegen_den_Fokus_Enter_und_Leertaste_waehlen_ohne_Umlauf() => Render("hell", "reiter-tastatur", async (window, main) =>
    {
        var calendar = await WithPages(window, main, 2);
        var tabs = RailTabs(window);
        Assert.Equal(["Warteliste", "Termin"], tabs.Select(t => t.GetVisualDescendants().OfType<TextBlock>().Single().Text));

        // Nur der gewählte Reiter ist ein Tab-Stopp.
        Assert.Equal([true, false], tabs.Select(t => t.IsTabStop));
        tabs[0].Focus(NavigationMethod.Tab);
        await Pump();

        // → bewegt den Fokus auf den zweiten Reiter, ohne die Seite zu wechseln; am Ende bleibt er stehen.
        Press(window, PhysicalKey.ArrowRight);
        await Pump();
        Assert.Same(tabs[1], Focused(window));
        Assert.NotNull(calendar.WaitlistPanel);
        Press(window, PhysicalKey.ArrowRight);
        await Pump();
        Assert.Same(tabs[1], Focused(window));
        Press(window, PhysicalKey.ArrowLeft);
        await Pump();
        Assert.Same(tabs[0], Focused(window));
        Press(window, PhysicalKey.ArrowLeft);
        await Pump();
        Assert.Same(tabs[0], Focused(window));

        // Enter wählt den fokussierten Reiter, danach auch die Leertaste.
        Press(window, PhysicalKey.ArrowRight);
        Press(window, PhysicalKey.Enter);
        await Pump(Settling);
        Assert.Equal(SpaSample.Selected.Id, calendar.Selected?.Appointment.Id);
        Assert.Null(calendar.WaitlistPanel);
        tabs = RailTabs(window);
        // Ein Klick ändert die Reihenfolge nicht: Termin steht rechts und ist jetzt der gewählte und der einzige Tab-Stopp.
        Assert.Equal([false, true], tabs.Select(t => t.Classes.Contains("on")));
        Assert.Equal([false, true], tabs.Select(t => t.IsTabStop));

        var other = tabs.Single(t => !t.Classes.Contains("on"));
        other.Focus(NavigationMethod.Directional);
        Press(window, PhysicalKey.Space);
        await Pump(Settling);
        Assert.NotNull(calendar.WaitlistPanel);
        return calendar;
    }, WithTimes());

    [Fact]
    public Task Segmentierte_Schiene_ist_ein_Tab_Stopp_Pfeile_bewegen_den_Fokus_Enter_waehlt() => Render("hell", "reiter-segmentiert", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        calendar.StartBooking(calendar.Columns[1], 330 * DayLayout.PixelsPerMinute);
        await Pump(Settling);
        var strip = window.GetVisualDescendants().OfType<Border>().Single(b => b.Name == "EntryKinds" && b.IsEffectivelyVisible);
        var tabs = strip.GetVisualDescendants().OfType<Button>().Where(b => b.IsEffectivelyVisible).ToList();
        Assert.True(tabs.Count >= 3);
        var selected = tabs.Single(t => t.Classes.Contains("on"));
        Assert.Equal(tabs.Select(t => ReferenceEquals(t, selected)), tabs.Select(t => t.IsTabStop));

        selected.Focus(NavigationMethod.Tab);
        var index = tabs.IndexOf(selected);
        Press(window, PhysicalKey.ArrowRight);
        await Pump();
        Assert.Same(tabs[index + 1], Focused(window));
        Assert.True(selected.Classes.Contains("on"), "Pfeil wählt nicht");
        Press(window, PhysicalKey.End);
        await Pump();
        Assert.Same(tabs[^1], Focused(window));
        Press(window, PhysicalKey.ArrowRight);
        await Pump();
        Assert.Same(tabs[^1], Focused(window));
        Press(window, PhysicalKey.Home);
        await Pump();
        Assert.Same(tabs[0], Focused(window));
        var kind = calendar.NewEntry!.Kind;
        Press(window, PhysicalKey.ArrowRight);
        Press(window, PhysicalKey.Enter);
        await Pump(Settling);
        Assert.NotEqual(kind, calendar.NewEntry!.Kind);
        return calendar;
    });

    [Fact]
    public Task Strg_W_schliesst_die_gewaehlte_Seite_und_fragt_bei_Eingaben_Strg_Ziffer_waehlt_den_Reiter_Strg_9_den_letzten() => Render("hell", "reiter-strg", async (window, main) =>
    {
        var calendar = await WithPages(window, main, 4);
        Assert.Equal(["Wunsch", "Freie Zeit", "Warteliste", "Termin"], calendar.OpenPanels.Select(t => t.Text));
        Assert.Equal("Wunsch", calendar.OpenPanels.Single(t => t.IsSelected).Text);

        // Strg+2 wählt den zweiten Reiter der Reihe, Strg+9 den letzten, Strg+8 ohne achten Reiter lässt alles stehen.
        Press(window, PhysicalKey.Digit2, RawInputModifiers.Control);
        await Pump(Settling);
        Assert.Equal("Freie Zeit", calendar.OpenPanels.Single(t => t.IsSelected).Text);
        Press(window, PhysicalKey.Digit9, RawInputModifiers.Control);
        await Pump(Settling);
        Assert.Equal("Termin", calendar.OpenPanels.Single(t => t.IsSelected).Text);
        Press(window, PhysicalKey.Digit8, RawInputModifiers.Control);
        await Pump(Settling);
        Assert.Equal("Termin", calendar.OpenPanels.Single(t => t.IsSelected).Text);
        Press(window, PhysicalKey.Digit1, RawInputModifiers.Control);
        await Pump(Settling);
        Assert.Equal("Wunsch", calendar.OpenPanels.Single(t => t.IsSelected).Text);

        // Strg+Tab und Strg+Umschalt+Tab laufen im Kreis (mit Umlauf).
        Press(window, PhysicalKey.Tab, RawInputModifiers.Control | RawInputModifiers.Shift);
        await Pump(Settling);
        Assert.Equal("Termin", calendar.OpenPanels.Single(t => t.IsSelected).Text);
        Press(window, PhysicalKey.Tab, RawInputModifiers.Control);
        await Pump(Settling);
        Assert.Equal("Wunsch", calendar.OpenPanels.Single(t => t.IsSelected).Text);

        // Strg+W schließt die gewählte Seite ohne Rückfrage, wenn nichts eingetragen ist: der nächste Reiter wird gewählt.
        Press(window, PhysicalKey.W, RawInputModifiers.Control);
        await Pump(Settling);
        Assert.False(main.DialogHost.HasDialog);
        Assert.Equal(["Freie Zeit", "Warteliste", "Termin"], calendar.OpenPanels.Select(t => t.Text));
        Assert.Single(calendar.OpenPanels, t => t.IsSelected);

        // Mit eingetragenem Text fragt Strg+W „Änderungen verwerfen?“; Weiter bearbeiten lässt die Seite offen, Verwerfen schließt sie.
        Assert.True(calendar.HandleKey(Key.K, KeyModifiers.Control));
        calendar.Request!.Text = "heute ab 17 Uhr eine Massage";
        await Pump(Settling);
        Press(window, PhysicalKey.W, RawInputModifiers.Control);
        await Pump(Settling);
        Assert.Equal("Änderungen verwerfen?", main.DialogHost.Current?.Title);
        main.DialogHost.Current!.CancelCommand.Execute(null);
        await Pump(Settling);
        Assert.NotNull(calendar.Request);
        Press(window, PhysicalKey.W, RawInputModifiers.Control);
        await Pump(Settling);
        main.DialogHost.Current!.ConfirmCommand.Execute(null);
        await Pump(Settling);
        Assert.Null(calendar.Request);
        return calendar;
    }, WithTimes());

    [Fact]
    public Task Strg_Ziffer_bucht_im_Wunsch_mit_Vorschlaegen_weiter_den_Vorschlag() => Render("hell", "reiter-strg-vorschlag", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        Assert.True(calendar.HandleKey(Key.K, KeyModifiers.Control));
        calendar.Request!.Text = "heute ab 17 Uhr eine kräftige Aromaöl-Massage, nicht bei Marco";
        await calendar.Request.SubmitAsync();
        await Pump(Settling);
        Assert.True(calendar.Request.HasProposals);
        // Mit Vorschlägen ist Strg+1 bis 3 das Buchen, kein Reiterwechsel.
        Assert.False(calendar.HandleTabShortcut(Key.D1));
        Assert.True(calendar.HandleTabShortcut(Key.D4));
        return calendar;
    }, Api().On($"POST /api/locations/{SpaSample.LocationId}/calendar-request", SpaSample.CalendarRequest()));

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    public Task Reiter_unten_kuerzen_bei_Platzmangel_und_zeigen_ab_drei_Seiten_nur_das_Symbol(int pages) => Render("hell", $"reiter-unten-{pages}-seiten", async (window, main) =>
    {
        var calendar = await WithPages(window, main, pages);
        var tabs = RailTabs(window);
        // Mit Text höchstens zwei, nur mit Symbol bis zu fünf (Ä29); die sechste Seite bringt den Chip „⋯ 1“.
        Assert.Equal(pages <= 2 ? pages : Math.Min(pages, 5), tabs.Count);
        var rail = Rail(window);
        var more = rail.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "PanelMore");

        if (pages <= 2)
        {
            // Der Platz reicht: Symbol und Text; ein langer Name wird gekürzt (Ellipsis), der volle Name steht im Tooltip.
            Assert.False(calendar.TabsCompact);
            Assert.All(tabs, t => Assert.True(t.GetVisualDescendants().OfType<TextBlock>().Single().IsEffectivelyVisible));
            Assert.All(tabs, t => Assert.True(t.GetVisualDescendants().OfType<TextBlock>().Single().Bounds.Width <= calendar.TabTextMaxWidth + 0.5));
        }
        else
        {
            // Die Reiter zeigen nur ihr Symbol, Name und Tooltip nennen die Seite.
            Assert.True(calendar.TabsCompact);
            Assert.All(tabs, t => Assert.False(t.GetVisualDescendants().OfType<TextBlock>().Single().IsEffectivelyVisible));
            Assert.All(tabs, t => Assert.Single(t.GetVisualDescendants().OfType<Icon>()));
            Assert.All(tabs, t => Assert.True(t.Bounds.Width < CalendarViewModel.MinTabWidth));
        }

        // Chip und Pfeile gibt es erst, wenn die Schiene nicht alle Seiten zeigt.
        Assert.Equal(pages == 6, more.IsEffectivelyVisible);
        if (pages == 6)
        {
            Assert.Equal("⋯ 1", more.GetVisualDescendants().OfType<TextBlock>().Single().Text);
        }

        // Jeder Reiter nennt seine Seite im Tooltip und für Screenreader („Neuer Termin“ mit dem Zusatz „in Bearbeitung“, Ä149).
        foreach (var (tab, panel) in tabs.Zip(calendar.VisibleTabs))
        {
            Assert.Equal(panel.Tip, ToolTip.GetTip(tab));
            Assert.Equal(panel.Name, Avalonia.Automation.AutomationProperties.GetName(tab));
        }

        // Nichts ragt aus der Schiene.
        Assert.All(rail.GetVisualDescendants().OfType<Button>().Where(b => b.IsEffectivelyVisible), b => Assert.True(LeftOf(window, b) + b.Bounds.Width <= LeftOf(window, rail) + rail.Bounds.Width - 60 + 0.5, b.Name));
        return calendar;
    }, WithTimes());

    [Fact]
    public void Breiten_der_Reiter_unten_aus_der_Breite_der_Seitenleiste()
    {
        // 360 breit: Schiene 291; bei zwei Seiten 144,5 je Reiter, ab der dritten wären es mit Chip und Pfeilen 88,5 (unter 96: nur Symbol, dann bis zu fünf).
        Assert.Equal(96, CalendarViewModel.MinTabWidth);
        Assert.Equal(112, CalendarViewModel.TabOverflowWidth);
    }
}
