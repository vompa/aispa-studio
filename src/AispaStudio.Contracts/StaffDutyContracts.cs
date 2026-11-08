namespace AispaStudio.Contracts;

/// <summary>Fester Schlüssel der sieben Vorgaben in der Liste der Zuständigkeiten (R05 Slice 117, Ä274); eigene Einträge haben keinen.</summary>
public enum DutyTypeKey
{
    FirstAid,
    SafetyRep,
    Stock,
    FireSafety,
    Hygiene,
    CashResponsibility,
    Training,
}

/// <summary>Art einer Erinnerung in „Erinnerungen“ (Kalender der Windows-App, „Heute“ der Mitarbeiter-App).</summary>
public enum DutyReminderKind
{
    /// <summary>Ein Zertifikat ist in der Erinnerungsfrist.</summary>
    CertificateExpiring,

    /// <summary>Ein Zertifikat ist abgelaufen und noch nicht erneuert.</summary>
    CertificateExpired,

    /// <summary>Der Bestellvorschlag des Standorts nennt Material.</summary>
    StockReorder,

    /// <summary>Chargen am Standort laufen bald ab oder sind abgelaufen.</summary>
    StockExpiry,
}

/// <summary>
/// Eintrag der Liste der Zuständigkeiten. <c>RequiredCertificate</c>: Bezeichnung des Zertifikats, das die Zuständigkeit verlangt (z. B. „Erste-Hilfe-Kurs“).
/// <c>MinimumOnDuty</c>: nur beim Ersthelfer, so viele müssen je Tag und Standort im Dienst sein (0 = keine Warnung).
/// </summary>
public sealed record DutyTypeDto(Guid Id, DutyTypeKey? Key, string Name, string? RequiredCertificate, int? MinimumOnDuty, bool IsActive);

/// <summary>Eintrag der Liste anlegen oder ändern; eine Mindestzahl zählt nur beim Ersthelfer.</summary>
public sealed record DutyTypeInput(string Name, string? RequiredCertificate = null, int? MinimumOnDuty = null, bool IsActive = true);

/// <summary>Zuständigkeit einer Person: gilt für alle Standorte. <c>DeputyId</c>: eine andere aktive Person als Vertretung.</summary>
public sealed record StaffDutyInput(Guid DutyTypeId, DateOnly ValidFrom, DateOnly? ValidUntil = null, Guid? DeputyId = null);

public sealed record StaffDutyDto(
    Guid Id, Guid EmployeeId, Guid DutyTypeId, string DutyTypeName, DateOnly ValidFrom, DateOnly? ValidUntil, Guid? DeputyId, string? DeputyName, bool IsExpired);

/// <summary>Zertifikat anlegen oder ändern. Ohne <c>ValidUntil</c> gilt es unbefristet und erinnert nie; <c>RemindDays</c> 1 bis 365.</summary>
public sealed record StaffCertificateInput(string Name, DateOnly? IssuedOn = null, DateOnly? ValidUntil = null, string? IssuedBy = null, int RemindDays = 60);

/// <summary><c>IsDue</c>: in der Erinnerungsfrist oder abgelaufen und nicht erneuert; <c>IsExpired</c>: „gültig bis“ liegt vor heute.</summary>
public sealed record StaffCertificateDto(
    Guid Id, Guid EmployeeId, string Name, DateOnly? IssuedOn, DateOnly? ValidUntil, string? IssuedBy, int RemindDays, bool HasScan, bool IsDue, bool IsExpired);

/// <summary>Scan eines Zertifikats hochladen: PDF, JPEG oder PNG bis 5 MB, als Base64.</summary>
public sealed record StaffCertificateScanInput(string FileName, string ContentType, string Base64);

/// <summary>Eine Person in „Wer ist wofür zuständig?“.</summary>
public sealed record DutyHolderDto(
    Guid DutyId, Guid EmployeeId, string Name, Guid? DeputyId, string? DeputyName, DateOnly ValidFrom, DateOnly? ValidUntil, bool IsExpired);

/// <summary>Eine Zuständigkeit in „Wer ist wofür zuständig?“; ohne Eintrag mit leerer Liste („niemand eingetragen“).</summary>
public sealed record DutyOverviewRowDto(Guid DutyTypeId, DutyTypeKey? Key, string Name, string? RequiredCertificate, IReadOnlyList<DutyHolderDto> Holders);

/// <summary>Ein Zertifikat in „Was läuft bald ab?“: in der Erinnerungsfrist oder abgelaufen, nicht erneuert.</summary>
public sealed record ExpiringCertificateDto(Guid CertificateId, Guid EmployeeId, string EmployeeName, string Name, DateOnly ValidUntil, bool IsExpired);

/// <summary>Erinnerung für die angemeldete Person; <c>Date</c>: Ablaufdatum des Zertifikats bzw. heute bei Lageraufgaben.</summary>
public sealed record DutyReminderDto(DutyReminderKind Kind, string Title, string Text, Guid? EmployeeId, Guid? LocationId, DateOnly Date);

/// <summary>Kennzeichen einer Zuständigkeit oder eines Zertifikats; <c>Warning</c> steht mit Warnsymbol vor den übrigen Kennzeichen.</summary>
public sealed record DutyFlagDto(string Text, string? Warning);
