using AispaStudio.Api.Access;
using AispaStudio.Api.Assistant;
using AispaStudio.Api.Assistant.Answers;
using AispaStudio.Api.Tenancy;
using AispaStudio.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace AispaStudio.Api.Tests;

/// <summary>
/// Fragen ohne Antwort (Slice 56): der Assistent gibt die Frage zum Ablegen auf diesem PC mit, ohne Gästedaten. Gespeichert wird nur in der App;
/// der Server bereinigt, weil er alle Kundennamen des Spas kennt.
/// </summary>
public sealed class UnansweredQuestionTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private async Task<AssistantAnswer> AskAsync(TestSpa spa, string question, string? page = null)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Use(spa.TenantId);
        var assistant = ActivatorUtilities.CreateInstance<RoleAssistant>(scope.ServiceProvider,
            (IAssistantClassifier)new RuleBasedAssistantClassifier(), (IAnswerPhraser)new NoAnswerPhraser(), new FixedTime(AnswerKit.Now));
        return await assistant.AskStaffAsync(new UserAccess("tester", StaffRole.Owner, true, new HashSet<Guid>(), null, false),
            new AssistantQuestion(question, spa.Location.Id, page, "windows-app"), Ct);
    }

    [Fact]
    public async Task Given_eine_Frage_ohne_Antwort_When_der_Assistent_sie_meldet_Then_traegt_die_Antwort_die_Frage_zum_Ablegen()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);

        var noIntent = await AskAsync(spa, "Wie färbe ich die Wolken lila?");
        var noHelp = await AskAsync(spa, "Wie mache ich ein Quorxl?");

        Assert.Equal(AssistantBlockKind.NoIntent, noIntent.Block!.Kind);
        Assert.Equal("Wie färbe ich die Wolken lila?", noIntent.Unanswered);
        Assert.Empty(noHelp.Help);
        Assert.Equal("Wie mache ich ein Quorxl?", noHelp.Unanswered);
    }

    [Fact]
    public async Task Given_eine_Frage_mit_einem_Gaestenamen_aus_dem_Kalender_When_sie_abgelegt_wird_Then_ist_der_Name_entfernt()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var last = spa.Customer.LastName;

        var answer = await AskAsync(spa, $"Wie färbe ich die Wolken für Eva {last} lila, Mail eva@example.at?");

        Assert.Equal("Wie färbe ich die Wolken für [Name] [Name] lila, Mail [E-Mail]?", answer.Unanswered);
        Assert.DoesNotContain(last, answer.Unanswered!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Beantwortete_Fragen_tragen_nichts_zum_Ablegen()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);

        var hours = await AskAsync(spa, "Wie lange haben wir heute offen?");
        var help = await AskAsync(spa, "Wie storniere ich einen Termin?");

        Assert.Equal(AssistantBlockKind.Answer, hours.Block!.Kind);
        Assert.Null(hours.Unanswered);
        Assert.NotEmpty(help.Help);
        Assert.Null(help.Unanswered);
    }

    [Fact]
    public void Bereinigen_entfernt_E_Mail_Telefon_und_Namen_und_laesst_Datum_und_Uhrzeit_stehen()
    {
        var cleaned = UnansweredQuestions.Clean(
            "Kann Lena Huber am 26.10.2026 um 14:30 kommen? Tel. +43 664 1234567 oder 0664/123 45 67, lena.huber@example.at, Termin 4711",
            ["lena", "huber"]);

        Assert.Equal("Kann [Name] [Name] am 26.10.2026 um 14:30 kommen? Tel. [Telefon] oder [Telefon], [E-Mail], Termin 4711", cleaned);
        Assert.Equal("Wo ist die Hubertus-Sauna?", UnansweredQuestions.Clean("Wo ist die Hubertus-Sauna?", ["huber"]));
    }

    [Fact]
    public void Bereinigen_erkennt_Genitiv_Doppelnamen_und_Nummern_mit_Punkt_und_laesst_ISO_Datum_und_Jahresbereich_stehen()
    {
        var cleaned = UnansweredQuestions.Clean(
            "Wann kommt Frau Hubers Termin, Huber's Gutschein, Anna-Lena am 2026-10-26? Umsatz 2025-2026, Tel. 0664.1234.567 oder 0664 - 123 4567",
            ["huber", "anna", "lena"]);

        Assert.Equal("Wann kommt Frau [Name] Termin, [Name] Gutschein, [Name] am 2026-10-26? Umsatz 2025-2026, Tel. [Telefon] oder [Telefon]", cleaned);
    }

    [Fact]
    public void Gespeicherte_Doppelnamen_zaehlen_ganz_und_in_ihren_Teilen()
    {
        Assert.Equal(["müller", "lüdenscheidt", "müller-lüdenscheidt"], UnansweredQuestions.NameParts("Müller-Lüdenscheidt"));
        Assert.Equal(["anna", "lena", "anna lena"], UnansweredQuestions.NameParts("Anna Lena"));
    }

    [Fact]
    public async Task Ein_Teil_eines_Doppelnamens_aus_der_Kartei_wird_entfernt()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var last = $"Lüd{Guid.NewGuid():N}";
        await spa.PostAsync<CustomerDto>("/api/customers", new CustomerInput("Mara", $"Ostrowski-{last}", null, null, null, null), Ct);

        var answer = await AskAsync(spa, $"Wie färbe ich die Wolken für {last}s Mutter lila?");

        Assert.Equal("Wie färbe ich die Wolken für [Name] Mutter lila?", answer.Unanswered);
    }
}
