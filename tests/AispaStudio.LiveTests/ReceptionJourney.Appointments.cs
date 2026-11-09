using System.Drawing;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.WindowsAPI;

namespace AispaStudio.LiveTests;

internal static partial class ReceptionJourney
{
    private static readonly string[] TermNeeds = ["appointment"];

    private static LiveTopic[] AppointmentTopics =>
    [
        new("appointment", "Termin buchen", $"{Treatment} zur ersten freien Zeit buchen, für die Kundin des Laufs oder {DemoCustomer}", BookAppointment),
        new("appointment-conflict", "Termin buchen: Zeitraum belegt", "Dieselbe Zeit, derselbe Raum, anderer Gast: „Zeitraum ist belegt.“", Conflict, TermNeeds,
            Leaves: "Legt nichts an (die Buchung wird abgelehnt)."),
        new("appointment-past", "Termin buchen: Vergangenheit", "Auf eine freie Stelle von gestern klicken: kein Termin in der Vergangenheit", PastBooking,
            Leaves: "Legt nichts an."),
        new("appointment-closed", "Termin buchen: außerhalb der Öffnungszeit", "Beginn 20:30 bietet „Trotzdem buchen“ an; es wird nicht gebucht", Closed,
            Leaves: "Legt nichts an."),
        new("checkin", "Einchecken und zurücknehmen", "Termin einchecken, mit „Rückgängig“ zurücknehmen", CheckIn, TermNeeds),
        new("probe-checkin-loop", "Probe: „Einchecken“ wiederholt anklicken", "Zählt, wie oft der Klick auf „Einchecken“ nicht ankommt (Mausprobe B8, mit AISPA_LIVE_MOUSE=1)", ProbeCheckInLoop, TermNeeds),
        new("probe-cancel-loop", "Probe: „Termin stornieren …“ wiederholt anklicken", "Zählt, wie oft der Klick auf „Termin stornieren …“ nicht ankommt (Mausprobe B8)", ProbeCancelLoop, TermNeeds,
            Leaves: "Legt nichts an."),
        new("checkin-late", "Gast kommt später", "Rechtsklick „Kommt 10 Min. später“, dann „Doch pünktlich“", Late, TermNeeds),
        new("checkin-undo-late", "Einchecken später zurücknehmen", "Einchecken, Meldung abwarten, dann im Panel „Einchecken zurücknehmen“", UndoLate, TermNeeds),
        new("checkin-noshow", "Nicht erschienen nach dem Einchecken", "Bei einem eingecheckten Termin ist „Nicht erschienen“ gesperrt", NoShowAfterCheckIn, TermNeeds),
        new("reschedule", "Termin verschieben", "Panel „Verschieben …“, „+1 Tag“, „Verschieben“, dann „Rückgängig“", Reschedule, TermNeeds),
        new("reschedule-past", "Verschieben in die Vergangenheit", Unchecked + "Bei einem Termin von heute ist „−1 Tag“ gesperrt (Lücke L2 behoben)", ReschedulePast, TermNeeds,
            Leaves: "Legt nichts an."),
        new("reschedule-checked-in", "Eingecheckten Termin verschieben", "Bei einem eingecheckten Termin fehlt „Verschieben …“", RescheduleCheckedIn, TermNeeds),
        new("cancel", "Termin stornieren und zurücknehmen", "" + Unchecked + "„Termin stornieren …“, „Stornieren“, mit „Rückgängig“ zurück", Cancel, TermNeeds),
        new("cancel-restore", "Stornierung später zurücknehmen", "" + Unchecked + "Storno, Meldung abwarten, im Panel „Stornierung zurücknehmen“", CancelRestore, TermNeeds),
        new("cancel-esc", "Stornieren mit Esc abbrechen", "" + Unchecked + "Der Dialog „Termin stornieren?“ schließt mit Esc, die Karte bleibt", CancelEsc, TermNeeds,
            Leaves: "Legt nichts an."),
        new("cancel-no-fee", "Keine Ausfallgebühr für die Rezeption", "" + Unchecked + "„Ausfallgebühr belasten …“ fehlt im Panel (Lücke L3)", CancelNoFee, TermNeeds,
            Leaves: "Legt nichts an."),
    ];

    /// <summary>Anzahl der Durchläufe der Probe-Themen (<c>AISPA_LIVE_LOOP_N</c>, Standard 10).</summary>
    private static int LoopCount => int.TryParse(Environment.GetEnvironmentVariable("AISPA_LIVE_LOOP_N"), out var n) && n > 0 ? n : 10;

    private static string GuestOf(LiveRunContext context) => context.CustomerName ?? DemoCustomer;

    private static void OpenCalendar(LiveSession s) => s.Step("Kalender öffnen", () => s.Click(Navigation(s, "Kalender")));

    /// <summary>Der Knopf „Termin“ in der Kopfzeile des Kalenders (Hauptteil des SplitButtons).</summary>
    private static AutomationElement NewAppointmentButton(LiveSession s) =>
        s.Find(e => e.Properties.AutomationId.ValueOrDefault == "PART_PrimaryButton" && e.Properties.Name.ValueOrDefault == "Termin",
            "Knopf „Termin“");

    /// <summary>
    /// Die Meldung unten im Kalender (Toast), deren Text mit <paramref name="start"/> beginnt. Der Text heißt für UI Automation fest
    /// „Meldung“; den Wortlaut liefert Avalonia über den ToolTip als Hilfetext.
    /// </summary>
    private static Func<AutomationElement, bool> Toast(string start) =>
        Class("TextBlock", e => e.Properties.Name.ValueOrDefault?.StartsWith(start, StringComparison.Ordinal) == true
                                || e.Properties.HelpText.ValueOrDefault?.StartsWith(start, StringComparison.Ordinal) == true);

    /// <summary>Sichtprüfung 09.11.2026: Fehlt die Meldung, steht ein Befund im Bericht, und der Lauf geht weiter.</summary>
    private static void ExpectToast(LiveSession s, string start, string what)
    {
        try
        {
            s.Expect(Toast(start), what);
        }
        catch (InvalidOperationException)
        {
            s.Report.AddEvent($"BEFUND: {what} nicht gefunden.");
        }
    }

    /// <summary>Einchecken geht nur am Tag des Termins; ohne Termin von heute scheitert das Thema mit klarer Meldung statt an einem fehlenden Knopf.</summary>
    private static void RequireToday(BookedAppointment booked)
    {
        if (booked.DaysAhead != 0)
        {
            throw new InvalidOperationException(
                $"Einchecken geht nur am Tag des Termins; der Termin des Laufs liegt am {booked.Date}, heute war keine Zeit mehr frei.");
        }
    }

    /// <summary>Wählt im Buchungspanel den Gast: suchen, Treffer klicken, warten bis „Ändern“ erscheint.</summary>
    private static void ChooseGuest(LiveSession s, string customer)
    {
        s.Type("Kundin oder Kunde suchen", customer);
        s.Click(s.Find(e => e.Properties.ClassName.ValueOrDefault is "ListBoxItem" or "Button"
                            && e.Properties.Name.ValueOrDefault?.StartsWith(customer, StringComparison.Ordinal) == true,
            $"Treffer „{customer}“"));
        s.Expect("Ändern");
    }

    private static void OpenBookingPanel(LiveSession s, string customer) =>
        s.Step($"Neuen Termin für {customer} beginnen", () =>
        {
            s.Click(NewAppointmentButton(s));
            s.Expect("Kundin oder Kunde suchen");
            ChooseGuest(s, customer);
            s.Choose("Behandlung", Treatment);
        });

    /// <summary>Schließt das Seitenpanel mit Esc, ohne etwas zu speichern; ein offenes Menü schließt Esc zuerst.</summary>
    private static void ClosePanelWithEscape(LiveSession s)
    {
        LiveSession.Press(VirtualKeyShort.ESCAPE);
        Thread.Sleep(300);
        // Mit Eingaben fragt das Panel „Änderungen verwerfen?“ (Sichtprüfung 09.11.2026): Verwerfen beantwortet es.
        if (s.TryFind(Button("Verwerfen"), TimeSpan.FromMilliseconds(600)) is { } discard)
        {
            s.Click(discard);
        }

        if (s.TryFind("Kundin oder Kunde suchen", TimeSpan.FromMilliseconds(600)) is not null)
        {
            LiveSession.Press(VirtualKeyShort.ESCAPE);
        }
    }

    private static void BookAppointment(LiveSession s, LiveRunContext context)
    {
        var customer = GuestOf(context);
        var today = default(DateTime);
        OpenCalendar(s);
        // Einchecken und Kassieren gehen nur am Tag des Termins; die Suche nach freier Zeit beginnt deshalb heute.
        s.Step("Auf „Heute“ stellen", () =>
        {
            s.Click(Button("Heute"), "Knopf „Heute“");
            today = ParseShownDate(ShownDate(s));
        });
        s.Step("Neuen Termin beginnen", () =>
        {
            s.Click(NewAppointmentButton(s));
            s.Expect("Kundin oder Kunde suchen");
        });
        s.Step("Ohne Gast ist „Termin buchen“ gesperrt", () =>
        {
            s.Expect(Button("Termin buchen"), "Knopf „Termin buchen“");
            s.ExpectMissing(Class("Button", e => e.Properties.Name.ValueOrDefault == "Termin buchen" && e.Properties.IsEnabled.ValueOrDefault),
                "Freigegebener Knopf „Termin buchen“");
        });
        s.Step($"Kundin {customer} suchen und wählen", () => ChooseGuest(s, customer));
        s.Step($"{Treatment} wählen", () => s.Choose("Behandlung", Treatment));
        // Eine feste Uhrzeit hielte nicht: jeder Lauf hinterlässt einen Termin. Die Rezeption sucht deshalb wie im Betrieb freie Zeit.
        s.Step("Freie Zeit suchen", () =>
        {
            s.Click(s.Find(e => e.Properties.Name.ValueOrDefault == "Freie Zeit suchen"
                                && e.Properties.ClassName.ValueOrDefault == "Button"
                                && e.Properties.AutomationId.ValueOrDefault != "FreeTimeButton",
                "Knopf „Freie Zeit suchen“ im Buchungsbereich"));
            s.Expect(e => e.Properties.ClassName.ValueOrDefault == "ListBoxItem", "Erste freie Zeit");
        });
        s.Step("Erste freie Zeit buchen", () =>
        {
            s.Click(s.Find(e => e.Properties.Name.ValueOrDefault?.EndsWith("buchen (Enter)", StringComparison.Ordinal) == true
                                || e.Properties.Name.ValueOrDefault == "Buchen (Enter)",
                "Knopf „Buchen“ unter den freien Zeiten"));
            // Ein Stammgast hat schon Termine; erst die Meldung belegt, dass dieser Lauf gebucht hat.
            try
            {
                ExpectToast(s, $"Gebucht: {customer},", $"Meldung „Gebucht: {customer}, …“");
            }
            catch (Exception)
            {
                // Sichtprüfung 09.11.2026: Der Toast war nicht zu finden; der Lauf geht weiter, die Karte belegt die Buchung.
                s.Report.AddEvent("BEFUND: Toast „Gebucht: …“ nach dem Buchen nicht gefunden.");
            }
            // Die Kalenderkarte heißt „<Zeit> <Kundin>, <Behandlung>, Gebucht“; der Name allein stünde auch im Kundenfeld.
            // Der Name der Karte ist beim Neuaufbau des Kalenders kurz leer (Sichtprüfung 09.11.2026, Gegenprobe B8): nochmal lesen.
            string cardName = "";
            for (var attempt = 0; attempt < 10 && cardName.Length < 5; attempt++)
            {
                var card = s.Find(Class("AppointmentCard", e => e.Properties.Name.ValueOrDefault is { } name
                                                              && name.Contains($" {customer}, {Treatment}, Gebucht", StringComparison.Ordinal)),
                    $"Kalenderkarte „{customer}, {Treatment}“");
                cardName = card.Properties.Name.ValueOrDefault ?? "";
                if (cardName.Length < 5)
                {
                    Thread.Sleep(300);
                }
            }

            var shown = ShownDate(s);
            context.Appointment = new BookedAppointment(customer, Treatment, cardName[..5], shown,
                (int)(ParseShownDate(shown) - today).TotalDays);
        });
        s.Report.AddEvent($"Termin des Laufs: {context.Appointment!.CardPrefix}.");
    }

    private static void Conflict(LiveSession s, LiveRunContext context)
    {
        var booked = context.Appointment!;
        OpenCalendar(s);
        s.Step("Rechtsklick auf die freie Stelle unter dem Termin des Laufs: „Termin hier“", () =>
        {
            var cardElement = s.Find(Card(booked), "Karte des Termins");
            s.ScrollIntoBody(cardElement);
            var card = cardElement.BoundingRectangle;
            LiveSession.RightClickAt(FreePointInColumn(s, card, card.Bottom));
            s.ChooseMenuItem("Termin hier");
            s.Expect("Kundin oder Kunde suchen");
        });
        s.Step($"Anderen Gast ({OtherDemoCustomer}), dieselbe Behandlung und denselben Beginn ({booked.Begin}) eintragen", () =>
        {
            ChooseGuest(s, OtherDemoCustomer);
            s.Choose("Behandlung", booked.Treatment);
            s.Type("Beginn", booked.Begin);
            LiveSession.Press(VirtualKeyShort.TAB);
        });
        s.Step("Termin buchen: „Zeitraum ist belegt.“ mit „Freie Zeit suchen“", () =>
        {
            s.Click(Button("Termin buchen"), "Knopf „Termin buchen“");
            s.Expect(NameContains("Zeitraum ist belegt"), "Meldung „Zeitraum ist belegt.“");
            s.Expect(Button("Freie Zeit suchen"), "Knopf „Freie Zeit suchen“ unter der Meldung");
        });
        s.Step("Panel schließen: kein zweiter Termin zu dieser Zeit", () =>
        {
            ClosePanelWithEscape(s);
            s.ExpectMissing(Class("AppointmentCard", NameContains($"{booked.Begin} {OtherDemoCustomer}")), $"Karte für {OtherDemoCustomer} um {booked.Begin}");
        });
    }

    /// <summary>Ein freier Punkt in der Spalte der Karte <paramref name="inColumnOf"/>: weder in einer Karte noch in einer Vor- oder Nachbereitung.</summary>
    private static Point FreePointInColumn(LiveSession s, Rectangle inColumnOf, int? fromY = null, bool betweenCards = false)
    {
        var body = s.Find("BodyScroll").BoundingRectangle;
        // Vor- und Nachbereitung einer Karte sind Rahmen ohne eigenen Namen; die Karte wird deshalb oben und unten großzügig erweitert.
        var occupied = s.VisibleRects(e => e.Properties.ClassName.ValueOrDefault == "AppointmentCard")
            .Select(rect => Rectangle.Inflate(rect, 0, 32)).ToList();
        var x = inColumnOf.X + (inColumnOf.Width / 2);
        // Außerhalb der Dienstzeit (schraffiert) meldet der Kalender „Kein Dienst“; zwischen zwei Karten ist die Person im Dienst.
        var columnCards = s.VisibleRects(e => e.Properties.ClassName.ValueOrDefault == "AppointmentCard")
            .Where(rect => rect.Left <= x && x <= rect.Right).ToList();
        var top = columnCards.Count == 0 ? int.MaxValue : columnCards.Min(rect => rect.Top);
        var bottom = columnCards.Count == 0 ? int.MinValue : columnCards.Max(rect => rect.Bottom);
        for (var y = Math.Max(body.Y + 8, fromY ?? 0); y < body.Bottom - 8; y += 6)
        {
            var point = new Point(x, y);
            if ((!betweenCards || (y > top && y < bottom)) && !occupied.Any(rect => rect.Contains(point)))
            {
                return point;
            }
        }

        throw new InvalidOperationException("Keine freie Stelle in der Spalte gefunden.");
    }

    private static void PastBooking(LiveSession s, LiveRunContext context)
    {
        OpenCalendar(s);
        // Sichtprüfung 09.11.2026 (T4): Am Montag war gestern ein Sonntag ohne Dienst; der Lauf geht bis zu sieben Tage zurück, bis ein Tag Karten hat.
        s.Step("Auf den letzten Tag davor mit Terminen gehen: „Heute“, dann „Vortag“ (bis zu 7 Tage)", () =>
        {
            s.Click(Button("Heute"), "Knopf „Heute“");
            for (var back = 1; back <= 7; back++)
            {
                s.Click("Vortag");
                Thread.Sleep(900); // die Karten des Vortags laden nach dem Klick
                if (s.TryFind(Class("AppointmentCard", _ => true), TimeSpan.FromMilliseconds(1500)) is not null)
                {
                    s.Report.AddEvent($"Vortag mit Terminen: {ShownDate(s)} ({back} {(back == 1 ? "Tag" : "Tage")} zurück).");
                    return;
                }
            }

            throw new InvalidOperationException("In den letzten sieben Tagen gab es keinen Tag mit Terminen.");
        });
        s.Step("Freie Stelle anklicken: kein Termin in der Vergangenheit", () =>
        {
            var spot = FreeSpotOnPastDay(s);
            // Ein Linksklick neben einer Karte in belegter Zeit bucht nie (StartBookingAt); „Termin hier“ im Rechtsklick-Menü ist die bewusste Wahl und trifft die Prüfung auf die Vergangenheit.
            LiveSession.RightClickAt(spot);
            s.ChooseMenuItem("Termin hier");
            ExpectToast(s, "Termine lassen sich nicht in der Vergangenheit eintragen", "Meldung „Termine lassen sich nicht in der Vergangenheit eintragen“");
            s.ExpectMissing(Class("TextBox", NameIs("Kundin oder Kunde suchen")), "Buchungspanel");
        });
        s.Step("Wieder auf den Tag des Laufs stellen", () =>
        {
            if (context.Appointment is { } booked)
            {
                ShowDay(s, booked, startFromToday: true);
                return;
            }

            s.Click(Button("Heute"), "Knopf „Heute“");
            s.Click("Nächster Tag");
        });
    }

    /// <summary>Eine freie Stelle am Ende einer Spalte des gezeigten Tages; eine Spalte ohne freie Stelle wird übergangen.</summary>
    private static Point FreeSpotOnPastDay(LiveSession s)
    {
        foreach (var card in s.VisibleRects(e => e.Properties.ClassName.ValueOrDefault == "AppointmentCard"))
        {
            try
            {
                // Weit unten in der Spalte (Abend, nach der letzten Karte): dort liegt keine Karte samt Vor- und Nachbereitung.
                return FreePointInColumn(s, card, s.Find("BodyScroll").BoundingRectangle.Bottom - 120);
            }
            catch (InvalidOperationException)
            {
                // diese Spalte ist unten belegt; die nächste Karte probieren
            }
        }

        throw new InvalidOperationException("Am Vortag mit Terminen gibt es keine freie Stelle am Ende einer Spalte.");
    }

    private static void Closed(LiveSession s, LiveRunContext context)
    {
        var customer = GuestOf(context);
        OpenCalendar(s);
        OpenBookingPanel(s, customer);
        s.Step("Beginn 20:30 eintragen: Warnung „geschlossen“ mit „Trotzdem buchen“", () =>
        {
            s.Type("Beginn", "20:30");
            LiveSession.Press(VirtualKeyShort.TAB);
            // Die Warnung steht nach „Termin buchen“ sicher da; „Trotzdem buchen“ bestätigt erst, was der Test nicht tut.
            s.Click(Button("Termin buchen"), "Knopf „Termin buchen“");
            s.Expect(NameContains("Der Standort hat zu dieser Zeit geschlossen"), "Warnung „Der Standort hat zu dieser Zeit geschlossen.“");
            s.Expect(Button("Trotzdem buchen"), "Knopf „Trotzdem buchen“");
        });
        s.Step("Nicht buchen: Panel schließen", () =>
        {
            ClosePanelWithEscape(s);
            s.ExpectMissing(Class("AppointmentCard", NameContains($"20:30 {customer}")), $"Karte für {customer} um 20:30");
        });
    }

    /// <summary>
    /// Klickt die Karte an und wartet auf das Panel („Mehr“). Ein Mausklick auf die Karte kam in der Gegenprobe B8 gelegentlich nicht an (nur die Hover-Vorschau
    /// erschien); der Tester klickt dann bis zu dreimal und schreibt jeden Wiederholungsversuch in den Bericht.
    /// </summary>
    private static void SelectCard(LiveSession s, BookedAppointment booked, string status)
    {
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            s.Click(Card(booked, status), $"Karte „{booked.CardPrefix}“ mit Status {status}");
            if (s.TryFind("MoreToggle", TimeSpan.FromSeconds(2)) is not null)
            {
                return;
            }

            s.Report.AddEvent($"Klick auf die Karte „{booked.CardPrefix}“ kam nicht an (kein Panel nach 2 s), Versuch {attempt} von 3.");
        }
    }

    private static void CheckIn(LiveSession s, LiveRunContext context)
    {
        var booked = context.Appointment!;
        RequireToday(booked);
        OpenCalendar(s);
        s.Step("Karte des Termins anklicken", () =>
        {
            SelectCard(s, booked, "Gebucht");
            s.Expect(Button("Einchecken"), "Knopf „Einchecken“ im Panel");
        });
        s.Step("Einchecken: Meldung mit „Rückgängig“, Karte „Eingecheckt“", () =>
        {
            s.Click(Button("Einchecken"), "Knopf „Einchecken“");
            ExpectToast(s, $"{booked.Customer} eingecheckt", $"Meldung „{booked.Customer} eingecheckt“");
            s.Expect(Card(booked, "Eingecheckt"), "Karte mit Status „Eingecheckt“");
        });
        s.Step("„Rückgängig“: Karte wieder „Gebucht“", () =>
        {
            s.Click(Class("Button", NameContains("Rückgängig")), "Knopf „Rückgängig“");
            s.Expect(Card(booked, "Gebucht"), "Karte mit Status „Gebucht“");
        });
    }

    /// <summary>
    /// Probe B8: Karte anklicken, „Einchecken“ anklicken, ohne Wiederholung zählen, ob die Karte „Eingecheckt“ zeigt; zurückgenommen wird per „Invoke“,
    /// damit nur der Klick auf „Einchecken“ gemessen wird. Das Ergebnis steht im Bericht („verloren x von n“); das Thema bleibt grün.
    /// </summary>
    private static void ProbeCheckInLoop(LiveSession s, LiveRunContext context)
    {
        var booked = context.Appointment!;
        RequireToday(booked);
        OpenCalendar(s);
        var runs = LoopCount;
        var lost = 0;
        s.Step($"„Einchecken“ {runs}-mal anklicken und zählen", () =>
        {
            for (var run = 1; run <= runs; run++)
            {
                SelectCard(s, booked, "Gebucht");
                s.Expect(Button("Einchecken"), "Knopf „Einchecken“ im Panel");
                s.Click(Button("Einchecken"), "Knopf „Einchecken“");
                if (s.TryFind(Card(booked, "Eingecheckt"), TimeSpan.FromSeconds(3)) is null)
                {
                    lost++;
                    s.Report.AddEvent($"Lauf {run} von {runs}: Klick auf „Einchecken“ kam NICHT an.");
                }
                else
                {
                    s.Report.AddEvent($"Lauf {run} von {runs}: eingecheckt.");
                    InvokeQuietly(s.Find(Button("Einchecken zurücknehmen"), "Knopf „Einchecken zurücknehmen“"));
                    s.Expect(Card(booked, "Gebucht"), "Karte mit Status „Gebucht“");
                }

                ClosePanelWithEscape(s);
            }

            s.Report.AddEvent($"ERGEBNIS Einchecken: verloren {lost} von {runs}.");
        });
    }

    /// <summary>Probe B8: wie <see cref="ProbeCheckInLoop"/> für „Termin stornieren …“; geprüft wird nur, ob der Dialog aufgeht, bestätigt wird nie.</summary>
    private static void ProbeCancelLoop(LiveSession s, LiveRunContext context)
    {
        var booked = context.Appointment!;
        OpenCalendar(s);
        var runs = LoopCount;
        var lost = 0;
        s.Step($"„Termin stornieren …“ {runs}-mal anklicken und zählen", () =>
        {
            for (var run = 1; run <= runs; run++)
            {
                SelectCard(s, booked, "Gebucht");
                s.Expect(Button("Termin stornieren …"), "Knopf „Termin stornieren …“");
                s.Click(Button("Termin stornieren …"), "Knopf „Termin stornieren …“");
                if (s.TryFind("ConfirmButton", TimeSpan.FromSeconds(3)) is null)
                {
                    lost++;
                    s.Report.AddEvent($"Lauf {run} von {runs}: Klick auf „Termin stornieren …“ kam NICHT an.");
                }
                else
                {
                    s.Report.AddEvent($"Lauf {run} von {runs}: Dialog offen.");
                    LiveSession.Press(VirtualKeyShort.ESCAPE);
                    s.ExpectMissing("ConfirmButton");
                }

                ClosePanelWithEscape(s);
            }

            s.Report.AddEvent($"ERGEBNIS Stornieren: verloren {lost} von {runs}.");
        });
    }

    private static void InvokeQuietly(AutomationElement button)
    {
        try
        {
            button.Patterns.Invoke.Pattern.Invoke();
        }
        catch (System.Runtime.InteropServices.COMException)
        {
            // Der Knopf verschwindet beim Auslösen; die Aktion ist geschehen.
        }
    }

    private static void Late(LiveSession s, LiveRunContext context)
    {
        var booked = context.Appointment!;
        OpenCalendar(s);
        s.Step("Rechtsklick auf die Karte: „Kommt 10 Min. später“", () =>
        {
            s.RightClick(s.Find(Card(booked, "Gebucht"), "Karte mit Status „Gebucht“"));
            s.ChooseMenuItem("Kommt 10 Min. später");
            ExpectToast(s, $"{booked.Customer} kommt 10 Min. später", $"Meldung „{booked.Customer} kommt 10 Min. später“");
        });
        s.Step("„Doch pünktlich“ nimmt die Verspätung zurück", () =>
        {
            s.RightClick(s.Find(Card(booked), "Karte des Termins"));
            s.ChooseMenuItem("Doch pünktlich");
            ExpectToast(s, $"{booked.Customer} kommt doch pünktlich", "Meldung „kommt doch pünktlich“");
            s.Expect(Card(booked, "Gebucht"), "Karte mit Status „Gebucht“");
        });
    }

    private static void UndoLate(LiveSession s, LiveRunContext context)
    {
        var booked = context.Appointment!;
        RequireToday(booked);
        OpenCalendar(s);
        s.Step("Termin einchecken", () =>
        {
            SelectCard(s, booked, "Gebucht");
            s.Expect(Button("Einchecken"), "Knopf „Einchecken“ im Panel");
            Thread.Sleep(700); // Sichtprüfung 09.11.2026: ein zu früher Klick geht ins Leere
            s.Click(Button("Einchecken"), "Knopf „Einchecken“");
            s.Expect(Card(booked, "Eingecheckt"), "Karte mit Status „Eingecheckt“");
        });
        s.Step("Meldung läuft ab", () =>
            s.WaitUntilMissing(Toast($"{booked.Customer} eingecheckt"), "Meldung „eingecheckt“", TimeSpan.FromSeconds(20)));
        s.Step("Im Panel „Einchecken zurücknehmen“: Karte wieder „Gebucht“", () =>
        {
            s.Click(Button("Einchecken zurücknehmen"), "Knopf „Einchecken zurücknehmen“");
            s.Expect(Card(booked, "Gebucht"), "Karte mit Status „Gebucht“");
        });
    }

    /// <summary>Klappt „Mehr“ im Panel auf; ein verlorener Mausklick auf die Kopfzeile (Gegenprobe B8) wird bis zu dreimal wiederholt und im Bericht vermerkt.</summary>
    private static void OpenMore(LiveSession s, Func<AutomationElement, bool> expected)
    {
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            s.Click("MoreToggle");
            if (s.TryFind(expected, TimeSpan.FromSeconds(2.5)) is not null)
            {
                return;
            }

            s.Report.AddEvent($"Klick auf „Mehr“ kam nicht an (nichts aufgeklappt nach 2,5 s), Versuch {attempt} von 3.");
        }
    }

    private static void NoShowAfterCheckIn(LiveSession s, LiveRunContext context)
    {
        var booked = context.Appointment!;
        RequireToday(booked);
        OpenCalendar(s);
        s.Step("Termin einchecken", () =>
        {
            SelectCard(s, booked, "Gebucht");
            s.Expect(Button("Einchecken"), "Knopf „Einchecken“ im Panel");
            Thread.Sleep(700); // Sichtprüfung 09.11.2026: ein zu früher Klick geht ins Leere
            s.Click(Button("Einchecken"), "Knopf „Einchecken“");
            s.Expect(Card(booked, "Eingecheckt"), "Karte mit Status „Eingecheckt“");
        });
        s.Step("Mehr aufklappen: „Nicht erschienen“ ist gesperrt", () =>
        {
            OpenMore(s, Button("Nicht erschienen"));
            s.Expect(Button("Nicht erschienen"), "Knopf „Nicht erschienen“");
            s.ExpectMissing(Class("Button", e => e.Properties.Name.ValueOrDefault == "Nicht erschienen" && e.Properties.IsEnabled.ValueOrDefault),
                "Freigegebener Knopf „Nicht erschienen“");
        });
        s.Step("Einchecken zurücknehmen (Zustand wie vorher)", () =>
        {
            s.Click(Button("Einchecken zurücknehmen"), "Knopf „Einchecken zurücknehmen“");
            s.Expect(Card(booked, "Gebucht"), "Karte mit Status „Gebucht“");
        });
    }

    private static void OpenPanel(LiveSession s, BookedAppointment booked, string status = "Gebucht") =>
        s.Step("Karte des Termins anklicken: Panel öffnet", () =>
        {
            SelectCard(s, booked, status);
            s.Expect(Button("Termin stornieren …"), "Knopf „Termin stornieren …“");
            Thread.Sleep(900); // Sichtprüfung 09.11.2026: ein zu früher Klick im frisch geöffneten Panel geht ins Leere
        });

    /// <summary>
    /// Verschiebt im offenen Panel mit „+1 Tag“, bis der Server annimmt: dieselbe Uhrzeit ist an einem Tag oft schon belegt („Zeitraum ist
    /// belegt“), dann geht es einen Tag weiter.
    /// </summary>
    private static void MoveDaysForward(LiveSession s)
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            s.Click(Button("+1 Tag"), "Knopf „+1 Tag“");
            s.Click(Button("Verschieben"), "Knopf „Verschieben“");
            if (s.TryFind(Toast("Verschoben auf"), TimeSpan.FromSeconds(4)) is not null)
            {
                return;
            }
        }

        throw new InvalidOperationException("Der Termin ließ sich an keinem der nächsten Tage verschieben.");
    }

    /// <summary>Das Datum in der Kopfzeile des Kalenders („22. November 2026“); der Kalender folgt einem verschobenen Termin auf dessen neuen Tag.</summary>
    private static DateTime ParseShownDate(string text)
    {
        // Für „Heute“ steht der Wochentag davor („Sonntag, 8. November 2026“).
        var comma = text.IndexOf(", ", StringComparison.Ordinal);
        var date = comma < 0 ? text : text[(comma + 2)..];
        return DateTime.ParseExact(date, "d. MMMM yyyy", System.Globalization.CultureInfo.GetCultureInfo("de-AT"));
    }

    /// <summary>Stellt den Kalender auf den Tag des Termins: „Heute“, dann „Nächster Tag“ bis das Datum stimmt.</summary>
    private static void ShowDay(LiveSession s, BookedAppointment booked, bool startFromToday = false)
    {
        if (!startFromToday && ShownDate(s) == booked.Date)
        {
            return;
        }

        s.Click(Button("Heute"), "Knopf „Heute“");
        for (var i = 0; i < booked.DaysAhead; i++)
        {
            s.Click("Nächster Tag");
        }

        if (ShownDate(s) != booked.Date)
        {
            throw new InvalidOperationException($"Der Kalender zeigt {ShownDate(s)}, erwartet {booked.Date}.");
        }
    }

    private static string ShownDate(LiveSession s) => LiveSession.TextOf(s.Find("DateLine"));

    /// <summary>
    /// Die Meldung zum Verschieben und ihr „Rückgängig“ verschwinden nach acht Sekunden; deshalb ein Schritt: Meldung, der Kalender
    /// zeigt einen anderen Tag, „Rückgängig“ geklickt, der Termin ist wieder am alten Tag.
    /// </summary>
    private static void MovedThenUndone(LiveSession s, string dateBefore, BookedAppointment booked)
    {
        ExpectToast(s, "Verschoben auf", "Meldung „Verschoben auf …“");
        var undo = s.Find(Class("Button", NameContains("Rückgängig")), "Knopf „Rückgängig“");
        if (ShownDate(s) == dateBefore)
        {
            throw new InvalidOperationException($"Der Kalender zeigt weiter {dateBefore}: der Termin ist nicht auf einen anderen Tag gewandert.");
        }

        s.Click(undo);
        ExpectBackOn(s, dateBefore, booked);
    }

    /// <summary>Nach „Rückgängig“ bleibt der Kalender auf dem Tag, auf den er dem Termin gefolgt war; „Heute“ und „Nächster Tag“ führen zum Tag des Termins zurück.</summary>
    private static void ExpectBackOn(LiveSession s, string dateBefore, BookedAppointment booked)
    {
        ShowDay(s, booked);
        s.Expect(Card(booked, "Gebucht"), "Karte mit Status „Gebucht“");
        if (ShownDate(s) != dateBefore)
        {
            throw new InvalidOperationException($"Der Kalender zeigt {ShownDate(s)}, erwartet wieder {dateBefore}.");
        }
    }

    private static void Reschedule(LiveSession s, LiveRunContext context)
    {
        var booked = context.Appointment!;
        OpenCalendar(s);
        var before = ShownDate(s);
        OpenPanel(s, booked);
        s.Step("„Verschieben …“, „+1 Tag“ und „Verschieben“", () =>
        {
            s.Click(Button("Verschieben …"), "Knopf „Verschieben …“");
            MoveDaysForward(s);
        });
        s.Step("Meldung „Verschoben auf“, der Kalender folgt dem Termin; „Rückgängig“ holt ihn zurück", () => MovedThenUndone(s, before, booked));
    }

    private static void ReschedulePast(LiveSession s, LiveRunContext context)
    {
        var booked = context.Appointment!;
        RequireToday(booked);
        OpenCalendar(s);
        OpenPanel(s, booked);
        s.Step("„Verschieben …“: der Termin liegt heute, „−1 Tag“ ist gesperrt", () =>
        {
            s.Click(Button("Verschieben …"), "Knopf „Verschieben …“");
            s.Expect(Button("−1 Tag"), "Knopf „−1 Tag“");
            s.ExpectMissing(EnabledButton("−1 Tag"), "Freigegebener Knopf „−1 Tag“");
        });
        s.Step("Panel schließen: der Termin bleibt", () =>
        {
            ClosePanelWithEscape(s);
            s.Expect(Card(booked, "Gebucht"), "Karte mit Status „Gebucht“");
        });
    }

    private static void RescheduleCheckedIn(LiveSession s, LiveRunContext context)
    {
        var booked = context.Appointment!;
        RequireToday(booked);
        OpenCalendar(s);
        OpenPanel(s, booked);
        s.Step("Einchecken", () =>
        {
            Thread.Sleep(700); // Sichtprüfung 09.11.2026: ein zu früher Klick geht ins Leere
            s.Click(Button("Einchecken"), "Knopf „Einchecken“");
            s.Expect(Card(booked, "Eingecheckt"), "Karte mit Status „Eingecheckt“");
        });
        s.Step("Im Panel fehlt „Verschieben …“", () =>
        {
            s.Expect(Button("Einchecken zurücknehmen"), "Knopf „Einchecken zurücknehmen“");
            s.ExpectMissing(Button("Verschieben …"), "Knopf „Verschieben …“");
        });
        s.Step("Einchecken zurücknehmen (Zustand wie vorher)", () =>
        {
            s.Click(Button("Einchecken zurücknehmen"), "Knopf „Einchecken zurücknehmen“");
            s.Expect(Card(booked, "Gebucht"), "Karte mit Status „Gebucht“");
        });
    }

    private static void BeginCancel(LiveSession s) =>
        s.Step("„Termin stornieren …“: Dialog „Termin stornieren?“", () =>
        {
            s.Click(Button("Termin stornieren …"), "Knopf „Termin stornieren …“");
            s.Expect("Termin stornieren?");
            s.Expect("ConfirmButton");
        });

    private static void Cancel(LiveSession s, LiveRunContext context)
    {
        var booked = context.Appointment!;
        OpenCalendar(s);
        OpenPanel(s, booked);
        BeginCancel(s);
        // Die Meldung mit „Rückgängig“ verschwindet nach acht Sekunden; deshalb alles in einem Schritt.
        s.Step("„Stornieren“: Meldung „Termin storniert“, Karte weg; „Rückgängig“ bringt sie zurück", () =>
        {
            s.Click("ConfirmButton");
            ExpectToast(s, "Termin storniert", "Meldung „Termin storniert“");
            var undo = s.Find(Class("Button", NameContains("Rückgängig")), "Knopf „Rückgängig“");
            s.ExpectMissing(Card(booked), $"Karte „{booked.CardPrefix}“");
            s.Click(undo);
            s.Expect(Card(booked, "Gebucht"), "Karte mit Status „Gebucht“");
        });
    }

    private static void CancelRestore(LiveSession s, LiveRunContext context)
    {
        var booked = context.Appointment!;
        OpenCalendar(s);
        OpenPanel(s, booked);
        BeginCancel(s);
        s.Step("„Stornieren“, Meldung läuft ab", () =>
        {
            s.Click("ConfirmButton");
            ExpectToast(s, "Termin storniert", "Meldung „Termin storniert“");
            s.WaitUntilMissing(Toast("Termin storniert"), "Meldung „Termin storniert“", TimeSpan.FromSeconds(20));
        });
        s.Step("Panel „Stornierung zurücknehmen“ und „Zurücknehmen“", () =>
        {
            s.Click(Button("Stornierung zurücknehmen"), "Knopf „Stornierung zurücknehmen“");
            s.Expect("Stornierung zurücknehmen?");
            s.Click(Button("Zurücknehmen"), "Knopf „Zurücknehmen“");
            s.Expect(Card(booked, "Gebucht"), "Karte mit Status „Gebucht“");
        });
    }

    private static void CancelEsc(LiveSession s, LiveRunContext context)
    {
        var booked = context.Appointment!;
        OpenCalendar(s);
        OpenPanel(s, booked);
        BeginCancel(s);
        s.Step("Esc: Dialog zu, Karte unverändert", () =>
        {
            LiveSession.Press(VirtualKeyShort.ESCAPE);
            s.ExpectMissing("ConfirmButton");
            s.Expect(Card(booked, "Gebucht"), "Karte mit Status „Gebucht“");
        });
    }

    private static void CancelNoFee(LiveSession s, LiveRunContext context)
    {
        var booked = context.Appointment!;
        OpenCalendar(s);
        OpenPanel(s, booked);
        s.Step("Im Panel gibt es keine „Ausfallgebühr belasten …“", () =>
            s.ExpectMissing(Class("Button", NameStarts("Ausfallgebühr")), "Knopf „Ausfallgebühr belasten …“"));
    }
}
