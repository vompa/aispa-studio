using AispaStudio.Contracts;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

// ---------- Stand der Anzahlung (Ä285) und Anzahlung retournieren (ADR 0073, Ä47) ----------
public partial class AppointmentViewModel
{
    /// <summary>
    /// Inhaber und Manager dürfen eine Anzahlung retournieren; nur dann fragt das Panel nach ihr und zeigt den Knopf „Anzahlung retournieren …“. Der Server prüft es bei jedem Aufruf
    /// (403 für alle anderen), die Oberfläche blendet nur aus.
    /// </summary>
    public bool MayReturnDeposit { get; init; }

    /// <summary>Die offene Anzahlung des Termins (bezahlt, nicht verrechnet, Anzahlungsbeleg nicht storniert); leer, wenn es keine gibt oder sie nicht geladen ist.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasReturnableDeposit), nameof(DepositText), nameof(HasDepositCard))]
    public partial DepositReturnInfo? ReturnableDeposit { get; private set; }

    /// <summary>Der Abschnitt „Anzahlung“ mit dem Knopf ist da: es gibt eine offene Anzahlung und die Person darf sie retournieren.</summary>
    public bool HasReturnableDeposit => MayReturnDeposit && ReturnableDeposit is { CanReturn: true };

    /// <summary>„30,00 € bezahlt (Karte), Beleg K1 Nr. 12, 08.03.2027.“ und wohin das Geld geht.</summary>
    public string DepositText => ReturnableDeposit is { } deposit
        ? $"{Money.Format(deposit.Amount, deposit.Currency)} bezahlt ({deposit.Way}), Beleg {deposit.ReceiptText}. Beim Retournieren geht das Geld auf diesem Weg zurück: Storno-Beleg über die Kassa, der Grund steht darauf."
        : "";

    /// <summary>Stand der Anzahlung für alle, die den Kalender sehen; eine Anzahlung anfordern gibt es nicht, sie entsteht bei der Online-Buchung.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDepositCard), nameof(HasDepositState), nameof(DepositTitle), nameof(DepositStateText), nameof(DepositLines))]
    public partial DepositStaffDto? DepositState { get; private set; }

    public bool HasDepositCard => DepositState is not null || HasReturnableDeposit;

    public bool HasDepositState => DepositState is not null;

    public string DepositTitle => DepositState is { Percent: >= 100 } ? "Vorauszahlung" : "Anzahlung";

    /// <summary>„Bezahlt, Anzahlungsbeleg erstellt · 30,00 € (30 %) am 19.10.2026 09:00“.</summary>
    public string DepositStateText => DepositState is { } deposit
        ? $"{DepositStatusText(deposit)} · {Money.Format(deposit.Amount, deposit.Currency)} ({deposit.Percent} %){(deposit.PaidAt is { } paid ? $" am {Stamp(paid)}" : "")}"
        : "";

    /// <summary>Erklärung des Servers, Frist und Fehler unter dem Stand.</summary>
    public IReadOnlyList<string> DepositLines
    {
        get
        {
            if (DepositState is not { } deposit)
            {
                return [];
            }

            List<string> lines = [];
            AddIfPresent(lines, deposit.Hint);
            if (deposit is { ReleaseAt: { } release, Status: OnlineDepositStatus.Pending or OnlineDepositStatus.Failed })
            {
                lines.Add($"Freigabe ohne Zahlung am {Stamp(release)}");
            }

            AddIfPresent(lines, deposit.Error);
            return lines;
        }
    }

    /// <summary>Lädt Stand und offene Anzahlung; ohne Anzahlung oder bei einem Fehler zeigt das Panel nichts. Solange der Beleg entsteht, fragt es nach.</summary>
    public async Task LoadDepositAsync()
    {
        await LoadDepositOnceAsync().ConfigureAwait(true);
        for (var i = 0; i < StatusPolls && DepositState?.Status == OnlineDepositStatus.Paid; i++)
        {
            await Task.Delay(StatusPollInterval).ConfigureAwait(true);
            await LoadDepositOnceAsync().ConfigureAwait(true);
        }
    }

    private async Task LoadDepositOnceAsync()
    {
        await LoadDepositStateAsync().ConfigureAwait(true);
        if (MayReturnDeposit)
        {
            ReturnableDeposit = (await Api.GetAsync<DepositReturnInfo>($"api/appointments/{Appointment.Id}/deposit-return").ConfigureAwait(true)).Value;
        }
    }

    private async Task LoadDepositStateAsync() =>
        DepositState = (await Api.GetAsync<DepositStaffDto>($"api/appointments/{Appointment.Id}/deposit").ConfigureAwait(true)).Value;

    private static string DepositStatusText(DepositStaffDto deposit) => deposit switch
    {
        { Voided: true } => "Anzahlungsbeleg storniert",
        { Status: OnlineDepositStatus.Pending } => "Noch nicht bezahlt",
        { Status: OnlineDepositStatus.Paid } => "Bezahlt, Beleg wird erstellt …",
        { Status: OnlineDepositStatus.Receipted } => "Bezahlt, Anzahlungsbeleg erstellt",
        { Status: OnlineDepositStatus.Failed } => "Zahlung abgebrochen",
        { Status: OnlineDepositStatus.Cancelled } => "Termin storniert, nichts bezahlt",
        { Status: OnlineDepositStatus.Refunded } => "Erstattet",
        _ => "Termin freigegeben (nicht bezahlt)",
    };

    /// <summary>
    /// „Anzahlung retournieren …“: fragt nach (Rückfragedialog, Design-Brief 5.27) mit Pflichtgrund; erst „Retournieren“ stellt den Storno-Beleg aus. Ohne Grund fragt der Dialog noch einmal.
    /// </summary>
    [RelayCommand]
    private async Task ReturnDepositAsync()
    {
        if (ReturnableDeposit is not { CanReturn: true } deposit)
        {
            return;
        }

        var amount = Money.Format(deposit.Amount, deposit.Currency);
        var reason = "";
        var hint = "";
        while (reason.Length == 0)
        {
            var answer = await AskAsync(new ConfirmRequest("Anzahlung retournieren?",
                $"{hint}Die Anzahlung von {amount} geht auf dem Zahlungsweg der Einzahlung ({deposit.Way}) zurück. Dafür entsteht ein Storno-Beleg an der Kassa; der Grund steht darauf.",
                "Retournieren", InputLabel: "Grund (Pflicht)")).ConfigureAwait(true);
            if (!answer.IsConfirmed)
            {
                return;
            }

            reason = answer.Input?.Trim() ?? "";
            hint = "Bitte einen Grund angeben. ";
        }

        var result = await Api.PostAsync<ReceiptDto>($"api/appointments/{Appointment.Id}/deposit-return", new DepositReturnRequest(reason)).ConfigureAwait(true);
        if (result.Value is not { } receipt)
        {
            ShowFailure("Anzahlung nicht retourniert", result.Error);
            return;
        }

        ReturnableDeposit = null;
        await LoadDepositStateAsync().ConfigureAwait(true);
        ShowToast?.Invoke($"Anzahlung retourniert (Storno-Beleg {receipt.RegisterCode} Nr. {receipt.Number})");
    }
}
