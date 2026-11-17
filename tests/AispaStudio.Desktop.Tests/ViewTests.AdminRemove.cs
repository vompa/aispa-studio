using AispaStudio.Contracts;
using AispaStudio.Desktop.Controls;
using AispaStudio.Desktop.ViewModels;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using static AispaStudio.Desktop.Tests.StockSample;

namespace AispaStudio.Desktop.Tests;

/// <summary>
/// ADR 0099: „Neu“ fest über der Liste, Entfernen im Menü „⋯“ im Kopf der Karte und der gemeinsame Dialog „Löschen oder nicht“ in der Windows-App, am Beispiel von Lagerorten,
/// Lieferanten, Räumen und Geräten. Bilder für das Handbuch, wenn AISPA_SCREENSHOTS gesetzt ist.
/// </summary>
public sealed partial class ViewTests
{
    private static MoreMenuButton MoreButton(Window window, string name) => Named<MoreMenuButton>(window, name);

    private static async Task OpenPlaces(Window window, StockViewModel stock)
    {
        await ClickScrolled(window, "SetupButton");
        await Pump(Settling);
        Assert.True(stock.IsPlacesSetup);
    }

    [Theory]
    [InlineData("hell")]
    [InlineData("dunkel")]
    public Task Lagerort_mit_Bewegungen_zeigt_beim_Loeschen_die_Liste_und_bietet_Nicht_mehr_verwenden(string theme)
    {
        var api = StockApi();
        return Render(theme, "verwaltung-lager-lagerort-loeschen", async (window, main) =>
    {
        var (shell, stock) = await OpenStock(window, main);
        await OpenPlaces(window, stock);
        stock.PlacesAdmin.SelectedRow = stock.PlacesAdmin.Rows.Single(r => r.Id == CabinetId);
        await Pump(Settling);

        // „Neu“ steht fest über der Liste, der Eintrag „Neuer Lagerort“ ist in der Liste nicht zu sehen
        var newButton = Named<Button>(window, "NewPlaceButton");
        Assert.True(newButton.IsEffectivelyVisible);
        Assert.Equal("Neuer Lagerort", newButton.Content);
        Assert.True(TopOf(window, newButton) < TopOf(window, Named<ListBox>(window, "PlaceList")));
        Assert.DoesNotContain(Named<ListBox>(window, "PlaceList").GetVisualDescendants().OfType<ListBoxItem>(), i => i.DataContext is StoragePlaceRow { IsNew: true } && i.IsEffectivelyVisible);

        // das Menü „⋯“ steht im Kopf der Karte, mit Gegenstand im Namen
        var more = MoreButton(window, "PlaceMoreMenu");
        Assert.True(more.IsEffectivelyVisible);
        Assert.Equal("Weitere Aktionen zu Schrank Zeder", AutomationProperties.GetName(more));
        ClickCenter(window, more);
        await Pump(Settling);
        var items = window.GetVisualDescendants().OfType<MenuItem>().Where(i => i.IsEffectivelyVisible).ToList();
        Assert.Equal(["Nicht mehr verwenden", "Löschen …"], items.Select(i => i.Header));
        Assert.Contains("danger", items[1].Classes);
        Press(window, PhysicalKey.Escape, RawInputModifiers.None);
        await Pump(Settling);

        // „Löschen …“: der Dialog nennt, was daran hängt, und bietet den anderen Weg an (nicht rot)
        var asking = stock.PlacesAdmin.DeleteCommand.ExecuteAsync(null);
        await Pump(Settling);
        var dialog = Assert.IsType<ConfirmDialogViewModel>(main.DialogHost.Current);
        Assert.Equal("„Schrank Zeder“ lässt sich nicht löschen", dialog.Title);
        Assert.True(Shows(window, "• 2 Lagerbewegungen"));
        var confirm = DialogButton(window, "ConfirmButton");
        Assert.Equal("Nicht mehr verwenden", confirm.Content);
        Assert.DoesNotContain("danger", confirm.Classes);
        Assert.Equal("Abbrechen", DialogButton(window, "CancelButton").Content);
        SaveShot(window, "verwaltung-lager-lagerort-loeschen-dialog", theme);

        ClickCenter(window, confirm);
        await asking;
        await Pump(Settling);
        Assert.False(main.DialogHost.HasDialog);
        Assert.Equal(1, CallsTo(api, $"PUT /api/stock/locations/{SpaSample.LocationId}/storage-places/{CabinetId}"));
        return shell.AdminPage;
    }, api);
    }

    [Fact]
    public Task Der_Dialog_ohne_Weg_hat_nur_Schliessen_und_der_Fokus_liegt_darauf() => Render("hell", "verwaltung-loeschen-ohne-weg", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        await Pump();
        var asking = main.DialogHost.AskAsync(DeletionDialog.RequestFor("Hauptlager", new DeletionCheckDto(false, "Das Hauptlager gehört zum Standort und lässt sich nicht löschen.", [])));
        await Pump(Settling);

        var card = Dialog(window);
        Assert.Equal("„Hauptlager“ lässt sich nicht löschen", Assert.IsType<ConfirmDialogViewModel>(main.DialogHost.Current).Title);
        Assert.False(card.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "ConfirmButton").IsEffectivelyVisible);
        var close = DialogButton(window, "CancelButton");
        Assert.Equal("Schließen", close.Content);
        Assert.Same(close, Focused(window));

        Press(window, PhysicalKey.Escape, RawInputModifiers.None);
        Assert.Equal(ConfirmChoice.Cancelled, (await asking).Choice);
        return calendar;
    });

    [Fact]
    public Task Lieferanten_haben_Neu_ueber_der_Liste_und_das_Menue_nur_bei_einem_vorhandenen_Lieferanten() => Render("hell", "verwaltung-lager-lieferanten-menue", async (window, main) =>
    {
        var (shell, stock) = await OpenStock(window, main);
        await OpenPlaces(window, stock);
        await ClickScrolled(window, "SuppliersTabButton");
        await Pump(Settling);

        var newButton = Named<Button>(window, "NewSupplierButton");
        Assert.True(TopOf(window, newButton) < TopOf(window, Named<ListBox>(window, "SupplierList")));
        Assert.DoesNotContain(Named<ListBox>(window, "SupplierList").GetVisualDescendants().OfType<ListBoxItem>(), i => i.DataContext is StockSupplierRow { IsNew: true } && i.IsEffectivelyVisible);
        stock.SuppliersAdmin.SelectedRow = stock.SuppliersAdmin.Rows[0];
        await Pump(Settling);
        Assert.True(MoreButton(window, "SupplierMoreMenu").IsEffectivelyVisible);

        ClickCenter(window, newButton);
        await Pump(Settling);
        Assert.Equal("Neuer Lieferant", stock.SuppliersAdmin.HeadingText);
        Assert.False(MoreButton(window, "SupplierMoreMenu").IsEffectivelyVisible);
        return shell.AdminPage;
    }, StockApi());

    [Theory]
    [InlineData("hell")]
    [InlineData("dunkel")]
    public Task Raeume_und_Geraete_haben_Neu_ueber_der_Liste_und_je_Zeile_das_Menue(string theme)
    {
        var resources = SpaSample.Resources with { Equipment = [new EquipmentDto(Guid.NewGuid(), SpaSample.LocationId, "Heizstein-Wärmer")] };
        return Render(theme, "verwaltung-standorte-menue", async (window, main) =>
    {
        var shell = await OpenLocations(window, main);

        var newRoom = Named<ContentControl>(window, "NewRoomRow");
        var rooms = Named<ItemsControl>(window, "RoomList");
        Assert.True(TopOf(window, newRoom) < TopOf(window, rooms));
        var roomMenus = rooms.GetVisualDescendants().OfType<MoreMenuButton>().Where(b => b.IsEffectivelyVisible).ToList();
        Assert.NotEmpty(roomMenus);
        Assert.All(roomMenus, b => Assert.StartsWith("Weitere Aktionen zu ", AutomationProperties.GetName(b), StringComparison.Ordinal));

        var equipment = Named<ItemsControl>(window, "EquipmentList");
        Assert.True(TopOf(window, Named<TextBox>(window, "NewEquipmentField")) < TopOf(window, equipment));
        Assert.Contains(equipment.GetVisualDescendants().OfType<MoreMenuButton>(), b => b.IsEffectivelyVisible);
        roomMenus[0].BringIntoView();
        await Pump(Settling);
        SaveShot(window, "verwaltung-standorte-raeume-menue", theme);
        return shell.AdminPage;
    }, LocationsApi(Api().On($"GET /api/locations/{SpaSample.LocationId}/resources", resources)));
    }
}
