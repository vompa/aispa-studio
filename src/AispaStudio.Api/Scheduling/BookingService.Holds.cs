using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Scheduling;

// ---------- Vormerkung aus dem Posteingang (ADR 0083): vormerken, bestätigen, freigeben ----------
internal sealed partial class BookingService
{
    /// <summary>Grund am Termin, wenn die Rezeption eine Vormerkung freigibt.</summary>
    public const string HoldReleasedReason = "Vormerkung freigegeben";

    /// <summary>
    /// Merkt einen eben gebuchten Termin für die Anfrage <paramref name="mailId"/> vor und rechnet, ab wann der Kalender ohne Antwort des Gastes erinnert
    /// (<see cref="HoldReminder"/>, Tagesstart = erste Öffnung des Standorts an dem Tag).
    /// </summary>
    public async Task<BookingOutcome> MarkHeldAsync(Guid id, Guid mailId, CancellationToken ct)
    {
        var appointment = await LoadAsync(id, ct);
        if (appointment is null)
        {
            return new BookingOutcome.NotFound("Termin");
        }

        var location = await db.Locations.AsNoTracking().Include(l => l.OpeningHours).FirstAsync(l => l.Id == appointment.LocationId, ct);
        var zone = LocalTime.Zone(location.TimeZone);
        var now = time.GetUtcNow();
        appointment.HeldSince = now;
        appointment.HoldMailId = mailId;
        appointment.HoldAnsweredAt = null;
        appointment.HoldRemindAt = HoldReminder.RemindAt(now, appointment.Start, zone,
            day => ResourcePlanner.OpeningWindows(location.OpeningHours, zone, day, null).Select(w => (DateTimeOffset?)w.Start).Min());
        appointment.UpdatedAt = now;
        if (!await TrySaveAsync(ct))
        {
            return new BookingOutcome.Rejected(new BookingRejected("Der Termin wurde gerade von jemand anderem geändert.", []));
        }

        return await PublishAsync(appointment, appointment.Treatment.Name);
    }

    /// <summary>Der Gast hat auf den Vorschlag geantwortet: ab jetzt keine Erinnerung mehr; die Rezeption bestätigt oder gibt frei.</summary>
    public async Task NoteHoldAnsweredAsync(Guid id, DateTimeOffset answeredAt, CancellationToken ct)
    {
        var appointment = await LoadAsync(id, ct);
        if (appointment is { HeldSince: not null, HoldAnsweredAt: null })
        {
            appointment.HoldAnsweredAt = answeredAt;
            appointment.UpdatedAt = time.GetUtcNow();
            if (await TrySaveAsync(ct))
            {
                await PublishAsync(appointment, appointment.Treatment.Name);
            }
        }
    }

    /// <summary>
    /// Bestätigen: der Termin ist fest gebucht, die Anfragen dazu stehen auf „Gebucht“. <c>Redraft</c> sind die Anfragen, deren Antwort noch nicht
    /// gesendet ist; für sie wird die Bestätigung an den Gast neu entworfen.
    /// </summary>
    public async Task<(BookingOutcome Outcome, List<Guid> Redraft)> ConfirmHoldAsync(Guid id, CancellationToken ct)
    {
        var appointment = await LoadAsync(id, ct);
        if (appointment is null)
        {
            return (new BookingOutcome.NotFound("Termin"), []);
        }

        if (appointment.HeldSince is null)
        {
            return (new BookingOutcome.Invalid("hold", "Der Termin ist nicht vorgemerkt."), []);
        }

        appointment.HeldSince = null;
        appointment.HoldRemindAt = null;
        appointment.HoldAnsweredAt = null;
        appointment.UpdatedAt = time.GetUtcNow();
        if (!await TrySaveAsync(ct))
        {
            return (new BookingOutcome.Rejected(new BookingRejected("Der Termin wurde gerade von jemand anderem geändert.", [])), []);
        }

        var mails = await db.InboundMails.Where(m => m.AppointmentId == id && m.Status != InboundMailStatus.Booked).ToListAsync(ct);
        foreach (var mail in mails)
        {
            mail.Status = InboundMailStatus.Booked;
            mail.HeldStart = null;
            mail.Proposals = [];
            mail.Note = null;
        }

        await db.SaveChangesAsync(ct);
        return (await PublishAsync(appointment, appointment.Treatment.Name), [.. mails.Where(m => m.ReplyStatus is not (ReplyStatus.Sending or ReplyStatus.Sent)).Select(m => m.Id)]);
    }

    /// <summary>Freigeben: der Termin wird ohne Nachricht an den Gast storniert, die Anfragen dazu stehen wieder auf „Prüfen“.</summary>
    public async Task<BookingOutcome> ReleaseHoldAsync(Guid id, CancellationToken ct)
    {
        var appointment = await LoadAsync(id, ct);
        if (appointment is null)
        {
            return new BookingOutcome.NotFound("Termin");
        }

        if (appointment.HeldSince is null)
        {
            return new BookingOutcome.Invalid("hold", "Der Termin ist nicht vorgemerkt.");
        }

        appointment.HeldSince = null;
        appointment.HoldRemindAt = null;
        var outcome = await CancelAsync(id, HoldReleasedReason, ct);
        if (outcome is not BookingOutcome.Success)
        {
            return outcome;
        }

        await db.InboundMails.Where(m => m.AppointmentId == id).ExecuteUpdateAsync(s => s
            .SetProperty(m => m.AppointmentId, (Guid?)null)
            .SetProperty(m => m.HeldStart, (DateTimeOffset?)null)
            .SetProperty(m => m.Status, InboundMailStatus.NeedsReview)
            .SetProperty(m => m.Note, "Vormerkung freigegeben; die Zeit ist wieder frei."), ct);
        return outcome;
    }
}
