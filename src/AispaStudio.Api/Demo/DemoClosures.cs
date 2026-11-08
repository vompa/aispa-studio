using System.Globalization;
using AispaStudio.Api.Closures;
using AispaStudio.Api.Data;
using AispaStudio.Api.Scheduling;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Demo;

/// <summary>
/// Zwei Schließzeiten für das Beispiel-Spa (ADR 0073): ein Betriebsurlaub in Salzburg in den kommenden Wochen (mit Terminen darin, die in der Meldung „Termine in der
/// Schließzeit“ stehen) und ein beendeter Ausfall in Wien (Wasserschaden). Dazu eine Raumsperre (ADR 0076): der Raum Lotus in Wien ist in den nächsten Tagen wegen
/// Wartung gesperrt (mit Terminen darin, die in der Meldung „Termine im gesperrten Raum“ stehen). Sie entstehen nach dem Verlauf, damit der Verlauf in beiden Wegen
/// (Dienste und im Speicher) unverändert bleibt; die Tage rechnen sich vom Einspielzeitpunkt aus.
/// </summary>
internal static class DemoClosures
{
    public const string VacationReason = "Betriebsurlaub, Jahresinventur und Malerarbeiten";
    public const string OutageReason = "Wasserschaden im Behandlungsbereich, Trocknung";
    public const string RoomName = "Raum Lotus";
    public const string RoomReason = "Wartung der Lüftung, neue Beleuchtung";

    public static async Task AddAsync(IServiceProvider root, Guid tenantId, DateTimeOffset now, CancellationToken ct)
    {
        await using var scope = DemoSeeder.Open(root, tenantId);
        var db = scope.ServiceProvider.GetRequiredService<AispaDbContext>();
        var locations = await db.Locations.AsNoTracking().ToListAsync(ct);
        var salzburg = locations.First(l => l.Name.StartsWith("Salzburg", StringComparison.Ordinal));
        var vienna = locations.First(l => l.Name.StartsWith("Wien", StringComparison.Ordinal));
        var today = LocalTime.LocalDate(now, LocalTime.Zone(salzburg.TimeZone));

        var vacationFrom = today.AddDays(16);
        var vacationUntil = vacationFrom.AddDays(4);
        var back = vacationUntil.AddDays(1).ToString("d.M.", CultureInfo.InvariantCulture);
        db.Closures.Add(await NewAsync(db, "leitung.salzburg", "Birgit Rainer", salzburg.Id, vacationFrom, vacationUntil, ClosureKind.Vacation, VacationReason,
            $"Wegen Betriebsurlaub geschlossen. Ab {back} sind wir wieder für Sie da.", now.AddDays(-20), ct));

        var outageFrom = today.AddDays(-12);
        db.Closures.Add(await NewAsync(db, "leitung.wien", "Markus Steiner", vienna.Id, outageFrom, outageFrom.AddDays(1), ClosureKind.Outage, OutageReason,
            "Wegen eines Wasserschadens vorübergehend geschlossen.", now.AddDays(-13), ct));

        var lotus = await db.Rooms.AsNoTracking().FirstAsync(r => r.LocationId == vienna.Id && r.Name == RoomName, ct);
        var roomFrom = LocalTime.LocalDate(now, LocalTime.Zone(vienna.TimeZone)).AddDays(2);
        db.RoomClosures.Add(new RoomClosure
        {
            RoomId = lotus.Id,
            FromDate = roomFrom,
            UntilDate = roomFrom.AddDays(2),
            Kind = RoomClosureKind.Maintenance,
            Reason = RoomReason,
            CreatedByUserId = await db.Users.AsNoTracking().Where(u => u.Email == DemoAccounts.Email("leitung.wien")).Select(u => u.Id).FirstAsync(ct),
            CreatedByName = "Markus Steiner",
            CreatedAt = now.AddDays(-1),
        });
        await db.SaveChangesAsync(ct);
    }

    private static async Task<Closure> NewAsync(AispaDbContext db, string mailbox, string name, Guid locationId, DateOnly from, DateOnly until, ClosureKind kind,
        string reason, string guestText, DateTimeOffset created, CancellationToken ct) =>
        new()
        {
            LocationId = locationId,
            FromDate = from,
            UntilDate = until,
            Kind = kind,
            InternalReason = reason,
            GuestText = guestText,
            CreatedByUserId = await db.Users.AsNoTracking().Where(u => u.Email == DemoAccounts.Email(mailbox)).Select(u => u.Id).FirstAsync(ct),
            CreatedByName = name,
            CreatedAt = created,
        };
}
