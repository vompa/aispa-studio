using System.Text.Json;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;
using AispaStudio.Desktop.ViewModels;

namespace AispaStudio.Desktop.Tests;

/// <summary>R05 Slice 96 (Ä191, ADR 0084): Erinnerungen als native Maske; geplant wird in der API nach dem gespeicherten Stand.</summary>
public sealed class RemindersTests
{
    internal static readonly ReminderSettingsDto Day = new(true, 24, 2, ReminderChannel.Email, null, "de");

    internal static readonly RebookSettingsDto Rebook = new(true, [new RebookIntervalDto(TestData.MassageTreatment.Id, 56)]);

    internal static readonly RebookReminderDto Sent = new(Guid.NewGuid(), Guid.NewGuid(), "Lena Huber", "Gesicht", "Sent", true, "Liebe Lena …",
        new DateTimeOffset(2026, 10, 19, 11, 30, 0, TimeSpan.Zero), new DateTimeOffset(2026, 10, 19, 11, 40, 0, TimeSpan.Zero), null);

    internal static StubApi Api() => new StubApi()
        .On("GET /api/reminders/settings", Day)
        .OnJson("PUT /api/reminders/settings", body => JsonSerializer.Deserialize<ReminderSettingsDto>(body!, ApiClient.Json)!)
        .On("GET /api/retention/settings", Rebook)
        .OnJson("PUT /api/retention/settings", body => JsonSerializer.Deserialize<RebookSettingsDto>(body!, ApiClient.Json)!)
        .On("GET /api/treatments", new[] { TestData.Facial, TestData.MassageTreatment })
        .On("GET /api/retention/reminders", new[] { Sent });

    private static async Task<RemindersViewModel> Loaded(StubApi api)
    {
        var reminders = new RemindersViewModel(api.Client(), TimeZoneInfo.Utc);
        await reminders.LoadAsync();
        Assert.True(reminders.IsLoaded);
        return reminders;
    }

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
    public async Task Given_Wiederbuchen_When_Tage_fuer_eine_Behandlung_eingetragen_Then_PUT_der_Abstaende_und_leer_heisst_keine()
    {
        var api = Api();
        var reminders = await Loaded(api);
        Assert.Equal(["Gesicht (Tage, leer = keine)", "Massage (Tage, leer = keine)"], reminders.Intervals.Select(r => r.Label));
        Assert.Equal([null, 56m], reminders.Intervals.Select(r => r.Days));

        reminders.Intervals[0].Days = 42m;
        reminders.Intervals[1].Days = null;
        Assert.True(reminders.HasUnsavedChanges);
        Assert.True(await reminders.SaveChangesAsync());

        var put = Assert.Single(api.Calls, c => c.Route == "PUT /api/retention/settings");
        var sent = JsonSerializer.Deserialize<RebookSettingsDto>(put.Body!, ApiClient.Json)!;
        Assert.True(sent.Enabled);
        Assert.Equal([new RebookIntervalDto(TestData.Facial.Id, 42)], sent.Intervals);
        Assert.DoesNotContain(api.Calls, c => c.Route == "PUT /api/reminders/settings");
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
    public async Task Given_verschickte_Erinnerung_When_geladen_Then_Zeile_wie_im_Web()
    {
        var reminders = await Loaded(Api());

        var row = Assert.Single(reminders.Recent);
        Assert.Equal("Lena Huber, Gesicht: gesendet (Text von der KI) 19.10.26, 11:40", row.Line);
        Assert.Equal("Liebe Lena …", row.Text);
        Assert.True(reminders.HasRecent);
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

    /// <summary>Die Behandlungsliste der API enthält nur angebotene Behandlungen; Abstände anderer Behandlungen bleiben wie im Web erhalten.</summary>
    [Fact]
    public async Task Given_Abstand_einer_nicht_mehr_angebotenen_Behandlung_When_geladen_und_gespeichert_Then_bleibt_er_erhalten()
    {
        var retired = new RebookIntervalDto(Guid.NewGuid(), 90);
        var api = Api().On("GET /api/retention/settings", Rebook with { Intervals = [.. Rebook.Intervals, retired] });
        var reminders = await Loaded(api);
        Assert.False(reminders.HasUnsavedChanges);

        reminders.Intervals[0].Days = 42m;
        Assert.True(await reminders.SaveChangesAsync());

        var put = Assert.Single(api.Calls, c => c.Route == "PUT /api/retention/settings");
        var sent = JsonSerializer.Deserialize<RebookSettingsDto>(put.Body!, ApiClient.Json)!;
        Assert.Contains(retired, sent.Intervals);
        Assert.Contains(new RebookIntervalDto(TestData.Facial.Id, 42), sent.Intervals);
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
    public async Task Given_Liste_zuletzt_verschickt_laedt_nicht_When_geladen_Then_ist_die_Maske_trotzdem_bedienbar()
    {
        var api = Api().On("GET /api/retention/reminders", new { title = "Fehler" }, System.Net.HttpStatusCode.InternalServerError);
        var reminders = await Loaded(api);

        Assert.False(reminders.HasRecent);
        Assert.Equal(24m, reminders.FirstHours);
    }
}
