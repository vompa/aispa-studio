using System.Globalization;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>Eine Person in „Wer darf was?“.</summary>
public sealed record PowerHolderRow(string Text, bool IsExpired);

/// <summary>Eine Vollmacht in „Wer darf was?“ mit den Personen, die sie haben.</summary>
public sealed record PowerOverviewRow(string Label, string Effect, IReadOnlyList<PowerHolderRow> Holders)
{
    public bool HasNoHolders => Holders.Count == 0;
}

/// <summary>Übersicht „Wer darf was?“ im Bereich Team (R05 Slice 111, E11): je Vollmacht wer und bis wann; abgelaufene ausgegraut. Nur lesen, gepflegt wird auf der Personenkarte.</summary>
public sealed partial class PowerOverviewViewModel(IWorkstationApi api) : ViewModelBase
{
    [ObservableProperty]
    public partial IReadOnlyList<PowerOverviewRow> Rows { get; private set; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLoadError))]
    public partial string? LoadError { get; private set; }

    public bool HasLoadError => LoadError is not null;

    public Task EnsureLoadedAsync() => LoadAsync();

    [RelayCommand]
    public async Task LoadAsync()
    {
        LoadError = null;
        var result = await api.GetAsync<List<PowerOverviewRowDto>>("api/staff-regulations/powers").ConfigureAwait(true);
        if (result.Value is not { } rows)
        {
            LoadError = result.Error ?? "Die Übersicht wurde nicht geladen.";
            return;
        }

        Rows = [.. rows.Select(ToRow)];
    }

    private static PowerOverviewRow ToRow(PowerOverviewRowDto row) => new(
        row.Label,
        row.IsChecked ? "Prüft die API zusätzlich zur Rolle" : "Nur zur Anzeige",
        [.. row.Holders.Select(h => new PowerHolderRow($"{h.Name}, {Until(h)}, erteilt von {h.GrantedBy}", h.IsExpired))]);

    private static string Until(PowerHolderDto holder)
    {
        if (holder.ValidUntil is not { } until)
        {
            return "ohne Ende";
        }

        var day = until.ToString("d.M.yyyy", CultureInfo.InvariantCulture);
        return holder.IsExpired ? $"abgelaufen am {day}" : $"bis {day}";
    }
}
