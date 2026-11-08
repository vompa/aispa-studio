using AispaStudio.Api.Scheduling;
using AispaStudio.Contracts;

namespace AispaStudio.Api.MailIntake.Assistant;

/// <summary>Auswahl, aus der die KI wählen darf (Behandlung, Standort, Person). Sie gibt nur diese Ids zurück.</summary>
public sealed record NamedOption(Guid Id, string Name);

/// <summary>Alles, was die KI zum Verstehen einer Mail bekommt. Zeiten der Antwort gelten in der Zeitzone <c>TimeZone</c>.</summary>
public sealed record MailAnalysisRequest(
    string FromEmail,
    string? FromName,
    string Subject,
    string Body,
    DateTimeOffset ReceivedAt,
    string TimeZone,
    IReadOnlyList<NamedOption> Treatments,
    IReadOnlyList<NamedOption> Locations,
    IReadOnlyList<NamedOption> Employees,
    string? Earlier = null);

/// <summary>Gewünschter Termin in Ortszeit: ein Tag, optional ab einer Uhrzeit und bis zu einer Uhrzeit.</summary>
/// <remarks>Nur <c>From</c> gesetzt heißt „genau um diese Uhrzeit“.</remarks>
public sealed record RequestedTime(DateOnly Date, TimeOnly? From, TimeOnly? Until);

public enum MailIntent
{
    BookingRequest,
    Cancellation,
    Reschedule,
    Question,
    Other,
}

/// <summary>Was die KI aus einer Mail erkannt hat. Ids stammen nur aus den Auswahllisten der Anfrage.</summary>
public sealed record MailAnalysis(
    MailIntent Intent,
    string Language,
    string? FirstName,
    string? LastName,
    string? Phone,
    Guid? TreatmentId,
    Guid? LocationId,
    Guid? EmployeeId,
    IReadOnlyList<RequestedTime> Wishes,
    string Summary,
    CustomerSalutation? Salutation = null,
    string? TitlesBefore = null);

/// <summary>Was die Terminannahme gemacht hat; daraus entsteht die Antwort an den Gast.</summary>
public enum ReplyCase
{
    /// <summary>Termin ist eingetragen (<c>Booked</c>).</summary>
    Booked,

    /// <summary>Wunsch ist nicht frei oder nicht eindeutig; <c>Alternatives</c> sind freie Zeiten.</summary>
    Proposed,

    /// <summary>In absehbarer Zeit nichts frei; das Team meldet sich.</summary>
    NoAvailability,

    /// <summary>Es fehlt etwas (z. B. welche Behandlung oder welcher Standort); <c>Missing</c> sagt was.</summary>
    NeedsDetails,

    /// <summary>Storno, Verschiebung oder Frage: Eingang bestätigen, das Team kümmert sich.</summary>
    Forwarded,

    /// <summary>Die Rezeption hat den abgesagten Termin storniert; <c>Booked</c> ist seine Zeit.</summary>
    Cancelled,

    /// <summary>Die Rezeption hat eine Zeit für den Gast vorgemerkt (ADR 0083); <c>Booked</c> ist ihre Zeit, der Gast soll kurz bestätigen.</summary>
    Held,
}

/// <summary>Fakten für die Antwort. Die KI formuliert nur, sie erfindet keine Zeiten oder Preise dazu.</summary>
/// <param name="Booked">Gebuchter Termin in Ortszeit des Standorts.</param>
/// <param name="Alternatives">Freie Zeiten in Ortszeit des Standorts.</param>
/// <param name="ShortMessage">Antwort per SMS oder WhatsApp: kurz, ohne Betreff und Briefform.</param>
public sealed record ReplyRequest(
    string Language,
    ReplyCase Case,
    string GuestName,
    string OriginalSubject,
    string OriginalBody,
    string SpaName,
    string? TreatmentName,
    string? LocationName,
    DateTime? Booked,
    IReadOnlyList<DateTime> Alternatives,
    IReadOnlyList<string> Missing,
    bool ShortMessage = false);

public sealed record ReplyDraft(string Subject, string Body);

/// <summary>
/// KI für die Terminannahme, austauschbar: Claude über die Anthropic-API oder <see cref="RuleBasedBookingAssistant"/>
/// (ohne KI, für Tests und Betrieb ohne API-Schlüssel). Die KI entscheidet nichts selbst: sie liest und formuliert,
/// buchen und prüfen tut der Kalender.
/// </summary>
public interface IBookingAssistant
{
    Task<MailAnalysis> AnalyzeAsync(MailAnalysisRequest request, CancellationToken ct);

    Task<ReplyDraft> DraftReplyAsync(ReplyRequest request, CancellationToken ct);
}

/// <summary>Der feste Hinweis auf die KI unter Antworten in den Sprachen der Antworten; welche das sind und der Rückfall (Deutsch, Ä52) stehen in <c>GuestLanguage</c>.</summary>
public static class ReplyLanguages
{
    /// <summary>
    /// Hinweis unter jeder Antwort, dass sie mit KI erstellt wurde (AI Act Art. 50). Steht fest im Code, damit weder
    /// die KI noch eine Bearbeitung ihn weglassen kann.
    /// </summary>
    public static string Disclosure(string language) => GuestLanguage.Normalize(language) switch
    {
        "de" => "Diese Nachricht wurde von einem KI-Assistenten erstellt. Bei Fragen antworten Sie einfach, unser Team liest mit.",
        "fr" => "Ce message a été rédigé par un assistant IA. Pour toute question, répondez simplement, notre équipe lit vos messages.",
        "it" => "Questo messaggio è stato redatto da un assistente IA. Per domande basta rispondere, il nostro team legge i messaggi.",
        _ => "This message was written by an AI assistant. If you have questions, just reply; our team reads every message.",
    };

    /// <summary>Kurzfassung des Hinweises für SMS und WhatsApp, damit die Nachricht kurz bleibt.</summary>
    public static string ShortDisclosure(string language) => GuestLanguage.Normalize(language) switch
    {
        "de" => "(Automatisch erstellt von einem KI-Assistenten)",
        "fr" => "(Rédigé automatiquement par un assistant IA)",
        "it" => "(Redatto automaticamente da un assistente IA)",
        _ => "(Written automatically by an AI assistant)",
    };
}
