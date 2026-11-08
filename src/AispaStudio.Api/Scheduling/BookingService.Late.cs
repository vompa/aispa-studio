using AispaStudio.Contracts;

namespace AispaStudio.Api.Scheduling;

// ---------- Verspätung: der Gast kommt später; der Termin bleibt an seiner Zeit, nur die Markierung wird gesetzt ----------
internal sealed partial class BookingService
{
    /// <summary>Längste gemeldete Verspätung; wer später kommt, bekommt einen neuen Termin.</summary>
    public const int MaxLateMinutes = 120;

    public async Task<BookingOutcome> ReportLateAsync(Guid id, int minutes, CancellationToken ct)
    {
        if (minutes is < 0 or > MaxLateMinutes)
        {
            return new BookingOutcome.Invalid(nameof(minutes), $"Die Verspätung muss zwischen 0 und {MaxLateMinutes} Minuten liegen.");
        }

        var appointment = await LoadAsync(id, ct);
        if (appointment is null)
        {
            return new BookingOutcome.NotFound("Termin");
        }

        if (appointment.Status != AppointmentStatus.Booked)
        {
            return new BookingOutcome.Invalid(nameof(appointment.Status), "Verspätung geht nur bei Terminen, die noch nicht begonnen haben.");
        }

        appointment.LateMinutes = minutes == 0 ? null : minutes;
        appointment.UpdatedAt = time.GetUtcNow();
        if (!await TrySaveAsync(ct))
        {
            return new BookingOutcome.Rejected(new BookingRejected("Der Termin wurde gerade von jemand anderem geändert.", []));
        }

        return await PublishAsync(appointment, appointment.Treatment.Name);
    }
}
