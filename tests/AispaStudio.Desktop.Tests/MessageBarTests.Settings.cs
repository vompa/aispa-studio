using System.Text.RegularExpressions;

namespace AispaStudio.Desktop.Tests;

/// <summary>Meldungen und Statuszeilen in Sicherung, Abgleich, Geräten und Verwaltung (Runde R01, Slice 04: W10, W13, W17, W-Muster 2, 3, 13).</summary>
public sealed partial class MessageBarTests
{
    [Theory]
    [InlineData("BackupView.axaml")]
    [InlineData("SyncView.axaml")]
    [InlineData("DevicesView.axaml")]
    public void Meldungen_von_Sicherung_Abgleich_Geraeten_und_Verwaltung_sind_InfoBars_und_kein_Border_alert(string file)
        => Assert.DoesNotContain("Classes=\"alert\"", File.ReadAllText(Path.Combine(ViewsDir, file)), StringComparison.Ordinal);

    [Theory]
    [InlineData("BackupView.axaml", "Message")]
    [InlineData("SyncView.axaml", "Message")]
    [InlineData("DevicesView.axaml", "PrinterText")]
    [InlineData("DevicesView.axaml", "TerminalText")]
    [InlineData("DevicesView.axaml", "SavedText")]
    public void Statuszeilen_von_Sicherung_Abgleich_und_Geraeten_sind_hoeflich_angesagt(string file, string property)
    {
        var line = Regex.Match(File.ReadAllText(Path.Combine(ViewsDir, file)), $@"<TextBlock[^>]*Text=""\{{Binding {property}\}}""[^>]*>");
        Assert.True(line.Success);
        Assert.Contains("AutomationProperties.LiveSetting=\"Polite\"", line.Value, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("BackupView.axaml")]
    [InlineData("SyncView.axaml")]
    public void Warnungen_mit_Code_bestaetigt_man_mit_Verstanden_nicht_Notiert(string file)
    {
        var text = File.ReadAllText(Path.Combine(ViewsDir, file));
        Assert.Contains("ActionText=\"Verstanden\"", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Content=\"Notiert\"", text, StringComparison.Ordinal);
    }
}
