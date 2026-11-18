using AispaStudio.Api.Data;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Staff;

/// <summary>
/// Gemeinsames der Tabelle „Rechte und Zuständigkeiten“ (Masken-Überarbeitung P11, ADR 0098 Nachtrag): Vollmachten und Zuständigkeiten stehen in denselben Themen,
/// ein Eintrag gilt als besetzt, wenn heute mindestens eine aktive Person ihn gültig hat, und mit einem Standort zählen nur Personen, die dort arbeiten.
/// Die Themen, ihre Reihenfolge, die Hinweise und die Wirkung legt die API fest; die App zeigt nur an.
/// </summary>
internal static class StaffRightsTable
{
    public const string CashAndMoney = "Kassa und Geld";
    public const string Stock = "Lager";
    public const string SafetyAndDeputy = "Sicherheit und Vertretung";
    public const string Other = "Weiteres";

    private const string PostedOnly = "Nur Aushang";

    /// <summary>Die Themen in der Reihenfolge der Tabelle, ab 1.</summary>
    public static int OrderOf(string group) => group switch
    {
        CashAndMoney => 1,
        Stock => 2,
        SafetyAndDeputy => 3,
        _ => 4,
    };

    public static string GroupOf(StaffPowerKind kind) => kind switch
    {
        StaffPowerKind.CashVoid or StaffPowerKind.CashClosing or StaffPowerKind.Safe or StaffPowerKind.BankDeposit => CashAndMoney,
        StaffPowerKind.AcceptDeliveries or StaffPowerKind.TriggerOrders => Stock,
        _ => SafetyAndDeputy,
    };

    public static string HintOf(StaffPowerKind kind) => kind switch
    {
        StaffPowerKind.CashVoid => "Belege stornieren",
        StaffPowerKind.CashClosing => "Tagesabschluss",
        StaffPowerKind.Safe => "Tresor öffnen und befüllen",
        StaffPowerKind.BankDeposit => "Geld zur Bank bringen",
        StaffPowerKind.Keys => "Schlüssel zum Haus",
        StaffPowerKind.AcceptDeliveries => "Lieferungen entgegennehmen",
        StaffPowerKind.TriggerOrders => "Bestellungen an Lieferanten senden",
        _ => "Vertritt die Leitung",
    };

    public static string EffectOf(StaffPowerKind kind) => StaffPowerCheck.IsChecked(kind) ? "Schaltet frei" : PostedOnly;

    /// <summary>Die Vorgaben der Liste ordnen sich ein; eigene Einträge stehen unter „Weiteres“, auch die „Ausbildung“.</summary>
    public static string GroupOf(DutyTypeKey? key) => key switch
    {
        DutyTypeKey.CashResponsibility => CashAndMoney,
        DutyTypeKey.Stock => Stock,
        DutyTypeKey.FirstAid or DutyTypeKey.SafetyRep or DutyTypeKey.FireSafety or DutyTypeKey.Hygiene => SafetyAndDeputy,
        _ => Other,
    };

    /// <summary>Was die App aus der Zuständigkeit macht: der Ersthelfer warnt im Dienstplan, „Lager“ bekommt die Erinnerungen, alles andere ist Aushang.</summary>
    public static string EffectOf(DutyTypeKey? key) => key switch
    {
        DutyTypeKey.FirstAid => "Warnt im Dienstplan",
        DutyTypeKey.Stock => "Erinnert an Bestellungen und Ablauf",
        _ => PostedOnly,
    };

    /// <summary>Die Personen, die am Standort arbeiten (Wochenarbeitszeit); ohne Standort <c>null</c>, also alle.</summary>
    public static async Task<HashSet<Guid>?> PeopleAtAsync(AispaDbContext db, Guid? locationId, CancellationToken ct) =>
        locationId is { } location
            ? [.. await db.Set<Scheduling.EmployeeWorkingHours>().AsNoTracking().Where(h => h.LocationId == location).Select(h => h.EmployeeId).Distinct().ToListAsync(ct)]
            : null;

    /// <summary>Besetzt: eine aktive Person (am Standort, falls gewählt) hat den Eintrag heute gültig.</summary>
    public static bool IsStaffed(IEnumerable<(Guid EmployeeId, bool IsActive, DateOnly ValidFrom, DateOnly? ValidUntil)> holders, HashSet<Guid>? atLocation, DateOnly today) =>
        holders.Any(h => h.IsActive && (atLocation is null || atLocation.Contains(h.EmployeeId)) && h.ValidFrom <= today && (h.ValidUntil is null || h.ValidUntil >= today));
}
