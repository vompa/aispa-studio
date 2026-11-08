using AispaStudio.Desktop.ViewModels;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>R01 Slice 17 (W50 Teil 1): Terminkarten, Zettel und Streifen im Kalender haben für Screenreader Namen, Rolle und bei Terminen den Auswahlzustand.</summary>
public sealed partial class ViewTests
{
    private static Border CardOf(Window window, Func<CalendarItem, bool> match) =>
        window.GetVisualDescendants().OfType<Border>().First(b => b.Classes.Contains("appt") && b.DataContext is CalendarItem item && match(item));

    [Fact]
    public Task Gegeben_ein_gebuchter_Termin_mit_Gast_und_Behandlung_Wenn_der_Peer_der_Karte_abgefragt_wird_Dann_heisst_er_Zeit_Gast_Behandlung_Status_und_ist_keine_Gruppe() =>
        Render("hell", "kalender-peer-name", async (window, main) =>
        {
            var calendar = await SignedInCalendar(main);
            await Pump();

            var peer = ControlAutomationPeer.CreatePeerForElement(CardOf(window, i => i.Appointment?.Id == SpaSample.Selected.Id));
            Assert.Equal("10:30 Brigitte Müller, Hot-Stone-Massage, Gebucht", peer.GetName());
            Assert.Equal(AutomationControlType.ListItem, peer.GetAutomationControlType());
            Assert.True(peer.IsControlElement());

            // Sperrzeiten heißen nach demselben Muster: Beginn, Grund, Art.
            var block = ControlAutomationPeer.CreatePeerForElement(CardOf(window, i => i.Title == "Schulung Peeling"));
            Assert.Equal("12:30 Schulung Peeling, Gesperrt", block.GetName());
            Assert.Equal(AutomationControlType.ListItem, block.GetAutomationControlType());
            return calendar;
        });

    [Fact]
    public Task Gegeben_der_Termin_ist_ausgewaehlt_Wenn_der_Peer_abgefragt_wird_Dann_meldet_er_ausgewaehlt() => Render("hell", "kalender-peer-auswahl", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        await Pump();
        var card = CardOf(window, i => i.Appointment?.Id == SpaSample.Selected.Id);
        var peer = ControlAutomationPeer.CreatePeerForElement(card);
        var selection = Assert.IsAssignableFrom<ISelectionItemProvider>(peer);
        Assert.False(selection.IsSelected);
        var changes = new List<object?>();
        peer.PropertyChanged += (_, e) =>
        {
            if (e.Property == SelectionItemPatternIdentifiers.IsSelectedProperty)
            {
                changes.Add(e.NewValue);
            }
        };

        var at = CenterOf(window, SpaSample.Selected.Id);
        window.MouseDown(at, MouseButton.Left);
        window.MouseUp(at, MouseButton.Left);
        await Pump();

        Assert.True(selection.IsSelected);

        // Der Wechsel wird gemeldet, damit der Screenreader den neuen Zustand erfährt, ohne neu zu fragen.
        Assert.Equal([true], changes);
        return calendar;
    });

    [Fact]
    public Task Zettel_und_Streifen_haben_Namen_und_Rolle_Text() => Render("hell", "kalender-peer-zettel", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        await Pump();

        var note = ControlAutomationPeer.CreatePeerForElement(window.GetVisualDescendants().OfType<Border>()
            .First(b => b.Classes.Contains("sticky") && b.DataContext is CalendarNoteItem { Text: "Herr Lang ruft wegen Gutschein zurück" }));
        Assert.Equal("Zettel 13:15 Herr Lang ruft wegen Gutschein zurück", note.GetName());
        Assert.Equal(AutomationControlType.Text, note.GetAutomationControlType());
        Assert.True(note.IsContentElement());

        var stripe = ControlAutomationPeer.CreatePeerForElement(window.GetVisualDescendants().OfType<Border>()
            .First(b => b.Classes.Contains("stripe") && b.DataContext is CalendarStripe { IsPreparation: true, Appointment.Id: var id } && id == SpaSample.Selected.Id));
        Assert.Equal("Vorbereitung 15 Min.: Steine auf 55 °C aufheizen", stripe.GetName());
        Assert.Equal(AutomationControlType.Text, stripe.GetAutomationControlType());
        Assert.True(stripe.IsContentElement());
        return calendar;
    }, NotesApi());

    // R01 Slice 18 (W50 Teil 2): Wechselt die Markierung mit den Pfeiltasten, bekommt die Karte des neuen Termins den Fokus; der Screenreader sagt sie an.

    [Fact]
    public Task Gegeben_Termin_A_ist_ausgewaehlt_Wenn_Pfeil_rechts_gedrueckt_wird_Dann_meldet_ein_Fokusereignis_den_naechsten_Termin_mit_seinem_Namen() =>
        Render("hell", "kalender-pfeil-fokus", async (window, main) =>
        {
            var calendar = await SignedInCalendar(main);
            await Pump();
            calendar.ShowAppointment(SpaSample.Selected.Id, SpaSample.Selected.Start);
            await Pump();
            Assert.Equal(SpaSample.Selected.Id, calendar.MarkedId);
            Assert.Null(calendar.Selected);

            // Der Fenster-Peer gibt UI Automation den Fokuswechsel weiter (in Avalonia intern); öffentlich sichtbar ist dasselbe Ereignis als GotFocus.
            var announced = new List<string>();
            window.AddHandler(InputElement.GotFocusEvent, (_, e) =>
            {
                if (e.Source is Control control)
                {
                    announced.Add(ControlAutomationPeer.CreatePeerForElement(control).GetName());
                }
            });

            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            await Pump();

            var next = calendar.MarkedId;
            Assert.NotNull(next);
            Assert.NotEqual(SpaSample.Selected.Id, next);
            var card = CardOf(window, i => i.Appointment?.Id == next);
            var name = ((CalendarItem)card.DataContext!).AccessibleName;
            Assert.Equal([name], announced);
            var focused = ControlAutomationPeer.CreatePeerForElement(card);
            Assert.True(focused.HasKeyboardFocus());
            Assert.Equal(name, focused.GetName());
            Assert.Equal(AutomationControlType.ListItem, focused.GetAutomationControlType());
            Assert.True(focused.GetProvider<ISelectionItemProvider>()!.IsSelected);
            return calendar;
        });

    [Fact]
    public Task Gegeben_ein_Termin_ist_markiert_Wenn_Pfeil_runter_gedrueckt_wird_Dann_hat_der_naechste_Termin_der_Spalte_den_Fokus_und_Tab_ueberspringt_die_Karten() =>
        Render("hell", "kalender-pfeil-runter", async (window, main) =>
        {
            var calendar = await SignedInCalendar(main);
            await Pump();
            IInputElement? Focused() => TopLevel.GetTopLevel(window)!.FocusManager!.GetFocusedElement();

            // Ohne Markierung markiert ↓ den ersten Termin; jedes weitere ↓ den nächsten der Spalte, auch wenn eine Karte den Fokus hat.
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            await Pump();
            var first = calendar.MarkedId;
            Assert.NotNull(first);
            Assert.Same(CardOf(window, i => i.Appointment?.Id == first), Focused());

            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            await Pump();
            var second = calendar.MarkedId;
            Assert.NotEqual(first, second);
            var card = CardOf(window, i => i.Appointment?.Id == second);
            Assert.Same(card, Focused());
            Assert.False(card.IsTabStop, "Die Karte ist kein Tab-Stopp; Tab führt wie bisher am Kalender vorbei.");

            // Der Fokus öffnet die Hover-Karte nicht, auch nicht nach ihrer Verzögerung: sie läge bei jedem Pfeil über den Nachbarterminen.
            await Task.Delay(ToolTip.GetShowDelay(card) + 300);
            await Pump();
            Assert.False(ToolTip.GetIsOpen(card));

            // Tab verlässt die Karte, statt zur nächsten Karte zu springen.
            window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.None);
            await Pump();
            Assert.IsNotType<AispaStudio.Desktop.Controls.AppointmentCard>(Focused());
            return calendar;
        });
}
