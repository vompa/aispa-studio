using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>R05 Slice 99, Prüfbefunde: dieselbe Zelle noch einmal anklicken, Fokus beim Öffnen und nach abgelehntem Speichern (masken.md, R8 und R13), Abstände und Breite der Tabellenzellen.</summary>
public sealed partial class ViewTests
{
    private static string? FocusedName(Window window) => Focused(window) is Control focused ? AutomationProperties.GetName(focused) : null;

    /// <summary>Das erste Eingabefeld des Tages in der Reihenfolge am Bildschirm; die Art-Leiste ist ein Umschalter und zählt nicht.</summary>
    private static Control FirstField(Window window, string editor) =>
        VisibleNamed<Control>(window, editor).GetVisualDescendants().OfType<Control>().First(c => c.IsEffectivelyVisible && c is ComboBox or TextBox);

    [Fact]
    public Task Given_ein_Tag_im_Plan_mit_Eingabe_When_dieselbe_Zelle_noch_einmal_geklickt_wird_Then_bleibt_die_Eingabe_ohne_Rueckfrage()
    {
        var (api, _) = RosterApi();
        return Render("hell", "verwaltung-team-wochenplan-zweiter-klick", async (window, main) =>
        {
            var team = await OpenWeekPlan(window, main);
            await ClickCell(window, "Lena Huber", "Di.");
            await TypeInto(window, "DayNote", "Dienst getauscht");

            await ClickCell(window, "Lena Huber", "Di.");

            Assert.Equal("Dienst getauscht", Named<TextBox>(window, "DayNote").Text);
            Assert.True(team.HasUnsavedChanges);
            Assert.Null(main.DialogHost.Current);
            return team;
        }, api);
    }

    [Fact]
    public Task Given_ein_Tag_in_den_Zeiten_mit_Eingabe_When_dieselbe_Zelle_noch_einmal_geklickt_wird_Then_bleibt_die_Eingabe_ohne_Rueckfrage()
    {
        var (api, _, _) = TimesApi();
        return Render("hell", "verwaltung-team-zeiten-zweiter-klick", async (window, main) =>
        {
            var team = await OpenTimes(window, main);
            await ClickTimesCell(window, "Lena Huber", "Mi.");
            await TypeIntoReason(window, "Stempeln vergessen");

            await ClickTimesCell(window, "Lena Huber", "Mi.");

            Assert.Equal("Stempeln vergessen", VisibleNamed<TextBox>(window, "TimeReason").Text);
            Assert.True(team.HasUnsavedChanges);
            Assert.Null(main.DialogHost.Current);
            return team;
        }, api);
    }

    [Fact]
    public Task Given_ein_Dienst_am_Tag_When_die_Zelle_geklickt_wird_Then_steht_der_Fokus_im_ersten_Feld_des_Tages()
    {
        var (api, _) = RosterApi();
        return Render("hell", "verwaltung-team-wochenplan-fokus", async (window, main) =>
        {
            var team = await OpenWeekPlan(window, main);

            await ClickCell(window, "Lena Huber", "Di.");

            Assert.Same(FirstField(window, "DayEditor"), Focused(window));
            Assert.IsType<ComboBox>(Focused(window));
            return team;
        }, api);
    }

    [Fact]
    public Task Given_der_naechste_Tag_When_die_andere_Zelle_geklickt_wird_Then_wandert_der_Fokus_in_den_neuen_Tag()
    {
        var (api, _) = RosterApi();
        return Render("hell", "verwaltung-team-wochenplan-fokus-wechsel", async (window, main) =>
        {
            var team = await OpenWeekPlan(window, main);
            await ClickCell(window, "Lena Huber", "Di.");

            await ClickCell(window, "Lena Huber", "Mi.");

            Assert.Equal("Lena Huber, Mi. 21.10.", Named<TextBlock>(window, "DayHeading").Text);
            Assert.Same(FirstField(window, "DayEditor"), Focused(window));
            return team;
        }, api);
    }

    [Theory]
    [InlineData(null, 17, "Dienst 1 Beginn")]
    [InlineData(9, null, "Dienst 1 Ende")]
    [InlineData(18, 17, "Dienst 1 Beginn")]
    public Task Given_ein_ungueltiger_Dienst_When_Strg_S_gedrueckt_wird_Then_steht_der_Fokus_im_falschen_Feld(int? start, int? end, string field)
    {
        var (api, _) = RosterApi();
        return Render("hell", $"verwaltung-team-wochenplan-fokus-{field.Replace(' ', '-')}-{start}", async (window, main) =>
        {
            var team = await OpenWeekPlan(window, main);
            await ClickCell(window, "Lena Huber", "Di.");
            await TypeInto(window, "DayNote", "Dienst getauscht");
            var shift = team.Plan!.Day!.Shifts[0];
            shift.Start = start is { } s ? new TimeOnly(s, 0) : null;
            shift.End = end is { } e ? new TimeOnly(e, 0) : null;

            PressSave(window);
            await Pump(Settling);

            Assert.True(team.HasUnsavedChanges);
            Assert.Equal(field, FocusedName(window));
            return team;
        }, api);
    }

    [Fact]
    public Task Given_ein_Tag_ohne_Zeiten_When_die_Zelle_geklickt_wird_Then_steht_der_Fokus_im_Beginn()
    {
        var (api, _, _) = TimesApi();
        return Render("hell", "verwaltung-team-zeiten-fokus", async (window, main) =>
        {
            var team = await OpenTimes(window, main);

            await ClickTimesCell(window, "Lena Huber", "Mi.");

            Assert.Equal("Beginn", FocusedName(window));
            return team;
        }, api);
    }

    [Fact]
    public Task Given_ein_Tag_mit_Zeiten_When_Aendern_geklickt_wird_Then_steht_der_Fokus_im_Beginn()
    {
        var (api, _, _) = TimesApi();
        return Render("hell", "verwaltung-team-zeiten-fokus-aendern", async (window, main) =>
        {
            var team = await OpenTimes(window, main);
            await ClickTimesCell(window, "Lena Huber", "Di.");
            Assert.NotEqual("Beginn", FocusedName(window));

            var change = window.GetVisualDescendants().OfType<Button>().First(b => b.IsEffectivelyVisible && AutomationProperties.GetName(b) == "Abschnitt 09:00–12:30 ändern");
            await ClickButton(window, change);

            Assert.Equal("Beginn", FocusedName(window));
            return team;
        }, api);
    }

    [Fact]
    public Task Given_ein_Abschnitt_ohne_Grund_When_Strg_S_gedrueckt_wird_Then_steht_der_Fokus_im_Grund()
    {
        var (api, _, _) = TimesApi();
        return Render("hell", "verwaltung-team-zeiten-fokus-grund", async (window, main) =>
        {
            var team = await OpenTimes(window, main);
            await ClickTimesCell(window, "Lena Huber", "Di.");
            var change = window.GetVisualDescendants().OfType<Button>().First(b => b.IsEffectivelyVisible && AutomationProperties.GetName(b) == "Abschnitt 09:00–12:30 ändern");
            await ClickButton(window, change);
            await TypeIntoVisibleTime(window, "Ende", "13:00");

            PressSave(window);
            await Pump(Settling);

            Assert.Same(VisibleNamed<TextBox>(window, "TimeReason"), Focused(window));
            return team;
        }, api);
    }

    [Fact]
    public Task Given_kein_Beginn_und_kein_Grund_When_Strg_S_gedrueckt_wird_Then_steht_der_Fokus_im_Beginn()
    {
        var (api, _, _) = TimesApi();
        return Render("hell", "verwaltung-team-zeiten-fokus-beginn", async (window, main) =>
        {
            var team = await OpenTimes(window, main);
            await ClickTimesCell(window, "Lena Huber", "Mi.");
            await TypeIntoVisibleTime(window, "Ende", "13:00");
            team.Times!.Day!.Start = null;
            await Pump(Settling);
            Assert.NotEqual("Beginn", FocusedName(window));

            PressSave(window);
            await Pump(Settling);

            Assert.Equal("Beginn", FocusedName(window));
            return team;
        }, api);
    }

    private static void AssertRoomyCell(ContentControl cell)
    {
        Assert.Equal(new Thickness(12, 8), cell.Padding);
        Assert.Equal(new Thickness(4), cell.Margin);
    }

    /// <summary>Eine Textzeile ist nicht umgebrochen, wenn sie nicht höher ist als zwei halbe Zeilen, und nicht abgeschnitten, wenn sie in ihre Zelle passt.</summary>
    private static void AssertOneLine(TextBlock text, Control cell)
    {
        Assert.True(text.Bounds.Height < 2 * text.FontSize, $"„{text.Text}“ ist {text.Bounds.Height} hoch");
        Assert.True(text.Bounds.Width <= cell.Bounds.Width, $"„{text.Text}“ ist breiter als die Zelle");
    }

    [Fact]
    public Task Given_der_Wochenplan_When_er_auf_der_Karte_steht_Then_haben_die_Zellen_die_Abstaende_und_die_Tabelle_laesst_sich_scrollen()
    {
        var (api, _) = RosterApi();
        return Render("hell", "verwaltung-team-karte-dienstplan-abstaende", async (window, main) =>
        {
            var team = await OpenTeam(window, main);
            await ClickRow(window, "Lena Huber");
            await ClickSection(window, "Dienstplan und Abwesenheit");
            await Pump(Settling);

            var cell = RosterCellButton(window, "Lena Huber", "Di.");
            // Seit Slice 142 füllt der Chip die Zelle: schmaler Innenabstand und Rand, dafür bricht die Zeit nicht um und wird nicht abgeschnitten.
            Assert.Equal(new Thickness(4, 6), cell.Padding);
            Assert.Equal(new Thickness(2), cell.Margin);
            AssertOneLine(cell.GetVisualDescendants().OfType<TextBlock>().First(t => t.Text == "09:00–17:00"), cell);
            var table = window.GetVisualDescendants().OfType<Control>().First(c => c.Name == "RosterTable" && c.IsEffectivelyVisible);
            var scroll = table.GetVisualAncestors().OfType<ScrollViewer>().First();
            Assert.True(scroll.Extent.Width >= table.Bounds.Width);
            return team;
        }, api);
    }

    [Fact]
    public Task Given_der_Wochenplan_When_er_die_ganze_Breite_hat_Then_gibt_es_nichts_zu_scrollen()
    {
        var (api, _) = RosterApi();
        return Render("hell", "verwaltung-team-wochenplan-breite", async (window, main) =>
        {
            var team = await OpenWeekPlan(window, main);

            var table = window.GetVisualDescendants().OfType<Control>().First(c => c.Name == "RosterTable" && c.IsEffectivelyVisible);
            var scroll = table.GetVisualAncestors().OfType<ScrollViewer>().First();
            Assert.True(scroll.Extent.Width <= scroll.Viewport.Width, $"Tabelle {scroll.Extent.Width}, Platz {scroll.Viewport.Width}");
            return team;
        }, api);
    }

    [Fact]
    public Task Given_die_Zeiten_When_sie_auf_der_Karte_stehen_Then_haben_die_Zellen_die_Abstaende_und_die_Zeile_bricht_nicht_um()
    {
        var (api, _, _) = TimesApi();
        return Render("hell", "verwaltung-team-karte-zeiten-abstaende", async (window, main) =>
        {
            var team = await OpenTeam(window, main);
            await ClickRow(window, "Lena Huber");
            await ClickCardTab(window, "Zeiten");
            await Pump(Settling);

            var cell = TimesCellButton(window, "Lena Huber", "Di.");
            AssertRoomyCell(cell);
            AssertOneLine(cell.GetVisualDescendants().OfType<TextBlock>().First(t => (t.Text ?? "").StartsWith("8 / ", StringComparison.Ordinal)), cell);
            return team;
        }, api);
    }
}
