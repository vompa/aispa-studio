using AispaStudio.Desktop.Controls;
using AispaStudio.Desktop.ViewModels;
using AispaStudio.Desktop.Views;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>
/// Bilder für die Abläufe der Hilfe im Muster „Schritte mit je einem Bild“ (web/src/help/README.md): je Schritt ein Bild des Fensters, auf dem
/// das Zielelement des Schritts markiert ist (Rahmen, leichte Fläche und die Nummer des Schritts). Die Bilder entstehen mit AISPA_SCREENSHOTS
/// unter dem Namen <c>&lt;ablauf&gt;-&lt;schritt&gt;</c>; <c>scripts/help-images.sh</c> übernimmt, was ein Hilfetext verwendet.
/// </summary>
public sealed partial class ViewTests
{
    /// <summary>Rahmenfarbe der Markierung: bewusst weder Akzent noch Warnfarbe der App, damit sie auf jedem Bild auffällt.</summary>
    private static readonly Color MarkColor = Color.Parse("#e8590c");

    private const double MarkGap = 4;

    /// <summary>Ein Ablauf der Hilfe: das Fenster, das Modell und die Aufnahme der Bilder je Schritt.</summary>
    private sealed class HelpShots(string flow, Window window, MainViewModel main)
    {
        public Window Window { get; } = window;

        /// <summary>Zahl der Elemente, die die Markierung während der Aufnahme in die Überlagerung gelegt hat (Rahmen und Nummer).</summary>
        public int MarkElements { get; private set; }

        public MainViewModel Main { get; } = main;

        /// <summary>Bild für Schritt <paramref name="step"/> mit markierten Zielen; gibt die Rahmen der Markierung in Fensterkoordinaten zurück.</summary>
        public async Task<IReadOnlyList<Rect>> Shot(int step, params Rect[] targets)
        {
            Assert.NotEmpty(targets);
            await Pump();
            await Pump(Settling);
            var marks = targets.Select(t => MarkRect(t, Window.Bounds)).ToList();
            var layer = Avalonia.Controls.Primitives.OverlayLayer.GetOverlayLayer(Window) ?? throw new InvalidOperationException("Das Fenster hat keine Überlagerung");
            var added = marks.SelectMany((rect, i) => AddMark(layer, rect, i == 0 ? step : null)).ToList();
            await Pump();
            MarkElements = added.Count;
            SaveShot(Window, $"{flow}-{step}", "hell");
            foreach (var control in added)
            {
                layer.Children.Remove(control);
            }

            return marks;
        }
    }

    /// <summary>Rahmen um das Ziel, mit etwas Luft, aber nie über das Fenster hinaus; ein Ziel ohne Fläche oder außerhalb des Fensters ist ein Fehler im Test.</summary>
    private static Rect MarkRect(Rect target, Rect window)
    {
        var visible = target.Intersect(new Rect(window.Size));
        Assert.True(visible.Width >= 8 && visible.Height >= 8, $"Das Ziel {target} liegt nicht im Fenster {window.Size}");
        var left = Math.Max(0, visible.X - MarkGap);
        var top = Math.Max(0, visible.Y - MarkGap);
        return new Rect(left, top, Math.Min(window.Width, visible.Right + MarkGap) - left, Math.Min(window.Height, visible.Bottom + MarkGap) - top);
    }

    /// <summary>Rahmen mit leichter Fläche; die Nummer des Schritts steht als Kreis an der oberen rechten Ecke (nur am ersten Ziel).</summary>
    private static IEnumerable<Control> AddMark(Avalonia.Controls.Primitives.OverlayLayer layer, Rect rect, int? number)
    {
        var frame = new Border
        {
            Width = rect.Width,
            Height = rect.Height,
            BorderBrush = new SolidColorBrush(MarkColor),
            BorderThickness = new Thickness(3),
            CornerRadius = new CornerRadius(8),
            Background = new SolidColorBrush(MarkColor, 0.12),
            IsHitTestVisible = false,
        };
        Canvas.SetLeft(frame, rect.X);
        Canvas.SetTop(frame, rect.Y);
        layer.Children.Add(frame);
        yield return frame;
        if (number is not { } n)
        {
            yield break;
        }

        var badge = new Border
        {
            Width = 28,
            Height = 28,
            CornerRadius = new CornerRadius(14),
            Background = new SolidColorBrush(MarkColor),
            IsHitTestVisible = false,
            Child = new TextBlock
            {
                Text = n.ToString(System.Globalization.CultureInfo.InvariantCulture),
                Foreground = Brushes.White,
                FontWeight = FontWeight.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            },
        };
        Canvas.SetLeft(badge, Math.Max(0, rect.Right - 18));
        Canvas.SetTop(badge, Math.Max(0, rect.Y - 16));
        layer.Children.Add(badge);
        yield return badge;
    }

    /// <summary>Startet die App im Fenster der Bildtests und lässt <paramref name="steps"/> die Schritte durchlaufen und aufnehmen.</summary>
    private static Task<bool> HelpFlow(string flow, Func<HelpShots, Task> steps, StubApi? stub = null) =>
        Session.Dispatch(async () =>
        {
            Application.Current!.RequestedThemeVariant = ThemeVariant.Light;
            var main = new MainViewModel((stub ?? Api()).Client(null), time: new SampleTime(), local: null);
            var window = new MainWindow { DataContext = main, Width = 1440, Height = 900 };
            window.Show();
            try
            {
                await steps(new HelpShots(flow, window, main));
            }
            finally
            {
                window.Close();
            }

            return true;
        }, CancellationToken.None);

    private static Control FoundVisible(Window window, Func<Control, bool> match, string what) =>
        window.GetVisualDescendants().OfType<Control>().FirstOrDefault(c => c.IsEffectivelyVisible && c.Bounds is { Width: > 0, Height: > 0 } && match(c))
        ?? throw new InvalidOperationException($"Ziel nicht gefunden: {what}");

    private static Rect RectOf(Window window, Control control) => new(control.TranslatePoint(default, window)!.Value, control.Bounds.Size);

    /// <summary>Feld samt Beschriftung (<see cref="FormField"/>).</summary>
    private static Rect FieldRect(Window window, string label) =>
        RectOf(window, FoundVisible(window, c => c is FormField f && f.Label == label, $"Feld {label}"));

    /// <summary>Knopf mit dem Text, als Inhalt oder als Beschriftung darin (Symbol und Text).</summary>
    private static Rect ButtonRect(Window window, string text) =>
        RectOf(window, FindButton(window, b => b.Content as string == text)
            ?? FoundVisible(window, c => c is Button b && b.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text == text), $"Knopf {text}"));

    private static Button? FindButton(Window window, Func<Button, bool> match) =>
        window.GetVisualDescendants().OfType<Button>().FirstOrDefault(b => b.IsEffectivelyVisible && b.Bounds is { Width: > 0, Height: > 0 } && match(b));

    /// <summary>Beschriftung und das, was unter ihr steht (der Elternteil des Textes), z. B. „Kundin oder Kunde“ samt Suchfeld.</summary>
    private static Rect GroupRect(Window window, string label) =>
        RectOf(window, (FoundVisible(window, c => c is TextBlock t && t.Text == label, $"Text {label}").Parent as Control)!);

    private static Rect NamedRect(Window window, string name) =>
        RectOf(window, FoundVisible(window, c => c.Name == name, $"Name {name}"));

    /// <summary>Karte des Termins im Kalender.</summary>
    private static Rect AppointmentRect(Window window, Guid appointmentId) =>
        RectOf(window, FoundVisible(window, c => c is Border b && b.Classes.Contains("appt") && b.DataContext is CalendarItem { Appointment.Id: var id } && id == appointmentId, "Termin"));

    /// <summary>Freie Stelle in der Spalte <paramref name="column"/>: ab der Uhrzeit <paramref name="from"/> (z. B. „12:00“), <paramref name="minutes"/> lang.</summary>
    private static Rect FreeSlotRect(Window window, CalendarViewModel calendar, int column, string from, double minutes)
    {
        var canvas = FoundVisible(window, c => c is Border b && b.Classes.Contains("appt"), "Termin im Kalender").Parent as Control
            ?? throw new InvalidOperationException("Die Spalten haben keine Fläche");
        var origin = canvas.TranslatePoint(default, window)!.Value;
        var scale = calendar.Scale;
        return new Rect(origin.X + DayLayout.ColumnLeft(column, scale) + 3, origin.Y + calendar.Hours.Single(h => h.Label == from).Top, scale.ItemWidth, minutes * scale.PixelsPerMinute);
    }

    /// <summary>Meldet an und wartet, bis der Kalender des Tages gezeichnet ist.</summary>
    private static async Task<CalendarViewModel> OpenCalendar(HelpShots shots)
    {
        var calendar = await SignedInCalendar(shots.Main);
        await Pump(Settling);
        return calendar;
    }

    [Fact]
    public Task Handbuch_Termin_buchen() => HelpFlow("termin-buchen", async shots =>
    {
        var calendar = await OpenCalendar(shots);
        var window = shots.Window;
        // Schritt 1: freie Stelle in der Spalte der Person.
        await shots.Shot(1, FreeSlotRect(window, calendar, 0, "12:00", 60));
        calendar.StartBookingAt(10, calendar.Hours.Single(h => h.Label == "12:00").Top + 1);
        await Pump();
        // Schritt 2: Behandlung wählen.
        await shots.Shot(2, FieldRect(window, "Behandlung"));
        // Schritt 3: Gast suchen und wählen.
        calendar.Booking!.CustomerQuery = "mül";
        await Pump();
        calendar.Booking.Customer = calendar.Booking.Customers[0];
        await Pump();
        await shots.Shot(3, GroupRect(window, "Kundin oder Kunde"));
        // Schritt 4: Termin buchen.
        await shots.Shot(4, ButtonRect(window, "Termin buchen"));
    });

    [Fact]
    public Task Given_ein_Bildtest_eines_Ablaufs_When_er_rendert_Then_markiert_er_das_Zielelement() => HelpFlow("probe-markierung", async shots =>
    {
        var calendar = await OpenCalendar(shots);
        var window = shots.Window;
        calendar.StartBooking(calendar.Columns[1], 330 * DayLayout.PixelsPerMinute);
        await Pump(Settling);
        var target = ButtonRect(window, "Termin buchen");
        var layer = Avalonia.Controls.Primitives.OverlayLayer.GetOverlayLayer(window)!;
        var before = layer.Children.Count;

        var marks = await shots.Shot(1, target);

        // Der Rahmen umschließt das Ziel mit Luft und liegt ganz im Fenster.
        var mark = Assert.Single(marks);
        Assert.True(mark.Contains(target.TopLeft) && mark.Contains(target.BottomRight - new Vector(1, 1)));
        Assert.True(mark.Width > target.Width && mark.Height > target.Height);
        Assert.True(new Rect(window.Bounds.Size).Contains(mark.TopLeft) && new Rect(window.Bounds.Size).Contains(mark.BottomRight - new Vector(1, 1)));
        // Rahmen und Nummer lagen während der Aufnahme in der Überlagerung (sonst wäre das Bild ohne Markierung).
        Assert.Equal(2, shots.MarkElements);
        // Die Markierung ist nur im Bild: danach ist die Überlagerung wieder wie vorher.
        Assert.Equal(before, layer.Children.Count);
        // Ein Ziel, das es nicht gibt, ist ein Fehler im Bildtest und kein Bild ohne Markierung.
        Assert.Throws<InvalidOperationException>(() => ButtonRect(window, "Gibt es nicht"));
    });
}
