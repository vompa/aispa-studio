using AispaStudio.Contracts;
using Microsoft.AspNetCore.Identity;

namespace AispaStudio.Api.Online;

/// <summary>
/// Konto einer Kundin oder eines Kunden im Kundenbereich. Getrennt von den Konten des Teams (<c>AispaUser</c>): eigene Tabelle,
/// eigenes Anmelde-Cookie, keine Rechte auf die Endpunkte der Rezeption. Wer im Team arbeitet, kann mit derselben
/// E-Mail-Adresse zusätzlich ein Kundenkonto haben.
/// </summary>
public sealed class CustomerAccount : IdentityUser<Guid>
{
    public CustomerAccount()
    {
        Id = Guid.CreateVersion7();
        SecurityStamp = Guid.NewGuid().ToString("N");
    }

    /// <summary>Kunde im gemeinsamen Kundenstamm; leer, bis Name und Kontakt bekannt sind (erste Buchung oder Profil).</summary>
    public Guid? CustomerId { get; set; }

    public DateTimeOffset CreatedAt { get; init; }
}

/// <summary>Einmal-Code für die Anmeldung per E-Mail. Gespeichert wird nur ein Hash.</summary>
public sealed class SignInCode
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public required string NormalizedEmail { get; init; }
    public required byte[] CodeHash { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset ExpiresAt { get; init; }
    public int FailedAttempts { get; set; }
    public DateTimeOffset? UsedAt { get; set; }
}

public enum AccessLinkPurpose
{
    /// <summary>Termin ansehen und stornieren, ohne Konto.</summary>
    ManageAppointment,

    /// <summary>Anamnesebogen ausfüllen, ohne Konto.</summary>
    Anamnesis,

    /// <summary>Anamnesebogen am Tablet des Empfangs: kurz gültig, nach dem Abgeben gelöscht.</summary>
    AnamnesisTablet,

    /// <summary>Eintrag auf der Warteliste ansehen und austragen, ohne Konto.</summary>
    ManageWaitlist,

    /// <summary>Einwilligung in Erinnerungen an die nächste Behandlung bestätigen oder widerrufen (ADR 0040).</summary>
    MarketingConsent,
}

/// <summary>
/// Link aus der Bestätigungsmail. Der Code im Link ist 256 Bit zufällig; gespeichert wird nur sein SHA-256-Hash,
/// damit ein Datenbankauszug keine gültigen Links enthält.
/// </summary>
public sealed class AccessLink
{
    public required byte[] TokenHash { get; init; }
    public AccessLinkPurpose Purpose { get; init; }
    public Guid CustomerId { get; init; }
    public Guid? AppointmentId { get; init; }

    /// <summary>Nur bei <see cref="AccessLinkPurpose.ManageWaitlist"/>.</summary>
    public Guid? WaitlistEntryId { get; init; }

    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset ExpiresAt { get; init; }
}

/// <summary>Herkunft eines online gebuchten Termins: Einwilligung in den Datenschutzhinweis, Konto (falls angemeldet), Sprache.</summary>
public sealed class OnlineBooking
{
    public Guid AppointmentId { get; init; }
    public Guid CustomerId { get; init; }
    public Guid? CustomerAccountId { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset PrivacyAcceptedAt { get; init; }
    public required string PrivacyNoticeVersion { get; init; }
    public required string Language { get; init; }
    public DateTimeOffset? CancelledAt { get; set; }
}

/// <summary>
/// Ausgefüllter Anamnesebogen (Gesundheitsdaten nach Art. 9 DSGVO). Die Antworten liegen nur verschlüsselt vor
/// (AES-256-GCM, siehe <see cref="HealthDataProtector"/>), in einem eigenen Datenbankschema.
/// </summary>
public sealed class AnamnesisRecord
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid CustomerId { get; init; }
    public Guid? AppointmentId { get; init; }
    public required string FormVersion { get; init; }

    /// <summary>„Link“, „Tablet“ oder „Papier“.</summary>
    public required string Channel { get; init; }

    public DateTimeOffset SubmittedAt { get; init; }
    public DateTimeOffset ConsentGivenAt { get; init; }

    /// <summary>Schlüsselkennung, Nonce, Tag und verschlüsselte Antworten samt Unterschrift.</summary>
    public required byte[] Payload { get; init; }
}

/// <summary>
/// Fotografierter Papierbogen. Foto und KI-Vorschlag sind Gesundheitsdaten und liegen nur verschlüsselt vor, wie der Bogen selbst.
/// Das Foto bleibt nach der Bestätigung als Beleg der Unterschrift erhalten.
/// </summary>
public sealed class AnamnesisScan
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid CustomerId { get; init; }
    public Guid? AppointmentId { get; init; }
    public DateTimeOffset UploadedAt { get; init; }

    /// <summary>Konto im Team, das hochgeladen hat.</summary>
    public required string UploadedBy { get; init; }

    public required string ContentType { get; init; }

    /// <summary>Sprache des gedruckten Bogens („de“, „en“, „fr“ oder „it“).</summary>
    public required string Language { get; init; }

    /// <summary>Verschlüsseltes Foto.</summary>
    public required byte[] Image { get; init; }

    public AnamnesisScanStatus Status { get; set; }

    /// <summary>„Claude“ oder „Manuell“.</summary>
    public string? Reader { get; set; }

    /// <summary>Verschlüsselter Vorschlag der KI (JSON).</summary>
    public byte[]? Reading { get; set; }

    public DateTimeOffset? ReadAt { get; set; }
    public Guid? RecordId { get; set; }
    public string? ConfirmedBy { get; set; }
}

/// <summary>
/// Eintrag auf der Warteliste. Kein Gesundheitsbezug, daher im Schema „online“. <c>Expired</c> wird nicht gespeichert, sondern
/// beim Lesen aus <c>Until</c> abgeleitet.
/// </summary>
public sealed class WaitlistEntry
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid CustomerId { get; init; }
    public Guid? CustomerAccountId { get; init; }
    public Guid LocationId { get; init; }
    public Guid TreatmentId { get; init; }
    public DateOnly From { get; init; }
    public DateOnly Until { get; init; }
    public DayPart DayPart { get; init; }
    public required string Language { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset PrivacyAcceptedAt { get; init; }
    public required string PrivacyNoticeVersion { get; init; }
    public WaitlistStatus Status { get; set; }
    public DateTimeOffset? ClosedAt { get; set; }

    /// <summary>Gebuchter Termin, der den Wunsch erfüllt hat.</summary>
    public Guid? AppointmentId { get; set; }

    public int OfferCount { get; set; }
    public DateTimeOffset? LastOfferedAt { get; set; }

    /// <summary>Tag des zuletzt gemeldeten freien Termins; derselbe Tag wird höchstens alle 12 Stunden erneut gemeldet.</summary>
    public DateOnly? LastOfferedDate { get; set; }
}

/// <summary>
/// Online-Bestellung eines Gutscheins. Den Gutschein selbst stellt nach der Zahlung die Kasse des Standorts aus (Beleg, Erlöskonto,
/// Signatur wie am Empfang); hier liegen nur Bestellung, Zahlung und Verweise darauf.
/// </summary>
public sealed class VoucherOrder
{
    public Guid Id { get; init; } = Guid.CreateVersion7();

    /// <summary>SHA-256 des Codes im Link zur Bestellung.</summary>
    public required byte[] TokenHash { get; init; }

    public Guid LocationId { get; init; }
    public Guid CustomerId { get; init; }
    public Guid? CustomerAccountId { get; init; }
    public VoucherKind Kind { get; init; }
    public decimal Amount { get; init; }
    public required string Currency { get; init; }
    public Guid? TreatmentId { get; init; }
    public required string BuyerEmail { get; init; }
    public string? RecipientName { get; init; }
    public string? Message { get; init; }
    public required string Language { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset PrivacyAcceptedAt { get; init; }
    public required string PrivacyNoticeVersion { get; init; }

    public VoucherOrderStatus Status { get; set; }

    /// <summary>Name des Zahlungsanbieters, z. B. „Test“.</summary>
    public required string Provider { get; init; }

    public string? ProviderReference { get; set; }

    /// <summary>Seite des Anbieters, auf der bezahlt wird; bis der Anbieter sie geliefert hat, leer.</summary>
    public string? PaymentUrl { get; set; }

    public DateTimeOffset? PaidAt { get; set; }

    /// <summary>Offene Rechnung an der Kasse, sobald das Ausstellen begonnen hat; so wird bei einer Wiederholung nichts doppelt verkauft.</summary>
    public Guid? BillId { get; set; }

    public Guid? ReceiptId { get; set; }
    public Guid? VoucherId { get; set; }
    public DateTimeOffset? IssuedAt { get; set; }
    public string? Error { get; set; }
}

/// <summary>
/// Hinterlegte Karte zu einem Online-Termin, wenn der Standort eine Ausfallgebühr verlangt. Gespeichert werden nur die Kennungen
/// beim Zahlungsanbieter, nie Kartendaten. Belastet wird erst, wenn jemand vom Team es ausdrücklich auslöst; Gebühr und Beleg
/// laufen dann über die Kasse des Standorts.
/// </summary>
public sealed class CardGuarantee
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid AppointmentId { get; init; }
    public Guid LocationId { get; init; }
    public Guid CustomerId { get; init; }
    public required string Email { get; init; }
    public required string Language { get; init; }

    /// <summary>Prozent und Betrag, denen beim Buchen zugestimmt wurde.</summary>
    public int FeePercent { get; init; }

    public decimal FeeAmount { get; init; }
    public required string Currency { get; init; }
    /// <summary>Zustimmung des Gastes zur Ausfallgebühr; leer, solange ein Link der Rezeption darauf wartet.</summary>
    public DateTimeOffset? AcceptedAt { get; set; }

    /// <summary>Stornofrist in Stunden beim Buchen; eine spätere Absage kostet die Gebühr.</summary>
    public int DeadlineHours { get; init; }

    public CardGuaranteeStatus Status { get; set; }

    /// <summary>
    /// Ohne hinterlegte Karte wird der online gebuchte Termin zu diesem Zeitpunkt freigegeben (Regel des Standorts beim Buchen);
    /// leer bei Links der Rezeption und wenn die Frist erst nach Terminbeginn endete.
    /// </summary>
    public DateTimeOffset? ReleaseAt { get; init; }

    /// <summary>Name des Zahlungsanbieters, z. B. „Stripe“.</summary>
    public required string Provider { get; init; }

    /// <summary>Wie oft das Hinterlegen begonnen wurde; gehört zum Idempotenzschlüssel beim Anbieter.</summary>
    public int Attempts { get; set; }

    /// <summary>Sitzung beim Anbieter, auf der die Karte hinterlegt wird.</summary>
    public string? SessionReference { get; set; }

    public string? SetupUrl { get; set; }

    /// <summary>Kennung der hinterlegten Zahlungsart beim Anbieter (bei Stripe der SetupIntent).</summary>
    public string? CardReference { get; set; }

    /// <summary>Kundin oder Kunde beim Anbieter (bei Stripe <c>cus_…</c>).</summary>
    public string? ProviderCustomer { get; set; }

    public DateTimeOffset? SavedAt { get; set; }

    /// <summary>
    /// Karte und Kunde beim Anbieter gelöscht, weil die Frist nach dem Termin abgelaufen ist; danach wird nicht mehr belastet.
    /// </summary>
    public DateTimeOffset? CardRemovedAt { get; set; }

    /// <summary>Wer im Team die Belastung ausgelöst hat (Benutzer-Id, steht als Kassier am Beleg).</summary>
    public string? ChargedBy { get; set; }

    /// <summary>Kennung der Belastung beim Anbieter; einmal gesetzt, wird nie ein zweites Mal belastet.</summary>
    public string? ChargeReference { get; set; }

    /// <summary>Offene Rechnung an der Kasse, sobald das Verbuchen begonnen hat.</summary>
    public Guid? BillId { get; set; }

    public Guid? ReceiptId { get; set; }
    public DateTimeOffset? ChargedAt { get; set; }
    public string? Error { get; set; }
}

/// <summary>
/// Anzahlung oder Vorauszahlung auf einen Online-Termin. Bezahlt wird beim Zahlungsanbieter; den Anzahlungsbeleg stellt danach die
/// Kasse des Standorts aus (<c>ReceiptOnlineDepositHandler</c>), versteuert wie die Behandlung. Beim Kassieren der Behandlung
/// verrechnet die Kasse den Betrag. Bei rechtzeitiger Absage wird der Beleg storniert und erstattet; bei Nichterscheinen oder
/// später Absage bleibt die Anzahlung beim Spa.
/// </summary>
public sealed class DepositOrder
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid AppointmentId { get; init; }
    public Guid LocationId { get; init; }
    public Guid CustomerId { get; init; }
    public required string Email { get; init; }
    public required string Language { get; init; }

    /// <summary>Prozent und Betrag, denen beim Buchen zugestimmt wurde.</summary>
    public int Percent { get; init; }

    public decimal Amount { get; init; }
    public required string Currency { get; init; }
    public DateTimeOffset AcceptedAt { get; init; }

    /// <summary>Stornofrist in Stunden beim Buchen; eine spätere Absage kostet die Anzahlung.</summary>
    public int DeadlineHours { get; init; }

    public OnlineDepositStatus Status { get; set; }

    /// <summary>Ohne Zahlung wird der Termin zu diesem Zeitpunkt freigegeben (Regel des Standorts beim Buchen); leer, wenn nie.</summary>
    public DateTimeOffset? ReleaseAt { get; init; }

    /// <summary>Name des Zahlungsanbieters, z. B. „Stripe“.</summary>
    public required string Provider { get; init; }

    /// <summary>Wie oft die Zahlung begonnen wurde; gehört zum Idempotenzschlüssel beim Anbieter.</summary>
    public int Attempts { get; set; }

    /// <summary>Zahlung beim Anbieter (Checkout-Sitzung); nach dem Bezahlen die Referenz für die Erstattung.</summary>
    public string? ProviderReference { get; set; }

    public string? PaymentUrl { get; set; }
    public DateTimeOffset? PaidAt { get; set; }

    /// <summary>Offene Rechnung an der Kasse, sobald das Verbuchen begonnen hat.</summary>
    public Guid? BillId { get; set; }

    /// <summary>Anzahlungsbeleg der Kasse.</summary>
    public Guid? ReceiptId { get; set; }

    public DateTimeOffset? ReceiptedAt { get; set; }
    public string? Error { get; set; }
}

/// <summary>
/// Rückerstattung einer Online-Zahlung, nachdem die Kassenaufsicht den Beleg storniert hat. Je Beleg höchstens eine; der
/// Idempotenzschlüssel beim Anbieter ist die Kennung dieser Zeile, ein erneuter Versuch erstattet also nie doppelt.
/// </summary>
public sealed class OnlineRefund
{
    public Guid Id { get; init; } = Guid.CreateVersion7();

    /// <summary>Der online bezahlte Beleg.</summary>
    public Guid ReceiptId { get; init; }

    public Guid? VoidReceiptId { get; set; }
    public OnlinePaymentSource Source { get; init; }

    /// <summary>Bestellung bzw. Kartenhinterlegung.</summary>
    public Guid SourceId { get; init; }

    public Guid LocationId { get; init; }
    public required string Provider { get; init; }

    /// <summary>Kennung der Zahlung beim Anbieter (Checkout-Sitzung oder Zahlung).</summary>
    public required string PaymentReference { get; init; }

    public decimal Amount { get; init; }
    public required string Currency { get; init; }
    public required string Email { get; init; }
    public required string Language { get; init; }
    public string? Reason { get; set; }
    public string? RequestedBy { get; set; }
    public DateTimeOffset RequestedAt { get; set; }
    public int Attempts { get; set; }
    public OnlineRefundStatus Status { get; set; }
    public string? RefundReference { get; set; }
    public DateTimeOffset? RefundedAt { get; set; }
    public string? Error { get; set; }
}
