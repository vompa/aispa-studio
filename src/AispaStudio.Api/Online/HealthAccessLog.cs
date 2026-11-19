using System.Security.Claims;
using AispaStudio.Api.Data;
using AispaStudio.Api.Scheduling;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Online;

/// <summary>
/// Ein Abruf von Gesundheitsdaten (Anamnese): wer, wann, welcher Gast, bei welchem Termin und wozu. Der Eintrag enthält selbst keine
/// Gesundheitsdaten und liegt deshalb im normalen Schema, nicht verschlüsselt im Schema health (ADR 0013, Nachtrag 19.11.2026).
/// </summary>
public sealed class HealthDataAccess
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid CustomerId { get; init; }
    public Guid? AppointmentId { get; init; }
    public required string UserId { get; init; }
    public HealthAccessPurpose Purpose { get; init; }
    public DateTimeOffset At { get; init; }
}

/// <summary>Schreibt und liest das Abrufprotokoll der Gesundheitsdaten.</summary>
internal sealed class HealthAccessLog(AispaDbContext db, TimeProvider time)
{
    /// <summary>Wie viele Einträge die Leitung je Gast auf einmal sieht (die neuesten zuerst).</summary>
    public const int ListLimit = 100;

    /// <summary>Hält einen Abruf fest; ohne angemeldete Person (Hintergrunddienst) nichts, denn dann hat niemand gelesen.</summary>
    public async Task WriteAsync(ClaimsPrincipal principal, Guid customerId, Guid? appointmentId, HealthAccessPurpose purpose, CancellationToken ct)
    {
        if (principal.FindFirstValue(ClaimTypes.NameIdentifier) is not { Length: > 0 } userId)
        {
            return;
        }

        db.HealthDataAccesses.Add(new HealthDataAccess
        {
            CustomerId = customerId,
            AppointmentId = appointmentId,
            UserId = userId,
            Purpose = purpose,
            At = time.GetUtcNow(),
        });
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Die letzten Abrufe zu einem Gast mit Name der Person und Behandlung des Termins.</summary>
    public async Task<List<HealthAccessEntry>> ListAsync(Guid customerId, CancellationToken ct)
    {
        var rows = await db.HealthDataAccesses.AsNoTracking().Where(a => a.CustomerId == customerId)
            .OrderByDescending(a => a.At).Take(ListLimit)
            .GroupJoin(db.Users.AsNoTracking(), a => a.UserId, u => u.Id, (a, users) => new { a, users })
            .SelectMany(x => x.users.DefaultIfEmpty(), (x, u) => new { x.a, Name = u == null ? null : u.DisplayName })
            .GroupJoin(db.Appointments.AsNoTracking(), x => x.a.AppointmentId, ap => ap.Id, (x, aps) => new { x.a, x.Name, aps })
            .SelectMany(x => x.aps.DefaultIfEmpty(), (x, ap) => new
            {
                x.a.At,
                x.a.Purpose,
                x.Name,
                Treatment = ap == null ? null : ap.Treatment.Name,
                Start = ap == null ? (DateTimeOffset?)null : ap.Start,
            })
            .ToListAsync(ct);
        return [.. rows.OrderByDescending(r => r.At)
            .Select(r => new HealthAccessEntry(r.At, string.IsNullOrWhiteSpace(r.Name) ? "Gelöschtes Konto" : r.Name, r.Purpose, r.Treatment, r.Start))];
    }
}

internal static class HealthAccessModel
{
    public static void Configure(ModelBuilder builder) =>
        builder.Entity<HealthDataAccess>(e =>
        {
            e.HasOne<Customer>().WithMany().HasForeignKey(a => a.CustomerId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(a => new { a.CustomerId, a.At });
            e.Property(a => a.UserId).HasMaxLength(450);
            e.Property(a => a.Purpose).HasConversion<string>().HasMaxLength(20);
        });
}
