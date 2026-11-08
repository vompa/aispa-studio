using AispaStudio.Contracts;
using AispaStudio.Desktop.ViewModels;

namespace AispaStudio.Desktop.Tests;

/// <summary>Posteingang: Absage des Gasts storniert den gefundenen Termin mit einem Klick.</summary>
public sealed class InboxReproposeTests
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
    public async Task Absage_storniert_den_gefundenen_Termin_mit_einem_Klick()
    {
        var mail = SpaSample.InboxMail() with
        {
            Status = InboundMailStatus.NeedsReview,
            Kind = InboundMailKind.Cancellation,
            Proposals = [],
            AppointmentId = Guid.NewGuid(),
            CanRepropose = false,
            CanCancelAppointment = true,
            Note = "Gast möchte absagen. Nächster Termin: Hot-Stone-Massage am 22.10.2026 um 17:00 Uhr, Wien.",
        };
        var (detail, api) = await OpenAsync(mail);
        var route = $"POST /api/inbox/{SpaSample.InboxMailId}/cancel-appointment";
        api.On(route, mail with { Status = InboundMailStatus.Cancelled, CanCancelAppointment = false, Note = null });

        Assert.True(detail.CanCancelAppointment);
        var dialogs = new FakeDialogs().Answer(ConfirmChoice.Cancelled).Answer(ConfirmChoice.Confirmed);
        detail.Dialogs = dialogs;
        await detail.AskCancelAppointmentCommand.ExecuteAsync(null);
        Assert.DoesNotContain(api.Calls, c => c.Route == route);
        Assert.Equal("Termin stornieren?", dialogs.Asked[0].Title);
        dialogs.AssertRules();
        await detail.AskCancelAppointmentCommand.ExecuteAsync(null);

        Assert.Single(api.Calls, c => c.Route == route);
        Assert.False(detail.CanCancelAppointment);
        Assert.Equal("Termin storniert. Die Antwort wird neu entworfen.", detail.Notice);
    }
}
