using AispaStudio.Api.Data;
using AispaStudio.Api.Pos;
using AispaStudio.Api.Scheduling;
using AispaStudio.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AispaStudio.Api.Stock;

/// <summary>Barcode am Material (Slice 114): Prüfung, Schreibweise und Suche per Code für Lager und Kassa.</summary>
internal static class StockBarcodes
{
    public const int MinLength = 3;
    public const int MaxLength = 50;

    private const string IndexName = "IX_StockItems_TenantId_Barcode";

    /// <summary>Getrimmt; leer heißt „kein Code“.</summary>
    public static string? Normalize(string? code) => string.IsNullOrWhiteSpace(code) ? null : code.Trim();

    /// <summary>Format und Eindeutigkeit; <paramref name="id"/> ist das Material, das gerade gespeichert wird.</summary>
    public static async Task CheckAsync(Errors errors, string? code, Guid? id, AispaDbContext db, CancellationToken ct)
    {
        if (Normalize(code) is not { } barcode)
        {
            return;
        }

        var valid = barcode.Length is >= MinLength and <= MaxLength && !barcode.Any(char.IsWhiteSpace);
        errors.Check(valid, nameof(StockItemInput.Barcode), $"Code mit {MinLength} bis {MaxLength} Zeichen ohne Leerzeichen.");
        if (valid && await db.StockItems.AsNoTracking().Where(i => i.Barcode == barcode && i.Id != id).Select(i => i.Name).FirstOrDefaultAsync(ct) is { } owner)
        {
            errors.Check(false, nameof(StockItemInput.Barcode), $"Der Code gehört schon zum Material „{owner}“.");
        }
    }

    /// <summary>
    /// Speichert; verliert die Speicherung gegen eine gleichzeitige mit demselben Code (die Vorprüfung sah ihn noch frei), sichert der Index das ab und
    /// die Antwort ist wie bei der Vorprüfung ein Fehler am Feld statt eines Serverfehlers.
    /// </summary>
    public static async Task<ValidationProblem?> SaveAsync(AispaDbContext db, CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
            return null;
        }
        catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: IndexName })
        {
            var errors = new Errors();
            errors.Check(false, nameof(StockItemInput.Barcode), "Der Code gehört schon zu einem anderen Material.");
            return errors.Problem();
        }
    }

    /// <summary>Das aktive Material mit diesem Code, oder <c>null</c>.</summary>
    public static async Task<StockItem?> FindAsync(AispaDbContext db, string? code, CancellationToken ct) =>
        Normalize(code) is { } barcode
            ? await db.StockItems.AsNoTracking().Include(i => i.Supplier).FirstOrDefaultAsync(i => i.IsActive && i.Barcode == barcode, ct)
            : null;

    /// <summary>Der aktive Kassa-Artikel des aktiven Materials mit diesem Code, oder <c>null</c>.</summary>
    public static async Task<Article?> FindArticleAsync(AispaDbContext db, string? code, CancellationToken ct) =>
        await FindAsync(db, code, ct) is { ArticleId: { } articleId }
            ? await db.Articles.AsNoTracking().FirstOrDefaultAsync(a => a.Id == articleId && a.IsActive, ct)
            : null;
}
