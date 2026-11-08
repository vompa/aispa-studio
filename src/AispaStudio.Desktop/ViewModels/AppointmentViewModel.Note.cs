using AispaStudio.Contracts;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

// ---------- Notiz zu diesem Termin (Runde 5, Ä33): ansehen, hinzufügen, ändern, entfernen ----------
public partial class AppointmentViewModel
{
    /// <summary>Name des Notizfelds in der Ansicht.</summary>
    public const string NoteBox = "AppointmentNoteBox";

    /// <summary>Name des Links „Notiz hinzufügen“ in der Ansicht.</summary>
    public const string AddNoteLink = "AddNoteLink";

    /// <summary>Name des Stift-Knopfs an der Notiz in der Ansicht.</summary>
    public const string EditNoteButton = "EditNoteButton";

    /// <summary>Längste Notiz in Zeichen (Server und Brief 5.16).</summary>
    public const int MaxNoteLength = 500;

    /// <summary>Bittet die Ansicht, ein Element der Seitenleiste zu fokussieren (Name der Konstanten dieser Klasse).</summary>
    public Action<string>? FocusRequested { get; init; }

    public bool HasNote => !string.IsNullOrEmpty(Note);

    /// <summary>Wer buchen darf, schreibt, ändert und entfernt die Notiz des Termins; sie ist nicht die Kundennotiz.</summary>
    public bool CanEditNote => MayBook;

    /// <summary>Ohne Notiz steht unter den Terminangaben der Link „Notiz hinzufügen“.</summary>
    public bool CanAddNote => CanEditNote && !HasNote && !IsEditingNote;

    /// <summary>Die Notiz steht als gelbe Hinweisfläche da, solange sie nicht gerade geändert wird.</summary>
    public bool ShowNote => HasNote && !IsEditingNote;

    /// <summary>Ändern geht über den Stift an der Hinweisfläche.</summary>
    public bool CanChangeNote => CanEditNote && ShowNote;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanAddNote), nameof(ShowNote), nameof(CanChangeNote), nameof(IsEditingCard))]
    public partial bool IsEditingNote { get; private set; }

    [ObservableProperty]
    public partial string NoteText { get; set; } = "";

    [ObservableProperty]
    public partial string? NoteError { get; private set; }

    [ObservableProperty]
    public partial bool IsSavingNote { get; private set; }

    /// <summary>Der Text im Notizfeld weicht von der gespeicherten Notiz ab: ein Schließen der Seite würde ihn verwerfen.</summary>
    private bool NoteChanged => IsEditingNote && NoteText.Trim() != (Note ?? "");

    [RelayCommand]
    public void BeginEditNote()
    {
        if (!CanEditNote || IsEditingNote || IsEditingTimes)
        {
            return;
        }

        NoteText = Note ?? "";
        NoteError = null;
        IsEditingNote = true;
        FocusRequested?.Invoke(NoteBox);
    }

    /// <summary>Speichert die Notiz (Knopf, Strg+Enter); ein leerer Text entfernt sie. Eine Ablehnung bleibt als Fehler am Feld stehen, der Text bleibt.</summary>
    [RelayCommand]
    public async Task SaveNoteAsync()
    {
        if (!CanEditNote || IsSavingNote)
        {
            return;
        }

        var text = NoteText.Trim();
        if (text.Length > MaxNoteLength)
        {
            NoteError = $"Höchstens {MaxNoteLength} Zeichen.";
            return;
        }

        IsSavingNote = true;
        try
        {
            var result = await Api.PutAsync<AppointmentDto>($"api/appointments/{Appointment.Id}/note", new AppointmentNoteRequest(text.Length == 0 ? null : text, Appointment.Version))
                .ConfigureAwait(true);
            if (result.Value is not { } saved)
            {
                if (ReportedStale(result))
                {
                    return;
                }

                NoteError = result.Error ?? "Die Notiz konnte nicht gespeichert werden.";
                return;
            }

            NoteError = null;
            IsEditingNote = false;
            await Changed(saved).ConfigureAwait(true);
            FocusRequested?.Invoke(string.IsNullOrEmpty(saved.Note) ? AddNoteLink : EditNoteButton);
        }
        finally
        {
            IsSavingNote = false;
        }
    }

    /// <summary>Esc und „Abbrechen“: das Feld schließt, der Text ist verworfen, der Fokus geht zum Auslöser zurück.</summary>
    [RelayCommand]
    public void CancelEditNote()
    {
        IsEditingNote = false;
        NoteError = null;
        FocusRequested?.Invoke(HasNote ? EditNoteButton : AddNoteLink);
    }
}
