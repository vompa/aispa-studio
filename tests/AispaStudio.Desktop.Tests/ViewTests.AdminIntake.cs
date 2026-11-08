using System.Text.Json;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;
using AispaStudio.Desktop.ViewModels;
using AispaStudio.Desktop.Views;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>R05 Slice 97 (Ä191): native Maske „Postfach &amp; Nummern“ im Fenster: Eingabe per Tastatur, Strg+S, Absenderregel per Klick, maskiertes Passwort, Scans.</summary>
public sealed partial class ViewTests
{
    private static readonly Guid TreatwellRuleId = Guid.Parse("00000000-0000-0000-0000-0000000c9701");

    private static readonly SpaMailboxDto IntakeMailbox = new(
        MailboxKind.Imap, "anfragen@day-spa-wien.at", true, "imap.hoster.example", 993, MailboxSecurity.SslOnConnect, "anfragen@day-spa-wien.at", "INBOX", null,
        null, null, true, SpaSample.At(10), null, false, null, 587, MailboxSecurity.StartTls, null, true, null);

    private static StubApi IntakeApi(StubApi? api = null) => (api ?? Api())
        .On("GET /api/intake/mailbox", IntakeMailbox)
        .On("GET /api/intake/routes", new List<IntakeRouteDto> { new(Guid.NewGuid(), BookingChannel.Sms, "+4366412345678", null), new(Guid.NewGuid(), BookingChannel.Email, "anfragen@day-spa-wien.at", null) })
        .On("GET /api/intake/sender-rules", new List<SenderRuleDto> { new(TreatwellRuleId, "treatwell.at", SenderPlatform.Treatwell, SenderHandling.Automatic, true, "Vorbelegt") })
        .OnJson("PUT /api/intake/mailbox", body =>
        {
            var i = JsonSerializer.Deserialize<SpaMailboxInput>(body!, ApiClient.Json)!;
            return new SpaMailboxDto(i.Kind, i.Address, i.Enabled, i.Host, i.Port, i.Security, i.UserName, i.Folder ?? "INBOX", i.ProcessedFolder, i.DirectoryTenantId, i.ClientId, true,
                SpaSample.At(10), null, i.SendReplies, i.SmtpHost, i.SmtpPort, i.SmtpSecurity, null, i.SaveSentCopy, i.SentFolder);
        })
        .OnJson("POST /api/intake/sender-rules", body =>
        {
            var i = JsonSerializer.Deserialize<SenderRuleInput>(body!, ApiClient.Json)!;
            return new SenderRuleDto(Guid.NewGuid(), i.Pattern, i.Platform, i.Handling, i.Active, i.Note);
        }, System.Net.HttpStatusCode.Created)
        .On($"DELETE /api/intake/sender-rules/{TreatwellRuleId}", null, System.Net.HttpStatusCode.NoContent);

    private static async Task<ShellViewModel> OpenIntake(Window window, MainViewModel main)
    {
        var shell = await OpenAdminPage(window, main);
        shell.AdminPage.SelectedArea = shell.AdminPage.Areas.Single(a => a.Title == "Postfach & Nummern");
        await Pump(Settling);
        await Pump(Settling);
        Assert.Same(shell.AdminPage.Intake, shell.AdminPage.Content);
        Assert.True(shell.AdminPage.Intake!.IsLoaded);
        return shell;
    }

    private static Task<bool> RunIntake(Func<Window, MainViewModel, ShellViewModel, StubApi, Task> test)
    {
        var api = IntakeApi();
        return Render("hell", "verwaltung-postfach-eingabe", async (window, main) =>
        {
            var shell = await OpenIntake(window, main);
            await test(window, main, shell, api);
            return shell.AdminPage;
        }, api);
    }

    [Theory]
    [InlineData("hell")]
    [InlineData("dunkel")]
    public Task Verwaltung_Postfach(string theme) => Render(theme, "verwaltung-postfach", async (window, main) =>
    {
        var shell = await OpenIntake(window, main);
        Assert.Equal("anfragen@day-spa-wien.at", Named<TextBox>(window, "AddressField").Text);
        Assert.Equal("imap.hoster.example", Named<TextBox>(window, "HostField").Text);
        Assert.True(Shows(window, "Zuletzt abgerufen"));
        Assert.True(Shows(window, "SMS: +4366412345678"));
        Assert.True(Shows(window, "treatwell.at"));
        return shell.AdminPage;
    }, IntakeApi());

    [Fact]
    public Task Postfach_aendern_und_Strg_S_speichert() => RunIntake(async (window, main, shell, api) =>
    {
        var intake = shell.AdminPage.Intake!;
        var secret = Named<TextBox>(window, "SecretField");
        Assert.Equal('●', secret.PasswordChar);
        Assert.Equal("gespeichert, leer lassen zum Behalten", secret.PlaceholderText);

        await TypeInto(window, "AddressField", "buchung@day-spa-wien.at");
        await TypeInto(window, "SecretField", "geheim-123");
        Assert.Equal('●', secret.PasswordChar);
        Assert.True(intake.HasUnsavedChanges);
        PressSave(window);
        await Pump(Settling);

        var put = JsonSerializer.Deserialize<SpaMailboxInput>(Assert.Single(api.Calls, c => c.Route == "PUT /api/intake/mailbox").Body!, ApiClient.Json)!;
        Assert.Equal(("buchung@day-spa-wien.at", "geheim-123"), (put.Address, put.Secret));
        Assert.False(intake.HasUnsavedChanges);
        Assert.Equal("", secret.Text);
        Assert.True(Shows(window, "Gespeichert."));
    });

    [Fact]
    public Task Absenderregel_anlegen_ueber_Klick() => RunIntake(async (window, main, shell, api) =>
    {
        await TypeInto(window, "RulePatternField", "newsletter@plattform.example");
        await TypeInto(window, "RuleNoteField", "Werbung");
        await ClickScrolled(window, "AddRuleButton");

        var post = JsonSerializer.Deserialize<SenderRuleInput>(Assert.Single(api.Calls, c => c.Route == "POST /api/intake/sender-rules").Body!, ApiClient.Json)!;
        Assert.Equal(new SenderRuleInput("newsletter@plattform.example", SenderPlatform.Treatwell, SenderHandling.Automatic, true, "Werbung"), post);
        Assert.Equal(["newsletter@plattform.example", "treatwell.at"], shell.AdminPage.Intake!.SenderRules.Rules.Select(r => r.Pattern).Order(StringComparer.Ordinal));
        Assert.True(Shows(window, "Regel für newsletter@plattform.example angelegt."));
        Assert.Equal("", Named<TextBox>(window, "RulePatternField").Text);
    });

    [Fact]
    public Task Absenderregel_loeschen_fragt_nach_und_sendet_DELETE() => RunIntake(async (window, main, shell, api) =>
    {
        await ClickScrolled(window, "DeleteRuleButton");
        Assert.Equal("Regel löschen?", Assert.IsType<ConfirmDialogViewModel>(main.DialogHost.Current).Title);
        Assert.Equal(0, CallsTo(api, $"DELETE /api/intake/sender-rules/{TreatwellRuleId}"));
        ClickCenter(window, DialogButton(window, "ConfirmButton"));
        await Pump(Settling);

        Assert.Equal(1, CallsTo(api, $"DELETE /api/intake/sender-rules/{TreatwellRuleId}"));
        Assert.Empty(shell.AdminPage.Intake!.SenderRules.Rules);
    });

    [Fact]
    public Task Rezeptionistin_sieht_kein_Postfach_keine_KI_und_kein_Handbuch() => Render("hell", "verwaltung-rezeption-postfach", async (window, main) =>
    {
        var shell = await OpenAdminPage(window, main);
        Assert.Null(shell.AdminPage.Intake);
        Assert.Null(shell.AdminPage.Ai);
        Assert.Null(shell.AdminPage.Handbook);
        Assert.False(window.GetVisualDescendants().OfType<IntakeView>().Any());
        return shell.AdminPage;
    }, ReceptionApi());

    [Fact]
    public Task Postfach_und_Absenderregeln_melden_keine_Bindungsfehler() => Render("hell", "bindungen-postfach", async (window, main) =>
    {
        await WithBindingLog(async log =>
        {
            var shell = await OpenAdminPage(window, main);
            await AssertNoBindingErrors(log, "Postfach", () =>
            {
                shell.AdminPage.SelectedArea = shell.AdminPage.Areas.Single(a => a.Title == "Postfach & Nummern");
                return Task.CompletedTask;
            });
            var intake = shell.AdminPage.Intake!;
            await AssertNoBindingErrors(log, "Eingabe", () => { intake.Address = "x@y.example"; intake.Secret = "x"; intake.SendReplies = true; return Task.CompletedTask; });
            await AssertNoBindingErrors(log, "Ohne Kopie", () => { intake.SaveSentCopy = false; return Task.CompletedTask; });
            await AssertNoBindingErrors(log, "Microsoft 365", () => { intake.Kind = IntakeViewModel.MailboxKinds[1]; return Task.CompletedTask; });
            await AssertNoBindingErrors(log, "Verworfen", () => { intake.Discard(); return Task.CompletedTask; });
            await AssertNoBindingErrors(log, "Regel", () => { intake.SenderRules.NewPattern = "x.example"; intake.SenderRules.Rules[0].Active = false; return Task.CompletedTask; });
            await AssertNoBindingErrors(log, "Anderer Bereich", () =>
            {
                shell.AdminPage.SelectedArea = shell.AdminPage.Areas.Single(a => a.Title == "Behandlungen");
                return Task.CompletedTask;
            });
            log.AssertClean();
            return shell.AdminPage;
        });
        return true;
    }, IntakeApi());

    [Fact]
    public Task Jede_Seite_Postfach_hat_fuer_jedes_bedienbare_Element_einen_lesbaren_Namen() => RunIntake(async (window, main, shell, api) =>
    {
        var intake = shell.AdminPage.Intake!;
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

        Scan("IMAP");
        intake.SendReplies = true;
        await Pump(Settling);
        Scan("Antworten aus dem Postfach");
        intake.Kind = IntakeViewModel.MailboxKinds[1];
        await Pump(Settling);
        Scan("Microsoft 365");

        Assert.True(problems.Count == 0, $"{problems.Count} Elemente:{Environment.NewLine}{string.Join(Environment.NewLine, problems)}");
    });
}
