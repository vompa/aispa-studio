using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using AispaStudio.Api.Access;
using AispaStudio.Api.Data;
using AispaStudio.Api.Scheduling;
using AispaStudio.Api.Tenancy;
using AispaStudio.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.MailIntake;

/// <summary>Externe Buchungsplattformen: Zuordnungen von Namen und ICS-Feeds mit der Belegung.</summary>
internal static class ChannelEndpoints
{
    /// <summary>Zeitraum eines ICS-Feeds: eine Woche zurück, ein halbes Jahr voraus.</summary>
    private static readonly TimeSpan FeedPast = TimeSpan.FromDays(7);

    private static readonly TimeSpan FeedFuture = TimeSpan.FromDays(183);

    public static IEndpointRouteBuilder MapChannelEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api").WithTags("Buchungsplattformen");

        group.MapGet("/channels/{channel}/mappings", async (BookingChannel channel, AispaDbContext db, CancellationToken ct) =>
                await db.ChannelMappings.AsNoTracking().Where(m => m.Channel == channel)
                    .OrderBy(m => m.Kind).ThenBy(m => m.ExternalName)
                    .Select(m => new ChannelMappingDto(m.Id, m.Channel, m.Kind, m.ExternalName, m.TargetId))
                    .ToListAsync(ct))
            .WithName("ListChannelMappings")
            .RequirePermission(AccessRight.ManageCatalog)
            .WithSummary("Zuordnungen von Leistungs-, Personen- und Standortnamen einer Plattform zu den eigenen Stammdaten.");

        group.MapPost("/channels/{channel}/mappings", async Task<Results<Created<ChannelMappingDto>, ValidationProblem>> (
                BookingChannel channel, ChannelMappingInput input, AispaDbContext db, CancellationToken ct) =>
            {
                var errors = new Errors();
                errors.Require(nameof(input.ExternalName), input.ExternalName);
                var exists = input.Kind switch
                {
                    ChannelMappingKind.Location => await db.Locations.AnyAsync(l => l.Id == input.TargetId, ct),
                    ChannelMappingKind.Treatment => await db.Treatments.AnyAsync(t => t.Id == input.TargetId, ct),
                    _ => await db.Employees.AnyAsync(e => e.Id == input.TargetId, ct),
                };
                errors.Check(exists, nameof(input.TargetId), "Unbekanntes Ziel.");
                if (errors.Any)
                {
                    return errors.Problem();
                }

                var normalized = MailIntakeProcessor.Normalize(input.ExternalName);
                var existing = await db.ChannelMappings.FirstOrDefaultAsync(
                    m => m.Channel == channel && m.Kind == input.Kind && m.NormalizedName == normalized, ct);
                if (existing is not null)
                {
                    db.ChannelMappings.Remove(existing);
                }

                var mapping = new ChannelMapping
                {
                    Channel = channel,
                    Kind = input.Kind,
                    ExternalName = input.ExternalName.Trim(),
                    NormalizedName = normalized,
                    TargetId = input.TargetId,
                };
                db.ChannelMappings.Add(mapping);
                await db.SaveChangesAsync(ct);
                return TypedResults.Created($"/api/channels/{channel}/mappings/{mapping.Id}",
                    new ChannelMappingDto(mapping.Id, mapping.Channel, mapping.Kind, mapping.ExternalName, mapping.TargetId));
            })
            .WithName("SetChannelMapping")
            .RequirePermission(AccessRight.ManageCatalog)
            .WithSummary("Legt eine Zuordnung an; eine bestehende für denselben Namen wird ersetzt.");

        group.MapDelete("/channels/{channel}/mappings/{mappingId:guid}", async Task<Results<NoContent, NotFound>> (
                BookingChannel channel, Guid mappingId, AispaDbContext db, CancellationToken ct) =>
                await db.ChannelMappings.Where(m => m.Id == mappingId && m.Channel == channel).ExecuteDeleteAsync(ct) > 0
                    ? TypedResults.NoContent()
                    : TypedResults.NotFound())
            .WithName("DeleteChannelMapping")
            .RequirePermission(AccessRight.ManageCatalog);

        group.MapGet("/locations/{locationId:guid}/calendar-feeds", async (Guid locationId, AispaDbContext db, CancellationToken ct) =>
                (await db.CalendarFeeds.AsNoTracking().Where(f => f.LocationId == locationId).OrderBy(f => f.Name).ToListAsync(ct))
                .Select(ToDto).ToList())
            .WithName("ListCalendarFeeds")
            .RequirePermission(AccessRight.ManageLocation);

        group.MapPost("/locations/{locationId:guid}/calendar-feeds", async Task<Results<Created<CalendarFeedDto>, NotFound, ValidationProblem>> (
                Guid locationId, CalendarFeedInput input, AispaDbContext db, TimeProvider time, CancellationToken ct) =>
            {
                if (!await db.Locations.AnyAsync(l => l.Id == locationId, ct))
                {
                    return TypedResults.NotFound();
                }

                var errors = new Errors();
                errors.Require(nameof(input.Name), input.Name);
                errors.Check(input.EmployeeId is null || await db.Employees.AnyAsync(e => e.Id == input.EmployeeId, ct),
                    nameof(input.EmployeeId), "Unbekannte Person.");
                if (errors.Any)
                {
                    return errors.Problem();
                }

                var feed = new CalendarFeed
                {
                    LocationId = locationId,
                    EmployeeId = input.EmployeeId,
                    Name = input.Name.Trim(),
                    Channel = input.Channel,
                    Token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_'),
                    CreatedAt = time.GetUtcNow(),
                };
                db.CalendarFeeds.Add(feed);
                await db.SaveChangesAsync(ct);
                return TypedResults.Created($"/api/calendar-feeds/{feed.Id}", ToDto(feed));
            })
            .WithName("CreateCalendarFeed")
            .RequirePermission(AccessRight.ManageLocation)
            .WithSummary("Neuer ICS-Feed; die Adresse (Path) bei der Plattform als Kalender eintragen, z. B. je Person bei Treatwell.");

        group.MapDelete("/calendar-feeds/{feedId:guid}", async Task<Results<NoContent, NotFound>> (
                Guid feedId, AispaDbContext db, CancellationToken ct) =>
                await db.CalendarFeeds.Where(f => f.Id == feedId).ExecuteDeleteAsync(ct) > 0
                    ? TypedResults.NoContent()
                    : TypedResults.NotFound())
            .WithName("DeleteCalendarFeed")
            .RequirePermission(AccessRight.ManageLocation)
            .WithSummary("Feed löschen; die Adresse funktioniert danach nicht mehr.");

        // Ohne Anmeldung: Plattformen rufen den Feed selbst ab. Der zufällige Schlüssel in der Adresse schützt ihn.
        routes.MapGet("/ics/{token}.ics", async Task<Results<ContentHttpResult, NotFound>> (
                string token, HttpContext http, AispaDbContext db, TenantContext tenants, TimeProvider time, CancellationToken ct) =>
            {
                // Ohne Anmeldung abgerufen (von der Plattform): der Mandant ergibt sich aus dem Feed.
                if (await TenantModel.TenantOfAsync(db.CalendarFeeds.Where(f => f.Token == token), ct) is not { } tenantId)
                {
                    return TypedResults.NotFound();
                }

                tenants.Use(tenantId);
                var feed = await db.CalendarFeeds.AsNoTracking().FirstAsync(f => f.Token == token, ct);

                var now = time.GetUtcNow();
                http.Response.Headers.CacheControl = "private, max-age=60";
                return TypedResults.Text(await BuildFeedAsync(feed, db, now, ct), "text/calendar; charset=utf-8");
            })
            .WithTags("Buchungsplattformen")
            .WithName("GetCalendarFeed")
            .WithSummary("Belegung als iCalendar (nur „Belegt“, ohne Namen von Gästen). Kurze Cache-Zeit, damit Plattformen Änderungen schnell sehen.");

        return routes;
    }

    /// <summary>
    /// Feed einer Person: alles, was sie belegt (Termine mit Vor-/Nachbereitung, auch an anderen Standorten, und Sperrzeiten),
    /// so wie Treatwell es je Mitarbeiter:in braucht. Feed eines Standorts: die Termine dort, als Übersicht der Auslastung.
    /// </summary>
    private static async Task<string> BuildFeedAsync(CalendarFeed feed, AispaDbContext db, DateTimeOffset now, CancellationToken ct)
    {
        var from = now - FeedPast;
        var until = now + FeedFuture;
        var excluded = db.ChannelBookings.Where(b => b.Channel == feed.Channel && b.AppointmentId != null).Select(b => b.AppointmentId);

        List<(Guid Id, DateTimeOffset From, DateTimeOffset Until)> busy;
        if (feed.EmployeeId is { } employeeId)
        {
            busy = (await db.ResourceBookings.AsNoTracking()
                    .Where(b => b.ResourceId == employeeId && b.From < until && from < b.Until)
                    .Where(b => b.AppointmentId == null || !excluded.Contains(b.AppointmentId))
                    .OrderBy(b => b.From)
                    .Select(b => new { b.Id, b.From, b.Until })
                    .ToListAsync(ct))
                .Select(b => (b.Id, b.From, b.Until)).ToList();
        }
        else
        {
            busy = (await db.Appointments.AsNoTracking()
                    .Where(a => a.LocationId == feed.LocationId && a.Status != AppointmentStatus.Cancelled)
                    .Where(a => a.BlockedFrom < until && from < a.BlockedUntil)
                    .Where(a => !excluded.Contains(a.Id))
                    .OrderBy(a => a.BlockedFrom)
                    .Select(a => new { a.Id, a.BlockedFrom, a.BlockedUntil })
                    .ToListAsync(ct))
                .Select(a => (a.Id, a.BlockedFrom, a.BlockedUntil)).ToList();
        }

        var ics = new StringBuilder();
        void Line(string text) => ics.Append(text).Append("\r\n");
        Line("BEGIN:VCALENDAR");
        Line("VERSION:2.0");
        Line("PRODID:-//AISPA Studio//Belegung//DE");
        Line("CALSCALE:GREGORIAN");
        Line("METHOD:PUBLISH");
        Line($"X-WR-CALNAME:{Escape(feed.Name)}");
        Line("REFRESH-INTERVAL;VALUE=DURATION:PT5M");
        foreach (var (id, start, end) in busy)
        {
            Line("BEGIN:VEVENT");
            Line($"UID:{id:N}@aispa.studio");
            Line($"DTSTAMP:{Stamp(now)}");
            Line($"DTSTART:{Stamp(start)}");
            Line($"DTEND:{Stamp(end)}");
            Line("SUMMARY:Belegt");
            Line("TRANSP:OPAQUE");
            Line("CLASS:PRIVATE");
            Line("END:VEVENT");
        }

        Line("END:VCALENDAR");
        return ics.ToString();
    }

    private static string Stamp(DateTimeOffset t) => t.UtcDateTime.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);

    private static string Escape(string text) =>
        text.Replace("\\", "\\\\", StringComparison.Ordinal).Replace(";", "\\;", StringComparison.Ordinal)
            .Replace(",", "\\,", StringComparison.Ordinal).Replace("\n", "\\n", StringComparison.Ordinal);

    private static CalendarFeedDto ToDto(CalendarFeed f) =>
        new(f.Id, f.LocationId, f.EmployeeId, f.Name, f.Channel, $"/ics/{f.Token}.ics", f.CreatedAt);
}
