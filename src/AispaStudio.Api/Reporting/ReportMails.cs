using System.Globalization;
using System.Text;
using AispaStudio.Api.Access;
using AispaStudio.Api.Data;
using AispaStudio.Api.Documents;
using AispaStudio.Api.Mail;
using AispaStudio.Api.Scheduling;
using AispaStudio.Api.System;
using AispaStudio.Api.Tenancy;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Wolverine;

namespace AispaStudio.Api.Reporting;

public enum ReportMailKind
{
    Week,
    Month,
}

/// <summary>Ein verschickter Wochen- oder Monatsbericht je Standort und Zeitraum; erst eingetragen, dann gesendet, damit nichts doppelt hinausgeht.</summary>
public sealed class ReportMailing
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid LocationId { get; init; }
    public ReportMailKind Kind { get; init; }
    public DateOnly From { get; init; }
    public DateOnly Until { get; init; }
    public int Recipients { get; set; }
    public DateTimeOffset SentAt { get; init; }
}

/// <summary>Abschnitt <c>ReportMails</c>: wie oft nachgesehen wird (PollSeconds 0 = gar nicht) und ab welcher Uhrzeit (Ortszeit) gesendet wird.</summary>
public sealed class ReportMailOptions
{
    public const string Section = "ReportMails";

    public int PollSeconds { get; set; } = 3600;

    public int SendHour { get; set; } = 7;

    /// <summary>So lange nach dem Stichtag wird ein ausgefallener Versand noch nachgeholt; danach nicht mehr (z. B. nach einer längeren Pause).</summary>
    public int CatchUpDays { get; set; } = 3;
}

internal static class ReportMailSetup
{
    public static IServiceCollection AddReportMails(this IServiceCollection services, IConfiguration configuration, ModuleSwitches modules)
    {
        services.Configure<ReportMailOptions>(configuration.GetSection(ReportMailOptions.Section));
        services.AddScoped<ReportMailPlanner>();
        services.AddSingleton<ReportMailScheduler>();
        services.AddBackgroundService<ReportMailScheduler>(modules);
        return services;
    }

    /// <summary>Rollen, die den Bericht per Mail bekommen: Inhaber, Manager, Standortleitung.</summary>
    public static bool Receives(StaffRole role) => role is StaffRole.Owner or StaffRole.GeneralManager or StaffRole.Manager;

    /// <summary>
    /// Der Haken steht der Person zur Wahl: ihre Rolle bekommt den Bericht, und im Einzelplatz ist ein Mailversand eingerichtet (<c>Mail:Smtp:Host</c>, T-110).
    /// Ohne Versand gingen die Mails dort nur ins Log; der Server-Betrieb bleibt wie bisher.
    /// </summary>
    public static bool Offered(StaffRole role, ModuleSwitches modules, MailOptions mail) =>
        Receives(role) && (!modules.Standalone || !string.IsNullOrWhiteSpace(mail.Smtp.Host));
}

/// <summary>Sieht regelmäßig in allen Spas nach, ob ein Wochen- oder Monatsbericht fällig ist (<c>ReportMails:PollSeconds</c>).</summary>
internal sealed partial class ReportMailScheduler(
    IServiceScopeFactory scopes,
    TimeProvider time,
    IOptions<ReportMailOptions> options,
    StartupState startup,
    ILogger<ReportMailScheduler> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (options.Value.PollSeconds <= 0)
        {
            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(Math.Max(60, options.Value.PollSeconds)));
        while (await timer.WaitForNextTickAsync(stoppingToken) && await startup.WaitUntilReadyAsync(stoppingToken))
        {
            try
            {
                await RunOnceAsync(time.GetUtcNow(), stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Failed(log, ex);
            }
        }
    }

    /// <summary>Ein Durchlauf über alle nicht gesperrten Spas; liefert die Zahl der verschickten Mails.</summary>
    public async Task<int> RunOnceAsync(DateTimeOffset now, CancellationToken ct)
    {
        List<Guid> tenants;
        await using (var scope = scopes.CreateAsyncScope())
        {
            tenants = await scope.ServiceProvider.GetRequiredService<AispaDbContext>().Tenants
                .Where(t => t.LockedAt == null).Select(t => t.Id).ToListAsync(ct);
        }

        var sent = 0;
        foreach (var tenantId in tenants)
        {
            await using var scope = scopes.CreateAsyncScope();
            scope.ServiceProvider.GetRequiredService<TenantContext>().Use(tenantId);
            sent += await scope.ServiceProvider.GetRequiredService<ReportMailPlanner>().SendDueAsync(now, ct);
        }

        return sent;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Wochen- und Monatsberichte konnten nicht verschickt werden.")]
    private static partial void Failed(ILogger logger, Exception ex);
}

/// <summary>
/// Wochenbericht (Montag bis Sonntag) montags, Monatsbericht am Monatsersten, je ab <c>SendHour</c> Uhr Ortszeit des Standorts: an Inhaber, Manager und
/// Standortleitung mit Zugang zum Standort, die ihn nicht abbestellt haben. Inhalt wie der Managementbericht (Kennzahlen mit Vorperiode, Hinweise), dazu der
/// KI-Kommentar, wenn es ihn gibt, und der Bericht als PDF im Anhang (T-096); Beträge nur für Personen mit dem Recht „Berichte mit Beträgen“.
/// </summary>
internal sealed class ReportMailPlanner(
    AispaDbContext db,
    ManagementReports reports,
    ManagementSummaries summaries,
    ManagementDocumentSources sources,
    IMessageBus bus,
    IOptions<ReportMailOptions> options)
{
    private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-AT");

    public async Task<int> SendDueAsync(DateTimeOffset now, CancellationToken ct)
    {
        var locations = await db.Locations.AsNoTracking().Where(l => l.ClosedFrom == null).OrderBy(l => l.Name).ToListAsync(ct);
        if (locations.Count == 0)
        {
            return 0;
        }

        var people = await (
                from a in db.StaffAccess.AsNoTracking()
                join u in db.Users.AsNoTracking() on a.UserId equals u.Id
                where a.IsActive && a.ReportMails && (a.ExpiresAt == null || a.ExpiresAt > now) && u.Email != null
                select new { Access = a, u.Email, u.DisplayName })
            .ToListAsync(ct);
        people = [.. people.Where(p => ReportMailSetup.Receives(p.Access.Role))];
        if (people.Count == 0)
        {
            return 0;
        }

        var spa = await db.Tenants.AsNoTracking().Select(t => t.Name).FirstAsync(ct);
        var sent = 0;
        foreach (var location in locations)
        {
            var zone = LocalTime.Zone(location.TimeZone);
            var local = TimeZoneInfo.ConvertTime(now, zone).DateTime;
            var today = DateOnly.FromDateTime(local);
            var recipients = people
                .Select(p => (Email: p.Email!, Name: p.DisplayName, Access: new UserAccess(p.Access.UserId, p.Access.Role, p.Access.AllLocations,
                    p.Access.LocationIds.ToHashSet(), null, p.Access.AmountReports)))
                .Where(p => p.Access.CanSee(location.Id))
                .ToList();
            if (recipients.Count == 0)
            {
                continue;
            }

            // Wochenbericht: die letzte volle Woche Montag bis Sonntag, fällig ab Montag.
            var monday = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
            sent += await SendIfDueAsync(location, ReportMailKind.Week, monday.AddDays(-7), monday.AddDays(-1), monday, local, recipients, spa, ct);

            // Monatsbericht: der letzte volle Monat, fällig ab dem Ersten.
            var first = new DateOnly(today.Year, today.Month, 1);
            sent += await SendIfDueAsync(location, ReportMailKind.Month, first.AddMonths(-1), first.AddDays(-1), first, local, recipients, spa, ct);
        }

        return sent;
    }

    private async Task<int> SendIfDueAsync(Location location, ReportMailKind kind, DateOnly from, DateOnly until, DateOnly dueDay, DateTime local,
        List<(string Email, string Name, UserAccess Access)> recipients, string spa, CancellationToken ct)
    {
        var due = dueDay.ToDateTime(new TimeOnly(Math.Clamp(options.Value.SendHour, 0, 23), 0));
        if (local < due || local >= due.AddDays(Math.Max(1, options.Value.CatchUpDays)))
        {
            return 0;
        }

        // Erst eintragen, dann senden: zwei Läufe zugleich (oder zwei Server) schicken so nicht doppelt.
        var mailing = new ReportMailing
        {
            LocationId = location.Id,
            Kind = kind,
            From = from,
            Until = until,
            Recipients = recipients.Count,
            SentAt = DateTimeOffset.UtcNow,
        };
        db.ReportMailings.Add(mailing);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            db.Entry(mailing).State = EntityState.Detached;
            return 0;
        }

        var contents = new Dictionary<bool, (string Body, MailAttachment? Pdf)?>();
        var sent = 0;
        foreach (var (email, name, access) in recipients)
        {
            var amounts = access.Has(AccessRight.ViewAmountReports);
            if (!contents.TryGetValue(amounts, out var content))
            {
                content = await ContentAsync(location, kind, from, until, amounts, ct);
                contents[amounts] = content;
            }

            if (content is not { } mail)
            {
                continue;
            }

            var title = kind == ReportMailKind.Week ? "Wochenbericht" : "Monatsbericht";
            await bus.PublishAsync(new SendMail(new OutgoingMail(email,
                $"{title} {location.Name}: {Period(kind, from, until)}",
                $"Guten Tag{(string.IsNullOrWhiteSpace(name) ? "" : $" {name}")},\n\n{mail.Body}",
                FromName: spa,
                Attachments: mail.Pdf is { } pdf ? [pdf] : null)));
            sent++;
        }

        return sent;
    }

    /// <summary>
    /// Text und PDF für eine Sicht (mit oder ohne Beträge): das PDF wie „PDF speichern“ im Bericht (T-095 D2) mit Vergleich zum Zeitraum davor, ohne Gästenamen, mit
    /// dem KI-Kommentar, wenn es ihn gibt. Über der Grenze für Anhänge geht die Mail ohne PDF hinaus.
    /// </summary>
    private async Task<(string Body, MailAttachment? Pdf)?> ContentAsync(Location location, ReportMailKind kind, DateOnly from, DateOnly until, bool amounts, CancellationToken ct)
    {
        if (await reports.ManagementAsync(location.Id, from, until, amounts, ct) is not { } report)
        {
            return null;
        }

        var comment = await summaries.CommentNowAsync(report, ct);
        var header = await sources.Headers.ForAsync(location.Id, null, ct);
        var document = ManagementReportDocument.Build(report, header, ManagementReports.CompareModes.Previous, guests: false,
            await sources.BenchmarkAsync(report, amounts, ct), comment, comment is null ? null : ManagementSummaries.Notice);
        var pdf = ManagementReportPdf.Build(document);
        return (Body(report, location.Name, kind, comment), pdf.Length <= OutgoingMail.MaxAttachmentBytes
            ? new MailAttachment(ManagementReportPdf.FileName(document, report), DocumentFiles.PdfContentType, pdf)
            : null);
    }

    private static string Period(ReportMailKind kind, DateOnly from, DateOnly until) => kind == ReportMailKind.Month
        ? from.ToString("MMMM yyyy", German)
        : $"{from.ToString("d. MMMM", German)} bis {until.ToString("d. MMMM yyyy", German)}";

    /// <summary>Text der Mail: Kennzahlen mit Vorperiode, Hinweise, KI-Kommentar (mit Hinweis), Weg zum Abbestellen.</summary>
    internal static string Body(ManagementReportDto report, string location, ReportMailKind kind, string? comment)
    {
        var text = new StringBuilder();
        var previous = kind == ReportMailKind.Week ? "Woche davor" : "Monat davor";
        text.AppendLine(CultureInfo.InvariantCulture, $"hier der {(kind == ReportMailKind.Week ? "Wochenbericht" : "Monatsbericht")} für {location}, "
            + $"{report.From.ToString("d. MMMM", German)} bis {report.Until.ToString("d. MMMM yyyy", German)}, im Vergleich zur {previous}.");
        if (comment is not null)
        {
            text.AppendLine();
            text.AppendLine("Kommentar der KI:");
            text.AppendLine(comment);
            text.AppendLine(CultureInfo.InvariantCulture, $"({ManagementSummaries.Notice})");
        }

        if (report.Alerts.Count > 0)
        {
            text.AppendLine();
            text.AppendLine("Worauf achten:");
            foreach (var alert in report.Alerts)
            {
                text.AppendLine(CultureInfo.InvariantCulture, $"- {alert}");
            }
        }

        text.AppendLine();
        text.AppendLine("Kennzahlen:");
        foreach (var kpi in report.Kpis.Where(k => !(report.AmountsHidden && k.Unit == "€")))
        {
            var was = kpi.Previous is { } p ? $" ({previous}: {Value(p, kpi.Unit)})" : "";
            text.AppendLine(CultureInfo.InvariantCulture, $"- {kpi.LabelWithNote()}: {Value(kpi.Value, kpi.Unit)}{was}");
        }

        if (report.Promotions is { Count: > 0 } promotions)
        {
            text.AppendLine();
            text.AppendLine("Aktionen an der Kassa:");
            foreach (var promotion in promotions)
            {
                text.AppendLine(CultureInfo.InvariantCulture, $"- {ManagementReports.PromotionText(promotion)}");
            }
        }

        if (report.AmountsHidden)
        {
            text.AppendLine();
            text.AppendLine("Beträge stehen hier nur mit dem Recht „Berichte mit Beträgen“.");
        }

        text.AppendLine();
        text.AppendLine("Alle Zahlen im Detail: AISPA Studio, Verwaltung › Zahlen › Berichte.");
        text.AppendLine("Abbestellen: dort beim Management den Haken „Wochen- und Monatsbericht per Mail“ entfernen.");
        return text.ToString();
    }

    private static string Value(decimal value, string unit) => unit switch
    {
        "€" => $"{value.ToString("N2", German)} €",
        "%" => $"{value.ToString("0.#", German)} %",
        "" => value.ToString("N0", German),
        _ => $"{value.ToString("0.##", German)} {unit}",
    };
}
