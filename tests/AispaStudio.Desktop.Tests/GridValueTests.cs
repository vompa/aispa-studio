using System.Text.RegularExpressions;

namespace AispaStudio.Desktop.Tests;

/// <summary>
/// Design-Brief 4.1, 4.2 und 4.4 (Runde R02, Slice 32: W-Muster 9, W3, W23, W41, W59): die Maße der Ansichten liegen auf dem Raster des Briefs
/// (Karte 20/16 bzw. 16/12, Abstände 12, 16, 24, Höhe 34, Hinweis Radius 8). Ausnahmen stehen hier mit Grund und im Brief 4.4.
/// </summary>
public sealed partial class GridValueTests
{
    private static readonly string RepoRoot = FindRepoRoot();
    private static readonly string DesktopRoot = Path.Combine(RepoRoot, "src", "AispaStudio.Desktop");

    /// <summary>Datei (relativ zu <c>src/AispaStudio.Desktop</c>), Attribut, Anzahl der Fundstellen und Grund.</summary>
    private static readonly (string File, string Attribute, int Count, string Reason)[] GridExceptions =
    [
        ("Views/PosView.axaml", "MinHeight=\"46\"", 1, "Bezahlknopf der Kassa: die eine Hauptaktion hebt sich aus der Zahlungsspalte heraus (Design-Brief 4.4)"),
    ];

    /// <summary>
    /// Prüft die Muster aus W-Muster 9, W3 und W59: Kartenränder 14/18/22/28 (auch als Teil eines mehrwertigen Innenabstands), 32 als einwertiger
    /// Kartenrand (Startkarten sind Dialoge mit 24, Brief 5.5), Abstände 14/18/28, eigene Höhen 40–52 und Radius 6 an Hinweisflächen.
    /// Kein allgemeiner Rasterscan: `Margin`, Setter in Styles.axaml und Hinweise als Karte (Radius 12) prüft er nicht.
    /// </summary>
    [Fact]
    public void Ansichten_nutzen_keine_Kartenraender_Abstaende_und_Hoehen_neben_dem_Raster()
    {
        var found = new List<(string File, string Attribute)>();
        foreach (var file in AxamlFiles())
        {
            var relative = Path.GetRelativePath(DesktopRoot, file).Replace('\\', '/');
            var text = File.ReadAllText(file);
            found.AddRange(OffGridAttribute().Matches(text).Select(m => (relative, m.Value)));
            found.AddRange(HintWithRadius6().Matches(text).Select(m => (relative, "CornerRadius=\"6\" an einem Hinweis")));
        }

        var problems = new List<string>();
        foreach (var group in found.GroupBy(f => f))
        {
            var allowed = GridExceptions.FirstOrDefault(e => e.File == group.Key.File && e.Attribute == group.Key.Attribute);
            if (allowed.File is null)
            {
                problems.Add($"{group.Key.File}: {group.Key.Attribute} ({group.Count()}x), Raster des Briefs: Karte 20/16 oder 16/12, Abstände 12/16/24, Höhe 34, Hinweis Radius 8");
            }
            else if (group.Count() != allowed.Count)
            {
                problems.Add($"{group.Key.File}: {group.Key.Attribute} kommt {group.Count()}x vor, erlaubt sind {allowed.Count}x ({allowed.Reason})");
            }
        }

        // Eine Ausnahme ohne Fundstelle ist veraltet und gehört aus der Liste (und aus Brief 4.4).
        problems.AddRange(GridExceptions.Where(e => !found.Contains((e.File, e.Attribute)))
            .Select(e => $"{e.File}: {e.Attribute} steht in der Ausnahmeliste, kommt aber nicht mehr vor ({e.Reason})"));

        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    [Fact]
    public void Ausnahmen_des_Rasters_stehen_im_Brief()
    {
        var brief = File.ReadAllText(Path.Combine(RepoRoot, "docs", "design.md"));
        var start = brief.IndexOf("### 4.4 Mindestmaße", StringComparison.Ordinal);
        var end = brief.IndexOf("### 4.5 Fokus", StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start, "Brief 4.4 nicht gefunden");
        var section = brief[start..end];
        Assert.Contains("Bezahlknopf", section, StringComparison.Ordinal);
        Assert.Contains("46 px", section, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("ShellView.axaml", "CompanionPanel")]
    [InlineData("CalendarView.axaml", "SidePanel")]
    public void Seitenleisten_sind_keine_schwebenden_Karten(string file, string name)
    {
        var text = File.ReadAllText(Path.Combine(DesktopRoot, "Views", file));
        var panel = Regex.Match(text, $@"<Border[^>]*x:Name=""{name}""[^>]*>");
        Assert.True(panel.Success, $"{name} nicht gefunden");
        Assert.DoesNotContain("card", panel.Value, StringComparison.Ordinal);
        Assert.DoesNotContain("Margin=", panel.Value, StringComparison.Ordinal);
    }

    private static IEnumerable<string> AxamlFiles() =>
        Directory.EnumerateFiles(DesktopRoot, "*.axaml", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal));

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AispaStudio.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("AispaStudio.slnx nicht gefunden.");
    }

    [GeneratedRegex(@"(?<![\w.])(?:Padding=""(?:32|[0-9,]*\b(?:14|18|22|28)\b[0-9,]*)""|(?:Column|Row|Item)?Spacing=""(?:14|18|28)""|MinHeight=""(?:40|42|44|46|52)"")")]
    private static partial Regex OffGridAttribute();

    [GeneratedRegex(@"CornerRadius=""6""[^\r\n>]*SoftBrush|SoftBrush[^\r\n>]*CornerRadius=""6""")]
    private static partial Regex HintWithRadius6();
}
