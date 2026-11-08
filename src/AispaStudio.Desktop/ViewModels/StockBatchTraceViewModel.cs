using System.Collections.ObjectModel;
using System.Globalization;
using AispaStudio.Contracts;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>Material in der Auswahl der Rückverfolgung.</summary>
public sealed record TraceItemChoice(Guid Id, string Name)
{
    public override string ToString() => Name;
}

/// <summary>Eine Buchung zur Charge.</summary>
public sealed record BatchUsageRow(string AtText, string Location, string KindText, string QuantityText, string Customer, string Detail)
{
    public string Summary => string.Join(", ", new[] { AtText, Location, KindText, QuantityText, Customer, Detail }.Where(p => p.Length > 0));

    public override string ToString() => Summary;
}

/// <summary>
/// Chargen-Rückverfolgung (R05 Slice 123, Ä223) nach <c>BatchTrace.tsx</c>: bei einem Rückruf zeigt sie über alle Standorte, wer Material dieser Charge in einer Behandlung oder beim Kauf bekommen hat.
/// Nur mit ManageLocation, wie im Web; zusammengestellt wird in der API.
/// </summary>
public sealed partial class StockBatchTraceViewModel : ViewModelBase
{
    private readonly StockViewModel _owner;

    internal StockBatchTraceViewModel(StockViewModel owner) => _owner = owner;

    public ObservableCollection<TraceItemChoice> Items { get; } = [];

    public ObservableCollection<BatchUsageRow> Rows { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(TraceCommand))]
    public partial TraceItemChoice? SelectedItem { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(TraceCommand))]
    public partial string Batch { get; set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? Error { get; private set; }

    public bool HasError => Error is not null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasResult))]
    public partial string? ResultText { get; private set; }

    public bool HasResult => ResultText is not null;

    public bool HasRows => Rows.Count > 0;

    public bool ShowNothingBooked => HasResult && !HasRows;

    private bool CanTrace => SelectedItem is not null && Batch.Trim().Length > 0;

    /// <summary>Übernimmt das Material des Lagers; die gewählte Zeile bleibt, solange es sie noch gibt.</summary>
    internal void RebuildItems(IReadOnlyList<StockItemDto> items)
    {
        var keep = SelectedItem?.Id;
        Items.Clear();
        foreach (var item in items)
        {
            Items.Add(new TraceItemChoice(item.Id, item.Name));
        }

        SelectedItem = Items.FirstOrDefault(i => i.Id == keep);
    }

    [RelayCommand(CanExecute = nameof(CanTrace))]
    private async Task TraceAsync()
    {
        var result = await _owner.Api.GetAsync<StockBatchUsageDto>($"api/stock/batch-usage?itemId={SelectedItem!.Id}&batch={Uri.EscapeDataString(Batch.Trim())}").ConfigureAwait(true);
        Error = result.Value is null ? result.Error ?? "Das Material wurde nicht gefunden." : null;
        Show(result.Value);
    }

    private void Show(StockBatchUsageDto? usage)
    {
        Rows.Clear();
        foreach (var entry in usage?.Entries ?? [])
        {
            Rows.Add(new BatchUsageRow(entry.At.ToLocalTime().ToString("dd.MM.yy HH:mm", CultureInfo.InvariantCulture), entry.LocationName,
                entry.Kind == StockMovementKind.Consumption ? "Behandlung" : StockFormat.Kind(entry.Kind), StockFormat.Quantity(entry.Quantity), entry.CustomerName ?? "", entry.Detail ?? ""));
        }

        ResultText = usage is null ? null : Result(usage);
        OnPropertyChanged(nameof(HasRows));
        OnPropertyChanged(nameof(ShowNothingBooked));
    }

    private static string Result(StockBatchUsageDto usage)
    {
        var who = usage.Customers == 1 ? "1 Kundin oder Kunde" : $"{usage.Customers} Kundinnen und Kunden";
        return $"{usage.ItemName}, Charge {usage.Batch}: {StockFormat.Quantity(usage.Received)} {usage.Unit} erhalten, {StockFormat.Quantity(usage.Used)} {usage.Unit} verwendet oder verkauft, {who}.";
    }
}
