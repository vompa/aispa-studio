using AispaStudio.Api.Scheduling;

namespace AispaStudio.Api.Tests;

public sealed class LocalTimeTests
{
    private static readonly TimeZoneInfo Vienna = LocalTime.Zone("Europe/Vienna");

    [Fact]
    public void Tag_der_Umstellung_auf_Sommerzeit_hat_23_Stunden()
    {
        var (from, until) = LocalTime.Day(new DateOnly(2027, 3, 28), Vienna);

        Assert.Equal(TimeSpan.FromHours(23), until - from);
    }

    [Fact]
    public void Ortszeit_im_Sommer_ist_UTC_plus_2()
    {
        var utc = LocalTime.ToUtc(new DateOnly(2027, 7, 1), new TimeOnly(10, 0), Vienna);

        Assert.Equal(new DateTimeOffset(2027, 7, 1, 8, 0, 0, TimeSpan.Zero), utc);
    }

    [Fact]
    public void Uhrzeit_in_der_Umstellungsluecke_wird_auf_gueltige_Zeit_geschoben()
    {
        var utc = LocalTime.ToUtc(new DateOnly(2027, 3, 28), new TimeOnly(2, 30), Vienna);

        Assert.Equal(new DateTimeOffset(2027, 3, 28, 1, 30, 0, TimeSpan.Zero), utc);
    }
}
