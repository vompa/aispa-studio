using System.Text.Json;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;
using AispaStudio.Desktop.ViewModels;
using AispaStudio.Desktop.Views;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>
/// R05 Slice 115 (Ä251): „Passt dazu“ mit echter Eingabe im Fenster. Kassa und Termin-Panel zeigen die Vorschläge der API, ein Klick legt den Artikel auf die
/// Rechnung; die Behandlungsmaske speichert die Zuordnung im Reiter „Verkauf“. Die Regeln ohne Klick stehen in <c>PosRecommendationTests</c>,
/// <c>AppointmentRecommendationTests</c> und <c>TreatmentsTests</c>.
/// </summary>
public sealed partial class ViewTests
{
    private static readonly ArticleDto RecommendedOil = SpaSample.Articles[0];

    private static readonly ArticleDto RecommendedCream = SpaSample.Articles[3];

    private static RecommendedArticleDto Recommendation(ArticleDto article, decimal shelf) => new(article.Id, article.Name, article.Price, shelf, "Stück");

    private static Button ButtonWithName(Window window, string name) =>
        window.GetVisualDescendants().OfType<Button>().First(b => b.IsEffectivelyVisible && AutomationProperties.GetName(b) == name);

    [Fact]
    public Task Given_die_API_schlaegt_das_Oel_vor_When_der_Termin_auf_die_Rechnung_kommt_Then_steht_Passt_dazu_unter_der_Rechnung_und_der_Klick_auf_Dazugeben_schickt_eine_Artikelzeile()
    {
        var stub = new StubApi()
            .On("GET /api/locations", new List<LocationDto> { SpaSample.Vienna })
            .On("GET /api/pos/registers", new List<CashRegisterDto> { SpaSample.Register })
            .On("GET /api/pos/articles", SpaSample.Articles)
            .On($"GET /api/pos/locations/{SpaSample.LocationId}/day-list?date=2026-10-19", SpaSample.DayList())
            .On($"GET /api/pos/registers/{SpaSample.Register.Id}/bills", new List<BillDto>())
            .On("POST /api/pos/bills", SpaSample.Bill(withArticle: false))
            .On($"POST /api/pos/bills/{SpaSample.BillId}/lines", SpaSample.Bill())
            .On($"GET /api/pos/bills/{SpaSample.BillId}/recommendations", new List<RecommendedArticleDto> { Recommendation(RecommendedCream, 3) });
        var pos = new PosViewModel(stub.Client(), new PromotionTime());
        return InWindow("hell", () => new PosView { DataContext = pos }, 1280, 760, async window =>
        {
            await pos.InitializeAsync(SpaSample.LocationId);
            await pos.AddAppointmentAsync(SpaSample.Paying);
            await PumpUntil(() => Shows(window, "Passt dazu: Gesichtscreme Hydro 50 ml"));

            Assert.True(Shows(window, "39,00 € · 3 Stück am Regal"));
            var add = ButtonWithName(window, "Gesichtscreme Hydro 50 ml dazugeben");
            Assert.Equal("Dazugeben", add.Content);
            Assert.NotNull(ToolTip.GetTip(add));
            Assert.Contains(add.GetVisualAncestors().OfType<Border>(), b => b.Classes.Contains("hint"));

            ClickCenter(window, add);
            await Pump(Settling);

            var line = JsonSerializer.Deserialize<BillLineInput>(Assert.Single(stub.Calls, c => c.Route == $"POST /api/pos/bills/{SpaSample.BillId}/lines").Body!, ApiClient.Json)!;
            Assert.Equal((BillLineKind.Article, (Guid?)RecommendedCream.Id, 1), (line.Kind, line.ArticleId, line.Quantity));
        }, "kassa-passt-dazu");
    }

    /// <summary>Volle App mit dem Vorschlag „Massageöl“ zu einem Termin; Kassa-Aufrufe gibt das Muster aus <see cref="Api"/> zurück.</summary>
    private static StubApi RecommendationApi(AppointmentDto appointment) => Api()
        .On($"GET /api/appointments/{appointment.Id}/recommendations", new List<RecommendedArticleDto> { Recommendation(RecommendedOil, 4) });

    [Fact]
    public Task Given_ein_erledigter_Termin_mit_Vorschlag_When_das_Panel_offen_ist_Then_zeigt_es_Passt_dazu_und_Mit_kassieren_oeffnet_die_Kassa_mit_Termin_und_Oel()
    {
        var api = RecommendationApi(SpaSample.Paying);
        return Render("hell", "termin-passt-dazu", async (window, main) =>
        {
            var calendar = await SignedInCalendar(main);
            var shell = Assert.IsType<ShellViewModel>(main.Content);
            calendar.OpenAppointment(SpaSample.Paying);
            await PumpUntil(() => Shows(window, "Massageöl Lavendel 100 ml · 24,90 € · 4 am Regal"));

            Assert.True(Shows(window, "Passt dazu"));
            var button = ButtonWithName(window, "Massageöl Lavendel 100 ml mit kassieren");
            Assert.Equal("Mit kassieren", button.Content);
            await ClickIntoView(window, button);
            await PumpUntil(() => api.Calls.Any(c => c.Route == $"POST /api/pos/bills/{SpaSample.BillId}/lines" && c.Body!.Contains(RecommendedOil.Id.ToString(), StringComparison.Ordinal)));

            Assert.Same(shell.Pos, shell.SelectedPage?.Page);
            var appointmentSent = api.Calls.Any(c => c.Route == "POST /api/pos/bills"
                && JsonSerializer.Deserialize<BillInput>(c.Body!, ApiClient.Json)!.AppointmentIds.Contains(SpaSample.Paying.Id));
            Assert.True(appointmentSent, "Der Termin kommt auf die Rechnung");
            var article = api.Calls.Where(c => c.Route == $"POST /api/pos/bills/{SpaSample.BillId}/lines")
                .Select(c => JsonSerializer.Deserialize<BillLineInput>(c.Body!, ApiClient.Json)!).Single(l => l.Kind == BillLineKind.Article);
            Assert.Equal(((Guid?)RecommendedOil.Id, 1), (article.ArticleId, article.Quantity));
            return shell.Pos;
        }, api);
    }

    [Fact]
    public Task Given_ein_gebuchter_Termin_When_die_Rezeption_auf_Erledigt_klickt_Then_laedt_das_Panel_die_Vorschlaege()
    {
        var done = SpaSample.Selected with { Status = AppointmentStatus.Completed, UpdatedAt = SpaSample.At(11, 41) };
        var api = RecommendationApi(SpaSample.Selected).On($"POST /api/appointments/{SpaSample.Selected.Id}/status", done);
        return Render("hell", "termin-erledigt-passt-dazu", async (window, main) =>
        {
            var calendar = await SignedInCalendar(main);
            calendar.OpenAppointment(SpaSample.Selected);
            await Pump(Settling);
            Assert.DoesNotContain(api.Calls, c => c.Route.EndsWith("/recommendations", StringComparison.Ordinal));

            await ClickIntoView(window, window.GetVisualDescendants().OfType<ToggleButton>().Single(t => t.Name == "MoreToggle"));
            await ClickIntoView(window, ButtonWithText(window, "Erledigt"));
            await PumpUntil(() => Shows(window, "Massageöl Lavendel 100 ml · 24,90 € · 4 am Regal"));

            Assert.Single(api.Calls, c => c.Route == $"GET /api/appointments/{SpaSample.Selected.Id}/recommendations");
            Assert.True(ButtonWithName(window, "Massageöl Lavendel 100 ml mit kassieren").IsEnabled);
            return calendar;
        }, api);
    }

    [Fact]
    public Task Given_eine_gespeicherte_Behandlung_When_die_Inhaberin_im_Reiter_Verkauf_das_Oel_hinzufuegt_und_speichert_Then_geht_die_Liste_per_PUT_an_die_API()
    {
        var route = $"/api/treatments/{HotStone60.Id}/recommended-articles";
        var api = TreatmentApi()
            .On($"GET {route}", new List<Guid> { RecommendedCream.Id })
            .OnJson($"PUT {route}", body => JsonSerializer.Deserialize<List<Guid>>(body!, ApiClient.Json)!);
        return Render("hell", "verwaltung-behandlungen-verkauf", async (window, main) =>
        {
            var treatments = await OpenTreatments(window, main);
            await ClickSection(window, "Verkauf");
            Assert.True(Shows(window, "Gesichtscreme Hydro 50 ml"));

            Named<ComboBox>(window, "RecommendedArticlePicker").SelectedItem = treatments.Editor!.ArticleChoices.Single(a => a.Id == RecommendedOil.Id);
            await Pump();
            await ClickIntoView(window, ButtonWithText(window, "Artikel hinzufügen"));
            Assert.True(treatments.HasUnsavedChanges);
            await ClickIntoView(window, ButtonWithText(window, "Speichern"));
            await Pump(Settling);

            var put = Assert.Single(api.Calls, c => c.Route == $"PUT {route}");
            Assert.Equal([RecommendedCream.Id, RecommendedOil.Id], JsonSerializer.Deserialize<List<Guid>>(put.Body!, ApiClient.Json));
            Assert.False(treatments.HasUnsavedChanges);
            return treatments;
        }, api);
    }
}
