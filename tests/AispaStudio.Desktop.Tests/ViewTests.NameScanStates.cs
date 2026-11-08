using AispaStudio.Contracts;
using AispaStudio.Desktop.Controls;
using AispaStudio.Desktop.ViewModels;
using AispaStudio.Desktop.Views;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>
/// Namens-Scan je Zustand (R02 Slice 37, W65): der Scan aus <c>Jedes_bedienbare_Element_jeder_Seite_hat_einen_lesbaren_Namen</c> prüft nur die Ausgangszustände;
/// hier laufen dieselben Regeln über Seitenleisten, Dialoge und Bearbeiten-Zustände, in denen die Bedienelemente erst entstehen.
/// </summary>
public sealed partial class ViewTests
{
    /// <summary>Alle sichtbaren, bedienbaren Elemente unter <paramref name="root"/>, deren Name leer, ein Typname oder ein Modellname ist; gibt die Zahl der geprüften Elemente zurück.</summary>
    private static int ScanNames(Visual root, string state, List<string> problems)
    {
        var scanned = 0;
        foreach (var control in root.GetVisualDescendants().OfType<Control>().Where(c => c is Button or ToggleButton or CheckBox or RadioButton or TextBox or ComboBox or ListBoxItem or MenuItem or Slider or TabItem))
        {
            if (!control.IsEffectivelyVisible || !control.Focusable)
            {
                continue;
            }

            scanned++;
            if (HasUnreadableName(control, out var name))
            {
                problems.Add($"{state}: {control.GetType().Name}#{control.Name} [{string.Join(" ", control.Classes)}] \"{name}\"");
            }
        }

        return scanned;
    }

    private static void AssertNoUnreadableNames(List<string> problems) =>
        Assert.True(problems.Count == 0, $"{problems.Count} Elemente:{Environment.NewLine}{string.Join(Environment.NewLine, problems.Distinct().Order(StringComparer.Ordinal))}");

    /// <summary>Gegenprobe: Given ein Knopf mit einem Layout statt Text und ohne Namen When der Scan läuft Then meldet er ihn (und nennt einen benannten Knopf nicht).</summary>
    [Fact]
    public Task Namens_Scan_meldet_einen_Knopf_ohne_Namen() => Render("hell", "namen-gegenprobe", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        var host = new StackPanel
        {
            Children =
            {
                new Button { Content = new StackPanel() },
                new Button { Content = new StackPanel(), [Avalonia.Automation.AutomationProperties.NameProperty] = "Benannt" },
            },
        };
        var problems = new List<string>();
        var probe = new Window { Content = host, Width = 300, Height = 200 };
        probe.Show();
        await Pump(Settling);
        Assert.Equal(2, ScanNames(host, "Gegenprobe", problems));
        probe.Close();
        Assert.Single(problems);
        return calendar;
    });

    /// <summary>Given das Termin-Panel (zu und mit aufgeklapptem Kunden) When der Scan läuft Then hat jedes Bedienelement einen lesbaren Namen.</summary>
    [Fact]
    public Task Namens_Scan_Termin_Panel_mit_aufgeklapptem_Kunden() => Render("hell", "namen-termin", async (window, main) =>
    {
        var problems = new List<string>();
        var calendar = await SignedInCalendar(main);
        calendar.OpenAppointment(SpaSample.Selected);
        await Pump(Settling);
        for (var i = 0; i < 40 && !calendar.Selected!.CustomerSection!.ShowStats; i++)
        {
            await Pump();
        }

        Assert.True(calendar.Selected!.CustomerSection!.ShowStats);
        var panel = SidePanelOf(window);
        Assert.True(ScanNames(panel, "Termin", problems) > 5);
        ClickCenter(window, window.GetVisualDescendants().OfType<AccordionHeader>().Single(h => h.Name == "CustomerHeader"));
        await Pump(Settling);
        Assert.True(ScanNames(panel, "Termin mit Kunde", problems) > 8);
        AssertNoUnreadableNames(problems);
        return calendar;
    });

    /// <summary>Given das Eingabefenster „Neuer Eintrag“ When jede Art gewählt ist Then hat jedes Bedienelement einen lesbaren Namen.</summary>
    [Fact]
    public Task Namens_Scan_Neuer_Eintrag_in_jeder_Art() => Render("hell", "namen-neuer-eintrag", async (window, main) =>
    {
        var problems = new List<string>();
        var calendar = await SignedInCalendar(main);
        calendar.StartBookingAt(10, calendar.Hours.Single(h => h.Label == "12:00").Top + 1);
        var entry = calendar.NewEntry!;
        Assert.Equal(6, entry.Choices.Count);
        foreach (var kind in entry.Choices.Select(c => c.Kind).ToList())
        {
            Assert.True(entry.Select(kind));
            if (kind == NewEntryKind.Appointment)
            {
                calendar.Booking!.ShowNewCustomer = true;
            }

            await Pump(Settling);
            Assert.True(ScanNames(SidePanelOf(window), $"Neuer Eintrag {kind}", problems) > 3, kind.ToString());
        }

        AssertNoUnreadableNames(problems);
        return calendar;
    });

    /// <summary>Given der Rückfragedialog When der Scan läuft Then haben seine Knöpfe lesbare Namen.</summary>
    [Fact]
    public Task Namens_Scan_Rueckfragedialog() => Render("hell", "namen-rueckfrage", async (window, main) =>
    {
        var problems = new List<string>();
        var calendar = await SignedInCalendar(main);
        var asking = main.DialogHost.AskAsync(DeleteRequest);
        await Pump(Settling);
        Assert.Equal(2, ScanNames(Dialog(window), "Rückfrage", problems));
        main.DialogHost.Current!.CancelCommand.Execute(null);
        await asking;

        // Die Rückfrage mit Alternative und Feld, wie beim Stornieren einer Serie.
        asking = main.DialogHost.AskAsync(new ConfirmRequest("Termin stornieren?", "Der Termin wird abgesagt.", "Stornieren", AlternativeText: "Diesen und alle folgenden", InputLabel: "Grund"));
        await Pump(Settling);
        Assert.True(ScanNames(Dialog(window), "Rückfrage mit Feld", problems) >= 4);
        main.DialogHost.Current!.CancelCommand.Execute(null);
        await asking;
        AssertNoUnreadableNames(problems);
        return calendar;
    });

    /// <summary>Given die Kundenseite mit jeweils einer Bearbeitung offen (Karten und Stammdaten) When der Scan läuft Then hat jedes Bedienelement einen lesbaren Namen.</summary>
    [Theory]
    [MemberData(nameof(CustomerEditors))]
    public Task Namens_Scan_Kundenkarte_im_Bearbeiten_Zustand(string editor) => Render("hell", $"namen-kunde-{editor}", async (window, main) =>
    {
        var problems = new List<string>();
        var calendar = await SignedInCalendar(main);
        var shell = (ShellViewModel)main.Content!;
        await shell.ShowCustomerAsync(SpaSample.Customers[0].Id);
        await Pump();
        await OpenEditor(shell.Customers, editor);
        await Pump(Settling);
        Assert.Equal([editor], OpenEditors(shell.Customers));
        Assert.True(ScanNames(window.GetVisualDescendants().OfType<CustomersView>().First(), $"Kunde {editor}", problems) > 5);
        AssertNoUnreadableNames(problems);
        return calendar;
    }, WithPlainCustomer());

    /// <summary>Given die Kassa vor und nach dem Bezahlen mit der Maske „Rechnungsempfänger“ When der Scan läuft Then hat jedes Bedienelement einen lesbaren Namen.</summary>
    [Fact]
    public Task Namens_Scan_Kassa_mit_Rechnung() => Render("hell", "namen-kassa-rechnung", async (window, main) =>
    {
        var problems = new List<string>();
        var calendar = await SignedInCalendar(main);
        var shell = (ShellViewModel)main.Content!;
        var pos = shell.Pos;
        await shell.CheckoutAsync(SpaSample.Paying);
        await Pump();
        var page = window.GetVisualDescendants().OfType<PosView>().First();
        Assert.True(ScanNames(page, "Kassieren", problems) > 5);
        pos.Payments.First(p => p.Method == PaymentMethod.Card).Amount = "89";
        await pos.PayCommand.ExecuteAsync(null);
        await pos.StartInvoiceCommand.ExecuteAsync(pos.Receipt!);
        await Pump();
        Assert.True(pos.IsInvoicing);
        Assert.True(ScanNames(page, "Beleg mit Rechnung", problems) > 3);
        AssertNoUnreadableNames(problems);
        return calendar;
    });
}
