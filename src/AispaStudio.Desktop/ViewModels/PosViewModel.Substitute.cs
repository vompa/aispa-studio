using System.Collections.ObjectModel;
using System.Text;
using System.Text.Json;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Controls;
using AispaStudio.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>
/// Zeile der Liste „Ersatzbelege“ unter „Belege“ (ADR 0096): ausgegeben, aber noch nicht nacherfasst. <c>State</c> ist „wartet auf Nacherfassung“
/// oder der Grund, aus dem die Kasse ihn nicht übernommen hat.
/// </summary>
public sealed record SubstituteItem(Guid Id, string Number, string Time, string Total, string State, bool IsRejected, ReceiptDto Receipt)
{
    /// <summary>Name für Screenreader wie angezeigt: Zeit, Nummer, Stand, Betrag.</summary>
    public string AccessibleName => AccessibleNames.Join(Time, Number, State, Total);
}

/// <summary>
/// Kassieren ohne Verbindung (ADR 0096): Die Rechnung entsteht am Gerät, der Gast bekommt einen Ersatzbeleg mit eigener Nummer, und die
/// Kasse erfasst ihn nach, sobald sie wieder erreichbar ist. Nur Termine und Artikel, bar oder mit Karte am eigenständigen Gerät.
/// </summary>
public partial class PosViewModel
{
    public const string SubstituteHint =
        "Keine Verbindung: Die Kassa gibt einen Ersatzbeleg aus. Der signierte Beleg entsteht, sobald die Verbindung wieder da ist.";

    public const string SubstitutePaymentsOnly = "Ohne Verbindung nur bar oder mit Karte am eigenständigen Gerät.";

    /// <summary>Artikel der Positionen einer Rechnung ohne Verbindung: die Nacherfassung braucht Artikel und Menge, die Zeile kennt nur den Text.</summary>
    private readonly Dictionary<Guid, SubstituteArticle> _substituteArticles = [];

    public const string SubstituteWaiting = "wartet auf Nacherfassung";

    /// <summary>Ersatzbelege dieses Geräts, die die Kasse noch nicht nacherfasst hat: vorgemerkte und abgelehnte, die ältesten zuerst.</summary>
    public ObservableCollection<SubstituteItem> Substitutes { get; } = [];

    public bool HasSubstitutes => Substitutes.Count > 0;

    public string SubstitutesTitle => $"Ersatzbelege, noch nicht nacherfasst ({Substitutes.Count})";

    /// <summary>Der rechts gezeigte Ersatzbeleg: ansehen und noch einmal drucken.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedReceipt), nameof(HasShownReceipt), nameof(VoidDisabledHint), nameof(VoidBlockedByRole))]
    public partial SubstituteItem? SelectedSubstitute { get; set; }

    partial void OnSelectedSubstituteChanged(SubstituteItem? value)
    {
        if (value is not null)
        {
            SelectedReceiptItem = null;
        }
    }

    /// <summary>Liste aus der Warteschlange des lokalen Stands neu lesen.</summary>
    private void RefreshSubstitutes()
    {
        if (_api.Offline is not { } offline)
        {
            return;
        }

        var shown = SelectedSubstitute?.Id;
        var rows = offline.Pending.Where(p => p.Substitute is not null).Select(p => Row(p.Id, p.Substitute!, SubstituteWaiting, rejected: false))
            .Concat(offline.Failed.Where(f => f.Substitute is not null).Select(f => Row(f.Id, f.Substitute!, $"nicht übernommen: {f.Reason}", rejected: true)))
            .OrderBy(r => r.Receipt.IssuedAt).ToList();
        Substitutes.Clear();
        foreach (var row in rows)
        {
            Substitutes.Add(row);
        }

        // Ist der gezeigte Ersatzbeleg inzwischen nacherfasst, verschwindet er; der signierte Beleg steht in der Liste darunter.
        SelectedSubstitute = shown is null ? null : Substitutes.FirstOrDefault(s => s.Id == shown);
        OnPropertyChanged(nameof(HasSubstitutes));
        OnPropertyChanged(nameof(SubstitutesTitle));

        SubstituteItem Row(Guid id, ReceiptDto receipt, string state, bool rejected) => new(id, receipt.SubstituteNumber ?? "",
            $"{DayLayout.Local(receipt.IssuedAt, Zone):dd.MM. HH:mm}", Money.Format(receipt.Total, Currency), state, rejected, receipt);
    }

    /// <summary>Abgelehnten Ersatzbeleg erneut zur Nacherfassung senden, z. B. nachdem der Grund an der Kassa geklärt ist.</summary>
    [RelayCommand]
    private async Task RetrySubstituteAsync(SubstituteItem item)
    {
        if (_api.Offline is not { } offline)
        {
            return;
        }

        offline.Retry(item.Id);
        await _api.SyncAsync().ConfigureAwait(true);
        await LoadReceiptsAsync().ConfigureAwait(true);
    }

    /// <summary>Die offene Rechnung gibt es nur an diesem Gerät; bezahlt wird sie mit einem Ersatzbeleg.</summary>
    [ObservableProperty]
    public partial bool IsSubstitute { get; private set; }

    /// <summary>Ersatzbelege gehen nur mit lokalem Stand (Server- und Mehrplatzbetrieb), nicht im Einzelplatz.</summary>
    private bool MaySubstitute => _api.Offline is not null && Register is not null;

    private void AddSubstituteAppointment(AppointmentDto appointment)
    {
        if (appointment.Status is not (AppointmentStatus.CheckedIn or AppointmentStatus.Completed))
        {
            Error = "Der Gast ist noch nicht eingecheckt. Zuerst im Kalender einchecken.";
            return;
        }

        if (Bill?.Lines.Any(l => l.AppointmentId == appointment.Id) == true)
        {
            Error = "Der Termin steht schon auf der Rechnung.";
            return;
        }

        // Die Tagesliste ist ohne Verbindung der letzte Stand: ein schon kassierter Termin stünde weiter darin.
        if (_api.Offline!.Pending.Any(p => p.Substitute?.Lines.Any(l => l.AppointmentId == appointment.Id) == true))
        {
            Error = "Für diesen Termin gibt es schon einen Ersatzbeleg.";
            return;
        }

        var local = DayLayout.Local(appointment.Start, Zone);
        var line = new BillLineDto(Guid.CreateVersion7(), BillLineKind.Appointment, $"{appointment.TreatmentName} {local:dd.MM.yyyy HH:mm}", 1,
            appointment.Price, 0, appointment.Price, 0, TaxCategory.Normal, 0, Guid.Empty, appointment.Id);
        SetSubstituteBill([.. Bill?.Lines ?? [], line], Bill?.CustomerId ?? appointment.CustomerId, Bill?.CustomerName ?? appointment.CustomerName);
    }

    /// <summary>Eintritte der zuletzt geladenen Tagesliste mit Text und Preis: ohne Verbindung kennt das Gerät den Artikel des Bereichs nicht.</summary>
    private Dictionary<Guid, (string What, decimal Price)> _dayVisits = [];

    private void AddSubstituteVisit(AreaVisitDto visit)
    {
        if (visit.IsCancelled)
        {
            Error = "Der Eintritt ist storniert.";
            return;
        }

        if (!_dayVisits.TryGetValue(visit.Id, out var known))
        {
            Error = "Der Preis dieses Eintritts ist ohne Verbindung nicht bekannt. Den Betrag notieren und mit Verbindung kassieren.";
            return;
        }

        if (Bill?.Lines.Any(l => l.AreaVisitId == visit.Id) == true)
        {
            Error = "Der Eintritt steht schon auf der Rechnung.";
            return;
        }

        if (_api.Offline!.Pending.Any(p => p.Substitute is not null && p.Body.Contains(visit.Id.ToString(), StringComparison.OrdinalIgnoreCase)))
        {
            Error = "Für diesen Eintritt gibt es schon einen Ersatzbeleg.";
            return;
        }

        var persons = Math.Max(1, visit.Persons);
        var local = DayLayout.Local(visit.From, Zone);
        var line = new BillLineDto(Guid.CreateVersion7(), BillLineKind.AreaVisit, $"{known.What} {local:dd.MM.yyyy HH:mm}", persons,
            Math.Round(known.Price / persons, 2), 0, known.Price, 0, TaxCategory.Normal, 0, Guid.Empty, null, AreaVisitId: visit.Id);
        SetSubstituteBill([.. Bill?.Lines ?? [], line], Bill?.CustomerId ?? visit.CustomerId, Bill?.CustomerName ?? visit.CustomerName);
    }

    private void AddSubstituteLine(BillLineInput input)
    {
        if (input.Kind != BillLineKind.Article || Articles.FirstOrDefault(a => a.Id == input.ArticleId) is not { } article)
        {
            Error = "Ohne Verbindung nur Termine, Eintritte und Artikel.";
            return;
        }

        var quantity = input.Quantity <= 0 ? 1 : input.Quantity;
        var line = new BillLineDto(Guid.CreateVersion7(), BillLineKind.Article, article.Name, quantity, article.Price, 0, article.Price * quantity, 0,
            article.TaxCategory, 0, Guid.Empty, null);
        _substituteArticles[line.Id] = new SubstituteArticle(article.Id, quantity);
        SetSubstituteBill([.. Bill?.Lines ?? [], line], Bill?.CustomerId, Bill?.CustomerName);
    }

    /// <summary>Die Rechnung ohne Verbindung mit diesen Positionen zeigen; ohne Positionen ist sie weg.</summary>
    private void SetSubstituteBill(IReadOnlyList<BillLineDto> lines, Guid? customerId, string? customerName)
    {
        if (lines.Count == 0 || Register is null)
        {
            SetBill(null);
            return;
        }

        foreach (var removed in _substituteArticles.Keys.Where(id => lines.All(l => l.Id != id)).ToList())
        {
            _substituteArticles.Remove(removed);
        }

        var id = IsSubstitute && Bill is { } current ? current.Id : Guid.CreateVersion7();
        var total = lines.Sum(l => l.Amount);
        Error = null;
        IsSubstitute = true;
        // Kein Barzahlungsrabatt ohne Verbindung: die Nacherfassung bucht, was auf dem Ersatzbeleg steht.
        SetBill(new BillDto(id, Register.Id, customerId, customerName, BillStatus.Open, lines, total, 0, total, []));
    }

    /// <summary>Einchecken ohne Verbindung vormerken und den Termin auf die Rechnung legen; der Abgleich sendet das Einchecken vor dem Ersatzbeleg.</summary>
    private async Task CheckInForSubstituteAsync(AppointmentDto appointment)
    {
        var answer = await AskAsync(new ConfirmRequest("Einchecken und kassieren?",
            "Der Termin wird eingecheckt und auf die Rechnung gelegt. Ohne Verbindung wird das Einchecken vorgemerkt.", "Einchecken und kassieren",
            IsDestructive: false)).ConfigureAwait(true);
        if (!answer.IsConfirmed)
        {
            return;
        }

        var preview = appointment with { Status = AppointmentStatus.CheckedIn };
        var checkedIn = await _api.PostOrQueueAsync($"api/appointments/{appointment.Id}/status",
            new AppointmentStatusChange(AppointmentStatus.CheckedIn, appointment.Version), $"Eingecheckt: {appointment.CustomerName}", preview).ConfigureAwait(true);
        if (checkedIn.Value is null)
        {
            Error = checkedIn.Error;
            return;
        }

        await AddAppointmentAsync(checkedIn.Value).ConfigureAwait(true);
    }

    /// <summary>
    /// Ersatzbeleg ausgeben: Nummer vergeben, als Zweitschrift am Gerät ablegen, zur Nacherfassung vormerken, zeigen und drucken.
    /// </summary>
    private async Task IssueSubstituteAsync(BillDto bill)
    {
        if (_api.Offline is not { } offline || Register is not { } register)
        {
            Error = NeedsConnection;
            return;
        }

        var amounts = Payments.Select(p => (Entry: p, Amount: Money.Parse(p.Amount) ?? 0)).Where(p => p.Amount > 0).ToList();
        if (amounts.Any(p => p.Entry.Method is not (PaymentMethod.Cash or PaymentMethod.Card) || p.Entry.CardPayment is not null))
        {
            Error = SubstitutePaymentsOnly;
            return;
        }

        var tip = Math.Max(0, Tip);
        var card = amounts.Where(p => p.Entry.Method == PaymentMethod.Card).Sum(p => p.Amount);
        var cashGiven = amounts.Where(p => p.Entry.Method == PaymentMethod.Cash).Sum(p => p.Amount);
        var total = bill.Open;
        var lines = bill.Lines.Select(l => new ReceiptLineDto(l.Description, l.Quantity, l.Amount, l.TaxCategory, 0, Guid.Empty, "", l.AppointmentId)).ToList();
        // Schweiz: bar wird auf 5 Rappen gerundet, wie es die Kasse bei der Nacherfassung tut.
        if (register.Country == FiscalCountry.CH && total + tip - card is > 0 and var cashPart
            && (Math.Round(cashPart * 20m, MidpointRounding.AwayFromZero) / 20m) - cashPart is var rounding and not 0)
        {
            lines.Add(new ReceiptLineDto("Rundungsdifferenz (5 Rappen)", 1, rounding, TaxCategory.Zero, 0, Guid.Empty, "", null));
            total += rounding;
        }

        var cashDue = total + tip - card;
        if (cashDue < 0 || cashGiven < cashDue)
        {
            Error = cashDue < 0 ? "Zu viel bezahlt. Rückgeld gibt es nur bar." : $"Es fehlen {Format(cashDue - cashGiven)}.";
            return;
        }

        var now = _time.GetUtcNow();
        var id = Guid.CreateVersion7();
        var number = $"E-{register.Code}-{DayLayout.Local(now, Zone):yyyyMMdd-HHmmss}-{id.ToString("N")[^2..].ToUpperInvariant()}";
        var payments = new List<PaymentInput>();
        if (card > 0)
        {
            payments.Add(new PaymentInput(PaymentMethod.Card, card, null, null));
        }

        if (cashDue > 0)
        {
            payments.Add(new PaymentInput(PaymentMethod.Cash, cashDue, null, null));
        }

        var input = new SubstituteReceiptInput(id, number, now, bill.CustomerId,
            [.. bill.Lines.Where(l => l.AppointmentId is not null).Select(l => l.AppointmentId!.Value)],
            [.. bill.Lines.Where(l => _substituteArticles.ContainsKey(l.Id)).Select(l => _substituteArticles[l.Id])], payments, total, tip,
            [.. bill.Lines.Where(l => l.AreaVisitId is not null).Select(l => l.AreaVisitId!.Value)]);
        var receipt = new ReceiptDto(id, register.Id, register.Code, 0, ReceiptKind.Sale, now, register.ReceiptHeader, null, bill.CustomerId, lines, [],
            [.. payments.Select(p => new PaymentDto(p.Method, p.Amount, null, null))], total, cashGiven - cashDue, null, null, null, null, false, [],
            Tip: tip, SubstituteNumber: number, SubstituteIssuedAt: now);
        offline.Enqueue(new PendingChange(id, $"api/pos/registers/{register.Id}/substitute-receipts", JsonSerializer.Serialize(input, ApiClient.Json),
            $"Ersatzbeleg {number}: {Format(total)}", now, null, Substitute: receipt));
        offline.Flush();

        Error = null;
        SetBill(null);
        Receipt = new ReceiptViewModel(receipt, Zone, register.Country, OwnerLevel);
        if (_settings.Load().PrintAutomatically)
        {
            await PrintSubstituteAsync(receipt).ConfigureAwait(true);
        }
    }

    /// <summary>Ersatzbeleg am Bondrucker drucken; der Text entsteht am Gerät, weil der Server nicht erreichbar ist.</summary>
    public async Task PrintSubstituteAsync(ReceiptDto receipt)
    {
        var settings = _settings.Load();
        if (string.IsNullOrWhiteSpace(settings.PrinterAddress))
        {
            PrintText = null;
            return;
        }

        PrintText = "Druckt …";
        var bytes = new List<byte> { 0x1B, 0x40, 0x1B, 0x74, 16 };
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        bytes.AddRange(Encoding.GetEncoding(1252).GetBytes(SubstituteText(receipt, Zone, Currency, settings.PrinterWidth)));
        bytes.AddRange([0x1B, 0x64, 4, 0x1D, 0x56, 0x42, 0]);
        PrintText = await _printer(settings.PrinterAddress).PrintAsync([.. bytes], CancellationToken.None).ConfigureAwait(true) is { } error
            ? $"Nicht gedruckt: {error}"
            : "Ersatzbeleg gedruckt.";
    }

    /// <summary>Text des Ersatzbelegs für den Bondrucker: Kopf, Nummer, Zeit, Positionen, Summe, Zahlung und der Hinweis auf den signierten Beleg.</summary>
    public static string SubstituteText(ReceiptDto receipt, TimeZoneInfo zone, string currency, int width)
    {
        width = Math.Clamp(width, 24, 64);
        var text = new StringBuilder();
        void Line(string value = "") => text.Append(value.Length > width ? value[..width] : value).Append('\n');
        void Columns(string left, string right)
        {
            var room = Math.Max(1, width - right.Length - 1);
            Line((left.Length > room ? left[..room] : left).PadRight(room) + " " + right);
        }

        foreach (var part in receipt.ReceiptHeader.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            Line(part);
        }

        Line();
        Line("ERSATZBELEG");
        Line(receipt.SubstituteNumber ?? "");
        Line($"{DayLayout.Local(receipt.IssuedAt, zone):dd.MM.yyyy HH:mm:ss}  Kassa {receipt.RegisterCode}");
        Line(new string('-', width));
        foreach (var l in receipt.Lines)
        {
            Columns(l.Quantity > 1 ? $"{l.Quantity} x {l.Description}" : l.Description, Money.Format(l.Amount, currency));
        }

        Line(new string('-', width));
        Columns("Summe inkl. USt", Money.Format(receipt.Total, currency));
        if (receipt.Tip != 0)
        {
            Columns("Trinkgeld (kein Umsatz)", Money.Format(receipt.Tip, currency));
        }

        foreach (var p in receipt.Payments)
        {
            Columns(Money.MethodText(p.Method), Money.Format(p.Amount, currency));
        }

        if (receipt.Change > 0)
        {
            Columns("Rückgeld", Money.Format(receipt.Change, currency));
        }

        Line();
        Line("Die Kassa war nicht erreichbar.");
        Line("Der signierte Beleg wird nacherfasst");
        Line("und nennt diese Nummer.");
        return text.ToString();
    }
}
