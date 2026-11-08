using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AispaStudio.Api.Data;
using AispaStudio.Api.MailIntake.Assistant;
using AispaStudio.Api.Tenancy;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;
using Wolverine;

namespace AispaStudio.Api.Reporting;

public enum BriefingSummaryStatus
{
    Pending,
    Done,
    Failed,
}

/// <summary>
/// KI-Zusammenfassung des Tagesstarts je Standort und Tag. Gilt nur für genau den Stand der Fakten mit diesem Fingerabdruck;
/// ändert sich etwas (neuer Termin, Krankmeldung), wird neu geschrieben.
/// </summary>
public sealed class BriefingSummary
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid LocationId { get; init; }
    public DateOnly Date { get; init; }
    public required string Fingerprint { get; set; }
    public BriefingSummaryStatus Status { get; set; }
    public string? Text { get; set; }
    public DateTimeOffset RequestedAt { get; set; }
}

/// <summary>Schreibt aus den fertig gerechneten Fakten des Tagesstarts zwei, drei Sätze. Rechnet nichts.</summary>
public interface IBriefingSummarizer
{
    bool Enabled { get; }

    Task<string?> SummarizeAsync(BriefingDto facts, CancellationToken ct);
}

/// <summary>Ohne KI: keine Zusammenfassung, der Tagesstart zeigt nur die regelbasierte Zeile.</summary>
internal sealed class NoBriefingSummarizer : IBriefingSummarizer
{
    public bool Enabled => false;

    public Task<string?> SummarizeAsync(BriefingDto facts, CancellationToken ct) => Task.FromResult<string?>(null);
}

internal sealed class ClaudeBriefingSummarizer(ClaudeJson claude) : IBriefingSummarizer
{
    private const string System = """
        Du schreibst für die Rezeption eines Day-Spas den Tagesstart: zwei bis drei kurze Sätze auf Deutsch, was heute wichtig ist.
        Zuerst, was Aufmerksamkeit braucht, dann Besonderheiten bei Gästen, Personal, Räumen und Kassa.
        Verwende nur die Fakten aus der Anfrage. Rechne nichts, schätze nichts und nenne keine Zahl, die nicht wörtlich in den Fakten steht.
        Nenne Gäste nur mit dem Namen, wie er in den Fakten steht. Keine Begrüßung, keine Aufzählungszeichen. Antworte nur im JSON-Format.
        """;

    private static readonly Dictionary<string, JsonElement> Schema = ClaudeJson.Schema(new
    {
        type = "object",
        additionalProperties = false,
        required = new[] { "summary" },
        properties = new { summary = new { type = "string" } },
    });

    public bool Enabled => true;

    public async Task<string?> SummarizeAsync(BriefingDto facts, CancellationToken ct)
    {
        var result = await claude.AskAsync<SummaryJson>(System, BriefingSummaries.FactsText(facts), Schema, ct);
        return result.Summary;
    }

    private sealed record SummaryJson(string Summary);
}

/// <summary>Queue: KI-Zusammenfassung für diesen Tagesstart schreiben.</summary>
public sealed record SummarizeBriefing(Guid BriefingSummaryId);

public static class SummarizeBriefingHandler
{
    public static async Task Handle(SummarizeBriefing message, IServiceScopeFactory scopes, CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AispaDbContext>();
        if (await TenantModel.TenantOfAsync(db.BriefingSummaries.Where(s => s.Id == message.BriefingSummaryId), ct) is not { } tenantId)
        {
            return;
        }

        scope.ServiceProvider.GetRequiredService<TenantContext>().Use(tenantId);
        await scope.ServiceProvider.GetRequiredService<BriefingSummaries>().WriteAsync(message.BriefingSummaryId, ct);
    }
}

/// <summary>
/// Hängt die KI-Zusammenfassung an den Tagesstart. Die Anfrage wartet nie auf die KI: sie stellt einen Auftrag in die Queue,
/// die Oberfläche holt den Text beim nächsten Laden. Zahlen im Text müssen wörtlich in den Fakten stehen, sonst wird er verworfen.
/// </summary>
internal sealed partial class BriefingSummaries(
    AispaDbContext db,
    DailyReports reports,
    IBriefingSummarizer summarizer,
    IMessageBus bus,
    TimeProvider time,
    ILogger<BriefingSummaries> log)
{
    private static readonly TimeSpan RequeueAfter = TimeSpan.FromMinutes(2);

    public const string Notice = "Von der KI aus den Angaben unten zusammengefasst. Maßgeblich sind die Angaben selbst.";

    public async Task<BriefingDto> AttachAsync(BriefingDto facts, CancellationToken ct)
    {
        if (!summarizer.Enabled)
        {
            return facts;
        }

        var fingerprint = Fingerprint(facts);
        var now = time.GetUtcNow();
        var row = await db.BriefingSummaries.FirstOrDefaultAsync(s => s.LocationId == facts.LocationId && s.Date == facts.Date, ct);
        if (row?.Fingerprint == fingerprint && (row.Status != BriefingSummaryStatus.Pending || now - row.RequestedAt < RequeueAfter))
        {
            return With(facts, row);
        }

        if (row is null)
        {
            row = new BriefingSummary { LocationId = facts.LocationId, Date = facts.Date, Fingerprint = fingerprint };
            db.BriefingSummaries.Add(row);
        }

        row.Fingerprint = fingerprint;
        row.Status = BriefingSummaryStatus.Pending;
        row.Text = null;
        row.RequestedAt = now;
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Zwei Anfragen gleichzeitig: die andere hat den Auftrag schon gestellt.
            return facts with { AiSummaryPending = true };
        }

        await bus.PublishAsync(new SummarizeBriefing(row.Id));
        return With(facts, row);
    }

    public async Task WriteAsync(Guid id, CancellationToken ct)
    {
        var row = await db.BriefingSummaries.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (row is not { Status: BriefingSummaryStatus.Pending }
            || await reports.BriefingAsync(row.LocationId, row.Date, ct) is not { } facts
            || Fingerprint(facts) != row.Fingerprint)
        {
            // Inzwischen geändert: der neuere Auftrag schreibt.
            return;
        }

        try
        {
            var text = (await summarizer.SummarizeAsync(facts, ct))?.Trim();
            if (string.IsNullOrEmpty(text) || !OnlyKnownNumbers(text, facts))
            {
                LogRejected(log, row.LocationId, row.Date);
                row.Status = BriefingSummaryStatus.Failed;
            }
            else
            {
                row.Text = text.Length > 600 ? text[..600] : text;
                row.Status = BriefingSummaryStatus.Done;
            }
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            LogFailed(log, e, row.LocationId, row.Date);
            row.Status = BriefingSummaryStatus.Failed;
        }

        await db.SaveChangesAsync(ct);
    }

    /// <summary>Was die KI zu sehen bekommt: nur die fertig gerechneten Angaben des Tagesstarts, keine Gesundheitsdaten.</summary>
    internal static string FactsText(BriefingDto facts)
    {
        var text = new StringBuilder();
        text.AppendLine(CultureInfo.InvariantCulture, $"Datum: {facts.Date:dd.MM.yyyy}");
        text.AppendLine(CultureInfo.InvariantCulture, $"Überblick: {facts.Summary}");
        Section(text, "Personal", facts.Staff.Select(s => $"{s.Name}: {s.State}{(s.Times is { } t ? $" {t}" : "")}"));
        Section(text, "Handlungsbedarf", facts.ActionNeeded.Select(i => i.Text));
        Section(text, "Gäste", facts.Guests.Select(i => i.Text));
        Section(text, "Räume und Geräte", facts.Rooms.Select(i => i.Text));
        Section(text, "Kassa", facts.Register.Select(i => i.Text));
        return text.ToString();
    }

    /// <summary>Jede Zahl im KI-Text muss so in den Fakten vorkommen; die KI rechnet keine Pflicht- oder Kennzahlen.</summary>
    internal static bool OnlyKnownNumbers(string text, BriefingDto facts)
    {
        // Auch Teile wie „18“ aus „18.10.2026“ oder „9“ aus „09:00“ gelten als bekannt.
        var known = Numbers().Matches(FactsText(facts))
            .SelectMany(m => m.Value.Split('.', ',').Append(m.Value))
            .Select(Normalize)
            .ToHashSet(StringComparer.Ordinal);
        return Numbers().Matches(text).All(m => known.Contains(Normalize(m.Value)));
    }

    private static string Normalize(string number) => number.TrimStart('0') is { Length: > 0 } trimmed ? trimmed.Replace(',', '.') : "0";

    internal static string Fingerprint(BriefingDto facts) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(FactsText(facts))));

    private static BriefingDto With(BriefingDto facts, BriefingSummary row) => row.Status switch
    {
        BriefingSummaryStatus.Done => facts with { AiSummary = row.Text, AiNotice = Notice },
        BriefingSummaryStatus.Pending => facts with { AiSummaryPending = true },
        _ => facts,
    };

    private static void Section(StringBuilder text, string title, IEnumerable<string> lines)
    {
        var list = lines.ToList();
        text.AppendLine(CultureInfo.InvariantCulture, $"{title}:{(list.Count == 0 ? " nichts" : "")}");
        foreach (var line in list)
        {
            text.AppendLine(CultureInfo.InvariantCulture, $"- {line}");
        }
    }

    [GeneratedRegex(@"\d+(?:[.,]\d+)*")]
    private static partial Regex Numbers();

    [LoggerMessage(Level = LogLevel.Warning, Message = "KI-Zusammenfassung für Standort {LocationId} am {Date} verworfen (leer oder mit fremden Zahlen).")]
    private static partial void LogRejected(ILogger logger, Guid locationId, DateOnly date);

    [LoggerMessage(Level = LogLevel.Warning, Message = "KI-Zusammenfassung für Standort {LocationId} am {Date} fehlgeschlagen.")]
    private static partial void LogFailed(ILogger logger, Exception exception, Guid locationId, DateOnly date);
}
