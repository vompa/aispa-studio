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

    private static async Task<(InboxDetailViewModel Detail, StubApi Api)> OpenAsync(InboundMailDto mail, Action<StubApi>? prepare = null)
    {
        var api = new StubApi()
            .On("GET /api/locations", new List<LocationDto> { SpaSample.Vienna })
            .On("GET /api/inbox", new List<InboundMailSummary> { SpaSample.Inbox()[0] })
            .On(MailRoute, mail);
        prepare?.Invoke(api);
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
    public async Task Hat_der_Gast_mehrere_Termine_waehlt_die_Rezeption_den_spaeteren_und_die_Rueckfrage_nennt_ihn()
    {
        var found = Guid.NewGuid();
        var later = Guid.NewGuid();
        var mail = SpaSample.InboxMail() with
        {
            Status = InboundMailStatus.NeedsReview,
            Kind = InboundMailKind.Cancellation,
            Proposals = [],
            AppointmentId = found,
            CanRepropose = false,
            CanCancelAppointment = true,
        };
        var (detail, api) = await OpenAsync(mail, stub => stub.On($"GET /api/inbox/{SpaSample.InboxMailId}/cancellable-appointments", new List<InboxAppointmentOption>
        {
            new(found, SpaSample.At(17), "Hot-Stone-Massage", "Wien", "Europe/Vienna", true),
            new(later, SpaSample.At(10).AddDays(7), "Aromaöl-Massage", "Wien", "Europe/Vienna", false),
        }));
        for (var i = 0; i < 100 && detail.CancelOptions.Count == 0; i++)
        {
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        var route = $"POST /api/inbox/{SpaSample.InboxMailId}/cancel-appointment?appointmentId={later}";
        api.On(route, mail with { Status = InboundMailStatus.Cancelled, CanCancelAppointment = false, AppointmentId = later });

        // Vorgewählt ist der gefundene, nächste Termin.
        Assert.True(detail.HasCancelChoice);
        Assert.Equal(found, detail.SelectedCancelOption!.Option.Id);
        Assert.EndsWith("(nächster Termin)", detail.SelectedCancelOption.Text, StringComparison.Ordinal);
        detail.SelectedCancelOption = detail.CancelOptions[1];
        var dialogs = new FakeDialogs().Answer(ConfirmChoice.Confirmed);
        detail.Dialogs = dialogs;
        await detail.AskCancelAppointmentCommand.ExecuteAsync(null);

        Assert.Contains("Aromaöl-Massage", dialogs.Asked[0].Message, StringComparison.Ordinal);
        Assert.Single(api.Calls, c => c.Route == route);
        // Nach dem Storno gibt es nichts mehr zu wählen.
        Assert.False(detail.HasCancelChoice);
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

    [Fact]
    public async Task Verschiebungswunsch_zeigt_freie_Zeiten_und_verschiebt_erst_nach_der_Rueckfrage()
    {
        var slot = new AvailableSlot(SpaSample.At(14), SpaSample.At(15), SpaSample.Elif.Id, SpaSample.Hamam.Id, null);
        var mail = SpaSample.InboxMail() with
        {
            Status = InboundMailStatus.NeedsReview,
            Kind = InboundMailKind.Reschedule,
            Proposals = [slot],
            AppointmentId = Guid.NewGuid(),
            CanRepropose = false,
            CanMoveAppointment = true,
            Note = "Gast möchte verschieben. Nächster Termin: Hot-Stone-Massage am 22.10.2026 um 17:00 Uhr, Wien.",
        };
        var (detail, api) = await OpenAsync(mail);
        var route = $"POST /api/inbox/{SpaSample.InboxMailId}/move-appointment";
        api.On(route, mail with { Status = InboundMailStatus.Booked, CanMoveAppointment = false, Proposals = [], Note = null });

        Assert.True(detail.CanMoveAppointment);
        var option = Assert.Single(detail.MoveOptions);
        Assert.Equal("Auf Mo. 19.10. 14:00 verschieben", option.Text);
        var dialogs = new FakeDialogs().Answer(ConfirmChoice.Cancelled).Answer(ConfirmChoice.Confirmed);
        detail.Dialogs = dialogs;
        await detail.AskMoveAppointmentCommand.ExecuteAsync(option);
        Assert.DoesNotContain(api.Calls, c => c.Route == route);
        Assert.Equal("Termin verschieben?", dialogs.Asked[0].Title);
        dialogs.AssertRules();
        await detail.AskMoveAppointmentCommand.ExecuteAsync(option);

        var call = Assert.Single(api.Calls, c => c.Route == route);
        Assert.Contains(slot.Start.ToString("yyyy-MM-ddTHH:mm", System.Globalization.CultureInfo.InvariantCulture), call.Body, StringComparison.Ordinal);
        Assert.False(detail.CanMoveAppointment);
        Assert.Empty(detail.MoveOptions);
        Assert.Equal("Termin auf Mo. 19.10. 14:00 verschoben. Die Antwort wird neu entworfen.", detail.Notice);
    }
}
