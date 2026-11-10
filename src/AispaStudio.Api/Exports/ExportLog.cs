using AispaStudio.Api.Access;
using AispaStudio.Api.Data;
using AispaStudio.Api.Sync;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Exports;

/// <summary>Eine Zeile je Export (Export/Import, Aufgabe A): Art, Format, Zeilenzahl und Filter. Wer und wann steht im Verlauf (<c>sync.history</c>), der die Tabelle begleitet.</summary>
public sealed class ExportEntry
{
    public Guid Id { get; init; } = Guid.CreateVersion7();

    /// <summary>Was exportiert wurde, wie es im Verlauf heißt: „Kundenliste“, „Newsletter-Liste“, „Terminliste“ …</summary>
    public required string Kind { get; init; }

    public ExportFormat Format { get; init; }

    public int RowCount { get; init; }

    /// <summary>Der Filter der Liste in Worten („Suche „Huber““, „1.–31.3.2027“), damit die Leitung sieht, welcher Ausschnitt die Datei verließ.</summary>
    public string? Filter { get; init; }

    public DateTimeOffset CreatedAt { get; init; }
}

internal static class ExportModel
{
    public static void Configure(ModelBuilder builder) => builder.Entity<ExportEntry>(e =>
    {
        e.ToTable("Exports");
        e.Property(x => x.Id).ValueGeneratedNever();
        e.Property(x => x.Kind).HasMaxLength(60);
        e.Property(x => x.Format).HasConversion<string>().HasMaxLength(16);
        e.Property(x => x.Filter).HasMaxLength(300);
        e.HasIndex(x => x.CreatedAt);
    });
}

/// <summary>Protokolliert einen Export; der Verlaufs-Trigger schreibt daraus den Satz „Export Kundenliste (Excel, 312 Zeilen) von Lena“.</summary>
public sealed class ExportLog(AispaDbContext db, TimeProvider time)
{
    public async Task RecordAsync(string kind, ExportFormat format, int rowCount, string? filter, CancellationToken ct)
    {
        db.Exports.Add(new ExportEntry { Kind = kind, Format = format, RowCount = rowCount, Filter = filter, CreatedAt = time.GetUtcNow() });
        await db.SaveChangesAsync(ct);
    }
}

/// <summary>Das Export-Protokoll für die Leitung; die Verlauf-Seite zeigt dieselben Zeilen zwischen den anderen Einträgen.</summary>
internal static class ExportEndpoints
{
    private const string LeadershipOnly = "Das Export-Protokoll sehen nur Inhaber und Manager.";

    public static IEndpointRouteBuilder MapExportEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/api/exports/history", async Task<IResult> (AccessService access, EntityHistory history, AispaDbContext db, CancellationToken ct) =>
                await access.CurrentAsync(ct) is { IsOwnerLevel: true }
                    ? TypedResults.Ok(await history.ForExportsAsync(db.CurrentTenantId, ct))
                    : PermissionExtensions.Forbidden(LeadershipOnly))
            .WithTags("Verlauf")
            .WithName("GetExportHistory")
            .WithSummary("Listet, wer wann welche Liste in welchem Format exportiert hat (nur Inhaber und Manager), neueste oben.")
            .Produces<List<HistoryEntry>>().ProducesProblem(StatusCodes.Status403Forbidden)
            .RequirePermission(AccessRight.ViewCalendar);
        return routes;
    }
}
