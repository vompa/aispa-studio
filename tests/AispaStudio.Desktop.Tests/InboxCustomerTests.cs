using AispaStudio.Contracts;
using AispaStudio.Desktop.ViewModels;

namespace AispaStudio.Desktop.Tests;

/// <summary>Posteingang: „Wer ist das?“ mit Vorschlägen, Zuordnen, Neu anlegen und Lösen; weitere Mailadressen („Schon vorhanden?“ der Kundenmaske: CustomerNewFormTests).</summary>
public sealed class InboxCustomerTests
{
    private static readonly string MailRoute = $"GET /api/inbox/{SpaSample.InboxMailId}";
    private static readonly string AssignRoute = $"POST /api/inbox/{SpaSample.InboxMailId}/customer/assign";

    private sealed class FixedTime : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => SpaSample.At(12);
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
    public async Task Vorschlaege_stehen_mit_Grund_da_und_ohne_Vorschlaege_fehlt_der_Abschnitt()
    {
        var (detail, _) = await OpenAsync(SpaSample.InboxMailWhoIsThis());

        Assert.True(detail.HasCandidates);
        Assert.Equal(["Claudia Haider", "Claudia Haidinger"], detail.Candidates.Select(c => c.Name));
        Assert.Equal("gleicher Name, Telefon endet auf 4567, letzter Besuch 14.09.2026", detail.Candidates[0].Reason);
        Assert.Equal("Name und ein weiteres Merkmal passen", detail.Candidates[0].StageText);
        Assert.Equal("Nur der Name ist ähnlich", detail.Candidates[1].StageText);
        Assert.False(detail.HasAssigned);

        var (plain, _) = await OpenAsync(SpaSample.InboxMail());
        Assert.False(plain.HasCandidates);
    }

    [Fact]
    public async Task Zuordnen_schickt_den_Kunden_und_das_Haekchen_und_zeigt_danach_die_Zuordnung()
    {
        var (detail, api) = await OpenAsync(SpaSample.InboxMailWhoIsThis());
        var assigned = SpaSample.InboxMail() with { CustomerId = SpaSample.HaiderId, CustomerConfirmed = true, CustomerCandidates = [] };
        api.On(AssignRoute, assigned)
            .On($"GET /api/customers/{SpaSample.HaiderId}", new CustomerDto(SpaSample.HaiderId, "Claudia", "Haider", "c.haider@example.at", null, null, null));

        detail.RememberContact = true;
        await detail.Candidates[0].AssignCommand.ExecuteAsync(null);

        var call = api.Calls.Single(c => c.Route == AssignRoute);
        Assert.Contains($"\"customerId\":\"{SpaSample.HaiderId}\"", call.Body, StringComparison.Ordinal);
        Assert.Contains("\"rememberContact\":true", call.Body, StringComparison.Ordinal);
        Assert.False(detail.HasCandidates);
        Assert.True(detail.HasAssigned);
        Assert.True(detail.CanReleaseCustomer);
        for (var i = 0; i < 100 && detail.AssignedName is null; i++)
        {
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        Assert.Equal("Zugeordnet: Claudia Haider", detail.AssignedText);
        Assert.Equal("Zugeordnet: Claudia Haider.", detail.Notice);
        Assert.False(detail.NoticeIsError);
    }

    [Fact]
    public async Task Neu_anlegen_und_Zuordnung_loesen()
    {
        var (detail, api) = await OpenAsync(SpaSample.InboxMailWhoIsThis());
        var created = SpaSample.InboxMail() with { CustomerId = Guid.NewGuid(), CustomerConfirmed = true, CustomerCandidates = [] };
        api.On($"POST /api/inbox/{SpaSample.InboxMailId}/customer/new", created);

        await detail.CreateCustomerCommand.ExecuteAsync(null);

        Assert.Contains(api.Calls, c => c.Route == $"POST /api/inbox/{SpaSample.InboxMailId}/customer/new");
        Assert.True(detail.HasAssigned);
        Assert.Equal("Kunde neu angelegt und zugeordnet.", detail.Notice);

        api.On($"POST /api/inbox/{SpaSample.InboxMailId}/customer/release", SpaSample.InboxMailWhoIsThis());
        await detail.ReleaseCustomerCommand.ExecuteAsync(null);

        Assert.False(detail.HasAssigned);
        Assert.True(detail.HasCandidates);
        Assert.Equal("Zuordnung gelöst.", detail.Notice);
    }

    [Fact]
    public async Task Ohne_Verbindung_gibt_es_einen_verstaendlichen_Hinweis_und_nichts_wird_vorgemerkt()
    {
        var (detail, api) = await OpenAsync(SpaSample.InboxMailWhoIsThis());
        api.Down = true;

        await detail.Candidates[0].AssignCommand.ExecuteAsync(null);

        Assert.True(detail.NoticeIsError);
        Assert.StartsWith("Ohne Verbindung zum Server nicht möglich.", detail.Notice, StringComparison.Ordinal);
        Assert.True(detail.HasCandidates);
    }

    [Fact]
    public async Task Weitere_Mailadressen_anzeigen_hinzufuegen_und_entfernen_und_Herkunft_aus_der_Nachricht()
    {
        var customer = SpaSample.Customers[0] with { CreatedFromChannel = nameof(BookingChannel.Email) };
        var privat = new CustomerEmailDto(Guid.NewGuid(), "brigitte@privat.example");
        var firma = new CustomerEmailDto(Guid.NewGuid(), "b.mueller@firma.example");
        var stub = new StubApi()
            .On("GET /api/customers?search=", new List<CustomerDto> { customer })
            .On($"GET /api/customers/{customer.Id}/appointments", new List<AppointmentDto>())
            .On($"GET /api/customers/{customer.Id}/emails", new List<CustomerEmailDto> { privat })
            .On($"POST /api/customers/{customer.Id}/emails", new List<CustomerEmailDto> { privat, firma })
            .On($"DELETE /api/customers/{customer.Id}/emails/{privat.Id}", new List<CustomerEmailDto> { firma });
        var customers = new CustomersViewModel(stub.Client(), TimeProvider.System);
        await customers.InitializeAsync();
        customers.Selected = customers.Results[0];
        for (var i = 0; i < 100 && customers.OtherEmails.Count == 0; i++)
        {
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        Assert.Equal("Aus Mail angelegt, bitte Angaben prüfen", customers.CreatedFromText);
        Assert.Equal(["brigitte@privat.example"], customers.OtherEmails.Select(e => e.Address));

        Assert.False(customers.AddEmailCommand.CanExecute(null));
        customers.NewOtherEmail = " b.mueller@firma.example ";
        await customers.AddEmailCommand.ExecuteAsync(null);
        Assert.Contains("\"address\":\"b.mueller@firma.example\"", stub.Calls.Last(c => c.Route.StartsWith("POST ", StringComparison.Ordinal)).Body, StringComparison.Ordinal);
        Assert.Equal(2, customers.OtherEmails.Count);
        Assert.Equal("", customers.NewOtherEmail);

        await customers.OtherEmails[0].RemoveCommand.ExecuteAsync(null);
        Assert.Equal(["b.mueller@firma.example"], customers.OtherEmails.Select(e => e.Address));

        stub.Down = true;
        customers.NewOtherEmail = "x@y.example";
        await customers.AddEmailCommand.ExecuteAsync(null);
        Assert.StartsWith("Ohne Verbindung zum Server nicht möglich.", customers.Error, StringComparison.Ordinal);
    }
}
