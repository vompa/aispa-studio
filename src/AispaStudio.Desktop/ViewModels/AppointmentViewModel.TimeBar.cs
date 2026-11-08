using AispaStudio.Contracts;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>Was ein Abschnitt des Zeitbalkens im Termin-Panel ist.</summary>
public enum TimeBarKind
{
    Preparation,
    HandsOn,
    Processing,
    Finishing,
    Treatment,
    FollowUp,
    Buffer,
    RoomCare,
}

/// <summary>
/// Ein Abschnitt des Zeitbalkens: Minuten, Anteil an der ganzen Bindung (0 bis 1) und Name. <paramref name="Tone"/> ist bei der Raumpflege
/// „ok“ (erledigt) oder „warn“ (überfällig), sonst leer.
/// </summary>
public sealed record TimeBarSegment(TimeBarKind Kind, int Minutes, double Fraction, string Label, string? Tone = null)
{
    /// <summary>Vor- und Nachbereitung lassen sich im Balken anklicken und ändern.</summary>
    public bool IsEditable => Kind is TimeBarKind.Preparation or TimeBarKind.FollowUp;

    /// <summary>Hinweis am Abschnitt: „Vorbereitung 10 Min.“.</summary>
    public string Tip => $"{Label} {Minutes} Min.";
}

/// <summary>Zeitbalken unter der Zeit-Karte des Termin-Panels (Design-Brief 5.12, Punkt 5): Vorbereitung, Behandlung, Nachbereitung, Puffer, in der Raumansicht die Raumpflege.</summary>
public partial class AppointmentViewModel
{
    /// <summary>Raumpflege nach dem Termin (nur Raumansicht): Minuten und Aufgabe mit ihrem Zustand; ohne Angabe gibt es keinen Abschnitt.</summary>
    public Func<(int Minutes, RoomCareTaskDto? Care)>? RoomCare { get; init; }

    /// <summary>Die Abschnitte in der Reihenfolge Vorbereitung, Behandlung (mit Einwirkzeit dreigeteilt), Nachbereitung, Puffer, Raumpflege; Abschnitte mit 0 Minuten fehlen.</summary>
    public IReadOnlyList<TimeBarSegment> TimeBar
    {
        get
        {
            var (preparation, followUp, buffer) = DayLayout.TimesOf(Appointment);
            var (handsOn, processing) = DayLayout.ProcessingOf(Appointment);
            var minutes = (int)(Appointment.End - Appointment.Start).TotalMinutes;
            var (cleaning, care) = RoomCare?.Invoke() ?? (0, null);
            List<(TimeBarKind Kind, int Minutes, string Label, string? Tone)> parts = [(TimeBarKind.Preparation, preparation, "Vorbereitung", null)];
            if (processing > 0)
            {
                parts.Add((TimeBarKind.HandsOn, handsOn, "Hände", null));
                parts.Add((TimeBarKind.Processing, processing, "Einwirken", null));
                parts.Add((TimeBarKind.Finishing, minutes - handsOn - processing, "Ausarbeiten", null));
            }
            else
            {
                parts.Add((TimeBarKind.Treatment, minutes, "Behandlung", null));
            }

            parts.Add((TimeBarKind.FollowUp, followUp, "Nachbereitung", null));
            parts.Add((TimeBarKind.Buffer, buffer, "Puffer", null));
            parts.Add((TimeBarKind.RoomCare, cleaning, "Raumpflege", care?.State switch
            {
                RoomCareState.Done => "ok",
                RoomCareState.Overdue => "warn",
                _ => null,
            }));

            var shown = parts.Where(p => p.Minutes > 0).ToList();
            var total = shown.Sum(p => p.Minutes);
            return [.. shown.Select(p => new TimeBarSegment(p.Kind, p.Minutes, (double)p.Minutes / total, p.Label, p.Tone))];
        }
    }

    /// <summary>Beginn der Bindung („09:50“), links unter dem Balken.</summary>
    public string TimeBarFrom => DayLayout.Local(Appointment.BlockedFrom, Zone).ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>Ende der Bindung samt Raumpflege („10:55“), rechts unter dem Balken.</summary>
    public string TimeBarUntil => DayLayout.Local(Appointment.BlockedUntil.AddMinutes(RoomCare?.Invoke().Minutes ?? 0), Zone)
        .ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>Die Farbe des Behandlungsabschnitts folgt dem Status wie die Terminkarte: booked, checkedin, done oder noshow.</summary>
    public string TimeBarStatus => Appointment.Status switch
    {
        AppointmentStatus.Booked => "booked",
        AppointmentStatus.CheckedIn => "checkedin",
        AppointmentStatus.Completed => "done",
        _ => "noshow",
    };

    /// <summary>Die Raumansicht wurde ein- oder ausgeschaltet: der Balken zeigt die Raumpflege neu.</summary>
    public void RefreshTimeBar()
    {
        OnPropertyChanged(nameof(TimeBar));
        OnPropertyChanged(nameof(TimeBarUntil));
    }
}
