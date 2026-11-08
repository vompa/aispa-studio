using System.Drawing;
using Avalonia;
using Avalonia.Controls;

namespace AispaStudio.LiveTests.Launcher;

/// <summary>
/// Liegt als schmales Fenster immer oben am rechten Rand des Bildschirms. Die AISPA-App bekommt die Fläche links daneben,
/// damit Klicks und Bilder des Testers die Starter-App nie treffen.
/// </summary>
internal sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel(AppArea);
        Opened += (_, _) => DockRight();
    }

    private void DockRight()
    {
        if (Screens.ScreenFromWindow(this) is not { } screen)
        {
            return;
        }

        var area = screen.WorkingArea;
        var scaling = screen.Scaling;
        Height = area.Height / scaling - 40;
        Position = new PixelPoint(area.Right - (int)Math.Ceiling(Width * scaling), area.Y);
    }

    /// <summary>Fläche links der Starter-App in Bildschirmpixeln, oder null, wenn der Bildschirm unbekannt ist.</summary>
    private Rectangle? AppArea()
    {
        if (Screens.ScreenFromWindow(this) is not { } screen)
        {
            return null;
        }

        var area = screen.WorkingArea;
        var width = Position.X - area.X;
        return width > 0 ? new Rectangle(area.X, area.Y, width, area.Height) : null;
    }
}
