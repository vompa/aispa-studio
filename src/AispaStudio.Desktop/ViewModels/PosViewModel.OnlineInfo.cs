using System.Net;
using AispaStudio.Contracts;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

// ---------- Gutschein abfragen und Rückerstattung online bezahlter Belege (Ä285) ----------
public partial class PosViewModel
{
    private const int RefundPolls = 20;

    /// <summary>Kassenaufsicht: nur sie sieht „Erstattung erneut anstoßen“. Die Shell setzt es aus den Rechten; der Server prüft es ebenso.</summary>
    public bool MaySuperviseCash { get; set; }

    /// <summary>
    /// Fragt den Code beim Verlassen des Felds ab: Restwert und Gültigkeit stammen vom Server, ob eingelöst werden kann, prüft er beim Bezahlen noch einmal.
    /// Ein leeres Betragsfeld bekommt bei einem gültigen Wertgutschein den kleineren Wert aus Restwert und offenem Betrag.
    /// </summary>
    public async Task LookUpVoucherAsync(PaymentEntry entry)
    {
        var code = entry.VoucherCode.Trim();
        if (code.Length == 0)
        {
            return;
        }

        var result = await _api.GetAsync<VoucherDto>($"api/pos/vouchers/{Uri.EscapeDataString(code)}").ConfigureAwait(true);
        if (entry.VoucherCode.Trim() != code)
        {
            return;
        }

        if (result.Value is not { } voucher)
        {
            entry.VoucherError = result.Status == HttpStatusCode.NotFound ? "Diesen Gutschein-Code gibt es nicht." : result.Error ?? "Der Gutschein ließ sich nicht abfragen.";
            return;
        }

        var kind = voucher.Kind == VoucherKind.SinglePurpose ? "Behandlungsgutschein" : $"Restwert {Format(voucher.Balance)} · Wertgutschein";
        if (voucher.Status != VoucherStatus.Active)
        {
            entry.VoucherError = $"{kind} · nicht mehr gültig";
            return;
        }

        entry.VoucherHelp = voucher.ValidUntil is { } until ? $"{kind} · gültig bis {until:dd.MM.yyyy}" : kind;
        if (voucher.Kind == VoucherKind.MultiPurpose && entry.Amount.Length == 0 && Bill is { } bill)
        {
            entry.Amount = Money.Input(Math.Min(voucher.Balance, bill.Open), Currency);
        }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasOnlineRefund), nameof(OnlineRefundTitle), nameof(OnlineRefundText), nameof(OnlineRefundProblem), nameof(CanRetryOnlineRefund))]
    public partial OnlineRefundDto? OnlineRefund { get; private set; }

    /// <summary>Gleich nach dem Storno hat die Erstattung oft noch nicht begonnen; solange nachgefragt wird, sagt der Beleg noch nicht „nicht zurück“.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(OnlineRefundProblem), nameof(CanRetryOnlineRefund))]
    public partial bool IsPollingOnlineRefund { get; private set; }

    public bool HasOnlineRefund => OnlineRefund is not null;

    public string OnlineRefundTitle => OnlineRefund is { } refund ? $"{SourceText(refund.Source)}, online bezahlt über {refund.Provider}." : "";

    /// <summary>Vor dem Storno der Weg des Geldes, danach der Stand der Erstattung.</summary>
    public string OnlineRefundText => OnlineRefund switch
    {
        null => "",
        { Voided: false } refund => $"Ein Storno gibt {Money.Format(refund.Amount, refund.Currency)} über den Zahlungsanbieter zurück und schickt dem Gast den Stornobeleg.",
        { Status: OnlineRefundStatus.Pending } refund => $"Erstattung von {Money.Format(refund.Amount, refund.Currency)} läuft …",
        { Status: OnlineRefundStatus.Refunded } refund => $"{Money.Format(refund.Amount, refund.Currency)} zurückerstattet, der Gast hat den Stornobeleg per Mail.",
        _ => "",
    };

    public string? OnlineRefundProblem => OnlineRefund switch
    {
        { Status: OnlineRefundStatus.Failed } refund => $"Nicht erstattet: {refund.Error}",
        { Voided: true, Status: null } when !IsPollingOnlineRefund => "Storniert, aber das Geld ist noch nicht zurück.",
        _ => null,
    };

    public bool CanRetryOnlineRefund => MaySuperviseCash && OnlineRefund is { CanRetry: true } && !IsPollingOnlineRefund;

    /// <summary>Lädt die Online-Zahlung hinter dem Beleg; bei einem Stornobeleg die des stornierten Belegs. Ohne Online-Zahlung oder bei einem Fehler zeigt die Kassa nichts.</summary>
    private async Task LoadOnlineRefundAsync(ReceiptDto receipt)
    {
        var id = receipt.VoidsReceiptId ?? receipt.Id;
        IsPollingOnlineRefund = true;
        for (var i = 0; i < RefundPolls; i++)
        {
            var result = await _api.GetAsync<OnlineRefundDto>($"api/pos/receipts/{id}/online-refund").ConfigureAwait(true);
            if (SelectedReceiptItem?.Receipt.Id != receipt.Id)
            {
                return;
            }

            OnlineRefund = result.Value;
            if (result.Value is not ({ Status: OnlineRefundStatus.Pending } or { Voided: true, Status: null }))
            {
                break;
            }

            await Task.Delay(_cardPoll).ConfigureAwait(true);
            if (SelectedReceiptItem?.Receipt.Id != receipt.Id)
            {
                return;
            }
        }

        IsPollingOnlineRefund = false;
    }

    [RelayCommand]
    private async Task RetryOnlineRefundAsync()
    {
        if (OnlineRefund is not { CanRetry: true } refund || SelectedReceiptItem is not { } item)
        {
            return;
        }

        var result = await _api.PostAsync<OnlineRefundDto>($"api/pos/receipts/{refund.ReceiptId}/online-refund", null).ConfigureAwait(true);
        if (SelectedReceiptItem != item)
        {
            return;
        }

        if (result.Value is null)
        {
            Error = result.Error;
            return;
        }

        OnlineRefund = result.Value;
        if (result.Value.Status == OnlineRefundStatus.Pending)
        {
            await LoadOnlineRefundAsync(item.Receipt).ConfigureAwait(true);
        }
    }

    private static string SourceText(OnlinePaymentSource source) => source switch
    {
        OnlinePaymentSource.Voucher => "Online-Gutschein",
        OnlinePaymentSource.NoShowFee => "Ausfallgebühr",
        _ => "Online-Anzahlung",
    };
}
