using System.Text;
using System.Text.Json;
using AispaStudio.Api.Online;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Tests;

/// <summary>
/// Liest „Fotos“, die Tests selbst bauen: JPEG-Kennung gefolgt vom gewünschten Ergebnis als JSON. So bestimmt jeder Test sein
/// Ergebnis selbst, und parallele Tests stören sich nicht. Ohne JSON wirft die Attrappe, wie eine KI, die nicht lesen kann.
/// </summary>
public sealed class FakeAnamnesisReader : IAnamnesisReader
{
    private static readonly byte[] Magic = [0xFF, 0xD8, 0xFF];

    public string Name => "Testleser";

    public static byte[] Photo(AnamnesisReading reading) =>
        [.. Magic, .. Encoding.UTF8.GetBytes(JsonSerializer.Serialize(reading, JsonSerializerOptions.Web))];

    public static byte[] Unreadable() => [.. Magic, .. "kein Bogen"u8];

    public Task<AnamnesisReading> ReadAsync(AnamnesisImage image, AnamnesisFormDefinition form, CancellationToken ct)
    {
        var json = Encoding.UTF8.GetString(image.Data.AsSpan(Magic.Length));
        return json.StartsWith('{')
            ? Task.FromResult(JsonSerializer.Deserialize<AnamnesisReading>(json, JsonSerializerOptions.Web)!)
            : throw new InvalidOperationException("Foto unlesbar.");
    }
}
