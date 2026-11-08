using AispaStudio.Contracts;
using AispaStudio.Desktop.Controls;
using AispaStudio.Desktop.Services;
using AispaStudio.Desktop.ViewModels;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>Runde 5, Ä34: der Abschnitt „Kunde“ im Termin-Panel als Akkordeon mit vier Kennzahlen im Kopf, aufgeklappt mit Notiz, Zuletzt und klickbarem Kontakt.</summary>
public sealed partial class ViewTests
{
    private sealed class RecordingActions : IContactActions
    {
        public List<Uri> Launched { get; } = [];

        public List<string> Copied { get; } = [];

        public Task LaunchAsync(Uri uri)
        {
            Launched.Add(uri);
            return Task.CompletedTask;
        }

        public Task CopyAsync(string text)
        {
            Copied.Add(text);
            return Task.CompletedTask;
        }
    }

    private static readonly string[] OpenSectionTitles = ["Kundennotiz", "Zuletzt", "Kontakt"];

    private static readonly string[] StatLabels = ["Besuche gesamt", "Letzter Besuch", "Nicht erschienen", "Nächster Termin"];

    /// <summary>Öffnet den Beispieltermin und wartet, bis der Überblick des Gastes geladen ist.</summary>
    private static async Task<Border> OpenCustomerPanel(Window window, CalendarViewModel calendar)
    {
        calendar.OpenAppointment(SpaSample.Selected);
        await Pump(Settling);
        var panel = Named<Border>(window, "SidePanel");
        for (var i = 0; i < 40 && !calendar.Selected!.CustomerSection!.ShowStats; i++)
        {
            await Pump();
        }

        await Pump();
        return panel;
    }

    /// <summary>Holt ein Element im scrollenden Inhalt der Seitenleiste ins Bild und klickt es an, wie man es mit der Maus täte.</summary>
    private static async Task ClickIntoView(Window window, Control control)
    {
        control.BringIntoView();
        await Pump();
        ClickCenter(window, control);
        await Pump();
    }

    private static ExpandCollapseState StateOf(Control header) => ((IExpandCollapseProvider)ControlAutomationPeer.CreatePeerForElement(header)).ExpandCollapseState;

    [Theory]
    [InlineData("hell")]
    [InlineData("dunkel")]
    public Task Kunde_Abschnitt_ist_zu_mit_vier_Kennzahlen_in_zwei_Spalten_und_einer_Zeile_Offen(string theme) => Render(theme, "termin-kunde-zu", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        var panel = await OpenCustomerPanel(window, calendar);
        var header = Named<AccordionHeader>(panel, "CustomerHeader");

        Assert.False(header.IsChecked);
        Assert.True(header.Bounds.Height >= 44);
        Assert.Equal("Kunde", Avalonia.Automation.AutomationProperties.GetName(header));
        Assert.Equal(ExpandCollapseState.Collapsed, StateOf(header));
        var labels = StatLabels.Select(l => TextIn(header, l)).ToList();
        Assert.Equal(["14", "21.9.", "1", "2.11."], labels.Select(l => ((StackPanel)l.Parent!).Children.OfType<TextBlock>().First().Text));
        // Zwei Spalten: Besuche links neben Letzter Besuch, darunter Nicht erschienen links neben Nächster Termin.
        Assert.True(Math.Abs(TopOf(window, labels[0]) - TopOf(window, labels[1])) < 1);
        Assert.True(Math.Abs(TopOf(window, labels[2]) - TopOf(window, labels[3])) < 1);
        Assert.True(TopOf(window, labels[2]) > TopOf(window, labels[0]));
        Assert.True(Math.Abs(LeftOf(window, labels[0]) - LeftOf(window, labels[2])) < 1);
        Assert.True(LeftOf(window, labels[1]) > LeftOf(window, labels[0]) + 40);
        Assert.Equal(22, ((StackPanel)labels[0].Parent!).Children.OfType<TextBlock>().First().FontSize);
        Assert.Equal("Offen 45,00 € · Paket Hot-Stone-Massage: 7 von 10 offen", TextIn(header, "Offen 45,00 € · Paket Hot-Stone-Massage: 7 von 10 offen").Text);
        // Zu: nichts vom Rest ist zu sehen, auch nicht die Links.
        Assert.DoesNotContain(panel.GetVisualDescendants().OfType<TextBlock>(), t => t.IsEffectivelyVisible && t.Text is "Zuletzt" or "Kontakt" or "Kundennotiz");
        Assert.False(Named<Button>(panel, "PhoneLink").IsEffectivelyVisible);
        // Der Hinweis zum letzten Besuch und zum nächsten Termin nennt das volle Datum.
        Assert.Equal("Letzter Besuch: Montag, 21. September 2026", ToolTip.GetTip((Control)labels[1].Parent!));
        Assert.Contains("Hot-Stone-Massage", ToolTip.GetTip((Control)labels[3].Parent!) as string, StringComparison.Ordinal);
        return calendar;
    });

    [Fact]
    public Task Kunde_Abschnitt_oeffnet_per_Klick_und_Leertaste_und_meldet_den_Zustand_auf_oder_zu() => Render("hell", "termin-kunde-auf", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        var panel = await OpenCustomerPanel(window, calendar);
        var header = Named<AccordionHeader>(panel, "CustomerHeader");

        ClickCenter(window, header);
        await Pump(Settling);
        Assert.True(header.IsChecked);
        Assert.True(calendar.Selected!.CustomerSection!.IsOpen);
        Assert.Equal(ExpandCollapseState.Expanded, StateOf(header));
        // Der Pfeil dreht sich beim Aufklappen um 180 Grad (Drehmatrix: M11 und M22 sind -1).
        var chevron = header.GetVisualDescendants().OfType<Icon>().Single(i => i.Classes.Contains("chevron"));
        var turned = Assert.IsAssignableFrom<ITransform>(chevron.RenderTransform).Value;
        Assert.Equal((-1, -1), (Math.Round(turned.M11), Math.Round(turned.M22)));
        foreach (var text in OpenSectionTitles)
        {
            Assert.True(TextIn(panel, text).IsEffectivelyVisible, text);
        }

        // Zuletzt: vier Zeilen, jede ein Knopf mit Datum, Behandlung und Person.
        var rows = panel.GetVisualDescendants().OfType<Button>().Where(b => b.DataContext is RecentVisitRow && b.IsEffectivelyVisible).ToList();
        Assert.Equal(4, rows.Count);
        Assert.Equal("Termin öffnen", ToolTip.GetTip(rows[0]));
        Assert.Contains(rows[0].GetVisualDescendants().OfType<TextBlock>(), t => t.Text == "Hot-Stone-Massage");

        // Die Kundennotiz ist auf zwei Zeilen gekürzt, der volle Text steht im Hinweis.
        var note = panel.GetVisualDescendants().OfType<TextBlock>().First(t => t.Text?.StartsWith("Stammkundin, mag Lavendelöl", StringComparison.Ordinal) == true);
        Assert.Equal(2, note.TextLayout.TextLines.Count);
        Assert.Equal(calendar.Selected.CustomerSection!.Note, ToolTip.GetTip(note));

        // Fokus auf dem Kopf, Leertaste klappt wieder zu.
        header.Focus(NavigationMethod.Tab);
        Press(window, PhysicalKey.Space);
        await Pump(Settling);
        Assert.False(header.IsChecked);
        Assert.Equal(ExpandCollapseState.Collapsed, StateOf(header));
        Assert.DoesNotContain(panel.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == "Zuletzt" && t.IsEffectivelyVisible);
        return calendar;
    });

    [Fact]
    public Task Kontaktzeilen_sind_Links_mit_Kopieren_Knoepfen_und_der_Toast_sagt_Kopiert() => Render("hell", "termin-kunde-kontakt", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        var actions = new RecordingActions();
        calendar.ContactActions = actions;
        var panel = await OpenCustomerPanel(window, calendar);
        ClickCenter(window, Named<AccordionHeader>(panel, "CustomerHeader"));
        await Pump(Settling);

        var phone = Named<Button>(panel, "PhoneLink");
        var whatsApp = Named<Button>(panel, "WhatsAppLink");
        var mail = Named<Button>(panel, "MailLink");
        foreach (var link in new[] { phone, whatsApp, mail, Named<Button>(panel, "OpenCardLink") })
        {
            Assert.True(link.IsEffectivelyVisible);
            Assert.Contains("link", link.Classes);
            Assert.Equal(ColorOf(window, "AccentBrush"), ColorOf(link.Foreground));
        }

        Assert.Equal("+43 664 123 45 67", phone.GetVisualDescendants().OfType<TextBlock>().Single().Text);
        Assert.Equal("b.mueller@example.at", mail.GetVisualDescendants().OfType<TextBlock>().Single().Text);
        await ClickIntoView(window, phone);
        await ClickIntoView(window, whatsApp);
        await ClickIntoView(window, mail);
        Assert.Equal(["tel:+436641234567", "https://wa.me/436641234567", "mailto:b.mueller@example.at?subject=Brigitte%20M%C3%BCller"], actions.Launched.Select(u => u.OriginalString));

        var copyPhone = Named<Button>(panel, "CopyPhoneButton");
        var copyMail = Named<Button>(panel, "CopyEmailButton");
        Assert.Equal(("Telefonnummer kopieren", "E-Mail-Adresse kopieren"), (ToolTip.GetTip(copyPhone), ToolTip.GetTip(copyMail)));
        Assert.Equal((28, 28), (copyPhone.Bounds.Width, copyPhone.Bounds.Height));
        await ClickIntoView(window, copyPhone);
        Assert.Equal("Kopiert", calendar.Toast);
        await ClickIntoView(window, copyMail);
        Assert.Equal(["+43 664 123 45 67", "b.mueller@example.at"], actions.Copied);
        return calendar;
    });

    [Fact]
    public Task Kunde_ohne_Kontakt_und_ohne_Recht_auf_Betraege_zeigt_weder_Links_noch_die_Zeile_Offen() => Render("hell", "termin-kunde-ohne-kontakt", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        var panel = await OpenCustomerPanel(window, calendar);
        ClickCenter(window, Named<AccordionHeader>(panel, "CustomerHeader"));
        await Pump(Settling);

        Assert.DoesNotContain(panel.GetVisualDescendants().OfType<TextBlock>(), t => t.IsEffectivelyVisible && t.Text?.StartsWith("Offen ", StringComparison.Ordinal) == true);
        Assert.False(Named<Button>(panel, "PhoneLink").IsEffectivelyVisible);
        Assert.False(Named<Button>(panel, "WhatsAppLink").IsEffectivelyVisible);
        Assert.False(Named<Button>(panel, "MailLink").IsEffectivelyVisible);
        Assert.Equal("Keine Telefonnummer und keine E-Mail-Adresse hinterlegt.", TextIn(panel, "Keine Telefonnummer und keine E-Mail-Adresse hinterlegt.").Text);
        Assert.Equal("Anamnese fehlt", TextIn(panel, "Anamnese fehlt").Text);
        Assert.Equal("Noch keine Behandlung.", TextIn(panel, "Noch keine Behandlung.").Text);
        return calendar;
    }, ReceptionApi().On($"GET /api/customers/{SpaSample.Selected.CustomerId}/summary", new CustomerSummary(0, null, 0, null, [], null, null, null, false, null)));

    [Fact]
    public Task Strg_Enter_im_Abschnitt_oeffnet_die_Kundenkarte_und_ein_Klick_auf_Zuletzt_springt_zum_Tag() => Render("hell", "termin-kunde-strg-enter", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        var shell = (ShellViewModel)main.Content!;
        var panel = await OpenCustomerPanel(window, calendar);
        var header = Named<AccordionHeader>(panel, "CustomerHeader");

        // Zuletzt: der Klick geht zum Tag des Besuchs (21 Tage vor dem 19.10. gab es keinen Besuch, der erste Eintrag liegt 28 Tage davor).
        ClickCenter(window, header);
        await Pump(Settling);
        var first = panel.GetVisualDescendants().OfType<Button>().First(b => b.DataContext is RecentVisitRow && b.IsEffectivelyVisible);
        // Seit der Aktionsblock über dem Abschnitt steht (05.11.2026), liegt die Zeile unter dem sichtbaren Bereich des Panels.
        first.BringIntoView();
        await Pump(Settling);
        ClickCenter(window, first);
        await Pump(Settling);
        Assert.Equal(new DateOnly(2026, 9, 21), calendar.Date);

        calendar.OpenAppointment(SpaSample.Selected);
        await Pump(Settling);
        panel = Named<Border>(window, "SidePanel");
        header = Named<AccordionHeader>(panel, "CustomerHeader");
        ClickCenter(window, header);
        await Pump(Settling);
        header.Focus(NavigationMethod.Tab);
        Press(window, PhysicalKey.Enter, RawInputModifiers.Control);
        await Pump(Settling);
        Assert.Same(shell.Customers, shell.SelectedPage?.Page);
        return calendar;
    });
}
