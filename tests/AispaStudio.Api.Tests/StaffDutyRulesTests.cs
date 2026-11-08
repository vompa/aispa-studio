using System.Globalization;
using AispaStudio.Api.Staff;

namespace AispaStudio.Api.Tests;

/// <summary>R05 Slice 117 (Ä274, E14): Erinnerungstag, Fälligkeit, Erneuerung, Bezeichnung und Mindestzahl mit festen Werten; erwartete Tage von Hand gerechnet.</summary>
public sealed class StaffDutyRulesTests
{
    private static readonly DateOnly NewYearsEve = new(2027, 12, 31);

    /// <summary>Ein Samstag.</summary>
    private static readonly DateOnly Saturday = new(2027, 3, 6);

    private static DateOnly Day(string text) => DateOnly.Parse(text, CultureInfo.InvariantCulture);

    private static StaffCertificate Course(DateOnly? validUntil, int remindDays = 60, string name = "Erste-Hilfe-Kurs", DateOnly? issuedOn = null) =>
        new() { Name = name, ValidUntil = validUntil, RemindDays = remindDays, IssuedOn = issuedOn };

    [Theory]
    [InlineData("2027-12-31", 60, "2027-11-01")]
    [InlineData("2027-12-31", 1, "2027-12-30")]
    [InlineData("2028-03-01", 30, "2028-01-31")]
    public void Erinnerungstag_ist_gueltig_bis_minus_Frist(string validUntil, int days, string expected) =>
        Assert.Equal(Day(expected), StaffDutyRules.ReminderDay(Day(validUntil), days));

    [Theory]
    [InlineData("2027-10-31", false)]
    [InlineData("2027-11-01", true)]
    [InlineData("2028-01-15", true)]
    public void Kurs_bis_31_12_mit_60_Tagen_ist_ab_1_11_faellig_auch_nach_Ablauf(string today, bool due)
    {
        var course = Course(NewYearsEve);

        Assert.Equal(due, StaffDutyRules.IsDue(course, Day(today), [course]));
    }

    [Fact]
    public void Unbefristetes_Zertifikat_ist_nie_faellig()
    {
        var course = Course(null);

        Assert.False(StaffDutyRules.IsDue(course, new DateOnly(2099, 1, 1), [course]));
    }

    [Fact]
    public void Erneuerter_Kurs_mit_spaeterem_Ablauf_ist_nicht_mehr_faellig()
    {
        var old = Course(NewYearsEve);
        var renewed = Course(new DateOnly(2029, 12, 31), name: " erste-hilfe-kurs ");

        Assert.False(StaffDutyRules.IsDue(old, new DateOnly(2027, 12, 1), [old, renewed]));
    }

    [Fact]
    public void Neuerer_Eintrag_mit_frueherem_Ablauf_erneuert_nicht()
    {
        var old = Course(NewYearsEve);
        var earlier = Course(new DateOnly(2027, 6, 30));

        Assert.True(StaffDutyRules.IsDue(old, new DateOnly(2027, 12, 1), [old, earlier]));
    }

    [Theory]
    [InlineData("  erste-hilfe-KURS ", true)]
    [InlineData("Erste Hilfe", false)]
    public void Bezeichnung_gleicht_ohne_Gross_Kleinschreibung_und_Rand(string name, bool matches) =>
        Assert.Equal(matches, StaffDutyRules.Matches(name, "Erste-Hilfe-Kurs"));

    [Theory]
    [InlineData("2027-03-06", true)]
    [InlineData("2027-03-05", false)]
    public void Kurs_erfuellt_die_Zustaendigkeit_nur_bis_zum_Ablauftag(string validUntil, bool satisfied) =>
        Assert.Equal(satisfied, StaffDutyRules.IsSatisfied("Erste-Hilfe-Kurs", [Course(Day(validUntil))], Saturday));

    [Fact]
    public void Kurs_ab_morgen_erfuellt_heute_noch_nicht() =>
        Assert.False(StaffDutyRules.IsSatisfied("Erste-Hilfe-Kurs", [Course(null, issuedOn: Saturday.AddDays(1))], Saturday));

    [Fact]
    public void Ohne_verlangtes_Zertifikat_ist_die_Zustaendigkeit_erfuellt() =>
        Assert.True(StaffDutyRules.IsSatisfied(null, [], Saturday));

    [Theory]
    [InlineData(0, 1, "Keine Ersthelferin im Dienst am Samstag, 6.3.")]
    [InlineData(1, 2, "Nur 1 von 2 Ersthelferinnen im Dienst am Samstag, 6.3.")]
    [InlineData(1, 1, null)]
    [InlineData(0, 0, null)]
    public void Mindestzahl_warnt_nur_bei_zu_wenigen(int onDuty, int minimum, string? expected) =>
        Assert.Equal(expected, StaffDutyRules.ShortageWarning(onDuty, minimum, Saturday));

    [Theory]
    [InlineData("2027-12-31", "Erste-Hilfe-Kurs läuft am 31.12. ab")]
    [InlineData("2028-01-01", "Erste-Hilfe-Kurs ist am 31.12. abgelaufen")]
    public void Text_der_Erinnerung_nennt_den_Ablauftag(string today, string expected) =>
        Assert.Equal(expected, StaffDutyRules.ReminderText("Erste-Hilfe-Kurs", NewYearsEve, Day(today)));
}
