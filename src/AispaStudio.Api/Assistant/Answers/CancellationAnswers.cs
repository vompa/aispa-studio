using AispaStudio.Api.Data;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Assistant.Answers;

/// <summary>
/// „Was ist unsere Stornoregel?“: aus den Buchungseinstellungen des Standorts, dem Freitext „Stornoregel“ und den festen Werten (Frist der Online-Absage,
/// Ausfallgebühr, Anzahlung). Gibt es weder eigene Einstellungen noch einen Text, ist nichts hinterlegt; die Vorgaben für alle Standorte zählen hier nicht,
/// weil niemand sie als Regel des Spas eingetragen hat.
/// </summary>
internal sealed class CancellationAnswers(AispaDbContext db) : IAnswerGiver
{
    public IReadOnlyCollection<BusinessTopic> Topics { get; } = [BusinessTopic.CancellationRule];

    public IReadOnlyCollection<string> Fields(BusinessQuestion question) => [FieldIds.CancellationPolicy];

    public async Task<AssistantBlock> AnswerAsync(BusinessQuestion question, AnswerContext context, CancellationToken ct)
    {
        if (Blocks.RequireCalendar(question.Topic, context) is { } denied)
        {
            return denied;
        }

        var source = $"Buchungseinstellungen des {context.PlaceOf}";
        var link = context.Fill(FieldIds.CancellationPolicy);
        var settings = await db.LocationOnlineSettings.AsNoTracking().FirstOrDefaultAsync(s => s.LocationId == context.Location.Id, ct);
        if (settings is null)
        {
            return Blocks.NotStored(question.Topic, "Stornoregel", source, link);
        }

        var rows = new List<IReadOnlyList<string>>();
        if (settings.CancellationDeadlineHours > 0)
        {
            rows.Add(["Absage online", $"bis {settings.CancellationDeadlineHours} Stunden vor dem Termin"]);
        }

        if (settings.NoShowFeePercent > 0)
        {
            rows.Add(["Ausfallgebühr", $"{settings.NoShowFeePercent} % des Preises bei Nichterscheinen oder später Absage"]);
        }

        if (settings.DepositPercent > 0)
        {
            rows.Add(["Anzahlung", $"{settings.DepositPercent} % des Preises beim Online-Buchen"]);
        }

        var policy = string.IsNullOrWhiteSpace(settings.CancellationPolicy) ? null : settings.CancellationPolicy.Trim();
        if (policy is null && rows.Count == 0)
        {
            return Blocks.NotStored(question.Topic, "Stornoregel", source, link);
        }

        var text = policy is null
            ? $"Stornoregel des {context.PlaceOf} laut Buchungseinstellungen."
            : $"Stornoregel des {context.PlaceOf}: {policy}";
        return Blocks.Answer(question.Topic, text, source, rows.Count == 0 ? null : Blocks.Table(["Regel", "Wert"], rows), policy is null ? link : null);
    }
}
