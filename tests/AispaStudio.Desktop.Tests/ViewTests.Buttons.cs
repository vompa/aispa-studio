using System.Xml.Linq;
using AispaStudio.Desktop.Controls;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>Fluent 2 F13: Link, Grund am deaktivierten Knopf, Haken am gewählten Umschalt-Knopf, Mindestbreite 96 und höchstens 26 Zeichen (Design-Brief 5.1 und 5.24).</summary>
public sealed partial class ViewTests
{
    private static string RepositoryRoot()
    {
        var folder = new DirectoryInfo(AppContext.BaseDirectory);
        while (folder is not null && !File.Exists(Path.Combine(folder.FullName, "AispaStudio.slnx")))
        {
            folder = folder.Parent;
        }

        return folder?.FullName ?? throw new InvalidOperationException("AispaStudio.slnx nicht gefunden");
    }

    private static string[] DesktopViewFiles() =>
        Directory.GetFiles(Path.Combine(RepositoryRoot(), "src", "AispaStudio.Desktop", "Views"), "*.axaml");

    [Theory]
    [InlineData("hell")]
    [InlineData("dunkel")]
    public Task Link_ist_akzentfarbener_Text_und_unterstrichen_bei_Hover_und_Fokus(string theme) =>
        InWindow(theme, () => new StackPanel
        {
            Margin = new Thickness(40),
            Spacing = 12,
            Children =
            {
                new Button { Content = "Anker" },
                new Button { Classes = { "link" }, Content = new TextBlock { Text = "3 Termine umbuchen" } },
            },
        }, 360, 200, async window =>
        {
            var panel = (StackPanel)window.Content!;
            var anchor = (Button)panel.Children[0];
            var link = (Button)panel.Children[1];
            var text = (TextBlock)link.Content!;
            bool Underlined() => text.TextDecorations?.Any(d => d.Location == TextDecorationLocation.Underline) == true;

            // Ruhe: Text accent, keine Fläche, kein Rahmen, nicht unterstrichen.
            Assert.Equal(ColorOf(window, "AccentBrush"), ColorOf(link.Foreground));
            Assert.Equal(Colors.Transparent, ColorOf(link.Background));
            Assert.Equal(new Thickness(0), link.BorderThickness);
            Assert.False(Underlined());
            Assert.True(Contrast(ColorOf(window, "AccentBrush"), ColorOf(window, "BgBrush")) >= 4.5);

            // Hover: unterstrichen und accent-hover.
            var center = link.TranslatePoint(new Point(link.Bounds.Width / 2, link.Bounds.Height / 2), window)!.Value;
            window.MouseMove(center);
            await Pump();
            Assert.True(Underlined());
            window.MouseMove(new Point(2, 2));
            await Pump();
            Assert.False(Underlined());

            // Tastaturfokus: unterstrichen.
            anchor.Focus(NavigationMethod.Pointer);
            link.Focus(NavigationMethod.Tab);
            await Pump();
            Assert.True(Underlined());
        }, "knopf-link");

    [Fact]
    public Task Knopf_mit_Text_ist_mindestens_96_breit_Symbol_Knopf_Chip_und_kleiner_Knopf_nicht() => InWindow("hell", () => new StackPanel
    {
        Margin = new Thickness(20),
        Spacing = 8,
        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
        Children =
        {
            new Button { Content = "OK" },
            new Button { Classes = { "primary" }, Content = "Ja" },
            new Button { Classes = { "small" }, Content = "Ja" },
            new Button { Classes = { "icon" }, Content = new Icon { Data = Icons.Close } },
            new Button { Classes = { "headchip" }, Content = new Icon { Data = Icons.Bell } },
            new Button { Classes = { "link" }, Content = "Link" },
        },
    }, 400, 400, window =>
    {
        var buttons = ((StackPanel)window.Content!).Children.OfType<Button>().ToList();
        Assert.True(buttons[0].Bounds.Width >= 96, $"Standard {buttons[0].Bounds.Width}");
        Assert.True(buttons[1].Bounds.Width >= 96, $"Primär {buttons[1].Bounds.Width}");
        Assert.True(buttons[2].Bounds.Width >= 64 && buttons[2].Bounds.Width < 96, $"Klein {buttons[2].Bounds.Width}");
        Assert.Equal(34, buttons[3].Bounds.Width, 0.5);
        Assert.True(buttons[4].Bounds.Width < 96, $"Chip {buttons[4].Bounds.Width}");
        Assert.True(buttons[5].Bounds.Width < 96, $"Link {buttons[5].Bounds.Width}");
        return Task.CompletedTask;
    });

    [Fact]
    public Task Mindestbreite_trifft_weder_den_Knopf_KI_und_Hilfe_noch_die_Knoepfe_des_Datumswaehlers() => Render("hell", "knopf-mindestbreite", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        await Pump();
        // Der schwebende Knopf unten rechts bleibt ein Kreis von 48 und deckt die Reiterschiene nicht zu.
        Assert.Equal(48, Named<Button>(window, "CompanionFab").Bounds.Width, 0.5);

        // Die Pfeile im Kalender des Datumswählers sind Knöpfe in der Vorlage und bleiben schmal.
        var date = Named<Button>(window, "DateButton");
        date.Flyout!.ShowAt(date);
        await Pump();
        var arrows = window.GetVisualDescendants().OfType<Avalonia.Controls.Primitives.CalendarItem>().SelectMany(i => i.GetVisualDescendants().OfType<Button>().Where(b => b.Name is "PART_PreviousButton" or "PART_NextButton")).ToList();
        Assert.Equal(2, arrows.Count);
        Assert.All(arrows, b => Assert.True(b.Bounds.Width < 96, $"{b.Name}: {b.Bounds.Width}"));
        return calendar;
    });

    [Fact]
    public Task Datumswaehler_schliesst_beim_Waehlen_eines_Tages_und_springt_mit_Heute_zurueck() => Render("hell", "datumswaehler", async (window, main) =>
    {
        // Auftraggeber, 23.10.2026: vorher blieb das Flyout nach dem Klick auf den Tag offen („dauert lange bis zum Tag“), und „Heute“ fehlte darin.
        var calendar = await SignedInCalendar(main);
        await Pump();
        var date = Named<Button>(window, "DateButton");
        date.Flyout!.ShowAt(date);
        await Pump();
        var picker = window.GetVisualDescendants().OfType<Calendar>().Single();
        var start = calendar.Date;

        picker.SelectedDate = start.AddDays(3).ToDateTime(TimeOnly.MinValue);
        await Pump();
        Assert.Equal(start.AddDays(3), calendar.Date);
        Assert.False(date.Flyout.IsOpen);

        date.Flyout.ShowAt(date);
        await Pump();
        var today = window.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "PickToday");
        Assert.True(today.IsEnabled);
        today.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await Pump();
        Assert.True(calendar.IsToday);
        Assert.False(date.Flyout.IsOpen);

        // Für das Bild bleibt das Blatt offen: ruhige runde Tage ohne Raster, heute als Akzentfläche.
        date.Flyout.ShowAt(date);
        await Pump();
        return calendar;
    });

    [Fact]
    public Task Deaktivierter_Knopf_nennt_im_Tooltip_den_Grund_und_danach_wieder_seinen_eigenen_Text() => InWindow("hell", () => new Button { Content = "Speichern", IsEnabled = false }, 300, 200, async window =>
    {
        var button = (Button)window.Content!;
        ToolTip.SetTip(button, "Speichert die Änderungen");
        DisabledHint.SetText(button, "Verfügbar, sobald ein Name eingetragen ist.");
        Assert.Equal("Verfügbar, sobald ein Name eingetragen ist.", ToolTip.GetTip(button));
        Assert.True(ToolTip.GetShowOnDisabled(button));

        button.IsEnabled = true;
        await Pump();
        Assert.Equal("Speichert die Änderungen", ToolTip.GetTip(button));
        Assert.False(ToolTip.GetShowOnDisabled(button));

        button.IsEnabled = false;
        await Pump();
        Assert.Equal("Verfügbar, sobald ein Name eingetragen ist.", ToolTip.GetTip(button));

        // Der Tooltip erscheint auch am deaktivierten Knopf.
        ToolTip.SetIsOpen(button, true);
        await Pump();
        Assert.Contains(window.GetVisualDescendants().OfType<ToolTip>(), t => t.Content as string == "Verfügbar, sobald ein Name eingetragen ist.");
    });

    /// <summary>
    /// Given ein gesperrter Knopf mit DisabledHint When der Screenreader ihn liest Then ist der Grund ohne Maus lesbar: als Hilfetext des Knopfes (W-Muster 12).
    /// Ein gesperrter Knopf nimmt keinen Fokus; der Screenreader erreicht ihn über die Elementliste und liest dort Name und Hilfetext.
    /// </summary>
    [Fact]
    public Task Gesperrter_Knopf_nennt_den_Grund_auch_als_Hilfetext_und_danach_wieder_seinen_eigenen() => InWindow("hell",
        () => new StackPanel { Children = { new Button { Content = "Speichern", IsEnabled = false }, new Button { Content = "Anlegen", IsEnabled = false } } }, 300, 200, async window =>
    {
        var panel = (StackPanel)window.Content!;
        var withHelp = (Button)panel.Children[0];
        var withoutHelp = (Button)panel.Children[1];
        AutomationProperties.SetHelpText(withHelp, "Speichert die Änderungen.");
        DisabledHint.SetText(withHelp, "Verfügbar, sobald ein Name eingetragen ist.");
        DisabledHint.SetText(withoutHelp, "Verfügbar, sobald ein Vor- oder Nachname eingetragen ist.");
        await Pump();
        Assert.Equal("Verfügbar, sobald ein Name eingetragen ist.", ControlAutomationPeer.CreatePeerForElement(withHelp).GetHelpText());
        Assert.Equal("Verfügbar, sobald ein Vor- oder Nachname eingetragen ist.", ControlAutomationPeer.CreatePeerForElement(withoutHelp).GetHelpText());

        // Frei: wieder der eigene Hilfetext bzw. keiner.
        withHelp.IsEnabled = true;
        withoutHelp.IsEnabled = true;
        await Pump();
        Assert.Equal("Speichert die Änderungen.", AutomationProperties.GetHelpText(withHelp));
        Assert.Null(AutomationProperties.GetHelpText(withoutHelp));

        withHelp.IsEnabled = false;
        await Pump();
        Assert.Equal("Verfügbar, sobald ein Name eingetragen ist.", AutomationProperties.GetHelpText(withHelp));
    });

    [Fact]
    public Task Deaktivierter_Knopf_im_Programm_traegt_den_Grund_die_Raumpflege_und_der_Buchen_Knopf() => Render("hell", "knopf-grund", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        calendar.StartBookingAt(10, calendar.Hours.Single(h => h.Label == "12:00").Top + 1);
        await Pump();

        // „Termin buchen“ ist ohne Gast gesperrt und sagt, was fehlt.
        var book = window.GetVisualDescendants().OfType<Button>().First(b => b.IsEffectivelyVisible && b.Content as string == "Termin buchen");
        Assert.False(book.IsEffectivelyEnabled);
        Assert.StartsWith("Verfügbar, sobald", ToolTip.GetTip(book) as string, StringComparison.Ordinal);
        Assert.True(ToolTip.GetShowOnDisabled(book));
        // Derselbe Grund steht im Hilfetext, den der Screenreader am gesperrten Knopf liest (W-Muster 12).
        Assert.Equal(ToolTip.GetTip(book), ControlAutomationPeer.CreatePeerForElement(book).GetHelpText());
        return calendar;
    });

    [Fact]
    public void Alle_Gruende_beginnen_mit_Verfuegbar_und_sind_kurz()
    {
        var reasons = new List<string>();
        foreach (var file in DesktopViewFiles())
        {
            var document = XDocument.Load(file);
            reasons.AddRange(document.Descendants().SelectMany(e => e.Attributes()).Where(a => a.Name.LocalName == "DisabledHint.Text" && !a.Value.StartsWith('{')).Select(a => a.Value));
        }

        Assert.True(reasons.Count >= 30, $"Es gibt nur {reasons.Count} Gründe");
        Assert.All(reasons, r =>
        {
            Assert.StartsWith("Verfügbar", r, StringComparison.Ordinal);
            Assert.EndsWith(".", r, StringComparison.Ordinal);
            Assert.True(r.Length <= 100, r);
        });
    }

    /// <summary>
    /// Beschriftungen über 26 Zeichen (Brief 5.1: sonst umbrechen oder die Leiste stapeln). Diese Knöpfe stehen allein in einer Zeile oder in voller Breite und werden
    /// im Handbuch wörtlich zitiert; ein neuer Knopf darüber gehört nicht hierher, sondern gekürzt.
    /// </summary>
    private static readonly string[] LongButtonTextsAlone =
    [
        "Neuen Wiederherstellungsschlüssel erzeugen", "Zeiten dieses Termins ändern …", "In der Verwaltung bearbeiten", "Vorschläge als Antwortentwurf übernehmen",
        "Gewähltes Terminal entfernen", "Diesen PC als Empfangsgerät anmelden", "Erledigt, ohne Termin und Antwort", "Mit einem bestehenden Spa verbinden …",
        "Passwort setzen und anmelden", "Passwort ändern und anmelden", "Bezahlen und Beleg erstellen", "Aus einer Sicherung wiederherstellen …",
    ];

    /// <summary>
    /// KI-Knöpfe der Absage-Rückfrage (W-Muster 13: Verb und Kennzeichen „(KI)“, Wortlaut der Befundliste, Auftraggeber 25.10.2026): sie stehen in einem <c>WrapPanel</c>, das bei Platzmangel umbricht.
    /// </summary>
    private static readonly string[] LongButtonTextsWrapping = ["Freundlicher formulieren (KI)", "Förmlicher formulieren (KI)"];

    [Fact]
    public void Knopfbeschriftungen_haben_hoechstens_26_Zeichen()
    {
        var tooLong = new List<string>();
        foreach (var file in DesktopViewFiles())
        {
            foreach (var button in XDocument.Load(file).Descendants().Where(e => e.Name.LocalName is "Button" or "ToggleButton" or "SplitButton"))
            {
                // Inhalte eines Flyouts oder Tooltips (Eigenschaftselemente wie Button.Flyout) gehören nicht zur Beschriftung des Knopfes.
                var own = button.DescendantsAndSelf().Where(e => e == button || !e.Ancestors().TakeWhile(a => a != button).Any(a => a.Name.LocalName.Contains('.', StringComparison.Ordinal)));
                foreach (var element in own)
                {
                    var text = element == button ? element.Attribute("Content")?.Value
                        : element.Name.LocalName == "TextBlock" ? element.Attribute("Text")?.Value : null;
                    if (text is { Length: > 26 } && !text.StartsWith('{') && !LongButtonTextsAlone.Contains(text) && !LongButtonTextsWrapping.Contains(text))
                    {
                        tooLong.Add($"{Path.GetFileName(file)}: {text}");
                    }
                }
            }
        }

        Assert.Empty(tooLong);
    }

    [Fact]
    public Task Umschalt_Chip_zeigt_gewaehlt_zusaetzlich_den_Haken() => Render("hell", "knopf-umschalter", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        await Pump();
        var chip = Named<Button>(window, "HeadStaffFilter");
        var check = chip.GetVisualDescendants().OfType<Icon>().Single(i => i.Data == Icons.Check);
        Assert.True(calendar.CanToggleAllStaff);
        Assert.False(calendar.ShowAllStaff);
        Assert.False(check.IsEffectivelyVisible);
        Assert.DoesNotContain("on", chip.Classes);

        calendar.ToggleAllStaffCommand.Execute(null);
        await Pump();
        Assert.True(calendar.ShowAllStaff);
        Assert.Contains("on", chip.Classes);
        Assert.True(check.IsEffectivelyVisible);
        Assert.Equal((16, 16), (check.Bounds.Width, check.Bounds.Height));
        // Der Haken steht links vor dem Symbol und dem Text.
        var icons = chip.GetVisualDescendants().OfType<Icon>().Where(i => i.IsEffectivelyVisible).OrderBy(i => LeftOf(window, i)).ToList();
        Assert.Same(check, icons[0]);

        // Das Chip-Paar Team/Räume wechselt eine Ansicht und trägt keinen Haken.
        Assert.DoesNotContain(Named<Button>(window, "HeadTeam").GetVisualDescendants().OfType<Icon>(), i => i.Data == Icons.Check);
        Assert.DoesNotContain(Named<Button>(window, "HeadRooms").GetVisualDescendants().OfType<Icon>(), i => i.Data == Icons.Check);
        return calendar;
    }, HiddenStaffApi());
}
