using System.Collections.ObjectModel;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>Treffer unter „Schon vorhanden?“ in der Maske „Neue Kundin“; „Diese Kundin nehmen“ öffnet die vorhandene Karte.</summary>
/// <param name="reason">Warum der Server die Karte für dieselbe Person hält, z. B. „gleiche Telefonnummer“ (ADR 0061); null, wenn sie nur zur Suche passt.</param>
public sealed partial class CustomerMatchOption(CustomerDto customer, Func<CustomerDto, Task> take, string? reason = null)
{
    public string? Reason => reason;

    public bool HasReason => !string.IsNullOrWhiteSpace(reason);

    /// <summary>Wie in der Trefferliste links: „Huber, Anna“ (Rufname statt Vorname).</summary>
    public string Name => Converters.ListName(customer);

    public string? Phone => customer.Phone;

    [RelayCommand]
    private Task TakeAsync() => take(customer);
}

/// <summary>
/// „Schon vorhanden?“ beim Tippen von Nachname, Telefon oder Mailadresse: fragt die Kundensuche der API (dieselbe wie links in der Liste). Die
/// Telefonsuche nimmt die letzten sieben Ziffern, damit „0664 1234567“ auch „+43 664 1234567“ findet; die Mail zählt erst als ganze Adresse
/// (T-023). Ohne Verbindung gibt es keinen Hinweis; das Anlegen bleibt möglich.
/// </summary>
public sealed partial class NewCustomerMatchesViewModel(IWorkstationApi api, Func<CustomerDto, Task> take) : ViewModelBase
{
    private const int MinLastNameLength = 2;
    private const int PhoneTailLength = 7;
    private const int MaxOptions = 3;

    private int _version;

    public ObservableCollection<CustomerMatchOption> Options { get; } = [];

    public bool HasOptions => Options.Count > 0;

    /// <summary>Wartezeit nach dem letzten Tastendruck, damit nicht jeder Buchstabe eine Anfrage auslöst.</summary>
    public TimeSpan Delay { get; set; } = TimeSpan.FromMilliseconds(400);

    public async Task CheckAsync(string lastName, string phone, string email = "", string firstName = "")
    {
        var version = ++_version;
        var terms = SearchTerms(lastName, phone, email);
        if (terms.Count == 0)
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

        var found = new List<CustomerDto>();
        foreach (var term in terms)
        {
            var result = await api.GetAsync<List<CustomerDto>>($"api/customers?search={Uri.EscapeDataString(term)}").ConfigureAwait(true);
            if (version != _version)
            {
                return;
            }

            found.AddRange((result.Value ?? []).Where(c => found.All(f => f.Id != c.Id)).ToList());
        }

        // Der Grund kommt von der Erkennung des Servers (dieselbe wie im Kalender und im Posteingang): Karten mit Grund stehen vorn (L13).
        static string? Blank(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        var candidates = (await api.PostAsync<List<CustomerCandidateDto>>("api/customers/duplicates",
            new DuplicateCheckInput(Blank(firstName), Blank(lastName), Blank(email), Blank(phone))).ConfigureAwait(true)).Value ?? [];
        if (version != _version)
        {
            return;
        }

        foreach (var candidate in candidates.Where(c => found.All(f => f.Id != c.CustomerId)))
        {
            var card = await api.GetAsync<CustomerDto>($"api/customers/{candidate.CustomerId}").ConfigureAwait(true);
            if (version != _version)
            {
                return;
            }

            if (card.Value is { } customer)
            {
                found.Add(customer);
            }
        }

        var reasons = candidates.GroupBy(c => c.CustomerId).ToDictionary(g => g.Key, g => g.OrderBy(c => c.Stage).First());
        Options.Clear();
        foreach (var customer in found.OrderBy(c => reasons.TryGetValue(c.Id, out var known) ? known.Stage : int.MaxValue).Take(MaxOptions))
        {
            Options.Add(new CustomerMatchOption(customer, take, reasons.GetValueOrDefault(customer.Id)?.Reason));
        }

        OnPropertyChanged(nameof(HasOptions));
    }

    public void Clear()
    {
        _version++;
        Options.Clear();
        OnPropertyChanged(nameof(HasOptions));
    }

    private static List<string> SearchTerms(string lastName, string phone, string email)
    {
        var terms = new List<string>();
        if (lastName.Trim().Length >= MinLastNameLength)
        {
            terms.Add(lastName.Trim());
        }

        var digits = new string([.. phone.Where(char.IsAsciiDigit)]);
        if (digits.Length >= PhoneTailLength)
        {
            terms.Add(digits[^PhoneTailLength..]);
        }

        if (WholeAddress(email) is { } address)
        {
            terms.Add(address);
        }

        return terms;
    }

    /// <summary>
    /// Die Mail erst als ganze Adresse suchen („anna@huber.at“): Halbe Eingaben beim Tippen fänden zu viel, und ein Wort mit Punkt am Ende
    /// („anna@huber.“) gilt in der Kundensuche als Titel und fiele weg, die Suche liefe dann ohne Filter.
    /// </summary>
    private static string? WholeAddress(string email)
    {
        var address = email.Trim().ToLowerInvariant();
        var at = address.IndexOf('@', StringComparison.Ordinal);
        var looksWhole = at > 0
            && address.IndexOf('.', at + 1) > at + 1
            && !address.EndsWith('.')
            && !address.Any(char.IsWhiteSpace);
        return looksWhole ? address : null;
    }
}
