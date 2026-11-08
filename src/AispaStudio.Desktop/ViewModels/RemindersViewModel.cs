using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
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

/// <summary>Abstand des Wiederbuchungs-Hinweises für eine Behandlung; leer heißt keine.</summary>
public sealed partial class RebookIntervalRow(Guid treatmentId, string label) : ObservableObject
{
    public Guid TreatmentId { get; } = treatmentId;

    /// <summary>„Hot Stone 60 (Tage, leer = keine)“ wie im Web.</summary>
    public string Label { get; } = label;

    [ObservableProperty]
    public partial decimal? Days { get; set; }
}

/// <summary>Ein zuletzt verschickter Wiederbuchungs-Hinweis; der Text der Mail steht im Hinweis.</summary>
public sealed record RebookReminderRow(string Line, string? Text)
{
    public override string ToString() => Line;
}

/// <summary>
/// Erinnerungen des Spas als native Maske der Verwaltung (R05 Slice 96): Terminerinnerungen (<c>api/reminders/settings</c>) und die Wiederbuchungs-Hinweise
/// (<c>api/retention/settings</c>) mit denselben Feldern, Grenzen und Hilfen wie <c>web/src/admin/RemindersTab.tsx</c> und <c>RebookSection.tsx</c>.
/// Ein „Speichern“ schickt nur, was sich geändert hat; geplant und versendet wird in der API nach dem gespeicherten Stand.
/// </summary>
public sealed partial class RemindersViewModel(IWorkstationApi api, TimeZoneInfo zone) : ViewModelBase, INativeArea
{
    private static readonly string[] Inputs =
        [nameof(Enabled), nameof(FirstHours), nameof(SecondHours), nameof(Channel), nameof(WhatsAppTemplate), nameof(WhatsAppLanguage), nameof(RebookEnabled)];

    private static readonly Dictionary<string, string> States = new()
    {
        ["Queued"] = "wird geschrieben",
        ["Sent"] = "gesendet",
        ["Skipped"] = "entfallen",
        ["Failed"] = "fehlgeschlagen",
    };

    /// <summary>Feldkennungen für den Fokus nach einem Fehler.</summary>
    public const string SecondHoursField = "second";

    public const string TemplateField = "template";

    public const string LanguageField = "language";

    private ReminderSettingsDto? _savedReminders;
    private RebookSettingsDto? _savedRebook;
    private bool _applying;

    public string Title => "Erinnerungen";

    public static IReadOnlyList<ReminderChannelOption> Channels { get; } =
        [new(ReminderChannel.Email, "Mail"), new(ReminderChannel.Sms, "SMS"), new(ReminderChannel.WhatsApp, "WhatsApp (freigegebene Vorlage)")];

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

    /// <summary>Abstand je Behandlung in der Reihenfolge der Behandlungsliste.</summary>
    public ObservableCollection<RebookIntervalRow> Intervals { get; } = [];

    /// <summary>Zuletzt verschickt.</summary>
    public ObservableCollection<RebookReminderRow> Recent { get; } = [];

    public bool HasRecent => Recent.Count > 0;

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
        _savedReminders is not null && _savedRebook is not null && (ReminderInput() != _savedReminders || !SameRebook(RebookInput(), _savedRebook));

    [RelayCommand]
    public async Task LoadAsync()
    {
        LoadError = null;
        var reminders = await api.GetAsync<ReminderSettingsDto>("api/reminders/settings").ConfigureAwait(true);
        var rebook = await api.GetAsync<RebookSettingsDto>("api/retention/settings").ConfigureAwait(true);
        var treatments = await api.GetAsync<List<TreatmentDto>>("api/treatments").ConfigureAwait(true);
        // „Zuletzt verschickt“ ist nur zum Nachsehen; fehlt die Liste, bleibt die Maske bedienbar (wie im Web).
        var recent = await api.GetAsync<List<RebookReminderDto>>("api/retention/reminders").ConfigureAwait(true);
        if (reminders.Value is not { } settings || rebook.Value is not { } rebookSettings || treatments.Value is not { } list)
        {
            LoadError = reminders.Error ?? rebook.Error ?? treatments.Error ?? "Die Erinnerungen wurden nicht geladen.";
            return;
        }

        foreach (var row in Intervals)
        {
            row.PropertyChanged -= OnRowChanged;
        }

        Intervals.Clear();
        foreach (var treatment in list)
        {
            var row = new RebookIntervalRow(treatment.Id, $"{treatment.Name} (Tage, leer = keine)");
            row.PropertyChanged += OnRowChanged;
            Intervals.Add(row);
        }

        Recent.Clear();
        foreach (var reminder in recent.Value ?? [])
        {
            Recent.Add(Row(reminder));
        }

        OnPropertyChanged(nameof(HasRecent));
        ApplyReminders(settings);
        ApplyRebook(rebookSettings);
        IsLoaded = true;
    }

    public Task EnsureLoadedAsync() => IsLoaded ? Task.CompletedTask : LoadAsync();

    private bool CanSave => !IsSaving;

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync() => await SaveChangesAsync().ConfigureAwait(true);

    /// <summary>Schickt nur die geänderten Teile; der erste Fehler hält an.</summary>
    public async Task<bool> SaveChangesAsync()
    {
        if (IsSaving || _savedReminders is null || _savedRebook is null)
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
            if (ReminderInput() is var reminders && reminders != _savedReminders)
            {
                var result = await api.PutAsync<ReminderSettingsDto>("api/reminders/settings", reminders).ConfigureAwait(true);
                if (result.Value is not { } settings)
                {
                    Error = result.Error ?? "Die Terminerinnerungen wurden nicht gespeichert.";
                    return false;
                }

                ApplyReminders(settings);
            }

            if (RebookInput() is var rebook && !SameRebook(rebook, _savedRebook))
            {
                var result = await api.PutAsync<RebookSettingsDto>("api/retention/settings", rebook).ConfigureAwait(true);
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

    /// <summary>Die Erinnerungen haben keine Einträge und Abschnitte zum Anspringen.</summary>
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

    private void Changed()
    {
        StatusText = null;
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    /// <summary>Was gespeichert würde; leere Stunden heißen 0, eine leere Vorlage „keine“ wie im Web.</summary>
    private ReminderSettingsDto ReminderInput() => new(Enabled, (int)(FirstHours ?? 0), (int)(SecondHours ?? 0), Channel.Value,
        WhatsAppTemplate.Length == 0 ? null : WhatsAppTemplate, WhatsAppLanguage);

    /// <summary>Abstände von Behandlungen ohne Zeile (nicht mehr angeboten) gehen unverändert mit, wie im Web.</summary>
    private RebookSettingsDto RebookInput()
    {
        var shown = Intervals.Select(r => r.TreatmentId).ToHashSet();
        return new(RebookEnabled, [
            .. Intervals.Where(r => r.Days is not null).Select(r => new RebookIntervalDto(r.TreatmentId, (int)r.Days!.Value)),
            .. (_savedRebook?.Intervals ?? []).Where(i => !shown.Contains(i.TreatmentId)),
        ]);
    }

    /// <summary>Die Regeln der API, die sich vorher am Feld zeigen lassen (masken.md, R8); das erste ungültige Feld bekommt den Fokus.</summary>
    private bool Validate()
    {
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
        a.Enabled == b.Enabled && a.Intervals.OrderBy(i => i.TreatmentId).SequenceEqual(b.Intervals.OrderBy(i => i.TreatmentId));

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
        var days = settings.Intervals.ToDictionary(i => i.TreatmentId, i => i.Days);
        foreach (var row in Intervals)
        {
            row.Days = days.TryGetValue(row.TreatmentId, out var d) ? d : null;
        }

        _applying = false;
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    private void OnRowChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!_applying)
        {
            Changed();
        }
    }

    /// <summary>„Lena Huber, Hot Stone 60: gesendet (Text von der KI) 19.10.26, 11:40“ wie im Web.</summary>
    private RebookReminderRow Row(RebookReminderDto reminder)
    {
        var at = TimeZoneInfo.ConvertTime(reminder.SentAt ?? reminder.CreatedAt, zone);
        var line = $"{reminder.CustomerName}, {reminder.TreatmentName}: {States.GetValueOrDefault(reminder.State, reminder.State)}"
            + (reminder.AiWritten ? " (Text von der KI)" : "")
            + $" {at.ToString("dd.MM.yy, HH:mm", CultureInfo.InvariantCulture)}"
            + (reminder.Error is { } error ? $" ({error})" : "");
        return new RebookReminderRow(line, reminder.Text);
    }
}
