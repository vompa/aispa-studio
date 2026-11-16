using System.Collections.ObjectModel;
using System.Windows.Input;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Controls;
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

/// <summary>Eine Kennzahl-Kachel der Übersicht (Design-Brief 5.5); mit Befehl führt sie an den Ort, an dem man die Zahl bearbeitet.</summary>
public sealed record StockStat(string Label, string Value, string? Hint, StatTileState State, ICommand? Command)
{
    public bool HasCommand => Command is not null;
}

/// <summary>
/// Reiter „Übersicht“ des Lagers (R07 Slice 143, Ä351; Kacheln seit ADR 0098): oben die Kennzahlen als Kacheln (Lagerwert, nachbestellen, unter dem Mindestbestand, ablaufende Chargen, mit ViewFinance der
/// Mehrverbrauch), darunter „Was ist zu tun“ mit den Einzelheiten, alles aus dem Stand, den die Maske ohnehin geladen hat. Nichts wird hier gerechnet, was die API nicht schon liefert.
/// Was ein Recht verlangt, erscheint ohne das Recht nicht: der Bestellvorschlag nur mit ManageLocation, der Sprung zu den Chargen ebenso.
/// </summary>
public sealed partial class StockTodoViewModel : ViewModelBase
{
    private const int NamesShown = 3;

    private readonly StockViewModel _owner;

    internal StockTodoViewModel(StockViewModel owner) => _owner = owner;

    public ObservableCollection<StockTodoItem> Items { get; } = [];

    /// <summary>Die Kennzahl-Kacheln über „Was ist zu tun“; der Lagerwert gilt für den ganzen Standort, unabhängig vom Lagerort-Filter des Bestands.</summary>
    public ObservableCollection<StockStat> Stats { get; } = [];

    /// <summary>Nichts zu tun, und alles ist geladen; ohne Chargen oder Bestellvorschlag stünde „Nichts zu tun“ zu Unrecht da.</summary>
    public bool IsEmpty => Items.Count == 0 && !HasGaps;

    /// <summary>Die Chargen oder der Bestellvorschlag sind nicht geladen; die Übersicht sagt es, statt Ordnung zu melden.</summary>
    public bool HasGaps => _owner.BatchData is null || (_owner.CanManage && _owner.Orders.HasLoadError);

    /// <summary>Baut Kacheln und Punkte aus dem Stand des Besitzers neu auf (nach jedem Laden).</summary>
    internal void Refresh()
    {
        Items.Clear();
        var overview = _owner.OverviewData;
        AddReorder();
        AddBelowMinimum(overview);
        AddFewTreatments(overview);
        AddExpiring();
        RefreshStats();
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(HasGaps));
    }

    /// <summary>Baut nur die Kacheln neu auf; der Verbrauch lädt nach dem Bestand und meldet sich hier.</summary>
    internal void RefreshStats()
    {
        Stats.Clear();
        var overview = _owner.OverviewData;
        if (overview is not null)
        {
            Stats.Add(new StockStat("Lagerwert", Money.Format(overview.TotalValue), "am Standort", StatTileState.None, null));
        }

        if (_owner.CanManage && !_owner.Orders.HasLoadError)
        {
            Stats.Add(Count("Nachbestellen", _owner.Orders.Suggestions.Count, "im Bestellvorschlag", new RelayCommand(() => _owner.SetTab(StockTab.Orders))));
        }

        if (overview is not null)
        {
            Stats.Add(Count("Unter Minimum", overview.Items.Count(i => i.BelowMinimum), null, new RelayCommand(ShowBelowMinimum)));
        }

        if (_owner.BatchData is { } batches)
        {
            Stats.Add(Count("Chargen laufen ab", batches.Batches.Count(b => b.ExpiresSoon), null, _owner.CanManage ? new RelayCommand(() => _owner.SetTab(StockTab.Tasks)) : null));
        }

        if (_owner.CanViewUsage && _owner.Usage.ExcessValueText is { } excess)
        {
            Stats.Add(new StockStat("Mehrverbrauch", excess, _owner.Usage.SelectedPeriod.Label, StatTileState.None, new RelayCommand(() => _owner.SetTab(StockTab.Usage))));
        }
    }

    /// <summary>Eine Zahl, die etwas zu tun bedeutet: ab 1 mit dem Warnzeichen, bei 0 ohne Zeichen (kein „ok“-Zeichen ohne Anlass, Design-Brief 5.8).</summary>
    private static StockStat Count(string label, int count, string? hint, ICommand? command) =>
        new(label, count.ToString(System.Globalization.CultureInfo.CurrentCulture), hint, count > 0 ? StatTileState.Warn : StatTileState.None, command);

    /// <summary>Die Punkte zählen den ganzen Standort; ein vorher gewählter Lagerort und eine Suche im Bestand würden den Link ins Leere führen.</summary>
    private void ShowWholeSite()
    {
        _owner.ScanText = "";
        _owner.SelectedPlace = _owner.PlaceFilter[0];
    }

    private void ShowBelowMinimum()
    {
        ShowWholeSite();
        _owner.SetTab(StockTab.Stock);
        _owner.Overview.OnlyBelow = true;
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
        Items.Add(new StockTodoItem(title, Names(below), "Im Bestand ansehen", new RelayCommand(ShowBelowMinimum)));
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
