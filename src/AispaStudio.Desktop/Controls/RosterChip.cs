using AispaStudio.Desktop.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;

namespace AispaStudio.Desktop.Controls;

/// <summary>
/// Dienst-Chip im Dienstplan (R07 Slice 142, Ä350; dunkle Variante T-039, Ä372): die Fläche entsteht aus der Farbe der Person, die in die Basis <c>RosterChipBaseBrush</c> des wirksamen Farbschemas
/// gemischt wird (<see cref="RosterChipColors"/>). Die Mischung muss hier geschehen, weil die Farbe ein Datenwert ist und das Farbschema (auch „System“) erst in der Ansicht feststeht;
/// ein Wechsel hell/dunkel zur Laufzeit rechnet sie neu.
/// </summary>
public sealed class RosterChip : Border
{
    public static readonly StyledProperty<string?> PersonColorProperty = AvaloniaProperty.Register<RosterChip, string?>(nameof(PersonColor));

    public string? PersonColor
    {
        get => GetValue(PersonColorProperty);
        set => SetValue(PersonColorProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == PersonColorProperty || change.Property == ThemeVariantScope.ActualThemeVariantProperty)
        {
            Refresh();
        }
    }

    private void Refresh()
    {
        if (!TryFindColor("RosterChipBaseBrush", out var chipBase) || !TryFindColor("RosterChipInkBrush", out var ink))
        {
            return;
        }

        var share = ActualThemeVariant == ThemeVariant.Dark ? RosterChipColors.ShareDark : RosterChipColors.ShareLight;
        Background = new SolidColorBrush(Color.Parse(RosterChipColors.Fill(PersonColor ?? string.Empty, Hex(chipBase), Hex(ink), share)));
    }

    private bool TryFindColor(string key, out Color color)
    {
        if (this.TryFindResource(key, ActualThemeVariant, out var value) && value is ISolidColorBrush brush)
        {
            color = brush.Color;
            return true;
        }

        color = default;
        return false;
    }

    private static string Hex(Color color) => $"#{color.R:x2}{color.G:x2}{color.B:x2}";
}
