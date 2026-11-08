using AispaStudio.Api.Data;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Operations;

/// <summary>Rezeptionshandbuch des Spas: Einträge lesen, abbilden und die Erinnerungen für den Tagesstart auswählen.</summary>
internal static class Handbook
{
    public static HandbookEntryDto ToDto(HandbookEntry e) =>
        new(e.Id, e.Title, e.Text, e.Area, e.RemindAtDayStart, DaysOf(e.Days), e.IsActive, e.UpdatedAt);

    public static IReadOnlyList<DayOfWeek> DaysOf(int mask) => [.. Enum.GetValues<DayOfWeek>().Where(d => (mask & (1 << (int)d)) != 0)];

    public static int MaskOf(IEnumerable<DayOfWeek> days) => days.Distinct().Aggregate(0, (mask, d) => mask | (1 << (int)d));

    /// <summary>Aktive Einträge mit „am Tagesstart erinnern“, die an diesem Wochentag gelten.</summary>
    public static async Task<List<HandbookEntryDto>> RemindersAsync(AispaDbContext db, DateOnly date, CancellationToken ct) =>
        [.. (await db.HandbookEntries.AsNoTracking().Where(e => e.IsActive && e.RemindAtDayStart).OrderBy(e => e.Area).ThenBy(e => e.Title).ToListAsync(ct))
            .Where(e => e.AppliesOn(date.DayOfWeek)).OrderBy(e => e.Area, StringComparer.Ordinal).ThenBy(e => e.Title, StringComparer.Ordinal).Select(ToDto)];
}
