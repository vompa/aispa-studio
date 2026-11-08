using AispaStudio.Contracts;
using AispaStudio.Desktop.Controls;
using AispaStudio.Desktop.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>Schritt 15: Reiter am unteren Rand der Seitenleiste und Zeitbalken im Termin-Panel (Design-Brief 5.12, Punkte 5 und 7).</summary>
public sealed partial class ViewTests
{
    /// <summary>Der Beispieltermin mit 5 Minuten Vorbereitung, 10 Minuten Nachbereitung und 10 Minuten Gast-Puffer.</summary>
    private static CalendarDay WithTimesDay()
    {
        var day = SpaSample.CalendarDay();
        return day with
        {
            Appointments =
            [
                .. day.Appointments.Select(a => a.Id == SpaSample.Selected.Id
                    ? a with { PreparationMinutes = 5, FollowUpMinutes = 10, ExtraFollowUpMinutes = 10, BlockedFrom = a.Start.AddMinutes(-5), BlockedUntil = a.End.AddMinutes(20) }
                    : a),
            ],
        };
    }

    private static StubApi WithTimes() => Api().On($"GET /api/locations/{SpaSample.LocationId}/calendar?date=2026-10-19", WithTimesDay());

    private static Border Segment(Visual root, TimeBarKind kind) =>
        root.GetVisualDescendants().OfType<TimeBar>().Single().GetVisualDescendants().OfType<Border>()
            .Single(b => b.Tag is TimeBarSegment segment && segment.Kind == kind);

    [Fact]
    public Task Zeitbalken_Klick_auf_die_Vorbereitung_oeffnet_die_Zahlenfelder() => Render("hell", "kalender-zeitbalken", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        var appointment = WithTimesDay().Appointments.First(a => a.Id == SpaSample.Selected.Id);
        calendar.OpenAppointment(appointment);
        await Pump();

        var bar = window.GetVisualDescendants().OfType<TimeBar>().Single();
        Assert.True(bar.IsEffectivelyVisible);
        Assert.Equal([TimeBarKind.Preparation, TimeBarKind.Treatment, TimeBarKind.FollowUp, TimeBarKind.Buffer], bar.Segments!.Select(s => s.Kind));
        Assert.Equal(10, Segment(window, TimeBarKind.Treatment).Bounds.Height);

        // Behandlung und Puffer sind nicht anklickbar, Vor- und Nachbereitung schon (Hand-Zeiger).
        Assert.DoesNotContain("editable", Segment(window, TimeBarKind.Treatment).Classes);
        Assert.Contains("editable", Segment(window, TimeBarKind.Preparation).Classes);
        Assert.Contains("editable", Segment(window, TimeBarKind.FollowUp).Classes);
        ClickCenter(window, Segment(window, TimeBarKind.Treatment));
        await Pump();
        Assert.False(calendar.Selected!.IsEditingTimes);

        ClickCenter(window, Segment(window, TimeBarKind.Preparation));
        await Pump();
        Assert.True(calendar.Selected!.IsEditingTimes);
        Assert.Equal(5, calendar.Selected.TimesPreparation);
        return calendar;
    }, WithTimes());

    [Fact]
    public Task Reiter_unten_zeigen_beide_Seiten_und_der_Klick_wechselt() => Render("hell", "kalender-reiter", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        calendar.OpenAppointment(WithTimesDay().Appointments.First(a => a.Id == SpaSample.Selected.Id));
        calendar.OpenWaitlistCommand.Execute(null);
        await Pump();

        var rail = window.GetVisualDescendants().OfType<Border>().Single(b => b.Name == "PanelRail");
        var tabs = rail.GetVisualDescendants().OfType<Button>().Where(b => b.Classes.Contains("paneltab") && b.Name is null && b.IsEffectivelyVisible).ToList();
        // Der zuletzt geöffnete Reiter steht ganz links; bei zwei offenen Seiten gibt es weder Chip noch Pfeile (Ä15).
        Assert.Equal(["Warteliste", "Termin"], tabs.Select(t => t.GetVisualDescendants().OfType<TextBlock>().Single().Text));
        Assert.Equal([true, false], tabs.Select(t => t.Classes.Contains("on")));
        Assert.DoesNotContain(rail.GetVisualDescendants().OfType<Button>(), b => b.IsEffectivelyVisible && (b.Name is "PanelMore" or "PanelPrevious" or "PanelNext"));
        Assert.All(tabs, t => Assert.Equal(28, t.Bounds.Height, 1));
        Assert.Equal(1, rail.BorderThickness.Top);

        // Die Schiene liegt unten in der Leiste, in voller Breite.
        var panel = window.GetVisualDescendants().OfType<Border>().Single(b => b.Name == "SidePanel");
        // Innerhalb des 1-px-Rahmens links der Leiste (Brief 5.12 Punkt 1).
        Assert.Equal(panel.Bounds.Width - 1, rail.Bounds.Width, 1);
        Assert.True(rail.TranslatePoint(new Point(0, rail.Bounds.Height), panel)!.Value.Y >= panel.Bounds.Height - 2);

        ClickCenter(window, tabs[1]);
        await Pump();
        Assert.Equal(SpaSample.Selected.Id, calendar.Selected?.Appointment.Id);
        Assert.Null(calendar.WaitlistPanel);
        Assert.Equal(["Warteliste", "Termin"], calendar.OpenPanels.Select(t => t.Text));
        // Der Zeitbalken steht wieder im Termin.
        Assert.Single(window.GetVisualDescendants().OfType<TimeBar>(), b => b.IsEffectivelyVisible);
        return calendar;
    }, WithTimes());

    [Fact]
    public Task Reiter_mit_sechs_Seiten_zeigen_fuenf_Symbole_den_Chip_mit_Klappliste_und_Pfeile() => Render("hell", "kalender-reiter-mehr", async (window, main) =>
    {
        var calendar = await WithPages(window, main, 6);
        await Pump();

        var rail = window.GetVisualDescendants().OfType<Border>().Single(b => b.Name == "PanelRail");
        var tabs = RailTabs(window);
        // Reihe: Meldungen, Neuer Termin, Wunsch, Freie Zeit, Warteliste, Termin. Fünf Symbole sind sichtbar, der älteste (Termin) steht nur in der Klappliste (Ä29).
        Assert.Equal(["Meldungen", "Neuer Termin, in Bearbeitung", "Wunsch", "Freie Zeit", "Warteliste"], tabs.Select(t => Avalonia.Automation.AutomationProperties.GetName(t)));
        Assert.Equal([true, false, false, false, false], tabs.Select(t => t.Classes.Contains("on")));
        Assert.All(tabs, t => Assert.False(t.GetVisualDescendants().OfType<TextBlock>().Single().IsEffectivelyVisible));

        // Rechts in der Schiene: Chip „⋯ 1“ und zwei Pfeile (28 × 28); die rechten 60 px bleiben für „KI und Hilfe“ frei.
        var more = rail.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "PanelMore");
        var previous = rail.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "PanelPrevious");
        var next = rail.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "PanelNext");
        Assert.True(more.IsEffectivelyVisible && previous.IsEffectivelyVisible && next.IsEffectivelyVisible);
        Assert.Equal("⋯ 1", more.GetVisualDescendants().OfType<TextBlock>().Single().Text);
        Assert.Equal((28, 28), (previous.Bounds.Width, previous.Bounds.Height));
        Assert.Equal((28, 28), (next.Bounds.Width, next.Bounds.Height));
        Assert.True(next.TranslatePoint(new Point(next.Bounds.Width, 0), rail)!.Value.X <= rail.Bounds.Width - 60 + 0.5);
        // Nichts wird abgeschnitten: die fünf Reiter, der Chip und die Pfeile stehen nebeneinander innerhalb der Schiene.
        var tabRight = tabs.Max(t => t.TranslatePoint(new Point(t.Bounds.Width, 0), rail)!.Value.X);
        Assert.True(tabRight <= more.TranslatePoint(new Point(0, 0), rail)!.Value.X + 0.5, "Reiter und Chip überlappen");

        // Die Klappliste nennt alle sechs offenen Seiten mit Symbol und Text; ein Klick wählt die Seite.
        ClickCenter(window, more);
        await Pump();
        Assert.True(more.Flyout!.IsOpen);
        var list = ((Flyout)more.Flyout).Content as Control;
        var entries = list!.GetVisualDescendants().OfType<Button>().ToList();
        Assert.Equal(["Meldungen", "Neuer Termin", "Wunsch", "Freie Zeit", "Warteliste", "Termin"], entries.Select(e => e.GetVisualDescendants().OfType<TextBlock>().Single().Text));
        Assert.All(entries, e => Assert.Single(e.GetVisualDescendants().OfType<Icon>()));
        // Ein Klick löst Click (schließt die Liste) und den Befehl des Eintrags (wählt die Seite) aus; der Test ruft beides auf, weil die Liste in einem eigenen Popup-Fenster liegt.
        entries[5].RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        entries[5].Command!.Execute(entries[5].CommandParameter);
        await Pump();
        Assert.False(more.Flyout.IsOpen);
        Assert.Equal(SpaSample.Selected.Id, calendar.Selected?.Appointment.Id);
        Assert.Equal(["Meldungen", "Neuer Termin", "Wunsch", "Freie Zeit", "Termin"], calendar.VisibleTabs.Select(t => t.Text));

        // Pfeil rechts blättert zum nächsten in der Reihe (am Ende von vorn).
        ClickCenter(window, next);
        await Pump();
        await Pump();
        Assert.Equal("Meldungen", calendar.OpenPanels.Single(t => t.IsSelected).Text);
        return calendar;
    }, WithTimes());

    [Fact]
    public Task Reiter_mit_fuenf_Seiten_zeigen_fuenf_Symbole_ohne_Chip_und_Pfeile() => Render("hell", "kalender-reiter-fuenf", async (window, main) =>
    {
        var calendar = await WithPages(window, main, 5);
        var rail = Rail(window);
        var tabs = RailTabs(window);

        Assert.All(tabs, t => Assert.False(string.IsNullOrWhiteSpace(ToolTip.GetTip(t) as string)));
        Assert.Equal(["Neuer Termin, in Bearbeitung", "Wunsch", "Freie Zeit", "Warteliste", "Termin"], tabs.Select(t => Avalonia.Automation.AutomationProperties.GetName(t)));
        Assert.All(tabs, t => Assert.Single(t.GetVisualDescendants().OfType<Icon>()));
        Assert.All(tabs, t => Assert.False(t.GetVisualDescendants().OfType<TextBlock>().Single().IsEffectivelyVisible));
        Assert.DoesNotContain(rail.GetVisualDescendants().OfType<Button>(), b => b.IsEffectivelyVisible && b.Name is "PanelMore" or "PanelPrevious" or "PanelNext");
        Assert.All(tabs, t => Assert.True(LeftOf(window, t) + t.Bounds.Width <= LeftOf(window, rail) + rail.Bounds.Width - 60 + 0.5));
        Assert.Equal([true, false, false, false, false], tabs.Select(t => t.Classes.Contains("on")));
        return calendar;
    }, WithTimes());
}
