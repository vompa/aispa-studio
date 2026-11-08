using AispaStudio.Contracts;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>Was ein Klick auf eine Zahl im Tagesüberblick hervorhebt; alle anderen Einträge werden blass.</summary>
public enum CalendarFocus
{
    None,

    /// <summary>Alle Termine des Tages (ohne Sperrzeiten und Pausen).</summary>
    Appointments,
    CheckedIn,

    /// <summary>Gebucht, aber noch nicht eingecheckt.</summary>
    Expected,

    /// <summary>Noch erwartet und nicht über den Link zugesagt: diese Gäste anrufen.</summary>
    Unconfirmed,

    /// <summary>Begonnen, aber noch nicht eingecheckt (wie <see cref="CalendarItem.IsOverdue"/>).</summary>
    Overdue,
}

/// <summary>Zahlen im Tagesüberblick: ein Klick hebt die passenden Termine hervor und springt zum ersten.</summary>
public partial class CalendarViewModel
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFocus), nameof(FocusText), nameof(KeyHints), nameof(CanResetView))]
    public partial CalendarFocus Focus { get; private set; }

    public bool HasFocus => Focus != CalendarFocus.None;

    /// <summary>Chip in der Kopfzeile, solange hervorgehoben wird; ein Klick darauf hebt es auf.</summary>
    public string FocusText => Focus switch
    {
        CalendarFocus.Appointments => $"Nur Termine: {FocusCount}",
        CalendarFocus.CheckedIn => $"Nur eingecheckt: {FocusCount}",
        CalendarFocus.Expected => $"Nur noch erwartet: {FocusCount}",
        CalendarFocus.Unconfirmed => $"Nur unbestätigt: {FocusCount}",
        CalendarFocus.Overdue => $"Nur überfällig: {FocusCount}",
        _ => "",
    };

    private int FocusCount => _day?.Appointments.Count(a => Matches(Focus, a)) ?? 0;

    /// <summary>Gleiche Zahl ein zweites Mal: wieder alles zeigen.</summary>
    [RelayCommand]
    private void FocusOn(CalendarFocus focus)
    {
        Focus = Focus == focus ? CalendarFocus.None : focus;
        if (Focus != CalendarFocus.None
            && _day?.Appointments.Where(a => Matches(Focus, a)).OrderBy(a => a.Start).FirstOrDefault() is { } first)
        {
            // Zum ersten passenden Termin, damit er zu sehen ist.
            MarkedId = first.Id;
        }

        Rebuild();
    }

    [RelayCommand]
    private void ClearFocus()
    {
        Focus = CalendarFocus.None;
        Rebuild();
    }

    private bool Matches(CalendarFocus focus, AppointmentDto a) => focus switch
    {
        CalendarFocus.Appointments => a.Status != AppointmentStatus.Cancelled,
        CalendarFocus.CheckedIn => a.Status == AppointmentStatus.CheckedIn,
        CalendarFocus.Expected => a.Status == AppointmentStatus.Booked,
        CalendarFocus.Unconfirmed => a is { Status: AppointmentStatus.Booked, ConfirmedAt: null },
        CalendarFocus.Overdue => DayLayout.OverdueMinutes(a, _time.GetUtcNow()) > 0,
        _ => true,
    };

    private void ApplyFocus(IReadOnlyList<CalendarColumn> columns)
    {
        if (Focus == CalendarFocus.None)
        {
            return;
        }

        foreach (var column in columns)
        {
            for (var i = 0; i < column.Items.Count; i++)
            {
                var item = column.Items[i];
                column.Items[i] = item with { IsFaded = item.Appointment is not { } a || !Matches(Focus, a) };
            }
        }

        OnPropertyChanged(nameof(FocusText));
    }
}
