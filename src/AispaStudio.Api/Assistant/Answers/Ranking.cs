namespace AispaStudio.Api.Assistant.Answers;

/// <summary>
/// Ein Eintrag einer Rangliste, nach <paramref name="Key"/> absteigend sortiert: <paramref name="Short"/> steht hinter dem Namen („Massage (5)“),
/// <paramref name="Group"/> in der Zusammenfassung gleicher Werte („2 Terminen“).
/// </summary>
internal sealed record RankedEntry(string Name, string Key, string Short, string Group);

/// <summary>Plätze und Kopfsatz einer Rangliste (Slice 139): gleiche Werte teilen sich den Platz und werden im Kopfsatz zusammengefasst statt aufgezählt.</summary>
internal static class Ranking
{
    private const int MaxGroups = 3;

    /// <summary>Plätze nach Wettbewerbsregel: gleiche Werte teilen den Platz, der nächste folgt nach der Zahl der Gleichen (1, 1, 3).</summary>
    public static IReadOnlyList<int> Ranks(IReadOnlyList<RankedEntry> entries)
    {
        var ranks = new int[entries.Count];
        for (var i = 0; i < entries.Count; i++)
        {
            ranks[i] = i > 0 && entries[i].Key == entries[i - 1].Key ? ranks[i - 1] : i + 1;
        }

        return ranks;
    }

    /// <summary>
    /// Die ersten drei Gruppen gleicher Werte: ein Einzelner als „1. Massage (5)“, mehrere als „7 Behandlungen mit je 2 Terminen“. Gibt es keine Gleichen, stehen die
    /// Einzelnen mit Komma; sonst trennt „danach“ die Gruppen. <paramref name="plural"/> ist die Mehrzahl des Gezählten („Behandlungen“).
    /// </summary>
    public static string Lead(IReadOnlyList<RankedEntry> entries, string plural)
    {
        var ranks = Ranks(entries);
        var parts = new List<string>();
        var anyTie = false;
        for (var i = 0; i < entries.Count && parts.Count < MaxGroups;)
        {
            var size = 1;
            while (i + size < entries.Count && ranks[i + size] == ranks[i])
            {
                size++;
            }

            anyTie |= size > 1;
            parts.Add(size == 1 ? $"{ranks[i]}. {entries[i].Name} ({entries[i].Short})" : $"{size} {plural} mit je {entries[i].Group}");
            i += size;
        }

        return string.Join(anyTie ? ", danach " : ", ", parts);
    }
}
