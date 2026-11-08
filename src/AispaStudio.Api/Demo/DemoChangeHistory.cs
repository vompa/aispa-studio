using AispaStudio.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Demo;

/// <summary>
/// Änderungsverlauf der Beispieltermine (ADR 0082). Er entsteht am Ende aus den fertigen Terminen und nicht beim Einspielen: der Weg im Speicher fügt
/// jeden Termin nur einmal ein, der Weg über die Dienste ändert ihn mehrfach, und die Uhr des Einspielens wäre ohnehin nicht die Zeit der Geschichte.
/// So ist der Verlauf auf beiden Wegen gleich (<c>DemoBulkTests</c>). Angelegt zum <c>CreatedAt</c> über den Kanal der Quelle, an der Rezeption von
/// der ersten unbefristeten Rezeptionskraft des Standorts; jeder siebte Termin der Rezeption wurde einmal um eine Stunde verschoben; Einchecken,
/// Abschluss, Storno und Nicht erschienen stehen mit dem Stand des Termins da.
/// </summary>
internal static class DemoChangeHistory
{
    public static async Task AddAsync(IServiceProvider root, Guid tenantId, DateTimeOffset now, CancellationToken ct)
    {
        await using var scope = DemoSeeder.Open(root, tenantId);
        var db = scope.ServiceProvider.GetRequiredService<AispaDbContext>();
        await db.Database.ExecuteSqlAsync($"DELETE FROM sync.history WHERE tenant_id = {tenantId}", ct);
        await db.Database.ExecuteSqlAsync($"""
            WITH a AS (
                SELECT ap."Id", ap."TenantId", ap."CustomerId", ap."Source", ap."Status", ap."Start", ap."CreatedAt", ap."UpdatedAt", ap."CancelReason",
                       ap."CancelledByBusiness",
                       CASE ap."Source" WHEN 'Reception' THEN 'Team' WHEN 'Email' THEN 'Mail' ELSE ap."Source" END AS channel,
                       (SELECT sa."UserId" FROM aispa."StaffAccess" sa JOIN aispa."AspNetUsers" u ON u."Id" = sa."UserId"
                         WHERE sa."TenantId" = ap."TenantId" AND sa."Role" = 'Reception' AND sa."ExpiresAt" IS NULL
                           AND (sa."AllLocations" OR ap."LocationId" = ANY (sa."LocationIds"))
                         ORDER BY u."Email" LIMIT 1) AS desk,
                       greatest(ap."CreatedAt", ap."Start" - interval '5 minutes') AS checked_in
                  FROM aispa."Appointments" ap
                 WHERE ap."TenantId" = {tenantId}
            ), entries AS (
                SELECT a."Id", a."TenantId", a."CustomerId", 'I' AS operation, a."CreatedAt" AS changed_at,
                       CASE WHEN a."Source" = 'Reception' THEN a.desk END AS user_id, a.channel, NULL::jsonb AS changes
                  FROM a
                UNION ALL
                SELECT a."Id", a."TenantId", a."CustomerId", 'U', least(a."CreatedAt" + least(interval '1 day', (a."Start" - a."CreatedAt") / 2), {now}),
                       a.desk, 'Team', jsonb_build_object('Start', jsonb_build_array(to_jsonb(a."Start" - interval '1 hour'), to_jsonb(a."Start")))
                  FROM a
                 WHERE a."Source" = 'Reception' AND a."Start" - a."CreatedAt" > interval '2 hours'
                   AND (extract(epoch FROM a."Start")::bigint / 900) % 7 = 0
                UNION ALL
                SELECT a."Id", a."TenantId", a."CustomerId", 'U', a.checked_in, a.desk, 'Team',
                       jsonb_build_object('Status', jsonb_build_array('Booked', 'CheckedIn'))
                  FROM a
                 WHERE a."Status" IN ('CheckedIn', 'Completed')
                UNION ALL
                SELECT a."Id", a."TenantId", a."CustomerId", 'U', greatest(a."UpdatedAt", a.checked_in), a.desk, 'Team',
                       jsonb_build_object('Status', jsonb_build_array('CheckedIn', 'Completed'))
                  FROM a
                 WHERE a."Status" = 'Completed'
                UNION ALL
                SELECT a."Id", a."TenantId", a."CustomerId", 'U', a."UpdatedAt", a.desk, 'Team',
                       jsonb_build_object('Status', jsonb_build_array('Booked', 'NoShow'))
                  FROM a
                 WHERE a."Status" = 'NoShow'
                UNION ALL
                SELECT a."Id", a."TenantId", a."CustomerId", 'U', a."UpdatedAt",
                       CASE WHEN a."Source" = 'Reception' OR a."CancelledByBusiness" THEN a.desk END,
                       CASE WHEN a."CancelledByBusiness" THEN 'Team' ELSE a.channel END,
                       jsonb_build_object('Status', jsonb_build_array('Booked', 'Cancelled'))
                       || CASE WHEN a."CancelReason" IS NULL THEN jsonb_build_object()
                               ELSE jsonb_build_object('CancelReason', jsonb_build_array(NULL::text, a."CancelReason")) END
                       || CASE WHEN a."CancelledByBusiness" THEN jsonb_build_object('CancelledByBusiness', jsonb_build_array(false, true))
                               ELSE jsonb_build_object() END
                  FROM a
                 WHERE a."Status" = 'Cancelled'
            )
            INSERT INTO sync.history (table_schema, table_name, row_key, tenant_id, customer_id, operation, changed_at, user_id, channel, changes)
            SELECT 'aispa', 'Appointments', jsonb_build_object('Id', e."Id"), e."TenantId", e."CustomerId", e.operation, e.changed_at, e.user_id, e.channel,
                   e.changes
              FROM entries e
             ORDER BY e.changed_at, e.operation
            """, ct);
    }
}
