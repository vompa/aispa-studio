using System.Collections.ObjectModel;
using System.Text;
using AispaStudio.Contracts;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>Eine Zeile des Bestellvorschlags mit dem Rechenweg der API.</summary>
public sealed record ReorderRow(string Material, string SupplierText, string OnHandText, string UseText, string TargetText, string SuggestedText, string CostText)
{
    public string Summary => $"{Material}, {SupplierText}, Bestand {OnHandText}, {UseText}, Ziel {TargetText}, Vorschlag {SuggestedText}, {CostText}";

    public override string ToString() => Summary;
}

/// <summary>Angefangene Eingaben zu einer Lieferung; sie überstehen das Neuladen des Lagers.</summary>
internal sealed record KeptDelivery(string? Quantity, string Outstanding, string Batch, DateOnly? ExpiresOn);

/// <summary>Eine Position einer Bestellung; im Entwurf lässt sich die Menge ändern, 0 nimmt sie heraus. Nach dem Bestellen nimmt sie die Lieferung auf, vorbelegt mit dem, was noch aussteht.</summary>
public sealed partial class PurchaseOrderLineRow : ObservableObject
{
    private readonly Action? _changed;
    private readonly string _outstanding;
    private string _saved;

    internal PurchaseOrderLineRow(PurchaseOrderLineDto line, string quantity, Action changed, bool isReceiving, KeptDelivery? kept)
    {
        Line = line;
        IsReceiving = isReceiving;
        _saved = StockFormat.Plain(line.Quantity);
        _outstanding = StockFormat.Plain(Math.Max(0, line.Quantity - line.Received));
        Quantity = quantity;
        // Hat inzwischen jemand anderes gebucht, gilt der neue Rest; eine alte Menge könnte sonst doppelt gebucht werden.
        DeliveryQuantity = kept is { Quantity: { } quantityKept } && kept.Outstanding == _outstanding ? quantityKept : _outstanding;
        Batch = kept?.Batch ?? "";
        ExpiresOn = kept?.ExpiresOn;
        _changed = changed;
    }

    public bool IsReceiving { get; }

    public string ReceivedText => $"{StockFormat.Quantity(Line.Received)} {Line.Unit} geliefert";

    public string DeliveryQuantityName => $"Geliefert {Line.ItemName} ({Line.Unit})";

    public string BatchName => $"Charge {Line.ItemName}";

    public string ExpiresName => $"Ablaufdatum {Line.ItemName}";

    [ObservableProperty]
    public partial string DeliveryQuantity { get; set; }

    [ObservableProperty]
    public partial string? DeliveryError { get; set; }

    [ObservableProperty]
    public partial string Batch { get; set; }

    [ObservableProperty]
    public partial DateOnly? ExpiresOn { get; set; }

    internal bool HasDeliveryInput => DeliveryQuantity != _outstanding || Batch.Length > 0 || ExpiresOn is not null;

    internal KeptDelivery KeptInput => new(DeliveryQuantity != _outstanding ? DeliveryQuantity : null, _outstanding, Batch, ExpiresOn);

    /// <summary>Das Datumsfeld zeigt keinen Fehler; ein unlesbares Datum käme sonst als „kein Datum“ an. Setzt die Ansicht.</summary>
    public bool IsExpiresValid { get; set; } = true;

    internal PurchaseOrderLineDto Line { get; }

    public string Material => Line.SupplierArticleNumber is { } number ? $"{Line.ItemName} ({number})" : Line.ItemName;

    public string Unit => Line.Unit;

    public string PackageText => Line.PackageText ?? "";

    public string OrderedText => $"{StockFormat.Quantity(Line.Quantity)} {Line.Unit}";

    public string QuantityName => $"Menge {Line.ItemName} ({Line.Unit})";

    internal bool IsDirty => Quantity != _saved;

    [ObservableProperty]
    public partial string? QuantityError { get; set; }

    internal void Reset()
    {
        Quantity = _saved;
        QuantityError = null;
    }

    internal void MarkSaved() => _saved = Quantity;

    [ObservableProperty]
    public partial string Quantity { get; set; }

    // Im Konstruktor steht _changed noch nicht: die erste Zuweisung ist keine Änderung.
    partial void OnQuantityChanged(string value) => _changed?.Invoke();
}

/// <summary>Eine Bestellung in der Liste; was der Mindestbestellwert verlangt, rechnet die API.</summary>
public sealed partial class PurchaseOrderCard : ObservableObject
{
    private readonly StockOrdersViewModel _owner;

    internal PurchaseOrderCard(PurchaseOrderDto order, IReadOnlyDictionary<Guid, string> keptQuantities, IReadOnlyDictionary<Guid, KeptDelivery> keptDeliveries, string keptNote, StockOrdersViewModel owner)
    {
        Order = order;
        _owner = owner;
        IsReceiving = order.Status is PurchaseOrderStatus.Ordered or PurchaseOrderStatus.PartiallyReceived;
        DeliveryNote = keptNote;
        Lines = [.. order.Lines.Select(l => new PurchaseOrderLineRow(l, keptQuantities.GetValueOrDefault(l.Id) ?? StockFormat.Plain(l.Quantity), Touch, IsReceiving, keptDeliveries.GetValueOrDefault(l.Id)))];
        SendCommand = new AsyncRelayCommand(() => _owner.SendAsync(this), () => CanSend);
        SaveCommand = new AsyncRelayCommand(() => _owner.SaveAsync(this), () => IsDirty);
        DiscardCommand = new AsyncRelayCommand(() => _owner.DiscardDraftAsync(this));
        MarkOrderedCommand = new AsyncRelayCommand(() => _owner.MarkOrderedAsync(this));
        ShowTextCommand = new AsyncRelayCommand(() => _owner.ShowTextAsync(this));
        DeliverCommand = new AsyncRelayCommand(() => _owner.DeliverAsync(this));
        CloseCommand = new AsyncRelayCommand(() => _owner.CloseAsync(this));
    }

    internal PurchaseOrderDto Order { get; }

    public IReadOnlyList<PurchaseOrderLineRow> Lines { get; }

    public AsyncRelayCommand SendCommand { get; }

    public AsyncRelayCommand SaveCommand { get; }

    public AsyncRelayCommand DiscardCommand { get; }

    public AsyncRelayCommand MarkOrderedCommand { get; }

    public AsyncRelayCommand ShowTextCommand { get; }

    public AsyncRelayCommand DeliverCommand { get; }

    public AsyncRelayCommand CloseCommand { get; }

    /// <summary>Bestellt oder teilweise geliefert: jetzt lässt sich eine Lieferung buchen.</summary>
    public bool IsReceiving { get; }

    public bool IsPartial => Order.Status == PurchaseOrderStatus.PartiallyReceived;

    [ObservableProperty]
    public partial string DeliveryNote { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasOrderText))]
    public partial string? OrderText { get; set; }

    public bool HasOrderText => OrderText is not null;

    [ObservableProperty]
    public partial string? CopyHint { get; set; }

    public string CloseText => IsPartial ? "Abschließen" : "Stornieren";

    public string CloseName => $"Bestellung {Order.Number} {(IsPartial ? "abschließen" : "stornieren")}";

    public string MarkOrderedName => $"Bestellung {Order.Number} als bestellt markieren";

    public string ShowTextName => $"Bestelltext der Bestellung {Order.Number}";

    public string OrderTextName => $"Bestelltext der Bestellung {Order.Number} zum Kopieren";

    public string CopyTextName => $"Bestelltext der Bestellung {Order.Number} kopieren";

    public string DeliverName => $"Lieferung zu Bestellung {Order.Number} buchen";

    public string DeliveryNoteName => $"Lieferschein der Bestellung {Order.Number}";

    public string Title => $"Bestellung {Order.Number}, {Order.Supplier}";

    public string StatusText => StockFormat.OrderStatus(Order.Status);

    public string TotalText => Money.Format(Order.Total);

    public bool IsDraft => Order.Status == PurchaseOrderStatus.Draft;

    public bool IsNotDraft => !IsDraft;

    public bool HasMail => Order.SupplierEmail is not null;

    public bool CanSend => IsDraft && HasMail;

    public bool ShowMissingMail => IsDraft && !HasMail;

    public string MailText => $"Geht per Mail an {Order.SupplierEmail}.";

    public bool IsBelowMinimum => IsDraft && Order.MissingToMinimum > 0;

    public string MinimumWarning => WarningOf(Order);

    public string SendName => $"Bestellung {Order.Number} bestellen und senden";

    public string SaveName => $"Mengen der Bestellung {Order.Number} speichern";

    public string DiscardName => $"Entwurf verwerfen, Bestellung {Order.Number}";

    internal bool IsDirty => IsDraft && Lines.Any(l => l.IsDirty);

    internal static string WarningOf(PurchaseOrderDto order) =>
        $"Unter dem Mindestbestellwert von {Money.Format(order.MinimumOrderValue ?? 0)}: es fehlen {Money.Format(order.MissingToMinimum)}.";

    private void Touch()
    {
        OrderText = null; // Der Text nennt die alte Menge und darf so nicht in eine Mail.
        SaveCommand.NotifyCanExecuteChanged();
        _owner.NotifyChanged();
    }
}

/// <summary>
/// Bestellungen des Lagers (R05 Slice 113, Ä251; Slice 123, Ä223): oben der Bestellvorschlag mit Rechenweg, darunter die Bestellungen. Entwürfe lassen sich ändern, verwerfen und nach der Bestätigung per Mail an den Lieferanten schicken;
/// nichts geht ohne diese Bestätigung hinaus. Wer selbst bestellt, nimmt den Bestelltext und markiert die Bestellung als bestellt. Danach bucht die Maske Lieferungen, auch in Teilen. Nur mit ManageLocation; gerechnet und gesendet wird in der API.
/// </summary>
public sealed partial class StockOrdersViewModel : ViewModelBase, IEditableArea
{
    private readonly StockViewModel _owner;

    internal StockOrdersViewModel(StockViewModel owner) => _owner = owner;

    public string Title => "Bestellungen";

    public ObservableCollection<ReorderRow> Suggestions { get; } = [];

    public ObservableCollection<PurchaseOrderCard> Orders { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLoadError))]
    public partial string? LoadError { get; private set; }

    public bool HasLoadError => LoadError is not null;

    public bool HasSuggestions => Suggestions.Count > 0;

    public bool ShowNoSuggestions => !HasLoadError && !HasSuggestions;

    public bool HasOrders => Orders.Count > 0;

    public bool ShowNoOrders => !HasLoadError && !HasOrders;

    public bool HasUnsavedChanges => Orders.Any(o => o.IsDirty);

    internal void Apply(List<PurchaseOrderDto>? orders, List<ReorderSuggestionDto>? suggestions)
    {
        LoadError = orders is null || suggestions is null ? "Die Bestellungen wurden nicht geladen. Lade das Lager neu." : null;
        Suggestions.Clear();
        foreach (var line in suggestions ?? [])
        {
            Suggestions.Add(new ReorderRow(line.ItemName, line.Supplier ?? "Ohne Lieferant", $"{StockFormat.Quantity(line.OnHand)} {line.Unit}", Use(line), $"{StockFormat.Quantity(line.Target)} {line.Unit}",
                line.PackageText ?? $"{StockFormat.Quantity(line.Suggested)} {line.Unit}", Money.Format(line.Cost)));
        }

        // Ein Ladefehler lässt den alten Stand stehen, sonst wären angefangene Eingaben ohne Rückfrage weg.
        if (orders is null)
        {
            OnPropertyChanged(nameof(HasSuggestions));
            OnPropertyChanged(nameof(ShowNoSuggestions));
            return;
        }

        // Eine angefangene Eingabe bleibt stehen, wenn das Lager neu lädt.
        var lines = Orders.SelectMany(o => o.Lines).ToList();
        var kept = lines.Where(l => l.IsDirty).ToDictionary(l => l.Line.Id, l => l.Quantity);
        var keptDeliveries = lines.Where(l => l.HasDeliveryInput).ToDictionary(l => l.Line.Id, l => l.KeptInput);
        var keptNotes = Orders.ToDictionary(o => o.Order.Id, o => o.DeliveryNote);
        var keptTexts = Orders.ToDictionary(o => o.Order.Id, o => o.OrderText);
        Orders.Clear();
        foreach (var order in orders)
        {
            Orders.Add(new PurchaseOrderCard(order, kept, keptDeliveries, keptNotes.GetValueOrDefault(order.Id) ?? "", this) { OrderText = keptTexts.GetValueOrDefault(order.Id) });
        }

        NotifyListChanged();
    }

    /// <summary>Beim Standortwechsel: Bestellungen des alten Standorts dürfen nicht buchbar stehen bleiben, auch wenn das Laden scheitert.</summary>
    internal void Reset()
    {
        Orders.Clear();
        NotifyListChanged();
    }

    private void NotifyListChanged()
    {
        foreach (var name in new[] { nameof(HasSuggestions), nameof(ShowNoSuggestions), nameof(HasOrders), nameof(ShowNoOrders), nameof(HasUnsavedChanges) })
        {
            OnPropertyChanged(name);
        }
    }

    private static string Use(ReorderSuggestionDto line)
    {
        var ordered = line.OnOrder > 0 ? $", bestellt {StockFormat.Quantity(line.OnOrder)} {line.Unit}" : "";
        var booked = $"Termine {StockFormat.Quantity(line.BookedUse)} {line.Unit}{ordered}";
        return line.LeadTimeDays is { } days
            ? $"{StockFormat.Quantity(line.DailyUse)} {line.Unit} am Tag × {days} {(days == 1 ? "Tag" : "Tage")}, {booked}"
            : $"Keine Lieferzeit, {booked}";
    }

    [RelayCommand]
    private async Task CreateDraftsAsync()
    {
        if (_owner.Location is not { } location)
        {
            return;
        }

        var result = await _owner.Api.PostAsync<List<PurchaseOrderDto>>($"api/stock/locations/{location.Id}/orders/from-suggestions", null).ConfigureAwait(true);
        if (result.Value is not { } created)
        {
            _owner.ShowError(result.Error ?? "Die Entwürfe wurden nicht angelegt.");
            return;
        }

        await _owner.ReloadAsync(created.Count switch
        {
            0 => "Es ist nichts zu bestellen, oder dem Material fehlt der Lieferant.",
            1 => "1 Entwurf angelegt.",
            _ => $"{created.Count} Entwürfe angelegt.",
        }).ConfigureAwait(true);
    }

    internal async Task SendAsync(PurchaseOrderCard card)
    {
        // Geänderte Mengen zuerst speichern: den fehlenden Betrag zum neuen Stand rechnet die API.
        var saved = card.IsDirty;
        var order = saved ? await SaveQuantitiesAsync(card).ConfigureAwait(true) : card.Order;
        if (order is null)
        {
            return;
        }

        var warning = order.MissingToMinimum > 0 ? $"{PurchaseOrderCard.WarningOf(order)} " : "";
        var answer = await _owner.Ask(new ConfirmRequest($"Bestellung {card.Order.Number} senden?",
            $"{warning}Die Bestellung geht per Mail an {card.Order.SupplierEmail} und steht danach auf „bestellt“.", "Bestellen und senden", IsDestructive: false)).ConfigureAwait(true);
        if (!answer.IsConfirmed)
        {
            if (saved)
            {
                await _owner.ReloadAsync($"Bestellung {card.Order.Number} gespeichert, nicht gesendet.").ConfigureAwait(true);
            }

            return;
        }

        var result = await _owner.Api.PostAsync<PurchaseOrderDto>($"api/stock/orders/{card.Order.Id}/send", null).ConfigureAwait(true);
        if (result.Value is null)
        {
            // Die Mengen sind vielleicht schon gespeichert: Stand der API zeigen, dann den Fehler.
            await _owner.ReloadAsync().ConfigureAwait(true);
            _owner.ShowError(result.Error ?? "Die Bestellung wurde nicht gesendet.");
            return;
        }

        await _owner.ReloadAsync($"Bestellung {card.Order.Number} ist bestellt, die Mail an {card.Order.SupplierEmail} ist unterwegs.").ConfigureAwait(true);
    }

    internal async Task SaveAsync(PurchaseOrderCard card)
    {
        if (await SaveQuantitiesAsync(card).ConfigureAwait(true) is not null)
        {
            await _owner.ReloadAsync($"Bestellung {card.Order.Number} gespeichert.").ConfigureAwait(true);
        }
    }

    private async Task<PurchaseOrderDto?> SaveQuantitiesAsync(PurchaseOrderCard card)
    {
        var lines = new List<PurchaseOrderLineInput>();
        var valid = true;
        foreach (var row in card.Lines)
        {
            var parsed = StockFormat.Parse(row.Quantity);
            row.QuantityError = parsed is null or < 0 ? "Gib eine Zahl ab 0 ein, z. B. 500." : null;
            valid &= row.QuantityError is null;
            if (parsed is { } amount && amount > 0)
            {
                lines.Add(new PurchaseOrderLineInput(row.Line.ItemId, amount, row.Line.UnitCost));
            }
        }

        if (!valid)
        {
            return null;
        }

        var result = await _owner.Api.PutAsync<PurchaseOrderDto>($"api/stock/orders/{card.Order.Id}", new PurchaseOrderInput(card.Order.Supplier, lines, card.Order.Note)).ConfigureAwait(true);
        if (result.Value is null)
        {
            _owner.ShowError(result.Error ?? "Die Bestellung wurde nicht gespeichert.");
            return null;
        }

        foreach (var row in card.Lines)
        {
            row.MarkSaved();
        }

        NotifyChanged();
        return result.Value;
    }

    internal async Task DiscardDraftAsync(PurchaseOrderCard card)
    {
        var answer = await _owner.Ask(new ConfirmRequest($"Entwurf {card.Order.Number} verwerfen?", "Der Entwurf wird storniert. An den Lieferanten geht nichts.", "Verwerfen")).ConfigureAwait(true);
        if (!answer.IsConfirmed)
        {
            return;
        }

        var result = await _owner.Api.PostAsync<PurchaseOrderDto>($"api/stock/orders/{card.Order.Id}/close", null).ConfigureAwait(true);
        if (result.Value is null)
        {
            _owner.ShowError(result.Error ?? "Der Entwurf wurde nicht verworfen.");
            return;
        }

        foreach (var row in card.Lines)
        {
            row.Reset();
        }

        await _owner.ReloadAsync($"Entwurf {card.Order.Number} verworfen.").ConfigureAwait(true);
    }

    /// <summary>Für alle, die selbst beim Lieferanten bestellen: es geht keine Mail hinaus. Geänderte Mengen zuerst speichern, sonst gälten die alten.</summary>
    internal async Task MarkOrderedAsync(PurchaseOrderCard card)
    {
        if (card.IsDirty && await SaveQuantitiesAsync(card).ConfigureAwait(true) is null)
        {
            return;
        }

        var result = await _owner.Api.PostAsync<PurchaseOrderDto>($"api/stock/orders/{card.Order.Id}/ordered", null).ConfigureAwait(true);
        if (result.Value is null)
        {
            await _owner.ReloadAsync().ConfigureAwait(true);
            _owner.ShowError(result.Error ?? "Die Bestellung wurde nicht als bestellt markiert.");
            return;
        }

        await _owner.ReloadAsync($"Bestellung {card.Order.Number} als bestellt markiert.").ConfigureAwait(true);
    }

    /// <summary>Holt den Bestelltext der API zum Kopieren. Geänderte Mengen zuerst speichern; das Neuladen baut die Karte neu, der Text gehört an die neue.</summary>
    internal async Task ShowTextAsync(PurchaseOrderCard card)
    {
        var id = card.Order.Id;
        if (card.IsDirty)
        {
            if (await SaveQuantitiesAsync(card).ConfigureAwait(true) is null)
            {
                return;
            }

            await _owner.ReloadAsync($"Bestellung {card.Order.Number} gespeichert.").ConfigureAwait(true);
        }

        var result = await _owner.Api.GetBytesAsync($"api/stock/orders/{id}/text").ConfigureAwait(true);
        if (result.Value is null)
        {
            _owner.ShowError(result.Error ?? "Der Bestelltext wurde nicht geladen.");
            return;
        }

        if (Orders.FirstOrDefault(o => o.Order.Id == id) is { } current)
        {
            current.OrderText = Encoding.UTF8.GetString(result.Value);
        }
    }

    /// <summary>Bucht die Lieferung; Mengen und Stand prüft und rechnet die API. Eine Position mit 0 kam nicht und geht nicht mit.</summary>
    internal async Task DeliverAsync(PurchaseOrderCard card)
    {
        var lines = new List<PurchaseOrderDeliveryLineInput>();
        var valid = true;
        foreach (var row in card.Lines)
        {
            var parsed = StockFormat.Parse(row.DeliveryQuantity);
            row.DeliveryError = parsed is null or < 0 ? "Gib eine Zahl ab 0 ein, z. B. 500." : null;
            valid &= row.DeliveryError is null && row.IsExpiresValid;
            if (parsed is { } amount && amount > 0)
            {
                lines.Add(new PurchaseOrderDeliveryLineInput(row.Line.Id, amount, null, Blank(row.Batch), row.ExpiresOn));
            }
        }

        if (!valid)
        {
            return;
        }

        var result = await _owner.Api.PostAsync<PurchaseOrderDto>($"api/stock/orders/{card.Order.Id}/deliveries", new PurchaseOrderDeliveryInput(lines, Blank(card.DeliveryNote))).ConfigureAwait(true);
        if (result.Value is null)
        {
            // Bei einer Zeitüberschreitung kann die API gebucht haben: Stand zeigen, sonst bucht der zweite Klick doppelt.
            await _owner.ReloadAsync().ConfigureAwait(true);
            _owner.ShowError(result.Error ?? "Die Lieferung wurde nicht gebucht.");
            return;
        }

        // Gebucht: was hier stand, darf nach dem Neuladen nicht als neue Lieferung stehen bleiben.
        Forget(card);
        await _owner.ReloadAsync($"Lieferung zu Bestellung {card.Order.Number} gebucht.").ConfigureAwait(true);
    }

    /// <summary>Storniert eine bestellte Bestellung oder schließt sie nach einer Teillieferung ab; beides nur nach der Rückfrage.</summary>
    internal async Task CloseAsync(PurchaseOrderCard card)
    {
        var number = card.Order.Number;
        var answer = await _owner.Ask(card.IsPartial
            ? new ConfirmRequest($"Bestellung {number} abschließen?", "Der Rest kommt nicht mehr und zählt nicht mehr als bestellt.", "Abschließen")
            : new ConfirmRequest($"Bestellung {number} stornieren?", "Die Bestellung wird storniert. Das Programm sagt beim Lieferanten nicht ab; die Absage läuft von Hand.", "Stornieren")).ConfigureAwait(true);
        if (!answer.IsConfirmed)
        {
            return;
        }

        var result = await _owner.Api.PostAsync<PurchaseOrderDto>($"api/stock/orders/{card.Order.Id}/close", null).ConfigureAwait(true);
        if (result.Value is null)
        {
            _owner.ShowError(result.Error ?? "Die Bestellung wurde nicht geändert.");
            return;
        }

        Forget(card);
        await _owner.ReloadAsync(card.IsPartial ? $"Bestellung {number} abgeschlossen." : $"Bestellung {number} storniert.").ConfigureAwait(true);
    }

    /// <summary>Nimmt die Karte heraus, auch wenn ein Neuladen sie während des Buchens ersetzt hat.</summary>
    private void Forget(PurchaseOrderCard card)
    {
        if (Orders.FirstOrDefault(o => o.Order.Id == card.Order.Id) is { } current)
        {
            Orders.Remove(current);
        }

        NotifyListChanged();
        NotifyChanged();
    }

    private static string? Blank(string text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    public async Task<bool> SaveChangesAsync()
    {
        foreach (var card in Orders.Where(o => o.IsDirty).ToList())
        {
            if (await SaveQuantitiesAsync(card).ConfigureAwait(true) is null)
            {
                return false;
            }
        }

        await _owner.ReloadAsync("Gespeichert.").ConfigureAwait(true);
        return true;
    }

    public void Discard()
    {
        foreach (var row in Orders.SelectMany(o => o.Lines))
        {
            row.Reset();
        }
    }

    internal void NotifyChanged()
    {
        OnPropertyChanged(nameof(HasUnsavedChanges));
        _owner.NotifyChanged();
    }
}
