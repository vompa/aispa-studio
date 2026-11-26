using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AispaStudio.Api.Data;
using AispaStudio.Api.MailIntake.Assistant;
using AispaStudio.Api.Tenancy;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;
using Wolverine;

namespace AispaStudio.Api.Reporting;

/// <summary>
/// KI-Kommentar zum Managementbericht je Standort, Zeitraum und Sicht (mit oder ohne Beträge). Gilt nur für genau den Stand der Fakten mit diesem
/// Fingerabdruck; ändern sich die Zahlen, wird neu geschrieben.
/// </summary>
public sealed class ManagementSummary
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid LocationId { get; init; }
    public DateOnly From { get; init; }
    public DateOnly Until { get; init; }

    /// <summary>Mit Geldbeträgen geschrieben (Recht „Berichte mit Beträgen“); ohne dieses Recht gibt es einen eigenen Text ohne Beträge.</summary>
    public bool Amounts { get; init; }

    public required string Fingerprint { get; set; }
    public BriefingSummaryStatus Status { get; set; }
    public string? Text { get; set; }
    public DateTimeOffset RequestedAt { get; set; }
}

/// <summary>Schreibt aus den fertig gerechneten Kennzahlen einen kurzen Kommentar mit Empfehlungen. Rechnet nichts.</summary>
public interface IManagementSummarizer
{
    bool Enabled { get; }

    Task<string?> SummarizeAsync(string facts, CancellationToken ct);
}

/// <summary>Ohne KI: kein Kommentar, der Bericht zeigt nur Zahlen und Hinweise.</summary>
internal sealed class NoManagementSummarizer : IManagementSummarizer
{
    public bool Enabled => false;

    public Task<string?> SummarizeAsync(string facts, CancellationToken ct) => Task.FromResult<string?>(null);
}

internal sealed class ClaudeManagementSummarizer(ClaudeJson claude) : IManagementSummarizer
{
    private const string System = """
        Du schreibst für die Leitung eines Day-Spas einen kurzen Kommentar zum Bericht eines Zeitraums: drei bis fünf Sätze auf Deutsch.
        Zuerst, was gut lief, dann, was schlechter lief als im Zeitraum davor, zum Schluss höchstens drei konkrete Empfehlungen.
        Verwende nur die Fakten aus der Anfrage. Rechne nichts, schätze nichts, bilde keine Differenzen oder Prozente und nenne keine Zahl,
        die nicht wörtlich in den Fakten steht. Keine Begrüßung, keine Aufzählungszeichen. Antworte nur im JSON-Format.
        """;

    private static readonly Dictionary<string, JsonElement> Schema = ClaudeJson.Schema(new
    {
        type = "object",
        additionalProperties = false,
        required = new[] { "summary" },
        properties = new { summary = new { type = "string" } },
    });

    public bool Enabled => true;

    public async Task<string?> SummarizeAsync(string facts, CancellationToken ct) => (await claude.AskAsync<SummaryJson>(System, facts, Schema, ct)).Summary;

    private sealed record SummaryJson(string Summary);
}

/// <summary>Queue: KI-Kommentar für diesen Managementbericht schreiben.</summary>
public sealed record SummarizeManagementReport(Guid ManagementSummaryId);

public static class SummarizeManagementReportHandler
{
    public static async Task Handle(SummarizeManagementReport message, IServiceScopeFactory scopes, CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AispaDbContext>();
        if (await TenantModel.TenantOfAsync(db.ManagementSummaries.Where(s => s.Id == message.ManagementSummaryId), ct) is not { } tenantId)
        {
            return;
        }

        scope.ServiceProvider.GetRequiredService<TenantContext>().Use(tenantId);
        await scope.ServiceProvider.GetRequiredService<ManagementSummaries>().WriteAsync(message.ManagementSummaryId, ct);
    }
}

/// <summary>
/// Hängt den KI-Kommentar an den Managementbericht, wie beim Tagesstart (<see cref="BriefingSummaries"/>): die Anfrage wartet nie auf die KI,
/// Zahlen im Text müssen wörtlich in den Fakten stehen, sonst wird er verworfen. Erst ab sieben Tagen: Für einen einzelnen Tag („Heute im Spa“)
/// ändern sich die Zahlen ständig, und ein Kommentar hätte wenig zu sagen.
/// </summary>
internal sealed partial class ManagementSummaries(
    AispaDbContext db,
    ManagementReports reports,
    IManagementSummarizer summarizer,
    IMessageBus bus,
    TimeProvider time,
    ILogger<ManagementSummaries> log)
{
    public const int MinDays = 7;
    public const int MaxLength = 1000;

    private static readonly TimeSpan RequeueAfter = TimeSpan.FromMinutes(2);
    private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-AT");

    public const string Notice = "Von der KI aus den Zahlen dieses Berichts kommentiert. Maßgeblich sind die Zahlen selbst.";

    public async Task<ManagementReportDto> AttachAsync(ManagementReportDto report, CancellationToken ct)
    {
        if (!summarizer.Enabled || report.Until.DayNumber - report.From.DayNumber + 1 < MinDays)
        {
            return report;
        }

        var amounts = !report.AmountsHidden;
        var fingerprint = Fingerprint(report);
        var now = time.GetUtcNow();
        var row = await db.ManagementSummaries.FirstOrDefaultAsync(
            s => s.LocationId == report.LocationId && s.From == report.From && s.Until == report.Until && s.Amounts == amounts, ct);
        if (row?.Fingerprint == fingerprint && (row.Status != BriefingSummaryStatus.Pending || now - row.RequestedAt < RequeueAfter))
        {
            return With(report, row);
        }

        if (row is null)
        {
            row = new ManagementSummary { LocationId = report.LocationId, From = report.From, Until = report.Until, Amounts = amounts, Fingerprint = fingerprint };
            db.ManagementSummaries.Add(row);
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
            return report with { AiSummaryPending = true };
        }

        await bus.PublishAsync(new SummarizeManagementReport(row.Id));
        return With(report, row);
    }

    /// <summary>
    /// Für den Bericht per Mail: den Kommentar gleich schreiben, statt über die Queue; einen schon passenden nimmt er. Ohne KI, bei kurzem Zeitraum
    /// oder wenn der Text verworfen wird: <c>null</c>, die Mail kommt dann ohne Kommentar.
    /// </summary>
    public async Task<string?> CommentNowAsync(ManagementReportDto report, CancellationToken ct)
    {
        var attached = await AttachAsync(report, ct);
        if (attached.AiSummary is { } done || !attached.AiSummaryPending)
        {
            return attached.AiSummary;
        }

        var amounts = !report.AmountsHidden;
        var row = await db.ManagementSummaries.AsNoTracking().FirstOrDefaultAsync(
            s => s.LocationId == report.LocationId && s.From == report.From && s.Until == report.Until && s.Amounts == amounts, ct);
        if (row is null)
        {
            return null;
        }

        await WriteAsync(row.Id, ct);
        return await db.ManagementSummaries.AsNoTracking().Where(s => s.Id == row.Id && s.Status == BriefingSummaryStatus.Done)
            .Select(s => s.Text).FirstOrDefaultAsync(ct);
    }

    /// <summary>
    /// Der schon geschriebene Kommentar zu genau diesen Zahlen, ohne einen neuen anzustoßen (für die Dokumente, T-095 D2); <c>null</c>, wenn es keinen gibt
    /// oder sich die Zahlen seitdem geändert haben.
    /// </summary>
    public async Task<string?> StoredAsync(ManagementReportDto report, CancellationToken ct)
    {
        var amounts = !report.AmountsHidden;
        var fingerprint = Fingerprint(report);
        return await db.ManagementSummaries.AsNoTracking()
            .Where(s => s.LocationId == report.LocationId && s.From == report.From && s.Until == report.Until && s.Amounts == amounts
                && s.Fingerprint == fingerprint && s.Status == BriefingSummaryStatus.Done)
            .Select(s => s.Text).FirstOrDefaultAsync(ct);
    }

    public async Task WriteAsync(Guid id, CancellationToken ct)
    {
        var row = await db.ManagementSummaries.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (row is not { Status: BriefingSummaryStatus.Pending }
            || await reports.ManagementAsync(row.LocationId, row.From, row.Until, row.Amounts, ct) is not { } report
            || Fingerprint(report) != row.Fingerprint)
        {
            // Inzwischen geändert: der neuere Auftrag schreibt.
            return;
        }

        var facts = FactsText(report);
        try
        {
            var text = (await summarizer.SummarizeAsync(facts, ct))?.Trim();
            if (string.IsNullOrEmpty(text) || !BriefingSummaries.OnlyKnownNumbers(text, facts))
            {
                LogRejected(log, row.LocationId, row.From, row.Until);
                row.Status = BriefingSummaryStatus.Failed;
            }
            else
            {
                row.Text = text.Length > MaxLength ? text[..MaxLength] : text;
                row.Status = BriefingSummaryStatus.Done;
            }
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            LogFailed(log, e, row.LocationId, row.From, row.Until);
            row.Status = BriefingSummaryStatus.Failed;
        }

        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Was die KI sieht: Zeitraum, Kennzahlen mit dem Wert des Zeitraums davor, die Hinweise und die Behandlungen und Personen mit Anzahlen. Ohne das
    /// Recht „Berichte mit Beträgen“ fehlen die Beträge schon im Bericht und damit auch hier. Keine Gästenamen, keine Gesundheitsdaten.
    /// </summary>
    internal static string FactsText(ManagementReportDto report)
    {
        var text = new StringBuilder();
        text.AppendLine(CultureInfo.InvariantCulture, $"Zeitraum: {report.From:dd.MM.yyyy} bis {report.Until:dd.MM.yyyy}, verglichen mit dem gleich langen Zeitraum davor.");
        text.AppendLine("Kennzahlen:");
        foreach (var kpi in report.Kpis.Where(k => !(report.AmountsHidden && k.Unit == "€")))
        {
            var previous = kpi.Previous is { } p ? $" (davor {Number(p)} {kpi.Unit})".Replace(" )", ")", StringComparison.Ordinal) : "";
            var line = $"- {kpi.Label}: {Number(kpi.Value)} {kpi.Unit}{previous}".TrimEnd();
            text.AppendLine(line);
        }

        text.AppendLine(CultureInfo.InvariantCulture, $"- Nicht erschienen: {report.NoShows}; storniert: {report.Cancellations}");
        Section(text, "Hinweise", report.Alerts);
        Section(text, "Behandlungen nach Anzahl", report.Treatments.OrderByDescending(t => t.Appointments).Take(8)
            .Select(t => $"{t.Name}: {t.Appointments} Termine{(t.NetRevenue is { } net ? $", netto {Number(net)} €" : "")}"));
        Section(text, "Personal", report.Employees.OrderByDescending(e => e.Utilization).Take(10)
            .Select(e => $"{e.Name}: Auslastung {e.Utilization} %, {e.Appointments} Termine"));
        if (report.Promotions is { Count: > 0 } promotions)
        {
            Section(text, "Aktionen an der Kassa", promotions.Select(ManagementReports.PromotionText));
        }

        return text.ToString();
    }

    internal static string Fingerprint(ManagementReportDto report) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(FactsText(report))));

    private static string Number(decimal value) => value.ToString("0.##", German);

    private static ManagementReportDto With(ManagementReportDto report, ManagementSummary row) => row.Status switch
    {
        BriefingSummaryStatus.Done => report with { AiSummary = row.Text, AiNotice = Notice },
        BriefingSummaryStatus.Pending => report with { AiSummaryPending = true },
        _ => report,
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

    [LoggerMessage(Level = LogLevel.Warning, Message = "KI-Kommentar zum Bericht für Standort {LocationId} ({From} bis {Until}) verworfen (leer oder mit fremden Zahlen).")]
    private static partial void LogRejected(ILogger logger, Guid locationId, DateOnly from, DateOnly until);

    [LoggerMessage(Level = LogLevel.Warning, Message = "KI-Kommentar zum Bericht für Standort {LocationId} ({From} bis {Until}) fehlgeschlagen.")]
    private static partial void LogFailed(ILogger logger, Exception exception, Guid locationId, DateOnly from, DateOnly until);
}
