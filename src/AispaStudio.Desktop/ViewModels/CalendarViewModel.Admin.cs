using AispaStudio.Contracts;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>
/// Sprünge in die Verwaltung (native Masken) direkt aus dem Kalender: Vor- und Nachbereitung einer Behandlung bearbeiten,
/// Raum bearbeiten. Die Arbeitsfläche öffnet die Verwaltung bei der Seite, dem Reiter und, wo es das gibt, der Behandlung bzw. dem Raum.
/// </summary>
public partial class CalendarViewModel
{
    /// <summary>„Verwaltung öffnen“: Seite (z. B. „masterData“), Reiter (z. B. „treatments“) und die Behandlung bzw. der Raum; <c>null</c> ohne Verwaltung.</summary>
    public Action<string, string?, Guid?>? AdminRequested { get; set; }

    /// <summary>Behandlungen und Räume pflegt, wer den Katalog bzw. die Standorte verwalten darf.</summary>
    public bool CanEditCatalog => AdminRequested is not null && _user.Rights.Contains(AccessRight.ManageCatalog);

    public bool CanEditRooms => AdminRequested is not null && _user.Rights.Contains(AccessRight.ManageLocation);

    /// <summary>Rechtsklick auf einen Streifen (Vorbereitung, Nachbereitung, Raumpflege): Vor-/Nachbereitung der Behandlung bearbeiten.</summary>
    public IReadOnlyList<CalendarAction> ActionsForStripe(CalendarStripe stripe)
    {
        var actions = new List<CalendarAction>();
        if (stripe.Care is { } care && CanCheckCare)
        {
            actions.Add(new(care.State == RoomCareState.Done ? "Raumpflege zurücknehmen" : "Raumpflege erledigt", null, () => _ = ToggleCareAsync(stripe)));
        }

        if (stripe.Appointment is { } appointment)
        {
            actions.AddRange(PreparationActions(appointment));
        }

        return actions;
    }

    /// <summary>
    /// „Zeiten dieses Termins ändern …“ nur für diesen Termin (öffnet das Panel mit den Zahlenfeldern), dazu
    /// „Behandlungszeiten bearbeiten …“: öffnet die Behandlung des Termins; Zeiten und Vorbereitungsschritte stehen dort im Bereich Ablauf.
    /// </summary>
    private List<CalendarAction> PreparationActions(AppointmentDto appointment)
    {
        var actions = new List<CalendarAction>();
        if (CanBook && appointment.Status is AppointmentStatus.Booked or AppointmentStatus.CheckedIn)
        {
            actions.Add(new("Zeiten dieses Termins ändern …", null, () =>
            {
                OpenAppointment(appointment);
                Selected?.BeginEditTimes();
            }));
        }

        if (CanEditCatalog)
        {
            actions.Add(new("Behandlungszeiten bearbeiten …", null, () => AdminRequested!("masterData", "treatments", appointment.TreatmentId)));
        }

        return actions;
    }
}
