using AispaStudio.Contracts;
using AispaStudio.Desktop.ViewModels;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>
/// Raumansicht (Ä65): dieselbe Spaltenbreite und Kopfhöhe wie die Teamansicht, und Raumsperren (ADR 0076) in beiden Farbschemata: schraffierte gesperrte Zeit mit Kapsel
/// (Art und Grund), Zeile 2 und Hinweis im Kopf, „n Termine umbuchen“ im Kopf, Meldung „n Termine im gesperrten Raum“ mit dem Knopf „Umbuchen …“.
/// </summary>
public sealed partial class ViewTests
{
    private static readonly Guid RoomClosureBandId = Guid.Parse("00000000-0000-0000-0000-00000000c1b0");

    /// <summary>Wartung des Raums Lotus ab Montag, 19.10., 13:00 bis Dienstag, 20.10., Ende.</summary>
    private static RoomClosureBand LotusMaintenance(int open = 4) =>
        new(RoomClosureBandId, SpaSample.Lotus.Id, SpaSample.Lotus.Name, SpaSample.At(13), ClosureMidnight.AddDays(2), RoomClosureKind.Maintenance, "Wartung", "Lüftung wird gewartet", open, true);

    private static StubApi RoomClosureApi(RoomClosureBand band) =>
        Api().On($"GET /api/locations/{SpaSample.LocationId}/calendar?date=2026-10-19", SpaSample.CalendarDay() with { RoomClosures = [band], RoomClosuresDue = [band] });

    /// <summary>Beispiel-Spa mit neun statt sechs Räumen: mehr Räume als Personen, wie beim Auftraggeber.</summary>
    private static StubApi NineRoomsApi() => Api().On($"GET /api/locations/{SpaSample.LocationId}/resources", SpaSample.Resources with
    {
        Rooms = [.. SpaSample.Resources.Rooms, new RoomDto(Guid.NewGuid(), SpaSample.LocationId, "Hand- und Fußpflegeplatz", 10),
            new RoomDto(Guid.NewGuid(), SpaSample.LocationId, "Ritual-Suite mit Dusche", 15, Places: 2), new RoomDto(Guid.NewGuid(), SpaSample.LocationId, "Raum Alpenrose", 10)],
    });

    private static List<Grid> ColumnHeaders(Window window) =>
        [.. window.GetVisualDescendants().OfType<Grid>().Where(g => g.DataContext is CalendarColumn && g.Classes.Contains("filterable") && g.IsEffectivelyVisible && g.Height == 38)];

    [Fact]
    public Task Raumansicht_hat_in_Pixeln_dieselbe_Spaltenbreite_und_Kopfhoehe_wie_die_Teamansicht() => Render("hell", "kalender-raeume-spaltenbreite", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        await Pump(Settling);
        var team = ColumnHeaders(window);
        var teamBody = window.GetVisualDescendants().OfType<Panel>().Where(p => p.DataContext is CalendarColumn && p.Height == calendar.DayHeight && p.IsEffectivelyVisible).ToList();
        Assert.Equal(6, team.Count);

        calendar.ByRoom = true;
        await Pump(Settling);

        var rooms = ColumnHeaders(window);
        var roomBody = window.GetVisualDescendants().OfType<Panel>().Where(p => p.DataContext is CalendarColumn && p.Height == calendar.DayHeight && p.IsEffectivelyVisible).ToList();
        // Neun Räume und die Bereiche Sauna und Ruheraum (Slice 77).
        Assert.Equal(11, rooms.Count);
        // Gleiche Breite und gleiche Höhe der Köpfe, gleiche Breite der Spalten darunter und derselbe Abstand dazwischen.
        Assert.Equal(team[0].Bounds.Width, rooms[0].Bounds.Width, 0.5);
        Assert.All(rooms, h => Assert.Equal(team[0].Bounds.Width, h.Bounds.Width, 0.5));
        Assert.All(rooms, h => Assert.Equal(team[0].Bounds.Height, h.Bounds.Height, 0.5));
        Assert.Equal(teamBody[0].Bounds.Width, roomBody[0].Bounds.Width, 0.5);
        Assert.Equal(teamBody[1].Bounds.X - teamBody[0].Bounds.X, roomBody[1].Bounds.X - roomBody[0].Bounds.X, 0.5);
        // Mehr Räume als in das Fenster passen: waagrecht scrollen, nicht stauchen.
        Assert.True(calendar.ColumnsWidth > 11 * 160);
        return calendar;
    }, NineRoomsApi());

    [Theory]
    [InlineData("hell")]
    [InlineData("dunkel")]
    public Task Der_gesperrte_Raum_ist_schraffiert_mit_Kapsel_und_Kopf_und_Termine_darin_sind_markiert(string theme) => Render(theme, "kalender-raumsperre", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        calendar.ByRoom = true;
        await Pump(Settling);

        var column = calendar.Columns.Single(c => c.Id == SpaSample.Lotus.Id);
        Assert.Equal("gesperrt: Wartung ab 13:00", column.Subtitle);
        var label = window.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Classes.Contains("closurelabel") && t.IsEffectivelyVisible);
        Assert.Equal("Wartung · Lüftung wird gewartet", label.Text);
        // Kapsel wie beim Schließband, Art „Wartung“ in der Farbe Akzent; nicht anklickbar, die Termine darunter bleiben bedienbar.
        var box = label.GetVisualAncestors().OfType<Border>().First(b => b.Classes.Contains("closurelabelbox"));
        Assert.Equal(new Avalonia.CornerRadius(999), box.CornerRadius);
        Assert.Equal(ColorOf(window, "AccentSoftBrush"), ColorOf(box.Background));
        Assert.Contains(label.GetVisualAncestors().OfType<ItemsControl>(), c => !c.IsHitTestVisible);
        Assert.True(Contrast(ColorOf(window, "AccentBrush"), ColorOf(window, "AccentSoftBrush")) >= 4.5);

        // Kopf wie bei einer abwesenden Person: mit offenen Terminen steht in Zeile 2 der Weg zur Umbuchung (das Warnsymbol in Zeile 1 fasst die Meldung zusammen),
        // sonst Art und Zeitraum; der Hinweis nennt den Grund und bleibt bei höchstens fünf Zeilen.
        var header = ColumnHeaders(window).Single(h => ((CalendarColumn)h.DataContext!).Id == SpaSample.Lotus.Id);
        Assert.Contains(header.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == "4 Termine umbuchen" && t.IsEffectivelyVisible);
        Assert.Contains(header.GetVisualDescendants().OfType<Button>(), b => b.IsEffectivelyVisible && b.Classes.Contains("ghost") && b.DataContext is CalendarColumn { HasAlert: true });
        Assert.Contains("Lüftung wird gewartet", AutomationProperties.GetName(header), StringComparison.Ordinal);
        Assert.True(column.HeaderTipLines.Count <= 5);

        // Termine im gesperrten Raum tragen den Hinweis „umbuchen“, der Termin davor nicht.
        Assert.Equal(4, column.Items.Count(i => i.IsAffected));
        Assert.Contains(column.Items, i => !i.IsAffected && i.Appointment is not null);
        return calendar;
    }, RoomClosureApi(LotusMaintenance()));

    [Fact]
    public Task In_der_Teamansicht_tragen_Termine_im_gesperrten_Raum_den_Hinweis() => Render("hell", "kalender-raumsperre-team", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        await Pump(Settling);

        var affected = calendar.Columns.SelectMany(c => c.Items).Where(i => i.IsAffected).ToList();
        Assert.Equal(4, affected.Count);
        Assert.All(affected, i => Assert.Equal(SpaSample.Lotus.Id, i.Appointment!.RoomId));
        Assert.All(affected, i => Assert.Contains("Raum Lotus ist gesperrt (Wartung · Lüftung wird gewartet): bitte umbuchen", i.Hover!.Alerts));
        Assert.Contains(window.GetVisualDescendants().OfType<Border>(), b => b.Classes.Contains("affected") && b.IsEffectivelyVisible);
        return calendar;
    }, RoomClosureApi(LotusMaintenance()));

    [Theory]
    [InlineData("hell")]
    [InlineData("dunkel")]
    public Task Meldungen_zeigen_n_Termine_im_gesperrten_Raum_mit_dem_Knopf_Umbuchen(string theme) => Render(theme, "meldung-raumsperre", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        calendar.OpenMessagesCommand.Execute(null);
        await Pump(Settling);

        var message = Assert.Single(calendar.Messages, m => m.Kind == MessageKind.RoomClosure);
        Assert.Equal("Raum Lotus ist gesperrt (Wartung, Mo. 19.10. 13:00 Uhr bis Di. 20.10.): 4 Termine umbuchen.", message.Text);
        var action = window.GetVisualDescendants().OfType<Button>().Single(b => b.IsEffectivelyVisible && b.Content as string == "Umbuchen …");
        Assert.True(action.IsEnabled);
        Assert.Contains(window.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == "Raumsperre" && t.IsEffectivelyVisible);
        return calendar;
    }, RoomClosureApi(LotusMaintenance()));

    [Fact]
    public Task Die_Umbuchungshilfe_zeigt_Vorschlaege_fuer_die_Termine_im_gesperrten_Raum() => Render("hell", "kalender-raumsperre-umbuchen", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        calendar.ByRoom = true;
        await Pump(Settling);
        var open = calendar.Columns.Single(c => c.Id == SpaSample.Lotus.Id);

        calendar.OpenAbsence(open);
        await Pump(Settling);
        await Pump(Settling);

        Assert.True(calendar.Absence!.IsRoomClosure);
        Assert.Equal(3, calendar.Absence.Rows.Count);
        Assert.Contains(window.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == "Umbuchung aus Raum Lotus" && t.IsEffectivelyVisible);
        Assert.Contains(window.GetVisualDescendants().OfType<Button>(), b => b.IsEffectivelyVisible && b.Content as string == "Gewählte übernehmen");
        return calendar;
    }, RoomClosureApi(LotusMaintenance()).On($"GET /api/locations/{SpaSample.LocationId}/room-closures/{RoomClosureBandId}/rebooking", RoomClosureProposal()));

    private static AbsenceProposal RoomClosureProposal()
    {
        var inLotus = SpaSample.CalendarDay().Appointments.Where(a => a.RoomId == SpaSample.Lotus.Id && a.Start >= SpaSample.At(13)).OrderBy(a => a.Start).ToList();
        return new AbsenceProposal(Guid.Empty, SpaSample.Day,
        [
            new AbsenceProposalItem(inLotus[0], AbsenceOption.SameTime, inLotus[0].Start, inLotus[0].EmployeeId, "Elif Demir", SpaSample.Lavender.Id, SpaSample.Lavender.Name,
                "Raum Lavendel ist um 14:00 frei; Elif Demir bleibt."),
            new AbsenceProposalItem(inLotus[1], AbsenceOption.SameTime, inLotus[1].Start, inLotus[1].EmployeeId, "Lena Huber", SpaSample.Cedar.Id, SpaSample.Cedar.Name,
                "Raum Zeder ist um 15:00 frei; Lena Huber bleibt."),
            new AbsenceProposalItem(inLotus[2], AbsenceOption.None, null, null, null, null, null, "An diesem Tag ist kein anderer Raum frei. Bitte den Gast kontaktieren und einen anderen Tag anbieten."),
        ]);
    }
}
