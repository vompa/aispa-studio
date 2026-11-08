using System.Net;
using System.Text.Json;
using AispaStudio.Contracts;
using AispaStudio.Desktop.ViewModels;

namespace AispaStudio.Desktop.Tests;

/// <summary>
/// Schließzeiten im Kalender (ADR 0073): das Schließband mit Lage und Farbe der Art, der Chip der Kopfzeile, die Meldung „n Termine in der Schließzeit“ mit dem Knopf nur für
/// Standortleitung und Inhaber, und die Rückfrage zum Absagen (Auswahl, Text, Zahl der Nachrichten, KI nur auf Knopfdruck, nichts ohne Klick).
/// </summary>
public sealed class ClosureTests
{
    private static readonly string CalendarRoute = $"GET /api/locations/{TestData.LocationId}/calendar?date=2027-03-01";
    /// <summary>Montag, 1.3.2027, 0:00 Uhr Wiener Ortszeit (UTC+1).</summary>
    internal static readonly DateTimeOffset Midnight = new(2027, 2, 28, 23, 0, 0, TimeSpan.Zero);

    internal static readonly Guid ClosureId = Guid.Parse("00000000-0000-0000-0000-00000000c105");

    private static readonly string[] MessageLines = ["Wegen Betriebsurlaub geschlossen.", "Grund: Inventur"];

    private static readonly string[] Rejected = ["Die KI hat den Text so verändert, dass Angaben fehlen."];

    internal static string Path(string tail = "") => $"/api/locations/{TestData.LocationId}/closures/{ClosureId}{tail}";

    /// <summary>Betriebsurlaub von Montag, 1.3. 0:00 bis Mittwoch, 3.3. Ende; mit <paramref name="open"/> noch offenen Terminen.</summary>
    internal static ClosureBand Vacation(int open = 0, string? reason = "Inventur") =>
        new(ClosureId, Midnight, Midnight.AddDays(3), ClosureKind.Vacation, "Betriebsurlaub", "Wegen Betriebsurlaub geschlossen.", reason, open);

    private static ClosureBand Outage(DateTimeOffset from, int open = 0) =>
        new(Guid.NewGuid(), from, Midnight.AddDays(1), ClosureKind.Outage, "Ausfall", "Vorübergehend geschlossen.", "Wasserschaden", open);

    internal static StubApi Api(CalendarDay day) => new StubApi()
        .On("GET /api/locations", new List<LocationDto> { TestData.Location })
        .On($"GET /api/locations/{TestData.LocationId}/resources", TestData.Resources)
        .On(CalendarRoute, day);

    internal static async Task<CalendarViewModel> OpenAsync(StubApi api, CurrentUser? user = null, FakeDialogs? dialogs = null)
    {
        var vm = new CalendarViewModel(api.Client(), user ?? TestData.Manager(), null, new ManualTime(TestData.At(10))) { Dialogs = dialogs };
        await vm.InitializeAsync();
        await vm.LoadAsync();
        return vm;
    }

    internal static async Task Until(Func<bool> condition)
    {
        for (var i = 0; i < 300 && !condition(); i++)
        {
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        Assert.True(condition());
    }

    /// <summary>Wartet, bis die Rückfrage offen ist; sonst steht im Fehler, was der Kalender stattdessen gemeldet hat.</summary>
    internal static async Task<CancelClosureDialogViewModel> OpenedAsync(CalendarViewModel vm, FakeDialogs dialogs, Task asking)
    {
        for (var i = 0; i < 300; i++)
        {
            if (dialogs.Open is CancelClosureDialogViewModel dialog)
            {
                return dialog;
            }

            if (asking.IsFaulted)
            {
                await asking;
            }

            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        throw new Xunit.Sdk.XunitException($"Die Rückfrage ging nicht auf. Meldung: {vm.Problem?.Title} {vm.Problem?.Message}");
    }

    internal static ClosureAppointmentDto Row(string guest, int hour, ClosureChannel channel, bool deposit = false) =>
        new(Guid.NewGuid(), guest, "Massage", TestData.At(hour), TestData.At(hour, 50), "Anna", AppointmentStatus.Booked, channel, deposit, false);

    internal static ClosureCancellationPreview Preview(bool ai = true, params ClosureAppointmentDto[] rows) => new(
        new ClosureDto(ClosureId, TestData.LocationId, TestData.Monday, null, TestData.Monday.AddDays(2), null, Midnight, Midnight.AddDays(3), ClosureKind.Vacation,
            "Inventur", null, ClosurePhase.Planned, "Katrin", TestData.At(8), rows.Length, 0),
        rows, "Absage Ihres Termins: {Behandlung}, {Zeit}", "Guten Tag {Anrede},\n\nwegen unseres Betriebsurlaubs haben wir geschlossen.\n\n{Behandlung}\n{Zeit}\n\nWien", ai);

    private static ClosureAppointmentDto[] Three() =>
        [Row("Ida Muster", 10, ClosureChannel.Email), Row("Sonja Test", 12, ClosureChannel.Sms, deposit: true), Row("Nora Ohne", 14, ClosureChannel.None)];

    // ---------- Band und Chip ----------

    [Fact]
    public async Task Das_Schliessband_deckt_den_ganzen_Tag_mit_Art_Grund_und_Hinweis()
    {
        var vm = await OpenAsync(Api(TestData.Day() with { Closures = [Vacation(open: 2)] }));

        var band = Assert.Single(vm.ClosureBands);
        Assert.Equal((0d, vm.DayHeight), (band.Top, band.Height));
        Assert.Equal((ClosureKind.Vacation, "Betriebsurlaub", "Betriebsurlaub · Inventur"), (band.Kind, band.Title, band.Label));
        Assert.True(band.IsVacation && !band.IsSpecial && !band.IsOutage && band.ShowLabel);
        Assert.Equal("Betriebsurlaub: Mo. 1.3. bis Mi. 3.3.\nGrund: Inventur\nText für Gäste: Wegen Betriebsurlaub geschlossen.\n2 Termine noch offen", band.Tip);
    }

    [Fact]
    public async Task Ein_halber_Tag_beginnt_zur_Uhrzeit_und_ohne_internen_Grund_steht_der_Text_fuer_Gaeste_im_Band()
    {
        var vm = await OpenAsync(Api(TestData.Day() with { Closures = [Outage(TestData.At(13)) with { InternalReason = null, Text = "Heute ab Mittag zu." }] }));

        var band = Assert.Single(vm.ClosureBands);
        var thirteen = vm.Hours.Single(h => h.Label == "13:00").Top;
        Assert.Equal(thirteen, band.Top, 0.5);
        Assert.Equal(vm.DayHeight - thirteen, band.Height, 0.5);
        Assert.Equal("Ausfall · Heute ab Mittag zu.", band.Label);
        Assert.True(band.IsOutage);
    }

    [Fact]
    public async Task Ohne_Schliesszeit_gibt_es_weder_Band_noch_Chip()
    {
        var vm = await OpenAsync(Api(TestData.Day()));

        Assert.Empty(vm.ClosureBands);
        Assert.Null(vm.ClosureChip);
        Assert.False(vm.HasClosureChip);
        Assert.DoesNotContain(vm.Messages, m => m.Kind == MessageKind.Closure);
    }

    [Fact]
    public async Task Der_Chip_nennt_Geschlossen_und_die_Art_kompakt_nur_Geschlossen()
    {
        var vm = await OpenAsync(Api(TestData.Day() with { Closures = [Vacation(open: 1)] }));

        Assert.Equal("Geschlossen · Betriebsurlaub", vm.ClosureChipLabel);
        Assert.Equal("Geschlossen: Betriebsurlaub, Mo. 1.3. bis Mi. 3.3. Wegen Betriebsurlaub geschlossen. 1 Termin noch offen.", vm.ClosureChipTip);
        Assert.True(vm.ClosureChip!.IsVacation);
        vm.SetViewport(900, 800);
        Assert.Equal("Geschlossen", vm.ClosureChipLabel);
    }

    [Theory]
    [InlineData(ClosureKind.Vacation, "Betriebsurlaub")]
    [InlineData(ClosureKind.SpecialClosure, "Sonderschließung")]
    [InlineData(ClosureKind.Outage, "Ausfall")]
    public async Task Die_Art_bestimmt_Titel_und_Farbklasse_von_Band_und_Chip(ClosureKind kind, string title)
    {
        var band = Vacation() with { Kind = kind, Title = title };
        var vm = await OpenAsync(Api(TestData.Day() with { Closures = [band] }));

        var item = Assert.Single(vm.ClosureBands);
        Assert.Equal((kind == ClosureKind.Vacation, kind == ClosureKind.SpecialClosure, kind == ClosureKind.Outage), (item.IsVacation, item.IsSpecial, item.IsOutage));
        Assert.Equal((kind == ClosureKind.Vacation, kind == ClosureKind.SpecialClosure, kind == ClosureKind.Outage),
            (vm.ClosureChip!.IsVacation, vm.ClosureChip.IsSpecial, vm.ClosureChip.IsOutage));
        Assert.Equal($"Geschlossen · {title}", vm.ClosureChipLabel);
    }

    [Fact]
    public void Der_Zeitraum_steht_in_Ortszeit_als_Tag_Bereich_oder_halber_Tag()
    {
        var vienna = TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna");
        var monday = Midnight;

        Assert.Equal("Mo. 1.3.", DayLayout.ClosurePeriod(monday, monday.AddDays(1), vienna));
        Assert.Equal("Mo. 1.3. bis Mi. 3.3.", DayLayout.ClosurePeriod(monday, monday.AddDays(3), vienna));
        Assert.Equal("Mo. 1.3. ab 13:00 Uhr", DayLayout.ClosurePeriod(TestData.At(13), monday.AddDays(1), vienna));
        Assert.Equal("Mo. 1.3. bis 18:00 Uhr", DayLayout.ClosurePeriod(monday, TestData.At(18), vienna));
        Assert.Equal("Mo. 1.3. von 13:00 bis 18:00 Uhr", DayLayout.ClosurePeriod(TestData.At(13), TestData.At(18), vienna));
        Assert.Equal("Mo. 1.3. 13:00 Uhr bis Di. 2.3. 09:00 Uhr", DayLayout.ClosurePeriod(TestData.At(13), TestData.At(9).AddDays(1), vienna));
    }

    // ---------- Meldung ----------

    [Fact]
    public async Task Die_Meldung_nennt_die_offenen_Termine_und_bietet_Termine_absagen_nur_mit_Verwalten_und_Buchen()
    {
        var day = TestData.Day() with { Closures = [Vacation(open: 3)], ClosuresDue = [Vacation(open: 3)] };

        var manager = await OpenAsync(Api(day));
        var message = Assert.Single(manager.Messages, m => m.Kind == MessageKind.Closure);
        Assert.Equal((MessageSeverity.Warning, "Schließzeit", "3 Termine in der Schließzeit (Betriebsurlaub, Mo. 1.3. bis Mi. 3.3.)."), (message.Severity, message.Title, message.Text));
        Assert.Equal(("Termine absagen …", "3 Termine in der Schließzeit", "Schließzeit"), (message.ActionLabel, message.ShortText, message.KindText));
        Assert.Equal(MessageLines, message.Lines);
        Assert.Empty(message.Subjects);
        Assert.True(manager.CanCancelClosure);

        var reception = await OpenAsync(Api(day), TestData.Reception());
        var seen = Assert.Single(reception.Messages, m => m.Kind == MessageKind.Closure);
        Assert.Null(seen.ActionLabel);
        Assert.False(reception.CanCancelClosure);
    }

    [Fact]
    public async Task Auch_eine_kommende_Schliesszeit_mit_offenen_Terminen_steht_in_den_Meldungen_eines_anderen_Tages()
    {
        var vm = await OpenAsync(Api(TestData.Day() with { ClosuresDue = [Vacation(open: 1)] }));

        var message = Assert.Single(vm.Messages, m => m.Kind == MessageKind.Closure);
        Assert.StartsWith("1 Termin in der Schließzeit (Betriebsurlaub", message.Text, StringComparison.Ordinal);
        Assert.Empty(vm.ClosureBands);
        Assert.True(vm.MessagesHaveWarning);
    }

    [Fact]
    public async Task Ohne_offene_Termine_gibt_es_keine_Meldung_und_Wegklicken_blendet_sie_bis_zur_naechsten_Aenderung_aus()
    {
        var none = await OpenAsync(Api(TestData.Day() with { Closures = [Vacation(open: 0)] }));
        Assert.DoesNotContain(none.Messages, m => m.Kind == MessageKind.Closure);

        var vm = await OpenAsync(Api(TestData.Day() with { Closures = [Vacation(open: 2)] }));
        vm.Messages.Single(m => m.Kind == MessageKind.Closure).DismissCommand!.Execute(null);
        Assert.DoesNotContain(vm.Messages, m => m.Kind == MessageKind.Closure);
    }

    [Fact]
    public async Task Die_Meldung_Schliesszeit_hat_ein_Handbuchthema_mit_Abschnitt()
    {
        var vm = await OpenAsync(Api(TestData.Day() with { Closures = [Vacation(open: 2)] }));

        var (topic, section) = vm.Messages.Single(m => m.Kind == MessageKind.Closure).Help!.Value;

        var found = global::AispaStudio.Desktop.Services.HelpContent.Find(topic);
        Assert.NotNull(found);
        Assert.Equal(("schliesszeiten-termine-absagen", "absagen"), (topic, section));
        Assert.True(found.HasSection(section!));
    }

    // ---------- Rückfrage ----------

    [Fact]
    public async Task Die_Rueckfrage_zeigt_alle_Termine_gewaehlt_mit_Zahl_der_Nachrichten_und_sagt_erst_nach_dem_Klick_ab()
    {
        var rows = Three();
        var dialogs = new FakeDialogs();
        var api = Api(TestData.Day() with { Closures = [Vacation(open: 3)] })
            .On($"GET {Path("/cancellation")}", Preview(true, rows))
            .On($"POST {Path("/cancellation")}", new CancelClosureAppointmentsResult(2, 2, 0, 0, []));
        var vm = await OpenAsync(api, dialogs: dialogs);

        var asking = vm.CancelClosureAppointmentsAsync(ClosureId);
        var dialog = await OpenedAsync(vm, dialogs, asking);

        // Vor dem Klick ist nichts geschehen; alle Termine sind gewählt, Gast ohne Kontakt wird nicht benachrichtigt.
        Assert.DoesNotContain(api.Calls, c => c.Route.StartsWith("POST", StringComparison.Ordinal));
        Assert.Equal(("Termine absagen?", true, "Termine in der Schließzeit (3 Termine)"), (dialog.Title, dialog.AllChecked, dialog.ListLabel));
        Assert.Equal(["Ida Muster", "Sonja Test", "Nora Ohne"], dialog.Rows.Select(r => r.Guest));
        Assert.Equal(["Mail", "SMS", "kein Kontakt"], dialog.Rows.Select(r => r.ChannelText));
        Assert.Equal("Mo. 1.3. 10:00 · Massage", dialog.Rows[0].Detail);
        Assert.Equal("Anzahlung zu erstatten", dialog.Rows[1].Hint);
        Assert.Equal("2 Nachrichten werden gesendet (1 Mail, 1 SMS). 1 Gast hat keinen Kontakt und wird nicht benachrichtigt.", dialog.Summary);
        Assert.Equal("3 Termine absagen", dialog.SendText);
        Assert.True(dialog.IsDestructive);
        Assert.False(dialog.AiWritten);

        // Einen abwählen, Text ändern, Umbuchung anbieten, absagen.
        dialog.Rows[2].IsChecked = false;
        Assert.Equal(("2 Nachrichten werden gesendet (1 Mail, 1 SMS).", "2 Termine absagen", false), (dialog.Summary, dialog.SendText, dialog.AllChecked));
        dialog.Text = "Guten Tag {Anrede}, leider fällt {Behandlung} am {Zeit} aus.";
        dialog.OfferRebooking = true;
        dialog.SendCommand.Execute(null);
        await asking;

        var post = api.Calls.Single(c => c.Route == $"POST {Path("/cancellation")}");
        using var json = JsonDocument.Parse(post.Body!);
        var root = json.RootElement;
        Assert.Equal([rows[0].AppointmentId, rows[1].AppointmentId], root.GetProperty("appointmentIds").EnumerateArray().Select(e => e.GetGuid()));
        Assert.Equal(("Absage Ihres Termins: {Behandlung}, {Zeit}", "Guten Tag {Anrede}, leider fällt {Behandlung} am {Zeit} aus.", true, false),
            (root.GetProperty("subject").GetString(), root.GetProperty("text").GetString(), root.GetProperty("offerRebooking").GetBoolean(), root.GetProperty("aiWritten").GetBoolean()));
        Assert.Equal("2 Termine abgesagt, 2 Nachrichten", vm.Toast);
    }

    [Fact]
    public async Task Abbrechen_sagt_nichts_ab_und_ohne_Auswahl_oder_Text_ist_der_Knopf_gesperrt()
    {
        var dialogs = new FakeDialogs();
        var api = Api(TestData.Day() with { Closures = [Vacation(open: 3)] }).On($"GET {Path("/cancellation")}", Preview(true, Three()));
        var vm = await OpenAsync(api, dialogs: dialogs);

        var asking = vm.CancelClosureAppointmentsAsync(ClosureId);
        var dialog = await OpenedAsync(vm, dialogs, asking);
        Assert.True(dialog.SendCommand.CanExecute(null));
        dialog.AllChecked = false;
        Assert.Equal(("Kein Termin gewählt.", "0 Termine absagen", false), (dialog.Summary, dialog.SendText, dialog.CanSend));
        Assert.False(dialog.SendCommand.CanExecute(null));
        dialog.AllChecked = true;
        dialog.Text = "   ";
        Assert.False(dialog.CanSend);
        dialog.Text = "Text";
        dialog.Subject = "";
        Assert.False(dialog.CanSend);
        dialog.Subject = "Betreff";
        Assert.True(dialog.SendCommand.CanExecute(null));

        dialog.CancelCommand.Execute(null);
        await asking;

        Assert.DoesNotContain(api.Calls, c => c.Route.StartsWith("POST", StringComparison.Ordinal));
        Assert.Null(vm.Toast);
    }

    [Fact]
    public async Task Die_KI_formuliert_nur_auf_Knopfdruck_kennzeichnet_den_Text_und_ein_verworfener_Text_bleibt_unveraendert()
    {
        var dialogs = new FakeDialogs();
        var api = Api(TestData.Day() with { Closures = [Vacation(open: 3)] })
            .On($"GET {Path("/cancellation")}", Preview(true, Three()))
            .On($"POST {Path("/cancellation/revise")}", new ReviseClosureTextResult("Herzlich willkommen! Guten Tag {Anrede}, {Behandlung}, {Zeit}"))
            .On($"POST {Path("/cancellation")}", new CancelClosureAppointmentsResult(3, 2, 1, 0, []));
        var vm = await OpenAsync(api, dialogs: dialogs);

        var asking = vm.CancelClosureAppointmentsAsync(ClosureId);
        var dialog = await OpenedAsync(vm, dialogs, asking);
        var original = dialog.Text;
        Assert.True(dialog.AiAvailable);
        Assert.False(dialog.AiWritten);
        Assert.StartsWith("Je Gast ersetzt der Server", dialog.TextHint, StringComparison.Ordinal);

        // Verworfen: der Text bleibt, der Grund steht am Feld, keine Kennzeichnung.
        api.On($"POST {Path("/cancellation/revise")}", new { errors = new { text = Rejected } }, HttpStatusCode.BadRequest);
        dialog.ReviseCommand.Execute("Formal");
        await Until(() => dialog.TextError is not null);
        Assert.Equal((original, false, "Die KI hat den Text so verändert, dass Angaben fehlen."), (dialog.Text, dialog.AiWritten, dialog.TextHint));

        api.On($"POST {Path("/cancellation/revise")}", new ReviseClosureTextResult("Herzlich willkommen! Guten Tag {Anrede}, {Behandlung}, {Zeit}"));
        dialog.ReviseCommand.Execute("Friendlier");
        await Until(() => dialog.AiWritten);
        Assert.Equal(("Herzlich willkommen! Guten Tag {Anrede}, {Behandlung}, {Zeit}", null), (dialog.Text, dialog.TextError));
        Assert.Equal("Die KI hat den Text umformuliert. Die Nachricht trägt beim Senden den Hinweis darauf.", dialog.TextHint);

        dialog.SendCommand.Execute(null);
        await asking;
        using var json = JsonDocument.Parse(api.Calls.Single(c => c.Route == $"POST {Path("/cancellation")}").Body!);
        Assert.True(json.RootElement.GetProperty("aiWritten").GetBoolean());
        Assert.Equal("3 Termine abgesagt, 2 Nachrichten, 1 ohne Kontakt", vm.Toast);
    }

    [Fact]
    public async Task Ohne_KI_gibt_es_keine_Umformulieren_Knoepfe()
    {
        var dialogs = new FakeDialogs();
        var api = Api(TestData.Day() with { Closures = [Vacation(open: 3)] }).On($"GET {Path("/cancellation")}", Preview(false, Three()));
        var vm = await OpenAsync(api, dialogs: dialogs);

        var asking = vm.CancelClosureAppointmentsAsync(ClosureId);
        var dialog = await OpenedAsync(vm, dialogs, asking);

        Assert.False(dialog.AiAvailable);
        dialog.CancelCommand.Execute(null);
        await asking;
    }

    [Fact]
    public async Task Ein_Fehler_beim_Laden_und_beim_Absagen_steht_als_Meldung_mit_Erneut_versuchen()
    {
        var dialogs = new FakeDialogs();
        var api = Api(TestData.Day() with { Closures = [Vacation(open: 3)] }).On($"GET {Path("/cancellation")}", null, HttpStatusCode.InternalServerError);
        var vm = await OpenAsync(api, dialogs: dialogs);

        await vm.CancelClosureAppointmentsAsync(ClosureId);
        Assert.Empty(dialogs.Shown);
        Assert.Equal(("Termine nicht geladen", "Erneut versuchen"), (vm.Problem!.Title, vm.Problem.ActionText));

        api.On($"GET {Path("/cancellation")}", Preview(true, Three())).On($"POST {Path("/cancellation")}", null, HttpStatusCode.Conflict);
        var asking = vm.CancelClosureAppointmentsAsync(ClosureId);
        (await OpenedAsync(vm, dialogs, asking)).SendCommand.Execute(null);
        await asking;
        Assert.Equal("Termine nicht abgesagt", vm.Problem!.Title);
    }

    [Fact]
    public async Task Ohne_die_Rechte_oeffnet_sich_keine_Rueckfrage()
    {
        var dialogs = new FakeDialogs();
        var api = Api(TestData.Day() with { Closures = [Vacation(open: 3)] }).On($"GET {Path("/cancellation")}", Preview(true, Three()));
        var vm = await OpenAsync(api, TestData.Reception(), dialogs);

        await vm.CancelClosureAppointmentsAsync(ClosureId);

        Assert.Empty(dialogs.Shown);
        Assert.DoesNotContain(api.Calls, c => c.Route.Contains("cancellation", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(new[] { ClosureChannel.Email }, "1 Nachricht wird gesendet (1 Mail).")]
    [InlineData(new[] { ClosureChannel.Sms, ClosureChannel.Sms, ClosureChannel.Email }, "3 Nachrichten werden gesendet (1 Mail, 2 SMS).")]
    [InlineData(new[] { ClosureChannel.None }, "Es wird keine Nachricht gesendet. 1 Gast hat keinen Kontakt und wird nicht benachrichtigt.")]
    [InlineData(new[] { ClosureChannel.None, ClosureChannel.None, ClosureChannel.Email }, "1 Nachricht wird gesendet (1 Mail). 2 Gäste haben keinen Kontakt und werden nicht benachrichtigt.")]
    public void Die_Zusammenfassung_nennt_Nachrichten_und_Gaeste_ohne_Kontakt(ClosureChannel[] channels, string expected) =>
        Assert.Equal(expected, CancelClosureDialogViewModel.Summarize(channels));

    [Fact]
    public void Der_Text_der_Rueckmeldung_nennt_Termine_Nachrichten_und_Gaeste_ohne_Kontakt()
    {
        Assert.Equal("1 Termin abgesagt, 1 Nachricht", CalendarViewModel.CancelledText(new CancelClosureAppointmentsResult(1, 1, 0, 0, [])));
        Assert.Equal("4 Termine abgesagt, 3 Nachrichten, 1 ohne Kontakt", CalendarViewModel.CancelledText(new CancelClosureAppointmentsResult(4, 3, 1, 0, [])));
        Assert.Equal("1 Termin abgesagt, 1 ohne Kontakt", CalendarViewModel.CancelledText(new CancelClosureAppointmentsResult(1, 0, 1, 0, [])));
    }
}
