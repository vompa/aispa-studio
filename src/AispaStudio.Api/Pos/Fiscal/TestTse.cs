using System.Formats.Tar;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using AispaStudio.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Pos.Fiscal;

/// <summary>Zustand der Test-TSE je Kasse (Client-ID): Schlüssel und Zähler.</summary>
public sealed class TestTseClientState
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public required string ClientId { get; init; }
    public required string PrivateKey { get; init; }
    public required string PublicKey { get; init; }
    public long TransactionCounter { get; set; }
    public long SignatureCounter { get; set; }
}

/// <summary>Protokoll der Test-TSE: eine Zeile je abgeschlossener Transaktion, Grundlage des TSE-Exports.</summary>
public sealed class TestTseLog
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public required string ClientId { get; init; }
    public long TransactionNumber { get; init; }
    public long SignatureCounter { get; init; }
    public DateTimeOffset Start { get; init; }
    public DateTimeOffset End { get; init; }
    public required string ProcessType { get; init; }
    public required string ProcessData { get; init; }
    public required string Signature { get; init; }
}

/// <summary>
/// Test-TSE für Entwicklung und Prototyp (Schalter <c>Fiscal:TestTse</c>, nur in der Entwicklung an): ECDSA P-256 je Kasse, Transaktions- und
/// Signaturzähler in der Datenbank. Signiert wird über „Transaktionsnummer;Signaturzähler;Start;Ende;Vorgangsart;Vorgangsdaten“, nicht über
/// die ASN.1-Lognachricht einer zertifizierten TSE. Für den Echtbetrieb ersetzt eine Cloud- oder USB-TSE diese Klasse; Belege bleiben gleich aufgebaut.
/// </summary>
internal sealed class TestTseClient(AispaDbContext db, TimeProvider time) : ITseClient
{
    public const string Algorithm = "ecdsa-plain-SHA256";

    public bool IsTest => true;

    public async Task<string> RegisterClientAsync(string clientId, CancellationToken ct)
    {
        var state = await FindAsync(clientId, ct);
        if (state is null)
        {
            using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            state = new TestTseClientState
            {
                ClientId = clientId,
                PrivateKey = Convert.ToBase64String(key.ExportPkcs8PrivateKey()),
                PublicKey = Convert.ToBase64String(key.ExportSubjectPublicKeyInfo()),
            };
            db.Add(state);
        }

        return Serial(state.PublicKey);
    }

    public Task<bool> IsAvailableAsync(CancellationToken ct) => Task.FromResult(true);

    public async Task<TseSignature?> SignTransactionAsync(string clientId, string processType, string processData, CancellationToken ct)
    {
        var state = await FindAsync(clientId, ct);
        if (state is null)
        {
            return null;
        }

        // Start und Ende einer Transaktion: Anlegen und Abschluss zählen je eine Signatur.
        var transaction = ++state.TransactionCounter;
        state.SignatureCounter += 2;
        var now = time.GetUtcNow();
        using var key = ECDsa.Create();
        key.ImportPkcs8PrivateKey(Convert.FromBase64String(state.PrivateKey), out _);
        var signature = key.SignData(SignedData(transaction, state.SignatureCounter, now, now, processType, processData),
            HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        var encoded = Convert.ToBase64String(signature);
        db.Add(new TestTseLog
        {
            ClientId = clientId,
            TransactionNumber = transaction,
            SignatureCounter = state.SignatureCounter,
            Start = now,
            End = now,
            ProcessType = processType,
            ProcessData = processData,
            Signature = encoded,
        });
        return new TseSignature(transaction, state.SignatureCounter, now, now, Algorithm, encoded, state.PublicKey);
    }

    public async Task<byte[]?> GetPublicKeyAsync(string clientId, CancellationToken ct) =>
        await FindAsync(clientId, ct) is { } state ? Convert.FromBase64String(state.PublicKey) : null;

    /// <summary>
    /// TAR wie beim Export einer zertifizierten TSE: <c>info.csv</c>, öffentlicher Schlüssel als PEM und je Transaktion eine Protokolldatei
    /// mit dem Namensschema nach BSI TR-03151 (<c>Unixt_…_Sig-…_Log-Tra_No-…_Finish_Client-….log</c>). Anders als bei einer zertifizierten TSE
    /// stehen darin die signierten Daten als Text und die Signatur, nicht die ASN.1-Lognachricht; der Export ist nur zum Ausprobieren.
    /// </summary>
    public async Task<byte[]?> ExportAsync(string clientId, DateTimeOffset from, DateTimeOffset until, CancellationToken ct)
    {
        if (await FindAsync(clientId, ct) is not { } state)
        {
            return null;
        }

        var logs = await db.Set<TestTseLog>().AsNoTracking().Where(l => l.ClientId == clientId && l.End >= from && l.End < until)
            .OrderBy(l => l.TransactionNumber).ToListAsync(ct);
        var serial = Serial(state.PublicKey);
        var created = time.GetUtcNow();
        using var stream = new MemoryStream();
        await using (var tar = new TarWriter(stream, TarEntryFormat.Ustar, leaveOpen: true))
        {
            var info = new StringBuilder()
                .AppendLine("Beschreibung;Wert")
                .AppendLine("Hersteller;AISPA Studio Test-TSE (nicht zertifiziert)")
                .AppendLine(CultureInfo.InvariantCulture, $"Seriennummer;{serial}")
                .AppendLine(CultureInfo.InvariantCulture, $"Client-ID;{clientId}")
                .AppendLine(CultureInfo.InvariantCulture, $"Signaturalgorithmus;{Algorithm}")
                .AppendLine(CultureInfo.InvariantCulture, $"Zeitraum;{KassenSichVFiscalization.Time(from)} bis {KassenSichVFiscalization.Time(until)}")
                .AppendLine(CultureInfo.InvariantCulture, $"Transaktionen;{logs.Count}")
                .AppendLine("Hinweis;Nur für Entwicklung und Vorführung. Protokolle als Text statt ASN.1.");
            await AddAsync(tar, "info.csv", Encoding.UTF8.GetBytes(info.ToString()), created, ct);
            var pem = PemEncoding.Write("PUBLIC KEY", Convert.FromBase64String(state.PublicKey));
            await AddAsync(tar, $"{serial[..16]}_PublicKey.pem", Encoding.ASCII.GetBytes(pem), created, ct);
            foreach (var l in logs)
            {
                var name = string.Create(CultureInfo.InvariantCulture,
                    $"Unixt_{l.End.ToUnixTimeSeconds()}_Sig-{l.SignatureCounter}_Log-Tra_No-{l.TransactionNumber}_Finish_Client-{l.ClientId}.log");
                var content = Encoding.UTF8.GetString(SignedData(l.TransactionNumber, l.SignatureCounter, l.Start, l.End, l.ProcessType, l.ProcessData))
                    + "\n" + l.Signature + "\n";
                await AddAsync(tar, name, Encoding.UTF8.GetBytes(content), l.End, ct);
            }
        }

        return stream.ToArray();
    }

    private static async Task AddAsync(TarWriter tar, string name, byte[] content, DateTimeOffset modified, CancellationToken ct)
    {
        using var data = new MemoryStream(content);
        await tar.WriteEntryAsync(new UstarTarEntry(TarEntryType.RegularFile, name) { DataStream = data, ModificationTime = modified }, ct);
    }

    /// <summary>Was die Test-TSE signiert; Prüfsoftware und Tests bauen es aus dem QR-Code nach.</summary>
    public static byte[] SignedData(long transaction, long counter, DateTimeOffset start, DateTimeOffset end, string processType, string processData) =>
        Encoding.UTF8.GetBytes(string.Join(';', transaction.ToString(CultureInfo.InvariantCulture), counter.ToString(CultureInfo.InvariantCulture),
            KassenSichVFiscalization.Time(start), KassenSichVFiscalization.Time(end), processType, processData));

    /// <summary>Seriennummer einer TSE: SHA-256 des öffentlichen Schlüssels, hexadezimal.</summary>
    public static string Serial(string publicKey) => Convert.ToHexString(SHA256.HashData(Convert.FromBase64String(publicKey))).ToLowerInvariant();

    private async Task<TestTseClientState?> FindAsync(string clientId, CancellationToken ct) =>
        db.ChangeTracker.Entries<TestTseClientState>().Select(e => e.Entity).FirstOrDefault(s => s.ClientId == clientId)
        ?? await db.Set<TestTseClientState>().FirstOrDefaultAsync(s => s.ClientId == clientId, ct);
}
