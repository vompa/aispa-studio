using AispaStudio.Api.Data;
using AispaStudio.Api.Mail;
using AispaStudio.Api.MailIntake;
using AispaStudio.Api.Scheduling;
using AispaStudio.Api.Tenancy;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;
using Wolverine;
using Wolverine.Attributes;

namespace AispaStudio.Api.Online;

/// <summary>Nachricht in der Queue: ein Termin hat sich geändert; die Warteliste des Standorts prüfen.</summary>
public sealed record CheckWaitlist(Guid LocationId, Guid TreatmentId, Guid CustomerId, Guid AppointmentId, DateTimeOffset Start, AppointmentStatus Status);

/// <summary>
/// Hängt die Warteliste an jede Terminänderung, ohne die Verteilung an die Kalender aufzuhalten: hier wird nur eine eigene
/// Nachricht verschickt, die Arbeit macht <see cref="CheckWaitlistHandler"/>.
/// </summary>
public static class WaitlistAppointmentChangedHandler
{
    public static async ValueTask Handle(AppointmentChangedMessage message, IMessageBus bus)
    {
        var a = message.Appointment;
        if (a.Status is AppointmentStatus.Booked or AppointmentStatus.Cancelled)
        {
            await bus.PublishAsync(new CheckWaitlist(a.LocationId, a.TreatmentId, a.CustomerId, a.Id, a.Start, a.Status));
        }

        // Verschoben oder an einen anderen Standort verlegt: die alte Zeit am alten Standort ist frei wie nach einem Storno.
        var movedAway = message.PreviousLocationId is { } from && from != a.LocationId;
        if (a.Status == AppointmentStatus.Booked && (message.PreviousStart is not null || movedAway))
        {
            await bus.PublishAsync(new CheckWaitlist(message.PreviousLocationId ?? a.LocationId, a.TreatmentId, a.CustomerId, a.Id,
                message.PreviousStart ?? a.Start, AppointmentStatus.Cancelled));
        }
    }
}

/// <summary>
/// Wird ein Termin storniert oder verschoben (alte Zeit), bekommen alle passenden Einträge für diesen Tag eine Mail mit den freien Zeiten und einem Link,
/// der die Buchung mit Standort, Behandlung und Tag öffnet. Wer zuerst bucht, bekommt den Termin; die Buchung selbst prüft
/// wie immer, ob die Zeit noch frei ist. Bucht eine Kundin oder ein Kunde einen passenden Termin, ist der Eintrag erledigt.
/// </summary>
// Nicht wiederholen: die Mails gehen vor dem Speichern von LastOfferedAt in die Queue, ein zweiter Lauf schickte sie noch einmal. Der nächste frei gewordene Termin bietet wieder an.
[MoveToErrorQueueOn(typeof(Exception))]
public static partial class CheckWaitlistHandler
{
    /// <summary>Höchstens so viele Mails je frei gewordenem Termin, die ältesten Einträge zuerst.</summary>
    public const int MaxMailsPerCheck = 10;

    /// <summary>Höchstens so viele Zeiten in einer Mail.</summary>
    public const int MaxTimesPerMail = 6;

    /// <summary>Derselbe Tag wird einem Eintrag frühestens nach dieser Zeit erneut gemeldet.</summary>
    public static readonly TimeSpan RepeatAfter = TimeSpan.FromHours(12);

    // Die Dienste kommen aus einem eigenen Scope, das kann Wolverine bei diesen DbContexten nicht selbst. Außerhalb einer Anfrage
    // liest AispaDbContext über alle Mandanten; der Standort grenzt alles auf sein Spa ein, geschrieben wird dort nichts.
    public static async Task Handle(CheckWaitlist message, IServiceScopeFactory scopes, CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var db = services.GetRequiredService<AispaDbContext>();
        var online = services.GetRequiredService<OnlineDbContext>();
        var time = services.GetRequiredService<TimeProvider>();

        var location = await db.Locations.AsNoTracking().FirstOrDefaultAsync(l => l.Id == message.LocationId, ct);
        if (location is null)
        {
            return;
        }

        var zone = LocalTime.Zone(location.TimeZone);
        var date = LocalTime.LocalDate(message.Start, zone);
        var now = time.GetUtcNow();
        if (message.Status == AppointmentStatus.Booked)
        {
            await online.WaitlistEntries
                .Where(w => w.CustomerId == message.CustomerId && w.LocationId == message.LocationId && w.TreatmentId == message.TreatmentId
                    && w.Status == WaitlistStatus.Waiting && w.From <= date && date <= w.Until)
                .ExecuteUpdateAsync(u => u
                    .SetProperty(w => w.Status, WaitlistStatus.Booked)
                    .SetProperty(w => w.AppointmentId, message.AppointmentId)
                    .SetProperty(w => w.ClosedAt, now), ct);
            return;
        }

        var portal = services.GetRequiredService<OnlinePortal>();
        var (first, last, earliest) = await portal.BookableRangeAsync(message.LocationId, zone, ct);
        if (date < first || date > last)
        {
            return;
        }

        var repeatFrom = now - RepeatAfter;
        var entries = await online.WaitlistEntries
            .Where(w => w.LocationId == message.LocationId && w.Status == WaitlistStatus.Waiting && w.From <= date && date <= w.Until
                && w.CustomerId != message.CustomerId
                && !(w.LastOfferedDate == date && w.LastOfferedAt > repeatFrom))
            .OrderBy(w => w.CreatedAt)
            .Take(100)
            .ToListAsync(ct);
        if (entries.Count == 0)
        {
            return;
        }

        var offered = (await OnlineBookingEndpoints.BookableTreatmentsAsync(db, message.LocationId, ct)).ToDictionary(t => t.Id);
        var freeTimes = new Dictionary<Guid, IReadOnlyList<DateTimeOffset>>();
        foreach (var treatmentId in entries.Select(e => e.TreatmentId).Distinct().Where(offered.ContainsKey))
        {
            freeTimes[treatmentId] = await portal.FreeTimesAsync(message.LocationId, treatmentId, date, earliest, ct) ?? [];
        }

        var customerIds = entries.Select(e => e.CustomerId).Distinct().ToList();
        var customers = await db.Customers.AsNoTracking().Where(c => customerIds.Contains(c.Id) && c.Email != null)
            .ToDictionaryAsync(c => c.Id, ct);
        var tenantId = await TenantModel.TenantOfAsync(db.Locations.Where(l => l.Id == location.Id), ct);
        var bus = services.GetRequiredService<IMessageBus>();
        var logger = services.GetRequiredService<ILogger<WaitlistEntry>>();
        var links = services.GetRequiredService<AccessLinks>();
        var sent = 0;
        foreach (var entry in entries)
        {
            if (sent == MaxMailsPerCheck)
            {
                break;
            }

            var times = freeTimes.GetValueOrDefault(entry.TreatmentId, [])
                .Where(t => Waitlist.Matches(entry.DayPart, t, zone)).Take(MaxTimesPerMail).ToList();
            if (times.Count == 0 || !customers.TryGetValue(entry.CustomerId, out var customer))
            {
                continue;
            }

            entry.OfferCount++;
            entry.LastOfferedAt = now;
            entry.LastOfferedDate = date;
            var token = links.Create(AccessLinkPurpose.ManageWaitlist, entry.CustomerId, null,
                LocalTime.ToUtc(entry.Until.AddDays(8), TimeOnly.MinValue, zone), entry.Id);
            var bookUrl = portal.Url($"?standort={location.Id}&behandlung={entry.TreatmentId}&datum={date:yyyy-MM-dd}", tenantId);
            var offer = OnlineMails.WaitlistOffer(
                customer.Email!, GuestName.Of(customer, location.TimeZone), offered[entry.TreatmentId].Name, location.Name, location.TimeZone, times,
                bookUrl, portal.Url($"warteliste/{token}"), entry.Language);
            await (tenantId is { } spa ? bus.PublishAsync(new SendSpaMail(spa, offer)) : bus.PublishAsync(new SendMail(offer)));
            sent++;
        }

        await online.SaveChangesAsync(ct);
        if (sent > 0)
        {
            Offered(logger, sent, date, message.LocationId);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Warteliste: {Count} Mail(s) für freie Termine am {Date} an Standort {LocationId}.")]
    private static partial void Offered(ILogger logger, int count, DateOnly date, Guid locationId);
}
