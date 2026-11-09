using AispaStudio.Contracts;
using AispaStudio.Desktop.ViewModels;
using AispaStudio.Desktop.Views;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>Seitenpanel der Seite „Verlauf“ und Chip „Storniert n“ im Kalender (Arbeitsanweisung Verlauf, Aufgabe C), in Hell und Dunkel.</summary>
public sealed partial class ViewTests
{
    private static ActivityEntry PanelEntry(long seq, ActivityKind kind, string text, IReadOnlyList<ActivityValue> values, Guid? appointmentId = null, Guid? receiptId = null, bool? late = null) =>
        new(seq, SpaSample.At(10, 30), kind, text, "Rita", ActivityChannel.Reception, SpaSample.Customers[0].Id, SpaSample.Customers[0].InHouseName, appointmentId, null, null, values, late, receiptId);

    private static readonly ActivityEntry PanelCancelled = PanelEntry(3, ActivityKind.Cancelled, "Storniert von Rita: Grund „Krank“",
        [new ActivityValue("Grund", null, "Krank"), new ActivityValue("Status", "Gebucht", "Storniert"), new ActivityValue("Ausfallgebühr", null, null)],
        SpaSample.Selected.Id, late: true);

    private static readonly ActivityEntry PanelPaid = PanelEntry(2, ActivityKind.Paid, "Bezahlt bar: 85,00 € (Beleg 2026-0412)", [], SpaSample.Selected.Id, Guid.NewGuid());

    private static StubApi PanelApi() => Api()
        .On(ActivityToday, new ActivityPage([PanelPaid, PanelCancelled], null))
        .On($"GET /api/locations/{SpaSample.LocationId}/online-rules", new LocationOnlineRules(true, 60, 90, 24, 15, null, false, NoShowFeePercent: 50))
        .On($"GET /api/appointments/{SpaSample.Selected.Id}", SpaSample.Selected);

    private static async Task<ShellViewModel> OpenPanel(MainViewModel main, int row)
    {
        await SignedInCalendar(main);
        var shell = (ShellViewModel)main.Content!;
        await OpenActivity(shell);
        shell.Activity.SelectedItem = shell.Activity.Rows[row];
        shell.Activity.OpenSelectedCommand.Execute(null);
        await Pump(Settling);
        return shell;
    }

    [Theory]
    [InlineData("hell")]
    [InlineData("dunkel")]
    public Task Verlauf_Panel_Storno_zeigt_Kopf_Hauptkarte_Hinweis_Werte_und_genau_einen_Primaerknopf(string theme) => Render(theme, "verlauf-panel-storno", async (window, main) =>
    {
        // Zeile 0 ist der Tageskopf, 1 die Zahlung, 2 der Storno.
        var shell = await OpenPanel(main, 2);
        var panel = Named<Border>(window, "SidePanel");

        Assert.True(panel.IsEffectivelyVisible);
        Assert.Equal(360, panel.Bounds.Width, 0.5);
        var texts = VisibleTexts(panel);
        Assert.Contains("Storno", texts);
        Assert.Contains("Mo. 19.10., 10:30", texts);
        Assert.Contains("Storniert von Rita: Grund „Krank“", texts);
        Assert.Contains("Brigitte Müller", texts);
        Assert.Contains("+43 664 123 45 67", texts);
        Assert.Contains("von Rita", texts);
        Assert.Contains("Rezeption", texts);
        Assert.Contains("Grund: Krank", texts);
        Assert.Contains("Kurzfristig abgesagt: innerhalb der Stornofrist von 24 Stunden. Laut Regel fallen 50 % Ausfallgebühr an.", texts);
        // Werte: Kopf und Zeilen; leer steht als „–“, der Grund steht nur in der Hauptkarte.
        Assert.Equal(["Feld", "Vorher", "Nachher"], texts.Where(t => t is "Feld" or "Vorher" or "Nachher").ToList());
        var values = VisibleTexts(Named<ItemsControl>(panel, "ValuesList"));
        Assert.Equal(["Status", "Gebucht", "Storniert", "Ausfallgebühr", "–", "–"], values);
        Assert.Equal("Stornierung zurücknehmen", Label(Assert.Single(VisiblePrimaries(panel))));
        Assert.True(Named<Button>(panel, "RestoreButton").IsEnabled);
        Assert.Equal(["Stornierung zurücknehmen", "Termin öffnen"], panel.GetVisualDescendants().OfType<Button>().Where(b => b.IsEffectivelyVisible && b.Name is not null).Select(Label));
        return shell.Calendar;
    }, PanelApi());

    [Fact]
    public Task Verlauf_Panel_Zahlung_zeigt_Beleg_anzeigen_als_Primaerknopf() => Render("hell", "verlauf-panel-zahlung", async (window, main) =>
    {
        var shell = await OpenPanel(main, 1);
        var panel = Named<Border>(window, "SidePanel");

        Assert.Contains("Zahlung", VisibleTexts(panel));
        Assert.Equal("Beleg anzeigen", Label(Assert.Single(VisiblePrimaries(panel))));
        Assert.DoesNotContain(panel.GetVisualDescendants().OfType<Control>(), c => c.Name == "ValuesCard" && c.IsEffectivelyVisible);
        Assert.Equal(["Beleg anzeigen", "Termin öffnen", "Kundenkarte öffnen"], panel.GetVisualDescendants().OfType<Button>().Where(b => b.IsEffectivelyVisible && b.Name is not null).Select(Label));
        return shell.Calendar;
    }, PanelApi());

    [Fact]
    public Task Verlauf_Panel_ohne_Recht_zu_buchen_sperrt_Zuruecknehmen_mit_Hinweis() => Render("hell", "verlauf-panel-ohne-recht", async (window, main) =>
    {
        // Eine Rezeption ohne das Recht zu buchen: Zahlungen und Kassa fehlen, Stornos stehen im Verlauf.
        var shell = await OpenPanel(main, 1);
        var panel = Named<Border>(window, "SidePanel");
        var restore = Named<Button>(panel, "RestoreButton");

        Assert.False(restore.IsEnabled);
        Assert.Equal("Dafür braucht der Zugang das Recht, Termine zu buchen.", shell.Activity.RestoreHint);
        Assert.Equal("Dafür braucht der Zugang das Recht, Termine zu buchen.", Avalonia.Controls.ToolTip.GetTip(restore)?.ToString());
        return shell.Calendar;
    }, ViewerApi());

    private static StubApi ViewerApi()
    {
        var viewer = new CurrentUser("u3", "lesen@demo.aispa.test", "Kim", 1, new CurrentAccess(Guid.NewGuid(), "Day-Spa Wien", StaffRole.Reception, true, []), null, [AccessRight.ViewCalendar]);
        return Api().On("GET /api/auth/me", viewer)
            .On(ActivityToday, new ActivityPage([PanelCancelled], null))
            .On($"GET /api/locations/{SpaSample.LocationId}/online-rules", new LocationOnlineRules(true, 60, 90, 24, 15, null, false));
    }

    [Fact]
    public Task Verlauf_Panel_schliesst_mit_Esc_und_der_Fokus_kehrt_zur_Liste_zurueck() => Render("hell", "verlauf-panel-esc", async (window, main) =>
    {
        var shell = await OpenPanel(main, 1);
        var page = window.GetVisualDescendants().OfType<ActivityView>().Single();
        Assert.True(Named<Border>(window, "SidePanel").IsEffectivelyVisible);
        // Der Fokus liegt im Panel (der primäre Knopf), Enter hat es geöffnet.
        var focused = TopLevel.GetTopLevel(window)!.FocusManager!.GetFocusedElement() as Control;
        Assert.True(focused is not null && focused.GetVisualAncestors().Any(a => a.Equals(Named<Border>(window, "SidePanel"))));

        Press(window, PhysicalKey.Escape, RawInputModifiers.None);
        await Pump(Settling);

        Assert.False(shell.Activity.HasPanel);
        Assert.False(Named<Border>(window, "SidePanel").IsEffectivelyVisible);
        var onList = TopLevel.GetTopLevel(window)!.FocusManager!.GetFocusedElement() as Control;
        Assert.True(onList is not null && onList.GetVisualAncestors().Any(a => a.Equals(Named<ListBox>(page, "List"))));
        return shell.Calendar;
    }, PanelApi());

    [Fact]
    public Task Kalender_Kopf_zeigt_Storniert_2_mit_Zaehler() => Render("hell", "kalender-storniert-zaehler", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        await Pump(Settling);
        var chip = Named<Button>(window, "HeadCancelled");
        Assert.Equal(2, calendar.CancelledCount);
        Assert.True(chip.IsEffectivelyVisible);
        Assert.Contains("warn", chip.Classes);
        Assert.Contains("headchip", chip.Classes);
        Assert.Equal(["Storniert 2"], chip.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text));
        Assert.Equal("Stornierte Termine dieses Tags im Verlauf ansehen.", Avalonia.Controls.ToolTip.GetTip(chip)?.ToString());
        Assert.Equal(TopOf(window, Named<Button>(window, "HeadTeam")), TopOf(window, chip), 0.5);
        return calendar;
    }, CancelledDayApi());

    [Fact]
    public Task Kalender_Kopf_Klick_auf_Storniert_oeffnet_den_Verlauf_mit_Tag_und_Art_Stornos() => Render("hell", "kalender-storniert-klick", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        await Pump(Settling);
        var chip = Named<Button>(window, "HeadCancelled");

        chip.Command!.Execute(null);
        await Pump(Settling);
        var shell = (ShellViewModel)main.Content!;
        Assert.Same(shell.Activity, shell.SelectedPage?.Page);
        Assert.Equal((ActivityPeriod.Custom, new DateOnly(2026, 10, 19)), (shell.Activity.Period, shell.Activity.CustomFrom));
        Assert.Equal(["Stornos"], shell.Activity.KindChips.Where(c => c.IsOn).Select(c => c.Label));
        return calendar;
    }, CancelledDayApi());

    private static StubApi CancelledDayApi()
    {
        var day = SpaSample.CalendarDay();
        var cancelled = new[] { SpaSample.Selected with { Id = Guid.NewGuid(), Status = AppointmentStatus.Cancelled }, SpaSample.Selected with { Id = Guid.NewGuid(), Status = AppointmentStatus.Cancelled } };
        return Api().On($"GET /api/locations/{SpaSample.LocationId}/calendar?date=2026-10-19", day with { Appointments = [.. day.Appointments, .. cancelled] })
            .On($"{ActivityToday}&kinds=Cancelled&kinds=Restored", new ActivityPage([PanelCancelled], null))
            .On(ActivityToday, new ActivityPage([PanelCancelled], null));
    }
}
