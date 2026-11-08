namespace AispaStudio.Contracts;

/// <summary>Anrede einer Kundin bzw. eines Kunden; „divers“ und „keine Angabe“ zeigen kein Symbol und sprechen ohne Frau/Herr an.</summary>
public enum CustomerSalutation
{
    None,
    Female,
    Male,
    Diverse,
}

/// <summary>Postanschrift des Gastes (nicht der Rechnungsempfänger); <c>Country</c> als ISO-Kürzel, z. B. „AT“.</summary>
public sealed record CustomerAddress(string? Street, string? PostalCode, string? City, string? Country);

/// <summary>
/// Namensregeln (ADR 0064): Der <b>gültige Name</b> (Vor- und Nachname) steht auf Rechnungen, Belegen und in Nachrichten an den Gast,
/// der <b>Rufname</b> ersetzt im Haus den Vornamen (Kalender, Suche, Kassa-Oberfläche). Titel stehen in der Adresszeile immer,
/// in der Anrede nach dem Land des Standorts: AT der höchste vorangestellte, DE nur Dr. und Prof., CH keine; je Kunde übersteuerbar.
/// </summary>
public static class PersonNames
{
    /// <summary>Vorangestellte Titel zur Auswahl; frei eingegebene sind ebenso erlaubt.</summary>
    public static IReadOnlyList<string> TitlesBefore { get; } =
        ["Dr.", "Mag.", "DI", "Ing.", "Prof.", "DDr.", "MMag.", "Dipl.-Ing.", "HR", "KR", "MR", "OStR", "Bakk."];

    /// <summary>Nachgestellte Titel zur Auswahl.</summary>
    public static IReadOnlyList<string> TitlesAfter { get; } = ["MSc", "BSc", "BA", "MA", "PhD", "MBA", "LL.M.", "MEd"];

    /// <summary>
    /// Rang für „der höchste“ vorangestellte Titel in Österreich: Professur vor Doktorat vor Diplom/Magister vor Ingenieur vor Berufstitel.
    /// Unbekannte Titel zählen nach den bekannten; bei Gleichstand gilt der zuerst genannte.
    /// </summary>
    private static readonly string[] RankAustria = ["Prof.", "DDr.", "Dr.", "MMag.", "Mag.", "DI", "Dipl.-Ing.", "Ing.", "HR", "KR", "MR", "OStR", "Bakk."];

    public static string Legal(string firstName, string lastName) => $"{firstName} {lastName}".Trim();

    /// <summary>Name im Haus: Rufname statt Vorname, wenn eingetragen („Steffi Huber“).</summary>
    public static string InHouse(string firstName, string lastName, string? preferredName) =>
        Legal(string.IsNullOrWhiteSpace(preferredName) ? firstName : preferredName.Trim(), lastName);

    public static string? Symbol(CustomerSalutation salutation) => salutation switch
    {
        CustomerSalutation.Female => "♀",
        CustomerSalutation.Male => "♂",
        _ => null,
    };

    public static string? Word(CustomerSalutation salutation) => salutation switch
    {
        CustomerSalutation.Female => "Frau",
        CustomerSalutation.Male => "Herr",
        _ => null,
    };

    public static string Label(CustomerSalutation salutation) => salutation switch
    {
        CustomerSalutation.Female => "Frau",
        CustomerSalutation.Male => "Herr",
        CustomerSalutation.Diverse => "divers",
        _ => "keine Angabe",
    };

    /// <summary>„Mag. Dr.“ oder „MSc, MBA“ in einzelne Titel; Leerzeichen und Kommas trennen.</summary>
    public static IReadOnlyList<string> SplitTitles(string? titles) =>
        string.IsNullOrWhiteSpace(titles) ? [] : titles.Split([' ', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    /// <summary>Titel einheitlich gespeichert: vorangestellte mit Leerzeichen, nachgestellte mit Komma; leer wird null.</summary>
    public static string? NormalizeTitles(string? titles, bool after) =>
        SplitTitles(titles) is { Count: > 0 } list ? string.Join(after ? ", " : " ", list) : null;

    /// <summary>Adresszeile mit allen Titeln: „Frau Mag. Stefanie Huber, MSc“.</summary>
    public static string AddressLine(CustomerSalutation salutation, string? titlesBefore, string firstName, string lastName, string? titlesAfter)
    {
        var before = string.Join(" ", new[] { Word(salutation) }.Concat(SplitTitles(titlesBefore)).Append(Legal(firstName, lastName))
            .Where(p => !string.IsNullOrEmpty(p)));
        return SplitTitles(titlesAfter) is { Count: > 0 } after ? $"{before}, {string.Join(", ", after)}" : before;
    }

    /// <summary>
    /// Titel in der Anrede, nachgestellte nie. <paramref name="titleInGreeting"/> null folgt dem Land (AT der höchste, DE Dr. und Prof., CH keiner),
    /// false nimmt keinen, true den höchsten auch in DE und CH.
    /// </summary>
    public static IReadOnlyList<string> GreetingTitles(string? titlesBefore, FiscalCountry country, bool? titleInGreeting = null)
    {
        var titles = SplitTitles(titlesBefore);
        if (titles.Count == 0 || titleInGreeting == false || (titleInGreeting is null && country == FiscalCountry.CH))
        {
            return [];
        }

        if (titleInGreeting is null && country == FiscalCountry.DE)
        {
            return [.. titles.Where(IsDoctorOrProfessor)];
        }

        return [titles.OrderBy(Rank).First()];
    }

    /// <summary>
    /// Wer angesprochen wird: „Frau Mag. Huber“; ohne Frau/Herr (divers, keine Angabe) oder ohne Nachnamen mit vollem Namen: „Mag. Kim Berger“.
    /// </summary>
    public static string GreetingName(CustomerSalutation salutation, string? titlesBefore, string firstName, string lastName, FiscalCountry country,
        bool? titleInGreeting = null)
    {
        var word = lastName.Trim().Length > 0 ? Word(salutation) : null;
        var name = word is null ? Legal(firstName, lastName) : lastName.Trim();
        return string.Join(" ", new[] { word }.Concat(GreetingTitles(titlesBefore, country, titleInGreeting)).Append(name)
            .Where(p => !string.IsNullOrEmpty(p)));
    }

    /// <summary>Begrüßung in Nachrichten an den Gast: „Guten Tag Frau Mag. Huber“ (ohne Satzzeichen).</summary>
    public static string Greeting(CustomerSalutation salutation, string? titlesBefore, string firstName, string lastName, FiscalCountry country,
        bool? titleInGreeting = null) =>
        $"Guten Tag {GreetingName(salutation, titlesBefore, firstName, lastName, country, titleInGreeting)}";

    /// <summary>Land eines Standorts aus seiner Zeitzone (so rechnen auch die Vergleichswerte).</summary>
    public static FiscalCountry CountryOfTimeZone(string? timeZone) => timeZone switch
    {
        "Europe/Berlin" => FiscalCountry.DE,
        "Europe/Zurich" => FiscalCountry.CH,
        _ => FiscalCountry.AT,
    };

    /// <summary>Vorangestellte Titel am Anfang eines Namens: „Mag. Dr. Stefanie Huber“ → „Mag. Dr.“; ohne Titel null.</summary>
    public static string? LeadingTitles(string? name) =>
        NormalizeTitles(string.Join(" ", (name ?? "").Split([' ', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .TakeWhile(IsTitle)), after: false);

    /// <summary>Name ohne Titel zum Vergleichen: „Dr. Stefanie Huber, MSc“ → „Stefanie Huber“.</summary>
    public static string WithoutTitles(string? name) =>
        string.Join(" ", (name ?? "").Split([' ', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(p => !IsTitle(p)));

    /// <summary>
    /// Bekannter Titel oder abgekürztes Wort mit Punkt („Univ.-Prof.“), das kein Name ist. Groß- und Kleinschreibung zählt, damit der
    /// Nachname „Ma“ nicht als „MA“ wegfällt.
    /// </summary>
    public static bool IsTitle(string word) =>
        TitlesBefore.Concat(TitlesAfter).Contains(word, StringComparer.Ordinal) || (word.Length > 1 && word.EndsWith('.'));

    private static bool IsDoctorOrProfessor(string title) =>
        title.StartsWith("Dr", StringComparison.OrdinalIgnoreCase) || title.StartsWith("DDr", StringComparison.OrdinalIgnoreCase)
        || title.Contains("Prof", StringComparison.OrdinalIgnoreCase);

    private static int Rank(string title)
    {
        var index = Array.FindIndex(RankAustria, t => t.Equals(title, StringComparison.OrdinalIgnoreCase));
        return index < 0 ? RankAustria.Length : index;
    }
}
