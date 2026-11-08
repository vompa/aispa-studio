using System.Text.RegularExpressions;

namespace AispaStudio.Desktop.Tests;

/// <summary>Technikdeutsch im Fließtext der Seiten „Im Netz“, „Abgleich“ und „Geräte“ (Runde R02, Slice 35: W8): nur unter „Technische Angaben“.</summary>
public sealed partial class MessageBarTests
{
    [Theory]
    [InlineData("NetworkView.axaml", "--standalone")]
    [InlineData("NetworkView.axaml", "Zertifikat")]
    [InlineData("SyncView.axaml", "PostgreSQL")]
    [InlineData("SyncView.axaml", "SSL Mode")]
    [InlineData("DevicesView.axaml", "ESC/POS")]
    [InlineData("DevicesView.axaml", "Port 9100")]
    [InlineData("DevicesView.axaml", "ZVT")]
    public void Technikdeutsch_steht_nur_unter_Technische_Angaben(string file, string technical)
    {
        var xaml = File.ReadAllText(Path.Combine(ViewsDir, file));
        Assert.Contains(technical, xaml, StringComparison.Ordinal);
        var withoutDetails = TechnicalDetailsElement().Replace(xaml, "");
        Assert.DoesNotContain(technical, withoutDetails, StringComparison.Ordinal);
    }

    [GeneratedRegex(@"<ui:TechnicalDetails\b[^>]*/>")]
    private static partial Regex TechnicalDetailsElement();
}
