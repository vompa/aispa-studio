using System.ComponentModel;
using AispaStudio.Desktop.Services;
using AispaStudio.Desktop.ViewModels;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Views;

/// <summary>
/// Startbildschirm (Design-Brief 5.31): die Petrolfläche füllt das Fenster, bis die API bereit ist; danach schrumpft sie auf die Navigationsleiste bzw.
/// die Markenspalte der Anmeldung, Kachel und Name wandern an ihren Platz, und die Ansicht dahinter blendet ein. Ein Fenster, das selbst nicht animiert wird.
/// </summary>
public partial class StartupScreen : UserControl
{
    public static readonly StyledProperty<Control?> TargetProperty = AvaloniaProperty.Register<StartupScreen, Control?>(nameof(Target));

    /// <summary>Fortschritt des Übergangs von 0 (Mitte, volle Fläche) bis 1 (Leiste bzw. Markenspalte); daraus folgen Breite und Lage der Teile.</summary>
    private static readonly StyledProperty<double> HandoverProperty = AvaloniaProperty.Register<StartupScreen, double>("Handover");

    // Kurven aus Design-Brief 4.6: „decelerate mid“ für Eintretendes, „accelerate mid“ für Austretendes.
    private static readonly SplineEasing Decelerate = new(0, 0, 0, 1);
    private static readonly SplineEasing Accelerate = new(1, 0, 1, 1);

    private const string RunningClass = "running";

    // Länge der Wellen im 24er-Raster geteilt durch die Strichstärke 2: Striche zählen in Vielfachen der Strichstärke.
    private const double Wave1Length = 10.1;
    private const double Wave2Length = 9.7;

    private MainViewModel? _viewModel;
    private Task _intro = Task.CompletedTask;
    private bool _introduced;
    private bool _leaving;
    private bool _wasOffline;
    private Action<double>? _handover;

    public StartupScreen()
    {
        InitializeComponent();
    }

    /// <summary>Die Ansicht dahinter: sie blendet nach dem Übergang ein; ihre Leiste bzw. Markenspalte (<c>BrandSurface</c>, <c>BrandTile</c>, <c>BrandName</c>) ist das Ziel.</summary>
    public Control? Target
    {
        get => GetValue(TargetProperty);
        set => SetValue(TargetProperty, value);
    }

    /// <summary>Lage von Fläche und Kachel im Fenster, sobald sie an ihrem Platz angekommen sind; für Tests.</summary>
    internal event Action<Rect, Rect>? HandedOver;

    private static bool Reduced => ReducedMotion.IsRequested();

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelChanged;
        }

        _viewModel = DataContext as MainViewModel;
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged += OnViewModelChanged;
        }

        Update();
    }

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MainViewModel.IsStarting) or nameof(MainViewModel.IsStartupScreenShown) or nameof(MainViewModel.IsOffline))
        {
            Update();
        }
    }

    private void Update()
    {
        if (_viewModel is not { } vm || _leaving)
        {
            return;
        }

        if (!vm.IsStarting && !vm.IsOffline)
        {
            _wasOffline = false;
            StopBar();
            if (!IsVisible)
            {
                return;
            }

            // Unter 300 ms war nur die Fläche zu sehen: die Ansicht steht sofort da, ohne Übergang.
            if (vm.IsStartupScreenShown)
            {
                _ = LeaveAsync();
            }
            else
            {
                IsVisible = false;
            }

            return;
        }

        IsVisible = true;
        // Vor 300 ms nur die Fläche; die Karte ohne Verbindung steht auch dann, wenn der Startbildschirm nie zu sehen war.
        Tile.IsVisible = BrandName.IsVisible = vm.IsStartupScreenShown;
        Status.IsVisible = vm.IsStarting && vm.IsStartupScreenShown;
        if (vm.IsStartupScreenShown && !_introduced)
        {
            _introduced = true;
            _intro = IntroduceAsync();
        }
        else if (vm.IsStarting && _introduced && !Reduced)
        {
            StartBar();
        }

        if (!vm.IsStarting)
        {
            StopBar();
        }

        if (vm.IsOffline && !_wasOffline)
        {
            // Erst jetzt gibt es etwas zu bedienen.
            Dispatcher.UIThread.Post(() => Retry.Focus(), DispatcherPriority.Loaded);
        }

        _wasOffline = vm.IsOffline;
    }

    /// <summary>Die Wellen zeichnen sich in 600 ms (die zweite 120 ms später), danach der Tropfen; Name und Stand erscheinen in 200 ms.</summary>
    private Task IntroduceAsync()
    {
        Bar.IsVisible = !Reduced;
        if (Reduced)
        {
            return Task.CompletedTask;
        }

        Wave1.StrokeDashArray = [Wave1Length, Wave1Length];
        Wave2.StrokeDashArray = [Wave2Length, Wave2Length];
        if (_viewModel?.IsStarting == true)
        {
            StartBar();
        }

        return Task.WhenAll(
            Animate(Wave1, Shape.StrokeDashOffsetProperty, Wave1Length, 0, 600, Decelerate),
            Animate(Wave2, Shape.StrokeDashOffsetProperty, Wave2Length, 0, 600, Decelerate, delay: 120),
            Animate(Drop, OpacityProperty, 0, 1, 200, Decelerate, delay: 560),
            Animate(BrandName, OpacityProperty, 0, 1, 200, Decelerate, delay: 500),
            Animate(Status, OpacityProperty, 0, 1, 200, Decelerate, delay: 600));
    }

    /// <summary>Unbestimmter Balken (Stil <c>Border#BarSegment.running</c>): läuft, solange gewartet wird.</summary>
    private void StartBar() => BarSegment.Classes.Add(RunningClass);

    private void StopBar() => BarSegment.Classes.Remove(RunningClass);

    /// <summary>
    /// Der Stand blendet in 120 ms aus, die Fläche schrumpft in 400 ms auf ihr Ziel, Kachel und Name wandern mit (ohne Ziel blendet der Name aus);
    /// danach blendet die Ansicht in 160 ms über der Fläche ein, die gleich gefärbte Leiste deckt sich mit ihr. Vorher bleibt das Logo, bis es gezeichnet ist.
    /// </summary>
    private async Task LeaveAsync()
    {
        _leaving = true;
        await _intro.ConfigureAwait(true);
        var target = Target;
        if (target is not null)
        {
            target.Opacity = 0;
        }

        // Die Zielansicht ist eben erst gekommen: einmal messen, damit Leiste, Kachel und Name ihre Lage haben.
        TopLevel.GetTopLevel(this)?.UpdateLayout();
        var steps = new List<Action<double>>();
        if (Find(target, "BrandSurface") is { } surface)
        {
            var (from, to) = (Bounds.Width, surface.Bounds.Width);
            Surface.HorizontalAlignment = HorizontalAlignment.Left;
            steps.Add(p => Surface.Width = from + (to - from) * p);
        }

        if (Find(target, "BrandTile") is { } tile)
        {
            steps.Add(MoveTo(Tile, tile, scales: true));
        }

        var name = Find(target, "BrandName");
        if (name is not null)
        {
            steps.Add(MoveTo(BrandName, name, scales: false));
        }

        _handover = p => steps.ForEach(step => step(p));
        await Task.WhenAll(
            Animate(Status, OpacityProperty, 1, 0, 120, Accelerate),
            name is null ? Animate(BrandName, OpacityProperty, 1, 0, 120, Accelerate) : Task.CompletedTask,
            Animate(this, HandoverProperty, 0, 1, 400, Decelerate)).ConfigureAwait(true);
        _handover = null;

        UpdateLayout();
        HandedOver?.Invoke(PlaceOf(Surface), PlaceOf(Tile));
        ZIndex = -1;
        if (target is not null)
        {
            await Animate(target, OpacityProperty, 0, 1, 160, Decelerate).ConfigureAwait(true);
        }

        IsVisible = false;
        Reset();
        _leaving = false;
        Update();
    }

    /// <summary>
    /// Schritt, der ein Teil des Startbildschirms auf die Lage des Gegenstücks in der Zielansicht schiebt (0 = Mitte, 1 = Ziel), die Kachel auch auf seine Größe.
    /// Der Name behält seine Größe: beide sind 34 px, nur der Rahmen des Ziels ist so breit wie seine Spalte.
    /// </summary>
    private Action<double> MoveTo(Control piece, Control goal, bool scales)
    {
        var from = PlaceOf(piece);
        var to = PlaceOf(goal);
        var scale = scales ? to.Width / from.Width : 1;
        return p =>
        {
            var k = 1 + (scale - 1) * p;
            piece.RenderTransform = new MatrixTransform(Matrix.CreateScale(k, k) * Matrix.CreateTranslation((to.X - from.X) * p, (to.Y - from.Y) * p));
        };
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == HandoverProperty)
        {
            _handover?.Invoke(change.GetNewValue<double>());
        }
    }

    private void Reset()
    {
        Surface.ClearValue(WidthProperty);
        Surface.ClearValue(HorizontalAlignmentProperty);
        Tile.RenderTransform = null;
        BrandName.RenderTransform = null;
        BrandName.Opacity = 1;
        Status.Opacity = 1;
        ZIndex = 0;
    }

    private static Control? Find(Control? root, string name) =>
        root?.GetVisualDescendants().OfType<Control>().FirstOrDefault(c => c.Name == name && c.IsEffectivelyVisible);

    /// <summary>Lage im Fenster samt Render-Transformation.</summary>
    private Rect PlaceOf(Visual visual)
    {
        var top = (Visual?)TopLevel.GetTopLevel(this) ?? this;
        var topLeft = visual.TranslatePoint(default, top) ?? default;
        var bottomRight = visual.TranslatePoint(new Point(visual.Bounds.Width, visual.Bounds.Height), top) ?? default;
        return new Rect(topLeft, bottomRight);
    }

    /// <summary>Ein Wert von <paramref name="from"/> nach <paramref name="to"/>; bei „Animationen aus“ (4.6) steht er sofort am Ziel.</summary>
    private static async Task Animate(Animatable target, StyledProperty<double> property, double from, double to, int milliseconds, Easing easing, int delay = 0)
    {
        target.SetValue(property, from);
        if (!Reduced)
        {
            var animation = new Animation
            {
                Duration = TimeSpan.FromMilliseconds(milliseconds),
                Delay = TimeSpan.FromMilliseconds(delay),
                Easing = easing,
                Children = { Frame(0, property, from), Frame(1, property, to) },
            };
            await animation.RunAsync(target).ConfigureAwait(true);
        }

        target.SetValue(property, to);
    }

    private static KeyFrame Frame(double cue, AvaloniaProperty property, double value) =>
        new() { Cue = new Cue(cue), Setters = { new Setter(property, value) } };
}
