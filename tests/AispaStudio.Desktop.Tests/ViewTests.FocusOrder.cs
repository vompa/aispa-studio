using AispaStudio.Desktop.Controls;
using AispaStudio.Desktop.ViewModels;
using AispaStudio.Desktop.Views;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>
/// Fluent 2 F28: Narrator-Prüfung als Regel. Für jede Maske eine Fokusreihenfolge als Liste im PR-Text; hier die Reihenfolge für Termin-Panel, Kundendaten-Maske und Rückfragedialog,
/// ermittelt durch Tab-Druck im Headless-Fenster, dazu Name, Rolle, Pflicht und Hilfe der Felder, wie sie ein Screenreader über UI Automation erhält.
/// </summary>
public sealed partial class ViewTests
{
    /// <summary>Beschreibung eines Elements, wie es der Screenreader hört: „Name (Rolle)“, bei Pflichtfeldern mit „, Pflicht“.</summary>
    private static string Announced(IInputElement? element)
    {
        if (element is not Control control)
        {
            return "(kein Element)";
        }

        var peer = ControlAutomationPeer.CreatePeerForElement(control);
        var name = peer.GetName();
        if (string.IsNullOrWhiteSpace(name))
        {
            name = control is ContentControl { Content: string text } ? text
                : control.GetVisualDescendants().OfType<TextBlock>().FirstOrDefault(t => !string.IsNullOrWhiteSpace(t.Text))?.Text
                ?? ToolTip.GetTip(control) as string ?? control.Name ?? "";
        }

        var required = AutomationProperties.GetIsRequiredForForm(control) ? ", Pflicht" : "";
        return $"{name} ({peer.GetAutomationControlType()}){required}";
    }

    /// <summary>Drückt Tab, bis der Fokus <paramref name="inside"/> verlässt oder zum Anfang zurückkehrt; gibt die Folge der Ansagen zurück, beginnend mit dem aktuellen Fokus.</summary>
    private static async Task<List<string>> TabOrder(Window window, Control inside, int limit = 60)
    {
        var order = new List<string>();
        var first = Focused(window);
        order.Add(Announced(first));
        for (var step = 0; step < limit; step++)
        {
            Press(window, PhysicalKey.Tab);
            await Pump(2);
            var current = Focused(window);
            if (ReferenceEquals(current, first) || current is not Visual visual || !(ReferenceEquals(inside, visual) || inside.IsVisualAncestorOf(visual)))
            {
                break;
            }

            order.Add(Announced(current));
        }

        return order;
    }

    private static void WriteFocusOrder(string name, IEnumerable<string> order)
    {
        if (Environment.GetEnvironmentVariable("AISPA_FOCUS_ORDER") is not { Length: > 0 } folder)
        {
            return;
        }

        Directory.CreateDirectory(folder);
        File.WriteAllLines(Path.Combine(folder, $"{name}.txt"), order.Select((entry, index) => $"{index + 1}. {entry}"));
    }

    [Fact]
    public Task Fokusreihenfolge_Termin_Panel() => Render("hell", "fokus-termin", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        calendar.OpenAppointment(SpaSample.Selected);
        await Pump(Settling);
        var panel = window.GetVisualDescendants().OfType<Border>().Single(b => b.Name == "SidePanel");
        for (var i = 0; i < 40 && !calendar.Selected!.CustomerSection!.ShowStats; i++)
        {
            await Pump();
        }

        Assert.True(calendar.Selected!.CustomerSection!.ShowStats);
        var firstInPanel = RegionNavigation.Target(panel)!;
        firstInPanel.Focus(NavigationMethod.Tab);
        await Pump();
        var order = await TabOrder(window, panel);
        WriteFocusOrder("termin-panel", order);
        // Kopf (Gast, Kundendaten, Schließen), Termin-Infos und Notiz, gleich danach der Aktionsblock (Hauptaktion, Verschieben, Stornieren, zuletzt „Mehr“; Auftraggeber, 05.11.2026),
        // dann der Kopf des Abschnitts Kunde mit den Kennzahlen als einem Stopp, der Kopf des Abschnitts Verlauf (Slice 90) und die Reiter unten (Brief 5.12, Punkt 9; Slice 75b).
        Assert.Equal(["Kundenübersicht (Button)", "Kundendaten ergänzen (Button)", "Schließen (Button)", "Raum-Infos Raum Lotus (Button)", "Notiz ändern (Button)",
            "Einchecken (Button)", "Verschieben … (Button)", "Termin stornieren … (Button)", "Mehr (Button)", "Kunde (Button)", "Besuche gesamt 14 (Text)", "Verlauf (Button)", "Termin (TabItem)"], order);
        return calendar;
    });

    /// <summary>
    /// Given das Termin-Panel mit geladenen Kennzahlen When Tab auf „Kunde“ und einmal weiter geht Then ist die erste Kennzahl ein Stopp mit dem Tooltip als Hilfetext,
    /// → wechselt zur nächsten Kennzahl, und Tab verlässt die Gruppe (W-Muster 12: Kennzahlen nur im Tooltip).
    /// </summary>
    [Fact]
    public Task Kennzahlen_im_Abschnitt_Kunde_sind_ein_Tab_Stopp_mit_Pfeilen() => Render("hell", "fokus-kennzahlen", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        calendar.OpenAppointment(SpaSample.Selected);
        await Pump(Settling);
        var section = calendar.Selected!.CustomerSection!;
        for (var i = 0; i < 40 && !section.ShowStats; i++)
        {
            await Pump();
        }

        Assert.True(section.ShowStats);
        var header = window.GetVisualDescendants().OfType<AccordionHeader>().Single(h => h.Name == "CustomerHeader");
        var members = header.GetVisualDescendants().OfType<StackPanel>().Where(ToolTipBehavior.GetReachable).ToList();
        Assert.Equal([true, false, false, false], members.Select(m => m.IsTabStop));
        Assert.All(members, m => Assert.True(m.Focusable));
        header.Focus(NavigationMethod.Tab);
        await Pump();
        Press(window, PhysicalKey.Tab);
        await Pump(2);
        var visits = (Control)Focused(window)!;
        Assert.Equal("Besuche gesamt 14 (Text)", Announced(visits));
        Assert.Equal("Erledigte Besuche insgesamt", ControlAutomationPeer.CreatePeerForElement(visits).GetHelpText());
        // Der Screenreader liest je Kennzahl einen Text; die inneren Zeilen sind für ihn ausgeblendet (keine Doppelansage).
        Assert.All(visits.GetVisualDescendants().OfType<TextBlock>(), t => Assert.False(ControlAutomationPeer.CreatePeerForElement(t).IsContentElement()));

        // Enter und Leertaste auf einer Kennzahl klappen den Abschnitt nicht um, obwohl sie in seinem Kopf (einem ToggleButton) liegt.
        var wasOpen = section.IsOpen;
        Press(window, PhysicalKey.Enter);
        await Pump(2);
        Press(window, PhysicalKey.Space);
        await Pump(2);
        Assert.Equal(wasOpen, section.IsOpen);
        Assert.Same(visits, Focused(window));

        Press(window, PhysicalKey.ArrowRight);
        await Pump(2);
        var last = (Control)Focused(window)!;
        Assert.Equal($"Letzter Besuch {section.LastVisitText} (Text)", Announced(last));
        Assert.Equal(section.LastVisitTip, ControlAutomationPeer.CreatePeerForElement(last).GetHelpText());

        // Der Tab-Stopp wandert mit: Umschalt+Tab führt zum Kopf, Tab zurück auf dieselbe Kennzahl.
        Press(window, PhysicalKey.Tab, RawInputModifiers.Shift);
        await Pump(2);
        Assert.Same(header, Focused(window));
        Press(window, PhysicalKey.Tab);
        await Pump(2);
        Assert.Same(last, Focused(window));

        Press(window, PhysicalKey.ArrowLeft);
        await Pump(2);
        Assert.Same(visits, Focused(window));
        Press(window, PhysicalKey.ArrowLeft);
        await Pump(2);
        Assert.Same(visits, Focused(window));

        // Tab verlässt die Gruppe, die übrigen Kennzahlen sind keine eigenen Stopps; nächster Stopp ist der Kopf des Abschnitts Verlauf (Slice 90).
        Press(window, PhysicalKey.Tab);
        await Pump(2);
        Assert.Equal("Verlauf (Button)", Announced(Focused(window)));
        WriteFocusOrder("termin-panel-kennzahlen", [Announced(header), Announced(visits), Announced(last), "Verlauf (Button)"]);
        return calendar;
    });

    /// <summary>
    /// Given die Kundenkarte mit Sternen When Tab durch den Kopf läuft Then sind Sterne, Adresszeile und Anrede-Zeile Stopps, die der Screenreader mit Namen und dem
    /// Tooltip als Hilfetext liest (W-Muster 12, W40).
    /// </summary>
    [Fact]
    public Task Fokusreihenfolge_Kundenkarte_Sterne_Adresszeile_und_Kontakt() => Render("hell", "fokus-kundenkarte-kopf", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        var shell = (ShellViewModel)main.Content!;
        await shell.ShowCustomerAsync(SpaSample.Customers[0].Id);
        await Pump(Settling);
        var customers = shell.Customers;
        Assert.True(customers.HasStars);
        Assert.NotNull(customers.SelectedAddressLine);
        // Seit R07 Slice 141 steht „Bearbeiten“ im Menü „…“ rechts oben; der Kopf beginnt mit dem Chip „★ Stammgast“.
        var starsChip = window.GetVisualDescendants().OfType<Border>().Single(b => b.Name == "StarsChip");
        starsChip.Focus(NavigationMethod.Tab);
        await Pump(2);
        var stars = (Control)Focused(window)!;
        Assert.Same(starsChip, stars);
        var page = window.GetVisualDescendants().OfType<CustomersView>().First();
        var order = await TabOrder(window, page);
        WriteFocusOrder("kundenkarte-kopf", order);
        Assert.Equal(
        [
            "Stammgast, 2 Sterne (Text)", $"{customers.SelectedAddressLine} (Text)",
            $"Telefon {customers.Selected!.Phone} kopieren (Button)", $"E-Mail {customers.Selected.Email} kopieren (Button)",
        ], order.Take(4));
        // Danach die Aktionen des Kopfs in Lesereihenfolge, vor „Gut zu wissen“.
        var book = order.IndexOf("Termin buchen (Button)");
        var more = order.IndexOf("Weitere Aktionen (Button)");
        var wishes = order.IndexOf("Wünsche ändern (Button)");
        Assert.True(book > 4 && more > book && (wishes < 0 || wishes > more), string.Join(" | ", order));
        Assert.Equal(customers.Insights!.StarsText, ControlAutomationPeer.CreatePeerForElement(stars).GetHelpText());
        var helps = page.GetVisualDescendants().OfType<TextBlock>().Where(t => t.Focusable).Select(t => ControlAutomationPeer.CreatePeerForElement(t).GetHelpText()).ToList();
        Assert.Contains("Adresszeile mit dem gültigen Namen: so steht es auf Rechnungen und Belegen.", helps);
        // Die Anrede steht nur noch im Tooltip am Namen (ADR 0094), mit Erklärung und Anrede je Land.
        Assert.Contains($"So beginnen Mails, Erinnerungen und Antworten der KI an diesen Gast:\n{customers.SelectedGreeting}", helps);
        return calendar;
    });

    [Fact]
    public Task Fokusreihenfolge_Termin_Panel_ohne_Notiz_mit_aufgeklapptem_Kunden() => Render("hell", "fokus-termin-kunde", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        calendar.OpenAppointment(NoteFree);
        await Pump(Settling);
        var panel = window.GetVisualDescendants().OfType<Border>().Single(b => b.Name == "SidePanel");
        for (var i = 0; i < 40 && !calendar.Selected!.CustomerSection!.ShowStats; i++)
        {
            await Pump();
        }

        ClickCenter(window, window.GetVisualDescendants().OfType<AccordionHeader>().Single(h => h.Name == "CustomerHeader"));
        await Pump(Settling);
        RegionNavigation.Target(panel)!.Focus(NavigationMethod.Tab);
        await Pump();
        var order = await TabOrder(window, panel);
        WriteFocusOrder("termin-panel-kunde-auf", order);
        // Kopf, Raum, Link „Notiz hinzufügen“, der Aktionsblock (Auftraggeber, 05.11.2026), dann der Abschnitt Kunde: Kopf, Kennzahlen (ein Stopp), Zuletzt (vier Zeilen), Anruf, Kopieren,
        // WhatsApp, E-Mail, Kopieren, Kundenkarte.
        Assert.Equal(["Kundenübersicht (Button)", "Kundendaten ergänzen (Button)", "Schließen (Button)", "Raum-Infos Raum Lotus (Button)", "Notiz hinzufügen (Button)",
            "Einchecken (Button)", "Verschieben … (Button)", "Termin stornieren … (Button)", "Mehr (Button)", "Kunde (Button)", "Besuche gesamt 14 (Text)"], order.Take(11));
        Assert.Equal(4, order.Count(o => o.Contains(" bei ", StringComparison.Ordinal)));
        var card = order.IndexOf("Kundenkarte öffnen (Button)");
        Assert.True(card > 10, string.Join(" | ", order));
        return calendar;
    }, NoteApi(null));

    [Fact]
    public Task Fokusreihenfolge_Kundendaten_Maske() => Render("hell", "fokus-kundendaten", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        var shell = (ShellViewModel)main.Content!;
        await shell.ShowCustomerAsync(SpaSample.Customers[0].Id);
        await Pump();
        shell.Customers.EditContactCommand.Execute(null);
        await Pump(Settling);
        var form = window.GetVisualDescendants().OfType<CustomerContactForm>().First(f => f.IsEffectivelyVisible);
        // Initialfokus (masken.md R13): das erste Feld, nicht der Hilfe-Knopf der Gruppe.
        var start = Focused(window);
        Assert.NotNull(start);
        WriteFocusOrder("kundendaten-maske-initialfokus", [Announced(start)]);
        var order = await TabOrder(window, form);
        WriteFocusOrder("kundendaten-maske", order);
        // Der Fokus liegt beim Öffnen im ersten Feld; Tab folgt der Anzeige, die Knopfleiste kommt zuletzt.
        Assert.Equal(["Vorname (Edit), Pflicht", "Nachname (Edit), Pflicht", "Hilfe zu Kontakt (Button)", "Telefon (Edit)", "E-Mail (Edit)", "Sprache (ComboBox)",
            "Geburtstag (Edit)", "Straße und Hausnummer (Edit)", "PLZ (Edit)", "Ort (Edit)", "Land (Edit)", "Weitere Angaben (Button)", "Hilfe zu Weitere Angaben (Button)",
            "Speichern (Button)", "Abbrechen (Button)"], order);
        return calendar;
    }, WithPlainCustomer());

    [Fact]
    public Task Fokusreihenfolge_Rueckfragedialog() => Render("hell", "fokus-rueckfrage", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        await Pump();
        var asking = main.DialogHost.AskAsync(DeleteRequest);
        await Pump(Settling);
        var card = Dialog(window);
        var order = await TabOrder(window, card);
        WriteFocusOrder("rueckfragedialog", order);
        Assert.Equal(["Abbrechen (Button)", "Löschen (Button)"], order);

        // Tab bleibt im Dialog (Fokusfalle): auch nach dem letzten Knopf liegt der Fokus noch darin.
        Press(window, PhysicalKey.Tab);
        await Pump(2);
        Assert.True(card.IsVisualAncestorOf((Visual)Focused(window)!));
        main.DialogHost.Current!.CancelCommand.Execute(null);
        await asking;
        Assert.NotEmpty(order);
        return calendar;
    });

    /// <summary>Stichprobe der Kundendaten-Maske: Name, Rolle, Wert, Pflicht und Fehler, wie sie ein Screenreader über UI Automation erhält.</summary>
    [Fact]
    public Task Felder_der_Kundendaten_Maske_liefern_Name_Rolle_Wert_Pflicht_und_Fehler() => Render("hell", "fokus-felder", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        var shell = (ShellViewModel)main.Content!;
        await shell.ShowCustomerAsync(SpaSample.Customers[0].Id);
        await Pump();
        shell.Customers.EditContactCommand.Execute(null);
        await Pump(Settling);
        var form = window.GetVisualDescendants().OfType<CustomerContactForm>().First(f => f.IsEffectivelyVisible);
        var vm = shell.Customers.Contact!;
        TextBox Box(string name) => form.GetVisualDescendants().OfType<TextBox>().First(t => t.Name == name);

        // Pflichtfeld: Name aus der Beschriftung, Rolle „Edit“, Wert, Pflicht.
        var first = Box("FirstNameBox");
        var firstPeer = ControlAutomationPeer.CreatePeerForElement(first);
        Assert.Equal("Vorname", firstPeer.GetName());
        Assert.Equal(AutomationControlType.Edit, firstPeer.GetAutomationControlType());
        Assert.Equal(first.Text ?? "", firstPeer.GetProvider<IValueProvider>()!.Value);
        Assert.True(AutomationProperties.GetIsRequiredForForm(first));

        // Fehler: der Hilfetext des Feldes ist jetzt der Fehlersatz, die Meldung ist eine assertive Live-Region.
        vm.Country = "AT";
        vm.PostalCode = "12";
        await vm.SaveCommand.ExecuteAsync(null);
        await Pump();
        var postal = Box("PostalCodeBox");
        var postalPeer = ControlAutomationPeer.CreatePeerForElement(postal);
        Assert.Equal("PLZ", postalPeer.GetName());
        Assert.Equal("12", postalPeer.GetProvider<IValueProvider>()!.Value);
        Assert.Equal("Die PLZ hat vier Ziffern, z. B. 1010.", AutomationProperties.GetHelpText(postal));
        Assert.False(AutomationProperties.GetIsRequiredForForm(postal));
        var message = form.GetVisualDescendants().OfType<TextBlock>().First(t => t.Text == vm.PostalCodeError && t.IsEffectivelyVisible);
        Assert.Equal(AutomationLiveSetting.Assertive, AutomationProperties.GetLiveSetting(message));

        WriteFocusOrder("felder-stichprobe",
        [
            $"{firstPeer.GetName()} ({firstPeer.GetAutomationControlType()}), Wert \"{firstPeer.GetProvider<IValueProvider>()!.Value}\", Pflicht",
            $"{postalPeer.GetName()} ({postalPeer.GetAutomationControlType()}), Wert \"{postalPeer.GetProvider<IValueProvider>()!.Value}\", Hilfetext \"{AutomationProperties.GetHelpText(postal)}\", Meldung {AutomationProperties.GetLiveSetting(message)}",
        ]);
        return calendar;
    }, WithPlainCustomer());

    /// <summary>Ein bedienbares Element ohne lesbaren Namen: leer oder der Typname eines Steuerelements bzw. Modells („Avalonia.Controls.StackPanel“), den ein Screenreader vorlesen würde.</summary>
    private static bool HasUnreadableName(Control control, out string name)
    {
        name = ControlAutomationPeer.CreatePeerForElement(control).GetName() ?? "";
        return string.IsNullOrWhiteSpace(name) || name.StartsWith("Avalonia.", StringComparison.Ordinal) || name.StartsWith("AispaStudio.", StringComparison.Ordinal) || (name.Contains('{', StringComparison.Ordinal) && name.EndsWith('}'));
    }

    [Fact]
    public Task Jedes_bedienbare_Element_jeder_Seite_hat_einen_lesbaren_Namen() => Session.Dispatch(async () =>
    {
        var problems = new SortedSet<string>(StringComparer.Ordinal);
        await WithApp(async (window, shell) =>
        {
            shell.Companion.IsOpen = false;
            await Pump(Settling);
            shell.Calendar.OpenAppointment(SpaSample.Selected);
            await Pump(Settling);
            foreach (var page in shell.Pages)
            {
                shell.SelectedPage = page;
                await Pump(Settling);
                foreach (var control in window.GetVisualDescendants().OfType<Control>().Where(c => c is Button or ToggleButton or CheckBox or RadioButton or TextBox or ComboBox or ListBoxItem or MenuItem or Slider))
                {
                    if (control.IsEffectivelyVisible && control.IsEffectivelyEnabled && control.Focusable && HasUnreadableName(control, out var name))
                    {
                        problems.Add($"{page.Title}: {control.GetType().Name}#{control.Name} [{string.Join(" ", control.Classes)}] \"{name}\"");
                    }
                }
            }
        });

        Assert.True(problems.Count == 0, $"{problems.Count} Elemente:{Environment.NewLine}{string.Join(Environment.NewLine, problems)}");
        return true;
    }, CancellationToken.None);
}
