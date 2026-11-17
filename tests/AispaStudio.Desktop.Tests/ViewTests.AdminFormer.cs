using AispaStudio.Contracts;
using AispaStudio.Desktop.Controls;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>
/// ADR 0099: „Nicht mehr verwendete zeigen“ unter den Listen der Räume und Geräte; die Zeilen stehen gedämpft mit dem Kennzeichen „Nicht mehr verwendet“ und im Menü „⋯“ nur „Wieder verwenden“.
/// Bilder für das Handbuch, wenn AISPA_SCREENSHOTS gesetzt ist.
/// </summary>
public sealed partial class ViewTests
{
    [Theory]
    [InlineData("hell")]
    [InlineData("dunkel")]
    public Task Raeume_und_Geraete_zeigen_nicht_mehr_verwendete_gedaempft_mit_Kennzeichen_und_nur_Wieder_verwenden(string theme)
    {
        var former = new RoomDto(Guid.NewGuid(), SpaSample.LocationId, "Alter Ruheraum", 10);
        var oldDevice = new EquipmentDto(Guid.NewGuid(), SpaSample.LocationId, "Heizstein-Wärmer");
        var resources = SpaSample.Resources with { Equipment = [new EquipmentDto(Guid.NewGuid(), SpaSample.LocationId, "Liege")] };
        var api = LocationsApi(Api()
            .On($"GET /api/locations/{SpaSample.LocationId}/resources", resources)
            .On($"GET /api/locations/{SpaSample.LocationId}/rooms/former", new List<RoomDto> { former })
            .On($"GET /api/locations/{SpaSample.LocationId}/equipment/former", new List<EquipmentDto> { oldDevice }));
        return Render(theme, "verwaltung-standorte-nicht-mehr-verwendet", async (window, main) =>
    {
        var shell = await OpenLocations(window, main);
        var locations = shell.AdminPage.Locations!;

        // ohne Haken keine Zeilen; das Häkchen steht unter der Liste
        var roomList = Named<ItemsControl>(window, "RoomList");
        var roomBox = Named<CheckBox>(window, "ShowFormerRoomsBox");
        Assert.False(Named<ItemsControl>(window, "FormerRoomList").IsEffectivelyVisible);
        roomBox.BringIntoView();
        await Pump(Settling);
        Assert.True(TopOf(window, roomBox) > TopOf(window, roomList));
        ClickCenter(window, roomBox);
        await locations.FormerLoading;
        await Pump(Settling);

        var formerRooms = Named<ItemsControl>(window, "FormerRoomList");
        Assert.True(formerRooms.IsEffectivelyVisible);
        Assert.Contains(formerRooms.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == "Alter Ruheraum" && t.Classes.Contains("muted"));
        Assert.Contains(formerRooms.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == "Nicht mehr verwendet" && t.IsEffectivelyVisible);

        // im Menü „⋯“ nur „Wieder verwenden“, kein „Löschen …“
        var more = formerRooms.GetVisualDescendants().OfType<MoreMenuButton>().Single();
        Assert.Equal("Weitere Aktionen zu Alter Ruheraum", AutomationProperties.GetName(more));
        more.BringIntoView();
        await Pump(Settling);
        ClickCenter(window, more);
        await Pump(Settling);
        var items = window.GetVisualDescendants().OfType<MenuItem>().Where(i => i.IsEffectivelyVisible).ToList();
        Assert.Equal(["Wieder verwenden"], items.Select(i => i.Header));
        Press(window, PhysicalKey.Escape, RawInputModifiers.None);
        await Pump(Settling);

        // Geräte genauso
        locations.ShowFormerEquipment = true;
        await locations.FormerLoading;
        await Pump(Settling);
        var formerEquipment = Named<ItemsControl>(window, "FormerEquipmentList");
        Assert.True(formerEquipment.IsEffectivelyVisible);
        Assert.Contains(formerEquipment.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == "Heizstein-Wärmer" && t.Classes.Contains("muted"));
        // Bilder für das Handbuch: Zeiger weg vom Menü (sonst steht dessen Tooltip im Bild), Häkchen und Zeilen im Bild
        window.MouseMove(new Point(4, 4));
        window.GetVisualDescendants().OfType<TextBlock>().First(t => t.Text == "Tagesstart-Kontrolle").BringIntoView();
        await Pump(Settling);
        SaveShot(window, "verwaltung-standorte-raeume-nicht-mehr-verwendet", theme);
        Named<CheckBox>(window, "ShowFormerEquipmentBox").BringIntoView();
        await Pump(Settling);
        SaveShot(window, "verwaltung-standorte-geraete-nicht-mehr-verwendet", theme);
        return shell.AdminPage;
    }, api);
    }
}
