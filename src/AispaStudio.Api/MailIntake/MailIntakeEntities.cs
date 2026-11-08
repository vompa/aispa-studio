using System.Text.Json;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace AispaStudio.Api.MailIntake;

// Terminannahme per Mail und Anbindung externer Buchungsplattformen. Eigene Tabellen neben dem Kalender-Fachmodell;
// Termine selbst entstehen immer über den BookingService (eine Wahrheit, gleiche Prüfung gegen Doppelbuchung).

/// <summary>Eine eingegangene Mail und was die Terminannahme daraus gemacht hat.</summary>
public sealed class InboundMail
{
    public Guid Id { get; init; } = Guid.CreateVersion7();

    /// <summary>Message-ID des Postfachs; verhindert, dass dieselbe Mail zweimal verarbeitet wird.</summary>
    public required string MessageId { get; init; }

    public BookingChannel Channel { get; set; } = BookingChannel.Email;
    public required string From { get; init; }
    public string? FromName { get; init; }
    public required string To { get; init; }
    public required string Subject { get; init; }
    public required string Body { get; init; }
    public DateTimeOffset ReceivedAt { get; init; }

    /// <summary>Vom Postfach bestätigt (SPF/DKIM/DMARC). Treatwell-Mails werden nur dann automatisch übernommen.</summary>
    public bool SenderVerified { get; init; }

    public InboundMailStatus Status { get; set; } = InboundMailStatus.Received;
    public InboundMailKind Kind { get; set; } = InboundMailKind.Unknown;

    /// <summary>Sprache der Mail (ISO 639-1, z. B. „de“); in dieser Sprache wird geantwortet.</summary>
    public string? Language { get; set; }

    /// <summary>Kurzfassung für die Rezeption.</summary>
    public string? Summary { get; set; }

    /// <summary>Warum die Rezeption entscheiden muss, oder Fehlermeldung.</summary>
    public string? Note { get; set; }

    public Guid? LocationId { get; set; }
    public Guid? TreatmentId { get; set; }
    public Guid? CustomerId { get; set; }

    /// <summary>
    /// Der Absender ist <see cref="CustomerId"/> sicher (Mail oder Nummer gleich) oder die Rezeption hat es bestätigt. Nur dann dürfen
    /// Antwortentwurf und Terminannahme Daten aus der Kundenkarte verwenden (Termine, Guthaben, Vorlieben; ADR 0061).
    /// </summary>
    public bool CustomerConfirmed { get; set; }

    /// <summary>Vorschläge, wer der Absender ist (höchstens 3), solange kein Kunde sicher oder bestätigt ist.</summary>
    public List<CustomerCandidateDto> CustomerCandidates { get; set; } = [];

    public Guid? AppointmentId { get; set; }

    /// <summary>Name und Telefon des Gasts, wie erkannt; daraus entsteht der Kunde, wenn es ihn noch nicht gibt.</summary>
    public string? GuestFirstName { get; set; }

    public string? GuestLastName { get; set; }
    public string? GuestPhone { get; set; }

    /// <summary>
    /// Vorschlag der Terminannahme aus Anrede und Signatur; nie automatisch übernommen, auch nicht beim neu angelegten Kunden: die Rezeption
    /// trägt Anrede und Titel selbst ein (ADR 0064).
    /// </summary>
    public CustomerSalutation? SuggestedSalutation { get; set; }

    public string? SuggestedTitlesBefore { get; set; }

    /// <summary>Mailadresse des Gasts, wenn sie nicht der Absender ist (Treatwell: die Adresse aus der Buchung).</summary>
    public string? GuestEmail { get; set; }

    public List<AvailableSlot> Proposals { get; set; } = [];

    public ReplyStatus ReplyStatus { get; set; } = ReplyStatus.None;
    public string? ReplyTo { get; set; }
    public string? ReplySubject { get; set; }
    public string? ReplyBody { get; set; }
    public DateTimeOffset? ProcessedAt { get; set; }
    public DateTimeOffset? RepliedAt { get; set; }

    /// <summary>Übersetzung ins Deutsche für die Rezeption; das Original bleibt in <see cref="Subject"/> und <see cref="Body"/>.</summary>
    public MessageTranslationState TranslationState { get; set; }

    public string? TranslatedSubject { get; set; }
    public string? TranslatedBody { get; set; }

    /// <summary>Rückübersetzung des Antwortentwurfs ins Deutsche, zur Kontrolle; gilt nur, solange der Entwurf gleich ist.</summary>
    public string? ReplyTranslation { get; set; }

    /// <summary>Prüfsumme des Entwurfs, zu dem <see cref="ReplyTranslation"/> gehört.</summary>
    public string? ReplyTranslationOf { get; set; }

    /// <summary>Frühere Fassungen des Entwurfs vor jeder KI-Überarbeitung, neueste zuletzt; „Rückgängig“ holt die letzte zurück.</summary>
    public List<string> ReplyHistory { get; set; } = [];

    /// <summary>Die KI überarbeitet den Entwurf gerade (Queue).</summary>
    public bool ReplyRevising { get; set; }

    /// <summary>Hinweis zur letzten Überarbeitung, z. B. warum sie verworfen wurde.</summary>
    public string? ReplyNotice { get; set; }

    /// <summary>Die Anfrage, auf deren gesendeten Vorschlag diese Nachricht antwortet (gleicher Absender).</summary>
    public Guid? PreviousMailId { get; set; }

    /// <summary>Schon vorgeschlagene, nicht gewählte Zeiten; neue Vorschläge lassen sie aus.</summary>
    public List<DateTimeOffset> RejectedStarts { get; set; } = [];

    /// <summary>Wievielter Vorschlag für diesen Wunsch (1 = erster); zählt über verknüpfte Antworten weiter.</summary>
    public int ProposalRound { get; set; }

    /// <summary>Hinweise der Rezeption aus „Neu vorschlagen“, die den Wunsch ergänzen.</summary>
    public string? WishNote { get; set; }

    /// <summary>„Neu vorschlagen“ läuft gerade (Queue).</summary>
    public bool Reproposing { get; set; }

    /// <summary>Buchungsnummer der Plattform (Treatwell), aus der Mail gelesen.</summary>
    public string? PlatformReference { get; set; }

    /// <summary>Wie der Gast laut Mail bezahlt: vorab über die Plattform oder im Spa.</summary>
    public PlatformPayment PlatformPayment { get; set; }

    /// <summary>Vorab über die Plattform bezahlter Betrag laut Mail.</summary>
    public decimal? PrepaidAmount { get; set; }

    /// <summary>Die Buchung steht im Kalender, ist aber in der Plattform noch anzunehmen (erst dann gilt sie dort); „Erledigt“ in der Rezeption setzt es zurück.</summary>
    public bool AcceptInPlatformPending { get; set; }

    /// <summary>Beginn der vorgemerkten Zeit (ADR 0083), solange die Vormerkung offen ist; <see cref="AppointmentId"/> ist der Termin dazu.</summary>
    public DateTimeOffset? HeldStart { get; set; }

    /// <summary>Muster der Absender-Regel, nach der die Mail behandelt wurde (zur Anzeige).</summary>
    public string? SenderRulePattern { get; set; }
}

/// <summary>
/// Buchung einer externen Plattform, z. B. Treatwell-Buchungsnummer → eigener Termin.
/// Der eindeutige Schlüssel (Kanal + externe Nummer) macht die Übernahme idempotent.
/// </summary>
public sealed class ChannelBooking
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public BookingChannel Channel { get; init; }
    public required string ExternalId { get; init; }
    public Guid? AppointmentId { get; set; }
    public Guid InboundMailId { get; set; }
    public DateTimeOffset CreatedAt { get; init; }
}

/// <summary>Name einer Leistung, Person oder eines Standorts auf einer Plattform → eigene Stammdaten.</summary>
public sealed class ChannelMapping
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public BookingChannel Channel { get; init; }
    public ChannelMappingKind Kind { get; init; }
    public required string ExternalName { get; init; }

    /// <summary>Für den eindeutigen Index: Name klein geschrieben und ohne Leerzeichen am Rand.</summary>
    public required string NormalizedName { get; init; }

    public Guid TargetId { get; init; }
}

/// <summary>ICS-Feed mit der Belegung eines Standorts oder einer Person, für Plattformen ohne Schnittstelle.</summary>
public sealed class CalendarFeed
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid LocationId { get; init; }
    public Guid? EmployeeId { get; init; }
    public required string Name { get; init; }
    public BookingChannel? Channel { get; init; }

    /// <summary>Zufälliger Zugangsschlüssel in der URL; Plattformen können keine Anmeldung.</summary>
    public required string Token { get; init; }

    public DateTimeOffset CreatedAt { get; init; }
}

internal static class MailIntakeModel
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static void Configure(ModelBuilder builder)
    {
        builder.Entity<InboundMail>(e =>
        {
            e.Property(m => m.MessageId).HasMaxLength(500);
            e.HasIndex(m => m.MessageId).IsUnique();
            e.Property(m => m.Channel).HasConversion<string>().HasMaxLength(16);
            e.Property(m => m.Status).HasConversion<string>().HasMaxLength(16);
            e.Property(m => m.Kind).HasConversion<string>().HasMaxLength(32);
            e.Property(m => m.ReplyStatus).HasConversion<string>().HasMaxLength(16);
            e.Property(m => m.From).HasMaxLength(320);
            e.Property(m => m.FromName).HasMaxLength(200);
            e.Property(m => m.To).HasMaxLength(320);
            e.Property(m => m.Subject).HasMaxLength(1000);
            e.Property(m => m.Language).HasMaxLength(8);
            e.Property(m => m.Summary).HasMaxLength(2000);
            e.Property(m => m.Note).HasMaxLength(2000);
            e.Property(m => m.GuestFirstName).HasMaxLength(100);
            e.Property(m => m.GuestLastName).HasMaxLength(100);
            e.Property(m => m.GuestPhone).HasMaxLength(50);
            e.Property(m => m.SuggestedSalutation).HasConversion<string>().HasMaxLength(8);
            e.Property(m => m.SuggestedTitlesBefore).HasMaxLength(100);
            e.Property(m => m.GuestEmail).HasMaxLength(320);
            e.Property(m => m.ReplyTo).HasMaxLength(320);
            e.Property(m => m.ReplySubject).HasMaxLength(1000);
            e.Property(m => m.Proposals).HasColumnType("jsonb").HasConversion(
                v => JsonSerializer.Serialize(v, JsonOptions),
                v => JsonSerializer.Deserialize<List<AvailableSlot>>(v, JsonOptions) ?? new List<AvailableSlot>(),
                new ValueComparer<List<AvailableSlot>>(
                    (a, b) => a!.SequenceEqual(b!),
                    v => v.Aggregate(0, (hash, slot) => HashCode.Combine(hash, slot.GetHashCode())),
                    v => v.ToList()));
            e.Property(m => m.CustomerCandidates).HasColumnType("jsonb").HasConversion(
                v => JsonSerializer.Serialize(v, JsonOptions),
                v => JsonSerializer.Deserialize<List<CustomerCandidateDto>>(v, JsonOptions) ?? new List<CustomerCandidateDto>(),
                new ValueComparer<List<CustomerCandidateDto>>(
                    (a, b) => a!.SequenceEqual(b!),
                    v => v.Aggregate(0, (hash, candidate) => HashCode.Combine(hash, candidate.GetHashCode())),
                    v => v.ToList()));
            e.Property(m => m.TranslationState).HasConversion<string>().HasMaxLength(16);
            e.Property(m => m.TranslatedSubject).HasMaxLength(1000);
            e.Property(m => m.ReplyTranslationOf).HasMaxLength(64);
            e.Property(m => m.ReplyNotice).HasMaxLength(500);
            e.Property(m => m.ReplyHistory).HasColumnType("jsonb").HasConversion(
                v => JsonSerializer.Serialize(v, JsonOptions),
                v => JsonSerializer.Deserialize<List<string>>(v, JsonOptions) ?? new List<string>(),
                new ValueComparer<List<string>>(
                    (a, b) => a!.SequenceEqual(b!),
                    v => v.Aggregate(0, (hash, text) => HashCode.Combine(hash, text.GetHashCode(StringComparison.Ordinal))),
                    v => v.ToList()));
            e.Property(m => m.RejectedStarts).HasColumnType("jsonb").HasConversion(
                v => JsonSerializer.Serialize(v, JsonOptions),
                v => JsonSerializer.Deserialize<List<DateTimeOffset>>(v, JsonOptions) ?? new List<DateTimeOffset>(),
                new ValueComparer<List<DateTimeOffset>>(
                    (a, b) => a!.SequenceEqual(b!),
                    v => v.Aggregate(0, (hash, start) => HashCode.Combine(hash, start.GetHashCode())),
                    v => v.ToList()));
            e.Property(m => m.WishNote).HasMaxLength(1000);
            e.Property(m => m.PlatformReference).HasMaxLength(200);
            e.Property(m => m.PlatformPayment).HasConversion<string>().HasMaxLength(16);
            e.Property(m => m.PrepaidAmount).HasPrecision(10, 2);
            e.Property(m => m.SenderRulePattern).HasMaxLength(320);
            e.HasIndex(m => new { m.Status, m.ReceivedAt });
            e.HasIndex(m => new { m.From, m.RepliedAt });
        });

        builder.Entity<ChannelBooking>(e =>
        {
            e.Property(b => b.Channel).HasConversion<string>().HasMaxLength(16);
            e.Property(b => b.ExternalId).HasMaxLength(200);
            e.HasIndex(b => new { b.Channel, b.ExternalId }).IsUnique();
            e.HasIndex(b => b.AppointmentId);
        });

        builder.Entity<ChannelMapping>(e =>
        {
            e.Property(m => m.Channel).HasConversion<string>().HasMaxLength(16);
            e.Property(m => m.Kind).HasConversion<string>().HasMaxLength(16);
            e.Property(m => m.ExternalName).HasMaxLength(200);
            e.Property(m => m.NormalizedName).HasMaxLength(200);
            e.HasIndex(m => new { m.Channel, m.Kind, m.NormalizedName }).IsUnique();
        });

        builder.Entity<CalendarFeed>(e =>
        {
            e.Property(f => f.Name).HasMaxLength(200);
            e.Property(f => f.Channel).HasConversion<string>().HasMaxLength(16);
            e.Property(f => f.Token).HasMaxLength(64);
            e.HasIndex(f => f.Token).IsUnique();
            e.HasIndex(f => f.LocationId);
        });
    }
}
