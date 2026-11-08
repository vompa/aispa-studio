using System.Text.Json;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;
using AispaStudio.Desktop.ViewModels;

namespace AispaStudio.Desktop.Tests;

/// <summary>
/// Antworten des Assistenten aus den Daten des Spas (ADR 0072) in der Leiste „KI und Hilfe“: Tabelle, Quelle, Verweis in die Verwaltung, Kopieren, Angebote als Fragen,
/// „Warum diese Antwort?“ mit der Quelle, Beispiele und Platzhalter, Backoffice ohne Kalender.
/// </summary>
public sealed class CompanionBlockTests
{
    private static readonly Guid Location = Guid.NewGuid();

    private static AssistantAnswer Reply(AssistantBlock block) =>
        new(AssistantAudience.Management, block.Text, [], [], "Antwort eines KI-Assistenten. Die Angaben stammen aus den Daten des Spas (Quelle unten), nicht von der KI; bei Zweifeln bitte dort nachsehen.", [], block);

    private static AssistantBlock Report() => new(AssistantBlockKind.Answer, "TopTreatments", "Am häufigsten gebucht, Oktober 2026: 1. Massage (12), 2. Hot Stone (7).",
        new AssistantTable(["Platz", "Behandlung", "Termine"], [["1", "Massage", "12"], ["2", "Hot Stone", "7"]]),
        "Management-Bericht des Standorts Wien Innere Stadt, Oktober 2026",
        new AssistantLink(AssistantLinkKind.Report, "Im Bericht öffnen", "reports", "management", Location, new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 22)), null, "Oktober 2026", []);

    private static CompanionViewModel Companion(StubApi stub, AssistantAudience audience = AssistantAudience.Management) =>
        new(stub.Client(), new MemorySettingsStore(), true, audience, () => Location);

    [Fact]
    public async Task Der_Block_liefert_Tabelle_Quelle_und_Begruendung_mit_Quelle()
    {
        var companion = Companion(new StubApi().On("POST /api/assistant", Reply(Report())));

        await companion.AskExampleCommand.ExecuteAsync("Top-Behandlungen diesen Monat");

        Assert.True(companion.HasAnswer);
        Assert.Equal(["Platz", "Behandlung", "Termine"], companion.BlockTable!.Columns);
        Assert.Equal("Quelle: Management-Bericht des Standorts Wien Innere Stadt, Oktober 2026", companion.SourceText);
        Assert.Equal("Warum diese Antwort?", companion.WhyTitle);
        Assert.Contains("Management-Bericht des Standorts Wien Innere Stadt, Oktober 2026", companion.WhyText, StringComparison.Ordinal);
        Assert.Contains("nur Ihre Frage verstanden", companion.WhyText, StringComparison.Ordinal);
        Assert.False(companion.HasCopy);
        Assert.Empty(companion.FollowUps);
    }

    [Fact]
    public async Task Eine_Antwort_ohne_Block_behaelt_den_bisherigen_Fuss()
    {
        var plain = new AssistantAnswer(AssistantAudience.Reception, "Heute stehen 5 Termine an.", [], [], "Antwort.", []);
        var companion = Companion(new StubApi().On("POST /api/assistant", plain), AssistantAudience.Reception);

        await companion.AskExampleCommand.ExecuteAsync("Was steht heute an?");

        Assert.Null(companion.Block);
        Assert.Null(companion.BlockTable);
        Assert.Null(companion.SourceText);
        Assert.Equal(AiLabels.WhyAssistant, companion.WhyText);
        Assert.Equal(AiLabels.WhyTitle, companion.WhyTitle);
    }

    [Fact]
    public async Task Der_Verweis_oeffnet_die_Verwaltung_beim_Bericht_mit_Standort_und_Zeitraum()
    {
        var stub = new StubApi().On("POST /api/assistant", Reply(Report()));
        var shell = new ShellViewModel(stub.Client(), BackofficeUser(), null, TimeProvider.System, new ThemeController(new MemorySettingsStore()), "", () => Task.CompletedTask);
        await shell.Companion.AskExampleCommand.ExecuteAsync("Top-Behandlungen diesen Monat");

        Assert.True(shell.Companion.HasBlockLink);
        Assert.Equal("Im Bericht öffnen", shell.Companion.LinkLabel);
        shell.Companion.OpenBlockLinkCommand.Execute(null);

        var reports = shell.AdminPage.Reports!;
        Assert.Same(reports, shell.AdminPage.Content);
        Assert.True(reports.IsManagementTab);
        Assert.Equal(new DateOnly(2026, 10, 1), reports.Management.From);
        Assert.Equal(new DateOnly(2026, 10, 22), reports.Management.Until);
        Assert.Same(shell.AdminPage, shell.SelectedPage!.Page);
        shell.Dispose();
    }

    private static AssistantBlock WithGaps(AssistantLink? link) => new(AssistantBlockKind.Answer, "OpeningHours", "Am Samstag, 24.10.2026 ist von 09:00 bis 20:00 Uhr geöffnet (11 Stunden).",
        null, "Öffnungszeiten des Standorts Wien", null, null, null, [],
        [new AssistantIncomplete("Unvollständig: Anreise fehlt beim Standort Wien.", "Stammdaten › Standorte › Anreise und Parkplätze", ["location.directions"], link,
            link is null ? "Nur Leitung" : null)]);

    private static AssistantLink DirectionsLink() =>
        new(AssistantLinkKind.Setting, "Jetzt ergänzen", "masterData", "locations", Location, Field: "location.directions");

    [Fact]
    public async Task Fehlende_Angaben_stehen_als_Meldung_mit_Ort_und_Knopf_nur_mit_Recht()
    {
        var withRight = Companion(new StubApi().On("POST /api/assistant", Reply(WithGaps(DirectionsLink()))));
        var withoutRight = Companion(new StubApi().On("POST /api/assistant", Reply(WithGaps(null))));

        await withRight.AskExampleCommand.ExecuteAsync("Wie lange haben wir heute offen?");
        await withoutRight.AskExampleCommand.ExecuteAsync("Wie lange haben wir heute offen?");

        var gap = Assert.Single(withRight.Gaps);
        Assert.Equal("Unvollständig: Anreise fehlt beim Standort Wien. Zu ergänzen unter Stammdaten › Standorte › Anreise und Parkplätze.", gap.Message);
        Assert.Equal("Jetzt ergänzen", gap.ActionText);
        var hint = Assert.Single(withoutRight.Gaps);
        Assert.Equal("Unvollständig: Anreise fehlt beim Standort Wien. Zu ergänzen unter Stammdaten › Standorte › Anreise und Parkplätze. Nur Leitung.", hint.Message);
        Assert.Null(hint.ActionText);
    }

    /// <summary>Firmendaten als native Maske mit Speichern (R05 Slice 93); die Leiste hängt an ihrem Ereignis <c>Saved</c>.</summary>
    private static StubApi CompanyApi(AssistantAnswer answer)
    {
        var tenant = new TenantDto(Guid.NewGuid(), "Day-Spa Wien", "AT", CompanyProfile.Empty);
        return new StubApi()
            .On("POST /api/assistant", answer)
            .On("GET /api/tenant", tenant)
            .On("PUT /api/tenant", tenant)
            .On("GET /api/tenant/embedding", new EmbeddingSettings([]))
            .On("GET /api/tenant/history", new List<HistoryEntry>());
    }

    private static ShellViewModel OwnerShell(StubApi stub) =>
        new(stub.Client(), TestData.Owner(), null, TimeProvider.System, new ThemeController(new MemorySettingsStore()), "", () => Task.CompletedTask);

    [Fact]
    public async Task Jetzt_ergaenzen_oeffnet_die_Maske_am_Feld_und_nach_dem_Speichern_wiederholt_die_Leiste_die_Frage()
    {
        var link = new AssistantLink(AssistantLinkKind.Setting, "Jetzt ergänzen", "masterData", "company", Field: "company.website");
        var stub = CompanyApi(Reply(WithGaps(link)));
        var shell = OwnerShell(stub);
        await shell.Companion.AskExampleCommand.ExecuteAsync("Wie lange haben wir heute offen?");
        var company = shell.AdminPage.Company!;
        await company.EnsureLoadedAsync();

        // Ohne Klick auf „Jetzt ergänzen“ bleibt ein Speichern in der Verwaltung ohne Folgen.
        Assert.True(await company.SaveChangesAsync());
        Assert.Null(shell.Companion.SavedNotice);

        shell.Companion.OpenGapCommand.Execute(link);
        Assert.Same(shell.AdminPage, shell.SelectedPage!.Page);
        Assert.Same(company, shell.AdminPage.Content);
        Assert.Equal(CompanyViewModel.WebsiteField, company.FocusTarget);
        Assert.Null(shell.Companion.SavedNotice);

        Assert.True(await company.SaveChangesAsync());
        Assert.Equal("Daten gespeichert. Frage wiederholen?", shell.Companion.SavedNotice);

        await shell.Companion.RepeatQuestionCommand.ExecuteAsync(null);
        var questions = stub.Calls.Where(c => c.Route == "POST /api/assistant").Select(c => JsonSerializer.Deserialize<AssistantQuestion>(c.Body!, ApiClient.Json)!.Question).ToList();
        Assert.Equal(["Wie lange haben wir heute offen?", "Wie lange haben wir heute offen?"], questions);
        Assert.Null(shell.Companion.SavedNotice);
        shell.Dispose();
    }

    [Fact]
    public async Task Ein_Verweis_ohne_Feld_wartet_nicht_auf_das_Speichern()
    {
        var shell = OwnerShell(CompanyApi(Reply(Report())));
        await shell.Companion.AskExampleCommand.ExecuteAsync("Top-Behandlungen diesen Monat");
        var company = shell.AdminPage.Company!;
        await company.EnsureLoadedAsync();

        shell.Companion.OpenBlockLinkCommand.Execute(null);
        Assert.True(await company.SaveChangesAsync());

        Assert.Null(shell.Companion.SavedNotice);
        shell.Dispose();
    }

    [Fact]
    public async Task Ohne_Behandler_fuer_den_Verweis_gibt_es_keinen_Knopf()
    {
        var companion = Companion(new StubApi().On("POST /api/assistant", Reply(Report())));

        await companion.AskExampleCommand.ExecuteAsync("Top-Behandlungen diesen Monat");

        // Die Leiste allein weiß nicht, wie man die Verwaltung öffnet; die Shell setzt das.
        Assert.False(companion.HasBlockLink);
        Assert.False(companion.HasBlockActions);
    }

    [Fact]
    public async Task Kopieren_gibt_es_nur_bei_Kontaktdaten_und_meldet_Kopiert()
    {
        var contact = new AssistantBlock(AssistantBlockKind.Answer, "Contact", "Kontaktdaten des Standorts Wien: Telefon +43 1 5120000.",
            new AssistantTable(["Angabe", "Wert"], [["Telefon", "+43 1 5120000"]]), "Kontaktdaten des Standorts Wien", null, "Wien\nTelefon: +43 1 5120000", null, []);
        var companion = Companion(new StubApi().On("POST /api/assistant", Reply(contact)));

        await companion.AskExampleCommand.ExecuteAsync("Unsere Kontaktdaten");

        Assert.True(companion.HasCopy);
        Assert.True(companion.HasBlockActions);
        Assert.Equal("Wien\nTelefon: +43 1 5120000", companion.CopyText);
        Assert.Null(companion.CopiedText);
        companion.MarkCopied();
        Assert.Equal("Kopiert.", companion.CopiedText);
    }

    [Fact]
    public async Task Angebote_der_Antwort_stellen_ihre_Frage_mit_einem_Klick()
    {
        var denied = new AssistantBlock(AssistantBlockKind.Denied, "RevenueByTreatment", "Dafür fehlt die Berechtigung. Umsätze sehen Inhaber, Manager und Backoffice.", null,
            "Berechtigungen dieses Zugangs", null, null, null, [new AssistantFollowUp("Behandlungen nach Anzahl zeigen", "Top-Behandlungen von 01.09.2026 bis 30.09.2026")]);
        var stub = new StubApi().On("POST /api/assistant", Reply(denied));
        var companion = Companion(stub);
        await companion.AskExampleCommand.ExecuteAsync("Umsatz nach Behandlung im September");

        var offer = Assert.Single(companion.FollowUps);
        await companion.AskFollowUpCommand.ExecuteAsync(offer);

        var questions = stub.Calls.Where(c => c.Route == "POST /api/assistant")
            .Select(c => JsonSerializer.Deserialize<AssistantQuestion>(c.Body!, ApiClient.Json)!.Question).ToList();
        Assert.Equal(["Umsatz nach Behandlung im September", "Top-Behandlungen von 01.09.2026 bis 30.09.2026"], questions);
        // Eine Ablehnung nennt keine Quelle der Daten, sondern die Rechte.
        Assert.DoesNotContain("stammen aus", companion.WhyText, StringComparison.Ordinal);
    }

    private static List<AssistantQuestion> Asked(StubApi stub) =>
        [.. stub.Calls.Where(c => c.Route == "POST /api/assistant").Select(c => JsonSerializer.Deserialize<AssistantQuestion>(c.Body!, ApiClient.Json)!)];

    private static AssistantBlock GuestAnswer(Guid? guest, params AssistantFollowUp[] offers) =>
        new(AssistantBlockKind.Answer, "GuestNextVisit", "Antwort", null, "Termine im Kalender", null, null, null, offers, GuestId: guest);

    [Fact]
    public async Task Given_Auswahl_When_Gast_angeklickt_Then_geht_die_Frage_mit_dem_Gast()
    {
        var eva = Guid.NewGuid();
        var stub = new StubApi().On("POST /api/assistant", Reply(GuestAnswer(null, new AssistantFollowUp("Eva Steiner", "wann kommt steiner", eva))));
        var companion = Companion(stub, AssistantAudience.Reception);
        await companion.AskExampleCommand.ExecuteAsync("wann kommt steiner");

        await companion.AskFollowUpCommand.ExecuteAsync(Assert.Single(companion.FollowUps));

        var chosen = Asked(stub)[1];
        Assert.Equal(("wann kommt steiner", eva), (chosen.Question, chosen.GuestId));
        Assert.Null(Asked(stub)[0].GuestId);
    }

    [Fact]
    public async Task Given_Antwort_ueber_einen_Gast_When_naechste_Frage_Then_nennt_der_Bezug_ihn()
    {
        var christian = Guid.NewGuid();
        var stub = new StubApi().On("POST /api/assistant", Reply(GuestAnswer(christian)));
        var companion = Companion(stub, AssistantAudience.Reception);
        await companion.AskExampleCommand.ExecuteAsync("wann kommt herr steiner");

        await companion.AskExampleCommand.ExecuteAsync("wann war er zuletzt da");

        Assert.Equal(new AssistantReference(null, null, christian), Asked(stub)[1].Reference);
        Assert.Null(Asked(stub)[1].GuestId);
    }

    [Fact]
    public async Task Given_offener_Termin_When_Frage_Then_nennt_der_Bezug_den_Termin()
    {
        var appointment = Guid.NewGuid();
        var stub = new StubApi().On("POST /api/assistant", Reply(GuestAnswer(null)));
        var companion = Companion(stub, AssistantAudience.Reception);
        companion.ReferenceSource = () => new AssistantReference(appointment, null, null);

        await companion.AskExampleCommand.ExecuteAsync("wann war sie zuletzt da");

        Assert.Equal(new AssistantReference(appointment, null, null), Assert.Single(Asked(stub)).Reference);
    }

    [Fact]
    public async Task Given_Termin_offen_im_Kalender_When_Frage_in_der_Leiste_Then_Bezug_auf_den_Termin()
    {
        var appointment = TestData.Appointment(10, TestData.Anna);
        var (stub, _) = RosterTests.Setup();
        stub.On("POST /api/assistant", Reply(GuestAnswer(null)))
            .On($"GET /api/locations/{RosterSample.Vienna.Id}/resources", TestData.Resources with { Location = RosterSample.Vienna })
            .On($"GET /api/locations/{RosterSample.Vienna.Id}/calendar?date=2027-03-02", TestData.Day(appointment));
        var shell = new ShellViewModel(stub.Client(), TestData.Owner(), null, new RosterTests.RosterClock(), new ThemeController(new MemorySettingsStore()), "", () => Task.CompletedTask);
        await shell.Calendar.InitializeAsync();
        await shell.Calendar.LoadAsync();
        shell.Calendar.OpenAppointment(appointment);

        await shell.Companion.AskExampleCommand.ExecuteAsync("wann war sie zuletzt da");

        Assert.Equal(new AssistantReference(appointment.Id, null, null), Assert.Single(Asked(stub)).Reference);
        shell.Dispose();
    }

    [Fact]
    public async Task Given_Kundenkarte_offen_When_Frage_in_der_Leiste_Then_Bezug_auf_den_Gast()
    {
        var stub = new StubApi().On("POST /api/assistant", Reply(GuestAnswer(null)));
        var shell = new ShellViewModel(stub.Client(), TestData.Owner(), null, TimeProvider.System, new ThemeController(new MemorySettingsStore()), "", () => Task.CompletedTask);
        var petra = new CustomerDto(Guid.NewGuid(), "Petra", "Schwarz", null, null, null, null);
        shell.SelectedPage = shell.Pages.Single(p => ReferenceEquals(p.Page, shell.Customers));
        shell.Customers.Selected = petra;

        await shell.Companion.AskExampleCommand.ExecuteAsync("wann war sie zuletzt da");

        Assert.Equal(new AssistantReference(null, petra.Id, null), Assert.Single(Asked(stub)).Reference);
        shell.Dispose();
    }

    [Fact]
    public void Platzhalter_ist_neutral_und_Beispiele_nennen_Fragen_an_die_Daten_des_Spas()
    {
        var management = Companion(new StubApi());
        var reception = Companion(new StubApi(), AssistantAudience.Reception);
        var therapist = Companion(new StubApi(), AssistantAudience.Therapist);

        Assert.Equal("Frage eingeben", AiLabels.QuestionPlaceholder);
        Assert.Contains("Wie lange haben wir heute offen?", management.Examples);
        Assert.Contains("Top-Behandlungen diesen Monat", management.Examples);
        Assert.Contains("Wer arbeitet morgen?", reception.Examples);
        Assert.Contains("Welche Rabatte oder Aktionen laufen gerade?", reception.Examples);
        Assert.Contains("Welcher Raum ist jetzt frei?", therapist.Examples);
    }

    [Fact]
    public void Das_Backoffice_fragt_den_Assistenten_nur_nach_Auswertungen()
    {
        var shell = new ShellViewModel(new StubApi().Client(), BackofficeUser(), null, TimeProvider.System, new ThemeController(new MemorySettingsStore()), "",
            () => Task.CompletedTask);

        Assert.True(shell.Companion.CanAsk);
        Assert.True(shell.Companion.ReportsOnly);
        Assert.DoesNotContain("Wie lange haben wir heute offen?", shell.Companion.Examples);
        Assert.Contains("Umsatz nach Behandlung im letzten Monat", shell.Companion.Examples);
        shell.Dispose();
    }

    [Fact]
    public void Die_Texte_der_Kennzeichnung_bleiben_sachlich()
    {
        var texts = new[] { AiLabels.WhyAnswerTitle, AiLabels.WhyBlock("Öffnungszeiten des Standorts Wien"), AiLabels.WhyBlock(null), AiLabels.QuestionPlaceholder, AiLabels.AssistantScope };

        foreach (var text in texts)
        {
            Assert.DoesNotMatch(@"\b(du|dir|dein\w*|ich|mir|leider|gerne|bitte)\b|!", text);
        }
    }

    private static CurrentUser BackofficeUser() =>
        new("u2", "backoffice@aispa.test", "Gerda", 0, new CurrentAccess(Guid.NewGuid(), "Test-Spa", StaffRole.Backoffice, true, []), null,
            [AccessRight.ViewFinance, AccessRight.ViewAmountReports, AccessRight.ViewReceipts, AccessRight.ViewStaffRecords]);
}
