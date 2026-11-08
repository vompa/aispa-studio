using AispaStudio.Contracts;
using AispaStudio.Desktop.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>Pausen, Sperrzeiten und Notizen im Kalender; die Tagesnotizen stehen in der Seitenleiste „Meldungen“ (Gruppe „Notizen heute“).</summary>
public partial class CalendarViewModel
{
    /// <summary>Sperrzeit, Pause oder Notiz an einer Zeit (im Fenster für neue Einträge).</summary>
    public CalendarEntryViewModel? Entry => NewEntry?.Content as CalendarEntryViewModel;

    /// <summary>Die Tagesnotizen ohne Uhrzeit für die Gruppe „Notizen heute“; Zettel in einer Spalte bleiben in der Spalte.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDayNotes), nameof(DayNotesText), nameof(MessagesTip), nameof(MessagesName))]
    public partial IReadOnlyList<CalendarNoteDto> DayNotes { get; private set; } = [];

    /// <summary>Es gibt Tagesnotizen: die Schaltfläche „Meldungen“ trägt dann einen kleinen Notiz-Punkt (der Zähler zählt nur Meldungen).</summary>
    public bool HasDayNotes => DayNotes.Count > 0;

    /// <summary>„1 Notiz heute“, „3 Notizen heute“; leer ohne Notizen.</summary>
    public string DayNotesText => DayNotes.Count switch
    {
        0 => "",
        1 => "1 Notiz heute",
        var n => $"{n} Notizen heute",
    };

    partial void OnDayNotesChanged(IReadOnlyList<CalendarNoteDto> value) => _messages?.ShowNotesOf(value);

    private DayNoteActions? NoteActions =>
        CanBook ? new DayNoteActions(AddDayNoteAsync, ChangeNoteTextAsync, ToggleNoteAsync, AskDeleteNoteAsync) : null;

    /// <summary>„.“ oder „Tagesnotiz“: die Seitenleiste „Meldungen“ geht auf, das Notizfeld in der Gruppe „Notizen heute“ bekommt den Fokus.</summary>
    [RelayCommand]
    private void OpenDayNote()
    {
        if (!CanBook || _resources is null)
        {
            return;
        }

        if (MessagesPanel is null || MessagesPanel.HasFilter)
        {
            ShowMessagesPanel(null);
        }

        MessagesPanel!.AddNote();
    }

    private async Task<string?> AddDayNoteAsync(string text)
    {
        if (Location is null)
        {
            return "Kein Standort gewählt.";
        }

        var result = await _api.PostAsync<CalendarNoteDto>($"api/locations/{Location.Id}/calendar-notes", new CalendarNoteInput(Date, text)).ConfigureAwait(true);
        if (result.Value is not { } note)
        {
            return result.Error ?? "Die Notiz konnte nicht gespeichert werden.";
        }

        ReplaceNote(note.Id, note);
        ShowToast("Notiz gespeichert");
        return null;
    }

    private async Task<string?> ChangeNoteTextAsync(CalendarNoteDto note, string text)
    {
        var result = await _api.PatchAsync<CalendarNoteDto>($"api/calendar-notes/{note.Id}", new CalendarNoteChange(Text: text)).ConfigureAwait(true);
        if (result.Value is not { } changed)
        {
            return result.Error ?? "Die Notiz konnte nicht geändert werden.";
        }

        ReplaceNote(note.Id, changed);
        ShowToast("Notiz geändert");
        return null;
    }

    /// <summary>Sperrzeit oder Pause wieder freigeben.</summary>
    public async Task DeleteBlockAsync(BlockDto block)
    {
        var result = await _api.DeleteAsync<object>($"api/blocks/{block.Id}").ConfigureAwait(true);
        if (!result.Ok)
        {
            ShowProblem("Nicht gelöscht", result.Error, action: () => _ = DeleteBlockAsync(block));
            return;
        }

        ShowToast(DayLayout.IsBreak(block.Reason) ? "Pause gelöscht." : "Sperrzeit gelöscht.");
        await LoadAsync().ConfigureAwait(true);
    }

    /// <summary>Notiz abhaken oder wieder öffnen; die Leiste und der Zettel zeigen es sofort.</summary>
    public async Task ToggleNoteAsync(CalendarNoteDto note)
    {
        if (!CanBook || _day is null)
        {
            return;
        }

        var result = await _api.PatchAsync<CalendarNoteDto>($"api/calendar-notes/{note.Id}", new CalendarNoteChange(Done: !note.Done))
            .ConfigureAwait(true);
        if (result.Value is not { } changed)
        {
            ShowProblem("Notiz nicht geändert", result.Error, action: () => _ = ToggleNoteAsync(note));
            return;
        }

        ReplaceNote(note.Id, changed);
    }

    public async Task DeleteNoteAsync(CalendarNoteDto note)
    {
        var result = await _api.DeleteAsync<object>($"api/calendar-notes/{note.Id}").ConfigureAwait(true);
        if (!result.Ok)
        {
            ShowProblem("Notiz nicht gelöscht", result.Error, action: () => _ = DeleteNoteAsync(note));
            return;
        }

        ReplaceNote(note.Id, null);
    }

    private void ReplaceNote(Guid id, CalendarNoteDto? note)
    {
        if (_day is null)
        {
            return;
        }

        var notes = (_day.Notes ?? []).Where(n => n.Id != id).ToList();
        if (note is not null)
        {
            notes.Add(note);
        }

        _day = _day with { Notes = [.. notes.OrderBy(n => n.At).ThenBy(n => n.Id)] };
        Rebuild();
    }

    /// <summary>Notiz löschen fragt nach (Rückfragedialog, Design-Brief 5.27).</summary>
    private async Task AskDeleteNoteAsync(CalendarNoteDto note)
    {
        var answer = await AskAsync(new ConfirmRequest("Notiz löschen?", $"Die Notiz „{note.Text}“ ist danach weg.", "Löschen")).ConfigureAwait(true);
        if (answer.IsConfirmed)
        {
            await DeleteNoteAsync(note).ConfigureAwait(true);
        }
    }

    /// <summary>Pause oder Sperrzeit löschen fragt nach; danach ist die Zeit wieder frei buchbar.</summary>
    private async Task AskDeleteBlockAsync(BlockDto block)
    {
        var what = DayLayout.IsBreak(block.Reason) ? "Pause" : "Sperrzeit";
        var answer = await AskAsync(new ConfirmRequest($"{what} löschen?",
            $"Die Zeit {DayLayout.TimeText(block.From, block.Until, _zone)} ist danach wieder frei.", "Löschen")).ConfigureAwait(true);
        if (answer.IsConfirmed)
        {
            await DeleteBlockAsync(block).ConfigureAwait(true);
        }
    }

    /// <summary>
    /// Klick auf einen Zettel (und „Details öffnen“): dasselbe Panel wie für einen neuen Zettel, aber mit Uhrzeit, Ende, Text und „Erledigt“
    /// der Notiz und ohne Umschaltleiste; Speichern ändert die Notiz (Auftraggeber, 23.10.2026: alle Einträge gleich bearbeiten).
    /// </summary>
    public void OpenNote(CalendarNoteDto note)
    {
        if (!CanBook || _resources is null || _day is null || note.At is not { } at
            || Columns.FirstOrDefault(c => c.Id == note.ResourceId && (c.Date ?? note.Date) == note.Date) is not { } column)
        {
            return;
        }

        ClosePanels();
        Ghost = null;
        NewEntry = new NewEntryViewModel([NewEntryKind.Note], NewEntryKind.Note, (_, _) => EntryPanel(CalendarEntryKind.Note, ResourceColumn(column), at, 15, () => NewEntry = null, note));
    }

    /// <summary>Rechtsklick auf einen Zettel.</summary>
    public IReadOnlyList<CalendarAction> ActionsFor(CalendarNoteDto note) =>
        CanBook
            ? [
                new("Details öffnen", "Enter", () => OpenNote(note), Icon: Icons.Info),
                new(note.Done ? "Wieder offen" : "Erledigt", null, () => _ = ToggleNoteAsync(note)),
                new("Notiz löschen", null, () => _ = AskDeleteNoteAsync(note), IsDanger: true),
            ]
            : [];

    /// <summary>Rechtsklick auf eine Sperrzeit oder Pause.</summary>
    private IReadOnlyList<CalendarAction> ActionsForBlock(BlockDto block) =>
        CanBook ? [new(DayLayout.IsBreak(block.Reason) ? "Pause löschen" : "Sperrzeit löschen", null, () => _ = AskDeleteBlockAsync(block), IsDanger: true)] : [];
}
