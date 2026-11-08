using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using AispaStudio.Api.Online;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AispaStudio.Api.Tests;

/// <summary>Anamnese am Tablet, auf Papier mit QR-Code und als fotografierter Papierbogen mit KI-Vorschlag.</summary>
public sealed class AnamnesisChannelTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static List<AnamnesisAnswer> Answers(string? allergies = "Lavendelöl") =>
    [
        new("allergies", allergies is not null, allergies),
        new("skin", false, null),
        new("circulation", false, null),
        new("pregnancy", false, null),
        new("medication", false, null),
        new("surgery", false, null),
        new("other", null, null),
    ];

    private static string Token(string url) => url.Split("#/anamnese/")[1].Split('?')[0];

    private static async Task<HttpResponseMessage> UploadAsync(TestSpa spa, Guid customerId, byte[] photo, Guid? appointmentId = null, HttpClient? client = null)
    {
        using var content = new MultipartFormDataContent();
        var file = new ByteArrayContent(photo);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        content.Add(file, "file", "bogen.jpg");
        if (appointmentId is { } id)
        {
            content.Add(new StringContent(id.ToString()), "appointmentId");
        }

        return await (client ?? spa.Client).PostAsync($"/api/customers/{customerId}/anamnesis/scans", content, Ct);
    }

    private static async Task<AnamnesisScanDto> WaitReadAsync(TestSpa spa, Guid customerId, Guid scanId)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));
        while (true)
        {
            var scan = await spa.GetAsync<AnamnesisScanDto>($"/api/customers/{customerId}/anamnesis/scans/{scanId}", Ct);
            if (scan.Status != AnamnesisScanStatus.Reading)
            {
                return scan;
            }

            await Task.Delay(50, timeout.Token);
        }
    }

    [Fact]
    public async Task Tablet_Link_gilt_nur_einmal_und_speichert_den_Kanal()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var appointment = await spa.BookOkAsync(TestSpa.At(10), Ct);
        var link = await spa.PostAsync<AnamnesisTabletLink>($"/api/customers/{spa.Customer.Id}/anamnesis/tablet",
            new AnamnesisLinkRequest(appointment.Id, "de"), Ct);
        using var tablet = fixture.CreateClient();
        var token = Token(link.Url);

        var info = await tablet.GetFromJsonAsync<AnamnesisLinkInfo>($"/api/online/anamnesis/{token}", TestSpa.Json, Ct);
        var submitted = await tablet.PostAsJsonAsync($"/api/online/anamnesis/{token}",
            new AnamnesisSubmission(Answers(), true, "Eva Muster", "de"), TestSpa.Json, Ct);
        var again = await tablet.GetAsync($"/api/online/anamnesis/{token}", Ct);

        Assert.Contains("/buchen/#/anamnese/", link.Url, StringComparison.Ordinal);
        Assert.True(link.ExpiresAt < DateTimeOffset.UtcNow.AddHours(1));
        Assert.Equal("Tablet", info!.Channel);
        Assert.Equal("Massage", info.TreatmentName);
        Assert.Equal(HttpStatusCode.NoContent, submitted.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, again.StatusCode);
        var record = await spa.GetAsync<AnamnesisRecordDto>($"/api/customers/{spa.Customer.Id}/anamnesis", Ct);
        Assert.Equal("Tablet", record.Channel);
        Assert.Equal(appointment.Id, record.AppointmentId);
    }

    [Fact]
    public async Task Papierbogen_enthaelt_Fragen_Kunde_und_QR_Link()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var appointment = await spa.BookOkAsync(TestSpa.At(10), Ct);

        var sheet = await spa.PostAsync<AnamnesisPaperSheet>($"/api/customers/{spa.Customer.Id}/anamnesis/paper",
            new AnamnesisLinkRequest(appointment.Id, "en"), Ct);
        using var phone = fixture.CreateClient();
        var info = await phone.GetFromJsonAsync<AnamnesisLinkInfo>($"/api/online/anamnesis/{Token(sheet.Url)}", TestSpa.Json, Ct);

        Assert.Equal("en", sheet.Form.Language);
        Assert.Equal(7, sheet.Form.Questions.Count);
        Assert.Equal($"Eva {spa.Customer.LastName}", sheet.CustomerName);
        Assert.Equal("Wien", sheet.LocationName);
        Assert.Equal("Massage", sheet.TreatmentName);
        Assert.EndsWith("?lang=en", sheet.Url, StringComparison.Ordinal);
        Assert.Equal("Link", info!.Channel);
    }

    [Fact]
    public async Task Fremder_Termin_oder_unbekannter_Kunde_wird_abgelehnt()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var other = await spa.PostAsync<CustomerDto>("/api/customers", new CustomerInput("Max", "Anders", null, null, null, null), Ct);
        var appointment = await spa.BookOkAsync(TestSpa.At(10), Ct);

        var wrongAppointment = await spa.Client.PostAsJsonAsync($"/api/customers/{other.Id}/anamnesis/tablet",
            new AnamnesisLinkRequest(appointment.Id, null), TestSpa.Json, Ct);
        var unknown = await spa.Client.PostAsJsonAsync($"/api/customers/{Guid.NewGuid()}/anamnesis/paper",
            new AnamnesisLinkRequest(null, null), TestSpa.Json, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, wrongAppointment.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
    }

    [Fact]
    public async Task Foto_wird_verschluesselt_gelesen_unsicheres_markiert_und_nach_Pruefung_uebernommen()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var appointment = await spa.BookOkAsync(TestSpa.At(10), Ct);
        var photo = FakeAnamnesisReader.Photo(new AnamnesisReading(
            [
                new ReadAnswer("allergies", true, "Lavendel?", 0.4, "Schrift schwer lesbar"),
                new ReadAnswer("skin", false, null, 0.97, null),
                new ReadAnswer("circulation", false, null, 0.95, null),
                new ReadAnswer("pregnancy", null, null, 0.9, null),
                new ReadAnswer("medication", false, null, 0.92, null),
                new ReadAnswer("surgery", false, null, 0.99, null),
                new ReadAnswer("erfunden", true, "nicht auf dem Bogen", 1, null),
            ],
            "Eva Muster", 0.9, true, null));

        var upload = await UploadAsync(spa, spa.Customer.Id, photo, appointment.Id);
        Assert.Equal(HttpStatusCode.Accepted, upload.StatusCode);
        var uploaded = await TestSpa.ReadAsync<AnamnesisScanDto>(upload, Ct);
        var scan = await WaitReadAsync(spa, spa.Customer.Id, uploaded.Id);

        Assert.Equal(AnamnesisScanStatus.ReadyForReview, scan.Status);
        Assert.Equal("Testleser", scan.Reader);
        Assert.Equal(["allergies", "skin", "circulation", "pregnancy", "medication", "surgery", "other"], scan.Answers.Select(a => a.Key));
        Assert.True(scan.Answers.Single(a => a.Key == "allergies").Uncertain);
        Assert.Equal("Schrift schwer lesbar", scan.Answers.Single(a => a.Key == "allergies").Reason);
        Assert.True(scan.Answers.Single(a => a.Key == "pregnancy").Uncertain);
        Assert.True(scan.Answers.Single(a => a.Key == "other").Uncertain);
        Assert.False(scan.Answers.Single(a => a.Key == "skin").Uncertain);
        Assert.Equal("Eva Muster", scan.Signature);
        Assert.False(scan.SignatureUncertain);
        Assert.True(scan.ConsentChecked);

        var image = await spa.Client.GetByteArrayAsync($"/api/customers/{spa.Customer.Id}/anamnesis/scans/{scan.Id}/image", Ct);
        Assert.Equal(photo, image);
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var stored = await scope.ServiceProvider.GetRequiredService<OnlineDbContext>().AnamnesisScans.AsNoTracking()
                .SingleAsync(s => s.Id == scan.Id, Ct);
            Assert.DoesNotContain("Lavendel", Encoding.UTF8.GetString(stored.Image), StringComparison.Ordinal);
            Assert.DoesNotContain("Lavendel", Encoding.UTF8.GetString(stored.Reading!), StringComparison.Ordinal);
        }

        var overview = await spa.GetAsync<AnamnesisOverview>($"/api/customers/{spa.Customer.Id}/anamnesis/overview", Ct);
        Assert.Equal(scan.Id, Assert.Single(overview.OpenScans).Id);

        var noConsent = await spa.Client.PostAsJsonAsync($"/api/customers/{spa.Customer.Id}/anamnesis/scans/{scan.Id}/confirm",
            new AnamnesisScanConfirmation(Answers("Lavendelöl"), false, true, "Eva Muster", null), TestSpa.Json, Ct);
        var confirmed = await spa.Client.PostAsJsonAsync($"/api/customers/{spa.Customer.Id}/anamnesis/scans/{scan.Id}/confirm",
            new AnamnesisScanConfirmation(Answers("Lavendelöl"), true, true, "Eva Muster", null), TestSpa.Json, Ct);
        var twice = await spa.Client.PostAsJsonAsync($"/api/customers/{spa.Customer.Id}/anamnesis/scans/{scan.Id}/confirm",
            new AnamnesisScanConfirmation(Answers("Lavendelöl"), true, true, "Eva Muster", null), TestSpa.Json, Ct);
        var discard = await spa.Client.DeleteAsync($"/api/customers/{spa.Customer.Id}/anamnesis/scans/{scan.Id}", Ct);

        Assert.Equal(HttpStatusCode.BadRequest, noConsent.StatusCode);
        Assert.Equal(HttpStatusCode.OK, confirmed.StatusCode);
        var record = await TestSpa.ReadAsync<AnamnesisRecordDto>(confirmed, Ct);
        Assert.Equal("Papier", record.Channel);
        Assert.Equal(appointment.Id, record.AppointmentId);
        Assert.Equal("Lavendelöl", record.Answers.Single(a => a.Key == "allergies").Details);
        Assert.Equal(HttpStatusCode.Conflict, twice.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, discard.StatusCode);
        overview = await spa.GetAsync<AnamnesisOverview>($"/api/customers/{spa.Customer.Id}/anamnesis/overview", Ct);
        Assert.Empty(overview.OpenScans);
        Assert.Equal("Papier", overview.Channel);
        Assert.NotNull(overview.ValidUntil);
    }

    [Fact]
    public async Task Unlesbares_Foto_bleibt_pruefbar_und_laesst_sich_verwerfen()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var upload = await UploadAsync(spa, spa.Customer.Id, FakeAnamnesisReader.Unreadable());
        var scan = await WaitReadAsync(spa, spa.Customer.Id, (await TestSpa.ReadAsync<AnamnesisScanDto>(upload, Ct)).Id);

        Assert.Equal(AnamnesisScanStatus.ReadyForReview, scan.Status);
        Assert.Equal("Manuell", scan.Reader);
        Assert.All(scan.Answers, a => Assert.True(a.Uncertain));
        Assert.True(scan.SignatureUncertain);
        Assert.NotNull(scan.Note);

        var discard = await spa.Client.DeleteAsync($"/api/customers/{spa.Customer.Id}/anamnesis/scans/{scan.Id}", Ct);
        var gone = await spa.Client.GetAsync($"/api/customers/{spa.Customer.Id}/anamnesis/scans/{scan.Id}", Ct);
        Assert.Equal(HttpStatusCode.NoContent, discard.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, gone.StatusCode);
    }

    [Fact]
    public async Task Nur_Bilder_und_nur_wer_buchen_darf()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        using var therapist = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Therapist, [spa.Location.Id], spa.Anna.Id);

        var text = await UploadAsync(spa, spa.Customer.Id, "Das ist kein Foto"u8.ToArray());
        var byTherapist = await UploadAsync(spa, spa.Customer.Id, FakeAnamnesisReader.Unreadable(), client: therapist);
        var overview = await therapist.GetAsync($"/api/customers/{spa.Customer.Id}/anamnesis/overview", Ct);

        Assert.Equal(HttpStatusCode.BadRequest, text.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, byTherapist.StatusCode);
        Assert.Equal(HttpStatusCode.OK, overview.StatusCode);
    }

    /// <summary>
    /// Die Rechtetabelle der Anamnese (Rulings des Auftraggebers, 31.10. und 02.11.2026, R05 Slice 128; seit Ä309 liest die Rezeption wie alle, die Kunden sehen): je Rolle der erwartete Status für Stand, Prüfhinweis, Antworten,
    /// Tablet, Papier, Hochladen, KI-Vorschlag, Foto, Übernehmen und Verwerfen. Von Hand aus der Entscheidung abgeschrieben, nicht aus <c>Roles</c>.
    /// </summary>
    public static TheoryData<StaffRole, int[]> RightsTable => new()
    {
        { StaffRole.Owner, [200, 200, 200, 200, 200, 202, 200, 200, 200, 204] },
        { StaffRole.GeneralManager, [200, 200, 200, 200, 200, 202, 200, 200, 200, 204] },
        { StaffRole.Manager, [200, 200, 200, 200, 200, 202, 200, 200, 200, 204] },
        { StaffRole.Reception, [200, 200, 200, 200, 200, 202, 200, 200, 200, 204] },
        { StaffRole.Therapist, [200, 200, 200, 403, 403, 403, 200, 200, 200, 204] },
        { StaffRole.Backoffice, [403, 403, 403, 403, 403, 403, 403, 403, 403, 403] },
    };

    [Theory]
    [MemberData(nameof(RightsTable))]
    public async Task Jede_Rolle_bekommt_bei_jedem_Anamnese_Aufruf_die_Antwort_der_Rechtetabelle(StaffRole role, int[] expected)
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var link = await spa.PostAsync<AnamnesisTabletLink>($"/api/customers/{spa.Customer.Id}/anamnesis/tablet", new AnamnesisLinkRequest(null, "de"), Ct);
        using var tablet = fixture.CreateClient();
        var submitted = await tablet.PostAsJsonAsync($"/api/online/anamnesis/{Token(link.Url)}",
            new AnamnesisSubmission(Answers(), true, "Eva Muster", "de"), TestSpa.Json, Ct);
        Assert.Equal(HttpStatusCode.NoContent, submitted.StatusCode);
        var toConfirm = await WaitReadAsync(spa, spa.Customer.Id,
            (await TestSpa.ReadAsync<AnamnesisScanDto>(await UploadAsync(spa, spa.Customer.Id, FakeAnamnesisReader.Unreadable()), Ct)).Id);
        var toDiscard = await WaitReadAsync(spa, spa.Customer.Id,
            (await TestSpa.ReadAsync<AnamnesisScanDto>(await UploadAsync(spa, spa.Customer.Id, FakeAnamnesisReader.Unreadable()), Ct)).Id);
        using var user = await TestUsers.ClientAsync(fixture, Ct, role, [spa.Location.Id], role == StaffRole.Therapist ? spa.Anna.Id : null);
        var path = $"/api/customers/{spa.Customer.Id}/anamnesis";

        var actual = new[]
        {
            await user.GetAsync($"{path}/overview", Ct),
            await user.GetAsync($"{path}/check?treatmentId={spa.MassageTreatment.Id}", Ct),
            await user.GetAsync(path, Ct),
            await user.PostAsJsonAsync($"{path}/tablet", new AnamnesisLinkRequest(null, "de"), TestSpa.Json, Ct),
            await user.PostAsJsonAsync($"{path}/paper", new AnamnesisLinkRequest(null, "de"), TestSpa.Json, Ct),
            await UploadAsync(spa, spa.Customer.Id, FakeAnamnesisReader.Unreadable(), client: user),
            await user.GetAsync($"{path}/scans/{toConfirm.Id}", Ct),
            await user.GetAsync($"{path}/scans/{toConfirm.Id}/image", Ct),
            await user.PostAsJsonAsync($"{path}/scans/{toConfirm.Id}/confirm",
                new AnamnesisScanConfirmation(Answers(), true, true, "Eva Muster", null), TestSpa.Json, Ct),
            await user.DeleteAsync($"{path}/scans/{toDiscard.Id}", Ct),
        };

        Assert.Equal(expected, actual.Select(r => (int)r.StatusCode));
    }

    [Fact]
    public async Task Rezeption_laedt_ein_Foto_hoch_und_die_Antwort_enthaelt_keine_Antworten()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        using var reception = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Reception, [spa.Location.Id]);

        var upload = await UploadAsync(spa, spa.Customer.Id, FakeAnamnesisReader.Photo(new AnamnesisReading(
            [new ReadAnswer("allergies", true, "Lavendel", 0.99, null)], "Eva Muster", 0.99, true, null)), client: reception);

        Assert.Equal(HttpStatusCode.Accepted, upload.StatusCode);
        var scan = await TestSpa.ReadAsync<AnamnesisScanDto>(upload, Ct);
        Assert.Equal(AnamnesisScanStatus.Reading, scan.Status);
        Assert.Empty(scan.Answers);
        Assert.Null(scan.Signature);
    }

    [Theory]
    [InlineData(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }, "image/jpeg")]
    [InlineData(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0 }, "image/png")]
    [InlineData(new byte[] { 0x52, 0x49, 0x46, 0x46, 1, 2, 3, 4, 0x57, 0x45, 0x42, 0x50 }, "image/webp")]
    [InlineData(new byte[] { 0x25, 0x50, 0x44, 0x46 }, null)]
    public void Bildformat_wird_am_Inhalt_erkannt(byte[] data, string? expected) =>
        Assert.Equal(expected, AnamnesisStaffEndpoints.ImageType(data));
}
