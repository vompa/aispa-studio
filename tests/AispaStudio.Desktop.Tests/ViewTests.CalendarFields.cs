using System.Xml.Linq;
using AispaStudio.Desktop.Controls;
using AispaStudio.Desktop.ViewModels;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>
/// Feldnamen im Kalender nach R01 Slice 07 (W51, W52, W55, W-Muster 1): jedes Feld der Seitenleiste hat einen Namen, der nicht aus dem Platzhalter stammt,
/// Kontrollkästchen ohne Text tragen den Termin ihrer Zeile als Namen, und „Personen“ beim Sauna-Eintritt ist ein Zahlenfeld.
/// </summary>
public sealed partial class ViewTests
{
    private static readonly XNamespace AvaloniaXaml = "https://github.com/avaloniaui";

    private static Border SidePanelOf(Visual root) => root.GetVisualDescendants().OfType<Border>().Single(b => b.Name == "SidePanel");

    [Fact]
    public Task Neuer_Termin_mit_neuer_Kundin_hat_an_jedem_Feld_einen_Namen_nicht_aus_dem_Platzhalter() => Render("hell", "kalender-felder-termin", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        calendar.StartBookingAt(10, calendar.Hours.Single(h => h.Label == "12:00").Top + 1);
        Assert.Equal(NewEntryKind.Appointment, calendar.NewEntry!.Kind);
        calendar.Booking!.ShowNewCustomer = true;
        await Pump();

        AssertFieldsNamedNotFromPlaceholder(SidePanelOf(window), 7);
        return calendar;
    });

    [Fact]
    public Task Neuer_Termin_laesst_die_Person_der_Spalte_nicht_abwaehlen_und_nennt_unter_der_Behandlung_die_Grenze() => Render("hell", "kalender-termin-spalte", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        calendar.StartBookingAt(10, calendar.Hours.Single(h => h.Label == "12:00").Top + 1);
        await Pump();

        var panel = SidePanelOf(window);
        Assert.DoesNotContain(panel.GetVisualDescendants().OfType<CheckBox>(), c => c.IsEffectivelyVisible);
        var texts = panel.GetVisualDescendants().OfType<TextBlock>().Where(t => t.IsEffectivelyVisible).Select(t => t.Text).ToList();
        Assert.Contains(calendar.Booking!.ColumnScopeText, texts);
        Assert.Contains(panel.GetVisualDescendants().OfType<Button>(), b => b.IsEffectivelyVisible && Equals(b.Content, "Freie Zeit suchen"));
        return calendar;
    });

    [Fact]
    public Task Gruppenbuchung_hat_an_jedem_Feld_einen_Namen_nicht_aus_dem_Platzhalter() => Render("hell", "kalender-felder-gruppe", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        calendar.OpenGroupBookingCommand.Execute(null);
        var group = calendar.GroupBooking!;
        group.AddStepCommand.Execute(null);
        await Pump();
        Assert.True(group.ShowGap);

        var panel = SidePanelOf(window);
        AssertFieldsNamedNotFromPlaceholder(panel, 6);
        var guest = group.Guests[0].ShortName;
        var names = panel.GetVisualDescendants().OfType<ComboBox>().Where(c => c.IsEffectivelyVisible)
            .Select(c => ControlAutomationPeer.CreatePeerForElement(c).GetName()).ToList();
        Assert.Contains($"Behandlung für {guest}", names);
        return calendar;
    });

    [Fact]
    public Task Gruppenbuchung_hat_ein_Datumsfeld_mit_Minimum_heute_und_der_Knopf_nennt_den_Sperrgrund() => Render("hell", "kalender-gruppe-datum", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        calendar.OpenGroupBookingCommand.Execute(null);
        var group = calendar.GroupBooking!;
        await Pump();

        var panel = SidePanelOf(window);
        var date = Assert.Single(panel.GetVisualDescendants().OfType<DateField>());
        Assert.Equal(group.Today, date.Minimum);
        var button = Assert.Single(ButtonsFor(panel, group.BookCommand));
        Assert.False(button.IsEffectivelyEnabled);
        Assert.Equal(group.BookBlocker, DisabledHint.GetText(button));
        Assert.StartsWith("Verfügbar, sobald Gast 1", DisabledHint.GetText(button), StringComparison.Ordinal);
        var guestCards = panel.GetVisualDescendants().OfType<Border>().Count(b => b.Classes.Contains("guestcard"));
        Assert.Equal(group.Guests.Count, guestCards);
        return calendar;
    });

    [Fact]
    public Task Freie_Zeit_suchen_hat_an_jedem_Feld_einen_Namen_nicht_aus_dem_Platzhalter() => Render("hell", "kalender-felder-freie-zeit", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        calendar.OpenFreeTimeCommand.Execute(null);
        await Pump();
        Assert.NotNull(calendar.FreeTime);

        AssertFieldsNamedNotFromPlaceholder(SidePanelOf(window), 4);
        return calendar;
    });

    [Fact]
    public Task Personen_im_Sauna_Eintritt_ist_ein_Zahlenfeld_und_Pfeil_hoch_zaehlt_hoch() => Render("hell", "kalender-felder-sauna", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        calendar.OpenAreasCommand.Execute(null);
        await Pump();
        var areas = calendar.Areas!;
        areas.Persons = 2;
        await Pump();

        var panel = SidePanelOf(window);
        var persons = panel.GetVisualDescendants().OfType<NumberField>().Single(f => f.IsEffectivelyVisible);
        Assert.True(persons.ShowSpinner);
        Assert.Equal("Personen", ControlAutomationPeer.CreatePeerForElement(persons).GetName());
        Assert.Equal("2", persons.Text);
        Assert.DoesNotContain(panel.GetVisualDescendants().OfType<Button>(), b => ToolTip.GetTip(b) is "Weniger" or "Mehr");

        // Given 2 Personen, When Pfeil hoch im Feld, Then 3.
        persons.Focus(NavigationMethod.Tab);
        window.KeyPressQwerty(PhysicalKey.ArrowUp, RawInputModifiers.None);
        await Pump();
        Assert.Equal("3", persons.Text);
        Assert.Equal(3, areas.Persons);
        AssertFieldsNamedNotFromPlaceholder(panel, 4);

        // Für das Bild: das Formular „Neuer Eintritt“ im Ausschnitt.
        persons.BringIntoView();
        await Pump();
        return calendar;
    });

    [Theory]
    [InlineData("25")]
    [InlineData("")]
    public Task Personen_ausserhalb_1_bis_20_oder_leer_sperrt_Eintritt_buchen(string typed) => Render("hell", "kalender-felder-sauna-fehler", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        calendar.OpenAreasCommand.Execute(null);
        await Pump();
        var areas = calendar.Areas!;
        areas.Search.Query = "br";
        await Pump();
        areas.Search.Customer = areas.Search.Results[0];
        await Pump();
        var panel = SidePanelOf(window);
        var book = ButtonWithText(panel, "Eintritt buchen");
        Assert.True(book.IsEffectivelyEnabled);

        // Given ein ungültiger Wert in „Personen“, When das Feld verlassen wird, Then Fehler am Feld und „Eintritt buchen“ gesperrt.
        var persons = panel.GetVisualDescendants().OfType<NumberField>().Single(f => f.IsEffectivelyVisible);
        persons.Focus(NavigationMethod.Tab);
        persons.SelectAll();
        window.KeyPressQwerty(PhysicalKey.Backspace, RawInputModifiers.None);
        if (typed.Length > 0)
        {
            window.KeyTextInput(typed);
        }

        await Blur(book);
        Assert.NotNull(persons.Error);
        Assert.False(book.IsEffectivelyEnabled);
        Assert.Equal(1, areas.Persons);
        return calendar;
    });

    [Fact]
    public Task Serienvorschau_traegt_an_jedem_Kontrollkaestchen_sein_Datum_als_Namen() => Render("hell", "kalender-felder-serie", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        calendar.OpenAppointment(SpaSample.Selected);
        var appointment = calendar.Selected!;
        appointment.EditSeriesCommand.Execute(null);
        appointment.SeriesCount = "3";
        await appointment.PreviewSeriesCommand.ExecuteAsync(null);
        await Pump();
        Assert.True(appointment.SeriesRows.Count >= 3);

        var panel = SidePanelOf(window);
        var boxes = panel.GetVisualDescendants().OfType<CheckBox>().Where(b => b.DataContext is SeriesRowViewModel).ToList();
        Assert.Equal(appointment.SeriesRows.Count, boxes.Count);
        Assert.All(boxes, box => Assert.Equal(((SeriesRowViewModel)box.DataContext!).DateText, ControlAutomationPeer.CreatePeerForElement(box).GetName()));
        AssertFieldsNamedNotFromPlaceholder(panel, 1);
        return calendar;
    });

    [Fact]
    public Task Ausfall_Assistent_nennt_an_jedem_Kontrollkaestchen_Gast_und_Termin() => Render("hell", "kalender-felder-ausfall", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        calendar.OpenAbsence(calendar.Columns.Single(c => c.IsSick));
        await Pump();
        Assert.NotEmpty(calendar.Absence!.Rows);

        var boxes = SidePanelOf(window).GetVisualDescendants().OfType<CheckBox>().Where(b => b.DataContext is AbsenceRow).ToList();
        Assert.Equal(calendar.Absence.Rows.Count + calendar.Absence.OtherRows.Count, boxes.Count);
        Assert.All(boxes, box =>
        {
            var row = (AbsenceRow)box.DataContext!;
            Assert.Equal($"{row.Guest}, {row.Before}", ControlAutomationPeer.CreatePeerForElement(box).GetName());
        });
        return calendar;
    }, Api()
        .On($"GET /api/locations/{SpaSample.LocationId}/resources", SpaSample.ResourcesWithAbsent)
        .On($"GET /api/locations/{SpaSample.LocationId}/calendar?date=2026-10-19", SpaSample.BusyDay())
        .On($"POST /api/locations/{SpaSample.LocationId}/absence/proposal", SpaSample.AbsenceProposal()));

    /// <summary>
    /// Quelltextprüfung für Zustände, die ein Scan zur Laufzeit schwer erreicht (Freigabe, Standort im Datums-Flyout, Serie bearbeiten, Warteliste „Grund“),
    /// seit Slice 11 für jede Ansicht der App, weil kein Name mehr aus dem Platzhalter kommt: jedes Feld steht in einem <see cref="FormField"/> mit Beschriftung
    /// oder hat einen Namen bzw. <c>LabeledBy</c>; jedes Kontrollkästchen hat Text oder einen Namen.
    /// </summary>
    [Theory]
    [MemberData(nameof(DesktopViews))]
    public void Jede_Ansicht_jedes_Feld_hat_eine_Beschriftung_oder_einen_Namen(string view)
    {
        var path = Path.Combine(MessageBarTests.FindRepoRoot(), "src", "AispaStudio.Desktop", view);
        var document = XDocument.Load(path, LoadOptions.SetLineInfo);
        string[] fields = ["TextBox", "ComboBox", "AutoCompleteBox", "DateField", "TimeField", "NumberField", "MoneyField", "SearchField"];

        static bool Named(XElement element) =>
            element.Attributes().Select(a => a.Name.LocalName).Concat(element.Elements().Select(c => c.Name.LocalName))
                .Any(name => name is "AutomationProperties.Name" or "AutomationProperties.LabeledBy");
        static bool InLabeledFormField(XElement element) =>
            element.Ancestors().FirstOrDefault(a => a.Name.LocalName == "FormField") is { } form && form.Attribute("Label") is not null;
        static bool HasText(XElement box) => box.Attribute("Content") is not null || box.Elements().Any(c => !c.Name.LocalName.Contains('.', StringComparison.Ordinal));

        var problems = document.Descendants()
            .Where(e => (fields.Contains(e.Name.LocalName) && !Named(e) && !InLabeledFormField(e))
                || (e.Name == AvaloniaXaml + "CheckBox" && !Named(e) && !HasText(e)))
            .Select(e => $"{e.Name.LocalName} in Zeile {((System.Xml.IXmlLineInfo)e).LineNumber}")
            .ToList();
        Assert.Empty(problems);
    }
}
