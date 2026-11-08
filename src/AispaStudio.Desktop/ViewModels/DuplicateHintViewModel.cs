using System.Collections.ObjectModel;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>Möglicher Dublett-Treffer beim Anlegen eines Kunden; „Übernehmen“ nimmt den vorhandenen statt neu anzulegen.</summary>
public sealed partial class DuplicateOption(CustomerCandidateDto candidate, Func<Guid, Task> adopt)
{
    public string Name => candidate.Name;

    public string Reason => candidate.Reason;

    [RelayCommand]
    private Task AdoptAsync() => adopt(candidate.CustomerId);
}

/// <summary>
/// „Gibt es schon?“ beim Anlegen eines neuen Kunden: fragt den Server nach Treffern zu Name, Telefon und Mailadresse (dieselbe Erkennung wie
/// bei eingehenden Nachrichten, ADR 0061). Ohne Verbindung gibt es keinen Hinweis; das Anlegen bleibt möglich.
/// </summary>
public sealed partial class DuplicateHintViewModel(IWorkstationApi api, Func<Guid, Task> adopt) : ViewModelBase
{
    private int _version;

    public ObservableCollection<DuplicateOption> Options { get; } = [];

    public bool HasOptions => Options.Count > 0;

    /// <summary>Wartezeit nach dem letzten Tastendruck, damit nicht jeder Buchstabe eine Anfrage auslöst.</summary>
    public TimeSpan Delay { get; set; } = TimeSpan.FromMilliseconds(400);

    public async Task CheckAsync(string firstName, string lastName, string email, string phone)
    {
        var version = ++_version;
        var enough = lastName.Trim().Length >= 2 || phone.Trim().Length >= 6 || email.Contains('@', StringComparison.Ordinal);
        if (!enough)
        {
            Clear();
            return;
        }

        if (Delay > TimeSpan.Zero)
        {
            await Task.Delay(Delay).ConfigureAwait(true);
            if (version != _version)
            {
                return;
            }
        }

        static string? Blank(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        var result = await api.PostAsync<List<CustomerCandidateDto>>("api/customers/duplicates",
            new DuplicateCheckInput(Blank(firstName), Blank(lastName), Blank(email), Blank(phone))).ConfigureAwait(true);
        if (version != _version)
        {
            return;
        }

        Options.Clear();
        foreach (var candidate in result.Value ?? [])
        {
            Options.Add(new DuplicateOption(candidate, adopt));
        }

        OnPropertyChanged(nameof(HasOptions));
    }

    public void Clear()
    {
        Options.Clear();
        OnPropertyChanged(nameof(HasOptions));
    }
}
