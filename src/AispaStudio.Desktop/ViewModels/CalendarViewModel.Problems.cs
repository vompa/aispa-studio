using System.Windows.Input;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>
/// Ein Problem, das eine Aktion im Kalender nicht erledigt hat (Fehler) oder nur halb (Warnung): steht als InfoBar oben im Kalender, bis es geschlossen wird
/// (docs/gestaltung/meldungen.md, M1 und M2). <see cref="ActionText"/> ist ein Verb wie „Erneut versuchen“; ohne Wiederholung oder Gegenschritt bleibt nur das Schließen.
/// </summary>
public sealed record CalendarProblem(InfoBarSeverity Severity, string Title, string Message, string? ActionText, ICommand? Action);

public partial class CalendarViewModel
{
    /// <summary>Der Toast ist eine Zeile bis zu dieser Länge (Fluent 2: unter etwa 60 Zeichen).</summary>
    public const int ToastMaxLength = 60;

    /// <summary>Das jüngste offene Problem (Fehler oder Warnung) als InfoBar; sonst leer. Es bleibt stehen, bis es geschlossen wird.</summary>
    [ObservableProperty]
    public partial CalendarProblem? Problem { get; private set; }

    /// <summary>Zeigt einen Fehler (<paramref name="isError"/>) oder eine Warnung als InfoBar statt als Toast; ein neues Problem ersetzt das alte.</summary>
    /// <param name="title">Kurz, was nicht geklappt hat („Verspätung nicht gespeichert“).</param>
    /// <param name="message">Die Begründung und, wenn es einen gibt, der Weg („Zu dieser Zeit ist nicht alles frei. Raum Zeder: belegt.“); ohne Text steht „Bitte noch einmal versuchen.“</param>
    /// <param name="actionText">Verb des Knopfes, z. B. „Erneut versuchen“; nur zusammen mit <paramref name="action"/>.</param>
    private void ShowProblem(string title, string? message, bool isError = true, string? actionText = null, Action? action = null)
    {
        Problem = new CalendarProblem(isError ? InfoBarSeverity.Error : InfoBarSeverity.Warning, title, string.IsNullOrWhiteSpace(message) ? "Bitte noch einmal versuchen." : message,
            action is null ? null : actionText ?? "Erneut versuchen",
            action is null ? null : new RelayCommand(() =>
            {
                Problem = null;
                action();
            }));
    }

    [RelayCommand]
    private void DismissProblem() => Problem = null;

    /// <summary>Was Konfliktmeldungen im Kalender anbieten: Termin markieren, „Zum Konflikt“ und das Gegenstück von Person und Raum.</summary>
    private ConflictLinks ConflictLinksOf => new(ShowAppointment, GoToConflict, CounterpartOf);

    /// <summary>
    /// „Zum Konflikt“: die Wochenansicht der Person (bei einem Raum im Weg: des Raums), die den Termin belegt, beginnend am Tag des Termins, mit dem Termin markiert.
    /// Ohne Person oder Raum (Gerät, Sperrzeit) bleibt es beim Markieren.
    /// </summary>
    public void GoToConflict(BookingConflict conflict)
    {
        if (conflict is not { AppointmentId: { } id, From: { } from })
        {
            return;
        }

        var filter = conflict.Kind switch
        {
            ResourceKind.Employee when _resources?.Employees.FirstOrDefault(e => e.Id == conflict.ResourceId) is { } person =>
                new CalendarFilter(CalendarFilterKind.Employee, person.Id, person.InHouseName(), person.Color),
            ResourceKind.Room when _resources?.Rooms.FirstOrDefault(r => r.Id == conflict.ResourceId) is { } room =>
                new CalendarFilter(CalendarFilterKind.Room, room.Id, room.Name, DayLayout.RoomColor),
            _ => null,
        };
        if (filter is null)
        {
            ShowAppointment(id, from);
            return;
        }

        _markAfterLoad = id;
        _changingDay = true;
        try
        {
            ByRoom = filter.ByRoom;
            Filter = filter;
            Date = DateOnly.FromDateTime(DayLayout.Local(from, _zone));
        }
        finally
        {
            _changingDay = false;
        }

        _ = LoadAsync();
    }

    /// <summary>Zum belegenden Termin der Konfliktmeldung das Gegenstück der belegten Art: bei einer Person der Raum, bei einem Raum die Person, sonst beides.</summary>
    private string? CounterpartOf(Guid appointmentId, ResourceKind kind)
    {
        if (_day?.Appointments.FirstOrDefault(a => a.Id == appointmentId) is not { } appointment || _resources is null)
        {
            return null;
        }

        var person = _resources.Employees.FirstOrDefault(e => e.Id == appointment.EmployeeId)?.InHouseName();
        var room = _resources.Rooms.FirstOrDefault(r => r.Id == appointment.RoomId)?.Name;
        return kind switch
        {
            ResourceKind.Employee => room,
            ResourceKind.Room => person,
            _ => string.Join(", ", new[] { person, room }.OfType<string>()),
        };
    }

    /// <summary>Zeile für den Toast: ohne Schlusspunkt und höchstens <see cref="ToastMaxLength"/> Zeichen (sonst mit „…“ gekürzt).</summary>
    internal static string FitToast(string text)
    {
        var line = text.Trim().TrimEnd('.');
        return line.Length <= ToastMaxLength ? line : string.Concat(line.AsSpan(0, ToastMaxLength - 1).TrimEnd(), "…");
    }
}
