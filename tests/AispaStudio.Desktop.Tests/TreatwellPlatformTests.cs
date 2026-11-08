using System.Globalization;
using AispaStudio.Contracts;
using AispaStudio.Desktop.ViewModels;

namespace AispaStudio.Desktop.Tests;

/// <summary>
/// Treatwell-Buchungen in der Windows-App (ADR 0062): vorab bezahlt oder vor Ort, „In Treatwell noch annehmen“ mit „Erledigt“, „Zuordnung offen“
/// im Termin und im Posteingang, und die Kassa schlägt den vorab bezahlten Betrag vor. Die Beispiele sind erfunden, nicht von Treatwell.
/// </summary>
public sealed class TreatwellPlatformTests
{
    private static readonly string MailRoute = $"GET /api/inbox/{SpaSample.InboxMailId}";

    private sealed class FixedTime : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => SpaSample.At(12);
    }

    private sealed class SampleTime : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => SpaSample.At(11, 40);

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna");
    }

    private static async Task<(InboxDetailViewModel Detail, StubApi Api)> OpenMailAsync(InboundMailDto mail)
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
    public async Task Posteingang_zeigt_vorab_bezahlt_Annehmen_in_Treatwell_und_Wer_ist_das_bei_gebuchtem_Termin()
    {
        var (detail, _) = await OpenMailAsync(SpaSample.InboxMailTreatwell());

        Assert.Equal("Vorab über Treatwell bezahlt: 35,00 €", detail.PaymentText);
        Assert.True(detail.ShowAcceptPending);
        Assert.Contains("TW-9823711", detail.AcceptPendingText, StringComparison.Ordinal);
        Assert.True(detail.AssignmentOpen);
        Assert.True(detail.HasCandidates);
        Assert.Equal("Als neuen Kunden bestätigen", detail.CreateCustomerText);
        Assert.Contains("Der Termin steht schon im Kalender", detail.CandidatesHint, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Erledigt_vermerkt_die_Annahme_in_Treatwell_und_der_Hinweis_verschwindet()
    {
        var (detail, api) = await OpenMailAsync(SpaSample.InboxMailTreatwell());
        api.On($"POST /api/inbox/{SpaSample.InboxMailId}/platform-accepted", SpaSample.InboxMailTreatwell() with { AcceptInPlatformPending = false });

        await detail.PlatformAcceptedCommand.ExecuteAsync(null);

        Assert.Contains(api.Calls, c => c.Route == $"POST /api/inbox/{SpaSample.InboxMailId}/platform-accepted");
        Assert.False(detail.ShowAcceptPending);
        Assert.Equal("Als in Treatwell angenommen vermerkt.", detail.Notice);
    }

    [Fact]
    public async Task Zuordnen_haengt_den_Termin_auf_den_Kunden_um_und_der_Abschnitt_verschwindet()
    {
        var (detail, api) = await OpenMailAsync(SpaSample.InboxMailTreatwell());
        api.On($"POST /api/inbox/{SpaSample.InboxMailId}/customer/assign",
                SpaSample.InboxMailTreatwell() with { CustomerId = SpaSample.HaiderId, CustomerConfirmed = true, CustomerCandidates = [], CustomerAssignmentOpen = false, Note = null })
            .On($"GET /api/customers/{SpaSample.HaiderId}", new CustomerDto(SpaSample.HaiderId, "Claudia", "Haider", "c.haider@example.at", null, null, null));

        await detail.Candidates[0].AssignCommand.ExecuteAsync(null);

        Assert.False(detail.AssignmentOpen);
        Assert.False(detail.HasCandidates);
        Assert.True(detail.HasAssigned);
        Assert.Equal("Zugeordnet: Claudia Haider.", detail.Notice);
    }

    [Fact]
    public async Task Eine_Nachricht_ohne_Treatwell_zeigt_weder_Zahlung_noch_Annehmen()
    {
        var (detail, _) = await OpenMailAsync(SpaSample.InboxMail());

        Assert.False(detail.HasPaymentText);
        Assert.False(detail.ShowAcceptPending);
        Assert.False(detail.AssignmentOpen);
        Assert.Equal("Neu anlegen", detail.CreateCustomerText);
    }

    [Fact]
    public async Task Terminpanel_zeigt_Zahlung_und_Hinweise_und_Erledigt_ruft_den_Server()
    {
        var api = new StubApi();
        AppointmentDto? changed = null;
        var appointment = SpaSample.TreatwellAppointment();
        api.On($"POST /api/appointments/{appointment.Id}/platform-accepted", appointment with { AcceptInPlatformPending = false });
        var panel = new AppointmentViewModel(api.Client(), appointment, "Lena Huber", "Raum Lotus", TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna"),
            true, true, a =>
            {
                changed = a;
                return Task.CompletedTask;
            }, () => { });

        Assert.Equal("Vorab über Treatwell bezahlt: 35,00 €", panel.PaymentText);
        Assert.True(panel.ShowAcceptPending);
        Assert.True(panel.ShowAssignmentOpen);

        await panel.PlatformAcceptedCommand.ExecuteAsync(null);

        Assert.Contains(api.Calls, c => c.Route == $"POST /api/appointments/{appointment.Id}/platform-accepted");
        Assert.False(changed!.AcceptInPlatformPending);
    }

    [Fact]
    public void Hover_Karte_nennt_Zahlung_Annehmen_und_offene_Zuordnung()
    {
        var onSite = TestData.Appointment(14, TestData.Anna) with { Source = AppointmentSource.Treatwell, PlatformPayment = PlatformPayment.OnSite };
        var prepaid = TestData.Appointment(10, TestData.Anna) with
        {
            Source = AppointmentSource.Treatwell,
            PlatformPayment = PlatformPayment.Prepaid,
            PrepaidAmount = 35m,
            AcceptInPlatformPending = true,
            CustomerAssignmentOpen = true,
        };
        var layout = DayLayout.Build(TestData.Day(prepaid, onSite), TestData.Resources, byRoom: false, TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna"),
            now: TestData.At(9));

        var first = layout.Columns[0].Items.Single(i => i.Id == prepaid.Id);
        var second = layout.Columns[0].Items.Single(i => i.Id == onSite.Id);

        Assert.Contains("Zahlung: Vorab über Treatwell bezahlt: 35,00 €", first.HoverText, StringComparison.Ordinal);
        Assert.Contains("In Treatwell noch annehmen", first.HoverText, StringComparison.Ordinal);
        Assert.Contains("Zuordnung offen", first.HoverText, StringComparison.Ordinal);
        Assert.Contains("Zahlung: Zahlt vor Ort", second.HoverText, StringComparison.Ordinal);
        Assert.DoesNotContain("In Treatwell noch annehmen", second.HoverText, StringComparison.Ordinal);
    }

    private static StubApi PosApi(BillDto bill) => new StubApi()
        .On("GET /api/locations", new List<LocationDto> { SpaSample.Vienna })
        .On("GET /api/pos/registers", new List<CashRegisterDto> { SpaSample.Register with { PaymentMethods = [.. SpaSample.Register.PaymentMethods, PaymentMethod.Treatwell] } })
        .On("GET /api/pos/articles", SpaSample.Articles)
        .On($"GET /api/pos/locations/{SpaSample.LocationId}/day-list?date=2026-10-19", SpaSample.DayList())
        .On($"GET /api/pos/registers/{SpaSample.Register.Id}/bills", new List<BillDto>())
        .On("POST /api/pos/bills", bill)
        .On($"POST /api/pos/bills/{SpaSample.BillId}/checkout", SpaSample.Receipt());

    [Fact]
    public async Task Kassa_schlaegt_den_vorab_ueber_Treatwell_bezahlten_Betrag_vor_und_schickt_ihn_als_Zahlungsart()
    {
        var bill = SpaSample.Bill(withArticle: false) with { PlatformPrepaid = 35m, PlatformReference = "TW-9823711" };
        var api = PosApi(bill);
        var pos = new PosViewModel(api.Client(), new SampleTime());
        await pos.InitializeAsync(SpaSample.LocationId);
        await pos.AddAppointmentAsync(SpaSample.Paying);

        var treatwell = pos.Payments.Single(p => p.Method == PaymentMethod.Treatwell);
        Assert.True(treatwell.IsOffered);
        Assert.Equal("Über Treatwell bezahlt", treatwell.Title);
        Assert.Equal("35,00", treatwell.Amount);
        Assert.Contains("Vorab über Treatwell bezahlt: 35,00 € (Buchung TW-9823711)", pos.PlatformPrepaidText, StringComparison.Ordinal);
        Assert.Contains("nicht noch einmal vom Gast verlangt", pos.PlatformPrepaidText, StringComparison.Ordinal);
        Assert.StartsWith("Noch offen:", pos.CoverText, StringComparison.Ordinal);

        pos.Payments.Single(p => p.Method == PaymentMethod.Cash).Amount = (bill.Open - 35m).ToString("0.00", CultureInfo.GetCultureInfo("de-AT"));
        Assert.Equal("Alles gedeckt", pos.CoverText);
        await pos.PayCommand.ExecuteAsync(null);

        var body = api.Calls.Last(c => c.Route == $"POST /api/pos/bills/{SpaSample.BillId}/checkout").Body!;
        Assert.Contains("\"method\":\"Treatwell\",\"amount\":35", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Ohne_Vorauszahlung_erscheint_die_Zahlungsart_Treatwell_nicht()
    {
        var api = PosApi(SpaSample.Bill(withArticle: false));
        var pos = new PosViewModel(api.Client(), new SampleTime());
        await pos.InitializeAsync(SpaSample.LocationId);
        await pos.AddAppointmentAsync(SpaSample.Paying);

        var treatwell = pos.Payments.Single(p => p.Method == PaymentMethod.Treatwell);
        Assert.False(treatwell.IsOffered);
        Assert.Equal("", treatwell.Amount);
        Assert.Equal("", pos.PlatformPrepaidText);
    }
}
