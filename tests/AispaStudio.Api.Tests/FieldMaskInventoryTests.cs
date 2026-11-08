using System.Text.RegularExpressions;
using AispaStudio.Api.Assistant.Answers;

namespace AispaStudio.Api.Tests;

/// <summary>Jedes Sprungziel in die Verwaltung, das die API erzeugt, steht im Masken-Inventar (Slice 103).</summary>
public sealed partial class FieldMaskInventoryTests
{
    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AispaStudio.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return directory.FullName;
    }

    [Fact]
    public void Jedes_Sprungziel_des_Assistenten_steht_im_Masken_Inventar()
    {
        var root = RepositoryRoot();
        var inventory = File.ReadAllText(Path.Combine(root, "docs", "umsetzung", "masken-inventar.md"));
        var section = inventory[inventory.IndexOf("## Sprungziele in die Verwaltung", StringComparison.Ordinal)..];
        section = section[..section.IndexOf("\n## Bleibt im Web", StringComparison.Ordinal)];
        var listed = section.Split('\n')
            .Where(line => line.StartsWith('|'))
            .Select(line => line.Split('|')[2])
            .SelectMany(cell => BacktickValue().Matches(cell).Select(m => m.Groups[1].Value))
            .ToHashSet(StringComparer.Ordinal);

        var produced = FieldMasks.All.Select(m => Key(m.Page, m.Tab)).ToHashSet(StringComparer.Ordinal);
        var answers = Path.Combine(root, "src", "AispaStudio.Api", "Assistant", "Answers");
        foreach (var file in Directory.EnumerateFiles(answers, "*.cs"))
        {
            var source = File.ReadAllText(file);
            foreach (Match m in SettingCall().Matches(source))
            {
                produced.Add(Key(m.Groups[1].Value, m.Groups[2].Success ? m.Groups[2].Value : null));
            }

            foreach (Match m in LinkLiteral().Matches(source))
            {
                produced.Add(Key(m.Groups[1].Value, m.Groups[2].Value));
            }
        }

        Assert.NotEmpty(listed);
        Assert.Contains("masterData/closures", produced);
        Assert.Contains("masterData/subscriptions", produced);
        Assert.Contains("reports/management", produced);
        Assert.Empty(produced.Except(listed));
    }

    private static string Key(string page, string? tab) => tab is null ? page : $"{page}/{tab}";

    [GeneratedRegex("`([A-Za-z]+(?:/[A-Za-z]+)?)`")]
    private static partial Regex BacktickValue();

    [GeneratedRegex("""\.Setting\([^,]+,\s*"[^"]*",\s*"(\w+)",\s*(?:"(\w+)"|null)\)""")]
    private static partial Regex SettingCall();

    [GeneratedRegex("""new AssistantLink\(AssistantLinkKind\.\w+,\s*"[^"]*",\s*"(\w+)",\s*"(\w+)""")]
    private static partial Regex LinkLiteral();
}
