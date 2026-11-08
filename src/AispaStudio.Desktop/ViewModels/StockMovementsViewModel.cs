using System.Collections.ObjectModel;
using System.Globalization;
using AispaStudio.Contracts;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>Eine Bewegung in der Liste.</summary>
public sealed record MovementRow(Guid Id, string AtText, string KindText, string Item, string Place, string QuantityText, bool IsNegative, string PriceText, string Detail)
{
    public string Summary => $"{AtText}, {KindText}, {Item}, {QuantityText}";

    public override string ToString() => Summary;
}

/// <summary>
/// Bewegungen (R05 Slice 101) nach <c>StockMovements.tsx</c>: die Bewegungen eines Standorts im gewählten Zeitraum (vorbelegt die letzten 30 Tage), auf Wunsch nur eines Lagerorts.
/// Verbrauch und Verkauf bucht das System selbst; hier stehen sie neben den Buchungen von Hand.
/// </summary>
public sealed partial class StockMovementsViewModel : ViewModelBase
{
    private readonly StockViewModel _owner;
    private List<StockMovementDto>? _movements;
    private int _version;

    internal StockMovementsViewModel(StockViewModel owner, TimeProvider time)
    {
        _owner = owner;
        var today = DateOnly.FromDateTime(time.GetLocalNow().DateTime);
        From = today.AddDays(-30);
        Until = today;
    }

    public ObservableCollection<MovementRow> Rows { get; } = [];

    [ObservableProperty]
    public partial DateOnly? From { get; set; }

    [ObservableProperty]
    public partial DateOnly? Until { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPeriodError))]
    public partial string? PeriodError { get; private set; }

    public bool HasPeriodError => PeriodError is not null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLoadError))]
    public partial string? LoadError { get; private set; }

    public bool HasLoadError => LoadError is not null;

    [ObservableProperty]
    public partial bool IsLoaded { get; private set; }

    public bool ShowEmpty => IsLoaded && !HasLoadError && Rows.Count == 0;

    /// <summary>Lädt die Bewegungen des Zeitraums neu; bei Ende vor Beginn meldet das Feld den Fehler und es geht nichts an die API.</summary>
    [RelayCommand]
    public async Task LoadAsync()
    {
        if (_owner.Location is not { } location || From is not { } from || Until is not { } until)
        {
            return;
        }

        PeriodError = from > until ? "Das Ende liegt vor dem Beginn. Wähle ein Ende am oder nach dem Beginn." : null;
        if (PeriodError is not null)
        {
            return;
        }

        var version = ++_version;
        var query = string.Create(CultureInfo.InvariantCulture, $"from={from:yyyy-MM-dd}&until={until:yyyy-MM-dd}");
        var result = await _owner.Api.GetAsync<List<StockMovementDto>>($"api/stock/locations/{location.Id}/movements?{query}").ConfigureAwait(true);
        if (version != _version)
        {
            return; // Zeitraum oder Standort wurden inzwischen gewechselt; die neuere Antwort gilt.
        }

        LoadError = result.Value is null ? result.Error ?? "Die Bewegungen konnten nicht geladen werden." : null;
        _movements = result.Value;
        IsLoaded = true;
        Refresh();
    }

    /// <summary>Zeigt die Bewegungen neu, etwa nach dem Wechsel des Lagerort-Filters.</summary>
    internal void Refresh()
    {
        var placeId = _owner.PlaceFilterId;
        Rows.Clear();
        foreach (var m in (_movements ?? []).Where(m => placeId is null || m.StoragePlaceId == placeId))
        {
            var detail = new[] { m.Reason, m.Batch is null ? null : $"Charge {m.Batch}", m.ExpiresOn is { } expires ? $"ablaufend {StockFormat.Date(expires)}" : null,
                m.AppointmentId is null ? null : "Termin", m.ReceiptId is null ? null : "Beleg", m.PurchaseOrderId is null ? null : "Bestellung" };
            Rows.Add(new MovementRow(m.Id, m.At.ToLocalTime().ToString("dd.MM.yy HH:mm", CultureInfo.InvariantCulture), StockFormat.Kind(m.Kind), m.ItemName, m.StoragePlaceName,
                StockFormat.Quantity(m.Quantity), m.Quantity < 0, Money.Format(m.UnitCost), string.Join(", ", detail.Where(d => !string.IsNullOrEmpty(d)))));
        }

        OnPropertyChanged(nameof(ShowEmpty));
    }

    partial void OnFromChanged(DateOnly? value) => _ = LoadAsync();

    partial void OnUntilChanged(DateOnly? value) => _ = LoadAsync();
}
