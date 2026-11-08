using AispaStudio.Api.Access;
using AispaStudio.Contracts;

namespace AispaStudio.Api.BusinessInfo;

/// <summary>
/// Wer welchen Eintrag der Betriebsinformationen lesen oder verwalten darf und wo er gilt (ADR 0074). Gemeinsam für die Endpunkte und den Assistenten,
/// damit beide dieselbe Stufe „Team“ oder „Leitung“ anwenden.
/// </summary>
internal static class BusinessContactRules
{
    /// <summary>Stufe „Team“ lesen alle Zugänge, Stufe „Leitung“ nur, wer <see cref="UserAccess.SeesManagementContacts"/> darf.</summary>
    public static bool CanRead(UserAccess access, BusinessContact contact) =>
        contact.Visibility == BusinessContactVisibility.Team || access.SeesManagementContacts;

    /// <summary>Der Eintrag liegt im Bereich der Standorte des Zugangs: er gilt für alle Standorte, oder mindestens einer seiner Standorte ist sichtbar.</summary>
    public static bool InScope(UserAccess access, BusinessContact contact) =>
        access.AllLocations || contact.AllLocations || contact.Locations.Any(l => access.CanSee(l.LocationId));

    /// <summary>Wer nur einzelne Standorte sieht, verwaltet nur Einträge, die ganz in seinen Standorten liegen, und keine für alle Standorte.</summary>
    public static bool CanManage(UserAccess access, BusinessContact contact) =>
        access.AllLocations || (!contact.AllLocations && contact.Locations.All(l => access.CanSee(l.LocationId)));

    /// <summary>Der Eintrag gilt an diesem Standort (für alle Standorte oder ausdrücklich genannt).</summary>
    public static bool AppliesTo(BusinessContact contact, Guid locationId) =>
        contact.AllLocations || contact.Locations.Any(l => l.LocationId == locationId);
}
