using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Controls;

/// <summary>
/// Zeile von Chips, die nie umbricht und nie scrollt (Design-Brief 7 „Kopfzeile des Kalenders“, Fluent 2 „Toolbar“): reicht die Breite nicht, wandern die Chips von rechts nach links
/// in ein Überlauf-Menü „⋯“ am rechten Ende der Zeile. Die Einträge des Menüs tragen Symbol und vollen Text in der Reihenfolge der Zeile und lösen dasselbe aus wie der Chip;
/// das Menü erreicht auch die Tastatur. Die Chips stehen je in einem eigenen Rahmen (Border), damit ihre eigenen Bindungen unberührt bleiben.
/// </summary>
public sealed class OverflowStrip : Panel
{
    /// <summary>Text des Menüeintrags, solange der Chip im Überlauf steht (der volle Text, auch wenn der Chip nur Symbol und Zahl zeigt).</summary>
    public static readonly AttachedProperty<string?> HeaderProperty = AvaloniaProperty.RegisterAttached<OverflowStrip, Control, string?>("Header");

    /// <summary>Pfaddaten des Symbols (16 px) im Menüeintrag; ohne Wert gibt es keins.</summary>
    public static readonly AttachedProperty<string?> IconProperty = AvaloniaProperty.RegisterAttached<OverflowStrip, Control, string?>("Icon");

    /// <summary>Der Chip steht am rechten Rand der Zeile (Personenfilter) und geht als erster in den Überlauf.</summary>
    public static readonly AttachedProperty<bool> PinRightProperty = AvaloniaProperty.RegisterAttached<OverflowStrip, Control, bool>("PinRight");

    /// <summary>Der Chip steht gerade im Überlauf: ohne Platz, unsichtbar und nicht mit Tab erreichbar.</summary>
    public static readonly AttachedProperty<bool> IsOverflowedProperty = AvaloniaProperty.RegisterAttached<OverflowStrip, Control, bool>("IsOverflowed");

    public static readonly StyledProperty<double> SpacingProperty = AvaloniaProperty.Register<OverflowStrip, double>(nameof(Spacing), 8);

    private readonly MoreButton _more;
    private Size? _moreSize;

    static OverflowStrip()
    {
        AffectsMeasure<OverflowStrip>(SpacingProperty);
        IsOverflowedProperty.Changed.AddClassHandler<Control>((control, e) =>
        {
            // Ein Chip im Überlauf ist weder zu sehen noch zu bedienen; die Bindungen des Chips selbst bleiben unberührt.
            var overflowed = e.NewValue is true;
            control.IsEnabled = !overflowed;
            control.Opacity = overflowed ? 0 : 1;
        });
    }

    public OverflowStrip()
    {
        var menu = new MenuFlyout { Placement = Avalonia.Controls.PlacementMode.BottomEdgeAlignedRight };
        _more = new MoreButton(() => FillMenu(menu)) { Name = "HeadOverflow" };
        _more.Classes.Add("headchip");
        _more.Content = new TextBlock { Text = "⋯", FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center };
        ToolTip.SetTip(_more, "Weitere Filter und Anzeigen");
        AutomationProperties.SetName(_more, "Weitere Filter und Anzeigen");
        _more.Flyout = menu;
        Children.Add(_more);
    }

    public double Spacing
    {
        get => GetValue(SpacingProperty);
        set => SetValue(SpacingProperty, value);
    }

    /// <summary>Das Menü „⋯“ am rechten Ende; nur sichtbar, solange etwas im Überlauf steht.</summary>
    public Button OverflowButton => _more;

    /// <summary>Die Chips im Überlauf in der Reihenfolge der Zeile (zuerst die linken, zuletzt der rechts angeheftete).</summary>
    public IReadOnlyList<Control> Overflowed => Chips().Where(GetIsOverflowed).OrderBy(c => GetPinRight(c)).ThenBy(c => Children.IndexOf(c)).ToList();

    public static string? GetHeader(Control control) => control.GetValue(HeaderProperty);

    public static void SetHeader(Control control, string? value) => control.SetValue(HeaderProperty, value);

    public static string? GetIcon(Control control) => control.GetValue(IconProperty);

    public static void SetIcon(Control control, string? value) => control.SetValue(IconProperty, value);

    public static bool GetPinRight(Control control) => control.GetValue(PinRightProperty);

    public static void SetPinRight(Control control, bool value) => control.SetValue(PinRightProperty, value);

    public static bool GetIsOverflowed(Control control) => control.GetValue(IsOverflowedProperty);

    private IEnumerable<Control> Chips() => Children.Where(c => !ReferenceEquals(c, _more));

    protected override Size MeasureOverride(Size availableSize)
    {
        var chips = Chips().ToList();
        foreach (var chip in chips)
        {
            chip.Measure(Size.Infinity);
        }

        // Die Größe des Menü-Chips ist konstant; sie wird einmal gemessen, solange er noch sichtbar ist, damit das Ein- und Ausblenden keine neue Messung auslöst.
        if (_moreSize is null)
        {
            _more.Measure(Size.Infinity);
            _moreSize = _more.DesiredSize;
        }

        var moreWidth = _moreSize.Value.Width;

        // Chips ohne Inhalt (der Rahmen enthält nur einen ausgeblendeten Chip) nehmen weder Platz noch Abstand.
        var present = chips.Where(c => c.DesiredSize.Width > 0).ToList();
        var left = present.Where(c => !GetPinRight(c)).ToList();
        var pinned = present.Where(GetPinRight).ToList();
        var kept = new List<Control>(left);
        var keptPinned = new List<Control>(pinned);
        double Width(IEnumerable<Control> items) => items.Sum(c => c.DesiredSize.Width) + (Spacing * Math.Max(0, items.Count() - 1));
        double Total() => Width([.. kept, .. keptPinned]);

        var fits = double.IsInfinity(availableSize.Width) || Total() <= availableSize.Width + 0.5;
        var overflow = new List<Control>();
        if (!fits)
        {
            // Von rechts nach links: zuerst der angeheftete Chip, dann die Chips vom Ende der Reihe, bis mit dem Menü „⋯“ alles passt.
            while ((keptPinned.Count > 0 || kept.Count > 0) && Total() + (kept.Count + keptPinned.Count > 0 ? Spacing : 0) + moreWidth > availableSize.Width + 0.5)
            {
                if (keptPinned.Count > 0)
                {
                    overflow.Add(keptPinned[^1]);
                    keptPinned.RemoveAt(keptPinned.Count - 1);
                }
                else
                {
                    overflow.Add(kept[^1]);
                    kept.RemoveAt(kept.Count - 1);
                }
            }
        }

        foreach (var chip in chips)
        {
            chip.SetValue(IsOverflowedProperty, overflow.Contains(chip));
        }

        if (_more.IsVisible != overflow.Count > 0)
        {
            _more.IsVisible = overflow.Count > 0;
        }

        var used = Total() + (overflow.Count > 0 ? (kept.Count + keptPinned.Count > 0 ? Spacing : 0) + moreWidth : 0);
        var height = chips.Count == 0 ? _moreSize.Value.Height : Math.Max(chips.Max(c => c.DesiredSize.Height), _moreSize.Value.Height);
        return new Size(double.IsInfinity(availableSize.Width) ? used : Math.Min(availableSize.Width, Math.Max(used, 0)), height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var x = 0.0;
        foreach (var chip in Chips())
        {
            if (GetIsOverflowed(chip) || chip.DesiredSize.Width <= 0)
            {
                chip.Arrange(new Rect(0, 0, 0, 0));
                continue;
            }

            if (GetPinRight(chip))
            {
                chip.Arrange(new Rect(Math.Max(x, finalSize.Width - chip.DesiredSize.Width), 0, chip.DesiredSize.Width, finalSize.Height));
                continue;
            }

            chip.Arrange(new Rect(x, 0, chip.DesiredSize.Width, finalSize.Height));
            x += chip.DesiredSize.Width + Spacing;
        }

        if (_more.IsVisible)
        {
            _more.Arrange(new Rect(Math.Max(x, finalSize.Width - _moreSize!.Value.Width), 0, _moreSize.Value.Width, finalSize.Height));
        }
        else
        {
            _more.Arrange(new Rect(0, 0, 0, 0));
        }

        return finalSize;
    }

    /// <summary>Baut die Einträge des Menüs aus den Chips im Überlauf: Symbol, voller Text, und dieselbe Wirkung wie der Chip.</summary>
    private void FillMenu(MenuFlyout menu)
    {
        menu.Items.Clear();
        foreach (var chip in Overflowed)
        {
            var item = new MenuItem { Header = GetHeader(chip) ?? InnerButton(chip)?.Content?.ToString() };
            if (GetIcon(chip) is { Length: > 0 } icon)
            {
                item.Icon = new Icon { Data = icon, Width = 16, Height = 16 };
            }

            if (chip is TabButton { IsSelected: true })
            {
                item.ToggleType = MenuItemToggleType.Radio;
                item.IsChecked = true;
            }

            var captured = chip;
            item.Click += (_, _) => Invoke(captured);
            menu.Items.Add(item);
        }
    }

    private static Button? InnerButton(Control chip) => chip as Button ?? chip.GetVisualDescendants().OfType<Button>().FirstOrDefault();

    /// <summary>Löst aus, was der Chip auslöst: sein Befehl, sonst sein Fenster (Tagesüberblick).</summary>
    private void Invoke(Control chip)
    {
        if (InnerButton(chip) is not { } button)
        {
            return;
        }

        if (button.Command is { } command && command.CanExecute(button.CommandParameter))
        {
            command.Execute(button.CommandParameter);
        }
        else if (button.Flyout is { } flyout)
        {
            flyout.ShowAt(_more);
        }
    }

    /// <summary>Füllt das Menü vor dem Öffnen, für Maus, Tastatur und Screenreader: Was erst in <c>Opening</c> hinzukommt, zeigt das Menü beim Öffnen nicht (leere Fläche, Slice 125).</summary>
    private sealed class MoreButton(Action beforeClick) : Button
    {
        protected override Type StyleKeyOverride => typeof(Button);

        protected override void OnClick()
        {
            if (Flyout is { IsOpen: false })
            {
                beforeClick();
            }

            base.OnClick();
        }
    }
}
