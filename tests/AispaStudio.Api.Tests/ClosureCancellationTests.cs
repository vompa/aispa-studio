using System.Net;
using System.Net.Http.Json;
using AispaStudio.Api.Data;
using AispaStudio.Api.MailIntake.Assistant;
using AispaStudio.Api.Online;
using AispaStudio.Api.Scheduling;
using AispaStudio.Api.Tenancy;
using AispaStudio.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AispaStudio.Api.Tests;

/// <summary>
/// Termine einer Schließzeit absagen (ADR 0073): die offenen Termine stehen in der Rückfrage, nichts geschieht ohne Anfrage, je abgesagtem Termin geht genau eine
/// Nachricht über die Queue (Mail, SMS oder gar keine ohne Kontakt), der Termin ist „storniert durch Betrieb“ ohne Ausfallgebühr, und das Protokoll hält fest, wer wann
/// über welchen Kanal abgesagt hat. Texte mit KI tragen den Hinweis darauf, Texte ohne KI nicht.
/// </summary>
public sealed class ClosureCancellationTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly DateOnly Day = TestSpa.Monday.AddDays(7);

    private sealed record Guests(CustomerDto ByMail, CustomerDto BySms, CustomerDto NoContact, string Phone);

    private static string Path(TestSpa spa, ClosureDto closure, string tail = "") => $"{ClosureTests.Path(spa)}/{closure.Id}{tail}";

    private static async Task<Guests> GuestsAsync(TestSpa spa)
    {
        var suffix = Guid.NewGuid().ToString("N");
        var phone = $"+43 664 {Random.Shared.Next(1000000, 9999999)}";
        return new Guests(
            await spa.PostAsync<CustomerDto>("/api/customers", new CustomerInput("Ida", $"Mail{suffix}", $"ida-{suffix}@example.at", null, null, null), Ct),
            await spa.PostAsync<CustomerDto>("/api/customers", new CustomerInput("Sonja", $"Sms{suffix}", null, phone, null, null), Ct),
            await spa.PostAsync<CustomerDto>("/api/customers", new CustomerInput("Nora", $"Ohne{suffix}", null, null, null, null), Ct),
            PhoneNumbers.Normalize(phone));
    }

    /// <summary>Drei Termine am selben Montag (Mail, SMS, ohne Kontakt) und eine Schließzeit über den ganzen Tag, die erst danach angelegt wird.</summary>
    private async Task<(TestSpa Spa, Guests Guests, AppointmentDto[] Appointments, ClosureDto Closure)> SetupAsync()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var guests = await GuestsAsync(spa);
        var appointments = new[]
        {
            await TestSpa.ReadAsync<AppointmentDto>(await spa.BookAsync(TestSpa.At(9, 30, Day), Ct, customer: guests.ByMail.Id), Ct),
            await TestSpa.ReadAsync<AppointmentDto>(await spa.BookAsync(TestSpa.At(11, 0, Day), Ct, customer: guests.BySms.Id), Ct),
            await TestSpa.ReadAsync<AppointmentDto>(await spa.BookAsync(TestSpa.At(13, 0, Day), Ct, customer: guests.NoContact.Id), Ct),
        };
        var closure = await ClosureTests.CreateAsync(spa, ClosureTests.Input(Day, Day.AddDays(2), ClosureKind.Vacation, reason: "Inventur"));
        return (spa, guests, appointments, closure);
    }

    private static CancelClosureAppointmentsRequest Request(IEnumerable<AppointmentDto> appointments, string? text = null, bool rebook = false, bool ai = false) =>
        new([.. appointments.Select(a => a.Id)], "Absage: {Behandlung}",
            text ?? "Guten Tag {Anrede},\n\nleider fällt Ihr Termin ({Behandlung}, {Zeit}) aus.\n\nHerzlich, das Team", rebook, ai);

    private static async Task<List<ClosureNoticeDto>> WaitForNoticesAsync(TestSpa spa, ClosureDto closure, int count)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));
        while (true)
        {
            var notices = await spa.GetAsync<List<ClosureNoticeDto>>(Path(spa, closure, "/notices"), timeout.Token);
            if (notices.Count >= count && notices.All(n => n.State != ClosureNoticeState.Queued))
            {
                return notices;
            }

            await Task.Delay(50, timeout.Token);
        }
    }

    private async Task<Appointment> StoredAsync(TestSpa spa, Guid appointmentId)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Use(spa.TenantId);
        return await scope.ServiceProvider.GetRequiredService<AispaDbContext>().Appointments.AsNoTracking().FirstAsync(a => a.Id == appointmentId, Ct);
    }

    // ---------- Rückfrage ----------

    [Fact]
    public async Task Eine_neue_Schliesszeit_sagt_nichts_ab_die_Rueckfrage_listet_die_offenen_Termine_mit_Kanal_und_Vorlage()
    {
        var (spa, _, appointments, closure) = await SetupAsync();

        // Nichts geschieht von selbst: die Termine sind gebucht, die Schließzeit zählt sie als offen, es ging keine Mail hinaus.
        Assert.Equal((3, 0), (closure.OpenAppointments, closure.CancelledAppointments));
        foreach (var appointment in appointments)
        {
            Assert.Equal(AppointmentStatus.Booked, (await StoredAsync(spa, appointment.Id)).Status);
        }

        Assert.Empty(await spa.GetAsync<List<ClosureNoticeDto>>(Path(spa, closure, "/notices"), Ct));

        var preview = await spa.GetAsync<ClosureCancellationPreview>(Path(spa, closure, "/cancellation"), Ct);

        Assert.Equal(appointments.Select(a => a.Id), preview.Appointments.Select(a => a.AppointmentId));
        Assert.Equal([ClosureChannel.Email, ClosureChannel.Sms, ClosureChannel.None], preview.Appointments.Select(a => a.Channel));
        Assert.All(preview.Appointments, a => Assert.Equal(("Massage", AppointmentStatus.Booked, false, false), (a.TreatmentName, a.Status, a.HasDeposit, a.PrepaidOnPlatform)));
        // Ohne Anzahlung gibt es keinen Betrag (nicht 0,00).
        Assert.All(preview.Appointments, a => Assert.Null(a.DepositAmount));
        Assert.Equal(TestSpa.At(9, 30, Day), preview.Appointments[0].Start);
        Assert.True(preview.AiAvailable);
        Assert.Equal("Absage Ihres Termins: {Behandlung}, {Zeit}", preview.SuggestedSubject);
        Assert.Contains("Guten Tag {Anrede},", preview.SuggestedText, StringComparison.Ordinal);
        Assert.Contains("Wegen unseres Betriebsurlaubs (", preview.SuggestedText, StringComparison.Ordinal);
        Assert.Contains("{Behandlung}\n{Zeit}", preview.SuggestedText, StringComparison.Ordinal);
        Assert.EndsWith("Wien", preview.SuggestedText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Vorlage_je_Art_nutzt_den_Text_fuer_Gaeste_wenn_es_einen_gibt()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var outage = await ClosureTests.CreateAsync(spa, ClosureTests.Input(Day, kind: ClosureKind.Outage, guestText: "Wegen eines Wasserschadens bleibt das Spa zu"));
        var special = await ClosureTests.CreateAsync(spa, ClosureTests.Input(Day.AddDays(7), kind: ClosureKind.SpecialClosure));

        var damage = await spa.GetAsync<ClosureCancellationPreview>(Path(spa, outage, "/cancellation"), Ct);
        var holiday = await spa.GetAsync<ClosureCancellationPreview>(Path(spa, special, "/cancellation"), Ct);

        Assert.Contains("Wegen eines Wasserschadens bleibt das Spa zu. Deshalb müssen wir Ihren Termin leider absagen:", damage.SuggestedText, StringComparison.Ordinal);
        Assert.Contains("haben wir geschlossen.", holiday.SuggestedText, StringComparison.Ordinal);
        Assert.Empty(damage.Appointments);
    }

    // ---------- Absagen ----------

    [Fact]
    public async Task Absagen_sagt_die_gewaehlten_Termine_ab_stellt_je_Termin_genau_eine_Nachricht_zu_und_protokolliert()
    {
        var (spa, guests, appointments, closure) = await SetupAsync();
        var mailAddress = guests.ByMail.Email!;

        var result = await spa.PostAsync<CancelClosureAppointmentsResult>(Path(spa, closure, "/cancellation"), Request(appointments.Take(2), rebook: true), Ct);

        Assert.Equal((2, 2, 0, 0), (result.Cancelled, result.Queued, result.WithoutContact, result.Failed));
        var notices = await WaitForNoticesAsync(spa, closure, 2);
        Assert.All(notices, n => Assert.Equal((ClosureNoticeState.Sent, "Test Person", false, true), (n.State, n.SentBy, n.AiWritten, n.OfferedRebooking)));
        Assert.Equal([ClosureChannel.Email, ClosureChannel.Sms], notices.OrderBy(n => n.AppointmentStart).Select(n => n.Channel));

        // Genau eine Mail an die erste Person: Platzhalter ersetzt, Link zum Neubuchen, kein Hinweis auf die KI.
        var mail = Assert.Single(fixture.Mailbox.To(mailAddress));
        Assert.Equal("Absage: Massage", mail.Subject);
        Assert.StartsWith($"Guten Tag Ida {guests.ByMail.LastName},", mail.Text, StringComparison.Ordinal);
        Assert.Contains("leider fällt Ihr Termin (Massage, Montag, 8. März um 09:30 Uhr) aus.", mail.Text, StringComparison.Ordinal);
        Assert.Contains($"Einen neuen Termin können Sie hier buchen: http://localhost:5173/buchen/?spa={spa.TenantId}#/", mail.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("KI-Assistenten", mail.Text, StringComparison.Ordinal);

        // Genau eine SMS an die zweite, ohne Leerzeilen; eine Mail geht an sie nicht.
        var sms = Assert.Single(fixture.Texts.To(guests.Phone));
        Assert.Equal(BookingChannel.Sms, sms.Channel);
        Assert.DoesNotContain("\n\n", sms.Text, StringComparison.Ordinal);
        Assert.StartsWith($"Guten Tag Sonja {guests.BySms.LastName},\nleider fällt Ihr Termin", sms.Text, StringComparison.Ordinal);

        // Status: storniert durch den Betrieb mit dem Grund der Schließzeit; der dritte Termin bleibt offen.
        var first = await StoredAsync(spa, appointments[0].Id);
        Assert.Equal((AppointmentStatus.Cancelled, true), (first.Status, first.CancelledByBusiness));
        Assert.StartsWith("Schließzeit (Betriebsurlaub): ", first.CancelReason, StringComparison.Ordinal);
        var third = await StoredAsync(spa, appointments[2].Id);
        Assert.Equal((AppointmentStatus.Booked, false), (third.Status, third.CancelledByBusiness));

        var updated = await spa.GetAsync<ClosureDto>(Path(spa, closure), Ct);
        Assert.Equal((1, 2), (updated.OpenAppointments, updated.CancelledAppointments));
        var calendar = await spa.GetAsync<CalendarDay>($"/api/locations/{spa.Location.Id}/calendar?date={Day:yyyy-MM-dd}", Ct);
        Assert.Equal(1, Assert.Single(calendar.Closures!).OpenAppointments);

        // Der Rest geht nur mit einer neuen Anfrage; wer schon abgesagt ist, ist nicht mehr offen.
        var again = await spa.Client.PostAsJsonAsync(Path(spa, closure, "/cancellation"), Request(appointments.Take(1)), TestSpa.Json, Ct);
        Assert.Contains("appointmentIds", (await ProblemAsync(again)).Errors.Keys);
        Assert.Single(fixture.Mailbox.To(mailAddress));
    }

    [Fact]
    public async Task Ohne_Mailadresse_und_Telefonnummer_wird_abgesagt_aber_nichts_gesendet_und_im_Protokoll_vermerkt()
    {
        var (spa, guests, appointments, closure) = await SetupAsync();

        var result = await spa.PostAsync<CancelClosureAppointmentsResult>(Path(spa, closure, "/cancellation"), Request([appointments[2]]), Ct);

        Assert.Equal((1, 0, 1, 0), (result.Cancelled, result.Queued, result.WithoutContact, result.Failed));
        var notice = Assert.Single(result.Notices);
        Assert.Equal((ClosureChannel.None, ClosureNoticeState.Skipped, "Keine Mailadresse und keine Telefonnummer.", "Nora"), (notice.Channel, notice.State, notice.Error, guests.NoContact.FirstName));
        Assert.Equal(AppointmentStatus.Cancelled, (await StoredAsync(spa, appointments[2].Id)).Status);
        Assert.Empty(fixture.Mailbox.To(guests.ByMail.Email!));
    }

    [Fact]
    public async Task Text_mit_KI_traegt_den_Hinweis_darauf_Text_ohne_KI_nicht()
    {
        var (spa, guests, appointments, closure) = await SetupAsync();

        await spa.PostAsync<CancelClosureAppointmentsResult>(Path(spa, closure, "/cancellation"), Request([appointments[0]], ai: true), Ct);
        await spa.PostAsync<CancelClosureAppointmentsResult>(Path(spa, closure, "/cancellation"), Request([appointments[1]], ai: false), Ct);
        await WaitForNoticesAsync(spa, closure, 2);

        var mail = Assert.Single(fixture.Mailbox.To(guests.ByMail.Email!));
        Assert.EndsWith(ReplyLanguages.Disclosure("de"), mail.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("KI-Assistenten", Assert.Single(fixture.Texts.To(guests.Phone)).Text, StringComparison.Ordinal);
        var notices = await spa.GetAsync<List<ClosureNoticeDto>>(Path(spa, closure, "/notices"), Ct);
        Assert.Equal([true, false], notices.OrderBy(n => n.AppointmentStart).Select(n => n.AiWritten));
    }

    [Fact]
    public async Task Ungueltige_Anfragen_sagen_nichts_ab()
    {
        var (spa, _, appointments, closure) = await SetupAsync();
        var elsewhere = await spa.BookOkAsync(TestSpa.At(10, 0, Day.AddDays(14)), Ct);

        async Task<List<string>> FieldsAsync(CancelClosureAppointmentsRequest request) =>
            [.. (await ProblemAsync(await spa.Client.PostAsJsonAsync(Path(spa, closure, "/cancellation"), request, TestSpa.Json, Ct))).Errors.Keys.Order(StringComparer.Ordinal)];

        Assert.Equal(["appointmentIds"], await FieldsAsync(Request([])));
        Assert.Equal(["text"], await FieldsAsync(Request(appointments, text: "  ")));
        Assert.Equal(["text"], await FieldsAsync(Request(appointments, text: new string('x', 2001))));
        Assert.Equal(["subject"], await FieldsAsync(Request(appointments) with { Subject = " " }));
        // Ein Termin außerhalb der Schließzeit macht die ganze Anfrage ungültig: auch die richtigen bleiben gebucht.
        Assert.Equal(["appointmentIds"], await FieldsAsync(Request([appointments[0], elsewhere])));
        Assert.Equal(["appointmentIds"], await FieldsAsync(Request(appointments) with { AppointmentIds = [Guid.NewGuid()] }));

        foreach (var appointment in appointments)
        {
            Assert.Equal(AppointmentStatus.Booked, (await StoredAsync(spa, appointment.Id)).Status);
        }

        Assert.Equal(HttpStatusCode.NotFound, (await spa.Client.PostAsJsonAsync($"{ClosureTests.Path(spa)}/{Guid.NewGuid()}/cancellation", Request(appointments), TestSpa.Json, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await spa.Client.GetAsync($"{ClosureTests.Path(spa)}/{Guid.NewGuid()}/cancellation", Ct)).StatusCode);
    }

    [Fact]
    public async Task Eine_Schliesszeit_mit_abgesagten_Terminen_bleibt_als_Verlauf_stehen()
    {
        var (spa, _, appointments, closure) = await SetupAsync();
        await spa.PostAsync<CancelClosureAppointmentsResult>(Path(spa, closure, "/cancellation"), Request([appointments[2]]), Ct);

        var response = await spa.Client.DeleteAsync(Path(spa, closure), Ct);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains(await spa.GetAsync<List<ClosureDto>>(ClosureTests.Path(spa), Ct), c => c.Id == closure.Id);
    }

    // ---------- KI beim Formulieren ----------

    [Fact]
    public async Task Die_KI_formuliert_nur_auf_Knopfdruck_und_behaelt_Platzhalter_und_Zahlen()
    {
        var (spa, _, _, closure) = await SetupAsync();
        var preview = await spa.GetAsync<ClosureCancellationPreview>(Path(spa, closure, "/cancellation"), Ct);
        string[] placeholders = ["{Anrede}", "{Behandlung}", "{Zeit}"];

        var friendlier = await spa.PostAsync<ReviseClosureTextResult>(Path(spa, closure, "/cancellation/revise"),
            new ReviseClosureTextRequest(preview.SuggestedText, ReplyRevisionKind.Friendlier), Ct);
        Assert.StartsWith("Herzlich willkommen! ", friendlier.Text, StringComparison.Ordinal);
        Assert.All(placeholders, p => Assert.Contains(p, friendlier.Text, StringComparison.Ordinal));

        // „Kürzer“ lässt die Platzhalter weg: der Server verwirft die Fassung.
        var shorter = await spa.Client.PostAsJsonAsync(Path(spa, closure, "/cancellation/revise"), new ReviseClosureTextRequest(preview.SuggestedText, ReplyRevisionKind.Shorter), TestSpa.Json, Ct);
        Assert.Contains("text", (await ProblemAsync(shorter)).Errors.Keys);
        var custom = await spa.Client.PostAsJsonAsync(Path(spa, closure, "/cancellation/revise"), new ReviseClosureTextRequest(preview.SuggestedText, ReplyRevisionKind.Custom), TestSpa.Json, Ct);
        Assert.Contains("text", (await ProblemAsync(custom)).Errors.Keys);
        var empty = await spa.Client.PostAsJsonAsync(Path(spa, closure, "/cancellation/revise"), new ReviseClosureTextRequest(" ", ReplyRevisionKind.Friendlier), TestSpa.Json, Ct);
        Assert.Contains("text", (await ProblemAsync(empty)).Errors.Keys);
        Assert.Equal(HttpStatusCode.NotFound, (await spa.Client.PostAsJsonAsync($"{ClosureTests.Path(spa)}/{Guid.NewGuid()}/cancellation/revise",
            new ReviseClosureTextRequest("Text", ReplyRevisionKind.Friendlier), TestSpa.Json, Ct)).StatusCode);
    }

    // ---------- Keine Gebühr bei Absage durch den Betrieb ----------

    [Fact]
    public void Eine_Absage_durch_den_Betrieb_kostet_nie_Ausfallgebuehr_und_behaelt_keine_Anzahlung()
    {
        var start = new DateTimeOffset(2027, 3, 8, 10, 0, 0, TimeSpan.Zero);
        // Eine Stunde vor Beginn abgesagt, bei 24 Stunden Frist: sonst eine späte Absage.
        Appointment Cancelled(bool byBusiness) => new()
        {
            Start = start,
            Status = AppointmentStatus.Cancelled,
            CancelledByBusiness = byBusiness,
            UpdatedAt = start.AddHours(-1),
        };
        var guarantee = new CardGuarantee
        {
            Email = "g@example.at",
            Language = "de",
            Currency = "EUR",
            Provider = "Stripe",
            DeadlineHours = 24,
            Status = CardGuaranteeStatus.Saved,
            SavedAt = start.AddDays(-5),
        };
        var deposit = new DepositOrder
        {
            Email = "g@example.at",
            Language = "de",
            Currency = "EUR",
            Provider = "Stripe",
            DeadlineHours = 24,
            Status = OnlineDepositStatus.Receipted,
            Amount = 40m,
            Percent = 50,
        };

        Assert.True(CardGuarantees.ToStaff(guarantee, Cancelled(byBusiness: false)).CanCharge);
        var spared = CardGuarantees.ToStaff(guarantee, Cancelled(byBusiness: true));
        Assert.False(spared.CanCharge);
        Assert.Equal("Vom Betrieb abgesagt: keine Ausfallgebühr.", spared.Hint);

        Assert.True(Deposits.ToStaff(deposit, Cancelled(byBusiness: false), null).Kept);
        var refunded = Deposits.ToStaff(deposit, Cancelled(byBusiness: true), null);
        Assert.False(refunded.Kept);
        Assert.StartsWith("Vom Betrieb abgesagt;", refunded.Hint, StringComparison.Ordinal);
    }

    private static async Task<HttpValidationProblemDetails> ProblemAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>(TestSpa.Json, Ct))!;
    }
}
