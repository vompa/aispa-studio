using System.Net;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;
using AispaStudio.Desktop.ViewModels;

namespace AispaStudio.Desktop.Tests;

/// <summary>
/// Slice 134 (Ä316): Listen-Antworten der Leiste als Kopfsatz und eine Liste anklickbarer Zeilen; die Frage geht bei Fehler oder leerer Antwort nicht verloren.
/// </summary>
public sealed class CompanionListTests
{
    private const string Route = "POST /api/assistant";
    private static readonly Guid Location = Guid.NewGuid();
    private static readonly DateTimeOffset Morning = new(2027, 3, 2, 8, 0, 0, TimeSpan.Zero);

    /// <summary>Termin <paramref name="index"/> der Liste: ab 09:00 Uhr alle 45 Minuten.</summary>
    internal static AssistantItem Appointment(int index, string treatment = "Massage 60 Min.")
    {
        var minutes = (9 * 60) + (45 * index);
        return new($"Gast {index + 1}", $"{treatment} · Lena", $"{minutes / 60:00}:{minutes % 60:00}",
            new AssistantSuggestion(AssistantSuggestionKind.OpenAppointment, $"Gast {index + 1}", null, null, null, null, Guid.NewGuid(), Morning.AddMinutes(45 * index)),
            null, "Im Kalender zeigen");
    }

    internal static AssistantAnswer ListAnswer(string text, IReadOnlyList<AssistantItem> items, params AssistantSuggestion[] suggestions) =>
        new(AssistantAudience.Reception, text, [], suggestions, "Antwort einer KI.", [],
            new AssistantBlock(AssistantBlockKind.Answer, "DayOverview", text, null, "Kalender des Standorts Wien", null, null, null, [], Items: items));

    internal static AssistantAnswer Fifteen() =>
        ListAnswer("Dienstag, 2. März, Wien: 15 Termine.", [.. Enumerable.Range(0, 15).Select(i => Appointment(i))]);

    private static CompanionViewModel Companion(StubApi stub) =>
        new(stub.Client(), new MemorySettingsStore(), true, AssistantAudience.Reception, () => Location);

    [Fact]
    public async Task Fuenfzehn_Zeilen_zeigen_zehn_und_Alle_zeigen_klappt_auf()
    {
        var companion = Companion(new StubApi().On(Route, Fifteen()));

        await companion.AskExampleCommand.ExecuteAsync("Termine morgen?");

        Assert.Equal("Dienstag, 2. März, Wien: 15 Termine.", companion.Answer?.Text);
        Assert.Equal(10, companion.Rows.Count);
        Assert.Equal(("09:00", "Gast 1", "Massage 60 Min. · Lena"), (companion.Rows[0].Lead, companion.Rows[0].Title, companion.Rows[0].Detail));
        Assert.Equal("09:00, Gast 1, Massage 60 Min. · Lena. Im Kalender zeigen", companion.Rows[0].AccessibleName);
        Assert.True(companion.HasMoreRows);
        Assert.Equal("Alle zeigen (15)", companion.ShowAllText);

        companion.ShowAllRowsCommand.Execute(null);

        Assert.Equal(15, companion.Rows.Count);
        Assert.False(companion.HasMoreRows);

        // Die nächste Antwort beginnt wieder mit zehn Zeilen.
        await companion.AskExampleCommand.ExecuteAsync("Termine morgen?");
        Assert.Equal(10, companion.Rows.Count);
    }

    [Fact]
    public async Task Eine_Zeile_ist_kein_zweites_Mal_als_Vorschlag_da()
    {
        var search = new AssistantSuggestion(AssistantSuggestionKind.SearchFreeTime, "Freie Zeit suchen", Location, null, null, null, null, null);
        var companion = new CompanionViewModel(new StubApi().On(Route, ListAnswer("2 Termine.", [Appointment(0), Appointment(1)], search)).Client(),
            new MemorySettingsStore(), true, AssistantAudience.Reception, () => Location)
        {
            CanBook = true,
            AppointmentRequested = (_, _) => { },
            FreeTimeRequested = (_, _) => Task.CompletedTask,
        };

        await companion.AskExampleCommand.ExecuteAsync("Termine morgen?");

        Assert.Equal(2, companion.Rows.Count);
        Assert.Equal(["Suche öffnen"], companion.Suggestions.Select(s => s.Action));
    }

    [Fact]
    public async Task Terminzeile_zeigt_den_Termin_im_Kalender()
    {
        (Guid Id, DateTimeOffset? Start)? shown = null;
        var companion = new CompanionViewModel(new StubApi().On(Route, Fifteen()).Client(), new MemorySettingsStore(), true, AssistantAudience.Reception, () => Location)
        {
            AppointmentRequested = (id, start) => shown = (id, start),
        };
        await companion.AskExampleCommand.ExecuteAsync("Termine morgen?");

        await companion.OpenRowCommand.ExecuteAsync(companion.Rows[2]);

        var expected = companion.Rows[2].Item.Suggestion!;
        Assert.Equal((expected.AppointmentId!.Value, expected.Start), shown);
    }

    [Fact]
    public async Task Personenzeile_oeffnet_den_Wochenplan_am_Tag_der_Antwort()
    {
        var lena = Guid.NewGuid();
        var link = new AssistantLink(AssistantLinkKind.Roster, "Im Wochenplan zeigen", "roster", null, Location, new DateOnly(2027, 3, 3), TargetId: lena);
        var answer = ListAnswer("Am Mittwoch, 3. März arbeiten im Standort Wien 1 Person.", [new AssistantItem("Lena", "09:00–17:00", null, null, link, "Im Wochenplan zeigen")]);
        AssistantLink? opened = null;
        var companion = new CompanionViewModel(new StubApi().On(Route, answer).Client(), new MemorySettingsStore(), true, AssistantAudience.Reception, () => Location)
        {
            LinkRequested = l => opened = l,
        };
        await companion.AskExampleCommand.ExecuteAsync("Wer arbeitet am Mittwoch?");

        var row = Assert.Single(companion.Rows);
        Assert.True(row.CanOpen);
        await companion.OpenRowCommand.ExecuteAsync(row);

        Assert.Equal(link, opened);
    }

    [Fact]
    public async Task Fehler_der_API_laesst_die_Frage_im_Feld_und_bietet_Erneut_fragen()
    {
        var stub = new StubApi().On(Route, Fifteen());
        var companion = Companion(stub);
        await companion.AskExampleCommand.ExecuteAsync("Termine morgen?");
        var before = companion.Answer;
        stub.On(Route, null, HttpStatusCode.InternalServerError);

        companion.Question = "Termine übermorgen?";
        await companion.AskCommand.ExecuteAsync(null);

        Assert.Equal("Termine übermorgen?", companion.Question);
        Assert.Equal("Der Assistent konnte die Frage gerade nicht beantworten.", companion.Error);
        Assert.True(companion.AskCommand.CanExecute(null));
        Assert.Same(before, companion.Answer);
        Assert.True(companion.ShowsAnswer);
    }

    [Fact]
    public async Task Leere_Antwort_gilt_als_Fehler_und_die_Frage_bleibt_im_Feld()
    {
        var companion = Companion(new StubApi().On(Route, new AssistantAnswer(AssistantAudience.Reception, " ", [], [], "Antwort einer KI.", [])));

        companion.Question = "Kaffee?";
        await companion.AskCommand.ExecuteAsync(null);

        Assert.Equal("Kaffee?", companion.Question);
        Assert.Equal(CompanionViewModel.EmptyAnswer, companion.Error);
        Assert.Null(companion.Answer);
    }

    [Fact]
    public async Task Waehrend_der_Antwort_steht_die_Frage_als_Kopf_und_es_laedt()
    {
        var gate = new TaskCompletionSource();
        var companion = Companion(new StubApi().On(Route, Fifteen()).Hold(Route, gate.Task));

        companion.Question = "Termine morgen?";
        var asking = companion.AskCommand.ExecuteAsync(null);

        Assert.True(companion.IsAsking);
        Assert.Equal("Termine morgen?", companion.Pending);
        Assert.Equal("Termine morgen?", companion.Question);
        Assert.False(companion.ShowsAnswer);

        gate.SetResult();
        await asking;

        Assert.Null(companion.Pending);
        Assert.Equal("Termine morgen?", companion.Asked);
        Assert.Equal("", companion.Question);
        Assert.True(companion.ShowsAnswer);
    }

    [Fact]
    public async Task Erneut_fragen_stellt_die_Frage_noch_einmal()
    {
        var stub = new StubApi().On(Route, null, HttpStatusCode.ServiceUnavailable);
        var companion = Companion(stub);
        companion.Question = "Termine morgen?";
        await companion.AskCommand.ExecuteAsync(null);
        Assert.NotNull(companion.Error);

        stub.On(Route, Fifteen());
        await companion.AskCommand.ExecuteAsync(null);

        Assert.Equal(2, stub.Calls.Count(c => c.Route == Route));
        Assert.Null(companion.Error);
        Assert.Equal(10, companion.Rows.Count);
    }

    [Fact]
    public async Task Pfeil_nach_oben_holt_die_letzte_Frage_nur_ins_leere_Feld()
    {
        var companion = Companion(new StubApi().On(Route, Fifteen()));
        Assert.False(companion.RecallQuestion());

        companion.Question = "Termine morgen?";
        await companion.AskCommand.ExecuteAsync(null);
        Assert.Equal("", companion.Question);

        Assert.True(companion.RecallQuestion());
        Assert.Equal("Termine morgen?", companion.Question);

        companion.Question = "Wer";
        Assert.False(companion.RecallQuestion());
        Assert.Equal("Wer", companion.Question);
    }
}
