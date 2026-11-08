namespace AispaStudio.Desktop.ViewModels;

// ---------- Abschnitt „Verlauf“ im Termin-Panel (Ä184, ADR 0082) ----------
public partial class AppointmentViewModel
{
    private HistorySectionViewModel? _historySection;

    /// <summary>Der Abschnitt „Verlauf“; er lädt erst beim Aufklappen.</summary>
    public HistorySectionViewModel HistorySection => _historySection ??= new HistorySectionViewModel(Api, $"api/appointments/{Appointment.Id}/history", Zone, "Wer den Termin wann über welchen Weg angelegt und geändert hat");
}
