using System.Text.RegularExpressions;

namespace AispaStudio.Desktop.Tests;

/// <summary>
/// T-108 (Auftraggeber, 26.11.2026, Punkt 6): Eine Pille, deren einziger Inhalt ein Text aus dem ViewModel ist, erscheint nie leer.
/// Sie trägt <c>IsVisible</c> auf demselben Text; Avalonia kennt keinen Selektor „Kind leer“. Der Scan über alle Ansichten fängt neue Pillen mit.
/// </summary>
public sealed partial class EmptyPillTests
{
    // Texte, die nie leer sind: feste Kennung der Sprache, Zustand der Live-Verbindung, Ergebnis der Signatur auf dem Beleg.
    private static readonly string[] AlwaysFilled = ["LanguageCode", "LiveText", "SignatureText"];

    [Fact]
    public void Leere_Pille_Eine_Pille_mit_nur_einem_gebundenen_Text_blendet_sich_bei_leerem_Text_aus()
    {
        var violations = ViewFiles().SelectMany(file => TextOnlyPillsWithoutVisibility(file.Path, file.Text)).ToList();

        Assert.True(violations.Count == 0, "Pillen ohne IsVisible auf dem Text:\n" + string.Join('\n', violations));
    }

    private static IEnumerable<string> TextOnlyPillsWithoutVisibility(string path, string xaml) =>
        TextOnlyPill().Matches(xaml)
            .Where(m => !m.Groups["tag"].Value.Contains("IsVisible", StringComparison.Ordinal))
            .Where(m => !AlwaysFilled.Contains(m.Groups["path"].Value))
            .Select(m => $"{Path.GetFileName(path)}:{xaml.AsSpan(0, m.Index).Count('\n') + 1}: Text={{Binding {m.Groups["path"].Value}}}");

    private static IEnumerable<(string Path, string Text)> ViewFiles()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AispaStudio.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        var views = Path.Combine(directory.FullName, "src", "AispaStudio.Desktop", "Views");
        return Directory.EnumerateFiles(views, "*.axaml", SearchOption.AllDirectories).Select(f => (f, File.ReadAllText(f)));
    }

    // Ein Border mit Klasse „pill“, dessen einziger Inhalt ein TextBlock mit reiner Textbindung ist (keine Zahl: ohne Klasse „num“, ohne Format, Pfad nicht leer).
    [GeneratedRegex("""<Border(?<tag>(?:(?!>)[\s\S])*?Classes="pill[^"]*"(?:(?!>)[\s\S])*?)>\s*<TextBlock Text="\{Binding (?<path>[\w.]+)\}"\s*/>\s*</Border>""")]
    private static partial Regex TextOnlyPill();
}
