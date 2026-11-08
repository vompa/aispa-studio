using System.Globalization;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Controls;
using AispaStudio.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

public enum CalendarEntryKind
{
    /// <summary>Sperrzeit in einer Spalte, z. B. „Arzt“ von 10 bis 12.</summary>
    Block,

    /// <summary>Zettel in einer Spalte zu einer Uhrzeit.</summary>
    Note,

    /// <summary>Pause einer Person: eine Sperrzeit mit dem Grund „Pause“.</summary>
    Break,
}

/// <summary>
/// Kleines Seitenpanel für Sperrzeit, Pause und Notiz an einer Zeit. Ob eine Sperrzeit passt, prüft der Server; eine Notiz belegt nichts.
/// Die Tagesnotiz ohne Uhrzeit steht in der Seitenleiste „Meldungen“.
/// </summary>
public partial class CalendarEntryViewModel : ViewModelBase
{
    private readonly IWorkstationApi _api;
    private readonly Guid _locationId;
    private readonly DateOnly _date;
    private readonly TimeZoneInfo _zone;
    private readonly CalendarColumn _column;
    private readonly Func<Task> _saved;
    private readonly Action _close;

    /// <summary>Die Notiz, die hier bearbeitet wird (Klick auf den Zettel); null für einen neuen Eintrag.</summary>
    private readonly CalendarNoteDto? _existing;

    public CalendarEntryViewModel(IWorkstationApi api, Guid locationId, DateOnly date, TimeZoneInfo zone, CalendarEntryKind kind, CalendarColumn column,
        DateTimeOffset start, Func<Task> saved, Action close, int minutes = 60, CalendarNoteDto? existing = null)
    {
        _api = api;
        _locationId = locationId;
        _date = date;
        _zone = zone;
        _column = column;
        _saved = saved;
        _close = close;
        _existing = existing;
        Kind = kind;
        var local = DayLayout.Local(start, zone);
        StartText = local.ToString("HH:mm", CultureInfo.InvariantCulture);
        // Eine Notiz bekommt ein Ende vorgeschlagen (15 Minuten, Auftraggeber 23.10.2026), das sich leeren lässt; beim Bearbeiten gilt ihr gespeichertes Ende.
        EndText = existing is not null
            ? existing.Until is { } until ? DayLayout.Local(until, zone).ToString("HH:mm", CultureInfo.InvariantCulture) : ""
            : local.AddMinutes(minutes).ToString("HH:mm", CultureInfo.InvariantCulture);
        Text = existing?.Text ?? "";
        Done = existing?.Done ?? false;
        _startAtOpen = StartText;
        _endAtOpen = EndText;
    }

    /// <summary>Ein bestehender Zettel wird bearbeitet: Uhrzeit, Ende, Text und „Erledigt“ in einem Panel wie bei Terminen.</summary>
    public bool IsEditing => _existing is not null;

    /// <summary>„Erledigt“ steht nur beim Bearbeiten; ein neuer Zettel ist offen.</summary>
    public bool ShowDone => IsNote && IsEditing;

    [ObservableProperty]
    public partial bool Done { get; set; }

    /// <summary>Die Zeiten beim Öffnen; was davon abweicht, ist eine ungespeicherte Änderung.</summary>
    private readonly string _startAtOpen;
    private readonly string _endAtOpen;

    /// <summary>Was der Kalender der Konfliktmeldung mitgibt: Termin markieren, „Zum Konflikt“, Gegenstück von Person und Raum.</summary>
    public ConflictLinks? Links { get; init; }

    public CalendarEntryKind Kind { get; }

    /// <summary>Tag und Spalte des Eintrags (Vorschau im Kalender).</summary>
    public DateOnly Date => _date;

    public Guid ColumnId => _column.Id;

    public bool IsBlock => Kind == CalendarEntryKind.Block;

    public bool IsBreak => Kind == CalendarEntryKind.Break;

    /// <summary>Sperrzeit und Pause haben ein Ende; eine Notiz nur einen Zeitpunkt, auf Wunsch mit Ende („Bis“).</summary>
    public bool HasEnd => IsBlock || IsBreak;

    /// <summary>Das Feld für das Ende steht bei Sperrzeit und Pause (Pflicht) und bei der Notiz an einer Zeit (optional).</summary>
    public bool ShowEnd => HasEnd || Kind == CalendarEntryKind.Note;

    public string StartLabel => Kind == CalendarEntryKind.Note ? "Uhrzeit" : "Beginn";

    public string EndLabel => Kind == CalendarEntryKind.Note ? "Bis" : "Ende";

    /// <summary>Eine Pause braucht keinen Text: der Grund ist „Pause“.</summary>
    public bool HasText => !IsBreak;

    public bool IsNote => Kind == CalendarEntryKind.Note;

    public string Title => Kind switch
    {
        CalendarEntryKind.Block => "Sperrzeit",
        CalendarEntryKind.Break => "Pause",
        _ => IsEditing ? "Notiz bearbeiten" : "Notiz",
    };

    public string Where => $"{_column.Title} · {DayLayout.DateText(_date)}";

    public string TextLabel => IsBlock ? "Grund" : "Notiz";

    public string TextHint => Kind switch
    {
        CalendarEntryKind.Block => "z. B. Arzt, Besprechung, Wartung",
        _ => "z. B. Herr Lang ruft zurück",
    };

    public string SaveText => Kind switch
    {
        CalendarEntryKind.Block => "Sperren",
        CalendarEntryKind.Break => "Pause eintragen",
        _ => IsEditing ? "Speichern" : "Notiz speichern",
    };

    [ObservableProperty]
    public partial string Text { get; set; } = "";

    [ObservableProperty]
    public partial string StartText { get; set; }

    [ObservableProperty]
    public partial string EndText { get; set; }

    [ObservableProperty]
    public partial string? Error { get; private set; }

    /// <summary>
    /// Der Beginn kommt von einem anderen Reiter: das Ende zieht um denselben Abstand mit, damit es nie vor dem neuen Beginn steht.
    /// Ist eines von beiden unlesbar, bleibt das Ende stehen.
    /// </summary>
    internal void MoveStartTo(string startText)
    {
        if (Parse(StartText) is { } oldFrom && Parse(startText) is { } newFrom && Parse(EndText) is { } until)
        {
            var shifted = until.AddMinutes((int)(newFrom - oldFrom).TotalMinutes);
            // Über Mitternacht gibt es kein Ende; dann bleibt der alte Abstand nicht erhalten, das Ende steht auf dem späteren Beginn plus 15 Minuten.
            EndText = shifted > newFrom ? shifted.ToString("HH:mm", CultureInfo.InvariantCulture) : newFrom.AddMinutes(15).ToString("HH:mm", CultureInfo.InvariantCulture);
        }

        StartText = startText;
    }

    /// <summary>Das Ende so setzen, dass der Eintrag <paramref name="minutes"/> Minuten ab dem Beginn dauert (Länge aus „Neuer Termin“, Ä153).</summary>
    internal void LastFor(int minutes)
    {
        if (Parse(StartText) is { } from)
        {
            EndText = from.AddMinutes(minutes).ToString("HH:mm", CultureInfo.InvariantCulture);
        }
    }

    /// <summary>Die Sperrzeit passt nicht: Liste der Belegungen in Ortszeit statt eines Fehlertextes.</summary>
    [ObservableProperty]
    public partial ConflictNotice? Conflict { get; private set; }

    [ObservableProperty]
    public partial bool IsBusy { get; private set; }

    [RelayCommand]
    public async Task SaveAsync()
    {
        Error = null;
        Conflict = null;
        if (HasText && string.IsNullOrWhiteSpace(Text))
        {
            Error = IsBlock ? "Bitte einen Grund angeben." : "Bitte einen Text eingeben.";
            return;
        }

        if (Parse(StartText) is not { } from)
        {
            Error = "Beginn bitte als Uhrzeit, z. B. 10:00.";
            return;
        }

        var start = DayLayout.LocalToUtc(_date, from, _zone);
        DateTimeOffset? end = null;
        if (HasEnd)
        {
            if (Parse(EndText) is not { } until || until <= from)
            {
                Error = "Ende bitte als Uhrzeit nach dem Beginn, z. B. 12:00.";
                return;
            }

            end = DayLayout.LocalToUtc(_date, until, _zone);
        }
        else if (Kind == CalendarEntryKind.Note && !string.IsNullOrWhiteSpace(EndText))
        {
            if (Parse(EndText) is not { } noteUntil || noteUntil <= from)
            {
                Error = "Das Ende muss nach dem Beginn liegen.";
                return;
            }

            end = DayLayout.LocalToUtc(_date, noteUntil, _zone);
        }

        IsBusy = true;
        try
        {
            var kind = _column.IsRoom ? ResourceKind.Room : ResourceKind.Employee;
            var (error, conflict) = Kind switch
            {
                CalendarEntryKind.Block => Outcome(await _api.PostAsync<BlockDto>("api/blocks",
                    new BlockInput(kind, _column.Id, start, end!.Value, Text.Trim())).ConfigureAwait(true)),
                CalendarEntryKind.Break => Outcome(await _api.PostAsync<BlockDto>("api/blocks",
                    new BlockInput(kind, _column.Id, start, end!.Value, "Pause")).ConfigureAwait(true)),
                _ when _existing is { } existing => Outcome(await _api.PatchAsync<CalendarNoteDto>($"api/calendar-notes/{existing.Id}",
                    new CalendarNoteChange(Text.Trim(), Done, end, start)).ConfigureAwait(true)),
                _ => Outcome(await _api.PostAsync<CalendarNoteDto>($"api/locations/{_locationId}/calendar-notes",
                    new CalendarNoteInput(_date, Text.Trim(), kind, _column.Id, start, end)).ConfigureAwait(true)),
            };
            if (conflict is not null)
            {
                Conflict = conflict;
                return;
            }

            if (error is not null)
            {
                Error = error;
                return;
            }

            await _saved().ConfigureAwait(true);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private (string? Error, ConflictNotice? Conflict) Outcome<T>(ApiResult<T> result) =>
        (result.Error, ConflictNotice.From(result.Rejection, _zone, _date, Links ?? new ConflictLinks(null)));

    [RelayCommand]
    private void Close() => _close();

    private static TimeOnly? Parse(string? text) => TimeInputParser.TryParse(text);
}
