using System.Text.Json;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Controls;
using AispaStudio.Desktop.Services;
using AispaStudio.Desktop.ViewModels;
using AispaStudio.Desktop.Views;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
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

            var history = Named<Control>(window, "HistoryHeader");
            history.BringIntoView();
            await Pump(Settling);
            ClickCenter(window, history);
            await Pump(Settling);
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
            Assert.True(Named<Control>(window, "ProcessSection").IsEffectivelyVisible);
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
            await ClickSection(window, "Qualifikationen & Räume");
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
            await ClickSection(window, "Ablauf");
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
