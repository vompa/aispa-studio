namespace AispaStudio.Api.Scheduling;

/// <summary>Umrechnung zwischen UTC und der Ortszeit eines Standorts (inkl. Sommerzeit).</summary>
internal static class LocalTime
{
    public static TimeZoneInfo Zone(string timeZoneId) => TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);

    public static bool IsKnownZone(string timeZoneId) => TimeZoneInfo.TryFindSystemTimeZoneById(timeZoneId, out _);

    public static DateTimeOffset ToUtc(DateOnly date, TimeOnly time, TimeZoneInfo zone)
    {
        var local = date.ToDateTime(time, DateTimeKind.Unspecified);
        if (zone.IsInvalidTime(local))
        {
            // Uhrzeit fällt in die Zeitumstellung (z. B. 02:30 im März): auf die nächste gültige Zeit schieben.
            local = local.AddHours(1);
        }

        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, zone), TimeSpan.Zero);
    }

    public static DateOnly LocalDate(DateTimeOffset instant, TimeZoneInfo zone) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, zone).DateTime);

    /// <summary>Beginn und Ende (exklusiv) eines Kalendertags in UTC; an Tagen der Zeitumstellung 23 bzw. 25 Stunden lang.</summary>
    public static (DateTimeOffset From, DateTimeOffset Until) Day(DateOnly date, TimeZoneInfo zone) =>
        (ToUtc(date, TimeOnly.MinValue, zone), ToUtc(date.AddDays(1), TimeOnly.MinValue, zone));
}
