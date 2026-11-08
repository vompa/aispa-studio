using AispaStudio.Desktop.Controls;
using AispaStudio.Desktop.Services;
using AispaStudio.Desktop.ViewModels;
using AispaStudio.Desktop.Views;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Styling;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>Fluent 2 F22: Bewegungswerte nach Design-Brief 4.6 und „Animationen aus“ ohne jeden Übergang.</summary>
public sealed partial class ViewTests
{
    private static IReadOnlyList<ITransition> TransitionsOf(Animatable animatable) => [.. animatable.Transitions ?? []];

    /// <summary>Dauer eines Übergangs; die Übergangsklassen von Avalonia sind je Werttyp eigene generische Typen ohne gemeinsame Basis mit <c>Duration</c>.</summary>
    private static TimeSpan DurationOf(ITransition transition) =>
        (TimeSpan)transition.GetType().GetProperty(nameof(Transition<double>.Duration))!.GetValue(transition)!;

    private static string? PropertyOf(ITransition transition) =>
        (transition.GetType().GetProperty(nameof(Transition<double>.Property))!.GetValue(transition) as AvaloniaProperty)?.Name;

    private static Easing? EasingOf(ITransition transition) =>
        (Easing?)transition.GetType().GetProperty(nameof(Transition<double>.Easing))!.GetValue(transition);

    private static void AssertTransition<T>(ITransition transition, int milliseconds, double x1, double y1, double x2, double y2)
    {
        Assert.IsType<T>(transition);
        AssertTransition(transition, milliseconds, x1, y1, x2, y2);
    }

    private static void AssertTransition(ITransition transition, int milliseconds, double x1, double y1, double x2, double y2)
    {
        Assert.Equal(TimeSpan.FromMilliseconds(milliseconds), DurationOf(transition));
        var spline = Assert.IsType<SplineEasing>(EasingOf(transition));
        Assert.Equal((x1, y1, x2, y2), (spline.X1, spline.Y1, spline.X2, spline.Y2));
    }

    /// <summary>Teil der Vorlage eines Steuerelements.</summary>
    private static T TemplatePart<T>(Control owner, string name)
        where T : Control =>
        owner.GetVisualDescendants().OfType<T>().First(c => c.Name == name);

    [Fact]
    public Task Bewegung_Hover_Fokus_und_Druck_100_ms_mit_easy_ease() => InWindow("hell", () => new StackPanel { Children = { new Button { Content = "Speichern" }, new ToggleButton { Content = "Sauna" } } },
        360, 160, async window =>
        {
            await Pump();
            foreach (Control control in ((StackPanel)window.Content!).Children)
            {
                var press = Assert.Single(TransitionsOf(control));
                AssertTransition<TransformOperationsTransition>(press, 100, 0.33, 0, 0.67, 1);

                var surface = TemplatePart<ContentPresenter>(control, "PART_ContentPresenter");
                var colors = TransitionsOf(surface);
                Assert.Equal(["Background", "BorderBrush", "Foreground"], colors.Select(PropertyOf));
                foreach (var color in colors)
                {
                    AssertTransition<BrushTransition>(color, 100, 0.33, 0, 0.67, 1);
                }
            }
        });

    [Fact]
    public Task Bewegung_Meldung_unten_160_ms_ein_120_ms_aus_Seitenleiste_200_ms_ein_120_ms_aus() => InWindow("hell", () => new StackPanel
    {
        Children =
        {
            new Border { Name = "Toast", Classes = { "toast" } },
            new Border { Name = "Panel", Classes = { "sidepanel" } },
        },
    }, 360, 160, async window =>
    {
        var panel = (StackPanel)window.Content!;
        var toast = (Border)panel.Children[0];
        var side = (Border)panel.Children[1];
        await Pump();

        // Aus: (1,0,1,1), 120 ms.
        Assert.All(TransitionsOf(toast), t => AssertTransition(t, 120, 1, 0, 1, 1));
        Assert.All(TransitionsOf(side), t => AssertTransition(t, 120, 1, 0, 1, 1));

        // Ein: (0,0,0,1), 160 ms bei der Meldung und 200 ms bei der Seitenleiste.
        toast.Classes.Add("shown");
        side.Classes.Add("open");
        await Pump();
        Assert.Equal(2, TransitionsOf(toast).Count);
        Assert.All(TransitionsOf(toast), t => AssertTransition(t, 160, 0, 0, 0, 1));
        Assert.Equal(2, TransitionsOf(side).Count);
        Assert.All(TransitionsOf(side), t => AssertTransition(t, 200, 0, 0, 0, 1));
    });

    /// <summary>
    /// Alle Übergänge der gerade gezeigten Oberfläche, die länger als 0 ms dauern, als Liste „Typ#Name [Klassen]: Eigenschaft Dauer“.
    /// Verborgene Teile zählen nicht (der Schalter von Avalonia setzt am unsichtbaren Teil <c>PART_MovingKnobs</c> im Code einen 200-ms-Übergang, den die Vorlage der App nicht zeigt).
    /// </summary>
    private static List<string> RunningTransitions(Visual root) =>
        [.. root.GetSelfAndVisualDescendants().Where(v => v.IsEffectivelyVisible).OfType<Animatable>()
            .SelectMany(a => TransitionsOf(a).Where(t => DurationOf(t) > TimeSpan.Zero).Select(t => $"{a.GetType().Name}#{(a as Control)?.Name} [{string.Join(" ", (a as StyledElement)?.Classes ?? [])}]: {PropertyOf(t)} {DurationOf(t).TotalMilliseconds} ms"))
            .Distinct()];

    /// <summary>Öffnet die App wie im Betrieb, ruft <paramref name="act"/> mit dem Fenster auf und schließt es wieder.</summary>
    private static async Task WithApp(Func<Window, ShellViewModel, Task> act)
    {
        Application.Current!.RequestedThemeVariant = ThemeVariant.Light;
        var main = new MainViewModel(Api().Client(null), time: new SampleTime(), local: null);
        var window = new MainWindow { DataContext = main, Width = 1440, Height = 900 };
        window.Show();
        var calendar = await SignedInCalendar(main);
        var shell = (ShellViewModel)main.Content!;
        calendar.OpenAppointment(SpaSample.Selected);
        shell.Companion.IsOpen = true;
        await Pump(Settling);
        await act(window, shell);
        window.Close();
    }

    [Fact]
    public Task Animationen_aus_setzt_alle_Uebergaenge_jeder_Seite_der_App_auf_0() => Session.Dispatch(async () =>
    {
        // Ohne die Stile gibt es Übergänge, sonst bewiese der Test nichts.
        await WithApp((window, _) =>
        {
            Assert.NotEmpty(RunningTransitions(window));
            return Task.CompletedTask;
        });

        // Wie im Betrieb: die Stile stehen in der Anwendung, bevor das erste Fenster entsteht (App.OnFrameworkInitializationCompleted).
        var overrides = ReducedMotion.Overrides().ToList();
        Application.Current!.Styles.AddRange(overrides);
        try
        {
            await WithApp(async (window, shell) =>
            {
                foreach (var page in shell.Pages)
                {
                    shell.SelectedPage = page;
                    await Pump(Settling);
                    var running = RunningTransitions(window);
                    Assert.True(running.Count == 0, $"{page.Title}: {string.Join("; ", running)}");
                }
            });
        }
        finally
        {
            foreach (var style in overrides)
            {
                Application.Current!.Styles.Remove(style);
            }
        }

        return true;
    }, CancellationToken.None);

    /// <summary>
    /// Die Lage auf dem Runner von GitHub: Windows meldet „Animationen aus“. Die App streicht dann beim Start alle Übergänge;
    /// ohne den Haken <see cref="ReducedMotion.SystemSource"/> zählten die Bewegungstests auf dem Runner leere Sammlungen.
    /// </summary>
    [Fact]
    public Task Meldet_das_System_Animationen_aus_entfallen_die_Uebergaenge_beim_Start() => Session.Dispatch(async () =>
    {
        // Die Testbasis stellt „an“ ein: beim Start der App kamen keine Stile hinzu.
        Assert.False(ReducedMotion.IsRequested());
        var untouched = new Styles();
        ReducedMotion.Apply(untouched);
        Assert.Empty(untouched);

        var input = new TextBox { Text = "Maria Muster" };
        var window = await ShowAsync("hell", input);
        var underline = Assert.Single(Strips(input, FieldChrome.UnderlineClass));
        Assert.NotEmpty(underline.Transitions!);

        var before = ReducedMotion.SystemSource;
        ReducedMotion.SystemSource = () => true;
        try
        {
            Assert.True(ReducedMotion.IsRequested());
            var overrides = new Styles();
            ReducedMotion.Apply(overrides);
            Assert.Equal(ReducedMotion.Overrides().Count(), overrides.Count);

            window.Styles.AddRange(overrides.ToList());
            await Pump();
            Assert.Empty(underline.Transitions!);
        }
        finally
        {
            ReducedMotion.SystemSource = before;
        }

        window.Close();
        return true;
    }, CancellationToken.None);
}
