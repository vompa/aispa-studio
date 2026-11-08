using AispaStudio.Contracts;

namespace AispaStudio.Api.Online;

/// <summary>Foto eines ausgefüllten Papierbogens.</summary>
public sealed record AnamnesisImage(byte[] Data, string ContentType);

/// <summary>Eine gelesene Antwort. <c>Confidence</c> 0–1: wie sicher die Antwort vom Papier gelesen wurde.</summary>
public sealed record ReadAnswer(string Key, bool? Yes, string? Details, double Confidence, string? Reason);

/// <summary>Was vom Papierbogen gelesen wurde. Felder, die fehlen, gelten als unsicher.</summary>
public sealed record AnamnesisReading(
    IReadOnlyList<ReadAnswer> Answers,
    string? Signature,
    double SignatureConfidence,
    bool? ConsentChecked,
    string? Note);

/// <summary>
/// Liest einen fotografierten Papierbogen. Austauschbar: Claude, sobald ein API-Schlüssel eingerichtet ist, sonst
/// <see cref="ManualAnamnesisReader"/>; in Tests eine Attrappe. Das Ergebnis ist immer nur ein Vorschlag, den das Team prüft.
/// </summary>
public interface IAnamnesisReader
{
    /// <summary>Wird beim Bogen gespeichert und im Team angezeigt, z. B. „Claude“.</summary>
    string Name { get; }

    Task<AnamnesisReading> ReadAsync(AnamnesisImage image, AnamnesisFormDefinition form, CancellationToken ct);
}

/// <summary>Ohne KI: alle Felder bleiben leer und gelb markiert, das Team überträgt den Bogen von Hand neben dem Foto.</summary>
internal sealed class ManualAnamnesisReader : IAnamnesisReader
{
    public string Name => "Manuell";

    public Task<AnamnesisReading> ReadAsync(AnamnesisImage image, AnamnesisFormDefinition form, CancellationToken ct) =>
        Task.FromResult(new AnamnesisReading([], null, 0, null, "Keine KI eingerichtet: bitte die Antworten vom Foto übertragen."));
}

internal static class AnamnesisReadings
{
    /// <summary>Darunter wird ein Feld gelb markiert und muss mit dem Foto verglichen werden.</summary>
    public const double ConfidentFrom = 0.8;

    /// <summary>Macht aus dem Vorschlag der KI die Anzeige: jede Frage genau einmal, Unbekanntes verworfen, Unsicheres markiert.</summary>
    public static IReadOnlyList<ExtractedAnswer> ForReview(AnamnesisReading reading, AnamnesisFormDefinition form)
    {
        return [.. form.Questions.Select(q =>
        {
            var read = reading.Answers.FirstOrDefault(a => a.Key == q.Key);
            if (read is null)
            {
                return new ExtractedAnswer(q.Key, null, null, true, "Nicht gelesen");
            }

            var yes = q.Kind == AnamnesisQuestionKind.YesNo ? read.Yes : null;
            var missing = q.Kind == AnamnesisQuestionKind.YesNo && yes is null;
            var details = string.IsNullOrWhiteSpace(read.Details) ? null : Trim(read.Details.Trim(), 1000);
            var uncertain = missing || read.Confidence < ConfidentFrom;
            return new ExtractedAnswer(q.Key, yes, details, uncertain,
                uncertain ? read.Reason ?? (missing ? "Kein Kästchen erkannt" : "Unsicher gelesen") : null);
        })];
    }

    private static string Trim(string value, int max) => value.Length <= max ? value : value[..max];
}
