using AispaStudio.Contracts;
using AispaStudio.Desktop.Controls;
using AispaStudio.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>Eine Art in der Auswahl (Optionsfelder): ein Wert des Aufzählungstyps der Maske, seine Beschriftung und ob er gerade gewählt ist.</summary>
public sealed class KindOption(object value, string label, Func<object> current, Action<object> select) : ObservableObject
{
    public object Value { get; } = value;

    public string Label { get; } = label;

    /// <summary>Setzen auf wahr wählt die Art; ein Optionsfeld wird nur durch die Wahl eines anderen abgewählt.</summary>
    public bool IsSelected
    {
        get => Equals(current(), Value);
        set
        {
            if (value)
            {
                select(Value);
            }
        }
    }

    public void Refresh() => OnPropertyChanged(nameof(IsSelected));
}

/// <summary>
/// Gemeinsame Grundlage der Formulare „Schließzeit“ und „Raumsperre“ (R05 Slice 95; seit ADR 0098 die Seitenleiste der Seite „Schließzeiten“, die beide Arten in einer Liste zeigt):
/// das Formular eines Eintrags oder eines neuen (Zeitraum in <see cref="Period"/>, Art und Texte im Formular), Speichern mit Strg+S, Verwerfen und Löschen mit Rückfrage. Standort und Liste
/// gehören der Seite (<see cref="ClosuresPageViewModel"/>): sie setzt den Standort (<see cref="SetLocation"/>) und öffnet einen Eintrag (<see cref="OpenAsync"/>) oder ein neues Formular
/// (<see cref="StartNew"/>); nach Speichern, Löschen und Absagen ruft das Formular <see cref="Refresh"/>, damit die Liste neu lädt. Wer mit ungespeicherten Änderungen den Eintrag wechselt, wird
/// von der Seite gefragt (<see cref="UnsavedChanges"/>). Prüfen und Speichern macht die API; vorab prüft das Formular die Regeln des Servers, damit der Fehler am Feld steht.
/// Rückmeldungen zu einer Aktion (Gespeichert, Gelöscht, Termine abgesagt) gehen als Toast an die Seite (<see cref="ToastRequested"/>, meldungen.md M1), Fehler stehen im Formular.
/// </summary>
public abstract partial class PeriodAreaViewModel : ViewModelBase, IEditableArea
{
    private readonly TimeProvider _time;
    private LocationDto? _location;
    private AdminTarget? _newTarget;
    private string? _savedSnapshot;
    private bool _applying;

    protected PeriodAreaViewModel(IWorkstationApi api, CurrentUser user, TimeProvider time, string title, string firstDayMissing, string lastDayMissing)
    {
        Api = api;
        User = user;
        _time = time;
        Title = title;
        Period = new PeriodFormViewModel(firstDayMissing, lastDayMissing, () => Today);
        Period.Changed += Touch;
    }

    public string Title { get; }

    /// <summary>Von, Bis und die Uhrzeiten eines Teil-Tages.</summary>
    public PeriodFormViewModel Period { get; }

    /// <summary>Das Formular hat gespeichert, gelöscht oder Termine abgesagt; die Leiste „KI und Hilfe“ bietet dann an, eine Frage zu wiederholen.</summary>
    public event Action? Saved;

    /// <summary>Eine Rückmeldung zu einer Aktion für den Toast der Seite (eine Zeile, ohne Schlusspunkt).</summary>
    public event Action<string>? ToastRequested;

    /// <summary>Der Eintrag wurde gelöscht; die Seite schließt die Seitenleiste.</summary>
    public event Action? Removed;

    /// <summary>Lädt die Liste der Seite neu; das Formular wartet darauf, bevor es seine Aktion beendet.</summary>
    public Func<Task>? Refresh { get; set; }

    public bool HasForm => IsCreating || CurrentId is not null;

    public bool CanDelete => HasForm && !IsCreating && CanDeleteItem;

    public bool HasPhase => PhaseText.Length > 0;

    public bool HasOpenNotice => OpenNotice is not null;

    public bool HasCreatedText => CreatedText is not null;

    public bool HasMessage => Message is not null;

    public bool HasError => Error is not null;

    public bool HasUnsavedChanges => HasForm && _savedSnapshot is not null && Snapshot() != _savedSnapshot;

    // ---------- Texte des Formulars ----------

    /// <summary>„Neue Schließzeit“: Überschrift des neuen Formulars.</summary>
    public abstract string NewText { get; }

    public abstract string HelpTopic { get; }

    public abstract string HelpSection { get; }

    protected abstract string SaveFailedText { get; }

    /// <summary>Toast nach dem Speichern, z. B. „Schließzeit gespeichert“.</summary>
    protected abstract string SavedToast { get; }

    /// <summary>Toast nach dem Löschen, z. B. „Schließzeit gelöscht“.</summary>
    protected abstract string DeletedToast { get; }

    // ---------- Zustand ----------

    /// <summary>Ein neues Formular ist offen.</summary>
    [ObservableProperty]
    public partial bool IsCreating { get; private set; }

    /// <summary>Der Eintrag, dessen Formular offen ist; leer bei einem neuen.</summary>
    [ObservableProperty]
    public partial Guid? CurrentId { get; private set; }

    [ObservableProperty]
    public partial string HeadingText { get; private set; } = "";

    /// <summary>„Geplant“, „Läuft“ oder „Beendet“ (wie in der Liste); leer bei einem neuen Formular.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPhase))]
    public partial string PhaseText { get; private set; } = "";

    /// <summary>Warnung über Termine, die in der Sperre liegen und gebucht bleiben.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasOpenNotice))]
    public partial string? OpenNotice { get; private set; }

    /// <summary>„Angelegt von Katrin am 01.03.2027 08:00.“</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCreatedText))]
    public partial string? CreatedText { get; private set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    public partial bool IsSaving { get; private set; }

    /// <summary>Fehler über der Leiste: die Zusammenfassung nach einer Prüfung oder die Meldung der API.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? Error { get; private set; }

    /// <summary>Ein Fehler oder eine Warnung zu einer Aktion (Absagen, Nachricht), oben im Formular; sie bleibt, bis man sie schließt (meldungen.md M2).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMessage))]
    public partial string? Message { get; private set; }

    [ObservableProperty]
    public partial InfoBarSeverity MessageSeverity { get; private set; } = InfoBarSeverity.Warning;

    /// <summary>Das Feld, das den Fokus bekommen soll (Feldkennung); die Ansicht setzt ihn und leert die Angabe.</summary>
    [ObservableProperty]
    public partial string? FocusTarget { get; set; }

    // ---------- Ableitungen der Formulare ----------

    protected IWorkstationApi Api { get; }

    protected CurrentUser User { get; }

    protected LocationDto? CurrentLocation => _location;

    protected TimeZoneInfo Zone => _location is { } location ? TimeZoneInfo.FindSystemTimeZoneById(location.TimeZone) : _time.LocalTimeZone;

    /// <summary>Der heutige Tag am Standort.</summary>
    protected DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(_time.GetUtcNow(), Zone).DateTime);

    protected DateTimeOffset UtcNow => _time.GetUtcNow();

    protected virtual bool CanDeleteItem => true;

    /// <summary>Das erste Feld des Formulars, das bei einem neuen den Fokus bekommt.</summary>
    protected virtual string FirstField => PeriodFormViewModel.FromDateField;

    /// <summary>Lädt den Eintrag; liefert die Fehlermeldung oder nichts.</summary>
    protected abstract Task<string?> FetchAsync(Guid locationId, Guid id);

    /// <summary>Füllt das Formular aus dem geladenen Eintrag.</summary>
    protected abstract void ApplyItem();

    protected abstract void ApplyBlank(AdminTarget? target);

    /// <summary>Was das Formular über den Zeitraum hinaus eingibt, für die Frage „geändert?“.</summary>
    protected abstract string FormSignature();

    /// <summary>Prüft die übrigen Felder; liefert die Kennung des ersten fehlerhaften Felds oder nichts.</summary>
    protected abstract string? ValidateFields();

    protected abstract Task<(Guid? Id, string? Error)> SendAsync(Guid locationId, Guid? id, PeriodValue period);

    protected abstract Task<string?> DeleteItemAsync(Guid locationId, Guid id);

    protected abstract ConfirmRequest DeleteRequest(Guid id);

    protected virtual Task OnItemShownAsync(Guid id) => Task.CompletedTask;

    protected virtual void FormStateChanged()
    {
    }

    protected static string PhaseName(ClosurePhase phase) => phase switch
    {
        ClosurePhase.Planned => "Geplant",
        ClosurePhase.Active => "Läuft",
        _ => "Beendet",
    };

    /// <summary>„01.03.2027 08:00“ in der Zeitzone des Standorts.</summary>
    protected string Stamp(DateTimeOffset at) => TimeZoneInfo.ConvertTime(at, Zone).ToString("dd.MM.yyyy HH:mm", System.Globalization.CultureInfo.InvariantCulture);

    protected static string? Trimmed(string text) => text.Trim().Length == 0 ? null : text.Trim();

    /// <summary>Überschrift, Zustand, Warnung und Angabe zum Anlegen des Formulars; das Formular setzt sie beim Anzeigen eines Eintrags.</summary>
    protected void SetInfo(string heading, string phase, string? openNotice, string? created)
    {
        HeadingText = heading;
        PhaseText = phase;
        OpenNotice = openNotice;
        CreatedText = created;
    }

    /// <summary>
    /// Eine Rückmeldung zu einer Aktion: ein Erfolg ist ein Toast der Seite (kurz, ohne Schlusspunkt), alles andere eine Meldung oben im Formular, die stehen bleibt (meldungen.md M1, M2).
    /// </summary>
    protected void Notify(string text, InfoBarSeverity severity = InfoBarSeverity.Success)
    {
        if (severity == InfoBarSeverity.Success)
        {
            ToastRequested?.Invoke(ToastState.Fit(text));
            return;
        }

        Message = text;
        MessageSeverity = severity;
    }

    /// <summary>Eine Eingabe des Formulars hat sich geändert: „Verwerfen“ wird bedienbar.</summary>
    protected void Touch()
    {
        if (_applying)
        {
            return;
        }

        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    /// <summary>Meldet der Seite eine Änderung und wartet, bis sie ihre Liste neu geladen hat.</summary>
    protected async Task AnnounceChangedAsync()
    {
        Saved?.Invoke();
        if (Refresh is { } refresh)
        {
            await refresh().ConfigureAwait(true);
        }
    }

    /// <summary>Wendet <paramref name="fill"/> an, ohne dass es als Änderung der Person gilt, und merkt den Stand für „Verwerfen“ und die Rückfrage.</summary>
    protected void Apply(Action fill)
    {
        _applying = true;
        try
        {
            fill();
        }
        finally
        {
            _applying = false;
        }

        _savedSnapshot = Snapshot();
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    // ---------- Öffnen ----------

    /// <summary>Der Standort, an dem das Formular arbeitet (Zeitzone, Währung); die Seite setzt ihn, wenn sie einen anderen wählt.</summary>
    public void SetLocation(LocationDto? location) => _location = location;

    /// <summary>Lädt den Eintrag und zeigt ihn im Formular. Liefert die Fehlermeldung oder nichts; bei einem Fehler bleibt das Formular, wie es war.</summary>
    public async Task<string?> OpenAsync(Guid id)
    {
        if (_location?.Id is not { } locationId)
        {
            return null;
        }

        var error = await FetchAsync(locationId, id).ConfigureAwait(true);
        if (error is not null)
        {
            return error;
        }

        IsCreating = false;
        CurrentId = id;
        Message = null;
        ResetFeedback();
        Apply(ApplyItem);
        RaiseFormState();
        await OnItemShownAsync(id).ConfigureAwait(true);
        return null;
    }

    /// <summary>Ein neues Formular; ein Sprung aus Kalender oder Assistent gibt Zeitraum und Raum vor.</summary>
    public void StartNew(AdminTarget? target)
    {
        _newTarget = target;
        IsCreating = true;
        CurrentId = null;
        Message = null;
        ResetFeedback();
        SetInfo(NewText, "", null, null);
        Apply(() =>
        {
            ApplyBlank(target);
            Period.Set(target?.From ?? target?.Until ?? Today, null, target?.Until ?? target?.From ?? Today, null);
        });
        RaiseFormState();
        FocusTarget = FirstField;
    }

    /// <summary>Schließt das Formular: kein Eintrag, kein neues Formular, nichts zu speichern.</summary>
    public void Clear()
    {
        IsCreating = false;
        CurrentId = null;
        _savedSnapshot = null;
        Message = null;
        ResetFeedback();
        SetInfo("", "", null, null);
        RaiseFormState();
    }

    private void ResetFeedback() => Error = null;

    private void RaiseFormState()
    {
        OnPropertyChanged(nameof(HasForm));
        OnPropertyChanged(nameof(CanDelete));
        OnPropertyChanged(nameof(HasUnsavedChanges));
        SaveCommand.NotifyCanExecuteChanged();
        DeleteCommand.NotifyCanExecuteChanged();
        FormStateChanged();
    }

    // ---------- Befehle ----------

    private bool CanSave => !IsSaving && HasForm;

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync() => await SaveChangesAsync().ConfigureAwait(true);

    public async Task<bool> SaveChangesAsync()
    {
        if (IsSaving || !HasForm || _location?.Id is not { } locationId)
        {
            return false;
        }

        Error = null;
        var periodRead = Period.TryRead(out var period, out var periodField);
        var fieldsField = ValidateFields();
        if (!periodRead || fieldsField is not null)
        {
            Error = SaveFailedText;
            FocusTarget = periodField ?? fieldsField;
            return false;
        }

        IsSaving = true;
        var (id, error) = await SendAsync(locationId, CurrentId, period).ConfigureAwait(true);
        IsSaving = false;
        if (id is not { } savedId)
        {
            Error = error ?? SaveFailedText;
            return false;
        }

        if (await OpenAsync(savedId).ConfigureAwait(true) is { } reloadError)
        {
            Notify(reloadError, InfoBarSeverity.Error);
        }

        Notify(SavedToast);
        await AnnounceChangedAsync().ConfigureAwait(true);
        return true;
    }

    /// <summary>Setzt alle Felder auf den gespeicherten Stand zurück.</summary>
    [RelayCommand]
    public void Discard()
    {
        if (IsCreating)
        {
            // Ein neuer Eintrag aus einem Sprung behält Raum und Zeitraum des Sprungs.
            var target = _newTarget;
            Apply(() =>
            {
                ApplyBlank(target);
                Period.Set(target?.From ?? target?.Until ?? Today, null, target?.Until ?? target?.From ?? Today, null);
            });
        }
        else if (CurrentId is not null)
        {
            Apply(ApplyItem);
        }

        Error = null;
    }

    [RelayCommand]
    private void CloseError() => Error = null;

    [RelayCommand]
    private void CloseMessage() => Message = null;

    [RelayCommand(CanExecute = nameof(CanDelete))]
    private async Task DeleteAsync()
    {
        if (CurrentId is not { } id || _location?.Id is not { } locationId)
        {
            return;
        }

        if (!(await AskAsync(DeleteRequest(id)).ConfigureAwait(true)).IsConfirmed)
        {
            return;
        }

        var error = await DeleteItemAsync(locationId, id).ConfigureAwait(true);
        if (error is not null)
        {
            Notify(error, InfoBarSeverity.Error);
            return;
        }

        Clear();
        Notify(DeletedToast);
        await AnnounceChangedAsync().ConfigureAwait(true);
        Removed?.Invoke();
    }

    private string Snapshot() => $"{Period.Signature}§{FormSignature()}";
}
