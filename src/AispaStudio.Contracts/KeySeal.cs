using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AispaStudio.Contracts;

/// <summary>
/// Schlüssel für Gesundheitsdaten mit einem Code verschlüsseln, den Menschen abtippen können (ADR 0041, ADR 0048). Gemeinsam für die
/// Windows-App (Wiederherstellungsschlüssel neben der Sicherung) und die API (Schlüssel der Stellen beim Abgleich). Der Code wird
/// nie gespeichert, wo auch die verschlüsselten Daten liegen.
/// </summary>
public static class KeySeal
{
    /// <summary>Ohne leicht verwechselbare Zeichen (0/O, 1/I/L): 31 Zeichen, knapp 5 Bit je Zeichen.</summary>
    private const string Alphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";
    private const int Iterations = 600_000;

    /// <summary>Länge eines Codes ohne Bindestriche.</summary>
    public const int CodeLength = 24;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Verschlüsselter Schlüssel als JSON: PBKDF2-SHA256 aus dem Code, AES-256-GCM.</summary>
    public sealed record Sealed(int Version, string Kdf, int Iterations, string Salt, string Nonce, string Tag, string Data);

    private sealed record Content(string KeyId, string Key);

    /// <summary>Neuer Code aus 24 Zeichen in sechs Vierergruppen (rund 119 Bit), z. B. „K7QM-2XRT-…“.</summary>
    public static string NewCode()
    {
        var chars = Enumerable.Range(0, CodeLength).Select(_ => Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)]).ToArray();
        return string.Join('-', chars.Chunk(4).Select(c => new string(c)));
    }

    /// <summary>Hat der eingetippte Code die volle Länge (Groß- und Kleinschreibung, Leerzeichen und Bindestriche zählen nicht)?</summary>
    public static bool IsComplete(string? code) => code is not null && code.Count(char.IsLetterOrDigit) == CodeLength;

    public static string Seal(string keyId, string key, string code)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var nonce = RandomNumberGenerator.GetBytes(12);
        var plain = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new Content(keyId, key), Json));
        var cipher = new byte[plain.Length];
        var tag = new byte[16];
        using (var aes = new AesGcm(Derive(code, salt, Iterations), tag.Length))
        {
            aes.Encrypt(nonce, plain, cipher, tag);
        }

        return JsonSerializer.Serialize(new Sealed(1, "pbkdf2-sha256", Iterations, Convert.ToBase64String(salt), Convert.ToBase64String(nonce),
            Convert.ToBase64String(tag), Convert.ToBase64String(cipher)), Json);
    }

    /// <summary>Kennung und Schlüssel; <c>false</c>, wenn der Code nicht passt oder der Inhalt beschädigt ist.</summary>
    public static bool TryOpen(string sealedJson, string code, out string keyId, out string key)
    {
        keyId = key = "";
        try
        {
            var box = JsonSerializer.Deserialize<Sealed>(sealedJson, Json) ?? throw new JsonException();
            var cipher = Convert.FromBase64String(box.Data);
            var plain = new byte[cipher.Length];
            using (var aes = new AesGcm(Derive(code, Convert.FromBase64String(box.Salt), box.Iterations), 16))
            {
                aes.Decrypt(Convert.FromBase64String(box.Nonce), cipher, Convert.FromBase64String(box.Tag), plain);
            }

            var content = JsonSerializer.Deserialize<Content>(plain, Json) ?? throw new JsonException();
            (keyId, key) = (content.KeyId, content.Key);
            return true;
        }
        catch (Exception ex) when (ex is CryptographicException or JsonException or FormatException)
        {
            return false;
        }
    }

    private static byte[] Derive(string code, byte[] salt, int iterations)
    {
        var normalized = new string(code.Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());
        return Rfc2898DeriveBytes.Pbkdf2(normalized, salt, iterations, HashAlgorithmName.SHA256, 32);
    }
}
