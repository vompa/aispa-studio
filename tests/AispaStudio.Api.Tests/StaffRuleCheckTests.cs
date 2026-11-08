using AispaStudio.Api.Staff;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Tests;

/// <summary>R05 Slice 111 (E10): die Regel je Art mit festen Werten; erlaubte und abgelehnte Fälle von Hand.</summary>
public sealed class StaffRuleCheckTests
{
    /// <summary>Ein Dienstag.</summary>
    private static readonly DateOnly Tuesday = new(2027, 3, 2);

    private static StaffRule Rule(StaffRuleKind kind, TimeOnly? latest = null, decimal? hours = null, DayOfWeek[]? days = null, DateOnly? from = null, DateOnly? until = null) => new()
    {
        Kind = kind,
        ValidFrom = from ?? Tuesday.AddDays(-30),
        ValidUntil = until,
        LatestEnd = latest,
        MaxHours = hours,
        DaysOff = [.. days ?? []],
    };

    private static (TimeOnly, TimeOnly) Span(int from, int to) => (new TimeOnly(from, 0), new TimeOnly(to, 0));

    [Theory]
    [InlineData(16, false)]
    [InlineData(17, true)]
    public void Keine_Termine_nach_16_Uhr_gilt_fuer_das_Ende(int end, bool violated)
    {
        var rule = Rule(StaffRuleKind.NoAppointmentsAfter, latest: new TimeOnly(16, 0));

        Assert.Equal(violated, StaffRuleCheck.Violated(rule, Tuesday, [Span(9, end)], hoursThatDay: end - 9));
    }

    [Theory]
    [InlineData(6, false)]
    [InlineData(6.5, true)]
    public void Hoechstens_sechs_Stunden_zaehlen_den_ganzen_Tag(double hours, bool violated)
    {
        var rule = Rule(StaffRuleKind.MaxHoursPerDay, hours: 6m);

        Assert.Equal(violated, StaffRuleCheck.Violated(rule, Tuesday, [Span(9, 10)], (decimal)hours));
    }

    [Theory]
    [InlineData(DayOfWeek.Tuesday, true)]
    [InlineData(DayOfWeek.Wednesday, false)]
    public void Feste_freie_Wochentage(DayOfWeek off, bool violated)
    {
        var rule = Rule(StaffRuleKind.FixedDaysOff, days: [off]);

        Assert.Equal(violated, StaffRuleCheck.Violated(rule, Tuesday, [Span(9, 10)], 1m));
    }

    [Fact]
    public void Schutzfrist_gilt_fuer_jeden_Dienst_im_Zeitraum()
    {
        Assert.True(StaffRuleCheck.Violated(Rule(StaffRuleKind.ProtectionPeriod), Tuesday, [Span(9, 10)], 1m));
    }

    [Fact]
    public void Ohne_Dienst_verstoesst_nichts()
    {
        Assert.False(StaffRuleCheck.Violated(Rule(StaffRuleKind.ProtectionPeriod), Tuesday, [], 0m));
    }

    [Fact]
    public void Der_letzte_Tag_gilt_noch_der_Tag_danach_und_der_Tag_davor_nicht()
    {
        var rule = Rule(StaffRuleKind.ProtectionPeriod, from: Tuesday, until: Tuesday);

        Assert.True(StaffRuleCheck.Violated(rule, Tuesday, [Span(9, 10)], 1m));
        Assert.False(StaffRuleCheck.Violated(rule, Tuesday.AddDays(1), [Span(9, 10)], 1m));
        Assert.False(StaffRuleCheck.Violated(rule, Tuesday.AddDays(-1), [Span(9, 10)], 1m));
    }

    [Fact]
    public void Der_Text_nennt_die_Regelung_und_das_Ende_aber_nie_den_Grund()
    {
        var rule = Rule(StaffRuleKind.NoAppointmentsAfter, latest: new TimeOnly(16, 0), until: new DateOnly(2027, 12, 31));
        rule.Reason = "Kinderbetreuung";

        Assert.Equal("Lena Huber: Sonderregelung „keine Termine nach 16:00“, gültig bis 31.12.2027.", StaffRuleCheck.Message("Lena Huber", [rule]));
    }

    [Theory]
    [InlineData(StaffRuleKind.MaxHoursPerDay, "höchstens 6,5 Stunden pro Tag")]
    [InlineData(StaffRuleKind.FixedDaysOff, "frei am Montag und Mittwoch")]
    [InlineData(StaffRuleKind.ProtectionPeriod, "Schutzfrist")]
    public void Die_Regelung_in_Worten(StaffRuleKind kind, string expected)
    {
        var rule = Rule(kind, hours: 6.5m, days: [DayOfWeek.Wednesday, DayOfWeek.Monday]);

        Assert.Equal(expected, StaffRuleCheck.Summary(rule));
    }
}
