using AispaStudio.Contracts;
using AispaStudio.Desktop.ViewModels;

namespace AispaStudio.Desktop.Tests;

/// <summary>Gekaufte Pakete im Kalender: Rest in der Hover-Karte, Kennzeichen nur wenn aufgebraucht oder bald abgelaufen.</summary>
public sealed class PackageCalendarTests
{
    private static readonly Guid Massage = Guid.NewGuid();
    private static readonly DateOnly Day = new(2027, 3, 1);

    private static CalendarGuest Guest(params GuestPackage[] packages) => new(Guid.NewGuid(), 4, 4, 0, null, null, Packages: packages);

    [Fact]
    public void Hover_nennt_den_Rest_und_ein_Kennzeichen_gibt_es_erst_am_Ende()
    {
        var plenty = Guest(new GuestPackage(Massage, "Massage", 10, 7, null));
        var last = Guest(new GuestPackage(Massage, "Massage", 10, 1, null));
        var expiring = Guest(new GuestPackage(Massage, "Massage", 10, 4, Day.AddDays(20)));

        Assert.Equal("Paket Massage: 7 von 10 offen", DayLayout.PackageText(plenty, Massage));
        Assert.Null(DayLayout.PackageText(plenty, Guid.NewGuid()));
        Assert.Null(DayLayout.PackageFlag(plenty, Massage, Day));
        Assert.Contains("letzte Behandlung", DayLayout.PackageFlag(last, Massage, Day)!.Tip, StringComparison.Ordinal);
        Assert.Equal(("P", GuestFlagKind.Package), (DayLayout.PackageFlag(expiring, Massage, Day)!.Symbol, DayLayout.PackageFlag(expiring, Massage, Day)!.Kind));
        Assert.Equal("Paket Massage: 4 von 10 offen, gültig bis 21.03.2027", DayLayout.PackageText(expiring, Massage));
    }
}
