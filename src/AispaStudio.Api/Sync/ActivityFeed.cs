using System.Globalization;
using System.Text.Json;
using AispaStudio.Api.Data;
using AispaStudio.Api.Online;
using AispaStudio.Api.Pos;
using AispaStudio.Api.Scheduling;
using AispaStudio.Api.Tenancy;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;

namespace AispaStudio.Api.Sync;

/// <summary>
/// Verlauf eines Standorts für die Seite „Verlauf“ (Arbeitsanweisung Verlauf, Aufgabe A): die Zeilen aus <c>sync.history</c> (ADR 0082) und die Zahlungen aus der Kassa
/// aus der Kassa in einer Reihenfolge, neueste zuerst, gefiltert und seitenweise. Filtern, Suchen und Seiten rechnet die Datenbank: eine Abfrage mit festen
/// Bedingungstexten und allen Werten als Parameter liefert die Schlüssel der Seite (Zeit, Nummer, Art); danach werden nur diese Zeilen gelesen und mit den
/// vorhandenen Satzbildnern (<see cref="EntityHistory"/>, <see cref="AppointmentHistory"/>, <see cref="PaymentHistory"/>) beschrieben, damit Termin-Panel,
/// Kundenkarte und Verlaufsseite denselben Wortlaut zeigen. Der Seitenzeiger ist die Nummer der letzten Zeile; Belege tragen eine negative Pseudo-Nummer.
/// </summary>
internal sealed class ActivityFeed(
    AispaDbContext db, TenantContext tenants, EntityHistory entities, AppointmentHistory appointments, PaymentHistory payments, OnlineRules rules)
{
    /// <summary>Die Anfrage der Seite; <c>Kinds</c> sind die Arten, die diese Person sehen darf und angefragt hat.</summary>
    public sealed record Query(
        Guid LocationId, DateOnly? From, DateOnly? Until, IReadOnlySet<ActivityKind> Kinds, string? Actor, ActivityChannel? Channel, Guid? CustomerId, string? Search,
        long? Before, int Take);

    private static readonly string[] RosterTables = ["RosterDays", "RosterDayEntries"];

    /// <summary><c>null</c>, wenn es den Standort (für diesen Mandanten) nicht gibt.</summary>
    public async Task<ActivityPage?> ReadAsync(Query query, CancellationToken ct)
    {
        var location = await db.Locations.AsNoTracking().Where(l => l.Id == query.LocationId).Select(l => new { l.TimeZone }).FirstOrDefaultAsync(ct);
        if (location is null || tenants.TenantId is not { } tenantId)
        {
            return null;
        }

        var zone = LocalTime.Zone(location.TimeZone);
        var staff = await db.Employees.AsNoTracking().Where(e => e.WorkingHours.Any(w => w.LocationId == query.LocationId)).Select(e => e.Id.ToString()).ToArrayAsync(ct);
        var keys = await KeysAsync(query, tenantId, zone, staff, ct);
        var actors = await ActorsAsync(query, tenantId, zone, staff, ct);
        var more = keys.Count > query.Take;
        var page = keys.Take(query.Take).ToList();

        var seqs = page.Where(k => k.Receipt is null).Select(k => k.Seq).ToArray();
#pragma warning disable EF1002
        var rows = seqs.Length == 0
            ? []
            : await db.Database.SqlQueryRaw<HistoryRow>($$"""SELECT {{HistoryRow.Columns}} FROM sync.history h WHERE h.seq = ANY ({0})""", seqs).ToDictionaryAsync(r => r.Seq, ct);
#pragma warning restore EF1002
        var events = (await payments.ForReceiptsAsync([.. page.Select(k => k.Receipt).OfType<Guid>()], ct)).ToDictionary(e => e.ReceiptId);

        var appointmentRows = page.Select(k => rows.GetValueOrDefault(k.Seq)).OfType<HistoryRow>().Where(r => r.Table == "Appointments").ToList();
        var otherRows = page.Select(k => rows.GetValueOrDefault(k.Seq)).OfType<HistoryRow>().Where(r => r.Table != "Appointments").ToList();
        var facts = await AppointmentFactsAsync(appointmentRows, ct);
        var appointmentTexts = await appointments.DescribeAsync(appointmentRows, r => facts[r.Seq].Source, zone, ct);
        var otherTexts = await entities.DescribeAsync(otherRows, ct);
        var described = appointmentRows.Select((r, i) => (r.Seq, Text: appointmentTexts[i])).Concat(otherRows.Select((r, i) => (r.Seq, Text: otherTexts[i])))
            .ToDictionary(p => p.Seq, p => p.Text);

        var userIds = rows.Values.Select(r => r.UserId).OfType<string>().Distinct().ToList();
        var people = await db.Users.AsNoTracking().Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.DisplayName, ct);
        var customerIds = rows.Values.Select(r => r.CustomerId).Concat(facts.Values.Select(f => f.CustomerId)).Concat(events.Values.Select(e => e.CustomerId))
            .OfType<Guid>().Distinct().ToList();
        var customers = await db.Customers.AsNoTracking().Where(c => customerIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => PersonNames.InHouse(c.FirstName, c.LastName, c.PreferredName), ct);
        var deadline = TimeSpan.FromHours((await rules.ForAsync(query.LocationId, ct)).CancellationDeadlineHours);

        var items = new List<ActivityEntry>(page.Count);
        foreach (var key in page)
        {
            if (key.Receipt is { } receiptId)
            {
                if (events.TryGetValue(receiptId, out var e))
                {
                    items.Add(new ActivityEntry(e.Seq, e.At, e.Kind, e.Text, e.Actor, e.Channel, e.CustomerId, Name(customers, e.CustomerId), e.AppointmentId, e.TreatmentId, null, [], null,
                        e.ReceiptId));
                }

                continue;
            }

            if (!rows.TryGetValue(key.Seq, out var row) || !Enum.TryParse<ActivityKind>(key.Kind, out var kind))
            {
                continue;
            }

            var text = described[row.Seq];
            var actor = row.UserId is { } userId ? people.GetValueOrDefault(userId, HistoryActor.DeletedPerson) : null;
            var channel = ChannelOf(HistoryActor.Channel(row.Channel));
            var fact = facts.GetValueOrDefault(row.Seq);
            var treatmentId = fact?.TreatmentId ?? (row.Table is "Treatments" or "TreatmentLocationPrices" or "TreatmentPriceRules" ? GuidOf(row.Subject) : null);
            var employeeId = fact?.EmployeeId ?? (RosterTables.Contains(row.Table) ? GuidOf(row.Subject) : null);
            var customerId = row.CustomerId ?? fact?.CustomerId;
            bool? late = kind == ActivityKind.Cancelled && fact?.Start is { } start
                ? deadline > TimeSpan.Zero && row.ChangedAt <= start && start - row.ChangedAt < deadline
                : null;
            items.Add(new ActivityEntry(row.Seq, row.ChangedAt, kind, text.Text, actor, channel, customerId, Name(customers, customerId), fact?.Id, treatmentId, employeeId,
                text.Values, late));
        }

        return new ActivityPage(items, more && page.Count > 0 ? page[^1].Seq : null, actors);
    }

    /// <summary>Rezeption (Team, Mitarbeiter-App), online vom Gast, Terminannahme über einen Kanal oder die KI, sonst Hintergrund (Job, Abgleich). Gegenstück zum SQL in <see cref="KeysAsync"/>.</summary>
    internal static ActivityChannel ChannelOf(ChangeChannel channel) => channel switch
    {
        ChangeChannel.Team or ChangeChannel.StaffApp => ActivityChannel.Reception,
        ChangeChannel.Online => ActivityChannel.Online,
        ChangeChannel.Job or ChangeChannel.Sync => ActivityChannel.Background,
        _ => ActivityChannel.Assistant,
    };

    private static string? Name(Dictionary<Guid, string> customers, Guid? customerId) => customerId is { } id ? customers.GetValueOrDefault(id) : null;

    private static Guid? GuidOf(string? value) => Guid.TryParse(value, out var id) ? id : null;

    /// <summary>Standort, Behandlung, Gast, Person, Beginn und Quelle eines Termins zur Zeile: aus <c>context</c> (seit dieser Aufgabe), sonst aus dem Termin selbst.</summary>
    private async Task<Dictionary<long, AppointmentFact>> AppointmentFactsAsync(List<HistoryRow> rows, CancellationToken ct)
    {
        var ids = rows.ToDictionary(r => r.Seq, r => IdOf(r.RowKey));
        var missing = rows.Where(r => r.Context is null).Select(r => ids[r.Seq]).OfType<Guid>().Distinct().ToList();
        var stored = missing.Count == 0
            ? []
            : await db.Appointments.AsNoTracking().Where(a => missing.Contains(a.Id))
                .Select(a => new AppointmentFact(a.Id, a.TreatmentId, a.CustomerId, a.EmployeeId, a.Start, a.Source)).ToDictionaryAsync(a => a.Id!.Value, ct);

        var facts = new Dictionary<long, AppointmentFact>();
        foreach (var row in rows)
        {
            var id = ids[row.Seq];
            if (row.Context is { } json)
            {
                using var context = JsonDocument.Parse(json);
                var root = context.RootElement;
                facts[row.Seq] = new AppointmentFact(id, GuidIn(root, "TreatmentId"), GuidIn(root, "CustomerId"), GuidIn(root, "EmployeeId"),
                    root.TryGetProperty("Start", out var start) && start.ValueKind == JsonValueKind.String ? start.GetDateTimeOffset() : null,
                    root.TryGetProperty("Source", out var source) && Enum.TryParse<AppointmentSource>(source.GetString(), out var parsed) ? parsed : AppointmentSource.Reception);
            }
            else
            {
                facts[row.Seq] = id is { } known && stored.TryGetValue(known, out var fact) ? fact : new AppointmentFact(id, null, null, null, null, AppointmentSource.Reception);
            }
        }

        return facts;
    }

    private static Guid? IdOf(string? rowKey)
    {
        if (rowKey is null)
        {
            return null;
        }

        using var key = JsonDocument.Parse(rowKey);
        return GuidIn(key.RootElement, "Id");
    }

    private static Guid? GuidIn(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && Guid.TryParse(value.GetString(), out var id) ? id : null;

    private sealed record AppointmentFact(Guid? Id, Guid? TreatmentId, Guid? CustomerId, Guid? EmployeeId, DateTimeOffset? Start, AppointmentSource Source);

    /// <summary>Ein Schlüssel der Seite: Nummer (Verlaufszeile oder Pseudo-Nummer des Belegs), Zeit, Art und bei Belegen die Beleg-Id.</summary>
    private sealed class FeedKey
    {
        public long Seq { get; init; }
        public DateTimeOffset At { get; init; }
        public required string Kind { get; init; }
        public Guid? Receipt { get; init; }
    }

    /// <summary>
    /// Die Schlüssel der Seite plus einen (für <c>NextBefore</c>). Verlaufszeilen des Mandanten, am Standort (Termine über Standort in <c>context</c> oder am Termin;
    /// Dienstplan über Standort in <c>context</c> oder die Personen des Standorts; Kunden, Behandlungen, Zugänge und Firmendaten mandantenweit) und Belege der
    /// Kassen des Standorts mit Terminbezug; die Art wird in SQL so bestimmt wie in den Satzbildnern.
    /// </summary>
    private async Task<List<FeedKey>> KeysAsync(Query query, Guid tenantId, TimeZoneInfo zone, string[] staff, CancellationToken ct)
    {
        // Fester Abfragetext; alle Werte sind Parameter ({0} … {13} sind die Parameter in dieser Reihenfolge).
#pragma warning disable EF1002
        return await db.Database.SqlQueryRaw<FeedKey>(KeysSql, Parameters(query, tenantId, zone, staff, query.Actor)).ToListAsync(ct);
#pragma warning restore EF1002
    }

    /// <summary>Die Personen, die im Zeitraum unter den übrigen Filtern vorkommen, ohne den Filter „Person“ selbst; der Pseudo-Benutzer der Online-Anzahlung bleibt weg.</summary>
    private async Task<List<ActivityActor>> ActorsAsync(Query query, Guid tenantId, TimeZoneInfo zone, string[] staff, CancellationToken ct)
    {
#pragma warning disable EF1002
        var ids = await db.Database.SqlQueryRaw<string>(ActorsSql, Parameters(query, tenantId, zone, staff, actor: null)).ToListAsync(ct);
#pragma warning restore EF1002
        var people = await db.Users.AsNoTracking().Where(u => ids.Contains(u.Id)).Select(u => new ActivityActor(u.Id, u.DisplayName)).ToListAsync(ct);
        return [.. people.OrderBy(p => p.Name, StringComparer.Create(CultureInfo.GetCultureInfo("de-AT"), ignoreCase: true))];
    }

    private static object[] Parameters(Query query, Guid tenantId, TimeZoneInfo zone, string[] staff, string? actor)
    {
        var from = query.From is { } f ? LocalTime.Day(f, zone).From : (DateTimeOffset?)null;
        var until = query.Until is { } u ? LocalTime.Day(u, zone).Until : (DateTimeOffset?)null;
        return
        [
            new NpgsqlParameter("tenant", NpgsqlDbType.Uuid) { Value = tenantId },
            new NpgsqlParameter("kinds", NpgsqlDbType.Array | NpgsqlDbType.Text) { Value = query.Kinds.Select(k => k.ToString()).ToArray() },
            new NpgsqlParameter("location", NpgsqlDbType.Uuid) { Value = query.LocationId },
            new NpgsqlParameter("staff", NpgsqlDbType.Array | NpgsqlDbType.Text) { Value = staff },
            new NpgsqlParameter("from", NpgsqlDbType.TimestampTz) { Value = (object?)from ?? DBNull.Value },
            new NpgsqlParameter("until", NpgsqlDbType.TimestampTz) { Value = (object?)until ?? DBNull.Value },
            new NpgsqlParameter("actor", NpgsqlDbType.Text) { Value = (object?)actor ?? DBNull.Value },
            new NpgsqlParameter("channel", NpgsqlDbType.Text) { Value = (object?)query.Channel?.ToString() ?? DBNull.Value },
            new NpgsqlParameter("customer", NpgsqlDbType.Uuid) { Value = (object?)query.CustomerId ?? DBNull.Value },
            new NpgsqlParameter("search", NpgsqlDbType.Text) { Value = query.Search is { } s ? $"%{Escape(s)}%" : DBNull.Value },
            new NpgsqlParameter("company", NpgsqlDbType.Array | NpgsqlDbType.Text) { Value = EntityHistory.CompanyColumns },
            new NpgsqlParameter("before", NpgsqlDbType.Bigint) { Value = (object?)query.Before ?? DBNull.Value },
            new NpgsqlParameter("take", NpgsqlDbType.Integer) { Value = query.Take + 1 },
            new NpgsqlParameter("onlineUser", NpgsqlDbType.Text) { Value = ReceiptOnlineDepositHandler.OnlineUser },
        ];
    }

    private static string Escape(string search) =>
        search.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("%", "\\%", StringComparison.Ordinal).Replace("_", "\\_", StringComparison.Ordinal);

    private const string HistoryKindSql = """
        CASE
            WHEN h.table_schema = 'health' THEN 'CustomerChanged'
            WHEN h.table_name = 'Appointments' THEN CASE
                WHEN h.operation = 'I' THEN 'Booked'
                WHEN h.operation = 'D' THEN 'AppointmentChanged'
                WHEN h.changes ? 'Status' THEN CASE
                    WHEN h.changes -> 'Status' ->> 1 = 'Cancelled' THEN 'Cancelled'
                    WHEN h.changes -> 'Status' ->> 0 IN ('Cancelled', 'NoShow') AND h.changes -> 'Status' ->> 1 = 'Booked' THEN 'Restored'
                    ELSE 'StatusChanged' END
                WHEN h.changes ?| ARRAY['Start', 'EmployeeId', 'RoomId'] THEN 'Moved'
                ELSE 'AppointmentChanged' END
            WHEN h.table_name = 'Customers' AND h.operation = 'I' THEN 'CustomerCreated'
            WHEN h.table_name IN ('Customers', 'CustomerEmails') THEN 'CustomerChanged'
            WHEN h.table_name IN ('Treatments', 'TreatmentLocationPrices', 'TreatmentPriceRules') THEN 'TreatmentChanged'
            WHEN h.table_name IN ('RosterDays', 'RosterDayEntries') THEN 'RosterChanged'
            WHEN h.table_name = 'StaffAccess' THEN 'AccessChanged'
            WHEN h.table_name = 'Tenants' THEN 'CompanyChanged'
        END
        """;

    private const string HistoryChannelSql = """
        CASE WHEN h.channel IN ('Team', 'StaffApp') THEN 'Reception' WHEN h.channel = 'Online' THEN 'Online' WHEN h.channel IN ('Job', 'Sync') THEN 'Background' ELSE 'Assistant' END
        """;

    private const string ReceiptKindSql = """
        CASE WHEN r."Kind" = 'Void' THEN 'ReceiptVoided'
             WHEN EXISTS (SELECT 1 FROM aispa."AppointmentDeposits" d WHERE d."ReceiptId" = r."Id") THEN 'Deposit'
             ELSE 'Paid' END
        """;

    /// <summary>Die Zeilen des Verlaufs und die Belege des Standorts in einer Reihe (mit allen Filtern außer dem Seitenzeiger); Basis für <see cref="KeysSql"/> und <see cref="ActorsSql"/>.</summary>
    private static readonly string FeedCte = $$"""
        WITH feed AS (
            SELECT h.seq AS seq, h.changed_at AS at, k.kind AS kind, NULL::uuid AS receipt, h.user_id AS actor
              FROM sync.history h
              LEFT JOIN aispa."Appointments" a ON h.table_name = 'Appointments' AND a."Id" = (h.row_key ->> 'Id')::uuid
              LEFT JOIN aispa."Customers" c ON c."Id" = h.customer_id
              LEFT JOIN aispa."Treatments" t ON t."Id" = CASE
                  WHEN h.table_name = 'Appointments' THEN COALESCE((h.context ->> 'TreatmentId')::uuid, a."TreatmentId")
                  WHEN h.table_name IN ('Treatments', 'TreatmentLocationPrices', 'TreatmentPriceRules') THEN h.subject::uuid END
              LEFT JOIN aispa."AspNetUsers" u ON u."Id" = h.user_id
              LEFT JOIN aispa."Employees" e ON e."Id" = CASE WHEN h.table_name IN ('RosterDays', 'RosterDayEntries') THEN h.subject::uuid END
              CROSS JOIN LATERAL (SELECT {{HistoryKindSql}} AS kind) k
             WHERE h.tenant_id = {0}
               AND k.kind = ANY ({1})
               AND (
                    (h.table_name = 'Appointments' AND COALESCE((h.context ->> 'LocationId')::uuid, a."LocationId") = {2})
                 OR (h.table_name IN ('RosterDays', 'RosterDayEntries') AND ((h.context ->> 'LocationId')::uuid = {2} OR h.subject = ANY ({3})))
                 OR h.table_name NOT IN ('Appointments', 'RosterDays', 'RosterDayEntries'))
               AND ({4}::timestamptz IS NULL OR h.changed_at >= {4})
               AND ({5}::timestamptz IS NULL OR h.changed_at < {5})
               AND ({6}::text IS NULL OR h.user_id = {6})
               AND ({7}::text IS NULL OR {{HistoryChannelSql}} = {7})
               AND ({8}::uuid IS NULL OR h.customer_id = {8})
               AND ({9}::text IS NULL
                    OR (c."FirstName" || ' ' || c."LastName") ILIKE {9} OR c."PreferredName" ILIKE {9} OR t."Name" ILIKE {9} OR u."DisplayName" ILIKE {9} OR e."DisplayName" ILIKE {9}
                    OR (h.changes -> 'CancelReason' ->> 1) ILIKE {9})
               AND NOT (h.table_name = 'RosterDays' AND h.operation = 'I'
                        AND EXISTS (SELECT 1 FROM sync.history x WHERE x.table_name = 'RosterDayEntries' AND x.subject = h.subject AND x.changed_at = h.changed_at))
               AND NOT (h.table_name = 'Tenants' AND h.operation = 'U' AND NOT (h.changes ?| {10}))
            UNION ALL
            SELECT {{PaymentHistory.PseudoSeqSql}} AS seq, r."IssuedAt" AS at, k.kind AS kind, r."Id" AS receipt,
                   CASE WHEN r."CreatedByUserId" <> {13} THEN r."CreatedByUserId" END AS actor
              FROM aispa."Receipts" r
              JOIN aispa."CashRegisters" g ON g."Id" = r."RegisterId"
              LEFT JOIN aispa."Receipts" o ON o."Id" = r."VoidsReceiptId"
              LEFT JOIN aispa."Customers" c ON c."Id" = r."CustomerId"
              LEFT JOIN aispa."AspNetUsers" u ON u."Id" = r."CreatedByUserId"
              CROSS JOIN LATERAL (SELECT {{ReceiptKindSql}} AS kind) k
             WHERE g."LocationId" = {2}
               AND r."Kind" IN ('Sale', 'Void')
               AND k.kind = ANY ({1})
               AND ({4}::timestamptz IS NULL OR r."IssuedAt" >= {4})
               AND ({5}::timestamptz IS NULL OR r."IssuedAt" < {5})
               AND ({6}::text IS NULL OR r."CreatedByUserId" = {6})
               AND ({7}::text IS NULL OR CASE WHEN r."CreatedByUserId" IS NOT NULL AND r."CreatedByUserId" <> {13} THEN 'Reception' WHEN k.kind = 'Deposit' THEN 'Online' ELSE 'Background' END = {7})
               AND ({8}::uuid IS NULL OR r."CustomerId" = {8})
               AND ({9}::text IS NULL
                    OR (c."FirstName" || ' ' || c."LastName") ILIKE {9} OR c."PreferredName" ILIKE {9} OR u."DisplayName" ILIKE {9}
                    OR (g."Code" || ' Nr. ' || COALESCE(o."Number", r."Number")::text) ILIKE {9}
                    OR EXISTS (SELECT 1 FROM aispa."ReceiptLine" l WHERE l."ReceiptId" = COALESCE(o."Id", r."Id") AND l."Description" ILIKE {9}))
        )
        """;

    private static readonly string KeysSql = FeedCte + """
        SELECT f.seq AS "Seq", f.at AS "At", f.kind AS "Kind", f.receipt AS "Receipt"
          FROM feed f
         WHERE ({11}::bigint IS NULL OR (f.at, f.seq) < (SELECT p.at, p.seq FROM feed p WHERE p.seq = {11}))
         ORDER BY f.at DESC, f.seq DESC
         LIMIT {12}
        """;

    private static readonly string ActorsSql = FeedCte + """
        SELECT DISTINCT f.actor AS "Value"
          FROM feed f
         WHERE f.actor IS NOT NULL AND {11}::bigint IS NULL AND {12}::integer > 0
        """;
}
