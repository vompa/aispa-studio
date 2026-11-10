using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;
using AispaStudio.Desktop.ViewModels;

namespace AispaStudio.Desktop.Tests;

/// <summary>Manager und Backoffice (ADR 0070) in der Windows-App.</summary>
public sealed class RoleTests
{
    private static CurrentUser Backoffice() =>
        new("u2", "backoffice@aispa.test", "Gerda", 0, new CurrentAccess(Guid.NewGuid(), "Test-Spa", StaffRole.Backoffice, true, []), null,
            [AccessRight.ViewFinance, AccessRight.ViewAmountReports, AccessRight.ViewReceipts, AccessRight.ViewStaffRecords]);

    private static ShellViewModel Shell(CurrentUser user) =>
        new(new StubApi().Client(), user, null, TimeProvider.System, new ThemeController(new MemorySettingsStore()), "", () => Task.CompletedTask);

    [Fact]
    public void Das_Backoffice_arbeitet_in_der_Verwaltung_ohne_Kalender_Kunden_und_Kassa()
    {
        var shell = Shell(Backoffice());

        Assert.False(shell.HasCalendar);
        Assert.Equal(["Verwaltung", "Hilfe"], shell.Pages.Select(p => p.Title));
        Assert.Equal(shell.AdminPage, shell.SelectedPage?.Page);
        shell.Dispose();
    }

    [Fact]
    public void Wer_den_Kalender_sehen_darf_behaelt_die_bisherigen_Seiten()
    {
        var shell = Shell(TestData.Reception());

        Assert.True(shell.HasCalendar);
        Assert.Equal(["Kalender", "Kunden", "Verlauf", "Kassa", "Geräte", "Verwaltung", "Hilfe"], shell.Pages.Select(p => p.Title).Where(t => t != "Anfragen"));
        shell.Dispose();
    }

    [Fact]
    public void Rollen_haben_Namen_und_Hilfe_Schluessel()
    {
        Assert.Equal("Manager", ShellViewModel.RoleText(StaffRole.GeneralManager));
        Assert.Equal("Backoffice", ShellViewModel.RoleText(StaffRole.Backoffice));
        Assert.Equal("Standortleitung", ShellViewModel.RoleText(StaffRole.Manager));
        Assert.Equal("manager", HelpContent.RoleKey(StaffRole.GeneralManager));
        Assert.Equal("backoffice", HelpContent.RoleKey(StaffRole.Backoffice));
        Assert.Equal("leitung", HelpContent.RoleKey(StaffRole.Manager));
    }

    [Fact]
    public void Der_Umsatz_im_Kalender_braucht_das_Recht_Berichte_mit_Betraegen()
    {
        var withoutRight = new CalendarViewModel(new StubApi().Client(), TestData.Reception(), null, TimeProvider.System, new MemorySettingsStore());
        var withRight = new CalendarViewModel(new StubApi().Client(), TestData.Reception() with
        {
            Rights = [.. TestData.Reception().Rights, AccessRight.ViewFinance, AccessRight.ViewAmountReports],
        }, null, TimeProvider.System, new MemorySettingsStore());
        var finance = new CalendarViewModel(new StubApi().Client(), TestData.Reception() with
        {
            Rights = [.. TestData.Reception().Rights, AccessRight.ViewFinance],
        }, null, TimeProvider.System, new MemorySettingsStore());

        Assert.False(withoutRight.CanSeeRevenue);
        Assert.False(finance.CanSeeRevenue);
        Assert.True(withRight.CanSeeRevenue);
    }
}

/// <summary>Seitenwechsel der Leiste ohne Seite (Absturz im Server-Betrieb, 23.10.2026).</summary>
public sealed class ShellNavigationTests
{
    [Fact]
    public void Keine_Seite_ausgewaehlt_stuerzt_im_Server_Betrieb_nicht_ab()
    {
        // Server-Betrieb: kein lokaler Speicher, also keine Seite „Abgleich“ (Sync ist null).
        var shell = new ShellViewModel(new StubApi().Client(), TestData.Reception(), null, TimeProvider.System,
            new ThemeController(new MemorySettingsStore()), "", () => Task.CompletedTask);
        Assert.Null(shell.Sync);

        shell.SelectedPage = null;

        Assert.Null(shell.SelectedPage);
        shell.SelectedPage = shell.Pages.First(p => p.Page == shell.AdminPage);
        Assert.Equal(shell.AdminPage, shell.SelectedPage?.Page);
    }
}
