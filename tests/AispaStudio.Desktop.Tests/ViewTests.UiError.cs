using AispaStudio.Desktop.ViewModels;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>T-055: Ein Fehler im UI-Thread ist protokolliert und die App läuft weiter; unten steht eine Warnung, bis sie geschlossen wird.</summary>
public sealed partial class ViewTests
{
    [Fact]
    public Task Fehler_im_UI_Thread_zeigt_eine_Meldung_bis_Schliessen() => Render("hell", "fehler-ui-thread", async (window, main) =>
    {
        // Vor der Anmeldung gibt es keine Shell: kein Absturz, keine Meldung.
        main.ReportUiError();

        await SignedInCalendar(main);
        var shell = Assert.IsType<ShellViewModel>(main.Content);
        main.ReportUiError();
        await Pump(Settling);

        Assert.Equal(ShellViewModel.UnexpectedErrorText, shell.Toast);
        var toast = window.GetVisualDescendants().OfType<Border>().Single(b => b.Name == "ShellToast");
        Assert.True(toast.IsVisible);
        Assert.Contains(toast.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == ShellViewModel.UnexpectedErrorText);

        shell.CloseToastCommand.Execute(null);
        await Pump(Settling);
        Assert.Null(shell.Toast);
        Assert.False(toast.IsVisible);
        return shell;
    });
}
