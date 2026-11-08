using AispaStudio.Api.Data;
using AispaStudio.Api.Roster;
using AispaStudio.Api.Scheduling;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Staff;

/// <summary>
/// Ersthelferinnen im Dienstplan (R05 Slice 117, E14): Beim Speichern eines Tages zählt die API am Standort, wer im Stand nach dem Speichern (Entwürfe eingerechnet) Dienst hat und
/// am geplanten Tag gültig Ersthelfer mit gültigem verlangtem Zertifikat ist. Sind es weniger als die Mindestzahl, warnt sie, sperrt aber nie. Das Programm prüft keine Rechtsvorschrift.
/// Die Prüfung greift erst, wenn das Spa mindestens eine Zuständigkeit Ersthelfer eingetragen hat; ohne Pflege bliebe sonst jeder gespeicherte Tag eine Warnung.
/// </summary>
internal static class FirstAidCheck
{
    /// <param name="entries">Die Einträge der gespeicherten Person an diesem Tag nach dem Speichern.</param>
    public static async Task<string?> WarningAsync(
        AispaDbContext db, RosterService roster, Guid locationId, Employee saved, DateOnly date, IReadOnlyList<RosterEntry> entries, CancellationToken ct)
    {
        var firstAid = await db.Set<DutyType>().AsNoTracking().FirstOrDefaultAsync(t => t.Key == DutyTypeKey.FirstAid && t.IsActive, ct);
        if (firstAid is not { MinimumOnDuty: > 0 } || !await db.Set<StaffDuty>().AnyAsync(d => d.DutyTypeId == firstAid.Id, ct))
        {
            return null;
        }

        var others = await roster.EmployeesAt(locationId, date, drafts: true).Where(e => e.Id != saved.Id)
            .AsNoTracking().Include(e => e.WorkingHours).ToListAsync(ct);
        var plans = await roster.PlansAsync([.. others, saved], date, date, ct, drafts: true);
        bool WorksHere(IEnumerable<RosterEntry> day) => day.Any(e => e.Kind == RosterKind.Work && e.LocationId == locationId);

        var savedWorks = WorksHere(entries);
        if (!savedWorks && !WorksHere(plans[(saved.Id, date)].Entries))
        {
            return null;
        }

        var onDuty = others.Where(e => WorksHere(plans[(e.Id, date)].Entries)).Select(e => e.Id).ToList();
        if (savedWorks)
        {
            onDuty.Add(saved.Id);
        }

        if (onDuty.Count == 0)
        {
            return null;
        }

        var holders = await db.Set<StaffDuty>().AsNoTracking()
            .Where(d => d.DutyTypeId == firstAid.Id && onDuty.Contains(d.EmployeeId) && d.ValidFrom <= date && (d.ValidUntil == null || d.ValidUntil >= date))
            .Select(d => d.EmployeeId).Distinct().ToListAsync(ct);
        var certificates = await db.Set<StaffCertificate>().AsNoTracking().Where(c => holders.Contains(c.EmployeeId)).ToListAsync(ct);
        var count = holders.Count(h => StaffDutyRules.IsSatisfied(firstAid.RequiredCertificate, certificates.Where(c => c.EmployeeId == h), date));
        return StaffDutyRules.ShortageWarning(count, firstAid.MinimumOnDuty.Value, date);
    }

    /// <summary>Sonderregelung und Ersthelfer in einer Warnung; ein „Trotzdem speichern“ gilt für beide.</summary>
    public static string? Combine(string? ruleWarning, string? firstAidWarning) =>
        (ruleWarning, firstAidWarning) switch
        {
            (null, null) => null,
            (null, _) => firstAidWarning,
            (_, null) => ruleWarning,
            _ => $"{ruleWarning} {firstAidWarning}",
        };
}
