using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>Zeile der Behandlungsliste: eine Behandlung, eine Kategorie als Überschrift oder „Behandlung anlegen“ (<c>Id</c> leer).</summary>
public sealed record TreatmentEntry(string Title, Guid? Id, bool IsHeading = false)
{
    public bool IsNew => Id is null && !IsHeading;

    public override string ToString() => Title;
}

/// <summary>
/// Bereich „Behandlungen“ der Verwaltung (R05 Slice 94, Ä191): links die Behandlungen nach Kategorie wie im Web (<c>TreatmentsTab.tsx</c>: Kategorien
/// alphabetisch, „Ohne Kategorie“ zuletzt) mit „Behandlung anlegen“, rechts die gewählte Behandlung (<see cref="TreatmentEditorViewModel"/>). Wer mit
/// ungespeicherten Änderungen eine andere Behandlung wählt, wird gefragt wie beim Verlassen der Maske (<see cref="UnsavedChanges"/>).
/// </summary>
public sealed partial class TreatmentsViewModel : ViewModelBase, INativeArea
{
    private const string WithoutCategory = "Ohne Kategorie";

    private const string NotOffered = "Diese Behandlung wird nicht mehr angeboten und steht deshalb nicht in der Liste.";

    private readonly IWorkstationApi _api;
    private readonly TimeZoneInfo _zone;
    private readonly bool _showsMaterial;
    private readonly SynchronizationContext? _ui = SynchronizationContext.Current;
    private TreatmentCatalog? _catalog;
    private List<TreatmentDto> _treatments = [];
    private bool _reverting;

    /// <summary>Sprungziel (Kalender, Assistent), das vor dem Laden oder vor dem Wechsel der Behandlung kam; gilt, sobald sie offen ist.</summary>
    private (Guid? Id, string? Section, string? Field)? _pending;

    /// <param name="showsMaterial">Recht auf Berichte mit Beträgen: nur dann gibt es den Abschnitt „Material“ (Kosten je Behandlung).</param>
    public TreatmentsViewModel(IWorkstationApi api, TimeZoneInfo zone, bool showsMaterial = true)
    {
        _api = api;
        _zone = zone;
        _showsMaterial = showsMaterial;
    }

    /// <summary>Name der Maske in der Rückfrage: die offene Behandlung.</summary>
    public string Title => Editor?.Title ?? "Behandlungen";

    [ObservableProperty]
    public partial IReadOnlyList<TreatmentEntry> Entries { get; private set; } = [];

    [ObservableProperty]
    public partial TreatmentEntry? SelectedEntry { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Title))]
    public partial TreatmentEditorViewModel? Editor { get; private set; }

    [ObservableProperty]
    public partial bool IsLoaded { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLoadError))]
    public partial string? LoadError { get; private set; }

    public bool HasLoadError => LoadError is not null;

    /// <summary>Meldung zur Liste über der Fußleiste: ein Sprungziel, das nicht mehr angeboten wird, oder ein gescheitertes „Nicht mehr anbieten“.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNotice))]
    public partial string? Notice { get; private set; }

    public bool HasNotice => Notice is not null;

    [RelayCommand]
    private void CloseNotice() => Notice = null;

    /// <summary>Eine Behandlung wurde gespeichert; die Leiste „KI und Hilfe“ bietet dann an, eine Frage zu wiederholen.</summary>
    public event Action? Saved;

    /// <summary>„Wer kann das?“: eine Person im Bereich Team öffnen.</summary>
    public event Action<Guid>? PersonRequested;

    public bool HasUnsavedChanges => Editor?.HasUnsavedChanges == true;

    // Für die Fußleiste: der Stand der offenen Behandlung, damit die Leiste ohne Bindung durch eine leere Maske auskommt.
    public string? StatusText => Editor?.StatusText;

    public string? Error => Editor?.Error;

    public bool HasError => Editor?.HasError == true;

    /// <summary>„Nicht mehr anbieten“ gibt es nur für eine gespeicherte Behandlung.</summary>
    public bool CanDeactivate => Editor is { IsNew: false };

    partial void OnEditorChanged(TreatmentEditorViewModel? oldValue, TreatmentEditorViewModel? newValue)
    {
        if (oldValue is not null)
        {
            oldValue.PropertyChanged -= OnEditorPropertyChanged;
        }

        if (newValue is not null)
        {
            newValue.PropertyChanged += OnEditorPropertyChanged;
        }

        RaiseFooter();
    }

    private void OnEditorPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(TreatmentEditorViewModel.HasUnsavedChanges) or nameof(TreatmentEditorViewModel.StatusText)
            or nameof(TreatmentEditorViewModel.Error) or nameof(TreatmentEditorViewModel.IsNew)
            or nameof(TreatmentEditorViewModel.Title))
        {
            RaiseFooter();
        }
    }

    private void RaiseFooter()
    {
        OnPropertyChanged(nameof(HasUnsavedChanges));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(Error));
        OnPropertyChanged(nameof(HasError));
        OnPropertyChanged(nameof(CanDeactivate));
        OnPropertyChanged(nameof(Title));
    }

    [RelayCommand]
    private void CloseError() => Editor?.CloseErrorCommand.Execute(null);

    /// <summary>Lädt Behandlungen und alles, was die Maske zum Auswählen braucht, beim ersten Öffnen und über „Erneut laden“.</summary>
    [RelayCommand]
    public async Task LoadAsync()
    {
        LoadError = null;
        var treatments = await _api.GetAsync<List<TreatmentDto>>("api/treatments").ConfigureAwait(true);
        var qualifications = await _api.GetAsync<List<QualificationDto>>("api/qualifications").ConfigureAwait(true);
        var locations = await _api.GetAsync<List<LocationDto>>("api/locations").ConfigureAwait(true);
        var employees = await _api.GetAsync<List<EmployeeDto>>("api/employees").ConfigureAwait(true);
        var stock = await _api.GetAsync<List<StockItemDto>>("api/stock/items").ConfigureAwait(true);
        if (treatments.Value is null || qualifications.Value is null || locations.Value is null || employees.Value is null || stock.Value is null)
        {
            LoadError = treatments.Error ?? qualifications.Error ?? locations.Error ?? employees.Error ?? stock.Error ?? "Die Behandlungen wurden nicht geladen.";
            return;
        }

        var rooms = new List<RoomDto>();
        var equipment = new List<EquipmentDto>();
        foreach (var location in locations.Value)
        {
            var resources = await _api.GetAsync<LocationResources>($"api/locations/{location.Id}/resources").ConfigureAwait(true);
            if (resources.Value is not { } found)
            {
                LoadError = resources.Error ?? "Die Behandlungen wurden nicht geladen.";
                return;
            }

            rooms.AddRange(found.Rooms);
            equipment.AddRange(found.Equipment);
        }

        _catalog = new TreatmentCatalog(qualifications.Value, locations.Value, rooms, equipment, employees.Value, [.. stock.Value.Where(s => s.IsActive)], _showsMaterial);
        _treatments = treatments.Value;
        Rebuild();
        IsLoaded = true;
        var target = _pending?.Id;
        var jumpEntry = Entries.FirstOrDefault(e => target is not null && e.Id == target);
        Select(jumpEntry ?? Entries.FirstOrDefault(e => !e.IsHeading));
        if (target is not null && jumpEntry is null)
        {
            RejectPending();
            return;
        }

        ApplyPending();
    }

    public Task EnsureLoadedAsync() => IsLoaded ? Task.CompletedTask : LoadAsync();

    /// <summary>Sprung aus Kalender oder Assistent: wählt die Behandlung, dann den Abschnitt (z. B. „ablauf“) und das Feld (z. B. „treatment.price“).</summary>
    public void Open(Guid? id, string? section, string? field)
    {
        _pending = (id ?? SelectedEntry?.Id, section, field);
        if (!IsLoaded)
        {
            return;
        }

        if (id is not null)
        {
            if (Entries.FirstOrDefault(e => e.Id == id) is not { } entry)
            {
                RejectPending();
                return;
            }

            if (entry != SelectedEntry)
            {
                SelectedEntry = entry;
                return;
            }
        }

        ApplyPending();
    }

    /// <summary>Ein Sprungziel, das nicht (mehr) in der Liste steht: nichts an der offenen Behandlung ändern, nur melden.</summary>
    private void RejectPending()
    {
        _pending = null;
        Notice = NotOffered;
    }

    public Task<bool> SaveChangesAsync() => Editor is { } editor ? SaveEditorAsync(editor) : Task.FromResult(true);

    public void Discard() => Editor?.Discard();

    [RelayCommand]
    private async Task SaveAsync() => await SaveChangesAsync().ConfigureAwait(true);

    [RelayCommand]
    private void DiscardChanges() => Discard();

    /// <summary>„Nicht mehr anbieten“: nach der Rückfrage; gebuchte Termine bleiben bestehen.</summary>
    [RelayCommand]
    private async Task DeactivateAsync()
    {
        if (Editor is not { Id: { } id } editor)
        {
            return;
        }

        var answer = await AskAsync(new ConfirmRequest($"„{editor.Title}“ nicht mehr anbieten?",
            "Gebuchte Termine bleiben bestehen. Neue Buchungen sind nicht mehr möglich.", "Nicht mehr anbieten")).ConfigureAwait(true);
        if (!answer.IsConfirmed)
        {
            return;
        }

        var result = await _api.DeleteAsync<object>($"api/treatments/{id}").ConfigureAwait(true);
        if (!result.Ok)
        {
            // In der Leiste der Behandlung, nicht als Ladefehler: „Erneut laden“ verwürfe sonst ungespeicherte Änderungen.
            Notice = result.Error ?? "Die Behandlung wird weiter angeboten; das Deaktivieren hat nicht geklappt.";
            return;
        }

        _treatments.RemoveAll(t => t.Id == id);
        Rebuild();
        Select(Entries.FirstOrDefault(e => !e.IsHeading));
    }

    private async Task<bool> SaveEditorAsync(TreatmentEditorViewModel editor)
    {
        if (!await editor.SaveChangesAsync().ConfigureAwait(true))
        {
            return false;
        }

        var treatments = await _api.GetAsync<List<TreatmentDto>>("api/treatments").ConfigureAwait(true);
        if (treatments.Value is { } list)
        {
            _treatments = list;
            Rebuild();
            if (Entries.FirstOrDefault(e => e.Id == editor.Id) is { } entry)
            {
                // Die Liste zeigt die gespeicherte Behandlung, ohne die Maske neu aufzubauen (Stand und Verlauf bleiben).
                _reverting = true;
                SelectedEntry = entry;
                _reverting = false;
            }
        }

        OnPropertyChanged(nameof(Title));
        Saved?.Invoke();
        return true;
    }

    partial void OnSelectedEntryChanged(TreatmentEntry? oldValue, TreatmentEntry? newValue)
    {
        if (_reverting || newValue is null)
        {
            return;
        }

        if (newValue.IsHeading)
        {
            // Überschriften sind nicht wählbar.
            if (oldValue is not null)
            {
                ReturnTo(oldValue);
            }

            return;
        }

        if (oldValue is not null && HasUnsavedChanges)
        {
            // Erst fragen, dann wechseln: die Liste bleibt bei der alten Behandlung, bis die Antwort da ist.
            ReturnTo(oldValue);
            _ = LeaveAsync(newValue);
            return;
        }

        Show(newValue);
    }

    private async Task LeaveAsync(TreatmentEntry target)
    {
        if (await UnsavedChanges.ConfirmLeaveAsync(Dialogs, this).ConfigureAwait(true))
        {
            Select(target);
        }
        else
        {
            _pending = null;
        }
    }

    private void Select(TreatmentEntry? entry)
    {
        _reverting = true;
        SelectedEntry = entry;
        _reverting = false;
        if (entry is not null)
        {
            Show(entry);
        }
    }

    private void Show(TreatmentEntry entry)
    {
        if (Editor is not null)
        {
            Editor.PersonRequested -= OnPersonRequested;
        }

        Notice = null;
        var treatment = _treatments.FirstOrDefault(t => t.Id == entry.Id);
        Editor = new TreatmentEditorViewModel(_api, _catalog!, treatment, _zone) { Dialogs = Dialogs };
        Editor.PersonRequested += OnPersonRequested;
        if (_pending?.Id == entry.Id)
        {
            ApplyPending();
        }

        if (treatment is not null)
        {
            _ = Editor.LoadAsync();
        }
    }

    private void ApplyPending()
    {
        if (_pending is not { } pending || Editor is null)
        {
            return;
        }

        _pending = null;
        if (pending.Section is not null)
        {
            Editor.SelectSection(pending.Section);
        }

        if (pending.Field is not null)
        {
            Editor.FocusField(pending.Field);
        }
    }

    private void OnPersonRequested(Guid id) => PersonRequested?.Invoke(id);

    /// <summary>Behandlungen nach Kategorie, Kategorien alphabetisch, ohne Kategorie zuletzt; Überschriften nur bei mehr als einer Gruppe.</summary>
    private void Rebuild()
    {
        var groups = _treatments.GroupBy(t => string.IsNullOrWhiteSpace(t.Category) ? WithoutCategory : t.Category!)
            .OrderBy(g => g.Key == WithoutCategory ? 1 : 0).ThenBy(g => g.Key, StringComparer.CurrentCulture).ToList();
        var entries = new List<TreatmentEntry>();
        foreach (var group in groups)
        {
            if (groups.Count > 1)
            {
                entries.Add(new TreatmentEntry(group.Key, null, IsHeading: true));
            }

            entries.AddRange(group.Select(t => new TreatmentEntry(t.Name, t.Id)));
        }

        entries.Add(new TreatmentEntry("Behandlung anlegen", null));
        var selected = SelectedEntry;
        _reverting = true;
        Entries = entries;
        SelectedEntry = selected is null ? null : entries.FirstOrDefault(e => !e.IsHeading && e.Id == selected.Id && e.IsNew == selected.IsNew);
        _reverting = false;
    }

    /// <summary>Zurück zur Behandlung, die gefragt hat; gepostet, damit die Liste den Wert übernimmt (wie in der Verwaltung, Slice 93).</summary>
    private void ReturnTo(TreatmentEntry entry)
    {
        void Return()
        {
            _reverting = true;
            SelectedEntry = entry;
            _reverting = false;
        }

        if (_ui is null)
        {
            Return();
        }
        else
        {
            _ui.Post(_ => Return(), null);
        }
    }
}
