using System.Net;
using System.Net.Http.Json;
using AispaStudio.Api.Data;
using AispaStudio.Api.Documents;
using AispaStudio.Api.Pos;
using AispaStudio.Api.Scheduling;
using AispaStudio.Api.Tenancy;
using AispaStudio.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace AispaStudio.Api.Tests;

/// <summary>
/// Datenschutz-Auskunft nach Art. 15 DSGVO als PDF (PDF-Ausgaben C, ADR 0091): Stammdaten, Termine mit Status, Zahlungen, Gutscheine, Online-Konto und Verlauf der Änderungen;
/// die Anamnese nur mit dem Recht, sie zu lesen, sonst ein fester Satz; Recht wie die Kundenkarte, fremder Mandant 404.
/// </summary>
public sealed class CustomerDisclosureTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static async Task<byte[]> DisclosureAsync(HttpClient client, Guid customerId)
    {
        var response = await client.GetAsync($"/api/customers/{customerId}/history/pdf", Ct);
        response.EnsureSuccessStatusCode();
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        return await response.Content.ReadAsByteArrayAsync(Ct);
    }

    private static async Task SubmitAnamnesisAsync(TestSpa spa, Guid customerId)
    {
        var link = await spa.PostAsync<AnamnesisTabletLink>($"/api/customers/{customerId}/anamnesis/tablet", new AnamnesisLinkRequest(null, "de"), Ct);
        using var tablet = spa.Fixture.CreateClient();
        var token = link.Url.Split("#/anamnese/")[1].Split('?')[0];
        List<AnamnesisAnswer> answers =
        [
            new("allergies", true, "Nüsse"), new("skin", false, null), new("circulation", false, null),
            new("pregnancy", false, null), new("medication", false, null), new("surgery", false, null), new("other", null, null),
        ];
        var submitted = await tablet.PostAsJsonAsync($"/api/online/anamnesis/{token}", new AnamnesisSubmission(answers, true, "Eva Muster", "de"), TestSpa.Json, Ct);
        Assert.Equal(HttpStatusCode.NoContent, submitted.StatusCode);
    }

    private static async Task AddVoucherAsync(TestSpa spa, Guid customerId, Guid receiptId)
    {
        await using var scope = spa.Fixture.Factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Use(spa.TenantId);
        var db = scope.ServiceProvider.GetRequiredService<AispaDbContext>();
        db.Vouchers.Add(new Voucher
        {
            Code = "AUSKUNFT-0001",
            Kind = VoucherKind.MultiPurpose,
            InitialValue = 50m,
            Balance = 35m,
            CustomerId = customerId,
            SoldOnReceiptId = receiptId,
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync(Ct);
    }

    [Fact]
    public async Task Die_Auskunft_enthaelt_Stammdaten_Termine_mit_Status_Zahlungen_Gutscheine_und_Verlauf()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await spa.PutAsync<CustomerDto>($"/api/customers/{spa.Customer.Id}", new CustomerInput("Eva", spa.Customer.LastName, "eva@example.at", "+43 664 111", new DateOnly(1985, 4, 12),
            "Mag Lavendel", Address: new CustomerAddress("Graben 1", "1010", "Wien", "AT")), Ct);
        var booked = await spa.BookOkAsync(TestSpa.At(10), Ct, spa.Anna.Id);
        var done = await spa.BookOkAsync(TestSpa.At(12), Ct, spa.Anna.Id);
        await spa.SetStatusAsync(done.Id, AppointmentStatus.Completed, Ct);
        var receipt = await ReceiptPdfTests.SellTeaAsync(spa);
        await AddVoucherAsync(spa, spa.Customer.Id, receipt.Id);

        var pdf = await DisclosureAsync(spa.Client, spa.Customer.Id);

        PdfText.Keep(pdf, "auskunft");
        var text = PdfText.Text(pdf);
        Assert.Contains("Auskunft über gespeicherte Daten", text, StringComparison.Ordinal);
        Assert.Contains("Art. 15 DSGVO", text, StringComparison.Ordinal);
        Assert.Contains(spa.Customer.LastName, text, StringComparison.Ordinal);
        Assert.Contains("12.04.1985", text, StringComparison.Ordinal);
        Assert.Contains("Graben 1", text, StringComparison.Ordinal);
        Assert.Contains("eva@example.at", text, StringComparison.Ordinal);
        Assert.Contains("+43 664 111", text, StringComparison.Ordinal);
        Assert.Contains("Mag Lavendel", text, StringComparison.Ordinal);
        Assert.Contains("01.03.2027 10:00", text, StringComparison.Ordinal);
        Assert.Contains("Gebucht", text, StringComparison.Ordinal);
        Assert.Contains("Erledigt", text, StringComparison.Ordinal);
        Assert.Contains($"Beleg Nr. {receipt.RegisterCode}-{receipt.Number}", text, StringComparison.Ordinal);
        Assert.Contains("25,00 EUR", text, StringComparison.Ordinal);
        Assert.Contains("AUSKUNFT-0001", text, StringComparison.Ordinal);
        Assert.Contains("35,00 EUR", text, StringComparison.Ordinal);
        Assert.Contains("Nein, es gibt kein Online-Konto", text, StringComparison.Ordinal);
        Assert.Contains("Verlauf der Änderungen", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Keine Änderungen vermerkt", text, StringComparison.Ordinal);
        Assert.DoesNotContain(booked.Id.ToString(), text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Mit_dem_Recht_Anamnese_zu_lesen_stehen_die_Antworten_im_Dokument()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await SubmitAnamnesisAsync(spa, spa.Customer.Id);

        var pdf = await DisclosureAsync(spa.Client, spa.Customer.Id);

        PdfText.Keep(pdf, "auskunft-anamnese");
        var text = PdfText.Text(pdf);
        Assert.Contains("Anamnesebogen vom", text, StringComparison.Ordinal);
        Assert.Contains("Nüsse", text, StringComparison.Ordinal);
        Assert.DoesNotContain(CustomerDisclosure.HealthWithheld, text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Die_Rezeption_darf_die_Anamnese_lesen_und_bekommt_sie_im_Dokument()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await SubmitAnamnesisAsync(spa, spa.Customer.Id);
        using var reception = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Reception, [spa.Location.Id], spa.Anna.Id);

        var text = PdfText.Text(await DisclosureAsync(reception, spa.Customer.Id));

        Assert.Contains("Nüsse", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Ohne_das_Recht_steht_nur_der_feste_Satz_und_keine_Antwort_im_Dokument()
    {
        var customer = new Customer { FirstName = "Eva", LastName = "Muster" };
        var header = new DocumentHeader("Test-Spa", [], [], DateTimeOffset.UtcNow, TimeZoneInfo.Utc, "Lena");
        var answers = new AnamnesisRecordDto(Guid.NewGuid(), customer.Id, null, "1", "Tablet", DateTimeOffset.UtcNow, "Eva Muster", [new AnamnesisAnswer("allergies", true, "Nüsse")]);
        CustomerDisclosure.Data Data(CustomerDisclosure.Health health) =>
            new(customer, [], null, [], [], [], [], false, health, []);

        var without = PdfText.Text(CustomerDisclosure.Build(Data(new CustomerDisclosure.Health(true, null)), header));
        var with = PdfText.Text(CustomerDisclosure.Build(Data(new CustomerDisclosure.Health(true, answers)), header));
        var none = PdfText.Text(CustomerDisclosure.Build(Data(new CustomerDisclosure.Health(false, null)), header));

        Assert.Contains("Gesundheitsangaben: vorhanden, Auskunft durch die Leitung", without, StringComparison.Ordinal);
        Assert.DoesNotContain("Nüsse", without, StringComparison.Ordinal);
        Assert.DoesNotContain("Allergien", without, StringComparison.Ordinal);
        Assert.Contains("Nüsse", with, StringComparison.Ordinal);
        Assert.Contains("Keine Gesundheitsangaben gespeichert.", none, StringComparison.Ordinal);
        PdfText.Keep(CustomerDisclosure.Build(Data(new CustomerDisclosure.Health(true, null)), header), "auskunft-ohne-anamnese-recht");
    }

    [Fact]
    public async Task Ein_Gast_ohne_Daten_bekommt_eine_Auskunft_mit_leeren_Abschnitten()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var guest = await spa.PostAsync<CustomerDto>("/api/customers", new CustomerInput("Paul", $"Neu{Guid.NewGuid():N}", null, null, null, null), Ct);

        var text = PdfText.Text(await DisclosureAsync(spa.Client, guest.Id));

        Assert.Contains("Keine Termine.", text, StringComparison.Ordinal);
        Assert.Contains("Keine Zahlungen.", text, StringComparison.Ordinal);
        Assert.Contains("Keine Gutscheine.", text, StringComparison.Ordinal);
        Assert.Contains("Keine Gesundheitsangaben gespeichert.", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Das_Recht_ist_das_der_Kundenkarte_und_ein_fremdes_Spa_bekommt_404()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var other = await TestSpa.CreateAsync(fixture, Ct);
        using var backoffice = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Backoffice, [spa.Location.Id]);

        var forbidden = await backoffice.GetAsync($"/api/customers/{spa.Customer.Id}/history/pdf", Ct);
        var foreign = await other.Client.GetAsync($"/api/customers/{spa.Customer.Id}/history/pdf", Ct);

        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
    }
}
