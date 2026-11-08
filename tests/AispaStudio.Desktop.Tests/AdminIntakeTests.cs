using System.Net;
using System.Text.Json;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;
using AispaStudio.Desktop.ViewModels;

namespace AispaStudio.Desktop.Tests;

/// <summary>R05 Slice 97 (Ä191): native Maske „Postfach &amp; Nummern“ nach IntakeTab.tsx und SenderRulesSection.tsx: Postfach-Formular, Prüfen, Entfernen, Nummern und Absenderregeln.</summary>
public sealed class AdminIntakeTests
{
    private const string MailboxRoute = "/api/intake/mailbox";
    private const string RulesRoute = "/api/intake/sender-rules";

    private static readonly string[] HostMissing = ["Server fehlt."];
    private static readonly string[] PatternInvalid = ["Keine gültige Adresse oder Domain."];

    private static readonly TimeZoneInfo Vienna = TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna");
    private static readonly Guid TreatwellRuleId = Guid.NewGuid();
    private static readonly Guid NewsletterRuleId = Guid.NewGuid();

    private static readonly SpaMailboxDto Mailbox = new(
        MailboxKind.Imap, "anfragen@day-spa-wien.at", true, "imap.hoster.example", 993, MailboxSecurity.SslOnConnect, "anfragen@day-spa-wien.at", "INBOX", "Verarbeitet",
        null, null, true, new DateTimeOffset(2026, 10, 30, 10, 40, 0, TimeSpan.Zero), null, false, null, 587, MailboxSecurity.StartTls, null, true, null);

    private static readonly List<IntakeRouteDto> Routes =
    [
        new(Guid.NewGuid(), BookingChannel.Sms, "+4366412345678", null),
        new(Guid.NewGuid(), BookingChannel.WhatsApp, "+4366412345679", "1234"),
        new(Guid.NewGuid(), BookingChannel.Phone, "+4312345678", null),
        new(Guid.NewGuid(), BookingChannel.Email, "anfragen@day-spa-wien.at", null),
    ];

    private static readonly SenderRuleDto TreatwellRule = new(TreatwellRuleId, "treatwell.at", SenderPlatform.Treatwell, SenderHandling.Automatic, true, "Vorbelegt");
    private static readonly SenderRuleDto NewsletterRule = new(NewsletterRuleId, "newsletter@plattform.example", SenderPlatform.Other, SenderHandling.Ignore, true, null);

    private static StubApi Api() => Api(Mailbox);

    private static StubApi Api(SpaMailboxDto? mailbox, params SenderRuleDto[] rules)
    {
        var api = new StubApi()
            .On($"GET {MailboxRoute}", mailbox, mailbox is null ? HttpStatusCode.NoContent : HttpStatusCode.OK)
            .On("GET /api/intake/routes", Routes)
            .On($"GET {RulesRoute}", rules.ToList())
            .OnJson($"PUT {MailboxRoute}", body => Stored(JsonSerializer.Deserialize<SpaMailboxInput>(body!, ApiClient.Json)!))
            .On($"DELETE {MailboxRoute}", null, HttpStatusCode.NoContent);
        return api;
    }

    /// <summary>Was die API nach dem Speichern zurückgibt: die Eingabe ohne Passwort, mit Hinweis, dass eines gespeichert ist.</summary>
    private static SpaMailboxDto Stored(SpaMailboxInput i) => new(
        i.Kind, i.Address, i.Enabled, i.Host, i.Port, i.Security, i.UserName, i.Folder ?? "INBOX", i.ProcessedFolder, i.DirectoryTenantId, i.ClientId, true, null, null,
        i.SendReplies, i.SmtpHost, i.SmtpPort, i.SmtpSecurity, null, i.SaveSentCopy, i.SentFolder);

    private static async Task<IntakeViewModel> Open(StubApi api, FakeDialogs? dialogs = null, TimeSpan? interval = null)
    {
        var intake = new IntakeViewModel(api.Client(), Vienna, interval ?? TimeSpan.FromMilliseconds(1)) { Dialogs = dialogs };
        await intake.EnsureLoadedAsync();
        await intake.SenderRules.IdleAsync();
        return intake;
    }

    private static T Sent<T>(StubApi api, string route) => JsonSerializer.Deserialize<T>(api.Calls.Single(c => c.Route == route).Body!, ApiClient.Json)!;

    private static int CallsTo(StubApi api, string route) => api.Calls.Count(c => c.Route == route);

    [Fact]
    public async Task Given_ein_Postfach_When_die_Maske_laedt_Then_stehen_Felder_Stand_und_Nummern_da()
    {
        var intake = await Open(Api(Mailbox, TreatwellRule));

        Assert.True(intake.IsLoaded);
        Assert.True(intake.HasMailbox);
        Assert.Equal(("anfragen@day-spa-wien.at", "imap.hoster.example", (decimal?)993m, "anfragen@day-spa-wien.at", "INBOX"), (intake.Address, intake.Host, intake.Port, intake.UserName, intake.Folder));
        Assert.Equal(MailboxKind.Imap, intake.Kind.Value);
        Assert.Equal(MailboxSecurity.SslOnConnect, intake.Security.Value);
        Assert.Equal("Zuletzt abgerufen 30.10.2026, 11:40.", intake.StatusLine);
        Assert.False(intake.StatusIsWarning);
        Assert.Equal("gespeichert, leer lassen zum Behalten", intake.SecretPlaceholder);
        Assert.Equal("Passwort", intake.SecretLabel);
        Assert.Equal(["SMS: +4366412345678", "WhatsApp: +4366412345679", "Telefon: +4312345678", "Mail: anfragen@day-spa-wien.at"], intake.Routes);
        Assert.False(intake.HasUnsavedChanges);
        Assert.Equal(["treatwell.at"], intake.SenderRules.Rules.Select(r => r.Pattern));
    }

    [Fact]
    public async Task Given_kein_Postfach_When_die_Maske_laedt_Then_ist_das_Formular_leer_und_Pruefen_und_Entfernen_fehlen()
    {
        var intake = await Open(Api(null));

        Assert.False(intake.HasMailbox);
        Assert.False(intake.CanCheck);
        Assert.Equal("", intake.Address);
        Assert.Equal(993m, intake.Port);
        Assert.Equal("", intake.SecretPlaceholder);
        Assert.Null(intake.StatusLine);
        Assert.False(intake.HasUnsavedChanges);
    }

    [Fact]
    public async Task Given_ein_Postfach_mit_Abruffehler_When_die_Maske_laedt_Then_steht_der_Fehler_als_Warnung_da()
    {
        var intake = await Open(Api(Mailbox with { LastError = "Anmeldung abgelehnt" }));

        Assert.Equal("Letzter Abruf fehlgeschlagen: Anmeldung abgelehnt", intake.StatusLine);
        Assert.True(intake.StatusIsWarning);
    }

    [Theory]
    [InlineData("Gesendet, aber die Kopie unter „Gesendet“ fehlt.", "Gesendet, aber die Kopie unter „Gesendet“ fehlt.")]
    [InlineData("Senden klappt, die Ablage nicht.", "Senden klappt, die Ablage nicht.")]
    [InlineData("535 Authentication failed", "Letzter Versand fehlgeschlagen: 535 Authentication failed")]
    public async Task Given_ein_Versandfehler_When_die_Maske_laedt_Then_nennt_sie_ihn_wie_der_Server_ihn_sagt(string serverText, string expected)
    {
        var intake = await Open(Api(Mailbox with { SendReplies = true, LastSendError = serverText }));

        Assert.Equal(expected, intake.SendNotice);
    }

    [Fact]
    public async Task Given_ein_Postfach_When_gespeichert_Then_PUT_ohne_Passwort_wenn_leer()
    {
        var api = Api();
        var intake = await Open(api);

        intake.Address = "buchung@day-spa-wien.at";
        Assert.True(intake.HasUnsavedChanges);
        Assert.True(await intake.SaveChangesAsync());

        var put = Sent<SpaMailboxInput>(api, $"PUT {MailboxRoute}");
        Assert.Null(put.Secret);
        Assert.Equal(("buchung@day-spa-wien.at", "imap.hoster.example", 993, MailboxSecurity.SslOnConnect, "anfragen@day-spa-wien.at", MailboxKind.Imap, true),
            (put.Address, put.Host, put.Port, put.Security, put.UserName, put.Kind, put.Enabled));
        Assert.Equal("Verarbeitet", put.ProcessedFolder);
        Assert.Equal("Gespeichert. Das Postfach wird beim nächsten Abruf gelesen.", intake.StatusText);
        Assert.False(intake.HasUnsavedChanges);
    }

    [Fact]
    public async Task Given_Passwort_eingetragen_Then_PUT_mit_Secret_und_danach_ist_das_Feld_leer()
    {
        var api = Api();
        var intake = await Open(api);

        intake.Secret = "geheim-123";
        Assert.True(intake.HasUnsavedChanges);
        Assert.True(await intake.SaveChangesAsync());

        Assert.Equal("geheim-123", Sent<SpaMailboxInput>(api, $"PUT {MailboxRoute}").Secret);
        Assert.Equal("", intake.Secret);
        Assert.False(intake.HasUnsavedChanges);
    }

    [Fact]
    public async Task Given_kein_Postfach_When_ein_IMAP_Postfach_angelegt_wird_Then_PUT_mit_allen_Feldern()
    {
        var api = Api(null);
        var intake = await Open(api);

        intake.Address = "anfragen@day-spa-wien.at";
        intake.Host = "imap.hoster.example";
        intake.Port = 143m;
        intake.Security = IntakeViewModel.SecurityOptions[1];
        intake.UserName = "anfragen";
        intake.Secret = "geheim";
        Assert.True(await intake.SaveChangesAsync());

        var put = Sent<SpaMailboxInput>(api, $"PUT {MailboxRoute}");
        Assert.Equal(new SpaMailboxInput(MailboxKind.Imap, "anfragen@day-spa-wien.at", true, "imap.hoster.example", 143, MailboxSecurity.StartTls, "anfragen", null, null, null, null,
            "geheim"), put);
        Assert.True(intake.HasMailbox);
        Assert.True(intake.CanCheck);
    }

    [Fact]
    public async Task Given_Microsoft_365_When_gespeichert_Then_PUT_mit_Verzeichnis_Anwendung_und_Client_Geheimnis()
    {
        var api = Api(null);
        var intake = await Open(api);

        intake.Kind = IntakeViewModel.MailboxKinds[1];
        Assert.Equal("Client-Geheimnis", intake.SecretLabel);
        Assert.True(intake.IsGraph);
        intake.Address = "anfragen@day-spa-wien.at";
        intake.DirectoryTenantId = "11111111-2222-3333-4444-555555555555";
        intake.ClientId = "66666666-7777-8888-9999-000000000000";
        intake.Secret = "client-geheimnis";
        Assert.True(await intake.SaveChangesAsync());

        var put = Sent<SpaMailboxInput>(api, $"PUT {MailboxRoute}");
        Assert.Equal((MailboxKind.Graph, "11111111-2222-3333-4444-555555555555", "66666666-7777-8888-9999-000000000000", "client-geheimnis"),
            (put.Kind, put.DirectoryTenantId, put.ClientId, put.Secret));
    }

    [Fact]
    public async Task Given_ein_gespeichertes_Passwort_When_die_Art_gewechselt_wird_Then_gilt_es_nicht_mehr_und_ein_eingetipptes_wird_verworfen()
    {
        var intake = await Open(Api());
        intake.Secret = "eingetippt";

        intake.Kind = IntakeViewModel.MailboxKinds[1];

        Assert.Equal("", intake.Secret);
        Assert.Equal("", intake.SecretPlaceholder);
        intake.Kind = IntakeViewModel.MailboxKinds[0];
        Assert.Equal("gespeichert, leer lassen zum Behalten", intake.SecretPlaceholder);
    }

    [Fact]
    public async Task Given_Antworten_aus_dem_Postfach_When_eingeschaltet_Then_erscheinen_SMTP_und_Ablage_nur_bei_IMAP()
    {
        var api = Api();
        var intake = await Open(api);
        Assert.False(intake.ShowSmtp);

        intake.SendReplies = true;
        Assert.True(intake.ShowSmtp);
        Assert.True(intake.ShowSentFolder);
        intake.SmtpHost = "smtp.hoster.example";
        intake.SmtpPort = 465m;
        intake.SmtpSecurity = IntakeViewModel.SecurityOptions[0];
        intake.SentFolder = "Gesendet";
        Assert.True(await intake.SaveChangesAsync());

        var put = Sent<SpaMailboxInput>(api, $"PUT {MailboxRoute}");
        Assert.Equal((true, "smtp.hoster.example", 465, MailboxSecurity.SslOnConnect, true, "Gesendet"), (put.SendReplies, put.SmtpHost, put.SmtpPort, put.SmtpSecurity, put.SaveSentCopy, put.SentFolder));

        intake.SaveSentCopy = false;
        Assert.False(intake.ShowSentFolder);
        intake.Kind = IntakeViewModel.MailboxKinds[1];
        Assert.False(intake.ShowSmtp);
    }

    [Fact]
    public async Task Given_die_API_lehnt_das_Postfach_ab_When_gespeichert_Then_steht_der_Fehler_da_und_die_Eingabe_bleibt()
    {
        var api = Api().On($"PUT {MailboxRoute}", new { errors = new { host = HostMissing } }, HttpStatusCode.BadRequest);
        var intake = await Open(api);
        intake.Host = "";

        Assert.False(await intake.SaveChangesAsync());

        Assert.Equal("Server fehlt.", intake.Error);
        Assert.True(intake.HasUnsavedChanges);
        Assert.Equal("", intake.Host);
    }

    [Fact]
    public async Task Given_geaenderte_Felder_When_verworfen_Then_steht_der_gespeicherte_Stand_wieder_da()
    {
        var intake = await Open(Api());
        intake.Host = "anders.example";
        intake.Secret = "x";
        intake.SendReplies = true;

        intake.Discard();

        Assert.Equal(("imap.hoster.example", "", false), (intake.Host, intake.Secret, intake.SendReplies));
        Assert.False(intake.HasUnsavedChanges);
    }

    [Fact]
    public async Task Given_ein_gespeichertes_Postfach_When_Jetzt_pruefen_Then_POST_check_und_Rueckmeldung_nach_neuem_Stand()
    {
        object current = Mailbox;
        var api = Api().OnJson($"GET {MailboxRoute}", _ => current)
            .OnJson($"POST {MailboxRoute}/check", _ =>
            {
                current = Mailbox with { LastCheckedAt = new DateTimeOffset(2026, 10, 30, 10, 45, 0, TimeSpan.Zero) };
                return new { };
            }, HttpStatusCode.Accepted);
        var intake = await Open(api);

        await intake.CheckCommand.ExecuteAsync(null);

        Assert.Equal(1, CallsTo(api, $"POST {MailboxRoute}/check"));
        Assert.Equal("Geprüft: das Postfach antwortet.", intake.Message);
        Assert.Equal("Zuletzt abgerufen 30.10.2026, 11:45.", intake.StatusLine);
        Assert.False(intake.IsChecking);
    }

    [Fact]
    public async Task Given_der_Abruf_scheitert_When_Jetzt_pruefen_Then_sagt_die_Rueckmeldung_siehe_Fehler_oben()
    {
        object current = Mailbox;
        var api = Api().OnJson($"GET {MailboxRoute}", _ => current)
            .OnJson($"POST {MailboxRoute}/check", _ =>
            {
                current = Mailbox with { LastCheckedAt = new DateTimeOffset(2026, 10, 30, 10, 45, 0, TimeSpan.Zero), LastError = "Zeitüberschreitung" };
                return new { };
            }, HttpStatusCode.Accepted);
        var intake = await Open(api);

        await intake.CheckCommand.ExecuteAsync(null);

        Assert.Equal("Geprüft, siehe Fehler oben.", intake.Message);
        Assert.Equal("Letzter Abruf fehlgeschlagen: Zeitüberschreitung", intake.StatusLine);
    }

    [Fact]
    public async Task Given_ausgeschaltetes_Postfach_When_Jetzt_pruefen_Then_sagt_die_Maske_dass_Pruefen_ein_eingeschaltetes_braucht()
    {
        var api = Api(Mailbox with { Enabled = false }).On($"POST {MailboxRoute}/check", null, HttpStatusCode.NotFound);
        var intake = await Open(api);

        Assert.False(intake.CanCheck);
        intake.Enabled = true;
        await intake.CheckCommand.ExecuteAsync(null);

        Assert.Equal("Prüfen geht nur mit einem eingeschalteten Postfach.", intake.Message);
    }

    [Fact]
    public async Task Given_der_Server_antwortet_nicht_When_Jetzt_pruefen_Then_gibt_die_Maske_nach_20_Nachfragen_auf()
    {
        var api = Api().On($"POST {MailboxRoute}/check", new { }, HttpStatusCode.Accepted);
        var intake = await Open(api);

        await intake.CheckCommand.ExecuteAsync(null);

        Assert.Equal("Noch kein Ergebnis. Der Stand oben aktualisiert sich beim nächsten Öffnen.", intake.Message);
        Assert.Equal(21, CallsTo(api, $"GET {MailboxRoute}"));
    }

    [Fact]
    public async Task Given_ein_Postfach_When_Entfernen_bestaetigt_Then_DELETE_und_leeres_Formular()
    {
        var api = Api();
        var dialogs = new FakeDialogs().Answer(ConfirmChoice.Confirmed);
        var intake = await Open(api, dialogs);

        await intake.RemoveCommand.ExecuteAsync(null);

        Assert.Equal(1, CallsTo(api, $"DELETE {MailboxRoute}"));
        Assert.False(intake.HasMailbox);
        Assert.Equal(("", ""), (intake.Address, intake.Host));
        Assert.Equal("Postfach entfernt.", intake.Message);
        Assert.Equal("Postfach entfernen?", dialogs.AssertRules().Asked.Single().Title);
    }

    [Fact]
    public async Task Given_ein_Postfach_When_Entfernen_gefragt_Then_nennt_die_Rueckfrage_Passwort_bei_IMAP_und_Client_Geheimnis_bei_Microsoft_365()
    {
        var imapDialogs = new FakeDialogs().Answer(ConfirmChoice.Cancelled);
        var imap = await Open(Api(), imapDialogs);
        await imap.RemoveCommand.ExecuteAsync(null);

        var graphDialogs = new FakeDialogs().Answer(ConfirmChoice.Cancelled);
        var graph = await Open(Api(Mailbox with { Kind = MailboxKind.Graph }), graphDialogs);
        await graph.RemoveCommand.ExecuteAsync(null);

        Assert.Contains("das gespeicherte Passwort wird gelöscht", imapDialogs.AssertRules().Asked.Single().Message, StringComparison.Ordinal);
        Assert.Contains("das gespeicherte Client-Geheimnis wird gelöscht", graphDialogs.AssertRules().Asked.Single().Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Given_ein_Postfach_When_Entfernen_abgebrochen_Then_bleibt_alles_wie_es_ist()
    {
        var api = Api();
        var intake = await Open(api, new FakeDialogs().Answer(ConfirmChoice.Cancelled));

        await intake.RemoveCommand.ExecuteAsync(null);

        Assert.Equal(0, CallsTo(api, $"DELETE {MailboxRoute}"));
        Assert.True(intake.HasMailbox);
        Assert.Equal("anfragen@day-spa-wien.at", intake.Address);
    }

    [Fact]
    public async Task Given_ungespeichertes_Postfach_When_der_Bereich_verlassen_wird_Then_fragt_die_Maske()
    {
        var dialogs = new FakeDialogs().Answer(ConfirmChoice.Alternative);
        var shell = new ShellViewModel(Api().Client(), TestData.Owner(), null, TimeProvider.System, new ThemeController(new MemorySettingsStore()), "Test", () => Task.CompletedTask);
        shell.AdminPage.Dialogs = dialogs;
        shell.AdminPage.SelectedArea = shell.AdminPage.Areas.Single(a => a.Title == "Postfach & Nummern");
        await shell.AdminPage.InitializeAsync();
        var intake = shell.AdminPage.Intake!;
        Assert.Same(intake, shell.AdminPage.Content);
        await intake.EnsureLoadedAsync();
        intake.Address = "neu@day-spa-wien.at";

        shell.AdminPage.SelectedArea = shell.AdminPage.Areas.Single(a => a.Title == "KI");
        await Task.Yield();

        var asked = Assert.Single(dialogs.AssertRules().Asked);
        Assert.Equal("Änderungen speichern?", asked.Title);
        Assert.Contains("Postfach & Nummern", asked.Message);
        Assert.Same(shell.AdminPage.Ai, shell.AdminPage.Content);
        Assert.Equal("anfragen@day-spa-wien.at", intake.Address);
    }

    [Fact]
    public void Given_Rezeptionistin_Then_fehlen_Postfach_KI_und_Handbuch()
    {
        var shell = new ShellViewModel(Api().Client(), TestData.Reception(), null, TimeProvider.System, new ThemeController(new MemorySettingsStore()), "Test", () => Task.CompletedTask);

        Assert.DoesNotContain(shell.AdminPage.Areas, a => a.Title is "Postfach & Nummern" or "KI" or "Handbuch");
        Assert.Null(shell.AdminPage.Intake);
        Assert.Null(shell.AdminPage.Ai);
        Assert.Null(shell.AdminPage.Handbook);
    }

    [Fact]
    public void Given_Standortleitung_Then_hat_sie_das_Handbuch_aber_weder_Postfach_noch_KI()
    {
        var shell = new ShellViewModel(Api().Client(), TestData.Manager(), null, TimeProvider.System, new ThemeController(new MemorySettingsStore()), "Test", () => Task.CompletedTask);

        Assert.NotNull(shell.AdminPage.Handbook);
        Assert.Null(shell.AdminPage.Intake);
        Assert.Null(shell.AdminPage.Ai);
    }

    // ---------- Absenderregeln ----------

    [Fact]
    public async Task Given_eine_Absenderregel_newsletter_When_sie_angelegt_wird_Then_POST_mit_Muster_Plattform_und_Verarbeitung()
    {
        var api = Api(Mailbox, TreatwellRule).OnJson($"POST {RulesRoute}", body =>
        {
            var input = JsonSerializer.Deserialize<SenderRuleInput>(body!, ApiClient.Json)!;
            return new SenderRuleDto(NewsletterRuleId, input.Pattern, input.Platform, input.Handling, input.Active, input.Note);
        }, HttpStatusCode.Created);
        var intake = await Open(api);
        var rules = intake.SenderRules;
        Assert.False(rules.AddCommand.CanExecute(null));

        rules.NewPattern = "  newsletter@plattform.example ";
        rules.NewPlatform = SenderRulesViewModel.Platforms[1];
        rules.NewHandling = SenderRulesViewModel.Handlings[2];
        Assert.True(rules.AddCommand.CanExecute(null));
        await rules.AddCommand.ExecuteAsync(null);

        Assert.Equal(new SenderRuleInput("newsletter@plattform.example", SenderPlatform.Other, SenderHandling.Ignore, true, null), Sent<SenderRuleInput>(api, $"POST {RulesRoute}"));
        Assert.Equal("Regel für newsletter@plattform.example angelegt.", rules.Message);
        Assert.Equal(["newsletter@plattform.example", "treatwell.at"], rules.Rules.Select(r => r.Pattern));
        Assert.Equal(("", ""), (rules.NewPattern, rules.NewNote));
        Assert.False(intake.HasUnsavedChanges);
    }

    [Fact]
    public async Task Given_eine_Notiz_When_die_Regel_angelegt_wird_Then_geht_sie_getrimmt_mit()
    {
        var api = Api(Mailbox).OnJson($"POST {RulesRoute}", body =>
        {
            var input = JsonSerializer.Deserialize<SenderRuleInput>(body!, ApiClient.Json)!;
            return new SenderRuleDto(NewsletterRuleId, input.Pattern, input.Platform, input.Handling, input.Active, input.Note);
        }, HttpStatusCode.Created);
        var rules = (await Open(api)).SenderRules;

        rules.NewPattern = "plattform.example";
        rules.NewNote = "  Werbung ";
        await rules.AddCommand.ExecuteAsync(null);

        Assert.Equal("Werbung", Sent<SenderRuleInput>(api, $"POST {RulesRoute}").Note);
        Assert.False(rules.ShowEmpty);
    }

    [Fact]
    public async Task Given_die_API_lehnt_die_Regel_ab_When_sie_angelegt_wird_Then_steht_der_Fehler_da_und_das_Muster_bleibt()
    {
        var api = Api(Mailbox).On($"POST {RulesRoute}", new { errors = new { pattern = PatternInvalid } }, HttpStatusCode.BadRequest);
        var rules = (await Open(api)).SenderRules;
        rules.NewPattern = "kein muster";

        await rules.AddCommand.ExecuteAsync(null);

        Assert.Equal("Keine gültige Adresse oder Domain.", rules.Message);
        Assert.Equal("kein muster", rules.NewPattern);
        Assert.Empty(rules.Rules);
        Assert.True(rules.ShowEmpty);
    }

    [Fact]
    public async Task Given_eine_Regel_When_die_Verarbeitung_umgestellt_wird_Then_PUT_der_Zeile()
    {
        var api = Api(Mailbox, TreatwellRule).OnJson($"PUT {RulesRoute}/{TreatwellRuleId}", body =>
        {
            var input = JsonSerializer.Deserialize<SenderRuleInput>(body!, ApiClient.Json)!;
            return new SenderRuleDto(TreatwellRuleId, input.Pattern, input.Platform, input.Handling, input.Active, input.Note);
        });
        var intake = await Open(api);
        var row = intake.SenderRules.Rules.Single();

        row.Handling = SenderRulesViewModel.Handlings[1];
        await intake.SenderRules.IdleAsync();

        Assert.Equal(new SenderRuleInput("treatwell.at", SenderPlatform.Treatwell, SenderHandling.Suggest, true, "Vorbelegt"), Sent<SenderRuleInput>(api, $"PUT {RulesRoute}/{TreatwellRuleId}"));
        Assert.Equal(SenderHandling.Suggest, row.Handling.Value);
        Assert.False(intake.HasUnsavedChanges);
    }

    [Fact]
    public async Task Given_eine_Regel_When_Plattform_und_Aktiv_geaendert_werden_Then_je_ein_PUT_mit_dem_neuen_Wert()
    {
        var api = Api(Mailbox, TreatwellRule).OnJson($"PUT {RulesRoute}/{TreatwellRuleId}", body =>
        {
            var input = JsonSerializer.Deserialize<SenderRuleInput>(body!, ApiClient.Json)!;
            return new SenderRuleDto(TreatwellRuleId, input.Pattern, input.Platform, input.Handling, input.Active, input.Note);
        });
        var rules = (await Open(api)).SenderRules;
        var row = rules.Rules.Single();

        row.Platform = SenderRulesViewModel.Platforms[1];
        await rules.IdleAsync();
        row.Active = false;
        await rules.IdleAsync();

        var bodies = api.Calls.Where(c => c.Route == $"PUT {RulesRoute}/{TreatwellRuleId}").Select(c => JsonSerializer.Deserialize<SenderRuleInput>(c.Body!, ApiClient.Json)!).ToList();
        Assert.Equal([(SenderPlatform.Other, true), (SenderPlatform.Other, false)], bodies.Select(b => (b.Platform, b.Active)));
    }

    [Fact]
    public async Task Given_ein_langsamer_PUT_When_ein_zweites_Feld_derselben_Zeile_geaendert_wird_Then_geht_keine_Aenderung_verloren()
    {
        var slow = new TaskCompletionSource();
        var api = Api(Mailbox, TreatwellRule).OnJson($"PUT {RulesRoute}/{TreatwellRuleId}", body =>
        {
            var input = JsonSerializer.Deserialize<SenderRuleInput>(body!, ApiClient.Json)!;
            return new SenderRuleDto(TreatwellRuleId, input.Pattern, input.Platform, input.Handling, input.Active, input.Note);
        }).Hold($"PUT {RulesRoute}/{TreatwellRuleId}", slow.Task);
        var rules = (await Open(api)).SenderRules;
        var row = rules.Rules.Single();

        row.Platform = SenderRulesViewModel.Platforms[1];
        row.Active = false;
        slow.SetResult();
        await rules.IdleAsync();

        var last = api.Calls.Where(c => c.Route == $"PUT {RulesRoute}/{TreatwellRuleId}").Select(c => JsonSerializer.Deserialize<SenderRuleInput>(c.Body!, ApiClient.Json)!).Last();
        Assert.Equal((SenderPlatform.Other, false), (last.Platform, last.Active));
        Assert.Equal((SenderPlatform.Other, false), (row.Platform.Value, row.Active));
    }

    [Fact]
    public async Task Given_ein_laufender_PUT_When_die_Regel_geloescht_wird_und_der_PUT_404_meldet_Then_keine_Fehlermeldung()
    {
        var slow = new TaskCompletionSource();
        var api = Api(Mailbox, TreatwellRule)
            .On($"PUT {RulesRoute}/{TreatwellRuleId}", new { title = "Gibt es nicht mehr." }, HttpStatusCode.NotFound)
            .Hold($"PUT {RulesRoute}/{TreatwellRuleId}", slow.Task)
            .On($"DELETE {RulesRoute}/{TreatwellRuleId}", null, HttpStatusCode.NoContent);
        var rules = (await Open(api, new FakeDialogs().Answer(ConfirmChoice.Confirmed))).SenderRules;
        var row = rules.Rules.Single();

        row.Active = false;
        await row.DeleteCommand.ExecuteAsync(null);
        slow.SetResult();
        await rules.IdleAsync();

        Assert.Equal("Regel für treatwell.at gelöscht.", rules.Message);
    }

    [Fact]
    public async Task Given_eine_Notiz_When_das_Feld_verlassen_wird_Then_PUT_nur_bei_Aenderung_und_getrimmt()
    {
        var api = Api(Mailbox, TreatwellRule).OnJson($"PUT {RulesRoute}/{TreatwellRuleId}", body =>
        {
            var input = JsonSerializer.Deserialize<SenderRuleInput>(body!, ApiClient.Json)!;
            return new SenderRuleDto(TreatwellRuleId, input.Pattern, input.Platform, input.Handling, input.Active, input.Note);
        });
        var rules = (await Open(api)).SenderRules;
        var row = rules.Rules.Single();

        await row.CommitNoteAsync();
        Assert.Equal(0, CallsTo(api, $"PUT {RulesRoute}/{TreatwellRuleId}"));

        row.Note = "  Neu und wichtig ";
        await row.CommitNoteAsync();
        await rules.IdleAsync();
        Assert.Equal("Neu und wichtig", Sent<SenderRuleInput>(api, $"PUT {RulesRoute}/{TreatwellRuleId}").Note);

        await row.CommitNoteAsync();
        Assert.Equal(1, CallsTo(api, $"PUT {RulesRoute}/{TreatwellRuleId}"));
    }

    [Fact]
    public async Task Given_die_API_lehnt_die_Aenderung_ab_Then_springt_die_Zeile_zurueck_und_die_Meldung_nennt_den_Grund()
    {
        var api = Api(Mailbox, TreatwellRule).On($"PUT {RulesRoute}/{TreatwellRuleId}", new { title = "Gibt es nicht mehr." }, HttpStatusCode.NotFound);
        var rules = (await Open(api)).SenderRules;
        var row = rules.Rules.Single();

        row.Handling = SenderRulesViewModel.Handlings[2];
        await rules.IdleAsync();

        Assert.Equal(SenderHandling.Automatic, row.Handling.Value);
        Assert.Equal("Gibt es nicht mehr.", rules.Message);
    }

    [Fact]
    public async Task Given_eine_Regel_When_Loeschen_bestaetigt_Then_Rueckfrage_und_DELETE()
    {
        var api = Api(Mailbox, TreatwellRule, NewsletterRule).On($"DELETE {RulesRoute}/{NewsletterRuleId}", null, HttpStatusCode.NoContent);
        var dialogs = new FakeDialogs().Answer(ConfirmChoice.Confirmed);
        var rules = (await Open(api, dialogs)).SenderRules;

        await rules.Rules.Single(r => r.Pattern == "newsletter@plattform.example").DeleteCommand.ExecuteAsync(null);

        Assert.Equal(1, CallsTo(api, $"DELETE {RulesRoute}/{NewsletterRuleId}"));
        Assert.Equal(["treatwell.at"], rules.Rules.Select(r => r.Pattern));
        Assert.Equal("Regel für newsletter@plattform.example gelöscht.", rules.Message);
        Assert.Equal("Regel löschen?", dialogs.AssertRules().Asked.Single().Title);
    }

    [Fact]
    public async Task Given_eine_Regel_When_Loeschen_abgebrochen_Then_kein_DELETE()
    {
        var api = Api(Mailbox, TreatwellRule);
        var rules = (await Open(api, new FakeDialogs().Answer(ConfirmChoice.Cancelled))).SenderRules;

        await rules.Rules.Single().DeleteCommand.ExecuteAsync(null);

        Assert.DoesNotContain(api.Calls, c => c.Route.StartsWith("DELETE ", StringComparison.Ordinal));
        Assert.Single(rules.Rules);
    }

    [Fact]
    public async Task Given_keine_Regel_Then_sagt_die_Maske_wie_die_Annahme_dann_arbeitet()
    {
        var rules = (await Open(Api(Mailbox))).SenderRules;

        Assert.True(rules.ShowEmpty);
        Assert.False(rules.HasRules);
    }
}
