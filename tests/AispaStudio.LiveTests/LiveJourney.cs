using System.Globalization;

namespace AispaStudio.LiveTests;

/// <summary>Ein wählbares Thema eines Ablaufs; <see cref="Key"/> ist der Name für <c>--topics</c>.</summary>
internal sealed record LiveTopic(string Key, string Name, string Description, Action<LiveSession, LiveRunContext> Run);

/// <summary>Ergebnis eines Schritts für die Anzeige in Konsole und Starter-App.</summary>
internal sealed record LiveStepResult(int Number, string Name, TimeSpan Duration, bool Passed, string? Error, string Image);

/// <summary>Was die Themen eines Laufs miteinander teilen.</summary>
internal sealed class LiveRunContext
{
    /// <summary>Zeitstempel des Laufs; macht Namen und Telefon eindeutig, sonst meldet die Kundenmaske eine Dublette.</summary>
    public string Stamp { get; } = DateTime.Now.ToString("MMddHHmmss", CultureInfo.InvariantCulture);

    /// <summary>Name der Kundin, die der Lauf angelegt hat; null, wenn das Thema „Kundin anlegen“ nicht lief.</summary>
    public string? CustomerName { get; set; }
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

        return keys.Count == 0 ? Topics : Topics.Where(topic => keys.Contains(topic.Key, StringComparer.OrdinalIgnoreCase)).ToList();
    }

    /// <summary>Führt Vorlauf und Themen aus und liefert den Exitcode von <see cref="LiveSession.Run"/>.</summary>
    public int Run(LiveSession session, IReadOnlyList<LiveTopic> topics) => session.Run(() =>
    {
        var context = new LiveRunContext();
        Setup(session);
        foreach (var topic in topics)
        {
            session.Report.AddEvent($"Thema „{topic.Name}“");
            topic.Run(session, context);
        }
    });
}
