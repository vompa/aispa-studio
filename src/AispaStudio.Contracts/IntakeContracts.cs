namespace AispaStudio.Contracts;

/// <summary>Art des eigenen Postfachs eines Spas.</summary>
public enum MailboxKind
{
    Imap,
    Graph,
}

/// <summary>Verschlüsselung der IMAP-Verbindung.</summary>
public enum MailboxSecurity
{
    SslOnConnect,
    StartTls,

    /// <summary>Nur für lokale Test-Postfächer.</summary>
    None,
}

/// <summary>Eigenes Postfach des Spas für die Terminannahme. Das Passwort bzw. Client-Geheimnis wird nie zurückgegeben.</summary>
/// <param name="Address">Adresse des Postfachs, bei Graph zugleich das Postfach, das gelesen wird.</param>
/// <param name="DirectoryTenantId">Nur Graph: Verzeichnis (Tenant) in Microsoft Entra.</param>
/// <param name="HasSecret">Ein Passwort bzw. Geheimnis ist gespeichert.</param>
/// <param name="LastCheckedAt">Letzter Abruf, erfolgreich oder nicht.</param>
/// <param name="LastError">Fehler beim letzten Abruf; leer, wenn er geklappt hat.</param>
/// <param name="SendReplies">Antworten und Erinnerungen gehen aus diesem Postfach (IMAP: über <paramref name="SmtpHost"/>, Graph: mit Mail.Send).</param>
/// <param name="SmtpHost">Nur IMAP: Server für den Versand, angemeldet mit demselben Benutzer und Passwort.</param>
/// <param name="LastSendError">Fehler beim letzten Versand aus dem Postfach; leer, wenn er geklappt hat.</param>
/// <param name="SaveSentCopy">Nur IMAP: gesendete Mails zusätzlich im Ordner „Gesendet“ ablegen (bei Gmail aus, Gmail tut das selbst).</param>
/// <param name="SentFolder">Ordner für die Kopie; leer heißt: den als „Gesendet“ gekennzeichneten Ordner bzw. einen üblichen Namen suchen.</param>
public sealed record SpaMailboxDto(
    MailboxKind Kind,
    string Address,
    bool Enabled,
    string? Host,
    int Port,
    MailboxSecurity Security,
    string? UserName,
    string Folder,
    string? ProcessedFolder,
    string? DirectoryTenantId,
    string? ClientId,
    bool HasSecret,
    DateTimeOffset? LastCheckedAt,
    string? LastError,
    bool SendReplies,
    string? SmtpHost,
    int SmtpPort,
    MailboxSecurity SmtpSecurity,
    string? LastSendError,
    bool SaveSentCopy,
    string? SentFolder);

/// <param name="Secret">IMAP-Passwort oder Graph-Client-Geheimnis; leer lassen, um das gespeicherte zu behalten.</param>
public sealed record SpaMailboxInput(
    MailboxKind Kind,
    string Address,
    bool Enabled,
    string? Host,
    int Port,
    MailboxSecurity Security,
    string? UserName,
    string? Folder,
    string? ProcessedFolder,
    string? DirectoryTenantId,
    string? ClientId,
    string? Secret,
    bool SendReplies = false,
    string? SmtpHost = null,
    int SmtpPort = 587,
    MailboxSecurity SmtpSecurity = MailboxSecurity.StartTls,
    bool SaveSentCopy = true,
    string? SentFolder = null);

/// <summary>Eine Nummer oder Adresse, über die Gäste dieses Spa erreichen. Vergeben vom Betreiber, weil die Nummern in seinem Anbieterkonto liegen.</summary>
/// <param name="Address">Nummer im Format E.164 oder Mailadresse.</param>
/// <param name="SenderId">Kennung beim Anbieter, falls nötig (Meta: Phone-Number-ID für den Versand).</param>
public sealed record IntakeRouteDto(Guid Id, BookingChannel Channel, string Address, string? SenderId);

/// <summary>Betreiber: Nummer oder Adresse einem Spa zuordnen.</summary>
public sealed record IntakeRouteInput(Guid TenantId, BookingChannel Channel, string Address, string? SenderId);

/// <summary>Betreiber: Zuordnung mit Spa.</summary>
public sealed record OperatorIntakeRouteDto(Guid Id, Guid TenantId, BookingChannel Channel, string Address, string? SenderId);

/// <summary>Betreiber: was in der Queue hängen geblieben ist, über alle Spas.</summary>
/// <param name="DeadLetters">Nachrichten in der Fehlerablage: fehlgeschlagen, niemand führt sie mehr aus.</param>
/// <param name="UnreceiptedDeposits">Bezahlte Anzahlungen, die länger als die Wartezeit des Nachholjobs keinen Beleg haben.</param>
/// <param name="UnissuedVouchers">Bezahlte Online-Gutscheine, die länger als die Wartezeit nicht ausgestellt sind.</param>
/// <param name="PendingRefunds">Erstattungen, die länger als die Wartezeit laufen.</param>
public sealed record OperatorQueueHealth(int DeadLetters, int UnreceiptedDeposits, int UnissuedVouchers, int PendingRefunds);

/// <summary>Betreiber: ein Spa mit dem Stand seines Postfachs und seinen Nummern.</summary>
/// <param name="MailboxAddress">Eigenes Postfach; leer, wenn keines eingerichtet ist.</param>
/// <param name="MailboxError">Fehler beim letzten Abruf oder Versand.</param>
/// <param name="LockedAt">Gesperrt seit; leer, wenn das Spa arbeiten kann.</param>
/// <param name="Plan">Abo-Stufe; schaltet die Webmodule frei (ADR 0015).</param>
public sealed record OperatorSpaDto(
    Guid Id,
    string Name,
    string Country,
    DateTimeOffset CreatedAt,
    int Locations,
    string? MailboxAddress,
    bool MailboxEnabled,
    DateTimeOffset? MailboxCheckedAt,
    string? MailboxError,
    IReadOnlyList<IntakeRouteDto> Routes,
    DateTimeOffset? LockedAt,
    string? LockReason,
    SubscriptionPlan Plan);

/// <summary>Betreiber: Spa sperren.</summary>
/// <param name="Reason">Grund, wie ihn das Team des Spas sieht (z. B. „Vertrag beendet, bitte melden Sie sich bei uns.“).</param>
public sealed record OperatorLockInput(string? Reason);

/// <summary>Betreiber: Abo-Stufe eines Spas setzen, z. B. nach Vertragsabschluss oder -änderung.</summary>
public sealed record OperatorPlanInput(SubscriptionPlan Plan);
