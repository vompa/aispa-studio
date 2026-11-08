using AispaStudio.Desktop.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>
/// T-038 (Ä378): Der Kopf von „Zahlen“ (fünf Reiter, Standort, Datum) bei schmalem Fenster. Die Reiter stehen immer in einer Zeile (Design-Brief 5.4), Standort und Datum rutschen in Zeile 2,
/// wenn sie neben den Reitern keinen Platz mehr haben. Gemessen (Plan 6.1, 06.11.2026): Reiter-Schiene 497 px, Standort und Datum 372 px, Abstand 16 px, zusammen 885 px.
/// Verfügbar für den Kopf: 964 px bei 1100 px Fenster, 604 px mit Seitenleiste „KI und Hilfe“; bei 1024 px 888 bzw. 528 px.
/// </summary>
public sealed partial class ViewTests
{
    private static Control ReportHeader(Window window) => Named<Control>(window, "ReportHeader");

    private static Border ReportRail(Window window) =>
        Named<Controls.TabButton>(window, "ReportBriefingTab").GetVisualAncestors().OfType<Border>().First(b => b.Classes.Contains("segmented"));

    private static StackPanel ReportFilters(Window window) =>
        Named<ComboBox>(window, "ReportLocationField").GetVisualAncestors().OfType<StackPanel>().First(p => p.Orientation == Avalonia.Layout.Orientation.Horizontal);

    private static double RightOf(Window window, Control control) => LeftOf(window, control) + control.Bounds.Width;

    private static double BottomOf(Window window, Control control) => TopOf(window, control) + control.Bounds.Height;

    private static List<Controls.TabButton> ReportTabs(Window window) =>
        [.. window.GetVisualDescendants().OfType<Controls.TabButton>().Where(t => t.IsEffectivelyVisible && t.Name is { } n && n.StartsWith("Report", StringComparison.Ordinal))];

    /// <summary>Stellt Fensterbreite und Seitenleiste ein (ohne Mindestbreite des Fensters) und wartet auf das Layout.</summary>
    private static async Task SetHeaderWidth(Window window, ShellViewModel shell, double width, bool companion)
    {
        window.MinWidth = 0;
        if (shell.Companion.IsOpen != companion)
        {
            shell.Companion.ToggleCommand.Execute(null);
        }

        window.Width = width;
        await Pump(Settling);
        await Pump(Settling);
        Assert.Equal(width, window.Bounds.Width, 0.5);
    }

    /// <summary>Alle fünf Reiter stehen auf einer Zeile und innerhalb des Kopfes (nicht nur „effektiv sichtbar“).</summary>
    private static void AssertTabsInOneLineWithinHeader(Window window)
    {
        var header = ReportHeader(window);
        var tabs = ReportTabs(window);
        Assert.Equal(5, tabs.Count);
        Assert.All(tabs, t => Assert.Equal(TopOf(window, tabs[0]), TopOf(window, t), 0.5));
        Assert.All(tabs, t => Assert.True(RightOf(window, t) <= RightOf(window, header) + 0.5, $"Reiter {t.Content} ragt über den Kopf: {RightOf(window, t):F1} > {RightOf(window, header):F1}"));
        Assert.Empty(ReportRail(window).GetVisualDescendants().OfType<ScrollViewer>());
    }

    private static void AssertFiltersBesideTabs(Window window)
    {
        var rail = ReportRail(window);
        var filters = ReportFilters(window);
        Assert.True(LeftOf(window, filters) >= RightOf(window, rail) + 15.5, "Standort und Datum müssen rechts neben der Schiene stehen");
        Assert.True(TopOf(window, filters) < BottomOf(window, rail) && BottomOf(window, filters) >= BottomOf(window, rail) - 0.5, "Standort und Datum müssen auf der Zeile der Reiter stehen");
        Assert.Equal(RightOf(window, ReportHeader(window)), RightOf(window, filters), 0.5);
    }

    private static void AssertFiltersBelowTabs(Window window)
    {
        var rail = ReportRail(window);
        var filters = ReportFilters(window);
        Assert.True(TopOf(window, filters) >= BottomOf(window, rail), $"Standort und Datum müssen unter den Reitern stehen: {TopOf(window, filters):F1} < {BottomOf(window, rail):F1}");
        Assert.Equal(RightOf(window, ReportHeader(window)), RightOf(window, filters), 0.5);
        Assert.True(LeftOf(window, filters) >= LeftOf(window, ReportHeader(window)) - 0.5);
    }

    [Fact]
    public Task Zahlen_haelt_den_Kopf_bei_Mindestbreite_in_einer_Zeile() => Render("hell", "probe-zahlen-kopf", async (window, main) =>
    {
        // Given „Zahlen“ ist offen; When das Fenster seine Mindestbreite 1100 px hat
        var (shell, _) = await OpenReports(window, main, ReportsViewModel.DayTab);
        await SetHeaderWidth(window, shell, 1100, companion: false);

        // Then stehen alle fünf Reiter, Standort und Datum auf einer Zeile, ohne Überlappung.
        AssertTabsInOneLineWithinHeader(window);
        AssertFiltersBesideTabs(window);
        Assert.True(Named<Controls.DateField>(window, "ReportDateField").IsEffectivelyVisible);
        return shell.AdminPage;
    }, ReportsApi());

    [Theory]
    [InlineData(1100)]
    [InlineData(1024)]
    public Task Zahlen_schiebt_Standort_und_Datum_bei_geoeffneter_Seitenleiste_unter_die_Reiter(int width) => Render("hell", $"probe-zahlen-kopf-{width}", async (window, main) =>
    {
        // Given „Zahlen“ ist offen; When die Seitenleiste „KI und Hilfe“ (360 px) aufgeht und das Fenster am Minimum steht
        var (shell, _) = await OpenReports(window, main, ReportsViewModel.DayTab);
        await SetHeaderWidth(window, shell, width, companion: true);

        // Then bleiben die Reiter auf einer Zeile innerhalb des Kopfes, Standort und Datum stehen rechtsbündig darunter.
        AssertTabsInOneLineWithinHeader(window);
        AssertFiltersBelowTabs(window);
        Assert.True(Named<ComboBox>(window, "ReportLocationField").IsEffectivelyVisible);
        Assert.True(Named<Controls.DateField>(window, "ReportDateField").IsEffectivelyVisible);
        return shell.AdminPage;
    }, ReportsApi());

    [Fact]
    public Task Zahlen_haelt_den_Kopf_bei_1024_px_ohne_Seitenleiste_ohne_Ueberlappung() => Render("hell", "probe-zahlen-kopf-1024", async (window, main) =>
    {
        // Given „Zahlen“ ist offen; When das Fenster 1024 px breit ist (Abnahme der Verwaltung, Slice 125), ohne Seitenleiste
        var (shell, _) = await OpenReports(window, main, ReportsViewModel.DayTab);
        await SetHeaderWidth(window, shell, 1024, companion: false);

        // Then ist nichts abgeschnitten, und Reiter und Filter überlappen nicht (eine oder zwei Zeilen, je nach Platz: 885 px Bedarf gegen 888 px).
        AssertTabsInOneLineWithinHeader(window);
        var rail = ReportRail(window);
        var filters = ReportFilters(window);
        Assert.True(RightOf(window, filters) <= RightOf(window, ReportHeader(window)) + 0.5);
        Assert.True(LeftOf(window, filters) >= RightOf(window, rail) || TopOf(window, filters) >= BottomOf(window, rail));
        return shell.AdminPage;
    }, ReportsApi());

    [Fact]
    public Task Zahlen_wechselt_beim_Verbreitern_wieder_in_eine_Zeile() => Render("hell", "probe-zahlen-kopf-breit", async (window, main) =>
    {
        // Given der Kopf ist bei 1100 px mit Seitenleiste zweizeilig
        var (shell, _) = await OpenReports(window, main, ReportsViewModel.DayTab);
        await SetHeaderWidth(window, shell, 1100, companion: true);
        AssertFiltersBelowTabs(window);

        // When die Seitenleiste zugeht und das Fenster auf 1440 px wächst
        await SetHeaderWidth(window, shell, 1440, companion: false);

        // Then stehen Standort und Datum wieder rechts neben den Reitern.
        AssertTabsInOneLineWithinHeader(window);
        AssertFiltersBesideTabs(window);
        return shell.AdminPage;
    }, ReportsApi());

    [Theory]
    [InlineData(1440, false)]
    [InlineData(1100, true)]
    public Task Zahlen_behaelt_die_Reihenfolge_Reiter_Standort_Datum_in_beiden_Zustaenden(int width, bool companion) => Render("hell", $"probe-zahlen-kopf-fokus-{width}", async (window, main) =>
    {
        // Given „Zahlen“ ist offen; When der Kopf einzeilig bzw. zweizeilig ist
        var (shell, _) = await OpenReports(window, main, ReportsViewModel.DayTab);
        await SetHeaderWidth(window, shell, width, companion);

        // Then ist die Tab-Reihenfolge die Quelltextreihenfolge: Schiene, dann Standort, dann Datum (der Panel ordnet nur an, die Kinder bleiben in dieser Reihenfolge).
        var header = Assert.IsType<Controls.ReflowRow>(ReportHeader(window));
        Assert.Same(ReportRail(window), header.Children[0]);
        Assert.Same(ReportFilters(window), header.Children[1]);
        var filters = (StackPanel)header.Children[1];
        Assert.Contains(filters.Children[0], Named<ComboBox>(window, "ReportLocationField").GetVisualAncestors());
        Assert.Contains(filters.Children[1], Named<Controls.DateField>(window, "ReportDateField").GetVisualAncestors());
        return shell.AdminPage;
    }, ReportsApi());

    [Fact]
    public Task Die_fuenf_Reiter_passen_bei_Mindestbreite_mit_Seitenleiste_in_eine_Zeile() => Render("hell", "probe-zahlen-kopf-waechter", async (window, main) =>
    {
        // Given „Zahlen“ mit allen fünf Reitern; When das Fenster am engsten Fall steht (1024 px mit Seitenleiste, Abnahme der Verwaltung)
        var (shell, _) = await OpenReports(window, main, ReportsViewModel.DayTab);
        await SetHeaderWidth(window, shell, 1024, companion: true);

        // Then passt die Schiene in den Kopf. Wird das falsch, scrollt die Schiene laut Design-Brief 5.4 waagrecht (nie umbrechen, kein Überlaufmenü).
        var rail = ReportRail(window);
        var header = ReportHeader(window);
        Assert.True(rail.DesiredSize.Width <= header.Bounds.Width, $"Reiter passen nicht mehr: Schiene scrollt laut design.md 5.4 (Schiene {rail.DesiredSize.Width:F0} px, Kopf {header.Bounds.Width:F0} px)");
        return shell.AdminPage;
    }, ReportsApi());

    [Fact]
    public Task ReflowRow_legt_zwei_Kinder_je_nach_Breite_in_eine_oder_zwei_Zeilen() => Session.Dispatch(() =>
    {
        // Given ein Kopf mit einem linken Kind (300 × 30) und einem rechten Kind (200 × 50), Abstand 16, Zeilenabstand 8
        var (row, left, right) = NewReflowRow();

        // When die Breite für beide reicht (516 px nötig)
        row.Measure(new Size(600, 200));
        row.Arrange(new Rect(0, 0, 600, row.DesiredSize.Height));

        // Then stehen sie auf einer Zeile, das rechte am rechten Rand.
        Assert.Equal(new Size(516, 50), row.DesiredSize);
        Assert.Equal(new Rect(0, 0, 300, 30), left.Bounds);
        Assert.Equal(new Rect(400, 0, 200, 50), right.Bounds);

        // When die Breite nicht reicht
        row.Measure(new Size(400, 200));
        row.Arrange(new Rect(0, 0, 400, row.DesiredSize.Height));

        // Then rutscht das rechte Kind rechtsbündig in Zeile 2.
        Assert.Equal(new Size(300, 88), row.DesiredSize);
        Assert.Equal(new Rect(0, 0, 300, 30), left.Bounds);
        Assert.Equal(new Rect(200, 38, 200, 50), right.Bounds);

        // When die Breite unbegrenzt ist, bleibt es eine Zeile.
        row.Measure(Size.Infinity);
        Assert.Equal(new Size(516, 50), row.DesiredSize);
        return Task.CompletedTask;
    }, CancellationToken.None);

    [Fact]
    public Task ReflowRow_laesst_ein_ausgeblendetes_Kind_weder_Platz_noch_Abstand_nehmen() => Session.Dispatch(() =>
    {
        // Given ein linkes und ein ausgeblendetes rechtes Kind (Filter bei der Betriebsinfo)
        var (row, left, right) = NewReflowRow();
        right.IsVisible = false;

        // When gemessen wird, auch bei knapper Breite
        row.Measure(new Size(310, 200));
        row.Arrange(new Rect(0, 0, 310, row.DesiredSize.Height));

        // Then gibt es weder Abstand noch Zeilenumbruch.
        Assert.Equal(new Size(300, 30), row.DesiredSize);
        Assert.Equal(new Rect(0, 0, 300, 30), left.Bounds);

        // Given das linke Kind fehlt, steht das rechte allein am rechten Rand.
        right.IsVisible = true;
        left.IsVisible = false;
        row.Measure(new Size(600, 200));
        row.Arrange(new Rect(0, 0, 600, row.DesiredSize.Height));
        Assert.Equal(new Size(200, 50), row.DesiredSize);
        Assert.Equal(new Rect(400, 0, 200, 50), right.Bounds);
        return Task.CompletedTask;
    }, CancellationToken.None);

    private static (Controls.ReflowRow Row, Border Left, Border Right) NewReflowRow()
    {
        var left = new Border { Width = 300, Height = 30, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top };
        var right = new Border { Width = 200, Height = 50, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top };
        var row = new Controls.ReflowRow { Spacing = 16, LineSpacing = 8 };
        row.Children.Add(left);
        row.Children.Add(right);
        return (row, left, right);
    }
}
