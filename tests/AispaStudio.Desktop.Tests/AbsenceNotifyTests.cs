using AispaStudio.Contracts;
using AispaStudio.Desktop.ViewModels;

namespace AispaStudio.Desktop.Tests;

/// <summary>Ausfall-Assistent: Gäste ohne Ersatz benachrichtigen, erst nach Rückfrage; der Termin bleibt stehen.</summary>
public sealed class AbsenceNotifyTests
{
    private static readonly string Route = $"POST /api/locations/{SpaSample.LocationId}/absence/notify";

    private static async Task<(AbsenceAssistantViewModel Assistant, StubApi Api, FakeDialogs Dialogs)> OpenAsync(AbsenceProposal proposal, params ConfirmChoice[] answers)
    {
        var api = new StubApi().On($"POST /api/locations/{SpaSample.LocationId}/absence/proposal", proposal);
        var dialogs = new FakeDialogs();
        foreach (var answer in answers)
        {
            dialogs.Answer(answer);
        }

        var assistant = new AbsenceAssistantViewModel(api.Client(), SpaSample.LocationId, SpaSample.Siriporn.Id, SpaSample.Siriporn.DisplayName, SpaSample.Day,
            TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna"), _ => Task.CompletedTask, () => { })
        {
            Dialogs = dialogs,
        };
        await assistant.LoadAsync();
        return (assistant, api, dialogs);
    }

    [Fact]
    public async Task Given_ein_Termin_ohne_Ersatz_When_nach_Rueckfrage_benachrichtigt_wird_Then_geht_er_an_die_API_und_steht_an_der_Zeile()
    {
        var proposal = SpaSample.AbsenceProposal();
        var without = proposal.Items[^1].Appointment.Id;
        var (assistant, api, dialogs) = await OpenAsync(proposal, ConfirmChoice.Cancelled, ConfirmChoice.Confirmed);
        api.On(Route, new AbsenceNotifyResult([without], []));

        Assert.True(assistant.CanNotifyWithoutCover);
        await assistant.NotifyWithoutCoverCommand.ExecuteAsync(null);
        // Abgebrochen: Es geht nichts hinaus.
        Assert.DoesNotContain(api.Calls, c => c.Route == Route);
        Assert.Equal("1 Gast benachrichtigen?", dialogs.Asked[0].Title);
        await assistant.NotifyWithoutCoverCommand.ExecuteAsync(null);

        var call = Assert.Single(api.Calls, c => c.Route == Route);
        Assert.Contains(without.ToString(), call.Body, StringComparison.Ordinal);
        Assert.Equal("Gast benachrichtigt", assistant.Rows[^1].Outcome);
        Assert.Equal("1 Gast benachrichtigt.", assistant.Summary);
        // Ein zweites Mal wird derselbe Gast nicht angeschrieben.
        Assert.False(assistant.CanNotifyWithoutCover);
    }

    [Fact]
    public async Task Given_ein_Gast_ohne_Kontakt_Then_nennt_die_Zeile_den_Grund()
    {
        var proposal = SpaSample.AbsenceProposal();
        var without = proposal.Items[^1].Appointment.Id;
        var (assistant, api, _) = await OpenAsync(proposal, ConfirmChoice.Confirmed);
        api.On(Route, new AbsenceNotifyResult([], [new AbsenceMoveFailure(without, "Keine Mailadresse und keine Nummer für eine SMS. Bitte den Gast anrufen.")]));

        await assistant.NotifyWithoutCoverCommand.ExecuteAsync(null);

        Assert.StartsWith("Nicht benachrichtigt: Keine Mailadresse", assistant.Rows[^1].Outcome, StringComparison.Ordinal);
        Assert.Equal("0 Gäste benachrichtigt, 1 nicht: bitte anrufen.", assistant.Summary);
    }

    [Fact]
    public async Task Given_alle_Termine_haben_Ersatz_Then_gibt_es_das_Angebot_nicht()
    {
        var proposal = SpaSample.AbsenceProposal();
        var (assistant, _, _) = await OpenAsync(proposal with { Items = [.. proposal.Items.Take(2)] });

        Assert.False(assistant.CanNotifyWithoutCover);
    }
}
