using System.Globalization;

namespace AispaStudio.LiveTests;

/// <summary>
/// Ein wählbares Thema eines Ablaufs; <see cref="Key"/> ist der Name für <c>--topics</c>. <see cref="Needs"/> nennt die Themen, die
/// vorher im selben Lauf gelaufen sein müssen (sie legen Kundin, Termin oder Gutschein an); <see cref="Leaves"/> ist der Vermerk
/// im Bericht, was das Thema in der Datenbank hinterlässt.
/// </summary>
internal sealed record LiveTopic(
    string Key,
    string Name,
    string Description,
    Action<LiveSession, LiveRunContext> Run,
    IReadOnlyList<string>? Needs = null,
    string Leaves = "Legt Daten an; zum Aufräumen die Datenbank neu laden.");

/// <summary>Ergebnis eines Schritts für die Anzeige in Konsole und Starter-App.</summary>
internal sealed record LiveStepResult(int Number, string Name, TimeSpan Duration, bool Passed, string? Error, string Image);

/// <summary>Was die Themen eines Laufs miteinander teilen.</summary>
internal sealed class LiveRunContext
{
    /// <summary>Zeitstempel des Laufs; macht Namen und Telefon eindeutig, sonst meldet die Kundenmaske eine Dublette.</summary>
    public string Stamp { get; } = DateTime.Now.ToString("MMddHHmmss", CultureInfo.InvariantCulture);

    /// <summary>Name der Kundin, die der Lauf angelegt hat; null, wenn das Thema „Kundin anlegen“ nicht lief.</summary>
    public string? CustomerName { get; set; }

    /// <summary>Schlüssel der Themen, die in diesem Lauf schon gelaufen sind.</summary>
    public HashSet<string> Done { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Der Termin, den „Termin buchen“ angelegt hat: Kundin, Behandlung und Beginn („HH:mm“); null vor diesem Thema.</summary>
    public BookedAppointment? Appointment { get; set; }

    /// <summary>Gutschein-Code, den „Gutschein“ verkauft hat (Format XXXX-XXXX-XXXX); null vor diesem Thema.</summary>
    public string? VoucherCode { get; set; }
}

/// <summary>
/// Ein Termin des Laufs, wie ihn der Kalender zeigt. <see cref="Date"/> ist das Datum in der Kopfzeile („9. November 2026“) und
/// <see cref="DaysAhead"/> der Abstand zu „Heute“: die Freie-Zeit-Suche nimmt die erste freie Zeit, und die liegt nach vielen
/// Läufen nicht mehr morgen.
/// </summary>
internal sealed record BookedAppointment(string Customer, string Treatment, string Begin, string Date, int DaysAhead)
{
    /// <summary>Die Kalenderkarte heißt „&lt;Beginn&gt; &lt;Kundin&gt;, &lt;Behandlung&gt;, &lt;Status&gt;“.</summary>
    public string CardPrefix => $"{Begin} {Customer}, {Treatment}";
}

/// <summary>Ablauf einer Rolle: ein fester Vorlauf (Anmeldung) und die wählbaren Themen in fester Reihenfolge.</summary>
internal sealed record LiveJourney(string Role, string SetupName, Action<LiveSession> Setup, IReadOnlyList<LiveTopic> Topics)
{
    /// <summary>Die Themen zu den Schlüsseln, in der Reihenfolge des Ablaufs; leer heißt alle.</summary>
    public IReadOnlyList<LiveTopic> Select(IReadOnlyCollection<string> keys)
    {
        var unknown = keys.Where(key => Topics.All(topic => !topic.Key.Equals(key, StringComparison.OrdinalIgnoreCase))).ToList();
        if (unknown.Count > 0)
        {
            throw new ArgumentException($"Unbekannte Themen: {string.Join(", ", unknown)}. Bekannt: {string.Join(", ", Topics.Select(topic => topic.Key))}");
        }

        var selected = keys.Count == 0 ? Topics : Topics.Where(topic => keys.Contains(topic.Key, StringComparer.OrdinalIgnoreCase)).ToList();
        CheckDependencies(selected);
        return selected;
    }

    /// <summary>Wirft mit klarer Meldung, wenn ein Thema ein anderes braucht, das nicht vor ihm läuft.</summary>
    public void CheckDependencies(IReadOnlyList<LiveTopic> selected)
    {
        for (var i = 0; i < selected.Count; i++)
        {
            var missing = (selected[i].Needs ?? [])
                .Where(need => !selected.Take(i).Any(earlier => earlier.Key.Equals(need, StringComparison.OrdinalIgnoreCase)))
                .Select(need => Topics.First(topic => topic.Key.Equals(need, StringComparison.OrdinalIgnoreCase)))
                .ToList();
            if (missing.Count > 0)
            {
                throw new ArgumentException(
                    $"Das Thema „{selected[i].Name}“ ({selected[i].Key}) braucht vorher: {string.Join(", ", missing.Select(topic => $"„{topic.Name}“ ({topic.Key})"))}. Bitte mit auswählen.");
            }
        }
    }

    /// <summary>Führt Vorlauf und Themen aus und liefert den Exitcode von <see cref="LiveSession.Run"/>.</summary>
    public int Run(LiveSession session, IReadOnlyList<LiveTopic> topics) => session.Run(() =>
    {
        var context = new LiveRunContext();
        Setup(session);
        foreach (var topic in topics)
        {
            session.Report.AddEvent($"Thema „{topic.Name}“ ({topic.Key}). Vermerk: {topic.Leaves}");
            topic.Run(session, context);
            context.Done.Add(topic.Key);
        }
    });
}
