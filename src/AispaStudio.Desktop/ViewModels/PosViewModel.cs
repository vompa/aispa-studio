using System.Collections.ObjectModel;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Controls;
using AispaStudio.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>
/// Zeile der Liste „Heute abzurechnen“: Termin oder Eintritt in einen Bereich. Genau eines von <c>Appointment</c> und <c>Visit</c> ist gesetzt.
/// <c>IsDone</c> heißt „wartet auf Abrechnung“ (fertig, vom Server eingeteilt); <c>Details</c> nennt Person und Raum, <c>FinishedText</c> „fertig seit 10:55“.
/// <c>GroupTitle</c> steht über der ersten Zeile einer Gruppe.
/// </summary>
public sealed record UnbilledItem(string Time, string Customer, string Treatment, string Price, bool IsDone, AppointmentDto? Appointment = null, AreaVisitDto? Visit = null,
    string? Details = null, string? FinishedText = null, string? GroupTitle = null)
{
    public bool IsVisit => Visit is not null;

    public bool HasGroupTitle => GroupTitle is not null;

    public bool HasDetails => !string.IsNullOrEmpty(Details);

    public bool HasFinishedText => FinishedText is not null;

    /// <summary>Noch gebucht, nicht eingecheckt: Die Zeile sagt es, und ein Klick fragt „Einchecken und kassieren?“ (L1).</summary>
    public bool NeedsCheckIn => Appointment is { Status: AppointmentStatus.Booked };

    private string? StatusText => NeedsCheckIn ? "noch nicht eingecheckt" : null;

    /// <summary>Zweite Zeile: Behandlung, Person und Raum („Hot-Stone-Massage · Lena · Lotus“), bei gebuchten Terminen mit „noch nicht eingecheckt“.</summary>
    public string SecondLine => string.Join(" · ", new[] { Treatment, Details, StatusText }.Where(t => !string.IsNullOrEmpty(t)));

    /// <summary>Name für den Screenreader wie angezeigt: „10:30 Brigitte Müller, Hot-Stone-Massage, € 95,00, Lena · Lotus, fertig seit 10:55“.</summary>
    public string AccessibleName => AccessibleNames.Join($"{Time} {Customer}", Treatment, Price, Details, StatusText, FinishedText);

    /// <summary>Passt zur Suche: Gast, Behandlung, Person oder Raum enthalten den Text (Groß- und Kleinschreibung egal).</summary>
    public bool Matches(string search) =>
        new[] { Customer, Treatment, Details }.Any(t => t?.Contains(search, StringComparison.CurrentCultureIgnoreCase) == true);
}

/// <summary>Position der offenen Rechnung. <c>Tax</c> nennt Steuersatz und, falls gegeben, den Rabatt.</summary>
public sealed record BillLineItem(string Description, string Quantity, string Amount, string Tax, BillLineDto Line)
{
    /// <summary>Rabatt gibt es auf Termine, Artikel und freie Positionen; Gutscheine werden zum vollen Wert verkauft.</summary>
    public bool CanDiscount => Line.Kind != BillLineKind.Voucher && Line.PaidAmount == 0;
}

/// <summary>Zahlungsart mit Eingabefeld; nur die an dieser Kasse erlaubten erscheinen.</summary>
public sealed partial class PaymentEntry(PaymentMethod method, Action changed) : ObservableObject
{
    public PaymentMethod Method { get; } = method;

    public string Title => Method == PaymentMethod.Cash ? "Bar gegeben" : Money.MethodText(Method);

    public bool IsVoucher => Method == PaymentMethod.Voucher;

    /// <summary>„Über Treatwell bezahlt“ erscheint nur bei Rechnungen mit vorab bezahlten Treatwell-Terminen; alle anderen Zahlungsarten immer.</summary>
    [ObservableProperty]
    public partial bool IsOffered { get; set; } = method != PaymentMethod.Treatwell;

    [ObservableProperty]
    public partial string Amount { get; set; } = "";

    [ObservableProperty]
    public partial string VoucherCode { get; set; } = "";

    /// <summary>Restwert und Gültigkeit des abgefragten Gutscheins; ein geänderter Code räumt sie weg.</summary>
    [ObservableProperty]
    public partial string? VoucherHelp { get; set; }

    /// <summary>Unbekannter oder nicht mehr gültiger Gutschein, als Fehler am Feld.</summary>
    [ObservableProperty]
    public partial string? VoucherError { get; set; }

    public bool IsInstantTransfer => Method == PaymentMethod.InstantTransfer;

    /// <summary>Zahlungsdaten der Sofortüberweisung, wie der Server sie liefert (Ä290); ein neuer Beleg räumt sie weg.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasTransfer), nameof(TransferLines), nameof(TransferPayload))]
    public partial InstantTransferQr? Transfer { get; set; }

    public bool HasTransfer => Transfer is not null;

    /// <summary>QR-Code mit dem Zahlungstext des Servers (Ä297); weg, sobald die Zahlungsdaten wegfallen.</summary>
    [ObservableProperty]
    public partial Avalonia.Media.Imaging.Bitmap? TransferQr { get; private set; }

    partial void OnTransferChanged(InstantTransferQr? value)
    {
        TransferQr?.Dispose();
        TransferQr = value is null ? null : QrCodeImage.Create(value.Payload);
    }

    /// <summary>Der Zahlungstext (EPC) für das Bankprogramm, zum Kopieren.</summary>
    public string TransferPayload => Transfer?.Payload ?? "";

    public IReadOnlyList<string> TransferLines => Transfer is { } transfer
        ? [$"Empfänger: {transfer.PayeeName}", $"IBAN: {transfer.Iban}", $"Betrag: {Money.Format(transfer.Amount, "EUR")}"]
        : [];

    public bool IsCard => Method == PaymentMethod.Card;

    /// <summary>An dieser Kasse ist ein Terminal angebunden; sonst wird die Karte am eigenständigen Gerät kassiert und nur erfasst.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowTerminalButton))]
    public partial bool HasTerminal { get; set; }

    /// <summary>Laufende oder genehmigte Zahlung am angebundenen Terminal.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLocked), nameof(IsWaiting), nameof(IsApproved), nameof(TerminalText), nameof(ShowTerminalButton),
        nameof(CanCancelTerminal), nameof(CancelTerminalText), nameof(TerminalSeverity), nameof(TerminalActionText))]
    public partial CardPaymentDto? CardPayment { get; set; }

    public bool IsWaiting => CardPayment?.Status is CardPaymentStatus.Pending or CardPaymentStatus.ReversalPending;

    public bool IsApproved => CardPayment?.Status == CardPaymentStatus.Approved;

    /// <summary>Betrag nicht mehr änderbar, solange am Terminal etwas läuft oder genehmigt ist.</summary>
    public bool IsLocked => IsWaiting || IsApproved;

    public bool ShowTerminalButton => HasTerminal && IsCard && !IsLocked;

    public bool CanCancelTerminal => CardPayment?.Status is CardPaymentStatus.Pending or CardPaymentStatus.Approved;

    /// <summary>Wartend: abbrechen. Genehmigt, aber noch nicht kassiert: am Terminal stornieren.</summary>
    public string CancelTerminalText => IsApproved ? "Stornieren" : "Abbrechen";

    /// <summary>Ergebnis am Terminal mit Symbol: genehmigt oder gutgeschrieben als Erfolg, abgelehnt oder fehlgeschlagen als Fehler, sonst neutral.</summary>
    public InfoBarSeverity TerminalSeverity => CardPayment?.Status switch
    {
        CardPaymentStatus.Approved or CardPaymentStatus.Refunded => InfoBarSeverity.Success,
        CardPaymentStatus.Declined or CardPaymentStatus.RefundFailed => InfoBarSeverity.Error,
        _ => InfoBarSeverity.Info,
    };

    /// <summary>Aktion der Meldung: „Abbrechen“ oder „Stornieren“, solange das geht.</summary>
    public string? TerminalActionText => CanCancelTerminal ? CancelTerminalText : null;

    public string TerminalText => CardPayment switch
    {
        null => "",
        { Status: CardPaymentStatus.Pending } => "Bitte Karte am Terminal …",
        { Status: CardPaymentStatus.Approved } c => $"Genehmigt{(c.CardBrand is { } brand ? $" · {brand}" : "")}{(c.MaskedPan is { } pan ? $" {pan}" : "")}",
        { Status: CardPaymentStatus.Declined } c => $"Nicht genehmigt{(c.Message is { } m ? $": {m}" : "")}",
        { Status: CardPaymentStatus.Cancelled } => "Abgebrochen",
        { Status: CardPaymentStatus.ReversalPending } => "Storno am Terminal läuft …",
        { Status: CardPaymentStatus.RefundPending } => "Gutschrift am Terminal läuft …",
        { Status: CardPaymentStatus.Refunded } => "Am Terminal gutgeschrieben",
        { Status: CardPaymentStatus.RefundFailed } c => c.Message ?? "Gutschrift am Terminal fehlgeschlagen",
        _ => "Am Terminal storniert",
    };

    partial void OnAmountChanged(string value)
    {
        // Ein geänderter Betrag passt nicht mehr zu den Zahlungsdaten des Servers; sie räumt die Zeile weg.
        if (Transfer is { } transfer && value != Money.Input(transfer.Amount))
        {
            Transfer = null;
        }

        changed();
    }

    partial void OnVoucherCodeChanged(string value)
    {
        VoucherHelp = VoucherError = null;
        changed();
    }
}

/// <summary>
/// Kassa in der Rezeptions-App: heutige Termine und Artikel auf eine Rechnung setzen, mit einer oder mehreren Zahlungsarten bezahlen
/// (z. B. Gutschein und Rest bar) und den signierten Beleg anzeigen. Steuern, Signatur und Beleg rechnet ausschließlich der Server;
/// die App zeigt nur an, was er zurückgibt. Dazu Belege (drucken, stornieren), Einlage/Entnahme und Tagesabschluss;
/// der DEP-Export bleibt im Web.
/// </summary>
public partial class PosViewModel : ViewModelBase
{
    private readonly IWorkstationApi _api;
    private readonly TimeProvider _time;
    private readonly ISettingsStore _settings;
    private readonly Func<string, IReceiptPrinter> _printer;
    private readonly TimeSpan _cardPoll;
    private List<LocationDto> _locations = [];
    private Task? _loadingApprovers;

    /// <summary>Wartende und spätere Zeilen des Tages vor der Suche; <see cref="Unbilled"/> zeigt davon, was zur Suche passt.</summary>
    private List<UnbilledItem> _dayRows = [];

    public PosViewModel(IWorkstationApi api, TimeProvider time, ISettingsStore? settings = null, Func<string, IReceiptPrinter>? printer = null,
        TimeSpan? cardPoll = null)
    {
        _api = api;
        _time = time;
        _settings = settings ?? new MemorySettingsStore();
        IsSalesColumnOpen = _settings.Load().PosSalesPanelOpen;
        _printer = printer ?? (address => new NetworkReceiptPrinter(address));
        _cardPoll = cardPoll ?? TimeSpan.FromSeconds(1.5);
        var ui = SynchronizationContext.Current;
        api.ConnectivityChanged += () =>
        {
            if (ui is null || SynchronizationContext.Current == ui)
            {
                OnPropertyChanged(nameof(ShowWarning));
            }
            else
            {
                ui.Post(_ => OnPropertyChanged(nameof(ShowWarning)), null);
            }
        };
    }

    /// <summary>Angebundene Kartenterminals am Standort der Kasse.</summary>
    public ObservableCollection<PaymentTerminalDto> Terminals { get; } = [];

    /// <summary>Meldung vom Bondrucker, z. B. „Gedruckt“ oder warum nicht.</summary>
    [ObservableProperty]
    public partial string? PrintText { get; private set; }

    private static readonly TimeSpan ToastDuration = TimeSpan.FromSeconds(5);
    private int _toastClock;

    /// <summary>Meldung unten (design.md 5.10): eine Zeile ohne Schlusspunkt, z. B. „Gespeichert: Beleg-K1-142.pdf“; verschwindet nach 5 s.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasToast))]
    public partial string? Toast { get; private set; }

    public bool HasToast => Toast is not null;

    public void ShowToast(string text)
    {
        Toast = text;
        _ = ExpireToastAsync(++_toastClock);
    }

    [RelayCommand]
    private void CloseToast() => Toast = null;

    private async Task ExpireToastAsync(int clock)
    {
        await Task.Delay(ToastDuration, _time).ConfigureAwait(true);
        if (clock == _toastClock)
        {
            Toast = null;
        }
    }

    /// <summary>Panel „Artikel und Gutscheine“ neben der Rechnung aufgeklappt; Standard zugeklappt, der Zustand gilt je Arbeitsplatz und bleibt über den Neustart.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SalesColumnTip))]
    public partial bool IsSalesColumnOpen { get; set; }

    public string SalesColumnTip => IsSalesColumnOpen ? "Artikel und Gutscheine einklappen" : "Artikel und Gutscheine aufklappen";

    partial void OnIsSalesColumnOpenChanged(bool value)
    {
        if (_settings.Load().PosSalesPanelOpen != value)
        {
            _settings.Save(_settings.Load() with { PosSalesPanelOpen = value });
        }
    }

    /// <summary>Hinweis neben „Drucken“, solange unter „Geräte“ kein Bondrucker eingerichtet ist.</summary>
    public const string NoPrinterHint = "Kein Bondrucker eingerichtet (unter „Geräte“).";

    public bool HasPrinter => !string.IsNullOrWhiteSpace(_settings.Load().PrinterAddress);

    /// <summary>Nach Änderungen unter „Geräte“: Drucker neu anzeigen.</summary>
    public void DevicesChanged() => OnPropertyChanged(nameof(HasPrinter));

    public ObservableCollection<CashRegisterDto> Registers { get; } = [];

    /// <summary>„Wartet auf Abrechnung“ und „Später heute“, gefiltert nach der Suche; die Gruppen teilt der Server ein.</summary>
    public ObservableCollection<UnbilledItem> Unbilled { get; } = [];

    /// <summary>„Erledigt“: heute schon bezahlt, nur zum Ansehen.</summary>
    public ObservableCollection<UnbilledItem> Done { get; } = [];

    public string DoneTitle => $"Erledigt ({Done.Count})";

    public bool HasDone => Done.Count > 0;

    /// <summary>„Erledigt“ aufgeklappt; zu Beginn zu.</summary>
    [ObservableProperty]
    public partial bool IsDoneOpen { get; set; }

    /// <summary>Artikel, deren Name zur Suche passt (ab zwei Zeichen); Klick oder Enter legt einen auf die Rechnung.</summary>
    public ObservableCollection<ArticleDto> ArticleHits { get; } = [];

    /// <summary>Weder Zeilen noch Artikel passen zur Suche.</summary>
    public bool ShowNoHits => ScanText.Trim().Length > 0 && Unbilled.Count == 0 && ArticleHits.Count == 0;

    /// <summary>Heute ist nichts mehr abzurechnen (ohne Suche).</summary>
    public bool ShowAllBilled => ScanText.Trim().Length == 0 && Unbilled.Count == 0;

    public ObservableCollection<ArticleDto> Articles { get; } = [];

    /// <summary>Behandlungen für Einzweckgutscheine; den Preis am Standort setzt der Server.</summary>
    public ObservableCollection<TreatmentDto> Treatments { get; } = [];

    /// <summary>Wert für einen Wertgutschein (frei wählbar, nicht steuerbar beim Verkauf).</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SellVoucherCommand))]
    public partial string VoucherValue { get; set; } = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SellTreatmentVoucherCommand))]
    public partial TreatmentDto? VoucherTreatment { get; set; }

    /// <summary>Position, deren Rabatt gerade eingegeben wird.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDiscounting), nameof(DiscountTitle), nameof(DiscountPreview), nameof(NeedsApproval))]
    public partial BillLineItem? DiscountLine { get; private set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ApplyDiscountCommand))]
    [NotifyPropertyChangedFor(nameof(DiscountPreview), nameof(NeedsApproval))]
    public partial string DiscountInput { get; set; } = "";

    /// <summary>Wer über der Rabattgrenze per PIN freigeben kann (Inhaber, Standortleitung am Standort der Kasse).</summary>
    public ObservableCollection<QuickSwitchPerson> Approvers { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ApplyDiscountCommand))]
    public partial QuickSwitchPerson? Approver { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ApplyDiscountCommand))]
    public partial string ApprovalPin { get; set; } = "";

    /// <summary>Der eingegebene Rabatt liegt über der Grenze und die angemeldete Person darf ihn nicht selbst geben.</summary>
    public bool NeedsApproval => Settings is { MayExceedLimit: false } s && DiscountRequestPercent() is { } p && p > s.DiscountLimitPercent;

    /// <summary>Rabatt in Prozent (Standard) oder als Betrag; der jeweils andere Wert wird gleich ausgerechnet.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DiscountInAmount), nameof(DiscountPreview), nameof(DiscountTitle), nameof(NeedsApproval))]
    [NotifyCanExecuteChangedFor(nameof(ApplyDiscountCommand))]
    public partial bool DiscountInPercent { get; set; } = true;

    public bool DiscountInAmount
    {
        get => !DiscountInPercent;
        set => DiscountInPercent = !value;
    }

    public bool IsDiscounting => DiscountLine is not null;

    /// <summary>Rabattgrenze des Spas (Standard 20 %); darüber gibt nur die Kassenaufsicht Rabatt.</summary>
    [ObservableProperty]
    public partial PosSettingsDto? Settings { get; private set; }

    public string DiscountLimitHint => Settings is { MayExceedLimit: false } s
        ? $"Bis {s.DiscountLimitPercent.ToString("0.##", System.Globalization.CultureInfo.GetCultureInfo("de-AT"))} % möglich, darüber gibt die Standortleitung mit ihrer PIN frei."
        : "";

    partial void OnSettingsChanged(PosSettingsDto? value)
    {
        OnPropertyChanged(nameof(DiscountLimitHint));
        OnPropertyChanged(nameof(NeedsApproval));
    }

    private decimal DiscountGross => DiscountLine is { } line ? line.Line.UnitPrice * line.Line.Quantity : 0;

    private decimal? DiscountPercentValue => DiscountInPercent && Money.Parse(DiscountInput.Replace("%", "", StringComparison.Ordinal)) is { } p && p < 100
        ? p
        : null;

    /// <summary>Der andere Wert zur Eingabe: bei Prozent der Betrag, beim Betrag der Prozentsatz.</summary>
    public string DiscountPreview
    {
        get
        {
            var gross = DiscountGross;
            if (gross <= 0)
            {
                return "";
            }

            if (DiscountInPercent)
            {
                return DiscountPercentValue is { } p and > 0
                    ? $"= −{Format(Math.Round(gross * p / 100, 2, MidpointRounding.AwayFromZero))}"
                    : "";
            }

            return Money.Parse(DiscountInput) is { } amount and > 0 && amount < gross
                ? $"= {Math.Round(amount * 100 / gross, 1, MidpointRounding.AwayFromZero).ToString("0.#", System.Globalization.CultureInfo.GetCultureInfo("de-AT"))} %"
                : "";
        }
    }

    /// <summary>Beleg, zu dem gerade ein Rechnungsempfänger erfasst wird (nach dem Bezahlen oder unter „Belege“).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsInvoicing))]
    [NotifyCanExecuteChangedFor(nameof(AddRecipientCommand), nameof(CheckVatIdCommand))]
    public partial ReceiptViewModel? InvoiceTarget { get; private set; }

    public bool IsInvoicing => InvoiceTarget is not null;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddRecipientCommand))]
    public partial string RecipientName { get; set; } = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddRecipientCommand))]
    public partial string RecipientAddress { get; set; } = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CheckVatIdCommand))]
    public partial string RecipientVatId { get; set; } = "";

    /// <summary>Letzte UID-Abfrage über VIES; gültig und zur eingegebenen UID passend wird sie als Nachweis an die Rechnung gehängt.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VatCheckText), nameof(VatCheckOk), nameof(VatCheckFailed))]
    public partial VatIdCheckDto? VatCheck { get; private set; }

    public bool VatCheckOk => VatCheck?.Status == VatIdCheckStatus.Valid;

    public bool VatCheckFailed => VatCheck?.Status is VatIdCheckStatus.Invalid or VatIdCheckStatus.Failed;

    public string VatCheckText => VatCheck switch
    {
        null => "",
        { Status: VatIdCheckStatus.Pending } => "UID wird geprüft …",
        { Status: VatIdCheckStatus.Valid } c => $"UID gültig{(c.Name is { } n ? $": {n}" : "")}{(c.ConsultationNumber is { } nr ? $" · Abfrage {nr}" : "")}",
        { Status: VatIdCheckStatus.Invalid } => "UID ungültig laut VIES.",
        _ => VatCheck.Message ?? "VIES hat nicht geantwortet; später nochmals prüfen.",
    };

    /// <summary>Kundin des Belegs, deren gespeicherter Rechnungsempfänger vorbelegt wurde und den die Kassa nach dem Übernehmen nachführt.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanRememberRecipient), nameof(RememberRecipientText))]
    public partial CustomerDto? InvoiceCustomer { get; private set; }

    public bool CanRememberRecipient => InvoiceCustomer is not null;

    public string RememberRecipientText => InvoiceCustomer is { } c ? $"Bei {c.InHouseName} als Rechnungsempfänger merken" : "";

    [ObservableProperty]
    public partial bool RememberRecipient { get; set; }

    public string DiscountTitle => DiscountLine is { } line ? $"Rabatt auf „{line.Description}“ in {(DiscountInPercent ? "%" : Currency)}" : "";

    public ObservableCollection<BillDto> OpenBills { get; } = [];

    public ObservableCollection<BillLineItem> Lines { get; } = [];

    public ObservableCollection<PaymentEntry> Payments { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Currency))]
    public partial CashRegisterDto? Register { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasBill), nameof(TotalText), nameof(OpenText), nameof(CustomerText), nameof(CashDiscountText), nameof(PlatformPrepaidText))]
    [NotifyPropertyChangedFor(nameof(CustomerLegalText))]
    public partial BillDto? Bill { get; private set; }

    /// <summary>Hinweis bei Terminen, die der Gast vorab über Treatwell bezahlt hat: der Betrag ist als Zahlungsart vorgeschlagen und wird nicht noch einmal verlangt.</summary>
    public string PlatformPrepaidText => Bill is { PlatformPrepaid: > 0 } bill
        ? $"Vorab über Treatwell bezahlt: {Format(Math.Min(bill.PlatformPrepaid, bill.Open))}{(bill.PlatformReference is { } reference ? $" (Buchung {reference})" : "")}. "
            + "Der Betrag wird nicht noch einmal vom Gast verlangt; der Umsatz läuft trotzdem über die Kassa."
        : "";

    /// <summary>Hinweis auf den Barzahlungsrabatt des Spas: so viel günstiger, wenn der Gast alles bar bezahlt.</summary>
    public string CashDiscountText => Bill is { CashDiscount: > 0 } bill
        ? $"Bar {Format(bill.CashDiscount)} günstiger ({bill.CashDiscountPercent.ToString("0.##", System.Globalization.CultureInfo.GetCultureInfo("de-AT"))} % Barzahlungsrabatt)"
        : "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasReceipt))]
    public partial ReceiptViewModel? Receipt { get; private set; }

    [ObservableProperty]
    public partial string? Error { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowWarning))]
    public partial string? Warning { get; private set; }

    /// <summary>
    /// Die Warnung der Kasse (z. B. Signatur) steht nicht, solange das Offline-Banner der Shell schon die Ursache nennt: höchstens zwei Leisten (meldungen.md M2).
    /// Sie kommt mit der Verbindung zurück.
    /// </summary>
    public bool ShowWarning => Warning is not null && !_api.IsOffline;

    /// <summary>Ein Aufruf läuft, der die Kassa warten lässt (Einchecken vor dem Kassieren): die Ansicht zeigt einen Fortschrittsbalken.</summary>
    [ObservableProperty]
    public partial bool IsWorking { get; private set; }

    private const string NeedsConnection = "Kassieren braucht eine Verbindung. Verbindung prüfen und erneut versuchen.";

    [ObservableProperty]
    public partial string CoverText { get; private set; } = "";

    [ObservableProperty]
    public partial string ChangeText { get; private set; } = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PayCommand))]
    public partial bool CanPay { get; private set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PayCommand))]
    public partial bool Busy { get; private set; }

    public bool HasBill => Bill is not null;

    public bool HasReceipt => Receipt is not null;

    /// <summary>Währung dieser Kasse (CHF in der Schweiz, sonst Euro); alle Beträge der Kassa werden damit angezeigt.</summary>
    public string Currency => Money.CurrencyOf(Register?.Country ?? FiscalCountry.AT);

    private string Format(decimal amount) => Money.Format(amount, Currency);

    public string TotalText => Format(Bill?.Total ?? 0);

    public string OpenText => Format(Bill?.Open ?? 0);

    /// <summary>Name im Haus, groß; der gültige Name (für Belege) steht klein darunter, wenn er abweicht.</summary>
    public string CustomerText => Bill?.CustomerName ?? "Laufkundschaft";

    public string? CustomerLegalText => Bill is { CustomerLegalName: { } legal, CustomerName: var name } && legal != name ? legal : null;

    public string DateText => DayLayout.DateText(Today);

    private DateOnly Today => DateOnly.FromDateTime(DayLayout.Local(_time.GetUtcNow(), Zone));

    private TimeZoneInfo Zone => _locations.FirstOrDefault(l => l.Id == Register?.LocationId) is { } location
        ? TimeZoneInfo.FindSystemTimeZoneById(location.TimeZone)
        : _time.LocalTimeZone;

    private Task? _initialization;

    /// <summary>Lädt Kassen und Artikel einmal; weitere Aufrufe warten auf dasselbe Laden.</summary>
    public Task InitializeAsync(Guid? locationId = null) => _initialization ??= LoadAsync(locationId);

    private async Task LoadAsync(Guid? locationId)
    {
        var registers = await _api.GetAsync<List<CashRegisterDto>>("api/pos/registers").ConfigureAwait(true);
        var locations = await _api.GetAsync<List<LocationDto>>("api/locations").ConfigureAwait(true);
        var articles = await _api.GetAsync<List<ArticleDto>>("api/pos/articles").ConfigureAwait(true);
        var treatments = await _api.GetAsync<List<TreatmentDto>>("api/treatments").ConfigureAwait(true);
        Settings = (await _api.GetAsync<PosSettingsDto>("api/pos/settings").ConfigureAwait(true)).Value;
        if (registers.Value is null)
        {
            Error = registers.Error;
            _initialization = null;
            return;
        }

        _locations = locations.Value ?? [];
        foreach (var article in articles.Value ?? [])
        {
            Articles.Add(article);
        }

        foreach (var treatment in (treatments.Value ?? []).OrderBy(t => t.Name, StringComparer.Create(System.Globalization.CultureInfo.GetCultureInfo("de-AT"), true)))
        {
            Treatments.Add(treatment);
        }

        // Kassen einer anderen Stelle (anderer PC oder Server, ADR 0041) kassieren hier nicht.
        foreach (var register in registers.Value.Where(r => r.IsActive && !r.OtherSite))
        {
            Registers.Add(register);
        }

        if (Registers.Count == 0 && registers.Value.Any(r => r.IsActive))
        {
            Error = "Die Kassen dieses Spas kassieren an einer anderen Stelle (anderer PC oder Server). Für diesen PC bitte eine eigene Kasse anlegen.";
            return;
        }

        if (Registers.Count == 0)
        {
            Error = "An diesem Standort ist noch keine Kasse eingerichtet. Kassen legt die Standortleitung unter „Verwaltung“ → „Kassa“ an.";
            return;
        }

        Register = Registers.FirstOrDefault(r => r.LocationId == locationId) ?? Registers[0];
    }

    partial void OnRegisterChanged(CashRegisterDto? value)
    {
        Bill = null;
        Receipt = null;
        Warning = value?.Warnings.Count > 0 ? string.Join(" ", value.Warnings) : null;
        Payments.Clear();
        foreach (var method in value?.PaymentMethods ?? [])
        {
            Payments.Add(new PaymentEntry(method, UpdateCover));
        }

        _ = LoadDayAsync();
        _ = LoadTerminalsAsync(value);
        OnRegisterChangedForModes();
    }

    private async Task LoadTerminalsAsync(CashRegisterDto? register)
    {
        Terminals.Clear();
        if (register is null)
        {
            return;
        }

        var result = await _api.GetAsync<List<PaymentTerminalDto>>($"api/pos/terminals?locationId={register.LocationId}").ConfigureAwait(true);
        if (register.Id != Register?.Id)
        {
            return;
        }

        foreach (var terminal in (result.Value ?? []).Where(t => t.IsActive))
        {
            Terminals.Add(terminal);
        }

        foreach (var entry in Payments)
        {
            entry.HasTerminal = Terminals.Count > 0;
        }
    }

    /// <summary>Betrag ans Terminal schicken (ohne Eingabe: der offene Rest) und warten, bis es genehmigt oder abgelehnt ist.</summary>
    [RelayCommand]
    private async Task StartCardPaymentAsync(PaymentEntry entry)
    {
        if (Bill is not { } bill || Terminals.FirstOrDefault() is not { } terminal)
        {
            return;
        }

        var amount = Money.Parse(entry.Amount) ?? 0;
        if (amount <= 0)
        {
            amount = Math.Max(0, bill.Open - Payments.Where(p => p != entry).Sum(p => Money.Parse(p.Amount) ?? 0));
        }

        if (amount <= 0)
        {
            Error = "Kein offener Betrag für die Karte.";
            return;
        }

        Error = null;
        entry.Amount = Money.Input(amount, Currency);
        var started = await _api.PostAsync<CardPaymentDto>($"api/pos/bills/{bill.Id}/card-payments", new CardPaymentRequest(terminal.Id, amount)).ConfigureAwait(true);
        if (started.Value is null)
        {
            Error = started.Error;
            return;
        }

        entry.CardPayment = started.Value;
        UpdateCover();
        await FollowCardPaymentAsync(entry).ConfigureAwait(true);
    }

    /// <summary>Abbrechen, solange das Terminal wartet; eine genehmigte, nicht kassierte Zahlung wird am Terminal storniert.</summary>
    [RelayCommand]
    private async Task CancelCardPaymentAsync(PaymentEntry entry)
    {
        if (entry.CardPayment is not { } payment)
        {
            return;
        }

        var result = await _api.PostAsync<CardPaymentDto>($"api/pos/card-payments/{payment.Id}/cancel", null).ConfigureAwait(true);
        if (result.Value is null)
        {
            Error = result.Error;
            return;
        }

        entry.CardPayment = result.Value;
        UpdateCover();
        await FollowCardPaymentAsync(entry).ConfigureAwait(true);
    }

    /// <summary>Fragt den Stand ab, bis das Terminal geantwortet hat. Was das Terminal meldet, zählt (ADR 0034).</summary>
    private async Task FollowCardPaymentAsync(PaymentEntry entry)
    {
        while (entry.CardPayment is { } payment && entry.IsWaiting)
        {
            await Task.Delay(_cardPoll).ConfigureAwait(true);
            var current = await _api.GetAsync<CardPaymentDto>($"api/pos/card-payments/{payment.Id}").ConfigureAwait(true);
            if (current.Value is null || entry.CardPayment?.Id != payment.Id)
            {
                return;
            }

            entry.CardPayment = current.Value;
        }

        if (entry.CardPayment?.Status is CardPaymentStatus.Cancelled or CardPaymentStatus.Reversed or CardPaymentStatus.Declined)
        {
            entry.Amount = "";
        }

        UpdateCover();
    }

    /// <summary>Liste „Heute abzurechnen“ (eingeteilt vom Server) und offene Rechnungen dieser Kasse.</summary>
    [RelayCommand]
    public async Task LoadDayAsync()
    {
        if (Register is not { } register)
        {
            return;
        }

        var day = await _api.GetAsync<PosDayListDto>($"api/pos/locations/{register.LocationId}/day-list?date={Today:yyyy-MM-dd}").ConfigureAwait(true);
        var bills = await _api.GetAsync<List<BillDto>>($"api/pos/registers/{register.Id}/bills").ConfigureAwait(true);
        if (register.Id != Register?.Id)
        {
            return;
        }

        if (day.Value is null && day.Error is { } error)
        {
            Error = error;
        }

        var list = day.Value ?? new PosDayListDto([], [], []);
        _dayRows = [.. list.Waiting.Select(e => Row(e, waiting: true)), .. list.Later.Select(e => Row(e, waiting: false))];
        Done.Clear();
        foreach (var entry in list.Done)
        {
            Done.Add(Row(entry, waiting: false));
        }

        OnPropertyChanged(nameof(DoneTitle));
        OnPropertyChanged(nameof(HasDone));
        ApplySearch();

        OpenBills.Clear();
        foreach (var bill in (bills.Value ?? []).Where(b => b.Id != Bill?.Id))
        {
            OpenBills.Add(bill);
        }
    }

    /// <summary>Zeile wie geliefert: Uhrzeit, Gast, Betrag, darunter Behandlung, Person und Raum; Wartende mit „fertig seit …“.</summary>
    private UnbilledItem Row(PosDayEntryDto entry, bool waiting) => new(
        $"{DayLayout.Local(entry.Start, Zone):HH:mm}", entry.CustomerName, entry.What, entry.Price is { } price ? Format(price) : "kein Preis", waiting,
        entry.Appointment, entry.Visit, string.Join(" · ", new[] { entry.EmployeeName, entry.RoomName }.Where(n => !string.IsNullOrEmpty(n))),
        waiting ? entry.FinishedAt is { } finished ? $"fertig seit {DayLayout.Local(finished, Zone):HH:mm}" : "fertig" : null);

    /// <summary>Zeigt die Zeilen und Artikel, die zur Suche passen; die erste Zeile jeder Gruppe trägt ihren Titel.</summary>
    private void ApplySearch()
    {
        var search = ScanText.Trim();
        var rows = search.Length == 0 ? _dayRows : _dayRows.Where(r => r.Matches(search)).ToList();
        Unbilled.Clear();
        UnbilledItem? previous = null;
        foreach (var row in rows)
        {
            Unbilled.Add(previous?.IsDone == row.IsDone ? row : row with { GroupTitle = row.IsDone ? "Wartet auf Abrechnung" : "Später heute" });
            previous = row;
        }

        ArticleHits.Clear();
        if (search.Length >= 2)
        {
            foreach (var article in Articles.Where(a => a.Name.Contains(search, StringComparison.CurrentCultureIgnoreCase)))
            {
                ArticleHits.Add(article);
            }
        }

        OnPropertyChanged(nameof(ShowNoHits));
        OnPropertyChanged(nameof(ShowAllBilled));
    }

    partial void OnScanTextChanged(string value) => ApplySearch();

    /// <summary>Termin auf die Rechnung; ohne offene Rechnung wird eine für die Kundin angelegt.</summary>
    public async Task AddAppointmentAsync(AppointmentDto appointment)
    {
        if (Register is null)
        {
            return;
        }

        if (Register.LocationId != appointment.LocationId && Registers.FirstOrDefault(r => r.LocationId == appointment.LocationId) is { } other)
        {
            Register = other;
        }

        Receipt = null;
        var result = Bill is null
            ? await _api.PostAsync<BillDto>("api/pos/bills", new BillInput(Register.Id, appointment.CustomerId, [appointment.Id])).ConfigureAwait(true)
            : await _api.PostAsync<BillDto>($"api/pos/bills/{Bill.Id}/lines",
                new BillLineInput(BillLineKind.Appointment, appointment.Id, null, 1, null, 0, null, null, null, null, null)).ConfigureAwait(true);
        await ShowBillAsync(result).ConfigureAwait(true);
    }

    /// <summary>Eintritt in einen Bereich auf die Rechnung; die Personenzahl wird zur Menge, den Preis bestimmt der Artikel am Bereich.</summary>
    public async Task AddVisitAsync(AreaVisitDto visit)
    {
        if (Register is null)
        {
            return;
        }

        if (Register.LocationId != visit.LocationId && Registers.FirstOrDefault(r => r.LocationId == visit.LocationId) is { } other)
        {
            Register = other;
        }

        Receipt = null;
        var result = Bill is null
            ? await _api.PostAsync<BillDto>("api/pos/bills", new BillInput(Register.Id, visit.CustomerId, [], [visit.Id])).ConfigureAwait(true)
            : await _api.PostAsync<BillDto>($"api/pos/bills/{Bill.Id}/lines",
                new BillLineInput(BillLineKind.AreaVisit, null, null, 1, null, 0, null, null, null, null, null, visit.Id)).ConfigureAwait(true);
        await ShowBillAsync(result).ConfigureAwait(true);
    }

    /// <summary>Zeile der Tagesliste auf die Rechnung; ein noch nicht eingecheckter Termin wird nach Rückfrage zuerst eingecheckt (L1), sonst lehnt der Server ab.</summary>
    [RelayCommand]
    private async Task AddUnbilledAsync(UnbilledItem item)
    {
        if (item.Visit is { } visit)
        {
            await AddVisitAsync(visit).ConfigureAwait(true);
            return;
        }

        var appointment = item.Appointment!;
        if (item.NeedsCheckIn)
        {
            // Wer nicht einchecken kann, wird nicht erst gefragt: Kassieren braucht die Verbindung zum Server.
            if (_api.IsOffline)
            {
                Error = NeedsConnection;
                return;
            }

            // Die Antwort „Einchecken und kassieren“ hat drei Wörter (Ausnahme zu 5.27): sie nennt genau, was geschieht; „Einchecken“ allein verschwiege die Rechnung.
            var answer = await AskAsync(new ConfirmRequest("Einchecken und kassieren?",
                "Der Termin wird eingecheckt und auf die Rechnung gelegt.", "Einchecken und kassieren", IsDestructive: false)).ConfigureAwait(true);
            if (!answer.IsConfirmed)
            {
                return;
            }

            Error = null;
            IsWorking = true;
            ApiResult<AppointmentDto> checkedIn;
            try
            {
                checkedIn = await _api.PostAsync<AppointmentDto>($"api/appointments/{appointment.Id}/status",
                    new AppointmentStatusChange(AppointmentStatus.CheckedIn, appointment.Version)).ConfigureAwait(true);
            }
            finally
            {
                IsWorking = false;
            }

            if (checkedIn.Value is null)
            {
                Error = checkedIn.Error;
                return;
            }

            appointment = checkedIn.Value;
        }

        await AddAppointmentAsync(appointment).ConfigureAwait(true);
    }

    [RelayCommand]
    private Task AddArticleAsync(ArticleDto article) =>
        AddLineAsync(new BillLineInput(BillLineKind.Article, null, article.Id, 1, null, 0, null, null, null, null, null));

    /// <summary>
    /// Das Suchfeld „Gast, Code oder Artikel“ über der Liste: Tippen filtert die Zeilen; ein Handscanner tippt den Code wie eine Tastatur und schließt mit Enter
    /// ab (Slice 114, Slice 137).
    /// </summary>
    [ObservableProperty]
    public partial string ScanText { get; set; } = "";

    /// <summary>
    /// Enter im Suchfeld: der Text wird als Code über die API gesucht, der Artikel kommt mit Menge 1 auf den Beleg. Ein unbekannter Code ändert den Beleg nicht.
    /// Eine Zeile oder ein Artikeltreffer kommt nur gewählt auf die Rechnung (Pfeil nach unten und Enter, Klick, Tab und Enter), nie, weil der Text auf sie passt.
    /// </summary>
    [RelayCommand]
    private async Task ScanAsync()
    {
        var code = ScanText.Trim();
        ScanText = "";
        if (code.Length == 0 || Register is null)
        {
            return;
        }

        var result = await _api.GetAsync<ArticleDto>($"api/pos/articles/by-barcode?code={Uri.EscapeDataString(code)}").ConfigureAwait(true);
        if (result.Value is { } article)
        {
            Error = null;
            await AddArticleAsync(article).ConfigureAwait(true);
        }
        else if (result.Status == System.Net.HttpStatusCode.NotFound)
        {
            Error = $"Der Code {code} gehört zu keinem Artikel der Kassa. Das Material und sein Verkaufsprodukt legt die Leitung im Lager an.";
        }
        else
        {
            Error = result.Error ?? "Der Code wurde nicht gesucht.";
        }
    }

    /// <summary>Wertgutschein verkaufen: der Server stellt ihn beim Bezahlen aus, der Code steht auf dem Beleg.</summary>
    [RelayCommand(CanExecute = nameof(CanSellVoucher))]
    private async Task SellVoucherAsync()
    {
        var value = Money.Parse(VoucherValue) ?? 0;
        await AddLineAsync(new BillLineInput(BillLineKind.Voucher, null, null, 1, value, 0, null, null, null, VoucherKind.MultiPurpose, null)).ConfigureAwait(true);
        if (Error is null)
        {
            VoucherValue = "";
        }
    }

    private bool CanSellVoucher() => (Money.Parse(VoucherValue) ?? 0) > 0;

    /// <summary>Einzweckgutschein für eine Behandlung zum Preis am Standort der Kasse (wird beim Verkauf versteuert).</summary>
    [RelayCommand(CanExecute = nameof(CanSellTreatmentVoucher))]
    private async Task SellTreatmentVoucherAsync()
    {
        if (VoucherTreatment is not { } treatment)
        {
            return;
        }

        await AddLineAsync(new BillLineInput(BillLineKind.Voucher, null, null, 1, null, 0, null, null, null, VoucherKind.SinglePurpose, treatment.Id)).ConfigureAwait(true);
        if (Error is null)
        {
            VoucherTreatment = null;
        }
    }

    private bool CanSellTreatmentVoucher() => VoucherTreatment is not null;

    /// <summary>Position auf die Rechnung; ohne offene Rechnung wird eine für Laufkundschaft angelegt.</summary>
    private async Task AddLineAsync(BillLineInput line)
    {
        if (Register is null)
        {
            return;
        }

        Receipt = null;
        if (Bill is null)
        {
            var created = await _api.PostAsync<BillDto>("api/pos/bills", new BillInput(Register.Id, null, [])).ConfigureAwait(true);
            if (created.Value is null)
            {
                Error = created.Error;
                return;
            }

            Bill = created.Value;
        }

        var result = await _api.PostAsync<BillDto>($"api/pos/bills/{Bill.Id}/lines", line).ConfigureAwait(true);
        await ShowBillAsync(result).ConfigureAwait(true);
    }

    /// <summary>Rabatt-Eingabe für eine Position öffnen; vorbelegt mit dem bestehenden Rabatt.</summary>
    [RelayCommand]
    private void EditDiscount(BillLineItem line)
    {
        if (!line.CanDiscount)
        {
            return;
        }

        DiscountLine = line;
        DiscountInPercent = true;
        Approver = null;
        ApprovalPin = "";
        // Ein neuer Rabatt darf die Freigebenden erneut laden, falls die letzte Antwort leer war oder fehlschlug.
        _loadingApprovers = null;
        var gross = line.Line.UnitPrice * line.Line.Quantity;
        DiscountInput = line.Line.Discount > 0 && gross > 0
            ? Math.Round(line.Line.Discount * 100 / gross, 2, MidpointRounding.AwayFromZero).ToString("0.##", System.Globalization.CultureInfo.GetCultureInfo("de-AT"))
            : "";
    }

    [RelayCommand]
    private void CancelDiscount() => DiscountLine = null;

    /// <summary>
    /// Rechnung mit Empfänger zu einem Beleg beginnen. Hat die Kundin des Belegs einen gespeicherten Rechnungsempfänger (z. B. ihre Firma),
    /// wird er vorbelegt; ohne Verbindung oder ohne Kundin bleiben die Felder leer.
    /// </summary>
    [RelayCommand]
    private async Task StartInvoiceAsync(ReceiptViewModel receipt)
    {
        if (!receipt.CanAddRecipient)
        {
            return;
        }

        InvoiceTarget = receipt;
        InvoiceCustomer = null;
        RecipientName = "";
        RecipientAddress = "";
        RecipientVatId = "";
        VatCheck = null;
        Error = null;
        if (receipt.Receipt.CustomerId is not { } customerId)
        {
            return;
        }

        var customer = (await _api.GetAsync<CustomerDto>($"api/customers/{customerId}").ConfigureAwait(true)).Value;
        if (customer is null || InvoiceTarget != receipt)
        {
            return;
        }

        InvoiceCustomer = customer;
        RememberRecipient = true;
        if (customer.Billing is { } billing && RecipientName.Length == 0 && RecipientAddress.Length == 0)
        {
            RecipientName = billing.Name;
            RecipientAddress = billing.Address;
            RecipientVatId = billing.VatId ?? "";
        }
        else if (customer.Address is { } address && CustomersViewModel.PostalText(address) is { } postal && RecipientName.Length == 0 && RecipientAddress.Length == 0)
        {
            // Ohne eigenen Rechnungsempfänger: gültiger Name mit Titeln und Postanschrift des Gastes, nie der Rufname.
            RecipientName = PersonNames.AddressLine(customer.Salutation, customer.TitlesBefore, customer.FirstName, customer.LastName, customer.TitlesAfter);
            RecipientAddress = postal;
        }
    }

    [RelayCommand]
    private void CancelInvoice()
    {
        InvoiceTarget = null;
        InvoiceCustomer = null;
    }

    /// <summary>UID über VIES prüfen (läuft am Server über die Queue) und das Ergebnis abfragen, bis es da ist.</summary>
    [RelayCommand(CanExecute = nameof(CanCheckVatId))]
    private async Task CheckVatIdAsync()
    {
        if (InvoiceTarget is not { } target)
        {
            return;
        }

        Error = null;
        var started = await _api.PostAsync<VatIdCheckDto>("api/pos/vat-checks", new VatIdCheckInput(RecipientVatId.Trim(), target.Receipt.RegisterId)).ConfigureAwait(true);
        if (started.Value is null)
        {
            Error = started.Error;
            return;
        }

        VatCheck = started.Value;
        for (var i = 0; i < 30 && VatCheck?.Status == VatIdCheckStatus.Pending && VatCheck.Id == started.Value.Id; i++)
        {
            await Task.Delay(_cardPoll).ConfigureAwait(true);
            var current = await _api.GetAsync<VatIdCheckDto>($"api/pos/vat-checks/{started.Value.Id}").ConfigureAwait(true);
            if (current.Value is null || VatCheck?.Id != started.Value.Id)
            {
                return;
            }

            VatCheck = current.Value;
        }

        if (VatCheck is { Status: VatIdCheckStatus.Valid } valid)
        {
            RecipientVatId = valid.VatId;
            RecipientName = RecipientName.Trim().Length > 0 ? RecipientName : valid.Name ?? "";
            RecipientAddress = RecipientAddress.Trim().Length > 0 ? RecipientAddress : valid.Address ?? "";
        }
    }

    private bool CanCheckVatId() => InvoiceTarget is not null && RecipientVatId.Trim().Length >= 4;

    /// <summary>Empfänger an den Beleg; der Server prüft Pflichtangaben und dass es nur einmal geschieht. Danach gilt der Beleg als Rechnung.</summary>
    [RelayCommand(CanExecute = nameof(CanAddRecipient))]
    private async Task AddRecipientAsync()
    {
        if (InvoiceTarget is not { } target)
        {
            return;
        }

        var vatId = RecipientVatId.Trim();
        var proof = VatCheck is { Status: VatIdCheckStatus.Valid } check && string.Equals(check.VatId, vatId, StringComparison.OrdinalIgnoreCase) ? check.Id : (Guid?)null;
        var result = await _api.PostAsync<ReceiptDto>($"api/pos/receipts/{target.Receipt.Id}/recipient",
            new InvoiceRecipient(RecipientName.Trim(), RecipientAddress.Trim(), vatId.Length == 0 ? null : vatId, proof)).ConfigureAwait(true);
        if (result.Value is null)
        {
            Error = result.Error;
            return;
        }

        Error = null;
        InvoiceTarget = null;
        if (InvoiceCustomer is { } customer && RememberRecipient && result.Value.Recipient is { } recipient
            && customer.Billing != new CustomerBilling(recipient.Name, recipient.Address, recipient.VatId))
        {
            // Die Rechnung steht schon; scheitert nur das Merken, sagt die Kassa das, ohne den Beleg anzufassen.
            var saved = await _api.PutAsync<CustomerDto>($"api/customers/{customer.Id}/billing",
                new CustomerBilling(recipient.Name, recipient.Address, recipient.VatId)).ConfigureAwait(true);
            if (saved.Value is null)
            {
                Error = $"Rechnung erstellt, aber der Empfänger wurde nicht bei der Kundin gespeichert: {saved.Error}";
            }
        }

        InvoiceCustomer = null;
        var updated = ReceiptFor(result.Value, copy: false);
        if (Receipt?.Receipt.Id == result.Value.Id)
        {
            Receipt = updated;
        }

        if (SelectedReceiptItem?.Receipt.Id == result.Value.Id)
        {
            await LoadReceiptsAsync(result.Value.Id).ConfigureAwait(true);
        }
    }

    private bool CanAddRecipient() => InvoiceTarget is not null && RecipientName.Trim().Length > 0 && RecipientAddress.Trim().Length > 0;

    /// <summary>Rabatt in Prozent oder als Betrag an den Server; 0 oder leer nimmt ihn zurück. Steuer und Summe rechnet der Server neu.</summary>
    [RelayCommand(CanExecute = nameof(CanApplyDiscount))]
    private async Task ApplyDiscountAsync()
    {
        if (Bill is null || DiscountLine is not { } line || DiscountRequest() is not { } request)
        {
            return;
        }

        if (NeedsApproval)
        {
            if (Approvers.Count == 0)
            {
                await LoadApproversAsync().ConfigureAwait(true);
            }

            request = request with { Approval = new SupervisorApproval(Approver!.UserId, ApprovalPin.Trim()) };
        }

        var result = await _api.PutAsync<BillDto>($"api/pos/bills/{Bill.Id}/lines/{line.Line.Id}/discount", request).ConfigureAwait(true);
        if (result.Value is null)
        {
            Error = result.Error;
            return;
        }

        DiscountLine = null;
        ApprovalPin = "";
        await ShowBillAsync(result).ConfigureAwait(true);
    }

    private bool CanApplyDiscount() => DiscountRequest() is not null && (!NeedsApproval || (Approver is not null && ApprovalPin.Trim().Length >= 4));

    /// <summary>Freigabe-Personen am Standort der Kasse; geladen, sobald eine Freigabe nötig wird.</summary>
    private async Task LoadApproversAsync()
    {
        var result = await _api.GetAsync<List<QuickSwitchPerson>>($"api/pos/approvers?locationId={Register?.LocationId}").ConfigureAwait(true);
        Approvers.Clear();
        foreach (var person in result.Value ?? [])
        {
            Approvers.Add(person);
        }

        Approver ??= Approvers.FirstOrDefault();
    }

    /// <summary>
    /// Das Betragsfeld meldet jedes getippte Zeichen (Slice 130); die Freigebenden lädt das Tippen deshalb nur einmal, auch wenn die Antwort leer
    /// war. Ein neuer Rabatt (<see cref="EditDiscount"/>) und das Übernehmen (<see cref="ApplyDiscountAsync"/>) laden sie erneut.
    /// </summary>
    partial void OnDiscountInputChanged(string value)
    {
        if (NeedsApproval && Approvers.Count == 0 && _loadingApprovers is null)
        {
            _loadingApprovers = LoadApproversAsync();
        }
    }

    /// <summary>Rabatt der Eingabe in Prozent der Position, egal ob in Prozent oder als Betrag eingegeben.</summary>
    private decimal? DiscountRequestPercent() => DiscountInPercent
        ? DiscountPercentValue
        : DiscountGross > 0 && Money.Parse(DiscountInput) is { } amount ? amount * 100 / DiscountGross : null;

    private LineDiscountInput? DiscountRequest() => DiscountInPercent
        ? DiscountPercentValue is { } percent ? new LineDiscountInput(0, percent) : null
        : Money.Parse(DiscountInput) is { } amount ? new LineDiscountInput(amount) : null;

    [RelayCommand]
    private async Task RemoveLineAsync(BillLineItem line)
    {
        if (Bill is null)
        {
            return;
        }

        var result = await _api.DeleteAsync<BillDto>($"api/pos/bills/{Bill.Id}/lines/{line.Line.Id}").ConfigureAwait(true);
        await ShowBillAsync(result).ConfigureAwait(true);
    }

    [RelayCommand]
    private Task OpenBillAsync(BillDto bill) => OpenBillByIdAsync(bill.Id);

    /// <summary>Offene Rechnung öffnen, wie ein Klick in der Liste; auch für „offene Rechnung …“ aus „KI und Hilfe“ (Ä319).</summary>
    public async Task OpenBillByIdAsync(Guid billId)
    {
        if (CardBlocks())
        {
            return;
        }

        Receipt = null;
        var result = await _api.GetAsync<BillDto>($"api/pos/bills/{billId}").ConfigureAwait(true);
        await ShowBillAsync(result).ConfigureAwait(true);
    }

    /// <summary>„Verwerfen“ fragt nach (Rückfragedialog, Design-Brief 5.27).</summary>
    [RelayCommand]
    private async Task AskDiscardBillAsync()
    {
        if (Bill is null)
        {
            return;
        }

        var answer = await AskAsync(new ConfirmRequest("Rechnung verwerfen?",
            "Die Positionen dieser Rechnung sind danach weg; die Termine stehen wieder unter „Heute abzurechnen“.", "Verwerfen")).ConfigureAwait(true);
        if (answer.IsConfirmed)
        {
            await DiscardBillAsync().ConfigureAwait(true);
        }
    }

    /// <summary>Rechnung verwerfen (nur ohne Zahlung); die Termine stehen danach wieder unter „Heute abzurechnen“.</summary>
    [RelayCommand]
    private async Task DiscardBillAsync()
    {
        if (Bill is null || CardBlocks())
        {
            return;
        }

        var result = await _api.DeleteAsync<object>($"api/pos/bills/{Bill.Id}").ConfigureAwait(true);
        if (!result.Ok)
        {
            Error = result.Error;
            return;
        }

        SetBill(null);
        await LoadDayAsync().ConfigureAwait(true);
    }

    /// <summary>Später weiterkassieren: die Rechnung bleibt offen in der Liste.</summary>
    [RelayCommand]
    private async Task ParkBillAsync()
    {
        if (CardBlocks())
        {
            return;
        }

        SetBill(null);
        await LoadDayAsync().ConfigureAwait(true);
    }

    /// <summary>Füllt den noch offenen Betrag in eine Zahlungsart (Schnellknopf „alles bar“, „alles Karte“).</summary>
    /// <summary>Genehmigtes Geld am Terminal darf nicht ohne Beleg bleiben (ADR 0010, 0034).</summary>
    private bool CardBlocks()
    {
        if (Payments.Any(p => p.IsLocked))
        {
            Error = "Am Terminal läuft eine Zahlung oder sie ist genehmigt: erst bezahlen oder am Terminal stornieren.";
            return true;
        }

        return false;
    }

    /// <summary>Ein leeres, nicht gesperrtes Betragsfeld bekommt beim Fokus den offenen Rest; ein befülltes bleibt, bei Rest 0 bleibt es leer (Ä312); true, wenn ein Betrag eingetragen wurde.</summary>
    public bool PrefillRest(PaymentEntry entry)
    {
        if (entry.IsLocked || !string.IsNullOrWhiteSpace(entry.Amount))
        {
            return false;
        }

        var rest = RestFor(entry);
        if (rest <= 0)
        {
            return false;
        }

        entry.Amount = Money.Input(rest, Currency);
        return true;
    }

    [RelayCommand]
    private void FillRest(PaymentEntry entry)
    {
        if (entry.IsLocked)
        {
            return;
        }

        entry.Amount = Money.Input(RestFor(entry), Currency);
    }

    /// <summary>Der noch offene Betrag für diese Zeile: wie „Noch offen“, bei reiner Barzahlung abzüglich des Barzahlungsrabatts.</summary>
    private decimal RestFor(PaymentEntry entry)
    {
        var others = Payments.Where(p => p != entry).Sum(p => Money.Parse(p.Amount) ?? 0);
        var cashOnly = entry.Method == PaymentMethod.Cash && others == 0;
        return Math.Max(0, (Bill?.Open ?? 0) - (cashOnly ? Bill?.CashDiscount ?? 0 : 0) - others);
    }

    [RelayCommand(CanExecute = nameof(CanPayNow))]
    private async Task PayAsync()
    {
        if (Bill is not { } bill)
        {
            return;
        }

        var payments = Payments
            .Select(p => (Entry: p, Amount: Money.Parse(p.Amount) ?? 0))
            .Where(p => p.Amount > 0)
            .Select(p => new PaymentInput(p.Entry.Method, p.Amount, p.Entry.IsVoucher ? p.Entry.VoucherCode.Trim() : null, null,
                p.Entry.IsApproved ? p.Entry.CardPayment!.Id : null))
            .ToList();
        Busy = true;
        Error = null;
        var result = await _api.PostAsync<ReceiptDto>($"api/pos/bills/{bill.Id}/checkout", new CheckoutRequest(null, payments)).ConfigureAwait(true);
        Busy = false;
        if (result.Value is null)
        {
            Error = result.Error;
            return;
        }

        SetBill(null);
        Receipt = ReceiptFor(result.Value, copy: false);
        await LoadDayAsync().ConfigureAwait(true);
        if (_settings.Load().PrintAutomatically)
        {
            await PrintAsync(result.Value.Id).ConfigureAwait(true);
        }
    }

    [RelayCommand]
    private Task PrintReceiptAsync(ReceiptViewModel? receipt) => receipt is null ? Task.CompletedTask : PrintAsync(receipt.Receipt.Id);

    /// <summary>Beleg als ESC/POS vom Server holen und an den Bondrucker schicken; ohne Drucker passiert nichts.</summary>
    public async Task PrintAsync(Guid receiptId)
    {
        var settings = _settings.Load();
        if (string.IsNullOrWhiteSpace(settings.PrinterAddress))
        {
            PrintText = null;
            return;
        }

        PrintText = "Druckt …";
        var data = await _api.GetBytesAsync($"api/pos/receipts/{receiptId}/print?format=EscPos&width={settings.PrinterWidth}").ConfigureAwait(true);
        if (data.Value is null)
        {
            PrintText = $"Nicht gedruckt: {data.Error}";
            return;
        }

        PrintText = await _printer(settings.PrinterAddress).PrintAsync(data.Value, CancellationToken.None).ConfigureAwait(true) is { } error
            ? $"Nicht gedruckt: {error}"
            : "Beleg gedruckt.";
    }

    [RelayCommand]
    private void CloseReceipt() => Receipt = null;

    /// <summary>Beleg mit Knopfblock „Drucken · PDF speichern · Per Mail senden“ (ADR 0091); der Nachdruck aus der Belegliste trägt „Kopie“.</summary>
    internal ReceiptViewModel ReceiptFor(ReceiptDto receipt, bool copy) => new(receipt, Zone, Register?.Country ?? FiscalCountry.AT, OwnerLevel)
    {
        Document = new DocumentActionsViewModel(ReceiptDocumentSource(receipt, copy), _api, Dialogs) { ShowToast = ShowToast },
    };

    /// <summary>Pfade des Beleg-PDFs: A4 als Standard („A4 drucken“, weil „Drucken“ der Bondrucker ist), Bon-Format im Menü, Versand an den Gast.</summary>
    internal static DocumentSource ReceiptDocumentSource(ReceiptDto receipt, bool copy)
    {
        var copyQuery = copy ? "copy=true" : null;
        return new DocumentSource(ReceiptViewModel.TitleOf(receipt), WithQuery($"api/pos/receipts/{receipt.Id}/pdf", copyQuery), $"api/pos/receipts/{receipt.Id}/send",
            AlternativePdfPath: WithQuery($"api/pos/receipts/{receipt.Id}/pdf", "format=Receipt", copyQuery), PrintLabel: "A4 drucken");

        static string WithQuery(string path, params string?[] query) =>
            string.Join('&', query.Where(q => q is not null)) is { Length: > 0 } joined ? $"{path}?{joined}" : path;
    }

    private bool CanPayNow() => CanPay && !Busy;

    private async Task ShowBillAsync(ApiResult<BillDto> result)
    {
        if (result.Value is null)
        {
            Error = result.Error;
            return;
        }

        Error = null;
        SetBill(result.Value);
        await LoadDayAsync().ConfigureAwait(true);
    }

    private void SetBill(BillDto? bill)
    {
        Bill = bill;
        DiscountLine = null;
        Lines.Clear();
        foreach (var line in bill?.Lines ?? [])
        {
            var quantity = line.Quantity > 1 ? $"{line.Quantity} ×" : "";
            var tax = line.Discount > 0 ? $"{line.TaxPercent:0.#} % USt · {line.DiscountNote ?? "Rabatt"} −{Format(line.Discount)}" : $"{line.TaxPercent:0.#} % USt";
            Lines.Add(new BillLineItem(line.Description, quantity, Format(line.Amount), tax, line));
        }

        foreach (var payment in Payments)
        {
            payment.CardPayment = null;
            payment.Transfer = null;
            payment.Amount = "";
            payment.VoucherCode = "";
            if (payment.Method == PaymentMethod.Treatwell)
            {
                // Vorgeschlagen: der vorab bezahlte Betrag; die Rezeption kann ihn ändern oder leeren.
                payment.IsOffered = bill is { PlatformPrepaid: > 0 };
                if (bill is { PlatformPrepaid: > 0 })
                {
                    payment.Amount = Money.Input(Math.Min(bill.PlatformPrepaid, bill.Open), Currency);
                }
            }
        }

        UpdateCover();
    }

    /// <summary>Wie viel ist gedeckt, wie viel Rückgeld; bezahlt werden kann erst, wenn alles gedeckt ist.</summary>
    private void UpdateCover()
    {
        var open = Bill?.Open ?? 0;
        var amounts = Payments.Select(p => (p.Method, Amount: Money.Parse(p.Amount))).ToList();
        if (amounts.Any(a => a.Amount is null))
        {
            CoverText = "Betrag nicht lesbar";
            ChangeText = "";
            CanPay = false;
            return;
        }

        var cash = amounts.Where(a => a.Method == PaymentMethod.Cash).Sum(a => a.Amount!.Value);
        var other = amounts.Where(a => a.Method != PaymentMethod.Cash).Sum(a => a.Amount!.Value);
        // Nur bar bezahlt: der Barzahlungsrabatt des Spas geht ab (der Server rechnet ihn als eigene Belegzeile).
        if (other == 0 && cash > 0 && Bill is { CashDiscount: > 0 } bill)
        {
            open -= bill.CashDiscount;
        }

        var rest = open - other - cash;
        var change = cash > 0 ? cash - Math.Max(0, open - other) : 0;
        CoverText = rest > 0 ? $"Noch offen: {Format(rest)}" : "Alles gedeckt";
        ChangeText = change > 0 ? $"Rückgeld: {Format(change)}" : "";
        var vouchersComplete = Payments.Where(p => p.IsVoucher && (Money.Parse(p.Amount) ?? 0) > 0).All(p => p.VoucherCode.Trim().Length > 0);
        var cardWaiting = Payments.Any(p => p.IsWaiting);
        CanPay = open > 0 && rest <= 0 && other <= open && vouchersComplete && !cardWaiting;
    }
}

/// <summary>Zeile auf dem Beleg: Text links, Betrag rechts.</summary>
public sealed record ReceiptRow(string Label, string Value);

/// <summary>
/// Signierter Beleg zum Anzeigen nach dem Bezahlen. <paramref name="ownerLevel"/>: Inhaber und Manager; nur sie stornieren einen Anzahlungsbeleg (ADR 0073, Ä51).
/// </summary>
public sealed class ReceiptViewModel(ReceiptDto receipt, TimeZoneInfo zone, FiscalCountry country, bool ownerLevel = false)
{
    public ReceiptDto Receipt { get; } = receipt;

    /// <summary>Knopfblock „Drucken · PDF speichern · Per Mail senden“ (ADR 0091); null in Masken ohne Knopfblock.</summary>
    public DocumentActionsViewModel? Document { get; init; }

    /// <summary>Vermerke „Per Mail gesendet an … am … von …“, damit die Rezeption Doppelsendungen sieht.</summary>
    public IReadOnlyList<string> SentMailLines => Receipt.SentMails is { } sent
        ? [.. sent.Select(m => $"Per Mail gesendet an {m.To} am {DayLayout.Local(m.SentAt, zone):dd.MM.yyyy HH:mm}{(m.SentBy is null ? "" : $" von {m.SentBy}")}")]
        : [];

    public bool HasSentMails => SentMailLines.Count > 0;

    private string Format(decimal amount) => Money.Format(amount, Money.CurrencyOf(country));

    public string Title => TitleOf(Receipt);

    public static string TitleOf(ReceiptDto receipt) => receipt.Kind switch
    {
        ReceiptKind.Void => $"Stornobeleg Nr. {receipt.Number}",
        ReceiptKind.Sale when receipt.Recipient is not null => $"Rechnung Nr. {receipt.RegisterCode}-{receipt.Number}",
        ReceiptKind.Sale => $"Beleg Nr. {receipt.Number}",
        _ => $"Sonderbeleg Nr. {receipt.Number}",
    };

    /// <summary>Rechnungsempfänger (Firmenkunden): einmal je Beleg, danach druckt der Beleg als Rechnung.</summary>
    public bool HasRecipient => Receipt.Recipient is not null;

    public IReadOnlyList<string> RecipientLines => Receipt.Recipient is { } r
        ? [.. new[] { r.Name }.Concat(r.Address.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            .Concat(r.VatId is { } vat ? [$"UID {vat}"] : Array.Empty<string>())]
        : [];

    /// <summary>Empfänger lässt sich nur zu einem nicht stornierten Verkaufsbeleg ergänzen, und nur einmal.</summary>
    public bool CanAddRecipient => Receipt.Kind == ReceiptKind.Sale && !IsVoided && !HasRecipient;

    /// <summary>Über der Kleinbetragsgrenze braucht eine Rechnung an ein Unternehmen den Empfänger (AT 400 €, DE 250 €, CH 400 CHF).</summary>
    public string? InvoiceHint => CanAddRecipient && Receipt.SmallInvoiceLimit is { } limit && Receipt.Total > limit
        ? $"Über {Format(limit)}: Für ein Unternehmen braucht die Rechnung Name und Anschrift des Empfängers."
        : null;

    /// <summary>Unternehmensangaben des Standorts, soweit sie nicht schon im Belegkopf stehen.</summary>
    public IReadOnlyList<string> CompanyLines => Receipt.Company is { } c
        ? [.. new[]
            {
                c.LegalName,
                c.VatId is { } vat ? $"UID {vat}" : null,
                c.CompanyRegisterNumber is { } fn ? string.Join(", ", new[] { fn, c.CompanyRegisterCourt }.Where(x => !string.IsNullOrWhiteSpace(x))) : null,
            }
            .Where(l => !string.IsNullOrWhiteSpace(l) && !Receipt.ReceiptHeader.Contains(l, StringComparison.OrdinalIgnoreCase))
            .Select(l => l!)]
        : [];

    public bool IsVoid => Receipt.Kind == ReceiptKind.Void;

    /// <summary>Dieser Beleg wurde storniert (es gibt einen Stornobeleg dazu).</summary>
    public bool IsVoided => Receipt.VoidedByReceiptId is not null;

    public bool CanVoid => Receipt.Kind == ReceiptKind.Sale && !IsVoided;

    /// <summary>Ein Anzahlungsbeleg lässt sich nur als Inhaber oder Manager stornieren; der Server lehnt es sonst mit 403 ab.</summary>
    public bool VoidBlockedByRole => Receipt.IsDepositReceipt && !ownerLevel;

    public const string DepositVoidBlocked = "Anzahlungsbelege storniert nur die Leitung.";

    public string? VoidReason => Receipt.VoidReason is { } reason ? $"Storno: {reason}" : null;

    public string Subtitle => $"Kasse {Receipt.RegisterCode} · {DayLayout.Local(Receipt.IssuedAt, zone):dd.MM.yyyy HH:mm}";

    public IReadOnlyList<string> Header => Receipt.ReceiptHeader.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

    /// <summary>
    /// Positionen wie auf dem gedruckten Beleg: mit Rabatt (auch einer Aktion) der Preis davor und darunter die Zeile mit dem Namen des Rabatts,
    /// damit die Spalte weiter die Summe ergibt.
    /// </summary>
    public IReadOnlyList<ReceiptRow> Lines => Receipt.Lines
        .SelectMany(l =>
        {
            var row = new ReceiptRow(l.Quantity > 1 ? $"{l.Quantity} × {l.Description}" : l.Description, Format(l.Amount + l.Discount));
            return l.Discount == 0 ? [row] : new[] { row, new ReceiptRow($"   {l.DiscountNote ?? "Rabatt"}", l.Discount > 0 ? $"−{Format(l.Discount)}" : Format(-l.Discount)) };
        })
        .ToList();

    public IReadOnlyList<ReceiptRow> Taxes => Receipt.Taxes
        .Select(t => new ReceiptRow($"{t.Percent:0.#} % USt von {Format(t.Net)}", Format(t.Tax)))
        .ToList();

    public IReadOnlyList<ReceiptRow> Payments => Receipt.Payments
        .Select(p => new ReceiptRow(p.VoucherCode is { } code ? $"Gutschein {code}" : Money.MethodText(p.Method), Format(p.Amount)))
        .ToList();

    /// <summary>Mit diesem Beleg ausgestellte Gutscheine: Code zum Mitgeben an den Gast.</summary>
    public IReadOnlyList<ReceiptRow> Vouchers => Receipt.IssuedVouchers
        .Select(v => new ReceiptRow(v.Kind == VoucherKind.SinglePurpose ? $"Gutschein {v.Code} (Behandlung)" : $"Gutschein {v.Code}", Format(v.InitialValue)))
        .ToList();

    public bool HasVouchers => Receipt.IssuedVouchers.Count > 0;

    public string Total => Format(Receipt.Total);

    public string? Change => Receipt.Change > 0 ? Format(Receipt.Change) : null;

    public bool HasChange => Receipt.Change > 0;

    /// <summary>Ohne funktionierende Signatureinrichtung muss der Beleg den Hinweis tragen (RKSV, KassenSichV).</summary>
    public string SignatureText => Receipt.SignatureDeviceFailed
        ? "Sicherheitseinrichtung ausgefallen"
        : Receipt.FiscalCode is null ? "Ohne Signaturpflicht"
        : country == FiscalCountry.DE ? "Signiert (TSE)" : "Signiert nach RKSV";

    public bool SignatureFailed => Receipt.SignatureDeviceFailed;
}
