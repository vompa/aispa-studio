using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>
/// Sucht freie Zeiten über mehrere Tage: der Server liefert den Tag ganz und bei <c>minSlots</c> auch Folgetage, bis genug da sind;
/// was nach dem Wunsch (Tageszeit, Therapeutin/Therapeut, ab jetzt) wegfällt, wird mit den nächsten Tagen nachgeholt.
/// Gemeinsam für „Freie Zeit suchen“ und „Frei: …“ an den Einträgen der Warteliste.
/// </summary>
internal static class FreeTimeFinder
{
    /// <summary>
    /// Die ersten <paramref name="wanted"/> Zeiten ab <paramref name="from"/> in höchstens <paramref name="span"/> Tagen, die <paramref name="keep"/> behält.
    /// <paramref name="extraQuery"/> hängt Person, Raum und Gast an („&amp;employeeId=…“). Gibt es weniger, sind alle Tage durchsucht.
    /// Ergebnis ohne Zeiten und ohne Fehler heißt: <paramref name="isCurrent"/> sagt, dass eine neuere Suche läuft.
    /// </summary>
    public static async Task<(List<AvailableSlot>? Slots, string? Error)> FindAsync(IWorkstationApi api, Guid locationId, Guid treatmentId, DateOnly from, int span,
        string extraQuery, int wanted, Func<AvailableSlot, bool> keep, Func<DateTimeOffset, DateOnly> dayOf, Func<bool> isCurrent)
    {
        var found = new List<AvailableSlot>();
        var last = from.AddDays(span);
        var start = from;
        while (start < last)
        {
            var requested = Math.Max(1, wanted - found.Count);
            var url = $"api/locations/{locationId}/availability?treatmentId={treatmentId}&date={start:yyyy-MM-dd}{extraQuery}"
                + $"&minSlots={requested}&days={last.DayNumber - start.DayNumber}";
            var result = await api.GetAsync<List<AvailableSlot>>(url).ConfigureAwait(true);
            if (!isCurrent())
            {
                return (null, null);
            }

            if (result.Value is null)
            {
                return (null, api.IsOffline ? "Ohne Verbindung lassen sich freie Zeiten nicht suchen." : result.Error ?? "Die Suche hat nicht geklappt.");
            }

            found.AddRange(result.Value.Where(keep));
            // Lieferte der Server weniger als verlangt, hat er schon alle Tage durchsucht.
            if (found.Count >= wanted || result.Value.Count < requested)
            {
                break;
            }

            // Alles Gefundene fiel weg (z. B. falsche Tageszeit): ab dem Tag nach der letzten Zeit weitersuchen.
            start = dayOf(result.Value.Max(s => s.Start)).AddDays(1);
        }

        return (found, null);
    }
}
