namespace AispaStudio.Contracts;

/// <summary>Eine Schließzeit für die Regel: Zeitraum in UTC und die Art in Worten („Betriebsurlaub“) für die Meldung.</summary>
public sealed record ClosedRange(DateTimeOffset From, DateTimeOffset Until, string Title);

/// <summary>
/// <c>Open</c>: die Zeit liegt ganz in einem Öffnungsfenster. <c>ClosedDay</c>: Schließtag (kein Öffnungsfenster oder niemand im Dienst); die Rezeption trägt trotzdem ein.
/// <c>Closed</c>: außerhalb der Öffnungszeit oder in einer Schließzeit; abgelehnt.
/// </summary>
public enum OpeningState
{
    Open,
    ClosedDay,
    Closed,
}

/// <summary>
/// Die Regel „offen, Schließtag oder geschlossen“ an einer Stelle (Slice 78): der Raumplaner auf dem Server und die Vorabprüfung in der Windows-App rechnen damit,
/// damit die App dieselbe Antwort sofort und ohne Aufruf geben kann. Reine Funktionen ohne Datenbank, alle Zeiten in UTC (ADR 0073, Nachtrag Runde R03).
/// </summary>
public static class OpeningRules
{
    /// <summary>Die Öffnungsfenster eines Tages ohne die Schließzeiten: ein halber Tag Betriebsurlaub lässt nur den Rest offen.</summary>
    public static IReadOnlyList<TimeRange> Windows(IEnumerable<TimeRange> opening, IEnumerable<ClosedRange> closed)
    {
        IEnumerable<TimeRange> parts = [.. opening.OrderBy(o => o.Start)];
        foreach (var span in closed)
        {
            parts = parts.SelectMany(part => Cut(part, span)).ToList();
        }

        return [.. parts];
    }

    /// <summary>Die Zeit liegt ganz in einem Öffnungsfenster.</summary>
    public static bool IsOpen(IEnumerable<TimeRange> windows, DateTimeOffset from, DateTimeOffset until) =>
        windows.Any(w => w.Start <= from && until <= w.End);

    /// <summary>
    /// Schließtag nur für die Rezeption (<paramref name="house"/>): kein Öffnungsfenster am Tag, oder niemand hat Dienst und die Zeit liegt in der Öffnungszeit.
    /// Ein halber Tag Schließzeit und eine Uhrzeit außerhalb der Öffnungszeit bleiben geschlossen.
    /// </summary>
    public static OpeningState Check(IReadOnlyList<TimeRange> windows, bool anyShift, bool house, DateTimeOffset from, DateTimeOffset until)
    {
        var open = IsOpen(windows, from, until);
        if (house && (windows.Count == 0 || (!anyShift && open)))
        {
            return OpeningState.ClosedDay;
        }

        return open ? OpeningState.Open : OpeningState.Closed;
    }

    /// <summary>Warum ein Termin nicht passt, wenn der Standort geschlossen ist: wegen einer Schließzeit mit ihrer Art, sonst wegen der Öffnungszeiten.</summary>
    public static string ClosedMessage(IEnumerable<ClosedRange> closed, DateTimeOffset from, DateTimeOffset until) =>
        closed.FirstOrDefault(c => c.From < until && from < c.Until) is { } hit
            ? $"Der Standort ist in dieser Zeit geschlossen ({hit.Title})."
            : "Der Standort hat zu dieser Zeit geschlossen.";

    private static IEnumerable<TimeRange> Cut(TimeRange part, ClosedRange span)
    {
        if (span.Until <= part.Start || part.End <= span.From)
        {
            yield return part;
            yield break;
        }

        if (part.Start < span.From)
        {
            yield return new TimeRange(part.Start, span.From);
        }

        if (span.Until < part.End)
        {
            yield return new TimeRange(span.Until, part.End);
        }
    }
}
