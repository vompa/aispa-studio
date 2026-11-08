using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace AispaStudio.Api.Pos.Fiscal.Rksv;

/// <summary>
/// Bausteine der Registrierkassensicherheitsverordnung (RKSV, Detailspezifikation):
/// maschinenlesbarer Code, verschlüsselter Umsatzzähler (AES-256-ICM), Verkettung über die vorige Signatur und JWS (ES256).
/// </summary>
internal static class RksvCode
{
    /// <summary>JWS-Header für ES256, Base64URL-kodiert.</summary>
    public const string JwsHeader = "eyJhbGciOiJFUzI1NiJ9";

    /// <summary>Statt des Umsatzzählers auf Stornobelegen: „STO“.</summary>
    public const string VoidMarker = "U1RP";

    /// <summary>Signaturwert bei ausgefallener Signatureinrichtung: „Sicherheitseinrichtung ausgefallen“.</summary>
    public static readonly byte[] DeviceFailedSignature = Encoding.UTF8.GetBytes("Sicherheitseinrichtung ausgefallen");

    /// <summary>Länge des Umsatzzählers in Bytes; bei FinanzOnline mit dem Schlüssel registriert.</summary>
    public const int TurnoverCounterLength = 8;

    public static byte[] NewTurnoverKey() => RandomNumberGenerator.GetBytes(32);

    public static string EncryptTurnover(long counterCents, string registerCode, long receiptNumber, byte[] key)
    {
        var plain = new byte[16];
        BinaryPrimitives.WriteInt64BigEndian(plain, counterCents);
        var stream = KeyStream(registerCode, receiptNumber, key);
        var result = new byte[TurnoverCounterLength];
        for (var i = 0; i < result.Length; i++)
        {
            result[i] = (byte)(plain[i] ^ stream[i]);
        }

        return Convert.ToBase64String(result);
    }

    public static long DecryptTurnover(string encrypted, string registerCode, long receiptNumber, byte[] key)
    {
        var cipher = Convert.FromBase64String(encrypted);
        var stream = KeyStream(registerCode, receiptNumber, key);
        var plain = new byte[8];
        for (var i = 0; i < plain.Length; i++)
        {
            plain[i] = (byte)(cipher[i] ^ stream[i]);
        }

        return BinaryPrimitives.ReadInt64BigEndian(plain);
    }

    /// <summary>Verkettungswert: die ersten 8 Bytes von SHA-256 über die JWS des vorigen Belegs, beim Startbeleg über die Kassen-ID.</summary>
    public static string ChainValue(string? previousJws, string registerCode)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(previousJws ?? registerCode));
        return Convert.ToBase64String(hash, 0, 8);
    }

    public static string MachineCode(
        string zdaId, string registerCode, long receiptNumber, DateTime localTime, RksvAmounts amounts,
        string turnover, string certificateSerial, string chainValue) =>
        string.Join('_',
            "",
            $"R1-{zdaId}",
            registerCode,
            receiptNumber.ToString(CultureInfo.InvariantCulture),
            localTime.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture),
            Amount(amounts.Normal),
            Amount(amounts.Reduced1),
            Amount(amounts.Reduced2),
            Amount(amounts.Zero),
            Amount(amounts.Special),
            turnover,
            certificateSerial,
            chainValue);

    public static string SigningInput(string machineCode) =>
        $"{JwsHeader}.{Base64Url(Encoding.UTF8.GetBytes(machineCode))}";

    /// <summary>QR-Code-Inhalt: maschinenlesbarer Code und Signatur (Base64, nicht URL-kodiert).</summary>
    public static string QrCode(string machineCode, byte[] signature) => $"{machineCode}_{Convert.ToBase64String(signature)}";

    public static string Jws(string signingInput, byte[] signature) => $"{signingInput}.{Base64Url(signature)}";

    /// <summary>Betrag mit Komma und zwei Nachkommastellen, wie im maschinenlesbaren Code verlangt.</summary>
    public static string Amount(decimal value) => value.ToString("0.00", CultureInfo.InvariantCulture).Replace('.', ',');

    public static string Base64Url(byte[] data) =>
        Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static byte[] FromBase64Url(string value)
    {
        var s = value.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(s.PadRight(s.Length + ((4 - (s.Length % 4)) % 4), '='));
    }

    private static byte[] KeyStream(string registerCode, long receiptNumber, byte[] key)
    {
        // IV: die ersten 16 Bytes von SHA-256 über Kassen-ID und Belegnummer; ICM entspricht AES-CTR mit diesem Startblock.
        var iv = SHA256.HashData(Encoding.UTF8.GetBytes(registerCode + receiptNumber.ToString(CultureInfo.InvariantCulture)))[..16];
        using var aes = Aes.Create();
        aes.Key = key;
        return aes.EncryptEcb(iv, PaddingMode.None);
    }
}

/// <summary>Bruttobeträge je Steuersatz in der Reihenfolge des maschinenlesbaren Codes.</summary>
internal sealed record RksvAmounts(decimal Normal, decimal Reduced1, decimal Reduced2, decimal Zero, decimal Special)
{
    public decimal Total => Normal + Reduced1 + Reduced2 + Zero + Special;
}
