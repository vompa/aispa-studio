using System.Text.Json;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;
using AispaStudio.Desktop.ViewModels;

namespace AispaStudio.Desktop.Tests;

/// <summary>R05 Slice 96 (Ä191, ADR 0084) und P8 (17.11.2026): Erinnerungen als native Maske; geplant wird in der API nach dem gespeicherten Stand.</summary>
public sealed class RemindersTests
{
    internal static readonly ReminderSettingsDto Day = new(true, 24, 2, ReminderChannel.Email, null, "de");

    /// <summary>Standard 42 Tage, die Kategorie „Gesicht“ nach 35, die Massage nach 56; „Massage“ liegt in der Kategorie „Massagen“.</summary>
    internal static readonly RebookSettingsDto Rebook = new(true, [new RebookIntervalDto(TestData.MassageTreatment.Id, 56)])
    {
        DefaultDays = 42,
        CategoryIntervals = [new RebookCategoryIntervalDto("Gesicht", 35)],
        SilentTreatmentIds = [],
    };

    internal static readonly DispatchSummaryDto Summary = new(30, new DispatchKindSummaryDto(34, 0, 2), new DispatchKindSummaryDto(12, 1, 0));

    internal static StubApi Api() => new StubApi()
        .On("GET /api/reminders/settings", Day)
        .OnJson("PUT /api/reminders/settings", body => JsonSerializer.Deserialize<ReminderSettingsDto>(body!, ApiClient.Json)!)
        .On("GET /api/retention/settings", Rebook)
        .OnJson("PUT /api/retention/settings", body => JsonSerializer.Deserialize<RebookSettingsDto>(body!, ApiClient.Json)!)
        .On("GET /api/treatments", new[] { TestData.Facial, TestData.MassageTreatment })
        .On("GET /api/dispatch-log/summary", Summary);

    private static async Task<RemindersViewModel> Loaded(StubApi api)
    {
        var reminders = new RemindersViewModel(api.Client(), TimeZoneInfo.Utc);
        await reminders.LoadAsync();
        Assert.True(reminders.IsLoaded);
        return reminders;
    }

    private static RebookSettingsDto SentRebook(StubApi api) =>
        JsonSerializer.Deserialize<RebookSettingsDto>(Assert.Single(api.Calls, c => c.Route == "PUT /api/retention/settings").Body!, ApiClient.Json)!;

    [Fact]
    public async Task Given_24_Stunden_When_48_eingetragen_und_gespeichert_Then_PUT_mit_48_und_nur_die_Terminerinnerung()
    {
        var api = Api();
        var reminders = await Loaded(api);
        Assert.Equal(24m, reminders.FirstHours);

        reminders.FirstHours = 48m;
        Assert.True(reminders.HasUnsavedChanges);
        Assert.True(await reminders.SaveChangesAsync());

        var put = Assert.Single(api.Calls, c => c.Route == "PUT /api/reminders/settings");
        Assert.Equal(Day with { FirstHoursBefore = 48 }, JsonSerializer.Deserialize<ReminderSettingsDto>(put.Body!, ApiClient.Json));
        Assert.DoesNotContain(api.Calls, c => c.Route == "PUT /api/retention/settings");
        Assert.False(reminders.HasUnsavedChanges);
        Assert.Equal("Gespeichert.", reminders.StatusText);
    }

    [Fact]
    public async Task Given_Standard_und_Ausnahmen_When_geladen_Then_Kategorien_vor_Behandlungen_und_das_Menue_nennt_den_Rest()
    {
        var reminders = await Loaded(Api());

        Assert.Equal(42m, reminders.RebookDefaultDays);
        Assert.Equal(["Kategorie Gesicht", "Massage"], reminders.Exceptions.Select(r => r.Label));
        Assert.Equal([35m, 56m], reminders.Exceptions.Select(r => r.Days));
        Assert.Equal(["Kategorie Massagen", "Gesicht"], reminders.ExceptionChoices.Select(c => c.Label));
        Assert.False(reminders.HasUnsavedChanges);
    }

    [Fact]
    public async Task Given_Standard_When_28_eingetragen_und_gespeichert_Then_PUT_mit_neuem_Standard_und_unveraenderten_Ausnahmen()
    {
        var api = Api();
        var reminders = await Loaded(api);

        reminders.RebookDefaultDays = 28m;
        Assert.True(reminders.HasUnsavedChanges);
        Assert.True(await reminders.SaveChangesAsync());

        var sent = SentRebook(api);
        Assert.Equal(28, sent.DefaultDays);
        Assert.Equal([new RebookCategoryIntervalDto("Gesicht", 35)], sent.CategoryIntervals);
        Assert.Equal([new RebookIntervalDto(TestData.MassageTreatment.Id, 56)], sent.Intervals);
        Assert.Empty(sent.SilentTreatmentIds!);
        Assert.DoesNotContain(api.Calls, c => c.Route == "PUT /api/reminders/settings");
    }

    [Fact]
    public async Task Given_Standard_geleert_When_gespeichert_Then_PUT_ohne_Standard_aber_mit_Kategorien_damit_die_API_ihn_loescht()
    {
        var api = Api();
        var reminders = await Loaded(api);

        reminders.RebookDefaultDays = null;
        Assert.True(await reminders.SaveChangesAsync());

        var sent = SentRebook(api);
        Assert.Null(sent.DefaultDays);
        Assert.NotNull(sent.CategoryIntervals);
    }

    [Fact]
    public async Task Given_Menue_When_Behandlung_gewaehlt_Then_Zeile_ohne_Tage_heisst_kein_Hinweis_und_wird_als_Ausnahme_ohne_Hinweis_gespeichert()
    {
        var api = Api();
        var reminders = await Loaded(api);

        reminders.NewException = reminders.ExceptionChoices.Single(c => c.Label == "Gesicht");

        Assert.Null(reminders.NewException);
        var row = Assert.Single(reminders.Exceptions, r => r.TreatmentId == TestData.Facial.Id);
        Assert.True(row.HasNoHint);
        Assert.DoesNotContain(reminders.ExceptionChoices, c => c.TreatmentId == TestData.Facial.Id);
        Assert.True(reminders.HasUnsavedChanges);
        Assert.True(await reminders.SaveChangesAsync());

        var sent = SentRebook(api);
        Assert.Equal([TestData.Facial.Id], sent.SilentTreatmentIds);
        Assert.Equal([new RebookIntervalDto(TestData.MassageTreatment.Id, 56)], sent.Intervals);
    }

    [Fact]
    public async Task Given_Ausnahme_When_Tage_eingetragen_und_eine_andere_entfernt_Then_PUT_mit_dem_neuen_Stand()
    {
        var api = Api();
        var reminders = await Loaded(api);
        reminders.NewException = reminders.ExceptionChoices.Single(c => c.Label == "Kategorie Massagen");
        reminders.Exceptions.Single(r => r.Category == "Massagen").Days = 49m;
        reminders.Exceptions.Single(r => r.TreatmentId == TestData.MassageTreatment.Id).RemoveCommand.Execute(null);

        Assert.True(reminders.HasUnsavedChanges);
        Assert.Contains(reminders.ExceptionChoices, c => c.TreatmentId == TestData.MassageTreatment.Id);
        Assert.True(await reminders.SaveChangesAsync());

        var sent = SentRebook(api);
        Assert.Empty(sent.Intervals);
        Assert.Equal([new RebookCategoryIntervalDto("Gesicht", 35), new RebookCategoryIntervalDto("Massagen", 49)], sent.CategoryIntervals);
    }

    [Fact]
    public async Task Given_Aenderung_When_verworfen_Then_Standard_und_Ausnahmen_wie_gespeichert()
    {
        var reminders = await Loaded(Api());
        reminders.RebookDefaultDays = 70m;
        reminders.Exceptions[0].RemoveCommand.Execute(null);
        Assert.True(reminders.HasUnsavedChanges);

        reminders.Discard();

        Assert.Equal(42m, reminders.RebookDefaultDays);
        Assert.Equal(["Kategorie Gesicht", "Massage"], reminders.Exceptions.Select(r => r.Label));
        Assert.False(reminders.HasUnsavedChanges);
    }

    [Fact]
    public async Task Given_WhatsApp_When_gewaehlt_Then_erscheinen_Vorlage_und_Sprache()
    {
        var reminders = await Loaded(Api());
        Assert.False(reminders.IsWhatsApp);

        reminders.Channel = RemindersViewModel.Channels.Single(c => c.Value == ReminderChannel.WhatsApp);

        Assert.True(reminders.IsWhatsApp);
        Assert.Equal("de", reminders.WhatsAppLanguage);
    }

    [Fact]
    public async Task Given_Zahlen_der_letzten_30_Tage_When_geladen_Then_je_Abschnitt_eine_Zeile_und_Fehlgeschlagenes_als_eigene_Marke()
    {
        var reminders = await Loaded(Api());

        Assert.Equal("Letzte 30 Tage: 34 verschickt, 0 fehlgeschlagen", reminders.ReminderSummary);
        Assert.False(reminders.HasReminderFailed);
        Assert.Equal("Letzte 30 Tage: 12 verschickt,", reminders.RebookSummary);
        Assert.Equal("1 fehlgeschlagen", reminders.RebookFailed);
        Assert.True(reminders.HasRebookFailed);
    }

    [Fact]
    public async Task Given_noch_nichts_verschickt_When_geladen_Then_die_Zeile_sagt_das()
    {
        var reminders = await Loaded(Api().On("GET /api/dispatch-log/summary", new DispatchSummaryDto(30, new(0, 0, 0), new(0, 0, 0))));

        Assert.Equal("Letzte 30 Tage: noch nichts verschickt", reminders.ReminderSummary);
        Assert.Equal("Letzte 30 Tage: noch nichts verschickt", reminders.RebookSummary);
    }

    [Fact]
    public async Task Given_API_lehnt_ab_When_gespeichert_Then_Fehler_und_ungespeichert()
    {
        var api = Api().On("PUT /api/reminders/settings",
            new { title = "Eingaben prüfen", errors = new Dictionary<string, string[]> { ["SecondHoursBefore"] = ["Die zweite Erinnerung muss vor der ersten liegen."] } },
            System.Net.HttpStatusCode.BadRequest);
        var reminders = await Loaded(api);

        reminders.SecondHours = 3m;
        Assert.False(await reminders.SaveChangesAsync());

        Assert.Contains("Die zweite Erinnerung muss vor der ersten liegen.", reminders.Error, StringComparison.Ordinal);
        Assert.True(reminders.HasUnsavedChanges);
    }

    /// <summary>Die Behandlungsliste der API enthält nur angebotene Behandlungen; Ausnahmen anderer Behandlungen bleiben wie im Web erhalten.</summary>
    [Fact]
    public async Task Given_Ausnahmen_einer_nicht_mehr_angebotenen_Behandlung_When_geladen_und_gespeichert_Then_bleiben_sie_erhalten()
    {
        var retired = new RebookIntervalDto(Guid.NewGuid(), 90);
        var retiredSilent = Guid.NewGuid();
        var api = Api().On("GET /api/retention/settings", Rebook with { Intervals = [.. Rebook.Intervals, retired], SilentTreatmentIds = [retiredSilent] });
        var reminders = await Loaded(api);
        Assert.False(reminders.HasUnsavedChanges);
        Assert.Equal(2, reminders.Exceptions.Count);

        reminders.RebookDefaultDays = 30m;
        Assert.True(await reminders.SaveChangesAsync());

        var sent = SentRebook(api);
        Assert.Contains(retired, sent.Intervals);
        Assert.Equal([retiredSilent], sent.SilentTreatmentIds);
    }

    [Fact]
    public async Task Given_zweite_Erinnerung_nicht_naeher_am_Termin_When_gespeichert_Then_Fehler_am_Feld_Fokus_und_kein_Aufruf()
    {
        var api = Api();
        var reminders = await Loaded(api);

        reminders.SecondHours = 30m;
        Assert.False(await reminders.SaveChangesAsync());

        Assert.Equal("Die zweite Erinnerung muss näher am Termin liegen als die erste.", reminders.SecondHoursError);
        Assert.Equal(RemindersViewModel.SecondHoursField, reminders.FocusTarget);
        Assert.DoesNotContain(api.Calls, c => c.Route.StartsWith("PUT ", StringComparison.Ordinal));

        reminders.SecondHours = 0m;
        Assert.Null(reminders.SecondHoursError);
    }

    [Fact]
    public async Task Given_WhatsApp_ohne_Vorlage_When_gespeichert_Then_Fehler_an_der_Vorlage_und_kein_Aufruf()
    {
        var api = Api();
        var reminders = await Loaded(api);

        reminders.Channel = RemindersViewModel.Channels.Single(c => c.Value == ReminderChannel.WhatsApp);
        Assert.False(await reminders.SaveChangesAsync());

        Assert.Equal("Für WhatsApp braucht es eine freigegebene Vorlage.", reminders.TemplateError);
        Assert.Equal(RemindersViewModel.TemplateField, reminders.FocusTarget);
        Assert.DoesNotContain(api.Calls, c => c.Route.StartsWith("PUT ", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Given_Zahlen_laden_nicht_When_geladen_Then_ist_die_Maske_trotzdem_bedienbar()
    {
        var api = Api().On("GET /api/dispatch-log/summary", new { title = "Fehler" }, System.Net.HttpStatusCode.InternalServerError);
        var reminders = await Loaded(api);

        Assert.Null(reminders.ReminderSummary);
        Assert.Null(reminders.RebookSummary);
        Assert.Equal(24m, reminders.FirstHours);
    }
}
