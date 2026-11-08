using AispaStudio.Api.Scheduling;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Tests;

/// <summary>
/// Geburtstage der Gäste (Runde 5b, Ä45): der Server rechnet sie (Kalender: am Tag und drei Tage davor und danach, Kundenkarte: der nächste), der Assistent nennt nur
/// Gäste mit Termin im Zeitraum. Der 29. Februar gilt in Jahren ohne Schalttag am 28. Februar. „Heute“ ist im Assistenten Montag, der 1. März 2027, 11:00 Uhr in Wien.
/// </summary>
public sealed class BirthdayTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly DateTimeOffset MondayNoon = new(2027, 3, 1, 10, 0, 0, TimeSpan.Zero);

    private readonly AnswerKit _kit = new(fixture);

    private static Task<CustomerDto> GuestAsync(TestSpa spa, string first, string last, DateOnly? birth) =>
        spa.PostAsync<CustomerDto>("/api/customers", new CustomerInput(first, last, null, null, birth, null), Ct);

    // ---------- Rechnung ----------

    [Fact]
    public void Der_Geburtstag_am_Tag_ist_heute_mit_Alter_und_ohne_Geburtsjahr_im_Fenster_von_drei_Tagen()
    {
        var birth = new DateOnly(1985, 3, 1);

        Assert.Equal(new GuestBirthday(new(2027, 3, 1), 0, 42, 42), Birthdays.Near(birth, new(2027, 3, 1)));
        Assert.Equal(new GuestBirthday(new(2027, 3, 1), 3, 42, 41), Birthdays.Near(birth, new(2027, 2, 26)));
        Assert.Equal(new GuestBirthday(new(2027, 3, 1), -3, 42, 42), Birthdays.Near(birth, new(2027, 3, 4)));
        Assert.Null(Birthdays.Near(birth, new(2027, 3, 5)));
        Assert.Null(Birthdays.Near(birth, new(2027, 2, 25)));
    }

    [Fact]
    public void Das_Fenster_geht_ueber_den_Jahreswechsel()
    {
        Assert.Equal(new GuestBirthday(new(2027, 1, 1), 2, 37, 36), Birthdays.Near(new(1990, 1, 1), new(2026, 12, 30)));
        Assert.Equal(new GuestBirthday(new(2026, 12, 31), -2, 36, 36), Birthdays.Near(new(1990, 12, 31), new(2027, 1, 2)));
    }

    [Fact]
    public void Der_29_Februar_gilt_in_Jahren_ohne_Schalttag_am_28_Februar()
    {
        var birth = new DateOnly(2000, 2, 29);

        Assert.Equal(new DateOnly(2027, 2, 28), Birthdays.InYear(birth, 2027));
        Assert.Equal(new DateOnly(2028, 2, 29), Birthdays.InYear(birth, 2028));
        Assert.Equal(new GuestBirthday(new(2027, 2, 28), 0, 27, 27), Birthdays.Near(birth, new(2027, 2, 28)));
        Assert.Equal(new GuestBirthday(new(2028, 2, 29), 1, 28, 27), Birthdays.Near(birth, new(2028, 2, 28)));
        Assert.Equal(new GuestBirthday(new(2028, 2, 29), 0, 28, 28), Birthdays.Near(birth, new(2028, 2, 29)));
        Assert.Equal(new DateOnly(2028, 2, 29), Birthdays.Next(birth, new(2027, 3, 1)).Date);
        Assert.Equal(new DateOnly(2027, 2, 28), Birthdays.Next(birth, new(2026, 12, 1)).Date);
    }

    [Fact]
    public void Der_naechste_Geburtstag_zaehlt_heute_mit_und_rechnet_das_Alter_heute_und_dann()
    {
        var birth = new DateOnly(1990, 3, 12);

        Assert.Equal(new GuestBirthday(new(2027, 3, 12), 11, 37, 36), Birthdays.Next(birth, new(2027, 3, 1)));
        Assert.Equal(new GuestBirthday(new(2027, 3, 12), 0, 37, 37), Birthdays.Next(birth, new(2027, 3, 12)));
        Assert.Equal(new GuestBirthday(new(2028, 3, 12), 365, 38, 37), Birthdays.Next(birth, new(2027, 3, 13)));
    }

    [Fact]
    public void Ohne_bekanntes_Geburtsjahr_gibt_es_kein_Alter_und_ein_Datum_in_der_Zukunft_auch_nicht()
    {
        Assert.Equal(new GuestBirthday(new(2027, 3, 1), 0, null, null), Birthdays.Near(new DateOnly(1, 3, 1), new(2027, 3, 1)));
        Assert.Equal(new GuestBirthday(new(2027, 3, 1), 0, null, null), Birthdays.Near(new DateOnly(2030, 3, 1), new(2027, 3, 1)));
    }

    // ---------- Kalender ----------

    [Fact]
    public async Task Der_Kalender_nennt_den_Geburtstag_am_Tag_und_drei_Tage_davor_und_danach()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var today = await GuestAsync(spa, "Brigitte", "Müller", new(1985, 3, 1));
        var before = await GuestAsync(spa, "Emil", "Vorher", new(1990, 3, 4));
        var after = await GuestAsync(spa, "Dora", "Danach", new(1990, 2, 26));
        var far = await GuestAsync(spa, "Fritz", "Fern", new(1990, 3, 5));
        var without = await GuestAsync(spa, "Gerda", "Ohne", null);
        var guests = new[] { today, before, after, far, without };
        foreach (var (guest, index) in guests.Select((g, i) => (g, i)))
        {
            var second = index % 2 == 1;
            await spa.BookOkAsync(TestSpa.At(10 + (index / 2 * 2)), Ct, second ? spa.Ben.Id : spa.Anna.Id, second ? spa.RoomB.Id : spa.RoomA.Id, guest.Id);
        }

        var day = await spa.GetAsync<CalendarDay>($"/api/locations/{spa.Location.Id}/calendar?date=2027-03-01", Ct);

        GuestBirthday? BirthdayOf(CustomerDto guest) => day.Guests!.Single(g => g.CustomerId == guest.Id).Birthday;
        Assert.Equal(new GuestBirthday(new(2027, 3, 1), 0, 42, 42), BirthdayOf(today));
        Assert.Equal(new GuestBirthday(new(2027, 3, 4), 3, 37, 36), BirthdayOf(before));
        Assert.Equal(new GuestBirthday(new(2027, 2, 26), -3, 37, 37), BirthdayOf(after));
        Assert.Null(BirthdayOf(far));
        Assert.Null(BirthdayOf(without));
    }

    [Fact]
    public async Task Der_Tag_des_Kalenders_bestimmt_den_Geburtstag_nicht_der_Server()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var guest = await GuestAsync(spa, "Brigitte", "Müller", new(1985, 3, 1));
        await spa.BookOkAsync(TestSpa.At(10, date: new(2027, 3, 8)), Ct, spa.Anna.Id, spa.RoomA.Id, guest.Id);

        var later = await spa.GetAsync<CalendarDay>($"/api/locations/{spa.Location.Id}/calendar?date=2027-03-08", Ct);

        Assert.Null(later.Guests!.Single().Birthday);
    }

    // ---------- Kundenkarte ----------

    [Fact]
    public async Task Die_Kundenkarte_nennt_den_naechsten_Geburtstag_mit_Alter_heute_und_dann()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var zone = TimeZoneInfo.FindSystemTimeZoneById(spa.Location.TimeZone);
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, zone).DateTime);
        var guest = await GuestAsync(spa, "Brigitte", "Müller", today.AddDays(5).AddYears(-30));
        var without = await GuestAsync(spa, "Gerda", "Ohne", null);

        var insights = await spa.GetAsync<CustomerInsights>($"/api/customers/{guest.Id}/insights", Ct);
        var none = await spa.GetAsync<CustomerInsights>($"/api/customers/{without.Id}/insights", Ct);

        Assert.Equal(new GuestBirthday(today.AddDays(5), 5, 30, 29), insights.Birthday);
        Assert.Null(none.Birthday);
    }

    // ---------- Assistent ----------

    private async Task<(TestSpa Spa, CustomerDto Brigitte)> AssistantDataAsync()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var brigitte = await GuestAsync(spa, "Brigitte", "Müller", new(1980, 3, 1));
        var pia = await GuestAsync(spa, "Pia", "Später", new(1992, 3, 3));
        var cancelled = await GuestAsync(spa, "Hans", "Abgesagt", new(1990, 3, 1));
        await GuestAsync(spa, "Otto", "Ohnetermin", new(1975, 3, 1));
        var noBirthday = await GuestAsync(spa, "Erika", "Ohnedatum", null);
        await spa.BookOkAsync(TestSpa.At(10, 30), Ct, spa.Anna.Id, spa.RoomA.Id, noBirthday.Id);
        await spa.BookOkAsync(TestSpa.At(10, 30), Ct, spa.Ben.Id, spa.RoomB.Id, brigitte.Id);
        await spa.BookOkAsync(TestSpa.At(12), Ct, spa.Anna.Id, spa.RoomA.Id, pia.Id);
        var away = await spa.BookOkAsync(TestSpa.At(14), Ct, spa.Anna.Id, spa.RoomA.Id, cancelled.Id);
        await spa.PostAsync<AppointmentDto>($"/api/appointments/{away.Id}/cancel", new CancelAppointmentRequest(null), Ct);
        return (spa, brigitte);
    }

    [Fact]
    public async Task Wer_heute_Geburtstag_hat_nennt_nur_Gaeste_mit_Termin_heute()
    {
        var (spa, _) = await AssistantDataAsync();

        var block = await _kit.AnswerAsync(spa, "Wer hat heute Geburtstag?", MondayNoon);

        Assert.Equal(("Birthdays", AssistantBlockKind.Answer), (block.Topic, block.Kind));
        Assert.Equal("Heute hat Brigitte Müller Geburtstag (Gäste mit Termin im Standort Wien).", block.Text);
        Assert.Equal("Termine und Kundendaten", block.Source);
        Assert.Equal(["Gast", "Datum", "Uhrzeit"], block.Table!.Columns);
        Assert.Equal([["Brigitte Müller", "Montag, 01.03.2027", "10:30 Uhr"]], block.Table.Rows);
    }

    [Fact]
    public async Task Wer_diese_Woche_Geburtstag_hat_nennt_Datum_und_Termin_auch_an_einem_anderen_Tag()
    {
        var (spa, _) = await AssistantDataAsync();

        var block = await _kit.AnswerAsync(spa, "Wer hat diese Woche Geburtstag?", MondayNoon);

        Assert.Equal("Im Zeitraum 01.03.2027 bis 07.03.2027 haben Brigitte Müller und Pia Später Geburtstag (Gäste mit Termin im Standort Wien).", block.Text);
        Assert.Equal(
            [["Brigitte Müller", "Montag, 01.03.2027", "10:30 Uhr"], ["Pia Später", "Mittwoch, 03.03.2027", "Montag, 01.03.2027, 12:00 Uhr"]],
            block.Table!.Rows);
        Assert.Equal("01.03.2027 bis 07.03.2027", block.Period);
    }

    [Fact]
    public async Task Ohne_Treffer_sagt_die_Antwort_es_und_nennt_keinen_Gast_ohne_Termin()
    {
        var (spa, _) = await AssistantDataAsync();

        var tomorrow = await _kit.AnswerAsync(spa, "Wer hat morgen Geburtstag?", MondayNoon);

        Assert.Equal(AssistantBlockKind.Answer, tomorrow.Kind);
        Assert.Equal("Am Dienstag, 02.03.2027 hat kein Gast mit Termin im Standort Wien Geburtstag.", tomorrow.Text);
        Assert.Null(tomorrow.Table);
    }

    [Fact]
    public async Task Rechte_Rezeption_und_Leitung_duerfen_das_Backoffice_nicht_Therapie_nur_aus_den_eigenen_Terminen()
    {
        var (spa, _) = await AssistantDataAsync();
        const string Question = "Wer hat heute Geburtstag?";

        var reception = await _kit.AnswerAsync(spa, Question, MondayNoon, StaffRole.Reception);
        var manager = await _kit.AnswerAsync(spa, Question, MondayNoon, StaffRole.Manager);
        var backoffice = await _kit.AnswerAsync(spa, Question, MondayNoon, StaffRole.Backoffice);
        var ben = await _kit.AnswerAsync(spa, Question, MondayNoon, StaffRole.Therapist, employeeId: spa.Ben.Id);
        var anna = await _kit.AnswerAsync(spa, Question, MondayNoon, StaffRole.Therapist, employeeId: spa.Anna.Id);

        Assert.Equal("Brigitte Müller", Assert.Single(reception.Table!.Rows)[0]);
        Assert.Equal("Brigitte Müller", Assert.Single(manager.Table!.Rows)[0]);
        Assert.Equal(AssistantBlockKind.Denied, backoffice.Kind);
        Assert.StartsWith("Dafür fehlt die Berechtigung.", backoffice.Text, StringComparison.Ordinal);
        Assert.Equal("Termine und Kundendaten", reception.Source);
        // Brigitte ist bei Ben; wer nur die eigenen Termine sieht, sieht bei Anna keinen Geburtstag.
        Assert.Equal("Ihre Termine und Kundendaten", ben.Source);
        Assert.Equal("Brigitte Müller", Assert.Single(ben.Table!.Rows)[0]);
        Assert.Null(anna.Table);
        Assert.Contains("hat kein Gast mit Termin", anna.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Der_29_Februar_zaehlt_in_diesem_Jahr_am_28_Februar()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var leapling = await GuestAsync(spa, "Lea", "Schalt", new(2000, 2, 29));
        await spa.BookOkAsync(TestSpa.At(10, date: new(2027, 2, 22)), Ct, spa.Anna.Id, spa.RoomA.Id, leapling.Id);

        // Mittwoch, 24. Februar 2027: diese Woche endet am Sonntag, 28. Februar.
        var block = await _kit.AnswerAsync(spa, "Wer hat diese Woche Geburtstag?", new DateTimeOffset(2027, 2, 24, 10, 0, 0, TimeSpan.Zero));

        Assert.Equal([["Lea Schalt", "Sonntag, 28.02.2027", "Montag, 22.02.2027, 10:00 Uhr"]], block.Table!.Rows);
    }

    [Theory]
    [InlineData("Wer hat heute Geburtstag?")]
    [InlineData("Hat heute jemand Geburtstag?")]
    [InlineData("Welche Gäste haben diese Woche Geburtstag?")]
    [InlineData("Geburtstage heute")]
    public async Task Die_Regeln_erkennen_Fragen_nach_Geburtstagen(string question)
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);

        var block = await _kit.AnswerAsync(spa, question, MondayNoon);

        Assert.Equal("Birthdays", block.Topic);
    }
}
