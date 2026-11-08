using System.Text.RegularExpressions;
using AispaStudio.Contracts;
using AispaStudio.Desktop.ViewModels;

namespace AispaStudio.Desktop.Tests;

/// <summary>
/// Ein Begriff „Mitarbeitende“ und kurze Tooltips (Slice 79, Ä101, Ä102; docs/gestaltung/texte.md T12 und T22):
/// geprüft werden die festen Texte der Views und die Zeichenketten im Code sowie die Hinweise der Kalender-Kopfzeile.
/// </summary>
public sealed partial class TermsAndTooltipTests
{
    private static readonly string Root = FindRoot();

    private static readonly TimeZoneInfo Vienna = TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna");

    private sealed class FixedTime : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => TestData.At(10);

        public override TimeZoneInfo LocalTimeZone => Vienna;
    }

    [Fact]
    public void Given_die_Oberflaechentexte_When_nach_MA_Personen_ohne_Dienst_und_Therapeuten_gesucht_wird_Then_gibt_es_keinen_Treffer()
    {
        var problems = new List<string>();
        foreach (var file in DesktopFiles("*.axaml").Concat(DesktopFiles("*.cs")))
        {
            foreach (var text in ShownTexts(file))
            {
                problems.AddRange(OldTerm().Matches(text).Select(m => $"{Path.GetFileName(file)}: „{m.Value}“ in „{text}“"));
            }
        }

        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    [Fact]
    public void Given_ein_fester_Tooltip_der_Windows_App_When_seine_Laenge_geprueft_wird_Then_hoechstens_ein_Satz_ohne_Beschriftung()
    {
        var problems = new List<string>();
        foreach (var file in DesktopFiles("*.axaml"))
        {
            var xaml = File.ReadAllText(file);
            foreach (Match m in FixedTip().Matches(xaml))
            {
                var tag = TagAround(xaml, m.Index);
                var label = TagLabel().Match(tag) is { Success: true } l ? l.Groups["text"].Value : null;
                problems.AddRange(TipProblems(m.Groups["text"].Value, label).Select(p => $"{Path.GetFileName(file)}: {p}"));
            }
        }

        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    [Fact]
    public async Task Given_die_Kopfzeile_des_Kalenders_When_die_Hinweise_der_Chips_geprueft_werden_Then_hoechstens_ein_Satz_ohne_Beschriftung()
    {
        var vm = await OpenAsync();

        var tips = new (string Name, string Tip, string Label)[]
        {
            (nameof(vm.AllStaffTip), vm.AllStaffTip, vm.AllStaffText),
            (nameof(vm.OverviewTip), vm.OverviewTip, vm.OverviewChip),
            (nameof(vm.MessagesTip), vm.MessagesTip, vm.MessagesButtonText),
            (nameof(vm.SubscriptionsTip), vm.SubscriptionsTip, "Kalender-Abos"),
            (nameof(vm.WaitlistTip), vm.WaitlistTip, "Warteliste"),
            (nameof(vm.AreaTip), vm.AreaTip, vm.AreaChip),
            (nameof(vm.RoomFilterTip), vm.RoomFilterTip, vm.RoomFilterText),
        };
        var problems = tips.SelectMany(t => TipProblems(t.Tip, t.Label).Select(p => $"{t.Name}: {p}")).ToList();
        vm.ShowAllStaff = true;
        problems.AddRange(TipProblems(vm.AllStaffTip, vm.AllStaffText).Select(p => $"AllStaffTip (alle gezeigt): {p}"));

        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    [Fact]
    public async Task Given_Alle_zeigen_When_der_Hinweis_gelesen_wird_Then_ein_kurzer_Satz_mit_Mitarbeitende()
    {
        var vm = await OpenAsync();

        Assert.Equal("Auch Mitarbeitende ohne Dienst an diesem Standort, mit Grund", vm.AllStaffTip);
    }

    [Fact]
    public async Task Given_die_kompakte_Kopfzeile_When_der_Hinweis_gelesen_wird_Then_steht_der_Name_des_Chips_davor()
    {
        var vm = await OpenAsync();
        var changed = new List<string?>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        vm.SetViewport(1000, 800);

        Assert.Equal("Alle zeigen (1): Auch Mitarbeitende ohne Dienst an diesem Standort, mit Grund", vm.AllStaffTip);
        foreach (var tip in new[] { "AllStaffTip", "OverviewTip", "MessagesTip", "SubscriptionsTip", "WaitlistTip", "AreaTip", "RoomFilterTip" })
        {
            Assert.Contains(tip, changed);
        }
    }

    private static async Task<CalendarViewModel> OpenAsync()
    {
        var clara = new EmployeeDto(Guid.NewGuid(), "Clara", "#c27a2c", [], []);
        var resources = new LocationResources(TestData.Location, [TestData.Room], [], [TestData.Anna, TestData.Ben, clara], [TestData.Facial, TestData.MassageTreatment], []);
        var api = new StubApi()
            .On("GET /api/locations", new List<LocationDto> { TestData.Location })
            .On($"GET /api/locations/{TestData.LocationId}/resources", resources)
            .On($"GET /api/locations/{TestData.LocationId}/calendar?date=2027-03-01", TestData.Day());
        var vm = new CalendarViewModel(api.Client(), TestData.Owner(), null, new FixedTime(), null);
        await vm.InitializeAsync();
        await vm.LoadAsync();
        return vm;
    }

    /// <summary>Verstöße gegen T22: ein Satz, kein Aufzählen (höchstens zwei Kommas), die Beschriftung nicht wiederholen.</summary>
    private static IEnumerable<string> TipProblems(string tip, string? label)
    {
        var plain = Abbreviation().Replace(tip, "");
        if (plain.Split('\n', StringSplitOptions.RemoveEmptyEntries).Any(line => SentenceEnd().Count(line) > 1))
        {
            yield return $"mehr als ein Satz: „{tip}“";
        }

        if (plain.Count(c => c == ',') > 2)
        {
            yield return $"zählt auf: „{tip}“";
        }

        if (!string.IsNullOrEmpty(label) && (tip.StartsWith(label + ":", StringComparison.OrdinalIgnoreCase) || string.Equals(tip, label, StringComparison.OrdinalIgnoreCase)))
        {
            yield return $"wiederholt die Beschriftung „{label}“: „{tip}“";
        }
    }

    private static IEnumerable<string> DesktopFiles(string pattern)
    {
        var separator = Path.DirectorySeparatorChar;
        return Directory.EnumerateFiles(Path.Combine(Root, "src", "AispaStudio.Desktop"), pattern, SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{separator}obj{separator}") && !f.Contains($"{separator}bin{separator}"));
    }

    /// <summary>Sichtbare Texte: bei AXAML die festen Attributwerte, bei C# die Zeichenketten außerhalb von Kommentaren.</summary>
    private static IEnumerable<string> ShownTexts(string file)
    {
        var text = File.ReadAllText(file);
        if (file.EndsWith(".axaml", StringComparison.Ordinal))
        {
            return AttributeValue().Matches(text).Select(m => m.Groups["text"].Value);
        }

        var code = string.Join('\n', text.Split('\n').Where(l => !l.TrimStart().StartsWith("//", StringComparison.Ordinal)));
        return StringLiteral().Matches(code).Select(m => m.Groups["text"].Value);
    }

    private static string TagAround(string xaml, int index)
    {
        var start = xaml.LastIndexOf('<', index);
        var end = xaml.IndexOf('>', index);
        return xaml[start..(end + 1)];
    }

    private static string FindRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "AispaStudio.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("Wurzel des Repositorys nicht gefunden.");
    }

    [GeneratedRegex(@"ToolTip\.Tip=""(?<text>[^""{][^""]*)""")]
    private static partial Regex FixedTip();

    [GeneratedRegex(@"\b(?:Content|Text|Header)=""(?<text>[^""{][^""]*)""")]
    private static partial Regex TagLabel();

    [GeneratedRegex(@"\b[\w.:]+=""(?<text>[^""{][^""]*)""")]
    private static partial Regex AttributeValue();

    [GeneratedRegex(@"""(?<text>(?:[^""\\]|\\.)*)""")]
    private static partial Regex StringLiteral();

    [GeneratedRegex(@"\bMA\b|Personen ohne Dienst|(?<!einen )\bTherapeuten\b|\bMitarbeiter\b")]
    private static partial Regex OldTerm();

    [GeneratedRegex(@"z\. ?B\.|bzw\.|ca\.|Min\.|Std\.|Strg\+\S+|Mo\.|Di\.|Mi\.|Do\.|Fr\.|Sa\.|So\.")]
    private static partial Regex Abbreviation();

    [GeneratedRegex(@"[.!?](?=\s|$)")]
    private static partial Regex SentenceEnd();
}
