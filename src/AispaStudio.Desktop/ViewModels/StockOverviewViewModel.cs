using System.Collections.ObjectModel;
using System.Windows.Input;
using AispaStudio.Contracts;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>Eine Zeile der Bestandsliste, aus der Standortsumme oder aus der Liste eines Lagerorts.</summary>
public sealed record StockRow(Guid ItemId, string Name, string Unit, decimal Quantity, string QuantityText, string MinimumText, string PlannedText, string OnOrderText,
    string SuggestedText, string ExpiryText, string ValueText, bool Below, bool Expired, string PlacesText)
{
    public bool HasPlaces => PlacesText.Length > 0;

    /// <summary>Verbrauch geplant, Bestellt und Bestellvorschlag gelten nur für den Standort, nicht für einen einzelnen Lagerort.</summary>
    public bool SiteColumns { get; init; }

    public bool CanBook { get; init; }

    public bool CanRefill { get; init; }

    public bool CanChange { get; init; }

    /// <summary>„133 Behandlungen“: so weit reicht der Bestand laut Richtwert des Materials; leer ohne Richtwert.</summary>
    public string ReachText { get; init; } = "";

    /// <summary>Der Chip der Zeile: „abgelaufen“, „nachbestellen“ (unter Mindestbestand oder nur noch für wenige Behandlungen) oder leer.</summary>
    public string StatusText { get; init; } = "";

    public bool HasStatus => StatusText.Length > 0;

    /// <summary>Der Balken gegen den Mindestbestand: voll bei dem Doppelten des Mindestbestands; ohne Mindestbestand gibt es keinen Balken.</summary>
    public double Fill { get; init; }

    public bool HasBar { get; init; }

    /// <summary>Die übrigen Werte der Zeile für das Seitenpanel; leere Werte fehlen.</summary>
    public IReadOnlyList<StockDetailLine> Details { get; init; } = [];

    public string ChangeName => $"{Name} ändern";

    /// <summary>Lädt das Material ins Formular „Material ändern“.</summary>
    public ICommand? ChangeCommand { get; init; }

    public string BookName => $"{Name} buchen";

    public string RefillName => $"{Name} nachfüllen";

    /// <summary>Öffnet die Leiste „Bewegung buchen“ für dieses Material.</summary>
    public ICommand? BookCommand { get; init; }

    /// <summary>Öffnet die Leiste im Reiter „Nachfüllen“ (nur bei gewähltem Lagerort außer dem Hauptlager).</summary>
    public ICommand? RefillCommand { get; init; }

    /// <summary>Setzt Chip, Balken, „Reicht für“ und die Werte des Seitenpanels aus Mindestbestand und Richtwert der Behandlungen.</summary>
    internal StockRow WithDisplay(decimal minimum, int? treatments)
    {
        var status = Expired ? "abgelaufen" : Below || StockOverviewViewModel.IsFew(treatments) ? "nachbestellen" : "";
        StockDetailLine[] lines =
        [
            new("Einheit", Unit), new("Mindestbestand", MinimumText), new("Verbrauch geplant", PlannedText), new("Bestellt", OnOrderText), new("Bestellvorschlag", SuggestedText),
            new("Läuft ab", ExpiryText), new("Wert", ValueText), new("Lagerorte", PlacesText),
        ];
        return this with
        {
            ReachText = StockOverviewViewModel.Reach(treatments),
            StatusText = status,
            HasBar = minimum > 0,
            Fill = minimum > 0 ? Math.Min(1d, (double)(Quantity / (2 * minimum))) : 0,
            Details = [.. lines.Where(l => l.Value.Length > 0)],
        };
    }

    /// <summary>Der Name für Screenreader: Material mit Bestand und Hinweisen.</summary>
    public string Summary => $"{Name}, {QuantityText} {Unit}{(ReachText.Length > 0 ? $", reicht für {ReachText}" : "")}{(Below ? ", unter Minimum" : "")}{(Expired ? ", abgelaufen" : "")}{(HasStatus && !Expired ? $", {StatusText}" : "")}";

    public override string ToString() => Summary;
}

/// <summary>Ein Wert im Seitenpanel einer Bestandszeile.</summary>
public sealed record StockDetailLine(string Label, string Value)
{
    /// <summary>Der Name für Screenreader: „Mindestbestand 2“.</summary>
    public string Summary => $"{Label} {Value}";

    public override string ToString() => Summary;
}

/// <summary>
/// Reiter „Bestand“ (R05 Slice 101, R07 Slice 143) nach <c>StockOverview.tsx</c>: Liste mit vier Spalten (Material, Bestand mit Balken, „Reicht für“, Status-Chip), Suche und „Nur unter Minimum“;
/// die übrigen Werte einer Zeile stehen im Seitenpanel. Bei „Alle Lagerorte“ ist der Bestand die Summe des Standorts (mit Verbrauch geplant, Bestellt und Bestellvorschlag); bei einem Lagerort nur
/// dessen Bestand, Mindestmenge und Wert. Die Maske zeigt nur an; gerechnet wird in der API. Die Übersicht („Was ist zu tun“) steht in <see cref="StockTodoViewModel"/>.
/// </summary>
public sealed partial class StockOverviewViewModel : ViewModelBase
{
    /// <summary>Bis zu dieser Zahl an Behandlungen gilt ein Material als „nur noch für wenige Behandlungen“.</summary>
    internal const int FewTreatments = 5;

    private readonly StockViewModel _owner;
    private List<StockRow> _all = [];

    internal StockOverviewViewModel(StockViewModel owner) => _owner = owner;

    public ObservableCollection<StockRow> Rows { get; } = [];

    /// <summary>Die gewählte Zeile; ein Sprung aus Kalender oder Assistent wählt hier das Material.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    public partial StockRow? SelectedRow { get; set; }

    public bool HasSelection => SelectedRow is not null;

    [ObservableProperty]
    public partial string Search { get; set; } = "";

    [ObservableProperty]
    public partial bool OnlyBelow { get; set; }

    [ObservableProperty]
    public partial string ValueText { get; private set; } = "";

    [ObservableProperty]
    public partial string ValueNote { get; private set; } = "am Standort";

    /// <summary>Ein Lagerort ist gewählt: „Verbrauch geplant“, „Bestellt“ und „Bestellvorschlag“ gelten nur für den Standort und entfallen.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowSiteColumns), nameof(ShowRefill))]
    public partial bool PlaceView { get; private set; }

    public bool ShowSiteColumns => !PlaceView;

    public bool CanBook => _owner.CanBook;

    public bool CanManage => _owner.CanManage;

    /// <summary>Nachfüllen gibt es bei gewähltem Lagerort außer dem Hauptlager.</summary>
    public bool ShowRefill => CanBook && PlaceView && _owner.PlaceFilterId != _owner.MainPlace?.Id;

    public bool ShowEmpty => Rows.Count == 0;

    /// <summary>Baut die Zeilen aus dem Stand des Besitzers neu auf (nach dem Laden, nach jeder Buchung, beim Wechsel des Lagerorts).</summary>
    internal void Refresh()
    {
        var overview = _owner.OverviewData;
        var placeId = _owner.PlaceFilterId;
        PlaceView = placeId is not null;
        OnPropertyChanged(nameof(CanBook));
        OnPropertyChanged(nameof(CanManage));
        if (overview is null)
        {
            _all = [];
            ApplyFilter();
            return;
        }

        var batches = _owner.BatchData;
        bool ExpiredIn(Guid itemId) => batches?.Batches.Any(b => b.ItemId == itemId && b.Expired && (placeId is null || b.StoragePlaceId == placeId)) ?? false;
        if (placeId is not null)
        {
            var canRefill = _owner.CanBook && placeId != _owner.MainPlace?.Id;
            _all = [.. (_owner.LevelData?.Items ?? []).Select(l => new StockRow(l.ItemId, l.Name, l.Unit, l.Quantity, StockFormat.Quantity(l.Quantity),
                l.MinimumQuantity is { } minimum ? StockFormat.Quantity(minimum) : "", "", "", "", l.NextExpiry is { } next ? StockFormat.Date(next) : "",
                Money.Format(l.Value), l.BelowMinimum, ExpiredIn(l.ItemId), "") { BookCommand = BookCommandFor(l.ItemId), RefillCommand = RefillCommandFor(l.ItemId), CanBook = _owner.CanBook, CanRefill = canRefill, CanChange = _owner.CanCatalog, ChangeCommand = ChangeCommandFor(l.ItemId) }
                .WithDisplay(l.MinimumQuantity ?? 0, l.TreatmentsCovered))];
            ValueText = Money.Format(_owner.LevelData?.TotalValue ?? 0);
            ValueNote = "am gewählten Lagerort";
        }
        else
        {
            var names = _owner.Places.ToDictionary(p => p.Id, p => p.Name);
            var several = _owner.Places.Count > 1;
            _all = [.. overview.Items.Select(i => new StockRow(i.ItemId, i.Name, i.Unit, i.Quantity, StockFormat.Quantity(i.Quantity), StockFormat.Quantity(i.MinimumQuantity),
                StockFormat.Quantity(i.PlannedUse), i.OnOrder != 0 ? StockFormat.Quantity(i.OnOrder) : "", i.Suggested > 0 ? StockFormat.Quantity(i.Suggested) : "",
                i.NextExpiry is { } next ? StockFormat.Date(next) : "", Money.Format(i.Value), i.BelowMinimum, ExpiredIn(i.ItemId),
                several ? string.Join(" · ", (i.Places ?? []).Select(p => $"{names.GetValueOrDefault(p.StoragePlaceId, "")}: {StockFormat.Quantity(p.Quantity)}")) : "")
                { BookCommand = BookCommandFor(i.ItemId), RefillCommand = RefillCommandFor(i.ItemId), SiteColumns = true, CanBook = _owner.CanBook, CanChange = _owner.CanCatalog,
                    ChangeCommand = ChangeCommandFor(i.ItemId) }.WithDisplay(i.MinimumQuantity, i.TreatmentsCovered))];
            ValueText = Money.Format(overview.TotalValue);
            ValueNote = "am Standort";
        }

        OnPropertyChanged(nameof(ShowRefill));
        ApplyFilter();
    }

    /// <summary>Wählt das Material in der Liste (Sprung aus Kalender, Assistent oder Scanner); das Suchfeld wird dafür geleert.</summary>
    internal void Select(Guid itemId)
    {
        _owner.ScanText = "";
        Search = "";
        OnlyBelow = false;
        SelectedRow = Rows.FirstOrDefault(r => r.ItemId == itemId);
    }

    private void ApplyFilter()
    {
        var selected = SelectedRow?.ItemId;
        var text = Search.Trim();
        Rows.Clear();
        foreach (var row in _all.Where(r => r.Name.Contains(text, StringComparison.CurrentCultureIgnoreCase) && (!OnlyBelow || r.Below)))
        {
            Rows.Add(row);
        }

        SelectedRow = Rows.FirstOrDefault(r => r.ItemId == selected);
        OnPropertyChanged(nameof(ShowEmpty));
    }

    /// <summary>Schließt das Seitenpanel der Zeile.</summary>
    [RelayCommand]
    private void CloseDetail() => SelectedRow = null;

    partial void OnSearchChanged(string value) => ApplyFilter();

    partial void OnOnlyBelowChanged(bool value) => ApplyFilter();

    private RelayCommand BookCommandFor(Guid itemId) => new(() => Book(itemId));

    private RelayCommand RefillCommandFor(Guid itemId) => new(() => Refill(itemId));

    private AsyncRelayCommand ChangeCommandFor(Guid itemId) => new(() => ChangeAsync(itemId));

    internal static string Reach(int? treatments) => treatments switch
    {
        null => "",
        1 => "1 Behandlung",
        _ => $"{treatments} Behandlungen",
    };

    /// <summary>Das Material reicht laut Richtwert nur noch für wenige Behandlungen.</summary>
    internal static bool IsFew(int? treatments) => treatments is { } count && count <= FewTreatments;

    /// <summary>„Material ändern“: eine angefangene Eingabe im Formular wird vorher nachgefragt.</summary>
    public async Task ChangeAsync(Guid itemId)
    {
        if (_owner.Items.FirstOrDefault(i => i.Id == itemId) is not { } item)
        {
            return;
        }

        if (await UnsavedChanges.ConfirmLeaveAsync(_owner.Dialogs, _owner.ItemForm).ConfigureAwait(true))
        {
            _owner.ItemForm.Edit(item);
            if (_owner.TakeBarcodeToAssign() is { } code)
            {
                _owner.ItemForm.Barcode = code;
            }
        }
    }

    /// <summary>Die Leiste „Bewegung buchen“ für dieses Material.</summary>
    public void Book(Guid itemId) => _owner.OpenBooking(new MovementPreset(ItemId: itemId, PlaceId: _owner.PlaceFilterId ?? _owner.MainPlace?.Id));

    /// <summary>Nachfüllen aus dem Hauptlager in den gewählten Lagerort.</summary>
    public void Refill(Guid itemId) => _owner.OpenBooking(new MovementPreset(StockMovementKind.Refill, itemId, _owner.MainPlace?.Id, _owner.PlaceFilterId));

    /// <summary>Bucht den Rest einer abgelaufenen Charge als Korrektur aus.</summary>
    public async Task WriteOffAsync(StockBatchDto batch)
    {
        if (_owner.Location is not { } location)
        {
            return;
        }

        var body = new StockMovementInput(batch.ItemId, StockMovementKind.Correction, -batch.Quantity, StoragePlaceId: batch.StoragePlaceId,
            Reason: $"Abgelaufen{(batch.Batch is null ? "" : $", Charge {batch.Batch}")} ({StockFormat.Date(batch.ExpiresOn)})");
        var result = await _owner.Api.PostAsync<List<StockMovementDto>>($"api/stock/locations/{location.Id}/movements", body).ConfigureAwait(true);
        if (result.Value is null)
        {
            _owner.ShowError(result.Error ?? "Die Charge wurde nicht ausgebucht.");
            return;
        }

        await _owner.ReloadAsync($"{StockFormat.Quantity(batch.Quantity)} {batch.Unit} {batch.ItemName} ausgebucht.").ConfigureAwait(true);
    }
}
