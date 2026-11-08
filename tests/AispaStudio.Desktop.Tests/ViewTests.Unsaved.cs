using AispaStudio.Desktop.ViewModels;
using AispaStudio.Desktop.Views;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>Fluent 2 F20: Schließen einer Seitenleiste mit ungespeicherten Eingaben fragt „Änderungen verwerfen?“ (Design-Brief 5.27).</summary>
public sealed partial class ViewTests
{
    /// <summary>Das Kreuz oben rechts in der Seitenleiste.</summary>
    private static Button PanelCloseButton(Window window) =>
        window.GetVisualDescendants().OfType<Button>().First(b => b.IsEffectivelyVisible && (ToolTip.GetTip(b) as string)?.StartsWith("Schließen", StringComparison.Ordinal) == true
            && b.GetVisualAncestors().Any(a => a is Border { Name: "SidePanel" }));

    [Fact]
    public Task Wunsch_mit_Text_fragt_beim_Schliessen_Aenderungen_verwerfen_und_Weiter_bearbeiten_laesst_ihn_offen() => Render("hell", "rueckfrage-verwerfen", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        Assert.True(calendar.HandleKey(Key.K, KeyModifiers.Control));
        await Pump();
        Assert.False(calendar.Request!.HasUnsavedChanges);

        // Ohne Eingabe schließt das Kreuz sofort, ohne Rückfrage.
        ClickCenter(window, PanelCloseButton(window));
        await Pump();
        Assert.False(main.DialogHost.HasDialog);
        Assert.Null(calendar.Request);

        // Mit Eingabe fragt es „Änderungen verwerfen?“.
        Assert.True(calendar.HandleKey(Key.K, KeyModifiers.Control));
        calendar.Request!.Text = "heute ab 17 Uhr eine kräftige Massage";
        await Pump();
        ClickCenter(window, PanelCloseButton(window));
        await Pump(Settling);
        var dialog = Assert.IsType<ConfirmDialogViewModel>(main.DialogHost.Current);
        Assert.Equal("Änderungen verwerfen?", dialog.Title);
        Assert.Equal("Verwerfen", dialog.ConfirmText);
        Assert.Equal("Weiter bearbeiten", dialog.CancelText);
        Assert.True(dialog.IsDestructive);
        AssertDialogRules(dialog.Request);
        // Der Anfangsfokus liegt auf „Weiter bearbeiten“.
        Assert.Same(DialogButton(window, "CancelButton"), Focused(window));
        SaveShot(window, "rueckfrage-verwerfen-dialog", "hell");

        // Esc ist „Weiter bearbeiten“: der Wunsch bleibt mit seinem Text offen.
        Press(window, PhysicalKey.Escape);
        await Pump(Settling);
        Assert.False(main.DialogHost.HasDialog);
        Assert.NotNull(calendar.Request);
        Assert.Equal("heute ab 17 Uhr eine kräftige Massage", calendar.Request!.Text);

        // Esc in der Seitenleiste fragt ebenso; „Verwerfen“ schließt die Seite.
        calendar.HandleKey(Key.Escape, KeyModifiers.None);
        await Pump(Settling);
        Assert.True(main.DialogHost.HasDialog);
        DialogButton(window, "ConfirmButton").Command!.Execute(null);
        await Pump(Settling);
        Assert.Null(calendar.Request);
        Assert.False(calendar.HasPanel);
        return calendar;
    });

    [Fact]
    public async Task Eingaben_in_Seitenleisten_zaehlen_als_Aenderungen_Zeiten_Text_Gast_und_Kundendaten()
    {
        await Session.Dispatch(async () =>
        {
            var main = new MainViewModel(Api().Client(), time: new SampleTime());
            var window = new MainWindow { DataContext = main, Width = 1440, Height = 900 };
            window.Show();
            var calendar = await SignedInCalendar(main);
            await Pump();

            // Neue Notiz: Text oder geänderte Zeit.
            calendar.OpenNoteAt(DayLayout.ColumnLeft(2, calendar.Scale) + 20, calendar.Hours.Single(h => h.Label == "16:00").Top + 4);
            var entry = calendar.Entry!;
            Assert.False(entry.HasUnsavedChanges);
            entry.Text = "Herr Lang ruft zurück";
            Assert.True(entry.HasUnsavedChanges);
            entry.Text = "";
            entry.StartText = "16:30";
            Assert.True(entry.HasUnsavedChanges);
            entry.StartText = "16:00";
            Assert.False(entry.HasUnsavedChanges);
            calendar.HandleKey(Key.Escape, KeyModifiers.None);

            // Neuer Termin: gewählter Gast oder Notiz.
            calendar.StartBooking(calendar.Columns[1], 330 * DayLayout.PixelsPerMinute);
            var booking = calendar.Booking!;
            Assert.False(booking.HasUnsavedChanges);
            booking.Note = "Wunsch";
            Assert.True(booking.HasUnsavedChanges);
            booking.Note = null;
            booking.Customer = SpaSample.Customers[0];
            Assert.True(booking.HasUnsavedChanges);
            calendar.HandleKey(Key.Escape, KeyModifiers.None);
            main.DialogHost.Current?.CancelCommand.Execute(null);

            // Termin: Zeiten geändert, Kundendaten geändert.
            calendar.OpenAppointment(SpaSample.Selected);
            var panel = calendar.Selected!;
            Assert.False(panel.HasUnsavedChanges);
            panel.BeginEditTimes();
            Assert.False(panel.HasUnsavedChanges);
            panel.TimesPreparation += 5;
            Assert.True(panel.HasUnsavedChanges);
            panel.CancelEditTimes();
            Assert.False(panel.HasUnsavedChanges);
            window.Close();
            return true;
        }, CancellationToken.None);
    }

    [Fact]
    public async Task Kundendaten_im_Formular_zaehlen_als_Aenderung_erst_nach_einem_geaenderten_Feld()
    {
        await Session.Dispatch(() =>
        {
            var form = new CustomerContactFormViewModel(Api().Client(), SpaSample.Customers[0], [AispaStudio.Contracts.FiscalCountry.AT], _ => Task.CompletedTask, () => { });
            Assert.False(form.HasChanges);
            form.Phone += "1";
            Assert.True(form.HasChanges);
            form.Phone = form.Phone[..^1];
            Assert.False(form.HasChanges);
            return Task.FromResult(true);
        }, CancellationToken.None);
    }
}
