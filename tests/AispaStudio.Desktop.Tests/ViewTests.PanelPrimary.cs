using System.Xml.Linq;
using AispaStudio.Contracts;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>
/// Termin-Panel nach R01 Slice 21 (W53, W-Muster 4): genau ein Primärknopf, und zwar der der Karte, die gerade bearbeitet wird;
/// „Abbrechen“ ist im Kalender überall ein Ghost-Knopf.
/// </summary>
public sealed partial class ViewTests
{
    private static List<Button> VisiblePrimaries(Control panel) =>
        [.. panel.GetVisualDescendants().OfType<Button>().Where(b => b.Classes.Contains("primary") && b.IsEffectivelyVisible)];

    /// <summary>Beschriftung eines Knopfs: Text-Inhalt oder die Texte im Inhalt (Symbol und Text).</summary>
    private static string Label(Button button) =>
        button.Content as string ?? string.Join(" ", button.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text));

    private static Button VisibleCancel(Control panel) =>
        Assert.Single(panel.GetVisualDescendants().OfType<Button>(), b => b.IsEffectivelyVisible && b.Content as string == "Abbrechen");

    [Fact]
    public Task Termin_Panel_eingecheckt_mit_offener_Notiz_zeigt_genau_einen_Primaerknopf() => Render("hell", "termin-primaer-notiz", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        calendar.OpenAppointment(SpaSample.CalendarDay().Appointments.First(a => a.CustomerName == "Laura Novak"));
        await Pump(Settling);
        var panel = Named<Border>(window, "SidePanel");
        Assert.Equal(AppointmentStatus.CheckedIn, calendar.Selected!.Appointment.Status);

        calendar.Selected.BeginEditNoteCommand.Execute(null);
        await Pump(Settling);
        Assert.True(calendar.Selected.IsEditingNote);
        Assert.Equal("Notiz speichern", Label(Assert.Single(VisiblePrimaries(panel))));
        Assert.Contains("ghost", VisibleCancel(panel).Classes);

        // Nach dem Abbrechen ist wieder die Hauptaktion des Termins der eine Primärknopf.
        calendar.Selected.CancelEditNoteCommand.Execute(null);
        await Pump(Settling);
        Assert.Equal("Kassieren", Label(Assert.Single(VisiblePrimaries(panel))));
        return calendar;
    });

    [Fact]
    public Task Termin_Panel_mit_offener_Karte_Zeiten_Verschieben_Wiederholen_zeigt_genau_einen_Primaerknopf_und_Abbrechen_als_Ghost() => Render("hell", "termin-primaer-karten", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        calendar.OpenAppointment(SpaSample.Selected);
        await Pump(Settling);
        var panel = Named<Border>(window, "SidePanel");
        var appointment = calendar.Selected!;
        Assert.Equal("Einchecken", Label(Assert.Single(VisiblePrimaries(panel))));

        (System.Windows.Input.ICommand Open, System.Windows.Input.ICommand Cancel, string Primary)[] cards =
        [
            (appointment.BeginEditTimesCommand, appointment.CancelEditTimesCommand, "Übernehmen"),
            (appointment.AskMoveToDayCommand, appointment.CancelMoveToDayCommand, "Verschieben"),
            (appointment.EditSeriesCommand, appointment.CancelSeriesCommand, appointment.CreateSeriesText),
        ];
        foreach (var (open, cancel, primary) in cards)
        {
            open.Execute(null);
            await Pump(Settling);
            Assert.Equal(primary, Label(Assert.Single(VisiblePrimaries(panel))));
            Assert.Contains("ghost", VisibleCancel(panel).Classes);
            cancel.Execute(null);
            await Pump(Settling);
            Assert.Equal("Einchecken", Label(Assert.Single(VisiblePrimaries(panel))));
        }

        return calendar;
    });

    [Fact]
    public Task Termin_Panel_einer_Gruppe_zeigt_nur_Einchecken_als_Primaerknopf() => Render("hell", "termin-primaer-gruppe", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        calendar.OpenAppointment(SpaSample.CalendarDay().Appointments.First(a => a.CustomerName == "Nina Weiss"));
        await Pump(Settling);
        var panel = Named<Border>(window, "SidePanel");
        Assert.True(calendar.Selected!.CanCheckInGroup);

        Assert.Equal("Einchecken", Label(Assert.Single(VisiblePrimaries(panel))));
        return calendar;
    });

    [Fact]
    public Task Termin_Panel_einer_Gruppe_stornieren_traegt_die_Klasse_danger() => Render("hell", "termin-gruppe-stornieren", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        calendar.OpenAppointment(SpaSample.CalendarDay().Appointments.First(a => a.CustomerName == "Nina Weiss"));
        await Pump(Settling);
        var panel = Named<Border>(window, "SidePanel");
        Assert.True(calendar.Selected!.CanCancelGroup);

        var cancel = Assert.Single(panel.GetVisualDescendants().OfType<Button>(), b => b.Content is "Ganze Buchung stornieren" && b.IsEffectivelyVisible);
        Assert.Contains("danger", cancel.Classes);
        Assert.DoesNotContain("ghost", cancel.Classes);
        return calendar;
    });

    [Fact]
    public void Abbrechen_im_Kalender_ist_an_allen_acht_Stellen_ein_Ghost_Knopf()
    {
        var path = Path.Combine(RepositoryRoot(), "src", "AispaStudio.Desktop", "Views", "CalendarView.axaml");
        var cancels = XDocument.Load(path).Descendants()
            .Where(e => e.Name.LocalName == "Button" && (string?)e.Attribute("Content") == "Abbrechen")
            .ToList();

        Assert.Equal(8, cancels.Count);
        Assert.All(cancels, e => Assert.Contains("ghost", ((string?)e.Attribute("Classes") ?? "").Split(' ')));
    }
}
