using AispaStudio.Contracts;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>
/// Zeile einer Listen-Antwort (Ä316) wie „Heute abzurechnen“: links Uhrzeit oder Tag, daneben Gast oder Person, darunter Behandlung oder Dienst. Die ganze Zeile ist der
/// Sprung; ohne Ziel (z. B. freie Zeiten ohne Buchungsrecht) ist sie nur Text.
/// </summary>
public sealed record CompanionRow(AssistantItem Item)
{
    public string Title => Item.Title;

    public string? Detail => Item.Detail;

    public string? Lead => Item.Lead;

    public bool HasDetail => !string.IsNullOrEmpty(Item.Detail);

    public bool HasLead => !string.IsNullOrEmpty(Item.Lead);

    public bool CanOpen => Item.Suggestion is not null || Item.Link is not null;

    /// <summary>Was ein Klick tut („Im Kalender zeigen“); Tooltip der Zeile.</summary>
    public string? Action => CanOpen ? Item.Action : null;

    /// <summary>Name für den Bildschirmleser: alles, was die Zeile zeigt, und was ein Klick tut.</summary>
    public string AccessibleName =>
        string.Join(", ", new[] { Item.Lead, Item.Title, Item.Detail }.Where(v => !string.IsNullOrEmpty(v))) + (CanOpen ? $". {Item.Action}" : "");
}

/// <summary>
/// Listen-Antworten (Ä316): oben der Kopfsatz mit der Zahl, darunter eine einzige Liste. Bis zehn Zeilen stehen alle da, darüber „Alle zeigen“, das an Ort und Stelle aufklappt.
/// </summary>
public sealed partial class CompanionViewModel
{
    /// <summary>So viele Zeilen zeigt die Leiste, bevor „Alle zeigen“ aufklappt.</summary>
    public const int VisibleRows = 10;

    private IReadOnlyList<AssistantItem> Items => Block?.Items ?? [];

    /// <summary>„Alle zeigen“ wurde gewählt; gilt bis zur nächsten Antwort.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Rows), nameof(HasMoreRows))]
    public partial bool ShowsAllRows { get; private set; }

    public IReadOnlyList<CompanionRow> Rows => [.. (ShowsAllRows ? Items : Items.Take(VisibleRows)).Select(i => new CompanionRow(i))];

    public bool HasRows => Items.Count > 0;

    public bool HasMoreRows => !ShowsAllRows && Items.Count > VisibleRows;

    public string ShowAllText => $"Alle zeigen ({Items.Count})";

    [RelayCommand]
    private void ShowAllRows() => ShowsAllRows = true;

    /// <summary>Klick auf eine Zeile: Wochenplan über den Verweis, sonst wie der Vorschlag (Termin im Kalender, Kundenkarte, Buchen).</summary>
    [RelayCommand]
    private Task OpenRowAsync(CompanionRow row)
    {
        if (row.Item.Link is { } link)
        {
            Open(link);
            return Task.CompletedTask;
        }

        return row.Item.Suggestion is { } suggestion ? UseSuggestionAsync(new CompanionSuggestion(suggestion, row.Item.Action)) : Task.CompletedTask;
    }

    /// <summary>Pfeil nach oben im leeren Feld: die zuletzt gestellte Frage von heute kommt zurück ins Feld. Gibt zurück, ob die Taste damit erledigt ist.</summary>
    public bool RecallQuestion()
    {
        // Nach Ruhezustand über Mitternacht kann der Zeitgeber noch schlafen: nie eine Frage von gestern zurückholen.
        ExpireHistory();
        if (Question.Length > 0 || RecentQuestions.FirstOrDefault() is not { } last)
        {
            return false;
        }

        Question = last;
        return true;
    }
}
