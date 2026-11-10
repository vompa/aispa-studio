using AispaStudio.Desktop.Controls;
using AispaStudio.Desktop.ViewModels;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>„+ Termin“ › „Terminliste exportieren …“ (Export/Import, Aufgabe C): Menüeintrag, Dialog mit Zeitraum und Format in Hell und Dunkel, Fehler am Feld, Handbuchbild.</summary>
public sealed partial class ViewTests
{
    [Theory]
    [InlineData("hell")]
    [InlineData("dunkel")]
    public Task Terminliste_exportieren_fragt_Zeitraum_und_Format(string theme) => Render(theme, "terminliste-exportieren", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        await Pump();

        var split = window.GetVisualDescendants().OfType<SplitButton>().First(b => b.IsEffectivelyVisible && b.Flyout is MenuFlyout);
        split.Flyout!.ShowAt(split);
        await Pump();
        var items = window.GetVisualDescendants().OfType<MenuItem>().Where(i => i.IsEffectivelyVisible && i.Bounds.Height > 0).ToList();
        Assert.Contains(items, i => i.Name == "ExportAppointmentsItem" && i.Header as string == "Terminliste exportieren …");
        split.Flyout.Hide();
        await Pump();

        var asking = main.DialogHost.ShowAsync(done => new AppointmentListDialogViewModel("Wien", new DateOnly(2027, 3, 1), new DateOnly(2027, 3, 7), done));
        await Pump(Settling);
        var dialog = (AppointmentListDialogViewModel)main.DialogHost.Current!;
        var card = Dialog(window);
        Assert.Equal(420, card.Bounds.Width, 0.5);
        Assert.Equal("Terminliste exportieren?", card.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Name == "DialogTitle").Text);
        var fields = card.GetVisualDescendants().OfType<DateField>().ToList();
        Assert.Equal(["ListFromField", "ListUntilField"], fields.Select(f => f.Name));
        Assert.True(fields.All(f => f.IsEffectivelyVisible));
        Assert.True(card.GetVisualDescendants().OfType<RadioButton>().Single(r => r.Name == "ListExcel").IsChecked);
        var confirm = DialogButton(window, "ConfirmButton");
        Assert.Equal("Exportieren", confirm.Content);
        Assert.Contains("primary", confirm.Classes);
        Assert.Same(confirm, Focused(window));
        FakeDialogs.AssertRules(dialog.Request);
        SaveShot(window, "terminliste-dialog", theme);

        // Ende vor Beginn: der Dialog bleibt offen, der Fehler steht am Feld „Bis“.
        dialog.Until = new DateOnly(2027, 2, 27);
        dialog.ExportCommand.Execute(null);
        await Pump();
        Assert.True(main.DialogHost.HasDialog);
        var texts = card.GetVisualDescendants().OfType<TextBlock>().Where(t => t.IsEffectivelyVisible).Select(t => t.Text).ToList();
        Assert.Contains(AppointmentListDialogViewModel.EndBeforeStart, texts);

        main.DialogHost.Current!.CancelCommand.Execute(null);
        Assert.False((await asking).IsConfirmed);
        await Pump();
        Assert.False(main.DialogHost.HasDialog);
        return calendar;
    });
}
