using AispaStudio.Api.Data;
using AispaStudio.Api.Scheduling;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.HrFile;

/// <summary>Fristen der Personalakte (R05 Slice 110, E9); ein Tag zählt in der Ortszeit des Spas.</summary>
internal static class HrFileRules
{
    /// <summary>Vorgabe für das Ablaufdatum einer Verwarnung; die Frist ist arbeitsrechtlich abzuklären.</summary>
    public const int DefaultWarningYears = 2;

    private const string FallbackZone = "Europe/Vienna";

    public static DateOnly DefaultExpiry(DateOnly issuedOn) => issuedOn.AddYears(DefaultWarningYears);

    public static bool IsFollowUpDue(DateOnly followUpOn, DateTimeOffset now, TimeZoneInfo zone) => followUpOn <= LocalTime.LocalDate(now, zone);

    /// <summary>Das Ablaufdatum ist der letzte Tag der Verwarnung.</summary>
    public static bool IsExpired(DateOnly expiresOn, DateTimeOffset now, TimeZoneInfo zone) => expiresOn < LocalTime.LocalDate(now, zone);

    /// <summary>Die Akte gilt fürs ganze Spa, nicht je Standort: die Zeitzone des ersten Standorts.</summary>
    public static async Task<TimeZoneInfo> ZoneAsync(AispaDbContext db, CancellationToken ct) =>
        LocalTime.Zone(await db.Locations.AsNoTracking().OrderBy(l => l.Name).Select(l => l.TimeZone).FirstOrDefaultAsync(ct) ?? FallbackZone);
}
