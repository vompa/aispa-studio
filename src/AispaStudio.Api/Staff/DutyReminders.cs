using AispaStudio.Api.Access;
using AispaStudio.Api.Data;
using AispaStudio.Api.Roster;
using AispaStudio.Api.Stock;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Staff;

/// <summary>
/// Erinnerungen je Empfänger (R05 Slice 117, Ä274), gerechnet beim Lesen, ohne Hintergrundjob und ohne Mail: Zertifikate vor dem Ablauf an die Person und an Inhaber und Manager;
/// Bestellvorschlag und Ablaufaufgaben eines Standorts an die Person mit heute gültiger Zuständigkeit Lager, ist sie heute abwesend an ihre Vertretung, sonst an die Leitung des Standorts.
/// Die Zuständigkeit gibt keine Rechte: Ob jemand das Lager öffnen darf, entscheidet weiter die Rolle. In der Mitarbeiter-App gibt es nur die eigenen Zertifikate, auch für die Leitung.
/// </summary>
internal static class DutyReminders
{
    public static async Task<List<DutyReminderDto>> ForAsync(
        AispaDbContext db, RosterService roster, StockService stock, UserAccess access, bool ownOnly, Guid? locationId, DateOnly today, CancellationToken ct)
    {
        var reminders = await CertificatesAsync(db, access, !ownOnly && access.Has(AccessRight.ManageHrFile), today, ct);
        if (!ownOnly && locationId is { } location && access.CanSee(location) && await db.Locations.AnyAsync(l => l.Id == location, ct)
            && await IsStockRecipientAsync(db, roster, access, location, today, ct))
        {
            reminders.AddRange(await StockAsync(stock, location, today, ct));
        }

        return reminders;
    }

    private static async Task<List<DutyReminderDto>> CertificatesAsync(AispaDbContext db, UserAccess access, bool leadership, DateOnly today, CancellationToken ct)
    {
        if (!leadership && access.EmployeeId is null)
        {
            return [];
        }

        var query = db.Set<StaffCertificate>().AsNoTracking().Where(c => c.Employee.IsActive);
        if (!leadership)
        {
            query = query.Where(c => c.EmployeeId == access.EmployeeId);
        }

        var certificates = await query.Select(c => new { Certificate = c, Name = c.Employee.PreferredName ?? c.Employee.DisplayName }).ToListAsync(ct);
        var byEmployee = certificates.Select(c => c.Certificate).ToLookup(c => c.EmployeeId);
        return
        [
            .. certificates.Where(c => StaffDutyRules.IsDue(c.Certificate, today, byEmployee[c.Certificate.EmployeeId]))
                .OrderBy(c => c.Certificate.ValidUntil).ThenBy(c => c.Name, StringComparer.Ordinal).ThenBy(c => c.Certificate.Name, StringComparer.Ordinal)
                .Select(c =>
                {
                    var until = c.Certificate.ValidUntil!.Value;
                    var expired = until < today;
                    var text = StaffDutyRules.ReminderText(c.Certificate.Name, until, today);
                    return new DutyReminderDto(
                        expired ? DutyReminderKind.CertificateExpired : DutyReminderKind.CertificateExpiring,
                        expired ? "Zertifikat abgelaufen" : "Zertifikat läuft ab",
                        leadership ? $"{c.Name}: {text}" : text,
                        c.Certificate.EmployeeId,
                        null,
                        until);
                }),
        ];
    }

    /// <summary>
    /// Geht die Lageraufgabe an diese Person? An alle mit heute gültiger Zuständigkeit Lager, eine heute abwesende an ihrer Stelle an ihre Vertretung;
    /// es zählen nur aktive Personen, die am Standort arbeiten; bleibt so niemand, an die Leitung des Standorts (wer den Standort verwalten darf).
    /// </summary>
    private static async Task<bool> IsStockRecipientAsync(AispaDbContext db, RosterService roster, UserAccess access, Guid locationId, DateOnly today, CancellationToken ct)
    {
        var duties = await db.Set<StaffDuty>().AsNoTracking()
            .Where(d => d.DutyType.Key == DutyTypeKey.Stock && d.DutyType.IsActive && d.Employee.IsActive
                && d.ValidFrom <= today && (d.ValidUntil == null || d.ValidUntil >= today))
            .Select(d => new { d.EmployeeId, d.DeputyId })
            .ToListAsync(ct);
        var holderIds = duties.Select(d => d.EmployeeId).Distinct().ToList();
        var holders = await db.Employees.AsNoTracking().Include(e => e.WorkingHours).Where(e => holderIds.Contains(e.Id)).ToListAsync(ct);
        var plans = await roster.PlansAsync(holders, today, today, ct);
        var atLocation = await roster.EmployeesAt(locationId, today).Select(e => e.Id).ToListAsync(ct);
        var recipients = duties
            .Select(d => plans[(d.EmployeeId, today)].Entries.Any(e => e.Kind != RosterKind.Work) ? d.DeputyId : d.EmployeeId)
            .OfType<Guid>()
            .Where(atLocation.Contains)
            .ToHashSet();
        return recipients.Count == 0
            ? access.Has(AccessRight.ManageLocation)
            : access.EmployeeId is { } self && recipients.Contains(self);
    }

    private static async Task<List<DutyReminderDto>> StockAsync(StockService stock, Guid locationId, DateOnly today, CancellationToken ct)
    {
        var reminders = new List<DutyReminderDto>();
        var reorder = (await stock.ReorderSuggestionsAsync(locationId, ct)).Count;
        if (reorder > 0)
        {
            reminders.Add(new(DutyReminderKind.StockReorder, "Bestellvorschlag", $"{reorder} {(reorder == 1 ? "Material" : "Materialien")} nachbestellen", null, locationId, today));
        }

        var expiry = await stock.ExpiryTasksAsync(locationId, ct);
        var batches = expiry.Expiring.Count + expiry.Expired.Count;
        if (batches > 0)
        {
            reminders.Add(new(DutyReminderKind.StockExpiry, "Ablaufdaten", ExpiryText(expiry.Expiring.Count, expiry.Expired.Count), null, locationId, today));
        }

        return reminders;
    }

    private static string ExpiryText(int expiring, int expired) => (expiring, expired) switch
    {
        (_, 0) => $"{expiring} {(expiring == 1 ? "Charge läuft" : "Chargen laufen")} bald ab",
        (0, _) => $"{expired} {(expired == 1 ? "Charge ist" : "Chargen sind")} abgelaufen",
        _ => $"{expiring + expired} Chargen laufen bald ab oder sind abgelaufen",
    };
}
