using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;

namespace AispaStudio.Desktop.ViewModels;

// ---------- Abschnitt „Kunde“ im Termin-Panel (Runde 5, Ä34) ----------
public partial class AppointmentViewModel
{
    private CustomerSectionViewModel? _customerSection;

    /// <summary>„Geburtstag heute · 42“ oder, drei Tage davor und danach, „Geburtstag am 25.10.“ (Ä45); der Server rechnet, <see cref="BirthdayText"/> setzt den Text.</summary>
    public string? Birthday { get; init; }

    public bool HasBirthday => Birthday is not null;

    /// <summary>Land des Standorts: seine Landesvorwahl ergänzt eine nationale Nummer für den WhatsApp-Link.</summary>
    public FiscalCountry DefaultCountry { get; init; } = FiscalCountry.AT;

    /// <summary>Öffnet einen anderen Termin des Gastes („Zuletzt“): der Kalender springt zu seinem Tag, markiert ihn und zeigt sein Panel (Id und Beginn).</summary>
    public Action<Guid, DateTimeOffset>? OpenOtherAppointment { get; init; }

    /// <summary>Kurze Rückmeldung unten im Kalender („Kopiert“).</summary>
    public Action<string>? ShowToast { get; init; }

    /// <summary>Öffnen von Links und Kopieren; das Fenster setzt es am Kalender, das Panel fragt es erst beim Klick ab.</summary>
    public Func<IContactActions?> ContactActions { get; init; } = () => null;

    /// <summary>
    /// Der Abschnitt „Kunde“; ein Gast ohne Namen hat keinen. Er lädt beim ersten Zeigen und nicht schon beim Bauen des Panels, damit jeder Klick auf einen
    /// Termin nur lädt, was die Ansicht auch zeigt.
    /// </summary>
    public CustomerSectionViewModel? CustomerSection
    {
        get
        {
            if (IsPlaceholder)
            {
                return null;
            }

            if (_customerSection is null)
            {
                _customerSection = new CustomerSectionViewModel(Api, Appointment.CustomerId, Title, DefaultCountry, Zone, Time, CanShowCustomer ? ShowCustomerAsync : null,
                    OpenOtherAppointment, ShowToast, ContactActions);
                _ = _customerSection.LoadAsync();
            }

            return _customerSection;
        }
    }
}
