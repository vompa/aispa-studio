using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>Eine Aktion in der Liste: Name, Art, Rabatt mit Bedingung, Standorte und Zustand.</summary>
public sealed record PromotionRow(Guid Id, string Name, string KindText, string Detail, string PlacesText, string PhaseText)
{
    /// <summary>Was der Screenreader für die Zeile liest.</summary>
    public string SpokenName => $"{Name}, {KindText}, {PhaseText}, {Detail}, {PlacesText}";

    public override string ToString() => Name;
}

/// <summary>Ein Standort zum Ankreuzen.</summary>
public sealed partial class PromotionLocationChoice(Guid id, string name) : ObservableObject
{
    public Guid Id { get; } = id;

    public string Name { get; } = name;

    [ObservableProperty]
    public partial bool IsSelected { get; set; }
}

/// <summary>Ein Feld des Wochenrasters schwacher Zeiten zum Abhaken, z. B. „Montag Vormittag“.</summary>
public sealed partial class OffPeakCell(DayOfWeek day, DayPart part, string name) : ObservableObject
{
    public DayOfWeek Day { get; } = day;

    public DayPart Part { get; } = part;

    /// <summary>Wochentag und Tageszeit ausgeschrieben, für Screenreader und Tooltip.</summary>
    public string Name { get; } = name;

    [ObservableProperty]
    public partial bool IsSelected { get; set; }
}

/// <summary>Eine Zeile des Wochenrasters: eine Tageszeit mit ihren sieben Feldern von Montag bis Sonntag.</summary>
public sealed record OffPeakRow(string Label, string Hours, IReadOnlyList<OffPeakCell> Cells);

/// <summary>Eine Behandlung in der Vorschau „So sieht es der Gast“: Preis laut Katalog und Preis mit Rabatt (von der API gerechnet).</summary>
public sealed record PromotionPreviewLine(string Name, string PriceText, string GuestPriceText)
{
    public string SpokenName => $"{Name}: statt {PriceText} nur {GuestPriceText}";
}

/// <summary>Eine Behandlung des Katalogs in der Auswahl.</summary>
public sealed record PromotionTreatmentChoice(Guid Id, string Name)
{
    public override string ToString() => Name;
}

/// <summary>Eine gewählte Behandlung mit ihrem Knopf „Entfernen“.</summary>
public sealed class ChosenTreatment
{
    public ChosenTreatment(Guid id, string name, Action<ChosenTreatment> remove)
    {
        Id = id;
        Name = name;
        RemoveCommand = new RelayCommand(() => remove(this));
    }

    public Guid Id { get; }

    public string Name { get; }

    public string RemoveName => $"{Name} entfernen";

    public IRelayCommand RemoveCommand { get; }
}

/// <summary>
/// Aktionen (R05 Slice 101, ADR 0071 mit Nachtrag vom 17.11.2026), native Maske: Rabatte auf Behandlungen oder Pakete in drei Arten, befristet, kurzfristig frei (Vorlauf in Stunden)
/// und schwache Zeiten (Wochenraster aus Wochentag und Tageszeit). Links die Liste mit „Neue Aktion“ fest oben, Art und Zustand jeder Aktion (Läuft, Geplant, Pausiert, Abgelaufen;
/// archivierte auf Wunsch), rechts das Formular mit Name, Gästetext, Art mit ihren Feldern, Rabatt, Zeitraum, Geltung und Standorten und daneben die Vorschau des Gästepreises
/// aus der API (<c>POST api/promotions/preview</c>). Pausieren und Archivieren stehen im Menü „⋯“; beide und das Zurückholen sind umkehrbar und fragen nicht. Gelöscht wird nie (ADR 0071). Die Rabattrechnung macht die Kassa; die Maske prüft nur vorab dieselben Regeln wie der Server
/// (<c>PromotionService.ValidateAsync</c>), damit der Fehler am Feld steht. Welche Standorte die Person sieht und ob sie „alle Standorte“ anlegen darf, entscheidet die API.
/// </summary>
public sealed partial class PromotionsViewModel(IWorkstationApi api, TimeProvider time) : ViewModelBase, INativeArea, IFocusTarget
{
    public const string NameField = "PromotionNameField";

    public const string GuestTextField = "PromotionGuestTextField";

    public const string ValueField = "PromotionValueField";

    public const string FromField = "PromotionFromField";

    public const string UntilField = "PromotionUntilField";

    public const string LocationsField = "SomeLocationsOption";

    public const string TreatmentsField = "TreatmentPickerField";

    public const string LeadHoursField = "PromotionLeadHoursField";

    public const string SlotsField = "OffPeakOption";

    public const int MaxLeadHours = 168;

    private const int DefaultLeadHours = 24;

    private const int DefaultRuleDays = 365;

    public const int MaxName = 40;

    public const int MaxGuestText = 200;

    private const int DefaultDays = 30;

    private static readonly NumberFormatInfo German = new() { NumberDecimalSeparator = ",", NumberGroupSeparator = ".", NegativeSign = "-" };

    private static readonly string[] Inputs =
    [
        nameof(PromotionName), nameof(GuestText), nameof(IsPercent), nameof(IsAmount), nameof(DiscountValue), nameof(ValidFrom), nameof(ValidUntil), nameof(IsActive),
        nameof(AllLocations), nameof(SomeLocations), nameof(ScopeAll), nameof(ScopeTreatments), nameof(ScopePackages), nameof(IsLimited), nameof(IsLastMinute), nameof(IsOffPeak),
        nameof(LeadHours),
    ];

    private static readonly (DayOfWeek Day, string Short, string Long)[] Week =
    [
        (DayOfWeek.Monday, "Mo", "Montag"), (DayOfWeek.Tuesday, "Di", "Dienstag"), (DayOfWeek.Wednesday, "Mi", "Mittwoch"), (DayOfWeek.Thursday, "Do", "Donnerstag"),
        (DayOfWeek.Friday, "Fr", "Freitag"), (DayOfWeek.Saturday, "Sa", "Samstag"), (DayOfWeek.Sunday, "So", "Sonntag"),
    ];

    private static readonly (DayPart Part, string Name, string Hours)[] Parts =
        [(DayPart.Morning, "Vormittag", "bis 12 Uhr"), (DayPart.Afternoon, "Nachmittag", "12 bis 17 Uhr"), (DayPart.Evening, "Abend", "ab 17 Uhr")];

    private readonly SynchronizationContext? _ui = SynchronizationContext.Current;
    private List<PromotionDto> _all = [];
    private List<LocationDto> _places = [];
    private List<TreatmentDto> _catalog = [];
    private PromotionDto? _current;
    private string? _savedSignature;
    private Task? _loading;
    private Guid? _pendingOpen;
    private bool _applying;
    private bool _switching;
    private bool _reverting;
    private OffPeakRow[]? _grid;
    private int _previewVersion;

    public string Title => "Aktionen";

    public ObservableCollection<PromotionRow> Rows { get; } = [];

    public ObservableCollection<PromotionLocationChoice> Locations { get; } = [];

    /// <summary>Die Behandlungen des Katalogs für die Auswahl. Die Liste bleibt stehen, auch wenn eine gewählt ist: sie zu ändern, während die Auswahl sie noch meldet, wählt dort die nächste.</summary>
    public ObservableCollection<PromotionTreatmentChoice> CatalogTreatments { get; } = [];

    public ObservableCollection<ChosenTreatment> Chosen { get; } = [];

    /// <summary>Kopf des Wochenrasters: Mo bis So.</summary>
    public IReadOnlyList<string> WeekDays { get; } = [.. Week.Select(d => d.Short)];

    /// <summary>Das Wochenraster schwacher Zeiten: drei Zeilen (Vormittag, Nachmittag, Abend) mit je sieben Feldern.</summary>
    public IReadOnlyList<OffPeakRow> OffPeakGrid => _grid ??= BuildGrid();

    /// <summary>Die Vorschau des Gästepreises aus der API; leer, solange sie nicht da ist.</summary>
    public ObservableCollection<PromotionPreviewLine> PreviewLines { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowEmpty))]
    public partial bool IsLoaded { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLoadError))]
    public partial string? LoadError { get; private set; }

    public bool HasLoadError => LoadError is not null;

    /// <summary>Die gewählte Aktion der Liste; ohne Auswahl steht das Formular für eine neue Aktion.</summary>
    [ObservableProperty]
    public partial PromotionRow? SelectedRow { get; set; }

    /// <summary>Archivierte Aktionen stehen nur auf Wunsch in der Liste.</summary>
    [ObservableProperty]
    public partial bool ShowArchived { get; set; }

    public bool ShowEmpty => IsLoaded && Rows.Count == 0;

    // ---------- Formular ----------

    public bool IsCreating => _current is null;

    public string HeadingText => _current?.Name ?? "Neue Aktion";

    public string PhaseText => _current is { } current ? PhaseOf(current) : "";

    public bool HasPhase => _current is not null;

    public bool IsArchived => _current?.IsArchived == true;

    /// <summary>Eine archivierte Aktion lässt sich nicht ändern; erst „Zurückholen“.</summary>
    public bool IsEditable => !IsArchived;

    public bool CanArchive => _current is { IsArchived: false };

    public bool CanUnarchive => IsArchived;

    public bool CanPause => _current is { IsArchived: false };

    public string PauseText => _current is { IsActive: false } ? "Fortsetzen" : "Pausieren";

    public string ValueLabel => IsPercent ? "Rabatt (%)" : $"Rabatt ({Currency})";

    public string ValueHint => IsPercent ? "Prozent vom Preis der Position." : "Fester Betrag je Position.";

    public string ValuePlaceholder => IsPercent ? "z. B. 10" : "z. B. 5,00";

    public bool ShowTreatmentPicker => !ScopeAll;

    /// <summary>Was die gewählte Art bedeutet, eine Zeile unter der Auswahl.</summary>
    public string KindHint => SelectedKind switch
    {
        PromotionKind.LastMinute => "Für Termine, die kurz vor Beginn noch frei sind und kurzfristig gebucht werden.",
        PromotionKind.OffPeak => "Für Termine in ruhigen Zeiten: die Felder im Wochenraster abhaken.",
        _ => "Gilt an jedem Tag des Zeitraums.",
    };

    public string PeriodHint => IsLimited ? "Erster und letzter Tag der Aktion." : "In diesem Zeitraum gilt die Regel; danach läuft sie ab.";

    /// <summary>Pakete gibt es nur bei befristeten Aktionen; die beiden anderen Arten hängen am Termin.</summary>
    public bool ShowPackagesOption => IsLimited;

    public bool HasPreview => PreviewLines.Count > 0 || PreviewExample is not null;

    public string TreatmentHint => ScopePackages ? "Betroffen sind die Pakete (Mehrfachkarten) dieser Behandlungen." : "Aus der Liste eine Behandlung wählen.";

    public bool HasChosen => Chosen.Count > 0 && ShowTreatmentPicker;

    public bool ShowLocationList => SomeLocations;

    [ObservableProperty]
    public partial string PromotionName { get; set; } = "";

    [ObservableProperty]
    public partial string GuestText { get; set; } = "";

    [ObservableProperty]
    public partial bool IsPercent { get; set; } = true;

    [ObservableProperty]
    public partial bool IsAmount { get; set; }

    /// <summary>Der Rabatt als Eingabe, mit Komma oder Punkt; ob Prozent oder Betrag, sagt die Art.</summary>
    [ObservableProperty]
    public partial string DiscountValue { get; set; } = "";

    [ObservableProperty]
    public partial DateOnly? ValidFrom { get; set; }

    [ObservableProperty]
    public partial DateOnly? ValidUntil { get; set; }

    /// <summary>Aktiv oder pausiert; eine pausierte Aktion wird an der Kassa nicht vorgeschlagen.</summary>
    [ObservableProperty]
    public partial bool IsActive { get; set; } = true;

    [ObservableProperty]
    public partial bool AllLocations { get; set; } = true;

    [ObservableProperty]
    public partial bool SomeLocations { get; set; }

    /// <summary>Art „befristet“: gilt an jedem Tag des Zeitraums.</summary>
    [ObservableProperty]
    public partial bool IsLimited { get; set; } = true;

    /// <summary>Art „kurzfristig frei“: für Termine, die höchstens <see cref="LeadHours"/> Stunden vor Beginn gebucht werden.</summary>
    [ObservableProperty]
    public partial bool IsLastMinute { get; set; }

    /// <summary>Art „schwache Zeiten“: für Termine in den abgehakten Feldern des Wochenrasters.</summary>
    [ObservableProperty]
    public partial bool IsOffPeak { get; set; }

    /// <summary>Vorlauf in Stunden als Eingabe (nur bei „kurzfristig frei“).</summary>
    [ObservableProperty]
    public partial string LeadHours { get; set; } = "";

    [ObservableProperty]
    public partial string? LeadHoursError { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSlotsError))]
    public partial string? SlotsError { get; private set; }

    public bool HasSlotsError => SlotsError is not null;

    /// <summary>Beispieltermin der Vorschau, z. B. „Dienstag, 24.11., Vormittag“.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPreview))]
    public partial string? PreviewExample { get; private set; }

    [ObservableProperty]
    public partial string? PreviewNote { get; private set; }

    [ObservableProperty]
    public partial bool ScopeAll { get; set; } = true;

    [ObservableProperty]
    public partial bool ScopeTreatments { get; set; }

    [ObservableProperty]
    public partial bool ScopePackages { get; set; }

    /// <summary>Die Behandlung, die in der Auswahl gewählt wurde; sie wandert sofort in die Liste der gewählten.</summary>
    [ObservableProperty]
    public partial PromotionTreatmentChoice? PickedTreatment { get; set; }

    [ObservableProperty]
    public partial string? NameError { get; private set; }

    [ObservableProperty]
    public partial string? GuestTextError { get; private set; }

    [ObservableProperty]
    public partial string? ValueError { get; private set; }

    [ObservableProperty]
    public partial string? FromError { get; private set; }

    [ObservableProperty]
    public partial string? UntilError { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLocationsError))]
    public partial string? LocationsError { get; private set; }

    public bool HasLocationsError => LocationsError is not null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasTreatmentsError))]
    public partial string? TreatmentsError { get; private set; }

    public bool HasTreatmentsError => TreatmentsError is not null;

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

    /// <summary>Rückmeldung wie „Archiviert.“ über dem Formular.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMessage))]
    public partial string? Message { get; private set; }

    public bool HasMessage => Message is not null;

    public bool HasUnsavedChanges => _savedSignature is not null && Signature() != _savedSignature;

    private string Currency => _places.Count > 0 && _places[0].Currency != "EUR" ? _places[0].Currency : "€";

    private DateOnly Today => DateOnly.FromDateTime(time.GetLocalNow().DateTime);

    // ---------- Laden ----------

    public Task EnsureLoadedAsync() => IsLoaded ? Task.CompletedTask : _loading ??= LoadAsync();

    [RelayCommand]
    public async Task LoadAsync()
    {
        try
        {
            LoadError = null;
            var list = api.GetAsync<List<PromotionDto>>("api/promotions?includeArchived=true");
            var places = api.GetAsync<List<LocationDto>>("api/locations");
            var catalog = api.GetAsync<List<TreatmentDto>>("api/treatments");
            await Task.WhenAll(list, places, catalog).ConfigureAwait(true);
            if (list.Result.Value is not { } promotions || places.Result.Value is not { } locations || catalog.Result.Value is not { } treatments)
            {
                LoadError = list.Result.Error ?? places.Result.Error ?? catalog.Result.Error ?? "Die Aktionen konnten nicht geladen werden.";
                return;
            }

            _places = locations;
            _catalog = treatments;
            CatalogTreatments.Clear();
            foreach (var treatment in treatments)
            {
                CatalogTreatments.Add(new PromotionTreatmentChoice(treatment.Id, treatment.Name));
            }

            _all = promotions;
            IsLoaded = true;
            var wanted = _pendingOpen;
            _pendingOpen = null;
            Rebuild(null);
            if (wanted is { } id)
            {
                ShowTarget(id);
            }
            else
            {
                Select(null);
            }
        }
        finally
        {
            _loading = null;
        }
    }

    /// <summary>Holt die Liste neu und wählt danach die Aktion mit der Kennung (sonst das Formular für eine neue).</summary>
    private async Task<bool> ReloadAsync(Guid? select)
    {
        var result = await api.GetAsync<List<PromotionDto>>("api/promotions?includeArchived=true").ConfigureAwait(true);
        if (result.Value is not { } promotions)
        {
            Error = result.Error ?? "Die Aktionen wurden nicht geladen.";
            return false;
        }

        _all = promotions;
        Rebuild(null);
        Select(select);
        return true;
    }

    public void Open(Guid? id, string? section, string? field)
    {
        if (id is not { } wanted)
        {
            return;
        }

        if (IsLoaded)
        {
            ShowTarget(wanted);
        }
        else
        {
            _pendingOpen = wanted;
        }
    }

    private void ShowTarget(Guid id)
    {
        if (_all.FirstOrDefault(p => p.Id == id) is not { } target)
        {
            return;
        }

        if (target.IsArchived && !ShowArchived)
        {
            ShowArchived = true;
        }

        Select(id);
    }

    private void Rebuild(Guid? keep)
    {
        _switching = true;
        Rows.Clear();
        foreach (var promotion in _all.Where(p => ShowArchived || !p.IsArchived))
        {
            Rows.Add(RowOf(promotion));
        }

        _switching = false;
        OnPropertyChanged(nameof(ShowEmpty));
        if (keep is { } id)
        {
            SelectedRow = Rows.FirstOrDefault(r => r.Id == id);
        }
    }

    private PromotionRow RowOf(PromotionDto promotion) =>
        new(promotion.Id, promotion.Name, KindName(promotion.Kind ?? PromotionKind.Limited), $"{DiscountLabel(promotion)} · {ConditionLabel(promotion)}", PlacesLabel(promotion),
            PhaseOf(promotion));

    private static string KindName(PromotionKind kind) => kind switch
    {
        PromotionKind.LastMinute => "Kurzfristig frei",
        PromotionKind.OffPeak => "Schwache Zeiten",
        _ => "Befristet",
    };

    /// <summary>Wann der Rabatt gilt: Zeitraum, Vorlauf oder die ersten Felder des Rasters.</summary>
    private static string ConditionLabel(PromotionDto promotion)
    {
        switch (promotion.Kind)
        {
            case PromotionKind.LastMinute:
                return $"bis {promotion.LeadHours} Std. vor Beginn gebucht";
            case PromotionKind.OffPeak:
                var slots = (promotion.Slots ?? []).Select(s => $"{Week.First(d => d.Day == s.Day).Short} {Parts.First(p => p.Part == s.Part).Name}").ToList();
                return slots.Count <= 2 ? string.Join(", ", slots) : $"{slots.Count} Felder im Wochenraster";
            default:
                return $"{StockFormat.Date(promotion.ValidFrom)} bis {StockFormat.Date(promotion.ValidUntil)}";
        }
    }

    private string DiscountLabel(PromotionDto promotion) => promotion.DiscountPercent is { } percent
        ? $"{percent.ToString("0.##", German)} %"
        : $"{(promotion.DiscountAmount ?? 0).ToString("#,##0.00", German)} {Currency}";

    private string PlacesLabel(PromotionDto promotion)
    {
        if (promotion.AllLocations)
        {
            return "alle Standorte";
        }

        var names = promotion.LocationIds.Select(id => _places.FirstOrDefault(l => l.Id == id)?.Name ?? "?").ToList();
        return names.Count <= 2 ? string.Join(", ", names) : $"{names.Count} Standorte";
    }

    private static string PhaseOf(PromotionDto promotion) => promotion.IsArchived ? "Archiviert" : promotion.Phase switch
    {
        PromotionPhase.Running => "Läuft",
        PromotionPhase.Planned => "Geplant",
        PromotionPhase.Paused => "Pausiert",
        _ => "Abgelaufen",
    };

    partial void OnShowArchivedChanged(bool value)
    {
        if (!IsLoaded)
        {
            return;
        }

        var keep = _current?.Id;
        var stays = keep is { } id && (value || !_all.First(p => p.Id == id).IsArchived);
        Rebuild(stays ? keep : null);
        if (!stays && keep is not null)
        {
            Select(null);
        }
    }

    // ---------- Auswahl ----------

    partial void OnSelectedRowChanged(PromotionRow? oldValue, PromotionRow? newValue)
    {
        if (_reverting || _switching || newValue is null)
        {
            return;
        }

        if (HasUnsavedChanges)
        {
            // Erst fragen, dann wechseln: die Liste bleibt bei der alten Aktion, bis die Antwort da ist.
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

    /// <summary>Wählt eine Aktion (oder, mit <c>null</c>, die neue) in Liste und Formular.</summary>
    private void Select(Guid? id)
    {
        _switching = true;
        SelectedRow = id is { } wanted ? Rows.FirstOrDefault(r => r.Id == wanted) : null;
        _switching = false;
        Apply(id is { } known ? _all.FirstOrDefault(p => p.Id == known) : null);
    }

    // ---------- Formular füllen ----------

    private void Apply(PromotionDto? promotion)
    {
        _applying = true;
        _current = promotion;
        var single = _places.Count == 1;
        PromotionName = promotion?.Name ?? "";
        GuestText = promotion?.GuestText ?? "";
        IsAmount = promotion?.DiscountAmount is not null;
        IsPercent = !IsAmount;
        DiscountValue = promotion is null ? "" : ((promotion.DiscountAmount ?? promotion.DiscountPercent) ?? 0).ToString("0.##", German);
        ValidFrom = promotion?.ValidFrom ?? Today;
        ValidUntil = promotion?.ValidUntil ?? Today.AddDays(DefaultDays);
        IsActive = promotion?.IsActive ?? true;
        AllLocations = promotion?.AllLocations ?? !single;
        SomeLocations = !AllLocations;
        var kind = promotion?.Kind ?? PromotionKind.Limited;
        IsLimited = kind == PromotionKind.Limited;
        IsLastMinute = kind == PromotionKind.LastMinute;
        IsOffPeak = kind == PromotionKind.OffPeak;
        LeadHours = (promotion?.LeadHours ?? DefaultLeadHours).ToString(CultureInfo.InvariantCulture);
        foreach (var cell in OffPeakGrid.SelectMany(r => r.Cells))
        {
            cell.IsSelected = promotion?.Slots?.Contains(new PromotionSlot(cell.Day, cell.Part)) == true;
        }

        ScopeAll = promotion is null || promotion.Scope == PromotionScope.AllTreatments;
        ScopeTreatments = promotion?.Scope == PromotionScope.SelectedTreatments;
        ScopePackages = promotion?.Scope == PromotionScope.SelectedPackages;
        PickedTreatment = null;
        Locations.Clear();
        foreach (var place in _places)
        {
            var choice = new PromotionLocationChoice(place.Id, place.Name)
            {
                IsSelected = promotion is null ? single : promotion.LocationIds.Contains(place.Id),
            };
            choice.PropertyChanged += OnLocationChanged;
            Locations.Add(choice);
        }

        Chosen.Clear();
        foreach (var id in promotion?.TreatmentIds ?? [])
        {
            Chosen.Add(NewChosen(id, _catalog.FirstOrDefault(t => t.Id == id)?.Name ?? "Behandlung (nicht mehr im Angebot)"));
        }

        _applying = false;
        _savedSignature = Signature();
        ClearErrors();
        StatusText = null;
        Error = null;
        foreach (var name in new[]
                 {
                     nameof(IsCreating), nameof(HeadingText), nameof(PhaseText), nameof(HasPhase), nameof(IsArchived), nameof(IsEditable), nameof(CanArchive), nameof(CanUnarchive), nameof(CanPause),
                     nameof(PauseText), nameof(ValueLabel), nameof(ValueHint), nameof(ValuePlaceholder), nameof(ShowTreatmentPicker), nameof(TreatmentHint), nameof(HasChosen), nameof(ShowLocationList),
                     nameof(HasUnsavedChanges), nameof(KindHint), nameof(PeriodHint), nameof(ShowPackagesOption),
                 })
        {
            OnPropertyChanged(name);
        }

        SaveCommand.NotifyCanExecuteChanged();
        ArchiveCommand.NotifyCanExecuteChanged();
        UnarchiveCommand.NotifyCanExecuteChanged();
        TogglePauseCommand.NotifyCanExecuteChanged();
        _ = RefreshPreviewAsync();
    }

    private OffPeakRow[] BuildGrid() =>
    [
        .. Parts.Select(part => new OffPeakRow(part.Name, part.Hours, [.. Week.Select(day =>
        {
            var cell = new OffPeakCell(day.Day, part.Part, $"{day.Long} {part.Name}");
            cell.PropertyChanged += OnCellChanged;
            return cell;
        })])),
    ];

    private void OnCellChanged(object? sender, PropertyChangedEventArgs e)
    {
        SlotsError = null;
        Touch();
    }

    private ChosenTreatment NewChosen(Guid id, string name) => new(id, name, RemoveTreatment);

    private void RemoveTreatment(ChosenTreatment treatment)
    {
        Chosen.Remove(treatment);
        TreatmentsError = null;
        OnPropertyChanged(nameof(HasChosen));
        Touch();
    }

    partial void OnPickedTreatmentChanged(PromotionTreatmentChoice? value)
    {
        if (value is null || _applying)
        {
            return;
        }

        if (Chosen.All(c => c.Id != value.Id))
        {
            Chosen.Add(NewChosen(value.Id, value.Name));
        }

        PickedTreatment = null;
        TreatmentsError = null;
        OnPropertyChanged(nameof(HasChosen));
        Touch();
    }

    private void OnLocationChanged(object? sender, PropertyChangedEventArgs e)
    {
        LocationsError = null;
        Touch();
    }

    // ---------- Radiogruppen ----------

    private PromotionKind SelectedKind => IsLastMinute ? PromotionKind.LastMinute : IsOffPeak ? PromotionKind.OffPeak : PromotionKind.Limited;

    partial void OnIsLimitedChanged(bool value) => ChooseKind(value, () => IsLastMinute = false, () => IsOffPeak = false);

    partial void OnIsLastMinuteChanged(bool value) => ChooseKind(value, () => IsLimited = false, () => IsOffPeak = false);

    partial void OnIsOffPeakChanged(bool value) => ChooseKind(value, () => IsLimited = false, () => IsLastMinute = false);

    /// <summary>
    /// Eine Art gewählt: die anderen gehen aus. Bei einer neuen Aktion mit unverändertem Zeitraum passt das Ende zur Art (befristet 30 Tage, Regeln ein Jahr); Pakete gibt es nur
    /// befristet, deshalb werden daraus die ausgewählten Behandlungen.
    /// </summary>
    private void ChooseKind(bool chosen, params Action[] others)
    {
        if (!chosen)
        {
            return;
        }

        foreach (var other in others)
        {
            other();
        }

        if (_applying)
        {
            return;
        }

        if (IsCreating && ValidFrom == Today && (ValidUntil == Today.AddDays(DefaultDays) || ValidUntil == Today.AddDays(DefaultRuleDays)))
        {
            ValidUntil = Today.AddDays(IsLimited ? DefaultDays : DefaultRuleDays);
        }

        if (!IsLimited && ScopePackages)
        {
            ScopeTreatments = true;
        }

        foreach (var name in new[] { nameof(KindHint), nameof(PeriodHint), nameof(ShowPackagesOption) })
        {
            OnPropertyChanged(name);
        }
    }

    partial void OnIsPercentChanged(bool value)
    {
        if (IsAmount == value)
        {
            IsAmount = !value;
        }

        OnPropertyChanged(nameof(ValueLabel));
        OnPropertyChanged(nameof(ValueHint));
        OnPropertyChanged(nameof(ValuePlaceholder));
    }

    partial void OnIsAmountChanged(bool value)
    {
        if (IsPercent == value)
        {
            IsPercent = !value;
        }
    }

    partial void OnAllLocationsChanged(bool value)
    {
        if (SomeLocations == value)
        {
            SomeLocations = !value;
        }
    }

    partial void OnSomeLocationsChanged(bool value)
    {
        if (AllLocations == value)
        {
            AllLocations = !value;
        }

        OnPropertyChanged(nameof(ShowLocationList));
    }

    partial void OnScopeAllChanged(bool value) => ChooseScope(value, () => ScopeTreatments = false, () => ScopePackages = false);

    partial void OnScopeTreatmentsChanged(bool value) => ChooseScope(value, () => ScopeAll = false, () => ScopePackages = false);

    partial void OnScopePackagesChanged(bool value) => ChooseScope(value, () => ScopeAll = false, () => ScopeTreatments = false);

    private void ChooseScope(bool chosen, params Action[] others)
    {
        if (chosen)
        {
            foreach (var other in others)
            {
                other();
            }
        }

        OnPropertyChanged(nameof(ShowTreatmentPicker));
        OnPropertyChanged(nameof(TreatmentHint));
        OnPropertyChanged(nameof(HasChosen));
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.PropertyName is not { } name || !Inputs.Contains(name))
        {
            return;
        }

        switch (name)
        {
            case nameof(PromotionName):
                NameError = null;
                break;
            case nameof(GuestText):
                GuestTextError = null;
                break;
            case nameof(IsPercent) or nameof(IsAmount) or nameof(DiscountValue):
                ValueError = null;
                break;
            case nameof(ValidFrom):
                FromError = null;
                UntilError = null;
                break;
            case nameof(ValidUntil):
                UntilError = null;
                break;
            case nameof(AllLocations) or nameof(SomeLocations):
                LocationsError = null;
                break;
            case nameof(ScopeAll) or nameof(ScopeTreatments) or nameof(ScopePackages):
                TreatmentsError = null;
                break;
            case nameof(LeadHours):
                LeadHoursError = null;
                break;
            case nameof(IsLimited) or nameof(IsLastMinute) or nameof(IsOffPeak):
                LeadHoursError = null;
                SlotsError = null;
                break;
        }

        Touch();
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
        _ = RefreshPreviewAsync();
    }

    private void ClearErrors() => NameError = GuestTextError = ValueError = FromError = UntilError = LocationsError = TreatmentsError = LeadHoursError = SlotsError = null;

    private IReadOnlyList<PromotionSlot> SelectedSlots => [.. OffPeakGrid.SelectMany(r => r.Cells).Where(c => c.IsSelected).Select(c => new PromotionSlot(c.Day, c.Part))];

    private int? LeadHoursValue => int.TryParse(LeadHours.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var hours) ? hours : null;

    private PromotionScope Scope => ScopeTreatments ? PromotionScope.SelectedTreatments : ScopePackages ? PromotionScope.SelectedPackages : PromotionScope.AllTreatments;

    private string Signature() => string.Join('\u001f', PromotionName, GuestText, IsPercent, DiscountValue, ValidFrom, ValidUntil, IsActive, AllLocations,
        string.Join(',', Locations.Where(l => l.IsSelected).Select(l => l.Id)), Scope, string.Join(',', Chosen.Select(c => c.Id)), SelectedKind,
        IsLastMinute ? LeadHours : "", IsOffPeak ? string.Join(',', SelectedSlots) : "");

    // ---------- Prüfen und Speichern ----------

    /// <summary>Dieselben Regeln wie am Server; der erste Fehler bekommt den Fokus. true, wenn nichts zu beanstanden ist.</summary>
    private bool Validate()
    {
        NameError = PromotionName.Trim().Length is 0 or > MaxName ? $"Name mit höchstens {MaxName} Zeichen angeben." : null;
        GuestTextError = GuestText.Trim().Length is 0 or > MaxGuestText ? $"Gästetext mit höchstens {MaxGuestText} Zeichen angeben." : null;
        var value = StockFormat.Parse(DiscountValue);
        var twoDecimals = value is { } v && Math.Round(v, 2) == v;
        ValueError = IsPercent
            ? value is > 0 and < 100 && twoDecimals ? null : "Rabatt in Prozent zwischen 0,01 und 99,99."
            : value is > 0 and < 100000 && twoDecimals ? null : "Rabatt als Betrag größer als 0 mit höchstens zwei Nachkommastellen.";
        FromError = ValidFrom is null ? "Trage den Beginn ein." : null;
        UntilError = ValidUntil is null ? "Trage das Ende ein." : ValidFrom is { } from && ValidUntil < from ? "Das Ende darf nicht vor dem Beginn liegen." : null;
        LocationsError = !AllLocations && Locations.All(l => !l.IsSelected) ? "Mindestens einen Standort wählen." : null;
        TreatmentsError = !ScopeAll && Chosen.Count == 0 ? "Mindestens eine Behandlung wählen." : null;
        LeadHoursError = IsLastMinute && LeadHoursValue is not (>= 1 and <= MaxLeadHours) ? $"Vorlauf in Stunden zwischen 1 und {MaxLeadHours} angeben." : null;
        SlotsError = IsOffPeak && SelectedSlots.Count == 0 ? "Mindestens ein Feld im Wochenraster wählen." : null;
        var firstInvalid = NameError is not null ? NameField
            : GuestTextError is not null ? GuestTextField
            : LeadHoursError is not null ? LeadHoursField
            : SlotsError is not null ? SlotsField
            : ValueError is not null ? ValueField
            : FromError is not null ? FromField
            : UntilError is not null ? UntilField
            : LocationsError is not null ? LocationsField
            : TreatmentsError is not null ? TreatmentsField
            : null;
        // Die Ansicht leert FocusTarget, sobald sie den Fokus gesetzt hat; das Ergebnis darf nicht davon abhängen.
        FocusTarget = firstInvalid;
        return firstInvalid is null;
    }

    private PromotionInput InputOf()
    {
        var value = StockFormat.Parse(DiscountValue);
        return new PromotionInput(PromotionName.Trim(), GuestText.Trim(), ValidFrom!.Value, ValidUntil!.Value, AllLocations,
            AllLocations ? [] : [.. Locations.Where(l => l.IsSelected).Select(l => l.Id)], IsPercent ? value : null, IsAmount ? value : null, Scope,
            ScopeAll ? [] : [.. Chosen.Select(c => c.Id)], IsActive)
        {
            Kind = SelectedKind,
            LeadHours = IsLastMinute ? LeadHoursValue : null,
            Slots = IsOffPeak ? SelectedSlots : [],
        };
    }

    // ---------- Vorschau des Gästepreises ----------

    /// <summary>
    /// Holt die Vorschau „So sieht es der Gast“ für den Stand des Formulars (auch ungespeichert); den Gästepreis rechnet die API wie die Kassa. Eine ältere Antwort, die nach einer
    /// neueren Eingabe eintrifft, wird verworfen. Ohne Antwort bleibt die Vorschau leer; Speichern hängt nicht davon ab.
    /// </summary>
    private async Task RefreshPreviewAsync()
    {
        var version = ++_previewVersion;
        var value = StockFormat.Parse(DiscountValue);
        var body = new PromotionInput(PromotionName.Trim(), GuestText.Trim(), ValidFrom ?? Today, ValidUntil ?? ValidFrom ?? Today, AllLocations, [], IsPercent ? value : null,
            IsAmount ? value : null, Scope, ScopeAll ? [] : [.. Chosen.Select(c => c.Id)], IsActive)
        {
            Kind = SelectedKind,
            LeadHours = IsLastMinute ? LeadHoursValue : null,
            Slots = IsOffPeak ? SelectedSlots : [],
        };
        var result = await api.PostAsync<PromotionPreviewDto>("api/promotions/preview", body).ConfigureAwait(true);
        if (version != _previewVersion)
        {
            return;
        }

        PreviewLines.Clear();
        foreach (var line in result.Value?.Lines ?? [])
        {
            PreviewLines.Add(new PromotionPreviewLine(line.Name, Money(line.Price), Money(line.GuestPrice)));
        }

        PreviewExample = result.Value?.Example;
        PreviewNote = result.Value?.Note;
        OnPropertyChanged(nameof(HasPreview));
    }

    private string Money(decimal value) => $"{value.ToString("#,##0.00", German)} {Currency}";

    private bool CanSave => !IsSaving && IsEditable;

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync() => await SaveChangesAsync().ConfigureAwait(true);

    public async Task<bool> SaveChangesAsync()
    {
        if (IsSaving || !IsEditable || !Validate())
        {
            return false;
        }

        Error = null;
        IsSaving = true;
        var body = InputOf();
        var result = _current is { } promotion
            ? await api.PutAsync<PromotionDto>($"api/promotions/{promotion.Id}", body).ConfigureAwait(true)
            : await api.PostAsync<PromotionDto>("api/promotions", body).ConfigureAwait(true);
        IsSaving = false;
        if (result.Value is not { } saved)
        {
            Error = result.Error ?? "Die Aktion wurde nicht gespeichert.";
            return false;
        }

        Message = null;
        if (!await ReloadAsync(saved.Id).ConfigureAwait(true))
        {
            // Gespeichert ist gespeichert: die Aktion gilt mit ihrer Kennung weiter, sonst legt ein zweites Speichern eine neue an.
            var reloadError = Error;
            var at = _all.FindIndex(p => p.Id == saved.Id);
            if (at >= 0)
            {
                _all[at] = saved;
            }
            else
            {
                _all.Add(saved);
            }

            Rebuild(null);
            Select(saved.Id);
            Error = reloadError;
        }

        StatusText = "Gespeichert.";
        return true;
    }

    [RelayCommand]
    public void Discard()
    {
        Apply(_current);
        Error = null;
    }

    /// <summary>Eine neue Aktion; vorher fragt die Maske, wenn etwas ungespeichert ist.</summary>
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

    // ---------- Pausieren, Archivieren, Zurückholen ----------

    /// <summary>Pausieren und Fortsetzen speichern gleich den gespeicherten Stand der Aktion mit dem anderen Schalter; ungespeicherte Änderungen fragt die Maske vorher ab.</summary>
    [RelayCommand(CanExecute = nameof(CanPause))]
    private async Task TogglePauseAsync()
    {
        if (!await UnsavedChanges.ConfirmLeaveAsync(Dialogs, this).ConfigureAwait(true) || _current is not { IsArchived: false } promotion)
        {
            return;
        }

        var resume = !promotion.IsActive;
        var body = new PromotionInput(promotion.Name, promotion.GuestText, promotion.ValidFrom, promotion.ValidUntil, promotion.AllLocations, promotion.LocationIds, promotion.DiscountPercent,
            promotion.DiscountAmount, promotion.Scope, promotion.TreatmentIds, resume)
        {
            Kind = promotion.Kind,
            LeadHours = promotion.LeadHours,
            Slots = promotion.Slots,
        };
        await ChangeAsync(api.PutAsync<PromotionDto>($"api/promotions/{promotion.Id}", body), resume ? "Fortgesetzt." : "Pausiert.", false).ConfigureAwait(true);
    }

    /// <summary>Archivieren löscht nichts und lässt sich zurücknehmen; deshalb ohne Rückfrage.</summary>
    [RelayCommand(CanExecute = nameof(CanArchive))]
    private async Task ArchiveAsync()
    {
        if (!await UnsavedChanges.ConfirmLeaveAsync(Dialogs, this).ConfigureAwait(true) || _current is not { IsArchived: false } promotion)
        {
            return;
        }

        await ChangeAsync(api.PostAsync<PromotionDto>($"api/promotions/{promotion.Id}/archive", null), "Archiviert.", true).ConfigureAwait(true);
    }

    [RelayCommand(CanExecute = nameof(CanUnarchive))]
    private async Task UnarchiveAsync()
    {
        if (_current is not { IsArchived: true } promotion)
        {
            return;
        }

        await ChangeAsync(api.PostAsync<PromotionDto>($"api/promotions/{promotion.Id}/unarchive", null), "Zurückgeholt.", false).ConfigureAwait(true);
    }

    private async Task ChangeAsync(Task<ApiResult<PromotionDto>> call, string done, bool leave)
    {
        var result = await call.ConfigureAwait(true);
        if (result.Value is not { } changed)
        {
            Error = result.Error ?? "Die Aktion wurde nicht geändert.";
            return;
        }

        Error = null;
        Message = null;
        // Eine archivierte Aktion verlässt die Liste, solange Archivierte nicht angezeigt werden.
        await ReloadAsync(leave && !ShowArchived ? null : changed.Id).ConfigureAwait(true);
        Message = done;
    }

    [RelayCommand]
    private void CloseError() => Error = null;

    [RelayCommand]
    private void CloseMessage() => Message = null;
}
