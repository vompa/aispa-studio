using AispaStudio.Contracts;
using AispaStudio.Desktop.ViewModels;

namespace AispaStudio.Desktop.Tests;

/// <summary>Posteingang: Original zuerst, Deutsch auf Knopfdruck, KI-Überarbeitung des Entwurfs und Rückgängig.</summary>
public sealed class InboxTranslationTests
{
    private static readonly TimeZoneInfo Vienna = TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna");
    private static readonly string MailRoute = $"GET /api/inbox/{SpaSample.InboxMailId}";

    private sealed class FixedTime : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => SpaSample.At(12);

        public override TimeZoneInfo LocalTimeZone => Vienna;
    }

    private static async Task<(InboxDetailViewModel Detail, StubApi Api)> OpenAsync(InboundMailDto mail)
    {
        var api = new StubApi()
            .On("GET /api/locations", new List<LocationDto> { SpaSample.Vienna })
            .On("GET /api/inbox", new List<InboundMailSummary> { SpaSample.Inbox()[0] })
            .On(MailRoute, mail);
        var vm = new InboxViewModel(api.Client(), null, new FixedTime(), poll: TimeSpan.Zero);
        await vm.InitializeAsync();
        vm.SelectedRow = vm.Mails[0];
        for (var i = 0; i < 100 && vm.Detail is null; i++)
        {
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        return (vm.Detail!, api);
    }

    [Fact]
    public async Task Original_steht_zuerst_und_O_schaltet_auf_Deutsch()
    {
        var (detail, _) = await OpenAsync(SpaSample.InboxMailItalian());

        Assert.True(detail.HasTranslation);
        Assert.False(detail.GermanChosen);
        Assert.False(detail.ShowsTranslation);
        Assert.StartsWith("Buongiorno,", detail.MessageText, StringComparison.Ordinal);
        Assert.False(detail.HasMessageSubject);
        Assert.Equal("DE", detail.LanguageButton);
        Assert.Equal("Auf Deutsch zeigen (O)", detail.LanguageButtonTip);
        Assert.Null(detail.TranslationNote);
        Assert.True(detail.HasReplyTranslation);

        detail.ToggleOriginal();
        Assert.True(detail.ShowsTranslation);
        Assert.StartsWith("Guten Tag,", detail.MessageText, StringComparison.Ordinal);
        Assert.True(detail.HasMessageSubject);
        Assert.Equal("Massage am Donnerstag?", detail.MessageSubject);
        Assert.Equal("Original", detail.LanguageButton);
        Assert.Equal("Original zeigen (O)", detail.LanguageButtonTip);

        detail.ToggleOriginal();
        Assert.StartsWith("Buongiorno,", detail.MessageText, StringComparison.Ordinal);

        // Nach einer Änderung am Entwurf passt die Rückübersetzung nicht mehr.
        detail.ReplyBody += " Grazie!";
        Assert.False(detail.HasReplyTranslation);
    }

    [Fact]
    public async Task Abweichende_Zahl_warnt_nur_beim_deutschen_Text_und_ohne_Uebersetzung_gibt_es_keinen_Wechsel()
    {
        var (mismatch, _) = await OpenAsync(SpaSample.InboxMailItalian() with { Translation = MessageTranslationState.Mismatch });
        Assert.Null(mismatch.TranslationNote);
        mismatch.ToggleOriginal();
        Assert.True(mismatch.ShowsTranslation);
        Assert.Equal("Achtung: eine Zahl weicht vom Original ab.", mismatch.TranslationNote);

        var (unavailable, _) = await OpenAsync(SpaSample.InboxMailItalian() with
        {
            Translation = MessageTranslationState.Unavailable,
            TranslatedBody = null,
            ReplyTranslation = null,
            CanRevise = false,
        });
        Assert.False(unavailable.HasTranslation);
        unavailable.ToggleOriginal();
        Assert.False(unavailable.ShowsTranslation);
        Assert.StartsWith("Buongiorno,", unavailable.MessageText, StringComparison.Ordinal);
        Assert.False(unavailable.CanRevise);
    }

    [Fact]
    public async Task Ueberarbeiten_speichert_vorher_den_Entwurf_und_Rueckgaengig_holt_die_vorige_Fassung()
    {
        var mail = SpaSample.InboxMailItalian();
        var (detail, api) = await OpenAsync(mail);
        api.On($"PUT /api/inbox/{mail.Id}/reply", mail with { ReplyBody = "Gentile Signora Romano, a presto." })
            .On($"POST /api/inbox/{mail.Id}/reply/revise", mail with { ReplyBody = "Gentile Signora Romano, a presto.", ReplyRevising = true })
            .On($"POST /api/inbox/{mail.Id}/reply/undo", mail with { CanUndoReply = false });
        api.On(MailRoute, mail with { ReplyBody = "Cara Giulia, a presto!" });

        detail.ReplyBody = "Gentile Signora Romano, a presto.";
        await detail.ReviseCommand.ExecuteAsync(ReplyRevisionKind.Friendlier);

        var routes = api.Calls.Select(c => c.Route).ToList();
        Assert.True(routes.IndexOf($"PUT /api/inbox/{mail.Id}/reply") < routes.IndexOf($"POST /api/inbox/{mail.Id}/reply/revise"));
        Assert.Contains("\"kind\":\"Friendlier\"", api.Calls.Single(c => c.Route.EndsWith("/revise", StringComparison.Ordinal)).Body, StringComparison.Ordinal);
        Assert.Equal("Cara Giulia, a presto!", detail.ReplyBody);

        detail.ReviseInstruction = "bitte Parkplatz erwähnen";
        await detail.ReviseCustomCommand.ExecuteAsync(null);
        Assert.Contains("\"instruction\":\"bitte Parkplatz er", api.Calls.Last(c => c.Route.EndsWith("/revise", StringComparison.Ordinal)).Body,
            StringComparison.Ordinal);

        await detail.UndoRevisionAsync();
        Assert.Contains(api.Calls, c => c.Route == $"POST /api/inbox/{mail.Id}/reply/undo");
        Assert.False(detail.UndoRevisionCommand.CanExecute(null));
    }
}
