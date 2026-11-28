using System.Security.Cryptography;
using System.Text.Json;
using AispaStudio.Api.Data;
using AispaStudio.Contracts;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Pos.Fiscal;

internal sealed class FiscalCredential
{
    public Guid RegisterId { get; init; }
    public required string ProtectedValues { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

internal sealed class FiscalCredentialStore(AispaDbContext db, IDataProtectionProvider protection, TimeProvider time)
{
    public async Task<IReadOnlyDictionary<string, string>> ReadAsync(Guid registerId, CancellationToken ct)
    {
        var stored = await db.Set<FiscalCredential>().AsNoTracking().SingleOrDefaultAsync(c => c.RegisterId == registerId, ct);
        return Unprotect(stored ?? throw PosFailure.Conflict("Für diese Kassa sind keine Zugangsdaten gespeichert."));
    }

    public async Task<FiscalCredentialsDto> StatusAsync(Guid registerId, CancellationToken ct)
    {
        var stored = await db.Set<FiscalCredential>().AsNoTracking().SingleOrDefaultAsync(c => c.RegisterId == registerId, ct);
        return stored is null ? new([], null) : new([.. Unprotect(stored).Keys.Order(StringComparer.Ordinal)], stored.UpdatedAt);
    }

    public async Task<FiscalCredentialsDto> StageAsync(Guid registerId, FiscalProviderDto provider,
        IReadOnlyDictionary<string, string>? values, CancellationToken ct)
    {
        if ((values?.Count ?? 0) != provider.Fields.Count || provider.Fields.Any(field =>
            values is null || !values.TryGetValue(field.Key, out var value) || string.IsNullOrWhiteSpace(value)))
        {
            throw PosFailure.Invalid("fiscalCredentials", "Bitte alle Zugangsfelder des gewählten Anbieters ausfüllen.");
        }

        if (provider.Fields.Count == 0)
        {
            return new([], null);
        }

        var ciphertext = Protector(registerId).Protect(JsonSerializer.Serialize(values));
        var now = time.GetUtcNow();
        var stored = await db.Set<FiscalCredential>().SingleOrDefaultAsync(c => c.RegisterId == registerId, ct);
        if (stored is null)
        {
            db.Add(new FiscalCredential { RegisterId = registerId, ProtectedValues = ciphertext, UpdatedAt = now });
        }
        else
        {
            stored.ProtectedValues = ciphertext;
            stored.UpdatedAt = now;
        }

        return new([.. provider.Fields.Select(f => f.Key).Order(StringComparer.Ordinal)], now);
    }

    private Dictionary<string, string> Unprotect(FiscalCredential stored)
    {
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(Protector(stored.RegisterId).Unprotect(stored.ProtectedValues))
                ?? throw new JsonException();
        }
        catch (Exception ex) when (ex is CryptographicException or JsonException)
        {
            throw PosFailure.Conflict("Die gespeicherten Zugangsdaten können an dieser Stelle nicht gelesen werden.");
        }
    }

    private IDataProtector Protector(Guid registerId) =>
        protection.CreateProtector("Pos.Fiscal.Credentials", db.CurrentTenantId.ToString(), registerId.ToString());
}
