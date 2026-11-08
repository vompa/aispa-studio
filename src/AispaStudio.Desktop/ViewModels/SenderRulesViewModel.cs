using System.Collections.ObjectModel;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Controls;
using AispaStudio.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>Plattform einer Absenderregel in der Auswahl (<c>PLATFORM</c> in <c>SenderRulesSection.tsx</c>).</summary>
public sealed record PlatformOption(SenderPlatform Value, string Label)
{
    public override string ToString() => Label;
}

/// <summary>Verarbeitung einer Absenderregel in der Auswahl (<c>HANDLING</c> in <c>SenderRulesSection.tsx</c>).</summary>
public sealed record HandlingOption(SenderHandling Value, string Label)
{
    public override string ToString() => Label;
}

/// <summary>Eine Absenderregel in der Tabelle; jede Änderung an Plattform, Verarbeitung, Aktiv oder Notiz speichert die Zeile sofort.</summary>
public sealed partial class SenderRuleRow : ObservableObject
{
    private readonly Func<SenderRuleRow, SenderRuleInput, Task> _changed;
    private bool _applying;
    private int _waiting;
    private SenderRuleDto _saved;

    public SenderRuleRow(SenderRuleDto rule, Func<SenderRuleRow, SenderRuleInput, Task> changed, Func<SenderRuleRow, Task> delete)
    {
        _changed = changed;
        Id = rule.Id;
        Pattern = rule.Pattern;
        _saved = rule;
        Show(rule);
        DeleteCommand = new AsyncRelayCommand(() => delete(this));
    }

    public Guid Id { get; }

    public string Pattern { get; }

    public IAsyncRelayCommand DeleteCommand { get; }

    /// <summary>Die Regel ist gelöscht; ein noch laufender oder wartender Speichervorgang meldet nichts mehr.</summary>
    public bool IsRemoved { get; set; }

    /// <summary>Name für Screenreader zu Plattform und Verarbeitung (die Zeile nennt sonst nur den Typnamen).</summary>
    public string PlatformName => $"Plattform von {Pattern}";

    public string HandlingName => $"Verarbeitung von {Pattern}";

    public string ActiveName => $"{Pattern} aktiv";

    public string NoteName => $"Notiz zu {Pattern}";

    public string DeleteName => $"Regel für {Pattern} löschen";

    [ObservableProperty]
    public partial PlatformOption Platform { get; set; } = SenderRulesViewModel.Platforms[0];

    [ObservableProperty]
    public partial HandlingOption Handling { get; set; } = SenderRulesViewModel.Handlings[0];

    [ObservableProperty]
    public partial bool Active { get; set; }

    [ObservableProperty]
    public partial string Note { get; set; } = "";

    /// <summary>Was gespeichert würde; die Notiz leer ist „keine Angabe“.</summary>
    public SenderRuleInput Input() => new(Pattern, Platform.Value, Handling.Value, Active, NoteValue());

    /// <summary>Die Notiz wird beim Verlassen des Feldes gespeichert, wenn sie sich geändert hat (wie <c>onBlur</c> im Web).</summary>
    public Task CommitNoteAsync() => NoteValue() == _saved.Note ? Task.CompletedTask : Enqueue();

    /// <summary>Der Server hat die Zeile angenommen: der Stand wird gemerkt und nur angezeigt, wenn keine weitere Änderung dieser Zeile wartet.</summary>
    public void Saved(SenderRuleDto rule)
    {
        _saved = rule;
        if (--_waiting == 0)
        {
            Show(rule);
        }
    }

    /// <summary>Der Server hat die Zeile abgelehnt: sie zeigt wieder den gespeicherten Stand, wenn keine weitere Änderung dieser Zeile wartet.</summary>
    public void Failed()
    {
        if (--_waiting == 0)
        {
            Show(_saved);
        }
    }

    private void Show(SenderRuleDto rule)
    {
        _applying = true;
        Platform = SenderRulesViewModel.Platforms.First(p => p.Value == rule.Platform);
        Handling = SenderRulesViewModel.Handlings.First(h => h.Value == rule.Handling);
        Active = rule.Active;
        Note = rule.Note ?? "";
        _applying = false;
    }

    private Task Enqueue()
    {
        _waiting++;
        return _changed(this, Input());
    }

    private string? NoteValue() => Note.Trim().Length == 0 ? null : Note.Trim();

    partial void OnPlatformChanged(PlatformOption value) => Changed();

    partial void OnHandlingChanged(HandlingOption value) => Changed();

    partial void OnActiveChanged(bool value) => Changed();

    private void Changed()
    {
        if (!_applying)
        {
            _ = Enqueue();
        }
    }
}

/// <summary>
/// Absenderregeln des Spas (ADR 0062), Teil der Maske „Postfach &amp; Nummern“ (R05 Slice 97) nach <c>SenderRulesSection.tsx</c>: welche Absender Buchungsplattformen sind und was mit
/// ihren Mails geschieht. Die Tabelle ist eine eigene Speicherebene: Änderungen an einer Zeile gehen sofort an die API (PUT), unabhängig vom Formular des Postfachs.
/// </summary>
public sealed partial class SenderRulesViewModel : ViewModelBase
{
    private readonly IWorkstationApi _api;
    private readonly Func<ConfirmRequest, Task<ConfirmResult>> _ask;
    private Task _pending = Task.CompletedTask;

    public SenderRulesViewModel(IWorkstationApi api, Func<ConfirmRequest, Task<ConfirmResult>> ask)
    {
        _api = api;
        _ask = ask;
    }

    public static IReadOnlyList<PlatformOption> Platforms { get; } =
        [new(SenderPlatform.Treatwell, "Treatwell"), new(SenderPlatform.Other, "Andere Plattform")];

    public static IReadOnlyList<HandlingOption> Handlings { get; } =
    [
        new(SenderHandling.Automatic, "Buchung automatisch übernehmen"),
        new(SenderHandling.Suggest, "Nur als Vorschlag in den Posteingang"),
        new(SenderHandling.Ignore, "Ignorieren"),
    ];

    public ObservableCollection<SenderRuleRow> Rules { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowEmpty), nameof(HasRules))]
    public partial bool IsLoaded { get; private set; }

    public bool HasRules => Rules.Count > 0;

    public bool ShowEmpty => IsLoaded && Rules.Count == 0;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddCommand))]
    public partial string NewPattern { get; set; } = "";

    [ObservableProperty]
    public partial PlatformOption NewPlatform { get; set; } = Platforms[0];

    [ObservableProperty]
    public partial HandlingOption NewHandling { get; set; } = Handlings[0];

    [ObservableProperty]
    public partial string NewNote { get; set; } = "";

    /// <summary>Rückmeldung zu Anlegen, Ändern und Löschen oder der Fehler dabei.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMessage))]
    public partial string? Message { get; private set; }

    [ObservableProperty]
    public partial InfoBarSeverity MessageSeverity { get; private set; } = InfoBarSeverity.Success;

    public bool HasMessage => Message is not null;

    /// <summary>Wartet, bis alle gestarteten Speichervorgänge der Zeilen fertig sind.</summary>
    public Task IdleAsync() => _pending;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLoadError))]
    public partial string? LoadError { get; private set; }

    public bool HasLoadError => LoadError is not null;

    [RelayCommand]
    public async Task LoadAsync()
    {
        var result = await _api.GetAsync<List<SenderRuleDto>>("api/intake/sender-rules").ConfigureAwait(true);
        if (result.Value is not { } rules)
        {
            LoadError = result.Error ?? "Die Absenderregeln wurden nicht geladen.";
            return;
        }

        LoadError = null;
        Rules.Clear();
        foreach (var rule in rules)
        {
            Rules.Add(NewRow(rule));
        }

        IsLoaded = true;
        Refresh();
    }

    private bool CanAdd => NewPattern.Trim().Length > 0;

    [RelayCommand(CanExecute = nameof(CanAdd))]
    private async Task AddAsync()
    {
        var input = new SenderRuleInput(NewPattern.Trim(), NewPlatform.Value, NewHandling.Value, true, Trimmed(NewNote));
        var result = await _api.PostAsync<SenderRuleDto>("api/intake/sender-rules", input).ConfigureAwait(true);
        if (result.Value is not { } created)
        {
            Notify(result.Error ?? "Die Regel wurde nicht angelegt.", InfoBarSeverity.Error);
            return;
        }

        var rows = Rules.Append(NewRow(created)).OrderBy(r => r.Platform.Value.ToString(), StringComparer.Ordinal).ThenBy(r => r.Pattern, StringComparer.Ordinal).ToList();
        Rules.Clear();
        foreach (var row in rows)
        {
            Rules.Add(row);
        }

        NewPattern = "";
        NewNote = "";
        Notify($"Regel für {created.Pattern} angelegt.", InfoBarSeverity.Success);
        Refresh();
    }

    private SenderRuleRow NewRow(SenderRuleDto rule) => new(rule, (row, input) => Enqueue(() => SaveRowAsync(row, input)), DeleteAsync);

    /// <summary>Die PUT-Aufrufe der Zeilen laufen nacheinander; jeder schickt den Stand der Zeile zu seiner Zeit.</summary>
    private Task Enqueue(Func<Task> work)
    {
        async Task Run(Task previous)
        {
            await previous.ConfigureAwait(true);
            await work().ConfigureAwait(true);
        }

        _pending = Run(_pending);
        return _pending;
    }

    private async Task SaveRowAsync(SenderRuleRow row, SenderRuleInput input)
    {
        if (row.IsRemoved)
        {
            row.Failed();
            return;
        }

        var result = await _api.PutAsync<SenderRuleDto>($"api/intake/sender-rules/{row.Id}", input).ConfigureAwait(true);
        if (result.Value is { } saved)
        {
            row.Saved(saved);
            Message = null;
            return;
        }

        row.Failed();
        if (row.IsRemoved)
        {
            return;
        }

        Notify(result.Error ?? "Die Regel wurde nicht gespeichert.", InfoBarSeverity.Error);
    }

    private async Task DeleteAsync(SenderRuleRow row)
    {
        var answer = await _ask(new ConfirmRequest("Regel löschen?",
            $"Mails von {row.Pattern} bearbeitet die Terminannahme danach wieder wie normale Anfragen.", "Löschen")).ConfigureAwait(true);
        if (!answer.IsConfirmed)
        {
            return;
        }

        var result = await _api.DeleteAsync<object>($"api/intake/sender-rules/{row.Id}").ConfigureAwait(true);
        if (!result.Ok)
        {
            Notify(result.Error ?? "Die Regel wurde nicht gelöscht.", InfoBarSeverity.Error);
            return;
        }

        row.IsRemoved = true;
        Rules.Remove(row);
        Notify($"Regel für {row.Pattern} gelöscht.", InfoBarSeverity.Success);
        Refresh();
    }

    private void Notify(string text, InfoBarSeverity severity)
    {
        Message = text;
        MessageSeverity = severity;
    }

    private void Refresh()
    {
        OnPropertyChanged(nameof(HasRules));
        OnPropertyChanged(nameof(ShowEmpty));
    }

    private static string? Trimmed(string text) => text.Trim().Length == 0 ? null : text.Trim();

    [RelayCommand]
    private void CloseMessage() => Message = null;
}
