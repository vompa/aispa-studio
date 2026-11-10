using System.Collections.ObjectModel;
using System.Windows.Input;
using AispaStudio.Contracts;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>Eine Charge in den Aufgaben des Lagers.</summary>
public sealed record StockTaskRow(string Material, string BatchText, string PlaceName, string ExpiryText, string DueText, string QuantityText, string ArticleText)
{
    public string Summary => $"{Material}{(BatchText.Length == 0 ? "" : $" Charge {BatchText}")}, {QuantityText}, {PlaceName}, {DueText}";

    public string WriteOffName => $"{Material}{(BatchText.Length == 0 ? "" : $" Charge {BatchText}")} ausbuchen";

    public ICommand? WriteOffCommand { get; init; }

    public override string ToString() => Summary;
}

/// <summary>
/// Aufgaben des Lagers (R05 Slice 112, Ä251): „Bald ablaufend“ nach der Frist des Materials, die Abverkaufsliste daraus und „Abgelaufen“ zum Ausbuchen. Erledigt ist eine Aufgabe, wenn die Charge verbraucht oder
/// ausgebucht ist; einen Haken gibt es nicht. Nur mit ManageLocation; die Listen rechnet die API.
/// </summary>
public sealed partial class StockTasksViewModel : ViewModelBase
{
    private readonly StockViewModel _owner;

    internal StockTasksViewModel(StockViewModel owner) => _owner = owner;

    public ObservableCollection<StockTaskRow> Expiring { get; } = [];

    public ObservableCollection<StockTaskRow> SellOff { get; } = [];

    public ObservableCollection<StockTaskRow> Expired { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLoadError))]
    public partial string? LoadError { get; private set; }

    public bool HasLoadError => LoadError is not null;

    public bool HasExpiring => Expiring.Count > 0;

    public bool HasSellOff => SellOff.Count > 0;

    public bool HasExpired => Expired.Count > 0;

    public bool ShowEmpty => !HasLoadError && !HasExpiring && !HasExpired;

    /// <summary>Wie viele Chargen zu erledigen sind; der Reiter nennt die Zahl.</summary>
    public int OpenCount => Expiring.Count + Expired.Count;

    internal void Apply(StockExpiryTasksDto? tasks)
    {
        LoadError = tasks is null ? "Die Aufgaben wurden nicht geladen. Lade das Lager neu." : null;
        Fill(Expiring, tasks?.Expiring, false);
        Fill(SellOff, tasks?.SellOff, false);
        Fill(Expired, tasks?.Expired, true);
        foreach (var name in new[] { nameof(HasExpiring), nameof(HasSellOff), nameof(HasExpired), nameof(ShowEmpty), nameof(OpenCount) })
        {
            OnPropertyChanged(name);
        }
        UpdateDocument();
    }

    private void Fill(ObservableCollection<StockTaskRow> rows, IReadOnlyList<StockExpiryTaskDto>? tasks, bool writeOff)
    {
        rows.Clear();
        foreach (var task in tasks ?? [])
        {
            rows.Add(new StockTaskRow(task.ItemName, task.Batch ?? "", task.StoragePlaceName, StockFormat.Date(task.ExpiresOn), Due(task.DaysLeft),
                $"{StockFormat.Quantity(task.Quantity)} {task.Unit}", task.ArticleName is { } article ? $"{article}, {Money.Format(task.SalePrice ?? 0)}" : "")
            { WriteOffCommand = writeOff ? new AsyncRelayCommand(() => WriteOffAsync(task)) : null });
        }
    }

    private static string Due(int daysLeft) => daysLeft switch
    {
        0 => "läuft heute ab",
        1 => "noch 1 Tag",
        > 1 => $"noch {daysLeft} Tage",
        -1 => "seit 1 Tag abgelaufen",
        _ => $"seit {-daysLeft} Tagen abgelaufen",
    };

    private Task WriteOffAsync(StockExpiryTaskDto task) =>
        _owner.Overview.WriteOffAsync(new StockBatchDto(task.ItemId, task.ItemName, task.Unit, task.Batch, task.ExpiresOn, task.Quantity, default, true, true, task.StoragePlaceId, task.StoragePlaceName));
}
