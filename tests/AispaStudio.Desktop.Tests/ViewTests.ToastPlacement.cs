using AispaStudio.Desktop.Controls;
using AispaStudio.Desktop.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>
/// Aufgabe T-084: Der Toast der Shell sitzt auf jeder Seite über der unteren Leiste (Tastenleiste im Kalender, Speichern-Leiste, Summe der Kassa),
/// und im Bildtest läuft er nicht mit der echten Uhr ab.
/// </summary>
public sealed partial class ViewTests
{
    private static double TopInWindow(Window window, Visual visual) => visual.TranslatePoint(new Point(0, 0), window)!.Value.Y;

    private static double ShownToastBottom(Window window)
    {
        var toast = Named<Border>(window, "ConfirmToast");
        Assert.True(toast.IsEffectivelyVisible);
        return toast.TranslatePoint(new Point(0, toast.Bounds.Height), window)!.Value.Y;
    }

    [Fact]
    public Task Toast_laeuft_im_Bildtest_nicht_mit_der_echten_Uhr_ab() => Render("hell", "toast-bleibt", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        calendar.Toast.Show("Gespeichert");

        // Unter Last vergehen zwischen Aktion und Prüfung leicht mehr als 5 Sekunden; hier vergehen sie mit Absicht.
        await Task.Delay(ToastState.Duration + TimeSpan.FromMilliseconds(600));
        await Pump();

        Assert.True(calendar.Toast.IsShown);
        return calendar;
    });

    [Fact]
    public Task Toast_sitzt_im_Kalender_ueber_der_Tastenleiste() => Render("hell", "toast-kalender", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        calendar.Toast.Show("Brigitte Müller auf 10:10 verschoben", "Rückgängig", calendar.UndoMoveCommand);
        await Pump(Settling);

        var strip = window.GetVisualDescendants().OfType<HintStrip>().Single(s => s.IsEffectivelyVisible);
        Assert.True(ShownToastBottom(window) <= TopInWindow(window, strip), $"Toast endet bei {ShownToastBottom(window)}, die Tastenleiste beginnt bei {TopInWindow(window, strip)}");
        return calendar;
    });

    [Fact]
    public Task Toast_sitzt_bei_schmalem_Fenster_ueber_der_Speichern_Leiste() => Render("hell", "toast-speichern-leiste", async (window, main) =>
    {
        window.Width = 1100;
        var (shell, promotions) = await OpenPromotions(window, main);
        promotions.Toast.Show("Gespeichert");
        await Pump(Settling);

        var bar = window.GetVisualDescendants().OfType<SaveBar>().Single(b => b.IsEffectivelyVisible);
        Assert.True(ShownToastBottom(window) <= TopInWindow(window, bar), $"Toast endet bei {ShownToastBottom(window)}, die Speichern-Leiste beginnt bei {TopInWindow(window, bar)}");
        return shell.AdminPage;
    }, PromotionsApi());

    [Fact]
    public Task Toast_sitzt_in_der_Kassa_ueber_der_Summe() => Render("hell", "toast-kassa", async (window, main) =>
    {
        var pos = await OpenBill(window, main);
        pos.Toast.Show("Gutschein GS-1042 als übergeben vermerkt");
        await Pump(Settling);

        var total = window.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Text == "Summe" && t.IsEffectivelyVisible);
        Assert.True(ShownToastBottom(window) <= TopInWindow(window, total), $"Toast endet bei {ShownToastBottom(window)}, die Summe beginnt bei {TopInWindow(window, total)}");
        return pos;
    });
}
