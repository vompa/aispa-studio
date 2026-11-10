using AispaStudio.Contracts;
using AispaStudio.Desktop.Controls;
using AispaStudio.Desktop.ViewModels;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>Kundenliste mit Knopf „Exportieren“ und Kundenkarte mit Werbe-Einwilligung (Export/Import, Aufgabe B): Menü je Rolle, Abschnitt in Hell und Dunkel, Handbuchbilder.</summary>
public sealed partial class ViewTests
{
    private static readonly MarketingConsentDto ReceptionConsent = new(MarketingConsentState.Confirmed, "b.mueller@example.at", "de",
        new DateTimeOffset(2026, 11, 9, 9, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 11, 9, 9, 0, 0, TimeSpan.Zero), null, "Rezeption", "Lena Berger");

    [Theory]
    [InlineData("Rezeption", 4)]
    [InlineData("Inhaberin", 5)]
    public Task Kundenliste_Export_Menue_zeigt_die_Newsletter_Liste_nur_der_Leitung(string role, int entries) => Render("hell", role == "Inhaberin" ? "kunden-export-menue-inhaberin" : "kunden-export-menue-rezeption", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        var shell = (ShellViewModel)main.Content!;
        await shell.ShowCustomerAsync(SpaSample.Customers[0].Id);
        await Pump();

        var export = window.GetVisualDescendants().OfType<ExportButton>().Single();
        Assert.True(export.IsEffectivelyVisible);
        Assert.True(export.Bounds.Right <= window.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "NewButton").Bounds.Left + 1, "Exportieren steht vor dem Hauptknopf");
        export.Flyout!.ShowAt(export);
        await Pump();

        var items = window.GetVisualDescendants().OfType<MenuItem>().Where(i => i.IsEffectivelyVisible && i.Bounds.Height > 0).Select(i => i.Header as string).ToList();
        Assert.Equal(entries, items.Count);
        Assert.Equal(["Excel", "CSV", "Portal-CSV", "vCard"], items.Take(4));
        Assert.Equal(entries == 5, items.Contains("Newsletter-Liste"));
        export.Flyout.Hide();

        var more = window.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "MoreButton");
        more.Flyout!.ShowAt(more);
        await Pump();
        Assert.Contains("Kontakt als vCard", window.GetVisualDescendants().OfType<MenuItem>().Where(i => i.IsEffectivelyVisible && i.Bounds.Height > 0).Select(i => i.Header as string));
        more.Flyout.Hide();
        return calendar;
    }, role == "Rezeption" ? ReceptionApi() : Api());

    [Theory]
    [InlineData("hell")]
    [InlineData("dunkel")]
    public Task Kundenkarte_zeigt_die_Werbe_Einwilligung_mit_Datum_und_Stift(string theme) => Render(theme, "kunden-einwilligung", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        var shell = (ShellViewModel)main.Content!;
        await shell.ShowCustomerAsync(SpaSample.Customers[0].Id);
        await Pump();

        var section = window.GetVisualDescendants().OfType<Border>().Single(b => b.Name == "ConsentSection");
        Assert.True(section.IsEffectivelyVisible);
        var text = window.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Name == "ConsentText");
        Assert.Equal("Ja, am Empfang erfasst am 9.11.2026 von Lena Berger", text.Text);
        Assert.True(Contrast(ColorOf(text.Foreground), ColorOf(window, "SurfaceBrush")) >= 4.5);
        var edit = window.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "EditConsentButton");
        Assert.True(edit.IsEffectivelyVisible);

        edit.Command!.Execute(null);
        await Pump();
        var box = window.GetVisualDescendants().OfType<ComboBox>().Single(c => c.Name == "ConsentBox");
        Assert.True(box.IsEffectivelyVisible);
        Assert.Equal("Ja", box.SelectedItem?.ToString());
        Assert.Equal(["Ja", "Nein", "Nicht gefragt"], box.Items.Cast<object>().Select(i => i.ToString()));
        shell.Customers.CancelConsentCommand.Execute(null);
        await Pump();
        Assert.True(text.IsEffectivelyVisible);
        return calendar;
    }, Api().On($"GET /api/customers/{SpaSample.Customers[0].Id}/marketing-consent", ReceptionConsent));
}
