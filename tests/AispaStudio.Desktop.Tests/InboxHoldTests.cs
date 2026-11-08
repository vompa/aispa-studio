using AispaStudio.Contracts;
using AispaStudio.Desktop.ViewModels;

namespace AispaStudio.Desktop.Tests;

/// <summary>Posteingang aus Sicht der Rezeption (Auftraggeber, 29.10.2026, ADR 0083): freie Zeit rechts, vormerken und vorschlagen, bestätigen, Erinnerung im Kalender.</summary>
public sealed class InboxHoldTests
{
    private static readonly TimeZoneInfo Vienna = TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna");

    private sealed class FixedTime : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => TestData.At(10);

        public override TimeZoneInfo LocalTimeZone => Vienna;
    }

    private static readonly AvailableSlot Slot = new(TestData.At(14), TestData.At(14, 50), TestData.Anna.Id, TestData.Room.Id, null);

    /// <summary>Offene Terminanfrage ohne Termin am Teststandort, ohne Vorschläge der KI.</summary>
    private static InboundMailDto Request() => SpaSample.InboxMail() with
    {
        LocationId = TestData.LocationId,
        TreatmentId = TestData.MassageTreatment.Id,
        Status = InboundMailStatus.NeedsReview,
        Proposals = [],
        CanRepropose = true,
    };

    private static InboundMailDto Held() => Request() with
    {
        Status = InboundMailStatus.Proposed,
        AppointmentId = Guid.NewGuid(),
        HeldStart = Slot.Start,
        CanRepropose = false,
    };

    private static StubApi Api() => new StubApi()
        .On($"GET /api/locations/{TestData.LocationId}/resources", TestData.Resources)
        .On($"GET /api/locations/{TestData.LocationId}/availability?treatmentId={TestData.MassageTreatment.Id}&date=2027-03-01&minSlots=3&days=14",
            new List<AvailableSlot> { Slot });

    private static InboxDetailViewModel Detail(StubApi api, InboundMailDto mail) =>
        new(api.Client(), mail, Vienna, TimeSpan.Zero, () => Task.CompletedTask, () => { }, [TestData.Location], new FixedTime());

    private static async Task Until(Func<bool> condition)
    {
        for (var i = 0; i < 300 && !condition(); i++)
        {
            await Task.Delay(10);
        }

        Assert.True(condition());
    }

    [Fact]
    public async Task Rechts_steht_die_freie_Zeit_und_ein_Klick_merkt_vor_und_schlaegt_vor()
    {
        var api = Api().On($"POST /api/inbox/{SpaSample.InboxMailId}/hold", Held());
        var detail = Detail(api, Request());

        Assert.True(detail.ShowFreeTime);
        Assert.False(detail.HasLocationChoice);
        Assert.Equal(TestData.LocationId, detail.FreeTimeLocation?.Id);
        await Until(() => detail.FreeTime is { HasSlots: true });
        var freeTime = detail.FreeTime!;
        Assert.Equal(InboxDetailViewModel.HoldLabel, freeTime.BookText);
        Assert.False(freeTime.ShowGuest);
        Assert.Equal(TestData.MassageTreatment.Id, freeTime.Treatment?.Id);

        await freeTime.BookActiveCommand.ExecuteAsync(null);

        var call = Assert.Single(api.Calls, c => c.Route == $"POST /api/inbox/{SpaSample.InboxMailId}/hold");
        Assert.Contains(TestData.MassageTreatment.Id.ToString(), call.Body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(TestData.Anna.Id.ToString(), call.Body, StringComparison.OrdinalIgnoreCase);
        Assert.False(detail.ShowFreeTime);
        Assert.Null(detail.FreeTime);
        Assert.True(detail.HasHold);
        Assert.StartsWith("Vorgemerkt: ", detail.HoldText, StringComparison.Ordinal);
        Assert.Equal("Vorgemerkt. Der Vorschlag steht im Entwurf: prüfen und senden.", detail.Notice);
    }

    [Fact]
    public async Task Bestaetigen_bucht_fest_und_danach_gibt_es_nichts_mehr_zu_entscheiden()
    {
        var held = Held();
        var appointment = held.AppointmentId!.Value;
        var api = Api()
            .On($"POST /api/appointments/{appointment}/hold/confirm", SpaSample.Selected)
            .On($"GET /api/inbox/{SpaSample.InboxMailId}", held with { Status = InboundMailStatus.Booked, HeldStart = null });
        var detail = Detail(api, held);
        Assert.True(detail.HasHold);
        Assert.Equal("Die Zeit bleibt frei, bis Sie bestätigen oder freigeben.", detail.HoldDetail);
        Assert.True(detail.ConfirmHoldCommand.CanExecute(null));

        await detail.ConfirmHoldCommand.ExecuteAsync(null);

        Assert.Contains(api.Calls, c => c.Route == $"POST /api/appointments/{appointment}/hold/confirm");
        Assert.False(detail.HasHold);
        Assert.Equal("Fest gebucht. Die Bestätigung an den Gast wird entworfen.", detail.Notice);
        Assert.False(detail.ConfirmHoldCommand.CanExecute(null));
        Assert.False(detail.ReleaseHoldCommand.CanExecute(null));
    }

    [Fact]
    public void Die_Antwort_des_Gastes_auf_eine_Vormerkung_sagt_was_zu_tun_ist()
    {
        var answer = Held() with { PreviousMailId = Guid.NewGuid(), Status = InboundMailStatus.NeedsReview };
        var detail = Detail(Api(), answer);
        Assert.True(detail.HasHold);
        Assert.False(detail.ShowFreeTime);
        Assert.Equal("Der Gast hat geantwortet. Passt es, bestätigen; sonst freigeben.", detail.HoldDetail);
    }

    [Fact]
    public async Task Kalender_erinnert_ab_dem_Zeitpunkt_des_Servers_und_fuehrt_zur_Anfrage()
    {
        var mailId = Guid.NewGuid();
        var due = new AppointmentHoldDto(Guid.NewGuid(), mailId, "Petra Haas", "Massage", TestData.At(17).AddDays(1), TestData.At(9));
        var later = new AppointmentHoldDto(Guid.NewGuid(), Guid.NewGuid(), "Lukas Berg", "Massage", TestData.At(17).AddDays(3), TestData.At(12));
        var api = new StubApi()
            .On("GET /api/locations", new List<LocationDto> { TestData.Location })
            .On($"GET /api/locations/{TestData.LocationId}/resources", TestData.Resources)
            .On($"GET /api/locations/{TestData.LocationId}/calendar?date=2027-03-01", TestData.Day())
            .On($"GET /api/locations/{TestData.LocationId}/appointment-holds", new List<AppointmentHoldDto> { due, later });
        var calendar = new CalendarViewModel(api.Client(), TestData.Reception(), null, new FixedTime());
        Guid? opened = null;
        calendar.InboxRequested = id =>
        {
            opened = id;
            return Task.CompletedTask;
        };
        await calendar.InitializeAsync();
        await calendar.LoadAsync();

        var hold = Assert.Single(calendar.Messages, m => m.Kind == MessageKind.Hold);
        Assert.Equal(MessageSeverity.Warning, hold.Severity);
        Assert.Equal("Gast hat sich nicht gemeldet", hold.Title);
        Assert.StartsWith("Petra Haas: Massage, ", hold.Text, StringComparison.Ordinal);
        Assert.Equal("Vormerkung", hold.KindText);
        Assert.Equal("Zur Anfrage", hold.ActionLabel);
        hold.ActionCommand!.Execute(null);
        Assert.Equal(mailId, opened);
    }
}
