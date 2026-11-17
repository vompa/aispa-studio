using System.Globalization;
using AispaStudio.Api.Access;
using AispaStudio.Api.Data;
using AispaStudio.Api.Exports;
using AispaStudio.Api.Scheduling;
using AispaStudio.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using static AispaStudio.Api.Exports.ExportRequests;

namespace AispaStudio.Api.Reminders;

/// <summary>
/// Versandprotokoll (P8, 17.11.2026): Terminerinnerungen und Wiederbuchungs-Hinweise in einer Liste, filterbar nach Art, Kanal und Ergebnis, mit Zusammenfassung
/// der letzten Tage und Export als Excel oder CSV. Es zeigt nur, was die beiden Dienste schon aufgezeichnet haben; verschickt wird weiter über deren Queue.
/// Erinnerungen, die mit einem anderen Termin des Gastes am selben Tag zusammengefasst wurden, stehen nicht einzeln darin, denn sie haben nichts versendet.
/// </summary>
internal static class DispatchLogEndpoints
{
    private const int DefaultDays = 30;

    private const int MaxDays = 366;

    private const int DefaultLimit = 200;

    private const int MaxLimit = 500;

    public static IEndpointRouteBuilder MapDispatchLogEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/dispatch-log").WithTags("Versandprotokoll").RequirePermission(AccessRight.ManageOrganization);

        group.MapGet("", async Task<Results<Ok<List<DispatchLogEntryDto>>, ValidationProblem>> (
                string? kind, string? channel, string? result, int? days, int? limit, AispaDbContext db, TimeProvider time, CancellationToken ct) =>
            {
                if (Parse(kind, channel, result, days) is not { } filter)
                {
                    return Problem("filter", FilterMessage);
                }

                var size = Math.Clamp(limit ?? DefaultLimit, 1, MaxLimit);
                return TypedResults.Ok((await EntriesAsync(db, filter, time.GetUtcNow(), size, ct)).ToList());
            })
            .WithName("ListDispatchLog")
            .WithSummary("Versandprotokoll: Terminerinnerungen und Wiederbuchungs-Hinweise der letzten Tage (Vorgabe 30, höchstens 366), neueste zuerst. "
                + "Filter: kind (reminder, rebook), channel (email, sms, whatsapp), result (sent, failed, skipped, queued).");

        group.MapGet("/summary", async Task<Results<Ok<DispatchSummaryDto>, ValidationProblem>> (int? days, AispaDbContext db, TimeProvider time, CancellationToken ct) =>
            {
                if (Parse(null, null, null, days) is not { } filter)
                {
                    return Problem("days", FilterMessage);
                }

                var since = time.GetUtcNow().AddDays(-filter.Days);
                var reminders = await db.AppointmentReminders.AsNoTracking().Where(r => r.BundledWithId == null && (r.SentAt ?? r.CreatedAt) >= since)
                    .GroupBy(r => r.State).Select(g => new { State = g.Key, Count = g.Count() }).ToListAsync(ct);
                var rebooks = await db.RebookReminders.AsNoTracking().Where(r => (r.SentAt ?? r.CreatedAt) >= since)
                    .GroupBy(r => r.State).Select(g => new { State = g.Key, Count = g.Count() }).ToListAsync(ct);
                return TypedResults.Ok(new DispatchSummaryDto(filter.Days,
                    Summary(reminders.ToDictionary(r => r.State, r => r.Count)), Summary(rebooks.ToDictionary(r => r.State, r => r.Count))));
            })
            .WithName("GetDispatchSummary")
            .WithSummary("Zahlen des Versandprotokolls der letzten Tage (Vorgabe 30), je Art: verschickt, fehlgeschlagen, entfallen.");

        group.MapGet("/export", async Task<Results<FileContentHttpResult, ValidationProblem>> (
                string? kind, string? channel, string? result, int? days, string? format, AispaDbContext db, ExportLog log, TimeProvider time, CancellationToken ct) =>
            {
                if (TableFormat(format) is not { } fileFormat)
                {
                    return Problem("format", TableFormatsOnly);
                }

                if (Parse(kind, channel, result, days) is not { } filter)
                {
                    return Problem("filter", FilterMessage);
                }

                var now = time.GetUtcNow();
                var entries = (await EntriesAsync(db, filter, now, TableExport.MaxRows + 1, ct)).ToList();
                if (entries.Count > TableExport.MaxRows)
                {
                    return Problem("days", TableExport.TooLargeMessage);
                }

                var zoneId = await db.Locations.AsNoTracking().OrderBy(l => l.Name).Select(l => l.TimeZone).FirstOrDefaultAsync(ct);
                var zone = zoneId is not null && LocalTime.IsKnownZone(zoneId) ? LocalTime.Zone(zoneId) : TimeZoneInfo.Utc;
                var table = ToTable(entries, zone);
                var description = $"letzte {filter.Days} Tage"
                    + string.Join("", new[] { kind, channel, result }.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => $", {p!.Trim().ToLowerInvariant()}"));
                return await FileAsync(table, fileFormat, "Versandprotokoll", description, "Versandprotokoll", DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, zone).DateTime), log, ct) switch
                {
                    { Result: FileContentHttpResult file } => file,
                    { Result: ValidationProblem problem } => problem,
                    _ => Problem("format", TableFormatsOnly),
                };
            })
            .WithName("ExportDispatchLog")
            .WithSummary("Das Versandprotokoll als Excel oder CSV mit denselben Filtern wie die Liste (höchstens so viele Zeilen wie jeder Export).");

        return routes;
    }

    private const string FilterMessage = "Filter unbekannt. kind: reminder oder rebook; channel: email, sms oder whatsapp; result: sent, failed, skipped oder queued; days: 1 bis 366.";

    private sealed record Filter(DispatchKind? Kind, ReminderChannel? Channel, ReminderState? Result, int Days);

    private static Filter? Parse(string? kind, string? channel, string? result, int? days)
    {
        if (days is < 1 or > MaxDays)
        {
            return null;
        }

        DispatchKind? parsedKind = null;
        if (!string.IsNullOrWhiteSpace(kind))
        {
            if (!Enum.TryParse<DispatchKind>(kind.Trim(), true, out var k) || !Enum.IsDefined(k))
            {
                return null;
            }

            parsedKind = k;
        }

        ReminderChannel? parsedChannel = null;
        if (!string.IsNullOrWhiteSpace(channel))
        {
            if (!Enum.TryParse<ReminderChannel>(channel.Trim(), true, out var c) || !Enum.IsDefined(c))
            {
                return null;
            }

            parsedChannel = c;
        }

        ReminderState? parsedResult = null;
        if (!string.IsNullOrWhiteSpace(result))
        {
            if (!Enum.TryParse<ReminderState>(result.Trim(), true, out var r) || !Enum.IsDefined(r))
            {
                return null;
            }

            parsedResult = r;
        }

        return new Filter(parsedKind, parsedChannel, parsedResult, days ?? DefaultDays);
    }

    private static DispatchKindSummaryDto Summary(Dictionary<ReminderState, int> counts) =>
        new(counts.GetValueOrDefault(ReminderState.Sent), counts.GetValueOrDefault(ReminderState.Failed), counts.GetValueOrDefault(ReminderState.Skipped));

    /// <summary>Beide Quellen im Zeitraum mit den Filtern, neueste zuerst; je Quelle höchstens <paramref name="size"/> Zeilen, dann gemeinsam gekürzt.</summary>
    private static async Task<IEnumerable<DispatchLogEntryDto>> EntriesAsync(AispaDbContext db, Filter filter, DateTimeOffset now, int size, CancellationToken ct)
    {
        var since = now.AddDays(-filter.Days);
        var entries = new List<DispatchLogEntryDto>();
        if (filter.Kind is null or DispatchKind.Reminder)
        {
            entries.AddRange(await RemindersAsync(db, filter, since, size, ct));
        }

        // Wiederbuchungs-Hinweise gehen immer per Mail.
        if (filter.Kind is null or DispatchKind.Rebook && filter.Channel is null or ReminderChannel.Email)
        {
            entries.AddRange(await RebooksAsync(db, filter, since, size, ct));
        }

        return entries.OrderByDescending(e => e.At).Take(size);
    }

    private static async Task<List<DispatchLogEntryDto>> RemindersAsync(AispaDbContext db, Filter filter, DateTimeOffset since, int size, CancellationToken ct)
    {
        var query = db.AppointmentReminders.AsNoTracking().Where(r => r.BundledWithId == null && (r.SentAt ?? r.CreatedAt) >= since);
        if (filter.Channel is { } channel)
        {
            query = query.Where(r => r.Channel == channel);
        }

        if (filter.Result is { } state)
        {
            query = query.Where(r => r.State == state);
        }

        var rows = await query.OrderByDescending(r => r.SentAt ?? r.CreatedAt).Take(size).ToListAsync(ct);
        var appointmentIds = rows.Select(r => r.AppointmentId).Distinct().ToList();
        var appointments = await db.Appointments.AsNoTracking().Where(a => appointmentIds.Contains(a.Id))
            .Select(a => new { a.Id, a.CustomerId, a.TreatmentId }).ToDictionaryAsync(a => a.Id, ct);
        var names = await NamesAsync(db, appointments.Values.Select(a => a.CustomerId).Distinct().ToList(), ct);
        var treatments = await TreatmentNamesAsync(db, appointments.Values.Select(a => a.TreatmentId).Distinct().ToList(), ct);
        return rows.Select(r =>
        {
            var appointment = appointments.GetValueOrDefault(r.AppointmentId);
            return new DispatchLogEntryDto(r.Id, DispatchKind.Reminder, appointment?.CustomerId ?? Guid.Empty,
                appointment is null ? "" : names.GetValueOrDefault(appointment.CustomerId, ""),
                appointment is null ? "" : treatments.GetValueOrDefault(appointment.TreatmentId, ""),
                r.Channel, r.State.ToString(), r.SentAt ?? r.CreatedAt, r.AppointmentStart, null, r.Error);
        }).ToList();
    }

    private static async Task<List<DispatchLogEntryDto>> RebooksAsync(AispaDbContext db, Filter filter, DateTimeOffset since, int size, CancellationToken ct)
    {
        var query = db.RebookReminders.AsNoTracking().Where(r => (r.SentAt ?? r.CreatedAt) >= since);
        if (filter.Result is { } state)
        {
            query = query.Where(r => r.State == state);
        }

        var rows = await query.OrderByDescending(r => r.SentAt ?? r.CreatedAt).Take(size).ToListAsync(ct);
        var names = await NamesAsync(db, rows.Select(r => r.CustomerId).Distinct().ToList(), ct);
        var treatments = await TreatmentNamesAsync(db, rows.Select(r => r.TreatmentId).Distinct().ToList(), ct);
        return rows.Select(r => new DispatchLogEntryDto(r.Id, DispatchKind.Rebook, r.CustomerId, names.GetValueOrDefault(r.CustomerId, ""),
            treatments.GetValueOrDefault(r.TreatmentId, ""), ReminderChannel.Email, r.State.ToString(), r.SentAt ?? r.CreatedAt, null, r.Text, r.Error)).ToList();
    }

    private static Task<Dictionary<Guid, string>> NamesAsync(AispaDbContext db, List<Guid> ids, CancellationToken ct) =>
        db.Customers.AsNoTracking().Where(c => ids.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.InHouseName, ct);

    private static Task<Dictionary<Guid, string>> TreatmentNamesAsync(AispaDbContext db, List<Guid> ids, CancellationToken ct) =>
        db.Treatments.AsNoTracking().Where(t => ids.Contains(t.Id)).ToDictionaryAsync(t => t.Id, t => t.Name, ct);

    private static ExportTable ToTable(IReadOnlyList<DispatchLogEntryDto> entries, TimeZoneInfo zone) => new("Versandprotokoll",
        [
            new ExportColumn("Datum", ColumnKind.Date),
            new ExportColumn("Uhrzeit", ColumnKind.Time),
            new ExportColumn("Art", ColumnKind.Text),
            new ExportColumn("Gast", ColumnKind.Text),
            new ExportColumn("Behandlung", ColumnKind.Text),
            new ExportColumn("Termin", ColumnKind.Text),
            new ExportColumn("Kanal", ColumnKind.Text),
            new ExportColumn("Ergebnis", ColumnKind.Text),
            new ExportColumn("Text", ColumnKind.Text),
            new ExportColumn("Fehler", ColumnKind.Text),
        ],
        [.. entries.Select(e =>
        {
            var at = TimeZoneInfo.ConvertTime(e.At, zone);
            return new object?[]
            {
                DateOnly.FromDateTime(at.DateTime), TimeOnly.FromDateTime(at.DateTime),
                e.Kind == DispatchKind.Reminder ? "Terminerinnerung" : "Wiederbuchung", e.Recipient, e.Subject,
                e.AppointmentStart is { } start ? TimeZoneInfo.ConvertTime(start, zone).ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture) : null,
                e.Channel switch { ReminderChannel.Sms => "SMS", ReminderChannel.WhatsApp => "WhatsApp", _ => "Mail" },
                StateText(e.State), e.Text, e.Error,
            };
        })]);

    private static string StateText(string state) => state switch
    {
        nameof(ReminderState.Sent) => "Gesendet",
        nameof(ReminderState.Failed) => "Fehlgeschlagen",
        nameof(ReminderState.Skipped) => "Entfallen",
        nameof(ReminderState.Queued) => "Wird geschrieben",
        _ => state,
    };
}
