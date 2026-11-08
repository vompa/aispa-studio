using AispaStudio.Api.MailIntake.Assistant;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Tests;

/// <summary>KI-Attrappe für die Übersetzung: setzt „[DE]“ davor; enthält die Nachricht „FALSCHE ZAHL“, ändert sie eine Zahl.</summary>
public sealed class FakeMessageTranslator : IMessageTranslator
{
    public bool Available => true;

    public Task<TranslatedText> ToGermanAsync(string subject, string body, CancellationToken ct) =>
        Task.FromResult(new TranslatedText($"[DE] {subject}",
            body.Contains("FALSCHE ZAHL", StringComparison.Ordinal) ? $"[DE] {body} 99" : $"[DE] {body}"));
}

/// <summary>
/// KI-Attrappe für die Überarbeitung („Skript-Modell“); ein Hinweis mit „Zahl“ lässt sie eine erfundene Zahl ergänzen. „Förmlicher“ setzt eine Anrede in der Sprache des Textes davor
/// (Beweis, dass die Sprache beim Modell ankommt und bleibt), „Mit Entschuldigung“ ergänzt bei einem Text mit „PARKPLATZ“ eine neue Zahl (die der Server verwerfen muss).
/// </summary>
public sealed class FakeReplyReviser : IReplyReviser
{
    public bool Available => true;

    public Task<string> ReviseAsync(string body, string language, ReplyRevisionKind kind, string? instruction, CancellationToken ct) =>
        Task.FromResult(kind switch
        {
            ReplyRevisionKind.Friendlier => $"Herzlich willkommen! {body}",
            ReplyRevisionKind.Shorter => body.Split('\n')[0],
            ReplyRevisionKind.Formal => $"{FormalGreeting(language)} {body}",
            ReplyRevisionKind.Apology when body.Contains("PARKPLATZ", StringComparison.Ordinal) => $"{body} Parkplatz Nr. 4711.",
            ReplyRevisionKind.Custom when instruction!.Contains("Zahl", StringComparison.Ordinal) => $"{body} Parkplatz Nr. 4711.",
            ReplyRevisionKind.Custom => $"{body} ({instruction})",
            _ => $"{kind}: {body}",
        });

    /// <summary>Der Satz, den die Attrappe in der jeweiligen Sprache voranstellt.</summary>
    public static string FormalGreeting(string language) => language switch
    {
        "en" => "Dear guest,",
        "fr" => "Cher client,",
        "it" => "Gentile cliente,",
        _ => "Sehr geehrter Gast,",
    };
}
