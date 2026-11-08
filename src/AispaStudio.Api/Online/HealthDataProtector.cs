using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace AispaStudio.Api.Online;

/// <summary>Schlüssel für Gesundheitsdaten (Abschnitt <c>HealthData</c>). Mehrere Schlüssel erlauben einen Schlüsselwechsel.</summary>
public sealed class HealthDataOptions
{
    public const string Section = "HealthData";

    /// <summary>Kennung des Schlüssels, mit dem neu verschlüsselt wird.</summary>
    public string CurrentKeyId { get; set; } = "";

    /// <summary>Schlüssel als Base64 (32 Byte), je Kennung. Alte Schlüssel bleiben zum Entschlüsseln stehen.</summary>
    public Dictionary<string, string> Keys { get; set; } = [];

    /// <summary>
    /// Schlüsselcode des Spas für den Abgleich (ADR 0048): Damit verschlüsselt legt jede Stelle ihren Schlüssel in <c>sync.health_keys</c>
    /// und liest die der anderen Stellen. Nur über Geheimnisspeicher oder Umgebung, nie in der Datenbank oder im Repository.
    /// </summary>
    public string? ShareCode { get; set; }
}

/// <summary>Schlüssel anderer Stellen, die der Abgleich mit dem Schlüsselcode des Spas geöffnet hat (ADR 0048); nur im Speicher.</summary>
public sealed class HealthKeyRing
{
    private readonly ConcurrentDictionary<string, byte[]> _keys = new(StringComparer.Ordinal);

    public bool Contains(string keyId) => _keys.ContainsKey(keyId);

    public void Add(string keyId, string base64)
    {
        var key = Convert.FromBase64String(base64);
        if (key.Length == 32)
        {
            _keys[keyId] = key;
        }
    }

    internal bool TryGet(string keyId, out byte[] key) => _keys.TryGetValue(keyId, out key!);
}

/// <summary>
/// Verschlüsselt Gesundheitsdaten (Anamnese) mit AES-256-GCM, bevor sie in die Datenbank gehen. Ein Datenbankauszug oder
/// ein Backup allein enthält damit keine lesbaren Gesundheitsdaten. Die Datensatz-Id ist als zusätzliche Authentifizierung
/// eingebunden, so lassen sich verschlüsselte Antworten nicht unbemerkt zwischen Datensätzen vertauschen.
/// </summary>
internal sealed class HealthDataProtector(Microsoft.Extensions.Options.IOptions<HealthDataOptions> options, HealthKeyRing ring)
{
    private const byte FormatVersion = 1;
    private const int NonceSize = 12;
    private const int TagSize = 16;

    public byte[] Protect(string plaintext, Guid recordId, string? context = null) =>
        Protect(Encoding.UTF8.GetBytes(plaintext), recordId, context);

    public string Unprotect(byte[] payload, Guid recordId, string? context = null) =>
        Encoding.UTF8.GetString(UnprotectBytes(payload, recordId, context));

    /// <summary>
    /// Verschlüsselt Bytes (z. B. ein Foto). <c>context</c> unterscheidet mehrere Felder desselben Datensatzes, damit sie sich nicht
    /// gegeneinander tauschen lassen.
    /// </summary>
    public byte[] Protect(byte[] data, Guid recordId, string? context = null)
    {
        var (keyId, key) = CurrentKey();
        var keyIdBytes = Encoding.ASCII.GetBytes(keyId);

        // Aufbau: Version | Länge der Schlüsselkennung | Schlüsselkennung | Nonce | Tag | verschlüsselte Daten
        var payload = new byte[2 + keyIdBytes.Length + NonceSize + TagSize + data.Length];
        payload[0] = FormatVersion;
        payload[1] = (byte)keyIdBytes.Length;
        keyIdBytes.CopyTo(payload, 2);
        var nonce = payload.AsSpan(2 + keyIdBytes.Length, NonceSize);
        var tag = payload.AsSpan(2 + keyIdBytes.Length + NonceSize, TagSize);
        var cipher = payload.AsSpan(2 + keyIdBytes.Length + NonceSize + TagSize);
        RandomNumberGenerator.Fill(nonce);

        using var aes = new AesGcm(key, TagSize);
        aes.Encrypt(nonce, data, cipher, tag, AssociatedData(recordId, context));
        return payload;
    }

    public byte[] UnprotectBytes(byte[] payload, Guid recordId, string? context = null)
    {
        if (payload.Length < 2 || payload[0] != FormatVersion)
        {
            throw new CryptographicException("Unbekanntes Format verschlüsselter Gesundheitsdaten.");
        }

        var keyIdLength = payload[1];
        var keyId = Encoding.ASCII.GetString(payload, 2, keyIdLength);
        var key = Key(keyId);
        var offset = 2 + keyIdLength;
        var nonce = payload.AsSpan(offset, NonceSize);
        var tag = payload.AsSpan(offset + NonceSize, TagSize);
        var cipher = payload.AsSpan(offset + NonceSize + TagSize);
        var data = new byte[cipher.Length];

        using var aes = new AesGcm(key, TagSize);
        aes.Decrypt(nonce, cipher, tag, data, AssociatedData(recordId, context));
        return data;
    }

    private static byte[] AssociatedData(Guid recordId, string? context) =>
        context is null ? recordId.ToByteArray() : [.. recordId.ToByteArray(), .. Encoding.UTF8.GetBytes(context)];

    public bool IsConfigured => options.Value.Keys.ContainsKey(options.Value.CurrentKeyId);

    private (string KeyId, byte[] Key) CurrentKey()
    {
        var keyId = options.Value.CurrentKeyId;
        return (keyId, Key(keyId));
    }

    private byte[] Key(string keyId)
    {
        if (!options.Value.Keys.TryGetValue(keyId, out var base64) || string.IsNullOrWhiteSpace(base64))
        {
            if (ring.TryGet(keyId, out var shared))
            {
                return shared;
            }

            throw new InvalidOperationException(
                $"Schlüssel „{keyId}“ für Gesundheitsdaten fehlt (Konfiguration {HealthDataOptions.Section}:Keys).");
        }

        var key = Convert.FromBase64String(base64);
        return key.Length == 32
            ? key
            : throw new InvalidOperationException($"Schlüssel „{keyId}“ für Gesundheitsdaten muss 32 Byte lang sein.");
    }
}
