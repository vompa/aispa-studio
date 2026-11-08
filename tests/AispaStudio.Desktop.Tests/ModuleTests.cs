using AispaStudio.Contracts;
using AispaStudio.Desktop.ViewModels;

namespace AispaStudio.Desktop.Tests;

/// <summary>Ausgeschaltete Webmodule blendet die App aus (ADR 0041).</summary>
public sealed class ModuleTests
{
    private static readonly CurrentUser Reception = new("u1", "rita@aispa.test", "Rita Steiner", 1,
        new CurrentAccess(Guid.NewGuid(), "Day-Spa Wien", StaffRole.Reception, true, []), null,
        [AccessRight.ViewCalendar, AccessRight.Book, AccessRight.ChangeStatus]);

    private static async Task<ShellViewModel> StartAsync(SystemModules? modules)
    {
        var stub = new StubApi()
            .On("GET /api/system/status", new SystemStatus("0.9.0", "Production", DateTimeOffset.UtcNow))
            .On("GET /api/auth/me", Reception)
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
}
