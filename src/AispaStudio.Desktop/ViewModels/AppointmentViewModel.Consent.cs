using AispaStudio.Contracts;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>Sprache der Bestätigungsmail an den Gast.</summary>
public sealed record ConsentLanguage(string Code, string Name)
{
    public static IReadOnlyList<ConsentLanguage> All { get; } =
        [new("de", "Deutsch"), new("en", "Englisch"), new("fr", "Französisch"), new("it", "Italienisch")];
}

// ---------- Einwilligung für Wiederbuchungs-Hinweise (Ä292) ----------
public partial class AppointmentViewModel
{
    private string ConsentPath => $"api/customers/{Appointment.CustomerId}/marketing-consent";

    /// <summary>Stand der Einwilligung des Gastes; leer, solange er nicht geladen ist.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasConsent), nameof(ConsentText), nameof(CanRequestConsent), nameof(CanWithdrawConsent))]
    public partial MarketingConsentDto? Consent { get; private set; }

    [ObservableProperty]
    public partial ConsentLanguage ConsentMailLanguage { get; set; } = ConsentLanguage.All[0];

    public IReadOnlyList<ConsentLanguage> ConsentLanguages { get; } = ConsentLanguage.All;

    public bool HasConsent => Consent is not null;

    /// <summary>Anfragen und Widerrufen brauchen das Recht zum Buchen; der Server prüft es ebenso.</summary>
    public bool CanRequestConsent => MayBook && Consent is { State: not MarketingConsentState.Confirmed };

    public bool CanWithdrawConsent => MayBook && Consent is { State: MarketingConsentState.Confirmed or MarketingConsentState.Pending };

    public string ConsentText => Consent switch
    {
        null => "",
        { State: MarketingConsentState.Confirmed } c => $"Eingewilligt am {Day(c.ConfirmedAt)} ({c.Email})",
        { State: MarketingConsentState.Pending } c => $"Angefragt am {Day(c.RequestedAt)}, wartet auf Bestätigung durch den Gast ({c.Email})",
        { State: MarketingConsentState.Withdrawn } c => $"Widerrufen am {Day(c.WithdrawnAt)}",
        { State: MarketingConsentState.Refused } c => $"Abgelehnt am {Day(c.WithdrawnAt)}",
        _ => "Nicht angefragt",
    };

    private string Day(DateTimeOffset? moment) => moment is { } value ? $"{DayLayout.Local(value, Zone):dd.MM.yyyy}" : "";

    /// <summary>Lädt den Stand; bei einem Fehler zeigt das Panel die Karte nicht.</summary>
    public async Task LoadConsentAsync() =>
        Consent = (await Api.GetAsync<MarketingConsentDto>(ConsentPath).ConfigureAwait(true)).Value;

    /// <summary>Fragt den Gast per Mail um Einwilligung (Double-Opt-in); der neue Stand kommt aus der Antwort.</summary>
    [RelayCommand]
    private async Task RequestConsentAsync()
    {
        var result = await Api.PostAsync<MarketingConsentDto>(ConsentPath, new MarketingConsentRequest(ConsentMailLanguage.Code)).ConfigureAwait(true);
        if (result.Value is not { } consent)
        {
            ShowFailure("Einwilligung nicht angefragt", result.Error);
            return;
        }

        Consent = consent;
        ShowToast?.Invoke(consent.State == MarketingConsentState.Confirmed ? "Der Gast hat schon eingewilligt" : "Mail zum Bestätigen ist unterwegs");
    }

    /// <summary>Widerruf, wenn der Gast am Empfang sagt, er möchte keine Erinnerungen mehr.</summary>
    [RelayCommand]
    private async Task WithdrawConsentAsync()
    {
        var result = await Api.DeleteAsync<MarketingConsentDto>(ConsentPath).ConfigureAwait(true);
        if (result.Value is not { } consent)
        {
            ShowFailure("Einwilligung nicht widerrufen", result.Error);
            return;
        }

        Consent = consent;
        ShowToast?.Invoke("Einwilligung widerrufen");
    }
}
