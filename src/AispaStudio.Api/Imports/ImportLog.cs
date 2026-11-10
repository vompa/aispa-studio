using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Importing;

/// <summary>
/// Eine Zeile je Import (Export/Import, Aufgabe E): Art, Datei und Zähler. Wer und wann steht im Verlauf (<c>sync.history</c>), der daraus den Satz
/// „Import Kunden: 240 neu, 12 ergänzt, 3 übersprungen (kunden.xlsx, 255 Zeilen) von Lena“ bildet. Probeläufe schreiben keine Zeile.
/// </summary>
public sealed class ImportEntry
{
    public Guid Id { get; init; } = Guid.CreateVersion7();

    /// <summary>Was importiert wurde, wie es im Verlauf heißt: „Kunden“.</summary>
    public required string Kind { get; init; }

    public required string FileName { get; init; }

    public int RowCount { get; init; }

    public int Created { get; init; }

    public int Merged { get; init; }

    public int Skipped { get; init; }

    public int Failed { get; init; }

    public DateTimeOffset CreatedAt { get; init; }
}

internal static class ImportModel
{
    public static void Configure(ModelBuilder builder) => builder.Entity<ImportEntry>(e =>
    {
        e.ToTable("Imports");
        e.Property(x => x.Id).ValueGeneratedNever();
        e.Property(x => x.Kind).HasMaxLength(60);
        e.Property(x => x.FileName).HasMaxLength(200);
        e.HasIndex(x => x.CreatedAt);
    });
}
