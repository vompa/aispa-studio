using System.Windows.Input;
using AispaStudio.Desktop.ViewModels;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;

namespace AispaStudio.Desktop.Controls;

/// <summary>
/// Zeitbalken im Termin-Panel (Design-Brief 5.12, Punkt 5): ein waagrechter Balken, 10 px hoch, dessen Abschnitte so breit sind wie ihre Minuten
/// (Vorbereitung, Behandlung, Nachbereitung, Puffer, Raumpflege); über jedem steht die Minutenzahl. Ein Klick auf Vor- oder Nachbereitung führt
/// <see cref="EditCommand"/> aus, sofern <see cref="CanEdit"/> gilt. Farben und Schraffur stehen in Styles.axaml („c|TimeBar“).
/// </summary>
public sealed class TimeBar : Grid
{
    /// <summary>Höhe des Balkens.</summary>
    private const double BarHeight = 10;

    public static readonly StyledProperty<IReadOnlyList<TimeBarSegment>?> SegmentsProperty =
        AvaloniaProperty.Register<TimeBar, IReadOnlyList<TimeBarSegment>?>(nameof(Segments));

    public static readonly StyledProperty<ICommand?> EditCommandProperty = AvaloniaProperty.Register<TimeBar, ICommand?>(nameof(EditCommand));

    public static readonly StyledProperty<bool> CanEditProperty = AvaloniaProperty.Register<TimeBar, bool>(nameof(CanEdit));

    /// <summary>Status des Termins (booked, checkedin, done, noshow); der Behandlungsabschnitt trägt die Farbe der Terminkarte.</summary>
    public static readonly StyledProperty<string?> StatusProperty = AvaloniaProperty.Register<TimeBar, string?>(nameof(Status));

    static TimeBar()
    {
        SegmentsProperty.Changed.AddClassHandler<TimeBar>((bar, _) => bar.Rebuild());
        CanEditProperty.Changed.AddClassHandler<TimeBar>((bar, _) => bar.Rebuild());
        StatusProperty.Changed.AddClassHandler<TimeBar>((bar, e) =>
        {
            if (e.OldValue is string old)
            {
                bar.Classes.Remove(old);
            }

            if (e.NewValue is string status)
            {
                bar.Classes.Add(status);
            }
        });
    }

    public TimeBar()
    {
        RowSpacing = 4;
        ColumnSpacing = 1;
        RowDefinitions = new RowDefinitions("Auto,Auto");
    }

    public IReadOnlyList<TimeBarSegment>? Segments
    {
        get => GetValue(SegmentsProperty);
        set => SetValue(SegmentsProperty, value);
    }

    /// <summary>Was ein Klick auf Vor- oder Nachbereitung tut (Zahlenfelder an Ort und Stelle öffnen).</summary>
    public ICommand? EditCommand
    {
        get => GetValue(EditCommandProperty);
        set => SetValue(EditCommandProperty, value);
    }

    /// <summary>Wer buchen darf, ändert die Zeiten; sonst sind die Abschnitte nur zum Ansehen.</summary>
    public bool CanEdit
    {
        get => GetValue(CanEditProperty);
        set => SetValue(CanEditProperty, value);
    }

    public string? Status
    {
        get => GetValue(StatusProperty);
        set => SetValue(StatusProperty, value);
    }

    private void Rebuild()
    {
        Children.Clear();
        ColumnDefinitions.Clear();
        var segments = Segments ?? [];
        for (var i = 0; i < segments.Count; i++)
        {
            var segment = segments[i];
            ColumnDefinitions.Add(new ColumnDefinition(segment.Fraction, GridUnitType.Star) { MinWidth = 1 });
            var minutes = new TextBlock
            {
                Text = segment.Minutes.ToString(System.Globalization.CultureInfo.InvariantCulture),
                HorizontalAlignment = HorizontalAlignment.Center,
                ClipToBounds = false,
                IsHitTestVisible = false,
            };
            minutes.Classes.Add("tbmin");
            SetColumn(minutes, i);
            Children.Add(minutes);

            var bar = new Border
            {
                Height = BarHeight,
                Tag = segment,
                CornerRadius = new CornerRadius(i == 0 ? 4 : 0, i == segments.Count - 1 ? 4 : 0, i == segments.Count - 1 ? 4 : 0, i == 0 ? 4 : 0),
            };
            bar.Classes.Add("tbseg");
            bar.Classes.Add(segment.Kind.ToString().ToLowerInvariant());
            if (segment.Tone is { } tone)
            {
                bar.Classes.Add(tone);
            }

            ToolTip.SetTip(bar, segment.Tip);
            AutomationProperties.SetName(bar, segment.Tip);
            if (segment.IsEditable && CanEdit)
            {
                bar.Classes.Add("editable");
                bar.Cursor = new Cursor(StandardCursorType.Hand);
                bar.PointerPressed += (_, e) =>
                {
                    if (e.GetCurrentPoint(bar).Properties.IsLeftButtonPressed && EditCommand?.CanExecute(null) == true)
                    {
                        EditCommand.Execute(null);
                        e.Handled = true;
                    }
                };
            }

            SetRow(bar, 1);
            SetColumn(bar, i);
            Children.Add(bar);
        }
    }
}
