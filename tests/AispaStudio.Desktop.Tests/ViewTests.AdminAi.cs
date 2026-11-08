using AispaStudio.Contracts;
using AispaStudio.Desktop.ViewModels;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>R05 Slice 97 (Ä191): native Maske „KI“ im Fenster: Stand, Download per Klick, Scans.</summary>
public sealed partial class ViewTests
{
    private static StubApi AiApi(AiStatus? status = null, StubApi? api = null) => (api ?? Api())
        .On("GET /api/system/ai", status ?? new AiStatus(true, LocalAiState.Missing, "gemma3:4b", null, null, SpaSample.At(10)))
        .On("POST /api/system/ai/local/download", new { }, System.Net.HttpStatusCode.Accepted);

    private static async Task<ShellViewModel> OpenAi(Window window, MainViewModel main)
    {
        var shell = await OpenAdminPage(window, main);
        shell.AdminPage.SelectedArea = shell.AdminPage.Areas.Single(a => a.Title == "KI");
        await Pump(Settling);
        await Pump(Settling);
        Assert.Same(shell.AdminPage.Ai, shell.AdminPage.Content);
        Assert.True(shell.AdminPage.Ai!.IsLoaded);
        return shell;
    }

    [Theory]
    [InlineData("hell")]
    [InlineData("dunkel")]
    public Task Verwaltung_KI(string theme) => Render(theme, "verwaltung-ki", async (window, main) =>
    {
        var shell = await OpenAi(window, main);
        Assert.True(Shows(window, "Eingeschaltet."));
        Assert.True(Shows(window, "Der Dienst läuft, das Modell ist noch nicht geladen."));
        Assert.True(Shows(window, "Modell: gemma3:4b"));
        Assert.False(window.GetVisualDescendants().OfType<TextBox>().Any(t => t.IsEffectivelyVisible && t.PasswordChar != default), "Die KI-Maske hat kein Schlüsselfeld.");
        return shell.AdminPage;
    }, AiApi());

    [Fact]
    public Task Verwaltung_KI_Download_laeuft() => Render("hell", "verwaltung-ki-download", async (window, main) =>
    {
        var shell = await OpenAi(window, main);
        var ai = shell.AdminPage.Ai!;
        Assert.True(Shows(window, "Das Modell wird heruntergeladen."));
        Assert.Equal(40, Named<ProgressBar>(window, "DownloadProgress").Value);
        Assert.DoesNotContain(window.GetVisualDescendants().OfType<Button>(), b => b.Name == "DownloadButton" && b.IsEffectivelyVisible);
        ai.Dispose();
        return shell.AdminPage;
    }, AiApi(new AiStatus(false, LocalAiState.Downloading, "gemma3:4b", 40, null, SpaSample.At(10))));

    [Fact]
    public Task Modell_herunterladen_ueber_Klick()
    {
        var api = AiApi();
        return Render("hell", "verwaltung-ki-herunterladen", async (window, main) =>
        {
            var shell = await OpenAi(window, main);

            await ClickScrolled(window, "DownloadButton");

            Assert.Equal(1, CallsTo(api, "POST /api/system/ai/local/download"));
            Assert.True(Shows(window, "Download gestartet. Er läuft im Hintergrund weiter, auch beim Verlassen der Seite."));
            return shell.AdminPage;
        }, api);
    }

    [Fact]
    public Task KI_melden_keine_Bindungsfehler() => Render("hell", "bindungen-ki", async (window, main) =>
    {
        await WithBindingLog(async log =>
        {
            var shell = await OpenAdminPage(window, main);
            await AssertNoBindingErrors(log, "KI", () =>
            {
                shell.AdminPage.SelectedArea = shell.AdminPage.Areas.Single(a => a.Title == "KI");
                return Task.CompletedTask;
            });
            await AssertNoBindingErrors(log, "Herunterladen", async () => await shell.AdminPage.Ai!.DownloadCommand.ExecuteAsync(null));
            await AssertNoBindingErrors(log, "Anderer Bereich", () =>
            {
                shell.AdminPage.SelectedArea = shell.AdminPage.Areas.Single(a => a.Title == "Behandlungen");
                return Task.CompletedTask;
            });
            log.AssertClean();
            shell.AdminPage.Ai!.Dispose();
            return shell.AdminPage;
        });
        return true;
    }, AiApi());

    [Fact]
    public Task Jede_Seite_KI_hat_fuer_jedes_bedienbare_Element_einen_lesbaren_Namen() => Render("hell", "namen-ki", async (window, main) =>
    {
        var shell = await OpenAi(window, main);
        var problems = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var control in window.GetVisualDescendants().OfType<Control>().Where(c => c is Button or CheckBox or RadioButton or TextBox or ComboBox or ListBoxItem or ProgressBar))
        {
            if (control.IsEffectivelyVisible && control.IsEffectivelyEnabled && (control.Focusable || control is ProgressBar) && HasUnreadableName(control, out var name))
            {
                problems.Add($"KI: {control.GetType().Name}#{control.Name} \"{name}\"");
            }
        }

        Assert.True(problems.Count == 0, $"{problems.Count} Elemente:{Environment.NewLine}{string.Join(Environment.NewLine, problems)}");
        return shell.AdminPage;
    }, AiApi());
}
