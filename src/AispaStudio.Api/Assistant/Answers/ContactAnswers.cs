using AispaStudio.Api.Data;
using AispaStudio.Api.Scheduling;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Assistant.Answers;

/// <summary>
/// „Unsere Kontaktdaten“, Telefon, Adresse, Mail und „Wie kommt der Gast zu uns, Parkplätze?“: aus den Angaben des Standorts. Zum Kopieren gibt der Block
/// die Kontaktdaten als Text mit. Was nicht eingetragen ist, sagt die Antwort und verweist auf die Einstellung (Standorte und Räume).
/// </summary>
internal sealed class ContactAnswers(AispaDbContext db) : IAnswerGiver
{
    public IReadOnlyCollection<BusinessTopic> Topics { get; } = [BusinessTopic.Contact, BusinessTopic.Directions];

    public IReadOnlyCollection<string> Fields(BusinessQuestion question) => question.Topic == BusinessTopic.Directions
        ? [FieldIds.Street]
        : question.Detail switch
        {
            "phone" => [FieldIds.Phone],
            "mail" => [FieldIds.Email],
            "address" => [FieldIds.Street],
            _ => [FieldIds.Phone, FieldIds.Email, FieldIds.Street, FieldIds.Directions, FieldIds.Website],
        };

    public async Task<AssistantBlock> AnswerAsync(BusinessQuestion question, AnswerContext context, CancellationToken ct)
    {
        if (Blocks.RequireCalendar(question.Topic, context) is { } denied)
        {
            return denied;
        }

        var location = await db.Locations.AsNoTracking().FirstAsync(l => l.Id == context.Location.Id, ct);
        return question.Topic == BusinessTopic.Directions ? Directions(location, context) : Contact(location, question, context);
    }

    private static AssistantBlock Directions(Location location, AnswerContext context) =>
        string.IsNullOrWhiteSpace(location.Directions)
            ? Blocks.NotStored(BusinessTopic.Directions, "Anreise und Parkplätze", $"Feld „Anreise“ des {context.PlaceOf}", context.Fill(FieldIds.Directions))
            : Blocks.Answer(BusinessTopic.Directions, $"Anreise zum {context.Place}: {location.Directions}", $"Feld „Anreise“ des {context.PlaceOf}", copy: location.Directions);

    private static AssistantBlock Contact(Location location, BusinessQuestion question, AnswerContext context)
    {
        var source = $"Kontaktdaten des {context.PlaceOf}";
        var street = string.Join(", ", new[] { location.Street, string.Join(' ', new[] { location.PostalCode, location.City }.Where(v => !string.IsNullOrWhiteSpace(v))) }
            .Where(v => !string.IsNullOrWhiteSpace(v)));
        var rows = new List<(string Label, string? Value, string Field)>
        {
            ("Telefon", location.Phone, FieldIds.Phone),
            ("E-Mail", location.Email, FieldIds.Email),
            ("Adresse", street.Length == 0 ? null : street, FieldIds.Street),
        };

        var wanted = question.Detail switch
        {
            "phone" => rows.Where(r => r.Label == "Telefon").ToList(),
            "mail" => rows.Where(r => r.Label == "E-Mail").ToList(),
            "address" => rows.Where(r => r.Label == "Adresse").ToList(),
            _ => rows,
        };
        var stored = wanted.Where(r => !string.IsNullOrWhiteSpace(r.Value)).ToList();
        if (stored.Count == 0)
        {
            return Blocks.NotStored(BusinessTopic.Contact, string.Join(", ", wanted.Select(r => r.Label)), source, context.Fill(wanted[0].Field));
        }

        var missingRows = wanted.Where(r => string.IsNullOrWhiteSpace(r.Value)).ToList();
        var missing = missingRows.Select(r => r.Label).ToList();
        var text = question.Detail is "phone" or "mail" or "address"
            ? $"{stored[0].Label} des {context.PlaceOf}: {stored[0].Value}"
            : $"Kontaktdaten des {context.PlaceOf}: {string.Join("; ", stored.Select(r => $"{r.Label} {r.Value}"))}.";
        if (missing.Count > 0)
        {
            text += $" Nicht hinterlegt: {string.Join(", ", missing)}.";
        }

        var copy = question.Detail is "phone" or "mail" or "address"
            ? stored[0].Value
            : string.Join('\n', new[] { location.Name }.Concat(stored.Select(r => $"{r.Label}: {r.Value}")));
        return Blocks.Answer(BusinessTopic.Contact, text, source, Blocks.Table(["Angabe", "Wert"], stored.Select(r => (IReadOnlyList<string>)[r.Label, r.Value!])),
            missingRows.Count > 0 ? context.Fill(missingRows[0].Field) : null, copy);
    }
}
