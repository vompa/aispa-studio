using System.Text.RegularExpressions;

namespace AispaStudio.Desktop.Tests;

/// <summary>
/// Fluent 2 F26 (Vorbereitung für Hochkontrast): in den Ansichten und Stilen steht keine feste Farbe, alles läuft über <c>DynamicResource</c> auf <c>Colors.axaml</c>,
/// damit ein Kontrastdesign alle Farben ersetzen kann. Ausnahmen stehen hier mit Grund.
/// </summary>
public sealed partial class DesignTokenTests
{
    /// <summary>Festes Farbliteral, Datei (relativ zu <c>src/AispaStudio.Desktop</c>), Anzahl der Fundstellen und Grund.</summary>
    private static readonly (string File, string Literal, int Count, string Reason)[] ColorExceptions =
    [
        ("App.axaml", "#0d5c63", 1, "Palette der Systemvorlage (hell): Akzent; ColorPaletteResources nimmt nur eine Farbe, keinen Pinsel; gleicher Wert wie AccentBrush"),
        ("App.axaml", "#f3f0ea", 1, "Palette der Systemvorlage (hell): Fläche; gleicher Wert wie BgBrush"),
        ("App.axaml", "#52b3ba", 1, "Palette der Systemvorlage (dunkel): Akzent; gleicher Wert wie AccentBrush"),
        ("App.axaml", "#0c1211", 1, "Palette der Systemvorlage (dunkel): Fläche; gleicher Wert wie BgBrush"),
        ("Themes/Styles.axaml", "White", 2, "Initialen im Personenkreis und Kürzel in der Raumansicht: Text auf der Personenfarbe aus den Daten (Brief 5.8: #ffffff), nicht themenfähig"),
        ("Views/CalendarView.axaml", "Black", 2, "Deckkraftmaske der Verläufe am Rand des Rasters (Ä154): nur der Alphawert zählt, die sichtbare Farbe ist SurfaceBrush"),
    ];

    [Fact]
    public void Ansichten_und_Stile_haben_keine_festen_Farben()
    {
        var root = Path.Combine(RepoRoot, "src", "AispaStudio.Desktop");
        var found = new List<(string File, string Literal)>();
        foreach (var file in AxamlFiles().Where(f => !f.EndsWith(Path.Combine("Themes", "Colors.axaml"), StringComparison.Ordinal)))
        {
            var relative = Path.GetRelativePath(root, file).Replace('\\', '/');
            var text = File.ReadAllText(file);
            found.AddRange(HexLiteral().Matches(text).Select(m => (relative, m.Value.ToLowerInvariant())));
            found.AddRange(NamedColorValue().Matches(text).Select(m => (relative, m.Groups["name"].Value)));
        }

        var problems = new List<string>();
        foreach (var group in found.GroupBy(f => f))
        {
            var allowed = ColorExceptions.FirstOrDefault(e => e.File == group.Key.File && string.Equals(e.Literal, group.Key.Literal, StringComparison.OrdinalIgnoreCase));
            if (allowed.File is null)
            {
                problems.Add($"{group.Key.File}: {group.Key.Literal} ({group.Count()}x), stattdessen ein Pinsel aus Colors.axaml per DynamicResource");
            }
            else if (group.Count() != allowed.Count)
            {
                problems.Add($"{group.Key.File}: {group.Key.Literal} kommt {group.Count()}x vor, erlaubt sind {allowed.Count}x ({allowed.Reason})");
            }
        }

        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    [Fact]
    public void Ausnahmen_der_Palette_haben_dieselben_Werte_wie_die_Tokens()
    {
        Assert.Equal([AppValues(false, "AccentBrush")[0], AppValues(false, "BgBrush")[0], AppValues(true, "AccentBrush")[0], AppValues(true, "BgBrush")[0]],
            ColorExceptions.Where(e => e.File == "App.axaml").Select(e => e.Literal).ToList());
    }

    [GeneratedRegex(@"#[0-9a-fA-F]{3,8}\b")]
    private static partial Regex HexLiteral();

    [GeneratedRegex(@"=""(?<name>White|Black|Red|Green|Blue|Gray|Grey|Yellow|Orange|Silver|Navy|Teal)""")]
    private static partial Regex NamedColorValue();
}
