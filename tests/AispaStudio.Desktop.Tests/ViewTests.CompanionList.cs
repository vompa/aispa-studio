using System.Net;
using AispaStudio.Contracts;
using AispaStudio.Desktop.ViewModels;
using AispaStudio.Desktop.Views;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>
/// Slice 134 (Ä316): Listen-Antworten in „KI und Hilfe“ – Kopfsatz mit der Zahl, eine Liste einzeiliger, anklickbarer Zeilen, „Alle zeigen“; Fehler und leere Antworten
/// lassen die Frage im Feld und bieten „Erneut fragen“; Pfeil nach oben holt die letzte Frage. Als Inhaberin, mit echter Maus und echten Tasten.
/// </summary>
public sealed partial class ViewTests
{
    private const string LongTreatment = "Ganzkörper-Aromaölmassage mit Hot-Stone-Ritual und Kräuterstempel";

    /// <summary>15 Termine; der zweite ist der Termin „Brigitte Müller“ des Beispieltags, damit der Kalender ihn zeigen kann, der dritte hat einen langen Namen.</summary>
    private static AssistantAnswer FifteenAppointments()
    {
        var items = Enumerable.Range(0, 15).Select(i => CompanionListTests.Appointment(i, i == 2 ? LongTreatment : "Massage 60 Min.")).ToList();
        items[1] = new AssistantItem(SpaSample.Selected.CustomerName, "Lotus-Ritual · Lena Huber", "10:30",
            new AssistantSuggestion(AssistantSuggestionKind.OpenAppointment, SpaSample.Selected.CustomerName, null, null, null, null, SpaSample.Selected.Id,
                SpaSample.Selected.Start), null, "Im Kalender zeigen");
        return CompanionListTests.ListAnswer("Montag, 19. Oktober, Wien Innere Stadt: 15 Termine.", items);
    }

    private static StubApi ListApi() => Api().On("POST /api/assistant", FifteenAppointments());

    private static async Task<(ShellViewModel Shell, CompanionContent Panel)> CompanionOpen(Window window, MainViewModel main)
    {
        await SignedInCalendar(main);
        var shell = (ShellViewModel)main.Content!;
        shell.Companion.ToggleCommand.Execute(null);
        await Pump(Settling);
        return (shell, window.GetVisualDescendants().OfType<CompanionContent>().Single());
    }

    /// <summary>Tippt die Frage ins Feld der Leiste und schickt sie mit Enter ab.</summary>
    private static async Task AskByKeyboard(Window window, string question)
    {
        await TypeInto(window, "CompanionQuestion", question);
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        await Pump(Settling);
    }

    private static List<Button> AnswerRows(Control panel) =>
        [.. Named<ItemsControl>(panel, "AnswerRows").GetVisualDescendants().OfType<Button>().Where(b => b.IsEffectivelyVisible)];

    [Theory]
    [InlineData("hell")]
    [InlineData("dunkel")]
    public Task Termine_morgen_zeigt_Kopfsatz_zehn_Zeilen_und_Alle_zeigen(string theme) => Render(theme, "ki-antwort-liste", async (window, main) =>
    {
        var (shell, panel) = await CompanionOpen(window, main);
        await AskByKeyboard(window, "Termine morgen?");

        Assert.Contains("Montag, 19. Oktober, Wien Innere Stadt: 15 Termine.", TextsIn(panel));
        Assert.Equal(10, AnswerRows(panel).Count);
        Assert.Single(panel.GetVisualDescendants().OfType<ItemsControl>(), i => i.IsEffectivelyVisible && i.Name == "AnswerRows");
        Assert.DoesNotContain(panel.GetVisualDescendants().OfType<Button>(), b => b.IsEffectivelyVisible && b.Content as string == "Im Kalender zeigen");
        Assert.True(VisibleButton(panel, "Alle zeigen (15)").IsEffectivelyVisible);
        Assert.Equal("10:30, Brigitte Müller, Lotus-Ritual · Lena Huber. Im Kalender zeigen", AutomationPropertiesName(AnswerRows(panel)[1]));

        // Für das Bild im Handbuch: Maus weg von den Zeilen, damit kein Tooltip und keine Hervorhebung darüber liegt.
        window.MouseMove(new Point(4, 4));
        await Pump(Settling);
        return shell.Companion;
    }, ListApi());

    [Fact]
    public Task Alle_zeigen_klappt_die_Liste_an_Ort_und_Stelle_auf() => Render("hell", "ki-antwort-liste-alle", async (window, main) =>
    {
        var (shell, panel) = await CompanionOpen(window, main);
        await AskByKeyboard(window, "Termine morgen?");

        await ClickIntoView(window, VisibleButton(panel, "Alle zeigen (15)"));
        await Pump(Settling);

        Assert.Equal(15, AnswerRows(panel).Count);
        Assert.DoesNotContain(panel.GetVisualDescendants().OfType<Button>(), b => b.IsEffectivelyVisible && b.Content as string == "Alle zeigen (15)");
        Assert.Same(shell.Calendar, shell.SelectedPage?.Page);
        return shell.Companion;
    }, ListApi());

    [Fact]
    public Task Klick_auf_eine_Terminzeile_zeigt_den_Termin_im_Kalender() => Render("hell", "ki-antwort-zeile-klick", async (window, main) =>
    {
        var (shell, panel) = await CompanionOpen(window, main);
        await AskByKeyboard(window, "Termine morgen?");
        Assert.Null(shell.Calendar.MarkedId);

        await ClickIntoView(window, AnswerRows(panel)[1]);
        await Pump(Settling);

        Assert.Same(shell.Calendar, shell.SelectedPage?.Page);
        Assert.Equal(SpaSample.Selected.Id, shell.Calendar.MarkedId);
        return shell.Calendar;
    }, ListApi());

    [Theory]
    [InlineData("hell")]
    [InlineData("dunkel")]
    public Task Fehler_zeigt_InfoBar_mit_Erneut_fragen_und_die_Frage_bleibt(string theme) => Render(theme, "ki-antwort-fehler", async (window, main) =>
    {
        var (shell, panel) = await CompanionOpen(window, main);
        await AskByKeyboard(window, "Termine morgen?");

        Assert.Equal("Termine morgen?", Named<TextBox>(panel, "CompanionQuestion").Text);
        Assert.True(Named<StackPanel>(panel, "ErrorBar").IsEffectivelyVisible);
        Assert.Contains("Der Assistent konnte die Frage gerade nicht beantworten.", TextsIn(panel));
        Assert.True(VisibleButton(panel, "Erneut fragen").IsEffectivelyVisible);
        Assert.False(Named<Border>(panel, "AnswerCard").IsEffectivelyVisible);
        return shell.Companion;
    }, Api().On("POST /api/assistant", null, HttpStatusCode.InternalServerError));

    [Fact]
    public Task Leere_Antwort_zeigt_die_Meldung_statt_einer_leeren_Karte() => Render("hell", "ki-antwort-leer", async (window, main) =>
    {
        var (shell, panel) = await CompanionOpen(window, main);
        await AskByKeyboard(window, "Kaffee?");

        Assert.Equal("Kaffee?", Named<TextBox>(panel, "CompanionQuestion").Text);
        Assert.Contains(CompanionViewModel.EmptyAnswer, TextsIn(panel));
        Assert.False(Named<Border>(panel, "AnswerCard").IsEffectivelyVisible);
        return shell.Companion;
    }, Api().On("POST /api/assistant", new AssistantAnswer(AssistantAudience.Management, "", [], [], "Antwort einer KI.", [])));

    [Fact]
    public Task Pfeil_nach_oben_im_leeren_Feld_holt_die_letzte_Frage() => Render("hell", "ki-antwort-pfeil-oben", async (window, main) =>
    {
        var (shell, panel) = await CompanionOpen(window, main);
        await AskByKeyboard(window, "Termine morgen?");
        var field = Named<TextBox>(panel, "CompanionQuestion");
        Assert.Equal("", field.Text ?? "");
        field.Focus();
        await Pump();

        window.KeyPressQwerty(PhysicalKey.ArrowUp, RawInputModifiers.None);
        await Pump();

        Assert.Equal("Termine morgen?", field.Text);
        Assert.Equal(field.Text!.Length, field.CaretIndex);
        return shell.Companion;
    }, ListApi());

    [Fact]
    public Task Langer_Behandlungsname_bleibt_einzeilig_mit_Auslassung_und_Tooltip() => Render("hell", "ki-antwort-lang", async (window, main) =>
    {
        var (shell, panel) = await CompanionOpen(window, main);
        await AskByKeyboard(window, "Termine morgen?");

        var detail = AnswerRows(panel)[2].GetVisualDescendants().OfType<TextBlock>().Single(t => t.Text == $"{LongTreatment} · Lena");
        Assert.Single(detail.TextLayout.TextLines);
        Assert.True(detail.TextLayout.TextLines[0].HasCollapsed);
        Assert.Equal(detail.Text, ToolTip.GetTip(detail));
        return shell.Companion;
    }, ListApi());

    [Fact]
    public Task Nach_einem_Fehler_steht_die_Meldung_im_sichtbaren_Bereich()
    {
        var stub = ListApi();
        return Render("hell", "ki-antwort-sichtbar", async (window, main) =>
        {
            var (shell, panel) = await CompanionOpen(window, main);
            await AskByKeyboard(window, "Termine morgen?");
            shell.Companion.ShowAllRowsCommand.Execute(null);
            await Pump(Settling);
            var scroller = panel.GetVisualAncestors().OfType<ScrollViewer>().First();
            scroller.ScrollToEnd();
            await Pump(Settling);
            var errorBar = Named<StackPanel>(panel, "ErrorBar");
            stub.On("POST /api/assistant", null, HttpStatusCode.InternalServerError);

            shell.Companion.Question = "Termine übermorgen?";
            await shell.Companion.AskCommand.ExecuteAsync(null);
            await Pump(Settling);

            Assert.True(errorBar.IsEffectivelyVisible);
            var top = errorBar.TranslatePoint(new Point(0, 0), scroller)!.Value.Y;
            Assert.InRange(top, 0, scroller.Viewport.Height - 1);
            return shell.Companion;
        }, stub);
    }

    [Fact]
    public Task Tab_geht_durch_die_Antwortzeilen_und_Enter_oeffnet() => Render("hell", "ki-antwort-tab", async (window, main) =>
    {
        var (shell, panel) = await CompanionOpen(window, main);
        await AskByKeyboard(window, "Termine morgen?");
        var rows = AnswerRows(panel);
        rows[0].Focus(NavigationMethod.Tab);
        await Pump();

        window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.None);
        await Pump();
        Assert.Same(rows[1], TopLevel.GetTopLevel(panel)!.FocusManager!.GetFocusedElement());

        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        await Pump(Settling);

        Assert.Equal(SpaSample.Selected.Id, shell.Calendar.MarkedId);
        return shell.Calendar;
    }, ListApi());
}
