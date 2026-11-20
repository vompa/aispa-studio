using System.Net;
using System.Text.Json;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;
using AispaStudio.Desktop.ViewModels;

namespace AispaStudio.Desktop.Tests;

/// <summary>R05 Slice 94 (Ä191, ADR 0084): Behandlungen als native Maske der Verwaltung, gespeichert mit einem „Speichern“ für alles.</summary>
public sealed class TreatmentsTests
{
    private static readonly QualificationDto HotStone = new(Guid.NewGuid(), "Hot Stone");
    private static readonly QualificationDto Cosmetics = new(Guid.NewGuid(), "Kosmetik");
    private static readonly LocationDto Vienna = new(Guid.NewGuid(), "Wien", "Europe/Vienna", "EUR", []);
    private static readonly RoomDto StoneRoom = new(Guid.NewGuid(), Vienna.Id, "Raum Stein", 10);
    private static readonly EmployeeDto Lena = new(Guid.NewGuid(), "Lena Huber", "#2f6f5e", [HotStone.Id], []);
    private static readonly EmployeeDto Marco = new(Guid.NewGuid(), "Marco Berger", "#7a4fa3", [Cosmetics.Id], []);
    private static readonly TreatmentDto HotStone60 = new(Guid.NewGuid(), "Hot Stone 60", 60, 10, 10, 89m, [HotStone.Id], [StoneRoom.Id], [], "Massagen");
    private static readonly TreatmentDto Facial = new(Guid.NewGuid(), "Gesichtsbehandlung", 45, 5, 5, 75m, [Cosmetics.Id], [StoneRoom.Id], [], "Kosmetik");
    private static readonly TreatmentDto FootBath = new(Guid.NewGuid(), "Fußbad", 20, 0, 5, 29m, [], [StoneRoom.Id], [], null);

    /// <summary>Speichern gibt zurück, was ankam, und die Liste enthält danach neu angelegte Behandlungen, wie die API.</summary>
    private static StubApi Api()
    {
        var treatments = new List<TreatmentDto> { HotStone60, Facial, FootBath };
        var api = new StubApi()
            .OnJson("GET /api/treatments", _ => treatments.ToList())
            .On("GET /api/qualifications", new List<QualificationDto> { HotStone, Cosmetics })
            .On("GET /api/locations", new List<LocationDto> { Vienna })
            .On("GET /api/employees", new List<EmployeeDto> { Lena, Marco })
            .On("GET /api/stock/items", new List<StockItemDto>())
            .On($"GET /api/locations/{Vienna.Id}/resources", new LocationResources(Vienna, [StoneRoom], [], [Lena, Marco], [], []))
            .OnJson("POST /api/treatments", body =>
            {
                var created = Saved(Guid.NewGuid(), body);
                treatments.Add(created);
                return created;
            });
        foreach (var treatment in new[] { HotStone60, Facial, FootBath })
        {
            api.On($"GET /api/treatments/{treatment.Id}/price-rules", new List<TreatmentPriceRuleDto>())
                .On($"GET /api/reports/treatments/{treatment.Id}/preparation", new List<PreparationStepDto>())
                .On($"GET /api/costs/treatments/{treatment.Id}/materials", new List<MaterialLine>())
                .On($"GET /api/treatments/{treatment.Id}/extras", new List<TreatmentExtraDto>())
                .On($"GET /api/treatments/{treatment.Id}/history", new List<HistoryEntry>())
                .OnJson($"PUT /api/treatments/{treatment.Id}", body => Saved(treatment.Id, body))
                .OnJson($"PUT /api/reports/treatments/{treatment.Id}/preparation", body =>
                    JsonSerializer.Deserialize<List<PreparationStepInput>>(body!, ApiClient.Json)!.Select(s => new PreparationStepDto(Guid.NewGuid(), s.Text, s.MinutesBefore)).ToList());
        }

        return api;
    }

    private static TreatmentDto Saved(Guid id, string? body)
    {
        var input = JsonSerializer.Deserialize<TreatmentInput>(body!, ApiClient.Json)!;
        return new TreatmentDto(id, input.Name, input.DurationMinutes, input.PreparationMinutes, input.FollowUpMinutes, input.Price, input.RequiredQualificationIds,
            input.RoomIds, input.EquipmentIds, input.Category);
    }

    private static async Task<TreatmentsViewModel> Loaded(StubApi api, FakeDialogs? dialogs = null, bool showsMaterial = true)
    {
        var treatments = new TreatmentsViewModel(api.Client(), TimeZoneInfo.Utc, showsMaterial) { Dialogs = dialogs };
        await treatments.LoadAsync();
        await Until(() => treatments.Editor?.IsLoaded == true);
        return treatments;
    }

    private static async Task<TreatmentEditorViewModel> Choose(TreatmentsViewModel treatments, string title)
    {
        if (title == "Neue Behandlung")
        {
            await treatments.NewCommand.ExecuteAsync(null);
        }
        else
        {
            treatments.SelectedEntry = treatments.Entries.Single(e => e.Title == title);
        }

        var expected = title;
        await Until(() => treatments.Editor?.Title == expected && treatments.Editor.IsLoaded);
        return treatments.Editor!;
    }

    /// <summary>Wartet auf Arbeit, die das Modell im Hintergrund anstößt (Laden der Teil-Listen).</summary>
    private static async Task Until(Func<bool> condition)
    {
        for (var i = 0; i < 200 && !condition(); i++)
        {
            await Task.Delay(10);
        }

        Assert.True(condition());
    }

    private static ShellViewModel Shell(StubApi api) =>
        new(api.Client(), TestData.Owner(), null, TimeProvider.System, new ThemeController(new MemorySettingsStore()), "Test", () => Task.CompletedTask);

    [Fact]
    public async Task Given_Behandlungen_mit_Kategorien_When_die_Maske_laedt_Then_Liste_nach_Kategorie_ohne_Kategorie_zuletzt_mit_Dauer_und_Preis()
    {
        var treatments = await Loaded(Api());

        Assert.Equal(["Kosmetik", "Gesichtsbehandlung", "Massagen", "Hot Stone 60", "Ohne Kategorie", "Fußbad"], treatments.Entries.Select(e => e.Title));
        Assert.Equal([true, false, true, false, true, false], treatments.Entries.Select(e => e.IsHeading));
        Assert.Equal([1, 1, 1], treatments.Entries.Where(e => e.IsHeading).Select(e => e.Count));
        Assert.Equal([null, "45 Min. · 75 €", null, "60 Min. · 89 €", null, "20 Min. · 29 €"], treatments.Entries.Select(e => e.Meta));
        Assert.Equal("Gesichtsbehandlung", treatments.SelectedEntry?.Title);
        Assert.Equal(Facial.Id, treatments.Editor?.Id);
    }

    [Fact]
    public async Task Given_Hot_Stone_60_zu_89_Euro_When_die_Inhaberin_95_eintraegt_und_speichert_Then_geht_95_an_die_API_und_keine_Teil_Liste()
    {
        var api = Api();
        var treatments = await Loaded(api);
        var editor = await Choose(treatments, "Hot Stone 60");
        Assert.Equal(89m, editor.Price);

        editor.Price = 95m;
        Assert.True(treatments.HasUnsavedChanges);
        Assert.True(await treatments.SaveChangesAsync());

        var put = Assert.Single(api.Calls, c => c.Route == $"PUT /api/treatments/{HotStone60.Id}");
        var sent = JsonSerializer.Deserialize<TreatmentInput>(put.Body!, ApiClient.Json)!;
        Assert.Equal(95m, sent.Price);
        Assert.Equal("Hot Stone 60", sent.Name);
        Assert.Equal([HotStone.Id], sent.RequiredQualificationIds);
        Assert.Single(api.Calls, c => c.Route.StartsWith("PUT ", StringComparison.Ordinal));
        Assert.False(treatments.HasUnsavedChanges);
        Assert.Equal("Gespeichert", treatments.Toast.Text);
        Assert.Same(editor, treatments.Editor);
    }

    [Fact]
    public async Task Given_ein_neuer_Vorbereitungsschritt_When_gespeichert_wird_Then_nur_die_Vorbereitung_geht_an_die_API()
    {
        var api = Api();
        var treatments = await Loaded(api);
        var editor = await Choose(treatments, "Hot Stone 60");

        editor.AddStepCommand.Execute(null);
        editor.PreparationSteps[0].Text = "Steine auf 55 °C aufheizen";
        editor.PreparationSteps[0].MinutesBefore = 30;
        Assert.True(await treatments.SaveChangesAsync());

        var put = Assert.Single(api.Calls, c => c.Route.StartsWith("PUT ", StringComparison.Ordinal));
        Assert.Equal($"PUT /api/reports/treatments/{HotStone60.Id}/preparation", put.Route);
        var step = Assert.Single(JsonSerializer.Deserialize<List<PreparationStepInput>>(put.Body!, ApiClient.Json)!);
        Assert.Equal(("Steine auf 55 °C aufheizen", (int?)30), (step.Text, step.MinutesBefore));
        Assert.False(treatments.HasUnsavedChanges);
    }

    [Fact]
    public async Task Given_neue_Behandlung_ohne_Qualifikation_When_gespeichert_wird_Then_Hinweis_am_Feld_und_erst_das_zweite_Speichern_legt_an()
    {
        var api = Api();
        var treatments = await Loaded(api);
        var editor = await Choose(treatments, "Neue Behandlung");
        Assert.True(editor.IsNew);
        editor.Name = "Kräuterstempel";
        editor.Price = 59m;

        Assert.False(await treatments.SaveChangesAsync());
        Assert.DoesNotContain(api.Calls, c => c.Route == "POST /api/treatments");
        Assert.StartsWith("Keine Qualifikation gewählt: Jede Person im Team kann diese Behandlung übernehmen.", editor.QualificationNotice, StringComparison.Ordinal);
        Assert.Equal(TreatmentEditorViewModel.Resources, editor.SelectedSection);
        Assert.Equal(TreatmentEditorViewModel.QualificationsField, editor.FocusTarget);

        Assert.True(await treatments.SaveChangesAsync());
        var post = Assert.Single(api.Calls, c => c.Route == "POST /api/treatments");
        var sent = JsonSerializer.Deserialize<TreatmentInput>(post.Body!, ApiClient.Json)!;
        Assert.Equal(("Kräuterstempel", 59m), (sent.Name, sent.Price));
        Assert.Empty(sent.RequiredQualificationIds);
        Assert.Equal("Kräuterstempel", treatments.SelectedEntry?.Title);
        Assert.False(editor.IsNew);
    }

    [Fact]
    public async Task Given_der_Hinweis_auf_die_fehlende_Qualifikation_When_eine_Qualifikation_gewaehlt_wird_Then_verschwindet_er_und_Speichern_legt_mit_ihr_an()
    {
        var api = Api();
        var treatments = await Loaded(api);
        var editor = await Choose(treatments, "Neue Behandlung");
        editor.Name = "Kräuterstempel";
        Assert.False(await treatments.SaveChangesAsync());

        editor.Qualifications.Single(q => q.Label == "Hot Stone").IsChecked = true;

        Assert.Null(editor.QualificationNotice);
        Assert.True(await treatments.SaveChangesAsync());
        var sent = JsonSerializer.Deserialize<TreatmentInput>(Assert.Single(api.Calls, c => c.Route == "POST /api/treatments").Body!, ApiClient.Json)!;
        Assert.Equal([HotStone.Id], sent.RequiredQualificationIds);
    }

    [Fact]
    public async Task Given_eine_Behandlung_ohne_Namen_When_gespeichert_wird_Then_Fehler_am_Namen_und_nichts_gesendet()
    {
        var api = Api();
        var treatments = await Loaded(api);
        var editor = await Choose(treatments, "Hot Stone 60");
        editor.SelectSection(TreatmentEditorViewModel.Prices);
        editor.Name = " ";

        Assert.False(await treatments.SaveChangesAsync());

        Assert.Equal("Der Name der Behandlung fehlt. Trage ihn ein, wie Gäste ihn bei der Buchung sehen.", editor.NameError);
        Assert.Equal(TreatmentEditorViewModel.Basics, editor.SelectedSection);
        Assert.Equal(TreatmentEditorViewModel.NameField, editor.FocusTarget);
        Assert.DoesNotContain(api.Calls, c => c.Route.StartsWith("PUT ", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Given_Hot_Stone_gewaehlt_When_die_Maske_Wer_kann_das_zeigt_Then_nur_wer_die_Qualifikation_hat_und_der_Klick_meldet_die_Person()
    {
        var treatments = await Loaded(Api());
        var editor = await Choose(treatments, "Hot Stone 60");
        Guid? requested = null;
        treatments.PersonRequested += id => requested = id;

        Assert.Equal(["Lena Huber"], editor.WhoCanDo.Select(p => p.Name));
        editor.WhoCanDo[0].Open.Execute(null);
        Assert.Equal(Lena.Id, requested);

        editor.Qualifications.Single(q => q.Label == "Kosmetik").IsChecked = true;
        Assert.Empty(editor.WhoCanDo);
        Assert.True(editor.HasNobody);
    }

    [Fact]
    public async Task Given_geaenderte_Behandlung_When_eine_andere_gewaehlt_wird_Then_fragt_die_Maske_Abbrechen_bleibt_Verwerfen_wechselt()
    {
        var dialogs = new FakeDialogs().Answer(ConfirmChoice.Cancelled).Answer(ConfirmChoice.Alternative);
        var treatments = await Loaded(Api(), dialogs);
        var editor = await Choose(treatments, "Hot Stone 60");
        editor.Price = 95m;

        treatments.SelectedEntry = treatments.Entries.Single(e => e.Title == "Fußbad");
        await Until(() => dialogs.Asked.Count == 1);
        await Task.Yield();
        Assert.Same(editor, treatments.Editor);
        Assert.Equal("Hot Stone 60", treatments.SelectedEntry?.Title);
        Assert.Equal(95m, editor.Price);

        treatments.SelectedEntry = treatments.Entries.Single(e => e.Title == "Fußbad");
        await Until(() => treatments.Editor?.Title == "Fußbad");
        Assert.Equal("Fußbad", treatments.SelectedEntry?.Title);
        Assert.Equal(["Änderungen speichern?", "Änderungen speichern?"], dialogs.AssertRules().Asked.Select(a => a.Title));
    }

    [Fact]
    public async Task Given_Hot_Stone_60_When_die_Inhaberin_sie_nicht_mehr_anbietet_Then_nach_der_Rueckfrage_geloescht_und_aus_der_Liste()
    {
        var api = Api().On($"DELETE /api/treatments/{HotStone60.Id}", null, HttpStatusCode.NoContent);
        var dialogs = new FakeDialogs().Answer(ConfirmChoice.Confirmed);
        var treatments = await Loaded(api, dialogs);
        await Choose(treatments, "Hot Stone 60");

        treatments.Editor!.DeactivateCommand.Execute(null);
        await Until(() => treatments.Entries.All(e => e.Title != "Hot Stone 60"));

        var asked = Assert.Single(dialogs.AssertRules().Asked);
        Assert.Equal("„Hot Stone 60“ nicht mehr anbieten?", asked.Title);
        Assert.Single(api.Calls, c => c.Route == $"DELETE /api/treatments/{HotStone60.Id}");
        Assert.DoesNotContain(treatments.Entries, e => e.Title == "Hot Stone 60");
    }

    [Fact]
    public async Task Given_der_Kalender_springt_zu_Hot_Stone_60_When_die_Verwaltung_oeffnet_Then_die_native_Maske_im_Abschnitt_Ablauf()
    {
        var shell = Shell(Api());

        shell.Calendar.AdminRequested!("masterData", "treatments", HotStone60.Id);
        await shell.AdminPage.InitializeAsync();

        Assert.Same(shell.AdminPage, shell.SelectedPage?.Page);
        Assert.Equal("Behandlungen", shell.AdminPage.SelectedArea?.Title);
        Assert.Same(shell.AdminPage.Treatments, shell.AdminPage.Content);
        await Until(() => shell.AdminPage.Treatments!.Editor?.Id == HotStone60.Id);
        // ADR 0098: „ablauf“ ist eine Gruppe im Bereich „Durchführung“; die alte Adresse führt dorthin.
        Assert.Equal(TreatmentEditorViewModel.Execution, shell.AdminPage.Treatments!.Editor!.SelectedSection);
        Assert.Equal(TreatmentEditorViewModel.Process, shell.AdminPage.Treatments.Editor.GroupTarget);
        shell.Dispose();
    }

    [Fact]
    public async Task Given_Wer_kann_das_When_die_Inhaberin_eine_Person_anklickt_Then_oeffnet_die_Verwaltung_den_Bereich_Team()
    {
        var shell = Shell(TeamSample.AddTeam(Api(), [Lena, Marco], [], [], []));
        shell.ShowAdmin("masterData", "treatments", HotStone60.Id);
        await shell.AdminPage.InitializeAsync();
        await Until(() => shell.AdminPage.Treatments!.Editor is { IsLoaded: true, WhoCanDo.Count: 1 });

        shell.AdminPage.Treatments!.Editor!.WhoCanDo[0].Open.Execute(null);
        await Until(() => shell.AdminPage.Team?.Card?.EmployeeId == Lena.Id);

        // R05 Slice 98: Team ist nativ; „Wer kann das?“ öffnet die Person beim Reiter Qualifikationen.
        Assert.Equal("Team", shell.AdminPage.SelectedArea?.Title);
        Assert.Same(shell.AdminPage.Team, shell.AdminPage.Content);
        Assert.Equal("Lena Huber", shell.AdminPage.Team!.SelectedEntry?.Title);
        Assert.Equal(PersonCardViewModel.ProfileArea, shell.AdminPage.Team.Card!.SelectedTab);
        Assert.Equal(PersonCardViewModel.QualificationsTab, shell.AdminPage.Team.Card.FocusedSection);
        shell.Dispose();
    }

    [Fact]
    public async Task Given_eine_Standortleitung_ohne_Recht_auf_Betraege_When_sie_Hot_Stone_60_oeffnet_Then_geladen_ohne_Abschnitt_Material_und_ohne_Kostenabfrage()
    {
        var api = Api();
        var treatments = await Loaded(api, showsMaterial: false);
        var editor = await Choose(treatments, "Hot Stone 60");

        Assert.Null(editor.LoadError);
        Assert.False(editor.HasMaterial);
        Assert.DoesNotContain(api.Calls, c => c.Route.Contains("/api/costs/", StringComparison.Ordinal));
        // Der Sprung von „Kosten“ landet trotzdem im Bereich, nur ohne die Gruppe, die es für sie nicht gibt.
        editor.SelectSection(TreatmentEditorViewModel.Material);
        Assert.Equal(TreatmentEditorViewModel.Execution, editor.SelectedSection);
        Assert.Null(editor.GroupTarget);
        editor.Price = 95m;
        Assert.True(treatments.HasUnsavedChanges);
    }

    [Fact]
    public async Task Given_eine_Teil_Liste_laedt_nicht_When_der_Preis_geaendert_und_gespeichert_wird_Then_fragt_die_Maske_beim_Wechsel_und_speichert_keine_Teil_Liste()
    {
        var api = Api().On($"GET /api/treatments/{HotStone60.Id}/extras", null, HttpStatusCode.InternalServerError);
        var dialogs = new FakeDialogs().Answer(ConfirmChoice.Cancelled);
        var treatments = await Loaded(api, dialogs);
        treatments.SelectedEntry = treatments.Entries.Single(e => e.Title == "Hot Stone 60");
        await Until(() => treatments.Editor?.Id == HotStone60.Id && treatments.Editor.HasLoadError);
        var editor = treatments.Editor!;

        editor.Price = 95m;
        Assert.True(treatments.HasUnsavedChanges);
        treatments.SelectedEntry = treatments.Entries.Single(e => e.Title == "Fußbad");
        await Until(() => dialogs.Asked.Count == 1);
        Assert.Same(editor, treatments.Editor);

        Assert.True(await treatments.SaveChangesAsync());
        Assert.Single(api.Calls, c => c.Route.StartsWith("PUT ", StringComparison.Ordinal));
        Assert.Single(api.Calls, c => c.Route == $"PUT /api/treatments/{HotStone60.Id}");
    }

    [Fact]
    public async Task Given_ein_Sprungziel_das_nicht_mehr_angeboten_wird_When_die_Verwaltung_es_oeffnet_Then_bleibt_die_offene_Behandlung_und_eine_Meldung_erklaert_es()
    {
        var treatments = await Loaded(Api());
        var editor = await Choose(treatments, "Hot Stone 60");

        treatments.Open(Guid.NewGuid(), TreatmentEditorViewModel.Process, null);

        Assert.Same(editor, treatments.Editor);
        Assert.Equal(TreatmentEditorViewModel.Overview, editor.SelectedSection);
        Assert.Equal("Diese Behandlung wird nicht mehr angeboten und steht deshalb nicht in der Liste.", treatments.Notice);
        Assert.False(treatments.Editor!.HasError);
    }

    [Fact]
    public async Task Given_Nicht_mehr_anbieten_scheitert_When_die_API_ablehnt_Then_Meldung_in_der_Leiste_der_Behandlung_und_kein_Ladefehler()
    {
        var api = Api().On($"DELETE /api/treatments/{HotStone60.Id}", new { detail = "Die Behandlung ist noch in offenen Gutscheinen." }, HttpStatusCode.Conflict);
        var dialogs = new FakeDialogs().Answer(ConfirmChoice.Confirmed);
        var treatments = await Loaded(api, dialogs);
        var editor = await Choose(treatments, "Hot Stone 60");

        editor.DeactivateCommand.Execute(null);
        await Until(() => treatments.HasNotice);

        Assert.Null(treatments.LoadError);
        Assert.True(treatments.HasNotice);
        Assert.Same(editor, treatments.Editor);
        Assert.Contains(treatments.Entries, e => e.Title == "Hot Stone 60");
    }

    private static readonly List<ArticleDto> Articles =
    [
        new(Guid.NewGuid(), "Massageöl Lavendel 100 ml", 24.90m, TaxCategory.Normal, Guid.NewGuid()),
        new(Guid.NewGuid(), "Gesichtscreme Hydro 50 ml", 39m, TaxCategory.Normal, Guid.NewGuid()),
        new(Guid.NewGuid(), "Ingwertee", 3.50m, TaxCategory.Reduced1, Guid.NewGuid()),
        new(Guid.NewGuid(), "Saunatuch", 5m, TaxCategory.Normal, Guid.NewGuid()),
        new(Guid.NewGuid(), "Badesalz", 12m, TaxCategory.Normal, Guid.NewGuid()),
        new(Guid.NewGuid(), "Duftkerze", 18m, TaxCategory.Normal, Guid.NewGuid()),
    ];

    /// <summary>Hot Stone 60 empfiehlt „Massageöl Lavendel 100 ml“; die Kassa kennt sechs Artikel.</summary>
    private static StubApi SalesApi(params ArticleDto[] recommended) => Api()
        .On("GET /api/pos/articles", Articles)
        .On($"GET /api/treatments/{HotStone60.Id}/recommended-articles", recommended.Select(a => a.Id).ToList());

    [Fact]
    public async Task Given_Hot_Stone_mit_Massageoel_When_die_Inhaberin_die_Creme_dazunimmt_und_verwirft_Then_steht_wieder_nur_das_Oel_da()
    {
        var treatments = await Loaded(SalesApi(Articles[0]));
        var editor = await Choose(treatments, "Hot Stone 60");
        Assert.Equal(["Massageöl Lavendel 100 ml"], editor.RecommendedArticles.Select(r => r.Name));

        editor.ArticleToAdd = editor.ArticleChoices.Single(a => a.Id == Articles[1].Id);
        editor.AddRecommendedArticleCommand.Execute(null);
        Assert.True(treatments.HasUnsavedChanges);
        editor.Discard();

        Assert.Equal(["Massageöl Lavendel 100 ml"], editor.RecommendedArticles.Select(r => r.Name));
        Assert.False(treatments.HasUnsavedChanges);
    }

    [Fact]
    public async Task Passt_dazu_laedt_nicht_dann_laesst_sich_kein_Artikel_hinzufuegen()
    {
        var treatments = await Loaded(Api().On("GET /api/pos/articles", Articles));
        var editor = await Choose(treatments, "Hot Stone 60");

        editor.ArticleToAdd = Articles[1];

        Assert.True(editor.HasSalesLoadError);
        Assert.Empty(editor.ArticleChoices);
        Assert.False(editor.AddRecommendedArticleCommand.CanExecute(null));
    }

    [Fact]
    public async Task Given_Passt_dazu_laedt_nicht_When_Erneut_laden_gewaehlt_wird_Then_verschwindet_die_Warnung_und_ein_Artikel_laesst_sich_hinzufuegen()
    {
        var api = Api().On("GET /api/pos/articles", Articles);
        var treatments = await Loaded(api);
        var editor = await Choose(treatments, "Hot Stone 60");
        Assert.True(editor.HasSalesLoadError);

        api.On($"GET /api/treatments/{HotStone60.Id}/recommended-articles", new List<Guid>());
        await editor.ReloadSalesCommand.ExecuteAsync(null);

        Assert.False(editor.HasSalesLoadError);
        Assert.False(treatments.HasUnsavedChanges);
        editor.ArticleToAdd = editor.ArticleChoices[0];
        Assert.True(editor.AddRecommendedArticleCommand.CanExecute(null));
    }

    [Fact]
    public async Task Given_kein_Artikel_gewaehlt_When_Artikel_hinzufuegen_Then_Fehler_am_Feld_und_Fokus_und_die_Liste_bleibt()
    {
        var treatments = await Loaded(SalesApi([.. Articles.Take(2)]));
        var editor = await Choose(treatments, "Hot Stone 60");
        var before = editor.RecommendedArticles.Count;

        Assert.True(editor.AddRecommendedArticleCommand.CanExecute(null));
        editor.AddRecommendedArticleCommand.Execute(null);

        Assert.Equal("Bitte einen Artikel wählen.", editor.ArticleToAddError);
        Assert.Equal(TreatmentEditorViewModel.RecommendedArticleField, editor.FocusTarget);
        Assert.Equal(before, editor.RecommendedArticles.Count);
        editor.ArticleToAdd = editor.ArticleChoices[0];
        Assert.Null(editor.ArticleToAddError);
    }

    [Fact]
    public async Task Given_fuenf_empfohlene_Artikel_When_ein_sechster_gewaehlt_wird_Then_laesst_die_Maske_ihn_nicht_hinzufuegen()
    {
        var treatments = await Loaded(SalesApi([.. Articles.Take(5)]));
        var editor = await Choose(treatments, "Hot Stone 60");

        editor.ArticleToAdd = editor.ArticleChoices.Single(a => a.Id == Articles[5].Id);

        Assert.False(editor.AddRecommendedArticleCommand.CanExecute(null));
    }

    [Fact]
    public async Task Given_das_Oel_ist_empfohlen_When_die_Inhaberin_es_entfernt_und_speichert_Then_geht_eine_leere_Liste_an_die_API()
    {
        var api = SalesApi(Articles[0]).OnJson($"PUT /api/treatments/{HotStone60.Id}/recommended-articles", body => JsonSerializer.Deserialize<List<Guid>>(body!, ApiClient.Json)!);
        var treatments = await Loaded(api);
        var editor = await Choose(treatments, "Hot Stone 60");

        editor.RecommendedArticles[0].RemoveCommand.Execute(null);
        Assert.True(await treatments.SaveChangesAsync());

        var put = Assert.Single(api.Calls, c => c.Route.StartsWith("PUT ", StringComparison.Ordinal));
        Assert.Equal($"PUT /api/treatments/{HotStone60.Id}/recommended-articles", put.Route);
        Assert.Empty(JsonSerializer.Deserialize<List<Guid>>(put.Body!, ApiClient.Json)!);
        Assert.False(treatments.HasUnsavedChanges);
    }

    [Fact]
    public async Task Given_eine_neue_Behandlung_When_die_Maske_offen_ist_Then_fehlt_die_Übersicht_und_eine_gespeicherte_hat_sie_zuerst()
    {
        var treatments = await Loaded(SalesApi());

        var saved = await Choose(treatments, "Hot Stone 60");
        Assert.Equal(TreatmentEditorViewModel.Overview, saved.Sections[0].Id);
        var created = await Choose(treatments, "Neue Behandlung");

        // Ohne gespeicherte Behandlung gibt es keine Übersicht, und „Passt dazu“ fehlt in „Grunddaten und Preis“.
        Assert.Equal([TreatmentEditorViewModel.Basics, TreatmentEditorViewModel.Execution, TreatmentEditorViewModel.Resources], created.Sections.Select(s => s.Id));
        Assert.Equal(TreatmentEditorViewModel.Basics, created.SelectedSection);
        created.SelectSection(TreatmentEditorViewModel.Sales);
        Assert.Equal(TreatmentEditorViewModel.Basics, created.SelectedSection);
        Assert.Null(created.GroupTarget);
    }

    [Fact]
    public async Task Given_eine_gespeicherte_Behandlung_When_die_Maske_offen_ist_Then_hat_die_Schiene_vier_Bereiche_in_fester_Reihenfolge()
    {
        var treatments = await Loaded(SalesApi());

        var editor = await Choose(treatments, "Hot Stone 60");

        // masken.md R15 (ADR 0098): eine Karte neben einer Liste hat höchstens vier Bereiche.
        Assert.Equal(["Übersicht", "Grunddaten und Preis", "Durchführung", "Qualifikationen und Räume"], editor.Sections.Select(s => s.Label));
        Assert.Equal(TreatmentEditorViewModel.Overview, editor.SelectedSection);
        Assert.True(editor.HasMaterial);
    }

    [Theory]
    [InlineData("uebersicht", TreatmentEditorViewModel.Overview, null)]
    [InlineData("grunddaten", TreatmentEditorViewModel.Basics, null)]
    [InlineData("preise-und-verkauf", TreatmentEditorViewModel.Basics, null)]
    [InlineData("ablauf", TreatmentEditorViewModel.Execution, "ablauf")]
    [InlineData("material", TreatmentEditorViewModel.Execution, "material")]
    [InlineData("bereitstellen", TreatmentEditorViewModel.Execution, "bereitstellen")]
    [InlineData("preise", TreatmentEditorViewModel.Basics, "preise")]
    [InlineData("verkauf", TreatmentEditorViewModel.Basics, "verkauf")]
    [InlineData("ressourcen", TreatmentEditorViewModel.Resources, null)]
    [InlineData("durchfuehrung", TreatmentEditorViewModel.Execution, null)]
    [InlineData("gibt-es-nicht", TreatmentEditorViewModel.Basics, null)]
    public async Task Given_eine_alte_Abschnittsadresse_When_die_Verwaltung_dorthin_springt_Then_oeffnet_der_richtige_Bereich_bei_der_richtigen_Gruppe(
        string address, string area, string? group)
    {
        var treatments = await Loaded(SalesApi());
        await Choose(treatments, "Hot Stone 60");

        treatments.Open(HotStone60.Id, address, null);

        Assert.Equal(area, treatments.Editor!.SelectedSection);
        Assert.Equal(group, treatments.Editor.GroupTarget);
        Assert.Single(treatments.Editor.Sections, s => s.IsOn);
    }

    [Fact]
    public async Task Given_das_Material_laesst_sich_nicht_speichern_When_gespeichert_wird_Then_oeffnet_der_Bereich_Durchfuehrung_bei_der_Gruppe_Material()
    {
        var api = Api().On($"PUT /api/costs/treatments/{HotStone60.Id}/materials", new { detail = "Die Menge fehlt." }, HttpStatusCode.BadRequest);
        var treatments = await Loaded(api);
        var editor = await Choose(treatments, "Hot Stone 60");
        editor.AddMaterialCommand.Execute(null);
        editor.SelectSection(TreatmentEditorViewModel.Resources);

        Assert.False(await treatments.SaveChangesAsync());

        Assert.True(editor.HasError);
        Assert.Equal(TreatmentEditorViewModel.Execution, editor.SelectedSection);
        Assert.Equal(TreatmentEditorViewModel.Material, editor.GroupTarget);
        Assert.True(treatments.HasUnsavedChanges);
    }

    [Fact]
    public async Task Given_eine_gespeicherte_Behandlung_When_die_Inhaberin_Verlauf_waehlt_Then_oeffnet_die_Seitenleiste_und_laedt_bei_jedem_Oeffnen_neu()
    {
        var api = Api();
        var treatments = await Loaded(api);
        var editor = await Choose(treatments, "Hot Stone 60");
        Assert.False(editor.IsHistoryOpen);
        Assert.DoesNotContain(api.Calls, c => c.Route == $"GET /api/treatments/{HotStone60.Id}/history");

        editor.ToggleHistoryCommand.Execute(null);
        await Until(() => api.Calls.Count(c => c.Route == $"GET /api/treatments/{HotStone60.Id}/history") == 1);
        Assert.True(editor.IsHistoryOpen);
        Assert.Same(editor.History, Assert.Single(editor.HistorySections));

        editor.ToggleHistoryCommand.Execute(null);
        Assert.False(editor.IsHistoryOpen);
        editor.ToggleHistoryCommand.Execute(null);
        await Until(() => api.Calls.Count(c => c.Route == $"GET /api/treatments/{HotStone60.Id}/history") == 2);
    }

    [Fact]
    public async Task Given_eine_neue_Behandlung_When_die_Maske_offen_ist_Then_gibt_es_keinen_Verlauf()
    {
        var treatments = await Loaded(Api());

        var created = await Choose(treatments, "Neue Behandlung");

        Assert.False(created.HasHistory);
        Assert.Empty(created.HistorySections);
        Assert.False(created.ToggleHistoryCommand.CanExecute(null));
    }

    [Fact]
    public async Task Given_eine_Behandlung_mit_eigener_Ausfallgebuehr_When_die_Maske_oeffnet_Then_sind_die_weiteren_Angaben_offen_sonst_zu()
    {
        var own = HotStone60 with { Id = Guid.NewGuid(), Name = "Hot Stone 90", NoShowFeePercent = 50 };
        var api = Api();
        api.OnJson("GET /api/treatments", _ => new List<TreatmentDto> { HotStone60, own })
            .On($"GET /api/treatments/{own.Id}/price-rules", new List<TreatmentPriceRuleDto>())
            .On($"GET /api/reports/treatments/{own.Id}/preparation", new List<PreparationStepDto>())
            .On($"GET /api/costs/treatments/{own.Id}/materials", new List<MaterialLine>())
            .On($"GET /api/treatments/{own.Id}/extras", new List<TreatmentExtraDto>());
        var treatments = await Loaded(api);

        // masken.md R2: Seltenes ist eingeklappt; ist ein Feld darin befüllt, ist die Gruppe offen.
        Assert.False((await Choose(treatments, "Hot Stone 60")).MoreOpen);
        Assert.True((await Choose(treatments, "Hot Stone 90")).MoreOpen);
    }

    [Fact]
    public async Task Given_die_Liste_When_eine_Kategorie_zugeklappt_wird_Then_zeigt_sie_nur_die_Ueberschrift_mit_Anzahl_und_klappt_wieder_auf()
    {
        var treatments = await Loaded(Api());
        var massages = treatments.Entries.Single(e => e.Title == "Massagen");

        treatments.SelectedEntry = massages;

        var collapsed = treatments.Entries.Single(e => e.Title == "Massagen");
        Assert.True(collapsed.IsCollapsed);
        Assert.Equal(1, collapsed.Count);
        Assert.DoesNotContain(treatments.Entries, e => e.Title == "Hot Stone 60");
        Assert.Equal("1 Behandlung, eingeklappt", collapsed.Description);
        Assert.Equal(Facial.Id, treatments.Editor?.Id);

        treatments.SelectedEntry = collapsed;

        Assert.Contains(treatments.Entries, e => e.Title == "Hot Stone 60");
        Assert.False(treatments.Entries.Single(e => e.Title == "Massagen").IsCollapsed);
    }

    [Fact]
    public async Task Given_ein_Suchbegriff_When_er_Name_oder_Kategorie_trifft_Then_filtert_die_Liste_klappt_alles_auf_und_sagt_bei_keinem_Treffer_Bescheid()
    {
        var treatments = await Loaded(Api());
        treatments.SelectedEntry = treatments.Entries.Single(e => e.Title == "Massagen");

        treatments.Search = "stone";
        // Bleibt eine Kategorie übrig, steht keine Überschrift mehr da.
        Assert.Equal(["Hot Stone 60"], treatments.Entries.Select(e => e.Title));
        Assert.False(treatments.HasNoMatches);

        treatments.Search = "kosmetik";
        Assert.Equal(["Gesichtsbehandlung"], treatments.Entries.Select(e => e.Title));

        treatments.Search = "gibt es nicht";
        Assert.Empty(treatments.Entries);
        Assert.True(treatments.HasNoMatches);
    }

    [Fact]
    public async Task Given_eine_nicht_mehr_angebotene_Behandlung_When_der_Schalter_an_ist_Then_steht_sie_ausgegraut_in_der_Liste_und_Wieder_anbieten_holt_sie_zurueck()
    {
        var retired = FootBath with { Id = Guid.NewGuid(), Name = "Altes Fußbad", IsActive = false };
        var api = Api()
            .On("GET /api/treatments?includeInactive=true", new List<TreatmentDto> { HotStone60, Facial, FootBath, retired })
            .On($"GET /api/treatments/{retired.Id}/price-rules", new List<TreatmentPriceRuleDto>())
            .On($"GET /api/reports/treatments/{retired.Id}/preparation", new List<PreparationStepDto>())
            .On($"GET /api/costs/treatments/{retired.Id}/materials", new List<MaterialLine>())
            .On($"GET /api/treatments/{retired.Id}/extras", new List<TreatmentExtraDto>())
            .On($"GET /api/treatments/{retired.Id}/history", new List<HistoryEntry>())
            .On($"POST /api/treatments/{retired.Id}/reactivate", retired with { IsActive = true });
        var treatments = await Loaded(api);
        Assert.DoesNotContain(treatments.Entries, e => e.Title == "Altes Fußbad");

        treatments.ShowInactive = true;
        await Until(() => treatments.Entries.Any(e => e.Title == "Altes Fußbad"));

        var row = treatments.Entries.Single(e => e.Title == "Altes Fußbad");
        Assert.False(row.IsOffered);
        Assert.Equal("20 Min. · 29 €, nicht angeboten", row.Description);
        var editor = await Choose(treatments, "Altes Fußbad");
        Assert.True(editor.IsInactive);
        Assert.False(editor.CanEdit);
        Assert.False(editor.CanDeactivate);

        editor.ReactivateCommand.Execute(null);
        await Until(() => treatments.Entries.Single(e => e.Title == "Altes Fußbad").IsOffered);

        Assert.Single(api.Calls, c => c.Route == $"POST /api/treatments/{retired.Id}/reactivate");
        Assert.False(treatments.Editor!.IsInactive);
        Assert.Equal("Altes Fußbad", treatments.SelectedEntry?.Title);
    }

    [Fact]
    public async Task Given_Hot_Stone_60_When_die_Uebersicht_zaehlt_Then_vier_Kacheln_mit_Preis_Dauer_Personen_und_Raeumen_und_der_Klick_springt()
    {
        var treatments = await Loaded(Api());
        var editor = await Choose(treatments, "Hot Stone 60");

        Assert.Equal(["Preis", "Dauer", "Dürfen behandeln", "Räume"], editor.Tiles.Select(t => t.Label));
        Assert.Equal(["89 €", "60 Min.", "1 Person", "1 Raum"], editor.Tiles.Select(t => t.Value));
        Assert.Equal("Hot Stone", editor.Tiles[2].Hint);

        editor.Price = 89.5m;
        editor.Tiles[0].Command.Execute(null);

        Assert.Equal("89,50 €", editor.Tiles[0].Value);
        Assert.Equal(TreatmentEditorViewModel.Basics, editor.SelectedSection);
        Assert.Equal(TreatmentEditorViewModel.PriceField, editor.FocusTarget);
        editor.Tiles[3].Command.Execute(null);
        Assert.Equal(TreatmentEditorViewModel.Resources, editor.SelectedSection);
    }

    [Fact]
    public async Task Given_ein_Sprung_zu_den_Preisen_nach_Zeit_When_er_ankommt_Then_ist_Weitere_Angaben_offen()
    {
        var treatments = await Loaded(Api());
        var editor = await Choose(treatments, "Hot Stone 60");
        Assert.False(editor.MoreOpen);

        editor.SelectSection(TreatmentEditorViewModel.Prices);

        Assert.True(editor.MoreOpen);
        Assert.Equal(TreatmentEditorViewModel.Basics, editor.SelectedSection);
        Assert.Equal(TreatmentEditorViewModel.Prices, editor.GroupTarget);
    }

    [Fact]
    public async Task Given_eine_neue_Behandlung_mit_Eingaben_When_eine_andere_gewaehlt_wird_Then_fragt_die_Maske_und_bleibt_bei_Abbruch_bei_der_neuen()
    {
        var dialogs = new FakeDialogs().Answer(ConfirmChoice.Cancelled);
        var treatments = await Loaded(Api(), dialogs);
        var created = await Choose(treatments, "Neue Behandlung");
        created.Name = "Kräuterstempel";

        treatments.SelectedEntry = treatments.Entries.Single(e => e.Title == "Fußbad");
        await Until(() => dialogs.Asked.Count == 1);

        Assert.Same(created, treatments.Editor);
        Assert.Equal("Änderungen speichern?", dialogs.AssertRules().Asked.Single().Title);
    }
}
