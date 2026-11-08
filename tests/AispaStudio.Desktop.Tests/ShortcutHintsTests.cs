using System.Text.RegularExpressions;
using AispaStudio.Desktop.Controls;

namespace AispaStudio.Desktop.Tests;

/// <summary>Fluent 2 F29: jedes Tastenkürzel steht im Tooltip und als <c>AcceleratorKey</c>; hier der Leser des Kürzels und der Scan des Quelltexts.</summary>
public sealed partial class ShortcutHintsTests
{
    private static readonly string Source = Path.Combine(FindRepoRoot(), "src", "AispaStudio.Desktop");

    [Theory]
    [InlineData("Vortag (←)", "←")]
    [InlineData("Nächster Tag (→)", "→")]
    [InlineData("Heute (Pos1)", "Pos1")]
    [InlineData("Stornieren (Entf)", "Entf")]
    [InlineData("Aktualisieren (F5)", "F5")]
    [InlineData("Datum wählen (G)", "G")]
    [InlineData("Tagesnotiz (.)", ".")]
    [InlineData("Rückgängig (Strg+Z)", "Strg+Z")]
    [InlineData("Vorige Seite (Strg+Umschalt+Tab)", "Strg+Umschalt+Tab")]
    [InlineData("Nummer 2 (Alt+2)", "Alt+2")]
    [InlineData("Schließen (Esc oder Strg+Leertaste)", "Esc oder Strg+Leertaste")]
    [InlineData("Zwischen Übersetzung und Original wechseln (Taste O)", "Taste O")]
    [InlineData("Ganzen Tag zeigen (Strg 0). Zoomen mit Strg + Mausrad.", "Strg 0")]
    [InlineData("KI und Hilfe (Strg+Leertaste)", "Strg+Leertaste")]
    public void Kuerzel_in_Klammern_wird_gelesen(string tip, string expected) => Assert.Equal(expected, ShortcutHints.Parse(tip));

    [Theory]
    [InlineData("Gast (optional)")]
    [InlineData("Dauer (min)")]
    [InlineData("Seite (2)")]
    [InlineData("Ohne Klammer")]
    [InlineData("Verfügbar, sobald ein Name eingetragen ist (a)")]
    [InlineData("")]
    public void Klammern_ohne_Kuerzel_werden_nicht_als_Kuerzel_gelesen(string tip) => Assert.Null(ShortcutHints.Parse(tip));

    /// <summary>Jeder Text im Quelltext, der ein Kürzel in Klammern nennt, muss vom Leser erkannt werden, sonst bekäme das Element keinen <c>AcceleratorKey</c>.</summary>
    [Fact]
    public void Kuerzel_im_Quelltext_werden_alle_erkannt()
    {
        var missed = new List<string>();
        foreach (var file in Directory.EnumerateFiles(Source, "*.*", SearchOption.AllDirectories).Where(IsSource))
        {
            foreach (Match match in NamedShortcut().Matches(File.ReadAllText(file)))
            {
                if (ShortcutHints.Parse(match.Groups["text"].Value) is null)
                {
                    missed.Add($"{Path.GetFileName(file)}: {match.Groups["text"].Value}");
                }
            }
        }

        Assert.True(missed.Count == 0, string.Join(Environment.NewLine, missed));
    }

    /// <summary>
    /// Ein Eintrag mit Kürzel nennt es im Text rechts (<c>menugesture</c>) und im <c>AcceleratorKey</c>; <c>InputGesture</c> entfällt (zeigt „Ctrl“ und „OemPeriod“,
    /// die Taste selbst gehört dem Kalender). <c>KeyBinding</c> gibt es nur für Enter in Eingabefeldern (steht in der Tastenhilfe, kein eigener Befehl mit Kürzel).
    /// </summary>
    [Fact]
    public void Menueeintraege_nennen_das_Kuerzel_im_Text_und_im_AcceleratorKey_und_ohne_InputGesture()
    {
        var problems = new List<string>();
        foreach (var file in Directory.EnumerateFiles(Source, "*.axaml", SearchOption.AllDirectories).Where(IsSource))
        {
            var name = Path.GetFileName(file);
            var text = File.ReadAllText(file);
            if (text.Contains("InputGesture=", StringComparison.Ordinal) || text.Contains("HotKey=", StringComparison.Ordinal))
            {
                problems.Add($"{name}: InputGesture oder HotKey");
            }

            problems.AddRange(KeyBindingGesture().Matches(text).Select(m => m.Groups["gesture"].Value).Where(g => g != "Enter").Select(g => $"{name}: KeyBinding {g} ohne Tooltip und AcceleratorKey"));
            foreach (Match item in MenuItemWithKey().Matches(text))
            {
                var key = item.Groups["key"].Value;
                if (!Regex.IsMatch(item.Value, $@"Text=""{Regex.Escape(key)}"" Classes=""menugesture"""))
                {
                    problems.Add($"{name}: Menüeintrag mit AcceleratorKey {key} ohne gleichen Text rechts");
                }
            }
        }

        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    private static bool IsSource(string file) =>
        (file.EndsWith(".axaml", StringComparison.Ordinal) || file.EndsWith(".cs", StringComparison.Ordinal))
        && !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
        && !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal);

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AispaStudio.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("AispaStudio.slnx nicht gefunden.");
    }

    // Nur Zeichenketten und Attributwerte, keine Kommentare: „…(Strg+Z)…“ in Anführungszeichen.
    [GeneratedRegex(@"""(?<text>[^""\n<>{}]*\((?:Strg|Alt|Umschalt|Esc|Enter|Entf|Pos1|F\d{1,2}|Taste [A-Z])[^()""\n]*\)[^""\n<>]*|[^""\n<>{}]*\([A-Z←→↑↓.]\)[^""\n<>]*)""")]
    private static partial Regex NamedShortcut();

    [GeneratedRegex(@"<KeyBinding[^>]*Gesture=""(?<gesture>[^""]+)""")]
    private static partial Regex KeyBindingGesture();

    [GeneratedRegex(@"<MenuItem[^>]*AutomationProperties\.AcceleratorKey=""(?<key>[^""]+)""[\s\S]*?</MenuItem>")]
    private static partial Regex MenuItemWithKey();
}
