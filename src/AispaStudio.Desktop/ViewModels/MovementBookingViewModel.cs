using System.Collections.ObjectModel;
using System.ComponentModel;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>Material in der Auswahl der Seitenleiste „Bewegung buchen“.</summary>
public sealed record StockItemChoice(Guid Id, string Name, string Unit, IReadOnlyList<StockPackageDto>? Packages = null)
{
    public override string ToString() => Name;
}

/// <summary>Eine Ebene (Einheit, Packung, Gebinde), in der ein Wareneingang erfasst wird.</summary>
public sealed record StockPackageChoice(StockPackageDto Package)
{
    public override string ToString() => Package.Label;
}

/// <summary>Lagerort in der Auswahl (nur aktive, dazu der gewählte).</summary>
public sealed record StockPlaceOption(Guid Id, string Name)
{
    public override string ToString() => Name;
}

/// <summary>Ziel einer Umbuchung oder des Nachfüllens: <c>place:&lt;Kennung&gt;</c> für einen Lagerort dieses Standorts, <c>location:&lt;Kennung&gt;</c> für ein anderes Hauptlager.</summary>
public sealed record MovementTarget(string Key, string Label)
{
    public override string ToString() => Label;
}

/// <summary>Was die Seitenleiste vorbelegt, wenn sie aus einer Zeile oder dem Knopf der Seite geöffnet wird.</summary>
public sealed record MovementPreset(StockMovementKind? Kind = null, Guid? ItemId = null, Guid? PlaceId = null, Guid? TargetPlaceId = null);

/// <summary>
/// Seitenleiste „Bewegung buchen“ (R05 Slice 101) nach <c>MovementPanel.tsx</c> und Design-Brief 5.12: Art als Reiter (Wareneingang, Nachfüllen, Umbuchen, Korrektur), Material,
/// Lagerort vorbelegt, Menge und je Art die weiteren Angaben. Die Inventur hat einen eigenen Reiter, Verbrauch und Verkauf bucht das System. Bei nur einem Lagerort entfällt die Wahl.
/// </summary>
public sealed partial class MovementBookingViewModel : ViewModelBase, IFocusTarget
{
    public const string ItemField = "MovementItemField";

    public const string AmountField = "MovementAmountField";

    public const string ReasonField = "MovementReasonField";

    private static readonly string[] Inputs =
    [
        nameof(Kind), nameof(SelectedItem), nameof(SelectedPlace), nameof(SelectedTarget), nameof(SelectedPackage), nameof(Amount), nameof(UnitCost), nameof(Batch), nameof(ExpiresOn), nameof(Reason),
    ];

    private readonly StockViewModel _owner;
    private readonly IWorkstationApi _api;
    private bool _applying;

    internal MovementBookingViewModel(StockViewModel owner, IWorkstationApi api)
    {
        _owner = owner;
        _api = api;
    }

    public ObservableCollection<StockItemChoice> Items { get; } = [];

    public ObservableCollection<StockPlaceOption> PlaceOptions { get; } = [];

    public ObservableCollection<MovementTarget> Targets { get; } = [];

    public ObservableCollection<StockPackageChoice> PackageOptions { get; } = [];

    [ObservableProperty]
    public partial bool IsOpen { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsReceipt), nameof(IsRefill), nameof(IsTransfer), nameof(IsCorrection), nameof(ShowTarget), nameof(ShowReceiptFields), nameof(AmountLabel),
        nameof(AmountHelp), nameof(ReasonLabel), nameof(PlaceLabel), nameof(TargetLabel), nameof(HelpTopic), nameof(HelpSection), nameof(Hint), nameof(ShowPackages), nameof(PackagePreview))]
    public partial StockMovementKind Kind { get; set; } = StockMovementKind.GoodsReceipt;

    [ObservableProperty]
    public partial StockItemChoice? SelectedItem { get; set; }

    [ObservableProperty]
    public partial StockPlaceOption? SelectedPlace { get; set; }

    [ObservableProperty]
    public partial MovementTarget? SelectedTarget { get; set; }

    /// <summary>In welcher Ebene der Wareneingang erfasst wird; Menge und Preis gelten je Ebene, umgerechnet wird in der API.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UnitCostLabel))]
    public partial StockPackageChoice? SelectedPackage { get; set; }

    /// <summary>Die Menge mit Komma oder Punkt; bei der Korrektur mit Vorzeichen.</summary>
    [ObservableProperty]
    public partial string Amount { get; set; } = "";

    [ObservableProperty]
    public partial string UnitCost { get; set; } = "";

    [ObservableProperty]
    public partial string Batch { get; set; } = "";

    [ObservableProperty]
    public partial DateOnly? ExpiresOn { get; set; }

    [ObservableProperty]
    public partial string Reason { get; set; } = "";

    [ObservableProperty]
    public partial string? AmountError { get; private set; }

    [ObservableProperty]
    public partial string? UnitCostError { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? Error { get; private set; }

    public bool HasError => Error is not null;

    [ObservableProperty]
    public partial string? FocusTarget { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(BookCommand))]
    public partial bool IsBusy { get; private set; }

    public bool IsReceipt => Kind == StockMovementKind.GoodsReceipt;

    public bool IsRefill => Kind == StockMovementKind.Refill;

    public bool IsTransfer => Kind == StockMovementKind.Transfer;

    public bool IsCorrection => Kind == StockMovementKind.Correction;

    /// <summary>Nachfüllen gibt es nur zwischen mehreren Lagerorten.</summary>
    public bool ShowRefillTab => ActivePlaces.Count > 1;

    /// <summary>Umbuchen geht zwischen Lagerorten oder an einen anderen Standort.</summary>
    public bool ShowTransferTab => ActivePlaces.Count > 1 || OtherLocations.Count > 0;

    public bool ShowPlace => ActivePlaces.Count > 1;

    public bool ShowTarget => Kind is StockMovementKind.Refill or StockMovementKind.Transfer;

    public bool ShowReceiptFields => IsReceipt;

    /// <summary>Die Wahl der Ebene gibt es nur beim Wareneingang und nur, wenn das Material Packungen kennt.</summary>
    public bool ShowPackages => IsReceipt && PackageOptions.Count > 1;

    public string UnitCostLabel => SelectedPackage is { Package.Level: not StockPackageLevel.Unit } choice ? $"Preis netto je {choice.Package.Name}" : "Preis netto je Einheit";

    /// <summary>Was die Eingabe in der Einheit des Materials ergibt, z. B. „= 2.000 ml“.</summary>
    public string? PackagePreview => ShowPackages && SelectedPackage is { Package.Level: not StockPackageLevel.Unit } choice && SelectedItem is { } item && StockFormat.Parse(Amount) is > 0 and var amount
        ? $"= {StockFormat.Quantity(amount * choice.Package.BaseQuantity)} {item.Unit}"
        : null;

    public string LocationName => _owner.Location?.Name ?? "";

    public string PlaceLabel => Kind is StockMovementKind.GoodsReceipt or StockMovementKind.Correction ? "Lagerort" : "Von Lagerort";

    public string TargetLabel => Kind == StockMovementKind.Refill ? "Nach Lagerort" : "Ziel";

    public string AmountLabel => IsCorrection ? "Menge mit Vorzeichen" : "Menge";

    public string? AmountHelp => IsCorrection ? "z. B. −2 für Bruch" : null;

    public string ReasonLabel => IsCorrection ? "Grund" : "Grund (freiwillig)";

    public string HelpTopic => Kind switch
    {
        StockMovementKind.Refill => "lager-nachfuellen",
        StockMovementKind.Transfer => "lager-umbuchen",
        StockMovementKind.Correction => "lager-korrektur-buchen",
        _ => "lager-wareneingang-buchen",
    };

    public string HelpSection => IsCorrection ? "korrektur" : "schritte";

    /// <summary>Was fehlt, damit „Buchen“ bedienbar wird (ruhige Zeile unter den Feldern).</summary>
    public string? Hint => MissingInput() is { } missing ? $"Zum Buchen fehlt: {missing}." : null;

    public bool CanBook => !IsBusy && MissingInput() is null;

    private IReadOnlyList<StoragePlaceDto> ActivePlaces => [.. _owner.Places.Where(p => p.IsActive)];

    private IReadOnlyList<StockLocationChoice> OtherLocations => [.. _owner.Locations.Where(l => l.Id != _owner.Location?.Id)];

    /// <summary>Öffnet die Leiste, vorbelegt aus einer Zeile des Bestands oder leer über den Knopf der Seite.</summary>
    public void Open(MovementPreset? preset = null)
    {
        preset ??= new MovementPreset();
        _applying = true;
        Items.Clear();
        foreach (var item in _owner.Items.Where(i => i.IsActive).OrderBy(i => i.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            Items.Add(new StockItemChoice(item.Id, item.Name, item.Unit, item.Packages));
        }

        PlaceOptions.Clear();
        var main = _owner.MainPlace;
        foreach (var place in _owner.Places.Where(p => p.IsActive || p.Id == (preset.PlaceId ?? main?.Id)))
        {
            PlaceOptions.Add(new StockPlaceOption(place.Id, place.Name));
        }

        var kind = preset.Kind ?? StockMovementKind.GoodsReceipt;
        Kind = KindAllowed(kind) ? kind : StockMovementKind.GoodsReceipt;
        SelectedItem = Items.FirstOrDefault(i => i.Id == preset.ItemId);
        SelectedPlace = PlaceOptions.FirstOrDefault(p => p.Id == (preset.PlaceId ?? main?.Id));
        Amount = UnitCost = Batch = Reason = "";
        ExpiresOn = null;
        Error = AmountError = UnitCostError = null;
        RebuildPackages(null);
        _applying = false;
        RebuildTargets(preset.TargetPlaceId);
        IsOpen = true;
        Refresh();
        FocusTarget = ItemField;
    }

    [RelayCommand]
    public void Close() => IsOpen = false;

    /// <summary>Die Reiter der Leiste setzen die Art.</summary>
    [RelayCommand]
    private void ShowKind(StockMovementKind kind)
    {
        if (!KindAllowed(kind))
        {
            return;
        }

        Error = null;
        Kind = kind;
    }

    private bool KindAllowed(StockMovementKind kind) => kind switch
    {
        StockMovementKind.GoodsReceipt or StockMovementKind.Correction => true,
        StockMovementKind.Refill => ShowRefillTab,
        StockMovementKind.Transfer => ShowTransferTab,
        _ => false,
    };

    [RelayCommand(CanExecute = nameof(CanBook))]
    private async Task BookAsync() => await BookMovementAsync().ConfigureAwait(true);

    /// <summary>Bucht die Bewegung; false bei fehlenden oder ungültigen Angaben (dann geht nichts an die API) oder bei einem Fehler der API.</summary>
    public async Task<bool> BookMovementAsync()
    {
        if (IsBusy || _owner.Location is not { } location)
        {
            return false;
        }

        if (MissingInput() is { } missing)
        {
            Error = $"Es fehlt: {missing}.";
            FocusTarget = SelectedItem is null ? ItemField : ReasonMissing() ? ReasonField : AmountField;
            return false;
        }

        var quantity = StockFormat.Parse(Amount)!.Value;
        decimal? unitCost = null;
        if (IsReceipt && UnitCost.Trim().Length > 0)
        {
            unitCost = StockFormat.Parse(UnitCost);
            if (unitCost is null or < 0)
            {
                UnitCostError = "Der Preis ist keine Zahl ab 0. Gib ihn z. B. als 12,50 ein.";
                return false;
            }
        }

        var targetPlace = SelectedTarget is { } target && target.Key.StartsWith("place:", StringComparison.Ordinal) ? Guid.Parse(target.Key["place:".Length..]) : (Guid?)null;
        var targetLocation = SelectedTarget is { } other && other.Key.StartsWith("location:", StringComparison.Ordinal) ? Guid.Parse(other.Key["location:".Length..]) : (Guid?)null;
        var level = IsReceipt ? SelectedPackage?.Package.Level ?? StockPackageLevel.Unit : StockPackageLevel.Unit;
        var unit = SelectedItem!.Unit;
        var body = new StockMovementInput(
            SelectedItem!.Id,
            Kind,
            quantity,
            unitCost,
            Kind == StockMovementKind.Transfer ? targetLocation : null,
            Reason.Trim().Length == 0 ? null : Reason.Trim(),
            IsReceipt && Batch.Trim().Length > 0 ? Batch.Trim() : null,
            IsReceipt ? ExpiresOn : null,
            SelectedPlace?.Id,
            Kind is StockMovementKind.Refill or StockMovementKind.Transfer ? targetPlace : null,
            PackageLevel: level);
        IsBusy = true;
        var result = await _api.PostAsync<List<StockMovementDto>>($"api/stock/locations/{location.Id}/movements", body).ConfigureAwait(true);
        IsBusy = false;
        if (result.Value is not { } booked)
        {
            Error = result.Error ?? "Die Bewegung wurde nicht gebucht.";
            return false;
        }

        Error = null;
        _applying = true;
        SelectedItem = null;
        Amount = UnitCost = Batch = Reason = "";
        ExpiresOn = null;
        RebuildPackages(null);
        _applying = false;
        Refresh();
        await _owner.ReloadAsync(level != StockPackageLevel.Unit && booked.Count > 0 ? $"Gebucht: {StockFormat.Quantity(booked[0].Quantity)} {unit}." : "Gebucht.").ConfigureAwait(true);
        return true;
    }

    /// <summary>Der Standort wechselt oder der Bestand wird neu geladen: die Auswahl der Lagerorte und Ziele folgt.</summary>
    internal void Rebuild()
    {
        if (!IsOpen)
        {
            return;
        }

        var item = SelectedItem?.Id;
        var place = SelectedPlace?.Id;
        _applying = true;
        PlaceOptions.Clear();
        foreach (var option in _owner.Places.Where(p => p.IsActive || p.Id == place))
        {
            PlaceOptions.Add(new StockPlaceOption(option.Id, option.Name));
        }

        SelectedPlace = PlaceOptions.FirstOrDefault(p => p.Id == place) ?? PlaceOptions.FirstOrDefault();
        Items.Clear();
        foreach (var choice in _owner.Items.Where(i => i.IsActive).OrderBy(i => i.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            Items.Add(new StockItemChoice(choice.Id, choice.Name, choice.Unit, choice.Packages));
        }

        var level = SelectedPackage?.Package.Level;
        SelectedItem = Items.FirstOrDefault(i => i.Id == item);
        RebuildPackages(level);
        _applying = false;
        if (!KindAllowed(Kind))
        {
            Kind = StockMovementKind.GoodsReceipt;
        }

        RebuildTargets(null);
        OnPropertyChanged(nameof(ShowRefillTab));
        OnPropertyChanged(nameof(ShowTransferTab));
        OnPropertyChanged(nameof(ShowPlace));
        OnPropertyChanged(nameof(LocationName));
    }

    /// <summary>Die Ebenen des gewählten Materials; ohne gewünschte Ebene steht die Einheit vorn.</summary>
    private void RebuildPackages(StockPackageLevel? keep)
    {
        PackageOptions.Clear();
        foreach (var package in SelectedItem?.Packages ?? [])
        {
            PackageOptions.Add(new StockPackageChoice(package));
        }

        SelectedPackage = PackageOptions.FirstOrDefault(p => p.Package.Level == keep) ?? PackageOptions.FirstOrDefault();
        OnPropertyChanged(nameof(ShowPackages));
        OnPropertyChanged(nameof(PackagePreview));
    }

    /// <summary>Die Ziele zur Art und zum Ausgangsort; ein Ziel darf nicht gleich dem Ausgangsort sein.</summary>
    private void RebuildTargets(Guid? preferPlace)
    {
        var keep = SelectedTarget?.Key;
        var from = SelectedPlace?.Id;
        _applying = true;
        Targets.Clear();
        if (Kind is StockMovementKind.Refill or StockMovementKind.Transfer)
        {
            foreach (var place in ActivePlaces.Where(p => p.Id != from))
            {
                Targets.Add(new MovementTarget($"place:{place.Id}", place.Name));
            }

            if (Kind == StockMovementKind.Transfer)
            {
                foreach (var location in OtherLocations)
                {
                    Targets.Add(new MovementTarget($"location:{location.Id}", $"{location.Name} (ins Hauptlager)"));
                }
            }
        }

        SelectedTarget = (preferPlace is { } wanted ? Targets.FirstOrDefault(t => t.Key == $"place:{wanted}") : null)
            ?? Targets.FirstOrDefault(t => t.Key == keep) ?? Targets.FirstOrDefault();
        _applying = false;
        Refresh();
    }

    private bool ReasonMissing() => IsCorrection && Reason.Trim().Length == 0;

    /// <summary>Die erste fehlende oder ungültige Angabe, sonst <c>null</c>: Material, Menge (bei der Korrektur ohne 0), Ziel, Grund der Korrektur.</summary>
    private string? MissingInput()
    {
        if (SelectedItem is null)
        {
            return "das Material";
        }

        var quantity = StockFormat.Parse(Amount);
        if (quantity is null || (IsCorrection ? quantity == 0 : quantity <= 0))
        {
            return "die Menge";
        }

        if (ShowTarget && SelectedTarget is null)
        {
            return "das Ziel";
        }

        return ReasonMissing() ? "der Grund" : null;
    }

    private void Refresh()
    {
        AmountError = Amount.Trim().Length == 0 || MissingInput() != "die Menge" ? null
            : StockFormat.Parse(Amount) is null ? "Die Menge ist keine Zahl. Gib sie z. B. als 2,5 ein."
            : IsCorrection ? "Die Menge darf nicht 0 sein. Gib z. B. −2 für Bruch ein." : "Die Menge muss größer als 0 sein.";
        UnitCostError = null;
        OnPropertyChanged(nameof(PackagePreview));
        OnPropertyChanged(nameof(Hint));
        OnPropertyChanged(nameof(CanBook));
        BookCommand.NotifyCanExecuteChanged();
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (_applying || e.PropertyName is not { } name || !Inputs.Contains(name))
        {
            return;
        }

        if (name is nameof(SelectedPlace) or nameof(Kind))
        {
            RebuildTargets(null);
            return;
        }

        if (name == nameof(SelectedItem))
        {
            _applying = true;
            RebuildPackages(null);
            _applying = false;
        }

        Refresh();
    }
}
