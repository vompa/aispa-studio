using AispaStudio.Api.Data;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Online;

/// <summary>
/// Schutz vor wiederholtem Nichterscheinen und gehorteten Online-Terminen. Wer in den letzten zwölf Monaten zweimal nicht
/// erschienen ist, gilt für Ausfallgebühr und Anzahlung nicht mehr als Stammgast; verlangt der Standort beides nicht, bucht er
/// nur noch telefonisch. Online gebuchte offene Termine sind je Gast begrenzt.
/// </summary>
internal static class NoShowHistory
{
    public const int RepeatNoShows = 2;
    public const int MaxOpenOnlineBookings = 3;

    public static async Task<bool> IsRepeatAsync(AispaDbContext db, Guid customerId, DateTimeOffset now, CancellationToken ct) =>
        await db.Appointments.CountAsync(a => a.CustomerId == customerId && a.Status == AppointmentStatus.NoShow && a.Start > now.AddYears(-1), ct)
        >= RepeatNoShows;

    /// <summary>Offene, online gebuchte Termine des Gasts; eine gemeinsame Buchung (Paar, Spa-Tag) zählt einmal.</summary>
    public static async Task<int> OpenOnlineBookingsAsync(AispaDbContext db, Guid customerId, DateTimeOffset now, CancellationToken ct) =>
        await db.Appointments
            .Where(a => a.CustomerId == customerId && a.Source == AppointmentSource.Online && a.Status == AppointmentStatus.Booked && a.Start > now)
            .Select(a => a.GroupId ?? a.Id).Distinct().CountAsync(ct);
}
