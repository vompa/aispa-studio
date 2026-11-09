using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Documents;

/// <summary>
/// Ein per Mail gesendetes Dokument (ADR 0091): Art und Ziel (Beleg, Termin, Gutschein, Bestellung), Empfänger, Betreff, Zeitpunkt und wer es ausgelöst hat (Vorname),
/// damit die Rezeption Doppelsendungen sieht. Mandantenspalte und Filter kommen vom <c>TenantModel</c>.
/// </summary>
public sealed class SentDocument
{
    public const string ReceiptKind = "receipt";
    public const string AppointmentConfirmationKind = "appointment-confirmation";

    public Guid Id { get; init; } = Guid.CreateVersion7();
    public required string Kind { get; init; }
    public Guid TargetId { get; init; }
    public required string To { get; init; }
    public required string Subject { get; init; }
    public DateTimeOffset SentAt { get; init; }
    public string? SentBy { get; init; }
    public string? UserId { get; init; }
}

internal static class SentDocumentModel
{
    public static void Configure(ModelBuilder builder) => builder.Entity<SentDocument>(e =>
    {
        e.Property(d => d.Id).ValueGeneratedNever();
        e.Property(d => d.Kind).HasMaxLength(40);
        e.Property(d => d.To).HasMaxLength(320);
        e.Property(d => d.Subject).HasMaxLength(200);
        e.Property(d => d.SentBy).HasMaxLength(100);
        e.Property(d => d.UserId).HasMaxLength(450);
        e.HasIndex(d => new { d.Kind, d.TargetId });
    });
}
