using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AispaStudio.Api.Data;
using AispaStudio.Api.Online;
using AispaStudio.Api.Scheduling;
using AispaStudio.Api.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.MailIntake.Assistant;

/// <summary>Frage eines Gasts und die Angaben des Spas, aus denen allein geantwortet werden darf.</summary>
/// <param name="Facts">Standorte, Öffnungszeiten, Behandlungen mit Dauer und Preis, Stornofrist, Link zum Buchen (<see cref="SpaFacts"/>).</param>
/// <param name="ShortMessage">SMS oder WhatsApp: kurz, ohne Briefform.</param>
public sealed record QuestionRequest(
    string Language,
    string GuestName,
    string Subject,
    string Body,
    string SpaName,
    string Facts,
    bool ShortMessage);

/// <param name="Answerable">Nur wenn die Angaben die Frage beantworten; sonst bleibt es bei „Das Team meldet sich“.</param>
public sealed record QuestionAnswer(bool Answerable, string? Body);

/// <summary>
/// Antwortvorschlag der KI auf Fragen von Gästen („Haben Sie Sonntag offen?“, „Was kostet die Hot-Stone-Massage?“). Nur ein
/// Vorschlag: die Rezeption prüft und sendet, automatisch geht er nie hinaus.
/// </summary>
public interface IQuestionAnswerer
{
    bool Enabled { get; }

    Task<QuestionAnswer> AnswerAsync(QuestionRequest request, CancellationToken ct);
}

/// <summary>Ohne KI: kein Vorschlag, die Rezeption antwortet selbst.</summary>
internal sealed class NoQuestionAnswerer : IQuestionAnswerer
{
    public bool Enabled => false;

    public Task<QuestionAnswer> AnswerAsync(QuestionRequest request, CancellationToken ct) => Task.FromResult(new QuestionAnswer(false, null));
}

internal sealed class ClaudeQuestionAnswerer(ClaudeJson claude) : IQuestionAnswerer
{
    private const string System = """
        Du schreibst für die Rezeption eines Day-Spas einen Antwortvorschlag auf die Nachricht eines Gasts. Die Rezeption prüft ihn,
        bevor er hinausgeht. Antworte in der Sprache mit dem angegebenen Code (de, en, fr, it), höflich (Sie, vous, Lei).
        Verwende ausschließlich die Angaben des Spas aus der Anfrage. Nenne Preise, Zeiten und Dauer genau so, wie sie dort stehen;
        rechne nichts, runde nichts, versprich keine Rabatte und keine Wirkung auf die Gesundheit. Medizinische Fragen
        (Schwangerschaft, Krankheiten, Medikamente) beantwortest du nicht inhaltlich, sondern sagst, dass das Team sich meldet.
        Beantworten die Angaben die Frage nicht vollständig, setze answerable auf false und lass body leer.
        Sprich den Gast so an, wie die Anrede in der Anfrage es angibt; Titel, Frau und Herr nicht ändern oder weglassen.
        Als Mail: Anrede mit Namen, Antwort, Grußformel mit dem Namen des Spas. Als Kurznachricht: zwei, drei Sätze ohne Anrede
        und Gruß. Kein Hinweis auf die KI, den fügt das System an. Antworte nur im JSON-Format.
        """;

    private static readonly Dictionary<string, JsonElement> Schema = ClaudeJson.Schema(new
    {
        type = "object",
        additionalProperties = false,
        required = new[] { "answerable", "body" },
        properties = new { answerable = new { type = "boolean" }, body = new { type = "string" } },
    });

    public bool Enabled => true;

    public async Task<QuestionAnswer> AnswerAsync(QuestionRequest request, CancellationToken ct)
    {
        var prompt = $"""
            Sprache: {request.Language}
            Form: {(request.ShortMessage ? "Kurznachricht (SMS/WhatsApp)" : "Mail")}
            Anrede des Gasts: {request.GuestName}
            Spa: {request.SpaName}

            Angaben des Spas:
            {request.Facts}

            Nachricht des Gasts:
            Betreff: {request.Subject}
            {request.Body}
            """;
        var result = await claude.AskAsync<AnswerJson>(System, prompt, Schema, ct);
        return new QuestionAnswer(result.Answerable, result.Body);
    }

    private sealed record AnswerJson(bool Answerable, string? Body);
}

/// <summary>
/// Was das Spa über sich selbst sagen kann, als Text für die KI: Standorte mit Adresse, Öffnungszeiten, Stornofrist und den
/// angebotenen Behandlungen mit Dauer und Preis am Standort, dazu der Link zum Buchen.
/// </summary>
internal sealed partial class SpaFacts(AispaDbContext db, OnlineRules rules, OnlinePortal portal, TenantContext tenants)
{
    /// <summary>Damit die Anfrage klein bleibt; große Spas haben selten mehr Behandlungen je Standort.</summary>
    private const int MaxTreatments = 60;

    private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-AT");

    public async Task<(string Name, string Text)> DescribeAsync(Guid? locationId, CancellationToken ct)
    {
        var tenantId = tenants.TenantId ?? throw new InvalidOperationException("Kein Spa gesetzt.");
        var name = await db.Tenants.Where(t => t.Id == tenantId).Select(t => t.Name).FirstAsync(ct);
        var locations = await db.Locations.AsNoTracking().Include(l => l.OpeningHours)
            .Where(l => locationId == null || l.Id == locationId).OrderBy(l => l.Name).ToListAsync(ct);

        var text = new StringBuilder();
        text.AppendLine(CultureInfo.InvariantCulture, $"Spa: {name}");
        text.AppendLine(CultureInfo.InvariantCulture, $"Online buchen: {portal.Url("", tenantId)}");
        foreach (var location in locations)
        {
            text.AppendLine();
            var address = string.Join(", ", new[] { location.Street, $"{location.PostalCode} {location.City}".Trim() }.Where(p => !string.IsNullOrWhiteSpace(p)));
            text.AppendLine(CultureInfo.InvariantCulture, $"Standort {location.Name}{(address.Length > 0 ? $", {address}" : "")}");
            if (!string.IsNullOrWhiteSpace(location.Phone))
            {
                text.AppendLine(CultureInfo.InvariantCulture, $"Telefon: {location.Phone}");
            }

            if (!string.IsNullOrWhiteSpace(location.Email))
            {
                text.AppendLine(CultureInfo.InvariantCulture, $"Mail: {location.Email}");
            }

            text.AppendLine(CultureInfo.InvariantCulture, $"Öffnungszeiten: {Hours(location.OpeningHours)}");
            var rule = await rules.ForAsync(location.Id, ct);
            text.AppendLine(CultureInfo.InvariantCulture, $"Kostenlos stornieren bis {rule.CancellationDeadlineHours} Stunden vor Beginn.");

            var treatments = await OnlineBookingEndpoints.OfferedTreatmentsAsync(db, location.Id, ct);
            text.AppendLine("Behandlungen:");
            foreach (var t in treatments.OrderBy(t => t.Category).ThenBy(t => t.Name).Take(MaxTreatments))
            {
                var price = t.PriceAt(location.Id).ToString("N2", German);
                text.AppendLine(CultureInfo.InvariantCulture,
                    $"- {(string.IsNullOrWhiteSpace(t.Category) ? "" : $"{t.Category}: ")}{t.Name}, {t.DurationMinutes} Minuten, {price} {location.Currency}");
            }
        }

        return (name, text.ToString());
    }

    private static string Hours(List<LocationOpeningHours> hours)
    {
        var days = new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday };
        return string.Join("; ", days.Select(day =>
        {
            var open = hours.Where(h => h.Day == day).OrderBy(h => h.Opens).ToList();
            var name = German.DateTimeFormat.GetDayName(day);
            return open.Count == 0 ? $"{name} geschlossen" : $"{name} {string.Join(" und ", open.Select(h => $"{h.Opens:HH\\:mm}–{h.Closes:HH\\:mm}"))}";
        }));
    }

    /// <summary>
    /// Jede Zahl im Vorschlag muss in den Angaben des Spas oder in der Nachricht des Gasts stehen; sonst hat die KI sie erfunden
    /// und der Vorschlag wird verworfen.
    /// </summary>
    public static bool OnlyKnownNumbers(string answer, string known) =>
        Numbers().Matches(answer).All(m => known.Contains(m.Value, StringComparison.Ordinal));

    [GeneratedRegex(@"\d+")]
    private static partial Regex Numbers();
}
