using System.Net.Http.Json;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Tests;

/// <summary>
/// Betriebsfragen zu Team, Tag und Belegung (ADR 0072, Szenario 2): wer arbeitet, ist eine Person da, wie viele Termine sind es noch, wie voll ist der Tag,
/// welcher Raum ist frei, ist die Sauna voll. Aus Dienstplan, Kalender und Belegung; „jetzt“ ist ein fester Zeitpunkt (Montag, 1. März 2027, 11:15 Uhr in Wien).
/// </summary>
public sealed class BusinessTeamDayTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly DateTimeOffset MondayNow = new(2027, 3, 1, 10, 15, 0, TimeSpan.Zero);

    private readonly AnswerKit _kit = new(fixture);

    private static Task<RosterDayResult> PlanAsync(TestSpa spa, Guid employee, DateOnly date, params RosterEntry[] entries) =>
        spa.PutAsync<RosterDayResult>($"/api/locations/{spa.Location.Id}/roster/{employee}/{date:yyyy-MM-dd}", new RosterDayInput(entries, null), Ct);

    // ---------- Team ----------

    [Fact]
    public async Task Wer_arbeitet_heute_nennt_Dienste_und_wer_nicht_da_ist()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await PlanAsync(spa, spa.Ben.Id, TestSpa.Monday, new RosterEntry(RosterKind.Vacation, null, null, null));

        var block = await _kit.AnswerAsync(spa, "Wer arbeitet heute?", MondayNow);
        var tuesday = await _kit.AnswerAsync(spa, "Wer arbeitet morgen?", MondayNow);

        // Ä316: der Kopfsatz nennt nur die Zahlen, die Liste je Person einmal Dienst oder Abwesenheit; ein Klick zeigt den Wochenplan dieses Tages.
        Assert.Equal("Am Montag, 01.03.2027 arbeitet im Standort Wien 1 Person; 1 ist nicht da.", block.Text);
        Assert.Equal("Dienstplan und Arbeitszeiten des Standorts Wien", block.Source);
        Assert.Null(block.Table);
        Assert.Equal([("Anna", "09:00 bis 17:00"), ("Ben", "Urlaub")], block.Items!.Select(i => (i.Title, i.Detail)));
        Assert.Equal([spa.Anna.Id, spa.Ben.Id], block.Items!.Select(i => i.Link!.TargetId!.Value));
        Assert.All(block.Items!, i => Assert.Equal((AssistantLinkKind.Roster, "roster", TestSpa.Monday, spa.Location.Id, "Im Wochenplan zeigen"),
            (i.Link!.Kind, i.Link.Page, i.Link.From!.Value, i.Link.LocationId!.Value, i.Action)));
        Assert.Equal("Am Dienstag, 02.03.2027 ist im Standort Wien niemand im Dienst eingeteilt.", tuesday.Text);
    }

    [Fact]
    public async Task Dienste_einer_Woche_stehen_je_Tag_in_der_Tabelle()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);

        var block = await _kit.AnswerAsync(spa, "Wer arbeitet nächste Woche?", MondayNow);

        Assert.Equal("08.03.2027 bis 14.03.2027", block.Period);
        Assert.Equal(7, block.Table!.Rows.Count);
        Assert.Equal(["Montag, 08.03.2027", "Anna, Ben"], block.Table.Rows[0]);
        Assert.Equal(["Dienstag, 09.03.2027", "niemand eingeteilt"], block.Table.Rows[1]);
    }

    [Fact]
    public async Task Ist_eine_Person_da_antwortet_der_Dienstplan_mit_Urlaub_und_Krankenstand()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var nextMonday = TestSpa.Monday.AddDays(7);
        await PlanAsync(spa, spa.Anna.Id, nextMonday, new RosterEntry(RosterKind.Vacation, null, null, null));
        await PlanAsync(spa, spa.Anna.Id, nextMonday.AddDays(1), new RosterEntry(RosterKind.Sick, null, null, null));

        var week = await _kit.AnswerAsync(spa, "Ist Anna nächste Woche da?", MondayNow);
        var thisMonday = await _kit.AnswerAsync(spa, "Ist Anna heute da?", MondayNow);
        var unknown = await _kit.AnswerAsync(spa, "Ist Xaver nächste Woche da?", MondayNow);

        Assert.Equal("Anna, 08.03.2027 bis 14.03.2027: im Dienst an 0 von 7 Tagen.", week.Text);
        Assert.Equal(["Montag, 08.03.2027", "Urlaub"], week.Table!.Rows[0]);
        Assert.Equal(["Dienstag, 09.03.2027", "Krankenstand"], week.Table.Rows[1]);
        Assert.Equal(["Mittwoch, 10.03.2027", "nicht eingeteilt"], week.Table.Rows[2]);
        Assert.Equal("Anna ist am Montag, 01.03.2027 im Dienst: 09:00 bis 17:00.", thisMonday.Text);
        Assert.Equal(AssistantBlockKind.NotFound, unknown.Kind);
        Assert.Equal("Eine Person namens „Xaver“ gibt es im Team des Standorts Wien nicht.", unknown.Text);
    }

    // ---------- Tag ----------

    [Fact]
    public async Task Termine_heute_noch_zaehlen_nach_Stand_und_Uhrzeit()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var done = await spa.BookOkAsync(TestSpa.At(10), Ct, spa.Ben.Id);
        await spa.PostAsync<AppointmentDto>($"/api/appointments/{done.Id}/status", new AppointmentStatusChange(AppointmentStatus.CheckedIn), Ct);
        await spa.PostAsync<AppointmentDto>($"/api/appointments/{done.Id}/status", new AppointmentStatusChange(AppointmentStatus.Completed), Ct);
        await spa.BookOkAsync(TestSpa.At(11), Ct, spa.Anna.Id);
        await spa.BookOkAsync(TestSpa.At(14), Ct, spa.Ben.Id);
        var cancelled = await spa.BookOkAsync(TestSpa.At(15, 30), Ct, spa.Anna.Id);
        await spa.PostAsync<AppointmentDto>($"/api/appointments/{cancelled.Id}/cancel", new CancelAppointmentRequest(null), Ct);

        var block = await _kit.AnswerAsync(spa, "Wie viele Termine haben wir heute noch?", MondayNow);
        var friday = await _kit.AnswerAsync(spa, "Wie viele Termine haben wir am Montag?", MondayNow);

        // Um 11:15 läuft der Termin von 11:00; um 14:00 steht noch einer an; der von 10:00 ist abgeschlossen, der von 15:30 storniert.
        Assert.Equal("Heute stehen im Standort Wien noch 1 Termin an; 1 läuft gerade. Insgesamt sind es heute 3, davon 1 abgeschlossen.", block.Text);
        Assert.Equal([["Gebucht", "2"], ["Eingecheckt", "0"], ["Abgeschlossen", "1"], ["Nicht erschienen", "0"]], block.Table!.Rows);
        Assert.Equal("Kalender des Standorts Wien", block.Source);
        Assert.Equal("Am Montag, 01.03.2027 stehen im Standort Wien 3 Termine im Kalender.", friday.Text);
    }

    [Fact]
    public async Task Therapeutinnen_sehen_bei_den_Terminen_nur_die_eigenen()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await spa.BookOkAsync(TestSpa.At(14), Ct, spa.Anna.Id);
        await spa.BookOkAsync(TestSpa.At(15), Ct, spa.Ben.Id);

        var own = await _kit.AnswerAsync(spa, "Wie viele Termine haben wir heute noch?", MondayNow, StaffRole.Therapist, employeeId: spa.Anna.Id);

        Assert.StartsWith("Heute stehen für Sie noch 1 Termin an", own.Text, StringComparison.Ordinal);
        Assert.Equal("Ihre Termine im Kalender des Standorts Wien", own.Source);
    }

    [Fact]
    public async Task Wie_voll_der_Tag_ist_sagt_der_Tagesueberblick_mit_Auslastung_laut_Dienstplan()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await spa.BookOkAsync(TestSpa.At(10), Ct, spa.Anna.Id);
        await spa.BookOkAsync(TestSpa.At(12), Ct, spa.Ben.Id);

        var block = await _kit.AnswerAsync(spa, "Wie voll sind wir am Montag?", MondayNow);
        var empty = await _kit.AnswerAsync(spa, "Wie voll sind wir am Dienstag?", MondayNow);

        // Zwei Behandlungen à 50 Minuten bei zwei Personen mit je 8 Stunden Dienst: 100 von 960 Minuten.
        Assert.Equal("Am Montag, 01.03.2027 sind im Standort Wien 2 Termine gebucht; die Auslastung laut Dienstplan liegt bei 10 %.", block.Text);
        Assert.Equal("Tagesüberblick (Kalender und Dienstplan) des Standorts Wien", block.Source);
        Assert.Equal("Am Dienstag, 02.03.2027 sind im Standort Wien noch keine Termine gebucht.", empty.Text);
    }

    // ---------- Räume und Bereiche ----------

    [Fact]
    public async Task Welcher_Raum_jetzt_frei_ist_sagt_die_Belegung()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await spa.BookOkAsync(TestSpa.At(11), Ct, spa.Anna.Id, spa.RoomA.Id);
        await spa.BookOkAsync(TestSpa.At(13), Ct, spa.Ben.Id, spa.RoomB.Id);

        var now = await _kit.AnswerAsync(spa, "Welcher Raum ist jetzt frei?", MondayNow);
        var named = await _kit.AnswerAsync(spa, "Welcher Raum ist jetzt frei? Raum A", MondayNow);

        Assert.Equal("Jetzt (11:15 Uhr) ist frei: Raum B. Belegt: Raum A.", now.Text);
        Assert.Equal(["Raum A", "belegt bis 12:15 Uhr"], now.Table!.Rows[0]);
        Assert.Equal(["Raum B", "frei bis 12:55 Uhr"], now.Table.Rows[1]);
        Assert.Equal("Belegung (Kalender) der Räume des Standorts Wien, Stand 11:15 Uhr", now.Source);
        Assert.Equal("Jetzt (11:15 Uhr) ist im Standort Wien kein Raum frei.", named.Text);
    }

    [Fact]
    public async Task Frei_bis_meint_den_heutigen_Tag_eine_Buchung_von_morgen_zaehlt_nicht()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await spa.BookOkAsync(TestSpa.At(10, date: TestSpa.Monday.AddDays(7)), Ct, spa.Anna.Id, spa.RoomA.Id);

        var block = await _kit.AnswerAsync(spa, "Welcher Raum ist jetzt frei?", MondayNow);

        Assert.Equal("Jetzt (11:15 Uhr) sind frei: Raum A, Raum B.", block.Text);
        Assert.Equal(["Raum A", "frei, heute nichts mehr gebucht"], block.Table!.Rows[0]);
    }

    [Fact]
    public async Task Ist_die_Sauna_voll_nennt_Plaetze_belegt_jetzt_und_frei()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var sauna = await spa.PostAsync<RoomDto>($"/api/locations/{spa.Location.Id}/rooms", new RoomInput("Sauna", 0, 8, IsArea: true), Ct);
        await spa.Client.PostAsJsonAsync("/api/area-visits", new AreaVisitRequest(spa.Location.Id, sauna.Id, spa.Customer.Id, TestSpa.At(10), TestSpa.At(13), 5), TestSpa.Json, Ct);
        await spa.Client.PostAsJsonAsync("/api/area-visits", new AreaVisitRequest(spa.Location.Id, sauna.Id, spa.Customer.Id, TestSpa.At(12), TestSpa.At(14), 2), TestSpa.Json, Ct);

        var now = await _kit.AnswerAsync(spa, "Ist die Sauna voll?", MondayNow);
        var evening = await _kit.AnswerAsync(spa, "Ist die Sauna um 20 Uhr voll?", new DateTimeOffset(2027, 3, 1, 19, 0, 0, TimeSpan.Zero));

        Assert.Equal("Jetzt (11:15 Uhr) — Sauna: 5 von 8 Plätzen belegt, 3 frei.", now.Text);
        Assert.Equal(["Sauna", "8", "5", "3"], now.Table!.Rows[0]);
        Assert.Equal("Bereiche und Eintritte (Kalender) des Standorts Wien", now.Source);
        Assert.Equal(["Sauna", "8", "0", "8"], evening.Table!.Rows[0]);
    }

    [Fact]
    public async Task Ohne_Bereiche_und_ohne_Raeume_sagt_die_Antwort_es_und_verweist_auf_die_Einstellung()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var bare = await AnswerKit.LocationAsync(spa, "Leer", null);

        var sauna = await _kit.AnswerAsync(spa, "Ist die Sauna voll?", locationId: bare.Id);
        var rooms = await _kit.AnswerAsync(spa, "Welcher Raum ist jetzt frei?", locationId: bare.Id);

        Assert.Equal(AssistantBlockKind.NotStored, sauna.Kind);
        Assert.Equal("Dazu ist nichts hinterlegt: Bereiche wie Sauna oder Ruheraum.", sauna.Text);
        Assert.Equal("Bereiche anlegen", sauna.Link!.Label);
        Assert.Equal("Dazu ist nichts hinterlegt: Räume.", rooms.Text);
    }

    [Fact]
    public async Task Team_Tag_und_Belegung_sind_nur_mit_Kalender_zu_sehen_das_Backoffice_bekommt_sie_nicht()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);

        foreach (var question in new[] { "Wer arbeitet heute?", "Ist Anna heute da?", "Wie viele Termine haben wir heute noch?", "Wie voll sind wir am Montag?", "Welcher Raum ist jetzt frei?", "Ist die Sauna voll?" })
        {
            var denied = await _kit.AnswerAsync(spa, question, MondayNow, StaffRole.Backoffice);
            Assert.Equal(AssistantBlockKind.Denied, denied.Kind);
            Assert.StartsWith("Dafür fehlt die Berechtigung.", denied.Text, StringComparison.Ordinal);
        }
    }
}
