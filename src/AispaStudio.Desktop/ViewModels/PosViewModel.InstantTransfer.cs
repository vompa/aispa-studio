using System.Globalization;
using AispaStudio.Contracts;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

// ---------- Sofortüberweisung: Zahlungsdaten des Servers anzeigen (Ä290) ----------
public partial class PosViewModel
{
    /// <summary>
    /// Holt die Zahlungsdaten für den Betrag der Zeile (ohne Eingabe der offene Rest nach den anderen Zahlungen) und trägt den Betrag des Servers ein.
    /// Empfänger, IBAN und Zahlungstext stammen vom Server; die App rechnet und bildet nichts nach.
    /// </summary>
    [RelayCommand]
    private async Task ShowTransferAsync(PaymentEntry entry)
    {
        if (Bill is not { } bill)
        {
            return;
        }

        var amount = Money.Parse(entry.Amount) ?? 0;
        if (amount <= 0)
        {
            amount = Math.Max(0, bill.Open - Payments.Where(p => p != entry).Sum(p => Money.Parse(p.Amount) ?? 0));
        }

        var query = amount > 0 ? $"?amount={amount.ToString(CultureInfo.InvariantCulture)}" : "";
        var result = await _api.GetAsync<InstantTransferQr>($"api/pos/bills/{bill.Id}/instant-transfer{query}").ConfigureAwait(true);
        if (result.Value is not { } transfer)
        {
            entry.Transfer = null;
            Error = result.Error ?? "Die Zahlungsdaten ließen sich nicht abrufen.";
            return;
        }

        Error = null;
        entry.Transfer = transfer;
        entry.Amount = Money.Input(transfer.Amount, Currency);
    }
}
