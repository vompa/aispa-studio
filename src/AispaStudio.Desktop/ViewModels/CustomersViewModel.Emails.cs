using System.Collections.ObjectModel;
using AispaStudio.Contracts;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>Weitere Mailadresse in der Kundenkarte; das Kreuz entfernt sie.</summary>
public sealed partial class OtherEmailRow(CustomerEmailDto email, Func<CustomerEmailDto, Task> remove)
{
    public string Address => email.Address;

    [RelayCommand]
    private Task RemoveAsync() => remove(email);
}

// ---------- Kundenkarte: weitere Mailadressen und Herkunft aus einer Nachricht (ADR 0061) ----------
public partial class CustomersViewModel
{
    public ObservableCollection<OtherEmailRow> OtherEmails { get; } = [];

    [ObservableProperty]
    public partial string NewOtherEmail { get; set; } = "";

    /// <summary>Name des Felds für <see cref="FocusTarget"/>: nach „Hinzufügen“ ohne gültige Adresse bekommt es den Fokus.</summary>
    public const string OtherEmailField = "OtherEmailBox";

    /// <summary>Fehler am Feld (masken.md, R8); verschwindet mit der nächsten Eingabe.</summary>
    [ObservableProperty]
    public partial string? OtherEmailError { get; private set; }

    partial void OnNewOtherEmailChanged(string value) => OtherEmailError = null;

    /// <summary>„Aus Mail angelegt“: die Terminannahme hat den Kunden aus einer Nachricht angelegt; die Rezeption prüft die Angaben.</summary>
    public string? CreatedFromText => Enum.TryParse<BookingChannel>(Selected?.CreatedFromChannel, out var channel)
        ? $"Aus {InboxViewModel.ChannelName(channel)} angelegt, bitte Angaben prüfen"
        : null;

    private async Task LoadEmailsAsync(CustomerDto? customer)
    {
        OtherEmails.Clear();
        NewOtherEmail = "";
        if (customer is null)
        {
            return;
        }

        var result = await api.GetAsync<List<CustomerEmailDto>>($"api/customers/{customer.Id}/emails").ConfigureAwait(true);
        if (Selected?.Id == customer.Id)
        {
            ShowEmails(result.Value ?? []);
        }
    }

    private void ShowEmails(IEnumerable<CustomerEmailDto> emails)
    {
        OtherEmails.Clear();
        foreach (var email in emails)
        {
            OtherEmails.Add(new OtherEmailRow(email, RemoveEmailAsync));
        }

        RaiseSections();
    }

    [RelayCommand]
    private async Task AddEmailAsync()
    {
        if (!NewOtherEmail.Contains('@', StringComparison.Ordinal))
        {
            OtherEmailError = "Bitte eine E-Mail-Adresse mit @ eintragen, z. B. privat@example.at.";
            FocusTarget = OtherEmailField;
            return;
        }

        if (Selected is not { } customer)
        {
            return;
        }

        var result = await api.PostAsync<List<CustomerEmailDto>>($"api/customers/{customer.Id}/emails", new CustomerEmailInput(NewOtherEmail.Trim()))
            .ConfigureAwait(true);
        if (result.Value is null)
        {
            Error = EmailError(result);
            return;
        }

        Error = null;
        NewOtherEmail = "";
        IsAddingEmail = false;
        ShowEmails(result.Value);
    }

    private async Task RemoveEmailAsync(CustomerEmailDto email)
    {
        if (Selected is not { } customer)
        {
            return;
        }

        var result = await api.DeleteAsync<List<CustomerEmailDto>>($"api/customers/{customer.Id}/emails/{email.Id}").ConfigureAwait(true);
        if (result.Value is null)
        {
            Error = EmailError(result);
            return;
        }

        Error = null;
        ShowEmails(result.Value);
    }

    /// <summary>Weitere Adressen ändern geht nur mit Verbindung (anders als das Anlegen wird nichts vorgemerkt).</summary>
    private static string? EmailError<T>(ApiResult<T> result) => result.Status == 0
        ? "Ohne Verbindung zum Server nicht möglich. Weitere Mailadressen lassen sich nur mit Verbindung ändern."
        : result.Error;
}
