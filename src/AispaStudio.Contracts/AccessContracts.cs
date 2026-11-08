namespace AispaStudio.Contracts;

/// <summary>Rolle einer Person im Spa. Jede Person hat genau eine Rolle, gültig für ihre Standorte.</summary>
public enum StaffRole
{
    /// <summary>Inhaberin oder Geschäftsführung: alles, an allen Standorten.</summary>
    Owner,

    /// <summary>Standortleitung: plant, pflegt Stammdaten und lädt Personen für ihre Standorte ein.</summary>
    Manager,

    /// <summary>Rezeption: bucht, verschiebt, storniert, checkt ein und legt Kunden an.</summary>
    Reception,

    /// <summary>Therapeutin oder Therapeut: sieht den Kalender und setzt den Status der eigenen Termine.</summary>
    Therapist,

    /// <summary>
    /// Manager (Geschäftsleitung neben dem Inhaber): dieselben Rechte wie der Inhaber. Der Inhaber bleibt Eigentümer des Kontos:
    /// nur er legt Inhaber- und Manager-Zugänge an, ändert oder entfernt sie und kündigt Lizenz und Abrechnung (ADR 0070).
    /// Der Name <c>Manager</c> ist an die Standortleitung vergeben, deshalb heißt dieser Wert <c>GeneralManager</c>.
    /// </summary>
    GeneralManager,

    /// <summary>Backoffice (Buchhaltung, Lohnverrechnung): liest Berichte mit Beträgen, Belege, Tagesabschlüsse, Dienstplan und Arbeitszeiten; bucht und kassiert nicht.</summary>
    Backoffice,
}

/// <summary>Was eine Rolle darf. Die Oberfläche blendet danach ein und aus, der Server prüft es bei jedem Aufruf.</summary>
public enum AccessRight
{
    /// <summary>Kalender, Team, Behandlungen und Kunden ansehen.</summary>
    ViewCalendar,

    /// <summary>Termine buchen, verschieben, stornieren, Sperrzeiten setzen, Kunden anlegen.</summary>
    Book,

    /// <summary>Status von Terminen setzen (eingecheckt, abgeschlossen, nicht erschienen). Therapeuten nur für eigene Termine.</summary>
    ChangeStatus,

    /// <summary>Öffnungszeiten, Räume und Geräte der eigenen Standorte pflegen.</summary>
    ManageLocation,

    /// <summary>Team, Behandlungen und Qualifikationen pflegen.</summary>
    ManageCatalog,

    /// <summary>Personen einladen, Rollen und Standorte zuweisen, Zugänge sperren.</summary>
    ManageStaff,

    /// <summary>Standorte anlegen; Firmendaten, Zahlungskonto, Kassa-Einstellungen, Kontenzuordnung und Lohn-Einstellungen ändern. Inhaber und Manager ernennt nur der Inhaber (kein Recht, sondern Eigenschaft der Rolle).</summary>
    ManageOrganization,

    /// <summary>
    /// Management-Cockpit und Pflichtberichte der eigenen Standorte ansehen, aber nur die Kennzahlen ohne Beträge (Auslastung, Anzahlen, Quoten).
    /// Beträge zeigt erst <see cref="ViewAmountReports"/>.
    /// </summary>
    ViewFinance,

    /// <summary>Kassenaufsicht: jeden Beleg stornieren und Bargeld aus der Lade entnehmen. Kassieren und Tagesabschluss darf auch die Rezeption.</summary>
    SuperviseCash,

    /// <summary>
    /// Berichte mit Beträgen: Umsätze, Umsatz je Behandlung, Person und Standort, Kosten und Margen, Gutscheinsummen, Tagesabschlusszahlen,
    /// Buchhaltungsexport und steuerliche Exporte (DEP, DSFinV-K, TSE). Inhaber, Manager und Backoffice haben es immer, Standortleitung
    /// und Rezeption nur, wenn es dem Zugang einzeln vergeben ist. Schließt <see cref="ViewFinance"/> ein.
    /// </summary>
    ViewAmountReports,

    /// <summary>Belege und Tagesabschlüsse der Kassen lesen (Backoffice); kassieren und abschließen darf nur, wer buchen darf.</summary>
    ViewReceipts,

    /// <summary>Dienstplan, Arbeitszeiten, Abwesenheiten, Stammdaten des Teams (ohne Gesundheits- und Zugangsdaten) und den Lohnexport lesen (Backoffice).</summary>
    ViewStaffRecords,

    /// <summary>Personalakte lesen und ändern: Gespräche, Anmerkungen, Verwarnungen (R05 Slice 110) sowie Hinweise, Sonderregelungen und Vollmachten (R05 Slice 111); nur Inhaber und Manager.</summary>
    ManageHrFile,

    /// <summary>
    /// Anamnese lesen: Antworten des Bogens und fotografierte Bögen ansehen, prüfen, übernehmen und verwerfen (R05 Slice 128, ADR 0013).
    /// Alle Rollen, die Kunden sehen (Inhaber, Manager, Standortleitung, Rezeption, Therapeut:innen; Ä309); das Backoffice sieht nichts.
    /// </summary>
    ViewHealthData,
}

/// <summary>Zugang einer Person: Rolle und Standorte.</summary>
/// <param name="EmployeeId">Verknüpft das Konto mit einer Person im Team (für Therapeuten nötig, um eigene Termine zu erkennen).</param>
/// <param name="ExpiresAt">Befristung: ab diesem Zeitpunkt wirkt der Zugang wie gesperrt; leer heißt unbefristet. Nicht für Inhaber.</param>
/// <param name="AmountReports">Einzeln vergebenes Recht „Berichte mit Beträgen“ (<see cref="AccessRight.ViewAmountReports"/>) für Standortleitung und Rezeption; bei den anderen Rollen ohne Wirkung und nicht gespeichert.</param>
/// <param name="PlainReports">Nur für die Rolle Therapeut:in: „Auswertungen ohne Beträge“ im Assistenten (Standard an); bei den anderen Rollen ohne Wirkung und nicht gespeichert (Ä55).</param>
public sealed record StaffAccessInput(
    StaffRole Role, bool AllLocations, IReadOnlyList<Guid> LocationIds, Guid? EmployeeId, bool IsActive, DateTimeOffset? ExpiresAt = null,
    bool AmountReports = false, bool PlainReports = true);

public sealed record StaffMemberDto(
    string UserId,
    string Email,
    string DisplayName,
    StaffRole Role,
    bool AllLocations,
    IReadOnlyList<Guid> LocationIds,
    Guid? EmployeeId,
    bool IsActive,
    int PasskeyCount,
    DateTimeOffset? ExpiresAt = null,
    string? UserName = null,
    bool MustChangePassword = false,
    bool AmountReports = false,
    bool PlainReports = true);

/// <summary>
/// Einzelplatz-Betrieb: Zugang direkt anlegen, mit Benutzername und Startpasswort statt Einladung per Link.
/// Das Startpasswort muss die Person bei der ersten Anmeldung ändern.
/// </summary>
public sealed record StaffAccountInput(
    string UserName,
    string DisplayName,
    string Password,
    StaffRole Role,
    bool AllLocations,
    IReadOnlyList<Guid> LocationIds,
    Guid? EmployeeId,
    DateTimeOffset? ExpiresAt = null,
    bool AmountReports = false,
    bool PlainReports = true);

/// <summary>Einzelplatz-Betrieb: die Leitung setzt ein neues Startpasswort, das die Person bei der nächsten Anmeldung ändert.</summary>
public sealed record PasswordReset(string NewPassword);

/// <summary>Einladung per Link: die eingeladene Person öffnet ihn, nimmt an und legt ihren Passkey an.</summary>
public sealed record InvitationInput(
    string Email,
    string DisplayName,
    StaffRole Role,
    bool AllLocations,
    IReadOnlyList<Guid> LocationIds,
    Guid? EmployeeId,
    DateTimeOffset? AccessExpiresAt = null,
    bool AmountReports = false,
    bool PlainReports = true);

public sealed record InvitationDto(
    Guid Id,
    string Email,
    string DisplayName,
    StaffRole Role,
    bool AllLocations,
    IReadOnlyList<Guid> LocationIds,
    Guid? EmployeeId,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt,
    DateTimeOffset? AccessExpiresAt = null,
    bool AmountReports = false,
    bool PlainReports = true);

/// <summary>Antwort beim Anlegen: nur hier steht der geheime Teil des Links, gespeichert wird nur sein Hash.</summary>
public sealed record InvitationCreated(InvitationDto Invitation, string Token, string Link);

/// <summary>Was die eingeladene Person vor dem Annehmen sieht.</summary>
public sealed record InvitationPreview(string Email, string DisplayName, StaffRole Role, IReadOnlyList<string> LocationNames, DateTimeOffset ExpiresAt);

/// <summary>Einmal-Code, mit dem sich die Windows-App als die Person anmeldet, die ihn im Browser erzeugt hat (10 Minuten gültig).</summary>
public sealed record DeviceCode(string Code, DateTimeOffset ExpiresAt);

public sealed record DeviceCodeRedeem(string Code);
