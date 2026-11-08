using System.Collections.ObjectModel;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>
/// Kundin suchen und wählen, für Gäste einer gemeinsamen Buchung und Eintritte. Nach der Wahl steht nur noch der Name da;
/// „Ändern“ öffnet die Suche wieder.
/// </summary>
public partial class CustomerSearchViewModel : ViewModelBase
{
    private readonly IWorkstationApi _api;
    private int _version;

    public CustomerSearchViewModel(IWorkstationApi api)
    {
        _api = api;
        Results.CollectionChanged += (_, _) => OnPropertyChanged(nameof(IsPicking));
    }

    public ObservableCollection<CustomerDto> Results { get; } = [];

    [ObservableProperty]
    public partial string Query { get; set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCustomer), nameof(IsPicking), nameof(Name))]
    public partial CustomerDto? Customer { get; set; }

    public bool HasCustomer => Customer is not null;

    /// <summary>Trefferliste nur, solange noch niemand gewählt ist.</summary>
    public bool IsPicking => Customer is null && Results.Count > 0;

    public string Name => Customer?.InHouseName ?? "";

    /// <summary>Wahl hat sich geändert.</summary>
    public event Action? Changed;

    partial void OnQueryChanged(string value) => _ = SearchAsync(value);

    partial void OnCustomerChanged(CustomerDto? value) => Changed?.Invoke();

    /// <summary>Sucht ab zwei Zeichen; eine ältere, langsamere Antwort überschreibt keine neuere.</summary>
    public async Task SearchAsync(string query)
    {
        var version = ++_version;
        if (query.Trim().Length < 2)
        {
            Results.Clear();
            return;
        }

        var result = await _api.GetAsync<List<CustomerDto>>($"api/customers?search={Uri.EscapeDataString(query.Trim())}").ConfigureAwait(true);
        if (version != _version || result.Value is null)
        {
            return;
        }

        Results.Clear();
        foreach (var customer in result.Value)
        {
            Results.Add(customer);
        }
    }

    [RelayCommand]
    private void Change()
    {
        Customer = null;
        Query = "";
    }
}
