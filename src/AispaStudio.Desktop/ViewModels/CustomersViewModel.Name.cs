using AispaStudio.Contracts;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>Anrede zur Auswahl in der Kundenkarte.</summary>
public sealed record SalutationChoice(CustomerSalutation Value, string Label)
{
    public static IReadOnlyList<SalutationChoice> All { get; } =
        [.. new[] { CustomerSalutation.None, CustomerSalutation.Female, CustomerSalutation.Male, CustomerSalutation.Diverse }
            .Select(s => new SalutationChoice(s, PersonNames.Label(s)))];

    public override string ToString() => Label;
}

/// <summary>„Titel in der Anrede“: nach dem Land des Standorts (null), immer oder nie.</summary>
public sealed record GreetingTitleChoice(bool? Value, string Label)
{
    public static IReadOnlyList<GreetingTitleChoice> All { get; } =
        [new(null, "nach Land"), new(true, "ja"), new(false, "nein")];

    public override string ToString() => Label;
}

/// <summary>
/// Anrede, Rufname, Titel und Postanschrift in der Kundenkarte (ADR 0064): der Kopf zeigt den Namen im Haus groß und darunter die
/// Adresszeile mit dem gültigen Namen, dazu eine Vorschau der Anrede in Nachrichten je Land der Standorte.
/// </summary>
public partial class CustomersViewModel
{
    private IReadOnlyList<FiscalCountry> _countries = [FiscalCountry.AT];

    /// <summary>Länder der Standorte des Spas; ohne Verbindung bleibt es bei Österreich.</summary>
    private async Task LoadCountriesAsync()
    {
        var locations = (await api.GetAsync<List<LocationDto>>("api/locations").ConfigureAwait(true)).Value;
        if (locations is { Count: > 0 })
        {
            _countries = [.. locations.Select(l => PersonNames.CountryOfTimeZone(l.TimeZone)).Distinct().Order()];
            OnPropertyChanged(nameof(SelectedGreeting));
        }
    }

    public string SelectedSymbol => Selected is { } c ? PersonNames.Symbol(c.Salutation) ?? "" : "";

    /// <summary>„(sie/ihr)“ hinter dem Namen im Kopf, wenn eingetragen.</summary>
    public string SelectedPronouns => Selected?.Pronouns is { Length: > 0 } p ? $"({p})" : "";

    public string? SelectedSymbolIcon => Controls.Icons.Salutation(SelectedSymbol);

    /// <summary>„Frau Mag. Stefanie Huber, MSc“, nur wenn es mehr sagt als der Name im Kopf.</summary>
    public string? SelectedAddressLine => Selected is { } c && AddressLine(c) is var line && line != c.InHouseName ? line : null;

    public string? SelectedPostalAddress => Selected?.Address is { } a ? PostalText(a) : null;

    /// <summary>„Anrede: Guten Tag Frau Mag. Huber“; bei Standorten in mehreren Ländern je Land.</summary>
    public string? SelectedGreeting => Selected is { } c
        ? GreetingText(c.Salutation, c.TitlesBefore, c.FirstName, c.LastName, c.TitleInGreeting)
        : null;

    // ---------- Neu anlegen: Anrede, Rufname und vorangestellte Titel gleich mit ----------

    [ObservableProperty]
    public partial SalutationChoice NewSalutation { get; set; } = SalutationChoice.All[0];

    [ObservableProperty]
    public partial string NewPreferredName { get; set; } = "";

    [ObservableProperty]
    public partial string NewTitlesBefore { get; set; } = "";

    private void ClearNew()
    {
        NewFirstName = NewLastName = NewPhone = NewEmail = NewNote = NewPreferredName = NewTitlesBefore = "";
        NewSalutation = SalutationChoice.All[0];
        NewMoreOpen = false;
    }

    // ---------- Texte ----------

    private static string AddressLine(CustomerDto c) => PersonNames.AddressLine(c.Salutation, c.TitlesBefore, c.FirstName, c.LastName, c.TitlesAfter);

    /// <summary>„Hauptstraße 1, 1010 Wien, AT“; leere Teile fallen weg.</summary>
    public static string? PostalText(CustomerAddress a)
    {
        var city = string.Join(" ", new[] { a.PostalCode, a.City }.Where(p => !string.IsNullOrWhiteSpace(p)));
        var text = string.Join(", ", new[] { a.Street, city, a.Country }.Where(p => !string.IsNullOrWhiteSpace(p)));
        return text.Length == 0 ? null : text;
    }

    private string GreetingText(CustomerSalutation salutation, string? titles, string first, string last, bool? titleInGreeting) =>
        CustomerContactFormViewModel.GreetingText(_countries, salutation, titles, first, last, titleInGreeting);
}
