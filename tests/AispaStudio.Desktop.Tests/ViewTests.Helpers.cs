using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;

namespace AispaStudio.Desktop.Tests;

/// <summary>Hilfen für die Tests der Fluent-2-Blöcke B bis D: ein eigenes Fenster mit einem Steuerelement, Farben aus den Ressourcen, Bildschirmfotos.</summary>
public sealed partial class ViewTests
{
    /// <summary>Zeigt das gebaute Steuerelement (auf dem Oberflächen-Thread erzeugt) in einem eigenen Fenster im gewählten Farbschema, ruft <paramref name="act"/> auf und macht auf Wunsch ein Bild.</summary>
    private static Task<bool> InWindow(string theme, Func<Control> build, double width, double height, Func<Window, Task> act, string? shot = null) =>
        Session.Dispatch(async () =>
        {
            Application.Current!.RequestedThemeVariant = theme == "dunkel" ? ThemeVariant.Dark : ThemeVariant.Light;
            var window = new Window { Width = width, Height = height, Content = build() };
            window.Show();
            await Pump();
            await act(window);
            if (shot is not null)
            {
                await Pump(Settling);
                SaveShot(window, shot, theme);
            }

            window.Close();
            return true;
        }, CancellationToken.None);

    /// <summary>Legt ein Bild des Fensters in den Ordner aus AISPA_SCREENSHOTS (nur wenn gesetzt).</summary>
    private static void SaveShot(Window window, string name, string theme)
    {
        if (Environment.GetEnvironmentVariable("AISPA_SCREENSHOTS") is not { Length: > 0 } folder)
        {
            return;
        }

        Directory.CreateDirectory(folder);
        window.CaptureRenderedFrame()?.Save(Path.Combine(folder, $"{name}{(theme == "hell" ? "" : "-" + theme)}.png"), PngBitmapEncoderOptions.Default);
    }

    /// <summary>Farbe einer Ressource im Schema des Steuerelements.</summary>
    private static Color ColorOf(Control owner, string key) =>
        owner.TryFindResource(key, owner.ActualThemeVariant, out var value) && value is ISolidColorBrush brush ? brush.Color : throw new InvalidOperationException(key);

    private static Color ColorOf(IBrush? brush) => brush is ISolidColorBrush solid ? solid.Color : throw new InvalidOperationException("Keine einfarbige Fläche");

    /// <summary>Kontrast nach WCAG (hell gegen dunkel, 1 bis 21).</summary>
    private static double Contrast(Color a, Color b)
    {
        static double Linear(byte v)
        {
            var s = v / 255.0;
            return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
        }

        static double Luminance(Color c) => (0.2126 * Linear(c.R)) + (0.7152 * Linear(c.G)) + (0.0722 * Linear(c.B));
        var (high, low) = (Math.Max(Luminance(a), Luminance(b)), Math.Min(Luminance(a), Luminance(b)));
        return (high + 0.05) / (low + 0.05);
    }

    /// <summary>Taste drücken und loslassen, mit Umschalttasten.</summary>
    private static void Press(Window window, PhysicalKey key, RawInputModifiers modifiers)
    {
        window.KeyPressQwerty(key, modifiers);
        window.KeyReleaseQwerty(key, modifiers);
    }
}
