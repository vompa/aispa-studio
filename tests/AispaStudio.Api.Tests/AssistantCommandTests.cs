using System.Net.Http.Json;
using AispaStudio.Api.Assistant;
using AispaStudio.Api.Data;
using AispaStudio.Api.Tenancy;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AispaStudio.Api.Tests;

/// <summary>
/// Aufträge an den Assistenten (Ä319, Slice 147): „buche“, „verschiebe“, „sag … ab“, „schick … den bogen“, „biete … an“ und „offene rechnung“ liefern einen Vorschlag,
/// mit dem die App die passende Maske vorausgefüllt öffnet. Gespeichert wird nichts; fehlt eine Angabe, bleibt das Feld leer. „Jetzt“ ist Mittwoch, 3. März 2027, 11:00 Uhr.
/// </summary>
public sealed class AssistantCommandTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly DateOnly Tomorrow = new(2027, 3, 4);

    private static readonly DateOnly NextMonday = TestSpa.Monday.AddDays(7);

    private static readonly AssistantSuggestionKind[] MaskKinds =
    [
        AssistantSuggestionKind.BookSlot, AssistantSuggestionKind.NewAppointment, AssistantSuggestionKind.MoveAppointment, AssistantSuggestionKind.CancelAppointment,
        AssistantSuggestionKind.SendQuestionnaire, AssistantSuggestionKind.OfferWaitlist, AssistantSuggestionKind.OpenBill, AssistantSuggestionKind.SearchFreeTime,
    ];

    private readonly AnswerKit _kit = new(fixture);

    private static Task<CustomerDto> GuestAsync(TestSpa spa, string first, string last) =>
        spa.PostAsync<CustomerDto>("/api/customers", new CustomerInput(first, last, null, null, null, null), Ct);

    private Task<AssistantAnswer> AskAsync(TestSpa spa, string question, StaffRole role = StaffRole.Reception, DateTimeOffset? now = null, Guid? employeeId = null,
        Guid? guestId = null) =>
        _kit.StaffAsync(spa, new AssistantQuestion(question, null, GuestId: guestId), role: role, now: now, employeeId: employeeId);

    private async Task<int> AppointmentsOfAsync(TestSpa spa, Guid customerId)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Use(spa.TenantId);
        return await scope.ServiceProvider.GetRequiredService<AispaDbContext>().Appointments.CountAsync(a => a.CustomerId == customerId, Ct);
    }

    private async Task<AppointmentStatus> StatusAsync(TestSpa spa, Guid appointmentId)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Use(spa.TenantId);
        return await scope.ServiceProvider.GetRequiredService<AispaDbContext>().Appointments.Where(a => a.Id == appointmentId).Select(a => a.Status).SingleAsync(Ct);
    }

    [Theory]
    [InlineData("buche steiner morgen 10 uhr massage", AssistantCommand.Book)]
    [InlineData("Buch Frau Moser für Freitag", AssistantCommand.Book)]
    [InlineData("verschiebe den termin von steiner auf 9.3. 14 uhr", AssistantCommand.Move)]
    [InlineData("sag den termin von steiner ab", AssistantCommand.Cancel)]
    [InlineData("storniere steiner", AssistantCommand.Cancel)]
    [InlineData("schick steiner den bogen", AssistantCommand.SendQuestionnaire)]
    [InlineData("sende frau moser den anamnesebogen", AssistantCommand.SendQuestionnaire)]
    [InlineData("biete wanda warte den termin an", AssistantCommand.Offer)]
    [InlineData("offene rechnung steiner", AssistantCommand.OpenBill)]
    public void Auftraege_am_Satzanfang_werden_erkannt(string question, AssistantCommand expected) =>
        Assert.Equal(expected, AssistantCommands.Detect(question));

    [Theory]
    [InlineData("wie verschiebe ich einen termin")]
    [InlineData("frau moser sagt ab – fällt eine gebühr an")]
    [InlineData("hat steiner eine offene rechnung")]
    [InlineData("wann ist steiner gebucht")]
    [InlineData("buchhaltung kontakt")]
    [InlineData("historie christian steiner")]
    public void Fragen_sind_keine_Auftraege(string question) =>
        Assert.Null(AssistantCommands.Detect(question));

    [Fact]
    public async Task Buche_steiner_morgen_10_uhr_massage_schlaegt_den_neuen_Termin_vor_und_bucht_nichts()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var steiner = await GuestAsync(spa, "Christian", "Steiner");

        var answer = await AskAsync(spa, "buche steiner morgen 10 uhr massage");

        var proposal = Assert.Single(answer.Suggestions);
        Assert.Equal(AssistantSuggestionKind.NewAppointment, proposal.Kind);
        Assert.Equal((spa.Location.Id, spa.MassageTreatment.Id, steiner.Id), (proposal.LocationId!.Value, proposal.TreatmentId!.Value, proposal.CustomerId!.Value));
        Assert.Equal(Tomorrow, proposal.Day);
        Assert.Equal(TestSpa.At(10, date: Tomorrow), proposal.Start);
        Assert.Equal("Vorschlag: neuer Termin für Christian Steiner, Massage, Donnerstag, 04.03.2027, 10:00 Uhr. Gebucht wird erst mit „Termin buchen“ in der Maske.", answer.Text);
        Assert.Equal(0, await AppointmentsOfAsync(spa, steiner.Id));
    }

    [Fact]
    public async Task Fehlende_Angaben_bleiben_leer_und_die_Antwort_nennt_sie()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);

        var answer = await AskAsync(spa, "buche morgen massage");

        var proposal = Assert.Single(answer.Suggestions);
        Assert.Equal(AssistantSuggestionKind.NewAppointment, proposal.Kind);
        Assert.Equal((spa.MassageTreatment.Id, (Guid?)null, (DateTimeOffset?)null, Tomorrow), (proposal.TreatmentId!.Value, proposal.CustomerId, proposal.Start, proposal.Day!.Value));
        Assert.EndsWith("Noch offen: Gast, Uhrzeit.", answer.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Therapeutin_ohne_Buchungsrecht_bekommt_keine_Buchungsmaske()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await GuestAsync(spa, "Christian", "Steiner");

        var answer = await AskAsync(spa, "buche steiner morgen 10 uhr massage", StaffRole.Therapist, employeeId: spa.Anna.Id);

        Assert.DoesNotContain(answer.Suggestions, s => MaskKinds.Contains(s.Kind));
        Assert.Equal("Termine buchen, verschieben und absagen darf, wer das Recht „Buchen“ hat; eine Maske dafür öffnet der Assistent hier nicht.", answer.Text);
    }

    [Fact]
    public async Task Sag_den_termin_von_steiner_ab_oeffnet_die_Absage_mit_der_Gebuehr_und_sagt_nichts_ab()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await spa.PutAsync<LocationOnlineRules>($"/api/locations/{spa.Location.Id}/online-rules", new LocationOnlineRules(true, 60, 365, 24, 15, null, false, 50), Ct);
        var steiner = await GuestAsync(spa, "Christian", "Steiner");
        var open = await spa.BookOkAsync(TestSpa.At(14, date: NextMonday), Ct, spa.Ben.Id, customer: steiner.Id);

        var answer = await AskAsync(spa, "sag den termin von steiner ab", now: TestSpa.At(9, date: NextMonday));

        var proposal = Assert.Single(answer.Suggestions);
        Assert.Equal((AssistantSuggestionKind.CancelAppointment, open.Id, open.Start), (proposal.Kind, proposal.AppointmentId!.Value, proposal.Start!.Value));
        Assert.Equal("Absage jetzt: 5 Std. vor Beginn, Frist 24 Std. – Gebühr 50 % = 39,50 EUR. Keine Karte hinterlegt.", proposal.Note);
        Assert.Contains("Abgesagt wird erst mit „Stornieren“ in der Rückfrage.", answer.Text, StringComparison.Ordinal);
        Assert.Equal(AppointmentStatus.Booked, await StatusAsync(spa, open.Id));
    }

    [Fact]
    public async Task Verschiebe_schlaegt_den_neuen_Beginn_vor_und_laesst_den_Termin_stehen()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var steiner = await GuestAsync(spa, "Christian", "Steiner");
        var open = await spa.BookOkAsync(TestSpa.At(10, date: NextMonday), Ct, spa.Anna.Id, customer: steiner.Id);

        var answer = await AskAsync(spa, "verschiebe den termin von steiner auf 9.3. 14 uhr");

        var proposal = Assert.Single(answer.Suggestions);
        Assert.Equal((AssistantSuggestionKind.MoveAppointment, open.Id, open.Start), (proposal.Kind, proposal.AppointmentId!.Value, proposal.Start!.Value));
        Assert.Equal(TestSpa.At(14, date: NextMonday.AddDays(1)), proposal.NewStart);
        Assert.Equal(AppointmentStatus.Booked, await StatusAsync(spa, open.Id));
    }

    [Fact]
    public async Task Schick_den_bogen_oeffnet_den_naechsten_Termin_des_Gastes()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var steiner = await GuestAsync(spa, "Christian", "Steiner");
        var open = await spa.BookOkAsync(TestSpa.At(10, date: NextMonday), Ct, spa.Anna.Id, customer: steiner.Id);

        var answer = await AskAsync(spa, "schick steiner den bogen");

        var proposal = Assert.Single(answer.Suggestions);
        Assert.Equal((AssistantSuggestionKind.SendQuestionnaire, open.Id, steiner.Id), (proposal.Kind, proposal.AppointmentId!.Value, proposal.CustomerId!.Value));
    }

    [Fact]
    public async Task Gast_ohne_Termin_bekommt_keine_Maske_zum_Absagen()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await GuestAsync(spa, "Christian", "Steiner");

        var answer = await AskAsync(spa, "sag den termin von steiner ab");

        Assert.Empty(answer.Suggestions);
        Assert.Equal("Christian Steiner hat keinen geplanten Termin.", answer.Text);
    }

    [Fact]
    public async Task Offene_rechnung_springt_mit_der_Rechnung_in_die_Kassa()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var steiner = await GuestAsync(spa, "Christian", "Steiner");
        var register = await spa.PostAsync<CashRegisterDto>("/api/pos/registers", new CashRegisterInput(spa.Location.Id, $"G-{Guid.NewGuid():N}"[..14], FiscalCountry.AT,
            "Test-Spa GmbH", "Test-Spa GmbH", "AT61 1904 3002 3457 3201", null), Ct);
        var bill = await spa.PostAsync<BillDto>("/api/pos/bills", new BillInput(register.Id, steiner.Id, []), Ct);

        var answer = await AskAsync(spa, "offene rechnung steiner");

        var proposal = Assert.Single(answer.Suggestions);
        Assert.Equal((AssistantSuggestionKind.OpenBill, bill.Id, spa.Location.Id), (proposal.Kind, proposal.TargetId!.Value, proposal.LocationId!.Value));
    }

    [Fact]
    public async Task Biete_an_oeffnet_den_Wunsch_auf_der_Warteliste()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        using var online = spa.OnlineClient();
        var joined = await online.PostAsJsonAsync("/api/online/waitlist", new WaitlistRequest(spa.Location.Id, spa.MassageTreatment.Id, NextMonday,
            NextMonday.AddDays(4), DayPart.Afternoon, new OnlineContact("Wanda", "Warte", $"warte-{Guid.NewGuid():N}@example.at", null), true, "de"), TestSpa.Json, Ct);
        joined.EnsureSuccessStatusCode();
        var entry = Assert.Single(await spa.GetAsync<List<WaitlistStaffEntry>>($"/api/locations/{spa.Location.Id}/waitlist", Ct));

        var answer = await AskAsync(spa, "biete wanda warte den termin an");

        var proposal = Assert.Single(answer.Suggestions);
        Assert.Equal((AssistantSuggestionKind.OfferWaitlist, entry.Id, entry.CustomerId), (proposal.Kind, proposal.TargetId!.Value, proposal.CustomerId!.Value));
    }

    [Fact]
    public async Task Mehrere_passende_Gaeste_stehen_zur_Auswahl_und_die_Wahl_gilt()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var christian = await GuestAsync(spa, "Christian", "Steiner");
        await GuestAsync(spa, "Maria", "Steiner");

        var choice = await AskAsync(spa, "buche steiner morgen 10 uhr massage");

        Assert.Empty(choice.Suggestions);
        Assert.Equal(2, choice.Block!.FollowUps.Count);
        var chosen = await AskAsync(spa, "buche steiner morgen 10 uhr massage", guestId: christian.Id);
        Assert.Equal(christian.Id, Assert.Single(chosen.Suggestions).CustomerId);
    }

    [Fact]
    public async Task Wie_verschiebe_ich_bleibt_eine_Frage_an_die_Hilfe()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);

        var answer = await AskAsync(spa, "wie verschiebe ich einen termin");

        Assert.DoesNotContain(answer.Suggestions, s => MaskKinds.Contains(s.Kind));
        Assert.NotEmpty(answer.Help);
    }
}
