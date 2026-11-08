using AispaStudio.Contracts;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

// ---------- Gast ohne Namen (Gruppe, Firmen-Event): Name im Termin nachtragen ----------
public partial class AppointmentViewModel
{
    private CustomerSearchViewModel? _guestSearch;

    public bool IsPlaceholder => Appointment.PlaceholderGuest is not null;

    /// <summary>„Gast 3 · Name folgt“ statt des Namens der buchenden Person.</summary>
    public static string GuestName(AppointmentDto a) => a.PlaceholderGuest is { } number ? $"Gast {number} · Name folgt" : a.CustomerName;

    public string BookedBy => $"Gebucht von {Appointment.CustomerName}. Der Name gilt für alle Termine dieses Gastes in der Buchung.";

    public CustomerSearchViewModel GuestSearch
    {
        get
        {
            if (_guestSearch is null)
            {
                _guestSearch = new CustomerSearchViewModel(Api);
                _guestSearch.Changed += () => NameGuestCommand.NotifyCanExecuteChanged();
            }

            return _guestSearch;
        }
    }

    private bool CanAssign => GuestSearch.HasCustomer;

    [RelayCommand(CanExecute = nameof(CanAssign))]
    private async Task NameGuestAsync()
    {
        if (GuestSearch.Customer is not { } customer)
        {
            return;
        }

        Error = null;
        var result = await Api.PutAsync<GroupBookingResult>($"api/appointments/{Appointment.Id}/guest", new AssignGuestRequest(customer.Id))
            .ConfigureAwait(true);
        await GroupDoneAsync(result).ConfigureAwait(true);
    }
}
