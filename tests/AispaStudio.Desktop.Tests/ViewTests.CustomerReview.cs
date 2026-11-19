using AispaStudio.Contracts;
using AispaStudio.Desktop.Controls;
using AispaStudio.Desktop.ViewModels;
using AispaStudio.Desktop.Views;
using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>Durchsicht der Kundenkarte (Befunde K2 bis K4, 27a bis 27e, 30a bis 30c, 32d, 35a, 45): eindeutige Überschriften, Beschriftungen und Namen.</summary>
public sealed partial class ViewTests
{
    [Fact]
    public Task Kundenkarte_Durchsicht_Ueberschriften_Namen_und_Hinweise() => Render("hell", "kunden-durchsicht", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        var shell = (ShellViewModel)main.Content!;
        await shell.ShowCustomerAsync(SpaSample.Customers[0].Id);
        await Pump();

        // 27e: Die rechte Spalte heißt „Termine“, nicht wie die Seite „Verlauf“.
        var timeline = window.GetVisualDescendants().OfType<StackPanel>().Single(p => p.Name == "Timeline");
        Assert.Contains("Termine", VisibleTexts(timeline));
        Assert.DoesNotContain("Verlauf", VisibleTexts(timeline));

        // 27c: Das Paket trägt sein Wort wie im Termin-Panel.
        Assert.Contains("Paket Hot-Stone-Massage: 7 von 10 offen", VisibleTexts(timeline));

        // 32d: Das Warnfeld sagt, worum es geht.
        var hint = AttentionHint(window);
        Assert.Contains("Zuverlässigkeit", VisibleTexts(hint));
        Assert.Contains("1× nicht erschienen · 1 späte Absage", VisibleTexts(hint));

        // K4: Das Symbol neben dem Namen hat Tooltip und Namen.
        var symbol = window.GetVisualDescendants().OfType<Icon>().Single(i => i.IsEffectivelyVisible && i.Data == Icons.Female);
        Assert.Equal("Anrede: Frau", ToolTip.GetTip(symbol));
        Assert.Equal("Anrede: Frau", Avalonia.Automation.AutomationProperties.GetName(symbol));

        // 27a: Ein Kennzeichen ohne Kreuz sagt im Tooltip, wer es aufheben darf.
        var flags = shell.Customers.Flags;
        Assert.All(flags.Where(f => !f.MayRemove), f => Assert.Contains("Aufheben", f.Tip));
        Assert.All(flags.Where(f => f.MayRemove), f => Assert.DoesNotContain("Aufheben", f.Tip));
        return calendar;
    });

    [Fact]
    public Task Kundenkarte_Kennzeichen_Formular_hat_eindeutige_Beschriftung_und_Pflichtstern() => Render("hell", "kunden-kennzeichen-formular", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        var shell = (ShellViewModel)main.Content!;
        await shell.ShowCustomerAsync(SpaSample.Customers[0].Id);
        await Pump();
        shell.Customers.AddFlagCommand.Execute(null);
        await Pump();

        // 30a: Abschnitt „Kennzeichen“ und Feld „Neues Kennzeichen“ stehen nicht doppelt übereinander; vorbelegt ist ein noch nicht gesetztes Kennzeichen.
        var section = VisibleSections(window).Single(s => s.Name == "FlagsSection");
        var texts = VisibleTexts(section);
        Assert.Equal(1, texts.Count(t => t == "Kennzeichen"));
        Assert.Contains("Neues Kennzeichen", texts);
        var set = shell.Customers.Flags.Select(f => f.Flag.Kind).ToList();
        Assert.DoesNotContain(shell.Customers.NewFlag!.Kind, set);

        // 30b: Der Knopf nennt, was er tut; der Hinweis ist ein vollständiger Satz.
        Assert.Contains(section.GetVisualDescendants().OfType<Button>(), b => b.IsEffectivelyVisible && b.Content as string == "Kennzeichen setzen");
        var reason = section.GetVisualDescendants().OfType<FormField>().Single(f => f.Label == "Grund");
        Assert.Equal("Bei „Nicht mehr buchen“ ist der Grund Pflicht. Der Gast kann ihn einsehen, deshalb sachlich schreiben.", reason.Help);

        // 30c: Der Pflichtstern erscheint, sobald „Nicht mehr buchen“ gewählt ist.
        Assert.False(reason.IsRequired);
        shell.Customers.NewFlag = shell.Customers.FlagChoices.Single(c => c.Kind == CustomerFlagKind.DoNotBook);
        await Pump();
        Assert.True(reason.IsRequired);
        return calendar;
    });

    [Fact]
    public Task Kundenkarte_ohne_Besuche_zeigt_keine_Kennzahlen_und_keine_Sterne() => Render("hell", "kunden-neu-gast", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        var shell = (ShellViewModel)main.Content!;

        // 32b: Der Gast war nie da: keine Zeile „7 Besuche …“, kein Stammgast, kein Warnfeld, und rechts steht „Noch keine Termine.“
        await shell.ShowCustomerAsync(EmptyGuest.Id);
        await Pump();
        Assert.False(shell.Customers.HasStars);
        Assert.False(shell.Customers.HasAttention);
        Assert.DoesNotContain(VisibleTexts(CustomerCardBorder(window)), t => t is not null && t.StartsWith("7 Besuche", StringComparison.Ordinal));
        Assert.Contains("Noch keine Termine.", VisibleTexts(CustomerCardBorder(window)));
        return calendar;
    }, EmptyGuestApi());

    [Fact]
    public Task Kundenkarte_Noch_nicht_erfasst_steht_als_Zeile_ueber_den_Links() => Render("hell", "kunden-noch-nicht-erfasst", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        var shell = (ShellViewModel)main.Content!;
        await shell.ShowCustomerAsync(EmptyGuest.Id);
        await Pump();

        // K3: Die Bezeichnung hat eine eigene Zeile, die Links brechen darunter gleichmäßig um.
        var block = window.GetVisualDescendants().OfType<StackPanel>().Single(p => p.Name == "MissingBlock");
        var label = block.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Text == "Noch nicht erfasst:");
        var links = block.GetVisualDescendants().OfType<Button>().Where(b => b.IsEffectivelyVisible).ToList();
        Assert.Equal(4, links.Count);
        Assert.All(links, b => Assert.True(b.TranslatePoint(new Point(0, 0), block)!.Value.Y >= label.Bounds.Bottom - 0.5));
        Assert.Equal(0, links[0].TranslatePoint(new Point(0, 0), block)!.Value.X, 0.5);
        return calendar;
    }, EmptyGuestApi());

    [Fact]
    public Task Kundendaten_Maske_nennt_die_Vorschauzeile_Auf_Belegen() => Render("hell", "kunden-vorschau", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        var shell = (ShellViewModel)main.Content!;
        await shell.ShowCustomerAsync(SpaSample.Customers[0].Id);
        await Pump();
        shell.Customers.EditContactCommand.Execute(null);
        await Pump();

        // 35a: „Anschrift“ ist die Überschrift der Adresse; die Vorschau des Namens heißt „Auf Belegen“.
        var form = window.GetVisualDescendants().OfType<CustomerContactForm>().Single(f => f.IsEffectivelyVisible);
        var runs = form.GetVisualDescendants().OfType<TextBlock>().SelectMany(t => t.Inlines ?? []).OfType<Avalonia.Controls.Documents.Run>().Select(r => r.Text).ToList();
        Assert.Contains("Auf Belegen: ", runs);
        Assert.DoesNotContain("Anschrift: ", runs);
        return calendar;
    });
}
