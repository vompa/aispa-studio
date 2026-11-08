using System.Collections.ObjectModel;
using System.Windows.Input;
using AispaStudio.Contracts;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>Ein Punkt in „Was ist zu tun“; der Link führt in den Reiter, in dem man ihn erledigt (ohne das Recht dafür gibt es keinen Link).</summary>
public sealed record StockTodoItem(string Title, string Detail, string? LinkText, ICommand? LinkCommand)
{
    public bool HasLink => LinkCommand is not null;

    public bool HasDetail => Detail.Length > 0;

    /// <summary>Der Name für Screenreader: Punkt mit Einzelheiten.</summary>
    public string Summary => HasDetail ? $"{Title}: {Detail}" : Title;

    public override string ToString() => Summary;
}

/// <summary>
/// Reiter „Übersicht“ des Lagers (R07 Slice 143, Ä351): „Was ist zu tun“ aus dem Stand, den die Maske ohnehin geladen hat: nachbestellen (Zahl der Positionen im Bestellvorschlag),
/// Material unter dem Mindestbestand oder nur noch für wenige Behandlungen, ablaufende Chargen; darunter klein der Lagerwert. Nichts wird hier gerechnet, was die API nicht schon liefert.
/// Was ein Recht verlangt, erscheint ohne das Recht nicht: der Bestellvorschlag nur mit ManageLocation, der Link zu den Chargen ebenso.
/// </summary>
public sealed partial class StockTodoViewModel : ViewModelBase
{
    private const int NamesShown = 3;

    private readonly StockViewModel _owner;

    internal StockTodoViewModel(StockViewModel owner) => _owner = owner;

    public ObservableCollection<StockTodoItem> Items { get; } = [];

    /// <summary>Nichts zu tun, und alles ist geladen; ohne Chargen oder Bestellvorschlag stünde „Nichts zu tun“ zu Unrecht da.</summary>
    public bool IsEmpty => Items.Count == 0 && !HasGaps;

    /// <summary>Die Chargen oder der Bestellvorschlag sind nicht geladen; die Übersicht sagt es, statt Ordnung zu melden.</summary>
    public bool HasGaps => _owner.BatchData is null || (_owner.CanManage && _owner.Orders.HasLoadError);

    /// <summary>„Lagerwert 987,50 € am Standort“; der Wert des ganzen Standorts, unabhängig vom Lagerort-Filter des Bestands.</summary>
    [ObservableProperty]
    public partial string ValueText { get; private set; } = "";

    /// <summary>Baut die Punkte aus dem Stand des Besitzers neu auf (nach jedem Laden).</summary>
    internal void Refresh()
    {
        Items.Clear();
        var overview = _owner.OverviewData;
        ValueText = overview is null ? "" : $"Lagerwert {Money.Format(overview.TotalValue)} am Standort";
        AddReorder();
        AddBelowMinimum(overview);
        AddFewTreatments(overview);
        AddExpiring();
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(HasGaps));
    }

    /// <summary>Die Punkte zählen den ganzen Standort; ein vorher gewählter Lagerort und eine Suche im Bestand würden den Link ins Leere führen.</summary>
    private void ShowWholeSite()
    {
        _owner.ScanText = "";
        _owner.SelectedPlace = _owner.PlaceFilter[0];
    }

    private void AddReorder()
    {
        var suggestions = _owner.Orders.Suggestions;
        if (!_owner.CanManage || suggestions.Count == 0)
        {
            return;
        }

        var title = suggestions.Count == 1 ? "1 Position nachbestellen" : $"{suggestions.Count} Positionen nachbestellen";
        Items.Add(new StockTodoItem(title, Names(suggestions.Select(s => s.Material).ToList()), "Bestellen öffnen", new RelayCommand(() => _owner.SetTab(StockTab.Orders))));
    }

    private void AddBelowMinimum(StockOverviewDto? overview)
    {
        var below = (overview?.Items ?? []).Where(i => i.BelowMinimum).Select(i => i.Name).ToList();
        if (below.Count == 0)
        {
            return;
        }

        var title = below.Count == 1 ? "1 Material unter dem Mindestbestand" : $"{below.Count} Materialien unter dem Mindestbestand";
        Items.Add(new StockTodoItem(title, Names(below), "Im Bestand ansehen", new RelayCommand(() =>
        {
            ShowWholeSite();
            _owner.SetTab(StockTab.Stock);
            _owner.Overview.OnlyBelow = true;
        })));
    }

    private void AddFewTreatments(StockOverviewDto? overview)
    {
        var few = (overview?.Items ?? []).Where(i => StockOverviewViewModel.IsFew(i.TreatmentsCovered)).OrderBy(i => i.TreatmentsCovered).ThenBy(i => i.Name, StringComparer.CurrentCulture).ToList();
        if (few.Count == 0)
        {
            return;
        }

        var title = few.Count == 1 ? "1 Material reicht nur noch für wenige Behandlungen" : $"{few.Count} Materialien reichen nur noch für wenige Behandlungen";
        Items.Add(new StockTodoItem(title, Names(few.Select(i => $"{i.Name} ({i.TreatmentsCovered})").ToList()), "Im Bestand ansehen", new RelayCommand(() =>
        {
            ShowWholeSite();
            _owner.SetTab(StockTab.Stock);
        })));
    }

    private void AddExpiring()
    {
        var batches = (_owner.BatchData?.Batches ?? []).Where(b => b.ExpiresSoon).OrderBy(b => b.ExpiresOn).ToList();
        if (batches.Count == 0)
        {
            return;
        }

        var expired = batches.Count(b => b.Expired);
        var title = expired == 0
            ? (batches.Count == 1 ? "1 Charge läuft bald ab" : $"{batches.Count} Chargen laufen bald ab")
            : (batches.Count == 1 ? "1 Charge ist abgelaufen" : $"{batches.Count} Chargen laufen ab oder sind abgelaufen");
        var first = batches[0];
        var detail = $"{first.ItemName}{(first.Batch is null ? "" : $", Charge {first.Batch}")}, {StockFormat.Date(first.ExpiresOn)}{(batches.Count > 1 ? $" und {batches.Count - 1} weitere" : "")}";
        ICommand? link = _owner.CanManage ? new RelayCommand(() => _owner.SetTab(StockTab.Tasks)) : null;
        Items.Add(new StockTodoItem(title, detail, link is null ? null : "Chargen öffnen", link));
    }

    private static string Names(List<string> names) =>
        names.Count <= NamesShown ? string.Join(", ", names) : $"{string.Join(", ", names.Take(NamesShown))} und {names.Count - NamesShown} weitere";
}
