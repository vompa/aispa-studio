using System.Globalization;
using AispaStudio.Api.Scheduling;

namespace AispaStudio.Api.Online;

/// <summary>
/// Sprachen des Kundenbereichs (wie <c>web/src/booking/lang</c>): Deutsch, Englisch, Französisch und Italienisch. Die Wahl der Sprache und der Rückfall für jede andere
/// (Deutsch, Ä52) stehen in <see cref="GuestLanguage"/>; hier bleibt, was der Kundenbereich dazu braucht (die Sprachkultur für Zahlen und Datumsnamen).
/// </summary>
internal static class OnlineLanguage
{
    /// <summary>Zahlen und Datumsnamen: Österreich für Deutsch, Großbritannien für Englisch, Schweiz für Französisch und Italienisch.</summary>
    public static CultureInfo Culture(string? language) => CultureInfo.GetCultureInfo(GuestLanguage.Normalize(language) switch
    {
        "en" => "en-GB",
        "fr" => "fr-CH",
        "it" => "it-CH",
        _ => "de-AT",
    });
}
