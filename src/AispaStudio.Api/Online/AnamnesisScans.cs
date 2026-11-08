using System.Text.Json;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Online;

/// <summary>Nachricht in der Queue: dieses Foto eines Papierbogens lesen. Das Hochladen wartet nicht auf die KI.</summary>
public sealed record ReadAnamnesisScan(Guid ScanId);

/// <summary>Vorschlag der KI, so wie er verschlüsselt beim Foto liegt.</summary>
internal sealed record StoredReading(
    IReadOnlyList<ExtractedAnswer> Answers, string? Signature, bool SignatureUncertain, bool? ConsentChecked, string? Note);

internal static class AnamnesisScanData
{
    public const string ImageContext = "image";
    public const string ReadingContext = "reading";

    public static StoredReading? Reading(AnamnesisScan scan, HealthDataProtector protector) =>
        scan.Reading is null
            ? null
            : JsonSerializer.Deserialize<StoredReading>(protector.Unprotect(scan.Reading, scan.Id, ReadingContext), JsonSerializerOptions.Web);

    public static AnamnesisScanDto ToDto(AnamnesisScan scan, HealthDataProtector protector)
    {
        var reading = Reading(scan, protector);
        return new AnamnesisScanDto(scan.Id, scan.CustomerId, scan.AppointmentId, scan.Status, scan.UploadedAt, scan.Reader,
            reading?.Answers ?? [], reading?.Signature, reading?.SignatureUncertain ?? true, reading?.ConsentChecked, reading?.Note, scan.RecordId);
    }
}

/// <summary>
/// Liest ein hochgeladenes Foto mit dem eingerichteten <see cref="IAnamnesisReader"/>. Kann die KI das Foto nicht lesen, bleibt
/// der Bogen trotzdem prüfbar: alle Felder leer und gelb, das Team überträgt von Hand.
/// </summary>
public static partial class ReadAnamnesisScanHandler
{
    // Die Dienste kommen aus einem eigenen Scope: der DbContext ist gepoolt (Fabrik-Registrierung), das kann Wolverine nicht selbst
    // einsetzen.
    public static async Task Handle(ReadAnamnesisScan message, IServiceScopeFactory scopes, CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var store = services.GetRequiredService<OnlineDbContext>();
        var protector = services.GetRequiredService<HealthDataProtector>();
        var reader = services.GetRequiredService<IAnamnesisReader>();
        var logger = services.GetRequiredService<ILogger<AnamnesisScan>>();
        var time = services.GetRequiredService<TimeProvider>();

        var scan = await store.AnamnesisScans.FirstOrDefaultAsync(s => s.Id == message.ScanId, ct);
        if (scan is null || scan.Status != AnamnesisScanStatus.Reading)
        {
            return;
        }

        var form = AnamnesisForm.Definition(scan.Language);
        var image = new AnamnesisImage(protector.UnprotectBytes(scan.Image, scan.Id, AnamnesisScanData.ImageContext), scan.ContentType);
        AnamnesisReading reading;
        var readerName = reader.Name;
        try
        {
            reading = await reader.ReadAsync(image, form, ct);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            ReadFailed(logger, scan.Id, e);
            readerName = "Manuell";
            reading = new AnamnesisReading([], null, 0, null, "Die KI konnte das Foto nicht lesen: bitte die Antworten vom Foto übertragen.");
        }

        var stored = new StoredReading(
            AnamnesisReadings.ForReview(reading, form),
            reading.Signature,
            reading.Signature is null || reading.SignatureConfidence < AnamnesisReadings.ConfidentFrom,
            reading.ConsentChecked,
            reading.Note);
        scan.Reading = protector.Protect(JsonSerializer.Serialize(stored, JsonSerializerOptions.Web), scan.Id, AnamnesisScanData.ReadingContext);
        scan.Reader = readerName;
        scan.ReadAt = time.GetUtcNow();
        scan.Status = AnamnesisScanStatus.ReadyForReview;
        await store.SaveChangesAsync(ct);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Papierbogen {ScanId} konnte nicht gelesen werden; Übertragung von Hand.")]
    private static partial void ReadFailed(ILogger logger, Guid scanId, Exception exception);
}
