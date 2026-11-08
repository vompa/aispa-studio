using System.ComponentModel;
using System.Globalization;
using AispaStudio.Contracts;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>Eine Zeile der Listen „Zuständigkeiten“ und „Zertifikate“; abgelaufene stehen ausgegraut, fällige und abgelaufene Zertifikate mit Warnung als Text und Symbol.</summary>
public sealed class DutyRow(string title, IReadOnlyList<string> lines, string? warning, bool isExpired, bool isEditable, IRelayCommand edit, IAsyncRelayCommand delete)
{
    public string Title => title;

    public IReadOnlyList<string> Lines => lines;

    public string? Warning => warning;

    public bool HasWarning => warning is not null;

    public bool IsExpired => isExpired;

    public bool IsEditable => isEditable;

    /// <summary>Für den Screenreader: die Warnung gehört zum Namen der Zeile, nicht nur zur Farbe.</summary>
    public string AutomationName => warning is null ? title : $"{title}, Warnung: {warning}";

    public IRelayCommand EditCommand => edit;

    public IAsyncRelayCommand DeleteCommand => delete;

    public bool HasScan { get; init; }

    public bool IsCertificate => RenewCommand is not null;

    public bool CanAddScan => IsEditable && IsCertificate;

    public bool CanRemoveScan => IsEditable && HasScan;

    public IRelayCommand? RenewCommand { get; init; }

    public IAsyncRelayCommand? AddScanCommand { get; init; }

    public IAsyncRelayCommand? SaveScanCommand { get; init; }

    public IAsyncRelayCommand? RemoveScanCommand { get; init; }

    /// <summary>„Scan hinzufügen“ bzw. „Scan ersetzen“, wenn schon einer da ist.</summary>
    public string AddScanLabel => HasScan ? "Scan ersetzen" : "Scan hinzufügen";
}

/// <summary>
/// Reiter „Zuständigkeiten“ der Personenkarte (R05 Slice 117, Ä274): Zuständigkeiten aus der Liste des Spas mit Gültigkeit und Vertretung, dazu Zertifikate mit Ablauf, Erinnerungsfrist und Scan.
/// Nur für Inhaberin und Manager (<c>ManageHrFile</c>); bei Ausgeschiedenen nur lesen. Ob ein Zertifikat fällig ist, rechnet die API. Ein offenes Formular zählt zu den ungespeicherten Änderungen der Karte.
/// </summary>
public sealed partial class PersonDutiesViewModel : ViewModelBase, ISavesFiles
{
    private enum FormKind
    {
        None,
        Duty,
        Certificate,
    }

    /// <summary>Wie die API: höchstens 5 MB je Scan.</summary>
    private const long MaxScanBytes = 5 * 1024 * 1024;

    private static readonly string[] ScanPatterns = ["*.pdf", "*.jpg", "*.jpeg", "*.png"];

    private readonly TeamContext _context;
    private readonly Guid _employeeId;
    private readonly string _dutiesPath;
    private readonly string _certificatesPath;
    private List<DutyTypeDto> _types = [];
    private FormKind _form;
    private Guid? _editingId;
    private string _savedState = "";
    private bool _applying;

    public PersonDutiesViewModel(TeamContext context, Guid employeeId, bool isReadOnly)
    {
        _context = context;
        _employeeId = employeeId;
        _dutiesPath = $"api/employees/{employeeId}/duties";
        _certificatesPath = $"api/employees/{employeeId}/certificates";
        IsEditable = !isReadOnly;
    }

    /// <summary>Etwas wurde gespeichert oder gelöscht: Kennzeichen auf Karte und Liste neu holen.</summary>
    public event Func<Task>? Changed;

    /// <summary>Die Ansicht setzt den Fokus auf das erste Feld oder das erste ungültige Feld.</summary>
    public event Action<string>? FocusRequested;

    public bool IsEditable { get; }

    /// <summary>Speicherdialog für „Scan speichern unter“; setzt die Ansicht.</summary>
    public IFileSaver? Files { get; set; }

    /// <summary>Dateiauswahl für „Scan hinzufügen“; setzt die Ansicht.</summary>
    public IFilePicker? Picker { get; set; }

    private DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(_context.Now, _context.Zone).DateTime);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsLoading))]
    public partial bool IsLoaded { get; private set; }

    public bool ShowsLoading => !IsLoaded && !HasLoadError;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLoadError), nameof(ShowsLoading))]
    public partial string? LoadError { get; private set; }

    public bool HasLoadError => LoadError is not null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoDuties))]
    public partial IReadOnlyList<DutyRow> Duties { get; private set; } = [];

    public bool HasNoDuties => Duties.Count == 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoCertificates))]
    public partial IReadOnlyList<DutyRow> Certificates { get; private set; } = [];

    public bool HasNoCertificates => Certificates.Count == 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError), nameof(ShowsFormError), nameof(ShowsListError))]
    public partial string? Error { get; private set; }

    public bool HasError => Error is not null;

    public bool ShowsFormError => HasError && IsEditing;

    public bool ShowsListError => HasError && !IsEditing;

    [ObservableProperty]
    public partial string? StatusText { get; private set; }

    // ---------- Offenes Formular ----------

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsAddButtons), nameof(ShowsFormError), nameof(ShowsListError), nameof(HasUnsavedChanges))]
    public partial bool IsEditing { get; private set; }

    public bool ShowsAddButtons => IsEditable && !IsEditing;

    [ObservableProperty]
    public partial string FormTitle { get; private set; } = "";

    public bool IsDutyForm => _form == FormKind.Duty;

    public bool IsCertificateForm => _form == FormKind.Certificate;

    /// <summary>Die aktiven Einträge der Liste; beim Ändern auch ein inzwischen ausgeblendeter, wenn die Zuständigkeit ihn hat.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<RegulationOption<Guid>> DutyTypes { get; private set; } = [];

    /// <summary>„Keine Vertretung“ und die anderen Personen im Team.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<RegulationOption<Guid?>> Deputies { get; private set; } = [];

    /// <summary>Die verlangten Zertifikate der Liste als Vorschläge; eine andere Bezeichnung ist erlaubt.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<string> CertificateSuggestions { get; private set; } = [];

    [ObservableProperty]
    public partial RegulationOption<Guid>? SelectedDutyType { get; set; }

    [ObservableProperty]
    public partial RegulationOption<Guid?>? SelectedDeputy { get; set; }

    [ObservableProperty]
    public partial DateOnly? ValidFrom { get; set; }

    [ObservableProperty]
    public partial DateOnly? ValidUntil { get; set; }

    [ObservableProperty]
    public partial string CertificateName { get; set; } = "";

    [ObservableProperty]
    public partial DateOnly? IssuedOn { get; set; }

    [ObservableProperty]
    public partial string IssuedBy { get; set; } = "";

    [ObservableProperty]
    public partial decimal? RemindDays { get; set; }

    [ObservableProperty]
    public partial string? DutyTypeError { get; private set; }

    [ObservableProperty]
    public partial string? CertificateNameError { get; private set; }

    [ObservableProperty]
    public partial string? ValidFromError { get; private set; }

    [ObservableProperty]
    public partial string? ValidUntilError { get; private set; }

    [ObservableProperty]
    public partial string? RemindDaysError { get; private set; }

    public bool HasUnsavedChanges => IsEditing && State() != _savedState;

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.PropertyName is nameof(SelectedDutyType) or nameof(SelectedDeputy) or nameof(ValidFrom) or nameof(ValidUntil) or nameof(CertificateName) or nameof(IssuedOn)
            or nameof(IssuedBy) or nameof(RemindDays))
        {
            FormChanged();
        }
    }

    public Task EnsureLoadedAsync() => IsLoaded ? Task.CompletedTask : LoadAsync();

    [RelayCommand]
    public async Task LoadAsync()
    {
        LoadError = null;
        var types = await _context.Api.GetAsync<List<DutyTypeDto>>("api/staff-duties/types").ConfigureAwait(true);
        var duties = await _context.Api.GetAsync<List<StaffDutyDto>>(_dutiesPath).ConfigureAwait(true);
        var certificates = await _context.Api.GetAsync<List<StaffCertificateDto>>(_certificatesPath).ConfigureAwait(true);
        if (types.Value is null || duties.Value is null || certificates.Value is null)
        {
            LoadError = types.Error ?? duties.Error ?? certificates.Error ?? "Die Zuständigkeiten und Zertifikate wurden nicht geladen.";
            return;
        }

        _types = types.Value;
        CertificateSuggestions = [.. _types.Where(t => t.IsActive).Select(t => t.RequiredCertificate).OfType<string>().Distinct(StringComparer.OrdinalIgnoreCase)];
        Duties = [.. duties.Value.Select(ToRow)];
        Certificates = [.. certificates.Value.Select(ToRow)];
        IsLoaded = true;
    }

    [RelayCommand]
    private void AddDuty() => OpenDuty(null);

    [RelayCommand]
    private void AddCertificate() => OpenCertificate(null, "Zertifikat hinzufügen", null);

    /// <summary>Für die Rückfrage der Karte: speichert nur, wenn etwas geändert ist.</summary>
    public Task<bool> SaveChangesAsync() => HasUnsavedChanges ? SaveFormAsync() : Task.FromResult(true);

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (IsEditing)
        {
            await SaveFormAsync().ConfigureAwait(true);
        }
    }

    [RelayCommand]
    private void Cancel() => Discard();

    [RelayCommand]
    private void CloseError() => Error = null;

    public void Discard()
    {
        Close();
        Error = null;
        StatusText = null;
    }

    private static string Day(DateOnly day) => day.ToString("d.M.yyyy", CultureInfo.InvariantCulture);

    private static string Period(DateOnly from, DateOnly? until) => until is { } u ? $"Gültig von {Day(from)} bis {Day(u)}" : $"Gültig ab {Day(from)}";

    private DutyRow ToRow(StaffDutyDto duty)
    {
        var lines = new List<string> { Period(duty.ValidFrom, duty.ValidUntil) };
        if (duty.DeputyName is { } deputy)
        {
            lines.Add($"Vertretung: {deputy}");
        }

        var title = duty.IsExpired ? $"{duty.DutyTypeName} (abgelaufen)" : duty.DutyTypeName;
        return new DutyRow(title, lines, null, duty.IsExpired, IsEditable,
            new RelayCommand(() => OpenDuty(duty), () => !IsEditing),
            new AsyncRelayCommand(() => DeleteAsync("Zuständigkeit", duty.DutyTypeName, $"{_dutiesPath}/{duty.Id}"), () => !IsEditing));
    }

    private DutyRow ToRow(StaffCertificateDto certificate)
    {
        var lines = new List<string>();
        if (certificate.IssuedOn is { } issued)
        {
            lines.Add($"Ausgestellt am {Day(issued)}");
        }

        lines.Add(certificate.ValidUntil is { } until ? $"Gültig bis {Day(until)}" : "Unbefristet");
        if (certificate.IssuedBy is { } by)
        {
            lines.Add($"Ausstellende Stelle: {by}");
        }

        if (certificate.ValidUntil is not null)
        {
            lines.Add($"Erinnerung {certificate.RemindDays} Tage vor dem Ablauf");
        }

        if (certificate.HasScan)
        {
            lines.Add("Scan gespeichert");
        }

        var warning = (certificate.IsDue, certificate.IsExpired, certificate.ValidUntil) switch
        {
            (true, true, { } day) => $"Abgelaufen am {Day(day)}",
            (true, false, { } day) => $"Läuft am {Day(day)} ab",
            _ => null,
        };
        var scanPath = $"{_certificatesPath}/{certificate.Id}/scan";
        return new DutyRow(certificate.Name, lines, warning, certificate.IsExpired && !certificate.IsDue, IsEditable,
            new RelayCommand(() => OpenCertificate(certificate, "Zertifikat ändern", certificate.Id), () => !IsEditing),
            new AsyncRelayCommand(() => DeleteAsync("Zertifikat", certificate.Name, $"{_certificatesPath}/{certificate.Id}"), () => !IsEditing))
        {
            HasScan = certificate.HasScan,
            RenewCommand = new RelayCommand(() => OpenCertificate(certificate with { IssuedOn = Today, ValidUntil = null }, "Zertifikat erneuern", null), () => !IsEditing),
            AddScanCommand = new AsyncRelayCommand(() => AddScanAsync(certificate, scanPath), () => !IsEditing),
            SaveScanCommand = new AsyncRelayCommand(() => SaveScanAsync(certificate, scanPath)),
            RemoveScanCommand = new AsyncRelayCommand(() => DeleteAsync("Scan", $"Scan zu {certificate.Name}", scanPath), () => !IsEditing),
        };
    }

    private void OpenDuty(StaffDutyDto? duty)
    {
        var types = _types.Where(t => t.IsActive || t.Id == duty?.DutyTypeId).Select(t => new RegulationOption<Guid>(t.Id, t.Name)).ToList();
        var people = _context.Data.Employees.Where(e => e.Id != _employeeId).OrderBy(e => e.DisplayName, StringComparer.CurrentCulture)
            .Select(e => new RegulationOption<Guid?>(e.Id, e.DisplayName));
        Open(FormKind.Duty, duty?.Id, duty is null ? "Zuständigkeit hinzufügen" : "Zuständigkeit ändern", () =>
        {
            DutyTypes = types;
            Deputies = [new RegulationOption<Guid?>(null, "Keine Vertretung"), .. people];
            SelectedDutyType = duty is null ? types.FirstOrDefault() : types.FirstOrDefault(t => t.Value == duty.DutyTypeId);
            SelectedDeputy = Deputies.FirstOrDefault(d => d.Value == duty?.DeputyId) ?? Deputies[0];
            ValidFrom = duty?.ValidFrom ?? Today;
            ValidUntil = duty?.ValidUntil;
        });
        FocusRequested?.Invoke("DutyTypeField");
    }

    private void OpenCertificate(StaffCertificateDto? certificate, string title, Guid? id)
    {
        Open(FormKind.Certificate, id, title, () =>
        {
            CertificateName = certificate?.Name ?? "";
            IssuedOn = certificate?.IssuedOn;
            ValidUntil = certificate?.ValidUntil;
            IssuedBy = certificate?.IssuedBy ?? "";
            RemindDays = certificate?.RemindDays ?? 60;
        });
        FocusRequested?.Invoke(id is null && certificate is not null ? "ValidUntilCertificateField" : "CertificateNameField");
    }

    private void Open(FormKind form, Guid? id, string title, Action fill)
    {
        _applying = true;
        _form = form;
        _editingId = id;
        fill();
        DutyTypeError = CertificateNameError = ValidFromError = ValidUntilError = RemindDaysError = Error = StatusText = null;
        FormTitle = title;
        _applying = false;
        _savedState = State();
        IsEditing = true;
        OnFormKindChanged();
    }

    private void Close()
    {
        _form = FormKind.None;
        _editingId = null;
        IsEditing = false;
        OnFormKindChanged();
    }

    private void OnFormKindChanged()
    {
        OnPropertyChanged(nameof(IsDutyForm));
        OnPropertyChanged(nameof(IsCertificateForm));
        foreach (var row in Duties.Concat(Certificates))
        {
            row.EditCommand.NotifyCanExecuteChanged();
            row.DeleteCommand.NotifyCanExecuteChanged();
            row.RenewCommand?.NotifyCanExecuteChanged();
            row.AddScanCommand?.NotifyCanExecuteChanged();
            row.RemoveScanCommand?.NotifyCanExecuteChanged();
        }
    }

    private void FormChanged()
    {
        if (_applying || !IsEditing)
        {
            return;
        }

        StatusText = null;
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    private string State() => string.Join('|', _form, SelectedDutyType?.Value, SelectedDeputy?.Value, ValidFrom, ValidUntil, CertificateName, IssuedOn, IssuedBy, RemindDays);

    /// <summary>Sagt nur früher, was fehlt; geprüft wird in der API.</summary>
    private bool Check()
    {
        var duty = _form == FormKind.Duty;
        DutyTypeError = duty && SelectedDutyType is null ? "Bitte eine Zuständigkeit wählen." : null;
        ValidFromError = duty && ValidFrom is null ? "Bitte ein Datum eingeben." : null;
        CertificateNameError = !duty && string.IsNullOrWhiteSpace(CertificateName) ? "Bitte eine Bezeichnung eingeben." : null;
        var start = duty ? ValidFrom : IssuedOn;
        ValidUntilError = start is { } from && ValidUntil is { } until && until < from
            ? duty ? "„Gültig bis“ darf nicht vor „Gültig von“ liegen." : "„Gültig bis“ darf nicht vor „Ausgestellt am“ liegen."
            : null;
        RemindDaysError = !duty && RemindDays is not (>= 1 and <= 365) ? "Bitte 1 bis 365 Tage eingeben." : null;
        var first = DutyTypeError is not null ? "DutyTypeField" : CertificateNameError is not null ? "CertificateNameField" : ValidFromError is not null ? "ValidFromField"
            : ValidUntilError is not null ? duty ? "ValidUntilField" : "ValidUntilCertificateField" : RemindDaysError is not null ? "RemindDaysField" : null;
        if (first is not null)
        {
            FocusRequested?.Invoke(first);
        }

        return first is null;
    }

    private async Task<bool> SaveFormAsync()
    {
        if (!Check())
        {
            return false;
        }

        Error = null;
        var (path, input) = _form == FormKind.Duty
            ? (_dutiesPath, (object)new StaffDutyInput(SelectedDutyType!.Value, ValidFrom!.Value, ValidUntil, SelectedDeputy?.Value))
            : (_certificatesPath, new StaffCertificateInput(CertificateName.Trim(), IssuedOn, ValidUntil, string.IsNullOrWhiteSpace(IssuedBy) ? null : IssuedBy.Trim(), (int)RemindDays!.Value));
        var result = _editingId is { } id
            ? await _context.Api.PutAsync<object>($"{path}/{id}", input).ConfigureAwait(true)
            : await _context.Api.PostAsync<object>(path, input).ConfigureAwait(true);
        if (result.Error is { } error)
        {
            Error = error;
            return false;
        }

        Close();
        await AfterChangeAsync().ConfigureAwait(true);
        StatusText = "Gespeichert.";
        return true;
    }

    /// <summary>Art der Datei an den ersten Bytes; die Endung im Dateinamen allein sagt nichts.</summary>
    private static (string ContentType, string Extension)? ScanType(byte[] content) => content switch
    {
        [0x25, 0x50, 0x44, 0x46, ..] => ("application/pdf", ".pdf"),
        [0x89, 0x50, 0x4E, 0x47, ..] => ("image/png", ".png"),
        [0xFF, 0xD8, 0xFF, ..] => ("image/jpeg", ".jpg"),
        _ => null,
    };

    private async Task AddScanAsync(StaffCertificateDto certificate, string path)
    {
        if (Picker is not { } picker)
        {
            return;
        }

        Error = StatusText = null;
        if (await picker.PickBytesAsync("Scan des Zertifikats wählen", "PDF oder Bild", ScanPatterns, MaxScanBytes).ConfigureAwait(true) is not { } content)
        {
            return;
        }

        if (content.Length == 0)
        {
            Error = "Der Scan ist größer als 5 MB. Bitte kleiner scannen oder als PDF verkleinern.";
            return;
        }

        if (ScanType(content) is not { } type)
        {
            Error = "Der Scan muss ein PDF, JPEG oder PNG sein. Bitte die Datei als PDF speichern oder fotografieren.";
            return;
        }

        var result = await _context.Api.PutAsync<object>(path, new StaffCertificateScanInput(certificate.Name + type.Extension, type.ContentType, Convert.ToBase64String(content))).ConfigureAwait(true);
        if (result.Error is { } error)
        {
            Error = error;
            return;
        }

        await AfterChangeAsync().ConfigureAwait(true);
        StatusText = "Scan gespeichert.";
    }

    private async Task SaveScanAsync(StaffCertificateDto certificate, string path)
    {
        if (Files is not { } files)
        {
            return;
        }

        Error = StatusText = null;
        var result = await _context.Api.GetBytesAsync(path).ConfigureAwait(true);
        if (result.Value is not { } content)
        {
            Error = result.Error ?? "Der Scan wurde nicht geladen.";
            return;
        }

        var extension = ScanType(content)?.Extension ?? ".pdf";
        if (await files.SaveAsync(certificate.Name + extension, "Scan", [$"*{extension}"], content).ConfigureAwait(true))
        {
            StatusText = "Datei gespeichert.";
        }
    }

    private async Task DeleteAsync(string kind, string title, string path)
    {
        if (_context.Dialogs is not { } dialogs)
        {
            return;
        }

        var answer = await dialogs.AskAsync(new ConfirmRequest($"{kind} löschen?", $"„{title}“ wird gelöscht. Im Verlauf bleibt nur, dass gelöscht wurde.", "Löschen", IsDestructive: true)).ConfigureAwait(true);
        if (!answer.IsConfirmed)
        {
            return;
        }

        var result = await _context.Api.DeleteAsync<object>(path).ConfigureAwait(true);
        if (result.Error is { } error)
        {
            Error = error;
            return;
        }

        await AfterChangeAsync().ConfigureAwait(true);
        StatusText = "Gelöscht.";
    }

    private async Task AfterChangeAsync()
    {
        await LoadAsync().ConfigureAwait(true);
        if (Changed is { } changed)
        {
            await changed().ConfigureAwait(true);
        }
    }
}
