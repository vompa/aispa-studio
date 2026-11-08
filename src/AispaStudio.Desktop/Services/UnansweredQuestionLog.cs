using System.Text.Json;

namespace AispaStudio.Desktop.Services;

/// <summary>
/// Eine Frage, auf die der Assistent nichts gefunden hat (Slice 56): der vom Server bereinigte Text (ohne Kundennamen, E-Mail, Telefon), die Seite,
/// zuletzt gefragt und wie oft. Nie die Person, der Standort oder die Antwort.
/// </summary>
public sealed record UnansweredQuestion(string Question, string? Page, DateTimeOffset At, int Count = 1);

/// <summary>Wohin offene Fragen an die Hilfe gehen. Gleiche Frage auf derselben Seite zählt hoch; nach <see cref="KeepDays"/> Tagen fällt sie weg.</summary>
public interface IUnansweredQuestionStore
{
    const int KeepDays = 90;

    void Record(UnansweredQuestion entry);

    /// <summary>Die Fragen der letzten 90 Tage, die neueste zuerst.</summary>
    IReadOnlyList<UnansweredQuestion> List(DateTimeOffset now);

    /// <summary>„Erledigt“: der Eintrag wird gelöscht.</summary>
    void Remove(UnansweredQuestion entry);
}

/// <summary>Zusammenfassen, Vergessen und Ordnen, gleich für Speicher und Datei.</summary>
internal static class UnansweredQuestions
{
    public static bool Same(UnansweredQuestion a, UnansweredQuestion b) =>
        a.Page == b.Page && string.Equals(a.Question, b.Question, StringComparison.OrdinalIgnoreCase);

    public static List<UnansweredQuestion> Add(IEnumerable<UnansweredQuestion> entries, UnansweredQuestion entry)
    {
        var list = entries.ToList();
        var known = list.FirstOrDefault(e => Same(e, entry));
        if (known is not null)
        {
            list.Remove(known);
        }

        list.Add(entry with { Count = (known?.Count ?? 0) + 1 });
        return list;
    }

    public static List<UnansweredQuestion> Fresh(IEnumerable<UnansweredQuestion> entries, DateTimeOffset now) =>
        [.. entries.Where(e => now - e.At <= TimeSpan.FromDays(IUnansweredQuestionStore.KeepDays)).OrderByDescending(e => e.At)];
}

/// <summary>Nur im Speicher (Tests).</summary>
public sealed class MemoryUnansweredQuestionStore : IUnansweredQuestionStore
{
    private List<UnansweredQuestion> _entries = [];

    public void Record(UnansweredQuestion entry) => _entries = UnansweredQuestions.Add(_entries, entry);

    public IReadOnlyList<UnansweredQuestion> List(DateTimeOffset now) => UnansweredQuestions.Fresh(_entries, now);

    public void Remove(UnansweredQuestion entry) => _entries.RemoveAll(e => UnansweredQuestions.Same(e, entry));
}

/// <summary>
/// Eine JSON-Zeile je Frage im Benutzerprofil (<c>unanswered-questions.jsonl</c>), neben den KI-Rückmeldungen. Die Datei wird bei jeder Änderung
/// und beim Lesen mit abgelaufenen Einträgen neu geschrieben, ohne diese; ein Lese- oder Schreibfehler geht nicht in die Bedienung ein.
/// </summary>
public sealed class FileUnansweredQuestionStore(string path) : IUnansweredQuestionStore
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public void Record(UnansweredQuestion entry) => Save(UnansweredQuestions.Fresh(UnansweredQuestions.Add(Load(), entry), entry.At));

    /// <summary>Liest die Fragen; abgelaufene löscht es dabei auch aus der Datei.</summary>
    public IReadOnlyList<UnansweredQuestion> List(DateTimeOffset now)
    {
        var all = Load();
        var fresh = UnansweredQuestions.Fresh(all, now);
        if (fresh.Count < all.Count)
        {
            Save(fresh);
        }

        return fresh;
    }

    public void Remove(UnansweredQuestion entry) => Save([.. Load().Where(e => !UnansweredQuestions.Same(e, entry))]);

    private List<UnansweredQuestion> Load()
    {
        try
        {
            return File.Exists(path) ? [.. File.ReadAllLines(path).Select(Parse).OfType<UnansweredQuestion>()] : [];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Nicht lesbar: die Liste bleibt leer, die Arbeit geht weiter.
            return [];
        }
    }

    /// <summary>Eine Zeile; eine kaputte (etwa nach einem Absturz beim Schreiben) fällt allein weg, die übrigen bleiben.</summary>
    private static UnansweredQuestion? Parse(string line)
    {
        try
        {
            return line.Length > 0 && JsonSerializer.Deserialize<UnansweredQuestion>(line, Json) is { Question: not null } entry ? entry : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private void Save(IEnumerable<UnansweredQuestion> entries)
    {
        try
        {
            // Erst in eine zweite Datei schreiben und dann tauschen: ein Absturz mittendrin lässt die alte Datei ganz.
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var next = path + ".tmp";
            File.WriteAllLines(next, entries.Select(e => JsonSerializer.Serialize(e, Json)));
            File.Move(next, path, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Nicht gespeichert: die Frage ist verloren, die Arbeit geht weiter.
        }
    }
}

/// <summary>Die Ablage der offenen Fragen dieser App; Tests setzen eine eigene.</summary>
public static class UnansweredQuestionLog
{
    public static IUnansweredQuestionStore Current { get; set; } = new FileUnansweredQuestionStore(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AispaStudio", "unanswered-questions.jsonl"));
}
