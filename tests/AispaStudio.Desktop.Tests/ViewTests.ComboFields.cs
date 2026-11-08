using AispaStudio.Desktop.Controls;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>Aufklappliste (F9, Design-Brief 5.3): Stil wie ein Eingabefeld, Liste mit Radius 8 und 32 hohen Einträgen, Tastatur wie in WinUI.</summary>
public sealed partial class ViewTests
{
    private static readonly string[] ComboItems = ["Eins", "Zwei", "Drei", "Vier"];

    private static readonly string[] LongComboItems = [.. Enumerable.Range(1, 14).Select(i => FormattableString.Invariant($"Eintrag {i}"))];

    private static ComboBox Choice(int selected = 1) => new() { ItemsSource = ComboItems, SelectedIndex = selected };

    private static async Task OpenAsync(Window window, ComboBox combo)
    {
        combo.Focus(NavigationMethod.Tab);
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        await Pump(Settling);
    }

    [Theory]
    [InlineData("hell")]
    [InlineData("dunkel")]
    public Task Aufklappliste_ist_ein_Eingabefeld_mit_Unterkante_Fokus_Unterstrich_und_Liste_mit_Radius_8(string theme) => Session.Dispatch(async () =>
    {
        var anchor = new Button { Content = "Anker" };
        var combo = Choice();
        var window = await ShowAsync(theme, anchor, combo);

        Assert.Equal(34, combo.Bounds.Height);
        Assert.Equal(new CornerRadius(8), combo.CornerRadius);
        var edge = Assert.Single(Strips(combo, FieldChrome.EdgeClass));
        var underline = Assert.Single(Strips(combo, FieldChrome.UnderlineClass));
        Assert.Equal(Resolve(combo, "Fg3Brush"), ((ISolidColorBrush)edge.BorderBrush!).Color);
        var surface = combo.GetVisualDescendants().OfType<Border>().First(b => b.Name == "Background");
        Assert.Equal(Resolve(combo, "SurfaceBrush"), ((ISolidColorBrush)surface.Background!).Color);
        Assert.Equal(Resolve(combo, "LineStrongBrush"), ((ISolidColorBrush)surface.BorderBrush!).Color);

        // Hover: Rahmen und Unterkante fg-2.
        var at = combo.TranslatePoint(new Point(40, 17), window)!.Value;
        window.MouseMove(at);
        await Pump(Settling);
        Assert.Equal(Resolve(combo, "Fg2Brush"), ((ISolidColorBrush)surface.BorderBrush!).Color);
        Assert.Equal(Resolve(combo, "Fg2Brush"), ((ISolidColorBrush)edge.BorderBrush!).Color);
        SaveFieldShot(window, $"aufklappliste-hover-{theme}");

        // Tastaturfokus: Unterstrich und Rahmen accent, Fläche bleibt surface (die Vorlage füllt sie sonst in der Akzentfarbe), Ring außen.
        window.MouseMove(new Point(380, 540));
        anchor.Focus(NavigationMethod.Pointer);
        combo.Focus(NavigationMethod.Tab);
        await Pump(Settling);
        Assert.Contains("on", underline.Classes);
        Assert.Equal(Resolve(combo, "AccentBrush"), ((ISolidColorBrush)surface.BorderBrush!).Color);
        Assert.Equal(Resolve(combo, "SurfaceBrush"), ((ISolidColorBrush)surface.Background!).Color);
        SaveFieldShot(window, $"aufklappliste-fokus-tastatur-{theme}");
        Assert.False(combo.GetVisualDescendants().OfType<Border>().First(b => b.Name == "HighlightBackground").IsVisible);
        Assert.Single(RingsOf(combo));

        // Fokus per Maus: Unterstrich ja, Ring nein.
        anchor.Focus(NavigationMethod.Pointer);
        await Pump(Settling);
        Assert.DoesNotContain("on", underline.Classes);
        combo.Focus(NavigationMethod.Pointer);
        await Pump(Settling);
        Assert.Contains("on", underline.Classes);
        Assert.Empty(RingsOf(combo));
        SaveFieldShot(window, $"aufklappliste-fokus-maus-{theme}");
        window.Close();
        return true;
    }, CancellationToken.None);

    [Theory]
    [InlineData("hell")]
    [InlineData("dunkel")]
    public Task Geoeffnete_Liste_hat_Radius_8_Schatten_Eintraege_32_hoch_und_zeigt_hoechstens_acht(string theme) => Session.Dispatch(async () =>
    {
        var combo = new ComboBox { ItemsSource = LongComboItems, SelectedIndex = 2 };
        var window = await ShowAsync(theme, combo);
        await OpenAsync(window, combo);

        var popup = combo.GetVisualDescendants().OfType<Popup>().First();
        Assert.True(popup.IsOpen);
        var list = Assert.IsType<Border>(popup.Child);
        Assert.Equal(new CornerRadius(8), list.CornerRadius);
        Assert.Equal(new Thickness(1), list.BorderThickness);
        Assert.Equal(Resolve(combo, "SurfaceBrush"), ((ISolidColorBrush)list.Background!).Color);
        Assert.Equal(Resolve(combo, "LineBrush"), ((ISolidColorBrush)list.BorderBrush!).Color);
        Assert.True(list.BoxShadow.Count > 0, "Schatten wie bei Karten");
        Assert.Equal(combo.Bounds.Width, list.Bounds.Width, 1);

        var items = list.GetVisualDescendants().OfType<ComboBoxItem>().ToList();
        Assert.All(items, item => Assert.Equal(32, item.Bounds.Height, 1));
        var visible = items.Count(item => item.Bounds.Bottom <= list.Bounds.Height);
        Assert.InRange(visible, 7, 8);
        Assert.True(list.Bounds.Height <= 270, $"höchstens acht Einträge sichtbar, Liste {list.Bounds.Height}");
        SaveFieldShot(window, $"aufklappliste-offen-{theme}");
        window.Close();
        return true;
    }, CancellationToken.None);

    [Fact]
    public Task Aufklappliste_Tastatur_Enter_und_Alt_Runter_oeffnen_Pfeile_markieren_ohne_zu_waehlen_Esc_aendert_nichts() => Session.Dispatch(async () =>
    {
        var combo = Choice(1);
        var window = await ShowAsync("hell", combo);
        combo.Focus(NavigationMethod.Tab);
        await Pump();

        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        await Pump();
        Assert.True(combo.IsDropDownOpen);

        // Pfeile markieren nur; gewählt ist weiter „Zwei“.
        window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
        window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
        await Pump();
        Assert.Equal(1, combo.SelectedIndex);
        var focused = TopLevel.GetTopLevel(combo)!.FocusManager!.GetFocusedElement();
        Assert.Equal("Vier", Assert.IsType<ComboBoxItem>(focused).Content);

        // Esc schließt ohne Änderung, der Fokus kehrt ins Feld zurück.
        window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        await Pump();
        Assert.False(combo.IsDropDownOpen);
        Assert.Equal(1, combo.SelectedIndex);
        Assert.True(combo.IsFocused);

        // Alt+Runter öffnet, Enter wählt den markierten Eintrag und schließt.
        window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.Alt);
        await Pump();
        Assert.True(combo.IsDropDownOpen);
        window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        await Pump();
        Assert.False(combo.IsDropDownOpen);
        Assert.Equal(2, combo.SelectedIndex);
        window.Close();
        return true;
    }, CancellationToken.None);

    [Fact]
    public Task Aufklappliste_Buchstaben_springen_zum_Eintrag() => Session.Dispatch(async () =>
    {
        var combo = Choice(0);
        var window = await ShowAsync("hell", combo);
        combo.Focus(NavigationMethod.Tab);
        await Pump();
        window.KeyTextInput("v");
        await Pump();
        Assert.Equal("Vier", combo.SelectedItem);
        window.Close();
        return true;
    }, CancellationToken.None);

    [Fact]
    public Task Aufklappliste_deaktiviert_Deckkraft_halb_und_Fehler_am_Feld_in_Fehlerfarbe() => Session.Dispatch(async () =>
    {
        var disabled = new FormField { Label = "Deaktiviert", Content = new ComboBox { ItemsSource = ComboItems, SelectedIndex = 0, IsEnabled = false } };
        var failing = new FormField { Label = "Fehler", Error = "Bitte eine Behandlung wählen.", Content = Choice(-1) };
        var window = await ShowAsync("hell", disabled, failing);

        Assert.Equal(0.5, ((ComboBox)disabled.Content!).Opacity);
        Assert.Equal(1.0, disabled.GetVisualDescendants().OfType<TextBlock>().First(t => t.Name == "PART_Label").Opacity);
        var surface = ((ComboBox)failing.Content!).GetVisualDescendants().OfType<Border>().First(b => b.Name == "Background");
        Assert.Equal(Resolve(failing, "CritBrush"), ((ISolidColorBrush)surface.BorderBrush!).Color);
        SaveFieldShot(window, "aufklappliste-zustaende");
        window.Close();
        return true;
    }, CancellationToken.None);
}
