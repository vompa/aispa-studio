using System.Text;
using System.Text.Json;
using AispaStudio.Api.Stock;

namespace AispaStudio.Api.Tests;

/// <summary>
/// Leser für Tests: Das „Foto“ sind die JPEG-Anfangsbytes, gefolgt vom gewünschten Ergebnis als JSON. Die Schlüssel der Positionen
/// (<c>p1</c>, <c>p2</c> …) folgen der Reihenfolge der offenen Positionen, wie sie auch die echte KI bekommt.
/// </summary>
internal sealed class FakeDeliveryNoteReader : IDeliveryNoteReader
{
    private static readonly byte[] Magic = [0xFF, 0xD8, 0xFF];

    /// <summary>Die Positionen, die der Leser zuletzt gezeigt bekam.</summary>
    public IReadOnlyList<DeliveryNoteOrderLine> LastLines { get; private set; } = [];

    public bool Enabled => true;

    public static byte[] Photo(DeliveryNoteReading reading) =>
        [.. Magic, .. Encoding.UTF8.GetBytes(JsonSerializer.Serialize(reading, JsonSerializerOptions.Web))];

    public static byte[] Unreadable() => [.. Magic, .. "kein Lieferschein"u8];

    public Task<DeliveryNoteReading> ReadAsync(DeliveryNoteImage image, IReadOnlyList<DeliveryNoteOrderLine> lines, CancellationToken ct)
    {
        LastLines = lines;
        var json = Encoding.UTF8.GetString(image.Data.AsSpan(Magic.Length));
        return json.StartsWith('{')
            ? Task.FromResult(JsonSerializer.Deserialize<DeliveryNoteReading>(json, JsonSerializerOptions.Web)!)
            : throw new InvalidOperationException("Foto unlesbar.");
    }
}
