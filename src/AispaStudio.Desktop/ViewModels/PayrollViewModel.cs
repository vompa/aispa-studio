using System.ComponentModel;
using System.Globalization;
using AispaStudio.Contracts;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>Land der gesetzlichen Feiertage in den Einstellungen der Lohnverrechnung (wie im Web: Österreich, Deutschland).</summary>
public sealed record PayrollCountry(FiscalCountry Country, string Label)
{
    public static IReadOnlyList<PayrollCountry> All { get; } = [new(FiscalCountry.AT, "Österreich"), new(FiscalCountry.DE, "Deutschland")];

    public override string ToString() => Label;
}

/// <summary>
/// Ansicht „Lohn“ im Bereich „Team“ (R05 Slice 100, Ä191, Ä250): Monat wählen, Monatswerte aller Personen ansehen, als Datei für BMD Lohn oder DATEV speichern und die
/// Einstellungen der Lohnverrechnung pflegen. Die Personalnummer steht auf der Personenkarte (<see cref="PersonPayrollViewModel"/>). Mengen, Prüfungen und das Dateiformat
/// kommen von der API; die App schickt die Bytes unverändert in den Speicherdialog. Vorbild: <c>web/src/payroll/PayrollPage.tsx</c>. Ändern darf, wer die Organisation verwaltet;
/// das Backoffice liest und exportiert.
/// </summary>
public sealed partial class PayrollViewModel : ViewModelBase, IEditableArea, ISavesFiles
{
    private static readonly string[] TimeFormats = ["H:mm", "HH:mm"];

    private static readonly HashSet<string> SettingFields =
    [
        nameof(SelectedCountry), nameof(NightStartText), nameof(NightEndText), nameof(ConsultantNumber), nameof(ClientNumber), nameof(HoursWageType), nameof(SundayWageType),
        nameof(HolidayWageType), nameof(NightWageType), nameof(VacationWageType), nameof(SickWageType), nameof(TrainingWageType),
    ];

    private readonly TeamContext _context;
    private SettingsState _savedState = new(FiscalCountry.AT, "", "", "", "", "", "", "", "", "", "", "");
    private bool _applying;

    public PayrollViewModel(TeamContext context)
    {
        _context = context;
        Months = PayrollMonth.Last(context.Today);
        SelectedMonth = PayrollMonth.Previous(context.Today);
        SelectedCountry = PayrollCountry.All[0];
    }

    /// <summary>Name der Maske in der Rückfrage.</summary>
    public string Title => "Lohn";

    public IFileSaver? Files { get; set; }

    /// <summary>Einstellungen ändern darf nur, wer die Organisation verwaltet (wie die API).</summary>
    public bool IsEditable => _context.CanManagePayroll;

    public IReadOnlyList<PayrollMonth> Months { get; }

    public IReadOnlyList<PayrollCountry> Countries { get; } = PayrollCountry.All;

    [ObservableProperty]
    public partial PayrollMonth SelectedMonth { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<PayrollRow> Rows { get; private set; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasWarnings))]
    public partial IReadOnlyList<string> Warnings { get; private set; } = [];

    public bool HasWarnings => Warnings.Count > 0;

    /// <summary>Die Vorschau wurde geholt (auch wenn sie leer ist).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsEmpty))]
    public partial bool HasPreview { get; private set; }

    public bool ShowsEmpty => HasPreview && Rows.Count == 0;

    /// <summary>„lohn-BmdLohn-2026-10.csv gespeichert.“</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMessage))]
    public partial string? Message { get; private set; }

    public bool HasMessage => Message is not null;

    /// <summary>Warum kein Export oder keine Vorschau entstanden ist (Text der API oder die Prüfung vor dem Export).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? Error { get; private set; }

    public bool HasError => Error is not null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLoadError))]
    public partial string? LoadError { get; private set; }

    public bool HasLoadError => LoadError is not null;

    [ObservableProperty]
    public partial bool IsLoaded { get; private set; }

    // ---------- Einstellungen ----------

    [ObservableProperty]
    public partial PayrollCountry SelectedCountry { get; set; }

    [ObservableProperty]
    public partial string NightStartText { get; set; } = "";

    [ObservableProperty]
    public partial string NightEndText { get; set; } = "";

    [ObservableProperty]
    public partial string ConsultantNumber { get; set; } = "";

    [ObservableProperty]
    public partial string ClientNumber { get; set; } = "";

    [ObservableProperty]
    public partial string HoursWageType { get; set; } = "";

    [ObservableProperty]
    public partial string SundayWageType { get; set; } = "";

    [ObservableProperty]
    public partial string HolidayWageType { get; set; } = "";

    [ObservableProperty]
    public partial string NightWageType { get; set; } = "";

    [ObservableProperty]
    public partial string VacationWageType { get; set; } = "";

    [ObservableProperty]
    public partial string SickWageType { get; set; } = "";

    [ObservableProperty]
    public partial string TrainingWageType { get; set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNightStartError))]
    public partial string? NightStartError { get; private set; }

    public bool HasNightStartError => NightStartError is not null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNightEndError))]
    public partial string? NightEndError { get; private set; }

    public bool HasNightEndError => NightEndError is not null;

    /// <summary>Fehler der API beim Speichern der Einstellungen.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSettingsError))]
    public partial string? SettingsError { get; private set; }

    public bool HasSettingsError => SettingsError is not null;

    /// <summary>„Einstellungen gespeichert.“, bis wieder etwas geändert wird.</summary>
    [ObservableProperty]
    public partial string? StatusText { get; private set; }

    public bool HasUnsavedChanges => IsLoaded && IsEditable && State() != _savedState;

    /// <summary>Alle Felder der Einstellungen als Wert; zwei Stände sind gleich, wenn jedes Feld gleich ist.</summary>
    private sealed record SettingsState(FiscalCountry Country, string NightStart, string NightEnd, string Consultant, string Client, string Hours, string Sunday, string Holiday,
        string Night, string Vacation, string Sick, string Training);

    private SettingsState State() => new(SelectedCountry.Country, NightStartText, NightEndText, ConsultantNumber, ClientNumber, HoursWageType, SundayWageType,
        HolidayWageType, NightWageType, VacationWageType, SickWageType, TrainingWageType);

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (!_applying && e.PropertyName is { } name && SettingFields.Contains(name))
        {
            StatusText = null;
            SettingsError = null;
            OnPropertyChanged(nameof(HasUnsavedChanges));
        }
    }

    partial void OnSelectedMonthChanged(PayrollMonth value)
    {
        Rows = [];
        Warnings = [];
        HasPreview = false;
        Message = null;
        Error = null;
        OnPropertyChanged(nameof(ShowsEmpty));
    }

    public Task EnsureLoadedAsync() => IsLoaded ? Task.CompletedTask : LoadAsync();

    [RelayCommand]
    public async Task LoadAsync()
    {
        LoadError = null;
        var result = await _context.Api.GetAsync<PayrollSettingsDto>("api/payroll/settings").ConfigureAwait(true);
        if (result.Value is not { } settings)
        {
            LoadError = result.Error ?? "Die Einstellungen der Lohnverrechnung wurden nicht geladen.";
            return;
        }

        Apply(settings);
        IsLoaded = true;
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    private void Apply(PayrollSettingsDto settings)
    {
        _applying = true;
        SelectedCountry = PayrollCountry.All.FirstOrDefault(c => c.Country == settings.Country) ?? PayrollCountry.All[0];
        NightStartText = settings.NightStart.ToString("HH:mm", CultureInfo.InvariantCulture);
        NightEndText = settings.NightEnd.ToString("HH:mm", CultureInfo.InvariantCulture);
        ConsultantNumber = settings.DatevConsultantNumber ?? "";
        ClientNumber = settings.DatevClientNumber ?? "";
        HoursWageType = settings.HoursWageType ?? "";
        SundayWageType = settings.SundayWageType ?? "";
        HolidayWageType = settings.HolidayWageType ?? "";
        NightWageType = settings.NightWageType ?? "";
        VacationWageType = settings.VacationWageType ?? "";
        SickWageType = settings.SickWageType ?? "";
        TrainingWageType = settings.TrainingWageType ?? "";
        _applying = false;
        _savedState = State();
        NightStartError = NightEndError = SettingsError = null;
    }

    public void Discard()
    {
        if (IsLoaded)
        {
            _applying = true;
            var saved = _savedState;
            SelectedCountry = PayrollCountry.All.First(c => c.Country == saved.Country);
            NightStartText = saved.NightStart;
            NightEndText = saved.NightEnd;
            ConsultantNumber = saved.Consultant;
            ClientNumber = saved.Client;
            HoursWageType = saved.Hours;
            SundayWageType = saved.Sunday;
            HolidayWageType = saved.Holiday;
            NightWageType = saved.Night;
            VacationWageType = saved.Vacation;
            SickWageType = saved.Sick;
            TrainingWageType = saved.Training;
            _applying = false;
        }

        NightStartError = NightEndError = SettingsError = null;
        StatusText = null;
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    /// <summary>Speichert die Einstellungen; false, wenn eine Uhrzeit nicht stimmt oder die API ablehnt (dann bleiben die Eingaben).</summary>
    public async Task<bool> SaveChangesAsync()
    {
        if (!IsEditable || !HasUnsavedChanges)
        {
            return true;
        }

        var start = ParseTime(NightStartText);
        var end = ParseTime(NightEndText);
        NightStartError = start is null ? "Trage die Uhrzeit wie 22:00 ein." : null;
        NightEndError = end is null ? "Trage die Uhrzeit wie 06:00 ein." : null;
        if (start is null || end is null)
        {
            return false;
        }

        var input = new PayrollSettingsDto(SelectedCountry.Country, Text(ConsultantNumber), Text(ClientNumber), start.Value, end.Value, Text(HoursWageType), Text(SundayWageType),
            Text(HolidayWageType), Text(NightWageType), Text(VacationWageType), Text(SickWageType), Text(TrainingWageType));
        var result = await _context.Api.PutAsync<PayrollSettingsDto>("api/payroll/settings", input).ConfigureAwait(true);
        if (result.Value is not { } saved)
        {
            SettingsError = result.Error ?? "Die Einstellungen wurden nicht gespeichert.";
            return false;
        }

        Apply(saved);
        StatusText = "Einstellungen gespeichert.";
        OnPropertyChanged(nameof(HasUnsavedChanges));
        return true;
    }

    [RelayCommand]
    private async Task SaveSettingsAsync() => await SaveChangesAsync().ConfigureAwait(true);

    [RelayCommand]
    private void DiscardSettings() => Discard();

    private static string? Text(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static TimeOnly? ParseTime(string text) =>
        TimeOnly.TryParseExact(text.Trim(), TimeFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var time) ? time : null;

    // ---------- Vorschau und Export ----------

    [RelayCommand]
    private async Task ShowPreviewAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        try
        {
            Message = Error = null;
            await LoadPreviewAsync().ConfigureAwait(true);
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Vorschau oder Export läuft; die Knöpfe sperren sich dann, sonst folgten zwei Speicherdialoge aufeinander.</summary>
    [ObservableProperty]
    public partial bool IsBusy { get; private set; }

    /// <summary>Holt die Monatswerte und zeigt sie; <c>null</c> mit gesetztem <see cref="Error"/>, wenn die API sie verweigert.</summary>
    private async Task<PayrollPreviewDto?> LoadPreviewAsync()
    {
        var month = SelectedMonth;
        var result = await _context.Api.GetAsync<PayrollPreviewDto>($"api/payroll/preview?year={month.Year}&month={month.Month}").ConfigureAwait(true);
        if (month != SelectedMonth)
        {
            return null;
        }

        if (result.Value is not { } preview)
        {
            Error = result.Error ?? "Die Monatswerte wurden nicht geladen.";
            return null;
        }

        Rows = [.. preview.Lines.Select(PayrollRow.From)];
        Warnings = preview.Warnings;
        HasPreview = true;
        OnPropertyChanged(nameof(ShowsEmpty));
        return preview;
    }

    [RelayCommand]
    private Task ExportBmdAsync() => ExportAsync(PayrollFormat.BmdLohn);

    [RelayCommand]
    private Task ExportLodasAsync() => ExportAsync(PayrollFormat.DatevLodas);

    [RelayCommand]
    private Task ExportLohnUndGehaltAsync() => ExportAsync(PayrollFormat.DatevLohnUndGehalt);

    /// <summary>Holt vorher die Monatswerte (und zeigt sie): fehlt etwas, gibt es die Meldung statt einer leeren Datei. Dann die Datei, Bytes unverändert.</summary>
    private async Task ExportAsync(PayrollFormat format)
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        try
        {
            await ExportCoreAsync(format).ConfigureAwait(true);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ExportCoreAsync(PayrollFormat format)
    {
        Message = Error = null;
        var month = SelectedMonth;
        if (await LoadPreviewAsync().ConfigureAwait(true) is not { } preview)
        {
            return;
        }

        if (PayrollExportCheck.Blocker(preview) is { } blocker)
        {
            Error = blocker;
            return;
        }

        var result = await _context.Api.GetBytesAsync($"api/payroll/export?format={format}&year={month.Year}&month={month.Month}").ConfigureAwait(true);
        if (result.Value is not { } content)
        {
            Error = result.Error ?? "Der Export konnte nicht erstellt werden.";
            return;
        }

        var fileName = $"lohn-{format}-{month.Key}.csv";
        try
        {
            if (Files is not null && await Files.SaveAsync(fileName, "CSV-Datei", ["*.csv"], content).ConfigureAwait(true))
            {
                Message = $"{fileName} gespeichert.";
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Error = "Die Datei konnte nicht gespeichert werden. Wähle einen anderen Ort.";
        }
    }
}
