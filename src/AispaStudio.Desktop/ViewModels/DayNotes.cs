using System.Windows.Input;
using AispaStudio.Contracts;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>
/// Was die Gruppe „Notizen heute“ der Seitenleiste „Meldungen“ mit den Tagesnotizen tun kann; der Kalender führt es aus. Jede Funktion
/// liefert null bei Erfolg, sonst den Fehlertext für das Feld. Ohne Buchungsrecht gibt es keine <see cref="DayNoteActions"/>.
/// </summary>
public sealed record DayNoteActions(
    Func<string, Task<string?>> Add,
    Func<CalendarNoteDto, string, Task<string?>> Change,
    Func<CalendarNoteDto, Task> Toggle,
    Func<CalendarNoteDto, Task> Delete);

/// <summary>Eine Tagesnotiz ohne Uhrzeit in der Gruppe „Notizen heute“: Haken, Text, Ändern und Löschen (mit Rückfrage).</summary>
public sealed class DayNoteRow(CalendarNoteDto note)
{
    /// <summary>Wie viele Zeichen der Name der Knöpfe (für Screenreader) vom Text nennt.</summary>
    private const int NameLength = 40;

    public CalendarNoteDto Note { get; } = note;

    public string Text => Note.Text;

    public bool IsDone => Note.Done;

    public ICommand? ToggleCommand { get; init; }

    public ICommand? EditCommand { get; init; }

    public ICommand? DeleteCommand { get; init; }

    /// <summary>Ändern, Löschen und Abhaken gibt es nur mit Buchungsrecht.</summary>
    public bool CanChange => ToggleCommand is not null;

    private string Short => Text.Length <= NameLength ? Text : $"{Text[..NameLength].TrimEnd()} …";

    public string ToggleName => $"Notiz abhaken: {Short}";

    public string EditName => $"Notiz ändern: {Short}";

    public string DeleteName => $"Notiz löschen: {Short}";
}
