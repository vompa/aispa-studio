namespace AispaStudio.Api.Scheduling;

// ---------- Notiz zu einem einzelnen Termin ändern ----------
internal sealed partial class BookingService
{
    /// <summary>Längste Notiz zu einem Termin (Design-Brief 5.16).</summary>
    public const int MaxNoteLength = 500;

    /// <summary>
    /// Setzt die Notiz des Termins (Wunsch oder Hinweis zu genau diesem Termin, nicht die Kundennotiz). Ein leerer Text löscht sie. Die Notiz belegt nichts
    /// und lässt sich auch bei erledigten und stornierten Terminen nachtragen; die Live-Meldung bringt sie in alle offenen Kalender.
    /// </summary>
    public async Task<BookingOutcome> ChangeNoteAsync(Guid id, string? note, CancellationToken ct, uint? expectedVersion = null)
    {
        var text = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        if (text is { Length: > MaxNoteLength })
        {
            return new BookingOutcome.Invalid(nameof(note), $"Höchstens {MaxNoteLength} Zeichen.");
        }

        var appointment = await LoadAsync(id, ct);
        if (appointment is null)
        {
            return new BookingOutcome.NotFound("Termin");
        }

        if (StaleOf(appointment, expectedVersion) is { } stale)
        {
            return stale;
        }

        if (appointment.Note == text)
        {
            return new BookingOutcome.Success(ToDto(appointment, appointment.Treatment.Name));
        }

        appointment.Note = text;
        appointment.UpdatedAt = time.GetUtcNow();
        if (!await TrySaveAsync(ct))
        {
            return new BookingOutcome.Rejected(TakenMeanwhile);
        }

        return await PublishAsync(appointment, appointment.Treatment.Name);
    }
}
