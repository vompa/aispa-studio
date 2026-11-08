using System.Net;
using System.Net.Http.Json;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Tests;

/// <summary>
/// Gestufte Kundenzuordnung eingehender Nachrichten (ADR 0061): sicher nur bei gleicher Mailadresse oder Nummer, sonst Vorschläge für die
/// Rezeption; bis dahin keine Daten aus der Kundenkarte in Antwort oder Buchung.
/// </summary>
public sealed partial class MailIntakeTests
{
    private const string Wish = "am 01.03.2027 um 10 Uhr";

    private static string Request(MailSpa spa, string signature = "Sonja Winkler", string time = Wish) =>
        $"Guten Tag,\nich hätte gerne eine {spa.Treatment.Name} {time} in {spa.Location.Name}.\n\nLiebe Grüße\n{signature}";

    private static Task<CustomerDto> NewCustomerAsync(MailSpa spa, string first, string last, string? email = null, string? phone = null) =>
        spa.Spa.PostAsync<CustomerDto>("/api/customers", new CustomerInput(first, last, email, phone, null, null), Ct);

    private static async Task<int> CountCustomersAsync(MailSpa spa, string search) =>
        (await spa.Spa.GetAsync<List<CustomerDto>>($"/api/customers?search={Uri.EscapeDataString(search)}", Ct)).Count;

    [Fact]
    public async Task Gleiche_Mailadresse_ordnet_sicher_zu_auch_bei_anderer_Schreibweise_und_weiterer_Adresse()
    {
        var spa = await MailSpa.CreateAsync(fixture, Ct);
        var customer = await NewCustomerAsync(spa, "Sonja", "Winkler", $"Sonja.{spa.Guest}");
        var extra = $"privat-{spa.Guest}";
        await spa.Spa.PostAsync<List<CustomerEmailDto>>($"/api/customers/{customer.Id}/emails", new CustomerEmailInput(extra), Ct);

        var viaMain = await spa.ReceiveAsync($"SONJA.{spa.Guest.ToUpperInvariant()}", "Termin", Request(spa, "Mama"), Ct);
        Assert.Equal(InboundMailStatus.Booked, viaMain.Status);
        Assert.Equal(customer.Id, viaMain.CustomerId);
        Assert.True(viaMain.CustomerConfirmed);
        Assert.Empty(viaMain.CustomerCandidates!);

        var viaExtra = await spa.ReceiveAsync(extra, "Termin", Request(spa, "Mama", "am 01.03.2027 um 12 Uhr"), Ct);
        Assert.Equal(InboundMailStatus.Booked, viaExtra.Status);
        Assert.Equal(customer.Id, viaExtra.CustomerId);
        Assert.Equal(1, await CountCustomersAsync(spa, "Winkler"));
    }

    [Fact]
    public async Task Kundensuche_findet_eine_ganze_Mailadresse_in_anderer_Schreibweise_auch_als_weitere_Adresse()
    {
        // „Schon vorhanden?“ in der Maske „Neue Kundin“ sucht mit der ganzen Mailadresse (T-023, Ä365).
        var spa = await MailSpa.CreateAsync(fixture, Ct);
        var customer = await NewCustomerAsync(spa, "Sonja", "Winkler", $"Sonja.{spa.Guest}");
        var extra = $"privat-{spa.Guest}";
        await spa.Spa.PostAsync<List<CustomerEmailDto>>($"/api/customers/{customer.Id}/emails", new CustomerEmailInput(extra), Ct);

        foreach (var search in new[] { $"sonja.{spa.Guest}".ToLowerInvariant(), extra.ToUpperInvariant() })
        {
            var found = await spa.Spa.GetAsync<List<CustomerDto>>($"/api/customers?search={Uri.EscapeDataString(search)}", Ct);
            Assert.Equal(customer.Id, Assert.Single(found).Id);
        }
    }

    [Fact]
    public async Task Gleiche_Nummer_nach_Normalisierung_ordnet_eine_SMS_sicher_zu()
    {
        var spa = await MailSpa.CreateAsync(fixture, Ct);
        var customer = await NewCustomerAsync(spa, "Sonja", "Winkler", null, "0660 (123) 45-67");

        var sender = "+436601234567";
        var response = await spa.Spa.Client.PostAsJsonAsync("/api/inbox/dev/messages",
            new { channel = BookingChannel.Sms, from = sender, fromName = (string?)null, text = Request(spa, "Mama") }, TestSpa.Json, Ct);
        response.EnsureSuccessStatusCode();
        var mail = await spa.WaitForSenderAsync(sender, Ct);

        Assert.Equal(InboundMailStatus.Booked, mail.Status);
        Assert.Equal(customer.Id, mail.CustomerId);
        Assert.True(mail.CustomerConfirmed);
    }

    [Fact]
    public async Task Ohne_Kandidaten_wird_der_Gast_neu_angelegt_und_als_aus_der_Nachricht_angelegt_gekennzeichnet()
    {
        var spa = await MailSpa.CreateAsync(fixture, Ct);

        var mail = await spa.ReceiveAsync(spa.Guest, "Termin", Request(spa, "Eva Sommer"), Ct);

        Assert.Equal(InboundMailStatus.Booked, mail.Status);
        Assert.True(mail.CustomerConfirmed);
        var customer = await spa.Spa.GetAsync<CustomerDto>($"/api/customers/{mail.CustomerId}", Ct);
        Assert.Equal(nameof(BookingChannel.Email), customer.CreatedFromChannel);
        Assert.Equal("Aus Mail angelegt", customer.Note);
        Assert.Equal(spa.Guest, customer.Email);
    }

    [Fact]
    public async Task Titel_aus_der_Signatur_ist_ein_Vorschlag_und_der_angelegte_Kunde_bleibt_ohne_Anrede_und_Titel()
    {
        var spa = await MailSpa.CreateAsync(fixture, Ct);

        var mail = await spa.ReceiveAsync(spa.Guest, "Termin", Request(spa, "Dr. Eva Sommer"), Ct);

        Assert.Equal("Dr.", mail.SuggestedTitlesBefore);
        var customer = await spa.Spa.GetAsync<CustomerDto>($"/api/customers/{mail.CustomerId}", Ct);
        Assert.Equal(("Eva", "Sommer"), (customer.FirstName, customer.LastName));
        Assert.Equal(CustomerSalutation.None, customer.Salutation);
        Assert.Null(customer.TitlesBefore);
    }

    [Fact]
    public async Task Gleicher_Name_unter_anderer_Adresse_ist_ein_Vorschlag_und_es_wird_nicht_gebucht_und_nichts_angelegt()
    {
        var spa = await MailSpa.CreateAsync(fixture, Ct);
        var customer = await NewCustomerAsync(spa, "Sonja", "Winkler", $"alt-{spa.Guest}", "+43 664 1112222");

        var mail = await spa.ReceiveAsync(spa.Guest, "Termin", Request(spa), Ct);

        Assert.Equal(InboundMailStatus.NeedsReview, mail.Status);
        Assert.Null(mail.AppointmentId);
        Assert.Null(mail.CustomerId);
        Assert.False(mail.CustomerConfirmed);
        var candidate = Assert.Single(mail.CustomerCandidates!);
        Assert.Equal(customer.Id, candidate.CustomerId);
        Assert.Equal(4, candidate.Stage);
        Assert.Contains("gleicher Name", candidate.Reason, StringComparison.Ordinal);
        Assert.Contains("Wer ist das?", mail.Note, StringComparison.Ordinal);
        Assert.Equal(1, await CountCustomersAsync(spa, "Winkler"));
        // Die KI schlägt trotzdem Zeiten vor und entwirft die Antwort, ohne etwas aus der Kundenkarte.
        Assert.Contains(mail.Proposals, p => p.Start == TestSpa.At(10));
        Assert.Equal(ReplyStatus.Draft, mail.ReplyStatus);
        Assert.Contains("Sonja Winkler", mail.ReplyBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Gleicher_Name_mit_Termin_ist_Stufe_3_und_nennt_den_Termin_im_Grund()
    {
        var spa = await MailSpa.CreateAsync(fixture, Ct);
        var customer = await NewCustomerAsync(spa, "Sonja", "Winkler", $"alt-{spa.Guest}");
        await TestSpa.ReadAsync<AppointmentDto>(
            await spa.Spa.BookAsync(TestSpa.At(15), Ct, treatment: spa.Treatment, location: spa.Location.Id, customer: customer.Id), Ct);

        var mail = await spa.ReceiveAsync(spa.Guest, "Termin", Request(spa), Ct);

        var candidate = Assert.Single(mail.CustomerCandidates!);
        Assert.Equal(3, candidate.Stage);
        Assert.Contains("nächster Termin 01.03.2027", candidate.Reason, StringComparison.Ordinal);
        Assert.Equal(InboundMailStatus.NeedsReview, mail.Status);
    }

    [Fact]
    public async Task Von_der_KI_gelesene_Nummer_ist_Stufe_2_und_steht_vor_den_Namen()
    {
        var spa = await MailSpa.CreateAsync(fixture, Ct);
        var byPhone = await NewCustomerAsync(spa, "Anna", "Berger", null, "0664 555 12 34");
        await NewCustomerAsync(spa, "Sonja", "Winkler", $"alt-{spa.Guest}");

        var mail = await spa.ReceiveAsync(spa.Guest, "Termin", Request(spa) + "\nTel. +43 664 5551234", Ct);

        Assert.Equal(2, mail.CustomerCandidates![0].Stage);
        Assert.Equal(byPhone.Id, mail.CustomerCandidates[0].CustomerId);
        Assert.Contains("Telefonnummer aus der Nachricht passt", mail.CustomerCandidates[0].Reason, StringComparison.Ordinal);
        Assert.Equal(4, mail.CustomerCandidates[1].Stage);
        Assert.Equal(InboundMailStatus.NeedsReview, mail.Status);
    }

    [Fact]
    public async Task Rezeption_ordnet_zu_merkt_die_Adresse_und_bucht_danach_dann_gilt_die_Adresse_als_sicher()
    {
        var spa = await MailSpa.CreateAsync(fixture, Ct);
        var customer = await NewCustomerAsync(spa, "Sonja", "Winkler", $"alt-{spa.Guest}");
        var mail = await spa.ReceiveAsync(spa.Guest, "Termin", Request(spa), Ct);

        // Vorher nicht still neu anlegen: erst zuordnen oder bewusst neu anlegen.
        var blocked = await spa.Spa.Client.PostAsJsonAsync($"/api/inbox/{mail.Id}/accept",
            new AcceptProposalRequest(TestSpa.At(10), null, null, null), TestSpa.Json, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, blocked.StatusCode);
        Assert.Contains("Wer ist das?", await blocked.Content.ReadAsStringAsync(Ct), StringComparison.Ordinal);
        Assert.Equal(1, await CountCustomersAsync(spa, "Winkler"));

        var assigned = await spa.Spa.PostAsync<InboundMailDto>($"/api/inbox/{mail.Id}/customer/assign",
            new AssignCustomerInput(customer.Id, RememberContact: true), Ct);
        Assert.Equal(customer.Id, assigned.CustomerId);
        Assert.True(assigned.CustomerConfirmed);
        Assert.Empty(assigned.CustomerCandidates!);
        Assert.Equal(InboundMailStatus.Proposed, assigned.Status);
        Assert.DoesNotContain("Wer ist das?", assigned.Note ?? "", StringComparison.Ordinal);
        var emails = await spa.Spa.GetAsync<List<CustomerEmailDto>>($"/api/customers/{customer.Id}/emails", Ct);
        Assert.Equal(spa.Guest, Assert.Single(emails).Address);

        var booked = await spa.Spa.PostAsync<InboundMailDto>($"/api/inbox/{mail.Id}/accept",
            new AcceptProposalRequest(TestSpa.At(10), null, null, null), Ct);
        Assert.Equal(InboundMailStatus.Booked, booked.Status);
        Assert.Equal(customer.Id, (await spa.Spa.GetAsync<AppointmentDto>($"/api/appointments/{booked.AppointmentId}", Ct)).CustomerId);

        // Die gemerkte Adresse ordnet die nächste Nachricht sicher zu.
        var next = await spa.ReceiveAsync(spa.Guest, "Noch ein Termin", Request(spa, "Mama", "am 01.03.2027 um 14 Uhr"), Ct);
        Assert.Equal(InboundMailStatus.Booked, next.Status);
        Assert.Equal(customer.Id, next.CustomerId);
    }

    [Fact]
    public async Task Neu_anlegen_uebernimmt_Name_und_Adresse_der_Nachricht_und_vermerkt_den_Kanal()
    {
        var spa = await MailSpa.CreateAsync(fixture, Ct);
        await NewCustomerAsync(spa, "Sonja", "Winkler", $"alt-{spa.Guest}");
        var mail = await spa.ReceiveAsync(spa.Guest, "Termin", Request(spa) + "\nTel. 0664 777 88 99", Ct);

        var created = await spa.Spa.PostAsync<InboundMailDto>($"/api/inbox/{mail.Id}/customer/new", new NewCustomerFromMailInput(), Ct);

        Assert.True(created.CustomerConfirmed);
        var customer = await spa.Spa.GetAsync<CustomerDto>($"/api/customers/{created.CustomerId}", Ct);
        Assert.Equal(("Sonja", "Winkler", spa.Guest, "0664 777 88 99"), (customer.FirstName, customer.LastName, customer.Email, customer.Phone));
        Assert.Equal(nameof(BookingChannel.Email), customer.CreatedFromChannel);
        Assert.Equal(2, await CountCustomersAsync(spa, "Winkler"));
        Assert.Equal(InboundMailStatus.Proposed, created.Status);

        var again = await spa.Spa.Client.PostAsync($"/api/inbox/{mail.Id}/customer/new", JsonContent.Create(new NewCustomerFromMailInput(), options: TestSpa.Json), Ct);
        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
    }

    [Fact]
    public async Task Zuordnung_loesen_schliesst_den_Kunden_aus_und_die_Nachricht_wartet_wieder()
    {
        var spa = await MailSpa.CreateAsync(fixture, Ct);
        var customer = await NewCustomerAsync(spa, "Lena", "Gruber", spa.Guest);
        await spa.BookAsync(TestSpa.At(10), Ct);
        var mail = await spa.ReceiveAsync(spa.Guest, "Massage",
            $"Gibt es am 1.3.2027 um 10:00 einen Termin für {spa.Treatment.Name} im {spa.Location.Name}? Danke\nLukas Berg", Ct);
        Assert.Equal(customer.Id, mail.CustomerId);
        Assert.Equal(InboundMailStatus.Proposed, mail.Status);

        var released = await spa.Spa.PostAsync<InboundMailDto>($"/api/inbox/{mail.Id}/customer/release", new { }, Ct);

        Assert.Null(released.CustomerId);
        Assert.False(released.CustomerConfirmed);
        Assert.DoesNotContain(released.CustomerCandidates!, c => c.CustomerId == customer.Id);
        var again = await spa.Spa.Client.PostAsync($"/api/inbox/{mail.Id}/customer/release", null, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, again.StatusCode);
    }

    [Theory]
    [InlineData("Hallo, ich muss meinen Termin leider absagen, bitte Storno.", InboundMailKind.Cancellation)]
    [InlineData("Hallo, ich möchte meinen Termin bitte verschieben.", InboundMailKind.Reschedule)]
    public async Task Absage_und_Verschieben_ohne_sichere_Zuordnung_nennen_keinen_Termin_aus_der_Kundenkarte(string text, InboundMailKind kind)
    {
        var spa = await MailSpa.CreateAsync(fixture, Ct);
        var customer = await NewCustomerAsync(spa, "Sonja", "Winkler", $"alt-{spa.Guest}");
        var appointment = await TestSpa.ReadAsync<AppointmentDto>(
            await spa.Spa.BookAsync(TestSpa.At(10), Ct, treatment: spa.Treatment, location: spa.Location.Id, customer: customer.Id), Ct);

        var mail = await spa.ReceiveAsync(spa.Guest, "Termin", $"{text}\n\nLiebe Grüße\nSonja Winkler", Ct);

        // Abgelehnt: Weder Notiz noch Entwurf noch Knöpfe verraten den Termin, bevor die Rezeption den Gast bestätigt hat.
        Assert.Equal(kind, mail.Kind);
        Assert.Equal(InboundMailStatus.NeedsReview, mail.Status);
        Assert.Null(mail.AppointmentId);
        Assert.False(mail.CanCancelAppointment);
        Assert.Single(mail.CustomerCandidates!);
        foreach (var text2 in new[] { mail.Note, mail.ReplyBody, mail.Summary })
        {
            Assert.DoesNotContain("01.03.2027", text2 ?? "", StringComparison.Ordinal);
            Assert.DoesNotContain(spa.Treatment.Name, text2 ?? "", StringComparison.Ordinal);
        }

        if (kind == InboundMailKind.Cancellation)
        {
            var refused = await spa.Spa.Client.PostAsync($"/api/inbox/{mail.Id}/cancel-appointment", null, Ct);
            Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        }

        // Erlaubt: Nach der Bestätigung durch die Rezeption steht der nächste Termin im Hinweis (nicht im Entwurf an den Gast).
        var assigned = await spa.Spa.PostAsync<InboundMailDto>($"/api/inbox/{mail.Id}/customer/assign", new AssignCustomerInput(customer.Id), Ct);
        Assert.Contains("am 01.03.2027 um 10:00 Uhr", assigned.Note, StringComparison.Ordinal);
        Assert.Equal(kind == InboundMailKind.Cancellation ? appointment.Id : null, assigned.AppointmentId);
        Assert.Equal(kind == InboundMailKind.Cancellation, assigned.CanCancelAppointment);
        Assert.DoesNotContain("01.03.2027", assigned.ReplyBody ?? "", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Frage_ohne_sichere_Zuordnung_schickt_nichts_aus_der_Kundenkarte_an_die_KI()
    {
        var spa = await MailSpa.CreateAsync(fixture, Ct);
        var customer = await NewCustomerAsync(spa, "Sonja", "Winkler", $"alt-{spa.Guest}", "+43 664 9990001");
        var subject = $"Öffnung {Guid.NewGuid():N}";

        var mail = await spa.ReceiveAsync(spa.Guest, subject, "Guten Tag, wie lange haben Sie abends geöffnet?\n\nLiebe Grüße\nSonja Winkler", Ct);

        Assert.Equal(InboundMailKind.Question, mail.Kind);
        var request = FakeQuestionAnswerer.Requests[subject];
        Assert.DoesNotContain(spa.Guest, request.Facts, StringComparison.Ordinal);
        Assert.DoesNotContain("9990001", request.Facts, StringComparison.Ordinal);
        Assert.DoesNotContain("alt-", request.Facts, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Gesperrter_Kunde_bleibt_Vorschlag_und_ist_im_Grund_gekennzeichnet()
    {
        var spa = await MailSpa.CreateAsync(fixture, Ct);
        var customer = await NewCustomerAsync(spa, "Sonja", "Winkler", $"alt-{spa.Guest}");
        await spa.Spa.PostAsync<List<CustomerFlagDto>>($"/api/customers/{customer.Id}/flags",
            new CustomerFlagInput(CustomerFlagKind.DoNotBook, null, "Mehrfach nicht erschienen"), Ct);

        var mail = await spa.ReceiveAsync(spa.Guest, "Termin", Request(spa), Ct);

        var candidate = Assert.Single(mail.CustomerCandidates!);
        Assert.Equal(customer.Id, candidate.CustomerId);
        Assert.Contains("„Nicht mehr buchen“ ist gesetzt", candidate.Reason, StringComparison.Ordinal);
        Assert.DoesNotContain("Mehrfach", candidate.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Hoechstens_drei_Vorschlaege_stufenweise_sortiert()
    {
        var spa = await MailSpa.CreateAsync(fixture, Ct);
        foreach (var first in new[] { "Sonja", "Sonja Maria", "Sonjaa", "Sonia" })
        {
            await NewCustomerAsync(spa, first, "Winkler", $"{first.Replace(' ', '-')}-{spa.Guest}");
        }

        var mail = await spa.ReceiveAsync(spa.Guest, "Termin", Request(spa), Ct);

        Assert.Equal(3, mail.CustomerCandidates!.Count);
        Assert.Equal(mail.CustomerCandidates.Select(c => c.Stage).Order(), mail.CustomerCandidates.Select(c => c.Stage));
        Assert.All(mail.CustomerCandidates, c => Assert.Equal(4, c.Stage));
    }

    [Fact]
    public async Task Treatwell_Buchung_mit_Vorschlag_blockt_sofort_und_wird_danach_auf_den_Kunden_umgehaengt()
    {
        var spa = await MailSpa.CreateAsync(fixture, Ct);
        var customer = await NewCustomerAsync(spa, "Max", "Mustermann", $"max-{spa.Guest}", "+43 664 1234000");
        var reference = $"TW-{Guid.NewGuid():N}"[..14];

        var mail = await spa.ReceiveAsync("buchungen@treatwell.at", "Neue Buchung über Treatwell", spa.TreatwellMail(reference, "14:00"), Ct);

        // Die Zeit ist sofort belegt; offen ist nur, wer der Gast ist.
        Assert.Equal(InboundMailStatus.Booked, mail.Status);
        Assert.NotNull(mail.AppointmentId);
        Assert.True(mail.CustomerAssignmentOpen);
        Assert.False(mail.CustomerConfirmed);
        Assert.Equal(customer.Id, Assert.Single(mail.CustomerCandidates!).CustomerId);
        Assert.Contains("Wer ist das?", mail.Note, StringComparison.Ordinal);
        var provisionalId = mail.CustomerId!.Value;
        Assert.NotEqual(customer.Id, provisionalId);
        var provisional = await spa.Spa.GetAsync<CustomerDto>($"/api/customers/{provisionalId}", Ct);
        Assert.True(provisional.AssignmentOpen);
        Assert.Equal("Treatwell", provisional.CreatedFromChannel);
        var open = await spa.Spa.GetAsync<AppointmentDto>($"/api/appointments/{mail.AppointmentId}", Ct);
        Assert.True(open.CustomerAssignmentOpen);
        Assert.Equal(provisionalId, open.CustomerId);

        var assigned = await spa.Spa.PostAsync<InboundMailDto>($"/api/inbox/{mail.Id}/customer/assign", new AssignCustomerInput(customer.Id), Ct);

        Assert.True(assigned.CustomerConfirmed);
        Assert.False(assigned.CustomerAssignmentOpen);
        Assert.Equal(InboundMailStatus.Booked, assigned.Status);
        var moved = await spa.Spa.GetAsync<AppointmentDto>($"/api/appointments/{mail.AppointmentId}", Ct);
        Assert.Equal(customer.Id, moved.CustomerId);
        Assert.False(moved.CustomerAssignmentOpen);
        Assert.Equal(HttpStatusCode.NotFound, (await spa.Spa.Client.GetAsync($"/api/customers/{provisionalId}", Ct)).StatusCode);
        Assert.Equal(1, await CountCustomersAsync(spa, "Mustermann"));

        var again = await spa.ReceiveAsync("buchungen@treatwell.at", "Neue Buchung über Treatwell", spa.TreatwellMail(reference, "14:00"), Ct);
        Assert.True(again.Status == InboundMailStatus.Ignored, again.Note);
    }

    [Fact]
    public async Task Vorlaeufiger_Treatwell_Gast_mit_weiterem_Termin_bleibt_beim_Zuordnen_stehen()
    {
        var spa = await MailSpa.CreateAsync(fixture, Ct);
        var customer = await NewCustomerAsync(spa, "Max", "Mustermann", $"max-{spa.Guest}", "+43 664 1234000");
        var mail = await spa.ReceiveAsync("buchungen@treatwell.at", "Neue Buchung über Treatwell",
            spa.TreatwellMail($"TW-{Guid.NewGuid():N}"[..14], "14:00"), Ct);
        var provisionalId = mail.CustomerId!.Value;
        // Die Rezeption hat dem vorläufigen Gast inzwischen einen weiteren Termin gegeben.
        var second = await spa.Spa.BookAsync(TestSpa.At(11), Ct, treatment: spa.Treatment, location: spa.Location.Id, customer: provisionalId);
        await TestSpa.ReadAsync<AppointmentDto>(second, Ct);

        var assigned = await spa.Spa.PostAsync<InboundMailDto>($"/api/inbox/{mail.Id}/customer/assign", new AssignCustomerInput(customer.Id), Ct);

        Assert.Equal(customer.Id, (await spa.Spa.GetAsync<AppointmentDto>($"/api/appointments/{mail.AppointmentId}", Ct)).CustomerId);
        Assert.Contains("bleibt bestehen", assigned.Note, StringComparison.Ordinal);
        var stays = await spa.Spa.GetAsync<CustomerDto>($"/api/customers/{provisionalId}", Ct);
        Assert.False(stays.AssignmentOpen);
    }

    [Fact]
    public async Task Bewusst_neu_bestaetigt_den_vorlaeufigen_Treatwell_Gast_statt_einen_zweiten_anzulegen()
    {
        var spa = await MailSpa.CreateAsync(fixture, Ct);
        await NewCustomerAsync(spa, "Max", "Mustermann", $"max-{spa.Guest}", "+43 664 1234000");
        var mail = await spa.ReceiveAsync("buchungen@treatwell.at", "Neue Buchung über Treatwell",
            spa.TreatwellMail($"TW-{Guid.NewGuid():N}"[..14], "14:00"), Ct);
        var provisionalId = mail.CustomerId!.Value;
        Assert.Equal(2, await CountCustomersAsync(spa, "Mustermann"));

        var decided = await spa.Spa.PostAsync<InboundMailDto>($"/api/inbox/{mail.Id}/customer/new", new NewCustomerFromMailInput(), Ct);

        Assert.True(decided.CustomerConfirmed);
        Assert.Equal(provisionalId, decided.CustomerId);
        Assert.Equal(2, await CountCustomersAsync(spa, "Mustermann"));
        Assert.False((await spa.Spa.GetAsync<CustomerDto>($"/api/customers/{provisionalId}", Ct)).AssignmentOpen);
    }

    [Fact]
    public async Task Treatwell_Buchung_mit_gleicher_Mailadresse_wird_sicher_zugeordnet()
    {
        var spa = await MailSpa.CreateAsync(fixture, Ct);
        var customer = await NewCustomerAsync(spa, "Maximilian", "Anders", $"max-{spa.Guest}");

        var mail = await spa.ReceiveAsync("buchungen@treatwell.at", "Neue Buchung über Treatwell",
            spa.TreatwellMail($"TW-{Guid.NewGuid():N}"[..14], "14:00", $"MAX-{spa.Guest}"), Ct);

        Assert.Equal(InboundMailStatus.Booked, mail.Status);
        Assert.Equal(customer.Id, mail.CustomerId);
    }

    [Fact]
    public async Task Dubletten_Pruefung_findet_gleichen_Namen_Mailadresse_und_Nummer()
    {
        var spa = await MailSpa.CreateAsync(fixture, Ct);
        var customer = await NewCustomerAsync(spa, "Sonja", "Winkler", $"alt-{spa.Guest}", "0664 111 22 33");

        var byName = await spa.Spa.PostAsync<List<CustomerCandidateDto>>("/api/customers/duplicates",
            new DuplicateCheckInput("Sonja", "Winkler", null, null), Ct);
        var byPhone = await spa.Spa.PostAsync<List<CustomerCandidateDto>>("/api/customers/duplicates",
            new DuplicateCheckInput("Jemand", "Anders", null, "+43 664 1112233"), Ct);
        var byMail = await spa.Spa.PostAsync<List<CustomerCandidateDto>>("/api/customers/duplicates",
            new DuplicateCheckInput(null, null, $"ALT-{spa.Guest}", null), Ct);
        var none = await spa.Spa.PostAsync<List<CustomerCandidateDto>>("/api/customers/duplicates",
            new DuplicateCheckInput("Völlig", "Neu", "neu@example.at", "0699 000 00 01"), Ct);

        Assert.Equal((customer.Id, 4), (byName.Single().CustomerId, byName.Single().Stage));
        Assert.Equal((customer.Id, 1), (byPhone.Single().CustomerId, byPhone.Single().Stage));
        Assert.Equal("gleiche Telefonnummer", byPhone.Single().Reason);
        Assert.Equal((customer.Id, 1), (byMail.Single().CustomerId, byMail.Single().Stage));
        Assert.Empty(none);

        using var therapist = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Therapist, [spa.Location.Id], spa.Employee.Id);
        var denied = await therapist.PostAsJsonAsync("/api/customers/duplicates", new DuplicateCheckInput("Sonja", "Winkler", null, null), TestSpa.Json, Ct);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
    }

    [Fact]
    public async Task Weitere_Mailadressen_pflegen_pruefen_und_in_der_Suche_finden()
    {
        var spa = await MailSpa.CreateAsync(fixture, Ct);
        var customer = await NewCustomerAsync(spa, "Sonja", "Winkler", $"haupt-{spa.Guest}");
        var path = $"/api/customers/{customer.Id}/emails";

        var added = await spa.Spa.PostAsync<List<CustomerEmailDto>>(path, new CustomerEmailInput($" Privat-{spa.Guest} "), Ct);
        Assert.Equal($"Privat-{spa.Guest}", Assert.Single(added).Address);

        foreach (var invalid in new[] { "keine-adresse", $"privat-{spa.Guest}", $"HAUPT-{spa.Guest}" })
        {
            var response = await spa.Spa.Client.PostAsJsonAsync(path, new CustomerEmailInput(invalid), TestSpa.Json, Ct);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        Assert.Single(await spa.Spa.GetAsync<List<CustomerDto>>($"/api/customers?search={Uri.EscapeDataString($"privat-{spa.Guest}")}", Ct));

        using var therapist = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Therapist, [spa.Location.Id], spa.Employee.Id);
        var denied = await therapist.PostAsJsonAsync(path, new CustomerEmailInput($"x-{spa.Guest}"), TestSpa.Json, Ct);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);

        var removed = await spa.Spa.Client.DeleteAsync($"{path}/{added[0].Id}", Ct);
        Assert.Equal(HttpStatusCode.OK, removed.StatusCode);
        Assert.Empty(await spa.Spa.GetAsync<List<CustomerEmailDto>>(path, Ct));
    }
}
