using AispaStudio.Api.Scheduling;

namespace AispaStudio.Api.Tests;

/// <summary>Ab wann der Kalender zu einer Vormerkung ohne Antwort erinnert (ADR 0083): nach Termintag, Tagesstart = erste Öffnung.</summary>
public sealed class HoldReminderTests
{
    private static readonly TimeZoneInfo Vienna = TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna");

    private static readonly DateOnly Monday = new(2027, 3, 1);

    private static DateTimeOffset At(DateOnly day, int hour, int minute = 0) =>
        new(TimeZoneInfo.ConvertTimeToUtc(day.ToDateTime(new TimeOnly(hour, minute)), Vienna), TimeSpan.Zero);

    /// <summary>Öffnung um 9:00, sonntags geschlossen.</summary>
    private static DateTimeOffset? OpensAtNine(DateOnly day) => day.DayOfWeek == DayOfWeek.Sunday ? null : At(day, 9);

    private static DateTimeOffset RemindAt(DateTimeOffset held, DateTimeOffset start) => HoldReminder.RemindAt(held, start, Vienna, OpensAtNine);

    [Fact]
    public void Termin_am_selben_Tag_erinnert_nach_einer_Stunde()
    {
        Assert.Equal(At(Monday, 11), RemindAt(At(Monday, 10), At(Monday, 17)));
    }

    [Fact]
    public void Termin_am_selben_Tag_erinnert_spaetestens_eine_Stunde_vor_Beginn_aber_nie_vor_dem_Vormerken()
    {
        Assert.Equal(At(Monday, 14), RemindAt(At(Monday, 13, 30), At(Monday, 15)));
        Assert.Equal(At(Monday, 14, 30), RemindAt(At(Monday, 14, 30), At(Monday, 15)));
    }

    [Theory]
    [InlineData(8, 0)]
    [InlineData(11, 59)]
    public void Termin_morgen_bis_Mittag_vorgemerkt_erinnert_nach_drei_Stunden(int hour, int minute)
    {
        var held = At(Monday, hour, minute);
        Assert.Equal(held.AddHours(3), RemindAt(held, At(Monday.AddDays(1), 17)));
    }

    [Theory]
    [InlineData(12)]
    [InlineData(19)]
    public void Termin_morgen_ab_Mittag_vorgemerkt_erinnert_zum_Tagesstart_des_Termintags(int hour)
    {
        Assert.Equal(At(Monday.AddDays(1), 9), RemindAt(At(Monday, hour), At(Monday.AddDays(1), 17)));
    }

    [Fact]
    public void Termin_spaeter_erinnert_zum_Tagesstart_des_Vortags()
    {
        Assert.Equal(At(Monday.AddDays(3), 9), RemindAt(At(Monday, 10), At(Monday.AddDays(4), 17)));
    }

    [Fact]
    public void Ist_der_Vortag_geschlossen_gilt_8_Uhr()
    {
        // Montag 1.3.2027: der Vortag ist Sonntag (geschlossen).
        Assert.Equal(At(Monday.AddDays(-1), 8), RemindAt(At(Monday.AddDays(-5), 10), At(Monday, 17)));
    }
}
