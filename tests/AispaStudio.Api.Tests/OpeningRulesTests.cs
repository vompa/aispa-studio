using AispaStudio.Contracts;

namespace AispaStudio.Api.Tests;

/// <summary>
/// Die gemeinsame Regel für „offen, Schließtag oder geschlossen“ (Slice 78): Server (Raumplaner) und Windows-App rechnen mit derselben Funktion.
/// Die erwarteten Werte sind von Hand gesetzt: Öffnungszeit 9 bis 20 Uhr, Zeiten in UTC.
/// </summary>
public sealed class OpeningRulesTests
{
    private static DateTimeOffset At(int hour, int minute = 0) => new(2027, 3, 1, hour, minute, 0, TimeSpan.Zero);

    private static readonly TimeRange[] Opening = [new(At(9), At(20))];

    private static readonly ClosedRange Afternoon = new(At(13), At(23, 59), "Technische Störung");

    [Fact]
    public void Ein_halber_Tag_Schliesszeit_laesst_nur_den_Vormittag_offen()
    {
        var windows = OpeningRules.Windows(Opening, [Afternoon]);

        Assert.Equal([new TimeRange(At(9), At(13))], windows);
    }

    [Fact]
    public void Eine_Schliesszeit_mitten_am_Tag_teilt_das_Fenster()
    {
        var windows = OpeningRules.Windows(Opening, [new ClosedRange(At(12), At(14), "Betriebsurlaub")]);

        Assert.Equal([new TimeRange(At(9), At(12)), new TimeRange(At(14), At(20))], windows);
    }

    [Fact]
    public void Ein_Termin_ueber_den_Beginn_der_Schliesszeit_ist_nicht_offen()
    {
        var windows = OpeningRules.Windows(Opening, [Afternoon]);

        Assert.True(OpeningRules.IsOpen(windows, At(12), At(13)));
        Assert.False(OpeningRules.IsOpen(windows, At(12, 30), At(13, 30)));
        Assert.Equal(OpeningState.Closed, OpeningRules.Check(windows, anyShift: true, house: true, At(12, 30), At(13, 30)));
    }

    [Fact]
    public void Ohne_Oeffnungsfenster_ist_es_ein_Schliesstag_fuer_die_Rezeption_und_geschlossen_fuer_Gaeste()
    {
        var windows = OpeningRules.Windows(Opening, [new ClosedRange(At(0), At(23, 59), "Betriebsurlaub")]);

        Assert.Empty(windows);
        Assert.Equal(OpeningState.ClosedDay, OpeningRules.Check(windows, anyShift: true, house: true, At(10), At(11)));
        Assert.Equal(OpeningState.Closed, OpeningRules.Check(windows, anyShift: true, house: false, At(10), At(11)));
    }

    [Fact]
    public void Ohne_Dienst_in_der_Oeffnungszeit_ist_es_ein_Schliesstag_ausserhalb_bleibt_es_geschlossen()
    {
        var windows = OpeningRules.Windows(Opening, []);

        Assert.Equal(OpeningState.ClosedDay, OpeningRules.Check(windows, anyShift: false, house: true, At(10), At(11)));
        Assert.Equal(OpeningState.Closed, OpeningRules.Check(windows, anyShift: false, house: true, At(3), At(4)));
        Assert.Equal(OpeningState.Open, OpeningRules.Check(windows, anyShift: true, house: true, At(10), At(11)));
    }

    [Fact]
    public void Die_Meldung_nennt_die_Art_der_Schliesszeit_sonst_die_Oeffnungszeit()
    {
        Assert.Equal("Der Standort ist in dieser Zeit geschlossen (Technische Störung).", OpeningRules.ClosedMessage([Afternoon], At(15), At(16)));
        Assert.Equal("Der Standort hat zu dieser Zeit geschlossen.", OpeningRules.ClosedMessage([Afternoon], At(3), At(4)));
    }
}
