using System.Net;
using AispaStudio.Contracts;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>Anamnese am Termin (Runde R05, Slice 127: Ä300; Slice 128: Ä304): Bogen lesen, Tablet-Link, Papierbogen, Foto mit Prüfen und Bestätigen; Antworten und Prüfen nur mit „Anamnese lesen“.</summary>
public sealed partial class ViewTests
{
    private static readonly string AnamnesisRoute = $"/api/customers/{SpaSample.Selected.CustomerId}/anamnesis";

    private static readonly Guid ScanId = Guid.Parse("00000000-0000-0000-0000-00000000a127");

    private static readonly string[] AnamnesisFlowButtons = ["TabletLinkButton", "PaperSheetButton", "UploadPhotoButton"];

    /// <summary>Oben und unten im Abschnitt Anamnese: nach dem Sprung müssen beide im Bild sein (T-022).</summary>
    private static readonly string[] AnamnesisJumpTargets = ["AnamnesisLanguageBox", "UploadPhotoButton"];

    private static readonly byte[] TinyPng = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M/wHwAEBgIApD5fRAAAAABJRU5ErkJggg==");

    private static readonly AnamnesisFormDefinition SampleForm = new("2", "de", "Bitte ausfüllen.", "Ich willige ein.",
    [
        new("allergies", AnamnesisQuestionKind.YesNo, "Haben Sie Allergien?", "Welche?", true),
        new("skin", AnamnesisQuestionKind.YesNo, "Haben Sie Hautprobleme?", "Welche?", true),
        new("other", AnamnesisQuestionKind.Text, "Sonstiges", null, false),
    ]);

    private static AnamnesisOverview NoSheet() => new(null, null, null, []);

    private static AnamnesisOverview SheetOnFile() => new(SpaSample.At(9), "Papier", SpaSample.At(9).AddYears(10), []);

    private static AnamnesisOverview OpenScan() => new(null, null, null, [new(ScanId, AnamnesisScanStatus.ReadyForReview, SpaSample.At(9))]);

    private static AnamnesisRecordDto SampleRecord() => new(Guid.NewGuid(), SpaSample.Selected.CustomerId, null, "2", "Papier", SpaSample.At(9), "Brigitte Müller",
        [new("allergies", true, "Nüsse"), new("skin", false, null), new("other", null, null)]);

    private static AnamnesisScanDto Scan(AnamnesisScanStatus status = AnamnesisScanStatus.ReadyForReview) => new(ScanId, SpaSample.Selected.CustomerId, SpaSample.Selected.Id, status,
        SpaSample.At(9), "Claude", status == AnamnesisScanStatus.Reading ? [] :
        [
            new("allergies", true, "Nüsse", false, null),
            new("skin", null, null, true, "Beide Kästchen angekreuzt"),
            new("other", null, null, false, null),
        ], "Brigitte Müller", false, true, null, null);

    private static StubApi ScanApi(AnamnesisScanStatus status = AnamnesisScanStatus.ReadyForReview) => Api()
        .On($"GET {AnamnesisRoute}/overview", NoSheet())
        .On($"POST {AnamnesisRoute}/scans", Scan(AnamnesisScanStatus.Reading), HttpStatusCode.Accepted)
        .On($"GET {AnamnesisRoute}/scans/{ScanId}", Scan(status))
        .OnBytes($"GET {AnamnesisRoute}/scans/{ScanId}/image", TinyPng)
        .On("GET /api/online/anamnesis/form?language=de", SampleForm);

    private static async Task<ViewModels.CalendarViewModel> OpenWithPhoto(Window window, ViewModels.MainViewModel main)
    {
        var calendar = await SignedInCalendar(main);
        calendar.Files = new FakeFilePicker { Bytes = TinyPng };
        calendar.OpenAppointment(SpaSample.Selected);
        await Pump(Settling);
        await ClickScrolled(window, "UploadPhotoButton");
        await Pump(Settling);
        return calendar;
    }

    private static List<string> DialogTexts(Window window) => VisibleTexts(Named<Border>(window, "DialogCard"));

    private static async Task AnswerSkinAndConfirmPaper(Window window)
    {
        var no = window.GetVisualDescendants().OfType<RadioButton>().First(r => r.Name == "ScanNo" && r.DataContext is ViewModels.ScanQuestionRow { Key: "skin" });
        no.BringIntoView();
        await Pump(Settling);
        ClickCenter(window, no);
        await Pump(Settling);
        await ClickScrolled(window, "ScanSignedBox");
        await Pump(Settling);
    }

    [Fact]
    public Task Inhaberin_klickt_Ansehen_und_liest_die_Antworten()
    {
        var stub = Api()
            .On($"GET {AnamnesisRoute}/overview", SheetOnFile())
            .On($"GET {AnamnesisRoute}", SampleRecord());
        return Render("hell", "termin-anamnese-antworten", async (window, main) =>
        {
            var calendar = await OpenSelected(main);
            Assert.Contains("Liegt vor: auf Papier am 19.10.2026, gültig bis 19.10.2036.", PanelTexts(window, "AnamnesisCard"));
            Assert.DoesNotContain(stub.Calls, c => c.Route == $"GET {AnamnesisRoute}");

            await ClickScrolled(window, "ShowRecordButton");

            var texts = PanelTexts(window, "AnamnesisCard");
            Assert.Contains("Allergien", texts);
            Assert.Contains("Ja: Nüsse", texts);
            Assert.Contains("Haut", texts);
            Assert.Contains("Nein", texts);
            return calendar;
        }, stub);
    }

    [Fact]
    public Task Therapeutin_ohne_Book_liest_die_Antworten_ohne_Knoepfe_fuer_die_Ablaeufe()
    {
        var stub = Api()
            .On("GET /api/auth/me", new CurrentUser("u4", "lena@demo.aispa.test", "Lena Huber", 1,
                new CurrentAccess(Guid.NewGuid(), "Day-Spa Wien", StaffRole.Therapist, true, []), null,
                [AccessRight.ViewCalendar, AccessRight.ChangeStatus, AccessRight.ViewHealthData]))
            .On($"GET {AnamnesisRoute}/overview", SheetOnFile() with { OpenScans = [new(ScanId, AnamnesisScanStatus.ReadyForReview, SpaSample.At(9))] })
            .On($"GET {AnamnesisRoute}", SampleRecord());
        return Render("hell", "termin-anamnese-therapeutin", async (window, main) =>
        {
            var calendar = await OpenSelected(main);

            await ClickScrolled(window, "ShowRecordButton");

            Assert.Contains("Ja: Nüsse", PanelTexts(window, "AnamnesisCard"));
            Assert.Contains("Papierbogen vom 19.10.2026 wartet auf Prüfung.", PanelTexts(window, "AnamnesisCard"));
            Assert.True(IsShown(window, "ReviewScanButton"));
            foreach (var button in AnamnesisFlowButtons)
            {
                Assert.False(IsShown(window, button), button);
            }

            return calendar;
        }, stub);
    }

    /// <summary>Ein Zugang, der buchen darf, aber nicht „Anamnese lesen“; seit Ä309 hat jede Rolle mit Kalender das Recht, die App blendet trotzdem nach der Rechteliste aus.</summary>
    private static CurrentUser UserWithoutHealthRight() => new("u5", "rita@demo.aispa.test", "Rita Berger", 1,
        new CurrentAccess(Guid.NewGuid(), "Day-Spa Wien", StaffRole.Reception, true, []), null,
        [AccessRight.ViewCalendar, AccessRight.Book, AccessRight.ChangeStatus]);

    [Fact]
    public Task Ohne_Recht_Anamnese_lesen_sieht_man_den_Stand_ohne_Ansehen_und_ohne_Pruefen()
    {
        var stub = Api()
            .On("GET /api/auth/me", UserWithoutHealthRight())
            .On($"GET {AnamnesisRoute}/overview", SheetOnFile() with { OpenScans = [new(ScanId, AnamnesisScanStatus.ReadyForReview, SpaSample.At(9))] });
        return Render("hell", "termin-anamnese-ohne-recht", async (window, main) =>
        {
            var calendar = await OpenSelected(main);

            var texts = PanelTexts(window, "AnamnesisCard");
            Assert.Contains("Liegt vor: auf Papier am 19.10.2026, gültig bis 19.10.2036.", texts);
            Assert.Contains("Papierbogen vom 19.10.2026 wartet auf Prüfung.", texts);
            Assert.False(IsShown(window, "ShowRecordButton"));
            Assert.False(IsShown(window, "ReviewScanButton"));
            Assert.True(IsShown(window, "UploadPhotoButton"));
            Assert.DoesNotContain(stub.Calls, c => c.Route == $"GET {AnamnesisRoute}");
            return calendar;
        }, stub);
    }

    [Fact]
    public Task Ohne_Recht_Anamnese_lesen_laedt_man_ein_Foto_hoch_ohne_Pruefdialog_und_sieht_den_Hinweis()
    {
        var stub = ScanApi().On("GET /api/auth/me", UserWithoutHealthRight());
        return Render("hell", "termin-anamnese-ohne-recht-foto", async (window, main) =>
        {
            var calendar = await OpenWithPhoto(window, main);

            Assert.Contains(stub.Calls, c => c.Route == $"POST {AnamnesisRoute}/scans");
            Assert.Null(main.DialogHost.Current);
            Assert.DoesNotContain(stub.Calls, c => c.Route.StartsWith($"GET {AnamnesisRoute}/scans/", StringComparison.Ordinal));
            Assert.Equal("Foto hochgeladen. Prüfen kann, wer Anamnesen lesen darf", calendar.Toast);
            return calendar;
        }, stub);
    }

    [Fact]
    public Task Auftrag_Bogen_senden_zeigt_den_Abschnitt_Anamnese_mit_den_drei_Wegen_im_Bild()
    {
        var stub = Api().On($"GET {AnamnesisRoute}/overview", NoSheet());
        return Render("hell", "termin-anamnese-auftrag", async (window, main) =>
        {
            var calendar = await SignedInCalendar(main);
            await Pump(Settling);
            var shell = Assert.IsType<ViewModels.ShellViewModel>(main.Content);
            shell.SelectedPage = shell.Pages.First(p => p.Page != calendar);
            await Pump(Settling);
            var selected = SpaSample.Selected;

            // Wie aus „KI und Hilfe“ auf einer anderen Seite: die Shell wechselt zum Kalender und öffnet gleich danach den Termin.
            await shell.OpenAssistantOrderAsync(new AssistantSuggestion(AssistantSuggestionKind.SendQuestionnaire, "Termin mit Anamnese öffnen", SpaSample.LocationId,
                null, null, selected.CustomerId, selected.Id, selected.Start));
            await Pump(Settling);

            Assert.Equal("AnamnesisLanguageBox", (TopLevel.GetTopLevel(window)!.FocusManager!.GetFocusedElement() as Control)?.Name);
            var scroll = window.GetVisualDescendants().OfType<Border>().First(b => b.Name == "SidePanel")
                .GetVisualDescendants().OfType<ScrollViewer>().First(s => s.IsEffectivelyVisible);
            foreach (var name in AnamnesisJumpTargets)
            {
                var control = window.GetVisualDescendants().OfType<Control>().First(c => c.Name == name);
                var top = control.TranslatePoint(new Point(0, 0), scroll)!.Value.Y;
                Assert.True(top >= 0 && top + control.Bounds.Height <= scroll.Viewport.Height, $"{name} liegt bei {top} außerhalb von {scroll.Viewport.Height}");
            }

            return calendar;
        }, stub);
    }

    [Fact]
    public Task Termin_ohne_Bogen_Klick_auf_Tablet_zeigt_den_Link_der_API()
    {
        const string Url = "https://spa.example.test/#/anamnese/abc?lang=de";
        var stub = Api()
            .On($"GET {AnamnesisRoute}/overview", NoSheet())
            .On($"POST {AnamnesisRoute}/tablet", new AnamnesisTabletLink(Url, SpaSample.At(11)));
        return Render("hell", "termin-anamnese-tablet", async (window, main) =>
        {
            var calendar = await OpenSelected(main);
            Assert.Contains("Fehlt noch.", PanelTexts(window, "AnamnesisCard"));

            await ClickScrolled(window, "TabletLinkButton");

            var call = Assert.Single(stub.Calls, c => c.Route == $"POST {AnamnesisRoute}/tablet");
            Assert.Contains($"\"appointmentId\":\"{SpaSample.Selected.Id}\"", call.Body, StringComparison.Ordinal);
            Assert.Contains("\"language\":\"de\"", call.Body, StringComparison.Ordinal);
            var texts = PanelTexts(window, "AnamnesisCard");
            Assert.Contains(Url, texts);
            Assert.Contains("Gültig bis 11:00 Uhr, nur einmal.", texts);
            Assert.NotNull(Named<Image>(window, "TabletQr").Source);
            return calendar;
        }, stub);
    }

    [Fact]
    public Task Klick_auf_Papierbogen_oeffnet_den_Bogen_aus_der_API_Vorgabe()
    {
        var stub = Api()
            .On($"GET {AnamnesisRoute}/overview", NoSheet())
            .On($"POST {AnamnesisRoute}/paper", new AnamnesisPaperSheet(SampleForm, "Brigitte Müller", "Wien", "Massage", SpaSample.At(10), "Europe/Vienna",
                "https://spa.example.test/#/anamnese/xyz", SpaSample.At(10).AddDays(1)));
        return Render("hell", "termin-anamnese-papier", async (window, main) =>
        {
            var calendar = await SignedInCalendar(main);
            var actions = new RecordingActions();
            calendar.ContactActions = actions;
            calendar.OpenAppointment(SpaSample.Selected);
            await Pump(Settling);

            await ClickScrolled(window, "PaperSheetButton");

            Assert.Single(stub.Calls, c => c.Route == $"POST {AnamnesisRoute}/paper");
            var file = Assert.Single(actions.Launched);
            Assert.True(file.IsFile);
            var html = await File.ReadAllTextAsync(file.LocalPath);
            File.Delete(file.LocalPath);
            Assert.Contains("Haben Sie Allergien?", html, StringComparison.Ordinal);
            Assert.Contains("Brigitte Müller", html, StringComparison.Ordinal);
            Assert.Contains("window.print()", html, StringComparison.Ordinal);
            return calendar;
        }, stub);
    }

    [Fact]
    public Task Foto_mit_Vorschlag_Schliessen_ohne_Bestaetigen_speichert_nichts()
    {
        var stub = ScanApi();
        return Render("hell", "termin-anamnese-pruefen", async (window, main) =>
        {
            var calendar = await OpenWithPhoto(window, main);

            Assert.Contains(stub.Calls, c => c.Route == $"POST {AnamnesisRoute}/scans");
            Assert.True(IsShown(window, "DialogCard"));
            var texts = DialogTexts(window);
            Assert.Contains("Haben Sie Allergien?", texts);
            Assert.Contains("Unsicher: Beide Kästchen angekreuzt", texts);
            Assert.True(IsShown(window, "ScanAiPill"));
            Assert.NotNull(Named<Image>(window, "ScanPhoto").Source);

            await ClickVisible(window, "CancelButton");
            await Pump(Settling);

            Assert.Null(main.DialogHost.Current);
            Assert.DoesNotContain(stub.Calls, c => c.Route.Contains("/confirm", StringComparison.Ordinal));
            Assert.DoesNotContain(stub.Calls, c => c.Route.StartsWith("DELETE", StringComparison.Ordinal));
            return calendar;
        }, stub);
    }

    [Fact]
    public Task Uebernehmen_bleibt_gesperrt_bis_Pflichtangaben_stehen() => Render("hell", "termin-anamnese-gesperrt", async (window, main) =>
    {
        var calendar = await OpenWithPhoto(window, main);
        var accept = Named<Button>(window, "ConfirmButton");

        Assert.False(accept.IsEffectivelyEnabled);
        Assert.Contains("Noch ohne Ja/Nein: 1 Frage(n).", DialogTexts(window));
        Assert.Contains("Noch gelb: 1 Feld(er).", DialogTexts(window));

        await AnswerSkinAndConfirmPaper(window);

        Assert.True(accept.IsEffectivelyEnabled);
        Assert.DoesNotContain("Unsicher: Beide Kästchen angekreuzt", DialogTexts(window));
        return calendar;
    }, ScanApi());

    [Fact]
    public Task Geprueft_und_bestaetigt_zeigt_der_Termin_liegt_vor()
    {
        var confirmed = false;
        var stub = ScanApi()
            .OnJson($"GET {AnamnesisRoute}/overview", _ => confirmed ? SheetOnFile() : NoSheet())
            .OnJson($"POST {AnamnesisRoute}/scans/{ScanId}/confirm", _ =>
            {
                confirmed = true;
                return SampleRecord();
            });
        return Render("hell", "termin-anamnese-bestaetigt", async (window, main) =>
        {
            var calendar = await OpenWithPhoto(window, main);
            await AnswerSkinAndConfirmPaper(window);

            await ClickVisible(window, "ConfirmButton");
            await Pump(Settling);

            var call = Assert.Single(stub.Calls, c => c.Route == $"POST {AnamnesisRoute}/scans/{ScanId}/confirm");
            Assert.Contains("\"key\":\"skin\",\"yes\":false", call.Body, StringComparison.Ordinal);
            Assert.Contains("\"consentOnPaper\":true", call.Body, StringComparison.Ordinal);
            Assert.Contains("\"signedOnPaper\":true", call.Body, StringComparison.Ordinal);
            Assert.Null(main.DialogHost.Current);
            Assert.Contains("Liegt vor: auf Papier am 19.10.2026, gültig bis 19.10.2036.", PanelTexts(window, "AnamnesisCard"));
            Assert.Equal("Anamnese vom Papierbogen übernommen", calendar.Toast);
            return calendar;
        }, stub);
    }

    [Fact]
    public Task Foto_verwerfen_ruft_DELETE()
    {
        var stub = ScanApi().On($"DELETE {AnamnesisRoute}/scans/{ScanId}", null, HttpStatusCode.NoContent);
        return Render("hell", "termin-anamnese-verwerfen", async (window, main) =>
        {
            var calendar = await OpenWithPhoto(window, main);

            await ClickVisible(window, "DiscardScanButton");
            await Pump(Settling);

            Assert.Contains(stub.Calls, c => c.Route == $"DELETE {AnamnesisRoute}/scans/{ScanId}");
            Assert.Null(main.DialogHost.Current);
            Assert.Equal("Foto verworfen", calendar.Toast);
            return calendar;
        }, stub);
    }

    [Fact]
    public Task Offener_Bogen_Klick_auf_Pruefen_oeffnet_den_Dialog()
    {
        var stub = ScanApi().On($"GET {AnamnesisRoute}/overview", OpenScan());
        return Render("hell", "termin-anamnese-offen", async (window, main) =>
        {
            var calendar = await OpenSelected(main);
            Assert.Contains("Papierbogen vom 19.10.2026 wartet auf Prüfung.", PanelTexts(window, "AnamnesisCard"));

            await ClickScrolled(window, "ReviewScanButton");
            await Pump(Settling);

            Assert.True(IsShown(window, "DialogCard"));
            Assert.Contains("Haben Sie Allergien?", DialogTexts(window));
            return calendar;
        }, stub);
    }

    [Fact]
    public Task Abgelehntes_Foto_meldet_den_Text_und_oeffnet_keinen_Dialog()
    {
        var stub = ScanApi().On($"POST {AnamnesisRoute}/scans", null, HttpStatusCode.BadRequest);
        return Render("hell", "termin-anamnese-abgelehnt", async (window, main) =>
        {
            var calendar = await OpenWithPhoto(window, main);

            Assert.Null(main.DialogHost.Current);
            Assert.Contains("Foto nicht angenommen. Erlaubt ist ein Foto als JPEG, PNG oder WebP bis 5 MB.", PanelTexts(window, "AnamnesisCard"));
            return calendar;
        }, stub);
    }

    [Fact]
    public Task Nicht_lesbares_Foto_meldet_den_Fehler_und_stuerzt_nicht_ab() => Render("hell", "termin-anamnese-lesefehler", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        calendar.Files = new FakeFilePicker { BytesFailure = new IOException("gesperrt") };
        calendar.OpenAppointment(SpaSample.Selected);
        await Pump(Settling);

        await ClickScrolled(window, "UploadPhotoButton");
        await Pump(Settling);

        Assert.Null(main.DialogHost.Current);
        Assert.Contains("Foto konnte nicht hochgeladen werden.", PanelTexts(window, "AnamnesisCard"));
        return calendar;
    }, ScanApi());

    [Fact]
    public Task Esc_waehrend_des_Uebernehmens_laesst_den_Dialog_offen()
    {
        Window? open = null;
        var canCancelWhileSaving = true;
        var stub = ScanApi().OnJson($"POST {AnamnesisRoute}/scans/{ScanId}/confirm", _ =>
        {
            open!.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            canCancelWhileSaving = Named<Button>(open!, "CancelButton").IsEffectivelyEnabled;
            return SampleRecord();
        });
        return Render("hell", "termin-anamnese-speichert", async (window, main) =>
        {
            open = window;
            var calendar = await OpenWithPhoto(window, main);
            await AnswerSkinAndConfirmPaper(window);

            await ClickVisible(window, "ConfirmButton");
            await Pump(Settling);

            Assert.False(canCancelWhileSaving);
            Assert.Equal("Anamnese vom Papierbogen übernommen", calendar.Toast);
            return calendar;
        }, stub);
    }

    [Fact]
    public Task Enter_im_Namensfeld_bleibt_im_Dialog() => Render("hell", "termin-anamnese-enter", async (window, main) =>
    {
        var calendar = await OpenWithPhoto(window, main);
        var enter = new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter };

        Named<TextBox>(window, "ScanSignature").RaiseEvent(enter);

        Assert.True(enter.Handled);
        return calendar;
    }, ScanApi());

    [Fact]
    public Task Foto_fehlt_nennt_der_Dialog_den_Fehler()
    {
        var stub = ScanApi().On($"GET {AnamnesisRoute}/scans/{ScanId}/image", null, HttpStatusCode.NotFound);
        return Render("hell", "termin-anamnese-ohne-foto", async (window, main) =>
        {
            var calendar = await OpenWithPhoto(window, main);

            Assert.Contains("Foto nicht geladen. Den Bogen schließen und erneut öffnen.", DialogTexts(window));
            return calendar;
        }, stub);
    }

    [Fact]
    public Task Fragen_und_Ansehen_nennen_dem_Screenreader_den_Zusammenhang()
    {
        var stub = ScanApi().On($"GET {AnamnesisRoute}/overview", SheetOnFile());
        return Render("hell", "termin-anamnese-namen", async (window, main) =>
        {
            var calendar = await OpenWithPhoto(window, main);

            var no = window.GetVisualDescendants().OfType<RadioButton>().First(r => r.Name == "ScanNo" && r.DataContext is ViewModels.ScanQuestionRow { Key: "skin" });
            Assert.EndsWith(": Nein", AutomationProperties.GetName(no), StringComparison.Ordinal);
            Assert.True(AutomationProperties.GetName(no)!.Length > ": Nein".Length);
            Assert.Equal("Antworten ansehen", AutomationProperties.GetName(Named<Button>(window, "ShowRecordButton")));
            return calendar;
        }, stub);
    }

    [Fact]
    public Task Waehrend_die_KI_liest_fragt_der_Dialog_alle_zwei_Sekunden_nach()
    {
        var clock = new StoppedTime();
        var reading = true;
        var stub = ScanApi().OnJson($"GET {AnamnesisRoute}/scans/{ScanId}", _ => Scan(reading ? AnamnesisScanStatus.Reading : AnamnesisScanStatus.ReadyForReview));
        return Render("hell", "termin-anamnese-liest", async (window, main) =>
        {
            var calendar = await OpenWithPhoto(window, main);
            Assert.Contains("Die KI liest den Bogen …", DialogTexts(window));

            reading = false;
            clock.Advance(TimeSpan.FromSeconds(2));
            await Pump(Settling);

            Assert.Contains("Haben Sie Allergien?", DialogTexts(window));
            return calendar;
        }, stub, time: clock);
    }
}
