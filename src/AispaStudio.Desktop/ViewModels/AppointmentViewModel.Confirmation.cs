using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

// ---------- Terminbestätigung hinter „Mehr“ (PDF-Ausgaben C, ADR 0091) ----------
public partial class AppointmentViewModel
{
    /// <summary>Die Bestätigung gibt es für jeden Termin, der nicht storniert ist; den Inhalt rechnet die API, das Recht ist das des Termins (Kalender sehen).</summary>
    public bool CanConfirm => Appointment.Status != AispaStudio.Contracts.AppointmentStatus.Cancelled;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsConfirmationOpen))]
    public partial DocumentActionsViewModel? ConfirmationDocument { get; private set; }

    public bool IsConfirmationOpen => ConfirmationDocument is not null;

    /// <summary>„Bestätigung …“ öffnet den Bereich mit „Drucken · PDF speichern · Per Mail senden“ unter „Mehr“; ein zweiter Klick schließt ihn.</summary>
    [RelayCommand]
    private void ToggleConfirmation()
    {
        ConfirmationDocument = ConfirmationDocument is null ? new DocumentActionsViewModel(ConfirmationSource(), Api, Dialogs) { ShowToast = ConfirmationToast } : null;
    }

    internal DocumentSource ConfirmationSource() =>
        new($"Terminbestätigung {ShortDateText}", $"api/appointments/{Appointment.Id}/confirmation/pdf", $"api/appointments/{Appointment.Id}/confirmation/send");

    /// <summary>Meldung im Kalender; nach dem Senden steht der Vermerk gleich im Verlauf des Termins.</summary>
    private void ConfirmationToast(string text)
    {
        ShowToast?.Invoke(text);
        if (_historySection is { IsOpen: true } history && text.StartsWith("Gesendet", StringComparison.Ordinal))
        {
            _ = history.LoadAsync();
        }
    }
}
