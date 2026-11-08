using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text.Json;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>
/// Was die Maske der Behandlungen zum Auswählen braucht: Qualifikationen, Standorte mit Räumen und Geräten, Team und Lager. Material (Kosten je Behandlung)
/// gibt es nur mit dem Recht auf Berichte mit Beträgen, wie der Endpunkt es verlangt.
/// </summary>
public sealed record TreatmentCatalog(
    IReadOnlyList<QualificationDto> Qualifications,
    IReadOnlyList<LocationDto> Locations,
    IReadOnlyList<RoomDto> Rooms,
    IReadOnlyList<EquipmentDto> Equipment,
    IReadOnlyList<EmployeeDto> Employees,
    IReadOnlyList<StockItemDto> StockItems,
    bool ShowsMaterial = true);

/// <summary>Abschnitt der Behandlung in der Reiterleiste (wie die Bereiche in <c>TreatmentsTab.tsx</c>).</summary>
public sealed partial class TreatmentSection(string id, string label, Action<string> select) : ObservableObject
{
    public string Id => id;

    public string Label => label;

    [ObservableProperty]
    public partial bool IsOn { get; set; }

    [RelayCommand]
    private void Select() => select(id);
}

/// <summary>
/// Eine Behandlung in der nativen Verwaltung (R05 Slice 94): dieselben Felder, Grenzen und Texte wie <c>web/src/admin/TreatmentsTab.tsx</c> mit den Abschnitten
/// Grunddaten, Ablauf (mit Vorbereitungsschritten), Material, Zum Termin bereitstellen, Qualifikationen/Räume/Geräte und Preise (je Standort und nach Zeit).
/// Anders als im Web speichert ein „Speichern“ alles gemeinsam: die Behandlung und danach jede geänderte Teil-Liste. Prüfen und Rechnen macht die API; hier
/// stehen nur die Pflichtfelder vorab am Feld und der Hinweis auf eine fehlende Qualifikation.
/// </summary>
public sealed partial class TreatmentEditorViewModel : ViewModelBase, IEditableArea
{
    public const string NameField = "name";
    public const string PriceField = "treatment.price";
    public const string DurationField = "treatment.duration";
    public const string QualificationsField = "qualifications";

    public const string Basics = "grunddaten";
    public const string Process = "ablauf";
    public const string Material = "material";
    public const string Provide = "bereitstellen";
    public const string Resources = "ressourcen";
    public const string Prices = "preise";
    public const string Sales = "verkauf";

    /// <summary>Mehr Vorschläge liest an der Rezeption niemand; die API lehnt mehr ab.</summary>
    public const int MaxRecommendedArticles = 5;

    private static readonly (string Id, string Label)[] AllSections =
    [
        (Basics, "Grunddaten"), (Process, "Ablauf"), (Material, "Material"), (Provide, "Bereitstellen"), (Sales, "Verkauf"),
        (Resources, "Qualifikationen & Räume"), (Prices, "Preise"),
    ];

    private static readonly string[] Inputs =
    [
        nameof(Name), nameof(Duration), nameof(Price), nameof(Category), nameof(NoShowFeePercent), nameof(DepositPercent), nameof(GuestHint),
        nameof(CheckAnamnesis), nameof(PreparationMinutes), nameof(FollowUpMinutes), nameof(HandsOnMinutes), nameof(ProcessingMinutes),
    ];

    private readonly IWorkstationApi _api;
    private readonly TreatmentCatalog _catalog;
    private readonly TimeZoneInfo _zone;
    private readonly IReadOnlyList<Choice> _stock;
    private readonly IReadOnlyList<Choice> _ruleLocations;
    private TreatmentDto? _saved;
    private List<TreatmentPriceRuleDto> _savedRules = [];
    private List<PreparationStepDto> _savedSteps = [];
    private List<MaterialLine> _savedMaterials = [];
    private List<TreatmentExtraDto> _savedExtras = [];
    private List<Guid> _savedRecommended = [];
    private List<ArticleDto> _articles = [];
    private string _treatmentState = "";
    private string _rulesState = "";
    private string _stepsState = "";
    private string _materialsState = "";
    private string _extrasState = "";
    private string _recommendedState = "";
    private bool _applying;

    /// <summary>Der Hinweis auf die fehlende Qualifikation wurde gezeigt; das nächste Speichern speichert ohne.</summary>
    private bool _qualificationNoticed;

    public TreatmentEditorViewModel(IWorkstationApi api, TreatmentCatalog catalog, TreatmentDto? treatment, TimeZoneInfo zone)
    {
        _api = api;
        _catalog = catalog;
        _zone = zone;
        _stock = [new Choice(null, "Ohne Material"), .. catalog.StockItems.Select(s => new Choice(s.Id, s.Name))];
        _ruleLocations = [new Choice(null, "Alle Standorte"), .. catalog.Locations.Select(l => new Choice(l.Id, l.Name))];
        Apply(treatment);
        ApplyLists([], [], [], []);
        ApplyRecommended([]);

        // Eine neue Behandlung hat keine Teil-Listen; eine gespeicherte lädt sie (LoadAsync).
        IsLoaded = treatment is null;
    }

    /// <summary>Id der gespeicherten Behandlung; leer für eine neue.</summary>
    public Guid? Id => _saved?.Id;

    public bool IsNew => _saved is null;

    /// <summary>Name in der Rückfrage und Überschrift: wie gespeichert, für eine neue „Neue Behandlung“.</summary>
    public string Title => _saved?.Name ?? "Neue Behandlung";

    /// <summary>Mehrere Standorte: dann gibt es Preise je Standort und die Standort-Auswahl der Preisregeln.</summary>
    public bool HasSeveralLocations => _catalog.Locations.Count > 1;

    [ObservableProperty]
    public partial IReadOnlyList<TreatmentSection> Sections { get; private set; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsBasics), nameof(ShowsProcess), nameof(ShowsMaterial), nameof(ShowsProvide), nameof(ShowsSales), nameof(ShowsResources),
        nameof(ShowsPrices))]
    public partial string SelectedSection { get; private set; } = Basics;

    public bool ShowsBasics => SelectedSection == Basics;

    public bool ShowsProcess => SelectedSection == Process;

    public bool ShowsMaterial => SelectedSection == Material;

    public bool ShowsProvide => SelectedSection == Provide;

    public bool ShowsSales => SelectedSection == Sales;

    public bool ShowsResources => SelectedSection == Resources;

    public bool ShowsPrices => SelectedSection == Prices;

    /// <summary>Die Teil-Listen (Preisregeln, Vorbereitung, Material, Bereitstellen) sind geladen; eine neue Behandlung hat keine.</summary>
    [ObservableProperty]
    public partial bool IsLoaded { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLoadError))]
    public partial string? LoadError { get; private set; }

    public bool HasLoadError => LoadError is not null;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    public partial bool IsSaving { get; private set; }

    [ObservableProperty]
    public partial string Name { get; set; } = "";

    [ObservableProperty]
    public partial decimal? Duration { get; set; }

    [ObservableProperty]
    public partial decimal? Price { get; set; }

    [ObservableProperty]
    public partial string Category { get; set; } = "";

    /// <summary>Eigene Ausfallgebühr in Prozent; leer heißt Regel des Standorts.</summary>
    [ObservableProperty]
    public partial decimal? NoShowFeePercent { get; set; }

    /// <summary>Eigene Anzahlung in Prozent; leer heißt Regel des Standorts.</summary>
    [ObservableProperty]
    public partial decimal? DepositPercent { get; set; }

    [ObservableProperty]
    public partial string GuestHint { get; set; } = "";

    [ObservableProperty]
    public partial bool CheckAnamnesis { get; set; }

    [ObservableProperty]
    public partial decimal? PreparationMinutes { get; set; }

    [ObservableProperty]
    public partial decimal? FollowUpMinutes { get; set; }

    [ObservableProperty]
    public partial decimal? HandsOnMinutes { get; set; }

    [ObservableProperty]
    public partial decimal? ProcessingMinutes { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<CheckOption> Qualifications { get; private set; } = [];

    [ObservableProperty]
    public partial IReadOnlyList<LocationResourceGroup> LocationGroups { get; private set; } = [];

    [ObservableProperty]
    public partial IReadOnlyList<LocationPriceRow> LocationPrices { get; private set; } = [];

    public ObservableCollection<PriceRuleRow> PriceRules { get; } = [];

    public ObservableCollection<PreparationStepRow> PreparationSteps { get; } = [];

    public ObservableCollection<MaterialRow> Materials { get; } = [];

    public ObservableCollection<ExtraRow> Extras { get; } = [];

    /// <summary>„Passt dazu“ (Slice 115): Verkaufsartikel, die Kassa und Termin-Panel zu dieser Behandlung vorschlagen, in ihrer Reihenfolge.</summary>
    public ObservableCollection<RecommendedArticleRow> RecommendedArticles { get; } = [];

    /// <summary>Aktive Kassa-Artikel, die noch nicht in „Passt dazu“ stehen.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<ArticleDto> ArticleChoices { get; private set; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddRecommendedArticleCommand))]
    public partial ArticleDto? ArticleToAdd { get; set; }

    /// <summary>Die Kassa-Artikel wurden nicht geladen: „Passt dazu“ lässt sich dann nicht ändern.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddRecommendedArticleCommand))]
    public partial bool HasSalesLoadError { get; private set; }

    private bool CanAddRecommendedArticle => !HasSalesLoadError && ArticleToAdd is not null && RecommendedArticles.Count < MaxRecommendedArticles;

    [RelayCommand(CanExecute = nameof(CanAddRecommendedArticle))]
    private void AddRecommendedArticle()
    {
        if (ArticleToAdd is not { } article || RecommendedArticles.Any(r => r.Id == article.Id))
        {
            return;
        }

        RecommendedArticles.Add(new RecommendedArticleRow(article.Id, article.Name, Changed, Remove));
        ArticleToAdd = null;
        RecommendedChanged();
    }

    /// <summary>„Wer kann das?“: Personen mit allen gewählten Qualifikationen (dieselbe Regel, nach der der Kalender sie anbietet, <see cref="TreatmentFits.Suits"/>).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNobody))]
    public partial IReadOnlyList<PersonEntry> WhoCanDo { get; private set; } = [];

    public bool HasNobody => WhoCanDo.Count == 0;

    /// <summary>Abschnitt „Verlauf“ (ADR 0082); erst für eine gespeicherte Behandlung.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasHistory))]
    public partial HistorySectionViewModel? History { get; private set; }

    public bool HasHistory => History is not null;

    [ObservableProperty]
    public partial string? NameError { get; private set; }

    [ObservableProperty]
    public partial string? PriceError { get; private set; }

    /// <summary>Hinweis am Feld „Nötige Qualifikationen“, wenn keine gewählt ist; ein zweites „Speichern“ speichert ohne.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasQualificationNotice))]
    public partial string? QualificationNotice { get; private set; }

    public bool HasQualificationNotice => QualificationNotice is not null;

    /// <summary>Fehler der API beim Speichern, über der Leiste.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? Error { get; private set; }

    public bool HasError => Error is not null;

    /// <summary>„Gespeichert. Gilt für neue Buchungen.“ in der Leiste, bis wieder etwas geändert wird.</summary>
    [ObservableProperty]
    public partial string? StatusText { get; private set; }

    /// <summary>Das Feld, das den Fokus bekommen soll; die Ansicht setzt ihn und leert die Angabe.</summary>
    [ObservableProperty]
    public partial string? FocusTarget { get; set; }

    /// <summary>Eine Person aus „Wer kann das?“ soll im Bereich Team geöffnet werden.</summary>
    public event Action<Guid>? PersonRequested;

    // Die Grunddaten zählen auch, wenn eine Teil-Liste nicht geladen wurde; die Teil-Listen erst, wenn sie da sind.
    public bool HasUnsavedChanges =>
        TreatmentState() != _treatmentState || (IsLoaded && (RulesState() != _rulesState || StepsState() != _stepsState
            || MaterialsState() != _materialsState || ExtrasState() != _extrasState || RecommendedState() != _recommendedState));

    /// <summary>Lädt Preisregeln, Vorbereitung, Material und Bereitstellen der gespeicherten Behandlung.</summary>
    [RelayCommand]
    public async Task LoadAsync()
    {
        if (_saved is not { } treatment)
        {
            return;
        }

        LoadError = null;
        var id = treatment.Id;
        var rules = await _api.GetAsync<List<TreatmentPriceRuleDto>>($"api/treatments/{id}/price-rules").ConfigureAwait(true);
        var steps = await _api.GetAsync<List<PreparationStepDto>>($"api/reports/treatments/{id}/preparation").ConfigureAwait(true);
        var materials = _catalog.ShowsMaterial
            ? await _api.GetAsync<List<MaterialLine>>($"api/costs/treatments/{id}/materials").ConfigureAwait(true)
            : new ApiResult<List<MaterialLine>>([], null, System.Net.HttpStatusCode.OK);
        var extras = await _api.GetAsync<List<TreatmentExtraDto>>($"api/treatments/{id}/extras").ConfigureAwait(true);
        if (rules.Value is null || steps.Value is null || materials.Value is null || extras.Value is null)
        {
            LoadError = rules.Error ?? steps.Error ?? materials.Error ?? extras.Error ?? "Die Behandlung wurde nicht vollständig geladen.";
            return;
        }

        ApplyLists(rules.Value, steps.Value, materials.Value, extras.Value);
        await LoadRecommendedAsync(id).ConfigureAwait(true);
        IsLoaded = true;
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    /// <summary>„Passt dazu“ und die Kassa-Artikel; ohne sie bleibt die Liste leer und unverändert, der Rest der Behandlung lässt sich trotzdem bearbeiten.</summary>
    private async Task LoadRecommendedAsync(Guid id)
    {
        var recommended = await _api.GetAsync<List<Guid>>($"api/treatments/{id}/recommended-articles").ConfigureAwait(true);
        var articles = await _api.GetAsync<List<ArticleDto>>("api/pos/articles").ConfigureAwait(true);
        HasSalesLoadError = recommended.Value is null || articles.Value is null;
        // Ohne gespeicherte Liste bietet die Maske nichts an: ein Hinzufügen würde die Zuordnung beim Speichern überschreiben.
        _articles = HasSalesLoadError ? [] : articles.Value!;
        ApplyRecommended(HasSalesLoadError ? [] : recommended.Value!);
    }

    /// <summary>Wählt einen Abschnitt; einen, den es für eine neue Behandlung nicht gibt, ersetzt „Grunddaten“.</summary>
    public void SelectSection(string? section)
    {
        SelectedSection = Sections.FirstOrDefault(s => s.Id == section)?.Id ?? Basics;
        foreach (var s in Sections)
        {
            s.IsOn = s.Id == SelectedSection;
        }
    }

    /// <summary>Sprung an ein Feld: wählt dessen Abschnitt und setzt den Fokus, sobald die Maske steht.</summary>
    public void FocusField(string? field)
    {
        if (field is PriceField or DurationField or NameField)
        {
            SelectSection(Basics);
        }

        FocusTarget = field;
    }

    private bool CanSave => !IsSaving;

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync() => await SaveChangesAsync().ConfigureAwait(true);

    public async Task<bool> SaveChangesAsync()
    {
        if (IsSaving || !Check())
        {
            return false;
        }

        Error = null;
        IsSaving = true;
        try
        {
            var wasNew = IsNew;
            if (wasNew || TreatmentState() != _treatmentState)
            {
                var result = wasNew
                    ? await _api.PostAsync<TreatmentDto>("api/treatments", Input()).ConfigureAwait(true)
                    : await _api.PutAsync<TreatmentDto>($"api/treatments/{_saved!.Id}", Input()).ConfigureAwait(true);
                if (result.Value is not { } saved)
                {
                    Error = result.Error ?? "Die Behandlung wurde nicht gespeichert.";
                    return false;
                }

                Apply(saved);
            }

            // Nicht geladene Teil-Listen werden nicht gespeichert, sonst ersetzten leere Listen die gespeicherten.
            if (!wasNew && IsLoaded && !await SaveListsAsync(_saved!.Id).ConfigureAwait(true))
            {
                return false;
            }

            StatusText = "Gespeichert. Gilt für neue Buchungen.";
            if (History?.IsOpen == true)
            {
                await History.LoadAsync().ConfigureAwait(true);
            }

            return true;
        }
        finally
        {
            IsSaving = false;
            OnPropertyChanged(nameof(HasUnsavedChanges));
        }
    }

    [RelayCommand]
    public void Discard()
    {
        Apply(_saved);
        ApplyLists(_savedRules, _savedSteps, _savedMaterials, _savedExtras);
        ApplyRecommended(_savedRecommended);
        NameError = null;
        PriceError = null;
        QualificationNotice = null;
        Error = null;
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    [RelayCommand]
    private void CloseError() => Error = null;

    [RelayCommand]
    private void AddPriceRule()
    {
        var rule = new TreatmentPriceRuleDto("", [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday],
            new TimeOnly(9, 0), new TimeOnly(12, 0), Price ?? 0);
        PriceRules.Add(new PriceRuleRow(rule, _ruleLocations, Changed, Remove));
        Changed();
    }

    [RelayCommand]
    private void AddStep()
    {
        PreparationSteps.Add(new PreparationStepRow(new PreparationStepDto(Guid.Empty, "", null), _stock, Changed, Remove));
        Changed();
    }

    [RelayCommand]
    private void AddMaterial()
    {
        Materials.Add(new MaterialRow(new MaterialLine("", 1, "Stück", 0), MaterialChoices(), _catalog.StockItems, Changed, Remove));
        Changed();
    }

    [RelayCommand]
    private void AddExtra()
    {
        Extras.Add(new ExtraRow(new TreatmentExtraDto(Guid.Empty, "", ExtraTiming.OnArrival, null, null, null, null, 0), ExtraChoices(), Changed, Remove));
        Changed();
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (_applying || e.PropertyName is not { } name || !Inputs.Contains(name))
        {
            return;
        }

        if (name == nameof(Name))
        {
            NameError = null;
        }
        else if (name == nameof(Price))
        {
            PriceError = null;
        }

        Changed();
    }

    /// <summary>Pflichtfelder und der Hinweis auf die fehlende Qualifikation vor dem Speichern; die übrigen Regeln prüft die API.</summary>
    private bool Check()
    {
        if (Name.Trim().Length == 0)
        {
            NameError = "Der Name der Behandlung fehlt. Trage ihn ein, wie Gäste ihn bei der Buchung sehen.";
            SelectSection(Basics);
            FocusTarget = NameField;
            return false;
        }

        if (Price is null)
        {
            PriceError = "Der Preis fehlt. Trage ihn ein, z. B. 89,00.";
            SelectSection(Basics);
            FocusTarget = PriceField;
            return false;
        }

        if (!Qualifications.Any(q => q.IsChecked) && (IsNew || _saved!.RequiredQualificationIds.Count > 0) && !_qualificationNoticed)
        {
            QualificationNotice =
                "Keine Qualifikation gewählt: Jede Person im Team kann diese Behandlung übernehmen. Zum Speichern ohne Qualifikation noch einmal „Speichern“ wählen.";
            _qualificationNoticed = true;
            SelectSection(Resources);
            FocusTarget = QualificationsField;
            return false;
        }

        if (PriceRules.Any(r => !r.IsComplete))
        {
            Error = "Jede Preisregel braucht Beginn, Ende und Preis.";
            SelectSection(Prices);
            return false;
        }

        return true;
    }

    /// <summary>Speichert jede geänderte Teil-Liste; beim ersten Fehler bleibt der Rest ungespeichert und die Maske offen.</summary>
    private async Task<bool> SaveListsAsync(Guid id)
    {
        if (RulesState() != _rulesState)
        {
            var result = await _api.PutAsync<List<TreatmentPriceRuleDto>>($"api/treatments/{id}/price-rules", Rules()).ConfigureAwait(true);
            if (result.Value is not { } rules)
            {
                return Failed(result.Error, "Die Preisregeln wurden nicht gespeichert.", Prices);
            }

            ApplyLists(rules, null, null, null);
        }

        if (StepsState() != _stepsState)
        {
            var result = await _api.PutAsync<List<PreparationStepDto>>($"api/reports/treatments/{id}/preparation", Steps()).ConfigureAwait(true);
            if (result.Value is not { } steps)
            {
                return Failed(result.Error, "Die Vorbereitung wurde nicht gespeichert.", Process);
            }

            ApplyLists(null, steps, null, null);
        }

        if (MaterialsState() != _materialsState)
        {
            var result = await _api.PutAsync<List<MaterialLine>>($"api/costs/treatments/{id}/materials", Lines()).ConfigureAwait(true);
            if (result.Value is not { } lines)
            {
                return Failed(result.Error, "Das Material wurde nicht gespeichert.", Material);
            }

            ApplyLists(null, null, lines, null);
        }

        if (ExtrasState() != _extrasState)
        {
            var result = await _api.PutAsync<List<TreatmentExtraDto>>($"api/treatments/{id}/extras", ExtraInputs()).ConfigureAwait(true);
            if (result.Value is not { } extras)
            {
                return Failed(result.Error, "„Zum Termin bereitstellen“ wurde nicht gespeichert.", Provide);
            }

            ApplyLists(null, null, null, extras);
        }

        if (RecommendedState() != _recommendedState)
        {
            var result = await _api.PutAsync<List<Guid>>($"api/treatments/{id}/recommended-articles", RecommendedIds()).ConfigureAwait(true);
            if (result.Value is not { } recommended)
            {
                return Failed(result.Error, "„Passt dazu“ wurde nicht gespeichert.", Sales);
            }

            ApplyRecommended(recommended);
        }

        return true;
    }

    private bool Failed(string? error, string fallback, string section)
    {
        Error = error ?? fallback;
        SelectSection(section);
        return false;
    }

    private void Changed()
    {
        if (_applying)
        {
            return;
        }

        StatusText = null;
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    private void QualificationsChanged()
    {
        if (_applying)
        {
            return;
        }

        _qualificationNoticed = false;
        QualificationNotice = null;
        UpdateWhoCanDo();
        Changed();
    }

    private void Remove(EditorRow row)
    {
        switch (row)
        {
            case PriceRuleRow rule:
                PriceRules.Remove(rule);
                break;
            case PreparationStepRow step:
                PreparationSteps.Remove(step);
                break;
            case MaterialRow material:
                Materials.Remove(material);
                break;
            case ExtraRow extra:
                Extras.Remove(extra);
                break;
            case RecommendedArticleRow article:
                RecommendedArticles.Remove(article);
                RecommendedChanged();
                return;
        }

        Changed();
    }

    private void UpdateWhoCanDo()
    {
        var required = Qualifications.Where(q => q.IsChecked).Select(q => q.Id).ToList();
        WhoCanDo = [.. _catalog.Employees.Where(e => required.All(e.QualificationIds.Contains)).OrderBy(e => e.DisplayName, StringComparer.CurrentCulture)
            .Select(e => new PersonEntry(e.Id, e.DisplayName, new RelayCommand(() => PersonRequested?.Invoke(e.Id))))];
    }

    /// <summary>Setzt alle Felder der Behandlung auf <paramref name="treatment"/> (leer: Vorgaben einer neuen wie im Web).</summary>
    private void Apply(TreatmentDto? treatment)
    {
        _applying = true;
        var sectionsChange = Sections.Count == 0 || IsNew != (treatment is null);
        _saved = treatment;
        Name = treatment?.Name ?? "";
        Duration = treatment?.DurationMinutes ?? 60;
        Price = treatment?.BasePrice ?? treatment?.Price ?? 0;
        Category = treatment?.Category ?? "";
        NoShowFeePercent = treatment?.NoShowFeePercent;
        DepositPercent = treatment?.DepositPercent;
        GuestHint = treatment?.GuestHint ?? "";
        CheckAnamnesis = treatment?.CheckAnamnesis ?? false;
        PreparationMinutes = treatment?.PreparationMinutes ?? 5;
        FollowUpMinutes = treatment?.FollowUpMinutes ?? 10;
        HandsOnMinutes = treatment?.HandsOnMinutes ?? 0;
        ProcessingMinutes = treatment?.ProcessingMinutes ?? 0;
        var required = treatment?.RequiredQualificationIds ?? [];
        Qualifications = [.. _catalog.Qualifications.Select(q => new CheckOption(q.Id, q.Name, required.Contains(q.Id), QualificationsChanged))];
        var rooms = treatment?.RoomIds ?? [];
        var equipment = treatment?.EquipmentIds ?? [];
        LocationGroups = [.. _catalog.Locations.Select(l => new LocationResourceGroup(l.Name,
            [.. _catalog.Rooms.Where(r => r.LocationId == l.Id && !r.IsArea).Select(r => new CheckOption(r.Id, r.Name, rooms.Contains(r.Id), Changed))],
            [.. _catalog.Equipment.Where(e => e.LocationId == l.Id).Select(e => new CheckOption(e.Id, e.Name, equipment.Contains(e.Id), Changed))]))];
        var prices = treatment?.LocationPrices ?? [];
        LocationPrices = HasSeveralLocations
            ? [.. _catalog.Locations.Select(l => new LocationPriceRow(l.Id, l.Name, prices.FirstOrDefault(p => p.LocationId == l.Id)?.Price, Changed))]
            : [];
        _treatmentState = TreatmentState();
        _qualificationNoticed = false;
        if (treatment is not null && History is null)
        {
            History = new HistorySectionViewModel(_api, $"api/treatments/{treatment.Id}/history", _zone, "Wer die Behandlung und ihre Preise wann geändert hat");
        }

        if (sectionsChange)
        {
            // Neu angelegt gibt es nur, was ohne gespeicherte Behandlung geht (wie im Web).
            Sections = [.. AllSections.Where(s => s.Id != Material || _catalog.ShowsMaterial)
                .Where(s => !IsNew || s.Id is Basics or Process or Resources || (s.Id == Prices && HasSeveralLocations))
                .Select(s => new TreatmentSection(s.Id, s.Label, SelectSection))];
            SelectSection(SelectedSection);
        }

        UpdateWhoCanDo();
        _applying = false;
        OnPropertyChanged(nameof(Id));
        OnPropertyChanged(nameof(IsNew));
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    /// <summary>Setzt die genannten Teil-Listen auf den gespeicherten Stand; <c>null</c> lässt eine Liste, wie sie ist.</summary>
    private void ApplyLists(List<TreatmentPriceRuleDto>? rules, List<PreparationStepDto>? steps, List<MaterialLine>? materials, List<TreatmentExtraDto>? extras)
    {
        _applying = true;
        if (rules is not null)
        {
            _savedRules = rules;
            Fill(PriceRules, rules.Select(r => new PriceRuleRow(r, _ruleLocations, Changed, Remove)));
            _rulesState = RulesState();
        }

        if (steps is not null)
        {
            _savedSteps = steps;
            Fill(PreparationSteps, steps.Select(s => new PreparationStepRow(s, _stock, Changed, Remove)));
            _stepsState = StepsState();
        }

        if (materials is not null)
        {
            _savedMaterials = materials;
            Fill(Materials, materials.Select(m => new MaterialRow(m, MaterialChoices(), _catalog.StockItems, Changed, Remove)));
            _materialsState = MaterialsState();
        }

        if (extras is not null)
        {
            _savedExtras = extras;
            Fill(Extras, extras.Select(e => new ExtraRow(e, ExtraChoices(), Changed, Remove)));
            _extrasState = ExtrasState();
        }

        _applying = false;
    }

    /// <summary>Setzt „Passt dazu“ auf den gespeicherten Stand; Namen kommen aus den Kassa-Artikeln.</summary>
    private void ApplyRecommended(List<Guid> ids)
    {
        _applying = true;
        _savedRecommended = ids;
        Fill(RecommendedArticles, ids.Select(id => new RecommendedArticleRow(id,
            _articles.FirstOrDefault(a => a.Id == id)?.Name ?? "Artikel nicht mehr an der Kassa", Changed, Remove)));
        _recommendedState = RecommendedState();
        _applying = false;
        UpdateArticleChoices();
    }

    private void RecommendedChanged()
    {
        UpdateArticleChoices();
        Changed();
    }

    private void UpdateArticleChoices()
    {
        ArticleChoices = [.. _articles.Where(a => RecommendedArticles.All(r => r.Id != a.Id))];
        AddRecommendedArticleCommand.NotifyCanExecuteChanged();
    }

    private static void Fill<T>(ObservableCollection<T> list, IEnumerable<T> rows)
    {
        list.Clear();
        foreach (var row in rows)
        {
            list.Add(row);
        }
    }

    private IReadOnlyList<Choice> MaterialChoices() => [new Choice(null, "frei erfasst"), .. _stock.Skip(1)];

    private IReadOnlyList<Choice> ExtraChoices() =>
        [new Choice(null, "Ohne Lager"), .. _catalog.StockItems.Select(s => new Choice(s.Id, $"{s.Name} ({s.Unit})"))];

    /// <summary>Was gespeichert würde; leere Felder heißen „Regel des Standorts“ bzw. „keine Angabe“ wie im Web.</summary>
    private TreatmentInput Input() => new(
        Name.Trim(), (int)(Duration ?? 0), (int)(PreparationMinutes ?? 0), (int)(FollowUpMinutes ?? 0), Price ?? 0,
        [.. Qualifications.Where(q => q.IsChecked).Select(q => q.Id)],
        [.. LocationGroups.SelectMany(g => g.Rooms).Where(r => r.IsChecked).Select(r => r.Id)],
        [.. LocationGroups.SelectMany(g => g.Equipment).Where(e => e.IsChecked).Select(e => e.Id)],
        Category.Trim().Length == 0 ? null : Category.Trim(),
        [.. LocationPrices.Where(p => p.Price is not null).Select(p => new TreatmentLocationPriceDto(p.LocationId, p.Price!.Value))],
        NoShowFeePercent is { } fee ? (int)fee : null,
        DepositPercent is { } deposit ? (int)deposit : null,
        GuestHint.Trim().Length == 0 ? null : GuestHint.Trim(),
        CheckAnamnesis,
        (int)(HandsOnMinutes ?? 0),
        (int)(ProcessingMinutes ?? 0));

    private List<TreatmentPriceRuleDto> Rules() => [.. PriceRules.Select(r => r.ToDto())];

    private List<PreparationStepInput> Steps() => [.. PreparationSteps.Select(s => s.ToInput())];

    private List<MaterialLine> Lines() => [.. Materials.Select(m => m.ToLine())];

    private List<TreatmentExtraInput> ExtraInputs() => [.. Extras.Select(e => e.ToInput())];

    private string TreatmentState() => JsonSerializer.Serialize(Input(), ApiJson.Options);

    private string RulesState() => JsonSerializer.Serialize(Rules(), ApiJson.Options);

    private string StepsState() => JsonSerializer.Serialize(Steps(), ApiJson.Options);

    private string MaterialsState() => JsonSerializer.Serialize(Lines(), ApiJson.Options);

    private string ExtrasState() => JsonSerializer.Serialize(ExtraInputs(), ApiJson.Options);

    private List<Guid> RecommendedIds() => [.. RecommendedArticles.Select(r => r.Id)];

    private string RecommendedState() => string.Join(",", RecommendedIds());
}
