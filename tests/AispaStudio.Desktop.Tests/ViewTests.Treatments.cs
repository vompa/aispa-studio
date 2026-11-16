using System.Net;
using System.Text.Json;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Controls;
using AispaStudio.Desktop.Services;
using AispaStudio.Desktop.ViewModels;
using AispaStudio.Desktop.Views;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Logging;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>R05 Slice 94 (Ä191, ADR 0084): Behandlungen als native Maske der Verwaltung, mit echter Eingabe (Klick, Tippen, Strg+S).</summary>
public sealed partial class ViewTests
{
    private static readonly QualificationDto HotStoneQualification = new(Guid.Parse("0190f5a3-0000-7000-8000-000000000941"), "Hot Stone");

    private static readonly TreatmentDto HotStone60 = new(Guid.Parse("0190f5a3-0000-7000-8000-000000000942"), "Hot Stone 60", 60, 10, 10, 89m,
        [HotStoneQualification.Id], [SpaSample.Lotus.Id], [], "Massagen", GuestHint: "Bitte 10 Minuten früher kommen.");

    /// <summary>
    /// Die Verwaltung mit Hot Stone 60 (89 €) an erster Stelle; Lena hat die Qualifikation. Speichern gibt zurück, was ankam, und der Verlauf zeigt danach die
    /// Preisänderung, wie die API.
    /// </summary>
    private static StubApi TreatmentApi()
    {
        var saved = false;
        List<TreatmentDto> treatments = [HotStone60, .. SpaSample.Treatments];
        var api = Api()
            .On("GET /api/treatments", treatments)
            .On("GET /api/qualifications", new List<QualificationDto> { HotStoneQualification })
            .On("GET /api/employees", SpaSample.Resources.Employees.Select(e => e == SpaSample.Lena ? e with { QualificationIds = [HotStoneQualification.Id] } : e).ToList())
            .On("GET /api/stock/items", new List<StockItemDto> { new(Guid.NewGuid(), "Massageöl", "ml", 0.04m, 500, null, true) })
            .On($"GET /api/locations/{SpaSample.Salzburg.Id}/resources", new LocationResources(SpaSample.Salzburg, [], [], [], [], []))
            .OnJson($"GET /api/treatments/{HotStone60.Id}/history", _ => saved
                ? new List<HistoryEntry> { new(SpaSample.At(11, 39), "Geändert von Katrin Aigner: Preis 89,00 € → 95,00 €") }
                : new List<HistoryEntry>())
            .OnJson($"PUT /api/treatments/{HotStone60.Id}", body =>
            {
                saved = true;
                var input = JsonSerializer.Deserialize<TreatmentInput>(body!, ApiClient.Json)!;
                return HotStone60 with { Name = input.Name, Price = input.Price, BasePrice = input.Price, RequiredQualificationIds = input.RequiredQualificationIds };
            });
        foreach (var treatment in treatments)
        {
            api.On($"GET /api/treatments/{treatment.Id}/price-rules", new List<TreatmentPriceRuleDto>())
                .On($"GET /api/reports/treatments/{treatment.Id}/preparation",
                    new List<PreparationStepDto> { new(Guid.NewGuid(), "Steine auf 55 °C aufheizen", 30) })
                .On($"GET /api/costs/treatments/{treatment.Id}/materials", new List<MaterialLine> { new("Massageöl", 20, "ml", 0.04m) })
                .On($"GET /api/treatments/{treatment.Id}/extras", new List<TreatmentExtraDto> { new(Guid.NewGuid(), "Früchteteller", ExtraTiming.AfterTreatment, null, null, null, null, 0) });
        }

        return api;
    }

    private static ListBoxItem RowIn(Window window, string list, string title) =>
        Named<ListBox>(window, list).GetVisualDescendants().OfType<ListBoxItem>().First(i => i.DataContext?.ToString() == title);

    /// <summary>Verwaltung öffnen und „Behandlungen“ anklicken, bis Hot Stone 60 mit allen Teil-Listen geladen ist.</summary>
    private static async Task<TreatmentsViewModel> OpenTreatments(Window window, MainViewModel main)
    {
        var shell = await OpenAdminPage(window, main);
        await OpenMask(window, shell, "Behandlungen");
        var treatments = shell.AdminPage.Treatments!;
        Assert.Same(treatments, shell.AdminPage.Content);
        Assert.True(treatments.Editor?.IsLoaded);
        return treatments;
    }

    private static async Task ClickSection(Window window, string label)
    {
        var tab = window.GetVisualDescendants().OfType<TabButton>().First(t => t.Content as string == label);
        ClickCenter(window, tab);
        await Pump(Settling);
    }

    /// <summary>Die Seitenleiste „Verlauf“ der Behandlung (ADR 0098); es gibt genau eine in der Maske.</summary>
    private static HistoryPanel TreatmentHistory(Window window) =>
        window.GetVisualDescendants().OfType<TreatmentsView>().Single().GetVisualDescendants().OfType<HistoryPanel>().Single();

    /// <summary>Ob der Anfang einer Gruppe im sichtbaren Teil der Maske liegt (nach einem Sprung gerollt, nicht nur vorhanden).</summary>
    private static bool InFieldsViewport(Window window, Control group)
    {
        var scroll = Named<ScrollViewer>(window, "FieldsScroll");
        var top = group.TranslatePoint(new Point(0, 0), scroll)!.Value.Y;
        return group.IsEffectivelyVisible && top >= -1 && top < scroll.Viewport.Height;
    }

    private static bool HasFocusInside(Window window, Control scope) => Focused(window) is Visual focused && scope.IsVisualAncestorOf(focused);

    [Fact]
    public Task Behandlung_hat_vier_Bereiche_auf_einer_Zeile_auch_im_kleinsten_Fenster() => Render("hell", "probe-behandlungen-1100", async (window, main) =>
    {
        // Given Hot Stone 60 ist offen; When das Fenster seine Mindestgröße 1100 × 680 hat
        var treatments = await OpenTreatments(window, main);
        window.MinWidth = 0;
        window.MinHeight = 0;
        window.Width = 1100;
        window.Height = 680;
        await Pump(Settling);
        await Pump(Settling);
        Assert.Equal(1100, window.Bounds.Width, 0.5);
        Assert.Equal(680, window.Bounds.Height, 0.5);

        // Then hat die Schiene vier Bereiche auf einer Zeile innerhalb der Karte (masken.md R15)
        var strip = Named<Border>(window, "SectionTabs");
        var tabs = strip.GetVisualDescendants().OfType<TabButton>().ToList();
        Assert.Equal(["Grunddaten", "Durchführung", "Preise und Verkauf", "Qualifikationen und Räume"], tabs.Select(t => t.Content as string));
        Assert.Single(tabs.Select(t => Math.Round(InWindow(window, t).Y)).Distinct());
        var editor = window.GetVisualDescendants().OfType<TreatmentEditorView>().Single();
        Assert.True(InWindow(window, strip).Right <= InWindow(window, editor).Right + 0.5,
            $"Schiene endet bei {InWindow(window, strip).Right}, die Karte bei {InWindow(window, editor).Right}");
        Assert.DoesNotContain(window.GetVisualDescendants().OfType<TabButton>(), t => t.Content as string is "Ablauf" or "Material" or "Bereitstellen" or "Verkauf" or "Preise");

        // And ein Sprung zur letzten Gruppe rollt sie in den sichtbaren Teil
        ((ShellViewModel)main.Content!).AdminPage.Show("masterData", "treatments", HotStone60.Id, TreatmentEditorViewModel.Provide);
        await Pump(Settling);
        Assert.True(InFieldsViewport(window, Named<Control>(window, "ProvideGroup")));
        Assert.True(Named<ScrollViewer>(window, "FieldsScroll").Offset.Y > 0);

        // And Kopf und Schiene rollen nicht mit: die Bereiche und „Verlauf“ bleiben an ihrem Platz
        var stripAfter = InWindow(window, strip);
        Assert.True(stripAfter.Top > InWindow(window, editor).Top && stripAfter.Bottom < InWindow(window, Named<ScrollViewer>(window, "FieldsScroll")).Top + 0.5);

        // And die Seitenleiste „Verlauf“ öffnet und liegt ganz im Fenster
        ClickCenter(window, Named<Button>(window, "HistoryButton"));
        await Pump(Settling);
        Assert.True(TreatmentHistory(window).IsOpen);
        var panel = InWindow(window, TreatmentHistory(window));
        Assert.True(panel.Left >= 0 && panel.Right <= 1100.5, $"Verlauf von {panel.Left} bis {panel.Right}");

        // And weil daneben keine Formularbreite bliebe, liegt sie über dem rechten Teil der Karte (ADR 0098)
        Assert.True(InWindow(window, Named<ScrollViewer>(window, "FieldsScroll")).Right > panel.Left);
        return treatments;
    }, TreatmentApi());

    [Theory]
    [InlineData("ablauf", "ExecutionSection", "ProcessGroup")]
    [InlineData("material", "ExecutionSection", "MaterialGroup")]
    [InlineData("bereitstellen", "ExecutionSection", "ProvideGroup")]
    [InlineData("preise", "OfferSection", "PricesGroup")]
    [InlineData("verkauf", "OfferSection", "SalesGroup")]
    public Task Given_eine_alte_Abschnittsadresse_When_die_Verwaltung_dorthin_springt_Then_ist_der_Bereich_offen_und_der_Fokus_in_der_Gruppe(
        string address, string area, string group) => Render("hell", $"probe-behandlungen-sprung-{address}", async (window, main) =>
        {
            var treatments = await OpenTreatments(window, main);

            ((ShellViewModel)main.Content!).AdminPage.Show("masterData", "treatments", HotStone60.Id, address);
            await Pump(Settling);

            Assert.True(Named<Control>(window, area).IsEffectivelyVisible);
            Assert.True(InFieldsViewport(window, Named<Control>(window, group)));
            Assert.True(HasFocusInside(window, Named<Control>(window, group)), $"Fokus liegt auf {Focused(window)}");
            Assert.Null(treatments.Editor!.GroupTarget);
            return treatments;
        }, TreatmentApi());

    [Fact]
    public Task Given_das_Material_wird_abgelehnt_When_die_Inhaberin_speichert_Then_oeffnet_Durchfuehrung_und_der_Fokus_steht_in_der_Gruppe_Material()
    {
        var api = TreatmentApi().On($"PUT /api/costs/treatments/{HotStone60.Id}/materials", new { detail = "Die Menge fehlt." }, HttpStatusCode.BadRequest);
        return Render("hell", "probe-behandlungen-materialfehler", async (window, main) =>
        {
            var treatments = await OpenTreatments(window, main);
            treatments.Editor!.AddMaterialCommand.Execute(null);
            await ClickSection(window, "Qualifikationen und Räume");

            await ClickIntoView(window, ButtonWithText(window, "Speichern"));
            await Pump(Settling);

            Assert.True(treatments.HasError);
            Assert.True(Named<Control>(window, "ExecutionSection").IsEffectivelyVisible);
            Assert.True(InFieldsViewport(window, Named<Control>(window, "MaterialGroup")));
            Assert.True(HasFocusInside(window, Named<Control>(window, "MaterialGroup")), $"Fokus liegt auf {Focused(window)}");
            return treatments;
        }, api);
    }

    [Theory]
    [InlineData("hell")]
    [InlineData("dunkel")]
    public Task Verwaltung_Behandlungen_Durchfuehrung(string theme) => Render(theme, "verwaltung-behandlungen-durchfuehrung", async (window, main) =>
    {
        var treatments = await OpenTreatments(window, main);

        await ClickSection(window, "Durchführung");

        // Ablauf, Material und Bereitstellen stehen als Gruppen untereinander (masken.md R1).
        Assert.True(Shows(window, "Vor- und Nachbereitung"));
        Assert.True(Shows(window, "Vorbereitungsschritte"));
        Assert.True(Shows(window, "Material je Termin"));
        Assert.True(Shows(window, "Zum Termin bereitstellen"));
        return treatments;
    }, TreatmentApi());

    [Theory]
    [InlineData("hell")]
    [InlineData("dunkel")]
    public Task Verwaltung_Behandlungen_Verlauf(string theme) => Render(theme, "verwaltung-behandlungen-verlauf", async (window, main) =>
    {
        // Given Hot Stone 60 mit einer Preisänderung im Verlauf
        var treatments = await OpenTreatments(window, main);
        var panel = TreatmentHistory(window);
        Assert.False(panel.IsEffectivelyVisible);
        Assert.Empty(Named<ScrollViewer>(window, "FieldsScroll").GetVisualDescendants().OfType<HistorySectionView>());

        // When die Inhaberin im Kopf der Karte „Verlauf“ wählt
        ClickCenter(window, Named<Button>(window, "HistoryButton"));
        await Pump(Settling);

        // Then steht der Verlauf als Seitenleiste rechts, aufgeklappt und mit dem Fokus darin
        Assert.True(panel.IsOpen);
        Assert.True(panel.IsEffectivelyVisible);
        Assert.Equal(360, panel.Bounds.Width, 0.5);
        Assert.True(HasFocusInside(window, panel));
        Assert.True(Shows(window, "Geändert von Katrin Aigner: Preis 89,00 € → 95,00 €"));
        Assert.True(Shows(window, "Änderungen"));

        // And die Leiste deckt (Fläche der Seitenleisten), und im breiten Fenster steht die Karte daneben statt darunter
        Assert.NotNull(panel.Background);
        Assert.True(InWindow(window, Named<ScrollViewer>(window, "FieldsScroll")).Right <= InWindow(window, panel).Left + 0.5);

        // Der Zeiger steht fürs Bild abseits, damit kein Tooltip der Schaltfläche über der Schiene liegt.
        window.MouseMove(new Point(700, 700));
        return treatments;
    }, TreatmentApi().On($"GET /api/treatments/{HotStone60.Id}/history",
        new List<HistoryEntry> { new(SpaSample.At(11, 39), "Geändert von Katrin Aigner: Preis 89,00 € → 95,00 €") }));

    [Fact]
    public Task Given_der_Verlauf_ist_offen_When_Esc_Then_schliesst_die_Seitenleiste_und_der_Fokus_steht_wieder_auf_Verlauf() =>
        Render("hell", "probe-behandlungen-verlauf-esc", async (window, main) =>
        {
            var treatments = await OpenTreatments(window, main);
            var button = Named<Button>(window, "HistoryButton");
            ClickCenter(window, button);
            await Pump(Settling);
            Assert.True(TreatmentHistory(window).IsOpen);

            Press(window, PhysicalKey.Escape, RawInputModifiers.None);
            await Pump(Settling);

            Assert.False(treatments.Editor!.IsHistoryOpen);
            Assert.False(TreatmentHistory(window).IsEffectivelyVisible);
            Assert.Same(button, Focused(window));
            // Esc gilt nur der Leiste: die Verwaltung bleibt offen.
            var shell = (ShellViewModel)main.Content!;
            Assert.Same(shell.AdminPage, shell.SelectedPage?.Page);
            return treatments;
        }, TreatmentApi());

    [Fact]
    public Task Given_eine_neue_Behandlung_When_die_Maske_offen_ist_Then_fehlt_die_Schaltflaeche_Verlauf() =>
        Render("hell", "probe-behandlungen-neu", async (window, main) =>
        {
            var treatments = await OpenTreatments(window, main);
            var create = RowIn(window, "TreatmentList", "Behandlung anlegen");
            create.BringIntoView();
            await Pump(Settling);
            ClickCenter(window, create);
            await Pump(Settling);

            Assert.True(treatments.Editor?.IsNew);
            Assert.False(Named<Button>(window, "HistoryButton").IsEffectivelyVisible);

            // Zwei Standorte: von „Preise und Verkauf“ gibt es vor dem Anlegen nur den Preis je Standort.
            await ClickSection(window, "Preise und Verkauf");
            Assert.True(Shows(window, "Preis je Standort"));
            Assert.False(Named<Control>(window, "SalesGroup").IsEffectivelyVisible);
            Assert.False(Shows(window, "Preise nach Wochentag und Uhrzeit"));
            await ClickSection(window, "Durchführung");
            Assert.True(Shows(window, "Vor- und Nachbereitung"));
            Assert.False(Named<Control>(window, "MaterialGroup").IsEffectivelyVisible);
            Assert.False(Named<Control>(window, "ProvideGroup").IsEffectivelyVisible);
            return treatments;
        }, TreatmentApi());

    [Theory]
    [InlineData("hell")]
    [InlineData("dunkel")]
    public Task Verwaltung_Behandlungen(string theme) => Render(theme, "verwaltung-behandlungen", async (window, main) =>
    {
        var treatments = await OpenTreatments(window, main);
        Assert.Equal("Hot Stone 60", Named<TextBox>(window, "NameField").Text);
        Assert.Equal("Hot Stone 60", treatments.SelectedEntry?.Title);
        return treatments;
    }, TreatmentApi());

    [Fact]
    public Task Given_Hot_Stone_60_zu_89_Euro_When_die_Inhaberin_95_eintippt_und_Strg_S_drueckt_Then_gespeichert_und_der_Verlauf_zeigt_89_auf_95()
    {
        var api = TreatmentApi();
        return Render("hell", "verwaltung-behandlungen-gespeichert", async (window, main) =>
        {
            var treatments = await OpenTreatments(window, main);
            Assert.Equal("89,00", Named<TextBox>(window, "PriceField").Text);

            await TypeInto(window, "PriceField", "95");
            PressSave(window);
            await Pump(Settling);

            var put = Assert.Single(api.Calls, c => c.Route == $"PUT /api/treatments/{HotStone60.Id}");
            Assert.Equal(95m, JsonSerializer.Deserialize<TreatmentInput>(put.Body!, ApiClient.Json)!.Price);
            Assert.False(treatments.HasUnsavedChanges);
            Assert.True(Shows(window, "Gespeichert. Gilt für neue Buchungen."));

            ClickCenter(window, Named<Button>(window, "HistoryButton"));
            await Pump(Settling);
            Assert.True(TreatmentHistory(window).IsEffectivelyVisible);
            Assert.True(Shows(window, "Preis 89,00 € → 95,00 €"));
            return treatments;
        }, api);
    }

    [Fact]
    public Task Given_ein_Termin_im_Kalender_When_Behandlungszeiten_bearbeiten_gewaehlt_wird_Then_oeffnet_die_native_Maske_beim_Ablauf() =>
        Render("hell", "verwaltung-behandlungen-ablauf", async (window, main) =>
        {
            var calendar = await SignedInCalendar(main);
            var shell = (ShellViewModel)main.Content!;
            var item = calendar.Columns.SelectMany(c => c.Items).First(i => i.Id == SpaSample.Selected.Id);
            var action = calendar.ActionsFor(item).Single(a => a.Header == "Behandlungszeiten bearbeiten …");

            action.Run!();
            await Pump(Settling);

            Assert.Same(shell.AdminPage, shell.SelectedPage?.Page);
            var treatments = shell.AdminPage.Treatments!;
            Assert.Equal(SpaSample.Selected.TreatmentId, treatments.Editor?.Id);
            // ADR 0098: „Ablauf“ ist die erste Gruppe im Bereich „Durchführung“.
            Assert.True(Named<Control>(window, "ExecutionSection").IsEffectivelyVisible);
            Assert.True(InFieldsViewport(window, Named<Control>(window, "ProcessGroup")));
            Assert.False(Named<Control>(window, "BasicsSection").IsEffectivelyVisible);
            Assert.True(Named<Control>(window, "PreparationField").IsEffectivelyVisible);
            return treatments;
        }, TreatmentApi());

    [Fact]
    public Task Given_eine_neue_Behandlung_ohne_Qualifikation_When_Strg_S_Then_Hinweis_am_Feld_Fokus_auf_der_Qualifikation_und_nichts_gesendet()
    {
        var api = TreatmentApi();
        return Render("hell", "verwaltung-behandlungen-qualifikation", async (window, main) =>
        {
            var treatments = await OpenTreatments(window, main);
            var create = RowIn(window, "TreatmentList", "Behandlung anlegen");
            create.BringIntoView();
            await Pump(Settling);
            ClickCenter(window, create);
            await Pump(Settling);
            Assert.True(treatments.Editor?.IsNew);

            await TypeInto(window, "NameField", "Kräuterstempel");
            PressSave(window);
            await Pump(Settling);

            Assert.DoesNotContain(api.Calls, c => c.Route == "POST /api/treatments");
            Assert.True(Shows(window, "Keine Qualifikation gewählt: Jede Person im Team kann diese Behandlung übernehmen."));
            var focused = Assert.IsType<CheckBox>(Focused(window));
            Assert.Equal("Hot Stone", focused.Content);
            Assert.True(Named<Control>(window, "ResourcesSection").IsEffectivelyVisible);
            return treatments;
        }, api);
    }

    [Fact]
    public Task Given_Hot_Stone_60_When_die_Inhaberin_den_Preis_aendert_und_Speichern_anklickt_Then_geht_der_neue_Preis_an_die_API()
    {
        var api = TreatmentApi();
        return Render("hell", "verwaltung-behandlungen-knopf", async (window, main) =>
        {
            var treatments = await OpenTreatments(window, main);
            await TypeInto(window, "PriceField", "95");
            var save = window.GetVisualDescendants().OfType<SaveBar>().Single().GetVisualDescendants().OfType<Button>().First(b => b.Content as string == "Speichern");
            ClickCenter(window, save);
            await Pump(Settling);

            var put = Assert.Single(api.Calls, c => c.Route == $"PUT /api/treatments/{HotStone60.Id}");
            Assert.Equal(95m, JsonSerializer.Deserialize<TreatmentInput>(put.Body!, ApiClient.Json)!.Price);
            Assert.False(treatments.HasUnsavedChanges);
            return treatments;
        }, api);
    }

    [Fact]
    public Task Given_Wer_kann_das_zeigt_Lena_Huber_When_die_Inhaberin_den_Namen_anklickt_Then_oeffnet_die_Verwaltung_den_Bereich_Team() =>
        Render("hell", "verwaltung-behandlungen-wer", async (window, main) =>
        {
            var treatments = await OpenTreatments(window, main);
            await ClickSection(window, "Qualifikationen und Räume");
            var chip = window.GetVisualDescendants().OfType<Button>().First(b => b.Classes.Contains("chip") && b.Content as string == "Lena Huber");
            chip.BringIntoView();
            await Pump(Settling);
            ClickCenter(window, chip);
            await Pump(Settling);

            var shell = (ShellViewModel)main.Content!;
            Assert.Equal("Team", shell.AdminPage.SelectedArea?.Title);
            return treatments;
        }, TreatmentApi());

    [Fact]
    public Task Given_der_Assistent_meldet_einen_fehlenden_Preis_When_die_Verwaltung_beim_Feld_Preis_oeffnet_Then_hat_das_Preisfeld_den_Fokus() =>
        Render("hell", "verwaltung-behandlungen-preisfeld", async (window, main) =>
        {
            var treatments = await OpenTreatments(window, main);
            await ClickSection(window, "Durchführung");
            var shell = (ShellViewModel)main.Content!;

            shell.ShowAssistantLink(new AssistantLink(AssistantLinkKind.Setting, "Jetzt ergänzen", "masterData", "treatments", TargetId: HotStone60.Id,
                Field: TreatmentEditorViewModel.PriceField));
            await Pump(Settling);

            Assert.True(Named<Control>(window, "BasicsSection").IsEffectivelyVisible);
            Assert.Same(Named<TextBox>(window, "PriceField"), Focused(window));
            return treatments;
        }, TreatmentApi());

    [Fact]
    public Task Behandlungen_jeder_Abschnitt_hat_lesbare_Namen_erreicht_jedes_Element_mit_Tab_und_meldet_keine_Bindungsfehler() =>
        Render("hell", "verwaltung-behandlungen-scan", async (window, main) =>
        {
            var previous = Logger.Sink;
            var log = new BindingLog();
            Logger.Sink = log;
            try
            {
                TreatmentsViewModel? treatments = null;
                await AssertNoBindingErrors(log, "Behandlungen öffnen", async () => treatments = await OpenTreatments(window, main));
                var problems = new List<string>();
                foreach (var section in treatments!.Editor!.Sections.Select(s => s.Label).ToList())
                {
                    await AssertNoBindingErrors(log, $"Abschnitt {section}", () => ClickSection(window, section));
                    var view = window.GetVisualDescendants().OfType<TreatmentsView>().Single();
                    foreach (var control in view.GetVisualDescendants().OfType<Control>()
                        .Where(c => c is Button or ToggleButton or CheckBox or TextBox or ComboBox or ListBoxItem))
                    {
                        if (control.IsEffectivelyVisible && control.IsEffectivelyEnabled && control.Focusable && HasUnreadableName(control, out var name))
                        {
                            problems.Add($"{section}: {control.GetType().Name}#{control.Name} \"{name}\"");
                        }
                    }

                    problems.AddRange(await TabProblems(window, $"Behandlungen, {section}"));
                }

                await AssertNoBindingErrors(log, "Behandlung anlegen", async () =>
                {
                    treatments.SelectedEntry = treatments.Entries.Single(e => e.IsNew);
                    await Pump(Settling);
                });
                await AssertNoBindingErrors(log, "Zum Kalender", async () =>
                {
                    var shell = (ShellViewModel)main.Content!;
                    shell.SelectedPage = shell.Pages.First(p => p.Page == shell.Calendar);
                    await Pump(Settling);
                });
                log.AssertClean();
                Assert.True(problems.Count == 0, $"{problems.Count} Abweichungen:{Environment.NewLine}{string.Join(Environment.NewLine, problems)}");
                return treatments;
            }
            finally
            {
                Logger.Sink = previous;
            }
        }, TreatmentApi());
}
