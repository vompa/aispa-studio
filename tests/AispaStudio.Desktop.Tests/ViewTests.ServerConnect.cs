using AispaStudio.Desktop.Services;
using AispaStudio.Desktop.ViewModels;
using AispaStudio.Desktop.Views;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>Verwalten von zu Hause (Runde R05, Slice 118: Ä196): der Weg „Mit Server verbinden“ über echte Eingabe.</summary>
public sealed partial class ViewTests
{
    [Fact]
    public Task Serveradresse_tippen_und_Server_verbinden_klicken_merkt_den_Server() => Session.Dispatch(async () =>
    {
        var settings = new MemorySettingsStore();
        var restarts = 0;
        var connect = new ServerConnectViewModel(settings, () => restarts++, (_, _) => Task.FromResult<string?>(null));
        var window = await Show(new ServerConnectView { DataContext = connect });
        var field = window.GetVisualDescendants().OfType<TextBox>().Single();
        field.Focus();
        window.KeyTextInput("spa.example.at");
        await Pump();

        var button = window.GetVisualDescendants().OfType<Button>().Single(b => b.Content is "Server verbinden");
        Assert.Contains("primary", button.Classes);
        var at = button.TranslatePoint(new Point(button.Bounds.Width / 2, button.Bounds.Height / 2), window)!.Value;
        window.MouseDown(at, MouseButton.Left);
        window.MouseUp(at, MouseButton.Left);
        await Pump();
        window.Close();

        Assert.Equal(("https://spa.example.at/", null, 1), (settings.Load().ServerAddress, settings.Load().ServerCode, restarts));
        return true;
    }, CancellationToken.None);

    [Fact]
    public Task Im_Server_Betrieb_steht_die_Seite_Netz_in_der_Seitenleiste() => Render("hell", "netz-server", async (window, main) =>
    {
        await SignedInCalendar(main);
        var shell = (ShellViewModel)main.Content!;
        var page = shell.Pages.Single(p => p.Page is ServerLinkViewModel);
        shell.SelectedPage = page;

        Assert.Equal("Netz", page.Title);
        Assert.Same(shell.ServerLink, page.Page);
        Assert.Contains("https://spa.example.at/", shell.ServerLink!.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(shell.Pages, p => p.Page is NetworkViewModel);
        return shell.ServerLink;
    }, serverLink: new ServerLinkViewModel(new MemorySettingsStore(),
        new ServerChoice.Choice(new Uri("https://spa.example.at/"), null, ServerSource.Server), () => { }));

    [Fact]
    public Task Ohne_Verbindung_passen_beide_Karten_auf_einen_Bildschirm_mit_1366_mal_768() => Session.Dispatch(async () =>
    {
        var stub = Api();
        stub.Down = true;
        var main = new MainViewModel(stub.Client(), time: new SampleTime())
        {
            Connect = new NetworkConnectViewModel(new MemorySettingsStore(), () => { }, (_, _, _) => Task.FromResult<string?>(null)),
            ServerConnect = new ServerConnectViewModel(new MemorySettingsStore(), () => { }, (_, _) => Task.FromResult<string?>(null)),
        };
        var window = new MainWindow { DataContext = main, Width = 1366, Height = 768 };
        window.Show();
        await main.StartAsync();
        await Pump();
        await Pump(Settling);

        var button = window.GetVisualDescendants().OfType<Button>().Single(b => b.Content is "Server verbinden");
        var bottom = button.TranslatePoint(new Point(0, button.Bounds.Height), window)!.Value.Y;
        var top = window.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "Retry").TranslatePoint(new Point(0, 0), window)!.Value.Y;
        window.Close();

        Assert.True(top >= 0, $"„Erneut versuchen“ liegt über dem Fenster ({top} px)");
        Assert.True(bottom <= 768, $"„Server verbinden“ liegt unter dem Fenster ({bottom} px)");
        return true;
    }, CancellationToken.None);

    [Fact]
    public Task Seite_Netz_Verbindung_loesen_ist_ein_benannter_Knopf_und_im_Server_Betrieb_ohne_Eintrag_gesperrt() => Session.Dispatch(async () =>
    {
        var link = new ServerLinkViewModel(new MemorySettingsStore(), ServerChoice.Resolve(new DesktopSettings(), null), () => { });
        var window = await Show(new ServerLinkView { DataContext = link });
        var button = window.GetVisualDescendants().OfType<Button>().Single(b => b.Content is "Verbindung lösen");
        var name = ControlAutomationPeerName(button);
        var enabled = button.IsEffectivelyEnabled;
        window.Close();

        Assert.Equal("Verbindung lösen", name);
        Assert.False(enabled);
        return true;
    }, CancellationToken.None);

    [Fact]
    public Task Serveradresse_Feld_hat_einen_eigenen_Namen() => Session.Dispatch(async () =>
    {
        var connect = new ServerConnectViewModel(new MemorySettingsStore(), () => { }, (_, _) => Task.FromResult<string?>(null));
        var window = await Show(new ServerConnectView { DataContext = connect });
        var problems = FieldsWithoutOwnName(window);
        var names = window.GetVisualDescendants().OfType<TextBox>().Select(ControlAutomationPeerName).ToList();
        window.Close();

        Assert.Empty(problems);
        Assert.Equal(["Serveradresse"], names);
        return true;
    }, CancellationToken.None);
}
