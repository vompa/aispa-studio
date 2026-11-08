using System.Net;
using System.Text.Json;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Controls;
using AispaStudio.Desktop.ViewModels;

namespace AispaStudio.Desktop.Tests;

/// <summary>
/// Die Rückfrage „Termine absagen?“ mit Sprache je Gast, Abschnitt Warteliste und Rückzahlung der Anzahlung, die Meldung als Warnung oder Fehler, und der Knopf „Anzahlung retournieren“
/// im Termin nur für Inhaber und Manager (ADR 0073, Ä46 bis Ä49).
/// </summary>
public sealed class ClosureWaitlistLanguageTests
{
    private static ClosureAppointmentDto Guest(string name, int hour, ClosureChannel channel, string language = "de", decimal? deposit = null) =>
        new(Guid.NewGuid(), name, "Massage", TestData.At(hour), TestData.At(hour, 50), "Anna", AppointmentStatus.Booked, channel, deposit is not null, false, language, deposit);

    private static ClosureWaitlistDto Wish(string name, ClosureChannel channel, string language, int fromDay = 0, int untilDay = 2) =>
        new(Guid.NewGuid(), name, "Massage", TestData.Monday.AddDays(fromDay), TestData.Monday.AddDays(untilDay), DayPart.Any, channel, language);

    private static ClosureCancellationPreview Preview(ClosureAppointmentDto[] rows, ClosureWaitlistDto[]? waitlist = null, bool ai = true) =>
        ClosureTests.Preview(ai, rows) with { Waitlist = waitlist };

    private static async Task<(CalendarViewModel Vm, CancelClosureDialogViewModel Dialog, Task Asking, StubApi Api)> OpenAsync(ClosureCancellationPreview preview)
    {
        var dialogs = new FakeDialogs();
        var api = ClosureTests.Api(TestData.Day() with { Closures = [ClosureTests.Vacation(open: preview.Appointments.Count)] })
            .On($"GET {ClosureTests.Path("/cancellation")}", preview)
            .On($"POST {ClosureTests.Path("/cancellation")}", new CancelClosureAppointmentsResult(2, 4, 0, 0, [], 2, 1, 0))
            .On($"POST {ClosureTests.Path("/cancellation/revise")}", new ReviseClosureTextResult("Herzlich willkommen! Guten Tag {Anrede}"));
        var vm = await ClosureTests.OpenAsync(api, dialogs: dialogs);
        var asking = vm.CancelClosureAppointmentsAsync(ClosureTests.ClosureId);
        return (vm, await ClosureTests.OpenedAsync(vm, dialogs, asking), asking, api);
    }

    // ---------- Sprache je Gast (Ä46) ----------

    [Fact]
    public async Task Die_Rueckfrage_zeigt_je_Gast_das_Sprachkuerzel_und_zaehlt_die_Nachrichten_je_Sprache()
    {
        ClosureAppointmentDto[] rows =
        [
            Guest("Ida Muster", 10, ClosureChannel.Email), Guest("Emma Smith", 11, ClosureChannel.Email, "en"), Guest("Chloé Martin", 12, ClosureChannel.Sms, "fr"),
            Guest("Anna Weiss", 13, ClosureChannel.Email, "en"), Guest("Nora Ohne", 14, ClosureChannel.None, "it"),
        ];
        var (_, dialog, asking, _) = await OpenAsync(Preview(rows));

        Assert.Equal(["DE", "EN", "FR", "EN", "IT"], dialog.Rows.Select(r => r.LanguageCode));
        Assert.Equal(["Deutsch", "Englisch", "Französisch", "Englisch", "Italienisch"], dialog.Rows.Select(r => r.LanguageName));
        // Die Sprache steht im Namen für Screenreader, wenn sie nicht Deutsch ist; ein Gast ohne Kontakt bekommt keine Nachricht und zählt in keiner Sprache.
        Assert.Equal("Ida Muster, Mo. 1.3. 10:00 · Massage, Mail", dialog.Rows[0].Name);
        Assert.Equal("Emma Smith, Mo. 1.3. 11:00 · Massage, Mail, Englisch", dialog.Rows[1].Name);
        Assert.Equal("Nachrichten nach Sprache: 1 Deutsch, 2 Englisch, 1 Französisch.", dialog.LanguageSummary);
        Assert.True(dialog.HasLanguageSummary);

        dialog.Rows[1].IsChecked = false;
        dialog.Rows[3].IsChecked = false;
        Assert.Equal("Nachrichten nach Sprache: 1 Deutsch, 1 Französisch.", dialog.LanguageSummary);
        Assert.StartsWith("Je Gast ersetzt der Server", dialog.TextHint, StringComparison.Ordinal);
        Assert.Contains("deutsche Vorlage", dialog.TextHint, StringComparison.Ordinal);
        Assert.Contains("Standardvorlage ihrer Sprache", dialog.TextHint, StringComparison.Ordinal);

        dialog.CancelCommand.Execute(null);
        await asking;
    }

    [Fact]
    public void Die_Sprachen_stehen_in_fester_Reihenfolge_und_ohne_Nachricht_steht_nichts()
    {
        Assert.Equal("Nachrichten nach Sprache: 2 Deutsch, 1 Englisch, 3 Französisch, 1 Italienisch.",
            CancelClosureDialogViewModel.SummarizeLanguages(["it", "fr", "de", "fr", "en", null, "fr"]));
        Assert.Equal("", CancelClosureDialogViewModel.SummarizeLanguages([]));
        Assert.Equal(("DE", "Deutsch"), (ClosureLanguage.Code(null), ClosureLanguage.Name("")));
        Assert.Equal(("EN", "Englisch"), (ClosureLanguage.Code("en"), ClosureLanguage.Name("EN")));
    }

    [Fact]
    public async Task Die_KI_formuliert_die_deutsche_Vorlage_um_und_der_Server_bekommt_die_Sprache_Deutsch()
    {
        var (_, dialog, asking, api) = await OpenAsync(Preview([Guest("Ida Muster", 10, ClosureChannel.Email)]));

        dialog.ReviseCommand.Execute("Formal");
        await ClosureTests.Until(() => dialog.AiWritten);

        using var json = JsonDocument.Parse(api.Calls.Single(c => c.Route == $"POST {ClosureTests.Path("/cancellation/revise")}").Body!);
        Assert.Equal("de", json.RootElement.GetProperty("language").GetString());
        dialog.CancelCommand.Execute(null);
        await asking;
    }

    // ---------- Warteliste (Ä48) ----------

    [Fact]
    public async Task Die_Warteliste_ist_ein_eigener_Abschnitt_mit_Auswahl_und_die_Nachrichten_zaehlen_sie_mit()
    {
        var wishes = new[] { Wish("Wanda Warte", ClosureChannel.Email, "fr"), Wish("Willi Wunsch", ClosureChannel.Sms, "de", 1, 1), Wish("Walter Ohne", ClosureChannel.None, "en") };
        var (_, dialog, asking, _) = await OpenAsync(Preview([Guest("Ida Muster", 10, ClosureChannel.Email)], wishes));

        Assert.True(dialog.HasWaitlist);
        Assert.Equal("Warteliste (3)", dialog.WaitlistLabel);
        Assert.Equal(["Wanda Warte", "Willi Wunsch", "Walter Ohne"], dialog.WaitlistRows.Select(r => r.Guest));
        Assert.Equal(["Wunsch Mo. 1.3. bis Mi. 3.3. · Massage", "Wunsch Di. 2.3. · Massage", "Wunsch Mo. 1.3. bis Mi. 3.3. · Massage"], dialog.WaitlistRows.Select(r => r.Detail));
        Assert.Equal(["Mail", "SMS", "kein Kontakt"], dialog.WaitlistRows.Select(r => r.ChannelText));
        Assert.Equal(["FR", "DE", "EN"], dialog.WaitlistRows.Select(r => r.LanguageCode));
        Assert.Equal("Wanda Warte, Wunsch Mo. 1.3. bis Mi. 3.3. · Massage, Mail, Französisch", dialog.WaitlistRows[0].Name);
        Assert.All(dialog.WaitlistRows, r => Assert.True(r.IsChecked));
        Assert.True(dialog.AllWaitlistChecked);
        Assert.Contains("bleiben auf der Liste", dialog.WaitlistHint, StringComparison.Ordinal);

        // Die Nachrichten zählen Termine und Einträge zusammen; wer keinen Kontakt hat, wird nicht benachrichtigt.
        Assert.Equal("3 Nachrichten werden gesendet (2 Mail, 1 SMS). 1 Gast hat keinen Kontakt und wird nicht benachrichtigt.", dialog.Summary);
        Assert.Equal("1 Termin absagen, 3 benachrichtigen", dialog.SendText);

        dialog.AllWaitlistChecked = false;
        Assert.Equal(("1 Nachricht wird gesendet (1 Mail).", "1 Termin absagen", false), (dialog.Summary, dialog.SendText, dialog.AllWaitlistChecked));
        dialog.WaitlistRows[1].IsChecked = true;
        Assert.Equal("1 Termin absagen, 1 benachrichtigen", dialog.SendText);

        dialog.CancelCommand.Execute(null);
        await asking;
    }

    [Fact]
    public async Task Nur_Eintraege_der_Warteliste_brauchen_keinen_Text_und_die_Anfrage_nennt_die_gewaehlten_Eintraege()
    {
        var wishes = new[] { Wish("Wanda Warte", ClosureChannel.Email, "fr"), Wish("Willi Wunsch", ClosureChannel.Email, "de") };
        var (vm, dialog, asking, api) = await OpenAsync(Preview([], wishes));

        Assert.False(dialog.HasRows);
        Assert.False(dialog.IsEmpty);
        Assert.Equal(("2 Einträge benachrichtigen", true), (dialog.SendText, dialog.CanSend));
        dialog.Text = "";
        dialog.Subject = "";
        Assert.True(dialog.CanSend);
        dialog.WaitlistRows[0].IsChecked = false;
        Assert.Equal("1 Eintrag benachrichtigen", dialog.SendText);
        dialog.WaitlistRows[1].IsChecked = false;
        Assert.False(dialog.CanSend);
        dialog.WaitlistRows[0].IsChecked = true;
        dialog.SendCommand.Execute(null);
        await asking;

        using var json = JsonDocument.Parse(api.Calls.Single(c => c.Route == $"POST {ClosureTests.Path("/cancellation")}").Body!);
        Assert.Empty(json.RootElement.GetProperty("appointmentIds").EnumerateArray());
        Assert.Equal([wishes[0].EntryId], json.RootElement.GetProperty("waitlistEntryIds").EnumerateArray().Select(e => e.GetGuid()));
        Assert.NotNull(vm.Toast);
    }

    [Fact]
    public async Task Ohne_Termine_und_ohne_Eintraege_gibt_es_nur_Abbrechen()
    {
        var (_, dialog, asking, _) = await OpenAsync(Preview([], []));

        Assert.True(dialog.IsEmpty);
        Assert.Contains("keinen Eintrag der Warteliste", dialog.EmptyText, StringComparison.Ordinal);
        Assert.False(dialog.HasWaitlist);
        dialog.CancelCommand.Execute(null);
        await asking;
    }

    [Fact]
    public void Die_Rueckmeldung_nennt_Warteliste_und_Rueckzahlung()
    {
        Assert.Equal("2 Termine abgesagt, 4 Nachrichten, 2 Wartelisten-Einträge benachrichtigt, 1 Anzahlung retourniert",
            CalendarViewModel.CancelledText(new CancelClosureAppointmentsResult(2, 4, 0, 0, [], 2, 1, 0)));
        Assert.Equal("1 Nachricht, 1 Wartelisten-Eintrag benachrichtigt", CalendarViewModel.CancelledText(new CancelClosureAppointmentsResult(0, 1, 0, 0, [], 1)));
        Assert.Equal("3 Termine abgesagt, 2 Nachrichten, 2 Anzahlungen retourniert, 1 ohne Kontakt", CalendarViewModel.CancelledText(new CancelClosureAppointmentsResult(3, 2, 1, 0, [], 0, 2)));
    }

    // ---------- Anzahlung (Ä47) ----------

    [Fact]
    public async Task Der_Hinweis_nennt_den_Betrag_der_automatisch_zurueckgeht_und_die_Zeile_fasst_ihn_zusammen()
    {
        ClosureAppointmentDto[] rows =
        [
            Guest("Ida Muster", 10, ClosureChannel.Email, deposit: 30m), Guest("Sonja Test", 12, ClosureChannel.Sms, deposit: 20m),
            Guest("Nora Ohne", 14, ClosureChannel.None), Guest("Petra Paid", 15, ClosureChannel.Email) with { HasDeposit = true },
            Guest("Tina Treat", 16, ClosureChannel.Email) with { PrepaidOnPlatform = true },
        ];
        var (_, dialog, asking, _) = await OpenAsync(Preview(rows));

        Assert.Equal("Anzahlung 30,00 € wird retourniert", dialog.Rows[0].Hint);
        Assert.Null(dialog.Rows[2].Hint);
        // Eine Zahlung, deren Beleg noch entsteht, nennt noch keinen Betrag; Treatwell erstattet die Rezeption dort.
        Assert.Equal("Anzahlung zu erstatten", dialog.Rows[3].Hint);
        Assert.Equal("bei Treatwell bezahlt", dialog.Rows[4].Hint);
        Assert.Equal("2 Anzahlungen (50,00 €) gehen über die Kassa zurück (je ein Storno-Beleg, auf dem Zahlungsweg der Einzahlung).", dialog.RefundSummary);

        dialog.Rows[1].IsChecked = false;
        Assert.Equal("Eine Anzahlung (30,00 €) geht über die Kassa zurück (Storno-Beleg, auf dem Zahlungsweg der Einzahlung).", dialog.RefundSummary);
        dialog.Rows[0].IsChecked = false;
        Assert.Equal("", dialog.RefundSummary);
        Assert.False(dialog.HasRefundSummary);
        Assert.Contains("eine geleistete Anzahlung geht über die Kassa zurück", dialog.Message, StringComparison.Ordinal);

        dialog.CancelCommand.Execute(null);
        await asking;
    }

    [Fact]
    public async Task Konnte_eine_Anzahlung_nicht_retourniert_werden_steht_es_als_Meldung_mit_dem_Weg()
    {
        var dialogs = new FakeDialogs();
        var api = ClosureTests.Api(TestData.Day() with { Closures = [ClosureTests.Vacation(open: 1)] })
            .On($"GET {ClosureTests.Path("/cancellation")}", Preview([Guest("Ida Muster", 10, ClosureChannel.Email, deposit: 30m)]))
            .On($"POST {ClosureTests.Path("/cancellation")}", new CancelClosureAppointmentsResult(1, 1, 0, 0, [], 0, 0, 1));
        var vm = await ClosureTests.OpenAsync(api, dialogs: dialogs);

        var asking = vm.CancelClosureAppointmentsAsync(ClosureTests.ClosureId);
        (await ClosureTests.OpenedAsync(vm, dialogs, asking)).SendCommand.Execute(null);
        await asking;

        Assert.Equal("Anzahlung nicht retourniert", vm.Problem!.Title);
        Assert.Contains("Bitte an der Kassa erstatten.", vm.Problem.Message, StringComparison.Ordinal);
        Assert.Equal(InfoBarSeverity.Warning, vm.Problem.Severity);
    }

    // ---------- Meldung (Ä49) ----------

    [Fact]
    public async Task Die_Meldung_ist_eine_Warnung_bei_fernem_Beginn_und_ein_Fehler_wenn_die_Schliesszeit_laeuft_oder_bald_beginnt()
    {
        var far = ClosureTests.Vacation(open: 2);
        var near = ClosureTests.Vacation(open: 1) with { Id = Guid.NewGuid(), Urgent = true };
        var vm = await ClosureTests.OpenAsync(ClosureTests.Api(TestData.Day() with { ClosuresDue = [far, near] }));

        var messages = vm.Messages.Where(m => m.Kind == MessageKind.Closure).OrderBy(m => m.Severity).ToList();

        Assert.Equal([MessageSeverity.Warning, MessageSeverity.Error], messages.Select(m => m.Severity));
        Assert.StartsWith("2 Termine in der Schließzeit", messages[0].Text, StringComparison.Ordinal);
        Assert.StartsWith("1 Termin in der Schließzeit", messages[1].Text, StringComparison.Ordinal);
        Assert.True(vm.MessagesHaveError);
    }

    [Theory]
    [InlineData(2, 0, "2 Termine")]
    [InlineData(0, 1, "1 Eintrag der Warteliste")]
    [InlineData(0, 3, "3 Einträge der Warteliste")]
    [InlineData(1, 2, "1 Termin und 2 Einträge der Warteliste")]
    public async Task Die_Meldung_nennt_Termine_und_Eintraege_der_Warteliste_auch_wenn_nur_Eintraege_da_sind(int appointments, int waitlist, string what)
    {
        var band = ClosureTests.Vacation(open: appointments) with { WaitlistEntries = waitlist };
        var vm = await ClosureTests.OpenAsync(ClosureTests.Api(TestData.Day() with { ClosuresDue = [band] }));

        var message = Assert.Single(vm.Messages, m => m.Kind == MessageKind.Closure);

        Assert.StartsWith($"{what} in der Schließzeit (Betriebsurlaub", message.Text, StringComparison.Ordinal);
        Assert.Equal("Termine absagen …", message.ActionLabel);
    }

    [Fact]
    public async Task Ohne_Termine_und_ohne_Eintraege_gibt_es_keine_Meldung()
    {
        var vm = await ClosureTests.OpenAsync(ClosureTests.Api(TestData.Day() with { ClosuresDue = [ClosureTests.Vacation(open: 0) with { Urgent = true }] }));

        Assert.DoesNotContain(vm.Messages, m => m.Kind == MessageKind.Closure);
    }

    // ---------- Anzahlung retournieren im Termin (Ä47) ----------

    private static readonly DepositReturnInfo OpenDeposit = new(Guid.NewGuid(), "K1 Nr. 12, 01.03.2027", 20m, "Bar", TestData.At(9), true);

    private static readonly TimeZoneInfo Vienna = TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna");

    private static string DepositPath(AppointmentDto appointment) => $"/api/appointments/{appointment.Id}/deposit-return";

    private static AppointmentViewModel Panel(StubApi api, AppointmentDto appointment, FakeDialogs dialogs, bool mayReturn, Action<string>? toast = null, Action<string, string?>? report = null) =>
        new(api.Client(), appointment, "Anna", "Raum Zeder", Vienna, true, true, _ => Task.CompletedTask, () => { }, currency: "EUR")
        {
            Dialogs = dialogs,
            MayReturnDeposit = mayReturn,
            ShowToast = toast,
            ReportProblem = report,
        };

    [Fact]
    public async Task Der_Knopf_Anzahlung_retournieren_fragt_nach_mit_Pflichtgrund_und_stellt_erst_dann_den_Storno_Beleg_aus()
    {
        var appointment = TestData.Appointment(10, TestData.Anna);
        var api = new StubApi().On($"GET {DepositPath(appointment)}", OpenDeposit).On($"POST {DepositPath(appointment)}", SpaSampleReceipt());
        var dialogs = new FakeDialogs().Answer(ConfirmChoice.Confirmed, "   ").Answer(ConfirmChoice.Confirmed, "  Kulanz  ");
        string? toast = null;
        var panel = Panel(api, appointment, dialogs, mayReturn: true, t => toast = t);

        await panel.LoadDepositAsync();
        Assert.True(panel.HasReturnableDeposit);
        Assert.Equal("20,00 € bezahlt (Bar), Beleg K1 Nr. 12, 01.03.2027. Beim Retournieren geht das Geld auf diesem Weg zurück: Storno-Beleg über die Kassa, der Grund steht darauf.", panel.DepositText);
        Assert.DoesNotContain(api.Calls, c => c.Route.StartsWith("POST", StringComparison.Ordinal));

        await panel.ReturnDepositCommand.ExecuteAsync(null);

        // Ohne Grund fragt der Dialog noch einmal, mit Grund geht die Anfrage hinaus.
        Assert.Equal(2, dialogs.Asked.Count);
        dialogs.AssertRules();
        Assert.Equal(("Anzahlung retournieren?", "Retournieren", "Grund (Pflicht)"), (dialogs.Asked[0].Title, dialogs.Asked[0].ConfirmText, dialogs.Asked[0].InputLabel));
        Assert.StartsWith("Bitte einen Grund angeben. ", dialogs.Asked[1].Message, StringComparison.Ordinal);
        Assert.Contains("20,00 €", dialogs.Asked[0].Message, StringComparison.Ordinal);
        var post = api.Calls.Single(c => c.Route == $"POST {DepositPath(appointment)}");
        Assert.Equal("Kulanz", JsonDocument.Parse(post.Body!).RootElement.GetProperty("reason").GetString());
        Assert.False(panel.HasReturnableDeposit);
        Assert.Equal("Anzahlung retourniert (Storno-Beleg K1 Nr. 13)", toast);
    }

    [Fact]
    public async Task Abbrechen_retourniert_nichts_und_ein_Fehler_des_Servers_steht_als_Meldung()
    {
        var appointment = TestData.Appointment(10, TestData.Anna);
        var api = new StubApi().On($"GET {DepositPath(appointment)}", OpenDeposit).On($"POST {DepositPath(appointment)}", null, HttpStatusCode.Conflict);

        var cancelled = Panel(api, appointment, new FakeDialogs(), mayReturn: true);
        await cancelled.LoadDepositAsync();
        await cancelled.ReturnDepositCommand.ExecuteAsync(null);
        Assert.DoesNotContain(api.Calls, c => c.Route.StartsWith("POST", StringComparison.Ordinal));
        Assert.True(cancelled.HasReturnableDeposit);

        (string Title, string? Text)? problem = null;
        var failing = Panel(api, appointment, new FakeDialogs().Answer(ConfirmChoice.Confirmed, "Kulanz"), mayReturn: true, report: (title, text) => problem = (title, text));
        await failing.LoadDepositAsync();
        await failing.ReturnDepositCommand.ExecuteAsync(null);

        Assert.Equal("Anzahlung nicht retourniert", problem!.Value.Title);
        Assert.True(failing.HasReturnableDeposit);
    }

    [Fact]
    public async Task Ohne_das_Recht_fragt_das_Panel_nicht_nach_der_Anzahlung_und_zeigt_keinen_Knopf()
    {
        var appointment = TestData.Appointment(10, TestData.Anna);
        var api = new StubApi().On($"GET {DepositPath(appointment)}", OpenDeposit with { CanReturn = false });
        var without = Panel(api, appointment, new FakeDialogs(), mayReturn: false);
        // Auch eine Antwort „darf nicht“ blendet den Knopf aus: der Server sagt es mit CanReturn.
        var denied = Panel(api, appointment, new FakeDialogs(), mayReturn: true);

        await without.LoadDepositAsync();
        Assert.DoesNotContain(api.Calls, c => c.Route == $"GET {DepositPath(appointment)}");
        await denied.LoadDepositAsync();

        Assert.False(without.HasReturnableDeposit);
        Assert.False(denied.HasReturnableDeposit);
    }

    [Fact]
    public async Task Nur_Inhaber_und_Manager_bekommen_im_Kalender_den_Abschnitt_Anzahlung()
    {
        var appointment = TestData.Appointment(10, TestData.Anna);
        StubApi Api() => ClosureTests.Api(TestData.Day(appointment)).On($"GET {DepositPath(appointment)}", OpenDeposit);

        var owner = await ClosureTests.OpenAsync(Api(), TestData.Owner());
        owner.OpenAppointment(appointment);
        var generalManager = await ClosureTests.OpenAsync(Api(), TestData.Owner(StaffRole.GeneralManager));
        generalManager.OpenAppointment(appointment);
        var location = await ClosureTests.OpenAsync(Api(), TestData.Manager());
        location.OpenAppointment(appointment);
        var reception = await ClosureTests.OpenAsync(Api(), TestData.Reception());
        reception.OpenAppointment(appointment);

        await ClosureTests.Until(() => owner.Selected is { HasReturnableDeposit: true } && generalManager.Selected is { HasReturnableDeposit: true });
        Assert.False(location.Selected!.MayReturnDeposit);
        Assert.False(location.Selected.HasReturnableDeposit);
        Assert.False(reception.Selected!.HasReturnableDeposit);
    }

    private static ReceiptDto SpaSampleReceipt() =>
        new(Guid.NewGuid(), Guid.NewGuid(), "K1", 13, ReceiptKind.Void, TestData.At(10), "Test-Spa", null, null, [], [], [], -20m, 0m, OpenDeposit.ReceiptId, null,
            "Anzahlung retourniert: Kulanz", null, false, []);
}
