using System.Collections.ObjectModel;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>Beleg in der Tagesliste.</summary>
public sealed record ReceiptListItem(string Number, string Time, string Total, string Payments, bool IsVoid, bool IsVoided, ReceiptDto Receipt, string? Relation = null)
{
    /// <summary>Name für Screenreader wie angezeigt: Uhrzeit, Nummer, „Stornobeleg“ bzw. „storniert“, Zahlungsart, Betrag („10:15, Nr. 42, Karte, € 89,00“).</summary>
    public string AccessibleName => AccessibleNames.Join(Time, Number, IsVoid ? "Stornobeleg" : null, IsVoided ? "storniert" : null, Payments, Total);
}

/// <summary>Bereich der Kassa: kassieren, Belege des Tages, Kassenlade und Tagesabschluss.</summary>
public enum PosMode
{
    Checkout,
    Receipts,
    Closing,
}

/// <summary>
/// Belege, Einlage/Entnahme und Tagesabschluss. Was gilt, rechnet der Server (Bericht, Soll-Bestand, Differenz);
/// die App zeigt vor dem Abschluss nur die Differenz aus der Eingabe als Vorschau.
/// </summary>
public partial class PosViewModel
{
    public ObservableCollection<ReceiptListItem> Receipts { get; } = [];

    /// <summary>Wie viele Tage vor heute der Reiter „Belege“ zeigt (0 = heute); beim Öffnen des Reiters wieder 0.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ReceiptsTitle), nameof(ReceiptsEmptyText))]
    [NotifyCanExecuteChangedFor(nameof(ShowNextReceiptDayCommand))]
    public partial int ReceiptDaysBack { get; set; }

    private DateOnly ReceiptDay => Today.AddDays(-ReceiptDaysBack);

    public string ReceiptsTitle => ReceiptDaysBack switch
    {
        0 => "Belege heute",
        1 => "Belege gestern",
        _ => $"Belege {DayLayout.DateText(ReceiptDay)}",
    };

    public string ReceiptsEmptyText => ReceiptDaysBack == 0
        ? "Heute noch keine Belege an dieser Kassa."
        : "An diesem Tag gab es keine Belege an dieser Kassa.";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCheckout), nameof(IsReceipts), nameof(IsClosing))]
    public partial PosMode Mode { get; set; }

    public bool IsCheckout => Mode == PosMode.Checkout;

    public bool IsReceipts => Mode == PosMode.Receipts;

    public bool IsClosing => Mode == PosMode.Closing;

    /// <summary>Inhaber und Manager: nur sie stornieren einen Anzahlungsbeleg (ADR 0073, Ä51). Die Shell setzt es aus der Rolle; der Server prüft es ebenso.</summary>
    public bool OwnerLevel
    {
        get => _ownerLevel;
        set
        {
            if (SetProperty(ref _ownerLevel, value))
            {
                OnPropertyChanged(nameof(Export));
                NotifyOutputChanged();
            }
        }
    }

    private bool _ownerLevel;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedReceipt), nameof(VoidDisabledHint), nameof(VoidBlockedByRole), nameof(HasShownReceipt))]
    public partial ReceiptListItem? SelectedReceiptItem { get; set; }

    /// <summary>Der gewählte Beleg der Liste; sein PDF ist ein Nachdruck und trägt „Kopie“. Ein gewählter Ersatzbeleg (ADR 0096) steht an seiner Stelle.</summary>
    public ReceiptViewModel? SelectedReceipt => SelectedReceiptItem is { } item ? ReceiptFor(item.Receipt, copy: true)
        : SelectedSubstitute is { } substitute ? new ReceiptViewModel(substitute.Receipt, Zone, Register?.Country ?? FiscalCountry.AT, OwnerLevel)
        : null;

    /// <summary>Rechts steht ein Beleg der Liste oder ein Ersatzbeleg.</summary>
    public bool HasShownReceipt => SelectedReceiptItem is not null || SelectedSubstitute is not null;

    /// <summary>Der gewählte Beleg ist ein Anzahlungsbeleg und der Nutzer nicht Inhaber oder Manager: „Stornieren“ bleibt gesperrt, auch mit Grund.</summary>
    public bool VoidBlockedByRole => SelectedReceipt?.VoidBlockedByRole == true;

    /// <summary>Warum „Stornieren“ gesperrt ist: wegen der Rolle bei einem Anzahlungsbeleg; sonst ist der Knopf aktiv und prüft den Grund beim Klick.</summary>
    public string? VoidDisabledHint => VoidBlockedByRole ? ReceiptViewModel.DepositVoidBlocked : null;

    [ObservableProperty]
    public partial string VoidReasonInput { get; set; } = "";

    // ---------- Tagesabschluss ----------

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasReport), nameof(ReportRevenue), nameof(ReportPayments), nameof(ReportTaxes), nameof(ReportAccounts),
        nameof(ReportCash), nameof(ReportTitle), nameof(ReportPeriod), nameof(ExpectedCashText), nameof(DifferenceText), nameof(IsClosed))]
    [NotifyCanExecuteChangedFor(nameof(CloseDayCommand))]
    public partial DayClosingDto? Report { get; private set; }

    /// <summary>Der angezeigte Bericht ist ein gespeicherter Tagesabschluss (Z-Bericht), kein Zwischenstand.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ReportTitle), nameof(DifferenceText))]
    [NotifyCanExecuteChangedFor(nameof(CloseDayCommand))]
    public partial bool IsClosed { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DifferenceText))]
    public partial string CountedCash { get; set; } = "";

    [ObservableProperty]
    public partial string ClosingNote { get; set; } = "";

    [ObservableProperty]
    public partial string MovementAmount { get; set; } = "";

    [ObservableProperty]
    public partial string MovementReason { get; set; } = "";

    [ObservableProperty]
    public partial bool MovementIsWithdrawal { get; set; }

    /// <summary>Knopfblock „Drucken · PDF speichern“ (ADR 0092) für den eben gespeicherten Tagesabschluss; null beim Zwischenbericht. Ohne Senden: der Abschluss wird abgeheftet.</summary>
    [ObservableProperty]
    public partial DocumentActionsViewModel? ClosingDocument { get; private set; }

    /// <summary>Das Recht „Berichte mit Beträgen“: ohne es liest der Server den Abschluss nicht, die Knöpfe sind gesperrt. Die Shell setzt es aus den Rechten.</summary>
    public bool MaySeeAmountReports
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                NotifyOutputChanged();
                OnPropertyChanged(nameof(MayPrintVoucherList));
            }
        }
    }

    public bool HasReport => Report is not null;

    public string ReportTitle => IsClosed ? "Tagesabschluss gespeichert" : "Zwischenbericht seit dem letzten Abschluss";

    public string ReportPeriod => Report is { } r
        ? $"{DayLayout.Local(r.From, Zone):dd.MM.yyyy HH:mm} bis {DayLayout.Local(r.Until, Zone):dd.MM.yyyy HH:mm} · {Count(r.ReceiptCount, "Beleg", "Belege")}, {Count(r.VoidCount, "Storno", "Stornos")}"
        : "";

    private static string Count(int count, string one, string many) => $"{count} {(count == 1 ? one : many)}";

    public string ReportRevenue => Format(Report?.Revenue ?? 0);

    /// <summary>Zahlarten des Berichts; enthaltenes Trinkgeld steht als letzte Zeile, weil es kein Umsatz ist (ADR 0095).</summary>
    public IReadOnlyList<ReceiptRow> ReportPayments => Report is { } r
        ? [.. r.Payments.Select(p => new ReceiptRow(Money.MethodText(p.Method), Format(p.Amount))),
            .. r.Tips != 0 ? new[] { new ReceiptRow("davon Trinkgeld (kein Umsatz)", Format(r.Tips)) } : [],
            .. (r.TipsByEmployee ?? []).Select(t => new ReceiptRow($"   Trinkgeld {t.Name}", Format(t.Amount)))]
        : [];

    public IReadOnlyList<ReceiptRow> ReportTaxes => Report?.Taxes
        .Select(t => new ReceiptRow($"{t.Percent:0.#} % USt von {Format(t.Net)} netto", Format(t.Tax))).ToList() ?? [];

    public IReadOnlyList<ReceiptRow> ReportAccounts => Report?.Accounts
        .Select(a => new ReceiptRow($"{a.Code} {a.Name}{(a.CostCenter is { } cc ? $" · {cc}" : "")}", Format(a.Gross))).ToList() ?? [];

    public IReadOnlyList<ReceiptRow> ReportCash => Report is { } r
        ?
        [
            new("Anfangsbestand", Format(r.OpeningCash)),
            new("Bar eingenommen", Format(r.Payments.Where(p => p.Method == PaymentMethod.Cash).Sum(p => p.Amount))),
            new("Einlagen", Format(r.CashDeposits)),
            new("Entnahmen", Format(-r.CashWithdrawals)),
        ]
        : [];

    public string ExpectedCashText => Format(Report?.ExpectedCash ?? 0);

    /// <summary>Vor dem Abschluss: Vorschau aus der Eingabe. Danach die gespeicherte Differenz des Servers.</summary>
    public string DifferenceText
    {
        get
        {
            if (Report is not { } r)
            {
                return "";
            }

            if (IsClosed)
            {
                return r.Difference == 0 ? "Keine Differenz" : $"Differenz {Signed(r.Difference)}";
            }

            return Money.Parse(CountedCash) is { } counted && CountedCash.Trim().Length > 0
                ? counted == r.ExpectedCash ? "Stimmt mit dem Soll überein" : $"Differenz {Signed(counted - r.ExpectedCash)}"
                : "";
        }
    }

    private string Signed(decimal amount) => amount > 0 ? $"+{Format(amount)}" : $"−{Format(-amount)}";

    [RelayCommand]
    private Task ShowCheckout()
    {
        Mode = PosMode.Checkout;
        return Task.CompletedTask;
    }

    [RelayCommand]
    private Task ShowReceiptsAsync()
    {
        Mode = PosMode.Receipts;
        ReceiptDaysBack = 0;
        return LoadReceiptsAsync();
    }

    [RelayCommand]
    private Task ShowPreviousReceiptDayAsync()
    {
        ReceiptDaysBack++;
        return LoadReceiptsAsync();
    }

    [RelayCommand(CanExecute = nameof(CanShowNextReceiptDay))]
    private Task ShowNextReceiptDayAsync()
    {
        ReceiptDaysBack--;
        return LoadReceiptsAsync();
    }

    private bool CanShowNextReceiptDay() => ReceiptDaysBack > 0;

    [RelayCommand]
    private Task ShowClosingAsync()
    {
        Mode = PosMode.Closing;
        IsClosed = false;
        ClosingDocument = null;
        return LoadReportAsync();
    }

    private void OnRegisterChangedForModes()
    {
        Receipts.Clear();
        Report = null;
        IsClosed = false;
        ClosingDocument = null;
        if (Mode == PosMode.Receipts)
        {
            _ = LoadReceiptsAsync();
        }
        else if (Mode == PosMode.Closing)
        {
            _ = LoadReportAsync();
        }
    }

    /// <summary>Belege des gewählten Tages (Vorgabe heute) dieser Kasse, neueste zuerst.</summary>
    public async Task LoadReceiptsAsync(Guid? select = null)
    {
        if (Register is not { } register)
        {
            return;
        }

        var daysBack = ReceiptDaysBack;
        var result = await _api.GetAsync<List<ReceiptDto>>($"api/pos/registers/{register.Id}/receipts?date={ReceiptDay:yyyy-MM-dd}").ConfigureAwait(true);
        if (register.Id != Register?.Id || daysBack != ReceiptDaysBack)
        {
            return;
        }

        if (result.Value is null)
        {
            Error = result.Error;
            return;
        }

        Receipts.Clear();
        var numbers = result.Value.ToDictionary(r => r.Id, r => r.Number);
        foreach (var r in result.Value.Where(r => r.Kind is ReceiptKind.Sale or ReceiptKind.Void))
        {
            // Welcher Beleg dazugehört, steht nur im Tooltip und nur, wenn der andere Beleg am selben Tag liegt.
            var relation = r.Kind == ReceiptKind.Void && r.VoidsReceiptId is { } voided && numbers.TryGetValue(voided, out var voidedNumber) ? $"Storniert Beleg Nr. {voidedNumber}"
                : r.VoidedByReceiptId is { } voiding && numbers.TryGetValue(voiding, out var voidingNumber) ? $"Storniert mit Stornobeleg Nr. {voidingNumber}"
                : null;
            Receipts.Add(new ReceiptListItem($"Nr. {r.Number}", $"{DayLayout.Local(r.IssuedAt, Zone):HH:mm}", Format(r.Total),
                string.Join(", ", r.Payments.Select(p => Money.MethodText(p.Method)).Distinct()), r.Kind == ReceiptKind.Void, r.VoidedByReceiptId is not null, r, relation));
        }

        SelectedReceiptItem = Receipts.FirstOrDefault(i => i.Receipt.Id == select) ?? Receipts.FirstOrDefault();
    }

    private bool CanVoid() => SelectedReceipt is { CanVoid: true, VoidBlockedByRole: false };

    /// <summary>Der Grund braucht mindestens 3 Zeichen; sonst steht der Fehler am Feld und es bekommt den Fokus.</summary>
    private bool ValidateVoidReason()
    {
        VoidReasonError = VoidReasonInput.Trim().Length >= 3 ? null : "Bitte einen Grund mit mindestens 3 Zeichen eintragen.";
        if (VoidReasonError is not null)
        {
            FocusTarget = VoidReasonField;
        }

        return VoidReasonError is null;
    }

    /// <summary>„Stornieren“ fragt nach (Rückfragedialog, Design-Brief 5.27); die Begründung steht schon im Feld darüber.</summary>
    [RelayCommand(CanExecute = nameof(CanVoid))]
    private async Task AskVoidReceiptAsync()
    {
        if (SelectedReceiptItem is not { } item || !ValidateVoidReason())
        {
            return;
        }

        var answer = await AskAsync(new ConfirmRequest("Beleg stornieren?",
            $"Es entsteht ein Stornobeleg zu Nr. {item.Receipt.Number}. Der Beleg selbst bleibt im Kassenjournal stehen.", "Stornieren")).ConfigureAwait(true);
        if (answer.IsConfirmed)
        {
            await VoidReceiptAsync().ConfigureAwait(true);
        }
    }

    /// <summary>
    /// Storno mit Begründung: es entsteht ein Stornobeleg. Ohne Kassenaufsicht nur eigene Belege der letzten 15 Minuten;
    /// das prüft der Server und nennt den Grund.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanVoid))]
    private async Task VoidReceiptAsync()
    {
        if (SelectedReceiptItem is not { } item || !ValidateVoidReason())
        {
            return;
        }

        Error = null;
        var result = await _api.PostAsync<ReceiptDto>($"api/pos/receipts/{item.Receipt.Id}/void", new VoidRequest(VoidReasonInput.Trim())).ConfigureAwait(true);
        if (result.Value is null)
        {
            Error = result.Error;
            return;
        }

        VoidReasonInput = "";
        await LoadReceiptsAsync(result.Value.Id).ConfigureAwait(true);
        if (_settings.Load().PrintAutomatically)
        {
            await PrintAsync(result.Value.Id).ConfigureAwait(true);
        }
    }

    public async Task LoadReportAsync()
    {
        if (Register is not { } register)
        {
            return;
        }

        var result = await _api.GetAsync<DayClosingDto>($"api/pos/registers/{register.Id}/day-report").ConfigureAwait(true);
        if (register.Id != Register?.Id)
        {
            return;
        }

        Error = result.Value is null ? result.Error : null;
        Report = result.Value;
    }

    /// <summary>Wechselgeld einlegen oder Bargeld entnehmen (kein Umsatz). Entnehmen nur mit Kassenaufsicht.</summary>
    [RelayCommand]
    private async Task AddMovementAsync()
    {
        if (Register is not { } register)
        {
            return;
        }

        MovementAmountError = (Money.Parse(MovementAmount) ?? 0) > 0 ? null : "Bitte einen Betrag über 0 eintragen.";
        MovementReasonError = MovementReason.Trim().Length > 0 ? null : "Bitte einen Grund eintragen.";
        if (MovementAmountError is not null || MovementReasonError is not null)
        {
            FocusTarget = MovementAmountError is not null ? MovementAmountField : MovementReasonField;
            return;
        }

        var kind = MovementIsWithdrawal ? CashMovementKind.Withdrawal : CashMovementKind.Deposit;
        var result = await _api.PostAsync<CashMovementDto>($"api/pos/registers/{register.Id}/cash-movements",
            new CashMovementInput(kind, Money.Parse(MovementAmount)!.Value, MovementReason.Trim())).ConfigureAwait(true);
        if (result.Value is null)
        {
            Error = result.Error;
            return;
        }

        MovementAmount = MovementReason = "";
        await LoadReportAsync().ConfigureAwait(true);
    }

    private bool CanCloseDay() => !IsClosed && Report is not null;

    /// <summary>
    /// Z-Bericht mit gezähltem Bestand; der Server speichert ihn unveränderlich. Vorher lädt die App den Bericht neu (der Server rechnet beim Abschluss neu,
    /// ein Beleg seit dem Öffnen des Reiters ändert die Differenz) und fragt mit der frischen Differenz nach (Design-Brief 5.27).
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanCloseDay))]
    private async Task CloseDayAsync()
    {
        if (Register is not { } register)
        {
            return;
        }

        CountedCashError = CountedCash.Trim().Length > 0 && Money.Parse(CountedCash) is not null ? null : "Bitte den gezählten Bargeldbestand eintragen, z. B. 120,50.";
        if (CountedCashError is not null)
        {
            FocusTarget = CountedCashField;
            return;
        }

        Error = null;
        await LoadReportAsync().ConfigureAwait(true);
        if (Error is not null || Report is not { } fresh)
        {
            return;
        }

        var answer = await AskAsync(new ConfirmRequest("Tagesabschluss durchführen?", ClosingConsequence(fresh), "Abschließen")).ConfigureAwait(true);
        if (!answer.IsConfirmed)
        {
            return;
        }

        var note = ClosingNote.Trim();
        var result = await _api.PostAsync<DayClosingDto>($"api/pos/registers/{register.Id}/day-closings",
            new DayClosingInput(Money.Parse(CountedCash)!.Value, note.Length == 0 ? null : note)).ConfigureAwait(true);
        if (result.Value is null)
        {
            Error = result.Error;
            return;
        }

        Report = result.Value;
        IsClosed = true;
        ClosingDocument = new DocumentActionsViewModel(ClosingDocumentSource(result.Value, MaySeeAmountReports), _api, Dialogs) { ShowToast = ShowToast };
        CountedCash = ClosingNote = "";
        CloseDayCommand.NotifyCanExecuteChanged();
    }

    /// <summary>Pfad des PDFs des gespeicherten Abschlusses; Speichern und Drucken, kein Versand.</summary>
    internal static DocumentSource ClosingDocumentSource(DayClosingDto closing, bool mayReadAmounts) =>
        new("Tagesabschluss", $"api/pos/registers/{closing.RegisterId}/day-closings/{closing.Id}/pdf", IsAllowed: mayReadAmounts,
            DisabledHint: "Dafür braucht es das Recht „Berichte mit Beträgen“.");

    private string ClosingConsequence(DayClosingDto fresh)
    {
        var difference = Money.Parse(CountedCash)!.Value - fresh.ExpectedCash;
        var state = difference == 0 ? "Der gezählte Bestand stimmt mit dem Soll überein." : $"Differenz zum Soll: {Signed(difference)}.";
        return $"Der Abschluss wird unveränderlich gespeichert. {state}";
    }

    partial void OnSelectedReceiptItemChanged(ReceiptListItem? value)
    {
        VoidReasonInput = "";
        VoidReasonError = null;
        VoidReceiptCommand.NotifyCanExecuteChanged();
        AskVoidReceiptCommand.NotifyCanExecuteChanged();
        OnlineRefund = null;
        IsPollingOnlineRefund = false;
        if (value is not null)
        {
            SelectedSubstitute = null;
            _ = LoadOnlineRefundAsync(value.Receipt);
        }
    }

    partial void OnReportChanged(DayClosingDto? value) => CloseDayCommand.NotifyCanExecuteChanged();
}
