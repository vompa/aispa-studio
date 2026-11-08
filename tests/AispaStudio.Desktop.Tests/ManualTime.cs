namespace AispaStudio.Desktop.Tests;

/// <summary>Uhr zum Weiterstellen, in Wiener Ortszeit.</summary>
internal sealed class ManualTime(DateTimeOffset now) : TimeProvider
{
    private DateTimeOffset _now = now;

    public override DateTimeOffset GetUtcNow() => _now;

    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna");

    public void Advance(TimeSpan by) => _now += by;
}
