using System.Text.Json;
using AispaStudio.Api.Online;
using AispaStudio.Api.Scheduling;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.HrFile;

/// <summary>
/// Eintrag der Personalakte (R05 Slice 110, E8): im eigenen Schema <c>hr</c>. Offen steht nur, womit die API rechnet (Art, Datum, Folgetermin, Ablauf);
/// alles andere liegt verschlüsselt in <see cref="Payload"/> wie die Gesundheitsdaten (<see cref="HealthDataProtector"/>).
/// </summary>
public sealed class HrEntry
{
    public Guid Id { get; init; } = Guid.CreateVersion7();

    public Guid EmployeeId { get; init; }

    public Employee Employee { get; init; } = null!;

    public HrEntryKind Kind { get; init; }

    public DateOnly Date { get; set; }

    public DateOnly? FollowUpOn { get; set; }

    public bool FollowUpDone { get; set; }

    public DateOnly? ExpiresOn { get; set; }

    public required byte[] Payload { get; set; }
}

/// <summary>Der verschlüsselte Teil eines Eintrags.</summary>
internal sealed record HrPayload(
    string Text, HrConversationKind? ConversationKind, string? Participants, IReadOnlyList<HrAgreement> Agreements, string? IssuedBy, bool HandedOverInWriting);

internal static class HrFileModel
{
    public const string Schema = "hr";

    private const string ProtectionContext = "hr";

    public static void Configure(ModelBuilder builder) => builder.Entity<HrEntry>(e =>
    {
        e.ToTable("HrEntries", Schema);
        e.Property(h => h.Id).ValueGeneratedNever();
        e.Property(h => h.Kind).HasConversion<string>().HasMaxLength(16);
        e.HasIndex(h => h.EmployeeId);
        e.HasOne(h => h.Employee).WithMany().HasForeignKey(h => h.EmployeeId).OnDelete(DeleteBehavior.Restrict);
    });

    /// <summary>Übernimmt die Eingabe in den Eintrag: nur die Felder seiner Art; ohne Ablaufdatum gilt bei einer Verwarnung die Vorgabe.</summary>
    public static void Apply(this HrEntry entry, HrEntryInput input, HealthDataProtector protector)
    {
        var conversation = entry.Kind == HrEntryKind.Conversation;
        var warning = entry.Kind == HrEntryKind.Warning;
        entry.Date = input.Date;
        entry.FollowUpOn = conversation ? input.FollowUpOn : null;
        entry.FollowUpDone = conversation && input.FollowUpDone;
        entry.ExpiresOn = warning ? input.ExpiresOn ?? HrFileRules.DefaultExpiry(input.Date) : null;
        var payload = new HrPayload(
            input.Text.Trim(),
            conversation ? input.ConversationKind : null,
            conversation ? Clean(input.Participants) : null,
            conversation ? [.. (input.Agreements ?? []).Where(a => !string.IsNullOrWhiteSpace(a.Text)).Select(a => a with { Text = a.Text.Trim() })] : [],
            warning ? Clean(input.IssuedBy) : null,
            warning && input.HandedOverInWriting);
        entry.Payload = protector.Protect(JsonSerializer.Serialize(payload), entry.Id, ProtectionContext);
    }

    public static HrEntryDto ToDto(this HrEntry entry, HealthDataProtector protector)
    {
        var payload = JsonSerializer.Deserialize<HrPayload>(protector.Unprotect(entry.Payload, entry.Id, ProtectionContext))!;
        return new HrEntryDto(entry.Id, entry.Kind, entry.Date, payload.Text, payload.ConversationKind, payload.Participants, payload.Agreements,
            entry.FollowUpOn, entry.FollowUpDone, payload.IssuedBy, payload.HandedOverInWriting, entry.ExpiresOn);
    }

    private static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
