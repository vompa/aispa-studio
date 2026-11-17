using AispaStudio.Desktop.Services;
using AispaStudio.Desktop.ViewModels;

namespace AispaStudio.Desktop.Tests;

/// <summary>
/// Hilfe als Leiste (ADR 0079, Nachtrag 17.11.2026): Ein Fragezeichen öffnet „KI und Hilfe“ mit genau Thema und Abschnitt, die Seite bleibt stehen;
/// die Hilfe kommt von der einen Web-Hilfe im Modus „ansicht=leiste“ (in Tests ohne Browser wird die Adresse geprüft).
/// </summary>
public sealed class HelpPanelTests
{
    private static ShellViewModel Shell(MemorySettingsStore? store = null) => new(new StubApi().Client(), TestData.Owner(), null, TimeProvider.System,
        new ThemeController(store ?? new MemorySettingsStore()), "Test", () => Task.CompletedTask);

    private static async Task<Uri> AddressOf(HelpViewModel help)
    {
        for (var i = 0; i < 200 && help.Address is null; i++)
        {
            await Task.Delay(10);
        }

        return Assert.IsType<Uri>(help.Address);
    }

    private static System.Collections.Specialized.NameValueCollection Query(Uri address) => System.Web.HttpUtility.ParseQueryString(address.Query);

    [Fact]
    public async Task Fragezeichen_oeffnet_die_Hilfe_in_der_Leiste_beim_Abschnitt_und_die_Seite_bleibt_stehen()
    {
        var store = new MemorySettingsStore();
        var shell = Shell(store);
        shell.SelectedPage = shell.Pages.Single(p => p.Title == "Verwaltung");
        Assert.False(shell.Companion.IsOpen);

        Assert.True(shell.ShowHelpInPanel("firmendaten", "unternehmen"));

        Assert.Same(shell.AdminPage, shell.SelectedPage!.Page);
        Assert.True(shell.Companion.IsOpen);
        Assert.True(shell.Companion.ShowsHelp);
        Assert.Equal(new HelpTarget("firmendaten", "unternehmen"), shell.Companion.Help);
        Assert.Equal("Hilfe: Unternehmen", shell.Companion.HelpTitle);
        Assert.NotEmpty(shell.Companion.HelpLead);
        Assert.False(shell.Companion.ShowsPageSection);
        var query = Query(await AddressOf(shell.Companion.HelpArea!));
        Assert.Equal("firmendaten", query["thema"]);
        Assert.Equal("unternehmen", query["abschnitt"]);
        Assert.Equal("leiste", query["ansicht"]);
        Assert.Equal("1", query["eingebettet"]);
        // Nur für die Hilfe geöffnet: offen oder zu der Seite merkt sich die App dabei nicht.
        Assert.False(store.Load().CompanionPages?.GetValueOrDefault("verwaltung") ?? false);
    }

    [Fact]
    public void Ein_weiteres_Fragezeichen_tauscht_nur_den_Inhalt_und_Schliessen_stellt_den_Stand_davor_wieder_her()
    {
        var shell = Shell();
        shell.SelectedPage = shell.Pages.Single(p => p.Title == "Verwaltung");

        shell.ShowHelpInPanel("firmendaten", "unternehmen");
        shell.ShowHelpInPanel("firmendaten", "bankverbindung");
        Assert.Equal(new HelpTarget("firmendaten", "bankverbindung"), shell.Companion.Help);
        Assert.True(shell.Companion.IsOpen);

        shell.Companion.CloseHelpCommand.Execute(null);
        Assert.False(shell.Companion.ShowsHelp);
        Assert.False(shell.Companion.IsOpen);

        // War die Leiste schon offen, bleibt sie nach der Hilfe offen.
        shell.Companion.ToggleCommand.Execute(null);
        shell.ShowHelpInPanel("firmendaten", "impressum");
        shell.Companion.CloseHelp();
        Assert.True(shell.Companion.IsOpen);
        Assert.True(shell.Companion.ShowsPageSection);
    }

    [Fact]
    public void Ein_unbekannter_Abschnitt_zeigt_das_Thema_und_ein_unbekanntes_Thema_die_Seite_Hilfe()
    {
        var shell = Shell();
        shell.SelectedPage = shell.Pages.Single(p => p.Title == "Verwaltung");

        shell.ShowHelpInPanel("firmendaten", "gibt-es-nicht");
        Assert.Equal(new HelpTarget("firmendaten", null), shell.Companion.Help);

        shell.ShowHelpInPanel("gibt-es-nicht", null);
        Assert.Same(shell.Help, shell.SelectedPage!.Page);
    }

    [Fact]
    public async Task Im_Handbuch_oeffnen_zeigt_die_Seite_Hilfe_an_derselben_Stelle_und_schliesst_den_Hilfe_Bereich()
    {
        var shell = Shell();
        shell.SelectedPage = shell.Pages.Single(p => p.Title == "Verwaltung");
        shell.ShowHelpInPanel("firmendaten", "bankverbindung");

        shell.Companion.OpenManualCommand.Execute(null);

        Assert.Same(shell.Help, shell.SelectedPage!.Page);
        Assert.False(shell.Companion.ShowsHelp);
        var query = Query(await AddressOf(shell.Help));
        Assert.Equal("firmendaten", query["thema"]);
        Assert.Equal("bankverbindung", query["abschnitt"]);
        Assert.Null(query["ansicht"]);
    }

    [Fact]
    public void Esc_und_F1_aus_der_Hilfe_der_Leiste_kommen_als_Nachricht_an()
    {
        var shell = Shell();
        shell.SelectedPage = shell.Pages.Single(p => p.Title == "Verwaltung");
        var keyHelp = 0;
        shell.HelpKeyRequested += () => keyHelp++;
        shell.ShowHelpInPanel("firmendaten", "unternehmen");

        shell.Companion.HelpArea!.OnWebMessage("""{"type":"f1"}""");
        shell.Help.OnWebMessage("""{"type":"f1"}""");
        Assert.Equal(2, keyHelp);

        shell.Companion.HelpArea.OnWebMessage("""{"type":"escape"}""");
        Assert.False(shell.Companion.ShowsHelp);
        Assert.False(shell.Companion.IsOpen);
    }

    [Fact]
    public void Im_Kalender_oeffnet_das_Fragezeichen_den_Reiter_KI_und_Hilfe_beim_Abschnitt()
    {
        var shell = Shell();
        Assert.Same(shell.Calendar, shell.SelectedPage!.Page);

        shell.ShowHelpInPanel("kalender-warteliste", null);

        Assert.Same(shell.Calendar, shell.SelectedPage!.Page);
        Assert.Same(shell.Companion, shell.Calendar.CompanionPanel);
        Assert.Same(shell.Companion, shell.Calendar.ActivePanel);
        Assert.True(shell.Companion.ShowsHelp);

        shell.Companion.CloseHelp();
        Assert.Null(shell.Calendar.CompanionPanel);
    }

    [Fact]
    public void Solange_die_Hilfe_steht_liegt_sie_ueber_den_Leisten_der_Masken_und_eine_neue_Leiste_macht_sie_zu()
    {
        var shell = Shell();
        shell.SelectedPage = shell.Pages.Single(p => p.Title == "Verwaltung");

        shell.ShowHelpInPanel("firmendaten", "unternehmen");
        Assert.True(shell.Panels.IsCovered);

        shell.Panels.ReportOpened();
        Assert.False(shell.Companion.ShowsHelp);
        Assert.False(shell.Panels.IsCovered);
    }

    [Fact]
    public void Seitenwechsel_schliesst_die_Hilfe_der_vorigen_Maske()
    {
        var shell = Shell();
        shell.SelectedPage = shell.Pages.Single(p => p.Title == "Verwaltung");
        shell.ShowHelpInPanel("firmendaten", "unternehmen");

        shell.SelectedPage = shell.Pages.Single(p => p.Title == "Kassa");

        Assert.False(shell.Companion.ShowsHelp);
        Assert.False(shell.Companion.IsOpen);
    }
}
