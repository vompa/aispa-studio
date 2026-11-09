using System.Security.Claims;
using AispaStudio.Api.Data;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Documents;

/// <summary>Vermerke gesendeter Dokumente (ADR 0091): schreibt, wohin und von wem ein PDF ging, und liest sie je Ziel für die Anzeige „Per Mail gesendet an …“.</summary>
internal sealed class SentDocuments(AispaDbContext db, DocumentHeaders headers, TimeProvider time)
{
    public async Task<SentDocumentDto> RecordAsync(string kind, Guid targetId, string to, string subject, ClaimsPrincipal user, CancellationToken ct)
    {
        var entry = new SentDocument
        {
            Kind = kind,
            TargetId = targetId,
            To = to.Trim(),
            Subject = subject,
            SentAt = time.GetUtcNow(),
            SentBy = await headers.FirstNameAsync(user, ct),
            UserId = user.FindFirstValue(ClaimTypes.NameIdentifier),
        };
        db.SentDocuments.Add(entry);
        await db.SaveChangesAsync(ct);
        return ToDto(entry);
    }

    /// <summary>Alle Vermerke zu den Zielen einer Art, älteste zuerst, gruppiert je Ziel.</summary>
    public static async Task<Dictionary<Guid, IReadOnlyList<SentDocumentDto>>> ListAsync(AispaDbContext db, string kind, IReadOnlyCollection<Guid> targetIds, CancellationToken ct) =>
        (await db.SentDocuments.AsNoTracking().Where(d => d.Kind == kind && targetIds.Contains(d.TargetId)).OrderBy(d => d.SentAt).ToListAsync(ct))
            .GroupBy(d => d.TargetId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<SentDocumentDto>)[.. g.Select(ToDto)]);

    public static SentDocumentDto ToDto(SentDocument entry) => new(entry.To, entry.SentAt, entry.SentBy);
}
