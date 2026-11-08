using AispaStudio.Contracts;
using AispaStudio.Desktop.Controls;
using AispaStudio.Desktop.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>Die übrigen Abläufe der Hilfe im Muster „Schritte mit je einem Bild“; Grundlagen und der Ablauf „Termin buchen“ stehen in <c>ViewTests.HelpFlows.cs</c>.</summary>
public sealed partial class ViewTests
{
    /// <summary>Bereich, der den Text enthält: der <paramref name="levels"/>-te Elternteil der Beschriftung.</summary>
    private static Rect AncestorRect(Window window, string text, int levels)
    {
        Visual node = FoundVisible(window, c => c is TextBlock t && t.Text == text, $"Text {text}");
        for (var i = 0; i < levels; i++)
        {
            node = node.GetVisualParent() ?? throw new InvalidOperationException($"Zu wenige Ebenen über {text}");
        }

        return RectOf(window, (Control)node);
    }

    /// <summary>Reiter, Knopf oder anderes Steuerelement mit dem Text als Inhalt.</summary>
    private static Rect ContentRect(Window window, string text) =>
        RectOf(window, FoundVisible(window, c => c is ContentControl { Content: string s } && s == text, $"Inhalt {text}"));

    /// <summary>Eingabefeld, in dem der Text steht.</summary>
    private static Rect TextBoxRect(Window window, string text) =>
        RectOf(window, FoundVisible(window, c => c is TextBox t && t.Text == text, $"Eingabefeld {text}"));

    /// <summary>Feld, dessen Beschriftung mit dem Text beginnt (die Währung steht erst zur Laufzeit darin).</summary>
    private static Rect FieldStartingRect(Window window, string start) =>
        RectOf(window, FoundVisible(window, c => c is FormField f && f.Label?.StartsWith(start, StringComparison.Ordinal) == true, $"Feld {start}"));

    /// <summary>Karte des Termins eines Gastes im Kalender; die Beispieltage vergeben ihre Nummern bei jedem Aufruf neu.</summary>
    private static Rect AppointmentOfRect(Window window, string customer) =>
        RectOf(window, FoundVisible(window, c => c is Border b && b.Classes.Contains("appt") && b.DataContext is CalendarItem { Appointment.CustomerName: var name } && name == customer, $"Termin von {customer}"));

    /// <summary>Knopf, dessen Beschriftung mit dem Text endet (z. B. „14:00 alles buchen“).</summary>
    private static Rect ButtonEndingRect(Window window, string end) =>
        RectOf(window, FoundVisible(window, c => c is Button b && (b.Content as string)?.EndsWith(end, StringComparison.Ordinal) == true || c is Button b2 && b2.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text?.EndsWith(end, StringComparison.Ordinal) == true), $"Knopf …{end}"));

    /// <summary>Der Knopf „Termin“ in der Kopfzeile samt Pfeil für das Menü.</summary>
    private static Rect NewAppointmentRect(Window window) =>
        RectOf(window, (FoundVisible(window, c => c is Button b && b.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text == "Termin"), "Knopf Termin").Parent as Control)!);

    private static Task<bool> HelpCalendarFlow(string flow, Func<HelpShots, CalendarViewModel, Task> steps, StubApi? stub = null) =>
        HelpFlow(flow, async shots => await steps(shots, await OpenCalendar(shots)), stub);

    [Fact]
    public Task Handbuch_Termin_verschieben() => HelpCalendarFlow("termin-verschieben", async (shots, calendar) =>
    {
        var window = shots.Window;
        var item = calendar.Columns.SelectMany(c => c.Items).First(i => i.Appointment?.Id == SpaSample.Selected.Id);
        // Schritt 1: den Termin fassen.
        await shots.Shot(1, AppointmentRect(window, SpaSample.Selected.Id));
        // Schritt 2: an die neue Zeit ziehen; die Vorschau zeigt die Stelle.
        calendar.BeginDrag(item, 20);
        calendar.DragTo(DayLayout.ColumnLeft(0, calendar.Scale) + 40, item.Top + 20 - (30 * calendar.Scale.PixelsPerMinute));
        await Pump();
        await shots.Shot(2, RectOf(window, FoundVisible(window, c => c is Rectangle { StrokeDashArray.Count: > 0 }, "Vorschau beim Ziehen")));
        // Schritt 3: loslassen; die Meldung unten bietet Rückgängig an.
        await calendar.DropAsync();
        await Pump();
        await shots.Shot(3, NamedRect(window, "ToastBar"));
    });

    [Fact]
    public Task Handbuch_Termin_stornieren() => HelpCalendarFlow("termin-stornieren", async (shots, calendar) =>
    {
        var window = shots.Window;
        var item = calendar.Columns.SelectMany(c => c.Items).First(i => i.Appointment?.Id == SpaSample.Selected.Id);
        // Schritt 1: den Termin anklicken (markieren).
        calendar.Mark(item);
        await shots.Shot(1, AppointmentRect(window, SpaSample.Selected.Id));
        // Schritt 2: Entf drücken; die Rückfrage verlangt einen Grund.
        Assert.True(calendar.HandleKey(Key.Delete, KeyModifiers.None));
        await Pump();
        var dialog = Assert.IsType<ConfirmDialogViewModel>(shots.Main.DialogHost.Current);
        await shots.Shot(2, TextBoxRect(window, "An der Rezeption storniert"));
        // Schritt 3: Stornieren.
        Assert.Equal("Termin stornieren?", dialog.Title);
        await shots.Shot(3, ButtonRect(window, dialog.ConfirmText));
    });

    [Fact]
    public Task Handbuch_Status_setzen() => HelpCalendarFlow("status-setzen", async (shots, calendar) =>
    {
        var window = shots.Window;
        // Schritt 1: den Termin anklicken; rechts öffnet sich das Terminfenster.
        await shots.Shot(1, AppointmentRect(window, SpaSample.Selected.Id));
        calendar.OpenAppointment(SpaSample.Selected);
        await Pump();
        // Schritt 2: Einchecken.
        await shots.Shot(2, ButtonRect(window, "Einchecken"));
        // Schritt 3: unter „Mehr“ Erledigt oder Nicht erschienen.
        var more = (Avalonia.Controls.Primitives.ToggleButton)FoundVisible(window, c => c.Name == "MoreToggle", "Mehr");
        more.IsChecked = true;
        await Pump();
        await shots.Shot(3, ButtonRect(window, "Erledigt"), ButtonRect(window, "Nicht erschienen"));
    });

    [Fact]
    public Task Handbuch_Freie_Zeit_suchen() => HelpCalendarFlow("freie-zeit-suchen", async (shots, calendar) =>
    {
        var window = shots.Window;
        // Schritt 1: der Knopf im Kalenderkopf (oder Strg+N) öffnet die Suche.
        await shots.Shot(1, NamedRect(window, "FreeTimeButton"));
        Assert.True(calendar.HandleKey(Key.N, KeyModifiers.Control));
        await Pump();
        // Schritt 2: Behandlung wählen.
        calendar.FreeTime!.Treatment = calendar.FreeTime.Treatments.First(t => t.Id == SpaSample.Treatments[1].Id);
        await Pump();
        await shots.Shot(2, NamedRect(window, "FreeTimeTreatment"));
        // Schritt 3: eine Zeit wählen; sie ist im Kalender umrahmt.
        calendar.FreeTime.Move(1);
        await Pump();
        await shots.Shot(3, NamedRect(window, "FreeTimeList"));
        // Schritt 4: die gewählte Zeit buchen; der Knopf steht am Ende des Panels.
        FindButton(window, b => b.Content as string == "Diese Zeit buchen (Enter)")!.BringIntoView();
        await Pump(Settling);
        await shots.Shot(4, ButtonRect(window, "Diese Zeit buchen (Enter)"));
    });

    [Fact]
    public Task Handbuch_Paar_und_Spa_Tag() => HelpCalendarFlow("paar-und-spa-tag", async (shots, calendar) =>
    {
        var window = shots.Window;
        // Schritt 1: P öffnet das Panel (oder das Menü neben „Termin“).
        await shots.Shot(1, NewAppointmentRect(window));
        calendar.OpenGroupBookingCommand.Execute(null);
        var group = calendar.GroupBooking!;
        await Pump();
        // Schritt 2: Tag und Gäste wählen.
        group.Guests[0].Search.Query = "br";
        group.Guests[1].Search.Query = "be";
        await Pump();
        group.Guests[0].Search.Customer = group.Guests[0].Search.Results[0];
        group.Guests[1].Search.Customer = group.Guests[1].Search.Results[0];
        await Pump();
        var dateRect = RectOf(window, FoundVisible(window, c => c is DateField, "Datumsfeld"));
        var guestCards = group.Guests.Select(g => RectOf(window, FoundVisible(window, c => c is Border b && b.Classes.Contains("guestcard") && b.DataContext == g, g.Label))).ToList();
        await shots.Shot(2, guestCards.Aggregate(dateRect, (all, card) => all.Union(card)));
        // Schritt 3: je Gast die Behandlung wählen.
        group.Steps[0].Entries[0].Choice = group.Steps[0].Entries[0].Choices[2];
        group.Steps[0].Entries[1].Choice = group.Steps[0].Entries[1].Choices[2];
        await Pump();
        await shots.Shot(3, group.Guests.Select(g => RectOf(window, FoundVisible(window, c => c is Border b && b.Classes.Contains("guestcard") && b.DataContext == g, g.Label)))
            .Aggregate((all, card) => all.Union(card)));
        // Schritt 4: eine Startzeit wählen.
        group.Gap = 10;
        await group.SearchSlotsAsync();
        group.Slot = group.Slots[1];
        await Pump();
        await shots.Shot(4, AncestorRect(window, "Freie Zeiten", 1));
        // Schritt 5: alles buchen.
        Assert.True(group.BookCommand.CanExecute(null));
        // Der Knopf steht am Ende des langen Panels: erst ins Bild holen.
        FoundVisible(window, c => c is Button b && b.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text?.EndsWith("alles buchen", StringComparison.Ordinal) == true) || c is Button { Content: string s } && s.EndsWith("alles buchen", StringComparison.Ordinal), "Knopf alles buchen").BringIntoView();
        await Pump(Settling);
        await shots.Shot(5, ButtonEndingRect(window, "alles buchen"));
    });

    [Fact]
    public Task Handbuch_Termin_kassieren() => HelpCalendarFlow("termin-kassieren", async (shots, calendar) =>
    {
        var window = shots.Window;
        var shell = (ShellViewModel)shots.Main.Content!;
        // Schritt 1: den Termin im Kalender anklicken.
        await shots.Shot(1, AppointmentOfRect(window, SpaSample.Paying.CustomerName));
        calendar.OpenAppointment(calendar.Columns.SelectMany(c => c.Items).Select(i => i.Appointment).First(a => a?.CustomerName == SpaSample.Paying.CustomerName)!);
        await Pump();
        // Schritt 2: Kassieren.
        await shots.Shot(2, ButtonRect(window, "Kassieren"));
        await shell.CheckoutAsync(SpaSample.Paying);
        await Pump();
        // Schritt 3: die Rechnung prüfen, bei Bedarf ergänzen.
        await shots.Shot(3, AncestorRect(window, "Offene Rechnung", 3));
        // Schritt 4: bezahlen.
        shell.Pos.Payments.First(p => p.Method == PaymentMethod.Card).Amount = "89";
        await Pump();
        await shots.Shot(4, ButtonRect(window, "Bezahlen und Beleg erstellen"));
    });

    [Fact]
    public Task Handbuch_Beleg_stornieren() => HelpCalendarFlow("beleg-stornieren", async (shots, calendar) =>
    {
        var window = shots.Window;
        var shell = (ShellViewModel)shots.Main.Content!;
        await shell.CheckoutAsync(SpaSample.Paying);
        await Pump();
        // Schritt 1: Belege wählen.
        await shots.Shot(1, ContentRect(window, "Belege"));
        shell.Pos.ShowReceiptsCommand.Execute(null);
        await shell.Pos.LoadReceiptsAsync();
        await Pump();
        // Schritt 2: den Beleg anklicken.
        await shots.Shot(2, AncestorRect(window, "Belege heute", 1));
        // Schritt 3: den Grund eintragen.
        shell.Pos.VoidReasonInput = "Kundin hat doch mit Karte bezahlt";
        await Pump();
        await shots.Shot(3, TextBoxRect(window, "Kundin hat doch mit Karte bezahlt"));
        // Schritt 4: Stornieren.
        await shots.Shot(4, ButtonRect(window, "Stornieren"));
    });

    [Fact]
    public Task Handbuch_Tagesabschluss() => HelpCalendarFlow("tagesabschluss", async (shots, calendar) =>
    {
        var window = shots.Window;
        var shell = (ShellViewModel)shots.Main.Content!;
        await shell.CheckoutAsync(SpaSample.Paying);
        await Pump();
        // Schritt 1: Kassenlade & Abschluss wählen.
        await shots.Shot(1, ContentRect(window, "Kassenlade & Abschluss"));
        shell.Pos.ShowClosingCommand.Execute(null);
        await shell.Pos.LoadReportAsync();
        await Pump();
        // Schritt 2: den Zwischenbericht ansehen.
        await shots.Shot(2, AncestorRect(window, "Zahlungsarten", 1));
        // Schritt 3: den gezählten Bestand eintragen.
        shell.Pos.CountedCash = "356,30";
        await Pump();
        await shots.Shot(3, FieldStartingRect(window, "Gezählter Bestand ("));
        // Schritt 4: Tagesabschluss durchführen.
        await shots.Shot(4, ButtonRect(window, "Tagesabschluss durchführen"));
    });

    [Fact]
    public Task Handbuch_Gutschein_verkaufen() => HelpCalendarFlow("gutschein-verkaufen", async (shots, calendar) =>
    {
        var window = shots.Window;
        var shell = (ShellViewModel)shots.Main.Content!;
        // Das Panel „Artikel und Gutscheine“ beginnt zugeklappt (Slice 137); der Ablauf klappt es auf.
        shell.Pos.IsSalesColumnOpen = true;
        await shell.CheckoutAsync(SpaSample.Paying);
        await Pump();
        // Schritt 1: den Wert eintragen.
        shell.Pos.VoucherValue = "50";
        await Pump();
        // Der Gutschein-Block steht unten in der scrollenden Rechnung.
        ButtonWithText(window, "Wertgutschein verkaufen").BringIntoView();
        await Pump(Settling);
        await shots.Shot(1, FieldStartingRect(window, "Wert ("));
        // Schritt 2: Wertgutschein verkaufen; er steht danach auf der Rechnung.
        await shots.Shot(2, ButtonRect(window, "Wertgutschein verkaufen"));
        await shell.Pos.SellVoucherCommand.ExecuteAsync(null);
        await Pump();
        Assert.Null(shell.Pos.Error);
        Assert.Contains(shell.Pos.Lines, l => l.Description == "Wertgutschein");
        // Schritt 3: bezahlen; der Code des Gutscheins steht danach auf dem Beleg.
        shell.Pos.Payments.First(p => p.Method == PaymentMethod.Card).Amount = shell.Pos.Bill!.Total.ToString("0.00", System.Globalization.CultureInfo.GetCultureInfo("de-AT"));
        await Pump();
        await shots.Shot(3, ButtonRect(window, "Bezahlen und Beleg erstellen"));
    }, Api().On($"POST /api/pos/bills/{SpaSample.BillId}/lines", SpaSample.Bill(voucher: 50m)));
}
