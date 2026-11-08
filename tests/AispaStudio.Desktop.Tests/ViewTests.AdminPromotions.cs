using AispaStudio.Contracts;
using AispaStudio.Desktop.ViewModels;
using Avalonia.Controls;
using Avalonia.VisualTree;
using static AispaStudio.Desktop.Tests.PromotionSample;

namespace AispaStudio.Desktop.Tests;

/// <summary>R05 Slice 101 (Ä191): native Maske „Aktionen“ im Fenster: Aktion anlegen per Klick, Tastatur und Strg+S, Archivieren ohne Rückfrage, Bildtests, Scans.</summary>
public sealed partial class ViewTests
{
    /// <summary>Die Bildtests laufen am 19.10.2026; Standorte und Katalog kommen aus dem Beispiel der Fenstertests.</summary>
    private static StubApi PromotionsApi() => PromotionSample.Api(new DateOnly(2026, 10, 19), Api(), catalog: false);

    private static async Task<(ShellViewModel Shell, PromotionsViewModel Promotions)> OpenPromotions(Window window, MainViewModel main)
    {
        var shell = await OpenAdminPage(window, main);
        shell.AdminPage.SelectedArea = shell.AdminPage.Areas.Single(a => a.Title == "Aktionen");
        await Pump(Settling);
        await Pump(Settling);
        Assert.Same(shell.AdminPage.Promotions, shell.AdminPage.Content);
        Assert.True(shell.AdminPage.Promotions!.IsLoaded);
        return (shell, shell.AdminPage.Promotions);
    }

    [Theory]
    [InlineData("hell")]
    [InlineData("dunkel")]
    public Task Verwaltung_Aktionen(string theme) => Render(theme, "verwaltung-aktionen", async (window, main) =>
    {
        var (shell, promotions) = await OpenPromotions(window, main);
        promotions.SelectedRow = promotions.Rows.Single(r => r.Id == SpringId);
        await Pump(Settling);
        Assert.Equal("Frühling", Named<TextBox>(window, "PromotionNameField").Text);
        Assert.True(Shows(window, "Läuft"));
        Assert.True(Shows(window, "Geplant"));
        Assert.True(Shows(window, "Pausiert"));
        Assert.True(Shows(window, "5,00 €"));
        Assert.True(Named<RadioButton>(window, "ScopeTreatmentsOption").IsChecked);
        Assert.True(Shows(window, "Klassische Massage"));
        return shell.AdminPage;
    }, PromotionsApi());

    [Fact]
    public Task Aktion_anlegen_ueber_Klick_und_Strg_S()
    {
        var api = PromotionsApi();
        return Render("hell", "verwaltung-aktionen-anlegen", async (window, main) =>
        {
            var (shell, promotions) = await OpenPromotions(window, main);
            Assert.True(promotions.IsCreating);

            await TypeInto(window, "PromotionNameField", "Herbst");
            await TypeInto(window, "PromotionGuestTextField", "10 % auf alle Massagen im Oktober");
            await TypeInto(window, "PromotionValueField", "10");
            Assert.True(promotions.HasUnsavedChanges);
            PressSave(window);
            await Pump(Settling);

            var sent = Sent(api, "POST /api/promotions");
            Assert.Equal(("Herbst", "10 % auf alle Massagen im Oktober", 10m, (decimal?)null, true, PromotionScope.AllTreatments, true),
                (sent.Name, sent.GuestText, sent.DiscountPercent, sent.DiscountAmount, sent.AllLocations, sent.Scope, sent.IsActive));
            Assert.Equal(30, sent.ValidUntil.DayNumber - sent.ValidFrom.DayNumber);
            Assert.False(promotions.HasUnsavedChanges);
            Assert.True(Shows(window, "Gespeichert."));
            Assert.Equal("Herbst", promotions.SelectedRow?.Name);
            return shell.AdminPage;
        }, api);
    }

    [Fact]
    public Task Aktion_mit_Betrag_und_Behandlung_ueber_Klick_anlegen()
    {
        var api = PromotionsApi();
        return Render("hell", "verwaltung-aktionen-betrag", async (window, main) =>
        {
            var (shell, promotions) = await OpenPromotions(window, main);

            await TypeInto(window, "PromotionNameField", "Hamam-Tage");
            await TypeInto(window, "PromotionGuestTextField", "5 € auf das Hamam-Ritual");
            await ClickScrolled(window, "AmountOption");
            await TypeInto(window, "PromotionValueField", "5");
            await ClickScrolled(window, "ScopeTreatmentsOption");
            Named<ComboBox>(window, "TreatmentPickerField").SelectedItem = promotions.CatalogTreatments.Single(t => t.Id == HamamRitual.Id);
            await Pump(Settling);
            Assert.True(Shows(window, HamamRitual.Name));
            await ClickScrolled(window, "SaveButton");

            var sent = Sent(api, "POST /api/promotions");
            Assert.Equal([HamamRitual.Id], sent.TreatmentIds);
            Assert.Equal((5m, (decimal?)null, PromotionScope.SelectedTreatments, HamamRitual.Id), (sent.DiscountAmount, sent.DiscountPercent, sent.Scope, Assert.Single(sent.TreatmentIds!)));
            Assert.True(Shows(window, "Gespeichert."));
            return shell.AdminPage;
        }, api);
    }

    [Fact]
    public Task Ohne_Rabatt_und_Strg_S_zeigt_den_Fehler_am_Feld_mit_Fokus_und_sendet_nichts()
    {
        var api = PromotionsApi();
        return Render("hell", "verwaltung-aktionen-fehler", async (window, main) =>
        {
            var (shell, promotions) = await OpenPromotions(window, main);
            await TypeInto(window, "PromotionNameField", "Herbst");
            await TypeInto(window, "PromotionGuestTextField", "Text");
            PressSave(window);
            await Pump(Settling);

            Assert.Equal("Rabatt in Prozent zwischen 0,01 und 99,99.", promotions.ValueError);
            Assert.True(Shows(window, "Rabatt in Prozent zwischen 0,01 und 99,99."));
            Assert.Same(Named<TextBox>(window, "PromotionValueField"), Focused(window));
            Assert.Equal(0, CallsTo(api, "POST /api/promotions"));
            return shell.AdminPage;
        }, api);
    }

    [Fact]
    public Task Aktion_archivieren_und_zurueckholen_ueber_Klick_ohne_Rueckfrage()
    {
        var api = PromotionsApi();
        return Render("hell", "verwaltung-aktionen-archiv", async (window, main) =>
        {
            var (shell, promotions) = await OpenPromotions(window, main);
            promotions.SelectedRow = promotions.Rows.Single(r => r.Id == AutumnId);
            await Pump(Settling);

            await ClickScrolled(window, "ArchiveButton");

            Assert.Null(main.DialogHost.Current);
            Assert.Equal(1, CallsTo(api, $"POST /api/promotions/{AutumnId}/archive"));
            Assert.True(Shows(window, "Archiviert."));
            Assert.DoesNotContain(promotions.Rows, r => r.Id == AutumnId);

            await ClickScrolled(window, "ShowArchivedBox");
            promotions.SelectedRow = promotions.Rows.Single(r => r.Id == WinterId);
            await Pump(Settling);
            Assert.False(Named<TextBox>(window, "PromotionNameField").IsEffectivelyEnabled);
            await ClickScrolled(window, "UnarchiveButton");

            Assert.Equal(1, CallsTo(api, $"POST /api/promotions/{WinterId}/unarchive"));
            Assert.True(Shows(window, "Zurückgeholt."));
            return shell.AdminPage;
        }, api);
    }

    [Fact]
    public Task Aktionen_melden_keine_Bindungsfehler() => Render("hell", "bindungen-aktionen", async (window, main) =>
    {
        await WithBindingLog(async log =>
        {
            var shell = await OpenAdminPage(window, main);
            await AssertNoBindingErrors(log, "Aktionen", () =>
            {
                shell.AdminPage.SelectedArea = shell.AdminPage.Areas.Single(a => a.Title == "Aktionen");
                return Task.CompletedTask;
            });
            var promotions = shell.AdminPage.Promotions!;
            await AssertNoBindingErrors(log, "Aktion", () => { promotions.SelectedRow = promotions.Rows.Single(r => r.Id == SpringId); return Task.CompletedTask; });
            await AssertNoBindingErrors(log, "Eingabe", () =>
            {
                promotions.IsPercent = true;
                promotions.ScopePackages = true;
                promotions.SomeLocations = true;
                promotions.PickedTreatment = promotions.CatalogTreatments.First();
                return Task.CompletedTask;
            });
            await AssertNoBindingErrors(log, "Verworfen", () => { promotions.Discard(); return Task.CompletedTask; });
            await AssertNoBindingErrors(log, "Archiv", () => { promotions.ShowArchived = true; promotions.SelectedRow = promotions.Rows.Single(r => r.Id == WinterId); return Task.CompletedTask; });
            await AssertNoBindingErrors(log, "Neu", async () => await promotions.NewCommand.ExecuteAsync(null));
            await AssertNoBindingErrors(log, "Fehler", async () => await promotions.SaveChangesAsync());
            await AssertNoBindingErrors(log, "Anderer Bereich", () =>
            {
                shell.AdminPage.SelectedArea = shell.AdminPage.Areas.Single(a => a.Title == "Behandlungen");
                return Task.CompletedTask;
            });
            log.AssertClean();
            return shell.AdminPage;
        });
        return true;
    }, PromotionsApi());

    [Fact]
    public Task Jede_Seite_Aktionen_hat_fuer_jedes_bedienbare_Element_einen_lesbaren_Namen() => Render("hell", "namen-aktionen", async (window, main) =>
    {
        var (shell, promotions) = await OpenPromotions(window, main);
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

        Scan("Neue Aktion");
        promotions.SelectedRow = promotions.Rows.Single(r => r.Id == SpringId);
        await Pump(Settling);
        Scan("Frühling");
        promotions.SomeLocations = true;
        promotions.ScopePackages = true;
        await Pump(Settling);
        Scan("Ausgewählte Standorte und Pakete");
        promotions.ShowArchived = true;
        promotions.SelectedRow = promotions.Rows.Single(r => r.Id == WinterId);
        await Pump(Settling);
        Scan("Archiviert");

        Assert.True(problems.Count == 0, $"{problems.Count} Elemente:{Environment.NewLine}{string.Join(Environment.NewLine, problems)}");
        return shell.AdminPage;
    }, PromotionsApi());
}
