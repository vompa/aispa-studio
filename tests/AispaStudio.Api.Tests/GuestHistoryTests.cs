using AispaStudio.Api.Assistant.Answers;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Tests;

/// <summary>
/// Verlauf eines Gastes im Assistenten (Ä317): „Historie Christian Steiner“ zeigt Besuche, den nächsten Termin, Nichterscheinen und Absagen und den Sprung in die
/// Kundenkarte statt eines Hilfethemas; die Hilfe steht nur ergänzend darunter. Therapeut:innen sehen nur eigene Termine. „Jetzt“ ist Mittwoch, 03.03.2027, 11:00 Uhr;
/// das Team arbeitet montags.
/// </summary>
public sealed class GuestHistoryTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly AnswerKit _kit = new(fixture);

    private static Task<CustomerDto> SteinerAsync(TestSpa spa) =>
        spa.PostAsync<CustomerDto>("/api/customers", new CustomerInput("Christian", "Steiner", null, null, null, null, Salutation: CustomerSalutation.Male), Ct);

    private static Task<AppointmentDto> BookAsync(TestSpa spa, CustomerDto guest, DateOnly day, int hour, EmployeeDto employee) =>
        spa.BookOkAsync(TestSpa.At(hour, date: day), Ct, employee.Id, customer: guest.Id);

    private static Task<AppointmentDto> StatusAsync(TestSpa spa, AppointmentDto appointment, AppointmentStatus status) =>
        spa.SetStatusAsync(appointment.Id, status, Ct);

    /// <summary>Ein Besuch an einem vergangenen Montag, eingecheckt.</summary>
    private static async Task VisitAsync(TestSpa spa, CustomerDto guest, DateOnly day, EmployeeDto employee) =>
        await StatusAsync(spa, await BookAsync(spa, guest, day, 10, employee), AppointmentStatus.CheckedIn);

    [Fact]
    public async Task Historie_nennt_drei_Besuche_naechsten_Termin_und_Kundenkarte()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var steiner = await SteinerAsync(spa);
        await VisitAsync(spa, steiner, TestSpa.Monday.AddDays(-14), spa.Anna);
        await VisitAsync(spa, steiner, TestSpa.Monday.AddDays(-7), spa.Ben);
        await VisitAsync(spa, steiner, TestSpa.Monday, spa.Anna);
        await BookAsync(spa, steiner, TestSpa.Monday.AddDays(7), 14, spa.Ben);

        var block = await _kit.AnswerAsync(spa, "historie christian steiner");

        Assert.Equal("Christian Steiner: 3 Besuche, zuletzt am 01.03.2027.", block.Text);
        var card = block.Items![0];
        Assert.Equal(("Kundenkarte öffnen", AssistantSuggestionKind.OpenCustomer, steiner.Id), (card.Action, card.Suggestion!.Kind, card.Suggestion.CustomerId));
        Assert.Equal(("Nächster Termin: Montag, 08.03.2027", "14:00", "Massage · Ben"), (block.Items[1].Title, block.Items[1].Lead, block.Items[1].Detail));
        Assert.Equal(["Montag, 01.03.2027", "Montag, 22.02.2027", "Montag, 15.02.2027"], block.Items.Skip(2).Select(i => i.Title));
        Assert.Equal(steiner.Id, block.GuestId);
    }

    [Fact]
    public async Task Historie_zaehlt_Nichterscheinen_und_Absagen()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var steiner = await SteinerAsync(spa);
        await VisitAsync(spa, steiner, TestSpa.Monday.AddDays(-14), spa.Anna);
        await StatusAsync(spa, await BookAsync(spa, steiner, TestSpa.Monday.AddDays(-7), 10, spa.Anna), AppointmentStatus.NoShow);
        var cancelled = await BookAsync(spa, steiner, TestSpa.Monday, 10, spa.Anna);
        await spa.PostAsync<AppointmentDto>($"/api/appointments/{cancelled.Id}/cancel", new CancelAppointmentRequest("Krank"), Ct);

        var block = await _kit.AnswerAsync(spa, "historie christian steiner");

        Assert.Equal("Christian Steiner: 1 Besuch, zuletzt am 15.02.2027.", block.Text);
        var titles = block.Items!.Select(i => i.Title).ToList();
        Assert.Contains("Nicht erschienen: 1", titles);
        Assert.Contains("Abgesagt: 1", titles);
    }

    [Fact]
    public async Task Historie_zeigt_Hilfethemen_unter_dem_Verlauf()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var steiner = await SteinerAsync(spa);
        await VisitAsync(spa, steiner, TestSpa.Monday, spa.Anna);

        var answer = await _kit.StaffAsync(spa, new AssistantQuestion("Historie Christian Steiner", null));

        Assert.Equal((nameof(BusinessTopic.GuestHistory), "Christian Steiner: 1 Besuch, zuletzt am 01.03.2027."), (answer.Block!.Topic, answer.Block.Text));
        Assert.InRange(answer.Help.Count, 1, 2);
    }

    [Fact]
    public async Task Therapeutin_sieht_im_Verlauf_nur_eigene_Termine()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var steiner = await SteinerAsync(spa);
        await VisitAsync(spa, steiner, TestSpa.Monday.AddDays(-7), spa.Ben);
        await VisitAsync(spa, steiner, TestSpa.Monday, spa.Anna);

        var block = await _kit.AnswerAsync(spa, "historie christian steiner", role: StaffRole.Therapist, employeeId: spa.Ben.Id);

        Assert.Equal("Christian Steiner, Ihre Termine: 1 Besuch, zuletzt am 22.02.2027.", block.Text);
        Assert.Equal(["Montag, 22.02.2027"], block.Items!.Where(i => i.Lead is not null).Select(i => i.Title));
    }
}
