using AispaStudio.Api.Data;
using AispaStudio.Api.Mail;
using AispaStudio.Api.MailIntake;
using AispaStudio.Api.Scheduling;
using AispaStudio.Api.Tenancy;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Wolverine;

namespace AispaStudio.Api.Online;

/// <summary>
/// Warteliste: eintragen, ansehen, austragen. Freie Termine meldet <see cref="CheckWaitlistHandler"/>, sobald im Kalender etwas
/// storniert wird.
/// </summary>
internal sealed class Waitlist(
    AispaDbContext db,
    OnlineDbContext online,
    OnlinePortal portal,
    AccessLinks links,
    CustomerAccounts accounts,
    IMessageBus bus,
    TenantContext tenants,
    TimeProvider time,
    IOptions<OnlineBookingOptions> options)
{
    /// <summary>Längster Zeitraum eines Eintrags.</summary>
    public const int MaxDays = 60;

    /// <summary>So viele offene Einträge darf eine Kundin oder ein Kunde gleichzeitig haben.</summary>
    public const int MaxActivePerCustomer = 5;

    public abstract record JoinOutcome
    {
        public sealed record Joined(WaitlistConfirmation Confirmation) : JoinOutcome;

        public sealed record Invalid(Errors Errors) : JoinOutcome;

        public sealed record NotFound : JoinOutcome;
    }

    public async Task<JoinOutcome> JoinAsync(WaitlistRequest request, Guid? accountId, CancellationToken ct)
    {
        var location = await db.Locations.AsNoTracking().FirstOrDefaultAsync(l => l.Id == request.LocationId, ct);
        if (location is null
            || !(await OnlineBookingEndpoints.BookableTreatmentsAsync(db, request.LocationId, ct)).Any(t => t.Id == request.TreatmentId))
        {
            return new JoinOutcome.NotFound();
        }

        var account = accountId is { } aid ? await online.Users.FirstOrDefaultAsync(a => a.Id == aid, ct) : null;
        var (first, last, _) = await portal.BookableRangeAsync(location.Id, LocalTime.Zone(location.TimeZone), ct);
        var errors = new Errors();
        errors.Check(request.AcceptPrivacy, nameof(request.AcceptPrivacy), "Bitte den Datenschutzhinweis bestätigen.");
        errors.Check(request.From >= first, nameof(request.From), "Dieser Tag ist online nicht mehr buchbar.");
        errors.Check(request.Until >= request.From, nameof(request.Until), "Das Ende muss nach dem Beginn liegen.");
        errors.Check(request.Until.DayNumber - request.From.DayNumber < MaxDays, nameof(request.Until), $"Höchstens {MaxDays} Tage.");
        errors.Check(request.Until <= last, nameof(request.Until), "So weit im Voraus ist online noch nicht buchbar.");
        errors.Check(Enum.IsDefined(request.DayPart), nameof(request.DayPart), "Unbekannte Tageszeit.");
        if (account?.CustomerId is null)
        {
            CustomerAccounts.ValidateContact(errors, request.Contact, requireEmail: account is null);
        }

        if (errors.Any)
        {
            return new JoinOutcome.Invalid(errors);
        }

        var (customerId, email) = await accounts.ResolveCustomerAsync(account, request.Contact, ct);
        var today = LocalTime.LocalDate(time.GetUtcNow(), LocalTime.Zone(location.TimeZone));
        var active = await online.WaitlistEntries
            .Where(w => w.CustomerId == customerId && w.Status == WaitlistStatus.Waiting && w.Until >= today)
            .ToListAsync(ct);
        errors.Check(
            !active.Any(w => w.LocationId == request.LocationId && w.TreatmentId == request.TreatmentId
                && w.From <= request.Until && request.From <= w.Until),
            nameof(request.TreatmentId), "Für diese Behandlung und diesen Zeitraum stehen Sie schon auf der Warteliste.");
        errors.Check(active.Count < MaxActivePerCustomer, nameof(request.TreatmentId),
            $"Sie stehen schon {MaxActivePerCustomer}-mal auf der Warteliste. Bitte tragen Sie sich zuerst aus einem Eintrag aus.");
        if (errors.Any)
        {
            return new JoinOutcome.Invalid(errors);
        }

        var now = time.GetUtcNow();
        var language = GuestLanguage.Normalize(request.Language);
        var entry = new WaitlistEntry
        {
            CustomerId = customerId,
            CustomerAccountId = account?.Id,
            LocationId = request.LocationId,
            TreatmentId = request.TreatmentId,
            From = request.From,
            Until = request.Until,
            DayPart = request.DayPart,
            Language = language,
            CreatedAt = now,
            PrivacyAcceptedAt = now,
            PrivacyNoticeVersion = options.Value.PrivacyNoticeVersion,
            Status = WaitlistStatus.Waiting,
        };
        online.WaitlistEntries.Add(entry);
        var token = links.Create(AccessLinkPurpose.ManageWaitlist, customerId, null, EndOf(entry, location), entry.Id);
        await online.SaveChangesAsync(ct);

        var dto = (await ToDtoAsync([entry], ct))[0];
        var guest = GuestName.Of(await db.Customers.AsNoTracking().FirstAsync(c => c.Id == customerId, ct), location.TimeZone);
        await bus.PublishGuestMailAsync(tenants, OnlineMails.WaitlistJoined(email, guest, dto, portal.Url($"warteliste/{token}"), language));
        return new JoinOutcome.Joined(new WaitlistConfirmation(dto, token));
    }

    /// <summary>Trägt aus; ein schon erledigter Eintrag bleibt, wie er ist.</summary>
    public async Task<WaitlistEntryDto?> CancelAsync(Guid entryId, Guid customerId, CancellationToken ct)
    {
        var entry = await online.WaitlistEntries.FirstOrDefaultAsync(w => w.Id == entryId && w.CustomerId == customerId, ct);
        if (entry is null)
        {
            return null;
        }

        if (entry.Status == WaitlistStatus.Waiting)
        {
            entry.Status = WaitlistStatus.Cancelled;
            entry.ClosedAt = time.GetUtcNow();
            await online.SaveChangesAsync(ct);
        }

        return (await ToDtoAsync([entry], ct))[0];
    }

    public async Task<WaitlistEntryDto?> FindAsync(Guid entryId, Guid customerId, CancellationToken ct) =>
        await online.WaitlistEntries.AsNoTracking().FirstOrDefaultAsync(w => w.Id == entryId && w.CustomerId == customerId, ct) is { } entry
            ? (await ToDtoAsync([entry], ct))[0]
            : null;

    public async Task<IReadOnlyList<WaitlistEntryDto>> ForCustomerAsync(Guid customerId, CancellationToken ct) =>
        await ToDtoAsync(await online.WaitlistEntries.AsNoTracking()
            .Where(w => w.CustomerId == customerId)
            .OrderByDescending(w => w.CreatedAt)
            .Take(50)
            .ToListAsync(ct), ct);

    /// <summary>Offene Einträge eines Standorts für das Team, nach gewünschtem Beginn.</summary>
    public async Task<IReadOnlyList<WaitlistStaffEntry>?> ForLocationAsync(Guid locationId, CancellationToken ct)
    {
        var location = await db.Locations.AsNoTracking().FirstOrDefaultAsync(l => l.Id == locationId, ct);
        if (location is null)
        {
            return null;
        }

        var today = LocalTime.LocalDate(time.GetUtcNow(), LocalTime.Zone(location.TimeZone));
        var entries = await online.WaitlistEntries.AsNoTracking()
            .Where(w => w.LocationId == locationId && w.Status == WaitlistStatus.Waiting && w.Until >= today)
            .OrderBy(w => w.From).ThenBy(w => w.CreatedAt)
            .Take(200)
            .ToListAsync(ct);
        var customerIds = entries.Select(e => e.CustomerId).Distinct().ToList();
        var customers = (await db.Customers.AsNoTracking().Where(c => customerIds.Contains(c.Id)).ToListAsync(ct)).ToDictionary(c => c.Id);
        var treatments = await TreatmentNamesAsync(entries, ct);
        return [.. entries.Where(e => customers.ContainsKey(e.CustomerId)).Select(e => new WaitlistStaffEntry(
            e.Id, e.CustomerId, customers[e.CustomerId].InHouseName, e.TreatmentId, treatments.GetValueOrDefault(e.TreatmentId, ""),
            e.From, e.Until, e.DayPart, e.Status, e.CreatedAt, e.OfferCount, e.LastOfferedAt, customers[e.CustomerId].Phone))];
    }

    /// <summary>
    /// Die Rezeption nimmt einen offenen Eintrag von der Liste: „vereinbart“ zählt als gebucht, „braucht keinen mehr“ wie ausgetragen. Der Gast
    /// bekommt dann keine Mails mehr zu freien Terminen. Ein schon geschlossener Eintrag bleibt, wie er ist. <c>null</c>: unbekannt oder kein Zugang zum Standort.
    /// </summary>
    public async Task<WaitlistEntryDto?> CloseAsync(Guid entryId, WaitlistCloseReason reason, Func<Guid, bool> canSee, CancellationToken ct)
    {
        var entry = await online.WaitlistEntries.FirstOrDefaultAsync(w => w.Id == entryId, ct);
        // Der Standort gehört zum Spa der angemeldeten Person (Mandantenfilter), sonst gibt es den Eintrag für sie nicht.
        if (entry is null || !canSee(entry.LocationId) || !await db.Locations.AnyAsync(l => l.Id == entry.LocationId, ct))
        {
            return null;
        }

        if (entry.Status == WaitlistStatus.Waiting)
        {
            entry.Status = reason == WaitlistCloseReason.Arranged ? WaitlistStatus.Booked : WaitlistStatus.Cancelled;
            entry.ClosedAt = time.GetUtcNow();
            await online.SaveChangesAsync(ct);
        }

        return (await ToDtoAsync([entry], ct))[0];
    }

    public async Task<IReadOnlyList<WaitlistEntryDto>> ToDtoAsync(IReadOnlyList<WaitlistEntry> entries, CancellationToken ct)
    {
        if (entries.Count == 0)
        {
            return [];
        }

        var locationIds = entries.Select(e => e.LocationId).Distinct().ToList();
        var locations = await db.Locations.AsNoTracking().Where(l => locationIds.Contains(l.Id)).ToDictionaryAsync(l => l.Id, ct);
        var treatments = await TreatmentNamesAsync(entries, ct);
        var now = time.GetUtcNow();
        return [.. entries.Where(e => locations.ContainsKey(e.LocationId)).Select(e =>
        {
            var location = locations[e.LocationId];
            var expired = e.Status == WaitlistStatus.Waiting && LocalTime.LocalDate(now, LocalTime.Zone(location.TimeZone)) > e.Until;
            return new WaitlistEntryDto(
                e.Id, e.LocationId, location.Name, e.TreatmentId, treatments.GetValueOrDefault(e.TreatmentId, ""), e.From, e.Until,
                e.DayPart, expired ? WaitlistStatus.Expired : e.Status, e.CreatedAt, e.LastOfferedAt);
        })];
    }

    /// <summary>Liegt eine Startzeit in der gewünschten Tageszeit (Ortszeit des Standorts)?</summary>
    public static bool Matches(DayPart part, DateTimeOffset start, TimeZoneInfo zone)
    {
        var hour = TimeZoneInfo.ConvertTime(start, zone).Hour;
        return part switch
        {
            DayPart.Morning => hour < 12,
            DayPart.Afternoon => hour is >= 12 and < 17,
            DayPart.Evening => hour >= 17,
            _ => true,
        };
    }

    /// <summary>
    /// Die Zeit, die ein Wunsch an einem Tag meint, in UTC (Beginn, Ende ausschließlich): vormittags bis 12 Uhr, nachmittags 12 bis 17 Uhr, abends ab 17 Uhr in Ortszeit des Standorts,
    /// ohne Tageszeit der ganze Tag. Dieselben Grenzen wie <see cref="Matches"/>.
    /// </summary>
    public static (DateTimeOffset From, DateTimeOffset Until) Window(DayPart part, DateOnly day, TimeZoneInfo zone)
    {
        var (start, end) = part switch
        {
            DayPart.Morning => (TimeOnly.MinValue, new TimeOnly(12, 0)),
            DayPart.Afternoon => (new TimeOnly(12, 0), new TimeOnly(17, 0)),
            DayPart.Evening => (new TimeOnly(17, 0), (TimeOnly?)null),
            _ => (TimeOnly.MinValue, (TimeOnly?)null),
        };
        return (LocalTime.ToUtc(day, start, zone), end is { } until ? LocalTime.ToUtc(day, until, zone) : LocalTime.ToUtc(day.AddDays(1), TimeOnly.MinValue, zone));
    }

    /// <summary>Der Link zum Eintrag gilt bis eine Woche nach dem letzten gewünschten Tag.</summary>
    private static DateTimeOffset EndOf(WaitlistEntry entry, Location location) =>
        LocalTime.ToUtc(entry.Until.AddDays(8), TimeOnly.MinValue, LocalTime.Zone(location.TimeZone));

    private async Task<Dictionary<Guid, string>> TreatmentNamesAsync(IReadOnlyList<WaitlistEntry> entries, CancellationToken ct)
    {
        var ids = entries.Select(e => e.TreatmentId).Distinct().ToList();
        return await db.Treatments.AsNoTracking().Where(t => ids.Contains(t.Id)).ToDictionaryAsync(t => t.Id, t => t.Name, ct);
    }
}
