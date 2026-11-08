using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>Ein Monat in der Auswahl: <c>Value</c> ist die Form der API (JJJJ-MM).</summary>
public sealed record CostMonthChoice(string Value, string Name)
{
    public override string ToString() => Name;
}

/// <summary>Eine Behandlung in der Vollkosten-Tabelle; alle Beträge kommen fertig gerechnet von der API.</summary>
public sealed partial class CostRow : ObservableObject
{
    public CostRow(TreatmentCostDto cost, bool canOpenTreatment, Action<Guid> openTreatment)
    {
        TreatmentId = cost.TreatmentId;
        Name = cost.TreatmentName;
        NetPrice = Money.Format(cost.NetPrice);
        Material = Money.Format(cost.Material);
        Staff = Money.Format(cost.Staff);
        Room = Money.Format(cost.Room);
        Cleaning = Money.Format(cost.Cleaning);
        Overhead = Money.Format(cost.Overhead);
        Total = Money.Format(cost.Total);
        Margin = $"{Money.Format(cost.Margin)} ({cost.MarginPercent.ToString("0.##", CostsViewModel.German)} %)";
        IsMarginNegative = cost.Margin < 0;
        Appointments = cost.Appointments.ToString(CultureInfo.InvariantCulture);
        Contribution = Money.Format(cost.ContributionTotal);
        IsContributionNegative = cost.ContributionTotal < 0;
        MissingText = string.Join(' ', cost.Missing);
        Parts = [.. cost.Parts.Select(p => $"{p.Kind}: {Money.Format(p.Amount)} ({p.Basis})")];
        CanOpenTreatment = canOpenTreatment;
        OpenTreatmentCommand = new RelayCommand(() => openTreatment(TreatmentId));
    }

    public Guid TreatmentId { get; }

    public string Name { get; }

    public string NetPrice { get; }

    public string Material { get; }

    public string Staff { get; }

    public string Room { get; }

    public string Cleaning { get; }

    public string Overhead { get; }

    public string Total { get; }

    public string Margin { get; }

    public bool IsMarginNegative { get; }

    public string Appointments { get; }

    public string Contribution { get; }

    public bool IsContributionNegative { get; }

    public string MissingText { get; }

    public bool HasMissing => MissingText.Length > 0;

    public IReadOnlyList<string> Parts { get; }

    public bool CanOpenTreatment { get; }

    public IRelayCommand OpenTreatmentCommand { get; }

    public string ToggleName => $"Herleitung {Name}";

    public string SpokenName => $"{Name}, Preis netto {NetPrice}, Vollkosten {Total}, Marge {Margin}, {Appointments} Termine, Deckungsbeitrag {Contribution}";

    /// <summary>Die Herleitung der Beträge ist aufgeklappt.</summary>
    [ObservableProperty]
    public partial bool IsOpen { get; set; }
}

/// <summary>Miete und Pflegematerial eines Raums.</summary>
public sealed partial class RoomCostRow(RoomCostDto room) : ObservableObject
{
    public Guid RoomId { get; } = room.RoomId;

    public string Name { get; } = room.RoomName;

    public string MonthlyName => $"Miete {Name}";

    public string MaterialName => $"Pflegematerial {Name}";

    [ObservableProperty]
    public partial decimal? MonthlyCost { get; set; } = room.MonthlyCost;

    [ObservableProperty]
    public partial decimal? MaterialCost { get; set; } = room.CleaningMaterialCost;

    public bool IsChanged => (MonthlyCost ?? 0) != room.MonthlyCost || (MaterialCost ?? 0) != room.CleaningMaterialCost;
}

/// <summary>Stundensatz einer Person; leer heißt Standardsatz des Standorts.</summary>
public sealed partial class EmployeeCostRow(EmployeeCostDto employee) : ObservableObject
{
    public Guid EmployeeId { get; } = employee.EmployeeId;

    public string Name { get; } = employee.EmployeeName;

    public string HourlyName => $"Stundensatz {Name}";

    [ObservableProperty]
    public partial decimal? HourlyCost { get; set; } = employee.HourlyCost;

    public bool IsChanged => HourlyCost != employee.HourlyCost;
}

/// <summary>
/// Kosten (R05 Slice 102), native Maske nach <c>CostsPage.tsx</c>: Vollkosten je Behandlung für Standort und Monat, darunter die Kostensätze, Räume und Stundensätze des Standorts. Alle Beträge
/// rechnet die API. Das Web speichert jeden Raum und jede Person einzeln; hier gilt eine Leiste „Speichern“ für alle Änderungen (Brief 5.27). Ändern darf nur, wer den Katalog pflegt (ManageCatalog).
/// </summary>
public sealed partial class CostsViewModel(IWorkstationApi api, CurrentUser user, TimeProvider time) : ViewModelBase, INativeArea, IFocusTarget
{
    public const string UtilizationField = "PlannedUtilizationField";

    public const string ProductivityField = "StaffProductivityField";

    internal static readonly NumberFormatInfo German = new() { NumberDecimalSeparator = ",", NumberGroupSeparator = "." };

    private const int MonthsBack = 24;

    private static readonly CultureInfo GermanCulture = CultureInfo.GetCultureInfo("de-AT");

    private readonly SynchronizationContext? _ui = SynchronizationContext.Current;
    private LocationCostSettingsDto? _settings;
    private Task? _loading;
    private bool _applying;
    private bool _reverting;
    private int _version;

    private int _analysisVersion;

    /// <summary>„Material pflegen“: die Seite springt zur Behandlung.</summary>
    public event Action<Guid>? TreatmentRequested;

    public string Title => "Kosten";

    public bool CanEdit => user.Rights.Contains(AccessRight.ManageCatalog);

    public bool ShowReadOnlyHint => !CanEdit;

    public ObservableCollection<LocationDto> Locations { get; } = [];

    public ObservableCollection<CostMonthChoice> Months { get; } = [];

    public ObservableCollection<CostRow> Rows { get; } = [];

    public ObservableCollection<RoomCostRow> Rooms { get; } = [];

    public ObservableCollection<EmployeeCostRow> Employees { get; } = [];

    [ObservableProperty]
    public partial bool IsLoaded { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLoadError))]
    public partial string? LoadError { get; private set; }

    public bool HasLoadError => LoadError is not null;

    [ObservableProperty]
    public partial LocationDto? SelectedLocation { get; set; }

    [ObservableProperty]
    public partial CostMonthChoice? SelectedMonth { get; set; }

    [ObservableProperty]
    public partial string CapacityText { get; private set; } = "";

    public bool ShowEmpty => IsLoaded && Rows.Count == 0;

    public bool ShowTable => Rows.Count > 0;

    [ObservableProperty]
    public partial decimal? CleaningHourly { get; set; }

    [ObservableProperty]
    public partial decimal? DefaultStaffHourly { get; set; }

    [ObservableProperty]
    public partial decimal? OtherMonthly { get; set; }

    [ObservableProperty]
    public partial decimal? PlannedUtilization { get; set; }

    [ObservableProperty]
    public partial decimal? StaffProductivity { get; set; }

    [ObservableProperty]
    public partial string? UtilizationError { get; private set; }

    [ObservableProperty]
    public partial string? ProductivityError { get; private set; }

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

    public bool HasUnsavedChanges => CanEdit && _settings is not null && (SettingsChanged || Rooms.Any(r => r.IsChanged) || Employees.Any(e => e.IsChanged));

    private bool SettingsChanged => _settings is { } settings && InputOf(settings) != settings;

    private static LocationCostSettingsDto ToDto(decimal? cleaning, decimal? staff, decimal? other, decimal? utilization, decimal? productivity, LocationCostSettingsDto saved) =>
        new(cleaning ?? 0, staff ?? 0, other ?? 0, utilization is { } u ? (int)u : saved.PlannedUtilizationPercent, productivity is { } p ? (int)p : saved.StaffProductivityPercent);

    private LocationCostSettingsDto InputOf(LocationCostSettingsDto saved) => ToDto(CleaningHourly, DefaultStaffHourly, OtherMonthly, PlannedUtilization, StaffProductivity, saved);

    // ---------- Laden ----------

    public Task EnsureLoadedAsync() => IsLoaded ? Task.CompletedTask : _loading ??= LoadAsync();

    [RelayCommand]
    public async Task LoadAsync()
    {
        try
        {
            LoadError = null;
            var places = await api.GetAsync<List<LocationDto>>("api/locations").ConfigureAwait(true);
            if (places.Value is not { } locations)
            {
                LoadError = places.Error ?? "Die Kosten konnten nicht geladen werden.";
                return;
            }

            _reverting = true;
            Locations.Clear();
            foreach (var location in locations)
            {
                Locations.Add(location);
            }

            FillMonths();
            SelectedLocation = Locations.FirstOrDefault();
            SelectedMonth = Months.FirstOrDefault();
            _reverting = false;
            if (await LoadLocationAsync().ConfigureAwait(true))
            {
                IsLoaded = true;
                OnPropertyChanged(nameof(ShowEmpty));
            }
        }
        finally
        {
            _loading = null;
        }
    }

    private void FillMonths()
    {
        var now = time.GetLocalNow();
        var month = new DateOnly(now.Year, now.Month, 1);
        Months.Clear();
        for (var back = 0; back < MonthsBack; back++)
        {
            var shown = month.AddMonths(-back);
            Months.Add(new CostMonthChoice(shown.ToString("yyyy-MM", CultureInfo.InvariantCulture), shown.ToString("MMMM yyyy", GermanCulture)));
        }
    }

    /// <summary>Analyse, Sätze, Räume und Stundensätze des gewählten Standorts; eine überholte Antwort gilt nicht mehr.</summary>
    private async Task<bool> LoadLocationAsync()
    {
        if (SelectedLocation is not { } location || SelectedMonth is not { } month)
        {
            return true;
        }

        // Ab hier gelten Sätze und Eingaben nicht mehr: Speichern bleibt gesperrt, bis die des gewählten Standorts da sind.
        _settings = null;
        var version = ++_version;
        var analysisVersion = ++_analysisVersion;
        var analysis = api.GetAsync<CostAnalysisDto>($"api/costs/locations/{location.Id}/analysis?month={month.Value}");
        var settings = api.GetAsync<LocationCostSettingsDto>($"api/costs/locations/{location.Id}/settings");
        var rooms = api.GetAsync<List<RoomCostDto>>($"api/costs/locations/{location.Id}/rooms");
        var employees = api.GetAsync<List<EmployeeCostDto>>($"api/costs/locations/{location.Id}/employees");
        await Task.WhenAll(analysis, settings, rooms, employees).ConfigureAwait(true);
        if (version != _version)
        {
            return false;
        }

        if (analysis.Result.Value is not { } report || settings.Result.Value is not { } rates || rooms.Result.Value is not { } roomList || employees.Result.Value is not { } staff)
        {
            LoadError = analysis.Result.Error ?? settings.Result.Error ?? rooms.Result.Error ?? employees.Result.Error ?? "Die Kosten konnten nicht geladen werden.";
            ClearShown();
            return false;
        }

        LoadError = null;
        if (analysisVersion == _analysisVersion)
        {
            ApplyAnalysis(report);
        }

        ApplyRates(rates, roomList, staff);
        return true;
    }

    /// <summary>Nach einem Ladefehler stehen keine Zahlen des vorigen Standorts oder Monats mehr unter der neuen Auswahl.</summary>
    private void ClearShown()
    {
        Rows.Clear();
        Rooms.Clear();
        Employees.Clear();
        OnPropertyChanged(nameof(ShowEmpty));
        OnPropertyChanged(nameof(ShowTable));
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    private void ApplyAnalysis(CostAnalysisDto report)
    {
        CapacityText = $"{Math.Round(report.CapacityMinutesPerRoom / 60, MidpointRounding.AwayFromZero).ToString("0", GermanCulture)} Öffnungsstunden je Raum im Monat. Beträge netto je Termin; Termine und Deckungsbeitrag für den gewählten Monat.";
        Rows.Clear();
        foreach (var treatment in report.Treatments)
        {
            Rows.Add(new CostRow(treatment, CanEdit, id => TreatmentRequested?.Invoke(id)));
        }

        OnPropertyChanged(nameof(ShowEmpty));
        OnPropertyChanged(nameof(ShowTable));
    }

    private void ApplyRates(LocationCostSettingsDto rates, List<RoomCostDto> rooms, List<EmployeeCostDto> staff)
    {
        _applying = true;
        _settings = rates;
        CleaningHourly = rates.CleaningHourlyCost;
        DefaultStaffHourly = rates.DefaultStaffHourlyCost;
        OtherMonthly = rates.OtherMonthlyCosts;
        PlannedUtilization = rates.PlannedUtilizationPercent;
        StaffProductivity = rates.StaffProductivityPercent;
        Rooms.Clear();
        foreach (var room in rooms)
        {
            var row = new RoomCostRow(room);
            row.PropertyChanged += OnRowChanged;
            Rooms.Add(row);
        }

        Employees.Clear();
        foreach (var employee in staff)
        {
            var row = new EmployeeCostRow(employee);
            row.PropertyChanged += OnRowChanged;
            Employees.Add(row);
        }

        _applying = false;
        UtilizationError = ProductivityError = null;
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    public void Open(Guid? id, string? section, string? field)
    {
    }

    private void OnRowChanged(object? sender, PropertyChangedEventArgs e) => Touch();

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        switch (e.PropertyName)
        {
            case nameof(PlannedUtilization):
                UtilizationError = null;
                Touch();
                break;
            case nameof(StaffProductivity):
                ProductivityError = null;
                Touch();
                break;
            case nameof(CleaningHourly) or nameof(DefaultStaffHourly) or nameof(OtherMonthly):
                Touch();
                break;
        }
    }

    private void Touch()
    {
        if (_applying)
        {
            return;
        }

        StatusText = null;
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    // ---------- Auswahl ----------

    partial void OnSelectedLocationChanged(LocationDto? oldValue, LocationDto? newValue)
    {
        if (_reverting || newValue is null)
        {
            return;
        }

        if (HasUnsavedChanges)
        {
            // Erst fragen, dann wechseln: die Auswahl bleibt beim alten Standort, bis die Antwort da ist.
            Revert(oldValue);
            _ = LeaveToLocationAsync(newValue);
            return;
        }

        _ = LoadLocationAsync();
    }

    private async Task LeaveToLocationAsync(LocationDto target)
    {
        if (await UnsavedChanges.ConfirmLeaveAsync(Dialogs, this).ConfigureAwait(true))
        {
            SelectedLocation = target;
        }
    }

    private void Revert(LocationDto? old)
    {
        void Restore()
        {
            _reverting = true;
            SelectedLocation = old;
            _reverting = false;
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

    /// <summary>Der Monat ändert nur die Analyse; Sätze und Eingaben bleiben stehen.</summary>
    partial void OnSelectedMonthChanged(CostMonthChoice? value)
    {
        if (!_reverting && value is not null)
        {
            _ = LoadAnalysisAsync();
        }
    }

    private async Task LoadAnalysisAsync()
    {
        if (SelectedLocation is not { } location || SelectedMonth is not { } month)
        {
            return;
        }

        var version = ++_analysisVersion;
        var result = await api.GetAsync<CostAnalysisDto>($"api/costs/locations/{location.Id}/analysis?month={month.Value}").ConfigureAwait(true);
        if (version != _analysisVersion)
        {
            return;
        }

        if (result.Value is { } report)
        {
            LoadError = null;
            ApplyAnalysis(report);
        }
        else
        {
            LoadError = result.Error ?? "Die Kosten konnten nicht geladen werden.";
            Rows.Clear();
            OnPropertyChanged(nameof(ShowEmpty));
            OnPropertyChanged(nameof(ShowTable));
        }
    }

    // ---------- Speichern ----------

    private bool Validate()
    {
        UtilizationError = PlannedUtilization is >= 1 and <= 100 ? null : "Die Raumauslastung liegt zwischen 1 und 100 %.";
        ProductivityError = StaffProductivity is >= 1 and <= 100 ? null : "Die produktive Arbeitszeit liegt zwischen 1 und 100 %.";
        FocusTarget = UtilizationError is not null ? UtilizationField : ProductivityError is not null ? ProductivityField : null;
        return FocusTarget is null;
    }

    private bool CanSave => !IsSaving && CanEdit;

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync() => await SaveChangesAsync().ConfigureAwait(true);

    public async Task<bool> SaveChangesAsync()
    {
        if (IsSaving || !CanEdit || _settings is not { } saved || SelectedLocation is not { } location || !Validate())
        {
            return false;
        }

        Error = null;
        IsSaving = true;
        var problem = await SendChangesAsync(location, saved).ConfigureAwait(true);
        IsSaving = false;
        // Nach einem Fehler bleiben die Eingaben stehen, damit sie sich noch einmal senden lassen.
        if (problem is not null)
        {
            Error = problem;
            return false;
        }

        await LoadLocationAsync().ConfigureAwait(true);
        StatusText = "Gespeichert.";
        return true;
    }

    /// <summary>Sendet Sätze, Räume und Stundensätze, die sich geändert haben; der erste Fehler bricht ab und kommt als Text zurück.</summary>
    private async Task<string?> SendChangesAsync(LocationDto location, LocationCostSettingsDto saved)
    {
        if (SettingsChanged)
        {
            var result = await api.PutAsync<LocationCostSettingsDto>($"api/costs/locations/{location.Id}/settings", InputOf(saved)).ConfigureAwait(true);
            if (result.Value is null)
            {
                return result.Error ?? "Die Kostensätze wurden nicht gespeichert.";
            }
        }

        foreach (var room in Rooms.Where(r => r.IsChanged).ToList())
        {
            var result = await api.PutAsync<RoomCostDto>($"api/costs/rooms/{room.RoomId}", new RoomCostInput(room.MonthlyCost ?? 0, room.MaterialCost ?? 0)).ConfigureAwait(true);
            if (result.Value is null)
            {
                return result.Error ?? $"{room.Name} wurde nicht gespeichert.";
            }
        }

        foreach (var employee in Employees.Where(e => e.IsChanged).ToList())
        {
            var result = await api.PutAsync<EmployeeCostDto>($"api/costs/employees/{employee.EmployeeId}", new EmployeeCostInput(employee.HourlyCost)).ConfigureAwait(true);
            if (result.Value is null)
            {
                return result.Error ?? $"Der Stundensatz von {employee.Name} wurde nicht gespeichert.";
            }
        }

        return null;
    }

    [RelayCommand]
    public void Discard()
    {
        _ = LoadLocationAsync();
        Error = null;
    }

    [RelayCommand]
    private void CloseError() => Error = null;
}
