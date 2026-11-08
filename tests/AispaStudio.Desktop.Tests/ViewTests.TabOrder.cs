using AispaStudio.Desktop.Controls;
using AispaStudio.Desktop.ViewModels;
using AispaStudio.Desktop.Views;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>
/// Tab-Reihenfolge der Masken (R04 Slice 83, Ä146, Ä147): Tab erreicht jedes bedienbare Element genau einmal, vorwärts wie rückwärts, in der Reihenfolge der Anzeige.
/// </summary>
public sealed partial class ViewTests
{
    [Fact]
    public Task Given_Neuer_Termin_mit_gewaehltem_Gast_When_Tab_von_der_Kundensuche_Then_folgen_Aendern_Vorschlag_Behandlung_Freie_Zeit_Datum_Beginn_Notiz_Termin_buchen() =>
        Render("hell", "fokus-neuer-termin", async (window, main) =>
        {
            var calendar = await SignedInCalendar(main);
            calendar.StartBookingAt(10, calendar.Hours.Single(h => h.Label == "12:00").Top + 1);
            var booking = calendar.Booking!;
            // Wie an der Rezeption: suchen und den Treffer in der Liste anklicken (die Liste merkt sich ihn als Tab-Ziel und verschwindet danach).
            booking.CustomerQuery = "mül";
            for (var i = 0; i < 40 && booking.Customers.Count == 0; i++)
            {
                await Pump();
            }

            await Pump();
            var hit = SidePanelOf(window).GetVisualDescendants().OfType<ListBoxItem>().First(item => item.IsEffectivelyVisible);
            ClickCenter(window, hit);
            await Pump();
            Assert.True(booking.HasChosenCustomer);
            for (var i = 0; i < 40 && !booking.HasSuggestions; i++)
            {
                await Pump();
            }

            Assert.True(booking.HasWishSuggestion);
            await Pump(Settling);
            var panel = SidePanelOf(window);
            panel.GetVisualDescendants().OfType<SearchField>().Single().Focus(NavigationMethod.Tab);
            await Pump();

            var order = await TabOrder(window, panel);
            WriteFocusOrder("neuer-termin", order);
            Assert.Equal(["Kundin oder Kunde suchen (Edit)", "Ändern (Button)", "Wunsch in die Notiz übernehmen (Button)", "Behandlung (ComboBox)", "Freie Zeit suchen (Button)",
                "Datum (Edit), Pflicht", "Beginn (Edit), Pflicht", "Notiz (Edit)", "Termin buchen (Button)"], order.Take(9));
            return calendar;
        });

    /// <summary>
    /// Given eine Liste, in der ein Eintrag mit der Tastatur den Fokus hatte When die Liste ausgeblendet wird Then vergisst sie ihn als Tab-Ziel, und Tab läuft am
    /// Knopf davor vorbei weiter zum Knopf danach (Ä146, Weg über die Tastatur; der Klick-Weg steht im Test zu „Neuer Termin“).
    /// </summary>
    [Fact]
    public Task Given_eine_Liste_mit_gemerktem_Eintrag_When_sie_ausgeblendet_wird_Then_laeuft_Tab_weiter() => Session.Dispatch(async () =>
    {
        var before = new Button { Content = "Davor" };
        var after = new Button { Content = "Danach" };
        var list = new ListBox { ItemsSource = new List<string> { "Eins", "Zwei" } };
        var window = new Window { Width = 400, Height = 300, Content = new StackPanel { Children = { before, list, after } } };
        window.Show();
        await Pump();
        var item = list.GetVisualDescendants().OfType<ListBoxItem>().First();
        item.Focus(NavigationMethod.Tab);
        await Pump();
        Assert.Same(item, KeyboardNavigation.GetTabOnceActiveElement(list));

        list.IsVisible = false;
        before.Focus(NavigationMethod.Tab);
        await Pump();
        Press(window, PhysicalKey.Tab);
        await Pump(2);

        Assert.Null(KeyboardNavigation.GetTabOnceActiveElement(list));
        Assert.Same(after, Focused(window));
        window.Close();
        return true;
    }, CancellationToken.None);

    /// <summary>
    /// Given eine Liste mit gewähltem zweitem Eintrag When sie aus- und wieder eingeblendet wird Then führt Tab wieder auf den gewählten Eintrag, nicht auf den ersten
    /// (das Vergessen des Tab-Ziels darf die Auswahl nicht übergehen).
    /// </summary>
    [Fact]
    public Task Given_eine_Liste_mit_Auswahl_When_sie_aus_und_wieder_eingeblendet_wird_Then_fuehrt_Tab_auf_den_gewaehlten_Eintrag() => Session.Dispatch(async () =>
    {
        var before = new Button { Content = "Davor" };
        var list = new ListBox { ItemsSource = new List<string> { "Eins", "Zwei", "Drei" }, SelectedIndex = 1 };
        var window = new Window { Width = 400, Height = 300, Content = new StackPanel { Children = { before, list, new Button { Content = "Danach" } } } };
        window.Show();
        await Pump();
        var chosen = list.ContainerFromIndex(1)!;
        chosen.Focus(NavigationMethod.Tab);
        await Pump();

        list.IsVisible = false;
        await Pump();
        list.IsVisible = true;
        await Pump();
        before.Focus(NavigationMethod.Tab);
        await Pump();
        Press(window, PhysicalKey.Tab);
        await Pump(2);

        Assert.Same(chosen, Focused(window));
        window.Close();
        return true;
    }, CancellationToken.None);

    /// <summary>Ein Element, das Tab erreichen muss: sichtbar, bedienbar, fokussierbar, Tab-Stopp und nicht in einem Bereich, den Tab überspringt (<see cref="KeyboardNavigationMode.None"/>).</summary>
    private static bool IsTabTarget(Control control) =>
        control.IsEffectivelyVisible && control.IsEffectivelyEnabled && control.Focusable && KeyboardNavigation.GetIsTabStop(control)
        && control.Bounds is { Width: > 0, Height: > 0 }
        && !control.GetVisualAncestors().OfType<Control>().Any(a => KeyboardNavigation.GetTabNavigation(a) == KeyboardNavigationMode.None);

    /// <summary>Gruppe eines Elements: ein Bereich mit <see cref="KeyboardNavigationMode.Once"/> (Liste, Reiterleiste) ist für Tab ein einziger Stopp, sonst das Element selbst.</summary>
    private static Control TabGroup(Control control) =>
        control.GetVisualAncestors().OfType<Control>().LastOrDefault(a => KeyboardNavigation.GetTabNavigation(a) == KeyboardNavigationMode.Once) ?? control;

    private static string Described(Control control) => $"{Announced(control)} #{control.Name} [{control.GetType().Name}]";

    /// <summary>Drückt Tab (oder Umschalt+Tab), bis der Fokus wieder in der Gruppe des Anfangs steht; <c>Complete</c> ist falsch, wenn er es nach <paramref name="limit"/> Schritten nicht tut (hängt oder kreist in einem Teil).</summary>
    private static async Task<(List<Control> Cycle, bool Complete, bool Lost)> TabCycle(Window window, Control start, bool backwards, int limit = 250)
    {
        start.Focus(NavigationMethod.Tab);
        await Pump();
        var cycle = new List<Control> { start };
        for (var step = 0; step < limit; step++)
        {
            Press(window, PhysicalKey.Tab, backwards ? RawInputModifiers.Shift : RawInputModifiers.None);
            await Pump(2);
            if (Focused(window) is not Control current)
            {
                return (cycle, false, true);
            }

            if (ReferenceEquals(TabGroup(current), TabGroup(start)))
            {
                return (cycle, true, false);
            }

            if (ReferenceEquals(current, cycle[^1]))
            {
                // Der Fokus bewegt sich nicht mehr: Tab hängt.
                return (cycle, false, false);
            }

            cycle.Add(current);
        }

        return (cycle, false, false);
    }

    /// <summary>Bereiche, in denen Tab der Lage im Bild folgen muss: Seitenleiste, Dialog und Kundendaten-Maske (oben nach unten, links nach rechts).</summary>
    private static Control? FormOf(Control control) =>
        control.GetVisualAncestors().OfType<Control>().FirstOrDefault(a => a.Name is "SidePanel" or "DialogCard" || a is CustomerContactForm);

    /// <summary>
    /// Tab springt zurück nach oben, ohne in eine Spalte weiter rechts zu gehen. Verglichen wird nur im selben Scrollbereich: die Reiter unten in der Seitenleiste liegen
    /// außerhalb und stehen über einem Feld, das im langen Formular unter dem sichtbaren Teil liegt.
    /// </summary>
    private static bool JumpsBack(Control from, Control to, Visual root)
    {
        if (!ReferenceEquals(from.FindAncestorOfType<ScrollViewer>(), to.FindAncestorOfType<ScrollViewer>()))
        {
            return false;
        }

        if (from.TranslatePoint(default, root) is not { } a || to.TranslatePoint(default, root) is not { } b)
        {
            return false;
        }

        return b.Y < a.Y - 4 && b.X < a.X + from.Bounds.Width;
    }

    /// <summary>
    /// Prüft eine Maske: Tab und Umschalt+Tab laufen ganz durch, jedes Ziel genau einmal, rückwärts in umgekehrter Folge, in Formularen in der Lage im Bild; gibt die
    /// Abweichungen zurück. <paramref name="scope"/> grenzt auf einen Bereich ein, den Tab nicht verlässt (Dialog).
    /// </summary>
    private static async Task<List<string>> TabProblems(Window window, string mask, Control? scope = null)
    {
        var problems = new List<string>();
        var targets = (scope ?? window).GetVisualDescendants().OfType<Control>().Where(IsTabTarget).ToList();
        if (targets.Count == 0)
        {
            return [$"{mask}: kein Tab-Ziel"];
        }

        var (forward, forwardComplete, forwardLost) = await TabCycle(window, targets[0], backwards: false);
        var (backward, backwardComplete, backwardLost) = await TabCycle(window, targets[0], backwards: true);
        if (!forwardComplete || !backwardComplete)
        {
            var stuck = forwardComplete ? backward : forward;
            var why = (forwardComplete ? backwardLost : forwardLost) ? "der Fokus geht verloren" : "Tab kommt nicht zum Anfang zurück";
            problems.Add($"{mask}: {why} ({(forwardComplete ? "rückwärts" : "vorwärts")}, Anfang {Described(targets[0])}), zuletzt: "
                + string.Join(" → ", stuck.TakeLast(8).Select(Described)));
            return problems;
        }

        // Tab erreicht nur Ziele: nichts Unsichtbares, nichts ohne Fläche und im Dialog nichts außerhalb (Fokusfalle).
        foreach (var stray in forward.Concat(backward).Distinct().Where(c => !IsTabTarget(c) || (scope is not null && !scope.IsVisualAncestorOf(c))))
        {
            problems.Add($"{mask}: erreicht, ist aber kein Ziel{(scope is not null && !scope.IsVisualAncestorOf(stray) ? " (außerhalb des Dialogs)" : "")}: {Described(stray)}");
        }

        WriteFocusOrder($"tab-{mask}", forward.Select(Described));
        foreach (var twice in forward.GroupBy(TabGroup).Where(g => g.Count() > 1))
        {
            problems.Add($"{mask}: mehrfach erreicht: {Described(twice.Key)} ({twice.Count()}×)");
        }

        var reached = forward.Select(TabGroup).ToHashSet();
        foreach (var missing in targets.Select(TabGroup).Distinct().Where(g => !reached.Contains(g)))
        {
            problems.Add($"{mask}: nicht erreicht: {Described(missing)}");
        }

        var reversed = backward.Skip(1).Reverse().Prepend(backward[0]).ToList();
        if (!forward.Select(TabGroup).SequenceEqual(reversed.Select(TabGroup)))
        {
            var at = forward.Zip(reversed).TakeWhile(p => ReferenceEquals(TabGroup(p.First), TabGroup(p.Second))).Count();
            problems.Add($"{mask}: rückwärts andere Folge ab Schritt {at + 1}: vorwärts {(at < forward.Count ? Described(forward[at]) : "Ende")}, rückwärts {(at < reversed.Count ? Described(reversed[at]) : "Ende")}");
        }

        foreach (var (from, to) in forward.Zip(forward.Skip(1)))
        {
            if (FormOf(from) is { } form && ReferenceEquals(form, FormOf(to)) && JumpsBack(from, to, form))
            {
                problems.Add($"{mask}: springt nach oben: {Described(from)} → {Described(to)}");
            }
        }

        return problems;
    }

    [Fact]
    public Task Jede_Seite_Tab_und_Umschalt_Tab_erreichen_jedes_bedienbare_Element_genau_einmal() => Session.Dispatch(async () =>
    {
        var problems = new List<string>();
        await WithApp(async (window, shell) =>
        {
            shell.Companion.IsOpen = false;
            await Pump(Settling);
            foreach (var page in shell.Pages)
            {
                shell.SelectedPage = page;
                await Pump(Settling);
                problems.AddRange(await TabProblems(window, page.Title));
            }

            // KI und Hilfe rechts steht auf jeder Seite; einmal offen im Kalender.
            shell.SelectedPage = shell.Pages[0];
            shell.Companion.IsOpen = true;
            await Pump(Settling);
            problems.AddRange(await TabProblems(window, $"{shell.Pages[0].Title}, KI und Hilfe"));
        });

        Assert.True(problems.Count == 0, $"{problems.Count} Abweichungen:{Environment.NewLine}{string.Join(Environment.NewLine, problems)}");
        return true;
    }, CancellationToken.None);

    /// <summary>Die Seitenleisten des Kalenders, wie die Rezeption sie öffnet; „Neuer Termin“ mit einem in der Trefferliste angeklickten Gast (Ä146).</summary>
    private static readonly (string Name, Func<Window, CalendarViewModel, Task> Open)[] CalendarPanels =
    [
        ("Termin", (_, calendar) =>
        {
            calendar.OpenAppointment(SpaSample.Selected);
            return Task.CompletedTask;
        }),
        ("Neuer Termin", async (window, calendar) =>
        {
            calendar.StartBookingAt(10, calendar.Hours.Single(h => h.Label == "12:00").Top + 1);
            calendar.Booking!.CustomerQuery = "mül";
            for (var i = 0; i < 40 && calendar.Booking.Customers.Count == 0; i++)
            {
                await Pump();
            }

            await Pump();
            ClickCenter(window, SidePanelOf(window).GetVisualDescendants().OfType<ListBoxItem>().First(item => item.IsEffectivelyVisible));
        }),
        ("Freie Zeit suchen", (_, calendar) => Execute(calendar.OpenFreeTimeCommand)),
        ("Gruppe", (_, calendar) => Execute(calendar.OpenGroupBookingCommand)),
        ("Eintritt", (_, calendar) => Execute(calendar.OpenAreasCommand)),
        ("Schließzeiten", (_, calendar) => Execute(calendar.OpenClosuresCommand)),
        ("Tagesnotiz", (_, calendar) => Execute(calendar.OpenDayNoteCommand)),
        ("Meldungen", (_, calendar) => Execute(calendar.OpenMessagesCommand)),
        ("Warteliste", (_, calendar) => Execute(calendar.OpenWaitlistCommand)),
        ("KI-Anfrage", (_, calendar) => Execute(calendar.OpenRequestCommand)),
        ("Tasten", (_, calendar) => Execute(calendar.OpenKeyManualCommand)),
    ];

    private static Task Execute(System.Windows.Input.ICommand command)
    {
        command.Execute(null);
        return Task.CompletedTask;
    }

    [Fact]
    public Task Jede_Seite_Seitenleisten_und_Dialog_Tab_und_Umschalt_Tab_erreichen_jedes_bedienbare_Element_genau_einmal() => Session.Dispatch(async () =>
    {
        var problems = new List<string>();
        foreach (var (name, open) in CalendarPanels)
        {
            await WithApp(async (window, shell) =>
            {
                shell.Companion.IsOpen = false;
                await Pump(Settling);
                await open(window, shell.Calendar);
                await Pump(Settling);
                problems.AddRange(await TabProblems(window, $"Kalender, {name}"));
            });
        }

        await WithApp(async (window, shell) =>
        {
            shell.Companion.IsOpen = false;
            await shell.ShowCustomerAsync(SpaSample.Customers[0].Id);
            await Pump();
            shell.Customers.EditContactCommand.Execute(null);
            await Pump(Settling);
            problems.AddRange(await TabProblems(window, "Kunden, Kundendaten"));

            var main = (MainViewModel)window.DataContext!;
            var asking = main.DialogHost.AskAsync(DeleteRequest);
            await Pump(Settling);
            problems.AddRange(await TabProblems(window, "Rückfragedialog", Dialog(window)));
            main.DialogHost.Current!.CancelCommand.Execute(null);
            await asking;
        });

        Assert.True(problems.Count == 0, $"{problems.Count} Abweichungen:{Environment.NewLine}{string.Join(Environment.NewLine, problems)}");
        return true;
    }, CancellationToken.None);
}
