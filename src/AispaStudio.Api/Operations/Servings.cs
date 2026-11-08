using AispaStudio.Api.Data;
using AispaStudio.Api.Online;
using AispaStudio.Api.Scheduling;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Operations;

/// <summary>
/// „Bereitstellen“ eines Tages: aus den Extras der Behandlungen entsteht je Termin ein Eintrag mit Uhrzeit, den das Team abhakt. Dazu die Wünsche
/// des Gastes von der Kundenkarte (freier Text, z. B. Teesorte) und der Hinweis, dass ein Gesundheitsfragebogen vorliegt – nie dessen Inhalt.
/// </summary>
internal sealed class ServingService(AispaDbContext db, OnlineDbContext online)
{
    private static readonly AppointmentStatus[] Gone = [AppointmentStatus.Cancelled, AppointmentStatus.NoShow];

    public async Task<ServingDayDto?> DayAsync(Guid locationId, DateOnly date, CancellationToken ct)
    {
        var timeZone = await db.Locations.Where(l => l.Id == locationId).Select(l => l.TimeZone).FirstOrDefaultAsync(ct);
        if (timeZone is null)
        {
            return null;
        }

        var (from, until) = LocalTime.Day(date, LocalTime.Zone(timeZone));
        var appointments = await db.Appointments.AsNoTracking().Include(a => a.Customer).Include(a => a.Treatment)
            .Where(a => a.LocationId == locationId && a.Start >= from && a.Start < until && !Gone.Contains(a.Status))
            .OrderBy(a => a.Start).ToListAsync(ct);
        var treatmentIds = appointments.Select(a => a.TreatmentId).Distinct().ToList();
        var appointmentIds = appointments.Select(a => a.Id).ToList();
        var customerIds = appointments.Select(a => a.CustomerId).Distinct().ToList();
        var extras = (await db.TreatmentExtras.AsNoTracking().Where(x => treatmentIds.Contains(x.TreatmentId)).OrderBy(x => x.Position).ToListAsync(ct))
            .ToLookup(x => x.TreatmentId);
        var checks = (await db.AppointmentExtraChecks.AsNoTracking().Where(c => appointmentIds.Contains(c.AppointmentId)).ToListAsync(ct))
            .ToDictionary(c => (c.AppointmentId, c.ExtraId));
        var userIds = checks.Values.Select(c => c.UserId).OfType<string>().Distinct().ToList();
        var users = await db.Users.AsNoTracking().Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.DisplayName, ct);
        var rooms = await db.Rooms.AsNoTracking().Where(r => r.LocationId == locationId).ToDictionaryAsync(r => r.Id, r => r.Name, ct);
        var wishes = await db.CustomerPreferences.AsNoTracking().Where(p => customerIds.Contains(p.CustomerId))
            .ToDictionaryAsync(p => p.CustomerId, p => p.Other, ct);
        var withAnamnesis = (await online.AnamnesisRecords.AsNoTracking().Where(r => customerIds.Contains(r.CustomerId))
            .Select(r => r.CustomerId).Distinct().ToListAsync(ct)).ToHashSet();

        var items = new List<ServingItemDto>();
        foreach (var a in appointments)
        {
            var wish = wishes.GetValueOrDefault(a.CustomerId);
            foreach (var extra in extras[a.TreatmentId])
            {
                checks.TryGetValue((a.Id, extra.Id), out var check);
                items.Add(new(a.Id, extra.Id, extra.Name, DueAt(a, extra), a.Treatment.Name, a.RoomId, rooms.GetValueOrDefault(a.RoomId, ""),
                    a.CustomerId, a.Customer.InHouseName, string.IsNullOrWhiteSpace(wish) ? null : wish.Trim(), withAnamnesis.Contains(a.CustomerId),
                    check is not null, check?.CheckedAt, check?.UserId is { } id ? users.GetValueOrDefault(id) : null));
            }
        }

        items = [.. items.OrderBy(i => i.DueAt).ThenBy(i => i.Text, StringComparer.Ordinal).ThenBy(i => i.CustomerName, StringComparer.Ordinal)];
        return new(locationId, date, items, Totals(items));
    }

    /// <summary>Zeitpunkt, zu dem das Extra bereitstehen soll.</summary>
    internal static DateTimeOffset DueAt(Appointment appointment, TreatmentExtra extra) => extra.Timing switch
    {
        ExtraTiming.BeforeEnd => appointment.End.AddMinutes(-(extra.MinutesBeforeEnd ?? 0)),
        ExtraTiming.AfterTreatment => appointment.End,
        _ => appointment.Start,
    };

    /// <summary>Summe je Bezeichnung (Groß- und Kleinschreibung gleich), alphabetisch.</summary>
    internal static List<ServingTotalDto> Totals(IEnumerable<ServingItemDto> items) =>
        [.. items.GroupBy(i => i.Text.Trim(), StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .Select(g => new ServingTotalDto(g.Key, g.Count(), [.. g.Select(i => i.DueAt).Order()]))];
}
