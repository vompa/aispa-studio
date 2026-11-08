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

/// <summary>R05 Slice 96 (Ä191, ADR 0084): Kassa, Zahlungen und Erinnerungen als native Masken der Verwaltung, mit echter Eingabe (Klick, Tippen, Strg+S).</summary>
public sealed partial class ViewTests
{
    /// <summary>Die Verwaltung mit Kassa 10 % / 2 %, einem Stripe-Konto mit eigenen Schlüsseln und Erinnerungen 24/2 Stunden; Speichern gibt zurück, was ankam.</summary>
    private static StubApi SettingsApi() => Api()
        .On("GET /api/pos/settings", new PosSettingsDto(10m, true, 2m))
        .OnJson("PUT /api/pos/settings", body =>
        {
            var input = JsonSerializer.Deserialize<PosSettingsInput>(body!, ApiClient.Json)!;
            return new PosSettingsDto(input.DiscountLimitPercent, true, input.CashDiscountPercent ?? 0);
        })
        .On("GET /api/tenant/payments", PaymentsTests.OwnKeys)
        .On("GET /api/reminders/settings", RemindersTests.Day)
        .OnJson("PUT /api/reminders/settings", body => JsonSerializer.Deserialize<ReminderSettingsDto>(body!, ApiClient.Json)!)
        .On("GET /api/retention/settings", new RebookSettingsDto(true, [new RebookIntervalDto(SpaSample.Treatments[0].Id, 42)]))
        .On("GET /api/treatments", SpaSample.Treatments)
        .On("GET /api/retention/reminders", new List<RebookReminderDto>
        {
            new(Guid.NewGuid(), Guid.NewGuid(), "Lena Huber", SpaSample.Treatments[0].Name, "Sent", true, "Liebe Lena, Zeit für die nächste Auszeit.",
                SpaSample.At(9, 0), SpaSample.At(9, 2), null),
        });

    /// <summary>Den Bereich in der offenen Verwaltung anklicken, bis seine Maske geladen ist.</summary>
    private static async Task<object?> ClickArea(Window window, ShellViewModel shell, string title)
    {
        await OpenMask(window, shell, title);
        if (shell.AdminPage.Content is PaymentsViewModel payments)
        {
            payments.PollDelay = TimeSpan.Zero;
        }

        return shell.AdminPage.Content;
    }

    /// <summary>Verwaltung öffnen und den Bereich anklicken, bis seine Maske geladen ist.</summary>
    private static async Task<T> OpenSettingsArea<T>(Window window, MainViewModel main, string title)
    {
        var shell = await OpenAdminPage(window, main);
        return Assert.IsType<T>(await ClickArea(window, shell, title));
    }

    private static Button SaveButton(Window window) =>
        window.GetVisualDescendants().OfType<SaveBar>().Single(b => b.IsEffectivelyVisible).GetVisualDescendants().OfType<Button>()
            .First(b => b.Content as string == "Speichern");

    [Theory]
    [InlineData("hell")]
    [InlineData("dunkel")]
    public Task Verwaltung_Kassa(string theme) => Render(theme, "verwaltung-kassa", async (window, main) =>
    {
        var settings = await OpenSettingsArea<PosSettingsViewModel>(window, main, "Kassa");
        Assert.Equal("10", Named<TextBox>(window, "LimitField").Text);
        return settings;
    }, SettingsApi());

    [Theory]
    [InlineData("hell")]
    [InlineData("dunkel")]
    public Task Verwaltung_Zahlungen(string theme) => Render(theme, "verwaltung-zahlungen", async (window, main) =>
        await OpenSettingsArea<PaymentsViewModel>(window, main, "Zahlungen"), SettingsApi());

    [Theory]
    [InlineData("hell")]
    [InlineData("dunkel")]
    public Task Verwaltung_Erinnerungen(string theme) => Render(theme, "verwaltung-erinnerungen", async (window, main) =>
        await OpenSettingsArea<RemindersViewModel>(window, main, "Erinnerungen"), SettingsApi());

    [Fact]
    public Task Given_hinterlegter_Stripe_Schluessel_When_die_Inhaberin_Zahlungen_oeffnet_Then_steht_nur_Punkte_mit_den_letzten_vier_Zeichen() =>
        Render("hell", "verwaltung-zahlungen-maske", async (window, main) =>
        {
            var payments = await OpenSettingsArea<PaymentsViewModel>(window, main, "Zahlungen");

            var key = Named<TextBox>(window, "SecretKeyField");
            Assert.True(key.IsEffectivelyVisible);
            Assert.Equal("Hinterlegt: ••••wxyz", key.PlaceholderText);
            Assert.True(string.IsNullOrEmpty(key.Text));
            Assert.True(Named<RadioButton>(window, "OwnKeysOption").IsChecked);
            Assert.False(Shows(window, "sk_live"));
            return payments;
        }, SettingsApi());

    [Fact]
    public Task Given_Erinnerung_24_Stunden_When_die_Inhaberin_48_eintippt_und_Strg_S_drueckt_Then_PUT_mit_48()
    {
        var api = SettingsApi();
        return Render("hell", "verwaltung-erinnerungen-gespeichert", async (window, main) =>
        {
            var reminders = await OpenSettingsArea<RemindersViewModel>(window, main, "Erinnerungen");
            Assert.Equal("24", Named<TextBox>(window, "FirstHoursField").Text);

            await TypeInto(window, "FirstHoursField", "48");
            PressSave(window);
            await Pump(Settling);

            var put = Assert.Single(api.Calls, c => c.Route == "PUT /api/reminders/settings");
            Assert.Equal(48, JsonSerializer.Deserialize<ReminderSettingsDto>(put.Body!, ApiClient.Json)!.FirstHoursBefore);
            Assert.DoesNotContain(api.Calls, c => c.Route == "PUT /api/retention/settings");
            Assert.False(reminders.HasUnsavedChanges);
            Assert.True(Shows(window, "Gespeichert."));
            return reminders;
        }, api);
    }

    [Fact]
    public Task Given_Kassa_Einstellungen_When_die_Inhaberin_ohne_Aenderung_Speichern_klickt_Then_kein_PUT()
    {
        var api = SettingsApi();
        return Render("hell", "verwaltung-kassa-unveraendert", async (window, main) =>
        {
            var settings = await OpenSettingsArea<PosSettingsViewModel>(window, main, "Kassa");

            ClickCenter(window, SaveButton(window));
            await Pump(Settling);

            Assert.DoesNotContain(api.Calls, c => c.Route == "PUT /api/pos/settings");
            Assert.True(Shows(window, "Gespeichert."));
            return settings;
        }, api);
    }

    [Fact]
    public Task Einstellungsmasken_haben_lesbare_Namen_erreichen_jedes_Element_mit_Tab_und_melden_keine_Bindungsfehler() =>
        Render("hell", "verwaltung-einstellungen-scan", async (window, main) =>
        {
            var previous = Logger.Sink;
            var log = new BindingLog();
            Logger.Sink = log;
            try
            {
                var problems = new List<string>();
                ShellViewModel? shell = null;
                object? last = null;
                await AssertNoBindingErrors(log, "Verwaltung öffnen", async () => shell = await OpenAdminPage(window, main));
                async Task Scan(string title, Type view)
                {
                    var root = window.GetVisualDescendants().OfType<Control>().Single(c => c.GetType() == view);
                    foreach (var control in root.GetVisualDescendants().OfType<Control>()
                        .Where(c => c is Button or ToggleButton or CheckBox or TextBox or ComboBox or ListBoxItem))
                    {
                        if (control.IsEffectivelyVisible && control.IsEffectivelyEnabled && control.Focusable && HasUnreadableName(control, out var name))
                        {
                            problems.Add($"{title}: {control.GetType().Name}#{control.Name} \"{name}\"");
                        }
                    }

                    problems.AddRange(await TabProblems(window, title));
                }

                foreach (var (title, view) in new (string, Type)[] { ("Kassa", typeof(PosSettingsView)), ("Zahlungen", typeof(PaymentsView)), ("Erinnerungen", typeof(RemindersView)) })
                {
                    await AssertNoBindingErrors(log, $"{title} öffnen", async () => last = await ClickArea(window, shell!, title));
                    await Scan(title, view);
                }

                // Die Vorlage für WhatsApp erscheint nur beim Kanal WhatsApp.
                await AssertNoBindingErrors(log, "Erinnerungen: WhatsApp", async () =>
                {
                    shell!.AdminPage.Reminders!.Channel = RemindersViewModel.Channels.Single(c => c.Value == ReminderChannel.WhatsApp);
                    await Pump(Settling);
                });
                Assert.True(Named<TextBox>(window, "TemplateField").IsEffectivelyVisible);
                await Scan("Erinnerungen, WhatsApp", typeof(RemindersView));
                shell!.AdminPage.Reminders!.Discard();

                await AssertNoBindingErrors(log, "Zahlungen: Mit Stripe verbinden", async () =>
                {
                    await ClickArea(window, shell!, "Zahlungen");
                    ClickCenter(window, Named<RadioButton>(window, "ConnectOption"));
                    await Pump(Settling);
                });
                Assert.True(Named<Button>(window, "ConnectButton").IsEffectivelyVisible);
                await Scan("Zahlungen, Mit Stripe verbinden", typeof(PaymentsView));
                await AssertNoBindingErrors(log, "Zum Kalender", async () =>
                {
                    shell!.SelectedPage = shell.Pages.First(p => p.Page == shell.Calendar);
                    await Pump(Settling);
                });
                log.AssertClean();
                Assert.True(problems.Count == 0, $"{problems.Count} Abweichungen:{Environment.NewLine}{string.Join(Environment.NewLine, problems)}");
                return last!;
            }
            finally
            {
                Logger.Sink = previous;
            }
        }, SettingsApi());
}
