using AispaStudio.Contracts;
using AispaStudio.Desktop.Controls;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>Runde 5, Ä31: die Gruppe „Notizen heute“ ganz oben in den Meldungen, der Notiz-Punkt am Knopf, die Taste „.“ und das Notizfeld.</summary>
public sealed partial class ViewTests
{
    private static string DayRoute => $"GET /api/locations/{SpaSample.LocationId}/calendar?date=2026-10-19";

    private static readonly string[] MessageGroupTitles = ["Fehler", "Warnungen", "Hinweise", "Erledigt"];

    private static StubApi NotesApi() => Api().On($"GET /api/locations/{SpaSample.LocationId}/resources", SpaSample.ResourcesWithAbsent).On(DayRoute, SpaSample.BusyDay());

    private static StubApi NoNotesApi() => Api().On(DayRoute, SpaSample.CalendarDay());

    private static TextBlock TextIn(Control root, string text) =>
        root.GetVisualDescendants().OfType<TextBlock>().First(t => t.Text == text && t.IsEffectivelyVisible);

    private static T Named<T>(Control root, string name) where T : Control => root.GetVisualDescendants().OfType<T>().First(c => c.Name == name);

    [Theory]
    [InlineData("hell")]
    [InlineData("dunkel")]
    public Task Kein_Heute_Band_mehr_die_Gruppe_Notizen_heute_steht_ganz_oben_in_den_Meldungen(string theme) => Render(theme, "meldungen-notizen", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        await Pump();

        // Über dem Kalender steht kein Band „Heute“ mit „einklappen“ mehr, und die Notizen stehen auch nirgends sonst als Text im Fenster.
        Assert.DoesNotContain(window.GetVisualDescendants().OfType<TextBlock>(), t => t.Text is "einklappen" or "aufklappen");
        Assert.DoesNotContain(window.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == "Lieferung Massageöle gegen 11 Uhr, bitte im Lager einräumen");

        calendar.OpenMessagesCommand.Execute(null);
        await Pump(Settling);
        var panel = Named<Border>(window, "SidePanel");

        var title = TextIn(panel, "Notizen heute");
        var groupTitles = MessageGroupTitles
            .SelectMany(g => panel.GetVisualDescendants().OfType<TextBlock>().Where(t => t.Text == g && t.IsEffectivelyVisible))
            .ToList();
        Assert.NotEmpty(groupTitles);
        Assert.All(groupTitles, g => Assert.True(TopOf(window, title) < TopOf(window, g), $"{g.Text} steht über „Notizen heute“"));

        // Die Tagesnotizen ohne Uhrzeit, die erledigte durchgestrichen; der Zettel mit Uhrzeit bleibt in der Spalte.
        Assert.True(TopOf(window, TextIn(panel, "Lieferung Massageöle gegen 11 Uhr, bitte im Lager einräumen")) > TopOf(window, title));
        Assert.Contains("done", TextIn(panel, "Sauna-Wartung erledigt").Classes);
        Assert.DoesNotContain(panel.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == "Herr Lang ruft wegen Gutschein zurück");
        Assert.Equal("2", TextIn(panel, "2").Text);

        // „+ Notiz“ steht in der Zeile des Titels rechts; Ändern und Löschen haben Namen und Hinweise.
        var add = Named<Button>(panel, "AddNoteButton");
        Assert.Equal("+ Notiz", add.Content);
        Assert.True(Math.Abs(TopOf(window, add) + (add.Bounds.Height / 2) - (TopOf(window, title) + (title.Bounds.Height / 2))) < 4);
        Assert.True(add.TranslatePoint(new Point(0, 0), window)!.Value.X > title.TranslatePoint(new Point(0, 0), window)!.Value.X);
        var names = panel.GetVisualDescendants().OfType<Button>().Where(b => b.Classes.Contains("icon") && b.IsEffectivelyVisible)
            .Select(b => Avalonia.Automation.AutomationProperties.GetName(b)).Where(n => n?.StartsWith("Notiz ", StringComparison.Ordinal) == true).ToList();
        Assert.Equal(4, names.Count);
        Assert.Contains("Notiz ändern: Lieferung Massageöle gegen 11 Uhr, bitte …", names);
        Assert.Contains("Notiz löschen: Sauna-Wartung erledigt", names);
        return calendar;
    }, NotesApi());

    [Fact]
    public Task Ohne_Notizen_zeigt_die_Gruppe_nur_den_Knopf_Notiz() => Render("hell", "meldungen-ohne-notizen", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        calendar.OpenMessagesCommand.Execute(null);
        await Pump(Settling);
        var panel = Named<Border>(window, "SidePanel");

        Assert.False(calendar.HasDayNotes);
        Assert.Equal("Notizen heute", TextIn(panel, "Notizen heute").Text);
        Assert.True(Named<Button>(panel, "AddNoteButton").IsEffectivelyVisible);
        Assert.DoesNotContain(panel.GetVisualDescendants().OfType<CheckBox>(), c => c.IsEffectivelyVisible);
        return calendar;
    }, NoNotesApi());

    [Fact]
    public Task Der_Meldungen_Knopf_traegt_bei_Notizen_einen_Punkt_neben_dem_Zaehler_und_der_Zaehler_zaehlt_nur_Meldungen() => Render("hell", "meldungen-knopf-punkt", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        await Pump();
        var button = Named<Button>(window, "HeadMessages");
        var dot = Named<Icon>(button, "MessagesNoteDot");

        Assert.True(dot.IsEffectivelyVisible);
        Assert.Equal((10, 10), (dot.Bounds.Width, dot.Bounds.Height));
        Assert.Equal(ColorOf(window, "AccentBrush"), ColorOf(dot.Foreground));
        var counter = button.GetVisualDescendants().OfType<TextBlock>().First(t => t.IsEffectivelyVisible && t.Text!.StartsWith("Meldungen", StringComparison.Ordinal));
        Assert.True(dot.TranslatePoint(new Point(0, 0), window)!.Value.X > counter.TranslatePoint(new Point(counter.Bounds.Width, 0), window)!.Value.X - 0.5);
        Assert.Equal(calendar.Messages.Count > 0 ? $"Meldungen {calendar.Messages.Count}" : "Meldungen", counter.Text);
        Assert.Equal($"Meldungen {calendar.Messages.Count}, 2 Notizen heute", Avalonia.Automation.AutomationProperties.GetName(button));
        Assert.Contains("2 Notizen heute", ToolTip.GetTip(button) as string, StringComparison.Ordinal);

        // Der Punkt (Symbol, kein Text) braucht 3:1 gegen die Fläche des Knopfs in jedem Zustand (Warnung hat die weiche Warnfläche).
        Assert.True(Contrast(ColorOf(window, "AccentBrush"), ColorOf(window, "WarnSoftBrush")) >= 3, "Punkt auf der Warnfläche");
        Assert.True(Contrast(ColorOf(window, "AccentBrush"), ColorOf(window, "CritSoftBrush")) >= 3, "Punkt auf der Fehlerfläche");
        Assert.True(Contrast(ColorOf(window, "AccentBrush"), ColorOf(window, "SurfaceBrush")) >= 3, "Punkt auf dem normalen Knopf");
        return calendar;
    }, NotesApi());

    [Fact]
    public Task Ohne_Notizen_hat_der_Meldungen_Knopf_keinen_Punkt() => Render("hell", "meldungen-knopf-ohne-punkt", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        await Pump();
        var button = Named<Button>(window, "HeadMessages");

        Assert.False(Named<Icon>(button, "MessagesNoteDot").IsEffectivelyVisible);
        Assert.Equal("Meldungen", Avalonia.Automation.AutomationProperties.GetName(button));
        return calendar;
    }, NoNotesApi());

    [Fact]
    public Task Die_Taste_Punkt_oeffnet_die_Meldungen_mit_dem_Notizfeld_Strg_Enter_speichert_und_Esc_geht_in_Stufen() => Render("hell", "meldungen-notizfeld", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        await Pump();
        Assert.False(calendar.HasPanel);

        Press(window, PhysicalKey.Period);
        await Pump(Settling);

        Assert.True(calendar.MessagesPanel is { IsEditingNote: true });
        var box = Assert.IsType<TextBox>(Focused(window));
        Assert.Equal("DayNoteBox", box.Name);
        Assert.Equal((68, true), (box.Height, box.AcceptsReturn));
        Assert.Equal(500, box.MaxLength);
        Assert.Equal("Notiz speichern", Named<Border>(window, "SidePanel").GetVisualDescendants().OfType<Button>().First(b => b.IsDefault).Content);

        // Esc in Stufen: erst stellt es den Text zurück, dann schließt es nur das Feld, dann die Seitenleiste.
        window.KeyTextInput("Gruppe kommt um 14 Uhr");
        Assert.Equal("Gruppe kommt um 14 Uhr", calendar.MessagesPanel!.NoteText);
        Press(window, PhysicalKey.Escape);
        await Pump();
        Assert.Equal("", calendar.MessagesPanel.NoteText);
        Assert.True(calendar.MessagesPanel.IsEditingNote);
        Press(window, PhysicalKey.Escape);
        await Pump(Settling);
        Assert.False(calendar.MessagesPanel!.IsEditingNote);
        Assert.Equal("AddNoteButton", Assert.IsType<Button>(Focused(window)).Name);

        // Strg+Enter im Feld löst „Notiz speichern“ aus.
        Press(window, PhysicalKey.Enter);
        await Pump(Settling);
        Assert.True(calendar.MessagesPanel.IsEditingNote);
        window.KeyTextInput("Gruppe kommt um 14 Uhr");
        Press(window, PhysicalKey.Enter, RawInputModifiers.Control);
        await Pump(Settling);
        Assert.False(calendar.MessagesPanel.IsEditingNote);
        Assert.Contains(calendar.DayNotes, n => n.Text == "Gruppe kommt um 14 Uhr");
        Assert.Equal("AddNoteButton", Assert.IsType<Button>(Focused(window)).Name);

        Press(window, PhysicalKey.Escape);
        await Pump(Settling);
        Assert.Null(calendar.MessagesPanel);
        return calendar;
    }, NoNotesApi().On($"POST /api/locations/{SpaSample.LocationId}/calendar-notes",
        new CalendarNoteDto(Guid.NewGuid(), SpaSample.Day, "Gruppe kommt um 14 Uhr", null, null, null, false, SpaSample.At(11, 41)), System.Net.HttpStatusCode.Created));

    [Fact]
    public Task Notiz_schreiben_im_Feld_der_Meldungen_hell_und_dunkel_zeigt_Zaehler_und_Fehler() => Render("hell", "meldungen-notizfeld-offen", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        calendar.OpenDayNoteCommand.Execute(null);
        await Pump(Settling);
        var panel = calendar.MessagesPanel!;
        panel.NoteText = "Lieferung Öle um 11, Wartung Sauna";
        await Pump();

        var field = Named<Border>(window, "SidePanel").GetVisualDescendants().OfType<FormField>().Single(f => f.IsEffectivelyVisible);
        Assert.Equal("Notiz", field.Label);
        Assert.True(field.IsRequired);
        var counter = field.GetVisualDescendants().OfType<TextBlock>().First(t => t.Name == "PART_Counter");
        Assert.True(counter.IsEffectivelyVisible, string.Join("|", field.GetVisualDescendants().OfType<TextBlock>().Select(t => $"{t.Name}:{t.Text}:{t.IsEffectivelyVisible}")));
        var box = field.GetVisualDescendants().OfType<TextBox>().First();
        Assert.Equal("Lieferung Öle um 11, Wartung Sauna", box.Text);
        Assert.Equal("noch 466 Zeichen", counter.Text);

        panel.NoteText = "";
        await panel.SaveNoteAsync();
        await Pump();
        Assert.Equal("Bitte einen Text eingeben.", field.Message);
        Assert.Equal(FieldMessageKind.Error, field.MessageKind);
        return calendar;
    }, NoNotesApi());
}
