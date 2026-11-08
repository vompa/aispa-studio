using System.Text.Json;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;
using AispaStudio.Desktop.ViewModels;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>R05 Slice 97 (Ä191): native Maske „Handbuch“ im Fenster: Eintrag anlegen per Tastatur und Strg+S, Löschen über die Rückfrage, Scans.</summary>
public sealed partial class ViewTests
{
    private static readonly Guid HandbookDogId = Guid.Parse("00000000-0000-0000-0000-0000000c9702");

    private static readonly HandbookEntryDto HandbookDog = new(HandbookDogId, "Gäste mit Hund", "Hunde warten im Vorraum, Wassernapf steht bereit.", "Empfang", true,
        [DayOfWeek.Monday, DayOfWeek.Friday], true, SpaSample.At(9));

    private static StubApi HandbookApi(StubApi? api = null)
    {
        var list = new List<HandbookEntryDto> { HandbookDog };
        return (api ?? Api())
            .OnJson("GET /api/handbook", _ => list.ToList())
            .OnJson("POST /api/handbook", body =>
            {
                var input = JsonSerializer.Deserialize<HandbookEntryInput>(body!, ApiClient.Json)!;
                var created = new HandbookEntryDto(Guid.NewGuid(), input.Title, input.Text, input.Area, input.RemindAtDayStart, input.Days, input.IsActive, SpaSample.At(9));
                list.Add(created);
                return created;
            }, System.Net.HttpStatusCode.Created)
            .OnJson($"DELETE /api/handbook/{HandbookDogId}", _ =>
            {
                list.RemoveAll(e => e.Id == HandbookDogId);
                return new { };
            });
    }

    private static async Task<ShellViewModel> OpenHandbook(Window window, MainViewModel main)
    {
        var shell = await OpenAdminPage(window, main);
        shell.AdminPage.SelectedArea = shell.AdminPage.Areas.Single(a => a.Title == "Handbuch");
        await Pump(Settling);
        await Pump(Settling);
        Assert.Same(shell.AdminPage.Handbook, shell.AdminPage.Content);
        Assert.True(shell.AdminPage.Handbook!.IsLoaded);
        return shell;
    }

    [Theory]
    [InlineData("hell")]
    [InlineData("dunkel")]
    public Task Verwaltung_Handbuch(string theme) => Render(theme, "verwaltung-handbuch", async (window, main) =>
    {
        var shell = await OpenHandbook(window, main);
        var handbook = shell.AdminPage.Handbook!;
        handbook.SelectedRow = handbook.Entries[0];
        await Pump(Settling);
        Assert.Equal("Gäste mit Hund", Named<TextBox>(window, "EntryTitleField").Text);
        Assert.Equal("Empfang", Named<TextBox>(window, "AreaField").Text);
        Assert.True(Shows(window, "Gäste mit Hund ★"));
        Assert.True(Named<CheckBox>(window, "DayMonday").IsChecked);
        Assert.False(Named<CheckBox>(window, "DayTuesday").IsChecked);
        return shell.AdminPage;
    }, HandbookApi());

    [Fact]
    public Task Handbucheintrag_anlegen_ueber_Tastatur_und_Strg_S()
    {
        var api = HandbookApi();
        return Render("hell", "verwaltung-handbuch-anlegen", async (window, main) =>
        {
            var shell = await OpenHandbook(window, main);
            var handbook = shell.AdminPage.Handbook!;

            await TypeInto(window, "EntryTitleField", "Schlüssel Sauna");
            await TypeInto(window, "AreaField", "Sauna");
            await TypeInto(window, "TextField", "Am Empfang abholen.");
            await ClickScrolled(window, "RemindBox");
            await ClickScrolled(window, "DayMonday");
            Assert.True(handbook.HasUnsavedChanges);
            PressSave(window);
            await Pump(Settling);

            var post = JsonSerializer.Deserialize<HandbookEntryInput>(Assert.Single(api.Calls, c => c.Route == "POST /api/handbook").Body!, ApiClient.Json)!;
            Assert.Equal(("Schlüssel Sauna", "Sauna", "Am Empfang abholen.", true, "Monday"), (post.Title, post.Area, post.Text, post.RemindAtDayStart, string.Join(',', post.Days)));
            Assert.False(handbook.HasUnsavedChanges);
            Assert.True(Shows(window, "Gespeichert."));
            Assert.Equal(["Gäste mit Hund ★", "Schlüssel Sauna ★"], handbook.Entries.Select(e => e.Name));
            return shell.AdminPage;
        }, api);
    }

    [Fact]
    public Task Handbucheintrag_loeschen_fragt_nach_und_sendet_DELETE()
    {
        var api = HandbookApi();
        return Render("hell", "verwaltung-handbuch-loeschen", async (window, main) =>
        {
            var shell = await OpenHandbook(window, main);
            var handbook = shell.AdminPage.Handbook!;
            handbook.SelectedRow = handbook.Entries[0];
            await Pump(Settling);

            await ClickScrolled(window, "DeleteButton");
            Assert.Equal("Eintrag löschen?", Assert.IsType<ConfirmDialogViewModel>(main.DialogHost.Current).Title);
            Assert.Equal(0, CallsTo(api, $"DELETE /api/handbook/{HandbookDogId}"));
            ClickCenter(window, DialogButton(window, "ConfirmButton"));
            await Pump(Settling);

            Assert.Equal(1, CallsTo(api, $"DELETE /api/handbook/{HandbookDogId}"));
            Assert.True(Shows(window, "Gelöscht."));
            Assert.Empty(handbook.Entries);
            return shell.AdminPage;
        }, api);
    }

    [Fact]
    public Task Handbuch_melden_keine_Bindungsfehler() => Render("hell", "bindungen-handbuch", async (window, main) =>
    {
        await WithBindingLog(async log =>
        {
            var shell = await OpenAdminPage(window, main);
            await AssertNoBindingErrors(log, "Handbuch", () =>
            {
                shell.AdminPage.SelectedArea = shell.AdminPage.Areas.Single(a => a.Title == "Handbuch");
                return Task.CompletedTask;
            });
            var handbook = shell.AdminPage.Handbook!;
            await AssertNoBindingErrors(log, "Eintrag", () => { handbook.SelectedRow = handbook.Entries[0]; return Task.CompletedTask; });
            await AssertNoBindingErrors(log, "Eingabe", () => { handbook.EntryTitle = "x"; handbook.RemindAtDayStart = false; handbook.Days[0].IsSelected = true; return Task.CompletedTask; });
            await AssertNoBindingErrors(log, "Verworfen", () => { handbook.Discard(); return Task.CompletedTask; });
            await AssertNoBindingErrors(log, "Neu", async () => await handbook.NewCommand.ExecuteAsync(null));
            await AssertNoBindingErrors(log, "Fehler", async () => await handbook.SaveChangesAsync());
            await AssertNoBindingErrors(log, "Anderer Bereich", () =>
            {
                shell.AdminPage.SelectedArea = shell.AdminPage.Areas.Single(a => a.Title == "Behandlungen");
                return Task.CompletedTask;
            });
            log.AssertClean();
            return shell.AdminPage;
        });
        return true;
    }, HandbookApi());

    [Fact]
    public Task Jede_Seite_Handbuch_hat_fuer_jedes_bedienbare_Element_einen_lesbaren_Namen() => Render("hell", "namen-handbuch", async (window, main) =>
    {
        var shell = await OpenHandbook(window, main);
        var handbook = shell.AdminPage.Handbook!;
        var problems = new SortedSet<string>(StringComparer.Ordinal);

        void Scan(string state)
        {
            foreach (var control in window.GetVisualDescendants().OfType<Control>().Where(c => c is Button or CheckBox or RadioButton or TextBox or ComboBox or ListBoxItem))
            {
                if (control.IsEffectivelyVisible && control.IsEffectivelyEnabled && control.Focusable && HasUnreadableName(control, out var name))
                {
                    problems.Add($"{state}: {control.GetType().Name}#{control.Name} \"{name}\"");
                }
            }
        }

        Scan("Neuer Eintrag");
        handbook.SelectedRow = handbook.Entries[0];
        await Pump(Settling);
        Scan("Eintrag");
        handbook.RemindAtDayStart = false;
        await Pump(Settling);
        Scan("Ohne Erinnerung");

        Assert.True(problems.Count == 0, $"{problems.Count} Elemente:{Environment.NewLine}{string.Join(Environment.NewLine, problems)}");
        return shell.AdminPage;
    }, HandbookApi());
}
