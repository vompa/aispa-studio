using AispaStudio.Contracts;
using AispaStudio.Desktop.Controls;
using AispaStudio.Desktop.Services;
using AispaStudio.Desktop.ViewModels;
using AispaStudio.Desktop.Views;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>
/// Durchsicht der Seiten „Hilfe“, Anmeldung und „System“ (Abgleich, Sicherung, Im Netz, Netz): Umbruch in der Karte, Banner nur auf anderen Seiten,
/// Gründe an gesperrten Knöpfen, erkennbare Knopfformen und Beschriftungen im Stil der Feldbeschriftung.
/// </summary>
public sealed partial class ViewTests
{
    private static readonly string[] CalmSyncButtons = ["Neu laden", "Abgleich ausschalten"];

    private static readonly string[] NetworkLabels = ["Adressen", "Verbindungscode"];

    private static async Task<ShellViewModel> OpenBackup(MainViewModel main)
    {
        await SignedInCalendar(main);
        var shell = (ShellViewModel)main.Content!;
        shell.SelectedPage = shell.Pages.First(p => p.Page == shell.Backup);
        return shell;
    }

    [Fact]
    public Task Sicherung_Meldung_zum_neuen_Schluessel_bricht_innerhalb_der_Karte_um() => Render("hell", "sicherung", async (window, main) =>
    {
        var shell = await OpenBackup(main);
        shell.Backup!.NewRecoveryKeyCommand.Execute(null);
        await Pump();

        var message = window.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Text == shell.Backup.Message && t.IsEffectivelyVisible);
        var card = message.GetVisualAncestors().OfType<Border>().First(b => b.Classes.Contains("card"));
        Assert.True(RightOf(window, message) <= RightOf(window, card) + 0.5, $"Meldung endet bei {RightOf(window, message):0}, die Karte bei {RightOf(window, card):0}");
        Assert.True(message.Bounds.Height > 24, $"Die Meldung bricht nicht um (Höhe {message.Bounds.Height:0})");
        return shell.Backup;
    }, local: new SampleLocal());

    [Fact]
    public Task Sicherungs_Banner_steht_auf_allen_Seiten_ausser_auf_der_Seite_Sicherung() => Render("hell", "sicherung-banner", async (window, main) =>
    {
        await SignedInCalendar(main);
        await Pump();
        var shell = (ShellViewModel)main.Content!;
        Assert.True(shell.Backup!.HasReminder);
        Assert.True(shell.Banners.Backup);
        Assert.Contains(VisibleBars(window), b => b.ActionText == "Sicherung öffnen");

        shell.SelectedPage = shell.Pages.First(p => p.Page == shell.Backup);
        await Pump();
        Assert.False(shell.Banners.Backup);
        Assert.DoesNotContain(VisibleBars(window), b => b.ActionText == "Sicherung öffnen");

        shell.SelectedPage = shell.Pages.First(p => p.Page == shell.Sync);
        await Pump();
        Assert.True(shell.Banners.Backup);
        Assert.Contains(VisibleBars(window), b => b.ActionText == "Sicherung öffnen");
        return shell.Backup;
    }, local: new SampleLocal());

    [Fact]
    public Task Sicherung_gesperrte_Knoepfe_nennen_den_Grund_und_der_Schluesselknopf_ist_kein_Ghost() => Render("hell", "sicherung-gruende", async (window, main) =>
    {
        await OpenBackup(main);
        await Pump();

        var now = ButtonNamed(window, "Jetzt sichern");
        Assert.False(now.IsEffectivelyEnabled);
        Assert.Equal("Verfügbar, sobald ein Sicherungsordner gespeichert ist.", ToolTip.GetTip(now));
        var save = ButtonNamed(window, "Speichern und sichern");
        Assert.False(save.IsEffectivelyEnabled);
        Assert.Equal("Verfügbar, sobald ein Ordner eingetragen ist.", ToolTip.GetTip(save));
        Assert.DoesNotContain("ghost", ButtonNamed(window, "Neuen Wiederherstellungsschlüssel erzeugen").Classes);
        return window.DataContext!;
    }, local: new SampleLocal());

    private static StubApi SyncWithConflict() => Api().On("GET /api/system/sync", new SyncStatus(true, SpaSample.At(11, 39), null, 2,
    [
        new SyncConflict(1, "Termine", "Appointments", "{}", "Zentral abgelehnt: Raum, Person oder Gerät ist zur selben Zeit schon gebucht.", SpaSample.At(10, 12)),
    ]));

    [Fact]
    public Task Abgleich_Hilfetext_steht_in_einer_Zeile_unter_dem_Schluesselfeld_und_die_Knoepfe_haben_eine_Form() => Render("hell", "abgleich", async (window, main) =>
    {
        await SignedInCalendar(main);
        var shell = (ShellViewModel)main.Content!;
        shell.SelectedPage = shell.Pages.First(p => p.Page == shell.Sync);
        await shell.Sync!.RefreshAsync();
        await Pump();

        var help = Named<TextBlock>(window, "ShareCodeHelp");
        var field = window.GetVisualDescendants().OfType<FormField>().Single(f => f.Label == "Schlüsselcode des Spas");
        var connection = window.GetVisualDescendants().OfType<FormField>().Single(f => f.Label == "Verbindungsangabe");
        Assert.True(TopOf(window, help) >= BottomOf(window, field), "Der Hilfetext steht unter dem Schlüsselfeld");
        Assert.True(help.Bounds.Height <= 17, $"Der Hilfetext braucht mehr als eine Zeile (Höhe {help.Bounds.Height:0})");
        Assert.True(TopOf(window, field) - BottomOf(window, connection) < 30, "Zwischen Verbindungsangabe und Schlüsselcode steht nichts");
        Assert.DoesNotContain(window.GetVisualDescendants().OfType<TextBlock>(), t => t.IsEffectivelyVisible && t.Text?.StartsWith("Schlüsselcode des Spas:", StringComparison.Ordinal) == true);

        foreach (var name in CalmSyncButtons)
        {
            Assert.DoesNotContain("ghost", ButtonNamed(window, name).Classes);
        }

        var done = ButtonNamed(window, "Erledigt");
        Assert.Contains("ok", done.Classes);
        Assert.DoesNotContain("ghost", done.Classes);
        Assert.Contains(window.GetVisualDescendants().OfType<TextBlock>(), t => t.IsEffectivelyVisible && t.Text == "2 Änderungen warten auf den nächsten Abgleich.");
        return shell.Sync;
    }, SyncWithConflict(), local: new SampleLocal());

    [Fact]
    public Task Im_Netz_Beschriftungen_stehen_im_Stil_der_Feldbeschriftung_ohne_Klammerzusatz() => Render("hell", "im-netz", async (window, main) =>
    {
        await SignedInCalendar(main);
        var shell = (ShellViewModel)main.Content!;
        shell.SelectedPage = shell.Pages.First(p => p.Page == shell.Network);
        await Pump();

        var texts = window.GetVisualDescendants().OfType<TextBlock>().Where(t => t.IsEffectivelyVisible).ToList();
        Assert.DoesNotContain(texts, t => t.Text?.Contains("eine davon)", StringComparison.Ordinal) == true);
        foreach (var label in NetworkLabels)
        {
            Assert.Contains("label", texts.Single(t => t.Text == label).Classes);
        }

        Assert.Contains(texts, t => t.Text == "Eine davon genügt.");
        Assert.Contains(texts, t => t.Text?.Contains("am Empfangsgerät mit Name und PIN", StringComparison.Ordinal) == true);
        return shell.Network!;
    }, local: new SampleLocal());

    [Fact]
    public Task Netz_verbinden_Verbindungscode_hat_die_Breite_eines_kurzen_Textfelds()
    {
        var connect = new NetworkConnectViewModel(new MemorySettingsStore(), () => { }, (_, _, _) => Task.FromResult<string?>(
            "Der Verbindungscode passt nicht zu empfang-pc. Bitte am Haupt-PC unter „Im Netz“ nachsehen."))
        {
            Address = "empfang-pc",
            Code = "3F9A-12C4-77B0-E1D2-9A0C",
        };
        return Render("hell", "netz-verbinden", async (window, main) =>
        {
            await main.StartAsync();
            await connect.ConnectCommand.ExecuteAsync(null);
            await Pump();

            var fields = window.GetVisualDescendants().OfType<FormField>().Where(f => f.IsEffectivelyVisible).ToList();
            var code = fields.Single(f => f.Label == "Verbindungscode");
            var address = fields.Single(f => f.Label == "Adresse");
            Assert.True(code.Bounds.Width <= 280.5, $"Der Verbindungscode ist {code.Bounds.Width:0} breit");
            Assert.True(address.Bounds.Width > 400, "Die Adresse füllt die Karte");
            return connect;
        }, DownApi(), connect: connect);
    }

    private static StubApi DownApi()
    {
        var stub = Api();
        stub.Down = true;
        return stub;
    }

    [Fact]
    public Task Keine_Verbindung_nennt_den_Befund_nur_im_Titel_und_darunter_den_Weg() => Render("hell", "netz-verbinden-meldung", async (window, main) =>
    {
        await main.StartAsync();
        Assert.False(main.IsConnected);
        Assert.Equal("Keine Verbindung zum Server", main.ConnectionTitle);
        Assert.Equal("Adresse und Netzwerk prüfen, dann „Erneut versuchen“.", main.StatusText);
        Assert.DoesNotContain("nicht erreichbar", main.StatusText, StringComparison.Ordinal);
        return main;
    }, DownApi());

    [Fact]
    public Task Netz_im_Server_Betrieb_erklaert_die_Folge_von_Verbindung_loesen() => Render("hell", "netz-server", async (window, main) =>
    {
        await SignedInCalendar(main);
        var shell = (ShellViewModel)main.Content!;
        shell.SelectedPage = shell.Pages.Single(p => p.Page is ServerLinkViewModel);
        await Pump();

        var texts = window.GetVisualDescendants().OfType<TextBlock>().Where(t => t.IsEffectivelyVisible).Select(t => t.Text).ToList();
        Assert.Contains(texts, t => t?.StartsWith("Hier steht, mit welchem Server diese App verbunden ist.", StringComparison.Ordinal) == true);
        Assert.DoesNotContain(texts, t => t?.Contains("dieselbe Anmeldung", StringComparison.Ordinal) == true);
        var button = ButtonNamed(window, "Verbindung lösen");
        var explanation = window.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Text?.StartsWith("Verbindung lösen trägt die Adresse", StringComparison.Ordinal) == true);
        Assert.True(BottomOf(window, explanation) <= TopOf(window, button), "Die Folge steht vor dem Knopf");
        return shell.ServerLink!;
    }, serverLink: new ServerLinkViewModel(new MemorySettingsStore(), new ServerChoice.Choice(new Uri("https://spa.example.at/"), null, ServerSource.Server), () => { }));

    [Fact]
    public Task Verbindung_loesen_ist_gesperrt_mit_Grund_wenn_die_Adresse_aus_der_Umgebung_kommt() => Session.Dispatch(async () =>
    {
        var link = new ServerLinkViewModel(new MemorySettingsStore(), new ServerChoice.Choice(new Uri("https://spa.example.at/"), null, ServerSource.Environment), () => { });
        var window = await Show(new ServerLinkView { DataContext = link });
        var button = ButtonNamed(window, "Verbindung lösen");
        var tip = ToolTip.GetTip(button);
        window.Close();

        Assert.False(button.IsEffectivelyEnabled);
        Assert.Equal("Verfügbar, wenn ein Server oder Haupt-PC eingetragen ist.", tip);
        return true;
    }, CancellationToken.None);

    [Fact]
    public Task Anmeldung_am_Empfangsgeraet_Anmelden_nennt_den_Grund_und_Anderes_Konto_ist_ein_Link() => Session.Dispatch(async () =>
    {
        var login = await LoginIn("Empfangsgerät");
        login.Pin = "12";
        var window = await Show(new LoginView { DataContext = login });
        var signIn = ButtonNamed(window, "Anmelden");
        var other = ButtonNamed(window, login.OtherAccountText);
        var disabled = signIn.IsEffectivelyEnabled;
        var tip = ToolTip.GetTip(signIn);
        var otherClasses = other.Classes.ToList();
        window.Close();

        Assert.False(disabled);
        Assert.Equal("Verfügbar, sobald die PIN mindestens 4 Ziffern hat.", tip);
        Assert.Contains("link", otherClasses);
        Assert.DoesNotContain("ghost", otherClasses);
        return true;
    }, CancellationToken.None);

    [Theory]
    [InlineData("Anmeldung mit Code", "Verfügbar, sobald der Code mit 8 Zeichen eingetragen ist.")]
    [InlineData("Einzelplatz", "Verfügbar, sobald Benutzername und Passwort eingetragen sind.")]
    public Task Anmelden_ist_ohne_Eingabe_gesperrt_und_nennt_den_Grund(string state, string reason) => Session.Dispatch(async () =>
    {
        var login = await LoginIn(state);
        var window = await Show(new LoginView { DataContext = login });
        var signIn = window.GetVisualDescendants().OfType<Button>().Single(b => b.Content as string == "Anmelden" && b.IsEffectivelyVisible);
        var enabled = signIn.IsEffectivelyEnabled;
        var tip = ToolTip.GetTip(signIn);
        window.Close();

        Assert.False(enabled);
        Assert.Equal(reason, tip);
        return true;
    }, CancellationToken.None);

    [Fact]
    public Task Alle_Eintraege_der_Navigation_stehen_bei_1440_mal_900_mit_Beschriftung_im_sichtbaren_Bereich() => Render("hell", "navigation-system", async (window, main) =>
    {
        var shell = await OpenBackup(main);
        await Pump(Settling);

        var navigation = window.GetVisualDescendants().OfType<ListBox>().Single(l => l.Classes.Contains("nav"));
        var items = Enumerable.Range(0, shell.Pages.Count).Select(i => (Control)navigation.ContainerFromIndex(i)!).ToList();
        Assert.Equal(11, items.Count);
        var height = navigation.Bounds.Height;
        foreach (var item in items)
        {
            var top = item.TranslatePoint(new Point(0, 0), navigation)!.Value.Y;
            var bottom = top + item.Bounds.Height;
            var title = item.GetVisualDescendants().OfType<TextBlock>().First(t => t.Classes.Contains("navtitle"));
            Assert.True(top >= -0.5 && bottom <= height + 0.5, $"„{title.Text}“ liegt bei {top:0} bis {bottom:0}, der sichtbare Bereich endet bei {height:0}");
        }

        return shell.Backup!;
    }, local: new SampleLocal());

    [Fact]
    public Task Hilfe_Knopf_Offene_Fragen_sagt_im_Tooltip_was_sich_oeffnet() => Render("hell", "hilfe", async (window, main) =>
    {
        var shell = await OpenPage(main, "Hilfe");
        var view = window.GetVisualDescendants().OfType<HelpView>().Single();
        var toggle = view.FindControl<ToggleButton>("OpenQuestionsToggle")!;
        Assert.True(toggle.IsEffectivelyVisible);
        Assert.Equal("Klappt die Liste der Fragen auf, auf die der Assistent nichts gefunden hat", ToolTip.GetTip(toggle));
        return shell;
    });
}
