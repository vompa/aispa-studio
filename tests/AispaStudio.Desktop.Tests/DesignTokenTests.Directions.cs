using System.Text.RegularExpressions;

namespace AispaStudio.Desktop.Tests;

/// <summary>
/// Texte nennen keine Richtung (W-Muster 10, fluent2 content-design §3): die Lage auf dem Bildschirm hängt von Fenstergröße und Screenreader ab.
/// Geprüft werden die Ansichten, die die Befundliste nannte; <c>AiLabels.cs</c> folgt Brief 5.28 und bleibt außen vor.
/// </summary>
public sealed partial class DesignTokenTests
{
    private static readonly string[] DirectionCheckedViews = ["CustomersView.axaml", "InboxView.axaml", "LoginView.axaml", "PosView.axaml"];

    [Fact]
    public void Texte_von_Kunden_Posteingang_Anmeldung_und_Kassa_nennen_keine_Richtung()
    {
        var root = Path.Combine(RepoRoot, "src", "AispaStudio.Desktop");
        var problems = new List<string>();
        foreach (var view in DirectionCheckedViews)
        {
            var text = File.ReadAllText(Path.Combine(root, "Views", view));
            problems.AddRange(ShownText().Matches(text).Select(m => m.Groups["text"].Value).Where(t => Direction().IsMatch(t)).Select(t => $"{view}: {t}"));
        }

        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    [GeneratedRegex(@"\b(?:Text|Content|ToolTip\.Tip|PlaceholderText|Label|Help|Message)=""(?<text>[^""{][^""]*)""")]
    private static partial Regex ShownText();

    [GeneratedRegex(@"\b(?:links|rechts|oben|unten)\b", RegexOptions.IgnoreCase)]
    private static partial Regex Direction();
}
