using AispaStudio.Api.Online;
using AispaStudio.Contracts;
using Microsoft.Extensions.Options;

namespace AispaStudio.Api.Assistant.Answers;

/// <summary>
/// „Hat Herr Steiner die Anamnese ausgefüllt?“ (Ä317): ob ein Fragebogen da ist, von wann und bis wann er gilt (<see cref="OnlineBookingOptions.AnamnesisValidMonths"/>),
/// wie im Gesundheitsbogen der Kundenkarte. Hinweise daraus (Allergie, Schwangerschaft …) nur mit dem Recht auf Gesundheitsdaten, nur als Stichwort und nie mit den
/// Freitexten des Bogens. Gelesen wird über <see cref="AnamnesisStore"/>: entschlüsselt (im Speicher) nur mit diesem Recht, sonst nur der Zeitpunkt aus den Metadaten;
/// nichts davon wird protokolliert.
/// </summary>
internal sealed class GuestAnamnesisAnswers(GuestFinder finder, AnamnesisStore anamnesis, IOptions<OnlineBookingOptions> options) : IAnswerGiver
{
    private const string Source = "Gesundheitsbogen der Kundenkarte";

    /// <summary>Stichwort je Frage des Bogens, die mit Ja beantwortet sein kann (<see cref="AnamnesisForm"/>).</summary>
    private static readonly (string Key, string Title)[] Hints =
    [
        ("allergies", "Allergie"), ("skin", "Hautprobleme"), ("circulation", "Herz-Kreislauf"), ("pregnancy", "Schwangerschaft oder Stillzeit"),
        ("medication", "Medikamente"), ("surgery", "Operation oder Verletzung"),
    ];

    public IReadOnlyCollection<BusinessTopic> Topics { get; } = [BusinessTopic.GuestAnamnesis];

    public async Task<AssistantBlock> AnswerAsync(BusinessQuestion question, AnswerContext context, CancellationToken ct)
    {
        if (Blocks.RequireCalendar(question.Topic, context) is { } denied)
        {
            return denied;
        }

        var found = await finder.FindAsync(question, context, Source, ct);
        if (found.Block is { } notFound)
        {
            return notFound;
        }

        if (found.Guests.Count > 1)
        {
            return await finder.ChoiceAsync(question, found, context, Source, ct);
        }

        var guest = found.Guests[0];
        var health = context.Has(AccessRight.ViewHealthData);
        // Ohne das Recht auf Gesundheitsdaten wird der Bogen nicht entschlüsselt; für den Stand reicht der Zeitpunkt.
        var record = health ? await anamnesis.LatestAsync(guest.Id, ct) : null;
        var latest = health ? record?.SubmittedAt : await anamnesis.LatestSubmittedAtAsync(guest.Id, ct);
        if (latest is not { } at)
        {
            return Blocks.Answer(question.Topic, $"{guest.Name}: Kein Fragebogen hinterlegt.", Source) with { GuestId = guest.Id };
        }

        var months = options.Value.AnamnesisValidMonths;
        var submitted = GuestFinder.Local(at, context);
        var validUntil = GuestFinder.Local(at.AddMonths(months), context);
        var state = validUntil >= context.Today
            ? $"Fragebogen vom {Date(submitted)}, gültig bis {Date(validUntil)}."
            : $"Fragebogen vom {Date(submitted)}, älter als {months} Monate: bitte erneuern lassen.";
        return Blocks.Answer(question.Topic, $"{guest.Name}: {state} {HintsOf(record)}".TrimEnd(), Source) with { GuestId = guest.Id };
    }

    private static string Date(DateOnly day) => day.ToString("dd.MM.yyyy", AnswerContext.German);

    /// <summary>Die mit Ja beantworteten Fragen als Stichwort, ohne Freitext; ohne das Recht auf Gesundheitsdaten nur der Satz, wer sie sieht.</summary>
    private static string HintsOf(AnamnesisRecordDto? record)
    {
        if (record is null)
        {
            return "Hinweise aus dem Fragebogen sieht nur, wer Gesundheitsdaten sehen darf.";
        }

        var yes = Hints.Where(h => record.Answers.Any(a => a.Key == h.Key && a.Yes == true)).Select(h => h.Title).ToList();
        return yes.Count switch
        {
            0 => "Keine Frage mit Ja beantwortet.",
            1 => $"Hinweis: {yes[0]} angegeben.",
            _ => $"Hinweise: {string.Join(", ", yes)} angegeben.",
        };
    }
}
