using AispaStudio.Contracts;

namespace AispaStudio.Api.Tests;

/// <summary>
/// Betriebsfragen zu Angebot, Gästen und Gutscheinen (ADR 0072, Szenario 3): Preis und Dauer, wer eine Behandlung anbietet, wann ein Gast zuletzt da war, ob er einen
/// Termin hat, was auf einem Gutschein ist. Aus Katalog, Qualifikationen, Kalender und Kassa; Gäste nur mit Kalenderrecht und ohne Kontakt- oder Gesundheitsdaten.
/// </summary>
public sealed class BusinessCatalogGuestTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly AnswerKit _kit = new(fixture);

    private static Task<CustomerDto> GuestAsync(TestSpa spa, string first, string last) =>
        spa.PostAsync<CustomerDto>("/api/customers", new CustomerInput(first, last, null, null, null, null), Ct);

    // ---------- Angebot ----------

    [Fact]
    public async Task Preis_und_Dauer_einer_Behandlung_aus_dem_Katalog_des_Standorts()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await spa.PostAsync<TreatmentDto>("/api/treatments", new TreatmentInput("Hot Stone Massage", 90, 0, 0, 129m, [spa.Massage.Id], [spa.RoomA.Id], []), Ct);

        var massage = await _kit.AnswerAsync(spa, "Was kostet eine Massage und wie lange dauert sie?");
        var hotStone = await _kit.AnswerAsync(spa, "Was kostet Hot Stone Massage?");
        var both = await _kit.AnswerAsync(spa, "Was kostet die Massage und was kostet die Hot Stone Massage?");

        Assert.Equal("Massage kostet 79,00 EUR und dauert 50 Minuten (dazu 15 Minuten Vor- und Nachbereitung, in denen der Raum belegt ist).", massage.Text);
        Assert.Equal("Behandlungskatalog des Standorts Wien", massage.Source);
        Assert.Equal("Hot Stone Massage kostet 129,00 EUR und dauert 90 Minuten.", hotStone.Text);
        Assert.Equal([["Hot Stone Massage", "129,00 EUR", "90 Minuten"], ["Massage", "79,00 EUR", "50 Minuten"]], both.Table!.Rows);
    }

    [Fact]
    public async Task Ein_Teil_des_Namens_genuegt_wenn_er_den_Katalog_eindeutig_trifft()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await spa.PostAsync<TreatmentDto>("/api/treatments", new TreatmentInput("Lava-Hot-Stone-Massage (60 Min.)", 60, 0, 0, 89m, [spa.Massage.Id], [spa.RoomA.Id], []), Ct);
        await spa.PostAsync<TreatmentDto>("/api/treatments", new TreatmentInput("Lava-Hot-Stone-Massage (90 Min.)", 90, 0, 0, 119m, [spa.Massage.Id], [spa.RoomA.Id], []), Ct);

        var both = await _kit.AnswerAsync(spa, "Was kostet Hot Stone und wie lange dauert sie?");
        var exact = await _kit.AnswerAsync(spa, "Was kostet die Lava-Hot-Stone-Massage (90 Min.)?");
        var who = await _kit.AnswerAsync(spa, "Wer macht Hot Stone?");

        Assert.Equal([["Lava-Hot-Stone-Massage (60 Min.)", "89,00 EUR", "60 Minuten"], ["Lava-Hot-Stone-Massage (90 Min.)", "119,00 EUR", "90 Minuten"]], both.Table!.Rows);
        Assert.Equal("Lava-Hot-Stone-Massage (90 Min.) kostet 119,00 EUR und dauert 90 Minuten.", exact.Text);
        Assert.StartsWith("Lava-Hot-Stone-Massage (60 Min.): Anna, Ben.", who.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Eine_unbekannte_Behandlung_nennt_den_Katalog_und_eine_ohne_Raum_am_Standort_sagt_das()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var other = await AnswerKit.LocationAsync(spa, "Ohne Raum", null);

        var unknown = await _kit.AnswerAsync(spa, "Was kostet das Aromaritual?");
        var elsewhere = await _kit.AnswerAsync(spa, "Was kostet eine Massage?", locationId: other.Id);

        Assert.Equal(AssistantBlockKind.NotFound, unknown.Kind);
        Assert.Equal("Welche Behandlung ist gemeint? Im Katalog des Standorts stehen:", unknown.Text);
        Assert.Equal([["Massage"]], unknown.Table!.Rows);
        Assert.Equal("Massage gibt es im Katalog, sie wird im Standort Ohne Raum aber nicht angeboten (es fehlt ein Raum oder eine qualifizierte Person).", elsewhere.Text);
    }

    [Fact]
    public async Task Wer_eine_Behandlung_anbietet_entscheiden_die_Qualifikationen_nicht_der_Name_im_Satz()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var thai = await spa.PostAsync<QualificationDto>("/api/qualifications", new QualificationInput("Thai-Massage"), Ct);
        await spa.CreateEmployeeAsync("Cora", [spa.Massage.Id, thai.Id], spa.Location.Id, Ct);
        await spa.PostAsync<TreatmentDto>("/api/treatments", new TreatmentInput("Thai Fußmassage", 40, 0, 0, 55m, [thai.Id], [spa.RoomA.Id], []), Ct);
        await spa.CreateEmployeeAsync("Dora", [], spa.Location.Id, Ct);

        var massage = await _kit.AnswerAsync(spa, "Wer macht Massagen?");
        var thaiFoot = await _kit.AnswerAsync(spa, "Wer bietet Thai Fußmassage an?");
        var qualification = await _kit.AnswerAsync(spa, "Wer bietet Thai-Massage an?");

        Assert.Equal("Massage: Anna, Ben, Cora.", massage.Text);
        Assert.Equal("Behandlungskatalog des Standorts Wien und Qualifikationen des Teams", massage.Source);
        Assert.Equal([["Massage", "Anna"], ["Massage", "Ben"], ["Massage", "Cora"]], massage.Table!.Rows);
        Assert.Equal("Thai Fußmassage: Cora.", thaiFoot.Text);
        Assert.Equal("Die Qualifikation „Thai-Massage“ haben: Cora.", qualification.Text);
    }

    // ---------- Gäste ----------

    [Fact]
    public async Task Wann_ein_Gast_zuletzt_da_war_steht_im_Kalender_ohne_Kontaktdaten()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var guest = await spa.PostAsync<CustomerDto>("/api/customers", new CustomerInput("Brigitte", "Müller", "bm@example.at", "+43 664 1234567", null, null), Ct);
        var visit = await TestSpa.ReadAsync<AppointmentDto>(await spa.BookAsync(TestSpa.At(10), Ct, spa.Anna.Id, customer: guest.Id), Ct);
        await spa.PostAsync<AppointmentDto>($"/api/appointments/{visit.Id}/status", new AppointmentStatusChange(AppointmentStatus.CheckedIn), Ct);
        await spa.PostAsync<AppointmentDto>($"/api/appointments/{visit.Id}/status", new AppointmentStatusChange(AppointmentStatus.Completed), Ct);
        await spa.BookOkAsync(TestSpa.At(11, date: TestSpa.Monday.AddDays(14)), Ct, spa.Anna.Id);

        var block = await _kit.AnswerAsync(spa, "Wann war Frau Müller zuletzt da?");
        var none = await _kit.AnswerAsync(spa, "Wann war Frau Zzyzx zuletzt da?");
        var noVisit = await _kit.AnswerAsync(spa, $"Wann war Frau {spa.Customer.FirstName} zuletzt da?");

        Assert.Equal("Brigitte Müller war zuletzt am Montag, 01.03.2027, 10:00 Uhr da (Massage).", block.Text);
        Assert.Equal("Termine im Kalender", block.Source);
        Assert.DoesNotContain("1234567", block.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("example.at", block.Text, StringComparison.Ordinal);
        Assert.Null(block.Copy);
        Assert.Equal(AssistantBlockKind.NotFound, none.Kind);
        Assert.Equal("Keinen Gast „Zzyzx“ gefunden.", none.Text);
        Assert.Contains("kein abgeschlossener Besuch", noVisit.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Mehrere_Gaeste_mit_dem_Namen_stehen_zur_Auswahl_mit_dem_naechsten_Termin()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var anna = await GuestAsync(spa, "Anna", "Huber");
        var berta = await GuestAsync(spa, "Berta", "Huber");
        await spa.BookOkAsync(TestSpa.At(10, date: TestSpa.Monday.AddDays(7)), Ct, spa.Ben.Id, customer: anna.Id);

        var block = await _kit.AnswerAsync(spa, "Wann war Frau Huber zuletzt da?");

        Assert.Equal("Der Name passt zu 2 Gästen. Bitte den Gast wählen.", block.Text);
        Assert.Equal(["Anna Huber", "–", "Montag, 08.03.2027, 10:00 Uhr"], block.Table!.Rows[0]);
        Assert.Equal(["Berta Huber", "–", "kein Termin geplant"], block.Table.Rows[1]);
        Assert.Equal([anna.Id, berta.Id], block.FollowUps.Select(f => f.GuestId));
        Assert.All(block.FollowUps, f => Assert.Equal("Wann war Frau Huber zuletzt da?", f.Question));
    }

    [Fact]
    public async Task Ob_ein_Gast_naechste_Woche_einen_Termin_hat_nennt_Tag_Behandlung_und_Person()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var berger = await GuestAsync(spa, "Hans", "Berger");
        var nextMonday = TestSpa.Monday.AddDays(7);
        await spa.BookAsync(TestSpa.At(14, date: nextMonday), Ct, spa.Ben.Id, customer: berger.Id);
        await spa.BookAsync(TestSpa.At(10, date: nextMonday.AddDays(14)), Ct, spa.Anna.Id, customer: berger.Id);

        var next = await _kit.AnswerAsync(spa, "Hat Herr Berger nächste Woche einen Termin?");
        var later = await _kit.AnswerAsync(spa, "Hat Herr Berger diese Woche einen Termin?");

        Assert.Equal("Hans Berger: 1 Termin 08.03.2027 bis 14.03.2027.", next.Text);
        Assert.Equal(["Montag, 08.03.2027", "14:00 Uhr", "Massage", "Ben"], next.Table!.Rows[0]);
        Assert.Equal("08.03.2027 bis 14.03.2027", next.Period);
        Assert.Equal("Hans Berger: 01.03.2027 bis 07.03.2027 kein Termin; der nächste ist am Montag, 08.03.2027, 14:00 Uhr (Massage).", later.Text);
        Assert.Null(later.Table);
    }

    [Fact]
    public async Task Gaeste_sieht_nur_wer_den_Kalender_sieht_und_nur_an_den_eigenen_Standorten()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var guest = await GuestAsync(spa, "Hans", "Berger");
        var visit = await TestSpa.ReadAsync<AppointmentDto>(await spa.BookAsync(TestSpa.At(10), Ct, spa.Anna.Id, customer: guest.Id), Ct);
        await spa.PostAsync<AppointmentDto>($"/api/appointments/{visit.Id}/status", new AppointmentStatusChange(AppointmentStatus.CheckedIn), Ct);
        var other = await spa.CreateLocationAsync("Graz", [DayOfWeek.Monday], Ct);
        using var grazOnly = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Reception, [other.Id]);

        var backoffice = await _kit.AnswerAsync(spa, "Wann war Herr Berger zuletzt da?", role: StaffRole.Backoffice);
        var therapist = await _kit.AnswerAsync(spa, "Wann war Herr Berger zuletzt da?", role: StaffRole.Therapist, employeeId: spa.Anna.Id);
        var otherTherapist = await _kit.AnswerAsync(spa, "Wann war Herr Berger zuletzt da?", role: StaffRole.Therapist, employeeId: spa.Ben.Id);
        var elsewhere = await AnswerKit.BlockAsync(grazOnly, "Wann war Herr Berger zuletzt da?", other.Id);

        Assert.Equal(AssistantBlockKind.Denied, backoffice.Kind);
        Assert.Equal("Hans Berger war zuletzt am Montag, 01.03.2027, 10:00 Uhr da (Massage).", therapist.Text);
        // Therapeut:innen sehen nur eigene Termine (Ä317): der Besuch bei Anna steht für Ben nicht im Kalender.
        Assert.Equal("Für Hans Berger ist kein abgeschlossener Besuch im Kalender eingetragen.", otherTherapist.Text);
        // Der Zugang für Graz kennt den Gast, sieht aber keine Termine in Wien.
        Assert.Equal("Für Hans Berger ist kein abgeschlossener Besuch im Kalender eingetragen.", elsewhere.Text);
    }

    // ---------- Gutscheine ----------

    [Fact]
    public async Task Gutschein_Restwert_und_Gueltigkeit_gibt_die_Rezeption_aus_der_Kassa()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var register = await spa.PostAsync<CashRegisterDto>("/api/pos/registers", new CashRegisterInput(spa.Location.Id, $"T-{Guid.NewGuid():N}"[..14], FiscalCountry.AT,
            "Test-Spa GmbH", "Test-Spa GmbH", "AT61 1904 3002 3457 3201", null), Ct);
        var bill = await spa.PostAsync<BillDto>("/api/pos/bills", new BillInput(register.Id, null, []), Ct);
        bill = await spa.PostAsync<BillDto>($"/api/pos/bills/{bill.Id}/lines",
            new BillLineInput(BillLineKind.Voucher, null, null, 1, 100m, 0, null, null, null, VoucherKind.MultiPurpose, null), Ct);
        var receipt = await spa.PostAsync<ReceiptDto>($"/api/pos/bills/{bill.Id}/checkout", new CheckoutRequest(null, [new PaymentInput(PaymentMethod.Cash, 100m, null, null)]), Ct);
        var voucher = Assert.Single(receipt.IssuedVouchers);

        var exact = await _kit.AnswerAsync(spa, $"Wie viel ist auf Gutschein {voucher.Code} noch drauf?", role: StaffRole.Reception);
        // Das Ende des Codes ohne Bindestriche, mindestens vier Zeichen und mit einer Ziffer (sonst wäre es kein Code im Sinn der Absichtserkennung).
        // Die Kassa erzeugt Codes immer mit mindestens einer Ziffer; ohne diese Zusage lief die Schleife hier über das Ende hinaus (CI, 23.10.2026).
        var plain = voucher.Code.Replace("-", "", StringComparison.Ordinal);
        Assert.True(plain.Any(char.IsDigit), $"Gutscheincode ohne Ziffer: {voucher.Code}");
        var length = 4;
        while (!plain[^length..].Any(char.IsDigit))
        {
            length++;
        }

        var tail = await _kit.AnswerAsync(spa, $"Wie viel ist auf Gutschein {plain[^length..]} noch drauf?", role: StaffRole.Reception);
        var unknown = await _kit.AnswerAsync(spa, "Wie viel ist auf Gutschein 0000-9999 noch drauf?", role: StaffRole.Reception);
        var none = await _kit.AnswerAsync(spa, "Wie viel ist auf dem Gutschein noch drauf?", role: StaffRole.Reception);
        var therapist = await _kit.AnswerAsync(spa, $"Wie viel ist auf Gutschein {voucher.Code} noch drauf?", role: StaffRole.Therapist);

        Assert.StartsWith($"Auf dem Gutschein {voucher.Code} sind noch 100,00 EUR (ursprünglich 100,00 EUR), ", exact.Text, StringComparison.Ordinal);
        Assert.Equal("Gutscheine der Kassa", exact.Source);
        Assert.Equal(["Restwert", "100,00 EUR"], exact.Table!.Rows[0]);
        Assert.Equal(exact.Text, tail.Text);
        Assert.Equal(AssistantBlockKind.NotFound, unknown.Kind);
        Assert.Equal(AssistantBlockKind.NotFound, none.Kind);
        Assert.Equal(AssistantBlockKind.Denied, therapist.Kind);
        Assert.Equal("Dafür fehlt die Berechtigung. Gutscheine schlägt die Rezeption an der Kassa nach.", therapist.Text);
    }
}
