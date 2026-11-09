using AispaStudio.Contracts;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>Der Chip „Storniert n“ in der Kopfzeile: stornierte Termine des gezeigten Tages, ein Klick öffnet sie im Verlauf.</summary>
public partial class CalendarViewModel
{
    /// <summary>Wird mit dem gezeigten Tag aufgerufen, wenn der Chip „Storniert n“ geklickt wird; die Shell wechselt zur Seite Verlauf (Zeitraum dieser Tag, Art Stornos).</summary>
    public Action<DateOnly>? CancelledRequested { get; set; }

    /// <summary>Stornierte Termine des gezeigten Tages am Standort; der Kalender zeigt sie nicht, der geladene Tag enthält sie aber.</summary>
    public int CancelledCount => _day is { } day
        ? day.Appointments.Count(a => a.Status == AppointmentStatus.Cancelled && a.LocationId == day.LocationId && DateOnly.FromDateTime(DayLayout.Local(a.Start, _zone)) == Date)
        : 0;

    public bool HasCancelled => CancelledCount > 0;

    /// <summary>Text des Chips: „Storniert 2“, kompakt nur die Zahl.</summary>
    public string CancelledLabel => IsCompactHeader ? CancelledCount.ToString(System.Globalization.CultureInfo.CurrentCulture) : $"Storniert {CancelledCount}";

    private void NotifyCancelled()
    {
        OnPropertyChanged(nameof(CancelledCount));
        OnPropertyChanged(nameof(HasCancelled));
        OnPropertyChanged(nameof(CancelledLabel));
    }

    [RelayCommand]
    private void OpenCancelled() => CancelledRequested?.Invoke(Date);
}
