using System.Text.RegularExpressions;

namespace AispaStudio.Api.Assistant.Answers;

/// <summary>
/// Regeln für Fragen zu Personal, Räumen und Lager (Slice 136): Pause und Dienstende, Ersthelfer und Zuständigkeiten, freie Zeiten und Sperren eines Raums, Bestand,
/// Mindestbestand und offene Bestellungen. Sie stehen in <see cref="TopicOf"/> vor den Gast-Mustern, weil „wann kommt die Lieferung“ und „wann hat sophie pause“ sonst als Frage
/// nach einem Gast gelten. Preise von Verkaufsartikeln beantwortet das Thema <c>TreatmentInfo</c> mit, wenn keine Behandlung passt.
/// </summary>
internal static partial class BusinessIntents
{
    private static BusinessTopic? StaffRoomStockTopic(string t)
    {
        if (DutyWords().IsMatch(t))
        {
            return BusinessTopic.DutyHolders;
        }

        if (PersonBreakWords().IsMatch(t) && !NamedGuest().IsMatch(t))
        {
            return BusinessTopic.PersonBreak;
        }

        if ((RoomClosureWords().IsMatch(t) || RoomFreeTimeWords().IsMatch(t)) && RoomNoun().IsMatch(t) && !GuestTitles().IsMatch(t))
        {
            return BusinessTopic.RoomFreeTimes;
        }

        if (StockLowWords().IsMatch(t))
        {
            return BusinessTopic.StockLow;
        }

        if (OpenOrderWords().IsMatch(t))
        {
            return BusinessTopic.OpenOrders;
        }

        return StockLevelWords().IsMatch(t) && !NotStock().IsMatch(t) ? BusinessTopic.StockLevel : null;
    }

    /// <summary>Der Name der Person, deren Pause oder Dienstende die Frage meint („wann hat sophie pause“, „wann geht Sophie heim“); sonst <c>null</c>.</summary>
    private static string? BreakPerson(string question, string lower)
    {
        if (PersonName(question, lower) is { } named)
        {
            return named;
        }

        foreach (Match match in PersonAfterVerb().Matches(lower))
        {
            var word = question.Substring(match.Groups["n"].Index, match.Groups["n"].Length);
            var key = word.ToLowerInvariant();
            if (!Stop.Contains(key) && !PersonStop.Contains(key) && key is not ("heim" or "nach" or "pause" or "feierabend"))
            {
                return word;
            }
        }

        return null;
    }

    /// <summary>Welche Zuständigkeit die Frage meint (<c>firstaid</c>, <c>cash</c>, <c>stock</c>, <c>fire</c>, <c>hygiene</c>, <c>safety</c>, <c>training</c>); ohne klares Wort <c>null</c> (alle).</summary>
    private static string? DutyDetail(string t) =>
        Regex.IsMatch(t, @"ersthelf|erste[- ]hilfe") ? "firstaid"
        : Regex.IsMatch(t, @"kassa|kasse") ? "cash"
        : Regex.IsMatch(t, @"lager") ? "stock"
        : Regex.IsMatch(t, @"brand") ? "fire"
        : Regex.IsMatch(t, @"hygiene") ? "hygiene"
        : Regex.IsMatch(t, @"sicherheit") ? "safety"
        : Regex.IsMatch(t, @"ausbildung|schulung") ? "training"
        : null;

    [GeneratedRegex(@"\b(pause|pausen|mittagspause|feierabend|dienstende)\b|\b(geht|gehen)\b.*\b(heim|nach hause)\b|\bwie lange arbeitet\b|\bbis wann arbeitet\b")]
    private static partial Regex PersonBreakWords();

    [GeneratedRegex(@"\b(?:geht|gehen|macht|hört|arbeitet|ist)\s+(?<n>[\p{L}'-]{2,})\b")]
    private static partial Regex PersonAfterVerb();

    /// <summary>Ersthelfer, oder die Frage nach der Zuständigkeit für Kassa, Lager, Brandschutz, Hygiene, Sicherheit, Ausbildung; „Erste-Hilfe-Kasten“ ist ein Ort, keine Person.</summary>
    [GeneratedRegex(@"\bersthelf\w*|\berste[- ]hilfe\b(?![- ]?(kasten|koffer|set|kurs|box))|(zuständig|verantwortlich)\w*\b.{0,30}\b(kassa|kasse|lager|brandschutz|hygiene|sicherheit|ausbildung|schulung)\b|\b(kassa|kasse|lager|brandschutz|hygiene|ausbildung)\w*\W{1,3}(zuständig|verantwortlich|beauftragt)")]
    private static partial Regex DutyWords();

    [GeneratedRegex(@"\b(gesperrt\w*|raumsperre\w*|sperrung\w*|sperre|außer betrieb)\b")]
    private static partial Regex RoomClosureWords();

    [GeneratedRegex(@"\b(wann|ab wann|bis wann|wie lange)\b.*\b(frei|verfügbar|leer|belegt)\b")]
    private static partial Regex RoomFreeTimeWords();

    [GeneratedRegex(@"\p{L}*(?<!zeit)(raum|räume)\b|\p{L}*(zimmer|kabine|suite)\b|\braumsperre\w*|\bhamam\b|\bloft\b")]
    private static partial Regex RoomNoun();

    [GeneratedRegex(@"\bmindestbestand\b|\bbald (aus|leer|alle)\b|\bwas\b.*\b(muss|müssen|sollten|sollen|sollte)\b.*\bbestell\w*|\bwas geht (uns )?aus\b|\bnachbestell\w*")]
    private static partial Regex StockLowWords();

    /// <summary>Nur „Was muss bestellt werden?“ und „Was sollten wir bestellen?“; sie beginnen wie eine Bedienfrage, fragen aber nach dem Lager („Wie bestelle ich …?“ bleibt beim Handbuch).</summary>
    [GeneratedRegex(@"^\W*was (muss|müssen|sollten|sollen|sollte)\b.*\bbestell\w*")]
    private static partial Regex WhatToOrderWords();

    [GeneratedRegex(@"\bliefer(ung|ungen|termin\w*)\b|\boffene[nr]? bestellung\w*|\bbestellungen\b|\bwas (ist|wurde|haben wir) (alles )?bestellt\b|\bwann kommt\b.*\b(ware|bestellung)\b|\bwas ist unterwegs\b")]
    private static partial Regex OpenOrderWords();

    [GeneratedRegex(@"\bbestand\b|\bauf lager\b|\bvorrätig\b|\blagernd\b|\bhaben wir\b.*\b(noch|genug)\b|\bwie ?viel(e)?\b.*\b(haben wir|sind da|ist da|übrig|im lager)\b")]
    private static partial Regex StockLevelWords();

    /// <summary>Wörter, die „haben wir noch …“ zu einer anderen Frage machen (Termine, Gäste, Zeiten, Öffnung, Gutscheine, Preise).</summary>
    [GeneratedRegex(@"termin|gast\b|gäste|kund|mitarbeit|person|stunde|minute|platz|plätze|\bfrei\b|\bzeit\b|\braum\b|\bräume\b|\boffen\b|geöffnet|geschlossen|urlaub|dienst|gutschein|rechnung|\bpaket|anzahlung|rabatt|aktion|behandlung|\bmassage\b|umsatz|warteliste|no-?show|erschein|geburtstag|feiertag|ferien|fragebogen|anamnese|absag|storn|schließ|sperr")]
    private static partial Regex NotStock();
}
