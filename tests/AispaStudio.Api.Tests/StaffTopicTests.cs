using AispaStudio.Contracts;

namespace AispaStudio.Api.Tests;

/// <summary>
/// Slice 136 (Ä318): Pause und Dienstende einer Person, Ersthelfer und Zuständigkeiten im Assistenten. „Jetzt“ ist Montag, der 1. März 2027, 11:15 Uhr in Wien; Anna, Ben und
/// jede weitere Person arbeiten montags von 9 bis 17 Uhr. Erwartete Zeiten und Namen stehen von Hand in den Tests.
/// </summary>
public sealed class StaffTopicTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly DateTimeOffset MondayNow = new(2027, 3, 1, 10, 15, 0, TimeSpan.Zero);

    private static readonly DateOnly Jan1 = new(2027, 1, 1);

    private static readonly DateOnly NewYearsEve = new(2027, 12, 31);

    private readonly AnswerKit _kit = new(fixture);

    private static Task<RosterDayResult> PlanAsync(TestSpa spa, Guid employee, params RosterEntry[] entries) =>
        spa.PutAsync<RosterDayResult>($"/api/locations/{spa.Location.Id}/roster/{employee}/{TestSpa.Monday:yyyy-MM-dd}", new RosterDayInput(entries, null, RulesConfirmed: true), Ct);

    private static RosterEntry Work(TestSpa spa, int from, int fromMinute, int until, int untilMinute = 0) =>
        new(RosterKind.Work, spa.Location.Id, new TimeOnly(from, fromMinute), new TimeOnly(until, untilMinute));

    private static Task<BlockDto> PauseAsync(TestSpa spa, Guid employee, int hour, int minute, int untilHour, int untilMinute) =>
        spa.PostAsync<BlockDto>("/api/blocks", new BlockInput(ResourceKind.Employee, employee, TestSpa.At(hour, minute), TestSpa.At(untilHour, untilMinute), "Pause"), Ct);

    private static async Task<Guid> TypeIdAsync(TestSpa spa, DutyTypeKey key) =>
        (await spa.GetAsync<List<DutyTypeDto>>("/api/staff-duties/types", Ct)).Single(t => t.Key == key).Id;

    private static async Task AddDutyAsync(TestSpa spa, Guid employeeId, DutyTypeKey key, Guid? deputyId = null) =>
        await spa.PostAsync<StaffDutyDto>($"/api/employees/{employeeId}/duties", new StaffDutyInput(await TypeIdAsync(spa, key), Jan1, null, deputyId), Ct);

    private static Task<StaffCertificateDto> AddCourseAsync(TestSpa spa, Guid employeeId) =>
        spa.PostAsync<StaffCertificateDto>($"/api/employees/{employeeId}/certificates",
            new StaffCertificateInput("Erste-Hilfe-Kurs", NewYearsEve.AddYears(-2), NewYearsEve, "Rotes Kreuz"), Ct);

    // ---------- Pause und Dienstende ----------

    [Fact]
    public async Task Pause_ohne_Eintrag_nennt_die_verlangte_Dauer()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);

        var block = await _kit.AnswerAsync(spa, "Wann hat Anna heute Pause?", MondayNow);

        Assert.Equal(AssistantBlockKind.Answer, block.Kind);
        Assert.Contains("Anna ist am Montag, 01.03.2027 im Dienst: 09:00 bis 17:00. Dienstende 17:00 Uhr.", block.Text, StringComparison.Ordinal);
        Assert.Contains("Pause: keine eingetragen; verlangt sind nach dem Arbeitszeitrecht 30 Min.", block.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Sophie_hat_Dienst_bis_18_Uhr_und_Pause_13_bis_13_30()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var sophie = await spa.CreateEmployeeAsync("Sophie", [spa.Massage.Id], spa.Location.Id, Ct);
        await PlanAsync(spa, sophie.Id, Work(spa, 9, 0, 18));
        await PauseAsync(spa, sophie.Id, 13, 0, 13, 30);

        var block = await _kit.AnswerAsync(spa, "Wann hat Sophie heute Pause?", MondayNow);
        var assistant = await _kit.StaffAsync(spa, new AssistantQuestion("wann hat sophie pause", spa.Location.Id), now: MondayNow);

        Assert.Equal("Sophie ist am Montag, 01.03.2027 im Dienst: 09:00 bis 18:00. Dienstende 18:00 Uhr. Pause: 13:00–13:30 Uhr.", block.Text);
        Assert.Equal("Dienstplan und Pausen des Standorts Wien", block.Source);
        Assert.Equal(block.Text, assistant.Block!.Text);
        Assert.Empty(assistant.Help);
    }

    [Fact]
    public async Task Pause_nennt_die_Luecke_zwischen_zwei_Diensten()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await PlanAsync(spa, spa.Anna.Id, Work(spa, 9, 0, 12), Work(spa, 13, 0, 17));

        var block = await _kit.AnswerAsync(spa, "Wann hat Anna heute Pause?", MondayNow);

        Assert.Equal("Anna ist am Montag, 01.03.2027 im Dienst: 09:00 bis 12:00, 13:00 bis 17:00. Dienstende 17:00 Uhr. Pause: 12:00–13:00 Uhr (Lücke zwischen den Diensten).", block.Text);
    }

    [Fact]
    public async Task Person_im_Urlaub_hat_keine_Pause_und_keinen_Dienst()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await PlanAsync(spa, spa.Ben.Id, new RosterEntry(RosterKind.Vacation, null, null, null));

        var block = await _kit.AnswerAsync(spa, "Wann hat Ben heute Pause?", MondayNow);

        Assert.Equal("Ben ist am Montag, 01.03.2027 nicht im Dienst im Standort Wien: Urlaub.", block.Text);
    }

    // ---------- Ersthelfer und Zuständigkeiten ----------

    [Fact]
    public async Task Lena_ist_Ersthelferin_mit_gueltigem_Kurs_und_im_Dienst()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var lena = await spa.CreateEmployeeAsync("Lena", [spa.Massage.Id], spa.Location.Id, Ct);
        await AddDutyAsync(spa, lena.Id, DutyTypeKey.FirstAid);
        await AddCourseAsync(spa, lena.Id);

        var block = await _kit.AnswerAsync(spa, "Wer ist heute Ersthelfer?", MondayNow);
        var assistant = await _kit.StaffAsync(spa, new AssistantQuestion("wer ist heute ersthelfer", spa.Location.Id), now: MondayNow);

        Assert.Equal("Ersthelfer im Dienst am Montag, 01.03.2027: Lena (bis 17:00 Uhr).", block.Text);
        Assert.Equal(block.Text, assistant.Block!.Text);
        Assert.Empty(assistant.Help);
    }

    [Fact]
    public async Task Ersthelferin_ohne_gueltiges_Zertifikat_zaehlt_nicht_und_steht_mit_Grund_in_der_Tabelle()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var lena = await spa.CreateEmployeeAsync("Lena", [spa.Massage.Id], spa.Location.Id, Ct);
        await AddDutyAsync(spa, lena.Id, DutyTypeKey.FirstAid);

        var block = await _kit.AnswerAsync(spa, "Wer ist heute Ersthelfer?", MondayNow);

        Assert.Equal("Am Montag, 01.03.2027 ist niemand im Dienst, der als Ersthelfer gilt.", block.Text);
        Assert.Equal([["Lena", "Zertifikat fehlt oder ist abgelaufen"]], block.Table!.Rows);
    }

    [Fact]
    public async Task Ersthelferin_nicht_im_Dienst_steht_mit_Vertretung_da()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var lena = await spa.CreateEmployeeAsync("Lena", [spa.Massage.Id], spa.Location.Id, Ct);
        await AddDutyAsync(spa, lena.Id, DutyTypeKey.FirstAid, spa.Ben.Id);
        await AddCourseAsync(spa, lena.Id);
        await PlanAsync(spa, lena.Id, new RosterEntry(RosterKind.Vacation, null, null, null));

        var block = await _kit.AnswerAsync(spa, "Wer ist heute Ersthelfer?", MondayNow);

        Assert.Equal("Am Montag, 01.03.2027 ist niemand im Dienst, der als Ersthelfer gilt.", block.Text);
        Assert.Equal([["Lena", "nicht im Dienst, Vertretung: Ben"]], block.Table!.Rows);
    }

    [Fact]
    public async Task Ohne_eingetragene_Zustaendigkeit_sagt_die_Antwort_wo_man_sie_eintraegt()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);

        var block = await _kit.AnswerAsync(spa, "Wer ist heute Ersthelfer?", MondayNow);

        Assert.Equal(AssistantBlockKind.NotStored, block.Kind);
        Assert.Contains("Ersthelfer", block.Text, StringComparison.Ordinal);
        Assert.Contains("Bereich „Team“", block.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Zustaendigkeit_Kassa_nennt_Person_und_Vertretung()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await AddDutyAsync(spa, spa.Anna.Id, DutyTypeKey.CashResponsibility, spa.Ben.Id);

        var block = await _kit.AnswerAsync(spa, "Wer ist für die Kassa zuständig?", MondayNow);

        Assert.Equal("Kassa-Verantwortung: Anna (Vertretung: Ben).", block.Text);
    }
}
