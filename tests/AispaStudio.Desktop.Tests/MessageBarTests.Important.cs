using System.Text.RegularExpressions;

namespace AispaStudio.Desktop.Tests;

/// <summary>Wichtige Meldungen (verhindern ein Ergebnis oder verlangen eine Entscheidung) sind InfoBars, nie kleiner Text (Runde R04, Slice 88: Ä156).</summary>
public sealed partial class MessageBarTests
{
    /// <summary>Eigenschaften, die Fehler oder Hindernisse tragen; sie stehen nur in <c>ui:InfoBar Message</c>.</summary>
    [Theory]
    [InlineData("Error")]
    [InlineData("ErrorText")]
    [InlineData("ReplyNotice")]
    [InlineData("NoSlotsMessage")]
    public void Wichtige_Meldungs_Eigenschaften_stehen_in_keiner_Ansicht_als_TextBlock(string property)
    {
        var offenders = Directory.EnumerateFiles(ViewsDir, "*.axaml")
            .SelectMany(file => TextBlocks().Matches(File.ReadAllText(file)).Select(m => (file, m.Value)))
            .Where(t => Regex.IsMatch(t.Value, @"Text=""\{Binding " + property + @"[}, ]"))
            .Select(t => $"{Path.GetFileName(t.file)}: {t.Value}");

        Assert.Empty(offenders);
    }

    [Fact]
    public void Der_Satz_Ueberblick_nicht_geladen_steht_in_einer_InfoBar()
    {
        var xaml = View("CalendarView.axaml");
        Assert.Matches(@"<ui:InfoBar[^>]*Message=""Der Überblick wurde nicht geladen\.""", xaml);
        Assert.DoesNotContain("<TextBlock Text=\"Der Überblick wurde nicht geladen.\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void Freie_Zeit_ohne_Ergebnis_zeigt_eine_InfoBar_mit_Weg()
    {
        Assert.Matches(@"<ui:InfoBar[^>]*Message=""\{Binding NoSlotsMessage\}""[^>]*ActionText=""\{Binding RelaxText\}""[^>]*ActionCommand=""\{Binding RelaxCommand\}""", View("FreeTimeView.axaml"));
    }

    [GeneratedRegex(@"<TextBlock\b[^>]*>", RegexOptions.Singleline)]
    private static partial Regex TextBlocks();
}
