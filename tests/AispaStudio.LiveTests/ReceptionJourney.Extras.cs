using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Text.Json.Nodes;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.WindowsAPI;

namespace AispaStudio.LiveTests;

/// <summary>
/// Zusätzliche Themen der Sichtprüfung (Schlüssel <c>extra-…</c>): Fälle, die der Katalog nicht spielt, mit Standortregel über die API,
/// Konto der Standortleitung, Fehlerfällen vom Server und der Verbindung, die wegfällt. Was fehlt, steht als „BEFUND: …“ im Bericht,
/// der Lauf geht weiter; die Texte der Dialoge und Meldungen stehen wörtlich im Bericht („DIALOG …“, „KNÖPFE …“, „OFFLINE …“).
/// </summary>
internal static partial class ReceptionJourney
{
    private static readonly string[] AnswerIds = ["ConfirmButton", "AlternativeButton", "CancelButton"];
    private static readonly string[] DialogTextIds = ["DialogTitle", "DialogMessage"];
    private static readonly string[] LockedWhenTomorrow = ["Nicht erschienen", "Erledigt"];
    private static readonly string[] StatusesBeforeTheDay = ["Completed", "NoShow"];
    private static readonly string[] TeamNames = ["Elif Demir", "Jana Wolf", "Lena Huber", "Malee Chaiyasit", "Sophie Gruber"];

    private static LiveTopic[] ExtraTopics =>
    [
        new("extra-cancel-late", "Stornieren innerhalb der Stornofrist", "Standortregel 24 Stunden und 50 %: Hinweiszeile und Vorbelegung im Dialog, als Rezeption und als Standortleitung", CancelLate, TermNeeds,
            Leaves: "Setzt die Standortregel für die Dauer des Themas und stellt sie danach wieder her; legt sonst nichts an."),
        new("extra-sick-pause-block", "Krank melden, Pause und Sperrzeit löschen", "Als Standortleitung: „krank melden?“ abbrechen, Pause und Sperrzeit anlegen, Löschfrage lesen, löschen", SickPauseBlock,
            Leaves: "Legt eine Pause und eine Sperrzeit an und löscht sie wieder."),
        new("extra-reschedule-yesterday", "Verschieben auf gestern", "Panel „Verschieben …“ mit gestrigem Tag; Server-Antwort für das Verschieben in die Vergangenheit", RescheduleYesterday, TermNeeds,
            Leaves: "Legt nichts an."),
        new("extra-checkin-tomorrow", "Einchecken bei einem Termin von morgen", "Termin für morgen buchen: kein „Einchecken“, Hinweise lesen; Server-Antwort für das Einchecken morgen", CheckInTomorrow,
            Leaves: "Legt einen Termin morgen an (Maniküre mit Lack, Anna Bauer)."),
        new("extra-pos-checkin-prompt", "Kassa: Einchecken und kassieren?", "Zeile eines nicht eingecheckten Termins anklicken: Rückfrage, Abbrechen, dann bestätigen und die Rechnung verwerfen", PosCheckInPrompt,
            Leaves: "Checkt einen Demo-Termin von heute ein; die Rechnung wird verworfen."),
        new("extra-withdrawal-tab", "Kassenlade: Entnahme nur für die Leitung", "Rezeption sieht nur „Einlage“, die Standortleitung zusätzlich „Entnahme“", WithdrawalTab,
            Leaves: "Legt nichts an."),
        new("extra-no-contact-pill-and-key-v", "Kundin ohne Kontakt und Taste V", "Pille „Keine Kontaktmöglichkeit“ im Buchungsbereich; Taste V im Kalender", NoContactAndKeyV, TermNeeds,
            Leaves: "Legt eine Kundin ohne Telefon und E-Mail an."),
        new("extra-api-errors", "Fehlerfälle am Server (400, 409)", "Buchung zur selben Zeit und in der Vergangenheit: Statuscodes und Texte im Bericht", ApiErrors, TermNeeds,
            Leaves: "Legt nichts an (die Buchungen werden abgelehnt)."),
        new("extra-offline", "Ohne Verbindung", "API-Container anhalten: Kassieren, Einchecken, Buchen versuchen, Meldungen festhalten, API wieder starten", Offline,
            Leaves: "Hält den API-Container des Projekts aispa-sicht an und startet ihn wieder; vorgemerkte Änderungen werden danach abgeglichen."),
    ];

    // ---------- Hilfen ----------

    private static void Note(LiveSession s, string text) => s.Report.AddEvent(text);

    private static void Check(LiveSession s, bool ok, string befund)
    {
        if (!ok)
        {
            s.Report.AddEvent($"BEFUND: {befund}");
        }
    }

    /// <summary>Die Antwort „Abbrechen“ (oder „Weiter bearbeiten“) einer Rückfrage; die AutomationId „CancelButton“ steht nicht immer im Baum.</summary>
    private static readonly Func<AutomationElement, bool> DialogCancel =
        Class("Button", e => e.Properties.AutomationId.ValueOrDefault == "CancelButton"
                             || e.Properties.Name.ValueOrDefault is "Abbrechen" or "Weiter bearbeiten");

    private static string? TextOfId(LiveSession s, string id) =>
        s.TryFind(id, TimeSpan.FromMilliseconds(600)) is { } element ? LiveSession.TextOf(element) : null;

    /// <summary>Liest die Rückfrage (Titel, Text, Hinweis, Feld, Antworten, Breite) in den Bericht und liefert die Texte zum Prüfen.</summary>
    private static (string? Title, string? Message, string? Notice, string? Input, string Answers) ReadDialog(LiveSession s, string label)
    {
        var title = TextOfId(s, "DialogTitle");
        var message = TextOfId(s, "DialogMessage");
        var notice = s.TryFind("DialogNoticeText", TimeSpan.FromMilliseconds(500)) is { } n ? LiveSession.TextOf(n) : null;
        var input = s.TryFind("DialogInput", TimeSpan.FromMilliseconds(500)) is { } i ? LiveSession.TextOf(i) : null;
        var answers = string.Join(" | ", AnswerIds
            .Select(id => s.TryFind(id, TimeSpan.FromMilliseconds(400)) is { } b ? LiveSession.TextOf(b) : null).Where(text => text is not null)
            .Append(s.TryFind(DialogCancel, TimeSpan.FromMilliseconds(400)) is { } cancelButton ? LiveSession.TextOf(cancelButton) : null)
            .Where(text => text is not null).Distinct());
        var card = s.TryFind("DialogCard", TimeSpan.FromMilliseconds(300));
        var width = card is not null
            ? $"{card.BoundingRectangle.Width} px (Dialog), Fenster {s.Window.BoundingRectangle.Width} px"
            : s.TryFind("DialogTitle", TimeSpan.FromMilliseconds(300)) is { } titleElement
                ? $"{titleElement.BoundingRectangle.Width} px (Titel), Fenster {s.Window.BoundingRectangle.Width} px"
                : "?";
        Note(s, $"DIALOG {label}: Titel „{title}“; Text „{message}“; Hinweis „{notice}“; Feld „{input}“; Antworten: {answers}; Breite {width}.");
        return (title, message, notice, input, answers);
    }

    /// <summary>Text der Meldung unten im Kalender (Toast), wenn eine da ist; sonst null.</summary>
    private static string? ReadToast(LiveSession s, TimeSpan? wait = null) =>
        s.TryFind(Class("TextBlock", e => !string.IsNullOrEmpty(e.Properties.HelpText.ValueOrDefault)
                                          && e.BoundingRectangle.Y > s.Window.BoundingRectangle.Bottom - 260), wait ?? TimeSpan.FromSeconds(3)) is { } toast
            ? toast.Properties.HelpText.ValueOrDefault
            : null;

    /// <summary>Alle sichtbaren Knöpfe mit Zustand und Hinweis (bei gesperrten der Grund) in den Bericht.</summary>
    private static void ReportButtons(LiveSession s, string label, Func<string, bool>? filter = null)
    {
        var names = s.VisibleTexts(Class("Button", _ => true)).Where(name => filter?.Invoke(name) ?? true).ToList();
        var lines = new List<string>();
        foreach (var name in names)
        {
            if (s.TryFind(Button(name), TimeSpan.FromMilliseconds(200)) is not { } button)
            {
                continue;
            }

            var help = button.Properties.HelpText.ValueOrDefault;
            lines.Add($"{name} [{(button.Properties.IsEnabled.ValueOrDefault ? "frei" : "gesperrt")}]{(string.IsNullOrEmpty(help) ? "" : $" Hinweis: {help}")}");
        }

        Note(s, $"KNÖPFE {label}: {string.Join("; ", lines)}");
    }

    /// <summary>Die Themen davor lassen den Kalender auf „Heute“ zurück (dort liegt der Termin des Laufs); nach einem Kontowechsel stellt dieser Schritt das wieder her.</summary>
    private static void TodayStep(LiveSession s) =>
        s.Step("Kalender auf „Heute“ stellen", () =>
        {
            s.Click(Navigation(s, "Kalender"));
            s.Click(Button("Heute"), "Knopf „Heute“");
        });

    private static void SwitchAccount(LiveSession s, string person, string role) =>
        s.Step($"Abmelden und als {person} ({role}) anmelden", () =>
        {
            s.Click(s.Find(NameStarts("Konto "), "Kontoknopf"));
            s.Click("Abmelden");
            s.Expect("Als Testkonto anmelden");
            s.Click("Testkonto");
            s.Click(person);
            s.Click("Als Testkonto anmelden");
            s.Expect(NameStarts($"Konto {person}, {role}"), $"Kontoknopf „Konto {person}, {role} …“");
        });

    private static (Guid Id, TimeZoneInfo Zone) ViennaOf(LiveApi api)
    {
        var (id, zone) = api.ViennaLocation();
        return (id, TimeZoneInfo.FindSystemTimeZoneById(zone));
    }

    private static DateOnly TodayIn(TimeZoneInfo zone) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, zone).DateTime);

    private static string LocalBegin(JsonNode appointment, TimeZoneInfo zone) =>
        TimeZoneInfo.ConvertTime(DateTimeOffset.Parse(appointment["start"]!.ToString(), CultureInfo.InvariantCulture), zone).ToString("HH:mm", CultureInfo.InvariantCulture);

    /// <summary>Der Termin des Laufs in der Antwort des Servers (Kundin und Beginn „HH:mm“ in Ortszeit).</summary>
    private static JsonNode ServerAppointment(LiveApi api, Guid location, TimeZoneInfo zone, DateOnly day, string customer, string begin) =>
        api.Appointments(location, day).First(a =>
            a!["customerName"]!.ToString().Contains(customer, StringComparison.Ordinal)
            && LocalBegin(a, zone) == begin
            && a["status"]!.ToString() != "Cancelled")!;

    /// <summary>Setzt Stornofrist 24 Stunden und 50 % Ausfallgebühr für Wien (als Standortleitung über die API) und merkt sich den alten Stand.</summary>
    private static void SetLateCancelRules(LiveSession s, LiveRunContext context)
    {
        using var api = new LiveApi(s.ApiUrl, "leitung.wien");
        var (location, _) = api.ViennaLocation();
        var path = $"api/locations/{location}/online-rules";
        context.SavedRules ??= api.Get(path);
        var rules = (JsonObject)context.SavedRules.DeepClone();
        rules["cancellationDeadlineHours"] = 24;
        rules["noShowFeePercent"] = 50;
        var put = api.Send(HttpMethod.Put, path, rules);
        Note(s, $"Standortregel gesetzt (PUT {path}): {put.Describe()}. Vorher: {context.SavedRules.ToJsonString()}");
        if (!put.IsSuccess)
        {
            throw new InvalidOperationException($"Die Standortregel ließ sich nicht setzen: {put.Describe()}");
        }
    }

    private static void RestoreRules(LiveSession s, LiveRunContext context)
    {
        if (context.SavedRules is not { } saved)
        {
            return;
        }

        using var api = new LiveApi(s.ApiUrl, "leitung.wien");
        var (location, _) = api.ViennaLocation();
        var path = $"api/locations/{location}/online-rules";
        var wasDefault = saved["isDefault"]?.GetValue<bool>() == true;
        var answer = wasDefault ? api.Send(HttpMethod.Delete, path) : api.Send(HttpMethod.Put, path, saved);
        Note(s, $"Standortregel zurückgesetzt ({(wasDefault ? "DELETE, es waren die Vorgaben" : "PUT, eigener alter Stand")}): {answer.Describe()}");
        context.SavedRules = null;
    }

    /// <summary>Das Datumsfeld im Seitenpanel („Tag“ beim Verschieben, „Datum“ beim Buchen).</summary>
    private static AutomationElement DateField(LiveSession s) =>
        s.Find(e => e.Properties.ClassName.ValueOrDefault == "DateField", "Datumsfeld");

    /// <summary>Stellt den Kalender auf einen Tag davor, an dem es Karten gibt (bis zu sieben Tage zurück).</summary>
    private static int GoBackToDayWithCards(LiveSession s)
    {
        s.Click(Button("Heute"), "Knopf „Heute“");
        for (var back = 1; back <= 7; back++)
        {
            s.Click("Vortag");
            Thread.Sleep(900);
            if (s.TryFind(Class("AppointmentCard", _ => true), TimeSpan.FromMilliseconds(1500)) is not null)
            {
                Note(s, $"Vortag mit Terminen: {ShownDate(s)} ({back} {(back == 1 ? "Tag" : "Tage")} zurück).");
                return back;
            }
        }

        throw new InvalidOperationException("In den letzten sieben Tagen gab es keinen Tag mit Terminen.");
    }

    /// <summary>
    /// Grundzustand nach einem roten Thema: offene Rückfrage schließen (Verwerfen, Abbrechen), Panel mit Esc schließen, wieder als Nina Kovač
    /// angemeldet sein, Kalender auf „Heute“.
    /// </summary>
    private static void ResetToBaseline(LiveSession s, LiveRunContext context)
    {
        for (var round = 0; round < 3; round++)
        {
            if (s.TryFind(Button("Verwerfen"), TimeSpan.FromMilliseconds(400)) is { } discard)
            {
                s.Click(discard);
            }
            else if (s.TryFind(DialogCancel, TimeSpan.FromMilliseconds(400)) is { } cancel)
            {
                s.Click(cancel);
            }
            else
            {
                LiveSession.Press(VirtualKeyShort.ESCAPE);
                Thread.Sleep(400);
            }
        }

        if (s.TryFind(Button("Verwerfen"), TimeSpan.FromMilliseconds(400)) is { } lastDiscard)
        {
            s.Click(lastDiscard);
        }

        EnsureSignedInAs(s, "Nina Kovač", "Rezeption");
        s.Click(Navigation(s, "Kalender"));
        s.Click(Button("Heute"), "Knopf „Heute“");
        Note(s, "Grundzustand: Rückfrage und Panel geschlossen, Nina Kovač angemeldet, Kalender auf „Heute“.");
        RepairBookedAppointment(s, context);
    }

    /// <summary>
    /// Ein rotes Thema lässt den Termin des Laufs oft eingecheckt zurück (der Schritt „Einchecken zurücknehmen“ lief nicht mehr); dann scheitern alle
    /// Themen danach an „Karte … Gebucht“. Der Tester nimmt das Einchecken über die API zurück und schreibt es in den Bericht.
    /// </summary>
    private static void RepairBookedAppointment(LiveSession s, LiveRunContext context)
    {
        if (context.Appointment is not { DaysAhead: 0 } booked)
        {
            return;
        }

        try
        {
            using var api = new LiveApi(s.ApiUrl, "leitung.wien");
            var (location, zone) = ViennaOf(api);
            var appointment = ServerAppointment(api, location, zone, TodayIn(zone), booked.Customer, booked.Begin);
            if (appointment["status"]!.ToString() == "CheckedIn")
            {
                var answer = api.Send(HttpMethod.Post, $"api/appointments/{appointment["id"]}/status", new { status = "Booked" });
                Note(s, $"Grundzustand: Termin des Laufs war eingecheckt, Einchecken über die API zurückgenommen: {answer.Status}.");
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException)
        {
            Note(s, $"Grundzustand: Termin des Laufs nicht geprüft: {ex.Message}");
        }
    }

    /// <summary>Meldet um, wenn ein anderes Konto angemeldet ist; ohne Bild und Schritt, damit es auch nach einem Fehler geht.</summary>
    private static void EnsureSignedInAs(LiveSession s, string person, string role)
    {
        var account = s.Find(NameStarts("Konto "), "Kontoknopf");
        if (account.Properties.Name.ValueOrDefault?.StartsWith($"Konto {person}, {role}", StringComparison.Ordinal) == true)
        {
            return;
        }

        s.Click(account);
        s.Click("Abmelden");
        s.Expect("Als Testkonto anmelden");
        s.Click("Testkonto");
        s.Click(person);
        s.Click("Als Testkonto anmelden");
        s.Expect(NameStarts($"Konto {person}, {role}"), $"Kontoknopf „Konto {person}, {role} …“");
    }

    // ---------- B1: Stornieren innerhalb der Stornofrist ----------

    private static void CancelLate(LiveSession s, LiveRunContext context)
    {
        var booked = context.Appointment!;
        RequireToday(booked);
        s.Step("Standortregel setzen: Stornofrist 24 Stunden, 50 % Ausfallgebühr (API, als Standortleitung)", () => SetLateCancelRules(s, context));
        try
        {
            ReadLateCancelDialog(s, booked, "Rezeption", mayCharge: false);
            SwitchAccount(s, "Markus Steiner", "Standortleitung");
            ReadLateCancelDialog(s, booked, "Standortleitung", mayCharge: true);
            SwitchAccount(s, "Nina Kovač", "Rezeption");
        }
        finally
        {
            RestoreRules(s, context);
        }
    }

    private static void ReadLateCancelDialog(LiveSession s, BookedAppointment booked, string who, bool mayCharge)
    {
        OpenCalendar(s);
        s.Step($"{who}: Kalender auf den Tag des Termins", () => ShowDay(s, booked, startFromToday: true));
        OpenPanel(s, booked);
        BeginCancel(s);
        s.Step($"{who}: Texte der Rückfrage „Termin stornieren?“ lesen", () =>
        {
            var (title, message, notice, input, _) = ReadDialog(s, $"Stornieren als {who}");
            Check(s, title == "Termin stornieren?", $"{who}: Titel ist „{title}“, erwartet „Termin stornieren?“.");
            Check(s, notice?.StartsWith("Absage innerhalb der Stornofrist (bis ", StringComparison.Ordinal) == true,
                $"{who}: Die Hinweiszeile „Absage innerhalb der Stornofrist (bis …)“ fehlt (Hinweis: „{notice}“).");
            Check(s, notice?.Contains("Laut Regel fallen 50 % Ausfallgebühr an", StringComparison.Ordinal) == true,
                $"{who}: „Laut Regel fallen 50 % Ausfallgebühr an …“ fehlt im Hinweis.");
            var tail = mayCharge ? "Belasten unter „Ausfallgebühr belasten …“ nach dem Stornieren" : "belasten kann die Standortleitung";
            Check(s, notice?.Contains(tail, StringComparison.Ordinal) == true, $"{who}: „{tail}“ fehlt im Hinweis.");
            Check(s, input == "Kurzfristig an der Rezeption storniert", $"{who}: Der Grund ist „{input}“, erwartet „Kurzfristig an der Rezeption storniert“.");
            Note(s, $"Text des Dialogs ({who}): {message}");
        });
        s.Step($"{who}: Esc schließt die Rückfrage, dann das Panel", () =>
        {
            LiveSession.Press(VirtualKeyShort.ESCAPE);
            s.ExpectMissing("ConfirmButton");
            ClosePanelWithEscape(s);
        });
    }

    // ---------- B2: krank melden, Pause, Sperrzeit ----------

    private static AutomationElement ColumnHeader(LiveSession s) =>
        s.Find(Class("TextBlock", e => e.Properties.Name.ValueOrDefault is { } name && TeamNames.Contains(name)
                                       && e.BoundingRectangle.Y < s.Window.BoundingRectangle.Y + 300),
            "Spaltenkopf einer Mitarbeiterin");

    private static void SickPauseBlock(LiveSession s, LiveRunContext context)
    {
        SwitchAccount(s, "Markus Steiner", "Standortleitung");
        OpenCalendar(s);
        s.Step("Kalender auf „Heute“", () => s.Click(Button("Heute"), "Knopf „Heute“"));
        s.Step("Rechtsklick auf den Spaltenkopf einer Mitarbeiterin: Menü mit „… krank melden“", () =>
        {
            var header = ColumnHeader(s);
            Note(s, $"Spaltenkopf: {header.Properties.Name.ValueOrDefault}");
            s.RightClick(header);
            Thread.Sleep(500);
            Note(s, $"MENÜ Spaltenkopf: {string.Join(" | ", s.VisibleMenuItems())}");
            s.Expect(Class("MenuItem", NameContains("krank melden")), "Menüeintrag „… krank melden“");
        });
        s.Step("„… krank melden“: Rückfrage „… krank melden?“", () =>
        {
            s.Click(s.Find(Class("MenuItem", NameContains("krank melden")), "Menüeintrag „… krank melden“"));
            s.Expect("ConfirmButton");
            var (title, _, _, _, _) = ReadDialog(s, "krank melden");
            Check(s, title?.EndsWith("krank melden?", StringComparison.Ordinal) == true, $"Der Titel der Rückfrage ist „{title}“, erwartet „… krank melden?“.");
        });
        s.Step("„Abbrechen“: die Rückfrage schließt, nichts ändert sich", () =>
        {
            s.Click(s.Find(DialogCancel, "Antwort „Abbrechen“"));
            s.ExpectMissing("ConfirmButton");
        });

        Point pause = default;
        Point block = default;
        s.Step("Pause anlegen: Rechtsklick auf eine freie Stelle, „Pause hier“, „Pause eintragen“", () =>
        {
            var header = ColumnHeader(s).BoundingRectangle;
            var body = s.Find("BodyScroll").BoundingRectangle;
            pause = FreePointInColumn(s, header, body.Y + (int)(body.Height * 0.25));
            LiveSession.RightClickAt(pause);
            Note(s, $"MENÜ freie Stelle: {string.Join(" | ", s.VisibleMenuItems())}");
            s.ChooseMenuItem("Pause hier");
            s.Click(Button("Pause eintragen"), "Knopf „Pause eintragen“");
            Note(s, $"Meldung nach „Pause eintragen“: {ReadToast(s)}; unterer Rand: {string.Join(" | ", BottomTexts(s))}");
        });
        s.Step("Sperrzeit anlegen: Rechtsklick auf eine zweite freie Stelle, „Sperrzeit hier“, „Sperren“", () =>
        {
            var header = ColumnHeader(s).BoundingRectangle;
            var body = s.Find("BodyScroll").BoundingRectangle;
            block = FreePointInColumn(s, header, body.Y + (int)(body.Height * 0.65));
            LiveSession.RightClickAt(block);
            s.ChooseMenuItem("Sperrzeit hier");
            s.Click(Button("Sperren"), "Knopf „Sperren“");
            Note(s, $"Sperren ohne Grund: {string.Join(" | ", s.VisibleTexts(e => e.Properties.ClassName.ValueOrDefault is "InfoBar" or "TextBlock" && e.Properties.Name.ValueOrDefault?.Contains("Grund", StringComparison.Ordinal) == true))}");
            s.Type("Grund", "Wartung (Live-Test)");
            s.Click(Button("Sperren"), "Knopf „Sperren“");
            Note(s, $"Meldung nach „Sperren“: {ReadToast(s)}; unterer Rand: {string.Join(" | ", BottomTexts(s))}");
        });
        DeleteBlockAt(s, "Pause", () => new Point(pause.X, pause.Y + 24));
        DeleteBlockAt(s, "Sperrzeit", () => new Point(block.X, block.Y + 8));
        SwitchAccount(s, "Nina Kovač", "Rezeption");
        TodayStep(s);
    }

    /// <summary>Rechtsklick auf die Pause oder Sperrzeit; ein verlorener Rechtsklick wird bis zu dreimal wiederholt und im Bericht vermerkt.</summary>
    private static void OpenBlockMenu(LiveSession s, string what, Func<Point> at)
    {
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            LiveSession.RightClickAt(at());
            if (s.TryFind(Class("MenuItem", NameStarts($"{what} löschen")), TimeSpan.FromSeconds(2)) is not null)
            {
                return;
            }

            Note(s, $"Rechtsklick auf die {what} brachte nicht „{what} löschen“ (Menü: {string.Join(" | ", s.VisibleMenuItems())}), Versuch {attempt} von 3.");
            LiveSession.Press(VirtualKeyShort.ESCAPE);
            Thread.Sleep(400);
        }
    }

    private static void DeleteBlockAt(LiveSession s, string what, Func<Point> at)
    {
        s.Step($"Rechtsklick auf die {what}: „{what} löschen“, Rückfrage „{what} löschen?“", () =>
        {
            OpenBlockMenu(s, what, at);
            Note(s, $"MENÜ {what}: {string.Join(" | ", s.VisibleMenuItems())}");
            s.ChooseMenuItem($"{what} löschen");
            s.Expect("ConfirmButton");
            var (title, _, _, _, _) = ReadDialog(s, $"{what} löschen");
            Check(s, title == $"{what} löschen?", $"Der Titel der Rückfrage ist „{title}“, erwartet „{what} löschen?“.");
        });
        s.Step($"„Abbrechen“ lässt die {what} stehen, danach „Löschen“", () =>
        {
            s.Click(s.Find(DialogCancel, "Antwort „Abbrechen“"));
            s.ExpectMissing("ConfirmButton");
            OpenBlockMenu(s, what, at);
            s.ChooseMenuItem($"{what} löschen");
            s.Click("ConfirmButton");
            s.ExpectMissing("ConfirmButton");
            Note(s, $"Meldung nach dem Löschen: {ReadToast(s, TimeSpan.FromSeconds(2))}");
        });
    }

    // ---------- B3: Verschieben auf gestern ----------

    private static void RescheduleYesterday(LiveSession s, LiveRunContext context)
    {
        var booked = context.Appointment!;
        RequireToday(booked);
        OpenCalendar(s);
        OpenPanel(s, booked);
        s.Step("„Verschieben …“, Tag auf gestern setzen", () =>
        {
            s.Click(Button("Verschieben …"), "Knopf „Verschieben …“");
            var yesterday = ParseShownDate(booked.Date).AddDays(-1).ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);
            s.TypeInto(DateField(s), yesterday);
            LiveSession.Press(VirtualKeyShort.TAB);
            Thread.Sleep(500);
            Note(s, $"Feld „Tag“ zeigt: {LiveSession.TextOf(DateField(s))}");
            ReportButtons(s, "Verschieben auf gestern", name => name is "Verschieben" or "+1 Tag" or "+1 Woche" or "−1 Tag" or "Abbrechen");
            var move = s.Find(Button("Verschieben"), "Knopf „Verschieben“");
            Check(s, !move.Properties.IsEnabled.ValueOrDefault, "„Verschieben“ ist mit dem Tag von gestern freigegeben (erwartet gesperrt).");
            LiveSession.Hover(move);
            Note(s, $"Hinweis am gesperrten Knopf „Verschieben“: {move.Properties.HelpText.ValueOrDefault}");
        });
        s.Step("Server: Verschieben auf gestern per API", () =>
        {
            using var api = new LiveApi(s.ApiUrl, "leitung.wien");
            var (location, zone) = ViennaOf(api);
            var appointment = ServerAppointment(api, location, zone, TodayIn(zone).AddDays(booked.DaysAhead), booked.Customer, booked.Begin);
            var start = DateTimeOffset.Parse(appointment["start"]!.ToString(), CultureInfo.InvariantCulture).AddDays(-1);
            var answer = api.Send(HttpMethod.Post, $"api/appointments/{appointment["id"]}/move", new { start });
            Note(s, $"API POST api/appointments/{{id}}/move auf gestern ({start:O}): {answer.Describe()}");
            Check(s, answer.Status is 400 or 409, $"Der Server antwortet auf das Verschieben in die Vergangenheit mit {answer.Status}, erwartet 400 oder 409.");
        });
        s.Step("Panel schließen: der Termin bleibt", () =>
        {
            ClosePanelWithEscape(s);
            s.Expect(Card(booked, "Gebucht"), "Karte mit Status „Gebucht“");
        });
    }

    // ---------- B4: Einchecken bei einem Termin von morgen ----------

    private static void CheckInTomorrow(LiveSession s, LiveRunContext context)
    {
        OpenCalendar(s);
        s.Step($"Neuen Termin für {DemoCustomer} auf morgen suchen und buchen", () =>
        {
            s.Click(Button("Heute"), "Knopf „Heute“");
            var tomorrowText = ParseShownDate(ShownDate(s)).AddDays(1).ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);
            s.Click(NewAppointmentButton(s));
            s.Expect("Kundin oder Kunde suchen");
            ChooseGuest(s, DemoCustomer);
            s.Choose("Behandlung", Treatment);
            s.TypeInto(DateField(s), tomorrowText);
            LiveSession.Press(VirtualKeyShort.TAB);
            s.Click(s.Find(e => e.Properties.Name.ValueOrDefault == "Freie Zeit suchen"
                                && e.Properties.ClassName.ValueOrDefault == "Button"
                                && e.Properties.AutomationId.ValueOrDefault != "FreeTimeButton",
                "Knopf „Freie Zeit suchen“ im Buchungsbereich"));
            s.Expect(e => e.Properties.ClassName.ValueOrDefault == "ListBoxItem", "Erste freie Zeit");
            s.Click(s.Find(e => e.Properties.Name.ValueOrDefault?.EndsWith("buchen (Enter)", StringComparison.Ordinal) == true
                                || e.Properties.Name.ValueOrDefault == "Buchen (Enter)",
                "Knopf „Buchen“ unter den freien Zeiten"));
            Note(s, $"Meldung nach dem Buchen: {ReadToast(s, TimeSpan.FromSeconds(4))}; unterer Rand: {string.Join(" | ", BottomTexts(s))}");
        });
        BookedAppointment tomorrow = null!;
        s.Step("Kalender auf morgen: die Karte ist da", () =>
        {
            s.Click(Button("Heute"), "Knopf „Heute“");
            s.Click("Nächster Tag");
            var name = "";
            for (var attempt = 0; attempt < 10 && name.Length < 5; attempt++)
            {
                name = s.Find(Class("AppointmentCard", NameContains($" {DemoCustomer}, {Treatment}, Gebucht")), $"Karte „{DemoCustomer}, {Treatment}“ morgen")
                    .Properties.Name.ValueOrDefault ?? "";
                Thread.Sleep(300);
            }

            tomorrow = new BookedAppointment(DemoCustomer, Treatment, name[..5], ShownDate(s), 1);
            Note(s, $"Termin morgen: {tomorrow.CardPrefix}");
        });
        s.Step("Karte öffnen: kein Knopf „Einchecken“, Hinweise lesen", () =>
        {
            SelectCard(s, tomorrow, "Gebucht");
            s.Expect(Button("Termin stornieren …"), "Knopf „Termin stornieren …“");
            Thread.Sleep(900);
            s.ExpectMissing(Button("Einchecken"), "Knopf „Einchecken“ bei einem Termin von morgen");
            Note(s, $"TEXTE mit „Einchecken“: {string.Join(" | ", s.VisibleTexts(NameContains("Einchecken")))}");
            ReportButtons(s, "Panel des Termins von morgen");
        });
        s.Step("Mehr aufklappen: Knöpfe gesperrt mit Hinweis", () =>
        {
            OpenMore(s, Button("Nicht erschienen"));
            ReportButtons(s, "Mehr beim Termin von morgen");
            foreach (var name in LockedWhenTomorrow)
            {
                if (s.TryFind(Button(name), TimeSpan.FromMilliseconds(500)) is { } disabled && !disabled.Properties.IsEnabled.ValueOrDefault)
                {
                    LiveSession.Hover(disabled);
                    Note(s, $"Hinweis am gesperrten Knopf „{name}“: {disabled.Properties.HelpText.ValueOrDefault}");
                    break;
                }
            }
        });
        s.Step("Server: Einchecken morgen per API", () =>
        {
            using var api = new LiveApi(s.ApiUrl, "leitung.wien");
            var (location, zone) = ViennaOf(api);
            var appointment = ServerAppointment(api, location, zone, TodayIn(zone).AddDays(1), DemoCustomer, tomorrow.Begin);
            var answer = api.Send(HttpMethod.Post, $"api/appointments/{appointment["id"]}/status", new { status = "CheckedIn" });
            Note(s, $"API POST api/appointments/{{id}}/status CheckedIn (Termin morgen): {answer.Describe()}");
            Check(s, answer.Status == 400 && answer.Body.Contains("Einchecken geht nur am Tag des Termins", StringComparison.Ordinal),
                $"Erwartet 400 „Einchecken geht nur am Tag des Termins.“, der Server antwortet: {answer.Describe()}");
            foreach (var status in StatusesBeforeTheDay)
            {
                var other = api.Send(HttpMethod.Post, $"api/appointments/{appointment["id"]}/status", new { status });
                Note(s, $"API POST api/appointments/{{id}}/status {status} (Termin morgen): {other.Describe()}");
                Check(s, other.Status == 400, $"Erwartet 400 für {status} bei einem Termin von morgen, der Server antwortet: {other.Status}.");
            }
        });
        s.Step("Panel schließen, Kalender zurück auf „Heute“", () =>
        {
            ClosePanelWithEscape(s);
            s.Click(Button("Heute"), "Knopf „Heute“");
        });
    }

    // ---------- B5: Kassa, Einchecken und kassieren? ----------

    private static void PosCheckInPrompt(LiveSession s, LiveRunContext context)
    {
        var rowName = "";
        Func<AutomationElement, bool> row = Class("ListBoxItem", e => e.Properties.Name.ValueOrDefault is { } name
                                                                     && name.Contains("noch nicht eingecheckt", StringComparison.Ordinal)
                                                                     && !name.Contains(GuestOf(context), StringComparison.Ordinal)
                                                                     && !name.Contains(DemoCustomer, StringComparison.Ordinal));
        OpenPos(s);
        s.Step("Zeile eines nicht eingecheckten Termins anklicken: Rückfrage „Einchecken und kassieren?“", () =>
        {
            var element = s.Find(row, "Zeile „noch nicht eingecheckt“ in „Heute abzurechnen“");
            rowName = element.Properties.Name.ValueOrDefault!;
            Note(s, $"Zeile: {rowName}");
            s.Click(element);
            s.Expect("ConfirmButton");
            var (title, _, _, _, _) = ReadDialog(s, "Einchecken und kassieren");
            Check(s, title == "Einchecken und kassieren?", $"Der Titel der Rückfrage ist „{title}“, erwartet „Einchecken und kassieren?“.");
        });
        s.Step("„Abbrechen“: nichts ändert sich (keine Rechnung, Zeile bleibt)", () =>
        {
            s.Click(s.Find(DialogCancel, "Antwort „Abbrechen“"));
            s.ExpectMissing("ConfirmButton");
            s.Expect("NoBillHint");
            s.Expect(NameIs(rowName), "Dieselbe Zeile in „Heute abzurechnen“");
        });
        s.Step("Zeile erneut anklicken und bestätigen: Termin eingecheckt, Rechnung offen", () =>
        {
            s.Click(s.Find(NameIs(rowName), "Zeile in „Heute abzurechnen“"));
            s.Click("ConfirmButton");
            s.WaitUntilMissing(e => e.Properties.AutomationId.ValueOrDefault == "NoBillHint", "Hinweis „Keine offene Rechnung“", TimeSpan.FromSeconds(10));
            Note(s, $"Nach dem Bestätigen: {string.Join(" | ", s.VisibleTexts(NameContains("Offene Rechnung")))}; Zeile noch „noch nicht eingecheckt“: {s.TryFind(NameIs(rowName), TimeSpan.FromSeconds(1)) is not null}");
            Note(s, $"Fehler oder Warnung der Kassa: {string.Join(" | ", s.VisibleTexts(Class("InfoBar", _ => true)))}");
        });
        s.Step("Rechnung verwerfen (der Termin bleibt eingecheckt)", () =>
        {
            s.Click(Button("Verwerfen"), "Knopf „Verwerfen“ der Rechnung");
            Note(s, "Rückfrage zum Verwerfen: " + string.Join(" | ", DialogTextIds.Select(id => TextOfId(s, id))));
            s.Click("ConfirmButton");
            s.Expect("NoBillHint");
        });
    }

    // ---------- B6: Kassenlade, Einlage und Entnahme ----------

    private static void WithdrawalTab(LiveSession s, LiveRunContext context)
    {
        OpenPos(s);
        s.Step("Rezeption: Reiter „Kassenlade & Abschluss“ zeigt nur „Einlage“", () =>
        {
            s.Click(Class("TabButton", NameIs("Kassenlade & Abschluss")), "Reiter „Kassenlade & Abschluss“");
            s.Expect(NameIs("Einlage"), "Überschrift „Einlage“");
            s.ExpectMissing(NameIs("Entnahme"), "Reiter „Entnahme“");
            Note(s, $"TEXTE Kassenlade (Rezeption): {string.Join(" | ", s.VisibleTexts(NameContains("ntnahme")))}");
        });
        SwitchAccount(s, "Markus Steiner", "Standortleitung");
        OpenPos(s);
        s.Step("Standortleitung: der Reiter „Entnahme“ ist da", () =>
        {
            s.Click(Class("TabButton", NameIs("Kassenlade & Abschluss")), "Reiter „Kassenlade & Abschluss“");
            var entnahme = s.TryFind(NameIs("Entnahme"), TimeSpan.FromSeconds(5));
            Check(s, entnahme is not null, "Standortleitung: Der Reiter „Entnahme“ fehlt in „Kassenlade & Abschluss“.");
            if (entnahme is not null)
            {
                s.Click(entnahme);
                Note(s, $"TEXTE Entnahme (Standortleitung): {string.Join(" | ", s.VisibleTexts().Where(text => text.Length < 200))}");
            }
        });
        SwitchAccount(s, "Nina Kovač", "Rezeption");
        TodayStep(s);
    }

    // ---------- B7: Kundin ohne Kontakt, Taste V ----------

    private static void NoContactAndKeyV(LiveSession s, LiveRunContext context)
    {
        var booked = context.Appointment!;
        var last = $"Kontakt{context.Stamp}";
        var fullName = $"Ohne {last}";
        s.Step("Kundin ohne Telefon und E-Mail über die API anlegen", () =>
        {
            using var api = new LiveApi(s.ApiUrl, "leitung.wien");
            var answer = api.Send(HttpMethod.Post, "api/customers",
                new { firstName = "Ohne", lastName = last, email = (string?)null, phone = (string?)null, birthDate = (string?)null, note = (string?)null });
            Note(s, $"API POST api/customers: {answer.Status}");
            if (!answer.IsSuccess)
            {
                throw new InvalidOperationException($"Die Kundin ließ sich nicht anlegen: {answer.Describe()}");
            }
        });
        OpenCalendar(s);
        s.Step($"Neuer Termin: Kundin „{last}“ wählen, Pille „Keine Kontaktmöglichkeit“", () =>
        {
            s.Click(Button("Heute"), "Knopf „Heute“");
            s.Click(NewAppointmentButton(s));
            s.Expect("Kundin oder Kunde suchen");
            ChooseGuest(s, fullName);
            var pill = s.Find(NameContains("Keine Kontaktmöglichkeit"), "Pille „Keine Kontaktmöglichkeit“");
            LiveSession.Hover(pill);
            Note(s, $"PILLE: {LiveSession.TextOf(pill)}; Hilfetext: {pill.Properties.HelpText.ValueOrDefault}");
        });
        s.Step("Panel schließen", () => ClosePanelWithEscape(s));
        s.Step("Taste V bei einem gebuchten Termin: Karte anklicken, Esc, V", () =>
        {
            ShowDay(s, booked, startFromToday: true);
            SelectCard(s, booked, "Gebucht");
            s.Expect(Button("Termin stornieren …"), "Knopf „Termin stornieren …“");
            Thread.Sleep(900);
            LiveSession.FocusCalendar();
            Note(s, $"Tastenleiste vor V: {string.Join(" | ", BottomTexts(s))}");
            LiveSession.Press(VirtualKeyShort.KEY_V);
            Thread.Sleep(800);
            var moving = s.TryFind(e => e.Properties.ClassName.ValueOrDefault == "DateField", TimeSpan.FromSeconds(2)) is not null;
            Note(s, $"Nach V: Verschieben-Feld sichtbar: {moving}; Meldung: {ReadToast(s, TimeSpan.FromSeconds(1))}; Tastenleiste: {string.Join(" | ", BottomTexts(s))}");
            Check(s, moving, "Taste V öffnete bei der markierten Karte das Verschieben (Feld „Tag“) nicht; siehe Meldung und Bild.");
        });
        s.Step("Panel schließen", () => ClosePanelWithEscape(s));
        s.Step("Taste V bei einem erledigten Termin des Vortags: Meldung statt Panel", () =>
        {
            GoBackToDayWithCards(s);
            var card = s.Find(Class("AppointmentCard", NameContains(", Erledigt")), "Karte „Erledigt“ am Vortag mit Terminen");
            s.ScrollIntoBody(card);
            s.Click(card);
            Thread.Sleep(900);
            LiveSession.FocusCalendar();
            LiveSession.Press(VirtualKeyShort.KEY_V);
            Thread.Sleep(800);
            Note(s, $"Nach V bei „{card.Properties.Name.ValueOrDefault}“: Meldung: {ReadToast(s, TimeSpan.FromSeconds(2))}; Verschieben-Feld: {s.TryFind(e => e.Properties.ClassName.ValueOrDefault == "DateField", TimeSpan.FromSeconds(1)) is not null}");
        });
        s.Step("Zurück auf „Heute“, Panel zu", () =>
        {
            ClosePanelWithEscape(s);
            s.Click(Button("Heute"), "Knopf „Heute“");
        });
    }

    /// <summary>Die Texte im unteren Streifen des Fensters (Tastenleiste und Meldungen).</summary>
    private static List<string> BottomTexts(LiveSession s)
    {
        var bottom = s.Window.BoundingRectangle.Bottom;
        return s.VisibleTexts(Class("TextBlock", e => e.BoundingRectangle.Y > bottom - 110 && e.BoundingRectangle.X > 120));
    }

    // ---------- Fehlerfälle am Server ----------

    private static void ApiErrors(LiveSession s, LiveRunContext context)
    {
        var booked = context.Appointment!;
        s.Step("Zwei Buchungen zur selben Zeit: 409 „Zeitraum ist belegt“", () =>
        {
            using var api = new LiveApi(s.ApiUrl, "leitung.wien");
            var (location, zone) = ViennaOf(api);
            var today = TodayIn(zone).AddDays(booked.DaysAhead);
            var first = ServerAppointment(api, location, zone, today, booked.Customer, booked.Begin);
            var request = new
            {
                locationId = location,
                treatmentId = first["treatmentId"]!.ToString(),
                customerId = first["customerId"]!.ToString(),
                start = first["start"]!.ToString(),
                employeeId = first["employeeId"]!.ToString(),
                roomId = first["roomId"]!.ToString(),
            };
            var second = api.Send(HttpMethod.Post, "api/appointments", request);
            Note(s, $"API POST api/appointments (zweite Buchung, gleiche Zeit, gleiche Person und Raum): {second.Describe()}");
            Check(s, second.Status == 409, $"Erwartet 409 für die zweite Buchung, der Server antwortet: {second.Describe()}");
            var past = api.Send(HttpMethod.Post, "api/appointments", request with
            {
                start = DateTimeOffset.Parse(first["start"]!.ToString(), CultureInfo.InvariantCulture).AddDays(-1).ToString("O", CultureInfo.InvariantCulture),
            });
            Note(s, $"API POST api/appointments (gestern): {past.Describe()}");
            Check(s, past.Status == 409, $"Erwartet 409 für eine Buchung in der Vergangenheit, der Server antwortet: {past.Describe()}");
        });
    }

    // ---------- B8: ohne Verbindung ----------

    private static void Compose(string verb)
    {
        var overrideFile = Environment.GetEnvironmentVariable("AISPA_LIVE_COMPOSE_OVERRIDE")
                           ?? throw new InvalidOperationException("Umgebungsvariable AISPA_LIVE_COMPOSE_OVERRIDE (Pfad der Override-Datei des Projekts aispa-sicht) fehlt.");
        var info = new ProcessStartInfo("docker") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        // Nur das Projekt aispa-sicht und nur der Dienst api; nie ein anderer Stack.
        foreach (var argument in new[] { "compose", "-p", "aispa-sicht", "-f", "compose.yaml", "-f", overrideFile, verb, "api" })
        {
            info.ArgumentList.Add(argument);
        }

        using var process = Process.Start(info) ?? throw new InvalidOperationException("docker ließ sich nicht starten.");
        process.StandardOutput.ReadToEnd();
        process.StandardError.ReadToEnd();
        process.WaitForExit(TimeSpan.FromMinutes(2));
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"docker compose {verb} api endete mit Code {process.ExitCode}.");
        }
    }

    private static void WaitForApi(LiveSession s)
    {
        using var http = new HttpClient { BaseAddress = s.ApiUrl, Timeout = TimeSpan.FromSeconds(5) };
        var deadline = DateTime.UtcNow.AddMinutes(3);
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                if (http.GetStringAsync("api/system/status").GetAwaiter().GetResult().Contains("\"phase\":\"Ready\"", StringComparison.Ordinal))
                {
                    return;
                }
            }
            catch (HttpRequestException)
            {
                // noch nicht erreichbar
            }
            catch (TaskCanceledException)
            {
                // Zeitüberschreitung
            }

            Thread.Sleep(2000);
        }

        throw new InvalidOperationException("Die API ist nach drei Minuten nicht wieder bereit.");
    }

    /// <summary>Hält fest, was die App ohne Verbindung meldet: Banner, Fehlerleisten, Meldung unten im Kalender, Texte am unteren Rand.</summary>
    private static void ReportOffline(LiveSession s, string label)
    {
        string[] keywords = ["erbindung", "ffline", "orgemerkt", "Server", "Fehler", "bgleich", "nicht", "Bitte", "bitte"];
        var messages = s.VisibleTexts(e => e.Properties.ClassName.ValueOrDefault is "InfoBar" or "TextBlock"
                                          && e.Properties.Name.ValueOrDefault is { } name
                                          && !name.StartsWith("Test-Signatureinrichtung", StringComparison.Ordinal)
                                          && !name.Contains("noch nicht eingecheckt", StringComparison.Ordinal)
                                          && !name.StartsWith("Der Beleg wird am Server", StringComparison.Ordinal)
                                          && (keywords.Any(name.Contains) || e.Properties.ClassName.ValueOrDefault == "InfoBar"));
        var toast = ReadToast(s, TimeSpan.FromSeconds(1));
        Note(s, $"OFFLINE {label}: Meldungen: [{string.Join(" | ", messages)}]; Toast „{toast}“; unterer Rand: [{string.Join(" | ", BottomTexts(s))}]");
        ReportButtons(s, $"offline {label}", name => name.Contains("erbindung", StringComparison.Ordinal) || name is "Erledigt" or "Rückgängig");
    }

    private static void Offline(LiveSession s, LiveRunContext context)
    {
        var apiStopped = false;
        try
        {
            s.Step("Vor dem Anhalten: Kassa lesen (Zeile merken)", () => s.Click(Navigation(s, "Kassa")));
            s.Step("API anhalten (docker compose stop api, nur Projekt aispa-sicht)", () =>
            {
                Compose("stop");
                apiStopped = true;
                Note(s, "API-Container angehalten.");
                Thread.Sleep(2000);
            });
            s.Step("Kassieren: Zeile in „Heute abzurechnen“ anklicken, Rückfrage bestätigen", () =>
            {
                var element = s.TryFind(Class("ListBoxItem", e => e.Properties.Name.ValueOrDefault is { } rowText
                                                                           && rowText.Contains("noch nicht eingecheckt", StringComparison.Ordinal)
                                                                           && !rowText.Contains(GuestOf(context), StringComparison.Ordinal)
                                                                           && !rowText.Contains(DemoCustomer, StringComparison.Ordinal)),
                    TimeSpan.FromSeconds(8));
                Check(s, element is not null, "Ohne Verbindung steht keine Zeile „noch nicht eingecheckt“ in „Heute abzurechnen“.");
                if (element is not null)
                {
                    Note(s, $"Zeile: {element.Properties.Name.ValueOrDefault}");
                    s.Click(element);
                    if (s.TryFind("ConfirmButton", TimeSpan.FromSeconds(3)) is not null)
                    {
                        ReadDialog(s, "ohne Verbindung: Einchecken und kassieren");
                        s.Click("ConfirmButton");
                    }
                }

                ReportOffline(s, "Kassieren, 2 s nach der Bestätigung");
                // Der Fehler kommt erst nach der Zeitüberschreitung der Anfrage; so lange warten und festhalten, was dann steht.
                var waited = Stopwatch.StartNew();
                var shown = s.TryFind(e => e.Properties.Name.ValueOrDefault is { } text
                                           && (text.StartsWith("Server nicht erreichbar", StringComparison.Ordinal) || text.StartsWith("Keine Verbindung zum Server", StringComparison.Ordinal)),
                    TimeSpan.FromSeconds(40));
                Note(s, $"Erste Meldung zur fehlenden Verbindung nach {waited.Elapsed.TotalSeconds:0} s (nach den 2 s oben): {(shown is null ? "keine" : "da")}.");
                s.Snapshot("kassieren-ohne-verbindung-nach-wartezeit");
                ReportOffline(s, "Kassieren, nach der Wartezeit");
            });
            s.Step("Einchecken: Karte öffnen, Knopf „Einchecken“", () =>
            {
                s.Click(Navigation(s, "Kalender"));
                s.Click(Button("Heute"), "Knopf „Heute“");
                var card = s.Find(Class("AppointmentCard", e => e.Properties.Name.ValueOrDefault is { } name
                                                               && name.EndsWith(", Gebucht", StringComparison.Ordinal)
                                                               && !name.Contains(GuestOf(context), StringComparison.Ordinal)
                                                               && !name.Contains(DemoCustomer, StringComparison.Ordinal)),
                    "Karte „Gebucht“ von heute");
                Note(s, $"Karte: {card.Properties.Name.ValueOrDefault}");
                s.ScrollIntoBody(card);
                s.Click(card);
                s.Expect(Button("Termin stornieren …"), "Knopf „Termin stornieren …“");
                Thread.Sleep(900);
                ReportOffline(s, "Karte geöffnet");
                if (s.TryFind(EnabledButton("Einchecken"), TimeSpan.FromSeconds(2)) is { } checkIn)
                {
                    s.Click(checkIn);
                }
                else
                {
                    Note(s, "BEFUND: Der Knopf „Einchecken“ ist ohne Verbindung nicht da oder gesperrt.");
                }

                Thread.Sleep(1500);
                ReportOffline(s, "Einchecken");
            });
            s.Step("Buchen: Neuer Termin, Kundin suchen, „Termin buchen“", () =>
            {
                ClosePanelWithEscape(s);
                s.Click(NewAppointmentButton(s));
                s.Expect("Kundin oder Kunde suchen");
                try
                {
                    s.Type("Kundin oder Kunde suchen", OtherDemoCustomer);
                    Thread.Sleep(3000);
                    ReportOffline(s, "Kundensuche");
                    ChooseGuest(s, OtherDemoCustomer);
                    s.Choose("Behandlung", Treatment);
                    s.TypeInto(DateField(s), ParseShownDate(ShownDate(s)).AddDays(3).ToString("dd.MM.yyyy", CultureInfo.InvariantCulture));
                    LiveSession.Press(VirtualKeyShort.TAB);
                    s.Type("Beginn", "16:40");
                    LiveSession.Press(VirtualKeyShort.TAB);
                    ReportOffline(s, "Buchungsbereich vor dem Buchen");
                    s.Click(Button("Termin buchen"), "Knopf „Termin buchen“");
                    Thread.Sleep(2000);
                }
                catch (InvalidOperationException ex)
                {
                    Note(s, $"BEFUND: Buchen ohne Verbindung ging nicht weiter: {ex.Message}");
                }

                ReportOffline(s, "Buchen");
            });
            s.Step("API wieder starten und auf „Bereit“ warten", () =>
            {
                Compose("start");
                apiStopped = false;
                WaitForApi(s);
                Note(s, "API wieder bereit.");
            });
            s.Step("Die App arbeitet wieder: Verbindung prüfen, Kalender lesen", () =>
            {
                if (s.TryFind(Button("Verbindung prüfen"), TimeSpan.FromSeconds(3)) is { } check)
                {
                    s.Click(check);
                }

                ClosePanelWithEscape(s);
                Thread.Sleep(5000);
                ReportOffline(s, "nach dem Abgleich");
                s.Click(Navigation(s, "Kalender"));
                s.Click(Button("Heute"), "Knopf „Heute“");
                s.Click(Button("Heute"), "Knopf „Heute“");
                s.Expect(Class("AppointmentCard", _ => true), "Eine Karte im Kalender");
            });
        }
        finally
        {
            if (apiStopped)
            {
                Compose("start");
                WaitForApi(s);
            }
        }
    }
}
