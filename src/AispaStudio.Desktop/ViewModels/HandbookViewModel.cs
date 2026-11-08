using System.Collections.ObjectModel;
using System.ComponentModel;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>Ein Eintrag in der Liste des Handbuchs; „★“ heißt: erinnert am Tagesstart, „(aus)“: gilt nicht mehr.</summary>
public sealed record HandbookRow(Guid Id, string Name)
{
    public override string ToString() => Name;
}

/// <summary>Ein Wochentag zum Ankreuzen („nur an diesen Tagen“).</summary>
public sealed partial class DayChoice(DayOfWeek day, string label) : ObservableObject
{
    public DayOfWeek Day { get; } = day;

    public string Label { get; } = label;

    /// <summary>Feldkennung für Tests und Fokus, z. B. „DayMonday“.</summary>
    public string FieldName => $"Day{Day}";

    [ObservableProperty]
    public partial bool IsSelected { get; set; }
}

/// <summary>
/// Rezeptionshandbuch des Spas (R05 Slice 97), native Maske nach <c>HandbookTab.tsx</c>: das Wissen des Hauses (z. B. „Gäste mit Hund“); wichtige Einträge erinnern am Tagesstart
/// (Kalender, Tagesbetrieb). Links die Liste, rechts das Formular mit Titel, Bereich, Text, „Wichtig – am Tagesstart erinnern“, Wochentagen und „Gilt“. Wer einen anderen Eintrag
/// wählt, wird bei ungespeicherten Änderungen gefragt.
/// </summary>
public sealed partial class HandbookViewModel : ViewModelBase, INativeArea
{
    public const int MaxTitle = 200;

    public const int MaxArea = 100;

    public const int MaxText = 4000;

    public const string TitleField = "EntryTitleField";

    public const string TextField = "TextField";

    private static readonly string[] Inputs = [nameof(EntryTitle), nameof(Area), nameof(Text), nameof(RemindAtDayStart), nameof(IsActive)];

    private readonly IWorkstationApi _api;
    private readonly SynchronizationContext? _ui = SynchronizationContext.Current;
    private List<HandbookEntryDto> _entries = [];
    private HandbookEntryDto? _current;
    private string? _savedSignature;
    private bool _applying;
    private bool _switching;
    private bool _reverting;

    public HandbookViewModel(IWorkstationApi api)
    {
        _api = api;
        foreach (var day in Days)
        {
            day.PropertyChanged += (_, _) => Touch();
        }
    }

    public string Title => "Handbuch";

    public ObservableCollection<HandbookRow> Entries { get; } = [];

    public IReadOnlyList<DayChoice> Days { get; } =
    [
        new(DayOfWeek.Monday, "Montag"), new(DayOfWeek.Tuesday, "Dienstag"), new(DayOfWeek.Wednesday, "Mittwoch"), new(DayOfWeek.Thursday, "Donnerstag"),
        new(DayOfWeek.Friday, "Freitag"), new(DayOfWeek.Saturday, "Samstag"), new(DayOfWeek.Sunday, "Sonntag"),
    ];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowEmpty))]
    public partial bool IsLoaded { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLoadError))]
    public partial string? LoadError { get; private set; }

    public bool HasLoadError => LoadError is not null;

    /// <summary>Der gewählte Eintrag der Liste; ohne Auswahl steht das Formular für einen neuen Eintrag.</summary>
    [ObservableProperty]
    public partial HandbookRow? SelectedRow { get; set; }

    public bool IsCreating => _current is null;

    public string HeadingText => _current?.Title ?? "Neuer Eintrag";

    public bool CanDelete => !IsCreating;

    public bool ShowEmpty => IsLoaded && Entries.Count == 0;

    [ObservableProperty]
    public partial string EntryTitle { get; set; } = "";

    [ObservableProperty]
    public partial string Area { get; set; } = "";

    [ObservableProperty]
    public partial string Text { get; set; } = "";

    [ObservableProperty]
    public partial bool RemindAtDayStart { get; set; }

    [ObservableProperty]
    public partial bool IsActive { get; set; } = true;

    [ObservableProperty]
    public partial string? TitleError { get; private set; }

    [ObservableProperty]
    public partial string? TextError { get; private set; }

    /// <summary>Das Feld, das nach einem Fehler den Fokus bekommen soll; die Ansicht setzt ihn und leert die Angabe.</summary>
    [ObservableProperty]
    public partial string? FocusTarget { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? Error { get; private set; }

    public bool HasError => Error is not null;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    public partial bool IsSaving { get; private set; }

    /// <summary>„Gespeichert.“ in der Leiste, bis wieder etwas geändert wird.</summary>
    [ObservableProperty]
    public partial string? StatusText { get; private set; }

    /// <summary>Rückmeldung „Gelöscht.“ über dem Formular.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMessage))]
    public partial string? Message { get; private set; }

    public bool HasMessage => Message is not null;

    public bool HasUnsavedChanges => _savedSignature is not null && Signature() != _savedSignature;

    [RelayCommand]
    public async Task LoadAsync()
    {
        LoadError = null;
        if (!await ReloadAsync(_current?.Id).ConfigureAwait(true))
        {
            return;
        }

        IsLoaded = true;
    }

    public Task EnsureLoadedAsync() => IsLoaded ? Task.CompletedTask : LoadAsync();

    private bool CanSave => !IsSaving;

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync() => await SaveChangesAsync().ConfigureAwait(true);

    public async Task<bool> SaveChangesAsync()
    {
        if (IsSaving)
        {
            return false;
        }

        TitleError = EntryTitle.Trim().Length == 0 ? "Der Titel fehlt. Trage ihn ein." : null;
        TextError = Text.Trim().Length == 0 ? "Der Text fehlt. Trage ihn ein." : null;
        if (TitleError is not null || TextError is not null)
        {
            FocusTarget = TitleError is not null ? TitleField : TextField;
            return false;
        }

        Error = null;
        IsSaving = true;
        var body = new HandbookEntryInput(EntryTitle.Trim(), Text.Trim(), Area.Trim().Length == 0 ? null : Area.Trim(), RemindAtDayStart,
            [.. Days.Where(d => d.IsSelected).Select(d => d.Day)], IsActive);
        var result = _current is { } entry
            ? await _api.PutAsync<HandbookEntryDto>($"api/handbook/{entry.Id}", body).ConfigureAwait(true)
            : await _api.PostAsync<HandbookEntryDto>("api/handbook", body).ConfigureAwait(true);
        IsSaving = false;
        if (result.Value is not { } saved)
        {
            Error = result.Error ?? "Der Eintrag wurde nicht gespeichert.";
            return false;
        }

        Message = null;
        await ReloadAsync(saved.Id).ConfigureAwait(true);
        StatusText = "Gespeichert.";
        return true;
    }

    [RelayCommand]
    public void Discard()
    {
        Apply(_current);
        Error = null;
    }

    /// <summary>Ein neuer Eintrag; vorher fragt die Maske, wenn etwas ungespeichert ist.</summary>
    [RelayCommand]
    private async Task NewAsync()
    {
        if (!await UnsavedChanges.ConfirmLeaveAsync(Dialogs, this).ConfigureAwait(true))
        {
            return;
        }

        Message = null;
        Select(null);
    }

    [RelayCommand(CanExecute = nameof(CanDelete))]
    private async Task DeleteAsync()
    {
        if (_current is not { } entry)
        {
            return;
        }

        var answer = await AskAsync(new ConfirmRequest("Eintrag löschen?",
            $"Der Eintrag „{entry.Title}“ steht danach nicht mehr im Handbuch und erinnert nicht mehr am Tagesstart.", "Löschen")).ConfigureAwait(true);
        if (!answer.IsConfirmed)
        {
            return;
        }

        var result = await _api.DeleteAsync<object>($"api/handbook/{entry.Id}").ConfigureAwait(true);
        if (!result.Ok)
        {
            Error = result.Error ?? "Der Eintrag wurde nicht gelöscht.";
            return;
        }

        Error = null;
        await ReloadAsync(null).ConfigureAwait(true);
        Message = "Gelöscht.";
    }

    [RelayCommand]
    private void CloseError() => Error = null;

    [RelayCommand]
    private void CloseMessage() => Message = null;

    public void Open(Guid? id, string? section, string? field)
    {
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.PropertyName is { } name && Inputs.Contains(name))
        {
            if (name == nameof(EntryTitle))
            {
                TitleError = null;
            }
            else if (name == nameof(Text))
            {
                TextError = null;
            }

            Touch();
        }
    }

    partial void OnSelectedRowChanged(HandbookRow? oldValue, HandbookRow? newValue)
    {
        if (_reverting || _switching || newValue is null)
        {
            return;
        }

        if (HasUnsavedChanges)
        {
            // Erst fragen, dann wechseln: die Liste bleibt beim alten Eintrag, bis die Antwort da ist.
            Revert(() => SelectedRow = oldValue);
            _ = LeaveToRowAsync(newValue.Id);
            return;
        }

        Select(newValue.Id);
    }

    private async Task LeaveToRowAsync(Guid id)
    {
        if (await UnsavedChanges.ConfirmLeaveAsync(Dialogs, this).ConfigureAwait(true))
        {
            Select(id);
        }
    }

    /// <summary>
    /// Zurück zur alten Auswahl. Kam der Wechsel aus der Liste, übernimmt sie einen Wert, den das Modell noch während ihrer eigenen Auswahl zurücksetzt, nicht; deshalb erst danach
    /// (gepostet), dann sieht sie eine echte Änderung.
    /// </summary>
    private void Revert(Action restore)
    {
        void Restore()
        {
            _reverting = true;
            try
            {
                restore();
            }
            finally
            {
                _reverting = false;
            }
        }

        if (_ui is null)
        {
            Restore();
        }
        else
        {
            _ui.Post(_ => Restore(), null);
        }
    }

    /// <summary>Wählt einen Eintrag (oder, mit <c>null</c>, den neuen) in Liste und Formular.</summary>
    private void Select(Guid? id)
    {
        _switching = true;
        SelectedRow = id is { } wanted ? Entries.FirstOrDefault(e => e.Id == wanted) : null;
        _switching = false;
        Apply(id is { } known ? _entries.FirstOrDefault(e => e.Id == known) : null);
    }

    /// <summary>Lädt die Liste neu und wählt danach den Eintrag mit der Kennung (sonst das Formular für einen neuen).</summary>
    private async Task<bool> ReloadAsync(Guid? select)
    {
        var result = await _api.GetAsync<List<HandbookEntryDto>>("api/handbook").ConfigureAwait(true);
        if (result.Value is not { } entries)
        {
            LoadError = result.Error ?? "Das Handbuch wurde nicht geladen.";
            return false;
        }

        LoadError = null;
        _entries = entries;
        _switching = true;
        Entries.Clear();
        foreach (var entry in entries)
        {
            Entries.Add(new HandbookRow(entry.Id, NameOf(entry)));
        }

        _switching = false;
        OnPropertyChanged(nameof(ShowEmpty));
        Select(select);
        return true;
    }

    private static string NameOf(HandbookEntryDto entry) => $"{entry.Title}{(entry.RemindAtDayStart ? " ★" : "")}{(entry.IsActive ? "" : " (aus)")}";

    private void Apply(HandbookEntryDto? entry)
    {
        _applying = true;
        _current = entry;
        EntryTitle = entry?.Title ?? "";
        Area = entry?.Area ?? "";
        Text = entry?.Text ?? "";
        RemindAtDayStart = entry?.RemindAtDayStart ?? false;
        IsActive = entry?.IsActive ?? true;
        foreach (var day in Days)
        {
            day.IsSelected = entry?.Days.Contains(day.Day) ?? false;
        }

        _applying = false;
        _savedSignature = Signature();
        TitleError = null;
        TextError = null;
        StatusText = null;
        foreach (var name in new[] { nameof(IsCreating), nameof(HeadingText), nameof(CanDelete), nameof(HasUnsavedChanges) })
        {
            OnPropertyChanged(name);
        }

        DeleteCommand.NotifyCanExecuteChanged();
    }

    /// <summary>Eine Eingabe hat sich geändert: „Gespeichert.“ gilt nicht mehr, und „Verwerfen“ wird bedienbar.</summary>
    private void Touch()
    {
        if (_applying)
        {
            return;
        }

        StatusText = null;
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    private string Signature() => string.Join('\u001f', EntryTitle, Area, Text, RemindAtDayStart, IsActive, string.Join(',', Days.Where(d => d.IsSelected).Select(d => d.Day)));
}
