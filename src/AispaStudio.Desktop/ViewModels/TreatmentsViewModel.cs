using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>
/// Zeile der Behandlungsliste: eine Behandlung mit Dauer und Preis, eine einklappbare Kategorie als Überschrift mit Anzahl, oder die noch nicht angelegte neue Behandlung
/// (<c>Id</c> leer, steht nicht in der Liste).
/// </summary>
public sealed record TreatmentEntry(string Title, Guid? Id, bool IsHeading = false)
{
    /// <summary>„60 Min. · 115 €“ rechts in der Zeile.</summary>
    public string? Meta { get; init; }

    /// <summary>Die Behandlung wird nicht mehr angeboten (nur sichtbar mit „Nicht mehr angebotene zeigen“).</summary>
    public bool IsOffered { get; init; } = true;

    /// <summary>Behandlungen der Kategorie (nur für eine Überschrift).</summary>
    public int Count { get; init; }

    public bool IsCollapsed { get; init; }

    public bool IsNew => Id is null && !IsHeading;

    public bool IsTreatment => !IsHeading;

    public bool IsExpandedHeading => IsHeading && !IsCollapsed;

    public bool IsNotOffered => !IsHeading && !IsOffered;

    /// <summary>Was der Screenreader zur Zeile zusätzlich sagt: Dauer und Preis, ob angeboten; bei der Kategorie der Zustand.</summary>
    public string? Description => IsHeading
        ? $"{Count} {(Count == 1 ? "Behandlung" : "Behandlungen")}, {(IsCollapsed ? "eingeklappt" : "aufgeklappt")}"
        : IsOffered ? Meta : $"{Meta}, nicht angeboten";

    public override string ToString() => Title;
}

/// <summary>
/// Bereich „Behandlungen“ der Verwaltung (R05 Slice 94, Ä191; neu geordnet am 17.11.2026, Nachtrag zu ADR 0098): links „Neue Behandlung“ fest über der Suche und der Liste nach
/// Kategorie (einklappbar, mit Anzahl; Kategorien alphabetisch, „Ohne Kategorie“ zuletzt), darunter „Nicht mehr angebotene zeigen“; rechts die gewählte Behandlung
/// (<see cref="TreatmentEditorViewModel"/>). Wer mit ungespeicherten Änderungen eine andere Behandlung wählt, wird gefragt wie beim Verlassen der Maske (<see cref="UnsavedChanges"/>).
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

    /// <summary>Nicht mehr angebotene Behandlungen; erst gefüllt, wenn „Nicht mehr angebotene zeigen“ an ist (<c>GET api/treatments?includeInactive=true</c>).</summary>
    private List<TreatmentDto> _inactive = [];

    private readonly HashSet<string> _collapsed = [];
    private readonly TreatmentEntry _newEntry = new("Neue Behandlung", null);
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

    /// <summary>Suche in Name und Kategorie; solange sie etwas enthält, sind alle Kategorien aufgeklappt.</summary>
    [ObservableProperty]
    public partial string Search { get; set; } = "";

    /// <summary>Nicht mehr angebotene Behandlungen in der Liste zeigen (ausgegraut, mit „Nicht angeboten“).</summary>
    [ObservableProperty]
    public partial bool ShowInactive { get; set; }

    /// <summary>Die Suche findet nichts.</summary>
    public bool HasNoMatches => IsLoaded && Entries.Count == 0 && Search.Trim().Length > 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Title))]
    public partial TreatmentEditorViewModel? Editor { get; private set; }

    [ObservableProperty]
    public partial bool IsLoaded { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLoadError))]
    public partial string? LoadError { get; private set; }

    public bool HasLoadError => LoadError is not null;

    /// <summary>Meldung über der Maske: ein Sprungziel, das nicht mehr angeboten wird, oder ein gescheitertes „Nicht mehr anbieten“ bzw. „Wieder anbieten“.</summary>
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

    /// <summary>Der Stand der offenen Behandlung („Gespeichert. Gilt für neue Buchungen.“).</summary>
    public string? StatusText => Editor?.StatusText;

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
            or nameof(TreatmentEditorViewModel.Title))
        {
            RaiseFooter();
        }
    }

    private void RaiseFooter()
    {
        OnPropertyChanged(nameof(HasUnsavedChanges));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(Title));
    }

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

    public Task<bool> SaveChangesAsync() => Editor is { } editor ? editor.SaveChangesAsync() : Task.FromResult(true);

    public void Discard() => Editor?.Discard();

    /// <summary>Legt eine neue Behandlung an; mit ungespeicherten Änderungen fragt die Maske vorher.</summary>
    [RelayCommand]
    private async Task NewAsync()
    {
        if (HasUnsavedChanges && !await UnsavedChanges.ConfirmLeaveAsync(Dialogs, this).ConfigureAwait(true))
        {
            return;
        }

        Select(_newEntry);
    }

    /// <summary>„Nicht mehr anbieten“ (Menü „⋯“ der Karte): nach der Rückfrage; gebuchte Termine bleiben bestehen.</summary>
    private async Task DeactivateAsync()
    {
        if (Editor is not { Id: { } id } editor)
        {
            return;
        }

        var answer = await AskAsync(new ConfirmRequest($"„{editor.Title}“ nicht mehr anbieten?",
            "Gebuchte Termine bleiben bestehen. Neue Buchungen sind nicht mehr möglich. Über „Nicht mehr angebotene zeigen“ lässt sich die Behandlung wieder anbieten.",
            "Nicht mehr anbieten")).ConfigureAwait(true);
        if (!answer.IsConfirmed)
        {
            return;
        }

        var result = await _api.DeleteAsync<object>($"api/treatments/{id}").ConfigureAwait(true);
        if (!result.Ok)
        {
            // In der Meldung über der Liste, nicht als Ladefehler: „Erneut laden“ verwürfe sonst ungespeicherte Änderungen.
            Notice = result.Error ?? "Die Behandlung wird weiter angeboten; das Deaktivieren hat nicht geklappt.";
            return;
        }

        if (_treatments.FirstOrDefault(t => t.Id == id) is { } removed && ShowInactive)
        {
            _inactive.Add(removed with { IsActive = false });
        }

        _treatments.RemoveAll(t => t.Id == id);
        Rebuild();
        Select(Entries.FirstOrDefault(e => !e.IsHeading));
    }

    /// <summary>„Wieder anbieten“ einer nicht mehr angebotenen Behandlung.</summary>
    private async Task ReactivateAsync()
    {
        if (Editor is not { Id: { } id })
        {
            return;
        }

        var result = await _api.PostAsync<TreatmentDto>($"api/treatments/{id}/reactivate", null).ConfigureAwait(true);
        if (result.Value is not { } offered)
        {
            Notice = result.Error ?? "Die Behandlung wird weiter nicht angeboten; das Wiederanbieten hat nicht geklappt.";
            return;
        }

        _inactive.RemoveAll(t => t.Id == id);
        _treatments.RemoveAll(t => t.Id == id);
        _treatments.Add(offered);
        Rebuild();
        Select(Entries.FirstOrDefault(e => e.Id == id) ?? Entries.FirstOrDefault(e => !e.IsHeading));
    }

    /// <summary>Lädt die nicht mehr angebotenen Behandlungen, sobald der Schalter angeht; ein Fehler schaltet zurück und meldet.</summary>
    partial void OnShowInactiveChanged(bool value) => _ = ApplyShowInactiveAsync(value);

    private async Task ApplyShowInactiveAsync(bool value)
    {
        if (!IsLoaded)
        {
            return;
        }

        if (!value)
        {
            _inactive = [];
            Rebuild();
            return;
        }

        var all = await _api.GetAsync<List<TreatmentDto>>("api/treatments?includeInactive=true").ConfigureAwait(true);
        if (all.Value is null)
        {
            Notice = all.Error ?? "Die nicht mehr angebotenen Behandlungen wurden nicht geladen.";
            ShowInactive = false;
            return;
        }

        _inactive = [.. all.Value.Where(t => t.IsActive == false)];
        Rebuild();
    }

    partial void OnSearchChanged(string value)
    {
        if (IsLoaded)
        {
            Rebuild();
        }
    }

    /// <summary>Klappt die Kategorie auf oder zu; die Liste zeigt ihre Behandlungen oder nur die Überschrift.</summary>
    private void ToggleCategory(string category)
    {
        if (!_collapsed.Remove(category))
        {
            _collapsed.Add(category);
        }

        Rebuild();
    }

    /// <summary>Nach dem Speichern der Karte: die Liste zeigt die gespeicherte Behandlung (Name, Dauer, Preis, Kategorie).</summary>
    private async Task RefreshAfterSaveAsync(TreatmentEditorViewModel editor)
    {
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
    }

    partial void OnSelectedEntryChanged(TreatmentEntry? oldValue, TreatmentEntry? newValue)
    {
        if (_reverting || newValue is null)
        {
            return;
        }

        if (newValue.IsHeading)
        {
            // Eine Überschrift ist nicht wählbar, ein Klick klappt die Kategorie auf oder zu; die Auswahl bleibt bei der Behandlung.
            ReturnTo(oldValue);
            ToggleCategory(newValue.Title);
            return;
        }

        if (HasUnsavedChanges)
        {
            // Erst fragen, dann wechseln: die Liste bleibt bei der alten Behandlung (bei einer neuen: ohne Auswahl), bis die Antwort da ist.
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
        Notice = null;
        var treatment = _treatments.FirstOrDefault(t => t.Id == entry.Id) ?? _inactive.FirstOrDefault(t => t.Id == entry.Id);
        Editor = new TreatmentEditorViewModel(_api, _catalog!, treatment, _zone)
        {
            Dialogs = Dialogs,
            AfterSaveAsync = RefreshAfterSaveAsync,
            DeactivateAction = () => _ = DeactivateAsync(),
            ReactivateAction = () => _ = ReactivateAsync(),
        };
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

    /// <summary>
    /// Behandlungen nach Kategorie, Kategorien alphabetisch, ohne Kategorie zuletzt; Überschriften nur bei mehr als einer Kategorie. Die Suche filtert nach Name und Kategorie
    /// und klappt alles auf; eine zugeklappte Kategorie zeigt nur ihre Überschrift mit der Anzahl.
    /// </summary>
    private void Rebuild()
    {
        var search = Search.Trim();
        var shown = _treatments.Select(t => (Treatment: t, Offered: true))
            .Concat(_inactive.Select(t => (Treatment: t, Offered: false)))
            .Where(t => search.Length == 0 || t.Treatment.Name.Contains(search, StringComparison.CurrentCultureIgnoreCase)
                || (t.Treatment.Category ?? "").Contains(search, StringComparison.CurrentCultureIgnoreCase));
        var groups = shown.GroupBy(t => string.IsNullOrWhiteSpace(t.Treatment.Category) ? WithoutCategory : t.Treatment.Category!)
            .OrderBy(g => g.Key == WithoutCategory ? 1 : 0).ThenBy(g => g.Key, StringComparer.CurrentCulture).ToList();
        var entries = new List<TreatmentEntry>();
        foreach (var group in groups)
        {
            var collapsed = search.Length == 0 && _collapsed.Contains(group.Key);
            if (groups.Count > 1 || collapsed)
            {
                entries.Add(new TreatmentEntry(group.Key, null, IsHeading: true) { Count = group.Count(), IsCollapsed = collapsed });
            }

            if (!collapsed)
            {
                entries.AddRange(group.OrderBy(t => t.Treatment.Name, StringComparer.CurrentCulture)
                    .Select(t => new TreatmentEntry(t.Treatment.Name, t.Treatment.Id) { Meta = Meta(t.Treatment), IsOffered = t.Offered }));
            }
        }

        var selected = SelectedEntry;
        _reverting = true;
        Entries = entries;
        SelectedEntry = selected is null ? null : entries.FirstOrDefault(e => !e.IsHeading && e.Id == selected.Id && e.IsNew == selected.IsNew);
        _reverting = false;
        OnPropertyChanged(nameof(HasNoMatches));
    }

    /// <summary>„60 Min. · 115 €“: Dauer und Grundpreis.</summary>
    private static string Meta(TreatmentDto treatment) =>
        $"{treatment.DurationMinutes} Min. · {TreatmentEditorViewModel.PriceText(treatment.BasePrice ?? treatment.Price)}";

    /// <summary>Zurück zur Behandlung, die gefragt hat; gepostet, damit die Liste den Wert übernimmt (wie in der Verwaltung, Slice 93).</summary>
    private void ReturnTo(TreatmentEntry? entry)
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
