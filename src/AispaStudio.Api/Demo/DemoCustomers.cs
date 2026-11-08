using System.Globalization;
using System.Text;
using AispaStudio.Api.Data;
using AispaStudio.Api.Reporting;
using AispaStudio.Api.Scheduling;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Demo;

/// <summary>
/// Frei erfundener Kundenstamm des Beispiel-Spas: Stammgäste, Gelegenheitsgäste, Touristinnen. Namen, Adressen und Nummern
/// sind zusammengewürfelt (Domains example.*, Nummern ohne echten Anschluss); immer gleich, weil der Zufall einen festen Startwert hat.
/// </summary>
internal static class DemoCustomers
{
    /// <summary>
    /// Kundenstamm einschließlich der ersten zwölf (Stammgäste, auf die sich Tests und Doku beziehen). Der Verlauf bucht höchstens einen Termin je
    /// Gast und Woche (<c>DemoHistory</c>); bei rund 2.400 Terminen braucht es daher viele Gäste, damit der Kalender voll bleibt.
    /// </summary>
    public const int Count = 600;

    private static readonly (string First, string Last)[] Regulars =
    [
        ("Anna", "Bauer"), ("Thomas", "Leitner"), ("Katharina", "Moser"), ("Florian", "Hofer"),
        ("Eva", "Steiner"), ("Lukas", "Pichler"), ("Maria", "Wimmer"), ("Stefan", "Egger"),
        ("Julia", "Novak"), ("Ayşe", "Kaya"), ("Clara", "Fischer"), ("David", "Horvath"),
    ];

    private static readonly HashSet<string> FemaleRegulars = ["Anna", "Katharina", "Eva", "Maria", "Julia", "Ayşe", "Clara"];

    /// <summary>Rufnamen, wie sie im Haus üblich sind; vergeben wird nur an einen kleinen Teil der Gäste mit passendem Vornamen.</summary>
    private static readonly Dictionary<string, string> Nicknames = new()
    {
        ["Magdalena"] = "Leni",
        ["Elisabeth"] = "Sissi",
        ["Johanna"] = "Hanni",
        ["Theresa"] = "Resi",
        ["Christina"] = "Tini",
        ["Katharina"] = "Kathi",
        ["Maximilian"] = "Max",
        ["Alexander"] = "Alex",
        ["Christian"] = "Chris",
        ["Matthias"] = "Matze",
    };

    /// <summary>Gäste (Index im Kundenstamm) ohne Anrede und mit „divers“; die übrigen haben Frau oder Herr passend zum Vornamen.</summary>
    private static readonly int[] WithoutSalutation = [41, 410];
    private const int DiverseSalutation = 123;

    /// <summary>Vorangestellte Titel: Gäste mit „Mag. Dr.“ gibt es genau einmal, die Stammgäste Thomas Leitner, Eva Steiner und David Horvath tragen Titel.</summary>
    private const int DoubleTitle = 17;

    private static readonly (string Street, string PostalCode, string City)[] Addresses =
    [
        ("Lindengasse 12", "1070", "Wien"), ("Schönbrunner Straße 88", "1050", "Wien"), ("Hernalser Hauptstraße 41", "1170", "Wien"),
        ("Währinger Gürtel 7", "1180", "Wien"), ("Kaigasse 5", "5020", "Salzburg"), ("Linzer Gasse 23", "5020", "Salzburg"),
        ("Annenstraße 30", "8020", "Graz"), ("Schillerplatz 3", "8010", "Graz"), ("Landstraße 61", "4020", "Linz"),
        ("Museumstraße 9", "6020", "Innsbruck"), ("Hauptplatz 14", "2500", "Baden"), ("Bahnhofstraße 2", "3100", "St. Pölten"),
    ];

    private static readonly string[] FemaleNames =
    [
        "Sabine", "Andrea", "Claudia", "Petra", "Barbara", "Elisabeth", "Christina", "Sandra", "Martina", "Monika", "Verena", "Lisa",
        "Sophie", "Hannah", "Laura", "Johanna", "Magdalena", "Theresa", "Viktoria", "Valentina", "Lea", "Emma", "Marie", "Nina",
        "Carina", "Bianca", "Daniela", "Melanie", "Silvia", "Ingrid", "Gabriele", "Renate", "Helga", "Ursula", "Tanja", "Jasmin",
        "Selina", "Elif", "Zeynep", "Mira", "Jelena", "Milica", "Agnieszka", "Zsófia", "Chiara", "Giulia", "Sarah", "Olivia",
    ];

    private static readonly string[] MaleNames =
    [
        "Michael", "Andreas", "Christian", "Markus", "Martin", "Daniel", "Alexander", "Wolfgang", "Gerhard", "Peter", "Bernhard", "Manuel",
        "Patrick", "Dominik", "Matthias", "Philipp", "Tobias", "Jakob", "Felix", "Paul", "Elias", "Maximilian", "Simon", "Jonas",
        "Emre", "Luka", "Marko", "Tomasz", "Ádám", "Luca", "James", "Oliver",
    ];

    private static readonly string[] LastNames =
    [
        "Gruber", "Huber", "Wagner", "Müller", "Pichler", "Moser", "Mayer", "Hofer", "Leitner", "Berger", "Fuchs", "Eder", "Fischer",
        "Schmid", "Winkler", "Weber", "Schwarz", "Maier", "Schneider", "Reiter", "Mayr", "Schmidt", "Wimmer", "Brunner", "Lang",
        "Baumgartner", "Auer", "Binder", "Lechner", "Wolf", "Wallner", "Aigner", "Ebner", "Koller", "Lehner", "Haas", "Schuster",
        "Holzer", "Strasser", "Gassner", "Kofler", "Pfeiffer", "Hackl", "Riegler", "Steiner", "Haider", "Kainz", "Kraus", "Zach",
        "Petrović", "Jovanović", "Nowak", "Kowalski", "Yılmaz", "Demir", "Öztürk", "Horváth", "Nagy", "Rossi", "Smith", "Kovačević",
    ];

    private static readonly string[] Notes =
    [
        "Stammgast seit 2019, mag den Raum Lavendel.",
        "Kommt meist mit Partner; Paarbuchungen nebeneinander planen.",
        "Bevorzugt Therapeutinnen.",
        "Zahlt gern mit Gutschein, fragt oft nach Restwert.",
        "Bitte vor Terminen per SMS erinnern, nicht per Mail.",
        "Firmenkunde, Rechnung an den Arbeitgeber.",
        "Spricht lieber Englisch.",
        "Rollstuhl: Raum im Erdgeschoss einplanen.",
    ];

    private static readonly (string? Pressure, string? Scent, string? Temperature, string? Music, bool? Quiet, string? Focus, string? Avoid)[] Preferences =
    [
        ("fest", "Lavendel", "warm", "leise Klaviermusik", true, "Nacken und Schultern", null),
        ("mittel", "ohne Duft", null, null, true, "unterer Rücken", "Bauch"),
        ("sanft", "Zitrusfrüchte", "eher kühl", "Naturklänge", false, null, "Füße (kitzlig)"),
        ("sehr fest", null, "warm", null, true, "Oberschenkel nach dem Laufen", null),
        ("mittel", "Rose", "warm", "keine Musik", true, "Kopf und Gesicht", null),
        (null, "Eukalyptus", null, "Lounge", false, "Rücken", "Gesicht"),
    ];

    public static void Add(AispaDbContext db, DateTimeOffset now)
    {
        var random = new Random(2610);
        // Anrede, Titel, Rufname und Anschrift kommen aus einem eigenen Zufall, damit die übrigen Angaben der Gäste gleich bleiben.
        var extra = new Random(2611);
        var customers = new List<Customer>();
        var used = new HashSet<string>();
        for (var i = 0; i < Count; i++)
        {
            var (first, last) = i < Regulars.Length ? Regulars[i] : NewName(random, used);
            var country = i < Regulars.Length ? "at" : random.Next(10) switch { < 6 => "at", < 8 => "de", 8 => "ch", _ => "com" };
            var customer = new Customer
            {
                FirstName = first,
                LastName = last,
                Email = random.Next(12) == 0 && i >= Regulars.Length ? null : $"{DemoText.Ascii(first)}.{DemoText.Ascii(last)}@example.{country}",
                Phone = i < Regulars.Length ? $"+43 664 1234{i:000}" : Phone(random, country),
                BirthDate = random.Next(3) == 0 ? new DateOnly(1950 + random.Next(52), 1 + random.Next(12), 1 + random.Next(28)) : null,
                Note = random.Next(14) == 0 ? Notes[random.Next(Notes.Length)] : null,
                Language = country == "com" ? "en" : null,
                CreatedAt = now.AddDays(-random.Next(30, 1500) - (i < Regulars.Length ? 1500 : 0)),
            };
            AddNameDetails(customer, i, extra);
            if (i < Regulars.Length || (country != "com" && extra.Next(4) == 0))
            {
                var address = Addresses[extra.Next(Addresses.Length)];
                (customer.Street, customer.PostalCode, customer.City, customer.Country) = (address.Street, address.PostalCode, address.City, "AT");
            }

            customers.Add(customer);
        }

        // Zwei Stammgäste mit Rabatt in der Kundenkartei: die Kassa zieht ihn selbst ab und weist ihn auf dem Beleg aus.
        customers[2].DiscountPercent = 10;
        customers[7].DiscountPercent = 15;
        customers[7].DiscountScope = CustomerDiscountScope.Products;

        // Stammgäste haben fast immer Vorlieben hinterlegt, die anderen selten.
        db.Customers.AddRange(customers);
        db.CustomerPreferences.AddRange(customers
            .Where((c, i) => i < Regulars.Length || random.Next(8) == 0)
            .Select(c =>
            {
                var p = Preferences[random.Next(Preferences.Length)];
                return new CustomerPreference
                {
                    CustomerId = c.Id,
                    Pressure = p.Pressure,
                    Scent = p.Scent,
                    RoomTemperature = p.Temperature,
                    Music = p.Music,
                    PrefersQuiet = p.Quiet,
                    FocusAreas = p.Focus,
                    AvoidAreas = p.Avoid,
                    UpdatedAt = now.AddDays(-random.Next(1, 300)),
                };
            }));
    }

    private static void AddNameDetails(Customer customer, int index, Random random)
    {
        customer.Salutation = index == DiverseSalutation ? CustomerSalutation.Diverse
            : WithoutSalutation.Contains(index) ? CustomerSalutation.None
            : FemaleNames.Contains(customer.FirstName) || FemaleRegulars.Contains(customer.FirstName) ? CustomerSalutation.Female
            : CustomerSalutation.Male;

        if (Nicknames.TryGetValue(customer.FirstName, out var nickname) && (customer.FirstName == "Katharina" || random.Next(8) == 0))
        {
            customer.PreferredName = nickname;
        }

        // Etwa jeder zehnte Gast trägt einen Titel, wie in Österreich üblich.
        customer.TitlesBefore = index switch
        {
            DoubleTitle => "Mag. Dr.",
            1 => "Mag.",
            4 => "Dr.",
            11 => "DI",
            _ => index < Regulars.Length ? null : random.Next(100) switch { < 5 => "Mag.", < 7 => "Dr.", < 9 => "DI", _ => null },
        };
        customer.TitlesAfter = index == 5 ? "BA" : index < Regulars.Length ? null : random.Next(100) switch { < 2 => "MSc", < 3 => "BA", _ => null };
    }

    private static (string, string) NewName(Random random, HashSet<string> used)
    {
        while (true)
        {
            var first = random.Next(10) < 7 ? FemaleNames[random.Next(FemaleNames.Length)] : MaleNames[random.Next(MaleNames.Length)];
            var last = LastNames[random.Next(LastNames.Length)];
            if (used.Add($"{first} {last}") && !Regulars.Contains((first, last)))
            {
                return (first, last);
            }
        }
    }

    private static string Phone(Random random, string country) => country switch
    {
        "de" => $"+49 151 {random.Next(1000000, 9999999)}",
        "ch" => $"+41 79 {random.Next(100, 999)} {random.Next(10, 99)} {random.Next(10, 99)}",
        "com" => $"+44 7700 {random.Next(900000, 900999)}",
        _ => $"+43 {(random.Next(3) switch { 0 => "664", 1 => "676", _ => "699" })} {random.Next(1000000, 9999999)}",
    };
}

internal static class DemoText
{
    /// <summary>Für Mailadressen: klein, Umlaute ausgeschrieben, sonstige Akzente weg („Marić“ → „maric“).</summary>
    public static string Ascii(string text)
    {
        var lower = text.ToLowerInvariant()
            .Replace("ä", "ae", StringComparison.Ordinal).Replace("ö", "oe", StringComparison.Ordinal)
            .Replace("ü", "ue", StringComparison.Ordinal).Replace("ß", "ss", StringComparison.Ordinal)
            .Replace("ı", "i", StringComparison.Ordinal).Replace(" ", "", StringComparison.Ordinal);
        var builder = new StringBuilder();
        foreach (var c in lower.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(c);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }
}
