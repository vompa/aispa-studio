namespace AispaStudio.Desktop.Tests;

/// <summary>Die Windows-App hat einen Toast: Die Shell zeigt ihn, keine Maske zeichnet einen eigenen (meldungen.md M1, Aufgabe T-083).</summary>
public sealed partial class MessageBarTests
{
    [Fact]
    public void Nur_die_Shell_zeichnet_einen_Toast()
    {
        var offenders = Directory.EnumerateFiles(ViewsDir, "*.axaml")
            .Where(file => File.ReadAllText(file).Contains("Classes=\"toast", StringComparison.Ordinal))
            .Select(Path.GetFileName);

        Assert.Equal(["ShellView.axaml"], offenders);
    }

    [Fact]
    public void Keine_Speichern_Leiste_zeigt_einen_Stand_neben_dem_Knopf()
    {
        var offenders = Directory.EnumerateFiles(ViewsDir, "*.axaml")
            .Where(file => SaveBars().IsMatch(File.ReadAllText(file)))
            .Select(Path.GetFileName);

        Assert.Empty(offenders);
    }

    [System.Text.RegularExpressions.GeneratedRegex(@"<ui:SaveBar\b[^>]*\bStatus=", System.Text.RegularExpressions.RegexOptions.Singleline)]
    private static partial System.Text.RegularExpressions.Regex SaveBars();
}
