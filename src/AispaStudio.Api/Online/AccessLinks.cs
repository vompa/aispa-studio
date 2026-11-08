using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Online;

/// <summary>Erzeugt und prüft die Links aus Bestätigungsmails (Termin verwalten, Anamnesebogen) und für das Tablet am Empfang.</summary>
internal sealed class AccessLinks(OnlineDbContext db, TimeProvider time)
{
    /// <summary>Legt einen Link an und liefert den Code im Klartext; gespeichert wird nur der Hash.</summary>
    public string Create(
        AccessLinkPurpose purpose, Guid customerId, Guid? appointmentId, DateTimeOffset expiresAt, Guid? waitlistEntryId = null)
    {
        var token = NewToken();
        db.AccessLinks.Add(new AccessLink
        {
            TokenHash = Hash(token),
            Purpose = purpose,
            CustomerId = customerId,
            AppointmentId = appointmentId,
            WaitlistEntryId = waitlistEntryId,
            CreatedAt = time.GetUtcNow(),
            ExpiresAt = expiresAt,
        });
        return token;
    }

    public Task<AccessLink?> FindAsync(string token, AccessLinkPurpose purpose, CancellationToken ct) =>
        FindAsync(token, [purpose], ct);

    public async Task<AccessLink?> FindAsync(string token, IReadOnlyCollection<AccessLinkPurpose> purposes, CancellationToken ct)
    {
        if (token.Length is < 20 or > 100)
        {
            return null;
        }

        var hash = Hash(token);
        var now = time.GetUtcNow();
        return await db.AccessLinks.AsNoTracking()
            .FirstOrDefaultAsync(l => l.TokenHash == hash && purposes.Contains(l.Purpose) && l.ExpiresAt > now, ct);
    }

    /// <summary>Macht einen Link ungültig (z. B. den Tablet-Link nach dem Abgeben).</summary>
    public Task RevokeAsync(AccessLink link, CancellationToken ct) =>
        db.AccessLinks.Where(l => l.TokenHash == link.TokenHash).ExecuteDeleteAsync(ct);

    /// <summary>256 Bit zufällig, URL-tauglich.</summary>
    public static string NewToken() => Base64Url(RandomNumberGenerator.GetBytes(32));

    public static byte[] Hash(string value) => SHA256.HashData(Encoding.UTF8.GetBytes(value));

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
