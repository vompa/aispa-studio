using AispaStudio.Contracts;

namespace AispaStudio.Api.MailIntake.Assistant;

/// <summary>
/// Welches Modell eine Aufgabe bekommt und was das Modell an Parametern annimmt (T-112b). Aufgaben, die nur feste Felder aus
/// einem Text oder Bild auslesen, laufen auf dem kleinen Modell (<see cref="ClaudeOptions.ModelExtract"/>); Texte für Menschen auf
/// dem großen (<see cref="ClaudeOptions.Model"/>).
/// </summary>
internal static class ClaudeModels
{
    /// <summary>Auslesen in ein festes Schema: Terminwunsch, Treatwell-Mail, Lieferschein, Anamnesebogen, Absicht im Assistenten.</summary>
    public static bool IsExtraction(AiUsageTask task) => task is AiUsageTask.BookingRequest or AiUsageTask.Treatwell
        or AiUsageTask.DeliveryNote or AiUsageTask.Anamnesis or AiUsageTask.AssistantIntent;

    /// <summary>
    /// <c>effort</c> lehnen ältere Modelle mit einem Fehler ab (Haiku 4.5, Sonnet 4.5 und älter); alle aktuellen Modelle nehmen es an.
    /// </summary>
    public static bool SupportsEffort(string model) =>
        !NoEffort.Any(prefix => model.StartsWith(prefix, StringComparison.Ordinal));

    /// <summary>
    /// Der Rückfall auf ein anderes Modell bei einer Ablehnung (<c>fallbacks</c>) gibt es nur für Opus, Sonnet 5.5 und Fable ab
    /// Version 5. Haiku 5.5 hat keinen; dort lehnt die API eine Liste ab.
    /// </summary>
    public static bool SupportsServerFallback(string model) =>
        WithFallback.Any(prefix => model.StartsWith(prefix, StringComparison.Ordinal));

    private static readonly string[] NoEffort =
        ["claude-3", "claude-haiku-4", "claude-sonnet-4-5", "claude-sonnet-4-2", "claude-opus-4-0", "claude-opus-4-1", "claude-opus-4-2"];

    private static readonly string[] WithFallback = ["claude-opus-5", "claude-sonnet-5-5", "claude-fable-5", "claude-mythos-5"];
}
