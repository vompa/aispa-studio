using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.RegularExpressions;
using AispaStudio.Api.HrFile;
using AispaStudio.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace AispaStudio.Api.Tests;

/// <summary>
/// R05 Slice 110 (Ä252, E8, E9): Personalakte mit eigenem Recht, verschlüsselt im Schema <c>hr</c>; Erinnerung an den Folgetermin, Löschen abgelaufener Verwarnungen,
/// Verlauf ohne Inhalt und Export als PDF. Erwartete Daten und Bytes stehen von Hand in den Tests.
/// </summary>
public sealed class HrFileTests(ApiFixture fixture)
{
    private static readonly TimeZoneInfo Vienna = TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna");

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, Vienna).DateTime);

    private static string FilePath(Guid employeeId) => $"/api/employees/{employeeId}/hr-file";

    private static HrEntryInput Conversation(DateOnly? followUp = null) => new(
        HrEntryKind.Conversation, new DateOnly(2026, 3, 15), "Ziele für das Jahr besprochen", HrConversationKind.Annual, "Lena, Inhaberin",
        [new HrAgreement("Kurs Hot Stone buchen", false), new HrAgreement("Mehr Samstage", true)], followUp);

    [Theory]
    [InlineData(StaffRole.Reception)]
    [InlineData(StaffRole.Therapist)]
    [InlineData(StaffRole.Manager)]
    [InlineData(StaffRole.Backoffice)]
    public async Task Given_eine_Rezeptionistin_When_sie_die_Personalakte_einer_Kollegin_abruft_Then_antwortet_die_API_403(StaffRole role)
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var entry = await spa.PostAsync<HrEntryDto>(FilePath(spa.Ben.Id), new HrEntryInput(HrEntryKind.Note, Today, "Vertraulich"), Ct);
        using var client = await StaffAsync(spa, role);

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(FilePath(spa.Ben.Id), Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"{FilePath(spa.Ben.Id)}/export", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"{FilePath(spa.Ben.Id)}/history", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/hr-file/follow-ups", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync(FilePath(spa.Ben.Id), new HrEntryInput(HrEntryKind.Note, Today, "x"), TestSpa.Json, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.DeleteAsync($"{FilePath(spa.Ben.Id)}/{entry.Id}", Ct)).StatusCode);
    }

    [Fact]
    public async Task Given_die_Managerin_When_sie_die_Personalakte_abruft_Then_sieht_sie_die_Eintraege()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await spa.PostAsync<HrEntryDto>(FilePath(spa.Ben.Id), new HrEntryInput(HrEntryKind.Note, Today, "Sieht die Managerin"), Ct);
        using var client = await StaffAsync(spa, StaffRole.GeneralManager);

        var entries = await client.GetFromJsonAsync<List<HrEntryDto>>(FilePath(spa.Ben.Id), TestSpa.Json, Ct);

        Assert.Equal("Sieht die Managerin", Assert.Single(entries!).Text);
    }

    [Fact]
    public async Task Given_ein_fremdes_Spa_When_es_die_Personalakte_abruft_Then_ist_sie_nicht_gefunden()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var other = await TestSpa.CreateAsync(fixture, Ct);

        Assert.Equal(HttpStatusCode.NotFound, (await other.Client.GetAsync(FilePath(spa.Ben.Id), Ct)).StatusCode);
    }

    [Fact]
    public async Task Given_ein_Gespraech_mit_zwei_Vereinbarungen_When_eine_abgehakt_wird_Then_bleibt_das_beim_Lesen_erhalten()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var created = await spa.PostAsync<HrEntryDto>(FilePath(spa.Ben.Id), Conversation(), Ct);

        var changed = Conversation() with { Agreements = [new HrAgreement("Kurs Hot Stone buchen", true), new HrAgreement("Mehr Samstage", true)] };
        await spa.PutAsync<HrEntryDto>($"{FilePath(spa.Ben.Id)}/{created.Id}", changed, Ct);

        var read = Assert.Single(await spa.GetAsync<List<HrEntryDto>>(FilePath(spa.Ben.Id), Ct));
        Assert.Equal([new HrAgreement("Kurs Hot Stone buchen", true), new HrAgreement("Mehr Samstage", true)], read.Agreements);
        Assert.Equal(HrConversationKind.Annual, read.ConversationKind);
        Assert.Equal("Lena, Inhaberin", read.Participants);
    }

    [Fact]
    public async Task Given_kein_Text_und_Ablauf_vor_dem_Datum_When_gespeichert_Then_nennt_die_API_beide_Felder()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);

        var response = await spa.Client.PostAsJsonAsync(FilePath(spa.Ben.Id),
            new HrEntryInput(HrEntryKind.Warning, new DateOnly(2026, 5, 1), " ", IssuedBy: "Inhaberin", ExpiresOn: new DateOnly(2026, 4, 30)), TestSpa.Json, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(Ct);
        Assert.Contains(nameof(HrEntryInput.Text), body, StringComparison.Ordinal);
        Assert.Contains(nameof(HrEntryInput.ExpiresOn), body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Given_eine_ausgeschiedene_Person_When_ein_Eintrag_angelegt_wird_Then_lehnt_die_API_ab_und_der_Export_bleibt_moeglich()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await spa.PostAsync<HrEntryDto>(FilePath(spa.Ben.Id), Conversation(Today), Ct);
        (await spa.Client.DeleteAsync($"/api/employees/{spa.Ben.Id}", Ct)).EnsureSuccessStatusCode();

        var response = await spa.Client.PostAsJsonAsync(FilePath(spa.Ben.Id), new HrEntryInput(HrEntryKind.Note, Today, "Später"), TestSpa.Json, Ct);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await spa.Client.GetAsync($"{FilePath(spa.Ben.Id)}/export", Ct)).StatusCode);
        Assert.Empty(await spa.GetAsync<List<HrFollowUpDto>>("/api/hr-file/follow-ups", Ct));
    }

    [Fact]
    public async Task Given_eine_ausgeschiedene_Person_When_ein_Eintrag_geloescht_wird_Then_lehnt_die_API_ab_und_er_bleibt()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var entry = await spa.PostAsync<HrEntryDto>(FilePath(spa.Ben.Id), Conversation(Today), Ct);
        (await spa.Client.DeleteAsync($"/api/employees/{spa.Ben.Id}", Ct)).EnsureSuccessStatusCode();

        var response = await spa.Client.DeleteAsync($"{FilePath(spa.Ben.Id)}/{entry.Id}", Ct);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Single(await spa.GetAsync<List<HrEntryDto>>(FilePath(spa.Ben.Id), Ct));
    }

    [Fact]
    public async Task Given_ein_Jahresgespraech_mit_Folgetermin_15_03_When_der_15_03_kommt_Then_hat_die_Inhaberin_eine_Erinnerung()
    {
        var day = new DateOnly(2027, 3, 15);

        // 22:59 UTC am 14.3. ist in Wien (MEZ) 23:59 am 14.3., eine Minute später beginnt der 15.3.
        Assert.False(HrFileRules.IsFollowUpDue(day, new DateTimeOffset(2027, 3, 14, 22, 59, 0, TimeSpan.Zero), Vienna));
        Assert.True(HrFileRules.IsFollowUpDue(day, new DateTimeOffset(2027, 3, 14, 23, 0, 0, TimeSpan.Zero), Vienna));

        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var lena = await spa.CreateEmployeeAsync("Lena", [spa.Massage.Id], spa.Location.Id, Ct);
        var due = await spa.PostAsync<HrEntryDto>(FilePath(lena.Id), Conversation(Today), Ct);
        await spa.PostAsync<HrEntryDto>(FilePath(spa.Ben.Id), Conversation(Today.AddDays(1)), Ct);

        var reminder = Assert.Single(await spa.GetAsync<List<HrFollowUpDto>>("/api/hr-file/follow-ups", Ct));
        Assert.Equal(new HrFollowUpDto(due.Id, lena.Id, "Folgegespräch mit Lena", Today), reminder);
    }

    [Fact]
    public async Task Given_ein_gefuehrtes_Folgegespraech_When_die_Erinnerungen_gelesen_werden_Then_ist_es_nicht_mehr_dabei()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var entry = await spa.PostAsync<HrEntryDto>(FilePath(spa.Ben.Id), Conversation(Today), Ct);

        await spa.PutAsync<HrEntryDto>($"{FilePath(spa.Ben.Id)}/{entry.Id}", Conversation(Today) with { FollowUpDone = true }, Ct);

        Assert.Empty(await spa.GetAsync<List<HrFollowUpDto>>("/api/hr-file/follow-ups", Ct));
    }

    [Fact]
    public async Task Given_eine_Verwarnung_mit_Ablaufdatum_gestern_When_der_Hintergrundjob_laeuft_Then_ist_sie_geloescht_und_der_Verlauf_nennt_nur_die_Loeschung()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        const string Reason = "Wiederholt zu spät gekommen";
        var issued = Today.AddDays(-30);
        await spa.PostAsync<HrEntryDto>(FilePath(spa.Ben.Id), new HrEntryInput(HrEntryKind.Warning, issued, Reason, IssuedBy: "Inhaberin", ExpiresOn: Today.AddDays(-1)), Ct);
        var stays = await spa.PostAsync<HrEntryDto>(FilePath(spa.Ben.Id), new HrEntryInput(HrEntryKind.Warning, issued, "Bleibt", IssuedBy: "Inhaberin", ExpiresOn: Today), Ct);

        await fixture.Factory.Services.GetRequiredService<HrFileCleanup>().RunOnceAsync(DateTimeOffset.UtcNow, Ct);

        Assert.Equal(stays.Id, Assert.Single(await spa.GetAsync<List<HrEntryDto>>(FilePath(spa.Ben.Id), Ct)).Id);
        var history = await spa.GetAsync<List<HistoryEntry>>($"{FilePath(spa.Ben.Id)}/history", Ct);
        Assert.Equal("Verwarnung automatisch gelöscht", history[0].Text);
        Assert.DoesNotContain(history, e => e.Text.Contains(Reason, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Given_eine_Anmerkung_When_der_Verlauf_geoeffnet_wird_Then_steht_geaendert_von_ohne_Inhalt()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        const string Secret = "Vertrauliche Anmerkung";
        var before = DateTimeOffset.UtcNow.AddSeconds(-5);
        var note = await spa.PostAsync<HrEntryDto>(FilePath(spa.Ben.Id), new HrEntryInput(HrEntryKind.Note, Today, Secret), Ct);
        await spa.PutAsync<HrEntryDto>($"{FilePath(spa.Ben.Id)}/{note.Id}", new HrEntryInput(HrEntryKind.Note, Today, $"{Secret} ergänzt"), Ct);

        var history = await spa.GetAsync<List<HistoryEntry>>($"{FilePath(spa.Ben.Id)}/history", Ct);

        Assert.Equal(["Anmerkung geändert von Test Person", "Anmerkung angelegt von Test Person"], history.Select(e => e.Text));
        Assert.InRange(history[0].At, before, DateTimeOffset.UtcNow.AddSeconds(5));

        // Auch die Protokolle in der Datenbank tragen nichts vom Inhalt, nicht einmal verschlüsselt.
        await using var db = new NpgsqlConnection(fixture.ConnectionString);
        await db.OpenAsync(Ct);
        await using var command = new NpgsqlCommand(
            $"""SELECT count(*) FROM sync.history WHERE table_schema = 'hr' AND row_key ->> 'Id' = '{note.Id}' AND changes IS NOT NULL""", db);
        Assert.Equal(0L, await command.ExecuteScalarAsync(Ct));
    }

    [Fact]
    public async Task Given_eine_Verwarnung_ohne_Ablaufdatum_When_gespeichert_Then_gilt_die_Vorgabe_zwei_Jahre()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);

        var saved = await spa.PostAsync<HrEntryDto>(FilePath(spa.Ben.Id),
            new HrEntryInput(HrEntryKind.Warning, new DateOnly(2026, 11, 5), "Grund", IssuedBy: "Inhaberin", HandedOverInWriting: true), Ct);

        Assert.Equal(new DateOnly(2028, 11, 5), saved.ExpiresOn);
        Assert.Equal("Inhaberin", saved.IssuedBy);
        Assert.True(saved.HandedOverInWriting);
    }

    [Fact]
    public async Task Given_ein_Eintrag_When_die_Tabelle_gelesen_wird_Then_steht_der_Inhalt_nicht_im_Klartext()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        const string Secret = "Streng vertraulicher Satz";
        var entry = await spa.PostAsync<HrEntryDto>(FilePath(spa.Ben.Id), new HrEntryInput(HrEntryKind.Note, Today, Secret), Ct);

        await using var db = new NpgsqlConnection(fixture.ConnectionString);
        await db.OpenAsync(Ct);
        await using var command = new NpgsqlCommand($"""SELECT to_jsonb(e)::text, "Payload" FROM hr."HrEntries" e WHERE "Id" = '{entry.Id}'""", db);
        await using var reader = await command.ExecuteReaderAsync(Ct);
        Assert.True(await reader.ReadAsync(Ct));
        Assert.DoesNotContain(Secret, reader.GetString(0), StringComparison.Ordinal);
        var payload = (byte[])reader[1];
        Assert.DoesNotContain(Secret, Encoding.UTF8.GetString(payload), StringComparison.Ordinal);
        Assert.Equal(1, payload[0]);
    }

    [Fact]
    public async Task Given_ein_nicht_lesbarer_Eintrag_When_Akte_oder_Export_abgerufen_Then_antwortet_die_API_mit_klarer_Meldung()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var entry = await spa.PostAsync<HrEntryDto>(FilePath(spa.Ben.Id), new HrEntryInput(HrEntryKind.Note, Today, "Inhalt"), Ct);
        await using var db = new NpgsqlConnection(fixture.ConnectionString);
        await db.OpenAsync(Ct);
        await using var command = new NpgsqlCommand($"""UPDATE hr."HrEntries" SET "Payload" = '\x00'::bytea WHERE "Id" = '{entry.Id}'""", db);
        await command.ExecuteNonQueryAsync(Ct);

        var file = await spa.Client.GetAsync(FilePath(spa.Ben.Id), Ct);
        var export = await spa.Client.GetAsync($"{FilePath(spa.Ben.Id)}/export", Ct);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, file.StatusCode);
        Assert.Contains("Schlüssel", await file.Content.ReadAsStringAsync(Ct), StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, export.StatusCode);
    }

    [Fact]
    public async Task Given_Eintraege_einer_Person_When_exportiert_Then_PDF_mit_allen_Eintraegen_nur_dieser_Person()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await spa.PostAsync<HrEntryDto>(FilePath(spa.Ben.Id), Conversation(new DateOnly(2027, 3, 15)), Ct);
        await spa.PostAsync<HrEntryDto>(FilePath(spa.Ben.Id), new HrEntryInput(HrEntryKind.Warning, new DateOnly(2026, 6, 1), "Grund der Verwarnung", IssuedBy: "Inhaberin"), Ct);
        await spa.PostAsync<HrEntryDto>(FilePath(spa.Anna.Id), new HrEntryInput(HrEntryKind.Note, Today, "Gehört zu Anna"), Ct);

        var response = await spa.Client.GetAsync($"{FilePath(spa.Ben.Id)}/export", Ct);

        response.EnsureSuccessStatusCode();
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        var text = SimplePdfReader.Text(await response.Content.ReadAsByteArrayAsync(Ct));
        Assert.Contains("Personalakte: Ben", text, StringComparison.Ordinal);
        Assert.Contains("Art. 15 DSGVO", text, StringComparison.Ordinal);
        Assert.Contains("Ziele für das Jahr besprochen", text, StringComparison.Ordinal);
        Assert.Contains("[ ] Kurs Hot Stone buchen", text, StringComparison.Ordinal);
        Assert.Contains("[x] Mehr Samstage", text, StringComparison.Ordinal);
        Assert.Contains("15.3.2027", text, StringComparison.Ordinal);
        Assert.Contains("Grund der Verwarnung", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Gehört zu Anna", text, StringComparison.Ordinal);
    }

    [Fact]
    public void SimplePdf_schreibt_Umlaute_als_WinAnsi_und_eine_gueltige_Querverweistabelle()
    {
        var pdf = new SimplePdf();
        pdf.Heading("Gespräch");
        pdf.Text("Müller – 5 €");

        var bytes = pdf.ToBytes();
        var raw = Encoding.Latin1.GetString(bytes);

        Assert.StartsWith("%PDF-1.4", raw, StringComparison.Ordinal);
        Assert.EndsWith("%%EOF\n", raw, StringComparison.Ordinal);
        // „ä“ = E4 in Windows-1252, „ü“ = FC, „–“ = 96, „€“ = 80 (Codepage von Hand nachgesehen).
        Assert.Contains("<47657370" + "72E4" + "6368>", raw, StringComparison.Ordinal);
        Assert.Contains("<4DFC6C6C6572" + "20" + "96" + "20" + "35" + "20" + "80>", raw, StringComparison.Ordinal);

        // Jede Zeile der Tabelle zeigt auf den Anfang ihres Objekts.
        var start = int.Parse(raw[(raw.LastIndexOf("startxref\n", StringComparison.Ordinal) + 10)..].Split('\n')[0], CultureInfo.InvariantCulture);
        Assert.StartsWith("xref\n0 ", raw[start..], StringComparison.Ordinal);
        var lines = raw[start..].Split('\n');
        var count = int.Parse(lines[1].Split(' ')[1], CultureInfo.InvariantCulture);
        for (var number = 1; number < count; number++)
        {
            var offset = int.Parse(lines[2 + number][..10], CultureInfo.InvariantCulture);
            Assert.StartsWith($"{number} 0 obj", raw[offset..], StringComparison.Ordinal);
        }
    }

    [Fact]
    public void SimplePdf_bricht_lange_Texte_um_und_beginnt_neue_Seiten()
    {
        var pdf = new SimplePdf();
        var word = string.Concat(Enumerable.Repeat("W", 300));
        pdf.Text(word);
        for (var line = 0; line < 120; line++)
        {
            pdf.Text($"Zeile {line}");
        }

        var raw = Encoding.Latin1.GetString(pdf.ToBytes());

        // 120 Zeilen zu 14 Punkt passen bei 842 Punkt Seitenhöhe nicht auf eine Seite; das lange Wort bleibt innerhalb der Seite.
        Assert.True(raw.Split("/Type /Page ").Length - 1 >= 3);
        Assert.Contains(word, SimplePdfReader.Text(pdf.ToBytes()).Replace("\n", "", StringComparison.Ordinal), StringComparison.Ordinal);
        Assert.DoesNotContain(string.Concat(Enumerable.Repeat("57", 300)), raw, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(2026, 11, 5, 2028, 11, 5)]
    [InlineData(2028, 2, 29, 2030, 2, 28)]
    public void Die_Vorgabe_fuer_das_Ablaufdatum_ist_zwei_Jahre(int year, int month, int day, int expectedYear, int expectedMonth, int expectedDay) =>
        Assert.Equal(new DateOnly(expectedYear, expectedMonth, expectedDay), HrFileRules.DefaultExpiry(new DateOnly(year, month, day)));

    [Fact]
    public void Abgelaufen_ist_erst_der_Tag_nach_dem_Ablaufdatum()
    {
        var day = new DateOnly(2027, 3, 15);

        Assert.False(HrFileRules.IsExpired(day, new DateTimeOffset(2027, 3, 15, 22, 59, 0, TimeSpan.Zero), Vienna));
        Assert.True(HrFileRules.IsExpired(day, new DateTimeOffset(2027, 3, 15, 23, 0, 0, TimeSpan.Zero), Vienna));
    }

    private async Task<HttpClient> StaffAsync(TestSpa spa, StaffRole role)
    {
        var email = $"{role.ToString().ToLowerInvariant()}-{Guid.NewGuid():N}@aispa.test";
        using var client = fixture.CreateClient(spa.Cookie);
        var response = await client.PostAsJsonAsync("/api/auth/dev/register", new DevRegistration(email, role.ToString(), role, [spa.Location.Id]), TestSpa.Json, Ct);
        response.EnsureSuccessStatusCode();
        return fixture.CreateClient(string.Join("; ", response.Headers.GetValues("Set-Cookie").Select(c => c.Split(';')[0])));
    }

    /// <summary>Liest die Textzeilen aus den Hex-Zeichenketten eines von <see cref="SimplePdf"/> geschriebenen PDF; die Codepage steht hier unabhängig vom Code.</summary>
    private static class SimplePdfReader
    {
        private static readonly Dictionary<byte, char> Special = new() { [0x80] = '€', [0x84] = '„', [0x93] = '“', [0x96] = '–', [0x85] = '…' };

        public static string Text(byte[] pdf)
        {
            var raw = Encoding.Latin1.GetString(pdf);
            var lines = Regex.Matches(raw, "<([0-9A-F]+)> Tj").Select(m =>
                new string([.. Convert.FromHexString(m.Groups[1].Value).Select(b => Special.TryGetValue(b, out var c) ? c : (char)b)]));
            return string.Join('\n', lines);
        }
    }
}
