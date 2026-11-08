using AispaStudio.Contracts;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>
/// Reiter „Lohn“ der Personenkarte (R05 Slice 100, Ä191, Ä250): die Personalnummer der Lohnverrechnung (gespeichert mit der Leiste der Karte über <c>api/payroll/employees/{id}</c>)
/// und die Monatswerte der Person aus der Vorschau der API. Eine Provision kennt die API nicht und gibt es hier deshalb nicht (Befund Ä220). Sehen darf, wer die Organisation verwaltet
/// oder Personalunterlagen liest; ändern nur mit ManageOrganization (wie die API).
/// </summary>
public sealed partial class PersonPayrollViewModel : ViewModelBase
{
    private readonly TeamContext _context;
    private readonly Guid _employeeId;
    private readonly Action _changed;
    private string _saved = "";
    private bool _loaded;
    private int _version;

    public PersonPayrollViewModel(TeamContext context, Guid employeeId, Action changed)
    {
        _context = context;
        _employeeId = employeeId;
        _changed = changed;
        Months = PayrollMonth.Last(context.Today);
        SelectedMonth = PayrollMonth.Previous(context.Today);
    }

    public bool IsEditable => _context.CanManagePayroll;

    /// <summary>Das Feld ist beschreibbar, sobald die Nummer gelesen ist.</summary>
    public bool CanEditNumber => IsEditable && IsLoaded;

    [CommunityToolkit.Mvvm.Input.RelayCommand]
    private Task ReloadAsync() => LoadAsync();

    public IReadOnlyList<PayrollMonth> Months { get; }

    [ObservableProperty]
    public partial PayrollMonth SelectedMonth { get; set; }

    /// <summary>Personalnummer der Lohnverrechnung; leer heißt: keine.</summary>
    [ObservableProperty]
    public partial string PersonnelNumber { get; set; } = "";

    [ObservableProperty]
    public partial IReadOnlyList<PayrollRow> Rows { get; private set; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsEmpty), nameof(CanEditNumber))]
    public partial bool IsLoaded { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLoadError), nameof(ShowsEmpty))]
    public partial string? LoadError { get; private set; }

    public bool HasLoadError => LoadError is not null;

    public bool ShowsEmpty => IsLoaded && Rows.Count == 0 && !HasLoadError;

    public bool HasUnsavedChanges => IsLoaded && IsEditable && PersonnelNumber.Trim() != _saved;

    partial void OnPersonnelNumberChanged(string value) => _changed();

    partial void OnSelectedMonthChanged(PayrollMonth value)
    {
        if (_loaded)
        {
            _ = LoadRowsAsync();
        }
    }

    public Task EnsureLoadedAsync() => _loaded ? Task.CompletedTask : LoadAsync();

    public async Task LoadAsync()
    {
        _loaded = true;
        LoadError = null;
        var numbers = await _context.Api.GetAsync<List<PayrollEmployeeDto>>("api/payroll/employees").ConfigureAwait(true);
        if (numbers.Value is null)
        {
            // Ohne gelesene Nummer bleibt das Feld gesperrt: Speichern würde eine vorhandene Nummer überschreiben, die niemand gesehen hat.
            LoadError = numbers.Error ?? "Die Lohnangaben wurden nicht geladen.";
            _loaded = false;
            return;
        }

        _saved = numbers.Value.FirstOrDefault(e => e.EmployeeId == _employeeId)?.PersonnelNumber ?? "";
        PersonnelNumber = _saved;
        IsLoaded = true;
        await LoadRowsAsync().ConfigureAwait(true);
    }

    /// <summary>Die Monatswerte der Person; die Mengen rechnet die API.</summary>
    private async Task LoadRowsAsync()
    {
        var version = ++_version;
        var month = SelectedMonth;
        var result = await _context.Api.GetAsync<PayrollPreviewDto>($"api/payroll/preview?year={month.Year}&month={month.Month}").ConfigureAwait(true);
        if (version != _version)
        {
            return;
        }

        if (result.Value is not { } preview)
        {
            LoadError = result.Error ?? "Die Monatswerte wurden nicht geladen.";
            Rows = [];
            return;
        }

        LoadError = null;
        Rows = [.. preview.Lines.Where(l => l.EmployeeId == _employeeId).Select(PayrollRow.From)];
        OnPropertyChanged(nameof(ShowsEmpty));
    }

    /// <summary>Speichert die Personalnummer; <c>null</c> bei Erfolg, sonst der Grund der API.</summary>
    public async Task<string?> SaveAsync()
    {
        var number = PersonnelNumber.Trim();
        var result = await _context.Api.PutAsync<PayrollEmployeeDto>($"api/payroll/employees/{_employeeId}", new PersonnelNumberInput(number.Length == 0 ? null : number))
            .ConfigureAwait(true);
        if (result.Value is null)
        {
            return result.Error ?? "Die Personalnummer wurde nicht gespeichert.";
        }

        _saved = number;
        PersonnelNumber = number;
        OnPropertyChanged(nameof(HasUnsavedChanges));
        return null;
    }

    public void Discard()
    {
        PersonnelNumber = _saved;
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }
}
