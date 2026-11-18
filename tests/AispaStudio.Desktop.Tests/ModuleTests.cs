using AispaStudio.Contracts;
using AispaStudio.Desktop.ViewModels;

namespace AispaStudio.Desktop.Tests;

/// <summary>Ausgeschaltete Webmodule blendet die App aus (ADR 0041).</summary>
public sealed class ModuleTests
{
    private static readonly CurrentUser Reception = new("u1", "rita@aispa.test", "Rita Steiner", 1,
        new CurrentAccess(Guid.NewGuid(), "Day-Spa Wien", StaffRole.Reception, true, []), null,
        [AccessRight.ViewCalendar, AccessRight.Book, AccessRight.ChangeStatus]);

    private static async Task<ShellViewModel> StartAsync(SystemModules? modules, CurrentUser? user = null)
    {
        var stub = new StubApi()
            .On("GET /api/system/status", new SystemStatus("0.9.0", "Production", DateTimeOffset.UtcNow))
            .On("GET /api/auth/me", user ?? Reception)
            .On("GET /api/locations", new List<LocationDto> { SpaSample.Vienna });
        if (modules is not null)
        {
            stub.On("GET /api/system/modules", modules);
        }

        var main = new MainViewModel(stub.Client());
        await main.StartAsync();
        return Assert.IsType<ShellViewModel>(main.Content);
    }

    [Fact]
    public async Task Einzelplatz_ohne_Terminannahme_zeigt_keine_Anfragen()
    {
        var shell = await StartAsync(new SystemModules(true, false, false, false, false, false, false, false));

        Assert.False(shell.HasInbox);
        Assert.DoesNotContain(shell.Pages, p => p.Page == shell.Inbox);
        Assert.Contains(shell.Pages, p => p.Page == shell.Pos);
    }

    [Fact]
    public async Task Ohne_Antwort_zum_Modul_bleiben_die_Anfragen_wie_bisher()
    {
        var shell = await StartAsync(null);

        Assert.True(shell.HasInbox);
        Assert.Contains(shell.Pages, p => p.Page == shell.Inbox);
    }

    private static readonly string[] ModuleAreas = ["Erinnerungen", "Postfach & Nummern", "Zahlungen"];

    [Fact]
    public async Task Einzelplatz_ohne_Webmodule_blendet_Erinnerungen_Postfach_und_Zahlungen_in_der_Verwaltung_aus()
    {
        var shell = await StartAsync(new SystemModules(true, false, false, false, false, false, false, false), TestData.Owner());

        var titles = shell.AdminPage.Areas.Select(a => a.Title).ToList();
        Assert.All(ModuleAreas, area => Assert.DoesNotContain(area, titles));
        Assert.Contains("KI", titles);
        Assert.Equal(["KI"], shell.AdminPage.Groups.Single(g => g.Title == "Kontakt").Areas.Select(a => a.Title));
        Assert.Null(shell.AdminPage.Reminders);
        Assert.Null(shell.AdminPage.Intake);
        Assert.Null(shell.AdminPage.Payments);
    }

    [Fact]
    public async Task Alle_Webmodule_an_zeigen_Erinnerungen_Postfach_und_Zahlungen()
    {
        var shell = await StartAsync(new SystemModules(false, true, true, true, true, true, true, true), TestData.Owner());

        var titles = shell.AdminPage.Areas.Select(a => a.Title).ToList();
        Assert.All(ModuleAreas, area => Assert.Contains(area, titles));
    }

    [Fact]
    public async Task Ohne_Antwort_zu_den_Modulen_zeigt_die_Verwaltung_wie_bisher_alle_Bereiche()
    {
        var shell = await StartAsync(null, TestData.Owner());

        var titles = shell.AdminPage.Areas.Select(a => a.Title).ToList();
        Assert.All(ModuleAreas, area => Assert.Contains(area, titles));
    }

    [Fact]
    public async Task Nur_Wiederbuchung_an_zeigt_den_Bereich_Erinnerungen()
    {
        var shell = await StartAsync(new SystemModules(false, false, false, false, false, true, false, false), TestData.Owner());

        Assert.Contains(shell.AdminPage.Areas, a => a.Title == "Erinnerungen");
    }

    [Fact]
    public async Task Sprung_auf_einen_ausgeschalteten_Bereich_zeigt_den_Hinweis_zum_Modul_statt_Recht_fehlt()
    {
        var shell = await StartAsync(new SystemModules(true, false, false, false, false, false, false, false), TestData.Owner());

        var toast = shell.AdminPage.Show("masterData", "reminders");

        Assert.Equal(AdminPageViewModel.ModuleOffToast, toast);
        Assert.Equal(AdminPageViewModel.ModuleOffNotice, shell.AdminPage.Notice);
        Assert.True(AdminPageViewModel.ModuleOffToast.Length <= 60);
        Assert.False(AdminPageViewModel.ModuleOffToast.EndsWith('.'));
    }
}
