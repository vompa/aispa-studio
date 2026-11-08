using AispaStudio.Contracts;
using AispaStudio.Desktop.ViewModels;

namespace AispaStudio.Desktop.Tests;

/// <summary>Rufname („Name im Haus“) für Mitarbeitende (ADR 0064, Nachtrag): Spaltenkopf, Kreis mit Initialen, Hover-Karte und Raumspalten im Kalender.</summary>
public sealed class EmployeePreferredNameTests
{
    private static readonly TimeZoneInfo Vienna = TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna");

    private static readonly EmployeeDto Siriporn = TestData.Anna with { DisplayName = "Siriporn Kaewdee", PreferredName = "Siri" };

    private static LocationResources ResourcesWith(EmployeeDto anna) => TestData.Resources with { Employees = [anna, TestData.Ben] };

    [Fact]
    public void Namensregel_nimmt_den_Rufnamen_sonst_den_vollen_Namen()
    {
        Assert.Equal("Siri", Siriporn.InHouseName());
        Assert.Equal("Siri", EmployeeNames.InHouse("Siriporn Kaewdee", "  Siri "));
        Assert.Equal("Siriporn Kaewdee", EmployeeNames.InHouse("Siriporn Kaewdee", null));
        Assert.Equal("Siriporn Kaewdee", EmployeeNames.InHouse("Siriporn Kaewdee", "   "));
    }

    [Fact]
    public void Spaltenkopf_und_Initialen_nutzen_den_Rufnamen()
    {
        var appointment = TestData.Appointment(10, Siriporn);

        var column = DayLayout.Build(TestData.Day(appointment), ResourcesWith(Siriporn), byRoom: false, Vienna).Columns.Single(c => c.Id == Siriporn.Id);
        var withoutNickname = DayLayout.Build(TestData.Day(appointment), ResourcesWith(Siriporn with { PreferredName = null }), byRoom: false, Vienna)
            .Columns.Single(c => c.Id == Siriporn.Id);

        Assert.Equal("Siri", column.Title);
        Assert.Equal("SI", column.Initials);
        Assert.Equal("Siriporn Kaewdee", withoutNickname.Title);
        Assert.Equal("SK", withoutNickname.Initials);
    }

    [Fact]
    public void Hover_und_Raumspalte_zeigen_den_Rufnamen()
    {
        var appointment = TestData.Appointment(10, Siriporn);

        var byPerson = DayLayout.Build(TestData.Day(appointment), ResourcesWith(Siriporn), byRoom: false, Vienna).Columns.Single(c => c.Id == Siriporn.Id).Items.Single();
        var byRoom = DayLayout.Build(TestData.Day(appointment), ResourcesWith(Siriporn), byRoom: true, Vienna).Columns.SelectMany(c => c.Items).Single();

        Assert.Contains("· Siri ·", byPerson.Hover!.When, StringComparison.Ordinal);
        Assert.DoesNotContain("Siriporn", byPerson.HoverText, StringComparison.Ordinal);
        Assert.Equal("Siri", byRoom.Who);
        Assert.Equal("SI", byRoom.WhoInitials);
    }
}
