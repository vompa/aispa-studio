using AispaStudio.Contracts;

namespace AispaStudio.Api.Scheduling;

/// <summary>
/// Geburtstage der Gäste (Runde 5b, Ä45): für den Kalender (am Tag und drei Tage davor und danach), für die Kundenkarte (der nächste) und für den Assistenten
/// („Wer hat heute Geburtstag?“). Gerechnet wird hier im Server, nie in den Oberflächen. Wer am 29. Februar geboren ist, feiert in Jahren ohne Schalttag am 28. Februar.
/// </summary>
internal static class Birthdays
{
    /// <summary>So viele Tage vor und nach dem Termintag zeigen Hover-Karte und Panel einen dezenten Hinweis.</summary>
    public const int NearDays = 3;

    /// <summary>Das Geburtsjahr 1 steht für „Jahr unbekannt“: dann gibt es kein Alter.</summary>
    private const int UnknownYear = 1;

    /// <summary>Der Geburtstag im Jahr <paramref name="year"/>; der 29. Februar rückt in Jahren ohne Schalttag auf den 28.</summary>
    public static DateOnly InYear(DateOnly birth, int year) =>
        birth.Month == 2 && birth.Day == 29 && !DateTime.IsLeapYear(year) ? new DateOnly(year, 2, 28) : new DateOnly(year, birth.Month, birth.Day);

    /// <summary>Geburtstag am Tag <paramref name="day"/> oder höchstens <see cref="NearDays"/> Tage davor oder danach; sonst <c>null</c>.</summary>
    public static GuestBirthday? Near(DateOnly birth, DateOnly day)
    {
        foreach (var year in new[] { day.Year, day.Year - 1, day.Year + 1 })
        {
            var date = InYear(birth, year);
            if (Math.Abs(date.DayNumber - day.DayNumber) <= NearDays)
            {
                return Of(birth, date, day);
            }
        }

        return null;
    }

    /// <summary>Der nächste Geburtstag ab dem Tag <paramref name="day"/> (heute eingeschlossen).</summary>
    public static GuestBirthday Next(DateOnly birth, DateOnly day)
    {
        var date = InYear(birth, day.Year);
        return Of(birth, date.DayNumber < day.DayNumber ? InYear(birth, day.Year + 1) : date, day);
    }

    private static GuestBirthday Of(DateOnly birth, DateOnly date, DateOnly day)
    {
        var age = birth.Year == UnknownYear ? (int?)null : date.Year - birth.Year;
        // Ein Geburtsdatum in der Zukunft ist ein Eingabefehler; es gibt dann kein Alter.
        age = age is < 0 ? null : age;
        return new GuestBirthday(date, date.DayNumber - day.DayNumber, age, age is { } a ? a - (date > day ? 1 : 0) : null);
    }
}
