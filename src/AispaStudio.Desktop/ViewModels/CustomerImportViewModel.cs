using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json;
using AispaStudio.Contracts;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>Die vier Schritte des Kunden-Imports (Export/Import, Aufgabe E).</summary>
public enum CustomerImportStep
{
    File,
    Mapping,
    Review,
    Run,
}

/// <summary>Ein Feld des Gastes in der Auswahl rechts neben einer Spalte; <c>Field = null</c> heißt „nicht importieren“.</summary>
public sealed record ImportFieldChoice(CustomerImportField? Field, string Label)
{
    public static IReadOnlyList<ImportFieldChoice> All { get; } =
    [
        new(null, "nicht importieren"),
        new(CustomerImportField.FirstName, "Vorname"),
        new(CustomerImportField.LastName, "Nachname"),
        new(CustomerImportField.Email, "E-Mail"),
        new(CustomerImportField.Phone, "Telefon"),
        new(CustomerImportField.BirthDate, "Geburtstag"),
        new(CustomerImportField.Salutation, "Anrede"),
        new(CustomerImportField.Street, "Straße"),
        new(CustomerImportField.PostalCode, "PLZ"),
        new(CustomerImportField.City, "Ort"),
        new(CustomerImportField.Country, "Land"),
        new(CustomerImportField.MarketingConsent, "Werbe-Einwilligung"),
    ];

    public static ImportFieldChoice For(CustomerImportField? field) => All.First(c => c.Field == field);

    public override string ToString() => Label;
}

/// <summary>Das Datumsformat der Datei in der Auswahl von Schritt 2.</summary>
public sealed record ImportDateFormatChoice(ImportDateFormat Format, string Label)
{
    public static IReadOnlyList<ImportDateFormatChoice> All { get; } =
    [
        new(ImportDateFormat.Auto, "Automatisch"),
        new(ImportDateFormat.DayMonthYear, "TT.MM.JJJJ"),
        new(ImportDateFormat.YearMonthDay, "JJJJ-MM-TT"),
        new(ImportDateFormat.MonthDayYear, "MM/TT/JJJJ"),
    ];

    public static ImportDateFormatChoice For(ImportDateFormat format) => All.First(c => c.Format == format);

    public override string ToString() => Label;
}

/// <summary>Eine Spalte der Datei in Schritt 2: Kopf und Beispielwerte links, das gewählte Feld von AISPA rechts.</summary>
public sealed partial class ImportColumnRow : ObservableObject
{
    private Action<ImportColumnRow>? _changed;

    public ImportColumnRow(ImportColumn column, CustomerImportField? field)
    {
        Index = column.Index;
        Header = string.IsNullOrWhiteSpace(column.Header) ? string.Create(CultureInfo.InvariantCulture, $"(Spalte {column.Index + 1})") : column.Header;
        SamplesText = column.Samples.Count == 0 ? "keine Werte" : string.Join(" · ", column.Samples);
        Choice = ImportFieldChoice.For(field);
    }

    public int Index { get; }

    public string Header { get; }

    public string SamplesText { get; }

    public IReadOnlyList<ImportFieldChoice> Choices { get; } = ImportFieldChoice.All;

    [ObservableProperty]
    public partial ImportFieldChoice Choice { get; set; }

    /// <summary>Meldet jede Änderung der Auswahl; erst nach dem Aufbau der Zeile gesetzt.</summary>
    public void Watch(Action<ImportColumnRow> changed) => _changed = changed;

    partial void OnChoiceChanged(ImportFieldChoice value) => _changed?.Invoke(this);
}

/// <summary>Eine Zeile der Vorschau in Schritt 3.</summary>
public sealed record ImportPreviewRow(int Row, string Status, string Name, string Contact, string? Reason)
{
    public static ImportPreviewRow From(ImportRowPreview row) => new(
        row.Row,
        row.Status switch
        {
            ImportRowStatus.New => "Neu",
            ImportRowStatus.Duplicate => "Dublette",
            _ => "Fehlerhaft",
        },
        string.IsNullOrWhiteSpace(row.Name) ? "(ohne Namen)" : row.Name,
        string.Join(" · ", new[] { row.Email, row.Phone }.Where(v => !string.IsNullOrWhiteSpace(v))),
        row.Reason);

    public string Title => string.Create(CultureInfo.InvariantCulture, $"Zeile {Row}: {Name}");

    public bool HasReason => !string.IsNullOrWhiteSpace(Reason);
}

/// <summary>Eine Dublette in Schritt 3 mit der Wahl „überspringen“ (Vorgabe) oder „ergänzen“.</summary>
public sealed partial class ImportDuplicateRow(ImportPreviewRow row) : ObservableObject
{
    public ImportPreviewRow Row { get; } = row;

    public string Title => Row.Title;

    public string? Reason => Row.Reason;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSkip))]
    public partial bool IsMerge { get; set; }

    public bool IsSkip
    {
        get => !IsMerge;
        set => IsMerge = !value;
    }

    public ImportDuplicateDecision Decision => new(Row.Row, IsMerge ? ImportDuplicateAction.Merge : ImportDuplicateAction.Skip);
}

/// <summary>
/// Kunden-Import in vier Schritten als Dialog (Export/Import, Aufgabe E): (1) Datei wählen, (2) Spalten zuordnen, (3) Vorschau und Prüfung mit Entscheidung je Dublette, (4) Probelauf
/// und Import. Die API hält nichts zwischen den Schritten; jeder Aufruf schickt die Datei neu mit. „Abbrechen“ ruft nichts auf. Nach einem echten Import schließt der Dialog mit
/// „bestätigt“ und <see cref="Outcome"/>; ein Probelauf zeigt das Ergebnis im Dialog und ändert nichts.
/// </summary>
public sealed partial class CustomerImportViewModel : ConfirmDialogViewModel
{
    public const string DialogTitle = "Kunden importieren?";
    public const long MaxBytes = 10L * 1024 * 1024;
    public const string NotSaved = "Die Datei konnte nicht gespeichert werden. Wählen Sie einen anderen Ort.";
    public const string NoPicker = "Der Dateidialog ist nicht verfügbar.";
    public const string UnsupportedFile = "Bitte eine Excel-Datei (.xlsx) oder CSV-Datei wählen.";
    public const string EmptyFile = "Die Datei ist leer.";
    public const string TooBig = "Die Datei ist größer als 10 MB. Bitte in mehrere Dateien aufteilen.";
    public const string ReadFailed = "Die Datei konnte nicht gelesen werden. Wählen Sie eine andere Datei.";
    public const string LastNameMissing = "Nachname fehlt. Bitte die Spalte mit dem Nachnamen zuordnen.";
    public const string NotChecked = "Der Import konnte nicht geprüft werden. Verbindung prüfen und erneut versuchen.";

    private static readonly string[] FilePatterns = ["*.xlsx", "*.csv"];
    private static readonly string[] Extensions = [".xlsx", ".csv"];

    private readonly IAispaApi _api;
    private readonly IFilePicker? _picker;
    private readonly Func<IFileSaver?> _saver;
    private byte[] _bytes = [];
    private string _fileName = "";

    public CustomerImportViewModel(IAispaApi api, IFilePicker? picker, Func<IFileSaver?> saver, Action<ConfirmResult> answer)
        : base(new ConfirmRequest(DialogTitle, "Gäste aus einer Excel- oder CSV-Datei übernehmen, zum Beispiel beim Umstieg von einem anderen Programm.", "Importieren", IsDestructive: false), answer)
    {
        _api = api;
        _picker = picker;
        _saver = saver;
    }

    public IReadOnlyList<ImportDateFormatChoice> DateFormats { get; } = ImportDateFormatChoice.All;

    public ObservableCollection<ImportColumnRow> Columns { get; } = [];

    public ObservableCollection<ImportPreviewRow> SampleRows { get; } = [];

    public ObservableCollection<ImportDuplicateRow> DuplicateRows { get; } = [];

    public ObservableCollection<ImportPreviewRow> InvalidRows { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsFileStep), nameof(IsMappingStep), nameof(IsReviewStep), nameof(IsRunStep), nameof(StepText), nameof(ContinueText), nameof(CanGoBack))]
    [NotifyCanExecuteChangedFor(nameof(ContinueCommand), nameof(BackCommand), nameof(CheckCommand))]
    public partial CustomerImportStep Step { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanGoBack))]
    [NotifyCanExecuteChangedFor(nameof(ContinueCommand), nameof(BackCommand), nameof(CheckCommand), nameof(SaveErrorsCommand), nameof(CancelCommand))]
    public partial bool IsBusy { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFile))]
    public partial string? FileName { get; private set; }

    /// <summary>Fehler am Feld „Datei“ (Schritt 1).</summary>
    [ObservableProperty]
    public partial string? FileError { get; private set; }

    /// <summary>Fehler des Servers in den Schritten 2 bis 4; bleibt stehen, nennt Problem und Weg.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? Error { get; private set; }

    [ObservableProperty]
    public partial ImportDateFormatChoice SelectedDateFormat { get; set; } = ImportDateFormatChoice.All[0];

    [ObservableProperty]
    public partial CustomerImportPreview? Preview { get; private set; }

    /// <summary>„240 neu, 12 Dubletten, 3 fehlerhaft“ (Schritt 3 und 4).</summary>
    [ObservableProperty]
    public partial string Summary { get; private set; } = "";

    /// <summary>Ergebnis des Probelaufs („Probelauf: …“), solange es gilt.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDryRun))]
    public partial string? DryRunText { get; private set; }

    /// <summary>„Gespeichert: Importfehler_2026-11-09.xlsx“ nach dem Speichern der Fehlerzeilen.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSaved))]
    public partial string? SavedText { get; private set; }

    /// <summary>Das Ergebnis des echten Imports; gesetzt, wenn der Dialog mit „bestätigt“ schließt.</summary>
    public CustomerImportResult? Outcome { get; private set; }

    public bool IsFileStep => Step == CustomerImportStep.File;

    public bool IsMappingStep => Step == CustomerImportStep.Mapping;

    public bool IsReviewStep => Step == CustomerImportStep.Review;

    public bool IsRunStep => Step == CustomerImportStep.Run;

    public bool HasFile => FileName is not null;

    public bool HasError => Error is not null;

    public bool HasDryRun => DryRunText is not null;

    public bool HasSaved => SavedText is not null;

    public bool HasDuplicates => DuplicateRows.Count > 0;

    public bool HasInvalid => InvalidRows.Count > 0;

    public string StepText => Step switch
    {
        CustomerImportStep.File => "Schritt 1 von 4 · Datei wählen",
        CustomerImportStep.Mapping => "Schritt 2 von 4 · Spalten zuordnen",
        CustomerImportStep.Review => "Schritt 3 von 4 · Vorschau und Prüfung",
        _ => "Schritt 4 von 4 · Probelauf und Import",
    };

    public string ContinueText => Step switch
    {
        CustomerImportStep.File => "Datei wählen …",
        CustomerImportStep.Run => "Importieren",
        _ => "Weiter",
    };

    public bool CanGoBack => Step != CustomerImportStep.File && !IsBusy;

    /// <summary>Fehler am Feld „Zuordnung“: ohne Nachname lässt sich kein Gast anlegen.</summary>
    public string? MappingError => Columns.Any(c => c.Choice.Field == CustomerImportField.LastName) ? null : LastNameMissing;

    /// <summary>Solange ein Aufruf läuft, lässt sich der Dialog nicht schließen (ein halber Import bliebe sonst unbemerkt).</summary>
    protected override bool CanCancel() => !IsBusy;

    private bool CanContinue() => !IsBusy && (Step != CustomerImportStep.Mapping || MappingError is null);

    [RelayCommand(CanExecute = nameof(CanContinue))]
    private async Task ContinueAsync()
    {
        switch (Step)
        {
            case CustomerImportStep.File:
                await ChooseFileAsync().ConfigureAwait(true);
                break;
            case CustomerImportStep.Mapping:
                await ReviewAsync().ConfigureAwait(true);
                break;
            case CustomerImportStep.Review:
                Step = CustomerImportStep.Run;
                break;
            default:
                await RunAsync(dryRun: false).ConfigureAwait(true);
                break;
        }
    }

    private bool CanBack() => CanGoBack;

    [RelayCommand(CanExecute = nameof(CanBack))]
    private void Back()
    {
        Error = null;
        DryRunText = null;
        SavedText = null;
        Step = Step switch
        {
            CustomerImportStep.Run => CustomerImportStep.Review,
            CustomerImportStep.Review => CustomerImportStep.Mapping,
            _ => CustomerImportStep.File,
        };
    }

    // ---------- Schritt 1: Datei wählen ----------

    private async Task ChooseFileAsync()
    {
        FileError = null;
        Error = null;
        if (_picker is null)
        {
            FileError = NoPicker;
            return;
        }

        PickedBytes? picked;
        try
        {
            picked = await _picker.PickFileAsync("Datei zum Import wählen", "Excel oder CSV", FilePatterns, MaxBytes).ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            FileError = ReadFailed;
            return;
        }

        if (picked is null)
        {
            return;
        }

        FileName = picked.Name;
        if (FileProblem(picked) is { } problem)
        {
            FileError = problem;
            return;
        }

        _bytes = picked.Content;
        _fileName = picked.Name;
        if (await LoadPreviewAsync(null).ConfigureAwait(true) is not { } preview)
        {
            FileError = Error;
            Error = null;
            return;
        }

        ShowColumns(preview);
        Step = CustomerImportStep.Mapping;
    }

    private static string? FileProblem(PickedBytes file) => file switch
    {
        _ when !Extensions.Contains(Path.GetExtension(file.Name), StringComparer.OrdinalIgnoreCase) => UnsupportedFile,
        { Size: > MaxBytes } => TooBig,
        { Content.Length: 0 } => EmptyFile,
        _ => null,
    };

    // ---------- Schritt 2: Spalten zuordnen ----------

    private void ShowColumns(CustomerImportPreview preview)
    {
        Columns.Clear();
        foreach (var column in preview.Columns)
        {
            var field = preview.Mapping.Columns.Where(m => m.Column == column.Index).Select(m => (CustomerImportField?)m.Field).FirstOrDefault();
            var row = new ImportColumnRow(column, field);
            row.Watch(OnColumnChanged);
            Columns.Add(row);
        }

        SelectedDateFormat = ImportDateFormatChoice.For(preview.Mapping.DateFormat);
        OnPropertyChanged(nameof(MappingError));
        ContinueCommand.NotifyCanExecuteChanged();
    }

    /// <summary>Ein Feld gehört nur einer Spalte: wer es wählt, nimmt es der Spalte weg, die es bisher hatte (sie steht dann auf „nicht importieren“).</summary>
    private void OnColumnChanged(ImportColumnRow changed)
    {
        if (changed.Choice.Field is { } field)
        {
            foreach (var other in Columns.Where(c => c != changed && c.Choice.Field == field))
            {
                other.Choice = ImportFieldChoice.For(null);
            }
        }

        OnPropertyChanged(nameof(MappingError));
        ContinueCommand.NotifyCanExecuteChanged();
    }

    private CustomerImportMapping CurrentMapping() => new(
        [.. Columns.Where(c => c.Choice.Field is not null).Select(c => new ImportColumnMapping(c.Index, c.Choice.Field!.Value))],
        SelectedDateFormat.Format);

    // ---------- Schritt 3: Vorschau und Prüfung ----------

    private async Task ReviewAsync()
    {
        Error = null;
        if (await LoadPreviewAsync(CurrentMapping()).ConfigureAwait(true) is not { } preview)
        {
            return;
        }

        SampleRows.Clear();
        foreach (var row in preview.Rows)
        {
            SampleRows.Add(ImportPreviewRow.From(row));
        }

        DuplicateRows.Clear();
        foreach (var row in preview.DuplicateRows)
        {
            DuplicateRows.Add(new ImportDuplicateRow(ImportPreviewRow.From(row)));
        }

        InvalidRows.Clear();
        foreach (var row in preview.InvalidRows)
        {
            InvalidRows.Add(ImportPreviewRow.From(row));
        }

        Summary = string.Create(CultureInfo.InvariantCulture, $"{preview.New} neu, {preview.Duplicates} Dubletten, {preview.Invalid} fehlerhaft");
        OnPropertyChanged(nameof(HasDuplicates));
        OnPropertyChanged(nameof(HasInvalid));
        SavedText = null;
        Step = CustomerImportStep.Review;
    }

    private async Task<CustomerImportPreview?> LoadPreviewAsync(CustomerImportMapping? mapping)
    {
        IsBusy = true;
        try
        {
            using var form = Form(mapping, decisions: null, dryRun: null);
            var result = await _api.PostAsync<CustomerImportPreview>("api/customers/import/preview", form).ConfigureAwait(true);
            if (result.Value is not { } preview)
            {
                Error = result.Error ?? NotChecked;
                return null;
            }

            Preview = preview;
            return preview;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanSaveErrors() => !IsBusy;

    /// <summary>„Fehlerzeilen als Excel …“: die fehlerhaften Zeilen mit Zeile und Grund, zum Berichtigen und erneuten Importieren.</summary>
    [RelayCommand(CanExecute = nameof(CanSaveErrors))]
    private async Task SaveErrorsAsync()
    {
        Error = null;
        SavedText = null;
        IsBusy = true;
        try
        {
            using var form = Form(CurrentMapping(), decisions: null, dryRun: null);
            var result = await _api.PostFileAsync("api/customers/import/errors", form).ConfigureAwait(true);
            if (result.Value is not { } file)
            {
                Error = result.Error ?? NotChecked;
                return;
            }

            var name = file.FileName ?? "Importfehler.xlsx";
            if (_saver() is not { } saver)
            {
                Error = NotSaved;
                return;
            }

            if (await saver.SaveAsync(name, "Excel-Arbeitsmappe", ["*.xlsx"], file.Content).ConfigureAwait(true))
            {
                SavedText = $"Gespeichert: {name}";
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Error = NotSaved;
        }
        finally
        {
            IsBusy = false;
        }
    }

    // ---------- Schritt 4: Probelauf und Import ----------

    private bool CanCheck() => !IsBusy && Step == CustomerImportStep.Run;

    /// <summary>„Nur prüfen“: derselbe Aufruf wie der Import mit <c>dryRun=true</c>; es wird nichts gespeichert.</summary>
    [RelayCommand(CanExecute = nameof(CanCheck))]
    private Task CheckAsync() => RunAsync(dryRun: true);

    private async Task RunAsync(bool dryRun)
    {
        Error = null;
        DryRunText = null;
        IsBusy = true;
        try
        {
            using var form = Form(CurrentMapping(), [.. DuplicateRows.Select(d => d.Decision)], dryRun);
            var result = await _api.PostAsync<CustomerImportResult>("api/customers/import", form).ConfigureAwait(true);
            if (result.Value is not { } import)
            {
                Error = result.Error ?? (dryRun ? NotChecked : "Der Import ist fehlgeschlagen. Verbindung prüfen und erneut versuchen.");
                return;
            }

            if (dryRun)
            {
                DryRunText = $"Probelauf: {ResultText(import)}. Es wurde nichts gespeichert.";
                return;
            }

            Outcome = import;
            Answer(new ConfirmResult(ConfirmChoice.Confirmed));
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>„240 neu, 12 ergänzt, 3 übersprungen“, bei Fehlern dazu „, 2 fehlerhaft“.</summary>
    public static string ResultText(CustomerImportResult result)
    {
        var text = string.Create(CultureInfo.InvariantCulture, $"{result.Created} neu, {result.Merged} ergänzt, {result.Skipped} übersprungen");
        return result.Failed > 0 ? string.Create(CultureInfo.InvariantCulture, $"{text}, {result.Failed} fehlerhaft") : text;
    }

    // ---------- Formular ----------

    /// <summary>Die Felder, die die API erwartet: <c>file</c>, dazu <c>mapping</c>, <c>decisions</c> und <c>dryRun</c> (je nach Schritt).</summary>
    private MultipartFormDataContent Form(CustomerImportMapping? mapping, IReadOnlyList<ImportDuplicateDecision>? decisions, bool? dryRun)
    {
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(_bytes);
        file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
        form.Add(file, "file", _fileName);
        if (mapping is not null)
        {
            form.Add(new StringContent(JsonSerializer.Serialize(mapping, ApiJson.Options)), "mapping");
        }

        if (decisions is not null)
        {
            form.Add(new StringContent(JsonSerializer.Serialize(decisions, ApiJson.Options)), "decisions");
        }

        if (dryRun is { } value)
        {
            form.Add(new StringContent(value ? "true" : "false"), "dryRun");
        }

        return form;
    }
}
