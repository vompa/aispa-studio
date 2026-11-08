namespace AispaStudio.Contracts;

/// <summary>
/// Über welchen Weg eine Anfrage kommt (Slice 104). Der Arbeitsplatz (Windows-App) hat die Rechte der Rolle; die anderen Kanäle erreichen nur
/// Endpunkte, die für sie freigegeben sind, und die Mitarbeiter-App nur eigene Daten.
/// </summary>
public enum AccessChannel
{
    Workstation,
    Display,
    StaffApp,
    CustomerApp,
}

/// <summary>Code, den eine neue Anzeige auf dem Bildschirm zeigt; mit <paramref name="Secret"/> fragt nur die Anzeige selbst nach, ob sie gekoppelt ist.</summary>
public sealed record DisplayPairingStart(string Code, string Secret, DateTimeOffset ExpiresAt);

public sealed record DisplayPairingRedeem(string Secret);

/// <summary>Die Leitung koppelt in der Windows-App (Seite Geräte) den Code einer Anzeige an einen Standort.</summary>
public sealed record DisplayPairInput(string Code, string Name, Guid LocationId);

/// <summary>Gekoppelte Anzeige, wie die Seite Geräte sie listet.</summary>
public sealed record DisplayDeviceDto(Guid Id, string Name, Guid LocationId, DateTimeOffset PairedAt, DateTimeOffset? LastSeenAt);

/// <summary>Was eine angemeldete Anzeige über sich erfährt; nach dem Widerruf antwortet die API mit 401.</summary>
public sealed record DisplaySession(Guid DeviceId, string Name, Guid LocationId, string LocationName);
