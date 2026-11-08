using System.Globalization;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Controls;
using AispaStudio.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>
/// Stammdaten eines Gastes bearbeiten: Anrede, Name, Rufname, Pronomen, Titel, Postanschrift, Telefon, E-Mail, Geburtstag, Sprache und Notiz
/// (ADR 0064). Die Kundenkarte und das Seitenpanel des Kalenders benutzen dasselbe Formular und dieselbe Prüfung. Aufbau nach
/// docs/gestaltung/masken.md, Abschnitt 7: Gruppen Name, Kontakt, Anschrift, dazu eingeklappt „Weitere Angaben“.
/// </summary>
public sealed partial class CustomerContactFormViewModel : ViewModelBase
{
    /// <summary>Namen der Felder für <see cref="FocusRequested"/>: das erste ungültige Feld bekommt nach dem Speichern den Fokus.</summary>
    public const string EmailField = "Email";

    public const string BirthDateField = "BirthDate";

    public const string PostalCodeField = "PostalCode";

    public const string CountryField = "Country";

    private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-AT");

    /// <summary>Ein Geburtstag braucht das Jahr und liegt nie in der Zukunft; ein zweistelliges Jahr gehört ins vorige Jahrhundert, wenn es sonst später wäre.</summary>
    public static readonly DateRules BirthDateRules = new(RequireYear: true, NotInFuture: true);

    private readonly IWorkstationApi _api;
    private readonly IReadOnlyList<FiscalCountry> _countries;
    private readonly Func<CustomerDto, Task> _saved;
    private readonly Action _cancel;

    /// <summary>Für den heutigen Tag bei Geburtstagen (zweistelliges Jahr, nicht in der Zukunft); Tests setzen einen festen.</summary>
    public TimeProvider Time { get; init; } = TimeProvider.System;

    /// <param name="countries">Länder der Standorte, für die Vorschau der Anrede.</param>
    /// <param name="saved">Der Gast wurde gespeichert; bekommt den neuen Stand.</param>
    /// <param name="cancel">Abbrechen.</param>
    public CustomerContactFormViewModel(IWorkstationApi api, CustomerDto customer, IReadOnlyList<FiscalCountry> countries, Func<CustomerDto, Task> saved, Action cancel)
    {
        _api = api;
        Customer = customer;
        _countries = countries;
        _saved = saved;
        _cancel = cancel;
        FirstName = customer.FirstName;
        LastName = customer.LastName;
        Phone = customer.Phone ?? "";
        Email = customer.Email ?? "";
        BirthDate = customer.BirthDate?.ToString("dd.MM.yyyy", German) ?? "";
        Note = customer.Note ?? "";
        Language = AllLanguages.FirstOrDefault(l => l.Code == customer.Language) ?? AllLanguages[0];
        Salutation = SalutationChoice.All.First(s => s.Value == customer.Salutation);
        PreferredName = customer.PreferredName ?? "";
        Pronouns = customer.Pronouns ?? "";
        TitlesBefore = customer.TitlesBefore ?? "";
        TitlesAfter = customer.TitlesAfter ?? "";
        TitleInGreeting = GreetingTitleChoice.All.First(g => g.Value == customer.TitleInGreeting);
        Street = customer.Address?.Street ?? "";
        PostalCode = customer.Address?.PostalCode ?? "";
        City = customer.Address?.City ?? "";
        Country = customer.Address?.Country ?? "";
        // Seltene Felder bleiben zu, solange alle leer sind; ist eines befüllt, geht der Bereich beim Öffnen auf (masken.md, R2).
        MoreOpen = HasMore;
        _atOpen = Signature();
    }

    /// <summary>Alle Felder beim Öffnen; was davon abweicht, ist eine ungespeicherte Änderung (Design-Brief 5.27, „Änderungen verwerfen?“).</summary>
    private readonly string _atOpen;

    /// <summary>Ein Feld unterscheidet sich vom Gast beim Öffnen des Formulars.</summary>
    public bool HasChanges => Signature() != _atOpen;

    private string Signature() => string.Join('\u001f', FirstName, LastName, Phone, Email, BirthDate, Note, Language.Code, Salutation.Value, PreferredName, Pronouns,
        TitlesBefore, TitlesAfter, TitleInGreeting.Value, Street, PostalCode, City, Country);

    /// <summary>Der Gast mit dem Stand beim Öffnen des Formulars.</summary>
    public CustomerDto Customer { get; }

    /// <summary>Das erste ungültige Feld soll den Fokus bekommen (Name des Feldes, z. B. <see cref="PostalCodeField"/>).</summary>
    public event Action<string>? FocusRequested;

    /// <summary>Sprache des Gastes; Deutsch steht zuerst und wird nicht gespeichert.</summary>
    public static IReadOnlyList<LanguageChoice> AllLanguages { get; } =
        [new LanguageChoice(null, "Deutsch"), .. GuestLanguages.All.Select(l => new LanguageChoice(l.Code, l.Name))];

    /// <summary>Titel zur Auswahl; die Vorschlagsliste hängt den gewählten Titel an, frei Eingetipptes bleibt erlaubt.</summary>
    public static IReadOnlyList<string> TitlesBeforeChoices => PersonNames.TitlesBefore;

    public static IReadOnlyList<string> TitlesAfterChoices => PersonNames.TitlesAfter;

    /// <summary>Überschrift des eingeklappten Bereichs: was darin steht.</summary>
    public static string MoreSummary => "Rufname, Pronomen, Titel, Notiz";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    [NotifyPropertyChangedFor(nameof(AddressPreview), nameof(GreetingPreview), nameof(ShowPreview))]
    public partial string FirstName { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    [NotifyPropertyChangedFor(nameof(AddressPreview), nameof(GreetingPreview), nameof(ShowPreview))]
    public partial string LastName { get; set; }

    [ObservableProperty]
    public partial string Phone { get; set; }

    [ObservableProperty]
    public partial string Email { get; set; }

    /// <summary>Geburtstag als „TT.MM.JJJJ“; leer löscht ihn.</summary>
    [ObservableProperty]
    public partial string BirthDate { get; set; }

    [ObservableProperty]
    public partial string Note { get; set; }

    [ObservableProperty]
    public partial LanguageChoice Language { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AddressPreview), nameof(GreetingPreview))]
    public partial SalutationChoice Salutation { get; set; }

    [ObservableProperty]
    public partial string PreferredName { get; set; }

    /// <summary>Pronomen, wenn der Gast sie nennt; nur für das Team.</summary>
    [ObservableProperty]
    public partial string Pronouns { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AddressPreview), nameof(GreetingPreview))]
    public partial string TitlesBefore { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AddressPreview))]
    public partial string TitlesAfter { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(GreetingPreview))]
    public partial GreetingTitleChoice TitleInGreeting { get; set; }

    [ObservableProperty]
    public partial string Street { get; set; }

    [ObservableProperty]
    public partial string PostalCode { get; set; }

    [ObservableProperty]
    public partial string City { get; set; }

    /// <summary>Land der Postanschrift als Kürzel („AT“).</summary>
    [ObservableProperty]
    public partial string Country { get; set; }

    /// <summary>„Weitere Angaben“ ist aufgeklappt.</summary>
    [ObservableProperty]
    public partial bool MoreOpen { get; set; }

    /// <summary>Maskenweiter Fehler (Server nicht erreichbar, Speichern abgelehnt): steht als Meldung über der Aktionsleiste.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? Error { get; private set; }

    public bool HasError => !string.IsNullOrEmpty(Error);

    /// <summary>Fehler am Feld (masken.md, R8): ersetzt den Hilfetext unter dem Feld und färbt den Rand.</summary>
    [ObservableProperty]
    public partial string? EmailError { get; private set; }

    [ObservableProperty]
    public partial string? BirthDateError { get; private set; }

    [ObservableProperty]
    public partial string? PostalCodeError { get; private set; }

    [ObservableProperty]
    public partial string? CountryError { get; private set; }

    /// <summary>Ein seltenes Feld ist befüllt: der Bereich „Weitere Angaben“ ist beim Öffnen schon auf.</summary>
    private bool HasMore => new[] { PreferredName, Pronouns, TitlesBefore, TitlesAfter, Note }.Any(v => !string.IsNullOrWhiteSpace(v)) || TitleInGreeting.Value is not null;

    /// <summary>Die Vorschau erscheint erst, wenn ein Name da ist.</summary>
    public bool ShowPreview => FirstName.Trim().Length > 0 || LastName.Trim().Length > 0;

    public string AddressPreview => PersonNames.AddressLine(Salutation.Value, TitlesBefore, FirstName.Trim(), LastName.Trim(), TitlesAfter);

    public string GreetingPreview => GreetingText(_countries, Salutation.Value, TitlesBefore, FirstName.Trim(), LastName.Trim(), TitleInGreeting.Value);

    partial void OnEmailChanged(string value) => EmailError = null;

    partial void OnBirthDateChanged(string value) => BirthDateError = null;

    partial void OnPostalCodeChanged(string value) => PostalCodeError = null;

    partial void OnCountryChanged(string value) => PostalCodeError = CountryError = null;

    /// <summary>„Anrede: Guten Tag Frau Mag. Huber“; bei Standorten in mehreren Ländern je Land.</summary>
    public static string GreetingText(IReadOnlyList<FiscalCountry> countries, CustomerSalutation salutation, string? titles, string first, string last, bool? titleInGreeting) =>
        countries.Count == 1
            ? $"Anrede: {PersonNames.Greeting(salutation, titles, first, last, countries[0], titleInGreeting)}"
            : string.Join("\n", countries.Select(country => $"Anrede in {country}: {PersonNames.Greeting(salutation, titles, first, last, country, titleInGreeting)}"));

    [RelayCommand]
    private void AddTitleBefore(string title) => TitlesBefore = AppendTitle(TitlesBefore, title, after: false);

    [RelayCommand]
    private void AddTitleAfter(string title) => TitlesAfter = AppendTitle(TitlesAfter, title, after: true);

    /// <summary>Hängt einen Titel aus der Auswahl an, ohne ihn doppelt einzutragen.</summary>
    private static string AppendTitle(string titles, string title, bool after)
    {
        var list = PersonNames.SplitTitles(titles);
        return list.Contains(title, StringComparer.Ordinal) ? titles : string.Join(after ? ", " : " ", list.Append(title));
    }

    /// <summary>
    /// Vorschläge beim Tippen: Titel, die mit dem zuletzt getippten Wort beginnen und noch nicht eingetragen sind.
    /// Ist das Feld leer oder endet es mit einem Trenner, kommen alle noch fehlenden Titel in Frage.
    /// </summary>
    public static bool TitleMatches(string? typed, string title)
    {
        var (done, partial) = SplitTyped(typed);
        return !done.Contains(title, StringComparer.Ordinal) && title.StartsWith(partial, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Der gewählte Vorschlag ersetzt das angefangene Wort und wird an die vollständigen Titel angehängt.</summary>
    public static string CompleteTitle(string? typed, string title, bool after)
    {
        var (done, _) = SplitTyped(typed);
        return AppendTitle(string.Join(after ? ", " : " ", done), title, after);
    }

    /// <summary>Fertige Titel und das angefangene letzte Wort (leer, wenn das Feld mit Leerzeichen oder Komma endet).</summary>
    private static (IReadOnlyList<string> Done, string Partial) SplitTyped(string? typed)
    {
        var words = PersonNames.SplitTitles(typed);
        if (words.Count == 0 || typed!.EndsWith(' ') || typed.EndsWith(','))
        {
            return (words, "");
        }

        return (words.Take(words.Count - 1).ToList(), words[^1]);
    }

    [RelayCommand]
    private void Cancel() => _cancel();

    /// <summary>Pflicht ist ein Name (Vor- oder Nachname), wie am Server; leer lässt „Speichern“ ausgegraut.</summary>
    private bool CanSave => FirstName.Trim().Length > 0 || LastName.Trim().Length > 0;

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync()
    {
        if (Validate(out var birthDate) is { } invalid)
        {
            FocusRequested?.Invoke(invalid);
            return;
        }

        static string? Blank(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        var input = new CustomerInput(FirstName.Trim(), LastName.Trim(), Blank(Email), Blank(Phone), birthDate, Blank(Note),
            Language: Language.Code, Salutation: Salutation.Value, PreferredName: Blank(PreferredName), Pronouns: Blank(Pronouns),
            TitlesBefore: Blank(TitlesBefore),
            TitlesAfter: Blank(TitlesAfter), TitleInGreeting: TitleInGreeting.Value,
            Address: new CustomerAddress(Blank(Street), Blank(PostalCode), Blank(City), Blank(Country)));
        var result = await _api.PutAsync<CustomerDto>($"api/customers/{Customer.Id}", input).ConfigureAwait(true);
        if (result.Value is not { } updated)
        {
            Error = result.Error;
            return;
        }

        Error = null;
        await _saved(updated).ConfigureAwait(true);
    }

    [RelayCommand]
    private void CloseError() => Error = null;

    /// <summary>Prüft die Felder mit Format; setzt die Fehler am Feld und nennt das erste ungültige Feld in der Reihenfolge der Maske, sonst <c>null</c>.</summary>
    private string? Validate(out DateOnly? birthDate)
    {
        birthDate = null;
        EmailError = Email.Trim().Length > 0 && !Email.Contains('@', StringComparison.Ordinal) ? "Die E-Mail-Adresse braucht ein @, z. B. name@beispiel.at." : null;
        BirthDateError = null;
        var birthday = DateInputParser.Parse(BirthDate, DateOnly.FromDateTime(Time.GetLocalNow().DateTime), BirthDateRules);
        BirthDateError = birthday.Error;
        birthDate = birthday.Value;

        PostalCodeError = PostalCodeProblem(PostalCode.Trim(), Country.Trim());
        CountryError = Country.Trim().Length > 0 && !(Country.Trim().Length == 2 && Country.Trim().All(char.IsAsciiLetter))
            ? "Das Land als Kürzel mit zwei Buchstaben eingeben, z. B. AT."
            : null;
        Error = null;
        return EmailError is not null ? EmailField
            : BirthDateError is not null ? BirthDateField
            : PostalCodeError is not null ? PostalCodeField
            : CountryError is not null ? CountryField
            : null;
    }

    /// <summary>Österreich und Schweiz haben vier, Deutschland fünf Ziffern; für andere Länder gilt nur die Höchstlänge des Servers.</summary>
    private static string? PostalCodeProblem(string postalCode, string country)
    {
        if (postalCode.Length == 0)
        {
            return null;
        }

        var digits = country.ToUpperInvariant() switch
        {
            "AT" or "CH" or "LI" => 4,
            "DE" => 5,
            _ => 0,
        };
        if (digits > 0)
        {
            return postalCode.Length == digits && postalCode.All(char.IsAsciiDigit)
                ? null
                : $"Die PLZ hat {(digits == 4 ? "vier" : "fünf")} Ziffern, z. B. {(digits == 4 ? "1010" : "10115")}.";
        }

        return postalCode.Length <= 16 ? null : "Die PLZ ist zu lang (höchstens 16 Zeichen).";
    }
}
