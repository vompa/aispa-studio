using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using AispaStudio.Api.Access;
using AispaStudio.Api.Assistant;
using AispaStudio.Api.Assistant.Answers;
using AispaStudio.Api.MailIntake.Assistant;
using AispaStudio.Api.Tenancy;
using AispaStudio.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace AispaStudio.Api.Tests;

/// <summary>
/// Der Weg einer Betriebsfrage durch den Assistenten (ADR 0072): jedes Thema hat einen Antwortgeber; mit und ohne Sprachmodell kommen dieselben Fakten; das Modell
/// erkennt höchstens die Absicht und formuliert höchstens den Satz, ohne eine Zahl zu ändern; ohne erkannte Absicht bleibt der Rückfall. Das „Claude“ der Tests ist
/// ein Skript, es gibt keine echten Aufrufe.
/// </summary>
public sealed class BusinessAnswerTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly DateTimeOffset Now = AnswerKit.Now;

    private readonly AnswerKit _kit = new(fixture);

    /// <summary>Ein Sprachmodell nach Drehbuch: antwortet auf die Einordnung der Frage und das Formulieren mit festen JSON-Antworten und merkt sich, was es gefragt wurde.</summary>
    private sealed class ScriptedModel(Func<string, string, string?> script) : IAiJson
    {
        private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

        public List<(string System, string Prompt)> Calls { get; } = [];

        public Task<T> AskAsync<T>(string system, string prompt, Dictionary<string, JsonElement> schema, CancellationToken ct)
        {
            Calls.Add((system, prompt));
            var json = script(system, prompt) ?? throw new InvalidOperationException("Das Modell ist nicht erreichbar.");
            return Task.FromResult(JsonSerializer.Deserialize<T>(json, Web)!);
        }
    }

    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private static string Intent(string task, string? topic = null, string? subject = null, string? from = null, string? until = null, string? detail = null) =>
        JsonSerializer.Serialize(new { task, from, until, customerQuery = (string?)null, helpQuery = (string?)null, topic, subject, detail });

    /// <summary>Der Assistent so, wie er mit Claude läuft (Einordnung und Formulierung durch das Modell), mit festem Zeitpunkt; ohne Modell mit den Regeln.</summary>
    private async Task<AssistantAnswer> AskAsync(TestSpa spa, string question, IAiJson? model, StaffRole role = StaffRole.Owner, bool amountReports = false)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Use(spa.TenantId);
        IAssistantClassifier classifier = model is null ? new RuleBasedAssistantClassifier() : new ModelAssistantClassifier(model);
        IAnswerPhraser phraser = model is null ? new NoAnswerPhraser() : new ModelAnswerPhraser(model, NullLogger<ModelAnswerPhraser>.Instance);
        var assistant = ActivatorUtilities.CreateInstance<RoleAssistant>(scope.ServiceProvider, classifier, phraser, new FixedTime(Now));
        return await assistant.AskStaffAsync(new UserAccess("tester", role, true, new HashSet<Guid>(), null, amountReports), new AssistantQuestion(question, spa.Location.Id), Ct);
    }

    [Fact]
    public async Task Jedes_Thema_hat_einen_Antwortgeber_und_die_Tests_laufen_ohne_echtes_Claude()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();

        var topics = scope.ServiceProvider.GetRequiredService<BusinessAnswers>().Topics;

        Assert.Equal(Enum.GetValues<BusinessTopic>().Order(), topics.Order());
        // Testumgebung: kein Schlüssel zählt, auch nicht aus den User-Secrets des Rechners (Ai:Provider = RuleBased).
        Assert.IsType<RuleBasedAssistantClassifier>(scope.ServiceProvider.GetRequiredService<IAssistantClassifier>());
        Assert.IsType<NoAnswerPhraser>(scope.ServiceProvider.GetRequiredService<IAnswerPhraser>());
    }

    [Fact]
    public async Task Mit_und_ohne_Modell_kommen_dieselben_Fakten_und_das_Modell_wird_dafuer_nicht_gebraucht()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var model = new ScriptedModel((_, _) => null);

        foreach (var question in new[] { "Haben wir am Samstag offen?", "Wer arbeitet heute?", "Welche Rabatte oder Aktionen laufen gerade?", "Unsere Kontaktdaten", "Wann ist der nächste Feiertag?" })
        {
            var rules = await AskAsync(spa, question, null);
            var claude = await AskAsync(spa, question, model);

            Assert.Equal(rules.Text, claude.Text);
            Assert.Equal(rules.Block!.Table?.Rows, claude.Block!.Table?.Rows);
            Assert.Equal((rules.Block.Source, rules.Block.Kind, rules.Block.Topic), (claude.Block.Source, claude.Block.Kind, claude.Block.Topic));
        }

        // Die Regeln erkennen diese Fragen selbst: das Modell wurde nur zum Formulieren gefragt (und hat nicht geantwortet), die Einordnung braucht es nicht.
        Assert.All(model.Calls, c => Assert.Contains("formulierst", c.System, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("wann hat herr steiner seinen termin", false)]
    [InlineData("wann kommt steiner", false)]
    [InlineData("termin herr steinr", false)]
    [InlineData("wann war sie zuletzt da", true)]
    public async Task Gastfragen_ergeben_mit_und_ohne_Modell_dieselbe_Antwort(string question, bool openAppointment)
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var christian = await spa.PostAsync<CustomerDto>("/api/customers",
            new CustomerInput("Christian", "Steiner", null, null, null, null, Salutation: CustomerSalutation.Male), Ct);
        await spa.PostAsync<CustomerDto>("/api/customers", new CustomerInput("Eva", "Steiner", null, null, null, null, Salutation: CustomerSalutation.Female), Ct);
        var petra = await spa.PostAsync<CustomerDto>("/api/customers", new CustomerInput("Petra", "Schwarz", null, null, null, null), Ct);
        await spa.BookOkAsync(TestSpa.At(14, date: TestSpa.Monday.AddDays(7)), Ct, spa.Ben.Id, customer: christian.Id);
        var open = await spa.BookOkAsync(TestSpa.At(10, date: TestSpa.Monday.AddDays(7)), Ct, spa.Anna.Id, customer: petra.Id);
        var asked = new AssistantQuestion(question, null, Reference: openAppointment ? new AssistantReference(open.Id, null, null) : null);
        var model = new ScriptedModel((_, _) => null);

        var rules = (await _kit.StaffAsync(spa, asked)).Block!;
        var claude = (await _kit.StaffAsync(spa, asked, model)).Block!;

        Assert.Equal((rules.Kind, rules.Text, rules.GuestId), (claude.Kind, claude.Text, claude.GuestId));
        Assert.Equal(rules.Table?.Rows, claude.Table?.Rows);
        Assert.Equal(rules.FollowUps, claude.FollowUps);
        Assert.Equal(AssistantBlockKind.Answer, rules.Kind);
        // Die Regeln erkennen die Frage selbst: das Modell wurde nur zum Formulieren gefragt.
        Assert.All(model.Calls, c => Assert.Contains("formulierst", c.System, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Das_Modell_ordnet_Umschreibungen_ein_die_die_Regeln_nicht_kennen_und_die_Fakten_bleiben_aus_den_Daten()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        const string Question = "Ist samstags bei uns Betrieb?";
        var model = new ScriptedModel((system, _) => system.Contains("ordnest", StringComparison.Ordinal)
            ? Intent("Business", "OpeningHours", from: "2027-03-06", until: "2027-03-06")
            : null);

        var rules = await AskAsync(spa, Question, null);
        var claude = await AskAsync(spa, Question, model);

        Assert.Equal(AssistantBlockKind.NoIntent, rules.Block!.Kind);
        Assert.Equal(AssistantBlockKind.Answer, claude.Block!.Kind);
        Assert.Equal("Ja, am Samstag, 06.03.2027 ist von 09:00 bis 20:00 Uhr geöffnet (11 Stunden).", claude.Text);
        Assert.Equal("Öffnungszeiten des Standorts Wien", claude.Block.Source);
    }

    [Fact]
    public async Task Die_Formulierung_des_Modells_darf_keine_Zahl_aendern_sonst_bleibt_der_Satz_aus_den_Daten()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        async Task<AssistantAnswer> WithPhrase(string phrase)
        {
            var model = new ScriptedModel((system, _) => system.Contains("formulierst", StringComparison.Ordinal) ? JsonSerializer.Serialize(new { text = phrase }) : null);
            return await AskAsync(spa, "Haben wir am Samstag offen?", model);
        }

        var kept = await WithPhrase("Am Samstag, dem 06.03.2027, ist das Spa von 09:00 bis 20:00 Uhr geöffnet, also 11 Stunden lang.");
        var changedTimes = await WithPhrase("Am Samstag ist das Spa von 08:00 bis 22:00 Uhr geöffnet.");
        var changedHours = await WithPhrase("Am Samstag ist das Spa von 09:00 bis 20:00 Uhr geöffnet, also 12 Stunden lang.");
        var numberWord = await WithPhrase("Am Samstag ist von neun bis zwanzig Uhr geöffnet, zwölf Stunden lang.");
        var empty = await WithPhrase("   ");
        var tooLong = await WithPhrase(new string('x', 700));

        Assert.Equal("Ja, am Samstag, 06.03.2027 ist von 09:00 bis 20:00 Uhr geöffnet (11 Stunden).", (await AskAsync(spa, "Haben wir am Samstag offen?", null)).Text);
        Assert.Equal("Am Samstag, dem 06.03.2027, ist das Spa von 09:00 bis 20:00 Uhr geöffnet, also 11 Stunden lang.", kept.Text);
        Assert.Equal(kept.Text, kept.Block!.Text);
        Assert.Equal("Öffnungszeiten des Standorts Wien", kept.Block.Source);
        foreach (var rejected in new[] { changedTimes, changedHours, numberWord, empty, tooLong })
        {
            Assert.Equal("Ja, am Samstag, 06.03.2027 ist von 09:00 bis 20:00 Uhr geöffnet (11 Stunden).", rejected.Text);
        }

    }

    [Fact]
    public async Task Das_Modell_bekommt_nur_die_Frage_und_die_Fakten_und_fehlende_Angaben_werden_nie_umformuliert()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var model = new ScriptedModel((system, _) => system.Contains("formulierst", StringComparison.Ordinal) ? JsonSerializer.Serialize(new { text = "Erfunden." }) : null);

        var answer = await AskAsync(spa, "Wie lautet unsere Telefonnummer?", model);
        var denied = await AskAsync(spa, "Umsatz nach Behandlung diesen Monat", model, StaffRole.Reception);
        var stored = await AskAsync(spa, "Wie kommt der Gast zu uns, Parkplätze?", model);

        // Fehlende Angaben und fehlende Rechte lauten immer gleich; nur eine Antwort aus Daten darf das Modell glätten.
        Assert.Equal("Dazu ist nichts hinterlegt: Telefon.", answer.Text);
        Assert.Equal(AssistantBlockKind.Denied, denied.Block!.Kind);
        Assert.Equal("Dazu ist nichts hinterlegt: Anreise und Parkplätze.", stored.Text);
        Assert.Empty(model.Calls);

        // Geprüft werden Zahlen, nicht Wörter: ein Satz ohne Zahl, den das Modell vorschlägt, ersetzt den Satz aus den Daten (Quelle und Tabelle bleiben).
        var plain = await AskAsync(spa, "Wer arbeitet heute?", model);
        Assert.Equal("Erfunden.", plain.Text);
        Assert.Equal("Dienstplan und Arbeitszeiten des Standorts Wien", plain.Block!.Source);
        var call = Assert.Single(model.Calls);
        Assert.Contains("<frage>\nWer arbeitet heute?\n</frage>", call.Prompt, StringComparison.Ordinal);
        Assert.Contains("<fakten>\nAm Mittwoch, 03.03.2027 ist im Standort Wien niemand im Dienst eingeteilt.", call.Prompt, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Antwortet_das_Modell_nicht_bleibt_es_bei_den_Regeln_und_beim_Satz_aus_den_Daten()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var offline = new ScriptedModel((_, _) => null);

        var ruleHit = await AskAsync(spa, "Wann sperren wir morgen zu?", offline);

        Assert.Equal("Am Donnerstag, 04.03.2027 wird um 20:00 Uhr zugesperrt (geöffnet 09:00 bis 20:00 Uhr, 11 Stunden).", ruleHit.Text);
        Assert.Equal(AssistantBlockKind.Answer, ruleHit.Block!.Kind);
    }

    [Fact]
    public async Task Ohne_Absicht_und_ohne_Handbuchtreffer_kommt_Dazu_habe_ich_nichts_gefunden_mit_zwei_Fragen()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);

        var owner = await AskAsync(spa, "Wie spät ist es in Tokio?", null);
        var reception = await AskAsync(spa, "Wie spät ist es in Tokio?", null, StaffRole.Reception);
        var therapist = await AskAsync(spa, "Wie spät ist es in Tokio?", null, StaffRole.Therapist);
        var treatmentNamed = await AskAsync(spa, "Massage am 01.03.2027 bei Anna", null);

        Assert.Equal("Dazu habe ich nichts gefunden. Meinen Sie vielleicht eine dieser Fragen?", owner.Text);
        Assert.Equal(AssistantBlockKind.NoIntent, owner.Block!.Kind);
        Assert.Equal(["Wie lange haben wir heute offen?", "Top-Behandlungen diesen Monat"], owner.Block.FollowUps.Select(f => f.Question));
        Assert.Equal(["Wer arbeitet morgen?", "Welche Rabatte oder Aktionen laufen gerade?"], reception.Block!.FollowUps.Select(f => f.Question));
        Assert.Equal(["Wer arbeitet morgen?", "Was steht heute an?"], therapist.Block!.FollowUps.Select(f => f.Question));
        // Die Angebote sind Fragen, die der Assistent beantwortet.
        var today = DateOnly.FromDateTime(Now.DateTime);
        Assert.All(owner.Block.FollowUps.Concat(reception.Block.FollowUps).Concat(therapist.Block.FollowUps),
            offer => Assert.True(BusinessIntents.Detect(offer.Question, today) is not null || offer.Question == "Was steht heute an?", offer.Question));

        // Nennt die Frage eine Behandlung, geht es wie bisher mit freien Zeiten weiter.
        Assert.Null(treatmentNamed.Block);
        Assert.Contains("Massage", treatmentNamed.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Das_Handbuch_bleibt_die_Rueckfallebene_vor_dem_Nichts_gefunden()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);

        var help = await AskAsync(spa, "Wie verschiebe ich einen Termin auf eine andere Uhrzeit?", null);

        Assert.Null(help.Block);
        Assert.Equal("kalender-termin-verschieben", help.Help[0].Topic);
        Assert.Equal(RoleAssistant.Disclosure, help.Disclosure);
    }

    [Fact]
    public async Task Antworten_aus_den_Daten_tragen_den_Hinweis_mit_Quelle_statt_des_allgemeinen()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        using var reception = await _kit.JoinAsync(spa, StaffRole.Reception);

        var answer = await AnswerKit.AskAsync(reception, "Haben wir am Samstag offen?", spa.Location.Id);

        Assert.Equal(RoleAssistant.BlockDisclosure, answer.Disclosure);
        Assert.Contains("Quelle unten", answer.Disclosure, StringComparison.Ordinal);
        Assert.Equal(answer.Block!.Text, answer.Text);
        Assert.Equal(AssistantAudience.Reception, answer.Audience);
        Assert.DoesNotContain(" dir ", answer.Block.Text + answer.Block.Source, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Die_Texte_des_Assistenten_duzen_nicht_und_sprechen_nicht_in_der_Ich_Form()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var asked = new List<(string Question, StaffRole Role)>
        {
            ("Wie war der Umsatz letzte Woche?", StaffRole.Reception), ("Wer ist Frau Muster?", StaffRole.Therapist), ("Telefonnummer von Frau Muster", StaffRole.Therapist),
            ("Wann ist eine Massage frei?", StaffRole.Owner), ("Massage am 01.03.2027", StaffRole.Owner), ("Wer ist", StaffRole.Reception), ("Top-Behandlungen diesen Monat", StaffRole.Reception),
            ("Umsatz nach Behandlung im September", StaffRole.Manager), ("Wann war Frau Zzyzx zuletzt da?", StaffRole.Owner), ("Wie viel ist auf Gutschein 0000 noch drauf?", StaffRole.Therapist),
            ("Was kostet das Aromaritual?", StaffRole.Owner), ("Welche Rabatte laufen gerade?", StaffRole.Owner), ("Was steht heute an?", StaffRole.Therapist),
            ("Wann sperren wir morgen zu?", StaffRole.Reception), ("Wer arbeitet heute?", StaffRole.Backoffice),
        };

        foreach (var (question, role) in asked)
        {
            var answer = await AskAsync(spa, question, null, role);
            var text = $"{answer.Text} {answer.Block?.Text} {answer.Block?.Source}";

            Assert.False(Regex.IsMatch(text, @"\b(du|dir|dich|dein\w*|deine\w*|leider)\b|!"), $"{question}: {text}");
            // Die Ich-Form nur im Wortlaut „Dazu habe ich nichts gefunden“ und „Dazu habe ich im Handbuch nichts gefunden“, den der Auftrag so vorgibt.
            var rest = text.Replace("Dazu habe ich nichts gefunden", "", StringComparison.Ordinal).Replace("Dazu habe ich im Handbuch nichts gefunden", "", StringComparison.Ordinal);
            Assert.False(Regex.IsMatch(rest, @"\bich\b"), $"{question}: {text}");
        }
    }

    [Fact]
    public async Task Fremder_Standort_und_ueberlange_Fragen_werden_wie_bisher_abgewiesen()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var other = await TestSpa.CreateAsync(fixture, Ct);
        using var reception = await _kit.JoinAsync(spa, StaffRole.Reception, allLocations: false);

        var foreign = await reception.PostAsJsonAsync("/api/assistant", new AssistantQuestion("Wer arbeitet heute?", other.Location.Id), TestSpa.Json, Ct);
        var tooLong = await reception.PostAsJsonAsync("/api/assistant", new AssistantQuestion(new string('x', 1001), spa.Location.Id), TestSpa.Json, Ct);

        Assert.Equal(HttpStatusCode.Forbidden, foreign.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);
    }
}
