using AispaStudio.Api.Data;
using AispaStudio.Api.Online;
using AispaStudio.Api.Scheduling;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Assistant.Answers;

/// <summary>
/// „Fällt eine Gebühr an?“ (Ä317): ob eine Absage jetzt etwas kostet, für den offenen Termin oder den nächsten Termin des genannten Gastes. Innerhalb der Stornofrist
/// gilt der Prozentsatz der Behandlung, sonst der des Standorts, bei „nur neue Gäste“ nicht für Stammgäste (wie <see cref="CardGuarantees.PercentForAsync"/>, aber
/// unabhängig vom Zahlungsanbieter). Den Betrag sieht nur, wer buchen darf. Ob eine Karte hinterlegt ist, steht dabei; gebucht wird nichts.
/// </summary>
internal sealed class CancellationFeeAnswers(AispaDbContext db, OnlineDbContext online, OnlineRules rules, GuestFinder finder) : IAnswerGiver
{
    private const string Source = "Stornoregel des Standorts und Termin im Kalender";

    public IReadOnlyCollection<BusinessTopic> Topics { get; } = [BusinessTopic.CancellationFee];

    public async Task<AssistantBlock> AnswerAsync(BusinessQuestion question, AnswerContext context, CancellationToken ct)
    {
        if (Blocks.RequireCalendar(question.Topic, context) is { } denied)
        {
            return denied;
        }

        var visible = GuestFinder.Visible(db.Appointments.AsNoTracking().Include(a => a.Treatment), context);
        Appointment? appointment;
        if (context.ReferredAppointmentId is { } referred && string.IsNullOrWhiteSpace(question.Subject))
        {
            appointment = await visible.FirstOrDefaultAsync(a => a.Id == referred, ct);
        }
        else
        {
            var found = await finder.FindAsync(question, context, Source, ct);
            if (found.Block is { } notFound)
            {
                return context.ReferredAppointmentId is null
                    ? Blocks.NotFound(question.Topic, "Für welchen Termin? Bitte den Termin öffnen oder den Gast nennen.", Source)
                    : notFound;
            }

            if (found.Guests.Count > 1)
            {
                return await finder.ChoiceAsync(question, found, context, Source, ct);
            }

            var guestId = found.Guests[0].Id;
            var now = context.Now;
            // Ist der Termin des genannten Gastes offen, gilt dieser, nicht sein frühester.
            appointment = (context.ReferredAppointmentId is { } open ? await visible.FirstOrDefaultAsync(a => a.Id == open && a.CustomerId == guestId, ct) : null)
                ?? await visible.Where(a => a.CustomerId == guestId && a.Status == AppointmentStatus.Booked && a.Start >= now)
                    .OrderBy(a => a.Start).ThenBy(a => a.Id).FirstOrDefaultAsync(ct);
            if (appointment is null)
            {
                return Blocks.NotFound(question.Topic, $"{found.Guests[0].Name} hat keinen geplanten Termin.", Source) with { GuestId = guestId };
            }
        }

        if (appointment is null)
        {
            return Blocks.NotFound(question.Topic, "Diesen Termin gibt es im Kalender nicht.", Source);
        }

        if (appointment.Status != AppointmentStatus.Booked)
        {
            return Blocks.Answer(question.Topic, "Der Termin ist nicht mehr offen; eine Absage ist nicht nötig.", Source) with { GuestId = appointment.CustomerId };
        }

        return await FeeAsync(question, appointment, context, ct) with { GuestId = appointment.CustomerId };
    }

    private async Task<AssistantBlock> FeeAsync(BusinessQuestion question, Appointment appointment, AnswerContext context, CancellationToken ct)
    {
        var rule = await rules.ForAsync(appointment.LocationId, ct);
        var deadline = rule.CancellationDeadlineHours;
        var hours = (int)Math.Floor((appointment.Start - context.Now).TotalHours);
        if (hours >= deadline)
        {
            return Blocks.Answer(question.Topic, $"Bis {deadline} Std. vorher kostenlos; jetzt noch {hours} Std. – keine Gebühr.", Source);
        }

        var percent = await PercentAsync(rule, appointment, context, ct);
        var before = hours <= 0 ? "Termin hat schon begonnen" : $"{hours} Std. vor Beginn";
        if (percent == 0)
        {
            return Blocks.Answer(question.Topic, $"Absage jetzt: {before}, Frist {deadline} Std. – für diesen Termin keine Gebühr.", Source);
        }

        var fee = context.Has(AccessRight.Book)
            ? $"Gebühr {percent} % = {context.Money(CardGuarantees.Fee(appointment.Price, percent))}"
            : $"Gebühr {percent} % des Preises";
        var card = await online.CardGuarantees.AsNoTracking().AnyAsync(g => g.AppointmentId == appointment.Id && g.Status == CardGuaranteeStatus.Saved, ct)
            ? "Karte hinterlegt."
            : "Keine Karte hinterlegt.";
        return Blocks.Answer(question.Topic, $"Absage jetzt: {before}, Frist {deadline} Std. – {fee}. {card}", Source);
    }

    /// <summary>Prozent der Behandlung vor dem des Standorts; 0 für Stammgäste, wenn der Standort die Gebühr nur von neuen Gästen verlangt.</summary>
    private async Task<int> PercentAsync(LocationOnlineRules rule, Appointment appointment, AnswerContext context, CancellationToken ct)
    {
        var percent = CardGuarantees.EffectivePercent(true, rule, appointment.Treatment);
        if (percent > 0 && rule.NoShowFeeNewCustomersOnly
            && await db.Appointments.AnyAsync(a => a.CustomerId == appointment.CustomerId && a.Status == AppointmentStatus.Completed, ct)
            && !await NoShowHistory.IsRepeatAsync(db, appointment.CustomerId, context.Now, ct))
        {
            return 0;
        }

        return percent;
    }
}
