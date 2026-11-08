using AispaStudio.Api.Data;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Payroll;

/// <summary>
/// Gesetzliche Feiertage je Land und Region (Bundesland, Kanton) nach ISO 3166-2, z. B. „DE-BY“ oder „CH-ZH“.
/// Österreich: das Arbeitsruhegesetz kennt nur bundesweite Feiertage; Landespatrone sind keine gesetzlichen Feiertage.
/// Deutschland: Mariä Himmelfahrt gilt in Bayern nur in überwiegend katholischen Gemeinden (die große Mehrheit, u. a. München)
/// und ist hier für ganz Bayern gesetzt; Fronleichnam in Teilen Sachsens und Thüringens und das Augsburger Friedensfest fehlen.
/// Solche Gemeindeausnahmen lassen sich später je Standort ergänzen.
/// Schweiz: bundesweit nur der 1. August; die übrigen bestimmen die Kantone (bis zu acht, Art. 20a ArG).
/// </summary>
internal static class PublicHolidays
{
    /// <summary>Regionen mit eigenen Feiertagen. Andere Kantone gelten vorerst nur mit dem Bundesfeiertag.</summary>
    public static readonly IReadOnlyList<HolidayRegion> Regions =
    [
        new("AT-1", FiscalCountry.AT, "Burgenland"), new("AT-2", FiscalCountry.AT, "Kärnten"),
        new("AT-3", FiscalCountry.AT, "Niederösterreich"), new("AT-4", FiscalCountry.AT, "Oberösterreich"),
        new("AT-5", FiscalCountry.AT, "Salzburg"), new("AT-6", FiscalCountry.AT, "Steiermark"),
        new("AT-7", FiscalCountry.AT, "Tirol"), new("AT-8", FiscalCountry.AT, "Vorarlberg"), new("AT-9", FiscalCountry.AT, "Wien"),
        new("DE-BW", FiscalCountry.DE, "Baden-Württemberg"), new("DE-BY", FiscalCountry.DE, "Bayern"),
        new("DE-BE", FiscalCountry.DE, "Berlin"), new("DE-BB", FiscalCountry.DE, "Brandenburg"),
        new("DE-HB", FiscalCountry.DE, "Bremen"), new("DE-HH", FiscalCountry.DE, "Hamburg"),
        new("DE-HE", FiscalCountry.DE, "Hessen"), new("DE-MV", FiscalCountry.DE, "Mecklenburg-Vorpommern"),
        new("DE-NI", FiscalCountry.DE, "Niedersachsen"), new("DE-NW", FiscalCountry.DE, "Nordrhein-Westfalen"),
        new("DE-RP", FiscalCountry.DE, "Rheinland-Pfalz"), new("DE-SL", FiscalCountry.DE, "Saarland"),
        new("DE-SN", FiscalCountry.DE, "Sachsen"), new("DE-ST", FiscalCountry.DE, "Sachsen-Anhalt"),
        new("DE-SH", FiscalCountry.DE, "Schleswig-Holstein"), new("DE-TH", FiscalCountry.DE, "Thüringen"),
        new("CH-BE", FiscalCountry.CH, "Bern"), new("CH-BS", FiscalCountry.CH, "Basel-Stadt"), new("CH-ZH", FiscalCountry.CH, "Zürich"),
    ];

    public static HolidayRegion? Region(string? code) => code is null ? null : Regions.FirstOrDefault(r => r.Code == code);

    /// <summary>
    /// Land und Region je Standort: das Land aus der Region, sonst aus der Kasse am Standort, sonst aus den Lohneinstellungen.
    /// </summary>
    public static async Task<Dictionary<Guid, LocationHolidays>> LocationsAsync(AispaDbContext db, CancellationToken ct)
    {
        var fallback = (await db.PayrollSettings.AsNoTracking().SettingsAsync(ct)).Country;
        var registers = await db.CashRegisters.AsNoTracking().GroupBy(r => r.LocationId)
            .Select(g => new { g.Key, Country = g.OrderBy(r => r.CreatedAt).Select(r => r.Country).First() })
            .ToDictionaryAsync(r => r.Key, r => r.Country, ct);
        return (await db.Locations.AsNoTracking().Select(l => new { l.Id, l.Name, l.Region }).ToListAsync(ct))
            .ToDictionary(l => l.Id, l => new LocationHolidays(l.Name,
                Region(l.Region)?.Country ?? (registers.TryGetValue(l.Id, out var c) ? c : fallback), Region(l.Region)?.Code));
    }

    public static bool IsHoliday(FiscalCountry country, DateOnly date) => For(country, date.Year).Contains(date);

    /// <summary>Feiertage eines Standorts: mit Region deren Feiertage, sonst die landesweiten des angegebenen Landes.</summary>
    public static HashSet<DateOnly> For(FiscalCountry country, string? region, int year) =>
        Region(region) is { } r ? Named(r.Country, r.Code, year).Select(h => h.Date).ToHashSet() : For(country, year);

    public static HashSet<DateOnly> For(FiscalCountry country, int year) => Named(country, null, year).Select(h => h.Date).ToHashSet();

    public static IReadOnlyList<(DateOnly Date, string Name)> Named(FiscalCountry country, string? region, int year)
    {
        var easter = EasterSunday(year);
        var list = new List<(DateOnly, string)>();
        void Add(DateOnly date, string name) => list.Add((date, name));
        void Fixed(int month, int day, string name) => Add(new(year, month, day), name);
        bool In(params ReadOnlySpan<string> regions) => region is not null && regions.Contains(region);

        switch (country)
        {
            case FiscalCountry.AT:
                Fixed(1, 1, "Neujahr");
                Fixed(1, 6, "Heilige Drei Könige");
                Add(easter.AddDays(1), "Ostermontag");
                Fixed(5, 1, "Staatsfeiertag");
                Add(easter.AddDays(39), "Christi Himmelfahrt");
                Add(easter.AddDays(50), "Pfingstmontag");
                Add(easter.AddDays(60), "Fronleichnam");
                Fixed(8, 15, "Mariä Himmelfahrt");
                Fixed(10, 26, "Nationalfeiertag");
                Fixed(11, 1, "Allerheiligen");
                Fixed(12, 8, "Mariä Empfängnis");
                Fixed(12, 25, "Christtag");
                Fixed(12, 26, "Stefanitag");
                break;
            case FiscalCountry.DE:
                Fixed(1, 1, "Neujahr");
                if (In("DE-BW", "DE-BY", "DE-ST"))
                {
                    Fixed(1, 6, "Heilige Drei Könige");
                }

                if ((In("DE-BE") && year >= 2019) || (In("DE-MV") && year >= 2023))
                {
                    Fixed(3, 8, "Internationaler Frauentag");
                }

                Add(easter.AddDays(-2), "Karfreitag");
                if (In("DE-BB"))
                {
                    Add(easter, "Ostersonntag");
                }

                Add(easter.AddDays(1), "Ostermontag");
                Fixed(5, 1, "Tag der Arbeit");
                Add(easter.AddDays(39), "Christi Himmelfahrt");
                if (In("DE-BB"))
                {
                    Add(easter.AddDays(49), "Pfingstsonntag");
                }

                Add(easter.AddDays(50), "Pfingstmontag");
                if (In("DE-BW", "DE-BY", "DE-HE", "DE-NW", "DE-RP", "DE-SL"))
                {
                    Add(easter.AddDays(60), "Fronleichnam");
                }

                if (In("DE-BY", "DE-SL"))
                {
                    Fixed(8, 15, "Mariä Himmelfahrt");
                }

                if (In("DE-TH") && year >= 2019)
                {
                    Fixed(9, 20, "Weltkindertag");
                }

                Fixed(10, 3, "Tag der Deutschen Einheit");
                if (In("DE-BB", "DE-MV", "DE-SN", "DE-ST", "DE-TH") || (In("DE-HB", "DE-HH", "DE-NI", "DE-SH") && year >= 2018))
                {
                    Fixed(10, 31, "Reformationstag");
                }

                if (In("DE-BW", "DE-BY", "DE-NW", "DE-RP", "DE-SL"))
                {
                    Fixed(11, 1, "Allerheiligen");
                }

                if (In("DE-SN"))
                {
                    // Mittwoch vor dem 23. November.
                    var day = new DateOnly(year, 11, 22);
                    Add(day.AddDays(-(((int)day.DayOfWeek - (int)DayOfWeek.Wednesday + 7) % 7)), "Buß- und Bettag");
                }

                Fixed(12, 25, "1. Weihnachtstag");
                Fixed(12, 26, "2. Weihnachtstag");
                break;
            default:
                if (In("CH-BE", "CH-BS", "CH-ZH"))
                {
                    Fixed(1, 1, "Neujahr");
                    if (In("CH-BE"))
                    {
                        Fixed(1, 2, "Berchtoldstag");
                    }

                    Add(easter.AddDays(-2), "Karfreitag");
                    Add(easter.AddDays(1), "Ostermontag");
                    if (In("CH-BS", "CH-ZH"))
                    {
                        Fixed(5, 1, "Tag der Arbeit");
                    }

                    Add(easter.AddDays(39), "Auffahrt");
                    Add(easter.AddDays(50), "Pfingstmontag");
                }

                Fixed(8, 1, "Bundesfeiertag");
                if (In("CH-BE", "CH-BS", "CH-ZH"))
                {
                    Fixed(12, 25, "Weihnachtstag");
                    Fixed(12, 26, "Stephanstag");
                }

                break;
        }

        return list;
    }

    /// <summary>Ostersonntag nach der Gaußschen Osterformel (gregorianisch).</summary>
    public static DateOnly EasterSunday(int year)
    {
        var a = year % 19;
        var b = year / 100;
        var c = year % 100;
        var d = b / 4;
        var e = b % 4;
        var f = (b + 8) / 25;
        var g = (b - f + 1) / 3;
        var h = ((19 * a) + b - d - g + 15) % 30;
        var i = c / 4;
        var k = c % 4;
        var l = (32 + (2 * e) + (2 * i) - h - k) % 7;
        var m = (a + (11 * h) + (22 * l)) / 451;
        var month = (h + l - (7 * m) + 114) / 31;
        var day = ((h + l - (7 * m) + 114) % 31) + 1;
        return new DateOnly(year, month, day);
    }
}

internal sealed record HolidayRegion(string Code, FiscalCountry Country, string Name);

internal sealed record LocationHolidays(string Name, FiscalCountry Country, string? Region)
{
    public HashSet<DateOnly> For(int year) => PublicHolidays.For(Country, Region, year);
}
