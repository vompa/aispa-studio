using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>Was der Bereich „Team“ geladen hat: Personen (aktiv und ausgeschieden), Qualifikationen, Standorte, Zugänge und offene Einladungen.</summary>
public sealed record TeamData(
    IReadOnlyList<EmployeeDto> Employees,
    IReadOnlyList<EmployeeDto> Former,
    IReadOnlyList<QualificationDto> Qualifications,
    IReadOnlyList<LocationDto> Locations,
    IReadOnlyList<StaffMemberDto> Members,
    IReadOnlyList<InvitationDto> Invitations);

/// <summary>
/// Gemeinsames der Personenkarten (R05 Slice 98): API, angemeldete Person mit ihren Rechten, Zeitzone, Einzelplatz und die geladenen Daten. Die Rechte blenden
/// nur aus; geprüft wird in der API.
/// </summary>
public sealed class TeamContext(IWorkstationApi api, CurrentUser user, TimeProvider time, bool standalone)
{
    public IWorkstationApi Api => api;

    public CurrentUser User => user;

    public TimeProvider Time => time;

    public TimeZoneInfo Zone => time.LocalTimeZone;

    /// <summary>Einzelplatz (ADR 0041): Zugänge mit Benutzername und Startpasswort statt Einladung.</summary>
    public bool Standalone => standalone;

    public TeamData Data { get; set; } = new([], [], [], [], [], []);

    public IConfirmDialogs? Dialogs { get; set; }

    /// <summary>Stammdaten und Qualifikationen pflegen.</summary>
    public bool CanCatalog => user.Rights.Contains(AccessRight.ManageCatalog);

    /// <summary>Zugänge verwalten und einladen.</summary>
    public bool CanStaff => user.Rights.Contains(AccessRight.ManageStaff);

    /// <summary>Dienstplan lesen: Kalender sehen oder Personalunterlagen lesen (wie die API an <c>GET …/roster</c>).</summary>
    public bool CanViewRoster => user.Rights.Contains(AccessRight.ViewCalendar) || user.Rights.Contains(AccessRight.ViewStaffRecords);

    /// <summary>Dienstplan einer einzelnen Person lesen (Reiter der Karte): Standorte verwalten oder Personalunterlagen lesen (wie die API an <c>GET …/roster?employeeId=</c>).</summary>
    public bool CanViewPersonRoster => user.Rights.Contains(AccessRight.ManageLocation) || user.Rights.Contains(AccessRight.ViewStaffRecords);

    /// <summary>Zeiten lesen: Standorte verwalten oder Personalunterlagen lesen (wie die API an <c>GET …/timesheet</c>); ändern darf nur <see cref="CanPlan"/>.</summary>
    public bool CanViewTimes => user.Rights.Contains(AccessRight.ManageLocation) || user.Rights.Contains(AccessRight.ViewStaffRecords);

    /// <summary>Dienste und Abwesenheiten planen, Entwürfe veröffentlichen, Anträge entscheiden (ManageLocation; der Server prüft es ebenfalls).</summary>
    public bool CanPlan => user.Rights.Contains(AccessRight.ManageLocation);

    /// <summary>Lohn lesen und exportieren: die Organisation verwalten oder Personalunterlagen lesen (wie die API an <c>GET api/payroll/…</c>).</summary>
    public bool CanViewPayroll => user.Rights.Contains(AccessRight.ManageOrganization) || user.Rights.Contains(AccessRight.ViewStaffRecords);

    /// <summary>Personalnummern und Einstellungen der Lohnverrechnung ändern (wie die API an <c>PUT api/payroll/…</c>).</summary>
    public bool CanManagePayroll => user.Rights.Contains(AccessRight.ManageOrganization);

    /// <summary>Personalakte sehen und ändern: nur Inhaber und Manager (wie die API an <c>api/employees/{id}/hr-file</c>).</summary>
    public bool CanHrFile => user.Rights.Contains(AccessRight.ManageHrFile);

    /// <summary>Kennzeichen je Person (R05 Slice 111); nur mit dem Recht auf die Personalakte geladen.</summary>
    public IReadOnlyDictionary<Guid, StaffFlagsDto> Flags { get; set; } = new Dictionary<Guid, StaffFlagsDto>();

    /// <summary>Ersatz für Termine außerhalb eines Dienstes vorschlagen und übernehmen.</summary>
    public bool CanBook => user.Rights.Contains(AccessRight.Book);

    /// <summary>Die Personenliste und die Karten sehen: Stammdaten pflegen oder Zugänge verwalten.</summary>
    public bool CanPeople => CanCatalog || CanStaff;

    public bool IsOwner => user.Access?.Role == StaffRole.Owner;

    /// <summary>Inhaber und Manager: vergeben „Berichte mit Beträgen“ und sehen den Verlauf von Zugang und Dienstplan (wie die API).</summary>
    public bool IsOwnerLevel => user.Access?.Role is StaffRole.Owner or StaffRole.GeneralManager;

    /// <summary>Nur wer selbst für alle Standorte arbeitet, vergibt „Alle Standorte, auch künftige“.</summary>
    public bool CanAllLocations => user.Access?.AllLocations == true;

    public DateTimeOffset Now => time.GetUtcNow();

    /// <summary>Die Zeitzone eines Standorts (<see cref="LocationDto.TimeZone"/>); ohne bekannten Standort oder Eintrag die des Rechners.</summary>
    public TimeZoneInfo ZoneAt(Guid? locationId)
    {
        var name = Data.Locations.FirstOrDefault(l => l.Id == locationId)?.TimeZone;
        try
        {
            return name is null ? Zone : TimeZoneInfo.FindSystemTimeZoneById(name);
        }
        catch (TimeZoneNotFoundException)
        {
            return Zone;
        }
    }

    public DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(Now, Zone).DateTime);

    /// <summary>Letzter gültiger Tag → Ende der Befristung: Mitternacht danach in der Zeitzone des Spa (wie <c>expiresAt</c> im Web).</summary>
    public DateTimeOffset? ExpiresAt(DateOnly? until)
    {
        if (until is not { } day)
        {
            return null;
        }

        var midnight = day.AddDays(1).ToDateTime(TimeOnly.MinValue);
        return new DateTimeOffset(midnight, Zone.GetUtcOffset(midnight));
    }

    /// <summary>Ende der Befristung → letzter gültiger Tag.</summary>
    public DateOnly? LastDay(DateTimeOffset? expires) =>
        expires is { } end ? DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(end.AddTicks(-1), Zone).DateTime) : null;

    /// <summary>Datum im Format der Oberfläche, z. B. „6.11.2026“.</summary>
    public string DateText(DateTimeOffset at) => TimeZoneInfo.ConvertTime(at, Zone).ToString("d.M.yyyy", System.Globalization.CultureInfo.InvariantCulture);
}
