using System.Text.RegularExpressions;

namespace AispaStudio.Desktop.Tests;

/// <summary>Quelltext-Scans für die Architekturregeln der Windows-App (<see cref="ClientArchitectureTests"/>); reine Funktionen über Text.</summary>
internal static partial class ArchitectureScan
{
    private static readonly string RepoRoot = FindRepoRoot();

    // Einzige Ausnahme von der Result-Regel: Eigenschaft Result des Ergebnisses einer Abo-Aktualisierung, kein Task.
    private const string DtoResultFile = "SubscriptionsViewModel.cs";
    private const string DtoResultReceiver = "imported";

    /// <summary>Verweise (Paket oder Projekt) auf EF Core, Npgsql oder die API in einer Projektdatei.</summary>
    public static IReadOnlyList<string> ForbiddenReferences(string projectText) =>
        [.. Reference().Matches(projectText).Select(m => m.Groups[1].Value).Where(IsForbiddenReference)];

    /// <summary>Zeilen <c>using Microsoft.EntityFrameworkCore…</c> oder <c>using Npgsql…</c> als „Datei:Zeile: Text“.</summary>
    public static IReadOnlyList<string> ForbiddenUsings(string path, string sourceText) =>
        [.. sourceText.Split('\n')
            .Select((line, index) => (Text: line.Trim(), Line: index + 1))
            .Where(l => ForbiddenUsing().IsMatch(l.Text))
            .Select(l => $"{path}:{l.Line}: {l.Text}")];

    /// <summary>Blockierendes Warten auf Tasks als „Datei:Zeile: Text“; <c>.Result</c> ist nur auf Tasks erlaubt, die <c>Task.WhenAll</c> abgewartet hat.</summary>
    public static IReadOnlyList<string> BlockingCalls(string path, string sourceText)
    {
        var text = sourceText.Replace("\r\n", "\n", StringComparison.Ordinal);
        var awaited = AwaitedTasks(text);
        var found = new List<(int Index, string Text)>();
        found.AddRange(Blocking().Matches(text).Select(m => (m.Index, m.Value)));
        foreach (Match match in ResultAccess().Matches(text))
        {
            var receiver = match.Groups[1].Value;
            var isTypeName = receiver.Length > 0 && char.IsUpper(receiver[0]) && TypeUse().IsMatch(text[(match.Index + match.Length)..]);
            var isKnownProperty = path.EndsWith(DtoResultFile, StringComparison.Ordinal) && receiver == DtoResultReceiver;
            if (!isTypeName && !isKnownProperty && !awaited.Any(a => a.Name == receiver && a.End <= match.Index))
            {
                found.Add((match.Index, match.Value));
            }
        }

        return [.. found.OrderBy(f => f.Index).Select(f => $"{path}:{LineOf(text, f.Index)}: {f.Text.Trim()}")];
    }

    private static bool IsForbiddenReference(string include) =>
        include.Contains("EntityFramework", StringComparison.Ordinal)
        || include.Contains("Npgsql", StringComparison.Ordinal)
        || include.Replace('\\', '/').EndsWith("/AispaStudio.Api.csproj", StringComparison.Ordinal)
        || include == "AispaStudio.Api";

    // Namen der Tasks aus „await Task.WhenAll(a, b)“ mit der Stelle, ab der sie fertig sind (Ende der Anweisung).
    private static List<(string Name, int End)> AwaitedTasks(string text) =>
        [.. WhenAll().Matches(text).SelectMany(m => m.Groups[1].Value.Split(',')
            .Select(name => name.Trim()).Where(name => PlainName().IsMatch(name)).Select(name => (name, m.Index + m.Length)))];

    private static int LineOf(string text, int index) => text.AsSpan(0, index).Count('\n') + 1;

    /// <summary>Die Projektdatei der Windows-App und alle Projekte, auf die sie (auch mittelbar) verweist.</summary>
    public static IReadOnlyList<string> DesktopProjectFiles()
    {
        var pending = new Stack<string>();
        pending.Push(Path.Combine(RepoRoot, "src", "AispaStudio.Desktop", "AispaStudio.Desktop.csproj"));
        var seen = new List<string>();
        while (pending.Count > 0)
        {
            var project = pending.Pop();
            if (seen.Contains(project, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            seen.Add(project);
            foreach (Match reference in ProjectReference().Matches(File.ReadAllText(project)))
            {
                var relative = reference.Groups[1].Value.Replace('\\', Path.DirectorySeparatorChar);
                pending.Push(Path.GetFullPath(Path.Combine(Path.GetDirectoryName(project)!, relative)));
            }
        }

        return seen;
    }

    public static IEnumerable<string> DesktopSourceFiles() => SourceFiles(Path.Combine(RepoRoot, "src", "AispaStudio.Desktop"));

    public static IEnumerable<string> ViewModelFiles() => SourceFiles(Path.Combine(RepoRoot, "src", "AispaStudio.Desktop", "ViewModels"));

    private static IEnumerable<string> SourceFiles(string folder) =>
        Directory.EnumerateFiles(folder, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal));

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AispaStudio.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("AispaStudio.slnx nicht gefunden.");
    }

    [GeneratedRegex("<ProjectReference\\s+Include=\"([^\"]+)\"")]
    private static partial Regex ProjectReference();

    [GeneratedRegex("<(?:Package|Project)Reference\\s+Include=\"([^\"]+)\"")]
    private static partial Regex Reference();

    [GeneratedRegex(@"^using\s+(?:Microsoft\.EntityFrameworkCore|Npgsql)\b")]
    private static partial Regex ForbiddenUsing();

    [GeneratedRegex(@"\.Wait(?:All|Any)?\(|\.GetAwaiter\(\)\s*\.GetResult\(\)")]
    private static partial Regex Blocking();

    [GeneratedRegex(@"(\w+|\)|\])[?!]?\.Result\b")]
    private static partial Regex ResultAccess();

    [GeneratedRegex(@"await\s+Task\.WhenAll\(([^)]*)\)")]
    private static partial Regex WhenAll();

    // Typname statt Task: auf „.Result“ folgt ein Bezeichner oder „?“/„>“ (DayLayout.Result Build, DayLayout.Result? x).
    [GeneratedRegex(@"^(?:\?\s|>|\s+[A-Za-z_])")]
    private static partial Regex TypeUse();

    [GeneratedRegex(@"^\w+$")]
    private static partial Regex PlainName();
}
