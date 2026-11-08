using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AispaStudio.Desktop.Tests;

/// <summary>
/// Design-Brief Abschnitt 3 (Fluent 2 F23): die Schriftstufen stehen in <c>docs/design-tokens.json</c> und gelten in Web und App gleich;
/// außerhalb der Stufen kommt keine Schriftgröße vor.
/// </summary>
public sealed partial class DesignTokenTests
{
    private static readonly string StylesAxaml = File.ReadAllText(Path.Combine(RepoRoot, "src", "AispaStudio.Desktop", "Themes", "Styles.axaml"));

    /// <summary>Größen, die der Brief nicht als Stufe führt, mit Grund (Datei, Größe).</summary>
    private static readonly (string File, double Size, string Reason)[] SizeExceptions =
    [
        ("LoginView.axaml", 34, "Anmeldemaske: Markenname als Anzeigegröße, im Brief nicht geregelt (Entscheidung des Auftraggebers offen)"),
        ("StartupScreen.axaml", 34, "Startbildschirm: derselbe Markenname wie in der Anmeldung, wandert an dessen Platz (Brief 5.31)"),
        ("LoginView.axaml", 24,"Anmeldemaske: PIN- und Code-Eingabe als Anzeigegröße, im Brief nicht geregelt (Entscheidung des Auftraggebers offen)"),
    ];

    public static TheoryData<string, double, double, int, string?, string> Levels()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(RepoRoot, "docs", "design-tokens.json")));
        var data = new TheoryData<string, double, double, int, string?, string>();
        foreach (var level in document.RootElement.GetProperty("schriftstufen").EnumerateArray())
        {
            data.Add(
                level.GetProperty("name").GetString()!,
                level.GetProperty("groesse").GetDouble(),
                level.GetProperty("zeile").GetDouble(),
                level.GetProperty("gewicht").GetInt32(),
                level.TryGetProperty("web", out var web) ? web.GetString() : null,
                level.GetProperty("app").GetString()!);
        }

        return data;
    }

    private static double[] AllowedSizes()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(RepoRoot, "docs", "design-tokens.json")));
        return [.. document.RootElement.GetProperty("schriftgroessen").EnumerateArray().Select(e => e.GetDouble())];
    }

    [Theory]
    [MemberData(nameof(Levels))]
    public void Schriftstufe_hat_in_der_App_Groesse_Zeilenhoehe_und_Gewicht(string name, double size, double line, int weight, string? web, string selector)
    {
        _ = web;
        var block = Regex.Match(StylesAxaml, $@"<Style Selector=""{Regex.Escape(selector)}"">([\s\S]*?)</Style>");
        Assert.True(block.Success, $"{name}: Stil {selector} fehlt in Styles.axaml");
        Assert.Equal(size, AxamlNumber(block.Groups[1].Value, "FontSize"));
        Assert.Equal(line, AxamlNumber(block.Groups[1].Value, "LineHeight"));
        Assert.Equal(weight, AxamlWeight(block.Groups[1].Value));
    }

    [Theory]
    [MemberData(nameof(Levels))]
    public void Schriftstufe_hat_im_Web_dieselben_Werte(string name, double size, double line, int weight, string? web, string selector)
    {
        _ = selector;
        if (web is null)
        {
            return;
        }

        var block = Regex.Match(IndexCss, $@"(?m)^{Regex.Escape(web)} \{{([^}}]*)\}}");
        Assert.True(block.Success, $"{name}: Regel {web} fehlt in index.css");
        Assert.Equal(size, CssNumber(block.Groups[1].Value, "font-size"));
        Assert.Equal(line, CssNumber(block.Groups[1].Value, "line-height"));
        Assert.Equal(weight, (int)CssNumber(block.Groups[1].Value, "font-weight"));
    }

    [Fact]
    public void App_nutzt_nur_Schriftgroessen_der_Stufen()
    {
        var allowed = AllowedSizes();
        var violations = new List<string>();
        foreach (var file in AxamlFiles())
        {
            var text = File.ReadAllText(file);
            foreach (Match match in FontSizeValue().Matches(text))
            {
                var size = double.Parse(match.Groups["size"].Value, CultureInfo.InvariantCulture);
                var name = Path.GetFileName(file);
                if (!allowed.Contains(size) && !SizeExceptions.Any(e => e.File == name && e.Size == size))
                {
                    violations.Add($"{name}: {match.Value}");
                }
            }
        }

        Assert.True(violations.Count == 0, string.Join(Environment.NewLine, violations));
    }

    [Fact]
    public void Web_nutzt_nur_Schriftgroessen_der_Stufen()
    {
        var allowed = AllowedSizes();
        var violations = FontSizePx().Matches(IndexCss)
            .Select(m => double.Parse(m.Groups["size"].Value, CultureInfo.InvariantCulture))
            .Where(size => !allowed.Contains(size))
            .Distinct()
            .ToList();
        Assert.True(violations.Count == 0, "Größen außerhalb der Stufen in index.css: " + string.Join(", ", violations));
    }

    [Fact]
    public void App_und_Web_nutzen_nur_die_Schriftgewichte_400_500_600()
    {
        var bad = Regex.Matches(StylesAxaml, @"FontWeight"" Value=""(?!Normal|Medium|SemiBold)\w+""").Select(m => m.Value).ToList();
        Assert.True(bad.Count == 0, string.Join("; ", bad));
        // 700 steht nur im Belegpapier (Brief 10: Schwarz auf Weiß für den Druck, Summenzeile).
        var css = Regex.Matches(IndexCss, @"font-weight:\s*(\d+)").Select(m => int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture)).Distinct().Where(w => w is not (400 or 500 or 600 or 700)).ToList();
        Assert.Empty(css);
    }

    /// <summary>Brief 3 und 10: keine Versalien zur Betonung, kein Kursiv, kein <c>TextBlock.overline</c> (F24). Ausnahmen: die Wortmarke „AISPA“ (Brief 1) und das Eingabemuster „ABCD-EFGH“ des Anmeldecodes.</summary>
    [Fact]
    public void App_hat_keine_Overline_Versalien_oder_Kursivschrift()
    {
        var problems = new List<string>();
        foreach (var file in AxamlFiles())
        {
            var name = Path.GetFileName(file);
            var text = File.ReadAllText(file);
            if (text.Contains("overline", StringComparison.Ordinal))
            {
                problems.Add($"{name}: overline");
            }

            if (text.Contains("Italic", StringComparison.Ordinal) || text.Contains("TextTransform", StringComparison.Ordinal))
            {
                problems.Add($"{name}: Kursiv oder TextTransform");
            }

            problems.AddRange(CapitalLiteral().Matches(text).Select(m => m.Groups["text"].Value).Where(t => t is not ("AISPA" or "ABCD-EFGH")).Select(t => $"{name}: Versalien \"{t}\""));
        }

        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    private static IEnumerable<string> AxamlFiles() =>
        Directory.EnumerateFiles(Path.Combine(RepoRoot, "src", "AispaStudio.Desktop"), "*.axaml", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal));

    [GeneratedRegex(@"(?:Text|Header|Content)=""(?<text>[A-ZÄÖÜ][A-ZÄÖÜ0-9 ,.:?-]{3,})""")]
    private static partial Regex CapitalLiteral();

    private static double AxamlNumber(string block, string property)
    {
        var match = Regex.Match(block, $@"Property=""{property}"" Value=""([0-9.]+)""");
        return match.Success ? double.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) : double.NaN;
    }

    private static int AxamlWeight(string block)
    {
        var match = Regex.Match(block, @"Property=""FontWeight"" Value=""(\w+)""");
        return !match.Success ? 400 : match.Groups[1].Value switch
        {
            "Medium" => 500,
            "SemiBold" => 600,
            var other => throw new InvalidOperationException($"Gewicht {other} ist nicht im Brief"),
        };
    }

    private static double CssNumber(string block, string property)
    {
        var match = Regex.Match(block, $@"{property}:\s*([0-9.]+)");
        return match.Success ? double.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) : double.NaN;
    }

    [GeneratedRegex(@"(?:FontSize=""|Property=""FontSize"" Value="")(?<size>[0-9.]+)""")]
    private static partial Regex FontSizeValue();

    [GeneratedRegex(@"font-size:\s*(?<size>[0-9.]+)px")]
    private static partial Regex FontSizePx();
}
