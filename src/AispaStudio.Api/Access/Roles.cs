using AispaStudio.Contracts;

namespace AispaStudio.Api.Access;

/// <summary>Welche Rolle was darf. Bewusst eine feste Tabelle statt frei konfigurierbarer Rechte: für Spas bis 50 Personen einfacher zu verstehen.</summary>
internal static class Roles
{
    private static readonly AccessRight[] All = Enum.GetValues<AccessRight>();

    /// <summary>
    /// Rechte einer Rolle. <paramref name="amountReports"/> ist das einzeln vergebene Recht „Berichte mit Beträgen“; es wirkt nur bei
    /// Rollen, für die es vergeben werden darf (<see cref="CanBeGranted"/>), und schließt die Berichte ohne Beträge ein.
    /// </summary>
    public static IReadOnlyList<AccessRight> RightsOf(StaffRole role, bool amountReports = false)
    {
        IReadOnlyList<AccessRight> rights = role switch
        {
            StaffRole.Owner or StaffRole.GeneralManager => All,
            StaffRole.Manager =>
            [
                AccessRight.ViewCalendar, AccessRight.Book, AccessRight.ChangeStatus,
                AccessRight.ManageLocation, AccessRight.ManageCatalog, AccessRight.ManageStaff, AccessRight.ViewFinance, AccessRight.SuperviseCash,
                AccessRight.ViewHealthData,
            ],
            StaffRole.Reception => [AccessRight.ViewCalendar, AccessRight.Book, AccessRight.ChangeStatus, AccessRight.ViewHealthData],
            StaffRole.Therapist => [AccessRight.ViewCalendar, AccessRight.ChangeStatus, AccessRight.ViewHealthData],
            StaffRole.Backoffice =>
            [
                AccessRight.ViewFinance, AccessRight.ViewAmountReports, AccessRight.ViewReceipts, AccessRight.ViewStaffRecords,
            ],
            _ => [],
        };

        if (!amountReports || !CanBeGranted(role))
        {
            return rights;
        }

        var granted = new List<AccessRight>(rights);
        foreach (var right in new[] { AccessRight.ViewFinance, AccessRight.ViewAmountReports }.Where(r => !granted.Contains(r)))
        {
            granted.Add(right);
        }

        return granted;
    }

    /// <summary>Ob das Recht „Berichte mit Beträgen“ dieser Rolle einzeln vergeben werden kann: Standortleitung und Rezeption.</summary>
    public static bool CanBeGranted(StaffRole role) => role is StaffRole.Manager or StaffRole.Reception;

    /// <summary>Inhaber und Manager: wer Zugänge dieser Rollen anlegt, ändert oder entfernt, ist allein der Inhaber.</summary>
    public static bool IsOwnerLevel(StaffRole role) => role is StaffRole.Owner or StaffRole.GeneralManager;
}

/// <summary>Zugang der angemeldeten Person für diesen Aufruf.</summary>
/// <param name="AmountReports">Dem Zugang einzeln vergebenes Recht „Berichte mit Beträgen“.</param>
/// <param name="PlainReports">Bei der Rolle Therapeut:in: „Auswertungen ohne Beträge“ im Assistenten sind erlaubt (Standard), sonst abgeschaltet; bei anderen Rollen ohne Wirkung.</param>
public sealed record UserAccess(
    string UserId, StaffRole Role, bool AllLocations, IReadOnlySet<Guid> LocationIds, Guid? EmployeeId, bool AmountReports = false, bool PlainReports = true)
{
    public IReadOnlyList<AccessRight> Rights => Roles.RightsOf(Role, AmountReports);

    public bool Has(AccessRight right) => Rights.Contains(right);

    /// <summary>
    /// Darf der Assistent diesem Zugang die Auswertungen ohne Beträge zeigen (Top-Behandlungen, Auslastung, Termine je Person, Nicht-erschienen, Top-Gäste nach Besuchen)? Mit dem
    /// Berichtsrecht immer, sonst mit dem Kalenderrecht; die Rolle Therapeut:in nur, solange das ihrem Zugang nicht abgeschaltet ist (Ä55).
    /// </summary>
    public bool CanSeePlainReports => Has(AccessRight.ViewFinance) || (Has(AccessRight.ViewCalendar) && (Role != StaffRole.Therapist || PlainReports));

    public bool CanSee(Guid locationId) => AllLocations || LocationIds.Contains(locationId);

    /// <summary>Der Inhaber ist Eigentümer des Kontos; nur er verwaltet Inhaber- und Manager-Zugänge und kündigt Lizenz und Abrechnung.</summary>
    public bool IsOwner => Role == StaffRole.Owner;

    /// <summary>Inhaber oder Manager: dürfen das Recht „Berichte mit Beträgen“ vergeben.</summary>
    public bool IsOwnerLevel => Roles.IsOwnerLevel(Role);

    /// <summary>
    /// Darf Einträge der Betriebsinformationen mit der Stufe „Leitung“ lesen: Inhaber, Manager, Standortleitung (alle mit <see cref="AccessRight.ManageLocation"/>)
    /// und Backoffice, das für Buchhaltung und Lohn alles lesen darf (ADR 0074). Rezeption und Therapeut:innen lesen nur die Stufe „Team“.
    /// </summary>
    public bool SeesManagementContacts => Has(AccessRight.ManageLocation) || Role == StaffRole.Backoffice;

    /// <summary>
    /// Ob diese Person einen Zugang mit dieser Rolle und diesen Standorten vergeben, ändern oder sehen darf:
    /// Inhaber alles; Manager und Standortleitung keine Inhaber- und Manager-Zugänge und nur innerhalb der eigenen Standorte.
    /// </summary>
    public bool CanManage(StaffRole role, bool allLocations, IEnumerable<Guid> locationIds) =>
        Has(AccessRight.ManageStaff)
        && (IsOwner
            || (!Roles.IsOwnerLevel(role) && (AllLocations || (!allLocations && locationIds.All(LocationIds.Contains)))));
}
