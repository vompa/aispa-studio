using AispaStudio.Desktop.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>Fluent 2 F19: Rückfragedialog nach Design-Brief 5.27 (420 breit, Radius 12, Innenabstand 24, Overlay 0,45, Fokus, Esc, Enter, Fokus zurück, höchstens einer).</summary>
public sealed partial class ViewTests
{
    private static readonly ConfirmRequest DeleteRequest = new("Termin endgültig löschen?",
        "Der Termin von Mia Muster am Mi. 21.10. 10:00 ist danach nicht mehr im Kalender.", "Löschen");

    private static Border Dialog(Window window) => window.GetVisualDescendants().OfType<Border>().Single(b => b.Name == "DialogCard" && b.IsEffectivelyVisible);

    private static Button DialogButton(Window window, string name) => Dialog(window).GetVisualDescendants().OfType<Button>().Single(b => b.Name == name);

    private static readonly string[] DialogButtonNames = ["ConfirmButton", "AlternativeButton", "CancelButton"];

    private static IInputElement? Focused(Window window) => TopLevel.GetTopLevel(window)!.FocusManager!.GetFocusedElement();

    private static void AssertDialogRules(ConfirmRequest request) => FakeDialogs.AssertRules(request);

    [Theory]
    [InlineData("hell")]
    [InlineData("dunkel")]
    public Task Rueckfragedialog_hat_die_Masse_den_Aufbau_und_den_Fokus_auf_Abbrechen(string theme) => Render(theme, "rueckfrage", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        await Pump();
        var asking = main.DialogHost.AskAsync(DeleteRequest);
        await Pump(Settling);

        var card = Dialog(window);
        Assert.Equal(420, card.Bounds.Width, 0.5);
        var border = card;
        Assert.Equal(new CornerRadius(12), border.CornerRadius);
        Assert.Equal(new Thickness(24), border.Padding);
        Assert.Equal(new Thickness(1), border.BorderThickness);
        Assert.Equal(default, border.BoxShadow);
        Assert.Equal(ColorOf(window, "SurfaceBrush"), ColorOf(border.Background));
        Assert.Equal(ColorOf(window, "LineBrush"), ColorOf(border.BorderBrush));

        // Overlay rgba(0,0,0,0.45) über dem ganzen Fenster.
        var scrim = window.GetVisualDescendants().OfType<Border>().Single(b => b.Name == "Scrim");
        Assert.Equal(window.Bounds.Width, scrim.Bounds.Width, 0.5);
        var scrimColor = ColorOf(scrim.Background);
        Assert.Equal((0, 0, 0), (scrimColor.R, scrimColor.G, scrimColor.B));
        Assert.Equal(0.45, scrimColor.A / 255.0, 0.01);

        // Aufbau: Titel (Frage), Text, Knopfleiste; die Aktion steht links, „Abbrechen“ rechts, rechtsbündig mit 8 px Abstand.
        var title = card.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Name == "DialogTitle");
        var message = card.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Name == "DialogMessage");
        Assert.Equal("Termin endgültig löschen?", title.Text);
        Assert.StartsWith("Der Termin von Mia Muster", message.Text, StringComparison.Ordinal);
        var confirm = DialogButton(window, "ConfirmButton");
        var cancel = DialogButton(window, "CancelButton");
        Assert.True(LeftOf(window, confirm) < LeftOf(window, cancel), "Aktion links, Abbrechen rechts");
        Assert.Equal(8, LeftOf(window, cancel) - (LeftOf(window, confirm) + confirm.Bounds.Width), 0.5);
        Assert.Equal(LeftOf(window, card) + card.Bounds.Width - 24, LeftOf(window, cancel) + cancel.Bounds.Width, 1.5);
        Assert.Contains("danger", confirm.Classes);
        Assert.Contains("filled", confirm.Classes);
        Assert.DoesNotContain("primary", confirm.Classes);
        Assert.Contains("ghost", cancel.Classes);
        Assert.Equal(ColorOf(window, "CritBrush"), ColorOf(confirm.Background));
        Assert.True(Contrast(ColorOf(confirm.Background), ColorOf(confirm.Foreground)) >= 4.5);
        AssertDialogRules(DeleteRequest);

        // Zerstörende Aktion: der Fokus liegt auf „Abbrechen“, nie auf „Löschen“.
        Assert.Same(cancel, Focused(window));

        SaveShot(window, "rueckfrage-dialog", theme);
        main.DialogHost.Current!.CancelCommand.Execute(null);
        Assert.False((await asking).IsConfirmed);
        await Pump();
        Assert.False(main.DialogHost.HasDialog);
        return calendar;
    });

    [Fact]
    public Task Enter_bei_zerstoerender_Rueckfrage_loest_Abbrechen_aus_Esc_ebenso() => Render("hell", "rueckfrage-tasten", async (window, main) =>
    {
        await SignedInCalendar(main);
        await Pump();
        var asking = main.DialogHost.AskAsync(DeleteRequest);
        await Pump(Settling);
        Press(window, PhysicalKey.Enter);
        await Pump();
        Assert.Equal(ConfirmChoice.Cancelled, (await asking).Choice);
        Assert.False(main.DialogHost.HasDialog);

        asking = main.DialogHost.AskAsync(DeleteRequest);
        await Pump(Settling);
        Press(window, PhysicalKey.Escape);
        await Pump();
        Assert.Equal(ConfirmChoice.Cancelled, (await asking).Choice);
        Assert.False(main.DialogHost.HasDialog);

        // Mit Tab auf die Aktion und Enter: die Aktion läuft.
        asking = main.DialogHost.AskAsync(DeleteRequest);
        await Pump(Settling);
        Press(window, PhysicalKey.Tab, RawInputModifiers.Shift);
        await Pump();
        Assert.Same(DialogButton(window, "ConfirmButton"), Focused(window));
        Press(window, PhysicalKey.Enter);
        await Pump();
        Assert.Equal(ConfirmChoice.Confirmed, (await asking).Choice);
        return main.Content!;
    });

    [Fact]
    public Task Neutrale_Rueckfrage_fokussiert_den_primaeren_Knopf_und_Enter_bestaetigt() => Render("hell", "rueckfrage-neutral", async (window, main) =>
    {
        await SignedInCalendar(main);
        await Pump();
        var request = new ConfirmRequest("Änderungen verwerfen?", "Was Sie eingetragen haben, geht verloren.", "Verwerfen", IsDestructive: false, CancelText: "Weiter bearbeiten");
        var asking = main.DialogHost.AskAsync(request);
        await Pump(Settling);

        var confirm = DialogButton(window, "ConfirmButton");
        Assert.Contains("primary", confirm.Classes);
        Assert.DoesNotContain("danger", confirm.Classes);
        Assert.Same(confirm, Focused(window));
        Assert.Equal("Weiter bearbeiten", DialogButton(window, "CancelButton").Content);
        AssertDialogRules(request);
        SaveShot(window, "rueckfrage-neutral-dialog", "hell");
        Press(window, PhysicalKey.Enter);
        await Pump();
        Assert.Equal(ConfirmChoice.Confirmed, (await asking).Choice);
        return main.Content!;
    });

    [Fact]
    public Task Klick_neben_den_Dialog_schliesst_nichts_und_Tab_bleibt_im_Dialog() => Render("hell", "rueckfrage-alert", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        await Pump();
        var request = new ConfirmRequest("Termin stornieren?", "Der Termin ist danach storniert.", "Nur diesen stornieren", AlternativeText: "Diesen und alle folgenden",
            InputLabel: "Grund", InputText: "An der Rezeption storniert");
        var asking = main.DialogHost.AskAsync(request);
        await Pump(Settling);
        SaveShot(window, "rueckfrage-drei-knoepfe", "hell");

        // Klick auf den Overlay (Ecke oben links) bleibt ohne Wirkung; es gibt kein Schließen-Kreuz.
        window.MouseDown(new Point(6, 6), MouseButton.Left);
        window.MouseUp(new Point(6, 6), MouseButton.Left);
        await Pump();
        Assert.True(main.DialogHost.HasDialog);
        Assert.False(asking.IsCompleted);
        Assert.DoesNotContain(Dialog(window).GetVisualDescendants().OfType<Button>(), b => !DialogButtonNames.Contains(b.Name));

        // Drei Knöpfe stehen untereinander und bleiben innerhalb der Karte (Aktion oben, Abbrechen unten).
        var buttons = DialogButtonNames.Select(n => DialogButton(window, n)).ToList();
        Assert.True(TopOf(window, buttons[0]) < TopOf(window, buttons[1]) && TopOf(window, buttons[1]) < TopOf(window, buttons[2]));
        Assert.All(buttons, b => Assert.True(LeftOf(window, b) + b.Bounds.Width <= LeftOf(window, Dialog(window)) + Dialog(window).Bounds.Width - 24 + 0.5, b.Name));

        // Drei Knöpfe und das Feld: Tab läuft im Kreis durch alle vier und verlässt den Dialog nie.
        var order = new List<string?>();
        for (var i = 0; i < 9; i++)
        {
            Press(window, PhysicalKey.Tab);
            await Pump();
            order.Add((Focused(window) as Control)?.Name);
        }

        Assert.All(order, name => Assert.True(name is not null && (DialogButtonNames.Contains(name) || name == "DialogInput"), name));
        Assert.Equal(4, order.Distinct().Count());

        // Eine zweite Rückfrage auf dem Dialog gibt es nicht: sie gilt als „Abbrechen“.
        var second = await main.DialogHost.AskAsync(DeleteRequest);
        Assert.Equal(ConfirmChoice.Cancelled, second.Choice);
        Assert.Equal("Termin stornieren?", main.DialogHost.Current!.Title);

        main.DialogHost.Current.AlternativeCommand.Execute(null);
        Assert.Equal(ConfirmChoice.Alternative, (await asking).Choice);
        return calendar;
    });

    [Fact]
    public Task Fokus_kehrt_nach_dem_Schliessen_zum_Ausloeser_zurueck() => Render("hell", "rueckfrage-fokus", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        await Pump();
        var trigger = Named<Button>(window, "HeadTeam");
        trigger.Focus(NavigationMethod.Tab);
        await Pump();
        Assert.Same(trigger, Focused(window));

        var asking = main.DialogHost.AskAsync(DeleteRequest);
        await Pump(Settling);
        Assert.NotSame(trigger, Focused(window));
        Press(window, PhysicalKey.Escape);
        await Pump(Settling);
        Assert.False((await asking).IsConfirmed);
        Assert.Same(trigger, Focused(window));
        return calendar;
    });

    [Fact]
    public Task Rueckfrage_mit_Feld_gibt_den_Text_zurueck() => Render("hell", "rueckfrage-grund", async (window, main) =>
    {
        await SignedInCalendar(main);
        await Pump();
        var request = new ConfirmRequest("Termin stornieren?", "Der Termin ist danach storniert.", "Stornieren", InputLabel: "Grund", InputText: "An der Rezeption storniert");
        var asking = main.DialogHost.AskAsync(request);
        await Pump(Settling);

        var box = Dialog(window).GetVisualDescendants().OfType<TextBox>().Single();
        Assert.Equal("An der Rezeption storniert", box.Text);
        box.Text = "Gast krank";
        DialogButton(window, "ConfirmButton").Command!.Execute(null);
        var answer = await asking;
        Assert.Equal(new ConfirmResult(ConfirmChoice.Confirmed, "Gast krank"), answer);
        return main.Content!;
    });

    [Fact]
    public Task Storno_Rueckfrage_mit_Feld_Grund_setzt_den_Fokus_beim_Oeffnen_ins_Feld() => Render("hell", "rueckfrage-grund-fokus", async (window, main) =>
    {
        await SignedInCalendar(main);
        await Pump();
        var request = new ConfirmRequest("Termin stornieren?", "Der Termin ist danach storniert.", "Stornieren", InputLabel: "Grund", InputText: "An der Rezeption storniert");
        var asking = main.DialogHost.AskAsync(request);
        await Pump(Settling);

        // Fluent 2: eine Rückfrage mit Feld fokussiert das Feld, die Marke steht am Ende des vorbelegten Grundes.
        var box = Dialog(window).GetVisualDescendants().OfType<TextBox>().Single(b => b.Name == "DialogInput");
        Assert.Same(box, Focused(window));
        Assert.Equal(box.Text!.Length, box.CaretIndex);
        Assert.Equal(0, box.SelectionEnd - box.SelectionStart);

        // Enter im Feld löst die zerstörende Aktion nicht aus und steigt nicht zum Fenster auf (dort feuerte sonst ein Standardknopf hinter dem Overlay).
        Press(window, PhysicalKey.Enter);
        await Pump();
        Assert.False(asking.IsCompleted);
        Assert.True(main.DialogHost.HasDialog);
        var enter = new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter, Source = box };
        box.RaiseEvent(enter);
        Assert.True(enter.Handled);

        Press(window, PhysicalKey.Escape);
        await Pump();
        Assert.Equal(ConfirmChoice.Cancelled, (await asking).Choice);
        return main.Content!;
    });

    [Fact]
    public async Task Ohne_Dialog_gilt_jede_Rueckfrage_als_Abbrechen_und_der_Host_haelt_hoechstens_einen()
    {
        var host = new ConfirmDialogHost();
        var first = host.AskAsync(DeleteRequest);
        Assert.True(host.HasDialog);
        Assert.Equal(ConfirmChoice.Cancelled, (await host.AskAsync(DeleteRequest)).Choice);
        host.Current!.ConfirmCommand.Execute(null);
        Assert.True((await first).IsConfirmed);
        Assert.False(host.HasDialog);
    }
}
