using System.Net;
using System.Text.Json;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;
using AispaStudio.Desktop.ViewModels;

namespace AispaStudio.Desktop.Tests;

/// <summary>R05 Slice 97 (Ä191): native Maske „Handbuch“ nach HandbookTab.tsx: Liste und Formular des Rezeptionshandbuchs, Erinnerung am Tagesstart, Löschen mit Rückfrage.</summary>
public sealed class AdminHandbookTests
{
    private const string Route = "/api/handbook";

    private static readonly string[] TextTooLong = ["Der Text ist zu lang."];

    private static readonly Guid DogId = Guid.NewGuid();
    private static readonly Guid KeyId = Guid.NewGuid();
    private static readonly DateTimeOffset Updated = new(2026, 10, 30, 9, 0, 0, TimeSpan.Zero);

    private static readonly HandbookEntryDto Dog = new(DogId, "Gäste mit Hund", "Hunde warten im Vorraum, Wassernapf steht bereit.", "Empfang", true, [DayOfWeek.Monday, DayOfWeek.Friday], true, Updated);
    private static readonly HandbookEntryDto Key = new(KeyId, "Schlüssel Saunabereich", "Liegt in der Schublade links.", null, false, [], false, Updated);

    private static StubApi Api(params HandbookEntryDto[] entries)
    {
        // Die Liste verändert sich mit POST, PUT und DELETE wie die der API.
        var list = entries.ToList();
        return new StubApi()
            .OnJson($"GET {Route}", _ => list.ToList())
            .OnJson($"POST {Route}", body =>
            {
                var created = FromInput(Guid.NewGuid(), body);
                list.Add(created);
                return created;
            }, HttpStatusCode.Created)
            .OnJson($"PUT {Route}/{DogId}", body =>
            {
                var changed = FromInput(DogId, body);
                list[list.FindIndex(e => e.Id == DogId)] = changed;
                return changed;
            })
            .OnJson($"DELETE {Route}/{DogId}", _ =>
            {
                list.RemoveAll(e => e.Id == DogId);
                return new { };
            });
    }

    private static HandbookEntryDto FromInput(Guid id, string? body)
    {
        var input = JsonSerializer.Deserialize<HandbookEntryInput>(body!, ApiClient.Json)!;
        return new(id, input.Title, input.Text, input.Area, input.RemindAtDayStart, input.Days, input.IsActive, Updated);
    }

    private static async Task<HandbookViewModel> Open(StubApi api, FakeDialogs? dialogs = null)
    {
        var handbook = new HandbookViewModel(api.Client()) { Dialogs = dialogs };
        await handbook.EnsureLoadedAsync();
        return handbook;
    }

    private static HandbookEntryInput Sent(StubApi api, string route) => JsonSerializer.Deserialize<HandbookEntryInput>(api.Calls.Single(c => c.Route == route).Body!, ApiClient.Json)!;

    /// <summary>Die Felder einer Eingabe als Wert (ein Record mit Liste ist nur bei derselben Liste gleich).</summary>
    private static (string, string, string?, bool, string, bool) Fields(HandbookEntryInput i) => (i.Title, i.Text, i.Area, i.RemindAtDayStart, string.Join(',', i.Days), i.IsActive);

    [Fact]
    public async Task Given_Eintraege_When_die_Maske_laedt_Then_zeigt_die_Liste_Sterne_und_aus_und_das_Formular_ist_neu()
    {
        var handbook = await Open(Api(Dog, Key));

        Assert.Equal(["Gäste mit Hund ★", "Schlüssel Saunabereich (aus)"], handbook.Entries.Select(e => e.Name));
        Assert.True(handbook.IsCreating);
        Assert.Equal("Neuer Eintrag", handbook.HeadingText);
        Assert.False(handbook.DeleteCommand.CanExecute(null));
        Assert.False(handbook.HasUnsavedChanges);
    }

    [Fact]
    public async Task Given_kein_Eintrag_When_die_Maske_laedt_Then_ist_die_Liste_leer_und_das_Formular_bereit()
    {
        var handbook = await Open(Api());

        Assert.True(handbook.ShowEmpty);
        Assert.True(handbook.IsActive);
        Assert.False(handbook.RemindAtDayStart);
    }

    [Fact]
    public async Task Given_ein_Eintrag_When_er_gewaehlt_wird_Then_zeigt_das_Formular_seine_Felder_und_Tage()
    {
        var handbook = await Open(Api(Dog, Key));

        handbook.SelectedRow = handbook.Entries[0];
        await Task.Yield();

        Assert.False(handbook.IsCreating);
        Assert.Equal("Gäste mit Hund", handbook.HeadingText);
        Assert.Equal(("Gäste mit Hund", "Empfang", "Hunde warten im Vorraum, Wassernapf steht bereit.", true, true),
            (handbook.EntryTitle, handbook.Area, handbook.Text, handbook.RemindAtDayStart, handbook.IsActive));
        Assert.Equal([DayOfWeek.Monday, DayOfWeek.Friday], handbook.Days.Where(d => d.IsSelected).Select(d => d.Day));
        Assert.True(handbook.DeleteCommand.CanExecute(null));
        Assert.False(handbook.HasUnsavedChanges);
    }

    [Fact]
    public async Task Given_Handbucheintrag_When_gespeichert_Then_POST_mit_Tagen_und_Erinnerung()
    {
        var api = Api(Dog);
        var handbook = await Open(api);

        handbook.EntryTitle = "Schlüssel Sauna";
        handbook.Area = "  Sauna ";
        handbook.Text = "Am Empfang abholen.";
        handbook.RemindAtDayStart = true;
        handbook.Days.Single(d => d.Day == DayOfWeek.Monday).IsSelected = true;
        handbook.Days.Single(d => d.Day == DayOfWeek.Friday).IsSelected = true;
        Assert.True(handbook.HasUnsavedChanges);
        Assert.True(await handbook.SaveChangesAsync());

        Assert.Equal(("Schlüssel Sauna", "Am Empfang abholen.", "Sauna", true, "Monday,Friday", true), Fields(Sent(api, $"POST {Route}")));
        Assert.Equal("Gespeichert.", handbook.StatusText);
        Assert.Equal(["Gäste mit Hund ★", "Schlüssel Sauna ★"], handbook.Entries.Select(e => e.Name));
        Assert.Equal("Schlüssel Sauna", handbook.SelectedRow?.Name.Replace(" ★", ""));
        Assert.False(handbook.IsCreating);
        Assert.False(handbook.HasUnsavedChanges);
    }

    [Fact]
    public async Task Given_ohne_Bereich_und_Tage_When_gespeichert_Then_Bereich_null_und_keine_Tage()
    {
        var api = Api();
        var handbook = await Open(api);

        handbook.EntryTitle = "Hygiene";
        handbook.Text = "Handtücher täglich wechseln.";
        Assert.True(await handbook.SaveChangesAsync());

        Assert.Equal(("Hygiene", "Handtücher täglich wechseln.", (string?)null, false, "", true), Fields(Sent(api, $"POST {Route}")));
    }

    [Fact]
    public async Task Given_die_Tage_ohne_Erinnerung_When_gespeichert_Then_gehen_sie_mit_wie_im_Web()
    {
        var api = Api();
        var handbook = await Open(api);

        handbook.EntryTitle = "Hygiene";
        handbook.Text = "Text";
        handbook.Days[2].IsSelected = true;
        Assert.True(await handbook.SaveChangesAsync());

        Assert.Equal([DayOfWeek.Wednesday], Sent(api, $"POST {Route}").Days);
    }

    [Fact]
    public async Task Given_ein_Eintrag_When_bearbeitet_und_gespeichert_Then_PUT()
    {
        var api = Api(Dog, Key);
        var handbook = await Open(api);
        handbook.SelectedRow = handbook.Entries[0];
        await Task.Yield();

        handbook.Text = "Hunde bleiben an der Leine.";
        handbook.IsActive = false;
        handbook.Days.Single(d => d.Day == DayOfWeek.Friday).IsSelected = false;
        Assert.True(await handbook.SaveChangesAsync());

        Assert.Equal(("Gäste mit Hund", "Hunde bleiben an der Leine.", "Empfang", true, "Monday", false), Fields(Sent(api, $"PUT {Route}/{DogId}")));
        Assert.Equal(["Gäste mit Hund ★ (aus)", "Schlüssel Saunabereich (aus)"], handbook.Entries.Select(e => e.Name));
        Assert.Equal("Gespeichert.", handbook.StatusText);
    }

    [Fact]
    public async Task Given_Titel_oder_Text_fehlt_When_gespeichert_Then_Fehler_am_Feld_und_kein_Aufruf()
    {
        var api = Api();
        var handbook = await Open(api);

        Assert.False(await handbook.SaveChangesAsync());
        Assert.Equal("Der Titel fehlt. Trage ihn ein.", handbook.TitleError);
        Assert.Equal("EntryTitleField", handbook.FocusTarget);

        handbook.EntryTitle = "Hygiene";
        Assert.False(await handbook.SaveChangesAsync());
        Assert.Null(handbook.TitleError);
        Assert.Equal("Der Text fehlt. Trage ihn ein.", handbook.TextError);
        Assert.Equal("TextField", handbook.FocusTarget);
        Assert.DoesNotContain(api.Calls, c => c.Route.StartsWith("POST ", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Given_die_API_lehnt_den_Eintrag_ab_When_gespeichert_Then_steht_der_Fehler_da_und_die_Eingabe_bleibt()
    {
        var api = Api().On($"POST {Route}", new { errors = new { text = TextTooLong } }, HttpStatusCode.BadRequest);
        var handbook = await Open(api);
        handbook.EntryTitle = "Hygiene";
        handbook.Text = "Text";

        Assert.False(await handbook.SaveChangesAsync());

        Assert.Equal("Der Text ist zu lang.", handbook.Error);
        Assert.True(handbook.HasUnsavedChanges);
    }

    [Fact]
    public async Task Given_Handbucheintrag_When_Loeschen_bestaetigt_Then_DELETE_und_ein_leeres_neues_Formular()
    {
        var api = Api(Dog, Key);
        var dialogs = new FakeDialogs().Answer(ConfirmChoice.Confirmed);
        var handbook = await Open(api, dialogs);
        handbook.SelectedRow = handbook.Entries[0];
        await Task.Yield();

        await handbook.DeleteCommand.ExecuteAsync(null);

        Assert.Equal(1, api.Calls.Count(c => c.Route == $"DELETE {Route}/{DogId}"));
        Assert.Equal(["Schlüssel Saunabereich (aus)"], handbook.Entries.Select(e => e.Name));
        Assert.True(handbook.IsCreating);
        Assert.Equal("", handbook.EntryTitle);
        Assert.Equal("Gelöscht.", handbook.Message);
        var asked = dialogs.AssertRules().Asked.Single();
        Assert.Equal("Eintrag löschen?", asked.Title);
        Assert.Contains("Gäste mit Hund", asked.Message);
    }

    [Fact]
    public async Task Given_Handbucheintrag_When_Loeschen_abgebrochen_Then_kein_DELETE()
    {
        var api = Api(Dog);
        var handbook = await Open(api, new FakeDialogs().Answer(ConfirmChoice.Cancelled));
        handbook.SelectedRow = handbook.Entries[0];
        await Task.Yield();

        await handbook.DeleteCommand.ExecuteAsync(null);

        Assert.DoesNotContain(api.Calls, c => c.Route.StartsWith("DELETE ", StringComparison.Ordinal));
        Assert.False(handbook.IsCreating);
    }

    [Fact]
    public async Task Given_ungespeicherte_Aenderung_When_ein_anderer_Eintrag_gewaehlt_wird_Then_fragt_die_Maske_und_Abbrechen_bleibt_beim_Eintrag()
    {
        var dialogs = new FakeDialogs().Answer(ConfirmChoice.Cancelled).Answer(ConfirmChoice.Alternative);
        var handbook = await Open(Api(Dog, Key), dialogs);
        handbook.SelectedRow = handbook.Entries[0];
        await Task.Yield();
        handbook.Text = "Geändert";

        handbook.SelectedRow = handbook.Entries[1];
        await Task.Yield();
        Assert.Equal("Gäste mit Hund", handbook.EntryTitle);
        Assert.Equal("Geändert", handbook.Text);

        handbook.SelectedRow = handbook.Entries[1];
        await Task.Yield();
        await Task.Delay(20, TestContext.Current.CancellationToken);
        Assert.Equal("Schlüssel Saunabereich", handbook.EntryTitle);
        Assert.Equal(["Änderungen speichern?", "Änderungen speichern?"], dialogs.AssertRules().Asked.Select(a => a.Title));
    }

    [Fact]
    public async Task Given_geaenderte_Felder_When_verworfen_Then_steht_der_gespeicherte_Stand_wieder_da()
    {
        var handbook = await Open(Api(Dog));
        handbook.SelectedRow = handbook.Entries[0];
        await Task.Yield();
        handbook.EntryTitle = "Anders";
        handbook.Days[1].IsSelected = true;

        handbook.Discard();

        Assert.Equal("Gäste mit Hund", handbook.EntryTitle);
        Assert.Equal([DayOfWeek.Monday, DayOfWeek.Friday], handbook.Days.Where(d => d.IsSelected).Select(d => d.Day));
        Assert.False(handbook.HasUnsavedChanges);
    }

    [Fact]
    public async Task Given_der_Eintrag_Neu_When_geklickt_Then_leeres_Formular_ohne_Auswahl()
    {
        var handbook = await Open(Api(Dog));
        handbook.SelectedRow = handbook.Entries[0];
        await Task.Yield();

        await handbook.NewCommand.ExecuteAsync(null);

        Assert.Null(handbook.SelectedRow);
        Assert.True(handbook.IsCreating);
        Assert.Equal("", handbook.EntryTitle);
        Assert.DoesNotContain(handbook.Days, d => d.IsSelected);
    }
}
