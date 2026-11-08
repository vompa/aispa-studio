using AispaStudio.Desktop.ViewModels;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;

namespace AispaStudio.Desktop.Controls;

/// <summary>
/// Zeitleiste einer Person im Tagesstart (R07 Slice 144, Ä352): eine Leiste von Öffnung bis Schluss, auf der die Dienstabschnitte als Balken liegen. Die Leiste zeigt nur an; die Zeiten
/// stehen daneben als Text, damit nichts allein an Farbe oder Länge hängt. Farben stehen in Styles.axaml („c|StaffBar“).
/// </summary>
public sealed class StaffBar : Grid
{
    public static readonly StyledProperty<IReadOnlyList<StaffSpan>?> SpansProperty = AvaloniaProperty.Register<StaffBar, IReadOnlyList<StaffSpan>?>(nameof(Spans));

    static StaffBar() => SpansProperty.Changed.AddClassHandler<StaffBar>((bar, _) => bar.Rebuild());

    public StaffBar()
    {
        Height = 10;
        AutomationProperties.SetAccessibilityView(this, AccessibilityView.Raw);
    }

    /// <summary>Die Dienstabschnitte als Anteil der Leiste (0 bis 1), in beliebiger Reihenfolge.</summary>
    public IReadOnlyList<StaffSpan>? Spans
    {
        get => GetValue(SpansProperty);
        set => SetValue(SpansProperty, value);
    }

    private void Rebuild()
    {
        Children.Clear();
        ColumnDefinitions.Clear();
        var parts = new List<(double Weight, bool Filled)>();
        var cursor = 0d;
        foreach (var span in (Spans ?? []).OrderBy(s => s.Start))
        {
            var start = Math.Clamp(span.Start, cursor, 1);
            var end = Math.Clamp(span.End, start, 1);
            if (end <= start)
            {
                continue;
            }

            if (start > cursor)
            {
                parts.Add((start - cursor, false));
            }

            parts.Add((end - start, true));
            cursor = end;
        }

        if (cursor < 1)
        {
            parts.Add((1 - cursor, false));
        }

        foreach (var (weight, _) in parts)
        {
            ColumnDefinitions.Add(new ColumnDefinition(weight, GridUnitType.Star));
        }

        var lane = new Border { Classes = { "lane" } };
        SetColumnSpan(lane, Math.Max(1, parts.Count));
        Children.Add(lane);
        for (var i = 0; i < parts.Count; i++)
        {
            if (parts[i].Filled)
            {
                var span = new Border { Classes = { "span" } };
                SetColumn(span, i);
                Children.Add(span);
            }
        }
    }
}
