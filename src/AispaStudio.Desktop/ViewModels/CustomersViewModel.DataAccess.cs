using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

// ---------- „Auskunft als PDF“ in der Kundenkarte (PDF-Ausgaben C, ADR 0091) ----------
public partial class CustomersViewModel
{
    /// <summary>
    /// Knopfblock der Datenschutz-Auskunft (Art. 15 DSGVO) der ausgewählten Person; ohne Senden: die Leitung übergibt die Auskunft persönlich oder schickt sie selbst.
    /// Ob Gesundheitsangaben darin stehen, entscheidet die API nach dem Recht der angemeldeten Person.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDataAccessOpen))]
    public partial DocumentActionsViewModel? DataAccessDocument { get; private set; }

    /// <summary>Meldung nach dem Speichern oder Drucken („Gespeichert: Auskunft-Eva-Muster.pdf“), vom Bildschirmleser angesagt.</summary>
    [ObservableProperty]
    public partial string? DataAccessMessage { get; private set; }

    public bool IsDataAccessOpen => DataAccessDocument is not null;

    [RelayCommand]
    private void ToggleDataAccess()
    {
        DataAccessMessage = null;
        DataAccessDocument = DataAccessDocument is null && Selected is { } customer
            ? new DocumentActionsViewModel(DataAccessSource(customer), api, Dialogs) { ShowToast = text => DataAccessMessage = text }
            : null;
    }

    internal static DocumentSource DataAccessSource(AispaStudio.Contracts.CustomerDto customer) =>
        new($"Auskunft {customer.InHouseName}", $"api/customers/{customer.Id}/history/pdf");
}
