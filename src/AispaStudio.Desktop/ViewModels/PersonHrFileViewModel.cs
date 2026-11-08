using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using AispaStudio.Contracts;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>Art eines Mitarbeitergesprächs in der Auswahl.</summary>
public sealed record HrConversationOption(HrConversationKind Kind, string Label)
{
    public static IReadOnlyList<HrConversationOption> All { get; } =
    [
        new(HrConversationKind.Annual, "Jahresgespräch"),
        new(HrConversationKind.Feedback, "Feedback"),
        new(HrConversationKind.Return, "Rückkehr"),
        new(HrConversationKind.Occasion, "Anlass"),
    ];

    public override string ToString() => Label;
}

/// <summary>Eine Vereinbarung im offenen Gespräch.</summary>
public sealed partial class HrAgreementRow(string text, bool isDone, Action changed, Action<HrAgreementRow> remove) : ObservableObject
{
    [ObservableProperty]
    public partial string Text { get; set; } = text;

    [ObservableProperty]
    public partial bool IsDone { get; set; } = isDone;

    partial void OnTextChanged(string value) => changed();

    partial void OnIsDoneChanged(bool value) => changed();

    [RelayCommand]
    private void Remove() => remove(this);
}

/// <summary>Ein Eintrag der Liste: Überschrift, Inhalt und die übrigen Angaben als Zeilen.</summary>
public sealed class HrEntryRow(HrEntryDto entry, string title, IReadOnlyList<string> lines, bool isEditable, IRelayCommand edit, IAsyncRelayCommand delete)
{
    public HrEntryDto Entry => entry;

    public string Title => title;

    public string Text => entry.Text;

    public IReadOnlyList<string> Lines => lines;

    public bool IsEditable => isEditable;

    public IRelayCommand EditCommand => edit;

    public IAsyncRelayCommand DeleteCommand => delete;
}

/// <summary>
/// Reiter „Personalakte“ der Personenkarte (R05 Slice 110, Ä252): Mitarbeitergespräche, Anmerkungen und Verwarnungen einer Person, nur für Inhaberin und Manager, und die Auskunft als PDF.
/// Ein offener Eintrag zählt zu den ungespeicherten Änderungen der Karte, hat aber seine eigene Leiste. Bei Ausgeschiedenen nur lesen und exportieren. Rechte, Pflichtfelder, die Vorgabe
/// für das Ablaufdatum und die Verschlüsselung liegen in der API; die Felder hier sagen nur früher, was fehlt.
/// </summary>
public sealed partial class PersonHrFileViewModel : ViewModelBase, ISavesFiles
{
    private static readonly HashSet<string> FormFields =
    [
        nameof(Date), nameof(Text), nameof(SelectedConversation), nameof(Participants), nameof(FollowUpOn), nameof(FollowUpDone), nameof(IssuedBy), nameof(HandedOverInWriting), nameof(ExpiresOn),
    ];

    private readonly TeamContext _context;
    private readonly string _basePath;
    private readonly string _personName;
    private Guid? _editingId;
    private string _savedState = "";
    private bool _applying;

    public PersonHrFileViewModel(TeamContext context, Guid employeeId, string personName, bool isReadOnly)
    {
        _context = context;
        _basePath = $"api/employees/{employeeId}/hr-file";
        _personName = personName;
        IsEditable = !isReadOnly;
    }

    public IFileSaver? Files { get; set; }

    /// <summary>Ausgeschiedene: nur lesen und exportieren.</summary>
    public bool IsEditable { get; }

    public IReadOnlyList<HrConversationOption> ConversationKinds { get; } = HrConversationOption.All;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsLoading))]
    public partial bool IsLoaded { get; private set; }

    public bool ShowsLoading => !IsLoaded && !HasLoadError;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLoadError), nameof(ShowsLoading))]
    public partial string? LoadError { get; private set; }

    public bool HasLoadError => LoadError is not null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsEmpty))]
    public partial IReadOnlyList<HrEntryRow> Rows { get; private set; } = [];

    public bool ShowsEmpty => IsLoaded && Rows.Count == 0 && !IsEditing;

    /// <summary>Fehler der API beim Speichern, Löschen oder Exportieren.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError), nameof(ShowsFormError), nameof(ShowsListError))]
    public partial string? Error { get; private set; }

    public bool HasError => Error is not null;

    /// <summary>Der Fehler steht dort, wo gerade gearbeitet wird: über der Leiste „Speichern“ oder über den Knöpfen.</summary>
    public bool ShowsFormError => HasError && IsEditing;

    public bool ShowsListError => HasError && !IsEditing;

    /// <summary>„personalakte-Lena Huber.pdf gespeichert.“</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMessage))]
    public partial string? Message { get; private set; }

    public bool HasMessage => Message is not null;

    [ObservableProperty]
    public partial bool IsBusy { get; private set; }

    // ---------- Offener Eintrag ----------

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsEmpty), nameof(HasUnsavedChanges), nameof(ShowsAddButtons), nameof(ShowsFormError), nameof(ShowsListError))]
    public partial bool IsEditing { get; private set; }

    public bool ShowsAddButtons => IsEditable && !IsEditing;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsConversation), nameof(IsWarning), nameof(TextLabel))]
    public partial HrEntryKind EditKind { get; private set; }

    public bool IsConversation => EditKind == HrEntryKind.Conversation;

    public bool IsWarning => EditKind == HrEntryKind.Warning;

    public string TextLabel => EditKind switch
    {
        HrEntryKind.Conversation => "Notizen",
        HrEntryKind.Warning => "Grund",
        _ => "Anmerkung",
    };

    public string FormTitle => (EditKind, _editingId is null) switch
    {
        (HrEntryKind.Conversation, true) => "Neues Gespräch",
        (HrEntryKind.Conversation, false) => "Gespräch bearbeiten",
        (HrEntryKind.Warning, true) => "Neue Verwarnung",
        (HrEntryKind.Warning, false) => "Verwarnung bearbeiten",
        (_, true) => "Neue Anmerkung",
        _ => "Anmerkung bearbeiten",
    };

    [ObservableProperty]
    public partial DateOnly? Date { get; set; }

    [ObservableProperty]
    public partial string Text { get; set; } = "";

    [ObservableProperty]
    public partial HrConversationOption SelectedConversation { get; set; } = HrConversationOption.All[0];

    [ObservableProperty]
    public partial string Participants { get; set; } = "";

    public ObservableCollection<HrAgreementRow> Agreements { get; } = [];

    [ObservableProperty]
    public partial DateOnly? FollowUpOn { get; set; }

    [ObservableProperty]
    public partial bool FollowUpDone { get; set; }

    [ObservableProperty]
    public partial string IssuedBy { get; set; } = "";

    [ObservableProperty]
    public partial bool HandedOverInWriting { get; set; }

    [ObservableProperty]
    public partial DateOnly? ExpiresOn { get; set; }

    [ObservableProperty]
    public partial string? DateError { get; private set; }

    [ObservableProperty]
    public partial string? TextError { get; private set; }

    [ObservableProperty]
    public partial string? IssuedByError { get; private set; }

    [ObservableProperty]
    public partial string? ExpiresError { get; private set; }

    /// <summary>„Gespeichert.“ über der Liste, bis wieder etwas geändert wird.</summary>
    [ObservableProperty]
    public partial string? StatusText { get; private set; }

    public bool HasUnsavedChanges => IsEditing && State() != _savedState;

    /// <summary>Die Ansicht setzt den Fokus: zu Beginn auf den Inhalt, nach einer Prüfung auf das erste ungültige Feld.</summary>
    public event Action<string>? FocusRequested;

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (!_applying && e.PropertyName is { } name && FormFields.Contains(name))
        {
            FormChanged();
        }
    }

    /// <summary>Solange ein Formular offen ist, sind Ändern und Löschen der Zeilen gesperrt, sonst gingen Eingaben verloren.</summary>
    partial void OnIsEditingChanged(bool value)
    {
        foreach (var row in Rows)
        {
            row.EditCommand.NotifyCanExecuteChanged();
            row.DeleteCommand.NotifyCanExecuteChanged();
        }
    }

    public Task EnsureLoadedAsync() => IsLoaded ? Task.CompletedTask : LoadAsync();

    [RelayCommand]
    public async Task LoadAsync()
    {
        LoadError = null;
        var result = await _context.Api.GetAsync<List<HrEntryDto>>(_basePath).ConfigureAwait(true);
        if (result.Value is not { } entries)
        {
            LoadError = result.Error ?? "Die Personalakte wurde nicht geladen.";
            return;
        }

        Rows = [.. entries.Select(ToRow)];
        IsLoaded = true;
        OnPropertyChanged(nameof(ShowsEmpty));
    }

    [RelayCommand]
    private void AddConversation() => Open(HrEntryKind.Conversation, null);

    [RelayCommand]
    private void AddNote() => Open(HrEntryKind.Note, null);

    [RelayCommand]
    private void AddWarning() => Open(HrEntryKind.Warning, null);

    [RelayCommand]
    private void AddAgreement()
    {
        Agreements.Add(NewAgreement("", false));
        FormChanged();
    }

    /// <summary>Für die Rückfrage der Karte: speichert nur, wenn etwas geändert ist; false, wenn ein Feld fehlt oder die API ablehnt (dann bleiben die Eingaben).</summary>
    public Task<bool> SaveChangesAsync() => HasUnsavedChanges ? SaveEntryAsync() : Task.FromResult(true);

    /// <summary>„Speichern“ prüft auch ein noch leeres Formular.</summary>
    [RelayCommand]
    private async Task SaveAsync()
    {
        if (IsEditing)
        {
            await SaveEntryAsync().ConfigureAwait(true);
        }
    }

    [RelayCommand]
    private void Cancel() => Discard();

    [RelayCommand]
    private void CloseError() => Error = null;

    [RelayCommand]
    private void CloseMessage() => Message = null;

    public void Discard()
    {
        Close();
        Error = null;
        StatusText = null;
    }

    [RelayCommand]
    private async Task ExportAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        try
        {
            Message = Error = null;
            var result = await _context.Api.GetBytesAsync($"{_basePath}/export").ConfigureAwait(true);
            if (result.Value is not { } content)
            {
                Error = result.Error ?? "Das PDF konnte nicht erstellt werden.";
                return;
            }

            var invalid = Path.GetInvalidFileNameChars();
            var fileName = $"personalakte-{string.Concat(_personName.Select(c => invalid.Contains(c) ? '-' : c))}.pdf";
            if (Files is not null && await Files.SaveAsync(fileName, "PDF-Datei", ["*.pdf"], content).ConfigureAwait(true))
            {
                Message = $"{fileName} gespeichert.";
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Error = "Die Datei konnte nicht gespeichert werden. Wähle einen anderen Ort.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static string Day(DateOnly day) => day.ToString("d.M.yyyy", CultureInfo.InvariantCulture);

    private static string? Clean(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string DayKey(DateOnly? day) => day?.ToString("O", CultureInfo.InvariantCulture) ?? "";

    private static string TitleOf(HrEntryDto entry) => entry.Kind switch
    {
        HrEntryKind.Conversation => $"{HrConversationOption.All.FirstOrDefault(o => o.Kind == entry.ConversationKind)?.Label ?? "Gespräch"}, {Day(entry.Date)}",
        HrEntryKind.Warning => $"Verwarnung, {Day(entry.Date)}",
        _ => $"Anmerkung, {Day(entry.Date)}",
    };

    private static IEnumerable<string> LinesOf(HrEntryDto entry)
    {
        if (entry.Participants is { } participants)
        {
            yield return $"Teilnehmende: {participants}";
        }

        foreach (var agreement in entry.Agreements)
        {
            yield return $"{(agreement.IsDone ? "Erledigt" : "Offen")}: {agreement.Text}";
        }

        if (entry.FollowUpOn is { } followUp)
        {
            yield return $"Folgetermin: {Day(followUp)}{(entry.FollowUpDone ? ", geführt" : "")}";
        }

        if (entry.Kind == HrEntryKind.Warning)
        {
            yield return $"Ausgesprochen von: {entry.IssuedBy}";
            yield return entry.HandedOverInWriting ? "Schriftlich übergeben" : "Nicht schriftlich übergeben";
            if (entry.ExpiresOn is { } expires)
            {
                yield return $"Gültig bis: {Day(expires)}";
            }
        }
    }

    private HrEntryRow ToRow(HrEntryDto entry) => new(entry, TitleOf(entry), [.. LinesOf(entry)], IsEditable,
        new RelayCommand(() => Open(entry.Kind, entry), () => !IsEditing), new AsyncRelayCommand(() => DeleteAsync(entry), () => !IsEditing));

    private async Task<bool> SaveEntryAsync()
    {
        if (!Check())
        {
            return false;
        }

        Error = null;
        var input = ToInput();
        var result = _editingId is { } id
            ? await _context.Api.PutAsync<HrEntryDto>($"{_basePath}/{id}", input).ConfigureAwait(true)
            : await _context.Api.PostAsync<HrEntryDto>(_basePath, input).ConfigureAwait(true);
        if (result.Value is null)
        {
            Error = result.Error ?? "Der Eintrag wurde nicht gespeichert.";
            return false;
        }

        Close();
        await LoadAsync().ConfigureAwait(true);
        StatusText = "Gespeichert.";
        return true;
    }

    private async Task DeleteAsync(HrEntryDto entry)
    {
        if (_context.Dialogs is not { } dialogs)
        {
            return;
        }

        var answer = await dialogs.AskAsync(new ConfirmRequest("Eintrag löschen?", $"„{TitleOf(entry)}“ wird gelöscht. Im Verlauf bleibt nur, dass gelöscht wurde.", "Löschen")).ConfigureAwait(true);
        if (!answer.IsConfirmed)
        {
            return;
        }

        var result = await _context.Api.DeleteAsync<object>($"{_basePath}/{entry.Id}").ConfigureAwait(true);
        if (result.Error is { } error)
        {
            Error = error;
            return;
        }

        Error = null;
        await LoadAsync().ConfigureAwait(true);
        StatusText = "Gelöscht.";
    }

    private void Open(HrEntryKind kind, HrEntryDto? entry)
    {
        _applying = true;
        _editingId = entry?.Id;
        EditKind = kind;
        Date = entry?.Date ?? _context.Today;
        Text = entry?.Text ?? "";
        SelectedConversation = HrConversationOption.All.FirstOrDefault(o => o.Kind == entry?.ConversationKind) ?? HrConversationOption.All[0];
        Participants = entry?.Participants ?? "";
        Agreements.Clear();
        foreach (var agreement in entry?.Agreements ?? [])
        {
            Agreements.Add(NewAgreement(agreement.Text, agreement.IsDone));
        }

        FollowUpOn = entry?.FollowUpOn;
        FollowUpDone = entry?.FollowUpDone ?? false;
        IssuedBy = entry?.IssuedBy ?? _context.User.DisplayName;
        HandedOverInWriting = entry?.HandedOverInWriting ?? false;
        ExpiresOn = entry?.ExpiresOn;
        _applying = false;
        DateError = TextError = IssuedByError = ExpiresError = Error = StatusText = Message = null;
        _savedState = State();
        OnPropertyChanged(nameof(FormTitle));
        IsEditing = true;
        FocusRequested?.Invoke("HrTextField");
    }

    private void Close()
    {
        IsEditing = false;
        _editingId = null;
        Agreements.Clear();
    }

    private HrAgreementRow NewAgreement(string text, bool isDone) => new(text, isDone, FormChanged, row =>
    {
        Agreements.Remove(row);
        FormChanged();
    });

    private void FormChanged()
    {
        StatusText = null;
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    /// <summary>Alles, was gespeichert würde, als ein Text; zwei Stände sind gleich, wenn jedes Feld gleich ist.</summary>
    private string State() => string.Join('\n', [
        DayKey(Date), Text, SelectedConversation.Kind.ToString(), Participants, DayKey(FollowUpOn), FollowUpDone.ToString(), IssuedBy, HandedOverInWriting.ToString(), DayKey(ExpiresOn),
        .. Agreements.Select(a => $"{a.IsDone} {a.Text}"),
    ]);

    private bool Check()
    {
        DateError = Date is null ? "Trage das Datum ein, z. B. 21.10.2026." : null;
        TextError = string.IsNullOrWhiteSpace(Text) ? $"Trage {(IsWarning ? "den Grund" : IsConversation ? "die Notizen" : "die Anmerkung")} ein." : null;
        IssuedByError = IsWarning && string.IsNullOrWhiteSpace(IssuedBy) ? "Trage ein, wer die Verwarnung ausgesprochen hat." : null;
        ExpiresError = IsWarning && ExpiresOn < Date ? "Wähle ein Ablaufdatum ab dem Datum der Verwarnung." : null;
        var first = DateError is not null ? "HrDateField"
            : TextError is not null ? "HrTextField"
            : IssuedByError is not null ? "HrIssuedByField"
            : ExpiresError is not null ? "HrExpiresField"
            : null;
        if (first is not null)
        {
            FocusRequested?.Invoke(first);
        }

        return first is null;
    }

    private HrEntryInput ToInput() => new(
        EditKind,
        Date!.Value,
        Text.Trim(),
        IsConversation ? SelectedConversation.Kind : null,
        IsConversation ? Clean(Participants) : null,
        IsConversation ? [.. Agreements.Where(a => !string.IsNullOrWhiteSpace(a.Text)).Select(a => new HrAgreement(a.Text.Trim(), a.IsDone))] : null,
        IsConversation ? FollowUpOn : null,
        IsConversation && FollowUpDone,
        IsWarning ? Clean(IssuedBy) : null,
        IsWarning && HandedOverInWriting,
        IsWarning ? ExpiresOn : null);
}
