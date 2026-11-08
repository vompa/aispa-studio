using AispaStudio.Api.Data;
using AispaStudio.Api.Online;
using AispaStudio.Api.Pos;
using AispaStudio.Api.Scheduling;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Assistant.Answers;

/// <summary>
/// „Was kostet Hot Stone und wie lange dauert sie?“ und „Wer bietet Thai-Massage an?“: aus dem Katalog des Standorts (Behandlungen mit Raum und qualifiziertem Team)
/// und den Qualifikationen der Personen. Der Name der Behandlung steht irgendwo im Satz; erkannt wird er am Katalog, nicht an einer Liste im Code.
/// </summary>
internal sealed class CatalogAnswers(AispaDbContext db) : IAnswerGiver
{
    public IReadOnlyCollection<BusinessTopic> Topics { get; } = [BusinessTopic.TreatmentInfo, BusinessTopic.WhoOffers];

    public async Task<AssistantBlock> AnswerAsync(BusinessQuestion question, AnswerContext context, CancellationToken ct)
    {
        if (Blocks.RequireCalendar(question.Topic, context) is { } denied)
        {
            return denied;
        }

        var offered = await OnlineBookingEndpoints.OfferedTreatmentsAsync(db, context.Location.Id, ct);
        var source = $"Behandlungskatalog des {context.PlaceOf}";
        var found = Mentioned(offered, question.Text, t => t.Name);
        if (found.Count == 0 && question.Topic == BusinessTopic.WhoOffers && await QualificationAsync(question, context, source, ct) is { } byQualification)
        {
            return byQualification;
        }

        // Slice 136: „Was kostet die Gesichtscreme?“ fragt nach einem Verkaufsartikel der Kassa; heißt er wie eine Behandlung, stehen beide in der Antwort.
        var articles = question.Topic == BusinessTopic.TreatmentInfo
            ? Mentioned(await db.Articles.AsNoTracking().Where(a => a.IsActive).ToListAsync(ct), question.Text, a => a.Name)
            : [];
        if (found.Count > 0)
        {
            // Nennt die Frage eine Behandlung, zählt ein Artikel nur, wenn sein ganzer Name in der Frage steht (nicht „Massageöl“ in „Wie lange dauert die Massage?“).
            articles = [.. articles.Where(a => NameMatch.Take(NameMatch.KeyWords(question.Text), a.Name))];
        }

        if (articles.Count > 0)
        {
            return ArticlePrices(found, articles, context);
        }

        if (found.Count == 0)
        {
            var elsewhere = Mentioned(await db.Treatments.AsNoTracking().Where(t => t.IsActive).ToListAsync(ct), question.Text, t => t.Name);
            return elsewhere.Count > 0
                ? Blocks.NotFound(question.Topic, $"{elsewhere[0].Name} gibt es im Katalog, sie wird im {context.Place} aber nicht angeboten (es fehlt ein Raum oder eine qualifizierte Person).", source)
                : Blocks.NotFound(question.Topic, "Welche Behandlung ist gemeint? Im Katalog des Standorts stehen:", source,
                    Blocks.Table(["Behandlung"], offered.Select(t => (IReadOnlyList<string>)[t.Name])));
        }

        return question.Topic == BusinessTopic.WhoOffers ? await WhoAsync(found, context, source, ct) : Info(found, context, source);
    }

    /// <summary>Verkaufspreise aus der Kassa; nennt die Frage auch eine Behandlung, steht sie mit ihrem Preis in derselben Tabelle.</summary>
    private static AssistantBlock ArticlePrices(List<Treatment> treatments, List<Article> articles, AnswerContext context)
    {
        const string Source = "Verkaufsartikel der Kassa";
        if (treatments.Count == 0 && articles.Count == 1)
        {
            return Blocks.Answer(BusinessTopic.TreatmentInfo, $"{articles[0].Name} kostet {context.Money(articles[0].Price)}.", Source);
        }

        var rows = treatments.Select(t => (IReadOnlyList<string>)[t.Name, "Behandlung", context.Money(t.PriceAt(context.Location.Id))])
            .Concat(articles.Select(a => (IReadOnlyList<string>)[a.Name, "Artikel", context.Money(a.Price)]));
        return Blocks.Answer(BusinessTopic.TreatmentInfo, $"Preise im {context.Place}: Behandlungen aus dem Katalog, Artikel aus der Kassa.", $"Behandlungskatalog und {Source}",
            Blocks.Table(["Name", "Art", "Preis"], rows));
    }

    private static AssistantBlock Info(List<Treatment> found, AnswerContext context, string source) =>
        WithGaps(Describe(found, context, source), found, context);

    /// <summary>Preis oder Dauer einer genannten Behandlung fehlen (Preis 0, Dauer 0): der Block sagt es mit Verweis auf die Behandlung (Feldkennungen <c>treatment.price</c>, <c>treatment.duration</c>).</summary>
    private static AssistantBlock WithGaps(AssistantBlock block, List<Treatment> found, AnswerContext context)
    {
        var gaps = found.SelectMany(t => new[]
        {
            t.PriceAt(context.Location.Id) <= 0 ? new FieldGap(FieldIds.Price, t.Name, t.Id) : (FieldGap?)null,
            t.DurationMinutes <= 0 ? new FieldGap(FieldIds.Duration, t.Name, t.Id) : (FieldGap?)null,
        }).OfType<FieldGap>().ToList();
        return gaps.Count == 0 ? block : block with { Incomplete = FieldMasks.Incomplete(context, gaps) };
    }

    private static AssistantBlock Describe(List<Treatment> found, AnswerContext context, string source)
    {
        string Minutes(Treatment t) => $"{t.DurationMinutes} Minuten";
        var rules = found.Any(t => t.PriceRules.Any(r => r.LocationId == null || r.LocationId == context.Location.Id));
        var note = rules ? " Zu bestimmten Wochentagen oder Uhrzeiten gelten abweichende Preise." : "";
        if (found.Count == 1)
        {
            var t = found[0];
            var extra = t.PreparationMinutes + t.FollowUpMinutes > 0 ? $" (dazu {t.PreparationMinutes + t.FollowUpMinutes} Minuten Vor- und Nachbereitung, in denen der Raum belegt ist)" : "";
            return Blocks.Answer(BusinessTopic.TreatmentInfo, $"{t.Name} kostet {context.Money(t.PriceAt(context.Location.Id))} und dauert {Minutes(t)}{extra}.{note}", source);
        }

        return Blocks.Answer(BusinessTopic.TreatmentInfo, $"Preis und Dauer der genannten Behandlungen im {context.Place}.{note}", source,
            Blocks.Table(["Behandlung", "Preis", "Dauer"], found.Select(t => (IReadOnlyList<string>)[t.Name, context.Money(t.PriceAt(context.Location.Id)), Minutes(t)])));
    }

    private async Task<AssistantBlock> WhoAsync(List<Treatment> found, AnswerContext context, string source, CancellationToken ct)
    {
        var people = await TeamAsync(context, ct);
        var rows = new List<IReadOnlyList<string>>();
        var lines = new List<string>();
        foreach (var treatment in found)
        {
            var names = people.Where(p => treatment.RequiredQualifications.All(q => p.Qualifications.Any(x => x.Id == q.Id))).Select(p => p.InHouseName).ToList();
            lines.Add(names.Count == 0 ? $"{treatment.Name}: niemand im Team hat die nötigen Qualifikationen." : $"{treatment.Name}: {string.Join(", ", names)}.");
            rows.AddRange(names.Select(n => (IReadOnlyList<string>)[treatment.Name, n]));
        }

        return Blocks.Answer(BusinessTopic.WhoOffers, string.Join(' ', lines), $"{source} und Qualifikationen des Teams", rows.Count == 0 ? null : Blocks.Table(["Behandlung", "Person"], rows));
    }

    /// <summary>Kein Behandlungsname im Satz: vielleicht der Name einer Qualifikation („Thai-Massage“).</summary>
    private async Task<AssistantBlock?> QualificationAsync(BusinessQuestion question, AnswerContext context, string source, CancellationToken ct)
    {
        var qualifications = Mentioned(await db.Qualifications.AsNoTracking().ToListAsync(ct), question.Text, q => q.Name);
        if (qualifications.Count == 0)
        {
            return null;
        }

        var people = await TeamAsync(context, ct);
        var qualification = qualifications[0];
        var names = people.Where(p => p.Qualifications.Any(q => q.Id == qualification.Id)).Select(p => p.InHouseName).ToList();
        return Blocks.Answer(BusinessTopic.WhoOffers,
            names.Count == 0 ? $"Niemand im Team des {context.PlaceOf} hat die Qualifikation „{qualification.Name}“." : $"Die Qualifikation „{qualification.Name}“ haben: {string.Join(", ", names)}.",
            $"Qualifikationen des Teams am {context.Place}", names.Count == 0 ? null : Blocks.Table(["Person"], names.Select(n => (IReadOnlyList<string>)[n])));
    }

    private Task<List<Employee>> TeamAsync(AnswerContext context, CancellationToken ct) =>
        db.Employees.AsNoTracking().Include(e => e.Qualifications)
            .Where(e => e.IsActive && e.WorkingHours.Any(w => w.LocationId == context.Location.Id)).ToListAsync(ct);

    private static readonly HashSet<string> Filler =
    [
        "was", "wie", "wer", "wo", "wann", "kostet", "kosten", "dauert", "dauer", "lange", "preis", "preise", "und", "oder", "eine", "einen", "ein", "die", "der", "das", "dem", "den", "von", "für",
        "bei", "uns", "sie", "es", "ist", "sind", "macht", "machen", "bietet", "bieten", "an", "aus", "im", "in", "auf", "mit", "zu", "wieviel", "viel", "teuer", "kann", "können", "unsere", "unser",
        "gibt", "haben", "hat", "wir", "ihr",
    ];

    /// <summary>
    /// Die Namen aus dem Katalog, die im Satz stehen; die längsten zuerst, damit „Massage“ nicht noch einmal in „Hot Stone Massage“ gefunden wird.
    /// Steht keiner ganz im Satz, zählen Namen, die die genannten Wörter enthalten („Hot Stone“ in „Lava-Hot-Stone-Massage (60 Min.)“), höchstens sechs.
    /// </summary>
    internal static List<T> Mentioned<T>(IReadOnlyCollection<T> items, string text, Func<T, string> name)
    {
        var words = NameMatch.KeyWords(text);
        var hits = new List<T>();
        foreach (var item in items.OrderByDescending(i => NameMatch.Key(name(i)).Length).ThenBy(name, StringComparer.Ordinal))
        {
            if (NameMatch.Take(words, name(item)))
            {
                hits.Add(item);
            }
        }

        return hits.Count > 0 ? [.. hits.OrderBy(name, StringComparer.Ordinal)] : Partial(items, text, name);
    }

    private static List<T> Partial<T>(IReadOnlyCollection<T> items, string text, Func<T, string> name)
    {
        var words = NameMatch.KeyWords(text).Where(w => !Filler.Contains(w)).ToList();
        for (var length = Math.Min(3, words.Count); length >= 1; length--)
        {
            for (var start = 0; start + length <= words.Count; start++)
            {
                var phrase = string.Concat(words.Skip(start).Take(length));
                if (phrase.Length < 4)
                {
                    continue;
                }

                var found = items.Where(i => NameMatch.Key(name(i)).Contains(phrase, StringComparison.Ordinal)).OrderBy(name, StringComparer.Ordinal).ToList();
                if (found.Count is > 0 and <= 6)
                {
                    return found;
                }
            }
        }

        return [];
    }
}
