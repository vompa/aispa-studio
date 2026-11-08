namespace AispaStudio.Contracts;

/// <summary>Angemeldete Person mit ihrer Rolle, ihren Standorten und was sie damit darf.</summary>
/// <param name="Access">Fehlt, wenn das Konto (noch) keinen Zugang hat oder gesperrt ist; dann ist auch <paramref name="Rights"/> leer.</param>
/// <param name="EmployeeId">Die Person im Team, die zu diesem Konto gehört (z. B. für „meine Termine“).</param>
/// <param name="MustChangePassword">Einzelplatz-Betrieb: das Startpasswort gilt noch und muss vor dem Weiterarbeiten geändert werden.</param>
/// <param name="UserName">Benutzername im Einzelplatz-Betrieb; sonst die E-Mail.</param>
/// <param name="Channel">Über welchen Weg die Anmeldung läuft (Arbeitsplatz oder Mitarbeiter-App, Slice 104).</param>
public sealed record CurrentUser(
    string Id,
    string Email,
    string DisplayName,
    int PasskeyCount,
    CurrentAccess? Access,
    Guid? EmployeeId,
    IReadOnlyList<AccessRight> Rights,
    bool HasPin = false,
    bool MustChangePassword = false,
    string? UserName = null,
    AccessChannel Channel = AccessChannel.Workstation);

/// <summary>Zugang im eigenen Spa-Unternehmen (Mandant).</summary>
/// <param name="LockedAt">Gesetzt, wenn der Betreiber das Spa gesperrt hat: alles lesbar und exportierbar, Ändern geht nicht.</param>
public sealed record CurrentAccess(
    Guid TenantId,
    string TenantName,
    StaffRole Role,
    bool AllLocations,
    IReadOnlyList<Guid> LocationIds,
    DateTimeOffset? LockedAt = null,
    string? LockReason = null);

/// <summary>Eigene PIN für den Schnellwechsel am Empfangsgerät: 4 bis 8 Ziffern (Inhaber mindestens 6), nicht alle gleich.</summary>
public sealed record QuickSwitchPin(string Pin);

/// <summary>Ein Gerät (Browser) als Empfangsgerät eines Standorts anmelden; danach wechseln Personen dort per PIN.</summary>
public sealed record SharedDeviceInput(string Name, Guid LocationId, int IdleMinutes = SharedDeviceInput.DefaultIdleMinutes)
{
    public const int DefaultIdleMinutes = 5;
    public const int MaxIdleMinutes = 120;
}

public sealed record SharedDeviceDto(Guid Id, string Name, Guid LocationId, string LocationName, DateTimeOffset CreatedAt, int IdleMinutes);

/// <summary>Person, die am Empfangsgerät per PIN wechseln kann: aktiver Zugang am Standort des Geräts und eine PIN.</summary>
public sealed record QuickSwitchPerson(string UserId, string DisplayName, StaffRole Role);

/// <summary>Stand des Schnellwechsels auf diesem Gerät; ohne angemeldetes Empfangsgerät ist <paramref name="Device"/> leer.</summary>
public sealed record QuickSwitchState(SharedDeviceDto? Device, IReadOnlyList<QuickSwitchPerson> People);

public sealed record QuickSwitchRequest(string UserId, string Pin);

/// <summary>Ergebnis von navigator.credentials.create() bzw. .get(), als JSON serialisiert.</summary>
public sealed record PasskeyCredential(string CredentialJson);

/// <summary>
/// Nur in der Entwicklungsumgebung: legt ein Konto an, damit ein erster Passkey registriert werden kann.
/// Ohne Angabe wird es Inhaber für alle Standorte; mit <paramref name="LocationIds"/> nur für diese Standorte (und in deren Unternehmen).
/// Ohne Standorte landet es im ersten Unternehmen (Demodaten), mit <paramref name="NewTenant"/> in einem neuen.
/// </summary>
public sealed record DevRegistration(
    string Email,
    string DisplayName,
    StaffRole Role = StaffRole.Owner,
    IReadOnlyList<Guid>? LocationIds = null,
    Guid? EmployeeId = null,
    bool NewTenant = false);

/// <summary>Nur in der Entwicklungsumgebung: als Testkonto des Beispiel-Spas anmelden, ohne Passkey. <c>Persistent</c> für die Windows-App.</summary>
public sealed record DevLogin(string Email, bool Persistent = false);

/// <summary>Testkonto des Beispiel-Spas für die Auswahl auf der Anmeldeseite (nur Entwicklung).</summary>
public sealed record DevAccount(string Email, string DisplayName, StaffRole Role, IReadOnlyList<string> Locations);

/// <summary>Ein neues Spa-Unternehmen registrieren: legt den Mandanten und das Konto der Inhaberin bzw. des Inhabers an.</summary>
/// <param name="Country">AT, DE oder CH.</param>
public sealed record TenantSignup(string SpaName, string Country, string OwnerEmail, string OwnerName);

/// <summary><c>Company</c> ist immer gesetzt; ohne gepflegte Angaben sind die Felder leer.</summary>
public sealed record TenantDto(Guid Id, string Name, string Country, CompanyProfile? Company = null);

/// <summary><c>Name</c> ist der Name des Spas, wie Gäste und Team ihn sehen. <c>Company</c> weglassen lässt die Firmendaten unverändert.</summary>
public sealed record TenantInput(string Name, string Country, CompanyProfile? Company = null);

/// <summary>Websites, die den Kundenbereich per iframe einbetten dürfen: <c>https://host</c> oder <c>https://host:port</c>, ohne Pfad.</summary>
/// <param name="BookingUrl">Adresse des Kundenbereichs dieses Spas (für den Einbettungscode); nur in der Antwort der API, die Eingabe ignoriert sie.</param>
public sealed record EmbeddingSettings(IReadOnlyList<string> AllowedOrigins, string? BookingUrl = null);

/// <summary>
/// Firmendaten des Spas: gelten für alle Standorte, solange ein Standort keine eigenen Unternehmensangaben hat (ADR 0052).
/// Sie füllen Rechnungen, die Sofortüberweisung an der Kassa, das Impressum der Online-Buchung und später die Zahlungsanbieter.
/// Alles optional; leere Angaben bleiben leer.
/// </summary>
/// <param name="LegalName">Firmenwortlaut mit Rechtsform, z. B. „AISPA Day Spa GmbH“ oder „Maria Huber e.U.“.</param>
/// <param name="CompanyRegisterNumber">Firmenbuch- bzw. Handelsregisternummer, z. B. „FN 123456a“.</param>
/// <param name="CompanyRegisterCourt">Firmenbuchgericht, z. B. „Handelsgericht Wien“.</param>
/// <param name="BusinessPurpose">Unternehmensgegenstand, z. B. „Betrieb eines Day-Spas“ (Impressum nach § 25 MedienG).</param>
/// <param name="SupervisoryAuthority">Gewerbebehörde bzw. Aufsichtsbehörde, z. B. „Magistratisches Bezirksamt des 1. Bezirks“.</param>
/// <param name="ChamberAndRules">Kammer und berufsrechtliche Vorschriften, z. B. „Mitglied der WKO Wien; Gewerbeordnung, www.ris.bka.gv.at“.</param>
public sealed record CompanyProfile(
    string? LegalName,
    string? Street,
    string? PostalCode,
    string? City,
    string? Phone,
    string? Email,
    string? Website,
    string? VatId,
    string? CompanyRegisterNumber,
    string? CompanyRegisterCourt,
    string? AccountHolder,
    string? Iban,
    string? Bic,
    string? BusinessPurpose,
    string? SupervisoryAuthority,
    string? ChamberAndRules)
{
    public static readonly CompanyProfile Empty = new(null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null);
}

/// <summary>Einzelplatz-Betrieb (ADR 0041): ob die Einrichtung beim ersten Start noch aussteht.</summary>
/// <param name="Joining">
/// Noch kein Spa, aber eine zentrale Datenbank ist eingetragen: der PC verbindet sich mit einem bestehenden Spa, der erste Abgleich
/// holt es. Neu einrichten geht dann nicht.
/// </param>
/// <param name="JoinError">Warum der Abgleich beim Verbinden zuletzt gescheitert ist (z. B. falscher Schlüsselcode).</param>
public sealed record StandaloneSetupState(bool Required, bool Joining = false, string? JoinError = null);

/// <summary>Einrichtung beim ersten Start: Spa, erster Standort und das Konto der Inhaberin bzw. des Inhabers mit Passwort.</summary>
/// <param name="Country">AT, DE oder CH; bestimmt Zeitzone und Währung des ersten Standorts.</param>
public sealed record StandaloneSetup(string SpaName, string Country, string LocationName, string OwnerName, string UserName, string Password);

/// <summary>Anmeldung mit Benutzername und Passwort (nur Einzelplatz-Betrieb). <c>Persistent</c> für die Windows-App.</summary>
public sealed record PasswordLogin(string UserName, string Password, bool Persistent = false);

/// <summary>Eigenes Passwort ändern; nach einem Startpasswort Pflicht.</summary>
public sealed record PasswordChange(string CurrentPassword, string NewPassword);

/// <summary>
/// Passwort vergessen ohne Leitung (nur Einzelplatz, nur am PC mit der Datenbank): Der Wiederherstellungsschlüssel aus der Sicherung
/// weist aus, dass man das Spa verwaltet, und setzt das Passwort eines Kontos neu.
/// </summary>
public sealed record PasswordRecovery(string UserName, string RecoveryCode, string NewPassword);
