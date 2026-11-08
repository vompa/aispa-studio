using AispaStudio.Contracts;
using AispaStudio.Desktop.Controls;
using AispaStudio.Desktop.ViewModels;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>
/// Runde 5b Block K4 (ADR 0075, Design-Brief 5.8, 7, 11.4): der Feiertag im Datumstitel, der Hinweis mit Quelle und Stand, die erste Zeile im Tagesüberblick, der Chip „Kalender“ mit
/// der Liste der Abos, dem Schalter „Zeigen“ (Leitung und Rezeption), „Aktiv (für alle)“ nur für die Leitung, dem Punkt bei ausgeblendeten Abos und „Falsch melden“.
/// </summary>
public sealed partial class ViewTests
{
    private static readonly CalendarDayFact HolidayFact = new(null, SubscriptionKind.PublicHoliday, "Nationalfeiertag", SpaSample.Day, SpaSample.Day, "Feiertagsregeln Österreich, Wien", null);

    private static readonly CalendarDayFact SchoolFact = new(Guid.Parse("00000000-0000-0000-0000-0000000000a2"), SubscriptionKind.SchoolHoliday, "Herbstferien", SpaSample.Day,
        SpaSample.Day.AddDays(14), "Abo ‚Schulferien Wien‘", SpaSample.At(11, 40).AddDays(-1));

    private static CalendarSubscriptionDto SampleSubscription(string name, SubscriptionKind kind, string status = "", bool show = true, bool active = true, int days = 136,
        SubscriptionFetchResult result = SubscriptionFetchResult.Ok, string? error = null, Guid? id = null) =>
        new(id ?? Guid.NewGuid(), name, "https://calendar.example.org/" + status, kind, "AT", null, active, false, show, SpaSample.At(11, 40).AddDays(-1), result, error, null, days,
            new DateOnly(2026, 1, 1), new DateOnly(2028, 12, 26), 0);

    private static readonly Guid SubscriptionHoliday = Guid.Parse("00000000-0000-0000-0000-0000000000a1");

    private static StubApi SubscriptionApi(bool reception = false, params CalendarSubscriptionDto[] subscriptions)
    {
        var list = subscriptions.Length > 0
            ? subscriptions
            : [
                SampleSubscription("Feiertage Wien", SubscriptionKind.PublicHoliday, id: SubscriptionHoliday),
                SampleSubscription("Schulferien Wien", SubscriptionKind.SchoolHoliday, days: 40, id: SchoolFact.SubscriptionId),
                SampleSubscription("Hinweise Messe", SubscriptionKind.Notice, show: false, days: 3, result: SubscriptionFetchResult.Failed, error: "Zeitüberschreitung beim Abruf."),
            ];
        var api = (reception ? ReceptionApi() : Api())
            .On($"GET /api/locations/{SpaSample.LocationId}/calendar?date=2026-10-19", SpaSample.CalendarDay() with { Facts = [HolidayFact, SchoolFact] })
            .On($"GET /api/calendar-subscriptions?locationId={SpaSample.LocationId}", list.ToList())
            .On($"GET /api/calendar-subscriptions/notices?locationId={SpaSample.LocationId}", new List<SubscriptionNoticeDto>());
        foreach (var sub in list)
        {
            // Der Server übernimmt, was gesendet wird: {"show":true} blendet das Abo ein.
            api.OnJson($"PUT /api/calendar-subscriptions/{sub.Id}/show", body => sub with { ShowInCalendar = System.Text.Json.JsonDocument.Parse(body!).RootElement.GetProperty("show").GetBoolean() });
        }

        return api;
    }

    private static Task<bool> WithSubscriptions(string theme, string shot, Func<Window, CalendarViewModel, Task> act, bool reception = false, StubApi? api = null) =>
        Render(theme, shot, async (window, main) =>
        {
            var calendar = await SignedInCalendar(main);
            await Pump();
            await act(window, calendar);
            return calendar;
        }, api ?? SubscriptionApi(reception));

    private static async Task OpenSubscriptions(Window window)
    {
        var chip = Named<Button>(window, "HeadSubscriptions");
        chip.Flyout!.ShowAt(chip);
        await Pump(Settling);
    }

    private static List<Control> SubscriptionRows(Window window) =>
        [.. window.GetVisualDescendants().OfType<ItemsControl>().Single(l => l.Name == "SubscriptionList").GetVisualDescendants().OfType<StackPanel>()
            .Where(p => p.DataContext is SubscriptionRowViewModel && p.Parent is ContentPresenter)];

    // ---------- Datumstitel, Hinweis, Tagesüberblick ----------

    [Theory]
    [InlineData("hell")]
    [InlineData("dunkel")]
    public Task Der_Feiertag_steht_hinter_dem_Datum_in_einer_Zeile_und_der_Hinweis_nennt_Quelle_und_Stand(string theme) => WithSubscriptions(theme, "kalender-abos-titel", async (window, calendar) =>
    {
        var line = Named<TextBlock>(window, "DateLine");
        var facts = Named<TextBlock>(window, "DateFacts");
        Assert.True(facts.IsEffectivelyVisible);
        Assert.Equal("· Nationalfeiertag", facts.Text);
        Assert.Equal(FontWeight.SemiBold, facts.FontWeight);

        // Eine Zeile: Datum und Feiertag stehen auf derselben Höhe, nichts bricht um und nichts ist gekürzt (Kopfzeile höchstens zwei Zeilen).
        Assert.Equal(line.TranslatePoint(default, window)!.Value.Y, facts.TranslatePoint(default, window)!.Value.Y, 1.5);
        Assert.True(facts.Bounds.Height <= 20, $"Höhe {facts.Bounds.Height}");
        facts.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Assert.True(facts.DesiredSize.Width - facts.Margin.Left <= facts.Bounds.Width + 0.5, $"Der Feiertag ist gekürzt: {facts.DesiredSize.Width - facts.Margin.Left:0} > {facts.Bounds.Width:0}");

        // Hinweis am Datum: je Eintrag eine Zeile mit Quelle und Stand, Ferien mit „bis“, zuletzt das Kürzel zum Datumsfenster.
        var button = Named<Button>(window, "DateButton");
        var tip = Assert.IsType<string>(ToolTip.GetTip(button));
        Assert.Equal(
            ["Nationalfeiertag · Feiertagsregeln Österreich, Wien", "Herbstferien bis 2.11. · Abo ‚Schulferien Wien‘, Stand gestern", "Datum wählen (G)"],
            tip.Split(Environment.NewLine));
        ToolTip.SetIsOpen(button, true);
        await Pump();
        var popup = window.GetVisualDescendants().OfType<ToolTip>().Single(t => ReferenceEquals(t.Content, tip));
        var text = popup.GetVisualDescendants().OfType<TextBlock>().First();
        Assert.True(Contrast(ColorOf(popup.Background), ColorOf(text.Foreground)) >= 4.5, $"Kontrast {Contrast(ColorOf(popup.Background), ColorOf(text.Foreground)):0.0}");
        ToolTip.SetIsOpen(button, false);
        // Ferien stehen nicht im Titel.
        Assert.DoesNotContain("Herbstferien", facts.Text, StringComparison.Ordinal);
        Assert.Equal("Nationalfeiertag", calendar.DayFactsText);
    });

    [Fact]
    public Task Ohne_Tagesfakten_fehlt_die_Zeile_und_der_Hinweis_bleibt_Datum_waehlen() => Render("hell", "kalender-abos-ohne-fakten", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        await Pump();

        Assert.False(Named<TextBlock>(window, "DateFacts").IsEffectivelyVisible);
        Assert.Equal("Datum wählen (G)", ToolTip.GetTip(Named<Button>(window, "DateButton")));
        Assert.False(calendar.HasOverviewDayLine);
        Assert.False(calendar.HasSubscriptions);
        Assert.False(Named<Button>(window, "HeadSubscriptions").IsEffectivelyVisible);
        return calendar;
    });

    [Theory]
    [InlineData("hell")]
    [InlineData("dunkel")]
    public Task Der_Tagesueberblick_nennt_Feiertag_und_Ferien_in_der_ersten_Zeile(string theme) => WithSubscriptions(theme, "kalender-abos-tagesueberblick", async (window, calendar) =>
    {
        var chip = Named<Button>(window, "OverviewChip");
        chip.Flyout!.ShowAt(chip);
        await Pump(Settling);
        var content = (Panel)((Flyout)chip.Flyout).Content!;

        var line = content.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Name == "OverviewDayLine");
        Assert.Equal("Heute: Nationalfeiertag · Herbstferien bis 2.11.", line.Text);
        Assert.Same(line, content.Children[0]);
        Assert.Equal(FontWeight.SemiBold, line.FontWeight);
        Assert.Equal(420, content.Bounds.Width, 0.5);
        // Eine Zeile, nichts gekürzt.
        Assert.True(line.Bounds.Height <= 20, $"Höhe {line.Bounds.Height}");
        Assert.Equal(calendar.OverviewDayLine, line.Text);
    });

    // ---------- Chip „Kalender“ ----------

    [Theory]
    [InlineData("hell")]
    [InlineData("dunkel")]
    public Task Der_Chip_Kalender_oeffnet_die_Liste_der_Abos_mit_Schaltern_und_der_Leitung_mit_Aktiv_fuer_alle(string theme) => WithSubscriptions(theme, "kalender-abos-liste", async (window, calendar) =>
    {
        var chip = Named<Button>(window, "HeadSubscriptions");
        Assert.True(chip.IsEffectivelyVisible);
        Assert.Equal(34, chip.Bounds.Height, 0.5);
        Assert.Contains("headchip", chip.Classes);
        Assert.Equal("Kalender", chip.GetVisualDescendants().OfType<TextBlock>().Single().Text);
        // Der Chip steht in Zeile 2 neben Team und Räumen.
        Assert.True(Named<Button>(window, "HeadRooms").TranslatePoint(default, window)!.Value.X < chip.TranslatePoint(default, window)!.Value.X);

        await OpenSubscriptions(window);
        var rows = SubscriptionRows(window);
        Assert.Equal(3, rows.Count);
        var content = (Panel)((Flyout)chip.Flyout!).Content!;
        Assert.Equal(420, content.Bounds.Width, 0.5);

        foreach (var row in rows)
        {
            var show = row.GetVisualDescendants().OfType<ToggleSwitch>().Single(s => s.Name == "ShowSwitch");
            var active = row.GetVisualDescendants().OfType<ToggleSwitch>().Single(s => s.Name == "ActiveSwitch");
            var vm = (SubscriptionRowViewModel)row.DataContext!;
            // Beide Schalter sind Schalter nach 5.23 (Spur 40 × 20) und haben einen Namen für Screenreader.
            Assert.Equal("Zeigen", show.Content);
            Assert.Equal("Aktiv (für alle)", active.Content);
            Assert.True(active.IsVisible, "Die Leitung sieht „Aktiv (für alle)“");
            Assert.Equal(vm.Show, show.IsChecked);
            Assert.Equal(vm.IsActive, active.IsChecked);
            Assert.Equal($"{vm.Name} im Kalender zeigen", AutomationProperties.GetName(show));
            Assert.Equal(40, show.GetVisualDescendants().OfType<Border>().Single(b => b.Name == "Track").Bounds.Width, 0.5);
            // Namen und Zustandszeilen sind nicht gekürzt.
            foreach (var text in row.GetVisualDescendants().OfType<TextBlock>().Where(t => t.IsEffectivelyVisible && !string.IsNullOrEmpty(t.Text)))
            {
                var arranged = text.Bounds.Width;
                text.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                Assert.True(text.DesiredSize.Width <= arranged + 0.5 || text.TextWrapping == TextWrapping.Wrap, $"„{text.Text}“ ist gekürzt");
            }
        }

        // Nur das Feiertags-Abo trägt den Hinweis zur Wirkung.
        var notes = rows.Select(r => r.GetVisualDescendants().OfType<TextBlock>().Any(t => t.IsEffectivelyVisible && t.Text == SubscriptionRowViewModel.HolidayNote)).ToList();
        Assert.Equal([true, false, false], notes);
        // Der Fehler des Abrufs steht in der Zeile des Abos (Warnfarbe mit Wortlaut, nicht nur Farbe).
        Assert.Contains(rows[2].GetVisualDescendants().OfType<TextBlock>(), t => t.IsEffectivelyVisible && t.Text == "Zeitüberschreitung beim Abruf.");
        Assert.All(rows, r => Assert.Contains(r.GetVisualDescendants().OfType<Button>(), b => b.Name == "ReportButton" && b.IsEffectivelyVisible));
        Assert.True(calendar.HasSubscriptions);
    });

    [Fact]
    public Task Die_Rezeption_sieht_Zeigen_und_Falsch_melden_aber_nicht_Aktiv_fuer_alle() => WithSubscriptions("hell", "kalender-abos-liste-empfang", async (window, calendar) =>
    {
        await OpenSubscriptions(window);

        foreach (var row in SubscriptionRows(window))
        {
            Assert.True(row.GetVisualDescendants().OfType<ToggleSwitch>().Single(s => s.Name == "ShowSwitch").IsEffectivelyVisible);
            Assert.False(row.GetVisualDescendants().OfType<ToggleSwitch>().Single(s => s.Name == "ActiveSwitch").IsEffectivelyVisible);
            Assert.True(row.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "ReportButton").IsEffectivelyVisible);
        }

        Assert.False(calendar.CanManageSubscriptions);
    }, reception: true);

    [Theory]
    [InlineData("hell")]
    [InlineData("dunkel")]
    public Task Zeigen_wirkt_sofort_und_der_Punkt_am_Chip_sagt_dass_Abos_ausgeblendet_sind(string theme) =>
        WithSubscriptions(theme, "kalender-abos-punkt", async (window, calendar) =>
        {
            var chip = Named<Button>(window, "HeadSubscriptions");
            var dot = Named<Ellipse>(window, "SubscriptionsDot");
            // Das dritte Abo ist schon ausgeblendet: der Punkt steht da, der Name des Chips sagt es.
            Assert.True(dot.IsEffectivelyVisible);
            Assert.Equal("Kalender-Abos, einige ausgeblendet", Avalonia.Automation.AutomationProperties.GetName(chip));
            // Nicht nur Farbe: der Punkt ist 7 px groß und die Fläche trägt ihn mit mindestens 3:1.
            Assert.Equal(7, dot.Bounds.Width, 0.5);
            Assert.True(Contrast(ColorOf(dot.Fill), ColorOf(window, "SurfaceBrush")) >= 3, $"Punkt {Contrast(ColorOf(dot.Fill), ColorOf(window, "SurfaceBrush")):0.0}");

            await OpenSubscriptions(window);
            var row = SubscriptionRows(window)[2];
            var show = row.GetVisualDescendants().OfType<ToggleSwitch>().Single(s => s.Name == "ShowSwitch");
            Assert.False(show.IsChecked);

            // Das dritte Abo einblenden: der Schalter wirkt sofort, der Punkt verschwindet.
            show.IsChecked = true;
            await Pump(Settling);
            Assert.True(calendar.Subscriptions[2].Show);
            Assert.False(dot.IsEffectivelyVisible);
            Assert.Equal("Kalender-Abos", Avalonia.Automation.AutomationProperties.GetName(chip));

            // Ausblenden: der Punkt kommt zurück.
            show.IsChecked = false;
            await Pump(Settling);
            Assert.True(dot.IsEffectivelyVisible);
        });

    [Theory]
    [InlineData("hell")]
    [InlineData("dunkel")]
    public Task Falsch_melden_schliesst_die_Liste_und_oeffnet_die_Rueckfrage_mit_Grund(string theme) => WithSubscriptions(theme, "kalender-abos-falsch-melden", async (window, _) =>
    {
        var chip = Named<Button>(window, "HeadSubscriptions");
        await OpenSubscriptions(window);
        var main = (MainViewModel)window.DataContext!;
        var report = SubscriptionRows(window)[0].GetVisualDescendants().OfType<Button>().Single(b => b.Name == "ReportButton");

        report.Command!.Execute(null);
        report.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        await Pump(Settling);

        // Der Befehl lief zuerst (die Rückfrage steht), danach ging die Liste zu.
        var card = Dialog(window);
        Assert.Equal("Falsch melden?", card.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Name == "DialogTitle").Text);
        Assert.Contains("Abo ‚Feiertage Wien‘ für den 19.10.2026", card.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Name == "DialogMessage").Text, StringComparison.Ordinal);
        Assert.Contains(card.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == "Grund");
        Assert.Contains(card.GetVisualDescendants().OfType<TextBox>(), t => t.IsEffectivelyVisible);
        Assert.False(chip.Flyout!.IsOpen);
        var confirm = DialogButton(window, "ConfirmButton");
        Assert.Equal("Melden", confirm.Content);
        Assert.Contains("primary", confirm.Classes);
        Assert.DoesNotContain("danger", confirm.Classes);
        SaveShot(window, "kalender-abos-falsch-melden-dialog", theme);

        main.DialogHost.Current!.CancelCommand.Execute(null);
        await Pump();
        Assert.False(main.DialogHost.HasDialog);
    });

    [Fact]
    public Task Im_Ueberlauf_Menue_heisst_der_Eintrag_Kalender_und_traegt_das_Symbol() => WithSubscriptions("hell", "kalender-abos-ueberlauf", (window, calendar) =>
    {
        var strip = Named<OverflowStrip>(window, "HeaderRow2");
        var chip = strip.Children.OfType<Border>().Single(b => ReferenceEquals(b.Child, Named<Button>(window, "HeadSubscriptions")));

        Assert.Equal("Kalender", OverflowStrip.GetHeader(chip));
        Assert.Equal(Icons.CalendarSync, OverflowStrip.GetIcon(chip));
        // Im Menü steht der volle Text, auch wenn der Chip nur das Symbol zeigt.
        Assert.True(calendar.HasSubscriptions);
        return Task.CompletedTask;
    });
}
