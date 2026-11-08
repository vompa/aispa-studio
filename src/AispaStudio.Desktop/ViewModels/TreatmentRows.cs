using System.ComponentModel;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>Eintrag einer Auswahl (Lagerartikel, Standort); <c>Id</c> leer heißt „keiner“ („frei erfasst“, „Alle Standorte“).</summary>
public sealed record Choice(Guid? Id, string Label)
{
    public override string ToString() => Label;
}

/// <summary>Zeitpunkt von „Zum Termin bereitstellen“ mit dem Text des Web (<c>ExtrasSection.tsx</c>).</summary>
public sealed record TimingChoice(ExtraTiming Timing, string Label)
{
    public static IReadOnlyList<TimingChoice> All { get; } =
        [new(ExtraTiming.OnArrival, "Bei Ankunft"), new(ExtraTiming.BeforeEnd, "Minuten vor Ende"), new(ExtraTiming.AfterTreatment, "Nach der Behandlung")];

    public override string ToString() => Label;
}

/// <summary>Ankreuzfeld einer Liste (Qualifikation, Raum, Gerät); meldet jede Änderung der Maske.</summary>
public sealed partial class CheckOption(Guid id, string label, bool isChecked, Action changed) : ObservableObject
{
    public Guid Id => id;

    public string Label => label;

    [ObservableProperty]
    public partial bool IsChecked { get; set; } = isChecked;

    partial void OnIsCheckedChanged(bool value) => changed();
}

/// <summary>Wochentag einer Preisregel (Mo–So).</summary>
public sealed partial class DayOption(DayOfWeek day, bool isChecked, Action changed) : ObservableObject
{
    private static readonly string[] Short = ["So", "Mo", "Di", "Mi", "Do", "Fr", "Sa"];
    private static readonly string[] Long = ["Sonntag", "Montag", "Dienstag", "Mittwoch", "Donnerstag", "Freitag", "Samstag"];

    public DayOfWeek Day => day;

    public string Label => Short[(int)day];

    /// <summary>Für den Screenreader der ausgeschriebene Tag.</summary>
    public string Name => Long[(int)day];

    [ObservableProperty]
    public partial bool IsChecked { get; set; } = isChecked;

    partial void OnIsCheckedChanged(bool value) => changed();
}

/// <summary>Räume und Geräte eines Standorts in „Qualifikationen, Räume und Geräte“.</summary>
public sealed record LocationResourceGroup(string Name, IReadOnlyList<CheckOption> Rooms, IReadOnlyList<CheckOption> Equipment)
{
    public bool HasEquipment => Equipment.Count > 0;
}

/// <summary>Abweichender Preis an einem Standort; leer heißt Grundpreis.</summary>
public sealed partial class LocationPriceRow(Guid locationId, string name, decimal? price, Action changed) : ObservableObject
{
    public Guid LocationId => locationId;

    public string Name => name;

    [ObservableProperty]
    public partial decimal? Price { get; set; } = price;

    partial void OnPriceChanged(decimal? value) => changed();
}

/// <summary>Person, die die Behandlung mit ihren Qualifikationen übernehmen kann („Wer kann das?“), mit Sprung in den Bereich Team.</summary>
public sealed record PersonEntry(Guid Id, string Name, IRelayCommand Open)
{
    /// <summary>Name für den Screenreader am Knopf.</summary>
    public string OpenName => $"{Name} im Team öffnen";
}

/// <summary>Zeile einer Teil-Liste der Behandlung; meldet jede Änderung der Maske und kann sich entfernen.</summary>
public abstract partial class EditorRow : ObservableObject
{
    private readonly Action _changed;
    private readonly Action<EditorRow> _remove;

    protected EditorRow(Action changed, Action<EditorRow> remove)
    {
        _changed = changed;
        _remove = remove;
    }

    /// <summary>Nur während des Aufbaus: Änderungen zählen dann nicht als ungespeichert.</summary>
    protected bool Building { get; set; } = true;

    [RelayCommand]
    private void Remove() => _remove(this);

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (!Building)
        {
            _changed();
        }
    }
}

/// <summary>Preis nach Wochentag und Uhrzeit (<c>PriceRules.tsx</c>).</summary>
public sealed partial class PriceRuleRow : EditorRow
{
    private static readonly DayOfWeek[] Week =
        [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday];

    public PriceRuleRow(TreatmentPriceRuleDto rule, IReadOnlyList<Choice> locations, Action changed, Action<EditorRow> remove) : base(changed, remove)
    {
        Locations = locations;
        Name = rule.Name;
        From = rule.From;
        Until = rule.Until;
        Price = rule.Price;
        Location = locations.FirstOrDefault(l => l.Id == rule.LocationId) ?? locations[0];
        Days = [.. Week.Select(d => new DayOption(d, rule.Days.Contains(d), changed))];
        Building = false;
    }

    public IReadOnlyList<Choice> Locations { get; }

    /// <summary>Die Auswahl des Standorts gibt es nur bei mehreren Standorten.</summary>
    public bool HasLocations => Locations.Count > 2;

    public IReadOnlyList<DayOption> Days { get; }

    [ObservableProperty]
    public partial string Name { get; set; }

    [ObservableProperty]
    public partial TimeOnly? From { get; set; }

    [ObservableProperty]
    public partial TimeOnly? Until { get; set; }

    [ObservableProperty]
    public partial decimal? Price { get; set; }

    [ObservableProperty]
    public partial Choice Location { get; set; }

    /// <summary>Ohne Preis, Beginn oder Ende ist die Regel unvollständig und wird nicht gespeichert.</summary>
    public bool IsComplete => Price is not null && From is not null && Until is not null;

    public TreatmentPriceRuleDto ToDto() =>
        new(Name, [.. Days.Where(d => d.IsChecked).Select(d => d.Day)], From ?? default, Until ?? default, Price ?? 0, Location.Id);
}

/// <summary>Vorbereitungsschritt (<c>PreparationSection.tsx</c>): Text, Minuten vorher und Material aus dem Lager.</summary>
public sealed partial class PreparationStepRow : EditorRow
{
    public PreparationStepRow(PreparationStepDto step, IReadOnlyList<Choice> stock, Action changed, Action<EditorRow> remove) : base(changed, remove)
    {
        Stock = stock;
        Text = step.Text;
        MinutesBefore = step.MinutesBefore;
        StockItem = stock.FirstOrDefault(s => s.Id == step.StockItemId) ?? stock[0];
        QuantityText = step.StockItemId is not null && step.Quantity is { } quantity
            ? $"{quantity.ToString("0.###", GermanNumbers.Format)} {step.Unit} je Termin laut Material"
            : null;
        Building = false;
    }

    public IReadOnlyList<Choice> Stock { get; }

    [ObservableProperty]
    public partial string Text { get; set; }

    [ObservableProperty]
    public partial decimal? MinutesBefore { get; set; }

    [ObservableProperty]
    public partial Choice StockItem { get; set; }

    /// <summary>Menge je Termin aus dem Material, wie gespeichert; ein anderer Artikel zeigt sie erst nach dem Speichern.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasQuantity))]
    public partial string? QuantityText { get; private set; }

    public bool HasQuantity => QuantityText is not null;

    partial void OnStockItemChanged(Choice value)
    {
        if (!Building)
        {
            QuantityText = null;
        }
    }

    public PreparationStepInput ToInput() => new(Text, MinutesBefore is { } minutes ? (int)minutes : null, StockItem.Id);
}

/// <summary>Material je Termin (<c>MaterialSection.tsx</c>): aus dem Lager (Name, Einheit und Preis von dort) oder frei erfasst.</summary>
public sealed partial class MaterialRow : EditorRow
{
    private readonly IReadOnlyList<StockItemDto> _items;

    public MaterialRow(MaterialLine line, IReadOnlyList<Choice> stock, IReadOnlyList<StockItemDto> items, Action changed, Action<EditorRow> remove)
        : base(changed, remove)
    {
        _items = items;
        Stock = stock;
        Name = line.Name;
        Unit = line.Unit;
        UnitCost = line.UnitCost;
        Quantity = line.Quantity;
        StockItem = stock.FirstOrDefault(s => s.Id == line.StockItemId) ?? stock[0];
        Building = false;
    }

    public IReadOnlyList<Choice> Stock { get; }

    /// <summary>Die Auswahl aus dem Lager gibt es nur, wenn das Lager Artikel hat.</summary>
    public bool HasStockChoice => Stock.Count > 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsFromStock), nameof(IsFree))]
    public partial Choice StockItem { get; set; }

    [ObservableProperty]
    public partial string Name { get; set; }

    [ObservableProperty]
    public partial string Unit { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UnitCostText))]
    public partial decimal? UnitCost { get; set; }

    [ObservableProperty]
    public partial decimal? Quantity { get; set; }

    public bool IsFromStock => StockItem.Id is not null;

    public bool IsFree => !IsFromStock;

    /// <summary>Preis je Einheit aus dem Lager, z. B. „0,04 €“.</summary>
    public string UnitCostText => $"{MoneyInputParser.Format(UnitCost ?? 0)} €";

    /// <summary>Material aus dem Lager: Bezeichnung, Einheit und Preis kommen von dort, abgeschlossene Behandlungen buchen ab.</summary>
    partial void OnStockItemChanged(Choice value)
    {
        if (Building || _items.FirstOrDefault(i => i.Id == value.Id) is not { } item)
        {
            return;
        }

        Name = item.Name;
        Unit = item.Unit;
        UnitCost = item.UnitCost;
    }

    public MaterialLine ToLine() => new(Name, Quantity ?? 0, Unit, UnitCost ?? 0, StockItem.Id);
}

/// <summary>„Zum Termin bereitstellen“ (<c>ExtrasSection.tsx</c>): Bezeichnung, Zeitpunkt und optional ein Lagerartikel mit Menge je Termin.</summary>
public sealed partial class ExtraRow : EditorRow
{
    public ExtraRow(TreatmentExtraDto extra, IReadOnlyList<Choice> stock, Action changed, Action<EditorRow> remove) : base(changed, remove)
    {
        Stock = stock;
        Name = extra.Name;
        Timing = TimingChoice.All.First(t => t.Timing == extra.Timing);
        MinutesBeforeEnd = extra.MinutesBeforeEnd;
        StockItem = stock.FirstOrDefault(s => s.Id == extra.StockItemId) ?? stock[0];
        Quantity = extra.StockItemId is null ? null : extra.Quantity;
        Building = false;
    }

    public IReadOnlyList<Choice> Stock { get; }

    public static IReadOnlyList<TimingChoice> Timings => TimingChoice.All;

    [ObservableProperty]
    public partial string Name { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBeforeEnd))]
    public partial TimingChoice Timing { get; set; }

    [ObservableProperty]
    public partial decimal? MinutesBeforeEnd { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStock))]
    public partial Choice StockItem { get; set; }

    [ObservableProperty]
    public partial decimal? Quantity { get; set; }

    public bool IsBeforeEnd => Timing.Timing == ExtraTiming.BeforeEnd;

    public bool HasStock => StockItem.Id is not null;

    public TreatmentExtraInput ToInput() => new(Name, Timing.Timing,
        IsBeforeEnd && MinutesBeforeEnd is { } minutes ? (int)minutes : null, StockItem.Id, HasStock ? Quantity ?? 0 : 0);
}

/// <summary>Ein Artikel in „Passt dazu“ (Slice 115); nur entfernen, die Reihenfolge ist die der Auswahl.</summary>
public sealed partial class RecommendedArticleRow(Guid id, string name, Action changed, Action<EditorRow> remove) : EditorRow(changed, remove)
{
    public Guid Id => id;

    public string Name => name;
}
