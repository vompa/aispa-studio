using System.Collections.ObjectModel;
using System.ComponentModel;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>Kanal der Terminerinnerung in der Auswahl.</summary>
public sealed record ReminderChannelOption(ReminderChannel Value, string Label)
{
    public override string ToString() => Label;
}

/// <summary>Was eine Ausnahme vom Standardabstand betrifft: eine Kategorie oder eine einzelne Behandlung.</summary>
public enum RebookExceptionKind
{
    Category,
    Treatment,
}

/// <summary>Eine Auswahl im Menü „+ Ausnahme“: eine Kategorie oder Behandlung, für die es noch keine Ausnahme gibt.</summary>
public sealed record RebookExceptionChoice(RebookExceptionKind Kind, string Label, Guid? TreatmentId, string? Category)
{
    public override string ToString() => Label;
}

/// <summary>Eine Ausnahme vom Standardabstand; die Tage leer heißen für sie kein Hinweis.</summary>
public sealed partial class RebookExceptionRow(RebookExceptionChoice choice, Action<RebookExceptionRow> remove) : ObservableObject
{
    public RebookExceptionKind Kind => choice.Kind;

    public Guid? TreatmentId => choice.TreatmentId;

    public string? Category => choice.Category;

    /// <summary>„Kategorie Gesicht“ oder der Name der Behandlung.</summary>
    public string Label => choice.Label;

    public string RemoveName => $"Ausnahme für {Label} entfernen";

    public string DaysName => $"Abstand in Tagen für {Label}";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoHint))]
    public partial decimal? Days { get; set; }

    /// <summary>Ohne Tage gibt es für diese Kategorie oder Behandlung keinen Hinweis; die Zeile sagt das neben dem Feld.</summary>
    public bool HasNoHint => Days is null;

    [RelayCommand]
    private void Remove() => remove(this);
}

/// <summary>
/// Erinnerungen des Spas als native Maske der Verwaltung (R05 Slice 96): Terminerinnerungen (<c>api/reminders/settings</c>) und die Wiederbuchungs-Hinweise
/// (<c>api/retention/settings</c>). Seit P8 (Auftraggeber, 17.11.2026) hat der Wiederbuchungs-Hinweis einen Standardabstand und eine kurze Liste von Ausnahmen je Kategorie
/// oder Behandlung statt eines Feldes je Behandlung (Tage leer = kein Hinweis), und „Zuletzt verschickt“ ist das Versandprotokoll als Seitenleiste (<see cref="DispatchLogViewModel"/>)
/// mit den Zahlen der letzten 30 Tage je Abschnitt. Ein „Speichern“ schickt nur, was sich geändert hat; geplant und versendet wird in der API nach dem gespeicherten Stand,
/// welcher Abstand für eine Behandlung gilt, rechnet sie ebenfalls.
/// </summary>
public sealed partial class RemindersViewModel : ViewModelBase, INativeArea
{
    private static readonly string[] Inputs =
        [nameof(Enabled), nameof(FirstHours), nameof(SecondHours), nameof(Channel), nameof(WhatsAppTemplate), nameof(WhatsAppLanguage), nameof(RebookEnabled), nameof(RebookDefaultDays)];

    /// <summary>Feldkennungen für den Fokus nach einem Fehler.</summary>
    public const string SecondHoursField = "second";

    public const string TemplateField = "template";

    public const string LanguageField = "language";

    private readonly IWorkstationApi _api;
    private List<TreatmentDto> _treatments = [];
    private ReminderSettingsDto? _savedReminders;
    private RebookSettingsDto? _savedRebook;
    private bool _applying;

    /// <param name="remindersOn">Modul „Terminerinnerungen“ (Terminerinnerung und Versandprotokoll); ausgeschaltet antwortet die API mit 404, die Maske lädt den Teil gar nicht (T-081).</param>
    /// <param name="rebookingOn">Modul „Wiederbuchung“ (in der Stufe „Start“ aus); ausgeschaltet gilt dasselbe.</param>
    public RemindersViewModel(IWorkstationApi api, TimeZoneInfo zone, TimeProvider? time = null, bool remindersOn = true, bool rebookingOn = true)
    {
        _api = api;
        RemindersOn = remindersOn;
        RebookingOn = rebookingOn;
        Log = new DispatchLogViewModel(api, zone, time);
    }

    public string Title => "Erinnerungen";

    /// <summary>Der Abschnitt „Terminerinnerungen“ mit Versandprotokoll gehört zur Maske.</summary>
    public bool RemindersOn { get; }

    /// <summary>Der Abschnitt „Wiederbuchungs-Hinweise“ gehört zur Maske.</summary>
    public bool RebookingOn { get; }

    public static IReadOnlyList<ReminderChannelOption> Channels { get; } =
        [new(ReminderChannel.Email, "Mail"), new(ReminderChannel.Sms, "SMS"), new(ReminderChannel.WhatsApp, "WhatsApp (freigegebene Vorlage)")];

    /// <summary>Das Versandprotokoll in der Seitenleiste.</summary>
    public DispatchLogViewModel Log { get; }

    /// <summary>Die Einstellungen wurden gespeichert; die Leiste „KI und Hilfe“ bietet dann an, eine Frage zu wiederholen.</summary>
    public event Action? Saved;

    [ObservableProperty]
    public partial bool IsLoaded { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLoadError))]
    public partial string? LoadError { get; private set; }

    public bool HasLoadError => LoadError is not null;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    public partial bool IsSaving { get; private set; }

    /// <summary>Erinnerungen verschicken.</summary>
    [ObservableProperty]
    public partial bool Enabled { get; set; }

    /// <summary>Erste Erinnerung (Stunden vorher, 0 = keine); leer heißt 0 wie im Web.</summary>
    [ObservableProperty]
    public partial decimal? FirstHours { get; set; }

    /// <summary>Zweite Erinnerung (Stunden vorher, 0 = keine).</summary>
    [ObservableProperty]
    public partial decimal? SecondHours { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsWhatsApp))]
    public partial ReminderChannelOption Channel { get; set; } = Channels[0];

    /// <summary>Vorlage und Sprache gibt es nur für WhatsApp.</summary>
    public bool IsWhatsApp => Channel.Value == ReminderChannel.WhatsApp;

    [ObservableProperty]
    public partial string WhatsAppTemplate { get; set; } = "";

    [ObservableProperty]
    public partial string WhatsAppLanguage { get; set; } = "";

    /// <summary>Wiederbuchungs-Hinweise verschicken.</summary>
    [ObservableProperty]
    public partial bool RebookEnabled { get; set; }

    /// <summary>Standardabstand in Tagen für alle Behandlungen ohne Ausnahme; leer heißt kein Standard.</summary>
    [ObservableProperty]
    public partial decimal? RebookDefaultDays { get; set; }

    /// <summary>Die Ausnahmen vom Standardabstand: erst Kategorien, dann Behandlungen, je nach Name.</summary>
    public ObservableCollection<RebookExceptionRow> Exceptions { get; } = [];

    /// <summary>Kategorien und Behandlungen, für die es noch keine Ausnahme gibt (Auswahl „+ Ausnahme“).</summary>
    public ObservableCollection<RebookExceptionChoice> ExceptionChoices { get; } = [];

    /// <summary>Die Auswahl „+ Ausnahme“: wer eine Kategorie oder Behandlung wählt, bekommt die Zeile; die Auswahl springt danach auf ihren Hinweistext zurück.</summary>
    [ObservableProperty]
    public partial RebookExceptionChoice? NewException { get; set; }

    public bool HasExceptions => Exceptions.Count > 0;

    public bool HasExceptionChoices => ExceptionChoices.Count > 0;

    /// <summary>„Letzte 30 Tage: 34 verschickt, 0 fehlgeschlagen“ unter den Terminerinnerungen.</summary>
    [ObservableProperty]
    public partial string? ReminderSummary { get; private set; }

    /// <summary>„1 fehlgeschlagen“ als rote Marke neben der Zusammenfassung; nur wenn etwas fehlgeschlagen ist.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasReminderFailed))]
    public partial string? ReminderFailed { get; private set; }

    public bool HasReminderFailed => ReminderFailed is not null;

    [ObservableProperty]
    public partial string? RebookSummary { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasRebookFailed))]
    public partial string? RebookFailed { get; private set; }

    public bool HasRebookFailed => RebookFailed is not null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? Error { get; private set; }

    public bool HasError => Error is not null;

    [ObservableProperty]
    public partial string? StatusText { get; private set; }

    /// <summary>Fehler am Feld „Zweite Erinnerung“.</summary>
    [ObservableProperty]
    public partial string? SecondHoursError { get; private set; }

    /// <summary>Fehler am Feld der WhatsApp-Vorlage.</summary>
    [ObservableProperty]
    public partial string? TemplateError { get; private set; }

    /// <summary>Fehler am Feld der Sprache der Vorlage.</summary>
    [ObservableProperty]
    public partial string? LanguageError { get; private set; }

    /// <summary>Das Feld, das den Fokus bekommen soll; die Ansicht setzt ihn und leert die Angabe.</summary>
    [ObservableProperty]
    public partial string? FocusTarget { get; set; }

    public bool HasUnsavedChanges =>
        IsLoaded
        && ((RemindersOn && _savedReminders is not null && ReminderInput() != _savedReminders)
            || (RebookingOn && _savedRebook is not null && !SameRebook(RebookInput(), _savedRebook)));

    [RelayCommand]
    public async Task LoadAsync()
    {
        LoadError = null;
        // Nur die eingeschalteten Teile fragen (T-081): ein ausgeschaltetes Modul antwortet mit 404 und ließe die ganze Maske mit Fehler laden.
        var reminders = RemindersOn ? await _api.GetAsync<ReminderSettingsDto>("api/reminders/settings").ConfigureAwait(true) : null;
        var rebook = RebookingOn ? await _api.GetAsync<RebookSettingsDto>("api/retention/settings").ConfigureAwait(true) : null;
        var treatments = RebookingOn ? await _api.GetAsync<List<TreatmentDto>>("api/treatments").ConfigureAwait(true) : null;
        // Die Zahlen der letzten 30 Tage sind nur zum Nachsehen; fehlen sie, bleibt die Maske bedienbar.
        var summary = RemindersOn ? await _api.GetAsync<DispatchSummaryDto>("api/dispatch-log/summary").ConfigureAwait(true) : null;
        if ((reminders is not null && reminders.Value is null) || (rebook is not null && rebook.Value is null) || (treatments is not null && treatments.Value is null))
        {
            LoadError = reminders?.Error ?? rebook?.Error ?? treatments?.Error ?? "Die Erinnerungen wurden nicht geladen.";
            return;
        }

        _treatments = treatments?.Value ?? [];
        ShowSummary(summary?.Value);
        if (reminders?.Value is { } settings)
        {
            ApplyReminders(settings);
        }

        if (rebook?.Value is { } rebookSettings)
        {
            ApplyRebook(rebookSettings);
        }

        IsLoaded = true;
    }

    public Task EnsureLoadedAsync() => IsLoaded ? Task.CompletedTask : LoadAsync();

    private bool CanSave => !IsSaving;

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync() => await SaveChangesAsync().ConfigureAwait(true);

    /// <summary>Öffnet das Versandprotokoll mit den Terminerinnerungen.</summary>
    [RelayCommand]
    private Task OpenReminderLogAsync() => Log.OpenAsync(DispatchFilter.Reminder);

    /// <summary>Öffnet das Versandprotokoll mit den Wiederbuchungs-Hinweisen.</summary>
    [RelayCommand]
    private Task OpenRebookLogAsync() => Log.OpenAsync(DispatchFilter.Rebook);

    /// <summary>Fügt eine Ausnahme ohne Tage hinzu (kein Hinweis); die Tage trägt man danach ein.</summary>
    [RelayCommand]
    private void AddException(RebookExceptionChoice? choice)
    {
        if (choice is null || Exceptions.Any(e => Matches(e, choice)))
        {
            return;
        }

        InsertException(new RebookExceptionRow(choice, RemoveException));
        RefreshChoices();
        Changed();
    }

    /// <summary>Schickt nur die geänderten Teile; der erste Fehler hält an.</summary>
    public async Task<bool> SaveChangesAsync()
    {
        if (IsSaving || (RemindersOn && _savedReminders is null) || (RebookingOn && _savedRebook is null))
        {
            return false;
        }

        if (!Validate())
        {
            return false;
        }

        Error = null;
        IsSaving = true;
        try
        {
            if (RemindersOn && ReminderInput() is var reminders && reminders != _savedReminders)
            {
                var result = await _api.PutAsync<ReminderSettingsDto>("api/reminders/settings", reminders).ConfigureAwait(true);
                if (result.Value is not { } settings)
                {
                    Error = result.Error ?? "Die Terminerinnerungen wurden nicht gespeichert.";
                    return false;
                }

                ApplyReminders(settings);
            }

            if (RebookingOn && RebookInput() is var rebook && !SameRebook(rebook, _savedRebook!))
            {
                var result = await _api.PutAsync<RebookSettingsDto>("api/retention/settings", rebook).ConfigureAwait(true);
                if (result.Value is not { } settings)
                {
                    Error = result.Error ?? "Die Wiederbuchungs-Hinweise wurden nicht gespeichert.";
                    return false;
                }

                ApplyRebook(settings);
            }
        }
        finally
        {
            IsSaving = false;
        }

        StatusText = "Gespeichert.";
        Saved?.Invoke();
        return true;
    }

    [RelayCommand]
    public void Discard()
    {
        if (_savedReminders is not null)
        {
            ApplyReminders(_savedReminders);
        }

        if (_savedRebook is not null)
        {
            ApplyRebook(_savedRebook);
        }

        Error = null;
        SecondHoursError = null;
        TemplateError = null;
        LanguageError = null;
    }

    [RelayCommand]
    private void CloseError() => Error = null;

    /// <summary>Die Erinnerungen haben keine Einträge zum Anspringen; ein Sprung aus Hilfe oder Assistent öffnet höchstens die Maske.</summary>
    public void Open(Guid? id, string? section, string? field)
    {
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (_applying || e.PropertyName is not { } name || !Inputs.Contains(name))
        {
            return;
        }

        switch (name)
        {
            case nameof(FirstHours) or nameof(SecondHours):
                SecondHoursError = null;
                break;
            case nameof(WhatsAppTemplate) or nameof(Channel):
                TemplateError = null;
                break;
            case nameof(WhatsAppLanguage):
                LanguageError = null;
                break;
        }

        Changed();
    }

    partial void OnNewExceptionChanged(RebookExceptionChoice? value)
    {
        if (value is null)
        {
            return;
        }

        AddException(value);
        NewException = null;
    }

    private void Changed()
    {
        StatusText = null;
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    /// <summary>Was gespeichert würde; leere Stunden heißen 0, eine leere Vorlage „keine“ wie im Web.</summary>
    private ReminderSettingsDto ReminderInput() => new(Enabled, (int)(FirstHours ?? 0), (int)(SecondHours ?? 0), Channel.Value,
        WhatsAppTemplate.Length == 0 ? null : WhatsAppTemplate, WhatsAppLanguage);

    /// <summary>
    /// Was gespeichert würde: Standard und Ausnahmen aus der Maske. Ausnahmen für Behandlungen, die nicht mehr angeboten werden (ohne Zeile in der Maske),
    /// gehen unverändert mit, wie im Web.
    /// </summary>
    private RebookSettingsDto RebookInput()
    {
        var offered = _treatments.Select(t => t.Id).ToHashSet();
        var saved = _savedRebook;
        var treatmentRows = Exceptions.Where(e => e.Kind == RebookExceptionKind.Treatment).ToList();
        return new RebookSettingsDto(RebookEnabled, [
            .. treatmentRows.Where(r => r.Days is not null).Select(r => new RebookIntervalDto(r.TreatmentId!.Value, (int)r.Days!.Value)),
            .. (saved?.Intervals ?? []).Where(i => !offered.Contains(i.TreatmentId)),
        ])
        {
            DefaultDays = RebookDefaultDays is { } days ? (int)days : null,
            CategoryIntervals = [.. Exceptions.Where(e => e.Kind == RebookExceptionKind.Category).Select(r => new RebookCategoryIntervalDto(r.Category!, r.Days is { } d ? (int)d : null))],
            SilentTreatmentIds = [
                .. treatmentRows.Where(r => r.Days is null).Select(r => r.TreatmentId!.Value),
                .. (saved?.SilentTreatmentIds ?? []).Where(id => !offered.Contains(id)),
            ],
        };
    }

    /// <summary>Die Regeln der API, die sich vorher am Feld zeigen lassen (masken.md, R8); das erste ungültige Feld bekommt den Fokus.</summary>
    private bool Validate()
    {
        if (!RemindersOn)
        {
            return true;
        }

        var first = (int)(FirstHours ?? 0);
        var second = (int)(SecondHours ?? 0);
        SecondHoursError = first == 0 || second == 0 || first > second ? null : "Die zweite Erinnerung muss näher am Termin liegen als die erste.";
        TemplateError = !IsWhatsApp || WhatsAppTemplate.Trim().Length > 0 ? null : "Für WhatsApp braucht es eine freigegebene Vorlage.";
        LanguageError = WhatsAppLanguage.Trim().Length > 0 ? null : "Sprache der Vorlage, z. B. „de“.";
        FocusTarget = SecondHoursError is not null ? SecondHoursField
            : TemplateError is not null ? TemplateField
            : LanguageError is not null ? LanguageField
            : null;
        return FocusTarget is null;
    }

    private static bool SameRebook(RebookSettingsDto a, RebookSettingsDto b) =>
        a.Enabled == b.Enabled
        && a.DefaultDays == b.DefaultDays
        && a.Intervals.OrderBy(i => i.TreatmentId).SequenceEqual(b.Intervals.OrderBy(i => i.TreatmentId))
        && (a.CategoryIntervals ?? []).OrderBy(c => c.Category, StringComparer.OrdinalIgnoreCase)
            .SequenceEqual((b.CategoryIntervals ?? []).OrderBy(c => c.Category, StringComparer.OrdinalIgnoreCase))
        && (a.SilentTreatmentIds ?? []).Order().SequenceEqual((b.SilentTreatmentIds ?? []).Order());

    private void ApplyReminders(ReminderSettingsDto settings)
    {
        _applying = true;
        _savedReminders = settings;
        Enabled = settings.Enabled;
        FirstHours = settings.FirstHoursBefore;
        SecondHours = settings.SecondHoursBefore;
        Channel = Channels.First(c => c.Value == settings.Channel);
        WhatsAppTemplate = settings.WhatsAppTemplate ?? "";
        WhatsAppLanguage = settings.WhatsAppTemplateLanguage;
        _applying = false;
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    private void ApplyRebook(RebookSettingsDto settings)
    {
        _applying = true;
        _savedRebook = settings;
        RebookEnabled = settings.Enabled;
        RebookDefaultDays = settings.DefaultDays;
        foreach (var row in Exceptions)
        {
            row.PropertyChanged -= OnRowChanged;
        }

        Exceptions.Clear();
        foreach (var category in settings.CategoryIntervals ?? [])
        {
            InsertException(new RebookExceptionRow(new(RebookExceptionKind.Category, $"Kategorie {category.Category}", null, category.Category), RemoveException)
            {
                Days = category.Days,
            });
        }

        var names = _treatments.ToDictionary(t => t.Id, t => t.Name);
        foreach (var interval in settings.Intervals.Where(i => names.ContainsKey(i.TreatmentId)))
        {
            InsertException(new RebookExceptionRow(new(RebookExceptionKind.Treatment, names[interval.TreatmentId], interval.TreatmentId, null), RemoveException)
            {
                Days = interval.Days,
            });
        }

        foreach (var id in (settings.SilentTreatmentIds ?? []).Where(names.ContainsKey))
        {
            InsertException(new RebookExceptionRow(new(RebookExceptionKind.Treatment, names[id], id, null), RemoveException));
        }

        RefreshChoices();
        _applying = false;
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    /// <summary>Setzt die Zeile an ihren Platz (Kategorien vor Behandlungen, je nach Name) und hört auf ihre Tage.</summary>
    private void InsertException(RebookExceptionRow row)
    {
        var index = Exceptions.Count;
        for (var i = 0; i < Exceptions.Count; i++)
        {
            var other = Exceptions[i];
            if (other.Kind > row.Kind || (other.Kind == row.Kind && StringComparer.CurrentCultureIgnoreCase.Compare(other.Label, row.Label) > 0))
            {
                index = i;
                break;
            }
        }

        row.PropertyChanged += OnRowChanged;
        Exceptions.Insert(index, row);
        OnPropertyChanged(nameof(HasExceptions));
    }

    private void RemoveException(RebookExceptionRow row)
    {
        row.PropertyChanged -= OnRowChanged;
        Exceptions.Remove(row);
        OnPropertyChanged(nameof(HasExceptions));
        RefreshChoices();
        Changed();
    }

    /// <summary>Alle Kategorien und angebotenen Behandlungen ohne Ausnahme: erst die Kategorien, dann die Behandlungen.</summary>
    private void RefreshChoices()
    {
        ExceptionChoices.Clear();
        var categories = _treatments.Select(t => t.Category?.Trim()).Where(c => !string.IsNullOrEmpty(c)).Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.CurrentCultureIgnoreCase);
        foreach (var category in categories)
        {
            AddChoice(new RebookExceptionChoice(RebookExceptionKind.Category, $"Kategorie {category}", null, category));
        }

        foreach (var treatment in _treatments.OrderBy(t => t.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            AddChoice(new RebookExceptionChoice(RebookExceptionKind.Treatment, treatment.Name, treatment.Id, null));
        }

        OnPropertyChanged(nameof(HasExceptionChoices));
    }

    private void AddChoice(RebookExceptionChoice choice)
    {
        if (!Exceptions.Any(e => Matches(e, choice)))
        {
            ExceptionChoices.Add(choice);
        }
    }

    private static bool Matches(RebookExceptionRow row, RebookExceptionChoice choice) =>
        row.Kind == choice.Kind && (choice.Kind == RebookExceptionKind.Treatment
            ? row.TreatmentId == choice.TreatmentId
            : string.Equals(row.Category, choice.Category, StringComparison.OrdinalIgnoreCase));

    private void ShowSummary(DispatchSummaryDto? summary)
    {
        (ReminderSummary, ReminderFailed) = Summarize(summary?.Reminders, summary?.Days ?? 30);
        (RebookSummary, RebookFailed) = Summarize(summary?.Rebook, summary?.Days ?? 30);
    }

    /// <summary>„Letzte 30 Tage: 34 verschickt, 0 fehlgeschlagen“; ist etwas fehlgeschlagen, steht es als eigene Marke daneben („1 fehlgeschlagen“).</summary>
    private static (string? Line, string? Failed) Summarize(DispatchKindSummaryDto? counts, int days)
    {
        if (counts is null)
        {
            return (null, null);
        }

        if (counts.Sent + counts.Failed + counts.Skipped == 0)
        {
            return ($"Letzte {days} Tage: noch nichts verschickt", null);
        }

        return counts.Failed == 0
            ? ($"Letzte {days} Tage: {counts.Sent} verschickt, 0 fehlgeschlagen", null)
            : ($"Letzte {days} Tage: {counts.Sent} verschickt,", $"{counts.Failed} fehlgeschlagen");
    }

    private void OnRowChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!_applying && e.PropertyName == nameof(RebookExceptionRow.Days))
        {
            Changed();
        }
    }
}
