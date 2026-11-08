using System.Text.Json;
using System.Text.RegularExpressions;

namespace AispaStudio.Desktop.Tests;

/// <summary>
/// Design-Brief Abschnitt 2 (G2): Web (<c>index.css</c>) und Windows-App (<c>Colors.axaml</c>) tragen dieselben Farben.
/// Die Paare stehen in <c>docs/design-tokens.json</c>; das Gegenstück für das Web ist <c>web/src/designTokens.test.ts</c>.
/// </summary>
public sealed partial class DesignTokenTests
{
    private static readonly string RepoRoot = FindRepoRoot();
    private static readonly string IndexCss = File.ReadAllText(Path.Combine(RepoRoot, "web", "src", "index.css"));
    private static readonly string ColorsAxaml = File.ReadAllText(Path.Combine(RepoRoot, "src", "AispaStudio.Desktop", "Themes", "Colors.axaml"));

    public static TheoryData<string, string, bool> Tokens()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(RepoRoot, "docs", "design-tokens.json")));
        var data = new TheoryData<string, string, bool>();
        foreach (var token in document.RootElement.GetProperty("tokens").EnumerateArray())
        {
            foreach (var dark in new[] { false, true })
            {
                data.Add(token.GetProperty("web").GetString()!, token.GetProperty("app").GetString()!, dark);
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Tokens))]
    public void Web_und_App_haben_denselben_Farbwert(string web, string app, bool dark)
    {
        var webValue = WebValues(dark, web);
        Assert.True(webValue.Count > 0, $"{web} fehlt in index.css ({Theme(dark)})");
        Assert.True(webValue.SequenceEqual(AppValues(dark, app)), $"{web} (Web) gegen {app} (App), {Theme(dark)}");
    }

    // Brief 2.6 und 2.1 bis 2.4: Text auf seiner Fläche mindestens 4,5:1 (WCAG 2.2 AA).
    [Fact]
    public void Erledigt_hell_hat_Kontrast_von_mindestens_4_5_zu_1()
    {
        var text = AppValues(dark: false, "StDoneBrush").Single();
        var background = AppValues(dark: false, "StDoneBgBrush").Single();
        Assert.True(Contrast(text, background) >= 4.5, $"StDone {text} auf StDoneBg {background}: {Contrast(text, background):0.00}:1");
    }

    // R05 Slice 125: Ruhe rail-fg-2 auf rail-hover, Überfahren rail-fg auf rail-bar-hover, gewählt rail-fg auf rail-selected (Brief 6).
    [Theory]
    [InlineData(false, "RailFg2Brush", "RailHoverBrush")]
    [InlineData(false, "RailFgBrush", "RailBarHoverBrush")]
    [InlineData(false, "RailFgBrush", "RailSelectedBrush")]
    [InlineData(true, "RailFg2Brush", "RailHoverBrush")]
    [InlineData(true, "RailFgBrush", "RailBarHoverBrush")]
    [InlineData(true, "RailFgBrush", "RailSelectedBrush")]
    public void Balken_der_Verwaltung_hat_in_jedem_Zustand_Textkontrast_von_mindestens_4_5_zu_1(bool dark, string text, string surface)
    {
        var contrast = Contrast(AppValues(dark, text).Single(), AppValues(dark, surface).Single());
        Assert.True(contrast >= 4.5, $"{text} auf {surface} ({Theme(dark)}): {contrast:0.00}:1");
    }

    // T-039 (Ä372): Tinte des Dienst-Chips auf der Basis, auf der die Personenfarbe gemischt wird (Brief 5.8); die Fläche selbst entsteht im Code, ihren Kontrast prüft RosterWeekLayoutTests.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Dienst_Chip_Tinte_hat_auf_der_Basis_in_beiden_Schemata_mindestens_4_5_zu_1(bool dark)
    {
        var ink = AppValues(dark, "RosterChipInkBrush").Single();
        var chipBase = AppValues(dark, "RosterChipBaseBrush").Single();
        Assert.True(Contrast(ink, chipBase) >= 4.5, $"RosterChipInk {ink} auf RosterChipBase {chipBase} ({Theme(dark)}): {Contrast(ink, chipBase):0.00}:1");
    }

    [Fact]
    public void Dienst_Chip_Tokens_stehen_im_Brief_mit_ihren_Werten()
    {
        var brief = File.ReadAllText(Path.Combine(RepoRoot, "docs", "design.md"));
        foreach (var dark in new[] { false, true })
        {
            foreach (var key in new[] { "RosterChipBaseBrush", "RosterChipInkBrush" })
            {
                var value = AppValues(dark, key).Single();
                Assert.Contains(key, brief, StringComparison.Ordinal);
                Assert.True(brief.Contains($"`{value}`", StringComparison.OrdinalIgnoreCase), $"{key} ({Theme(dark)}) = {value} steht nicht im Brief");
            }
        }

        Assert.Contains("roster-chip-base", brief, StringComparison.Ordinal);
        Assert.Contains("roster-chip-ink", brief, StringComparison.Ordinal);
    }

    private static double Contrast(string first, string second)
    {
        var lighter = Math.Max(Luminance(first), Luminance(second));
        var darker = Math.Min(Luminance(first), Luminance(second));
        return (lighter + 0.05) / (darker + 0.05);
    }

    private static readonly int[] RgbOffsets = [1, 3, 5];

    private static double Luminance(string hex)
    {
        var channels = RgbOffsets.Select(i => Convert.ToInt32(hex.Substring(i, 2), 16) / 255.0)
            .Select(c => c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4))
            .ToArray();
        return 0.2126 * channels[0] + 0.7152 * channels[1] + 0.0722 * channels[2];
    }

    private static string Theme(bool dark) => dark ? "dunkel" : "hell";

    private static List<string> HexColors(string text) =>
        HexColor().Matches(text).Select(m => m.Value.ToLowerInvariant()).ToList();

    private static List<string> WebValues(bool dark, string name)
    {
        var block = dark ? DarkRoot().Match(IndexCss) : LightRoot().Match(IndexCss);
        var value = Regex.Match(block.Groups[1].Value, $@"{Regex.Escape(name)}:\s*([^;]+);");
        return value.Success ? HexColors(value.Groups[1].Value) : [];
    }

    internal static List<string> AppValues(bool dark, string name)
    {
        var start = ColorsAxaml.IndexOf(dark ? "x:Key=\"Dark\"" : "x:Key=\"Light\"", StringComparison.Ordinal);
        var end = dark ? ColorsAxaml.Length : ColorsAxaml.IndexOf("x:Key=\"Dark\"", StringComparison.Ordinal);
        var dictionary = ColorsAxaml[start..end];
        var solid = Regex.Match(dictionary, $@"<SolidColorBrush x:Key=""{Regex.Escape(name)}"" Color=""(#[0-9a-fA-F]+)""");
        if (solid.Success)
        {
            return [solid.Groups[1].Value.ToLowerInvariant()];
        }

        var gradient = Regex.Match(dictionary, $@"<LinearGradientBrush x:Key=""{Regex.Escape(name)}""[^>]*>([\s\S]*?)</LinearGradientBrush>");
        return gradient.Success ? HexColors(gradient.Groups[1].Value) : [];
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AispaStudio.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("AispaStudio.slnx nicht gefunden.");
    }

    [GeneratedRegex(@"#[0-9a-fA-F]{6,8}\b")]
    private static partial Regex HexColor();

    [GeneratedRegex(@":root\s*\{([^}]*)\}")]
    private static partial Regex LightRoot();

    [GeneratedRegex(@"@media \(prefers-color-scheme: dark\)\s*\{\s*:root\s*\{([^}]*)\}")]
    private static partial Regex DarkRoot();
}
