using AispaStudio.Api.Access;
using AispaStudio.Api.Data;
using AispaStudio.Api.HrFile;
using AispaStudio.Api.Scheduling;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Staff;

/// <summary>
/// Vollmachten, die die API zusätzlich zur Rolle prüft (R05 Slice 111, E11). Inhaber und Manager brauchen nie eine. Die Prüfung greift je Art erst, sobald das Spa eine Vollmacht
/// dieser Art erteilt hat, auch eine abgelaufene; ohne das bliebe in bestehenden Spas niemand übrig, der stornieren darf.
/// </summary>
internal static class StaffPowerCheck
{
    public static string Label(StaffPowerKind kind) => kind switch
    {
        StaffPowerKind.CashVoid => "Kassa-Storno",
        StaffPowerKind.CashClosing => "Kassa-Abschluss",
        StaffPowerKind.Safe => "Tresor",
        StaffPowerKind.BankDeposit => "Bankeinzahlung",
        StaffPowerKind.Keys => "Schlüssel",
        StaffPowerKind.AcceptDeliveries => "Lieferungen annehmen",
        StaffPowerKind.TriggerOrders => "Bestellungen auslösen",
        _ => "Vertretung der Leitung",
    };

    public static bool IsChecked(StaffPowerKind kind) => kind is StaffPowerKind.CashVoid or StaffPowerKind.TriggerOrders;

    /// <summary>Der Grund der Ablehnung oder <c>null</c>, wenn die Vollmacht nicht im Weg steht.</summary>
    public static async Task<string?> DeniedAsync(AispaDbContext db, AccessService access, StaffPowerKind kind, TimeProvider time, CancellationToken ct)
    {
        if (await access.CurrentAsync(ct) is not { } user || user.IsOwnerLevel || !await db.Set<StaffPower>().AnyAsync(p => p.Kind == kind, ct))
        {
            return null;
        }

        var today = LocalTime.LocalDate(time.GetUtcNow(), await HrFileRules.ZoneAsync(db, ct));
        var own = await db.Set<StaffPower>().AsNoTracking().Where(p => p.Kind == kind && p.EmployeeId == user.EmployeeId).ToListAsync(ct);
        if (own.Any(p => StaffRegulationModel.IsValidOn(today, p.ValidFrom, p.ValidUntil)))
        {
            return null;
        }

        var label = Label(kind);
        if (own.Where(p => p.ValidUntil < today).MaxBy(p => p.ValidUntil) is { ValidUntil: { } expired })
        {
            return $"Die Vollmacht „{label}“ ist am {expired:d.M.yyyy} abgelaufen.";
        }

        return own.MinBy(p => p.ValidFrom) is { } future
            ? $"Die Vollmacht „{label}“ gilt erst ab {future.ValidFrom:d.M.yyyy}."
            : $"Für diese Aktion braucht es die Vollmacht „{label}“.";
    }
}
