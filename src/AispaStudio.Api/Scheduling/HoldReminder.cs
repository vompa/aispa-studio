namespace AispaStudio.Api.Scheduling;

/// <summary>
/// Ab wann der Kalender zu einer Vormerkung ohne Antwort des Gastes „Gast hat sich nicht gemeldet“ zeigt (ADR 0083, Auftraggeber 29.10.2026).
/// Alles in Ortszeit des Standorts; der Tagesstart ist die erste Öffnung des Tages.
/// </summary>
internal static class HoldReminder
{
    /// <summary>Termin am selben Tag: so lange nach dem Vormerken.</summary>
    public static readonly TimeSpan SameDay = TimeSpan.FromHours(1);

    /// <summary>Termin am selben Tag: spätestens so lange vor Beginn.</summary>
    public static readonly TimeSpan BeforeStart = TimeSpan.FromHours(1);

    /// <summary>Termin am nächsten Tag, vor <see cref="Noon"/> vorgemerkt: so lange danach.</summary>
    public static readonly TimeSpan NextDayMorning = TimeSpan.FromHours(3);

    public static readonly TimeOnly Noon = new(12, 0);

    /// <summary>Tagesstart, wenn der Standort an dem Tag keine Öffnungszeit hat.</summary>
    public static readonly TimeOnly DefaultDayStart = new(8, 0);

    /// <summary>
    /// Termin heute: 1 Stunde nach dem Vormerken, spätestens 1 Stunde vor Beginn. Termin morgen: bis 12 Uhr vorgemerkt 3 Stunden später,
    /// danach zum Tagesstart des Termintags. Termin später: zum Tagesstart des Vortags. Nie vor dem Vormerken.
    /// </summary>
    /// <param name="dayStart">Tagesstart eines Tages (erste Öffnung) als Zeitpunkt, oder <c>null</c>, wenn der Standort dann geschlossen hat.</param>
    public static DateTimeOffset RemindAt(DateTimeOffset heldAt, DateTimeOffset start, TimeZoneInfo zone, Func<DateOnly, DateTimeOffset?> dayStart)
    {
        var heldLocal = TimeZoneInfo.ConvertTime(heldAt, zone);
        var heldDay = DateOnly.FromDateTime(heldLocal.DateTime);
        var startDay = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(start, zone).DateTime);
        DateTimeOffset StartOf(DateOnly day) => dayStart(day) ?? LocalTime.ToUtc(day, DefaultDayStart, zone);

        var remind = (startDay.DayNumber - heldDay.DayNumber) switch
        {
            <= 0 => Min(heldAt + SameDay, start - BeforeStart),
            1 => TimeOnly.FromDateTime(heldLocal.DateTime) < Noon ? heldAt + NextDayMorning : StartOf(startDay),
            _ => StartOf(startDay.AddDays(-1)),
        };
        return remind < heldAt ? heldAt : remind;
    }

    private static DateTimeOffset Min(DateTimeOffset a, DateTimeOffset b) => a < b ? a : b;
}
