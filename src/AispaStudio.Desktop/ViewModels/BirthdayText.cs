using System.Globalization;
using AispaStudio.Contracts;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>
/// Texte zum Geburtstag eines Gastes (Runde 5b, Ä45). Der Server rechnet Tag, Abstand und Alter (<see cref="GuestBirthday"/>), die Oberfläche setzt nur Worte daraus
/// zusammen: Hover-Karte und Termin-Panel, Tagesüberblick, Kundenkarte.
/// </summary>
public static class BirthdayText
{
    private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-AT");

    /// <summary>
    /// Hover-Karte und Termin-Panel: „Geburtstag heute · 42“ (das Alter nur, wenn das Geburtsjahr bekannt ist), drei Tage davor und danach „Geburtstag am 25.10.“.
    /// Ohne Geburtstag in diesem Fenster <c>null</c>.
    /// </summary>
    public static string? Line(GuestBirthday? birthday) => birthday switch
    {
        null => null,
        { DaysAway: 0, Age: { } age } => $"Geburtstag heute · {age}",
        { DaysAway: 0 } => "Geburtstag heute",
        _ => $"Geburtstag am {birthday.Date.ToString("dd.MM.", German)}",
    };

    /// <summary>
    /// Kundenkarte, nach dem Geburtsdatum: „45 Jahre · Nächster Geburtstag in 12 Tagen“, am Tag selbst „Geburtstag heute · 46“. Ohne Geburtsjahr nur der Abstand;
    /// ohne Geburtstag <c>null</c>.
    /// </summary>
    public static string? Card(GuestBirthday? birthday)
    {
        if (birthday is null)
        {
            return null;
        }

        if (birthday.DaysAway == 0)
        {
            return Line(birthday);
        }

        var away = birthday.DaysAway == 1 ? "Nächster Geburtstag morgen" : $"Nächster Geburtstag in {birthday.DaysAway} Tagen";
        return birthday.AgeNow is { } age ? $"{age} Jahre · {away}" : away;
    }
}
