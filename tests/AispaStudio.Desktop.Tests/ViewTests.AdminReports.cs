using AispaStudio.Contracts;
using AispaStudio.Desktop.ViewModels;
using Avalonia.Controls;
using Avalonia.VisualTree;
using static AispaStudio.Desktop.Tests.ReportSample;

namespace AispaStudio.Desktop.Tests;

/// <summary>R05 Slice 102 (Ä191): native Masken „Berichte“, „Kosten“, „Buchhaltung“ und „Betriebsinfo“ im Fenster: Reiter und Haken per Klick, Bildtests, Scans. Geprüft wird als Inhaberin.</summary>
public sealed partial class ViewTests
{
    private static readonly string[] OperationAreas = ["Kosten", "Buchhaltung"];

    private static readonly string[] AreasAfterReports = [.. OperationAreas, "Behandlungen"];

    private static StubApi ReportsApi() => AddBusinessInfo(AddCosts(AddReports(Api()), "2026-10"))
        .On("GET /api/pos/revenue-accounts", new List<RevenueAccountDto>())
        .On("GET /api/accounting/settings", new AccountingSettingsDto(null, null, 4, 1, "1370", "1000", "1360", "1200", "3250", "1460", "1370", "6960", "4960", "19=3;7=2"));

    private static async Task<(ShellViewModel Shell, ReportsViewModel Reports)> OpenReports(Window window, MainViewModel main, string tab)
    {
        var shell = await OpenAdminPage(window, main);
        shell.AdminPage.SelectedArea = shell.AdminPage.Areas.Single(a => a.Title == "Zahlen");
        await Pump(Settling);
        await Pump(Settling);
        Assert.Same(shell.AdminPage.Reports, shell.AdminPage.Content);
        var reports = shell.AdminPage.Reports!;
        reports.SelectTabCommand.Execute(tab);
        await Pump(Settling);
        await Pump(Settling);
        return (shell, reports);
    }

    private static async Task<ShellViewModel> OpenArea(Window window, MainViewModel main, string title)
    {
        var shell = await OpenAdminPage(window, main);
        shell.AdminPage.SelectedArea = shell.AdminPage.Areas.Single(a => a.Title == title);
        await Pump(Settling);
        await Pump(Settling);
        return shell;
    }

    [Theory]
    [InlineData("hell")]
    [InlineData("dunkel")]
    public Task Verwaltung_Berichte_Management(string theme) => Render(theme, "verwaltung-berichte-management", async (window, main) =>
    {
        var (shell, reports) = await OpenReports(window, main, ReportsViewModel.ManagementTab);
        reports.Management.Compare = true;
        await Pump(Settling);
        Assert.True(Shows(window, "12.345,50 €"));
        Assert.True(Shows(window, "Umsatz nach Erlöskonto"));
        Assert.True(Shows(window, "Beispieldaten"));
        return shell.AdminPage;
    }, ReportsApi());

    [Theory]
    [InlineData("hell")]
    [InlineData("dunkel")]
    public Task Verwaltung_Berichte_Tagesstart(string theme) => Render(theme, "verwaltung-berichte-tagesstart", async (window, main) =>
    {
        var (shell, _) = await OpenReports(window, main, ReportsViewModel.BriefingTab);
        Assert.True(Shows(window, "Ein ruhiger Tag mit einem Erstbesuch."));
        Assert.True(Shows(window, "Von der KI zusammengefasst."));
        Assert.True(Shows(window, "Bereitstellen"));
        return shell.AdminPage;
    }, ReportsApi());

    [Theory]
    [InlineData("hell")]
    [InlineData("dunkel")]
    public Task Verwaltung_Berichte_Tagesplan(string theme) => Render(theme, "verwaltung-berichte-tagesplan", async (window, main) =>
    {
        var (shell, _) = await OpenReports(window, main, ReportsViewModel.DayTab);
        Assert.True(Shows(window, "Vorbereitung"));
        Assert.True(Shows(window, "Letzte Behandlungen"));
        return shell.AdminPage;
    }, ReportsApi());

    [Theory]
    [InlineData("hell")]
    [InlineData("dunkel")]
    public Task Verwaltung_Berichte_Hygiene(string theme) => Render(theme, "verwaltung-berichte-hygiene", async (window, main) =>
    {
        var (shell, _) = await OpenReports(window, main, ReportsViewModel.HygieneTab);
        Assert.True(Shows(window, "Tagesstart je Raum"));
        Assert.True(Shows(window, "überfällig"));
        return shell.AdminPage;
    }, ReportsApi());

    [Theory]
    [InlineData("hell")]
    [InlineData("dunkel")]
    public Task Verwaltung_Berichte_Pflichtberichte(string theme) => Render(theme, "verwaltung-berichte-pflichtberichte", async (window, main) =>
    {
        var (shell, _) = await OpenReports(window, main, ReportsViewModel.ComplianceTab);
        Assert.True(Shows(window, "Kassa AT1 (AT)"));
        Assert.True(Shows(window, "Arbeitszeitaufzeichnung laut Dienstplan"));
        return shell.AdminPage;
    }, ReportsApi());

    [Theory]
    [InlineData("hell")]
    [InlineData("dunkel")]
    public Task Verwaltung_Kosten(string theme) => Render(theme, "verwaltung-kosten", async (window, main) =>
    {
        var shell = await OpenArea(window, main, "Kosten");
        Assert.Same(shell.AdminPage.Costs, shell.AdminPage.Content);
        Assert.True(Shows(window, "Klassische Massage"));
        return shell.AdminPage;
    }, ReportsApi());

    [Theory]
    [InlineData("hell")]
    [InlineData("dunkel")]
    public Task Verwaltung_Betriebsinfo(string theme) => Render(theme, "verwaltung-betriebsinfo", async (window, main) =>
    {
        var (shell, reports) = await OpenReports(window, main, ReportsViewModel.BusinessInfoTab);
        reports.BusinessInfo!.SelectedRow = reports.BusinessInfo.Rows.Single(r => r.Id == ContactId);
        await Pump(Settling);
        Assert.True(Shows(window, "Haftpflicht"));
        return shell.AdminPage;
    }, ReportsApi());

    [Theory]
    [InlineData("hell")]
    [InlineData("dunkel")]
    public Task Verwaltung_Buchhaltung(string theme) => Render(theme, "verwaltung-buchhaltung", async (window, main) =>
    {
        var shell = await OpenArea(window, main, "Buchhaltung");
        Assert.Same(shell.AdminPage.Accounting, shell.AdminPage.Content);
        Assert.True(Shows(window, "Export für die Buchhaltung"));
        return shell.AdminPage;
    }, ReportsApi());

    [Fact]
    public Task Reiter_und_Haken_der_Berichte_reagieren_auf_den_Klick()
    {
        var api = ReportsApi().On($"PUT /api/rooms/{RoomId}/start-checks/{CheckId}", new { });
        return Render("hell", "verwaltung-berichte-klick", async (window, main) =>
        {
            var (shell, reports) = await OpenReports(window, main, ReportsViewModel.DayTab);
            ClickCenter(window, Named<Controls.TabButton>(window, "ReportHygieneTab"));
            await Pump(Settling);
            await Pump(Settling);
            Assert.True(reports.IsHygieneTab);

            var box = window.GetVisualDescendants().OfType<CheckBox>().Single(c => c.Content as string == "Liege desinfiziert");
            ClickCenter(window, box);
            await Pump(Settling);
            await Pump(Settling);
            Assert.Equal(1, CallsTo(api, $"PUT /api/rooms/{RoomId}/start-checks/{CheckId}"));
            return shell.AdminPage;
        }, api);
    }

    [Fact]
    public Task Berichte_und_Betriebsmasken_melden_keine_Bindungsfehler() => Render("hell", "bindungen-berichte", async (window, main) =>
    {
        await WithBindingLog(async log =>
        {
            var shell = await OpenAdminPage(window, main);
            await AssertNoBindingErrors(log, "Zahlen", () =>
            {
                shell.AdminPage.SelectedArea = shell.AdminPage.Areas.Single(a => a.Title == "Zahlen");
                return Task.CompletedTask;
            });
            var reports = shell.AdminPage.Reports!;
            foreach (var tab in new[] { ReportsViewModel.BriefingTab, ReportsViewModel.HygieneTab, ReportsViewModel.ComplianceTab, ReportsViewModel.ManagementTab, ReportsViewModel.BusinessInfoTab, ReportsViewModel.DayTab })
            {
                await AssertNoBindingErrors(log, tab, () =>
                {
                    reports.SelectTabCommand.Execute(tab);
                    return Task.CompletedTask;
                });
            }

            await AssertNoBindingErrors(log, "Vergleich", () =>
            {
                reports.SelectTabCommand.Execute(ReportsViewModel.ManagementTab);
                reports.Management.Compare = true;
                return Task.CompletedTask;
            });
            foreach (var title in AreasAfterReports)
            {
                await AssertNoBindingErrors(log, title, () =>
                {
                    shell.AdminPage.SelectedArea = shell.AdminPage.Areas.Single(a => a.Title == title);
                    return Task.CompletedTask;
                });
            }

            log.AssertClean();
            return shell.AdminPage;
        });
        return true;
    }, ReportsApi());

    [Fact]
    public Task Jede_Seite_Zahlen_Kosten_Buchhaltung_hat_fuer_jedes_bedienbare_Element_einen_lesbaren_Namen() => Render("hell", "namen-berichte", async (window, main) =>
    {
        var problems = new SortedSet<string>(StringComparer.Ordinal);

        void Scan(string state)
        {
            foreach (var control in window.GetVisualDescendants().OfType<Control>().Where(c => c is Button or CheckBox or RadioButton or TextBox or ComboBox or ListBoxItem))
            {
                if (control.IsEffectivelyVisible && control.IsEffectivelyEnabled && control.Focusable && HasUnreadableName(control, out var name))
                {
                    problems.Add($"{state}: {control.GetType().Name}#{control.Name} \"{name}\"");
                }
            }
        }

        var (shell, reports) = await OpenReports(window, main, ReportsViewModel.DayTab);
        reports.DayPlan.Cards[0].EditPreferencesCommand.Execute(null);
        await Pump(Settling);
        Scan("Mein Tag");
        foreach (var tab in new[] { ReportsViewModel.BriefingTab, ReportsViewModel.HygieneTab, ReportsViewModel.ComplianceTab, ReportsViewModel.ManagementTab })
        {
            reports.SelectTabCommand.Execute(tab);
            await Pump(Settling);
            await Pump(Settling);
            Scan(tab);
        }

        reports.Management.Compare = true;
        await Pump(Settling);
        Scan("Vergleich");
        reports.SelectTabCommand.Execute(ReportsViewModel.BusinessInfoTab);
        await Pump(Settling);
        await Pump(Settling);
        Scan("Betriebsinfo");
        var info = reports.BusinessInfo!;
        info.SelectedRow = info.Rows.Single(r => r.Id == ContactId);
        await Pump(Settling);
        Scan("Eintrag");
        foreach (var title in OperationAreas)
        {
            shell.AdminPage.SelectedArea = shell.AdminPage.Areas.Single(a => a.Title == title);
            await Pump(Settling);
            await Pump(Settling);
            Scan(title);
        }

        Assert.True(problems.Count == 0, $"{problems.Count} Elemente:{Environment.NewLine}{string.Join(Environment.NewLine, problems)}");
        return shell.AdminPage;
    }, ReportsApi());
}
