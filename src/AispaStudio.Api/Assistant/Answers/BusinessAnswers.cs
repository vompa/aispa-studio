using AispaStudio.Contracts;

namespace AispaStudio.Api.Assistant.Answers;

/// <summary>
/// Antwort auf eine Betriebsfrage (ADR 0072): sucht den Antwortgeber zum Thema und gibt seinen Block zurück. Die Absicht haben Regeln
/// (<see cref="BusinessIntents"/>) oder das Sprachmodell erkannt; hier wird nichts geraten und nichts gerechnet.
/// </summary>
internal sealed class BusinessAnswers(IEnumerable<IAnswerGiver> givers, FieldGaps gaps)
{
    private readonly Dictionary<BusinessTopic, IAnswerGiver> _byTopic = givers.SelectMany(g => g.Topics.Select(t => (Topic: t, Giver: g)))
        .ToDictionary(x => x.Topic, x => x.Giver);

    /// <summary>Die Themen, zu denen es einen Antwortgeber gibt; ein Test verlangt, dass es alle sind.</summary>
    public IReadOnlyCollection<BusinessTopic> Topics => _byTopic.Keys;

    public async Task<AssistantBlock> AnswerAsync(BusinessQuestion question, AnswerContext context, CancellationToken ct)
    {
        if (!_byTopic.TryGetValue(question.Topic, out var giver))
        {
            return Blocks.NotFound(question.Topic, "Dazu kann der Assistent noch nichts sagen.", "");
        }

        var block = await giver.AnswerAsync(question, context, ct);
        if (block.Kind != AssistantBlockKind.Answer)
        {
            return block;
        }

        // Die Antwort war möglich; fehlen dem Spa trotzdem Angaben, die zum Thema gehören, sagt der Block es mit Verweis in die Maske.
        var missing = FieldMasks.Incomplete(context, await gaps.FindAsync([.. giver.Fields(question)], context, ct), block.Link);
        return missing.Count == 0 ? block : block with { Incomplete = [.. block.Incomplete ?? [], .. missing] };
    }
}

internal static class BusinessAnswersSetup
{
    /// <summary>Alle Antwortgeber, je Thema einer; ein neuer Antwortgeber wird hier eingetragen und ist damit für die Absichtserkennung erreichbar.</summary>
    public static IServiceCollection AddBusinessAnswers(this IServiceCollection services)
    {
        services.AddScoped<BusinessAnswers>();
        services.AddScoped<FieldGaps>();
        services.AddScoped<GuestFinder>();
        services.AddScoped<IAnswerGiver, OpeningHoursAnswers>();
        services.AddScoped<IAnswerGiver, ContactAnswers>();
        services.AddScoped<IAnswerGiver, HolidayAnswers>();
        services.AddScoped<IAnswerGiver, SchoolHolidayAnswers>();
        services.AddScoped<IAnswerGiver, CancellationAnswers>();
        services.AddScoped<IAnswerGiver, TeamAnswers>();
        services.AddScoped<IAnswerGiver, DayAnswers>();
        services.AddScoped<IAnswerGiver, RoomAnswers>();
        services.AddScoped<IAnswerGiver, CatalogAnswers>();
        services.AddScoped<IAnswerGiver, GuestAnswers>();
        services.AddScoped<IAnswerGiver, VoucherAnswers>();
        services.AddScoped<IAnswerGiver, PromotionAnswers>();
        services.AddScoped<IAnswerGiver, ReportAnswers>();
        services.AddScoped<IAnswerGiver, BirthdayAnswers>();
        services.AddScoped<IAnswerGiver, BusinessContactAnswers>();
        services.AddScoped<IAnswerGiver, GuestHistoryAnswers>();
        services.AddScoped<IAnswerGiver, GuestBalanceAnswers>();
        services.AddScoped<IAnswerGiver, GuestAnamnesisAnswers>();
        services.AddScoped<IAnswerGiver, ArrivalAnswers>();
        services.AddScoped<IAnswerGiver, CancellationFeeAnswers>();
        services.AddScoped<IAnswerGiver, WaitlistAnswers>();
        services.AddScoped<IAnswerGiver, StaffTopicAnswers>();
        services.AddScoped<IAnswerGiver, RoomTimesAnswers>();
        services.AddScoped<IAnswerGiver, StockAnswers>();
        services.AddScoped<IAnswerGiver, OrderAnswers>();
        return services;
    }
}
