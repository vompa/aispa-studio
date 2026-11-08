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
        Class("TextBlock", e => e.Properties.Name.ValueOrDefault == "Meldung"
                                && e.Properties.HelpText.ValueOrDefault?.StartsWith(start, StringComparison.Ordinal) == true);

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
            s.Expect(Toast($"Gebucht: {customer},"), $"Meldung „Gebucht: {customer}, …“");
            // Die Kalenderkarte heißt „<Zeit> <Kundin>, <Behandlung>, Gebucht“; der Name allein stünde auch im Kundenfeld.
            var card = s.Find(Class("AppointmentCard", e => e.Properties.Name.ValueOrDefault is { } name
                                                          && name.Contains($" {customer}, {Treatment}, Gebucht", StringComparison.Ordinal)),
                $"Kalenderkarte „{customer}, {Treatment}“");
            var shown = ShownDate(s);
            context.Appointment = new BookedAppointment(customer, Treatment, card.Properties.Name.ValueOrDefault![..5], shown,
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
        s.Step("Auf gestern gehen: „Heute“, dann „Vortag“", () =>
        {
            s.Click(Button("Heute"), "Knopf „Heute“");
            s.Click("Vortag");
        });
        s.Step("Freie Stelle anklicken: kein Termin in der Vergangenheit", () =>
        {
            var anyCard = s.Find(Class("AppointmentCard", _ => true), "Eine Karte von gestern").BoundingRectangle;
            var spot = FreePointInColumn(s, anyCard, betweenCards: true);
            LiveSession.ClickAt(spot);
            s.Expect(Toast("Termine lassen sich nicht in der Vergangenheit eintragen"), "Meldung „Termine lassen sich nicht in der Vergangenheit eintragen“");
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

    private static void SelectCard(LiveSession s, BookedAppointment booked, string status) =>
        s.Click(Card(booked, status), $"Karte „{booked.CardPrefix}“ mit Status {status}");

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
            s.Expect(Toast($"{booked.Customer} eingecheckt"), $"Meldung „{booked.Customer} eingecheckt“");
            s.Expect(Card(booked, "Eingecheckt"), "Karte mit Status „Eingecheckt“");
        });
        s.Step("„Rückgängig“: Karte wieder „Gebucht“", () =>
        {
            s.Click(Class("Button", NameContains("Rückgängig")), "Knopf „Rückgängig“");
            s.Expect(Card(booked, "Gebucht"), "Karte mit Status „Gebucht“");
        });
    }

    private static void Late(LiveSession s, LiveRunContext context)
    {
        var booked = context.Appointment!;
        OpenCalendar(s);
        s.Step("Rechtsklick auf die Karte: „Kommt 10 Min. später“", () =>
        {
            s.RightClick(s.Find(Card(booked, "Gebucht"), "Karte mit Status „Gebucht“"));
            s.ChooseMenuItem("Kommt 10 Min. später");
            s.Expect(Toast($"{booked.Customer} kommt 10 Min. später"), $"Meldung „{booked.Customer} kommt 10 Min. später“");
        });
        s.Step("„Doch pünktlich“ nimmt die Verspätung zurück", () =>
        {
            s.RightClick(s.Find(Card(booked), "Karte des Termins"));
            s.ChooseMenuItem("Doch pünktlich");
            s.Expect(Toast($"{booked.Customer} kommt doch pünktlich"), "Meldung „kommt doch pünktlich“");
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

    private static void NoShowAfterCheckIn(LiveSession s, LiveRunContext context)
    {
        var booked = context.Appointment!;
        RequireToday(booked);
        OpenCalendar(s);
        s.Step("Termin einchecken", () =>
        {
            SelectCard(s, booked, "Gebucht");
            s.Click(Button("Einchecken"), "Knopf „Einchecken“");
            s.Expect(Card(booked, "Eingecheckt"), "Karte mit Status „Eingecheckt“");
        });
        s.Step("Mehr aufklappen: „Nicht erschienen“ ist gesperrt", () =>
        {
            s.Click("MoreToggle");
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
        s.Expect(Toast("Verschoben auf"), "Meldung „Verschoben auf …“");
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
            s.Expect(Toast("Termin storniert"), "Meldung „Termin storniert“");
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
            s.Expect(Toast("Termin storniert"), "Meldung „Termin storniert“");
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
