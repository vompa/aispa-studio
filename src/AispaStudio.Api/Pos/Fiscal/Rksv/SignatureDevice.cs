using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using AispaStudio.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Pos.Fiscal.Rksv;

/// <summary>
/// Signatureinrichtung nach RKSV: Signaturkarte, HSM oder Cloud-Dienst eines Vertrauensdiensteanbieters (z. B. A-Trust, PrimeSign, fiskaly).
/// Signiert ES256 (ECDSA P-256 mit SHA-256), Signatur als R‖S (64 Bytes).
/// </summary>
internal interface IRksvSignatureDevice
{
    /// <summary>Kennung des Vertrauensdiensteanbieters im Belegcode: AT1 A-Trust, AT2 Global Trust, AT3 PrimeSign; AT0 ohne Anbieter/Test.</summary>
    string ZdaId { get; }

    /// <summary>Richtet die Einrichtung für eine Kasse ein und liefert die Seriennummer des Zertifikats.</summary>
    Task<string> SetUpAsync(CashRegister register, CancellationToken ct);

    Task<bool> IsAvailableAsync(CashRegister register, CancellationToken ct);

    /// <summary>Signatur oder <c>null</c>, wenn die Einrichtung ausgefallen ist.</summary>
    Task<byte[]?> SignAsync(CashRegister register, byte[] data, CancellationToken ct);

    Task<byte[]?> GetCertificateAsync(CashRegister register, CancellationToken ct);
}

/// <summary>Schalter für Tests und Vorführungen: lässt die Test-Signatureinrichtung einzelner Kassen ausfallen.</summary>
internal sealed class TestSignatureDeviceSwitch
{
    private readonly ConcurrentDictionary<Guid, bool> _failing = new();

    public void SetFailing(Guid registerId, bool failing) => _failing[registerId] = failing;

    public bool IsFailing(Guid registerId) => _failing.GetValueOrDefault(registerId);
}

/// <summary>
/// Test-Signatureinrichtung für Entwicklung und Prototyp: ECDSA-Schlüssel und selbst ausgestelltes Zertifikat je Kasse in der Datenbank.
/// Für den Echtbetrieb ersetzt eine zertifizierte Einrichtung diese Klasse; Belege bleiben gleich aufgebaut.
/// </summary>
internal sealed class TestSignatureDevice(AispaDbContext db, TestSignatureDeviceSwitch failure) : IRksvSignatureDevice
{
    public string ZdaId => "AT0";

    public Task<string> SetUpAsync(CashRegister register, CancellationToken ct)
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var request = new CertificateRequest($"CN=AISPA Test-Signatureinrichtung {register.Code}", key, HashAlgorithmName.SHA256);
        var serial = RandomNumberGenerator.GetBytes(8);
        serial[0] &= 0x7f;
        using var certificate = request.Create(
            request.SubjectName,
            X509SignatureGenerator.CreateForECDsa(key),
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddYears(5),
            serial);

        db.Add(new TestSignatureKey
        {
            RegisterId = register.Id,
            PrivateKey = Convert.ToBase64String(key.ExportPkcs8PrivateKey()),
            Certificate = Convert.ToBase64String(certificate.RawData),
        });
        return Task.FromResult(Convert.ToHexString(serial));
    }

    public Task<bool> IsAvailableAsync(CashRegister register, CancellationToken ct) => Task.FromResult(!failure.IsFailing(register.Id));

    public async Task<byte[]?> SignAsync(CashRegister register, byte[] data, CancellationToken ct)
    {
        if (failure.IsFailing(register.Id))
        {
            return null;
        }

        var stored = await FindAsync(register, ct);
        using var key = ECDsa.Create();
        key.ImportPkcs8PrivateKey(Convert.FromBase64String(stored.PrivateKey), out _);
        return key.SignData(data, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
    }

    public async Task<byte[]?> GetCertificateAsync(CashRegister register, CancellationToken ct) =>
        Convert.FromBase64String((await FindAsync(register, ct)).Certificate);

    private async Task<TestSignatureKey> FindAsync(CashRegister register, CancellationToken ct) =>
        db.ChangeTracker.Entries<TestSignatureKey>().Select(e => e.Entity).FirstOrDefault(k => k.RegisterId == register.Id)
        ?? await db.Set<TestSignatureKey>().AsNoTracking().FirstAsync(k => k.RegisterId == register.Id, ct);
}
