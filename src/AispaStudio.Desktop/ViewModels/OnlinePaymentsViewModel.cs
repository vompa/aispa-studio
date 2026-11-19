using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>
/// Eine Zeile der Karte „Online-Zahlungen“: was, wie viel und wann in der ersten Zeile, Stand und Belege darunter.
/// Bei einem Problem nennt <paramref name="Detail"/> nur den Zustand (in normaler Textgröße) und <paramref name="Next"/> den nächsten Schritt samt Belegen und Fehlertext (klein).
/// </summary>
public sealed record OnlinePaymentLine(string Title, string Detail, bool IsProblem, string? Next = null)
{
    public bool HasNext => !string.IsNullOrEmpty(Next);
}

/// <summary>
/// „Online-Zahlungen …“ in der Kopfzeile der Kassa: bezahlte Online-Gutscheine und Erstattungen online bezahlter Belege des Standorts, je die neuesten 100.
/// Nur zum Nachsehen; erstattet wird weiter über das Storno des Belegs, erneut angestoßen im Beleg selbst.
/// </summary>
public sealed partial class OnlinePaymentsViewModel : ObservableObject
{
    private readonly Guid _locationId;
    private readonly IWorkstationApi _api;
    private readonly TimeZoneInfo _zone;
    private readonly Action _close;

    public OnlinePaymentsViewModel(Guid locationId, IWorkstationApi api, TimeZoneInfo zone, Action close)
    {
        _locationId = locationId;
        _api = api;
        _zone = zone;
        _close = close;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasOrders), nameof(NoOrders))]
    public partial IReadOnlyList<OnlinePaymentLine> Orders { get; private set; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasRefunds), nameof(NoRefunds))]
    public partial IReadOnlyList<OnlinePaymentLine> Refunds { get; private set; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NoOrders), nameof(NoRefunds))]
    public partial bool IsLoading { get; private set; }

    [ObservableProperty]
    public partial string? Error { get; private set; }

    public bool HasOrders => Orders.Count > 0;

    public bool NoOrders => !IsLoading && Orders.Count == 0;

    public bool HasRefunds => Refunds.Count > 0;

    public bool NoRefunds => !IsLoading && Refunds.Count == 0;

    public async Task LoadAsync()
    {
        IsLoading = true;
        Error = null;
        try
        {
            var orders = await _api.GetAsync<List<VoucherOrderStaffEntry>>($"api/locations/{_locationId}/voucher-orders").ConfigureAwait(true);
            var refunds = await _api.GetAsync<List<OnlineRefundStaffEntry>>($"api/locations/{_locationId}/online-refunds").ConfigureAwait(true);
            Orders = [.. (orders.Value ?? []).Select(OrderLine)];
            Refunds = [.. (refunds.Value ?? []).Select(RefundLine)];
            if (orders.Value is null || refunds.Value is null)
            {
                Error = orders.Error ?? refunds.Error ?? "Die Online-Zahlungen ließen sich nicht laden.";
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void Close() => _close();

    private string Stamp(DateTimeOffset at) => $"{DayLayout.Local(at, _zone):dd.MM.yyyy HH:mm}";

    /// <summary>„Wertgutschein 50,00 € · 17.11.2026 09:12“ und „Ausgestellt · Beleg K1 Nr. 12, 17.11.2026 · für Maria“.</summary>
    private OnlinePaymentLine OrderLine(VoucherOrderStaffEntry order)
    {
        var kind = order.Kind == VoucherKind.SinglePurpose ? "Behandlungsgutschein" : "Wertgutschein";
        var state = order.Status switch
        {
            VoucherOrderStatus.Paid => "Bezahlt, wird ausgestellt …",
            VoucherOrderStatus.Issued => "Ausgestellt",
            VoucherOrderStatus.Failed => "Bezahlt, aber nicht ausgestellt",
            VoucherOrderStatus.Refunded => "Storniert und erstattet",
            _ => "Offen",
        };
        List<string> parts = [state];
        if (order.Receipt is { } receipt)
        {
            parts.Add($"Beleg {receipt}");
        }

        if (order.RecipientName is { Length: > 0 } recipient)
        {
            parts.Add($"für {recipient}");
        }

        if (order.Error is { Length: > 0 } error)
        {
            parts.Add(error);
        }

        var title = $"{kind} {Money.Format(order.Amount, order.Currency)} · {Stamp(order.CreatedAt)}";
        return order.Status == VoucherOrderStatus.Failed
            ? new(title, state, true, string.Join(" · ", parts.Skip(1)))
            : new(title, string.Join(" · ", parts), false);
    }

    /// <summary>„Online-Gutschein 50,00 € · 17.11.2026 10:00“ und „Erstattet am 17.11.2026 10:01 · Beleg K1 Nr. 12 · Stornobeleg K1 Nr. 13“.</summary>
    private OnlinePaymentLine RefundLine(OnlineRefundStaffEntry refund)
    {
        var source = refund.Source switch
        {
            OnlinePaymentSource.Voucher => "Online-Gutschein",
            OnlinePaymentSource.NoShowFee => "Ausfallgebühr",
            _ => "Online-Anzahlung",
        };
        var state = refund.Status switch
        {
            OnlineRefundStatus.Refunded => refund.RefundedAt is { } at ? $"Erstattet am {Stamp(at)}" : "Erstattet",
            OnlineRefundStatus.Failed => "Erstattung fehlgeschlagen",
            _ => "Erstattung läuft …",
        };
        List<string> parts = [state];
        if (refund.Receipt is { } receipt)
        {
            parts.Add($"Beleg {receipt}");
        }

        if (refund.VoidReceipt is { } voided)
        {
            parts.Add($"Stornobeleg {voided}");
        }

        if (refund.Status == OnlineRefundStatus.Failed && refund.Error is { Length: > 0 } error)
        {
            parts.Add(error);
        }

        var title = $"{source} {Money.Format(refund.Amount, refund.Currency)} · {Stamp(refund.RequestedAt)}";
        return refund.Status == OnlineRefundStatus.Failed
            ? new(title, state, true, string.Join(" · ", parts.Skip(1).Prepend("Im Beleg erneut anstoßen (Kassenaufsicht)")))
            : new(title, string.Join(" · ", parts), false);
    }
}
