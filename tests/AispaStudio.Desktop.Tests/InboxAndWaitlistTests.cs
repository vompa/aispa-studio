using System.Net;
using System.Text.Json;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;
using AispaStudio.Desktop.ViewModels;

namespace AispaStudio.Desktop.Tests;

public sealed class InboxAndWaitlistTests
{
    private static readonly TimeZoneInfo Vienna = TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna");
    private static readonly Guid MailId = Guid.NewGuid();
    private static readonly Guid OtherId = Guid.NewGuid();

    private sealed class FixedTime : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => TestData.At(10);

        public override TimeZoneInfo LocalTimeZone => Vienna;
    }

    private static InboundMailSummary Summary(Guid id, InboundMailStatus status, BookingChannel channel = BookingChannel.Email) =>
        new(id, channel, "eva@example.at", "Eva Muster", "Terminanfrage", TestData.At(9, 12), status, InboundMailKind.BookingRequest,
            ReplyStatus.Draft, "Massage morgen 15 Uhr", null);

    private static InboundMailDto Mail(InboundMailStatus status = InboundMailStatus.Proposed, ReplyStatus reply = ReplyStatus.Draft,
        string replyBody = "Liebe Frau Muster, gerne um 15:00.", Guid? appointmentId = null) =>
        new(MailId, BookingChannel.Email, "eva@example.at", "Eva Muster", "Terminanfrage", "Hallo, Massage morgen 15 Uhr?", TestData.At(9, 12),
            status, InboundMailKind.BookingRequest, "de", "Massage morgen 15 Uhr", status == InboundMailStatus.Proposed ? "15 Uhr ist belegt." : null,
            TestData.LocationId, TestData.MassageTreatment.Id, null, appointmentId,
            [new AvailableSlot(TestData.At(16), TestData.At(16, 50), TestData.Anna.Id, TestData.Room.Id, null)],
            reply, "eva@example.at", "Ihr Termin", replyBody, "Diese Antwort wurde mit KI erstellt.", TestData.At(9, 13), null);

    private static StubApi Api() => new StubApi()
        .On("GET /api/locations", new List<LocationDto> { TestData.Location })
        .On("GET /api/inbox", new List<InboundMailSummary>
        {
            Summary(MailId, InboundMailStatus.Proposed),
            Summary(OtherId, InboundMailStatus.Booked, BookingChannel.WhatsApp),
        })
        .On($"GET /api/inbox/{MailId}", Mail());

    private static async Task<InboxViewModel> OpenAsync(StubApi api, FakeLive? live = null)
    {
        var vm = new InboxViewModel(api.Client(), live, new FixedTime(), poll: TimeSpan.Zero);
        await vm.InitializeAsync();
        return vm;
    }

    private static async Task<InboxDetailViewModel> SelectAsync(InboxViewModel vm, Guid id)
    {
        vm.SelectedRow = vm.Mails.First(m => m.Mail.Id == id);
        for (var i = 0; i < 50 && vm.Detail?.Id != id; i++)
        {
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        return vm.Detail!;
    }

    [Fact]
    public async Task Posteingang_zeigt_Stand_Kanal_und_Uhrzeit()
    {
        var api = Api();
        var vm = await OpenAsync(api);

        var detail = await SelectAsync(vm, MailId);

        Assert.Equal(["Vorschlag", "Eingetragen"], vm.Mails.Select(m => m.StatusText));
        Assert.Equal("WhatsApp", vm.Mails[1].ChannelText);
        Assert.True(vm.Mails[1].ShowChannel);
        Assert.Equal("09:12", vm.Mails[0].TimeText);
        Assert.Equal(1, vm.OpenCount);
        Assert.Equal("Vorschlag", detail.StatusText);
    }

    [Fact]
    public async Task Antwort_erst_speichern_dann_senden_und_bis_gesendet_nachfragen()
    {
        var api = Api();
        var vm = await OpenAsync(api);
        var detail = await SelectAsync(vm, MailId);
        api.On($"PUT /api/inbox/{MailId}/reply", Mail(replyBody: "Liebe Frau Muster, gerne um 16:00."))
            .On($"POST /api/inbox/{MailId}/send", Mail(reply: ReplyStatus.Sending, replyBody: "Liebe Frau Muster, gerne um 16:00."));

        var sendBefore = detail.SendCommand.CanExecute(null);
        detail.ReplyBody = "Liebe Frau Muster, gerne um 16:00.";
        var canSave = detail.SaveDraftCommand.CanExecute(null);
        var canSendEdited = detail.SendCommand.CanExecute(null);
        await detail.SaveDraftCommand.ExecuteAsync(null);
        var saved = JsonSerializer.Deserialize<ReplyDraftInput>(api.Calls.Last(c => c.Route.StartsWith("PUT", StringComparison.Ordinal)).Body!, ApiClient.Json)!;
        api.On($"GET /api/inbox/{MailId}", Mail(reply: ReplyStatus.Sent, replyBody: "Liebe Frau Muster, gerne um 16:00."));
        await detail.SendCommand.ExecuteAsync(null);

        Assert.True(sendBefore);
        Assert.True(canSave);
        Assert.False(canSendEdited);
        Assert.Equal("Liebe Frau Muster, gerne um 16:00.", saved.Body);
        Assert.Equal("Ihr Termin", saved.Subject);
        Assert.Equal("Gesendet.", detail.ReplyState);
        Assert.False(detail.IsDraft);
        Assert.Equal("Beim Senden angehängt: „Diese Antwort wurde mit KI erstellt.“", detail.DraftHint);
    }

    [Fact]
    public async Task Live_Meldung_laedt_nach_ohne_den_bearbeiteten_Entwurf_zu_ueberschreiben()
    {
        var api = Api();
        var live = new FakeLive();
        var vm = await OpenAsync(api, live);
        var detail = await SelectAsync(vm, MailId);
        detail.ReplyBody = "Mein eigener Text";
        api.On($"GET /api/inbox/{MailId}", Mail(InboundMailStatus.NeedsReview, replyBody: "Neuer Entwurf der KI"))
            .On("GET /api/inbox", new List<InboundMailSummary> { Summary(MailId, InboundMailStatus.NeedsReview), Summary(Guid.NewGuid(), InboundMailStatus.Failed) });

        live.SendInbox(MailId);
        for (var i = 0; i < 50 && detail.StatusText != "Prüfen"; i++)
        {
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        Assert.True(live.WatchingInbox);
        Assert.True(vm.IsLive);
        Assert.Equal("Prüfen", detail.StatusText);
        Assert.Equal("Mein eigener Text", detail.ReplyBody);
        Assert.True(detail.DraftChanged);
        Assert.Equal(2, vm.OpenCount);
        Assert.Same(detail, vm.Detail);
    }

    [Fact]
    public async Task Erledigen_und_Fehler_beim_erneuten_Verarbeiten_werden_gemeldet()
    {
        var api = Api();
        var vm = await OpenAsync(api);
        var detail = await SelectAsync(vm, MailId);
        api.On($"POST /api/inbox/{MailId}/retry", new { errors = new Dictionary<string, string[]> { ["status"] = ["Nur fehlgeschlagene Mails werden erneut verarbeitet."] } },
                HttpStatusCode.BadRequest)
            .On($"POST /api/inbox/{MailId}/dismiss", Mail(InboundMailStatus.Ignored, reply: ReplyStatus.None));

        await detail.RetryCommand.ExecuteAsync(null);
        var error = detail.Notice;
        var wasError = detail.NoticeIsError;
        await detail.DismissCommand.ExecuteAsync(null);

        Assert.Equal("Nur fehlgeschlagene Mails werden erneut verarbeitet.", error);
        Assert.True(wasError);
        Assert.Equal("Erledigt", detail.StatusText);
        Assert.False(detail.CanDismiss);
        Assert.False(detail.ShowReply);
        Assert.Equal("Als erledigt markiert.", detail.Notice);
    }

    [Fact]
    public async Task Warteliste_steht_in_der_Seitenleiste_mit_Zeitraum_und_Stand()
    {
        WaitlistStaffEntry Entry(string name, int day, DayPart part, int offers = 0) =>
            new(Guid.NewGuid(), Guid.NewGuid(), name, TestData.MassageTreatment.Id, "Massage", TestData.Monday.AddDays(day),
                TestData.Monday.AddDays(day + 4), part, WaitlistStatus.Waiting, TestData.At(8), offers, null);
        var api = new StubApi()
            .On("GET /api/locations", new List<LocationDto> { TestData.Location })
            .On($"GET /api/locations/{TestData.LocationId}/resources", TestData.Resources)
            .On($"GET /api/locations/{TestData.LocationId}/calendar?date=2027-03-01", TestData.Day())
            .On($"GET /api/locations/{TestData.LocationId}/calendar?date=2027-03-03", TestData.Day())
            .On($"GET /api/locations/{TestData.LocationId}/waitlist", new List<WaitlistStaffEntry>
            {
                Entry("Eva Muster", 2, DayPart.Morning, offers: 2), Entry("Paul Huber", 3, DayPart.Any), Entry("Ida Berger", 5, DayPart.Evening),
                Entry("Otto Lang", 6, DayPart.Afternoon),
            });
        var vm = new CalendarViewModel(api.Client(), TestData.Reception(), null, new FixedTime());
        await vm.InitializeAsync();
        await vm.LoadAsync();

        vm.OpenWaitlistCommand.Execute(null);
        var panel = vm.WaitlistPanel!;

        Assert.Equal("WARTELISTE · 4", vm.WaitlistTitle);
        Assert.Equal(3, vm.WaitlistPreview.Count);
        Assert.Equal("und 1 weitere", vm.WaitlistMore);
        Assert.Equal("Mi. 3.3. bis So. 7.3., vormittags", panel.Rows[0].Period);
        Assert.Equal("2× freien Termin gemeldet", panel.Rows[0].State);
        Assert.Equal("Wartet", panel.Rows[1].State);
        Assert.True(vm.HasPanel);
    }
}
